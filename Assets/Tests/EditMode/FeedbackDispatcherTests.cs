using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.Feedback;

namespace KeyFlow.Tests.EditMode
{
    public class FeedbackDispatcherTests
    {
        private class FakeHaptics : IHapticService
        {
            public readonly List<Judgment> calls = new List<Judgment>();
            public void Fire(Judgment j) => calls.Add(j);
        }

        private class FakeParticles : IParticleSpawner
        {
            public readonly List<(Judgment j, Vector3 p)> calls = new();
            public void Spawn(Judgment j, Vector3 p) => calls.Add((j, p));
        }

        private class FakeTextSpawner : IJudgmentTextSpawner
        {
            public readonly List<(Judgment j, Vector3 p)> calls = new();
            public void Spawn(Judgment j, Vector3 p) => calls.Add((j, p));
        }

        [SetUp] public void Setup() { PlayerPrefs.DeleteAll(); }
        [TearDown] public void Teardown() { PlayerPrefs.DeleteAll(); }

        private static (JudgmentSystem js, FeedbackDispatcher d, FakeHaptics h, FakeParticles p, FakeTextSpawner t)
            Build()
        {
            var jsGo = new GameObject("judgment");
            var js = jsGo.AddComponent<JudgmentSystem>();
            js.Initialize(totalNotes: 4, difficulty: Difficulty.Normal);

            var dGo = new GameObject("dispatcher");
            var d = dGo.AddComponent<FeedbackDispatcher>();
            var h = new FakeHaptics();
            var p = new FakeParticles();
            var t = new FakeTextSpawner();
            // SetDependenciesForTest both injects deps and subscribes to the event,
            // so we do not need to manually fire OnEnable (which would trigger
            // Unity's ShouldRunBehaviour() assertion in EditMode).
            d.SetDependenciesForTest(js, h, p, t);
            return (js, d, h, p, t);
        }

        [Test]
        public void Dispatches_ToHaptics_WhenHapticsEnabled()
        {
            UserPrefs.HapticsEnabled = true;
            var (js, d, h, p, t) = Build();

            js.HandleAutoMiss(MakeNoteAt(Vector3.zero));

            Assert.AreEqual(1, h.calls.Count);
            Assert.AreEqual(Judgment.Miss, h.calls[0]);

            Object.DestroyImmediate(d.gameObject);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void Skips_Haptics_WhenHapticsDisabled()
        {
            UserPrefs.HapticsEnabled = false;
            var (js, d, h, p, t) = Build();

            js.HandleAutoMiss(MakeNoteAt(Vector3.zero));

            Assert.AreEqual(0, h.calls.Count);

            Object.DestroyImmediate(d.gameObject);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void AlwaysDispatches_ToParticlePool_RegardlessOfHapticsToggle()
        {
            UserPrefs.HapticsEnabled = false;
            var (js, d, h, p, t) = Build();

            js.HandleAutoMiss(MakeNoteAt(new Vector3(2f, 0f, 0f)));

            Assert.AreEqual(1, p.calls.Count);
            Assert.AreEqual(Judgment.Miss, p.calls[0].j);

            Object.DestroyImmediate(d.gameObject);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void WorldPosition_ForwardedUnchanged()
        {
            UserPrefs.HapticsEnabled = true;
            var (js, d, h, p, t) = Build();
            var note = MakeNoteAt(new Vector3(-1.23f, 4.56f, 0f));

            js.HandleAutoMiss(note);

            Assert.AreEqual(new Vector3(-1.23f, 4.56f, 0f), p.calls[0].p);

            Object.DestroyImmediate(d.gameObject);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void DispatchesMissKind_ViaAutoMiss()
        {
            // Perfect/Great/Good kind forwarding is covered in JudgmentSystemTests
            // (HandleTap_FiresFeedbackEvent_OnPerfect). Here we assert the dispatcher
            // forwards Miss specifically — the kind easy to produce deterministically in EditMode.
            UserPrefs.HapticsEnabled = true;
            var (js, d, h, p, t) = Build();

            js.HandleAutoMiss(MakeNoteAt(Vector3.zero));

            Assert.AreEqual(1, p.calls.Count);
            Assert.AreEqual(Judgment.Miss, p.calls[0].j);

            Object.DestroyImmediate(d.gameObject);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void Handle_InvokesTextPool()
        {
            UserPrefs.HapticsEnabled = true;
            var (js, d, h, p, t) = Build();

            js.HandleAutoMiss(MakeNoteAt(new Vector3(1.5f, 2.5f, 0f)));

            Assert.AreEqual(1, t.calls.Count);
            Assert.AreEqual(Judgment.Miss, t.calls[0].j);
            Assert.AreEqual(new Vector3(1.5f, 2.5f, 0f), t.calls[0].p);

            Object.DestroyImmediate(d.gameObject);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void Handle_ForwardsMultipleEventsToTextPool()
        {
            // Proves the dispatcher forwards each event individually, not that it
            // batches or deduplicates. Fires 4× Miss via HandleAutoMiss (the
            // easiest-to-produce deterministic event in EditMode). Per-judgment-kind
            // forwarding is covered by JudgmentSystemTests
            // (HandleTap_FiresFeedbackEvent_OnPerfect) + the dispatcher's
            // treat-judgment-as-opaque-parameter behavior.
            UserPrefs.HapticsEnabled = true;
            var (js, d, h, p, t) = Build();

            js.HandleAutoMiss(MakeNoteAt(Vector3.zero));
            js.HandleAutoMiss(MakeNoteAt(Vector3.zero));
            js.HandleAutoMiss(MakeNoteAt(Vector3.zero));
            js.HandleAutoMiss(MakeNoteAt(Vector3.zero));

            Assert.AreEqual(4, t.calls.Count);
            foreach (var c in t.calls) Assert.AreEqual(Judgment.Miss, c.j);

            Object.DestroyImmediate(d.gameObject);
            Object.DestroyImmediate(js.gameObject);
        }

        private static NoteController MakeNoteAt(Vector3 pos)
        {
            var go = new GameObject("note");
            var ctrl = go.AddComponent<NoteController>();
            ctrl.Initialize(
                sync: null, lane: 0, laneX: pos.x, hitMs: 1000, pitch: 60,
                type: KeyFlow.Charts.NoteType.TAP, durMs: 0,
                spawnY: 5f, judgmentY: -3f, previewMs: 2000, missGraceMs: 60,
                onAutoMiss: null);
            go.transform.position = pos;
            return ctrl;
        }
    }
}
