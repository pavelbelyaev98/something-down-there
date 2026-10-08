"""Writes the cave rock's texture set (116): a great cave's stone, dark brown (user, 2026-10-08: "caves are brownish
darkish"; Crystal Caverns' flaky purple-grey cave wall "looks like it is from an apartment"). The source is the Mining
pack's jagged small rock, a scanned brown stone: a square from inside the largest island of its 4K atlas (`crop`, clear of
the islands' stretched padding), colour, normal and occlusion (the pack's mask, G), each blended with its half-offset copy
where its edges meet so it tiles exactly (seamless.py), the colour darkened. 2048 px, into Content/GroundTextures as
CaveRock_*. Run from the repository root (needs Pillow and numpy) after reimporting the pack."""
from pathlib import Path

import numpy as np

from seamless import atlases, load, save, tileable, weight

source = Path(atlases) / 'Rocks_Jagged'
# The square inside the atlas's largest island (x, y, size in source px).
crop = (1980, 2280, 1200)
target = Path('unity/Assets/Content/GroundTextures')
size = 2048
# How much the stone's shading is stretched about its mean, and how much darker the whole is (a cave wall, lit by its
# crystals and lamps rather than the sun).
contrast, level = 1.15, .72
# The cross fade's half width as a share of the tile.
fade = .18

w = weight(size, fade)
colour = tileable(load(source / 'T_Rock_Jagged_Small_BC.tga', 'RGB', crop, size) / 255, w)
mean = colour.mean(axis=(0, 1))
colour = np.clip((mean + (colour - mean) * contrast) * level, 0, 1)
normal = tileable(load(source / 'T_Rock_Jagged_Small_N.tga', 'RGB', crop, size) / 255 * 2 - 1, w)
normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
occlusion = tileable(load(source / 'T_Rock_Jagged_Small_Mask.tga', 'RGBA', crop, size)[..., 1] / 255, w)
save(target, 'CaveRock', colour, normal, occlusion)
