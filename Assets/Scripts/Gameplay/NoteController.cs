using UnityEngine;

namespace KeyFlow
{
    public class NoteController : MonoBehaviour
    {
        [SerializeField] private int previewTimeMs = 2000;

        private AudioSyncManager audioSync;
        private Vector3 spawnPos;
        private Vector3 judgmentPos;
        private int hitTimeMs;
        private bool initialized;

        public int HitTimeMs => hitTimeMs;

        public void Initialize(AudioSyncManager sync, Vector3 spawn, Vector3 judgment, int hitMs, int previewMs = 2000)
        {
            audioSync = sync;
            spawnPos = spawn;
            judgmentPos = judgment;
            hitTimeMs = hitMs;
            previewTimeMs = previewMs;
            transform.position = spawn;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || !audioSync.IsPlaying) return;

            float progress = GameTime.GetNoteProgress(audioSync.SongTimeMs, hitTimeMs, previewTimeMs);
            if (progress < 0f) return;

            transform.position = Vector3.LerpUnclamped(spawnPos, judgmentPos, progress);

            if (audioSync.SongTimeMs > hitTimeMs + 500)
            {
                Destroy(gameObject);
            }
        }
    }
}
