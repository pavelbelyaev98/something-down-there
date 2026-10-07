"""Writes the cave rock's texture set (116) from Crystal Caverns' own cave wall: CaveWall_0's purple-grey flaky stone with
its warm flecks, the rock the demo's crystals grow from (user, 2026-10-07: "use the textures from the demo where the rocks
were"). Unlike the cave surfaces' maps, CaveWall_0's colour and normal are continuous (no atlas gaps); its own occlusion
map has gaps, so occlusion comes from the colour's shading as the geode shell's does. The stone is low in contrast, so its
shading is stretched about its mean, and each map is blended with its half-offset copy where its edges meet so it tiles
exactly. Colour, normal and occlusion (G), each 2048 px, into Content/GroundTextures as CaveRock_*.
Run from the repository root (needs Pillow and numpy) after reimporting the pack."""
from pathlib import Path

import numpy as np
from PIL import Image

source = Path('unity/Assets/BK/PureNature_CrystalCaverns/Models/CaveWall/Textures')
target = Path('unity/Assets/Content/GroundTextures')
size = 2048
# How much the stone's shading is stretched about its mean, and how much darker the whole is (a cave wall, lit by its
# crystals and lamps rather than the sun).
contrast, level = 1.7, .9
# The cross fade's half width as a share of the tile.
fade = .18


def load(path, mode):
    return np.asarray(Image.open(path).convert(mode).resize((size, size), Image.LANCZOS), dtype=np.float64)


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
colour = tileable(load(source / 'CaveWall_0_a.png', 'RGB') / 255, w)
mean = colour.mean(axis=(0, 1))
colour = np.clip((mean + (colour - mean) * contrast) * level, 0, 1)
shade = colour.mean(axis=-1)
low, high = np.percentile(shade, [2, 98])
t = np.clip((shade - low) / max(1e-6, high - low), 0, 1)
normal = tileable(load(source / 'CaveWall_0_n.png', 'RGB') / 255 * 2 - 1, w)
normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
# The stone's dark cracks and the shadowed sides of its flakes sit a little in shadow.
occlusion = .72 + .28 * t
target.mkdir(parents=True, exist_ok=True)
Image.fromarray(np.uint8(colour * 255 + .5)).save(target / 'CaveRock_Albedo.png')
Image.fromarray(np.uint8(np.clip((normal + 1) / 2, 0, 1) * 255 + .5)).save(target / 'CaveRock_Normal.png')
full = np.full(occlusion.shape, 255, np.uint8)
Image.fromarray(np.dstack([np.zeros_like(full), np.uint8(np.clip(occlusion, 0, 1) * 255 + .5), np.zeros_like(full), full])).save(
    target / 'CaveRock_Mask.png')
print('Cave rock written to', target)
