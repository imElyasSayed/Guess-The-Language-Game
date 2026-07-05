using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AccentGuesser.App
{
    /// <summary>
    /// Builds the BASIC placeholder uGUI at runtime so the scene needs only an empty
    /// GameObject with a <see cref="GameManager"/> on it. No art, no prefabs — buttons and
    /// text (brief §16 step 2). Guarantees an EventSystem exists for keyboard navigation (§15).
    ///
    /// This is throwaway scaffolding: the 3D tavern presentation (§12) replaces it later.
    /// </summary>
    public static class UiBuilder
    {
        public sealed class Refs
        {
            public Text Status;
            public Text ScoreText;
            public Text AnswerText;
            public Button DealButton;
            public Button PlayButton;
            public Button AskButton;
            public InputField QuestionField;
            public GameObject GuessRow;
            public Button[] GuessButtons;
            public Text[] GuessLabels;
        }

        public static Refs Build(Transform parent)
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            var root = NewPanel("Root", canvasGo.transform, new Color(0.08f, 0.10f, 0.18f, 1f));
            Stretch(root, 24);
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            var refs = new Refs();

            MakeText(root, "Title", "Say Again? — single-player core (placeholder UI)", 28, FontStyle.Bold);
            refs.ScoreText = MakeText(root, "Score", "Score 0    Streak 0    Round 0", 20, FontStyle.Normal);
            refs.Status = MakeText(root, "Status", "Loading...", 20, FontStyle.Normal);

            refs.DealButton = MakeButton(root, "DealButton", "Deal me in");
            refs.PlayButton = MakeButton(root, "PlayButton", "Play clip");

            // Question row: input field + Ask button.
            var qRow = NewRow(root, "QuestionRow");
            refs.QuestionField = MakeInputField(qRow, "QuestionField", "Ask the Keep one question...");
            refs.AskButton = MakeButton(qRow, "AskButton", "Ask the Keep");

            refs.AnswerText = MakeText(root, "Answer", "", 20, FontStyle.Italic);

            // Guess row: 4 buttons.
            var guessRow = NewRow(root, "GuessRow");
            refs.GuessRow = guessRow.gameObject;
            refs.GuessButtons = new Button[4];
            refs.GuessLabels = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                var b = MakeButton(guessRow, $"Guess{i}", $"Choice {i + 1}");
                refs.GuessButtons[i] = b;
                refs.GuessLabels[i] = b.GetComponentInChildren<Text>();
            }

            return refs;
        }

        // ---- primitives -------------------------------------------------------

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static RectTransform NewPanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        private static RectTransform NewRow(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            go.AddComponent<LayoutElement>().minHeight = 48;
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt, float margin)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
        }

        private static Text MakeText(Transform parent, string name, string value, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = value;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = style;
            t.color = new Color(0.96f, 0.95f, 0.92f, 1f);
            t.alignment = TextAnchor.MiddleLeft;
            go.AddComponent<LayoutElement>().minHeight = size + 12;
            return t;
        }

        private static Button MakeButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.44f, 0.42f, 1f);
            go.AddComponent<LayoutElement>().minHeight = 48;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 20;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        private static InputField MakeInputField(Transform parent, string name, string placeholder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.96f, 0.95f, 0.92f, 1f);
            go.AddComponent<LayoutElement>().minHeight = 48;
            var input = go.GetComponent<InputField>();

            var ph = MakeChildText(go.transform, "Placeholder", placeholder, new Color(0.3f, 0.3f, 0.3f, 1f));
            var txt = MakeChildText(go.transform, "Text", "", new Color(0.05f, 0.05f, 0.05f, 1f));
            input.placeholder = ph;
            input.textComponent = txt;
            return input;
        }

        private static Text MakeChildText(Transform parent, string name, string value, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = value;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 18;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(10, 4); rt.offsetMax = new Vector2(-10, -4);
            return t;
        }
    }
}
