using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.Feedback;

namespace KeyFlow.Tests.EditMode
{
    public class FeedbackPresetsTests
    {
        private static FeedbackPresets MakePresets()
        {
            var presets = ScriptableObject.CreateInstance<FeedbackPresets>();
            // Assign 4 distinct colors so the returned value is identifiable.
            presets.perfectTextColor = new Color(1f, 0.84f, 0f, 1f);   // gold
            presets.greatTextColor   = new Color(0.31f, 0.76f, 1f, 1f); // cyan
            presets.goodTextColor    = new Color(0.5f, 0.87f, 0.5f, 1f); // green
            presets.missTextColor    = new Color(1f, 0.25f, 0.25f, 1f); // red
            return presets;
        }

        [Test]
        public void GetTextColor_ReturnsPerfect_ForPerfect()
        {
            var p = MakePresets();
            Assert.AreEqual(new Color(1f, 0.84f, 0f, 1f), p.GetTextColor(Judgment.Perfect));
            Object.DestroyImmediate(p);
        }

        [Test]
        public void GetTextColor_ReturnsGreat_ForGreat()
        {
            var p = MakePresets();
            Assert.AreEqual(new Color(0.31f, 0.76f, 1f, 1f), p.GetTextColor(Judgment.Great));
            Object.DestroyImmediate(p);
        }

        [Test]
        public void GetTextColor_ReturnsGood_ForGood()
        {
            var p = MakePresets();
            Assert.AreEqual(new Color(0.5f, 0.87f, 0.5f, 1f), p.GetTextColor(Judgment.Good));
            Object.DestroyImmediate(p);
        }

        [Test]
        public void GetTextColor_ReturnsMiss_ForMiss()
        {
            var p = MakePresets();
            Assert.AreEqual(new Color(1f, 0.25f, 0.25f, 1f), p.GetTextColor(Judgment.Miss));
            Object.DestroyImmediate(p);
        }
    }
}
