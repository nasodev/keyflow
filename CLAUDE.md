# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project at a glance

KeyFlow is a Magic-Piano-style 4-lane rhythm game for Android, built on **Unity 6000.3.13f1** (IL2CPP, arm64-v8a, target: Galaxy S22 / Android 16). Portrait orientation, single `GameplayScene.unity` drives all screens via SetActive toggling. Target perceived input→audio latency is 50–80 ms.

## Unity Editor menu items (all live under `KeyFlow/`)

| Menu | Method | Purpose |
|---|---|---|
| `KeyFlow/Build W4 Scene` | `SceneBuilder.Build` | **Regenerates** `Assets/Scenes/GameplayScene.unity` from scratch. This is the single source of truth for scene wiring — never hand-edit the `.unity` file; modify `SceneBuilder.cs` and re-run. |
| `KeyFlow/Build Calibration Click` | `CalibrationClickBuilder.Build` | Generates `Assets/Audio/calibration_click.wav` deterministically (seeded RNG → byte-identical output). One-time setup. |
| `KeyFlow/Build Feedback Assets` | `FeedbackPrefabBuilder.Build` | Generates hit/miss particle prefabs + `FeedbackPresets.asset`. One-time setup. |
| `KeyFlow/Build APK` | `ApkBuilder.Build` | Release APK → `Builds/keyflow-w<X>-sp<Y>.apk`. **The filename is hard-coded** — bump it per SP milestone (see `Assets/Editor/ApkBuilder.cs`). |
| `KeyFlow/Build APK (Profile)` | `ApkBuilder.BuildProfile` | Development + profiler + debug APK. |
| `KeyFlow/Apply Android Icon (SP9)` | `SP9IconSetter.Apply` | Writes `Assets/Textures/icon.png` into every Android legacy icon slot. Safe to re-run. |

Order of first-time setup: Calibration Click → Feedback Assets → Build W4 Scene → Build APK.

## Batch-mode CLI (CI / headless)

```bash
# Regenerate scene
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -executeMethod KeyFlow.Editor.SceneBuilder.Build -quit

# Release APK (scene must already be built)
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -executeMethod KeyFlow.Editor.ApkBuilder.Build -quit

# Force-reimport piano WAVs so AudioImporter postprocessor settings serialize
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -executeMethod KeyFlow.Editor.PianoSampleImportPostprocessor.ForceReimportPianoSamples -quit

# EditMode tests → Builds/test-results.xml
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults Builds/test-results.xml
```

**CRITICAL**: do **not** pass `-quit` together with `-runTests` — Unity skips the test runner if both are present. All other `-executeMethod` invocations must pass `-quit`.

Install to a connected device:

```bash
adb install -r Builds/keyflow-w6-sp12.apk
```

## Python charting pipeline (tools/midi_to_kfchart/)

Converts Mutopia PD MIDIs → `.kfchart` JSON files in `Assets/StreamingAssets/charts/`.

```bash
cd tools/midi_to_kfchart
python -m venv .venv && source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -r requirements.txt
pytest -q                                            # unit tests
python midi_to_kfchart.py --batch batch_w6_sp2.yaml  # regenerate all shipped charts
```

Hand-edits to `.kfchart` files are lost on re-run. Tune by adjusting `target_nps` in the batch YAML, not by editing JSON.

## Personal songs (copyrighted material)

The repo distinguishes **public** songs (public-domain or licensed for distribution; safe to commit) from **personal** songs (copyrighted material kept on local machines only). The split is enforced by **directory convention**:

| Public | Personal (gitignored) |
|---|---|
| `midi/` (any non-`personal/` subdir) | `midi/personal/` |
| `Assets/StreamingAssets/charts/*.kfchart` | `Assets/StreamingAssets/charts/personal/*.kfchart` |
| `Assets/StreamingAssets/thumbs/*.png` | `Assets/StreamingAssets/thumbs/personal/*.png` |
| `Assets/StreamingAssets/catalog.kfmanifest` | `Assets/StreamingAssets/catalog.personal.kfmanifest` |
| `tools/midi_to_kfchart/batch_*.yaml` | `tools/midi_to_kfchart/personal/batch_*.yaml` |

