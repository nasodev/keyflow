using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class LaneLayoutTests
    {
        private const float Width = 4f;

        [Test]
        public void LaneToX_Lane0_ReturnsLeftQuarter()
        {
            Assert.AreEqual(-1.5f, LaneLayout.LaneToX(0, Width), 0.001f);
        }

        [Test]
        public void LaneToX_Lane3_ReturnsRightQuarter()
        {
            Assert.AreEqual(1.5f, LaneLayout.LaneToX(3, Width), 0.001f);
        }

        [Test]
        public void LaneToX_AllLanesEquallySpaced()
        {
            float x0 = LaneLayout.LaneToX(0, Width);
            float x1 = LaneLayout.LaneToX(1, Width);
            float x2 = LaneLayout.LaneToX(2, Width);
            float x3 = LaneLayout.LaneToX(3, Width);
            Assert.AreEqual(1f, x1 - x0, 0.001f);
            Assert.AreEqual(1f, x2 - x1, 0.001f);
            Assert.AreEqual(1f, x3 - x2, 0.001f);
        }

        [Test]
        public void XToLane_NearCenter_ReturnsCorrectLane()
        {
            Assert.AreEqual(0, LaneLayout.XToLane(-1.5f, Width));
            Assert.AreEqual(1, LaneLayout.XToLane(-0.5f, Width));
            Assert.AreEqual(2, LaneLayout.XToLane(0.5f, Width));
            Assert.AreEqual(3, LaneLayout.XToLane(1.5f, Width));
        }

        [Test]
        public void XToLane_LeftOfScreen_ClampsTo0()
        {
            Assert.AreEqual(0, LaneLayout.XToLane(-999f, Width));
        }

        [Test]
        public void XToLane_RightOfScreen_ClampsTo3()
        {
            Assert.AreEqual(3, LaneLayout.XToLane(999f, Width));
        }

        [Test]
        public void XToLane_OnBoundary_UsesFloor()
        {
            Assert.AreEqual(2, LaneLayout.XToLane(0f, Width));
        }

        [Test]
        public void LaneCount_IsFour()
        {
            Assert.AreEqual(4, LaneLayout.LaneCount);
        }
    }
}
