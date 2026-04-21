# KeyFlow W5: MIDI → .kfchart Pipeline + ChartLoader Coroutine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a pytest-verified Python pipeline that converts MIDI → .kfchart, use it to generate Für Elise Normal, and refactor ChartLoader to a coroutine with hardened validation.

**Architecture:** Python pipeline lives outside Unity in `tools/midi_to_kfchart/` — pure `mido`-based modules composed by a thin CLI, no Unity dependency. Unity side swaps ChartLoader's Android busy-wait for a coroutine + callback API, adds list-level validation, and replaces `Mathf.Clamp` with `System.Math.Clamp` in two files.

**Tech Stack:** Python 3.11+, `mido` 1.3.x, `pytest` 8.x (Python side) · Unity 6.3 LTS, C# `UnityWebRequest`, `System.Math` (Unity side).

**Spec:** [docs/superpowers/specs/2026-04-21-keyflow-w5-pipeline-design.md](../specs/2026-04-21-keyflow-w5-pipeline-design.md)

---

## Pre-flight

Run once before starting:

- [ ] Verify Python 3.11+ available: `python --version` (Windows) — expect `Python 3.11.x` or higher.
- [ ] Verify Unity EditMode baseline is green (88 tests) before any edits. See CLAUDE memory *Unity batch mode* — always run foreground.

---

## Phase A — Python Pipeline (Spec §3)

### Task 1: Scaffold `tools/midi_to_kfchart/`

**Files:**
- Create: `tools/midi_to_kfchart/requirements.txt`
- Create: `tools/midi_to_kfchart/README.md`
- Create: `tools/midi_to_kfchart/pipeline/__init__.py` (empty)
- Create: `tools/midi_to_kfchart/tests/__init__.py` (empty)
- Create: `tools/midi_to_kfchart/tests/conftest.py`
- Modify: `.gitignore` (append `midi_sources/` and `tools/midi_to_kfchart/.venv/`)

- [ ] **Step 1: Create requirements.txt**

```
mido==1.3.2
pytest==8.3.3
```

- [ ] **Step 2: Create README.md skeleton**

```markdown
# midi_to_kfchart

Python pipeline that converts MIDI files into KeyFlow `.kfchart` charts.

## Setup (Windows)

    python -m venv .venv
    .venv\Scripts\activate
    pip install -r requirements.txt

## Run tests

    pytest -q

## Single-file conversion

    python midi_to_kfchart.py <input.mid> --song-id <id> --title "<T>" \
      --composer "<C>" --difficulty NORMAL --target-nps 3.5 --bpm 72 \
      --duration-ms 45000 --merge-into Assets/StreamingAssets/charts/<id>.kfchart

## Batch conversion

    python midi_to_kfchart.py --batch batch.yaml

See `batch.yaml.example` for schema.

## Tuning loop

1. Run pipeline → `.kfchart` output.
2. Play in Unity Editor.
3. Adjust `--target-nps` or hand-edit the JSON (hand-edits will be lost on re-run).
```

- [ ] **Step 3: Create tests/conftest.py**

```python
"""Shared pytest fixtures for midi_to_kfchart tests."""
import sys
from pathlib import Path

# Allow `from pipeline import ...` from test files without installing as package.
ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
```

- [ ] **Step 4: Append .gitignore entries**

Append at bottom of repo root `.gitignore`:

```
# W5 pipeline
midi_sources/
tools/midi_to_kfchart/.venv/
tools/midi_to_kfchart/**/__pycache__/
```

- [ ] **Step 5: Create venv and install deps**

```bash
cd tools/midi_to_kfchart
python -m venv .venv
.venv/Scripts/pip install -r requirements.txt
```

Expected: `Successfully installed mido-1.3.2 pytest-8.3.3 ...`.

- [ ] **Step 6: Commit**

```bash
git add tools/midi_to_kfchart/requirements.txt tools/midi_to_kfchart/README.md \
        tools/midi_to_kfchart/pipeline/__init__.py tools/midi_to_kfchart/tests/__init__.py \
        tools/midi_to_kfchart/tests/conftest.py .gitignore
git commit -m "chore(w5): scaffold tools/midi_to_kfchart"
```

---

### Task 2: `parser.py` — MIDI → RawNote

**Files:**
- Create: `tools/midi_to_kfchart/pipeline/parser.py`
- Create: `tools/midi_to_kfchart/tests/test_parser.py`

**Responsibility:** Parse a `.mid` file into a flat list of `RawNote(t_ms, pitch, dur_ms)` dictionaries, merging all tracks. Uses `mido.MidiFile` iteration with cumulative time accumulation.

- [ ] **Step 1: Write failing tests**

Write `tests/test_parser.py`:

```python
import mido
from pathlib import Path
from pipeline.parser import parse_midi

def _make_midi(events):
    """events: list[(delta_ticks, msg_type, note, velocity)]"""
    mid = mido.MidiFile(type=0, ticks_per_beat=480)
    track = mido.MidiTrack()
    mid.tracks.append(track)
    track.append(mido.MetaMessage("set_tempo", tempo=500000, time=0))  # 120 bpm
    for delta, mtype, note, vel in events:
        track.append(mido.Message(mtype, note=note, velocity=vel, time=delta))
    return mid

def test_parse_single_note(tmp_path):
    mid = _make_midi([
        (0, "note_on", 60, 80),
        (480, "note_off", 60, 0),  # 1 beat = 0.5s at 120bpm
    ])
    p = tmp_path / "a.mid"
    mid.save(p)
    notes = parse_midi(str(p))
    assert len(notes) == 1
    assert notes[0]["pitch"] == 60
    assert notes[0]["t_ms"] == 0
    assert 495 <= notes[0]["dur_ms"] <= 505  # ~500ms

def test_parse_empty_midi(tmp_path):
    mid = mido.MidiFile(type=0, ticks_per_beat=480)
    mid.tracks.append(mido.MidiTrack())
    p = tmp_path / "empty.mid"
    mid.save(p)
    assert parse_midi(str(p)) == []

def test_parse_note_off_zero_velocity_treated_as_off(tmp_path):
    # Some MIDI exporters encode note-off as note_on velocity=0.
    mid = _make_midi([
        (0, "note_on", 60, 80),
        (480, "note_on", 60, 0),
    ])
    p = tmp_path / "b.mid"
    mid.save(p)
    notes = parse_midi(str(p))
    assert len(notes) == 1
    assert 495 <= notes[0]["dur_ms"] <= 505

def test_parse_multi_track_merged(tmp_path):
    mid = mido.MidiFile(type=1, ticks_per_beat=480)
    t1 = mido.MidiTrack(); mid.tracks.append(t1)
    t1.append(mido.MetaMessage("set_tempo", tempo=500000, time=0))
    t1.append(mido.Message("note_on", note=60, velocity=80, time=0))
    t1.append(mido.Message("note_off", note=60, velocity=0, time=240))
    t2 = mido.MidiTrack(); mid.tracks.append(t2)
    t2.append(mido.Message("note_on", note=72, velocity=80, time=240))
    t2.append(mido.Message("note_off", note=72, velocity=0, time=240))
    p = tmp_path / "multi.mid"
    mid.save(p)
    notes = sorted(parse_midi(str(p)), key=lambda n: (n["t_ms"], n["pitch"]))
    assert len(notes) == 2
    assert notes[0]["pitch"] == 60
    assert notes[1]["pitch"] == 72
```

