from pipeline.chord_reducer import collapse


def _n(t, pitch, dur=0):
    return {"t_ms": t, "pitch": pitch, "dur_ms": dur}


def test_same_timestamp_keeps_highest_pitch():
    result = collapse([_n(0, 60), _n(0, 64), _n(0, 67)])
    assert len(result) == 1
    assert result[0]["pitch"] == 67


def test_different_timestamps_preserved():
    result = collapse([_n(0, 60), _n(100, 64)])
    assert [n["pitch"] for n in result] == [60, 64]


def test_empty_input():
    assert collapse([]) == []
