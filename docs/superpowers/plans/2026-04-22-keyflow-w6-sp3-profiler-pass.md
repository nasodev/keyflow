# KeyFlow W6 SP3 — Profiler Pass Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate all `GC.Alloc` in the gameplay hot loop so `GC.Collect` count == 0 during a 2-minute Entertainer Normal session, resolving W4 carry-over #1 ("mid-game tap drops on device").

**Architecture:** Device-attached Unity Profiler session against Galaxy S22 captures a baseline → top-N GC allocators are fixed with reusable-buffer patterns → profiler re-capture verifies GC Collect = 0 → release APK regression playtest confirms no behavior drift.

**Tech Stack:** Unity 6000.3.13f1 Android, Unity Profiler (standard sampling mode, not Deep), NUnit EditMode tests, ADB for install/attach.

**Spec:** `docs/superpowers/specs/2026-04-22-keyflow-w6-sp3-profiler-pass-design.md`

---

## File structure

**Created:**
- `Assets/Editor/ApkBuilder.cs` — **extended** with a new `BuildProfile()` method. Kept in the same file (31 lines pre-SP3, adding ~20 lines for the profile build).
- `docs/superpowers/reports/2026-04-22-w6-sp3-profile-baseline.md` — baseline + after profile numbers
- `docs/superpowers/reports/2026-04-22-w6-sp3-completion.md` — final completion report (Task 10)

**Modified (per profile findings — concrete files depend on baseline):**
- `Assets/Scripts/Gameplay/HoldTracker.cs` — expected allocator (line 34 `new HashSet<int>()`)
- `Assets/Scripts/Gameplay/HoldStateMachine.cs` — expected allocator (line 55 `new List<HoldTransition>()`); also signature change
- `Assets/Tests/EditMode/HoldStateMachineTests.cs` — migrate 10 existing `sm.Tick(...)` call sites + rewrite `TickReturnsTransitions_ForObservation` + add 2 new tests

**NOT modified (guardrails):**
- `Assets/Scripts/Audio/*` — unless profile shows a hit (hypothesis only)
- `Assets/Scripts/UI/*` — not in gameplay hot loop
- `Assets/Scripts/Catalog/*` — scene-load only
- `tools/midi_to_kfchart/*` — unrelated
- Release-path `ApkBuilder.Build()` method — stays byte-identical (the new `BuildProfile` is a sibling method)

---

## Task 1: Add ApkBuilder.BuildProfile development-build entry point

**Files:**
- Modify: `Assets/Editor/ApkBuilder.cs`

- [ ] **Step 1: Read the existing file**

```bash
cat Assets/Editor/ApkBuilder.cs
```

Current content is a 31-line single-method class. The new method is a sibling.

- [ ] **Step 2: Add BuildProfile method**

Edit `Assets/Editor/ApkBuilder.cs` — append a new method `BuildProfile` inside the `ApkBuilder` class. Final file content (both methods together, full file):

```csharp
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;

namespace KeyFlow.Editor
{
    public static class ApkBuilder
    {
        [MenuItem("KeyFlow/Build APK")]
        public static void Build()
        {
            string dir = "Builds";
            Directory.CreateDirectory(dir);
            string apk = Path.Combine(dir, "keyflow-w6-sp2.apk");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/GameplayScene.unity" },
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"APK build failed: {report.summary.result}");
            UnityEngine.Debug.Log($"[KeyFlow] APK built at {apk}, size {report.summary.totalSize / 1024 / 1024} MB");
        }

        [MenuItem("KeyFlow/Build APK (Profile)")]
        public static void BuildProfile()
        {
            string dir = "Builds";
            Directory.CreateDirectory(dir);
            string apk = Path.Combine(dir, "keyflow-w6-sp3-profile.apk");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/GameplayScene.unity" },
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging
            };

            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"Profile APK build failed: {report.summary.result}");
            UnityEngine.Debug.Log($"[KeyFlow] Profile APK built at {apk}, size {report.summary.totalSize / 1024 / 1024} MB");
        }
    }
}
```

