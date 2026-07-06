using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AccentGuesser.Core;
using AccentGuesser.Services;
using UnityEngine;
using UnityEngine.UI;

namespace AccentGuesser.App
{
    /// <summary>
    /// Boots the single-player core and drives the round loop over a BASIC uGUI layout
    /// (brief §16 step 2). Deliberately no art: buttons + text only. The 3D tavern (§12)
    /// replaces this presentation at the very end.
    ///
    /// Responsibilities:
    ///  - Build the <see cref="IClipCatalog"/> from StreamingAssets/clips.json.
    ///  - Own a <see cref="GameController"/> and translate button clicks into its transitions.
    ///  - Wire audio (AudioService) and the oracle (IOracleClient) — Mock by default.
    ///
    /// Accessibility (§15): no autoplay — the clip plays only on the Play/Replay button;
    /// all controls are standard uGUI Buttons/InputField, keyboard-navigable via the
    /// EventSystem this manager ensures exists.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("clips.json path relative to StreamingAssets.")]
        [SerializeField] private string _manifestFile = "clips.json";

        [Tooltip("Optional difficulty filter: 'common' | 'all' | empty for any.")]
        [SerializeField] private string _difficulty = "";

        [Tooltip("Optional region (continent) filter, or empty for any.")]
        [SerializeField] private string _region = "";

        [Tooltip("If set, use the real relay via HttpOracleClient; otherwise the offline MockOracleClient.")]
        [SerializeField] private string _relayBaseUrl = "";

        // --- Systems ---
        private IClipCatalog _catalog;
        private GameController _game;
        private IOracleClient _oracle;
        private AudioService _audio;
        private System.Random _rng = new System.Random();

        // --- UI refs (built at runtime by UiBuilder) ---
        private UiBuilder.Refs _ui;

        private void Start()
        {
            _ui = UiBuilder.Build(transform);

            _audio = gameObject.AddComponent<AudioService>();

            _oracle = string.IsNullOrEmpty(_relayBaseUrl)
                ? (IOracleClient)new MockOracleClient()
                : new HttpOracleClient(_relayBaseUrl, ForbiddenFor);

            if (!TryLoadCatalog(out _catalog, out string err))
            {
                _ui.Status.text = $"Failed to load {_manifestFile}: {err}";
                _ui.DealButton.interactable = false;
                return;
            }

            _game = new GameController(_catalog, _rng);

            WireButtons();
            RenderSetup();
        }

        // ---- Catalog loading from StreamingAssets ----------------------------

        private bool TryLoadCatalog(out IClipCatalog catalog, out string error)
        {
            catalog = null;
            error = null;
            string path = Path.Combine(Application.streamingAssetsPath, _manifestFile);
            try
            {
                if (!File.Exists(path)) { error = "file not found"; return false; }
                string json = File.ReadAllText(path);
                var clips = ParseClips(json);
                catalog = new JsonClipCatalog(clips);
                return clips.Count > 0;
            }
            catch (System.Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        [System.Serializable]
        private class ClipArrayWrapper { public List<ClipInfo> clips; }

        /// <summary>
        /// Parse clips.json. JsonUtility cannot parse a bare top-level array, so we wrap it.
        /// clips.json may be either a bare array [ {..}, {..} ] or { "clips": [ ... ] }.
        /// </summary>
        private static List<ClipInfo> ParseClips(string json)
        {
            string trimmed = json.TrimStart();
            string wrapped = trimmed.StartsWith("[") ? "{\"clips\":" + json + "}" : json;
            var w = JsonUtility.FromJson<ClipArrayWrapper>(wrapped);
            return w?.clips ?? new List<ClipInfo>();
        }

        /// <summary>
        /// Forbidden-word lookup for the HttpOracleClient. Production: read
        /// StreamingAssets/forbidden/{langId}.json (produced by the prep pipeline, §8).
        /// Stubbed empty here for the single-player core.
        /// </summary>
        private string[] ForbiddenFor(string langId) => System.Array.Empty<string>();

        // ---- Button wiring ----------------------------------------------------

        private void WireButtons()
        {
            _ui.DealButton.onClick.AddListener(OnDeal);
            _ui.PlayButton.onClick.AddListener(OnPlay);
            _ui.AskButton.onClick.AddListener(() => _ = OnAsk());
            _ui.GuessButton.onClick.AddListener(OnGuess);
        }

        private void OnDeal()
        {
            _game.StartRound(NullIfEmpty(_difficulty), NullIfEmpty(_region));
            RenderRound();
        }

        private void OnPlay()
        {
            if (_game.CurrentClip == null) return;
            _ui.Status.text = "The centerpiece speaks...";
            _audio.PlayClip(
                _game.CurrentClip.file,
                onPlaying: () => _ui.PlayButton.GetComponentInChildren<Text>().text = "Replay clip",
                onError: e => _ui.Status.text = $"(clip not found: {e}) — copy prep output into StreamingAssets/clips/");
        }

        private async Task OnAsk()
        {
            if (!_game.MarkAsked()) return;      // one-question lock
            _ui.AskButton.interactable = false;  // consumed for this round
            _ui.AnswerText.text = "The Keep grumbles...";
            string q = _ui.QuestionField.text;
            string answer = await _oracle.AskAsync(q, _game.CurrentClip);
            _ui.AnswerText.text = $"The Keep: \"{answer}\"";
        }

        private void OnGuess()
        {
            if (_game.Phase != GamePhase.Round) return;
            _game.SubmitGuess(_ui.GuessField.text);
            RenderReveal();
        }

        // ---- Rendering --------------------------------------------------------

        private void RenderSetup()
        {
            _ui.Status.text = $"Ready. {_catalog.GetLanguages(new ClipFilter(NullIfEmpty(_difficulty), NullIfEmpty(_region))).Count} languages loaded.";
            _ui.AnswerText.text = "";
            _ui.GuessField.interactable = false;
            _ui.GuessButton.interactable = false;
            _ui.PlayButton.interactable = false;
            _ui.AskButton.interactable = false;
            _ui.QuestionField.interactable = false;
            UpdateScoreLine();
        }

        private void RenderRound()
        {
            _ui.Status.text = $"Round {_game.RoundNumber}: press Play, ask ONE question (or skip for +15), then guess.";
            _ui.AnswerText.text = "";
            _ui.QuestionField.text = "";
            _ui.QuestionField.interactable = true;
            _ui.PlayButton.interactable = true;
            _ui.PlayButton.GetComponentInChildren<Text>().text = "Play clip";
            _ui.AskButton.interactable = true;

            _ui.GuessField.text = "";
            _ui.GuessField.interactable = true;
            _ui.GuessButton.interactable = true;
            UpdateScoreLine();
        }

        private void RenderReveal()
        {
            var r = _game.LastResult;
            string verdict = r.Correct ? "CORRECT" : "WRONG";
            string bonus = r.Correct ? (_game.Asked ? "(asked: +10)" : "(no question: +15 bonus!)") : "(+0, streak reset)";
            _ui.Status.text =
                $"{verdict} {bonus}  —  It was {_game.Target}.  You guessed {_game.LastGuess}.  Press \"Deal me in\" for the next round.";
            _ui.GuessField.interactable = false;
            _ui.GuessButton.interactable = false;
            _ui.PlayButton.interactable = true; // allow replay of the revealed clip
            _ui.AskButton.interactable = false;
            _ui.QuestionField.interactable = false;
            UpdateScoreLine();
        }

        private void UpdateScoreLine() =>
            _ui.ScoreText.text = $"Score {_game.Score}    Streak {_game.Streak}    Round {_game.RoundNumber}";

        private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
