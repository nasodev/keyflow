from pipeline.hold_detector import classify


def _raw(t, pitch, dur):
    return {"t_ms": t, "pitch": pitch, "dur_ms": dur}


def test_299ms_is_tap():
    typed = classify([_raw(0, 60, 299)])
    assert typed[0]["type"] == "TAP"
    assert typed[0]["dur"] == 0


def test_300ms_is_hold():
    typed = classify([_raw(0, 60, 300)])
    assert typed[0]["type"] == "HOLD"
    assert typed[0]["dur"] == 300


def test_hold_cap_4000ms():
    typed = classify([_raw(0, 60, 8000)])
    assert typed[0]["type"] == "HOLD"
    assert typed[0]["dur"] == 4000


def test_tap_always_dur_zero():
    typed = classify([_raw(0, 60, 0), _raw(100, 61, 50)])
    assert all(n["type"] == "TAP" and n["dur"] == 0 for n in typed)
