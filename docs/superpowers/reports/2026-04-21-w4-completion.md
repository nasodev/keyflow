# W4 Completion Report

**Date:** 2026-04-21
**Branch:** w4-screens (HEAD: 997c0d4)
**APK:** Builds/keyflow-w4.apk (31.5 MB)

## Scope delivered

- Main / Settings / Pause / Results screens live end-to-end on device.
- `ScreenManager` with `Replace` + overlay toggle + Back dispatch.
- `SongCatalog` JSON parser + `catalog.kfmanifest` (1 real + 4 W5 placeholders).
- Procedural star (filled/empty) and thumbnail sprites generated in `SceneBuilder`.
- `AudioSyncManager.Pause/Resume` with dspTime-anchor shift; pause guards on spawner/note/tap/hold.
- `UserPrefs` facade + legacy `CalibOffsetMs` migration + per-song best-stars/score.
- `GameplayController.ResetAndStart` — Retry stays in-scene (no `SceneManager.LoadScene`).
- `UIStrings` Korean hub.
- Carry-overs absorbed: #4 namespace consolidation, #5 calibration prompt copy, #6 OverlayBase + OverlayBase-based CalibrationController, #7 SongSession static.

## Test counts

- EditMode: 88 / 88 pass (was 68 at W3 end).
  - +7 UserPrefs (incl. migration + best-record)
  - +2 OverlayBase
  - +4 ScreenManager
  - +4 SongCatalog
  - +3 AudioSyncPause

## Device validation (Galaxy S22, Android 16)

Hand-walked by user against spec §10:

- [x] 1. First-launch path — Main → Easy → Calibration → Gameplay → Results → Retry → Gameplay → Home → Main.
- [x] 2. Settings — ⚙ → SFX/NoteSpeed sliders respond → Recalibrate re-enters Calibration and returns to Settings → ✕ → Main.
- [x] 3. Pause — ⏸ freezes notes and audio; 계속하기 resumes. Android Back = same. No piano sample leak on UI buttons.
- [x] 4. Exit — double-Back on Main within 2 s quits.
- [x] 5. Record persistence — 3-star retry flashes "최고 기록!" and Main card updates to ★★★.
- [x] 6. Migration — skipped (not a regression path this cycle).
- [x] 7. APK size: 31.5 MB (< 40 MB target).
- [x] 8. 60 FPS confirmed via LatencyMeter HUD.

## Device fixes applied during validation

- `d780d2c` — ResultsCanvas Retry/Home button widths (was 440 × 2 on a 720 screen; now 300 each, anchored 0.3 / 0.7). TapInputHandler now skips touches over any UI (`EventSystem.RaycastAll`) and returns early while any overlay is visible — fixes the piano sample firing when tapping Resume/Quit. HUDText `raycastTarget=false`. MainScreen subscribes to `OnReplaced(Main)` and re-populates cards so new best stars show after Home.
- `997c0d4` — SongCard layout rewritten from chained `LayoutGroup`s (Center column was collapsing to 0 width and scattering Title/Stars as siblings of Thumbnail) to anchor-based placement.

## Outstanding issues

None that block. Calibration is still entered on every fresh install — no in-scene shortcut to skip when `HasCalibration=false`, which is by design for MVP.

## Performance snapshot

- FPS: 60 (Galaxy S22 LatencyMeter HUD)
- APK size: 31.5 MB
- Audio latency: within W1 PoC envelope (not re-measured this week)

## Carry-over items still deferred

- W3 carry-over #1 (mid-game tap drops profiler) → W6
- W3 carry-over #2 (dedicated Calibration click sample) → W6
- W3 carry-over #3 (ChartLoader coroutine) → W5
- W3 carry-over #8–#11 → unchanged

## Next steps → W5

Python MIDI → .kfchart pipeline, 4 more songs to replace the placeholder slots, Für Elise Normal difficulty, and ChartLoader moved to coroutine (W3 carry-over #3).
