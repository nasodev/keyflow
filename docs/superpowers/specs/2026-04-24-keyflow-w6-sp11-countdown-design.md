# KeyFlow W6 Sub-Project 11 — Game Start Countdown (3-2-1-GO!)

**Date:** 2026-04-24
**Week:** 6 (폴리싱 + 사운드)
**Parent MVP spec:** `docs/superpowers/specs/2026-04-20-keyflow-mvp-v2-4lane-design.md`
**Depends on:**
- W6 SP3 profiler pass (merged `30b846d`) — GC=0 baseline this SP must not regress.
- W6 SP5 calibration click (merged `c0ade42`) — SP11 reuses `Assets/Audio/calibration_click.wav` for countdown tick audio (3/2/1 at pitch 1.0, GO! at pitch 1.5).
- W6 SP10 judgment text popup (merged `08f2e6a`) — SP11 adapts SP10's `JudgmentTextPopup` animation pattern (scale-punch + alpha-fade) for countdown numbers. Also inherits the SP10 `SceneBuilder` scene-save-time `SetActive(false)` pattern to prevent ScreenManager.Start race on the new countdown canvas.

**Status:** Proposed

---

## 1. Motivation

Today the gameplay flow is:

```
Song select → (Calibration if first run) → Notes fall immediately
```

The transition from calibration-complete (or song-select tap, for calibrated players) to the first descending note is abrupt. There is no moment for the player to set their fingers, orient to the lane layout, or sync their breathing to the chart. On a casual children-oriented rhythm game, that cold start leads to first-note misses that feel arbitrary rather than earned.

Magic-Piano-class rhythm games universally gate gameplay behind a **3-2-1-GO! countdown** — a short, rhythmic preamble that sets expectation, gives the eye a moment to adjust to the game canvas, and creates a "we're starting now" moment that amplifies the first beat.

SP11 adds that countdown as a non-interactive 3-second preamble between `GameplayController.BeginGameplay()` and `AudioSyncManager.StartSilentSong()`. Nothing else about the chart clock, preview window, or calibration offset changes — the song clock simply starts later.

**Qualitative success criterion:** On Galaxy S22 (R5CT21A31QB) during a fresh-profile first-play of Entertainer Normal, the player sees a centered "3" → "2" → "1" → "GO!" with audible click on each number and a pitch-up click on GO!, then the first note falls in sync with the song clock. No perceptible latency between GO! and the first note's preview window. 60 fps and GC.Collect=0 are preserved from the SP3 baseline.

