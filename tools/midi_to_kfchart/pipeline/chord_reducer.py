"""Collapse simultaneous notes to a single monophonic line (highest pitch)."""


def collapse(raws: list[dict]) -> list[dict]:
    by_time: dict[int, dict] = {}
    for n in raws:
        existing = by_time.get(n["t_ms"])
        if existing is None or n["pitch"] > existing["pitch"]:
            by_time[n["t_ms"]] = n
    return [by_time[t] for t in sorted(by_time)]
