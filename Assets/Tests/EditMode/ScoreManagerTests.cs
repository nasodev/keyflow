using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests
{
    public class ScoreManagerTests
    {
        [Test]
        public void AllPerfect_YieldsOneMillion()
        {
            var mgr = new ScoreManager(totalNotes: 100);
            for (int i = 0; i < 100; i++) mgr.RegisterJudgment(Judgment.Perfect);
            Assert.AreEqual(1_000_000, mgr.Score);
            Assert.AreEqual(100, mgr.Combo);
        }

        [Test]
        public void AllMiss_YieldsZero()
        {
            var mgr = new ScoreManager(totalNotes: 100);
            for (int i = 0; i < 100; i++) mgr.RegisterJudgment(Judgment.Miss);
            Assert.AreEqual(0, mgr.Score);
            Assert.AreEqual(0, mgr.Combo);
        }

        [Test]
        public void MissResetsCombo()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Miss);
            Assert.AreEqual(0, mgr.Combo);
        }

        [Test]
        public void GoodAndGreat_PartialCredit()
        {
            var mgr = new ScoreManager(totalNotes: 100);
            for (int i = 0; i < 100; i++) mgr.RegisterJudgment(Judgment.Good);
            Assert.AreEqual(370_000, mgr.Score);
        }

        [Test]
        public void MaxComboTracks()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Miss);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Perfect);
            Assert.AreEqual(3, mgr.MaxCombo);
            Assert.AreEqual(2, mgr.Combo);
        }

        [Test]
        public void JudgmentCounts_Tracked()
        {
            var mgr = new ScoreManager(totalNotes: 4);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Great);
            mgr.RegisterJudgment(Judgment.Good);
            mgr.RegisterJudgment(Judgment.Miss);
            Assert.AreEqual(1, mgr.PerfectCount);
            Assert.AreEqual(1, mgr.GreatCount);
            Assert.AreEqual(1, mgr.GoodCount);
            Assert.AreEqual(1, mgr.MissCount);
        }

        [Test]
        public void Stars_ZeroIfUnder500k()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            for (int i = 0; i < 4; i++) mgr.RegisterJudgment(Judgment.Perfect);
            Assert.AreEqual(0, mgr.Stars);
        }

        [Test]
        public void Stars_OneAt500k()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            for (int i = 0; i < 5; i++) mgr.RegisterJudgment(Judgment.Perfect);
            Assert.AreEqual(1, mgr.Stars);
        }

        [Test]
        public void Stars_TwoAt750k()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            for (int i = 0; i < 8; i++) mgr.RegisterJudgment(Judgment.Perfect);
            Assert.AreEqual(2, mgr.Stars);
        }

        [Test]
        public void Stars_ThreeAt900k()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            for (int i = 0; i < 9; i++) mgr.RegisterJudgment(Judgment.Perfect);
            Assert.AreEqual(3, mgr.Stars);
        }

        [Test]
        public void Accuracy_PercentageOfHits()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            for (int i = 0; i < 7; i++) mgr.RegisterJudgment(Judgment.Perfect);
            for (int i = 0; i < 3; i++) mgr.RegisterJudgment(Judgment.Miss);
            Assert.AreEqual(70f, mgr.AccuracyPercent, 0.01f);
        }
    }
}