Keep `Build()` byte-identical to current version. Only add `BuildProfile()`.

- [ ] **Step 3: Verify compile — run EditMode tests**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults-EditMode-sp3-task1.xml \
  -logFile Logs/editmode-sp3-task1.log
```

Expected: 112 / 112 pass (no test changes yet).

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/ApkBuilder.cs
git commit -m "feat(w6-sp3): ApkBuilder.BuildProfile for dev-build APK"
```

---

## Task 2: Produce baseline profile APK + capture session

**Files (created by build + profiler):**
- `Builds/keyflow-w6-sp3-profile.apk` (not committed — `.gitignore:57 *.apk`)
- `Logs/profile-w6sp3-baseline.data` (committed as evidence artifact)

- [ ] **Step 1: Build profile APK**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -projectPath . \
  -executeMethod KeyFlow.Editor.ApkBuilder.BuildProfile \
  -quit \
  -logFile Logs/build-sp3-profile.log
```

Rules: `-executeMethod` MUST include `-quit`. Foreground only, no `run_in_background`, no stdout pipe. Expected duration 5-15 minutes. Retry once on `IPCStream (Not connected)` per W6-1 precedent.

Expected: exit 0, `Builds/keyflow-w6-sp3-profile.apk` exists (~33-40 MB with Development symbols).

- [ ] **Step 2: Verify device connected**

```bash
adb devices
```

Expect `R5CT21A31QB device`. If not present, escalate BLOCKED — user needs to plug in / authorize USB debugging.

- [ ] **Step 3: Install profile APK**

```bash
adb install -r Builds/keyflow-w6-sp3-profile.apk
```

Expect `Success`.

- [ ] **Step 4: Hand off attach + capture to the user**

Subagent MUST NOT attempt automated Unity Profiler attach — this is an interactive Editor flow. Hand a checklist to the user:

```
PROFILER CAPTURE (baseline) — user-driven

1. Open Unity Editor at this worktree's path.
2. Window → Analysis → Profiler
3. Top bar dropdown "Connected Players" → pick the device entry (should
   auto-appear because APK was built with ConnectWithProfiler).
   - If no entry: run `adb forward tcp:34999 localabstract:Unity<BundleId>`
     and retry. Bundle ID visible in Project Settings → Player → Identification.
4. Click "Record" so the red dot is active.
5. On device: launch KeyFlow → pick Entertainer → Normal. Wait for countdown.
6. At the first note, the session is in gameplay. Play the full 2 minutes.
7. On score screen, click "Record" again in Profiler to stop.
8. Profiler menu → Save → save to Logs/profile-w6sp3-baseline.data

Report back when .data is saved.
```

Subagent reports `AWAITING_USER_ACTION` and waits.

- [ ] **Step 5: Commit baseline capture**

After user confirms `.data` saved:

```bash
ls -la Logs/profile-w6sp3-baseline.data
git add Logs/profile-w6sp3-baseline.data
git commit -m "chore(w6-sp3): baseline profile capture (Entertainer Normal, 2min)"
```

Note: `.data` files are binary; size likely a few MB. If gitignore treats `Logs/` as gitignored, use `git add -f`. Check:

```bash
grep -n "^Logs\|^/Logs\|^Logs/" .gitignore 2>&1
```

If gitignored, prefer to NOT commit the binary — reference-link it in the baseline report instead, preserving evidence locally but keeping repo lean.

---

## Task 3: Write the baseline analysis report

**Files:**
- Create: `docs/superpowers/reports/2026-04-22-w6-sp3-profile-baseline.md`

- [ ] **Step 1: Open the .data in Unity Profiler**

User-driven step. Unity Editor → Profiler → Load → `Logs/profile-w6sp3-baseline.data`.

- [ ] **Step 2: Collect top-N GC allocator table**

User reads the Profiler's "GC Alloc" column in the Hierarchy view (filtered to the gameplay period — drag the highlight to cover after countdown, before score screen). Capture:
- Top 10 callsites by Total Bytes allocated during the selection
- Total `GC.Alloc` sum (bytes)
- `GC.Collect` event count (look for GC events on the timeline)
- Frame time P50 / P95 / P99 (from Overview → CPU chart)

The user passes this table back to the subagent for transcription.

- [ ] **Step 3: Write the report**

Create `docs/superpowers/reports/2026-04-22-w6-sp3-profile-baseline.md`:

```markdown
# W6-SP3 Profile Baseline Report

