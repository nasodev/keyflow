namespace KeyFlow
{
    public class ScoreManager
    {
        private readonly int totalNotes;
        private readonly int perNoteScore;
        private readonly int perNoteComboBonus;

        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int PerfectCount { get; private set; }
        public int GreatCount { get; private set; }
        public int GoodCount { get; private set; }
        public int MissCount { get; private set; }
        public int JudgedCount => PerfectCount + GreatCount + GoodCount + MissCount;

        public int Stars
        {
            get
            {
                if (Score >= 900_000) return 3;
                if (Score >= 750_000) return 2;
                if (Score >= 500_000) return 1;
                return 0;
            }
        }

        public float AccuracyPercent
        {
            get
            {
                int hits = PerfectCount + GreatCount + GoodCount;
                return JudgedCount == 0 ? 0f : 100f * hits / JudgedCount;
            }
        }

        public ScoreManager(int totalNotes)
        {
            this.totalNotes = totalNotes > 0 ? totalNotes : 1;
            perNoteScore = 900_000 / this.totalNotes;
            perNoteComboBonus = 100_000 / this.totalNotes;
        }

        public void RegisterJudgment(Judgment j)
        {
            switch (j)
            {
                case Judgment.Perfect: PerfectCount++; Score += perNoteScore; Combo++; Score += perNoteComboBonus; break;
                case Judgment.Great:   GreatCount++;   Score += (int)(perNoteScore * 0.7f); Combo++; Score += perNoteComboBonus; break;
                case Judgment.Good:    GoodCount++;    Score += (int)(perNoteScore * 0.3f); Combo++; Score += perNoteComboBonus; break;
                case Judgment.Miss:    MissCount++;    Combo = 0; break;
            }
            if (Combo > MaxCombo) MaxCombo = Combo;
        }
    }
}
