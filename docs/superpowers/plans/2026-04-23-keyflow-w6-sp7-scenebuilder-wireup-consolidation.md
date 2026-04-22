# KeyFlow W6 SP7 — SceneBuilder ↔ Wireup Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fold every responsibility of `Assets/Editor/W6SamplesWireup.cs` into `Assets/Editor/SceneBuilder.cs` and delete W6SamplesWireup, so a single `KeyFlow/Build W4 Scene` menu click produces a fully-wired scene (no more manual two-step workflow that has caused regressions in SP4 and SP6).

**Architecture:** Pure Editor-tooling refactor. Runtime code, runtime assets, and EditMode test suite are all untouched. The 5 post-build wirings move into `SceneBuilder`'s existing methods (`BuildManagers` for audio/tap-input wiring, `BuildSettingsCanvas` for credits Text construction) using the existing `SetField` / `SetArrayField` helpers. One new helper (`LoadPitchSamples`) loads the 17 Salamander sample clips with a null-guard abort matching the existing `piano_c4` / `calibration_click` / `background_gameplay` pattern. `W6SamplesWireup.cs` + `.meta` deleted from git.

**Tech Stack:** Unity 6000.3.13f1 Editor API (`AssetDatabase`, `SerializedObject`), existing SceneBuilder helpers (`SetField`, `SetArrayField`), NUnit EditMode tests (regression gate only — no new tests).

**Spec:** `docs/superpowers/specs/2026-04-23-keyflow-w6-sp7-scenebuilder-wireup-consolidation-design.md`

---

## File structure

**Modified:**
- `Assets/Editor/SceneBuilder.cs` (~+55 lines, −0):
  - Add `PianoFolder` const + `SampleNames[17]` const array (copy from Wireup)
  - Add `LoadPitchSamples()` private static helper
  - Add `SetField(Object target, string name, int value)` overload (~8 lines) — needed for `baseMidi` / `stepSemitones` int wiring
  - In `Build()`: add `LoadPitchSamples()` call + null-guard + pass `pitchSamples` through to `BuildManagers`
  - In `BuildManagers`: add `AudioClip[] pitchSamples` parameter; wire `pitchSamples` / `baseMidi` / `stepSemitones` on `AudioSamplePool` right after `SetField(samplePool, "defaultClip", pianoClip)`; add `SetField(tapInput, "judgmentSystem", judgmentSystem)` back-reference after `judgmentSystem` is constructed
  - In `BuildSettingsCanvas`: add CreditsLabel GameObject + RectTransform + Text construction + `SetField(screen, "creditsLabel", creditsText)` right before `return screen;`
- `Assets/Scenes/GameplayScene.unity` — regenerated; expected diff: none or minimal (final state equals current "SceneBuilder + Wireup" output)

**Deleted:**
- `Assets/Editor/W6SamplesWireup.cs` (111 lines)
- `Assets/Editor/W6SamplesWireup.cs.meta`

**Created (docs):**
- `docs/superpowers/reports/2026-04-23-w6-sp7-scenebuilder-wireup-consolidation-completion.md` — Task 8 final report

**NOT modified (guardrails):**
- Any `Assets/Scripts/*` runtime code
- Any `Assets/Tests/EditMode/*` test
- `Assets/Audio/*` (all sound assets from SP1 + SP5)
- `Assets/Sprites/*` (background_gameplay.png + white sprite + stars)
- `Assets/Prefabs/*` (Note prefab and SP4 feedback prefabs)
- Other Editor tools: `CalibrationClickBuilder`, `FeedbackPrefabBuilder`, `ApkBuilder`, `PianoSampleImportPostprocessor`, `BackgroundImporterPostprocessor`, `AddVibratePermission`

---

## Engineer context (zero-assumption brief)

