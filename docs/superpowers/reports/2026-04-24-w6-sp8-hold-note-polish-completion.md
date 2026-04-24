# KeyFlow W6 Sub-Project 8 Completion Report — Hold-Note Polish Pass

**Date:** 2026-04-24 (merged to main 2026-04-24 as merge commit `5c58aed`)
**Branch:** `claude/w6-sp8-hold-note-polish` (worktree at `.claude/worktrees/w6-sp8-hold-note-polish`)
**Spec:** `docs/superpowers/specs/2026-04-23-keyflow-w6-sp8-hold-note-polish-design.md`
**Plan:** `docs/superpowers/plans/2026-04-23-keyflow-w6-sp8-hold-note-polish.md`
**Device:** Galaxy S22 (R5CT21A31QB) — PLAYTEST PASSED 2026-04-24 (see §6)
**Release APK:** `Builds/keyflow-w6-sp2.apk` — 37.14 MB (SP8-only); 39.89 MB post SP9 merge on main
**Unity version:** 6000.3.13f1

---

## 1. Summary

Addressed three user-reported hold-note pain points in one sub-project:

1. **"홀드 노트가 너무 많이 나온다"** — raised MIDI→chart HOLD classification threshold from 300 ms to 500 ms. Quarter notes at ≥120 BPM become TAPs; long sustains remain HOLDs. Regenerated 4 `.kfchart` files; Für Elise handled via in-place rewrite (hand-authored, no MIDI source).

2. **"홀드 중 화면 임팩트 약하다"** — new `LaneGlowController` (`Assets/Scripts/Feedback/LaneGlowController.cs`) pulses per-lane judgment-line SpriteRenderers at ~1 Hz (alpha 0.1–0.5) while a HOLD is active on that lane. Stack-only math in the `Update` path (Mathf.Sin + Color struct), no heap allocation.

3. **"홀드 중 처음 누를 때 음만 나온다"** — `HoldTracker` now seeds an **id-keyed** `Dictionary<int, HoldAudioState>` on tap-accept and retriggers the held pitch every 250 ms at 0.7 volume. Id-keying (not lane-keying) accommodates same-lane HOLD overlaps present in Debussy/Beethoven charts. `AudioSamplePool.PlayForPitch` gained a `volume` parameter with default `1f` (backward-compatible).

Bonus: discovered and documented that the empirical "indexer-set during foreach" pattern referenced in the spec **actually throws `InvalidOperationException`** on Unity 6.0.3 Mono (the spec Risks table anticipated this and provided the `List<int>` buffer fallback, which was adopted). A precedent correction worth remembering for future sub-projects.

148/148 EditMode tests green (135 prior + 2 AudioSamplePool volume + 6 HoldAudioRetrigger + 5 LaneGlowController); 38/38 pytest tests green (37 prior + 1 new threshold regression guard, boundary tests rewritten for 500 ms). Scene regenerated successfully via SP7-consolidated `KeyFlow/Build W4 Scene`.

Qualitative success criteria (spec §9): **OBJECTIVE criteria all met** (tests, code review). **SUBJECTIVE criteria pending device playtest** (§6).

## 2. Commits (8 on branch vs main baseline `9e3eac1`)

### Implementation (6)
- `6eb17b1` feat(w6-sp8): raise HOLD threshold 300 → 500 ms + regenerate 4 charts
- `266ebbe` feat(w6-sp8): add volume parameter to AudioSamplePool.PlayForPitch (amended from initial `286ff9c` per code-quality review to tighten silent-pass trap in default-volume test and thread volume through `PlayOneShot` fallback)
- `eb1db12` feat(w6-sp8): hold-note audio retrigger at 250 ms, 0.7 volume
- `7c710dc` feat(w6-sp8): LaneGlowController real implementation
- `c0b0774` chore(w6-sp8): wire LaneGlow and HoldTracker audio pool in SceneBuilder (amended from initial `52971bc` per code-quality review to add `whiteSprite` null-guard consistent with existing SceneBuilder helpers)
- `036639d` chore(w6-sp8): add missed LaneGlowControllerTests.cs.meta

### Documentation (1)
- `b0aed27` docs(w6-sp8): completion report (this file, device playtest deferred)

