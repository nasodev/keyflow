using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.Charts;

namespace KeyFlow.Tests.EditMode
{
    public class HoldAudioRetriggerTests
    {
        private class ManualClock : ITimeSource
        {
            public double DspTime { get; set; }
        }

        private static AudioClip[] MakeDummyMap(int count)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++)
                clips[i] = AudioClip.Create($"dummy{i}", 1, 1, 48000, false);
            return clips;
        }

        // songTimeMs = (nowDsp - songStartDsp) * 1000 (calibOffset = 0).
        // After StartSilentSong at clock.DspTime = T0, songStartDsp = T0 + scheduleLeadSec (default 0.5).
        private static double DspTimeForSongMs(double songStartDsp, int desiredMs)
            => songStartDsp + desiredMs / 1000.0;

        private static (HoldTracker tracker, AudioSamplePool pool, AudioSyncManager audioSync,
                        ManualClock clock, GameObject host)
            BuildTracker()
        {
            var host = new GameObject("HoldTrackerHost");

            var poolGo = new GameObject("Pool");
            poolGo.transform.SetParent(host.transform);
            var pool = poolGo.AddComponent<AudioSamplePool>();
            pool.InitializeForTest(channels: 8);
            pool.SetPitchMapForTest(MakeDummyMap(17), baseMidiValue: 36, stepSemitonesValue: 3);

            var audioSyncGo = new GameObject("AudioSync");
            audioSyncGo.transform.SetParent(host.transform);
            audioSyncGo.AddComponent<AudioSource>();
            var audioSync = audioSyncGo.AddComponent<AudioSyncManager>();
            var clock = new ManualClock { DspTime = 100.0 };
            audioSync.TimeSource = clock;
            audioSync.StartSilentSong();   // IsPlaying = true; songStartDsp = 100.5

            var trackerGo = new GameObject("HoldTracker");
            trackerGo.transform.SetParent(host.transform);
            var tracker = trackerGo.AddComponent<HoldTracker>();
            tracker.SetDependenciesForTest(audioSync: audioSync, audioPool: pool);

            return (tracker, pool, audioSync, clock, host);
        }

        private static NoteController MakeNote(int lane, int hitMs, int durMs, int pitch)
        {
            var go = new GameObject($"Note_L{lane}_T{hitMs}");
            var note = go.AddComponent<NoteController>();
            note.SetForTest(lane: lane, hitTimeMs: hitMs, durMs: durMs, pitch: pitch, type: NoteType.HOLD);
            return note;
        }

        // Counts how many AudioSources on the pool GameObject have a non-null clip.
        // AudioSource.isPlaying is unreliable in EditMode/batch (no audio subsystem).
        // With 8 channels and only a handful of retriggers per test, cycling ensures
        // each retrigger lands on a fresh source, so `clip != null` count == total
        // retrigger calls across the test's lifetime. Tests must stay under 8 retriggers.
        private static int CountUsedSources(AudioSamplePool pool)
        {
            var sources = pool.gameObject.GetComponents<AudioSource>();
            int n = 0;
            foreach (var s in sources)
                if (s.clip != null) n++;
            return n;
        }

        [Test]
        public void OnHoldStartTapAccepted_FirstRetriggerAtTapTimePlus250()
        {
            var (tracker, pool, audioSync, clock, host) = BuildTracker();
            double songStart = audioSync.SongStartDspTime;

            var note = MakeNote(lane: 0, hitMs: 1000, durMs: 1000, pitch: 60);
            tracker.OnHoldStartTapAccepted(note, tapTimeMs: 980);

            clock.DspTime = DspTimeForSongMs(songStart, 1229);
            tracker.TickForTest();
            Assert.AreEqual(0, CountUsedSources(pool), "before tapTimeMs + 250 ms, no retrigger");

            clock.DspTime = DspTimeForSongMs(songStart, 1230);
            tracker.TickForTest();
            Assert.AreEqual(1, CountUsedSources(pool), "at tapTimeMs + 250 ms, one retrigger fires");

            Object.DestroyImmediate(host);
        }

        [Test]
        public void NoRetrigger_Before250ms()
        {
            var (tracker, pool, audioSync, clock, host) = BuildTracker();
            double songStart = audioSync.SongStartDspTime;

            var note = MakeNote(lane: 0, hitMs: 1000, durMs: 2000, pitch: 60);
            tracker.OnHoldStartTapAccepted(note, tapTimeMs: 1000);

            clock.DspTime = DspTimeForSongMs(songStart, 1249);
            tracker.TickForTest();

            Assert.AreEqual(0, CountUsedSources(pool));
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Retrigger_ThreeTimesOver750ms()
        {
            var (tracker, pool, audioSync, clock, host) = BuildTracker();
            double songStart = audioSync.SongStartDspTime;

            var note = MakeNote(lane: 0, hitMs: 1000, durMs: 2000, pitch: 60);
            tracker.OnHoldStartTapAccepted(note, tapTimeMs: 1000);

            // Tick at each retrigger boundary — songMs 1250, 1500, 1750 → 3 retriggers.
            // (Plan's original 100-ms stride from 1100..1750 skipped the exact 250-ms
            // boundaries due to floating-point truncation in int-cast SongTimeMs,
            // producing 2 retriggers instead of the intended 3.)
            int[] tickTimes = { 1250, 1500, 1750 };
            foreach (int t in tickTimes)
            {
                clock.DspTime = DspTimeForSongMs(songStart, t);
                tracker.TickForTest();
            }
            Assert.AreEqual(3, CountUsedSources(pool));
            Object.DestroyImmediate(host);
        }

        [Test]
        public void NoRetrigger_WhilePaused()
        {
            var (tracker, pool, audioSync, clock, host) = BuildTracker();
            double songStart = audioSync.SongStartDspTime;

            var note = MakeNote(lane: 0, hitMs: 1000, durMs: 2000, pitch: 60);
            tracker.OnHoldStartTapAccepted(note, tapTimeMs: 1000);

            // Advance to 1100 then pause — pause freezes SongTimeMs at pauseStartDsp.
            clock.DspTime = DspTimeForSongMs(songStart, 1100);
            audioSync.Pause();

            // Even though real time advances past 250 ms worth, no retrigger fires.
            clock.DspTime = DspTimeForSongMs(songStart, 1500);
            tracker.TickForTest();

            Assert.AreEqual(0, CountUsedSources(pool));
            Object.DestroyImmediate(host);
        }

        [Test]
        public void SameLaneOverlap_TwoHoldsBothTrackedIndependently()
        {
            // HOLD A: lane 0, t=1000, dur=1500, pitch=60. Tap at 1000.
            // HOLD B: lane 0, t=2000, dur=1500, pitch=72. Tap at 2000 — OVERLAPS A.
            var (tracker, pool, audioSync, clock, host) = BuildTracker();

            var a = MakeNote(lane: 0, hitMs: 1000, durMs: 1500, pitch: 60);
            tracker.OnHoldStartTapAccepted(a, tapTimeMs: 1000);

            var b = MakeNote(lane: 0, hitMs: 2000, durMs: 1500, pitch: 72);
            tracker.OnHoldStartTapAccepted(b, tapTimeMs: 2000);

            // Both entries exist. Lane-keyed storage would collapse this to 1.
            Assert.AreEqual(2, tracker.HoldAudioCountForTest,
                "Id-keyed holdAudio must hold both A and B even though they share a lane");

            Object.DestroyImmediate(host);
        }

        [Test]
        public void SetBpmForRetrigger_60bpm_FirstRetriggerAt500ms()
        {
            var (tracker, pool, audioSync, clock, host) = BuildTracker();
            double songStart = audioSync.SongStartDspTime;
            tracker.SetBpmForRetrigger(60);  // 8th note = 60000/60/2 = 500 ms

            var note = MakeNote(lane: 0, hitMs: 1000, durMs: 2000, pitch: 60);
            tracker.OnHoldStartTapAccepted(note, tapTimeMs: 1000);

            clock.DspTime = DspTimeForSongMs(songStart, 1499);
            tracker.TickForTest();
            Assert.AreEqual(0, CountUsedSources(pool), "before tap+500 ms, no retrigger at BPM 60");

            clock.DspTime = DspTimeForSongMs(songStart, 1500);
            tracker.TickForTest();
            Assert.AreEqual(1, CountUsedSources(pool), "at tap+500 ms, retrigger fires at BPM 60");

            Object.DestroyImmediate(host);
        }

        [Test]
        public void SetBpmForRetrigger_ZeroBpm_FallsBackTo250ms()
        {
            var (tracker, pool, audioSync, clock, host) = BuildTracker();
            double songStart = audioSync.SongStartDspTime;
            tracker.SetBpmForRetrigger(0);  // invalid → fallback

            var note = MakeNote(lane: 0, hitMs: 1000, durMs: 2000, pitch: 60);
            tracker.OnHoldStartTapAccepted(note, tapTimeMs: 1000);

            clock.DspTime = DspTimeForSongMs(songStart, 1250);
            tracker.TickForTest();
            Assert.AreEqual(1, CountUsedSources(pool), "BPM 0 falls back to 250 ms interval");

            Object.DestroyImmediate(host);
        }

        [Test]
        public void ResetForRetry_ClearsHoldAudio()
        {
            var (tracker, pool, audioSync, clock, host) = BuildTracker();
            var note = MakeNote(lane: 0, hitMs: 1000, durMs: 2000, pitch: 60);
            tracker.OnHoldStartTapAccepted(note, tapTimeMs: 1000);

            Assert.AreEqual(1, tracker.HoldAudioCountForTest);
            tracker.ResetForRetry();
            Assert.AreEqual(0, tracker.HoldAudioCountForTest);

            Object.DestroyImmediate(host);
        }
    }
}
