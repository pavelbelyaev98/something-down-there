"""Writes project maps from the Mining Tools, Ore & Ingots pack (user, 2026-10-06):
- Copper ore with more copper on it ("copper is fine to be rock and copper in one, but I need more copper on it"):
  the pack's copper flecks grown and joined by more patches to about half the stone, in the pack's own copper colour,
  with its metal and sheen in the mask (Ore_Copper_Rich.png, Ore_Copper_Rich_Mask.png).
- The rock sets coal and the minerals are built on, at 1024 px (Rocks/<set>_BC/_N/_Mask.png): the pack's 4K TGAs are
  160 MB a set, and a find a metre away needs no more. Coal takes the layered rocks; silver, gold and iron the jagged
  ones. Silver and gold are solid metal ("gold, silver and others can be full gold/silver": a grey map of the rock's
  light and dark only, around white, Rocks/<set>_Metal.png, which the metal's colour tints).
- Iron ore as in the user's reference (2026-10-08: grey stone with rust red in its cracks, "you made the brown the
  dominant colour, I didn't want that"): the rock's light and dark in grey, rust in its strongest creases (darker than
  their surroundings in its occlusion and colour, the same share of every rock), matte; the mask holds the stone's and
  the rust's metal and sheen and the rock's occlusion (Rocks/<set>_Iron.png, Rocks/<set>_Iron_Mask.png).
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
# Iron ore: its rocks, the stone's grey and how far the rock's light and dark spread it, the rust's colour, the share of the
# stone it takes and how softly it fades, and the stone's and the rust's metal and smoothness.
iron_rocks = ['Jagged_Large', 'Jagged_Small']
iron_stone, iron_range = np.array([.37, .36, .35]), (.6, 1.3)
rust_colour, rust_share, rust_soft = np.array([.46, .28, .22]), .12, 3.5
stone_metal, stone_gloss, rust_metal, rust_gloss = .06, .38, 0, .22


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


def blurred(values, radius):
    """A 0..1 map softened by a Gaussian of `radius` px."""
    return np.asarray(Image.fromarray(np.uint8(np.clip(values, 0, 1) * 255)).filter(ImageFilter.GaussianBlur(radius)), dtype=np.float64) / 255


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
    if rock_set in iron_rocks:
        rock = np.asarray(Image.open(target / 'Rocks' / f'{rock_set}_BC.png').convert('RGB'), dtype=np.float64) / 255
        rock_mask = np.asarray(Image.open(target / 'Rocks' / f'{rock_set}_Mask.png').convert('RGBA'), dtype=np.float64) / 255
        luminance = rock @ np.array([.2126, .7152, .0722])
        shade = np.clip(luminance / max(luminance.mean(), 1e-6), *iron_range)[..., None]
        occlusion = rock_mask[..., 1]
        # Broad creases, not the colour's fine speckle: measured against wide surroundings, then softened.
        crease = np.clip((blurred(occlusion, 14) - occlusion) / .08, 0, 1) * .7 + np.clip((blurred(luminance, 12) - luminance) / .14, 0, 1) * .5
        crease = blurred(np.clip(crease, 0, 1), 4) * (.5 + .5 * blotches(11))
        edge = np.percentile(crease, 100 - rust_share * 100)
        rust = blurred(np.clip((crease - edge * .7) / max(edge * .6, 1e-6), 0, 1), rust_soft)[..., None]
        colour = iron_stone * shade * (1 - rust) + rust_colour * np.clip(shade, .7, 1.05) * rust
        iron_mask = np.zeros((size, size, 4))
        iron_mask[..., 0] = stone_metal + (rust_metal - stone_metal) * rust[..., 0]
        iron_mask[..., 1] = occlusion
        iron_mask[..., 3] = stone_gloss + (rust_gloss - stone_gloss) * rust[..., 0]
        Image.fromarray(np.uint8(np.clip(colour, 0, 1) * 255 + .5)).save(target / 'Rocks' / f'{rock_set}_Iron.png')
        Image.fromarray(np.uint8(np.clip(iron_mask, 0, 1) * 255 + .5), 'RGBA').save(target / 'Rocks' / f'{rock_set}_Iron_Mask.png')
        print(f'  rust on {(rust > .5).mean():.0%} of {rock_set}')
    print('wrote', target / 'Rocks' / f'{rock_set}_*.png')
