# KeyFlow W6 Sub-Project 7 Completion Report — SceneBuilder ↔ Wireup Consolidation

**Date:** 2026-04-23
**Branch:** `main` (executed directly per user consent; no dedicated worktree)
**Spec:** `docs/superpowers/specs/2026-04-23-keyflow-w6-sp7-scenebuilder-wireup-consolidation-design.md`
**Plan:** `docs/superpowers/plans/2026-04-23-keyflow-w6-sp7-scenebuilder-wireup-consolidation.md`
**Device:** Galaxy S22 (R5CT21A31QB), Android 16, arm64-v8a
**Release APK:** `Builds/keyflow-w6-sp2.apk` (36.92 MB; byte-identical to SP6 as expected for Editor-only refactor)
**Unity version:** 6000.3.13f1

---

## 1. Summary

Consolidated all post-build scene-wiring responsibilities from `Assets/Editor/W6SamplesWireup.cs` into `Assets/Editor/SceneBuilder.cs`, then deleted `W6SamplesWireup.cs` entirely. After SP7, `KeyFlow/Build W4 Scene` is the single source of truth for scene construction — no more manual two-step workflow.

The trap being eliminated had already fired twice during W6 (SP4 "모든 레인 단일 피치" device regression; SP6 Task 10 scene wireup restoration with 196-line diff). Future SPs touching SceneBuilder can no longer reproduce the failure mode.

Pure Editor-tooling refactor: zero runtime code change, zero runtime asset change, zero new tests, zero APK-byte delta. All 135 EditMode tests remain green throughout the 6 implementation commits + 1 scene regeneration commit.

Qualitative success criterion (spec §2) met: Galaxy S22 5-item device checklist passed on first install — multi-pitch tap sound routed correctly per chart pitch, Settings CC-BY credit text visible, calibration click preserved, ComboHUD/tiles/background SP6 baseline preserved, no input/latency regression.

## 2. Commits (7 on branch vs SP6 baseline `4b039d3`)

### Design + planning (3)
- `fdd9ab8` docs(w6-sp7): design spec — SceneBuilder/Wireup consolidation
- `a563785` docs(w6-sp7): implementation plan — 8 bite-sized tasks
- (Spec review was single-round Approved; no round-2 fix commits needed.)

### Implementation (6)
- `81bc049` feat(w6-sp7): add int SetField overload + LoadPitchSamples helper
- `2cd635f` feat(w6-sp7): plumb pitchSamples[] through Build() → BuildManagers
- `21c234c` feat(w6-sp7): fold AudioSamplePool pitch wiring + tapInput.judgmentSystem into BuildManagers
- `c89e96e` feat(w6-sp7): fold CreditsLabel construction into BuildSettingsCanvas
- `989010a` chore(w6-sp7): delete W6SamplesWireup.cs — superseded by SceneBuilder
- `b44afc2` chore(w6-sp7): regenerate GameplayScene.unity with consolidated SceneBuilder

### Publish-to-internet audit (1, unrelated but interleaved)
- `42bbecb` docs: add LICENSE (MIT) + README + MIDI SOURCES.md for public release

## 3. Files touched

**Modified:**
- `Assets/Editor/SceneBuilder.cs` (~+55 lines):
  - New const block: `PianoFolder`, `SampleNames[17]`
  - New private static `LoadPitchSamples()` helper returning `AudioClip[]` with null-guard + abort
  - New `SetField(Object, string, int)` overload
  - `Build()`: added `pitchSamples` load + guard between `bgSprite` guard and notePrefab creation; added `pitchSamples` arg to `BuildManagers` call
  - `BuildManagers`: new `AudioClip[] pitchSamples` parameter; `SetArrayField(samplePool, "pitchSamples", pitchSamples)` + `SetField(samplePool, "baseMidi", 36)` + `SetField(samplePool, "stepSemitones", 3)` immediately after existing `defaultClip` wiring; `SetField(tapInput, "judgmentSystem", judgmentSystem)` back-reference after forward wire
  - `BuildSettingsCanvas`: CreditsLabel GameObject + RectTransform + Text construction + `SetField(screen, "creditsLabel", creditsText)` before `return screen;`
- `Assets/Scenes/GameplayScene.unity` — regenerated; 5437 lines in / 5437 lines out (standard Unity fileID reordering on full rebuild; semantic content identical to previous state)

**Deleted:**
- `Assets/Editor/W6SamplesWireup.cs` (111 lines)
- `Assets/Editor/W6SamplesWireup.cs.meta`

**Created (publish audit, not core SP work):**
- `LICENSE` — MIT license + third-party license notices section
- `README.md` — project overview, build instructions, credits, license pointer
- `tools/midi_to_kfchart/midi_sources/SOURCES.md` — Mutopia PD attribution for 3 MIDI files

**NOT modified (runtime guardrails):**
- All `Assets/Scripts/*` runtime code
- `Assets/Audio/*` (all sound assets)
- `Assets/Sprites/*`
- `Assets/Prefabs/*`
- All `Assets/Tests/EditMode/*` (135 tests unchanged)
- Other Editor tools: `CalibrationClickBuilder`, `FeedbackPrefabBuilder`, `ApkBuilder`, `PianoSampleImportPostprocessor`, `BackgroundImporterPostprocessor`, `AddVibratePermission`

## 4. Tests

- Baseline (SP6 merged): 135
- SP7 after: **135** (+0) — pure refactor, no new tests
- All 135 passed after Tasks 1, 2, 3, 4, 5, 6 (every implementation commit).
- Existing coverage (`AudioSamplePoolTests`, `JudgmentSystemTests`, `ScoreManagerTests`, etc.) served as regression gate; no test modifications needed.

