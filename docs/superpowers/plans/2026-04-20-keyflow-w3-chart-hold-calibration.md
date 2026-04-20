# KeyFlow W3 Implementation Plan: Chart Loader + Hold + Calibration + First Song

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn W2's hardcoded-sequence judgment engine into a data-driven song player. Deliver a build in which **Für Elise Easy plays end-to-end on Galaxy S22**, with Hold notes, first-launch audio calibration, and a completion panel that can Restart. On completion, EditMode test count rises from 40 → 68, and the APK passes the device checklist in the W3 spec.

**Architecture:** Three new pure-logic units (`ChartLoader.ParseJson`, `HoldStateMachine`, `CalibrationCalculator`) with full EditMode tests, plus four new MonoBehaviours (`HoldTracker`, `CalibrationController`, `CompletionPanel`, `GameplayController`). W2 components (`AudioSyncManager`, `AudioSamplePool`, `LaneLayout`, `JudgmentEvaluator`, `ScoreManager`, `LatencyMeter`) stay untouched. W2 MonoBehaviours (`TapInputHandler`, `NoteController`, `NoteSpawner`, `JudgmentSystem`) get targeted extensions for release tracking, Hold rendering, Hold handoff, and chart-driven spawning. `SceneBuilder` is rewritten to generate overlays; menu renamed `Build W3 Gameplay Scene`.

**Tech Stack:** Unity 6.3 LTS (6000.3.13f1), C#, .NET Standard 2.1, Unity Test Framework (NUnit EditMode), Unity UI (legacy Text/Image/Button), new Input System, **Newtonsoft.Json** (`com.unity.nuget.newtonsoft-json`, add if absent).

**Reference spec:** [2026-04-20-keyflow-w3-chart-hold-calibration-design.md](../specs/2026-04-20-keyflow-w3-chart-hold-calibration-design.md) — §2 decisions (Hold model C, pitch-shift A, metronome calibration A, completion panel B), §3 data model, §4 feature details, §6 test plan.

**Parent spec:** [2026-04-20-keyflow-mvp-v2-4lane-design.md](../specs/2026-04-20-keyflow-mvp-v2-4lane-design.md) — §5.1 judgment windows, §5.3 star thresholds, §7 `.kfchart` schema.

**Working directory:** `C:\dev\unity-music` (Windows, Git Bash).

**Starting state:** branch `main`, HEAD at `5a513d8` (docs(w3): tighten spec after reviewer advisory notes) or later. 40 EditMode tests passing from W2.

---

## Scope & Out-of-Scope

### In scope for W3
- Newtonsoft.Json package verification/addition
- `.kfchart` JSON schema types + pure `ChartLoader.ParseJson` + EditMode tests
- `ChartLoader.LoadFromStreamingAssets` runtime wrapper
- First chart file: `beethoven_fur_elise.kfchart` (Easy, ~60–100 notes, 45s duration, 2–4 Hold notes, pitches MIDI 48–72)
- `HoldStateMachine` pure logic + EditMode tests
- `TapInputHandler` extension: `OnLaneRelease` event + `IsLanePressed(int lane)` query
- `NoteController` Hold rendering (elongated capsule, color states)
- `JudgmentSystem` HOLD start-tap handoff to `HoldTracker`
- `HoldTracker` MonoBehaviour
- `CalibrationCalculator` pure logic + EditMode tests
- `CalibrationController` MonoBehaviour + overlay UI
- `CompletionPanel` MonoBehaviour + overlay UI
- `GameplayController` bootstrap orchestrator (calibration → chart load → gameplay → completion)
- `NoteSpawner` refactor: consume `ChartData` instead of hardcoded sequence
- `SceneBuilder` rewrite with new overlays + prefab/scene regeneration
- Play-in-editor smoke test + APK build + Galaxy S22 device test
- W3 completion report

### Out of scope (deferred to later weeks)
- Settings screen with Calibration re-run button → W4
- Splash / Main / Song list scenes → W4
- Full Results screen with star animations → W4 (CompletionPanel is the W3 stand-in)
- Python MIDI → .kfchart pipeline → W5
- Remaining 4 songs (Ode to Joy, Canon in D, Clair de Lune, The Entertainer) → W5
- 48-key Salamander sample bundle → W4/W5 (W3 keeps W1 single C4 + pitch-shift)
- Normal difficulty chart for Für Elise → W5
- Particles, haptics → W6
- PlayMode integration tests (not added per W2 policy; device playtest covers)

---

## File Structure

```
Assets/
├─ Scripts/
│   ├─ Common/
│   │   └─ GameTime.cs                 (unchanged)
│   ├─ Charts/
│   │   ├─ ChartTypes.cs               (C)  NoteType enum, ChartNote, ChartDifficulty, ChartData
│   │   └─ ChartLoader.cs              (C)  static ParseJson (pure) + LoadFromStreamingAssets (IO)
│   ├─ Gameplay/
│   │   ├─ AudioSyncManager.cs         (unchanged)
│   │   ├─ AudioSamplePool.cs          (unchanged)
│   │   ├─ JudgmentEvaluator.cs        (unchanged)
│   │   ├─ Judgment.cs                 (unchanged)
│   │   ├─ LaneLayout.cs               (unchanged)
│   │   ├─ ScoreManager.cs             (unchanged)
│   │   ├─ NoteController.cs           (M)  accept NoteType + dur; Hold capsule rendering
│   │   ├─ NoteSpawner.cs              (M)  consume ChartData instead of hardcoded sequence
│   │   ├─ JudgmentSystem.cs           (M)  on HOLD start tap → handoff to HoldTracker
│   │   ├─ TapInputHandler.cs          (M)  OnLaneRelease event + IsLanePressed query
│   │   ├─ HoldStateMachine.cs         (C)  pure state transitions
│   │   ├─ HoldTracker.cs              (C)  MonoBehaviour wrapping HoldStateMachine
│   │   └─ GameplayController.cs       (C)  bootstrap: calibration → load → spawn → completion
│   ├─ Calibration/
│   │   ├─ CalibrationCalculator.cs    (C)  pure: (expected[], taps[]) → Result
│   │   └─ CalibrationController.cs    (C)  MonoBehaviour + overlay UI driver
│   └─ UI/
│       ├─ LatencyMeter.cs             (unchanged)
│       └─ CompletionPanel.cs          (C)  MonoBehaviour + overlay UI
├─ StreamingAssets/
│   └─ charts/
│       └─ beethoven_fur_elise.kfchart (C)  hand-authored Easy chart, 73 notes, 45s
├─ Editor/
│   └─ SceneBuilder.cs                 (M)  W3 rewrite; menu renamed Build W3 Gameplay Scene
└─ Tests/EditMode/
    ├─ GameTimeTests.cs                (unchanged)
    ├─ LaneLayoutTests.cs              (unchanged)
    ├─ JudgmentEvaluatorTests.cs       (M)  +2 Hold-start-tap regression tests
    ├─ ScoreManagerTests.cs            (unchanged)
    ├─ ChartLoaderTests.cs             (C)  10 ParseJson tests
    ├─ HoldStateMachineTests.cs        (C)  7–9 state transition tests
    └─ CalibrationCalculatorTests.cs   (C)  6–7 compute tests

Packages/manifest.json                 (M)  add com.unity.nuget.newtonsoft-json if absent
```

No new asmdefs. `ChartLoader` and `ChartTypes` slot into the existing `KeyFlow.Runtime` asmdef (path is under `Assets/Scripts/`). Test files slot into `KeyFlow.Tests.EditMode` — **verify the EditMode asmdef references Newtonsoft.Json's asmdef** before adding parser tests.

**Design notes (why these boundaries):**
- `HoldStateMachine`, `ChartLoader.ParseJson`, `CalibrationCalculator` are pure static/class logic — no MonoBehaviour, no `UnityEngine.Object`. Fully EditMode-testable.
- `HoldTracker` wraps the state machine; `CalibrationController` wraps the calculator. The pure core never imports Unity; the wrapper owns all Unity types.
- `GameplayController` is the single bootstrap orchestrator. All existing scene-wide state transitions live here. Keeps `NoteSpawner` focused on just-spawning and `JudgmentSystem` focused on just-judging.
- `ChartTypes` and `ChartLoader` live in a separate folder/namespace-space to signal they're pure-data concerns, reusable outside gameplay.

---

## Prerequisites (verify before Task 1)

- Unity 6.3 LTS Editor present, opens the project without compile errors on `main` HEAD
- 40 EditMode tests currently pass (W2 baseline)
- Galaxy S22 connected via adb (`adb devices` shows 1 device)
- Repo clean at `C:\dev\unity-music` on branch `main`, HEAD at `5a513d8` or later
- Unity Editor open OK for most tasks — **close only during Task 14** (SceneBuilder rewrite regenerates scene asset)

---

## Task 1: Preflight — add Newtonsoft.Json + confirm TapInputHandler state

**Files:**
- Modify: `Packages/manifest.json` (conditionally — only if Newtonsoft.Json absent)
- Read-only check: `Assets/Scripts/Gameplay/TapInputHandler.cs`
- Read-only check: `Assets/Scripts/Gameplay/AudioSyncManager.cs`

- [ ] **Step 1.1: Verify clean starting state**

```bash
cd /c/dev/unity-music && git status --short && git log -1 --oneline
```

Expected: no uncommitted changes; HEAD at `5a513d8` or later.

- [ ] **Step 1.2: Check if Newtonsoft.Json is installed**

```bash
grep -c "com.unity.nuget.newtonsoft-json" Packages/manifest.json
```

If output is `1` or higher: installed, skip Step 1.3.
If output is `0`: proceed to Step 1.3.

- [ ] **Step 1.3: Add Newtonsoft.Json (only if Step 1.2 returned 0)**

Open `Packages/manifest.json`. In the `"dependencies"` block, add the line:

```json
"com.unity.nuget.newtonsoft-json": "3.2.1",
```

Place it alphabetically (after `com.unity.modules.xxx` entries, before any `com.unity.render-pipelines.xxx` — or any reasonable position inside `dependencies`). Then let Unity resolve:

