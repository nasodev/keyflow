# KeyFlow W6 Sub-Project 6 — Gameplay Visual Polish (Tiles / Combo / Background)

**Date:** 2026-04-22
**Week:** 6 (폴리싱 + 사운드)
**Priority:** W6 #5 (derived from user feedback during SP5 device playtest; replaces abstract "UI polish" carry-over with three concrete items)
**Status:** Proposed

---

## 1. Motivation

SP5 device playtest surfaced that the gameplay scene looks visually underdeveloped compared to reference Magic-Tiles-genre apps. User-supplied reference showed three concrete deltas:

1. **Tiles don't fill the screen.** Current `LaneAreaWidth = 4f` world units at camera ortho 8 puts lanes in ~44% of screen width (720×1280), leaving wide empty side margins. Reference tiles span full width.
2. **No live combo counter during gameplay.** `ScoreManager.Combo` is tracked but only surfaced in the end-of-song `ResultsScreen` (via `MaxCombo`). Reference shows a large center-top combo number that builds as the player chains Perfect/Great/Good hits.
3. **Background is flat dark navy.** `camera.backgroundColor = (0.08, 0.08, 0.12, 1)` is functional but visually uninteresting vs. reference's colorful gradient + geometric pattern backgrounds.

These three items fit naturally as a single bundled SP because they all modify the same gameplay-scene composition (`SceneBuilder.cs`, Canvas layout, camera/background). Splitting would triple the spec/plan/review overhead for highly-coupled visual changes.

## 2. Goal

Ship a single cohesive gameplay-scene visual upgrade such that:

- **Tiles fill ~100% of screen width** with full-width-lane touch areas.
- **A live combo HUD** displays the current combo count at center-top, hidden when combo=0.
- **A colorful user-provided background** replaces the flat dark navy (`Assets/Sprites/background_gameplay.png`, ~1 MB source, blue gradient with subtle geometric/cloud layers).
- **No regression** in tap latency, judgment accuracy, multi-pitch audio, or GC-free gameplay (SP3 baseline) — gameplay runtime logic is not touched at all.
- **Existing UI elements** (lane dividers, judgment line, top progress bar, pause button, particle/haptic feedback, calibration overlay) continue to work, with lane dividers and judgment line re-tuned for visual harmony against the new background.

Qualitative success criterion: on Galaxy S22, mid-song Entertainer Normal session, the scene visually reads as "Magic-Tiles-style polished" with clear combo feedback, and the player reports no perceived input or audio regression.

## 3. Non-goals

- User-selectable background themes, multiple gradients, or seasonal skins.
- Animated/parallax background effects.
- Geometric pattern generation (procedural) — user supplied a finished image.
- Combo milestone effects (e.g., 100-combo celebratory burst) — separate SP candidate.
- Tile texture gradients/highlights — kept as flat black placeholder.
- High-resolution asset variants per device DPI — one 941×1672 PNG + Unity auto-scale handles all target phones.
- iOS support — Android-only MVP.
- Removing `piano_c4.wav` or `calibration_click.wav` — unchanged.
- Refactoring score or combo tracking — `ScoreManager.Combo` public getter is consumed as-is.

## 4. Approach

**Three bundled visual changes driven by `SceneBuilder.cs` edits + one new runtime component + one user-supplied asset + one TextureImporter postprocessor.**

### 4.1 Full-width tiles (`LaneAreaWidth` 4f → 9f) + reference-matching note color

At camera `orthographicSize = 8` and 720×1280 screen aspect (0.5625), half-width in world units ≈ 4.5. Thus `LaneAreaWidth = 9f` fills the visible horizontal area end-to-end with zero margin. At `LaneLayout.LaneCount = 4`, each lane is 2.25 world units wide.

