using System;
using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.UI;

namespace KeyFlow.Tests.EditMode
{
    public class GameplayControllerCountdownTests
    {
        private class FakeCountdown : ICountdownOverlay
        {
            public int callCount;
            public Action pendingCallback;
            public void BeginCountdown(Action onComplete)
            {
                callCount++;
                pendingCallback = onComplete;
            }
            public void FireCallback() => pendingCallback?.Invoke();
        }

        [Test]
        public void SetCountdownForTest_BeforeBeginGameplay_NoOp()
        {
            // Guard test: injecting a fake before any gameplay call should not
            // spontaneously start audio or callbacks.
            var go = new GameObject("gp");
            var gp = go.AddComponent<GameplayController>();
            var fake = new FakeCountdown();
            gp.SetCountdownForTest(fake);
            Assert.AreEqual(0, fake.callCount);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void BeginCountdownInvoked_AudioNotStartedYet()
        {
            var go = new GameObject("gp");
            var gp = go.AddComponent<GameplayController>();
            var audioGo = new GameObject("audio");
            audioGo.AddComponent<AudioSource>();
            var audio = audioGo.AddComponent<AudioSyncManager>();
            // Wire audioSync via reflection (matches existing project pattern in SceneBuilder.SetField).
            typeof(GameplayController).GetField("audioSync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(gp, audio);

            var fake = new FakeCountdown();
            gp.SetCountdownForTest(fake);

            gp.InvokeStartCountdownForTest();

            Assert.AreEqual(1, fake.callCount);
            Assert.IsFalse(audio.IsPlaying);  // callback not yet fired
            Assert.IsFalse(gp.PlayingForTest);

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(audioGo);
        }

        [Test]
        public void CountdownCallback_StartsAudioAndPlaying()
        {
            var go = new GameObject("gp");
            var gp = go.AddComponent<GameplayController>();
            var audioGo = new GameObject("audio");
            audioGo.AddComponent<AudioSource>();
            var audio = audioGo.AddComponent<AudioSyncManager>();
            typeof(GameplayController).GetField("audioSync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(gp, audio);

            var fake = new FakeCountdown();
            gp.SetCountdownForTest(fake);

            gp.InvokeStartCountdownForTest();
            fake.FireCallback();

            Assert.IsTrue(audio.IsPlaying);
            Assert.IsTrue(gp.PlayingForTest);

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(audioGo);
        }

        [Test]
        public void InvokeStartCountdown_OnRetry_StopsPriorSessionAudio()
        {
            // SP11 retry-bug guard: audioSync.started stays true across gameplay
            // sessions (no ResetForRetry on AudioSyncManager). Without Stop() before
            // countdown, NoteSpawner sees IsPlaying=true + stale songStartDsp during
            // the 3-second countdown window and spawns every upcoming note at once.
            var go = new GameObject("gp");
            var gp = go.AddComponent<GameplayController>();
            var audioGo = new GameObject("audio");
            audioGo.AddComponent<AudioSource>();
            var audio = audioGo.AddComponent<AudioSyncManager>();
            typeof(GameplayController).GetField("audioSync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(gp, audio);

            var fake = new FakeCountdown();
            gp.SetCountdownForTest(fake);

            // Simulate prior gameplay session leaving audioSync running.
            audio.StartSilentSong();
            Assert.IsTrue(audio.IsPlaying, "precondition: prior session left started=true");

            gp.InvokeStartCountdownForTest();

            // Before countdown callback fires, audio must be Stop()'d so NoteSpawner
            // stays gated. Countdown invocation itself happened (FakeCountdown).
            Assert.IsFalse(audio.IsPlaying, "Stop() must clear started before countdown starts");
            Assert.AreEqual(1, fake.callCount);

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(audioGo);
        }
    }
}