Open Unity Editor. Wait for package resolution (~1 minute on first add). Verify no errors in the Console window. The Package Manager window (`Window → Package Manager`) should list "Newtonsoft Json" under "In Project".

- [ ] **Step 1.4: Verify AudioSyncManager.StartSilentSong exists**

```bash
grep -n "StartSilentSong" Assets/Scripts/Gameplay/AudioSyncManager.cs
```

Expected: one match in the method declaration (already exists from W1). If not found, the plan's assumption is broken — stop and escalate.

- [ ] **Step 1.5: Sanity-check TapInputHandler current shape**

```bash
grep -n "OnTap\|OnLaneTap\|OnLaneRelease\|wasPressedThisFrame\|wasReleasedThisFrame" Assets/Scripts/Gameplay/TapInputHandler.cs
```

Expected: `OnTap`, `OnLaneTap`, `wasPressedThisFrame` are found; `OnLaneRelease` and `wasReleasedThisFrame` are **not** found yet. Task 6 will add them. If they already exist, adjust Task 6 to integrate instead of add.

- [ ] **Step 1.6: Commit if manifest changed**

If Step 1.3 modified `manifest.json`:

```bash
cd /c/dev/unity-music && git add Packages/manifest.json Packages/packages-lock.json && git commit -m "$(cat <<'EOF'
chore(w3): add Newtonsoft.Json package for chart loader

Required for ChartLoader.ParseJson to deserialize .kfchart JSON
with Dictionary<Difficulty, ChartDifficulty> — JsonUtility cannot.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

If Step 1.3 was skipped: no commit this task.

---

## Task 2: Chart data types

**Files:**
- Create: `Assets/Scripts/Charts/ChartTypes.cs`

- [ ] **Step 2.1: Create folder**

```bash
cd /c/dev/unity-music && mkdir -p Assets/Scripts/Charts
```

- [ ] **Step 2.2: Write ChartTypes.cs**

Create `Assets/Scripts/Charts/ChartTypes.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace KeyFlow
{
    public enum NoteType { TAP, HOLD }

    [Serializable]
    public class ChartNote
    {
        public int t;
        public int lane;
        public int pitch;
        public NoteType type;
        public int dur;
    }

    [Serializable]
    public class ChartDifficulty
    {
        public int totalNotes;
        public List<ChartNote> notes;
    }

    [Serializable]
    public class ChartData
    {
        public string songId;
        public string title;
        public string composer;
        public int bpm;
        public int durationMs;
        public Dictionary<Difficulty, ChartDifficulty> charts;
    }

    public class ChartValidationException : Exception
    {
        public ChartValidationException(string message) : base(message) { }
    }
}
```

- [ ] **Step 2.3: Unity compiles cleanly**

Return to Unity Editor. Watch Console for compile errors. Expect: no errors. If Unity warns about `Dictionary<Difficulty, ChartDifficulty>` Serializable — ignore (JsonUtility can't serialize it but we use Newtonsoft.Json).

- [ ] **Step 2.4: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Charts/ && git commit -m "$(cat <<'EOF'
feat(chart): add ChartData/ChartNote/ChartDifficulty types + NoteType enum

Pure data types for .kfchart schema (spec §3.1-3.2). No logic;
ChartLoader.ParseJson (next task) will populate and validate.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: ChartLoader.ParseJson (pure function + tests)

**Files:**
- Create: `Assets/Scripts/Charts/ChartLoader.cs`
- Create: `Assets/Tests/EditMode/ChartLoaderTests.cs`

- [ ] **Step 3.1: Write failing tests first**

Create `Assets/Tests/EditMode/ChartLoaderTests.cs`:

```csharp
using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class ChartLoaderTests
    {
        private const string ValidJson = @"{
            ""songId"": ""test_song"",
            ""title"": ""Test"",
            ""composer"": ""Anon"",
            ""bpm"": 120,
            ""durationMs"": 5000,
            ""charts"": {
                ""EASY"": {
                    ""totalNotes"": 2,
                    ""notes"": [
                        {""t"": 1000, ""lane"": 0, ""pitch"": 60, ""type"": ""TAP"", ""dur"": 0},
                        {""t"": 2000, ""lane"": 3, ""pitch"": 72, ""type"": ""HOLD"", ""dur"": 500}
                    ]
                }
            }
        }";

        [Test]
        public void ParseJson_ValidInput_PopulatesAllFields()
        {
            var c = ChartLoader.ParseJson(ValidJson);
            Assert.AreEqual("test_song", c.songId);
            Assert.AreEqual(120, c.bpm);
            Assert.AreEqual(5000, c.durationMs);
            Assert.IsTrue(c.charts.ContainsKey(Difficulty.Easy));
            Assert.AreEqual(2, c.charts[Difficulty.Easy].notes.Count);
        }

        [Test]
        public void ParseJson_Tap_HasZeroDur()
        {
            var c = ChartLoader.ParseJson(ValidJson);
            var tap = c.charts[Difficulty.Easy].notes[0];
            Assert.AreEqual(NoteType.TAP, tap.type);
            Assert.AreEqual(0, tap.dur);
        }

        [Test]
        public void ParseJson_Hold_HasPositiveDur()
        {
            var c = ChartLoader.ParseJson(ValidJson);
            var hold = c.charts[Difficulty.Easy].notes[1];
            Assert.AreEqual(NoteType.HOLD, hold.type);
            Assert.AreEqual(500, hold.dur);
        }

        [Test]
        public void ParseJson_InvalidLane_Throws()
        {
            var bad = ValidJson.Replace(@"""lane"": 0", @"""lane"": 5");
            Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
        }

        [Test]
        public void ParseJson_NegativeLane_Throws()
        {
            var bad = ValidJson.Replace(@"""lane"": 0", @"""lane"": -1");
            Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
        }

        [Test]
        public void ParseJson_TapWithDur_Throws()
        {
            var bad = ValidJson.Replace(
                @"{""t"": 1000, ""lane"": 0, ""pitch"": 60, ""type"": ""TAP"", ""dur"": 0}",
                @"{""t"": 1000, ""lane"": 0, ""pitch"": 60, ""type"": ""TAP"", ""dur"": 100}");
            Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
        }

        [Test]
        public void ParseJson_HoldWithZeroDur_Throws()
        {
            var bad = ValidJson.Replace(
                @"{""t"": 2000, ""lane"": 3, ""pitch"": 72, ""type"": ""HOLD"", ""dur"": 500}",
                @"{""t"": 2000, ""lane"": 3, ""pitch"": 72, ""type"": ""HOLD"", ""dur"": 0}");
            Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
        }

        [Test]
        public void ParseJson_PitchOutOfRange_ClampsSilently()
        {
            var c = ChartLoader.ParseJson(ValidJson.Replace(@"""pitch"": 60", @"""pitch"": 100"));
            Assert.AreEqual(83, c.charts[Difficulty.Easy].notes[0].pitch);
        }

        [Test]
        public void ParseJson_TimeBeyondDuration_Throws()
        {
            var bad = ValidJson.Replace(@"""t"": 1000", @"""t"": 999999");
            Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
        }

        [Test]
        public void ParseJson_NegativeTime_Throws()
        {
            var bad = ValidJson.Replace(@"""t"": 1000", @"""t"": -10");
            Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
        }
    }
}
```

- [ ] **Step 3.2: Run tests — expect compile failure**

In Unity: `Window → General → Test Runner → EditMode → Run All`.
Expected: Compile error `ChartLoader does not exist`. Good.

- [ ] **Step 3.3: Add Newtonsoft.Json reference to EditMode asmdef**

Open `Assets/Tests/EditMode/KeyFlow.Tests.EditMode.asmdef` (locate via Project window). In the Inspector, under "Assembly Definition References", add `Unity.Nuget.Newtonsoft.Json` if absent. Save. Unity recompiles.

- [ ] **Step 3.4: Write ChartLoader.cs**

Create `Assets/Scripts/Charts/ChartLoader.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KeyFlow
{
    public static class ChartLoader
    {
        private const int PitchMin = 36;
        private const int PitchMax = 83;

        public static ChartData LoadFromStreamingAssets(string songId)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "charts", songId + ".kfchart");
            string json = File.ReadAllText(path);
            return ParseJson(json);
        }

        public static ChartData ParseJson(string json)
        {
            var root = JObject.Parse(json);

            var chart = new ChartData
            {
                songId = (string)root["songId"],
                title = (string)root["title"],
                composer = (string)root["composer"],
                bpm = (int)root["bpm"],
                durationMs = (int)root["durationMs"],
                charts = new Dictionary<Difficulty, ChartDifficulty>()
            };

            var chartsObj = (JObject)root["charts"];
            foreach (var prop in chartsObj.Properties())
            {
                Difficulty diff = ParseDifficulty(prop.Name);
                var diffObj = (JObject)prop.Value;
                var cd = new ChartDifficulty
                {
                    totalNotes = (int)diffObj["totalNotes"],
                    notes = new List<ChartNote>()
                };
                foreach (var n in (JArray)diffObj["notes"])
                {
                    var note = new ChartNote
                    {
                        t = (int)n["t"],
                        lane = (int)n["lane"],
                        pitch = Mathf.Clamp((int)n["pitch"], PitchMin, PitchMax),
                        type = ParseType((string)n["type"]),
                        dur = (int)n["dur"]
                    };
                    Validate(note, chart.durationMs);
                    cd.notes.Add(note);
                }
                chart.charts[diff] = cd;
            }
            return chart;
        }

        private static Difficulty ParseDifficulty(string s)
        {
            switch (s)
            {
                case "EASY": return Difficulty.Easy;
                case "NORMAL": return Difficulty.Normal;
                default: throw new ChartValidationException("Unknown difficulty: " + s);
            }
        }

        private static NoteType ParseType(string s)
        {
            switch (s)
            {
                case "TAP": return NoteType.TAP;
                case "HOLD": return NoteType.HOLD;
                default: throw new ChartValidationException("Unknown note type: " + s);
            }
        }

        private static void Validate(ChartNote n, int durationMs)
        {
            if (n.t < 0)
                throw new ChartValidationException($"t {n.t} negative");
            if (n.t > durationMs)
                throw new ChartValidationException($"t {n.t} exceeds durationMs {durationMs}");
            if (n.lane < 0 || n.lane > 3)
                throw new ChartValidationException($"lane {n.lane} out of range [0,3] at t={n.t}");
            if (n.dur < 0)
                throw new ChartValidationException($"dur {n.dur} negative at t={n.t}");
            if (n.type == NoteType.TAP && n.dur != 0)
                throw new ChartValidationException($"TAP must have dur=0, got {n.dur} at t={n.t}");
            if (n.type == NoteType.HOLD && n.dur <= 0)
                throw new ChartValidationException($"HOLD must have dur>0, got {n.dur} at t={n.t}");
        }
    }
}
```

- [ ] **Step 3.5: Run tests — expect all 10 pass**

In Test Runner: Run All → EditMode. Expected: 40 W2 tests + 8 new ChartLoader tests = 48 total, all green.

If any fail: read error, fix `ChartLoader.cs` only (tests are the spec). Re-run.

- [ ] **Step 3.6: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Charts/ChartLoader.cs Assets/Tests/EditMode/ChartLoaderTests.cs Assets/Tests/EditMode/KeyFlow.Tests.EditMode.asmdef && git commit -m "$(cat <<'EOF'
feat(chart): pure ChartLoader.ParseJson + 10 tests (spec 3.3-3.4)

Newtonsoft.Json-based parser. Validates lane 0..3, dur sign
matches type, clamps pitch to MIDI 36..83 silently.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: LoadFromStreamingAssets + first chart file

**Files:**
- Create: `Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart`
- Modify: `Assets/Scripts/Charts/ChartLoader.cs` (already has `LoadFromStreamingAssets` from Task 3; this task just exercises it)

- [ ] **Step 4.1: Create StreamingAssets/charts folder**

```bash
cd /c/dev/unity-music && mkdir -p Assets/StreamingAssets/charts
```

- [ ] **Step 4.2: Author Für Elise Easy chart**

Create `Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart`. The chart covers the iconic opening motif (~45 seconds) at NPS ~1.6, Easy. Lanes chosen by rough pitch tier (low pitches → lane 0/1, high → lane 2/3). Two Hold notes: one mid-song, one at end.

```json
{
  "songId": "beethoven_fur_elise",
  "title": "Für Elise",
  "composer": "Beethoven",
  "bpm": 72,
  "durationMs": 45000,
  "charts": {
    "EASY": {
      "totalNotes": 73,
      "notes": [
        {"t": 2000, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 2400, "lane": 3, "pitch": 63, "type": "TAP", "dur": 0},
        {"t": 2800, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 3200, "lane": 3, "pitch": 63, "type": "TAP", "dur": 0},
        {"t": 3600, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 4000, "lane": 2, "pitch": 59, "type": "TAP", "dur": 0},
        {"t": 4400, "lane": 2, "pitch": 62, "type": "TAP", "dur": 0},
        {"t": 4800, "lane": 2, "pitch": 60, "type": "TAP", "dur": 0},
        {"t": 5200, "lane": 1, "pitch": 57, "type": "TAP", "dur": 0},
        {"t": 5600, "lane": 0, "pitch": 48, "type": "TAP", "dur": 0},
        {"t": 6000, "lane": 0, "pitch": 52, "type": "TAP", "dur": 0},
        {"t": 6400, "lane": 1, "pitch": 57, "type": "TAP", "dur": 0},
        {"t": 6800, "lane": 2, "pitch": 60, "type": "TAP", "dur": 0},
        {"t": 7200, "lane": 2, "pitch": 59, "type": "TAP", "dur": 0},
        {"t": 7600, "lane": 0, "pitch": 48, "type": "TAP", "dur": 0},
        {"t": 8000, "lane": 0, "pitch": 52, "type": "TAP", "dur": 0},
        {"t": 8400, "lane": 1, "pitch": 55, "type": "TAP", "dur": 0},
        {"t": 8800, "lane": 2, "pitch": 59, "type": "TAP", "dur": 0},
        {"t": 9200, "lane": 2, "pitch": 60, "type": "TAP", "dur": 0},
        {"t": 9600, "lane": 2, "pitch": 62, "type": "HOLD", "dur": 800},
        {"t": 11000, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 11400, "lane": 3, "pitch": 63, "type": "TAP", "dur": 0},
        {"t": 11800, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 12200, "lane": 3, "pitch": 63, "type": "TAP", "dur": 0},
        {"t": 12600, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 13000, "lane": 2, "pitch": 59, "type": "TAP", "dur": 0},
        {"t": 13400, "lane": 2, "pitch": 62, "type": "TAP", "dur": 0},
        {"t": 13800, "lane": 2, "pitch": 60, "type": "TAP", "dur": 0},
        {"t": 14200, "lane": 1, "pitch": 57, "type": "TAP", "dur": 0},
        {"t": 15000, "lane": 0, "pitch": 48, "type": "TAP", "dur": 0},
        {"t": 15500, "lane": 0, "pitch": 52, "type": "TAP", "dur": 0},
        {"t": 16000, "lane": 1, "pitch": 57, "type": "TAP", "dur": 0},
        {"t": 16500, "lane": 2, "pitch": 60, "type": "TAP", "dur": 0},
        {"t": 17000, "lane": 2, "pitch": 59, "type": "TAP", "dur": 0},
        {"t": 17500, "lane": 0, "pitch": 48, "type": "TAP", "dur": 0},
        {"t": 18000, "lane": 0, "pitch": 53, "type": "TAP", "dur": 0},
        {"t": 18500, "lane": 1, "pitch": 57, "type": "TAP", "dur": 0},
        {"t": 19000, "lane": 2, "pitch": 60, "type": "TAP", "dur": 0},
        {"t": 19500, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 20000, "lane": 0, "pitch": 48, "type": "TAP", "dur": 0},
        {"t": 20500, "lane": 3, "pitch": 67, "type": "TAP", "dur": 0},
        {"t": 21000, "lane": 3, "pitch": 65, "type": "TAP", "dur": 0},
        {"t": 21500, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 22000, "lane": 2, "pitch": 62, "type": "TAP", "dur": 0},
        {"t": 22500, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 23000, "lane": 3, "pitch": 63, "type": "TAP", "dur": 0},
        {"t": 23500, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 24000, "lane": 3, "pitch": 63, "type": "TAP", "dur": 0},
        {"t": 24500, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 25000, "lane": 2, "pitch": 59, "type": "TAP", "dur": 0},
        {"t": 25500, "lane": 2, "pitch": 62, "type": "TAP", "dur": 0},
        {"t": 26000, "lane": 2, "pitch": 60, "type": "TAP", "dur": 0},
        {"t": 26500, "lane": 1, "pitch": 57, "type": "TAP", "dur": 0},
        {"t": 27500, "lane": 0, "pitch": 48, "type": "TAP", "dur": 0},
        {"t": 28000, "lane": 0, "pitch": 52, "type": "TAP", "dur": 0},
        {"t": 28500, "lane": 1, "pitch": 57, "type": "TAP", "dur": 0},
        {"t": 29000, "lane": 2, "pitch": 60, "type": "TAP", "dur": 0},
        {"t": 29500, "lane": 2, "pitch": 59, "type": "TAP", "dur": 0},
        {"t": 30000, "lane": 0, "pitch": 48, "type": "TAP", "dur": 0},
        {"t": 30500, "lane": 0, "pitch": 52, "type": "TAP", "dur": 0},
        {"t": 31000, "lane": 1, "pitch": 55, "type": "TAP", "dur": 0},
        {"t": 31500, "lane": 2, "pitch": 59, "type": "TAP", "dur": 0},
        {"t": 32000, "lane": 2, "pitch": 60, "type": "TAP", "dur": 0},
        {"t": 33000, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 33500, "lane": 3, "pitch": 63, "type": "TAP", "dur": 0},
        {"t": 34000, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 34500, "lane": 3, "pitch": 63, "type": "TAP", "dur": 0},
        {"t": 35000, "lane": 3, "pitch": 64, "type": "TAP", "dur": 0},
        {"t": 35500, "lane": 2, "pitch": 59, "type": "TAP", "dur": 0},
        {"t": 36000, "lane": 2, "pitch": 62, "type": "TAP", "dur": 0},
        {"t": 36500, "lane": 2, "pitch": 60, "type": "TAP", "dur": 0},
        {"t": 37000, "lane": 1, "pitch": 57, "type": "TAP", "dur": 0},
        {"t": 38000, "lane": 1, "pitch": 57, "type": "HOLD", "dur": 1500}
      ]
    }
  }
}
```

Notes:
- 73 total notes over ~40 seconds of active play. `durationMs: 45000` leaves 5s tail after last Hold ends (`38000 + 1500 = 39500`).
- Pitches span MIDI 48 (C3) to 67 (G4), all within ±1 octave of C4 for pitch-shift tolerance.
- Two Hold notes: short (800ms) at t=9600, long (1500ms) final at t=38000.
- 3-same-lane-in-a-row only appears at lanes 3 (the melody motif) — this is musically intentional for Für Elise's repeated high E, accepted.

- [ ] **Step 4.3: Editor — verify StreamingAssets appears**

Return to Unity Editor. In Project window, confirm `Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart` is visible. Unity generates a `.meta` sibling.

- [ ] **Step 4.4: Write a throwaway smoke test (optional but recommended)**

Temporarily add to `ChartLoaderTests.cs`:

```csharp
[Test]
public void LoadFromStreamingAssets_FurElise_Parses()
{
    var c = ChartLoader.LoadFromStreamingAssets("beethoven_fur_elise");
    Assert.AreEqual("beethoven_fur_elise", c.songId);
    Assert.AreEqual(73, c.charts[Difficulty.Easy].notes.Count);
}
```

Run in Test Runner. **If green, revert this test** (it's an IO test that requires StreamingAssets path to resolve in EditMode — fine on desktop, but we're not shipping it). Alternatively, keep as `[Explicit]` so it doesn't run by default. Choice: delete and rely on Play-in-editor test in Task 16.

- [ ] **Step 4.5: Commit chart file**

```bash
cd /c/dev/unity-music && git add Assets/StreamingAssets/ && git commit -m "$(cat <<'EOF'
feat(chart): hand-authored Für Elise Easy chart (73 notes, 45s)

Covers iconic opening motif + two Hold notes (mid + end) for
W3 device verification. All pitches MIDI 48-67 to stay within
±1 octave of C4 for pitch-shift quality.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: HoldStateMachine (pure logic + tests)

**Files:**
- Create: `Assets/Scripts/Gameplay/HoldStateMachine.cs`
- Create: `Assets/Tests/EditMode/HoldStateMachineTests.cs`

- [ ] **Step 5.1: Write failing tests**

Create `Assets/Tests/EditMode/HoldStateMachineTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class HoldStateMachineTests
    {
        [Test]
        public void Start_WithoutTap_IsSpawned()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(lane: 0, startMs: 1000, endMs: 2000);
            Assert.AreEqual(HoldState.Spawned, sm.GetState(id));
        }

        [Test]
        public void OnStartTapAccepted_TransitionsToHolding()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            Assert.AreEqual(HoldState.Holding, sm.GetState(id));
        }

        [Test]
        public void Holding_ThroughEnd_TransitionsToCompleted()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            var pressed = new HashSet<int> { 0 };

            sm.Tick(songTimeMs: 1500, pressedLanes: pressed);
            Assert.AreEqual(HoldState.Holding, sm.GetState(id));

            sm.Tick(songTimeMs: 2000, pressedLanes: pressed);
            Assert.AreEqual(HoldState.Completed, sm.GetState(id));
        }

        [Test]
        public void Holding_ReleasedEarly_TransitionsToBroken()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);

            sm.Tick(1500, new HashSet<int>());
            Assert.AreEqual(HoldState.Broken, sm.GetState(id));
        }

        [Test]
        public void Spawned_Tick_RemainsSpawned()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.Tick(1500, new HashSet<int> { 0 });
            Assert.AreEqual(HoldState.Spawned, sm.GetState(id));
        }

        [Test]
        public void MultipleConcurrentHolds_IndependentStates()
        {
            var sm = new HoldStateMachine();
            var a = sm.Register(0, 1000, 2000);
            var b = sm.Register(2, 1200, 2500);
            sm.OnStartTapAccepted(a);
            sm.OnStartTapAccepted(b);

            var pressed = new HashSet<int> { 0 };
            sm.Tick(1500, pressed);

            Assert.AreEqual(HoldState.Holding, sm.GetState(a));
            Assert.AreEqual(HoldState.Broken, sm.GetState(b));
        }

        [Test]
        public void Completed_SubsequentTick_StaysCompleted()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            sm.Tick(2000, new HashSet<int> { 0 });
            sm.Tick(2500, new HashSet<int>());
            Assert.AreEqual(HoldState.Completed, sm.GetState(id));
        }

        [Test]
        public void Broken_SubsequentTick_StaysBroken()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            sm.Tick(1500, new HashSet<int>());
            sm.Tick(1800, new HashSet<int> { 0 });
            Assert.AreEqual(HoldState.Broken, sm.GetState(id));
        }

        [Test]
        public void TickReturnsTransitions_ForObservation()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            var transitions = sm.Tick(2000, new HashSet<int> { 0 });

            Assert.AreEqual(1, transitions.Count);
            Assert.AreEqual(id, transitions[0].id);
            Assert.AreEqual(HoldState.Completed, transitions[0].newState);
        }
    }
}
```

- [ ] **Step 5.2: Run tests — expect compile failure**

Test Runner → Run All. Expected: compile error (HoldStateMachine missing).

- [ ] **Step 5.3: Implement HoldStateMachine.cs**

Create `Assets/Scripts/Gameplay/HoldStateMachine.cs`:

```csharp
using System.Collections.Generic;

namespace KeyFlow
{
    public enum HoldState { Spawned, Holding, Completed, Broken }

    public struct HoldTransition
    {
        public int id;
        public HoldState newState;
    }

    public class HoldStateMachine
    {
        private class Entry
        {
            public int lane;
            public int startMs;
            public int endMs;
            public HoldState state;
        }

        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private int nextId = 1;

        public int Register(int lane, int startMs, int endMs)
        {
            int id = nextId++;
            entries[id] = new Entry
            {
                lane = lane,
                startMs = startMs,
                endMs = endMs,
                state = HoldState.Spawned
            };
            return id;
        }

        public HoldState GetState(int id) => entries[id].state;

        public void OnStartTapAccepted(int id)
        {
            if (entries.TryGetValue(id, out var e) && e.state == HoldState.Spawned)
                e.state = HoldState.Holding;
        }

        public List<HoldTransition> Tick(int songTimeMs, HashSet<int> pressedLanes)
        {
            var transitions = new List<HoldTransition>();
            foreach (var kv in entries)
            {
                var e = kv.Value;
                if (e.state != HoldState.Holding) continue;

                if (!pressedLanes.Contains(e.lane))
                {
                    e.state = HoldState.Broken;
                    transitions.Add(new HoldTransition { id = kv.Key, newState = HoldState.Broken });
                }
                else if (songTimeMs >= e.endMs)
                {
                    e.state = HoldState.Completed;
                    transitions.Add(new HoldTransition { id = kv.Key, newState = HoldState.Completed });
                }
            }
            return transitions;
        }
    }
}
```

- [ ] **Step 5.4: Run tests — expect all 9 pass**

Test Runner → Run All. Expected: 50 + 9 = 59 tests passing.

- [ ] **Step 5.5: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Gameplay/HoldStateMachine.cs Assets/Tests/EditMode/HoldStateMachineTests.cs && git commit -m "$(cat <<'EOF'
feat(hold): pure HoldStateMachine + 9 tests (spec 4.1 model C)

States: Spawned → Holding → Completed|Broken. Tick returns
transitions for observability. Fully decoupled from Unity.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: TapInputHandler extension — OnLaneRelease + IsLanePressed

**Files:**
- Modify: `Assets/Scripts/Gameplay/TapInputHandler.cs`

- [ ] **Step 6.1: Replace TapInputHandler.cs contents**

Rewrite `Assets/Scripts/Gameplay/TapInputHandler.cs`. This tracks per-touch press/release and exposes a lane-pressed query.

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace KeyFlow
{
    public class TapInputHandler : MonoBehaviour
    {
        [SerializeField] private AudioSamplePool samplePool;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private float laneAreaWidth = 4f;

        public System.Action<int> OnTap;
        public System.Action<int, int> OnLaneTap;
        public System.Action<int> OnLaneRelease;

        private readonly Dictionary<int, int> touchToLane = new Dictionary<int, int>();
        private readonly HashSet<int> pressedLanes = new HashSet<int>();
        private bool mousePressed;
        private int mouseLane = -1;

        public bool IsLanePressed(int lane) => pressedLanes.Contains(lane);

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Update()
        {
            int songTimeMs = audioSync != null ? audioSync.SongTimeMs : 0;

            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    int tid = touch.touchId.ReadValue();

                    if (touch.press.wasPressedThisFrame)
                    {
                        Vector2 pos = touch.position.ReadValue();
                        int lane = ScreenToLane(pos);
                        FirePress(tid, lane, songTimeMs);
                    }
                    else if (touch.press.wasReleasedThisFrame)
                    {
                        FireRelease(tid);
                    }
                }
            }

            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    Vector2 pos = Mouse.current.position.ReadValue();
                    int lane = ScreenToLane(pos);
                    mousePressed = true;
                    mouseLane = lane;
                    FirePressRaw(lane, songTimeMs);
                }
                else if (Mouse.current.leftButton.wasReleasedThisFrame && mousePressed)
                {
                    FireReleaseRaw(mouseLane);
                    mousePressed = false;
                    mouseLane = -1;
                }
            }
        }

        private int ScreenToLane(Vector2 screenPos)
        {
            Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10));
            return LaneLayout.XToLane(world.x, laneAreaWidth);
        }

        private void FirePress(int touchId, int lane, int songTimeMs)
        {
            touchToLane[touchId] = lane;
            pressedLanes.Add(lane);
            samplePool.PlayOneShot();
            OnTap?.Invoke(songTimeMs);
            OnLaneTap?.Invoke(songTimeMs, lane);
        }

        private void FireRelease(int touchId)
        {
            if (!touchToLane.TryGetValue(touchId, out int lane)) return;
            touchToLane.Remove(touchId);
            // Only remove from pressedLanes if no other touch is on this lane
            bool stillPressed = false;
            foreach (var kv in touchToLane) if (kv.Value == lane) { stillPressed = true; break; }
            if (!stillPressed) pressedLanes.Remove(lane);
            OnLaneRelease?.Invoke(lane);
        }

        private void FirePressRaw(int lane, int songTimeMs)
        {
            pressedLanes.Add(lane);
            samplePool.PlayOneShot();
            OnTap?.Invoke(songTimeMs);
            OnLaneTap?.Invoke(songTimeMs, lane);
        }

        private void FireReleaseRaw(int lane)
        {
            pressedLanes.Remove(lane);
            OnLaneRelease?.Invoke(lane);
        }
    }
}
```

- [ ] **Step 6.2: Verify compile**

Return to Unity Editor. Console should be clean.

- [ ] **Step 6.3: Run EditMode tests (smoke, nothing new here)**

Test Runner → Run All. Expected: 59 tests still pass (TapInputHandler has no EditMode tests; this is just a regression check — if compilation broke anything, some W2 test may fail).

- [ ] **Step 6.4: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Gameplay/TapInputHandler.cs && git commit -m "$(cat <<'EOF'
feat(input): OnLaneRelease event + IsLanePressed query (spec 4.1)

Tracks touchId → lane mapping so Hold release detection works
across multi-touch and mouse input. Required for HoldStateMachine
break detection.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: NoteController Hold rendering

**Files:**
- Modify: `Assets/Scripts/Gameplay/NoteController.cs`

- [ ] **Step 7.1: Rewrite NoteController.cs**

Replace contents of `Assets/Scripts/Gameplay/NoteController.cs`:

```csharp
using UnityEngine;

