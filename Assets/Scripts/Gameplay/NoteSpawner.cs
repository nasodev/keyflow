using UnityEngine;
using KeyFlow.Charts;

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
        [SerializeField] private int previewMs = 2000;

        private ChartDifficulty chart;
        private Difficulty difficulty;
        private int spawnedCount;
        private bool initialized;

        public int LastSpawnedHitMs { get; private set; }
        public int LastSpawnedDurMs { get; private set; }
        public Difficulty CurrentDifficulty => difficulty;
        public int TotalNotes => chart != null ? chart.notes.Count : 0;

        public void Initialize(ChartDifficulty chartDifficulty, Difficulty diff)
        {
            this.chart = chartDifficulty;
            this.difficulty = diff;
            this.spawnedCount = 0;
            this.initialized = true;
            judgmentSystem.Initialize(chart.notes.Count, diff);
        }

        public bool AllSpawned => initialized && spawnedCount >= chart.notes.Count;

        private void Update()
        {
            if (!initialized || !audioSync.IsPlaying || audioSync.IsPaused) return;
            if (spawnedCount >= chart.notes.Count) return;

            var next = chart.notes[spawnedCount];
            if (audioSync.SongTimeMs >= next.t - previewMs)
            {
                SpawnNote(next);
                LastSpawnedHitMs = next.t;
                LastSpawnedDurMs = next.dur;
                spawnedCount++;
            }
        }

        private void SpawnNote(ChartNote n)
        {
            float laneX = LaneLayout.LaneToX(n.lane, laneAreaWidth);
            var go = Instantiate(notePrefab);
            var ctrl = go.GetComponent<NoteController>();
            int missMs = JudgmentEvaluator.GetGoodWindowMs(difficulty);
            ctrl.Initialize(
                audioSync, n.lane, laneX,
                n.t,
                n.type,
                n.dur,
                spawnY, judgmentY,
                previewMs,
                missMs,
                onAutoMiss: missed => judgmentSystem.HandleAutoMiss(missed));
            judgmentSystem.RegisterPendingNote(ctrl);
        }
    }
}
