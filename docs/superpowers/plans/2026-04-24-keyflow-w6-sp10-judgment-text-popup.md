# KeyFlow W6 SP10 — Judgment Text Popup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a per-judgment text popup (PERFECT / GREAT / GOOD / MISS) that spawns at the judgment line on every `JudgmentSystem.OnJudgmentFeedback` invocation, mirroring the SP4 haptic + particle fan-out pattern with a third subscriber.

**Architecture:** Extend `FeedbackDispatcher` with a third dependency — `IJudgmentTextSpawner`, implemented by a new `JudgmentTextPool` MonoBehaviour. The pool owns a `RenderMode.WorldSpace` Canvas and round-robin-cycles 12 pre-instantiated UGUI Legacy `Text` GameObjects. Each active popup runs a small `JudgmentTextPopup` MonoBehaviour that drives scale-punch + alpha-fade + y-rise over a configurable lifetime, then deactivates itself. Text color comes from 4 new `Color` fields on the existing `FeedbackPresets` ScriptableObject. No changes to `JudgmentSystem` (its event already fires for the exact set of paths we want); no changes to `HapticService` or `ParticlePool`; no new assets.

**Tech Stack:** Unity 6000.3.13f1 Android (IL2CPP, arm64-v8a), UGUI Legacy Text + `LegacyRuntime.ttf` built-in font, NUnit EditMode tests, ScriptableObject-driven presets.

**Spec:** `docs/superpowers/specs/2026-04-24-keyflow-w6-sp10-judgment-text-popup-design.md`

---

## File structure

**Created:**
- `Assets/Scripts/Feedback/IJudgmentTextSpawner.cs` — single-method interface for DI in tests
- `Assets/Scripts/Feedback/JudgmentTextPool.cs` — MonoBehaviour implementing `IJudgmentTextSpawner`; owns the world-space Canvas and the round-robin pool
- `Assets/Scripts/Feedback/JudgmentTextPopup.cs` — per-popup MonoBehaviour; lifecycle animator
- `Assets/Tests/EditMode/FeedbackPresetsTests.cs` — covers new `GetTextColor(Judgment)` method
- `Assets/Tests/EditMode/JudgmentTextPoolTests.cs` — pool Spawn / round-robin / preset-color / position tests
- `Assets/Tests/EditMode/JudgmentTextPopupTests.cs` — popup activate / lifecycle / deactivate tests
- `docs/superpowers/reports/2026-04-24-w6-sp10-judgment-text-popup-completion.md` — final report (Task 13)

**Modified:**
- `Assets/Scripts/Feedback/FeedbackPresets.cs` — add 4 `Color` fields + `Color GetTextColor(Judgment j)` method
- `Assets/ScriptableObjects/FeedbackPresets.asset` — write 4 color values (gold / cyan / green / red) via YAML edit
- `Assets/Scripts/Feedback/FeedbackDispatcher.cs` — add `textPool` SerializeField + `IJudgmentTextSpawner` runtime field; extend `Handle`; extend `SetDependenciesForTest` (append 4th parameter)
- `Assets/Tests/EditMode/FeedbackDispatcherTests.cs` — update `Build()` helper to match new `SetDependenciesForTest` signature; add `FakeTextSpawner`; add 2 new tests
- `Assets/Editor/SceneBuilder.cs` — `BuildFeedbackPipeline` constructs `JudgmentTextCanvas` + `JudgmentTextPool` + 12 popup GameObjects, wires into dispatcher
- `Assets/Editor/ApkBuilder.cs` — bump release output to `keyflow-w6-sp10.apk` (profile output also bumped)
- `.gitignore` — append 5 generated-path patterns
- `Assets/Scenes/GameplayScene.unity` — regenerated via `KeyFlow/Build W4 Scene` menu after SceneBuilder change

**Potentially deleted (Task 12, conditional on pytest):**
- `tools/midi_to_kfchart/truncate_charts.py` — delete if pytest stays green without it

**NOT modified (guardrails):**
- `Assets/Scripts/Gameplay/JudgmentSystem.cs` — `OnJudgmentFeedback` event and its 3 call sites unchanged
- `Assets/Scripts/Feedback/HapticService.cs` — SP4 contract unchanged
- `Assets/Scripts/Feedback/ParticlePool.cs` — SP4 contract unchanged
- `Assets/Scripts/Feedback/LaneGlowController.cs` — unrelated subsystem
- Existing 163 EditMode tests — additive changes only (FeedbackDispatcherTests gets Build() helper tweak but no test-level contract change)

---

## Execution prerequisite

Before starting Task 1, the executor should be on a dedicated branch, either a standard Git branch or a `.claude/worktrees/` worktree. The repo's recent pattern is `merge: W6 SP<N>` squash-merges from feature branches. Suggested name: `w6-sp10-judgment-text-popup`. All commits in this plan land on that branch; Task 13 merges back.

**Unity Editor must be CLOSED** on this project before running any `Unity.exe -batchmode` step (SP3 discovery: IL2CPP link fails at step 1101/1110 if an interactive Editor has the project open at the same time). Re-open the Editor after the batch command finishes if needed for inspector edits.

---

## Task 1: Housekeeping — .gitignore + stale worktree cleanup

Bundled first because they're zero-risk and their presence distracts `git status` during TDD iteration.

**Files:**
- Modify: `.gitignore`

- [ ] **Step 1: Append 5 ignore patterns to `.gitignore`**

Append to the end of `.gitignore` (after the existing `tools/midi_to_kfchart/**/__pycache__/` line):

```
# Test runner + profiler outputs
TestResults-*.xml
/.test-results/
/test-results/
/ProfilerCaptures/
/img/
```

The 3 xml/result patterns match the untracked files currently in `git status`. `ProfilerCaptures/` matches SP3 workflow artifacts. `img/` matches SP9 supplied assets that live outside `Assets/`.

- [ ] **Step 2: Verify `git status` now shows only tracked-change candidates**

Run:
```bash
git status
```

Expected: `.gitignore` shows as modified; the previously-listed `TestResults-*.xml`, `.test-results/`, `ProfilerCaptures/`, `img/`, `test-results/` entries under "Untracked files" have disappeared. If any of those 5 still show, adjust the pattern (e.g., path depth mismatch).

- [ ] **Step 3: Enumerate stale `.claude/worktrees/` directories**

Run:
```bash
git worktree list
ls .claude/worktrees/
```

