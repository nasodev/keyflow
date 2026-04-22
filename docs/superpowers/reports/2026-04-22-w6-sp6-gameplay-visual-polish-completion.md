# KeyFlow W6 Sub-Project 6 Completion Report — Gameplay Visual Polish

**Date:** 2026-04-23 (spec/plan/impl spanned 2026-04-22 into 2026-04-23)
**Branch:** `main` (executed directly per user consent; no dedicated worktree)
**Spec:** `docs/superpowers/specs/2026-04-22-keyflow-w6-sp6-gameplay-visual-polish-design.md`
**Plan:** `docs/superpowers/plans/2026-04-22-keyflow-w6-sp6-gameplay-visual-polish.md`
**Device:** Galaxy S22 (R5CT21A31QB), Android 16, arm64-v8a
**Release APK:** `Builds/keyflow-w6-sp2.apk` (36.92 MB; filename kept pre-bump per user direction)
**Unity version:** 6000.3.13f1

---

## 1. Summary

Shipped three bundled gameplay-scene visual upgrades driven by a concrete user-supplied reference image (Magic-Tiles-style green-gradient/black-tiles reference):

1. **Full-width tiles** — `LaneAreaWidth` 4f → 9f; note prefab `localScale` (0.8, 0.4, 1) → (2.25, 0.4, 1); note color cream → near-black `(0.08, 0.08, 0.12)` matching the reference.
2. **Live combo HUD** — new `ComboHUD` MonoBehaviour polls `JudgmentSystem.Score.Combo`, toggles `comboText.enabled` (not `gameObject.SetActive`, avoiding the Update-trap bug surfaced during spec review), and allocates only on combo-change frames. GC-free on unchanged frames; ~600 taps/song × 28 B = 17 KB worst-case over a full session.
3. **User-supplied blue gradient background** — `Assets/Sprites/background_gameplay.png` (941×1672, 1.02 MB source) rendered via a UI `Image` on a dedicated `BackgroundCanvas`. Initial `ScreenSpaceOverlay` render mode regressed on device (covered world-space gameplay entirely — user reported "파란 배경만 나온다"); fixed to `ScreenSpaceCamera` with `planeDistance=50`, which z-sorts behind world-space SpriteRenderers correctly.

`BackgroundImporterPostprocessor` (sibling of `PianoSampleImportPostprocessor`) enforces Sprite / no-mipmap / Android ASTC_4x4 so settings survive re-imports and fresh clones. Judgment line + lane divider colors retuned for palette harmony against the new brighter background.

All runtime gameplay code untouched: `ScoreManager`, `JudgmentSystem`, `TapInputHandler`, `NoteSpawner`, `HoldTracker`, `LaneLayout`, `LanePitches` — zero changes. SP5 calibration click, SP4 haptics+particles, SP3 GC=0 baseline, SP1 multi-pitch all preserved.

Qualitative success criterion (spec §1) met: user confirmed "테스트완료" after the `ScreenSpaceCamera` fix — scene reads as Magic-Tiles-style polished with clear combo feedback and no perceived input/audio regression.

## 2. Commits (11 on branch vs SP5 baseline `f43ceaa`)

### Design + planning (4)
- `4ad2dd3` docs(w6-sp6): design spec
- `a5e97d4` docs(w6-sp6): spec review round-1 fixes
- `5158c50` docs(w6-sp6): spec review round-2 fixes
- `440a22c` docs(w6-sp6): spec round-3 nit
- `cb5fed4` docs(w6-sp6): implementation plan — 12 bite-sized tasks

### Implementation (10)
- `896c0cb` feat(w6-sp6): BackgroundImporterPostprocessor + imported background .meta
- `d9f5c95` feat(w6-sp6): ComboHUD scaffold (empty Update, internal test hooks)
- `f89d61f` test(w6-sp6): ComboHUD 4 EditMode tests (3 failing, TDD anchor)
- `e8ac061` feat(w6-sp6): ComboHUD.Update — poll, toggle enabled, alloc on change only
- `ced190d` feat(w6-sp6): SceneBuilder full-width tiles + reference-black note color
- `0e41964` feat(w6-sp6): SceneBuilder BuildBackgroundCanvas + asset load guard
- `bd852ff` feat(w6-sp6): SceneBuilder retune judgment line + lane divider for new bg
- `a3728f1` feat(w6-sp6): SceneBuilder wire ComboHUD + Text into HUDCanvas
- `eee84a9` chore(w6-sp6): regenerate GameplayScene.unity with SP6 SceneBuilder changes
- `0b394c9` chore(w6-sp6): re-run W6SamplesWireup after SceneBuilder regen

