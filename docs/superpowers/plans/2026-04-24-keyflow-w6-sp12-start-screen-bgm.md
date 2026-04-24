# KeyFlow W6 SP12 — StartScreen BGM Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a user-authored looping piano BGM (`piano_play_start.mp3`) to the StartScreen (profile-selection screen). Music starts on StartScreen appear, loops infinitely, stops immediately on profile tap, no Settings UI.

**Architecture:** One `AudioSource` component is added as a child GameObject of `StartCanvas` via SceneBuilder. `StartScreen.cs` gains `OnEnable`/`OnDisable` hooks that call `Play()`/`Stop()` on a SerializeField-injected `AudioSource`. Unity's built-in `SetActive` lifecycle propagates `startRoot.SetActive(false)` → `StartScreen.OnDisable` → `bgmSource.Stop()`, giving us stop-for-free without explicit coupling to `ScreenManager.Replace`. Null-tolerance in `OnEnable`/`OnDisable` keeps minimal-wiring test environments working.

**Tech Stack:** Unity 6000.3.13f1 Android (IL2CPP, arm64-v8a), AudioSource + AudioClip Vorbis compression, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-04-24-keyflow-w6-sp12-start-screen-bgm-design.md`

---

## File structure

**Created:**
- `Assets/Audio/bgm/piano_play_start.mp3` — source MP3 relocated from `C:/dev/unity-music/sound/Piano-Play-Start.mp3`
- `Assets/Audio/bgm/piano_play_start.mp3.meta` — Unity-generated meta with Vorbis/CompressedInMemory/Quality=0.70/Stereo import settings (committed)
- `Assets/Tests/EditMode/StartScreenBgmTests.cs` — 2 null-tolerance tests
- `docs/superpowers/reports/2026-04-24-w6-sp12-start-screen-bgm-completion.md` — completion report (Task 7)

**Modified:**
- `Assets/Scripts/UI/StartScreen.cs` — add `bgmSource` SerializeField + `OnEnable`/`OnDisable`
- `Assets/Editor/SceneBuilder.cs` — extend `BuildStartCanvas` signature, construct `BgmAudioSource` child, wire SerializeField
- `Assets/Editor/ApkBuilder.cs` — bump release + profile APK filenames to `keyflow-w6-sp12*.apk`
- `Assets/Scenes/GameplayScene.unity` — regenerated via `KeyFlow/Build W4 Scene` after SceneBuilder change

**NOT modified (guardrails):**
- `Assets/Scripts/UI/ScreenManager.cs` — Replace/HandleBack/lifecycle unchanged
- `Assets/Scripts/UI/BackgroundSwitcher.cs` — unrelated
- `Assets/Scripts/Gameplay/*` — gameplay-scene audio pipeline untouched
- `Assets/Scripts/Feedback/*` — feedback pipeline untouched
- `Assets/Editor/PianoSampleImportPostprocessor.cs` — piano folder unchanged (we do NOT add a `BgmImportPostprocessor`; single-file asset doesn't justify it — .meta commit captures settings)
- Existing 179 EditMode tests (pre-SP11) or ~191 (post-SP11) — purely additive, no signature changes to shared helpers

---

## Execution prerequisite

This plan runs on the existing worktree: `C:\dev\unity-music\.claude\worktrees\trusting-hopper-29b964` (branch `claude/trusting-hopper-29b964`). All commits land on that branch. The spec commit (`b31961d`) is already present; start from there.

**Unity Editor must be CLOSED on this project before any `Unity.exe -batchmode` step.** SP3 discovery: IL2CPP link fails at step 1101/1110 if an interactive Editor has the project open concurrently. Re-open the Editor after the batch command finishes if needed for Inspector work.

**Unity path:** `"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe"` (absolute).

**Merge-order note:** SP11 may or may not be merged first. Task 5 explicitly verifies the test count against both baselines. No file this plan touches overlaps with SP11's `GameplayController` / `Assets/Scripts/UI/CountdownOverlay*` additions; only `SceneBuilder.cs` is shared, and SP11's `BuildCountdownOverlay` method is a new method (no edit conflict with `BuildStartCanvas` extension).

---

## Task 1: Import the BGM MP3 into `Assets/Audio/bgm/`

This task brings the user's scratch-folder MP3 into the Unity project, sets import settings via the Inspector, and commits both the audio file and its `.meta`.

**Files:**
- Create: `Assets/Audio/bgm/piano_play_start.mp3`
- Create: `Assets/Audio/bgm/piano_play_start.mp3.meta` (Unity-generated)

- [ ] **Step 1: Create the target folder and copy the MP3**

From the worktree root, run:
```bash
mkdir -p Assets/Audio/bgm
cp "C:/dev/unity-music/sound/Piano-Play-Start.mp3" Assets/Audio/bgm/piano_play_start.mp3
ls -la Assets/Audio/bgm/
```

Expected: `piano_play_start.mp3` appears, ~577 KB. (The user may keep the `sound/Piano-Play-Start.mp3` scratch as a working copy — do NOT delete it in this task; the spec §10 "Deleted" entry is optional and user-authored.)

- [ ] **Step 2: Open Unity Editor and let it auto-import the new asset**

With the Unity Hub, open the `unity-music` project using the worktree path. Unity will auto-import `piano_play_start.mp3` and generate `piano_play_start.mp3.meta` with default AudioImporter settings.

- [ ] **Step 3: Configure AudioImporter settings via Inspector**

In the Project window, select `Assets/Audio/bgm/piano_play_start.mp3`. In the Inspector, set:

| Field | Value |
|---|---|
| Force To Mono | unchecked (false) |
| Normalize | unchecked (false) |
| Load In Background | unchecked (false) |
| Ambisonic | unchecked (false) |
| Preload Audio Data | checked (true) |
| **Default — Load Type** | `Compressed In Memory` |
| **Default — Compression Format** | `Vorbis` |
| **Default — Quality** | `70` (slider) |
| **Default — Sample Rate Setting** | `Preserve Sample Rate` |

Click **Apply** at the bottom of the Inspector. Unity re-imports the clip with the new settings and writes them into the `.meta` file.

- [ ] **Step 4: Verify the .meta file captured the settings**

Close Unity Editor. From the worktree:
```bash
cat Assets/Audio/bgm/piano_play_start.mp3.meta | grep -E "loadType|compressionFormat|quality|forceToMono|preloadAudioData"
```

Expected lines (values):
- `loadType: 1` (CompressedInMemory)
- `compressionFormat: 1` (Vorbis)
- `quality: 0.7` (approximately — Unity serializes as float)
- `forceToMono: 0` (false)
- `preloadAudioData: 1` (true)

If any value disagrees, reopen Unity, fix in Inspector, click Apply, close Editor, and re-check.

- [ ] **Step 5: Commit**

```bash
git add Assets/Audio/bgm/piano_play_start.mp3 Assets/Audio/bgm/piano_play_start.mp3.meta
git commit -m "feat(w6-sp12): import piano_play_start.mp3 with Vorbis/CompressedInMemory/Q0.70"
```

---

## Task 2: `StartScreen.cs` — BGM hooks (TDD)

Adds the field and lifecycle hooks to `StartScreen.cs`, driven by two null-tolerance tests that must first fail (RED) without the null check, then pass (GREEN) once added.

**Files:**
- Modify: `Assets/Scripts/UI/StartScreen.cs`
- Create: `Assets/Tests/EditMode/StartScreenBgmTests.cs`

- [ ] **Step 1: Add the SerializeField and unsafe `OnEnable`/`OnDisable` (deliberately missing null-check)**

Edit `Assets/Scripts/UI/StartScreen.cs`. The current file is 28 lines; after this step it should read:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    public class StartScreen : MonoBehaviour
    {
        [SerializeField] private Button nayoonButton;
        [SerializeField] private Button soyoonButton;
        [SerializeField] private AudioSource bgmSource;

        private void Awake()
        {
            if (nayoonButton != null) nayoonButton.onClick.AddListener(() => Select(Profile.Nayoon));
            if (soyoonButton != null) soyoonButton.onClick.AddListener(() => Select(Profile.Soyoon));
        }

        private void OnEnable()
        {
            bgmSource.Play();
        }

        private void OnDisable()
        {
            bgmSource.Stop();
        }

        private void Select(Profile p)
        {
            SessionProfile.Current = p;
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.Replace(AppScreen.Main);
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal void InvokeSelectForTest(Profile p) => Select(p);
#endif
    }
}
```

Note: `bgmSource.Play()` will NRE if `bgmSource` is null. Step 2 writes the tests that prove this; Step 4 adds the null-check that fixes it.

- [ ] **Step 2: Write the failing tests**

Create `Assets/Tests/EditMode/StartScreenBgmTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using KeyFlow.UI;

namespace KeyFlow.Tests.EditMode
{
    public class StartScreenBgmTests
    {
        [Test]
        public void OnEnable_WithNullBgmSource_DoesNotThrow()
        {
            var go = new GameObject("StartScreenTest");
            go.SetActive(false);
            var s = go.AddComponent<StartScreen>();
            // bgmSource intentionally unassigned.

            Assert.DoesNotThrow(() => go.SetActive(true));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void OnDisable_WithNullBgmSource_DoesNotThrow()
        {
            var go = new GameObject("StartScreenTest");
            go.SetActive(false);
            var s = go.AddComponent<StartScreen>();
            // bgmSource intentionally unassigned.
            go.SetActive(true);

            Assert.DoesNotThrow(() => go.SetActive(false));

            Object.DestroyImmediate(go);
        }
    }
}
```

Rationale for the `SetActive(false)`-before-`AddComponent` pattern: adding a component to an already-active GO fires its `OnEnable` immediately (Unity quirk). We activate explicitly with `SetActive(true)` inside the `Assert.DoesNotThrow` body so the throw site is captured by the assertion.

- [ ] **Step 3: Run EditMode tests — expect both new tests to FAIL with NRE**

Ensure Unity Editor is CLOSED on this project. Run:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

(Do NOT pass `-quit` with `-runTests` — the test runner skips if `-quit` is also passed. See project memory.)

Expected: `Builds/test-results.xml` reports 2 new failures in `StartScreenBgmTests`. Both failures should stack-trace through `NullReferenceException` at `StartScreen.OnEnable` / `StartScreen.OnDisable`.

Verify:
```bash
grep -E "StartScreenBgmTests|NullReference" Builds/test-results.xml | head -20
```

If the 2 tests passed instead of failed, the null-check is already present — re-examine Step 1 and remove the guard.

- [ ] **Step 4: Add the null-check to make the tests pass**

Edit `Assets/Scripts/UI/StartScreen.cs` — change the `OnEnable`/`OnDisable` bodies to:

```csharp
        private void OnEnable()
        {
            if (bgmSource != null) bgmSource.Play();
        }

        private void OnDisable()
        {
            if (bgmSource != null) bgmSource.Stop();
        }
```

- [ ] **Step 5: Run EditMode tests — expect GREEN**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

Expected: all existing tests pass + 2 new `StartScreenBgmTests` pass. Total = (baseline) + 2.

Verify:
```bash
grep -E "passed=|failed=" Builds/test-results.xml | head -5
```

`failed="0"` should appear.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/StartScreen.cs Assets/Tests/EditMode/StartScreenBgmTests.cs
git commit -m "feat(w6-sp12): StartScreen.OnEnable/OnDisable drive BGM via injected AudioSource"
```

---

## Task 3: `SceneBuilder.BuildStartCanvas` — add `BgmAudioSource` child

Extends the private `BuildStartCanvas` method signature to accept the BGM clip, creates a child GameObject carrying a configured `AudioSource`, and wires it into `StartScreen.bgmSource` via the existing `SetField` helper.

**Files:**
- Modify: `Assets/Editor/SceneBuilder.cs` (two edits: method signature + body, and the single call site)

- [ ] **Step 1: Read the call site (line ~125) and the method body (line ~663) to confirm current state**

```bash
grep -n "BuildStartCanvas" Assets/Editor/SceneBuilder.cs
```

Expected: 2 hits — one call at line 125 (`var startCanvas = BuildStartCanvas(startBg, out var startScreen);`), one definition at line 663.

- [ ] **Step 2: Modify the method signature and body**

In `Assets/Editor/SceneBuilder.cs`, replace the `BuildStartCanvas` method (starts at line 663 `private static GameObject BuildStartCanvas(...)`) with:

```csharp
        private static GameObject BuildStartCanvas(Sprite startBg, AudioClip bgmClip, out StartScreen startScreen)
        {
            var canvasGO = new GameObject("StartCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 7;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Full-screen backdrop painting the start.png art.
            var bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.sprite = startBg;
            bgImg.preserveAspect = false;
            bgImg.raycastTarget = false;

            var nayoonBtn = BuildInvisibleButton(canvasGO.transform, "NayoonButton", NAYOON_ANCHOR, BUTTON_SIZE);
            var soyoonBtn = BuildInvisibleButton(canvasGO.transform, "SoyoonButton", SOYOON_ANCHOR, BUTTON_SIZE);

            // BGM AudioSource child (SP12)
            var bgmGO = new GameObject("BgmAudioSource");
            bgmGO.transform.SetParent(canvasGO.transform, false);
            var bgmSource = bgmGO.AddComponent<AudioSource>();
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = 0.6f;
            bgmSource.spatialBlend = 0f;
            if (bgmClip == null)
                Debug.LogWarning("[SceneBuilder] BGM clip is null — StartScreen will have no music. " +
                                 "Verify Assets/Audio/bgm/piano_play_start.mp3 exists and is imported as an AudioClip.");

            startScreen = canvasGO.AddComponent<StartScreen>();
            SetField(startScreen, "nayoonButton", nayoonBtn);
            SetField(startScreen, "soyoonButton", soyoonBtn);
            SetField(startScreen, "bgmSource", bgmSource);

            return canvasGO;
        }
```

The 4 new code regions are:
1. The extra `AudioClip bgmClip` parameter.
2. The `// BGM AudioSource child (SP12)` block constructing `bgmGO` + `AudioSource`.
3. The `Debug.LogWarning` on null clip (fail-soft: scene still builds, user gets a diagnostic instead of a mysterious silent APK).
4. The extra `SetField(startScreen, "bgmSource", bgmSource);` wiring line.

- [ ] **Step 3: Update the call site (line ~125) to load the clip and pass it**

Find the line:
```csharp
            var startCanvas = BuildStartCanvas(startBg, out var startScreen);
```

Replace it with a 2-line version (the preceding line that loads the sprite is nearby — do NOT remove it):
```csharp
            var bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bgm/piano_play_start.mp3");
            var startCanvas = BuildStartCanvas(startBg, bgmClip, out var startScreen);
```

If the file does not already have `using UnityEditor;` (it does — it's an Editor script), no using addition is needed.

- [ ] **Step 4: Verify the change compiles by running EditMode tests**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

Expected: all tests pass. A compile error here most likely means a typo in Step 2 (missing semicolon, wrong field name in SetField). Check `Builds/test-log.txt` for Unity's compilation errors.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/SceneBuilder.cs
git commit -m "feat(w6-sp12): SceneBuilder constructs BgmAudioSource child on StartCanvas"
```

---

## Task 4: Regenerate `GameplayScene.unity`

Runs the `KeyFlow/Build W4 Scene` menu item in batch mode so the scene file reflects the `SceneBuilder` changes. Commits the regenerated scene separately from the SceneBuilder change for a clean audit trail.

**Files:**
- Modify: `Assets/Scenes/GameplayScene.unity` (regenerated)

- [ ] **Step 1: Ensure Unity Editor is closed**

If Unity Editor is open on this project, close it. (Verify: no `Unity.exe` process holding `C:\dev\unity-music\Temp\UnityLockfile`.)

- [ ] **Step 2: Confirm the method name for `KeyFlow/Build W4 Scene`**

The method is `KeyFlow.Editor.SceneBuilder.Build` (under `[MenuItem("KeyFlow/Build W4 Scene")]` around line 44 of `SceneBuilder.cs`). Verify it still exists with that exact name:

```bash
grep -n "MenuItem" Assets/Editor/SceneBuilder.cs | head -5
```

If the method was renamed by a prior SP, adjust Step 3's command accordingly.

- [ ] **Step 3: Run the scene rebuild via `-executeMethod`**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath . -executeMethod KeyFlow.Editor.SceneBuilder.Build -logFile Builds/scene-build.log
```

(For `-executeMethod`, `-quit` IS correct — this is a different invocation than `-runTests`.)

Expected: exit code 0, `Builds/scene-build.log` contains no error-level lines. The scene file changes on disk.

- [ ] **Step 4: Inspect the scene diff**

```bash
git diff --stat Assets/Scenes/GameplayScene.unity
git diff Assets/Scenes/GameplayScene.unity | head -80
```

Expected: the diff is small and mechanical — a new `BgmAudioSource` GameObject child under `StartCanvas`, an `AudioSource` component with `clip`, `loop=1`, `playOnAwake=0`, `volume=0.6`, `spatialBlend=0`, and a new `bgmSource` fileID pointing at that AudioSource on the `StartScreen` component.

If the diff touches hundreds of lines or non-StartCanvas objects, stop and investigate — something in SceneBuilder touched state it shouldn't.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scenes/GameplayScene.unity
git commit -m "chore(w6-sp12): regenerate GameplayScene with BgmAudioSource child"
```

---

## Task 5: Full EditMode test sweep

Sanity check that SP12 changes did not regress any existing test.

**Files:** none modified in this task (verification only).

- [ ] **Step 1: Determine the expected test count baseline**

Check the most recent completion report's test count:
```bash
ls -t docs/superpowers/reports/*.md | head -3
grep -E "EditMode tests|test count|\\d+/\\d+ green" $(ls -t docs/superpowers/reports/*.md | head -1)
```

Baseline expectation:
- If pre-SP11: 179 existing + 2 new = **181 total**.
- If post-SP11 (SP11 added ~12 tests): ~191 existing + 2 new = **~193 total**.

Pick the baseline that matches current `git log` state.

- [ ] **Step 2: Run all EditMode tests**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

- [ ] **Step 3: Verify pass count**

```bash
grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' Builds/test-results.xml | head -1
```

Expected: `passed="<baseline+2>" failed="0"`. If `failed > 0`, open `Builds/test-results.xml` and locate the failing test's `<failure>` element; address before proceeding.

- [ ] **Step 4: Run pytest to confirm pipeline untouched**

```bash
cd tools/midi_to_kfchart && python -m pytest -q && cd ../..
```

Expected: `49 passed` (or whatever the current pytest baseline is — SP11 does not change this count).

- [ ] **Step 5: No commit**

No code changes in this task.

---

## Task 6: APK filename bump + Android build + device playtest

Bump the release and profile APK output names to `keyflow-w6-sp12*.apk`, build, and run the on-device playtest checklist from spec §6.3.

**Files:**
- Modify: `Assets/Editor/ApkBuilder.cs`

- [ ] **Step 1: Bump APK filenames**

Edit `Assets/Editor/ApkBuilder.cs`:
- Line 14: change `"keyflow-w6-sp10.apk"` → `"keyflow-w6-sp12.apk"`
- Line 35: change `"keyflow-w6-sp10-profile.apk"` → `"keyflow-w6-sp12-profile.apk"`

(If SP11 bumped these to sp11 first, instead bump sp11 → sp12.)

- [ ] **Step 2: Build the release APK via batch mode**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath . -executeMethod KeyFlow.Editor.ApkBuilder.Build -logFile Builds/apk-build.log
```

Expected: exit code 0, `Builds/keyflow-w6-sp12.apk` exists, last line of `Builds/apk-build.log` reports size in MB.

Record the reported size:
```bash
tail -5 Builds/apk-build.log
ls -la Builds/keyflow-w6-sp12.apk
```

Note the byte count and MB-rounded value. The spec §1 guardrail is **≤38.60 MB** (superseding SP11's 38.10 MB).

- [ ] **Step 3: Verify APK size against guardrail**

If APK > 38.60 MB:
- Stop. Do not install on device.
- Reduce Vorbis quality: reopen Unity, select `piano_play_start.mp3`, lower Quality to 50, Apply, close Unity, commit the `.meta` change, and rerun Step 2.
- If still > 38.60 MB after Quality=50, escalate to the user (the MP3 may need re-encoding upstream).

- [ ] **Step 4: Install on Galaxy S22 (R5CT21A31QB) via adb**

```bash
adb devices
adb install -r Builds/keyflow-w6-sp12.apk
```

Expected: `devices` shows `R5CT21A31QB device` (not `unauthorized`); `install -r` finishes with `Success`.

- [ ] **Step 5: Run spec §6.3 playtest checklist**

Working from a copy of spec §6.3, tick off each item in sequence. Record any failures with a short note. The order is chosen so earlier items expose issues before more expensive ones:

1. APK cold-start → BGM starts with no perceptible silent gap on StartScreen.
2. Leave StartScreen idle 45+ seconds → verify loop continuity (no silence, no audible seam at the ~30 s mark).
3. Tap 나윤 → Main appears; audio cuts immediately.
4. Back out to Start → tap 소윤 → Main appears; audio cuts immediately.
5. Back out to Start → BGM restarts from 0:00.
6. Rapidly toggle Start↔Main 3× → no stuck audio, no overlapping plays.
7. Enter Gameplay → play a song → verify no BGM bleed (piano samples + SP4/SP10/SP11 feedback audio only).
8. Finish song → Results → Back → Main → Back → Start → BGM starts fresh.
9. Incoming call or notification during BGM → Unity auto-pauses; resume after → BGM continues.
10. Home button → backgrounding pauses; foregrounding resumes.
11. With the Profile build (Step 7 below), verify GC.Collect count = 0 during 1-minute StartScreen idle.

- [ ] **Step 6: Build the profile APK and capture GC stats**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath . -executeMethod KeyFlow.Editor.ApkBuilder.BuildProfile -logFile Builds/apk-profile-build.log
adb install -r Builds/keyflow-w6-sp12-profile.apk
```

Attach Unity Profiler over adb. Let StartScreen run 60 seconds. Look at the GC.Alloc column in the Profiler's CPU → Hierarchy view. Target: 0 allocations from BGM loop playback frames. Record result.

- [ ] **Step 7: Commit APK filename bump**

```bash
git add Assets/Editor/ApkBuilder.cs
git commit -m "chore(w6-sp12): bump APK output names to sp12"
```

If any playtest item failed, do NOT proceed to Task 7 — create a follow-up fix (likely in Task 2/3/4) and re-run Task 5 + Task 6.

---

## Task 7: Completion report and final push

Writes the completion report and commits it. If a merge is desired, this is where it happens.

**Files:**
- Create: `docs/superpowers/reports/2026-04-24-w6-sp12-start-screen-bgm-completion.md`

- [ ] **Step 1: Draft the completion report**

Create `docs/superpowers/reports/2026-04-24-w6-sp12-start-screen-bgm-completion.md` with sections:
- Summary (1–2 sentences)
- Delivered (bullet list of files + LoC)
- Test results (EditMode pass count, pytest pass count)
- Device playtest (S22, items passed/failed)
- APK size (actual, vs. guardrail)
- GC verification (0 allocs on StartScreen idle)
- Deviations from spec (if any)
- Follow-ups / carry-overs (if any)

Cross-reference commits by SHA.

- [ ] **Step 2: Commit the report**

```bash
git add docs/superpowers/reports/2026-04-24-w6-sp12-start-screen-bgm-completion.md
git commit -m "docs(w6-sp12): completion report"
```

- [ ] **Step 3: Push and propose merge (optional — follow project cadence)**

```bash
git push -u origin claude/trusting-hopper-29b964
```

If the project pattern is squash-merge via PR (see recent `merge: W6 SP<N>` commits on `main`), open a PR via `gh pr create`. Otherwise hand off to the user for their preferred merge flow.

---

## Rollback

If SP12 causes a post-merge regression:
1. Revert the merge commit.
2. Re-run `KeyFlow/Build W4 Scene` on the reverted state to regenerate `GameplayScene.unity` (the `BgmAudioSource` child disappears naturally).
3. The `piano_play_start.mp3` + `.meta` stay in the repo but are unreferenced → Unity build excludes unreferenced audio → no APK size impact.

---

## Risks during implementation

| When | Risk | Mitigation |
|---|---|---|
| Task 1 Step 3 | User sets wrong Import settings in Inspector | Step 4 cross-checks the `.meta` values via grep — catches drift before commit. |
| Task 2 Step 3 | Null-tolerance tests pass on the first run (never see RED) | Re-examine Step 1 — the unsafe bodies might have been written with a null check by mistake. The whole point is to prove the null-check is load-bearing. |
| Task 3 Step 3 | Call site line number drifts (because a prior SP added lines) | Use `grep -n "BuildStartCanvas(startBg"` to find the actual call site, not line 125 literally. |
| Task 4 Step 3 | Unity Editor was accidentally left open | Batch mode errors with lockfile. Check `Temp/UnityLockfile`, kill dangling `Unity.exe` if any. |
| Task 5 Step 3 | Test count mismatch (higher than expected) | Means another SP merged during implementation — not a bug; reconcile the baseline and proceed. |
| Task 6 Step 3 | APK exceeds 38.60 MB guardrail | Plan's Step 3 has explicit tune-Vorbis-Quality-to-50 fallback. |
| Task 6 Step 5 | Playtest finding "audio bleed into Gameplay" | Indicates OnDisable didn't fire (unexpected — SetActive(false) should call it). Debug: add `Debug.Log` to OnDisable and reproduce; most likely root cause is SceneBuilder wiring `bgmSource` onto the wrong component. |

---

## Out-of-scope for this plan (covered in spec §2.2)

- SongSelect / Results / Gameplay BGM
- Fade in/out
- Settings UI (mute / volume slider)
- `MusicManager` singleton
- `IBgmPlayer` interface + adapter
- Android `OnApplicationFocus` handling
- Preloading optimization
- Replacing the MP3 content
- Adding a `BgmImportPostprocessor.cs` — single-file asset, manual Inspector + committed `.meta` is sufficient
