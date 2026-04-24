# KeyFlow W6 Sub-Project 10 — Judgment Text Popup

**Date:** 2026-04-24
**Week:** 6 (폴리싱 + 사운드)
**Parent MVP spec:** `docs/superpowers/specs/2026-04-20-keyflow-mvp-v2-4lane-design.md`
**Depends on:**
- W6 SP3 profiler pass (merged `30b846d`) — established GC=0 baseline that this SP must not regress.
- W6 SP4 judgment feedback (merged `79a7af9`) — provides `JudgmentSystem.OnJudgmentFeedback` event, `FeedbackDispatcher`, `FeedbackPresets`, and the `BuildFeedbackPipeline` SceneBuilder hook that this SP extends.
- W6 SP6 visual polish (merged `e144564`) — informs the world-space-canvas sorting lesson (ScreenSpaceOverlay can never paint under world-space — use WorldSpace or ScreenSpaceCamera).
- W6 SP8 hold-note polish (merged `5c58aed`) — `HandleHoldBreak` already passes `brokenNote` so its `worldPos` is exact.
- W6 SP9 profile start screen (merged `ca8c648`) — no direct dependency, but the per-profile gameplay background means text popups must remain legible on both blue and yellow backgrounds.

**Status:** Proposed

---

## 1. Motivation

SP4 added haptic + particle feedback for note judgments. The player now feels a buzz and sees a burst when a tap lands. But the **judgment class** — whether that tap was Perfect, Great, Good, or Miss — is communicated only through:

- Particle tint (white / light-blue / light-green / red), which is too subtle to read in motion,
- Haptic strength, which the brain interprets as "strong vs weak" not "Perfect vs Miss",
- `ComboHUD` number, which only moves up (hits) or resets (Miss).

Magic-Piano-class rhythm games universally pair the particle with a **short judgment-name popup** — "PERFECT!" / "GREAT!" / "GOOD!" / "MISS!" — that flashes at the judgment zone for ~0.5 s. The letter shape is what carries the class. This SP adds that popup without changing any other feedback channel.

**Qualitative success criterion:** On Galaxy S22 (R5CT21A31QB) during a 2-minute Entertainer Normal run, the player can read the judgment class from screen at arm's length for every tap, hold-break, and auto-miss. The popup does not stutter, pile up, or occlude the note lane. 60 fps and GC.Collect=0 are preserved from the SP3 baseline.