Changes:
- `SceneBuilder.LaneAreaWidth` const: `4f` → `9f`.
- `BuildNotePrefab` in `SceneBuilder.cs:1182` — current value `localScale = new Vector3(0.8f, 0.4f, 1)`. New value: **`localScale = new Vector3(LaneAreaWidth / LaneLayout.LaneCount, 0.4f, 1)` = `(2.25f, 0.4f, 1)`.** X scales up 2.81× (0.8 → 2.25) so tiles exactly fill one lane width edge-to-edge. Y stays at 0.4f (tile height unchanged — reference-image tiles are wider, not taller). Result: tile aspect ratio goes from 2:1 (0.8/0.4) to 5.625:1 (2.25/0.4), matching Magic-Tiles look.
- `BuildNotePrefab` color at `SceneBuilder.cs:1185` — current `sr.color = new Color(1f, 0.95f, 0.85f, 1)` (cream/warm white) → **new `sr.color = new Color(0.08f, 0.08f, 0.12f, 1)`** (near-black, slight blue tint matching `camera.backgroundColor` for palette consistency). This matches the user-supplied reference (black tiles) and gives strong contrast against the blue gradient background.
- Downstream: `SetField(tapInput, "laneAreaWidth", LaneAreaWidth)` and `SetField(spawner, "laneAreaWidth", LaneAreaWidth)` calls are already parameterized — value automatically flows through.
- `LaneLayout.LaneToX`, `LaneLayout.XToLane` are already pure functions taking `width` as argument — no code changes.

### 4.2 Live combo HUD