### Device-playtest fix (1)
- `e144564` fix(w6-sp6): BackgroundCanvas ScreenSpaceOverlay→ScreenSpaceCamera
  - Also committed missed `background_gameplay.png` (Task 1 only committed `.meta`) and regenerated `Note.prefab` (Task 9 only picked up `GameplayScene.unity`).

## 3. Files touched

**Created (runtime):**
- `Assets/Scripts/UI/ComboHUD.cs` — 48 lines

**Created (Editor tooling):**
- `Assets/Editor/BackgroundImporterPostprocessor.cs` — 25 lines

**Created (asset, committed):**
- `Assets/Sprites/background_gameplay.png` — 1,042,755 bytes user-supplied
- `Assets/Sprites/background_gameplay.png.meta` — auto-generated after postprocessor first import

**Created (tests):**
- `Assets/Tests/EditMode/ComboHUDTests.cs` — 4 tests

**Modified:**
- `Assets/Editor/SceneBuilder.cs` — `LaneAreaWidth` 4f→9f, note prefab scale+color, `BuildBackgroundCanvas` method (+35 lines), ComboHUD wire-up (+23 lines), judgment line + lane divider colors, load guard
- `Assets/Scenes/GameplayScene.unity` — regenerated twice (Task 9 + regression fix)
- `Assets/Prefabs/Note.prefab` — regenerated with new scale + color (Task 5 effects flushed in Task 9)

**NOT modified (runtime gameplay guardrails):**
- `Assets/Scripts/Gameplay/ScoreManager.cs`, `JudgmentSystem.cs`, `TapInputHandler.cs`, `NoteSpawner.cs`, `HoldTracker.cs`, `LaneLayout.cs`, `LanePitches.cs`, `HoldStateMachine.cs`
- `Assets/Scripts/Calibration/*`, `Assets/Scripts/Feedback/*`
- `Assets/Audio/*` (all SP1/SP5 sound assets)
- `Assets/Editor/W6SamplesWireup.cs`, `CalibrationClickBuilder.cs`, `FeedbackPrefabBuilder.cs`, `PianoSampleImportPostprocessor.cs`, `ApkBuilder.cs`

## 4. Tests

- Baseline (SP5 merged): 131
- SP6 after: **135** (+4)
  - `ComboHUDTests`: 0 → 4 (new file)
    - `HidesWhenComboZero`
    - `ShowsAndUpdatesTextWhenComboIncreases`
    - `HidesWhenComboResetsToZero`
    - `DoesNotReassignTextWhenComboUnchanged`
- 135/135 green after Tasks 4, 5, 6, 7, 8, 9, 10, and regression fix. TDD anchor working: Task 3 committed with 3 failing tests; Task 4 turned them green.

## 5. APK size

| Stage | Size | Delta vs prev |
|---|---|---|
| SP5 release baseline | 34.29 MB | — |
| SP6 first release build (with Overlay bug) | 36.92 MB | +2.63 MB |
| SP6 final (after regression fix) | **36.92 MB** | identical |

