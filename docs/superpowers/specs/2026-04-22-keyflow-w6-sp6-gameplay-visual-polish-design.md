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

### 4.1 Full-width tiles (`LaneAreaWidth` 4f → 9f)

At camera `orthographicSize = 8` and 720×1280 screen aspect (0.5625), half-width in world units ≈ 4.5. Thus `LaneAreaWidth = 9f` fills the visible horizontal area end-to-end with zero margin.

Changes:
- `SceneBuilder.LaneAreaWidth` const: `4f` → `9f`.
- `BuildNotePrefab`: note sprite `localScale.x` was hardcoded `1f`; now `LaneAreaWidth / LaneLayout.LaneCount = 9f / 4 = 2.25f`. This keeps one tile = one lane width.
- Downstream: `SetField(tapInput, "laneAreaWidth", LaneAreaWidth)` and `SetField(spawner, "laneAreaWidth", LaneAreaWidth)` calls are already parameterized — value automatically flows through.
- `LaneLayout.LaneToX`, `LaneLayout.XToLane` are already pure functions taking `width` as argument — no code changes.

### 4.2 Live combo HUD

New component `Assets/Scripts/UI/ComboHUD.cs`:

```csharp
namespace KeyFlow.UI
{
    public class ComboHUD : MonoBehaviour
    {
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private Text comboText;
        private int lastCombo = -1;

        private void Update()
        {
            if (scoreManager == null) return;
            int current = scoreManager.Combo;
            if (current == lastCombo) return;
            lastCombo = current;

            if (current == 0)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
            }
            else
            {
                if (!gameObject.activeSelf) gameObject.SetActive(true);
                comboText.text = current.ToString();
            }
        }
    }
}
```

Design notes:
- **Polling, not event-based.** `ScoreManager` doesn't expose a change event and adding one would touch gameplay runtime. Polling an int is cheap (no alloc) and the Update-gate on `current == lastCombo` short-circuits 99% of frames.
- **GC-free on unchanged frames** (SP3 baseline preserved). `int.ToString()` allocates on changed frames only (~1× per tap), ~28 bytes × ~200 taps/song = 5.6 KB total — acceptable.
- **Hides at combo=0.** At song start, Combo=0 → HUD hidden. First Perfect/Great/Good tap → combo=1 → HUD appears. Miss → combo reset to 0 → HUD hidden.
- **Placement:** top-center, below the existing ProgressBar, above the SP4 FeedbackPipeline particle layer. Large white Text (fontSize ~96 at 720×1280 reference, bold-weight LegacyRuntime font).

Wiring: `SceneBuilder.BuildHUD` instantiates the `ComboText` GameObject under `HUDCanvas`, creates the `ComboHUD` component on a container, and `SetField`s the `scoreManager` (constructed in `BuildManagers`) and `comboText` references.

### 4.3 Background image

- **User-supplied asset:** `Assets/Sprites/background_gameplay.png` (941×1672 RGB PNG, 1.02 MB source).
- **TextureImporter postprocessor** `Assets/Editor/BackgroundImporterPostprocessor.cs` — mirrors `PianoSampleImportPostprocessor` pattern. Forces `textureType = Sprite (2D and UI)`, `mipmapEnabled = false`, `isReadable = false`, platform-specific Android override for ASTC 4×4 compression. This guarantees import settings survive re-imports and fresh clones.
- **Scene wiring:** `SceneBuilder.BuildBackground` creates a `GameObject "Background"` with `SpriteRenderer`, assigns the loaded sprite, sets `sortingOrder = -10`, scales to camera view (sprite bounds stretched so either height or width reaches camera ortho view size). Z-depth behind all gameplay elements.
- **Camera backgroundColor** remains dark navy as fallback for areas outside the sprite (portion of screen with aspect ratio differing from the image). Unchanged from current `(0.08, 0.08, 0.12, 1)`.

### 4.4 Judgment line + lane dividers retuning

- **Judgment line (`BuildJudgmentLine`):** alpha reduced from 1.0 to 0.5. Keeps it present as timing reference without visually fighting the new background. Color kept white.
- **Lane dividers (`BuildLaneDividers`):** alpha reduced from current (~1.0) to 0.3, color shifted to blue-tinted white (e.g., `(0.8, 0.9, 1.0, 0.3)`) to harmonize with blue background. Keeps lane boundaries legible without being harsh.

### 4.5 Files

