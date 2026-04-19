using UnityEngine;

namespace KeyFlow
{
    public class NoteSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject notePrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform judgmentPoint;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private int firstNoteHitMs = 2000;
        [SerializeField] private int noteIntervalMs = 1000;
        [SerializeField] private int totalNotes = 30;

        private int spawnedCount;

        private void Start()
        {
            audioSync.StartSilentSong();
        }

        private void Update()
        {
            if (!audioSync.IsPlaying) return;
            if (spawnedCount >= totalNotes) return;

            int nextHitMs = firstNoteHitMs + spawnedCount * noteIntervalMs;
            int previewMs = 2000;
            if (audioSync.SongTimeMs >= nextHitMs - previewMs)
            {
                SpawnNote(nextHitMs);
                spawnedCount++;
            }
        }

        private void SpawnNote(int hitTimeMs)
        {
            var go = Instantiate(notePrefab, spawnPoint.position, Quaternion.identity);
            var ctrl = go.GetComponent<NoteController>();
            ctrl.Initialize(audioSync, spawnPoint.position, judgmentPoint.position, hitTimeMs);
        }
    }
}
