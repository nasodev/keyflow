using UnityEngine;
using KeyFlow.Charts;
using KeyFlow.Calibration;
using KeyFlow.UI;

namespace KeyFlow
{
    public class GameplayController : MonoBehaviour
    {
        [SerializeField] private CalibrationController calibration;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private NoteSpawner spawner;
        [SerializeField] private JudgmentSystem judgmentSystem;
        [SerializeField] private CompletionPanel completionPanel;

        private ChartData chart;
        private bool playing;
        private bool completed;
        private Difficulty difficulty;

        private void Start()
        {
            UserPrefs.MigrateLegacy();

            string songId = SongSession.CurrentSongId;
            if (string.IsNullOrEmpty(songId))
            {
                Debug.LogError("[KeyFlow] GameplayController.Start with no SongSession.CurrentSongId");
                return;
            }
            difficulty = SongSession.CurrentDifficulty;
            chart = ChartLoader.LoadFromStreamingAssets(songId);

            if (UserPrefs.HasCalibration)
            {
                audioSync.CalibrationOffsetSec = UserPrefs.CalibrationOffsetMs / 1000.0;
                BeginGameplay();
            }
            else
            {
                calibration.Begin(BeginGameplay);
            }
        }

        private void BeginGameplay()
        {
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
