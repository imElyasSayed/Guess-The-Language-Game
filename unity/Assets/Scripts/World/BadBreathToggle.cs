using UnityEngine;

namespace AccentGuesser.World
{
    /// <summary>
    /// Toggles the giraffe character's green "bad breath" on and off by showing/hiding
    /// the low-poly breath mesh baked into the model. Press <b>B</b> at runtime to
    /// toggle, or call <see cref="SetOn"/> / <see cref="Toggle"/> from game code.
    /// </summary>
    public class BadBreathToggle : MonoBehaviour
    {
        [Tooltip("The green breath mesh child (baked into the FBX as 'BadBreath').")]
        public GameObject breathMesh;

        [Tooltip("Key that toggles the effect while playing.")]
        public KeyCode toggleKey = KeyCode.B;

        [SerializeField] private bool on;

        private void Start() => Apply();

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                Toggle();
        }

        public void Toggle() => SetOn(!on);

        public void SetOn(bool value)
        {
            on = value;
            Apply();
        }

        private void Apply()
        {
            if (breathMesh != null)
                breathMesh.SetActive(on);
        }
    }
}