namespace KeyFlow
{
    public class NoteController : MonoBehaviour
    {
        [SerializeField] private int previewTimeMs = 2000;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private AudioSyncManager audioSync;
        private int hitTimeMs;
        private int lane;
        private int durMs;
        private NoteType noteType;
        private float spawnY;
        private float judgmentY;
        private float laneX;
        private bool initialized;
        private bool judged;
        private int missGraceMs;
        private System.Action<NoteController> onAutoMiss;

        public int HitTimeMs => hitTimeMs;
        public int Lane => lane;
        public int DurMs => durMs;
        public NoteType Type => noteType;
        public bool Judged => judged;

        public void Initialize(
            AudioSyncManager sync,
            int lane, float laneX,
            int hitMs,
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
            this.noteType = type;
            this.durMs = durMs;
            this.spawnY = spawnY;
            this.judgmentY = judgmentY;
            this.previewTimeMs = previewMs;
            this.missGraceMs = missGraceMs;
            this.onAutoMiss = onAutoMiss;
            transform.position = new Vector3(laneX, spawnY, 0);

            if (type == NoteType.HOLD)
            {
                float holdHeightUnits = (durMs / (float)previewMs) * (spawnY - judgmentY);
                transform.localScale = new Vector3(
                    transform.localScale.x,
                    transform.localScale.y + holdHeightUnits,
                    transform.localScale.z);
            }

            initialized = true;
        }