- [ ] **Step 2: Run tests — expect fail**

```
cd tools/midi_to_kfchart && .venv/Scripts/pytest tests/test_parser.py -v
```

Expected: `ImportError: cannot import name 'parse_midi' from 'pipeline.parser'`.

- [ ] **Step 3: Implement `parser.py`**

```python
"""MIDI file → list[RawNote] where RawNote = {"t_ms", "pitch", "dur_ms"}."""
import mido


def parse_midi(path: str) -> list[dict]:
    mid = mido.MidiFile(path)
    notes: list[dict] = []
    active: dict[int, float] = {}  # pitch -> start_sec
    abs_sec = 0.0
    for msg in mid:  # iterator yields with msg.time = delta in seconds
        abs_sec += msg.time
        if msg.type == "note_on" and msg.velocity > 0:
            if msg.note not in active:
                active[msg.note] = abs_sec
        elif (msg.type == "note_off") or (msg.type == "note_on" and msg.velocity == 0):
            start = active.pop(msg.note, None)
            if start is not None:
                notes.append({
                    "t_ms": int(round(start * 1000)),
                    "pitch": msg.note,
                    "dur_ms": int(round((abs_sec - start) * 1000)),
                })
    notes.sort(key=lambda n: (n["t_ms"], n["pitch"]))
    return notes
```

- [ ] **Step 4: Run tests — expect pass**

```
.venv/Scripts/pytest tests/test_parser.py -v
```

Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add tools/midi_to_kfchart/pipeline/parser.py tools/midi_to_kfchart/tests/test_parser.py
git commit -m "feat(w5): parser.py MIDI to RawNote"
```

---

### Task 3: `chord_reducer.py` — monophonic collapse

**Files:**
- Create: `tools/midi_to_kfchart/pipeline/chord_reducer.py`
- Create: `tools/midi_to_kfchart/tests/test_chord_reducer.py`

**Responsibility:** Given a list of RawNotes sorted by `t_ms`, collapse all entries sharing the same `t_ms` to the single entry with the highest `pitch` (melody bias). Spec §3.4.

- [ ] **Step 1: Write failing tests**

```python
from pipeline.chord_reducer import collapse

def _n(t, pitch, dur=0): return {"t_ms": t, "pitch": pitch, "dur_ms": dur}

def test_same_timestamp_keeps_highest_pitch():
    result = collapse([_n(0, 60), _n(0, 64), _n(0, 67)])
    assert len(result) == 1
    assert result[0]["pitch"] == 67

def test_different_timestamps_preserved():
    result = collapse([_n(0, 60), _n(100, 64)])
    assert [n["pitch"] for n in result] == [60, 64]

def test_empty_input():
    assert collapse([]) == []
```

- [ ] **Step 2: Run — expect fail**

- [ ] **Step 3: Implement**

```python
"""Collapse simultaneous notes to a single monophonic line (highest pitch)."""


def collapse(raws: list[dict]) -> list[dict]:
    by_time: dict[int, dict] = {}
    for n in raws:
        existing = by_time.get(n["t_ms"])
        if existing is None or n["pitch"] > existing["pitch"]:
            by_time[n["t_ms"]] = n
    return [by_time[t] for t in sorted(by_time)]
```

- [ ] **Step 4: Run — expect 3 passed**

- [ ] **Step 5: Commit**

```bash
git add tools/midi_to_kfchart/pipeline/chord_reducer.py tools/midi_to_kfchart/tests/test_chord_reducer.py
git commit -m "feat(w5): chord_reducer monophonic collapse"
```

---

### Task 4: `hold_detector.py` — 300ms sustain → HOLD

**Files:**
- Create: `tools/midi_to_kfchart/pipeline/hold_detector.py`
- Create: `tools/midi_to_kfchart/tests/test_hold_detector.py`

**Responsibility:** Given RawNotes, emit TypedNotes with `type` (TAP/HOLD) and normalized `dur`. Threshold 300ms; HOLD `dur_ms` capped at 4000ms. Spec §3.4.

- [ ] **Step 1: Write failing tests**

```python
from pipeline.hold_detector import classify

def _raw(t, pitch, dur): return {"t_ms": t, "pitch": pitch, "dur_ms": dur}

def test_299ms_is_tap():
    typed = classify([_raw(0, 60, 299)])
    assert typed[0]["type"] == "TAP"
    assert typed[0]["dur"] == 0

def test_300ms_is_hold():
    typed = classify([_raw(0, 60, 300)])
    assert typed[0]["type"] == "HOLD"
    assert typed[0]["dur"] == 300

def test_hold_cap_4000ms():
    typed = classify([_raw(0, 60, 8000)])
    assert typed[0]["type"] == "HOLD"
    assert typed[0]["dur"] == 4000

def test_tap_always_dur_zero():
    typed = classify([_raw(0, 60, 0), _raw(100, 61, 50)])
    assert all(n["type"] == "TAP" and n["dur"] == 0 for n in typed)
```

- [ ] **Step 2: Run — expect fail**

- [ ] **Step 3: Implement**

```python
"""Classify RawNotes into TAP/HOLD based on sustain length."""
HOLD_THRESHOLD_MS = 300
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

- [ ] **Step 4: Run — expect 4 passed**

- [ ] **Step 5: Commit**

```bash
git add tools/midi_to_kfchart/pipeline/hold_detector.py tools/midi_to_kfchart/tests/test_hold_detector.py
git commit -m "feat(w5): hold_detector 300ms threshold"
```

---

### Task 5: `density.py` — NPS-target thinning

**Files:**
- Create: `tools/midi_to_kfchart/pipeline/density.py`
- Create: `tools/midi_to_kfchart/tests/test_density.py`

