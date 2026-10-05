"""Writes the project's URP mask for the purchased Big old TV: metallic 0 (R) and smoothness (A), the inverse of the
roughness map its Autodesk Interactive material read. Occlusion stays the vendor's AO map. Run from the repository root
after reimporting the pack."""
from pathlib import Path

from PIL import Image, ImageOps

source = Path('unity/Assets/JustPlay/Old TV/Textures/Roughness.psd')
target = Path('unity/Assets/Content/BuriedProps/BigOldTV')

target.mkdir(parents=True, exist_ok=True)
smoothness = ImageOps.invert(Image.open(source).convert('L'))
zero, full = Image.new('L', smoothness.size, 0), Image.new('L', smoothness.size, 255)
Image.merge('RGBA', (zero, full, zero, smoothness)).save(target / 'BigOldTV_Mask.png')
