using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow;
using KeyFlow.UI;

namespace KeyFlow.Tests.EditMode
{
    public class ComboHUDTests
    {
        private GameObject hostGo;
        private ComboHUD hud;
        private Text comboText;
        private JudgmentSystem judgmentSystem;

        [SetUp]
        public void SetUp()
        {
            hostGo = new GameObject("ComboHUDTestHost");
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(hostGo.transform, false);
            comboText = textGo.AddComponent<Text>();
            comboText.enabled = false;

            var jsGo = new GameObject("JS");
            judgmentSystem = jsGo.AddComponent<JudgmentSystem>();
            judgmentSystem.Initialize(totalNotes: 10, Difficulty.Easy);

            hud = hostGo.AddComponent<ComboHUD>();
            Inject(hud, "judgmentSystem", judgmentSystem);
            Inject(hud, "comboText", comboText);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(hostGo);
            Object.DestroyImmediate(judgmentSystem.gameObject);
        }

        private static void Inject(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                BindingFlags.NonPublic | BindingFlags.Instance);
            f.SetValue(target, value);
        }

        [Test]
        public void HidesWhenComboZero()
        {
            comboText.enabled = true; // seed opposite to starting state
            hud.UpdateForTest();
            Assert.IsFalse(comboText.enabled);
        }

        [Test]
        public void ShowsAndUpdatesTextWhenComboIncreases()
        {
            judgmentSystem.Score.RegisterJudgment(Judgment.Perfect);
            hud.UpdateForTest();
            Assert.IsTrue(comboText.enabled);
            Assert.AreEqual("1", comboText.text);

            judgmentSystem.Score.RegisterJudgment(Judgment.Perfect);
            hud.UpdateForTest();
            Assert.AreEqual("2", comboText.text);
        }

        [Test]
        public void HidesWhenComboResetsToZero()
        {
            judgmentSystem.Score.RegisterJudgment(Judgment.Perfect);
            judgmentSystem.Score.RegisterJudgment(Judgment.Perfect);
            judgmentSystem.Score.RegisterJudgment(Judgment.Perfect);
            hud.UpdateForTest();
            Assert.IsTrue(comboText.enabled);

            judgmentSystem.Score.RegisterJudgment(Judgment.Miss);
            hud.UpdateForTest();
            Assert.IsFalse(comboText.enabled);
        }

        [Test]
        public void DoesNotReassignTextWhenComboUnchanged()
        {
            judgmentSystem.Score.RegisterJudgment(Judgment.Perfect);
            judgmentSystem.Score.RegisterJudgment(Judgment.Perfect);
            hud.UpdateForTest();
            int baseline = hud.TextAssignmentCount;

            for (int i = 0; i < 5; i++) hud.UpdateForTest();

            Assert.AreEqual(baseline, hud.TextAssignmentCount);
        }
    }
}
