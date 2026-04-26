using System.Collections.Generic;
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
        [SerializeField] private int previewMs = 2000;          // EASY: 2.0s spawn → judgment fall
        [SerializeField] private int previewMsNormal = 1400;    // NORMAL: 1.4s — faster scroll, tighter reaction

        private int CurrentPreviewMs => difficulty == Difficulty.Normal ? previewMsNormal : previewMs;

        private ChartDifficulty chart;
        private Difficulty difficulty;
        private int spawnedCount;
        private bool initialized;
        private readonly List<NoteController> liveNotes = new List<NoteController>();

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

        public void ResetForRetry()
        {
            foreach (var n in liveNotes)
                if (n != null) Destroy(n.gameObject);
            liveNotes.Clear();
            spawnedCount = 0;
            LastSpawnedHitMs = 0;
            LastSpawnedDurMs = 0;
            initialized = false;
        }

        private void Update()
        {
            if (!initialized || !audioSync.IsPlaying || audioSync.IsPaused) return;
            if (spawnedCount >= chart.notes.Count) return;

            var next = chart.notes[spawnedCount];
            if (audioSync.SongTimeMs >= next.t - CurrentPreviewMs)
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
                n.pitch,
                n.type,
                n.dur,
                spawnY, judgmentY,
                CurrentPreviewMs,
                missMs,
                onAutoMiss: missed => judgmentSystem.HandleAutoMiss(missed));
            judgmentSystem.RegisterPendingNote(ctrl);
            liveNotes.Add(ctrl);
        }
    }
}
