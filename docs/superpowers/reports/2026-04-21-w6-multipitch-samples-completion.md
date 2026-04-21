# W6 Sub-Project 1 Completion Report — Multi-Pitch Piano Samples

**Date:** 2026-04-21
**Branch:** `claude/ecstatic-franklin-a7436a` (worktree — merge to `main` pending)
**HEAD:** `f3c5326`
**APK:** `Builds/keyflow-w6.apk` (33.54 MB, +0.39 MB vs W5)

## Scope delivered

- **17 Salamander Grand Piano V3 samples** (minor-third intervals, MIDI 36–84, v10 velocity layer) imported at `Assets/Audio/piano/` with `PianoSampleImportPostprocessor` enforcing Force Mono + Vorbis Q60 + DecompressOnLoad + 48 kHz override.
- **`AudioSamplePool.PlayForPitch(int midiPitch)`** — pure-static `ResolveSample(midi, map, baseMidi, step)` maps a MIDI pitch to `(clip, pitchRatio)` via nearest sampled key + `2^(semitones/12)` ratio for ±1 semitone shift; the instance method wraps it with round-robin AudioSource selection and `src.Play()` replace semantics (not layered PlayOneShot — see Deviations).
- **`JudgmentSystem.GetClosestPendingPitch(lane, tapTimeMs, windowMs)`** — read-only query over the pending note list, reusing the same lane/closest-delta scan as `HandleTap` but with a window filter. Returns -1 when no pending note is within `windowMs` of the tap.
- **`TapInputHandler.PlayTapSound(lane, songTimeMs)`** — called synchronously from `FirePress`/`FirePressRaw` BEFORE `OnLaneTap` is invoked (audio-first ordering preserved for latency). Queries `JudgmentSystem.GetClosestPendingPitch`; falls back to `LanePitches.Default(lane)` when out-of-window.
- **`LanePitches.Default(lane)`** — static perfect-5th staircase map `{C3=48, G3=55, C4=60, G4=67}` with middle-C fallback for out-of-range lanes.
- **`ChartNote.pitch`** (already parsed by W5's ChartLoader) threaded through `NoteController.Initialize(..., int pitch, ...)` → `NoteController.Pitch` property → `JudgmentSystem.GetClosestPendingPitch` return value.
- **`W6SamplesWireup` editor script** (`KeyFlow/W6 Samples Wireup` menu + `-executeMethod` CLI) — idempotent one-off that loads the 17 clips in MIDI order, populates `AudioSamplePool.pitchSamples`/`baseMidi=36`/`stepSemitones=3`, points `TapInputHandler.judgmentSystem` at the scene's `JudgmentSystem`, and creates a bottom-anchored `CreditsLabel` Text under `SettingsScreen`.
- **CC-BY 3.0 attribution** double-covered: `Assets/StreamingAssets/licenses/salamander-piano-v3.txt` bundles the original Salamander README verbatim; `SettingsScreen` surfaces `UIStrings.CreditsSamples` ("Piano samples: Salamander Grand Piano V3 by Alexander Holm, CC-BY 3.0").
- **Parent MVP spec `docs/superpowers/specs/2026-04-19-keyflow-mvp-design.md` §2/§6.2/§11.2/§14** — all four CC0 references scrubbed to CC-BY 3.0 + Alexander Holm.
- **Voice-exhaustion bug discovered during device playtest** — `src.PlayOneShot(clip)` layered voices on each AudioSource (W6 initial implementation); with 44-second piano release samples, rapid taps saturated Unity's 32 real-voice budget and new taps went inaudible. Fixed by switching to `src.clip = clip; src.Play()` which replaces prior playback on the same source. Pool of 16 round-robin sources = max 16 concurrent voices, well under the Android real-voice limit. Commit `f3c5326` includes a regression-guard test (`PlayForPitch_AssignsClipAndPitchToSource_NotLayered`).

## Test counts

- **Python pytest**: unchanged from W5 — 32 / 32 pass. W6 did not touch `tools/midi_to_kfchart/`.
- **Unity EditMode**: 112 / 112 pass (was 93 at W5 end → +19 new tests):
  - +1 `NoteControllerTests.Initialize_PersistsPitch`
  - +6 `LanePitchesTests.Default_*` (lane 0–3, negative, out-of-range)
  - +6 `AudioSamplePoolTests.ResolveSample_*` (exact, +1 semitone, offset==2 cross-down, low/high clamp, empty map)
  - +1 `AudioSamplePoolTests.PlayForPitch_AssignsClipAndPitchToSource_NotLayered` (regression guard for voice-exhaustion bug)
  - +5 `JudgmentSystemTests.GetClosestPendingPitch_*` (in-window, out-of-window, wrong-lane, temporally-nearest, empty-pending)

## Commits (oldest → newest)

```
30053a3 chore(w6): bundle Salamander CC-BY license + correct parent spec §11.2
c6d2ffd docs(w6): scrub remaining CC0 references from MVP spec
f657e95 feat(w6): Settings Credits label for Salamander CC-BY attribution
9134405 feat(w6): import 17 Salamander v10 piano samples
d614fd1 feat(w6): thread ChartNote.pitch to NoteController
ae53a4a feat(w6): LanePitches fallback map for wrong-tap audio
b9383db feat(w6): AudioSamplePool.ResolveSample + PlayForPitch
f43a75e feat(w6): JudgmentSystem.GetClosestPendingPitch query
17f4a10 feat(w6): TapInputHandler routes taps through PlayForPitch
d7bf6ec chore(w6): editor script wires scene refs for pitch samples
8e92c6d chore(w6): rename APK artifact to keyflow-w6.apk
f3c5326 fix(w6): bound voice count in PlayForPitch via src.Play() replace
```

Preceded by 3 docs commits on the branch that defined the work:
```
89400c2 docs(w6): multi-pitch piano samples sub-project design
c875f64 docs(w6): clean up Appendix A per spec review feedback
8a86349 docs(w6): implementation plan for multi-pitch piano samples
b8199ec docs(w6): tighten Task 10 APK rename per plan review
```

## Deviations from plan

- **Task 3 — Unity 6 API adjustments in PianoSampleImportPostprocessor.** `AudioImporter.preloadAudioData` is obsolete in Unity 6; setting moved to `AudioImporterSampleSettings.preloadAudioData`. Also added explicit `using UnityEngine;` for `AudioClipLoadType`/`AudioCompressionFormat`/`AudioSampleRateSetting`. The implementer made these API-version fixes during execution — not a design change.

- **Task 3 — `ForceReimportPianoSamples` helper added beyond plan.** `-runTests` invocation alone did not fully serialize the AudioImporter settings into `.meta` files; a separate `-executeMethod KeyFlow.Editor.PianoSampleImportPostprocessor.ForceReimportPianoSamples` was required to flush them. The helper is ~14 lines, editor-only, and resides in the same file as the postprocessor. Reviewer judged it KEEP-in-place given empirical necessity, cohesive location, and reproducibility value for fresh checkouts. Not in the plan, but not scope creep per reviewer — it solves a demonstrated Task 3 execution problem.

- **Task 6 — `ResolveSample` made pure-static.** Plan's original §4.1 had it as an instance method. The implementer (following the plan's guidance at Step 3) extracted it to `public static (AudioClip clip, float pitchRatio) ResolveSample(int midiPitch, AudioClip[] pitchSamples, int baseMidi, int stepSemitones)`. This makes it unit-testable with dummy `AudioClip[]` injection (no Unity MonoBehaviour required). `PlayForPitch` is the instance wrapper that reads the SerializeField array and delegates. Cleaner than plan; spec reviewer approved.

