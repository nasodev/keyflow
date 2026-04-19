# KeyFlow W1 PoC Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a minimal Unity 6 + Android PoC that (a) drops a single note from spawn to judgment line using `AudioSettings.dspTime`, (b) plays a piano sample on tap via an `AudioSource` pool, (c) measures tap-to-audio latency + FPS on a real Android device, and (d) produces a signed APK for install on test devices. This is the **technical go/no-go gate** for the entire KeyFlow MVP.

**Architecture:** Single Unity scene (`GameplayScene`) with three concerns separated into focused MonoBehaviours — `AudioSyncManager` (dspTime single-source-of-truth), `NoteController` (pure dspTime→position math), `TapInputHandler` + `AudioSamplePool` (tap → piano sample with pooled `AudioSource`s). Pure timing/math logic lives in a testable `GameTime` static class (EditMode tests). On-screen `LatencyMeter` HUD reports tap-to-audio delta, FPS, and dspTime drift for empirical validation.

**Tech Stack:** Unity 6 LTS (6000.0.x) + C# + .NET Standard 2.1, Android Build Support module (IL2CPP, arm64-v8a), Unity Test Framework (EditMode NUnit tests), Unity's new Input System package, OGG Vorbis audio. No third-party asset store packages in W1 PoC (Native Audio plugin decision deferred per spec §0).

**Reference spec:** [2026-04-19-keyflow-mvp-design.md](../specs/2026-04-19-keyflow-mvp-design.md) — this PoC validates §6.2 (audio path), §5.4 (dspTime scrolling), §9 W1 milestone, §12 success criteria #1 (60 fps).

**Target environment:** `C:\dev\unity-music` (Windows 10, bash shell). Unity project lives at repo root alongside `docs/`. Git initialized at repo root.

---

## File Structure

Files created or modified in this plan:

```
C:\dev\unity-music\
├─ .gitignore                              (Unity-flavored ignore list)
├─ .gitattributes                          (LFS for binaries)
├─ Assets\
│   ├─ Scenes\
│   │   └─ GameplayScene.unity             (single scene for PoC)
│   ├─ Scripts\
│   │   ├─ Gameplay\
│   │   │   ├─ AudioSyncManager.cs         (dspTime single source)
│   │   │   ├─ NoteController.cs           (note falls from spawn→judgment)
│   │   │   ├─ NoteSpawner.cs              (spawns ONE hardcoded note)
│   │   │   ├─ TapInputHandler.cs          (tap detection via InputSystem)
│   │   │   └─ AudioSamplePool.cs          (16× AudioSource pool)
│   │   ├─ Common\
│   │   │   └─ GameTime.cs                 (pure math, testable)
│   │   └─ UI\
│   │       └─ LatencyMeter.cs             (HUD: FPS + tap-latency + drift)
│   ├─ Prefabs\
│   │   └─ Note.prefab                     (sprite + NoteController)
│   ├─ Audio\
│   │   └─ piano_c4.ogg                    (single Salamander sample, MIDI 60)
│   ├─ Sprites\
│   │   ├─ note_circle.png                 (8×8 white circle → note)
│   │   └─ judgment_line.png               (horizontal beam)
│   ├─ Tests\
│   │   └─ EditMode\
│   │       ├─ KeyFlow.Tests.EditMode.asmdef
│   │       ├─ GameTimeTests.cs
│   │       └─ AudioSamplePoolTests.cs
│   └─ KeyFlow.Runtime.asmdef               (runtime assembly definition)
├─ ProjectSettings\                         (Unity-generated, committed)
├─ Packages\
│   └─ manifest.json                        (InputSystem, TestFramework)
└─ docs\
    └─ superpowers\
        └─ plans\
            └─ 2026-04-19-keyflow-w1-poc.md  (this file)
```

**Design notes:**
- `GameTime.cs` is a static class holding pure functions (`GetSongTimeMs`, `GetNoteProgress`, `PitchToX`) so business-critical timing math is unit-testable in EditMode without a running scene.
- `AudioSyncManager` owns the `songStartDspTime` value — every other component reads from it, never recomputes from `Time.time`.
- `NoteController` is "dumb": it receives a reference to `AudioSyncManager` + its own `hitTime` and computes its position each frame by asking `GameTime` for progress.
- Separating `TapInputHandler` from `AudioSamplePool` means input handling can be replaced (multi-touch in v1.0) without touching the audio layer.

---

## Prerequisites (W0 checklist)

Before Task 1, the following must be satisfied per spec §14:

