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
