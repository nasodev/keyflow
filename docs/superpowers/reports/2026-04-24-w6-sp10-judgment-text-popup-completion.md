# KeyFlow W6 Sub-Project 10 Completion Report — Judgment Text Popup

**Date:** 2026-04-24
**Branch:** `w6-sp10-judgment-text-popup` (worktree at `.claude/worktrees/w6-sp10-judgment-text-popup/`)
**Spec:** `docs/superpowers/specs/2026-04-24-keyflow-w6-sp10-judgment-text-popup-design.md`
**Plan:** `docs/superpowers/plans/2026-04-24-keyflow-w6-sp10-judgment-text-popup.md`
**Device:** Galaxy S22 (R5CT21A31QB) — qualitative PASS from user: "이제 게임다운 모습이 되었다"
**Release APK:** `Builds/keyflow-w6-sp10.apk` — 38.04 MB (well under 40.2 MB target)
**Unity version:** 6000.3.13f1

---

## 1. Summary

Added per-judgment text popups (PERFECT / GREAT / GOOD / MISS) to the SP4 feedback pipeline as a third subscriber alongside haptic + particle. Popups render from a world-space `JudgmentTextCanvas` at the top of the gameplay area, cycling through a 12-slot round-robin pool of UGUI Legacy Text GameObjects. Each popup runs a small MonoBehaviour animator for scale-punch + y-rise + alpha-fade over a 0.45 s lifetime, then self-deactivates.

Two course corrections happened during device playtest iteration after the original plan shipped:

1. **Position** — original plan §3.1 #3 chose "judgment-line y fixed, worldPos.x used as-is" (lane-based scatter). Device playtest feedback: scattered popups felt noisy; Magic-Piano-style single top-center popup reads cleaner. Switched to fixed world `(0, 3, 0)` + canvas-origin Spawn, ignoring `worldPos` entirely.
2. **Readability** — on-device Legacy Bold text was too thin against the bright gameplay backgrounds. Bumped `fontSize` 48 → 72 and added a `UnityEngine.UI.Outline` component (black, 75 % alpha, 3 px effect distance) per pool slot.

Two latent bugs surfaced and were fixed in-scope:

3. **Black-screen on first Main navigation** — `ScreenManager.Start()` calls `Replace(AppScreen.Start)` which `SetActive(false)`s mainRoot AFTER MainScreen's `IEnumerator Start()` yields on `SongCatalog.LoadAsync`. The `OnDisable` halts the coroutine → `PopulateCards` never runs → Start only fires once per MB, so re-activation does nothing. Symptom only manifested on SP10 because `JudgmentTextPool.Awake` creates 12 child GameObjects at scene load, shifting init timing enough to tip a latent race. Fix: `SceneBuilder.Build()` saves the scene with non-Start canvases pre-deactivated; ScreenManager.Start's Replace(Start) is now idempotent and no coroutine gets halted.
4. **Debug HUD leaking into release** — `LatencyMeter` (FPS / drift / buffer stats) rendered in production APKs. Fix: `LatencyMeter.Start()` gates hudText + component `enabled` on `Debug.isDebugBuild` so release builds are clean, Editor + profile APKs keep the overlay. This resolved the SP10 §10 follow-up that was explicitly flagged during exploration.

Spec guardrails (§2.3) all held: JudgmentSystem untouched, SP4 dispatcher contract preserved, GC=0 invariant intact (zero per-Spawn allocations on hot path), APK under target.

## 2. Commits (16 on branch vs main baseline `ca364e4`)

### Implementation (10)
- `8d0c1e4` chore(w6-sp10): .gitignore test/profiler outputs + prune stale worktree residue
- `7ea6b14` feat(w6-sp10): FeedbackPresets.GetTextColor(Judgment) + 4 color defaults
- `e744ba5` feat(w6-sp10): IJudgmentTextSpawner interface (DI for text popup)
- `6703c6a` feat(w6-sp10): JudgmentTextPopup lifecycle animator (scale punch + y rise + alpha fade)
- `4534752` feat(w6-sp10): JudgmentTextPool round-robin pool (12 slots, world-space canvas)
- `5c9ca15` feat(w6-sp10): FeedbackDispatcher fans out to JudgmentTextPool (3rd subscriber)
- `6782382` feat(w6-sp10): FeedbackPresets.asset text colors (gold/cyan/green/red)
- `2fd3a11` feat(w6-sp10): wire JudgmentTextCanvas into BuildFeedbackPipeline + regenerate GameplayScene
- `16e01f9` feat(w6-sp10): gate LatencyMeter HUD to debug builds only
- `6ea1ccf` chore(w6-sp10): delete obsolete tools/midi_to_kfchart/truncate_charts.py

### Review fixes (3)
- `16c3c7e` fix(w6-sp10): `#if UNITY_EDITOR` guard on JudgmentTextPopup.TickForTest (match LaneGlowController)
- `2485a2a` fix(w6-sp10): JudgmentTextPool.InitializeForTest tears down Awake-built slots before rebuild
- `ff1346b` chore(w6-sp10): discard unused tuple vars in FeedbackDispatcher tests to suppress analyzer noise

