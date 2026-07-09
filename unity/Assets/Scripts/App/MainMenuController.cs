using AccentGuesser.Core;
using AccentGuesser.Net;
using AccentGuesser.Services;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AccentGuesser.App
{
    /// <summary>
    /// Drives the main-menu scene: the three-screen card built by <see cref="MenuHud"/> over the
    /// live tavern vignette. Networking for a match now starts HERE —
    ///
    ///  • Play Solo    → flags <see cref="MenuSelection"/> and loads Tavern.unity locally.
    ///  • Host a Table → injects catalog/oracle into the menu's <see cref="MatchNetworkBehaviour"/>,
    ///                   starts the host, shows the shareable code + live player list, and on Start
    ///                   walks every connected client into the tavern together via
    ///                   <c>NetworkManager.SceneManager.LoadScene</c>.
    ///  • Join a Table → starts a client toward the entered code/address, renders the same waiting
    ///                   room, and follows the host's scene switch automatically.
    ///
    /// Back buttons shut the network down cleanly and reload the menu for a fresh lobby; a client
    /// that gets turned away (full table, host left) lands back here with a one-shot notice.
    /// Host authority is untouched: clients never hold a catalog, an oracle, or the answer.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string TavernScene = "Tavern";
        private const float JoinTimeoutSeconds = 10f;

        [Header("Config")]
        [Tooltip("clips.json path relative to StreamingAssets (host/solo only).")]
        [SerializeField] private string _manifestFile = "clips.json";

        [Tooltip("If set, the host uses the real oracle relay; otherwise the offline MockOracleClient.")]
        [SerializeField] private string _oracleRelayBaseUrl = "";

        [Tooltip("Use Unity Relay (share a short join code that works over the internet). " +
                 "Off = direct IP for LAN / same-machine testing.")]
        [SerializeField] private bool _useRelay = true;

        private enum Screen { Menu, Join, Lobby, Licenses }

        private MenuHud.Refs _ui;
        private MatchNetworkBehaviour _match;
        private IConnectionManager _conn;
        private Screen _screen = Screen.Menu;
        private string _joinCode = "";
        private bool _busy;
        private bool _connecting;
        private bool _leaving;   // Start pressed / scene switch underway — ignore stray callbacks

        private void Start()
        {
            _ui = MenuHud.Build(transform);
            _match = FindFirstObjectByType<MatchNetworkBehaviour>();
            _conn = _useRelay ? (IConnectionManager)new RelayConnectionManager() : new DirectConnectionManager();

            _ui.SoloButton.onClick.AddListener(OnSolo);
            _ui.HostButton.onClick.AddListener(() => { if (!_busy) _ = OnHost(); });
            _ui.JoinNavButton.onClick.AddListener(() => Show(Screen.Join));
            _ui.SettingsButton.onClick.AddListener(() =>
                Notice("Settings are still brewing — nothing to tune yet."));
            _ui.LicensesButton.onClick.AddListener(() => Show(Screen.Licenses));
            _ui.LicensesBackButton.onClick.AddListener(() => Show(Screen.Menu));

            _ui.JoinBackButton.onClick.AddListener(OnJoinBack);
            _ui.JoinSubmitButton.onClick.AddListener(() => { if (!_busy) _ = OnJoin(); });
            _ui.CodeField.onEndEdit.AddListener(code =>
            {
                if (!_busy && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                    _ = OnJoin();
            });

            _ui.LobbyBackButton.onClick.AddListener(OnLobbyBack);
            _ui.CopyButton.onClick.AddListener(OnCopy);
            _ui.StartButton.onClick.AddListener(OnStartGame);
            _ui.StartButton.interactable = false;

            if (_match != null) _match.OnRoundView += HandleRoundView;
            var nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientDisconnectCallback += HandleLocalDisconnect;

            Notice(MenuSelection.ConsumeNotice());
            Show(Screen.Menu);
        }

        private void OnDestroy()
        {
            if (_match != null) _match.OnRoundView -= HandleRoundView;
            var nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientDisconnectCallback -= HandleLocalDisconnect;
        }

        private void Update()
        {
            // Esc backs out of any sub-screen (lobby Esc = the same clean shutdown as Back).
            if (!Input.GetKeyDown(KeyCode.Escape) || _leaving) return;
            switch (_screen)
            {
                case Screen.Join: OnJoinBack(); break;
                case Screen.Lobby: OnLobbyBack(); break;
                case Screen.Licenses: Show(Screen.Menu); break;
            }
        }

        // ---- Screen swapping ----------------------------------------------------

        private void Show(Screen screen)
        {
            _screen = screen;
            _ui.MenuPanel.SetActive(screen == Screen.Menu);
            _ui.JoinPanel.SetActive(screen == Screen.Join);
            _ui.LobbyPanel.SetActive(screen == Screen.Lobby);
            _ui.LicensesPanel.SetActive(screen == Screen.Licenses);

            // keyboard-first: land focus on the screen's natural control
            var focus = screen switch
            {
                Screen.Join => _ui.CodeField.gameObject,
                Screen.Lobby => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost
                    ? _ui.StartButton.gameObject : _ui.LobbyBackButton.gameObject,
                Screen.Licenses => _ui.LicensesBackButton.gameObject,
                _ => _ui.SoloButton.gameObject
            };
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(focus);
        }

        private void Notice(string message) => _ui.MenuNotice.text = message ?? "";

        // ---- Solo -----------------------------------------------------------------

        private void OnSolo()
        {
            if (_busy) return;
            _busy = true;
            MenuSelection.SoloPending = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(TavernScene);
        }

        // ---- Host a table -----------------------------------------------------------

        private async System.Threading.Tasks.Task OnHost()
        {
            _busy = true;
            if (_match == null)
            {
                Notice("No Match object in the scene — rebuild the menu scene.");
                _busy = false;
                return;
            }

            // Host-only rules injection, BEFORE the network starts (clients never run this).
            if (!ClipCatalogLoader.TryLoad(_manifestFile, out IClipCatalog catalog, out string err))
            {
                Notice($"Failed to load {_manifestFile}: {err}");
                _busy = false;
                return;
            }
            IOracleClient oracle = string.IsNullOrEmpty(_oracleRelayBaseUrl)
                ? (IOracleClient)new MockOracleClient()
                : new HttpOracleClient(_oracleRelayBaseUrl);
            _match.ConfigureHost(catalog, oracle, new System.Random());

            SetLobbyChrome(host: true);
            _ui.CodeValue.text = "opening the tavern…";
            Show(Screen.Lobby);

            string joinCode = await _conn.HostAsync();
            if (string.IsNullOrEmpty(joinCode))
            {
                Notice("Hosting failed — check the transport setup.");
                Show(Screen.Menu);
                _busy = false;
                return;
            }

            _joinCode = joinCode;
            _ui.CodeValue.text = joinCode;
            _ui.StartButton.interactable = true;
            _busy = false;
        }

        private void OnStartGame()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsHost || _leaving) return;
            _leaving = true;
            _ui.StartButton.interactable = false;

            // Close the menu lobby to newcomers, then walk every connected client into the
            // tavern together. The avatar select itself is (re)triggered on arrival by
            // TavernBootstrap, so a client mid-scene-load can't miss it.
            MenuSelection.JoinCode = _joinCode;
            _match.HostStartAvatarSelect();
            nm.SceneManager.LoadScene(TavernScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        private void OnCopy()
        {
            if (string.IsNullOrEmpty(_joinCode)) return;
            GUIUtility.systemCopyBuffer = _joinCode;
            StartCoroutine(FlashCopied());
        }

        private System.Collections.IEnumerator FlashCopied()
        {
            var label = _ui.CopyButton.GetComponentInChildren<Text>();
            if (label == null) yield break;
            label.text = "COPIED!";
            float until = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < until) yield return null;
            label.text = "COPY";
        }

        // ---- Join a table -------------------------------------------------------------

        private async System.Threading.Tasks.Task OnJoin()
        {
            string code = _ui.CodeField.text;
            if (string.IsNullOrWhiteSpace(code))
            {
                JoinStatus("Enter the table code first.", failed: true);
                return;
            }

            _busy = true;
            _connecting = true;
            _ui.JoinSubmitButton.interactable = false;
            JoinStatus("Knocking on the tavern door…", failed: false);

            bool ok = await _conn.JoinAsync(code.Trim());
            if (!ok)
            {
                JoinFailed("Couldn't reach that table — check the address/code.");
                return;
            }
            StartCoroutine(WatchConnect(code.Trim()));
        }

        /// <summary>Poll the pending connection: seated → waiting room; timeout → clean failure.</summary>
        private System.Collections.IEnumerator WatchConnect(string code)
        {
            float deadline = Time.realtimeSinceStartup + JoinTimeoutSeconds;
            var nm = NetworkManager.Singleton;
            while (_connecting)
            {
                if (nm.IsConnectedClient)
                {
                    _connecting = false;
                    _busy = false;
                    _joinCode = code;
                    SetLobbyChrome(host: false);
                    _ui.CodeValue.text = code;
                    Show(Screen.Lobby);
                    yield break;
                }
                if (!nm.IsListening) // kicked during handshake (full/closed) or transport failed
                {
                    JoinFailed("The table turned you away — full, already playing, or a wrong code.");
                    yield break;
                }
                if (Time.realtimeSinceStartup > deadline)
                {
                    nm.Shutdown();
                    JoinFailed("No answer from that table — is the host still there?");
                    yield break;
                }
                yield return null;
            }
        }

        private void JoinFailed(string message)
        {
            _connecting = false;
            _busy = false;
            _ui.JoinSubmitButton.interactable = true;
            JoinStatus(message, failed: true);
        }

        private void JoinStatus(string message, bool failed)
        {
            _ui.JoinStatus.text = message;
            _ui.JoinStatus.color = failed ? UiKit.Coral : UiKit.Paper;
        }

        /// <summary>The local client dropped (host left, table full, network died).</summary>
        private void HandleLocalDisconnect(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsHost || _leaving) return;
            if (clientId != nm.LocalClientId && clientId != 0) return;

            if (_screen == Screen.Lobby)
            {
                // seated and then the tavern closed — start the menu over with an explanation
                MenuSelection.Notice = "The tavern closed — the host ended the table.";
                ReloadMenu();
            }
            else if (_connecting)
            {
                JoinFailed("The table turned you away — full, already playing, or a wrong code.");
            }
        }

        // ---- Waiting-room rendering ------------------------------------------------------

        /// <summary>Host sees Start; guests see the "waiting for the host" line instead.</summary>
        private void SetLobbyChrome(bool host)
        {
            _ui.StartButton.gameObject.SetActive(host);
            _ui.LobbyStatus.text = host ? "" : "Waiting for the host to start…";
            for (int i = 0; i < _ui.Slots.Length; i++) RenderSlotEmpty(_ui.Slots[i]);
            _ui.PlayersLabel.text = $"PLAYERS · 0/{MatchNetworkBehaviour.MaxPlayers}";
        }

        /// <summary>Setup-phase roster broadcasts from the menu Match = the live player list.</summary>
        private void HandleRoundView(RoundView view)
        {
            if (view.Phase != NetPhase.Setup || _ui == null) return;
            var nm = NetworkManager.Singleton;
            string myId = nm.LocalClientId.ToString();
            string hostId = NetworkManager.ServerClientId.ToString();

            _ui.PlayersLabel.text = $"PLAYERS · {view.Roster.Length}/{MatchNetworkBehaviour.MaxPlayers}";
            for (int i = 0; i < _ui.Slots.Length; i++)
            {
                if (i < view.Roster.Length)
                    RenderSlotFilled(_ui.Slots[i], i, view.Roster[i].Name,
                        isHost: view.Roster[i].Id == hostId, isYou: view.Roster[i].Id == myId);
                else
                    RenderSlotEmpty(_ui.Slots[i]);
            }
        }

        private static void RenderSlotFilled(MenuHud.PlayerSlot slot, int seat, string name,
            bool isHost, bool isYou)
        {
            slot.Fill.color = new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.05f);
            slot.Outline.enabled = false;
            var bg = MenuHud.SlotColors[seat % MenuHud.SlotColors.Length];
            slot.Avatar.enabled = true;
            slot.Avatar.color = bg;
            slot.AvatarLetter.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
            slot.AvatarLetter.color = bg == UiKit.Teal ? UiKit.Paper : UiKit.Ink;
            slot.Name.text = name;
            slot.Name.fontStyle = FontStyle.Bold;
            slot.Name.color = UiKit.Paper;
            slot.HostTag.SetActive(isHost);
            slot.YouTag.SetActive(isYou);
        }

        private static void RenderSlotEmpty(MenuHud.PlayerSlot slot)
        {
            slot.Fill.color = Color.clear;
            slot.Outline.enabled = true;
            slot.Avatar.enabled = false;
            slot.AvatarLetter.text = "";
            slot.Name.text = "Waiting for players…";
            slot.Name.fontStyle = FontStyle.Italic;
            slot.Name.color = new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.4f);
            slot.HostTag.SetActive(false);
            slot.YouTag.SetActive(false);
        }

        // ---- Back buttons (clean shutdowns) --------------------------------------------

        private void OnJoinBack()
        {
            if (_connecting)
            {
                _connecting = false; // stops WatchConnect
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsListening) nm.Shutdown();
                _busy = false;
                _ui.JoinSubmitButton.interactable = true;
            }
            JoinStatus("", failed: false);
            Show(Screen.Menu);
        }

        private void OnLobbyBack()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                _leaving = true;
                nm.Shutdown(); // host: closes the table for everyone; client: leaves it
                StartCoroutine(ReloadWhenShutDown(nm));
            }
            else
            {
                Show(Screen.Menu);
            }
        }

        private System.Collections.IEnumerator ReloadWhenShutDown(NetworkManager nm)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (nm != null && nm.ShutdownInProgress && Time.realtimeSinceStartup < deadline)
                yield return null;
            ReloadMenu();
        }

        /// <summary>Fresh lobby state (scene-placed Match, clean flags) after any shutdown.</summary>
        private static void ReloadMenu() =>
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
