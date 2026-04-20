using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

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
        public System.Action<int> OnLaneRelease;

        private readonly Dictionary<int, int> touchToLane = new Dictionary<int, int>();
        private readonly HashSet<int> pressedLanes = new HashSet<int>();
        private bool mousePressed;
        private int mouseLane = -1;

        public bool IsLanePressed(int lane) => pressedLanes.Contains(lane);

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Update()
        {
            if (audioSync != null && audioSync.IsPaused) return;
            int songTimeMs = audioSync != null ? audioSync.SongTimeMs : 0;

            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    int tid = touch.touchId.ReadValue();

                    if (touch.press.wasPressedThisFrame)
                    {
                        Vector2 pos = touch.position.ReadValue();
                        int lane = ScreenToLane(pos);
                        FirePress(tid, lane, songTimeMs);
                    }
                    else if (touch.press.wasReleasedThisFrame)
                    {
                        FireRelease(tid);
                    }
                }
            }

            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    Vector2 pos = Mouse.current.position.ReadValue();
                    int lane = ScreenToLane(pos);
                    mousePressed = true;
                    mouseLane = lane;
                    FirePressRaw(lane, songTimeMs);
                }
                else if (Mouse.current.leftButton.wasReleasedThisFrame && mousePressed)
                {
                    FireReleaseRaw(mouseLane);
                    mousePressed = false;
                    mouseLane = -1;
                }
            }
        }

        private int ScreenToLane(Vector2 screenPos)
        {
            Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10));
            return LaneLayout.XToLane(world.x, laneAreaWidth);
        }

        private void FirePress(int touchId, int lane, int songTimeMs)
        {
            touchToLane[touchId] = lane;
            pressedLanes.Add(lane);
            samplePool.PlayOneShot();
            OnTap?.Invoke(songTimeMs);
            OnLaneTap?.Invoke(songTimeMs, lane);
        }

        private void FireRelease(int touchId)
        {
            if (!touchToLane.TryGetValue(touchId, out int lane)) return;
            touchToLane.Remove(touchId);
            // Only remove from pressedLanes if no other touch is on this lane
            bool stillPressed = false;
            foreach (var kv in touchToLane) if (kv.Value == lane) { stillPressed = true; break; }
            if (!stillPressed) pressedLanes.Remove(lane);
            OnLaneRelease?.Invoke(lane);
        }

        private void FirePressRaw(int lane, int songTimeMs)
        {
            pressedLanes.Add(lane);
            samplePool.PlayOneShot();
            OnTap?.Invoke(songTimeMs);
            OnLaneTap?.Invoke(songTimeMs, lane);
        }

        private void FireReleaseRaw(int lane)
        {
            pressedLanes.Remove(lane);
            OnLaneRelease?.Invoke(lane);
        }
    }
}
