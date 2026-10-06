"""Writes project maps from the Mining Tools, Ore & Ingots pack (user, 2026-10-06):
- Iron ore without the lava: the pack's orange veins read as glowing lava, so they turn a dull rust brown and the rock a
  little cooler, like hematite in the ground (Ore_Iron_Rust.png).
- The rock sets coal and the copper nugget are built on, at 1024 px (Rocks/<set>_BC/_N/_Mask.png): the pack's 4K TGAs
  are 160 MB a set, and a find a metre away needs no more. Coal takes the layered rocks, the native copper nugget the
  large jagged one.
Run from the repository root (needs Pillow and numpy) after reimporting the pack."""
import colorsys
from pathlib import Path

import numpy as np
from PIL import Image

textures = Path('unity/Assets/REAL_DEDICATED/MiningTools_Ore_Ingots/Textures')
target = Path('unity/Assets/Content/BuriedProps/MiningPack')
size = 1024
rocks = ['Layered_Large', 'Layered_Small', 'Jagged_Large']
# Iron: what counts as an orange vein (hue in degrees, saturation), the rust it becomes (sRGB) at what share of its
# brightness, and how much colour the rest of the rock keeps.
vein_hue, vein_saturation = (0, 45), .22
rust, rust_value = np.array([.4, .25, .18]), .65
rock_saturation = .5


def load(path, mode):
    return Image.open(path).convert(mode).resize((size, size), Image.LANCZOS)


iron = np.asarray(load(textures / 'T_Ore_Iron_BC.tga', 'RGB'), dtype=np.float64) / 255
hsv = np.vectorize(colorsys.rgb_to_hsv)(iron[..., 0], iron[..., 1], iron[..., 2])
hue, saturation, value = hsv[0] * 360, hsv[1], hsv[2]
vein = np.clip((saturation - vein_saturation) / .2, 0, 1) * ((hue >= vein_hue[0]) & (hue <= vein_hue[1]))
grey = (iron @ np.array([.2126, .7152, .0722]))[..., None]
rock = grey + (iron - grey) * rock_saturation
# The vein's brightness carries over, scaled so the rust's brightest channel matches it.
colour = rock * (1 - vein[..., None]) + np.clip(rust / rust.max() * (value * rust_value)[..., None], 0, 1) * vein[..., None]
target.mkdir(parents=True, exist_ok=True)
Image.fromarray(np.uint8(np.clip(colour, 0, 1) * 255 + .5)).save(target / 'Ore_Iron_Rust.png')
print('wrote', target / 'Ore_Iron_Rust.png')

(target / 'Rocks').mkdir(exist_ok=True)
for rock_set in rocks:
    for channel, mode in (('BC', 'RGB'), ('N', 'RGB'), ('Mask', 'RGBA')):
        load(textures / f'Rocks_{rock_set.split("_")[0]}' / f'T_Rock_{rock_set}_{channel}.tga', mode).save(target / 'Rocks' / f'{rock_set}_{channel}.png')
    print('wrote', target / 'Rocks' / f'{rock_set}_*.png')
