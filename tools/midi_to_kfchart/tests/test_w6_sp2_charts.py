"""Regression test for shipped .kfchart assets (W6 SP2 spec §9 acceptance).

Scope note: W6 SP2 shipped 3 new songs (Canon in D deferred due to lack of
PD/CC0 MIDI on both Mutopia and IMSLP; see commit 794946f). Expected playable
roster is 4 songs (Für Elise from W6 SP1 + Ode to Joy, Clair de Lune,
The Entertainer from W6 SP2).
"""
import json
from pathlib import Path
import pytest

REPO_ROOT = Path(__file__).resolve().parents[3]
CHARTS_DIR = REPO_ROOT / "Assets" / "StreamingAssets" / "charts"


def _all_charts():
    return sorted(CHARTS_DIR.glob("*.kfchart"))


def _load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


@pytest.mark.parametrize("chart_path", _all_charts(), ids=lambda p: p.stem)
def test_chart_passes_acceptance(chart_path: Path):
    doc = _load(chart_path)

    # Top-level
    assert "songId" in doc and doc["songId"]
    assert "durationMs" in doc and isinstance(doc["durationMs"], int)
    assert doc["durationMs"] > 0
    assert "charts" in doc

    easy = doc["charts"].get("EASY")
    normal = doc["charts"].get("NORMAL")
    assert easy is not None, "EASY difficulty missing"
    assert normal is not None, "NORMAL difficulty missing"

    # Note counts — density ordering
    assert easy["totalNotes"] > 0
    assert normal["totalNotes"] > 0
    assert normal["totalNotes"] > easy["totalNotes"], (
        f"{chart_path.stem}: NORMAL ({normal['totalNotes']}) "
        f"must have more notes than EASY ({easy['totalNotes']})"
    )

    # Per-note invariants
    for diff_name, diff in (("EASY", easy), ("NORMAL", normal)):
        notes = diff["notes"]
        assert len(notes) == diff["totalNotes"], (
            f"{chart_path.stem} {diff_name}: totalNotes mismatch"
        )
        # Temporal order
        for i in range(1, len(notes)):
            assert notes[i]["t"] >= notes[i - 1]["t"], (
                f"{chart_path.stem} {diff_name}: notes not sorted by t"
            )
        # Start buffer + within duration
        if notes:
            assert notes[0]["t"] >= 0, (
                f"{chart_path.stem} {diff_name}: first note has negative t"
            )
            assert notes[-1]["t"] <= doc["durationMs"], (
                f"{chart_path.stem} {diff_name}: last note past durationMs"
            )
        # Lane + pitch bounds (W6-1 Salamander bank: MIDI 36–84)
        for n in notes:
            assert 0 <= n["lane"] <= 3, f"lane out of range: {n}"
            assert 36 <= n["pitch"] <= 84, (
                f"{chart_path.stem} {diff_name}: pitch {n['pitch']} "
                f"outside Salamander bank 36–84"
            )


def test_four_songs_shipped():
    """W6 SP2 playable roster: 4 songs (Für Elise + 3 new; Canon deferred)."""
    expected = {
        "beethoven_ode_to_joy",
        "beethoven_fur_elise",
        "debussy_clair_de_lune",
        "joplin_the_entertainer",
    }
    actual = {p.stem for p in _all_charts()}
    assert expected.issubset(actual), f"missing: {expected - actual}"
