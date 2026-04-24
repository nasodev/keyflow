"""Merge same-lane HOLDs that are back-to-back or near-adjacent.

Adjacent HOLDs on the same lane read as one continuous note to the player,
but the input state machine expects a fresh tap for each HOLD's start window.
When the player holds through, the second HOLD's start tap never arrives →
auto-miss. This pass consolidates such pairs into a single longer HOLD.
"""
MERGE_GAP_MS = 500
MAX_DUR_MS = 4000


def merge(notes: list[dict]) -> list[dict]:
    """Merge same-lane HOLDs whose gap is < MERGE_GAP_MS.

    Input: list of note dicts each with {t, lane, pitch, type, dur}, in temporal order.
    Output: new list in temporal order, possibly shorter, with merged HOLDs.

    A merged HOLD spans from the first's start to the last's end, capped at MAX_DUR_MS.
    Overlapping HOLDs (gap < 0) merge as well.
    TAPs and non-adjacent HOLDs pass through unchanged.
    """
    if not notes:
        return notes

    result: list[dict] = []
    last_hold_idx_by_lane: dict[int, int] = {}

    for note in notes:
        if note["type"] != "HOLD":
            result.append(dict(note))
            continue

        last_idx = last_hold_idx_by_lane.get(note["lane"])
        if last_idx is not None:
            prev = result[last_idx]
            prev_end = prev["t"] + prev["dur"]
            gap = note["t"] - prev_end
            if gap < MERGE_GAP_MS:
                new_end = note["t"] + note["dur"]
                new_dur = new_end - prev["t"]
                if new_dur > MAX_DUR_MS:
                    new_dur = MAX_DUR_MS
                prev["dur"] = new_dur
                continue

        new_note = dict(note)
        result.append(new_note)
        last_hold_idx_by_lane[note["lane"]] = len(result) - 1

    return result
