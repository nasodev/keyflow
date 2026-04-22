# W6-SP3 Profile Baseline Report

**Date:** 2026-04-22
**Session:** Entertainer Normal, ~2 min gameplay
**Device:** Galaxy S22 (R5CT21A31QB, samsung_SM-S901N), Android, IL2CPP ARM64
**Build:** `Builds/keyflow-w6-sp3-profile.apk` (74.64 MB, Development + ConnectWithProfiler + AllowDebugging)
**Capture:** `Logs/profile-w6sp3-baseline.data` (63.7 MB, local only — `Logs/` gitignored)
**Target Frame Time:** 60 FPS
**Analyzer:** Unity 6000.3.13f1 Editor, Profiler window, Hierarchy view filtered to a mid-gameplay frame

## Summary

**Primary finding — W4 carry-over #1 hypothesis refuted by data.**

The W4 carry-over memo suspected `HoldTracker.Update` (per-frame `new HashSet<int>()`) and `HoldStateMachine.Tick` (per-tick `new List<HoldTransition>`) as the gameplay-loop allocators responsible for "mid-game tap drops." On The Entertainer Normal this hypothesis is **wrong** — both allocators report **0 B / frame** because the chart contains no hold notes (ragtime is all staccato taps), so `HoldTracker.Update` early-returns at the `if (idToNote.Count == 0) return;` guard before reaching the allocation site.

**The actual sole gameplay allocator: `LatencyMeter.Update()`**, a W1 PoC debug HUD overlay that shipped into the release scene inadvertently.

| Metric | Value |
|---|---|
| Total `GC.Alloc` per gameplay frame (PlayerLoop) | **1.1 KB** |
| `LatencyMeter.Update()` share of that total | **1.1 KB (100 %)** |
| `LatencyMeter.Update()` internal `GC.Alloc` calls / frame | 20 |
| `HoldTracker.Update()` allocation / frame | 0 B |
| `HoldStateMachine.Tick()` allocation / frame | 0 B (not reached — no holds) |
| `TapInputHandler.Update()` allocation / frame | 0 B |
| `NoteSpawner.Update()` allocation / frame | 0 B |
| `GameplayController.Update()` allocation / frame | 0 B |
| CPU time / frame (steady state) | ~16 ms (steady 60 FPS) |

Steady-state allocation rate: 1.1 KB/frame × 60 FPS = **66 KB/sec**, ~8 MB over a 2-minute session, triggering multiple GC cycles — consistent with the W3 sign-off's "transient, recoverable" mid-game tap drops.

## Top GC allocators (gameplay period, single representative mid-frame)

Data captured from Profiler Hierarchy view at Frame 1629 / 2000, sorted by GC Alloc descending:

| Rank | Callsite | GC Alloc | Calls | Time ms |
|------|---|---|---|---|
| 1 | `Update.ScriptRunBehaviourUpdate → BehaviourUpdate → LatencyMeter.Update() [Invoke]` | 1.1 KB | 1 | 0.26 |
| 1a | └─ `GC.Alloc` (nested inside LatencyMeter) | 1.1 KB | 20 | 0.00 |
| 2+ | All other callsites | 0 B | — | — |

Every other callsite — `HoldTracker`, `HoldStateMachine`, `TapInputHandler`, `NoteSpawner`, `GameplayController`, all `PostLateUpdate.*` entries — reports 0 B allocation. `PlayerLoop.PostLateUpdate.PlayerUpdateCanvases` at 1.4 % CPU but 0 B GC Alloc (UGUI rebuild cost, not allocation).

## Root-cause analysis — why LatencyMeter allocates 1.1 KB/frame

`Assets/Scripts/UI/LatencyMeter.cs:49-88`, `Update()` method. Every frame:

