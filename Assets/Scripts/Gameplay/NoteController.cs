using UnityEngine;

namespace KeyFlow
{
    public class NoteController : MonoBehaviour
    {
        [SerializeField] private int previewTimeMs = 2000;

        private AudioSyncManager audioSync;
        private int hitTimeMs;
        private int lane;
        private float spawnY;
        private float judgmentY;
        private float laneX;
        private bool initialized;
        private bool judged;
        private int missGraceMs;
        private System.Action<NoteController> onAutoMiss;

        public int HitTimeMs => hitTimeMs;
        public int Lane => lane;
        public bool Judged => judged;

        public void Initialize(
            AudioSyncManager sync,
            int lane, float laneX,
            int hitMs,
            float spawnY, float judgmentY,
            int previewMs,
            int missGraceMs,
            System.Action<NoteController> onAutoMiss)
        {
            this.audioSync = sync;
            this.lane = lane;
            this.laneX = laneX;
            this.hitTimeMs = hitMs;
            this.spawnY = spawnY;
            this.judgmentY = judgmentY;
            this.previewTimeMs = previewMs;
            this.missGraceMs = missGraceMs;
            this.onAutoMiss = onAutoMiss;
            transform.position = new Vector3(laneX, spawnY, 0);
            initialized = true;
        }

        public void MarkJudged()
        {
            judged = true;
            Destroy(gameObject);
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

            if (songTime > hitTimeMs + missGraceMs)
            {
                judged = true;
                onAutoMiss?.Invoke(this);
                Destroy(gameObject);
            }
        }
    }
}