**Date:** 2026-04-22
**Session:** Entertainer Normal, 120 s
**Device:** Galaxy S22 (R5CT21A31QB), Android, IL2CPP ARM64
**Build:** Builds/keyflow-w6-sp3-profile.apk (Development + ConnectWithProfiler)
**Capture:** Logs/profile-w6sp3-baseline.data

## Summary

- `GC.Alloc` total over 120 s gameplay: <X> KB
- `GC.Collect` event count: <N>
- Total pause from GC.Collect: <Y> ms
- Frame time: P50 = <a> ms, P95 = <b> ms, P99 = <c> ms

## Top GC allocators (gameplay period)

| Rank | Callsite | Bytes/sec | Bytes/frame (approx) |
|------|---|---|---|
| 1 | ... | ... | ... |
| ... | ... | ... | ... |

## Scope decision — allocators to fix

- **In scope** (gameplay hot loop, under our control):
  - <file>:<line> — <brief>
- **Out of scope** (documented, will not fix this SP):
  - <third-party or Unity internal> — <reason>

## Next

Proceed to Task 4+ (one commit per allocator fix).
```

Fill in the actual numbers. The `Top GC allocators` table drives the fix-task scope.

- [ ] **Step 4: Commit the baseline report**

```bash
git add docs/superpowers/reports/2026-04-22-w6-sp3-profile-baseline.md
git commit -m "docs(w6-sp3): profile baseline report"
```

---

## Task 4: Fix HoldStateMachine.Tick — out-buffer signature (high-probability candidate)

This task assumes the baseline report (Task 3) confirmed `HoldStateMachine.Tick()` list allocation is a real hot-loop allocator. If the baseline report DOES NOT list this as an allocator (e.g., below noise floor or stripped by JIT), skip this task and note it in the completion report.

**Files:**
- Modify: `Assets/Scripts/Gameplay/HoldStateMachine.cs`
- Modify: `Assets/Scripts/Gameplay/HoldTracker.cs`
- Modify: `Assets/Tests/EditMode/HoldStateMachineTests.cs` (10 call sites + 1 semantic rewrite + 2 new tests)

TDD order: modify tests first (red), implement the signature change (green), commit.

- [ ] **Step 1: Write the new regression tests (red)**

Append to `Assets/Tests/EditMode/HoldStateMachineTests.cs` (inside the class, before the closing `}`):

```csharp
        [Test]
        public void Tick_ClearsBufferBeforeAdding()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            var buffer = new List<HoldTransition>
            {
                new HoldTransition { id = 999, newState = HoldState.Broken } // sentinel
            };

            sm.Tick(1500, new HashSet<int> { 0 }, buffer); // no transition this tick

            Assert.AreEqual(0, buffer.Count, "Tick should clear caller's buffer before adding");
        }

        [Test]
        public void Tick_PreservesBufferCapacityAcrossCalls()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            var buffer = new List<HoldTransition>(capacity: 8);
            int initialCapacity = buffer.Capacity;

            for (int i = 0; i < 5; i++)
                sm.Tick(1500, new HashSet<int> { 0 }, buffer);

            Assert.AreEqual(initialCapacity, buffer.Capacity, "Buffer capacity must not grow across steady-state ticks");
        }
