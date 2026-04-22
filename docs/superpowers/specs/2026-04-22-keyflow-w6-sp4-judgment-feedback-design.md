# KeyFlow W6 Sub-Project 4 Design — Judgment Feedback (Haptics + Particles)

**Date:** 2026-04-22
**Branch:** `claude/jovial-almeida-e42f12` (worktree)
**Parent MVP spec:** `docs/superpowers/specs/2026-04-20-keyflow-mvp-v2-4lane-design.md`
**Depends on:**
- W6 SP2 content pack (merged `b66c7cc`) — provides charts to playtest feedback against
- W6 SP3 profiler pass (merged `30b846d`) — established GC=0 baseline that this SP must not regress
**Scope priority:** W6 "폴리싱 + 사운드" weekly goal — remaining polish items (particle + haptic) from v2 spec §9. Sibling SPs (#4 calibration click, #5 UI polish, #6 2nd device) stay deferred.

---

## 1. Goal

v2 spec §9 lists W6 as **"폴리싱 + 사운드 — 파티클, 햅틱, 버그 수정"**. SP1 shipped piano-sample sound polish, SP2 shipped content, SP3 shipped perf/bug hygiene. **파티클 + 햅틱 remain untouched.** This sub-project covers both as a bundled "judgment feedback" polish pass.

Core gameplay currently gives the player **audio + score-text** feedback on tap. That is adequate for validation but flat-feeling — a Piano-Tiles-style rhythm game depends heavily on tactile (haptic) and visual (particle) response to feel "alive" during the friends-playtest phase of MVP validation.

**Qualitative success criterion:** On Galaxy S22 during a 2-minute Entertainer Normal run, a player feels distinct physical and visual feedback for each of the four judgment outcomes (Perfect / Great / Good / Miss), with no noticeable frame hitches and no GC regressions from the SP3 baseline.

**Quantitative guardrails:**
- `GC.Collect` count = 0 during the same 2-min session (SP3 parity).
- APK size < 35 MB (< 40 MB MVP target, current 33.70 MB + < 1 MB budget).
- EditMode tests: 114 (current) + ~8 new = ~122, all green.

---

## 2. Scope

### 2.1 In scope

| ID | Item | Deliverable |
|---|---|---|
| SP4-T1 | `JudgmentSystem.OnJudgmentFeedback` event | `public event Action<Judgment, Vector3>`; invoked from `HandleTap` (all four outcomes, incl. Miss), `HandleAutoMiss`, `HandleHoldBreak` |
| SP4-T2 | Feedback module skeleton | 5 new files under `Assets/Scripts/Feedback/` (see §4) |
| SP4-T3 | `AndroidHapticsBridge` native wrapper | `AndroidJavaObject` wrapper for `Vibrator.vibrate(VibrationEffect)`, API 26+; Editor / non-Android compile-time no-op |
| SP4-T4 | `HapticService` preset table | 4 `VibrationEffect` instances cached in `Awake`; `Fire(Judgment)` looks up and calls `vibrate(effect)` |
| SP4-T5 | `FeedbackPresets` ScriptableObject | Inspector-tunable haptic (duration, amplitude) + particle (color, size, burst count) per judgment |
| SP4-T6 | `ParticlePool` round-robin pool | 16 hit instances + 4 miss instances pre-spawned; `Spawn(Judgment, Vector3)` returns to pool on lifetime expiry |
| SP4-T7 | `hit.prefab` + `miss.prefab` particle prefabs | Unity `ParticleSystem` prefabs under `Assets/Prefabs/Feedback/` |
| SP4-T8 | `FeedbackDispatcher` MonoBehaviour | Subscribes to `JudgmentSystem.OnJudgmentFeedback`; routes to `HapticService` (gated by `UserPrefs.HapticsEnabled`) + `ParticlePool` (always) |
| SP4-T9 | Settings screen haptic toggle | `SettingsScreen` UI addition; `UserPrefs.HapticsEnabled` bool (default true) |
| SP4-T10 | Scene wiring | `SceneBuilder` / gameplay scene prefab hooks up `FeedbackDispatcher` references |
| SP4-T11 | EditMode tests | `FeedbackDispatcherTests` (5) + `JudgmentSystemTests` extensions (3) |
| SP4-T12 | Device playtest | Galaxy S22 checklist (see §6.3) + profiler GC=0 re-verification |

### 2.2 Out of scope

- **iOS Taptic Engine support** — MVP is Android-only.
- **Nice Vibrations / MoreMountains plugin** — raw `AndroidJavaObject` chosen; zero new deps.
- **Haptic patterns / waveforms** — one-shot `createOneShot(ms, amplitude)` per judgment only.
- **Particle on/off toggle** — only haptic gets a toggle; particles always on.
- **System silent-mode auto-respect** — user's explicit toggle is the only gate.
- **Hold completion bonus feedback** — Hold start already fires through `HandleTap`; Hold success is silent by design.
- **Hold sustain glow / streak particle while held** — deferred to future audio/visual polish SP.
- **Empty-lane tap feedback** — taps with no pending note in the window remain silent (current `HandleTap` behavior preserved at `closest == null`).
- **Velocity-sensitive feedback** — v10 fixed sample carries forward from SP1; no per-tap dynamics.
- **Judgment-name text popup ("PERFECT!")** — text-popup UX is a separate polish SP.
- **Combo milestone feedback** (e.g., 50-combo buzz) — gamification layer, post-MVP.
- **Inspector color-picker UI exposed to end users** — tuning happens in Editor/ScriptableObject; no runtime customization.

### 2.3 Guardrails (non-regression contracts)

- **GC=0 during 2-min Entertainer Normal session** — carried forward from SP3. Every added code path must be zero-alloc at steady state.
- **Tap→audio latency path unchanged.** `TapInputHandler.FirePress/FirePressRaw` keeps its existing `samplePool.PlayForPitch` call site and ordering. New feedback hooks attach **downstream** of the existing judgment event — they never sit on the audio-input latency path.
- **`HandleHoldBreak` signature unchanged.** Hold-break particle position is reconstructed from `(lane, judgment-line-y)` inside `JudgmentSystem` — no new parameter threading into `HoldTracker`.
- **`JudgmentSystem` game logic unchanged.** Miss early-return in `HandleTap` (line 93) is modified to fire the feedback event **then** return — score-keeping behavior identical.
- **Existing 114 EditMode tests remain green** without modification.

---

## 3. Approach

### 3.1 Design decisions (from brainstorming)

| Decision | Chosen | Rejected alternatives |
|---|---|---|
| Scope bundling | Haptic + particle as one SP | Haptic-only / particle-only / +Settings screen expansion |
| Trigger policy | **Per-judgment tiered** (P/G/G/Miss, 4 intensities) | Every tap / judged-hits-only / asymmetric (haptic=Miss, particle=hit) |
| Miss coverage | Judgment Miss + Auto Miss + Hold break | +empty-lane tap / judgment Miss only / +Hold break only |
| Haptic tech | `AndroidJavaObject` + `VibrationEffect` (API 26+) | `Handheld.Vibrate()` — no amplitude control; third-party plugin — over-kill for Android-only MVP |
| Particle prefabs | 2 (hit w/ runtime tint + separate miss) | 1 fully-parametric / 4 per-judgment |
| Settings toggle | Haptic on/off only | Haptic+particle toggles / no toggle / silent-mode auto-detect |
| Hold scope | Start tap only | +Hold completion / +sustain glow |

### 3.2 Dispatch architecture

```
JudgmentSystem.HandleTap          HoldTracker.Tick
  ├─ (Perfect/Great/Good/Miss)      └─ HoldTransition{Broken}
  │     └─ OnJudgmentFeedback        └─ JudgmentSystem.HandleHoldBreak
  │          ↓                             └─ OnJudgmentFeedback(Miss, ...)
  │  HandleAutoMiss
  │     └─ OnJudgmentFeedback(Miss, notePos)
  ↓
FeedbackDispatcher (MonoBehaviour, subscriber)
  ├─ if (UserPrefs.HapticsEnabled) HapticService.Fire(judgment)
  └─ ParticlePool.Spawn(judgment, worldPos)
        ├─ hit pool (16) for Perfect/Great/Good
        └─ miss pool (4) for Miss
```

**Separation of concerns:** `JudgmentSystem` raises one event and stays pure-C#-testable (no `UnityEngine`-specific feedback calls in the judgment path). `FeedbackDispatcher` owns the fan-out. Presets live in a `ScriptableObject` so tuning doesn't require code changes.

### 3.3 Rejected architectural alternatives

- **Direct coupling** (`JudgmentSystem` calls `hapticService.Fire(...)` / `particlePool.Spawn(...)` inline). Rejected: breaks `JudgmentSystem`'s current EditMode-testable purity and creates 3+ places to mock when testing judgment logic.
- **Bus / pub-sub framework (e.g., UniRx, MessagePipe).** Rejected: YAGNI, and adds new dependency conflicting with "zero new deps" guardrail.
- **Components on each `NoteController` that self-fire feedback.** Rejected: fan-in pattern is simpler to reason about and to keep zero-alloc; also, Auto Miss / Hold break fire without a Note reference in some paths.

---

## 4. Components

### 4.1 New files

| Path | Type | Responsibility |
|---|---|---|
| `Assets/Scripts/Feedback/JudgmentFeedbackEvent.cs` | `readonly struct` | `(Judgment kind, Vector3 worldPos)` payload. Value type → zero-alloc invocation. |
| `Assets/Scripts/Feedback/FeedbackDispatcher.cs` | `MonoBehaviour` | Subscribes/unsubscribes in `OnEnable`/`OnDisable`. Dispatches to haptic (gated) + particle (always). |
| `Assets/Scripts/Feedback/HapticService.cs` | `MonoBehaviour` | Caches 4 `VibrationEffect` `AndroidJavaObject`s in `Awake`. `Fire(Judgment)` selects and calls `AndroidHapticsBridge.Vibrate(effect)`. Respects `vibrator.hasVibrator()`. |
| `Assets/Scripts/Feedback/AndroidHapticsBridge.cs` | `static` | Thin `AndroidJavaObject` wrapper. `#if UNITY_ANDROID && !UNITY_EDITOR` real calls; else no-op. |
| `Assets/Scripts/Feedback/ParticlePool.cs` | `MonoBehaviour` | Round-robin pools: 16 `hit`, 4 `miss`. `Spawn(Judgment, Vector3)` activates, positions, sets tint (hit only), plays particle, auto-returns via `ParticleSystem.main.stopAction = Callback` or lifetime check. |
| `Assets/ScriptableObjects/FeedbackPresets.asset` | ScriptableObject asset | Inspector-authored preset table. |
| `Assets/Scripts/Feedback/FeedbackPresets.cs` | `ScriptableObject` | Class definition backing the asset. Fields: per-judgment `{durationMs, amplitude, tintColor, startSize, burstCount}`. |
| `Assets/Prefabs/Feedback/hit.prefab` | `ParticleSystem` prefab | White default burst, runtime tint applied. |
| `Assets/Prefabs/Feedback/miss.prefab` | `ParticleSystem` prefab | Red inward-collapse motif; fixed color/size. |

### 4.2 Modified files

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/JudgmentSystem.cs` | Add `public event Action<Judgment, Vector3> OnJudgmentFeedback`. Invoke in `HandleTap` (for all 4 outcomes — Miss now fires the event before its early return), `HandleAutoMiss`, `HandleHoldBreak`. Hold-break position computed as `new Vector3(LaneLayout.LaneToX(lane), GameTime.JudgmentLineY, 0)`. |
| `Assets/Scripts/Common/UserPrefs.cs` | Add `HapticsEnabled` property backed by `PlayerPrefs.GetInt("haptics_enabled", 1)` / `SetInt`. |
| `Assets/Scripts/UI/SettingsScreen.cs` | Add haptic on/off toggle row. Bind `onValueChanged` → `UserPrefs.HapticsEnabled`. |
| `Assets/Scripts/Gameplay/HoldTracker.cs` | **No code change** — Hold-break position is computed inside `JudgmentSystem.HandleHoldBreak` from lane, not passed in from HoldTracker. `HandleHoldBreak` gets the lane via the transition id → existing pending-note lookup. |
| Gameplay scene / `SceneBuilder` | Wire new `FeedbackDispatcher`, `HapticService`, `ParticlePool` MonoBehaviours onto scene root; assign `judgmentSystem` SerializeField refs. |

**Note on `HandleHoldBreak` lane lookup:** current signature is `HandleHoldBreak()` (parameterless). The lane needs to come from somewhere. Options:
- **Option I:** Change signature to `HandleHoldBreak(int lane)` — minimal, HoldTracker already has the broken note's lane.
- **Option II:** Change signature to `HandleHoldBreak(NoteController brokenNote)` — gives worldPos directly.
- **Option III:** Keep signature, store "last broken lane" on HoldTracker.

**Choice:** Option II. Passes the `NoteController` directly, so worldPos = `brokenNote.transform.position` is readily available and more accurate than reconstructing from `(lane, judgment-line-y)`. Signature change is small: one caller (`HoldTracker` line 51) and one callee. Miss particle fires at the note's actual on-screen position at the moment of break, which is closer to where the player's attention is. **§4.2 HoldTracker.cs "no code change" row revises to: call-site update `judgmentSystem.HandleHoldBreak(brokenNote)`**.

### 4.3 Preset defaults (starting values, tunable in Editor)

Haptic (`VibrationEffect.createOneShot(ms, amplitude)`):

| Judgment | duration (ms) | amplitude (0-255) |
|---|---|---|
| Perfect | 15 | 200 |
| Great | 10 | 120 |
| Good | 8 | 60 |
| Miss | 40 | 180 |

Particle (`hit.prefab` tint + common params; `miss.prefab` fixed):

| Judgment | prefab | tintColor | startSize | burstCount |
|---|---|---|---|---|
| Perfect | hit | white (1,1,1,1) | 0.45 | 16 |
| Great | hit | light-blue (0.7,0.9,1,1) | 0.32 | 10 |
| Good | hit | light-green (0.8,1,0.8,1) | 0.22 | 6 |
| Miss | miss | red (prefab) | 0.30 (prefab) | 8 (prefab) |

**All values are starting points** — device playtest in §6.3 will drive final tuning, which lands as `FeedbackPresets.asset` data changes (no code rebuild).

---

## 5. Data flow

### 5.1 Path 1 — Perfect / Great / Good tap

1. `TapInputHandler.FirePress` → `samplePool.PlayForPitch` (audio, latency-critical, unchanged).
2. `OnLaneTap` event → `JudgmentSystem.HandleTap(tapTimeMs, tapLane)`.
3. Evaluator returns `{Judgment.Perfect|Great|Good, deltaMs}`.
4. `score.RegisterJudgment(...)`, `LastJudgment = ...`, `LastDeltaMs = ...`.
5. **New:** `OnJudgmentFeedback?.Invoke(result.Judgment, closest.transform.position)`.
6. `pending.Remove(closest)` + `MarkJudged` / `MarkAcceptedAsHold`.

### 5.2 Path 2 — Judgment Miss (in-window but timing too off)

1–4 as above; step 3 returns `Judgment.Miss`.
5. **New:** `OnJudgmentFeedback?.Invoke(Miss, closest.transform.position)`.
6. **Existing:** `return` (no score change, `closest` stays in `pending`). Preserves current game-logic contract.

### 5.3 Path 3 — Auto Miss

1. `NoteController.Update` detects `songTimeMs > HitTimeMs + maxWindow`.
2. → `JudgmentSystem.HandleAutoMiss(note)`.
3. `pending.Remove(note)`, `score.RegisterJudgment(Miss)`.
4. **New:** `OnJudgmentFeedback?.Invoke(Miss, note.transform.position)`.

### 5.4 Path 4 — Hold break

1. `HoldTracker.Update` → `HoldStateMachine.Tick` yields `HoldTransition{Broken, id}`.
2. HoldTracker looks up the note by id (already does so to call `MarkHoldBroken` / equivalent), calls `judgmentSystem.HandleHoldBreak(brokenNote)`.
3. `score.RegisterJudgment(Miss)`.
4. **New:** `OnJudgmentFeedback?.Invoke(Miss, brokenNote.transform.position)`.

### 5.5 Dispatcher consumption

```csharp
void OnEnable()  { judgmentSystem.OnJudgmentFeedback += Handle; }
void OnDisable() { judgmentSystem.OnJudgmentFeedback -= Handle; }

void Handle(Judgment j, Vector3 worldPos) {
    if (UserPrefs.HapticsEnabled && hapticService != null) hapticService.Fire(j);
    if (particlePool != null) particlePool.Spawn(j, worldPos);
}
```

---

## 6. Error handling, platform gates, testing

### 6.1 Platform gates

```csharp
// AndroidHapticsBridge.cs
public static void Vibrate(AndroidJavaObject effect) {
#if UNITY_ANDROID && !UNITY_EDITOR
    // vibrator.Call("vibrate", effect)
#else
    // no-op: Editor Play Mode, Standalone
#endif
}
```

`HapticService.Awake` checks `Build.VERSION.SDK_INT >= 26` and `vibrator.hasVibrator()`. If either fails, every subsequent `Fire` call is a cheap no-op (one bool check).

### 6.2 Null-guards

- `FeedbackDispatcher` SerializeFields (`judgmentSystem`, `hapticService`, `particlePool`): `Debug.LogError` in `OnEnable` if any is null; every subsequent `Handle` invocation early-returns. Mirrors `JudgmentSystem.OnEnable` holdTracker pattern.
- `UserPrefs.HapticsEnabled` first read: `PlayerPrefs.GetInt("haptics_enabled", 1)` defaults to on.
- `ParticlePool.Spawn` with a pool that failed to initialize (prefab ref missing): log once, no-op.
- Pool exhaustion: round-robin naturally recycles the oldest in-flight instance — no failure case.

### 6.3 Device verification checklist (Galaxy S22, release APK)

- [ ] Entertainer Normal full run: Perfect / Great / Good haptic intensity distinguishable by feel.
- [ ] Intentional off-timing tap → Miss buzz (40 ms) clearly felt.
- [ ] Auto Miss (hands off during note) → Miss buzz fires at roughly the note's screen position.
- [ ] Für Elise Normal with an intentional Hold break → Miss buzz fires, particle at break position.
- [ ] Particle visuals: Perfect white-big / Great light-blue-medium / Good light-green-small / Miss red-inward all distinguishable at playing distance.
- [ ] Settings → haptic toggle OFF → no vibration; particles still fire.
- [ ] Settings → haptic toggle ON → vibration resumes.
- [ ] Unity Profiler attached: `GC.Collect` count = 0 across a 2-min Entertainer Normal run.
- [ ] APK size < 35 MB.

### 6.4 EditMode tests

**New (~8 cases):**

`FeedbackDispatcherTests`:
- `Dispatches_ToHaptics_WhenHapticsEnabled`
- `Skips_Haptics_WhenHapticsDisabled`
- `AlwaysDispatches_ToParticlePool_RegardlessOfHapticsToggle`
- `ParticlePool_ReceivesCorrectJudgmentKind` (parametrized Perfect/Great/Good/Miss)
- `WorldPosition_ForwardedUnchanged`

`JudgmentSystemTests` extensions:
- `HandleTap_FiresFeedbackEvent_OnPerfect`
- `HandleTap_FiresFeedbackEvent_OnMiss` (checks new event before early-return path)
- `HandleAutoMiss_FiresFeedbackEvent`
- `HandleHoldBreak_FiresFeedbackEvent_WithBrokenNotePosition`

**Mocking strategy:** introduce minimal `IHapticService` and `IParticleSpawner` interfaces; `FeedbackDispatcher` depends on interfaces. EditMode tests inject fake implementations that record invocations. `UserPrefs` interacts with `PlayerPrefs` directly; tests `DeleteKey` in `TearDown`.

**Not unit-testable (device-only):**
- Actual `AndroidJavaObject` native calls — guarded out in Editor.
- Actual `ParticleSystem` rendering — device playtest.

### 6.5 SP3 regression guard

Profiler session during device playtest (checklist item above) — must report `GC.Collect = 0`. If any allocation resurfaces, the two likely suspects:
1. `VibrationEffect` java-side object re-created per Fire instead of cached — fix by confirming the 4 cached effects are constructed exactly once in `Awake` and reused.
2. `ParticleSystem` `.main`/`.emission` struct assignments allocating — fix by using the `var main = ps.main; main.startColor = c;` idiom and verifying via profiler.

---

## 7. Risks and mitigations

| Risk | Likelihood | Mitigation |
|---|---|---|
| `VibrationEffect` per-call allocation leaks into GC | Medium | Cache 4 effects in `Awake` (explicit contract in §6.5); profiler-verify during device playtest |
| Particle `MainModule` struct-copy pattern accidentally allocates | Medium | Unit test cannot catch (Editor mono vs device IL2CPP); verify via device profiler |
| Haptic feels "off" on devices other than S22 (vendor Vibrator quirks) | Medium | Preset-driven → Inspector tuning; flag for revisit during W6 #6 2nd-device test |
| `HandleHoldBreak` signature change breaks other callers | Low | Only caller is `HoldTracker` line 51 — simple. EditMode test confirms compile + regression |
| Particle overdraw causes frame hitches in dense sections | Low-Medium | Pool size (16 hit) sized for Entertainer NPS ~3 × 0.6s lifetime = ~2 concurrent, 8× safety. Device playtest verifies no drops |
| Settings toggle scene not wired → toggle does nothing | Low | Device checklist items explicitly include toggle-off/on round trip |
| Runtime tint on `hit.prefab` doesn't visibly differentiate Good vs Great | Medium | Accept subjectivity; preset defaults allow quick Inspector re-tune; device playtest is the calibration mechanism |

---

## 8. Estimated effort

| Phase | Estimate |
|---|---|
| Code (5 new files + 4 modified) | 1 day |
| Tests (EditMode 8 + interface extraction) | 0.5 day |
| Prefab authoring (hit.prefab + miss.prefab) | 0.5 day |
| Device playtest + Inspector tuning | 0.5 day |
| Profiler re-verify + APK + completion report | 0.5 day |
| **Total** | **~2.5 days** (~W6 SP3 scale) |

---

## 9. Rollback / exit criteria

**Green-light merge if:**
- EditMode ≥ 122 tests green.
- Device checklist §6.3 all 9 items pass.
- APK < 35 MB.
- Profiler confirms `GC.Collect` = 0.
- User sign-off on S22 playtest.

**Rollback triggers (revert branch):**
- Tap→audio latency regresses beyond W1 PoC ±10 ms jitter band.
- Any `GC.Collect` activity re-appears on device.
- Haptic vendor quirk on S22 makes the feature unusable (fall back: disable haptic entirely, ship particle-only; demote `UserPrefs.HapticsEnabled` default to false).
- Particle visuals cause measurable frame drops during Entertainer Normal peak density.

---

## 10. Follow-ups (explicit, not in this SP)

- 2nd-device haptic tuning pass after W6 #6 mid-tier Android procurement.
- Hold sustain glow (deferred; design question: loop sample vs damp sample vs visual-only from memory).
- Judgment-name text popup ("PERFECT!" flash) — separate UI polish SP.
- iOS Taptic parity if / when iOS MVP target is added (post-MVP).
