namespace KeyFlow
{
    public static class SongSession
    {
        public static string CurrentSongId;
        public static Difficulty CurrentDifficulty;
        public static ScoreManager LastScore;

        public static void Reset()
        {
            CurrentSongId = null;
            CurrentDifficulty = Difficulty.Easy;
            LastScore = null;
        }
    }
}
