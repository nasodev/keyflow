# KeyFlow W6 SP8 — Hold-Note Polish Pass Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve three hold-note pain points in one sub-project: raise the MIDI→chart HOLD threshold (300→500 ms) to reduce hold density, add a lane-judgment-line glow while holding (visual signal), and retrigger the held pitch at 250 ms intervals (audio signal).

**Architecture:** Three orthogonal layers of change. (1) Content: one Python constant + chart regeneration. (2) Audio: new `AudioSamplePool.PlayForPitch(int, float)` overload + id-keyed retrigger state inside `HoldTracker`. (3) Visual: new `LaneGlowController` MonoBehaviour wired into `HoldTracker` transitions. All three are surfaced through `SceneBuilder` to preserve the SP7 "single-menu scene build" invariant. GC-free throughout (SP3 baseline must survive).

**Tech Stack:** Unity 6 (C#, IL2CPP, Android), Python 3 (midi_to_kfchart pipeline), NUnit (EditMode), pytest.

**Spec:** `docs/superpowers/specs/2026-04-23-keyflow-w6-sp8-hold-note-polish-design.md`

---

## File Structure

```
tools/midi_to_kfchart/
  pipeline/hold_detector.py          MODIFY (1 line)
  tests/test_hold_detector.py        MODIFY (threshold boundary cases)
  batch_w6_sp8.yaml                  NEW (all 4 songs, EASY+NORMAL)

Assets/StreamingAssets/charts/
  beethoven_fur_elise.kfchart        REGENERATE
  beethoven_ode_to_joy.kfchart       REGENERATE
  debussy_clair_de_lune.kfchart      REGENERATE
  joplin_the_entertainer.kfchart     REGENERATE

Assets/Scripts/Gameplay/
  AudioSamplePool.cs                 MODIFY (PlayForPitch overload)
  HoldTracker.cs                     MODIFY (holdAudio dict, laneGlow field, retrigger loop)
  JudgmentSystem.cs                  MODIFY (pass tapTimeMs to OnHoldStartTapAccepted)

Assets/Scripts/Feedback/
  LaneGlowController.cs              NEW
  LaneGlowController.cs.meta         NEW (Unity auto-generates)

Assets/Tests/EditMode/
  AudioSamplePoolTests.cs            MODIFY (volume parameter tests)
  HoldAudioRetriggerTests.cs         NEW
  LaneGlowControllerTests.cs         NEW

Assets/Editor/
  SceneBuilder.cs                    MODIFY (BuildLaneGlow helper + field wirings)

Assets/Scenes/
  GameplayScene.unity                REGENERATE (via SceneBuilder menu)
```

**Ordering rationale:** Python layer is pure and has zero Unity dependencies, so it ships first (Task 1). Then the smallest Unity change (AudioSamplePool overload, Task 2) is landed in isolation — EditMode-only, no scene work. `HoldTracker` retrigger logic (Task 3) depends on Task 2's API and exercises the id-keyed dict design that carried the heaviest review-round scrutiny. `LaneGlowController` (Task 4) is a pure MonoBehaviour — lands independently of Task 3. SceneBuilder integration (Task 5) wires everything together and regenerates the scene. APK build + device playtest + completion report (Task 6) is the acceptance gate.

---

## Task 1: Raise HOLD threshold 300 → 500 ms and regenerate charts

**Spec reference:** §4.1

**Files:**
- Modify: `tools/midi_to_kfchart/pipeline/hold_detector.py:2`
- Modify: `tools/midi_to_kfchart/tests/test_hold_detector.py`
- Create: `tools/midi_to_kfchart/batch_w6_sp8.yaml`
- Regenerate: `Assets/StreamingAssets/charts/*.kfchart` (4 files)

- [ ] **Step 1.1: Update failing threshold-boundary tests (TDD)**

Edit `tools/midi_to_kfchart/tests/test_hold_detector.py`. Replace the existing 299/300 boundary tests with 499/500 boundary tests. The expectation is that 300 ms — which currently is HOLD — becomes TAP at the new threshold:

```python
from pipeline.hold_detector import classify


def _raw(t, pitch, dur):
    return {"t_ms": t, "pitch": pitch, "dur_ms": dur}


def test_499ms_is_tap():
    typed = classify([_raw(0, 60, 499)])
    assert typed[0]["type"] == "TAP"
    assert typed[0]["dur"] == 0


def test_500ms_is_hold():
    typed = classify([_raw(0, 60, 500)])
    assert typed[0]["type"] == "HOLD"
    assert typed[0]["dur"] == 500


def test_old_threshold_300ms_is_now_tap():
    """Regression: 300 ms is TAP under the new 500 ms threshold."""
    typed = classify([_raw(0, 60, 300)])
    assert typed[0]["type"] == "TAP"
    assert typed[0]["dur"] == 0


def test_hold_cap_4000ms():
    typed = classify([_raw(0, 60, 8000)])
    assert typed[0]["type"] == "HOLD"
    assert typed[0]["dur"] == 4000


def test_tap_always_dur_zero():
    typed = classify([_raw(0, 60, 0), _raw(100, 61, 50)])
    assert all(n["type"] == "TAP" and n["dur"] == 0 for n in typed)
```

- [ ] **Step 1.2: Run the tests and watch them fail**

Command (from repo root):
```
cd tools/midi_to_kfchart && python -m pytest tests/test_hold_detector.py -v
```

Expected: `test_499ms_is_tap` passes (currently 499 < 300 check is already TAP... actually wait — under the current 300 threshold, 499 ms is HOLD since 499 >= 300. So this SHOULD fail under the current threshold). `test_500ms_is_hold` passes (500 >= 300). `test_old_threshold_300ms_is_now_tap` FAILS (currently 300 ms classifies as HOLD).

- [ ] **Step 1.3: Update the threshold constant**

Edit `tools/midi_to_kfchart/pipeline/hold_detector.py`:

```python
"""Classify RawNotes into TAP/HOLD based on sustain length."""
HOLD_THRESHOLD_MS = 500
HOLD_CAP_MS = 4000


def classify(raws: list[dict]) -> list[dict]:
    out: list[dict] = []
    for n in raws:
        if n["dur_ms"] >= HOLD_THRESHOLD_MS:
            out.append({
                "t": n["t_ms"], "pitch": n["pitch"],
                "type": "HOLD",
                "dur": min(n["dur_ms"], HOLD_CAP_MS),
            })
        else:
            out.append({
                "t": n["t_ms"], "pitch": n["pitch"],
                "type": "TAP", "dur": 0,
            })
    return out
```

(Only the constant changes; body is unchanged.)

- [ ] **Step 1.4: Run the tests and confirm all pass**

Command:
```
cd tools/midi_to_kfchart && python -m pytest tests/test_hold_detector.py -v
```

Expected: all 5 tests green.

- [ ] **Step 1.5: Run the full pytest suite**

Command:
```
cd tools/midi_to_kfchart && python -m pytest -q
```

Expected: all green. If `test_w6_sp2_charts.py` starts failing, read the failure carefully — that test validates structural invariants (lane bounds, pitch bounds, temporal sort) but NOT specific HOLD counts, so it should still pass unless regeneration produces a truly malformed chart.

- [ ] **Step 1.6: Create new batch config including Für Elise**

Create `tools/midi_to_kfchart/batch_w6_sp8.yaml`:

```yaml
defaults:
  out_dir: ../../Assets/StreamingAssets/charts/

songs:
  - song_id: beethoven_fur_elise
    midi: midi_sources/fur_elise.mid
    title: "Für Elise"
    composer: "Beethoven"
    bpm: 72
    duration_ms: 45000
    difficulties:
      EASY:   { target_nps: 1.5 }
      NORMAL: { target_nps: 3.5 }

  - song_id: beethoven_ode_to_joy
    midi: midi_sources/ode_to_joy.mid
    title: "Ode to Joy"
    composer: "Beethoven"
    bpm: 100
    duration_ms: 120000
    difficulties:
      EASY:   { target_nps: 0.35 }
      NORMAL: { target_nps: 0.55 }

  - song_id: debussy_clair_de_lune
    midi: midi_sources/clair_de_lune.mid
    title: "Clair de Lune"
    composer: "Debussy"
    bpm: 60
    duration_ms: 320000
    difficulties:
      EASY:   { target_nps: 1.5 }
      NORMAL: { target_nps: 2.8 }

  - song_id: joplin_the_entertainer
    midi: midi_sources/the_entertainer.mid
    title: "The Entertainer"
    composer: "Joplin"
    bpm: 72
    duration_ms: 255000
    difficulties:
      EASY:   { target_nps: 2.2 }
      NORMAL: { target_nps: 4.0 }
```

(The Für Elise EASY/NORMAL NPS values are copied from the existing chart's implied tuning; if regeneration lands very different totals, flag before proceeding to Task 2.)

- [ ] **Step 1.7: Regenerate all 4 charts**

Command:
```
cd tools/midi_to_kfchart && python midi_to_kfchart.py --batch batch_w6_sp8.yaml
```

Expected output: 8 `[OK]` lines (one per song × difficulty), writing to `../../Assets/StreamingAssets/charts/<song_id>.kfchart`.

- [ ] **Step 1.8: Verify chart structure + HOLD density reduction**

Command (from repo root):
```
python -c "
import json
for f in ['beethoven_fur_elise', 'beethoven_ode_to_joy', 'debussy_clair_de_lune', 'joplin_the_entertainer']:
    c=json.load(open(f'Assets/StreamingAssets/charts/{f}.kfchart'))
    for d, ch in c['charts'].items():
        total=ch['totalNotes']
        holds=sum(1 for n in ch['notes'] if n['type']=='HOLD')
        print(f'{f:35s} {d:6s} total={total:4d} HOLD={holds:4d} ({100*holds/total:.1f}%)')
"
```

Expected: HOLD percentages drop vs. pre-change baseline (see spec §4.1 table). Exact numbers depend on regeneration; record them for the completion report. Red flags: any song × difficulty where `totalNotes == 0`, or where HOLD count exceeds previous. Either indicates pipeline regression — investigate before proceeding.

- [ ] **Step 1.9: Run the full chart-regression test**

Command:
```
cd tools/midi_to_kfchart && python -m pytest tests/test_w6_sp2_charts.py -v
```

Expected: all green. Structural invariants still hold.

- [ ] **Step 1.10: Commit**

```
git add tools/midi_to_kfchart/pipeline/hold_detector.py \
        tools/midi_to_kfchart/tests/test_hold_detector.py \
        tools/midi_to_kfchart/batch_w6_sp8.yaml \
        Assets/StreamingAssets/charts/*.kfchart
git commit -m "$(cat <<'EOF'
feat(w6-sp8): raise HOLD threshold 300 → 500 ms + regenerate 4 charts

Quarter notes at 120 BPM (500 ms) remain HOLD; 8th notes and faster
at common tempos now TAP. Regenerated 4 songs × 2 difficulties = 8
difficulty sections across 4 .kfchart files.

Adds batch_w6_sp8.yaml covering all 4 shipped songs (batch_w6_sp2.yaml
was missing Für Elise despite the chart existing).
EOF
)"
```

---

## Task 2: AudioSamplePool volume overload

**Spec reference:** §4.3 AudioSamplePool API addition

**Files:**
- Modify: `Assets/Scripts/Gameplay/AudioSamplePool.cs`
- Modify: `Assets/Tests/EditMode/AudioSamplePoolTests.cs`

- [ ] **Step 2.1: Write failing test for volume parameter**

Add to `Assets/Tests/EditMode/AudioSamplePoolTests.cs` inside the `AudioSamplePoolTests` class:

```csharp
[Test]
public void PlayForPitch_NoVolume_UsesFullVolume()
{
    var go = new GameObject("pool");
    var pool = go.AddComponent<AudioSamplePool>();
    pool.InitializeForTest(channels: 4);
    pool.SetPitchMapForTest(MakeDummyMap(17), baseMidiValue: 36, stepSemitonesValue: 3);

    pool.PlayForPitch(60);
    var src = pool.NextSource();
    // PlayForPitch picked src[0], NextSource advanced to src[1]; rewind:
    // Actually: after PlayForPitch, nextIndex == 1. Grab src[0] directly by
    // re-obtaining what was just used — easier: re-order so we capture first.
    Object.DestroyImmediate(go);
}

[Test]
public void PlayForPitch_WithVolume_SetsSourceVolume()
{
    var go = new GameObject("pool");
    var pool = go.AddComponent<AudioSamplePool>();
    pool.InitializeForTest(channels: 4);
    pool.SetPitchMapForTest(MakeDummyMap(17), baseMidiValue: 36, stepSemitonesValue: 3);

    // Peek at the source that PlayForPitch will use FIRST.
    var srcBefore = pool.NextSource();
    // PlayForPitch uses nextIndex's source internally and advances — rewind by
    // running through the cycle once more. The cleanest approach is to expose
    // a "last used source" accessor for tests, but since the pool cycles in a
    // known order, we can predict: after the NextSource peek above, nextIndex
    // == 1. The next PlayForPitch call will use sources[1].
    pool.PlayForPitch(60, volume: 0.7f);
    // sources[1] was just used. NextSource() advances to sources[2]; call it
    // once to re-grab sources[1]... that's not right either.

    // Simpler: do PlayForPitch first (uses sources[0]), then inspect sources[0]
    // via NextSource() cycling back.
    Object.DestroyImmediate(go);
}
```

**Reviewer note for the implementer:** the above draft exposes a test-design issue: `AudioSamplePool` doesn't expose the source it just used, only `NextSource()` which advances the cursor. There are two clean options:

(a) Rewrite the test to inspect the `AudioSource` via GameObject hierarchy: `go.GetComponents<AudioSource>()[0].volume` — works because `InitializeForTest` attaches sources as components on the pool's GameObject. Use this approach; the final test looks like:

```csharp
[Test]
public void PlayForPitch_NoVolumeArg_UsesVolumeOne()
{
    var go = new GameObject("pool");
    var pool = go.AddComponent<AudioSamplePool>();
    pool.InitializeForTest(channels: 4);
    pool.SetPitchMapForTest(MakeDummyMap(17), baseMidiValue: 36, stepSemitonesValue: 3);

    pool.PlayForPitch(60);

    // PlayForPitch used sources[0] (nextIndex started at 0, advanced to 1).
    // The AudioSources were added in Initialize() and are retrievable via
    // GetComponents in insertion order.
    var sources = go.GetComponents<AudioSource>();
    Assert.AreEqual(1f, sources[0].volume, "default PlayForPitch should use volume = 1");

    Object.DestroyImmediate(go);
}

[Test]
public void PlayForPitch_WithVolumeArg_PassesToSource()
{
    var go = new GameObject("pool");
    var pool = go.AddComponent<AudioSamplePool>();
    pool.InitializeForTest(channels: 4);
    pool.SetPitchMapForTest(MakeDummyMap(17), baseMidiValue: 36, stepSemitonesValue: 3);

    pool.PlayForPitch(60, volume: 0.7f);

    var sources = go.GetComponents<AudioSource>();
    Assert.AreEqual(0.7f, sources[0].volume, 1e-5f);

    Object.DestroyImmediate(go);
}
```

Delete the exploratory drafts and keep only the two tests above.

- [ ] **Step 2.2: Run the tests and confirm they fail**

From Unity Editor: run EditMode tests under KeyFlow.Tests.EditMode → AudioSamplePoolTests.
Or from CLI (preferred in batch build, requires Unity Editor path; adjust UNITY_EXE env):
```
"$UNITY_EXE" -batchmode -projectPath "$(pwd)" -runTests -testPlatform EditMode \
  -testCategory "" -testFilter "AudioSamplePoolTests.PlayForPitch_" \
  -logFile - -quit 2>&1 | tail -40
```
Wait — per `memory/feedback_unity_runtests_no_quit.md`: `-runTests` + `-quit` skips the runner. Drop `-quit` when running tests.

Expected: `PlayForPitch_WithVolumeArg_PassesToSource` FAILS (current overload takes no volume; compile error or default 1).

- [ ] **Step 2.3: Add the volume overload**

Edit `Assets/Scripts/Gameplay/AudioSamplePool.cs`. Replace the existing `PlayForPitch(int)` method:

```csharp
public void PlayForPitch(int midiPitch, float volume = 1f)
{
    var (clip, ratio) = ResolveSample(midiPitch, pitchSamples, baseMidi, stepSemitones);
    if (clip == null)
    {
        PlayOneShot();
        return;
    }
    var src = NextSource();
    src.pitch = ratio;
    src.clip = clip;
    src.volume = volume;
    src.Play();
}
```

(The addition is the `volume` parameter with default 1 and the `src.volume = volume` assignment. All existing call sites continue to work unchanged.)

- [ ] **Step 2.4: Run the tests and confirm they pass**

Same command as 2.2. Expected: both new tests green, all existing `AudioSamplePoolTests` green.

- [ ] **Step 2.5: Commit**

```
git add Assets/Scripts/Gameplay/AudioSamplePool.cs Assets/Tests/EditMode/AudioSamplePoolTests.cs
git commit -m "$(cat <<'EOF'
feat(w6-sp8): add volume parameter to AudioSamplePool.PlayForPitch

Default 1f preserves existing TapInputHandler.PlayTapSound behavior.
Used by upcoming hold-note retrigger at volume 0.7.
EOF
)"
```

---

## Task 3: Hold-note audio retrigger in HoldTracker

**Spec reference:** §4.3 (flow, constants, state, GC invariants)

**Files:**
- Modify: `Assets/Scripts/Gameplay/HoldTracker.cs`
- Modify: `Assets/Scripts/Gameplay/JudgmentSystem.cs:116`
- Create: `Assets/Tests/EditMode/HoldAudioRetriggerTests.cs`

This is the largest task. Sub-steps 3.1–3.4 prepare the test scaffolding (test-first); 3.5 updates the caller signature; 3.6 implements the retrigger. Split into subtasks because HoldTracker is a MonoBehaviour with serialized dependencies — setting up test scaffolding correctly is the bulk of the effort.

- [ ] **Step 3.1: Write test file skeleton with first failing test**

Create `Assets/Tests/EditMode/HoldAudioRetriggerTests.cs`. The tests exercise `HoldTracker` through its public API by injecting fake or stub-like dependencies. Since `HoldTracker` is a MonoBehaviour with SerializeField dependencies (`tapInput`, `audioSync`, `judgmentSystem`, and new ones `laneGlow`, `audioPool`), and uses private-field assignments from the editor, write tests that use reflection or an internal test hook.

Look at `memory` note from SP3 and examine existing `HoldStateMachineTests` — those work on pure C# classes, easier. For `HoldTracker`, the testing approach will need either:
- (a) Public/internal test hooks on HoldTracker (e.g., `internal void InjectDependencies(...)` — see `JudgmentSystem.InvokeHandleTapForTest` for precedent).
- (b) Real `AudioSyncManager` + `TapInputHandler` GameObjects wired via reflection.

**Choose (a).** Add `internal` test hooks to HoldTracker in the implementation step; for tests, use `InternalsVisibleTo` (already set: see `JudgmentSystem.cs:6`).

Initial test — first behavior to verify: `OnHoldStartTapAccepted` seeds `holdAudio` with `tapTimeMs`, not `HitTimeMs`.

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.Charts;

namespace KeyFlow.Tests.EditMode
{
    public class HoldAudioRetriggerTests
    {
        // Fake AudioSamplePool to count/record PlayForPitch calls.
        private class FakePool : AudioSamplePool
        {
            public readonly List<(int pitch, float volume)> Calls = new();
            // We can't override non-virtual PlayForPitch. Approach: subclass
            // and shadow via new, OR route through a test-only delegate.
            // HoldTracker uses audioPool.PlayForPitch(pitch, vol) directly,
            // so shadowing is fine if HoldTracker calls through a virtual or
            // the field type is AudioSamplePool (exact match). Add virtual to
            // AudioSamplePool.PlayForPitch in Task 2 refinement? No —
            // minimal change: introduce an indirection interface in Task 3.
            // Simpler: test via behavior on the real AudioSamplePool + inspect
            // AudioSource list after each call.
        }
    }
}
```

**Reviewer note for the implementer:** the FakePool subclass-with-shadowing approach gets complicated because C# `new` hiding doesn't intercept a base-class pointer. Two cleaner paths:

**Option A (preferred, minimal):** Use a real `AudioSamplePool` with `SetPitchMapForTest(MakeDummyMap(17), 36, 3)`. After each tick, inspect `go.GetComponents<AudioSource>()` to count `AudioSource.isPlaying == true` sources or to check `volume` values. This matches the Task 2 test style.

**Option B (structured):** Introduce an `IAudioSamplePlayer` interface with `void PlayForPitch(int pitch, float volume)`. Make `AudioSamplePool` implement it. Make `HoldTracker.audioPool` field type `AudioSamplePool` (keep concrete for SceneBuilder-SetField) but have tests inject via an internal setter that accepts `IAudioSamplePlayer`. This is over-engineered for one test file — skip unless Option A proves infeasible.

**Go with Option A.** Tests inspect the real AudioSource children's `volume` field and `isPlaying` state after each simulated tick.

Throw away the FakePool draft above. The first real test:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.Charts;

namespace KeyFlow.Tests.EditMode
{
    public class HoldAudioRetriggerTests
    {
        // Helpers mirror AudioSamplePoolTests patterns.
        private static AudioClip[] MakeDummyMap(int count)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++)
                clips[i] = AudioClip.Create($"dummy{i}", 1, 1, 48000, false);
            return clips;
        }

        // Build a HoldTracker wired with stubs. Returns the tracker + pool + audioSync GameObject.
        private static (HoldTracker tracker, AudioSamplePool pool, AudioSyncManager audioSync, GameObject host)
            BuildTracker()
        {
            var host = new GameObject("HoldTrackerHost");

            var poolGo = new GameObject("Pool");
            poolGo.transform.SetParent(host.transform);
            var pool = poolGo.AddComponent<AudioSamplePool>();
            pool.InitializeForTest(channels: 8);
            pool.SetPitchMapForTest(MakeDummyMap(17), baseMidiValue: 36, stepSemitonesValue: 3);

            var audioSyncGo = new GameObject("AudioSync");
            audioSyncGo.transform.SetParent(host.transform);
            audioSyncGo.AddComponent<AudioSource>();
            var audioSync = audioSyncGo.AddComponent<AudioSyncManager>();

            var trackerGo = new GameObject("HoldTracker");
            trackerGo.transform.SetParent(host.transform);
            var tracker = trackerGo.AddComponent<HoldTracker>();

            // Inject via internal test hook (added in Step 3.5).
            tracker.SetDependenciesForTest(audioSync: audioSync, audioPool: pool);

            return (tracker, pool, audioSync, host);
        }

        private static NoteController MakeNote(int lane, int hitMs, int durMs, int pitch)
        {
            var go = new GameObject($"Note_L{lane}_T{hitMs}");
            var note = go.AddComponent<NoteController>();
            // Set via test hook (added in Step 3.5).
            note.SetForTest(lane: lane, hitTimeMs: hitMs, durMs: durMs, pitch: pitch, type: NoteType.HOLD);
            return note;
        }

        [Test]
        public void OnHoldStartTapAccepted_SeedsHoldAudioWithTapTime()
        {
            var (tracker, pool, audioSync, host) = BuildTracker();
            var note = MakeNote(lane: 0, hitMs: 1000, durMs: 1000, pitch: 60);

            // Tap fires at 980 (early P judgment).
            tracker.OnHoldStartTapAccepted(note, tapTimeMs: 980);

            // Advance song time: at 1229, no retrigger yet (since 1229 - 980 = 249 < 250).
            audioSync.SetSongTimeMsForTest(1229);
            audioSync.SetIsPlayingForTest(true);
            tracker.TickForTest();

            int playCountAt1229 = CountPlayingSources(pool);
            Assert.AreEqual(0, playCountAt1229, "before 250 ms since tap, no retrigger");

            // At 1230 (980 + 250), first retrigger fires.
            audioSync.SetSongTimeMsForTest(1230);
            tracker.TickForTest();

            int playCountAt1230 = CountPlayingSources(pool);
            Assert.AreEqual(1, playCountAt1230, "at tapTimeMs + 250, one retrigger fires");

            Object.DestroyImmediate(host);
        }

        private static int CountPlayingSources(AudioSamplePool pool)
        {
            var sources = pool.gameObject.GetComponents<AudioSource>();
            int n = 0;
            foreach (var s in sources)
                if (s.isPlaying || s.clip != null) n++; // isPlaying is false in batch/test mode
            return n;
        }
    }
}
```

**Reviewer note on the helper approach:** `AudioSource.isPlaying` is `false` in EditMode/batch without the audio subsystem spinning, so `CountPlayingSources` falls back to checking `clip != null`. This is a proxy — any source whose `Play()` was invoked has `clip` set. Reset happens on subsequent `Play(newClip)`. For two retriggers in a row on the same source cycle, this would undercount. The workaround: use a larger `channels` setting (8) and observe that cycling through fresh sources each retrigger keeps the `clip != null` count monotonic over a short test. Document this in a test-helper comment.

Better alternative: track the last-assigned `clip` field changes via a MonoBehaviour. Skip — too elaborate. The `clip != null` count is adequate when each retrigger lands on a fresh source (true for 4-hold max with 8 channels over sub-second intervals).

- [ ] **Step 3.2: Add the rest of the test cases (still failing)**

Extend `HoldAudioRetriggerTests` with:

```csharp
[Test]
public void NoRetrigger_Before250ms()
{
    var (tracker, pool, audioSync, host) = BuildTracker();
    var note = MakeNote(lane: 0, hitMs: 1000, durMs: 2000, pitch: 60);
    tracker.OnHoldStartTapAccepted(note, tapTimeMs: 1000);
    audioSync.SetIsPlayingForTest(true);

    audioSync.SetSongTimeMsForTest(1249);
    tracker.TickForTest();

    Assert.AreEqual(0, CountPlayingSources(pool));
    Object.DestroyImmediate(host);
}

[Test]
public void Retrigger_ThreeTimesAt750ms()
{
    var (tracker, pool, audioSync, host) = BuildTracker();
    var note = MakeNote(lane: 0, hitMs: 1000, durMs: 2000, pitch: 60);
    tracker.OnHoldStartTapAccepted(note, tapTimeMs: 1000);
    audioSync.SetIsPlayingForTest(true);

    // Simulate ticks every 100 ms from 1100 through 1750.
    for (int t = 1100; t <= 1750; t += 100)
    {
        audioSync.SetSongTimeMsForTest(t);
        tracker.TickForTest();
    }

    // Expected retriggers: 1250, 1500, 1750 → 3 retriggers.
    Assert.AreEqual(3, CountPlayingSources(pool));
    Object.DestroyImmediate(host);
}

[Test]
public void NoRetrigger_WhilePaused()
{
    var (tracker, pool, audioSync, host) = BuildTracker();
    var note = MakeNote(lane: 0, hitMs: 1000, durMs: 2000, pitch: 60);
    tracker.OnHoldStartTapAccepted(note, tapTimeMs: 1000);
    audioSync.SetIsPlayingForTest(true);
    audioSync.SetIsPausedForTest(true);

    audioSync.SetSongTimeMsForTest(1500);
    tracker.TickForTest();

    Assert.AreEqual(0, CountPlayingSources(pool));
    Object.DestroyImmediate(host);
}

[Test]
public void NoRetrigger_WhenIsPlayingFalse()
{
    var (tracker, pool, audioSync, host) = BuildTracker();
    var note = MakeNote(lane: 0, hitMs: 1000, durMs: 2000, pitch: 60);
    tracker.OnHoldStartTapAccepted(note, tapTimeMs: 1000);
    audioSync.SetIsPlayingForTest(false);

    audioSync.SetSongTimeMsForTest(1500);
    tracker.TickForTest();

    Assert.AreEqual(0, CountPlayingSources(pool));
    Object.DestroyImmediate(host);
}

[Test]
public void SameLaneOverlap_TwoHoldsRetriggerIndependently()
{
    // HOLD A: lane 0, t=1000, dur=1500, pitch=60. Tap at 1000.
    // HOLD B: lane 0, t=2000, dur=1500, pitch=72. Tap at 2000 — OVERLAPS A.
    var (tracker, pool, audioSync, host) = BuildTracker();
    audioSync.SetIsPlayingForTest(true);

    var a = MakeNote(lane: 0, hitMs: 1000, durMs: 1500, pitch: 60);
    tracker.OnHoldStartTapAccepted(a, tapTimeMs: 1000);
    // Advance to 1250: A retriggers (pitch 60).
    audioSync.SetSongTimeMsForTest(1250);
    tracker.TickForTest();
    // Advance to 2000: B starts; A still holding.
    audioSync.SetSongTimeMsForTest(2000);
    tracker.TickForTest();  // A retriggers a couple more times between 1500 and 2000

    var b = MakeNote(lane: 0, hitMs: 2000, durMs: 1500, pitch: 72);
    tracker.OnHoldStartTapAccepted(b, tapTimeMs: 2000);

    // Verify both A and B are in holdAudio via internal hook.
    Assert.AreEqual(2, tracker.HoldAudioCountForTest,
        "Both A and B must coexist in holdAudio after B starts; lane-keyed storage would collapse to 1");

    Object.DestroyImmediate(host);
}

[Test]
public void NoRetrigger_AfterCompletedTransition()
{
    var (tracker, pool, audioSync, host) = BuildTracker();
    var note = MakeNote(lane: 0, hitMs: 1000, durMs: 500, pitch: 60);
    tracker.OnHoldStartTapAccepted(note, tapTimeMs: 1000);
    audioSync.SetIsPlayingForTest(true);

    // Hold ends at 1500 → Completed.
    audioSync.SetSongTimeMsForTest(1500);
    tracker.TickForTest();

    int before = tracker.HoldAudioCountForTest;

    audioSync.SetSongTimeMsForTest(1800);
    tracker.TickForTest();  // no new retrigger; entry removed

    Assert.AreEqual(0, tracker.HoldAudioCountForTest,
        "Completed transition should remove entry from holdAudio");
    Object.DestroyImmediate(host);
}

[Test]
public void ResetForRetry_ClearsHoldAudio()
{
    var (tracker, pool, audioSync, host) = BuildTracker();
    var note = MakeNote(lane: 0, hitMs: 1000, durMs: 2000, pitch: 60);
    tracker.OnHoldStartTapAccepted(note, tapTimeMs: 1000);

    Assert.AreEqual(1, tracker.HoldAudioCountForTest);
    tracker.ResetForRetry();
    Assert.AreEqual(0, tracker.HoldAudioCountForTest);

    Object.DestroyImmediate(host);
}
```

**Note on test hooks required:** these tests reference `tracker.SetDependenciesForTest(...)`, `tracker.TickForTest()`, `tracker.HoldAudioCountForTest`, `note.SetForTest(...)`, `audioSync.SetSongTimeMsForTest(...)`, `audioSync.SetIsPlayingForTest(...)`, `audioSync.SetIsPausedForTest(...)`. These do not exist yet. Add them in Step 3.5 as `internal` members guarded by `InternalsVisibleTo("KeyFlow.Tests.EditMode")` (already set in `JudgmentSystem.cs:6`).

Before moving on, check the assembly attribute — `InternalsVisibleTo` must be applied at assembly level exactly once. Since it is set in `JudgmentSystem.cs`, we do NOT add it again.

- [ ] **Step 3.3: Compile and confirm everything fails to compile**

Open Unity Editor; compilation should fail because `tracker.OnHoldStartTapAccepted` still has the old signature, and all the `*ForTest` hooks don't exist yet.

Expected: compile errors on HoldAudioRetriggerTests.cs — "OnHoldStartTapAccepted does not take 2 arguments," "HoldTracker does not contain a definition for SetDependenciesForTest", etc.

This is fine. The upcoming implementation steps add these.

- [ ] **Step 3.4: Add test hooks to AudioSyncManager and NoteController**

**`Assets/Scripts/Gameplay/AudioSyncManager.cs`** — check if these hooks already exist. If not, add:

```csharp
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
internal void SetSongTimeMsForTest(int ms) => /* assign to private song time field */;
internal void SetIsPlayingForTest(bool v) => /* assign to IsPlaying backing field */;
internal void SetIsPausedForTest(bool v) => /* assign to IsPaused backing field */;
#endif
```

Look at existing AudioSyncManager implementation first — the backing fields might already be settable via reflection-free test hooks. If the properties `SongTimeMs`, `IsPlaying`, `IsPaused` wrap private fields, add `internal` setters with minimal surface.

**`Assets/Scripts/Gameplay/NoteController.cs`** — add:

```csharp
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
internal void SetForTest(int lane, int hitTimeMs, int durMs, int pitch, NoteType type)
{
    this.lane = lane;
    this.hitTimeMs = hitTimeMs;
    this.durMs = durMs;
    this.pitch = pitch;
    this.noteType = type;
    // Do NOT flip `initialized` — Update guards on it, but the tests don't call Update.
}
#endif
```

- [ ] **Step 3.5: Implement HoldTracker retrigger**

Edit `Assets/Scripts/Gameplay/HoldTracker.cs` — this is the core change. Full replacement for readability (original is short):

```csharp
using System.Collections.Generic;
using UnityEngine;
using KeyFlow.Feedback;

namespace KeyFlow
{
    public class HoldTracker : MonoBehaviour
    {
        private const int   HOLD_RETRIGGER_INTERVAL_MS = 250;
        private const float HOLD_RETRIGGER_VOLUME      = 0.7f;

        [SerializeField] private TapInputHandler tapInput;
        [SerializeField] private AudioSyncManager audioSync;
        [SerializeField] private JudgmentSystem judgmentSystem;
        [SerializeField] private AudioSamplePool audioPool;
        [SerializeField] private LaneGlowController laneGlow;

        private readonly HoldStateMachine stateMachine = new HoldStateMachine();
        private readonly Dictionary<int, NoteController> idToNote = new Dictionary<int, NoteController>();
        private readonly HashSet<int> pressed = new HashSet<int>();
        private readonly List<HoldTransition> transitionBuffer = new List<HoldTransition>();

        private struct HoldAudioState { public int pitch; public int lastRetriggerMs; }
        private readonly Dictionary<int, HoldAudioState> holdAudio
            = new Dictionary<int, HoldAudioState>(LaneLayout.LaneCount * 2);

        public void ResetForRetry()
        {
            stateMachine.Clear();
            idToNote.Clear();
            holdAudio.Clear();
            if (laneGlow != null) laneGlow.Clear();
        }

        public void OnHoldStartTapAccepted(NoteController note, int tapTimeMs)
        {
            int endMs = note.HitTimeMs + note.DurMs;
            int id = stateMachine.Register(note.Lane, note.HitTimeMs, endMs);
            stateMachine.OnStartTapAccepted(id);
            idToNote[id] = note;
            holdAudio[id] = new HoldAudioState
            {
                pitch = note.Pitch,
                lastRetriggerMs = tapTimeMs,
            };
            if (laneGlow != null) laneGlow.On(note.Lane);
        }

        private void Update()
        {
            if (!audioSync.IsPlaying || audioSync.IsPaused) return;
            if (idToNote.Count == 0 && holdAudio.Count == 0) return;

            pressed.Clear();
            for (int lane = 0; lane < LaneLayout.LaneCount; lane++)
                if (tapInput.IsLanePressed(lane)) pressed.Add(lane);

            stateMachine.Tick(audioSync.SongTimeMs, pressed, transitionBuffer);
            foreach (var t in transitionBuffer)
            {
                if (!idToNote.TryGetValue(t.id, out var note)) continue;

                if (t.newState == HoldState.Completed)
                {
                    note.MarkHoldCompleted();
                    if (laneGlow != null) laneGlow.Off(note.Lane);
                }
                else if (t.newState == HoldState.Broken)
                {
                    judgmentSystem.HandleHoldBreak(note);
                    note.MarkHoldBroken();
                    if (laneGlow != null) laneGlow.Off(note.Lane);
                }
                idToNote.Remove(t.id);
                holdAudio.Remove(t.id);
            }

            // Retrigger loop — after transitions so Completed/Broken entries are gone.
            int songMs = audioSync.SongTimeMs;
            foreach (var kv in holdAudio)
            {
                var st = kv.Value;
                if (songMs - st.lastRetriggerMs < HOLD_RETRIGGER_INTERVAL_MS) continue;
                audioPool.PlayForPitch(st.pitch, HOLD_RETRIGGER_VOLUME);
                st.lastRetriggerMs = songMs;
                holdAudio[kv.Key] = st;  // value-only write; no key add/remove during iteration
            }
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal int HoldAudioCountForTest => holdAudio.Count;

        internal void SetDependenciesForTest(
            AudioSyncManager audioSync,
            AudioSamplePool audioPool,
            LaneGlowController laneGlow = null,
            TapInputHandler tapInput = null,
            JudgmentSystem judgmentSystem = null)
        {
            this.audioSync = audioSync;
            this.audioPool = audioPool;
            this.laneGlow = laneGlow;
            this.tapInput = tapInput;
            this.judgmentSystem = judgmentSystem;
        }

        internal void TickForTest() => Update();
#endif
    }
}
```

- [ ] **Step 3.6: Update JudgmentSystem to pass tapTimeMs**

Edit `Assets/Scripts/Gameplay/JudgmentSystem.cs:116`:

```csharp
// Before:
holdTracker.OnHoldStartTapAccepted(closest);
// After:
holdTracker.OnHoldStartTapAccepted(closest, tapTimeMs);
```

(`tapTimeMs` is already a local variable in the enclosing `HandleTap(int tapTimeMs, int tapLane)` method.)

- [ ] **Step 3.7: Also update any existing HoldTrackerTests — check for them**

Run:
```
grep -rn "OnHoldStartTapAccepted" Assets/
```

Expected: matches in `HoldTracker.cs` (def), `JudgmentSystem.cs:116` (caller — already updated), `HoldAudioRetriggerTests.cs` (new tests — already use new signature). If any other file references the old signature, update it.

- [ ] **Step 3.8: Run EditMode tests**

From Unity Editor: run the full EditMode test suite. Confirm:
- All `HoldAudioRetriggerTests` pass.
- All pre-existing tests still pass (`HoldStateMachineTests`, `AudioSamplePoolTests`, `JudgmentSystemTests`, etc.).
- Total count = prior count + 7 new tests (or however many you wrote).

- [ ] **Step 3.9: Commit**

```
git add Assets/Scripts/Gameplay/HoldTracker.cs \
        Assets/Scripts/Gameplay/JudgmentSystem.cs \
        Assets/Scripts/Gameplay/AudioSyncManager.cs \
        Assets/Scripts/Gameplay/NoteController.cs \
        Assets/Tests/EditMode/HoldAudioRetriggerTests.cs
git commit -m "$(cat <<'EOF'
feat(w6-sp8): hold-note audio retrigger at 250 ms, 0.7 volume

HoldTracker now seeds an id-keyed holdAudio dict on tap-accept and
retriggers each held pitch every 250 ms at volume 0.7. Id-keying (not
lane-keying) accommodates the 13 same-lane HOLD overlaps currently
shipped in Clair de Lune NORMAL + Für Elise NORMAL.

OnHoldStartTapAccepted gains tapTimeMs parameter so retrigger cadence
anchors to the player's audible first tap rather than chart-nominal
hit time.

Dict foreach uses struct enumerator (SP3-verified GC-free); value-only
indexer writes during iteration are safe in Mono/IL2CPP.

Adds 7 EditMode tests including same-lane overlap regression guard.
Test hooks (SetDependenciesForTest, TickForTest, HoldAudioCountForTest)
gated by UNITY_EDITOR || UNITY_INCLUDE_TESTS.
EOF
)"
```

---

## Task 4: LaneGlowController

**Spec reference:** §4.2 (architecture, pulse formula, GC invariants)

**Files:**
- Create: `Assets/Scripts/Feedback/LaneGlowController.cs`
- Create: `Assets/Tests/EditMode/LaneGlowControllerTests.cs`

- [ ] **Step 4.1: Write failing tests**

Create `Assets/Tests/EditMode/LaneGlowControllerTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.Feedback;

namespace KeyFlow.Tests.EditMode
{
    public class LaneGlowControllerTests
    {
        private static (LaneGlowController controller, SpriteRenderer[] sprites, GameObject host)
            Build()
        {
            var host = new GameObject("LaneGlow");
            var sprites = new SpriteRenderer[LaneLayout.LaneCount];
            for (int i = 0; i < LaneLayout.LaneCount; i++)
            {
                var child = new GameObject($"Glow_{i}");
                child.transform.SetParent(host.transform);
                sprites[i] = child.AddComponent<SpriteRenderer>();
                sprites[i].color = new Color(1, 1, 1, 0);
            }
            var ctrl = host.AddComponent<LaneGlowController>();
            ctrl.SetSpritesForTest(sprites);
            return (ctrl, sprites, host);
        }

        [Test]
        public void InitialState_AllAlphaZero()
        {
            var (ctrl, sprites, host) = Build();
            ctrl.TickForTest();
            for (int i = 0; i < sprites.Length; i++)
                Assert.AreEqual(0f, sprites[i].color.a, 1e-5f, $"lane {i}");
            Object.DestroyImmediate(host);
        }

        [Test]
        public void On_SingleLane_AlphaGreaterThanZero()
        {
            var (ctrl, sprites, host) = Build();
            ctrl.On(1);
            ctrl.TickForTest();
            Assert.Greater(sprites[1].color.a, 0f);
            Assert.AreEqual(0f, sprites[0].color.a, 1e-5f);
            Assert.AreEqual(0f, sprites[2].color.a, 1e-5f);
            Assert.AreEqual(0f, sprites[3].color.a, 1e-5f);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Off_AfterOn_ResetsAlphaToZero()
        {
            var (ctrl, sprites, host) = Build();
            ctrl.On(1);
            ctrl.TickForTest();
            Assert.Greater(sprites[1].color.a, 0f);

            ctrl.Off(1);
            ctrl.TickForTest();
            Assert.AreEqual(0f, sprites[1].color.a, 1e-5f);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void OnOn_Idempotent()
        {
            var (ctrl, sprites, host) = Build();
            ctrl.On(1);
            ctrl.On(1);
            ctrl.Off(1);
            ctrl.TickForTest();
            Assert.AreEqual(0f, sprites[1].color.a, 1e-5f);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Clear_ResetsAllActiveLanes()
        {
            var (ctrl, sprites, host) = Build();
            for (int i = 0; i < LaneLayout.LaneCount; i++) ctrl.On(i);
            ctrl.TickForTest();
            for (int i = 0; i < LaneLayout.LaneCount; i++)
                Assert.Greater(sprites[i].color.a, 0f);

            ctrl.Clear();
            ctrl.TickForTest();
            for (int i = 0; i < LaneLayout.LaneCount; i++)
                Assert.AreEqual(0f, sprites[i].color.a, 1e-5f);

            Object.DestroyImmediate(host);
        }
    }
}
```

- [ ] **Step 4.2: Confirm tests fail**

Run EditMode tests → LaneGlowControllerTests. Expected: all fail to compile (class doesn't exist yet).

- [ ] **Step 4.3: Implement LaneGlowController**

Create `Assets/Scripts/Feedback/LaneGlowController.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace KeyFlow.Feedback
{
    public class LaneGlowController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] glowSprites;
        [SerializeField] private AudioSyncManager audioSync;

        private readonly HashSet<int> activeLanes = new HashSet<int>(LaneLayout.LaneCount);

        public void On(int lane)  => activeLanes.Add(lane);
        public void Off(int lane)
        {
            activeLanes.Remove(lane);
            if (lane >= 0 && lane < glowSprites.Length && glowSprites[lane] != null)
                SetAlpha(glowSprites[lane], 0f);
        }
        public void Clear()
        {
            activeLanes.Clear();
            for (int i = 0; i < glowSprites.Length; i++)
                if (glowSprites[i] != null) SetAlpha(glowSprites[i], 0f);
        }

        private void Update()
        {
            if (audioSync != null && audioSync.IsPaused) return;

            float pulse = 0.3f + 0.2f * Mathf.Sin(Time.time * 6f);
            for (int i = 0; i < glowSprites.Length; i++)
            {
                if (glowSprites[i] == null) continue;
                float targetAlpha = activeLanes.Contains(i) ? pulse : 0f;
                SetAlpha(glowSprites[i], targetAlpha);
            }
        }

        private static void SetAlpha(SpriteRenderer sr, float a)
        {
            var c = sr.color;
            c.a = a;
            sr.color = c;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal void SetSpritesForTest(SpriteRenderer[] sprites) => glowSprites = sprites;
        internal void TickForTest() => Update();
#endif
    }
}
```

- [ ] **Step 4.4: Run tests, confirm pass**

All 5 `LaneGlowControllerTests` green.

- [ ] **Step 4.5: Commit**

```
git add Assets/Scripts/Feedback/LaneGlowController.cs \
        Assets/Scripts/Feedback/LaneGlowController.cs.meta \
        Assets/Tests/EditMode/LaneGlowControllerTests.cs
git commit -m "$(cat <<'EOF'
feat(w6-sp8): LaneGlowController for hold-note visual feedback

Per-lane judgment-line glow sprites pulse (~1 Hz, alpha 0.1–0.5) while
the corresponding lane has an active HOLD. Pulse math is
stack-only (Mathf.Sin + struct Color); no per-frame heap allocation.

HoldTracker calls On/Off/Clear; SceneBuilder (next task) will wire the
sprite array.
EOF
)"
```

---

## Task 5: SceneBuilder integration

**Spec reference:** §4.4

**Files:**
- Modify: `Assets/Editor/SceneBuilder.cs`
- Regenerate: `Assets/Scenes/GameplayScene.unity` (via editor menu)

- [ ] **Step 5.1: Read SceneBuilder.cs to locate integration points**

Relevant sections of `Assets/Editor/SceneBuilder.cs`:
- `BuildManagers` (line 186) — creates all manager GameObjects and wires them. This is where `HoldTracker` is created, and where new `LaneGlow` siblings need to go.
- `EnsureWhiteSprite()` (line 1297) — provides the white sprite reused for glow.
- `LaneLayout.LaneToX(lane, laneAreaWidth)` — pattern from existing lane divider construction.

- [ ] **Step 5.2: Add BuildLaneGlow helper**

Insert into `SceneBuilder.cs` (near other `BuildXxx` helpers; exact placement below `BuildJudgmentLine`):

```csharp
private static LaneGlowController BuildLaneGlow(
    Sprite whiteSprite,
    Transform managersParent,
    AudioSyncManager audioSync)
{
    var root = new GameObject("LaneGlow");
    root.transform.SetParent(managersParent, worldPositionStays: false);
    var controller = root.AddComponent<LaneGlowController>();

    var sprites = new SpriteRenderer[LaneLayout.LaneCount];
    float tileWidth = LaneAreaWidth / LaneLayout.LaneCount;

    for (int i = 0; i < LaneLayout.LaneCount; i++)
    {
        var go = new GameObject($"Glow_{i}");
        go.transform.SetParent(root.transform, worldPositionStays: false);
        go.transform.position = new Vector3(LaneLayout.LaneToX(i, LaneAreaWidth), JudgmentY, 0);
        go.transform.localScale = new Vector3(tileWidth, 0.3f, 1);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = whiteSprite;
        sr.color = new Color(1f, 1f, 1f, 0f);
        sr.sortingOrder = 0;
        sprites[i] = sr;
    }

    SetArrayField(controller, "glowSprites", sprites);
    SetField(controller, "audioSync", audioSync);
    return controller;
}
```

- [ ] **Step 5.3: Call BuildLaneGlow from BuildManagers and wire HoldTracker**

Inside `BuildManagers(...)` (line 186), locate where `HoldTracker` is created and wired. Add:

```csharp
// After holdTracker is created and its audioSync/tapInput/judgmentSystem fields are wired:
var laneGlow = BuildLaneGlow(whiteSprite, managers.transform, audioSync);
SetField(holdTracker, "laneGlow", laneGlow);
SetField(holdTracker, "audioPool", samplePool);
```

(The `whiteSprite` is already in scope as a local or parameter in `BuildManagers`; if not, pass it through like `pitchSamples` is passed. Check SceneBuilder for the asset load pattern — look for `EnsureWhiteSprite()` callers.)

- [ ] **Step 5.4: Regenerate the scene via menu**

In Unity Editor: Menu → `KeyFlow/Build W4 Scene` (or whatever the SP7-consolidated menu item is now called — check `[MenuItem]` attribute in SceneBuilder).

Save the scene. Commit `Assets/Scenes/GameplayScene.unity`.

- [ ] **Step 5.5: EditMode sanity pass**

Run the full EditMode test suite. Expected: all green. No gameplay regression from SceneBuilder changes (EditMode doesn't load the scene, so this verifies only that compile is clean).

- [ ] **Step 5.6: Play in Editor sanity pass**

Open `GameplayScene.unity`, press Play, tap through a few notes including a hold. Verify:
- Held lane's judgment-line area visibly pulses.
- Held note plays the first tap pitch + roughly 3–4 retriggers per second at lower volume.
- Release → glow stops and no more retriggers.

If anything is off, diagnose before committing scene regeneration. Common issues (from SP4/SP6 carry-overs):
- LaneGlowController fields null → confirm SetField/SetArrayField order in Task 5.2/5.3.
- Glow sprite behind background → confirm sortingOrder 0 is above background -100.

- [ ] **Step 5.7: Commit**

```
git add Assets/Editor/SceneBuilder.cs Assets/Scenes/GameplayScene.unity
git commit -m "$(cat <<'EOF'
chore(w6-sp8): wire LaneGlow and HoldTracker audio pool in SceneBuilder

BuildLaneGlow creates LaneGlow/Glow_{0..3} under Managers, attaches
LaneGlowController with glowSprites array wired, and pins sortingOrder
= 0 (same layer as JudgmentLine, below note tiles).

HoldTracker gets new SerializeField wirings for laneGlow and audioPool
to complete the SP8 runtime loop. GameplayScene regenerated.
EOF
)"
```

---

## Task 6: Device playtest + completion report

**Spec reference:** §7 (Device playtest), §9 (Success criteria)

**Files:**
- Create: `docs/superpowers/reports/2026-04-23-w6-sp8-completion.md`
- Build: `Builds/keyflow-w6-sp8.apk` (local — not committed)

- [ ] **Step 6.1: Close interactive Unity Editor (per memory/feedback_unity_batch_mode.md)**

Unity 6 IL2CPP batch build fails at step 1101/1110 if the interactive Editor is open on the same project. Close before the next step.

- [ ] **Step 6.2: Build release APK**

From a terminal (foreground, no run_in_background per `memory/feedback_unity_batch_mode.md`):
```
"$UNITY_EXE" -batchmode -nographics -projectPath "$(pwd)" \
  -executeMethod KeyFlow.Editor.ApkBuilder.Build \
  -logFile - 2>&1 | tail -120
```

Expected: `Builds/keyflow-w6-sp2.apk` (release APK name unchanged from SP3 onward per memory), exit code 0.

- [ ] **Step 6.3: Install and playtest on S22 (R5CT21A31QB)**

```
adb install -r Builds/keyflow-w6-sp2.apk
```

Playtest checklist:
1. Entertainer Normal full run: HOLD density feels reduced vs. prior; lane glow visible during all holds; audio retrigger audible during each hold; GC.Collect stays 0.
2. Für Elise Normal partial run (grace-note + sustained sections): ornaments unaffected; sustained chords feel actively "held" (glow + retrigger); GC.Collect stays 0.
3. Ode to Joy Normal run: note that HOLD-heavy feel will remain (see spec §4.1 table); document whether it's still acceptable or if a follow-up SP is warranted.
4. Clair de Lune Normal partial run: same observation.
5. Retry mid-hold: glow + laneAudio clear cleanly.
6. Pause mid-hold: glow freezes (no pulse), retriggers suppressed; resume continues cleanly.

- [ ] **Step 6.4: Profiler attach during Entertainer Normal**

Build profile APK:
```
"$UNITY_EXE" -batchmode -nographics -projectPath "$(pwd)" \
  -executeMethod KeyFlow.Editor.ApkBuilder.BuildProfile \
  -logFile - 2>&1 | tail -120
```

Install profile build, attach Unity Profiler. Confirm:
- `GC.Collect == 0` during 2-minute Entertainer Normal session.
- Per-frame allocation attributable to `HoldTracker.Update` is 0 B.
- No regression vs. SP3 baseline.

If non-zero allocations appear in `HoldTracker.Update` or `LaneGlowController.Update`, diagnose — likely culprits: Dictionary rehashing (unlikely, capacity 8 is preallocated), `HashSet<int>.Enumerator` boxing (don't iterate activeLanes with `foreach` on `ISet<int>` — use `for` over lane indices).

- [ ] **Step 6.5: Write completion report**

Template: `docs/superpowers/reports/2026-04-22-w6-sp3-completion.md` is the most recent perf-focused completion report; copy its structure. Create `docs/superpowers/reports/2026-04-23-w6-sp8-completion.md` with:

- Summary: commit hash of merged SP8, user-confirmed playtest results
- Threshold change: before/after HOLD density table (from Step 1.8)
- LaneGlowController: integration points, sortingOrder rationale
- Retrigger: timing, volume, id-keying rationale + same-lane-overlap evidence
- Test suite growth: N before → N+12 after (or whatever the actual count is)
- Device playtest results: per-song verdicts including any carry-overs for Ode to Joy / Clair de Lune
- Profiler: GC.Collect count, per-frame alloc in hold-bearing sections
- APK size delta: MB before (33.70 from SP3) → MB after
- New carry-overs (if any): e.g., "per-song threshold for BPM-60 songs warranted?"

- [ ] **Step 6.6: Commit the completion report**

```
git add docs/superpowers/reports/2026-04-23-w6-sp8-completion.md
git commit -m "docs(w6-sp8): completion report"
```

- [ ] **Step 6.7: Update memory**

Write a new memory file `memory/project_w6_sp8_complete.md` following the template of `project_w6_sp3_complete.md`:
- Date merged, commit hash
- Key technical decisions (id-keyed holdAudio; 500 ms threshold; lane-glow visual pulse)
- Device playtest verdict
- Any new carry-overs
- Note whether Ode to Joy / Clair de Lune remain hold-dominant

Add one line to `memory/MEMORY.md` index pointing at the new file.

Commit:
```
git add memory/MEMORY.md memory/project_w6_sp8_complete.md
git commit -m "memory(w6-sp8): record sub-project completion"
```

---

## Final Verification Checklist

Before considering SP8 done:

- [ ] Python: `pytest -q` green in `tools/midi_to_kfchart/`
- [ ] Unity EditMode: full suite green, count = prior + 12
- [ ] Device (S22) release APK: user confirms all three issues ("#1 덜 나온다 / #2 화면 살아있다 / #3 소리 이어진다")
- [ ] Profiler: GC.Collect = 0 during Entertainer Normal
- [ ] APK size delta ≤ +100 KB vs. SP3 baseline (33.70 MB → ≤ 33.80 MB)
- [ ] Completion report written and committed
- [ ] Memory updated

---

## Notes for the implementer

- **Unity batch-mode rules** (see `memory/feedback_unity_batch_mode.md`): `-runTests` must NOT be combined with `-quit`; `-executeMethod` CAN use `-quit`; batch-build fails if interactive Editor is open on same project. All Unity CLI commands must run foreground.
- **SP3 GC baseline**: the retrigger loop's `foreach` on `Dictionary<int, HoldAudioState>` is proven GC-free only if `HoldAudioState` is a `struct` and the dictionary is reused without rehashing. Initial capacity `LaneCount * 2 = 8` exceeds max realistic concurrent-hold count on shipped charts.
- **SceneBuilder consolidation** (see `memory/project_w6_sp7_complete.md`): do not re-introduce any secondary Wireup step. All SP8 wiring lives inside `SceneBuilder.BuildManagers`.
- **Test hook discipline**: all `*ForTest` members gated by `#if UNITY_EDITOR || UNITY_INCLUDE_TESTS`. Never leak them into release builds.
- **Frequent commits**: each Task has a single commit at the end, but individual steps within a task can be split into smaller commits if that helps bisection.
