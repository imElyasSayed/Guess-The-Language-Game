using System.Threading.Tasks;
using AccentGuesser.Core;
using AccentGuesser.Services;
using UnityEngine;
using UnityEngine.UI;

namespace AccentGuesser.App
{
    /// <summary>
    /// Drives the single-player round loop on the 3D tavern stage. This is the 3D replacement for
    /// the placeholder <c>GameManager</c>: same Core wiring (Setup → Round → Reveal via
    /// <see cref="GameController"/>), but rendered onto the <see cref="TavernHud"/> console and the
    /// <see cref="TavernStage"/> animals instead of a flat uGUI panel.
    ///
    /// It reuses Core untouched — the presenter never holds the hidden target or duplicates scoring;
    /// it calls transitions and reads back state. The single human player is seated at
    /// <c>P1_Bulldog</c> (<see cref="SinglePlayerSeat"/>). Multiplayer is layered on later by a sibling
    /// presenter that subscribes to <c>MatchNetworkBehaviour</c> and drives the SAME stage.
    ///
    /// Added to Tavern.unity by <c>TavernSceneBuilder</c>, so the scene stays fully regenerable.
    /// Accessibility (§15): no autoplay — the clip plays only on the Play/Replay button; all controls
    /// are keyboard-navigable uGUI.
    /// </summary>
    public sealed class TavernPresenter : MonoBehaviour
    {
        /// <summary>
        /// Seat the local player occupies. Chosen on the avatar-select screen at startup
        /// (0=Bulldog, 1=Giraffe, 2=Horse, 3=Fox); multiplayer will assign it from the roster.
        /// </summary>
        private int _localSeat;

        /// <summary>Seconds for the select-screen camera to swoop down into the chosen seat.</summary>
        private const float SwoopSeconds = 1.1f;

        /// <summary>The animal the player chose (drives the first-person eye height).</summary>
        private int _myAvatar;

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

        // --- Presentation ---
        private TavernHud.Refs _ui;
        private TavernStage _stage;

        /// <summary>
        /// Start the solo flow on the shared HUD/stage (called by <see cref="TavernBootstrap"/>
        /// when the player picks "Play solo"): show the avatar select over the wide table view;
        /// picking an animal swoops the camera down into that seat (first-person, Liar's-Bar
        /// style) — you ARE that avatar, facing the other three.
        /// </summary>
        public void Boot(TavernHud.Refs ui, TavernStage stage, AudioService audio)
        {
            _ui = ui;
            _stage = stage;
            _audio = audio;

            _ui.AvatarPanel.SetActive(true);
            for (int i = 0; i < _ui.AvatarButtons.Length; i++)
            {
                int seat = i; // capture per-iteration
                _ui.AvatarButtons[i].onClick.AddListener(() => OnPickAvatar(seat));
            }

            _oracle = string.IsNullOrEmpty(_relayBaseUrl)
                ? (IOracleClient)new MockOracleClient()
                : new HttpOracleClient(_relayBaseUrl, ForbiddenFor);

            if (!ClipCatalogLoader.TryLoad(_manifestFile, out _catalog, out string err))
            {
                _ui.Status.text = $"Failed to load {_manifestFile}: {err}";
                _ui.DealButton.interactable = false;
                return;
            }

            _game = new GameController(_catalog, _rng);

            WireButtons();
            RenderSetup();
        }

        private void Update()
        {
            // Pulse the active seat's highlight with the clip amplitude so the room reads as
            // "someone is speaking"; settle back to the resting glow when nothing is playing.
            if (_stage != null && _audio != null)
                _stage.SpeakingPulse(_audio.IsPlaying ? _audio.GetAmplitude() : 0f);
        }

        // ---- Avatar selection & camera ---------------------------------------

        private static Camera SceneCamera() =>
            Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

        /// <summary>
        /// You ARE the picked animal: seat it (and three tavern regulars) at the table, then swoop
        /// first-person into your chair. Solo always sits YOU at seat 0; the remaining seats get
        /// the next animals from the lineup for company.
        /// </summary>
        private void OnPickAvatar(int avatar)
        {
            _localSeat = 0;
            _myAvatar = avatar;
            _stage.SetLocalSeat(0);
            _ui.AvatarPanel.SetActive(false);

            var cast = new int[TavernSeating.SeatCount];
            cast[0] = avatar;
            int next = avatar;
            for (int seat = 1; seat < cast.Length; seat++)
            {
                next = (next + 1) % TavernStage.AvatarCount;
                cast[seat] = next;
            }
            _stage.PopulateSeats(cast);

            // Your own body must not move under your fixed camera — any bob/glance makes your
            // own head float through your view. Freeze it on the base pose; the OTHERS keep living.
            var myIdle = _stage.IdleAt(0);
            if (myIdle != null) myIdle.Freeze();

            StartCoroutine(SwoopToSeat(0));
        }

