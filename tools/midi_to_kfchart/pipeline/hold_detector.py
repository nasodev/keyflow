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
