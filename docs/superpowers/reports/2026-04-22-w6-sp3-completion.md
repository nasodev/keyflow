# W6 Sub-Project 3 Completion Report — Profiler Pass (GC-Free Gameplay)

**Date:** 2026-04-22
**Branch:** `claude/pensive-turing-5827ba` (worktree — merge to `main` pending)
**HEAD (pre-merge):** `5af0a7c`
**Release APK:** `Builds/keyflow-w6-sp2.apk` (33.70 MB, **not committed** per `.gitignore:57 *.apk`)

## Scope delivered

- **Root cause identified via device-attached Unity Profiler** — not where W4 carry-over #1 memo hypothesized.
  - W4 memo's suspected allocators (`HoldTracker.Update` HashSet, `HoldStateMachine.Tick` List): **0 B / frame** on The Entertainer Normal (chart has no hold notes → early-return in HoldTracker.Update, never reached the alloc site).
  - Actual sole gameplay allocator: **`LatencyMeter.Update()`** — a W1 PoC debug HUD (`Assets/Scripts/UI/LatencyMeter.cs`) shipped into the release gameplay scene, allocating 1.1 KB per frame via per-frame `string.Format`/boxing/concatenation for the on-screen FPS/Score/Combo/Judgment text. That's 66 KB/sec, ~8 MB across a 2-min session — consistent with W3 sign-off's "transient, recoverable" tap drops.
- **`LatencyMeter.Update` GC-free rewrite** (commit `b03855e`) — single decisive fix:
  - Reusable `StringBuilder(256)` field reset via `sb.Length = 0`
  - HUD text update throttled to 2 Hz (`HUD_UPDATE_INTERVAL_SEC = 0.5f`) — FPS accumulation still per-frame
  - `JudgmentName(Judgment)` switch returning interned constants — replaces `Enum.ToString()` boxing
  - `AppendOneDecimal(StringBuilder, float)` helper — alloc-free "N.N" formatting
  - Unity IL2CPP no-alloc path for `StringBuilder.Append(int)` / `.Append(long)`
  - Dropped `:N0` thousands separators on Score (MVP simplification; `1234567` vs `1,234,567`)
  - Residual: `sb.ToString()` on `Text.text` setter still allocates ~200-400 B per HUD update (unavoidable with UGUI `Text` — would require TextMeshPro migration to fully eliminate). At 2 Hz = ~600 B/sec aggregate, below Unity's GC threshold over 2-min sessions.
- **`HoldStateMachine` + `HoldTracker` buffer reuse** (commit `04900a5`) — defense-in-depth fix for hold-bearing charts:
  - `HoldStateMachine.Tick` signature: `List<HoldTransition>` return → `void` with caller-supplied `List<HoldTransition> outTransitions` (Option X from spec §6 Pattern B). First statement enforces `outTransitions.Clear()` contract.
  - `HoldTracker.Update` now holds `readonly HashSet<int> pressed` + `readonly List<HoldTransition> transitionBuffer` fields; per-frame clear-then-fill replaces per-frame `new`.
  - Migrated 10 existing `sm.Tick(...)` call sites in `HoldStateMachineTests.cs`; `TickReturnsTransitions_ForObservation` renamed to `Tick_WritesTransitionsIntoProvidedBuffer` with semantic rewrite.
  - +2 new tests: `Tick_ClearsBufferBeforeAdding` (sentinel removed on call), `Tick_PreservesBufferCapacityAcrossCalls` (no capacity growth at steady state).
- **`ApkBuilder.BuildProfile` sibling method** (commit `f3ea489`) — produces `Builds/keyflow-w6-sp3-profile.apk` with `Development | ConnectWithProfiler | AllowDebugging`. Release path `ApkBuilder.Build` unchanged, still outputs `Builds/keyflow-w6-sp2.apk`.
- **Baseline + after profile reports** (`docs/superpowers/reports/2026-04-22-w6-sp3-profile-baseline.md`) — top-N allocator tables with before/after comparison.

## Device validation — **DONE**

Release APK `Builds/keyflow-w6-sp2.apk` installed on Galaxy S22 (`R5CT21A31QB`) via `adb install -r` (replaces Development Build profile APK). User-performed playtest covered Entertainer Normal full 2-min run + Für Elise Normal partial run (hold-note regression exercise).

