# KeyFlow W4 Implementation Plan: Main + Settings + Results + Pause + carry-over bundle

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn W3's single-screen "calibrate → play → completion-overlay" build into a proper 5-screen app: Main (song list) → Gameplay (with Pause) → Results, plus Settings overlay. Retry stays in-scene (no scene reload). All UI Korean. Net EditMode test count rises from 68 → 87.

**Architecture:** Keep the single `GameplayScene.unity`. Introduce a lightweight `ScreenManager` MonoBehaviour that toggles `GameplayRoot` / `MainCanvas` / `ResultsCanvas` + three overlays (`SettingsCanvas` / `PauseCanvas` / `CalibrationCanvas`). Introduce pure-logic singletons (`SongSession` static, `UserPrefs` static facade over PlayerPrefs, `SongCatalog` static parser + cache). New overlays inherit a shared `OverlayBase`. Pause is implemented by advancing `AudioSyncManager.songStartDsp` by the paused duration on Resume. Results replaces W3's `CompletionPanel` with a real animated screen. Retry calls a new `GameplayController.ResetAndStart()` that clears notes/score and restarts the silent song.

**Tech Stack:** Unity 6.3 LTS (6000.3.13f1), C#, .NET Standard 2.1, Unity Test Framework (NUnit EditMode), Unity UI (legacy `Text`/`Image`/`Button`/`Slider`/`ScrollRect`), Input System 1.14 (`Keyboard.current.escapeKey` for Android Back), Newtonsoft.Json (already present), `PlayerPrefs` (key namespace `KeyFlow.*`).

**Reference spec:** [2026-04-21-keyflow-w4-screens-design.md](../specs/2026-04-21-keyflow-w4-screens-design.md) — §2 architecture, §3 new files, §4 file-by-file modifications, §5 Pause, §6 UI specs, §8 carry-over, §9 tests.

**Parent spec:** [2026-04-20-keyflow-mvp-v2-4lane-design.md](../specs/2026-04-20-keyflow-mvp-v2-4lane-design.md) — §4 screens, §12 schedule, M-07 / M-09 / M-10.

**Working directory:** `C:\dev\unity-music` (Windows, Git Bash). **Unity batch mode must run foreground** — never background / pipe (historic silent-exit bug documented in memory).

**Starting state:** branch `main`, HEAD at `e1ef320` (docs(w4): address spec reviewer findings) or later. 68 EditMode tests passing from W3. APK 33 MB. Für Elise Easy playable on Galaxy S22 via single-scene calibration → gameplay → CompletionPanel.

---

## Scope & Out-of-Scope

