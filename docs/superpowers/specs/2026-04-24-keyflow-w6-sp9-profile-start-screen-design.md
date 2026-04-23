# KeyFlow W6 Sub-Project 9 — Profile Start Screen + Per-Profile Background

**Date:** 2026-04-24
**Week:** 6 (폴리싱 + 사운드)
**Priority:** Personalization for the two-child owner of the device. Builds on SP8 but is independent of its device-playtest gate.
**Status:** Proposed

---

## 1. Motivation

The KeyFlow owner has two children (나윤 / 소윤) sharing the device. Today the app drops straight into the song-list `MainScreen` with a single navy background and a single blue gameplay background (`Assets/Sprites/background_gameplay.png`). The children asked for a "this is MY turn" visual cue — a start screen that lets each child tap their own profile, and a different colored gameplay background for each.

Supplied assets from the owner (in `img/`):
- `icon.png` — new app launcher icon.
- `start.png` — full-screen mockup of the intended start screen (two profile cards, "시작하기" buttons, plus four decorative sub-menu tiles 연주하기/배우기/챌린지/마이룸 and two decorative top-bar icons 설정/보호자).
- `blue-bg.png` — byte-identical to the currently-shipped `Assets/Sprites/background_gameplay.png` (same blue rays). Kept for naming symmetry with the yellow counterpart.
- `yellow-bg.png` — golden-ray gameplay background for 소윤's profile.

## 2. Goal

Ship an MVP profile-selection flow with three concrete outcomes:

1. **App icon** updated to `icon.png` (launcher icon on Android).
2. **Start screen** inserted in front of the existing `Main → Gameplay → Results` flow. The start screen renders `start.png` full-screen as a backdrop; two invisible `Button` hit-boxes sit over the two "시작하기" regions.
3. **Per-profile gameplay background**: tapping 나윤's "시작하기" chooses the blue background for this session; tapping 소윤's chooses the yellow one. The selection is session-scoped (no `PlayerPrefs` persistence).

Qualitative success criterion (Galaxy S22 R5CT21A31QB, release APK): the launcher shows the new icon; launching the app lands on a `start.png` screen; tapping 소윤 reaches a song and the in-game background is yellow; back-navigating to Start and tapping 나윤 reaches a song and the in-game background is blue.

## 3. Non-goals

- **Functional sub-menus on `start.png`**. The four tiles (연주하기/배우기/챌린지/마이룸) and the two top-bar icons (설정/보호자) are present only as artwork in the backdrop image. They have no hit-boxes, no tap feedback, no "준비 중" toast. Revisit in a later SP if the sub-menus are ever built.
- **Per-profile score / calibration / settings partitions**. All children share `PlayerPrefs` state (per owner request — A from brainstorming: "세션 선택만, 점수·설정 공용").
- **Profile persistence**. Every app launch starts at the Start screen; no "remember last profile" shortcut.
- **MainScreen background re-theming**. The dark-navy `Color(0.08, 0.08, 0.12)` background of the `MainCanvas` is untouched. The per-profile color change lives in gameplay only (which is where the single full-screen visual is most dominant).
- **Mid-game profile switching**. Backing out from gameplay → Main → Start is the only path to change profiles; the Start screen exposes no "switch profile" shortcut from within a session.
- **Start-screen animations, hover/selected states, audio stings on tap**. Static image + hit-boxes only.
- **Icon.png adaptive-icon split** (foreground/background layer separation). Single-layer legacy icon at all density buckets, Unity downscales.

## 4. Approach

### 4.1 Start screen architecture

A new `AppScreen.Start` is added to the existing `ScreenManager` enum. `ScreenManager.Start()` now calls `Replace(AppScreen.Start)` as the boot-time target instead of `AppScreen.Main`.

A new `StartCanvas` GameObject owns the Start screen:

