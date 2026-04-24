using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow.UI;

namespace KeyFlow.Tests.EditMode
{
    public class CountdownOverlayTests
    {
        private class FakeClickPlayer : IClickPlayer
        {
            public readonly System.Collections.Generic.List<float> pitchCalls = new();
            public void Play(float pitch) => pitchCalls.Add(pitch);
        }

        private static (GameObject overlayGo, CountdownOverlay overlay, CountdownNumberPopup popup, GameObject pauseBtn, FakeClickPlayer click)
            Build()
        {
            var popupGo = new GameObject("popup");
            popupGo.AddComponent<RectTransform>();
            var text = popupGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            popupGo.AddComponent<Outline>();
            var popup = popupGo.AddComponent<CountdownNumberPopup>();
            popupGo.SetActive(false);

            var pauseBtn = new GameObject("pauseButton");
            pauseBtn.SetActive(true);

            var overlayGo = new GameObject("countdown_overlay");
            var overlay = overlayGo.AddComponent<CountdownOverlay>();
            var click = new FakeClickPlayer();
            overlay.SetDependenciesForTest(click, popup, pauseBtn);

            return (overlayGo, overlay, popup, pauseBtn, click);
        }

        private static void Destroy(GameObject overlayGo, CountdownNumberPopup popup, GameObject pauseBtn)
        {
            UnityEngine.Object.DestroyImmediate(overlayGo);
            UnityEngine.Object.DestroyImmediate(popup.gameObject);
            UnityEngine.Object.DestroyImmediate(pauseBtn);
        }

        [Test]
        public void BeginCountdown_FiresFirstClickAtPitch1()
        {
            var (overlayGo, overlay, popup, pauseBtn, click) = Build();
            overlay.BeginCountdown(onComplete: () => { });
            Assert.AreEqual(1, click.pitchCalls.Count);
            Assert.AreEqual(1.0f, click.pitchCalls[0]);
            Destroy(overlayGo, popup, pauseBtn);
        }

        [Test]
        public void TickForTest_At1Second_ActivatesPopupWithLabel2()
        {
            var (overlayGo, overlay, popup, pauseBtn, click) = Build();
            var text = popup.GetComponent<Text>();
            overlay.BeginCountdown(onComplete: () => { });
            Assert.AreEqual("3", text.text);  // sanity — Step3 fired on BeginCountdown
            overlay.TickForTest(simulatedElapsed: 1.0f);
            Assert.AreEqual("2", text.text);
            Assert.AreEqual(2, click.pitchCalls.Count);  // Step3 + Step2
            Assert.AreEqual(1.0f, click.pitchCalls[1]);
            Destroy(overlayGo, popup, pauseBtn);
        }

        [Test]
        public void TickForTest_At3Seconds_FiresGoPitch()
        {
            var (overlayGo, overlay, popup, pauseBtn, click) = Build();
            var text = popup.GetComponent<Text>();
            overlay.BeginCountdown(onComplete: () => { });
            overlay.TickForTest(simulatedElapsed: 3.0f);
            Assert.AreEqual("GO!", text.text);
            Assert.AreEqual(4, click.pitchCalls.Count);  // 3, 2, 1, GO!
            Assert.AreEqual(1.5f, click.pitchCalls[3]);  // pitch-up on GO!
            Destroy(overlayGo, popup, pauseBtn);
        }

        [Test]
        public void BeginCountdown_HidesPauseButtonDuringSequence()
        {
            var (overlayGo, overlay, popup, pauseBtn, click) = Build();
            Assert.IsTrue(pauseBtn.activeSelf);
            overlay.BeginCountdown(onComplete: () => { });
            Assert.IsFalse(pauseBtn.activeSelf);
            overlay.TickForTest(simulatedElapsed: 2.5f);
            Assert.IsFalse(pauseBtn.activeSelf);
            Destroy(overlayGo, popup, pauseBtn);
        }

        [Test]
        public void TickForTest_AfterGoHold_InvokesOnCompleteAndShowsPauseButton()
        {
            var (overlayGo, overlay, popup, pauseBtn, click) = Build();
            bool callbackFired = false;
            overlay.BeginCountdown(onComplete: () => callbackFired = true);
            // Total: 3 steps × 1.0s + 0.4s GO! hold = 3.4s.
            overlay.TickForTest(simulatedElapsed: 3.45f);
            Assert.IsTrue(callbackFired);
            Assert.IsTrue(pauseBtn.activeSelf);
            Destroy(overlayGo, popup, pauseBtn);
        }
    }
}
