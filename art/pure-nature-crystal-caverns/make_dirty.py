"""Writes dusty copies of the Crystal Caverns crystal colour maps for crystals found in the ground (user, 2026-10-06:
"make them dirtier"): the crystal in its own colour, with a pale film of the site's dust settled in its crevices and in
soft patches over it, so a crystal dug out of the earth reads as dusty, unlike the clean ones in the old chests.
The crystal's colour is baked in (its material draws the map in white): tinted afterwards, the dust took the crystal's
dark colour and read as black spots (user, 2026-10-06). Colours match BuriedPropsSetup.CrystalTints.
Run from the repository root (needs Pillow and numpy) after reimporting either pack."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

crystals = Path('unity/Assets/BK/PureNature_CrystalCaverns/Models/Crystals/Textures')
soil_path = Path('unity/Assets/BK/PureNature_Mountains/Textures/Surfaces/Mud01_a.png')
target = Path('unity/Assets/Content/BuriedProps/CrystalCaverns')
# Each family's crystal colour (sRGB, as BuriedPropsSetup.CrystalTints).
families = {'Beryl': (.08, .38, .18), 'Ruby': (.42, .03, .06), 'Quartz': (.45, .5, .56), 'Pyrite': (.72, .6, .3)}
size = 1024
# The dig ground's soil tint (LakebedSiteSetup.ClayLoamTint, linear), as the backfill uses.
soil_tint = np.array([.62, .98, 1.25])
# Dust is the soil dried and powdered: paler and greyer (dust_pale of the way to dust_colour).
dust_colour, dust_pale = np.array([.66, .62, .56]), .6
# Share of the surface under dust patches, how opaque the dust is there and in the crevices, and the crevices' shade.
coverage, film, crevice_film, crevice_shade = .45, .6, .7, .85


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
dust = soil * (1 - dust_pale) + dust_colour * dust_pale
for n, (family, tint) in enumerate(families.items()):
    rgba = Image.open(crystals / f'{family}_a.png').convert('RGBA').resize((size, size), Image.LANCZOS)
    detail = np.asarray(rgba, dtype=np.float64)[..., :3] / 255
    alpha = np.asarray(rgba)[..., 3]
    crystal = detail * np.array(tint)
    # Crevices: the crystal map's own dark parts.
    shade = detail @ np.array([.2126, .7152, .0722])
    crevice = np.clip((shade.mean() - shade) * 3, 0, 1)
    mask = np.clip((blotches(17 + n) - (1 - coverage)) / .2, 0, 1) * film
    mask = np.maximum(mask, crevice * crevice_film)
    crystal = crystal * (1 - crevice[..., None] * (1 - crevice_shade))
    dusty = crystal * (1 - mask[..., None]) + dust * mask[..., None]
    out = np.dstack([np.uint8(np.clip(dusty, 0, 1) * 255 + .5), alpha])
    target.mkdir(parents=True, exist_ok=True)
    Image.fromarray(out, 'RGBA').save(target / f'{family}_Dirty.png')
    print('wrote', target / f'{family}_Dirty.png')