**Responsibility:** Thin TypedNotes when current NPS exceeds `target_nps * 1.1` using deterministic step-drop. No random. Spec §3.4.

- [ ] **Step 1: Write failing tests**

```python
from pipeline.density import thin

def _make(count, t_step=100): return [{"t": i*t_step, "pitch": 60, "type": "TAP", "dur": 0} for i in range(count)]

def test_already_below_target_no_change():
    notes = _make(10)  # 10 notes / 1s = 10 NPS. target 20 -> no thin.
    assert len(thin(notes, target_nps=20, duration_ms=1000)) == 10

def test_above_target_thins():
    notes = _make(20)  # 20 notes / 1s = 20 NPS. target 10 -> thin ~half.
    result = thin(notes, target_nps=10, duration_ms=1000)
    assert 9 <= len(result) <= 13  # target*1.1 = 11 notes
    # determinism: same call returns same result
    assert thin(notes, target_nps=10, duration_ms=1000) == result

def test_empty_passes_through():
    assert thin([], target_nps=10, duration_ms=1000) == []

def test_zero_duration_returns_empty():
    assert thin(_make(5), target_nps=10, duration_ms=0) == []

def test_extreme_ratio_returns_empty():
    # 100 notes / 1s = 100 NPS. target 1 NPS -> step would be <2 -> empty.
    notes = _make(100)
    assert thin(notes, target_nps=1, duration_ms=1000) == []
```

- [ ] **Step 2: Run — expect fail**

- [ ] **Step 3: Implement**

```python
"""NPS-target density thinning. Deterministic, no random."""


def thin(notes: list[dict], target_nps: float, duration_ms: int) -> list[dict]:
    if duration_ms <= 0:
        return []
    if not notes:
        return []
    duration_sec = duration_ms / 1000.0
    current_nps = len(notes) / duration_sec
    allowed_nps = target_nps * 1.1
    if current_nps <= allowed_nps:
        return list(notes)
    keep_ratio = allowed_nps / current_nps  # in (0, 1)
    step = round(1 / (1 - keep_ratio))
    if step < 2:
        return []  # ratio so extreme that every note drops
    return [n for i, n in enumerate(notes) if (i + 1) % step != 0]
```

- [ ] **Step 4: Run — expect 5 passed**

- [ ] **Step 5: Commit**

```bash
git add tools/midi_to_kfchart/pipeline/density.py tools/midi_to_kfchart/tests/test_density.py
git commit -m "feat(w5): density NPS-target thinning"
```

---

### Task 6: `pitch_clamp.py` — MIDI 36~83 octave transpose

**Files:**
- Create: `tools/midi_to_kfchart/pipeline/pitch_clamp.py`
- Create: `tools/midi_to_kfchart/tests/test_pitch_clamp.py`

**Responsibility:** Transpose out-of-range pitches into `[36, 83]` by adding/subtracting whole octaves. Spec §3.4.

- [ ] **Step 1: Write failing tests**

```python
import pytest
from pipeline.pitch_clamp import clamp_pitches

def _n(pitch): return {"t": 0, "pitch": pitch, "type": "TAP", "dur": 0}

def test_35_transposes_to_47():
    # 35 + 12 = 47 (still < 36? 47 >= 36, in range)
    result = clamp_pitches([_n(35)])
    assert result[0]["pitch"] == 47

def test_84_transposes_to_72():
    # 84 - 12 = 72 (>= 36, <= 83, in range)
    result = clamp_pitches([_n(84)])
    assert result[0]["pitch"] == 72

def test_boundary_36_and_83_unchanged():
    assert clamp_pitches([_n(36), _n(83)]) == [_n(36), _n(83)]

def test_double_octave_transpose():
    # 20 needs +24 to reach 44
    result = clamp_pitches([_n(20)])
    assert result[0]["pitch"] == 44
```

- [ ] **Step 2: Run — expect fail**

- [ ] **Step 3: Implement**

```python
"""Clamp pitches into MIDI 36~83 via whole-octave transpose."""
PITCH_MIN = 36
PITCH_MAX = 83


def clamp_pitches(notes: list[dict]) -> list[dict]:
    out: list[dict] = []
    for n in notes:
        p = n["pitch"]
        while p < PITCH_MIN:
            p += 12
        while p > PITCH_MAX:
            p -= 12
        if not (PITCH_MIN <= p <= PITCH_MAX):
            raise ValueError(f"pitch {n['pitch']} could not be clamped into [{PITCH_MIN},{PITCH_MAX}]")
        out.append({**n, "pitch": p})
    return out
```

- [ ] **Step 4: Run — expect 4 passed**

- [ ] **Step 5: Commit**

```bash
git add tools/midi_to_kfchart/pipeline/pitch_clamp.py tools/midi_to_kfchart/tests/test_pitch_clamp.py
git commit -m "feat(w5): pitch_clamp octave transpose"
```

---

### Task 7: `lane_assigner.py` — 4-lane + 3-consecutive relief

**Files:**
- Create: `tools/midi_to_kfchart/pipeline/lane_assigner.py`
- Create: `tools/midi_to_kfchart/tests/test_lane_assigner.py`

**Responsibility:** Map pitch range to 4 lanes (quartiles). After assignment, if 3 consecutive notes share a lane, shift the third to `(lane+1) % 4`; if that still matches the previous two, use `(lane+2) % 4`. Spec §3.4.

- [ ] **Step 1: Write failing tests**

```python
from pipeline.lane_assigner import assign

def _n(t, pitch): return {"t": t, "pitch": pitch, "type": "TAP", "dur": 0}

def test_single_pitch_all_lane_0():
    notes = [_n(i*100, 60) for i in range(3)]
    # 3 consecutive same-pitch -> 3rd shifted
    result = assign(notes)
    assert result[0]["lane"] == 0
    assert result[1]["lane"] == 0
    assert result[2]["lane"] != 0

def test_pitch_range_quartiles():
    notes = [_n(i*100, p) for i, p in enumerate([40, 50, 60, 70])]
    result = assign(notes)
    assert result[0]["lane"] == 0
    assert result[3]["lane"] == 3

def test_three_consecutive_relief():
    # All same pitch -> all would be lane 0 without relief
    notes = [_n(i*100, 60) for i in range(5)]
    result = assign(notes)
    # No 3 consecutive identical lanes anywhere
    for i in range(2, len(result)):
        assert not (result[i]["lane"] == result[i-1]["lane"] == result[i-2]["lane"])

def test_relief_two_step_when_needed():
    # Craft a sequence where simple +1 wouldn't break the run.
    # lanes would be [0, 0, 0, 0] -> after relief the 3rd goes to 1, 4th sees [0,0,1] so OK at 0? 
    # Just assert no triple regardless of strategy.
    notes = [_n(i*100, 60) for i in range(8)]
    result = assign(notes)
    for i in range(2, len(result)):
        assert not (result[i]["lane"] == result[i-1]["lane"] == result[i-2]["lane"])
```

