# W2 Completion — 2026-04-20

## Delivered

Portrait 4-lane Piano Tiles gameplay foundation with judgment, scoring, and HUD, built on top of the W1 PoC audio/timing core. All implementation done via subagent-driven development, 11 commits across 13 planned tasks.

### Implementation summary

| Plan task | Commit | Deliverable |
|---|---|---|
| 1 | `02b665b` | Orientation → Portrait; `GameTime.PitchToX` removed (PitchRange stays public for W3) |
| 2 | `0224186` | `LaneLayout` pure lane math (LaneToX / XToLane) + 8 tests |
| 3 | `27a20de` | `Judgment` + `Difficulty` enums + `JudgmentResult` struct |
| 4 | `9e3af52` | `JudgmentEvaluator` pure threshold math + 13 tests (spec §5.1) |
| 5 | `a0155e5` | `ScoreManager` plain C# class + 11 tests (spec §5.2-5.3) |
| 6-9 | `ae0a586` | Gameplay glue: `NoteController` lane-based, `NoteSpawner` lane-based + JudgmentSystem registration, new `JudgmentSystem` MonoBehaviour, `TapInputHandler` with `OnLaneTap` event |
| 10 | `53c3861` | `LatencyMeter` HUD shows Score / Combo / Last judgment |
| 11 | `4391c5f` | `SceneBuilder` rewritten for portrait 4-lane; menu renamed `Build W2 Gameplay Scene` |
| (scene regen) | `d3ba28a` | Regenerated `GameplayScene.unity` + `Note.prefab` + auto-generated `.meta` files |

All tasks passed two-stage review (spec compliance → code quality) by dispatched reviewer subagents before the next task began.

## Test count

- W1 end: 11 EditMode tests
- W2 end: **40 EditMode tests** (6 GameTime + 2 AudioSamplePool + 8 LaneLayout + 13 JudgmentEvaluator + 11 ScoreManager)
- Net change: +32 tests (3 PitchToX removed, 32 added across LaneLayout/JudgmentEvaluator/ScoreManager)
- User-verified **40/40 passing** in Unity Test Runner (EditMode) after Task 5.

## Verified in Editor (Play mode)

- Portrait aspect ratio (720×1280 reference)
- 4 lane dividers, cyan judgment line at y=-5 (~81% down the viewport)
- Notes fall from y=6.5 to y=-5 across all 4 lanes, cycling lane 0→1→2→3→0
- Tap in correct lane during judgment window → Perfect/Great/Good, combo increments
- Miss (ignore) → note auto-despawns after Good-window grace (180ms for Normal), combo resets
- Ghost tap (tap in lane with no pending note) → silent, no penalty (Piano Tiles-forgiving behavior)
- HUD shows 7 lines: FPS / Score / Combo / Last judgment / dspTime drift / Song time / Buffer

## Known limitations (deferred to W3)

- Notes still hardcoded sequence (not `.kfchart` JSON loaded) — in `NoteSpawner` defaults
- Only C4 piano sample — multi-pitch library is W3
- No Hold notes — TAP only in W2
- No calibration UI — `CalibrationOffsetSec` stays 0
- No difficulty selection — Normal hardcoded in `NoteSpawner.difficulty`
- No scene transitions (main menu, song list, results) — only GameplayScene exists
- No particle / haptic polish — W6

## Subjective observations (device playtest, 2026-04-20)

- **APK size:** 27.72 MB (target <40MB ✅)
- **Install:** `adb install -r` success, streamed
- **Cold start to playable:** ≤3s (subjective, not timed precisely)
- **Portrait layout:** as designed, 4 lanes visible, cyan judgment line at bottom
- **Tap → judgment:** working across all 4 lanes
- **HUD:** all 7 lines render
- **Crashes in 30s+ play:** none
- **User verdict:** "확인함" (confirmed) — gameplay loop works end-to-end

Precise numeric metrics (FPS, dspTime drift, tap latency) not separately captured this session. The W1 PoC measurement baseline (~110ms tap-to-audio on this same device) still applies — W2 did not change the audio pipeline.

## W2 completion criteria check

Per plan Task 13:
- [x] 40 EditMode tests pass (verified in Unity Test Runner mid-sprint)
- [x] Portrait 4-lane scene, no compile errors
- [x] Play-in-editor: lane taps yield P/G/G judgments, miss auto-detect, score climbs, combo resets
- [x] APK built, installed on Galaxy S22, 30s+ play without crash
- [x] Completion report filled
- [x] Repo at `main` with commits from all 13 tasks (11 implementation commits covering the 13 plan tasks — compile-unit tasks 6-9 combined into one commit per plan)

## Next step

Plan 3 / W3: `.kfchart` chart loader + Hold note support + Calibration MVP + first song (Für Elise Easy) completable end-to-end. Plan 3 document to be written via `writing-plans` skill once W2 is signed off.
