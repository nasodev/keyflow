# W3 Completion — 2026-04-20

## Delivered

Turned W2's hardcoded-sequence judgment engine into a data-driven song player. Für Elise Easy (73 notes, 45s) is playable end-to-end on Galaxy S22 with `.kfchart` chart loader, Hold notes, first-launch Calibration, and a Completion panel.

All 16 plan tasks executed via team-agent subagent-driven development — persistent `SpecReviewer` + `CodeReviewer` agents, ephemeral `ImplementerT<N>` per task. Flow: plan-reviewer round (pre-code blockers), TDD-first task implementation, two-stage review per task (spec compliance → code quality), and three device-test iteration fixes before user sign-off.

## Implementation summary

| Plan task | Commit | Deliverable |
|---|---|---|
| 1 | `c0f29a5` | Newtonsoft.Json package + lockfile |
| 2 | `555e9d0` | `ChartData` / `ChartNote` / `ChartDifficulty` types + `NoteType` enum |
| 3 | `b530c8a` | `ChartLoader.ParseJson` pure + 10 tests (spec §3.3-3.4) |
| 4 | `b94d0b8` | Hand-authored Für Elise Easy chart (73 notes, 45s) |
| 5 | `7102f1d` | `HoldStateMachine` pure + 9 tests (spec §4.1 model C) |
| 6 | `44752e6` | `TapInputHandler` — `OnLaneRelease` event + `IsLanePressed` query |
| 7 | `bc4bf91` | `NoteController` Hold rendering + `NoteType`/`dur` fields |
| 8 | `8b2ca1f` | `JudgmentSystem` hands off HOLD start-taps to `HoldTracker` (fail-loud amend) |
| 9 | `7e82703` | `HoldTracker` MonoBehaviour wires state machine to scene (GC amend) |
| 10 | `6cfb052` | `CalibrationCalculator` pure + 7 tests (spec §4.2, pairing-algorithm deviation) |
| 11 | `cf86651` | `CalibrationController` MonoBehaviour + overlay coroutine (pool fix amend) |
| 12 | `b5b6b3c` | `CompletionPanel` + Restart button (spec §4.3) |
| 13 | `184d641` | `NoteSpawner` chart-driven (no more hardcoded sequence) |
| 14 | `2334b30` + `f33cfde` | `GameplayController` bootstrap + completion detection; GoodWindow magic-number dedup |
| 15 | `4c4b0fa` | `SceneBuilder` rewrite for W3 calibration + completion overlays + 2 regression tests |
| 16 infra | `532b72d` | `ApkBuilder` editor script for CLI APK builds |
| 16 device-fix 1 | `104896e` | `ChartLoader` uses `UnityWebRequest` on Android (StreamingAssets jar:file: URI) |
| 16 device-fix 2 | `1e1d5df` | `SceneBuilder` creates `EventSystem` + `InputSystemUIInputModule` |
| 16 device-fix 3 | `a2eedd6` | `CalibrationController.Awake` disables the overlay GameObject |

All tasks passed two-stage review (spec compliance → code quality) by dispatched reviewer subagents before the next task began.

## Test count

- W2 end: 40 EditMode tests
- W3 end: **68 EditMode tests** (40 carry-over + 10 ChartLoader + 9 HoldStateMachine + 7 CalibrationCalculator + 2 JudgmentEvaluator regression)
- Net change: +28 tests
- User-verified **68/68 passing** in Unity Test Runner (EditMode) after Task 15, and re-verified after each of the three device-test fixes.

## Device verification (Galaxy S22, 2026-04-21 KST)

Verified on-device:

- APK build via `ApkBuilder.Build` CLI, foreground Unity batch invocation
- APK size 33 MB (target <40 MB ✅)
- Install via `adb install -r`, streamed
- Für Elise Easy playable end-to-end (chart loads, notes spawn in 4 lanes, judgment registers, completion panel appears)
- Calibration overlay on first launch (fresh PlayerPrefs), saves offset, applies on next launch
- Restart button returns to calibration-skipped gameplay
- User verdict: "확인함" (confirmed) — signed off after 3 iteration fixes

