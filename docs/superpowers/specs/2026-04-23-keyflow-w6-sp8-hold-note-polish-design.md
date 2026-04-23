# KeyFlow W6 Sub-Project 8 — Hold-Note Polish Pass

**Date:** 2026-04-23
**Week:** 6 (폴리싱 + 사운드)
**Priority:** SP3 carry-over ("Hold-note audio feedback") + two adjacent hold-note UX complaints surfaced during SP7 device playtest
**Status:** Proposed

---

## 1. Motivation

During SP7 device-playtest follow-up, three hold-note pain points were called out together:

1. **홀드 노트가 너무 많이 나온다** — too many notes become HOLDs, making gameplay feel like sustained holding rather than tapping. Root cause: `tools/midi_to_kfchart/pipeline/hold_detector.py:2` uses `HOLD_THRESHOLD_MS = 300`, so any MIDI note ≥ 300 ms (quarter notes at 120 BPM) becomes a HOLD.
2. **홀드 중 화면 임팩트 약하다** — once a hold has started, the player sees only the scrolling tile. There is no active signal at the judgment line telling them "the lane is live." `FeedbackDispatcher` only reacts to judgment events (TAP / hold break / hold completion), never to "currently holding."
3. **홀드 중 처음 누를 때만 소리 난다** — `TapInputHandler.FirePress → AudioSamplePool.PlayForPitch` fires once on press. The Salamander piano sample has a ~4–44 s natural decay, so audio is technically present, but for holds over ~1.5 s the perceived loudness fades and the player feels silent.

Issue #3 was already logged as an open SP3 carry-over (see `memory/project_w6_sp3_complete.md`, "New carry-over" section). Issues #1 and #2 are adjacent enough that folding them into a single "hold-note polish pass" is cheaper than three SPs.

## 2. Goal

One sub-project, W6-SP8, that resolves all three issues:

- **Content**: retune the MIDI-to-chart HOLD classifier so fewer notes become HOLDs.
- **Visual**: give each lane a subtle glow at the judgment line while a hold is active.
- **Audio**: retrigger the held pitch at a fixed interval so the sustain is audibly maintained.

Qualitative success criterion (Galaxy S22 R5CT21A31QB device playtest, release APK):
- The Entertainer Normal and Für Elise Normal feel like tap-dominant gameplay with holds as occasional events, not continuous.
- While holding, the player sees a pulsing glow on the active lane's judgment line and hears the pitch repeatedly; both cease immediately on release / break / complete.
- SP3 `GC.Collect = 0` baseline is preserved (profiler attach re-verified).

## 3. Non-goals

- Continuous particle emission from the judgment line during hold. Rejected as option "B" during brainstorming — continuous emit risks re-introducing GC allocations that SP3 eliminated, and one decent glow is enough visual signal. Revisit in a later polish pass if the glow alone feels flat on device.
- Tile pulse animation (scaling/color on the scrolling hold tile itself). Rejected — the tile is already scrolling, so layered animation on the tile looks busy.
- Continuous haptic during hold. Rejected — SP4 haptic tiers cover strike events; sustained vibration drains battery and often reads as device malfunction on low-end Android.
- Piano-damper simulation (fading the original AudioSource on release). The retrigger mechanism subsumes the felt need; a separate damp would add code for negligible perceived change.
- BPM-synced retrigger interval. Rejected — requires routing chart BPM into `TapInputHandler` / `HoldTracker`; fixed 250 ms is musically close enough to 16th-note feel at 240 BPM and produces comparable felt rhythm at other tempos.
- Per-song or per-difficulty HOLD threshold tuning. Uniform 500 ms for all songs and all difficulties. If specific songs feel off after playtest, tune globally or open a follow-up SP.
- Exposing retrigger interval / volume / glow intensity in the Settings screen.
- TextMeshPro migration (separate SP3 carry-over).

## 4. Approach

### 4.1 Issue #1 — HOLD threshold 300 → 500 ms

**Change**: `tools/midi_to_kfchart/pipeline/hold_detector.py:2` `HOLD_THRESHOLD_MS = 300` → `500`. `HOLD_CAP_MS = 4000` unchanged.

**Effect at common tempos**:
- 120 BPM quarter note (500 ms) → boundary, slight overruns still HOLD, exact quarters become TAP.
- 100 BPM quarter note (600 ms) → HOLD (unchanged classification).
- 80 BPM quarter note (750 ms) → HOLD (unchanged).
- All 8th notes and shorter at common tempos → TAP (unchanged).

