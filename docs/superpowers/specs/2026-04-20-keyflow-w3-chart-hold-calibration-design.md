# KeyFlow W3 Design: Chart Loader + Hold Notes + Calibration + First Song

> **Status:** Draft pending review (2026-04-20)
> **Scope:** Week 3 of the KeyFlow MVP v2 schedule
> **Parent spec:** [2026-04-20-keyflow-mvp-v2-4lane-design.md](./2026-04-20-keyflow-mvp-v2-4lane-design.md)
> **Predecessor report:** [2026-04-20-w2-completion.md](../reports/2026-04-20-w2-completion.md)
> **Developer profile:** Senior full-stack web developer, first Unity/game project

---

## 0. Summary

W3 turns W2's hardcoded-sequence judgment engine into a **data-driven song player with Hold notes and audio calibration**, delivering a build in which **Für Elise Easy is completable end-to-end on a Galaxy S22** device. Four features are added: `.kfchart` JSON loader, Hold note semantics, first-launch calibration flow, and a completion panel with Restart. Everything else (UI screens, Results screen, MIDI pipeline, 48-key sample bundle) stays deferred to W4+.

**W3 completion definition:** First-launch calibration → Für Elise Easy plays start-to-finish → completion panel → Restart → repeatable, on device, without crashes.

---

## 1. Goals and Non-Goals

### 1.1 In scope for W3

- `.kfchart` JSON loader with schema validation
- First chart authored by hand: `beethoven_fur_elise.kfchart` (Easy, ~60–100 notes, first 30–50 seconds of the piece)
- Hold note support, judgment model **C** (start-tap judgment + Break-on-release-before-end)
- Calibration MVP, method **A** (metronome tap, 8 clicks, median offset)
- Completion panel with score + stars + Restart button
- Single `GameplayScene` with Calibration/Completion overlays
- EditMode test count grows from 40 → ~65
- Galaxy S22 device verification + completion report

### 1.2 Out of scope (deferred)

