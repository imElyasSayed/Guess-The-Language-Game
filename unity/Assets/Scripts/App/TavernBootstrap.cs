using AccentGuesser.Core;
using AccentGuesser.Net;
using AccentGuesser.Services;
using UnityEngine;

namespace AccentGuesser.App
{
    /// <summary>
    /// The tavern's front door: builds the shared HUD/stage/audio, parks the camera on the wide
    /// selection view, and boots the right presenter for however the player got here.
    ///
    /// The usual way in is now the MAIN MENU scene, which either flags a solo game via
    /// <see cref="MenuSelection"/> or arrives with the network already live (the menu lobby walked
    /// everyone here through <c>NetworkManager.SceneManager</c>):
    ///
    ///  • Solo         → <see cref="TavernPresenter"/> (local <c>GameController</c>, avatar select).
    ///  • Menu host    → re-injects catalog + oracle into THIS scene's fresh
    ///                   <see cref="MatchNetworkBehaviour"/>, triggers the avatar select once it
    ///                   spawns, and hands rendering to <see cref="MatchTavernPresenter"/>.
    ///  • Menu client  → hands rendering to <see cref="MatchTavernPresenter"/> straight at the
    ///                   avatar select; never holds a catalog, an oracle, or the answer.
    ///
    /// Played directly in-editor (no menu), the original in-tavern mode panel still offers
    /// solo / host / join as a fallback, transport-agnostic via <see cref="IConnectionManager"/>.
    /// Added to Tavern.unity by the scene builder, so the scene stays regenerable.
    /// </summary>
    public sealed class TavernBootstrap : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("clips.json path relative to StreamingAssets (host/solo only).")]
        [SerializeField] private string _manifestFile = "clips.json";

        [Tooltip("If set, the host uses the real oracle relay; otherwise the offline MockOracleClient.")]
        [SerializeField] private string _oracleRelayBaseUrl = "";

        [Tooltip("Use Unity Relay (share a join code). Off = direct IP for LAN / same-machine.")]
        [SerializeField] private bool _useRelay = false;

        private TavernHud.Refs _ui;
        private TavernStage _stage;
        private AudioService _audio;
        private IConnectionManager _conn;
        private bool _busy;

        private void Start()
        {
            _ui = TavernHud.Build(transform);
            _stage = new TavernStage();
            _stage.Initialize();
            _audio = gameObject.AddComponent<AudioService>();
            _conn = _useRelay ? (IConnectionManager)new RelayConnectionManager() : new DirectConnectionManager();

            // Wide view of the table while choosing how (and later, who) to play.
            var cam = Camera.main;
            if (cam != null)
            {
                TavernSeating.SelectionPose(out var pos, out var rot);
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.fieldOfView = TavernSeating.SelectionFieldOfView;
                cam.nearClipPlane = 0.05f;
            }

            _ui.ConsoleRoot.SetActive(false);
            _ui.AvatarPanel.SetActive(false);
            _ui.ModePanel.SetActive(false);

            // Esc → Back to the Table / Leave the Table / Quit, on every way into the tavern.
            gameObject.AddComponent<TavernEscapeMenu>();

            // Arrivals from the main menu skip the in-tavern mode panel entirely.
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                BootFromMenuLobby(nm.IsHost);
                return;
            }
            if (MenuSelection.ConsumeSolo())
            {
                StartSolo();
                return;
            }

            // Fallback: Tavern.unity opened directly (in-editor) — offer the modes here.
            _ui.ModePanel.SetActive(true);
            _ui.SoloButton.onClick.AddListener(StartSolo);
            _ui.HostButton.onClick.AddListener(() => { if (!_busy) _ = StartHost(); });
            _ui.JoinButton.onClick.AddListener(() => { if (!_busy) _ = StartJoin(); });
        }

        private void StartSolo()
        {
            _ui.ModePanel.SetActive(false);
            gameObject.AddComponent<TavernPresenter>().Boot(_ui, _stage, _audio);
        }

        /// <summary>
        /// The menu lobby already gathered everyone; the scene switch landed us here with the
        /// network live. Hosts re-arm this scene's fresh Match (rules + catalog + oracle live
        /// host-side only) and send the table straight to avatar selection.
        /// </summary>
        private void BootFromMenuLobby(bool isHost)
        {
            var match = FindFirstObjectByType<MatchNetworkBehaviour>();
            if (match == null)
            {
                _ui.ModePanel.SetActive(true);
                _ui.ModeStatus.text = "No Match object in the scene — rebuild it.";
                return;
            }

            string joinCode = MenuSelection.ConsumeJoinCode();
            if (isHost)
            {
                if (!ClipCatalogLoader.TryLoad(_manifestFile, out IClipCatalog catalog, out string err))
                {
                    _ui.ModePanel.SetActive(true);
                    _ui.ModeStatus.text = $"Failed to load {_manifestFile}: {err}";
                    return;
                }
                IOracleClient oracle = string.IsNullOrEmpty(_oracleRelayBaseUrl)
                    ? (IOracleClient)new MockOracleClient()
                    : new HttpOracleClient(_oracleRelayBaseUrl);
                match.ConfigureHost(catalog, oracle, new System.Random());
                StartCoroutine(StartAvatarSelectWhenSpawned(match));
            }

            gameObject.AddComponent<MatchTavernPresenter>()
                .Boot(_ui, _stage, _audio, match, joinCode, menuLobbyDone: true);
        }

        /// <summary>
        /// HOST: kick off avatar selection once the scene-placed Match has spawned. One extra
        /// frame so the ClientRpc isn't sent inside the spawn step (it can be dropped there);
        /// clients that are still scene-syncing don't rely on it anyway — their presenter opens
        /// the avatar select locally.
        /// </summary>
        private System.Collections.IEnumerator StartAvatarSelectWhenSpawned(MatchNetworkBehaviour match)
        {
            while (match != null && !match.IsSpawned) yield return null;
            yield return null;
            if (match != null) match.HostStartAvatarSelect();
        }

        private async System.Threading.Tasks.Task StartHost()
        {
            _busy = true;
            var match = FindFirstObjectByType<MatchNetworkBehaviour>();
            if (match == null) { _ui.ModeStatus.text = "No Match object in the scene — rebuild it."; _busy = false; return; }

            // Host-only rules injection — must happen BEFORE the network starts so the first
            // round can deal the moment the host spawns. Clients never run this.
            if (!ClipCatalogLoader.TryLoad(_manifestFile, out IClipCatalog catalog, out string err))
            {
                _ui.ModeStatus.text = $"Failed to load {_manifestFile}: {err}";
                _busy = false;
                return;
            }
            IOracleClient oracle = string.IsNullOrEmpty(_oracleRelayBaseUrl)
                ? (IOracleClient)new MockOracleClient()
                : new HttpOracleClient(_oracleRelayBaseUrl);
            match.ConfigureHost(catalog, oracle, new System.Random());

            _ui.ModeStatus.text = "Opening the tavern...";
            string joinCode = await _conn.HostAsync();
            if (string.IsNullOrEmpty(joinCode))
            {
                _ui.ModeStatus.text = "Hosting failed — check the transport setup.";
                _busy = false;
                return;
            }

            _ui.ModePanel.SetActive(false);
            gameObject.AddComponent<MatchTavernPresenter>().Boot(_ui, _stage, _audio, match, joinCode);
        }

        private async System.Threading.Tasks.Task StartJoin()
        {
            string joinInfo = _ui.JoinField.text;
            if (string.IsNullOrWhiteSpace(joinInfo))
            {
                _ui.ModeStatus.text = _useRelay ? "Enter the join code first." : "Enter the host's ip:port first.";
                return;
            }

            _busy = true;
            var match = FindFirstObjectByType<MatchNetworkBehaviour>();
            if (match == null) { _ui.ModeStatus.text = "No Match object in the scene — rebuild it."; _busy = false; return; }

            _ui.ModeStatus.text = "Knocking on the tavern door...";
            bool ok = await _conn.JoinAsync(joinInfo);
            if (!ok)
            {
                _ui.ModeStatus.text = "Couldn't reach that table — check the address/code.";
                _busy = false;
                return;
            }

            _ui.ModePanel.SetActive(false);
            gameObject.AddComponent<MatchTavernPresenter>().Boot(_ui, _stage, _audio, match, "");
        }
    }
}
