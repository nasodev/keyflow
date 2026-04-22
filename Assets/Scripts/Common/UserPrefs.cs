using UnityEngine;

namespace KeyFlow
{
    public static class UserPrefs
    {
        private const string K_SfxVolume   = "KeyFlow.Settings.SfxVolume";
        private const string K_NoteSpeed   = "KeyFlow.Settings.NoteSpeed";
        private const string K_CalibOffset = "KeyFlow.Settings.CalibrationOffsetMs";
        private const string K_MigrationV1 = "KeyFlow.Migration.V1.Done";
        private const string Legacy_CalibOffset = "CalibOffsetMs";
        private const string K_HapticsEnabled = "KeyFlow.Settings.HapticsEnabled";

        private const float DefaultSfxVolume = 0.8f;
        private const float DefaultNoteSpeed = 2.0f;

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(K_SfxVolume, DefaultSfxVolume);
            set { PlayerPrefs.SetFloat(K_SfxVolume, value); PlayerPrefs.Save(); }
        }

        public static float NoteSpeed
        {
            get => PlayerPrefs.GetFloat(K_NoteSpeed, DefaultNoteSpeed);
            set { PlayerPrefs.SetFloat(K_NoteSpeed, value); PlayerPrefs.Save(); }
        }

        public static int CalibrationOffsetMs
        {
            get => PlayerPrefs.GetInt(K_CalibOffset, 0);
            set { PlayerPrefs.SetInt(K_CalibOffset, value); PlayerPrefs.Save(); }
        }

        public static bool HapticsEnabled
        {
            get => PlayerPrefs.GetInt(K_HapticsEnabled, 1) == 1;
            set { PlayerPrefs.SetInt(K_HapticsEnabled, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool HasCalibration => PlayerPrefs.HasKey(K_CalibOffset);

        public static int GetBestStars(string songId, Difficulty d) =>
            PlayerPrefs.GetInt(RecordStarsKey(songId, d), 0);

        public static int GetBestScore(string songId, Difficulty d) =>
            PlayerPrefs.GetInt(RecordScoreKey(songId, d), 0);

        public static bool TrySetBest(string songId, Difficulty d, int stars, int score)
        {
            int prevScore = GetBestScore(songId, d);
            if (score <= prevScore) return false;
            PlayerPrefs.SetInt(RecordStarsKey(songId, d), stars);
            PlayerPrefs.SetInt(RecordScoreKey(songId, d), score);
            PlayerPrefs.Save();
            return true;
        }

        public static void MigrateLegacy()
        {
            if (PlayerPrefs.GetInt(K_MigrationV1, 0) == 1) return;
            if (PlayerPrefs.HasKey(Legacy_CalibOffset))
            {
                int legacy = PlayerPrefs.GetInt(Legacy_CalibOffset, 0);
                PlayerPrefs.SetInt(K_CalibOffset, legacy);
                PlayerPrefs.DeleteKey(Legacy_CalibOffset);
            }
            PlayerPrefs.SetInt(K_MigrationV1, 1);
            PlayerPrefs.Save();
        }

        private static string RecordStarsKey(string id, Difficulty d) =>
            $"KeyFlow.Record.{id}.{d}.Stars";
        private static string RecordScoreKey(string id, Difficulty d) =>
            $"KeyFlow.Record.{id}.{d}.Score";
    }
}
