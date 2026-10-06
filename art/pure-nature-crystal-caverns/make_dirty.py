"""Writes dirty copies of the Crystal Caverns crystal colour maps for crystals found in the ground (user, 2026-10-06:
"make them dirtier"): the site's soil (Mountains Mud01 in the dig ground's tint) caked over the crystal in soft blotches
and darkened into its crevices, so a crystal dug out of the earth reads as earthy, unlike the clean ones in the old
chests. The material's own crystal tint still colours what shows through.
Run from the repository root (needs Pillow and numpy) after reimporting either pack."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

crystals = Path('unity/Assets/BK/PureNature_CrystalCaverns/Models/Crystals/Textures')
soil_path = Path('unity/Assets/BK/PureNature_Mountains/Textures/Surfaces/Mud01_a.png')
target = Path('unity/Assets/Content/BuriedProps/CrystalCaverns')
families = ['Beryl', 'Ruby', 'Quartz', 'Pyrite']
size = 1024
# The dig ground's soil tint (LakebedSiteSetup.ClayLoamTint, linear), as the backfill uses.
soil_tint = np.array([.62, .98, 1.25])
# Share of the surface under soil, how solid the soil is where it lies, and the darkening in the crevices.
coverage, caked, crevice_dark = .45, .8, .55


def to_linear(c):
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= .0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - .055)


def blotches(seed):
    """Soft, tileable blotches: random noise blurred on a wrapped 3 x 3 repeat, normalised to 0..1."""
    rng = np.random.default_rng(seed)
    small = Image.fromarray(np.uint8(rng.random((size // 8, size // 8)) * 255)).resize((size, size), Image.BICUBIC)
    big = Image.new('L', (size * 3, size * 3))
    for i in range(3):
        for j in range(3):
            big.paste(small, (i * size, j * size))
    big = big.filter(ImageFilter.GaussianBlur(18)).crop((size, size, 2 * size, 2 * size))
    a = np.asarray(big, dtype=np.float64)
    return (a - a.min()) / max(1e-6, a.max() - a.min())


soil = np.asarray(Image.open(soil_path).convert('RGB').resize((size, size), Image.LANCZOS), dtype=np.float64) / 255
soil = to_srgb(to_linear(soil) * soil_tint)
for n, family in enumerate(families):
    rgba = Image.open(crystals / f'{family}_a.png').convert('RGBA').resize((size, size), Image.LANCZOS)
    crystal = np.asarray(rgba, dtype=np.float64)[..., :3] / 255
    alpha = np.asarray(rgba)[..., 3]
    # Crevices: the crystal map's own dark parts.
    shade = crystal @ np.array([.2126, .7152, .0722])
    crevice = np.clip((shade.mean() - shade) * 3, 0, 1)
    mask = np.clip((blotches(17 + n) - (1 - coverage)) / .15, 0, 1) * caked
    mask = np.maximum(mask, crevice * .5)
    dirty = crystal * (1 - crevice[..., None] * (1 - crevice_dark))
    dirty = dirty * (1 - mask[..., None]) + soil * mask[..., None]
    out = np.dstack([np.uint8(np.clip(dirty, 0, 1) * 255 + .5), alpha])
    target.mkdir(parents=True, exist_ok=True)
    Image.fromarray(out, 'RGBA').save(target / f'{family}_Dirty.png')
    print('wrote', target / f'{family}_Dirty.png')
