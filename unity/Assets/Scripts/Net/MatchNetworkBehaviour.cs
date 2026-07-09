using System;
using System.Collections.Generic;
using AccentGuesser.Core;
using AccentGuesser.Services;
using Unity.Netcode;
using UnityEngine;

namespace AccentGuesser.Net
{
    /// <summary>
    /// The single netcode seam between clients and the pure <see cref="MatchController"/>
    /// (design spec §"Networking contract"). Host-authoritative:
    ///
    ///  • The HOST (and only the host) owns one <see cref="MatchController"/> and the hidden target.
    ///  • Clients send intents via ServerRpc (lock a guess, ask the Keep).
    ///  • The host broadcasts a redacted <see cref="RoundView"/>, the public hint, and — only at
    ///    REVEAL — the answer, via ClientRpc.
    ///
    /// Clients construct no MatchController, no oracle, and never receive the target before REVEAL,
    /// so answer secrecy is structural, not UI-gated.
    ///
    /// Presentation (App layer) subscribes to <see cref="OnRoundView"/>, <see cref="OnHint"/>,
    /// and <see cref="OnReveal"/>; it never touches the host state directly.
    /// </summary>
    public sealed class MatchNetworkBehaviour : NetworkBehaviour
    {
        [SerializeField] private float _roundSeconds = 30f;
        [SerializeField] private float _revealSeconds = 8f;
        [SerializeField] private string _difficulty = "";
        [SerializeField] private string _region = "";

        [Tooltip("Lobby flow: wait in a lobby, host presses start, everyone picks an avatar, THEN " +
                 "rounds begin. Off = legacy auto-start (the IMGUI test scene).")]
        [SerializeField] private bool _lobby = false;

        /// <summary>Hard cap — the tavern has four seats. Extra joiners are turned away.</summary>
        public const int MaxPlayers = 4;

        // ---- Client-facing events (fired from ClientRpc handlers) ----
        public event Action<RoundView> OnRoundView;
        public event Action<string, string, string> OnHint; // (asker name, question, answer)
        public event Action<RoundResultView> OnReveal;

        /// <summary>The host pressed Start — every player should pick an avatar now.</summary>
        public event Action OnAvatarSelect;

        /// <summary>A player locked their avatar choice (playerId, avatar index).</summary>
        public event Action<string, int> OnAvatarPicked;

        /// <summary>All picks are in: avatar index per SEAT (roster order). Rounds begin next.</summary>
        public event Action<int[]> OnAvatarsLocked;

        // ---- Host-only state ----
        private MatchController _match;
        private IOracleClient _oracle;
        private readonly Dictionary<ulong, string> _names = new Dictionary<ulong, string>();
        private readonly List<ulong> _joinOrder = new List<ulong>();   // roster/seat order
        private readonly Dictionary<ulong, int> _avatarPicks = new Dictionary<ulong, int>();
        private double _timerDeadline;
        private double _nextRoundAt;
        private bool _awaitingNextRound;
        private bool _needFirstRound;
        private bool _needLobbyView;
        private bool _gameStarted;   // host pressed Start (lobby closed, avatar select underway)

        private static string Pid(ulong clientId) => clientId.ToString();
        private double Now => NetworkManager.ServerTime.Time;

