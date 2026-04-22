# KeyFlow W6 Sub-Project 3 Design — Profiler Pass (GC-Free Gameplay Loop)

**Date:** 2026-04-22
**Branch:** `claude/pensive-turing-5827ba` (worktree)
**Parent MVP spec:** `docs/superpowers/specs/2026-04-19-keyflow-mvp-design.md`
**Depends on:** W6 SP2 4-song content pack (merged `b66c7cc`) — Entertainer Normal (481 notes) is the stress fixture
**Carry-over resolved:** W4 carry-over #1 (mid-game tap drops on device)
**Scope priority:** W6 #3 (profiler) — #4–6 deferred to later sub-projects

---

## 1. Goal

W3 sign-off flagged transient *"mid-game tap drops"* on device (W4 carry-over #1). Candidates suspected: per-frame `new HashSet<int>()` in `HoldTracker.Update`, per-tick `new List<HoldTransition>` in `HoldStateMachine.Tick`. Suspicion was never profiled.

This sub-project performs a **device-attached Unity Profiler pass** against the highest-density chart shipped (Entertainer Normal, 481 notes) to:

1. **Validate** the W4 suspicion with evidence — are these the allocators responsible, or are there others?
2. **Eliminate** every gameplay-loop GC allocator identified (target: zero `GC.Collect` calls during a 2-minute session).
3. **Guard** against regression with +2 EditMode tests around the state-machine contract that gets reshaped.

**Quantitative success criterion**: `GC.Collect` count == 0 during a 2-minute Entertainer Normal session captured via Unity Profiler attached to Galaxy S22.

---

## 2. Scope

### In scope
- Device profiler attach (Galaxy S22 via USB)
- Separate Development Build APK for profiler symbols
- Baseline profile capture + analysis report
- Fix every `GC.Alloc` callsite in gameplay hot path (`Assets/Scripts/Gameplay/*`) revealed by profile data
- `HoldStateMachine.Tick()` signature change from returning a `List<HoldTransition>` to writing into a caller-supplied buffer (Option X from Section 4 — explicit ownership)
- +2 EditMode regression tests around the reshaped signature
- Re-profile to verify GC Collect count = 0
- Final release APK device playtest (regression check — Entertainer Normal completes cleanly)

### Out of scope
- Frame time / render optimization (separate concern; GC-free is a necessary-but-not-sufficient condition for steady frame time)
- UI / Canvas rebuild cost (gameplay canvas is static; main-menu scroll is separate and not hot)
- Audio DSP latency (W1 PoC territory)
- Third-party Unity internal allocators (TextMesh Pro, UGUI layout, input pipeline) — cannot modify; will document if dominant and re-negotiate the goal
- 2nd-device profiling (W6 #6 — after mid-tier Android procurement)
- Canon in D content (deferred self-sequencing SP)
- `EditMode` `GC.GetTotalAllocatedBytes` assertion tests — Editor mono GC ≠ Android IL2CPP GC; device measurement is the ground truth. Deferred.

---

## 3. Approach

**Profiler-first, evidence-based fix** (rejected alternatives: blind fix of the 2 memory candidates; EditMode-only GC assertions).

```
[1] Baseline capture  →  [2] Identify top-N allocators  →  [3] Fix each  →  [4] Re-capture  →  [5] Verify GC Collect = 0
```

If re-capture shows residual GC activity, loop back to [2] (top-N of the residual).

Rejected alternatives:
- **A. Fix 2 memory candidates only** — fast but no evidence, could miss other allocators or fix the wrong thing.
- **C. EditMode `GC.GetTotalAllocatedBytes` tests** — proxy measurement against the wrong GC runtime (mono vs IL2CPP); cannot catch platform-specific allocators.

---

## 4. Measurement setup

### Build variant

- **`Builds/keyflow-w6-sp3-profile.apk`** — separate Development Build APK.
- Unity Editor setting: `Development Build = ON`, `Autoconnect Profiler = ON`.
- Not committed (`.gitignore:57 *.apk`). Lives at `Builds/` for duration of the session.
- Does NOT affect release pipeline. The existing release-path ApkBuilder stays as-is; we add a second menu item / `-executeMethod` entry (`ApkBuilder.BuildProfile`) that flips the Development Build flag.

### Attach protocol

1. S22 connected via USB, `adb devices` lists `R5CT21A31QB`.
2. Launch the app on device.
3. Unity Editor → `Window → Analysis → Profiler` → "Connected Players" dropdown → select device entry.
4. If "Connected Players" successfully lists the device, skip the `adb forward` step entirely. Only fall back to manual forwarding (`adb forward tcp:34999 localabstract:Unity<BundleId>`) if auto-detection fails — the exact local-abstract name is derived from the Unity bundle identifier and the implementer will read it from Unity's attach error message.
5. Once "Recording" is visible in Profiler header, proceed.

### Capture protocol

1. In-app: Main screen → select Entertainer → tap Normal.
2. Wait for countdown → at "Go!", start Profiler Record.
3. Play through the full 120-second chart. Do not pause.
4. On completion (score screen), stop Profiler Record.
5. `Profiler → Save → Logs/profile-w6sp3-baseline.data`.

### Metrics collected
- **Primary**: `GC.Alloc` samples per callsite (bytes/sec, counts/sec). `GC.Collect` count.
- **Secondary**: CPU time under `Update()`, `FixedUpdate()`, `LateUpdate()`. Script scope only.
- **Tertiary** (context only): Frame time histogram (P50 / P95 / P99).

---

## 5. Known candidates + discovery

### Confirmed candidates (W4 carry-over #1, code-verified)

| File:line | Code | Frequency |
|---|---|---|
| `HoldTracker.Update():34` | `var pressed = new HashSet<int>();` | Every frame when `idToNote.Count > 0` |
| `HoldStateMachine.Tick():55` | `var transitions = new List<HoldTransition>();` | Every frame when called (currently every `HoldTracker.Update`) |

### Cleared by inspection (no fix needed)
- `JudgmentSystem.pending` — `private readonly List<NoteController>`, no `new` in hot path
- `NoteSpawner.liveNotes` — same
- `TapInputHandler.raycastResults` — `private static readonly List<RaycastResult>`, reused across taps

### Hypotheses (informational only — let profile data drive)

Not "suspected allocators" — these are untested guesses that should be ignored unless the profile shows allocation:

- `AudioSamplePool.PlayForPitch` — assignment + native `src.Play()`; likely clean.
- `NoteController.Update` — Vector3 / Transform math; likely clean.
- Any `string.Format` / boxing in UI text updates under gameplay canvas.

These notes exist only as context for the implementer when reading the profile — NOT as candidates to pre-fix. Data is king.

### Discovery rule
Every callsite in the profile with `bytes/sec > 0` that is (a) in the gameplay hot loop and (b) under our control (i.e., in `Assets/Scripts/*`, not a Unity internal) is added to the fix list.

Exemptions (not in fix list, acknowledged in report):
- Scene load / `Start` / `Awake` / one-time initialization paths
- Third-party / Unity internals (UGUI layout, TMP, input pipeline, WebRequest)
- Debug-only code paths that are stripped in release build (`UNITY_EDITOR`, `DEVELOPMENT_BUILD`)

---

## 6. Fix pattern

### Pattern A: reusable field + `Clear()` at Update top

**Before** (per-frame alloc):
```csharp
private void Update()
{
    var pressed = new HashSet<int>();
    for (int lane = 0; lane < LaneLayout.LaneCount; lane++)
        if (tapInput.IsLanePressed(lane)) pressed.Add(lane);
    // ...
}
```

**After** (single alloc at construction, reused):
```csharp
private readonly HashSet<int> pressed = new HashSet<int>();

private void Update()
{
    pressed.Clear();   // O(size), no alloc
    for (int lane = 0; lane < LaneLayout.LaneCount; lane++)
        if (tapInput.IsLanePressed(lane)) pressed.Add(lane);
    // ...
}
```

### Pattern B: buffer as out-parameter (Option X from brainstorming)

For `HoldStateMachine.Tick()` — currently returns a freshly-allocated `List<HoldTransition>`, caller foreaches it.

**Before**:
```csharp
public List<HoldTransition> Tick(int songTimeMs, HashSet<int> pressedLanes)
{
    var transitions = new List<HoldTransition>();
    // ... fill ...
    return transitions;
}
```

**After**:
```csharp
public void Tick(int songTimeMs, HashSet<int> pressedLanes, List<HoldTransition> outTransitions)
{
    outTransitions.Clear();
    // ... fill outTransitions.Add(...) ...
}
```

Caller (`HoldTracker.Update`):
```csharp
private readonly List<HoldTransition> transitionBuffer = new List<HoldTransition>();
// ...
stateMachine.Tick(audioSync.SongTimeMs, pressed, transitionBuffer);
foreach (var t in transitionBuffer) { /* ... */ }
```

**Ownership**: caller owns the buffer. `HoldStateMachine` is stateful but the Tick's output is ephemeral — no stale-view risk across frames because the caller clears at the start of the next Tick call.

### Alternative considered (Option Y) — rejected

`HoldStateMachine` could hold the list as a private field with a read-only property (`IReadOnlyList<HoldTransition> LastTransitions`). Rejected because it imposes an implicit "caller must iterate before the next Tick" invariant — unit-testable but fragile and less explicit than Option X.

### Pattern C: other patterns as discovered
- **Pooling** — if profile reveals per-note allocations, object-pool NoteController/transition objects. Deferred unless needed.
- **Struct conversion** — if a class is alloc-heavy and small, convert to struct. Deferred unless needed.
- **Avoid LINQ in hot path** — LINQ `Where/Select` allocate enumerators/closures. If found, rewrite with manual loops.

### Fix application
- **Separate commit per allocator** (or per file, when a file has 2+ allocators with the same fix pattern)
- **Commit message records profiler numbers**: before/after bytes/sec for that callsite
- **Field naming**: keep the original local variable name (`pressed`, `transitions`, etc.) so `git diff` reads as locality-preserved

---

## 7. Verification

### 7.1 Device re-profile (primary)

1. Rebuild `Builds/keyflow-w6-sp3-profile.apk` with fixes applied.
2. Reinstall on S22 (`adb install -r`).
3. Reattach Profiler, capture second 2-minute Entertainer Normal session → `Logs/profile-w6sp3-after.data`.
4. **Pass criterion**: `GC.Collect` count in the captured session == 0 (Unity Profiler "GC Alloc" track shows zero `Collect` events on the timeline during gameplay).
5. **Secondary**: `GC.Alloc bytes/sec` summed over the gameplay period should be ≥ 95% lower than baseline. If not 100% zero but close, the report explains the residual (typically third-party / Unity internal).

### 7.2 EditMode tests — existing migration + 2 new

The `HoldStateMachine.Tick` signature change breaks compile on **10 existing call sites** in `Assets/Tests/EditMode/HoldStateMachineTests.cs` (lines 34, 37, 48, 57, 71, 83, 84, 94, 95, 105). All 10 must be migrated to pass a buffer argument.

**Migration breakdown**:
- **9 call sites** (lines 34, 37, 48, 57, 71, 83, 84, 94, 95) discard the return value in current code — mechanical migration: add a shared `List<HoldTransition> transitions = new();` at fixture level or per-test local, pass as third arg, continue asserting post-state as before.
- **1 call site** (line 105, `TickReturnsTransitions_ForObservation`) captures the return value and asserts on it. Must be semantically rewritten to assert against the supplied buffer. Test name should be updated to `Tick_WritesTransitionsIntoProvidedBuffer` for accuracy.

**+2 new tests** on top of the migration:

**Test N+1**: `HoldStateMachineTests.Tick_ClearsBufferBeforeAdding`
- Pre-fill the buffer with a sentinel `HoldTransition`. Call `Tick` against a state where no transitions will occur.
- Assert buffer is empty on return (caller's previous content was cleared, not appended to).

**Test N+2**: `HoldStateMachineTests.Tick_PreservesBufferCapacityAcrossCalls`
- Call `Tick` repeatedly with the same buffer; assert no allocation between calls (buffer capacity stable after first growth). Use `GC.GetTotalAllocatedBytes(true)` diff — a narrow guard, not a general perf test.

**Test count**: 112 → 114 (no tests removed; 10 rewrites + 2 additions, all passing).

### 7.3 Release-path regression playtest

APK artifact naming for this SP is fixed:
- **Release APK name: `keyflow-w6-sp2.apk`** — UNCHANGED from SP2. The gameplay-loop fix does not ship new content or features user-facing; the SP2 artifact name stays stable. `ApkBuilder.Build` is not modified in this sub-project.
- **Profile APK name: `keyflow-w6-sp3-profile.apk`** — new. Produced by a new sibling method `ApkBuilder.BuildProfile` (see §10 C2). This variant has Development Build + Autoconnect Profiler enabled.

After Profile build is validated:
1. Rebuild the release APK via existing `ApkBuilder.Build` → `Builds/keyflow-w6-sp2.apk`.
2. `adb install -r Builds/keyflow-w6-sp2.apk`, device playtest: Entertainer Normal completes cleanly, no visible hitches. User-reported "드롭 사라짐" OK.

### 7.4 Regression safety net
- 112 pre-existing EditMode tests stay green (any that fail = behavior-changing fix, needs investigation).
- Python pytest 37 / 37 unchanged (no Python code touched).

---

## 8. Acceptance criteria

- [ ] Profile-variant APK produced (`Builds/keyflow-w6-sp3-profile.apk` or equivalent)
- [ ] Baseline capture saved to `Logs/profile-w6sp3-baseline.data`
- [ ] Baseline report created (`docs/superpowers/reports/2026-04-XX-w6-sp3-profile-baseline.md`) listing top-N GC allocators
- [ ] Every allocator in scope fixed (separate commit per allocator or per file)
- [ ] +2 EditMode tests written, both passing
- [ ] Total EditMode count 114 / 114 green
- [ ] After capture saved to `Logs/profile-w6sp3-after.data`
- [ ] GC.Collect count = 0 in after capture (gameplay period)
- [ ] Profile report appended with "After" metrics
- [ ] Release APK regression: Entertainer Normal completes, no crash
- [ ] Python pytest 37 / 37 unchanged

---

## 9. Risks

| # | Risk | Probability | Mitigation |
|---|---|---|---|
| R1 | Profiler attach fails (USB/network/permission) | Medium | Alt: WiFi attach; fallback: on-device Development Build overlay (touch toggle) |
| R2 | Dev Build perf differs from Release | Medium | GC alloc pattern is identical across build variants; release regression playtest confirms no behavior drift |
| R3 | Profiler observer effect (Heisenbug) | Low | Unity Profiler is sampling-based; Deep Profiling NOT used |
| R4 | Fix introduces behavior regression | Medium | 112 pre-existing EditMode + 2 new tests + release playtest |
| R5 | Third-party allocator dominates (e.g., UGUI layout) | Low | Scope-out; document in report; re-negotiate acceptance if critical |
| R6 | Session interrupted mid-capture (user error) | Low | Retry protocol; secondary Editor play-mode session if device unavailable |
| R7 | ApkBuilder.BuildProfile integration accidentally breaks release Build | Low | Separate method, separate menu item, separate `-executeMethod` entry; release path stays byte-identical |

---

## 10. Commit plan

- C1: `docs(w6-sp3): profiler pass design spec` (this doc)
- C2: `feat(w6-sp3): ApkBuilder.BuildProfile for development-build APK`
- C3: `docs(w6-sp3): profile baseline report` (captures baseline; filled with measured top-N table)
- C4-CN: `perf(w6-sp3): <file>: reusable buffer for <hot-loop alloc>` (one per file)
- C_final: `docs(w6-sp3): profile after capture + completion report`

Expected total: **6–10 commits** (dependent on number of allocators found).
