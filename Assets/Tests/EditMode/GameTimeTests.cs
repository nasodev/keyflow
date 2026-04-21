using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class GameTimeTests
    {
        [Test]
        public void GetSongTimeMs_AtStartDspTime_ReturnsZero()
        {
            int result = GameTime.GetSongTimeMs(nowDsp: 100.5, songStartDsp: 100.5, calibOffsetSec: 0.0);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void GetSongTimeMs_OneSecondAfterStart_Returns1000()
        {
            int result = GameTime.GetSongTimeMs(nowDsp: 101.5, songStartDsp: 100.5, calibOffsetSec: 0.0);
            Assert.AreEqual(1000, result);
        }

        [Test]
        public void GetSongTimeMs_AppliesCalibrationOffset()
        {
            int result = GameTime.GetSongTimeMs(nowDsp: 101.5, songStartDsp: 100.5, calibOffsetSec: 0.05);
            Assert.AreEqual(950, result);
        }

        [Test]
        public void GetNoteProgress_AtSpawnTime_ReturnsZero()
        {
            float progress = GameTime.GetNoteProgress(songTimeMs: 0, hitTimeMs: 2000, previewTimeMs: 2000);
            Assert.AreEqual(0f, progress, 0.001f);
        }

        [Test]
        public void GetNoteProgress_AtHitTime_ReturnsOne()
        {
            float progress = GameTime.GetNoteProgress(songTimeMs: 2000, hitTimeMs: 2000, previewTimeMs: 2000);
            Assert.AreEqual(1f, progress, 0.001f);
        }

        [Test]
        public void GetNoteProgress_HalfwayDown_ReturnsHalf()
        {
            float progress = GameTime.GetNoteProgress(songTimeMs: 1000, hitTimeMs: 2000, previewTimeMs: 2000);
            Assert.AreEqual(0.5f, progress, 0.001f);
        }

}
}
