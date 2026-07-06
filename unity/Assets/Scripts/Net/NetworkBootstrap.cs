using System.Collections.Generic;
using System.IO;
using AccentGuesser.Core;
using AccentGuesser.Services;
using Unity.Netcode;
using UnityEngine;

namespace AccentGuesser.Net
{
    /// <summary>
    /// Zero-prefab test driver for multiplayer (design spec §"Testing"): an IMGUI lobby + HUD so
    /// two people can play a full match without any uGUI wiring. This is throwaway scaffolding —
    /// the 3D tavern presentation replaces it later, exactly like <c>GameManager</c>'s placeholder UI.
    ///
    /// Put this on the same GameObject as the scene's <see cref="MatchNetworkBehaviour"/>
    /// (a NetworkObject) alongside a NetworkManager+UnityTransport. Press Host, share the code,
    /// friend presses Join.
    /// </summary>
    [RequireComponent(typeof(MatchNetworkBehaviour))]
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string _manifestFile = "clips.json";
        [Tooltip("If set, host uses the real relay oracle; else the offline MockOracleClient.")]
        [SerializeField] private string _oracleRelayBaseUrl = "";
        [Tooltip("Use Unity Relay (join code). Off = direct IP for LAN / same-machine tests.")]
        [SerializeField] private bool _useRelay = true;

        private MatchNetworkBehaviour _match;
        private AudioService _audio;
        private IConnectionManager _conn;

        // client-side render state
        private RoundView _view;
        private bool _haveView;
        private string _hint = "";
        private string _reveal = "";
        private string _joinCodeToShare = "";
        private string _joinInput = "";
        private string _guessInput = "";
        private string _questionInput = "";
        private string _status = "";

        private void Awake()
        {
            _match = GetComponent<MatchNetworkBehaviour>();
            _audio = gameObject.AddComponent<AudioService>();
            _conn = _useRelay ? new RelayConnectionManager() : (IConnectionManager)new DirectConnectionManager();

            _match.OnRoundView += v => { _view = v; _haveView = true; if (v.Phase == NetPhase.Listen) { _hint = ""; _reveal = ""; } };
            _match.OnHint += (q, a) => _hint = $"Asker asked: “{q}”\nThe Keep: “{a}”";
            _match.OnReveal += RenderReveal;
        }

        // ---- Host wiring: inject catalog + oracle BEFORE the network starts ----

        private void ConfigureHostIfPossible()
        {
            if (!TryLoadCatalog(out var catalog, out string err))
            {
                _status = $"Failed to load {_manifestFile}: {err}";
                return;
            }
            IOracleClient oracle = string.IsNullOrEmpty(_oracleRelayBaseUrl)
                ? new MockOracleClient()
                : new HttpOracleClient(_oracleRelayBaseUrl);
            _match.ConfigureHost(catalog, oracle, new System.Random());
        }

        private bool TryLoadCatalog(out IClipCatalog catalog, out string error)
        {
            catalog = null; error = null;
            string path = Path.Combine(Application.streamingAssetsPath, _manifestFile);
            try
            {
                if (!File.Exists(path)) { error = "file not found"; return false; }
                var clips = ParseClips(File.ReadAllText(path));
                catalog = new JsonClipCatalog(clips);
                return clips.Count > 0;
            }
            catch (System.Exception e) { error = e.Message; return false; }
        }

        [System.Serializable] private class Wrapper { public List<ClipInfo> clips; }
        private static List<ClipInfo> ParseClips(string json)
        {
            string trimmed = json.TrimStart();
            string wrapped = trimmed.StartsWith("[") ? "{\"clips\":" + json + "}" : json;
            var w = JsonUtility.FromJson<Wrapper>(wrapped);
            return w?.clips ?? new List<ClipInfo>();
        }

        // ---- Reveal text -----------------------------------------------------