- [x] App launches, no ANR, main screen shows 4 song cards.
- [x] Entertainer Normal completes end-to-end, no crash / freeze / hitch.
- [x] Für Elise Normal hold-note paths exercised without regression.
- [x] HUD text updates visibly at 2 Hz, score/combo display correct.
- [x] No "tap drop" symptom observed.

User confirmation: "크래시·히치·드롭 없음, SP3 목표 달성."

### Observed but out of scope

Hold-note audio behavior: **holding a key sounds identical to a single tap**. Current implementation triggers `src.Play()` once on tap; natural Salamander sample release (~44s) plays out regardless of hold duration. No hold-specific sustain / release / damp logic. This is a **pre-existing missing feature** (not a regression — same since W6-SP1 shipped multi-pitch samples) outside W6-SP3 scope. Logged as carry-over.

## Profile results — before/after

| Metric | Baseline | After fix | Delta |
|---|---|---|---|
| `GC.Alloc` per gameplay frame (PlayerLoop) | **1.1 KB** | **0 B** | −100 % |
| `LatencyMeter.Update` per-frame alloc | 1.1 KB | 0 B | −100 % |
| Alloc rate (60 FPS) | 66 KB/sec | 0 B/sec (visible) | — |
| `GC.Collect` event count across 2-min session | > 0 (multiple) | **0** | ✅ spec §1 success criterion |
| CPU / frame (steady state) | ~16.84 ms | ~16.68 ms | unchanged (within noise) |
| `HoldTracker.Update` alloc | 0 B (early return) | 0 B | = (no holds in chart) |

Profile captures (local only, `Logs/` gitignored):
- `Logs/profile-w6sp3-baseline.data` (63.7 MB)
- `Logs/profile-w6sp3-after.data` (61.0 MB)

## Test counts

- **Python pytest**: 37 / 37 unchanged (no Python touched).
- **Unity EditMode**: **112 → 114** green. New: `Tick_ClearsBufferBeforeAdding`, `Tick_PreservesBufferCapacityAcrossCalls`. 10 existing `HoldStateMachineTests.Tick(...)` call sites migrated in place; `TickReturnsTransitions_ForObservation` renamed to `Tick_WritesTransitionsIntoProvidedBuffer`. Net +2, no tests removed.

## Commits (oldest → newest, 9 total on branch)

```
6ff7b43 docs(w6-sp3): profiler pass design spec
036c553 docs(w6-sp3): apply spec review advisories
c61f7ea docs(w6-sp3): implementation plan for profiler pass
cecf04c docs(w6-sp3): apply plan review advisories
f3ea489 feat(w6-sp3): ApkBuilder.BuildProfile for dev-build APK
2807990 docs(w6-sp3): profile baseline report — LatencyMeter is sole allocator
04900a5 perf(w6-sp3): reusable buffers in HoldStateMachine + HoldTracker
b03855e perf(w6-sp3): LatencyMeter GC-free rewrite (StringBuilder + throttle)
5af0a7c docs(w6-sp3): profile after-fix capture — GC.Collect=0 achieved
```

## Deviations from plan

1. **W4 carry-over #1 hypothesis was wrong, data corrected it.** Plan Task 4 was framed as "primary fix of the two known allocators." Baseline profile showed zero allocation from those sites on The Entertainer; Task 4 was reframed mid-execution as "defense-in-depth fix for hold-bearing charts" and kept in scope because (a) it's a legitimate latent allocator on charts with holds, (b) the 10-test migration was already scoped, (c) the signature change enforces a clean contract. `LatencyMeter` was added as Task 5 (new PRIMARY fix). Both landed; the fix that moved the GC=0 needle was Task 5.

2. **Plan Task 5 "open-ended / data-driven"** → concretized to LatencyMeter GC-free rewrite once baseline pointed there.

