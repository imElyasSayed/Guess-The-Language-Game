using AccentGuesser.World;
using UnityEngine;

namespace AccentGuesser.App
{
    /// <summary>
    /// The 3D presentation surface for a match. The five animals stand in a showroom lineup by
    /// the hearth while avatars are being chosen (the builder bakes them under
    /// <c>AvatarLineup</c>); once picks are locked, <see cref="PopulateSeats"/> CLONES the chosen
    /// animal onto each seat — clones, so two players who both pick the giraffe get two giraffes
    /// at the table. Reactions live per seat: a gold highlight on the asker, a dim "locked"
    /// state, green/red verdict flashes, and the giraffe's breath-puff gag on wrong answers.
    ///
    /// This is a pure view: it holds NO game state, no target, and no scoring. Presenters read
    /// state and call these methods; the stage only spawns models, tints lights and toggles
    /// meshes. It is a plain class (not a MonoBehaviour) so the scene builder only ever adds one
    /// component (<see cref="TavernBootstrap"/>), which owns and shares this stage.
    /// </summary>
    public sealed class TavernStage
    {
        /// <summary>All pickable animals, in lineup/button order. Index = avatar id on the wire.</summary>
        public static readonly string[] AvatarNames =
            { "P1_Bulldog", "P2_Giraffe", "P3_Horse", "P4_Fox", "P5_Cat" };

        public static int AvatarCount => AvatarNames.Length;

        // Warm lantern gold (brief palette #d8a53a) for the active seat.
        private static readonly Color GoldActive = new Color(0.847f, 0.647f, 0.227f);
        private static readonly Color GreenCorrect = new Color(0.30f, 0.90f, 0.40f);
        private static readonly Color RedWrong = new Color(0.92f, 0.32f, 0.25f);

        private const float ActiveBase = 2.0f;
        private const float ReactIntensity = 3.2f;
        private const float PulseScale = 3.0f;

        private GameObject _lineup;                       // the 5 showroom animals
        private readonly GameObject[] _lineupModels = new GameObject[AvatarNames.Length];

        private readonly GameObject[] _seats = new GameObject[TavernSeating.SeatCount];
        private readonly Light[] _highlights = new Light[TavernSeating.SeatCount];
        private readonly TavernIdle[] _idles = new TavernIdle[TavernSeating.SeatCount];
        private readonly BadBreathToggle[] _gags = new BadBreathToggle[TavernSeating.SeatCount];
        private readonly bool[] _reacting = new bool[TavernSeating.SeatCount];
        private Light _centerGlow;
        private bool _centerReacting;
        private int _activeSeat = -1;
        private int _localSeat = -1;

        public int SeatCount => TavernSeating.SeatCount;

        /// <summary>Locate the showroom lineup baked by the scene builder.</summary>
        public void Initialize()
        {
            _lineup = GameObject.Find("AvatarLineup");
            if (_lineup == null)
            {
                Debug.LogWarning("[Say Again] TavernStage: 'AvatarLineup' not found — rebuild the scene.");
            }
            else
            {
                for (int i = 0; i < AvatarNames.Length; i++)
                {
                    var t = _lineup.transform.Find(AvatarNames[i]);
                    _lineupModels[i] = t != null ? t.gameObject : null;
                    if (_lineupModels[i] == null)
                        Debug.LogWarning($"[Say Again] TavernStage: lineup model '{AvatarNames[i]}' missing.");
                }
            }
            _centerGlow = MakeCenterGlow();
        }

        /// <summary>Show/hide the five showroom animals (visible only while choosing).</summary>
        public void ShowLineup(bool visible)
        {
            if (_lineup != null) _lineup.SetActive(visible);
        }

