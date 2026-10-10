# UI icons
- **Item:** The game's icon set: coin, backpack, work lamp, fuel pump, jerrycan, shovel, drill (jackhammer), jetpack. Flat colours, one darker side tone, one dark rounded outline.
- **Purpose:** HUD resources (money, bag, lamps, fuel) and the shop's rows (`Icons.uss` classes `icon-<name>`); on slate they sit on bone tiles, since the outline vanishes on slate.
- **Source/License:** Original. Hand-written SVGs here, each drawn the way common real-world icon sets draw the object (the lamp after our caged work light). No external content.
- **Unity Path:** `unity/Assets/Content/UI/Icons/<name>.png`: 256 px, mipmapped, uncompressed, clamped.
- **Recipe:** `python render_icons.py` (needs `pip install resvg-py`) re-renders every SVG; `--sheet` also writes `Logs/icons-sheet.png` at HUD and shop sizes for review.
- **Status:** In use (menu redesign `117`, on trial).
