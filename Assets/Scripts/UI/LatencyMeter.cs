using System.Text;
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

        private const float HUD_UPDATE_INTERVAL_SEC = 0.5f;

        private readonly StringBuilder sb = new StringBuilder(256);

        private float fpsAccum;
        private int fpsFrames;
        private float fpsDisplay;
        private float fpsTimer;

        private double lastTapDsp;
        private float lastFrameLatencyMs = -1;

        private float timeAtStart;
        private double dspAtStart;
        private float hudTimer;

        private void Start()
        {
            // targetFrameRate is release-relevant global — set before any
            // isDebugBuild gate so it takes effect in production too.
            Application.targetFrameRate = 60;

            // Debug.isDebugBuild is true in the Editor and in Development
            // Builds (our keyflow-w6-sp10-profile.apk), false in release
            // Builds (our keyflow-w6-sp10.apk). Hide the HUD text and
            // short-circuit Update() in release so end-users never see the
            // FPS / drift / buffer debug overlay.
            if (!Debug.isDebugBuild)
            {
                if (hudText != null) hudText.enabled = false;
                enabled = false;
                return;
            }

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
            // FPS accumulation — every frame, alloc-free
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

            // HUD text update — throttled to 2 Hz to keep allocation budget low.
            // Per-frame updates would accumulate via sb.ToString() + hudText.text
            // setter; at 0.5s cadence the combined residual is below Unity GC
            // threshold across a typical 2-minute gameplay session.
            hudTimer += Time.unscaledDeltaTime;
            if (hudTimer < HUD_UPDATE_INTERVAL_SEC) return;
            hudTimer = 0;

            if (hudText == null) return;

            double dspElapsed = AudioSettings.dspTime - dspAtStart;
            double frameElapsed = Time.time - timeAtStart;
            float driftMs = (float)((dspElapsed - frameElapsed) * 1000.0);

            sb.Length = 0;
            sb.Append("FPS: ");
            AppendOneDecimal(sb, fpsDisplay);
            sb.Append('\n');

            if (judgmentSystem != null && judgmentSystem.Score != null)
            {
                var s = judgmentSystem.Score;
                sb.Append("Score: ").Append(s.Score)
                  .Append("  Stars: ").Append(s.Stars).Append('\n');
                sb.Append("Combo: ").Append(s.Combo)
                  .Append("  Max: ").Append(s.MaxCombo).Append('\n');
                sb.Append("Last: ").Append(JudgmentName(judgmentSystem.LastJudgment))
                  .Append("  (Δ ").Append(judgmentSystem.LastDeltaMs).Append(" ms)\n");
            }
            else
            {
                sb.Append("Score: —\n");
                sb.Append("Combo: 0\n");
                sb.Append("Last: —\n");
            }

            sb.Append("dspTime drift: ");
            AppendOneDecimal(sb, driftMs);
            sb.Append(" ms\n");
            sb.Append("Song time: ").Append(audioSync != null ? audioSync.SongTimeMs : 0).Append(" ms\n");
            sb.Append("Buffer: ").Append(AudioSettings.GetConfiguration().dspBufferSize).Append(" samples");

            hudText.text = sb.ToString();
        }

        // Alloc-free enum → constant-string conversion. Avoids Enum.ToString()
        // which boxes the value.
        private static string JudgmentName(Judgment j)
        {
            switch (j)
            {
                case Judgment.Perfect: return "Perfect";
                case Judgment.Great:   return "Great";
                case Judgment.Good:    return "Good";
                case Judgment.Miss:    return "Miss";
                default:               return "—";
            }
        }

        // Alloc-free "N.N" formatting for floats. Handles sign + one decimal
        // place. Unity's StringBuilder.Append(int) uses an internal no-alloc
        // path under IL2CPP.
        private static void AppendOneDecimal(StringBuilder target, float value)
        {
            if (value < 0)
            {
                target.Append('-');
                value = -value;
            }
            int whole = (int)value;
            int frac = (int)((value - whole) * 10f + 0.5f);
            if (frac >= 10) { whole += 1; frac -= 10; }
            target.Append(whole).Append('.').Append(frac);
        }
    }
}
