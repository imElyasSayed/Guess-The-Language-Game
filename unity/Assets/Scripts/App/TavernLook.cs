using UnityEngine;

namespace AccentGuesser.App
{
    /// <summary>
    /// Seated first-person look-around: hold the RIGHT mouse button and drag to turn your head.
    /// Yaw/pitch are clamped to a natural seated range and applied on top of a base rotation
    /// (facing the announcer), so releasing the mouse leaves you looking where you looked.
    ///
    /// Right-drag (not cursor lock) keeps the cursor free for the HUD buttons and text fields —
    /// this is a menu-driven party game, not a shooter. Left-drag also works when the pointer
    /// isn't over the UI. Added to the camera by <see cref="TavernPresenter"/> after the
    /// seat-swoop finishes.
    /// </summary>
    public sealed class TavernLook : MonoBehaviour
    {
        [Tooltip("Degrees per mouse-axis unit.")]
        public float sensitivity = 2.4f;

        [Tooltip("How far you can turn left/right from facing the announcer (degrees).")]
        public float yawRange = 110f;

        [Tooltip("How far you can look up (degrees).")]
        public float pitchUp = 40f;

        [Tooltip("How far you can look down (degrees) — enough to see your own body.")]
        public float pitchDown = 70f;

        private Quaternion _base = Quaternion.identity;
        private float _yaw;
        private float _pitch;

        /// <summary>Set the neutral head direction (facing the announcer) and reset the gaze.</summary>
        public void SetBase(Quaternion baseRotation)
        {
            _base = baseRotation;
            _yaw = 0f;
            _pitch = 0f;
            Apply();
        }

        private void Update()
        {
            if (!Input.GetMouseButton(1) && !Input.GetMouseButton(0)) return;
            // Left-drag only counts when it starts outside the UI (the HUD owns its clicks).
            if (!Input.GetMouseButton(1) && IsPointerOverUi()) return;

            _yaw = Mathf.Clamp(_yaw + Input.GetAxis("Mouse X") * sensitivity, -yawRange, yawRange);
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * sensitivity, -pitchUp, pitchDown);
            Apply();
        }

        private void Apply() =>
            transform.rotation = _base * Quaternion.Euler(_pitch, _yaw, 0f);

        private static bool IsPointerOverUi() =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}
