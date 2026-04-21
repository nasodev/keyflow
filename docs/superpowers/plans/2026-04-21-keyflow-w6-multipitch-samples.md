# KeyFlow W6 (Sub-Project 1/2): Multi-Pitch Piano Samples Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single-clip `piano_c4.wav` tap sound with 17 Salamander Grand Piano V3 samples covering MIDI 36–83 via runtime ±1-semitone pitch-shifting. At tap time, play the nearest pending note's pitch if within a 500ms window; otherwise play a lane-default pitch.

**Architecture:** 17 source WAVs are imported with shared AudioImporter settings via `AssetPostprocessor` (Vorbis Q60, mono, 48kHz, Decompress On Load). `AudioSamplePool.ResolveSample` is a pure static function mapping a MIDI pitch to (clip, pitch ratio). `ChartNote.pitch` (already parsed) is threaded through `NoteController`. `TapInputHandler` asks `JudgmentSystem.GetClosestPendingPitch` at tap time; falls back to `LanePitches.Default(lane)` when no pending note within the window. CC-BY 3.0 attribution is bundled in `StreamingAssets/licenses/` and surfaced on the Settings screen.

**Tech Stack:** Unity 6 LTS (6000.3.13f1), C#, NUnit EditMode tests. No new third-party dependencies.

**Spec:** [2026-04-21-keyflow-w6-multipitch-samples-design.md](../specs/2026-04-21-keyflow-w6-multipitch-samples-design.md)

**Scope excluded from this plan:** W6 priorities 2–6 (4-song content, profiler pass, calibration click sample, UI polish, second device). Each gets its own spec/plan cycle.

---

## Conventions

**Unity test command (EditMode)** — **foreground only (batchmode silently exits under pipes/background)**, **omit `-quit`** (with `-runTests`, `-quit` silently skips the test runner):

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "C:/dev/unity-music/.claude/worktrees/ecstatic-franklin-a7436a" \
  -runTests -testPlatform EditMode \
  -testResults "C:/dev/unity-music/.claude/worktrees/ecstatic-franklin-a7436a/test-results.xml" \
  -logFile -
```

- First run takes ~5–10 min (asset import for the worktree). Subsequent runs ~1 s after import settles.
- Results XML parse: look for `<test-run total="N" passed="M" failed="F">` at the top. Or `grep -E 'total=|failed=' test-results.xml`.
- **Do not pass `-quit`.** Do not run this in background. Plain foreground invocation only.

**Commit style:** follow W5 precedent — scoped subject (`feat(w6)`, `test(w6)`, `chore(w6)`, `docs(w6)`), short body explaining the why.

---

## File Map

### Create

- `Assets/StreamingAssets/licenses/salamander-piano-v3.txt` — CC-BY 3.0 attribution (README copy)
- `Assets/StreamingAssets/licenses/.gitkeep` — only if folder empty otherwise (handled by the license file itself)
- `Assets/Audio/piano/C2v10.wav` … `C6v10.wav` — 17 samples (renamed `D#` → `Ds`, `F#` → `Fs`)
- `Assets/Editor/PianoSampleImportPostprocessor.cs` — `AssetPostprocessor` applying import settings
- `Assets/Scripts/Gameplay/LanePitches.cs` — static class with lane→default-pitch map
- `Assets/Tests/EditMode/LanePitchesTests.cs` — NUnit tests
- `Assets/Tests/EditMode/JudgmentSystemTests.cs` — NUnit tests for the new query
- `Assets/Editor/W6SamplesWireup.cs` — one-off editor script to wire scene references

### Modify

- `Assets/Scripts/Charts/ChartNote.cs` — no changes (pitch already exists). Kept as reference.
- `Assets/Scripts/Gameplay/NoteController.cs` — add `int pitch` + `Pitch` property; extend `Initialize` signature
- `Assets/Scripts/Gameplay/NoteSpawner.cs` — pass `n.pitch` to `Initialize`
- `Assets/Scripts/Gameplay/AudioSamplePool.cs` — add `pitchSamples`, `baseMidi`, `stepSemitones`, `ResolveSample` (static), `PlayForPitch`
- `Assets/Scripts/Gameplay/JudgmentSystem.cs` — add `GetClosestPendingPitch(lane, tapTimeMs, windowMs)`
- `Assets/Scripts/Gameplay/TapInputHandler.cs` — add `judgmentSystem` ref + `pitchLookupWindowMs`; replace `PlayOneShot` calls with pitch-aware path
- `Assets/Scripts/UI/UIStrings.cs` — add `CreditsSamples`
- `Assets/Scripts/UI/SettingsScreen.cs` — add `creditsLabel` field + bind in `Awake`
- `Assets/Tests/EditMode/AudioSamplePoolTests.cs` — add `ResolveSample_*` cases
- `docs/superpowers/specs/2026-04-19-keyflow-mvp-design.md` — correct §11.2 licenseInfo string

### Build artifact (not committed, gitignored)

- `Builds/keyflow-w6.apk` (device validation)

---

## Task 1: Bundle Salamander license text + correct parent spec

**Files:**
- Create: `Assets/StreamingAssets/licenses/salamander-piano-v3.txt`
- Modify: `docs/superpowers/specs/2026-04-19-keyflow-mvp-design.md` §11.2

Pure asset/docs task. No code, no test. Lands first so later tasks referencing attribution never drift from the legal posture.

- [ ] **Step 1: Copy the Salamander README into StreamingAssets**

```bash
mkdir -p "C:/dev/unity-music/.claude/worktrees/ecstatic-franklin-a7436a/Assets/StreamingAssets/licenses"
cp "C:/dev/music/piano-v3/README" \
   "C:/dev/unity-music/.claude/worktrees/ecstatic-franklin-a7436a/Assets/StreamingAssets/licenses/salamander-piano-v3.txt"
```

