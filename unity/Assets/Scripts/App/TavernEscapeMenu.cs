using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AccentGuesser.App
{
    /// <summary>
    /// The tavern's Escape menu: press Esc anywhere in-game for a small card offering
    /// Back to the Table (close the popup), Leave the Table (shut the network down cleanly and
    /// return to the main menu — as host that closes the table for everyone), and Quit to
    /// Desktop. Esc also closes an open popup.
    ///
    /// Doubles as the client's safety net: if the connection to the host dies while playing
    /// (host left, network dropped), it returns to the main menu with a one-shot notice instead
    /// of leaving the table frozen. Added by <see cref="TavernBootstrap"/>, so every way into the
    /// tavern (menu solo, menu lobby, direct play) gets it. Nothing here pauses the simulation —
    /// a networked table can't stop for one player.
    /// </summary>
    public sealed class TavernEscapeMenu : MonoBehaviour
    {
        private const string MenuScene = "MainMenu";

        private GameObject _canvas;
        private GameObject _panel;
        private Text _subtitle;
        private Button _resume;
        private bool _leaving;
        private bool _wasConnectedClient;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
            WatchForLostHost();
        }

        /// <summary>Open/close the popup (public so tests can drive it — Esc does this).</summary>
        public void Toggle()
        {
            if (_leaving) return;
            if (_canvas == null) Build();
            bool show = !_panel.activeSelf;
            _panel.SetActive(show);
            if (show)
            {
                RefreshSubtitle();
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(_resume.gameObject);
            }
        }

        /// <summary>A connected client whose server vanished goes home with an explanation.</summary>
        private void WatchForLostHost()
        {
            var nm = NetworkManager.Singleton;
            bool connectedClient = nm != null && nm.IsListening && !nm.IsHost;
            if (_wasConnectedClient && !connectedClient && !_leaving)
            {
                _leaving = true;
                MenuSelection.Notice = "The tavern closed — connection to the host was lost.";
                UnityEngine.SceneManagement.SceneManager.LoadScene(MenuScene);
            }
            _wasConnectedClient = connectedClient;
        }

        // ---- Actions --------------------------------------------------------------

        private void OnLeave()
        {
            if (_leaving) return;
            _leaving = true;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                nm.Shutdown(); // host: closes the table for everyone; client: gives up the seat
                StartCoroutine(LoadMenuWhenShutDown(nm));
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(MenuScene);
            }
        }

        private System.Collections.IEnumerator LoadMenuWhenShutDown(NetworkManager nm)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (nm != null && nm.ShutdownInProgress && Time.realtimeSinceStartup < deadline)
                yield return null;
            UnityEngine.SceneManagement.SceneManager.LoadScene(MenuScene);
        }

        private void OnQuit()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) nm.Shutdown(); // best effort before the lights go out
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- UI (built lazily on first Esc) ----------------------------------------

        private void Build()
        {
            UiKit.EnsureEventSystem();
            _canvas = new GameObject("EscapeMenu",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // above the game HUD
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // dimmer that also swallows clicks meant for the HUD underneath
            _panel = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(_canvas.transform, false);
            var dim = _panel.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            var drt = _panel.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;

            var card = UiKit.NewCard("Card", _panel.transform, 28);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(560f, 460f);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 44, 40);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            UiKit.MakeText(card, "Title", "Stepping out?",
                34, FontStyle.Bold, UiKit.Paper, TextAnchor.MiddleCenter);
            _subtitle = UiKit.MakeText(card, "Subtitle", "",
                17, FontStyle.Italic, new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.6f),
                TextAnchor.MiddleCenter);

            var flex = new GameObject("Flex", typeof(RectTransform));
            flex.transform.SetParent(card, false);
            flex.AddComponent<LayoutElement>().flexibleHeight = 1f;

            _resume = PopupButton(card, "ResumeButton", "Back to the Table", UiKit.Gold,
                UiKit.GoldHover, UiKit.GoldPressed, UiKit.Ink);
            _resume.onClick.AddListener(Toggle);
            PopupButton(card, "LeaveButton", "Leave the Table", UiKit.Teal,
                UiKit.TealHover, UiKit.TealPressed, UiKit.Paper).onClick.AddListener(OnLeave);
            PopupButton(card, "QuitButton", "Quit to Desktop", UiKit.Coral,
                new Color(0.93f, 0.47f, 0.37f, 1f), new Color(0.75f, 0.31f, 0.22f, 1f),
                UiKit.Ink).onClick.AddListener(OnQuit);

            _panel.SetActive(false);
        }

        private void RefreshSubtitle()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && nm.IsHost && nm.ConnectedClientsIds.Count > 1)
                _subtitle.text = "You're the host — leaving closes the table for everyone.";
            else if (nm != null && nm.IsListening && !nm.IsHost)
                _subtitle.text = "Your seat goes back up for grabs.";
            else
                _subtitle.text = "The Keep will pretend not to miss you.";
        }

        private static Button PopupButton(Transform parent, string name, string label,
            Color normal, Color hover, Color pressed, Color fg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = UiKit.Rounded(13);
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 64f;

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = hover;
            colors.selectedColor = hover;
            colors.pressedColor = pressed;
            colors.disabledColor = UiKit.SlateDisabled;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var t = UiKit.MakeText(go.transform, "Text", label.ToUpperInvariant(),
                19, FontStyle.Bold, fg, TextAnchor.MiddleCenter);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            return button;
        }
    }
}