### In scope for W4
- Namespace consolidation (carry-over #6): `KeyFlow` / `KeyFlow.UI` / `KeyFlow.Calibration` / `KeyFlow.Charts` / `KeyFlow.Editor`
- `UserPrefs` PlayerPrefs facade + prefix + 1-shot legacy migration (`CalibOffsetMs` → `KeyFlow.Settings.CalibrationOffsetMs`)
- `UIStrings` static Korean strings (carry-over #5)
- `OverlayBase` abstract (carry-over #7)
- `SongSession` static holder
- `CalibrationController` migrated to `OverlayBase`, `Begin(Action onDone)` for re-entry from Settings
- `ScreenManager` MonoBehaviour + full-screen `Replace` + independent overlay toggle + Android Back dispatch
- `GameplayRoot` regrouping in SceneBuilder so ScreenManager can SetActive wholesale
- `SongCatalog` JSON parser over `catalog.kfmanifest` + StreamingAssets loading (Editor/Android path split)
- `catalog.kfmanifest` with 1 real song + 4 placeholder entries, procedurally generated star and locked-thumb sprites
- `SongCardView` + `MainScreen` + scroll list
- `AudioSyncManager.Pause()` / `Resume()` + `IsPaused` (dspTime anchor math, EditMode-testable)
- Pause guards in `NoteSpawner` / `NoteController` / `TapInputHandler` / `HoldTracker` / `JudgmentSystem`
- HUD ⏸ button + `PauseScreen` overlay
- `SettingsScreen` overlay (SFX slider, NoteSpeed slider, Recalibrate button, version label)
- `ResultsScreen` overlay (star pop anim, score count-up, breakdown, Retry/Home, best-record label) — replaces `CompletionPanel`
- `GameplayController.ResetAndStart()` for in-scene Retry (no `SceneManager.LoadScene`)
- Android Back mapping per spec §5.5, Main double-back-to-exit
- 19 new EditMode tests (total 87): SongCatalog, UserPrefs, ScreenManager, AudioSyncPause, OverlayBase
- Galaxy S22 device validation per spec §10
- W4 completion report

### Out of scope (deferred)
- Splash screen + Korean ToS (v2 §4.1) → W6
- Particles, haptics, "NEW RECORD" banner animation, background gradient → W6
- Unity Localization package / multi-language → post-MVP
- Toast UI for Main "press again to exit" → W6 (MVP uses silent 2-second window)
- Additional 4 songs → W5
- Python MIDI → .kfchart pipeline → W5
- PlayMode integration tests
- NoteSpeed runtime-change mid-song → not MVP (next-song only)

---

## File Structure (end state)

```
Assets/
├─ Scripts/
│   ├─ Common/
│   │   ├─ GameTime.cs                 (unchanged)
│   │   ├─ LaneLayout.cs               (ns: KeyFlow)
│   │   ├─ SongSession.cs              (C, ns: KeyFlow)
│   │   └─ UserPrefs.cs                (C, ns: KeyFlow)
│   ├─ Charts/
│   │   ├─ ChartTypes.cs               (ns: KeyFlow.Charts)
│   │   └─ ChartLoader.cs              (ns: KeyFlow.Charts)
│   ├─ Catalog/
│   │   ├─ SongEntry.cs                (C, ns: KeyFlow)
│   │   └─ SongCatalog.cs              (C, ns: KeyFlow)
│   ├─ Calibration/
│   │   ├─ CalibrationCalculator.cs    (ns: KeyFlow.Calibration)
│   │   └─ CalibrationController.cs    (M, inherits OverlayBase, Begin(Action), ns: KeyFlow.Calibration)
│   ├─ Gameplay/
│   │   ├─ AudioSyncManager.cs         (M, +Pause/Resume/IsPaused; +ITimeSource test hook)
│   │   ├─ AudioSamplePool.cs          (unchanged)
│   │   ├─ Judgment.cs                 (unchanged, ns: KeyFlow)
│   │   ├─ JudgmentEvaluator.cs        (unchanged, ns: KeyFlow)
│   │   ├─ JudgmentSystem.cs           (M, paused guard)
│   │   ├─ ScoreManager.cs             (unchanged)
│   │   ├─ NoteController.cs           (M, paused guard)
│   │   ├─ NoteSpawner.cs              (M, paused guard + ResetForRetry)
│   │   ├─ TapInputHandler.cs          (M, paused guard)
│   │   ├─ HoldStateMachine.cs         (unchanged)
│   │   ├─ HoldTracker.cs              (M, paused guard + ResetForRetry)
│   │   └─ GameplayController.cs      (M, reads SongSession; ResetAndStart; completion writes records)
│   └─ UI/
│       ├─ LatencyMeter.cs             (ns: KeyFlow.UI)
│       ├─ UIStrings.cs                (C, ns: KeyFlow.UI)
│       ├─ OverlayBase.cs              (C, abstract, ns: KeyFlow.UI)
│       ├─ ScreenManager.cs            (C, ns: KeyFlow.UI)
│       ├─ SongCardView.cs             (C, ns: KeyFlow.UI)
│       ├─ MainScreen.cs               (C, ns: KeyFlow.UI)
│       ├─ SettingsScreen.cs           (C : OverlayBase, ns: KeyFlow.UI)
│       ├─ PauseScreen.cs              (C : OverlayBase, ns: KeyFlow.UI)
│       └─ ResultsScreen.cs            (C : OverlayBase, ns: KeyFlow.UI, replaces CompletionPanel)
├─ Editor/
│   ├─ ApkBuilder.cs                   (unchanged, ns: KeyFlow.Editor)
│   └─ SceneBuilder.cs                 (M, + new canvases, GameplayRoot, star/thumb sprites, ScreenManager wiring)
├─ StreamingAssets/
│   ├─ charts/
│   │   └─ beethoven_fur_elise.kfchart (unchanged)
│   ├─ thumbs/
│   │   ├─ fur_elise.png               (C, procedurally generated at build)
│   │   └─ locked.png                  (C, procedurally generated at build)
│   └─ catalog.kfmanifest              (C)
├─ Sprites/
│   ├─ white.png                       (unchanged)
│   ├─ star_filled.png                 (C, procedurally generated)
│   └─ star_empty.png                  (C, procedurally generated)
└─ Tests/EditMode/
    ├─ (existing W3 suites: 68 tests, unchanged)
    ├─ SongCatalogTests.cs              (C, 4 tests)
    ├─ UserPrefsTests.cs                (C, 6 tests)
    ├─ ScreenManagerTests.cs            (C, 4 tests)
    ├─ AudioSyncPauseTests.cs           (C, 3 tests)
    └─ OverlayBaseTests.cs              (C, 2 tests)
```

Legend: `C` = create, `M` = modify, unchanged = left alone.

**`CompletionPanel.cs` is deleted** (replaced by `ResultsScreen`).

---

## Task Map

| Task | Theme | Tests delta | Commit type |
|---|---|---|---|
| 1 | Namespace refactor | 0 | chore(ns) |
| 2 | UserPrefs + tests + migration | +6 | feat(prefs) |
| 3 | UIStrings | 0 | feat(ui) |
| 4 | OverlayBase + tests | +2 | feat(ui) |
| 5 | SongSession static | 0 | feat(session) |
| 6 | CalibrationController → OverlayBase + Begin(Action) | 0 | refactor(calib) |
| 7 | ScreenManager + tests | +4 | feat(screen) |
| 8 | GameplayRoot regrouping in SceneBuilder | 0 | refactor(scene) |
| 9 | SongCatalog parser + tests | +4 | feat(catalog) |
| 10 | catalog.kfmanifest + thumb/star sprites | 0 | feat(assets) |
| 11 | SongCardView + MainScreen | 0 | feat(ui) |
| 12 | SceneBuilder MainCanvas + SongSession wiring in GameplayController | 0 | feat(scene) |
| 13 | AudioSyncManager Pause/Resume + tests | +3 | feat(audio) |
| 14 | Pause guards across spawner/note/tap/hold/judgment | 0 | feat(pause) |
| 15 | PauseScreen + HUD ⏸ button + SceneBuilder | 0 | feat(ui) |
| 16 | SettingsScreen + SceneBuilder + calib re-run | 0 | feat(ui) |
| 17 | ResultsScreen + SceneBuilder (delete CompletionPanel) | 0 | feat(ui) |
| 18 | GameplayController.ResetAndStart + Retry wiring | 0 | feat(gameplay) |
| 19 | Back button mapping + Main double-back-to-quit | 0 | feat(input) |
| 20 | Device validation + completion report | 0 | docs(w4) |

**Total 20 tasks, +19 tests, W3's 68 → 87 at end.**

---

## General Coding Conventions

- C# 10, nullable disabled (matches W3).
- Every new `MonoBehaviour` field either `[SerializeField] private` (inspector-wired) or plain `private`. No `public` fields.
- `using UnityEngine;` first. Usings sorted. Remove unused.
- File namespace matches target in **File Structure** above.
- Every new `public` method gets an XML summary if behavior is non-obvious (not a rule — judgment).
- New tests live in `KeyFlow.Tests.EditMode` namespace.
- PlayerPrefs: never touch `PlayerPrefs.*` directly outside `UserPrefs` after Task 2.
- UI text: never hardcode a user-visible Korean string after Task 3 — reference `UIStrings.*`.
- Procedurally generated sprites: generate in `SceneBuilder.Ensure*Sprite()` methods, PNG bytes via `EncodeToPNG`, write to `Assets/Sprites` or `Assets/StreamingAssets/thumbs`, reimport with sprite settings.

## How to run tests

EditMode tests run via Unity CLI (foreground):

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -nographics -quit \
  -projectPath "C:/dev/unity-music" \
  -runTests -testPlatform EditMode \
  -testResults "C:/dev/unity-music/test-results.xml" \
  -logFile -
```

Unity may take 30–90 s cold. Watch `test-results.xml` for pass/fail. **Do NOT run in background or pipe to another process** — batch mode silently exits if its stdout is captured.

## How to build APK

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -nographics -quit \
  -projectPath "C:/dev/unity-music" \
  -executeMethod KeyFlow.Editor.ApkBuilder.Build \
  -logFile -
```

APK drops at `Builds/KeyFlow.apk`. Install via `adb install -r Builds/KeyFlow.apk`.

---

## Task 1: Namespace consolidation (carry-over #6)

**Files:** Modify every `.cs` under `Assets/Scripts/` and `Assets/Editor/`, plus all `Assets/Tests/EditMode/*.cs`. No new files.

**Intent:** Move files to the namespace assignments listed in §4.1 of the spec. Unity `SerializedObject` references are GUID-based and survive namespace changes, so scene/prefab wiring is preserved — but this MUST be verified by rebuilding the scene and running tests.

**Target namespaces:**
- `KeyFlow` — `GameTime`, `LaneLayout`, `Judgment`, `JudgmentEvaluator`, `ScoreManager`, `AudioSyncManager`, `AudioSamplePool`, `NoteController`, `NoteSpawner`, `TapInputHandler`, `HoldStateMachine`, `HoldTracker`, `JudgmentSystem`, `GameplayController`
- `KeyFlow.UI` — `LatencyMeter`, `CompletionPanel` (will be deleted in Task 17 anyway, but move now for consistency)
- `KeyFlow.Calibration` — `CalibrationController`, `CalibrationCalculator`
- `KeyFlow.Charts` — `ChartTypes`, `ChartLoader`, `ChartData`, `ChartNote`, `ChartDifficulty`, `NoteType`
- `KeyFlow.Editor` — `SceneBuilder`, `ApkBuilder`
- `KeyFlow.Tests.EditMode` — all existing test files (they're already in this ns; verify)

Steps:

- [ ] **Step 1:** Read current namespace of every file:
```bash
grep -rn "^namespace" Assets/Scripts Assets/Editor Assets/Tests
```
Capture which files move.

- [ ] **Step 2:** For each file needing a namespace change, edit the `namespace` declaration. Update `using KeyFlow;` / `using KeyFlow.UI;` / etc. in consumers.

Known current state:
- `Assets/Scripts/Charts/ChartLoader.cs` + `ChartTypes.cs` currently `KeyFlow` → move to `KeyFlow.Charts`. Add `using KeyFlow.Charts;` in `NoteSpawner.cs`, `GameplayController.cs`, `ChartLoaderTests.cs`.
- `Assets/Scripts/Calibration/*.cs` currently `KeyFlow` → move to `KeyFlow.Calibration`. Add `using KeyFlow.Calibration;` in `GameplayController.cs`, `CalibrationCalculatorTests.cs`, `SceneBuilder.cs`.
- `Assets/Editor/SceneBuilder.cs` currently `KeyFlow.Editor` with `using KeyFlow.UI` — verify; `ApkBuilder.cs` namespace check.
- `Assets/Scripts/UI/LatencyMeter.cs` + `CompletionPanel.cs` — confirm `KeyFlow.UI`.

- [ ] **Step 3:** Regenerate the scene to catch wiring breakage early. Run in Unity menu (via `-executeMethod`):
```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -nographics -quit \
  -projectPath "C:/dev/unity-music" \
  -executeMethod KeyFlow.Editor.SceneBuilder.Build \
  -logFile -
```

Expected: scene rebuilds without `[KeyFlow] Field '...' not found` errors. If errors appear, the namespace move broke a `SetField` call — inspect, fix usings.

- [ ] **Step 4:** Run EditMode tests. Expected: 68 / 68 pass.

- [ ] **Step 5:** Commit.
```bash
git add -A Assets/
git commit -m "chore(ns): consolidate namespaces into KeyFlow/.UI/.Calibration/.Charts/.Editor

Carry-over #6 from W3 review. File move is namespace-only; class
names and signatures unchanged. Scene GUID references survive.
SceneBuilder regeneration verified. 68/68 tests green."
```

---

## Task 2: UserPrefs facade + migration + tests

**Files:**
- Create: `Assets/Scripts/Common/UserPrefs.cs`
- Create: `Assets/Tests/EditMode/UserPrefsTests.cs`
- Modify (later in this task): `Assets/Scripts/Calibration/CalibrationController.cs`

**Intent:** Single choke-point for PlayerPrefs. 1-shot migration of legacy key. Typed accessors for settings + per-song records.

- [ ] **Step 1: Write failing tests** → `Assets/Tests/EditMode/UserPrefsTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace KeyFlow.Tests.EditMode
{
    public class UserPrefsTests
    {
        [SetUp] public void Setup() { PlayerPrefs.DeleteAll(); }
        [TearDown] public void Teardown() { PlayerPrefs.DeleteAll(); }

        [Test] public void Defaults_WhenNeverSet_ReturnSpecValues()
        {
            Assert.AreEqual(0.8f, UserPrefs.SfxVolume, 1e-4);
            Assert.AreEqual(2.0f, UserPrefs.NoteSpeed, 1e-4);
            Assert.AreEqual(0, UserPrefs.CalibrationOffsetMs);
        }

        [Test] public void SfxVolume_And_NoteSpeed_RoundTrip()
        {
            UserPrefs.SfxVolume = 0.3f;
            UserPrefs.NoteSpeed = 2.5f;
            Assert.AreEqual(0.3f, UserPrefs.SfxVolume, 1e-4);
            Assert.AreEqual(2.5f, UserPrefs.NoteSpeed, 1e-4);
        }

        [Test] public void BestRecord_RoundTrip_PerSongPerDifficulty()
        {
            UserPrefs.TrySetBest("song_a", Difficulty.Easy, 2, 600_000);
            UserPrefs.TrySetBest("song_a", Difficulty.Normal, 1, 400_000);
            UserPrefs.TrySetBest("song_b", Difficulty.Easy, 3, 900_000);

            Assert.AreEqual(2, UserPrefs.GetBestStars("song_a", Difficulty.Easy));
            Assert.AreEqual(600_000, UserPrefs.GetBestScore("song_a", Difficulty.Easy));
            Assert.AreEqual(1, UserPrefs.GetBestStars("song_a", Difficulty.Normal));
            Assert.AreEqual(3, UserPrefs.GetBestStars("song_b", Difficulty.Easy));
        }

        [Test] public void TrySetBest_OnlyUpdatesWhenScoreHigher()
        {
            bool first = UserPrefs.TrySetBest("s", Difficulty.Easy, 1, 500_000);
            bool second = UserPrefs.TrySetBest("s", Difficulty.Easy, 2, 400_000);  // lower score
            bool third = UserPrefs.TrySetBest("s", Difficulty.Easy, 3, 900_000);

            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.IsTrue(third);
            Assert.AreEqual(3, UserPrefs.GetBestStars("s", Difficulty.Easy));
            Assert.AreEqual(900_000, UserPrefs.GetBestScore("s", Difficulty.Easy));
        }

        [Test] public void MigrateLegacy_CopiesOldCalibOffsetMsKey()
        {
            PlayerPrefs.SetInt("CalibOffsetMs", 123);
            PlayerPrefs.Save();

            UserPrefs.MigrateLegacy();

            Assert.AreEqual(123, UserPrefs.CalibrationOffsetMs);
            Assert.IsFalse(PlayerPrefs.HasKey("CalibOffsetMs"), "legacy key should be removed");
            Assert.IsTrue(PlayerPrefs.HasKey("KeyFlow.Migration.V1.Done"));
        }

        [Test] public void MigrateLegacy_IsIdempotent()
        {
            PlayerPrefs.SetInt("CalibOffsetMs", 50);
            UserPrefs.MigrateLegacy();

            // Write a different value to legacy — second migration must NOT overwrite prefixed.
            PlayerPrefs.SetInt("CalibOffsetMs", 999);
            UserPrefs.MigrateLegacy();

            Assert.AreEqual(50, UserPrefs.CalibrationOffsetMs);
        }
    }
}
```

- [ ] **Step 2: Run tests, expect FAIL** (class not defined).

- [ ] **Step 3: Implement UserPrefs**:

```csharp
using UnityEngine;

namespace KeyFlow
{
    public static class UserPrefs
    {
        private const string K_SfxVolume   = "KeyFlow.Settings.SfxVolume";
        private const string K_NoteSpeed   = "KeyFlow.Settings.NoteSpeed";
        private const string K_CalibOffset = "KeyFlow.Settings.CalibrationOffsetMs";
        private const string K_MigrationV1 = "KeyFlow.Migration.V1.Done";
        private const string Legacy_CalibOffset = "CalibOffsetMs";

        private const float DefaultSfxVolume = 0.8f;
        private const float DefaultNoteSpeed = 2.0f;

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(K_SfxVolume, DefaultSfxVolume);
            set { PlayerPrefs.SetFloat(K_SfxVolume, value); PlayerPrefs.Save(); }
        }

        public static float NoteSpeed
        {
            get => PlayerPrefs.GetFloat(K_NoteSpeed, DefaultNoteSpeed);
            set { PlayerPrefs.SetFloat(K_NoteSpeed, value); PlayerPrefs.Save(); }
        }

        public static int CalibrationOffsetMs
        {
            get => PlayerPrefs.GetInt(K_CalibOffset, 0);
            set { PlayerPrefs.SetInt(K_CalibOffset, value); PlayerPrefs.Save(); }
        }

        public static int GetBestStars(string songId, Difficulty d) =>
            PlayerPrefs.GetInt(RecordStarsKey(songId, d), 0);

        public static int GetBestScore(string songId, Difficulty d) =>
            PlayerPrefs.GetInt(RecordScoreKey(songId, d), 0);

        /// <summary>Returns true when the new score beat the previous best and was stored.</summary>
        public static bool TrySetBest(string songId, Difficulty d, int stars, int score)
        {
            int prevScore = GetBestScore(songId, d);
            if (score <= prevScore) return false;
            PlayerPrefs.SetInt(RecordStarsKey(songId, d), stars);
            PlayerPrefs.SetInt(RecordScoreKey(songId, d), score);
            PlayerPrefs.Save();
            return true;
        }

        public static void MigrateLegacy()
        {
            if (PlayerPrefs.GetInt(K_MigrationV1, 0) == 1) return;
            if (PlayerPrefs.HasKey(Legacy_CalibOffset))
            {
                int legacy = PlayerPrefs.GetInt(Legacy_CalibOffset, 0);
                PlayerPrefs.SetInt(K_CalibOffset, legacy);
                PlayerPrefs.DeleteKey(Legacy_CalibOffset);
            }
            PlayerPrefs.SetInt(K_MigrationV1, 1);
            PlayerPrefs.Save();
        }

        private static string RecordStarsKey(string id, Difficulty d) =>
            $"KeyFlow.Record.{id}.{d}.Stars";
        private static string RecordScoreKey(string id, Difficulty d) =>
            $"KeyFlow.Record.{id}.{d}.Score";
    }
}
```

- [ ] **Step 4: Run tests, expect 6/6 PASS** (74/74 total).

- [ ] **Step 5: Commit**:
```bash
git add Assets/Scripts/Common/UserPrefs.cs Assets/Tests/EditMode/UserPrefsTests.cs
git commit -m "feat(prefs): UserPrefs facade + legacy CalibOffsetMs migration

Single choke-point for PlayerPrefs with KeyFlow.* key prefix.
1-shot idempotent migration copies CalibOffsetMs → prefixed key.
Per-song best stars+score with TrySetBest (updates only if higher).
+6 EditMode tests (74 total)."
```

Note: `CalibrationController` itself still uses `"CalibOffsetMs"` directly. That replacement is deferred to Task 6 where we also give it `OverlayBase` inheritance.

---

## Task 3: UIStrings

**Files:** Create `Assets/Scripts/UI/UIStrings.cs`.

- [ ] **Step 1:** Write the full strings class exactly as specified in spec §3.4. Namespace `KeyFlow.UI`. No tests (pure constants).

- [ ] **Step 2:** Replace the one existing Korean hardcoded string in `CalibrationController.cs`:
  - Line 44: `"화면 아무 곳이나, 클릭 소리에 맞춰 8번 탭하세요."` → `UIStrings.CalibrationPrompt`
  - Line 127 (`$"결과가 흔들려요 (MAD {result.madMs}ms)...`): leave as-is for now (interpolated, retry-specific, low priority to centralize). Add a TODO comment referencing W6 polish.
  - `SceneBuilder.cs` line ~247 sets the same prompt text directly — switch to `UIStrings.CalibrationPrompt`. Add `using KeyFlow.UI;` at top of SceneBuilder if not already.

- [ ] **Step 3:** Build scene + run tests: `SceneBuilder.Build` foreground, then EditMode tests. Expected: 74/74 pass, no runtime string change when running the scene.

- [ ] **Step 4: Commit**:
```bash
git add Assets/Scripts/UI/UIStrings.cs \
        Assets/Scripts/Calibration/CalibrationController.cs \
        Assets/Editor/SceneBuilder.cs
git commit -m "feat(ui): UIStrings Korean hub + wire calibration prompt

Carry-over #5. All user-visible Korean strings will flow through
UIStrings.*. Replaced single current usage in CalibrationController
and SceneBuilder. Retry-advice string kept inline (TODO W6)."
```

---

## Task 4: OverlayBase abstract + tests

**Files:**
- Create: `Assets/Scripts/UI/OverlayBase.cs`
- Create: `Assets/Tests/EditMode/OverlayBaseTests.cs`

**Intent:** Extract the `Awake → SetActive(false)` + `Show()` + `Finish()` pattern duplicated by W3's `CalibrationController` and `CompletionPanel`. New overlays (`SettingsScreen`, `PauseScreen`, `ResultsScreen`) inherit.

- [ ] **Step 1: Write failing tests:**

```csharp
using NUnit.Framework;
using UnityEngine;
using KeyFlow.UI;

namespace KeyFlow.Tests.EditMode
{
    public class OverlayBaseTests
    {
        private class SpyOverlay : OverlayBase
        {
            public int ShownCalls;
            public int FinishingCalls;
            protected override void OnShown() => ShownCalls++;
            protected override void OnFinishing() => FinishingCalls++;
        }

        [Test] public void Awake_DisablesGameObject()
        {
            var go = new GameObject("spy");
            var overlay = go.AddComponent<SpyOverlay>();
            Assert.IsFalse(go.activeSelf);
            Assert.IsFalse(overlay.IsVisible);
            Object.DestroyImmediate(go);
        }

        [Test] public void ShowAndFinish_InvokeHooksInOrder()
        {
            var go = new GameObject("spy");
            var overlay = go.AddComponent<SpyOverlay>();
            overlay.Show();
            Assert.IsTrue(go.activeSelf);
            Assert.IsTrue(overlay.IsVisible);
            Assert.AreEqual(1, overlay.ShownCalls);
            Assert.AreEqual(0, overlay.FinishingCalls);

            overlay.Finish();
            Assert.IsFalse(go.activeSelf);
            Assert.IsFalse(overlay.IsVisible);
            Assert.AreEqual(1, overlay.FinishingCalls);
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2:** Run tests, expect FAIL.

- [ ] **Step 3: Implement OverlayBase** — exactly as specified in spec §3.1.

- [ ] **Step 4:** Run tests, expect 2/2 pass (76 total).

- [ ] **Step 5: Commit**:
```bash
git add Assets/Scripts/UI/OverlayBase.cs Assets/Tests/EditMode/OverlayBaseTests.cs
git commit -m "feat(ui): OverlayBase abstract with Show/Finish hooks

Carry-over #7. Settings/Pause/Results will inherit. CalibrationController
migration follows in Task 6, CompletionPanel replaced in Task 17.
+2 EditMode tests (76 total)."
```

---

## Task 5: SongSession static

**Files:** Create `Assets/Scripts/Common/SongSession.cs`.

```csharp
namespace KeyFlow
{
    public static class SongSession
    {
        public static string CurrentSongId;
        public static Difficulty CurrentDifficulty;
        public static ScoreManager LastScore;

        public static void Reset()
        {
            CurrentSongId = null;
            CurrentDifficulty = Difficulty.Easy;
            LastScore = null;
        }
    }
}
```

No tests — pure state holder. The `Reset` method exists so EditMode tests that need a clean slate can call it.

- [ ] **Step 1:** Create file.
- [ ] **Step 2:** Build verification: run `SceneBuilder.Build` + tests. 76/76.
- [ ] **Step 3: Commit:**
```bash
git add Assets/Scripts/Common/SongSession.cs
git commit -m "feat(session): SongSession static holder

Transient state passed Main → Gameplay → Results (songId, difficulty,
last score). Static since single-scene means no DontDestroyOnLoad
need. Reset() for test isolation."
```

---

## Task 6: CalibrationController → OverlayBase + Begin(Action onDone)

**Files:** Modify `Assets/Scripts/Calibration/CalibrationController.cs`.

**Intent:**
1. Inherit `OverlayBase` — eliminate manual `Awake→SetActive(false)` and `Finish` body.
2. Change `Begin()` signature to `Begin(Action onDone)` so Settings can pass its own callback for re-entry. Remove `public Action OnCalibrationDone` field.
3. Route persistence through `UserPrefs.CalibrationOffsetMs` — delete local `PrefsKey` const and `HasSavedOffset` / `LoadSavedOffsetMs` statics (migrated callers below).

Migrating callers:
- `GameplayController.Start` currently reads `CalibrationController.HasSavedOffset()` and `LoadSavedOffsetMs()`. Replace with `UserPrefs.CalibrationOffsetMs != 0` (treating 0 as "never set" — acceptable because CalibrationCalculator returns ~100ms; literal 0 only happens after 3 retries fail and we save 0, which is fine — user still gets zero-offset gameplay). Actually: `0` is ambiguous. **Use `PlayerPrefs.HasKey("KeyFlow.Settings.CalibrationOffsetMs")` via a new `UserPrefs.HasCalibration` helper.**

Add to `UserPrefs`:
```csharp
public static bool HasCalibration =>
    PlayerPrefs.HasKey("KeyFlow.Settings.CalibrationOffsetMs");
```

- [ ] **Step 1:** Extend `UserPrefs` with `HasCalibration` property + add a test:
```csharp
[Test] public void HasCalibration_TracksKeyPresence()
{
    Assert.IsFalse(UserPrefs.HasCalibration);
    UserPrefs.CalibrationOffsetMs = 0;  // explicit set even if zero
    Assert.IsTrue(UserPrefs.HasCalibration);
}
```

- [ ] **Step 2:** Refactor `CalibrationController`:
  - Change inheritance: `public class CalibrationController : OverlayBase`
  - Remove manual `Awake` (base handles it); override `OnShown` if needed (nothing needed — Begin does the work).
  - `Begin(Action onDone)`:
    ```csharp
    public void Begin(Action onDone)
    {
        this.onDoneCallback = onDone;
        Show();
        retryCount = 0;
        ShowIdle(UIStrings.CalibrationPrompt);
        startButton.onClick.RemoveAllListeners();
        startButton.onClick.AddListener(StartOneRun);
    }
    ```
  - Replace `Finish()` body to: `Finish() { base.Finish(); onDoneCallback?.Invoke(); onDoneCallback = null; }`. Override semantics — need `new` keyword or a rename: actually `OverlayBase.Finish` is `virtual`, so `override` it. Then:
    ```csharp
    public override void Finish()
    {
        base.Finish();
        var cb = onDoneCallback; onDoneCallback = null;
        cb?.Invoke();
    }
    ```
  - Delete `public System.Action OnCalibrationDone;` field.
  - Delete `PrefsKey` const, `HasSavedOffset()`, `LoadSavedOffsetMs()`.
  - `Save(int offsetMs)`: `UserPrefs.CalibrationOffsetMs = offsetMs;` + keep `audioSync.CalibrationOffsetSec = offsetMs / 1000.0;`.

- [ ] **Step 3:** Update `GameplayController.Start`:
```csharp
private void Start()
{
    UserPrefs.MigrateLegacy();  // first call site
    chart = ChartLoader.LoadFromStreamingAssets(SongSession.CurrentSongId ?? "beethoven_fur_elise");
    if (UserPrefs.HasCalibration)
    {
        audioSync.CalibrationOffsetSec = UserPrefs.CalibrationOffsetMs / 1000.0;
        BeginGameplay();
    }
    else
    {
        calibration.Begin(BeginGameplay);
    }
}
```

Note: `SongSession.CurrentSongId ?? "beethoven_fur_elise"` default is defensive — Task 12 wires Main to set it properly. Remove the `?? "..."` fallback in Task 12.

Also remove `calibration.OnCalibrationDone = null;` and `calibration.OnCalibrationDone = BeginGameplay;` lines.

- [ ] **Step 4:** Build scene + run tests. Expected: 77/77 pass. Open scene in Editor; verify calibration overlay still hides on start and shows on first run.

- [ ] **Step 5: Commit**:
```bash
git add Assets/Scripts/Calibration/CalibrationController.cs \
        Assets/Scripts/Gameplay/GameplayController.cs \
        Assets/Scripts/Common/UserPrefs.cs \
        Assets/Tests/EditMode/UserPrefsTests.cs
git commit -m "refactor(calib): CalibrationController inherits OverlayBase + Begin(Action)

Eliminates duplicate Awake-disable/Finish pattern. Begin takes an
onDone callback directly so Settings re-run can re-enter without
sharing state with Gameplay's initial calibration. Persistence moved
to UserPrefs.CalibrationOffsetMs; local PrefsKey/HasSavedOffset/
LoadSavedOffsetMs deleted. +1 UserPrefs test (77 total)."
```

---

## Task 7: ScreenManager + tests

**Files:**
- Create: `Assets/Scripts/UI/ScreenManager.cs`
- Create: `Assets/Tests/EditMode/ScreenManagerTests.cs`

**Intent:** Central dispatcher. Full-screen target (`Main` / `Gameplay` / `Results`) via `Replace`; overlays (`Settings` / `Pause` / `Calibration`) via `ShowOverlay` / `HideOverlay`. `HandleBack()` dispatches based on current state. Android Back is surfaced via `Keyboard.current.escapeKey` (Input System maps Android Back to ESC).

Interface:

```csharp
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KeyFlow.UI
{
    public enum Screen { Main, Gameplay, Results }

    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance { get; private set; }

        [SerializeField] private GameObject mainRoot;
        [SerializeField] private GameObject gameplayRoot;
        [SerializeField] private GameObject resultsCanvas;

        [SerializeField] private OverlayBase settingsOverlay;
        [SerializeField] private OverlayBase pauseOverlay;
        [SerializeField] private OverlayBase calibrationOverlay;

        public Screen Current { get; private set; }

        public event Action<Screen> OnReplaced;

        // Double-back-to-quit state
        private float lastBackOnMain = -10f;
        private const float DoubleBackWindow = 2.0f;

        // Public hooks for event-driven testing without going through Update
        public void Replace(Screen target)
        {
            HideAllOverlays();
            mainRoot.SetActive(target == Screen.Main);
            gameplayRoot.SetActive(target == Screen.Gameplay);
            resultsCanvas.SetActive(target == Screen.Results);
            Current = target;
            OnReplaced?.Invoke(target);
        }

        public void ShowOverlay(OverlayBase o) => o.Show();
        public void HideOverlay(OverlayBase o) => o.Finish();

        public bool AnyOverlayVisible =>
            settingsOverlay.IsVisible || pauseOverlay.IsVisible || calibrationOverlay.IsVisible;

        private void HideAllOverlays()
        {
            if (settingsOverlay.IsVisible) settingsOverlay.Finish();
            if (pauseOverlay.IsVisible) pauseOverlay.Finish();
            if (calibrationOverlay.IsVisible) calibrationOverlay.Finish();
        }

        // External-invocable for tests (no Input System dependency)
        public void HandleBack()
        {
            // Overlays first: Settings closes, Pause = Resume (delegated to listener),
            //                Calibration ignored (MVP)
            if (settingsOverlay.IsVisible) { settingsOverlay.Finish(); return; }
            if (calibrationOverlay.IsVisible) return;
            if (pauseOverlay.IsVisible) { pauseOverlay.Finish(); return; }

            switch (Current)
            {
                case Screen.Gameplay:
                    pauseOverlay.Show();   // PauseScreen.OnShown triggers audioSync.Pause()
                    break;
                case Screen.Results:
                    Replace(Screen.Main);
                    break;
                case Screen.Main:
                    if (Time.unscaledTime - lastBackOnMain < DoubleBackWindow)
                        Application.Quit();
                    else
                        lastBackOnMain = Time.unscaledTime;
                    break;
            }
        }

        private void Awake() { Instance = this; }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            HandleBack();
        }
    }
}
```

Tests (skip the `Update()` path — test `HandleBack` directly):

```csharp
using NUnit.Framework;
using UnityEngine;
using KeyFlow.UI;

namespace KeyFlow.Tests.EditMode
{
    public class ScreenManagerTests
    {
        private GameObject mgr, mainRoot, gameplayRoot, results;
        private GameObject settingsGO, pauseGO, calibGO;
        private ScreenManager sm;
        private TestOverlay settings, pause, calib;

        private class TestOverlay : OverlayBase { }

        [SetUp] public void Setup()
        {
            mgr = new GameObject("sm");
            mainRoot = new GameObject("main");
            gameplayRoot = new GameObject("gameplay");
            results = new GameObject("results");
            settingsGO = new GameObject("settings"); settings = settingsGO.AddComponent<TestOverlay>();
            pauseGO = new GameObject("pause"); pause = pauseGO.AddComponent<TestOverlay>();
            calibGO = new GameObject("calib"); calib = calibGO.AddComponent<TestOverlay>();

            sm = mgr.AddComponent<ScreenManager>();
            // Wire via reflection since fields are [SerializeField] private
            SetPrivate(sm, "mainRoot", mainRoot);
            SetPrivate(sm, "gameplayRoot", gameplayRoot);
            SetPrivate(sm, "resultsCanvas", results);
            SetPrivate(sm, "settingsOverlay", settings);
            SetPrivate(sm, "pauseOverlay", pause);
            SetPrivate(sm, "calibrationOverlay", calib);
        }

        [TearDown] public void Teardown()
        {
            foreach (var go in new[] { mgr, mainRoot, gameplayRoot, results, settingsGO, pauseGO, calibGO })
                Object.DestroyImmediate(go);
        }

        [Test] public void Replace_Main_ActivatesOnlyMainRoot()
        {
            sm.Replace(Screen.Main);
            Assert.IsTrue(mainRoot.activeSelf);
            Assert.IsFalse(gameplayRoot.activeSelf);
            Assert.IsFalse(results.activeSelf);
            Assert.AreEqual(Screen.Main, sm.Current);
        }

        [Test] public void ShowAndHideOverlay_TogglesIndependentlyOfScreen()
        {
            sm.Replace(Screen.Main);
            sm.ShowOverlay(settings);
            Assert.IsTrue(settings.IsVisible);
            Assert.IsTrue(mainRoot.activeSelf, "overlay must not deactivate underlying screen");
            sm.HideOverlay(settings);
            Assert.IsFalse(settings.IsVisible);
        }

        [Test] public void Replace_HidesAllVisibleOverlays()
        {
            sm.Replace(Screen.Main);
            sm.ShowOverlay(settings);
            sm.Replace(Screen.Gameplay);
            Assert.IsFalse(settings.IsVisible);
            Assert.IsTrue(gameplayRoot.activeSelf);
        }

        [Test] public void HandleBack_OnGameplayWithNoOverlay_ShowsPauseOverlay()
        {
            sm.Replace(Screen.Gameplay);
            sm.HandleBack();
            Assert.IsTrue(pause.IsVisible);
        }

        private static void SetPrivate(object target, string name, object value)
        {
            var f = target.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f.SetValue(target, value);
        }
    }
}
```

- [ ] **Step 1:** Write tests → expect FAIL.
- [ ] **Step 2:** Implement `ScreenManager`.
- [ ] **Step 3:** Run tests → expect 4/4 pass (81/81 total).
- [ ] **Step 4: Commit:**
```bash
git add Assets/Scripts/UI/ScreenManager.cs Assets/Tests/EditMode/ScreenManagerTests.cs
git commit -m "feat(screen): ScreenManager with Replace + overlay toggle + Back dispatch

Central hub: 3 full-screen slots (Main/Gameplay/Results) and 3 overlay
slots (Settings/Pause/Calibration). HandleBack() dispatches per spec
§5.5 Android Back mapping. Double-back-to-quit on Main. +4 tests
(81 total)."
```

---

## Task 8: GameplayRoot regrouping in SceneBuilder

**Files:** Modify `Assets/Editor/SceneBuilder.cs`.

**Intent:** So that Task 7's `ScreenManager` can toggle gameplay wholesale, all gameplay-specific objects (LaneDividers, JudgmentLine, Managers, HUDCanvas, Note prefab spawns — which don't exist until runtime so not relevant) must share a `GameplayRoot` parent. Currently they're loose children of the scene.

- [ ] **Step 1:** Add a helper:
```csharp
private static GameObject BuildGameplayRoot()
{
    var root = new GameObject("GameplayRoot");
    return root;
}
```

- [ ] **Step 2:** In `Build()`, create `gameplayRoot` after camera, and re-parent `LaneDividers`, `JudgmentLine`, `Managers`, `HUDCanvas` under it.

For `BuildLaneDividers`, `BuildJudgmentLine`: change to accept `Transform parent` argument and call `go.transform.SetParent(parent, false)`.

For `BuildManagers`: the method already creates a `Managers` parent GO — re-parent that under `gameplayRoot`.

For `BuildHUD`: re-parent the `canvasGO` under `gameplayRoot`.

- [ ] **Step 3:** Add `ScreenManager` creation:
```csharp
var screenManagerGO = new GameObject("ScreenManager");
var screenMgr = screenManagerGO.AddComponent<ScreenManager>();
// Wire mainRoot/gameplayRoot/resultsCanvas/overlays at the end after all are built.
SetField(screenMgr, "gameplayRoot", gameplayRoot);
SetField(screenMgr, "calibrationOverlay", calibration);
// main/results/settings/pause come in Tasks 12/15/16/17
```

For now, wire only `gameplayRoot` and `calibrationOverlay`. Add `using KeyFlow.UI;` at top.

Also add a new `SetField(Object, string, GameObject)` overload — GameObject is not UnityEngine.Object in the field-lookup path currently, verify.

Actually `GameObject` IS a `UnityEngine.Object`; the existing `SetField(Object, string, Object)` overload works.

- [ ] **Step 4:** Rename the menu item:
```csharp
[MenuItem("KeyFlow/Build W4 Scene")]
public static void Build()
```

- [ ] **Step 5:** Build scene via `-executeMethod KeyFlow.Editor.SceneBuilder.Build`. Open in editor. Verify hierarchy: `GameplayRoot → [LaneDividers, JudgmentLine, Managers, HUDCanvas]`. Gameplay still plays end-to-end. `ScreenManager` GO exists at scene root.

- [ ] **Step 6:** EditMode tests: 81/81.

- [ ] **Step 7: Commit:**
```bash
git add Assets/Editor/SceneBuilder.cs
git commit -m "refactor(scene): group gameplay objects under GameplayRoot + create ScreenManager GO

Enables ScreenManager to toggle gameplay wholesale in upcoming tasks.
Menu renamed Build W4 Scene. Runtime behavior unchanged."
```

---

## Task 9: SongCatalog parser + tests

**Files:**
- Create: `Assets/Scripts/Catalog/SongEntry.cs`
- Create: `Assets/Scripts/Catalog/SongCatalog.cs`
- Create: `Assets/Tests/EditMode/SongCatalogTests.cs`

**Intent:** JSON → `SongEntry[]`. Pure `ParseJson` for tests; `LoadFromStreamingAssets` runtime wrapper (mirrors `ChartLoader` split).

`SongEntry.cs`:
```csharp
using System;

namespace KeyFlow
{
    [Serializable]
    public class SongEntry
    {
        public string id;
        public string title;
        public string composer;
        public string thumbnail;
        public string[] difficulties;
        public bool chartAvailable;

        public bool HasDifficulty(Difficulty d)
        {
            if (difficulties == null) return false;
            string target = d.ToString();  // "Easy" or "Normal"
            foreach (var s in difficulties) if (s == target) return true;
            return false;
        }
    }

    [Serializable]
    internal class CatalogDto
    {
        public int version;
        public SongEntry[] songs;
    }
}
```

`SongCatalog.cs` (loading mirrors `ChartLoader` — follow its pattern exactly):

```csharp
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace KeyFlow
{
    public static class SongCatalog
    {
        public static IReadOnlyList<SongEntry> All => loaded;
        private static SongEntry[] loaded = System.Array.Empty<SongEntry>();

        public static SongEntry[] ParseJson(string json)
        {
            var dto = JsonConvert.DeserializeObject<CatalogDto>(json);
            if (dto == null) throw new System.FormatException("Null catalog");
            if (dto.songs == null) throw new System.FormatException("Missing 'songs' array");
            foreach (var s in dto.songs)
            {
                if (string.IsNullOrEmpty(s.id)) throw new System.FormatException("Entry missing 'id'");
            }
            return dto.songs;
        }

        public static bool TryGet(string id, out SongEntry entry)
        {
            foreach (var s in loaded) if (s.id == id) { entry = s; return true; }
            entry = null; return false;
        }

        public static IEnumerator LoadAsync()
        {
            const string file = "catalog.kfmanifest";
#if UNITY_ANDROID && !UNITY_EDITOR
            string url = Path.Combine(Application.streamingAssetsPath, file);
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                    throw new System.IO.FileNotFoundException($"catalog load failed: {req.error}");
                loaded = ParseJson(req.downloadHandler.text);
            }
#else
            string path = Path.Combine(Application.streamingAssetsPath, file);
            loaded = ParseJson(File.ReadAllText(path));
            yield break;
#endif
        }

        // Test hook only.
        internal static void SetForTesting(SongEntry[] entries) => loaded = entries ?? System.Array.Empty<SongEntry>();
    }
}
```

Tests:

```csharp
using NUnit.Framework;

namespace KeyFlow.Tests.EditMode
{
    public class SongCatalogTests
    {
        private const string ValidJson = @"{
            ""version"": 1,
            ""songs"": [
                { ""id"": ""a"", ""title"": ""Song A"", ""composer"": ""X"",
                  ""thumbnail"": ""thumbs/a.png"", ""difficulties"": [""Easy"",""Normal""],
                  ""chartAvailable"": true },
                { ""id"": ""b"", ""title"": ""(locked)"", ""composer"": ""-"",
                  ""thumbnail"": ""thumbs/locked.png"", ""difficulties"": [],
                  ""chartAvailable"": false }
            ]
        }";

        [Test] public void ParseJson_Valid_Returns_Entries_With_Fields()
        {
            var entries = SongCatalog.ParseJson(ValidJson);
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual("a", entries[0].id);
            Assert.AreEqual("Song A", entries[0].title);
            Assert.AreEqual(2, entries[0].difficulties.Length);
            Assert.IsTrue(entries[0].chartAvailable);
            Assert.IsFalse(entries[1].chartAvailable);
        }

        [Test] public void ParseJson_MissingSongs_Throws()
        {
            Assert.Throws<System.FormatException>(() => SongCatalog.ParseJson(@"{""version"":1}"));
        }

        [Test] public void ParseJson_EntryMissingId_Throws()
        {
            string bad = @"{""songs"":[{""title"":""x""}]}";
            Assert.Throws<System.FormatException>(() => SongCatalog.ParseJson(bad));
        }

        [Test] public void TryGet_ReturnsHitAndMiss()
        {
            var entries = SongCatalog.ParseJson(ValidJson);
            SongCatalog.SetForTesting(entries);
            Assert.IsTrue(SongCatalog.TryGet("a", out var a));
            Assert.AreEqual("Song A", a.title);
            Assert.IsFalse(SongCatalog.TryGet("missing", out _));
        }
    }
}
```

- [ ] **Step 1:** Write tests → FAIL.
- [ ] **Step 2:** Implement `SongEntry` + `SongCatalog`.
- [ ] **Step 3:** Tests → 4/4 pass (85/85 total).
- [ ] **Step 4: Commit:**
```bash
git add Assets/Scripts/Catalog/ Assets/Tests/EditMode/SongCatalogTests.cs
git commit -m "feat(catalog): SongCatalog JSON parser + loader

