using UnityEngine;

namespace KeyFlow
{
    public class NoteController : MonoBehaviour
    {
        [SerializeField] private int previewTimeMs = 2000;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private AudioSyncManager audioSync;
        private int hitTimeMs;
        private int lane;
        private int durMs;
        private NoteType noteType;
        private float spawnY;
        private float judgmentY;
        private float laneX;
        private bool initialized;
        private bool judged;
        private int missGraceMs;
        private System.Action<NoteController> onAutoMiss;

        public int HitTimeMs => hitTimeMs;
        public int Lane => lane;
        public int DurMs => durMs;
        public NoteType Type => noteType;
        public bool Judged => judged;

        public void Initialize(
            AudioSyncManager sync,
            int lane, float laneX,
            int hitMs,
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
            this.noteType = type;
            this.durMs = durMs;
            this.spawnY = spawnY;
            this.judgmentY = judgmentY;
            this.previewTimeMs = previewMs;
            this.missGraceMs = missGraceMs;
            this.onAutoMiss = onAutoMiss;
            transform.position = new Vector3(laneX, spawnY, 0);

            if (type == NoteType.HOLD)
            {
                float holdHeightUnits = (durMs / (float)previewMs) * (spawnY - judgmentY);
                transform.localScale = new Vector3(
                    transform.localScale.x,
                    transform.localScale.y + holdHeightUnits,
                    transform.localScale.z);
            }

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
            if (!initialized || judged || !audioSync.IsPlaying) return;

            int songTime = audioSync.SongTimeMs;
            float progress = GameTime.GetNoteProgress(songTime, hitTimeMs, previewTimeMs);
            if (progress < 0f) return;

            transform.position = new Vector3(
                laneX,
                Mathf.LerpUnclamped(spawnY, judgmentY, progress),
                0);

            // Auto-miss: start tap never came within the miss window.
            // Applies to both TAP and HOLD (HOLD whose start tap was accepted
            // is already `judged=true` and returns at the top of Update).
            if (songTime > hitTimeMs + missGraceMs)
            {
                judged = true;
                onAutoMiss?.Invoke(this);
                Destroy(gameObject);
            }
        }
    }
}