- **Why this refactor:** SP4 report §6.3 item #1 and SP6 Task 10 both show device-playtest regressions caused by forgetting to re-run `W6SamplesWireup.Wire` after `SceneBuilder.Build`. Consolidating removes the manual step.
- **Baseline test count:** 135 (SP6 merged state). Target post-SP7: **135** (pure refactor, no new tests).
- **Project quirks (CRITICAL, from memory):**
  - `-runTests` + `-quit` = test runner skipped. Never combine these flags.
  - Close interactive Unity Editor before every `Unity.exe -batchmode` — concurrent IL2CPP session fails at link step 1101/1110.
  - `Unity.exe -batchmode` must run foreground (no `run_in_background`, no pipes).
- **Existing helpers to reuse (avoid duplication):**
  - `SetField(Object target, string name, Object value)` — single object reference field (line 1395)
  - `SetField(Object target, string name, float value)` — single float field (line 1404)
  - `SetArrayField(Object target, string name, Object[] values)` — object array field (line 1413)
  - **NEW: `SetField(Object, string, int)` overload needed for this SP** — `baseMidi` / `stepSemitones` wiring
- **Wireup's current patterns (to preserve behavior):**
  - Loads 17 samples from `Assets/Audio/piano/{name}.wav` using `AssetDatabase.LoadAssetAtPath<AudioClip>` — reuse verbatim
  - Uses `SerializedObject` batch-write for `pitchSamples` array + `baseMidi` int + `stepSemitones` int — we'll use `SetArrayField` + new int `SetField` overload instead (same effect, already-tested helpers)
  - Wires `TapInputHandler.judgmentSystem` via `SerializedObject` — we'll use existing Object `SetField`
  - Creates `CreditsLabel` GameObject with `RectTransform + Text` + sets font, fontSize=18, alignment, color, text. Parented to `settings.transform` — this is the same as `canvasGO.transform` in SceneBuilder since `SettingsScreen` is `AddComponent`'d on canvasGO itself.

### Test-run command (verbatim)

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

Parse `Builds/test-results.xml` for `<test-run total="N" passed="N" failed="N">`. Baseline: 135. Target: 135 (unchanged).

### Scene rebuild / APK commands (with `-quit`)

```bash
# Scene rebuild (Task 6)
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod KeyFlow.Editor.SceneBuilder.Build -quit -logFile Builds/scene-build.txt

# Release APK build (Task 7)
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod KeyFlow.Editor.ApkBuilder.Build -quit -logFile Builds/apk-build.txt
```

Note: **Do NOT run `W6SamplesWireup.Wire` at any point** — after Task 5, the class no longer exists. Task 6's `SceneBuilder.Build` does all wiring in one pass.

---

## Task 1: Add int SetField overload + pitch sample constants + LoadPitchSamples helper

**Files:**
- Modify: `Assets/Editor/SceneBuilder.cs`

**Why this task exists:** Establish the foundational infrastructure — new helper overload and sample-loading logic — before any wiring migration. This keeps subsequent tasks focused on the actual migration, not on the helpers they need.

- [ ] **Step 1: Add the int SetField overload**

Locate the existing `SetField(Object target, string name, float value)` at `Assets/Editor/SceneBuilder.cs:1404`. Add a new overload directly after it, before `SetArrayField`:

```csharp
private static void SetField(Object target, string name, int value)
{
    var so = new SerializedObject(target);
    var prop = so.FindProperty(name);
    if (prop == null) { Debug.LogError($"[KeyFlow] Field '{name}' not found on {target.GetType().Name}"); return; }
    prop.intValue = value;
    so.ApplyModifiedProperties();
}
```

- [ ] **Step 2: Add pitch sample constants near other top-of-class constants**

Locate the existing const block at top of `SceneBuilder` class (around `LaneAreaWidth`, `JudgmentY`). Add:

```csharp
private const string PianoFolder = "Assets/Audio/piano";
private static readonly string[] SampleNames =
{
    "C2v10", "Ds2v10", "Fs2v10", "A2v10",
    "C3v10", "Ds3v10", "Fs3v10", "A3v10",
    "C4v10", "Ds4v10", "Fs4v10", "A4v10",
    "C5v10", "Ds5v10", "Fs5v10", "A5v10",
    "C6v10",
};
```