1. **Unity Hub** installed (from https://unity.com/download)
2. **Unity 6 LTS** (6000.0.x) installed via Hub with modules: *Android Build Support*, *OpenJDK*, *Android SDK & NDK Tools*
3. **Android test device** with USB debugging enabled + USB cable + `adb devices` shows device
4. **Salamander Grand Piano V3** downloaded locally (CC0): https://freepats.zenvoid.org/Piano/acoustic-grand-piano.html — we only need one sample (C4) for W1
5. **Git** installed and configured (`git config --global user.name/email` set)
6. **Audacity** or any OGG converter for trimming the C4 sample
7. **Test device placed within 30cm of the user** for tap-latency measurement (distance affects perceived latency — keep test environment consistent)

---

## Task 1: Initialize repo + Unity project skeleton

**Files:**
- Create: `C:\dev\unity-music\.gitignore`
- Create: `C:\dev\unity-music\.gitattributes`
- Create: `C:\dev\unity-music\Assets\` (via Unity Hub)
- Create: `C:\dev\unity-music\ProjectSettings\` (via Unity Hub)
- Create: `C:\dev\unity-music\Packages\manifest.json` (via Unity Hub)

- [ ] **Step 1.1: Initialize git at repo root**

```bash
cd /c/dev/unity
git init -b main
git status
```

Expected: `On branch main`, `docs/` untracked.

- [ ] **Step 1.2: Write Unity-flavored `.gitignore`**

Create `C:\dev\unity-music\.gitignore`:

```gitignore
# Unity generated
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
[Mm]emoryCaptures/
[Rr]ecordings/

# Never ignore Asset meta data
!/[Aa]ssets/**/*.meta

# Uncomment this line if you wish to ignore the asset store tools plugin
# /[Aa]ssets/AssetStoreTools*

# Autogenerated Jetbrains Rider plugin
/[Aa]ssets/Plugins/Editor/JetBrains*

# Visual Studio cache directory
.vs/

# Gradle cache directory
.gradle/

# Autogenerated VS/MD/Consulo solution and project files
ExportedObj/
.consulo/
*.csproj
*.unityproj
*.sln
*.suo
*.tmp
*.user
*.userprefs
*.pidb
*.booproj
*.svd
*.pdb
*.mdb
*.opendb
*.VC.db

# Unity3D generated meta files
*.pidb.meta
*.pdb.meta
*.mdb.meta

# Unity3D generated file on crash reports
sysinfo.txt

# Builds
*.apk
*.aab
*.unitypackage
*.app

# Crashlytics generated file
crashlytics-build.properties

# Packed Addressables
/[Aa]ssets/[Aa]ddressable[Aa]ssets[Dd]ata/*/*.bin*

# Temporary auto-generated Android Assets
/[Aa]ssets/[Ss]treaming[Aa]ssets/aa.meta
/[Aa]ssets/[Ss]treaming[Aa]ssets/aa/*

# Keystore (NEVER commit production keystores)
*.keystore
*.jks
```

- [ ] **Step 1.3: Write `.gitattributes` for binary tracking**

Create `C:\dev\unity-music\.gitattributes`:

```gitattributes
# Normalize line endings for text
* text=auto eol=lf

# Unity text serialized assets (keep as text for diffing)
*.unity   text merge=unityyamlmerge eol=lf
*.asset   text merge=unityyamlmerge eol=lf
*.prefab  text merge=unityyamlmerge eol=lf
*.meta    text eol=lf
*.mat     text eol=lf
*.anim    text eol=lf
*.controller text eol=lf

# Binaries - no merge attempts
*.png   binary
*.jpg   binary
*.wav   binary
*.ogg   binary
*.mp3   binary
*.fbx   binary
*.dll   binary
*.keystore binary
```

- [ ] **Step 1.4: Create Unity project via Unity Hub (MANUAL STEP)**

In Unity Hub:
1. Click *New project*
2. Editor version: Unity 6000.0.x LTS
3. Template: **2D (Built-In Render Pipeline)**
4. Project name: `KeyFlow`
5. Location: `C:\dev` (so it resolves to `C:\dev\unity-music` — overwrite/merge OK since only `docs/` exists)
6. Click *Create project*

Wait for Unity to finish importing packages (~2-5 minutes).

> **Unity note for web devs:** Unity will generate `Assets/`, `Library/`, `Packages/`, `ProjectSettings/`, and a bunch of `*.csproj`/`*.sln` files. Only `Assets/`, `Packages/`, and `ProjectSettings/` get committed. The `.gitignore` in step 1.2 handles the rest.

- [ ] **Step 1.5: Verify the project opened**

Check `C:\dev\unity-music` now contains:
- `Assets/` (with default `Scenes/SampleScene.unity`)
- `Packages/manifest.json`
- `ProjectSettings/ProjectVersion.txt`

Run:
```bash
cat /c/dev/unity/ProjectSettings/ProjectVersion.txt
```

Expected: `m_EditorVersion: 6000.0.x` (matches Unity 6 LTS).

- [ ] **Step 1.6: Commit**

```bash
cd /c/dev/unity
git add .gitignore .gitattributes Assets/ Packages/ ProjectSettings/ docs/
git status   # sanity check — Library/ must NOT appear
git commit -m "chore: initialize Unity 6 project and repo scaffolding"
```

---

## Task 2: Configure Player + Audio settings for Android

**Files:**
- Modify: `ProjectSettings/ProjectSettings.asset` (via Unity Editor)
- Modify: `ProjectSettings/AudioManager.asset` (via Unity Editor)

All changes happen via Unity Editor UI (`Edit → Project Settings`), which serializes to YAML files committed to git.

- [ ] **Step 2.1: Switch build target to Android**

Unity: `File → Build Profiles → Android → Switch Platform`. Wait for asset reimport.

> **Unity note:** Switching platforms reimports all assets (textures get ETC2-compressed for Android, etc.). First-time switch can take 5+ minutes.

- [ ] **Step 2.2: Configure Player Settings**

`Edit → Project Settings → Player → Android tab`:

| Field | Value |
|---|---|
| Company Name | `yourcompany` (or chosen org name) |
| Product Name | `KeyFlow` |
| Package Name | `com.yourcompany.keyflow` |
| Version | `0.1.0` |
| Bundle Version Code | `1` |
| Minimum API Level | Android 8.0 (API 26) |
| Target API Level | Android 15 (API 35) |
| Scripting Backend | **IL2CPP** |
| Api Compatibility Level | .NET Standard 2.1 |
| Target Architectures | **ARM64 only** (uncheck ARMv7) |
| Default Orientation | **Landscape Left** |
| Optimized Frame Pacing | **Enabled** (helps 60fps stability) |

- [ ] **Step 2.3: Configure Quality settings**

`Edit → Project Settings → Quality`:
- For Android default tier, set *VSync Count* = `Don't Sync` (we rely on `Application.targetFrameRate`)
- Anti Aliasing: `Disabled` (PoC — save GPU budget)

- [ ] **Step 2.4: Configure Audio settings (CRITICAL for latency)**

`Edit → Project Settings → Audio`:

| Field | Value |
|---|---|
| System Sample Rate | `48000` |
| DSP Buffer Size | **Best Latency** |
| Max Real Voices | `32` |
| Max Virtual Voices | `128` |

> **Unity note:** `Best Latency` = 256 samples ≈ 5.3ms at 48kHz. This is the single most impactful audio latency setting in Unity — do not skip.

- [ ] **Step 2.5: Install Input System package**

`Window → Package Manager → Unity Registry → Input System → Install`.

When prompted "Enable new Input System?": choose **Yes** (restarts editor, uses new system instead of legacy `Input.GetTouch`).

- [ ] **Step 2.6: Verify settings persisted**

```bash
grep -E "(bundleVersion|productName|targetApiLevel|scriptingBackend)" /c/dev/unity/ProjectSettings/ProjectSettings.asset | head -20
```

Expected: `productName: KeyFlow`, `scriptingBackend: 1` (IL2CPP), `AndroidMinSdkVersion: 26`, etc.

- [ ] **Step 2.7: Commit**

```bash
cd /c/dev/unity
git add ProjectSettings/ Packages/manifest.json Packages/packages-lock.json
git commit -m "chore: configure Android Player + Audio + Input System for W1 PoC"
```

---

## Task 3: Prepare piano audio sample

**Files:**
- Create: `Assets/Audio/piano_c4.ogg`
- Create: `Assets/Audio/piano_c4.ogg.meta` (Unity-generated)

- [ ] **Step 3.1: Extract C4 sample from Salamander (MANUAL)**

Open any C4 sample file from the Salamander V3 download (filename like `A0v8.wav` — use the MIDI 60 / C4 equivalent, typically `C4v8.wav` or similar). Trim to ~1.5 seconds of audio (release included).

- [ ] **Step 3.2: Convert to OGG Vorbis 128 kbps**

Using Audacity:
1. File → Open → (Salamander C4 WAV)
2. Select first 1.5 seconds (tail silence OK, we want natural decay)
3. File → Export → Export as OGG → Quality 5 (~128 kbps)
4. Save as `C:\dev\unity-music\Assets\Audio\piano_c4.ogg`

Target size: < 50 KB.

- [ ] **Step 3.3: Configure audio import settings in Unity**

Select `Assets/Audio/piano_c4.ogg` in Project window. In Inspector:

| Field | Value |
|---|---|
| Force To Mono | ✅ Enabled (saves ~50% size) |
| Normalize | ✅ Enabled |
| Load In Background | ❌ Disabled |
| **Load Type** | **Decompress On Load** ← critical |
| **Preload Audio Data** | ✅ Enabled ← critical |
| Compression Format | Vorbis |
| Quality | 70 |

Click **Apply**.

> **Unity note:** `Decompress On Load` + `Preload Audio Data` = the sample sits in RAM as PCM from scene load, so `PlayOneShot` has zero decode latency. Without this, first playback stalls while Unity decodes.

- [ ] **Step 3.4: Commit**

```bash
cd /c/dev/unity
git add Assets/Audio/
git commit -m "feat(audio): add piano C4 sample with decompress-on-load settings"
```

---

## Task 4: Create sprites for note + judgment line

**Files:**
- Create: `Assets/Sprites/note_circle.png`
- Create: `Assets/Sprites/judgment_line.png`

- [ ] **Step 4.1: Create `note_circle.png`**

Any 64×64 PNG with a filled white circle on transparent background. Can be generated in Paint.NET, Photoshop, or downloaded. Save to `C:\dev\unity-music\Assets\Sprites\note_circle.png`.

- [ ] **Step 4.2: Create `judgment_line.png`**

A 512×8 PNG, solid white with 1-pixel feathered top/bottom, transparent background. Save to `C:\dev\unity-music\Assets\Sprites\judgment_line.png`.

- [ ] **Step 4.3: Configure sprite import settings**

For both files, Inspector:
- Texture Type: **Sprite (2D and UI)**
- Sprite Mode: Single
- Pixels Per Unit: `100`
- Filter Mode: Bilinear
- Compression: None (PoC — tiny assets)

Click **Apply**.

- [ ] **Step 4.4: Commit**

```bash
cd /c/dev/unity
git add Assets/Sprites/
git commit -m "feat(art): add placeholder note circle + judgment line sprites"
```

---

## Task 5: Assembly definitions + test scaffold

**Files:**
- Create: `Assets/KeyFlow.Runtime.asmdef`
- Create: `Assets/Tests/EditMode/KeyFlow.Tests.EditMode.asmdef`

> **Unity note (web-dev analogue):** Assembly Definitions (`.asmdef`) are like `package.json` in monorepos — they carve Assets into compile units. Without them, one `.cs` edit recompiles the whole project (~10s). With them, only the affected assembly recompiles. We need one for runtime code and one for tests so tests can reference runtime but not vice versa.

- [ ] **Step 5.1: Create runtime asmdef**

In Unity: right-click `Assets` → `Create → Assembly Definition`. Name it `KeyFlow.Runtime`.

Inspect the generated `Assets/KeyFlow.Runtime.asmdef` and edit (either via Inspector or text editor) to:

```json
{
  "name": "KeyFlow.Runtime",
  "rootNamespace": "KeyFlow",
  "references": ["Unity.InputSystem"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

Click **Apply** in Inspector if edited via UI.

- [ ] **Step 5.2: Create EditMode test asmdef**

In Unity: `Window → General → Test Runner → EditMode tab → Create EditMode Test Assembly Folder`.

This creates `Assets/Tests/EditMode/KeyFlow.Tests.EditMode.asmdef`. Edit it to reference the runtime assembly:

```json
{
  "name": "KeyFlow.Tests.EditMode",
  "rootNamespace": "KeyFlow.Tests",
  "references": [
    "KeyFlow.Runtime",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ],
  "optionalUnityReferences": ["TestAssemblies"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

- [ ] **Step 5.3: Verify Test Runner sees the assembly**

`Window → General → Test Runner → EditMode tab`. The assembly `KeyFlow.Tests.EditMode` should appear in the list (empty — no tests yet).

- [ ] **Step 5.4: Commit**

```bash
cd /c/dev/unity
git add Assets/KeyFlow.Runtime.asmdef Assets/KeyFlow.Runtime.asmdef.meta Assets/Tests/
git commit -m "chore: add runtime + editmode test assembly definitions"
```

---

## Task 6: `GameTime` pure logic + tests (TDD)

**Files:**
- Create: `Assets/Scripts/Common/GameTime.cs`
- Create: `Assets/Tests/EditMode/GameTimeTests.cs`

> **TDD rationale:** `GameTime` contains the timing math that *must* be frame-rate-independent per spec §5.4. This is the single most critical piece of business logic in the PoC — we unit-test it in EditMode where no scene is required.

- [ ] **Step 6.1: Write failing tests**

Create `Assets/Tests/EditMode/GameTimeTests.cs`:

```csharp
using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests
{
    public class GameTimeTests
    {
        [Test]
        public void GetSongTimeMs_AtStartDspTime_ReturnsZero()
        {
            double songStartDsp = 100.5;
            double nowDsp = 100.5;
            double calibOffsetSec = 0.0;

            int result = GameTime.GetSongTimeMs(nowDsp, songStartDsp, calibOffsetSec);

            Assert.AreEqual(0, result);
        }

        [Test]
        public void GetSongTimeMs_OneSecondAfterStart_Returns1000()
        {
            int result = GameTime.GetSongTimeMs(nowDsp: 101.5, songStartDsp: 100.5, calibOffsetSec: 0.0);
            Assert.AreEqual(1000, result);
        }

        [Test]
        public void GetSongTimeMs_AppliesCalibrationOffset()
        {
            // user tapped 50ms early on average → subtract 0.05s from song time
            int result = GameTime.GetSongTimeMs(nowDsp: 101.5, songStartDsp: 100.5, calibOffsetSec: 0.05);
            Assert.AreEqual(950, result);
        }

        [Test]
        public void GetNoteProgress_AtSpawnTime_ReturnsZero()
        {
            // note hits at t=2000ms, currently t=0ms, previewTime=2000ms → just spawned
            float progress = GameTime.GetNoteProgress(songTimeMs: 0, hitTimeMs: 2000, previewTimeMs: 2000);
            Assert.AreEqual(0f, progress, 0.001f);
        }

        [Test]
        public void GetNoteProgress_AtHitTime_ReturnsOne()
        {
            float progress = GameTime.GetNoteProgress(songTimeMs: 2000, hitTimeMs: 2000, previewTimeMs: 2000);
            Assert.AreEqual(1f, progress, 0.001f);
        }

        [Test]
        public void GetNoteProgress_HalfwayDown_ReturnsHalf()
        {
            float progress = GameTime.GetNoteProgress(songTimeMs: 1000, hitTimeMs: 2000, previewTimeMs: 2000);
            Assert.AreEqual(0.5f, progress, 0.001f);
        }

        [Test]
        public void PitchToX_MinPitch_ReturnsZero()
        {
            // MIDI 36 = C2, our lowest supported key
            float x = GameTime.PitchToX(pitch: 36);
            Assert.AreEqual(0f, x, 0.001f);
        }

        [Test]
        public void PitchToX_MaxPitch_ReturnsOne()
        {
            // MIDI 83 = B5, our highest
            float x = GameTime.PitchToX(pitch: 83);
            Assert.AreEqual(1f, x, 0.001f);
        }

        [Test]
        public void PitchToX_C4_ReturnsMidRange()
        {
            // MIDI 60 = C4, should be around 0.51
            float x = GameTime.PitchToX(pitch: 60);
            Assert.AreEqual((60 - 36) / 47f, x, 0.001f);
        }
    }
}
```

- [ ] **Step 6.2: Run tests — verify they fail**

`Window → General → Test Runner → EditMode → Run All`.

Expected: 9 tests fail with compile error `The type or namespace name 'GameTime' could not be found`.

- [ ] **Step 6.3: Implement `GameTime`**

Create `Assets/Scripts/Common/GameTime.cs`:

```csharp
namespace KeyFlow
{
    public static class GameTime
    {
        public const int MinPitch = 36;  // MIDI C2
        public const int MaxPitch = 83;  // MIDI B5
        private const int PitchRange = MaxPitch - MinPitch;

        public static int GetSongTimeMs(double nowDsp, double songStartDsp, double calibOffsetSec)
        {
            double sec = nowDsp - songStartDsp - calibOffsetSec;
            return (int)(sec * 1000.0);
        }

        public static float GetNoteProgress(int songTimeMs, int hitTimeMs, int previewTimeMs)
        {
            return 1f - (float)(hitTimeMs - songTimeMs) / previewTimeMs;
        }

        public static float PitchToX(int pitch)
        {
            return (pitch - MinPitch) / (float)PitchRange;
        }
    }
}
```

- [ ] **Step 6.4: Run tests — verify they pass**

`Test Runner → Run All`. Expected: 9/9 pass.

- [ ] **Step 6.5: Commit**

```bash
cd /c/dev/unity
git add Assets/Scripts/Common/ Assets/Tests/EditMode/GameTimeTests.cs
git commit -m "feat(gametime): add dspTime math with EditMode coverage"
```

---

## Task 7: `AudioSamplePool` + tests

**Files:**
- Create: `Assets/Scripts/Gameplay/AudioSamplePool.cs`
- Create: `Assets/Tests/EditMode/AudioSamplePoolTests.cs`

> **Why pool?** `PlayOneShot` on a single `AudioSource` is fine for rare SFX but a rhythm game taps 2-4 times per second. Pooling 16 `AudioSource` components and rotating through them gives polyphony + avoids allocations during Update.

- [ ] **Step 7.1: Write failing test**

Create `Assets/Tests/EditMode/AudioSamplePoolTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using KeyFlow;

namespace KeyFlow.Tests
{
    public class AudioSamplePoolTests
    {
        [Test]
        public void NextSource_CyclesThroughAllSources()
        {
            var go = new GameObject("pool");
            var pool = go.AddComponent<AudioSamplePool>();
            pool.InitializeForTest(channels: 4);

            var s1 = pool.NextSource();
            var s2 = pool.NextSource();
            var s3 = pool.NextSource();
            var s4 = pool.NextSource();
            var s5 = pool.NextSource();  // wraps to first

            Assert.AreNotSame(s1, s2);
            Assert.AreNotSame(s2, s3);
            Assert.AreNotSame(s3, s4);
            Assert.AreSame(s1, s5, "pool should cycle back to first source");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void InitializeForTest_CreatesRequestedNumberOfSources()
        {
            var go = new GameObject("pool");
            var pool = go.AddComponent<AudioSamplePool>();
            pool.InitializeForTest(channels: 16);

            Assert.AreEqual(16, pool.Count);

            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 7.2: Run tests — verify they fail**

`Test Runner → Run All`. Expected: 2 new failures (compile error — class missing).

- [ ] **Step 7.3: Implement `AudioSamplePool`**

Create `Assets/Scripts/Gameplay/AudioSamplePool.cs`:

```csharp
using UnityEngine;

namespace KeyFlow
{
    public class AudioSamplePool : MonoBehaviour
    {
        [SerializeField] private int channels = 16;
        [SerializeField] private AudioClip defaultClip;

        private AudioSource[] sources;
        private int nextIndex;

        public int Count => sources?.Length ?? 0;

        private void Awake()
        {
            if (sources == null) Initialize(channels);
        }

        public void Initialize(int channelCount)
        {
            sources = new AudioSource[channelCount];
            for (int i = 0; i < channelCount; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;  // 2D
                src.loop = false;
                src.priority = 64;
                sources[i] = src;
            }
        }

        public void InitializeForTest(int channels) => Initialize(channels);

        public AudioSource NextSource()
        {
            var src = sources[nextIndex];
            nextIndex = (nextIndex + 1) % sources.Length;
            return src;
        }

        public void PlayOneShot(AudioClip clip = null)
        {
            var src = NextSource();
            src.PlayOneShot(clip ?? defaultClip);
        }
    }
}
```

- [ ] **Step 7.4: Run tests — verify they pass**

`Test Runner → Run All`. Expected: 11/11 pass.

- [ ] **Step 7.5: Commit**

```bash
cd /c/dev/unity
git add Assets/Scripts/Gameplay/AudioSamplePool.cs Assets/Tests/EditMode/AudioSamplePoolTests.cs
git commit -m "feat(audio): add 16-channel AudioSource pool with cycling test"
```

---

## Task 8: `AudioSyncManager` — dspTime single source of truth

**Files:**
- Create: `Assets/Scripts/Gameplay/AudioSyncManager.cs`

> No EditMode tests for this one — it depends on `AudioSettings.dspTime` which only runs in PlayMode. We'll validate empirically via `LatencyMeter` HUD in Task 12.

- [ ] **Step 8.1: Implement `AudioSyncManager`**

Create `Assets/Scripts/Gameplay/AudioSyncManager.cs`:

```csharp
using UnityEngine;

namespace KeyFlow
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioSyncManager : MonoBehaviour
    {
        [SerializeField] private double scheduleLeadSec = 0.5;

        private AudioSource bgmSource;
        private double songStartDsp;
        private bool started;

        public double SongStartDspTime => songStartDsp;
        public bool IsPlaying => started;

        // Calibration offset in seconds. Positive = audio perceived late → subtract from songTime.
        public double CalibrationOffsetSec { get; set; } = 0.0;

        public int SongTimeMs =>
            started ? GameTime.GetSongTimeMs(AudioSettings.dspTime, songStartDsp, CalibrationOffsetSec) : 0;

        private void Awake()
        {
            bgmSource = GetComponent<AudioSource>();
            bgmSource.playOnAwake = false;
        }

        // W1 PoC: start a "silent" song — no BGM clip needed, just a synchronized clock.
        // Later plans will attach the song OGG here.
        public void StartSilentSong()
        {
            songStartDsp = AudioSettings.dspTime + scheduleLeadSec;
            started = true;
        }

        // For Plan 2+, when we have real BGM
        public void StartSong(AudioClip bgm)
        {
            bgmSource.clip = bgm;
            songStartDsp = AudioSettings.dspTime + scheduleLeadSec;
            bgmSource.PlayScheduled(songStartDsp);
            started = true;
        }
    }
}
```

- [ ] **Step 8.2: Compile check**

Return to Unity Editor, wait for script compilation (bottom-right spinner). Console should show zero errors.

- [ ] **Step 8.3: Commit**

```bash
cd /c/dev/unity
git add Assets/Scripts/Gameplay/AudioSyncManager.cs
git commit -m "feat(audio): add AudioSyncManager dspTime single-source-of-truth"
```

---

## Task 9: `NoteController` + `NoteSpawner` + Note prefab

**Files:**
- Create: `Assets/Scripts/Gameplay/NoteController.cs`
- Create: `Assets/Scripts/Gameplay/NoteSpawner.cs`
- Create: `Assets/Prefabs/Note.prefab` (via Unity Editor)

- [ ] **Step 9.1: Implement `NoteController`**

Create `Assets/Scripts/Gameplay/NoteController.cs`:

```csharp
using UnityEngine;

namespace KeyFlow
{
    public class NoteController : MonoBehaviour
    {
        [SerializeField] private int previewTimeMs = 2000;

        private AudioSyncManager audioSync;
        private Vector3 spawnPos;
        private Vector3 judgmentPos;
        private int hitTimeMs;
        private bool initialized;

        public int HitTimeMs => hitTimeMs;

        public void Initialize(AudioSyncManager sync, Vector3 spawn, Vector3 judgment, int hitMs, int previewMs = 2000)
        {
            audioSync = sync;
            spawnPos = spawn;
            judgmentPos = judgment;
            hitTimeMs = hitMs;
            previewTimeMs = previewMs;
            transform.position = spawn;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || !audioSync.IsPlaying) return;

            float progress = GameTime.GetNoteProgress(audioSync.SongTimeMs, hitTimeMs, previewTimeMs);
            if (progress < 0f) return;  // not yet visible

            transform.position = Vector3.LerpUnclamped(spawnPos, judgmentPos, progress);

            // Despawn 500ms past hit time
            if (audioSync.SongTimeMs > hitTimeMs + 500)
            {
                Destroy(gameObject);
            }
        }
    }
}
```

- [ ] **Step 9.2: Implement `NoteSpawner`**

Create `Assets/Scripts/Gameplay/NoteSpawner.cs`:

```csharp
using UnityEngine;

namespace KeyFlow
{
    public class NoteSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject notePrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform judgmentPoint;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private int firstNoteHitMs = 2000;   // first note hits at 2s
        [SerializeField] private int noteIntervalMs = 1000;   // then every 1s
        [SerializeField] private int totalNotes = 10;

        private int spawnedCount;

        private void Start()
        {
            audioSync.StartSilentSong();
        }

        private void Update()
        {
            if (!audioSync.IsPlaying) return;
            if (spawnedCount >= totalNotes) return;

            int nextHitMs = firstNoteHitMs + spawnedCount * noteIntervalMs;
            int previewMs = 2000;
            // Spawn when it's time for the note to appear at the top
            if (audioSync.SongTimeMs >= nextHitMs - previewMs)
            {
                SpawnNote(nextHitMs);
                spawnedCount++;
            }
        }

        private void SpawnNote(int hitTimeMs)
        {
            var go = Instantiate(notePrefab, spawnPoint.position, Quaternion.identity);
            var ctrl = go.GetComponent<NoteController>();
            ctrl.Initialize(audioSync, spawnPoint.position, judgmentPoint.position, hitTimeMs);
        }
    }
}
```

- [ ] **Step 9.3: Create Note prefab in Unity**

In Unity:
1. Hierarchy → right-click → *Create Empty* → name it `Note`
2. Add Component → `Sprite Renderer`. Drag `Assets/Sprites/note_circle.png` into the `Sprite` field. Set Order in Layer = 1.
3. Add Component → `Note Controller` (our script)
4. Transform Scale = (0.5, 0.5, 1)
5. Drag `Note` from Hierarchy into `Assets/Prefabs/` folder (creates Prefab)
6. Delete the Note from Hierarchy (prefab is saved)

- [ ] **Step 9.4: Commit**

```bash
cd /c/dev/unity
git add Assets/Scripts/Gameplay/NoteController.cs Assets/Scripts/Gameplay/NoteSpawner.cs Assets/Prefabs/
git commit -m "feat(gameplay): add NoteController + NoteSpawner + Note prefab"
```

---

## Task 10: `TapInputHandler` wired to `AudioSamplePool`

**Files:**
- Create: `Assets/Scripts/Gameplay/TapInputHandler.cs`

- [ ] **Step 10.1: Implement `TapInputHandler`**

Create `Assets/Scripts/Gameplay/TapInputHandler.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace KeyFlow
{
    public class TapInputHandler : MonoBehaviour
    {
        [SerializeField] private AudioSamplePool samplePool;
        [SerializeField] private AudioSyncManager audioSync;

        public System.Action<int /*tapTimeMs*/> OnTap;

        private void Update()
        {
            bool tapped = false;

            // Touchscreen
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.wasPressedThisFrame) { tapped = true; break; }
                }
            }

            // Mouse (for editor playtest)
            if (!tapped && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                tapped = true;
            }

            if (tapped)
            {
                samplePool.PlayOneShot();
                OnTap?.Invoke(audioSync.SongTimeMs);
            }
        }
    }
}
```

- [ ] **Step 10.2: Commit**

```bash
cd /c/dev/unity
git add Assets/Scripts/Gameplay/TapInputHandler.cs
git commit -m "feat(input): add TapInputHandler firing on touch/mouse"
```

---

## Task 11: `LatencyMeter` HUD

**Files:**
- Create: `Assets/Scripts/UI/LatencyMeter.cs`

> This component is the **primary empirical instrument** for the go/no-go decision. It measures three things simultaneously:
> 1. **FPS** (rolling 1s average) — target ≥ 58
> 2. **Frame latency (tap→next dspTime read)** — only the one-frame Unity scheduling cost. **This is NOT the real tap-to-audio latency** — the OS audio pipeline latency is invisible from managed code. Real tap-to-audio is measured externally in Task 14. Expect this HUD value to show ~16ms regardless of device audio quality.
> 3. **dspTime drift** — compare `dspTime - songStart` with `Time.time - songStart` over a long run; drift should stay < 10ms over 30s

- [ ] **Step 11.1: Implement `LatencyMeter`**

Create `Assets/Scripts/UI/LatencyMeter.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    public class LatencyMeter : MonoBehaviour
    {
        [SerializeField] private Text hudText;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private TapInputHandler tapInput;
        [SerializeField] private AudioSamplePool samplePool;

        private float fpsAccum;
        private int fpsFrames;
        private float fpsDisplay;
        private float fpsTimer;

        private double lastTapDsp;
        private float lastFrameLatencyMs = -1;

        private float timeAtStart;
        private double dspAtStart;

        private void Start()
        {
            Application.targetFrameRate = 60;
            tapInput.OnTap += OnTap;
            timeAtStart = Time.time;
            dspAtStart = AudioSettings.dspTime;
        }

        private void OnDestroy()
        {
            if (tapInput != null) tapInput.OnTap -= OnTap;
        }

        private void OnTap(int tapMs)
        {
            lastTapDsp = AudioSettings.dspTime;
            Invoke(nameof(MeasureFrameLatency), 0f);  // defer 1 frame
        }

        private void MeasureFrameLatency()
        {
            // Only measures Unity frame scheduling — NOT real tap-to-audio.
            // Real tap-to-audio = measured externally in Task 14.
            lastFrameLatencyMs = (float)((AudioSettings.dspTime - lastTapDsp) * 1000);
        }

        private void Update()
        {
            fpsAccum += Time.unscaledDeltaTime;
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                fpsDisplay = fpsFrames / fpsAccum;
                fpsAccum = 0;
                fpsFrames = 0;
                fpsTimer = 0;
            }

            double dspElapsed = AudioSettings.dspTime - dspAtStart;
            double frameElapsed = Time.time - timeAtStart;
            double driftMs = (dspElapsed - frameElapsed) * 1000.0;

            hudText.text =
                $"FPS: {fpsDisplay:F1}\n" +
                $"Frame latency: {(lastFrameLatencyMs < 0 ? "--" : lastFrameLatencyMs.ToString("F1"))} ms (not tap→audio)\n" +
                $"dspTime drift: {driftMs:F1} ms\n" +
                $"Song time: {audioSync.SongTimeMs} ms\n" +
                $"Buffer: {AudioSettings.GetConfiguration().dspBufferSize} samples";
        }
    }
}
```

> **Measurement caveat:** `lastFrameLatencyMs` inside Unity will always show ~16ms because we only measure one frame of scheduling delay, not the real OS audio pipeline. The *real* ground truth is an **external audio recording** (see Task 14) — record phone speaker + tap sound with a second device's mic at 48kHz, measure sample offset in Audacity. The HUD is for detecting frame-rate/drift problems, not final latency numbers.

- [ ] **Step 11.2: Commit**

```bash
cd /c/dev/unity
git add Assets/Scripts/UI/LatencyMeter.cs
git commit -m "feat(hud): add LatencyMeter HUD for FPS + tap latency + drift"
```

---

## Task 12: Assemble `GameplayScene`

**Files:**
- Modify: `Assets/Scenes/GameplayScene.unity` (created from scratch in Unity Editor)

All operations happen in the Unity Editor.

- [ ] **Step 12.1: Create the scene**

1. `File → New Scene → 2D → Create`
2. `File → Save As → Assets/Scenes/GameplayScene.unity`
3. Delete `Assets/Scenes/SampleScene.unity` (default one) — `rm /c/dev/unity/Assets/Scenes/SampleScene.unity*`

- [ ] **Step 12.2: Set up camera**

Main Camera → Inspector:
- Position: (0, 0, -10)
- Projection: Orthographic
- Size: 5
- Background: dark gray (`#1A1A1A`)