Net: most mid-tempo quarter notes convert from HOLD to TAP; long sustained voices (half notes and above) remain HOLD. Short ornaments and grace notes in Für Elise already classify as TAP at 300 ms, so they are unaffected.

**Chart regeneration**: all 3 songs × 3 difficulties = 9 JSON files under `Assets/StreamingAssets/charts/` regenerated via the existing `midi_to_kfchart` pipeline. SP2's `truncate_charts.py` workaround for the `density.thin()` truncation bug is reapplied if needed (per `memory/project_w6_sp2_complete.md`).

### 4.2 Issue #2 — Lane glow while holding

**New component**: `Assets/Scripts/Feedback/LaneGlowController.cs` (MonoBehaviour, `KeyFlow.Feedback` namespace).

```
LaneGlowController
  SpriteRenderer[] glowSprites     // one per lane, set by SceneBuilder
  HashSet<int>    activeLanes      // reusable field, no per-frame alloc
  AudioSyncManager audioSync       // pause-gating

  On(lane)     → activeLanes.Add(lane)
  Off(lane)    → activeLanes.Remove(lane); reset sprite alpha to 0
  Clear()      → activeLanes.Clear(); reset all sprite alphas
  Update()     → if paused, return; for each lane 0..3, if active set alpha to pulse, else 0
```

Pulse formula: `alpha = 0.3f + 0.2f * Mathf.Sin(Time.time * 6f)` — oscillates 0.1 ~ 0.5, ~1 Hz visible pulse. Stack-only math; no heap allocations per frame.

**Wiring**: `HoldTracker` gains a `[SerializeField] private LaneGlowController laneGlow` field.
- On `OnHoldStartTapAccepted(note)` → `laneGlow.On(note.Lane)`.
- On transition to `Completed` or `Broken` → `laneGlow.Off(lane)` at the same site that removes `idToNote`.
- On `ResetForRetry()` → `laneGlow.Clear()`.

**Sprite construction (SceneBuilder)**: for each of 4 lanes, create a child GameObject under a `LaneGlowRoot` parent with:
- `SpriteRenderer` using a 1×1 white sprite (or a stock UI rounded-rect), scaled to lane-width × small height (e.g., 0.3 units tall).
- Positioned at `(LaneLayout.LaneToX(lane, laneAreaWidth), judgmentY, 0)`.
- Sorting order behind note tiles but above the lane background.
- Initial `color = new Color(1, 1, 1, 0)` (invisible).

`LaneGlowController` is added as a component; `glowSprites[lane]` is wired via `SetField` in the SceneBuilder style (SerializeField + SerializedObject batch-set).

**GC invariants**:
- `Update()` walks lane indices 0..3 by `for` loop — no enumerator.
- `On` / `Off` on a `HashSet<int>` with capacity 4 — no realloc once warm.
- `Color` is a struct; `SpriteRenderer.color = ...` assigns by value.

### 4.3 Issue #3 — Hold-note audio retrigger

**Constants (in HoldTracker)**:
```csharp
private const int   HOLD_RETRIGGER_INTERVAL_MS = 250;
private const float HOLD_RETRIGGER_VOLUME      = 0.7f;
```

**State (in HoldTracker)**:
```csharp
private struct HoldAudioState { public int pitch; public int lastRetriggerMs; }
private readonly Dictionary<int, HoldAudioState> laneAudio
    = new Dictionary<int, HoldAudioState>(LaneLayout.LaneCount);
```

Key is lane index (0..3), not register id. Charts never schedule overlapping HOLDs on the same lane, so lane-keyed is safe and enables for-loop iteration (no enumerator box).

