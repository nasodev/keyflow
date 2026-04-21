# KeyFlow W6 SP2 — Four-Song Content Pack Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 4 PD songs (Ode to Joy, Canon in D, Clair de Lune, The Entertainer) × Easy+Normal to the KeyFlow MVP via batch YAML pipeline, unlocking all 5 song slots in the catalog.

**Architecture:** Content-only sub-project. Mutopia PD/CC0 MIDI → W5 pipeline (`midi_to_kfchart.py --batch`) → 4 `.kfchart` files. Self-generated typography PNGs (PIL) for thumbnails matching existing Für Elise style. Manifest update replaces 4 placeholders. ApkBuilder artifact name bumped for traceability. **No C# code changes, no Python pipeline code changes.**

**Tech Stack:** Python 3 + `mido` + `PyYAML` + `Pillow` (new); Unity 6000.x Android IL2CPP; existing W5/W6-1 infrastructure.

**Spec:** `docs/superpowers/specs/2026-04-22-keyflow-w6-sp2-four-songs-design.md`

---

## File structure

**Created:**
- `tools/midi_to_kfchart/batch_w6_sp2.yaml` — batch config for 4 songs × 2 difficulties
- `tools/midi_to_kfchart/midi_sources/ode_to_joy.mid`
- `tools/midi_to_kfchart/midi_sources/canon_in_d.mid`
- `tools/midi_to_kfchart/midi_sources/clair_de_lune.mid`
- `tools/midi_to_kfchart/midi_sources/the_entertainer.mid`
- `tools/midi_to_kfchart/tests/test_w6_sp2_charts.py` — asset regression test (parametrized over all 5 `.kfchart` files)
- `tools/gen_thumbs.py` — PIL-based typography thumbnail generator
- `Assets/StreamingAssets/charts/beethoven_ode_to_joy.kfchart`
- `Assets/StreamingAssets/charts/pachelbel_canon_in_d.kfchart`
- `Assets/StreamingAssets/charts/debussy_clair_de_lune.kfchart`
- `Assets/StreamingAssets/charts/joplin_the_entertainer.kfchart`
- `Assets/StreamingAssets/thumbs/ode_to_joy.png`
- `Assets/StreamingAssets/thumbs/canon_in_d.png`
- `Assets/StreamingAssets/thumbs/clair_de_lune.png`
- `Assets/StreamingAssets/thumbs/the_entertainer.png`

**Modified:**
- `tools/midi_to_kfchart/requirements.txt` — add `Pillow`
- `Assets/StreamingAssets/catalog.kfmanifest` — 5 songs unlocked in MVP §3 order
- `Assets/Editor/ApkBuilder.cs:14` — output name `keyflow-w6.apk` → `keyflow-w6-sp2.apk`

**NOT modified (guardrails):**
- No C# runtime code (SongCatalog/MainScreen/SongCardView unchanged)
- No Python pipeline code (`pipeline/*.py`, `midi_to_kfchart.py` unchanged)
- Für Elise chart and thumbnail left as-is (W6-1 device-validated state preserved)

---

## Task 1: Collect PD/CC0 MIDI from Mutopia Project

**Files:**
- Create: `tools/midi_to_kfchart/midi_sources/ode_to_joy.mid`
- Create: `tools/midi_to_kfchart/midi_sources/canon_in_d.mid`
- Create: `tools/midi_to_kfchart/midi_sources/clair_de_lune.mid`
- Create: `tools/midi_to_kfchart/midi_sources/the_entertainer.mid`

For each of the 4 songs, find a Mutopia Project piece with `License: Public Domain` or `License: Creative Commons 0` and download its `.mid` file.

- [ ] **Step 1: Create midi_sources directory**

```bash
mkdir -p tools/midi_to_kfchart/midi_sources
```

- [ ] **Step 2: Search Mutopia for each piece**

For each song, use WebFetch against `https://www.mutopiaproject.org/cgibin/make-table.cgi?searchingfor=<query>`:

