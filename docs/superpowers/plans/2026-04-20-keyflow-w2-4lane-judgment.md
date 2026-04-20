# KeyFlow W2 Implementation Plan: 4-Lane Layout + Judgment + Scoring

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pivot W1's landscape free-x-position PoC to a **portrait 4-lane Piano Tiles layout**, wire in **tap judgment** (Perfect/Great/Good/Miss per spec §5.1) and **scoring** (100만점 normalized per §5.2), using a still-hardcoded note sequence. At plan completion, pressing Play in the Unity Editor yields a runnable 4-lane rhythm game on desktop + APK on Android with score/combo/judgment HUD.

**Architecture:** Three new pure-logic units (`LaneLayout`, `JudgmentEvaluator`, `ScoreManager`) with EditMode tests; one MonoBehaviour glue component (`JudgmentSystem`) that wires `TapInputHandler` taps against pending `NoteController` instances and drives `ScoreManager`. Existing W1 W1 components (`AudioSyncManager`, `AudioSamplePool`, `LatencyMeter`, `NoteSpawner`) survive with small modifications; `GameTime.PitchToX` is deleted. `SceneBuilder` is rewritten to generate the portrait 4-lane scene via `KeyFlow → Build W2 Gameplay Scene` menu.

**Tech Stack:** Unity 6.3 LTS (6000.3.13f1), C#, .NET Standard 2.1, Unity Test Framework (NUnit EditMode), Unity UI (legacy Text), new Input System. No new packages.

**Reference spec:** [2026-04-20-keyflow-mvp-v2-4lane-design.md](../specs/2026-04-20-keyflow-mvp-v2-4lane-design.md) — §4.3 layout, §5.1 judgment windows, §5.2 scoring, §5.3 stars, §5.4 scroll formula, §5.5 lane math, §14 W1 carryover.

**Working directory:** `C:\dev\unity-music` (Windows, Git Bash).

---

## Scope & Out-of-Scope

### In scope for W2
- Portrait Orientation flip (Player Settings)
- Remove obsolete `GameTime.PitchToX` and its 3 tests
- Pure-logic: `LaneLayout`, `JudgmentEvaluator`, `ScoreManager` + EditMode tests
- Data: `Judgment` enum/struct, `Difficulty` enum
- Glue: `JudgmentSystem` MonoBehaviour (pending-note queue, tap matching, auto-miss detection)
- Modify: `NoteController` (lane-x injection, registration with JudgmentSystem), `NoteSpawner` (lane-based positioning, still hardcoded sequence), `TapInputHandler` (adds tapped-lane to OnTap event)
- Rewrite `SceneBuilder` for portrait 4-lane scene
- HUD: display score + combo + last judgment
- Play-in-editor verification + commit

