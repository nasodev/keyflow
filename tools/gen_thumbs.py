"""Generate 128x128 RGBA typography thumbnails matching existing fur_elise.png style.

Idempotent: rerunning overwrites outputs. Skips fur_elise.png (preserved from W6-1).

Scope: W6-SP2 shipped 3 new songs (Canon in D deferred due to lack of
PD/CC0 MIDI). Targets here correspond to the 3 playable new songs.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

BG = (40, 48, 95)          # #28305F dark blue
FG = (239, 228, 176)       # cream, approximation of existing Für Elise glyph color
SIZE = 128
REPO = Path(__file__).resolve().parents[1]
OUT_DIR = REPO / "Assets" / "StreamingAssets" / "thumbs"

TARGETS = [
    ("ode_to_joy.png", "O"),
    ("clair_de_lune.png", "D"),        # Debussy initial; avoids Canon "C" collision (Canon deferred)
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
    img = Image.new("RGBA", (SIZE, SIZE), BG + (255,))
    draw = ImageDraw.Draw(img)
    font = _find_font(96)
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
