using UnityEngine;

namespace KeyFlow
{
    public class NoteSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject notePrefab;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private JudgmentSystem judgmentSystem;
        [SerializeField] private float laneAreaWidth = 4f;
        [SerializeField] private float spawnY = 4f;
        [SerializeField] private float judgmentY = -3f;
        [SerializeField] private int firstNoteHitMs = 2000;
        [SerializeField] private int noteIntervalMs = 800;
        [SerializeField] private int totalNotes = 30;
        [SerializeField] private int previewMs = 2000;
        [SerializeField] private Difficulty difficulty = Difficulty.Normal;

        private int spawnedCount;

        public int TotalNotes => totalNotes;
        public Difficulty CurrentDifficulty => difficulty;

        private void Start()
        {
            judgmentSystem.Initialize(totalNotes, difficulty);
            audioSync.StartSilentSong();
        }

        private void Update()
        {
            if (!audioSync.IsPlaying) return;
            if (spawnedCount >= totalNotes) return;

            int nextHitMs = firstNoteHitMs + spawnedCount * noteIntervalMs;
            if (audioSync.SongTimeMs >= nextHitMs - previewMs)
            {
                SpawnNote(nextHitMs, spawnedCount % LaneLayout.LaneCount);
                spawnedCount++;
            }
        }

        private void SpawnNote(int hitTimeMs, int lane)
        {
            float laneX = LaneLayout.LaneToX(lane, laneAreaWidth);
            var go = Instantiate(notePrefab);
            var ctrl = go.GetComponent<NoteController>();
            // Miss grace = difficulty's Good window (spec §5.1)
            int missMs = difficulty == Difficulty.Easy ? 225 : 180;
            ctrl.Initialize(
                audioSync, lane, laneX,
                hitTimeMs,
                NoteType.TAP,
                0,
                spawnY, judgmentY,
                previewMs,
                missMs,
                onAutoMiss: n => judgmentSystem.HandleAutoMiss(n));
            judgmentSystem.RegisterPendingNote(ctrl);
        }
    }
}