```
StartCanvas (Canvas ScreenSpaceOverlay, sortingOrder 7, CanvasScaler ScaleWithScreenSize 720x1280, matchWidthOrHeight 0.5)
  Background (Image, stretched full-screen, sprite = background_start.png, raycastTarget = false)
  NayoonButton (Button over nayoon "시작하기" region, Image color α = 0, raycastTarget = true)
  SoyoonButton (Button over soyoon "시작하기" region, Image color α = 0, raycastTarget = true)
  StartScreen (MonoBehaviour coordinating button clicks)
```

`StartCanvas.sortingOrder = 7` sits above `MainCanvas.sortingOrder = 5` and below the overlays (Settings 10, Pause 12, Calibration 15). `ScreenManager.Replace` toggles `startRoot.SetActive(target == AppScreen.Start)` alongside the existing Main/Gameplay/Results toggles.

### 4.2 Hit-box placement

`start.png` has no programmatic metadata for the "시작하기" regions, so hit-boxes are positioned via RectTransform anchors against the CanvasScaler reference resolution (720×1280).

**Concrete measurement procedure** (implementer step, not optional):

1. Open `img/start.png` in an image viewer that reports pixel coordinates (Windows Paint, IrfanView, or a Python one-liner `PIL.Image.open('img/start.png').size`).
2. Record the image's native pixel dimensions as `(W_px, H_px)`.
3. Identify the center pixel of the 나윤 "시작하기" button's drawn rect — call it `(Nx, Ny)` where Ny is measured from the BOTTOM of the image (Unity UI convention).
4. Identify the same for 소윤: `(Sx, Sy)`.
5. Measure the drawn rect's width and height in pixels — `(Bw_px, Bh_px)`. Both buttons share the same size.
6. Compute normalized anchors (values in [0, 1]):
   ```
   NAYOON_ANCHOR = (Nx / W_px, Ny / H_px)
   SOYOON_ANCHOR = (Sx / W_px, Sy / H_px)
   ```
7. Compute reference-unit button size against the 720×1280 CanvasScaler:
   ```
   BUTTON_SIZE = (Bw_px * 720 / W_px, Bh_px * 1280 / H_px)
   ```
8. Add 20% margin to `BUTTON_SIZE` for thumb-tap forgiveness.
9. Author the four values as named constants in `SceneBuilder`, with a short comment block showing the source `(Nx, Ny, Sx, Sy, W_px, H_px)` so future maintainers can redo the math if `start.png` changes.

Indicative values (not authoritative — implementer MUST re-measure):
- `NAYOON_ANCHOR ≈ (0.27, 0.34)`, `SOYOON_ANCHOR ≈ (0.73, 0.34)`, `BUTTON_SIZE ≈ (220, 90)` in reference units with margin.

**Acceptance check** (Play-in-Editor, not device-only): open the regenerated scene, tap the drawn "시작하기" button centers, verify the click lands. Edge-tap on button corners should also register. Misalignment > 20 px in either direction means re-measure.

**Aspect-ratio behavior**: `start.png` is authored at an approximately 9:16 ratio (phone portrait). With `matchWidthOrHeight = 0.5`, Unity stretches the sprite to fill while hit-box anchors stay proportional to the canvas. On extreme ratios (tablet 4:3, foldables), the image gets slight horizontal letterbox/pillarbox that matches the hit-box offsets. If S22 playtest reveals that the hit-boxes visibly drift from the drawn buttons, adjustments happen in the single `SceneBuilder` constant block.

### 4.3 Profile state

New file `Assets/Scripts/Common/Profile.cs`:

```csharp
namespace KeyFlow
{
    public enum Profile { Nayoon, Soyoon }

    public static class SessionProfile
    {
        public static Profile Current { get; set; } = Profile.Nayoon;
    }
}
```

Rationale for keeping this separate from `SongSession`: `SongSession` carries the song/difficulty pair that drives a single playthrough; `SessionProfile` carries a cross-playthrough identity. One-line classes, single responsibility each — easier to read than a merged bag of static fields.

