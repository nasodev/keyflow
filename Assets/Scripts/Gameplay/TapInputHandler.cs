using UnityEngine;
using UnityEngine.InputSystem;

namespace KeyFlow
{
    public class TapInputHandler : MonoBehaviour
    {
        [SerializeField] private AudioSamplePool samplePool;
        [SerializeField] private AudioSyncManager audioSync;

        public System.Action<int> OnTap;

        private void Update()
        {
            bool tapped = false;

            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.wasPressedThisFrame) { tapped = true; break; }
                }
            }

            if (!tapped && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                tapped = true;
            }

            if (tapped)
            {
                samplePool.PlayOneShot();
                OnTap?.Invoke(audioSync.SongTimeMs);
            }
        }
    }
}