- [ ] **Step 2: Run — expect fail**

- [ ] **Step 3: Implement**

```python
"""Assign notes to 4 lanes by pitch quartile, with 3-consecutive relief."""
LANE_COUNT = 4


def assign(notes: list[dict]) -> list[dict]:
    if not notes:
        return []
    pitches = [n["pitch"] for n in notes]
    pmin, pmax = min(pitches), max(pitches)
    out = [dict(n) for n in notes]

    for n in out:
        if pmax == pmin:
            n["lane"] = 0
        else:
            ratio = (n["pitch"] - pmin) / (pmax - pmin)
            n["lane"] = min(LANE_COUNT - 1, int(ratio * LANE_COUNT))

    # 3-consecutive relief
    for i in range(2, len(out)):
        if out[i]["lane"] == out[i-1]["lane"] == out[i-2]["lane"]:
            candidate = (out[i]["lane"] + 1) % LANE_COUNT
            if i >= 2 and candidate == out[i-1]["lane"] == out[i-2]["lane"]:
                candidate = (out[i]["lane"] + 2) % LANE_COUNT
            out[i]["lane"] = candidate

    return out
```

- [ ] **Step 4: Run — expect 4 passed**

- [ ] **Step 5: Commit**

```bash
git add tools/midi_to_kfchart/pipeline/lane_assigner.py tools/midi_to_kfchart/tests/test_lane_assigner.py
git commit -m "feat(w5): lane_assigner quartile + 3-consec relief"
```

---

### Task 8: `emitter.py` — JSON output + merge + validation

**Files:**
- Create: `tools/midi_to_kfchart/pipeline/emitter.py`
- Create: `tools/midi_to_kfchart/tests/test_emitter.py`

**Responsibility:** Build final `.kfchart` dict. Sort by `t`, validate (non-empty, sorted, ranges, type/dur consistency). Supports `merge_into=<path>` to overlay onto an existing file preserving other difficulties and metadata. Spec §3.4, §4.

- [ ] **Step 1: Write failing tests**

```python
import json, pytest
from pathlib import Path
from pipeline.emitter import to_kfchart, write_kfchart

def _note(t, lane=0, pitch=60, typ="TAP", dur=0):
    return {"t": t, "lane": lane, "pitch": pitch, "type": typ, "dur": dur}

META = {"song_id": "test", "title": "T", "composer": "C", "bpm": 120, "duration_ms": 5000}

def test_sort_and_totalnotes():
    chart = to_kfchart([_note(300), _note(100), _note(200)], META, "EASY")
    notes = chart["charts"]["EASY"]["notes"]
    assert [n["t"] for n in notes] == [100, 200, 300]
    assert chart["charts"]["EASY"]["totalNotes"] == 3

def test_empty_notes_rejected():
    with pytest.raises(ValueError, match="empty"):
        to_kfchart([], META, "EASY")

def test_hold_dur_zero_rejected():
    with pytest.raises(ValueError, match="HOLD"):
        to_kfchart([_note(100, typ="HOLD", dur=0)], META, "EASY")

def test_tap_nonzero_dur_rejected():
    with pytest.raises(ValueError, match="TAP"):
        to_kfchart([_note(100, typ="TAP", dur=50)], META, "EASY")

def test_merge_into_preserves_other_difficulty(tmp_path):
    existing = tmp_path / "x.kfchart"
    existing.write_text(json.dumps({
        "songId": "test", "title": "Old", "composer": "Old", "bpm": 60, "durationMs": 5000,
        "charts": {"EASY": {"totalNotes": 1, "notes": [_note(50)]}}
    }))
    write_kfchart([_note(100)], META, "NORMAL", merge_into=str(existing))
    result = json.loads(existing.read_text())
    assert "EASY" in result["charts"]
    assert "NORMAL" in result["charts"]
    assert result["charts"]["EASY"]["totalNotes"] == 1
    # root meta from META overwrote (bpm 120 vs old 60)
    assert result["bpm"] == 120
```

- [ ] **Step 2: Run — expect fail**

- [ ] **Step 3: Implement**

```python
"""Build and serialize .kfchart JSON."""
import json
from pathlib import Path

PITCH_MIN = 36
PITCH_MAX = 83


def to_kfchart(notes: list[dict], meta: dict, difficulty: str) -> dict:
    if difficulty not in ("EASY", "NORMAL"):
        raise ValueError(f"difficulty must be EASY or NORMAL, got {difficulty}")

    sorted_notes = sorted((dict(n) for n in notes), key=lambda n: n["t"])
    _validate(sorted_notes, difficulty)

    return {
        "songId": meta["song_id"],
        "title": meta["title"],
        "composer": meta["composer"],
        "bpm": meta["bpm"],
        "durationMs": meta["duration_ms"],
        "charts": {
            difficulty: {
                "totalNotes": len(sorted_notes),
                "notes": [
                    {"t": n["t"], "lane": n["lane"], "pitch": n["pitch"],
                     "type": n["type"], "dur": n["dur"]}
                    for n in sorted_notes
                ],
            }
        },
    }


def _validate(notes: list[dict], difficulty: str) -> None:
    if not notes:
        raise ValueError(f"{difficulty} has empty notes")
    prev_t = -1
    for i, n in enumerate(notes):
        if n["t"] < prev_t:
            raise ValueError(f"{difficulty} notes not sorted at idx {i}")
        prev_t = n["t"]
        if not (0 <= n["lane"] <= 3):
            raise ValueError(f"{difficulty} lane out of range at idx {i}")
        if not (PITCH_MIN <= n["pitch"] <= PITCH_MAX):
            raise ValueError(f"{difficulty} pitch out of range at idx {i}")
        if n["type"] == "TAP" and n["dur"] != 0:
            raise ValueError(f"{difficulty} TAP must have dur=0 at idx {i}")
        if n["type"] == "HOLD" and n["dur"] <= 0:
            raise ValueError(f"{difficulty} HOLD must have dur>0 at idx {i}")
        if n["type"] not in ("TAP", "HOLD"):
            raise ValueError(f"{difficulty} unknown type {n['type']} at idx {i}")


def write_kfchart(notes: list[dict], meta: dict, difficulty: str,
                  out: str | None = None, merge_into: str | None = None) -> None:
    if (out is None) == (merge_into is None):
        raise ValueError("exactly one of --out or --merge-into must be set")

    new_chart = to_kfchart(notes, meta, difficulty)

    if merge_into:
        path = Path(merge_into)
        if path.exists():
            existing = json.loads(path.read_text(encoding="utf-8"))
            existing["songId"] = new_chart["songId"]
            existing["title"] = new_chart["title"]
            existing["composer"] = new_chart["composer"]
            existing["bpm"] = new_chart["bpm"]
            existing["durationMs"] = new_chart["durationMs"]
            existing.setdefault("charts", {})[difficulty] = new_chart["charts"][difficulty]
            merged = existing
        else:
            merged = new_chart
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(merged, indent=2, ensure_ascii=False), encoding="utf-8")
    else:
        out_path = Path(out)
        out_path.parent.mkdir(parents=True, exist_ok=True)
        out_path.write_text(json.dumps(new_chart, indent=2, ensure_ascii=False), encoding="utf-8")
```

