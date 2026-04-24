# W6 SP11 Completion — Game Start Countdown (3-2-1-GO!)

**Date:** 2026-04-24
**Branch:** `w6-sp11-countdown`
**Worktree:** `.claude/worktrees/w6-sp11-countdown`
**Commits on branch:** 12 (see tail of report)
**Device playtest:** Passed on Galaxy S22 R5CT21A31QB after 4 APK iterations.

---

## What shipped

A 3-second "3 → 2 → 1 → GO!" countdown between `GameplayController.BeginGameplay()` and `AudioSyncManager.StartSilentSong()`. Song clock now starts at GO! instead of immediately post-calibration. Center-screen world-space popups (144pt white digits, `#FFD700` gold GO!, Outline + scale-punch + alpha-fade). SP5 `calibration_click.wav` reused for each tick; GO! at pitch 1.5. Pause button hidden during the 3.4-second sequence, restored at GO!.

## Components

**New files (8):**
- `Assets/Scripts/UI/ICountdownOverlay.cs` — 1-method injection seam
- `Assets/Scripts/UI/CountdownOverlay.cs` — Update-driven state machine
- `Assets/Scripts/UI/CountdownNumberPopup.cs` — per-popup animator (SP10 pattern)
- `Assets/Scripts/UI/IClickPlayer.cs` — audio injection seam
- `Assets/Scripts/UI/AudioSourceClickPlayer.cs` — production adapter
- `Assets/Tests/EditMode/CountdownNumberPopupTests.cs` — 4 tests
- `Assets/Tests/EditMode/CountdownOverlayTests.cs` — 5 tests
- `Assets/Tests/EditMode/GameplayControllerCountdownTests.cs` — 4 tests (3 original + 1 retry guard)

**Modified files (4):**
- `Assets/Scripts/Gameplay/GameplayController.cs` — `BeginGameplay` refactor + `ResetAndStart` audio-reset
- `Assets/Scripts/Gameplay/AudioSyncManager.cs` — new `Stop()` method
- `Assets/Scripts/Feedback/ParticlePool.cs` — `OnEnable` auto-clear
- `Assets/Editor/SceneBuilder.cs` — `BuildCountdownOverlay` + `BuildGameplayController` signature extension
- `Assets/Scenes/GameplayScene.unity` — mechanically regenerated
- `Assets/Scripts/Gameplay/AudioSyncManager` / `Tests/EditMode/AudioSyncPauseTests.cs` — +2 Stop tests
- `Assets/Editor/ApkBuilder.cs` — filename bump sp10 → sp11

## Test counts

- EditMode: 179 (baseline) → **194** (+15). Breakdown: +4 popup, +5 overlay, +4 GameplayController countdown, +2 AudioSync Stop.
- pytest: 49/49 green (pipeline untouched).
- APK: 38.05 MB (within spec §1 38.10 MB guardrail, same as SP10).

## Device playtest — 4 iterations

Unlike prior SPs which typically took 1-3 iterations, SP11 needed 4 APKs because two latent cross-session state leaks only manifested with the new 3-second countdown gap. Pre-SP11, `StartSilentSong()` fired immediately in `BeginGameplay` and masked the leaks.

| APK | Commit | Device result | Bug found | Fix |
|---|---|---|---|---|
| v1 | `5a37677` | 2nd play: countdown shows + notes spawn + MISS flood | `audioSync.started` stale from prior session; `songStartDsp` stale → `NoteSpawner.Update` sees `IsPlaying=true` with huge `SongTimeMs` during countdown, spawns all chart notes at once | `AudioSyncManager.Stop()` method added; called in `BeginGameplay` before spawner init (commit `0419f78`) |
| v2 | `0419f78` | 2nd play: particles still burst during countdown (no MISS text) | Old `NoteController`s from prior session still alive during `ChartLoader` coroutine yield window; their Update ran with stale `audioSync.started=true`, fired `HandleAutoMiss` before `ContinueAfterChartLoaded` could `ResetForRetry` them | Moved `audioSync.Stop()` earlier — to top of `ResetAndStart` so it runs BEFORE the chart-load coroutine yields (commit `ae4122f`) |
| v3 | `ae4122f` | 2nd play: still particle bursts before countdown | Not a JudgmentSystem firing at all — Unity's `ParticleSystem.SetActive(false)` PAUSES simulation without clearing it. Pool entries mid-emission at end of 1st play resumed visually when GameplayRoot reactivated for 2nd play | `ParticlePool.OnEnable` auto-clears all pool entries via `ps.Clear(true) + SetActive(false)`. Fires on every GameplayRoot reactivation (commit `271ad1f`) |
| v4 | `271ad1f` | **All passing** — clean countdown, no ghost bursts, first-note timing preserved | — | — |

