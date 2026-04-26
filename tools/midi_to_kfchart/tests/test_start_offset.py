from pipeline.start_offset import shift


def _r(t, dur=0, pitch=60):
    return {"t_ms": t, "pitch": pitch, "dur_ms": dur}


def test_zero_offset_passes_through():
    raws = [_r(0), _r(1000), _r(2000)]
    assert shift(raws, 0) == raws


def test_drops_notes_before_offset():
    raws = [_r(500), _r(1000), _r(1500)]
    assert shift(raws, 1000) == [_r(0), _r(500)]


def test_shifts_remaining_notes_earlier():
    raws = [_r(2000, 200), _r(3000, 300)]
    assert shift(raws, 1500) == [_r(500, 200), _r(1500, 300)]


def test_negative_or_zero_offset_no_op():
    raws = [_r(100)]
    assert shift(raws, -100) == raws


def test_empty_input():
    assert shift([], 1000) == []


def test_note_exactly_at_offset_kept_at_zero():
    raws = [_r(1000, 50)]
    assert shift(raws, 1000) == [_r(0, 50)]