Expected: new file ~1.9 KB.

- [ ] **Step 2: Correct parent spec §11.2**

Use Edit tool on `docs/superpowers/specs/2026-04-19-keyflow-mvp-design.md`.

Old string (exact — L288 in the current file):
```
MVP 수록 5곡 전원 **작곡 저작권 PD 확정**(Beethoven 1827 사망, Debussy 1918, Joplin 1917, Pachelbel 1706, 모두 사후 70년 경과). 녹음은 **자체 MIDI 시퀀싱 + Salamander CC0 샘플러**로 렌더링하여 연주 저작권도 해결. 인터넷 무료 MIDI 파일 사용 금지(재배포 라이선스 불명확). 각 곡 메타데이터에 `"licenseInfo": "PD-composition; self-sequenced; CC0-samples(Salamander)"` 기록.
```

New string:
```
MVP 수록 5곡 전원 **작곡 저작권 PD 확정**(Beethoven 1827 사망, Debussy 1918, Joplin 1917, Pachelbel 1706, 모두 사후 70년 경과). 녹음은 **자체 MIDI 시퀀싱 + Salamander CC-BY 샘플러**로 렌더링하여 연주 저작권도 해결 (CC-BY 3.0, 저자: Alexander Holm — Assets/StreamingAssets/licenses/salamander-piano-v3.txt 및 Settings Credits에 귀속 표기). 인터넷 무료 MIDI 파일 사용 금지(재배포 라이선스 불명확). 각 곡 메타데이터에 `"licenseInfo": "PD-composition; self-sequenced; CC-BY-samples(Salamander V3, Alexander Holm)"` 기록.
```

- [ ] **Step 3: Commit**

```bash
cd "C:/dev/unity-music/.claude/worktrees/ecstatic-franklin-a7436a"
git add Assets/StreamingAssets/licenses/salamander-piano-v3.txt \
        docs/superpowers/specs/2026-04-19-keyflow-mvp-design.md
git commit -m "chore(w6): bundle Salamander CC-BY license + correct parent spec §11.2

Salamander Grand Piano V3 README ships with the APK via
StreamingAssets so the attribution file is always present on device.
Parent MVP spec §11.2 previously claimed CC0 samples — the README
is CC-BY 3.0 (Alexander Holm). Updated licenseInfo example and the
prose paragraph to match.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Add Credits line to UIStrings + SettingsScreen code path

**Files:**
- Modify: `Assets/Scripts/UI/UIStrings.cs`
- Modify: `Assets/Scripts/UI/SettingsScreen.cs`

Skip EditMode test — pure serialized-field binding, no logic worth asserting at the unit level. Device validation (Task 10) confirms the label renders.

- [ ] **Step 1: Add the Credits string**

Edit `Assets/Scripts/UI/UIStrings.cs`. Add **after** the `Results` block, before the closing brace of the `UIStrings` class:

```csharp
        // Credits
        public const string CreditsSamples = "Piano samples: Salamander Grand Piano V3 by Alexander Holm, CC-BY 3.0";
```

- [ ] **Step 2: Add the SerializeField + binding in SettingsScreen**

Edit `Assets/Scripts/UI/SettingsScreen.cs`.

In the serialized-field block (after `versionLabel`, before `calibration`), add:

```csharp
        [SerializeField] private Text creditsLabel;
```

At the end of `Awake()` (after the `versionLabel` assignment), add:

```csharp
            if (creditsLabel != null)
                creditsLabel.text = UIStrings.CreditsSamples;
```

- [ ] **Step 3: Verify compile (no tests yet — pure UI binding)**

Run the Unity EditMode command from **Conventions**. Expected: 93 passed, 0 failed (baseline unchanged — no new test yet).

Reading the result XML:
```bash
grep -E 'total=|failed=' "C:/dev/unity-music/.claude/worktrees/ecstatic-franklin-a7436a/test-results.xml" | head -5
```
Expected: `total="93"`, `failed="0"`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/UIStrings.cs Assets/Scripts/UI/SettingsScreen.cs
git commit -m "feat(w6): Settings Credits label for Salamander CC-BY attribution

Adds UIStrings.CreditsSamples and a [SerializeField] Text creditsLabel
that SettingsScreen populates in Awake. Wire-up of the Text GameObject
in the scene happens in a later task (editor script).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Import 17 Salamander WAV samples + AudioImporter settings

**Files:**
- Create: `Assets/Audio/piano/C2v10.wav` … `C6v10.wav` (17 files, `#` → `s` rename)
- Create: `Assets/Editor/PianoSampleImportPostprocessor.cs`

Unity generates `.meta` files on first import; the postprocessor ensures consistent settings across all 17 samples. Meta files are committed (standard Unity practice) after Unity has settled on them.

- [ ] **Step 1: Create the `Assets/Audio/piano/` folder**

```bash
mkdir -p "C:/dev/unity-music/.claude/worktrees/ecstatic-franklin-a7436a/Assets/Audio/piano"
```

- [ ] **Step 2: Copy + rename 17 source WAVs**

```bash
SRC="C:/dev/music/piano-v3/44.1khz16bit"
DST="C:/dev/unity-music/.claude/worktrees/ecstatic-franklin-a7436a/Assets/Audio/piano"

# Natural-name samples (no # in filename)
for n in C2 A2 C3 A3 C4 A4 C5 A5 C6; do
  cp "$SRC/${n}v10.wav" "$DST/${n}v10.wav"
done

# D# → Ds rename
for n in Ds2 Ds3 Ds4 Ds5; do
  src_name="${n/Ds/D#}v10.wav"
  cp "$SRC/$src_name" "$DST/${n}v10.wav"
done

# F# → Fs rename
for n in Fs2 Fs3 Fs4 Fs5; do
  src_name="${n/Fs/F#}v10.wav"
  cp "$SRC/$src_name" "$DST/${n}v10.wav"
done

ls "$DST" | wc -l  # expected: 17
```

