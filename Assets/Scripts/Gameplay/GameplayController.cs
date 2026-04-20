using UnityEngine;

namespace KeyFlow
{
    public class GameplayController : MonoBehaviour
    {
        [SerializeField] private string songId = "beethoven_fur_elise";
        [SerializeField] private Difficulty difficulty = Difficulty.Easy;

        [SerializeField] private CalibrationController calibration;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private NoteSpawner spawner;
        [SerializeField] private JudgmentSystem judgmentSystem;
        [SerializeField] private CompletionPanel completionPanel;

        private ChartData chart;
        private bool playing;
        private bool completed;

        private void Start()
        {
            chart = ChartLoader.LoadFromStreamingAssets(songId);

            if (CalibrationController.HasSavedOffset())
            {
                audioSync.CalibrationOffsetSec = CalibrationController.LoadSavedOffsetMs() / 1000.0;
                BeginGameplay();
            }
            else
            {
                calibration.OnCalibrationDone = BeginGameplay;
                calibration.Begin();
            }
        }

        private void BeginGameplay()
        {
            calibration.OnCalibrationDone = null;
            var chartDiff = chart.charts[difficulty];
            spawner.Initialize(chartDiff, difficulty);
            audioSync.StartSilentSong();
            playing = true;
        }

        private void Update()
        {
            if (!playing || completed) return;
            if (!spawner.AllSpawned) return;

            int missWindowMs = JudgmentEvaluator.GetGoodWindowMs(difficulty);
            int endSongMs = spawner.LastSpawnedHitMs + spawner.LastSpawnedDurMs + missWindowMs;
            if (audioSync.SongTimeMs < endSongMs) return;

            int judgedExpected = spawner.TotalNotes;
            if (judgmentSystem.Score != null && judgmentSystem.Score.JudgedCount < judgedExpected) return;

            completed = true;
            completionPanel.Show(judgmentSystem.Score);
        }
    }
}