- [ ] **Step 4: Run — expect 5 passed**

- [ ] **Step 5: Commit**

```bash
git add tools/midi_to_kfchart/pipeline/emitter.py tools/midi_to_kfchart/tests/test_emitter.py
git commit -m "feat(w5): emitter JSON + merge + validate"
```

---

### Task 9: `midi_to_kfchart.py` CLI (single-file mode)

**Files:**
- Create: `tools/midi_to_kfchart/midi_to_kfchart.py`
- Create: `tools/midi_to_kfchart/tests/test_cli.py`
- Create: `tools/midi_to_kfchart/tests/fixtures/tiny.mid` (generated by test, not committed as binary if possible — see step 1)

**Responsibility:** argparse wrapper that composes `parser → chord_reducer → hold_detector → density → pitch_clamp → lane_assigner → emitter.write_kfchart`.

- [ ] **Step 1: Write failing CLI integration test**

`tests/test_cli.py`:

```python
import json, subprocess, sys
from pathlib import Path
import mido

TOOL = Path(__file__).resolve().parent.parent / "midi_to_kfchart.py"

def _make_tiny_midi(path):
    mid = mido.MidiFile(type=0, ticks_per_beat=480)
    track = mido.MidiTrack(); mid.tracks.append(track)
    track.append(mido.MetaMessage("set_tempo", tempo=500000, time=0))
    for i in range(5):
        track.append(mido.Message("note_on", note=60 + i, velocity=80, time=0 if i == 0 else 240))
        track.append(mido.Message("note_off", note=60 + i, velocity=0, time=240))
    mid.save(path)

def test_cli_generates_valid_chart(tmp_path):
    midi = tmp_path / "tiny.mid"
    _make_tiny_midi(midi)
    out = tmp_path / "out.kfchart"
    r = subprocess.run([
        sys.executable, str(TOOL), str(midi),
        "--song-id", "tiny", "--title", "Tiny", "--composer", "Test",
        "--difficulty", "EASY", "--target-nps", "5", "--bpm", "120",
        "--duration-ms", "5000", "--out", str(out),
    ], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    data = json.loads(out.read_text(encoding="utf-8"))
    assert data["songId"] == "tiny"
    assert "EASY" in data["charts"]
    assert data["charts"]["EASY"]["totalNotes"] >= 1

def test_cli_rejects_both_out_and_merge(tmp_path):
    midi = tmp_path / "tiny.mid"
    _make_tiny_midi(midi)
    out = tmp_path / "out.kfchart"
    merge = tmp_path / "merge.kfchart"
    r = subprocess.run([
        sys.executable, str(TOOL), str(midi),
        "--song-id", "t", "--title", "T", "--composer", "C",
        "--difficulty", "EASY", "--target-nps", "5", "--bpm", "120",
        "--duration-ms", "5000", "--out", str(out), "--merge-into", str(merge),
    ], capture_output=True, text=True)
    assert r.returncode != 0
```

- [ ] **Step 2: Run — expect fail** (script not present yet).

- [ ] **Step 3: Implement `midi_to_kfchart.py`**

```python
"""KeyFlow MIDI -> .kfchart CLI."""
import argparse
import sys
from pathlib import Path

# Ensure sibling pipeline/ importable when run as script.
sys.path.insert(0, str(Path(__file__).resolve().parent))

from pipeline import parser, chord_reducer, hold_detector, density, pitch_clamp, lane_assigner, emitter


def _single(args) -> int:
    raws = parser.parse_midi(args.input)
    mono = chord_reducer.collapse(raws)
    typed = hold_detector.classify(mono)
    thinned = density.thin(typed, target_nps=args.target_nps, duration_ms=args.duration_ms)
    clamped = pitch_clamp.clamp_pitches(thinned)
    assigned = lane_assigner.assign(clamped)
    meta = {
        "song_id": args.song_id, "title": args.title, "composer": args.composer,
        "bpm": args.bpm, "duration_ms": args.duration_ms,
    }
    emitter.write_kfchart(assigned, meta, args.difficulty,
                          out=args.out, merge_into=args.merge_into)
    print(f"[OK] {args.difficulty}: {len(assigned)} notes -> "
          f"{args.out or args.merge_into}")
    return 0


def _batch(args) -> int:
    import yaml  # lazy; requirements add pyyaml in Task 10
    cfg = yaml.safe_load(Path(args.batch).read_text(encoding="utf-8"))
    defaults = cfg.get("defaults", {}) or {}
    out_dir = Path(defaults.get("out_dir", "."))
    rc = 0
    for song in cfg.get("songs", []):
        song_id = song["song_id"]
        target = out_dir / f"{song_id}.kfchart"
        for diff_name, diff_cfg in (song.get("difficulties") or {}).items():
            ns = argparse.Namespace(
                input=song["midi"], song_id=song_id, title=song["title"],
                composer=song["composer"], difficulty=diff_name,
                target_nps=float(diff_cfg["target_nps"]),
                bpm=int(song["bpm"]), duration_ms=int(song["duration_ms"]),
                out=None, merge_into=str(target),
            )
            try:
                _single(ns)
            except Exception as exc:
                print(f"[FAIL] {song_id} {diff_name}: {exc}", file=sys.stderr)
                rc = 1
    return rc


def main(argv=None) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("input", nargs="?", help=".mid input file")
    ap.add_argument("--song-id")
    ap.add_argument("--title")
    ap.add_argument("--composer")
    ap.add_argument("--difficulty", choices=["EASY", "NORMAL"])
    ap.add_argument("--target-nps", type=float)
    ap.add_argument("--bpm", type=int)
    ap.add_argument("--duration-ms", type=int)
    ap.add_argument("--out")
    ap.add_argument("--merge-into")
    ap.add_argument("--batch")
    args = ap.parse_args(argv)

    if args.batch:
        return _batch(args)

    required = ["input", "song_id", "title", "composer", "difficulty",
                "target_nps", "bpm", "duration_ms"]
    missing = [r for r in required if getattr(args, r) is None]
    if missing:
        ap.error("missing required: " + ", ".join(missing))
    if (args.out is None) == (args.merge_into is None):
        ap.error("exactly one of --out or --merge-into required")

    return _single(args)


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 4: Run — expect 2 passed**

```
.venv/Scripts/pytest tests/test_cli.py -v
```

- [ ] **Step 5: Run full suite**

```
.venv/Scripts/pytest -q
```

Expected: all 30 tests pass (4+3+4+4+4+4+5+2).

- [ ] **Step 6: Commit**

```bash
git add tools/midi_to_kfchart/midi_to_kfchart.py tools/midi_to_kfchart/tests/test_cli.py
git commit -m "feat(w5): CLI single-file mode"
```

---

### Task 10: Batch mode + example YAML

**Files:**
- Modify: `tools/midi_to_kfchart/requirements.txt` (add `PyYAML==6.0.2`)
- Create: `tools/midi_to_kfchart/batch.yaml.example`

Note: `_batch()` function already implemented in Task 9; this task finalizes the YAML dep and example file, plus verifies batch end-to-end.

- [ ] **Step 1: Add PyYAML to requirements**

```
mido==1.3.2
pytest==8.3.3
PyYAML==6.0.2
```

Install:

```
.venv/Scripts/pip install -r requirements.txt
```

- [ ] **Step 2: Create `batch.yaml.example`**

```yaml
defaults:
  out_dir: Assets/StreamingAssets/charts/
