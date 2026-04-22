# KeyFlow W6 SP4 — Judgment Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship per-judgment haptic + particle feedback (Perfect/Great/Good/Miss tiers) while preserving the SP3 GC=0 baseline and adding a Settings-level haptic on/off toggle.

**Architecture:** `JudgmentSystem` fires a new `OnJudgmentFeedback(Judgment, Vector3)` event from all four judgment paths (tap-judged including Miss, auto-miss, hold-break). A new `FeedbackDispatcher` MonoBehaviour subscribes and fans out to `HapticService` (gated by `UserPrefs.HapticsEnabled`) and `ParticlePool`. Haptics use a thin `AndroidJavaObject` wrapper over `VibrationEffect` (API 26+), with compile-time Editor/non-Android no-op. Particles use two prefabs (`hit.prefab` with runtime tint + `miss.prefab`) round-robin-pooled. All presets live in a `FeedbackPresets` ScriptableObject so device tuning happens in the Inspector without code rebuilds.

**Tech Stack:** Unity 6000.3.13f1 Android (minSdk 26, arm64-v8a), NUnit EditMode tests, `AndroidJavaObject` native bridge, Unity `ParticleSystem`, ScriptableObject-driven presets.

**Spec:** `docs/superpowers/specs/2026-04-22-keyflow-w6-sp4-judgment-feedback-design.md`

---

## File structure

**Created:**
- `Assets/Scripts/Feedback/JudgmentFeedbackEvent.cs` — readonly struct payload
- `Assets/Scripts/Feedback/IHapticService.cs` — interface for DI in tests
- `Assets/Scripts/Feedback/IParticleSpawner.cs` — interface for DI in tests
- `Assets/Scripts/Feedback/FeedbackPresets.cs` — ScriptableObject class (preset table)
- `Assets/Scripts/Feedback/AndroidHapticsBridge.cs` — static `#if UNITY_ANDROID && !UNITY_EDITOR` wrapper
- `Assets/Scripts/Feedback/HapticService.cs` — MonoBehaviour implementing `IHapticService`
- `Assets/Scripts/Feedback/ParticlePool.cs` — MonoBehaviour implementing `IParticleSpawner`
- `Assets/Scripts/Feedback/FeedbackDispatcher.cs` — MonoBehaviour subscriber
- `Assets/Scripts/Feedback/KeyFlow.Feedback.asmdef` — assembly definition (or adjust existing; see Task 1)
- `Assets/Tests/EditMode/FeedbackDispatcherTests.cs`
- `Assets/ScriptableObjects/FeedbackPresets.asset` — authored in Editor
- `Assets/Prefabs/Feedback/hit.prefab` — authored in Editor (Task 10)
- `Assets/Prefabs/Feedback/miss.prefab` — authored in Editor (Task 10)
- `docs/superpowers/reports/2026-04-22-w6-sp4-completion.md` — final report (Task 13)

**Modified:**
- `Assets/Scripts/Gameplay/JudgmentSystem.cs` — add `OnJudgmentFeedback` event; fire from `HandleTap` (including Miss branch), `HandleAutoMiss`, `HandleHoldBreak(NoteController)` (signature change)
- `Assets/Scripts/Gameplay/HoldTracker.cs` — update call site at line 51 to pass `note` argument
- `Assets/Scripts/Common/UserPrefs.cs` — add `HapticsEnabled` bool property
- `Assets/Scripts/UI/SettingsScreen.cs` — add haptic Toggle UI binding
- `Assets/Tests/EditMode/JudgmentSystemTests.cs` — add 4 event-firing tests
- `Assets/Tests/EditMode/UserPrefsTests.cs` — add HapticsEnabled tests
- `Assets/Editor/SceneBuilder.cs` — instantiate + wire `FeedbackDispatcher`, `HapticService`, `ParticlePool`; add Settings Toggle
- `Assets/Editor/ApkBuilder.cs` — release APK output name bump `keyflow-w6-sp2.apk` → `keyflow-w6-sp4.apk` (profile build also bumped)

**NOT modified (guardrails):**
- `Assets/Scripts/Gameplay/TapInputHandler.cs` — audio/latency path stays identical
- `Assets/Scripts/Gameplay/AudioSamplePool.cs` — unrelated
- `Assets/Scripts/Gameplay/HoldStateMachine.cs` — transition contract unchanged
- Release `ApkBuilder.Build` method signature — stays identical (only output filename bumped)

---

## Task 1: UserPrefs.HapticsEnabled foundation

**Files:**
- Modify: `Assets/Scripts/Common/UserPrefs.cs`
- Modify: `Assets/Tests/EditMode/UserPrefsTests.cs`

- [ ] **Step 1: Write failing tests**

Append to `Assets/Tests/EditMode/UserPrefsTests.cs` inside the `UserPrefsTests` class (do not alter existing tests):

