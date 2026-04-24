using System.Collections.Generic;
using UnityEngine;

namespace KeyFlow.Feedback
{
    public class LaneGlowController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] glowSprites;
        [SerializeField] private AudioSyncManager audioSync;

        private readonly HashSet<int> activeLanes = new HashSet<int>(LaneLayout.LaneCount);

        public void On(int lane)  => activeLanes.Add(lane);

        public void Off(int lane)
        {
            activeLanes.Remove(lane);
            if (lane >= 0 && lane < glowSprites.Length && glowSprites[lane] != null)
                SetAlpha(glowSprites[lane], 0f);
        }

        public void Clear()
        {
            activeLanes.Clear();
            for (int i = 0; i < glowSprites.Length; i++)
                if (glowSprites[i] != null) SetAlpha(glowSprites[i], 0f);
        }

        private void Update()
        {
            if (audioSync != null && audioSync.IsPaused) return;

            // Brighter pulse range so the halo reads clearly over both the blue
            // and yellow backgrounds AND on top of the dark hold tile when the
            // glow sprite sortingOrder=2 draws above the tile (set in SceneBuilder).
            // Range 0.4..0.9; ~1 Hz visible.
            float pulse = 0.65f + 0.25f * Mathf.Sin(Time.time * 6f);
            for (int i = 0; i < glowSprites.Length; i++)
            {
                if (glowSprites[i] == null) continue;
                float targetAlpha = activeLanes.Contains(i) ? pulse : 0f;
                SetAlpha(glowSprites[i], targetAlpha);
            }
        }

        private static void SetAlpha(SpriteRenderer sr, float a)
        {
            var c = sr.color;
            c.a = a;
            sr.color = c;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal void SetSpritesForTest(SpriteRenderer[] sprites) => glowSprites = sprites;
        internal void TickForTest() => Update();
#endif
    }
}