## 3. Files touched

**Modified:**
- `tools/midi_to_kfchart/pipeline/hold_detector.py` — one-line constant `HOLD_THRESHOLD_MS = 500`
- `tools/midi_to_kfchart/tests/test_hold_detector.py` — boundary tests rewritten for 500 ms; added `test_old_threshold_300ms_is_now_tap` as regression guard
- `tools/midi_to_kfchart/batch_w6_sp8.yaml` (new) — 3-song batch with comment explaining Für Elise exclusion
- `Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart` — in-place HOLD→TAP reclassification for notes with `dur < 500`; `totalNotes` unchanged
- `Assets/StreamingAssets/charts/beethoven_ode_to_joy.kfchart` — pipeline regeneration
- `Assets/StreamingAssets/charts/debussy_clair_de_lune.kfchart` — pipeline regeneration
- `Assets/StreamingAssets/charts/joplin_the_entertainer.kfchart` — pipeline regeneration
- `Assets/Scripts/Gameplay/AudioSamplePool.cs` — `PlayForPitch(int, float volume = 1f)` overload; `PlayOneShot(AudioClip, float volume = 1f)` overload added to thread volume through the `pitchSamples == null` fallback path (public surface expansion beyond spec §4.3; adopted in `266ebbe` after code-quality review caught that the original patch would silently drop volume when the pitch fallback fired — minor scope widening vs. a real-bug fix, accepted)
- `Assets/Scripts/Gameplay/HoldTracker.cs` — id-keyed `holdAudio` dict, `OnHoldStartTapAccepted(NoteController, int tapTimeMs)` signature, `[SerializeField] audioPool` + `laneGlow` fields, retrigger loop using two-phase `retriggerBuffer` pattern (spec Risks row 2 fallback), `ResetForRetry` clears new state, internal test hooks
- `Assets/Scripts/Gameplay/JudgmentSystem.cs` — one-line caller update at `:116` to pass `tapTimeMs`
- `Assets/Scripts/Gameplay/NoteController.cs` — `internal SetForTest(...)` method under test-gate
- `Assets/Editor/SceneBuilder.cs` — new `BuildLaneGlow` helper with null-guard; `BuildManagers` gains `Sprite whiteSprite` parameter; HoldTracker `laneGlow` / `audioPool` field wiring
- `Assets/Scenes/GameplayScene.unity` — regenerated with `LaneGlow` GameObject + 4 `Glow_{0..3}` children
- `Assets/Tests/EditMode/AudioSamplePoolTests.cs` — 2 new volume tests with defensive pre-set
- `Assets/Tests/EditMode/HoldAudioRetriggerTests.cs` (new) — 6 tests, driven by existing `ITimeSource` seam via `ManualClock`
- `Assets/Tests/EditMode/LaneGlowControllerTests.cs` (new) — 5 tests

**Created:**
- `Assets/Scripts/Feedback/LaneGlowController.cs` (+meta) — MonoBehaviour, `KeyFlow.Feedback` namespace
- `tools/midi_to_kfchart/batch_w6_sp8.yaml`
- `docs/superpowers/specs/2026-04-23-keyflow-w6-sp8-hold-note-polish-design.md` (on main)
- `docs/superpowers/plans/2026-04-23-keyflow-w6-sp8-hold-note-polish.md` (on main)

**Unchanged (spec §3 non-goals):**
- `Assets/Scripts/Gameplay/AudioSyncManager.cs` — the existing `ITimeSource` seam covered all test needs; no new hooks added
- `Assets/Scripts/Feedback/HapticService.cs`, `FeedbackDispatcher.cs` — spec explicitly deferred sustained haptic during holds to a later polish pass

## 4. HOLD density — before vs. after threshold change

Data captured from `.kfchart` files pre- and post-regeneration.

