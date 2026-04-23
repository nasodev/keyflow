using System;
using System.Collections.Generic;
using UnityEngine;
using KeyFlow.Charts;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("KeyFlow.Tests.EditMode")]

namespace KeyFlow
{
    public class JudgmentSystem : MonoBehaviour
    {
        [SerializeField] private TapInputHandler tapInput;
        [SerializeField] private HoldTracker holdTracker;

        private readonly List<NoteController> pending = new List<NoteController>();
        private ScoreManager score;
        private Difficulty difficulty;

        public ScoreManager Score => score;
        public Judgment LastJudgment { get; private set; }
        public int LastDeltaMs { get; private set; }

        public event Action<Judgment, Vector3> OnJudgmentFeedback;

        public void Initialize(int totalNotes, Difficulty difficulty)
        {
            this.difficulty = difficulty;
            score = new ScoreManager(totalNotes);
            LastJudgment = Judgment.Miss;
        }

        public void ResetForRetry()
        {
            pending.Clear();
            score = null;
            LastJudgment = Judgment.Miss;
            LastDeltaMs = 0;
        }

        private void OnEnable()
        {
            if (tapInput != null) tapInput.OnLaneTap += HandleTap;
            if (holdTracker == null)
                Debug.LogError("JudgmentSystem: holdTracker SerializeField is unassigned. HOLD notes will silently behave as TAPs.");
        }

        private void OnDisable()
        {
            if (tapInput != null) tapInput.OnLaneTap -= HandleTap;
        }

        public void RegisterPendingNote(NoteController note)
        {
            pending.Add(note);
        }

        public void HandleAutoMiss(NoteController note)
        {
            if (score == null) return;
            pending.Remove(note);
            score.RegisterJudgment(Judgment.Miss);
            LastJudgment = Judgment.Miss;
            LastDeltaMs = 0;
            OnJudgmentFeedback?.Invoke(Judgment.Miss, note.transform.position);
        }

        public void HandleHoldBreak(NoteController brokenNote)
        {
            if (score == null) return;
            score.RegisterJudgment(Judgment.Miss);
            LastJudgment = Judgment.Miss;
            LastDeltaMs = 0;
            OnJudgmentFeedback?.Invoke(Judgment.Miss, brokenNote.transform.position);
        }

        private void HandleTap(int tapTimeMs, int tapLane)
        {
            if (score == null) return;

            NoteController closest = null;
            int closestAbsDelta = int.MaxValue;
            for (int i = 0; i < pending.Count; i++)
            {
                var n = pending[i];
                if (n == null || n.Judged) continue;
                if (n.Lane != tapLane) continue;
                int delta = tapTimeMs - n.HitTimeMs;
                int abs = delta < 0 ? -delta : delta;
                if (abs < closestAbsDelta)
                {
                    closestAbsDelta = abs;
                    closest = n;
                }
            }

            if (closest == null) return;

            int signedDelta = tapTimeMs - closest.HitTimeMs;
            var result = JudgmentEvaluator.Evaluate(signedDelta, difficulty);

            // Fire feedback for EVERY branch that reaches here, including Miss.
            // Miss still early-returns for score purposes (note stays in pending), but
            // the player's attempted tap should still produce feedback.
            OnJudgmentFeedback?.Invoke(result.Judgment, closest.transform.position);

            if (result.Judgment == Judgment.Miss) return;

            score.RegisterJudgment(result.Judgment);
            LastJudgment = result.Judgment;
            LastDeltaMs = result.DeltaMs;
            pending.Remove(closest);

            if (closest.Type == NoteType.HOLD && holdTracker != null)
            {
                closest.MarkAcceptedAsHold();
                holdTracker.OnHoldStartTapAccepted(closest, tapTimeMs);
            }
            else
            {
                closest.MarkJudged();
            }
        }

        public int GetClosestPendingPitch(int lane, int tapTimeMs, int windowMs)
        {
            NoteController closest = null;
            int closestAbsDelta = int.MaxValue;
            for (int i = 0; i < pending.Count; i++)
            {
                var n = pending[i];
                if (n == null || n.Judged) continue;
                if (n.Lane != lane) continue;
                int delta = tapTimeMs - n.HitTimeMs;
                int abs = delta < 0 ? -delta : delta;
                if (abs < closestAbsDelta)
                {
                    closestAbsDelta = abs;
                    closest = n;
                }
            }
            if (closest == null || closestAbsDelta > windowMs) return -1;
            return closest.Pitch;
        }

        internal void InvokeHandleTapForTest(int tapTimeMs, int tapLane)
            => HandleTap(tapTimeMs, tapLane);
    }
}
