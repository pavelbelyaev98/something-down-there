"""Rewrites the workshop's baked PNGs as 8 bits a channel (Blender saves its float bakes at 16), cutting their size several times;
Unity block-compresses them either way. Run with plain Python (Pillow) after create_assets.py."""
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2] / 'unity/Assets/Content/Camp'
for path in sorted(ROOT.glob('*/Textures/*.png')):
    with path.open('rb') as f:
        depth = f.read(25)[24]  # IHDR bit depth
    if depth != 16:
        continue
    with Image.open(path) as image:
        image.load()
        converted = image.convert(image.mode)
    before = path.stat().st_size
    converted.save(path, optimize=True)
    print(f'{path.name}: {before / 1e6:.1f} MB -> {path.stat().st_size / 1e6:.1f} MB')