+2.63 MB vs SP5 — larger than spec's predicted +0.2-0.4 MB. Main driver is the 1.02 MB background PNG + Unity's ASTC 4x4 compressed runtime variant still being meaningfully larger than expected (~1.5-2 MB contribution). Also possible: build-artifact variance (SP5 was actually 1.66 MB smaller than SP4 unexpectedly, so SP6's +2.63 may partly be returning toward the SP4 baseline of 35.95 MB). Not investigated deeper.

- spec §7 binding `< 40 MB`: ✅ 36.92 MB
- spec §2.3 aspirational `< 35 MB`: ⚠️ missed by 1.92 MB (SP5 was the first W6 SP to hit this; SP6 regresses due to background image)

## 6. Device verification (Galaxy S22)

### 6.1 Playtest result

User reported "파란배경만 나온다" on first install (regression — see §6.2), then "테스트완료" after the fix rebuild. Final state: 7-item checklist all passing per user confirmation.

| # | Item | Result |
|---|---|---|
| 1 | Blue gradient bg + geometric pattern + bottom cloud glow visible | ✅ |
| 2 | Tiles fill screen width edge-to-edge | ✅ |
| 3 | Judgment line faintly visible | ✅ |
| 4 | Lane dividers harmonize with background | ✅ |
| 5 | ComboHUD hide-at-zero, updates live, hides on Miss | ✅ |
| 6 | No tap latency / judgment regression vs SP5 | ✅ |
| 7 | No frame hitches during 2-min session | ✅ |

### 6.2 Regression discovered during playtest

**`ScreenSpaceOverlay` covers all world-space gameplay.** Initial implementation used `RenderMode.ScreenSpaceOverlay` for `BackgroundCanvas` with `sortingOrder=-100`. On device, only the blue background rendered — notes, lane dividers, judgment line, and even the HUD pause button and ComboHUD were invisible.

**Root cause:** `sortingOrder` on `ScreenSpaceOverlay` canvases only sorts between Overlay canvases, NOT between an Overlay canvas and world-space SpriteRenderers. Overlay canvases always paint on top of world-space rendering regardless of sortingOrder. A negative sortingOrder just means "painted first among overlays" — still above world.

**Fix:** Changed `BackgroundCanvas.renderMode` to `ScreenSpaceCamera` with `worldCamera = camera` and `planeDistance = 50f`. Camera at z=-10, plane at z=40, world-space sprites at z=0 → orthographic z-sort puts sprites in front of the canvas plane. Commit `e144564`.

Also surfaced in the same commit: (a) `background_gameplay.png` itself had never been committed (Task 1 only added `.meta`); fresh clones would have errored on SceneBuilder load. (b) `Note.prefab` regeneration from Task 5's scale/color changes was only flushed in Task 9's scene rebuild but wasn't git-added at the time.

**Lessons:** When implementing ScreenSpace*Overlay* canvases that need to sit behind world-space rendering, don't. Use `ScreenSpaceCamera` or `WorldSpace` render mode. This could become a memory entry.

## 7. Post-SP6 carry-overs

**New carry-overs raised:**

- **APK +2.63 MB not fully accounted.** Background PNG is 1 MB source, ASTC runtime ~0.8 MB, leaves ~1-1.5 MB unexplained. Build-artifact inspection via Gradle `--stats` would clarify.
- **`Text.text = int.ToString()` per combo change allocates.** Spec accepts this (~17 KB/song), but a zero-GC version would require a fixed-size ASCII char buffer + `Text.cachedTextGenerator` direct manipulation. Deferred; not worth the complexity for MVP.
- **SP4 SceneBuilder ↔ W6SamplesWireup coupling trap** still active — SP6 re-hit it on Task 10 (scene diff was non-empty after wireup). Integration of wireup into SceneBuilder is still an open carry-over SP candidate.

**Remaining W6 priorities:**
- **W6 #6 2nd-device test** — separate SP (needs additional devices).

**Remaining SP3/SP4/SP5 carry-overs (unchanged by SP6):**
- SP3: hold-note audio feedback
- SP4: `AudioSource.Play()` per-tap ~1.6 KB allocation (SoundHandle.CreateChannel)
- SP4: APK filename bump (still `keyflow-w6-sp2.apk`)
- SP4: `SettingsScreen.creditsLabel` scene wireup relocation

## 8. Spec guardrails — verdict

| Guardrail | Result |
|---|---|
| EditMode 131 tests remain green | ✅ 135/135 including new 4 |
| Device playtest checklist (§6.3 all 7 items) | ✅ after regression fix (7/7) |
| APK < 40 MB binding | ✅ 36.92 MB |
| APK < 35 MB aspirational | ⚠️ 36.92 MB (missed by 1.92 MB) |
| No runtime gameplay code changes | ✅ (all `Assets/Scripts/Gameplay/*` untouched) |
| SP3 GC=0 baseline preserved | ✅ (ComboHUD alloc only on change frames) |
| SP1 multi-pitch audio preserved | ✅ (W6SamplesWireup re-run Task 10) |
| Deterministic asset setup | ✅ (BackgroundImporterPostprocessor enforces settings) |

## 9. Verdict: ship

SP6 delivers three bundled visual upgrades with user's reference-image fidelity, EditMode + device playtest all green after the one-commit regression fix. Ship (already on `main`).

Next W6 candidates per weekly goal:
- W6 #6 2nd-device test — the last W6 item
- Then transition to W7 "3~5종 기기, 마지막 버그 수정" per v2 spec §9

Carry-over SP candidates for future:
- SceneBuilder ↔ W6SamplesWireup fold-in (DX improvement)
- Audio per-tap allocation investigation (GC hygiene)
- Hold-note audio feedback (SP3 item)