- [ ] **Step 3: Add LoadPitchSamples helper**

Place this method anywhere that compiles — adjacent to other private static helpers is cleanest. Suggested near `SetArrayField` at the bottom:

```csharp
private static AudioClip[] LoadPitchSamples()
{
    var clips = new AudioClip[SampleNames.Length];
    for (int i = 0; i < SampleNames.Length; i++)
    {
        string path = $"{PianoFolder}/{SampleNames[i]}.wav";
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
        {
            Debug.LogError($"[KeyFlow] Missing pitch sample: {path}. Aborting.");
            return null;
        }
        clips[i] = clip;
    }
    return clips;
}
```

- [ ] **Step 4: Verify compilation (run tests)**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

Expected: 135/135 pass. The new helpers are unused so far; compilation is the only check here.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/SceneBuilder.cs
git commit -m "$(cat <<'EOF'
feat(w6-sp7): add int SetField overload + LoadPitchSamples helper

Foundation for Task 2-4. No behavior change yet — helpers unused until
subsequent tasks migrate Wireup's responsibilities in.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Load pitch samples in Build() and pass to BuildManagers

**Files:**
- Modify: `Assets/Editor/SceneBuilder.cs`

**Why this task exists:** Plumb the pitch samples through. `BuildManagers` will gain a new `pitchSamples` parameter that Task 3 uses for actual wiring. This task just connects the data flow.

- [ ] **Step 1: Call LoadPitchSamples + null-guard in Build()**

Locate the asset-load block around `Assets/Editor/SceneBuilder.cs:45-62`. After the existing `bgSprite` null-guard (Task ~57-62 area from SP6), add:

```csharp
var pitchSamples = LoadPitchSamples();
if (pitchSamples == null) return;
```

- [ ] **Step 2: Extend `BuildManagers` signature to accept pitchSamples**

Locate the `BuildManagers` method signature around line 163. It currently reads:

```csharp
private static void BuildManagers(
    Camera camera,
    AudioClip pianoClip,
    GameObject notePrefab,
    Transform parent,
    out AudioSyncManager audioSync,
    out AudioSamplePool samplePool,
    out TapInputHandler tapInput,
    out JudgmentSystem judgmentSystem,
    out NoteSpawner spawner,
    out HoldTracker holdTracker)
```

Add `AudioClip[] pitchSamples` as the third parameter (after `pianoClip`):

```csharp
private static void BuildManagers(
    Camera camera,
    AudioClip pianoClip,
    AudioClip[] pitchSamples,
    GameObject notePrefab,
    Transform parent,
    out AudioSyncManager audioSync,
    out AudioSamplePool samplePool,
    out TapInputHandler tapInput,
    out JudgmentSystem judgmentSystem,
    out NoteSpawner spawner,
    out HoldTracker holdTracker)
```

- [ ] **Step 3: Update the `BuildManagers` call site in Build()**

Locate the call in `Build()` (around line 73). Currently:

```csharp
BuildManagers(
    camera, pianoClip, notePrefab, gameplayRoot.transform,
    out var audioSync, out var samplePool, out var tapInput,
    out var judgmentSystem, out var spawner, out var holdTracker);
```

Change to:

```csharp
BuildManagers(
    camera, pianoClip, pitchSamples, notePrefab, gameplayRoot.transform,
    out var audioSync, out var samplePool, out var tapInput,
    out var judgmentSystem, out var spawner, out var holdTracker);
```

- [ ] **Step 4: Verify compilation**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

Expected: 135/135 pass. `pitchSamples` is now plumbed through but not yet wired on `AudioSamplePool`; Wireup still does that part for now.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/SceneBuilder.cs
git commit -m "$(cat <<'EOF'
feat(w6-sp7): plumb pitchSamples[] through Build() → BuildManagers

