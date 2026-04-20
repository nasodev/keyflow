namespace KeyFlow
{
    public static class JudgmentEvaluator
    {
        // Per spec §5.1
        private const int NormalPerfectMs = 60;
        private const int NormalGreatMs = 120;
        private const int NormalGoodMs = 180;
        private const int EasyPerfectMs = 75;
        private const int EasyGreatMs = 150;
        private const int EasyGoodMs = 225;

        public static JudgmentResult Evaluate(int deltaMs, Difficulty difficulty)
        {
            int abs = deltaMs < 0 ? -deltaMs : deltaMs;
            int perfect, great, good;
            if (difficulty == Difficulty.Easy)
            {
                perfect = EasyPerfectMs; great = EasyGreatMs; good = EasyGoodMs;
            }
            else
            {
                perfect = NormalPerfectMs; great = NormalGreatMs; good = NormalGoodMs;
            }

            if (abs <= perfect)     return new JudgmentResult(Judgment.Perfect, deltaMs);
            if (abs <= great)       return new JudgmentResult(Judgment.Great, deltaMs);
            if (abs <= good)        return new JudgmentResult(Judgment.Good, deltaMs);
            return new JudgmentResult(Judgment.Miss, deltaMs);
        }

        public static int GetGoodWindowMs(Difficulty difficulty)
            => difficulty == Difficulty.Easy ? EasyGoodMs : NormalGoodMs;
    }
}