```

These tests reference the NEW `Tick(int, HashSet<int>, List<HoldTransition>)` signature that doesn't yet exist.

- [ ] **Step 2: Migrate the 10 existing Tick call sites in HoldStateMachineTests.cs**

Replace each `sm.Tick(<args>)` call with the new 3-arg form. Pattern: add a local `var buffer = new List<HoldTransition>();` at the top of each test (for tests with multiple Tick calls, one buffer reused). For `TickReturnsTransitions_ForObservation` (line 100-110), rewrite semantics.

Final state of the existing 10 call sites:

```csharp
// Test: Holding_ThroughEnd_TransitionsToCompleted (was lines 27-39)
[Test]
public void Holding_ThroughEnd_TransitionsToCompleted()
{
    var sm = new HoldStateMachine();
    var id = sm.Register(0, 1000, 2000);
    sm.OnStartTapAccepted(id);
    var pressed = new HashSet<int> { 0 };
    var buffer = new List<HoldTransition>();

    sm.Tick(songTimeMs: 1500, pressedLanes: pressed, outTransitions: buffer);
    Assert.AreEqual(HoldState.Holding, sm.GetState(id));

    sm.Tick(songTimeMs: 2000, pressedLanes: pressed, outTransitions: buffer);
    Assert.AreEqual(HoldState.Completed, sm.GetState(id));
}

// Test: Holding_ReleasedEarly_TransitionsToBroken (was lines 41-50)
[Test]
public void Holding_ReleasedEarly_TransitionsToBroken()
{
    var sm = new HoldStateMachine();
    var id = sm.Register(0, 1000, 2000);
    sm.OnStartTapAccepted(id);
    var buffer = new List<HoldTransition>();

    sm.Tick(1500, new HashSet<int>(), buffer);
    Assert.AreEqual(HoldState.Broken, sm.GetState(id));
}

// Test: Spawned_Tick_RemainsSpawned (was lines 52-59)
[Test]
public void Spawned_Tick_RemainsSpawned()
{
    var sm = new HoldStateMachine();
    var id = sm.Register(0, 1000, 2000);
    sm.Tick(1500, new HashSet<int> { 0 }, new List<HoldTransition>());
    Assert.AreEqual(HoldState.Spawned, sm.GetState(id));
}

// Test: MultipleConcurrentHolds_IndependentStates (was lines 61-75)
[Test]
public void MultipleConcurrentHolds_IndependentStates()
{
    var sm = new HoldStateMachine();
    var a = sm.Register(0, 1000, 2000);
    var b = sm.Register(2, 1200, 2500);
    sm.OnStartTapAccepted(a);
    sm.OnStartTapAccepted(b);

    var pressed = new HashSet<int> { 0 };
    sm.Tick(1500, pressed, new List<HoldTransition>());

    Assert.AreEqual(HoldState.Holding, sm.GetState(a));
    Assert.AreEqual(HoldState.Broken, sm.GetState(b));
}

// Test: Completed_SubsequentTick_StaysCompleted (was lines 77-86)
[Test]
public void Completed_SubsequentTick_StaysCompleted()
{
    var sm = new HoldStateMachine();
    var id = sm.Register(0, 1000, 2000);
    sm.OnStartTapAccepted(id);
    var buffer = new List<HoldTransition>();
    sm.Tick(2000, new HashSet<int> { 0 }, buffer);
    sm.Tick(2500, new HashSet<int>(), buffer);
    Assert.AreEqual(HoldState.Completed, sm.GetState(id));
}

// Test: Broken_SubsequentTick_StaysBroken (was lines 88-97)
[Test]
public void Broken_SubsequentTick_StaysBroken()
{
    var sm = new HoldStateMachine();
    var id = sm.Register(0, 1000, 2000);
    sm.OnStartTapAccepted(id);
    var buffer = new List<HoldTransition>();
    sm.Tick(1500, new HashSet<int>(), buffer);
    sm.Tick(1800, new HashSet<int> { 0 }, buffer);
    Assert.AreEqual(HoldState.Broken, sm.GetState(id));
}

