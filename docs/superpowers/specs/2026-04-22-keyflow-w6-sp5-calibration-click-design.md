# KeyFlow W6 Sub-Project 5 — Dedicated Calibration Click Sample

**Date:** 2026-04-22
**Week:** 6 (폴리싱 + 사운드)
**Priority:** W6 #4 (W4 carry-over #2)
**Status:** Proposed

---

## 1. Motivation

`CalibrationController` currently reuses `Assets/Audio/piano_c4.wav` as its click — a placeholder carried from W3 sign-off (`docs/superpowers/reports/2026-04-20-w3-completion.md` §"Known limitations" item 4). Two problems:

1. **Aurally heavy.** A piano note has a slow percussive attack relative to a calibration click; the "where the click is" landmark is fuzzier than it should be.
2. **Conflicts with SP1 multi-pitch tap sound.** Post-SP1, lane tap sounds vary dynamically by chart pitch (`TapInputHandler.PlayTapSound → JudgmentSystem.GetClosestPendingPitch → AudioSamplePool.PlayForPitch`, fallback `LanePitches.Default`). The calibration stage asks the user to tap to a click, but the click itself is the same instrument family (piano) they'll hear when they tap during gameplay. A non-piano, categorically-distinct click separates "reference metronome" from "tap feedback."

## 2. Goal

Ship a dedicated short, crisp, non-piano calibration click sample, wired through `CalibrationController`, such that:

- The click is clearly distinguishable from piano tap sounds during the 8-beat calibration run.
- The offset measurement itself stays unchanged in behavior (timing math, retry logic, Reliable threshold — all untouched).
- No regression in the existing 126 EditMode tests.

Qualitative success criterion: on Galaxy S22, during a cold-launch first-time calibration, the 8 clicks are heard as crisp "tic" hits distinct from any subsequent gameplay piano tap.

## 3. Non-goals

- User-customizable click sample (setting UI, multiple built-in options).
- Click volume slider (global SFX volume already covers this from W4 Settings).
- Localized/accessibility click variants (visual-only calibration, vibration-only, etc.).
- `piano_c4.wav` removal. It stays as `AudioSamplePool.defaultClip` fallback.
- Touching `CalibrationCalculator` math or retry flow.
- iOS/other-platform validation. Android-only MVP.

## 4. Approach

**Procedurally generate** a short white-noise burst WAV at Editor time via a new menu command. The generated `.wav` is committed to git; at scene-build and runtime, it is loaded like any other static `AudioClip` asset.

This mirrors the SP4 pattern (`FeedbackPrefabBuilder` editor menu → generated `.prefab` + `.asset` committed). Rationale: zero license risk (no 3rd-party sample), deterministic (seeded RNG → reproducible bytes), trivial iteration (tweak constants + re-run menu), CI-friendly (no runtime generation).

### 4.1 Click parameters

| Parameter | Value | Rationale |
|---|---|---|
| Sample rate | 48000 Hz mono | Matches `PianoSampleImportPostprocessor` project convention. |
| Bit depth | 16-bit PCM | Lossless for a ~2 KB asset; avoids Vorbis smearing on a short transient. |
| Duration | 20 ms (960 samples) | Short enough to never overlap at 0.5 s interval; percussive feel. |
| Waveform | White noise (seed 1) | Tone-less → categorically distinct from piano. Fixed seed → deterministic bytes. |
| High-pass | 1-pole @ 500 Hz | Removes low-frequency rumble; pushes timbre to "tic" territory. |
| Attack env | Linear fade-in over 0.5 ms (first 24 samples) | Eliminates click/pop artifact at buffer start. |
| Decay env | Exponential, τ ≈ 4 ms (192 samples) | Sharp transient; well below -60 dB by sample 960. |
| Peak level | -6 dBFS (scale 0.501) | Consistently quieter than piano taps; won't clip when layered. |
| Import | `forceToMono=true`, `DecompressOnLoad`, `preloadAudioData=true` | No first-play load hitch during `PlayScheduled` 2 s lead-in. |

### 4.2 Files

**New (committed):**

- `Assets/Editor/CalibrationClickBuilder.cs` — Editor menu `KeyFlow/Build Calibration Click`. Runs parameter-driven WAV generation, writes `Assets/Audio/calibration_click.wav`, runs `AssetDatabase.ImportAsset` with the import settings above.
- `Assets/Audio/calibration_click.wav` — ~2 KB generated asset. Committed so the repo is buildable without running the menu.
- `Assets/Audio/calibration_click.wav.meta` — AudioImporter settings serialized by the postprocessing step.
- `Assets/Tests/EditMode/CalibrationClickBuilderTests.cs` — 5-6 EditMode tests (see §6).

