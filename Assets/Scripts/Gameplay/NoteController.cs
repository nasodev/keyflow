using UnityEngine;
using KeyFlow.Charts;

namespace KeyFlow
{
    public class NoteController : MonoBehaviour
    {
        [SerializeField] private int previewTimeMs = 2000;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private AudioSyncManager audioSync;
        private int hitTimeMs;
        private int pitch;
        private int lane;
        private int durMs;
        private NoteType noteType;
        private float spawnY;
        private float judgmentY;
        private float laneX;
        private float holdHalfExtent;  // 0 for TAP; half the extended scale.y for HOLD. Used to offset position so the tile's BOTTOM (not center) is at the lerp target.
        private bool initialized;
        private bool judged;
        private int missGraceMs;
        private System.Action<NoteController> onAutoMiss;

        public int HitTimeMs => hitTimeMs;
        public int Pitch => pitch;
        public int Lane => lane;
        public int DurMs => durMs;
        public NoteType Type => noteType;
        public bool Judged => judged;

        public void Initialize(
            AudioSyncManager sync,
            int lane, float laneX,
            int hitMs,
            int pitch,
            NoteType type,
            int durMs,
            float spawnY, float judgmentY,
            int previewMs,
            int missGraceMs,
            System.Action<NoteController> onAutoMiss)
        {
            this.audioSync = sync;
            this.lane = lane;
            this.laneX = laneX;
            this.hitTimeMs = hitMs;
            this.pitch = pitch;
            this.noteType = type;
            this.durMs = durMs;
            this.spawnY = spawnY;
            this.judgmentY = judgmentY;
            this.previewTimeMs = previewMs;
            this.missGraceMs = missGraceMs;
            this.onAutoMiss = onAutoMiss;
            if (type == NoteType.HOLD)
            {
                float holdHeightUnits = (durMs / (float)previewMs) * (spawnY - judgmentY);
                transform.localScale = new Vector3(
                    transform.localScale.x,
                    transform.localScale.y + holdHeightUnits,
                    transform.localScale.z);
                holdHalfExtent = holdHeightUnits * 0.5f;
            }

            transform.position = new Vector3(laneX, spawnY + holdHalfExtent, 0);
            initialized = true;
        }

        public void MarkJudged()
        {
            judged = true;
            Destroy(gameObject);
        }

        public void MarkAcceptedAsHold()
        {
            // Called by JudgmentSystem when a HOLD start tap is judged P/G/G.
            // Marks judged to block auto-miss, but leaves the GameObject alive
            // so HoldTracker can drive Completion/Broken visuals.
            judged = true;
        }

        public void MarkHoldCompleted()
        {
            Destroy(gameObject);
        }

        public void MarkHoldBroken()
        {
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            Destroy(gameObject, 0.2f);
        }

        private void Update()
        {
            if (!initialized || !audioSync.IsPlaying || audioSync.IsPaused) return;

            int songTime = audioSync.SongTimeMs;
            float progress = GameTime.GetNoteProgress(songTime, hitTimeMs, previewTimeMs);
            if (progress < 0f) return;

            // HOLD tiles: bottom of the tile is at the lerp target (not the center),
            // so that at progress=1 (hit time) the BOTTOM reaches judgmentY. The tile
            // continues scrolling through the judgment line until its top reaches
            // judgmentY at hold-end time. Previously the tile center reached
            // judgmentY at hit time, so the bottom was already well below the
            // judgment line when the player was expected to tap — and tall holds
            // appeared to "come from the middle of the screen" because their
            // bottom halves were already below the camera frame.
            transform.position = new Vector3(
                laneX,
                Mathf.LerpUnclamped(spawnY, judgmentY, progress) + holdHalfExtent,
                0);

            // Auto-miss: start tap never came within the miss window. Skip once the
            // note has been judged (e.g., HOLD whose start tap was accepted); it
            // keeps scrolling visually but is no longer eligible for auto-miss.
            if (!judged && songTime > hitTimeMs + missGraceMs)
            {
                judged = true;
                onAutoMiss?.Invoke(this);

                if (noteType == NoteType.HOLD)
                {
                    // Grey the tile and let it keep scrolling through the judgment
                    // line until the hold's would-have-ended time. Previously a
                    // 4-second hold tile would vanish ~100 ms after the hit window
                    // closed, which is jarring — the user sees a huge bar, fails
                    // once, and it's gone. Now the missed hold scrolls past just
                    // like a hit one, so the player gets closure on the beat.
                    if (spriteRenderer != null)
                        spriteRenderer.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                    float remainingSec = (hitTimeMs + durMs - songTime) / 1000f;
                    Destroy(gameObject, Mathf.Max(0.2f, remainingSec));
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal void SetForTest(int lane, int hitTimeMs, int durMs, int pitch, NoteType type)
        {
            this.lane = lane;
            this.hitTimeMs = hitTimeMs;
            this.durMs = durMs;
            this.pitch = pitch;
            this.noteType = type;
            // Do NOT flip `initialized` — Update guards on it, but the tests don't call Update.
        }
#endif
    }
}