Mirrors ChartLoader split: pure ParseJson for tests, LoadAsync
for StreamingAssets (UnityWebRequest on Android, File.ReadAllText
in editor). +4 tests (85 total)."
```

---

## Task 10: catalog.kfmanifest + thumbnail/star sprites

**Files:**
- Create: `Assets/StreamingAssets/catalog.kfmanifest`
- Modify: `Assets/Editor/SceneBuilder.cs` (add `EnsureStarSprite(bool filled)`, `EnsureThumbnailSprite(string id)`, `EnsureLockedThumbSprite()`)

**Intent:** Data + procedural placeholder assets for Task 11 to consume.

`Assets/StreamingAssets/catalog.kfmanifest`:
```json
{
  "version": 1,
  "songs": [
    {
      "id": "beethoven_fur_elise",
      "title": "엘리제를 위하여",
      "composer": "Beethoven",
      "thumbnail": "thumbs/fur_elise.png",
      "difficulties": ["Easy", "Normal"],
      "chartAvailable": true
    },
    { "id": "placeholder_w5_1", "title": "(W5에 공개)", "composer": "—",
      "thumbnail": "thumbs/locked.png", "difficulties": [], "chartAvailable": false },
    { "id": "placeholder_w5_2", "title": "(W5에 공개)", "composer": "—",
      "thumbnail": "thumbs/locked.png", "difficulties": [], "chartAvailable": false },
    { "id": "placeholder_w5_3", "title": "(W5에 공개)", "composer": "—",
      "thumbnail": "thumbs/locked.png", "difficulties": [], "chartAvailable": false },
    { "id": "placeholder_w5_4", "title": "(W5에 공개)", "composer": "—",
      "thumbnail": "thumbs/locked.png", "difficulties": [], "chartAvailable": false }
  ]
}
```

**Note:** The chart currently only contains `Easy` (per W3 completion report). The manifest advertising `["Easy", "Normal"]` for Für Elise is aspirational — Normal chart arrives in W5. **In Task 11 `SongCardView`, treat "Normal" button as disabled if `ChartLoader` can't find the difficulty, OR simpler: trust the manifest and let Normal load fail at runtime with a log; for MVP, disable Normal button in-card when the manifest's `difficulties` array contains it but we know the chart is Easy-only**. Actually, simpler path: **change the manifest to `["Easy"]` until W5**, since the UI reads the manifest and won't offer a button that would fail. Adjust: Für Elise `"difficulties": ["Easy"]` for W4; W5 plan will flip to `["Easy", "Normal"]`.

**Update to manifest:**
```json
"difficulties": ["Easy"],
```

`SceneBuilder` additions:

```csharp
private const string StarFilledPath = "Assets/Sprites/star_filled.png";
private const string StarEmptyPath = "Assets/Sprites/star_empty.png";
private const string ThumbsDir = "Assets/StreamingAssets/thumbs";
private const string LockedThumbPath = "Assets/StreamingAssets/thumbs/locked.png";
private const string FurEliseThumbPath = "Assets/StreamingAssets/thumbs/fur_elise.png";

