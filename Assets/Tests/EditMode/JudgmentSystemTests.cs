using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.Charts;

namespace KeyFlow.Tests.EditMode
{
    public class JudgmentSystemTests
    {
        private static NoteController MakeNote(int lane, int hitMs, int pitch)
        {
            var go = new GameObject($"note_L{lane}_{hitMs}");
            var ctrl = go.AddComponent<NoteController>();
            ctrl.Initialize(
                sync: null,
                lane: lane,
                laneX: 0f,
                hitMs: hitMs,
                pitch: pitch,
                type: NoteType.TAP,
                durMs: 0,
                spawnY: 5f,
                judgmentY: -3f,
                previewMs: 2000,
                missGraceMs: 60,
                onAutoMiss: null);
            return ctrl;
        }

        private static JudgmentSystem MakeSystem()
        {
            var go = new GameObject("judgment");
            var js = go.AddComponent<JudgmentSystem>();
            js.Initialize(totalNotes: 4, difficulty: Difficulty.Normal);
            return js;
        }

        [Test]
        public void GetClosestPendingPitch_InWindow_ReturnsPitch()
        {
            var js = MakeSystem();
            js.RegisterPendingNote(MakeNote(lane: 1, hitMs: 1000, pitch: 64));

            int result = js.GetClosestPendingPitch(lane: 1, tapTimeMs: 1050, windowMs: 500);

            Assert.AreEqual(64, result);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void GetClosestPendingPitch_OutOfWindow_ReturnsMinusOne()
        {
            var js = MakeSystem();
            js.RegisterPendingNote(MakeNote(lane: 1, hitMs: 3000, pitch: 64));

            int result = js.GetClosestPendingPitch(lane: 1, tapTimeMs: 1000, windowMs: 500);

            Assert.AreEqual(-1, result);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void GetClosestPendingPitch_WrongLane_ReturnsMinusOne()
        {
            var js = MakeSystem();
            js.RegisterPendingNote(MakeNote(lane: 2, hitMs: 1000, pitch: 64));

            int result = js.GetClosestPendingPitch(lane: 0, tapTimeMs: 1000, windowMs: 500);

            Assert.AreEqual(-1, result);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void GetClosestPendingPitch_MultipleOnLane_ReturnsTemporallyNearest()
        {
            var js = MakeSystem();
            js.RegisterPendingNote(MakeNote(lane: 1, hitMs: 900, pitch: 60));
            js.RegisterPendingNote(MakeNote(lane: 1, hitMs: 1100, pitch: 67));

            int result = js.GetClosestPendingPitch(lane: 1, tapTimeMs: 1050, windowMs: 500);

            Assert.AreEqual(67, result, "note at hitMs=1100 (delta=50) is closer than 900 (delta=150)");
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void GetClosestPendingPitch_EmptyPending_ReturnsMinusOne()
        {
            var js = MakeSystem();

            int result = js.GetClosestPendingPitch(lane: 0, tapTimeMs: 1000, windowMs: 500);

            Assert.AreEqual(-1, result);
            Object.DestroyImmediate(js.gameObject);
        }
    }
}