| Song | Difficulty | Before total / HOLD % | After total / HOLD % | Notes |
|------|------------|-----------------------|----------------------|-------|
| beethoven_fur_elise | EASY | 73 / 2.7% | 73 / 2.7% | Existing HOLDs all ≥ 500 ms → no change |
| beethoven_fur_elise | NORMAL | 589 / 11.9% | 589 / 5.4% | In-place rewrite dropped 38 HOLDs → TAPs |
| beethoven_ode_to_joy | EASY | 46 / 100.0% | 46 / 84.8% | 7 quarters at 100 BPM TAP'd; majority still HOLD (BPM 100) |
| beethoven_ode_to_joy | NORMAL | 68 / 100.0% | 68 / 85.3% | Same |
| debussy_clair_de_lune | EASY | 107 / 94.4% | 506 / 47.2% | Big content shift — see §5 |
| debussy_clair_de_lune | NORMAL | 160 / 93.8% | 759 / 48.7% | Same |
| joplin_the_entertainer | EASY | 321 / 41.4% | 648 / 6.3% | Big content shift — see §5 |
| joplin_the_entertainer | NORMAL | 481 / 41.2% | 972 / 6.3% | Same |

## 5. Notable side-effect: pre-existing pipeline under-density, now corrected

Three of the 4 charts (Clair de Lune + Entertainer) **grew total note counts dramatically** (2× to 5×) despite no change to `target_nps` settings in the YAML. Analysis traces this to the SP2-era `density.thin()` truncation bug (documented in `memory/project_w6_sp2_complete.md`):

Under the old 300 ms threshold, so many notes classified as HOLD that `density.thin()` hit its hold-saturation edge case and truncated aggressively. With fewer notes becoming HOLDs under the 500 ms rule, density budget freed up and more notes survived. Sanity check against target NPS × song duration:

| Song | Difficulty | target_nps | duration | Ideal count | Old count (% of ideal) | New count (% of ideal) |
|------|------------|-----------:|---------:|------------:|-----------------------:|-----------------------:|
| Clair de Lune | EASY | 1.5 | 320 s | 480 | 107 (22%) | 506 (105%) |
| Clair de Lune | NORMAL | 2.8 | 320 s | 896 | 160 (18%) | 759 (85%) |
| Entertainer | EASY | 2.2 | 255 s | 561 | 321 (57%) | 648 (116%) |
| Entertainer | NORMAL | 4.0 | 255 s | 1020 | 481 (47%) | 972 (95%) |

Old charts were severely under-dense (Clair de Lune NORMAL at 18% of target). New charts land within ±16% of ideal. Net effect: Threshold change ALSO acted as a de-facto fix for the density truncation bug. Follow-up consideration: the long-standing `truncate_charts.py` workaround may no longer be needed; a dedicated regression test against NPS target ± tolerance could codify this.

Subjective implication for playtest: Clair de Lune NORMAL went from 160 sparse-but-held-dominant notes to 759 denser-with-half-holds notes. Noticeably different song to play. Ode to Joy remains essentially "hold all the things" at both difficulties (small number of notes, all long at BPM 100). Per spec §3 non-goals, per-song threshold tuning stays out of scope — any follow-up polish for Ode to Joy / Clair de Lune should be a separate SP.

## 6. Device playtest — PASSED (2026-04-24)

Playtest executed on Galaxy S22 (R5CT21A31QB) across 5 APK iterations during the same session, each driven by in-the-moment findings. Subjective pain points + follow-ups:

**Initial playtest (after APK from commit `0821694`-era) — 3 findings reported:**
- #1 "홀드 노트가 너무 많이 나온다 / 너무 긴 게 있고 둘로 나뉘어진 것 같은 게 구분이 없어서 누르고 있으면 미스가 뜬다"
- #2 "홀드 누르고 있을 때 화면 임팩트 없다"
- #3 "Clair de Lune은 홀드 중 소리 이어지지 않는 것도 있고 이어지는 것도 있다"

**Iteration 1 (glow v1 = alpha 0.4-0.9, sortingOrder 2):**
- Glow still not visible on device (sortingOrder change theoretically correct, but tile body covered the halo).

**Iteration 2 (chart-merge pipeline + glow v2 = alpha 0.5-1.0, 0.8 u tall):**
- Chart merge eliminated same-lane adjacent HOLD pairs from 184 (Clair de Lune NORMAL) to 0. User no longer "holds through and misses".
- Glow v2 still reported as "no impact".
- New finding: "긴 홀드가 내려오다 중간에 사라지듯 끊긴다" + "아주 긴 건 위에서부터 안 내려오고 갑자기 중간부터 내려온다".
  - Root cause: NoteController lerp moved tile CENTER to judgmentY at hit time; for tall HOLDs (up to 14 world units after SP8 merge enabled 4s holds), the tile's bottom was already below judgment line at spawn and its top never appeared from camera top.

