using NUnit.Framework;
using UnityEngine;
using KeyFlow.UI;

namespace KeyFlow.Tests.EditMode
{
    public class OverlayBaseTests
    {
        private class SpyOverlay : OverlayBase
        {
            public int ShownCalls;
            public int FinishingCalls;
            protected override void OnShown() => ShownCalls++;
            protected override void OnFinishing() => FinishingCalls++;
        }

        // Note: OverlayBase.OnEnable self-deactivates on first enable, but
        // Unity's EditMode AddComponent flow does not propagate activeSelf
        // changes synchronously, so we verify the IsVisible state variable
        // here. The production contract is SceneBuilder saves the GO
        // deactivated in the scene asset; Awake/OnEnable is a safety net.
        [Test]
        public void InitialState_IsNotVisible()
        {
            var go = new GameObject("spy");
            var overlay = go.AddComponent<SpyOverlay>();
            Assert.IsFalse(overlay.IsVisible);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShowAndFinish_InvokeHooksInOrder()
        {
            var go = new GameObject("spy");
            var overlay = go.AddComponent<SpyOverlay>();

            overlay.Show();
            Assert.IsTrue(go.activeSelf);
            Assert.IsTrue(overlay.IsVisible);
            Assert.AreEqual(1, overlay.ShownCalls);
            Assert.AreEqual(0, overlay.FinishingCalls);

            overlay.Finish();
            Assert.IsFalse(go.activeSelf);
            Assert.IsFalse(overlay.IsVisible);
            Assert.AreEqual(1, overlay.FinishingCalls);

            Object.DestroyImmediate(go);
        }
    }
}