- **Task 7 — `Difficulty.NORMAL` in plan code template corrected to `Difficulty.Normal` in the test file.** The plan's test template used the enum name with uppercase, but the codebase's `Difficulty` enum uses PascalCase (`Difficulty.Normal`). The implementer correctly adapted. Plan document line 819 still has the stale spelling — noted as a Minor follow-up.

- **Device playtest (Task 10) uncovered a voice-exhaustion bug not anticipated by the spec.** The initial W6 implementation used `src.PlayOneShot(clip)` (layered) in `PlayForPitch`, consistent with the spec §4.1 code snippet. On rapid taps with long Salamander release samples (~44 s each), Unity's 32 real-voice budget saturated, producing transient silence. Diagnosed via the systematic-debugging skill, fixed in commit `f3c5326` by switching to `src.clip = clip; src.Play()`. Regression-guard unit test added. Spec §8's "AudioSource.pitch glitch on device" risk did not materialize, but this was a distinct and more fundamental issue — worth updating the spec if it were ever revisited.

## Operational findings

- **Unity 6 IL2CPP batch build is flaky on fresh `Library/Bee` state.** First APK build attempt failed at step 1093/1104 with `IPCStream (Upm-32052): IPC stream failed to read (Not connected)` after 557 seconds of compilation. Second attempt (no code changes, no process cleanup) succeeded cleanly. Likely transient — Unity Package Manager daemon or IL2CPP backend IPC issue. For future runs: if recurrence becomes chronic, consider closing Unity Hub / clearing `Library/PackageCache` before batch builds.

