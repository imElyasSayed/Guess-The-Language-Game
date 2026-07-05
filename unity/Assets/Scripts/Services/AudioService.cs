using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AccentGuesser.Services
{
    /// <summary>
    /// Loads a local Ogg Vorbis clip from StreamingAssets and plays it (brief §7).
    /// No autoplay — playback happens on an explicit call driven by a button (accessibility, §15).
    ///
    /// Exposes a Replay method and a simple GetSpectrumData-based amplitude value that the
    /// (later) 3D centerpiece will use to pulse (brief §13). Amplitude works now; the visual
    /// hookup is deferred with the rest of the art.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioService : MonoBehaviour
    {
        [SerializeField] private int _spectrumSamples = 256;

        private AudioSource _source;
        private AudioClip _current;
        private string _currentRelativePath;
        private float[] _spectrum;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _spectrum = new float[Mathf.NextPowerOfTwo(_spectrumSamples)];
        }

        /// <summary>Absolute file:// URL for a clip path relative to StreamingAssets (e.g. "clips/x.ogg").</summary>
        private static string ToStreamingUrl(string relativePath)
        {
            string full = Path.Combine(Application.streamingAssetsPath, relativePath);
            // On desktop platforms streamingAssetsPath is a plain path; UnityWebRequestMultimedia
            // wants a file:// URI. On Android it is already a URL — handle both.
            if (full.Contains("://")) return full;
            return "file://" + full;
        }

        /// <summary>
        /// Load and play a clip. If it is already loaded, just replays. Invokes callbacks so
        /// the caller (GameManager) can update UI / handle a load failure gracefully.
        /// </summary>
        public void PlayClip(string relativePath, Action onPlaying = null, Action<string> onError = null)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                onError?.Invoke("empty clip path");
                return;
            }

            if (_current != null && _currentRelativePath == relativePath)
            {
                Replay();
                onPlaying?.Invoke();
                return;
            }

            StartCoroutine(LoadAndPlay(relativePath, onPlaying, onError));
        }

        private IEnumerator LoadAndPlay(string relativePath, Action onPlaying, Action<string> onError)
        {
            string url = ToStreamingUrl(relativePath);
            using var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.OGGVORBIS);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(req.error);
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null)
            {
                onError?.Invoke("failed to decode clip");
                yield break;
            }

            _current = clip;
            _currentRelativePath = relativePath;
            _source.clip = clip;
            _source.Play();
            onPlaying?.Invoke();
        }

        /// <summary>Replay the current clip from the start (the visible "Replay" control, §15).</summary>
        public void Replay()
        {
            if (_source.clip == null) return;
            _source.Stop();
            _source.Play();
        }

        public bool IsPlaying => _source != null && _source.isPlaying;

        /// <summary>
        /// Current playback amplitude in [0,1], from the FFT spectrum. Feeds the centerpiece
        /// "speaking" pulse later (brief §13). Returns 0 when nothing is playing.
        /// </summary>
        public float GetAmplitude()
        {
            if (_source == null || !_source.isPlaying) return 0f;
            _source.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
            float sum = 0f;
            for (int i = 0; i < _spectrum.Length; i++) sum += _spectrum[i];
            // Rough normalization; the centerpiece can scale/curve this to taste.
            return Mathf.Clamp01(sum * 8f);
        }
    }
}