**Adding a personal song:**

1. Drop the source MIDI in `midi/personal/`.
2. Add an entry to a batch yaml under `tools/midi_to_kfchart/personal/` (the location triggers personal routing — no YAML flag needed).
3. `python midi_to_kfchart.py --batch tools/midi_to_kfchart/personal/<file>.yaml` — output goes to `charts/personal/`.
4. Drop a thumbnail PNG in `thumbs/personal/`.
5. Add a song entry to `Assets/StreamingAssets/catalog.personal.kfmanifest` (create the file if absent — `version: 1` plus `songs: []` template). Set `"thumbnail": "thumbs/personal/<file>.png"`.

That's it. `git status` will show no new tracked files. The five `.gitignore` directory rules cover everything.

**At runtime:** `SongCatalog.LoadAsync` loads `catalog.kfmanifest` (required) and merges `catalog.personal.kfmanifest` (optional, missing → no overlay). Personal entries are tagged `isPersonal=true` and `ChartLoader` resolves their charts to `charts/personal/<id>.kfchart`.

## Code architecture

### Assemblies

Three C# assemblies (see the three `.asmdef` files):

- **`KeyFlow.Runtime`** (`Assets/KeyFlow.Runtime.asmdef`) — all gameplay code under `KeyFlow` namespace + submodules (`KeyFlow.Charts`, `KeyFlow.Calibration`, `KeyFlow.UI`, `KeyFlow.Feedback`).
- **`KeyFlow.Editor`** (`Assets/Editor/KeyFlow.Editor.asmdef`) — Editor-only tooling (menu items, asset builders, import postprocessors, Android post-gradle hook).
- **`KeyFlow.Tests.EditMode`** (`Assets/Tests/EditMode/KeyFlow.Tests.EditMode.asmdef`) — NUnit EditMode suite (135+ tests), gated by `UNITY_INCLUDE_TESTS`.

Runtime code exposes internals to tests via `[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("KeyFlow.Tests.EditMode")]` (declared in `JudgmentSystem.cs`).

### Gameplay data flow

The `GameplayScene` wires these components into a pipeline. Dependencies are `[SerializeField]` references set by `SceneBuilder`, never by hand:

```
TapInputHandler ──onLaneTap──►  JudgmentSystem ──OnJudgmentFeedback──►  FeedbackDispatcher
                                     ▲                                  ├──► HapticService (Android VibrationEffect)
                                     │                                  ├──► ParticlePool
NoteSpawner ──RegisterPendingNote──┘                                    └──► JudgmentTextPool
     ▲
     │reads ChartDifficulty
ChartLoader (JSON → ChartData)          HoldTracker ──drives──► HoldStateMachine (Spawned→Holding→Completed/Broken)
     ▲                                       │
AudioSyncManager (owns songStart dspTime)    └─►  AudioSamplePool.PlayForPitch (16-channel round-robin, semitone pitch-shifted Salamander samples)
     ▲
CalibrationController → UserPrefs.CalibrationOffsetMs
```

`GameplayController.ResetAndStart()` orchestrates per-song startup: chart load → optional calibration → countdown → `audioSync.StartSilentSong()`. Completion is polled in `Update()` by comparing `SongTimeMs` against the last note's scheduled end + Good window, then transitions to `Results` via `ScreenManager.Instance.Replace`.

### Timing model

**`AudioSyncManager`** owns the authoritative song clock via `AudioSettings.dspTime` (not `Time.time`). All gameplay time is expressed in **ms** relative to `songStartDsp`, computed in `GameTime.GetSongTimeMs(nowDsp, songStartDsp, calibOffsetSec)`. Every component that reads time calls `audioSync.SongTimeMs` and guards on `IsPlaying && !IsPaused`.