Expected: `17` files in `Assets/Audio/piano/`.

- [ ] **Step 3: Create the AssetPostprocessor for consistent import settings**

Create `Assets/Editor/PianoSampleImportPostprocessor.cs`:

```csharp
using UnityEditor;

namespace KeyFlow.Editor
{
    // Applies shared AudioImporter settings to every WAV under Assets/Audio/piano/.
    // Must run as an AssetPostprocessor so settings stick through re-imports
    // (including fresh checkouts of the worktree).
    public class PianoSampleImportPostprocessor : AssetPostprocessor
    {
        private const string PianoFolder = "Assets/Audio/piano/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(PianoFolder)) return;

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.preloadAudioData = true;

            var defaults = importer.defaultSampleSettings;
            defaults.loadType = AudioClipLoadType.DecompressOnLoad;
            defaults.compressionFormat = AudioCompressionFormat.Vorbis;
            defaults.quality = 0.60f;
            defaults.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            defaults.sampleRateOverride = 48000;
            importer.defaultSampleSettings = defaults;
        }
    }
}
```

- [ ] **Step 4: Force Unity to re-import the folder so settings apply**

Run the EditMode test command (from **Conventions**) once — first invocation re-imports the new folder with the post-processor active. Unity writes `.meta` files to each `.wav`.

Expected: 93 passed, 0 failed. Re-import may extend this run to ~5 min on first pass.

- [ ] **Step 5: Verify the settings stuck**

```bash
grep -E 'loadType:|compressionFormat:|forceToMono:|sampleRateOverride:' \
  "C:/dev/unity-music/.claude/worktrees/ecstatic-franklin-a7436a/Assets/Audio/piano/C4v10.wav.meta"
```

Expected lines include: `forceToMono: 1`, `loadType: 0` (DecompressOnLoad), `compressionFormat: 1` (Vorbis), `sampleRateOverride: 48000`.

- [ ] **Step 6: Commit samples + post-processor + meta**

```bash
git add Assets/Audio/piano/ Assets/Editor/PianoSampleImportPostprocessor.cs
git commit -m "feat(w6): import 17 Salamander v10 piano samples

17 samples from Salamander Grand Piano V3 (minor-thirds, MIDI 36-84)
as WAV sources. PianoSampleImportPostprocessor applies consistent
AudioImporter settings (Force Mono, Vorbis Q60, Decompress On Load,
Override Sample Rate 48000) so re-imports stay reproducible.

D# → Ds and F# → Fs in filenames for Unity asset-path safety.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Thread `ChartNote.pitch` through `NoteController` (TDD)

**Files:**
- Create: `Assets/Tests/EditMode/NoteControllerTests.cs`
- Modify: `Assets/Scripts/Gameplay/NoteController.cs`
- Modify: `Assets/Scripts/Gameplay/NoteSpawner.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/NoteControllerTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.Charts;