Default value `Profile.Nayoon` is a safety net for test paths or early boot ticks that read before `StartScreen.Select*` fires. Production always hits the Start screen first. **Test-author note**: any test that asserts "no profile selected yet" behavior must explicitly reset `SessionProfile.Current = Profile.Nayoon` in `[SetUp]` to avoid state leakage between tests (static field persists across tests in the same AppDomain). NUnit `[SetUp]` is the right place; `[OneTimeSetUp]` is not (isolation between each test matters).

### 4.4 `StartScreen` MonoBehaviour

New file `Assets/Scripts/UI/StartScreen.cs`:

```csharp
namespace KeyFlow.UI
{
    public class StartScreen : MonoBehaviour
    {
        [SerializeField] private Button nayoonButton;
        [SerializeField] private Button soyoonButton;

        private void Awake()
        {
            nayoonButton.onClick.AddListener(() => Select(Profile.Nayoon));
            soyoonButton.onClick.AddListener(() => Select(Profile.Soyoon));
        }

        private void Select(Profile p)
        {
            SessionProfile.Current = p;
            ScreenManager.Instance.Replace(AppScreen.Main);
        }

        internal void InvokeSelectForTest(Profile p) => Select(p);
    }
}
```

Tests invoke `InvokeSelectForTest` under `InternalsVisibleTo("KeyFlow.Tests.EditMode")` (already declared once in `JudgmentSystem.cs`). No click simulation needed.

### 4.5 Gameplay background switching

Today `SceneBuilder.BuildBackgroundCanvas(Sprite bgSprite, Camera cam)` bakes `background_gameplay.png` into the Image component at Editor build time. Runtime cannot swap it.

New component `Assets/Scripts/Feedback/BackgroundSwitcher.cs`:

```csharp
namespace KeyFlow.Feedback
{
    public class BackgroundSwitcher : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite blueBg;
        [SerializeField] private Sprite yellowBg;

        public void Apply(Profile p)
        {
            backgroundImage.sprite = p == Profile.Soyoon ? yellowBg : blueBg;
        }

        internal void SetDependenciesForTest(Image img, Sprite blue, Sprite yellow)
        {
            backgroundImage = img; blueBg = blue; yellowBg = yellow;
        }
    }
}
```