**Flow**:
1. On `OnHoldStartTapAccepted(note)` (called from `JudgmentSystem.HandleTap` after a HOLD's start tap is judged P/G/G) → `laneAudio[note.Lane] = { pitch = note.Pitch, lastRetriggerMs = note.HitTimeMs }`. The first audible note is already produced by the existing `TapInputHandler.FirePress → PlayTapSound`.
2. In `HoldTracker.Update()`, after existing `idToNote.Count == 0` guard but also entered when `laneAudio.Count > 0`, iterate lanes:
   ```csharp
   int songMs = audioSync.SongTimeMs;
   for (int lane = 0; lane < LaneLayout.LaneCount; lane++)
   {
       if (!laneAudio.TryGetValue(lane, out var st)) continue;
       if (songMs - st.lastRetriggerMs < HOLD_RETRIGGER_INTERVAL_MS) continue;
       audioPool.PlayForPitch(st.pitch, HOLD_RETRIGGER_VOLUME);
       st.lastRetriggerMs = songMs;
       laneAudio[lane] = st;
   }
   ```
3. On Completed / Broken transition in the existing foreach → `laneAudio.Remove(note.Lane)`.

**AudioSamplePool API addition**:
```csharp
public void PlayForPitch(int midiPitch, float volume = 1f)
{
    var (clip, ratio) = ResolveSample(...);
    if (clip == null) { PlayOneShot(); return; }
    var src = NextSource();
    src.pitch = ratio;
    src.clip = clip;
    src.volume = volume;
    src.Play();
}
```

Default parameter preserves existing call sites — `TapInputHandler.PlayTapSound` behavior is byte-identical.

**Update-loop gating**: existing `audioSync.IsPlaying` / `!audioSync.IsPaused` guards at the top of `HoldTracker.Update()` already freeze retrigger during pause. `ResetForRetry()` clears `laneAudio`.

**GC invariants**:
- Dictionary access by key (`TryGetValue`, indexer set) — no allocation after warm-up.
- `HoldAudioState` is a struct; re-assignment `laneAudio[lane] = st` copies in place.
- `for` loop over int range — no enumerator.

### 4.4 SceneBuilder integration

`Assets/Editor/SceneBuilder.cs` gains:
- Lane-glow sprite construction (4 GameObjects under a `LaneGlowRoot` parent).
- `LaneGlowController` component added to a manager GameObject (reusing an existing manager GameObject or a new one — TBD during implementation, whichever matches SceneBuilder's pattern).
- `HoldTracker.laneGlow` wired via SetField to the `LaneGlowController`.

Per the SP7 consolidation principle, no separate Wireup step is introduced; everything lives inside `SceneBuilder.Build()`.

Audio changes require no SceneBuilder work — `HoldTracker` already has the `audioSync` and `tapInput` wirings; `AudioSamplePool` reference is added as a new `[SerializeField]` and wired in SceneBuilder next to existing `HoldTracker` field wiring.

## 5. Files

**Modified:**
- `tools/midi_to_kfchart/pipeline/hold_detector.py` — threshold constant.
- `tools/midi_to_kfchart/tests/test_hold_detector.py` — boundary cases at 500 ms.
- `tools/midi_to_kfchart/tests/test_w6_sp2_charts.py` — update expected HOLD counts / totals per regenerated chart.
- `Assets/StreamingAssets/charts/*.json` — 9 regenerated files.
- `Assets/Scripts/Gameplay/HoldTracker.cs` — glow + retrigger fields, logic, ResetForRetry.
- `Assets/Scripts/Gameplay/AudioSamplePool.cs` — `PlayForPitch(int, float)` overload with default.
- `Assets/Editor/SceneBuilder.cs` — lane glow sprite construction, HoldTracker field wiring.
- `Assets/Scenes/GameplayScene.unity` — regenerated by SceneBuilder.

**New:**
- `Assets/Scripts/Feedback/LaneGlowController.cs`
- `Assets/Scripts/Feedback/LaneGlowController.cs.meta`
- `Assets/Tests/EditMode/LaneGlowControllerTests.cs`
- `Assets/Tests/EditMode/HoldAudioRetriggerTests.cs`

**Possibly updated:**
- `Assets/Tests/EditMode/AudioSamplePoolTests.cs` — add volume-parameter case.

## 6. Implementation sequence

Commit-level ordering (refined further in writing-plans):

1. **Python threshold + tests + chart regeneration** — fully independent from Unity work. Verify 9 chart files regenerate cleanly; if `density.thin()` truncation appears, apply `truncate_charts.py` workaround.
2. **AudioSamplePool volume overload + test** — smallest isolated Unity change.
3. **HoldTracker retrigger logic + HoldAudioRetriggerTests** — uses (2). Verify GC-free with for-loop iteration pattern; confirm via EditMode asserts and later profiler.
4. **LaneGlowController + LaneGlowControllerTests** — pure component, independent of (3).
5. **SceneBuilder integration** — wire glow sprites + HoldTracker fields; regenerate scene; EditMode sanity pass.
6. **Release APK build + device playtest** — Entertainer Normal + Für Elise Normal; confirm felt improvements for all three issues; profiler attach to confirm SP3 GC baseline preserved.
7. **W6-SP8 completion report** + memory update.

## 7. Testing

**Python:**
- `test_hold_detector.py` — cases at 499 ms (TAP), 500 ms (HOLD), 4001 ms (HOLD, capped at 4000).
- `test_w6_sp2_charts.py` — regenerate fixtures, update expected counts.

**Unity EditMode:**
- `LaneGlowControllerTests`:
  - `On_SingleLane_SetsActiveLane` — after `On(0)`, `Update()` once, sprite[0].color.a > 0.
  - `Off_SingleLane_ResetsAlpha` — after `On(0)` then `Off(0)`, sprite[0].color.a == 0.
  - `On_Idempotent` — calling `On(0)` twice then `Off(0)` leaves alpha 0.
  - `Clear_ResetsAllLanes` — after `On(0..3)` then `Clear()`, all sprite alphas 0.
- `HoldAudioRetriggerTests` (injects fake `AudioSamplePool` + `AudioSyncManager`):
  - `NoRetrigger_Before250ms` — 0 extra play calls within 249 ms.
  - `Retrigger_At250ms` — exactly 1 extra play call at 250 ms.
  - `Retrigger_Three_At750ms` — exactly 3 extra play calls at 750 ms.
  - `NoRetrigger_AfterCompleted` — after `MarkHoldCompleted` transition, no further retrigger.
  - `NoRetrigger_AfterBroken` — after `Broken` transition, no further retrigger.
  - `RetriggerUsesCapturedPitch` — last play call uses the pitch captured at hold start, not any newer note's pitch.
  - `NoRetrigger_WhilePaused` — verifies `audioSync.IsPaused` gate.
- `AudioSamplePoolTests` (extend):
  - `PlayForPitch_VolumeDefault_IsOne` — regression.
  - `PlayForPitch_VolumeExplicit_PassedToSource`.

**Device playtest (S22, release APK):**
- Entertainer Normal full run — HOLD count feels "sparse"; mid-hold audio retrigger audible; lane glow visible on every hold; GC.Collect == 0 on profiler.
- Für Elise Normal partial run (grace-note section + sustained chords) — ornaments unaffected by threshold change; chord-sustains feel held; retrigger audible.
- Retry mid-hold — verify glow clears and laneAudio resets.
- Pause mid-hold — verify glow freeze + no retrigger fires; resume restarts retrigger cleanly from the correct song time.

## 8. Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Dictionary enumerator GC allocation (hold audio) | Medium | Medium (SP3 GC baseline regression) | Enforce for-loop + TryGetValue pattern; EditMode unit test exercises path; profiler attach step in sequence (6). |
| `density.thin()` truncation needed after regeneration | Medium | Low | SP2's `truncate_charts.py` workaround is available per `memory/project_w6_sp2_complete.md`. |
| Glow sprite renders in front of note tiles or behind background | Low | Medium | Set `sortingOrder` explicitly between background and notes; SP6 learned the ScreenSpaceOverlay lesson — this is world-space sprites, so a 2D sorting-order assignment suffices. |
| AudioSource channel exhaustion (16 channels) | Low | Low | Worst-case = 4 lane retriggers + 1 concurrent TAP = 5 ≪ 16. |
| 500 ms threshold makes HOLDs too rare / boring | Medium | Low | Device playtest gates acceptance; if too sparse, retune to 450 in a single-line follow-up. |
| Retrigger at 250 ms feels robotic or desynced with music | Low | Medium | Playtest on all 3 songs; if bad, bump to 300 ms or 400 ms; BPM sync is a backlog idea, not this SP's goal. |
| 0.7 retrigger volume too loud or too quiet | Low | Low | Same playtest-driven tuning; constant is a single line to edit. |

## 9. Success criteria

**Objective (measurable):**
- All Python and Unity EditMode tests green.
- Profiler attach during Entertainer Normal: `GC.Collect == 0`, per-frame Reserved-Total delta indistinguishable from SP3 baseline.
- Release APK size within SP7 baseline +100 KB (new sprite + ~150 LOC gameplay code).

**Subjective (device playtest, user-confirmed):**
- #1: "홀드가 덜 나온다" — user-acknowledged reduction in hold density.
- #2: "홀드 중 화면이 살아있다" — lane glow visible and readable during all hold events.
- #3: "홀드 중 소리가 이어진다" — user no longer reports "only first tap sounds."

## 10. Out-of-scope follow-ups

Logged for future SPs, not this one:
- BPM-synced retrigger interval (musical polish).
- Per-song / per-difficulty HOLD threshold tuning (content-design tool).
- Glow intensity / retrigger volume exposed in Settings.
- Continuous sparse particle emission at judgment line during hold (the previously-rejected "A+B" visual option).
- Piano-damper fade on release (the previously-rejected audio option B).
- Sustain tone underlay (the previously-rejected audio option D).
- TextMeshPro migration (pre-existing SP3 carry-over).
