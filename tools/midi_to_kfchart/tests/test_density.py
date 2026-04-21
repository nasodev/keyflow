from pipeline.density import thin


def _make(count, t_step=100):
    return [{"t": i * t_step, "pitch": 60, "type": "TAP", "dur": 0} for i in range(count)]


def test_already_below_target_no_change():
    notes = _make(10)  # 10 notes / 1s = 10 NPS. target 20 -> no thin.
    assert len(thin(notes, target_nps=20, duration_ms=1000)) == 10


def test_above_target_thins():
    notes = _make(20)  # 20 notes / 1s = 20 NPS. target 10 -> thin ~half.
    result = thin(notes, target_nps=10, duration_ms=1000)
    assert 9 <= len(result) <= 13  # target*1.1 = 11 notes
    # determinism: same call returns same result
    assert thin(notes, target_nps=10, duration_ms=1000) == result


def test_empty_passes_through():
    assert thin([], target_nps=10, duration_ms=1000) == []


def test_zero_duration_returns_empty():
    assert thin(_make(5), target_nps=10, duration_ms=0) == []


def test_extreme_ratio_returns_empty():
    # 100 notes / 1s = 100 NPS. target 1 NPS -> step would be <2 -> empty.
    notes = _make(100)
    assert thin(notes, target_nps=1, duration_ms=1000) == []