songs:
  - song_id: beethoven_fur_elise
    midi: midi_sources/fur_elise.mid
    title: "Für Elise"
    composer: "Beethoven"
    bpm: 72
    duration_ms: 45000
    difficulties:
      NORMAL: { target_nps: 3.5 }
```

- [ ] **Step 3: Add batch integration test**

Append to `tests/test_cli.py`:

```python
def test_cli_batch_mode(tmp_path):
    midi = tmp_path / "tiny.mid"
    _make_tiny_midi(midi)
    out_dir = tmp_path / "out"
    yml = tmp_path / "batch.yaml"
    yml.write_text(f"""
defaults:
  out_dir: {out_dir.as_posix()}/
songs:
  - song_id: tiny
    midi: {midi.as_posix()}
    title: Tiny
    composer: Test
    bpm: 120
    duration_ms: 5000
    difficulties:
      EASY: {{ target_nps: 5 }}
""", encoding="utf-8")
    r = subprocess.run(
        [sys.executable, str(TOOL), "--batch", str(yml)],
        capture_output=True, text=True,
    )
    assert r.returncode == 0, r.stderr
    assert (out_dir / "tiny.kfchart").exists()
```

- [ ] **Step 4: Run — expect 31 passed**

- [ ] **Step 5: Commit**

```bash
git add tools/midi_to_kfchart/requirements.txt tools/midi_to_kfchart/batch.yaml.example tools/midi_to_kfchart/tests/test_cli.py
git commit -m "feat(w5): batch mode + PyYAML + example"
```

---

## Phase B — Content (Spec §4)

### Task 11: Generate Für Elise Normal chart

**Files:**
- Create: `midi_sources/` (gitignored by Task 1)
- Modify: `Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart`

**Responsibility:** Acquire a Für Elise MIDI (any source, personal-use scope), run the pipeline, validate in Unity Editor, commit the chart.

- [ ] **Step 1: Obtain MIDI**

Download Für Elise MIDI to `midi_sources/fur_elise.mid` (any freely-available MIDI — personal-use distribution only). Verify with:

```
.venv/Scripts/python -c "import mido; m=mido.MidiFile('../../midi_sources/fur_elise.mid'); print(len(m.tracks), sum(1 for t in m.tracks for msg in t if msg.type=='note_on'))"
```

Expected: at least 2 tracks, > 50 note_on events.

- [ ] **Step 2: Run pipeline**

From `tools/midi_to_kfchart/`:

```bash
.venv/Scripts/python midi_to_kfchart.py ../../midi_sources/fur_elise.mid \
  --song-id beethoven_fur_elise --title "Für Elise" --composer "Beethoven" \
  --difficulty NORMAL --target-nps 3.5 --bpm 72 --duration-ms 45000 \
  --merge-into ../../Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart
```

Expected: `[OK] NORMAL: NN notes -> .../beethoven_fur_elise.kfchart`.

- [ ] **Step 3: Inspect generated chart**

Verify:
- `charts.EASY` key still present (73 notes, unchanged).
- `charts.NORMAL.notes` length consistent with NPS 3.0–4.0 over 45s (expect ~135–180 notes, allow slack).
- Hold notes ≥ 5 and no 3-consecutive same-lane. Verify via Python:

```bash
python -c "import json; d=json.load(open('Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart', encoding='utf-8')); n=d['charts']['NORMAL']['notes']; holds=sum(1 for x in n if x['type']=='HOLD'); triple=any(n[i]['lane']==n[i-1]['lane']==n[i-2]['lane'] for i in range(2,len(n))); print(f'notes={len(n)} holds={holds} triple_lane={triple}')"
```

Pass: `holds >= 5`, `triple_lane=False`.

If any check fails, adjust `--target-nps` and re-run, or hand-edit JSON with README caveat.

- [ ] **Step 4: Open Unity Editor, play Für Elise Normal**

Main → Für Elise → Normal → Calibration (if first run) → Gameplay → complete.

Pass criteria: completable in ≤ 2 retries; no "얽힌 레인" / "뚝 끊긴 구간" feel. Hand-tune JSON if needed.

- [ ] **Step 5: Commit**

```bash
git add Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart
git commit -m "content(w5): Fur Elise Normal via pipeline"
```

---

## Phase C — ChartLoader Refactor (Spec §5–§7)

### Task 12: Validate hardening + totalNotes check

**Files:**
- Modify: `Assets/Scripts/Charts/ChartLoader.cs`
- Modify: `Assets/Tests/EditMode/ChartLoaderTests.cs`

**Responsibility:** Add list-level validation (empty notes, sort order, totalNotes match) to `ParseJson`. Spec §6.

- [ ] **Step 1: Write failing tests**

Append to `ChartLoaderTests.cs` (before the final `}` of the class):

```csharp
[Test]
public void ParseJson_EmptyNotes_Throws()
{
    var bad = @"{
        ""songId"":""x"",""title"":""x"",""composer"":""x"",""bpm"":120,""durationMs"":5000,
        ""charts"":{""EASY"":{""totalNotes"":0,""notes"":[]}}
    }";
    Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
}

