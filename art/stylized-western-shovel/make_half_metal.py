"""Writes the project's copy of the purchased shovel's metallic-smoothness map with half its metallic (RGB), smoothness
(alpha) unchanged. Run from the repository root after reimporting the pack."""
from PIL import Image

source = 'unity/Assets/StylizedShovel/Textures/T_Shovel_MS.png'
target = 'unity/Assets/Content/ToolRig/WesternShovel/T_Shovel_MS_Half.png'
r, g, b, a = Image.open(source).convert('RGBA').split()
half = [c.point(lambda v: int(v * .5 + .5)) for c in (r, g, b)]
Image.merge('RGBA', (*half, a)).save(target)