**Created (committed):**
- `Assets/Scripts/UI/ComboHUD.cs` — MonoBehaviour component (~30 lines)
- `Assets/Editor/BackgroundImporterPostprocessor.cs` — AssetPostprocessor enforcing Sprite/no-mipmap/ASTC settings (~30 lines)
- `Assets/Sprites/background_gameplay.png` — user-supplied, 1.02 MB
- `Assets/Sprites/background_gameplay.png.meta` — auto-generated after postprocessor runs
- `Assets/Tests/EditMode/ComboHUDTests.cs` — 4 EditMode tests

**Modified:**
- `Assets/Editor/SceneBuilder.cs`:
  - `LaneAreaWidth` const 4f → 9f
  - `BuildNotePrefab`: note sprite localScale.x hardcode 1f → computed from lane width
  - Load `background_gameplay.png` + null-guard + `BuildBackground(sprite)`
  - `BuildLaneDividers`: alpha/color tuning
  - `BuildJudgmentLine`: alpha 1.0 → 0.5
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
  → BuildBackground(bgSprite) → GameObject with SpriteRenderer at sortingOrder=-10
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
        androidSettings.compressionQuality = (int)UnityEditor.TextureCompressionQuality.Normal;
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

**Background sprite aspect mismatch:**

- Source 941×1672 ≈ 9:16 portrait. On a 720×1280 (9:16) or 1080×2340 (9:19.5 edge-to-edge) phone the aspect differs slightly. The `SpriteRenderer` will scale to fit either width or height; areas outside the sprite fall back to `camera.backgroundColor = (0.08, 0.08, 0.12)`. No visual break.
- On ultrawide (e.g., foldables unfolded) the camera backgroundColor would fill a larger portion. Acceptable for MVP; W7 2nd-device test will surface if problematic.

**Intentionally not handled (YAGNI):**

- Dynamic background swap at runtime, theme selector, per-song background — out of scope.
- Combo rollback on slight timing errors — `ScoreManager` semantics are the source of truth; ComboHUD is a pure mirror.
- High-DPI scaling of ComboText fontSize — CanvasScaler's `ScaleWithScreenSize` already handles this.

## 6. Testing

### 6.1 EditMode tests (`ComboHUDTests.cs`, 4 tests)

| Test | Asserts |
|---|---|
| `HidesWhenComboZero` | ScoreManager starts at Combo=0 → hud.Update() → gameObject.activeSelf == false |
| `ShowsAndUpdatesTextWhenComboIncreases` | Apply Perfect (Combo=1) → hud.Update() → activeSelf==true, text.text == "1"; Apply Perfect again (Combo=2) → text.text == "2" |
| `HidesWhenComboResetsToZero` | Drive Combo 0 → 3 (HUD visible) → Miss (Combo=0) → hud.Update() → activeSelf == false |
| `DoesNotTouchTextWhenComboUnchanged` | Use a recording Text wrapper (or mock with counter); call Update 10 times without changing Combo; assert the text setter was called at most once (the initial sync) |

Test fixtures will use `new ScoreManager()` + `Initialize(totalNotes=10)` + `Apply(Judgment.X)` to drive Combo — same pattern as existing `ScoreManagerTests`.

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

- Revert SceneBuilder's `LaneAreaWidth` back to 4f + note scale back to 1f → full-width tiles disappear.
- Revert `BuildBackground` call + `background_gameplay.png` load → revert to camera dark navy.
- Delete ComboHUD wire-up in SceneBuilder + unused `ComboHUD.cs` stays dormant (no runtime cost if not instantiated).

All rollbacks are isolated and do not depend on each other. Background image + postprocessor can stay in repo harmlessly even if SceneBuilder doesn't use them.

## 8. Out-of-scope / deferred

- **W6 #6 2nd-device test** — separate SP.
- **SP3 carry-over:** hold-note audio feedback — separate SP.
- **SP4 carry-overs:** (a) `AudioSource.Play()` 1.6 KB/tap allocation, (b) `SceneBuilder ↔ W6SamplesWireup` fold-in, (c) APK filename bump, (d) `SettingsScreen.creditsLabel` relocation — each a separate SP candidate.
- **Combo milestone visual effects** (e.g., 100-combo celebration) — separate SP, depends on SP6's HUD foundation.
- **Background alternatives / theme selector** — post-MVP.

## 9. Done criteria

- [ ] `SceneBuilder.LaneAreaWidth` is 9f; note prefab scale reflects this.
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