**Quantitative guardrails:**
- GC.Collect count = 0 during a 2-minute Entertainer Normal session (SP3 parity).
- EditMode tests: 179 (current) + ~12 new = ~191, all green.
- pytest: 49/49 green (pipeline untouched).
- APK size ≤ 38.10 MB (current 38.04 MB + new C# files only, no new assets).
- Judgment offset identical to SP10 — countdown end redefines song-t=0, not calibration offset.

---

## 2. Scope

### 2.1 In scope

| ID | Item | Deliverable |
|---|---|---|
| SP11-T1 | `ICountdownOverlay` interface | Single-method contract `void BeginCountdown(System.Action onComplete)` for test injection into `GameplayController`. |
| SP11-T2 | `CountdownNumberPopup` MonoBehaviour | Per-instance animator. Scale-punch (1.5 → 1.0 over 18%) + alpha-fade (1.0 → 0.0 over last 45%) + no y-rise. Deactivates self at t≥1. Adapted from SP10 `JudgmentTextPopup`. |
| SP11-T3 | `CountdownOverlay` MonoBehaviour | Coroutine sequencer. `BeginCountdown` runs 3→2→1→GO! with 1.0 s spacing, 0.4 s GO! hold, invokes `onComplete`. Owns single `CountdownNumberPopup` instance (max-concurrent = 1). Owns the pauseButton hide/show lifecycle. |
| SP11-T4 | `IClickPlayer` + `AudioSourceClickPlayer` | Adapter so `CountdownOverlay` tests can verify pitch-per-call without driving real `AudioSource`. Mirrors SP4 `IHapticService` / SP10 `IJudgmentTextSpawner` pattern. |
| SP11-T5 | `GameplayController.BeginGameplay` change | Delegate `audioSync.StartSilentSong()` and `playing=true` into the `countdown.BeginCountdown` callback. Add `SetCountdownForTest` hook. |
| SP11-T6 | `SceneBuilder.BuildCountdownOverlay` | Construct world-space `CountdownCanvas` (sortingOrder = JudgmentTextCanvas+1), single `CountdownNumberPopup` child, `AudioSource` child, wire `clickSample`, `pauseButtonRoot`, and `GameplayController.countdown` SerializeField. |
| SP11-T7 | EditMode tests | `CountdownNumberPopupTests` (~4), `CountdownOverlayTests` (~5), `GameplayControllerCountdownTests` (~3). Target 12 new tests. |
| SP11-T8 | Device playtest | Galaxy S22 checklist: sequence rendering, audio, first-note sync, pause button hide/show, retry-flow countdown re-play, GC=0, 60fps. |
| SP11-T9 | Housekeeping — SP10 orphan worktree | Remove `.claude/worktrees/suspicious-hodgkin-52399d` (filesystem-only orphan, not in `git worktree list`). One-command cleanup bundled with SP11 PR. |

### 2.2 Out of scope

- **Tap-to-skip countdown.** Rejected (Q4). Scope creep on input handling + edge case of "skip-tap doubles as first-note tap". Revisit post-MVP if playtest reveals it's annoying.
- **Pause during countdown.** Rejected (Q5). Pause button is hidden for the 3 seconds and restored at GO!. Pausing a 3-second preamble is not worth the time-source complexity.
- **"READY?" pre-text.** Rejected (Q2). The countdown itself IS the readiness cue.
- **BPM-synced countdown.** Chart BPM varies by song; a fixed 1.0 s interval reads as rhythmic on its own without introducing chart-metadata coupling.
- **Custom countdown length (5-4-3-2-1 etc.).** `stepDurationSec` is [SerializeField]-exposed for future tuning but defaults are fixed at 3 steps × 1.0 s.
- **New font / font-size override.** Uses `LegacyRuntime.ttf` (Unity built-in) at 144pt with `UnityEngine.UI.Outline`, matching SP10's on-device readability approach.
- **Visual art assets.** No new sprites, no new images, no new fonts. White text for 3/2/1; `#FFD700` (SP10 Perfect palette) for GO! as a "success signal" color.
- **Countdown animation alternatives per step** (e.g., different animation for "3" vs. "GO!"). All four popups share the same animator; only `label` and `color` vary.
- **Retry-flow countdown skip.** Retry re-enters `ResetAndStart` → `BeginGameplay` → countdown, which is the desired behavior (player expects a new attempt to feel like a new start).
- **Localized text (Korean).** English glyphs "3", "2", "1", "GO!" are genre-standard and locale-neutral at the visual register used here.

### 2.3 Guardrails (non-regression contracts)

- **GC.Collect=0 during 2-min Entertainer Normal session** (SP3 baseline). The `CountdownOverlay` uses a single popup instance (no instantiation in the game loop). `PlayOneShot` does not allocate.
- **No change to chart timing.** Countdown end is the new song-t=0 reference. Calibration offset, preview window, and judgment windows are identical.
- **SP10 feedback pipeline unchanged.** `JudgmentTextCanvas` / `JudgmentTextPool` / `FeedbackDispatcher` not touched.
- **SP4 haptic + particle pipeline unchanged.** No `JudgmentSystem` modifications.
- **`AudioSyncManager` unchanged.** Delay is achieved by postponing the `StartSilentSong()` call from the `BeginGameplay` body into the countdown callback, not by changing `scheduleLeadSec` or introducing a new API.
- **Existing 179 EditMode tests remain green** with zero source modification. New tests are additive; existing `GameplayController` tests (if any exercise `BeginGameplay`) gain a mock-countdown injection but no assertion changes.
- **ScreenManager.Start race (SP10 lesson).** The new `CountdownCanvas` is saved in the scene `SetActive(false)`; `CountdownOverlay.Show()` does not rely on a coroutine `Start()` and is only activated by `BeginCountdown()`.

---

## 3. Approach

### 3.1 Design decisions (from brainstorming)

| # | Decision | Chosen | Rejected alternatives |
|---|---|---|---|
| 1 | Timing model | **Postpone `StartSilentSong()` to countdown-complete callback.** Song clock starts at GO!. `NoteSpawner.Update` already gates on `audioSync.IsPlaying` → automatic preview suppression during countdown. | (B) Override `scheduleLeadSec = 3.5s` — hides countdown intent in AudioSync config; (C) Offset chart t by +3000 ms — pipeline-wide change, breaks existing charts. |
| 2 | Rendering architecture | **New `CountdownOverlay` + `CountdownNumberPopup` components, SP10 animation principles reused, dedicated world-space canvas.** | (B) Parameterize `JudgmentTextPopup` for both roles — conflates feedback vs. UI responsibilities, pollutes SP10 class with config; (C) Minimal fade-only — flat, risk of "밋밋하다" playtest feedback (SP10 precedent at 48→72pt). |
| 3 | Popup instance count | **Single reusable instance.** Max-concurrent countdown popups = 1 (sequential 3→2→1→GO!). Pool overhead not justified. | 4-slot pool — over-engineered for N=1 concurrent; 1-shot Instantiate-per-step — GC hit every countdown. |
| 4 | Popup position | **Fixed screen center** (y offset ~0 in canvas local). | Top-center like SP10 — judgment-popup real estate; lane-relative — countdown has no lane identity. |
| 5 | Number styling | **White text** for 3/2/1, **`#FFD700`** (SP10 Perfect gold) for GO! | All-white — GO! reads same as 3; rainbow per number — noisy. |
| 6 | Font + size | **`LegacyRuntime.ttf` at 144pt + `UnityEngine.UI.Outline`** (SP10 readability convention, doubled size). | TextMeshPro — new stack for 4 glyphs; 72pt like judgment popup — visually interchangeable with feedback. |
| 7 | Animation | **Scale-punch (1.5→1.0 first 18%) + alpha-fade (last 45%); no y-rise.** | y-rise like judgment popup — judgment popups drift because they represent hit moments; countdown numbers are anchored "announcements"; no animation — flat, see Q2 (C). |
| 8 | Audio | **SP5 `calibration_click.wav` × 3 at pitch 1.0, then × 1 at pitch 1.5 for GO!.** `AudioSource.PlayOneShot`. | Silence — genre convention cost; new bespoke clip — asset bloat, SP11 does not need a new sample; metronome / BPM-synced — see §2.2. |
| 9 | Skip | **No skip gesture.** Player sits through full 3 seconds. | Any-tap skip — collides with first-note input; hold-0.3 s skip — children-UX unfriendly. |
| 10 | Pause during countdown | **Pause button hidden for 3 seconds, restored at GO!.** | Pause halts countdown — Time.time source complexity + minor-case UX; pause button no-op visible — looks broken. |
| 11 | Housekeeping bundling | **SP10 orphan worktree cleanup bundled into SP11 PR.** | Separate PR — 1-command overhead; leave indefinitely — long tail of filesystem residue. |

### 3.2 Control flow

```
GameplayController.BeginGameplay()
  │
  ├─► spawner.Initialize(chart, difficulty)        (unchanged)
  │
  └─► countdown.BeginCountdown(onComplete: () => {
        audioSync.StartSilentSong();              (previously inline)
        playing = true;                            (previously inline)
      })
            │
            ▼
         CountdownOverlay.RunSequence (coroutine)
            │
            ├─► pauseButtonRoot.SetActive(false)
            ├─► [t=0.0s]  popup.Activate("3", white);  clickPlayer.Play(1.0)
            ├─► [t=1.0s]  popup.Activate("2", white);  clickPlayer.Play(1.0)
            ├─► [t=2.0s]  popup.Activate("1", white);  clickPlayer.Play(1.0)
            ├─► [t=3.0s]  popup.Activate("GO!", gold); clickPlayer.Play(1.5)
            ├─► [t=3.4s]  popup hides itself (alpha=0 and SetActive(false))
            ├─► pauseButtonRoot.SetActive(true)
            └─► onComplete() invoked
                  │
                  ▼
               audioSync.StartSilentSong()          (songStartDsp = now + 0.5s)
               NoteSpawner.Update begins spawning   (preview window applies from this point)
```

### 3.3 Rejected architectural alternatives

- **Put countdown logic into `GameplayController` directly.** Rejected: would bloat `GameplayController` with UI coroutines and make it untestable without MonoBehaviour tooling. `ICountdownOverlay` injection keeps GC testable and lets countdown evolve independently.
- **Reuse `JudgmentTextPool` by extending `Activate(...)` with more parameters.** Rejected: SP10 class is tuned for transient judgment feedback; adding `fontSize` / `lifetime` / `yRise` overloads muddles its role. Duplication cost is small (~50 lines in `CountdownNumberPopup`); clarity is large.
- **Drive the countdown from `AudioSyncManager` via `scheduleLeadSec = 3.5`.** Rejected: `scheduleLeadSec` is a DSP-scheduling concept. Overloading it for UI pacing makes both harder to reason about.
- **Use Unity's `Invoke` or `InvokeRepeating` instead of a coroutine.** Rejected: coroutines are already the project convention (`CalibrationController.RunCalibration`) and `WaitForSeconds` is zero-alloc once the coroutine is started.
- **ScreenSpaceOverlay canvas for countdown.** Rejected: SP6 lesson — `ScreenSpaceOverlay` paints over world-space regardless of sortingOrder. World-space canvas with sortingOrder stays consistent with SP10 judgment text canvas and avoids coord-conversion.

---

## 4. Components

### 4.1 New files

| Path | Type | Responsibility |
|---|---|---|
| `Assets/Scripts/UI/ICountdownOverlay.cs` | interface | `void BeginCountdown(System.Action onComplete)`. Injection seam for `GameplayController`. |
| `Assets/Scripts/UI/CountdownOverlay.cs` | `MonoBehaviour`, implements `ICountdownOverlay` | Coroutine sequencer. SerializeFields: `popup` (CountdownNumberPopup), `clickPlayer` (via interface field + `Awake` adapter fallback), `clickSample` (AudioClip), `pauseButtonRoot` (GameObject), `stepDurationSec=1.0`, `popupLifetimeSec=0.9`, `goHoldSec=0.4`, `goPitch=1.5`, `normalColor=Color.white`, `goColor=new Color(1f, 0.843f, 0f, 1f)`. |
| `Assets/Scripts/UI/CountdownNumberPopup.cs` | `MonoBehaviour` | Per-instance animator. Fields: `Text text`, `Outline outline`, `spawnTime`, `lifetime`, `baseColor`. `Activate(startTime, lifetime, label, color)` resets state. `Update` advances `t = (Time.time - spawnTime) / lifetime`, lerps scale + alpha, deactivates at `t ≥ 1`. `TickForTest(simulatedTime)` guarded by `UNITY_EDITOR || UNITY_INCLUDE_TESTS`. |
| `Assets/Scripts/UI/IClickPlayer.cs` | interface | `void Play(float pitch)`. |
| `Assets/Scripts/UI/AudioSourceClickPlayer.cs` | adapter | Wraps an `AudioSource` + `AudioClip`. `Play(pitch)` sets `src.pitch = pitch; src.PlayOneShot(clip)`. |
| `Assets/Tests/EditMode/CountdownNumberPopupTests.cs` | EditMode tests | 4 tests (see §6). |
| `Assets/Tests/EditMode/CountdownOverlayTests.cs` | EditMode tests (UnityTest coroutine) | 5 tests (see §6). |
| `Assets/Tests/EditMode/GameplayControllerCountdownTests.cs` | EditMode tests | 3 tests (see §6). |

### 4.2 Modified files

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/GameplayController.cs` | Add `[SerializeField] private CountdownOverlay countdown`. Replace `audioSync.StartSilentSong(); playing = true;` in `BeginGameplay` with `countdown.BeginCountdown(() => { audioSync.StartSilentSong(); playing = true; })`. Add `internal void SetCountdownForTest(ICountdownOverlay c)`. |
| `Assets/Editor/SceneBuilder.cs` | Add private `BuildCountdownOverlay(Camera, AudioClip clickSample, GameObject pauseButtonRoot, Transform gameplayRoot)` method. Call site: after `BuildHUD(...)` returns (pauseButtonRoot is a direct reference to the `HUDPauseButton.gameObject`). Wire `countdown` SerializeField onto `GameplayController` via existing `SetField` helper. Scene save: `countdownCanvas.SetActive(false)` to match SP10 pattern. |
| `Assets/Scenes/GameplayScene.unity` | Scene snapshot regenerated by `SceneBuilder`; adds `CountdownCanvas` (inactive), its popup child, and `AudioSource` child. Reviewer diff will be large but mechanically produced. |

### 4.3 Timing parameter defaults

| Param | Value | Rationale |
|---|---|---|
| `stepDurationSec` | 1.0 | One second between numbers is rhythm-game standard. |
| `popupLifetimeSec` | 0.9 | Slightly under `stepDurationSec` so each number fully fades before the next appears (no overlap with single-instance reuse). |
| `goHoldSec` | 0.4 | Brief hold after GO! to let player register it before the first note arrives. |
| `goPitch` | 1.5 | ~+7 semitones, matches upbeat "kick-off" feel. Bounded well within Unity AudioSource pitch range [−3, 3]. |
| `normalColor` | `Color.white` (FFFFFFFF) | High contrast on both profile backgrounds (SP9 blue/yellow). |
| `goColor` | `new Color(1f, 0.843f, 0f, 1f)` (FFD700) | SP10 "Perfect" gold — reuses established success-signal color. |
| `fontSize` | 144 | SP10 settled at 72pt for judgment; countdown is the focal UI element for the 3 seconds → 2× scale. |
| Outline thickness | `(2, -2)` | SP10 convention. |
| Canvas sortingOrder | `JudgmentTextCanvas.sortingOrder + 1` | Countdown layered above judgment popups (irrelevant in practice since no judgments fire during countdown, but prevents future ordering surprises). |

### 4.4 Single-instance popup rationale

A round-robin pool (SP10 pattern) is the right default for **transient burst-y feedback**: taps arrive faster than a popup's lifetime, so multiple must co-exist. The countdown is the opposite — strictly sequential at 1.0 s spacing, with `popupLifetimeSec=0.9 < stepDurationSec=1.0`. At most one popup is ever active.

We keep the **popup logic in its own class** (not inlined into `CountdownOverlay`) to preserve the single-responsibility split: `CountdownOverlay` owns *sequencing*, `CountdownNumberPopup` owns *animation*. The single-instance optimization is a detail of the overlay, not of the popup.

If playtest reveals a need for overlapping popups (e.g., a future fancy transition where "1" is still fading as "GO!" appears), we upgrade `CountdownOverlay` to hold a small pool without touching `CountdownNumberPopup`.

---

## 5. Flow integration with existing screens

| Entry scenario | Trigger | Countdown invoked? |
|---|---|---|
| First-ever play (no calibration) | `GameplayController.ContinueAfterChartLoaded()` → `calibration.Begin(BeginGameplay)` → (user runs 8-click calibration) → `BeginGameplay()` | Yes, after calibration overlay Finish()es. |
| Calibrated player, fresh song | `GameplayController.ContinueAfterChartLoaded()` → `BeginGameplay()` directly | Yes. |
| Retry from ResultsScreen | User taps Retry → `ScreenManager.Replace(Gameplay)` → `OnReplaced(Gameplay)` → `ResetAndStart()` → `BeginGameplay()` | Yes. Fresh countdown each retry — intentional (a retry should feel like a restart). |
| Return from PauseScreen resume | `audioSync.Resume()` | **No.** Pause/resume happens mid-song after countdown already completed. Resume restarts only the song clock. |

### 5.1 Retry flow interaction

`ResetAndStart()` calls `spawner.ResetForRetry()`, `holdTracker.ResetForRetry()`, `judgmentSystem.ResetForRetry()`. None of these are affected by the countdown insertion — they already run in `ContinueAfterChartLoaded` before `BeginGameplay`. The countdown simply runs between spawner-init and audio-start on retry, identical to first play.

### 5.2 Calibration interaction

`CalibrationController.Finish()` calls `OverlayBase.Finish()` which `SetActive(false)`s the calibration canvas, then invokes `onDoneCallback` (= `BeginGameplay`). So the overlay disappears *before* the countdown canvas appears. No visual overlap concern.

### 5.3 Pause button lifecycle

`CountdownOverlay.BeginCountdown` sets `pauseButtonRoot.SetActive(false)` at entry, restores `SetActive(true)` before invoking `onComplete`. If `pauseButtonRoot` is already inactive at entry (e.g., during test scenarios), the method is idempotent.

---

## 6. Testing

### 6.1 `CountdownNumberPopupTests` (~4)

SP10 `JudgmentTextPopupTests` pattern. Pure EditMode, no PlayMode coroutine dependency.

| # | Name | Verifies |
|---|---|---|
| 1 | `Activate_SetsLabelColorAndInitialScale` | `text.text == "3"`, `text.color == Color.white`, `rt.localScale == Vector3.one * 1.5f` |
| 2 | `Tick_ScalePunchConverges` | `TickForTest(0.09)` (50% of 0.18) → scale ≈ 1.25. `TickForTest(0.18)` → scale == 1.0. `TickForTest(0.5)` → scale stays 1.0. |
| 3 | `Tick_AlphaFadeAfterFadeStart` | `TickForTest(0.54)` → alpha == 1.0. `TickForTest(0.9 * 1.0)` → alpha == 0 (end of lifetime).  |
| 4 | `Tick_DeactivatesOnExpiry` | `TickForTest(1.01)` → `gameObject.activeSelf == false` |

### 6.2 `CountdownOverlayTests` (~5)

`IClickPlayer` fake and `CountdownNumberPopup` stub injected via `SetDependenciesForTest`. Coroutine tested via `UnityTest` IEnumerator.

| # | Name | Verifies |
|---|---|---|
| 1 | `BeginCountdown_PlaysClickInOrder` | After full coroutine, `fakeClickPlayer.pitchCalls == [1.0f, 1.0f, 1.0f, 1.5f]` |
| 2 | `BeginCountdown_ActivatesPopupWithLabelSequence` | Captured `popup.Activate` label args == `["3", "2", "1", "GO!"]` |
| 3 | `BeginCountdown_GoPopupUsesGoldColor` | 4th `Activate` color == `new Color(1f, 0.843f, 0f, 1f)` within epsilon |
| 4 | `BeginCountdown_PauseButtonHiddenDuringSequence` | Immediately after `BeginCountdown` invocation, `pauseButtonRoot.active == false`. Immediately before `onComplete` invocation, `pauseButtonRoot.active == true`. |
| 5 | `BeginCountdown_InvokesOnCompleteAfterGoHold` | `onComplete` called after elapsed ≈ `3 * stepDurationSec + goHoldSec = 3.4s` (tolerance ±0.1s for coroutine frame granularity) |

### 6.3 `GameplayControllerCountdownTests` (~3)

`ICountdownOverlay` mock injected via `SetCountdownForTest`. Existing `GameplayController` tests that exercise `BeginGameplay` will also use this (expect ~0–2 touch-ups).

| # | Name | Verifies |
|---|---|---|
| 1 | `BeginGameplay_DelegatesToCountdownAndDefersAudio` | `mockCountdown.BeginCountdownCallCount == 1` after `BeginGameplay`; `audioSync.IsPlaying == false` (callback not yet invoked). |
| 2 | `CountdownCallback_StartsAudioAndSetsPlaying` | Invoke mock's captured `onComplete` → `audioSync.IsPlaying == true`, `playing == true` (verified via spawner activation in Update) |
| 3 | `Update_GatesOnCountdownPending` | Before `onComplete` invocation, `Update` takes the `!playing` early-return path (no end-of-song evaluation) |

### 6.4 Regression surface

- **179 existing EditMode tests** expected to remain green. The only `GameplayController`-level tests that may need touching are those that drive `BeginGameplay` directly and assume synchronous audio-start; those inject a fake countdown that auto-fires its callback.
- **49 pytest tests**: untouched (no pipeline change).

### 6.5 Manual playtest — Galaxy S22 R5CT21A31QB

- [ ] Fresh profile (나윤) cold-start: song select → calibration → "3/2/1/GO!" centered, clean animation, audible click on each, pitch-up on GO!
- [ ] Calibrated profile (소윤): song select → countdown immediately
- [ ] All four songs × easy/normal difficulties: first note lands cleanly after GO!, no perceived latency shift
- [ ] Pause button invisible during countdown; visible and responsive from GO! forward
- [ ] ResultsScreen → Retry → countdown replays
- [ ] LatencyMeter HUD (Debug build) — 60 fps steady through countdown and into gameplay, no GC spikes
- [ ] 2-minute Entertainer Normal session: `GC.Collect` count = 0 (Unity Profiler on-device)
- [ ] Return-from-pause: audio resumes without re-triggering countdown

---

## 7. Housekeeping

### 7.1 SP11-T9 — SP10 orphan worktree removal

`git worktree list` shows 8 registered worktrees. Filesystem `ls .claude/worktrees/` shows 9 directories. The difference is `suspicious-hodgkin-52399d`, a Windows MAX_PATH residue from an earlier session. `git` doesn't know it, so `git worktree prune` is a no-op.

Cleanup: `rmdir /s /q .claude\worktrees\suspicious-hodgkin-52399d` (single command, bundled into the SP11 PR).

---

## 8. Risks & mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| First note lands "too close" to GO! after countdown ends (player not ready) | Medium | UX — feels rushed | `goHoldSec=0.4` buffer + `scheduleLeadSec=0.5` AudioSync lead = ~0.9s from GO! fade to first audible note. Tunable post-playtest. |
| `IEnumerator` coroutine drift (Unity frame quantum) drifts the 1.0 s step over the 3-second span | Low | Visual jitter | Tested to ±0.1s tolerance in §6.2 #5. Human perception threshold on countdown pacing is ~±100ms; visual-only timing, no DSP coupling. |
| Audio pitch=1.5 `PlayOneShot` exceeds duration and overlaps next step | Low | Audio artifact | `calibration_click.wav` is ~50ms long; at pitch 1.5 becomes ~33ms. Well under `stepDurationSec=1.0`. |
| `CountdownCanvas` active at scene load (SP10 race) | Low | Visible countdown during non-gameplay screens | SceneBuilder scene-save-time `SetActive(false)` (SP10 pattern). `BeginCountdown` is the sole activator. |
| Test coroutine flakiness (`UnityTest` timing) | Medium | CI noise | Use generous ±0.1s tolerance in the one time-bound assertion (§6.2 #5). All other assertions are value-equality. |
| `GameplayController` existing tests break on new SerializeField | Medium | Local dev friction | `SetCountdownForTest` with default no-op mock allows existing tests to pass without real countdown wiring. |

---

## 9. Rollback

If SP11 introduces a regression discovered post-merge:

1. Revert the merge commit (single revert, since SP11 is delivered as one PR).
2. Scene regenerates on next `KeyFlow/Build Scene` — no manual cleanup needed.
3. No data migration (no UserPrefs keys, no asset changes).
4. `Assets/Audio/calibration_click.wav` stays (owned by SP5).

---

## 10. Acceptance criteria

- [ ] All 12 new EditMode tests green
- [ ] Existing 179 EditMode tests green with at most mechanical `SetCountdownForTest` injections
- [ ] pytest 49/49 green
- [ ] Manual playtest checklist §6.5 all passing on Galaxy S22
- [ ] APK ≤ 38.10 MB
- [ ] GC=0 on 2-min Entertainer Normal session
- [ ] `.claude/worktrees/suspicious-hodgkin-52399d` removed
- [ ] Completion report committed to `docs/superpowers/reports/2026-04-?-w6-sp11-countdown-completion.md`

---

## 11. File summary

**New (7):**
- `Assets/Scripts/UI/ICountdownOverlay.cs`
- `Assets/Scripts/UI/CountdownOverlay.cs`
- `Assets/Scripts/UI/CountdownNumberPopup.cs`
- `Assets/Scripts/UI/IClickPlayer.cs`
- `Assets/Scripts/UI/AudioSourceClickPlayer.cs`
- `Assets/Tests/EditMode/CountdownNumberPopupTests.cs`
- `Assets/Tests/EditMode/CountdownOverlayTests.cs`
- `Assets/Tests/EditMode/GameplayControllerCountdownTests.cs`

**Modified (3):**
- `Assets/Scripts/Gameplay/GameplayController.cs`
- `Assets/Editor/SceneBuilder.cs`
- `Assets/Scenes/GameplayScene.unity` (mechanical, regenerated)

**Deleted (1):**
- `.claude/worktrees/suspicious-hodgkin-52399d/` (filesystem directory, not git-tracked)

**Total LoC (C# new code, excluding tests):** ~200 lines across 5 files.
**Total LoC (tests):** ~300 lines across 3 files.
