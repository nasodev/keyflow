import json
import pytest
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
