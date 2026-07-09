using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AccentGuesser.App
{
    /// <summary>
    /// Shared primitives for every runtime-built canvas — the in-game <see cref="TavernHud"/> and
    /// the main menu — so both draw from ONE palette and one set of procedural rounded-coaster
    /// sprites instead of re-deriving the look. Extracted from TavernHud when the menu scene
    /// arrived; behaviour is identical to the original private helpers.
    ///
    /// Sprites are generated once per radius and cached (hidden, never saved): a filled rounded
    /// rect for cards/buttons, an outline ring for rims and empty slots. All are 9-sliced so one
    /// small texture scales to any control.
    /// </summary>
    public static class UiKit
    {
        // --- Palette (brief §"palette") -------------------------------------
        public static readonly Color Gold = new Color(0.847f, 0.647f, 0.227f, 1f);   // #d8a53a
        public static readonly Color Teal = new Color(0.122f, 0.435f, 0.420f, 1f);   // #1f6f6b
        public static readonly Color Coral = new Color(0.878f, 0.392f, 0.290f, 1f);  // #e0644a
        public static readonly Color Paper = new Color(0.965f, 0.953f, 0.925f, 1f);  // #f6f3ec
        public static readonly Color Shadow = new Color(0.078f, 0.106f, 0.180f, 0.88f); // #141b2e @88%
        public static readonly Color Ink = new Color(0.063f, 0.063f, 0.102f, 1f);    // #10121a

        // Button state fills from the approved menu design (+8% hover, −12% pressed, slate disabled).
        public static readonly Color GoldHover = new Color(0.890f, 0.714f, 0.341f, 1f);    // #e3b657
        public static readonly Color GoldPressed = new Color(0.722f, 0.541f, 0.173f, 1f);  // #b88a2c
        public static readonly Color TealHover = new Color(0.165f, 0.510f, 0.490f, 1f);    // #2a827d
        public static readonly Color TealPressed = new Color(0.090f, 0.329f, 0.310f, 1f);  // #17544f
        public static readonly Color SlateDisabled = new Color(0.169f, 0.192f, 0.278f, 1f); // #2b3147
        public static readonly Color TextDisabled = new Color(0.420f, 0.443f, 0.525f, 1f);  // #6b7186

        // ---- procedural 9-slice sprites ---------------------------------------

        private static readonly Dictionary<int, Sprite> _rounded = new Dictionary<int, Sprite>();
        private static readonly Dictionary<long, Sprite> _outlines = new Dictionary<long, Sprite>();

        /// <summary>Filled rounded-rect sprite of the given corner radius, 9-sliced ("coaster").</summary>
        public static Sprite Rounded(int radius = 13)
        {
            if (_rounded.TryGetValue(radius, out var cached) && cached != null) return cached;
            int size = radius * 2 + 14;
            var tex = NewTex(size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, FillAlpha(x, y, size, radius)));
            tex.Apply();
            var sprite = MakeSliced(tex, size, radius);
            _rounded[radius] = sprite;
            return sprite;
        }

        /// <summary>Rounded-rect OUTLINE sprite (a ring): card rims and "empty seat" slots.</summary>
        public static Sprite RoundedOutline(int radius = 13, int thickness = 2)
        {
            long key = ((long)radius << 8) | (uint)thickness;
            if (_outlines.TryGetValue(key, out var cached) && cached != null) return cached;
            int size = radius * 2 + 14;
            var tex = NewTex(size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // outer rounded rect minus the same rect inset by the ring thickness
                    float outer = FillAlpha(x, y, size, radius);
                    float inner = (x < thickness || y < thickness ||
                                   x >= size - thickness || y >= size - thickness)
                        ? 0f
                        : FillAlpha(x - thickness, y - thickness, size - thickness * 2,
                                    Mathf.Max(1f, radius - thickness));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(outer - inner)));
                }
            }
            tex.Apply();
            var sprite = MakeSliced(tex, size, radius);
            _outlines[key] = sprite;
            return sprite;
        }

        private static Sprite _softCircle;
        private static Sprite _gradientUp;
        private static Sprite _hatch;

        /// <summary>Soft radial glow (alpha falls smoothly to the rim) — lantern light, dust motes.</summary>
        public static Sprite SoftCircle()
        {
            if (_softCircle != null) return _softCircle;
            const int size = 128;
            var tex = NewTex(size);
            float half = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _softCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            _softCircle.hideFlags = HideFlags.HideAndDontSave;
            return _softCircle;
        }

        /// <summary>Vertical fade, opaque at the bottom to clear at the top — edge vignettes.</summary>
        public static Sprite GradientUp()
        {
            if (_gradientUp != null) return _gradientUp;
            const int w = 4, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Pow(1f - y / (float)(h - 1), 1.6f);
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _gradientUp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            _gradientUp.hideFlags = HideFlags.HideAndDontSave;
            return _gradientUp;
        }

        /// <summary>Tileable 135° pinstripe (gold at 5% baked in) — the design's backdrop hatching.</summary>
        public static Sprite Hatch()
        {
            if (_hatch != null) return _hatch;
            const int size = 52; // two 26px stripe periods per tile
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat
            };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, (x + y) % 26 < 2
                        ? new Color(Gold.r, Gold.g, Gold.b, 0.05f)
                        : Color.clear);
            tex.Apply();
            _hatch = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            _hatch.hideFlags = HideFlags.HideAndDontSave;
            return _hatch;
        }

        private static float FillAlpha(float x, float y, int size, float radius)
        {
            // distance outside the rounded-rect core; 1px feather for AA
            float dx = Mathf.Max(0f, Mathf.Max(radius - x, x - (size - 1 - radius)));
            float dy = Mathf.Max(0f, Mathf.Max(radius - y, y - (size - 1 - radius)));
            return Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
        }

        private static Texture2D NewTex(int size) =>
            new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp
            };

        private static Sprite MakeSliced(Texture2D tex, int size, int radius)
        {
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 3f, radius + 3f, radius + 3f, radius + 3f));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // ---- primitives -------------------------------------------------------

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        /// <summary>Rounded shadow-colored panel (the standard card chrome).</summary>
        public static RectTransform NewCard(string name, Transform parent, int radius = 13)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = Rounded(radius);
            img.type = Image.Type.Sliced;
            img.color = Shadow;
            return go.GetComponent<RectTransform>();
        }

        public static RectTransform NewRow(Transform parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childAlignment = TextAnchor.MiddleCenter; // a lone button sits centred, not flush-left
            go.AddComponent<LayoutElement>().minHeight = height;
            return go.GetComponent<RectTransform>();
        }

        public static Text MakeText(Transform parent, string name, string value, int size,
            FontStyle style, Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = value;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }

        public static Button MakeButton(Transform parent, string name, string label,
            Color bg, Color fg, float minWidth)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = Rounded();
            img.type = Image.Type.Sliced;
            img.color = bg;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 44;
            if (minWidth > 0f) le.minWidth = minWidth;
            else le.flexibleWidth = 1f;

            // subtle hover/press feedback on the coaster itself
            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            button.colors = colors;

            var t = MakeText(go.transform, "Text", label, 17, FontStyle.Bold, fg, TextAnchor.MiddleCenter);
            var trt = t.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            return button;
        }

        public static InputField MakeInputField(Transform parent, string name, string placeholder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = Rounded();
            img.type = Image.Type.Sliced;
            img.color = Paper;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 44;
            le.minWidth = 170;
            le.flexibleWidth = 1.4f;

            var input = go.GetComponent<InputField>();
            var ph = MakeChildText(go.transform, "Placeholder", placeholder, new Color(0.42f, 0.40f, 0.36f, 1f));
            ph.fontStyle = FontStyle.Italic;
            var txt = MakeChildText(go.transform, "Text", "", Ink);
            input.placeholder = ph;
            input.textComponent = txt;
            return input;
        }

        public static Text MakeChildText(Transform parent, string name, string value, Color color)
        {
            var t = MakeText(parent, name, value, 16, FontStyle.Normal, color, TextAnchor.MiddleLeft);
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12, 4); rt.offsetMax = new Vector2(-12, -4);
            return t;
        }
    }
}