Three bugs surfaced during device test, each fixed in a separate commit:

1. **Android StreamingAssets load failure** — `File.ReadAllText` cannot traverse the `jar:file:/data/app/...base.apk!/assets/…` URI that Android uses for StreamingAssets inside an APK. `ChartLoader.LoadFromStreamingAssets` threw `DirectoryNotFoundException`, which aborted `GameplayController.Start()` so the calibration Start button had no listener. Fixed by switching to `UnityWebRequest.Get` on Android (blocking busy-wait on `isDone` acceptable for boot-time chart load) via `#if UNITY_ANDROID && !UNITY_EDITOR`. — `104896e`
2. **Unity UI Buttons silently ignoring taps** — `SceneBuilder` never created an `EventSystem` GameObject. Without one, no input module runs, `GraphicRaycaster` has no handler to forward clicks to, and `Button.onClick` never fires. Fixed by adding a `CreateEventSystem()` helper that instantiates `EventSystem` + `InputSystemUIInputModule` (project already uses the new Input System end-to-end). — `1e1d5df`
3. **Calibration overlay stays visible on re-launch** — after a successful first calibration, `PlayerPrefs` has the cached offset → `GameplayController` calls `BeginGameplay` directly, bypassing `CalibrationController.Begin()` (which is the only place that would have set the overlay active). But `SceneBuilder` creates the `CalibrationCanvas` with `activeSelf=true`, so on the skip path the overlay and Start button stayed drawn during gameplay. Fixed by adding `private void Awake() { gameObject.SetActive(false); }` to `CalibrationController` — matching `CompletionPanel`'s pattern; `Begin()` already re-enables as its first line. — `a2eedd6`

## Known limitations / accepted quirks (deferred)

- **Mid-game tap drops** — occasional transient, recoverable on Galaxy S22. Suspected cause: frame hitch or Input System polling race under spawner+judgment+audio load. Not investigated this week; accepted for MVP; revisit in W6 polish.
- **ASCII star rendering** (`★★★`, `★★-`, etc. shown as `**-`-style) — plan-compliant MVP stand-in per Task 15 Step 15.2; replaced by icon sprites in W4 or W6 polish.
- **Locale inconsistency** — Calibration overlay prompt is Korean ("화면 아무 곳이나, 클릭 소리에 맞춰 8번 탭하세요."), CompletionPanel labels are English ("SONG COMPLETE", "Restart"). W4 polish.
- **No dedicated click sample** — `piano_c4.wav` is reused as the Calibration click sample. Works but is aurally heavier than a true click. W4.
- **`BuildPipeline.BuildPlayer` reports 653 MB total size** in build log (Unity's `report.summary.totalSize` includes intermediate artifacts). The on-disk APK is the correct 33 MB; the 40 MB spec target is measured on the APK, not on total build size. Just a log-reading gotcha for future reference.

## W3 completion criteria check

Per spec §8:

- [x] `beethoven_fur_elise.kfchart` exists in StreamingAssets and validates via `ChartLoader`
- [x] `NoteSpawner` consumes `ChartData` (no more hardcoded sequence)
- [x] Hold notes exhibit full state machine on device: COMPLETED / BROKEN / MISSED observably distinct
- [x] Calibration overlay appears on first launch, saves offset, applies to subsequent sessions
- [x] Completion panel appears when song ends; Restart works
- [x] 68 EditMode tests all passing (spec target was ~65)
- [x] Galaxy S22 device checklist fully checked (after 3 iteration fixes)
- [x] W3 completion report committed (this file)
- [x] `main` carries commits for all 16 plan tasks + 3 device-fix commits

## Next step

Plan 4 / W4: Main scene + song list + Settings screen (with Calibration re-run entry point) + real Results screen that replaces the `CompletionPanel` MVP stand-in. Plan 4 document to be written via `writing-plans` skill once W3 is signed off.
