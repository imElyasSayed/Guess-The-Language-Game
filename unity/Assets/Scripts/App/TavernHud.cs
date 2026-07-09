using UnityEngine;
using UnityEngine.UI;
using static AccentGuesser.App.UiKit;

namespace AccentGuesser.App
{
    /// <summary>
    /// The tavern's screen-space HUD, built at runtime (no prefabs, no hand-authored canvas).
    ///
    /// Design: the 3D room is the star — the HUD is a slim console along the bottom (status
    /// line + ONE phase-adaptive control row), a small score chip in the top-left, and the
    /// Keep's answer floating as lantern-gold script over the room. All chrome is drawn with a
    /// procedurally generated rounded 9-slice sprite ("coasters" rather than flat slabs), in
    /// the brief's palette: lantern gold for primary actions and good news, worn teal for
    /// secondary actions, ember coral for bad news, aged paper for text, tavern shadow for
    /// panels.
    ///
    /// The presenter toggles which controls are visible per phase (Setup: Deal · Round:
    /// Play/Ask/Guess · Reveal: Deal/Replay). Guarantees an EventSystem so every control is
    /// keyboard-navigable (accessibility §15). Playback is button-driven — no autoplay.
    /// </summary>
    public static class TavernHud
    {
        // --- Palette: aliases into the shared kit so existing presenters keep compiling ----
        public static readonly Color Gold = UiKit.Gold;
        public static readonly Color Teal = UiKit.Teal;
        public static readonly Color Coral = UiKit.Coral;
        public static readonly Color Paper = UiKit.Paper;
        public static readonly Color Shadow = UiKit.Shadow;
        public static readonly Color Ink = UiKit.Ink;

        public sealed class Refs
        {
            public Text Status;
            public Text ScoreText;
            public Text AnswerText;
            public Button DealButton;
            public Button PlayButton;
            public Button AskButton;
            public InputField QuestionField;
            public InputField GuessField;
            public Button GuessButton;

            /// <summary>The round console; hidden until an avatar is chosen.</summary>
            public GameObject ConsoleRoot;

            /// <summary>The "choose your avatar" panel.</summary>
            public GameObject AvatarPanel;

            /// <summary>One button per pickable animal, in TavernStage.AvatarNames order.</summary>
            public Button[] AvatarButtons;

            /// <summary>Line under the avatar buttons ("waiting for the others...").</summary>
            public Text AvatarStatus;

            /// <summary>The waiting room: who's at the table + the host's Start button.</summary>
            public GameObject LobbyPanel;
            public Text LobbyInfo;
            public Button StartGameButton;

            /// <summary>The very first panel: play solo, host a table, or join a friend's.</summary>
            public GameObject ModePanel;
            public Button SoloButton;
            public Button HostButton;
            public Button JoinButton;
            public InputField JoinField;
            public Text ModeStatus;

            /// <summary>Top-right multiplayer roster chip (names · scores · asker · locked).</summary>
            public GameObject RosterChip;
            public Text RosterText;
        }

        public static Refs Build(Transform parent)
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("TavernHud",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var refs = new Refs();

            // --- Score chip, top-left: SCORE · STREAK · ROUND -----------------
            var chip = NewCard("ScoreChip", canvasGo.transform);
            chip.anchorMin = chip.anchorMax = new Vector2(0f, 1f);
            chip.pivot = new Vector2(0f, 1f);
            chip.anchoredPosition = new Vector2(14f, -12f);
            var chipLayout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipLayout.padding = new RectOffset(14, 14, 6, 6);
            var chipFit = chip.gameObject.AddComponent<ContentSizeFitter>();
            chipFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            chipFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            refs.ScoreText = MakeText(chip, "Score", "SCORE 0   STREAK 0   ROUND 0",
                15, FontStyle.Bold, Gold, TextAnchor.MiddleLeft);

            // --- The Keep's answer: floating gold script over the room --------
            refs.AnswerText = MakeFloatingText(canvasGo.transform, "Answer",
                new Vector2(0.08f, 0.215f), new Vector2(0.92f, 0.28f),
                18, FontStyle.Italic, Gold, TextAnchor.MiddleCenter);

            // --- Slim console: status line + one adaptive control row ---------
            var console = NewCard("Console", canvasGo.transform);
            console.anchorMin = new Vector2(0.03f, 0.02f);
            console.anchorMax = new Vector2(0.97f, 0.205f);
            console.offsetMin = console.offsetMax = Vector2.zero;
            var v = console.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.padding = new RectOffset(18, 18, 10, 12);
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;

            refs.Status = MakeText(console, "Status", "Loading...",
                17, FontStyle.Normal, Paper, TextAnchor.MiddleCenter);
            refs.Status.gameObject.AddComponent<LayoutElement>().minHeight = 24;

            var row = NewRow(console, "Controls", 46);
            refs.DealButton = MakeButton(row, "DealButton", "Deal me in", Gold, Ink, 220);
            refs.PlayButton = MakeButton(row, "PlayButton", "Play clip", Teal, Paper, 150);
            refs.QuestionField = MakeInputField(row, "QuestionField", "Ask the Keep one question...");
            refs.AskButton = MakeButton(row, "AskButton", "Ask the Keep", Teal, Paper, 150);
            refs.GuessField = MakeInputField(row, "GuessField", "Name the language...");
            refs.GuessButton = MakeButton(row, "GuessButton", "Lock it in", Gold, Ink, 150);

            refs.ConsoleRoot = console.gameObject;
            BuildAvatarSelect(canvasGo.transform, refs);
            BuildModePanel(canvasGo.transform, refs);
            BuildLobbyPanel(canvasGo.transform, refs);
            BuildRosterChip(canvasGo.transform, refs);

            return refs;
        }