        public void MarkJudged()
        {
            judged = true;
            Destroy(gameObject);
        }

        public void MarkAcceptedAsHold()
        {
            // Called by JudgmentSystem when a HOLD start tap is judged P/G/G.
            // Marks judged to block auto-miss, but leaves the GameObject alive
            // so HoldTracker can drive Completion/Broken visuals.
            judged = true;
        }

        public void MarkHoldCompleted()
        {
            Destroy(gameObject);
        }

        public void MarkHoldBroken()
        {
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            Destroy(gameObject, 0.2f);
        }

        private void Update()
        {
            if (!initialized || judged || !audioSync.IsPlaying) return;

            int songTime = audioSync.SongTimeMs;
            float progress = GameTime.GetNoteProgress(songTime, hitTimeMs, previewTimeMs);
            if (progress < 0f) return;

            transform.position = new Vector3(
                laneX,
                Mathf.LerpUnclamped(spawnY, judgmentY, progress),
                0);

            // Auto-miss: start tap never came within the miss window.
            // Applies to both TAP and HOLD (HOLD whose start tap was accepted
            // is already `judged=true` and returns at the top of Update).
            if (songTime > hitTimeMs + missGraceMs)
            {
                judged = true;
                onAutoMiss?.Invoke(this);
                Destroy(gameObject);
            }
        }
    }
}
```

**Key changes from W2:**
- `Initialize` now takes `NoteType type` and `int durMs`.
- `MarkHoldCompleted` / `MarkHoldBroken` added — `HoldTracker` (Task 8) calls these.
- Hold notes render taller: scale Y extended by `(durMs / previewMs) * fall distance`.
- Auto-miss logic treats HOLD the same as TAP for the initial-tap miss: if no one has tapped within miss window, the whole Hold is a Miss.

- [ ] **Step 7.2: Verify compile**

Editor Console clean. Existing NoteSpawner in W2 code still calls old `Initialize` signature — **expect one red compile error**: `NoteSpawner.cs: No overload for Initialize takes 9 arguments`. This is expected; Task 13 fixes it. For now, to unblock tests, temporarily apply the minimal NoteSpawner patch:

- [ ] **Step 7.3: Temporary NoteSpawner patch to keep compile green**

In `Assets/Scripts/Gameplay/NoteSpawner.cs`, inside `SpawnNote`, replace the `ctrl.Initialize(...)` call with the new signature. Pass `NoteType.TAP` and `0` for dur — Task 13 will replace with chart-driven values:

```csharp
ctrl.Initialize(
    audioSync, lane, laneX,
    hitTimeMs,
    NoteType.TAP,
    0,
    spawnY, judgmentY,
    previewMs,
    missMs,
    onAutoMiss: n => judgmentSystem.HandleAutoMiss(n));