```csharp
[Test] public void HapticsEnabled_DefaultsToTrue_WhenNeverSet()
{
    Assert.IsTrue(UserPrefs.HapticsEnabled);
}

[Test] public void HapticsEnabled_RoundTripsFalse()
{
    UserPrefs.HapticsEnabled = false;
    Assert.IsFalse(UserPrefs.HapticsEnabled);
}

[Test] public void HapticsEnabled_RoundTripsTrueAfterFalse()
{
    UserPrefs.HapticsEnabled = false;
    UserPrefs.HapticsEnabled = true;
    Assert.IsTrue(UserPrefs.HapticsEnabled);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run via Unity Editor Test Runner (Window → General → Test Runner → EditMode → Run All) OR via CLI:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

**Do NOT pass `-quit`** (known project quirk — `-quit` + `-runTests` skips the runner; see memory).
**IMPORTANT**: Close any interactive Unity Editor on this project before running batch mode (IL2CPP link fails at step 1101/1110 with concurrent Editor — SP3 finding).

Expected: 3 new tests FAIL with "HapticsEnabled does not exist".

- [ ] **Step 3: Implement HapticsEnabled in UserPrefs**

Add to `Assets/Scripts/Common/UserPrefs.cs` — one const and one property. Insert the const after existing const block (after `Legacy_CalibOffset`), and the property after `CalibrationOffsetMs`:

```csharp
private const string K_HapticsEnabled = "KeyFlow.Settings.HapticsEnabled";
```

```csharp
public static bool HapticsEnabled
{
    get => PlayerPrefs.GetInt(K_HapticsEnabled, 1) == 1;
    set { PlayerPrefs.SetInt(K_HapticsEnabled, value ? 1 : 0); PlayerPrefs.Save(); }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run EditMode tests. Expected: all 3 new tests PASS. All existing UserPrefsTests still PASS (total +3).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Common/UserPrefs.cs Assets/Tests/EditMode/UserPrefsTests.cs
git commit -m "feat(w6-sp4): UserPrefs.HapticsEnabled toggle (default on)"
```

---

## Task 2: Feedback module scaffold — struct, interfaces, ScriptableObject class

**Files:**
- Create: `Assets/Scripts/Feedback/JudgmentFeedbackEvent.cs`
- Create: `Assets/Scripts/Feedback/IHapticService.cs`
- Create: `Assets/Scripts/Feedback/IParticleSpawner.cs`
- Create: `Assets/Scripts/Feedback/FeedbackPresets.cs`

These are pure contract definitions with no behavior, so they share one commit. No EditMode tests yet — tests land with the consumers (Task 4 onward).

- [ ] **Step 1: Create JudgmentFeedbackEvent struct**

`Assets/Scripts/Feedback/JudgmentFeedbackEvent.cs`:

```csharp
using UnityEngine;

namespace KeyFlow.Feedback
{
    public readonly struct JudgmentFeedbackEvent
    {
        public readonly Judgment Kind;
        public readonly Vector3 WorldPos;

        public JudgmentFeedbackEvent(Judgment kind, Vector3 worldPos)
        {
            Kind = kind;
            WorldPos = worldPos;
        }
    }
}
```

- [ ] **Step 2: Create IHapticService interface**

`Assets/Scripts/Feedback/IHapticService.cs`:

```csharp
namespace KeyFlow.Feedback
{
    public interface IHapticService
    {
        void Fire(Judgment judgment);
    }
}
```

- [ ] **Step 3: Create IParticleSpawner interface**

`Assets/Scripts/Feedback/IParticleSpawner.cs`:

```csharp
using UnityEngine;

namespace KeyFlow.Feedback
{
    public interface IParticleSpawner
    {
        void Spawn(Judgment judgment, Vector3 worldPos);
    }
}
```

- [ ] **Step 4: Create FeedbackPresets ScriptableObject class**

`Assets/Scripts/Feedback/FeedbackPresets.cs`:

```csharp
using UnityEngine;

namespace KeyFlow.Feedback
{
    [CreateAssetMenu(fileName = "FeedbackPresets", menuName = "KeyFlow/FeedbackPresets")]
    public class FeedbackPresets : ScriptableObject
    {
        [System.Serializable]
        public struct HapticPreset
        {
            public int durationMs;
            [Range(0, 255)] public int amplitude;
        }

        [System.Serializable]
        public struct ParticlePreset
        {
            public Color tintColor;
            public float startSize;
            public int burstCount;
        }

        [Header("Haptics (VibrationEffect.createOneShot(ms, amplitude))")]
        public HapticPreset perfect = new HapticPreset { durationMs = 15, amplitude = 200 };
        public HapticPreset great   = new HapticPreset { durationMs = 10, amplitude = 120 };
        public HapticPreset good    = new HapticPreset { durationMs = 8,  amplitude = 60  };
        public HapticPreset miss    = new HapticPreset { durationMs = 40, amplitude = 180 };

        [Header("Particles (hit.prefab tint; miss uses prefab-fixed values)")]
        public ParticlePreset perfectParticle = new ParticlePreset {
            tintColor = new Color(1f, 1f, 1f, 1f), startSize = 0.45f, burstCount = 16 };
        public ParticlePreset greatParticle   = new ParticlePreset {
            tintColor = new Color(0.7f, 0.9f, 1f, 1f), startSize = 0.32f, burstCount = 10 };
        public ParticlePreset goodParticle    = new ParticlePreset {
            tintColor = new Color(0.8f, 1f, 0.8f, 1f), startSize = 0.22f, burstCount = 6 };

        public HapticPreset GetHaptic(Judgment j) => j switch
        {
            Judgment.Perfect => perfect,
            Judgment.Great   => great,
            Judgment.Good    => good,
            _                => miss,
        };

        public ParticlePreset GetParticle(Judgment j) => j switch
        {
            Judgment.Perfect => perfectParticle,
            Judgment.Great   => greatParticle,
            _                => goodParticle,
            // Miss uses prefab-fixed values — callers route to miss.prefab and ignore this
        };
    }
}
```

- [ ] **Step 5: Verify compilation**

Open Unity Editor (or re-run the EditMode test batch command). Expected: compile succeeds, existing 114 tests still pass, no new tests yet.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Feedback/JudgmentFeedbackEvent.cs Assets/Scripts/Feedback/IHapticService.cs Assets/Scripts/Feedback/IParticleSpawner.cs Assets/Scripts/Feedback/FeedbackPresets.cs
git commit -m "feat(w6-sp4): feedback module scaffold (event, interfaces, presets SO)"
```

---

## Task 3: JudgmentSystem event + HandleHoldBreak signature change

**Files:**
- Modify: `Assets/Scripts/Gameplay/JudgmentSystem.cs`
- Modify: `Assets/Tests/EditMode/JudgmentSystemTests.cs`
- Modify: `Assets/Scripts/Gameplay/HoldTracker.cs` (call-site update — same commit to keep compile green)

- [ ] **Step 1: Write failing tests for the new event**

Append to `Assets/Tests/EditMode/JudgmentSystemTests.cs` inside the `JudgmentSystemTests` class:

```csharp
[Test]
public void HandleTap_FiresFeedbackEvent_OnPerfect()
{
    var js = MakeSystem();
    js.RegisterPendingNote(MakeNote(lane: 0, hitMs: 1000, pitch: 60));

    Judgment capturedJudgment = Judgment.Miss;
    Vector3 capturedPos = Vector3.zero;
    int callCount = 0;
    js.OnJudgmentFeedback += (j, p) => { capturedJudgment = j; capturedPos = p; callCount++; };

    // Simulate a Perfect tap by invoking HandleTap via reflection OR public proxy.
    // JudgmentSystem's HandleTap is subscribed via TapInputHandler.OnLaneTap in production;
    // for tests we invoke via the public test-only entry point added in this task.
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
    // Normal Good window is 180 ms (per JudgmentEvaluator); +200ms delta → Miss.
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run EditMode tests. Expected: 4 new tests FAIL with compile errors (`OnJudgmentFeedback`, `InvokeHandleTapForTest`, and new `HandleHoldBreak(NoteController)` signature don't exist).

- [ ] **Step 3: Modify JudgmentSystem.cs**

Replace the full contents of `Assets/Scripts/Gameplay/JudgmentSystem.cs` with:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using KeyFlow.Charts;

namespace KeyFlow
{
    public class JudgmentSystem : MonoBehaviour
    {
        [SerializeField] private TapInputHandler tapInput;
        [SerializeField] private HoldTracker holdTracker;

        private readonly List<NoteController> pending = new List<NoteController>();
        private ScoreManager score;
        private Difficulty difficulty;

        public ScoreManager Score => score;
        public Judgment LastJudgment { get; private set; }
        public int LastDeltaMs { get; private set; }

        public event Action<Judgment, Vector3> OnJudgmentFeedback;

        public void Initialize(int totalNotes, Difficulty difficulty)
        {
            this.difficulty = difficulty;
            score = new ScoreManager(totalNotes);
            LastJudgment = Judgment.Miss;
        }

        public void ResetForRetry()
        {
            pending.Clear();
            score = null;
            LastJudgment = Judgment.Miss;
            LastDeltaMs = 0;
        }

        private void OnEnable()
        {
            if (tapInput != null) tapInput.OnLaneTap += HandleTap;
            if (holdTracker == null)
                Debug.LogError("JudgmentSystem: holdTracker SerializeField is unassigned. HOLD notes will silently behave as TAPs.");
        }

        private void OnDisable()
        {
            if (tapInput != null) tapInput.OnLaneTap -= HandleTap;
        }

        public void RegisterPendingNote(NoteController note)
        {
            pending.Add(note);
        }

        public void HandleAutoMiss(NoteController note)
        {
            if (score == null) return;
            pending.Remove(note);
            score.RegisterJudgment(Judgment.Miss);
            LastJudgment = Judgment.Miss;
            LastDeltaMs = 0;
            OnJudgmentFeedback?.Invoke(Judgment.Miss, note.transform.position);
        }

        public void HandleHoldBreak(NoteController brokenNote)
        {
            if (score == null) return;
            score.RegisterJudgment(Judgment.Miss);
            LastJudgment = Judgment.Miss;
            LastDeltaMs = 0;
            OnJudgmentFeedback?.Invoke(Judgment.Miss, brokenNote.transform.position);
        }

        private void HandleTap(int tapTimeMs, int tapLane)
        {
            if (score == null) return;

            NoteController closest = null;
            int closestAbsDelta = int.MaxValue;
            for (int i = 0; i < pending.Count; i++)
            {
                var n = pending[i];
                if (n == null || n.Judged) continue;
                if (n.Lane != tapLane) continue;
                int delta = tapTimeMs - n.HitTimeMs;
                int abs = delta < 0 ? -delta : delta;
                if (abs < closestAbsDelta)
                {
                    closestAbsDelta = abs;
                    closest = n;
                }
            }

            if (closest == null) return;

            int signedDelta = tapTimeMs - closest.HitTimeMs;
            var result = JudgmentEvaluator.Evaluate(signedDelta, difficulty);

            // Fire feedback for EVERY branch that reaches here, including Miss.
            // Miss still early-returns for score purposes (note stays in pending), but
            // the player's attempted tap should still produce feedback.
            OnJudgmentFeedback?.Invoke(result.Judgment, closest.transform.position);

            if (result.Judgment == Judgment.Miss) return;

            score.RegisterJudgment(result.Judgment);
            LastJudgment = result.Judgment;
            LastDeltaMs = result.DeltaMs;
            pending.Remove(closest);

            if (closest.Type == NoteType.HOLD && holdTracker != null)
            {
                closest.MarkAcceptedAsHold();
                holdTracker.OnHoldStartTapAccepted(closest);
            }
            else
            {
                closest.MarkJudged();
            }
        }

        public int GetClosestPendingPitch(int lane, int tapTimeMs, int windowMs)
        {
            NoteController closest = null;
            int closestAbsDelta = int.MaxValue;
            for (int i = 0; i < pending.Count; i++)
            {
                var n = pending[i];
                if (n == null || n.Judged) continue;
                if (n.Lane != lane) continue;
                int delta = tapTimeMs - n.HitTimeMs;
                int abs = delta < 0 ? -delta : delta;
                if (abs < closestAbsDelta)
                {
                    closestAbsDelta = abs;
                    closest = n;
                }
            }
            if (closest == null || closestAbsDelta > windowMs) return -1;
            return closest.Pitch;
        }

        // Test-only entry — HandleTap is private because in production it's wired
        // exclusively via TapInputHandler.OnLaneTap. EditMode tests can't construct
        // a TapInputHandler easily, so we expose an internal proxy.
        internal void InvokeHandleTapForTest(int tapTimeMs, int tapLane)
            => HandleTap(tapTimeMs, tapLane);
    }
}
```

Key changes vs current file:
1. `using System;` added for `Action`.
2. `public event Action<Judgment, Vector3> OnJudgmentFeedback;` added.
3. `HandleAutoMiss` invokes event with `note.transform.position`.
4. `HandleHoldBreak()` → `HandleHoldBreak(NoteController brokenNote)`; invokes event.
5. `HandleTap` moved `OnJudgmentFeedback?.Invoke(...)` call **before** the `if (Miss) return`.
6. New `InvokeHandleTapForTest` internal method for EditMode test reach-in.

- [ ] **Step 4: Update HoldTracker call site (keeps compile green)**

Edit `Assets/Scripts/Gameplay/HoldTracker.cs` line 51 only:

```csharp
// before:
judgmentSystem.HandleHoldBreak();
// after:
judgmentSystem.HandleHoldBreak(note);
```

`note` is already in scope (line 43: `if (!idToNote.TryGetValue(t.id, out var note)) continue;`).

- [ ] **Step 5: Expose `InvokeHandleTapForTest` to test assembly**

The `internal` modifier requires `InternalsVisibleTo`. Check `Assets/Scripts/AssemblyInfo.cs` or the main runtime asmdef. If no `InternalsVisibleTo` exists for the test assembly, add to `Assets/Scripts/Gameplay/JudgmentSystem.cs` top-of-file (after usings):

```csharp
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("KeyFlow.Tests.EditMode")]
```

(If a runtime-wide `AssemblyInfo.cs` already defines this, skip — no duplicate needed. Check first:)

```bash
grep -r "InternalsVisibleTo" Assets/Scripts/ 2>/dev/null
```

If test assembly name differs from `KeyFlow.Tests.EditMode`, adjust to match `Assets/Tests/EditMode/*.asmdef`.

- [ ] **Step 6: Run tests to verify they pass**

Run EditMode tests. Expected: 4 new tests PASS. All 114 existing tests still PASS (total 118 at this point).

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Gameplay/JudgmentSystem.cs Assets/Scripts/Gameplay/HoldTracker.cs Assets/Tests/EditMode/JudgmentSystemTests.cs
git commit -m "feat(w6-sp4): JudgmentSystem.OnJudgmentFeedback event + HandleHoldBreak(note)"
```

---

## Task 4: FeedbackDispatcher MonoBehaviour + tests

**Files:**
- Create: `Assets/Scripts/Feedback/FeedbackDispatcher.cs`
- Create: `Assets/Tests/EditMode/FeedbackDispatcherTests.cs`

- [ ] **Step 1: Write failing tests**

`Assets/Tests/EditMode/FeedbackDispatcherTests.cs`:

```csharp
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

        [SetUp] public void Setup() { PlayerPrefs.DeleteAll(); }
        [TearDown] public void Teardown() { PlayerPrefs.DeleteAll(); }

        private static (JudgmentSystem js, FeedbackDispatcher d, FakeHaptics h, FakeParticles p)
            Build()
        {
            var jsGo = new GameObject("judgment");
            var js = jsGo.AddComponent<JudgmentSystem>();
            js.Initialize(totalNotes: 4, difficulty: Difficulty.Normal);

            var dGo = new GameObject("dispatcher");
            var d = dGo.AddComponent<FeedbackDispatcher>();
            var h = new FakeHaptics();
            var p = new FakeParticles();
            d.SetDependenciesForTest(js, h, p);

            // Manually drive OnEnable subscription (Unity doesn't run it on AddComponent
            // in EditMode tests for new GameObjects already-enabled). SendMessage forces it.
            d.SendMessage("OnEnable");
            return (js, d, h, p);
        }

        [Test]
        public void Dispatches_ToHaptics_WhenHapticsEnabled()
        {
            UserPrefs.HapticsEnabled = true;
            var (js, d, h, p) = Build();

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
            var (js, d, h, p) = Build();

            js.HandleAutoMiss(MakeNoteAt(Vector3.zero));

            Assert.AreEqual(0, h.calls.Count);

            Object.DestroyImmediate(d.gameObject);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void AlwaysDispatches_ToParticlePool_RegardlessOfHapticsToggle()
        {
            UserPrefs.HapticsEnabled = false;
            var (js, d, h, p) = Build();

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
            var (js, d, h, p) = Build();
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
            // forwards Miss specifically — the kind that's easy to produce deterministically
            // in EditMode without wiring a TapInputHandler.
            UserPrefs.HapticsEnabled = true;
            var (js, d, h, p) = Build();

            js.HandleAutoMiss(MakeNoteAt(Vector3.zero));

            Assert.AreEqual(1, p.calls.Count);
            Assert.AreEqual(Judgment.Miss, p.calls[0].j);

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
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: all 5 tests FAIL with "FeedbackDispatcher does not exist".

- [ ] **Step 3: Implement FeedbackDispatcher**

`Assets/Scripts/Feedback/FeedbackDispatcher.cs`:

```csharp
using UnityEngine;

namespace KeyFlow.Feedback
{
    public class FeedbackDispatcher : MonoBehaviour
    {
        [SerializeField] private JudgmentSystem judgmentSystem;
        [SerializeField] private HapticService hapticService;
        [SerializeField] private ParticlePool particlePool;

        private IHapticService haptic;
        private IParticleSpawner particles;

        private void Awake()
        {
            if (haptic == null) haptic = hapticService;
            if (particles == null) particles = particlePool;
        }

        private void OnEnable()
        {
            if (judgmentSystem == null)
            {
                Debug.LogError("FeedbackDispatcher: judgmentSystem unassigned.");
                return;
            }
            judgmentSystem.OnJudgmentFeedback += Handle;
        }

        private void OnDisable()
        {
            if (judgmentSystem != null) judgmentSystem.OnJudgmentFeedback -= Handle;
        }

        private void Handle(Judgment j, Vector3 worldPos)
        {
            if (UserPrefs.HapticsEnabled && haptic != null) haptic.Fire(j);
            if (particles != null) particles.Spawn(j, worldPos);
        }

        internal void SetDependenciesForTest(
            JudgmentSystem js, IHapticService h, IParticleSpawner p)
        {
            judgmentSystem = js;
            haptic = h;
            particles = p;
        }
    }
}
```

**Note**: `HapticService` and `ParticlePool` types are forward-referenced here — they're created in Task 5 and Task 6. If compile fails, add empty stubs to those files first (classes extending MonoBehaviour with no body), then fill them in.

- [ ] **Step 4: Add HapticService + ParticlePool empty stubs (to let Task 4 compile)**

`Assets/Scripts/Feedback/HapticService.cs`:

```csharp
using UnityEngine;

namespace KeyFlow.Feedback
{
    public class HapticService : MonoBehaviour, IHapticService
    {
        public void Fire(Judgment judgment) { /* filled in Task 6 */ }
    }
}
```

`Assets/Scripts/Feedback/ParticlePool.cs`:

```csharp
using UnityEngine;

namespace KeyFlow.Feedback
{
    public class ParticlePool : MonoBehaviour, IParticleSpawner
    {
        public void Spawn(Judgment judgment, Vector3 worldPos) { /* filled in Task 7 */ }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Expected: 5 new `FeedbackDispatcherTests` PASS. All prior tests still green (total 123).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Feedback/FeedbackDispatcher.cs Assets/Scripts/Feedback/HapticService.cs Assets/Scripts/Feedback/ParticlePool.cs Assets/Tests/EditMode/FeedbackDispatcherTests.cs
git commit -m "feat(w6-sp4): FeedbackDispatcher + empty HapticService/ParticlePool stubs"
```

---

## Task 5: AndroidHapticsBridge native wrapper

**Files:**
- Create: `Assets/Scripts/Feedback/AndroidHapticsBridge.cs`

Platform-gated code — no EditMode tests possible (the real code path only runs on device). Editor verifies compile only.

- [ ] **Step 1: Write the bridge**

`Assets/Scripts/Feedback/AndroidHapticsBridge.cs`:

```csharp
using UnityEngine;

namespace KeyFlow.Feedback
{
    /// <summary>
    /// Thin static wrapper around Android Vibrator + VibrationEffect (API 26+).
    /// All calls compile to no-op outside UNITY_ANDROID or inside UNITY_EDITOR.
    /// </summary>
    public static class AndroidHapticsBridge
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static AndroidJavaClass vibrationEffectClass;
        private static bool initialized;
        private static bool available;

        private static void EnsureInit()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                using var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity");
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator == null) { available = false; return; }
                bool hasVib = vibrator.Call<bool>("hasVibrator");
                if (!hasVib) { available = false; return; }
                vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                available = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[KeyFlow] Haptics init failed: {e.Message}");
                available = false;
            }
        }

        public static AndroidJavaObject CreateOneShot(int durationMs, int amplitude)
        {
            EnsureInit();
            if (!available) return null;
            return vibrationEffectClass.CallStatic<AndroidJavaObject>(
                "createOneShot", (long)durationMs, amplitude);
        }

        public static void Vibrate(AndroidJavaObject effect)
        {
            EnsureInit();
            if (!available || effect == null) return;
            vibrator.Call("vibrate", effect);
        }
#else
        public static object CreateOneShot(int durationMs, int amplitude) => null;
        public static void Vibrate(object effect) { /* no-op */ }
#endif
    }
}
```

**Why `object` overloads on non-Android**: `AndroidJavaObject` is Android-only; using `object` lets `HapticService` call the same API signature from shared code.

- [ ] **Step 2: Verify compile in Editor**

Run EditMode tests (or just open Unity — compile errors surface immediately). Expected: 0 errors, all 123 tests still pass.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Feedback/AndroidHapticsBridge.cs
git commit -m "feat(w6-sp4): AndroidHapticsBridge — VibrationEffect wrapper (API 26+)"
```

---

## Task 6: HapticService full implementation

**Files:**
- Modify: `Assets/Scripts/Feedback/HapticService.cs`

No new EditMode tests — the device native path cannot run in Editor. The stub from Task 4 is replaced with the full implementation.

- [ ] **Step 1: Implement HapticService with cached VibrationEffect objects**

Replace the full contents of `Assets/Scripts/Feedback/HapticService.cs`:

```csharp
using UnityEngine;

namespace KeyFlow.Feedback
{
    public class HapticService : MonoBehaviour, IHapticService
    {
        [SerializeField] private FeedbackPresets presets;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject effectPerfect;
        private AndroidJavaObject effectGreat;
        private AndroidJavaObject effectGood;
        private AndroidJavaObject effectMiss;
#else
        private object effectPerfect, effectGreat, effectGood, effectMiss;
#endif

        private bool ready;

        private void Awake()
        {
            if (presets == null)
            {
                Debug.LogError("HapticService: presets SerializeField unassigned — all Fire() calls will no-op.");
                return;
            }

            // Cache the 4 VibrationEffect Java objects once; reuse on every Fire.
            effectPerfect = AndroidHapticsBridge.CreateOneShot(
                presets.perfect.durationMs, presets.perfect.amplitude)
#if UNITY_ANDROID && !UNITY_EDITOR
                as AndroidJavaObject;
#else
                ;
#endif
            effectGreat = AndroidHapticsBridge.CreateOneShot(
                presets.great.durationMs, presets.great.amplitude)
#if UNITY_ANDROID && !UNITY_EDITOR
                as AndroidJavaObject;
#else
                ;
#endif
            effectGood = AndroidHapticsBridge.CreateOneShot(
                presets.good.durationMs, presets.good.amplitude)
#if UNITY_ANDROID && !UNITY_EDITOR
                as AndroidJavaObject;
#else
                ;
#endif
            effectMiss = AndroidHapticsBridge.CreateOneShot(
                presets.miss.durationMs, presets.miss.amplitude)
#if UNITY_ANDROID && !UNITY_EDITOR
                as AndroidJavaObject;
#else
                ;
#endif
            ready = true;
        }

        public void Fire(Judgment judgment)
        {
            if (!ready) return;
            var effect = judgment switch
            {
                Judgment.Perfect => effectPerfect,
                Judgment.Great   => effectGreat,
                Judgment.Good    => effectGood,
                _                => effectMiss,
            };
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidHapticsBridge.Vibrate(effect);
#else
            AndroidHapticsBridge.Vibrate(effect);
#endif
        }
    }
}
```

- [ ] **Step 2: Verify compile + tests**

Run EditMode tests. Expected: 0 errors, 123 tests pass (no new tests).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Feedback/HapticService.cs
git commit -m "feat(w6-sp4): HapticService — cached VibrationEffect per judgment"
```

---

## Task 7: ParticlePool full implementation

**Files:**
- Modify: `Assets/Scripts/Feedback/ParticlePool.cs`

Pool sizes: 16 hit + 4 miss (§4.2/§5 design — Entertainer Normal NPS 3 × 0.6s lifetime ≈ 2 concurrent, 8× safety).

- [ ] **Step 1: Implement ParticlePool**

Replace the full contents of `Assets/Scripts/Feedback/ParticlePool.cs`:

```csharp
using UnityEngine;

namespace KeyFlow.Feedback
{
    public class ParticlePool : MonoBehaviour, IParticleSpawner
    {
        [SerializeField] private ParticleSystem hitPrefab;
        [SerializeField] private ParticleSystem missPrefab;
        [SerializeField] private FeedbackPresets presets;
        [SerializeField] private int hitPoolSize = 16;
        [SerializeField] private int missPoolSize = 4;

        private ParticleSystem[] hitPool;
        private ParticleSystem[] missPool;
        private int hitNextIndex;
        private int missNextIndex;
        private bool ready;

        private void Awake()
        {
            if (hitPrefab == null || missPrefab == null)
            {
                Debug.LogError("ParticlePool: prefab refs unassigned — Spawn calls will no-op.");
                return;
            }
            if (presets == null)
            {
                Debug.LogError("ParticlePool: presets unassigned — tints will be default.");
            }
            hitPool = new ParticleSystem[hitPoolSize];
            for (int i = 0; i < hitPoolSize; i++)
            {
                var ps = Instantiate(hitPrefab, transform);
                ps.gameObject.SetActive(false);
                hitPool[i] = ps;
            }
            missPool = new ParticleSystem[missPoolSize];
            for (int i = 0; i < missPoolSize; i++)
            {
                var ps = Instantiate(missPrefab, transform);
                ps.gameObject.SetActive(false);
                missPool[i] = ps;
            }
            ready = true;
        }

        public void Spawn(Judgment judgment, Vector3 worldPos)
        {
            if (!ready) return;
            if (judgment == Judgment.Miss)
            {
                SpawnFromPool(missPool, ref missNextIndex, worldPos, null);
            }
            else
            {
                FeedbackPresets.ParticlePreset? preset = presets != null
                    ? presets.GetParticle(judgment)
                    : (FeedbackPresets.ParticlePreset?)null;
                SpawnFromPool(hitPool, ref hitNextIndex, worldPos, preset);
            }
        }

        private static void SpawnFromPool(
            ParticleSystem[] pool, ref int nextIndex, Vector3 worldPos,
            FeedbackPresets.ParticlePreset? preset)
        {
            var ps = pool[nextIndex];
            nextIndex = (nextIndex + 1) % pool.Length;

            ps.transform.position = worldPos;

            if (preset.HasValue)
            {
                // Struct-copy-safe idiom: assign to local, mutate field, assign back.
                // ParticleSystem.main returns a MainModule struct but its setter applies
                // the change to the underlying system — no GC allocation.
                var main = ps.main;
                main.startColor = preset.Value.tintColor;
                main.startSize = preset.Value.startSize;
                var emission = ps.emission;
                // Burst 0 = the default burst configured on the prefab; rewrite its count.
                var burst = emission.GetBurst(0);
                burst.count = preset.Value.burstCount;
                emission.SetBurst(0, burst);
            }

            if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);
            ps.Clear();
            ps.Play();
        }
    }
}
```

- [ ] **Step 2: Verify compile + tests**

Run EditMode tests. Expected: 0 errors, 123 tests pass.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Feedback/ParticlePool.cs
git commit -m "feat(w6-sp4): ParticlePool — round-robin 16 hit + 4 miss, zero-alloc spawn"
```

---

## Task 8: Author particle prefabs + FeedbackPresets asset

**Files:**
- Create: `Assets/Prefabs/Feedback/hit.prefab` (via Unity Editor)
- Create: `Assets/Prefabs/Feedback/miss.prefab` (via Unity Editor)
- Create: `Assets/ScriptableObjects/FeedbackPresets.asset` (via Unity Editor)

**This task requires the Unity Editor UI.** No CLI equivalent. Either do it manually as described, or (preferred) add an editor script that generates prefabs procedurally following the SceneBuilder pattern.

### Option A: Procedural generator (recommended for reproducibility)

- [ ] **Step 1: Create Editor generator script**

`Assets/Editor/FeedbackPrefabBuilder.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;
using KeyFlow.Feedback;

namespace KeyFlow.Editor
{
    public static class FeedbackPrefabBuilder
    {
        private const string PrefabDir = "Assets/Prefabs/Feedback";
        private const string SoDir = "Assets/ScriptableObjects";

        [MenuItem("KeyFlow/Build Feedback Assets")]
        public static void Build()
        {
            EnsureFolder(PrefabDir);
            EnsureFolder(SoDir);

            BuildHitPrefab();
            BuildMissPrefab();
            BuildPresetsAsset();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KeyFlow] Feedback assets built.");
        }

        private static void BuildHitPrefab()
        {
            var go = new GameObject("hit");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSpeed = 2.0f;
            main.startSize = 0.35f;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            var burst = new ParticleSystem.Burst(0f, 12);
            emission.SetBursts(new[] { burst });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            // Default Particle material; Unity provides "Default-Particle.mat"
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>(
                "Default-Particle.mat");

            string path = $"{PrefabDir}/hit.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void BuildMissPrefab()
        {
            var go = new GameObject("miss");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = -1.5f;  // inward collapse
            main.startSize = 0.30f;
            main.startColor = new Color(1f, 0.3f, 0.3f, 1f);  // red
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 0.3f, 0.3f), 0f),
                    new GradientColorKey(new Color(0.5f, 0f, 0f), 1f),
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>(
                "Default-Particle.mat");

            string path = $"{PrefabDir}/miss.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void BuildPresetsAsset()
        {
            string path = $"{SoDir}/FeedbackPresets.asset";
            if (AssetDatabase.LoadAssetAtPath<FeedbackPresets>(path) != null) return;
            var so = ScriptableObject.CreateInstance<FeedbackPresets>();
            AssetDatabase.CreateAsset(so, path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
```

- [ ] **Step 2: Run the generator**

In Unity Editor: menu **KeyFlow → Build Feedback Assets**. Verify 3 assets created under `Assets/Prefabs/Feedback/` and `Assets/ScriptableObjects/`.

(CLI equivalent:)
```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod KeyFlow.Editor.FeedbackPrefabBuilder.Build -quit -logFile Builds/feedback-build.txt
```

Remember: **no overlapping interactive Editor sessions** (IL2CPP quirk, SP3 finding).

- [ ] **Step 3: Verify assets exist**

```bash
ls Assets/Prefabs/Feedback/ Assets/ScriptableObjects/
```

Expected: `hit.prefab`, `hit.prefab.meta`, `miss.prefab`, `miss.prefab.meta`, `FeedbackPresets.asset`, `FeedbackPresets.asset.meta`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/FeedbackPrefabBuilder.cs Assets/Prefabs/Feedback/ Assets/ScriptableObjects/
git commit -m "feat(w6-sp4): procedural feedback prefab + presets generator"
```

---

## Task 9: Scene wiring — SceneBuilder additions

**Files:**
- Modify: `Assets/Editor/SceneBuilder.cs`

SceneBuilder constructs the runtime scene programmatically. After this task the rebuilt `GameplayScene.unity` includes `FeedbackDispatcher`, `HapticService`, `ParticlePool` as child GameObjects wired to `JudgmentSystem`.

- [ ] **Step 1: Read SceneBuilder to find the wiring point**

```bash
cat Assets/Editor/SceneBuilder.cs
```

Locate `BuildManagers` (which creates `judgmentSystem`) and the block after it — that's where to insert the Feedback wiring. Also note where `BuildSettingsCanvas` is called.

- [ ] **Step 2: Add feedback wiring method**

`SceneBuilder.cs` already has a private static helper `SetField(Object target, string name, Object value)` around line 1234 — **use it, do not introduce a new helper**. Add this new private static method to `SceneBuilder`:

```csharp
private static void BuildFeedbackPipeline(
    JudgmentSystem judgmentSystem, Transform parent)
{
    var feedbackRoot = new GameObject("FeedbackPipeline");
    feedbackRoot.transform.SetParent(parent, false);

    var presets = AssetDatabase.LoadAssetAtPath<FeedbackPresets>(
        "Assets/ScriptableObjects/FeedbackPresets.asset");
    var hitPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(
        "Assets/Prefabs/Feedback/hit.prefab");
    var missPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(
        "Assets/Prefabs/Feedback/miss.prefab");

    if (presets == null || hitPrefab == null || missPrefab == null)
    {
        Debug.LogError("SceneBuilder: Feedback assets missing. Run 'KeyFlow/Build Feedback Assets' first.");
        return;
    }

    var hapticsGo = new GameObject("HapticService");
    hapticsGo.transform.SetParent(feedbackRoot.transform, false);
    var hapticService = hapticsGo.AddComponent<HapticService>();
    SetField(hapticService, "presets", presets);

    var particlesGo = new GameObject("ParticlePool");
    particlesGo.transform.SetParent(feedbackRoot.transform, false);
    var particlePool = particlesGo.AddComponent<ParticlePool>();
    SetField(particlePool, "hitPrefab", hitPrefab);
    SetField(particlePool, "missPrefab", missPrefab);
    SetField(particlePool, "presets", presets);

    var dispatcherGo = new GameObject("FeedbackDispatcher");
    dispatcherGo.transform.SetParent(feedbackRoot.transform, false);
    var dispatcher = dispatcherGo.AddComponent<FeedbackDispatcher>();
    SetField(dispatcher, "judgmentSystem", judgmentSystem);
    SetField(dispatcher, "hapticService", hapticService);
    SetField(dispatcher, "particlePool", particlePool);
}
```

Add this `using` directive near the top of `SceneBuilder.cs` (alongside the existing `using KeyFlow;` block):

```csharp
using KeyFlow.Feedback;
```

**Note on `Object` ambiguity:** the existing `SetField` signature uses `Object target` — this resolves to `UnityEngine.Object` because of the file's existing `using UnityEngine;`. Do not add `using System;` to this file unless necessary; if you must, qualify the helper as `UnityEngine.Object` to avoid CS0104 ambiguity.

- [ ] **Step 3: Call BuildFeedbackPipeline from Build**

In `SceneBuilder.Build`, after the `BuildManagers` call (which returns `judgmentSystem`), insert:

```csharp
BuildFeedbackPipeline(judgmentSystem, gameplayRoot.transform);
```

- [ ] **Step 4: Rebuild the scene**

Via Editor menu: **KeyFlow → Build W4 Scene** (existing menu item — confirm it's still named W4 despite us being on W6; or add a new W6 variant). Or CLI:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod KeyFlow.Editor.SceneBuilder.Build -quit -logFile Builds/scene-build.txt
```

- [ ] **Step 5: Verify scene file changed**

```bash
git status Assets/Scenes/GameplayScene.unity
```

Expected: `Assets/Scenes/GameplayScene.unity` modified.

- [ ] **Step 6: Run EditMode tests for regression**

Expected: 123 tests still pass.

- [ ] **Step 7: Commit**

```bash
git add Assets/Editor/SceneBuilder.cs Assets/Scenes/GameplayScene.unity
git commit -m "feat(w6-sp4): SceneBuilder wires FeedbackDispatcher + HapticService + ParticlePool"
```

---

## Task 10: SettingsScreen haptic toggle

**Files:**
- Modify: `Assets/Scripts/UI/SettingsScreen.cs`
- Modify: `Assets/Editor/SceneBuilder.cs` (toggle UI element in BuildSettingsCanvas)

- [ ] **Step 1: Add Toggle SerializeField + handler to SettingsScreen**

Modify `Assets/Scripts/UI/SettingsScreen.cs`:

Add to SerializeFields (after `creditsLabel`):

```csharp
[SerializeField] private Toggle hapticsToggle;
```

Add to `Awake` (after `creditsLabel` block):

```csharp
if (hapticsToggle != null)
    hapticsToggle.onValueChanged.AddListener(OnHapticsToggleChanged);
```

Add to `OnShown` (after `AudioListener.volume` line):

```csharp
if (hapticsToggle != null) hapticsToggle.SetIsOnWithoutNotify(UserPrefs.HapticsEnabled);
```

Add method:

```csharp
private void OnHapticsToggleChanged(bool v)
{
    UserPrefs.HapticsEnabled = v;
}
```

- [ ] **Step 2: Add haptic toggle UI to SceneBuilder.BuildSettingsCanvas**

Open `Assets/Editor/SceneBuilder.cs`, find `BuildSettingsCanvas` (around line 693). Current layout: Title at y=0.9, SFX label y=0.75 / slider y=0.7, Speed label y=0.6 / slider y=0.55, Recalibrate button y=0.35. Place the Haptics toggle row at y=0.45 (between Speed value and Recalibrate).

There is **no existing `BuildToggle` helper** in SceneBuilder. Add this private static helper near the existing `BuildSlider`/`BuildPrimaryButton` helpers — a Unity `Toggle` needs a Background Image + Checkmark Image child to render visibly:

```csharp
private static Toggle BuildToggle(
    Transform parent, Sprite whiteSprite, Vector2 anchor, Vector2 size)
{
    var go = new GameObject("Toggle");
    go.transform.SetParent(parent, false);
    var rt = go.AddComponent<RectTransform>();
    rt.anchorMin = anchor;
    rt.anchorMax = anchor;
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.sizeDelta = size;

    var toggle = go.AddComponent<Toggle>();

    var bgGo = new GameObject("Background");
    bgGo.transform.SetParent(go.transform, false);
    var bgRT = bgGo.AddComponent<RectTransform>();
    bgRT.anchorMin = Vector2.zero;
    bgRT.anchorMax = Vector2.one;
    bgRT.offsetMin = Vector2.zero;
    bgRT.offsetMax = Vector2.zero;
    var bgImg = bgGo.AddComponent<Image>();
    bgImg.sprite = whiteSprite;
    bgImg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
    toggle.targetGraphic = bgImg;

    var checkGo = new GameObject("Checkmark");
    checkGo.transform.SetParent(bgGo.transform, false);
    var checkRT = checkGo.AddComponent<RectTransform>();
    checkRT.anchorMin = new Vector2(0.1f, 0.1f);
    checkRT.anchorMax = new Vector2(0.9f, 0.9f);
    checkRT.offsetMin = Vector2.zero;
    checkRT.offsetMax = Vector2.zero;
    var checkImg = checkGo.AddComponent<Image>();
    checkImg.sprite = whiteSprite;
    checkImg.color = new Color(0.4f, 0.85f, 0.5f, 1f);
    toggle.graphic = checkImg;

    return toggle;
}
```

Then inside `BuildSettingsCanvas`, after the speed-value text (around line 759) and before the Recalibrate button (line 762), insert:

```csharp
// Haptics toggle
CreateCenteredText(canvasGO.transform, "HapticsLabel", "Haptics", 28,
    new Vector2(0.35f, 0.45f), new Vector2(300, 50));
var hapticsToggle = BuildToggle(canvasGO.transform, whiteSprite,
    new Vector2(0.65f, 0.45f), new Vector2(60, 60));
```

Finally, at the end of `BuildSettingsCanvas` where the existing `SetField` block assigns SettingsScreen fields (around line 781-788), add:

```csharp
SetField(screen, "hapticsToggle", hapticsToggle);
```

**If `UIStrings` is preferred over hardcoded "Haptics":** add `public const string HapticsLabel = "Haptics";` (or Korean equivalent consistent with existing UI strings) to `Assets/Scripts/UI/UIStrings.cs` and swap the literal. Check existing UIStrings first — follow whichever localization style is already in use.

- [ ] **Step 3: Rebuild scene**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod KeyFlow.Editor.SceneBuilder.Build -quit -logFile Builds/scene-build.txt
```

- [ ] **Step 4: Run EditMode tests for regression**

Expected: 123 tests still pass (no new tests for SettingsScreen — the toggle wiring is Editor-authored UI, not logic).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/SettingsScreen.cs Assets/Editor/SceneBuilder.cs Assets/Scenes/GameplayScene.unity
git commit -m "feat(w6-sp4): SettingsScreen haptics toggle + scene UI"
```

---

## Task 11: Release APK name bump + build

**Files:**
- Modify: `Assets/Editor/ApkBuilder.cs`

- [ ] **Step 1: Bump APK output filenames**

Modify `Assets/Editor/ApkBuilder.cs`:
- `Builds/keyflow-w6-sp2.apk` → `Builds/keyflow-w6-sp4.apk` (release)
- `Builds/keyflow-w6-sp3-profile.apk` → `Builds/keyflow-w6-sp4-profile.apk`

- [ ] **Step 2: Close any interactive Unity Editor session on this project**

Per SP3 finding: IL2CPP link fails at step 1101/1110 if interactive Editor is open. Verify no Unity Editor window has this project loaded before running build.

- [ ] **Step 3: Build release APK**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod KeyFlow.Editor.ApkBuilder.Build -quit -logFile Builds/apk-build.txt
```

Expected: `Builds/keyflow-w6-sp4.apk` exists. Check size:

```bash
ls -la Builds/keyflow-w6-sp4.apk
```

Target: < 35 MB (spec guardrail).

- [ ] **Step 4: Install on Galaxy S22 via ADB**

```bash
adb devices                                       # verify R5CT21A31QB listed
adb install -r Builds/keyflow-w6-sp4.apk
```

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/ApkBuilder.cs
git commit -m "build(w6-sp4): bump APK output names to keyflow-w6-sp4*"
```

---

## Task 12: Device playtest + Inspector tuning loop

**Goal:** Validate the 9-item §6.3 device checklist from the spec. Tune presets via Inspector if feel is off; no code changes expected.

- [ ] **Step 1: Launch the app on S22 and play Entertainer Normal full run**

Checklist (from spec §6.3):
- [ ] Perfect / Great / Good haptic intensity distinguishable by feel
- [ ] Intentional off-timing tap → Miss buzz (~40ms) clearly felt
- [ ] Particles: Perfect white-big / Great light-blue-medium / Good light-green-small all distinguishable at playing distance

- [ ] **Step 2: Exercise Miss paths**

- [ ] Hands-off auto-miss on a note → Miss buzz + red inward-collapse particle at note's on-screen position
- [ ] Play Für Elise Normal, intentionally break a Hold mid-way → Miss buzz + red particle at break position

- [ ] **Step 3: Exercise Settings toggle round-trip**

- [ ] Navigate to Settings → haptics toggle OFF → resume gameplay → no vibration; particles still fire
- [ ] Settings → haptics toggle ON → resume → vibration resumes

- [ ] **Step 4: Profiler GC verification**

Rebuild profile APK and re-run the 2-min Entertainer Normal session:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod KeyFlow.Editor.ApkBuilder.BuildProfile -quit -logFile Builds/profile-build.txt
adb install -r Builds/keyflow-w6-sp4-profile.apk
```

Attach Unity Profiler (Window → Analysis → Profiler → Connected Players → device). Record a 2-min Entertainer Normal session with taps, misses, and hold-breaks.

- [ ] `GC.Collect` count == 0 across the session

If non-zero: likely causes documented in spec §6.5 — re-check `VibrationEffect` object caching (all 4 constructed once in Awake) and `ParticleSystem.main`/`.emission` struct assignment pattern.

- [ ] **Step 5: Tune presets in Inspector (if needed)**

Open Unity Editor → `Assets/ScriptableObjects/FeedbackPresets.asset`. Adjust haptic durations/amplitudes or particle colors/sizes/burstCounts based on device feel. Re-deploy release APK, re-test.

**Commit tuning changes if any:**

```bash
git add Assets/ScriptableObjects/FeedbackPresets.asset
git commit -m "tune(w6-sp4): device playtest adjustments for <specifically-what>"
```

- [ ] **Step 6: Record checklist results**

All 9 checklist items ticked → proceed to Task 13. Any unticked → fix in code and loop Task 11–12.

---

## Task 13: Completion report + final commit

**Files:**
- Create: `docs/superpowers/reports/2026-04-22-w6-sp4-completion.md`

- [ ] **Step 1: Capture final state**

Run:

```bash
git log --oneline main..HEAD          # all commits this SP
ls -la Builds/keyflow-w6-sp4.apk       # final APK size
```

EditMode test count: should be 123 (114 baseline + 3 UserPrefs + 4 JudgmentSystem + 5 FeedbackDispatcher — though 5 might count as 4 if one test was split/merged; verify actual count).

- [ ] **Step 2: Write the completion report**

`docs/superpowers/reports/2026-04-22-w6-sp4-completion.md`:

Template sections:
1. **Summary** — SP4 goal, what was shipped (haptic + particle judgment feedback, Settings toggle)
2. **Commits** — one-line per commit (`git log main..HEAD --oneline`)
3. **Files touched** — new + modified lists
4. **Tests** — before/after counts
5. **APK** — size before (33.70 MB from SP3) and after
6. **Profiler result** — GC.Collect count = 0 confirmed
7. **Device checklist results** — all 9 items checked
8. **Tuning notes** — any preset adjustments made during device testing
9. **Carry-overs** — anything deferred (should mirror spec §10)

Follow the exact structure of `docs/superpowers/reports/2026-04-22-w6-sp3-completion.md` (same SP pattern).

- [ ] **Step 3: Commit the report**

```bash
git add docs/superpowers/reports/2026-04-22-w6-sp4-completion.md
git commit -m "docs(w6-sp4): completion report"
```

- [ ] **Step 4: Verify clean working tree**

```bash
git status
```

Expected: `nothing to commit, working tree clean`.

- [ ] **Step 5: Offer merge to user**

SP branch ready for merge to `main`. Surface to user for sign-off before merging (per project pattern — merge is user-initiated).

---

## Success criteria (mirrors spec §9)

- ✅ EditMode tests green, ≥ 122 total (target was 114 + 8; actual will be 123 if all tests above pass)
- ✅ Device checklist §6.3 all 9 items pass
- ✅ APK < 35 MB
- ✅ Profiler confirms `GC.Collect` = 0
- ✅ User sign-off on S22 playtest

## Rollback triggers (mirrors spec §9)

- Tap→audio latency regresses beyond W1 PoC ±10 ms jitter band
- Any `GC.Collect` activity re-appears on device
- Haptic vendor quirk makes feature unusable → fall back: particle-only, default `HapticsEnabled = false`
- Particle visuals cause measurable frame drops during peak density

---

## References

- Spec: `docs/superpowers/specs/2026-04-22-keyflow-w6-sp4-judgment-feedback-design.md`
- Parent MVP v2 spec: `docs/superpowers/specs/2026-04-20-keyflow-mvp-v2-4lane-design.md`
- Sibling SP3 plan (style reference): `docs/superpowers/plans/2026-04-22-keyflow-w6-sp3-profiler-pass.md`
- Sibling SP3 completion report (structure): `docs/superpowers/reports/2026-04-22-w6-sp3-completion.md`
- Android VibrationEffect API docs: https://developer.android.com/reference/android/os/VibrationEffect
- Unity ParticleSystem module idioms: https://docs.unity3d.com/ScriptReference/ParticleSystem.MainModule.html