- [ ] **Step 12.3: Create judgment line GameObject**

Hierarchy → Create → 2D Object → Sprites → Square → name it `JudgmentLine`:
- Drag `Sprites/judgment_line.png` into SpriteRenderer's Sprite
- Position: (0, -3, 0)
- Scale: (10, 0.2, 1)
- Sprite color: cyan

- [ ] **Step 12.4: Create spawn point (empty)**

Hierarchy → Create Empty → name it `SpawnPoint`, position (0, 4, 0).

- [ ] **Step 12.5: Create Managers parent**

Hierarchy → Create Empty → `Managers`. Add empty children:
- `AudioSync` — add components: `AudioSource`, `AudioSyncManager`
- `SamplePool` — add components: `AudioSamplePool`. Drag `Audio/piano_c4.ogg` into `Default Clip`. Keep Channels = 16.
- `TapInput` — add component: `TapInputHandler`. Wire `samplePool` → `SamplePool`, `audioSync` → `AudioSync`.
- `Spawner` — add component: `NoteSpawner`. Wire `notePrefab` → `Prefabs/Note`, `spawnPoint` → `SpawnPoint`, `judgmentPoint` → `JudgmentLine`, `audioSync` → `AudioSync`.

- [ ] **Step 12.6: Create HUD Canvas (legacy UI Text)**