| song_id | Query |
|---|---|
| `ode_to_joy` | `Ode+to+Joy` or `Beethoven+Symphony+9` |
| `canon_in_d` | `Canon+Pachelbel` |
| `clair_de_lune` | `Debussy+Clair+de+Lune` or `Suite+bergamasque` |
| `the_entertainer` | `Joplin+Entertainer` |

From the results page, pick the piece whose **License column** shows `Public Domain` or `CC0`. Skip CC-BY / CC-BY-SA entries.

- [ ] **Step 3: Verify license on piece page**

WebFetch the piece's detail page. Confirm the license text explicitly says `Public Domain` or `Creative Commons 0`. Extract: Mutopia piece ID, arranger/maintainer name, license string, `.mid` download URL.

- [ ] **Step 4: Download each .mid file**

```bash
# Example pattern per song:
curl -L -o tools/midi_to_kfchart/midi_sources/ode_to_joy.mid "<mutopia_mid_url>"
```

Repeat for all 4 songs. Verify each file is non-empty and parseable:

```bash
python -c "import mido; mido.MidiFile('tools/midi_to_kfchart/midi_sources/ode_to_joy.mid')"
```

Expected: no exception. Repeat for other 3.

- [ ] **Step 5: Fallback for unavailable songs**

If WebFetch reveals no PD/CC0 MIDI for any of the 4 songs on Mutopia:
- STOP. Do not proceed to Task 2.
- Document which song failed, what searches were attempted, and what licenses were available.
- Report back with a summary; the user will decide whether to (a) drop that song from scope, (b) search an alternative PD archive, or (c) commission self-sequencing as a separate sub-project.

- [ ] **Step 6: Commit MIDI sources**

Commit message must record Mutopia piece ID, URL, and license for each of the 4 files:

```bash
git add tools/midi_to_kfchart/midi_sources/
git commit -m "$(cat <<'EOF'
feat(w6-sp2): 4 PD/CC0 MIDI sources from Mutopia

- ode_to_joy.mid: Mutopia piece <ID>, <arranger>, License: <PD|CC0>
  <url>
- canon_in_d.mid: Mutopia piece <ID>, <arranger>, License: <PD|CC0>
  <url>
- clair_de_lune.mid: Mutopia piece <ID>, <arranger>, License: <PD|CC0>
  <url>
- the_entertainer.mid: Mutopia piece <ID>, <arranger>, License: <PD|CC0>
  <url>
EOF
)"
```

---

## Task 2: Create batch YAML

**Files:**
- Create: `tools/midi_to_kfchart/batch_w6_sp2.yaml`

- [ ] **Step 1: Write the YAML file**

```yaml
defaults:
  out_dir: Assets/StreamingAssets/charts/

songs:
  - song_id: beethoven_ode_to_joy
    midi: midi_sources/ode_to_joy.mid
    title: "Ode to Joy"
    composer: "Beethoven"
    bpm: 120
    duration_ms: 120000
    difficulties:
      EASY:   { target_nps: 1.5 }
      NORMAL: { target_nps: 3.0 }

  - song_id: pachelbel_canon_in_d
    midi: midi_sources/canon_in_d.mid
    title: "Canon in D"
    composer: "Pachelbel"
    bpm: 60
    duration_ms: 120000
    difficulties:
      EASY:   { target_nps: 1.8 }
      NORMAL: { target_nps: 3.2 }

  - song_id: debussy_clair_de_lune
    midi: midi_sources/clair_de_lune.mid
    title: "Clair de Lune"
    composer: "Debussy"
    bpm: 66
    duration_ms: 120000
    difficulties:
      EASY:   { target_nps: 1.5 }
      NORMAL: { target_nps: 2.8 }

  - song_id: joplin_the_entertainer
    midi: midi_sources/the_entertainer.mid
    title: "The Entertainer"
    composer: "Joplin"
    bpm: 100
    duration_ms: 120000
    difficulties:
      EASY:   { target_nps: 2.2 }
      NORMAL: { target_nps: 4.0 }
```

- [ ] **Step 2: Verify YAML parses**