`AudioSyncManager.TimeSource` is a test seam: EditMode tests inject a manual `ITimeSource` clock so they can advance `DspTime` deterministically without Unity's audio thread running.

Calibration offsets are stored in `UserPrefs.CalibrationOffsetMs` (PlayerPrefs-backed) and applied as `audioSync.CalibrationOffsetSec`. `UserPrefs.MigrateLegacy()` migrates a pre-W4 `CalibOffsetMs` key on first run.

### Scene construction is Editor-only

SP7 consolidated all post-build scene wiring into `SceneBuilder.cs` as a single source of truth. Previously, forgetting a second "W6 Samples Wireup" step caused "all lanes play the same pitch" regressions twice in W6. **If you add a new component that needs scene-wiring**, add its `BuildX` method to `SceneBuilder.cs` and ensure `Build` calls it — don't create a new `[MenuItem]` helper tool.

Texture/audio import settings are enforced by AssetPostprocessors (`BackgroundImporterPostprocessor`, `PianoSampleImportPostprocessor`) so they survive fresh checkouts where `.meta` files may be re-generated. Don't hand-edit texture/audio import settings in the Inspector for covered assets — they'll be overwritten on next import.

### Chart format (`.kfchart`)

Newtonsoft JSON, loaded by `ChartLoader.ParseJson`. Per-difficulty note arrays, each note has `{t, lane (0-3), pitch (36-83 clamped), type ("TAP"|"HOLD"), dur}`. `ChartLoader.Validate` enforces: `TAP.dur=0`, `HOLD.dur>0`, notes sorted by `t`, `totalNotes == notes.Count`. These checks are load-bearing — don't loosen them without a test.

Chart is loaded from `Application.streamingAssetsPath/charts/<songId>.kfchart`. On Android this requires `UnityWebRequest` (streaming assets are inside the APK); the loader branches on `UNITY_ANDROID && !UNITY_EDITOR`.

### Screen model

`ScreenManager` (`AppScreen.Start | Main | Gameplay | Results`) manages four mutually-exclusive root GameObjects + three overlays (`Settings`, `Pause`, `Calibration`). **There is only one scene.** Transitions are `Replace(target)` which toggles `SetActive` and fires `OnReplaced`. Back button is Android-hardware `Escape` handled in `ScreenManager.Update`; double-press on `Start` quits.

StartScreen BGM uses Unity's native `SetActive`-lifecycle propagation: `startRoot.SetActive(false)` → `StartScreen.OnDisable` → `bgmSource.Stop()`. No explicit cross-screen coupling — this pattern is intentional, don't replace it with a manager.

### Test hooks convention

Runtime components expose EditMode-only test seams inside `#if UNITY_EDITOR || UNITY_INCLUDE_TESTS` blocks. Two patterns:

- `internal void SetDependenciesForTest(...)` — inject dependencies that are normally `SerializeField`-wired by SceneBuilder.
- `internal void TickForTest()` / `InvokeXForTest(...)` — drive `Update` or private methods deterministically.

When adding features, follow this pattern rather than making fields `public` or using reflection.

## Working notes

- `Assets/Scenes/GameplayScene.unity` is in git but is **generated**. Regenerate via `KeyFlow/Build W4 Scene` whenever scene-affecting code changes; commit the resulting diff.
- `docs/superpowers/{specs,plans,reports}/` contains the per-sprint design docs, plans, and completion reports (one per SP). Read the latest spec+completion report pair to understand recent context before starting a W6 SP-style task.
- Project freezes built-in Packages (see `Packages/manifest.json`) at specific versions — Input System 1.19.0, Test Framework 1.6.0, Newtonsoft JSON 3.2.1. Don't upgrade casually; the SceneBuilder + InputSystem wiring assumes these.
- Android VIBRATE permission is injected at gradle-generation time by `AddVibratePermission : IPostGenerateGradleAndroidProject`, not via an `AndroidManifest.xml` override.
