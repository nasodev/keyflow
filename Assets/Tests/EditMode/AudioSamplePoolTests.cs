using NUnit.Framework;
using UnityEngine;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class AudioSamplePoolTests
    {
        [Test]
        public void NextSource_CyclesThroughAllSources()
        {
            var go = new GameObject("pool");
            var pool = go.AddComponent<AudioSamplePool>();
            pool.InitializeForTest(channels: 4);

            var s1 = pool.NextSource();
            var s2 = pool.NextSource();
            var s3 = pool.NextSource();
            var s4 = pool.NextSource();
            var s5 = pool.NextSource();

            Assert.AreNotSame(s1, s2);
            Assert.AreNotSame(s2, s3);
            Assert.AreNotSame(s3, s4);
            Assert.AreSame(s1, s5, "pool should cycle back to first source");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void InitializeForTest_CreatesRequestedNumberOfSources()
        {
            var go = new GameObject("pool");
            var pool = go.AddComponent<AudioSamplePool>();
            pool.InitializeForTest(channels: 16);

            Assert.AreEqual(16, pool.Count);

            Object.DestroyImmediate(go);
        }
        private static AudioClip[] MakeDummyMap(int count)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++)
                clips[i] = AudioClip.Create($"dummy{i}", 1, 1, 48000, false);
            return clips;
        }

        [Test]
        public void ResolveSample_ExactSamplePitch_ReturnsRatioOne()
        {
            var map = MakeDummyMap(17);
            var r36 = AudioSamplePool.ResolveSample(36, map, 36, 3);
            var r48 = AudioSamplePool.ResolveSample(48, map, 36, 3);
            var r84 = AudioSamplePool.ResolveSample(84, map, 36, 3);

            Assert.AreSame(map[0], r36.clip);
            Assert.AreEqual(1f, r36.pitchRatio, 1e-6f);

            Assert.AreSame(map[4], r48.clip);
            Assert.AreEqual(1f, r48.pitchRatio, 1e-6f);

            Assert.AreSame(map[16], r84.clip);
            Assert.AreEqual(1f, r84.pitchRatio, 1e-6f);
        }

        [Test]
        public void ResolveSample_PlusOneSemitone_ReturnsSameClipWithRatioUp()
        {
            var map = MakeDummyMap(17);
            var expectedRatio = Mathf.Pow(2f, 1f / 12f);

            var r37 = AudioSamplePool.ResolveSample(37, map, 36, 3);
            Assert.AreSame(map[0], r37.clip);
            Assert.AreEqual(expectedRatio, r37.pitchRatio, 1e-5f);

            var r82 = AudioSamplePool.ResolveSample(82, map, 36, 3);
            Assert.AreSame(map[15], r82.clip, "MIDI 82 should stay on A5v10 (index 15), not cross to C6");
            Assert.AreEqual(expectedRatio, r82.pitchRatio, 1e-5f);
        }

        [Test]
        public void ResolveSample_OffsetTwo_CrossesToNextSampleDown()
        {
            var map = MakeDummyMap(17);
            var expectedRatio = Mathf.Pow(2f, -1f / 12f);

            var r38 = AudioSamplePool.ResolveSample(38, map, 36, 3);
            Assert.AreSame(map[1], r38.clip, "MIDI 38 should cross to Ds2v10 (index 1) at pitch -1");
            Assert.AreEqual(expectedRatio, r38.pitchRatio, 1e-5f);

            var r83 = AudioSamplePool.ResolveSample(83, map, 36, 3);
            Assert.AreSame(map[16], r83.clip, "MIDI 83 should cross to C6v10 (index 16) at pitch -1");
            Assert.AreEqual(expectedRatio, r83.pitchRatio, 1e-5f);
        }

        [Test]
        public void ResolveSample_OutOfRangeLow_ClampsToFirstSample()
        {
            var map = MakeDummyMap(17);
            var r = AudioSamplePool.ResolveSample(20, map, 36, 3);
            Assert.AreSame(map[0], r.clip);
            Assert.AreEqual(1f, r.pitchRatio, 1e-6f);
        }

        [Test]
        public void ResolveSample_OutOfRangeHigh_ClampsToLastSample()
        {
            var map = MakeDummyMap(17);
            var r = AudioSamplePool.ResolveSample(120, map, 36, 3);
            Assert.AreSame(map[16], r.clip);
            Assert.AreEqual(1f, r.pitchRatio, 1e-6f);
        }

        [Test]
        public void ResolveSample_EmptyMap_ReturnsNullWithRatioOne()
        {
            var r = AudioSamplePool.ResolveSample(60, new AudioClip[0], 36, 3);
            Assert.IsNull(r.clip);
            Assert.AreEqual(1f, r.pitchRatio, 1e-6f);
        }

        [Test]
        public void PlayForPitch_AssignsClipAndPitchToSource_NotLayered()
        {
            var go = new GameObject("pool");
            var pool = go.AddComponent<AudioSamplePool>();
            pool.InitializeForTest(channels: 4);

            var map = MakeDummyMap(17);
            pool.SetPitchMapForTest(map, 36, 3);

            pool.PlayForPitch(48); // midi 48 → map[4], ratio 1.0; uses sources[0]

            var sources = go.GetComponents<AudioSource>();
            Assert.AreSame(map[4], sources[0].clip,
                "PlayForPitch must assign the clip to the AudioSource (not layered PlayOneShot). " +
                "Layered PlayOneShot leaves src.clip null, exhausting Unity's real voice budget on rapid taps.");
            Assert.AreEqual(1f, sources[0].pitch, 1e-6f,
                "Pitch ratio must be applied to the source before Play().");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PlayForPitch_NoVolumeArg_UsesVolumeOne()
        {
            var go = new GameObject("pool");
            var pool = go.AddComponent<AudioSamplePool>();
            pool.InitializeForTest(channels: 4);
            pool.SetPitchMapForTest(MakeDummyMap(17), baseMidiValue: 36, stepSemitonesValue: 3);

            // Pre-set volume away from 1 so the test verifies PlayForPitch ACTUALLY assigns volume,
            // not that AudioSource defaulted to 1.
            var sources = go.GetComponents<AudioSource>();
            sources[0].volume = 0.5f;

            pool.PlayForPitch(60);

            Assert.AreEqual(1f, sources[0].volume, "default PlayForPitch should use volume = 1");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void PlayForPitch_WithVolumeArg_PassesToSource()
        {
            var go = new GameObject("pool");
            var pool = go.AddComponent<AudioSamplePool>();
            pool.InitializeForTest(channels: 4);
            pool.SetPitchMapForTest(MakeDummyMap(17), baseMidiValue: 36, stepSemitonesValue: 3);

            // Pre-set volume away from 0.7 so the test verifies PlayForPitch ACTUALLY assigns
            // the passed volume, not that AudioSource happened to hold it.
            var sources = go.GetComponents<AudioSource>();
            sources[0].volume = 0.5f;

            pool.PlayForPitch(60, volume: 0.7f);

            Assert.AreEqual(0.7f, sources[0].volume, 1e-5f);

            Object.DestroyImmediate(go);
        }
    }
}
