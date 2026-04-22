using NUnit.Framework;
using UnityEngine;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class UserPrefsTests
    {
        [SetUp] public void Setup() { PlayerPrefs.DeleteAll(); }
        [TearDown] public void Teardown() { PlayerPrefs.DeleteAll(); }

        [Test] public void Defaults_WhenNeverSet_ReturnSpecValues()
        {
            Assert.AreEqual(0.8f, UserPrefs.SfxVolume, 1e-4);
            Assert.AreEqual(2.0f, UserPrefs.NoteSpeed, 1e-4);
            Assert.AreEqual(0, UserPrefs.CalibrationOffsetMs);
        }

        [Test] public void SfxVolume_And_NoteSpeed_RoundTrip()
        {
            UserPrefs.SfxVolume = 0.3f;
            UserPrefs.NoteSpeed = 2.5f;
            Assert.AreEqual(0.3f, UserPrefs.SfxVolume, 1e-4);
            Assert.AreEqual(2.5f, UserPrefs.NoteSpeed, 1e-4);
        }

        [Test] public void BestRecord_RoundTrip_PerSongPerDifficulty()
        {
            UserPrefs.TrySetBest("song_a", Difficulty.Easy, 2, 600_000);
            UserPrefs.TrySetBest("song_a", Difficulty.Normal, 1, 400_000);
            UserPrefs.TrySetBest("song_b", Difficulty.Easy, 3, 900_000);

            Assert.AreEqual(2, UserPrefs.GetBestStars("song_a", Difficulty.Easy));
            Assert.AreEqual(600_000, UserPrefs.GetBestScore("song_a", Difficulty.Easy));
            Assert.AreEqual(1, UserPrefs.GetBestStars("song_a", Difficulty.Normal));
            Assert.AreEqual(3, UserPrefs.GetBestStars("song_b", Difficulty.Easy));
        }

        [Test] public void TrySetBest_OnlyUpdatesWhenScoreHigher()
        {
            bool first = UserPrefs.TrySetBest("s", Difficulty.Easy, 1, 500_000);
            bool second = UserPrefs.TrySetBest("s", Difficulty.Easy, 2, 400_000);
            bool third = UserPrefs.TrySetBest("s", Difficulty.Easy, 3, 900_000);

            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.IsTrue(third);
            Assert.AreEqual(3, UserPrefs.GetBestStars("s", Difficulty.Easy));
            Assert.AreEqual(900_000, UserPrefs.GetBestScore("s", Difficulty.Easy));
        }

        [Test] public void MigrateLegacy_CopiesOldCalibOffsetMsKey()
        {
            PlayerPrefs.SetInt("CalibOffsetMs", 123);
            PlayerPrefs.Save();

            UserPrefs.MigrateLegacy();

            Assert.AreEqual(123, UserPrefs.CalibrationOffsetMs);
            Assert.IsFalse(PlayerPrefs.HasKey("CalibOffsetMs"), "legacy key should be removed");
            Assert.IsTrue(PlayerPrefs.HasKey("KeyFlow.Migration.V1.Done"));
        }

        [Test] public void MigrateLegacy_IsIdempotent()
        {
            PlayerPrefs.SetInt("CalibOffsetMs", 50);
            UserPrefs.MigrateLegacy();

            PlayerPrefs.SetInt("CalibOffsetMs", 999);
            UserPrefs.MigrateLegacy();

            Assert.AreEqual(50, UserPrefs.CalibrationOffsetMs);
        }

        [Test] public void HasCalibration_TracksKeyPresence()
        {
            Assert.IsFalse(UserPrefs.HasCalibration);
            UserPrefs.CalibrationOffsetMs = 0;
            Assert.IsTrue(UserPrefs.HasCalibration);
        }

        [Test] public void HapticsEnabled_DefaultsToTrue_WhenNeverSet()
        {
            Assert.IsTrue(UserPrefs.HapticsEnabled);
        }

        [Test] public void HapticsEnabled_RoundTripsFalse()
        {
            UserPrefs.HapticsEnabled = false;
            Assert.IsFalse(UserPrefs.HapticsEnabled);
        }

        [Test] public void HapticsEnabled_RoundTripsTrueAfterFalse()
        {
            UserPrefs.HapticsEnabled = false;
            UserPrefs.HapticsEnabled = true;
            Assert.IsTrue(UserPrefs.HapticsEnabled);
        }
    }
}