```bash
cd tools/midi_to_kfchart
python -c "import yaml; from pathlib import Path; print(yaml.safe_load(Path('batch_w6_sp2.yaml').read_text()))"
```

Expected: dict with `defaults` and `songs` keys, 4 song entries.

- [ ] **Step 3: If BPM guideline mismatches actual MIDI header**

After downloading MIDIs in Task 1, `mido` can inspect tempo:

```bash
python -c "
import mido
mf = mido.MidiFile('tools/midi_to_kfchart/midi_sources/canon_in_d.mid')
for msg in mf:
    if msg.type == 'set_tempo':
        print('BPM:', mido.tempo2bpm(msg.tempo)); break
"
```

If actual BPM differs significantly from the guideline (e.g., Canon comes in at 90 BPM instead of 60), update `batch_w6_sp2.yaml` with the MIDI's actual BPM before running the pipeline. The pipeline uses this for timing calculations; a wrong value produces drift.

- [ ] **Step 4: Commit**

```bash
git add tools/midi_to_kfchart/batch_w6_sp2.yaml
git commit -m "feat(w6-sp2): batch YAML for 4-song content pack"
```

---

## Task 3: Write failing chart-acceptance test (TDD red)

**Files:**
- Create: `tools/midi_to_kfchart/tests/test_w6_sp2_charts.py`

This test parses every `.kfchart` in `Assets/StreamingAssets/charts/` and asserts the acceptance criteria from spec §9. Parametrizing over the directory means future chart additions auto-join the suite.

- [ ] **Step 1: Write the test**

```python
"""Regression test for shipped .kfchart assets (spec §9 acceptance)."""
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
            assert notes[0]["t"] >= 1000, (
                f"{chart_path.stem} {diff_name}: first note before 1000ms"
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


def test_five_songs_shipped():
    """MVP roster: all 5 songs have .kfchart files."""
    expected = {
        "beethoven_ode_to_joy",
        "beethoven_fur_elise",
        "pachelbel_canon_in_d",
        "debussy_clair_de_lune",
        "joplin_the_entertainer",
    }
    actual = {p.stem for p in _all_charts()}
    assert expected.issubset(actual), f"missing: {expected - actual}"
```

- [ ] **Step 2: Run test to confirm it fails**

```bash
cd tools/midi_to_kfchart && pytest tests/test_w6_sp2_charts.py -v
```

Expected: `test_five_songs_shipped` FAILS with "missing: {beethoven_ode_to_joy, ...}". `test_chart_passes_acceptance` parametrized only over Für Elise (passes for that one row).

- [ ] **Step 3: Commit failing test**

```bash
git add tools/midi_to_kfchart/tests/test_w6_sp2_charts.py
git commit -m "test(w6-sp2): shipped .kfchart acceptance (fails red)"
```

---

## Task 4: Generate 4 charts via pipeline (TDD green)

**Files:**
- Create: `Assets/StreamingAssets/charts/beethoven_ode_to_joy.kfchart`
- Create: `Assets/StreamingAssets/charts/pachelbel_canon_in_d.kfchart`
- Create: `Assets/StreamingAssets/charts/debussy_clair_de_lune.kfchart`
- Create: `Assets/StreamingAssets/charts/joplin_the_entertainer.kfchart`

- [ ] **Step 1: Run the batch pipeline**

```bash
cd tools/midi_to_kfchart
python midi_to_kfchart.py --batch batch_w6_sp2.yaml
```

Expected output: 8 `[OK] <DIFF>: <N> notes -> .../Assets/StreamingAssets/charts/<song_id>.kfchart` lines (one per difficulty per song). `rc=0`. No `[FAIL]` lines.

- [ ] **Step 2: Run the acceptance test (expect green)**

```bash
cd tools/midi_to_kfchart && pytest tests/test_w6_sp2_charts.py -v
```

Expected: `test_five_songs_shipped` PASSES. `test_chart_passes_acceptance` runs 5 parametrized cases (all 5 songs), all PASS.