New component `Assets/Scripts/UI/ComboHUD.cs` (uses `UnityEngine.UI.Text` — the Legacy UI Text component, NOT TextMeshPro; matches the rest of this project's UI):

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    public class ComboHUD : MonoBehaviour
    {
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private Text comboText;
        private int lastCombo = -1;

        // Exposed for EditMode test: counts how many times we wrote text.text.
        internal int TextAssignmentCount { get; private set; }

        private void Update()
        {
            if (scoreManager == null || comboText == null) return;
            int current = scoreManager.Combo;
            if (current == lastCombo) return;
            lastCombo = current;

            if (current == 0)
            {
                if (comboText.enabled) comboText.enabled = false;
            }
            else
            {
                if (!comboText.enabled) comboText.enabled = true;
                comboText.text = current.ToString();
                TextAssignmentCount++;
            }
        }

        internal void UpdateForTest() => Update();
    }
}
```

Design notes:
- **Toggle `comboText.enabled`, NOT `gameObject.SetActive`.** Toggling the ComboHUD's own GameObject would stop its `Update` from firing, trapping the HUD in the hidden state forever. Toggling only the `Text` component's `enabled` hides the visual while letting the HUD's Update keep running and re-enable when combo goes non-zero. (The ComboHUD GameObject and the ComboText GameObject may be the same object or parent-child — either works with this approach.)
- **Polling, not event-based.** `ScoreManager` doesn't expose a change event and adding one would touch gameplay runtime. Polling an int is cheap (no alloc) and the Update-gate on `current == lastCombo` short-circuits all frames where combo didn't change.
- **GC-free on unchanged frames** (SP3 baseline preserved). `int.ToString()` allocates on changed frames only. Upper bound: Entertainer Normal is ~400-600 notes = up to ~600 combo changes per song × ~28 bytes ≈ 17 KB total over a full session. Well within the ambient heap budget; managed heap stays stable per SP4 profiling.
- **Hides at combo=0.** At song start, Combo=0 → `comboText.enabled = false`. First Perfect/Great/Good tap → combo=1 → `enabled = true` + `text = "1"`. Miss → combo reset to 0 → `enabled = false`.
- **Placement:** top-center, below the existing ProgressBar, above the SP4 FeedbackPipeline particle layer. Large white `UnityEngine.UI.Text` (fontSize ~96 at 720×1280 Canvas reference, LegacyRuntime.ttf font).
- **`TextAssignmentCount` and `UpdateForTest()` are `internal`** — visible to EditMode tests via `InternalsVisibleTo` (or by keeping the test assembly under the same project which sees `internal` members across asmdefs when the test asmdef references the runtime asmdef). Tests assert on this counter instead of mocking `Text.text`, which is impractical because `UnityEngine.UI.Text.text` is a virtual property on a sealed assembly.

Wiring: `SceneBuilder.BuildHUD` adds a `ComboText` child GameObject with a `UnityEngine.UI.Text` to `HUDCanvas`, adds the `ComboHUD` component to the same `ComboText` GameObject (or a parent container — either is fine because we toggle the Text component, not the GameObject), and `SetField`s the `scoreManager` (constructed earlier in `BuildManagers`) and `comboText` references.

### 4.3 Background image

- **User-supplied asset:** `Assets/Sprites/background_gameplay.png` (941×1672 RGB PNG, 1.02 MB source) — already committed to the repo; plan Task references it as existing, not created.
- **TextureImporter postprocessor** `Assets/Editor/BackgroundImporterPostprocessor.cs` — mirrors `PianoSampleImportPostprocessor` pattern. Forces `textureType = Sprite (2D and UI)`, `mipmapEnabled = false`, `isReadable = false`, platform-specific Android override for ASTC 4×4 compression (minSdk 26 guarantees ASTC hardware support on target devices; no fallback needed in practice). This guarantees import settings survive re-imports and fresh clones.
- **Scene wiring strategy: UI `Image` on a dedicated background Canvas, NOT `SpriteRenderer` in world space.** This removes the scaling-math ambiguity of world-space placement.
  - New `SceneBuilder.BuildBackgroundCanvas` creates `BackgroundCanvas` GameObject:
    - `Canvas` component: `renderMode = RenderMode.ScreenSpaceOverlay`, `sortingOrder = -100` (underneath every other Canvas — existing ones use 5, 10).
    - `CanvasScaler`: `uiScaleMode = ScaleWithScreenSize`, `referenceResolution = (720, 1280)`, `matchWidthOrHeight = 0.5f` (match same convention as existing canvases).
    - Child `GameObject "BackgroundImage"` with:
      - `RectTransform`: `anchorMin = (0, 0)`, `anchorMax = (1, 1)`, `offsetMin = Vector2.zero`, `offsetMax = Vector2.zero` — stretch to fill entire screen.
      - `UnityEngine.UI.Image` component: `sprite = <loaded background_gameplay.png>`, `preserveAspect = false` (fill entire screen without letterboxing; slight horizontal stretch on non-9:16 aspects is acceptable).
  - On 9:16 aspect (S22, 720×1280 reference): image displays without distortion.
  - On 9:19.5 aspect (common modern Android): image stretches ~1.2× vertically — acceptable because the image has soft gradient/cloud content that tolerates stretching; no hard lines that would reveal distortion.
- **Camera backgroundColor** stays at current `(0.08, 0.08, 0.12, 1)` as a defense-in-depth fallback for the rare edge case where the BackgroundCanvas fails to render (e.g., missing sprite). In the normal case the Canvas fully covers the camera view and this color is never visible.

### 4.4 Judgment line + lane dividers retuning

- **Judgment line (`BuildJudgmentLine` at `SceneBuilder.cs:158`):** current `sr.color = new Color(0.2f, 0.9f, 1.0f, 1)` (bright cyan, α=1.0) → new `sr.color = new Color(0.9f, 0.95f, 1.0f, 0.5f)` (white with subtle blue tint, α=0.5). `localScale` stays `(LaneAreaWidth, 0.12f, 1)` — automatically scales with the new `LaneAreaWidth = 9f`.
- **Lane dividers (`BuildLaneDividers` at `SceneBuilder.cs:145`):** current `sr.color = new Color(0.3f, 0.3f, 0.4f, 0.8f)` (dark blue-gray, α=0.8) → new `sr.color = new Color(0.8f, 0.9f, 1.0f, 0.3f)` (blue-tinted white, α=0.3). `localScale` stays `(0.02f, 20f, 1)`. Dividers become thinner-feeling against the new brighter background without being harsh.

### 4.5 Files

**Created (committed):**
- `Assets/Scripts/UI/ComboHUD.cs` — MonoBehaviour component (~30 lines)
- `Assets/Editor/BackgroundImporterPostprocessor.cs` — AssetPostprocessor enforcing Sprite/no-mipmap/ASTC settings (~30 lines)
- `Assets/Sprites/background_gameplay.png` — user-supplied, 1.02 MB
- `Assets/Sprites/background_gameplay.png.meta` — auto-generated after postprocessor runs
- `Assets/Tests/EditMode/ComboHUDTests.cs` — 4 EditMode tests

**Modified:**
- `Assets/Editor/SceneBuilder.cs`:
  - `LaneAreaWidth` const: 4f → 9f
  - `BuildNotePrefab` (line ~1182): `localScale.x` 0.8f → `LaneAreaWidth / LaneLayout.LaneCount` (= 2.25f); Y unchanged at 0.4f
  - `BuildNotePrefab` (line ~1185): `sr.color` cream `(1, 0.95, 0.85, 1)` → near-black `(0.08, 0.08, 0.12, 1)`
  - Load `background_gameplay.png` + null-guard + new `BuildBackgroundCanvas(sprite)` call
  - `BuildLaneDividers` (line ~145): `sr.color` `(0.3, 0.3, 0.4, 0.8)` → `(0.8, 0.9, 1.0, 0.3)`
  - `BuildJudgmentLine` (line ~158): `sr.color` `(0.2, 0.9, 1.0, 1)` → `(0.9, 0.95, 1.0, 0.5)`
  - `BuildHUD`: add ComboHUD sub-pipeline (Text GameObject + ComboHUD component + `SetField` for scoreManager/comboText)
- `Assets/Scenes/GameplayScene.unity` — regenerated by SceneBuilder

**NOT modified (guardrails):**
- `Assets/Scripts/Gameplay/ScoreManager.cs` — `Combo` getter consumed as-is; no new events
- `Assets/Scripts/Gameplay/TapInputHandler.cs` — `laneAreaWidth` SerializeField receives new value; no code change
- `Assets/Scripts/Gameplay/NoteSpawner.cs` — same
- `Assets/Scripts/Gameplay/JudgmentSystem.cs`, `HoldTracker.cs`, `LaneLayout.cs`, `LanePitches.cs` — untouched
- `Assets/Scripts/Calibration/*`, `Assets/Scripts/Feedback/*` — untouched
- `Assets/Editor/W6SamplesWireup.cs` — untouched (but MUST be re-run after SceneBuilder; see implementation plan Task)
- `Assets/Audio/piano_c4.wav`, `Assets/Audio/calibration_click.wav`, all piano samples — untouched

### 4.6 Data flow

**Build-time (developer, one-shot):**

```
KeyFlow/Build W4 Scene (SceneBuilder.Build)
  → Load piano_c4.wav, calibration_click.wav, background_gameplay.png
  → Null-guard each; abort on missing
  → BuildBackgroundCanvas(bgSprite) → ScreenSpaceOverlay Canvas (sortingOrder=-100)
    with full-screen stretch-anchored UI Image child
  → BuildLaneDividers(tuned alpha/color)
  → BuildJudgmentLine(alpha=0.5)
  → BuildNotePrefab(localScale.x = LaneAreaWidth / LaneCount)
  → BuildManagers(... pianoClip=defaultClip, laneAreaWidth=9f)
  → BuildHUD → existing HUD + new ComboHUD wire-up
  → BuildCalibrationOverlay(clickClip, ...)  [unchanged from SP5]

After SceneBuilder.Build():
KeyFlow/W6 Samples Wireup (W6SamplesWireup.Wire)
  → Restore AudioSamplePool.pitchSamples, TapInputHandler.judgmentSystem, SettingsScreen.creditsLabel
  (Idempotent; safe to re-run; SP4 workflow trap mitigation)
```

**Runtime (gameplay frame, GC-sensitive path unchanged):**

```
Tap → TapInputHandler.Update → ScreenToLane(width=9f) → FirePress → JudgmentSystem.HandleTap
  → ScoreManager.Apply(judgment) → Combo++ (or reset to 0 on Miss)
  → JudgmentSystem.OnJudgmentFeedback event → FeedbackDispatcher (SP4 path, unchanged)

ComboHUD.Update (every frame, cheap):
  read scoreManager.Combo (int, no alloc)
  short-circuit if unchanged (99% of frames)
  on change: SetActive toggle + text update (alloc only when combo changes)
```

### 4.7 Import settings (BackgroundImporterPostprocessor)

```csharp
public class BackgroundImporterPostprocessor : AssetPostprocessor
{
    private const string TargetPath = "Assets/Sprites/background_gameplay.png";

    private void OnPreprocessTexture()
    {
        if (assetPath != TargetPath) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.mipmapEnabled = false;
        importer.isReadable = false;

        var androidSettings = importer.GetPlatformTextureSettings("Android");
        androidSettings.overridden = true;
        androidSettings.format = TextureImporterFormat.ASTC_4x4;
        androidSettings.compressionQuality = 50; // Normal (0=Fast, 50=Normal, 100=Best)
        importer.SetPlatformTextureSettings(androidSettings);
    }
}
```

## 5. Error handling & edge cases

**SceneBuilder missing-asset guards:**

- `background_gameplay.png` missing → `Debug.LogError("[KeyFlow] Missing Assets/Sprites/background_gameplay.png. Aborting scene build."); return;`
- Uses same error shape as existing `piano_c4.wav` and `calibration_click.wav` guards — fresh-clone scene rebuild surfaces the missing asset loudly rather than producing a broken scene.

**ComboHUD runtime guards:**

- `scoreManager == null` → early-return in `Update`; component is inert. Doesn't crash gameplay.
- `comboText == null` → `Update` will NullReferenceException on first text assignment. Left unguarded because it's an authoring bug, not a runtime condition; caught by EditMode test `ShowsAndUpdatesTextWhenComboIncreases` which asserts both fields are wired.
- Combo transitions: 0 → 5 shows HUD; 5 → 0 hides; 0 → 0 no-op; 5 → 5 no-op (short-circuit); 5 → 7 updates text. All covered by tests.

**LaneAreaWidth change ripple:**

- `TapInputHandler.ScreenToLane` uses `LaneLayout.XToLane(x, laneAreaWidth)` which is pure and receives width as parameter → no code change, just different numeric output.
- `NoteSpawner` uses `LaneLayout.LaneToX(lane, laneAreaWidth)` — same.
- `LaneLayoutTests` / `NoteSpawnerTests` / `TapInputHandlerTests` may reference `laneAreaWidth` in test setup; if any hardcode `4f`, they keep their own values (tests don't auto-inherit SceneBuilder's const). Implementation plan includes a grep to confirm no hidden coupling.

**Edge display false-touch:**

- Galaxy S22 is a flat display (no edge curvature) → full-width touch is safe. If a future device test reveals oversensitive side edges, fallback is `LaneAreaWidth = 8.5f` (minor pullback), config-only change.

**Background sprite aspect handling:**

- Source 941×1672 ≈ 9:16 portrait. The UI `Image` on BackgroundCanvas uses `preserveAspect = false` and stretch anchors `(0,0)`-`(1,1)`, so the sprite is **uniformly stretched to fill the entire screen** regardless of device aspect. No letterboxing, no `camera.backgroundColor` fallback visible.
- On 720×1280 / 1080×1920 / 1440×2560 (exact 9:16): no distortion.
- On 1080×2340 / 1440×3120 (≈ 9:19.5): ~1.2× vertical stretch. Acceptable because the image content is soft gradients + clouds + subtle diagonal geometric shadows — no hard lines or circular elements that would reveal the stretch.
- On ultrawide / foldable unfolded: further stretch, but still fills the screen. Visual tolerance margin narrows; flagged for W6 #6 2nd-device test.
- `camera.backgroundColor` remains `(0.08, 0.08, 0.12)` as a defense-in-depth fallback only; in the normal case it is never visible because the BackgroundCanvas fully covers the camera view.

**Intentionally not handled (YAGNI):**

- Dynamic background swap at runtime, theme selector, per-song background — out of scope.
- Combo rollback on slight timing errors — `ScoreManager` semantics are the source of truth; ComboHUD is a pure mirror.
- High-DPI scaling of ComboText fontSize — CanvasScaler's `ScaleWithScreenSize` already handles this.

## 6. Testing

### 6.1 EditMode tests (`ComboHUDTests.cs`, 4 tests)

Test setup: each test creates a GameObject with a `UnityEngine.UI.Text` child, adds a `ComboHUD` component, manually injects `scoreManager` and `comboText` via reflection (`SerializedObject` or `GetField("scoreManager", BindingFlags.NonPublic | BindingFlags.Instance)` pattern already used in `FeedbackDispatcherTests` / `JudgmentSystemTests`), and calls `hud.UpdateForTest()` (internal method) to drive the Update path.

| Test | Asserts |
|---|---|
| `HidesWhenComboZero` | Fresh ScoreManager (Combo=0) → UpdateForTest() → comboText.enabled == false |
| `ShowsAndUpdatesTextWhenComboIncreases` | Apply Perfect (Combo=1) → UpdateForTest() → comboText.enabled == true AND comboText.text == "1"; Apply Perfect again (Combo=2) → UpdateForTest() → comboText.text == "2" |
| `HidesWhenComboResetsToZero` | Drive Combo 0 → 3 (enabled==true) → Apply Miss (Combo=0) → UpdateForTest() → comboText.enabled == false |
| `DoesNotReassignTextWhenComboUnchanged` | After Combo=5, record `TextAssignmentCount`; call UpdateForTest() 5 more times without changing ScoreManager; assert `TextAssignmentCount` unchanged |

Test fixtures will use `new ScoreManager()` + `Initialize(totalNotes=10)` + `Apply(Judgment.X)` to drive Combo — same pattern as existing `ScoreManagerTests`. The `TextAssignmentCount` + `UpdateForTest()` hooks on `ComboHUD` are deliberately `internal` to avoid complicating tests with `UnityEngine.UI.Text` mocking.

### 6.2 Intentionally NOT tested in EditMode

- **BackgroundImporterPostprocessor behavior.** Mirrors `PianoSampleImportPostprocessor` which also lacks unit tests; tested implicitly by verifying the imported asset's importer settings after a one-time import via `AssetImporter.GetAtPath` in Editor REPL or device playtest.
- **Background sprite aspect / scale rendering.** Visual concern → device checklist.
- **Tile `localScale.x` numeric value.** Scene asset inspection is fragile under Unity serialization; verified via SceneBuilder build log + device visual.
- **Judgment line alpha / lane divider color.** Same — visual only, device checklist.
- **APK size delta.** Measured at build time in Task 10 of plan, not EditMode.

### 6.3 Device checklist (Galaxy S22)

1. ✅ Fresh install → gameplay entry → background is blue gradient + geometric pattern + bottom cloud glow (user-supplied image).
2. ✅ Tiles fill screen width edge-to-edge; no visible side margins.
3. ✅ Judgment line faintly visible (not gone, not harsh).
4. ✅ Lane dividers harmonize with background (not competing for attention).
5. ✅ ComboHUD: hidden at song start; appears on first Perfect/Great/Good; updates live; hides instantly on Miss.
6. ✅ No tap latency / judgment regression vs SP5 (subjective user ear + reaction).
7. ✅ GC-free gameplay: no visible frame hitch during a 2-minute session; ComboHUD updates don't stall.

### 6.4 Test count target

- Baseline (SP5 merged): 131
- Target (SP6 merged): **135** (+4, all in ComboHUDTests)
- Existing 131 must all remain green throughout.

## 7. Risks & rollback

| Risk | Likelihood | Mitigation |
|---|---|---|
| Edge-display false-touch on full-width tiles | Low on S22 (flat); unknown on curved-edge devices | Fallback: `LaneAreaWidth = 8.5f` — one-line config change |
| ComboHUD overlaps ProgressBar or PauseButton | Medium | RectTransform anchor tuning in SceneBuilder; device checklist item 5 catches it |
| Background too visually dominant; tiles unreadable | Medium | User-supplied image already vetted against this concern (high contrast vs black tiles); fallback is to reduce sprite alpha to 0.85 |
| Unity ASTC compression degrades image quality visibly | Low | Fallback to `TextureImporterFormat.RGB24` (uncompressed) in postprocessor — larger APK, acceptable |
| `W6SamplesWireup` not re-run after scene rebuild → silent multi-pitch loss | Medium (SP4-known trap) | Plan mandates re-run as explicit task; scene diff verified |
| Test count stays at 131 (ComboHUD tests fail silently) | Low | Test run command parses `test-results.xml`; failures surface as exit code + XML |

**Rollback (per component):**

- Revert SceneBuilder's `LaneAreaWidth` back to 4f, note prefab `localScale` back to `(0.8f, 0.4f, 1)`, note color back to cream `(1f, 0.95f, 0.85f, 1)` → full-width tiles disappear.
- Revert `BuildBackgroundCanvas` call + `background_gameplay.png` load → BackgroundCanvas not created; `camera.backgroundColor` fallback becomes the visible background (dark navy).
- Delete ComboHUD wire-up in SceneBuilder + unused `ComboHUD.cs` stays dormant (no runtime cost if not instantiated).
- Revert lane divider and judgment line colors to their pre-SP6 values `(0.3, 0.3, 0.4, 0.8)` and `(0.2, 0.9, 1.0, 1)`.

All rollbacks are isolated and do not depend on each other. Background image + postprocessor can stay in repo harmlessly even if SceneBuilder doesn't use them.

## 8. Out-of-scope / deferred

- **W6 #6 2nd-device test** — separate SP.
- **SP3 carry-over:** hold-note audio feedback — separate SP.
- **SP4 carry-overs:** (a) `AudioSource.Play()` 1.6 KB/tap allocation, (b) `SceneBuilder ↔ W6SamplesWireup` fold-in, (c) APK filename bump, (d) `SettingsScreen.creditsLabel` relocation — each a separate SP candidate.
- **Combo milestone visual effects** (e.g., 100-combo celebration) — separate SP, depends on SP6's HUD foundation.
- **Background alternatives / theme selector** — post-MVP.

## 9. Done criteria

- [ ] `SceneBuilder.LaneAreaWidth` is 9f; note prefab `localScale = (2.25f, 0.4f, 1)` and color `(0.08f, 0.08f, 0.12f, 1)`.
- [ ] `Assets/Sprites/background_gameplay.png` + `.meta` committed; imports as Sprite with Android ASTC.
- [ ] `BackgroundImporterPostprocessor` enforces settings on re-import.
- [ ] `ComboHUD` component exists, wired in SceneBuilder, displays live combo with hide-at-zero behavior.
- [ ] `SceneBuilder` null-guards `background_gameplay.png`.
- [ ] Judgment line alpha 0.5; lane divider alpha/color tuned.
- [ ] W6SamplesWireup re-run; multi-pitch + creditsLabel intact.
- [ ] EditMode tests 135/135 green.
- [ ] Galaxy S22 checklist §6.3 all 7 items pass.
- [ ] APK < 40 MB (spec §7 binding); < 37 MB ideal (+1 MB source compresses to ~300 KB ASTC).
- [ ] Completion report `docs/superpowers/reports/2026-04-22-w6-sp6-gameplay-visual-polish-completion.md` committed.
- [ ] Memory updated: new W6 SP6 memo + MEMORY.md index entry.

## 10. References

- v2 spec §9 (W6 weekly goal): `docs/superpowers/specs/2026-04-20-keyflow-mvp-v2-4lane-design.md`
- SP5 completion (most recent sibling, sets style baseline): `docs/superpowers/reports/2026-04-22-w6-sp5-calibration-click-completion.md`
- SP4 workflow-trap memo (W6SamplesWireup re-run requirement): `C:/Users/lhk/.claude/projects/C--dev-unity-music/memory/project_w6_sp4_complete.md`
- `SceneBuilder.cs` — gameplay scene entrypoint; lines 28 (LaneAreaWidth const), 45-57 (asset loads + guards), 63-71 (root/dividers/judgment), 135-155 (lane dividers/judgment line internals), 156-280 (BuildManagers), BuildNotePrefab helper
- `ScoreManager.cs` — source of `Combo` getter consumed by ComboHUD
- Sibling TextureImporter postprocessor for style consistency: `Assets/Editor/PianoSampleImportPostprocessor.cs`
- User-supplied reference image (pinned in conversation) — bright gradient + "PERFECT" judgment popup + center-top combo number "2323"
- User-supplied background asset: `Assets/Sprites/background_gameplay.png` (941×1672, 1.02 MB)
