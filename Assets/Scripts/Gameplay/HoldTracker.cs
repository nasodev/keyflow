using System.Collections.Generic;
using UnityEngine;

namespace KeyFlow
{
    public class HoldTracker : MonoBehaviour
    {
        [SerializeField] private TapInputHandler tapInput;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private JudgmentSystem judgmentSystem;

        private readonly HoldStateMachine stateMachine = new HoldStateMachine();
        private readonly Dictionary<int, NoteController> idToNote = new Dictionary<int, NoteController>();

        public void ResetForRetry()
        {
            stateMachine.Clear();
            idToNote.Clear();
        }

        public void OnHoldStartTapAccepted(NoteController note)
        {
            int endMs = note.HitTimeMs + note.DurMs;
            int id = stateMachine.Register(note.Lane, note.HitTimeMs, endMs);
            stateMachine.OnStartTapAccepted(id);
            idToNote[id] = note;
        }

        private void Update()
        {
            if (!audioSync.IsPlaying || audioSync.IsPaused) return;
            if (idToNote.Count == 0) return;

            var pressed = new HashSet<int>();
            for (int lane = 0; lane < LaneLayout.LaneCount; lane++)
                if (tapInput.IsLanePressed(lane)) pressed.Add(lane);

            var transitions = stateMachine.Tick(audioSync.SongTimeMs, pressed);
            foreach (var t in transitions)
            {
                if (!idToNote.TryGetValue(t.id, out var note)) continue;

                if (t.newState == HoldState.Completed)
                {
                    note.MarkHoldCompleted();
                }
                else if (t.newState == HoldState.Broken)
                {
                    judgmentSystem.HandleHoldBreak();
                    note.MarkHoldBroken();
                }
                idToNote.Remove(t.id);
            }
        }
    }
}