private static Sprite EnsureStarSprite(bool filled)
{
    string path = filled ? StarFilledPath : StarEmptyPath;
    var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
    if (existing != null) return existing;

    const int size = 64;
    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    Color clear = new Color(0, 0, 0, 0);
    Color fill = filled ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.4f, 0.4f, 0.4f, 0.6f);
    var pixels = new Color[size * size];
    for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

    // 5-point star via analytic polygon rasterize
    Vector2 center = new Vector2(size / 2f, size / 2f);
    float outer = 28f, inner = 12f;
    Vector2[] verts = new Vector2[10];
    for (int i = 0; i < 10; i++)
    {
        float r = (i % 2 == 0) ? outer : inner;
        float a = Mathf.PI / 2f - i * Mathf.PI / 5f;  // start at top, 36° steps
        verts[i] = center + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
    }

    // Rasterize by point-in-polygon test
    for (int y = 0; y < size; y++)
    for (int x = 0; x < size; x++)
    {
        if (PointInPolygon(new Vector2(x + 0.5f, y + 0.5f), verts))
            pixels[y * size + x] = fill;
    }

    tex.SetPixels(pixels);
    tex.Apply();
    File.WriteAllBytes(path, tex.EncodeToPNG());
    Object.DestroyImmediate(tex);

    AssetDatabase.ImportAsset(path);
    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
    if (importer != null)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }
    return AssetDatabase.LoadAssetAtPath<Sprite>(path);
}

