# KeyFlow W6 Sub-Project 9 Completion Report — Profile Start Screen + Per-Profile Background

**Date:** 2026-04-24
**Branch:** `claude/w6-sp9-profile-start-screen` (worktree at `.claude/worktrees/w6-sp9-profile-start-screen`)
**Spec:** `docs/superpowers/specs/2026-04-24-keyflow-w6-sp9-profile-start-screen-design.md`
**Plan:** `docs/superpowers/plans/2026-04-24-keyflow-w6-sp9-profile-start-screen.md`
**Device:** Galaxy S22 (R5CT21A31QB) — PLAYTEST DEFERRED (see §6)
**Release APK:** `Builds/keyflow-w6-sp2.apk` — NOT YET REBUILT (see §6)
**Unity version:** 6000.3.13f1

---

## 1. Summary

Added a profile-selection start screen in front of the existing `Main → Gameplay → Results` flow, with two invisible hit-boxes over `start.png`'s 시작하기 buttons. Tapping 나윤 → blue gameplay background (current `background_gameplay.png`); tapping 소윤 → new `background_yellow.png`. Android launcher icon updated to `img/icon.png`.

Session-scoped profile (no `PlayerPrefs`); profile resets on every launch. Scores/calibration/settings still shared between children per spec §3 non-goal. MainScreen's dark-navy background is unchanged.

Qualitative success criteria (spec §9) split cleanly by category:
- **Objective criteria met**: all EditMode tests green (150/150, +15 vs baseline), all scope declared in plan §3 shipped, scene regeneration succeeded, Player Settings diff cleanly scoped to icon keys.
- **Subjective criteria pending**: launcher-icon visual confirmation, 시작하기 hit-box visual alignment, yellow gameplay background visual confirmation, profiler `GC.Collect == 0` regression guard. All blocked on device playtest (user deferred; see §6).

## 2. Commits (7 on branch vs main baseline `42f8283`)

### Implementation (6)
- `0ee4717` feat(w6-sp9): import start.png, yellow-bg.png, icon.png + cover yellow in background postprocessor
- `2644b10` feat(w6-sp9): Profile enum + SessionProfile static
- `8e28011` feat(w6-sp9): BackgroundSwitcher for runtime blue/yellow toggle
- `a107b6a` feat(w6-sp9): AppScreen.Start + StartScreen + profile-background-apply hook (bundled Tasks 4+5; also bumps `BackgroundSwitcher.Apply` to `virtual` for test-spy interception)
- `b178d22` chore(w6-sp9): SceneBuilder integration + regenerate GameplayScene
- `88ab179` chore(w6-sp9): Android launcher icon → Assets/Textures/icon.png

### Documentation (1)
- this file

## 3. Files touched

**Modified:**
- `Assets/Editor/BackgroundImporterPostprocessor.cs` — `TargetPath` → `TargetPaths[]` allowlist covering both `background_gameplay.png` and `background_yellow.png`.
- `Assets/Editor/SceneBuilder.cs` — new `BuildStartCanvas` + `BuildInvisibleButton` helpers; `BuildBackgroundCanvas` signature changed to `(Sprite blueBg, Sprite yellowBg, Camera cam) → BackgroundSwitcher`; `Build()` loads 3 sprites; ScreenManager wiring adds `startRoot` + `backgroundSwitcher`.
- `Assets/Scripts/UI/ScreenManager.cs` — `AppScreen` gains `Start` at enum head; `startRoot` + `backgroundSwitcher` SerializeFields; `Replace` fires `BackgroundSwitcher.Apply` on Gameplay; `HandleBack` Main→Start, Start→double-back quit with `Debug.Log`.
- `Assets/Scripts/Feedback/BackgroundSwitcher.cs` — one-line retroactive change in Task 4+5: `public void Apply` → `public virtual void Apply`.
- `Assets/Tests/EditMode/ScreenManagerTests.cs` — startCanvas in Setup/TearDown; startCanvas assert in `Replace_Main_ActivatesOnlyMainRoot`; 4 new tests; `BackgroundSwitcherSpy` companion class.
- `Assets/Scenes/GameplayScene.unity` — regenerated.
- `ProjectSettings/ProjectSettings.asset` — Android Legacy icon (all 6 density buckets) references `Assets/Textures/icon.png`. No other keys changed.

