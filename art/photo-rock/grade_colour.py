"""Regrades the plain rock's colour maps (variants/*/textures/Rock_BaseColor.png) to a warm grey-brown in the minerals'
family (user, 2026-10-08: the rock "doesn't really match the vibe of other minerals ... a bit too greenish"; its shape
stays: "I had fine shape before but just colors were weird"). Only the covered texels change (the atlas padding stays
black): their mean goes to `target`, their brightness spread to `spread`, and each texel keeps its own hue relative to
the mean, so it is idempotent (a second run changes nothing). Run from the repository root (needs Pillow and numpy),
then Sync Discovery Models."""
from pathlib import Path

import numpy as np
from PIL import Image

variants = Path('art/photo-rock/variants')
# The rock's mean colour (sRGB 0-255) and the spread of its texels' brightness about it (standard deviation of
# brightness over mean): a little more character than the bake's flat 0.06.
target, spread = (110, 97, 82), .09
luminance = np.array([.2126, .7152, .0722])


def to_linear(c):
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= .0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - .055)


for path in sorted(variants.glob('*/textures/Rock_BaseColor.png')):
    srgb = np.asarray(Image.open(path).convert('RGB'), dtype=np.float64) / 255
    covered = srgb.sum(-1) > 15 / 255
    linear = to_linear(srgb[covered])
    lum = np.maximum(linear @ luminance, 1e-5)
    ratio = lum / lum.mean()
    ratio = 1 + (ratio - 1) * spread / max(ratio.std(), 1e-6)
    hue = linear / lum[:, None]
    hue /= hue.mean(0)
    graded = to_linear(np.array(target, dtype=np.float64) / 255) * np.maximum(ratio, 0)[:, None] * hue
    out = srgb.copy()
    out[covered] = to_srgb(graded)
    Image.fromarray(np.uint8(np.clip(out, 0, 1) * 255 + .5)).save(path)
    print(path, 'mean', np.round(to_srgb(graded.mean(0)) * 255, 1))