- [ ] **Step 3: If acceptance fails, diagnose**

Common failure modes:
- `pitch` out of range 36–84 → the raw Mutopia MIDI has notes outside the Salamander bank. Fix: the pipeline's `pitch_clamp.clamp_pitches` should already clamp; if it doesn't, the bug is pre-existing and out of this sub-project's scope. Flag and stop.
- `first note t < 1000` → pipeline's note placement placed the first note too early. Adjust `bpm` upward in YAML to compress timeline, or re-run.
- `NORMAL <= EASY` note count → `target_nps` too close between difficulties. Adjust YAML (increase NORMAL target_nps).
- `last note > durationMs` → source MIDI longer than 120s; pipeline should truncate. If it didn't, bug — flag.

After any YAML adjustment, re-run Step 1 and Step 2.

- [ ] **Step 4: Run full Python test suite (regression)**

```bash
cd tools/midi_to_kfchart && pytest -v
```

Expected: all 33 tests pass (32 existing + 1 new `test_w6_sp2_charts.py` case, or 32 + parametrized count — pytest counts each parametrize row as 1 test, so: 32 + 5 parametrized + 1 `test_five_songs_shipped` = 38 total; exact count is not critical, just no failures).

- [ ] **Step 5: Commit generated charts**

```bash
git add Assets/StreamingAssets/charts/*.kfchart Assets/StreamingAssets/charts/*.meta
git commit -m "feat(w6-sp2): 4 generated .kfchart via batch pipeline"
```

Note: Unity will auto-generate `.meta` files for the new `.kfchart` files on next Editor launch; if not yet present at commit time, commit them in Task 8 after the Editor verification run.

---

## Task 5: Thumbnail generator tool + 4 PNGs

**Files:**
- Modify: `tools/midi_to_kfchart/requirements.txt` — add `Pillow==10.4.0`
- Create: `tools/gen_thumbs.py`
- Create: `Assets/StreamingAssets/thumbs/ode_to_joy.png`
- Create: `Assets/StreamingAssets/thumbs/canon_in_d.png`
- Create: `Assets/StreamingAssets/thumbs/clair_de_lune.png`
- Create: `Assets/StreamingAssets/thumbs/the_entertainer.png`

- [ ] **Step 1: Add Pillow to requirements**

Edit `tools/midi_to_kfchart/requirements.txt` — append one line:

```
Pillow==10.4.0
```

- [ ] **Step 2: Install Pillow**

```bash
cd tools/midi_to_kfchart
pip install -r requirements.txt
```

Expected: `Pillow` installs successfully.

- [ ] **Step 3: Write gen_thumbs.py**

Create `tools/gen_thumbs.py` with this exact content:

```python
"""Generate 64x64 typography thumbnails matching existing fur_elise.png style.

Idempotent: rerunning overwrites outputs. Skips fur_elise.png (preserved from W6-1).
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

BG = (40, 48, 95)          # #28305F dark blue
FG = (239, 228, 176)       # cream, approximation of existing Für Elise glyph color
SIZE = 64
REPO = Path(__file__).resolve().parents[1]
OUT_DIR = REPO / "Assets" / "StreamingAssets" / "thumbs"

TARGETS = [
    ("ode_to_joy.png", "O"),
    ("canon_in_d.png", "C"),
    ("clair_de_lune.png", "D"),       # avoid "C" collision; Debussy initial
    ("the_entertainer.png", "E"),
]


def _find_font(px: int) -> ImageFont.FreeTypeFont:
    """Best-effort sans-serif bold font lookup across Windows/macOS/Linux."""
    candidates = [
        "arialbd.ttf", "Arial Bold.ttf",
        "DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "C:/Windows/Fonts/arialbd.ttf",
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
    ]
    for path in candidates:
        try:
            return ImageFont.truetype(path, px)
        except (OSError, IOError):
            continue
    return ImageFont.load_default()


def _render(glyph: str, out_path: Path) -> None:
    img = Image.new("RGB", (SIZE, SIZE), BG)
    draw = ImageDraw.Draw(img)
    font = _find_font(48)
    # Center the glyph
    bbox = draw.textbbox((0, 0), glyph, font=font)
    w, h = bbox[2] - bbox[0], bbox[3] - bbox[1]
    x = (SIZE - w) // 2 - bbox[0]
    y = (SIZE - h) // 2 - bbox[1]
    draw.text((x, y), glyph, fill=FG, font=font)
    img.save(out_path, format="PNG", optimize=True)
    print(f"[OK] wrote {out_path}")


def main() -> int:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for filename, glyph in TARGETS:
        _render(glyph, OUT_DIR / filename)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 4: Run the generator**

```bash
python tools/gen_thumbs.py
```

Expected: 4 `[OK] wrote ...` lines, 4 new PNG files at the paths above.

- [ ] **Step 5: Verify PNG dimensions**

```bash
python -c "
from PIL import Image
from pathlib import Path
for p in Path('Assets/StreamingAssets/thumbs').glob('*.png'):
    im = Image.open(p)
    print(p.name, im.size, im.mode)