private static bool PointInPolygon(Vector2 p, Vector2[] poly)
{
    bool inside = false;
    int j = poly.Length - 1;
    for (int i = 0; i < poly.Length; i++)
    {
        if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
            p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
            inside = !inside;
        j = i;
    }
    return inside;
}

private static void EnsureThumbnailAssets()
{
    EnsureFolder(ThumbsDir);
    // Locked thumbnail: dark gray 128x128 with diagonal stripes
    if (!File.Exists(LockedThumbPath))
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var p = new Color[size * size];
        Color bg = new Color(0.2f, 0.2f, 0.22f, 1f);
        Color stripe = new Color(0.3f, 0.3f, 0.32f, 1f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            p[y * size + x] = (((x + y) / 8) % 2 == 0) ? bg : stripe;
        tex.SetPixels(p); tex.Apply();
        File.WriteAllBytes(LockedThumbPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(LockedThumbPath);
    }
    // Für Elise thumbnail: dark blue 128x128 with single musical-note-ish white glyph (simple rectangle for MVP)
    if (!File.Exists(FurEliseThumbPath))
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var p = new Color[size * size];
        Color bg = new Color(0.15f, 0.2f, 0.4f, 1f);
        Color fg = new Color(0.95f, 0.9f, 0.7f, 1f);
        for (int i = 0; i < p.Length; i++) p[i] = bg;
        // Simple "F" letter in the center
        for (int y = 32; y < 96; y++) for (int x = 48; x < 56; x++) p[y * size + x] = fg;
        for (int y = 88; y < 96; y++) for (int x = 48; x < 80; x++) p[y * size + x] = fg;
        for (int y = 60; y < 68; y++) for (int x = 48; x < 72; x++) p[y * size + x] = fg;
        tex.SetPixels(p); tex.Apply();
        File.WriteAllBytes(FurEliseThumbPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(FurEliseThumbPath);
    }
}
```

Call `EnsureStarSprite(true)`, `EnsureStarSprite(false)`, `EnsureThumbnailAssets()` at the start of `SceneBuilder.Build()`.

Also add `EnsureFolder` call to create `Assets/StreamingAssets/thumbs` (uses existing helper).

- [ ] **Step 1:** Write manifest JSON file. Verify JSON syntactically valid.
- [ ] **Step 2:** Add sprite generation methods + call them in `Build()`.
- [ ] **Step 3:** Run `SceneBuilder.Build`. Verify files appear: `Assets/Sprites/star_filled.png`, `star_empty.png`, `Assets/StreamingAssets/thumbs/locked.png`, `fur_elise.png`.
- [ ] **Step 4:** Run tests — 85/85 still pass. `SongCatalog` isn't loaded at test time yet.
- [ ] **Step 5: Commit:**
```bash
git add Assets/StreamingAssets/catalog.kfmanifest \
        Assets/StreamingAssets/thumbs/ \
        Assets/Sprites/star_filled.png \
        Assets/Sprites/star_empty.png \
        Assets/Editor/SceneBuilder.cs
git commit -m "feat(assets): catalog.kfmanifest + procedural star/thumb sprites

1 real + 4 placeholder entries. 5-point star (filled gold / empty
gray outline), locked stripe thumbnail, Für Elise F-glyph placeholder.
All sprites generated procedurally in SceneBuilder (no art deps)."
```

---

## Task 11: SongCardView + MainScreen

**Files:**
- Create: `Assets/Scripts/UI/SongCardView.cs`
- Create: `Assets/Scripts/UI/MainScreen.cs`

**Intent:** UI logic components. Actual Canvas wiring happens in Task 12 (SceneBuilder).

`SongCardView.cs` — a prefab-less component that binds a preconstructed card GameObject:

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    public class SongCardView : MonoBehaviour
    {
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text composerText;
        [SerializeField] private Image[] starImages;  // length 3
        [SerializeField] private Button easyButton;
        [SerializeField] private Button normalButton;
        [SerializeField] private CanvasGroup canvasGroup;

        [SerializeField] private Sprite starFilled;
        [SerializeField] private Sprite starEmpty;

        public event Action<string, Difficulty> OnDifficultySelected;

        public void Bind(SongEntry entry, Sprite thumbnail)
        {
            titleText.text = entry.title;
            composerText.text = entry.composer;
            if (thumbnail != null) thumbnailImage.sprite = thumbnail;

            int bestStars = entry.chartAvailable
                ? UserPrefs.GetBestStars(entry.id, Difficulty.Easy)
                : 0;
            for (int i = 0; i < starImages.Length; i++)
                starImages[i].sprite = (i < bestStars) ? starFilled : starEmpty;

            bool easy = entry.chartAvailable && entry.HasDifficulty(Difficulty.Easy);
            bool normal = entry.chartAvailable && entry.HasDifficulty(Difficulty.Normal);
            easyButton.interactable = easy;
            normalButton.interactable = normal;
            canvasGroup.alpha = entry.chartAvailable ? 1f : 0.5f;

            easyButton.onClick.RemoveAllListeners();
            normalButton.onClick.RemoveAllListeners();
            if (easy) easyButton.onClick.AddListener(() => OnDifficultySelected?.Invoke(entry.id, Difficulty.Easy));
            if (normal) normalButton.onClick.AddListener(() => OnDifficultySelected?.Invoke(entry.id, Difficulty.Normal));
        }
    }
}
```

`MainScreen.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    public class MainScreen : MonoBehaviour
    {
        [SerializeField] private Transform cardContainer;    // ScrollRect.content
        [SerializeField] private GameObject cardPrefab;      // SongCardView attached; SceneBuilder constructs
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsScreen settingsOverlay;
        [SerializeField] private Sprite starFilled;
        [SerializeField] private Sprite starEmpty;

        private readonly List<SongCardView> cards = new();

        private IEnumerator Start()
        {
            settingsButton.onClick.AddListener(() => settingsOverlay.Show());
            yield return SongCatalog.LoadAsync();
            PopulateCards();
        }

        public void Refresh()
        {
            foreach (var card in cards)
            {
                // Re-read best stars (in case user just finished a song and came back)
                // Simplest: re-populate from catalog.
            }
            // Rebuild for simplicity (up to 5 cards):
            foreach (Transform child in cardContainer) Destroy(child.gameObject);
            cards.Clear();
            PopulateCards();
        }

        private void PopulateCards()
        {
            foreach (var entry in SongCatalog.All)
            {
                var go = Instantiate(cardPrefab, cardContainer);
                var card = go.GetComponent<SongCardView>();
                SetPrivate(card, "starFilled", starFilled);
                SetPrivate(card, "starEmpty", starEmpty);
                StartCoroutine(LoadThumbnailThenBind(card, entry));
                card.OnDifficultySelected += HandleCardTap;
                cards.Add(card);
            }
        }

        private IEnumerator LoadThumbnailThenBind(SongCardView card, SongEntry entry)
        {
            Sprite sprite = null;
            string path = Path.Combine(Application.streamingAssetsPath, entry.thumbnail);
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var req = UnityWebRequestTexture.GetTexture(path))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
#else
            if (File.Exists(path))
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            yield break;
#endif
            card.Bind(entry, sprite);
        }

        private void HandleCardTap(string songId, Difficulty difficulty)
        {
            SongSession.CurrentSongId = songId;
            SongSession.CurrentDifficulty = difficulty;
            ScreenManager.Instance.Replace(Screen.Gameplay);
        }

        private static void SetPrivate(object t, string name, object v) =>
            t.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
              .SetValue(t, v);
    }
}
```

**Decisions baked in:**
- Card prefab built procedurally by SceneBuilder (no real `Prefab` asset — SceneBuilder makes an inactive template GO and `MainScreen.cardPrefab` references that, then `Instantiate` clones it). Actually Unity `Instantiate(go, parent)` works fine on scene GameObjects. The template GO is a child that stays inactive; clones get `SetActive(true)`. Spell this out in Task 12.
- No pooling — 5 cards, negligible.
- `Refresh()` is called when returning from Results (Task 18 wires this via a `ScreenManager.OnReplaced` subscription).

- [ ] **Step 1:** Write both files. No tests (heavy Unity dependencies; covered by device validation).
- [ ] **Step 2:** Compile-check by running `SceneBuilder.Build` — scene rebuilds, tests 85/85.
- [ ] **Step 3: Commit:**
```bash
git add Assets/Scripts/UI/SongCardView.cs Assets/Scripts/UI/MainScreen.cs
git commit -m "feat(ui): SongCardView + MainScreen logic

Card binds one SongEntry (title, composer, 3 stars, Easy/Normal
buttons). Locked (chartAvailable=false) cards show at 50% alpha
with buttons non-interactable. MainScreen loads catalog + thumbnails
async, stamps SongSession on tap, Replaces to Gameplay. SceneBuilder
wires in next task."
```

---

## Task 12: SceneBuilder MainCanvas + SongSession wiring in GameplayController

**Files:**
- Modify: `Assets/Editor/SceneBuilder.cs` (add `BuildMainCanvas`, wire ScreenManager.mainRoot, SettingsScreen is still null — fill in Task 16)
- Modify: `Assets/Scripts/Gameplay/GameplayController.cs` (drop the `?? "beethoven_fur_elise"` fallback added in Task 6, trust SongSession)

**Intent:** Construct the `MainCanvas` hierarchy + card template + wire up `ScreenManager.mainRoot` to it. Start the app on Main.

`BuildMainCanvas` structure:

```
MainCanvas (Canvas, ScreenSpaceOverlay, sortingOrder=5)
├── CanvasScaler (720x1280 ref)
├── GraphicRaycaster
├── Header (anchor top, height 120)
│   ├── TitleText "KeyFlow"
│   └── SettingsButton (⚙ text)
├── ScrollView (anchor stretch)
│   ├── Viewport (Image mask + RectMask2D)
│   │   └── Content (VerticalLayoutGroup + ContentSizeFitter)
│   │       └── CardTemplate (SetActive(false), SongCardView)
└── MainScreen (MonoBehaviour)
```

Procedural card template (inside CardTemplate GO):
- Horizontal layout: Thumbnail 112px | VStack { Title 18sp, Composer 14sp, Stars (3 Image) } | VStack { Easy button, Normal button }
- `SongCardView` with fields wired via `SetField`

Coding notes:
- `VerticalLayoutGroup` on Content with spacing=12, padding 16 all sides.
- `ContentSizeFitter.verticalFit = PreferredSize`.
- `LayoutElement.preferredHeight = 150` on card.
- Use `whiteSprite` for backgrounds.

Also on `Build()`:
- After all canvases built, set `screenMgr.mainRoot = mainCanvasGO`.
- Set `screenMgr` initial state: not strictly needed since `ScreenManager.Awake → Instance=this` and we need app to start on Main. Add: `screenMgr.Replace(Screen.Main)` called from a separate bootstrap MonoBehaviour or trigger manually. **Simplest:** `ScreenManager.Awake` calls `Replace(Screen.Main)` itself (see Task 7 code — remove that line there and move to Task 12 once we know all roots are wired). OR: leave it in Awake; on scene load, `mainRoot` is already wired via SerializeField so `SetActive(true)` works on the right GO. **Go with keeping `Replace(Screen.Main)` in `Awake`.** Adjust Task 7's implementation post-hoc.

Actually — Task 7 already did `Awake() { Instance = this; Replace(Screen.Main); }`. But tests set private fields via reflection AFTER Awake runs (Awake fires on `AddComponent`). So the test `Replace(Screen.Main)` call won't find wired roots. **Fix in Task 7's code: move `Replace(Screen.Main)` out of Awake, into a `Start()` method.** Correct the Task 7 implementation:

```csharp
private void Awake() { Instance = this; }
private void Start() { Replace(Screen.Main); }
```

OR better: tests already call `Replace` explicitly. Just remove the Awake-side `Replace`. `ScreenManager.Start` kicks the initial screen.

**If Task 7 was written as shown above (Replace in Awake), this task amends it.**

GameplayController adjustment:
```csharp
private void Start()
{
    UserPrefs.MigrateLegacy();
    string songId = SongSession.CurrentSongId;
    if (string.IsNullOrEmpty(songId))
    {
        Debug.LogError("[KeyFlow] GameplayController.Start with no SongSession.CurrentSongId");
        return;
    }
    chart = ChartLoader.LoadFromStreamingAssets(songId);
    // ... (existing calibration check)
}
```

- [ ] **Step 1:** Amend `ScreenManager` — move `Replace(Screen.Main)` to `Start()`.
- [ ] **Step 2:** Implement `BuildMainCanvas` in SceneBuilder. Add call in `Build()` before `ScreenManager` wiring. Wire `screenMgr.mainRoot`, `screenMgr.settingsOverlay = null` (filled Task 16), etc.
- [ ] **Step 3:** Amend `GameplayController.Start`.
- [ ] **Step 4:** Run `SceneBuilder.Build`. Open scene. In Editor play mode: Main appears, Für Elise card shown, Easy button tappable, placeholder cards at 50% alpha. Tap Easy → Gameplay begins (calibration flow if needed).
- [ ] **Step 5:** Tests: 85/85.
- [ ] **Step 6: Commit:**
```bash
git add Assets/Editor/SceneBuilder.cs \
        Assets/Scripts/UI/ScreenManager.cs \
        Assets/Scripts/Gameplay/GameplayController.cs
git commit -m "feat(scene): MainCanvas + SongSession-driven gameplay bootstrap

5 song cards rendering with procedural card template. Tap flows
SongSession → ScreenManager.Replace(Gameplay). GameplayController
trusts SongSession.CurrentSongId (no hardcoded fallback). ScreenManager
initial Replace moved from Awake→Start to play nice with Serialize
wiring order."
```

---

## Task 13: AudioSyncManager Pause/Resume + tests

**Files:**
- Modify: `Assets/Scripts/Gameplay/AudioSyncManager.cs`
- Create: `Assets/Tests/EditMode/AudioSyncPauseTests.cs`

**Intent:** Pause freezes `SongTimeMs`. Resume shifts `songStartDsp` by paused-elapsed so `SongTimeMs` continues from where it stopped. EditMode testable via an internal time-source seam.

Add `ITimeSource` seam:

```csharp
internal interface ITimeSource { double DspTime { get; } }

internal class RealTimeSource : ITimeSource
{
    public double DspTime => AudioSettings.dspTime;
}
```

Modify `AudioSyncManager`:

```csharp
public class AudioSyncManager : MonoBehaviour
{
    [SerializeField] private double scheduleLeadSec = 0.5;

    private AudioSource bgmSource;
    private double songStartDsp;
    private bool started;

    internal ITimeSource TimeSource { get; set; } = new RealTimeSource();  // tests swap

    // Pause state
    private bool paused;
    private double pauseStartDsp;
    public bool IsPaused => paused;

    public double CalibrationOffsetSec { get; set; } = 0.0;

    public int SongTimeMs
    {
        get
        {
            if (!started) return 0;
            double nowDsp = paused ? pauseStartDsp : TimeSource.DspTime;
            return GameTime.GetSongTimeMs(nowDsp, songStartDsp, CalibrationOffsetSec);
        }
    }

    private void Awake()
    {
        bgmSource = GetComponent<AudioSource>();
        bgmSource.playOnAwake = false;
    }

    public void StartSilentSong()
    {
        songStartDsp = TimeSource.DspTime + scheduleLeadSec;
        started = true;
        paused = false;
    }

    public void StartSong(AudioClip bgm)
    {
        bgmSource.clip = bgm;
        songStartDsp = TimeSource.DspTime + scheduleLeadSec;
        bgmSource.PlayScheduled(songStartDsp);
        started = true;
        paused = false;
    }

    public void Pause()
    {
        if (paused || !started) return;
        pauseStartDsp = TimeSource.DspTime;
        AudioListener.pause = true;
        paused = true;
    }

    public void Resume()
    {
        if (!paused) return;
        double elapsed = TimeSource.DspTime - pauseStartDsp;
        songStartDsp += elapsed;
        AudioListener.pause = false;
        paused = false;
    }
}
```

Tests — inject a manual `ITimeSource`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace KeyFlow.Tests.EditMode
{
    public class AudioSyncPauseTests
    {
        private class ManualClock : ITimeSource
        {
            public double DspTime { get; set; }
        }

        private GameObject go;
        private AudioSyncManager sync;
        private ManualClock clock;

        [SetUp] public void Setup()
        {
            go = new GameObject("sync");
            go.AddComponent<AudioSource>();
            sync = go.AddComponent<AudioSyncManager>();
            clock = new ManualClock { DspTime = 100.0 };
            sync.TimeSource = clock;
        }

        [TearDown] public void Teardown() { Object.DestroyImmediate(go); }

        [Test] public void SongTimeMs_IsFrozenWhilePaused()
        {
            sync.StartSilentSong();
            clock.DspTime = 101.0;  // 0.5s after startLead (scheduleLeadSec=0.5)
            int t0 = sync.SongTimeMs;
            sync.Pause();
            clock.DspTime = 105.0;
            Assert.AreEqual(t0, sync.SongTimeMs);
        }

        [Test] public void SongTimeMs_ContinuesAfterResume()
        {
            sync.StartSilentSong();
            clock.DspTime = 102.0;
            int before = sync.SongTimeMs;
            sync.Pause();
            clock.DspTime = 110.0;  // 8s of paused time
            sync.Resume();
            clock.DspTime = 111.0;  // +1s of real progression
            int after = sync.SongTimeMs;
            Assert.AreEqual(before + 1000, after, "SongTime should advance by only the non-paused delta");
        }

        [Test] public void PauseAndResume_AreIdempotent()
        {
            sync.StartSilentSong();
            clock.DspTime = 101.0;
            sync.Pause();
            clock.DspTime = 102.0;
            sync.Pause();   // no-op
            clock.DspTime = 103.0;
            sync.Resume();
            sync.Resume();  // no-op
            // After 2s of paused (between Pause and Resume), then 0s elapsed
            // SongTime should equal value at first Pause
            Assert.IsFalse(sync.IsPaused);
        }
    }
}
```

**Note:** `AudioListener.pause = true` runs even in EditMode but has no audible effect; it doesn't affect `SongTimeMs` (which is pure math against `TimeSource`). Tests are clean.

- [ ] **Step 1:** Write tests → FAIL (interface doesn't exist).
- [ ] **Step 2:** Implement changes to `AudioSyncManager`.
- [ ] **Step 3:** Run tests → 3/3 pass (88/88 total).
- [ ] **Step 4: Commit:**
```bash
git add Assets/Scripts/Gameplay/AudioSyncManager.cs \
        Assets/Tests/EditMode/AudioSyncPauseTests.cs
git commit -m "feat(audio): AudioSyncManager.Pause/Resume with dspTime anchor shift

Pause freezes SongTimeMs by snapshotting dspTime. Resume advances
songStartDsp by the paused-elapsed so song time continues from the
freeze point. ITimeSource internal seam lets EditMode tests drive
a ManualClock without AudioSettings. +3 tests (88 total)."
```

---

## Task 14: Pause guards across spawner/note/tap/hold/judgment

**Files (all modify):**
- `Assets/Scripts/Gameplay/NoteSpawner.cs`
- `Assets/Scripts/Gameplay/NoteController.cs`
- `Assets/Scripts/Gameplay/TapInputHandler.cs`
- `Assets/Scripts/Gameplay/HoldTracker.cs`
- `Assets/Scripts/Gameplay/JudgmentSystem.cs`

Add `if (audioSync.IsPaused) return;` at the top of each `Update` (or before the main logic) in each file. `NoteController` doesn't have an `audioSync` reference — it has a progress source via `NoteSpawner`; add a `[SerializeField] AudioSyncManager audioSync` or pass IsPaused in via NoteSpawner iteration. **Simpler path:** `NoteSpawner.Update` is what iterates + positions notes. Guard only the spawner's Update, and NoteController's own Update (if any) just does render animation. Verify NoteController has its own Update.

Check:
- `NoteController` has a `Update` that reads `spawner.audioSync.SongTimeMs` through an accessor OR it's passive (position driven externally). Read the file.

Looking at W3: `NoteController` reads position via a reference to something that provides time. Let me re-check.

**Ground-truthed during implementation:** inspect each file; add guard only where `Update` does time-dependent work.

- `TapInputHandler.Update` — needs guard: if paused, don't emit `OnLaneTap` / `OnLaneRelease`.
- `JudgmentSystem.Update` — needs guard (if it has one; might be event-driven only).
- `HoldTracker.Update` — needs guard.
- `NoteSpawner.Update` — needs guard (prevents spawning new notes + advancing positions).

For each: capture before/after behavior in the device test (Task 20).

- [ ] **Step 1:** Inspect each file, identify `Update` methods.
- [ ] **Step 2:** Add `if (audioSync != null && audioSync.IsPaused) return;` at top of each relevant `Update`. For `TapInputHandler` whose `audioSync` might not be serialized, add or re-use the existing `AudioSyncManager` field reference.
- [ ] **Step 3:** Build scene. Run a play test in Editor: Start game → play a few seconds → call `audioSync.Pause()` via a test button (temporary Debug key `P` bound in LatencyMeter or similar) OR postpone to Task 15 where PauseScreen wires it. **Postpone playtest to Task 15.**
- [ ] **Step 4:** EditMode tests: 88/88. No new tests — behavior is integration-tested on device.
- [ ] **Step 5: Commit:**
```bash
git add Assets/Scripts/Gameplay/
git commit -m "feat(pause): add IsPaused guards to spawner/note/tap/hold/judgment

Each Update returns early when audioSync.IsPaused. Integration-verified
on device in Task 20."
```

---

## Task 15: PauseScreen + HUD pause button + SceneBuilder

**Files:**
- Create: `Assets/Scripts/UI/PauseScreen.cs`
- Modify: `Assets/Editor/SceneBuilder.cs` (add `BuildPauseCanvas`, add HUD ⏸ button, wire `ScreenManager.pauseOverlay`)

`PauseScreen.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    public class PauseScreen : OverlayBase
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private AudioSyncManager audioSync;

        protected override void Awake()
        {
            base.Awake();
            resumeButton.onClick.AddListener(OnResume);
            quitButton.onClick.AddListener(OnQuit);
        }

        protected override void OnShown()
        {
            if (audioSync != null) audioSync.Pause();
        }

        private void OnResume()
        {
            if (audioSync != null) audioSync.Resume();
            Finish();
        }

        private void OnQuit()
        {
            if (audioSync != null) audioSync.Resume();  // clean state
            Finish();
            ScreenManager.Instance.Replace(Screen.Main);
        }
    }
}
```

SceneBuilder `BuildPauseCanvas`:

- Canvas, sortingOrder 15, ScreenSpaceOverlay, dark overlay BG.
- Texts: "일시정지" (UIStrings.Paused, 48sp, top-center), "계속하기" button (UIStrings.Resume), "메인으로 나가기" button (UIStrings.QuitToMain).

HUD ⏸ button — add in `BuildHUD`:
- Child of HUDCanvas, anchored top-left, 80×80, uses whiteSprite with ▶ (actually ⏸) glyph; for MVP, "II" via two vertical stripes.
- OnClick: `ScreenManager.Instance.ShowOverlay(pauseOverlay)`. Problem: `BuildHUD` runs before `pauseOverlay` exists. **Solution:** add a `BuildHUD` signature change to return the pause button reference, wire in the OnClick after `pauseOverlay` is built. Or add a `HUDController.cs` MonoBehaviour that finds `ScreenManager.Instance` at runtime and calls `ShowOverlay(pauseOverlay)` via another ref. **Go with**: a tiny `HUDPauseButton.cs` component that holds a `[SerializeField] OverlayBase pauseOverlay` and wires the button OnClick at Awake. SceneBuilder wires the ref after pauseOverlay is built.

Actually simpler: `BuildPauseCanvas` then wire HUD pause button after via `SetField`. Refactor:

```csharp
// In Build():
var hudPauseButton = BuildHUD(...);  // now returns Button
var pauseScreen = BuildPauseCanvas(...);
hudPauseButton.onClick.AddListener(() => ScreenManager.Instance.ShowOverlay(pauseScreen));
```

But Unity serializes `onClick` on the scene `Button`. A lambda added in the editor script is a "runtime listener" — it's NOT serialized with the scene. It'll vanish when the scene is saved. Solution: use `Button.persistentCalls` via `UnityEventTools` — complex. **Better**: create `HUDPauseButton.cs` with a `[SerializeField] PauseScreen pauseOverlay` field:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    [RequireComponent(typeof(Button))]
    public class HUDPauseButton : MonoBehaviour
    {
        [SerializeField] private PauseScreen pauseOverlay;
        private Button button;
        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(() => pauseOverlay.Show());
        }
    }
}
```

