using UnityEngine;
using UnityEngine.InputSystem;

namespace KeyFlow
{
    public class TapInputHandler : MonoBehaviour
    {
        [SerializeField] private AudioSamplePool samplePool;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private float laneAreaWidth = 4f;

        public System.Action<int> OnTap;
        public System.Action<int, int> OnLaneTap;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Update()
        {
            bool tapped = false;
            Vector2 screenPos = Vector2.zero;

            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.wasPressedThisFrame)
                    {
                        tapped = true;
                        screenPos = touch.position.ReadValue();
                        break;
                    }
                }
            }

            if (!tapped && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                tapped = true;
                screenPos = Mouse.current.position.ReadValue();
            }

            if (!tapped) return;

            samplePool.PlayOneShot();
            int songTimeMs = audioSync != null ? audioSync.SongTimeMs : 0;

            Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10));
            int lane = LaneLayout.XToLane(world.x, laneAreaWidth);

            OnTap?.Invoke(songTimeMs);
            OnLaneTap?.Invoke(songTimeMs, lane);
        }
    }
}