Hierarchy → Create → UI → Canvas:
- Canvas → Render Mode: Screen Space - Overlay
- Canvas Scaler: Scale With Screen Size, Reference Resolution 1280×720, Match 0.5
- Right-click Canvas → Create → UI → **Legacy → Text** (NOT TextMeshPro — plan uses `UnityEngine.UI.Text` for simplicity, avoids TMP Essentials import prompt)
- Name the Text child `HUDText`. RectTransform: anchor top-left, position (20, -20, 0), size (500, 160). Font size 22, color white, horizontal overflow = Overflow
- Canvas → Add Component → `LatencyMeter`. Wire fields: `hudText` → `HUDText`, `audioSync` → `Managers/AudioSync`, `tapInput` → `Managers/TapInput`, `samplePool` → `Managers/SamplePool`

- [ ] **Step 12.7: Add GameplayScene to Build Settings**

`File → Build Profiles → Scene List → Add Open Scene`. Ensure `Assets/Scenes/GameplayScene` is index 0 and checked.

- [ ] **Step 12.8: Play in Editor — validate**

Press Play. Expected:
- Dark gray background, cyan line at bottom
- After ~2.5s, white circle appears at top and falls smoothly to the line
- Clicking anywhere plays piano C4 sample (you hear it through your speakers)
- HUD shows FPS ≥ 58, Song time incrementing, buffer size = 256
- Over 30 seconds, notes keep spawning at 1s intervals; dspTime drift stays < 10ms

