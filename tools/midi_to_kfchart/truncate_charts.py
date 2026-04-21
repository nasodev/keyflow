"""Truncate .kfchart files to a target durationMs.

Usage:
    python truncate_charts.py <chart_path> <target_ms>

Idempotent: if chart is already at or below target, exits with no changes.
Drops notes with t > target_ms from every difficulty's notes[] and updates
totalNotes + top-level durationMs. Preserves formatting (2-space indent).

Needed because pipeline/density.py does not enforce durationMs as an upper
bound on note timestamps (known spec sec.6 vs code mismatch; post-W6 follow-up
logged in W6-SP2 completion report).
"""
import json
import sys
from pathlib import Path


def truncate(path: Path, target_ms: int) -> dict:
    doc = json.loads(path.read_text(encoding="utf-8"))
    changed = doc["durationMs"] != target_ms
    doc["durationMs"] = target_ms
    for diff_name, diff in doc["charts"].items():
        before = len(diff["notes"])
        diff["notes"] = [n for n in diff["notes"] if n["t"] <= target_ms]
        diff["totalNotes"] = len(diff["notes"])
        if len(diff["notes"]) != before:
            changed = True
    if changed:
        path.write_text(json.dumps(doc, indent=2, ensure_ascii=False), encoding="utf-8")
    return doc


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: truncate_charts.py <chart_path> <target_ms>", file=sys.stderr)
        return 2
    path = Path(sys.argv[1])
    target_ms = int(sys.argv[2])
    doc = truncate(path, target_ms)
    print(f"[OK] {path.name}: durationMs={doc['durationMs']}, "
          f"EASY={doc['charts']['EASY']['totalNotes']}, "
          f"NORMAL={doc['charts']['NORMAL']['totalNotes']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