**Modified:**

- `Assets/Editor/SceneBuilder.cs` — Add one `AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/calibration_click.wav")` load + null guard (same pattern as existing `piano_c4.wav` load at line 45). Pass the click clip into `BuildCalibrationOverlay` instead of `pianoClip`. `BuildManagers(camera, pianoClip, ...)` signature unchanged (still uses `pianoClip` for `AudioSamplePool.defaultClip`).

**Untouched guardrails:**

- `Assets/Scripts/Calibration/CalibrationController.cs` — `clickSample` SerializeField name/type unchanged; runtime logic identical.
- `Assets/Scripts/Calibration/CalibrationCalculator.cs`
- `Assets/Scripts/Gameplay/AudioSamplePool.cs` — `defaultClip = piano_c4.wav` retained.
- `Assets/Editor/W6SamplesWireup.cs` — not touched; not re-required by this SP (scene builder change only affects calibration overlay wiring, not multi-pitch pool wiring).

### 4.3 Data flow

**Build-time (developer, one-shot):**

```
Developer → Unity menu "KeyFlow/Build Calibration Click"
  → CalibrationClickBuilder.Build()
    1. Allocate float[960]
    2. rng = new System.Random(seed: 1)
    3. 1-pole HP filter state = 0
    4. For i in 0..959:
         raw  = rng.NextDouble() * 2 - 1
         hp   = HighPass(raw, cutoff=500Hz @ 48kHz)
         env  = AttackEnv(i) * DecayEnv(i)
         buf[i] = hp * env * 0.501
    5. int16 = Math.Clamp(buf[i] * 32767, -32768, 32767)
    6. WAV header (RIFF/WAVE/fmt/data chunks, 48000 Hz mono PCM16)
    7. File.WriteAllBytes("Assets/Audio/calibration_click.wav", bytes)
    8. AssetDatabase.ImportAsset + configure AudioImporter
    9. AssetDatabase.SaveAssets
```

**Runtime (unchanged):**

```
GameplayController.Start → CalibrationController.Begin
  → StartOneRun → coroutine RunCalibration
    → 8× PlayScheduled(expectedDspTimes[i])  // now plays calibration_click.wav
    → user taps → tapDspTimes collected
    → Evaluate → CalibrationCalculator.Compute → UserPrefs.CalibrationOffsetMs
```

**Scene-build (SceneBuilder diff):**

```
KeyFlow/Build W4 Scene
  → pianoClip = Load(piano_c4.wav)          // existing
  → clickClip = Load(calibration_click.wav) // new (1 line + null guard)
  → BuildCalibrationOverlay(white, clickClip, audioSync)  // arg swap
  → rest identical
```

## 5. Error handling & edge cases

**Builder:**