        /// <summary>
        /// Seat the cast for this match: clone each seat's chosen animal from the lineup onto its
        /// stool. Duplicates are fine — every seat gets its own instance. Seats beyond
        /// <paramref name="avatarBySeat"/>.Length stay empty. Clears any previous cast first.
        /// </summary>
        public void PopulateSeats(int[] avatarBySeat)
        {
            ShowLineup(false);

            for (int seat = 0; seat < _seats.Length; seat++)
            {
                if (_seats[seat] != null) Object.Destroy(_seats[seat]);
                _seats[seat] = null;
                _highlights[seat] = null;
                _idles[seat] = null;
                _gags[seat] = null;
                _reacting[seat] = false;

                if (avatarBySeat == null || seat >= avatarBySeat.Length) continue;
                int avatar = Mathf.Clamp(avatarBySeat[seat], 0, AvatarNames.Length - 1);
                var source = _lineupModels[avatar];
                if (source == null) continue;

                var facing = TavernSeating.SeatFacing(seat);
                // Meshy models face local -Z (Blender forward), hence the 180° yaw.
                var rot = Quaternion.LookRotation(facing) * Quaternion.Euler(0f, 180f, 0f);
                var clone = Object.Instantiate(source, TavernSeating.SeatPosition(seat), rot);
                clone.name = $"Seat{seat}_{AvatarNames[avatar]}";
                clone.SetActive(true);

                var idle = clone.GetComponent<TavernIdle>();
                if (idle == null) idle = clone.AddComponent<TavernIdle>();
                idle.phase = seat * 1.3f;

                _seats[seat] = clone;
                _idles[seat] = idle;
                _gags[seat] = clone.GetComponent<BadBreathToggle>(); // giraffes only
                if (_gags[seat] != null) _gags[seat].SetOn(false);
                _highlights[seat] = MakeHighlight(clone.transform);
            }

            HideLocalBody(); // first-person: your own head must never block the view
        }

        // The "centerpiece speaks": a warm glow over the table that pulses with the clip audio.
        // Visible from every first-person seat (unlike a per-seat highlight, which can sit behind you).
        private static Light MakeCenterGlow()
        {
            var go = new GameObject("CenterSpeakGlow");
            go.transform.position = new Vector3(0f, 1.9f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.40f);
            l.range = 6f;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            l.enabled = false;
            return l;
        }

        private static Light MakeHighlight(Transform seat)
        {
            var go = new GameObject("SeatHighlight");
            go.transform.SetParent(seat, false);
            go.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 3.0f;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            l.enabled = false;
            return l;
        }

        /// <summary>Show/hide a seat's avatar on THIS client (renderers only; the clone stays live).</summary>
        public void SetSeatVisible(int seat, bool visible)
        {
            if (!InRange(seat) || _seats[seat] == null) return;
            foreach (var r in _seats[seat].GetComponentsInChildren<Renderer>())
                r.enabled = visible;
        }

        /// <summary>The idle animator on a seat's avatar (null if missing). Lets the presenter tune the LOCAL body.</summary>
        public TavernIdle IdleAt(int seat) => InRange(seat) ? _idles[seat] : null;

        /// <summary>
        /// Which seat the local player embodies. That seat's body is HIDDEN on this client only:
        /// the first-person camera sits just in front of its face, so any glance left or right
        /// otherwise fills the screen with your own head and neck. Other clients still render it,
        /// and effects that would land in the camera (the giraffe's breath puff) skip this seat.
        /// </summary>
        public void SetLocalSeat(int seat)
        {
            if (_localSeat == seat) return;
            if (InRange(_localSeat)) SetSeatVisible(_localSeat, true); // restore a previous body
            _localSeat = seat;
            HideLocalBody();
        }

        // Also applied at the end of PopulateSeats — the seat is usually claimed (solo pick)
        // before the clones exist, so the hide must re-run once they do.
        private void HideLocalBody()
        {
            if (InRange(_localSeat)) SetSeatVisible(_localSeat, false);
        }