**Iteration 3 (NoteController bottom-pivot + LaneGlow in tap zone below judgment line):**
- Tile BOTTOM now reaches judgmentY at hit time; tile keeps scrolling through judgment line until top reaches it at hold end.
- Glow moved to y=-4 (tap zone below judgment, 2 u tall, spanning y=-5..-3). Tile never covers it by design.
- User reported: long holds now scroll from top naturally; glow visible in tap zone.

**Iteration 4 (missed-HOLD grey + scroll-through):**
- User reported: "긴 홀드를 처음에 놓치면 없어져 버린다" — missed holds Destroy'd ~100 ms after window close, jarring for 4s tall tiles.
- Fix: HOLD auto-miss greys tile and schedules Destroy at `hitTimeMs + durMs`; tile keeps scrolling via the normal lerp path. TAP unchanged (immediate Destroy).
- User confirmation: "잘된다".

**Merged APK smoke test (post SP8+SP9 merge):**
- All 5 SP8 iteration features still working alongside SP9 profile flow.
- Owner's 2 children tested nayoon→blue-bg and soyoon→yellow-bg with assorted songs including Clair de Lune long holds.

**Profiler attach:** DEFERRED (not executed in this session). Remains a carry-over to confirm SP3 GC-free baseline under the 5-iteration feature surface. Low risk: no new `Update`-hot-path allocations introduced in iterations 3-4 (scroll math is still stack-only; `Destroy(gameObject, delay)` schedules via Unity scheduler, not per-frame allocation).

Remaining checklist for the playtest pass:
1. Close any interactive Unity Editor on this project (IL2CPP link fails otherwise).
2. Release APK build: `"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath "$(pwd)" -executeMethod KeyFlow.Editor.ApkBuilder.Build -logFile - -quit 2>&1 | tail -120`. Expected output at `Builds/keyflow-w6-sp2.apk`.
3. `adb install -r Builds/keyflow-w6-sp2.apk` on S22.
4. Playtest acceptance:
   - #1 "홀드가 덜 나온다" — Entertainer/Für Elise/Clair de Lune NORMAL runs; confirm hold density feels reduced. Ode to Joy remains hold-dominant (expected).
   - #2 "홀드 중 화면이 살아있다" — confirm per-lane judgment-line glow pulses during every hold; glow stops immediately on release/break.
   - #3 "홀드 중 소리가 이어진다" — confirm retrigger audible every ~250 ms during hold at lowered volume; stops on release/break.
5. Profile build: `-executeMethod KeyFlow.Editor.ApkBuilder.BuildProfile` → `Builds/keyflow-w6-sp8-profile.apk`. Unity Profiler attach during 2-minute Entertainer Normal run. Target: `GC.Collect == 0`; per-frame Reserved-Total delta ≤ SP3 baseline.
6. Retry mid-hold + Pause mid-hold smoke tests: confirm laneAudio and lane glow clear cleanly on retry; no retrigger during pause, glow freezes.

After playtest is complete, update this file's header block (Device line, Release APK line) and fill in results. Any new carry-overs from playtest get logged in §9.

## 7. Test suite growth

| Suite | Before | After | Delta |
|-------|--------|-------|-------|
| pytest (`tools/midi_to_kfchart/`) | 37 | 38 | +1 (threshold regression guard; existing boundary tests rewritten rather than added) |
| Unity EditMode | 135 | 148 | +13 (AudioSamplePool +2, HoldAudioRetrigger +6, LaneGlowController +5) |

Full EditMode suite runs in ~0.7 s; full pytest in ~1.7 s.

## 8. Technical notes worth preserving

