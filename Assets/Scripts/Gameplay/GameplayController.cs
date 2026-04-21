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
        [SerializeField] private ResultsScreen resultsScreen;

        [SerializeField] private HoldTracker holdTracker;

        private ChartData chart;
        private bool playing;
        private bool completed;
        private Difficulty difficulty;
        private bool prefsMigrated;

        private void OnEnable()
        {
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.OnReplaced += HandleScreenReplaced;
        }

        private void OnDisable()
        {
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.OnReplaced -= HandleScreenReplaced;
        }

        private void HandleScreenReplaced(AppScreen target)
        {
            if (target == AppScreen.Gameplay) ResetAndStart();
        }

        public void ResetAndStart()
        {
            if (!prefsMigrated) { UserPrefs.MigrateLegacy(); prefsMigrated = true; }

            string songId = SongSession.CurrentSongId;
            if (string.IsNullOrEmpty(songId))
            {
                Debug.LogError("[KeyFlow] GameplayController.ResetAndStart with no SongSession.CurrentSongId");
                return;
            }
            difficulty = SongSession.CurrentDifficulty;

            playing = false;
            completed = false;

            StartCoroutine(ChartLoader.LoadFromStreamingAssetsCo(
                songId,
                loaded => { chart = loaded; ContinueAfterChartLoaded(); },
                err => Debug.LogError($"[KeyFlow] chart load failed: {err}")));
        }

        private void ContinueAfterChartLoaded()
        {
            spawner.ResetForRetry();
            holdTracker.ResetForRetry();
            judgmentSystem.ResetForRetry();

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
            var score = judgmentSystem.Score;
            bool newRecord = UserPrefs.TrySetBest(
                SongSession.CurrentSongId, SongSession.CurrentDifficulty,
                score.Stars, score.Score);
            SongSession.LastScore = score;
            ScreenManager.Instance.Replace(AppScreen.Results);
            resultsScreen.Display(score, newRecord);
        }
    }
}
