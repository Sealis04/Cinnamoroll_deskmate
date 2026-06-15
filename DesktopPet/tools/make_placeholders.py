#!/usr/bin/env python3
"""Generate placeholder sprites so Track B can be built before Track A renders.

Writes flat-colored stand-ins that follow the naming contract in
assets/sprites/README.md (plan §2.4). Requires Pillow (`pip install pillow`).

Run from the DesktopPet/ directory:
    python3 tools/make_placeholders.py
"""
from __future__ import annotations

import os

try:
    from PIL import Image, ImageDraw
except ImportError:
    raise SystemExit(
        "Pillow not installed. `pip install pillow`, or skip — the app renders "
        "missing sprites as magenta placeholders at runtime (see AssetLoader)."
    )

SIZE = 512
OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "sprites")
CENTER = (SIZE // 2, SIZE // 2)
BODY_RGBA = (180, 220, 255, 255)   # pale Cinnamoroll blue-white
PUPIL_RGBA = (40, 40, 60, 255)

# Eye-socket centers — keep in sync with EyeTracker exports.
LEFT_EYE = (210, 230)
RIGHT_EYE = (302, 230)


def _canvas() -> tuple[Image.Image, "ImageDraw.ImageDraw"]:
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def _body(draw: "ImageDraw.ImageDraw", tint=BODY_RGBA) -> None:
    draw.ellipse([136, 136, 376, 376], fill=tint)


def base() -> Image.Image:
    img, d = _canvas()
    _body(d)
    for ex, ey in (LEFT_EYE, RIGHT_EYE):  # eye whites, no pupils
        d.ellipse([ex - 14, ey - 12, ex + 14, ey + 12], fill=(255, 255, 255, 255))
    return img


def pupils() -> Image.Image:
    img, d = _canvas()
    for ex, ey in (LEFT_EYE, RIGHT_EYE):
        d.ellipse([ex - 6, ey - 6, ex + 6, ey + 6], fill=PUPIL_RGBA)
    return img


def frame(i: int, n: int) -> Image.Image:
    """A body that bobs vertically so sequences are visibly animated."""
    img, d = _canvas()
    dy = int(12 * (0.5 - abs(i / max(n - 1, 1) - 0.5)) * 2)
    d.ellipse([136, 136 - dy, 376, 376 - dy], fill=BODY_RGBA)
    return img


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    base().save(os.path.join(OUT, "idle_neutral_base.png"))
    pupils().save(os.path.join(OUT, "idle_neutral_pupils.png"))

    sequences = {"anim_blink": 3, "anim_bounce": 6, "react_click": 4,
                 "react_drag": 4, "react_sleep": 4, "anim_lookaround": 6, "anim_sit": 4}
    for state, n in sequences.items():
        for i in range(n):
            frame(i, n).save(os.path.join(OUT, f"{state}_{i:03d}.png"))

    print(f"Wrote placeholders to {os.path.normpath(OUT)}")


if __name__ == "__main__":
    main()