## New Unity gotchas learned

1. **Unity `ParticleSystem.SetActive(false)` pauses without clearing.** Simulation state persists across reactivation — designed for "seamless pause" but behaves as a state leak when used as a visibility toggle. Fix pattern: `ps.Clear(true)` on `OnEnable`. Relevant anywhere a ParticleSystem can be parented under a screen that toggles visibility.

2. **`AudioSyncManager.started` was never reset across gameplay sessions pre-SP11.** Invisible because `StartSilentSong()` overwrote `songStartDsp` on every call. SP11's 3-second deferral exposed the latent issue. New `Stop()` method handles this explicitly; `ResetAndStart` calls it before the chart-load coroutine yields so prior-session `NoteController.Update` early-returns during the yield window.

3. **`ChartLoader.LoadFromStreamingAssetsCo` yields for 1+ frames on Android file I/O.** This window is large enough that `NoteController` Update calls fire during it. Any per-frame state machine referenced by live objects during this yield MUST be quiescent before coroutine starts.

## Scope drift from original spec

The spec §2.3 Guardrails claimed "AudioSyncManager unchanged". Retry-bug discovery required adding `AudioSyncManager.Stop()` — a 3-line additive method that doesn't change any existing public API behavior. Defensible: the guardrail was written to prevent scope creep, and this is a discovered regression that must be fixed before merge. No existing callers of AudioSyncManager are affected.

ParticlePool auto-clear is additive (new `OnEnable` method) and has no spec conflict.

## Housekeeping

- **SP10 leftover `.claude/worktrees/suspicious-hodgkin-52399d`** cleanup (SP11-T9): blocked. The orphan directory is empty but Windows is holding a process handle on it (likely File Explorer or antivirus). `rm -rf`, `rmdir /s /q` both fail with "Device or resource busy". Deferred to post-merge manual cleanup (close any Explorer windows, retry; or reboot + rmdir).

## Deferred / follow-up

- **Spec §4.3 "Completion report at `docs/superpowers/reports/2026-04-??-w6-sp11-countdown-completion.md`"** — this file, date 2026-04-24.
- **JudgmentTextPool OnEnable clear**: The same pattern could apply to SP10's text popup pool. Not observed as a bug in v4 playtest (text popups auto-deactivate on first post-reactivation Update tick — 1-frame blip at most), but defensive parity with ParticlePool is cheap. Tracking for post-MVP if playtest ever reveals a flash.
- **EditMode test for ParticlePool OnEnable clearing**: Skipped in this SP — ParticleSystem simulation doesn't run in EditMode batch (requires playmode frame ticks). Reviewing pattern in a future SP that needs PlayMode tests.

## Commit chain

```
271ad1f fix(w6-sp11): clear ParticlePool on reactivation (ghost burst bug)
ae4122f fix(w6-sp11): stop audioSync in ResetAndStart before chart-load yield
0419f78 fix(w6-sp11): reset audioSync state before countdown (retry bug)
5a37677 chore(w6-sp11): bump APK output filename to keyflow-w6-sp11
b2f4da0 fix(w6-sp11): reorder LogError check + document Popup.TickForTest semantics
c9224be refactor(w6-sp11): log error if CountdownOverlay.clickPlayer unwired
82612a9 feat(w6-sp11): SceneBuilder wiring + CountdownOverlay production audio
08b80a5 feat(w6-sp11): GameplayController defers audio start behind countdown
d250044 refactor(w6-sp11): CountdownOverlay ordering + docs clarity
e43b8f9 feat(w6-sp11): CountdownOverlay state machine (3->2->1->GO!)
1222c1e feat(w6-sp11): IClickPlayer interface + AudioSource adapter
f608166 feat(w6-sp11): CountdownNumberPopup (scale-punch + alpha-fade)
```

Spec: `docs/superpowers/specs/2026-04-24-keyflow-w6-sp11-countdown-design.md`
Plan: `docs/superpowers/plans/2026-04-24-keyflow-w6-sp11-countdown.md`