3. **Spec §9 "pipeline-truncation" / "start-buffer" criteria not relevant to this SP.** (Those belonged to W6-SP2's chart acceptance tests.) Not an actual deviation but clarifying for readers who jump between SPs.

4. **`ApkBuilder` size log bug carried forward.** Both `Build` and `BuildProfile` still log `report.summary.totalSize / 1024 / 1024` which is the aggregate of all build artifacts, not the APK file size itself (observed "1145 MB" for the profile build despite 124 MB APK). This was a W6-1 carry-over flagged in that SP's completion report; not fixed here. Listed in carry-overs.

## Operational findings

- **Unity 6 IL2CPP batch build concurrency conflict**: when the user had interactive Unity Editor open (for Profiler analysis), a parallel `ApkBuilder.Build` batch-mode run failed at step 1101/1110 (`Link_Android_arm64 libil2cpp.so` ExitCode 1). Closing the interactive Editor and retrying succeeded cleanly. **Persistent rule update candidate**: "Unity batch build requires no other Unity instance open on the same project."
- **Unity 6 `IPCStream / Not connected`** flakiness (W6-1 origin) did NOT recur in SP3 builds. The retry that succeeded was caused by the Editor-lock issue above, a different class of error.
- **Development Build APK size is ~3.5× release size** (124 MB vs 34 MB) — IL2CPP debug symbols + Burst debug information. Worth documenting so future profile sessions don't spook observers about apparent size inflation.
- **Unity Profiler default save location** is `<project-root>/ProfilerCaptures/` with auto-generated timestamp name. Plan specified saving to `Logs/profile-w6sp3-*.data`; relocation was a simple `mv` step. Plans going forward should either specify absolute path in Save dialog or accept the default-location + relocate pattern.
- **Profiler Hierarchy view is single-frame analysis**, not aggregate across a time range. For "top allocators across 2-min session" the user had to pick a representative mid-gameplay frame; the per-frame hot-loop pattern was obvious enough that single-frame sampling sufficed. For finer aggregation (detecting irregular allocations), Unity Profile Analyzer package is the next tool tier.
- **Evidence-first profiling caught what grep-first hypothesis missed.** The W4 carry-over memo pointed at two concrete allocator candidates; both turned out to be zero-fire on the actual stress-test chart, while the real offender (a W1 PoC debug HUD) was never suspected in any prior review. Pattern worth carrying forward: always profile before fixing.

## Carry-over items

Logged as follow-ups, not spawned separately:

1. **Hold-note audio feedback** (observed during Für Elise playtest) — holding a key sounds identical to single tap. Pre-existing missing feature, not a W6-SP3 regression. Design question: should hold produce (a) looped sustain sample, (b) additional damp sample on release, (c) visual-only feedback only? Candidate for a dedicated audio-polish SP.
2. **TextMeshPro migration for HUD** — would eliminate the last `sb.ToString()` allocation residual (~600 B/sec) via `TMP_Text.SetText(StringBuilder)` zero-alloc API. Not needed for current GC=0 goal but useful if frame-budget tightens later.
3. **`ApkBuilder` size log bug** — `report.summary.totalSize` vs actual APK file size. W6-1 carry-over still open; cosmetic but misleading in CI logs.
4. **`LatencyMeter.lastFrameLatencyMs` dead write** — field assigned in `MeasureFrameLatency` but never read. Pre-existing dead code from W1 PoC era. Candidate for cleanup.
5. **Score thousands-separator restoration** — SP3 dropped `:N0` for alloc-free formatting. If UX review wants commas back, either implement alloc-free comma insertion in `StringBuilder` OR accept one allocation per HUD update (already at 2 Hz cadence). Minor, low priority.
6. **Unity interactive-Editor lock rule** — document in memory (`feedback_unity_batch_mode.md`?) that batch builds require no other Editor instance open on the same project; pattern occurred in SP3 Task N+2 build.

## Next steps → remaining W6 + beyond

Remaining W6 scope in declared order (per W6-SP1/SP2 completion reports):

- **W6 #4**: Calibration click sample (dedicated sample replacing `piano_c4.wav` reuse). Small asset + code change.
- **W6 #5**: UI polish — star sprites, chart-load error toast, unreferenced `thumbs/locked.png` cleanup.
- **W6 #6**: 2nd device (mid-tier Android) before Internal Testing distribution.
- **Canon in D self-sequencing** (from W6-SP2 carry-over) — orthogonal content SP, can interleave.

SP3's hold-audio carry-over (item 1 above) is a new candidate; priority to be set during next planning cycle.

BGM audio remains out of scope per v2 pivot; post-MVP only.

## W4 carry-over resolution

**W4 carry-over #1 (mid-game tap drops) — RESOLVED.** Root cause was LatencyMeter HUD allocation, not the HoldTracker/HoldStateMachine paths the memo suspected. Fix shipped; device playtest confirms symptom eliminated. Memo should be updated to reflect actual root cause; the Hold* fix still stands as latent-bug prevention.
