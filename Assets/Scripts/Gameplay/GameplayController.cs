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
        [SerializeField] private CountdownOverlay countdown;
        private ICountdownOverlay countdownOverride;

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
            // Reset prior-session audio state BEFORE Initialize so NoteSpawner (which
            // gates on audioSync.IsPlaying) stays dormant through the countdown window.
            // Without this, a retry / 2nd-song scenario floods the screen with notes.
            audioSync.Stop();
            spawner.Initialize(chartDiff, difficulty);
            StartCountdownAndDeferAudio();
        }

        private void StartCountdownAndDeferAudio()
        {
            ICountdownOverlay cd = countdownOverride ?? (ICountdownOverlay)countdown;
            cd.BeginCountdown(() =>
            {
                audioSync.StartSilentSong();
                playing = true;
            });
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal void SetCountdownForTest(ICountdownOverlay c) => countdownOverride = c;
        // Mirrors BeginGameplay's tail: reset prior audio state, then start countdown.
        // Skips the chart/spawner setup that requires full scene state.
        internal void InvokeStartCountdownForTest()
        {
            audioSync.Stop();
            StartCountdownAndDeferAudio();
        }
        internal bool PlayingForTest => playing;
#endif

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