If any of these fail → debug before proceeding. Check Console for errors; common issues:
- SpriteRenderer field empty on Note prefab → nothing visible
- AudioSource missing `DefaultClip` on pool → silent taps
- Build Settings missing scene → app crashes on device

- [ ] **Step 12.9: Commit**

```bash
cd /c/dev/unity
git add Assets/Scenes/ ProjectSettings/EditorBuildSettings.asset
git rm Assets/Scenes/SampleScene.unity Assets/Scenes/SampleScene.unity.meta 2>/dev/null || true
git commit -m "feat(scene): assemble GameplayScene with note spawner + HUD"
```

---

## Task 13: Build debug APK + install to device

**Files:**
- Create: `C:\dev\unity-music\Builds\KeyFlow-W1-debug.apk` (gitignored)

- [ ] **Step 13.1: Create debug keystore**

In Unity: `Edit → Project Settings → Player → Android → Publishing Settings`:
- Keystore Manager → *Keystore... → Create New → Anywhere*
- Path: `C:\dev\unity-music\debug.keystore` (gitignored)
- Password: any (remember it, just for PoC)
- Alias: `keyflow-debug`, validity 25 years, your name for CN

Check *Custom Keystore*, select the new keystore.

> **Safety:** This debug keystore is for W1 testing only. Production builds use a separate keystore stored in a password manager, NOT in the repo.

