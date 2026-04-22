using NUnit.Framework;
using KeyFlow.Editor;

namespace KeyFlow.Tests.EditMode
{
    public class CalibrationClickBuilderTests
    {
        [Test]
        public void GenerateWavBytes_IsDeterministic()
        {
            byte[] first  = CalibrationClickBuilder.GenerateWavBytes();
            byte[] second = CalibrationClickBuilder.GenerateWavBytes();
            Assert.AreEqual(first.Length, second.Length, "Lengths differ across runs.");
            CollectionAssert.AreEqual(first, second, "Bytes differ across runs.");
        }
    }
}
