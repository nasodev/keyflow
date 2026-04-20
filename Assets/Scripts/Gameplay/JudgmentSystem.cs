using System.Collections.Generic;
using UnityEngine;

namespace KeyFlow
{
    public class JudgmentSystem : MonoBehaviour
    {
        [SerializeField] private TapInputHandler tapInput;

        private readonly List<NoteController> pending = new List<NoteController>();
        private ScoreManager score;
        private Difficulty difficulty;

        public ScoreManager Score => score;
        public Judgment LastJudgment { get; private set; }
        public int LastDeltaMs { get; private set; }

        public void Initialize(int totalNotes, Difficulty difficulty)
        {
            this.difficulty = difficulty;
            score = new ScoreManager(totalNotes);
            LastJudgment = Judgment.Miss;
        }

        private void OnEnable()
        {
            if (tapInput != null) tapInput.OnLaneTap += HandleTap;
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
            if (result.Judgment == Judgment.Miss) return;

            score.RegisterJudgment(result.Judgment);
            LastJudgment = result.Judgment;
            LastDeltaMs = result.DeltaMs;
            pending.Remove(closest);
            closest.MarkJudged();
        }
    }
}
