using AccentGuesser.Net;
using AccentGuesser.Services;
using Unity.Netcode;
using UnityEngine;

namespace AccentGuesser.App
{
    /// <summary>
    /// Drives a MULTIPLAYER match on the 3D tavern stage. The host-authoritative model is never
    /// bypassed: this class holds no <c>MatchController</c>, no catalog, no oracle, and never sees
    /// the answer before REVEAL — it renders the redacted <see cref="RoundView"/> broadcast by
    /// <see cref="MatchNetworkBehaviour"/> and sends intents back as ServerRpcs (lock a guess,
    /// ask the Keep). It is the 3D replacement for the IMGUI <c>NetworkBootstrap</c> HUD.
    ///
    /// Round shape: every player gets ONE question to the Keep (ask first, then guess — the ask
    /// row disappears once you lock), every answer is broadcast to the whole table with the
    /// asker's name, and locking without asking keeps the +15 Trust-Your-Ear tier.
    ///
    /// Staging: roster order maps to seats 0–3 (join order picks your animal — Bulldog, Giraffe,
    /// Horse, Fox). Your own seat becomes your first-person body, exactly like solo; opponents'
    /// animals dim when they lock and flash green/red with their results at reveal. Players past
    /// the fourth still play fully via the HUD, watching from the selection vantage.
    /// </summary>
    public sealed class MatchTavernPresenter : MonoBehaviour
    {
        private const float SwoopSeconds = 1.1f;

        private MatchNetworkBehaviour _match;
        private AudioService _audio;
        private TavernHud.Refs _ui;
        private TavernStage _stage;

        private RoundView _view;
        private bool _haveView;
        private int _mySeat = -1;
        private bool _seated;
        private bool _myLockedThisRound;
        private bool _myAskedThisRound;
        private readonly System.Collections.Generic.List<string> _hints =
            new System.Collections.Generic.List<string>();   // this round's broadcast Q&As
        private string _baseStatus = "";
        private string _joinCode = "";

        // lobby → avatar select → play
        private int[] _avatars;          // avatar index per seat, set when picks lock
        private bool _pickedAvatar;
        private int _picksSeen;
        private bool _consoleLive;
        private bool _menuLobbyDone;     // the MENU scene was the waiting room — skip the in-tavern one

        private static string MyId => NetworkManager.Singleton.LocalClientId.ToString();

        /// <summary>
        /// Start rendering the match on the shared HUD/stage (called by TavernBootstrap).
        /// <paramref name="menuLobbyDone"/>: everyone already gathered in the main menu's waiting
        /// room and the host already pressed Start — open the avatar select immediately instead of
        /// showing the in-tavern lobby (and don't wait for the ClientRpc, which a client still
        /// scene-syncing could miss).
        /// </summary>
        public void Boot(TavernHud.Refs ui, TavernStage stage, AudioService audio,
                         MatchNetworkBehaviour match, string joinCode, bool menuLobbyDone = false)
        {
            _ui = ui;
            _stage = stage;
            _audio = audio;
            _match = match;
            _joinCode = joinCode ?? "";
            _menuLobbyDone = menuLobbyDone;

            _match.OnRoundView += HandleRoundView;
            _match.OnHint += HandleHint;
            _match.OnReveal += HandleReveal;
            _match.OnAvatarSelect += HandleAvatarSelect;
            _match.OnAvatarPicked += HandleAvatarPicked;
            _match.OnAvatarsLocked += HandleAvatarsLocked;

            if (menuLobbyDone)
            {
                HandleAvatarSelect(); // straight to "who are you tonight?"
            }
            else
            {
                // Waiting room first: the table fills up, then the HOST starts the game.
                _ui.LobbyPanel.SetActive(true);
                _ui.StartGameButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);
                _ui.StartGameButton.onClick.AddListener(() => _match.HostStartAvatarSelect());
                _ui.LobbyInfo.text = "Connecting...";
            }

            for (int i = 0; i < _ui.AvatarButtons.Length; i++)
            {
                int avatar = i; // capture per-iteration
                _ui.AvatarButtons[i].onClick.AddListener(() => OnPickAvatar(avatar));
            }

