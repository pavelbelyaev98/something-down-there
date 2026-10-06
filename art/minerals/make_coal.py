"""Writes coal's texture set from the approved photo rock (art/photo-rock, look A): the rock's own colour detail turned
coal-black with a faint cold cast, its normal map as it is, and its mask with no metal, the rock's occlusion and a
little more sheen than the rock (coal's dull gloss), plus copies of the rock's model and collider, so it reads in the
same style as the rock the user likes (user, 2026-10-06: the old Blender coal was too low-poly).
Run from the repository root (needs Pillow and numpy)."""
import shutil
from pathlib import Path

import numpy as np
from PIL import Image

rock = Path('art/photo-rock/variants/a/textures')
rock_models = Path('art/photo-rock/variants/a')
target = Path('art/minerals/coal')
size = 1024
# Black with the rock's own detail kept: the rock's luminance, stretched over its own 2-98 % range, into
# [floor, floor + spread] (sRGB), a hint of blue.
floor, spread, cast = .03, .19, np.array([.95, .97, 1.06])
# Masks: R metallic, G occlusion, B roughness (the import turns it into smoothness). Coal's sheen: roughness near
# coal_roughness, following a little of the rock's own variation.
coal_roughness, roughness_follow = 150, .3

base = np.asarray(Image.open(rock / 'Rock_BaseColor.png').convert('RGB').resize((size, size), Image.LANCZOS), dtype=np.float64) / 255
luminance = base @ np.array([.2126, .7152, .0722])
low, high = np.percentile(luminance, [2, 98])
luminance = np.clip((luminance - low) / max(1e-6, high - low), 0, 1)
coal = np.clip((floor + spread * luminance)[..., None] * cast, 0, 1)
masks = np.asarray(Image.open(rock / 'Rock_Masks.png').convert('RGB').resize((size, size), Image.LANCZOS), dtype=np.float64)
roughness = np.clip(coal_roughness + (masks[..., 2] - masks[..., 2].mean()) * roughness_follow, 0, 255)

target.mkdir(parents=True, exist_ok=True)
Image.fromarray(np.uint8(coal * 255 + .5)).save(target / 'BaseColor.png')
Image.open(rock / 'Rock_Normal.png').convert('RGB').resize((size, size), Image.LANCZOS).save(target / 'Normal.png')
Image.fromarray(np.dstack([np.zeros_like(roughness), masks[..., 1], roughness]).astype(np.uint8)).save(target / 'Masks.png')
# The rock's own model and collider, copied: the discovery import reads only the minerals' source folder.
shutil.copyfile(rock_models / 'models/ordinary_weathered_rock.fbx', target / 'Coal.fbx')
shutil.copyfile(rock_models / 'collisions/ordinary_weathered_rock.fbx', target / 'Coal_collision.fbx')
print('Coal texture set and model written to', target)