### Out of scope (moved to Plan 3 / W3)
- `.kfchart` JSON loading (notes still hardcoded in NoteSpawner like W1)
- Hold note support (TAP only for W2)
- Calibration UI and persistence
- Difficulty selection UI (hardcoded to Normal for W2 testing)
- Multi-pitch piano sample playback (still just C4 per tap — v2 spec §6.2's 48-key library is W3+)

### Still hardcoded in this plan (paves way for W3)
- Note sequence: 30-note sequence like W1, but with lane assignment (lane = (noteIdx % 4))
- Difficulty: `Normal` (±60/120/180ms judgment windows)
- Pitch per tap: all taps play the same C4 sample (multi-sample library is W3)

---

## File Structure

Files created (C) / modified (M) / deleted (D) in this plan. Absolute paths under `C:\dev\unity-music\`:

```
Assets/
├─ Scripts/
│   ├─ Common/
│   │   ├─ GameTime.cs              (M)  remove PitchToX, MinPitch/MaxPitch stay
│   │   └─ LaneLayout.cs            (C)  pure lane math (LaneToX, XToLane)
│   ├─ Gameplay/
│   │   ├─ Judgment.cs              (C)  Judgment enum, JudgmentResult struct, Difficulty enum
│   │   ├─ JudgmentEvaluator.cs     (C)  pure function: deltaMs + difficulty → Judgment
│   │   ├─ JudgmentSystem.cs        (C)  MonoBehaviour glue: pending notes, tap matching, miss detect
│   │   ├─ ScoreManager.cs          (C)  score/combo/stars calc
│   │   ├─ NoteController.cs        (M)  lane-x injection; Judged() / Missed() callbacks; despawn
│   │   ├─ NoteSpawner.cs           (M)  lane-based positioning; registers note with JudgmentSystem
│   │   ├─ TapInputHandler.cs       (M)  OnTap event signature: (songTimeMs) → (songTimeMs, lane)
│   │   ├─ AudioSyncManager.cs      (unchanged)
│   │   └─ AudioSamplePool.cs       (unchanged)
│   └─ UI/
│       └─ LatencyMeter.cs          (M)  HUD now also shows score + combo + last judgment
├─ Editor/
│   └─ SceneBuilder.cs              (M)  portrait 4-lane rewrite; new ScoreManager+JudgmentSystem wiring
└─ Tests/EditMode/
    ├─ GameTimeTests.cs             (M)  remove 3 PitchToX tests
    ├─ LaneLayoutTests.cs           (C)  LaneToX, XToLane boundary + midpoint tests
    ├─ JudgmentEvaluatorTests.cs    (C)  Perfect/Great/Good/Miss boundary tests for Easy + Normal
    └─ ScoreManagerTests.cs         (C)  perNote, combo bonus, star thresholds

ProjectSettings/ProjectSettings.asset   (M)  defaultScreenOrientation: 3 (Landscape) → 1 (Portrait)
```

No package.json / manifest.json changes. No new C# asmdef (code slots into existing `KeyFlow.Runtime` and `KeyFlow.Tests.EditMode` asmdefs).

**Design notes (why these boundaries):**
- `LaneLayout`, `JudgmentEvaluator` are **pure static classes** — no MonoBehaviour, no Unity dependencies in logic — so both are fully EditMode-testable without a scene.
- `Judgment.cs` holds a narrow enum + result struct so pure-logic and MonoBehaviour both reference the same type without circular deps.
- `JudgmentSystem` is the only new MonoBehaviour; it owns the pending-note queue and is the single matcher between taps and notes. Future Hold-note support (W3) plugs in here.
- `ScoreManager` is a plain C# class (`System.Object` subclass, not MonoBehaviour) attached to nothing — `JudgmentSystem` holds a reference and calls it. This keeps scoring testable without a scene.

---

## Prerequisites (carry over from W1, verified)

- Unity 6.3 LTS + Android Build Support (installed, see W1 prerequisites)
- Galaxy S22 connected via adb (unplug/replug if needed)
- Repo clean state at `C:\dev\unity-music` on branch `main` with W1 commits through `3845479` (v2 spec review fixes)
- Unity Editor: keep closed during Task 1 (orientation flip via file edit); open after

---

## Task 1: Flip orientation + remove obsolete GameTime.PitchToX

**Files:**
- Modify: `ProjectSettings/ProjectSettings.asset:11` (defaultScreenOrientation)
- Modify: `Assets/Scripts/Common/GameTime.cs` (remove PitchToX)
- Modify: `Assets/Tests/EditMode/GameTimeTests.cs` (remove 3 PitchToX tests)

- [ ] **Step 1.1: Verify clean starting state**

```bash
cd /c/dev/unity-music && git status --short && git log --oneline | head -5
```

Expected: no uncommitted changes; HEAD at commit `3845479` or later.

- [ ] **Step 1.2: Close Unity Editor** (if open) so ProjectSettings.asset file edit is not fought by running Editor. Verify via task manager.

- [ ] **Step 1.3: Flip orientation to Portrait**

Unity YAML serializes `defaultScreenOrientation` as an integer:
- `0` = Portrait
- `1` = Portrait Upside Down
- `2` = Landscape Right
- `3` = Landscape Left
- `4` = Auto Rotation

Currently `3` (Landscape Left). Change to `0` (Portrait).

```bash
cd /c/dev/unity-music && sed -i 's/defaultScreenOrientation: 3/defaultScreenOrientation: 0/' ProjectSettings/ProjectSettings.asset && grep "defaultScreenOrientation" ProjectSettings/ProjectSettings.asset
```

Expected output: `  defaultScreenOrientation: 0`

- [ ] **Step 1.4: Remove PitchToX from GameTime**

Edit `Assets/Scripts/Common/GameTime.cs` — delete the `PitchToX` method only. `MinPitch`, `MaxPitch`, `PitchRange` constants stay (used by W3 pitch-range quartile math). `GetSongTimeMs` and `GetNoteProgress` stay.

Final GameTime.cs body:

```csharp
namespace KeyFlow
{
    public static class GameTime
    {
        public const int MinPitch = 36;
        public const int MaxPitch = 83;
        public const int PitchRange = MaxPitch - MinPitch;

        public static int GetSongTimeMs(double nowDsp, double songStartDsp, double calibOffsetSec)
        {
            double sec = nowDsp - songStartDsp - calibOffsetSec;
            return (int)(sec * 1000.0);
        }

        public static float GetNoteProgress(int songTimeMs, int hitTimeMs, int previewTimeMs)
        {
            return 1f - (float)(hitTimeMs - songTimeMs) / previewTimeMs;
        }
    }
}
```

- [ ] **Step 1.5: Remove 3 PitchToX tests from GameTimeTests.cs**

Delete these three tests in `Assets/Tests/EditMode/GameTimeTests.cs`:
- `PitchToX_MinPitch_ReturnsZero`
- `PitchToX_MaxPitch_ReturnsOne`
- `PitchToX_C4_ReturnsMidRange`

Keep the other 6 tests (all GetSongTimeMs + GetNoteProgress tests).

- [ ] **Step 1.6: Open Unity Editor and wait for compile**

Launch Unity Hub → open `unity-music` project. Unity recompiles. Watch Console for errors.

Expected: 0 errors. The `GameplayScene` may show warnings about missing components (SceneBuilder-generated scene is still W1 layout and will be rebuilt in Task 9).

- [ ] **Step 1.7: Run EditMode tests**

`Window → General → Test Runner → EditMode → Run All`.

Expected: 8/8 pass (was 11 in W1 → minus 3 PitchToX tests).

- [ ] **Step 1.8: Commit**

```bash
cd /c/dev/unity-music && git add ProjectSettings/ProjectSettings.asset Assets/Scripts/Common/GameTime.cs Assets/Tests/EditMode/GameTimeTests.cs
git commit -m "refactor(w2): flip to portrait + drop GameTime.PitchToX

v2 spec removes free-x position (MIDI pitch mapping) in favor of
discrete lanes. PitchToX is no longer used. MinPitch/MaxPitch
constants stay - W3 pitch-range quartile math still needs them.

Orientation: Landscape Left (3) -> Portrait (0).

Tests: 11 -> 8 (three PitchToX tests removed)."
```

---

## Task 2: LaneLayout (pure logic + TDD)

**Files:**
- Create: `Assets/Scripts/Common/LaneLayout.cs`
- Create: `Assets/Tests/EditMode/LaneLayoutTests.cs`

Per spec §5.5. Pure static class, no Unity dependency.

- [ ] **Step 2.1: Write failing tests**

Create `Assets/Tests/EditMode/LaneLayoutTests.cs`:

```csharp
using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests
{
    public class LaneLayoutTests
    {
        private const float Width = 4f;  // simple test width: 1 unit per lane

        [Test]
        public void LaneToX_Lane0_ReturnsLeftQuarter()
        {
            // leftEdge = -2, laneWidth = 1, center of lane 0 = -2 + 0.5 = -1.5
            Assert.AreEqual(-1.5f, LaneLayout.LaneToX(0, Width), 0.001f);
        }

        [Test]
        public void LaneToX_Lane3_ReturnsRightQuarter()
        {
            // center of lane 3 = -2 + 1*3.5 = 1.5
            Assert.AreEqual(1.5f, LaneLayout.LaneToX(3, Width), 0.001f);
        }

        [Test]
        public void LaneToX_AllLanesEquallySpaced()
        {
            float x0 = LaneLayout.LaneToX(0, Width);
            float x1 = LaneLayout.LaneToX(1, Width);
            float x2 = LaneLayout.LaneToX(2, Width);
            float x3 = LaneLayout.LaneToX(3, Width);
            Assert.AreEqual(1f, x1 - x0, 0.001f);
            Assert.AreEqual(1f, x2 - x1, 0.001f);
            Assert.AreEqual(1f, x3 - x2, 0.001f);
        }

        [Test]
        public void XToLane_NearCenter_ReturnsCorrectLane()
        {
            Assert.AreEqual(0, LaneLayout.XToLane(-1.5f, Width));
            Assert.AreEqual(1, LaneLayout.XToLane(-0.5f, Width));
            Assert.AreEqual(2, LaneLayout.XToLane(0.5f, Width));
            Assert.AreEqual(3, LaneLayout.XToLane(1.5f, Width));
        }

        [Test]
        public void XToLane_LeftOfScreen_ClampsTo0()
        {
            Assert.AreEqual(0, LaneLayout.XToLane(-999f, Width));
        }

        [Test]
        public void XToLane_RightOfScreen_ClampsTo3()
        {
            Assert.AreEqual(3, LaneLayout.XToLane(999f, Width));
        }

        [Test]
        public void XToLane_OnBoundary_UsesFloor()
        {
            // Boundary between lane 1 and lane 2 is at x = 0.0
            // FloorToInt((0 - (-2)) / 1) = 2 → lane 2
            Assert.AreEqual(2, LaneLayout.XToLane(0f, Width));
        }

        [Test]
        public void LaneCount_IsFour()
        {
            Assert.AreEqual(4, LaneLayout.LaneCount);
        }
    }
}
```

- [ ] **Step 2.2: Run tests — verify they fail**

Unity Test Runner → EditMode → Run All. Expected: 8 new failures (compile error `LaneLayout not found`).

- [ ] **Step 2.3: Implement LaneLayout**

Create `Assets/Scripts/Common/LaneLayout.cs`:

```csharp
using UnityEngine;

namespace KeyFlow
{
    public static class LaneLayout
    {
        public const int LaneCount = 4;

        public static float LaneToX(int lane, float screenWorldWidth)
        {
            float laneWidth = screenWorldWidth / LaneCount;
            float leftEdge = -screenWorldWidth / 2f;
            return leftEdge + laneWidth * (lane + 0.5f);
        }

        public static int XToLane(float x, float screenWorldWidth)
        {
            float laneWidth = screenWorldWidth / LaneCount;
            float leftEdge = -screenWorldWidth / 2f;
            int raw = Mathf.FloorToInt((x - leftEdge) / laneWidth);
            return Mathf.Clamp(raw, 0, LaneCount - 1);
        }
    }
}
```

- [ ] **Step 2.4: Run tests — verify they pass**

Expected: 16/16 total (8 GameTime + 8 LaneLayout).

- [ ] **Step 2.5: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Common/LaneLayout.cs Assets/Tests/EditMode/LaneLayoutTests.cs
git commit -m "feat(lane): pure lane math (LaneToX/XToLane) + 8 tests"
```

---

## Task 3: Judgment types (enum + result struct)

**Files:**
- Create: `Assets/Scripts/Gameplay/Judgment.cs`

Tiny file — only types, no logic. Pure C#.

- [ ] **Step 3.1: Create the types**

Create `Assets/Scripts/Gameplay/Judgment.cs`:

```csharp
namespace KeyFlow
{
    public enum Judgment
    {
        Perfect = 0,
        Great = 1,
        Good = 2,
        Miss = 3
    }

    public enum Difficulty
    {
        Easy = 0,
        Normal = 1
    }

    public readonly struct JudgmentResult
    {
        public readonly Judgment Judgment;
        public readonly int DeltaMs;        // tap time - note hit time. Negative = early.

        public JudgmentResult(Judgment j, int deltaMs)
        {
            Judgment = j;
            DeltaMs = deltaMs;
        }

        public bool IsHit => Judgment != Judgment.Miss;
    }
}
```

- [ ] **Step 3.2: Verify compile**

Save. Unity recompiles. Expected: 0 errors in Console.

- [ ] **Step 3.3: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Gameplay/Judgment.cs
git commit -m "feat(judgment): add Judgment enum + Difficulty enum + JudgmentResult struct"
```

---

## Task 4: JudgmentEvaluator (pure logic + TDD)

**Files:**
- Create: `Assets/Scripts/Gameplay/JudgmentEvaluator.cs`
- Create: `Assets/Tests/EditMode/JudgmentEvaluatorTests.cs`

Per spec §5.1. Pure static function — no time handling, just threshold math. Time source (dspTime) injected as int deltas.

- [ ] **Step 4.1: Write failing tests**

Create `Assets/Tests/EditMode/JudgmentEvaluatorTests.cs`:

```csharp
using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests
{
    public class JudgmentEvaluatorTests
    {
        // Normal: ±60 Perfect, ±120 Great, ±180 Good, beyond = Miss

        [Test]
        public void Normal_OnTime_Perfect()
        {
            var r = JudgmentEvaluator.Evaluate(0, Difficulty.Normal);
            Assert.AreEqual(Judgment.Perfect, r.Judgment);
            Assert.AreEqual(0, r.DeltaMs);
        }

        [Test]
        public void Normal_PerfectBoundary_AtPlus60_Perfect()
        {
            var r = JudgmentEvaluator.Evaluate(60, Difficulty.Normal);
            Assert.AreEqual(Judgment.Perfect, r.Judgment);
        }

        [Test]
        public void Normal_JustPastPerfect_Great()
        {
            var r = JudgmentEvaluator.Evaluate(61, Difficulty.Normal);
            Assert.AreEqual(Judgment.Great, r.Judgment);
        }

        [Test]
        public void Normal_GreatBoundary_AtPlus120_Great()
        {
            var r = JudgmentEvaluator.Evaluate(120, Difficulty.Normal);
            Assert.AreEqual(Judgment.Great, r.Judgment);
        }

        [Test]
        public void Normal_JustPastGreat_Good()
        {
            var r = JudgmentEvaluator.Evaluate(121, Difficulty.Normal);
            Assert.AreEqual(Judgment.Good, r.Judgment);
        }

        [Test]
        public void Normal_GoodBoundary_AtPlus180_Good()
        {
            var r = JudgmentEvaluator.Evaluate(180, Difficulty.Normal);
            Assert.AreEqual(Judgment.Good, r.Judgment);
        }

        [Test]
        public void Normal_JustPastGood_Miss()
        {
            var r = JudgmentEvaluator.Evaluate(181, Difficulty.Normal);
            Assert.AreEqual(Judgment.Miss, r.Judgment);
        }

        [Test]
        public void Normal_Early_SymmetricWindow()
        {
            Assert.AreEqual(Judgment.Perfect, JudgmentEvaluator.Evaluate(-60, Difficulty.Normal).Judgment);
            Assert.AreEqual(Judgment.Great,   JudgmentEvaluator.Evaluate(-61, Difficulty.Normal).Judgment);
            Assert.AreEqual(Judgment.Good,    JudgmentEvaluator.Evaluate(-121, Difficulty.Normal).Judgment);
            Assert.AreEqual(Judgment.Miss,    JudgmentEvaluator.Evaluate(-181, Difficulty.Normal).Judgment);
        }

        // Easy: ±75 Perfect, ±150 Great, ±225 Good, beyond = Miss

        [Test]
        public void Easy_PerfectBoundary_AtPlus75_Perfect()
        {
            Assert.AreEqual(Judgment.Perfect, JudgmentEvaluator.Evaluate(75, Difficulty.Easy).Judgment);
        }

        [Test]
        public void Easy_GreatBoundary_AtPlus150_Great()
        {
            Assert.AreEqual(Judgment.Great, JudgmentEvaluator.Evaluate(150, Difficulty.Easy).Judgment);
        }

        [Test]
        public void Easy_GoodBoundary_AtPlus225_Good()
        {
            Assert.AreEqual(Judgment.Good, JudgmentEvaluator.Evaluate(225, Difficulty.Easy).Judgment);
        }

        [Test]
        public void Easy_JustPastGood_Miss()
        {
            Assert.AreEqual(Judgment.Miss, JudgmentEvaluator.Evaluate(226, Difficulty.Easy).Judgment);
        }

        [Test]
        public void DeltaMs_IsEchoed()
        {
            var r = JudgmentEvaluator.Evaluate(42, Difficulty.Normal);
            Assert.AreEqual(42, r.DeltaMs);
        }
    }
}
```

- [ ] **Step 4.2: Run tests — verify they fail**

Expected: 13 new failures (compile error).

- [ ] **Step 4.3: Implement JudgmentEvaluator**

Create `Assets/Scripts/Gameplay/JudgmentEvaluator.cs`:

```csharp
namespace KeyFlow
{
    public static class JudgmentEvaluator
    {
        // Per spec §5.1
        private const int NormalPerfectMs = 60;
        private const int NormalGreatMs = 120;
        private const int NormalGoodMs = 180;
        private const int EasyPerfectMs = 75;
        private const int EasyGreatMs = 150;
        private const int EasyGoodMs = 225;

        public static JudgmentResult Evaluate(int deltaMs, Difficulty difficulty)
        {
            int abs = deltaMs < 0 ? -deltaMs : deltaMs;
            int perfect, great, good;
            if (difficulty == Difficulty.Easy)
            {
                perfect = EasyPerfectMs; great = EasyGreatMs; good = EasyGoodMs;
            }
            else
            {
                perfect = NormalPerfectMs; great = NormalGreatMs; good = NormalGoodMs;
            }

            if (abs <= perfect)     return new JudgmentResult(Judgment.Perfect, deltaMs);
            if (abs <= great)       return new JudgmentResult(Judgment.Great, deltaMs);
            if (abs <= good)        return new JudgmentResult(Judgment.Good, deltaMs);
            return new JudgmentResult(Judgment.Miss, deltaMs);
        }
    }
}
```

- [ ] **Step 4.4: Run tests — verify they pass**

Expected: 29/29 total (16 + 13).

- [ ] **Step 4.5: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Gameplay/JudgmentEvaluator.cs Assets/Tests/EditMode/JudgmentEvaluatorTests.cs
git commit -m "feat(judgment): pure JudgmentEvaluator + 13 tests (spec 5.1)"
```

---

## Task 5: ScoreManager (TDD)

**Files:**
- Create: `Assets/Scripts/Gameplay/ScoreManager.cs`
- Create: `Assets/Tests/EditMode/ScoreManagerTests.cs`

Per spec §5.2 scoring formula and §5.3 star thresholds. Plain C# class (not MonoBehaviour) — tested without a scene.

- [ ] **Step 5.1: Write failing tests**

Create `Assets/Tests/EditMode/ScoreManagerTests.cs`:

```csharp
using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests
{
    public class ScoreManagerTests
    {
        [Test]
        public void AllPerfect_YieldsOneMillion()
        {
            var mgr = new ScoreManager(totalNotes: 100);
            for (int i = 0; i < 100; i++) mgr.RegisterJudgment(Judgment.Perfect);
            Assert.AreEqual(1_000_000, mgr.Score);
            Assert.AreEqual(100, mgr.Combo);
        }

        [Test]
        public void AllMiss_YieldsZero()
        {
            var mgr = new ScoreManager(totalNotes: 100);
            for (int i = 0; i < 100; i++) mgr.RegisterJudgment(Judgment.Miss);
            Assert.AreEqual(0, mgr.Score);
            Assert.AreEqual(0, mgr.Combo);
        }

        [Test]
        public void MissResetsCombo()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Miss);
            Assert.AreEqual(0, mgr.Combo);
        }

        [Test]
        public void GoodAndGreat_PartialCredit()
        {
            var mgr = new ScoreManager(totalNotes: 100);
            for (int i = 0; i < 100; i++) mgr.RegisterJudgment(Judgment.Good);
            // 100 notes * Good (0.3): judgmentScore = 100 * (900000/100 * 0.3) = 270000
            // comboBonus = 100 * (100000/100) = 100000 (Good is still a hit)
            // total = 370000
            Assert.AreEqual(370_000, mgr.Score);
        }

        [Test]
        public void MaxComboTracks()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Miss);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Perfect);
            Assert.AreEqual(3, mgr.MaxCombo);
            Assert.AreEqual(2, mgr.Combo);
        }

        [Test]
        public void JudgmentCounts_Tracked()
        {
            var mgr = new ScoreManager(totalNotes: 4);
            mgr.RegisterJudgment(Judgment.Perfect);
            mgr.RegisterJudgment(Judgment.Great);
            mgr.RegisterJudgment(Judgment.Good);
            mgr.RegisterJudgment(Judgment.Miss);
            Assert.AreEqual(1, mgr.PerfectCount);
            Assert.AreEqual(1, mgr.GreatCount);
            Assert.AreEqual(1, mgr.GoodCount);
            Assert.AreEqual(1, mgr.MissCount);
        }

        // Star thresholds per spec §5.3
        [Test]
        public void Stars_ZeroIfUnder500k()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            for (int i = 0; i < 4; i++) mgr.RegisterJudgment(Judgment.Perfect); // ~400k
            Assert.AreEqual(0, mgr.Stars);
        }

        [Test]
        public void Stars_OneAt500k()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            for (int i = 0; i < 5; i++) mgr.RegisterJudgment(Judgment.Perfect); // 500k
            Assert.AreEqual(1, mgr.Stars);
        }

        [Test]
        public void Stars_TwoAt750k()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            for (int i = 0; i < 8; i++) mgr.RegisterJudgment(Judgment.Perfect);
            // 8/10 * 1M = 800k → 2 stars
            Assert.AreEqual(2, mgr.Stars);
        }

        [Test]
        public void Stars_ThreeAt900k()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            for (int i = 0; i < 9; i++) mgr.RegisterJudgment(Judgment.Perfect);
            // 9/10 * 1M = 900k → 3 stars
            Assert.AreEqual(3, mgr.Stars);
        }

        [Test]
        public void Accuracy_PercentageOfHits()
        {
            var mgr = new ScoreManager(totalNotes: 10);
            for (int i = 0; i < 7; i++) mgr.RegisterJudgment(Judgment.Perfect);
            for (int i = 0; i < 3; i++) mgr.RegisterJudgment(Judgment.Miss);
            // 7/10 hits = 70%
            Assert.AreEqual(70f, mgr.AccuracyPercent, 0.01f);
        }
    }
}
```

- [ ] **Step 5.2: Run tests — verify they fail**

Expected: 11 new failures.

- [ ] **Step 5.3: Implement ScoreManager**

Create `Assets/Scripts/Gameplay/ScoreManager.cs`:

```csharp
namespace KeyFlow
{
    public class ScoreManager
    {
        private readonly int totalNotes;
        private readonly int perNoteScore;
        private readonly int perNoteComboBonus;

        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int PerfectCount { get; private set; }
        public int GreatCount { get; private set; }
        public int GoodCount { get; private set; }
        public int MissCount { get; private set; }
        public int JudgedCount => PerfectCount + GreatCount + GoodCount + MissCount;

        public int Stars
        {
            get
            {
                if (Score >= 900_000) return 3;
                if (Score >= 750_000) return 2;
                if (Score >= 500_000) return 1;
                return 0;
            }
        }

        public float AccuracyPercent
        {
            get
            {
                int hits = PerfectCount + GreatCount + GoodCount;
                return JudgedCount == 0 ? 0f : 100f * hits / JudgedCount;
            }
        }

        public ScoreManager(int totalNotes)
        {
            this.totalNotes = totalNotes > 0 ? totalNotes : 1;
            perNoteScore = 900_000 / this.totalNotes;
            perNoteComboBonus = 100_000 / this.totalNotes;
        }

        public void RegisterJudgment(Judgment j)
        {
            switch (j)
            {
                case Judgment.Perfect: PerfectCount++; Score += perNoteScore; Combo++; Score += perNoteComboBonus; break;
                case Judgment.Great:   GreatCount++;   Score += (int)(perNoteScore * 0.7f); Combo++; Score += perNoteComboBonus; break;
                case Judgment.Good:    GoodCount++;    Score += (int)(perNoteScore * 0.3f); Combo++; Score += perNoteComboBonus; break;
                case Judgment.Miss:    MissCount++;    Combo = 0; break;
            }
            if (Combo > MaxCombo) MaxCombo = Combo;
        }
    }
}
```

- [ ] **Step 5.4: Run tests — verify they pass**

Expected: 40/40 total (29 + 11).

> **Note on integer truncation**: `(int)(perNoteScore * 0.7f)` can lose a tiny bit of precision per note, causing all-Great runs to land a few hundred points below 700k. This is acceptable — matches real rhythm-game scoring behavior and doesn't affect star thresholds.

- [ ] **Step 5.5: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Gameplay/ScoreManager.cs Assets/Tests/EditMode/ScoreManagerTests.cs
git commit -m "feat(score): ScoreManager (score/combo/stars) + 11 tests (spec 5.2-5.3)"
```

---

## Task 6: NoteController — lane-based + judgment hooks

**Files:**
- Modify: `Assets/Scripts/Gameplay/NoteController.cs`

The note needs to:
1. Know its lane (caller provides x)
2. Report itself as missed if it passes judgment line without being judged (despawn after Miss window elapses)
3. Accept a `MarkJudged()` call from `JudgmentSystem` to despawn with visual feedback

- [ ] **Step 6.1: Rewrite NoteController**

Replace `Assets/Scripts/Gameplay/NoteController.cs`:

```csharp
using UnityEngine;

namespace KeyFlow
{
    public class NoteController : MonoBehaviour
    {
        [SerializeField] private int previewTimeMs = 2000;

        private AudioSyncManager audioSync;
        private int hitTimeMs;
        private int lane;
        private float spawnY;
        private float judgmentY;
        private float laneX;
        private bool initialized;
        private bool judged;
        private int missGraceMs;           // time past hitTime after which we auto-miss
        private System.Action<NoteController> onAutoMiss;

        public int HitTimeMs => hitTimeMs;
        public int Lane => lane;
        public bool Judged => judged;

        public void Initialize(
            AudioSyncManager sync,
            int lane, float laneX,
            int hitMs,
            float spawnY, float judgmentY,
            int previewMs,
            int missGraceMs,
            System.Action<NoteController> onAutoMiss)
        {
            this.audioSync = sync;
            this.lane = lane;
            this.laneX = laneX;
            this.hitTimeMs = hitMs;
            this.spawnY = spawnY;
            this.judgmentY = judgmentY;
            this.previewTimeMs = previewMs;
            this.missGraceMs = missGraceMs;
            this.onAutoMiss = onAutoMiss;
            transform.position = new Vector3(laneX, spawnY, 0);
            initialized = true;
        }

        public void MarkJudged()
        {
            judged = true;
            Destroy(gameObject);
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

> `missGraceMs` is fed from the difficulty's Miss threshold (spec §5.1). Normal = 180ms. This is the cutoff past the hit time after which the note auto-misses and despawns.

- [ ] **Step 6.2: Compile check**

Save. Unity recompiles. Console: there WILL be compile errors in `NoteSpawner` because its old call to `Initialize` no longer matches. This is expected and fixed in Task 7.

- [ ] **Step 6.3: Do NOT commit yet** — next task fixes the compile error together.

---

## Task 7: NoteSpawner — lane-based, registers with JudgmentSystem

**Files:**
- Modify: `Assets/Scripts/Gameplay/NoteSpawner.cs`

Still hardcoded 30-note sequence for W2. Notes are assigned lanes via `noteIdx % 4` for variety, and all play the same C4 pitch.

- [ ] **Step 7.1: Rewrite NoteSpawner**

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
        [SerializeField] private int firstNoteHitMs = 2000;
        [SerializeField] private int noteIntervalMs = 800;
        [SerializeField] private int totalNotes = 30;
        [SerializeField] private int previewMs = 2000;
        [SerializeField] private Difficulty difficulty = Difficulty.Normal;

        private int spawnedCount;

        public int TotalNotes => totalNotes;
        public Difficulty CurrentDifficulty => difficulty;

        private void Start()
        {
            judgmentSystem.Initialize(totalNotes, difficulty);
            audioSync.StartSilentSong();
        }

        private void Update()
        {
            if (!audioSync.IsPlaying) return;
            if (spawnedCount >= totalNotes) return;

            int nextHitMs = firstNoteHitMs + spawnedCount * noteIntervalMs;
            if (audioSync.SongTimeMs >= nextHitMs - previewMs)
            {
                SpawnNote(nextHitMs, spawnedCount % LaneLayout.LaneCount);
                spawnedCount++;
            }
        }

        private void SpawnNote(int hitTimeMs, int lane)
        {
            float laneX = LaneLayout.LaneToX(lane, laneAreaWidth);
            var go = Instantiate(notePrefab);
            var ctrl = go.GetComponent<NoteController>();
            // Miss grace = difficulty's Good window (spec §5.1)
            int missMs = difficulty == Difficulty.Easy ? 225 : 180;
            ctrl.Initialize(
                audioSync, lane, laneX,
                hitTimeMs,
                spawnY, judgmentY,
                previewMs,
                missMs,
                onAutoMiss: n => judgmentSystem.HandleAutoMiss(n));
            judgmentSystem.RegisterPendingNote(ctrl);
        }
    }
}
```

> Serialized `laneAreaWidth` defaults to 4 world units, matching the judgment-line sprite scale in SceneBuilder (Task 11). Portrait camera orthographic size 8 gives ~9 × 16 world-unit viewport — 4 units of lane area leaves margins.
>
> Serialized `spawnY` (4) and `judgmentY` (-3) defaults here are placeholders — SceneBuilder (Task 11) overrides them via `SetField` to `SpawnY=6.5`, `JudgmentY=-5`. At orthoSize 8 (viewport y ∈ [-8, +8]), y=-5 puts the judgment line at (8-(-5))/16 ≈ **81% down from top**, matching spec §4.3's 80% target.

- [ ] **Step 7.2: Do NOT commit yet** — still need JudgmentSystem before anything compiles.

---

## Task 8: JudgmentSystem MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Gameplay/JudgmentSystem.cs`

The glue. Owns the pending-note queue, hooks into `TapInputHandler.OnTap`, calls `JudgmentEvaluator.Evaluate`, calls `ScoreManager.RegisterJudgment`.

- [ ] **Step 8.1: Implement JudgmentSystem**

Create `Assets/Scripts/Gameplay/JudgmentSystem.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace KeyFlow
{
    public class JudgmentSystem : MonoBehaviour
    {
        [SerializeField] private TapInputHandler tapInput;

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
            LastJudgment = Judgment.Miss;   // sentinel; UI may hide until first real event
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

        private void HandleTap(int tapTimeMs, int tapLane)
        {
            if (score == null) return;

            // Find nearest un-judged pending note in the tapped lane
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

            if (closest == null) return;   // tap in lane with no notes — silent (no ghost taps)

            var result = JudgmentEvaluator.Evaluate(closestAbsDelta * (closest.HitTimeMs > tapTimeMs ? -1 : 1), difficulty);
            if (result.Judgment == Judgment.Miss) return;   // too far from any note — treat as no-op

            score.RegisterJudgment(result.Judgment);
            LastJudgment = result.Judgment;
            LastDeltaMs = result.DeltaMs;
            pending.Remove(closest);
            closest.MarkJudged();
        }
    }
}
```

> **Tap-to-no-note behavior**: if player taps a lane with no pending note within Good window, we silently ignore (no penalty). Strict rhythm games would penalize; Piano Tiles is forgiving. This matches the casual-feel target audience.

- [ ] **Step 8.2: Do NOT commit yet** — TapInputHandler still needs the new OnLaneTap event.

---

## Task 9: TapInputHandler — emit tapped lane

**Files:**
- Modify: `Assets/Scripts/Gameplay/TapInputHandler.cs`

Add a new `OnLaneTap(int tapTimeMs, int lane)` event alongside the existing `OnTap(int tapTimeMs)`. The old `OnTap` is **preserved** so `LatencyMeter`'s existing `tapInput.OnTap += OnTap` subscription continues to work unchanged. `JudgmentSystem` subscribes to the new `OnLaneTap`. Derive lane from touch x coord via `LaneLayout.XToLane`.

- [ ] **Step 9.1: Rewrite TapInputHandler**

Replace `Assets/Scripts/Gameplay/TapInputHandler.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace KeyFlow
{
    public class TapInputHandler : MonoBehaviour
    {
        [SerializeField] private AudioSamplePool samplePool;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private float laneAreaWidth = 4f;

        // Back-compat: W1 subscribers on OnTap still work (lane defaults to 0).
        public System.Action<int> OnTap;
        // v2: tap with lane info
        public System.Action<int, int> OnLaneTap;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Update()
        {
            bool tapped = false;
            Vector2 screenPos = Vector2.zero;

            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.wasPressedThisFrame)
                    {
                        tapped = true;
                        screenPos = touch.position.ReadValue();
                        break;
                    }
                }
            }

            if (!tapped && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                tapped = true;
                screenPos = Mouse.current.position.ReadValue();
            }

            if (!tapped) return;

            samplePool.PlayOneShot();
            int songTimeMs = audioSync != null ? audioSync.SongTimeMs : 0;

            // Screen x → world x → lane
            Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10));
            int lane = LaneLayout.XToLane(world.x, laneAreaWidth);

            OnTap?.Invoke(songTimeMs);
            OnLaneTap?.Invoke(songTimeMs, lane);
        }
    }
}
```

- [ ] **Step 9.2: Compile check**

Save. Unity recompiles. Expected: 0 errors. Tasks 6-9 form a compile-unit; all files must be saved before errors clear.

- [ ] **Step 9.3: Run EditMode tests**

Expected: 40/40 still pass (no test changes; just verifying we didn't break pure-logic tests).

- [ ] **Step 9.4: Commit Tasks 6-9 together**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/Gameplay/NoteController.cs Assets/Scripts/Gameplay/NoteSpawner.cs Assets/Scripts/Gameplay/JudgmentSystem.cs Assets/Scripts/Gameplay/TapInputHandler.cs
git commit -m "feat(gameplay): 4-lane judgment + scoring glue

NoteController: lane-based x injection, auto-miss after Good window,
callback-based miss reporting, MarkJudged despawn.

NoteSpawner: still hardcoded 30-note sequence (W3 replaces with
.kfchart loader), but lane = (noteIdx % 4) for visual variety.
Registers each note with JudgmentSystem.

JudgmentSystem: owns pending-note list. Wires TapInputHandler.OnLaneTap
to JudgmentEvaluator + ScoreManager. Silent ignore on ghost taps
(taps in lanes with no notes).

TapInputHandler: OnLaneTap (songTimeMs, lane) via ScreenToWorldPoint
+ LaneLayout.XToLane. OnTap kept for back-compat with LatencyMeter.

Difficulty hardcoded Normal for W2 testing."
```

---

## Task 10: Update LatencyMeter HUD with score/combo/last judgment

**Files:**
- Modify: `Assets/Scripts/UI/LatencyMeter.cs`

Add 3 more HUD lines: current Score, current Combo, Last Judgment.

- [ ] **Step 10.1: Rewrite HUD Update body**

Edit `Assets/Scripts/UI/LatencyMeter.cs` — add a `[SerializeField] private JudgmentSystem judgmentSystem;` field and extend the `hudText.text` format:

Find this block:

```csharp
hudText.text =
    $"FPS: {fpsDisplay:F1}\n" +
    $"Frame latency: {(lastFrameLatencyMs < 0 ? "--" : lastFrameLatencyMs.ToString("F1"))} ms (not tap→audio)\n" +
    $"dspTime drift: {driftMs:F1} ms\n" +
    $"Song time: {(audioSync != null ? audioSync.SongTimeMs : 0)} ms\n" +
    $"Buffer: {AudioSettings.GetConfiguration().dspBufferSize} samples";
```

Replace with:

```csharp
string scoreLine = "Score: —";
string comboLine = "Combo: 0";
string judgLine  = "Last: —";
if (judgmentSystem != null && judgmentSystem.Score != null)
{
    var s = judgmentSystem.Score;
    scoreLine = $"Score: {s.Score:N0}  Stars: {s.Stars}";
    comboLine = $"Combo: {s.Combo}  Max: {s.MaxCombo}";
    judgLine  = $"Last: {judgmentSystem.LastJudgment}  (Δ {judgmentSystem.LastDeltaMs} ms)";
}

hudText.text =
    $"FPS: {fpsDisplay:F1}\n" +
    $"{scoreLine}\n" +
    $"{comboLine}\n" +
    $"{judgLine}\n" +
    $"dspTime drift: {driftMs:F1} ms\n" +
    $"Song time: {(audioSync != null ? audioSync.SongTimeMs : 0)} ms\n" +
    $"Buffer: {AudioSettings.GetConfiguration().dspBufferSize} samples";
```

Also add the new field near the other `[SerializeField]` lines:

```csharp
[SerializeField] private JudgmentSystem judgmentSystem;
```

- [ ] **Step 10.2: Compile check**

Save. Expected: 0 errors.

- [ ] **Step 10.3: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Scripts/UI/LatencyMeter.cs
git commit -m "feat(hud): show score + combo + last judgment in LatencyMeter"
```

---

## Task 11: SceneBuilder — portrait 4-lane rewrite

**Files:**
- Modify: `Assets/Editor/SceneBuilder.cs`

Full rewrite of the scene layout logic. Menu item still `KeyFlow → Build W1 PoC Scene`, but we rename to `Build W2 Gameplay Scene` to signal it's the v2 layout.

- [ ] **Step 11.1: Rewrite SceneBuilder**

Replace `Assets/Editor/SceneBuilder.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow.UI;

namespace KeyFlow.Editor
{
    public static class SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/GameplayScene.unity";
        private const string NotePrefabPath = "Assets/Prefabs/Note.prefab";
        private const string WhiteSpritePath = "Assets/Sprites/white.png";

        // Portrait layout (camera orthographic size 8 → 9 world-unit-wide viewport at 9:16 aspect)
        private const float LaneAreaWidth = 4f;       // world units
        private const float SpawnY = 6.5f;
        private const float JudgmentY = -5f;           // ~80% down the viewport

        [MenuItem("KeyFlow/Build W2 Gameplay Scene")]
        public static void BuildScene()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Sprites");

            var whiteSprite = EnsureWhiteSprite();
            var pianoClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/piano_c4.wav");
            if (pianoClip == null)
            {
                Debug.LogError("[KeyFlow] Missing Assets/Audio/piano_c4.wav. Aborting.");
                return;
            }

            var notePrefab = BuildNotePrefab(whiteSprite);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "GameplayScene";

            var camera = BuildMainCamera();
            BuildLaneDividers(whiteSprite);
            var judgmentLine = BuildJudgmentLine(whiteSprite);
            BuildManagers(
                camera, pianoClip, notePrefab,
                out var audioSync, out var samplePool, out var tapInput, out var judgmentSystem);
            BuildHUD(audioSync, tapInput, samplePool, judgmentSystem);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSceneManager.OpenScene(ScenePath);

            Debug.Log($"[KeyFlow] W2 4-lane portrait scene built: {ScenePath}");
        }

        private static Camera BuildMainCamera()
        {
            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 0, -10);
            var camera = cam.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cam.AddComponent<AudioListener>();
            return camera;
        }

        private static void BuildLaneDividers(Sprite sprite)
        {
            // 5 thin vertical lines at lane boundaries (-2, -1, 0, 1, 2 world-x)
            float leftEdge = -LaneAreaWidth / 2f;
            for (int i = 0; i <= LaneLayout.LaneCount; i++)
            {
                float x = leftEdge + i * (LaneAreaWidth / LaneLayout.LaneCount);
                var go = new GameObject($"LaneDivider_{i}");
                go.transform.position = new Vector3(x, 0, 0);
                go.transform.localScale = new Vector3(0.02f, 20f, 1);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(0.3f, 0.3f, 0.4f, 0.8f);
                sr.sortingOrder = -1;
            }
        }

        private static GameObject BuildJudgmentLine(Sprite sprite)
        {
            var go = new GameObject("JudgmentLine");
            go.transform.position = new Vector3(0, JudgmentY, 0);
            go.transform.localScale = new Vector3(LaneAreaWidth, 0.12f, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.2f, 0.9f, 1.0f, 1);
            sr.sortingOrder = 0;
            return go;
        }

        private static void BuildManagers(
            Camera camera,
            AudioClip pianoClip,
            GameObject notePrefab,
            out AudioSyncManager audioSync,
            out AudioSamplePool samplePool,
            out TapInputHandler tapInput,
            out JudgmentSystem judgmentSystem)
        {
            var managers = new GameObject("Managers");

            var audioSyncGO = new GameObject("AudioSync");
            audioSyncGO.transform.SetParent(managers.transform);
            audioSyncGO.AddComponent<AudioSource>();
            audioSync = audioSyncGO.AddComponent<AudioSyncManager>();

            var samplePoolGO = new GameObject("SamplePool");
            samplePoolGO.transform.SetParent(managers.transform);
            samplePool = samplePoolGO.AddComponent<AudioSamplePool>();
            SetField(samplePool, "defaultClip", pianoClip);

            var tapInputGO = new GameObject("TapInput");
            tapInputGO.transform.SetParent(managers.transform);
            tapInput = tapInputGO.AddComponent<TapInputHandler>();
            SetField(tapInput, "samplePool", samplePool);
            SetField(tapInput, "audioSync", audioSync);
            SetField(tapInput, "mainCamera", camera);
            SetField(tapInput, "laneAreaWidth", LaneAreaWidth);

            var judgmentGO = new GameObject("JudgmentSystem");
            judgmentGO.transform.SetParent(managers.transform);
            judgmentSystem = judgmentGO.AddComponent<JudgmentSystem>();
            SetField(judgmentSystem, "tapInput", tapInput);

            var spawnerGO = new GameObject("Spawner");
            spawnerGO.transform.SetParent(managers.transform);
            var spawner = spawnerGO.AddComponent<NoteSpawner>();
            SetField(spawner, "notePrefab", notePrefab);
            SetField(spawner, "audioSync", audioSync);
            SetField(spawner, "judgmentSystem", judgmentSystem);
            SetField(spawner, "laneAreaWidth", LaneAreaWidth);
            SetField(spawner, "spawnY", SpawnY);
            SetField(spawner, "judgmentY", JudgmentY);
        }

        private static void BuildHUD(
            AudioSyncManager audioSync,
            TapInputHandler tapInput,
            AudioSamplePool samplePool,
            JudgmentSystem judgmentSystem)
        {
            var canvasGO = new GameObject("HUDCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);   // portrait
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var textGO = new GameObject("HUDText");
            textGO.transform.SetParent(canvasGO.transform, false);
            var rt = textGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(20, -20);
            rt.sizeDelta = new Vector2(680, 260);

            var text = textGO.AddComponent<Text>();
            text.text = "Initializing...";
            text.fontSize = 22;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = TextAnchor.UpperLeft;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var meter = canvasGO.AddComponent<LatencyMeter>();
            SetField(meter, "hudText", text);
            SetField(meter, "audioSync", audioSync);
            SetField(meter, "tapInput", tapInput);
            SetField(meter, "samplePool", samplePool);
            SetField(meter, "judgmentSystem", judgmentSystem);
        }

        private static GameObject BuildNotePrefab(Sprite sprite)
        {
            var go = new GameObject("Note");
            go.transform.localScale = new Vector3(0.8f, 0.4f, 1);   // wider/shorter = piano-tile look
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(1f, 0.95f, 0.85f, 1);               // warm white
            sr.sortingOrder = 1;
            go.AddComponent<NoteController>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, NotePrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static Sprite EnsureWhiteSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            if (existing != null) return existing;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(WhiteSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(WhiteSpritePath);
            var importer = AssetImporter.GetAtPath(WhiteSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 4;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SetField(Object target, string name, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(name);
            if (prop == null) { Debug.LogError($"[KeyFlow] Field '{name}' not found on {target.GetType().Name}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetField(Object target, string name, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(name);
            if (prop == null) { Debug.LogError($"[KeyFlow] Field '{name}' not found on {target.GetType().Name}"); return; }
            prop.floatValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
```

- [ ] **Step 11.2: Compile check**

Save. Unity recompiles. 0 errors expected.

- [ ] **Step 11.3: Run the menu command**

`KeyFlow → Build W2 Gameplay Scene`. Console should log: `[KeyFlow] W2 4-lane portrait scene built: Assets/Scenes/GameplayScene.unity`.

Switch Scene view to orthographic (shortcut key `O` on numpad or via gizmo). Should show:
- 4 vertical lane dividers
- Cyan horizontal judgment line at y ≈ -5
- Dark blue-black background
- Managers hierarchy node with 5 children
- HUDCanvas with HUDText child

- [ ] **Step 11.4: Press Play in Editor**

Expected:
- Portrait aspect-ratio Game view (720x1280 reference)
- After ~2 seconds the first note appears at top of lane 0 and falls
- Every 800ms another note appears, cycling lanes 0→1→2→3→0...
- Click/tap anywhere in a lane to play piano + register judgment
- HUD updates: Score, Combo, Last judgment (e.g. `Last: Perfect (Δ -12 ms)`)
- Notes you ignore auto-miss; you see `Last: Miss` and combo resets

If tap in a lane with no active note → silent (intentional).

- [ ] **Step 11.5: Commit**

```bash
cd /c/dev/unity-music && git add Assets/Editor/SceneBuilder.cs Assets/Scenes/GameplayScene.unity Assets/Prefabs/Note.prefab
git commit -m "feat(scene): portrait 4-lane scene builder for W2

SceneBuilder renamed 'Build W1 PoC Scene' -> 'Build W2 Gameplay Scene'.
Rebuilds scene with:
- Camera orthographic size 8, portrait aspect
- 4 lane dividers at x = {-2, -1, 0, 1, 2}
- Judgment line cyan horizontal bar at y=-5
- Managers: AudioSync, SamplePool, TapInput, JudgmentSystem, Spawner
- HUD: portrait 720x1280 reference, 7-line LatencyMeter output
- Note prefab: wider/shorter rectangle for piano-tile aesthetic

Verified in editor: notes spawn and fall across 4 lanes,
tap matches lane, judgment + score HUD live updates."
```

---

## Task 12: Build APK and verify on Galaxy S22

**Files:**
- Create: `Builds/KeyFlow-W2-debug.apk` (gitignored)

- [ ] **Step 12.1: Verify device connection**

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe" devices -l
```

Expected: `R5CT21A31QB   device   model:SM_S901N`.

- [ ] **Step 12.2: Build APK via File → Build Profiles → Android**

In Unity: `File → Build Profiles → Android → Build`. Save path: `C:\dev\unity-music\Builds\KeyFlow-W2-debug.apk`.

Incremental build (since W1 compiled already): expected ~2-5 minutes.

- [ ] **Step 12.3: Install and launch**

```bash
ADB="/c/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
"$ADB" install -r /c/dev/unity-music/Builds/KeyFlow-W2-debug.apk
"$ADB" shell monkey -p com.funqdev.keyflow -c android.intent.category.LAUNCHER 1
```

Expected: `Success` + app launches on phone in portrait.

- [ ] **Step 12.4: Play on device — verify 4-lane rhythm gameplay**

Success criteria (all must be true):
- Portrait orientation
- 4 visible lanes with dividers
- Notes falling in sequence
- Tapping correct lane → Perfect/Great/Good per timing
- Tapping wrong lane → silent, note passes → Miss
- HUD shows Score climbing, Combo count, last judgment
- FPS holds at 58+
- No crashes after 30+ seconds of play

Record any abnormalities.

- [ ] **Step 12.5: No commit needed** (APK is gitignored) — W2 core completion is covered by the Task 11 commit.

---

## Task 13: Final verification + W2 retro note

**Files:**
- Create: `docs/superpowers/reports/2026-04-DD-w2-completion.md`

- [ ] **Step 13.1: Run all EditMode tests one more time**

Unity Test Runner → EditMode → Run All. Expected: 40/40 pass.

- [ ] **Step 13.2: Write brief W2 completion note**

Create `docs/superpowers/reports/2026-04-DD-w2-completion.md` (substitute actual date):

```markdown
# W2 Completion — <date>

## Delivered
- Portrait 4-lane layout (SceneBuilder rewritten)
- JudgmentEvaluator (pure, 13 tests, spec §5.1)
- ScoreManager (pure, 11 tests, spec §5.2-5.3)
- LaneLayout (pure, 8 tests, spec §5.5)
- JudgmentSystem (glue, manual tested)
- HUD shows score/combo/last judgment
- APK deployed + gameplay loop works on Galaxy S22

## Test count
- W1: 11 EditMode tests
- W2: 40 EditMode tests (added 32 / removed 3)

## Known limitations (deferred to W3)
- Notes still hardcoded sequence (not .kfchart loaded)
- Only C4 piano sample (not 48-key library)
- No Hold notes
- No calibration UI (offset still 0)
- No difficulty selection (Normal hardcoded)
- No scene transitions (main menu, results) — just gameplay scene

## Subjective observations (fill in from W2 playtest)
- _[device feel, any jank, tap-hit feedback quality]_

## Next step
- Plan 3 / W3 covers: chart loader + Hold notes + calibration + first song end-to-end
```

- [ ] **Step 13.3: Commit the report**

```bash
cd /c/dev/unity-music && git add docs/superpowers/reports/
git commit -m "docs(w2): completion report"
```

---

## W2 Completion Criteria

All of the following must be true before declaring W2 done:

- [ ] 40 EditMode tests pass (LaneLayout 8 + JudgmentEvaluator 13 + ScoreManager 11 + GameTime 6 + AudioSamplePool 2)
- [ ] Unity Editor shows portrait 4-lane scene, no compile errors
- [ ] Play-in-editor: tapping lanes yields P/G/G judgments, missed notes auto-miss, score climbs to ~1M on all-Perfect, combo resets on miss
- [ ] APK built, installed on Galaxy S22, gameplay runs for 30+ seconds at 58+ FPS, no crashes
- [ ] W2 completion report filled in with subjective observations
- [ ] Repo at `main` with commits from all 13 tasks

After W2 sign-off → invoke writing-plans for Plan 3 (W3: chart loader + Hold + calibration + first song completable).

---

## Out of Scope for W2 (explicit reminders)

These belong to Plan 3+ / W3+; do **not** let them creep in:

- `.kfchart` JSON load — hardcoded W2 sequence stays
- Hold notes — TAP only for W2
- Calibration UX — `CalibrationOffsetSec` stays at 0 in W2
- 48-key piano library — one sample (C4) plays for every tap
- Main menu / song list / settings screens — only GameplayScene exists
- Difficulty selection — Normal hardcoded in `NoteSpawner.CurrentDifficulty`
- Pause UI — no pause in W2
- Result screen — gameplay loops forever after last note (fix in W4)
- Particle / haptic polish — W6

Adding any of these expands W2 scope.
