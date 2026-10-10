"""Render the UI icon SVGs in this folder to 256 px PNGs for Unity.

Usage (needs `pip install resvg-py`; Pillow only for --sheet):
    python render_icons.py            # writes unity/Assets/Content/UI/Icons/<name>.png
    python render_icons.py --sheet    # also writes Logs/icons-sheet.png: every icon at
                                      # HUD and shop sizes on the slate sheet and on the world
"""
import io
import pathlib
import sys

import resvg_py

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parent.parent
OUT = REPO / "unity" / "Assets" / "Content" / "UI" / "Icons"
SIZE = 256


def render(svg: pathlib.Path, size: int) -> bytes:
    return bytes(resvg_py.svg_to_bytes(svg_path=str(svg), width=size, height=size))


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    icons = sorted(HERE.glob("*.svg"))
    for svg in icons:
        (OUT / (svg.stem + ".png")).write_bytes(render(svg, SIZE))
        print("wrote", (OUT / (svg.stem + ".png")).relative_to(REPO))
    if "--sheet" in sys.argv:
        sheet(icons)


def sheet(icons) -> None:
    from PIL import Image

    sizes = (128, 72, 36)
    backgrounds = ((31, 42, 48), (40, 52, 57), (126, 82, 46))  # slate sheet, slate tile, dug soil
    cell = max(sizes) + 24
    image = Image.new("RGB", (cell * len(icons), cell * len(sizes) * len(backgrounds)))
    for b, background in enumerate(backgrounds):
        for s, size in enumerate(sizes):
            y = (b * len(sizes) + s) * cell
            for i, svg in enumerate(icons):
                image.paste(background, (i * cell, y, (i + 1) * cell, y + cell))
                icon = Image.open(io.BytesIO(render(svg, SIZE))).resize((size, size), Image.LANCZOS)
                image.paste(icon, (i * cell + (cell - size) // 2, y + (cell - size) // 2), icon)
    target = REPO / "Logs" / "icons-sheet.png"
    target.parent.mkdir(exist_ok=True)
    image.save(target)
    print("wrote", target.relative_to(REPO))


if __name__ == "__main__":
    main()
