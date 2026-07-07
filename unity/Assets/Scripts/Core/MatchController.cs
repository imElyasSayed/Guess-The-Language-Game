using System;
using System.Collections.Generic;

namespace AccentGuesser.Core
{
    /// <summary>The three phases of a multiplayer round (design spec §"Round lifecycle").</summary>
    public enum MatchPhase
    {
        Setup,
        Listen,
        Reveal
    }

    /// <summary>
    /// Pure, MonoBehaviour-free, netcode-free host-authoritative match state machine
    /// (see docs/superpowers/specs/2026-07-06-multiplayer-design.md). Generalizes the
    /// single-player <see cref="GameController"/> from one player to a roster of N (2–8),
    /// and is the ONLY holder of the hidden <see cref="Target"/>.
    ///
    /// This class NEVER lives on a client: the network layer runs one instance on the host,
    /// takes client intents (LockGuess / MarkAsked) in, and broadcasts a redacted view out.
    /// Single-player is just a roster of one running locally.
    ///
    /// Lifecycle: Setup --StartRound--> Listen --(all locked | ExpireTimer)--> Reveal --StartRound--> Listen ...
    ///
    /// Scoring (per-player "Trust Your Ear"):
    ///   locked BEFORE the hint + correct => +15   (LockedBeforeHint == true)
    ///   locked AFTER  the hint + correct => +10
    ///   wrong or never locked            =>  +0 and the streak resets.
    /// </summary>
    public sealed class MatchController
    {
        private readonly IClipCatalog _catalog;
        private readonly Random _rng;
        private readonly List<Player> _roster = new List<Player>();
        private readonly List<Player> _pending = new List<Player>();
        private int _askerRotation;

        public MatchController(IClipCatalog catalog, Random rng)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        // ---- State ------------------------------------------------------------
        public MatchPhase Phase { get; private set; } = MatchPhase.Setup;
        public int RoundNumber { get; private set; }

        /// <summary>The active roster (excludes joiners still pending until the next round).</summary>
        public IReadOnlyList<Player> Roster => _roster;

        public ClipInfo CurrentClip { get; private set; }

        /// <summary>The hidden answer. HOST-ONLY — never replicate this to clients before REVEAL.</summary>
        public Lang Target { get; private set; }

        /// <summary>Id of the player who may ask the Keep this round.</summary>
        public string AskerId { get; private set; }

        /// <summary>True once the asker has consumed the round's single question.</summary>
        public bool Asked { get; private set; }

        /// <summary>True once the hint has been broadcast; flips the lock tier from +15 to +10.</summary>
        public bool HintPublic { get; private set; }

        // ---- Roster management ------------------------------------------------

        /// <summary>
        /// Add a player. In SETUP/REVEAL they join the active roster immediately; during a live
        /// LISTEN they are held pending and enter at the next <see cref="StartRound"/> (drop-in
        /// joiners cannot join mid-round). Throws if the id is already present.
        /// </summary>
        public Player AddPlayer(string id, string displayName)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Player id required.", nameof(id));
            if (Find(id) != null || FindPending(id) != null)
                throw new ArgumentException($"Player '{id}' is already in the match.", nameof(id));

            var p = new Player(id, displayName);
            if (Phase == MatchPhase.Listen) _pending.Add(p);
            else _roster.Add(p);
            return p;
        }

        /// <summary>
        /// Remove a player (disconnect or leave). Returns false if unknown. If the player leaves
        /// mid-LISTEN they stop gating REVEAL, so this may trigger REVEAL when the remaining
        /// players have all locked. A leaving asker who had not asked simply forfeits the hint.
        /// </summary>
        public bool RemovePlayer(string id)
        {
            var pending = FindPending(id);
            if (pending != null) { _pending.Remove(pending); return true; }

            var p = Find(id);
            if (p == null) return false;
            _roster.Remove(p);

            if (Phase == MatchPhase.Listen) MaybeReveal();
            return true;
        }

