using System;
using UnityEngine;

namespace KeyFlow.UI
{
    public class CountdownOverlay : MonoBehaviour, ICountdownOverlay
    {
        [SerializeField] private CountdownNumberPopup popup;
        [SerializeField] private GameObject pauseButtonRoot;

        [Header("Timing")]
        [SerializeField] private float stepDurationSec = 1.0f;
        [SerializeField] private float popupLifetimeSec = 0.9f;
        [SerializeField] private float goHoldSec = 0.4f;
        [SerializeField] private float goPitch = 1.5f;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color goColor = new Color(1f, 0.843f, 0f, 1f);

        [Header("Audio (production wiring)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip clickSample;

        private IClickPlayer clickPlayer;
        private Action onComplete;
        private float startTime;
        // lastStepFired values: -1 = pre-init / reset (Update early-returns),
        // 0=Step3, 1=Step2, 2=Step1, 3=StepGo, 4=Complete. -1 is transient —
        // BeginCountdown resets it then immediately calls EnterStep(0), so the
        // value is never observed as a steady state after BeginCountdown returns.
        private int lastStepFired = -1;

        private void Awake()
        {
            if (clickPlayer == null && audioSource != null && clickSample != null)
                clickPlayer = new AudioSourceClickPlayer(audioSource, clickSample);
        }

        public void BeginCountdown(Action onComplete)
        {
            // CountdownCanvas is saved SetActive(false) to avoid SP10 ScreenManager.Start
            // race (see spec §2.3 guardrail). Reactivate on demand.
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            this.onComplete = onComplete;
            this.startTime = Time.time;
            this.lastStepFired = -1;
            // Hide pause button BEFORE firing Step3 so "hide then start" ordering
            // is explicit, not dependent on same-frame Unity batching.
            if (pauseButtonRoot != null) pauseButtonRoot.SetActive(false);
            EnterStep(0, Time.time);
        }

        private void Update()
        {
            if (lastStepFired == -1 || lastStepFired == 4) return;
            Tick(Time.time - startTime);
        }

        private void Tick(float elapsed)
        {
            int step = StepForElapsed(elapsed);
            if (step > lastStepFired)
            {
                // Fire through all missed transitions in order
                for (int s = lastStepFired + 1; s <= step; s++)
                    EnterStep(s, startTime + elapsed);
            }
        }

        private int StepForElapsed(float elapsed)
        {
            if (elapsed < stepDurationSec) return 0;
            if (elapsed < 2 * stepDurationSec) return 1;
            if (elapsed < 3 * stepDurationSec) return 2;
            if (elapsed < 3 * stepDurationSec + goHoldSec) return 3;
            return 4;
        }

        private void EnterStep(int step, float now)
        {
            if (step == lastStepFired) return;
            lastStepFired = step;
            switch (step)
            {
                case 0: FireStep("3", normalColor, 1.0f, now); break;
                case 1: FireStep("2", normalColor, 1.0f, now); break;
                case 2: FireStep("1", normalColor, 1.0f, now); break;
                case 3: FireStep("GO!", goColor, goPitch, now); break;
                case 4:
                    if (pauseButtonRoot != null) pauseButtonRoot.SetActive(true);
                    onComplete?.Invoke();
                    break;
            }
        }

        private void FireStep(string label, Color color, float pitch, float now)
        {
            if (clickPlayer != null) clickPlayer.Play(pitch);
            if (popup != null) popup.Activate(now, popupLifetimeSec, label, color);
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal void SetDependenciesForTest(IClickPlayer cp, CountdownNumberPopup p, GameObject pauseRoot)
        {
            this.clickPlayer = cp;
            this.popup = p;
            this.pauseButtonRoot = pauseRoot;
        }

        // Parameter is elapsed seconds since BeginCountdown, not absolute time.
        // This differs from CountdownNumberPopup.TickForTest(absoluteTime) to keep
        // overlay tests independent of Time.time.
        internal void TickForTest(float simulatedElapsed) => Tick(simulatedElapsed);
#endif
    }
}
