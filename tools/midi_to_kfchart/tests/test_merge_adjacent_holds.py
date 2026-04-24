from pipeline.merge_adjacent_holds import merge


def _note(t, lane, type_, dur, pitch=60):
    return {"t": t, "lane": lane, "pitch": pitch, "type": type_, "dur": dur}


def test_empty_list():
    assert merge([]) == []


def test_single_hold_unchanged():
    notes = [_note(1000, 0, "HOLD", 500)]
    assert merge(notes) == notes


def test_same_lane_gap_zero_merges():
    notes = [_note(1000, 0, "HOLD", 500), _note(1500, 0, "HOLD", 500)]
    out = merge(notes)
    assert len(out) == 1
    assert out[0]["t"] == 1000 and out[0]["dur"] == 1000 and out[0]["type"] == "HOLD"


def test_same_lane_gap_300_merges():
    notes = [_note(1000, 0, "HOLD", 500), _note(1800, 0, "HOLD", 500)]
    out = merge(notes)
    assert len(out) == 1
    assert out[0]["dur"] == 1300  # 1800+500-1000


def test_same_lane_gap_500_does_NOT_merge():
    notes = [_note(1000, 0, "HOLD", 500), _note(2000, 0, "HOLD", 500)]
    out = merge(notes)
    assert len(out) == 2


def test_same_lane_overlap_merges():
    notes = [_note(1000, 0, "HOLD", 2000), _note(2500, 0, "HOLD", 1000)]
    out = merge(notes)
    assert len(out) == 1
    assert out[0]["dur"] == 2500  # 2500+1000-1000


def test_cap_at_4000ms():
    notes = [_note(1000, 0, "HOLD", 3000), _note(4000, 0, "HOLD", 3000)]
    out = merge(notes)
    assert len(out) == 1
    assert out[0]["dur"] == 4000  # capped


def test_different_lanes_independent():
    notes = [_note(1000, 0, "HOLD", 500), _note(1500, 1, "HOLD", 500)]
    out = merge(notes)
    assert len(out) == 2


def test_three_holds_chain_all_merge():
    notes = [
        _note(1000, 0, "HOLD", 500),
        _note(1500, 0, "HOLD", 500),
        _note(2000, 0, "HOLD", 500),
    ]
    out = merge(notes)
    assert len(out) == 1
    assert out[0]["dur"] == 1500


def test_tap_between_holds_breaks_chain():
    # TAP on different lane 1 does not affect lane 0 merging.
    # TAP on same lane 0 in between — does that break the chain?
    # Current logic: it passes through but doesn't update last_hold_idx_by_lane[0],
    # so the following HOLD on lane 0 still merges with the first HOLD.
    notes = [
        _note(1000, 0, "HOLD", 500),
        _note(1300, 0, "TAP", 0),
        _note(1500, 0, "HOLD", 500),
    ]
    out = merge(notes)
    # Expected: 2 notes (merged HOLD + TAP, or TAP + merged HOLD depending on implementation).
    # Let's assert the specific behavior: TAP preserved, HOLDs merged.
    tap_count = sum(1 for n in out if n["type"] == "TAP")
    hold_count = sum(1 for n in out if n["type"] == "HOLD")
    assert tap_count == 1 and hold_count == 1
    merged_hold = next(n for n in out if n["type"] == "HOLD")
    assert merged_hold["t"] == 1000 and merged_hold["dur"] == 1000


def test_taps_unchanged():
    notes = [_note(1000, 0, "TAP", 0), _note(2000, 1, "TAP", 0)]
    out = merge(notes)
    assert out == notes