"
```

Expected: 5 lines (4 new + existing `fur_elise.png` + `locked.png`). New files should report `(64, 64) RGB`.

- [ ] **Step 6: Commit tool + assets**

```bash
git add tools/gen_thumbs.py tools/midi_to_kfchart/requirements.txt \
        Assets/StreamingAssets/thumbs/ode_to_joy.png \
        Assets/StreamingAssets/thumbs/canon_in_d.png \
        Assets/StreamingAssets/thumbs/clair_de_lune.png \
        Assets/StreamingAssets/thumbs/the_entertainer.png
git commit -m "feat(w6-sp2): gen_thumbs.py + 4 typography PNGs"
```

Note: `.meta` files for the new PNGs commit in Task 8.

---

## Task 6: Update catalog.kfmanifest

**Files:**
- Modify: `Assets/StreamingAssets/catalog.kfmanifest`

- [ ] **Step 1: Replace placeholders with real metadata**

Replace the entire file contents with:

```json
{
  "version": 1,
  "songs": [
    { "id": "beethoven_ode_to_joy",     "title": "환희의 송가",     "composer": "Beethoven",
      "thumbnail": "thumbs/ode_to_joy.png",      "difficulties": ["Easy", "Normal"], "chartAvailable": true },
    { "id": "beethoven_fur_elise",      "title": "엘리제를 위하여", "composer": "Beethoven",
      "thumbnail": "thumbs/fur_elise.png",       "difficulties": ["Easy", "Normal"], "chartAvailable": true },
    { "id": "pachelbel_canon_in_d",     "title": "Canon in D",      "composer": "Pachelbel",
      "thumbnail": "thumbs/canon_in_d.png",      "difficulties": ["Easy", "Normal"], "chartAvailable": true },
    { "id": "debussy_clair_de_lune",    "title": "Clair de Lune",   "composer": "Debussy",
      "thumbnail": "thumbs/clair_de_lune.png",   "difficulties": ["Easy", "Normal"], "chartAvailable": true },
    { "id": "joplin_the_entertainer",   "title": "The Entertainer", "composer": "Joplin",
      "thumbnail": "thumbs/the_entertainer.png", "difficulties": ["Easy", "Normal"], "chartAvailable": true }
  ]
}
```

- [ ] **Step 2: Validate JSON**

```bash
python -c "import json; json.loads(open('Assets/StreamingAssets/catalog.kfmanifest', encoding='utf-8').read())"
```

Expected: no exception.

- [ ] **Step 3: Verify each song_id matches a chart file**

```bash
python -c "
import json
from pathlib import Path
cat = json.loads(open('Assets/StreamingAssets/catalog.kfmanifest', encoding='utf-8').read())
charts = {p.stem for p in Path('Assets/StreamingAssets/charts').glob('*.kfchart')}
for s in cat['songs']:
    if s['chartAvailable']:
        assert s['id'] in charts, f'Missing chart for {s[\"id\"]}'