- `Assets/Audio/` missing → `EnsureFolder` first (SceneBuilder's pattern).
- `File.WriteAllBytes` failure (lock, ACL) → propagate exception; don't try to delete partial writes.
- `AssetDatabase.GetImporterAtPath` returning null → `Debug.LogError` + return (import-time race).
- Clipping check: mathematically impossible given 0.501 peak × envelope ≤ 1.0, but the int16 cast still guards with `Math.Clamp`.

**SceneBuilder:**

- `calibration_click.wav` absent → `Debug.LogError("[KeyFlow] Missing Assets/Audio/calibration_click.wav. Run KeyFlow/Build Calibration Click first."); return;` (same shape as the existing piano_c4 guard). Fresh-clone scene rebuild catches missing asset immediately.

**Runtime:**

- `CalibrationController.clickSample == null` → existing behavior (no sound). Not hardened here; scope out.

**Intentionally not handled (YAGNI):**

- User-settable click. Fixed asset.
- Per-platform variants. 48k mono 16-bit PCM is universal.
- Build-time auto-run of `CalibrationClickBuilder` from CI. Manual one-shot is fine for MVP.

## 6. Testing

### 6.1 EditMode tests (`CalibrationClickBuilderTests.cs`, 5-6 tests)

| Test | Asserts |
|---|---|
| `Build_CreatesFile` | After `Build()`, `Assets/Audio/calibration_click.wav` exists. |
| `Build_IsDeterministic` | Two sequential `Build()` calls → byte-identical `.wav` payloads (seed fixed). |
| `WavHeader_Is48kMono16Bit` | Parse RIFF header: fmt chunk → PCM, sampleRate=48000, channels=1, bitsPerSample=16. |
| `SampleCount_Is960` | Data chunk size = 960 × 2 = 1920 bytes (20 ms × 48k × mono × 16-bit). |
| `Peak_BelowZeroDbfs` | For all int16 samples `s`: `abs(s) ≤ 16384` (~-6 dBFS); no clipping. |
| `AudioImporter_MonoAndPreload` | After import: `AudioImporter.forceToMono == true` and default sample settings have `preloadAudioData == true`. |

### 6.2 Intentional test gaps

- No PlayMode / Android integration test — audio playback is platform-dependent; existing calibration has no PlayMode coverage either.
- No FFT spectral test — over-engineered for a seeded noise burst; determinism test covers the "did the output change" question.
- No filter coefficient unit test — 1-pole IIR is trivially correct by construction; determinism check covers regression.

### 6.3 Device checklist (Galaxy S22)

1. ✅ Fresh install → calibration overlay on first launch → hear 8 non-piano "tics" at 0.5 s interval.
2. ✅ Click audibly distinct from piano taps (user ear judgment; no metric).
3. ✅ Tap 8 times to the beat → `reliable=true` → `UserPrefs.CalibrationOffsetMs` saved (sanity check offset is in a plausible range, e.g., 60-150 ms on this device's prior measurements).
4. ✅ Enter gameplay → piano tap sounds normal (multi-pitch wiring not broken).
5. ✅ Settings → Calibration Re-run → click + tap flow works identically.

### 6.4 Test count target

- Baseline: 126 (post-SP4)
- Target: **131-132** (+5 to +6 new)
- All pass before merging.

## 7. Risks & rollback

| Risk | Likelihood | Mitigation |
|---|---|---|
| Click is subjectively unpleasant on device | Medium | Parameters are all constants in one file; re-tune (duration, decay τ, peak, HP cutoff) and re-run menu. ~10 min iteration. |
| Fresh clone builds scene before running builder menu → missing asset | Low | SceneBuilder null guard with explicit "Run KeyFlow/Build Calibration Click first" hint. |
| WAV header bytes wrong on some platform → Unity import fails | Low | Write canonical 44-byte RIFF/WAVE/fmt/data header; tests verify Unity import succeeds (`AudioImporter` lookup non-null). |
| Builder executed in a session while SceneBuilder.Build() is mid-run | Very low | Both are Editor menus, single-threaded; concurrent invocation not a typical workflow. |

**Rollback:** Revert SceneBuilder line change; calibration falls back to `piano_c4.wav`. `calibration_click.wav` + builder files can stay dormant or be removed. No data migration needed (calibration offset is device-local PlayerPrefs, unaffected).

## 8. Out-of-scope / deferred

W6 remaining SPs and carry-overs NOT addressed by this sub-project:

- **W6 #5 UI polish** (star ASCII→sprite, locale unification) — separate SP.
- **W6 #6 2nd-device test** — separate SP.
- **SP3 carry-over:** hold-note audio feedback — separate SP.
- **SP4 carry-overs:** (a) `AudioSource.Play()` per-tap 1.6 KB allocation via `SoundHandle.CreateChannel`, (b) `SceneBuilder`↔`W6SamplesWireup` coupling fold-in, (c) APK +2 MB unaccounted delta, (d) APK filename bump, (e) `SettingsScreen.creditsLabel` wireup relocation — each a separate SP candidate.

## 9. Done criteria

- [ ] `CalibrationClickBuilder.Build()` produces deterministic 1920-byte-payload WAV.
- [ ] `calibration_click.wav` + `.meta` committed.
- [ ] `SceneBuilder` passes `calibration_click.wav` into `BuildCalibrationOverlay`; null-guarded.
- [ ] EditMode: 131-132 tests, all green.
- [ ] Galaxy S22 checklist §6.3 all 5 items pass.
- [ ] Completion report `docs/superpowers/reports/2026-04-22-w6-sp5-calibration-click-completion.md` committed.
- [ ] Memory: W4 carry-over #2 marked resolved; new W6 SP5 memo added.

## 10. References

- v2 spec §9 (W6 weekly goal: 폴리싱 + 사운드): `docs/superpowers/specs/2026-04-20-keyflow-mvp-v2-4lane-design.md`
- W3 completion (original carry-over): `docs/superpowers/reports/2026-04-20-w3-completion.md` §"Known limitations" #4
- SP4 completion (most recent W6 sibling, sets expectations for this SP's rigor): `docs/superpowers/reports/2026-04-22-w6-sp4-completion.md`
- SP1 merged memo — tap sound routing (why click must be non-piano): `C:/Users/lhk/.claude/projects/C--dev-unity-music/memory/project_w6_sp1_complete.md`
- `CalibrationController.cs` runtime: `Assets/Scripts/Calibration/CalibrationController.cs`
- `SceneBuilder.cs` build entry: `Assets/Editor/SceneBuilder.cs` (lines 45-73 are the relevant region)
