using NUnit.Framework;
using UnityEngine;
using KeyFlow;

namespace KeyFlow.Tests
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
    }
}
