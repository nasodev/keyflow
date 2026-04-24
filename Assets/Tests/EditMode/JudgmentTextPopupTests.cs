using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow;
using KeyFlow.Feedback;

namespace KeyFlow.Tests.EditMode
{
    public class JudgmentTextPopupTests
    {
        private static (GameObject go, Text text, JudgmentTextPopup popup) MakePopup()
        {
            var go = new GameObject("popup");
            go.AddComponent<RectTransform>();
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var popup = go.AddComponent<JudgmentTextPopup>();
            go.SetActive(false);
            return (go, text, popup);
        }

        [Test]
        public void Activate_SetsGameObjectActive()
        {
            var (go, text, popup) = MakePopup();
            popup.Activate(startTime: 0f, lifetime: 0.45f, yRiseUnits: 0.36f, color: Color.red);
            Assert.IsTrue(go.activeSelf);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TickForTest_BeforeLifetimeEnd_StaysActive()
        {
            var (go, text, popup) = MakePopup();
            popup.Activate(startTime: 0f, lifetime: 0.45f, yRiseUnits: 0.36f, color: Color.red);
            popup.TickForTest(simulatedTime: 0.3f);
            Assert.IsTrue(go.activeSelf);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TickForTest_AfterLifetime_DeactivatesGameObject()
        {
            var (go, text, popup) = MakePopup();
            popup.Activate(startTime: 0f, lifetime: 0.45f, yRiseUnits: 0.36f, color: Color.red);
            popup.TickForTest(simulatedTime: 0.5f);
            Assert.IsFalse(go.activeSelf);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TickForTest_InPunchPhase_ScaleGreaterThanOne()
        {
            var (go, text, popup) = MakePopup();
            popup.Activate(startTime: 0f, lifetime: 0.45f, yRiseUnits: 0.36f, color: Color.red);
            popup.TickForTest(simulatedTime: 0.03f); // ~6.6% into lifetime, well inside the 0-22% punch window
            Assert.Greater(go.transform.localScale.x, 1.0f);
            Assert.Less(go.transform.localScale.x, 1.3001f);
            Object.DestroyImmediate(go);
        }
    }
}
