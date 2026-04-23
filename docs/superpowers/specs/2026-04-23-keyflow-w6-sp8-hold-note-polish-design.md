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

**Chart regeneration**: all 4 `.kfchart` files under `Assets/StreamingAssets/charts/` regenerated via the existing `midi_to_kfchart` pipeline (`beethoven_fur_elise`, `beethoven_ode_to_joy`, `debussy_clair_de_lune`, `joplin_the_entertainer`). Each file contains both EASY and NORMAL difficulty sections; the pipeline's `--merge-into` mode writes one file per difficulty pass. SP2's `truncate_charts.py` workaround for the `density.thin()` truncation bug is reapplied if needed (per `memory/project_w6_sp2_complete.md`).

Current HOLD density (pre-threshold-change, for reference):

| Song | EASY total / HOLD % | NORMAL total / HOLD % |
|------|---------------------|------------------------|
| beethoven_fur_elise | 73 / 2.7% | 589 / 11.9% |
| beethoven_ode_to_joy | 46 / 100.0% | 68 / 100.0% |
| debussy_clair_de_lune | 107 / 94.4% | 160 / 93.8% |
| joplin_the_entertainer | 321 / 41.4% | 481 / 41.2% |

Ode to Joy (BPM 100) and Clair de Lune (BPM 60) have nearly every note classified as HOLD under the 300 ms threshold because quarter notes at those tempos already exceed 600 ms. Raising to 500 ms reduces Entertainer/Für Elise HOLD density but will only partially relieve Ode to Joy and Clair de Lune. Per Non-Goals §3, per-song threshold tuning is out of scope; if Ode to Joy and Clair de Lune still feel hold-dominant after playtest, follow-up SP considers per-song threshold or higher uniform threshold.

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

**Sprite construction (SceneBuilder)**: a new `LaneGlow` GameObject is added as a child of the existing `Managers` GameObject (sibling to `AudioSync`, `SamplePool`, `TapInput`, etc.). The `LaneGlowController` component lives on `LaneGlow`. For each of 4 lanes, create a child GameObject under `LaneGlow` with:
- `SpriteRenderer` using the existing `EnsureWhiteSprite()` asset (same sprite the judgment line uses).
- Transform scaled to `(LaneAreaWidth / LaneLayout.LaneCount, 0.3f, 1)` — matches note tile lane width, 0.3 units tall.
- Positioned at `(LaneLayout.LaneToX(lane), JudgmentY, 0)`.
- `sortingOrder = 0` — same layer as `JudgmentLine`, below note tiles (`sortingOrder = 1`), above lane dividers (`-1`).
- Initial `color = new Color(1, 1, 1, 0)` (invisible until `On(lane)` is called).

`LaneGlowController.glowSprites[lane]` is wired via `SetArrayField` (matching the existing `SetArrayField(samplePool, "pitchSamples", ...)` pattern on line 211).

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
private readonly Dictionary<int, HoldAudioState> holdAudio
    = new Dictionary<int, HoldAudioState>(LaneLayout.LaneCount * 2);

