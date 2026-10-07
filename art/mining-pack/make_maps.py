"""Writes project maps from the Mining Tools, Ore & Ingots pack (user, 2026-10-06):
- Copper ore with more copper on it ("copper is fine to be rock and copper in one, but I need more copper on it"):
  the pack's copper flecks grown and joined by more patches to about half the stone, in the pack's own copper colour,
  with its metal and sheen in the mask (Ore_Copper_Rich.png, Ore_Copper_Rich_Mask.png).
- The rock sets coal and the native metal nuggets are built on, at 1024 px (Rocks/<set>_BC/_N/_Mask.png): the pack's
  4K TGAs are 160 MB a set, and a find a metre away needs no more. Coal takes the layered rocks, iron and silver the
  jagged ones, gold the rounded ones. A nugget is solid metal ("gold, silver and others can be full gold/silver"): the jagged sets also get
  a grey map of the rock's light and dark only, around white (Rocks/<set>_Metal.png), which the metal's colour tints.
Run from the repository root (needs Pillow and numpy) after reimporting the pack."""
import colorsys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

textures = Path('unity/Assets/REAL_DEDICATED/MiningTools_Ore_Ingots/Textures')
target = Path('unity/Assets/Content/BuriedProps/MiningPack')
size = 1024
rocks = ['Layered_Large', 'Layered_Small', 'Jagged_Large', 'Jagged_Small']
# Copper: what counts as a copper fleck (hue in degrees, saturation), how far the flecks grow (px at 1024), the share of
# the stone the added patches take, and the copper's metal and smoothness in the mask (the pack's own, measured).
copper_hue, copper_saturation = 45, .3
grow, patches = 9, .3
copper_metal, copper_gloss = .73, .58
# Nugget maps: the rock's luminance over its mean, kept within metal_range, so the tint sets the colour.
metal_rocks, metal_range = ['Jagged_Large', 'Jagged_Small'], (.6, 1.1)


def load(path, mode):
    return Image.open(path).convert(mode).resize((size, size), Image.LANCZOS)


def blotches(seed):
    """Soft, tileable blotches: random noise blurred on a wrapped 3 x 3 repeat, normalised to 0..1."""
    rng = np.random.default_rng(seed)
    small = Image.fromarray(np.uint8(rng.random((size // 16, size // 16)) * 255)).resize((size, size), Image.BICUBIC)
    big = Image.new('L', (size * 3, size * 3))
    for i in range(3):
        for j in range(3):
            big.paste(small, (i * size, j * size))
    big = big.filter(ImageFilter.GaussianBlur(10)).crop((size, size, 2 * size, 2 * size))
    a = np.asarray(big, dtype=np.float64)
    return (a - a.min()) / max(1e-6, a.max() - a.min())


ore = np.asarray(load(textures / 'T_Ore_Copper_BC.tga', 'RGB'), dtype=np.float64) / 255
mask = np.asarray(load(textures / 'T_Ore_Copper_Mask.tga', 'RGBA'), dtype=np.float64) / 255
hsv = np.vectorize(colorsys.rgb_to_hsv)(ore[..., 0], ore[..., 1], ore[..., 2])
fleck = (hsv[1] > copper_saturation) & (hsv[0] * 360 < copper_hue)
copper_colour = ore[fleck].mean(axis=0)
grown = Image.fromarray(np.uint8(fleck * 255)).filter(ImageFilter.MaxFilter(grow)).filter(ImageFilter.GaussianBlur(3))
share = np.maximum(np.asarray(grown, dtype=np.float64) / 255, np.clip((blotches(5) - (1 - patches)) / .1, 0, 1))
share = np.where(fleck, 1, share)
luminance = ore @ np.array([.2126, .7152, .0722])
detail = np.clip(luminance / max(luminance.mean(), 1e-6), .7, 1.3)[..., None]
copper = np.where(fleck[..., None], ore, np.clip(copper_colour * detail, 0, 1))
colour = ore * (1 - share[..., None]) + copper * share[..., None]
rich_mask = mask.copy()
rich_mask[..., 0] = np.maximum(mask[..., 0], share * copper_metal)
rich_mask[..., 3] = np.maximum(mask[..., 3], share * copper_gloss)
target.mkdir(parents=True, exist_ok=True)
Image.fromarray(np.uint8(np.clip(colour, 0, 1) * 255 + .5)).save(target / 'Ore_Copper_Rich.png')
Image.fromarray(np.uint8(np.clip(rich_mask, 0, 1) * 255 + .5), 'RGBA').save(target / 'Ore_Copper_Rich_Mask.png')
print('wrote', target / 'Ore_Copper_Rich*.png', f'(copper on {share.mean():.0%} of the map, {fleck.mean():.0%} before)')

(target / 'Rocks').mkdir(exist_ok=True)
for rock_set in rocks:
    for channel, mode in (('BC', 'RGB'), ('N', 'RGB'), ('Mask', 'RGBA')):
        load(textures / f'Rocks_{rock_set.split("_")[0]}' / f'T_Rock_{rock_set}_{channel}.tga', mode).save(target / 'Rocks' / f'{rock_set}_{channel}.png')
    if rock_set in metal_rocks:
        rock = np.asarray(Image.open(target / 'Rocks' / f'{rock_set}_BC.png').convert('RGB'), dtype=np.float64) / 255
        shade = rock @ np.array([.2126, .7152, .0722])
        shade = np.clip(shade / max(shade.mean(), 1e-6), *metal_range) / metal_range[1]
        Image.fromarray(np.uint8(np.repeat(shade[..., None], 3, axis=2) * 255 + .5)).save(target / 'Rocks' / f'{rock_set}_Metal.png')
    print('wrote', target / 'Rocks' / f'{rock_set}_*.png')
