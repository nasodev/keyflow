"""Personal routing: batch yaml under tools/midi_to_kfchart/personal/ -> outputs go to charts/personal/."""
import json
import subprocess
import sys
from pathlib import Path

import mido

TOOL = Path(__file__).resolve().parent.parent / "midi_to_kfchart.py"


def _make_tiny_midi(path):
    mid = mido.MidiFile(type=0, ticks_per_beat=480)
    track = mido.MidiTrack()
    mid.tracks.append(track)
    track.append(mido.MetaMessage("set_tempo", tempo=500000, time=0))
    for i in range(5):
        track.append(mido.Message("note_on", note=60 + i, velocity=80, time=0 if i == 0 else 240))
        track.append(mido.Message("note_off", note=60 + i, velocity=0, time=240))
    mid.save(path)


def _write_batch_yaml(path, midi_path, out_dir):
    path.write_text(
        "defaults:\n"
        f"  out_dir: {out_dir}\n"
        "songs:\n"
        "  - song_id: testsong\n"
        f"    midi: {midi_path}\n"
        "    title: Test\n"
        "    composer: Tester\n"
        "    bpm: 120\n"
        "    duration_ms: 5000\n"
        "    difficulties:\n"
        "      EASY: { target_nps: 2.0 }\n",
        encoding="utf-8",
    )


def test_batch_under_personal_dir_routes_chart_to_personal_subdir(tmp_path):
    personal_dir = tmp_path / "personal"
    personal_dir.mkdir()
    out_dir = tmp_path / "out"
    out_dir.mkdir()
    midi = tmp_path / "tiny.mid"
    _make_tiny_midi(midi)

    batch = personal_dir / "batch_test.yaml"
    _write_batch_yaml(batch, midi, out_dir)

    r = subprocess.run(
        [sys.executable, str(TOOL), "--batch", str(batch)],
        capture_output=True, text=True,
    )
    assert r.returncode == 0, r.stderr

    expected = out_dir / "personal" / "testsong.kfchart"
    assert expected.exists(), (
        f"chart not at expected path {expected}; "
        f"actual files: {sorted(out_dir.rglob('*.kfchart'))}"
    )
    data = json.loads(expected.read_text(encoding="utf-8"))
    assert data["songId"] == "testsong"


def test_batch_outside_personal_dir_routes_to_public_dir(tmp_path):
    out_dir = tmp_path / "out"
    out_dir.mkdir()
    midi = tmp_path / "tiny.mid"
    _make_tiny_midi(midi)

    batch = tmp_path / "batch_public.yaml"   # no `personal` segment in path
    _write_batch_yaml(batch, midi, out_dir)

    r = subprocess.run(
        [sys.executable, str(TOOL), "--batch", str(batch)],
        capture_output=True, text=True,
    )
    assert r.returncode == 0, r.stderr

    expected = out_dir / "testsong.kfchart"
    assert expected.exists()
    assert not (out_dir / "personal").exists(), \
        "public batch must not create personal/ subdir"
