"""Writes the project's URP masks for the purchased TV Set pack: metallic 0 (R), no occlusion (G) and smoothness (A)
from the pack's Shining map the way its Shader Forge shader read it (gloss = Shining.r * GlossHigh + GlossLow, per
material). Run from the repository root after reimporting the pack."""
from pathlib import Path

from PIL import Image

source = Path('unity/Assets/_Television_set/Sources/Materials')
target = Path('unity/Assets/Content/BuriedProps/TVSet')
# The vendor materials' GlossHigh and GlossLow.
sets = {'Tv_1_2': (.7169596, .1666343), 'Tv_3_4': (.8419596, .33928573)}

target.mkdir(parents=True, exist_ok=True)
for name, (high, low) in sets.items():
    shining = Image.open(source / f'{name}_gloss.png').convert('RGBA').getchannel('R')
    gloss = shining.point(lambda v: min(255, int((v / 255 * high + low) * 255 + .5)))
    zero, full = Image.new('L', gloss.size, 0), Image.new('L', gloss.size, 255)
    Image.merge('RGBA', (zero, full, zero, gloss)).save(target / f'{name}_Mask.png')
