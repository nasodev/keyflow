using System.Collections.Generic;
using UnityEngine;
using KeyFlow.Feedback;

namespace KeyFlow
{
    public class HoldTracker : MonoBehaviour
    {
        // Default 250 ms = 8th note at BPM 120. SetBpmForRetrigger overrides per song.
        private const int   HOLD_RETRIGGER_FALLBACK_MS = 250;
        private const float HOLD_RETRIGGER_VOLUME      = 0.3f;
        private int retriggerIntervalMs = HOLD_RETRIGGER_FALLBACK_MS;

        public void SetBpmForRetrigger(int bpm)
        {
            if (bpm <= 0) { retriggerIntervalMs = HOLD_RETRIGGER_FALLBACK_MS; return; }
            // 8th note in ms = (60_000 / bpm) / 2
            retriggerIntervalMs = 60_000 / bpm / 2;
        }

        [SerializeField] private TapInputHandler tapInput;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private JudgmentSystem judgmentSystem;
        [SerializeField] private AudioSamplePool audioPool;
        [SerializeField] private LaneGlowController laneGlow;

        private readonly HoldStateMachine stateMachine = new HoldStateMachine();
        private readonly Dictionary<int, NoteController> idToNote = new Dictionary<int, NoteController>();
        private readonly HashSet<int> pressed = new HashSet<int>();
        private readonly List<HoldTransition> transitionBuffer = new List<HoldTransition>();

        private struct HoldAudioState { public int pitch; public int lastRetriggerMs; }
        private readonly Dictionary<int, HoldAudioState> holdAudio
            = new Dictionary<int, HoldAudioState>(LaneLayout.LaneCount * 2);
        // Buffers ids of holdAudio entries that retriggered this tick, so we can
        // write the updated lastRetriggerMs back after iteration completes.
        // Empirical result (2026-04-23, Unity 6000.3.13f1 Mono EditMode): value-only
        // indexer-set during `foreach` on Dictionary<int, HoldAudioState> throws
        // InvalidOperationException. Spec's "indexer doesn't bump version counter"
        // assumption did not hold; this is the Risks-table fallback pattern.
        private readonly List<int> retriggerBuffer = new List<int>(LaneLayout.LaneCount * 2);

        public void ResetForRetry()
        {
            stateMachine.Clear();
            idToNote.Clear();
            holdAudio.Clear();
            if (laneGlow != null) laneGlow.Clear();
        }

        public void OnHoldStartTapAccepted(NoteController note, int tapTimeMs)
        {
            int endMs = note.HitTimeMs + note.DurMs;
            int id = stateMachine.Register(note.Lane, note.HitTimeMs, endMs);
            stateMachine.OnStartTapAccepted(id);
            idToNote[id] = note;
            holdAudio[id] = new HoldAudioState
            {
                pitch = note.Pitch,
                lastRetriggerMs = tapTimeMs,
            };
            if (laneGlow != null) laneGlow.On(note.Lane);
        }

        private void Update()
        {
            if (!audioSync.IsPlaying || audioSync.IsPaused) return;
            if (idToNote.Count == 0 && holdAudio.Count == 0) return;

            pressed.Clear();
            if (tapInput != null)
            {
                for (int lane = 0; lane < LaneLayout.LaneCount; lane++)
                    if (tapInput.IsLanePressed(lane)) pressed.Add(lane);
            }
            else
            {
                // EditMode-only path: SetDependenciesForTest can omit tapInput to
                // isolate retrigger behavior from input mechanics. Treat all lanes
                // as pressed so holds don't transition to Broken mid-test.
                for (int lane = 0; lane < LaneLayout.LaneCount; lane++)
                    pressed.Add(lane);
            }

            stateMachine.Tick(audioSync.SongTimeMs, pressed, transitionBuffer);
            foreach (var t in transitionBuffer)
            {
                if (!idToNote.TryGetValue(t.id, out var note)) continue;

                if (t.newState == HoldState.Completed)
                {
                    note.MarkHoldCompleted();
                    if (laneGlow != null) laneGlow.Off(note.Lane);
                }
                else if (t.newState == HoldState.Broken)
                {
                    judgmentSystem.HandleHoldBreak(note);
                    note.MarkHoldBroken();
                    if (laneGlow != null) laneGlow.Off(note.Lane);
                }
                idToNote.Remove(t.id);
                holdAudio.Remove(t.id);
            }

            // Retrigger loop — after transitions so Completed/Broken entries are gone.
            // Two-phase to avoid InvalidOperationException from indexer-set during
            // Dictionary enumeration (see retriggerBuffer comment above).
            int songMs = audioSync.SongTimeMs;
            retriggerBuffer.Clear();
            foreach (var kv in holdAudio)
            {
                if (songMs - kv.Value.lastRetriggerMs < retriggerIntervalMs) continue;
                retriggerBuffer.Add(kv.Key);
                audioPool.PlayForPitch(kv.Value.pitch, HOLD_RETRIGGER_VOLUME);
            }
            for (int i = 0; i < retriggerBuffer.Count; i++)
            {
                int id = retriggerBuffer[i];
                var st = holdAudio[id];
                st.lastRetriggerMs = songMs;
                holdAudio[id] = st;
            }
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal int HoldAudioCountForTest => holdAudio.Count;

        internal void SetDependenciesForTest(
            AudioSyncManager audioSync,
            AudioSamplePool audioPool,
            LaneGlowController laneGlow = null,
            TapInputHandler tapInput = null,
            JudgmentSystem judgmentSystem = null)
        {
            this.audioSync = audioSync;
            this.audioPool = audioPool;
            this.laneGlow = laneGlow;
            this.tapInput = tapInput;
            this.judgmentSystem = judgmentSystem;
        }

        internal void TickForTest() => Update();
#endif
    }
}