        /// <summary>
        /// HOST-ONLY injection of the rules dependencies. Clients never call this, so they hold no
        /// catalog and no oracle. Usually runs BEFORE the host starts; when this object spawns with
        /// the network already live (the tavern scene loaded from the menu lobby), it runs just
        /// after — so any roster gathered at spawn is backfilled into the fresh controller here.
        /// </summary>
        public void ConfigureHost(IClipCatalog catalog, IOracleClient oracle, System.Random rng)
        {
            _match = new MatchController(catalog, rng);
            _oracle = oracle;
            foreach (ulong clientId in _joinOrder)
            {
                try { _match.AddPlayer(Pid(clientId), _names[clientId]); }
                catch (ArgumentException) { /* already present */ }
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            NetworkManager.OnClientConnectedCallback += HandleConnected;
            NetworkManager.OnClientDisconnectCallback += HandleDisconnected;

            // Everyone already seated joins the roster — that's just the host on a fresh listen
            // server, but it is the WHOLE menu lobby when this scene-placed object spawns after a
            // NetworkSceneManager scene switch (those clients connected long ago, so
            // HandleConnected will never fire for them). Defer the first broadcast to Update:
            // sending a ClientRpc during OnNetworkSpawn is too early and the message can be dropped.
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                AddIfKnown(clientId);
            if (_lobby) _needLobbyView = true;   // wait in the lobby for the host to press Start
            else _needFirstRound = true;         // legacy: deal immediately
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            NetworkManager.OnClientConnectedCallback -= HandleConnected;
            NetworkManager.OnClientDisconnectCallback -= HandleDisconnected;
        }

        // ---- Host: roster wiring ---------------------------------------------

        private void HandleConnected(ulong clientId)
        {
            // The tavern has four seats, and a lobby that has started is closed to newcomers.
            if (!_names.ContainsKey(clientId) &&
                (_names.Count >= MaxPlayers || (_lobby && _gameStarted)))
            {
                NetworkManager.DisconnectClient(clientId);
                return;
            }
            AddIfKnown(clientId);
            PushView();
        }

        private void HandleDisconnected(ulong clientId)
        {
            _names.Remove(clientId);
            _joinOrder.Remove(clientId);
            _avatarPicks.Remove(clientId);
            if (_match == null) return;
            _match.RemovePlayer(Pid(clientId));
            PushView();
            if (_match.Phase == MatchPhase.Reveal) ScheduleNextRound();
            // If the match was waiting on this player's avatar pick, it may be complete now.
            if (_lobby && _gameStarted && _match.RoundNumber == 0) TryBeginRounds();
        }

        private void AddIfKnown(ulong clientId)
        {
            string id = Pid(clientId);
            if (!_names.TryGetValue(clientId, out var name))
            {
                name = clientId == NetworkManager.LocalClientId ? "Host" : $"Player {clientId}";
                _names[clientId] = name;
                _joinOrder.Add(clientId);
            }
            if (_match == null) return; // spawned before ConfigureHost — backfilled there
            try { _match.AddPlayer(id, name); }
            catch (ArgumentException) { /* already present (reconnect) */ }
        }

        // ---- Host: round driving ---------------------------------------------

        private void StartNextRound()
        {
            _awaitingNextRound = false;
            _match.StartRound(NullIfEmpty(_difficulty), NullIfEmpty(_region));
            _timerDeadline = Now + _roundSeconds;
            PushView();
        }

        private void ScheduleNextRound()
        {
            _awaitingNextRound = true;
            _nextRoundAt = Now + _revealSeconds;
        }

        private void Update()
        {
            if (!IsServer || _match == null) return;

            if (_needFirstRound)
            {
                _needFirstRound = false;
                StartNextRound();
                return;
            }

            if (_needLobbyView)
            {
                _needLobbyView = false;
                PushView(); // Setup-phase roster snapshot = the lobby list
                return;
            }

            if (_match.Phase == MatchPhase.Listen && Now >= _timerDeadline)
            {
                _match.ExpireTimer();
                if (_match.Phase == MatchPhase.Reveal) EnterReveal();
            }
            else if (_awaitingNextRound && Now >= _nextRoundAt)
            {
                if (_match.Roster.Count > 0) StartNextRound();
                else _awaitingNextRound = false; // nobody left; wait for a connect
            }
        }

        // ---- Lobby → avatar select → rounds (lobby mode only) -----------------

        /// <summary>
        /// HOST-ONLY: close the lobby and send everyone to avatar selection. Plain method (not an
        /// RPC) because only the host's UI shows a Start button — clients cannot trigger it.
        /// </summary>
        public void HostStartAvatarSelect()
        {
            if (!IsServer || !_lobby || _gameStarted) return;
            _gameStarted = true;
            AvatarSelectClientRpc();
        }

        /// <summary>
        /// A player locked their avatar. Duplicates are allowed by design (two giraffes are two
        /// giraffes). When every seated player has picked, the match deals round one.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void PickAvatarServerRpc(int avatar, ServerRpcParams p = default)
        {
            if (!_gameStarted || _match == null || _match.RoundNumber > 0) return; // picks only during selection
            if (avatar < 0 || avatar > 4) return;
            ulong sender = p.Receive.SenderClientId;
            if (_avatarPicks.ContainsKey(sender)) return;          // first pick is final
            _avatarPicks[sender] = avatar;
            AvatarPickedClientRpc(Pid(sender), avatar);
            TryBeginRounds();
        }

        /// <summary>Once every rostered player has an avatar, broadcast seat→avatar and deal.</summary>
        private void TryBeginRounds()
        {
            if (_match.RoundNumber > 0) return;
            var roster = _match.Roster;
            if (roster.Count == 0) return;

            var avatars = new int[roster.Count];
            for (int i = 0; i < roster.Count; i++)
            {
                if (!ulong.TryParse(roster[i].Id, out ulong cid) ||
                    !_avatarPicks.TryGetValue(cid, out avatars[i]))
                    return; // someone is still choosing
            }

            AvatarsLockedClientRpc(avatars);
            StartNextRound();
        }

        [ClientRpc]
        private void AvatarSelectClientRpc() => OnAvatarSelect?.Invoke();

        [ClientRpc]
        private void AvatarPickedClientRpc(string playerId, int avatar) =>
            OnAvatarPicked?.Invoke(playerId, avatar);

        [ClientRpc]
        private void AvatarsLockedClientRpc(int[] avatarsBySeat) =>
            OnAvatarsLocked?.Invoke(avatarsBySeat);

        // ---- Host: intents from clients --------------------------------------

        [ServerRpc(RequireOwnership = false)]
        public void LockGuessServerRpc(string guess, ServerRpcParams p = default)
        {
            if (_match == null) return;
            if (_match.LockGuess(Pid(p.Receive.SenderClientId), guess))
            {
                if (_match.Phase == MatchPhase.Reveal) EnterReveal();
                else PushView();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void AskQuestionServerRpc(string question, ServerRpcParams p = default)
        {
            ulong sender = p.Receive.SenderClientId;
            // Everyone gets one question per round; repeat asks and post-lock asks are rejected.
            if (_match == null || !_match.MarkAsked(Pid(sender))) return;
            PushView();
            string asker = _names.TryGetValue(sender, out var name) ? name : "Someone";
            _ = ResolveHint(asker, question);
        }

        private async System.Threading.Tasks.Task ResolveHint(string asker, string question)
        {
            string answer = _oracle != null
                ? await _oracle.AskAsync(question, _match.CurrentClip)
                : "The Keep says nothing.";

            if (_match.Phase != MatchPhase.Listen) return; // round already resolved
            BroadcastHintClientRpc(asker, question, answer); // the whole table hears every answer
        }

        // ---- Host → clients ---------------------------------------------------

        private void PushView()
        {
            if (_match == null) return; // between spawn and ConfigureHost (menu → tavern arrival)
            var roster = _match.Roster;
            var views = new PlayerView[roster.Count];
            for (int i = 0; i < roster.Count; i++)
            {
                var pl = roster[i];
                views[i] = new PlayerView
                {
                    Id = pl.Id, Name = pl.DisplayName,
                    Score = pl.Score, Streak = pl.Streak,
                    HasLocked = pl.HasLocked, HasAsked = pl.HasAsked
                };
            }

            var view = new RoundView
            {
                Phase = (NetPhase)(byte)_match.Phase,
                RoundNumber = _match.RoundNumber,
                ClipId = _match.CurrentClip?.file ?? "",
                TimerDeadline = _timerDeadline,
                Roster = views
            };
            RoundViewClientRpc(view);
        }

        private void EnterReveal()
        {
            var roster = _match.Roster;
            var results = new PlayerResultView[roster.Count];
            for (int i = 0; i < roster.Count; i++)
            {
                var pl = roster[i];
                results[i] = new PlayerResultView
                {
                    Id = pl.Id,
                    Guess = pl.Guess ?? "",
                    Correct = pl.LastResult.Correct,
                    Points = pl.LastResult.Points,
                    NewScore = pl.Score
                };
            }

            var payload = new RoundResultView
            {
                TargetLanguage = _match.Target?.Language ?? "",
                TargetCountry = _match.Target?.Country ?? "",
                Results = results
            };

            PushView();                 // roster/phase update (still no target here)
            RevealClientRpc(payload);   // the ONLY place the answer crosses the wire
            ScheduleNextRound();
        }

        [ClientRpc]
        private void RoundViewClientRpc(RoundView view) => OnRoundView?.Invoke(view);

        [ClientRpc]
        private void BroadcastHintClientRpc(string asker, string question, string answer) =>
            OnHint?.Invoke(asker, question, answer);

        [ClientRpc]
        private void RevealClientRpc(RoundResultView result) => OnReveal?.Invoke(result);

        private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