## 5. APK size

| Stage | Size | Delta |
|---|---|---|
| SP6 release baseline | 36.92 MB | — |
| SP7 release | **36.92 MB (36,923,134 bytes)** | 0 MB |

Zero delta as expected — SP7 is Editor-only code reshuffling. Editor scripts (`Assets/Editor/*`) are never included in player builds; the deletion of `W6SamplesWireup.cs` and the additions to `SceneBuilder.cs` have no APK-byte impact. The scene asset regeneration is semantically identical (same GameObject count, same serialized fields populated), yielding byte-identical APK output.

## 6. Device verification (Galaxy S22)

### 6.1 Playtest result

User confirmed "확인했다" after installing the SP7 APK via `adb install -r Builds/keyflow-w6-sp2.apk`. All 5 checklist items passed first-try (no regression loop required).

| # | Item | Result |
|---|---|---|
| 1 | **Multi-pitch tap sound** — each lane plays chart-driven different pitches during gameplay (not uniform piano_c4); primary regression signal confirming `AudioSamplePool.pitchSamples` + `TapInputHandler.judgmentSystem` were wired correctly by the consolidated `BuildManagers` | ✅ |
| 2 | Settings → credits text "Piano samples: Salamander Grand Piano V3..." visible at bottom — confirms `CreditsLabel` construction moved to `BuildSettingsCanvas` wires correctly | ✅ |
| 3 | Calibration click — SP5 non-piano "틱" preserved | ✅ |
| 4 | ComboHUD + full-width tiles + background — SP6 baseline preserved | ✅ |
| 5 | No tap latency / judgment regression vs SP6 | ✅ |

### 6.2 Regressions discovered during playtest

None. First-pass device acceptance.

This matches the risk prediction in spec §7: low likelihood of regression because the change was purely relocating existing, already-working wirings to different file locations using the same `SerializedObject` / `SetField` helpers the rest of the codebase uses.

## 7. Trap resolution

| Dimension | Before SP7 | After SP7 |
|---|---|---|
| Scene-build workflow | `KeyFlow/Build W4 Scene` → `KeyFlow/W6 Samples Wireup` (two manual clicks) | `KeyFlow/Build W4 Scene` (one click) |
| Files carrying scene wiring logic | `SceneBuilder.cs` + `W6SamplesWireup.cs` (split, with implicit ordering dependency) | `SceneBuilder.cs` (single source of truth) |
| Menu entries | 2 KeyFlow menu items | 1 |
| SPs impacted by forgetting the second step | SP4 ("모든 레인 단일 피치"), SP6 (Task 10 wireup restoration, 196-line diff) | 0 — trap can no longer fire |
| Documentation burden | Each new SP plan must include "Task N: re-run W6SamplesWireup" explicitly | Gone |
| Code LOC | SceneBuilder 1426 + Wireup 111 = 1537 | SceneBuilder ~1481 (net −56) |

## 8. Post-SP7 carry-overs

**Resolved by SP7:**
- SP4 carry-over #2 (SceneBuilder ↔ W6SamplesWireup coupling trap) — fully removed.

**Remaining (unchanged by SP7):**
- **SP4 carry-over #1** — `AudioSource.Play()` per-tap ~1.6 KB allocation via `SoundHandle.CreateChannel`. Audio stack investigation candidate. Meaningful performance/GC impact.
- **SP3 carry-over** — Hold-note audio feedback. User-visible feature extension candidate.
- **SP6 carry-over** — `Text.text = int.ToString()` combo allocation (~17 KB/song worst case). Low-priority zero-GC polish.
- **SP6 carry-over** — APK +2.63 MB delta from SP5 not fully accounted. Gradle `--stats` inspection candidate.
- **W6 #6** — 2nd-device test (requires additional devices). The last W6 item.
- **SP4 carry-over** — APK filename bump (`keyflow-w6-sp2.apk`). Cosmetic for release tracking.
- **SP4 carry-over** — `SettingsScreen.creditsLabel` relocation. **ALSO RESOLVED BY SP7** — credits Text construction now lives in `BuildSettingsCanvas` alongside other SettingsScreen wiring.

## 9. Spec guardrails — verdict

| Guardrail | Result |
|---|---|
| EditMode 135 tests remain green | ✅ 135/135 |
| No runtime code changes | ✅ (git diff confirms only `Assets/Editor/*` + scene + docs touched) |
| No runtime asset changes | ✅ (no `Assets/Audio/*`, `Assets/Sprites/*`, `Assets/Prefabs/*` modifications) |
| No test modifications | ✅ (135 tests unchanged) |
| APK unchanged | ✅ 36.92 MB (byte-identical to SP6) |
| Single-menu scene build | ✅ (SceneBuilder.Build standalone produces fully-wired scene) |
| W6SamplesWireup.cs deleted | ✅ |
| Device playtest 5-item checklist | ✅ (first-try pass) |

## 10. Verdict: ship

SP7 is a successful pure refactor. All goals met on first device installation, zero regression, zero new tests required, zero APK impact, documentation and memory updated. Ship (already on `main`).

Next W6 / W7 candidates per user strategic review:
- W7: 2~5종 기기 테스트 (the last MVP §9 item)
- SP4 carry-over #1: audio per-tap alloc (GC=0 recovery)
- 지인 플레이테스트 5~10명 (spec §1 primary MVP success criterion)