        // ---- Transitions ------------------------------------------------------

        /// <summary>
        /// Begin a new round. Valid from SETUP or REVEAL. Merges pending joiners, draws a clip +
        /// hidden target, assigns the rotating asker, resets per-round state, and enters LISTEN.
        /// Throws if a round is already live or the roster is empty.
        /// </summary>
        public void StartRound(string difficulty = null, string region = null)
        {
            if (Phase == MatchPhase.Listen)
                throw new InvalidOperationException("A round is already in progress.");

            if (_pending.Count > 0) { _roster.AddRange(_pending); _pending.Clear(); }
            if (_roster.Count < 1)
                throw new InvalidOperationException("At least one player is required to start a round.");

            var round = RoundFactory.CreateRound(_catalog, new ClipFilter(difficulty, region), _rng);
            CurrentClip = round.Clip;
            Target = round.Target;

            AskerId = _roster[_askerRotation % _roster.Count].Id;
            _askerRotation++;

            Asked = false;
            HintPublic = false;
            foreach (var p in _roster) p.ResetForRound();

            RoundNumber++;
            Phase = MatchPhase.Listen;
        }

        /// <summary>
        /// Consume the round's single question on behalf of the asker. Returns false if not in
        /// LISTEN, already asked, or the caller is not the asker (one-question, asker-only lock).
        /// The host then fetches the oracle answer and calls <see cref="PublishHint"/>.
        /// </summary>
        public bool MarkAsked(string playerId)
        {
            if (Phase != MatchPhase.Listen || Asked) return false;
            if (playerId != AskerId) return false;
            Asked = true;
            return true;
        }

        /// <summary>
        /// Flip the round into the "hint is public" state — called by the host once the oracle
        /// answer has been broadcast to the table. Guesses locked from now on score the +10 tier.
        /// No-op outside LISTEN.
        /// </summary>
        public void PublishHint()
        {
            if (Phase != MatchPhase.Listen) return;
            HintPublic = true;
        }

        /// <summary>
        /// Lock a player's guess for this round. First lock is final; later locks are ignored.
        /// The tier (+15 vs +10) is fixed here by whether the hint is public yet. Returns false
        /// outside LISTEN, for an unknown player, or on a repeat lock. May trigger REVEAL when
        /// this is the last player to lock.
        /// </summary>
        public bool LockGuess(string playerId, string guess)
        {
            if (Phase != MatchPhase.Listen) return false;
            var p = Find(playerId);
            if (p == null || p.HasLocked) return false;

            p.HasLocked = true;
            p.Guess = guess ?? string.Empty;
            p.LockedBeforeHint = !HintPublic;

            MaybeReveal();
            return true;
        }

        /// <summary>
        /// Force the round to REVEAL because the per-round timer expired. Players who never locked
        /// score +0 and reset their streak. No-op outside LISTEN.
        /// </summary>
        public void ExpireTimer()
        {
            if (Phase != MatchPhase.Listen) return;
            EnterReveal();
        }

        // ---- Internals --------------------------------------------------------

        private void MaybeReveal()
        {
            if (_roster.Count == 0) return;
            if (_roster.TrueForAll(p => p.HasLocked)) EnterReveal();
        }

        private void EnterReveal()
        {
            foreach (var p in _roster)
            {
                bool correct = p.HasLocked
                    && p.Guess.Trim().Equals(Target.Language, StringComparison.OrdinalIgnoreCase);
                // After-hint lock counts as "asked" (+10); before-hint counts as "not asked" (+15).
                bool asked = p.HasLocked && !p.LockedBeforeHint;

                var result = ScoreCalculator.Evaluate(correct, asked, p.Streak);
                p.Score += result.Points;
                p.Streak = result.NewStreak;
                p.LastResult = result;
            }
            Phase = MatchPhase.Reveal;
        }

        private Player Find(string id) => _roster.Find(p => p.Id == id);
        private Player FindPending(string id) => _pending.Find(p => p.Id == id);
    }
}