        private System.Collections.IEnumerator SwoopToSeat(int seat)
        {
            var cam = SceneCamera();
            TavernSeating.FirstPersonPose(seat, _myAvatar, out var endPos, out var endRot);
            if (cam != null)
            {
                Vector3 startPos = cam.transform.position;
                Quaternion startRot = cam.transform.rotation;
                float startFov = cam.fieldOfView;
                // Real time, not Time.deltaTime: robust when the editor throttles frames.
                float begun = Time.realtimeSinceStartup;
                float t;
                do
                {
                    t = Mathf.Clamp01((Time.realtimeSinceStartup - begun) / SwoopSeconds);
                    float s = Mathf.SmoothStep(0f, 1f, t);
                    cam.transform.SetPositionAndRotation(
                        Vector3.Lerp(startPos, endPos, s),
                        Quaternion.Slerp(startRot, endRot, s));
                    cam.fieldOfView = Mathf.Lerp(startFov, TavernSeating.FieldOfView, s);
                    yield return null;
                } while (t < 1f);

                // Seated: enable look-around (right-drag) on top of the announcer-facing base.
                var look = cam.GetComponent<TavernLook>();
                if (look == null) look = cam.gameObject.AddComponent<TavernLook>();
                look.SetBase(endRot);
            }
            _ui.ConsoleRoot.SetActive(true);
        }

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
            _stage.ResetReactions();
            _stage.SetActiveSeat(_localSeat);
            RenderRound();
        }

        private void OnPlay()
        {
            if (_game.CurrentClip == null) return;
            _ui.Status.text = "The centerpiece speaks...";
            _audio.PlayClip(
                _game.CurrentClip.file,
                onPlaying: () => SetPlayLabel("Replay clip"),
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
            var result = _game.SubmitGuess(_ui.GuessField.text);
            _stage.ReactSeat(_localSeat, result.Correct);
            RenderReveal();
        }

        // ---- Rendering --------------------------------------------------------

        private void RenderSetup()
        {
            int langs = _catalog.GetLanguages(new ClipFilter(NullIfEmpty(_difficulty), NullIfEmpty(_region))).Count;
            _ui.Status.text = $"Welcome to the tavern — {langs} tongues to tell apart. (Hold RIGHT mouse to look around.)";
            _ui.Status.color = TavernHud.Paper;
            _ui.AnswerText.text = "";
            ShowControls(deal: true, play: false, ask: false, guess: false);
            UpdateScoreLine();
        }

        private void RenderRound()
        {
            _ui.Status.text = $"Round {_game.RoundNumber} — listen close. One question to the Keep, or trust your ear for the +15.";
            _ui.Status.color = TavernHud.Paper;
            _ui.AnswerText.text = "";
            _ui.QuestionField.text = "";
            _ui.GuessField.text = "";
            ShowControls(deal: false, play: true, ask: true, guess: true);
            _ui.QuestionField.interactable = true;
            _ui.AskButton.interactable = true;
            SetPlayLabel("Play clip");
            UpdateScoreLine();
        }

        private void RenderReveal()
        {
            var r = _game.LastResult;
            string verdict = r.Correct ? "CORRECT" : "WRONG";
            string bonus = r.Correct ? (_game.Asked ? "(asked: +10)" : "(trusted your ear: +15!)") : "(+0, streak reset)";
            _ui.Status.text =
                $"{verdict} {bonus}  —  It was {_game.Target}. You guessed \"{_game.LastGuess}\".";
            _ui.Status.color = r.Correct ? TavernHud.Gold : TavernHud.Coral;
            ShowControls(deal: true, play: true, ask: false, guess: false);
            SetPlayLabel("Replay clip");
            UpdateScoreLine();
        }

        /// <summary>Phase-adaptive control row: only the actions that make sense right now exist.</summary>
        private void ShowControls(bool deal, bool play, bool ask, bool guess)
        {
            _ui.DealButton.gameObject.SetActive(deal);
            _ui.PlayButton.gameObject.SetActive(play);
            _ui.QuestionField.gameObject.SetActive(ask);
            _ui.AskButton.gameObject.SetActive(ask);
            _ui.GuessField.gameObject.SetActive(guess);
            _ui.GuessButton.gameObject.SetActive(guess);
        }

        private void UpdateScoreLine() =>
            _ui.ScoreText.text = $"SCORE {_game.Score}   STREAK {_game.Streak}   ROUND {_game.RoundNumber}";

        private void SetPlayLabel(string label)
        {
            var t = _ui.PlayButton.GetComponentInChildren<Text>();
            if (t != null) t.text = label;
        }

        /// <summary>
        /// Forbidden-word lookup for the HttpOracleClient. Production: read
        /// StreamingAssets/forbidden/{langId}.json. Stubbed empty for the offline single-player slice.
        /// </summary>
        private string[] ForbiddenFor(string langId) => System.Array.Empty<string>();

        private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