- Settings screen with Calibration re-run button → **W4**
- Splash / Main / Song list scenes → **W4**
- Full Results screen with star animations, retry-vs-home choice → **W4** (W3 CompletionPanel is a minimal stand-in)
- Python MIDI → .kfchart pipeline → **W5**
- Remaining 4 songs (Ode to Joy, Canon in D, Clair de Lune, The Entertainer) → **W5**
- 48-key Salamander sample bundle → **W4 or W5** (W3 keeps W1's single C4 sample + `AudioSource.pitch` shift)
- Normal difficulty chart (only Easy shipped in W3) → **W5**
- Particles, haptics → **W6**

### 1.3 Explicit non-decisions (hardcoded for W3, parameterized later)

- Difficulty stays hardcoded to `Easy` in the chart-load path. `NoteSpawner.difficulty` is the lever; no selection UI yet.
- Song is hardcoded to `beethoven_fur_elise`. No catalog or picker.
- Calibration re-run path for W3: delete `PlayerPrefs` key manually (debug) or wipe app data. No in-game button yet.

---

## 2. Key Design Decisions

Four decisions were resolved during brainstorming (2026-04-20). Each is load-bearing for later work — changing them costs schema migrations, logic rewrites, or UX reshoots.

### 2.1 Hold judgment model: **C — Start tap + Break on release before end**

Tapping the Hold's start earns a normal P/G/G/M judgment (same windows as Tap). While the finger stays on the lane through `t + dur`, the Hold is `COMPLETED` — no extra release-timing judgment, no additional score. If the finger leaves the lane before `t + dur`, the Hold becomes `BROKEN`: combo resets, Miss count increments by one. The start-tap score already granted is **not** revoked.

**Rationale:** Piano Tiles–style casual pacing + ~110ms device latency make release-timing precision frustrating. Model A (start-only, no release check) makes Hold visually distinct but mechanically identical to a long Tap — no reason to author them. Model B (full release judgment) doubles test surface and input-tracking complexity for a feature the target player cannot reliably execute at this latency. Model C keeps "you must actually hold it" as a real mechanic while cutting the release-window judgment code path.

### 2.2 Multi-pitch audio: **A — keep W1's single C4 sample + `AudioSource.pitch` shift**

`AudioSamplePool.PlayPitch(int midi)` keeps its current shape but internally still routes to the one C4 sample with `pitch = Mathf.Pow(2f, (midi - 60) / 12f)`. First chart is constrained to MIDI 48–72 so shift stays within ±1 octave, preserving tone quality.

**Rationale:** W3 is already carrying four features. Adding a 48-key asset pipeline (download Salamander V3, convert 48 WAVs → OGGs, name convention, runtime loader, APK size check) is a full day of work on an orthogonal axis and repeats the W1 "audio path is a schedule killer" lesson. The loader *interface* is kept sample-set-ready, so W4/W5 can swap the internals by dropping files + changing one lookup, no API change.

### 2.3 Calibration UX: **A — metronome tap, 8 clicks at 500ms intervals**

`CalibrationController` MonoBehaviour drives a one-screen overlay. `AudioSource.PlayScheduled` plays 8 clicks at `dspTime + 2.0 + 0.5 * i`. For each touch-down, `AudioSettings.dspTime` is captured. After the 8th click + 500ms tail, `CalibrationCalculator` (pure static class) computes `offsetMs = round(median(taps - expected) * 1000)` and a `madMs` confidence metric. If `madMs > 50`, the run is marked unreliable and the user is asked to retry. After three unreliable runs, offset=0 is saved to prevent first-run lockout.

**Rationale:** Metronome tap is the most-decoupled form — it does not touch `NoteController` / `JudgmentEvaluator`, so calibration bugs do not manifest as gameplay bugs and vice versa. `CalibrationCalculator` is a pure function, directly testable in EditMode without Unity runtime. When W5 polishes the UX with animations or a progress bar, the calculator is unchanged — the contract is `(expected[], taps[]) → int`.

### 2.4 End-of-song behavior: **B — Completion overlay with Restart button**

When `songTime >= lastNote.t + lastNote.dur + MissWindow` and `activeNotes.Count == 0`, a semi-transparent panel fades in over the gameplay view: "🏁 SONG COMPLETE" + final Score, Max Combo, P/G/G/M counts, star count (0–3 per Spec §5.3). One button: `[Restart]`, which calls `SceneManager.LoadScene(activeScene.buildIndex)`. The Android Back button is bound to the same action.

**Rationale:** W2 established the precedent that every week ends with Galaxy S22 device playtest. Without a Restart button, device iteration means re-launching the APK between each playtest — slow and error-prone. The panel is ~30 lines of UI code and, when W4's real Results scene lands, this panel swaps out for a `SceneManager.LoadScene("ResultsScene")` call with no other code changes.

---

## 3. Data Model

### 3.1 `.kfchart` file format

Schema is inherited verbatim from [Spec v2 §7](./2026-04-20-keyflow-mvp-v2-4lane-design.md#7-곡-차트-포맷-kfchart-v2). W3 does not change the schema.

Concrete file for W3: `Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart`

```json
{
  "songId": "beethoven_fur_elise",
  "title": "Für Elise",
  "composer": "Beethoven",
  "bpm": 120,
  "durationMs": 45000,
  "charts": {
    "EASY": {
      "totalNotes": 72,
      "notes": [
        {"t": 2000, "lane": 2, "pitch": 64, "type": "TAP",  "dur": 0},
        {"t": 2250, "lane": 2, "pitch": 63, "type": "TAP",  "dur": 0},
        ...
        {"t": 43000, "lane": 1, "pitch": 60, "type": "HOLD", "dur": 1500}
      ]
    }
  }
}
```

- W3 ships **only** the `EASY` chart. `NORMAL` is absent; loader tolerates absence (empty or missing key).
- 2–4 Hold notes distributed for verification; remainder are Tap.
- Pitches clamped to MIDI 48–72 (Q2.2 rationale).
- `durationMs` reflects hand-authored length (~45s is sufficient for W3 verification; full 2:00 Für Elise is W5 polish).

### 3.2 Runtime types

```csharp
public enum NoteType { TAP, HOLD }

[Serializable]
public class ChartNote {
    public int t;        // song-time ms
    public int lane;     // 0..3
    public int pitch;    // MIDI, expected 48..72 in W3
    public NoteType type;
    public int dur;      // ms, 0 for TAP, >0 for HOLD
}

[Serializable]
public class ChartDifficulty {
    public int totalNotes;
    public List<ChartNote> notes;
}

[Serializable]
public class ChartData {
    public string songId;
    public string title;
    public string composer;
    public int bpm;
    public int durationMs;
    public Dictionary<Difficulty, ChartDifficulty> charts;
}
```

### 3.3 `ChartLoader`

```csharp
public static class ChartLoader {
    public static ChartData LoadFromStreamingAssets(string songId);
    public static ChartData ParseJson(string json);  // pure, EditMode-testable
}

public class ChartValidationException : Exception { ... }
```

Parser choice: **Newtonsoft.Json** (`com.unity.nuget.newtonsoft-json`, verify presence before implementation — add via Package Manager if absent). Unity's built-in `JsonUtility` cannot deserialize `Dictionary<Difficulty, ChartDifficulty>`, and hand-rolling a dict wrapper is more fragile than adding the package.

`ParseJson` is a **pure function** consumed by EditMode tests with inline JSON strings. `LoadFromStreamingAssets` wraps it with `System.IO.File.ReadAllText` for runtime.

### 3.4 Schema validation

`ParseJson` rejects with `ChartValidationException` when:
- `lane` ∉ [0, 3]
- `type` ∉ {`TAP`, `HOLD`}
- `dur < 0`
- `dur > 0` on a `TAP` note
- `dur == 0` on a `HOLD` note
- `t < 0` or `t > durationMs`
- `pitch` clamp policy: **warn but continue** via `Mathf.Clamp(pitch, 36, 83)` — matches Spec v2 §7 "범위 밖의 원본 MIDI 노트는 가장 가까운 옥타브로 이동" policy. Validation only fails on structural errors, not pitch-range ones.

---

## 4. Feature Implementation

### 4.1 Hold notes (judgment model C)

**State machine per Hold note:**

```
SPAWNED ──(start tap in judgment window)──> HOLDING
   │                                           │
   │                                           ├─(finger held through t+dur)──> COMPLETED
   │                                           │
   │                                           └─(finger left lane before t+dur)──> BROKEN
   │
   └─(judgment window passed, never tapped)──> MISSED
```

**Components:**

- **`TapInputHandler` extension:** adds `OnLaneRelease(int laneIdx)` event and `bool IsLanePressed(int laneIdx)` query. Internally tracks `Dictionary<int touchId, int laneIdx>` on press, fires release when the touch ends. The existing `OnLaneTap` event is unchanged.
- **`HoldTracker` (new MonoBehaviour):** maintains a list of currently HOLDING notes. Each frame:
  - `songTime >= note.t + note.dur` → transition to COMPLETED, no extra score.
  - `!tapInput.IsLanePressed(note.lane)` → transition to BROKEN, call `scoreManager.OnMiss()`, trigger visual fade on the note.
- **`JudgmentSystem` extension:** when `JudgmentEvaluator` accepts a start tap on a `HOLD` note, it registers the note with `HoldTracker` instead of immediately despawning.
- **`NoteController` visual:** Hold notes render as an elongated capsule sized to `dur` (height in world units = `dur * fallSpeed`). While HOLDING, color intensifies. On BROKEN, fades to gray over 200ms.

**Scoring policy:**
- Start-tap Miss → Hold overall Miss. Whole note missed.
- BROKEN → combo resets, Miss count +1 in `JudgmentCounts`. The P/G/G score from the successful start tap is **kept** (no clawback).
- Successful start-tap + immediate release (press and release within one frame): `IsLanePressed` reads false next frame, so Hold goes BROKEN on the following frame. Acceptable — device playtest will validate feel.

**Testable seam:** `HoldTracker` logic is split into a pure `HoldStateMachine` class (manages transitions given `(currentSongTime, noteDataList, pressedLanes)`) that the MonoBehaviour wraps. Tests drive the state machine directly.

### 4.2 Calibration (method A)

**Flow:**

1. GameplayScene `Start()` checks `PlayerPrefs.HasKey("CalibOffsetMs")`.
2. If absent, `CalibrationController` activates its overlay and disables the main gameplay controller.
3. Overlay shows prompt: "화면 아무 곳이나, 클릭 소리에 맞춰 8번 탭하세요." + big Start button.
4. On Start press, 8 clicks are scheduled at `dspTime + 2.0 + 0.5 * i` (i = 0..7) via `AudioSource.PlayScheduled`. A small visual flash accompanies each click for additional cueing.
5. Each touch-down captures `AudioSettings.dspTime` into `tapDspTimes[]`.
6. After the 8th click + 500ms tail, `CalibrationCalculator.Compute(expectedDspTimes, tapDspTimes)` runs.
7. If `reliable == true` → save `PlayerPrefs.SetInt("CalibOffsetMs", result.offsetMs)`, inject into `AudioSyncManager.CalibrationOffsetSec`, deactivate overlay, enable gameplay controller.
8. If `reliable == false` → show "결과가 흔들려요. 다시 해보세요." + retry button. On third unreliable run, save offset=0 and proceed.

**`CalibrationCalculator` contract:**

```csharp
public static class CalibrationCalculator {
    public struct Result {
        public int offsetMs;      // clamped to [-500, 500]
        public int madMs;         // median absolute deviation
        public bool reliable;     // madMs <= 50
    }

    public static Result Compute(double[] expectedDspTimes, double[] tapDspTimes);
}
```

- Pairing: if `tapDspTimes.Length < expectedDspTimes.Length`, match each tap to its nearest-in-time expected click. Missing taps are skipped (do not contribute).
- Outlier handling: drop the highest and lowest delta before taking median when `taps.Length >= 6`.
- `madMs`: `median(|delta_i - median_delta|) * 1000`.

### 4.3 Completion panel (model B)

**Trigger condition:** every frame, after updating `songTime`:

```
if (songTime >= lastNote.t + lastNote.dur + NormalMissWindowMs
    && activeNotes.Count == 0
    && !completionShown) {
    ShowCompletionPanel();
    completionShown = true;
}
```

`NormalMissWindowMs = 180` (Spec §5.1).

**Panel contents:**
- Centered: "🏁 SONG COMPLETE"
- Score: `scoreManager.Score`
- Max Combo: `scoreManager.MaxCombo`
- Judgment counts: `Perfect / Great / Good / Miss` (from `JudgmentCounts`)
- Stars: 0–3, computed via Spec §5.3 thresholds (500k / 750k / 900k)
- One button: `[Restart]` → `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)`
- Android Back button (`Input.GetKeyDown(KeyCode.Escape)` or equivalent in new Input System) → same Restart action

**LatencyMeter HUD stays visible** through the completion state for debug observation.

### 4.4 Scene flow (single `GameplayScene`)

```
App Start
  ↓
GameplayScene.Start()
  ↓
has CalibOffsetMs in PlayerPrefs?
  │
  ├─ NO  → CalibrationOverlay.Show()
  │          ↓ (on completion)
  │        Save offset, hide overlay
  │
  └─ YES → apply saved offset
  ↓
GameplayController.Begin()
  ├─ ChartLoader loads beethoven_fur_elise.kfchart
  ├─ NoteSpawner receives ChartData.charts[Easy].notes
  ├─ AudioSyncManager.StartSilentSong() (dspTime anchor, no BGM)
  └─ gameplay loop (W2 judgment + W3 Hold tracking)
  ↓
(all notes processed + tail window passed)
  ↓
CompletionPanel.Show()
  ↓
[Restart] → scene reload
```

**`SceneBuilder` editor menu** gains `KeyFlow → Build W3 Gameplay Scene` to regenerate the scene with Calibration and Completion overlay GameObjects added.

---

## 5. W2 Code Reuse and Modifications

### 5.1 Reused without modification

- `GameTime` (time utilities)
- `AudioSyncManager` (dspTime anchor, `CalibrationOffsetSec` property already exists)
- `AudioSamplePool` (16-channel pool, pitch-shifted from C4)
- `LaneLayout` (lane math)
- `JudgmentEvaluator` (P/G/G/M threshold logic — Hold start taps use identical code path)
- `ScoreManager` (score/combo/stars/counts)
- `LatencyMeter` HUD (Score/Combo/LastJudgment display)

### 5.2 Modified

- **`NoteSpawner`:** replace hardcoded 30-note sequence with `ChartData`-driven spawning. Accept a `ChartData` reference at initialization; iterate `notes[]` and schedule spawns by `t - previewMs`.
- **`NoteController`:** add Hold rendering (elongated capsule, color states). Tap rendering unchanged.
- **`TapInputHandler`:** add `OnLaneRelease` event and `IsLanePressed(int)` query. Keep `OnLaneTap` compatible.
- **`SceneBuilder`:** add Calibration and Completion overlay creation; menu renamed to `Build W3 Gameplay Scene`. Regenerate `GameplayScene.unity` + any new prefabs + meta files.

### 5.3 New

- `ChartLoader` (static)
- `ChartData`, `ChartDifficulty`, `ChartNote`, `NoteType` types
- `HoldStateMachine` (pure logic) + `HoldTracker` (MonoBehaviour wrapper)
- `CalibrationCalculator` (pure static)
- `CalibrationController` (MonoBehaviour + overlay UI)
- `CompletionPanel` (MonoBehaviour + overlay UI)
- `GameplayController` (bootstrap + state orchestration — wires calibration → chart load → gameplay → completion)

### 5.4 No deletions

W2 code is all retained. `GameTime.PitchToX` was already removed in W2.

---

## 6. Test Plan

### 6.1 EditMode test increments

W2 baseline: **40 tests passing**. W3 adds approximately **25 tests**, target ~65 total.

| Area | New tests |
|---|---|
| `ChartLoader.ParseJson` — valid JSON, missing required fields, invalid lane, invalid type, negative dur, TAP with dur, HOLD with dur=0, multi-difficulty, pitch clamp | 6–8 |
| `HoldStateMachine` — SPAWNED→HOLDING→COMPLETED, BROKEN mid-hold, MISSED (no tap), multiple concurrent holds, release+retap edge | 7–9 |
| `CalibrationCalculator` — zero delta, constant +100ms, outlier tolerance, unreliable variance, missing taps, clamp to [-500, 500] | 6–7 |
| `JudgmentEvaluator` regression — Hold start tap behaves identically to Tap | 2 |
| `ScoreManager` Hold integration — start-tap score retained on BROKEN, Miss count increments | 3 |

### 6.2 PlayMode tests

None added. W2 policy stands: PlayMode tests are not part of this MVP. Device playtest covers integration.

### 6.3 Device verification checklist (Galaxy S22)

Per the W2 precedent, format matches [2026-04-20-w2-completion.md](../reports/2026-04-20-w2-completion.md):

- [ ] APK <40MB, cold start ≤3s
- [ ] First launch auto-shows calibration overlay; dismisses after 8 taps
- [ ] Second launch skips calibration (PlayerPrefs persistence)
- [ ] Für Elise Easy plays to completion
- [ ] Hold notes: start-tap + hold-through = COMPLETED; start-tap + early-release = BROKEN (combo resets, note fades gray)
- [ ] Hold notes: no tap = MISSED (combo resets)
- [ ] Completion panel shows final Score, MaxCombo, P/G/G/M, stars
- [ ] Restart button reloads scene, playable immediately
- [ ] 30+ seconds continuous play, no crashes
- [ ] Subjective: Perfect judgments feel reachable after calibration (Spec §12 success criterion 2)

### 6.4 Completion report

`docs/superpowers/reports/2026-04-20-w3-completion.md`, W2 format, filled before calling W3 done.

---

## 7. Risks and Preflight Checks

Before coding begins, confirm:

- **Newtonsoft.Json availability** in Unity 6.3 LTS project. Check `Packages/manifest.json` for `com.unity.nuget.newtonsoft-json`. If absent, add via Package Manager. Loader implementation depends on this.
- **Touch press/release tracking** in the current W2 `TapInputHandler`. W2 code may use `EnhancedTouch` or legacy `Touch`; Hold Break detection requires both press and release events. If only press is wired, extend before Hold work.
- **`AudioSyncManager.StartSilentSong()` method exists** (Spec §5.4 claims it does; verify in W1/W2 code). If only `StartSong(clip)` exists, add the silent variant. Calibration also benefits from this anchor.
- **`AudioSource.PlayScheduled` latency on Android:** 2-second lead-in before first calibration click is not arbitrary — shorter leads risk absorbing AudioSource warmup into the offset. Do not shrink without device validation.

---

## 8. Success Criteria (W3 complete when)

1. `beethoven_fur_elise.kfchart` exists in StreamingAssets and validates successfully via `ChartLoader`.
2. `NoteSpawner` consumes `ChartData` (no more hardcoded sequence).
3. Hold notes exhibit full state machine on device: COMPLETED / BROKEN / MISSED observably distinct.
4. Calibration overlay appears on first launch, saves offset, applies to subsequent sessions.
5. Completion panel appears when song ends; Restart works.
6. ~65 EditMode tests all passing.
7. Galaxy S22 device checklist fully checked.
8. W3 completion report committed.
9. `main` branch carries commits for all tasks in the W3 plan.

---

## 9. Deferred Follow-Ups (reminder for W4+ planning)

- Real Results screen replaces `CompletionPanel` (W4)
- Settings screen with Calibration re-run (W4)
- Splash / Main / Song list scenes and scene transitions (W4)
- 48-key Salamander sample bundle + `AudioSamplePool` sample-set mode (W4 or W5)
- Python MIDI → .kfchart pipeline (W5)
- Remaining 4 songs × 2 difficulties (W5)
- Normal difficulty chart for Für Elise (W5)
- Particles, haptics, polish (W6)