`SceneBuilder` adds `HUDPauseButton` to the HUD ⏸ button and sets the `pauseOverlay` ref via `SetField` after PauseCanvas is built.

- [ ] **Step 1:** Create `PauseScreen.cs` + `HUDPauseButton.cs`.
- [ ] **Step 2:** Add `BuildPauseCanvas` to SceneBuilder, wire ScreenManager.pauseOverlay, wire HUDPauseButton.pauseOverlay.
- [ ] **Step 3:** Build scene + play test in Editor: Tap Easy → Gameplay → tap ⏸ → Pause overlay shows, notes freeze, audio stops. Resume → continues. Quit → Main. Also test Android Back = Pause (Editor: press ESC key).
- [ ] **Step 4:** Device test (deferred to Task 20, but Editor test serves now).
- [ ] **Step 5:** EditMode tests: 88/88.
- [ ] **Step 6: Commit:**
```bash
git add Assets/Scripts/UI/PauseScreen.cs Assets/Scripts/UI/HUDPauseButton.cs \
        Assets/Editor/SceneBuilder.cs
git commit -m "feat(ui): PauseScreen overlay + HUD pause button

Pause overlay with Resume/Quit. OnShown calls audioSync.Pause; OnResume
calls Resume. Quit returns to Main. HUD ⏸ button wires to PauseScreen
via HUDPauseButton MonoBehaviour (Unity serializer-safe). Editor play
test confirmed pause freezes notes and audio; Resume continues from
freeze point without drift."
```

