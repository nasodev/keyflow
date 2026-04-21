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