// Test: TickReturnsTransitions_ForObservation — RENAME + REWRITE (was lines 99-110)
[Test]
public void Tick_WritesTransitionsIntoProvidedBuffer()
{
    var sm = new HoldStateMachine();
    var id = sm.Register(0, 1000, 2000);
    sm.OnStartTapAccepted(id);
    var buffer = new List<HoldTransition>();

    sm.Tick(2000, new HashSet<int> { 0 }, buffer);

    Assert.AreEqual(1, buffer.Count);
    Assert.AreEqual(id, buffer[0].id);
    Assert.AreEqual(HoldState.Completed, buffer[0].newState);
}
```

- [ ] **Step 3: Run EditMode tests, expect RED (compile fail)**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults-EditMode-sp3-t4-red.xml \
  -logFile Logs/editmode-sp3-t4-red.log
```

Expected: compile failures on `Tick(...)` due to arity mismatch (existing `Tick` returns `List`, tests pass 3 args). Task 4 Step 4 below introduces the new signature.

- [ ] **Step 4: Implement the new Tick signature**

Replace the `Tick` method in `Assets/Scripts/Gameplay/HoldStateMachine.cs` (current lines 53-73):

```csharp
        public void Tick(int songTimeMs, HashSet<int> pressedLanes, List<HoldTransition> outTransitions)
        {
            outTransitions.Clear();
            foreach (var kv in entries)
            {
                var e = kv.Value;
                if (e.state != HoldState.Holding) continue;

                if (!pressedLanes.Contains(e.lane))
                {
                    e.state = HoldState.Broken;
                    outTransitions.Add(new HoldTransition { id = kv.Key, newState = HoldState.Broken });
                }
                else if (songTimeMs >= e.endMs)
                {
                    e.state = HoldState.Completed;
                    outTransitions.Add(new HoldTransition { id = kv.Key, newState = HoldState.Completed });
                }
            }
        }
```

Key changes:
- Return type `List<HoldTransition>` → `void`
- New parameter `List<HoldTransition> outTransitions`
- First statement `outTransitions.Clear()` (enforces the clear-then-write contract the tests assert)
- Removed `var transitions = new List<HoldTransition>();` — this was the hot-loop allocator

- [ ] **Step 5: Update HoldTracker.Update to supply the buffer**

Modify `Assets/Scripts/Gameplay/HoldTracker.cs`. Current Update method at lines 29-54 — replace with:

```csharp
        private readonly HashSet<int> pressed = new HashSet<int>();
        private readonly List<HoldTransition> transitionBuffer = new List<HoldTransition>();

        private void Update()
        {
            if (!audioSync.IsPlaying || audioSync.IsPaused) return;
            if (idToNote.Count == 0) return;

            pressed.Clear();
            for (int lane = 0; lane < LaneLayout.LaneCount; lane++)
                if (tapInput.IsLanePressed(lane)) pressed.Add(lane);

            stateMachine.Tick(audioSync.SongTimeMs, pressed, transitionBuffer);
            foreach (var t in transitionBuffer)
            {
                if (!idToNote.TryGetValue(t.id, out var note)) continue;

                if (t.newState == HoldState.Completed)
                {
                    note.MarkHoldCompleted();
                }
                else if (t.newState == HoldState.Broken)
                {
                    judgmentSystem.HandleHoldBreak();
                    note.MarkHoldBroken();
                }
                idToNote.Remove(t.id);
            }
        }
```

Note the two new `readonly` fields at the top — `pressed` and `transitionBuffer`. They replace the per-Update `new HashSet<int>()` and the `Tick`'s internal `new List<>`. This Task also folds in the Task 5 `HoldTracker.pressed` fix (the two changes share this file).

- [ ] **Step 6: Run EditMode tests, expect GREEN**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults-EditMode-sp3-t4-green.xml \
  -logFile Logs/editmode-sp3-t4-green.log
