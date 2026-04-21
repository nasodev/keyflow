# midi_to_kfchart

Python pipeline that converts MIDI files into KeyFlow `.kfchart` charts.

## Setup (Windows)

    python -m venv .venv
    .venv\Scripts\activate
    pip install -r requirements.txt

## Run tests

    pytest -q

## Single-file conversion

    python midi_to_kfchart.py <input.mid> --song-id <id> --title "<T>" \
      --composer "<C>" --difficulty NORMAL --target-nps 3.5 --bpm 72 \
      --duration-ms 45000 --merge-into Assets/StreamingAssets/charts/<id>.kfchart

## Batch conversion

    python midi_to_kfchart.py --batch batch.yaml

See `batch.yaml.example` for schema.

## Tuning loop

1. Run pipeline → `.kfchart` output.
2. Play in Unity Editor.
3. Adjust `--target-nps` or hand-edit the JSON (hand-edits will be lost on re-run).