        /// <summary>
        /// The waiting room shown after hosting/joining: who's at the table, the address/code to
        /// share, and — on the host's machine only — the Start button.
        /// </summary>
        private static void BuildLobbyPanel(Transform canvas, Refs refs)
        {
            var panel = NewCard("LobbyPanel", canvas);
            panel.anchorMin = new Vector2(0.22f, 0.03f);
            panel.anchorMax = new Vector2(0.78f, 0.34f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(22, 22, 12, 14);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            MakeText(panel, "Title", "The table is filling up...", 24, FontStyle.Bold, Gold, TextAnchor.MiddleCenter);
            refs.LobbyInfo = MakeText(panel, "LobbyInfo", "", 16, FontStyle.Normal, Paper, TextAnchor.UpperCenter);
            refs.LobbyInfo.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;

            var row = NewRow(panel, "LobbyRow", 48);
            refs.StartGameButton = MakeButton(row, "StartGameButton", "Start the game", Gold, Ink, 240);

            refs.LobbyPanel = panel.gameObject;
            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// The very first screen: play alone, host a table for friends, or join one. Shown over
        /// the wide selection view; the bootstrap hides it once a mode is running.
        /// </summary>
        private static void BuildModePanel(Transform canvas, Refs refs)
        {
            var panel = NewCard("ModePanel", canvas);
            panel.anchorMin = new Vector2(0.16f, 0.03f);
            panel.anchorMax = new Vector2(0.84f, 0.27f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(20, 20, 12, 12);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            MakeText(panel, "Title", "Say Again?", 26, FontStyle.Bold, Gold, TextAnchor.MiddleCenter);
            refs.ModeStatus = MakeText(panel, "ModeStatus", "Pull up a stool.",
                15, FontStyle.Normal, Paper, TextAnchor.MiddleCenter);
            refs.ModeStatus.gameObject.AddComponent<LayoutElement>().minHeight = 20;

            var row = NewRow(panel, "ModeRow", 48);
            refs.SoloButton = MakeButton(row, "SoloButton", "Play solo", Gold, Ink, 170);
            refs.HostButton = MakeButton(row, "HostButton", "Host a table", Teal, Paper, 170);
            refs.JoinField = MakeInputField(row, "JoinField", "Host address or code...");
            refs.JoinButton = MakeButton(row, "JoinButton", "Join", Teal, Paper, 110);

            refs.ModePanel = panel.gameObject;
        }

        /// <summary>Top-right chip listing the table: names, scores, who's the asker, who locked.</summary>
        private static void BuildRosterChip(Transform canvas, Refs refs)
        {
            var chip = NewCard("RosterChip", canvas);
            chip.anchorMin = chip.anchorMax = new Vector2(1f, 1f);
            chip.pivot = new Vector2(1f, 1f);
            chip.anchoredPosition = new Vector2(-14f, -12f);
            var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 8, 8);
            var fit = chip.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            refs.RosterText = MakeText(chip, "Roster", "", 14, FontStyle.Normal, Paper, TextAnchor.UpperLeft);
            refs.RosterChip = chip.gameObject;
            chip.gameObject.SetActive(false); // multiplayer only
        }

        /// <summary>
        /// The startup "who are you tonight?" panel: one coaster per seated animal, over the
        /// wide table view. The presenter hides this on pick and swoops into that seat.
        /// </summary>
        private static void BuildAvatarSelect(Transform canvas, Refs refs)
        {
            var panel = NewCard("AvatarSelect", canvas);
            panel.anchorMin = new Vector2(0.10f, 0.03f);
            panel.anchorMax = new Vector2(0.90f, 0.235f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(20, 20, 12, 14);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            MakeText(panel, "Title", "Who are you tonight?",
                24, FontStyle.Bold, Gold, TextAnchor.MiddleCenter);

            var row = NewRow(panel, "AvatarRow", 52);
            // Button order matches the showroom lineup left→right (baked by the scene builder).
            string[] names = { "Bulldog", "Giraffe", "Horse", "Fox", "Cat" };
            refs.AvatarButtons = new Button[names.Length];
            for (int i = 0; i < names.Length; i++)
                refs.AvatarButtons[i] = MakeButton(row, names[i] + "Button", names[i], Teal, Paper, 0);

            refs.AvatarStatus = MakeText(panel, "AvatarStatus", "", 15, FontStyle.Italic, Paper, TextAnchor.MiddleCenter);
            refs.AvatarStatus.gameObject.AddComponent<LayoutElement>().minHeight = 20;

            refs.AvatarPanel = panel.gameObject;
        }

        // ---- primitives (shared with the menu via UiKit) -----------------------

        private static Text MakeFloatingText(Transform canvas, string name,
            Vector2 anchorMin, Vector2 anchorMax, int size, FontStyle style, Color color,
            TextAnchor align)
        {
            var t = MakeText(canvas, name, "", size, style, color, align);
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }
    }
}
