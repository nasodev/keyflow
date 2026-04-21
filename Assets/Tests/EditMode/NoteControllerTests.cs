using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.Charts;

namespace KeyFlow.Tests.EditMode
{
    public class NoteControllerTests
    {
        [Test]
        public void Initialize_PersistsPitch()
        {
            var go = new GameObject("note");
            var ctrl = go.AddComponent<NoteController>();

            ctrl.Initialize(
                sync: null,
                lane: 2,
                laneX: 0f,
                hitMs: 1000,
                pitch: 64,
                type: NoteType.TAP,
                durMs: 0,
                spawnY: 5f,
                judgmentY: -3f,
                previewMs: 2000,
                missGraceMs: 60,
                onAutoMiss: null);

            Assert.AreEqual(64, ctrl.Pitch);
            Assert.AreEqual(2, ctrl.Lane);
            Assert.AreEqual(1000, ctrl.HitTimeMs);

            Object.DestroyImmediate(go);
        }
    }
}