Compare: directory names present under `.claude/worktrees/` that are **not** listed by `git worktree list` are stale. (As of this plan's write time these are: `ecstatic-franklin-a7436a`, `pensive-turing-5827ba`, `suspicious-hodgkin-52399d`, `w6-sp8-hold-note-polish`, `w6-sp9-profile-start-screen` — 5 entries. Verify at execution time; do not rely on this list without re-checking.)

- [ ] **Step 4: Delete stale directories one by one with confirmation**

For each stale name `N`:
```bash
rm -rf ".claude/worktrees/N"
```

(Use `rm -rf` rather than Windows `rmdir /s /q` — bash shell is in use; `rm -rf` handles long paths via the Cygwin/Git-Bash shim. If a single `rm -rf` still fails on MAX_PATH, fall back to `cmd.exe /c "rmdir /s /q .claude\\worktrees\\N"`.)

- [ ] **Step 5: Verify `git worktree list` still shows the same registered worktrees**

```bash
git worktree list
```

Expected: output unchanged from Step 3 (only registered worktrees remain; stale dirs are now gone).

- [ ] **Step 6: Commit**

```bash
git add .gitignore
git commit -m "chore(w6-sp10): .gitignore test/profiler outputs + prune stale worktree residue"
```

Note: the stale-dir deletions are not git-tracked (they were under `.claude/` which is already ignored). Only `.gitignore` is staged.

---

## Task 2: `FeedbackPresets` text-color extension (TDD)

Adds 4 `Color` fields + `GetTextColor(Judgment)` method. Pure data change; independent of any MonoBehaviour; perfect first TDD task.

**Files:**
- Create: `Assets/Tests/EditMode/FeedbackPresetsTests.cs`
- Modify: `Assets/Scripts/Feedback/FeedbackPresets.cs`

- [ ] **Step 1: Write failing tests**

Create `Assets/Tests/EditMode/FeedbackPresetsTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.Feedback;

namespace KeyFlow.Tests.EditMode
{
    public class FeedbackPresetsTests
    {
        private static FeedbackPresets MakePresets()
        {
            var presets = ScriptableObject.CreateInstance<FeedbackPresets>();
            // Assign 4 distinct colors so the returned value is identifiable.
            presets.perfectTextColor = new Color(1f, 0.84f, 0f, 1f);   // gold
            presets.greatTextColor   = new Color(0.31f, 0.76f, 1f, 1f); // cyan
            presets.goodTextColor    = new Color(0.5f, 0.87f, 0.5f, 1f); // green
            presets.missTextColor    = new Color(1f, 0.25f, 0.25f, 1f); // red
            return presets;
        }

        [Test]
        public void GetTextColor_ReturnsPerfect_ForPerfect()
        {
            var p = MakePresets();
            Assert.AreEqual(new Color(1f, 0.84f, 0f, 1f), p.GetTextColor(Judgment.Perfect));
            Object.DestroyImmediate(p);
        }

        [Test]
        public void GetTextColor_ReturnsGreat_ForGreat()
        {
            var p = MakePresets();
            Assert.AreEqual(new Color(0.31f, 0.76f, 1f, 1f), p.GetTextColor(Judgment.Great));
            Object.DestroyImmediate(p);
        }

        [Test]
        public void GetTextColor_ReturnsGood_ForGood()
        {
            var p = MakePresets();
            Assert.AreEqual(new Color(0.5f, 0.87f, 0.5f, 1f), p.GetTextColor(Judgment.Good));
            Object.DestroyImmediate(p);
        }

        [Test]
        public void GetTextColor_ReturnsMiss_ForMiss()
        {
            var p = MakePresets();
            Assert.AreEqual(new Color(1f, 0.25f, 0.25f, 1f), p.GetTextColor(Judgment.Miss));
            Object.DestroyImmediate(p);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:
```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

Do NOT pass `-quit` (documented project quirk — `-quit` + `-runTests` skips the runner).
Unity Editor must be closed on this project first.

Expected: 4 new tests **fail** with "no definition for perfectTextColor" / "GetTextColor not found" — compilation error level, because fields don't exist.

- [ ] **Step 3: Implement the extension in `FeedbackPresets.cs`**

Modify `Assets/Scripts/Feedback/FeedbackPresets.cs`. Append 4 Color fields below the existing `[Header("Particles ...")]` block (before the existing `GetHaptic` method), and a `GetTextColor` method after `GetParticle`:

```csharp
[Header("Text popup colors (SP10)")]
public Color perfectTextColor = new Color(1f, 0.84f, 0f, 1f);      // #FFD700 gold
public Color greatTextColor   = new Color(0.31f, 0.76f, 1f, 1f);    // #4FC3FF cyan
public Color goodTextColor    = new Color(0.5f, 0.87f, 0.5f, 1f);   // #7FDF7F green
public Color missTextColor    = new Color(1f, 0.25f, 0.25f, 1f);    // #FF4040 red
```

Add after `GetParticle`:

```csharp
public Color GetTextColor(Judgment j) => j switch
{
    Judgment.Perfect => perfectTextColor,
    Judgment.Great   => greatTextColor,
    Judgment.Good    => goodTextColor,
    _                => missTextColor,
};
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the batch command from Step 2. Expected: 4 new tests PASS. Existing 163 tests still PASS. Total: 167.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Feedback/FeedbackPresets.cs Assets/Tests/EditMode/FeedbackPresetsTests.cs
git commit -m "feat(w6-sp10): FeedbackPresets.GetTextColor(Judgment) + 4 color defaults"
```

---

## Task 3: `IJudgmentTextSpawner` interface

Trivial contract file, no tests (mirrors `IHapticService.cs` / `IParticleSpawner.cs` — those landed in SP4 without dedicated tests).

**Files:**
- Create: `Assets/Scripts/Feedback/IJudgmentTextSpawner.cs`

- [ ] **Step 1: Create the interface**

`Assets/Scripts/Feedback/IJudgmentTextSpawner.cs`:

```csharp
using UnityEngine;

namespace KeyFlow.Feedback
{
    public interface IJudgmentTextSpawner
    {
        void Spawn(Judgment judgment, Vector3 worldPos);
    }
}
```

- [ ] **Step 2: Verify compilation (no test run needed)**

Open Unity Editor briefly (or run the EditMode batch again). Expected: compile succeeds, no test count change.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Feedback/IJudgmentTextSpawner.cs
git commit -m "feat(w6-sp10): IJudgmentTextSpawner interface (DI for text popup)"
```

---

## Task 4: `JudgmentTextPopup` lifecycle animator (TDD)

The per-instance animator. Deterministic via `TickForTest(float simulatedTime)` — we pass simulated time rather than depending on `Time.time`, so tests don't need coroutine or frame simulation. This mirrors the `LaneGlowController.TickForTest()` pattern but accepts a time parameter because the popup reads elapsed time, not just per-frame delta.

**Files:**
- Create: `Assets/Scripts/Feedback/JudgmentTextPopup.cs`
- Create: `Assets/Tests/EditMode/JudgmentTextPopupTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Assets/Tests/EditMode/JudgmentTextPopupTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow;
using KeyFlow.Feedback;

namespace KeyFlow.Tests.EditMode
{
    public class JudgmentTextPopupTests
    {
        private static (GameObject go, Text text, JudgmentTextPopup popup) MakePopup()
        {
            var go = new GameObject("popup");
            go.AddComponent<RectTransform>();
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var popup = go.AddComponent<JudgmentTextPopup>();
            go.SetActive(false);
            return (go, text, popup);
        }

        [Test]
        public void Activate_SetsGameObjectActive()
        {
            var (go, text, popup) = MakePopup();
            popup.Activate(startTime: 0f, lifetime: 0.45f, yRiseUnits: 0.36f, color: Color.red);
            Assert.IsTrue(go.activeSelf);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TickForTest_BeforeLifetimeEnd_StaysActive()
        {
            var (go, text, popup) = MakePopup();
            popup.Activate(startTime: 0f, lifetime: 0.45f, yRiseUnits: 0.36f, color: Color.red);
            popup.TickForTest(simulatedTime: 0.3f);
            Assert.IsTrue(go.activeSelf);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TickForTest_AfterLifetime_DeactivatesGameObject()
        {
            var (go, text, popup) = MakePopup();
            popup.Activate(startTime: 0f, lifetime: 0.45f, yRiseUnits: 0.36f, color: Color.red);
            popup.TickForTest(simulatedTime: 0.5f);
            Assert.IsFalse(go.activeSelf);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TickForTest_InPunchPhase_ScaleGreaterThanOne()
        {
            var (go, text, popup) = MakePopup();
            popup.Activate(startTime: 0f, lifetime: 0.45f, yRiseUnits: 0.36f, color: Color.red);
            popup.TickForTest(simulatedTime: 0.03f); // ~6.6% into lifetime, well inside the 0-22% punch window
            Assert.Greater(go.transform.localScale.x, 1.0f);
            Assert.Less(go.transform.localScale.x, 1.3001f);
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode batch command. Expected: 4 new tests **fail** with "JudgmentTextPopup not found" (compile error).

- [ ] **Step 3: Implement `JudgmentTextPopup`**

Create `Assets/Scripts/Feedback/JudgmentTextPopup.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.Feedback
{
    // Per-popup lifecycle animator. Pool pre-instantiates 12 GameObjects each
    // with this component + a Text component; JudgmentTextPool calls Activate
    // and the popup drives its own scale-punch, y-rise, and alpha-fade until
    // t >= 1 at which point it self-deactivates.
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Text))]
    public class JudgmentTextPopup : MonoBehaviour
    {
        private const float PunchEndT = 0.22f;   // scale returns to 1.0 by 22% of lifetime
        private const float FadeStartT = 0.55f;  // alpha starts fading at 55% of lifetime
        private const float PunchPeak = 1.3f;    // initial scale at t=0

        private Text text;
        private RectTransform rt;
        private float startTime;
        private float lifetime;
        private float yRiseUnits;
        private Color baseColor;
        private float baseLocalY;

        private void Awake()
        {
            text = GetComponent<Text>();
            rt = GetComponent<RectTransform>();
        }

        // Called by the pool. `startTime` is `Time.time` in production; tests
        // pass 0 and use TickForTest with a simulated time.
        public void Activate(float startTime, float lifetime, float yRiseUnits, Color color)
        {
            // Awake only runs on active GameObjects; tests may call Activate
            // before the first implicit enable. Lazy-init here as a safety net.
            if (text == null) text = GetComponent<Text>();
            if (rt == null) rt = GetComponent<RectTransform>();

            this.startTime = startTime;
            this.lifetime = lifetime;
            this.yRiseUnits = yRiseUnits;
            this.baseColor = color;
            this.baseLocalY = rt.anchoredPosition.y;

            text.color = color;
            rt.localScale = Vector3.one * PunchPeak;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        private void Update()
        {
            Tick(Time.time);
        }

        private void Tick(float now)
        {
            float elapsed = now - startTime;
            if (elapsed <= 0f) return;

            float t = elapsed / lifetime;
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }

            // Scale punch: 1.3 -> 1.0 across first 22% of lifetime, then steady.
            float scale = t < PunchEndT
                ? Mathf.Lerp(PunchPeak, 1.0f, t / PunchEndT)
                : 1.0f;
            rt.localScale = new Vector3(scale, scale, 1f);

            // Y rise: linear across full lifetime.
            float yOffset = yRiseUnits * t;
            var ap = rt.anchoredPosition;
            ap.y = baseLocalY + yOffset;
            rt.anchoredPosition = ap;

            // Alpha fade: 1.0 -> 0.0 across last 45% of lifetime.
            float alpha = t < FadeStartT
                ? 1.0f
                : Mathf.Lerp(1.0f, 0.0f, (t - FadeStartT) / (1f - FadeStartT));
            var c = baseColor;
            c.a = alpha;
            text.color = c;
        }

        // Test hook. Drives the same Tick path that Update calls in play mode,
        // but with a caller-supplied simulated time so EditMode tests don't
        // depend on Time.time progression.
        internal void TickForTest(float simulatedTime) => Tick(simulatedTime);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the batch command. Expected: 4 new tests PASS. Existing 167 (after Task 2) still PASS. Total: 171.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Feedback/JudgmentTextPopup.cs Assets/Tests/EditMode/JudgmentTextPopupTests.cs
git commit -m "feat(w6-sp10): JudgmentTextPopup lifecycle animator (scale punch + y rise + alpha fade)"
```

---

## Task 5: `JudgmentTextPool` round-robin pool (TDD)

The pool owns the world-space Canvas and 12 child GameObjects. Each Spawn picks the next slot, sets `text.text` + `text.color` + `rt.anchoredPosition`, then calls `popup.Activate(Time.time, lifetime, yRise, color)`. Position rule: `rt.anchoredPosition = (worldPos.x → canvas-local x, 0f)`. Since the canvas is positioned at `(0, judgmentY, 0)` in world, canvas-local `y=0` corresponds to the judgment line.

**Files:**
- Create: `Assets/Scripts/Feedback/JudgmentTextPool.cs`
- Create: `Assets/Tests/EditMode/JudgmentTextPoolTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Assets/Tests/EditMode/JudgmentTextPoolTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow;
using KeyFlow.Feedback;

namespace KeyFlow.Tests.EditMode
{
    public class JudgmentTextPoolTests
    {
        private static (GameObject root, JudgmentTextPool pool, FeedbackPresets presets)
            BuildPool(int size = 12)
        {
            var root = new GameObject("textCanvas");
            root.AddComponent<RectTransform>();
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var presets = ScriptableObject.CreateInstance<FeedbackPresets>();
            presets.perfectTextColor = new Color(1f, 0f, 0f, 1f);
            presets.greatTextColor   = new Color(0f, 1f, 0f, 1f);
            presets.goodTextColor    = new Color(0f, 0f, 1f, 1f);
            presets.missTextColor    = new Color(1f, 1f, 0f, 1f);

            var pool = root.AddComponent<JudgmentTextPool>();
            pool.InitializeForTest(presets, poolSize: size, lifetimeSec: 0.45f,
                                   yRiseUnits: 0.36f, fontSize: 48, worldCanvasScale: 1f);
            return (root, pool, presets);
        }

        [Test]
        public void Spawn_FirstCall_ActivatesIndexZero()
        {
            var (root, pool, presets) = BuildPool();
            pool.Spawn(Judgment.Perfect, Vector3.zero);
            Assert.IsTrue(pool.GetSlotForTest(0).activeSelf);
            for (int i = 1; i < 12; i++)
                Assert.IsFalse(pool.GetSlotForTest(i).activeSelf, $"slot {i} should be inactive");
            Object.DestroyImmediate(root); Object.DestroyImmediate(presets);
        }

        [Test]
        public void Spawn_RoundRobin_CyclesThroughPool()
        {
            var (root, pool, presets) = BuildPool();
            for (int i = 0; i < 12; i++) pool.Spawn(Judgment.Perfect, Vector3.zero);
            // After 12 spawns every slot should have been touched at least once.
            for (int i = 0; i < 12; i++)
                Assert.IsTrue(pool.GetSlotForTest(i).activeSelf, $"slot {i} should be active after full cycle");
            // 13th spawn wraps back to slot 0 — verify by re-reading slot 0's text
            // after setting it to a distinguishable value.
            pool.Spawn(Judgment.Miss, new Vector3(5f, 0f, 0f));
            var slot0Text = pool.GetSlotForTest(0).GetComponent<Text>();
            Assert.AreEqual("MISS", slot0Text.text);
            Object.DestroyImmediate(root); Object.DestroyImmediate(presets);
        }

        [Test]
        public void Spawn_AppliesPresetColorPerJudgment()
        {
            var (root, pool, presets) = BuildPool();
            pool.Spawn(Judgment.Perfect, Vector3.zero);
            pool.Spawn(Judgment.Great, Vector3.zero);
            pool.Spawn(Judgment.Good, Vector3.zero);
            pool.Spawn(Judgment.Miss, Vector3.zero);
            Assert.AreEqual(new Color(1f, 0f, 0f, 1f), pool.GetSlotForTest(0).GetComponent<Text>().color);
            Assert.AreEqual(new Color(0f, 1f, 0f, 1f), pool.GetSlotForTest(1).GetComponent<Text>().color);
            Assert.AreEqual(new Color(0f, 0f, 1f, 1f), pool.GetSlotForTest(2).GetComponent<Text>().color);
            Assert.AreEqual(new Color(1f, 1f, 0f, 1f), pool.GetSlotForTest(3).GetComponent<Text>().color);
            Object.DestroyImmediate(root); Object.DestroyImmediate(presets);
        }

        [Test]
        public void Spawn_SetsTextString_MatchesJudgment()
        {
            var (root, pool, presets) = BuildPool();
            pool.Spawn(Judgment.Perfect, Vector3.zero);
            pool.Spawn(Judgment.Great, Vector3.zero);
            pool.Spawn(Judgment.Good, Vector3.zero);
            pool.Spawn(Judgment.Miss, Vector3.zero);
            Assert.AreEqual("PERFECT", pool.GetSlotForTest(0).GetComponent<Text>().text);
            Assert.AreEqual("GREAT",   pool.GetSlotForTest(1).GetComponent<Text>().text);
            Assert.AreEqual("GOOD",    pool.GetSlotForTest(2).GetComponent<Text>().text);
            Assert.AreEqual("MISS",    pool.GetSlotForTest(3).GetComponent<Text>().text);
            Object.DestroyImmediate(root); Object.DestroyImmediate(presets);
        }

        [Test]
        public void Spawn_PlacesXAtWorldPosX_YAtJudgmentLineLocalZero()
        {
            var (root, pool, presets) = BuildPool();
            pool.Spawn(Judgment.Perfect, new Vector3(2.5f, 99f, 0f));
            var rt = pool.GetSlotForTest(0).GetComponent<RectTransform>();
            Assert.AreEqual(2.5f, rt.anchoredPosition.x, 0.0001f);
            Assert.AreEqual(0f, rt.anchoredPosition.y, 0.0001f, "y is judgment-line-local, independent of worldPos.y");
            Object.DestroyImmediate(root); Object.DestroyImmediate(presets);
        }

        [Test]
        public void Spawn_Miss_UsesJudgmentLineY_NotWorldPosY()
        {
            // Specifically covers the Miss path where worldPos may be far from judgment line
            // (e.g., note above line, or note expired past line).
            var (root, pool, presets) = BuildPool();
            pool.Spawn(Judgment.Miss, new Vector3(-1.5f, 50f, 0f));
            var rt = pool.GetSlotForTest(0).GetComponent<RectTransform>();
            Assert.AreEqual(-1.5f, rt.anchoredPosition.x, 0.0001f);
            Assert.AreEqual(0f, rt.anchoredPosition.y, 0.0001f);
            Object.DestroyImmediate(root); Object.DestroyImmediate(presets);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the batch command. Expected: 6 new tests **fail** with "JudgmentTextPool not found" (compile error).

- [ ] **Step 3: Implement `JudgmentTextPool`**

Create `Assets/Scripts/Feedback/JudgmentTextPool.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.Feedback
{
    public class JudgmentTextPool : MonoBehaviour, IJudgmentTextSpawner
    {
        private static readonly string PerfectStr = "PERFECT";
        private static readonly string GreatStr   = "GREAT";
        private static readonly string GoodStr    = "GOOD";
        private static readonly string MissStr    = "MISS";

        [SerializeField] private FeedbackPresets presets;
        [SerializeField] private int poolSize = 12;
        [SerializeField] private float lifetimeSec = 0.45f;
        [SerializeField] private float yRiseUnits = 0.36f;
        [SerializeField] private int fontSize = 48;
        [SerializeField] private float worldCanvasScale = 0.01f;

        private GameObject[] slots;
        private Text[] texts;
        private RectTransform[] rects;
        private JudgmentTextPopup[] popups;
        private int nextIndex;
        private bool ready;

        private void Awake()
        {
            if (!ready) BuildPool();
        }

        private void BuildPool()
        {
            if (presets == null)
                Debug.LogError("JudgmentTextPool: presets unassigned — spawns will use default colors.");

            slots = new GameObject[poolSize];
            texts = new Text[poolSize];
            rects = new RectTransform[poolSize];
            popups = new JudgmentTextPopup[poolSize];

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"JudgmentText_{i}");
                go.transform.SetParent(transform, worldPositionStays: false);

                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(400, 120);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;

                var t = go.AddComponent<Text>();
                t.text = string.Empty;
                t.font = font;
                t.fontSize = fontSize;
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                t.fontStyle = FontStyle.Bold;

                var popup = go.AddComponent<JudgmentTextPopup>();

                go.SetActive(false);

                slots[i] = go;
                texts[i] = t;
                rects[i] = rt;
                popups[i] = popup;
            }

            ready = true;
        }

        public void Spawn(Judgment judgment, Vector3 worldPos)
        {
            if (!ready) BuildPool();

            int idx = nextIndex;
            nextIndex = (nextIndex + 1) % poolSize;

            texts[idx].text = LookupString(judgment);
            Color color = presets != null
                ? presets.GetTextColor(judgment)
                : Color.white;

            // Canvas sits at (0, judgmentY, 0) in world; canvas-local y=0 is the
            // judgment line. We use worldPos.x as canvas-local x (world units),
            // and fix y at 0 regardless of the worldPos.y the caller provided.
            rects[idx].anchoredPosition = new Vector2(worldPos.x, 0f);

            popups[idx].Activate(Time.time, lifetimeSec, yRiseUnits, color);
        }

        private static string LookupString(Judgment j) => j switch
        {
            Judgment.Perfect => PerfectStr,
            Judgment.Great   => GreatStr,
            Judgment.Good    => GoodStr,
            _                => MissStr,
        };

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        // Test hooks.
        internal void InitializeForTest(
            FeedbackPresets presets, int poolSize, float lifetimeSec,
            float yRiseUnits, int fontSize, float worldCanvasScale)
        {
            this.presets = presets;
            this.poolSize = poolSize;
            this.lifetimeSec = lifetimeSec;
            this.yRiseUnits = yRiseUnits;
            this.fontSize = fontSize;
            this.worldCanvasScale = worldCanvasScale;
            BuildPool();
        }

        internal GameObject GetSlotForTest(int index) => slots[index];
#endif
    }
}
```

Note the `worldCanvasScale` SerializeField is passed to `InitializeForTest` but isn't applied to any transform inside the pool — it exists on the pool as a Inspector-tunable value that **SceneBuilder** uses when it sets `transform.localScale` on the parent canvas GameObject. Within the pool's own logic, canvas-local positions are used directly. Tests pass `1.0f` to disambiguate intent.

- [ ] **Step 4: Run tests to verify they pass**

Re-run the batch command. Expected: 6 new tests PASS. Total now: 171 + 6 = 177. (Exceeds the spec's "~175" estimate; spec language allows it.)

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Feedback/JudgmentTextPool.cs Assets/Tests/EditMode/JudgmentTextPoolTests.cs
git commit -m "feat(w6-sp10): JudgmentTextPool round-robin pool (12 slots, world-space canvas)"
```

---

## Task 6: `FeedbackDispatcher` extension (TDD)

Append `IJudgmentTextSpawner` to the dispatcher's fan-out. `SetDependenciesForTest` gains a 4th parameter; existing `FeedbackDispatcherTests` helper updated to match.

**Files:**
- Modify: `Assets/Scripts/Feedback/FeedbackDispatcher.cs`
- Modify: `Assets/Tests/EditMode/FeedbackDispatcherTests.cs`

- [ ] **Step 1: Update `FeedbackDispatcherTests` to add a `FakeTextSpawner` + 2 new tests**

Modify `Assets/Tests/EditMode/FeedbackDispatcherTests.cs`:

Add the fake near the other two fakes (inside the `FeedbackDispatcherTests` class):

```csharp
private class FakeTextSpawner : IJudgmentTextSpawner
{
    public readonly List<(Judgment j, Vector3 p)> calls = new();
    public void Spawn(Judgment j, Vector3 p) => calls.Add((j, p));
}
```

Update the `Build()` helper to return a 5-tuple and construct a `FakeTextSpawner`:

```csharp
private static (JudgmentSystem js, FeedbackDispatcher d, FakeHaptics h, FakeParticles p, FakeTextSpawner t)
    Build()
{
    var jsGo = new GameObject("judgment");
    var js = jsGo.AddComponent<JudgmentSystem>();
    js.Initialize(totalNotes: 4, difficulty: Difficulty.Normal);

    var dGo = new GameObject("dispatcher");
    var d = dGo.AddComponent<FeedbackDispatcher>();
    var h = new FakeHaptics();
    var p = new FakeParticles();
    var t = new FakeTextSpawner();
    d.SetDependenciesForTest(js, h, p, t);
    return (js, d, h, p, t);
}
```

Update each existing test's tuple destructure (`var (js, d, h, p) = Build();` → `var (js, d, h, p, t) = Build();`) in all 5 existing tests. The existing tests do not need behavioral changes — they only pass the new argument through.

Append 2 new tests at the end of the class (before the `MakeNoteAt` helper):

```csharp
[Test]
public void Handle_InvokesTextPool()
{
    UserPrefs.HapticsEnabled = true;
    var (js, d, h, p, t) = Build();

    js.HandleAutoMiss(MakeNoteAt(new Vector3(1.5f, 2.5f, 0f)));

    Assert.AreEqual(1, t.calls.Count);
    Assert.AreEqual(Judgment.Miss, t.calls[0].j);
    Assert.AreEqual(new Vector3(1.5f, 2.5f, 0f), t.calls[0].p);

    Object.DestroyImmediate(d.gameObject);
    Object.DestroyImmediate(js.gameObject);
}

[Test]
public void Handle_ForwardsMultipleEventsToTextPool()
{
    // Proves the dispatcher forwards each event individually, not that it
    // batches or deduplicates. Fires 4× Miss via HandleAutoMiss (the
    // easiest-to-produce deterministic event in EditMode). Per-judgment-kind
    // forwarding is covered by JudgmentSystemTests
    // (HandleTap_FiresFeedbackEvent_OnPerfect) + the dispatcher's
    // treat-judgment-as-opaque-parameter behavior.
    UserPrefs.HapticsEnabled = true;
    var (js, d, h, p, t) = Build();

    js.HandleAutoMiss(MakeNoteAt(Vector3.zero));
    js.HandleAutoMiss(MakeNoteAt(Vector3.zero));
    js.HandleAutoMiss(MakeNoteAt(Vector3.zero));
    js.HandleAutoMiss(MakeNoteAt(Vector3.zero));

    Assert.AreEqual(4, t.calls.Count);
    foreach (var c in t.calls) Assert.AreEqual(Judgment.Miss, c.j);

    Object.DestroyImmediate(d.gameObject);
    Object.DestroyImmediate(js.gameObject);
}
```

- [ ] **Step 2: Run tests to verify the existing tests now fail to compile**

Run the batch command. Expected: compile error on existing tests' tuple destructure (now expects 4-arg `SetDependenciesForTest` but `FeedbackDispatcher` still defines 3-arg). No tests execute yet.

- [ ] **Step 3: Implement the extension in `FeedbackDispatcher`**

Modify `Assets/Scripts/Feedback/FeedbackDispatcher.cs`:

```csharp
using UnityEngine;

namespace KeyFlow.Feedback
{
    public class FeedbackDispatcher : MonoBehaviour
    {
        [SerializeField] private JudgmentSystem judgmentSystem;
        [SerializeField] private HapticService hapticService;
        [SerializeField] private ParticlePool particlePool;
        [SerializeField] private JudgmentTextPool textPool;

        private IHapticService haptic;
        private IParticleSpawner particles;
        private IJudgmentTextSpawner textSpawner;

        private void Awake()
        {
            if (haptic == null) haptic = hapticService;
            if (particles == null) particles = particlePool;
            if (textSpawner == null) textSpawner = textPool;
        }

        private void OnEnable()
        {
            if (judgmentSystem == null)
            {
                Debug.LogError("FeedbackDispatcher: judgmentSystem unassigned.");
                return;
            }
            judgmentSystem.OnJudgmentFeedback += Handle;
        }

        private void OnDisable()
        {
            if (judgmentSystem != null) judgmentSystem.OnJudgmentFeedback -= Handle;
        }

        private void Handle(Judgment j, Vector3 worldPos)
        {
            if (UserPrefs.HapticsEnabled && haptic != null) haptic.Fire(j);
            if (particles != null) particles.Spawn(j, worldPos);
            if (textSpawner != null) textSpawner.Spawn(j, worldPos);
        }

        internal void SetDependenciesForTest(
            JudgmentSystem js, IHapticService h, IParticleSpawner p, IJudgmentTextSpawner t)
        {
            if (judgmentSystem != null) judgmentSystem.OnJudgmentFeedback -= Handle;
            judgmentSystem = js;
            haptic = h;
            particles = p;
            textSpawner = t;
            if (judgmentSystem != null) judgmentSystem.OnJudgmentFeedback += Handle;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify everything passes**

Re-run the batch command. Expected: compile succeeds; all 177 + 2 = 179 tests PASS. (Exceeds the spec's "~175" estimate.)

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Feedback/FeedbackDispatcher.cs Assets/Tests/EditMode/FeedbackDispatcherTests.cs
git commit -m "feat(w6-sp10): FeedbackDispatcher fans out to JudgmentTextPool (3rd subscriber)"
```

---

## Task 7: `FeedbackPresets.asset` color values

The SP4 asset file has `color:` entries for particle tints via Unity's YAML representation. Task 2 gave `FeedbackPresets.cs` defaults in code (gold/cyan/green/red), but Unity's ScriptableObject persistence in the existing asset does **not** re-read those defaults — any new Color field in a previously-saved asset becomes `Color(1,1,1,1)` (white) in the asset YAML on next save unless explicitly re-written.

**Files:**
- Modify: `Assets/ScriptableObjects/FeedbackPresets.asset`

- [ ] **Step 1: Inspect current asset contents**

Read `Assets/ScriptableObjects/FeedbackPresets.asset` to confirm it does not already contain the 4 new fields.

- [ ] **Step 2: Re-save the asset via the Editor**

Option A (preferred): Open Unity Editor, select `Assets/ScriptableObjects/FeedbackPresets.asset` in Project pane, the Inspector shows the 4 new `Text popup colors (SP10)` fields with default values (Unity initializes them to the code defaults because this asset is now re-serialized). Change nothing; click anywhere to flush, or right-click → Reimport. This forces Unity to rewrite the asset YAML with the new fields.

Option B (CLI only, no Editor): edit the `.asset` YAML file directly, appending:

```yaml
  perfectTextColor: {r: 1, g: 0.84, b: 0, a: 1}
  greatTextColor: {r: 0.3058824, g: 0.7607843, b: 1, a: 1}
  goodTextColor: {r: 0.5, g: 0.8745098, b: 0.5, a: 1}
  missTextColor: {r: 1, g: 0.2509804, b: 0.2509804, a: 1}
```

These values match the code defaults in Task 2. Position: after the existing `goodParticle` block, before the next asset's `--- !u!` delimiter (or end of file).

- [ ] **Step 3: Verify asset persists the 4 colors**

Either:
- In Editor: inspect the asset — all 4 colors show the expected swatches.
- CLI: re-read the file and confirm the 4 keys are present.

- [ ] **Step 4: Commit**

```bash
git add Assets/ScriptableObjects/FeedbackPresets.asset
git commit -m "feat(w6-sp10): FeedbackPresets.asset text colors (gold/cyan/green/red)"
```

---

## Task 8: `SceneBuilder` wiring

Extend `BuildFeedbackPipeline` to construct `JudgmentTextCanvas` (WorldSpace) + `JudgmentTextPool`, wire into dispatcher. Also bump APK output names in `ApkBuilder.cs`.

**Files:**
- Modify: `Assets/Editor/SceneBuilder.cs`
- Modify: `Assets/Editor/ApkBuilder.cs`
- Modify: `Assets/Scenes/GameplayScene.unity` (regenerated by menu)

- [ ] **Step 1: Extend `BuildFeedbackPipeline` in `SceneBuilder.cs`**

Modify `Assets/Editor/SceneBuilder.cs`. In `BuildFeedbackPipeline` (line ~318), after the existing `var particlePool = particlesGo.AddComponent<ParticlePool>();` block but before the `dispatcherGo` block, insert:

```csharp
// Text popup pool (SP10) — world-space canvas sits at the judgment line,
// pool cycles 12 Text slots. Each spawn sets text + color + x=worldPos.x
// and animates via JudgmentTextPopup.
var textCanvasGo = new GameObject("JudgmentTextCanvas");
textCanvasGo.transform.SetParent(feedbackRoot.transform, false);
textCanvasGo.transform.position = new Vector3(0f, JudgmentY, 0f);
textCanvasGo.transform.localScale = Vector3.one * 0.01f;
var textCanvasRt = textCanvasGo.AddComponent<RectTransform>();
textCanvasRt.sizeDelta = new Vector2(1000f, 400f);
var textCanvas = textCanvasGo.AddComponent<Canvas>();
textCanvas.renderMode = RenderMode.WorldSpace;
textCanvas.worldCamera = camera;
textCanvas.sortingOrder = 10;   // above particles and gameplay BG

var textPool = textCanvasGo.AddComponent<JudgmentTextPool>();
SetField(textPool, "presets", presets);
SetField(textPool, "poolSize", 12);
SetField(textPool, "lifetimeSec", 0.45f);
SetField(textPool, "yRiseUnits", 0.36f);
SetField(textPool, "fontSize", 48);
SetField(textPool, "worldCanvasScale", 0.01f);
```

Then, in the existing `dispatcherGo` block, append a SetField for `textPool`:

```csharp
var dispatcherGo = new GameObject("FeedbackDispatcher");
dispatcherGo.transform.SetParent(feedbackRoot.transform, false);
var dispatcher = dispatcherGo.AddComponent<FeedbackDispatcher>();
SetField(dispatcher, "judgmentSystem", judgmentSystem);
SetField(dispatcher, "hapticService", hapticService);
SetField(dispatcher, "particlePool", particlePool);
SetField(dispatcher, "textPool", textPool);    // NEW (SP10)
```

Canvas y rationale: the canvas's transform is in world space at `(0, JudgmentY=-5, 0)`. `rt.localScale = 0.01` means canvas-local 1 unit = 0.01 world units. The pool passes `worldPos.x` directly to `anchoredPosition.x` — so effectively canvas-local x is measured in "world x units" (the canvas never rotates or scales x non-uniformly relative to y). `worldPos.x ∈ [-4.5, 4.5]` (half of `LaneAreaWidth=9`) maps 1:1 to canvas-local anchored x in world units. With `localScale.x = 0.01` this renders at world width `worldPos.x × 0.01`… wait — that's a bug. See note below.

**Canvas coordinate note (important):** World-space Canvas with `localScale = 0.01` transforms canvas-local points by 0.01. So setting `anchoredPosition.x = worldPos.x` produces a rendered x of `worldPos.x × 0.01` in world. That's wrong — popup at `worldPos.x = 2.5` would render at world x 0.025.

Two ways to fix:

**Option A (chosen, simpler):** Divide `worldPos.x` by `worldCanvasScale` when setting anchored position. The pool already holds `worldCanvasScale` as a SerializeField. Update Task 5 Step 3 implementation BEFORE running Task 5 tests so all tests pass — OR, equivalently, set `worldCanvasScale = 1.0f` in tests (already done) and 0.01 in SceneBuilder (done), and divide in the pool's Spawn.

Update `JudgmentTextPool.Spawn`:
```csharp
rects[idx].anchoredPosition = new Vector2(worldPos.x / worldCanvasScale, 0f);
```

Tests pass `worldCanvasScale = 1.0f` so the division is a no-op and existing tests stay green. Production uses 0.01 so the anchored x is scaled up by 100× to cancel the canvas scale's 0.01 ×.

**Apply the fix in Task 5's already-committed implementation.** This is a small amendment commit.

**Option B (rejected):** Use a Canvas at `localScale = 1.0` world and a large `sizeDelta` — fonts render at unreadable size because 48-point text at scale 1.0 covers 1+ world unit of height. World-space canvases conventionally use a 0.01-ish scale for text legibility; we keep it.

- [ ] **Step 2: Amend Task 5 pool Spawn with the scale division**

Modify `Assets/Scripts/Feedback/JudgmentTextPool.cs` `Spawn` method. Change:
```csharp
rects[idx].anchoredPosition = new Vector2(worldPos.x, 0f);
```
to:
```csharp
rects[idx].anchoredPosition = new Vector2(worldPos.x / worldCanvasScale, 0f);
```

Update Task 5's tests to pass `worldCanvasScale = 1.0f` (already done — the `BuildPool` helper passes 1f). Re-run the batch command to confirm 179 green.

- [ ] **Step 3: Bump APK output names in `ApkBuilder.cs`**

Modify `Assets/Editor/ApkBuilder.cs`:

- Line 14: `"keyflow-w6-sp2.apk"` → `"keyflow-w6-sp10.apk"`
- Line 35: `"keyflow-w6-sp3-profile.apk"` → `"keyflow-w6-sp10-profile.apk"`

- [ ] **Step 4: Rebuild GameplayScene via menu**

Open Unity Editor on the project, run menu `KeyFlow → Build W4 Scene`. This regenerates `Assets/Scenes/GameplayScene.unity` with the new `JudgmentTextCanvas` GameObject wired. Verify in the Hierarchy under `FeedbackPipeline`: `HapticService`, `ParticlePool`, `JudgmentTextCanvas`, `FeedbackDispatcher`.

Confirm `FeedbackDispatcher`'s inspector shows the `Text Pool` SerializeField populated with the `JudgmentTextCanvas` reference.

- [ ] **Step 5: Run EditMode tests one more time (full run, 179 expected green)**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

Expected: 179/179 green. If any red, inspect `Builds/test-log.txt` and fix before committing the scene.

- [ ] **Step 6: Commit**

```bash
git add Assets/Editor/SceneBuilder.cs Assets/Editor/ApkBuilder.cs Assets/Scenes/GameplayScene.unity Assets/Scripts/Feedback/JudgmentTextPool.cs
git commit -m "feat(w6-sp10): wire JudgmentTextCanvas into BuildFeedbackPipeline + regenerate GameplayScene"
```

---

## Task 9: Full EditMode test run (checkpoint)

Before touching the device, confirm no regressions at the EditMode level.

- [ ] **Step 1: Close Unity Editor on this project**

If the Editor is open from Task 8 Step 4, save and close it. (IL2CPP batch will collide otherwise.)

- [ ] **Step 2: Run full EditMode suite**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile Builds/test-log.txt
```

Expected: 179/179 green. If count differs from plan's projection, reconcile: test count may be 175-180 depending on whether any existing tests were inadvertently duplicated or skipped.

- [ ] **Step 3: Run pytest to confirm pipeline untouched**

```bash
cd tools/midi_to_kfchart && python -m pytest -v
```

Expected: 49/49 green (same as before this SP).

- [ ] **Step 4: No commit**

This task is a verification checkpoint only. No file changes.

---

## Task 10: Build release APK

**Files:**
- Output: `Builds/keyflow-w6-sp10.apk`

- [ ] **Step 1: Close Unity Editor**

- [ ] **Step 2: Build release APK**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod KeyFlow.Editor.ApkBuilder.Build -logFile Builds/apk-build-log.txt
```

Note: `-executeMethod` uses `-quit`-compatible semantics (unlike `-runTests`) — `-quit` optional but can be added to force Editor exit after the build. The call will block this shell until complete (~2-3 minutes).

Expected: `Builds/keyflow-w6-sp10.apk` exists.

- [ ] **Step 3: Verify APK size**

```bash
ls -la Builds/keyflow-w6-sp10.apk
```

Expected: size < 40.2 MB (≈ 42,000,000 bytes). Previous SP9 APK was 39.89 MB; SP10 adds 3 new C# files (~8 KB) + 1 Scene regen delta + the same built-in `LegacyRuntime.ttf` font (already linked). Expected delta ~0.05 MB.

If size exceeds 40.2 MB, inspect the build log for unexpected asset inclusions.

- [ ] **Step 4: No commit**

APK is build artifact; gitignored (`*.apk` pattern).

---

## Task 11: Device playtest (Galaxy S22, R5CT21A31QB)

Acceptance criteria from the spec §6.5. Execute against release APK.

- [ ] **Step 1: Install APK on device**

```bash
adb -s R5CT21A31QB install -r Builds/keyflow-w6-sp10.apk
```

Expected: `Success`.

- [ ] **Step 2: Launch app and play Entertainer Normal for 2 minutes**

On the device, tap `나윤` → select Entertainer → Normal → play the full chart.

- [ ] **Step 3: Verify acceptance checklist (record results for the completion report)** 🧑 HUMAN HANDOFF REQUIRED

The following checks require a person physically tapping the device and observing the screen. A subagent running this plan cannot complete this step — it should stop here and surface the checklist to a human for execution, attaching the APK path and any diagnostic build output.

For each item, mark ✓ / ✗ and note observations:

- [ ] All 4 judgment texts (PERFECT / GREAT / GOOD / MISS) appear during the run.
- [ ] Each text's color matches the preset (gold / cyan / green / red).
- [ ] Text y-position stays on the judgment line regardless of tap timing (early-tap text does not drift up).
- [ ] No text pile-up: 4-note chord spawns ≤ 4 text popups simultaneously, no 5+ overlapping.
- [ ] 60 fps throughout — use `LatencyMeter` overlay FPS line (should read 59.5-60.5 steady).
- [ ] APK size < 40.2 MB (confirmed Task 10).
- [ ] Text legible on blue (나윤) gameplay background.
- [ ] Back-navigate to Start → pick 소윤 → yellow bg → text also legible on yellow.

- [ ] **Step 4: Profiler GC=0 verification**

Build profile APK (`KeyFlow → Build APK (Profile)` or via CLI equivalent):

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod KeyFlow.Editor.ApkBuilder.BuildProfile -logFile Builds/apk-profile-build-log.txt
```

Install on device, connect Unity Profiler, play Entertainer Normal for 2 minutes with the Memory → GC.Alloc module visible. Expected: GC.Collect count delta = 0 across the 2-minute span.

If GC is non-zero: inspect the allocations panel, identify the source (most likely suspect: `Text.text` assignment if the static readonly strings aren't being recognized as same-string by UGUI's dirty check, or `rt.anchoredPosition = new Vector2(...)` if somehow boxing). Fix, rebuild, re-verify.

- [ ] **Step 5: Record results**

Open `docs/superpowers/reports/2026-04-24-w6-sp10-judgment-text-popup-completion.md` (Task 13 creates it fresh; for now draft key points in-place or a scratch file).

- [ ] **Step 6: No commit yet**

Playtest outcomes fold into Task 13's completion report.

---

## Task 12: Housekeeping C — `truncate_charts.py` obsolescence check

Verify whether `truncate_charts.py` is still needed post-SP8. If pytest and chart generation pipeline are clean without it, delete; else document why it stays.

**Files:**
- Potentially delete: `tools/midi_to_kfchart/truncate_charts.py`

- [ ] **Step 1: Confirm `truncate_charts.py` has no pytest callers**

```bash
cd tools/midi_to_kfchart && grep -rn "truncate_charts" tests/ pipeline/
```

Expected: no matches (docstring mentions an SP2 hand-use, no automation).

- [ ] **Step 2: Run the current pytest suite to confirm 49/49 green without invoking truncate**

```bash
cd tools/midi_to_kfchart && python -m pytest -v
```

Expected: 49/49 green. (pytest does not reference truncate_charts.)

- [ ] **Step 3: Decide delete vs. keep**

- If a run of `midi_to_kfchart.py` on a representative MIDI (e.g., one of the 3 shipped songs) produces a `.kfchart` where `max(notes[].t) ≤ durationMs` — **truncate_charts.py is obsolete.** Delete it.
- If any chart still produces notes past `durationMs` — **truncate_charts.py is still a required manual cleanup.** Keep it; do not delete.

Spot-check one song (e.g., Entertainer):
```bash
cd tools/midi_to_kfchart && python midi_to_kfchart.py data/entertainer.mid && python -c "
import json
doc = json.loads(open('data/entertainer.kfchart').read())
for diff, c in doc['charts'].items():
    max_t = max((n['t'] for n in c['notes']), default=0)
    print(f'{diff}: max_t={max_t}ms, durationMs={doc[\"durationMs\"]}ms, ok={max_t <= doc[\"durationMs\"]}')
"
```

Expected output: `ok=True` for all difficulties. If any `ok=False`, keep `truncate_charts.py`.

- [ ] **Step 4a (if obsolete): Delete the script**

```bash
git rm tools/midi_to_kfchart/truncate_charts.py
```

- [ ] **Step 4b (if still needed): Update docstring**

Modify the top docstring of `truncate_charts.py` to explicitly state "Verified still required as of 2026-04-24 post-SP10 obsolescence check — density.py may produce notes beyond durationMs for charts with hold tails extending past song end."

(This path is a no-op if the script is obsolete.)

- [ ] **Step 5: Commit**

If deleted:
```bash
git add tools/midi_to_kfchart/truncate_charts.py  # captures the deletion
git commit -m "chore(w6-sp10): delete truncate_charts.py (obsolete after SP2 density fix + SP8 merge_adjacent_holds)"
```

If kept:
```bash
git add tools/midi_to_kfchart/truncate_charts.py
git commit -m "docs(w6-sp10): annotate truncate_charts.py still-required post-SP10 check"
```

---

## Task 13: Merge to main + completion report

**Files:**
- Create: `docs/superpowers/reports/2026-04-24-w6-sp10-judgment-text-popup-completion.md`

- [ ] **Step 1: Draft completion report**

Using the Task 11 playtest results, create `docs/superpowers/reports/2026-04-24-w6-sp10-judgment-text-popup-completion.md`. Use the same template as `2026-04-24-w6-sp9-profile-start-screen-completion.md`: summary, test counts, APK size, device playtest results, residual issues, follow-ups.

Key sections to fill:
- **Tests**: 179/179 EditMode green, 49/49 pytest green.
- **APK**: final size (Task 10 Step 3).
- **Device playtest**: all checklist items.
- **GC**: profiler result (Task 11 Step 4).
- **Residual issues**: any tuning values that ended up different from spec defaults.
- **Follow-ups**: items from spec §10 (hold-success popup, combo-milestones, localization).

- [ ] **Step 2: Commit the report**

```bash
git add docs/superpowers/reports/2026-04-24-w6-sp10-judgment-text-popup-completion.md
git commit -m "docs(w6-sp10): completion report with device playtest results"
```

- [ ] **Step 3: Merge the SP10 branch into main**

Switch to main and merge (no-ff for clear history, matching SP8/SP9 pattern):

```bash
git checkout main
git merge --no-ff w6-sp10-judgment-text-popup -m "merge: W6 SP10 judgment text popup"
```

If a merge conflict in `Assets/Scenes/GameplayScene.unity` or `Assets/Editor/SceneBuilder.cs`, resolve by taking the SP10 branch's version (SP10 is the latest author of these files); re-run Task 9 Step 2 to confirm scene integrity after resolve, then `git commit`.

- [ ] **Step 4: Verify main's state**

```bash
git log --oneline -5
git status
```

Expected: latest commit is `merge: W6 SP10 judgment text popup`, working tree clean.

- [ ] **Step 5: Done**

SP10 complete. Memory update (by human): add `project_w6_sp10_complete.md` note referencing the merge commit and any device-playtest learnings.

---

## Post-merge follow-ups (explicit non-scope)

From spec §10:
- Hold-success "CLEARED" popup.
- Combo-milestone popups (50/100 COMBO!).
- Korean localization (if QA reports English hard to read).
- `LatencyMeter` HUD removal or gating by debug flag (unrelated; noticed during SP10 exploration).