print('OK: all unlocked songs have charts')
"
```

Expected: `OK: all unlocked songs have charts`.

- [ ] **Step 4: Commit**

```bash
git add Assets/StreamingAssets/catalog.kfmanifest
git commit -m "feat(w6-sp2): catalog.kfmanifest — 5 songs unlocked"
```

---

## Task 7: Rename APK artifact

**Files:**
- Modify: `Assets/Editor/ApkBuilder.cs:14`

- [ ] **Step 1: Edit ApkBuilder.cs**

Change line 14 from:

```csharp
string apk = Path.Combine(dir, "keyflow-w6.apk");
```

to:

```csharp
string apk = Path.Combine(dir, "keyflow-w6-sp2.apk");
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Editor/ApkBuilder.cs
git commit -m "chore(w6-sp2): APK artifact name keyflow-w6-sp2.apk"
```

---

## Task 8: Unity Editor verification (EditMode + asset .meta)

**Files:**
- Create (auto): `Assets/StreamingAssets/charts/*.kfchart.meta` (4)
- Create (auto): `Assets/StreamingAssets/thumbs/*.png.meta` (4)

Launching Unity Editor imports the new assets and auto-generates `.meta` files. EditMode tests then confirm no C# regression.

Foreground, no pipes, no background per the project's persistent "Unity batch mode" rule.

- [ ] **Step 1: Run EditMode tests (regression check)**

```bash
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" \
  -batchmode -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults-EditMode-w6sp2.xml \
  -logFile Logs/editmode-w6sp2.log
```

**IMPORTANT**: No `-quit` flag (persistent memory: `-runTests` + `-quit` skips the runner). No `run_in_background`. No stdout pipe.

Expected: 112 / 112 pass (same as W6-1 baseline, no new EditMode tests in this sub-project).

- [ ] **Step 2: Inspect results**

```bash
grep -E "^<test-run |total=|passed=|failed=" TestResults-EditMode-w6sp2.xml | head -5
```

Expected: `passed="112" failed="0"`.

- [ ] **Step 3: Stage .meta files for new assets**

After the Editor run, the following .meta files should now exist:
- `Assets/StreamingAssets/charts/beethoven_ode_to_joy.kfchart.meta`
- `Assets/StreamingAssets/charts/pachelbel_canon_in_d.kfchart.meta`
- `Assets/StreamingAssets/charts/debussy_clair_de_lune.kfchart.meta`
- `Assets/StreamingAssets/charts/joplin_the_entertainer.kfchart.meta`
- `Assets/StreamingAssets/thumbs/ode_to_joy.png.meta`
- `Assets/StreamingAssets/thumbs/canon_in_d.png.meta`
- `Assets/StreamingAssets/thumbs/clair_de_lune.png.meta`
- `Assets/StreamingAssets/thumbs/the_entertainer.png.meta`

Verify they exist:

```bash
ls Assets/StreamingAssets/charts/*.meta Assets/StreamingAssets/thumbs/*.meta
```

Expected: 7 chart `.meta` (existing 1 + new 4 + 2 hidden) + thumb `.meta` files listed. If any are missing, open the project in Unity Editor GUI once to trigger import, then re-check.

- [ ] **Step 4: Commit .meta files**

```bash
git add Assets/StreamingAssets/charts/*.kfchart.meta \
        Assets/StreamingAssets/thumbs/*.png.meta
git commit -m "chore(w6-sp2): Unity auto-generated .meta for new assets"
```

---

## Task 9: APK build + device playtest

**Files:**
- Create (build output): `Builds/keyflow-w6-sp2.apk`

- [ ] **Step 1: Build the APK**

```bash
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" \
  -batchmode -projectPath . \
  -executeMethod KeyFlow.Editor.ApkBuilder.Build \
  -quit \
  -logFile Logs/build-w6sp2.log
```

**IMPORTANT**: `-executeMethod` REQUIRES `-quit` (persistent memory). Foreground, no pipes, no background.

Expected: exit 0. `Builds/keyflow-w6-sp2.apk` exists.

- [ ] **Step 2: Handle transient IL2CPP flakiness**

If the build fails mid-IL2CPP with `IPCStream` or `Not connected` error (known flaky per W6-1 report), simply retry Step 1 once. If a second attempt also fails, clear `Library/PackageCache/` and retry.

- [ ] **Step 3: Install on device**

```bash
adb devices
# Confirm R5CT21A31QB is listed
adb install -r Builds/keyflow-w6-sp2.apk
```

Expected: `Success`.

- [ ] **Step 4: Device playtest — hand off to user**

Request the user to perform the following on the S22:

1. Launch KeyFlow. Confirm no ANR, main screen renders.
2. Confirm 5 song cards visible: Ode to Joy, Für Elise, Canon in D, Clair de Lune, The Entertainer — with thumbnails.
3. For each of the 4 new songs, play **both Easy and Normal** once (= 8 runs). Note any crash, freeze, or audio glitch.
4. Replay Für Elise Normal once (regression check for W6-1 multi-pitch audio).
5. Confirm subjective impression — does each song "feel like" the melody?

Expected: 10 runs total (9 new + 1 regression), 0 crashes, pitch audio matches chart melody for each song.

If a playtest issue is reported: do **not** auto-fix. Document in the completion report and hand off for separate triage.

- [ ] **Step 5: Commit APK artifact**

```bash
git add Builds/keyflow-w6-sp2.apk
git commit -m "chore(w6-sp2): keyflow-w6-sp2.apk device-validated"
```

---

## Task 10: Completion report

**Files:**
- Create: `docs/superpowers/reports/2026-04-22-w6-sp2-four-songs-completion.md`

- [ ] **Step 1: Write the completion report**

Follow the structure of `docs/superpowers/reports/2026-04-21-w6-multipitch-samples-completion.md`:

- Scope delivered (what shipped)
- Per-song Mutopia source (piece ID, URL, license, note counts Easy/Normal, any BPM deviations from guideline)
- Test counts before/after (pytest: 32 → 32 + N where N = parametrized chart tests; EditMode: 112/112 unchanged)
- Commit list oldest → newest
- Deviations from plan
- Operational findings (Mutopia availability issues, IL2CPP flakiness recurrence, etc.)
- Device validation confirmation (user quote)
- Next steps → W6 priorities 3–6

- [ ] **Step 2: Commit report**

```bash
git add docs/superpowers/reports/2026-04-22-w6-sp2-four-songs-completion.md
git commit -m "docs(w6-sp2): completion report"
```

- [ ] **Step 3: Final branch summary**

```bash
git log --oneline main..HEAD
```

Review the commit sequence. Expected ~8-10 commits on this branch.

Hand off to `superpowers:finishing-a-development-branch` skill for merge decision.

---

## Risks recap (from spec §11)

- **R1 (High)**: Mutopia PD/CC0 MIDI unavailable for one or more songs → Task 1 Step 5 fallback.
- **R2 (Medium)**: Multi-part piano MIDI melody track ambiguity → Task 4 Step 3 diagnosis flow.
- **R3 (Medium)**: BPM guideline drift from actual MIDI → Task 2 Step 3 inspection + YAML update.
- **R5 (Medium)**: IL2CPP flakiness → Task 9 Step 2 retry procedure.

## Non-obvious constraints

- Unity batch mode: foreground only, no `run_in_background`, no piped stdout (persistent memory).
- `-runTests` MUST omit `-quit`; `-executeMethod` MUST include `-quit`.
- Salamander sample bank range: MIDI 36–84 only. Notes outside this range are clamped to the nearest boundary sample by `ResolveSample`'s ±1 semitone logic (silent fallback beyond that). Task 3 test enforces this range to prevent Mutopia MIDI leaking extreme notes.
- No runtime C# edits. Any proposed C# change beyond `ApkBuilder.cs:14` is out of scope — flag and defer.
- Für Elise chart/thumbnail are device-validated W6-1 outputs and must not be modified by this sub-project.
