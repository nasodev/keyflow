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
