# KeyFlow W6 Sub-Project 7 — SceneBuilder ↔ Wireup Consolidation

**Date:** 2026-04-23
**Week:** 6 (폴리싱 + 사운드)
**Priority:** SP4 carry-over #2 (DX improvement — eliminate the two-step scene build workflow trap)
**Status:** Proposed

---

## 1. Motivation

Since W6 SP1 (2026-04-21), the gameplay scene has required **two manual menu clicks to be correct**:

1. `KeyFlow/Build W4 Scene` (`SceneBuilder.Build`) — creates the scene with most wiring.
2. `KeyFlow/W6 Samples Wireup` (`W6SamplesWireup.Wire`) — wires five extra things afterwards: `AudioSamplePool.pitchSamples[17]`, `AudioSamplePool.baseMidi=36`, `AudioSamplePool.stepSemitones=3`, `TapInputHandler.judgmentSystem`, `SettingsScreen.creditsLabel` Text child.

SceneBuilder wipes the scene from scratch each build. If a SP modifies `SceneBuilder.Build()` and the implementer forgets step 2, the five wirings above vanish and multi-pitch audio silently breaks (all four lanes play default `piano_c4.wav` regardless of chart pitch).

**This trap has fired twice already:**
- **SP4** (`3157251`): "모든 4개 레인이 단일 피치" — fixed by manually re-running wireup and documented in the completion report.
- **SP6** (`0b394c9`): scene regeneration diff was 196 lines — concrete evidence that Wireup re-run restored wiring that SceneBuilder.Build() had wiped.

Both SPs caught it during device playtest (not EditMode — runtime-only symptom), costing one APK rebuild cycle each time.

## 2. Goal

Consolidate all gameplay-scene wiring into `SceneBuilder.Build()` so that running the single menu produces a fully-wired, ready-to-play scene. Delete `W6SamplesWireup.cs` entirely — its responsibilities move to SceneBuilder; it becomes redundant.

Qualitative success criterion: on Galaxy S22, after running only `KeyFlow/Build W4 Scene` + building APK, gameplay works identically to the current two-step workflow (multi-pitch tap sound, CC-BY credit text in Settings, calibration click, full-width tiles + combo HUD + background all preserved from SP1-SP6).

## 3. Non-goals

- Refactoring SceneBuilder.cs's overall structure (1200+ line file) — separate SP candidate.
- Adding EditMode tests for SceneBuilder — Editor-tool testing not in project scope.
- Adding dynamic runtime sample loading — `pitchSamples` array stays authored at build time.
- Reimplementing SerializedObject batch-wiring as reflection-based — keeps existing safer SerializedObject approach.
- Providing a standalone "repair Wireup" utility — `SceneBuilder.Build()` re-run is idempotent and serves any repair case.
- Touching any runtime code (`AudioSamplePool`, `TapInputHandler`, `JudgmentSystem`, `SettingsScreen`, etc.).

## 4. Approach

**Full consolidation (Option A from brainstorming).** All five Wireup responsibilities move into `SceneBuilder.cs` methods where they belong naturally:

