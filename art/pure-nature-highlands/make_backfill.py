"""Writes the backfill ground's texture set from the approved packs' own ground textures (106): the site's soil
(Mountains Mud01, carrying the dig ground's clay-loam tint) turned over with Highlands Mud_rubble's lumpy mud, a
little darker, and a scatter of that texture's stones churned in. One tileable set (colour, normal, occlusion in G)
covering 2 x 2 soil tiles and one rubble tile; the ground shader draws backfill from it alone.
Run from the repository root (needs Pillow and numpy) after reimporting either pack."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

mountains = Path('unity/Assets/BK/PureNature_Mountains/Textures/Surfaces')
highlands = Path('unity/Assets/BK/PureNature_Highlands/Textures/Surfaces')
target = Path('unity/Assets/Content/GroundTextures')
size = 2048
# The dig ground's soil tint (LakebedSiteSetup.ClayLoamTint, linear): baked into the dirt, never the stones.
soil_tint = np.array([.62, .98, 1.25])
# Stirred-up fill reads a little darker and damper than settled soil; the rubble's mud gives it lumps, and its
# stones keep most of their own colour, so the churned-in rubble shows.
dirt_shade, rubble_mud_share, stone_dirt = .84, .35, .12


def load(path, mode):
    return Image.open(path).convert(mode)


def soil_size(image):
    """2 x 2 repeats of a 2048 soil texture, box-reduced to the set's size (stays tileable)."""
    w, h = image.size
    tiled = Image.new(image.mode, (w * 2, h * 2))
    for i in range(2):
        for j in range(2):
            tiled.paste(image, (i * w, j * h))
    return tiled.reduce(tiled.width // size)


def rubble_size(image):
    return image.reduce(image.width // size)


def wrapped(mask, *filters):
    """Filters a tileable mask on a 3 x 3 repeat and keeps the middle, so nothing seams at the edges."""
    w, h = mask.size
    big = Image.new(mask.mode, (w * 3, h * 3))
    for i in range(3):
        for j in range(3):
            big.paste(mask, (i * w, j * h))
    for f in filters:
        big = big.filter(f)
    return big.crop((w, h, 2 * w, 2 * h))


def to_linear(c):
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= .0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - .055)


def normals(image):
    n = np.asarray(image, dtype=np.float64)[..., :3] / 255 * 2 - 1
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


soil = np.asarray(soil_size(load(mountains / 'Mud01_a.png', 'RGB')), dtype=np.float64) / 255
soil_n = normals(soil_size(load(mountains / 'Mud01_n.png', 'RGB')))
soil_ao = np.asarray(soil_size(load(mountains / 'Mud01_m.png', 'RGBA')), dtype=np.float64)[..., 1] / 255
rubble_image = rubble_size(load(highlands / 'Mud_rubble_a.png', 'RGB'))
rubble = np.asarray(rubble_image, dtype=np.float64) / 255
rubble_n = normals(rubble_size(load(highlands / 'Mud_rubble_n.png', 'RGB')))
rubble_ao = np.asarray(rubble_size(load(highlands / 'Mud_rubble_mask.png', 'RGBA')), dtype=np.float64)[..., 1] / 255

# The rubble's stones are its pale parts: every stone (to keep them out of the mud) and the larger ones (churned in).
grey = rubble_image.convert('L')
all_stones = wrapped(grey.point(lambda v: 255 if v > 140 else 0), ImageFilter.MaxFilter(3), ImageFilter.GaussianBlur(1))
churned = wrapped(grey.point(lambda v: 255 if v > 145 else 0), ImageFilter.MinFilter(3), ImageFilter.GaussianBlur(1))
all_stones = np.asarray(all_stones, dtype=np.float64)[..., None] / 255
churned = np.asarray(churned, dtype=np.float64)[..., None] / 255

# Dirt: the soil in the dig ground's tint, turned over with the rubble's mud (its stones left out), a little darker.
soil_tinted = to_srgb(to_linear(soil) * soil_tint)
rubble_mud = rubble * (1 - all_stones) + soil_tinted * all_stones
dirt = (soil_tinted * (1 - rubble_mud_share) + rubble_mud * rubble_mud_share) * dirt_shade
stones = rubble * (1 - stone_dirt) + dirt * stone_dirt
colour = dirt * (1 - churned) + stones * churned

# Whiteout blend: both reliefs add up instead of averaging each other flat.
dirt_n = np.dstack([soil_n[..., 0] + rubble_n[..., 0], soil_n[..., 1] + rubble_n[..., 1], soil_n[..., 2] * rubble_n[..., 2]])
dirt_n /= np.linalg.norm(dirt_n, axis=-1, keepdims=True)
normal = dirt_n * (1 - churned) + rubble_n * churned
normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
occlusion = (soil_ao * (1 - rubble_mud_share) + rubble_ao * rubble_mud_share) * (1 - churned[..., 0]) + rubble_ao * churned[..., 0]

target.mkdir(parents=True, exist_ok=True)
Image.fromarray(np.uint8(np.clip(colour, 0, 1) * 255 + .5)).save(target / 'Backfill_Albedo.png')
Image.fromarray(np.uint8(np.clip((normal + 1) / 2, 0, 1) * 255 + .5)).save(target / 'Backfill_Normal.png')
full = np.full(occlusion.shape, 255, np.uint8)
Image.fromarray(np.dstack([np.zeros_like(full), np.uint8(np.clip(occlusion, 0, 1) * 255 + .5), np.zeros_like(full), full])).save(target / 'Backfill_Mask.png')
print('Backfill texture set written to', target)
