# W5 Completion Report

**Date:** 2026-04-21
**Branch:** `claude/jovial-leavitt-5ac913` (worktree — merge to `main` pending)
**HEAD:** `b40cc7b`
**APK:** `Builds/keyflow-w5.apk` (33.15 MB)

## Scope delivered

- Python MIDI → `.kfchart` pipeline at `tools/midi_to_kfchart/` — 7 `pipeline/` modules + CLI (single + batch) + pytest.
- Für Elise NORMAL chart generated via pipeline from bitmidi.com source MIDI (157.6 s, 2102 note_on → 589 NORMAL notes, NPS 3.68, 70 HOLD notes, no 3-consecutive lane).
- `ChartLoader.LoadFromStreamingAssetsCo` coroutine API replaces the Android busy-wait.
- `GameplayController.ResetAndStart` split around the async callback (new `ContinueAfterChartLoaded`).
- `ChartLoader.Validate` hardened: empty-notes, sort-order, totalNotes-mismatch checks (carry-over #9).
- `Mathf.Clamp` → `System.Math.Clamp` in `ChartLoader` and `CalibrationCalculator` (carry-over #8).
- `LoadFromPath` extracted for editor/test use; old sync `LoadFromStreamingAssets` + `ReadStreamingAsset` removed.
- UWR resource leak fix (carry-over from Task 15 code review): explicit `req.Dispose()` instead of `using { yield return }`.

## Test counts

- **Python pytest**: 32 / 32 pass (`tools/midi_to_kfchart/.venv/Scripts/pytest -q`)
  - parser 4, chord_reducer 3, hold_detector 4, density 5, pitch_clamp 4, lane_assigner 4, emitter 5, CLI 3.
- **Unity EditMode**: 93 / 93 pass (was 88 at W4 end).
  - +3 ChartLoaderTests list-level validators (empty / unsorted / totalNotes mismatch)
  - +2 ChartLoaderTests LoadFromPath (real chart parses, missing file throws).

## Commits (worktree, oldest → newest)

1. `fefdb2c` — scaffold `tools/midi_to_kfchart/`
2. `38791a7` — parser.py
3. `231eddc` — chord_reducer.py
4. `c2531e4` — hold_detector.py (300 ms threshold)
5. `a651060` → `45120d9` — density.py (initial + spec-alignment fix removing `max(2, ...)` clamp)
6. `8ae2e9b` — pitch_clamp.py
7. `7fc4d2c` → `ce1b794` — lane_assigner.py (initial + cleanup: defensive comment, test tighten, redundant guard removed)
8. `11001b5` — emitter.py
9. `2cddbb4` — CLI single-file mode
10. `598e4ee` — batch mode + PyYAML + example YAML
11. `13998dc` — ChartLoader Validate hardening (empty/sort/totalNotes)
12. `84c74be` — `Mathf.Clamp` → `System.Math.Clamp` (carry-over #8)
13. `3ae984d` — `ChartLoader.LoadFromPath`
14. `3147632` → `453bcdf` — coroutine API + GameplayController split (initial + explicit UWR Dispose fix)
15. `ad530af` — Für Elise NORMAL chart content (pipeline output, merged with EASY)
16. `b40cc7b` — rename APK artifact to `keyflow-w5.apk`

## Deviations from plan

- **Task 11 `--duration-ms`**: plan said `45000` (parity with EASY 45 s window). Actual source MIDI is 157.6 s. Using `45000` would have failed `ChartLoader.Validate` because most generated notes have `t > 45000`. Used `--duration-ms 160000`. Root `durationMs` of the `.kfchart` updates to 160000; EASY (maxT 38000) still validates. `GameplayController` computes end-of-song from `spawner.LastSpawnedHitMs + LastSpawnedDurMs + missWindowMs`, not root `durationMs`, so EASY gameplay UX is unchanged. Spec reviewer accepted.
- **Task 5 density formula**: plan had `step = max(2, round(1 / (1 - keep_ratio)))`. Code-reviewer flagged the `max(2, ...)` clamp as over-delivering at extreme NPS ratios. Aligned both plan and implementation to spec §3.4's `round(...)` with an explicit `if step < 2: return []` early-exit.
- **Task 7 `+2` lane-relief fallback**: code-reviewer flagged as unreachable. Confirmed unreachable given left-to-right traversal. Kept the code (matches spec §3.4 pseudocode verbatim) with a defensive comment; strengthened one test; dropped a redundant `i >= 2` guard inside an already-guarded loop.

## Operational findings

- Unity `-runTests` must **not** include `-quit` (silently skips the test runner — different from `-executeMethod` where `-quit` is required). Saved as memory `feedback_unity_runtests_no_quit.md`.
- Running Unity tests against a git worktree works by pointing `-projectPath` at the worktree root. First run takes ~5–10 min (asset import); subsequent runs ~1 s. Main project left untouched.

## Device validation — **DONE**

APK at `Builds/keyflow-w5.apk` installed on Galaxy S22 (R5CT21A31QB) via `adb install -r`. User walk-through:

- [x] Normal button appears and is tappable after the catalog fix (`catalog.kfmanifest` `difficulties: ["Easy", "Normal"]`, committed `d51c404`).
- [x] Main → Für Elise → Normal → Gameplay → runs to completion on device.
- [x] No ANR, no blocked launch (coroutine chart load on Android works end-to-end).
- [x] No regression on Easy path.

### User feedback on Normal experience

- **"배경음이 없다"** — by design. Spec §0 item 4 and §1.3 explicitly removed BGM in the v2 pivot ("탭이 곧 음악"). Not a W5/W6 item; a post-MVP v1.0 decision if ever revisited.
- **"타격음이 한 종류라 게임하는 느낌이 안 든다"** — real gap. `Assets/Audio/` contains only `piano_c4.wav` (W1 PoC state). Spec §6.2 requires 48-key bundle (MIDI 36–83) but §9 didn't pin it to a week. **Promoting to explicit W6 scope** (see Next steps).

## Carry-over items from this W5 cycle

From the original W4-carryover list that was intentionally deferred:

- #1 Mid-game tap drops profiler — W6
- #2 Dedicated calibration click sample — W6
- #4 Star ASCII placeholder → sprite icons — already resolved earlier in W4 or still deferred; verify at W6 start
- #10 Calibration index-pairing alignment — v2 (post-MVP)
- #11 Additional EditMode edge coverage — W6
- #12 `-batchmode` foreground note — actively applied; no outstanding action

New in W5:
- Minor: `_make_tiny_midi` helper duplicated in `test_parser.py` and `test_cli.py`; consider moving to `conftest.py` if a third test file needs synthetic MIDI.
- Minor: `GameplayController`'s chart-load error callback only logs; no visible UI surface. For a solo MVP this is acceptable, but a W6/W7 polish pass could add a toast or retry button.

## Next steps → W6

W5 signed off. W6 brainstorm should open in a fresh session; this report is the primary handoff artifact. Scope candidates in priority order:

1. **Multi-pitch piano samples (NEW, promoted from spec §6.2)** — bundle Salamander Grand Piano V3 48-key set (MIDI 36–83) as WAV or OGG. Wire `AudioSamplePool` to select pitch-appropriate sample per tapped note. Highest impact on "게임하는 느낌" per W5 playtest feedback.
2. **Content: remaining 4 songs** — Ode to Joy, Canon in D, Clair de Lune, The Entertainer × 2 difficulties each. Feed MIDIs through `tools/midi_to_kfchart/midi_to_kfchart.py --batch batch.yaml` (pipeline and example YAML already shipped in W5).
3. **Profiler pass** — carry-over #1 (mid-game tap drops).
4. **Calibration click sample** — carry-over #2 (dedicated sample, not piano_c4 reuse).
5. **UI polish** — star sprites (carry-over #4 if still open), chart-load error toast.
6. **Second device** — mid-tier Android before Internal Testing distribution.

BGM is **not** on this list — it's post-MVP per v2 pivot.