---

## Task 16: SettingsScreen + SceneBuilder + Calibration re-run

**Files:**
- Create: `Assets/Scripts/UI/SettingsScreen.cs`
- Modify: `Assets/Editor/SceneBuilder.cs` (add `BuildSettingsCanvas`, wire MainScreen.settingsOverlay, ScreenManager.settingsOverlay)

`SettingsScreen.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using KeyFlow.Calibration;

namespace KeyFlow.UI
{
    public class SettingsScreen : OverlayBase
    {
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider noteSpeedSlider;
        [SerializeField] private Text noteSpeedValueLabel;
        [SerializeField] private Button recalibrateButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text versionLabel;
        [SerializeField] private CalibrationController calibration;

        protected override void Awake()
        {
            base.Awake();
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxChanged);

            noteSpeedSlider.minValue = 1.0f;
            noteSpeedSlider.maxValue = 3.0f;
            noteSpeedSlider.onValueChanged.AddListener(OnNoteSpeedChanged);

            recalibrateButton.onClick.AddListener(OnRecalibrate);
            closeButton.onClick.AddListener(Finish);

            versionLabel.text = string.Format(UIStrings.VersionLabelFmt, Application.version);
        }

        protected override void OnShown()
        {
            // Reload from prefs (they may have been changed elsewhere — defensive)
            sfxVolumeSlider.SetValueWithoutNotify(UserPrefs.SfxVolume);
            noteSpeedSlider.SetValueWithoutNotify(UserPrefs.NoteSpeed);
            noteSpeedValueLabel.text = UserPrefs.NoteSpeed.ToString("F1");
            AudioListener.volume = UserPrefs.SfxVolume;
        }

        private void OnSfxChanged(float v)
        {
            UserPrefs.SfxVolume = v;
            AudioListener.volume = v;
        }

        private void OnNoteSpeedChanged(float v)
        {
            UserPrefs.NoteSpeed = v;
            noteSpeedValueLabel.text = v.ToString("F1");
        }

        private void OnRecalibrate()
        {
            Finish();
            calibration.Begin(onDone: () => Show());
        }
    }
}
```

SceneBuilder `BuildSettingsCanvas`:
- Canvas sortingOrder 12.
- Background black 80% alpha.
- Title text (UIStrings.SettingsTitle).
- Close ✕ button top-right.
- Label "효과음 볼륨" + Slider (0-1).
- Label "노트 속도" + Slider (1.0-3.0) + value readout text.
- "오디오 다시 맞추기" button.
- Version text bottom-right.
- Add `SettingsScreen` component; SetField all refs.

Also: wire `mainScreen.settingsOverlay = settingsScreen` and `screenMgr.settingsOverlay = settingsScreen` in `Build()`.

`MainScreen.cardContainer` already has a template ref; no SettingsScreen dependency there other than the settings button OnClick calling `settingsOverlay.Show()` which is wired in `MainScreen.Start()` (see Task 11 code).

- [ ] **Step 1:** Create `SettingsScreen.cs`.
- [ ] **Step 2:** Add `BuildSettingsCanvas` to SceneBuilder.
- [ ] **Step 3:** Build scene. Editor play test: Main → ⚙ → Settings opens. Move SFX slider → `AudioListener.volume` changes (audible on any SFX). Move NoteSpeed → value readout updates, `UserPrefs.NoteSpeed` changes. Tap "오디오 다시 맞추기" → Settings closes, Calibration starts, on completion Settings reopens. ✕ → closes.
- [ ] **Step 4:** EditMode tests: 88/88.
- [ ] **Step 5: Commit:**
```bash
git add Assets/Scripts/UI/SettingsScreen.cs Assets/Editor/SceneBuilder.cs
git commit -m "feat(ui): SettingsScreen overlay + recalibrate re-entry

SFX volume and NoteSpeed sliders write to UserPrefs. AudioListener.volume
reflects SFX immediately. Recalibrate triggers CalibrationController.Begin
with onDone callback that re-shows Settings. Editor play test confirmed
the re-entry loop."
```

---

## Task 17: ResultsScreen + SceneBuilder (replace CompletionPanel)

**Files:**
- Create: `Assets/Scripts/UI/ResultsScreen.cs`
- Delete: `Assets/Scripts/UI/CompletionPanel.cs`
- Modify: `Assets/Editor/SceneBuilder.cs` (swap `BuildCompletionPanel` → `BuildResultsCanvas`; wire ScreenManager.resultsCanvas)
- Modify: `Assets/Scripts/Gameplay/GameplayController.cs` (call `ResultsScreen.Show` on completion + `UserPrefs.TrySetBest` + `ScreenManager.Replace(Screen.Results)`)

`ResultsScreen.cs`:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    public class ResultsScreen : OverlayBase
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Image[] starImages;       // length 3
        [SerializeField] private Sprite starFilled;
        [SerializeField] private Sprite starEmpty;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text maxComboText;
        [SerializeField] private Text breakdownText;
        [SerializeField] private Text newRecordLabel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button homeButton;

        private const float CountupDuration = 1.5f;

        public void Display(ScoreManager score, bool isNewRecord)
        {
            Show();
            titleText.text = UIStrings.SongComplete;
            maxComboText.text = string.Format(UIStrings.MaxComboFmt, score.MaxCombo);
            breakdownText.text = string.Format(UIStrings.BreakdownFmt,
                score.PerfectCount, score.GreatCount, score.GoodCount, score.MissCount);
            newRecordLabel.gameObject.SetActive(isNewRecord);
            if (isNewRecord) newRecordLabel.text = UIStrings.NewRecord;

            retryButton.interactable = false;
            homeButton.interactable = false;

            foreach (var s in starImages) { s.sprite = starEmpty; s.transform.localScale = Vector3.zero; }
            scoreText.text = string.Format(UIStrings.ScoreFmt, 0);

            StartCoroutine(Animate(score.Stars, score.Score));
        }

        private IEnumerator Animate(int stars, int finalScore)
        {
            // Star pop: 0.2s interval, scale 0→1.2→1.0 over 0.3s each
            for (int i = 0; i < stars; i++)
            {
                starImages[i].sprite = starFilled;
                yield return StartCoroutine(ScalePop(starImages[i].transform));
                yield return new WaitForSeconds(0.05f);
            }
            // Any remaining empties stay at scale 0 — set them to scale 1 non-animated
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i].transform.localScale == Vector3.zero)
                    starImages[i].transform.localScale = Vector3.one;
            }

            // Score count-up
            float t = 0;
            while (t < CountupDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / CountupDuration);
                float eased = 1 - Mathf.Pow(1 - u, 2);  // EaseOut
                int val = Mathf.FloorToInt(finalScore * eased);
                scoreText.text = string.Format(UIStrings.ScoreFmt, val);
                yield return null;
            }
            scoreText.text = string.Format(UIStrings.ScoreFmt, finalScore);

            retryButton.interactable = true;
            homeButton.interactable = true;
        }

        private IEnumerator ScalePop(Transform tr)
        {
            const float dur = 0.3f;
            float t = 0;
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                float s = (u < 0.5f) ? Mathf.Lerp(0, 1.2f, u * 2) : Mathf.Lerp(1.2f, 1.0f, (u - 0.5f) * 2);
                tr.localScale = Vector3.one * s;
                yield return null;
            }
            tr.localScale = Vector3.one;
        }

        protected override void Awake()
        {
            base.Awake();
            retryButton.onClick.AddListener(OnRetry);
            homeButton.onClick.AddListener(OnHome);
        }

        private void OnRetry()
        {
            Finish();
            ScreenManager.Instance.Replace(Screen.Gameplay);
            // GameplayController subscribes to OnReplaced(Gameplay) or similar — see Task 18
        }

        private void OnHome()
        {
            Finish();
            ScreenManager.Instance.Replace(Screen.Main);
        }
    }
}
```

**Modify GameplayController.Update** (where completion currently calls `completionPanel.Show(judgmentSystem.Score)`):

```csharp
completed = true;
var score = judgmentSystem.Score;
bool newRecord = UserPrefs.TrySetBest(
    SongSession.CurrentSongId, SongSession.CurrentDifficulty,
    score.Stars, score.Score);
SongSession.LastScore = score;
ScreenManager.Instance.Replace(Screen.Results);
resultsScreen.Display(score, newRecord);
```

Add `[SerializeField] ResultsScreen resultsScreen;` to GameplayController and delete `[SerializeField] CompletionPanel completionPanel;`.

SceneBuilder: rename `BuildCompletionPanel` to `BuildResultsCanvas`, add stars (3 Images using `starFilled`/`starEmpty` sprites loaded via `EnsureStarSprite`), maxCombo text, breakdown text, newRecord label (hidden by default), Retry + Home buttons. Wire `ScreenManager.resultsCanvas = resultsCanvasGO` and update `BuildGameplayController` to set `resultsScreen` field instead of `completionPanel`.

- [ ] **Step 1:** Delete `Assets/Scripts/UI/CompletionPanel.cs`.
- [ ] **Step 2:** Create `ResultsScreen.cs`.
- [ ] **Step 3:** Update `GameplayController.cs` (replace completion-handling block).
- [ ] **Step 4:** Replace `BuildCompletionPanel` → `BuildResultsCanvas` in SceneBuilder. Update `BuildGameplayController` field names.
- [ ] **Step 5:** Build scene. Editor play test: play through Für Elise Easy → Results shows with stars popping, score counting up, breakdown present. Retry and Home buttons disabled until animation ends (~1.8s). Verify newRecord shown on first completion.
- [ ] **Step 6:** EditMode tests: 88/88. CompletionPanel tests — there were none.
- [ ] **Step 7: Commit:**
```bash
git add -A Assets/Scripts/UI/ Assets/Scripts/Gameplay/GameplayController.cs Assets/Editor/SceneBuilder.cs
git commit -m "feat(ui): ResultsScreen replaces CompletionPanel