```

Expected: 114 / 114 pass (112 pre-existing — but now 7 of them have been rewritten mechanically; 10 original Tick sites + 2 new = 12 test methods in HoldStateMachineTests.cs) + unchanged other tests. If count shows less than 114, one of the new tests or a rewrite is broken.

Actually — verify exact count by parsing the XML:

```bash
grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' TestResults-EditMode-sp3-t4-green.xml | head -1
```

Expected: `total="114" passed="114" failed="0"`.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Gameplay/HoldStateMachine.cs \
        Assets/Scripts/Gameplay/HoldTracker.cs \
        Assets/Tests/EditMode/HoldStateMachineTests.cs
git commit -m "$(cat <<'EOF'
perf(w6-sp3): reusable buffers in HoldStateMachine + HoldTracker

Eliminates two W4-carry-over #1 allocators:
- HoldStateMachine.Tick: returned fresh List<HoldTransition> every
  call → now writes into caller-supplied buffer (Option X ownership).
- HoldTracker.Update: allocated fresh HashSet<int> every frame → now
  reuses a readonly field, clear-then-fill.

Test impact: 10 existing HoldStateMachineTests Tick call sites
migrated to 3-arg signature; TickReturnsTransitions_ForObservation
renamed and rewritten to assert on provided buffer. +2 new tests
(Tick_ClearsBufferBeforeAdding, Tick_PreservesBufferCapacityAcrossCalls).
Net EditMode: 112 → 114, all green.

Baseline bytes/sec numbers for each allocator are in
docs/superpowers/reports/2026-04-22-w6-sp3-profile-baseline.md.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5+: Fix additional allocators identified by baseline

The baseline report (Task 3) may reveal allocators beyond the two in Task 4. For each additional allocator:

- [ ] **Step N.1: Identify the callsite**

From the baseline top-N table. Expect one of:
- Another list/set allocation inside `Update`/`FixedUpdate`/`LateUpdate`
- A LINQ chain (`.Where(...).Select(...)`)
- A `string.Format` / `string +` concat in a UI text update
- A `new Dictionary<...>()` in a per-frame path
- Method group → delegate allocation (`() => { ... }` captured in per-frame callback)

- [ ] **Step N.2: Apply the appropriate pattern**

- **Reusable field + Clear()** → for containers
- **String caching** / `int.ToString()` avoidance → for text updates
- **Cached delegate** → for lambda capture; promote to `private readonly` method group
- **Manual loop** → replace LINQ

- [ ] **Step N.3: Run EditMode tests (no regression)**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults-EditMode-sp3-t<N>.xml \
  -logFile Logs/editmode-sp3-t<N>.log
```

Expected: 114 / 114.

- [ ] **Step N.4: Commit**

One commit per fix. Commit message: `perf(w6-sp3): <file>: <short>`.

---

## Task N+1: Re-profile to verify GC Collect = 0

**Files (created):**
- `Logs/profile-w6sp3-after.data` (committed as evidence, same policy as baseline)

- [ ] **Step 1: Rebuild profile APK**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -projectPath . \
  -executeMethod KeyFlow.Editor.ApkBuilder.BuildProfile \
  -quit \
  -logFile Logs/build-sp3-profile-after.log
```

- [ ] **Step 2: Reinstall on device**

```bash
adb install -r Builds/keyflow-w6-sp3-profile.apk
```

- [ ] **Step 3: Hand off re-capture to user**

Same protocol as Task 2 Step 4. Save capture to `Logs/profile-w6sp3-after.data`.

- [ ] **Step 4: User verifies GC Collect count in Profiler**

Have the user:
- Load `Logs/profile-w6sp3-after.data` in Unity Profiler
- Select gameplay period (after countdown, before score screen)
- Verify **no GC.Collect events** on the timeline for the selected range
- Note the residual `GC.Alloc bytes/sec` (should be near zero; any residual is either third-party or Unity internal)

- [ ] **Step 5: Append "After" section to baseline report**

Edit `docs/superpowers/reports/2026-04-22-w6-sp3-profile-baseline.md`. Append:

```markdown
## After — profile with SP3 fixes applied

**Capture:** Logs/profile-w6sp3-after.data

