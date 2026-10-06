"""Writes the geode shell's texture set (110) from Crystal Caverns' own tiling rock detail: _RockDetail1's porous stone
graded to a dark grey, with its own normal map. It reads apart from the warm soil, the grey-brown backfill and the
site's walls. (User, 2026-10-06: "geode shell A is beautiful". B, the same stone pale and cool with a fifth of
_RockDetail2's banding, lost the A/B; _RockDetail2 alone, with relief from its lines, read as white marble; the cave
surfaces' maps are UV atlases whose island gaps showed as black holes in the wall.) The detail is a near-white overlay,
so it is stretched over its own 2-98 % range into the palette, and blended with its half-offset copy where its edges
meet so it tiles exactly. Colour, normal and occlusion (G), each 2048 px, into Content/GroundTextures as GeodeShell_*.
Run from the repository root (needs Pillow and numpy) after reimporting the pack."""
from pathlib import Path

import numpy as np
from PIL import Image

surfaces = Path('unity/Assets/BK/PureNature_CrystalCaverns/Textures/Surfaces')
target = Path('unity/Assets/Content/GroundTextures')
size = 2048
# The dark and light ends of the palette (sRGB).
dark, light = np.array([.22, .21, .23]), np.array([.48, .46, .48])
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
shade = tileable(load(surfaces / '_RockDetail1_a.png', 'L'), w)
low, high = np.percentile(shade, [2, 98])
t = np.clip((shade - low) / max(1e-6, high - low), 0, 1)
colour = dark + (light - dark) * t[..., None]
normal = tileable(load(surfaces / '_RockDetail1_n.png', 'RGB') / 255 * 2 - 1, w)
normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
# The detail's dark pits and seams sit a little in shadow.
occlusion = .75 + .25 * t
target.mkdir(parents=True, exist_ok=True)
Image.fromarray(np.uint8(np.clip(colour, 0, 1) * 255 + .5)).save(target / 'GeodeShell_Albedo.png')
Image.fromarray(np.uint8(np.clip((normal + 1) / 2, 0, 1) * 255 + .5)).save(target / 'GeodeShell_Normal.png')
full = np.full(occlusion.shape, 255, np.uint8)
Image.fromarray(np.dstack([np.zeros_like(full), np.uint8(np.clip(occlusion, 0, 1) * 255 + .5), np.zeros_like(full), full])).save(
    target / 'GeodeShell_Mask.png')
print('Geode shell written to', target)
