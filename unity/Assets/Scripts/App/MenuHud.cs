using UnityEngine;
using UnityEngine.UI;

namespace AccentGuesser.App
{
    /// <summary>
    /// The main-menu canvas, built at runtime from the approved 2D design: one left-anchored card
    /// (624px wide, radius 28, tavern-shadow at 88% with a lantern-gold rim) over the design's
    /// warm 2D backdrop — wood-dark page, faint gold hatching, breathing lantern glows, drifting
    /// dust motes (no 3D: the tavern is only ever seen in-game). The card interior swaps between
    /// three screens — MAIN MENU (title, tagline, Play Solo / Host a Table / Join a Table,
    /// footer), FIND A TABLE (code entry + join states), and YOUR TABLE (shareable code box, four
    /// player slots, Start) — plus a Data &amp; Licenses overlay for the FLEURS/FLoRes attribution
    /// the brief requires.
    ///
    /// All px values are the design's 1920×1080 spec, applied 1:1 via the CanvasScaler reference
    /// resolution. Chrome comes from <see cref="UiKit"/> (same coaster sprites as the in-game
    /// HUD); every control is a keyboard-navigable uGUI Selectable and nothing autoplays.
    /// </summary>
    public static class MenuHud
    {
        /// <summary>One waiting-room row: filled (player) or empty ("waiting…") per state.</summary>
        public sealed class PlayerSlot
        {
            public GameObject Root;
            public Image Fill;          // paper @5% when filled, transparent when empty
            public Image Outline;       // faint rim shown only when empty
            public Image Avatar;        // 44px circle
            public Text AvatarLetter;
            public Text Name;
            public GameObject HostTag;
            public GameObject YouTag;
        }

        public sealed class Refs
        {
            public GameObject MenuPanel, JoinPanel, LobbyPanel, LicensesPanel;

            // main menu
            public Button SoloButton, HostButton, JoinNavButton, SettingsButton, LicensesButton;
            public Text MenuNotice;

            // find a table
            public Button JoinBackButton, JoinSubmitButton;
            public InputField CodeField;
            public Text JoinStatus;

            // your table
            public Button LobbyBackButton, CopyButton, StartButton;
            public Text CodeValue, PlayersLabel, LobbyStatus;
            public PlayerSlot[] Slots;

            // licenses
            public Button LicensesBackButton;
        }

        // Per-seat avatar-circle fills (design: gold You, teal, paper, coral for a fourth).
        public static readonly Color[] SlotColors = { UiKit.Gold, UiKit.Teal, UiKit.Paper, UiKit.Coral };

        private const float CardWidth = 624f;
        private const int SeatCount = 4;

        public static Refs Build(Transform parent)
        {
            UiKit.EnsureEventSystem();

            var canvasGo = new GameObject("MenuHud",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // --- Backdrop (first child, everything draws over it): the design's warm page ---
            BuildBackdrop(canvasGo.transform);

            // --- The one shared card: left-anchored, 70px top/bottom, 90px left ---
            var card = UiKit.NewCard("Card", canvasGo.transform, 28);
            card.anchorMin = new Vector2(0f, 0f);
            card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 0.5f);
            card.sizeDelta = new Vector2(CardWidth, -140f);
            card.anchoredPosition = new Vector2(90f, 0f);
            Rim(card, 28, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.22f), false);

            var refs = new Refs();
            BuildMenuScreen(card, refs);
            BuildJoinScreen(card, refs);
            BuildLobbyScreen(card, refs);
            BuildLicensesScreen(card, refs);

            refs.JoinPanel.SetActive(false);
            refs.LobbyPanel.SetActive(false);
            refs.LicensesPanel.SetActive(false);
            return refs;
        }

        // --- Backdrop: the design's warm page, alive ------------------------------