            _ui.PlayButton.onClick.AddListener(OnPlay);
            _ui.AskButton.onClick.AddListener(OnAsk);
            _ui.GuessButton.onClick.AddListener(OnLockGuess);
            ShowControls(play: false, ask: false, guess: false);
            _ui.DealButton.gameObject.SetActive(false); // the host machine deals automatically
        }

        private void OnDestroy()
        {
            if (_match == null) return;
            _match.OnRoundView -= HandleRoundView;
            _match.OnHint -= HandleHint;
            _match.OnReveal -= HandleReveal;
            _match.OnAvatarSelect -= HandleAvatarSelect;
            _match.OnAvatarPicked -= HandleAvatarPicked;
            _match.OnAvatarsLocked -= HandleAvatarsLocked;
        }

        // ---- Lobby → avatar select --------------------------------------------

        private void RenderLobby()
        {
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(_joinCode)) sb.AppendLine($"Invite friends with:  {_joinCode}");
            sb.AppendLine($"{_view.Roster.Length}/{TavernSeating.SeatCount} at the table:");
            for (int i = 0; i < _view.Roster.Length; i++)
                sb.AppendLine($"  {_view.Roster[i].Name}{(_view.Roster[i].Id == MyId ? " (you)" : "")}");
            if (!NetworkManager.Singleton.IsHost) sb.AppendLine("Waiting for the host to start...");
            _ui.LobbyInfo.text = sb.ToString().TrimEnd();
        }

        private void HandleAvatarSelect()
        {
            _ui.LobbyPanel.SetActive(false);
            _ui.AvatarPanel.SetActive(true);
            _ui.AvatarStatus.text = "Everyone picks — same animal twice is fair game.";
        }

        private void OnPickAvatar(int avatar)
        {
            if (_pickedAvatar) return;
            _pickedAvatar = true;
            _match.PickAvatarServerRpc(avatar);
            foreach (var b in _ui.AvatarButtons) b.interactable = false;
            _ui.AvatarStatus.text = $"You picked {TavernStage.AvatarNames[avatar].Substring(3)} — waiting for the others...";
        }

        private void HandleAvatarPicked(string playerId, int avatar)
        {
            _picksSeen++;
            int total = _haveView ? _view.Roster.Length : 0;
            if (total > 0 && !string.IsNullOrEmpty(_ui.AvatarStatus.text))
                _ui.AvatarStatus.text = $"{_ui.AvatarStatus.text.Split('·')[0].TrimEnd()} · {_picksSeen}/{total} picked";
        }

        private void HandleAvatarsLocked(int[] avatarsBySeat)
        {
            _avatars = avatarsBySeat;
            _stage.PopulateSeats(avatarsBySeat);
            _ui.AvatarPanel.SetActive(false);
        }

        // ---- Server → view ----------------------------------------------------

        private void HandleRoundView(RoundView v)
        {
            bool newRound = !_haveView || v.RoundNumber != _view.RoundNumber
                            || (v.Phase == NetPhase.Listen && _view.Phase != NetPhase.Listen);
            _view = v;
            _haveView = true;

            // Setup phase = the lobby (rounds haven't begun): render the waiting room — unless
            // the menu already played that part, in which case the avatar select is up instead.
            if (v.Phase == NetPhase.Setup)
            {
                if (!_menuLobbyDone) RenderLobby();
                return;
            }

            // First live round: swap the waiting-room chrome for the round console.
            if (!_consoleLive)
            {
                _consoleLive = true;
                _ui.LobbyPanel.SetActive(false);
                _ui.AvatarPanel.SetActive(false);
                _ui.ConsoleRoot.SetActive(true);
                _ui.RosterChip.SetActive(true);
            }

            SeatCameraIfNeeded();

            if (newRound && v.Phase == NetPhase.Listen)
            {
                _myLockedThisRound = false;
                _myAskedThisRound = false;
                _hints.Clear();
                _ui.AnswerText.text = "";
                _ui.GuessField.text = "";
                _ui.QuestionField.text = "";
                _stage.ResetReactions();
            }

            // Stage: locked players dim (no single asker anymore — everyone owns a question).
            for (int i = 0; i < v.Roster.Length && i < _stage.SeatCount; i++)
                _stage.SetSeatLocked(i, v.Roster[i].HasLocked);

            RenderRoster();
            if (v.Phase == NetPhase.Listen) RenderListen();
        }

        /// <summary>Every answer reaches the whole table; show the freshest two Keep replies.</summary>
        private void HandleHint(string asker, string question, string answer)
        {
            _hints.Add($"{asker} asked: “{question}” — The Keep: “{answer}”");
            int from = Mathf.Max(0, _hints.Count - 2);
            _ui.AnswerText.text = string.Join("\n", _hints.GetRange(from, _hints.Count - from));
        }

        private void HandleReveal(RoundResultView r)
        {
            bool meCorrect = false;
            int myPoints = 0;
            string myGuess = "";
            for (int i = 0; i < r.Results.Length; i++)
            {
                var pr = r.Results[i];
                int seat = SeatOf(pr.Id);
                if (pr.Id == MyId)
                {
                    meCorrect = pr.Correct;
                    myPoints = pr.Points;
                    myGuess = pr.Guess;
                    if (seat >= 0) _stage.ReactSeat(seat, pr.Correct);          // drives centre flash
                }
                else if (seat >= 0) _stage.ReactSeat(seat, pr.Correct, false);  // their animal only
            }

            string mine = meCorrect ? $"you got it, +{myPoints}!" : $"you guessed “{myGuess}” — no luck.";
            _baseStatus = $"It was {r.TargetLanguage} ({r.TargetCountry}) — {mine} Next round shortly...";
            _ui.Status.text = _baseStatus;
            _ui.Status.color = meCorrect ? TavernHud.Gold : TavernHud.Coral;
            ShowControls(play: true, ask: false, guess: false);
            SetPlayLabel("Replay clip");
        }

        // ---- Rendering ----------------------------------------------------------

        private void RenderListen()
        {
            _baseStatus = $"Round {_view.RoundNumber} — listen close. One question each, or trust your ear for the +15.";
            _ui.Status.color = TavernHud.Paper;

            // Ask first, then guess: your question disappears once spent OR once you lock.
            bool canAsk = !_myAskedThisRound && !MyAsked() && !_myLockedThisRound;
            ShowControls(play: true, ask: canAsk, guess: !_myLockedThisRound);
            SetPlayLabel("Play clip");
        }

        /// <summary>Whether the server has recorded MY question as spent this round.</summary>
        private bool MyAsked()
        {
            for (int i = 0; i < _view.Roster.Length; i++)
                if (_view.Roster[i].Id == MyId) return _view.Roster[i].HasAsked;
            return false;
        }

        private void Update()
        {
            // Live countdown while listening (server-clock based, ticks even between broadcasts).
            if (!_haveView || _view.Phase != NetPhase.Listen || NetworkManager.Singleton == null) return;
            int secs = Mathf.Max(0, (int)(_view.TimerDeadline - NetworkManager.Singleton.ServerTime.Time));
            _ui.Status.text = $"{_baseStatus}  · {secs}s";

            if (_stage != null && _audio != null)
                _stage.SpeakingPulse(_audio.IsPlaying ? _audio.GetAmplitude() : 0f);
        }

        private void RenderRoster()
        {
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(_joinCode)) sb.AppendLine($"Join: {_joinCode}");
            for (int i = 0; i < _view.Roster.Length; i++)
            {
                var p = _view.Roster[i];
                string tags = (p.HasAsked ? " ★" : "") + (p.HasLocked ? " ✓" : ""); // ★ question spent · ✓ locked
                string me = p.Id == MyId ? " (you)" : "";
                sb.AppendLine($"{p.Name}{me} · {SeatAnimal(i)} · {p.Score}{tags}");
            }
            _ui.RosterText.text = sb.ToString().TrimEnd();

            // My own score in the chip
            for (int i = 0; i < _view.Roster.Length; i++)
                if (_view.Roster[i].Id == MyId)
                    _ui.ScoreText.text = $"SCORE {_view.Roster[i].Score}   STREAK {_view.Roster[i].Streak}   ROUND {_view.RoundNumber}";
        }

        /// <summary>Display name of the animal seated at a slot (from the locked picks).</summary>
        private string SeatAnimal(int seat)
        {
            if (_avatars == null || seat < 0 || seat >= _avatars.Length) return "…";
            return TavernStage.AvatarNames[_avatars[seat]].Substring(3); // strip the "P#_" prefix
        }

        // ---- Intents (ServerRpc — the ONLY way anything leaves this client) -----

        private void OnPlay()
        {
            if (!_haveView || string.IsNullOrEmpty(_view.ClipId)) return;
            _audio.PlayClip(_view.ClipId,
                onPlaying: () => SetPlayLabel("Replay clip"),
                onError: e => { _ui.Status.text = $"(clip not found: {e})"; });
        }

        private void OnAsk()
        {
            string q = _ui.QuestionField.text;
            if (string.IsNullOrWhiteSpace(q)) return;
            _myAskedThisRound = true; // local echo — the next RoundView confirms it server-side
            _match.AskQuestionServerRpc(q);
            if (_hints.Count == 0) _ui.AnswerText.text = "The Keep grumbles...";
            ShowControls(play: true, ask: false, guess: !_myLockedThisRound);
        }

        private void OnLockGuess()
        {
            string g = _ui.GuessField.text;
            if (string.IsNullOrWhiteSpace(g)) return;
            _match.LockGuessServerRpc(g);
            _myLockedThisRound = true;
            // Locked = your round is sealed; the unused question (if any) is forfeit.
            ShowControls(play: true, ask: false, guess: false);
        }

        // ---- Seating -------------------------------------------------------------

        private int SeatOf(string playerId)
        {
            if (!_haveView || string.IsNullOrEmpty(playerId)) return -1;
            for (int i = 0; i < _view.Roster.Length; i++)
                if (_view.Roster[i].Id == playerId)
                    return i < TavernSeating.SeatCount ? i : -1;
            return -1;
        }

        /// <summary>Sit me at my roster seat the first time (or re-seat if the roster shifted).</summary>
        private void SeatCameraIfNeeded()
        {
            int seat = SeatOf(MyId);
            if (seat == _mySeat && _seated) return;
            _mySeat = seat;
            if (seat < 0) return; // fifth-plus player: spectate from the wide view, HUD fully live

            _stage.SetLocalSeat(seat);
            var idle = _stage.IdleAt(seat);
            if (idle != null) idle.Freeze();

            if (!_seated)
            {
                _seated = true;
                StartCoroutine(SwoopToSeat(seat));
            }
            else
            {
                // roster shifted (someone left): re-pin instantly
                TavernSeating.FirstPersonPose(seat, MyAvatar(seat), out var pos, out var rot);
                var cam = Camera.main;
                if (cam == null) return;
                cam.transform.SetPositionAndRotation(pos, rot);
                var look = cam.GetComponent<TavernLook>();
                if (look != null) look.SetBase(rot);
            }
        }

        /// <summary>The avatar seated at a slot (0 = bulldog fallback until picks lock).</summary>
        private int MyAvatar(int seat) =>
            _avatars != null && seat >= 0 && seat < _avatars.Length ? _avatars[seat] : 0;

        private System.Collections.IEnumerator SwoopToSeat(int seat)
        {
            var cam = Camera.main;
            TavernSeating.FirstPersonPose(seat, MyAvatar(seat), out var endPos, out var endRot);
            if (cam != null)
            {
                Vector3 startPos = cam.transform.position;
                Quaternion startRot = cam.transform.rotation;
                float startFov = cam.fieldOfView;
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

                var look = cam.GetComponent<TavernLook>();
                if (look == null) look = cam.gameObject.AddComponent<TavernLook>();
                look.SetBase(endRot);
            }
        }

        // ---- HUD helpers -----------------------------------------------------------

        private void ShowControls(bool play, bool ask, bool guess)
        {
            _ui.PlayButton.gameObject.SetActive(play);
            _ui.QuestionField.gameObject.SetActive(ask);
            _ui.AskButton.gameObject.SetActive(ask);
            _ui.GuessField.gameObject.SetActive(guess);
            _ui.GuessButton.gameObject.SetActive(guess);
        }

        private void SetPlayLabel(string label)
        {
            var t = _ui.PlayButton.GetComponentInChildren<UnityEngine.UI.Text>();
            if (t != null) t.text = label;
        }
    }
}
