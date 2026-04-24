from pipeline.hold_detector import classify


def _raw(t, pitch, dur):
    return {"t_ms": t, "pitch": pitch, "dur_ms": dur}


def test_499ms_is_tap():
    typed = classify([_raw(0, 60, 499)])
    assert typed[0]["type"] == "TAP"
    assert typed[0]["dur"] == 0


def test_500ms_is_hold():
    typed = classify([_raw(0, 60, 500)])
    assert typed[0]["type"] == "HOLD"
    assert typed[0]["dur"] == 500


def test_old_threshold_300ms_is_now_tap():
    """Regression: 300 ms is TAP under the new 500 ms threshold."""
    typed = classify([_raw(0, 60, 300)])
    assert typed[0]["type"] == "TAP"
    assert typed[0]["dur"] == 0


def test_hold_cap_4000ms():
    typed = classify([_raw(0, 60, 8000)])
    assert typed[0]["type"] == "HOLD"
    assert typed[0]["dur"] == 4000


def test_tap_always_dur_zero():
    typed = classify([_raw(0, 60, 0), _raw(100, 61, 50)])
    assert all(n["type"] == "TAP" and n["dur"] == 0 for n in typed)
