"""Writes the project's 2K copies of the purchased Hand Mining Drill's 4K PBR maps (`source/`, untouched and kept outside Unity):
colour, OpenGL normal, and a URP Lit mask (metallic R, occlusion G, smoothness A = 1 - roughness). Run from the
repository root."""
from pathlib import Path
from PIL import Image

SOURCE = Path('art/hand-mining-drill/source')
TARGET = Path('unity/Assets/Content/ToolRig/MiningDrill')
SETS = {'Body': 'jackhammer Drill', 'Parts': 'Parts_2'}
SIZE = 2048


def grey(name):
    """A 16-bit greyscale map as 8 bits at SIZE."""
    im = Image.open(SOURCE / name)
    return im.point(lambda v: v / 257).convert('L').resize((SIZE, SIZE), Image.LANCZOS)


TARGET.mkdir(parents=True, exist_ok=True)
for ours, theirs in SETS.items():
    Image.open(SOURCE / f'{theirs}_Base_color.png').convert('RGB').resize((SIZE, SIZE), Image.LANCZOS).save(TARGET / f'{ours}_Albedo.png')
    Image.open(SOURCE / f'{theirs}_Normal_OpenGL.png').convert('RGB').resize((SIZE, SIZE), Image.LANCZOS).save(TARGET / f'{ours}_Normal.png')
    metal, ao, rough = grey(f'{theirs}_Metallic.png'), grey(f'{theirs}_Mixed_AO.png'), grey(f'{theirs}_Roughness.png')
    smooth = rough.point(lambda v: 255 - v)
    Image.merge('RGBA', (metal, ao, Image.new('L', (SIZE, SIZE), 0), smooth)).save(TARGET / f'{ours}_Mask.png')
    print(ours, 'done')