namespace KeyFlow.Tests.EditMode
{
    public class NoteControllerTests
    {
        [Test]
        public void Initialize_PersistsPitch()
        {
            var go = new GameObject("note");
            var ctrl = go.AddComponent<NoteController>();

            ctrl.Initialize(
                sync: null,
                lane: 2,
                laneX: 0f,
                hitMs: 1000,
                pitch: 64,
                type: NoteType.TAP,
                durMs: 0,
                spawnY: 5f,
                judgmentY: -3f,
                previewMs: 2000,
                missGraceMs: 60,
                onAutoMiss: null);

            Assert.AreEqual(64, ctrl.Pitch);
            Assert.AreEqual(2, ctrl.Lane);
            Assert.AreEqual(1000, ctrl.HitTimeMs);

            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run the Unity EditMode command from **Conventions**.

Expected: compilation error — `Initialize` signature lacks `pitch`; `Pitch` property does not exist on `NoteController`. `test-results.xml` will not regenerate or will show build errors.

- [ ] **Step 3: Add `Pitch` field + property to `NoteController`**

Edit `Assets/Scripts/Gameplay/NoteController.cs`.

In the field block (after `private int hitTimeMs;`), add:

```csharp
        private int pitch;
```

In the public getter block (after `public int HitTimeMs => hitTimeMs;`), add:

```csharp
        public int Pitch => pitch;
```

Modify the `Initialize` method signature — insert `int pitch` between `int hitMs,` and `NoteType type,`:

```csharp
        public void Initialize(
            AudioSyncManager sync,
            int lane, float laneX,
            int hitMs,
            int pitch,
            NoteType type,
            int durMs,
            float spawnY, float judgmentY,
            int previewMs,
            int missGraceMs,
            System.Action<NoteController> onAutoMiss)
        {
            this.audioSync = sync;
            this.lane = lane;
            this.laneX = laneX;
            this.hitTimeMs = hitMs;
            this.pitch = pitch;
            this.noteType = type;
            // ... rest unchanged
```

- [ ] **Step 4: Update `NoteSpawner.SpawnNote` call site**

Edit `Assets/Scripts/Gameplay/NoteSpawner.cs`. In `SpawnNote`, update the `ctrl.Initialize` call to pass `n.pitch` between `n.t` and `n.type`:

```csharp
            ctrl.Initialize(
                audioSync, n.lane, laneX,
                n.t,
                n.pitch,
                n.type,
                n.dur,
                spawnY, judgmentY,
                previewMs,
                missMs,
                onAutoMiss: missed => judgmentSystem.HandleAutoMiss(missed));
```

- [ ] **Step 5: Run test to verify it passes**

Run the Unity EditMode command from **Conventions**.

Expected: `total="94"`, `failed="0"` (93 existing + 1 new).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Gameplay/NoteController.cs \
        Assets/Scripts/Gameplay/NoteSpawner.cs \
        Assets/Tests/EditMode/NoteControllerTests.cs \
        Assets/Tests/EditMode/NoteControllerTests.cs.meta
git commit -m "feat(w6): thread ChartNote.pitch to NoteController

ChartNote.pitch was already parsed and clamped in ChartLoader,
but NoteSpawner dropped it. Add a Pitch property + Initialize param
so JudgmentSystem/TapInputHandler can look up the intended pitch
for the sample pool.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: `LanePitches` static class (TDD)

**Files:**
- Create: `Assets/Tests/EditMode/LanePitchesTests.cs`
- Create: `Assets/Scripts/Gameplay/LanePitches.cs`

Tiny, independent, unblocks Task 7. Pure TDD cycle.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/LanePitchesTests.cs`:

```csharp
using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class LanePitchesTests
    {
        [Test]
        public void Default_Lane0_ReturnsC3()
        {
            Assert.AreEqual(48, LanePitches.Default(0));
        }

        [Test]
        public void Default_Lane1_ReturnsG3()
        {
            Assert.AreEqual(55, LanePitches.Default(1));
        }

        [Test]
        public void Default_Lane2_ReturnsC4()
        {
            Assert.AreEqual(60, LanePitches.Default(2));
        }

        [Test]
        public void Default_Lane3_ReturnsG4()
        {
            Assert.AreEqual(67, LanePitches.Default(3));
        }

        [Test]
        public void Default_NegativeLane_ReturnsMiddleC()
        {
            Assert.AreEqual(60, LanePitches.Default(-1));
        }

        [Test]
        public void Default_OutOfRangeLane_ReturnsMiddleC()
        {
            Assert.AreEqual(60, LanePitches.Default(99));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run the Unity EditMode command from **Conventions**.

Expected: compilation error — `LanePitches` type does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `Assets/Scripts/Gameplay/LanePitches.cs`:

```csharp
namespace KeyFlow
{
    public static class LanePitches
    {
        // Perfect 5th staircase across 4 lanes: C3, G3, C4, G4.
        // Low-clash with MVP song-set keys (A-minor, D/C/Db-major).
        private static readonly int[] defaults = { 48, 55, 60, 67 };

        public static int Default(int lane)
        {
            if (lane < 0 || lane >= defaults.Length) return 60;
            return defaults[lane];
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run the Unity EditMode command from **Conventions**.

Expected: `total="100"`, `failed="0"` (94 + 6).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Gameplay/LanePitches.cs \
        Assets/Scripts/Gameplay/LanePitches.cs.meta \
        Assets/Tests/EditMode/LanePitchesTests.cs \
        Assets/Tests/EditMode/LanePitchesTests.cs.meta
git commit -m "feat(w6): LanePitches fallback map for wrong-tap audio

Perfect-5th staircase (C3, G3, C4, G4) covers the 4 gameplay lanes
when no pending note lies within the tap-pitch lookup window.
Tuning avoids strong clash with the MVP song keys.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: `AudioSamplePool.ResolveSample` + `PlayForPitch` (TDD)

**Files:**
- Modify: `Assets/Tests/EditMode/AudioSamplePoolTests.cs`
- Modify: `Assets/Scripts/Gameplay/AudioSamplePool.cs`

Make `ResolveSample` a pure static method that takes the pitch-map array + base/step as parameters. Lets EditMode tests drive it with dummy `AudioClip.Create` clips without loading real WAVs.

- [ ] **Step 1: Write the failing tests**

Append to `Assets/Tests/EditMode/AudioSamplePoolTests.cs` (before the closing `}` of the class):

```csharp
        private static AudioClip[] MakeDummyMap(int count)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++)
                clips[i] = AudioClip.Create($"dummy{i}", 1, 1, 48000, false);
            return clips;
        }

        [Test]
        public void ResolveSample_ExactSamplePitch_ReturnsRatioOne()
        {
            var map = MakeDummyMap(17);
            var r36 = AudioSamplePool.ResolveSample(36, map, 36, 3);
            var r48 = AudioSamplePool.ResolveSample(48, map, 36, 3);
            var r84 = AudioSamplePool.ResolveSample(84, map, 36, 3);

            Assert.AreSame(map[0], r36.clip);
            Assert.AreEqual(1f, r36.pitchRatio, 1e-6f);

            Assert.AreSame(map[4], r48.clip);
            Assert.AreEqual(1f, r48.pitchRatio, 1e-6f);

            Assert.AreSame(map[16], r84.clip);
            Assert.AreEqual(1f, r84.pitchRatio, 1e-6f);
        }

        [Test]
        public void ResolveSample_PlusOneSemitone_ReturnsSameClipWithRatioUp()
        {
            var map = MakeDummyMap(17);
            var expectedRatio = Mathf.Pow(2f, 1f / 12f);

            var r37 = AudioSamplePool.ResolveSample(37, map, 36, 3);
            Assert.AreSame(map[0], r37.clip);
            Assert.AreEqual(expectedRatio, r37.pitchRatio, 1e-5f);

            var r82 = AudioSamplePool.ResolveSample(82, map, 36, 3);
            Assert.AreSame(map[15], r82.clip, "MIDI 82 should stay on A5v10 (index 15), not cross to C6");
            Assert.AreEqual(expectedRatio, r82.pitchRatio, 1e-5f);
        }

        [Test]
        public void ResolveSample_OffsetTwo_CrossesToNextSampleDown()
        {
            var map = MakeDummyMap(17);
            var expectedRatio = Mathf.Pow(2f, -1f / 12f);

            var r38 = AudioSamplePool.ResolveSample(38, map, 36, 3);
            Assert.AreSame(map[1], r38.clip, "MIDI 38 should cross to Ds2v10 (index 1) at pitch -1");
            Assert.AreEqual(expectedRatio, r38.pitchRatio, 1e-5f);

            var r83 = AudioSamplePool.ResolveSample(83, map, 36, 3);
            Assert.AreSame(map[16], r83.clip, "MIDI 83 should cross to C6v10 (index 16) at pitch -1");
            Assert.AreEqual(expectedRatio, r83.pitchRatio, 1e-5f);
        }

