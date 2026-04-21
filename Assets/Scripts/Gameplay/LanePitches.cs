namespace KeyFlow
{
    public static class LanePitches
    {
        // Perfect 5th staircase across 4 lanes: C3, G3, C4, G4.
        // Low-clash with MVP song-set keys (A-minor, D/C/Db-major).
        private static readonly int[] defaults = { 48, 55, 60, 67 };

        public static int Default(int lane)
        {
            if (lane < 0 || lane >= defaults.Length) return 60;
            return defaults[lane];
        }
    }
}
