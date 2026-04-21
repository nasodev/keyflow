import pytest
from pipeline.pitch_clamp import clamp_pitches

def _n(pitch): return {"t": 0, "pitch": pitch, "type": "TAP", "dur": 0}

def test_35_transposes_to_47():
    # 35 + 12 = 47 (still < 36? 47 >= 36, in range)
    result = clamp_pitches([_n(35)])
    assert result[0]["pitch"] == 47

def test_84_transposes_to_72():
    # 84 - 12 = 72 (>= 36, <= 83, in range)
    result = clamp_pitches([_n(84)])
    assert result[0]["pitch"] == 72

def test_boundary_36_and_83_unchanged():
    assert clamp_pitches([_n(36), _n(83)]) == [_n(36), _n(83)]

def test_double_octave_transpose():
    # 20 needs +24 to reach 44
    result = clamp_pitches([_n(20)])
    assert result[0]["pitch"] == 44