- **17 Salamander sample loading** + `SampleNames[]` constant array → new private helper `LoadPitchSamples()` called from `SceneBuilder.Build()` alongside existing asset loads (piano_c4, calibration_click, background).
- **`AudioSamplePool.pitchSamples` / `baseMidi` / `stepSemitones` wiring** → moved into `BuildManagers`, right after the existing `SetField(samplePool, "defaultClip", pianoClip)` line. Uses SerializedObject batch-set (Wireup's existing pattern, preserved) for array field safety.
- **`TapInputHandler.judgmentSystem` back-reference** → moved into `BuildManagers`, after `judgmentSystem` local variable exists.
- **`SettingsScreen.creditsLabel` Text child construction** → moved into `BuildSettingsCanvas`. GameObject + RectTransform + Text + SetField all happen inline where the SettingsCanvas is built.

`W6SamplesWireup.cs` + `.meta` deleted; `KeyFlow/W6 Samples Wireup` menu disappears.

### 4.1 Files

**Modified:**
- `Assets/Editor/SceneBuilder.cs` (+~50 lines):
  - Add `PianoFolder` const + `SampleNames[17]` const array (copy-paste from Wireup)
  - Add `LoadPitchSamples()` private static helper returning `AudioClip[]` or triggering abort (LogError + early return, same pattern as existing asset guards)
  - In `Build()`: call `LoadPitchSamples()` with null-guard immediately after the existing `background_gameplay.png` guard
  - In `BuildManagers`: accept `AudioClip[] pitchSamples` parameter or use closure; wire `pitchSamples`/`baseMidi`/`stepSemitones` on `AudioSamplePool` via SerializedObject; wire `TapInputHandler.judgmentSystem = judgmentSystem` via SetField
  - In `BuildSettingsCanvas`: after SettingsScreen component is added and canvas built, construct CreditsLabel GameObject (RectTransform + Text + Font + color + anchoring) and `SetField(settings, "creditsLabel", txt)`
- `Assets/Scenes/GameplayScene.unity` — may regenerate with no net diff (if SP7 done correctly, final state matches current "SceneBuilder + Wireup" output)

**Deleted:**
- `Assets/Editor/W6SamplesWireup.cs`
- `Assets/Editor/W6SamplesWireup.cs.meta`

**NOT modified (guardrails):**
- All `Assets/Scripts/*` runtime code
- `Assets/Audio/piano/*` (17 Salamander samples)
- `Assets/Audio/piano_c4.wav`, `calibration_click.wav`
- `Assets/Sprites/background_gameplay.png`
- Other Editor tools: `CalibrationClickBuilder`, `FeedbackPrefabBuilder`, `ApkBuilder`, `PianoSampleImportPostprocessor`, `BackgroundImporterPostprocessor`, `AddVibratePermission`

### 4.2 Key code changes (sketch)

**New constants + helper (add near other const block):**

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

**In `Build()` (after background_gameplay.png guard):**

```csharp
var pitchSamples = LoadPitchSamples();
if (pitchSamples == null) return;
```

Pass `pitchSamples` into `BuildManagers` (signature gets +1 parameter) or capture in closure.

**In `BuildManagers` (after `SetField(samplePool, "defaultClip", pianoClip);`):**

```csharp
// Wire pitchSamples array via SerializedObject (safer than SetField for arrays)
var poolSo = new SerializedObject(samplePool);
var pitchProp = poolSo.FindProperty("pitchSamples");
pitchProp.arraySize = pitchSamples.Length;
for (int i = 0; i < pitchSamples.Length; i++)
    pitchProp.GetArrayElementAtIndex(i).objectReferenceValue = pitchSamples[i];
poolSo.FindProperty("baseMidi").intValue = 36;
poolSo.FindProperty("stepSemitones").intValue = 3;
poolSo.ApplyModifiedPropertiesWithoutUndo();
```

**In `BuildManagers` (after `judgmentSystem` is constructed):**

```csharp
// Back-reference: TapInputHandler needs JudgmentSystem for pitch lookup on tap
SetField(tapInput, "judgmentSystem", judgmentSystem);
```

**In `BuildSettingsCanvas` (after `var settings = canvasGO.AddComponent<SettingsScreen>();` or equivalent):**

```csharp
// CreditsLabel Text child for CC-BY Salamander credit
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
SetField(settings, "creditsLabel", creditsText);
```

### 4.3 Data flow (before → after)

**Before (two manual steps):**

```
Developer → KeyFlow/Build W4 Scene → SceneBuilder.Build
  (most wiring done, save scene, Wireup 5 items missing)

Developer → KeyFlow/W6 Samples Wireup → W6SamplesWireup.Wire
  (opens scene, FindFirstObjectByType, wires 5 items, saves scene again)
```

**After (single step):**

```
Developer → KeyFlow/Build W4 Scene → SceneBuilder.Build
  Load piano_c4, calibration_click, background_gameplay, pitchSamples[17]
  Build camera, background canvas, gameplay root
  BuildManagers: samplePool/tapInput/judgmentSystem/spawner/holdTracker
    - defaultClip + pitchSamples[17]+baseMidi+stepSemitones wired
    - tapInput.judgmentSystem wired (back-reference)
  BuildHUD, BuildCalibrationOverlay, BuildResultsCanvas, BuildSettingsCanvas
    - BuildSettingsCanvas also creates CreditsLabel Text child + wires
  BuildGameplayController, ScreenManager, save scene (once)
```

## 5. Error handling & edge cases

**Missing piano sample (one of 17):**
- `LoadPitchSamples()` returns `null` → Build() early-returns with LogError. Same pattern as existing piano_c4 / calibration_click / background guards. Scene not left in partial state.

**SerializedObject array wiring failure:**
- `FindProperty("pitchSamples")` returning null would NRE on `arraySize` — acceptable because field name must exist (if `AudioSamplePool.pitchSamples` is renamed in runtime code, this script correctly breaks loudly).
- `ApplyModifiedPropertiesWithoutUndo` ensures no undo-stack pollution during scene build.

**SceneBuilder's existing `SetField` vs SerializedObject — consistency:**
- Mixed approach acceptable: single-value primitive fields use `SetField`; array fields use SerializedObject. This matches the existing Wireup code's implicit contract. Not a refactoring target for this SP.

**creditsLabel timing:**
- Previously Wireup ran `FindFirstObjectByType<SettingsScreen>(FindObjectsInactive.Include)` because SettingsCanvas is initially inactive (shown only when user opens settings).
- In SceneBuilder, the SettingsScreen component and the canvas are constructed in the same method, so we have the component reference in a local variable — no Find needed.

**Deletion cleanup:**
- `git rm` `W6SamplesWireup.cs` and `.meta`. Project compiles (no imports of this namespace member exist outside the file).

**Runtime field rename protection:**
- If someone later renames `AudioSamplePool.pitchSamples` to `pitchClips`, SceneBuilder silently fails at wiring (SerializedObject returns null property). **Mitigation:** the device checklist's multi-pitch verification catches this immediately.

**Intentionally not handled (YAGNI):**
- Any "Wireup repair" mode — absent by design; `SceneBuilder.Build()` re-run is the only path.
- Diff-only update to scene (in-place patching without full rebuild) — SceneBuilder has always been full-rebuild; don't change that here.
- Validation that `SampleNames[]` matches actual files on disk at build time — `LoadPitchSamples` itself is that validation.

## 6. Testing

### 6.1 EditMode tests: no new tests

This SP is a pure refactor — no behavior change from the user's perspective, no new API surface. SceneBuilder is not unit-tested (matching project convention for Editor tools). Existing 135 tests must all remain green.

### 6.2 Existing test coverage as regression gate

- `AudioSamplePoolTests` (runtime pitchSamples logic) — protects runtime integrity
- `JudgmentSystemTests` — covers pitch lookup via `GetClosestPendingPitch`
- `ScoreManagerTests`, `ChartLoaderTests`, etc. — baseline gameplay health
- None of these exercise `SceneBuilder.Build()` directly; they operate on runtime types assembled by tests themselves.

### 6.3 Device checklist (Galaxy S22)

Only device can catch multi-pitch / creditsLabel wiring regressions (EditMode can't observe scene asset state meaningfully).

1. ✅ **Multi-pitch tap sound** — enter Für Elise Easy or Entertainer Normal; confirm each lane plays chart-driven different pitches (not uniform piano_c4). **This is the primary defense against wiring regression.**
2. ✅ **Settings → credits text** — open Settings screen; confirm "Piano samples: Salamander Grand Piano V3 by Alexander Holm, CC-BY 3.0" visible at bottom.
3. ✅ **Calibration click** — fresh calibration or Settings re-run; confirm non-piano "틱" click (SP5 guard).
4. ✅ **ComboHUD + full-width tiles + background** — SP6 baseline preserved.
5. ✅ **No tap latency / judgment regression** — subjective feel vs SP6.

### 6.4 Regression scene diff check

After running `SceneBuilder.Build()` on the new implementation:

```bash
git diff Assets/Scenes/GameplayScene.unity
```

Expected: minimal diff vs post-SP6 state (the goal is SceneBuilder.Build() alone now produces what SceneBuilder + W6SamplesWireup used to produce together). Large unrelated diff → something in BuildManagers or BuildSettingsCanvas drifted; investigate before committing.

### 6.5 Test count target

- Baseline (SP6 merged): 135
- Target (SP7 merged): **135** (+0)

## 7. Risks & rollback

| Risk | Likelihood | Mitigation |
|---|---|---|
| SerializedObject wiring silently skips because field name typo | Low | Device checklist item 1 (multi-pitch) catches immediately; SerializedObject NRE is loud in logs |
| SceneBuilder gets noticeably harder to read post-refactor | Medium | +50 lines scoped to 2 existing methods; not introducing new abstractions. Accept as incremental cost |
| W6SamplesWireup deletion breaks some undocumented workflow | Very low | grep confirms no code callers; only human-menu usage |
| Scene diff emerges where not expected | Medium | §6.4 explicit check before commit; rollback by reverting the commit |
| Runtime code inadvertently touched | Very low | Plan's commit discipline keeps runtime untouched; spec §3 guardrail list verifiable via git log |

**Rollback:** Single `git revert` of the SP's final commit restores both `W6SamplesWireup.cs` and the SceneBuilder changes. No asset migration needed.

## 8. Out-of-scope / deferred

- **Hold-note audio feedback** (SP3 carry-over) — separate SP candidate
- **`AudioSource.Play()` per-tap 1.6 KB alloc** (SP4 carry-over) — separate SP candidate
- **APK filename bump** — still `keyflow-w6-sp2.apk` per user direction
- **W6 #6 2nd-device test** — separate SP
- **SceneBuilder.cs decomposition** (1200+ lines → smaller files) — separate SP if desired

## 9. Done criteria

- [ ] `Assets/Editor/W6SamplesWireup.cs` + `.meta` deleted from git.
- [ ] `Assets/Editor/SceneBuilder.cs` contains `SampleNames[]`, `LoadPitchSamples()`, pitchSamples/baseMidi/stepSemitones wiring in `BuildManagers`, `tapInput.judgmentSystem` back-ref in `BuildManagers`, CreditsLabel construction + wire in `BuildSettingsCanvas`.
- [ ] `KeyFlow/W6 Samples Wireup` menu entry no longer appears in Unity (verified post-compile).
- [ ] EditMode tests 135/135 green.
- [ ] `SceneBuilder.Build()` single-run produces scene with multi-pitch + credits wired (scene diff vs baseline minimal).
- [ ] Galaxy S22 checklist §6.3 all 5 items pass.
- [ ] APK unchanged (no runtime or asset change).
- [ ] Completion report `docs/superpowers/reports/2026-04-23-w6-sp7-scenebuilder-wireup-consolidation-completion.md` committed.
- [ ] Memory: new SP7 memo + MEMORY.md index entry; SP4 memo can note the trap is now fully resolved.

## 10. References

- Trap precedent: `docs/superpowers/reports/2026-04-22-w6-sp4-completion.md` §6.3 finding #1 (multi-pitch wipe)
- Trap precedent 2: `docs/superpowers/reports/2026-04-22-w6-sp6-gameplay-visual-polish-completion.md` Task 10 (wireup re-run 196-line diff)
- Current SceneBuilder entrypoint: `Assets/Editor/SceneBuilder.cs`
- Current Wireup to be deleted: `Assets/Editor/W6SamplesWireup.cs`
- Runtime targets (unchanged): `Assets/Scripts/Gameplay/AudioSamplePool.cs`, `TapInputHandler.cs`, `JudgmentSystem.cs`, `Assets/Scripts/UI/SettingsScreen.cs`
- SP4 memo documenting the trap: `C:/Users/lhk/.claude/projects/C--dev-unity-music/memory/project_w6_sp4_complete.md`
