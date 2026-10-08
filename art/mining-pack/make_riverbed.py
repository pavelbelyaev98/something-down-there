"""Writes the riverbed's texture sets (111): zone 3's ground in the cave rock's scanned-stone style (user, 2026-10-08:
"the cave rock vibe ... this is the vibe I want"; the first sand-and-pebble looks had "weird colors and too many rocks").
Each is a seamless square from inside one island of the Mining pack's rounded rocks (seamless.py), graded to a warm
brown a little lighter than the cave rock, so a great cave's stone still stands apart from the ground round it:
  RiverbedConglomerate  A: the large rounded rock's stone, small water-worn pebbles set in it, a warm grey-brown.
  RiverbedBrown         B: the small rounded rock's pebbled stone, browner, nearer the cave rock's colour.
  RiverbedGrit          C: the small rounded rock's gritty brown top, packed sand and grit.
2048 px each (colour, normal, occlusion in the mask's G), into Content/GroundTextures. Run from the repository root
(needs Pillow and numpy) after reimporting the pack; delete a look's files when the user picks another."""
from pathlib import Path

import numpy as np

from seamless import atlases, load, save, tileable, weight

target = Path('unity/Assets/Content/GroundTextures')
size, fade = 2048, .18
luminance = np.array([.2126, .7152, .0722])
# Each look: source rock, crop (x, y, side in source px) inside one atlas island, the mean colour (sRGB 0-255) it is
# graded to, and how much of its own colour variation and contrast it keeps.
looks = {
    'RiverbedConglomerate': ('Rocks_Rounded/T_Rock_Rounded_Large', (400, 1700, 1850), (104, 92, 76), .5, 1.1),
    'RiverbedBrown': ('Rocks_Rounded/T_Rock_Rounded_Small', (1500, 1950, 1700), (98, 81, 62), .5, 1.1),
    'RiverbedGrit': ('Rocks_Rounded/T_Rock_Rounded_Small', (2450, 250, 1100), (100, 84, 65), .6, 1),
}


def to_linear(c):
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= .0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - .055)


def grade(srgb, mean_srgb, keep, contrast):
    """The stone's brightness detail (stretched by contrast) and keep of its colour variation around a new mean colour;
    no light direction is added."""
    linear = to_linear(srgb)
    lum = linear @ luminance
    ratio = 1 + (lum / lum.mean() - 1) * contrast
    hue = linear / np.maximum(lum, 1e-4)[..., None]
    hue = (hue / hue.reshape(-1, 3).mean(0)) ** keep
    mean = to_linear(np.array(mean_srgb, dtype=np.float64) / 255)
    return to_srgb(mean * np.maximum(ratio, 0)[..., None] * hue)


w = weight(size, fade)
for name, (rock, crop, mean, keep, contrast) in looks.items():
    source = Path(atlases) / rock
    colour = tileable(load(f'{source}_BC.tga', 'RGB', crop, size) / 255, w)
    normal = tileable(load(f'{source}_N.tga', 'RGB', crop, size) / 255 * 2 - 1, w)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    occlusion = tileable(load(f'{source}_Mask.tga', 'RGBA', crop, size)[..., 1] / 255, w)
    save(target, name, grade(np.clip(colour, 0, 1), mean, keep, contrast), normal, occlusion)
print('Riverbed texture sets written to', target)