- [ ] **Step 13.2: Verify device connection**

In a terminal:

```bash
adb devices
```

Expected: your device listed with status `device`. If `unauthorized`, accept the USB debugging prompt on the phone.

- [ ] **Step 13.3: Build APK**

Unity: `File → Build Profiles → Android → Build`. Target path: `C:\dev\unity-music\Builds\KeyFlow-W1-debug.apk`.

First Android build takes 10-20 minutes (Gradle downloads, IL2CPP compile). Subsequent ones are ~2 minutes.

- [ ] **Step 13.4: Install APK**

```bash
adb install -r /c/dev/unity/Builds/KeyFlow-W1-debug.apk
```

Expected: `Success`.

- [ ] **Step 13.5: Launch app**

On the device, tap KeyFlow icon. App should launch in landscape, show the cyan judgment line, and spawn notes.

- [ ] **Step 13.6: Commit (gitignore check)**

```bash
cd /c/dev/unity
git status   # Builds/ and debug.keystore must NOT appear (gitignored)
```

No commit needed — nothing tracked changed. If `Builds/` shows up, fix `.gitignore` and try again.

---

## Task 14: Measure latency empirically + record results

**Files:**
- Create: `docs/superpowers/reports/2026-04-19-w1-poc-measurements.md`

- [ ] **Step 14.1: Prep external measurement rig**