`SceneBuilder.BuildBackgroundCanvas` is modified to also add the `BackgroundSwitcher` component to the background GameObject and wire both sprites plus the `Image` reference. The component is a sibling of the `Image` (or on the same GameObject — implementer's call), discoverable via SceneBuilder's `SetField` pattern.

**Apply trigger**: `ScreenManager.Replace(AppScreen target)` gets a one-line addition: if `target == AppScreen.Gameplay && backgroundSwitcher != null`, call `Apply(SessionProfile.Current)`. Leaving gameplay (→ Main or Results) doesn't re-apply; the sprite stays whatever it last was. Cheap and correct for a shared gameplay background.

`ScreenManager` gains a `[SerializeField] private BackgroundSwitcher backgroundSwitcher` field, wired in SceneBuilder.

### 4.6 Asset pipeline

Two images from `img/` are imported into `Assets/Sprites/` (no existing-file rename — see below for rationale):

- `img/start.png` → `Assets/Sprites/background_start.png`
- `img/yellow-bg.png` → `Assets/Sprites/background_yellow.png`

**`img/blue-bg.png` is intentionally NOT imported.** It is byte-identical to the already-shipped `Assets/Sprites/background_gameplay.png` (SP6 asset). We keep the existing file untouched and treat it as "blue" in the runtime switch. Rationale for not renaming it to `background_blue.png`: `Assets/Editor/BackgroundImporterPostprocessor.cs:10` hardcodes `TargetPath = "Assets/Sprites/background_gameplay.png"` and enforces ASTC compression + no-mipmap import settings. A rename without updating the postprocessor would silently regress those settings; updating the postprocessor to cover both the renamed file and the new yellow file is extra scope for zero user-visible benefit.

**Updates to `BackgroundImporterPostprocessor`** to cover the new yellow file (§4.5 needs it imported with the same settings as blue):

Change from a single `TargetPath` constant to an allowlist of paths that must share import settings:

```csharp
private static readonly string[] TargetPaths = new[]
{
    "Assets/Sprites/background_gameplay.png",
    "Assets/Sprites/background_yellow.png",
};
// Replace `if (assetPath == TargetPath)` with `if (Array.IndexOf(TargetPaths, assetPath) >= 0)`.
```

`background_start.png` does NOT join the allowlist — the start screen sprite has different needs (sharper presentation for readable text/illustration). Leave it on Unity default import with just a Max Size override via manual TextureImporter setting (or through a separate postprocessor if desired; out of scope).

Import settings on `background_gameplay.png` and `background_yellow.png` (enforced by the updated postprocessor):
- Texture Type: Sprite (2D and UI)
- Compression: ASTC 4x4 (existing postprocessor policy)
- Max Size: per existing policy (verify by reading current postprocessor body)
- `raycastTarget` on the resulting `Image` is off for the gameplay backdrop so taps pass through to gameplay input.

Import settings on `background_start.png` (manual, not policy-enforced):
- Texture Type: Sprite (2D and UI)
- Max Size: Android override 2048 (preserves full-frame detail at high-DPI without inflating APK too much)
- `raycastTarget` on the resulting `Image` is off; the invisible buttons above it receive taps.

SceneBuilder load paths:
- `BuildBackgroundCanvas` loads `Assets/Sprites/background_gameplay.png` (blue) — no change from today.
- Additionally loads `Assets/Sprites/background_yellow.png` and wires both into `BackgroundSwitcher.blueBg` / `yellowBg`.
- `BuildStartCanvas` (new helper) loads `Assets/Sprites/background_start.png`.

`img/icon.png` → `Assets/Textures/icon.png`. Wired into Player Settings → Icon (Android) as the sole source for all density buckets.

**Docs drift to also update in §5 Modified list:** `README.md:70` currently references `background_gameplay.png` by name; the name stays, but verify the reference still makes sense in context (may need an aside that "gameplay background" is now per-profile).

### 4.7 Back navigation

`ScreenManager.HandleBack` case table updated:

| Current screen | Prior behavior | New behavior |
|----------------|----------------|--------------|
| Start | n/a (screen didn't exist) | Double-back within 2 s → Application.Quit (mirrors old Main behavior) |
| Main | Double-back quit | `Replace(AppScreen.Start)` — back to profile picker |
| Gameplay | Show pause overlay | (unchanged) |
| Results | `Replace(Main)` | (unchanged) |

This preserves the "Android double-back to quit" ergonomic while making profile switching discoverable through the back button only. The Start screen itself has no UI affordance for quitting.

### 4.8 App icon

`ProjectSettings/ProjectSettings.asset` has Unity's Android icon slots. Changing this is a one-click (or one-property) settings edit. Because it's a settings diff, it's committed manually, not through Editor code. The implementer must:

1. Import `icon.png` into `Assets/Textures/` as a Texture2D (not Sprite; Unity's Player Settings icon fields want Texture2D).
2. Open Player Settings → Android → Icon. Assign `icon.png` to Legacy Icon (all densities). Optionally leave Adaptive Icon unassigned; Android falls back to Legacy.
3. Verify `ProjectSettings.asset` diff shows only icon-related keys.

APK rebuild picks up the new icon automatically.

## 5. Files

**Modified:**
- `ProjectSettings/ProjectSettings.asset` — Android icon references.
- `Assets/Editor/SceneBuilder.cs` — `AppScreen.Start` wiring; `BuildStartCanvas` new helper; `BuildBackgroundCanvas` gains BackgroundSwitcher wiring and both-sprites load.
- `Assets/Editor/BackgroundImporterPostprocessor.cs` — `TargetPath` string constant replaced with a `TargetPaths[]` allowlist covering both `background_gameplay.png` (existing) and `background_yellow.png` (new). Update the file's leading comment ("Enforces import settings for the single background_gameplay.png asset.") to reflect the two-path coverage.
- `Assets/Scripts/UI/ScreenManager.cs` — `AppScreen.Start` enum value inserted at the head (reads as logical ordering first→last); initial `Replace` target; `HandleBack` new Start case; `[SerializeField] BackgroundSwitcher` + `Apply` call in `Replace`.
- `Assets/Scripts/Common/SongSession.cs` — unchanged (profile lives separately).
- `Assets/Sprites/background_gameplay.png` — unchanged (still the blue asset; no rename).
- `Assets/Scenes/GameplayScene.unity` — regenerated.
- `ProjectSettings/ProjectSettings.asset` — Android icon. Prior commits touching this file (`git log` shows portrait flip, InputActions preload, Android player config — all manual edits) confirm precedent for manual ProjectSettings-only commits.
- `README.md` — potential reference update if any build-instructions or screenshot description mentions the blue-only background.

**New:**
- `Assets/Scripts/Common/Profile.cs` + meta
- `Assets/Scripts/UI/StartScreen.cs` + meta
- `Assets/Scripts/Feedback/BackgroundSwitcher.cs` + meta
- `Assets/Sprites/background_start.png` + meta (imported from `img/start.png`)
- `Assets/Sprites/background_yellow.png` + meta (imported from `img/yellow-bg.png`)
- `Assets/Textures/icon.png` + meta (imported from `img/icon.png`)
- `Assets/Tests/EditMode/StartScreenTests.cs` + meta
- `Assets/Tests/EditMode/BackgroundSwitcherTests.cs` + meta
- `Assets/Tests/EditMode/ScreenManagerStartTests.cs` + meta (or extension of existing `ScreenManagerTests.cs`)

## 6. Implementation sequence

1. **Assets**: import 3 bg images + icon; rename existing `background_gameplay.png`. Ensure SceneBuilder still compiles (just a string path update).
2. **Profile + SessionProfile** scaffolding. Trivial, commit standalone.
3. **BackgroundSwitcher** + unit tests. Standalone component.
4. **StartScreen MonoBehaviour** + unit tests.
5. **ScreenManager** extensions: `AppScreen.Start`, `HandleBack` case, Apply hook. Tests extended.
6. **SceneBuilder** integration: `BuildStartCanvas`, BackgroundSwitcher wiring in `BuildBackgroundCanvas`, `ScreenManager` field wire, initial Replace target change. Regenerate scene.
7. **Player Settings** icon assignment (manual edit, commit `ProjectSettings.asset`).
8. **APK build + device playtest + completion report**.

Hit-box coordinate tuning happens as part of step 6 — `SceneBuilder` constants visually verified in-editor before a device build.

## 7. Testing

**Unity EditMode** (15-18 new tests):

`ScreenManagerStartTests`:
- `Start_OnBoot_EntersStartScreen` — new ScreenManager after a new scene should have `Current == AppScreen.Start`.
- `Replace_StartToMain_DeactivatesStart` — startRoot.activeSelf == false after Replace to Main.
- `Replace_MainToGameplay_AppliesProfileBackground` — with profile set to Soyoon, verify BackgroundSwitcher.Apply was called with Soyoon.
- `HandleBack_FromMain_GoesToStart` — replaces to Start.
- `HandleBack_FromStart_DoubleWindow_Quits` — two back-presses within DoubleBackWindow triggers quit request (mock Application.Quit).

`StartScreenTests`:
- `SelectNayoon_SetsProfileToNayoon` — `SessionProfile.Current == Profile.Nayoon` after InvokeSelectForTest.
- `SelectSoyoon_SetsProfileToSoyoon` — analog.
- `SelectNayoon_ReplacesToMain` — uses mock/stub ScreenManager to observe.
- `SelectSoyoon_ReplacesToMain` — analog.

`BackgroundSwitcherTests`:
- `Apply_Nayoon_SetsBlueSprite`
- `Apply_Soyoon_SetsYellowSprite`
- `Apply_Nayoon_ThenSoyoon_SwitchesSprite`
- `Apply_Soyoon_ThenNayoon_SwitchesSprite`

**Manual Play-in-Editor** sanity pass:
- Boot → Start screen shows the backdrop.
- Tap Nayoon's "시작하기" region → MainScreen. Tap a song → blue gameplay bg.
- Back out to Main → Back again → Start. Tap Soyoon → Main → song → yellow gameplay bg.
- Back twice from Start → Application.Quit is a no-op in Editor (Unity limitation) but the code path runs; add a `Debug.Log("[ScreenManager] Quit requested on double-back from Start")` at the quit site so the editor test confirms the branch reaches the call. Real quit only verifiable on device.

**Device playtest (S22, release APK)**:
1. Launcher icon verified as the new `icon.png`.
2. Start screen renders cleanly on the S22 aspect ratio; both "시작하기" hit-boxes align with the drawn buttons (within ±10 px visual tolerance).
3. Profile flow for each child; background color observed.
4. Back-navigation through all transitions.
5. Profiler attach during a 2-minute gameplay session on yellow bg: `GC.Collect == 0` (SP3 baseline preserved).

## 8. Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Hit-boxes drift from drawn "시작하기" buttons on non-9:16 devices | Medium | Medium (UX confusion) | Generous hit-box size (+20% margin); S22 playtest catches visible drift before merge; single-block constants ease re-tuning. |
| Renaming `background_gameplay.png` → `background_blue.png` breaks a hidden reference | Low | Medium | Repo-wide grep for old path before rename; SceneBuilder is the only documented caller. |
| Start sprite byte-bloat (1.9 MB) on APK | Low | Low | Android compression override; combined net delta ≤ +500 KB expected; completion report records actual. |
| Icon change doesn't survive Unity's Android icon cache / Gradle incremental | Low | Low | Clean-rebuild APK if icon appears stale; verified on install, not on edit. |
| Back-button behavior on Start (quit flow) breaks ADB `adb shell input keyevent KEYCODE_BACK` automation if we ever add it | Low | Low | Out of scope for this SP; note in carry-overs. |
| Test scaffolding for StartScreen depends on a `ScreenManager.Instance` in the EditMode host | Low | Low | Existing `ScreenManagerTests.cs` pattern already handles this: `sm = mgr.AddComponent<ScreenManager>()` triggers `Awake()` which assigns `Instance = this`. No new seam needed. |

## 9. Success criteria

**Objective:**
- All EditMode tests green; count = prior + 12-15 new tests.
- APK size delta ≤ +500 KB vs. SP8 baseline.
- Profiler: `GC.Collect == 0` preserved during 2-minute Entertainer Normal session (any profile).

**Subjective (device playtest, owner/children-confirmed):**
- Launcher icon is the new cute piano-kid image.
- Start screen displays `start.png` correctly; 나윤's 시작하기 and 소윤's 시작하기 buttons feel tappable.
- Gameplay background visually differs between children's sessions (blue vs yellow).

## 10. Out-of-scope follow-ups

- Sub-menus (연주하기/배우기/챌린지/마이룸) become real screens.
- 설정 / 보호자 top-bar buttons.
- Per-profile PlayerPrefs partition (separate score / calibration / settings per child).
- "Remember last profile" shortcut (auto-skip Start screen on subsequent launches).
- MainScreen background also re-themes per profile.
- Mid-session profile switch shortcut.
- Start-screen animations, card selected-state feedback, audio sting on tap.
- Adaptive icon with foreground/background layer separation.
