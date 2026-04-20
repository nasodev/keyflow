using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class CalibrationCalculatorTests
    {
        private static double[] Expected(double first, int count, double interval)
        {
            var arr = new double[count];
            for (int i = 0; i < count; i++) arr[i] = first + i * interval;
            return arr;
        }

        [Test]
        public void Compute_PerfectTaps_ZeroOffset()
        {
            var exp = Expected(2.0, 8, 0.5);
            var r = CalibrationCalculator.Compute(exp, exp);
            Assert.AreEqual(0, r.offsetMs);
            Assert.IsTrue(r.reliable);
        }

        [Test]
        public void Compute_ConstantLateDelay_ReturnsDelayAsOffset()
        {
            var exp = Expected(2.0, 8, 0.5);
            var taps = new double[8];
            for (int i = 0; i < 8; i++) taps[i] = exp[i] + 0.100;
            var r = CalibrationCalculator.Compute(exp, taps);
            Assert.AreEqual(100, r.offsetMs);
            Assert.IsTrue(r.reliable);
        }

        [Test]
        public void Compute_ConstantEarlyTaps_NegativeOffset()
        {
            var exp = Expected(2.0, 8, 0.5);
            var taps = new double[8];
            for (int i = 0; i < 8; i++) taps[i] = exp[i] - 0.080;
            var r = CalibrationCalculator.Compute(exp, taps);
            Assert.AreEqual(-80, r.offsetMs);
            Assert.IsTrue(r.reliable);
        }

        [Test]
        public void Compute_OneOutlier_StillReliable()
        {
            var exp = Expected(2.0, 8, 0.5);
            var taps = new double[8];
            for (int i = 0; i < 8; i++) taps[i] = exp[i] + 0.050;
            taps[3] += 0.400; // one wild outlier
            var r = CalibrationCalculator.Compute(exp, taps);
            Assert.AreEqual(50, r.offsetMs);
            Assert.IsTrue(r.reliable);
        }

        [Test]
        public void Compute_HighVariance_NotReliable()
        {
            var exp = Expected(2.0, 8, 0.5);
            var taps = new double[8];
            double[] jitter = { 0.010, 0.200, -0.150, 0.180, -0.100, 0.220, -0.170, 0.190 };
            for (int i = 0; i < 8; i++) taps[i] = exp[i] + jitter[i];
            var r = CalibrationCalculator.Compute(exp, taps);
            Assert.IsFalse(r.reliable);
        }

        [Test]
        public void Compute_MissingTaps_StillComputes()
        {
            var exp = Expected(2.0, 8, 0.5);
            var taps = new double[6];
            for (int i = 0; i < 6; i++) taps[i] = exp[i] + 0.080;
            var r = CalibrationCalculator.Compute(exp, taps);
            Assert.AreEqual(80, r.offsetMs);
        }

        [Test]
        public void Compute_OffsetClampedToRange()
        {
            var exp = Expected(2.0, 8, 0.5);
            var taps = new double[8];
            for (int i = 0; i < 8; i++) taps[i] = exp[i] + 2.000;
            var r = CalibrationCalculator.Compute(exp, taps);
            Assert.AreEqual(500, r.offsetMs);
        }
    }
}