LoadPitchSamples invoked after background asset guard. BuildManagers
signature gains AudioClip[] pitchSamples parameter; Task 3 will use it
to wire AudioSamplePool fields.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Wire AudioSamplePool pitchSamples / baseMidi / stepSemitones + TapInputHandler.judgmentSystem

**Files:**
- Modify: `Assets/Editor/SceneBuilder.cs`

**Why this task exists:** The core of the consolidation. Four wirings move from Wireup into BuildManagers. Uses the new `SetField(int)` helper and existing `SetArrayField` / `SetField(Object)` helpers.

- [ ] **Step 1: Wire AudioSamplePool pitch fields in BuildManagers**

Locate the line `SetField(samplePool, "defaultClip", pianoClip);` inside `BuildManagers` (around line 195). Add immediately after it:

```csharp
SetArrayField(samplePool, "pitchSamples", pitchSamples);
SetField(samplePool, "baseMidi", 36);
SetField(samplePool, "stepSemitones", 3);
```

- [ ] **Step 2: Wire TapInputHandler.judgmentSystem back-reference**

Also in `BuildManagers`. Locate where `judgmentSystem` is added as a component (around line 207) and where its related wirings happen. Currently there's:

```csharp
SetField(judgmentSystem, "tapInput", tapInput);
```

(or similar, around line 208). Add immediately after that line:

```csharp
SetField(tapInput, "judgmentSystem", judgmentSystem);
```

This closes the bidirectional reference (JudgmentSystem ↔ TapInputHandler) that SP1 multi-pitch audio routing requires. Previously only the forward direction was set in SceneBuilder; the back-reference was in Wireup.

- [ ] **Step 3: Verify compilation — DO NOT run Wireup or rebuild scene yet**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

Expected: 135/135 pass. Code paths are duplicated now (both SceneBuilder and Wireup set pitchSamples etc.) — that's fine temporarily; Task 5 deletes Wireup.

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/SceneBuilder.cs
git commit -m "$(cat <<'EOF'
feat(w6-sp7): fold AudioSamplePool pitch wiring + tapInput.judgmentSystem into BuildManagers

Moves 4 of 5 W6SamplesWireup responsibilities: pitchSamples[17],
baseMidi=36, stepSemitones=3 on AudioSamplePool; judgmentSystem
back-reference on TapInputHandler. Uses existing SetField / SetArrayField
helpers + new int SetField overload. Wireup still does these in parallel
until Task 5 deletes it.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Fold CreditsLabel Text construction into BuildSettingsCanvas

**Files:**
- Modify: `Assets/Editor/SceneBuilder.cs`

**Why this task exists:** Fifth and final Wireup responsibility. CreditsLabel is a settings-screen visual, so its natural home is `BuildSettingsCanvas`.

- [ ] **Step 1: Add CreditsLabel construction before `return screen;`**

Locate the end of `BuildSettingsCanvas` around line 908-909:

```csharp
SetField(screen, "calibration", calibration);
return screen;
```

Insert the following block between `SetField(screen, "calibration", calibration);` and `return screen;`:

```csharp
// CC-BY Salamander credit Text, anchored to bottom of SettingsCanvas
var creditsGo = new GameObject("CreditsLabel", typeof(RectTransform), typeof(Text));
creditsGo.transform.SetParent(canvasGO.transform, false);
var creditsRT = creditsGo.GetComponent<RectTransform>();
creditsRT.anchorMin = new Vector2(0.05f, 0.02f);
creditsRT.anchorMax = new Vector2(0.95f, 0.08f);
creditsRT.offsetMin = Vector2.zero;
creditsRT.offsetMax = Vector2.zero;
var creditsText = creditsGo.GetComponent<Text>();
creditsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
creditsText.fontSize = 18;
creditsText.alignment = TextAnchor.MiddleCenter;
creditsText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
creditsText.text = UIStrings.CreditsSamples;
SetField(screen, "creditsLabel", creditsText);
```

