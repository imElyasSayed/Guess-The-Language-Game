using UnityEngine;

namespace AccentGuesser.World
{
    /// <summary>
    /// Gives a static character some life without a skeletal rig: a gentle breathing bob and
    /// subtle sway, plus optional emphasis while "speaking" and a one-shot reveal reaction. The
    /// Meshy models ship as rigid T-poses, so this procedural motion (transform-only) is what makes
    /// the tavern feel alive until real Mixamo-style animation lands.
    ///
    /// Play-mode only (a plain MonoBehaviour). The presenter's stage flags the active speaker via
    /// <see cref="SetSpeaking"/> and fires <see cref="PlayReaction"/> at reveal; ambient idle needs
    /// no driver. Per-character <see cref="phase"/> keeps the cast from moving in lockstep.
    /// </summary>
    public class TavernIdle : MonoBehaviour
    {
        [Header("Idle")]
        [Tooltip("Vertical breathing amplitude in metres.")]
        public float bobAmplitude = 0.045f;
        [Tooltip("Breathing speed (radians/sec).")]
        public float bobSpeed = 1.7f;
        [Tooltip("Peak side-to-side sway in degrees.")]
        public float swayDegrees = 2.2f;
        [Tooltip("Sway speed (radians/sec).")]
        public float swaySpeed = 0.85f;
        [Tooltip("Per-character offset so the cast doesn't move in unison.")]
        public float phase;

        [Header("Glances")]
        [Tooltip("Peak yaw when idly looking around the room (degrees).")]
        public float glanceDegrees = 22f;

        private const float ReactDuration = 0.9f;

        private Vector3 _basePos;
        private Quaternion _baseRot;
        private float _t;
        private float _last;
        private float _speaking;      // 0 = idle, 1 = active speaker (more animated)
        private float _reactSign;     // +1 happy hop, -1 slump/shake
        private float _reactStart = -999f;

        // idle glances: ease toward a random look direction, re-rolled every few seconds
        private float _glanceYaw;
        private float _glanceTarget;
        private float _nextGlanceAt;

        // Awake, not Start: seat clones are frozen (Freeze) in the same frame they are spawned,
        // and Start would not have captured the base pose yet — Freeze would then "reset" the
        // model to an uninitialized base at the world origin (i.e. standing in the table).
        // Awake runs during Instantiate, after the spawn position is already applied.
        private void Awake()
        {
            _basePos = transform.localPosition;
            _baseRot = transform.localRotation;
            _last = Time.realtimeSinceStartup;
        }

        /// <summary>Flag this character as the active speaker (livelier bob/sway).</summary>
        public void SetSpeaking(bool on) => _speaking = on ? 1f : 0f;

        /// <summary>
        /// Stop all idle motion and settle exactly on the base pose. Used on the LOCAL player's
        /// own avatar in first-person: the camera sits just in front of its face, so any bob or
        /// glance makes your own head float distractingly through your view.
        /// </summary>
        public void Freeze()
        {
            transform.localPosition = _basePos;
            transform.localRotation = _baseRot;
            enabled = false;
        }

        /// <summary>Fire a one-shot reveal reaction: a hop when correct, a slump + head-shake when wrong.</summary>
        public void PlayReaction(bool correct)
        {
            _reactSign = correct ? 1f : -1f;
            _reactStart = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            // Real time, not Time.deltaTime: the idle must keep breathing even when the editor
            // throttles frames (unfocused window), and it is unaffected by timeScale.
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Min(now - _last, 0.1f);
            _t += dt;
            _last = now;

            float emphasis = 1f + _speaking * 0.7f;
            float bob = Mathf.Sin(_t * bobSpeed + phase) * bobAmplitude * emphasis;
            float sway = Mathf.Sin(_t * swaySpeed + phase * 1.3f) * swayDegrees * emphasis;

            // Occasionally glance around the room (at the announcer, at a neighbour...):
            // pick a new look target every few seconds and ease toward it.
            if (_t >= _nextGlanceAt)
            {
                _glanceTarget = Random.Range(-glanceDegrees, glanceDegrees);
                _nextGlanceAt = _t + Random.Range(2.5f, 6.5f);
            }
            _glanceYaw = Mathf.MoveTowards(_glanceYaw, _glanceTarget, 30f * dt);

            float reactY = 0f, reactTilt = 0f;
            float u = (now - _reactStart) / ReactDuration;
            if (u >= 0f && u <= 1f)
            {
                float decay = 1f - u;
                if (_reactSign > 0f)
                {
                    reactY = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3f)) * 0.22f * decay;   // triple hop
                }
                else
                {
                    reactY = -0.08f * decay;                                            // slump down
                    reactTilt = Mathf.Sin(u * Mathf.PI * 6f) * 6f * decay;             // shake "no"
                }
            }

            transform.localPosition = _basePos + Vector3.up * (bob + reactY);
            transform.localRotation = _baseRot * Quaternion.Euler(0f, sway + _glanceYaw, reactTilt);
        }
    }
}
