using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using KeyFlow;

namespace KeyFlow.Calibration
{
    public class CalibrationController : MonoBehaviour
    {
        private const string PrefsKey = "CalibOffsetMs";
        private const int ClickCount = 8;
        private const double IntervalSec = 0.5;
        private const double LeadInSec = 2.0;
        private const int MaxRetries = 3;

        [SerializeField] private AudioSource[] clickSources;
        [SerializeField] private AudioClip clickSample;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private Text promptText;
        [SerializeField] private Button startButton;
        [SerializeField] private Image[] beatIndicators;

        public System.Action OnCalibrationDone;

        private int retryCount;
        private bool running;
        private readonly List<double> tapDspTimes = new List<double>();
        private double[] expectedDspTimes;

        public static bool HasSavedOffset() => PlayerPrefs.HasKey(PrefsKey);

        public static int LoadSavedOffsetMs() => PlayerPrefs.GetInt(PrefsKey, 0);

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void Begin()
        {
            gameObject.SetActive(true);
            retryCount = 0;
            ShowIdle("화면 아무 곳이나, 클릭 소리에 맞춰 8번 탭하세요.");
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartOneRun);
        }

        private void ShowIdle(string message)
        {
            promptText.text = message;
            startButton.gameObject.SetActive(true);
            foreach (var img in beatIndicators) img.color = Color.gray;
        }

        private void StartOneRun()
        {
            startButton.gameObject.SetActive(false);
            promptText.text = "준비...";
            StartCoroutine(RunCalibration());
        }

        private IEnumerator RunCalibration()
        {
            running = true;
            tapDspTimes.Clear();
            double start = AudioSettings.dspTime + LeadInSec;
            expectedDspTimes = new double[ClickCount];
            for (int i = 0; i < ClickCount; i++)
            {
                expectedDspTimes[i] = start + i * IntervalSec;
                if (i < clickSources.Length && clickSources[i] != null)
                {
                    clickSources[i].clip = clickSample;
                    clickSources[i].PlayScheduled(expectedDspTimes[i]);
                }
            }

            // Flash indicators at each click time
            for (int i = 0; i < ClickCount; i++)
            {
                while (AudioSettings.dspTime < expectedDspTimes[i]) yield return null;
                if (i < beatIndicators.Length) beatIndicators[i].color = Color.white;
                promptText.text = $"탭 {i + 1} / {ClickCount}";
            }

            // Wait 500ms tail after last click
            double tailEnd = expectedDspTimes[ClickCount - 1] + 0.5;
            while (AudioSettings.dspTime < tailEnd) yield return null;

            running = false;
            Evaluate();
        }

        private void Update()
        {
            if (!running) return;

            if (Touchscreen.current != null)
                foreach (var t in Touchscreen.current.touches)
                    if (t.press.wasPressedThisFrame)
                        tapDspTimes.Add(AudioSettings.dspTime);

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                tapDspTimes.Add(AudioSettings.dspTime);
        }

        private void Evaluate()
        {
            var result = CalibrationCalculator.Compute(expectedDspTimes, tapDspTimes.ToArray());

            if (result.reliable)
            {
                Save(result.offsetMs);
                Finish();
            }
            else
            {
                retryCount++;
                if (retryCount >= MaxRetries)
                {
                    Save(0);
                    Finish();
                }
                else
                {
                    ShowIdle($"결과가 흔들려요 (MAD {result.madMs}ms). 다시 해보세요. [{retryCount}/{MaxRetries - 1}]");
                }
            }
        }

        private void Save(int offsetMs)
        {
            PlayerPrefs.SetInt(PrefsKey, offsetMs);
            PlayerPrefs.Save();
            if (audioSync != null) audioSync.CalibrationOffsetSec = offsetMs / 1000.0;
        }

        private void Finish()
        {
            gameObject.SetActive(false);
            OnCalibrationDone?.Invoke();
        }
    }
}