The `using KeyFlow.UI;` for `UIStrings` is already at the top of the file (confirmed earlier — it resolves `HUDPauseButton`, `ScreenManager`, etc.).

Parenting `creditsGo.transform.SetParent(canvasGO.transform, ...)` is equivalent to Wireup's `settings.transform` because `SettingsScreen` is `AddComponent`'d on `canvasGO` (line 900) — they share the same Transform.

- [ ] **Step 2: Verify compilation**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

Expected: 135/135 pass.

- [ ] **Step 3: Commit**

```bash
git add Assets/Editor/SceneBuilder.cs
git commit -m "$(cat <<'EOF'
feat(w6-sp7): fold CreditsLabel construction into BuildSettingsCanvas

Fifth and final W6SamplesWireup responsibility. GameObject + RectTransform
+ Text + Font + SetField all inline where SettingsScreen is constructed.
SceneBuilder now does 100% of post-build wiring. Wireup deletion in Task 5.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Delete W6SamplesWireup.cs + .meta

**Files:**
- Delete: `Assets/Editor/W6SamplesWireup.cs`
- Delete: `Assets/Editor/W6SamplesWireup.cs.meta`

**Why this task exists:** All responsibilities now live in SceneBuilder. The old file is dead code and a source of future confusion.

- [ ] **Step 1: Confirm no callers**

```bash
grep -rn "W6SamplesWireup\|KeyFlow/W6 Samples Wireup" Assets/ docs/ 2>/dev/null
```

Expected results:
- `Assets/Editor/W6SamplesWireup.cs` itself (about to be deleted)
- `Assets/Editor/W6SamplesWireup.cs.meta` (about to be deleted)
- Some references in `docs/superpowers/plans/*` and `docs/superpowers/reports/*` (historical documentation — leave as-is; they describe the pre-SP7 state correctly)
- NO C# code in `Assets/Scripts/*` or other files under `Assets/Editor/*` that imports or calls `W6SamplesWireup.Wire` — if any such caller exists, STOP and escalate (unexpected coupling).

- [ ] **Step 2: Delete the files via git**

```bash
git rm Assets/Editor/W6SamplesWireup.cs Assets/Editor/W6SamplesWireup.cs.meta
```

- [ ] **Step 3: Verify compilation and test suite**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

Expected: 135/135 pass. If compilation fails because something references `W6SamplesWireup` or `KeyFlow.Editor.W6SamplesWireup` namespace member, restore and investigate (should not happen given Step 1 check).

- [ ] **Step 4: Commit the deletion**

```bash
git commit -m "$(cat <<'EOF'
chore(w6-sp7): delete W6SamplesWireup.cs — superseded by SceneBuilder

All 5 responsibilities migrated into SceneBuilder via Tasks 1-4:
- LoadPitchSamples helper + 17 Salamander sample loading
- AudioSamplePool.pitchSamples / baseMidi / stepSemitones in BuildManagers
- TapInputHandler.judgmentSystem back-reference in BuildManagers
- SettingsScreen.creditsLabel Text child in BuildSettingsCanvas

KeyFlow/W6 Samples Wireup menu entry disappears. SceneBuilder.Build()
single-run is now complete; no more manual two-step workflow.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Scene regeneration + diff verification

**Files:**
- Modify: `Assets/Scenes/GameplayScene.unity` (regenerated by Unity)

**Why this task exists:** Confirm that SceneBuilder's new single-pass output equals what the previous two-step workflow produced. Any unexpected scene diff means something drifted.

- [ ] **Step 1: Close any interactive Unity Editor, then rebuild scene**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod KeyFlow.Editor.SceneBuilder.Build -quit -logFile Builds/scene-build.txt
```

Expected: log ends with `[KeyFlow] W4 scene built: Assets/Scenes/GameplayScene.unity`. No `Missing pitch sample` LogError, no `Field '...' not found` LogError.

- [ ] **Step 2: Run EditMode tests after scene rebuild**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

Expected: 135/135 pass.

- [ ] **Step 3: Inspect scene diff**

```bash
git diff --stat Assets/Scenes/GameplayScene.unity
```

**Expected:** minimal diff — ideally none or only trivial line reorderings. Why: the previous workflow was `SceneBuilder.Build` → `W6SamplesWireup.Wire`, whose combined output is what git already has. SP7's `SceneBuilder.Build` alone should now produce the same output.

**If the diff is large or contains unexpected changes** (e.g., missing pitchSamples references, missing CreditsLabel GameObject, `TapInputHandler judgmentSystem: {fileID: 0}`):
- STOP, do not commit.
- Inspect the specific hunk to identify which wiring was missed.
- Fix in SceneBuilder.cs, re-run Step 1 + Step 2.
- Treat as BLOCKED until diff is minimal.

- [ ] **Step 4: Commit the scene (if diff is non-empty, even if small)**

```bash
git add Assets/Scenes/GameplayScene.unity
git diff --cached --quiet && echo "No scene diff to commit" || git commit -m "$(cat <<'EOF'
chore(w6-sp7): regenerate GameplayScene.unity with consolidated SceneBuilder

SceneBuilder.Build() now produces the same wired scene that previously
required SceneBuilder.Build + W6SamplesWireup.Wire. Single menu click.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: APK build + device playtest (Galaxy S22)

**Files:** none code-side; produces `Builds/keyflow-w6-sp2.apk`

**Why this task exists:** The ultimate regression gate. EditMode can't catch wiring mistakes on serialized fields (empty arrays, null references) — only the device can prove multi-pitch audio and CreditsLabel still work.

- [ ] **Step 1: Close any interactive Unity Editor, then build release APK**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod KeyFlow.Editor.ApkBuilder.Build -quit -logFile Builds/apk-build.txt
```

Long build (5-15 min typical for IL2CPP on this project). Use Bash timeout of 900000.

Expected final log line: `[KeyFlow] APK built at Builds\keyflow-w6-sp2.apk, size XXX MB`. APK on-disk size should be very close to SP6's 36.92 MB (no runtime or asset change in SP7).

- [ ] **Step 2: Verify APK size**

```bash
stat -c "%s" Builds/keyflow-w6-sp2.apk
```

Expected bytes < 41_943_040 (40 MB spec §7 binding), ideally very close to 36.92 MB. If significantly larger, investigate (BLOCKED).

- [ ] **Step 3: Install on Galaxy S22 and surface device checklist to user**

```bash
adb install -r Builds/keyflow-w6-sp2.apk
```

If device isn't connected, surface to user: "APK built at `Builds/keyflow-w6-sp2.apk` (N MB). Please reconnect Galaxy S22 then I can install."

Once installed, hand the 5-item checklist to the user (from spec §6.3):

1. Multi-pitch tap sound — enter gameplay; each lane plays chart-driven different pitches (NOT uniform piano_c4). **Primary regression signal.**
2. Settings → credits text — open Settings; confirm "Piano samples: Salamander Grand Piano V3 by Alexander Holm, CC-BY 3.0" visible at bottom.
3. Calibration click — non-piano "틱" sound (SP5 guard).
4. ComboHUD + full-width tiles + background — SP6 baseline preserved.
5. No tap latency / judgment regression.

- [ ] **Step 4: Do NOT commit the APK** — build artifact, not source.

If any checklist item fails, STOP and report the specific failure before proceeding to Task 8. Most likely failure mode: item 1 fails because `SetArrayField` or `SetField` didn't write the expected field → check SceneBuilder's BuildManagers block, fix, loop back to Task 6.

---

## Task 8: Completion report + memory update

**Files:**
- Create: `docs/superpowers/reports/2026-04-23-w6-sp7-scenebuilder-wireup-consolidation-completion.md`
- Create: `C:/Users/lhk/.claude/projects/C--dev-unity-music/memory/project_w6_sp7_complete.md`
- Modify: `C:/Users/lhk/.claude/projects/C--dev-unity-music/memory/MEMORY.md`
- Modify: `C:/Users/lhk/.claude/projects/C--dev-unity-music/memory/project_w6_sp4_complete.md` (note trap resolved)

**Why this task exists:** Capture that the SP4/SP6 wireup trap is now permanently removed, and give future sessions a tidy memory entry.

- [ ] **Step 1: Write completion report**

Create `docs/superpowers/reports/2026-04-23-w6-sp7-scenebuilder-wireup-consolidation-completion.md` with sections:
1. Summary (1 paragraph: what moved, what was deleted, trap status)
2. Commits list (Tasks 1-6, excluding Task 7/8 docs)
3. Files touched (modified / deleted / untouched)
4. Tests (135 → 135)
5. APK size (before/after vs SP6 36.92 MB — expected ~identical)
6. Device verification (copy §6.3 results from user)
7. Regressions (if any)
8. Carry-overs (remaining after SP7)
9. Spec guardrails verdict table
10. Ship/Hold verdict

Template: `docs/superpowers/reports/2026-04-22-w6-sp6-gameplay-visual-polish-completion.md`.

- [ ] **Step 2: Create new memory file**

`C:/Users/lhk/.claude/projects/C--dev-unity-music/memory/project_w6_sp7_complete.md`:

```markdown
---
name: W6 SP7 merged — SceneBuilder/Wireup consolidation
description: SP4 carry-over #2 resolved. W6SamplesWireup.cs deleted; all 5 responsibilities folded into SceneBuilder. Single-menu scene build. Pure refactor.
type: project
---
```

Body: 2-3 paragraphs — what shipped, why (SP4+SP6 trap history), what to apply (future SPs touching SceneBuilder only need single menu run).

- [ ] **Step 3: Update MEMORY.md**

Append to `C:/Users/lhk/.claude/projects/C--dev-unity-music/memory/MEMORY.md`:

```
- [W6 SP7 merged; SceneBuilder/Wireup consolidation](project_w6_sp7_complete.md) — W6SamplesWireup.cs deleted 2026-04-23; SP4 carry-over #2 resolved; future SPs need only SceneBuilder.Build() (no more two-step trap)
```

- [ ] **Step 4: Annotate SP4 memo as trap-resolved**

Edit `C:/Users/lhk/.claude/projects/C--dev-unity-music/memory/project_w6_sp4_complete.md`. Find the SceneBuilder↔Wireup coupling carry-over entry and prepend:

```
**[RESOLVED by W6 SP7 on 2026-04-23]**
```

- [ ] **Step 5: Commit report**

```bash
git add docs/superpowers/reports/2026-04-23-w6-sp7-scenebuilder-wireup-consolidation-completion.md
git commit -m "$(cat <<'EOF'
docs(w6-sp7): completion report

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Memory files live outside the project root — update via Write tool, not git.

---

## Summary

- **Tasks:** 8 (Task 1 infra, 2-4 migration, 5 deletion, 6 scene regen + diff, 7 APK + device, 8 docs/memory)
- **Test count:** 135 → **135** (pure refactor, no new tests)
- **SceneBuilder.cs growth:** +~55 lines (int SetField overload + 2 const entries + LoadPitchSamples helper + 4 wiring lines in BuildManagers + CreditsLabel block in BuildSettingsCanvas)
- **W6SamplesWireup.cs:** −111 lines (deleted)
- **Net code delta:** −56 lines. And one manual menu step eliminated forever.
- **APK delta target:** ~0 (no runtime or asset change)
- **Verification gates:** EditMode green after Tasks 1-6; scene diff minimal in Task 6; device checklist in Task 7
- **Rollback:** single `git revert` of each relevant commit (6 commits) — or revert-range via `git revert <Task 1 SHA>..<Task 6 SHA>`