```csharp
string scoreLine = $"Score: {s.Score:N0}  Stars: {s.Stars}";        // alloc: string.Format + boxing
string comboLine = $"Combo: {s.Combo}  Max: {s.MaxCombo}";          // alloc
string judgLine  = $"Last: {judgmentSystem.LastJudgment}  (Δ {judgmentSystem.LastDeltaMs} ms)"; // alloc

hudText.text =                                                       // alloc: 7-line concat
    $"FPS: {fpsDisplay:F1}\n" +
    $"{scoreLine}\n" +
    $"{comboLine}\n" +
    $"{judgLine}\n" +
    $"dspTime drift: {driftMs:F1} ms\n" +
    $"Song time: {(audioSync != null ? audioSync.SongTimeMs : 0)} ms\n" +
    $"Buffer: {AudioSettings.GetConfiguration().dspBufferSize} samples";
```

- 3 intermediate `$""` interpolations
- 7-segment `$""` concatenation assigned to `hudText.text`
- `int.ToString("N0")`, `float.ToString("F1")` for formatting — each boxes + allocates
- Total: ~20 internal `GC.Alloc` calls producing 1.1 KB per frame

LatencyMeter combines two roles in the current scene:
1. **Dev telemetry**: FPS, dsp-time drift, audio buffer size (W1 PoC carry-over)
2. **Gameplay HUD**: Score, Combo, Max Combo, Last judgment — currently the only in-game score display

Because it serves as the live score display, it cannot simply be removed from the release build — it would leave the player with no visual feedback during play. The fix must preserve user-visible HUD output while eliminating per-frame allocation.

## Scope decision — allocators to fix

**In scope** (gameplay hot loop, under our control):

- **`LatencyMeter.Update()`** — PRIMARY target. GC-free rewrite using `StringBuilder` + throttled update (user decision: do not strip from release; preserve HUD functionality).
- **`HoldStateMachine.Tick()`** — DEFENSE-IN-DEPTH. Allocator does not fire on The Entertainer (no holds) but IS active on Für Elise and any future chart with hold notes. Fix is the signature change planned in `docs/superpowers/plans/2026-04-22-keyflow-w6-sp3-profiler-pass.md` Task 4. Still worth fixing: (a) latent bug on hold-bearing charts, (b) test migration was pre-scoped, (c) no additional test churn.
- **`HoldTracker.Update()`** — DEFENSE-IN-DEPTH, same rationale. Folded into Task 4 atomically.

**Out of scope** (documented, will not fix this SP):

- `Update.ScriptRunBehaviourUpdate` at 3.9 % CPU — normal Unity overhead, not allocating.
- `PostLateUpdate.FinishFrameRendering` at 7.2 % CPU — Unity rendering path, not allocating.
- `PlayerUpdateCanvases` at 1.4 % — UGUI layout, 0 B allocation.
- Third-party / Unity internals — none identified as top allocator.

## Plan adjustment (vs. `docs/superpowers/plans/2026-04-22-keyflow-w6-sp3-profiler-pass.md`)

- **Task 4 (HoldStateMachine + HoldTracker)**: execute as planned. Reframed from "primary fix" to "defense-in-depth fix" since data shows these don't fire on The Entertainer. Still reduces latent allocation on hold-bearing charts.
- **Task 5 (originally "open-ended, data-driven")**: concretized to **LatencyMeter GC-free rewrite with StringBuilder + throttle**. Design:
  - `StringBuilder` field, `.Clear()` + `.Append*()` at update time
  - Throttle `hudText.text` assignment to ~4 Hz (0.25 s interval) — cuts even residual UGUI text re-upload cost
  - Number formatting via `StringBuilder.Append(int)` / manual formatting (no `ToString("N0")` boxing)
- **Task N+1 (re-profile)**: acceptance unchanged — GC.Collect count == 0 during re-captured session.

## Next

Proceed to Task 4 (defense-in-depth) then Task 5 (LatencyMeter primary fix).

Tasks 4 and 5 are independent in terms of files touched (4: Gameplay/*; 5: UI/LatencyMeter.cs + tests) but share the implementation session. Either order works; Task 4 first is convenient because it also exercises the test migration path planned in the spec.
