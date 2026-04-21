"""Assign notes to 4 lanes by pitch quartile, with 3-consecutive relief."""
LANE_COUNT = 4


def assign(notes: list[dict]) -> list[dict]:
    if not notes:
        return []
    pitches = [n["pitch"] for n in notes]
    pmin, pmax = min(pitches), max(pitches)
    out = [dict(n) for n in notes]

    for n in out:
        if pmax == pmin:
            n["lane"] = 0
        else:
            ratio = (n["pitch"] - pmin) / (pmax - pmin)
            n["lane"] = min(LANE_COUNT - 1, int(ratio * LANE_COUNT))

    # 3-consecutive relief
    for i in range(2, len(out)):
        if out[i]["lane"] == out[i - 1]["lane"] == out[i - 2]["lane"]:
            candidate = (out[i]["lane"] + 1) % LANE_COUNT
            # defensive: unreachable given left-to-right traversal, matches spec §3.4 pseudocode
            if candidate == out[i - 1]["lane"] == out[i - 2]["lane"]:
                candidate = (out[i]["lane"] + 2) % LANE_COUNT
            out[i]["lane"] = candidate

    return out
