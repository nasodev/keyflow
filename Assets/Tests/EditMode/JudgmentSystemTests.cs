using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
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

        [Test]
        public void HandleTap_FiresFeedbackEvent_OnPerfect()
        {
            var js = MakeSystem();
            js.RegisterPendingNote(MakeNote(lane: 0, hitMs: 1000, pitch: 60));

            Judgment capturedJudgment = Judgment.Miss;
            Vector3 capturedPos = Vector3.zero;
            int callCount = 0;
            js.OnJudgmentFeedback += (j, p) => { capturedJudgment = j; capturedPos = p; callCount++; };

            // NoteController.MarkJudged() calls Destroy(gameObject); in EditMode this logs an
            // error (Destroy-in-edit-mode) that the test runner upgrades to a failure unless
            // acknowledged via LogAssert.Expect.
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
            js.InvokeHandleTapForTest(tapTimeMs: 1000, tapLane: 0);

            Assert.AreEqual(1, callCount);
            Assert.AreEqual(Judgment.Perfect, capturedJudgment);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void HandleTap_FiresFeedbackEvent_OnMiss()
        {
            var js = MakeSystem();
            js.RegisterPendingNote(MakeNote(lane: 0, hitMs: 1000, pitch: 60));

            Judgment capturedJudgment = Judgment.Perfect;
            int callCount = 0;
            js.OnJudgmentFeedback += (j, _) => { capturedJudgment = j; callCount++; };

            // A tap far outside the Good window yields Miss.
            // Normal Good window is 180 ms (per JudgmentEvaluator); +200ms delta -> Miss.
            js.InvokeHandleTapForTest(tapTimeMs: 1200, tapLane: 0);

            Assert.AreEqual(1, callCount, "Miss branch must still fire the feedback event (unlike score)");
            Assert.AreEqual(Judgment.Miss, capturedJudgment);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void HandleAutoMiss_FiresFeedbackEvent_WithNotePosition()
        {
            var js = MakeSystem();
            var note = MakeNote(lane: 0, hitMs: 1000, pitch: 60);
            note.transform.position = new Vector3(1.5f, -3f, 0f);
            js.RegisterPendingNote(note);

            Vector3 capturedPos = Vector3.zero;
            int callCount = 0;
            js.OnJudgmentFeedback += (_, p) => { capturedPos = p; callCount++; };

            js.HandleAutoMiss(note);

            Assert.AreEqual(1, callCount);
            Assert.AreEqual(new Vector3(1.5f, -3f, 0f), capturedPos);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void HandleHoldBreak_FiresFeedbackEvent_WithBrokenNotePosition()
        {
            var js = MakeSystem();
            var note = MakeNote(lane: 2, hitMs: 1000, pitch: 64);
            note.transform.position = new Vector3(-0.7f, -3f, 0f);

            Judgment capturedJudgment = Judgment.Perfect;
            Vector3 capturedPos = Vector3.zero;
            int callCount = 0;
            js.OnJudgmentFeedback += (j, p) => { capturedJudgment = j; capturedPos = p; callCount++; };

            js.HandleHoldBreak(note);

            Assert.AreEqual(1, callCount);
            Assert.AreEqual(Judgment.Miss, capturedJudgment);
            Assert.AreEqual(new Vector3(-0.7f, -3f, 0f), capturedPos);
            Object.DestroyImmediate(js.gameObject);
        }
    }
}
