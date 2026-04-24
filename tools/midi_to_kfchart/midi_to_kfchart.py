"""KeyFlow MIDI -> .kfchart CLI."""
import argparse
import sys
from pathlib import Path

# Ensure sibling pipeline/ importable when run as script.
sys.path.insert(0, str(Path(__file__).resolve().parent))

from pipeline import parser, chord_reducer, hold_detector, density, pitch_clamp, lane_assigner, merge_adjacent_holds, emitter


def _single(args) -> int:
    raws = parser.parse_midi(args.input)
    mono = chord_reducer.collapse(raws)
    typed = hold_detector.classify(mono)
    thinned = density.thin(typed, target_nps=args.target_nps, duration_ms=args.duration_ms)
    clamped = pitch_clamp.clamp_pitches(thinned)
    assigned = lane_assigner.assign(clamped)
    merged = merge_adjacent_holds.merge(assigned)
    meta = {
        "song_id": args.song_id, "title": args.title, "composer": args.composer,
        "bpm": args.bpm, "duration_ms": args.duration_ms,
    }
    emitter.write_kfchart(merged, meta, args.difficulty,
                          out=args.out, merge_into=args.merge_into)
    print(f"[OK] {args.difficulty}: {len(merged)} notes -> "
          f"{args.out or args.merge_into}")
    return 0


def _batch(args) -> int:
    import yaml  # lazy; requirements add pyyaml in Task 10
    cfg = yaml.safe_load(Path(args.batch).read_text(encoding="utf-8"))
    defaults = cfg.get("defaults", {}) or {}
    out_dir = Path(defaults.get("out_dir", "."))
    rc = 0
    for song in cfg.get("songs", []):
        song_id = song["song_id"]
        target = out_dir / f"{song_id}.kfchart"
        for diff_name, diff_cfg in (song.get("difficulties") or {}).items():
            ns = argparse.Namespace(
                input=song["midi"], song_id=song_id, title=song["title"],
                composer=song["composer"], difficulty=diff_name,
                target_nps=float(diff_cfg["target_nps"]),
                bpm=int(song["bpm"]), duration_ms=int(song["duration_ms"]),
                out=None, merge_into=str(target),
            )
            try:
                _single(ns)
            except Exception as exc:
                print(f"[FAIL] {song_id} {diff_name}: {exc}", file=sys.stderr)
                rc = 1
    return rc


def main(argv=None) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("input", nargs="?", help=".mid input file")
    ap.add_argument("--song-id")
    ap.add_argument("--title")
    ap.add_argument("--composer")
    ap.add_argument("--difficulty", choices=["EASY", "NORMAL"])
    ap.add_argument("--target-nps", type=float)
    ap.add_argument("--bpm", type=int)
    ap.add_argument("--duration-ms", type=int)
    ap.add_argument("--out")
    ap.add_argument("--merge-into")
    ap.add_argument("--batch")
    args = ap.parse_args(argv)

    if args.batch:
        return _batch(args)

    required = ["input", "song_id", "title", "composer", "difficulty",
                "target_nps", "bpm", "duration_ms"]
    missing = [r for r in required if getattr(args, r) is None]
    if missing:
        ap.error("missing required: " + ", ".join(missing))
    if (args.out is None) == (args.merge_into is None):
        ap.error("exactly one of --out or --merge-into required")

    return _single(args)


if __name__ == "__main__":
    raise SystemExit(main())