[SerializeField] private AudioSamplePool audioPool;
```

**Key is `HoldStateMachine` register id, NOT lane index.** Real charts do have same-lane HOLD-HOLD overlap: scanning the currently shipped `.kfchart` files (at simulated 500 ms threshold), `debussy_clair_de_lune` NORMAL has 12 same-lane HOLD overlaps and `beethoven_fur_elise` NORMAL has 1. Lane-keying would overwrite the first hold's pitch when the second starts, and prematurely drop the second's entry when the first completes. Id-keying sidesteps both failure modes with no content-pipeline change.

Iteration uses `foreach (var kv in holdAudio)` — `Dictionary<int, HoldAudioState>.Enumerator` is a `struct`, so `foreach` on a concrete Dictionary type does not allocate (verified in SP3 via the identical pattern at `HoldStateMachine.Tick`: `foreach (var kv in entries)` on `Dictionary<int, Entry>`, proven GC-free in profiler attach).

**Flow**:
1. `HoldTracker.OnHoldStartTapAccepted` signature gains one parameter: `public void OnHoldStartTapAccepted(NoteController note, int tapTimeMs)`. `JudgmentSystem.HandleTap` passes its local `tapTimeMs` (the player's actual tap time, which is what fires the first audible tap via `TapInputHandler.FirePress`). Inside `OnHoldStartTapAccepted`, after the existing `stateMachine.Register` + `stateMachine.OnStartTapAccepted` lines, add:
   ```csharp
   holdAudio[id] = new HoldAudioState { pitch = note.Pitch, lastRetriggerMs = tapTimeMs };
   ```
   Seeding with the actual tap time (not nominal `note.HitTimeMs`) keeps retrigger cadence anchored to the player's audible first note, regardless of early/late judgment delta. The first audible note is already produced by the existing `TapInputHandler.FirePress → PlayTapSound` path; no change there.
2. In `HoldTracker.Update()`, extend the early-return guards so audio retrigger runs whenever holdAudio has entries, but **without bypassing the existing IsPlaying / IsPaused guards**:
   ```csharp
   if (!audioSync.IsPlaying || audioSync.IsPaused) return;   // unchanged — gates EVERYTHING below
   if (idToNote.Count == 0 && holdAudio.Count == 0) return;  // expanded from `idToNote.Count == 0`
   ```
   Then, after the existing state-machine tick and transition-handling block, add the retrigger loop:
   ```csharp
   int songMs = audioSync.SongTimeMs;
   foreach (var kv in holdAudio)
   {
       var st = kv.Value;
       if (songMs - st.lastRetriggerMs < HOLD_RETRIGGER_INTERVAL_MS) continue;
       audioPool.PlayForPitch(st.pitch, HOLD_RETRIGGER_VOLUME);
       st.lastRetriggerMs = songMs;
       holdAudio[kv.Key] = st;   // struct write-back; mutating during iteration of SAME dict is unsafe, see note below
   }
   ```
   **Safe-mutation note**: mutating a Dictionary's keyset during iteration invalidates the struct enumerator. We're only updating a value for an existing key (not adding/removing), which `Dictionary<K,V>` permits with its internal version counter untouched for value-only writes via indexer. Verified behavior, matching the write-during-iteration pattern shipped in `HoldStateMachine.Tick` (where `e.state = ...` mutates entries' value fields mid-foreach). If this proves fragile in a future .NET runtime upgrade, a tiny `List<int> retriggerBuffer` field can collect pending writes and flush after the loop — no API change required.
3. In the existing `foreach (var t in transitionBuffer)` block where `Completed` / `Broken` are handled, add `holdAudio.Remove(t.id);` after the existing `idToNote.Remove(t.id);` (id is already in scope as `t.id`). No lane lookup needed.

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

**Update-loop gating**: the `audioSync.IsPlaying` / `!audioSync.IsPaused` guard stays the single topmost early-return and governs both glow and retrigger. During pause, `audioSync.SongTimeMs` freezes, so delta math on resume would otherwise be correct — but we early-return anyway, and `lastRetriggerMs` remains at its pre-pause value, so resume re-enters a correct cadence. `ResetForRetry()` clears `holdAudio` alongside `stateMachine` and `idToNote`.

**GC invariants**:
- Dictionary access by key (`TryGetValue`, indexer set) — no allocation after warm-up; initial capacity `LaneCount * 2 = 8` exceeds maximum-realistic concurrent-hold count on any shipped chart.
- `HoldAudioState` is a struct; value write-back copies in place.
- `foreach` on `Dictionary<int, HoldAudioState>` uses struct enumerator (matches HoldStateMachine pattern, SP3-verified GC-free).

### 4.4 SceneBuilder integration

`Assets/Editor/SceneBuilder.cs` gains:
- A new `BuildLaneGlow(Sprite white, Transform managersParent, out LaneGlowController)` private static helper called from `BuildManagers` (after `holdTracker` is created, before field wiring of `holdTracker.laneGlow`). The helper constructs the `LaneGlow` parent GameObject under `Managers`, adds 4 child lane-glow sprite GameObjects, adds the `LaneGlowController` component, and wires the `glowSprites[]` array via `SetArrayField`.
- `HoldTracker.laneGlow` wired via `SetField` to the returned `LaneGlowController` (right after the existing `SetField(holdTracker, "tapInput", tapInput)` line).
- `HoldTracker.audioPool` wired via `SetField` to the existing `samplePool` local (same wiring block).

Per the SP7 consolidation principle, no separate Wireup step is introduced; everything lives inside `SceneBuilder.Build()`.

## 5. Files

**Modified:**
- `tools/midi_to_kfchart/pipeline/hold_detector.py` — threshold constant.
- `tools/midi_to_kfchart/tests/test_hold_detector.py` — boundary cases at 500 ms.
- `tools/midi_to_kfchart/tests/test_w6_sp2_charts.py` — update expected HOLD counts / totals per regenerated chart.
- `Assets/StreamingAssets/charts/*.kfchart` — 4 regenerated files (each contains EASY + NORMAL difficulty sections): `beethoven_fur_elise`, `beethoven_ode_to_joy`, `debussy_clair_de_lune`, `joplin_the_entertainer`.
- `tools/midi_to_kfchart/batch_w6_sp2.yaml` → replaced by (or renamed to) `batch_w6_sp8.yaml` that includes all 4 songs' EASY+NORMAL, including `beethoven_fur_elise` (currently missing from `batch_w6_sp2.yaml` despite having EASY+NORMAL charts).
- `Assets/Scripts/Gameplay/HoldTracker.cs` — glow + retrigger fields, logic, ResetForRetry, `OnHoldStartTapAccepted(NoteController, int)` signature.
- `Assets/Scripts/Gameplay/JudgmentSystem.cs` — single-line update at `HandleTap` to pass `tapTimeMs` through.
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
  - `NoRetrigger_Before250ms` — 0 extra play calls within 249 ms of the seed `tapTimeMs`.
  - `Retrigger_At250ms` — exactly 1 extra play call at `tapTimeMs + 250`.
  - `Retrigger_Three_At750ms` — exactly 3 extra play calls across a 750 ms hold.
  - `NoRetrigger_AfterCompleted` — after `Completed` transition fires for the hold, no further retrigger (verifies `holdAudio.Remove(t.id)`).
  - `NoRetrigger_AfterBroken` — after `Broken` transition fires, no further retrigger.
  - `SameLaneOverlap_TwoHoldsRetriggerIndependently` — two HOLDs on lane 0 with overlapping time windows: first HOLD's retriggers use its own pitch through its entire duration; second HOLD's retriggers use its own pitch; completing the first does NOT cancel the second. Guards against the lane-keyed regression.
  - `RetriggerUsesTapTimeNotHitTime` — `OnHoldStartTapAccepted` called with `tapTimeMs = note.HitTimeMs - 20` (early P judgment): first retrigger fires at `tapTimeMs + 250`, not `HitTimeMs + 250`.
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
| Dictionary enumerator GC allocation (hold audio) | Low | Medium (SP3 GC baseline regression) | `foreach` on `Dictionary<int, HoldAudioState>` uses struct enumerator, proven GC-free via the existing `HoldStateMachine.Tick` pattern on `Dictionary<int, Entry>`. Profiler attach in sequence step (6) re-verifies. |
| Struct-value write during Dictionary iteration triggers version-check exception | Low | Medium (runtime exception under load) | Value-only writes (indexer set on an existing key) do not bump Dictionary's version counter in Mono/IL2CPP — matches `HoldStateMachine.Tick`'s `e.state = ...` pattern. If a future runtime tightens this, fall back to a `List<int> retriggerBuffer` field that collects pending keys and flushes after the loop (no API change). |
| `OnHoldStartTapAccepted` signature change misses a caller | Low | High (compile break, hold audio silently disabled) | One caller in repo (`JudgmentSystem.HandleTap` line 116); compile-time checked. EditMode test suite exercises this path. |
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