**Quantitative guardrails:**
- GC.Collect count = 0 during the same 2-min session (SP3 parity).
- EditMode tests: 163 (current) + ~12 new = ~175, all green.
- pytest: 49/49 green (pipeline not touched).
- APK size < 40.2 MB (current 39.89 MB + new C# files only, no new assets).

---

## 2. Scope

### 2.1 In scope

| ID | Item | Deliverable |
|---|---|---|
| SP10-T1 | `IJudgmentTextSpawner` interface | Single-method contract `Spawn(Judgment, Vector3)` for test injection. |
| SP10-T2 | `JudgmentTextPool` MonoBehaviour | World-space Canvas + 12-entry round-robin pool of UGUI Legacy `Text` components. `Spawn(Judgment, Vector3)` places, tints, activates. |
| SP10-T3 | `JudgmentTextPopup` MonoBehaviour | Per-popup lifetime animator. alpha-fade + y-rise + scale-punch over ~0.45 s, deactivates self on expiry. |
| SP10-T4 | `FeedbackPresets` extension | Add `perfectTextColor / greatTextColor / goodTextColor / missTextColor` Color fields and `GetTextColor(Judgment)` method. Asset gets 4 new color values. |
| SP10-T5 | `FeedbackDispatcher` extension | Add `JudgmentTextPool` SerializeField, call `textPool.Spawn(j, worldPos)` alongside existing haptic + particle fan-out. Extend `SetDependenciesForTest`. |
| SP10-T6 | `SceneBuilder` wiring | Extend `BuildFeedbackPipeline` to construct `JudgmentTextCanvas` (world-space) + `JudgmentTextPool` GameObject + popup instances; wire SerializeField refs into dispatcher. |
| SP10-T7 | EditMode tests | `JudgmentTextPoolTests` (~6), `JudgmentTextPopupTests` (~4), `FeedbackDispatcherTests` +2. |
| SP10-T8 | Device playtest | Galaxy S22 checklist: 4 judgment texts legible, no overlap trails, 60 fps steady, GC=0. |
| SP10-T9 | Housekeeping A — `.gitignore` | Append `TestResults-*.xml`, `.test-results/`, `test-results/`, `ProfilerCaptures/`, `img/`. Clears 5 currently-untracked paths. |
| SP10-T10 | Housekeeping B — stale worktrees | `rmdir /s /q` the 7 orphaned `.claude/worktrees/*` directories not listed by `git worktree list` (Windows MAX_PATH residue). |
| SP10-T11 | Housekeeping C — truncate_charts re-eval | Verify `tools/midi_to_kfchart/truncate_charts.py` is obsolete after SP8's `merge_adjacent_holds`; if pytest stays green without it, delete. If not, document why it's still needed. |

### 2.2 Out of scope

- **Hold-success "CLEARED" popup.** `JudgmentSystem` currently fires no event on hold completion (intentional, SP4 §2.2). Adding one expands this SP. Re-visit if player feedback requests it.
- **Combo-milestone popups** ("50 COMBO!", "100 COMBO!"). Separate gamification layer, post-MVP.
- **Per-lane color modulation.** All 4 lanes share the same palette. Lane identity is already conveyed by tile x-position and particle location.
- **Localized text (Korean).** Genre convention is English uppercase. Revisit if children's QA suggests otherwise.
- **TextMeshPro migration.** UGUI Legacy Text + `LegacyRuntime.ttf` is used everywhere else (`ComboHUD`, `LatencyMeter`, HUD). No reason to introduce TMP for one popup system; stay consistent.
- **Settings-screen on/off toggle.** Haptic has a toggle (some users want silence); text popup is pure visual, non-intrusive, always on.
- **Screen-shake / flash-on-miss.** Larger UX redesign; out of scope.
- **Judgment-specific animation curves.** All 4 classes share the same animation (scale-punch + rise + fade). Only color and string differ.
- **Sub-frame popup spawning.** One popup per `OnJudgmentFeedback` invocation; no double-spawn or burst.

### 2.3 Guardrails (non-regression contracts)

- **GC=0 during 2-min Entertainer Normal session** (SP3 baseline).
- **SP4 feedback pipeline unchanged for existing subscribers.** `FeedbackDispatcher.Handle` retains its existing haptic + particle calls; text pool is appended as a third target, not a replacement.
- **`JudgmentSystem` unchanged.** The event is already public and emitted for all three code paths (HandleTap, HandleAutoMiss, HandleHoldBreak). No gameplay-path modifications.
- **No new asset dependencies.** No new prefabs, sprites, or fonts. `LegacyRuntime.ttf` is Unity built-in.
- **Existing 163 EditMode tests remain green** without modification. New tests are additive.
- **HUDCanvas unchanged.** The existing ScreenSpaceOverlay canvas (ComboHUD, LatencyMeter, pause button) is untouched. The new JudgmentTextCanvas is a separate world-space canvas, wired at a different sortingOrder.

---

## 3. Approach

### 3.1 Design decisions (from brainstorming)

| # | Decision | Chosen | Rejected alternatives |
|---|---|---|---|
| 1 | Text rendering tech | **World-space Canvas + UGUI Legacy `Text`** | `TextMesh` 3D (different stack, bitmap font sharpness issue); ScreenSpaceOverlay + `WorldToScreenPoint` per-frame (camera-coupling trap). |
| 2 | Which judgments get text | **All 4 (Perfect / Great / Good / Miss)** | Perfect/Good/Miss only (loses Great-vs-Good distinction that haptic already makes); Great→Good text merge (inconsistent with haptic 4-way split). |
| 3 | Popup position | **Judgment-line y fixed, worldPos.x used as-is** | `worldPos` direct use (early/late taps look like y drift; noisy); screen-center single popup (loses lane identity); hybrid (Miss at worldPos, hits at line — inconsistent). |
| 4 | HOLD coverage | **Subscribe to existing `OnJudgmentFeedback` — covers start-tap (4 classes), hold break (Miss), auto-miss (Miss)** | +"CLEARED" hold-success popup (needs new event, expands scope); start-tap-only (player loses "why did this miss?" signal for breaks). |
| 5 | Language | **English uppercase** ("PERFECT" / "GREAT" / "GOOD" / "MISS") | Korean ("완벽 / 훌륭 / 좋음 / 실패") — genre convention is English; `LegacyRuntime.ttf` handles Korean but font weights for uppercase glyphs are more legible than mixed-width Hangul at popup size; mixed (noisy tone). |
| 6 | Color palette | **Genre-standard high-saturation**: Perfect `#FFD700`, Great `#4FC3FF`, Good `#7FDF7F`, Miss `#FF4040` | Reuse SP4 particle `tintColor` (pastel — low contrast on blue/yellow gameplay backgrounds); per-lane color with judgment-modulated brightness (lane identity already conveyed by x-position — redundant). |
| 7 | Animation | **Scale-punch + y-rise + alpha-fade** (shared across all 4 classes) | Base alpha-fade only (flat, no impact); per-judgment animation (complexity 3× with low UX gain); minimal fade (no y-rise — fails to draw the eye). |
| 8 | Housekeeping bundling | **3 housekeeping items in SP10 PR** | Separate PR (extra merge overhead for 1-line + 2-command changes). |

### 3.2 Dispatch architecture

```
JudgmentSystem.OnJudgmentFeedback(Judgment, Vector3 worldPos)
  │  (unchanged from SP4 — 3 call sites: HandleTap, HandleAutoMiss, HandleHoldBreak)
  ▼
FeedbackDispatcher.Handle(j, worldPos)
  ├─► HapticService.Fire(j)                 (SP4, gated by UserPrefs.HapticsEnabled)
  ├─► ParticlePool.Spawn(j, worldPos)        (SP4, always on)
  └─► JudgmentTextPool.Spawn(j, worldPos)    (SP10, always on)
        │
        ▼
     Round-robin pick slot [nextIndex], advance.
        │
        ▼
     Slot N: JudgmentTextPopup.Activate(lifetimeSec)
        - text.text = "PERFECT"/"GREAT"/"GOOD"/"MISS"  (static readonly)
        - text.color = presets.GetTextColor(j)
        - RectTransform.anchoredPosition = (worldPos.x → canvas local x, judgmentY → canvas local y=0)
        - gameObject.SetActive(true)
        - spawnTime = Time.time
        │
        ▼ Update (per frame, while active)
     lerp scale (1.3 → 1.0 in first 22% of lifetime)
     lerp alpha (1.0 → 0.0 in last 45% of lifetime)
     integrate y-rise (+0.36 world units total over lifetime)
        │
        ▼ Update (t ≥ 1.0)
     gameObject.SetActive(false)
```

### 3.3 Rejected architectural alternatives

- **Put the popup logic into `ParticlePool`.** Rejected: particle pool is already a single-responsibility GameObject (pool a ParticleSystem). Mixing a Text pool in would couple two different rendering primitives behind one SerializeField set and break the "one pool, one prefab kind" pattern.
- **Per-popup coroutines instead of `Update`.** Rejected: coroutines allocate on each `StartCoroutine`; 12 popups × multiple taps/sec in a chorus burst = GC hit. `Update` with `spawnTime` sentinel is zero-alloc.
- **Separate event on `JudgmentSystem` for text (e.g., `OnJudgmentTextRequested`).** Rejected: `OnJudgmentFeedback` already covers the exact set of call sites we want (start tap 4-class, hold break Miss, auto-miss Miss). A second event would be pure duplication.
- **Spawn text directly from `JudgmentSystem`.** Rejected: breaks the SP4 separation of concerns — `JudgmentSystem` stays pure-C#-testable (no `UnityEngine` GameObject calls in the judgment path). `FeedbackDispatcher` is the canonical fan-out owner.
- **Use `ScreenSpaceCamera` canvas instead of `WorldSpace`.** Marginal; both would work. Chose `WorldSpace` because popup position is naturally expressed in world units (same as particles and the judgment line), so world-space avoids the per-spawn `WorldToScreenPoint` coordinate conversion and matches particles' frame of reference exactly.

---

## 4. Components

### 4.1 New files

| Path | Type | Responsibility |
|---|---|---|
| `Assets/Scripts/Feedback/IJudgmentTextSpawner.cs` | interface | `void Spawn(Judgment j, Vector3 worldPos)`. Single-method contract so `FeedbackDispatcher` can be tested with a fake spawner. Mirrors SP4's `IHapticService` / `IParticleSpawner` pattern. |
| `Assets/Scripts/Feedback/JudgmentTextPool.cs` | `MonoBehaviour`, implements `IJudgmentTextSpawner` | Owns pool array. `Awake` instantiates 12 text GameObjects under its own transform, each with `Text` + `JudgmentTextPopup`. `Spawn(j, wp)` picks `pool[nextIndex]`, sets text + color + position, calls `popup.Activate(lifetime)`. SerializeFields: `presets` (FeedbackPresets), `poolSize=12`, `lifetimeSec=0.45`, `yRiseUnits=0.36`, `fontSize=48`, `laneAreaWidth`, `judgmentY`. |
| `Assets/Scripts/Feedback/JudgmentTextPopup.cs` | `MonoBehaviour` | Per-instance animator. Fields: `spawnTime`, `lifetime`, `yRiseUnits`, `baseColor`, `startLocalY`. `Activate(lifetime, yRise, color)` resets state. `Update` advances `t = (Time.time - spawnTime) / lifetime`, lerps scale / alpha / y, deactivates at `t ≥ 1`. |
| `Assets/Tests/EditMode/JudgmentTextPoolTests.cs` | EditMode tests | 6 tests (see §6). |
| `Assets/Tests/EditMode/JudgmentTextPopupTests.cs` | EditMode tests | 4 tests (see §6). |

### 4.2 Modified files

| Path | Change |
|---|---|
| `Assets/Scripts/Feedback/FeedbackPresets.cs` | Add 4 `Color` public fields (`perfectTextColor`, `greatTextColor`, `goodTextColor`, `missTextColor`). Add `Color GetTextColor(Judgment j)` method (switch expression, same pattern as `GetHaptic` / `GetParticle`). Defaults: gold / cyan / green / red (§3.1 #6). |
| `Assets/ScriptableObjects/FeedbackPresets.asset` | Add 4 color values via Inspector (manual edit; the new fields use `Color.white` as Unity's default, so explicit values must be set). |
| `Assets/Scripts/Feedback/FeedbackDispatcher.cs` | Add `[SerializeField] private JudgmentTextPool textPool` + `private IJudgmentTextSpawner textSpawner`. `Awake` wires `textSpawner ??= textPool`. `Handle` appends `if (textSpawner != null) textSpawner.Spawn(j, worldPos);`. `SetDependenciesForTest` gains a 4th parameter `IJudgmentTextSpawner t`. |
| `Assets/Tests/EditMode/FeedbackDispatcherTests.cs` | Update `SetDependenciesForTest` callers; add 2 new tests (§6). Existing tests use a fake `IJudgmentTextSpawner` stub to avoid NullRefs. |
| `Assets/Editor/SceneBuilder.cs` | Extend `BuildFeedbackPipeline`: after the existing ParticlePool construction, instantiate `JudgmentTextCanvas` GameObject with `Canvas (WorldSpace)` + `CanvasScaler` + `GraphicRaycaster=off` + `JudgmentTextPool`. Wire `presets`, `laneAreaWidth=LaneAreaWidth`, `judgmentY=JudgmentY` SerializeFields. Append `SetField(dispatcher, "textPool", textPool)`. |
| `.gitignore` | Append 5 patterns (T9). |
| `tools/midi_to_kfchart/truncate_charts.py` | Deleted if T11 verifies obsolescence, else documented. |

### 4.3 Preset defaults (starting values, tunable in Editor)

Text color (new):

| Judgment | Color | Hex |
|---|---|---|
| Perfect | gold | `#FFD700` |
| Great | cyan | `#4FC3FF` |
| Good | green | `#7FDF7F` |
| Miss | red | `#FF4040` |

Animation (hard-coded in `JudgmentTextPopup`, tunable as SerializeField on `JudgmentTextPool`):

| Parameter | Default | Rationale |
|---|---|---|
| `lifetimeSec` | 0.45 s | Long enough to read at arm's length, short enough to clear before next chord (2 notes/s avg). |
| `yRiseUnits` | 0.36 world units | ~10% of the visible lane height; subtle upward drift, not a launch. |
| Scale-punch | 1.3 → 1.0 over first 22% | "Pop" feel without overshooting into cartoon territory. |
| Alpha-fade | 1.0 → 0.0 over last 45% | Starts fading after ~55% elapsed — text is fully readable during the peak. |
| `fontSize` | 48 (Text component) | On a 720×1280 reference canvas at world-scale 0.01 the text occupies ~0.48 world units tall. |

All values are starting points — device playtest drives final tuning via Inspector, no code rebuild.

### 4.4 Canvas scale rationale

World-space canvas needs an `rt.sizeDelta` in pixels and a `localScale` that converts pixels to world units. Matching SP6 gameplay visuals where lane widths are in world units (`LaneAreaWidth ≈ 3.6`), a 720×1280 "virtual pixel" canvas at scale 0.01 gives a ~7.2×12.8 world unit render area — wide enough that text positioned at `worldPos.x ∈ [-LaneAreaWidth/2, +LaneAreaWidth/2]` sits inside the canvas without clipping.

---

## 5. Data flow

### 5.1 Path 1 — Perfect / Great / Good tap

1. `TapInputHandler.FirePress` → audio (unchanged).
2. `OnLaneTap` → `JudgmentSystem.HandleTap`.
3. Evaluator returns `{Judgment, deltaMs}`.
4. `OnJudgmentFeedback?.Invoke(judgment, closest.transform.position)` — SP4 code path.
5. `FeedbackDispatcher.Handle`:
   - `HapticService.Fire(j)` (SP4)
   - `ParticlePool.Spawn(j, wp)` (SP4)
   - **`JudgmentTextPool.Spawn(j, wp)`** (SP10 new)
6. `JudgmentTextPool.Spawn`:
   - `pool[nextIndex]` — next available slot
   - `text.text = LookupString(j)` — returns static readonly string
   - `text.color = presets.GetTextColor(j)`
   - `rt.anchoredPosition = new Vector2(WorldXToCanvasX(wp.x), 0f)` — y=0 is the canvas origin, positioned at judgment line in world
   - `popup.Activate(lifetimeSec, yRiseUnits, text.color)`
   - `nextIndex = (nextIndex + 1) % poolSize`
7. On each frame where `popup` is active: `Update` lerps scale/alpha/y.
8. When `t ≥ 1`: `gameObject.SetActive(false)`. Slot is now available for reuse when `nextIndex` wraps back.

### 5.2 Path 2 — Judgment Miss (tap, but timing too off)

Same as Path 1 but `j = Miss`. `LookupString(Miss) → "MISS"`, `GetTextColor(Miss) → #FF4040`. Popup animates and expires identically.

### 5.3 Path 3 — Auto Miss (note expired without tap)

1. `NoteController.Update` detects `songTimeMs > HitTimeMs + maxWindow`.
2. → `JudgmentSystem.HandleAutoMiss(note)`.
3. `OnJudgmentFeedback?.Invoke(Miss, note.transform.position)` — SP4.
4. Dispatcher fan-out as Path 1. Text popup appears at the note's last visible x (lane center), y snapped to judgment line. The note's actual y at expiry is off-screen below the judgment line; snapping y to the line keeps the popup readable.

### 5.4 Path 4 — Hold break

1. `HoldTracker.Tick` emits `HoldTransition.Broken`.
2. → `JudgmentSystem.HandleHoldBreak(brokenNote)`.
3. `OnJudgmentFeedback?.Invoke(Miss, brokenNote.transform.position)` — SP4.
4. Dispatcher fan-out. Popup appears at hold tile's x, judgment-line y.

### 5.5 Pool exhaustion behavior

Round-robin never blocks or drops. When `nextIndex` wraps back to 0 and slot 0 is still animating, `Spawn` overwrites slot 0 — the previous popup is pre-empted. With `poolSize=12` and `lifetimeSec=0.45`, the pool can sustain 12 / 0.45 ≈ 26 popups/sec without overwrite, well above the peak-chord rate (4 notes spaced tight = ~8 popups/sec in worst 4-note chord + follow-up). Overwrite in pathological bursts is acceptable; player won't perceive a single-frame text swap.

---

## 6. Testing

### 6.1 EditMode: `JudgmentTextPoolTests` (new, 6 tests)

1. `Spawn_FirstCall_ActivatesIndexZero` — after `Awake` + first `Spawn`, `pool[0].gameObject.activeSelf == true` and `pool[1..11]` all inactive.
2. `Spawn_RoundRobin_CyclesThroughPool` — 12 `Spawn` calls produce slot indices 0,1,2,...,11; 13th call wraps to 0.
3. `Spawn_AppliesPresetColorPerJudgment` — `Spawn(Perfect, _)` → `text.color == goldFromPreset`; same for Great/Good/Miss.
4. `Spawn_SetsTextString_MatchesJudgment` — `text.text == "PERFECT" | "GREAT" | "GOOD" | "MISS"`.
5. `Spawn_PlacesXAtWorldPos_YAtFixedJudgmentLine` — `Spawn(Perfect, (2.5f, 99f, 0f))` → `rt.anchoredPosition.y` is the canvas-local equivalent of `judgmentY`, regardless of the 99 in input.
6. `Spawn_Miss_UsesJudgmentY_NotWorldPosY` — same as #5 but with Miss + an off-screen y input, verifying Miss doesn't special-case.

### 6.2 EditMode: `JudgmentTextPopupTests` (new, 4 tests)

1. `Activate_SetsGameObjectActive` — initial state inactive; after `Activate(0.45f, 0.36f, Color.red)`, `gameObject.activeSelf == true`.
2. `TickForTest_BeforeLifetimeEnd_StaysActive` — simulate `Time.time` advance to `spawnTime + 0.3f`; after `TickForTest()`, `gameObject.activeSelf == true` and `transform.localScale ≈ Vector3.one`.
3. `TickForTest_AfterLifetime_DeactivatesGameObject` — advance to `spawnTime + 0.5f`; after `TickForTest()`, `gameObject.activeSelf == false`.
4. `TickForTest_InPunchPhase_ScaleGreaterThanOne` — advance to `spawnTime + 0.05f` (early in 0-22% punch window); `transform.localScale.x > 1.0f`.

Test hooks: `JudgmentTextPopup` exposes `internal void TickForTest(float simulatedTime)` that takes a simulated `Time.time` and runs the body of `Update` — same pattern as `ComboHUD.UpdateForTest`, `LaneGlowController.TickForTest`.

### 6.3 EditMode: `FeedbackDispatcherTests` extensions (+2 tests)

- `Handle_InvokesTextPool` — fake `IJudgmentTextSpawner` captures `(Judgment, Vector3)`; firing the event calls `Spawn` once with matching args.
- `Handle_RoutesAllFourJudgmentsToTextPool` — fire 4 events (Perfect, Great, Good, Miss); fake captures 4 calls with the corresponding Judgment.

Existing `FeedbackDispatcherTests` callers of `SetDependenciesForTest` get a 4th argument (null-object stub for `IJudgmentTextSpawner`). No behavioral changes to existing tests.

### 6.4 pytest

Not touched. `tools/midi_to_kfchart/` pipeline is unchanged. If T11 deletes `truncate_charts.py`, existing pytest coverage must still pass without it — that's the go/no-go.

### 6.5 Device playtest (Galaxy S22, R5CT21A31QB)

Acceptance checklist, executed on release APK over `2026-04-19-keyflow-mvp-design.md` sample chart (Entertainer Normal):

- [ ] All 4 judgment texts appear during a 2-minute run. Confirm all 4 seen.
- [ ] Each text's color matches the preset (gold / cyan / green / red).
- [ ] Text y-position stays on the judgment line regardless of tap timing.
- [ ] Text lifetime is short enough that 4-note chords don't pile up 5+ overlapping popups.
- [ ] 60 fps hold throughout the session (via Unity profiler connect-on-play or `LatencyMeter` overlay).
- [ ] GC.Collect delta = 0 (profiler verification, SP3 baseline).
- [ ] APK size < 40.2 MB.
- [ ] Text legible on both blue (나윤) and yellow (소윤) gameplay backgrounds.

---

## 7. Risks

| # | Risk | Probability | Mitigation |
|---|---|---|---|
| 1 | World-space Canvas scale tuning fails first try — text too small / too large | Medium | `fontSize` and canvas `localScale` are Inspector-tunable. First-pass values calibrated against `LaneAreaWidth ≈ 3.6` and 720×1280 reference; iterate from device playtest. Pool rebuild not needed for tuning. |
| 2 | `Text.text = "..."` assignment triggers GC | Low | UGUI `Text.text` setter compares against current value before re-mark-dirty; with `static readonly string` per judgment and 4 distinct values, repeated assigns of the same string are no-op. Verified by SP3 profiler pattern — `LatencyMeter` does similar `sb.ToString()` → `text.text` assignment at 0.5s cadence with GC=0. |
| 3 | Canvas sortingOrder collides with particles or ScreenSpaceCamera gameplay BG | Low | SP6 established that ScreenSpaceCamera gameplay BG uses low sortingOrder; particles use default world-space sort (near-z). JudgmentTextCanvas world-space at sortingOrder=10 renders above both. Verified on device. |
| 4 | 12-slot pool overwrites during dense 4-note chord + stragglers | Low | 12 slots / 0.45 s = 26 popups/sec capacity. Peak density is ~8 popups/sec (see §5.5). If device playtest shows overwrites, bump `poolSize` SerializeField. |
| 5 | `JudgmentTextPopup.Update` × 12 inactive GameObjects per frame adds measurable cost | Very low | Unity skips `Update` on inactive GameObjects. Only active popups tick; steady state is 1-3 active. |
| 6 | T11 (truncate_charts deletion) silently breaks a chart post-pipeline | Low | pytest is the gate. If 49/49 stays green without it, it's obsolete. If any test fails, the script stays and T11 becomes a "document why" task instead of delete. |
| 7 | T10 (stale worktree deletion) wipes in-progress feature work | Low | Only delete worktrees **not** listed by `git worktree list`. The 7 in question are directory residue — git has already forgotten them. Cross-check before `rmdir`. |

---

## 8. Timeline

SP10 is similar scale to SP4 (haptic + particle bundle). Target completion in one session:

| Phase | Estimate |
|---|---|
| Brainstorming (this doc) | 30 min (complete) |
| Implementation plan (writing-plans skill) | 20 min |
| Implementation (TDD, following plan) | ~90 min |
| Device playtest + tuning | 30 min |
| Housekeeping (T9, T10, T11) | 10 min |
| Merge | 5 min |
| **Total** | ~3 hours |

---

## 9. Open questions

None at brainstorming close. Any discovery during implementation that invalidates a decision above returns here for an update.

---

## 10. Post-merge follow-ups (explicit non-scope)

- Hold-success "CLEARED" popup (§2.2).
- Combo-milestone popups (§2.2).
- Korean localization if QA reveals reading difficulty (§2.2).
- `LatencyMeter` HUD removal or gating by debug flag — `LatencyMeter` currently always runs; unrelated to SP10 but noticed during exploration.