        /// <summary>
        /// Wood-dark base, the sketch's faint 135° gold pinstripes, two breathing lantern glows,
        /// a soft bottom vignette, and a dozen dust motes drifting up through the light. Entirely
        /// 2D and shared by every screen — the tavern itself only appears once you're in-game.
        /// </summary>
        private static void BuildBackdrop(Transform canvas)
        {
            var root = new GameObject("Backdrop", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            Stretch(root.GetComponent<RectTransform>());

            FullImage(root.transform, "Base", null, new Color(0.165f, 0.118f, 0.075f, 1f)); // #2a1e13
            var hatch = FullImage(root.transform, "Hatch", UiKit.Hatch(), Color.white);
            hatch.type = Image.Type.Tiled;

            // lantern pools of light: one high right (the sketch's lantern), one low over the room
            var glowHigh = Glow(root.transform, "GlowHigh", new Vector2(0.76f, 0.78f), 620f,
                new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.34f));
            var glowLow = Glow(root.transform, "GlowLow", new Vector2(0.58f, 0.22f), 820f,
                new Color(0.878f, 0.502f, 0.255f, 0.17f)); // ember-warm

            // dust motes, seeded deterministically so rebuilds look identical
            var motes = new RectTransform[12];
            for (int i = 0; i < motes.Length; i++)
            {
                float x = 300f + (i * 137f) % 1620f;             // spread across the width
                float y = (i * 293f) % 1080f;
                float size = 5f + (i * 7f) % 9f;
                var mote = new GameObject($"Mote{i}", typeof(RectTransform), typeof(Image));
                mote.transform.SetParent(root.transform, false);
                var img = mote.GetComponent<Image>();
                img.sprite = UiKit.SoftCircle();
                img.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.10f + (i % 4) * 0.03f);
                img.raycastTarget = false;
                var rt = mote.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.anchoredPosition = new Vector2(x, y);
                rt.sizeDelta = new Vector2(size, size);
                motes[i] = rt;
            }

            // settle the floor of the frame so the card and footer read against something calm
            var vignette = FullImage(root.transform, "BottomVignette", UiKit.GradientUp(),
                new Color(0f, 0f, 0f, 0.5f));
            var vrt = vignette.rectTransform;
            vrt.anchorMin = new Vector2(0f, 0f);
            vrt.anchorMax = new Vector2(1f, 0.42f);
            vrt.offsetMin = vrt.offsetMax = Vector2.zero;

