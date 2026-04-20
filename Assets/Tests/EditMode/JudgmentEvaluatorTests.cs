using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests
{
    public class JudgmentEvaluatorTests
    {
        // Normal: ±60 Perfect, ±120 Great, ±180 Good, beyond = Miss

        [Test]
        public void Normal_OnTime_Perfect()
        {
            var r = JudgmentEvaluator.Evaluate(0, Difficulty.Normal);
            Assert.AreEqual(Judgment.Perfect, r.Judgment);
            Assert.AreEqual(0, r.DeltaMs);
        }

        [Test]
        public void Normal_PerfectBoundary_AtPlus60_Perfect()
        {
            var r = JudgmentEvaluator.Evaluate(60, Difficulty.Normal);
            Assert.AreEqual(Judgment.Perfect, r.Judgment);
        }

        [Test]
        public void Normal_JustPastPerfect_Great()
        {
            var r = JudgmentEvaluator.Evaluate(61, Difficulty.Normal);
            Assert.AreEqual(Judgment.Great, r.Judgment);
        }

        [Test]
        public void Normal_GreatBoundary_AtPlus120_Great()
        {
            var r = JudgmentEvaluator.Evaluate(120, Difficulty.Normal);
            Assert.AreEqual(Judgment.Great, r.Judgment);
        }

        [Test]
        public void Normal_JustPastGreat_Good()
        {
            var r = JudgmentEvaluator.Evaluate(121, Difficulty.Normal);
            Assert.AreEqual(Judgment.Good, r.Judgment);
        }

        [Test]
        public void Normal_GoodBoundary_AtPlus180_Good()
        {
            var r = JudgmentEvaluator.Evaluate(180, Difficulty.Normal);
            Assert.AreEqual(Judgment.Good, r.Judgment);
        }

        [Test]
        public void Normal_JustPastGood_Miss()
        {
            var r = JudgmentEvaluator.Evaluate(181, Difficulty.Normal);
            Assert.AreEqual(Judgment.Miss, r.Judgment);
        }

        [Test]
        public void Normal_Early_SymmetricWindow()
        {
            Assert.AreEqual(Judgment.Perfect, JudgmentEvaluator.Evaluate(-60, Difficulty.Normal).Judgment);
            Assert.AreEqual(Judgment.Great,   JudgmentEvaluator.Evaluate(-61, Difficulty.Normal).Judgment);
            Assert.AreEqual(Judgment.Good,    JudgmentEvaluator.Evaluate(-121, Difficulty.Normal).Judgment);
            Assert.AreEqual(Judgment.Miss,    JudgmentEvaluator.Evaluate(-181, Difficulty.Normal).Judgment);
        }

        // Easy: ±75 Perfect, ±150 Great, ±225 Good, beyond = Miss

        [Test]
        public void Easy_PerfectBoundary_AtPlus75_Perfect()
        {
            Assert.AreEqual(Judgment.Perfect, JudgmentEvaluator.Evaluate(75, Difficulty.Easy).Judgment);
        }

        [Test]
        public void Easy_GreatBoundary_AtPlus150_Great()
        {
            Assert.AreEqual(Judgment.Great, JudgmentEvaluator.Evaluate(150, Difficulty.Easy).Judgment);
        }

        [Test]
        public void Easy_GoodBoundary_AtPlus225_Good()
        {
            Assert.AreEqual(Judgment.Good, JudgmentEvaluator.Evaluate(225, Difficulty.Easy).Judgment);
        }

        [Test]
        public void Easy_JustPastGood_Miss()
        {
            Assert.AreEqual(Judgment.Miss, JudgmentEvaluator.Evaluate(226, Difficulty.Easy).Judgment);
        }

        [Test]
        public void DeltaMs_IsEchoed()
        {
            var r = JudgmentEvaluator.Evaluate(42, Difficulty.Normal);
            Assert.AreEqual(42, r.DeltaMs);
        }
    }
}