```

This is a surgical 2-line change just to keep the build green.

- [ ] **Step 7.4: Run EditMode tests**

Test Runner → Run All. Expected: 59 tests still pass.

- [ ] **Step 7.5: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Gameplay/NoteController.cs Assets/Scripts/Gameplay/NoteSpawner.cs && git commit -m "$(cat <<'EOF'
feat(note): Hold rendering + NoteType/dur in NoteController (spec 4.1)

Adds MarkHoldCompleted / MarkHoldBroken lifecycle hooks for
HoldTracker. Hold notes render as elongated sprites scaled
by duration. NoteSpawner gets a surgical patch to the new
Initialize signature; full chart-driven refactor in Task 13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: JudgmentSystem HOLD handoff

**Files:**
- Modify: `Assets/Scripts/Gameplay/JudgmentSystem.cs`

- [ ] **Step 8.1: Add Hold-note branch**

In `Assets/Scripts/Gameplay/JudgmentSystem.cs`, replace `HandleTap` with a version that checks `NoteType` and delegates Hold start-taps to a registered `HoldTracker`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace KeyFlow
{
    public class JudgmentSystem : MonoBehaviour
    {
        [SerializeField] private TapInputHandler tapInput;
        [SerializeField] private HoldTracker holdTracker;

        private readonly List<NoteController> pending = new List<NoteController>();
        private ScoreManager score;
        private Difficulty difficulty;

        public ScoreManager Score => score;
        public Judgment LastJudgment { get; private set; }
        public int LastDeltaMs { get; private set; }

        public void Initialize(int totalNotes, Difficulty difficulty)
        {
            this.difficulty = difficulty;
            score = new ScoreManager(totalNotes);
            LastJudgment = Judgment.Miss;
        }

        private void OnEnable()
        {
            if (tapInput != null) tapInput.OnLaneTap += HandleTap;
        }

        private void OnDisable()
        {
            if (tapInput != null) tapInput.OnLaneTap -= HandleTap;
        }

        public void RegisterPendingNote(NoteController note)
        {
            pending.Add(note);
        }

        public void HandleAutoMiss(NoteController note)
        {
            if (score == null) return;
            pending.Remove(note);
            score.RegisterJudgment(Judgment.Miss);
            LastJudgment = Judgment.Miss;
            LastDeltaMs = 0;
        }

        public void HandleHoldBreak()
        {
            if (score == null) return;
            score.RegisterJudgment(Judgment.Miss);
            LastJudgment = Judgment.Miss;
            LastDeltaMs = 0;
        }

        private void HandleTap(int tapTimeMs, int tapLane)
        {
            if (score == null) return;

            NoteController closest = null;
            int closestAbsDelta = int.MaxValue;
            for (int i = 0; i < pending.Count; i++)
            {
                var n = pending[i];
                if (n == null || n.Judged) continue;
                if (n.Lane != tapLane) continue;
                int delta = tapTimeMs - n.HitTimeMs;
                int abs = delta < 0 ? -delta : delta;
                if (abs < closestAbsDelta)
                {
                    closestAbsDelta = abs;
                    closest = n;
                }
            }

            if (closest == null) return;

            int signedDelta = tapTimeMs - closest.HitTimeMs;
            var result = JudgmentEvaluator.Evaluate(signedDelta, difficulty);
            if (result.Judgment == Judgment.Miss) return;

            score.RegisterJudgment(result.Judgment);
            LastJudgment = result.Judgment;
            LastDeltaMs = result.DeltaMs;
            pending.Remove(closest);

            if (closest.Type == NoteType.HOLD && holdTracker != null)
            {
                closest.MarkAcceptedAsHold();
                holdTracker.OnHoldStartTapAccepted(closest);
            }
            else
            {
                closest.MarkJudged();
            }
        }
    }
}
```

**Key change:** on Hold start-tap acceptance, hand off to `HoldTracker` instead of destroying the note. `holdTracker` is an inspector-wired reference (Task 15 will wire it in SceneBuilder). `HandleHoldBreak()` is called by `HoldTracker` on BROKEN transitions to update score/combo.

- [ ] **Step 8.2: Verify compile**

Expect compile error: `HoldTracker` doesn't exist yet. **Leave unfixed** — Task 9 creates HoldTracker. This is deliberate sequencing: the next task is tight.

Alternatively, to keep the build green between Task 8 and Task 9, stub `HoldTracker` now:

```csharp
// Minimal stub; Task 9 replaces entirely.
using UnityEngine;
namespace KeyFlow {
    public class HoldTracker : MonoBehaviour {
        public void OnHoldStartTapAccepted(NoteController note) { }
    }
}
```

Save as `Assets/Scripts/Gameplay/HoldTracker.cs`. Task 9 overwrites this file.

- [ ] **Step 8.3: Run EditMode tests**

Test Runner → Run All. Expected: 59 tests still pass.

- [ ] **Step 8.4: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Gameplay/JudgmentSystem.cs Assets/Scripts/Gameplay/HoldTracker.cs && git commit -m "$(cat <<'EOF'
feat(judge): JudgmentSystem hands off HOLD start-taps to HoldTracker

On accepted Hold start-tap: RegisterJudgment still fires (score
banks the P/G/G), note is removed from pending, but MarkJudged
is skipped — HoldTracker takes ownership. HoldTracker stub
committed; Task 9 fleshes it out.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: HoldTracker MonoBehaviour

**Files:**
- Modify: `Assets/Scripts/Gameplay/HoldTracker.cs` (replacing Task 8 stub)

- [ ] **Step 9.1: Replace HoldTracker.cs**

