using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow.UI;

namespace KeyFlow.Tests.EditMode
{
    public class CountdownNumberPopupTests
    {
        private static (GameObject go, Text text, CountdownNumberPopup popup) MakePopup()
        {
            var go = new GameObject("countdown_popup");
            go.AddComponent<RectTransform>();
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Outline is required by CountdownNumberPopup's inspector reference but
            // harmless to add eagerly in tests.
            go.AddComponent<Outline>();
            var popup = go.AddComponent<CountdownNumberPopup>();
            go.SetActive(false);
            return (go, text, popup);
        }

        [Test]
        public void Activate_SetsLabelAndColor()
        {
            var (go, text, popup) = MakePopup();
            popup.Activate(startTime: 0f, lifetime: 1.0f, label: "3", color: Color.white);
            Assert.AreEqual("3", text.text);
            Assert.AreEqual(Color.white, text.color);
            Assert.IsTrue(go.activeSelf);
            Object.DestroyImmediate(go);
        }
        [Test]
        public void TickForTest_PunchPhase_ScaleBetween1And1_5()
        {
            var (go, _, popup) = MakePopup();
            popup.Activate(startTime: 0f, lifetime: 1.0f, label: "3", color: Color.white);
            // At t=0.09 (50% into the 0-0.18 punch window), scale lerps halfway between 1.5 and 1.0 → 1.25.
            popup.TickForTest(simulatedTime: 0.09f);
            Assert.Greater(go.transform.localScale.x, 1.0f);
            Assert.Less(go.transform.localScale.x, 1.5001f);
            Object.DestroyImmediate(go);
        }
        [Test]
        public void TickForTest_AfterFadeStart_AlphaLessThan1()
        {
            var (go, text, popup) = MakePopup();
            popup.Activate(startTime: 0f, lifetime: 1.0f, label: "3", color: Color.white);
            // Fade starts at t=0.55. At t=0.9 (80% through fade window), alpha ≈ 0.22.
            popup.TickForTest(simulatedTime: 0.9f);
            Assert.Less(text.color.a, 1f);
            Assert.Greater(text.color.a, 0f);
            Object.DestroyImmediate(go);
        }
        [Test]
        public void TickForTest_AfterLifetime_DeactivatesGameObject()
        {
            var (go, _, popup) = MakePopup();
            popup.Activate(startTime: 0f, lifetime: 1.0f, label: "3", color: Color.white);
            popup.TickForTest(simulatedTime: 1.01f);
            Assert.IsFalse(go.activeSelf);
            Object.DestroyImmediate(go);
        }
    }
}
