using NUnit.Framework;
using UnityEngine;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class AudioSyncPauseTests
    {
        private class ManualClock : ITimeSource
        {
            public double DspTime { get; set; }
        }

        private GameObject go;
        private AudioSyncManager sync;
        private ManualClock clock;

        [SetUp]
        public void Setup()
        {
            go = new GameObject("sync");
            go.AddComponent<AudioSource>();
            sync = go.AddComponent<AudioSyncManager>();
            clock = new ManualClock { DspTime = 100.0 };
            sync.TimeSource = clock;
        }

        [TearDown]
        public void Teardown() { Object.DestroyImmediate(go); }

        [Test]
        public void SongTimeMs_IsFrozenWhilePaused()
        {
            sync.StartSilentSong();
            clock.DspTime = 101.0;
            int t0 = sync.SongTimeMs;
            sync.Pause();
            clock.DspTime = 105.0;
            Assert.AreEqual(t0, sync.SongTimeMs);
        }

        [Test]
        public void SongTimeMs_ContinuesAfterResume()
        {
            sync.StartSilentSong();
            clock.DspTime = 102.0;
            int before = sync.SongTimeMs;
            sync.Pause();
            clock.DspTime = 110.0;
            sync.Resume();
            clock.DspTime = 111.0;
            int after = sync.SongTimeMs;
            Assert.AreEqual(before + 1000, after, "SongTime should advance by only the non-paused delta");
        }

        [Test]
        public void Stop_ResetsStartedFlagAndAllowsFreshStart()
        {
            // SP11 retry-bug guard: after Stop(), IsPlaying is false so NoteSpawner
            // gate holds during countdown; then StartSilentSong re-enables with a
            // fresh songStartDsp (not the stale one from the previous session).
            sync.StartSilentSong();
            Assert.IsTrue(sync.IsPlaying, "precondition: started after StartSilentSong");
            double staleStart = sync.SongStartDspTime;

            sync.Stop();
            Assert.IsFalse(sync.IsPlaying, "Stop() must clear started flag");
            Assert.IsFalse(sync.IsPaused);
            Assert.AreEqual(0, sync.SongTimeMs, "SongTimeMs returns 0 when !started");

            // Simulate a retry: clock advances while 'stopped', then re-start.
            clock.DspTime = 200.0;
            sync.StartSilentSong();
            Assert.IsTrue(sync.IsPlaying);
            Assert.AreNotEqual(staleStart, sync.SongStartDspTime,
                "StartSilentSong after Stop must compute a fresh songStartDsp");
        }

        [Test]
        public void Stop_WhilePaused_ClearsBothFlagsAndUnpausesListener()
        {
            sync.StartSilentSong();
            sync.Pause();
            Assert.IsTrue(sync.IsPaused, "precondition: paused");

            sync.Stop();
            Assert.IsFalse(sync.IsPlaying);
            Assert.IsFalse(sync.IsPaused);
            Assert.IsFalse(AudioListener.pause, "AudioListener.pause must be released");
        }

        [Test]
        public void PauseAndResume_AreIdempotent()
        {
            sync.StartSilentSong();
            clock.DspTime = 101.0;
            sync.Pause();
            clock.DspTime = 102.0;
            sync.Pause();
            clock.DspTime = 103.0;
            sync.Resume();
            sync.Resume();
            Assert.IsFalse(sync.IsPaused);
        }
    }
}
