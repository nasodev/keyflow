"""Shared pytest fixtures for midi_to_kfchart tests."""
import sys
from pathlib import Path

# Allow `from pipeline import ...` from test files without installing as package.
ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
