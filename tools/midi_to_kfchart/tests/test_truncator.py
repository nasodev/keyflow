from pipeline.truncator import truncate


def _r(t, dur, pitch=60):
    return {"t_ms": t, "pitch": pitch, "dur_ms": dur}


def test_drops_notes_at_or_past_duration():
    raws = [_r(0, 100), _r(99000, 100), _r(100000, 100), _r(150000, 100)]
    assert truncate(raws, 100000) == [_r(0, 100), _r(99000, 100)]


def test_clamps_hold_overhanging_end():
    raws = [_r(99000, 5000)]  # ends at 104000
    out = truncate(raws, 100000)
    assert out == [_r(99000, 1000)]


def test_passes_through_when_all_in_range():
    raws = [_r(0, 100), _r(50000, 100), _r(99000, 100)]
    assert truncate(raws, 100000) == raws


def test_empty_input():
    assert truncate([], 100000) == []


def test_note_exactly_at_duration_dropped():
    # Boundary: t == duration_ms → dropped (out of range, mirrors `t > durationMs` Validate semantics)
    assert truncate([_r(100000, 100)], 100000) == []


def test_note_ending_exactly_at_duration_kept_unclamped():
    # End == duration_ms is fine; only end > duration_ms triggers clamp.
    raws = [_r(99000, 1000)]  # ends at 100000
    assert truncate(raws, 100000) == raws