        [Test]
        public void ResolveSample_OutOfRangeLow_ClampsToFirstSample()
        {
            var map = MakeDummyMap(17);
            var r = AudioSamplePool.ResolveSample(20, map, 36, 3);
            Assert.AreSame(map[0], r.clip);
            Assert.AreEqual(1f, r.pitchRatio, 1e-6f);
        }

        [Test]
        public void ResolveSample_OutOfRangeHigh_ClampsToLastSample()
        {
            var map = MakeDummyMap(17);
            var r = AudioSamplePool.ResolveSample(120, map, 36, 3);
            Assert.AreSame(map[16], r.clip);
            Assert.AreEqual(1f, r.pitchRatio, 1e-6f);
        }

        [Test]
        public void ResolveSample_EmptyMap_ReturnsNullWithRatioOne()
        {
            var r = AudioSamplePool.ResolveSample(60, new AudioClip[0], 36, 3);
            Assert.IsNull(r.clip);
            Assert.AreEqual(1f, r.pitchRatio, 1e-6f);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run the Unity EditMode command from **Conventions**.

Expected: compilation error — `ResolveSample` not yet defined.

- [ ] **Step 3: Add `ResolveSample` (static) + `PlayForPitch` to `AudioSamplePool`**

Edit `Assets/Scripts/Gameplay/AudioSamplePool.cs`. Replace the file's contents with:

```csharp
using UnityEngine;

namespace KeyFlow
{
    public class AudioSamplePool : MonoBehaviour
    {
        [SerializeField] private int channels = 16;
        [SerializeField] private AudioClip defaultClip;

        [Header("Pitch Sample Map")]
        [SerializeField] private AudioClip[] pitchSamples;
        [SerializeField] private int baseMidi = 36;
        [SerializeField] private int stepSemitones = 3;

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
                src.spatialBlend = 0f;
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
            src.pitch = 1f;
            src.PlayOneShot(clip ?? defaultClip);
        }

        public void PlayForPitch(int midiPitch)
        {
            var (clip, ratio) = ResolveSample(midiPitch, pitchSamples, baseMidi, stepSemitones);
            if (clip == null)
            {
                PlayOneShot();
                return;
            }
            var src = NextSource();
            src.pitch = ratio;
            src.PlayOneShot(clip);
        }

        public static (AudioClip clip, float pitchRatio) ResolveSample(
            int midiPitch,
            AudioClip[] pitchSamples,
            int baseMidi,
            int stepSemitones)
        {
            if (pitchSamples == null || pitchSamples.Length == 0) return (null, 1f);

            int hi = baseMidi + (pitchSamples.Length - 1) * stepSemitones;
            int p = System.Math.Clamp(midiPitch, baseMidi, hi);

            int baseIdx = (p - baseMidi) / stepSemitones;
            int sampleMidi = baseMidi + baseIdx * stepSemitones;
            int offset = p - sampleMidi;

            if (offset == 2 && baseIdx + 1 < pitchSamples.Length)
            {
                baseIdx += 1;
                sampleMidi = baseMidi + baseIdx * stepSemitones;
                offset = -1;
            }

            float ratio = Mathf.Pow(2f, offset / 12f);
            return (pitchSamples[baseIdx], ratio);
        }
    }
}
```

Note: `PlayOneShot` now resets `src.pitch = 1f` before playing. This prevents pitch drift for legacy callers (e.g. if the same AudioSource was last used by `PlayForPitch` with ratio ≠ 1). Without this reset, the pooled AudioSource retains its last pitch between calls.

- [ ] **Step 4: Run tests to verify they pass**

Run the Unity EditMode command from **Conventions**.

Expected: `total="106"`, `failed="0"` (100 + 6).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Gameplay/AudioSamplePool.cs \
        Assets/Tests/EditMode/AudioSamplePoolTests.cs
git commit -m "feat(w6): AudioSamplePool.ResolveSample + PlayForPitch

Pure-static ResolveSample maps a MIDI pitch to (clip, ratio) against
an inspector-serialized pitch map (baseMidi + stepSemitones). Handles
exact hits (ratio=1), +1/-1 semitone, and the offset==2 branch that
crosses to the next sample down.

PlayForPitch uses the pool's round-robin AudioSource with the ratio
applied. PlayOneShot now resets src.pitch=1 to avoid drift from
pitched callers on the shared pool.

Out-of-range inputs clamp silently; empty pitch map falls back to
legacy PlayOneShot(defaultClip) so pre-wiring test scenes don't break.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: `JudgmentSystem.GetClosestPendingPitch` (TDD)

**Files:**
- Create: `Assets/Tests/EditMode/JudgmentSystemTests.cs`
- Modify: `Assets/Scripts/Gameplay/JudgmentSystem.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/JudgmentSystemTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.Charts;

namespace KeyFlow.Tests.EditMode
{
    public class JudgmentSystemTests
    {
        private static NoteController MakeNote(int lane, int hitMs, int pitch)
        {
            var go = new GameObject($"note_L{lane}_{hitMs}");
            var ctrl = go.AddComponent<NoteController>();
            ctrl.Initialize(
                sync: null,
                lane: lane,
                laneX: 0f,
                hitMs: hitMs,
                pitch: pitch,
                type: NoteType.TAP,
                durMs: 0,
                spawnY: 5f,
                judgmentY: -3f,
                previewMs: 2000,
                missGraceMs: 60,
                onAutoMiss: null);
            return ctrl;
        }

        private static JudgmentSystem MakeSystem()
        {
            var go = new GameObject("judgment");
            var js = go.AddComponent<JudgmentSystem>();
            js.Initialize(totalNotes: 4, difficulty: Difficulty.NORMAL);
            return js;
        }

        [Test]
        public void GetClosestPendingPitch_InWindow_ReturnsPitch()
        {
            var js = MakeSystem();
            js.RegisterPendingNote(MakeNote(lane: 1, hitMs: 1000, pitch: 64));

            int result = js.GetClosestPendingPitch(lane: 1, tapTimeMs: 1050, windowMs: 500);

            Assert.AreEqual(64, result);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void GetClosestPendingPitch_OutOfWindow_ReturnsMinusOne()
        {
            var js = MakeSystem();
            js.RegisterPendingNote(MakeNote(lane: 1, hitMs: 3000, pitch: 64));

            int result = js.GetClosestPendingPitch(lane: 1, tapTimeMs: 1000, windowMs: 500);

            Assert.AreEqual(-1, result);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void GetClosestPendingPitch_WrongLane_ReturnsMinusOne()
        {
            var js = MakeSystem();
            js.RegisterPendingNote(MakeNote(lane: 2, hitMs: 1000, pitch: 64));

            int result = js.GetClosestPendingPitch(lane: 0, tapTimeMs: 1000, windowMs: 500);

            Assert.AreEqual(-1, result);
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void GetClosestPendingPitch_MultipleOnLane_ReturnsTemporallyNearest()
        {
            var js = MakeSystem();
            js.RegisterPendingNote(MakeNote(lane: 1, hitMs: 900, pitch: 60));
            js.RegisterPendingNote(MakeNote(lane: 1, hitMs: 1100, pitch: 67));

            int result = js.GetClosestPendingPitch(lane: 1, tapTimeMs: 1050, windowMs: 500);

            Assert.AreEqual(67, result, "note at hitMs=1100 (delta=50) is closer than 900 (delta=150)");
            Object.DestroyImmediate(js.gameObject);
        }

        [Test]
        public void GetClosestPendingPitch_EmptyPending_ReturnsMinusOne()
        {
            var js = MakeSystem();

            int result = js.GetClosestPendingPitch(lane: 0, tapTimeMs: 1000, windowMs: 500);

            Assert.AreEqual(-1, result);
            Object.DestroyImmediate(js.gameObject);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the Unity EditMode command from **Conventions**.

Expected: compilation error — `GetClosestPendingPitch` not yet defined.

- [ ] **Step 3: Add the query method**

Edit `Assets/Scripts/Gameplay/JudgmentSystem.cs`. Add a new method **before the closing `}` of the class** (after `HandleHoldBreak`, before `HandleTap` — or simply at the end of the class, placement doesn't matter):

```csharp
        public int GetClosestPendingPitch(int lane, int tapTimeMs, int windowMs)
        {
            NoteController closest = null;
            int closestAbsDelta = int.MaxValue;
            for (int i = 0; i < pending.Count; i++)
            {
                var n = pending[i];
                if (n == null || n.Judged) continue;
                if (n.Lane != lane) continue;
                int delta = tapTimeMs - n.HitTimeMs;
                int abs = delta < 0 ? -delta : delta;
                if (abs < closestAbsDelta)
                {
                    closestAbsDelta = abs;
                    closest = n;
                }
            }
            if (closest == null || closestAbsDelta > windowMs) return -1;
            return closest.Pitch;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run the Unity EditMode command from **Conventions**.

Expected: `total="111"`, `failed="0"` (106 + 5).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Gameplay/JudgmentSystem.cs \
        Assets/Tests/EditMode/JudgmentSystemTests.cs \
        Assets/Tests/EditMode/JudgmentSystemTests.cs.meta
git commit -m "feat(w6): JudgmentSystem.GetClosestPendingPitch query

Read-only query for TapInputHandler to look up the intended pitch
before firing PlayForPitch. Mirrors the closest-note scan inside
HandleTap but adds a windowMs filter (returns -1 when no pending
note is close enough to treat as 'the note the user meant to hit').

Kept as a separate method from HandleTap's scan because HandleTap
takes the unconditionally-closest (its job is to judge every tap),
while this query filters by window (its job is to pick a pitch that
makes musical sense for the current moment).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: `TapInputHandler` pitch-aware audio path

**Files:**
- Modify: `Assets/Scripts/Gameplay/TapInputHandler.cs`

No new unit test — the logic here is a 3-line branch whose correctness reduces entirely to Task 6 (pitch map) + Task 7 (query). Integration covered by device validation in Task 10.

- [ ] **Step 1: Add SerializeFields and the helper**

Edit `Assets/Scripts/Gameplay/TapInputHandler.cs`. In the serialized-field block at the top of the class (after `mainCamera`, before `laneAreaWidth`), add:

```csharp
        [SerializeField] private JudgmentSystem judgmentSystem;
        [SerializeField] private int pitchLookupWindowMs = 500;
```

- [ ] **Step 2: Replace the two `samplePool.PlayOneShot()` call sites with a pitch-aware helper**

In `FirePress`, replace:
```csharp
            samplePool.PlayOneShot();
```
with:
```csharp
            PlayTapSound(lane, songTimeMs);
```

In `FirePressRaw`, replace:
```csharp
            samplePool.PlayOneShot();
```
with:
```csharp
            PlayTapSound(lane, songTimeMs);
```

Add this helper to the class (placement: anywhere — e.g., right before `FirePress`):

```csharp
        private void PlayTapSound(int lane, int songTimeMs)
        {
            int pitch = judgmentSystem != null
                ? judgmentSystem.GetClosestPendingPitch(lane, songTimeMs, pitchLookupWindowMs)
                : -1;
            if (pitch < 0) pitch = LanePitches.Default(lane);
            samplePool.PlayForPitch(pitch);
        }
```

- [ ] **Step 3: Run tests to verify nothing regressed**

Run the Unity EditMode command from **Conventions**.

Expected: `total="111"`, `failed="0"`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Gameplay/TapInputHandler.cs
git commit -m "feat(w6): TapInputHandler routes taps through PlayForPitch

FirePress/FirePressRaw now query JudgmentSystem for the nearest
pending note's pitch within pitchLookupWindowMs (default 500).
Falls back to LanePitches.Default(lane) when no pending note is
close enough (or JudgmentSystem reference is unset for dev scenes).

Audio still fires synchronously inside FirePress BEFORE OnLaneTap
— the query is O(pending_count) which is typically single-digit,
and the pending note is still in the list at query time because
HandleTap (which removes it) runs after OnLaneTap later in the
same frame.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: Wire scene references via editor script

**Files:**
- Create: `Assets/Editor/W6SamplesWireup.cs`

Unity scene references (serialized fields pointing at GameObjects/AudioClips) are saved into `.unity` scene files. Rather than hand-editing the YAML, use a `MenuItem` method invoked via `-executeMethod`. This matches the existing `SceneBuilder` pattern.

- [ ] **Step 1: Write the wire-up script**

Create `Assets/Editor/W6SamplesWireup.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow;
using KeyFlow.UI;

namespace KeyFlow.Editor
{
    public static class W6SamplesWireup
    {
        private const string ScenePath = "Assets/Scenes/GameplayScene.unity";
        private const string PianoFolder = "Assets/Audio/piano";

        private static readonly string[] SampleNames =
        {
            "C2v10", "Ds2v10", "Fs2v10", "A2v10",
            "C3v10", "Ds3v10", "Fs3v10", "A3v10",
            "C4v10", "Ds4v10", "Fs4v10", "A4v10",
            "C5v10", "Ds5v10", "Fs5v10", "A5v10",
            "C6v10",
        };

        [MenuItem("KeyFlow/W6 Samples Wireup")]
        public static void Wire()
        {
            // 1. Load the 17 clips in the expected order.
            var clips = new AudioClip[SampleNames.Length];
            for (int i = 0; i < SampleNames.Length; i++)
            {
                string path = $"{PianoFolder}/{SampleNames[i]}.wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    Debug.LogError($"[W6Wireup] Missing sample: {path}. Aborting.");
                    return;
                }
                clips[i] = clip;
            }

            // 2. Open the gameplay scene.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 3. Wire AudioSamplePool.pitchSamples.
            var pool = Object.FindFirstObjectByType<AudioSamplePool>();
            if (pool == null)
            {
                Debug.LogError("[W6Wireup] AudioSamplePool not found in scene.");
                return;
            }
            var poolSo = new SerializedObject(pool);
            var pitchSamplesProp = poolSo.FindProperty("pitchSamples");
            pitchSamplesProp.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
                pitchSamplesProp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            poolSo.FindProperty("baseMidi").intValue = 36;
            poolSo.FindProperty("stepSemitones").intValue = 3;
            poolSo.ApplyModifiedPropertiesWithoutUndo();

            // 4. Wire TapInputHandler.judgmentSystem.
            var tapInput = Object.FindFirstObjectByType<TapInputHandler>();
            var judgment = Object.FindFirstObjectByType<JudgmentSystem>();
            if (tapInput == null || judgment == null)
            {
                Debug.LogError("[W6Wireup] TapInputHandler or JudgmentSystem missing.");
                return;
            }
            var tapSo = new SerializedObject(tapInput);
            tapSo.FindProperty("judgmentSystem").objectReferenceValue = judgment;
            tapSo.ApplyModifiedPropertiesWithoutUndo();

            // 5. Wire SettingsScreen credits label — create a Text child if needed.
            var settings = Object.FindFirstObjectByType<SettingsScreen>(FindObjectsInactive.Include);
            if (settings == null)
            {
                Debug.LogWarning("[W6Wireup] SettingsScreen not found; skipping credits label wire-up.");
            }
            else
            {
                var settingsSo = new SerializedObject(settings);
                var creditsProp = settingsSo.FindProperty("creditsLabel");
                if (creditsProp.objectReferenceValue == null)
                {
                    var creditsGo = new GameObject("CreditsLabel", typeof(RectTransform), typeof(Text));
                    creditsGo.transform.SetParent(settings.transform, false);
                    var rt = creditsGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.05f, 0.02f);
                    rt.anchorMax = new Vector2(0.95f, 0.08f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    var txt = creditsGo.GetComponent<Text>();
                    txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    txt.fontSize = 18;
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                    txt.text = UIStrings.CreditsSamples;
                    creditsProp.objectReferenceValue = txt;
                }
                settingsSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 6. Save.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[W6Wireup] Done. Samples + JudgmentSystem ref + Credits label wired.");
        }
    }
}
```

- [ ] **Step 2: Invoke the wire-up via Unity batchmode `-executeMethod`**

Unity batchmode with `-executeMethod` requires `-quit` (opposite of `-runTests`). Foreground only.

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "C:/dev/unity-music/.claude/worktrees/ecstatic-franklin-a7436a" \
  -executeMethod KeyFlow.Editor.W6SamplesWireup.Wire \
  -logFile - -quit
```

Expected in stdout: `[W6Wireup] Done. Samples + JudgmentSystem ref + Credits label wired.`

If you see `[W6Wireup] Missing sample:` or similar, verify Task 3 completed.

- [ ] **Step 3: Run EditMode tests to verify nothing regressed**

Run the Unity EditMode command from **Conventions**.

Expected: `total="111"`, `failed="0"`.

- [ ] **Step 4: Commit scene + editor script**

```bash
git add Assets/Editor/W6SamplesWireup.cs \
        Assets/Editor/W6SamplesWireup.cs.meta \
        Assets/Scenes/GameplayScene.unity
git commit -m "chore(w6): editor script wires scene refs for pitch samples

KeyFlow/W6 Samples Wireup populates AudioSamplePool.pitchSamples
with the 17 clips in MIDI order, sets baseMidi=36 / step=3, points
TapInputHandler at JudgmentSystem, and creates a CreditsLabel Text
under SettingsScreen showing UIStrings.CreditsSamples.

Idempotent: existing creditsLabel reference is preserved; only
re-populates the sample array. Scene changes committed alongside.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: Device validation (Galaxy S22)

**Files:**
- Build artifact: `Builds/keyflow-w6.apk` (not committed)

Sign-off gate. Every checkbox must pass before marking the plan complete.

- [ ] **Step 1a: Bump APK output filename to w6**

Edit `Assets/Editor/ApkBuilder.cs` line 14:

```csharp
string apk = Path.Combine(dir, "keyflow-w5.apk");
```
→
```csharp
string apk = Path.Combine(dir, "keyflow-w6.apk");
```

Commit the rename as its own micro-commit:

```bash
git add Assets/Editor/ApkBuilder.cs
git commit -m "chore(w6): rename APK artifact to keyflow-w6.apk

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 1b: Build the APK**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "C:/dev/unity-music/.claude/worktrees/ecstatic-franklin-a7436a" \
  -executeMethod KeyFlow.Editor.ApkBuilder.Build \
  -logFile - -quit
```

Expected final artifact: `Builds/keyflow-w6.apk`. Log line: `[KeyFlow] APK built at Builds/keyflow-w6.apk, size <N> MB`.

- [ ] **Step 2: APK size check**

```bash
ls -la "C:/dev/unity-music/.claude/worktrees/ecstatic-franklin-a7436a/Builds/keyflow-w6.apk"
```

Expected: ≤ 36 MB (baseline W5 = 33.15 MB; +1.5 MB budget).

- [ ] **Step 3: Install on Galaxy S22**

```bash
adb install -r "C:/dev/unity-music/.claude/worktrees/ecstatic-franklin-a7436a/Builds/keyflow-w6.apk"
```

Expected: `Success`.

- [ ] **Step 4: Playtest checklist (user performs, assistant records)**

- [ ] App launches without ANR
- [ ] Main → Für Elise → **Easy** → Gameplay → runs to completion — melody heard via tap pitches, no regression in game feel
- [ ] Main → Für Elise → **Normal** → Gameplay → runs to completion — varied piano pitches track the chart melody
- [ ] Tapping an empty lane (no note nearby) plays a lane-default piano pitch, not silence or the old single-clip sound
- [ ] Settings screen shows the "Piano samples: Salamander Grand Piano V3 by Alexander Holm, CC-BY 3.0" line at the bottom
- [ ] Tap→sound latency feels equivalent or better than W5 (no added perceptual delay)
- [ ] Calibration still works (uses the retained `piano_c4.wav` — W6 does not change it)

- [ ] **Step 5: If any checklist item fails**

- Sample-related regression (no sound / wrong pitch / distortion): re-run the EditMode test suite; compare `ResolveSample` expected vs actual for the failing MIDI pitch; check `.meta` import settings survived.
- Missing Credits label: re-run `W6SamplesWireup.Wire` and inspect the GameplayScene YAML for the `CreditsLabel` GameObject under the Settings overlay hierarchy.
- Latency regression: verify `TapInputHandler.PlayTapSound` still calls `samplePool.PlayForPitch` **before** `OnLaneTap?.Invoke` (i.e., audio first, event second). Revert to direct `PlayOneShot()` path to isolate whether the query adds measurable latency.

---

## Risk Register Cross-Check (from spec §8)

The spec enumerates 7 risks. Coverage here:

| Risk | Mitigation in this plan |
|---|---|
| AudioSource.pitch glitch on device | Task 10 Step 4 playtest catches it. Fallback to offline 48-semitone expansion is out of scope for this plan (would be a follow-up). |
| Vorbis Q60 audibly lossy | Task 10 Step 4 playtest catches it. Raise Q in PianoSampleImportPostprocessor and re-run if flagged. |
| ±1 semitone timbre warp | Task 10 Step 4 playtest. Not blocking. |
| JudgmentSystem field addition breaks existing test wiring | Task 7 tests use null-safe/explicit setup; TapInputHandler (Task 8) has null-guard for judgmentSystem. |
| Ds/Fs rename mistake | Task 3 Step 2 bash loop uses explicit name maps; Step 5 meta-file grep confirms import stuck. |
| APK > 40 MB | Task 10 Step 2 size check. |
| CC-BY attribution missing | Tasks 1 (license text) + 2+9 (Credits label). Both bundle + visible UI. |

---

## Completion Criteria

- [ ] All 10 tasks ticked
- [ ] EditMode ≥ 111 pass (baseline 93 + 18 new: 1 NoteController + 6 LanePitches + 6 AudioSamplePool.ResolveSample + 5 JudgmentSystem)
- [ ] Task 10 Step 4 playtest checklist all ticked
- [ ] APK ≤ 36 MB
- [ ] Parent spec §11.2 correction committed (Task 1)
- [ ] No changes to Python pipeline (pytest 32/32 untouched)

---

## Post-implementation handoff

- Write W6 completion report at `docs/superpowers/reports/2026-04-21-w6-multipitch-samples-completion.md` (parallel to W5 report's style) summarizing: scope delivered, test counts, commits, deviations, W5 feedback resolution, next priorities (song content, profiler, etc.)
- Do not merge this worktree yet — the second W6 sub-project (4-song content) is next and may land on the same `claude/ecstatic-franklin-a7436a` branch or its own worktree, per user preference
