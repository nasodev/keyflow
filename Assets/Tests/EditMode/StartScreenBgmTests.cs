using NUnit.Framework;
using UnityEngine;
using KeyFlow.UI;

namespace KeyFlow.Tests.EditMode
{
    public class StartScreenBgmTests
    {
        [Test]
        public void OnEnable_WithNullBgmSource_DoesNotThrow()
        {
            var go = new GameObject("StartScreenTest");
            var s = go.AddComponent<StartScreen>();
            // bgmSource intentionally unassigned.

            Assert.DoesNotThrow(() => s.InvokeOnEnableForTest());

            Object.DestroyImmediate(go);
        }

        [Test]
        public void OnDisable_WithNullBgmSource_DoesNotThrow()
        {
            var go = new GameObject("StartScreenTest");
            var s = go.AddComponent<StartScreen>();
            // bgmSource intentionally unassigned.

            Assert.DoesNotThrow(() => s.InvokeOnDisableForTest());

            Object.DestroyImmediate(go);
        }
    }
}