[Test]
public void ParseJson_UnsortedNotes_Throws()
{
    var bad = @"{
        ""songId"":""x"",""title"":""x"",""composer"":""x"",""bpm"":120,""durationMs"":5000,
        ""charts"":{""EASY"":{""totalNotes"":2,""notes"":[
            {""t"":2000,""lane"":0,""pitch"":60,""type"":""TAP"",""dur"":0},
            {""t"":1000,""lane"":1,""pitch"":61,""type"":""TAP"",""dur"":0}
        ]}}
    }";
    Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
}

[Test]
public void ParseJson_TotalNotesMismatch_Throws()
{
    var bad = @"{
        ""songId"":""x"",""title"":""x"",""composer"":""x"",""bpm"":120,""durationMs"":5000,
        ""charts"":{""EASY"":{""totalNotes"":99,""notes"":[
            {""t"":1000,""lane"":0,""pitch"":60,""type"":""TAP"",""dur"":0}
        ]}}
    }";
    Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
}
```

- [ ] **Step 2: Run Unity EditMode tests — expect 3 failures**

Foreground batch mode (per CLAUDE memory):

```bash
# Windows cmd syntax
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "C:/dev/unity-music" \
  -runTests -testPlatform EditMode \
  -testResults "C:/dev/unity-music/test-results.xml" \
  -logFile - -quit
```

Read `test-results.xml` to confirm the 3 new tests fail (expected messages reference missing validation).

- [ ] **Step 3: Implement in `ChartLoader.cs`**

In `ParseJson`, after the `foreach (var n in (JArray)diffObj["notes"])` loop completes for each difficulty, before `chart.charts[diff] = cd;`, add:

```csharp
if (cd.notes.Count == 0)
    throw new ChartValidationException($"{prop.Name} has no notes");
for (int i = 1; i < cd.notes.Count; i++)
{
    if (cd.notes[i].t < cd.notes[i - 1].t)
        throw new ChartValidationException($"{prop.Name} notes not sorted at idx {i}");
}
if (cd.totalNotes != cd.notes.Count)
    throw new ChartValidationException(
        $"{prop.Name} totalNotes {cd.totalNotes} != actual {cd.notes.Count}");
```

- [ ] **Step 4: Run EditMode tests — expect 91 passed**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "C:/dev/unity-music" -runTests -testPlatform EditMode \
  -testResults "C:/dev/unity-music/test-results.xml" -logFile - -quit
```

Read `test-results.xml` `<test-run total="91" passed="91">`.

Verified in plan drafting: existing Für Elise EASY chart has `totalNotes=73` and 73 notes — the new `totalNotes == notes.Count` check passes.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Charts/ChartLoader.cs Assets/Tests/EditMode/ChartLoaderTests.cs
git commit -m "feat(w5): ChartLoader Validate hardening (empty/sort/totalNotes)"
```

---

### Task 13: `Mathf.Clamp` → `System.Math.Clamp`

**Files:**
- Modify: `Assets/Scripts/Charts/ChartLoader.cs:71`
- Modify: `Assets/Scripts/Calibration/CalibrationCalculator.cs:43`

**Responsibility:** Spec §7. Replace the two `Mathf.Clamp` call sites — no new tests; existing 91 EditMode tests cover behavior.

- [ ] **Step 1: Edit `ChartLoader.cs` line 71**

```csharp
// before
pitch = Mathf.Clamp((int)n["pitch"], PitchMin, PitchMax),
// after
pitch = System.Math.Clamp((int)n["pitch"], PitchMin, PitchMax),
```

- [ ] **Step 2: Edit `CalibrationCalculator.cs` line 43**

```csharp
// before
offsetMs = Mathf.Clamp(offsetMs, -500, 500);
// after
offsetMs = System.Math.Clamp(offsetMs, -500, 500);
```

Leave `Mathf.RoundToInt` on lines 42 and 44 untouched (spec §7.1: Unity-specific math stays).

- [ ] **Step 3: Run EditMode tests — expect 91 passed**

Same batch command as Task 12 Step 4.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Charts/ChartLoader.cs Assets/Scripts/Calibration/CalibrationCalculator.cs
git commit -m "refactor(w5): Mathf.Clamp to System.Math.Clamp (carry-over #8)"
```

---

### Task 14: `ChartLoader.LoadFromPath` introduction

**Files:**
- Modify: `Assets/Scripts/Charts/ChartLoader.cs`
- Modify: `Assets/Tests/EditMode/ChartLoaderTests.cs`

**Responsibility:** Extract a synchronous file-to-ChartData path for Editor/tests. Used by both EditMode suite and non-Android runtime inside the new coroutine.

- [ ] **Step 1: Write failing tests**

Append to `ChartLoaderTests.cs`:

```csharp
[Test]
public void LoadFromPath_RealChart_ParsesFurEliseEasy()
{
    string path = System.IO.Path.Combine(
        UnityEngine.Application.streamingAssetsPath,
        "charts", "beethoven_fur_elise.kfchart");
    var chart = ChartLoader.LoadFromPath(path);
    Assert.AreEqual("beethoven_fur_elise", chart.songId);
    Assert.IsTrue(chart.charts.ContainsKey(Difficulty.Easy));
    Assert.Greater(chart.charts[Difficulty.Easy].notes.Count, 0);
}

[Test]
public void LoadFromPath_MissingFile_Throws()
{
    Assert.Throws<System.IO.FileNotFoundException>(() =>
        ChartLoader.LoadFromPath("no/such/path.kfchart"));
}
```

- [ ] **Step 2: Run — expect 2 failures (method not defined)**

- [ ] **Step 3: Implement in `ChartLoader.cs`**

Add public method (keep existing `LoadFromStreamingAssets` for now — removed in Task 15):

```csharp
public static ChartData LoadFromPath(string absolutePath)
{
    if (!System.IO.File.Exists(absolutePath))
        throw new System.IO.FileNotFoundException($"Chart not found: {absolutePath}");
    return ParseJson(System.IO.File.ReadAllText(absolutePath));
}
```

