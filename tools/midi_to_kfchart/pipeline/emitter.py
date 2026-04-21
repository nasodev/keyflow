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