You need a **second recording device** (laptop mic, second phone, etc.) and Audacity. Place both close together. Open Audacity on the recording device, sample rate 48kHz, start recording.

- [ ] **Step 14.2: Measure tap-to-audio latency (10 trials)**

On the test phone:
1. Launch KeyFlow
2. Tap firmly on screen near the recording device mic (so tap *sound* + resulting audio are both captured)
3. Repeat 10 times with ~1s between taps
4. Stop Audacity recording

- [ ] **Step 14.3: Analyze in Audacity**

For each tap:
1. Zoom to the tap impulse (sharp spike) and the following piano attack (softer onset with harmonics)
2. Measure sample delta between impulse peak and piano onset
3. Divide by 48000, multiply by 1000 → latency in ms
4. Record all 10 values

Take median.

- [ ] **Step 14.4: Measure on 3 devices if available**

Repeat Task 14.1-14.3 on high/mid/low-spec Android devices per spec §12 criterion #1.

- [ ] **Step 14.5: Measure FPS stability**

While the HUD is visible, run a 30-second tapping session on each device. Note minimum FPS and whether any frame drops occurred visually.

- [ ] **Step 14.6: Write measurement report**

Create `docs/superpowers/reports/` and write `2026-04-19-w1-poc-measurements.md`:

```markdown
# W1 PoC Measurement Report — 2026-04-19

## Test Setup
- Unity 6000.0.x, IL2CPP arm64-v8a, DSP Buffer 256 (Best Latency)
- Audio path: Unity AudioSource pool (16 channels), PlayOneShot
- Sample: piano_c4.ogg, 128kbps Vorbis, DecompressOnLoad + Preload

## Devices Tested
| # | Device | Android | Audio driver latency (typical) |
|---|---|---|---|
| 1 | [fill in] | [fill in] | - |
| 2 | [fill in] | [fill in] | - |
| 3 | [fill in] | [fill in] | - |

## Tap-to-Audio Latency (ms, median of 10)
| Device | Median | Min | Max |
|---|---|---|---|
| 1 | ? | ? | ? |
| 2 | ? | ? | ? |
| 3 | ? | ? | ? |

## FPS Stability (30s tapping session)
| Device | Avg FPS | Min FPS | Drops visible? |
|---|---|---|---|
| 1 | ? | ? | ? |

## dspTime Drift (HUD readout at 30s mark)
| Device | Drift (ms) |
|---|---|

## Go/No-Go Decision

Per spec §0 risk #1 and §9 W1:

- **< 80ms median on all 3 devices:** ✅ GO. Proceed with AudioSource pool (no Native Audio needed).
- **80-120ms median:** ⚠️ MARGINAL. Purchase Native Audio plugin ($35), swap `AudioSamplePool.PlayOneShot` implementation, re-measure.
- **> 120ms median OR any device hits > 150ms:** ❌ NO-GO for Unity path. Pivot to Native Kotlin + Oboe (+4 weeks per spec §9).

**Decision:** _(fill in after measurement)_

**Rationale:** _(brief notes on what you observed)_

**Next step:** _(proceed to Plan 2 / invoke Native Audio fallback / pivot)_
```

