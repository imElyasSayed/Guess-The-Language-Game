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

        // ---- Client-facing events (fired from ClientRpc handlers) ----
        public event Action<RoundView> OnRoundView;
        public event Action<string, string> OnHint;         // (question, answer)
        public event Action<RoundResultView> OnReveal;

        // ---- Host-only state ----
        private MatchController _match;
        private IOracleClient _oracle;
        private readonly Dictionary<ulong, string> _names = new Dictionary<ulong, string>();
        private double _timerDeadline;
        private double _nextRoundAt;
        private bool _awaitingNextRound;
        private bool _needFirstRound;

        private static string Pid(ulong clientId) => clientId.ToString();
        private double Now => NetworkManager.ServerTime.Time;

        /// <summary>
        /// HOST-ONLY injection of the rules dependencies. Call before/at host start. Clients never
        /// call this, so they hold no catalog and no oracle.
        /// </summary>
        public void ConfigureHost(IClipCatalog catalog, IOracleClient oracle, System.Random rng)
        {
            _match = new MatchController(catalog, rng);
            _oracle = oracle;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            NetworkManager.OnClientConnectedCallback += HandleConnected;
            NetworkManager.OnClientDisconnectCallback += HandleDisconnected;

            // The host is also a player (listen-server). Defer the first round to Update: sending a
            // ClientRpc during OnNetworkSpawn is too early and the message can be dropped.
            AddIfKnown(NetworkManager.LocalClientId);
            _needFirstRound = true;
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
            AddIfKnown(clientId);
            PushView();
        }

        private void HandleDisconnected(ulong clientId)
        {
            _match.RemovePlayer(Pid(clientId));
            _names.Remove(clientId);
            PushView();
            if (_match.Phase == MatchPhase.Reveal) ScheduleNextRound();
        }

        private void AddIfKnown(ulong clientId)
        {
            string id = Pid(clientId);
            if (!_names.TryGetValue(clientId, out var name))
            {
                name = clientId == NetworkManager.LocalClientId ? "Host" : $"Player {clientId}";
                _names[clientId] = name;
            }
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

        // ---- Host: intents from clients --------------------------------------

        [ServerRpc(RequireOwnership = false)]
        public void LockGuessServerRpc(string guess, ServerRpcParams p = default)
        {
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
            if (!_match.MarkAsked(Pid(sender))) return; // rejects non-askers / repeat asks
            PushView();
            _ = ResolveHint(question);
        }

        private async System.Threading.Tasks.Task ResolveHint(string question)
        {
            string answer = _oracle != null
                ? await _oracle.AskAsync(question, _match.CurrentClip)
                : "The Keep says nothing.";

            if (_match.Phase != MatchPhase.Listen) return; // round already resolved
            _match.PublishHint();
            BroadcastHintClientRpc(question, answer);
            PushView();
        }

        // ---- Host → clients ---------------------------------------------------

        private void PushView()
        {
            var roster = _match.Roster;
            var views = new PlayerView[roster.Count];
            for (int i = 0; i < roster.Count; i++)
            {
                var pl = roster[i];
                views[i] = new PlayerView
                {
                    Id = pl.Id, Name = pl.DisplayName,
                    Score = pl.Score, Streak = pl.Streak, HasLocked = pl.HasLocked
                };
            }

            var view = new RoundView
            {
                Phase = (NetPhase)(byte)_match.Phase,
                RoundNumber = _match.RoundNumber,
                ClipId = _match.CurrentClip?.file ?? "",
                AskerId = _match.AskerId ?? "",
                Asked = _match.Asked,
                HintPublic = _match.HintPublic,
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
        private void BroadcastHintClientRpc(string question, string answer) => OnHint?.Invoke(question, answer);

        [ClientRpc]
        private void RevealClientRpc(RoundResultView result) => OnReveal?.Invoke(result);

        private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