        /// <summary>Highlight one seat as the active player; -1 clears all highlights.</summary>
        public void SetActiveSeat(int seat)
        {
            _activeSeat = seat;
            for (int i = 0; i < _highlights.Length; i++)
            {
                if (_idles[i] != null) _idles[i].SetSpeaking(i == seat);
                if (_reacting[i]) continue; // a reveal flash owns this light until reset
                var l = _highlights[i];
                if (l == null) continue;
                if (i == seat)
                {
                    l.color = GoldActive;
                    l.intensity = ActiveBase;
                    l.enabled = true;
                }
                else
                {
                    l.enabled = false;
                    l.intensity = 0f;
                }
            }
        }

        /// <summary>Dim a seat's highlight to signal that player has locked their guess.</summary>
        public void SetSeatLocked(int seat, bool locked)
        {
            if (!InRange(seat) || _highlights[seat] == null || _reacting[seat]) return;
            _highlights[seat].intensity = locked ? ActiveBase * 0.4f : ActiveBase;
        }

        /// <summary>
        /// Flash a seat green (correct) or red (wrong) and hold it until <see cref="ResetReactions"/>.
        /// A wrong answer also puffs that seat's breath gag if its animal has one (giraffes).
        /// <paramref name="driveCenter"/>: whether this verdict also flashes the table-centre glow —
        /// in multiplayer only the LOCAL player's result drives the centre.
        /// </summary>
        public void ReactSeat(int seat, bool correct, bool driveCenter = true)
        {
            if (!InRange(seat)) return;
            var l = _highlights[seat];
            if (l != null)
            {
                l.color = correct ? GreenCorrect : RedWrong;
                l.intensity = ReactIntensity;
                l.enabled = true;
            }
            _reacting[seat] = true;
            if (_idles[seat] != null) _idles[seat].PlayReaction(correct);
            // Breath-puff gag on that seat's giraffe — but never on the local player's own seat
            // (the puff renders straight into their first-person camera).
            if (!correct && _gags[seat] != null && seat != _localSeat)
                _gags[seat].SetOn(true);

            // In first-person your own seat is behind the camera, so mirror the verdict on the
            // table centre where every seat can see it.
            if (driveCenter && _centerGlow != null)
            {
                _centerGlow.color = correct ? GreenCorrect : RedWrong;
                _centerGlow.intensity = 4.0f;
                _centerGlow.enabled = true;
                _centerReacting = true;
            }
        }

        /// <summary>Clear all reveal flashes and breath gags before the next round.</summary>
        public void ResetReactions()
        {
            for (int i = 0; i < _highlights.Length; i++)
            {
                _reacting[i] = false;
                if (_idles[i] != null) _idles[i].SetSpeaking(false);
                if (_gags[i] != null) _gags[i].SetOn(false);
                if (_highlights[i] == null) continue;
                _highlights[i].enabled = false;
                _highlights[i].intensity = 0f;
            }
            if (_centerGlow != null)
            {
                _centerGlow.color = new Color(1f, 0.72f, 0.40f); // back to warm voice glow
                _centerGlow.intensity = 0f;
                _centerGlow.enabled = false;
                _centerReacting = false;
            }
            _activeSeat = -1;
        }

        /// <summary>
        /// Pulse the table-centre "speaking" glow (and the asker's highlight) with the clip's
        /// playback amplitude so the room reads as "someone is speaking".
        /// </summary>
        public void SpeakingPulse(float amplitude)
        {
            float a = Mathf.Clamp01(amplitude);

            // The visible cue in first-person: the table centre glows with the voice.
            // A reveal verdict flash owns this light until ResetReactions.
            if (_centerGlow != null && !_centerReacting)
            {
                _centerGlow.intensity = a * 3.5f;
                _centerGlow.enabled = a > 0.001f;
            }

            // Also pulse the active seat's highlight (seen by OTHERS in multiplayer).
            if (!InRange(_activeSeat) || _reacting[_activeSeat]) return;
            var l = _highlights[_activeSeat];
            if (l != null) l.intensity = ActiveBase + a * PulseScale;
        }

        private bool InRange(int seat) => seat >= 0 && seat < _seats.Length;
    }
}