- `GC.Alloc` total over 120 s gameplay: <X> KB (baseline <Y> KB)
- `GC.Collect` event count: <N> (baseline was <M>)
- Total pause from GC.Collect: <Y> ms (baseline <Z> ms)
- Frame time: P50 = <a> ms, P95 = <b> ms, P99 = <c> ms

## Residual allocators (if any)

| Callsite | Bytes/sec | Rationale (why not fixed) |
|---|---|---|
| ... | ... | third-party / Unity internal |
```

- [ ] **Step 6: Commit**

```bash
git add docs/superpowers/reports/2026-04-22-w6-sp3-profile-baseline.md
git commit -m "docs(w6-sp3): profile after-fix capture — GC.Collect=<N>"
```

(Replace `<N>` with actual count. Target is 0. If not 0, diagnose and loop back to Task 5.)

---

## Task N+2: Release-path regression playtest

**Files (output):**
- `Builds/keyflow-w6-sp2.apk` (rebuilt — not committed)

- [ ] **Step 1: Rebuild release APK**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -projectPath . \
  -executeMethod KeyFlow.Editor.ApkBuilder.Build \
  -quit \
  -logFile Logs/build-sp3-release.log
```

- [ ] **Step 2: Install**

```bash
adb install -r Builds/keyflow-w6-sp2.apk
```

- [ ] **Step 3: Hand off device playtest to user**

```
RELEASE REGRESSION PLAYTEST (user)

On the S22, play Entertainer Normal end-to-end. Verify:
- No visible tap drops (the W4 symptom should be gone)
- No visible frame hitches
- No crash / ANR
- Main screen still shows 4 songs with correct thumbs

Report back: "문제 없다" or specific issue.
```

- [ ] **Step 4: No commit needed** (APK is gitignored; playtest result goes into completion report)

---

## Task N+3: Completion report

**Files:**
- Create: `docs/superpowers/reports/2026-04-22-w6-sp3-completion.md`

- [ ] **Step 1: Write the completion report**

Follow the pattern of `docs/superpowers/reports/2026-04-22-w6-sp2-four-songs-completion.md`:

- Scope delivered
- Baseline → after GC numbers (headline)
- Commit list (oldest → newest)
- Deviations from plan
- Operational findings (IL2CPP flakiness, profiler attach issues, third-party residual allocators)
- Device validation confirmation
- Carry-over items (profile-ID artifact policy, any allocator-not-fixed items)
- Next steps → W6 #4-6 + Canon

- [ ] **Step 2: Commit**

```bash
git add docs/superpowers/reports/2026-04-22-w6-sp3-completion.md
git commit -m "docs(w6-sp3): completion report"
```

- [ ] **Step 3: Hand off to finishing-a-development-branch skill**

---

## Risks recap (from spec §9)

- **R1 (Medium)**: Profiler attach failure → Step 4 of Task 2 has fallback protocol (WiFi, manual `adb forward`).
- **R4 (Medium)**: Fix introduces behavior regression → 114 EditMode tests + release playtest catch.
- **R5 (Low)**: Third-party allocator dominates → documented in "Residual allocators" table, scope-out.
- **R7 (Low)**: ApkBuilder.BuildProfile accidentally breaks release Build → kept as sibling method; Task 1 Step 3 verifies EditMode still green after ApkBuilder edit.

## Non-obvious constraints

- Unity batch mode: foreground only, no `run_in_background`, no piped stdout (persistent memory).
- `-runTests` MUST omit `-quit`; `-executeMethod` MUST include `-quit`.
- Profiler Deep mode NOT used — standard sampling preserves GC pattern fidelity.
- `.data` profile captures are binary; committing them is OK if Logs/ isn't gitignored, otherwise reference-link them from the report.
- Keep `ApkBuilder.Build()` byte-identical; only extend via `BuildProfile()`.
- `HoldStateMachine.Tick` has exactly 1 caller in production (`HoldTracker.Update`) but 10 callers in EditMode tests — migration is bounded and explicit.