**Dict-indexer-set-during-foreach is NOT safe in Unity 6.0.3 Mono.** The spec's Risks row 2 anticipated this and provided the `List<int>` buffer fallback pattern, which was adopted. The false precedent ("matches `HoldStateMachine.Tick`'s e.state = X") was caught and corrected in spec revision `5bb21b8`: `HoldStateMachine.Entry` is a class (reference type), so mutating `e.state` never writes back to the dictionary. `HoldAudioState` is a struct, so `holdAudio[key] = st` IS a dictionary mutation, and Mono/IL2CPP's version counter check fires `InvalidOperationException`. Two-phase write (collect keys to a pre-sized `List<int> retriggerBuffer`, write values after iteration ends) preserves GC-free semantics.

**ITimeSource seam is the established test-time-travel mechanism.** No new `SetSongTimeMsForTest` / `SetIsPlayingForTest` hooks were added to `AudioSyncManager` — the existing `ITimeSource` injection + `StartSilentSong()` + `Pause()` / `Resume()` API covers all HoldAudioRetrigger test needs. Any future test that needs song-time manipulation should follow `AudioSyncPauseTests.cs:9-26`'s `ManualClock : ITimeSource` pattern.

**Für Elise chart is hand-authored.** No MIDI source in `tools/midi_to_kfchart/midi_sources/`. Under threshold changes, its chart must be rewritten in-place rather than re-pipelined. The `batch_w6_sp8.yaml` includes a comment explaining the exclusion.

**LaneGlowController pause-gate check uses `audioSync.IsPaused`.** The early-return correctly freezes the visual pulse when the player pauses the song. No EditMode test exercises this branch directly — pause behavior is verified via the HoldAudioRetrigger tests' `NoRetrigger_WhilePaused` (covers the shared `IsPaused` gate in HoldTracker.Update, which drives what lanes the glow would be active on).

## 9. Carry-overs

1. **Device playtest + profiler attach + APK size + Release APK filename update** — see §6.
2. **Per-song HOLD threshold tuning for slow pieces** — Ode to Joy (BPM 100) and Clair de Lune (BPM 60) remain hold-dominant at 500 ms because their quarter notes exceed 500 ms. If playtest feels unbalanced, a follow-up SP could tune threshold per-song or introduce a `HOLD_THRESHOLD_MS` override in the `batch_*.yaml` schema.
3. **`truncate_charts.py` may be obsolete** — §5 shows the SP2 density.thin() truncation bug is now masked by the threshold change. An NPS-target test would codify whether the truncate workaround is still needed for any song.
4. **TestResults-*.xml gitignore** — Unity batch-mode test runs leave ~10 transient XML files per worktree. Should be added to `.gitignore` in a separate housekeeping pass.
5. **`tapInput != null` guard in HoldTracker.Update** — cosmetic: the guard is a test-only code path (production always wires tapInput via SerializeField). Per code-quality review recommendation, could be gated by `#if UNITY_EDITOR || UNITY_INCLUDE_TESTS` or removed once a production TapInputHandler is always guaranteed. Non-blocking; defer to a cleanup SP.
6. **Pause-branch unit test for LaneGlowController** — code-quality review recommended but marked non-blocking. Would tighten coverage if LaneGlow pause behavior is ever modified.
7. **Hold-retrigger Settings exposure** — spec §3 non-goal. If user wants to tune retrigger interval / volume / glow intensity in-game, a follow-up SP adds them to SettingsScreen.
8. **HoldTracker + LaneGlowController integration test** — final-review flagged gap: no EditMode test wires both via `SetDependenciesForTest(laneGlow: ...)` to verify On at tap-accept, Off at Completed/Broken, Clear at retry. Each class is tested in isolation; the integration path is covered only by device playtest. Non-blocking; a one-test addition would close the gap cheaply.
9. **`retriggerBuffer` pre-size at LaneCount * 2** — fine for current charts (max observed same-lane overlap is 2 from SP8 discovery), but 3+ simultaneous holds per lane would realloc. Revisit if polyphony grows.

## 10. Stale worktree directories (Windows MAX_PATH)

At session start, `.claude/worktrees/` contained 5 registered worktrees + 3 stale physical dirs (pre-existing from SP1/SP2/SP3 per `memory/project_w6_sp3_complete.md` §40). SP8's worktree `w6-sp8-hold-note-polish` will likely join them as undeletable until Unity's `Library/Bee/` paths are pruned. Not blocking, but worth tracking across SPs.