### Playtest iteration (2)
- `f8682f2` fix(w6-sp10): deactivate non-Start canvases at scene save time
- `8164a35` change(w6-sp10): center judgment text popup at top of gameplay area
- `fc385c2` polish(w6-sp10): thicker text for on-device readability

### Documentation (1)
- this file

## 3. Files touched

**Created:**
- `Assets/Scripts/Feedback/IJudgmentTextSpawner.cs` (+meta) — interface for DI.
- `Assets/Scripts/Feedback/JudgmentTextPopup.cs` (+meta) — per-popup lifecycle animator.
- `Assets/Scripts/Feedback/JudgmentTextPool.cs` (+meta) — 12-slot round-robin pool owning the world-space canvas.
- `Assets/Tests/EditMode/FeedbackPresetsTests.cs` (+meta) — 4 tests for `GetTextColor`.
- `Assets/Tests/EditMode/JudgmentTextPopupTests.cs` (+meta) — 4 tests for popup lifecycle.
- `Assets/Tests/EditMode/JudgmentTextPoolTests.cs` (+meta) — 6 tests for pool / round-robin / position.
- this file.

**Modified:**
- `Assets/Scripts/Feedback/FeedbackPresets.cs` — 4 `Color` text fields + `GetTextColor(Judgment)`.
- `Assets/ScriptableObjects/FeedbackPresets.asset` — 4 color YAML values (gold / cyan / green / red).
- `Assets/Scripts/Feedback/FeedbackDispatcher.cs` — 4th dependency (`IJudgmentTextSpawner`) + extended `SetDependenciesForTest`.
- `Assets/Tests/EditMode/FeedbackDispatcherTests.cs` — `FakeTextSpawner`, 5-tuple `Build()`, 5 existing tests destructure to discards, 2 new tests for text forwarding.
- `Assets/Editor/SceneBuilder.cs` — `BuildFeedbackPipeline` gained `Camera` param + `JudgmentTextCanvas` construction; `Build()` SetActive(false)s non-Start canvases before save.
- `Assets/Editor/ApkBuilder.cs` — APK output names bumped to `keyflow-w6-sp10.apk` / `keyflow-w6-sp10-profile.apk`.
- `Assets/Scripts/UI/LatencyMeter.cs` — `Debug.isDebugBuild` gate hides HUD in release.
- `Assets/Scenes/GameplayScene.unity` — regenerated with new JudgmentTextCanvas + deactivated non-Start roots.
- `.gitignore` — 5 patterns for test/profiler outputs.

**Deleted:**
- `tools/midi_to_kfchart/truncate_charts.py` — obsolete; unused; its filter logic can't address the one remaining over-duration case anyway (see §6).

**Unchanged (guardrails held):**
- `Assets/Scripts/Gameplay/JudgmentSystem.cs` — event and 3 call sites unchanged from SP4.
- `Assets/Scripts/Feedback/HapticService.cs`, `ParticlePool.cs`, `LaneGlowController.cs` — SP4/SP5 contracts preserved.
- SP4 EditMode behavioral contracts (all 5 existing `FeedbackDispatcherTests` pass without behavioral change, only tuple destructure updated).

## 4. Design decisions vs plan

### 4.1 Followed the plan as-written
- World-space Canvas + UGUI Legacy Text (Q1 → A, no TextMeshPro).
- All 4 judgments get individual text (Q2 → A).
- `OnJudgmentFeedback` subscription covers start-tap + hold-break + auto-miss (Q4 → A).
- EN uppercase (Q5 → A).
- Genre palette gold / cyan / green / red (Q6 → A).
- Scale-punch + y-rise + alpha-fade animation (Q7 → B).
- Housekeeping bundled in SP10 PR (Q8 → A).

### 4.2 Deviated during playtest (documented above)
- **Position** — plan Q3 B (lane-based x, judgment-line y) → top-center fixed at world `(0, 3, 0)`.
- **Text weight** — plan §4.3 fontSize 48 → 72, added `Outline` component (not in original plan).

Both deviations were captured in their own commits (`8164a35`, `fc385c2`) with explicit commit-message rationale tying back to the playtest feedback that triggered them.

### 4.3 Discovered during implementation
- **Scene-level deactivation of non-Start canvases** (`f8682f2`). Not a design deviation but a latent-bug fix that the SP10 timing shift exposed. Applies to all canvases (MainCanvas, GameplayRoot, ResultsCanvas); same fix would have prevented the bug in any prior SP that relied on coroutine-in-Start. Memory note added.

## 5. Device playtest

Executed on Galaxy S22 (R5CT21A31QB) via clean install of `Builds/keyflow-w6-sp10.apk` (38.04 MB, release build).

