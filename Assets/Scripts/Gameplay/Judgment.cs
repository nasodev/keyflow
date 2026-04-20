namespace KeyFlow
{
    public enum Judgment
    {
        Perfect = 0,
        Great = 1,
        Good = 2,
        Miss = 3
    }

    public enum Difficulty
    {
        Easy = 0,
        Normal = 1
    }

    public readonly struct JudgmentResult
    {
        public readonly Judgment Judgment;
        public readonly int DeltaMs;

        public JudgmentResult(Judgment j, int deltaMs)
        {
            Judgment = j;
            DeltaMs = deltaMs;
        }

        public bool IsHit => Judgment != Judgment.Miss;
    }
}