- [ ] **Step 4: Run EditMode — expect 93 passed**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Charts/ChartLoader.cs Assets/Tests/EditMode/ChartLoaderTests.cs
git commit -m "feat(w5): ChartLoader.LoadFromPath for editor/tests"
```

---

### Task 15: Coroutine API + GameplayController wire-up

**Files:**
- Modify: `Assets/Scripts/Charts/ChartLoader.cs`
- Modify: `Assets/Scripts/Gameplay/GameplayController.cs`

**Responsibility:** Add `LoadFromStreamingAssetsCo` coroutine with callbacks, remove the synchronous `LoadFromStreamingAssets`, and split `GameplayController.ResetAndStart` around the async callback. Spec §5.

- [ ] **Step 1: Replace ChartLoader's `LoadFromStreamingAssets` + `ReadStreamingAsset`**

In `ChartLoader.cs`:

1. Delete `public static ChartData LoadFromStreamingAssets(string songId)` entirely.
2. Delete `private static string ReadStreamingAsset(string path)` entirely.
3. Add at top: `using System.Collections;`
4. Add new method:

```csharp
public static IEnumerator LoadFromStreamingAssetsCo(
    string songId,
    System.Action<ChartData> onLoaded,
    System.Action<string> onError)
{
    string path = System.IO.Path.Combine(
        UnityEngine.Application.streamingAssetsPath, "charts", songId + ".kfchart");

#if UNITY_ANDROID && !UNITY_EDITOR
    using (var req = UnityEngine.Networking.UnityWebRequest.Get(path))
    {
        yield return req.SendWebRequest();
        if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{path}: {req.error}");
            yield break;
        }
        ChartData loaded;
        try { loaded = ParseJson(req.downloadHandler.text); }
        catch (System.Exception e) { onError?.Invoke(e.Message); yield break; }
        onLoaded?.Invoke(loaded);
    }
#else
    ChartData chart;
    try { chart = LoadFromPath(path); }
    catch (System.Exception e) { onError?.Invoke(e.Message); yield break; }
    yield return null;  // yield once for symmetry with Android path
    onLoaded?.Invoke(chart);
#endif
}
```

- [ ] **Step 2: Refactor `GameplayController.ResetAndStart`**

Replace the current method body. Split post-load logic into `ContinueAfterChartLoaded`:

```csharp
public void ResetAndStart()
{
    if (!prefsMigrated) { UserPrefs.MigrateLegacy(); prefsMigrated = true; }

    string songId = SongSession.CurrentSongId;
    if (string.IsNullOrEmpty(songId))
    {
        Debug.LogError("[KeyFlow] GameplayController.ResetAndStart with no SongSession.CurrentSongId");
        return;
    }
    difficulty = SongSession.CurrentDifficulty;

    playing = false;
    completed = false;

    StartCoroutine(ChartLoader.LoadFromStreamingAssetsCo(
        songId,
        loaded => { chart = loaded; ContinueAfterChartLoaded(); },
        err => Debug.LogError($"[KeyFlow] chart load failed: {err}")));
}

private void ContinueAfterChartLoaded()
{
    spawner.ResetForRetry();
    holdTracker.ResetForRetry();
    judgmentSystem.ResetForRetry();

    if (UserPrefs.HasCalibration)
    {
        audioSync.CalibrationOffsetSec = UserPrefs.CalibrationOffsetMs / 1000.0;
        BeginGameplay();
    }
    else
    {
        calibration.Begin(BeginGameplay);
    }
}
```

- [ ] **Step 3: Run EditMode — expect 93 passed**

Same batch command. Verifies existing test suite still green after removing sync API (tests use `ParseJson` + `LoadFromPath`, not `LoadFromStreamingAssets`, so should be unaffected).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Charts/ChartLoader.cs Assets/Scripts/Gameplay/GameplayController.cs
git commit -m "feat(w5): ChartLoader coroutine + GameplayController split (carry-over #3)"
```

---

## Phase D — Verification & Report

### Task 16: Device verification + W5 completion report

**Files:**
- Create: `docs/superpowers/reports/2026-04-??-w5-completion.md` (replace ?? with actual day)

**Responsibility:** Build APK, install on Galaxy S22, hand-walk acceptance, record results.

- [ ] **Step 1: Full test suite**

```
# Python
cd tools/midi_to_kfchart && .venv/Scripts/pytest -q
```

Expected: 31 passed.

```
# Unity EditMode (foreground)
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "C:/dev/unity-music" -runTests -testPlatform EditMode \
  -testResults "C:/dev/unity-music/test-results.xml" -logFile - -quit
```

Expected: `<test-run total="93" passed="93">`.

- [ ] **Step 2: Build APK**

```bash
# Use existing project build command. Writes Builds/keyflow-w5.apk.
"C:/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "C:/dev/unity-music" -executeMethod KeyFlow.Editor.BuildScript.BuildAndroid \
  -logFile - -quit
```

Rename artifact if needed: `Builds/keyflow-w5.apk`. Verify `ls -la Builds/keyflow-w5.apk` < 40 MB.

- [ ] **Step 3: Install + hand-walk on Galaxy S22**

```bash
adb install -r Builds/keyflow-w5.apk
```

Acceptance walk:
1. First launch → Calibration → Main → Für Elise → **Normal** → play to completion.
2. Back to Main → Für Elise → Easy → play to completion (regression — existing chart still works).
3. No ANR / blocked launch (coroutine chart load succeeded on device).
4. 60 FPS per LatencyMeter HUD.

- [ ] **Step 4: Write completion report**

Fill `docs/superpowers/reports/2026-04-<DD>-w5-completion.md` mirroring W4's structure:
- Scope delivered
- Test counts (Python 31, Unity EditMode 93)
- Device validation checklist
- Outstanding items (4 songs + pipeline polish deferred to W6)
- APK size
- Next steps → W6

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/reports/2026-04-*-w5-completion.md Builds/keyflow-w5.apk
git commit -m "docs(w5): completion report"
```

(If `Builds/*.apk` is gitignored per repo convention, only commit the report.)

---

## Definition of Done

Mirror of spec §10:

- [ ] `cd tools/midi_to_kfchart && pytest -q` → 31 passed
- [ ] Für Elise NORMAL chart present in `beethoven_fur_elise.kfchart` `charts.NORMAL`
- [ ] Main → Für Elise → Normal → play → complete
- [ ] ChartLoader Android path uses coroutine (no busy-wait), sync `LoadFromStreamingAssets` removed
- [ ] ChartLoader list-level Validate (empty/sort/totalNotes) active
- [ ] `Mathf.Clamp` → `System.Math.Clamp` in ChartLoader + CalibrationCalculator
- [ ] EditMode 93 tests pass
- [ ] APK < 40 MB
- [ ] Galaxy S22 Für Elise Normal walk-through by user
- [ ] W5 completion report committed
