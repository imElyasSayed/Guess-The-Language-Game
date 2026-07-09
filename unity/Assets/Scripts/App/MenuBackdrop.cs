using UnityEngine;
using UnityEngine.UI;

namespace AccentGuesser.App
{
    /// <summary>
    /// Breathes life into the menu's 2D backdrop: the lantern glows flicker like real flames
    /// (Perlin, never strobing) and dust motes drift slowly up through the light, swaying as they
    /// go. Pure decoration — no input, no raycasts, and real-time clocks so it keeps moving even
    /// when the editor throttles frames. Wired up by <see cref="MenuHud"/> at build time.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        private Image[] _glows;
        private float[] _glowBaseAlpha;
        private RectTransform[] _motes;
        private float[] _moteX;
        private float[] _moteStartY;
        private float[] _moteSpeed;
        private float[] _moteSeed;

        /// <summary>Glows to flicker (base alpha is read from each image's current color).</summary>
        public void SetGlows(Image[] glows)
        {
            _glows = glows;
            _glowBaseAlpha = new float[glows.Length];
            for (int i = 0; i < glows.Length; i++) _glowBaseAlpha[i] = glows[i].color.a;
        }

        /// <summary>Motes to drift; each rises at its own pace and wraps back to the bottom.</summary>
        public void SetMotes(RectTransform[] motes)
        {
            _motes = motes;
            _moteX = new float[motes.Length];
            _moteStartY = new float[motes.Length];
            _moteSpeed = new float[motes.Length];
            _moteSeed = new float[motes.Length];
            for (int i = 0; i < motes.Length; i++)
            {
                _moteX[i] = motes[i].anchoredPosition.x;
                _moteStartY[i] = motes[i].anchoredPosition.y;
                _moteSpeed[i] = 8f + 11f * (i % 5);        // 8–52 px/s, varied per mote
                _moteSeed[i] = i * 1.7f;
            }
        }

        private void Update()
        {
            float t = Time.realtimeSinceStartup;

            if (_glows != null)
            {
                for (int i = 0; i < _glows.Length; i++)
                {
                    var c = _glows[i].color;
                    c.a = _glowBaseAlpha[i] * (0.78f + 0.22f * Mathf.PerlinNoise(i * 7.3f, t * 0.7f));
                    _glows[i].color = c;
                }
            }

            if (_motes != null)
            {
                float height = ((RectTransform)transform).rect.height + 80f;
                for (int i = 0; i < _motes.Length; i++)
                {
                    float y = Mathf.Repeat(_moteStartY[i] + t * _moteSpeed[i], height) - 40f;
                    float x = _moteX[i] + Mathf.Sin(t * 0.22f + _moteSeed[i]) * 46f;
                    _motes[i].anchoredPosition = new Vector2(x, y);
                }
            }
        }
    }
}
