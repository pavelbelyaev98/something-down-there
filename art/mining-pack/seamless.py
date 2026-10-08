"""Shared by the Mining pack's ground scripts (make_cave_rock.py, make_riverbed.py): a square from inside one island of a
4K rock atlas (clear of the islands' stretched padding), resized and blended with its half-offset copy where its edges
meet so it tiles exactly."""
import numpy as np
from PIL import Image

atlases = 'unity/Assets/REAL_DEDICATED/MiningTools_Ore_Ingots/Textures'


def load(path, mode, crop, size):
    """The crop (x, y, side in source px) of an image, resized to size, as float pixels."""
    x, y, side = crop
    return np.asarray(Image.open(path).convert(mode).crop((x, y, x + side, y + side)).resize((size, size), Image.LANCZOS),
                      dtype=np.float64)


def weight(size, fade):
    """1 in the middle of the tile, falling to 0 within `fade` (a share of the tile) of its edges, so the half-offset copy
    covers them."""
    t = np.linspace(0, 1, size, endpoint=False) + .5 / size
    edge = np.clip(np.minimum(t, 1 - t) / fade, 0, 1)
    edge = edge * edge * (3 - 2 * edge)
    return np.minimum.outer(edge, edge)


def tileable(image, w):
    """The image over its half-offset copy by w, rescaled about the mean by 1 / sqrt(w^2 + (1 - w)^2) so the cross keeps
    the image's contrast (variance-preserving blending)."""
    size = image.shape[0]
    shifted = np.roll(np.roll(image, size // 2, axis=0), size // 2, axis=1)
    w3 = w[..., None] if image.ndim == 3 else w
    blend = image * w3 + shifted * (1 - w3)
    mean = image.mean(axis=(0, 1))
    scale = 1 / np.sqrt(w * w + (1 - w) ** 2)
    return mean + (blend - mean) * (scale[..., None] if image.ndim == 3 else scale)


def save(target, name, colour, normal, occlusion):
    """<name>_Albedo/_Normal/_Mask.png (occlusion in the mask's G) from float colour (0-1), unit normals and occlusion."""
    target.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.uint8(np.clip(colour, 0, 1) * 255 + .5)).save(target / f'{name}_Albedo.png')
    Image.fromarray(np.uint8(np.clip((normal + 1) / 2, 0, 1) * 255 + .5)).save(target / f'{name}_Normal.png')
    full = np.full(occlusion.shape, 255, np.uint8)
    Image.fromarray(np.dstack([np.zeros_like(full), np.uint8(np.clip(occlusion, 0, 1) * 255 + .5), np.zeros_like(full), full])).save(
        target / f'{name}_Mask.png')