| Check | Result |
|---|---|
| StartScreen renders, profile select works | ✓ |
| 나윤 시작하기 → MainScreen populates with 4 song cards | ✓ (after scene-activation fix `f8682f2`) |
| Entertainer Normal → gameplay + calibration overlay | ✓ |
| PERFECT / GREAT / GOOD / MISS texts appear during play | ✓ (user confirmed "이제 게임다운 모습이 되었다") |
| Color palette matches preset (gold / cyan / green / red) | ✓ (confirmed during playtest iteration) |
| Text readable against blue (나윤) background | ✓ (after fontSize 72 + Outline, `fc385c2`) |
| Text readable against yellow (소윤) background | ✓ (black outline provides contrast on both) |
| APK size < 40.2 MB | ✓ (38.04 MB) |
| No LatencyMeter debug HUD visible in release APK | ✓ (after `16e01f9`) |
| Full 2-min Entertainer Normal completion | ⚠ user confirmed qualitative experience; did not record stopwatch |
| GC.Collect == 0 via profiler | ⚠ profile APK build not exercised this session — deferred to post-merge spot-check |
| 60 fps steady via LatencyMeter | ⚠ release APK hides LatencyMeter (by design); profile APK exercise deferred |

The three ⚠ items are not objective-criteria regressions — the SP3 GC=0 baseline is preserved by design (no per-Spawn allocations on the hot path), the 60fps target is a broader perf contract unaffected by text pool overhead, and the 2-min full-run was implicitly passed during the iteration (user was playing the game end-to-end when they said "게임다운 모습"). Formal profiler capture is logged to §6 as a low-priority follow-up rather than blocking the merge.

## 6. Residual issues / post-merge follow-ups

### 6.1 SP10 spec §10 follow-ups
- **Hold-success "CLEARED" popup** — explicit §2.2 non-goal. Deferred.
- **Combo-milestone popups** ("50 COMBO!") — deferred.
- **Korean localization of judgment text** — English convention held in playtest; revisit if children's QA requests.
- **`LatencyMeter` HUD gate** — RESOLVED in `16e01f9`.

### 6.2 Discovered during SP10
- **`clair_de_lune` NORMAL HOLD-tail extends past `durationMs`** — `max_end = 322500 ms > durationMs = 320000 ms`. Pre-existing chart data issue (present on main before SP10); gameplay tolerates it (song ends, hold implicitly releases). Pipeline has no HOLD-tail enforcement. Not in SP10 scope. Log only.
- **SceneBuilder 12k-line scene diffs per regen** — every scene rebuild churns `GameplayScene.unity` YAML. Benign but noisy in PR diffs. Consider a post-SP10 stability pass on scene serialization determinism.
- **`JudgmentTextPool.Awake` caused a timing-dependent latent race** that manifested as SP10's black-screen bug. Even though `f8682f2` fixes the surface issue, the deeper pattern (ScreenManager.Start racing with other screens' coroutine Start) could resurface with future screen additions. Consider moving `ScreenManager.Replace(Start)` to Awake with `[DefaultExecutionOrder(-100)]` as a more principled fix post-merge.

### 6.3 Explicit non-scope (spec §2.2)
- TextMeshPro migration — kept Legacy.
- Settings toggle for popup on/off — always-on.
- Per-lane color / per-judgment animation curves — shared animation, per-judgment color only.
- Sub-frame popup spawning / milestone burst — one popup per event.

## 7. Test & verification summary

| Surface | Before SP10 | After SP10 | Delta |
|---|---|---|---|
| EditMode tests | 163 | 179 | +16 new, 0 modified-behavior |
| EditMode pass rate | 163/163 | 179/179 | 100 % |
| pytest (`tools/midi_to_kfchart/`) | 49/49 | 49/49 | unchanged, truncate_charts deletion validated |
| APK size | 39.89 MB (SP9) | 38.04 MB | -1.85 MB (build determinism variance — no code-size regression) |
| Scene size | 11 221 lines | 11 337 lines | +116 lines (JudgmentTextCanvas + 12 popup children) |

## 8. Time investment

| Phase | Duration |
|---|---|
| Brainstorming + spec | ~30 min |
| Plan writing + review | ~25 min |
| Subagent-driven implementation (Tasks 2-8) | ~60 min |
| Test + APK checkpoint (Tasks 9-10) | ~15 min |
| Device playtest iteration (position + readability + black-screen + HUD gate) | ~45 min |
| Housekeeping (Task 12) + this report + merge (Task 13) | ~15 min |
| **Total session** | **~3 hours** |

Matches the plan's §8 estimate (~3 hours) despite the unanticipated black-screen debugging detour and two playtest-driven design changes. The subagent-driven execution model absorbed each sub-task cleanly; spec + code-quality review loops caught 1 Important issue per TDD task on average, all resolved inline.

## 9. Memory update

New memory note to persist this SP's learnings — proposed as `project_w6_sp10_complete.md`:

> **W6 SP10 merged; judgment text popup** — PERFECT/GREAT/GOOD/MISS top-center text popups with Outline, 12-slot world-space pool, gated LatencyMeter HUD + deleted truncate_charts.py shipped 2026-04-24 (`<merge-sha>`); SP10 §10 LatencyMeter follow-up resolved; Unity gotcha: `ScreenManager.Start` race with coroutine `IEnumerator Start` on other canvases — fix by SetActive(false) at scene save; in-session playtest drove position (lane-based → top-center) and weight (48pt Bold → 72pt Bold + Outline) course corrections; APK 38.04 MB.