Star pop (scale 0→1.2→1.0, 0.2s interval). Score count-up 1.5s
EaseOut. Breakdown P/G/G/M. NEW RECORD label on new-best. Retry
and Home buttons disabled until animation completes. GameplayController
now persists best via UserPrefs.TrySetBest and shows Results screen."
```

---

## Task 18: GameplayController.ResetAndStart + Retry wiring

**Files:** Modify `Assets/Scripts/Gameplay/GameplayController.cs`. Add `ResetForRetry` methods to `NoteSpawner`, `HoldTracker`, `JudgmentSystem` (they zero internal state).

**Intent:** Retry must not reload the scene. After `ScreenManager.Replace(Screen.Gameplay)` from Results, `GameplayController` needs an entry-point that wipes game state and starts fresh.

Approach: subscribe `GameplayController` to `ScreenManager.Instance.OnReplaced`. On `Screen.Gameplay` transition, call `ResetAndStart()`.

```csharp
private void OnEnable()
{
    if (ScreenManager.Instance != null)
        ScreenManager.Instance.OnReplaced += HandleScreenReplaced;
}

private void OnDisable()
{
    if (ScreenManager.Instance != null)
        ScreenManager.Instance.OnReplaced -= HandleScreenReplaced;
}

private void HandleScreenReplaced(Screen target)
{
    if (target == Screen.Gameplay) ResetAndStart();
}

public void ResetAndStart()
{
    playing = false;
    completed = false;

    spawner.ResetForRetry();          // destroy all live notes, reset index
    holdTracker.ResetForRetry();      // clear any hold state
    judgmentSystem.ResetForRetry();   // zero ScoreManager, re-init

    // Chart reload is cheap and safer than caching
    chart = ChartLoader.LoadFromStreamingAssets(SongSession.CurrentSongId);

    if (UserPrefs.HasCalibration)
    {
        audioSync.CalibrationOffsetSec = UserPrefs.CalibrationOffsetMs / 1000.0;
        BeginGameplay();
    }
    else
    {
        calibration.Begin(BeginGameplay);
    }
}
```

Also: remove `Start()` method entirely? No — Start fires once on scene load for the initial Main → Gameplay transition. Actually if `OnEnable → OnReplaced(Gameplay) → ResetAndStart` fires on the first entry too (user taps a card → Replace(Gameplay)), there's duplication with `Start()`. Solution: delete `Start()`'s body and rely entirely on `OnReplaced`.

But `OnEnable` fires on scene load even before MainScreen has replaced anything. `ScreenManager.Instance` might be null at that point. Sequence audit:
1. Scene loads. All Awake() run (including ScreenManager's, setting Instance).
2. All Start() run. `ScreenManager.Start` fires `Replace(Screen.Main)` — but this sets `gameplayRoot.SetActive(false)` → triggers `GameplayController.OnDisable` (if OnEnable had run). Actually OnEnable fires when GameObject becomes active; if `gameplayRoot` is initially active at Awake time then GameplayController.OnEnable already ran subscribing to events. Then SetActive(false) triggers OnDisable, unsubscribing. Good.
3. User taps Easy → `Replace(Gameplay)` → GameplayController OnEnable subscribes + OnReplaced fires → ResetAndStart.

OK this works. But initial scene layout: ScreenManager.Start runs AFTER all Awake. GameplayController's OnEnable runs AFTER its Awake. What was the state of GameplayRoot during Awake? If SceneBuilder leaves GameplayRoot active by default, OnEnable runs once with ScreenManager.Instance non-null, subscribes. Then ScreenManager.Start calls Replace(Main) which SetActives gameplayRoot(false) → OnDisable unsubscribes. Clean.

But wait: subscribing in OnEnable also catches the `Replace(Main)` call? No, because Replace(Main) doesn't fire OnReplaced... well it does (any Replace fires). But GameplayController's handler only acts on `Screen.Gameplay`. Safe.

Edge case: on the VERY first `Replace(Gameplay)` (user tapped card), GameplayController's GameObject was inactive; OnEnable runs when SetActive(true) flips, subscribes to `OnReplaced`. But by then, `OnReplaced` for Gameplay has ALREADY been invoked in the Replace method BEFORE our subscribe. We'd miss the first ResetAndStart call.

**Fix:** GameplayController's OnEnable should also call `ResetAndStart()` directly if `SongSession.CurrentSongId` is set and `!playing && !completed`:

```csharp
private void OnEnable()
{
    if (ScreenManager.Instance != null)
        ScreenManager.Instance.OnReplaced += HandleScreenReplaced;

    // First-time enable OR Retry re-enable: both need a fresh start
    if (!string.IsNullOrEmpty(SongSession.CurrentSongId) && !playing)
        ResetAndStart();
}
```

NoteSpawner additions:
```csharp
public void ResetForRetry()
{
    foreach (var live in liveNotes)  // assumes a list; may need to inspect current impl
        if (live != null) Destroy(live.gameObject);
    liveNotes.Clear();
    nextNoteIndex = 0;
    AllSpawned = false;
    LastSpawnedHitMs = 0;
    LastSpawnedDurMs = 0;
}
```

`HoldTracker.ResetForRetry()`: clear any active holds dictionary/list.

`JudgmentSystem.ResetForRetry()`:
```csharp
public void ResetForRetry()
{
    Score = new ScoreManager(...);  // re-instantiate
    // etc.
}
```

Inspect current impls during implementation — exact zero-reset steps depend on what state each class holds.

- [ ] **Step 1:** Implement `ResetForRetry` in NoteSpawner, HoldTracker, JudgmentSystem. May require inspecting W3 code.
- [ ] **Step 2:** Refactor `GameplayController` to OnEnable/OnDisable pattern + `ResetAndStart`.
- [ ] **Step 3:** Editor play test: complete Für Elise Easy → Results → Retry → Gameplay resets and plays again without scene reload. Score starts at 0, notes spawn from start. Test again with Home → Main → pick song → Gameplay (all fresh).
- [ ] **Step 4:** EditMode tests: 88/88.
- [ ] **Step 5: Commit:**
```bash
git add Assets/Scripts/Gameplay/
git commit -m "feat(gameplay): GameplayController.ResetAndStart for in-scene Retry

ScreenManager.OnReplaced(Gameplay) kicks ResetAndStart, which destroys
live notes, resets spawner/hold/judgment state, reloads chart, and
restarts the silent song. No SceneManager.LoadScene. Retry from
Results now stays in-scene."
```

---

## Task 19: Back button mapping + Main double-back-to-quit

**Files:** Already scoped into `ScreenManager.HandleBack` (Task 7). This task is a verification + any missing pieces for Calibration ignore + Pause overlay equivalence.

Verify:
- Settings overlay active → Back closes it. (HandleBack handles: if settingsOverlay.IsVisible → Finish → return.)
- Calibration overlay active → Back does nothing. (HandleBack: if calibrationOverlay.IsVisible → return early.)
- Pause overlay active → Back closes it + Resume. The current HandleBack hides pauseOverlay via `.Finish()`. But Resume also needs to be triggered. `PauseScreen.OnShown` calls `audioSync.Pause()`. Symmetry: override `OnFinishing` to call `audioSync.Resume()`:

```csharp
// In PauseScreen
protected override void OnFinishing()
{
    if (audioSync != null) audioSync.Resume();
}
```

Then HandleBack's `pauseOverlay.Finish()` does the right thing.

- Main + no overlay → first Back: record time. Second Back within 2s: `Application.Quit()`.

- [ ] **Step 1:** Add `OnFinishing` override to `PauseScreen`.
- [ ] **Step 2:** Remove the explicit `audioSync.Resume()` from `PauseScreen.OnResume` / `OnQuit` (now handled via `OnFinishing`).
- [ ] **Step 3:** Editor test all Back scenarios:
  - Main (no overlay), press ESC once → no visible effect. Press ESC again within 2s → Editor play stops (`Application.Quit` is a no-op in Editor, so observe log `Application.Quit called`).
  - Main + Settings open, press ESC → Settings closes.
  - Gameplay, press ESC → Pause opens.
  - Gameplay + Pause open, press ESC → Pause closes, game resumes.
  - Gameplay + Calibration open (shouldn't happen in normal flow but test by manually showing), press ESC → nothing.
  - Results, press ESC → Main shown.
- [ ] **Step 4:** EditMode tests: 88/88.
- [ ] **Step 5: Commit:**
```bash
git add Assets/Scripts/UI/PauseScreen.cs
git commit -m "feat(input): wire pause Resume via OnFinishing for symmetric Back

PauseScreen.OnShown pauses; OnFinishing resumes. Now whether the
user taps Resume, ⏸ again (future), or Android Back, the same path
calls audioSync.Resume. ScreenManager.HandleBack + double-back-to-quit
on Main verified in Editor."
```

---

## Task 20: Device validation + W4 completion report

**Files:**
- Create: `docs/superpowers/reports/YYYY-MM-DD-w4-completion.md`

- [ ] **Step 1:** Build APK foreground:
```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -nographics -quit \
  -projectPath "C:/dev/unity-music" \
  -executeMethod KeyFlow.Editor.ApkBuilder.Build \
  -logFile -
```
Verify: `Builds/KeyFlow.apk` produced, size printed, <40 MB.

- [ ] **Step 2:** Install to Galaxy S22:
```bash
adb install -r Builds/KeyFlow.apk
adb shell am start -n com.funqdev.keyflow/com.unity3d.player.UnityPlayerActivity
```

- [ ] **Step 3:** Run full checklist from spec §10 (hand-executed):

1. First-launch path: Main → Easy card → Calibration → Gameplay → Für Elise completion → Results (star anim + count-up) → Retry → Gameplay → Home → Main.
2. Settings path: Main → ⚙ → Settings → SFX slider (hear SFX volume change) → NoteSpeed slider (value readout updates) → "오디오 다시 맞추기" → Calibration re-run → Settings reopens → ✕ → Main.
3. Pause path: Gameplay → ⏸ → notes frozen, audio paused → 계속하기 → resumes. Repeat with Android Back key (use adb `input keyevent KEYCODE_BACK`).
4. Exit path: Main → Back once (no-op) → Back again within 2s → app exits.
5. Record persistence: complete a song with 2 stars → Main card shows ★★☆. Retry with 3 stars → Results shows "최고 기록!" + Main card shows ★★★.
6. Migration: uninstall, install W3 build, calibrate (saves `CalibOffsetMs`), uninstall W3 without `adb uninstall` that clears data (use `adb install -r` upgrade path). Install W4 APK on top. Verify calibration NOT re-requested (UserPrefs.MigrateLegacy copied the key).
7. APK size check.
8. 60 FPS check via LatencyMeter HUD.

- [ ] **Step 4:** If any device issue found — fix in a targeted device-fix commit (pattern from W3: one commit per issue, message prefix `fix(scene)` / `fix(ui)` etc.). Re-test.

- [ ] **Step 5:** Write `docs/superpowers/reports/YYYY-MM-DD-w4-completion.md`:

```markdown
# W4 Completion Report

**Date:** YYYY-MM-DD
**Branch:** main (HEAD: <sha>)
**APK:** Builds/KeyFlow.apk (size MB)

## Scope delivered
- Main / Settings / Pause / Results screens live
- 5 carry-overs absorbed (#4/#5/#6/#7 plus namespace polish)
- UserPrefs migration from CalibOffsetMs

## Test counts
- EditMode: 87 (was 68 at W3 end)
  - +6 UserPrefs, +2 OverlayBase, +4 ScreenManager, +4 SongCatalog, +3 AudioSyncPause

## Device validation (Galaxy S22, Android 16)
- [ ] Checklist item 1 ... [ ] 8

## Device fixes applied during validation
- (list commits if any)

## Outstanding issues
- (any not blocking but noted for W5/W6)

## Performance snapshot
- FPS:
- APK size:
- Latency:

## Carry-over items still deferred
- W3 carry-over #1 (mid-game tap drops profiler) → W6
- W3 carry-over #2 (dedicated Calibration click sample) → W6
- W3 carry-over #3 (ChartLoader coroutine) → W5
- W3 carry-over #8-#11 → unchanged

## Next steps → W5
Python MIDI → .kfchart pipeline, 4 more songs, Für Elise Normal difficulty.
```

- [ ] **Step 6: Commit:**
```bash
git add docs/superpowers/reports/*-w4-completion.md
git commit -m "docs(w4): completion report

Main/Settings/Pause/Results screens + carry-overs shipped. 87/87
EditMode tests. Galaxy S22 device checklist passed. Ready for W5."
```

---

## Plan Review Loop Context

**Reviewer hand-off:**
- Plan path: `docs/superpowers/plans/2026-04-21-keyflow-w4-screens.md`
- Spec path: `docs/superpowers/specs/2026-04-21-keyflow-w4-screens-design.md`
- Existing plan style reference: `docs/superpowers/plans/2026-04-20-keyflow-w3-chart-hold-calibration.md`

---

## Execution Handoff

**Recommended:** Subagent-driven via `KeyFlow-W4` team — mirrors W3's successful pattern (persistent SpecReviewer + CodeReviewer, ephemeral ImplementerT<N> per task, final full-scope review). See memory note `feedback_team_agent_pattern.md` for the exact setup.