Rewrite `Assets/Scripts/Gameplay/HoldTracker.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace KeyFlow
{
    public class HoldTracker : MonoBehaviour
    {
        [SerializeField] private TapInputHandler tapInput;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private JudgmentSystem judgmentSystem;

        private readonly HoldStateMachine stateMachine = new HoldStateMachine();
        private readonly Dictionary<int, NoteController> idToNote = new Dictionary<int, NoteController>();

        public void OnHoldStartTapAccepted(NoteController note)
        {
            int endMs = note.HitTimeMs + note.DurMs;
            int id = stateMachine.Register(note.Lane, note.HitTimeMs, endMs);
            stateMachine.OnStartTapAccepted(id);
            idToNote[id] = note;
        }

        private void Update()
        {
            if (!audioSync.IsPlaying) return;

            var pressed = new HashSet<int>();
            for (int lane = 0; lane < LaneLayout.LaneCount; lane++)
                if (tapInput.IsLanePressed(lane)) pressed.Add(lane);

            var transitions = stateMachine.Tick(audioSync.SongTimeMs, pressed);
            foreach (var t in transitions)
            {
                if (!idToNote.TryGetValue(t.id, out var note)) continue;

                if (t.newState == HoldState.Completed)
                {
                    note.MarkHoldCompleted();
                }
                else if (t.newState == HoldState.Broken)
                {
                    judgmentSystem.HandleHoldBreak();
                    note.MarkHoldBroken();
                }
                idToNote.Remove(t.id);
            }
        }
    }
}
```

- [ ] **Step 9.2: Verify compile**

Editor Console clean. No pending errors.

- [ ] **Step 9.3: Run EditMode tests**

Test Runner → Run All. Expected: 59 tests still pass.

- [ ] **Step 9.4: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Gameplay/HoldTracker.cs && git commit -m "$(cat <<'EOF'
feat(hold): HoldTracker MonoBehaviour wires state machine to scene

Per-frame: collects pressed lanes from TapInputHandler, ticks
the state machine, drives note lifecycle hooks + calls
JudgmentSystem.HandleHoldBreak on BROKEN transitions.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: CalibrationCalculator (pure + tests)

**Files:**
- Create: `Assets/Scripts/Calibration/CalibrationCalculator.cs`
- Create: `Assets/Tests/EditMode/CalibrationCalculatorTests.cs`

- [ ] **Step 10.1: Create folder**

```bash
cd /c/dev/unity-music && mkdir -p Assets/Scripts/Calibration
```

- [ ] **Step 10.2: Write failing tests**

Create `Assets/Tests/EditMode/CalibrationCalculatorTests.cs`:

```csharp
using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class CalibrationCalculatorTests
    {
        private static double[] Expected(double first, int count, double interval)
        {
            var arr = new double[count];
            for (int i = 0; i < count; i++) arr[i] = first + i * interval;
            return arr;
        }

        [Test]
        public void Compute_PerfectTaps_ZeroOffset()
        {
            var exp = Expected(2.0, 8, 0.5);
            var r = CalibrationCalculator.Compute(exp, exp);
            Assert.AreEqual(0, r.offsetMs);
            Assert.IsTrue(r.reliable);
        }

        [Test]
        public void Compute_ConstantLateDelay_ReturnsDelayAsOffset()
        {
            var exp = Expected(2.0, 8, 0.5);
            var taps = new double[8];
            for (int i = 0; i < 8; i++) taps[i] = exp[i] + 0.100;
            var r = CalibrationCalculator.Compute(exp, taps);
            Assert.AreEqual(100, r.offsetMs);
            Assert.IsTrue(r.reliable);
        }

        [Test]
        public void Compute_ConstantEarlyTaps_NegativeOffset()
        {
            var exp = Expected(2.0, 8, 0.5);
            var taps = new double[8];
            for (int i = 0; i < 8; i++) taps[i] = exp[i] - 0.080;
            var r = CalibrationCalculator.Compute(exp, taps);
            Assert.AreEqual(-80, r.offsetMs);
            Assert.IsTrue(r.reliable);
        }

        [Test]
        public void Compute_OneOutlier_StillReliable()
        {
            var exp = Expected(2.0, 8, 0.5);
            var taps = new double[8];
            for (int i = 0; i < 8; i++) taps[i] = exp[i] + 0.050;
            taps[3] += 0.400; // one wild outlier
            var r = CalibrationCalculator.Compute(exp, taps);
            Assert.AreEqual(50, r.offsetMs);
            Assert.IsTrue(r.reliable);
        }

        [Test]
        public void Compute_HighVariance_NotReliable()
        {
            var exp = Expected(2.0, 8, 0.5);
            var taps = new double[8];
            double[] jitter = { 0.010, 0.200, -0.150, 0.180, -0.100, 0.220, -0.170, 0.190 };
            for (int i = 0; i < 8; i++) taps[i] = exp[i] + jitter[i];
            var r = CalibrationCalculator.Compute(exp, taps);
            Assert.IsFalse(r.reliable);
        }

        [Test]
        public void Compute_MissingTaps_StillComputes()
        {
            var exp = Expected(2.0, 8, 0.5);
            var taps = new double[6];
            for (int i = 0; i < 6; i++) taps[i] = exp[i] + 0.080;
            var r = CalibrationCalculator.Compute(exp, taps);
            Assert.AreEqual(80, r.offsetMs);
        }

        [Test]
        public void Compute_OffsetClampedToRange()
        {
            var exp = Expected(2.0, 8, 0.5);
            var taps = new double[8];
            for (int i = 0; i < 8; i++) taps[i] = exp[i] + 2.000;
            var r = CalibrationCalculator.Compute(exp, taps);
            Assert.AreEqual(500, r.offsetMs);
        }
    }
}
```

- [ ] **Step 10.3: Run tests — expect compile failure**

Expected: CalibrationCalculator missing.

- [ ] **Step 10.4: Implement CalibrationCalculator.cs**

Create `Assets/Scripts/Calibration/CalibrationCalculator.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KeyFlow
{
    public static class CalibrationCalculator
    {
        public struct Result
        {
            public int offsetMs;
            public int madMs;
            public bool reliable;
        }

        public static Result Compute(double[] expectedDspTimes, double[] tapDspTimes)
        {
            if (tapDspTimes == null || tapDspTimes.Length == 0)
                return new Result { offsetMs = 0, madMs = 0, reliable = false };

            var deltas = new List<double>(tapDspTimes.Length);
            foreach (var tap in tapDspTimes)
            {
                double nearest = expectedDspTimes[0];
                double bestDist = Math.Abs(tap - nearest);
                for (int i = 1; i < expectedDspTimes.Length; i++)
                {
                    double d = Math.Abs(tap - expectedDspTimes[i]);
                    if (d < bestDist) { bestDist = d; nearest = expectedDspTimes[i]; }
                }
                deltas.Add(tap - nearest);
            }

            deltas.Sort();
            // Trim outliers when enough samples
            if (deltas.Count >= 6)
            {
                deltas.RemoveAt(deltas.Count - 1);
                deltas.RemoveAt(0);
            }

            double median = Median(deltas);
            var absDev = new List<double>(deltas.Count);
            foreach (var d in deltas) absDev.Add(Math.Abs(d - median));
            absDev.Sort();
            double mad = Median(absDev);

            int offsetMs = Mathf.RoundToInt((float)(median * 1000.0));
            offsetMs = Mathf.Clamp(offsetMs, -500, 500);
            int madMs = Mathf.RoundToInt((float)(mad * 1000.0));

            return new Result
            {
                offsetMs = offsetMs,
                madMs = madMs,
                reliable = madMs <= 50
            };
        }

        private static double Median(List<double> sorted)
        {
            if (sorted.Count == 0) return 0.0;
            int n = sorted.Count;
            return (n % 2 == 1) ? sorted[n / 2] : 0.5 * (sorted[n / 2 - 1] + sorted[n / 2]);
        }
    }
}
```

- [ ] **Step 10.5: Run tests — expect all 7 pass**

Test Runner → Run All. Expected: 59 + 7 = 66 tests passing.

If variance test fails because `reliable` threshold interpretation differs: the spec says MAD ≤ 50ms is reliable. Check test data: the `jitter` array has MAD > 50ms, so `reliable` should be false. If implementation returns true, inspect `madMs` value and adjust either threshold or test data (keeping spec threshold fixed).

- [ ] **Step 10.6: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Calibration/ Assets/Tests/EditMode/CalibrationCalculatorTests.cs && git commit -m "$(cat <<'EOF'
feat(calib): pure CalibrationCalculator + 7 tests (spec 4.2)

Median-based offset with outlier trim, MAD-based reliability
gate (≤50ms MAD → reliable), offset clamped to ±500ms.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 11: CalibrationController MonoBehaviour + overlay UI

**Files:**
- Create: `Assets/Scripts/Calibration/CalibrationController.cs`

- [ ] **Step 11.1: Write CalibrationController.cs**

