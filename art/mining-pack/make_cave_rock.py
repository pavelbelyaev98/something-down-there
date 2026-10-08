"""Writes the cave rock's texture set (116): a great cave's stone, dark brown (user, 2026-10-08: "caves are brownish
darkish"; Crystal Caverns' flaky purple-grey cave wall "looks like it is from an apartment"). The source is the Mining
pack's jagged small rock, a scanned brown stone: a square from inside the largest island of its 4K atlas (`crop`, clear of
the islands' stretched padding), colour, normal and occlusion (the pack's mask, G), each blended with its half-offset copy
where its edges meet so it tiles exactly, the colour darkened. 2048 px, into Content/GroundTextures as CaveRock_*.
Run from the repository root (needs Pillow and numpy) after reimporting the pack."""
from pathlib import Path

import numpy as np
from PIL import Image

source = Path('unity/Assets/REAL_DEDICATED/MiningTools_Ore_Ingots/Textures/Rocks_Jagged')
# The square inside the atlas's largest island (x, y, size in source px).
crop = (1980, 2280, 1200)
target = Path('unity/Assets/Content/GroundTextures')
size = 2048
# How much the stone's shading is stretched about its mean, and how much darker the whole is (a cave wall, lit by its
# crystals and lamps rather than the sun).
contrast, level = 1.15, .72
# The cross fade's half width as a share of the tile.
fade = .18


def load(path, mode):
    x, y, side = crop
    return np.asarray(Image.open(path).convert(mode).crop((x, y, x + side, y + side)).resize((size, size), Image.LANCZOS), dtype=np.float64)


def weight():
    """1 in the middle of the tile, falling to 0 within `fade` of its edges, so the half-offset copy covers them."""
    t = np.linspace(0, 1, size, endpoint=False) + .5 / size
    edge = np.clip(np.minimum(t, 1 - t) / fade, 0, 1)
    edge = edge * edge * (3 - 2 * edge)
    return np.minimum.outer(edge, edge)


def tileable(image, w):
    """The image over its half-offset copy by w, rescaled about the mean by 1 / sqrt(w^2 + (1 - w)^2) so the cross keeps
    the image's contrast (variance-preserving blending)."""
    shifted = np.roll(np.roll(image, size // 2, axis=0), size // 2, axis=1)
    w3 = w[..., None] if image.ndim == 3 else w
    blend = image * w3 + shifted * (1 - w3)
    mean = image.mean(axis=(0, 1))
    scale = 1 / np.sqrt(w * w + (1 - w) ** 2)
    return mean + (blend - mean) * (scale[..., None] if image.ndim == 3 else scale)


w = weight()
colour = tileable(load(source / 'T_Rock_Jagged_Small_BC.tga', 'RGB') / 255, w)
mean = colour.mean(axis=(0, 1))
colour = np.clip((mean + (colour - mean) * contrast) * level, 0, 1)
normal = tileable(load(source / 'T_Rock_Jagged_Small_N.tga', 'RGB') / 255 * 2 - 1, w)
normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
occlusion = tileable(load(source / 'T_Rock_Jagged_Small_Mask.tga', 'RGBA')[..., 1] / 255, w)
target.mkdir(parents=True, exist_ok=True)
Image.fromarray(np.uint8(colour * 255 + .5)).save(target / 'CaveRock_Albedo.png')
Image.fromarray(np.uint8(np.clip((normal + 1) / 2, 0, 1) * 255 + .5)).save(target / 'CaveRock_Normal.png')
full = np.full(occlusion.shape, 255, np.uint8)
Image.fromarray(np.dstack([np.zeros_like(full), np.uint8(np.clip(occlusion, 0, 1) * 255 + .5), np.zeros_like(full), full])).save(
    target / 'CaveRock_Mask.png')
print('Cave rock written to', target)
