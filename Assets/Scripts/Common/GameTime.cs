namespace KeyFlow
{
    public static class GameTime
    {
        public const int MinPitch = 36;
        public const int MaxPitch = 83;
        private const int PitchRange = MaxPitch - MinPitch;

        public static int GetSongTimeMs(double nowDsp, double songStartDsp, double calibOffsetSec)
        {
            double sec = nowDsp - songStartDsp - calibOffsetSec;
            return (int)(sec * 1000.0);
        }

        public static float GetNoteProgress(int songTimeMs, int hitTimeMs, int previewTimeMs)
        {
            return 1f - (float)(hitTimeMs - songTimeMs) / previewTimeMs;
        }

        public static float PitchToX(int pitch)
        {
            return (pitch - MinPitch) / (float)PitchRange;
        }
    }
}