**Created:**
- `Assets/Sprites/background_start.png` (+meta) — imported from `img/start.png`.
- `Assets/Sprites/background_yellow.png` (+meta) — imported from `img/yellow-bg.png`.
- `Assets/Textures/icon.png` (+meta) — imported from `img/icon.png`.
- `Assets/Scripts/Common/Profile.cs` (+meta) — `Profile` enum + `SessionProfile` static.
- `Assets/Scripts/Feedback/BackgroundSwitcher.cs` (+meta).
- `Assets/Scripts/UI/StartScreen.cs` (+meta).
- `Assets/Editor/SP9IconSetter.cs` (+meta) — one-shot utility + `KeyFlow/Apply Android Icon (SP9)` menu for future Player Settings resets.
- `Assets/Tests/EditMode/ProfileTests.cs` (+meta).
- `Assets/Tests/EditMode/BackgroundSwitcherTests.cs` (+meta).
- `Assets/Tests/EditMode/StartScreenTests.cs` (+meta).
- this file (`docs/superpowers/reports/2026-04-24-w6-sp9-profile-start-screen-completion.md`).

**Unchanged (spec §3 non-goals):**
- `Assets/Sprites/background_gameplay.png` — NOT renamed (postprocessor hardcoded the path; rename would silently break import settings enforcement).
- MainScreen dark-navy background.
- `UserPrefs.cs` / score schema — no per-profile partitioning.

## 4. Hit-box coordinates — measured, not estimated

Task 6 implementer performed programmatic color-segmentation of the purple/pink button bodies in `img/start.png` (native 853 × 1844 px), producing these bounding boxes:

- Nayoon 시작하기: pixel bbox ≈ (41, 1069)..(411, 1142), center (226, 1106), size 370 × 73 px.
- Soyoon 시작하기: pixel bbox ≈ (452, 1069)..(799, 1142), center (626, 1106), size 347 × 73 px.

Converted to normalized anchors (Unity bottom-origin, `y = (H_px - Cy_top) / H_px`):
- `NAYOON_ANCHOR = Vector2(0.265f, 0.400f)` — committed in `SceneBuilder.cs`.
- `SOYOON_ANCHOR = Vector2(0.734f, 0.400f)`.
- `BUTTON_SIZE   = Vector2(280f, 120f)` — 280 ref-units wide keeps a 57-unit gap between the two hit-boxes; 120 tall adds generous thumb-forgiveness around the 51-unit visible button height. Intentionally larger than drawn rect to avoid edge-miss.

**Note**: the original spec §4.2 suggested `~0.49` for the Y anchor based on controller's visual estimate. The programmatic measurement landed at 0.40. Trust the measurement; device playtest §6 item 2 is the final confirmation.

## 5. Test suite growth

| Suite | Before (branch parent 42f8283) | After | Delta |
|-------|---------|-------|-------|
| pytest (`tools/midi_to_kfchart/`) | 37 | 37 | 0 (SP9 is UI-only; no pipeline change) |
| Unity EditMode | 135 | 150 | +15 (ProfileTests +3, BackgroundSwitcherTests +4, StartScreenTests +4, ScreenManagerTests +4) |

