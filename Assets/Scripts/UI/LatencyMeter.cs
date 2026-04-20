using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    public class LatencyMeter : MonoBehaviour
    {
        [SerializeField] private Text hudText;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private TapInputHandler tapInput;
        [SerializeField] private AudioSamplePool samplePool;
        [SerializeField] private JudgmentSystem judgmentSystem;

        private float fpsAccum;
        private int fpsFrames;
        private float fpsDisplay;
        private float fpsTimer;

        private double lastTapDsp;
        private float lastFrameLatencyMs = -1;

        private float timeAtStart;
        private double dspAtStart;

        private void Start()
        {
            Application.targetFrameRate = 60;
            if (tapInput != null) tapInput.OnTap += OnTap;
            timeAtStart = Time.time;
            dspAtStart = AudioSettings.dspTime;
        }

        private void OnDestroy()
        {
            if (tapInput != null) tapInput.OnTap -= OnTap;
        }

        private void OnTap(int tapMs)
        {
            lastTapDsp = AudioSettings.dspTime;
            Invoke(nameof(MeasureFrameLatency), 0f);
        }

        private void MeasureFrameLatency()
        {
            lastFrameLatencyMs = (float)((AudioSettings.dspTime - lastTapDsp) * 1000);
        }

        private void Update()
        {
            fpsAccum += Time.unscaledDeltaTime;
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                fpsDisplay = fpsFrames / fpsAccum;
                fpsAccum = 0;
                fpsFrames = 0;
                fpsTimer = 0;
            }

            double dspElapsed = AudioSettings.dspTime - dspAtStart;
            double frameElapsed = Time.time - timeAtStart;
            double driftMs = (dspElapsed - frameElapsed) * 1000.0;

            string scoreLine = "Score: —";
            string comboLine = "Combo: 0";
            string judgLine  = "Last: —";
            if (judgmentSystem != null && judgmentSystem.Score != null)
            {
                var s = judgmentSystem.Score;
                scoreLine = $"Score: {s.Score:N0}  Stars: {s.Stars}";
                comboLine = $"Combo: {s.Combo}  Max: {s.MaxCombo}";
                judgLine  = $"Last: {judgmentSystem.LastJudgment}  (Δ {judgmentSystem.LastDeltaMs} ms)";
            }

            if (hudText != null)
            {
                hudText.text =
                    $"FPS: {fpsDisplay:F1}\n" +
                    $"{scoreLine}\n" +
                    $"{comboLine}\n" +
                    $"{judgLine}\n" +
                    $"dspTime drift: {driftMs:F1} ms\n" +
                    $"Song time: {(audioSync != null ? audioSync.SongTimeMs : 0)} ms\n" +
                    $"Buffer: {AudioSettings.GetConfiguration().dspBufferSize} samples";
            }
        }
    }
}
