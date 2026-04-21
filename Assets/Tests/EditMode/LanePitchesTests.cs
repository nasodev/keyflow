using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class LanePitchesTests
    {
        [Test]
        public void Default_Lane0_ReturnsC3()
        {
            Assert.AreEqual(48, LanePitches.Default(0));
        }

        [Test]
        public void Default_Lane1_ReturnsG3()
        {
            Assert.AreEqual(55, LanePitches.Default(1));
        }

        [Test]
        public void Default_Lane2_ReturnsC4()
        {
            Assert.AreEqual(60, LanePitches.Default(2));
        }

        [Test]
        public void Default_Lane3_ReturnsG4()
        {
            Assert.AreEqual(67, LanePitches.Default(3));
        }

        [Test]
        public void Default_NegativeLane_ReturnsMiddleC()
        {
            Assert.AreEqual(60, LanePitches.Default(-1));
        }

        [Test]
        public void Default_OutOfRangeLane_ReturnsMiddleC()
        {
            Assert.AreEqual(60, LanePitches.Default(99));
        }
    }
}
