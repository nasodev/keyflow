# KeyFlow W6 SP9 — Profile Start Screen + Per-Profile Background Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Insert a start screen (`start.png` backdrop + two profile hit-boxes) before MainScreen; tap 나윤 → blue gameplay background (current), tap 소윤 → yellow. Update Android launcher icon to `icon.png`.

**Architecture:** `AppScreen.Start` added to `ScreenManager`; new `StartScreen` MonoBehaviour fires `SessionProfile.Current = {Nayoon, Soyoon}` then `Replace(AppScreen.Main)`; new `BackgroundSwitcher` swaps between blue/yellow sprites on `Replace(AppScreen.Gameplay)`. Session-scoped profile (no `PlayerPrefs`). All wiring consolidated in `SceneBuilder` per SP7 convention.

**Tech Stack:** Unity 6 (C#, NUnit EditMode tests), existing SP1–SP8 scaffolding.

**Spec:** `docs/superpowers/specs/2026-04-24-keyflow-w6-sp9-profile-start-screen-design.md`

---

## File Structure

```
img/                                                                (unchanged — source images stay here)

Assets/Sprites/
  background_gameplay.png                                UNCHANGED (blue asset, SP6-era)
  background_yellow.png                                  NEW (import of img/yellow-bg.png)
  background_start.png                                   NEW (import of img/start.png)

Assets/Textures/
  icon.png                                               NEW (import of img/icon.png)

Assets/Scripts/Common/
  Profile.cs                                             NEW (enum + static SessionProfile)
  SongSession.cs                                         UNCHANGED

Assets/Scripts/UI/
  StartScreen.cs                                         NEW (2-button click handler)
  ScreenManager.cs                                       MODIFY (AppScreen.Start + HandleBack + Apply hook)

Assets/Scripts/Feedback/
  BackgroundSwitcher.cs                                  NEW (blue/yellow sprite toggle)

Assets/Editor/
  SceneBuilder.cs                                        MODIFY (BuildStartCanvas + BackgroundSwitcher wire)
  BackgroundImporterPostprocessor.cs                     MODIFY (TargetPath → TargetPaths[])

Assets/Tests/EditMode/
  ProfileTests.cs                                        NEW
  BackgroundSwitcherTests.cs                             NEW
  StartScreenTests.cs                                    NEW
  ScreenManagerTests.cs                                  MODIFY (add Start + profile-apply cases)

Assets/Scenes/
  GameplayScene.unity                                    REGENERATE

ProjectSettings/
  ProjectSettings.asset                                  MODIFY (Android icon)
```

**Ordering rationale:** Assets (Task 1) must land first so Unity can import them and postprocessor settings can be verified. Pure-C# units (Profile, BackgroundSwitcher, StartScreen) in Tasks 2–4 are order-independent but ordered smallest-first for clean commits. ScreenManager extensions (Task 5) depend on `Profile`, `BackgroundSwitcher`, `StartScreen` existing for types to resolve. SceneBuilder integration (Task 6) depends on everything above. Icon + APK + playtest + completion report (Task 7) finalizes.

---

## Task 1: Import assets + extend BackgroundImporterPostprocessor

**Spec reference:** §4.6 (Asset pipeline), §5 (Files)

**Files:**
- Create: `Assets/Sprites/background_start.png` (from `img/start.png`)
- Create: `Assets/Sprites/background_yellow.png` (from `img/yellow-bg.png`)
- Create: `Assets/Textures/icon.png` (from `img/icon.png`)
- Modify: `Assets/Editor/BackgroundImporterPostprocessor.cs`

- [ ] **Step 1.1: Verify source images exist**

```
ls -la img/icon.png img/start.png img/yellow-bg.png
```

Expected: three non-empty files. `blue-bg.png` presence is fine but NOT imported (spec §4.6 rationale).

- [ ] **Step 1.2: Copy assets into the project (keep originals in `img/`)**

```
cp img/start.png      Assets/Sprites/background_start.png
cp img/yellow-bg.png  Assets/Sprites/background_yellow.png
mkdir -p Assets/Textures
cp img/icon.png       Assets/Textures/icon.png
```

On Windows bash (MINGW), forward slashes work. Use `cp -n` if there's a risk of overwriting.

- [ ] **Step 1.3: Extend `BackgroundImporterPostprocessor` to cover yellow too**

Edit `Assets/Editor/BackgroundImporterPostprocessor.cs`:

```csharp
using System;
using UnityEditor;

namespace KeyFlow.Editor
{
    // Enforces import settings for gameplay background sprites.
    // Mirrors PianoSampleImportPostprocessor so settings stick across
    // re-imports and fresh worktree checkouts.
    public class BackgroundImporterPostprocessor : AssetPostprocessor
    {
        private static readonly string[] TargetPaths = new[]
        {
            "Assets/Sprites/background_gameplay.png",
            "Assets/Sprites/background_yellow.png",
        };

        private void OnPreprocessTexture()
        {
            if (Array.IndexOf(TargetPaths, assetPath) < 0) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.isReadable = false;

            var androidSettings = importer.GetPlatformTextureSettings("Android");
            androidSettings.overridden = true;
            androidSettings.format = TextureImporterFormat.ASTC_4x4;
            androidSettings.compressionQuality = 50;
            importer.SetPlatformTextureSettings(androidSettings);
        }
    }
}
```

Only 3 changes from the current file: (a) `using System;` added for `Array.IndexOf`; (b) `TargetPath` constant → `TargetPaths[]` array; (c) `if (assetPath != TargetPath)` → `if (Array.IndexOf(TargetPaths, assetPath) < 0)`; plus the leading comment generalized ("the single background_gameplay.png asset" → "gameplay background sprites").

- [ ] **Step 1.4: Trigger import + verify settings applied**

Open Unity Editor on the worktree once (required so Unity imports the newly-copied files and the postprocessor runs). Check the Inspector on `Assets/Sprites/background_yellow.png`:
- Texture Type = Sprite (2D and UI)
- Platform override (Android) = ASTC 4x4, compressionQuality 50

If Unity is not open for the subagent, the import will happen at the next batch-mode or Editor launch. The postprocessor is idempotent — no action required beyond making sure it runs at some point before Task 6 (SceneBuilder regeneration).

`background_start.png` has default import (Sprite via auto-detect; no postprocessor enforcement needed for this SP).

- [ ] **Step 1.5: Also set `background_start.png` to Sprite manually, if Unity didn't auto-detect**

After Unity imports it, confirm via the Inspector that `background_start.png` is Sprite type. If it's Texture2D, toggle to Sprite (2D and UI). Save.

- [ ] **Step 1.6: Commit**

```
git add Assets/Sprites/background_start.png Assets/Sprites/background_start.png.meta \
        Assets/Sprites/background_yellow.png Assets/Sprites/background_yellow.png.meta \
        Assets/Textures/icon.png Assets/Textures/icon.png.meta \
        Assets/Editor/BackgroundImporterPostprocessor.cs
git commit -m "$(cat <<'EOF'
feat(w6-sp9): import start.png, yellow-bg.png, icon.png + cover yellow in background postprocessor

Two gameplay backgrounds (blue=existing, yellow=new) + start-screen
backdrop + Android launcher icon. BackgroundImporterPostprocessor
generalized from single-path to allowlist to enforce ASTC 4x4 on both
gameplay variants.

background_gameplay.png intentionally untouched (byte-identical to
img/blue-bg.png; rename would regress postprocessor coverage).
EOF
)"
```

---

## Task 2: Profile enum + SessionProfile

**Spec reference:** §4.3

**Files:**
- Create: `Assets/Scripts/Common/Profile.cs`
- Create: `Assets/Tests/EditMode/ProfileTests.cs`

- [ ] **Step 2.1: Write failing tests**

Create `Assets/Tests/EditMode/ProfileTests.cs`:

```csharp
using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class ProfileTests
    {
        [SetUp]
        public void ResetProfile()
        {
            // Static field persists across tests in same AppDomain.
            SessionProfile.Current = Profile.Nayoon;
        }

        [Test]
        public void SessionProfile_DefaultsToNayoon()
        {
            Assert.AreEqual(Profile.Nayoon, SessionProfile.Current);
        }

        [Test]
        public void SessionProfile_SetSoyoon_PersistsWithinSession()
        {
            SessionProfile.Current = Profile.Soyoon;
            Assert.AreEqual(Profile.Soyoon, SessionProfile.Current);
        }

        [Test]
        public void SessionProfile_RoundTrip_NayoonSoyoonNayoon()
        {
            SessionProfile.Current = Profile.Soyoon;
            SessionProfile.Current = Profile.Nayoon;
            Assert.AreEqual(Profile.Nayoon, SessionProfile.Current);
        }
    }
}
```

- [ ] **Step 2.2: Run tests, confirm they fail (compile error)**

Run the full EditMode suite via Unity batch mode:
```
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -projectPath "$(pwd)" -runTests -testPlatform EditMode \
  -testFilter "ProfileTests" -logFile - 2>&1 | tail -40
```

(No `-quit` — `-runTests` + `-quit` skips the runner per `memory/feedback_unity_runtests_no_quit.md`.)

Expected: compile errors — `Profile` / `SessionProfile` don't exist.

- [ ] **Step 2.3: Create Profile.cs**

Create `Assets/Scripts/Common/Profile.cs`:

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

- [ ] **Step 2.4: Run tests, confirm all 3 pass**

Same command. Expected: `ProfileTests.*` 3/3 pass; full suite green (148 prior + 3 new = 151).

- [ ] **Step 2.5: Commit**

```
git add Assets/Scripts/Common/Profile.cs Assets/Scripts/Common/Profile.cs.meta \
        Assets/Tests/EditMode/ProfileTests.cs Assets/Tests/EditMode/ProfileTests.cs.meta
git commit -m "$(cat <<'EOF'
feat(w6-sp9): Profile enum + SessionProfile static

Session-scoped profile state (no PlayerPrefs). Default Nayoon is a
safety net for test paths and early boot ticks; production always
passes through StartScreen first.
EOF
)"
```

---

## Task 3: BackgroundSwitcher

**Spec reference:** §4.5

**Files:**
- Create: `Assets/Scripts/Feedback/BackgroundSwitcher.cs`
- Create: `Assets/Tests/EditMode/BackgroundSwitcherTests.cs`

- [ ] **Step 3.1: Write failing tests**

Create `Assets/Tests/EditMode/BackgroundSwitcherTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow;
using KeyFlow.Feedback;

namespace KeyFlow.Tests.EditMode
{
    public class BackgroundSwitcherTests
    {
        private static (BackgroundSwitcher switcher, Image img, Sprite blue, Sprite yellow, GameObject host)
            Build()
        {
            var host = new GameObject("BgHost");
            var imgGO = new GameObject("BgImage");
            imgGO.transform.SetParent(host.transform);
            var img = imgGO.AddComponent<Image>();

            // Create two distinguishable dummy sprites.
            var blueTex = new Texture2D(1, 1);
            blueTex.SetPixel(0, 0, Color.blue); blueTex.Apply();
            var blue = Sprite.Create(blueTex, new Rect(0, 0, 1, 1), Vector2.zero);
            blue.name = "blue";

            var yellowTex = new Texture2D(1, 1);
            yellowTex.SetPixel(0, 0, Color.yellow); yellowTex.Apply();
            var yellow = Sprite.Create(yellowTex, new Rect(0, 0, 1, 1), Vector2.zero);
            yellow.name = "yellow";

            var switcher = host.AddComponent<BackgroundSwitcher>();
            switcher.SetDependenciesForTest(img, blue, yellow);

            return (switcher, img, blue, yellow, host);
        }

        [Test]
        public void Apply_Nayoon_SetsBlueSprite()
        {
            var (switcher, img, blue, yellow, host) = Build();
            switcher.Apply(Profile.Nayoon);
            Assert.AreSame(blue, img.sprite);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Apply_Soyoon_SetsYellowSprite()
        {
            var (switcher, img, blue, yellow, host) = Build();
            switcher.Apply(Profile.Soyoon);
            Assert.AreSame(yellow, img.sprite);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Apply_NayoonThenSoyoon_SwitchesSprite()
        {
            var (switcher, img, blue, yellow, host) = Build();
            switcher.Apply(Profile.Nayoon);
            switcher.Apply(Profile.Soyoon);
            Assert.AreSame(yellow, img.sprite);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Apply_SoyoonThenNayoon_SwitchesSprite()
        {
            var (switcher, img, blue, yellow, host) = Build();
            switcher.Apply(Profile.Soyoon);
            switcher.Apply(Profile.Nayoon);
            Assert.AreSame(blue, img.sprite);
            Object.DestroyImmediate(host);
        }
    }
}
```

- [ ] **Step 3.2: Run tests, confirm compile failure**

Expected: `BackgroundSwitcher` type does not exist.

- [ ] **Step 3.3: Implement BackgroundSwitcher**

Create `Assets/Scripts/Feedback/BackgroundSwitcher.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.Feedback
{
    public class BackgroundSwitcher : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite blueBg;
        [SerializeField] private Sprite yellowBg;

        public void Apply(Profile p)
        {
            if (backgroundImage == null) return;
            backgroundImage.sprite = (p == Profile.Soyoon) ? yellowBg : blueBg;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal void SetDependenciesForTest(Image img, Sprite blue, Sprite yellow)
        {
            backgroundImage = img;
            blueBg = blue;
            yellowBg = yellow;
        }
#endif
    }
}
```

- [ ] **Step 3.4: Run tests, confirm all 4 pass**

Expected: `BackgroundSwitcherTests.*` 4/4 green.

- [ ] **Step 3.5: Commit**

```
git add Assets/Scripts/Feedback/BackgroundSwitcher.cs Assets/Scripts/Feedback/BackgroundSwitcher.cs.meta \
        Assets/Tests/EditMode/BackgroundSwitcherTests.cs Assets/Tests/EditMode/BackgroundSwitcherTests.cs.meta
git commit -m "$(cat <<'EOF'
feat(w6-sp9): BackgroundSwitcher for runtime blue/yellow toggle

Nayoon → blue sprite, Soyoon → yellow. Null-guard on Image ref.
Test hook gated by UNITY_EDITOR || UNITY_INCLUDE_TESTS.
EOF
)"
```

---

## Task 4: StartScreen MonoBehaviour

**Spec reference:** §4.4

**Files:**
- Create: `Assets/Scripts/UI/StartScreen.cs`
- Create: `Assets/Tests/EditMode/StartScreenTests.cs`

- [ ] **Step 4.1: Write failing tests**

Create `Assets/Tests/EditMode/StartScreenTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow;
using KeyFlow.UI;

namespace KeyFlow.Tests.EditMode
{
    public class StartScreenTests
    {
        private GameObject mgr, startCanvas, mainRoot, gameplayRoot, results;
        private GameObject nayoonBtnGO, soyoonBtnGO;
        private ScreenManager sm;
        private StartScreen startScreen;
        private Button nayoonBtn, soyoonBtn;

        private class TestOverlay : OverlayBase { }

        [SetUp]
        public void Setup()
        {
            SessionProfile.Current = Profile.Nayoon; // reset between tests

            mgr = new GameObject("sm");
            startCanvas = new GameObject("start");
            mainRoot = new GameObject("main");
            gameplayRoot = new GameObject("gameplay");
            results = new GameObject("results");

            var settings = new GameObject("settings").AddComponent<TestOverlay>();
            var pause = new GameObject("pause").AddComponent<TestOverlay>();
            var calib = new GameObject("calib").AddComponent<TestOverlay>();

            sm = mgr.AddComponent<ScreenManager>();
            SetPrivate(sm, "startRoot", startCanvas);
            SetPrivate(sm, "mainRoot", mainRoot);
            SetPrivate(sm, "gameplayRoot", gameplayRoot);
            SetPrivate(sm, "resultsCanvas", results);
            SetPrivate(sm, "settingsOverlay", settings);
            SetPrivate(sm, "pauseOverlay", pause);
            SetPrivate(sm, "calibrationOverlay", calib);

            nayoonBtnGO = new GameObject("nayoon"); nayoonBtn = nayoonBtnGO.AddComponent<Button>();
            soyoonBtnGO = new GameObject("soyoon"); soyoonBtn = soyoonBtnGO.AddComponent<Button>();

            var startGO = new GameObject("startScreen");
            startScreen = startGO.AddComponent<StartScreen>();
            SetPrivate(startScreen, "nayoonButton", nayoonBtn);
            SetPrivate(startScreen, "soyoonButton", soyoonBtn);
        }

        [TearDown]
        public void Teardown()
        {
            SessionProfile.Current = Profile.Nayoon;
            foreach (var go in new[] { mgr, startCanvas, mainRoot, gameplayRoot, results, nayoonBtnGO, soyoonBtnGO })
                if (go != null) Object.DestroyImmediate(go);
            // Clean up the overlay + startScreen GameObjects via their component owners.
            foreach (var overlay in GameObject.FindObjectsByType<TestOverlay>(FindObjectsSortMode.None))
                Object.DestroyImmediate(overlay.gameObject);
            foreach (var s in GameObject.FindObjectsByType<StartScreen>(FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);
        }

        [Test]
        public void SelectNayoon_SetsProfileNayoon()
        {
            SessionProfile.Current = Profile.Soyoon; // seed non-default
            startScreen.InvokeSelectForTest(Profile.Nayoon);
            Assert.AreEqual(Profile.Nayoon, SessionProfile.Current);
        }

        [Test]
        public void SelectSoyoon_SetsProfileSoyoon()
        {
            startScreen.InvokeSelectForTest(Profile.Soyoon);
            Assert.AreEqual(Profile.Soyoon, SessionProfile.Current);
        }

        [Test]
        public void SelectNayoon_ReplacesToMain()
        {
            sm.Replace(AppScreen.Start);
            startScreen.InvokeSelectForTest(Profile.Nayoon);
            Assert.AreEqual(AppScreen.Main, sm.Current);
            Assert.IsTrue(mainRoot.activeSelf);
            Assert.IsFalse(startCanvas.activeSelf);
        }

        [Test]
        public void SelectSoyoon_ReplacesToMain()
        {
            sm.Replace(AppScreen.Start);
            startScreen.InvokeSelectForTest(Profile.Soyoon);
            Assert.AreEqual(AppScreen.Main, sm.Current);
        }

        private static void SetPrivate(object t, string name, object v) =>
            t.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
              .SetValue(t, v);
    }
}
```

**Note**: these tests depend on `ScreenManager` knowing about `AppScreen.Start` and having a `startRoot` field — those arrive in Task 5. Compile will fail until then.

- [ ] **Step 4.2: Attempt compile, confirm failure (expected, deferred to Task 5)**

```
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -projectPath "$(pwd)" -runTests -testPlatform EditMode \
  -testFilter "StartScreenTests" -logFile - 2>&1 | tail -40
```

Expected: compile errors on `AppScreen.Start`, `startRoot`, `StartScreen`, `InvokeSelectForTest` — all 4 from different locations. Do NOT fix these yet; Task 5 lands them together.

- [ ] **Step 4.3: Implement StartScreen.cs**

Create `Assets/Scripts/UI/StartScreen.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    public class StartScreen : MonoBehaviour
    {
        [SerializeField] private Button nayoonButton;
        [SerializeField] private Button soyoonButton;

        private void Awake()
        {
            if (nayoonButton != null) nayoonButton.onClick.AddListener(() => Select(Profile.Nayoon));
            if (soyoonButton != null) soyoonButton.onClick.AddListener(() => Select(Profile.Soyoon));
        }

        private void Select(Profile p)
        {
            SessionProfile.Current = p;
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.Replace(AppScreen.Main);
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal void InvokeSelectForTest(Profile p) => Select(p);
#endif
    }
}
```

At this point the project still doesn't compile (missing `AppScreen.Start` in `ScreenManager`). Do NOT commit this file alone; it lands with Task 5's ScreenManager changes.

- [ ] **Step 4.4: Do NOT commit yet — continue to Task 5**

StartScreen.cs is staged for the Task 5 commit. This is because StartScreen depends on `AppScreen.Start` which doesn't exist in ScreenManager until Task 5, and the repo must compile at every committed state (for bisect-ability).

---

## Task 5: ScreenManager extensions + tests

**Spec reference:** §4.1, §4.7

**Files:**
- Modify: `Assets/Scripts/UI/ScreenManager.cs`
- Modify: `Assets/Tests/EditMode/ScreenManagerTests.cs`

- [ ] **Step 5.1: Add failing tests for Start flow + profile apply**

Append to `Assets/Tests/EditMode/ScreenManagerTests.cs` inside the `ScreenManagerTests` class:

```csharp
[Test]
public void Replace_Start_ActivatesOnlyStartRoot()
{
    // `startCanvas` must be added to Setup — see Step 5.2.
    sm.Replace(AppScreen.Start);
    Assert.IsTrue(startCanvas.activeSelf);
    Assert.IsFalse(mainRoot.activeSelf);
    Assert.IsFalse(gameplayRoot.activeSelf);
    Assert.IsFalse(results.activeSelf);
    Assert.AreEqual(AppScreen.Start, sm.Current);
}

[Test]
public void Replace_FromStartToMain_DeactivatesStart()
{
    sm.Replace(AppScreen.Start);
    sm.Replace(AppScreen.Main);
    Assert.IsFalse(startCanvas.activeSelf);
    Assert.IsTrue(mainRoot.activeSelf);
}

[Test]
public void HandleBack_FromMain_GoesToStart()
{
    sm.Replace(AppScreen.Main);
    sm.HandleBack();
    Assert.AreEqual(AppScreen.Start, sm.Current);
}

[Test]
public void Replace_MainToGameplay_AppliesProfileBackground()
{
    // Requires BackgroundSwitcher + wiring — see Step 5.3.
    SessionProfile.Current = Profile.Soyoon;
    int applyCount = 0;
    Profile? lastApplied = null;
    // testHook: BackgroundSwitcherSpy captures the apply call.
    var spyHost = new GameObject("spy");
    var spy = spyHost.AddComponent<BackgroundSwitcherSpy>();
    spy.OnApply = p => { applyCount++; lastApplied = p; };
    SetPrivate(sm, "backgroundSwitcher", spy);

    sm.Replace(AppScreen.Main);   // no apply
    Assert.AreEqual(0, applyCount);

    sm.Replace(AppScreen.Gameplay);  // apply Soyoon
    Assert.AreEqual(1, applyCount);
    Assert.AreEqual(Profile.Soyoon, lastApplied);

    Object.DestroyImmediate(spyHost);
}

// Companion class; place at file-bottom inside the ScreenManagerTests namespace.
public class BackgroundSwitcherSpy : KeyFlow.Feedback.BackgroundSwitcher
{
    public System.Action<Profile> OnApply;
    public new void Apply(Profile p) { OnApply?.Invoke(p); }
    // We use `new` rather than override because Apply is non-virtual.
    // The ScreenManager calls via the concrete type reference, so this shadow
    // intercepts the call correctly for test-driven Replace hooks.
}
```

**Wait — `new` shadow won't intercept if ScreenManager holds `BackgroundSwitcher` reference and calls `backgroundSwitcher.Apply(...)`.** The shadow only activates if the caller uses `BackgroundSwitcherSpy` as the static type. Fix by making `BackgroundSwitcher.Apply` virtual in the Task 3 implementation OR using a delegate-hook pattern.

Simpler: add a virtual keyword to `BackgroundSwitcher.Apply` during this task (minor retroactive change to Task 3's file). Document in the Task 5 commit. Replace spy with:

```csharp
public class BackgroundSwitcherSpy : KeyFlow.Feedback.BackgroundSwitcher
{
    public System.Action<Profile> OnApply;
    public override void Apply(Profile p) { OnApply?.Invoke(p); }
}
```

Step 5.1 therefore also edits `BackgroundSwitcher.cs` line to `public virtual void Apply(Profile p)`. Authored as part of Task 5's commit.

Also update `Setup` to wire `startCanvas`:

```csharp
// In [SetUp], add:
startCanvas = new GameObject("start");
SetPrivate(sm, "startRoot", startCanvas);
// In [TearDown]'s GameObject array, also include startCanvas.
```

- [ ] **Step 5.2: Confirm tests fail**

Run `-testFilter "ScreenManagerTests"`. Expected: compile errors — `AppScreen.Start`, `startRoot`, `backgroundSwitcher` fields absent.

- [ ] **Step 5.3: Implement ScreenManager extensions**

Edit `Assets/Scripts/UI/ScreenManager.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using KeyFlow;
using KeyFlow.Feedback;

namespace KeyFlow.UI
{
    public enum AppScreen { Start, Main, Gameplay, Results }  // Start inserted at head

    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance { get; private set; }

        [SerializeField] private GameObject startRoot;
        [SerializeField] private GameObject mainRoot;
        [SerializeField] private GameObject gameplayRoot;
        [SerializeField] private GameObject resultsCanvas;

        [SerializeField] private OverlayBase settingsOverlay;
        [SerializeField] private OverlayBase pauseOverlay;
        [SerializeField] private OverlayBase calibrationOverlay;

        [SerializeField] private BackgroundSwitcher backgroundSwitcher;

        public AppScreen Current { get; private set; }

        public event Action<AppScreen> OnReplaced;

        private float lastBackOnStart = -10f;
        private const float DoubleBackWindow = 2.0f;

        public OverlayBase SettingsOverlay => settingsOverlay;
        public OverlayBase PauseOverlay => pauseOverlay;
        public OverlayBase CalibrationOverlay => calibrationOverlay;

        public void Replace(AppScreen target)
        {
            HideAllOverlays();
            if (startRoot)     startRoot.SetActive(target == AppScreen.Start);
            if (mainRoot)      mainRoot.SetActive(target == AppScreen.Main);
            if (gameplayRoot)  gameplayRoot.SetActive(target == AppScreen.Gameplay);
            if (resultsCanvas) resultsCanvas.SetActive(target == AppScreen.Results);
            Current = target;

            if (target == AppScreen.Gameplay && backgroundSwitcher != null)
                backgroundSwitcher.Apply(SessionProfile.Current);

            OnReplaced?.Invoke(target);
        }

        public void ShowOverlay(OverlayBase o) { if (o != null) o.Show(); }
        public void HideOverlay(OverlayBase o) { if (o != null) o.Finish(); }

        public bool AnyOverlayVisible =>
            (settingsOverlay != null && settingsOverlay.IsVisible) ||
            (pauseOverlay != null && pauseOverlay.IsVisible) ||
            (calibrationOverlay != null && calibrationOverlay.IsVisible);

        private void HideAllOverlays()
        {
            if (settingsOverlay != null && settingsOverlay.IsVisible) settingsOverlay.Finish();
            if (pauseOverlay != null && pauseOverlay.IsVisible) pauseOverlay.Finish();
            if (calibrationOverlay != null && calibrationOverlay.IsVisible) calibrationOverlay.Finish();
        }

        public void HandleBack()
        {
            if (settingsOverlay != null && settingsOverlay.IsVisible) { settingsOverlay.Finish(); return; }
            if (calibrationOverlay != null && calibrationOverlay.IsVisible) return;
            if (pauseOverlay != null && pauseOverlay.IsVisible) { pauseOverlay.Finish(); return; }

            switch (Current)
            {
                case AppScreen.Gameplay:
                    if (pauseOverlay != null) pauseOverlay.Show();
                    break;
                case AppScreen.Results:
                    Replace(AppScreen.Main);
                    break;
                case AppScreen.Main:
                    Replace(AppScreen.Start);
                    break;
                case AppScreen.Start:
                    if (Time.unscaledTime - lastBackOnStart < DoubleBackWindow)
                    {
                        Debug.Log("[ScreenManager] Quit requested on double-back from Start");
                        Application.Quit();
                    }
                    else
                    {
                        lastBackOnStart = Time.unscaledTime;
                    }
                    break;
            }
        }

        private void Awake() { Instance = this; }

        private void Start() { Replace(AppScreen.Start); }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            HandleBack();
        }
    }
}
```

Changes from current:
- `enum AppScreen { Main, Gameplay, Results }` → `{ Start, Main, Gameplay, Results }` (Start at head).
- New `[SerializeField] GameObject startRoot`.
- New `[SerializeField] BackgroundSwitcher backgroundSwitcher`.
- `Replace` toggles `startRoot` + fires `backgroundSwitcher.Apply` on Gameplay.
- `HandleBack` switch: Main → Start (was double-back quit); Start → double-back quit with Debug.Log.
- `lastBackOnMain` field renamed to `lastBackOnStart`.
- `Start()` replaces to `AppScreen.Start` (was `Main`).
- `using KeyFlow.Feedback;` added for `BackgroundSwitcher`.

- [ ] **Step 5.4: Bump `BackgroundSwitcher.Apply` to virtual**

Edit `Assets/Scripts/Feedback/BackgroundSwitcher.cs`:

```csharp
public virtual void Apply(Profile p)
{
    ...
}
```

(Minimal change: add the `virtual` keyword. Enables the `BackgroundSwitcherSpy` in tests to override. Production behavior unchanged.)

- [ ] **Step 5.5: Run tests, confirm all pass**

```
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -projectPath "$(pwd)" -runTests -testPlatform EditMode \
  -logFile - 2>&1 | tail -60
```

Expected: the full EditMode suite green. Counts:
- Prior (pre-SP9): 148
- Task 2 (ProfileTests): +3
- Task 3 (BackgroundSwitcherTests): +4
- Task 4 (StartScreenTests): +4 (now compile-unblocked)
- Task 5 (ScreenManagerTests): +4 new cases appended
- Total: 148 + 15 = 163 green

If `ScreenManagerTests.Replace_Main_ActivatesOnlyMainRoot` (existing test) fails because of the `startRoot.SetActive(false)` new line — update it to also assert `startCanvas.activeSelf == false`. Similarly any existing test that checks "3 roots" now needs to cover 4.

Actually audit and update the existing `ScreenManagerTests` cases that enumerate roots. This is a small fix (3-5 tests likely touch it).

- [ ] **Step 5.6: Commit Tasks 4 + 5 together**

```
git add Assets/Scripts/UI/StartScreen.cs Assets/Scripts/UI/StartScreen.cs.meta \
        Assets/Scripts/UI/ScreenManager.cs \
        Assets/Scripts/Feedback/BackgroundSwitcher.cs \
        Assets/Tests/EditMode/StartScreenTests.cs Assets/Tests/EditMode/StartScreenTests.cs.meta \
        Assets/Tests/EditMode/ScreenManagerTests.cs
git commit -m "$(cat <<'EOF'
feat(w6-sp9): AppScreen.Start + StartScreen + profile-background-apply hook

ScreenManager now has four screens (Start at head of enum) with
startRoot SerializeField and backgroundSwitcher SerializeField. Replace
fires BackgroundSwitcher.Apply(SessionProfile.Current) on transition to
Gameplay. HandleBack case table: Main → Start (was quit); Start →
double-back quit.

StartScreen MonoBehaviour binds two buttons to profile selection then
Replace(AppScreen.Main). InvokeSelectForTest hook gated by test flags.

BackgroundSwitcher.Apply made virtual so a test spy can intercept.

Adds 4 StartScreenTests + 4 new ScreenManagerTests cases; updates
existing ScreenManagerTests to cover 4 roots instead of 3.
EOF
)"
```

---

## Task 6: SceneBuilder integration + scene regeneration

**Spec reference:** §4.1, §4.2 (hit-box measurement), §4.6 (SceneBuilder load paths)

**Files:**
- Modify: `Assets/Editor/SceneBuilder.cs`
- Regenerate: `Assets/Scenes/GameplayScene.unity`

- [ ] **Step 6.1: Measure start.png hit-box coordinates**

From the worktree root, run:
```
python -c "from PIL import Image; im=Image.open('img/start.png'); print(im.size)"
```

Record `(W_px, H_px)`. If PIL isn't available, open `img/start.png` in Windows Paint or an online image viewer.

Open the same image in a viewer that shows pixel coordinates under the cursor. Find each "시작하기" button's visual center and size:
- `(Nx, Ny)` — center of 나윤's 시작하기 button (left profile card). Measure Y from BOTTOM (Unity UI convention).
- `(Sx, Sy)` — center of 소윤's 시작하기 button (right profile card).
- `(Bw_px, Bh_px)` — width and height of one button's rounded-rect. Both buttons same size.

Record results as a comment block you'll paste into SceneBuilder.

**Sanity check**: `Nx / W_px` should be around 0.25–0.30; `Sx / W_px` around 0.70–0.75; `Ny / H_px` and `Sy / H_px` both around 0.30–0.40. Expected BUTTON_SIZE reference units ≈ 200×80 to 260×100.

- [ ] **Step 6.2: Add `BuildStartCanvas` helper to SceneBuilder**

Insert into `Assets/Editor/SceneBuilder.cs` near `BuildMainCanvas` (around line 531). Use the measurements from Step 6.1:

```csharp
// Start-screen hit-box coordinates authored from img/start.png measurement
// (Step 6.1 procedure). Update these if start.png is re-authored.
// Source: start.png is W_px x H_px pixels.
// Nayoon button center: (Nx, Ny) px from top-left; measured Ny from bottom for Unity convention.
// Soyoon button center: (Sx, Sy) px.
// Button size on source: (Bw_px, Bh_px) px. +20% margin for thumb-tap forgiveness.
private static readonly Vector2 NAYOON_ANCHOR = new Vector2(<Nx/W_px>, <Ny/H_px>);
private static readonly Vector2 SOYOON_ANCHOR = new Vector2(<Sx/W_px>, <Sy/H_px>);
private static readonly Vector2 BUTTON_SIZE   = new Vector2(<Bw_px*720/W_px*1.2>, <Bh_px*1280/H_px*1.2>);

private static GameObject BuildStartCanvas(Sprite startBg, out StartScreen startScreen)
{
    var canvasGO = new GameObject("StartCanvas");
    var canvas = canvasGO.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 7;
    var scaler = canvasGO.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(720, 1280);
    scaler.matchWidthOrHeight = 0.5f;
    canvasGO.AddComponent<GraphicRaycaster>();

    // Full-screen backdrop
    var bgGO = new GameObject("Background", typeof(RectTransform));
    bgGO.transform.SetParent(canvasGO.transform, false);
    var bgRT = bgGO.GetComponent<RectTransform>();
    bgRT.anchorMin = Vector2.zero;
    bgRT.anchorMax = Vector2.one;
    bgRT.offsetMin = Vector2.zero;
    bgRT.offsetMax = Vector2.zero;
    var bgImg = bgGO.AddComponent<Image>();
    bgImg.sprite = startBg;
    bgImg.preserveAspect = false;
    bgImg.raycastTarget = false;

    // Nayoon invisible button
    var nayoonBtn = BuildInvisibleButton(canvasGO.transform, "NayoonButton", NAYOON_ANCHOR, BUTTON_SIZE);
    // Soyoon invisible button
    var soyoonBtn = BuildInvisibleButton(canvasGO.transform, "SoyoonButton", SOYOON_ANCHOR, BUTTON_SIZE);

    // StartScreen coordinator
    startScreen = canvasGO.AddComponent<StartScreen>();
    SetField(startScreen, "nayoonButton", nayoonBtn);
    SetField(startScreen, "soyoonButton", soyoonBtn);

    return canvasGO;
}

private static Button BuildInvisibleButton(Transform parent, string name, Vector2 anchorXY, Vector2 size)
{
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var rt = go.GetComponent<RectTransform>();
    rt.anchorMin = anchorXY;
    rt.anchorMax = anchorXY;
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.anchoredPosition = Vector2.zero;
    rt.sizeDelta = size;

    var img = go.AddComponent<Image>();
    img.color = new Color(1f, 1f, 1f, 0f);  // invisible but raycastTarget = true by default
    return go.AddComponent<Button>();
}
```

- [ ] **Step 6.3: Update `BuildBackgroundCanvas` to attach BackgroundSwitcher**

Edit `Assets/Editor/SceneBuilder.cs`, modify `BuildBackgroundCanvas` signature + body. Current:
```csharp
private static void BuildBackgroundCanvas(Sprite bgSprite, Camera cam)
```

Change to:
```csharp
private static BackgroundSwitcher BuildBackgroundCanvas(Sprite blueBg, Sprite yellowBg, Camera cam)
{
    // ... existing canvas + image setup unchanged, but use blueBg as initial sprite ...
    // (replace `img.sprite = bgSprite;` with `img.sprite = blueBg;`)

    // Add the BackgroundSwitcher component on the same GameObject as the Image.
    var switcher = imgGO.AddComponent<BackgroundSwitcher>();
    SetField(switcher, "backgroundImage", img);
    SetField(switcher, "blueBg", blueBg);
    SetField(switcher, "yellowBg", yellowBg);

    return switcher;
}
```

- [ ] **Step 6.4: Thread yellowBg + startBg through `Build()`**

At the top of `Build()` (around line 70), after the existing `bgSprite` load:

```csharp
var blueBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/background_gameplay.png");
if (blueBg == null) { Debug.LogError("[KeyFlow] Missing Assets/Sprites/background_gameplay.png."); return; }

var yellowBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/background_yellow.png");
if (yellowBg == null) { Debug.LogError("[KeyFlow] Missing Assets/Sprites/background_yellow.png."); return; }

var startBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/background_start.png");
if (startBg == null) { Debug.LogError("[KeyFlow] Missing Assets/Sprites/background_start.png."); return; }
```

Replace the existing `BuildBackgroundCanvas(bgSprite, camera);` call with:

```csharp
var backgroundSwitcher = BuildBackgroundCanvas(blueBg, yellowBg, camera);
```

Remove the old `bgSprite` local if unused.

- [ ] **Step 6.5: Build the start canvas and wire it to ScreenManager**

After `var mainCanvas = BuildMainCanvas(whiteSprite);` (around line 107), add:

```csharp
var startCanvas = BuildStartCanvas(startBg, out var startScreen);
```

Then in the existing `screenMgr` wiring block (around line 118), add two fields:

```csharp
SetField(screenMgr, "startRoot", startCanvas);
SetField(screenMgr, "backgroundSwitcher", backgroundSwitcher);
```

- [ ] **Step 6.6: Regenerate the scene**

Close any interactive Unity Editor on this project, then run:

```
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath "$(pwd)" \
  -executeMethod KeyFlow.Editor.SceneBuilder.Build \
  -logFile - -quit 2>&1 | tail -80
```

Expected: exit code 0; log shows `SceneBuilder.Build() completed`; `Assets/Scenes/GameplayScene.unity` modified. `.unity` diff will be large (Unity serialization) but semantically just adds the StartCanvas subtree and the BackgroundSwitcher component.

If it fails with "Unity is already running" or IL2CPP error, STOP and report BLOCKED.

- [ ] **Step 6.7: Re-run EditMode tests**

```
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -projectPath "$(pwd)" -runTests -testPlatform EditMode \
  -logFile - 2>&1 | tail -60
```

Expected: all 163 tests green (148 prior + 15 SP9).

- [ ] **Step 6.8: Play-in-Editor hit-box check**

Open Unity Editor, load `Assets/Scenes/GameplayScene.unity`, hit Play.
- Verify Start screen renders start.png fullscreen.
- Click each "시작하기" button CENTER — should transition to MainScreen. Profile (inspect in a quick debug console log, or just assume correct from test coverage).
- Click button CORNERS — should still register.
- If miss > 20 px → return to Step 6.1 and re-measure.

If coordinates need tuning, edit the 3 constants in SceneBuilder and re-run Step 6.6. Keep iterating until all 4 corners register.

- [ ] **Step 6.9: Commit**

```
git add Assets/Editor/SceneBuilder.cs Assets/Scenes/GameplayScene.unity
git commit -m "$(cat <<'EOF'
chore(w6-sp9): SceneBuilder integration + regenerate GameplayScene

BuildStartCanvas creates StartCanvas with full-screen backdrop +
invisible Nayoon/Soyoon buttons positioned from img/start.png
measurements. BuildBackgroundCanvas now attaches BackgroundSwitcher
and returns it for ScreenManager wiring. ScreenManager gets new
startRoot + backgroundSwitcher field wiring. GameplayScene regenerated.
EOF
)"
```

---

## Task 7: Android app icon + APK build + device playtest + completion report

**Spec reference:** §4.8, §7 (Device playtest)

**Files:**
- Modify: `ProjectSettings/ProjectSettings.asset`
- Create: `docs/superpowers/reports/2026-04-24-w6-sp9-profile-start-screen-completion.md`

- [ ] **Step 7.1: Assign Android icon in Player Settings**

Open Unity Editor. Edit → Project Settings → Player → Android tab → Icon section.

Assign `Assets/Textures/icon.png` to every Legacy Icon density bucket (192, 144, 96, 72, 48, 36 dp). Leave Adaptive Icon unassigned (Android falls back to Legacy per spec §3 non-goal).

Save. Verify the `ProjectSettings.asset` diff:

```
git diff ProjectSettings/ProjectSettings.asset | head -40
```

Expected: icon fileID references only — no unrelated key changes. If other settings have drifted, revert them before committing.

- [ ] **Step 7.2: Close Unity Editor, build release APK**

Per project memory (feedback_unity_batch_mode.md), batch-mode build requires Editor closed.

```
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath "$(pwd)" \
  -executeMethod KeyFlow.Editor.ApkBuilder.Build \
  -logFile - -quit 2>&1 | tail -120
```

Expected: `Builds/keyflow-w6-sp2.apk` refreshed (release APK name unchanged per SP3 convention). Exit code 0.

- [ ] **Step 7.3: Install and device playtest on Galaxy S22**

```
adb install -r Builds/keyflow-w6-sp2.apk
```

Playtest checklist:
1. Launcher icon — confirm the new piano-kid icon appears on the home screen and app drawer.
2. Launch app → Start screen displays `start.png` fullscreen.
3. Tap 나윤's 시작하기 button CENTER → MainScreen loads. Pick any song → gameplay loads with BLUE background.
4. Tap Android back → Main. Tap back again → Start screen.
5. Tap 소윤's 시작하기 button → MainScreen → song → YELLOW background.
6. Corner-tap both buttons; verify hit-boxes forgive edge contact.
7. Extreme: start at start → double-tap back within 2s → app quits.
8. 4 decorative bottom menu items + top-bar icons → verify they are visually present but do NOT respond to taps.

Record verdict + any hit-box misalignment notes.

- [ ] **Step 7.4: Profiler attach during Soyoon gameplay**

Build profile APK:
```
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath "$(pwd)" \
  -executeMethod KeyFlow.Editor.ApkBuilder.BuildProfile \
  -logFile - -quit 2>&1 | tail -60
```

Install profile APK, attach Unity Profiler. Run 2-minute Entertainer Normal with SOYOON profile (yellow background). Confirm:
- `GC.Collect == 0` — SP3 baseline preserved.
- Per-frame allocation from `ScreenManager.Replace` and `BackgroundSwitcher.Apply` = 0 B (they run at screen-transition only, not per-frame).

- [ ] **Step 7.5: Write completion report**

Use `docs/superpowers/reports/2026-04-24-w6-sp8-hold-note-polish-completion.md` as the template. Create `docs/superpowers/reports/2026-04-24-w6-sp9-profile-start-screen-completion.md` with:

- Summary: profile flow working, icon updated, both backgrounds verified.
- Commits list (7 expected).
- Files touched summary.
- Hit-box coordinates authored (Step 6.1 numbers), recorded for future maintenance.
- APK size delta vs. SP8 baseline (expected ≤ +500 KB per spec §9).
- Test suite growth: 148 → 163 (+15) — confirm.
- Profiler result: GC.Collect count; regression vs. SP8 baseline.
- Device playtest verdicts per §7.3 checklist items.
- Carry-overs (if any): decorative menu items, 보호자 mode, adaptive icon, per-profile PlayerPrefs.

- [ ] **Step 7.6: Commit icon + completion report**

Two separate commits:

```
git add ProjectSettings/ProjectSettings.asset
git commit -m "chore(w6-sp9): Android app icon → icon.png (all legacy density buckets)"

git add docs/superpowers/reports/2026-04-24-w6-sp9-profile-start-screen-completion.md
git commit -m "docs(w6-sp9): completion report"
```

- [ ] **Step 7.7: Update memory**

Write `memory/project_w6_sp9_complete.md` following the template of `memory/project_w6_sp8_complete.md`. Add one line to `memory/MEMORY.md` index.

This file lives under `C:\Users\lhk\.claude\projects\C--dev-unity-music\memory\`, NOT in the repo.

---

## Final Verification Checklist

Before considering SP9 merge-ready:

- [ ] Unity EditMode: 163 tests green (148 prior + 15 SP9)
- [ ] Device (S22) release APK: new icon; Start screen lands; both profiles produce the right background; back navigation flows through Start
- [ ] Profiler: GC.Collect == 0 during Entertainer Normal with yellow background
- [ ] APK size delta ≤ +500 KB vs. SP8 baseline
- [ ] Completion report written and committed
- [ ] Memory updated

---

## Notes for the implementer

- **Unity batch-mode rules** (from `memory/feedback_unity_batch_mode.md`): `-runTests` without `-quit`; `-executeMethod` with `-quit`; interactive Editor must be closed. All Unity CLI commands run foreground.
- **`InternalsVisibleTo`** is declared exactly once in `Assets/Scripts/Gameplay/JudgmentSystem.cs:6`. Do NOT re-declare in StartScreen, BackgroundSwitcher, or any test file.
- **`SessionProfile.Current` static persists across EditMode tests** in the same AppDomain. Every test suite that touches it MUST reset in `[SetUp]` (pattern shown in ProfileTests and StartScreenTests).
- **BackgroundSwitcher.Apply is virtual** — called by ScreenManager via concrete reference; the virtual keyword enables the test spy pattern used in `Replace_MainToGameplay_AppliesProfileBackground`. Production behavior unchanged.
- **Hit-box coordinates in Step 6.1** are the longest-tail risk. The measurement procedure and acceptance check (§6.8) must both pass before device build. Re-measuring is cheap; rebuilding APK 3x because coordinates drift is expensive.
- **Frequent commits** — Tasks 1, 2, 3 each get their own commit. Tasks 4+5 bundle because StartScreen requires ScreenManager changes to compile (bisect invariant: every commit compiles). Task 6 is one commit (scene + SceneBuilder). Task 7 is 2-3 commits (icon settings, report, and optionally the memory update if tracked in the repo — which it isn't).
- **If `Unity.exe` path differs on your setup**, edit the one-line path and continue. Don't hardcode elsewhere.
