"""Writes the project's URP metallic mask for the purchased old chest: metallic (R) where its grey specular map marks
iron and brass, no occlusion (G) and its smoothness (A, the specular map's alpha). Buried props draw through the
excavation daylight shader, whose specular setup is not kept in builds, so the chest uses the metallic workflow on its
own colour map. Run from the repository root after reimporting the pack."""
from pathlib import Path

from PIL import Image

source = Path('unity/Assets/NOT_Lonely/OldChest/ModelAndTexture/Chest_S_R.tga')
target = Path('unity/Assets/Content/BuriedProps/OldChest')
# Wood and paint keep a specular grey under about 0.2; the bands, studs and lock sit around 0.4-0.6.
metal_from, metal_to = .22, .52

target.mkdir(parents=True, exist_ok=True)
specular = Image.open(source).convert('RGBA')
grey, smoothness = specular.getchannel('R'), specular.getchannel('A')
metallic = grey.point(lambda v: int(max(0, min(1, (v / 255 - metal_from) / (metal_to - metal_from))) * 255 + .5))
zero, full = Image.new('L', grey.size, 0), Image.new('L', grey.size, 255)
Image.merge('RGBA', (metallic, full, zero, smoothness)).save(target / 'OldChest_Mask.png')