            var life = root.AddComponent<MenuBackdrop>();
            life.SetGlows(new[] { glowHigh, glowLow });
            life.SetMotes(motes);
        }

        private static Image FullImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            Stretch(go.GetComponent<RectTransform>());
            return img;
        }

        private static Image Glow(Transform parent, string name, Vector2 anchor, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = UiKit.SoftCircle();
            img.color = color;
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(size, size);
            return img;
        }

        // --- Screen A: main menu -------------------------------------------------

        private static void BuildMenuScreen(RectTransform card, Refs refs)
        {
            var panel = Screen(card, "MenuScreen");

            // pill badge
            var pillHolder = Holder(panel, "PillHolder", 34f);
            var pill = new GameObject("Pill", typeof(RectTransform), typeof(Image));
            pill.transform.SetParent(pillHolder, false);
            var pillImg = pill.GetComponent<Image>();
            pillImg.sprite = UiKit.Rounded(16);
            pillImg.type = Image.Type.Sliced;
            pillImg.color = new Color(UiKit.Teal.r, UiKit.Teal.g, UiKit.Teal.b, 0.32f);
            var pillRt = pill.GetComponent<RectTransform>();
            pillRt.anchorMin = pillRt.anchorMax = new Vector2(0f, 0.5f);
            pillRt.pivot = new Vector2(0f, 0.5f);
            var pillLayout = pill.AddComponent<HorizontalLayoutGroup>();
            pillLayout.padding = new RectOffset(16, 16, 9, 9);
            var pillFit = pill.AddComponent<ContentSizeFitter>();
            pillFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            pillFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiKit.MakeText(pill.transform, "Label", "COZY TAVERN PARTY GAME",
                13, FontStyle.Bold, UiKit.Gold, TextAnchor.MiddleCenter);

            Gap(panel, 22f);
            var title = UiKit.MakeText(panel, "Title",
                "Say\nAgain<color=#d8a53a>?</color>", 104, FontStyle.Bold, UiKit.Paper, TextAnchor.UpperLeft);
            title.lineSpacing = 0.92f;
            title.verticalOverflow = VerticalWrapMode.Overflow;

            Gap(panel, 22f);
            UiKit.MakeText(panel, "Tagline", "Listen close. Trust your ear.",
                22, FontStyle.Normal, Faded(UiKit.Paper, 0.66f), TextAnchor.UpperLeft);

            Gap(panel, 26f);
            BuildWave(panel);

            Flex(panel);
            refs.MenuNotice = UiKit.MakeText(panel, "Notice", "",
                16, FontStyle.Italic, UiKit.Coral, TextAnchor.MiddleLeft);
            Gap(panel, 12f);

            // the three coasters
            var actions = SubColumn(panel, "Actions", 16f);
            refs.SoloButton = Coaster(actions, "SoloButton", "Play Solo", true);
            refs.HostButton = Coaster(actions, "HostButton", "Host a Table", false);
            refs.JoinNavButton = Coaster(actions, "JoinNavButton", "Join a Table", false);

            // footer
            Gap(panel, 26f);
            Divider(panel);
            Gap(panel, 24f);
            var footer = Row(panel, "Footer", 42f, 20f);
            refs.SettingsButton = IconButton(footer, "SettingsButton", "✱", 42f, 11); // "⚙" won't rasterize in LegacyRuntime
            refs.LicensesButton = LinkButton(footer, "LicensesButton", "Data & Licenses");
            FlexRow(footer);
            UiKit.MakeText(footer, "Version", "v0.1 — feature/3d-world",
                13, FontStyle.Normal, Faded(UiKit.Paper, 0.38f), TextAnchor.MiddleRight);

            refs.MenuPanel = panel.gameObject;
        }

        /// <summary>The little waveform flourish under the tagline (heights from the design).</summary>
        private static void BuildWave(RectTransform panel)
        {
            int[] heights = { 9, 17, 29, 21, 39, 13, 25, 34, 19, 11, 33, 23, 15, 37,
                              27, 10, 22, 31, 18, 30, 14, 26, 20, 34, 12, 24, 16, 28 };
            var wave = Holder(panel, "Wave", 40f);
            for (int i = 0; i < heights.Length; i++)
            {
                var bar = new GameObject($"Bar{i}", typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(wave, false);
                var img = bar.GetComponent<Image>();
                img.sprite = UiKit.Rounded(3);
                img.type = Image.Type.Sliced;
                img.color = i % 5 == 2 ? UiKit.Gold : Faded(UiKit.Paper, 0.2f);
                img.raycastTarget = false;
                var rt = bar.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(i * 9f, 0f);
                rt.sizeDelta = new Vector2(5f, heights[i]);
            }
        }

        // --- Screen C: find a table ------------------------------------------------

        private static void BuildJoinScreen(RectTransform card, Refs refs)
        {
            var panel = Screen(card, "JoinScreen");
            refs.JoinBackButton = Header(panel, "Find a Table");

            Gap(panel, 16f);
            UiKit.MakeText(panel, "Sub", "Pull up a chair — max 4 to a table.",
                18, FontStyle.Normal, Faded(UiKit.Paper, 0.6f), TextAnchor.UpperLeft);

            Gap(panel, 22f);
            var row = Row(panel, "CodeRow", 74f, 12f);
            refs.CodeField = BigField(row, "CodeField", "table code or host address…");
            refs.JoinSubmitButton = Coaster(row, "JoinSubmitButton", "Join", false, 23, 74f, 150f);

            Gap(panel, 26f);
            refs.JoinStatus = UiKit.MakeText(panel, "JoinStatus", "",
                18, FontStyle.Normal, UiKit.Paper, TextAnchor.UpperLeft);

            Flex(panel);
            UiKit.MakeText(panel, "Hint",
                "The table code is on your host's table screen — ask them to read it out.",
                15, FontStyle.Italic, Faded(UiKit.Paper, 0.38f), TextAnchor.LowerLeft);

            refs.JoinPanel = panel.gameObject;
        }

        // --- Screen B: your table (the waiting room) --------------------------------

        private static void BuildLobbyScreen(RectTransform card, Refs refs)
        {
            var panel = Screen(card, "LobbyScreen");
            refs.LobbyBackButton = Header(panel, "Your Table");

            // the shareable code box
            Gap(panel, 26f);
            var codeBox = new GameObject("CodeBox", typeof(RectTransform), typeof(Image));
            codeBox.transform.SetParent(panel, false);
            var boxImg = codeBox.GetComponent<Image>();
            boxImg.sprite = UiKit.Rounded(16);
            boxImg.type = Image.Type.Sliced;
            boxImg.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.10f);
            Rim(codeBox.GetComponent<RectTransform>(), 16,
                new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.30f), true);
            var boxLayout = codeBox.AddComponent<HorizontalLayoutGroup>();
            boxLayout.padding = new RectOffset(24, 24, 16, 16);
            boxLayout.spacing = 16f;
            boxLayout.childControlWidth = true;
            boxLayout.childControlHeight = true;
            boxLayout.childForceExpandWidth = false;
            boxLayout.childForceExpandHeight = false;
            boxLayout.childAlignment = TextAnchor.MiddleLeft;
            var boxLe = codeBox.AddComponent<LayoutElement>();
            boxLe.minHeight = 96f;
            boxLe.preferredHeight = 96f;
            boxLe.flexibleHeight = 0f;

            var codeCol = SubColumn(codeBox.transform, "CodeCol", 6f);
            codeCol.GetComponent<LayoutElement>().flexibleWidth = 1f;
            UiKit.MakeText(codeCol, "CodeLabel", "TABLE CODE",
                13, FontStyle.Bold, Faded(UiKit.Paper, 0.5f), TextAnchor.MiddleLeft);
            refs.CodeValue = UiKit.MakeText(codeCol, "CodeValue", "…",
                36, FontStyle.Bold, UiKit.Gold, TextAnchor.MiddleLeft);
            refs.CodeValue.resizeTextForBestFit = true;
            refs.CodeValue.resizeTextMinSize = 16;
            refs.CodeValue.resizeTextMaxSize = 36;
            refs.CodeValue.horizontalOverflow = HorizontalWrapMode.Wrap;
            refs.CopyButton = Coaster(codeBox.transform, "CopyButton", "Copy", false, 15, 48f, 100f);

            Gap(panel, 26f);
            refs.PlayersLabel = UiKit.MakeText(panel, "PlayersLabel", "PLAYERS · 0/4",
                14, FontStyle.Bold, Faded(UiKit.Paper, 0.45f), TextAnchor.UpperLeft);

            Gap(panel, 14f);
            var list = SubColumn(panel, "Slots", 12f);
            refs.Slots = new PlayerSlot[SeatCount];
            for (int i = 0; i < SeatCount; i++)
                refs.Slots[i] = BuildSlot(list, i);

            Flex(panel);
            refs.LobbyStatus = UiKit.MakeText(panel, "LobbyStatus", "",
                17, FontStyle.Italic, Faded(UiKit.Paper, 0.6f), TextAnchor.MiddleCenter);
            Gap(panel, 14f);
            refs.StartButton = Coaster(panel, "StartButton", "Start Game", true);

            refs.LobbyPanel = panel.gameObject;
        }

        private static PlayerSlot BuildSlot(Transform list, int index)
        {
            var slot = new PlayerSlot();
            var go = new GameObject($"Slot{index}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(list, false);
            slot.Root = go;
            slot.Fill = go.GetComponent<Image>();
            slot.Fill.sprite = UiKit.Rounded(14);
            slot.Fill.type = Image.Type.Sliced;
            slot.Outline = Rim(go.GetComponent<RectTransform>(), 14, Faded(UiKit.Paper, 0.16f), true);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 68f;
            le.preferredHeight = 68f;
            le.flexibleHeight = 0f;

            var avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatarGo.transform.SetParent(go.transform, false);
            slot.Avatar = avatarGo.GetComponent<Image>();
            slot.Avatar.sprite = UiKit.Rounded(20);
            slot.Avatar.type = Image.Type.Sliced;
            var avatarLe = avatarGo.AddComponent<LayoutElement>();
            avatarLe.minWidth = avatarLe.preferredWidth = 44f;
            avatarLe.minHeight = avatarLe.preferredHeight = 44f;
            slot.AvatarLetter = UiKit.MakeText(avatarGo.transform, "Letter", "",
                19, FontStyle.Bold, UiKit.Ink, TextAnchor.MiddleCenter);
            Stretch(slot.AvatarLetter.rectTransform);

            slot.Name = UiKit.MakeText(go.transform, "Name", "",
                19, FontStyle.Bold, UiKit.Paper, TextAnchor.MiddleLeft);
            slot.Name.GetComponent<RectTransform>().gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            slot.HostTag = Tag(go.transform, "HostTag", "HOST",
                new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.20f), UiKit.Gold);
            slot.YouTag = Tag(go.transform, "YouTag", "YOU",
                new Color(UiKit.Teal.r, UiKit.Teal.g, UiKit.Teal.b, 0.30f), UiKit.Paper);
            return slot;
        }

        // --- Licenses overlay ---------------------------------------------------------

        private static void BuildLicensesScreen(RectTransform card, Refs refs)
        {
            var panel = Screen(card, "LicensesScreen");
            refs.LicensesBackButton = Header(panel, "Data & Licenses");

            Gap(panel, 24f);
            UiKit.MakeText(panel, "Body",
                "The accents you'll hear are real people.\n\n" +
                "Speech clips come from FLEURS (Few-shot Learning Evaluation of Universal " +
                "Representations of Speech), by Google Research, built on the FLoRes-101 " +
                "sentence corpus by Meta AI / No Language Left Behind.\n\n" +
                "Both datasets are distributed under the Creative Commons Attribution 4.0 " +
                "International license (CC-BY-4.0):\ncreativecommons.org/licenses/by/4.0\n\n" +
                "FLEURS — huggingface.co/datasets/google/fleurs\n" +
                "FLoRes — github.com/facebookresearch/flores\n\n" +
                "This game claims no ownership over the FLEURS or FLoRes data.",
                18, FontStyle.Normal, Faded(UiKit.Paper, 0.82f), TextAnchor.UpperLeft);
            Flex(panel);

            refs.LicensesPanel = panel.gameObject;
        }

        // --- shared pieces --------------------------------------------------------

        /// <summary>Full-card screen panel with the design's 58/60/42 interior insets.</summary>
        private static RectTransform Screen(RectTransform card, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(card, false);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(58, 58, 60, 42);
            layout.spacing = 0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rt;
        }

        /// <summary>Back button (48×48) + 36px screen title. Returns the back button.</summary>
        private static Button Header(RectTransform panel, string title)
        {
            var row = Row(panel, "Header", 48f, 16f);
            var back = IconButton(row, "BackButton", "◀", 48f, 12);
            var text = UiKit.MakeText(row, "Title", title, 36, FontStyle.Bold, UiKit.Paper, TextAnchor.MiddleLeft);
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            return back;
        }

        /// <summary>Design coaster: exact palette per state (hover +8%, pressed −12%, slate disabled).</summary>
        private static Button Coaster(Transform parent, string name, string label, bool gold,
            int fontSize = 23, float height = 74f, float fixedWidth = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = UiKit.Rounded(13);
            img.type = Image.Type.Sliced;
            img.color = Color.white; // state colors carry the palette (tint multiplies)

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
            if (fixedWidth > 0f) { le.minWidth = fixedWidth; le.preferredWidth = fixedWidth; }
            else le.flexibleWidth = 1f;

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = gold ? UiKit.Gold : UiKit.Teal;
            colors.highlightedColor = gold ? UiKit.GoldHover : UiKit.TealHover;
            colors.selectedColor = gold ? UiKit.GoldHover : UiKit.TealHover; // keyboard focus reads
            colors.pressedColor = gold ? UiKit.GoldPressed : UiKit.TealPressed;
            colors.disabledColor = UiKit.SlateDisabled;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var t = UiKit.MakeText(go.transform, "Text", label.ToUpperInvariant(),
                fontSize, FontStyle.Bold, gold ? UiKit.Ink : UiKit.Paper, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            return button;
        }

        /// <summary>Square icon chip on a faint paper tile (footer gear, screen back arrows).</summary>
        private static Button IconButton(Transform parent, string name, string glyph, float size, int radius)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = UiKit.Rounded(radius);
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            var le = go.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = size;
            le.minHeight = le.preferredHeight = size;

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = Faded(UiKit.Paper, 0.08f);
            colors.highlightedColor = Faded(UiKit.Paper, 0.16f);
            colors.selectedColor = Faded(UiKit.Paper, 0.16f);
            colors.pressedColor = Faded(UiKit.Paper, 0.24f);
            colors.disabledColor = Faded(UiKit.Paper, 0.04f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var t = UiKit.MakeText(go.transform, "Glyph", glyph,
                Mathf.RoundToInt(size * 0.45f), FontStyle.Normal, UiKit.Paper, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            return button;
        }

        /// <summary>Plain text link button (footer "Data &amp; Licenses").</summary>
        private static Button LinkButton(Transform parent, string name, string label)
        {
            var t = UiKit.MakeText(parent, name, label, 16, FontStyle.Bold,
                Faded(UiKit.Paper, 0.72f), TextAnchor.MiddleLeft);
            var button = t.gameObject.AddComponent<Button>();
            button.targetGraphic = t;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.89f, 0.71f, 0.34f, 1f); // gold-ish hover
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = UiKit.GoldPressed;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
        }

        /// <summary>Small uppercase pill tag (HOST / YOU) that hugs its label.</summary>
        private static GameObject Tag(Transform parent, string name, string label, Color bg, Color fg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = UiKit.Rounded(8);
            img.type = Image.Type.Sliced;
            img.color = bg;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 6, 6);
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiKit.MakeText(go.transform, "Label", label, 12, FontStyle.Bold, fg, TextAnchor.MiddleCenter);
            return go;
        }

        /// <summary>Paper input field at coaster height (74) with the design's 22px text.</summary>
        private static InputField BigField(Transform parent, string name, string placeholder)
        {
            var field = UiKit.MakeInputField(parent, name, placeholder);
            var le = field.GetComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 74f;
            field.textComponent.fontSize = 22;
            var ph = (Text)field.placeholder;
            ph.fontSize = 22;
            foreach (var t in new[] { field.textComponent, ph })
            {
                var rt = t.rectTransform;
                rt.offsetMin = new Vector2(24f, 6f);
                rt.offsetMax = new Vector2(-24f, -6f);
            }
            return field;
        }

        /// <summary>Stretched outline image over a rounded rect (card/code-box rims, empty slots).</summary>
        private static Image Rim(RectTransform host, int radius, Color color, bool insideLayout)
        {
            var go = new GameObject("Rim", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(host, false);
            var img = go.GetComponent<Image>();
            img.sprite = UiKit.RoundedOutline(radius);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            Stretch(go.GetComponent<RectTransform>());
            if (insideLayout) go.AddComponent<LayoutElement>().ignoreLayout = true;
            return img;
        }

        private static RectTransform Row(Transform parent, string name, float height, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false; // else the row reports flexible height and grows
            layout.childAlignment = TextAnchor.MiddleLeft;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
            le.flexibleHeight = 0f;
            return go.GetComponent<RectTransform>();
        }

        /// <summary>Nested vertical stack (its own spacing) inside a screen or row.</summary>
        private static Transform SubColumn(Transform parent, string name, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            go.AddComponent<LayoutElement>().flexibleHeight = 0f;
            return go.transform;
        }

        /// <summary>Fixed-height strip whose children are positioned manually (pill, wave bars).</summary>
        private static RectTransform Holder(Transform parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
            return go.GetComponent<RectTransform>();
        }

        private static void Gap(Transform parent, float height)
        {
            var go = new GameObject("Gap", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
        }

        private static void Flex(Transform parent)
        {
            var go = new GameObject("Flex", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().flexibleHeight = 1f;
        }

        private static void FlexRow(Transform parent)
        {
            var go = new GameObject("Flex", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private static void Divider(Transform parent)
        {
            var go = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = Faded(UiKit.Paper, 0.10f);
            img.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 1f;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Color Faded(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);
    }
}
