"""MIDI file → list[RawNote] where RawNote = {"t_ms", "pitch", "dur_ms"}.

When include_tracks is given, only notes from tracks whose track_name matches
are returned. Tempo events from any track are still honored globally.
"""
import mido


def parse_midi(path: str, include_tracks: list[str] | None = None) -> list[dict]:
    mid = mido.MidiFile(path)

    # Global tempo map: (abs_tick, tempo_microseconds_per_beat).
    tempo_map: list[tuple[int, int]] = []
    for track in mid.tracks:
        abs_tick = 0
        for m in track:
            abs_tick += m.time
            if m.type == "set_tempo":
                tempo_map.append((abs_tick, m.tempo))
    tempo_map.sort()
    if not tempo_map or tempo_map[0][0] > 0:
        tempo_map.insert(0, (0, 500_000))  # 120 BPM default

    def tick_to_sec(target_tick: int) -> float:
        sec = 0.0
        prev_tick = 0
        prev_tempo = tempo_map[0][1]
        for evt_tick, tempo in tempo_map:
            if evt_tick >= target_tick:
                break
            sec += mido.tick2second(evt_tick - prev_tick, mid.ticks_per_beat, prev_tempo)
            prev_tick = evt_tick
            prev_tempo = tempo
        sec += mido.tick2second(target_tick - prev_tick, mid.ticks_per_beat, prev_tempo)
        return sec

    notes: list[dict] = []
    for track in mid.tracks:
        track_name = next((m.name for m in track if m.type == "track_name"), None)
        if include_tracks is not None and track_name not in include_tracks:
            continue

        active: dict[int, int] = {}  # pitch -> start_tick
        abs_tick = 0
        for m in track:
            abs_tick += m.time
            if m.type == "note_on" and m.velocity > 0:
                if m.note not in active:
                    active[m.note] = abs_tick
            elif m.type == "note_off" or (m.type == "note_on" and m.velocity == 0):
                start_tick = active.pop(m.note, None)
                if start_tick is not None:
                    start_sec = tick_to_sec(start_tick)
                    end_sec = tick_to_sec(abs_tick)
                    notes.append({
                        "t_ms": int(round(start_sec * 1000)),
                        "pitch": m.note,
                        "dur_ms": int(round((end_sec - start_sec) * 1000)),
                    })

    notes.sort(key=lambda n: (n["t_ms"], n["pitch"]))
    return notes
