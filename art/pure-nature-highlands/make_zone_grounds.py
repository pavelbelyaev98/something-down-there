"""Writes the zone grounds' texture sets (111) from the approved packs' own ground textures: zone 2's old lake sediment
and zone 3's old riverbed, three looks each for the user to pick from in play (Developer admin > Zone grounds).

Lake sediment (grey silt and clay):
  LakeSediment      A grey silt: Highlands Mud, graded to an olive grey-brown, its own relief and occlusion.
  LakeSedimentClay  B blue-grey clay: the same mud graded blue-grey (colour only; relief from Crystal Caverns
                    GroundDirt, occlusion from LakeSediment).
  LakeSedimentMud   C puddled mud: Mountains Mud02 with its moss turned to mud, its smooth relief and occlusion.
Riverbed (sand and gravel):
  Riverbed          A sand and gravel: Highlands Sand_rubble, the sand darkened to damp ochre, its grey stones kept.
  RiverbedGravel    B gravel: Mountains Gravel warmed to a sandy grey-brown (never the geode shell's dark grey).
  RiverbedMixed     C gravel in sand: Highlands Sand in the same damp ochre with Gravel's pebbles churned in.

Each set is <name>_Albedo/_Normal/_Mask.png (occlusion in the mask's G) at 2048 px; colour carries no light direction:
grading keeps each texture's own brightness detail around a new mean. The ground shader (GroundTriplanar) draws the
chosen look; GroundTextureSetup wires them. Run from the repository root (needs Pillow and numpy) after reimporting a
pack; delete a look's files when the user picks another."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

highlands = Path('unity/Assets/BK/PureNature_Highlands/Textures/Surfaces')
mountains = Path('unity/Assets/BK/PureNature_Mountains/Textures/Surfaces')
caverns = Path('unity/Assets/BK/PureNature_CrystalCaverns/Textures/Surfaces')
target = Path('unity/Assets/Content/GroundTextures')
size = 2048
luminance = np.array([.2126, .7152, .0722])

# Mean colours (sRGB 0-255) each look is graded to. Silt sits apart from the warm clay-loam soil above it and from the
# geode shells' purple-grey stone inside it; the riverbed's damp sand from the dark brown cave rock and grey shells.
silt_mean = (92, 86, 72)
clay_mean = (80, 85, 87)
mud_mean = (92, 82, 67)
sand_mean = (106, 89, 64)
stone_mean = (98, 94, 88)
gravel_mean = (103, 92, 75)
# How much of a texture's own colour variation survives the grade (0: brightness detail only).
keep_hue = .35


def load(path, mode):
    image = Image.open(path).convert(mode)
    return image.reduce(image.width // size) if image.width > size else image


def to_linear(c):
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= .0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - .055)


def colour(path):
    return to_linear(np.asarray(load(path, 'RGB'), dtype=np.float64) / 255)


def normals(path):
    n = np.asarray(load(path, 'RGB'), dtype=np.float64) / 255 * 2 - 1
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def occlusion(path):
    return np.asarray(load(path, 'RGBA'), dtype=np.float64)[..., 1] / 255


def grade(linear, mean_srgb, weight=None, keep=keep_hue):
    """The texture's brightness detail (and keep_hue of its colour variation) around a new mean colour. weight: which
    pixels set the reference mean (default all)."""
    lum = linear @ luminance
    w = np.ones(lum.shape) if weight is None else weight
    ref = (lum * w).sum() / w.sum()
    hue = linear / np.maximum(lum, 1e-4)[..., None]
    ref_hue = (hue * w[..., None]).reshape(-1, 3).sum(0) / w.sum()
    mean = to_linear(np.array(mean_srgb, dtype=np.float64) / 255)
    graded = mean * (lum / ref)[..., None]
    return graded * ((hue / ref_hue) ** keep)


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


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def save(name, albedo=None, normal=None, occ=None):
    target.mkdir(parents=True, exist_ok=True)
    if albedo is not None:
        Image.fromarray(np.uint8(to_srgb(albedo) * 255 + .5)).save(target / f'{name}_Albedo.png')
    if normal is not None:
        normal = normal / np.linalg.norm(normal, axis=-1, keepdims=True)
        Image.fromarray(np.uint8(np.clip((normal + 1) / 2, 0, 1) * 255 + .5)).save(target / f'{name}_Normal.png')
    if occ is not None:
        full = np.full(occ.shape, 255, np.uint8)
        g = np.uint8(np.clip(occ, 0, 1) * 255 + .5)
        Image.fromarray(np.dstack([np.zeros_like(full), g, np.zeros_like(full), full])).save(target / f'{name}_Mask.png')


# Lake sediment.
mud = colour(highlands / 'Mud_a.png')
save('LakeSediment', grade(mud, silt_mean), normals(highlands / 'Mud_n.png'), occlusion(highlands / 'Mud_mask.png'))
save('LakeSedimentClay', grade(mud, clay_mean), normals(caverns / 'GroundDirt_n.png'))
# Mud02's moss patches (green well above red and blue, widened over their fringes) take the mud's own colour at the
# mud's brightness, so the patches survive only as faint damp shading in the relief.
mud02 = colour(mountains / 'Mud02_a.png')
srgb = to_srgb(mud02)
moss = smoothstep(.0, .05, srgb[..., 1] - np.maximum(srgb[..., 0], srgb[..., 2]))
moss = np.asarray(wrapped(Image.fromarray(np.uint8(moss * 255)), ImageFilter.MaxFilter(5), ImageFilter.GaussianBlur(2)),
                  dtype=np.float64) / 255
lum = mud02 @ luminance
mud_ref = (lum * (1 - moss)).sum() / (1 - moss).sum()
moss_ref = (lum * moss).sum() / max(moss.sum(), 1)
mud_hue = (mud02 * (1 - moss[..., None])).reshape(-1, 3).sum(0) / ((1 - moss).sum() * mud_ref)
levelled = mud02 * (1 - moss[..., None]) + (lum * mud_ref / moss_ref)[..., None] * mud_hue * moss[..., None]
save('LakeSedimentMud', grade(levelled, mud_mean, 1 - moss, .1), normals(mountains / 'Mud02_n.png'), occlusion(mountains / 'Mud02_m.png'))

# Riverbed. Sand_rubble's sand is pale, its stones darker (bimodal brightness): the sand goes to damp ochre, the
# stones to washed grey river pebbles.
rubble = colour(highlands / 'Sand_rubble_a.png')
sand_share = smoothstep(.6, .66, to_srgb(rubble) @ luminance)
sand_share = np.asarray(wrapped(Image.fromarray(np.uint8(sand_share * 255)), ImageFilter.GaussianBlur(1)), dtype=np.float64) / 255
sand = grade(rubble, sand_mean, sand_share)
stones = grade(rubble, stone_mean, 1 - sand_share)
save('Riverbed', sand * sand_share[..., None] + stones * (1 - sand_share[..., None]),
     normals(highlands / 'Sand_rubble_n.png'), occlusion(highlands / 'Sand_rubble_mask.png'))

gravel = colour(mountains / 'Gravel_a.png')
gravel_n = normals(mountains / 'Gravel_n.png')
gravel_ao = occlusion(mountains / 'Gravel_m.png')
gravel_graded = grade(gravel, gravel_mean)
save('RiverbedGravel', gravel_graded, gravel_n, gravel_ao)

# Gravel's brightest pebble tops, filled and softened, churned into the sand.
pale = Image.fromarray(np.uint8(np.clip(to_srgb(gravel) @ luminance, 0, 1) * 255))
level = np.percentile(np.asarray(pale), 78)
pebbles = wrapped(pale.point(lambda v: 255 if v > level else 0), ImageFilter.MaxFilter(3), ImageFilter.MinFilter(3),
                  ImageFilter.GaussianBlur(1))
pebbles = np.asarray(pebbles, dtype=np.float64)[..., None] / 255
plain_sand = grade(colour(highlands / 'Sand_a.png'), sand_mean)
sand_n = normals(highlands / 'Sand_n.png')
save('RiverbedMixed', plain_sand * (1 - pebbles) + grade(gravel, stone_mean) * .75 * pebbles, sand_n * (1 - pebbles) + gravel_n * pebbles,
     occlusion(highlands / 'Sand_m.png') * (1 - pebbles[..., 0]) + gravel_ao * pebbles[..., 0])
print('Zone ground texture sets written to', target)