Create `Assets/Scripts/Calibration/CalibrationController.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace KeyFlow
{
    public class CalibrationController : MonoBehaviour
    {
        private const string PrefsKey = "CalibOffsetMs";
        private const int ClickCount = 8;
        private const double IntervalSec = 0.5;
        private const double LeadInSec = 2.0;
        private const int MaxRetries = 3;

        [SerializeField] private AudioSource clickSource;
        [SerializeField] private AudioClip clickSample;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private Text promptText;
        [SerializeField] private Button startButton;
        [SerializeField] private Image[] beatIndicators;

        public System.Action OnCalibrationDone;

        private int retryCount;
        private bool running;
        private readonly List<double> tapDspTimes = new List<double>();
        private double[] expectedDspTimes;

        public static bool HasSavedOffset() => PlayerPrefs.HasKey(PrefsKey);

        public static int LoadSavedOffsetMs() => PlayerPrefs.GetInt(PrefsKey, 0);

        public void Begin()
        {
            gameObject.SetActive(true);
            retryCount = 0;
            ShowIdle("화면 아무 곳이나, 클릭 소리에 맞춰 8번 탭하세요.");
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartOneRun);
        }

        private void ShowIdle(string message)
        {
            promptText.text = message;
            startButton.gameObject.SetActive(true);
            foreach (var img in beatIndicators) img.color = Color.gray;
        }

        private void StartOneRun()
        {
            startButton.gameObject.SetActive(false);
            promptText.text = "준비...";
            StartCoroutine(RunCalibration());
        }

        private IEnumerator RunCalibration()
        {
            running = true;
            tapDspTimes.Clear();
            double start = AudioSettings.dspTime + LeadInSec;
            expectedDspTimes = new double[ClickCount];
            for (int i = 0; i < ClickCount; i++)
            {
                expectedDspTimes[i] = start + i * IntervalSec;
                clickSource.clip = clickSample;
                clickSource.PlayScheduled(expectedDspTimes[i]);
            }

            // Flash indicators at each click time
            for (int i = 0; i < ClickCount; i++)
            {
                while (AudioSettings.dspTime < expectedDspTimes[i]) yield return null;
                if (i < beatIndicators.Length) beatIndicators[i].color = Color.white;
                promptText.text = $"탭 {i + 1} / {ClickCount}";
            }

            // Wait 500ms tail after last click
            double tailEnd = expectedDspTimes[ClickCount - 1] + 0.5;
            while (AudioSettings.dspTime < tailEnd) yield return null;

            running = false;
            Evaluate();
        }

        private void Update()
        {
            if (!running) return;

            if (Touchscreen.current != null)
                foreach (var t in Touchscreen.current.touches)
                    if (t.press.wasPressedThisFrame)
                        tapDspTimes.Add(AudioSettings.dspTime);

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                tapDspTimes.Add(AudioSettings.dspTime);
        }

        private void Evaluate()
        {
            var result = CalibrationCalculator.Compute(expectedDspTimes, tapDspTimes.ToArray());

            if (result.reliable)
            {
                Save(result.offsetMs);
                Finish();
            }
            else
            {
                retryCount++;
                if (retryCount >= MaxRetries)
                {
                    Save(0);
                    Finish();
                }
                else
                {
                    ShowIdle($"결과가 흔들려요 (MAD {result.madMs}ms). 다시 해보세요. [{retryCount}/{MaxRetries - 1}]");
                }
            }
        }

        private void Save(int offsetMs)
        {
            PlayerPrefs.SetInt(PrefsKey, offsetMs);
            PlayerPrefs.Save();
            if (audioSync != null) audioSync.CalibrationOffsetSec = offsetMs / 1000.0;
        }

        private void Finish()
        {
            gameObject.SetActive(false);
            OnCalibrationDone?.Invoke();
        }
    }
}
```

- [ ] **Step 11.2: Verify compile**

Editor Console clean.

- [ ] **Step 11.3: Run EditMode tests**

Test Runner → Run All. Expected: 66 tests still pass.

- [ ] **Step 11.4: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Calibration/CalibrationController.cs && git commit -m "$(cat <<'EOF'
feat(calib): CalibrationController MonoBehaviour + overlay coroutine

Coroutine runs 8 clicks at 500ms interval after 2s lead-in.
Visual beat indicators flash at each click. Evaluate via
CalibrationCalculator, save offset to PlayerPrefs + inject
into AudioSyncManager. Retry up to 3 times; fall back to 0.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 12: CompletionPanel MonoBehaviour + overlay UI

**Files:**
- Create: `Assets/Scripts/UI/CompletionPanel.cs`

- [ ] **Step 12.1: Write CompletionPanel.cs**

Create `Assets/Scripts/UI/CompletionPanel.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace KeyFlow
{
    public class CompletionPanel : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text breakdownText;
        [SerializeField] private Text starsText;
        [SerializeField] private Button restartButton;

        private bool shown;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void Show(ScoreManager score)
        {
            shown = true;
            gameObject.SetActive(true);
            titleText.text = "SONG COMPLETE";
            scoreText.text = $"Score: {score.Score:N0}";
            comboText.text = $"Max Combo: {score.MaxCombo}";
            breakdownText.text =
                $"Perfect: {score.PerfectCount}   Great: {score.GreatCount}   Good: {score.GoodCount}   Miss: {score.MissCount}";
            starsText.text = new string('*', score.Stars) + new string('-', 3 - score.Stars);

            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(Restart);
        }

        private void Update()
        {
            if (!shown) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Restart();
        }

        private void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
```

- [ ] **Step 12.2: Verify compile + tests**

Expected: 66 tests still pass.

- [ ] **Step 12.3: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/UI/CompletionPanel.cs && git commit -m "$(cat <<'EOF'
feat(ui): CompletionPanel + Restart button (spec 4.3)

Stand-in for W4's real Results screen. Shows score, max combo,
P/G/G/M counts, stars. Restart reloads active scene. Escape key
(desktop) or Back button (Android via Input System) also restarts.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 13: NoteSpawner chart-driven refactor

**Files:**
- Modify: `Assets/Scripts/Gameplay/NoteSpawner.cs`

- [ ] **Step 13.1: Rewrite NoteSpawner.cs**

Replace `Assets/Scripts/Gameplay/NoteSpawner.cs`:

```csharp
using UnityEngine;

namespace KeyFlow
{
    public class NoteSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject notePrefab;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private JudgmentSystem judgmentSystem;
        [SerializeField] private float laneAreaWidth = 4f;
        [SerializeField] private float spawnY = 4f;
        [SerializeField] private float judgmentY = -3f;
        [SerializeField] private int previewMs = 2000;

        private ChartDifficulty chart;
        private Difficulty difficulty;
        private int spawnedCount;
        private bool initialized;

        public int LastSpawnedHitMs { get; private set; }
        public int LastSpawnedDurMs { get; private set; }
        public Difficulty CurrentDifficulty => difficulty;
        public int TotalNotes => chart != null ? chart.notes.Count : 0;

        public void Initialize(ChartDifficulty chartDifficulty, Difficulty diff)
        {
            this.chart = chartDifficulty;
            this.difficulty = diff;
            this.spawnedCount = 0;
            this.initialized = true;
            judgmentSystem.Initialize(chart.notes.Count, diff);
        }

        public bool AllSpawned => initialized && spawnedCount >= chart.notes.Count;

        private void Update()
        {
            if (!initialized || !audioSync.IsPlaying) return;
            if (spawnedCount >= chart.notes.Count) return;

            var next = chart.notes[spawnedCount];
            if (audioSync.SongTimeMs >= next.t - previewMs)
            {
                SpawnNote(next);
                LastSpawnedHitMs = next.t;
                LastSpawnedDurMs = next.dur;
                spawnedCount++;
            }
        }

        private void SpawnNote(ChartNote n)
        {
            float laneX = LaneLayout.LaneToX(n.lane, laneAreaWidth);
            var go = Instantiate(notePrefab);
            var ctrl = go.GetComponent<NoteController>();
            int missMs = difficulty == Difficulty.Easy ? 225 : 180;
            ctrl.Initialize(
                audioSync, n.lane, laneX,
                n.t,
                n.type,
                n.dur,
                spawnY, judgmentY,
                previewMs,
                missMs,
                onAutoMiss: missed => judgmentSystem.HandleAutoMiss(missed));
            judgmentSystem.RegisterPendingNote(ctrl);
        }
    }
}
```

**Key changes:**
- `Initialize(ChartDifficulty, Difficulty)` replaces hardcoded serialized fields for note count / interval / first note. Scene/inspector no longer sets those.
- Each frame, check only the next-in-sequence note. `chart.notes` is already sorted by `t` (chart file authored in time order; if not, caller is wrong — no runtime sort here by design).
- Exposes `LastSpawnedHitMs` + `LastSpawnedDurMs` so `GameplayController` can determine when the song ends.
- **`Start()` no longer calls `audioSync.StartSilentSong()`** — `GameplayController` does that now, after calibration.

- [ ] **Step 13.2: Verify compile**

Console clean.

- [ ] **Step 13.3: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Gameplay/NoteSpawner.cs && git commit -m "$(cat <<'EOF'
refactor(spawner): chart-driven NoteSpawner (no more hardcoded seq)

Initialize(ChartDifficulty, Difficulty) replaces inspector-set
note count/interval. Exposes LastSpawnedHitMs/DurMs for
completion detection. StartSilentSong moved to GameplayController.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 14: GameplayController bootstrap

**Files:**
- Create: `Assets/Scripts/Gameplay/GameplayController.cs`

- [ ] **Step 14.1: Write GameplayController.cs**

Create `Assets/Scripts/Gameplay/GameplayController.cs`:

```csharp
using UnityEngine;

namespace KeyFlow
{
    public class GameplayController : MonoBehaviour
    {
        [SerializeField] private string songId = "beethoven_fur_elise";
        [SerializeField] private Difficulty difficulty = Difficulty.Easy;

        [SerializeField] private CalibrationController calibration;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private NoteSpawner spawner;
        [SerializeField] private JudgmentSystem judgmentSystem;
        [SerializeField] private CompletionPanel completionPanel;

        private ChartData chart;
        private bool playing;
        private bool completed;

        private void Start()
        {
            chart = ChartLoader.LoadFromStreamingAssets(songId);

            if (CalibrationController.HasSavedOffset())
            {
                audioSync.CalibrationOffsetSec = CalibrationController.LoadSavedOffsetMs() / 1000.0;
                BeginGameplay();
            }
            else
            {
                calibration.OnCalibrationDone = BeginGameplay;
                calibration.Begin();
            }
        }

        private void BeginGameplay()
        {
            calibration.OnCalibrationDone = null;
            var chartDiff = chart.charts[difficulty];
            spawner.Initialize(chartDiff, difficulty);
            audioSync.StartSilentSong();
            playing = true;
        }

        private void Update()
        {
            if (!playing || completed) return;
            if (!spawner.AllSpawned) return;

            int missWindowMs = difficulty == Difficulty.Easy ? 225 : 180;
            int endSongMs = spawner.LastSpawnedHitMs + spawner.LastSpawnedDurMs + missWindowMs;
            if (audioSync.SongTimeMs < endSongMs) return;

            int judgedExpected = spawner.TotalNotes;
            if (judgmentSystem.Score != null && judgmentSystem.Score.JudgedCount < judgedExpected) return;

            completed = true;
            completionPanel.Show(judgmentSystem.Score);
        }
    }
}
```

- [ ] **Step 14.2: Verify compile**

Console clean.

- [ ] **Step 14.3: Run EditMode tests**

Test Runner → Run All. Expected: 66 tests still pass.

- [ ] **Step 14.4: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Gameplay/GameplayController.cs && git commit -m "$(cat <<'EOF'
feat(game): GameplayController bootstrap + completion detection

