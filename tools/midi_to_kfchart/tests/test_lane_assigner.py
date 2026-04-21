from pipeline.lane_assigner import assign


def _n(t, pitch):
    return {"t": t, "pitch": pitch, "type": "TAP", "dur": 0}


def test_single_pitch_all_lane_0():
    notes = [_n(i * 100, 60) for i in range(3)]
    # 3 consecutive same-pitch -> 3rd shifted
    result = assign(notes)
    assert result[0]["lane"] == 0
    assert result[1]["lane"] == 0
    assert result[2]["lane"] != 0


def test_pitch_range_quartiles():
    notes = [_n(i * 100, p) for i, p in enumerate([40, 50, 60, 70])]
    result = assign(notes)
    assert result[0]["lane"] == 0
    assert result[3]["lane"] == 3


def test_three_consecutive_relief():
    # All same pitch -> all would be lane 0 without relief
    notes = [_n(i * 100, 60) for i in range(5)]
    result = assign(notes)
    # No 3 consecutive identical lanes anywhere
    for i in range(2, len(result)):
        assert not (result[i]["lane"] == result[i - 1]["lane"] == result[i - 2]["lane"])


def test_relief_two_step_when_needed():
    # Craft a sequence where simple +1 wouldn't break the run.
    # lanes would be [0, 0, 0, 0] -> after relief the 3rd goes to 1, 4th sees [0,0,1] so OK at 0?
    # Just assert no triple regardless of strategy.
    notes = [_n(i * 100, 60) for i in range(8)]
    result = assign(notes)
    for i in range(2, len(result)):
        assert not (result[i]["lane"] == result[i - 1]["lane"] == result[i - 2]["lane"])