- [ ] **Step 14.7: Commit report**

```bash
cd /c/dev/unity
git add docs/superpowers/reports/
git commit -m "docs(w1): record PoC latency + FPS measurements and go/no-go decision"
```

---

## W1 Completion Criteria

All of the following must be true:

- [ ] All EditMode tests green (11/11)
- [ ] APK installs and launches on ≥ 1 real Android device (ideally 3)
- [ ] Notes fall smoothly from top to judgment line for ≥ 30 seconds without frame drops visible to the eye
- [ ] Tap plays piano C4 sample with no audible stutter
- [ ] HUD readable in landscape, FPS ≥ 58 on mid-spec device
- [ ] dspTime drift < 10ms over 30s
- [ ] Measurement report filled in with actual numbers
- [ ] Go/No-Go decision recorded

Once all boxes checked → brief the user with the go/no-go decision. If **GO**, invoke writing-plans skill again for Plan 2 (W2-W3 gameplay core). If **MARGINAL** or **NO-GO**, pause and reassess with the user before writing more plans.

---

## Out of Scope for W1 (explicit)

These belong to later plans; **do not add them to this plan even if tempting**:

- Multiple piano samples (all 48 keys) — Plan 2
- Chart JSON loading — Plan 2
- Perfect/Great/Good/Miss judgment — Plan 2
- Score/combo/stars — Plan 2
- Main menu / song list UI — Plan 3
- Calibration UX — Plan 4
- Haptics, particles, polish — Plan 5
- Play Store release / signed release keystore — Plan 5
- Native Audio plugin integration — only if W1 measurement triggers it