Orchestrates: chart load → calibration-if-needed → spawner
init → StartSilentSong → wait for spawner-done + last-note
miss-window tail + all-judged → show CompletionPanel.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 15: JudgmentEvaluator regression tests + SceneBuilder W3 rewrite

**Files:**
- Modify: `Assets/Tests/EditMode/JudgmentEvaluatorTests.cs` (add 2 Hold-start-tap regression tests)
- Modify: `Assets/Editor/SceneBuilder.cs`

- [ ] **Step 15.1: Add 2 regression tests**

Append to `Assets/Tests/EditMode/JudgmentEvaluatorTests.cs`:

```csharp
[Test]
public void Evaluate_HoldStartTapInPerfectWindow_BehavesLikeTap()
{
    // Hold start taps use the same evaluator as Tap — this guards against
    // accidental per-NoteType branching creeping into the evaluator.
    var result = JudgmentEvaluator.Evaluate(30, Difficulty.Normal);
    Assert.AreEqual(Judgment.Perfect, result.Judgment);
    Assert.AreEqual(30, result.DeltaMs);
}

[Test]
public void Evaluate_HoldStartTapInGreatWindow_BehavesLikeTap()
{
    var result = JudgmentEvaluator.Evaluate(100, Difficulty.Normal);
    Assert.AreEqual(Judgment.Great, result.Judgment);
}
```

Run tests. Expected: 68 tests passing.

- [ ] **Step 15.2: Close Unity Editor**

(SceneBuilder regenerates the scene asset; avoid Editor-file conflicts.)

- [ ] **Step 15.3: Rewrite SceneBuilder.cs**

Replace `Assets/Editor/SceneBuilder.cs` with a W3-aware generator. It creates the same W2 geometry (portrait, 4 lanes, judgment line, LatencyMeter HUD) plus:
- Calibration overlay Canvas with prompt text, Start button, 8 beat indicator images
- Completion panel Canvas with title/score/combo/breakdown/stars text + Restart button
- `GameplayController`, `HoldTracker`, `CalibrationController`, `CompletionPanel` wired to the right GameObjects

Due to scene-construction complexity, the full body of SceneBuilder is long. Match the W2 SceneBuilder file's style exactly (same helper methods, same GUIUtility conventions). The key additions:

```csharp
// Inside BuildScene():
var holdTrackerGo = CreateGameObject("HoldTracker", rootObjects);
var ht = holdTrackerGo.AddComponent<HoldTracker>();
SetField(ht, "tapInput", tapInputHandlerRef);
SetField(ht, "audioSync", audioSyncRef);
SetField(ht, "judgmentSystem", judgmentSystemRef);

// Wire the reverse direction: JudgmentSystem needs a HoldTracker reference
// for the HOLD start-tap handoff branch in HandleTap (Task 8).
SetField(judgmentSystemRef, "holdTracker", ht);

var calibCanvas = CreateCalibrationOverlay(mainCanvasParent);
var compPanel = CreateCompletionPanel(mainCanvasParent);

var controllerGo = CreateGameObject("GameplayController", rootObjects);
var ctl = controllerGo.AddComponent<GameplayController>();
SetField(ctl, "songId", "beethoven_fur_elise");
SetField(ctl, "difficulty", Difficulty.Easy);
SetField(ctl, "calibration", calibCanvas.GetComponent<CalibrationController>());
SetField(ctl, "audioSync", audioSyncRef);
SetField(ctl, "spawner", spawnerRef);
SetField(ctl, "judgmentSystem", judgmentSystemRef);
SetField(ctl, "completionPanel", compPanel.GetComponent<CompletionPanel>());
```

Menu name is changed:

```csharp
[MenuItem("KeyFlow/Build W3 Gameplay Scene")]
public static void Build() { ... }
```

The NoteSpawner inspector fields for note-count / interval / first-hit are dropped since those are now chart-driven.

**Implementation hand-off to worker:** Read the full current `SceneBuilder.cs` first, duplicate its style, then insert the overlay creation methods. Don't invent a new pattern.

- [ ] **Step 15.4: Open Unity Editor**

Let scripts recompile.

- [ ] **Step 15.5: Regenerate scene via menu**

In Unity menu: `KeyFlow → Build W3 Gameplay Scene`. Verify:
- `GameplayScene.unity` updated
- `Note.prefab` still present (Hold rendering uses transform.localScale at runtime, no prefab change)
- Calibration and Completion Canvas GameObjects appear in hierarchy
- `GameplayController` GameObject has all SerializeField refs populated (not `None`)

- [ ] **Step 15.6: Run EditMode tests (regression)**

Test Runner → Run All. Expected: 68 tests passing.

- [ ] **Step 15.7: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Editor/SceneBuilder.cs Assets/Tests/EditMode/JudgmentEvaluatorTests.cs Assets/Scenes/GameplayScene.unity Assets/Scenes/GameplayScene.unity.meta Assets/Prefabs/Note.prefab.meta Assets/Prefabs/Note.prefab && git commit -m "$(cat <<'EOF'
refactor(scene): SceneBuilder for W3 calibration + completion overlays

Adds Calibration overlay Canvas + Completion panel Canvas to
generated scene. Wires GameplayController, HoldTracker. Menu
renamed to Build W3 Gameplay Scene.

Plus 2 regression tests: Hold start tap uses same JudgmentEvaluator
path as Tap.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 16: Play-in-editor smoke test + device verification + completion report

**Files:**
- Create: `docs/superpowers/reports/2026-04-20-w3-completion.md`

- [ ] **Step 16.1: Play-in-editor smoke test (desktop)**

Unity Editor: press Play on `GameplayScene`. Expected behavior on first run:

1. Calibration overlay visible (PlayerPrefs key absent on fresh install; if it exists from prior tests, go to Edit → Clear All PlayerPrefs via custom menu or just delete the `CalibOffsetMs` key).
2. Click Start → 2s lead-in → 8 clicks → tap mouse along with each.
3. Overlay closes, song begins. Notes fall across 4 lanes.
4. Tap notes via mouse at judgment line. Verify score climbs, combo increments.
5. Hold notes: click+hold, release mid-way → note fades gray (BROKEN). Click+hold, hold through → note disappears cleanly (COMPLETED).
6. After last note's window elapses, CompletionPanel fades in with final stats. Restart button reloads scene; subsequent runs skip calibration.

If any step fails: debug iteratively. Common failure: `GameplayController` SerializeFields not wired — re-run SceneBuilder menu.

- [ ] **Step 16.2: Clear PlayerPrefs for fresh device install**

Not required for device APK (fresh install has no prefs), but if re-testing on a device that already has the app, use ADB:

```bash
adb shell pm clear com.funqdev.keyflow
```

- [ ] **Step 16.3: Build APK**

Unity: `File → Build Profiles → Android → Build`. Output to `Builds/W3-<timestamp>.apk` or whatever the project convention is (check W2's report for the naming).

Check APK size <40MB. If over, investigate asset bloat (likely Newtonsoft.Json is the main new weight, expected ~3MB).

- [ ] **Step 16.4: Install on Galaxy S22**

```bash
adb install -r Builds/W3-<timestamp>.apk
```

Expected: success, streamed.

- [ ] **Step 16.5: Device verification — run checklist**

Using spec §6.3 checklist, verify on-device:

- [ ] Cold start ≤3s
- [ ] First launch: calibration overlay auto-shows
- [ ] Calibration: 8 taps work, offset saves, overlay closes
- [ ] Second launch: calibration skipped (PlayerPrefs persistence)
- [ ] Für Elise Easy plays to completion
- [ ] Hold notes COMPLETED / BROKEN / MISSED observably distinct
- [ ] Hold start-tap success + hold-through: **exactly one** P/G/G scored, no Miss on top (guards against the auto-miss regression fixed in Task 7)
- [ ] CompletionPanel shows final Score + Max Combo + P/G/G/M + stars
- [ ] Restart button reloads scene, playable immediately
- [ ] 30+ seconds continuous play, no crashes
- [ ] Subjective: Perfect judgments feel reachable after calibration

Capture any issues. If the "early-release BROKEN" tuning knob from spec §4.1 becomes a real problem (e.g., every Hold breaks because the player's natural tap is too brief), log it and consider the fallback grace window before marking W3 done.

- [ ] **Step 16.6: Write completion report**

Create `docs/superpowers/reports/2026-04-20-w3-completion.md`, modeled on [2026-04-20-w2-completion.md](../reports/2026-04-20-w2-completion.md):

```markdown
# W3 Completion — 2026-04-20

## Delivered

[1-2 paragraph summary]

## Implementation summary

| Plan task | Commit | Deliverable |
|---|---|---|
| 1 | <hash> | Newtonsoft.Json package (if added) |
| 2 | <hash> | Chart data types |
| 3 | <hash> | ChartLoader.ParseJson + 10 tests |
| ...etc |

## Test count
- W2 end: 40
- W3 end: <N>
- Net change: +<N - 40>
- User-verified <N>/<N> passing in Unity Test Runner

## Verified in Editor (Play mode)
[list]

## Known limitations (deferred to W4+)
[list]

## Subjective observations (device playtest, 2026-04-20)
[APK size, cold start, issues encountered, user verdict]

## W3 completion criteria check
[Reference spec §8, check each]

## Next step
Plan 4 / W4: Main scene, song list, Results screen, Settings with Calibration re-run.
```

- [ ] **Step 16.7: Commit completion report**

```bash
cd /c/dev/unity-music && git add docs/superpowers/reports/2026-04-20-w3-completion.md && git commit -m "$(cat <<'EOF'
docs(w3): completion report - signed off after Galaxy S22 device test

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 16.8: Final state verification**

```bash
cd /c/dev/unity-music && git log --oneline | head -20 && git status
```

Expected:
- Clean working tree
- W3 commits from all tasks present
- `main` branch pointed at the final W3 completion commit

---

## Post-plan: sign-off

After Task 16, W3 is done when:
1. All EditMode tests passing (target ~66)
2. Galaxy S22 device checklist fully checked
3. W3 completion report committed
4. `main` branch carries all W3 task commits

Hand off to W4 planning only after the user confirms the device test sign-off.
