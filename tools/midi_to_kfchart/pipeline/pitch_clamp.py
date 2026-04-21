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