- **`-runTests` vs `-executeMethod` `-quit` flag rules hold** (carried from W5 memory): `-runTests` MUST omit `-quit` (else test runner silently skips); `-executeMethod` MUST include `-quit` (else batch process never exits). Both must run foreground, never with `run_in_background=true` or piped stdout.

## Device validation — **DONE**

APK `Builds/keyflow-w6.apk` installed on Galaxy S22 (`R5CT21A31QB`) via `adb install -r`. After the voice-exhaustion bugfix, user walk-through confirmed:

- [x] App launches, no ANR.
- [x] Für Elise Easy/Normal both complete end-to-end, with varied piano pitches matching the chart melody.
- [x] Empty-lane taps play a lane-default pitch (C3/G3/C4/G4 staircase).
- [x] Settings screen displays the Credits line.
- [x] **Rapid taps no longer produce silent gaps** (voice-exhaustion bug resolved).
- [x] No perceptible tap-to-sound latency regression vs W5.
- [x] Calibration flow unchanged (still uses `piano_c4.wav`).

User confirmation: "잘된다" (works well).

## W5 user-feedback resolution

W5 sign-off flagged: *"타격음이 한 종류라 게임하는 느낌이 안 든다"* ("only one tap sound — doesn't feel like playing a game"). **Resolved.** W6 ships 17 distinct pitch samples routed by each tapped note's chart pitch; the melody of the song is heard through the user's taps themselves.

## Carry-over items from this W6 cycle

Spawned as separate follow-up tasks via `spawn_task` during the final review:

1. **Fix `PlayOneShot` fallback silent-regression** — `PlayForPitch`'s fallback path (when `pitchSamples` unconfigured) still uses layered `src.PlayOneShot(clip ?? defaultClip)`. Add a warning log + convert to `src.Play()` for consistency. Prevents silent re-introduction of the voice-exhaustion bug on a fresh worktree before wireup runs.
2. **Document bidirectional `TapInputHandler` ↔ `JudgmentSystem` references** — one-line comments on each serialized field explaining intent (no initialization-order dependency).
3. **Fix `ApkBuilder` misleading size log** — logs `report.summary.totalSize` (658 MB all-artifacts) instead of `FileInfo(apk).Length` (33.54 MB actual APK). Cosmetic but confusing in CI logs.

Minor items noted by final code review, not spawned:
- Dead `sampleMidi` reassignment in `ResolveSample` `offset==2` branch (compiler-eliminated).
- `AudioImporter.normalize = false` not set (Unity default `true` applies; musically harmless for tap SFX).
- Plan document line 819 has stale `Difficulty.NORMAL` spelling.
- Salamander README first line says "V2" (upstream-document quirk).

## Next steps → W6 priorities 2–6

From the W5 completion report's remaining W6 scope, in declared priority order:

2. **Content: remaining 4 songs** — Ode to Joy, Canon in D, Clair de Lune, The Entertainer × 2 difficulties each. Feed MIDIs through `tools/midi_to_kfchart/midi_to_kfchart.py --batch batch.yaml` (pipeline already shipped in W5). **Next sub-project after W6-1 merges.**
3. Profiler pass — mid-game tap drops (W4 carry-over #1).
4. Calibration click sample — dedicated sample replacing `piano_c4.wav` reuse (W4 carry-over #2).
5. UI polish — star sprites (W4 carry-over #4 if still open), chart-load error toast.
6. Second device — mid-tier Android before Internal Testing distribution.

BGM remains out-of-scope per v2 pivot; post-MVP only.
