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

            // Full-brightness pulse range so the halo reads loud on both blue
            // and yellow backgrounds AND over the dark hold tile (sortingOrder=2
            // renders above tile sortingOrder=1 per SceneBuilder). Device playtest
            // on 2026-04-24 reported "no impact" even at 0.4-0.9; bumped to
            // 0.5-1.0 with a wider size (see SceneBuilder scale Y=0.8) to make
            // the halo unmissable.
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
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