All 150 EditMode tests pass in ~1.0 s. EditMode test count now 15 tests higher than SP8 main (which has 135 — SP8's +13 tests live on its own un-merged branch).

## 6. Device playtest — DEFERRED

User deferred device playtest pending completion of this SP. Remaining checklist:

1. Close any interactive Unity Editor on this project (IL2CPP link fails otherwise per `memory/feedback_unity_batch_mode.md`).
2. Release APK build:
   ```
   "C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath "$(pwd)" -executeMethod KeyFlow.Editor.ApkBuilder.Build -logFile - -quit 2>&1 | tail -120
   ```
   Expected output: `Builds/keyflow-w6-sp2.apk` refreshed.
3. `adb install -r Builds/keyflow-w6-sp2.apk` on S22.
4. Playtest acceptance checks:
   - **Launcher icon**: new piano-kid icon appears on Android home/app drawer (NOT the Unity default logo).
   - **Start screen renders**: `start.png` fills screen cleanly on 9:19.5 S22 aspect.
   - **Nayoon tap**: tap center of 나윤 시작하기 button → MainScreen. Pick any song → BLUE gameplay background.
   - **Soyoon tap**: back to Start via Android back (Main→Start). Tap 소윤 시작하기 → MainScreen → song → YELLOW gameplay background.
   - **Hit-box edges**: tap each button's corners → should register. If >20 px of the drawn rect is unresponsive, re-measure (Task 6 §5 procedure) and update anchors in `SceneBuilder.cs`, then rebuild scene + APK.
   - **Double-back quit from Start**: tap Android back twice within 2 s from Start → app quits.
   - **Decorative elements**: 4 bottom menu items (연주하기/배우기/챌린지/마이룸) + 2 top-bar icons (설정/보호자) are visibly present but do NOT respond to taps (spec §3 non-goal).
5. Profile APK build:
   ```
   "C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath "$(pwd)" -executeMethod KeyFlow.Editor.ApkBuilder.BuildProfile -logFile - -quit 2>&1 | tail -60
   ```
6. Install profile APK; attach Unity Profiler; run Soyoon profile × Entertainer Normal for 2 minutes. Confirm `GC.Collect == 0` (SP3 baseline preserved). `ScreenManager.Replace` and `BackgroundSwitcher.Apply` are screen-transition-only, not per-frame, so they should contribute 0 B/frame.

Once items 1–6 complete, fill in this §6 with actual results and APK size delta (§9 target ≤ +500 KB vs. SP8 baseline).

## 7. Technical notes worth preserving

**Background import settings allowlist.** The `BackgroundImporterPostprocessor` was previously hardcoded to a single path. Renaming `background_gameplay.png` → `background_blue.png` would have silently regressed ASTC 4x4 enforcement — caught during spec-review loop, avoided via the no-rename + allowlist approach. Pattern: when touching a path-hardcoded postprocessor, audit for hidden couplings before renaming the target asset.

**ITimeSource-like seam for ScreenManager.Instance.** The SP9 spec review claimed that `AddComponent<ScreenManager>()` in an EditMode test would trigger `Awake()` and assign the `Instance` static. That assumption was empirically wrong for cross-component test scenarios (confirmed during Tasks 4+5 implementation when `StartScreenTests` initially failed with `ScreenManager.Instance == null`). Workaround: reflection-assign the `Instance` backing property in test Setup. Non-blocking but worth remembering — if a future SP needs clean `ScreenManager.Instance` access in tests, a `SetInstanceForTest` seam (`#if UNITY_EDITOR || UNITY_INCLUDE_TESTS`) would eliminate the reflection boilerplate.

**Dict indexer-set-during-foreach (SP8 echo).** Not relevant to SP9 directly, but the spy-pattern technique used in `ScreenManagerTests.BackgroundSwitcherSpy` relies on `virtual` dispatch — which is why `BackgroundSwitcher.Apply` was retroactively bumped to virtual in Tasks 4+5. Pattern: if a test needs to observe a MonoBehaviour method call via a subclass, mark the method virtual. Cheap; no production cost.

**Icon assignment automated via editor method + menu.** `SP9IconSetter.Apply` is re-runnable via `KeyFlow/Apply Android Icon (SP9)` menu. Unity's `PlayerSettings.GetIconSizesForTargetGroup(..., IconKind.Any)` returns all Legacy buckets; `SetIconsForTargetGroup` assigns the same texture to all of them. Adaptive icons left unassigned (spec §3 non-goal; Android falls back to Legacy automatically).

## 8. Carry-overs

1. **Device playtest + profiler attach + APK size + Release APK filename update** — §6.
2. **Sub-menu screens** (연주하기/배우기/챌린지/마이룸) — currently decorative. A follow-up SP could wire 연주하기 to MainScreen (tapping 시작하기 also goes there, so duplicate), and build the 3 other sections.
3. **보호자 / 설정 top-bar buttons** on Start screen — currently decorative. 설정 could route to existing `SettingsScreen` overlay; 보호자 needs its own spec.
4. **Per-profile PlayerPrefs partition** — separate score / calibration / settings per child. Would require schema change in `UserPrefs.cs` (profile-prefixed keys).
5. **Remember-last-profile shortcut** — skip Start screen on subsequent launches if preference is set. Non-goal per spec §3.
6. **MainScreen per-profile theme** — currently stays dark-navy regardless. Could become blue (Nayoon) / yellow (Soyoon) to match gameplay.
7. **Adaptive icon with foreground/background layer separation** — Android's modern icon system; spec §3 non-goal.
8. **Start-screen animations, card selected-state feedback, audio sting on tap** — all non-goals; polish for a later SP.
9. **ScreenManager.SetInstanceForTest seam** — remove the reflection boilerplate in `StartScreenTests`.
10. **`TestResults-*.xml` gitignore** — carried over from SP8; Unity batch-mode test runs leave transient XML files. `.gitignore` addition is a 1-line housekeeping commit.

## 9. Stale worktree directories

At merge time, the worktree `.claude/worktrees/w6-sp9-profile-start-screen` will likely become undeletable due to Windows MAX_PATH + Unity `Library/Bee/` paths (same pattern as SP1/SP2/SP3/SP7). Git drops it from the worktree registry and deletes the branch; the physical directory remains until a fresh session `rmdir /s /q`s it.