        private void RenderReveal(RoundResultView r)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"It was {r.TargetLanguage} ({r.TargetCountry}).");
            foreach (var pr in r.Results)
            {
                string verdict = pr.Correct ? $"correct +{pr.Points}" : "wrong";
                sb.AppendLine($"  {pr.Id}: “{pr.Guess}” → {verdict} (score {pr.NewScore})");
            }
            _reveal = sb.ToString();
        }

        // ---- IMGUI lobby + HUD ------------------------------------------------

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 560, Screen.height - 40));
            GUI.skin.label.fontSize = 15;

            var nm = NetworkManager.Singleton;
            bool connected = nm != null && (nm.IsClient || nm.IsServer);

            if (!connected) DrawLobby();
            else DrawHud();

            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);
            GUILayout.EndArea();
        }

        private void DrawLobby()
        {
            GUILayout.Label("Say Again? — Multiplayer test");
            _useRelay = GUILayout.Toggle(_useRelay, "Use Unity Relay (off = direct IP / LAN)");
            _conn = _useRelay ? new RelayConnectionManager() : (IConnectionManager)new DirectConnectionManager();

            if (GUILayout.Button("Host game", GUILayout.Height(36)))
            {
                ConfigureHostIfPossible();
                _status = "Starting host...";
                _ = HostFlow();
            }

            GUILayout.Space(8);
            GUILayout.Label(_useRelay ? "Join code:" : "Host ip:port:");
            _joinInput = GUILayout.TextField(_joinInput, GUILayout.Height(28));
            if (GUILayout.Button("Join", GUILayout.Height(36)))
            {
                _status = "Joining...";
                _ = JoinFlow();
            }

            if (!string.IsNullOrEmpty(_joinCodeToShare))
                GUILayout.Label($"Share this: {_joinCodeToShare}");
        }

        private async System.Threading.Tasks.Task HostFlow()
        {
            _joinCodeToShare = await _conn.HostAsync();
            _status = string.IsNullOrEmpty(_joinCodeToShare) ? "Host failed." : "Hosting.";
        }

        private async System.Threading.Tasks.Task JoinFlow()
        {
            bool ok = await _conn.JoinAsync(_joinInput);
            _status = ok ? "Connected." : "Join failed.";
        }

        private void DrawHud()
        {
            if (!string.IsNullOrEmpty(_joinCodeToShare))
                GUILayout.Label($"Join code: {_joinCodeToShare}");

            if (!_haveView) { GUILayout.Label("Waiting for the first round..."); return; }

            string me = NetworkManager.Singleton.LocalClientId.ToString();
            bool iAmAsker = _view.AskerId == me;

            GUILayout.Label($"Round {_view.RoundNumber} — {_view.Phase}");
            GUILayout.Label("Players:");
            foreach (var p in _view.Roster)
            {
                string tag = p.Id == _view.AskerId ? " (asker)" : "";
                string locked = p.HasLocked ? " [locked]" : "";
                GUILayout.Label($"   {p.Name}{tag}: {p.Score}  streak {p.Streak}{locked}");
            }

            if (_view.Phase == NetPhase.Listen)
            {
                if (GUILayout.Button("Play clip"))
                    _audio.PlayClip(_view.ClipId, onError: e => _status = $"clip error: {e}");

                if (!string.IsNullOrEmpty(_hint)) GUILayout.Label(_hint);

                if (iAmAsker && !_view.Asked)
                {
                    GUILayout.Label("You are the asker — ask the Keep ONE question:");
                    _questionInput = GUILayout.TextField(_questionInput, GUILayout.Height(26));
                    if (GUILayout.Button("Ask the Keep"))
                    {
                        _match.AskQuestionServerRpc(_questionInput);
                        _questionInput = "";
                    }
                }

                GUILayout.Space(6);
                GUILayout.Label("Your guess (lock early = +15, after the hint = +10):");
                _guessInput = GUILayout.TextField(_guessInput, GUILayout.Height(26));
                if (GUILayout.Button("Lock guess", GUILayout.Height(32)))
                {
                    _match.LockGuessServerRpc(_guessInput);
                    _guessInput = "";
                }
            }
            else if (_view.Phase == NetPhase.Reveal)
            {
                GUILayout.Label(string.IsNullOrEmpty(_reveal) ? "Revealing..." : _reveal);
                GUILayout.Label("Next round starting shortly...");
            }
        }
    }
}
