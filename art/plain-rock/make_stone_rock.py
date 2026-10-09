"""Writes the plain rock's maps (user, 2026-10-08: four new shapes from the asset packs, "not the poop like form", keeping
the rock's colours and texture): the Mountains pack's Stone1b-4b shapes wear the old rock's own colour map, cut as a
seamless tile, with the stones' own relief and occlusion.
- StoneRock_Albedo.png: the largest square fully inside one island of rock_colour.png (the retired Blender rock's colour
  map, already graded warm grey-brown by its grade_colour.py), blended with its half-offset copy where its edges meet so
  it tiles (art/mining-pack/seamless.py), repeated `repeats` times each way: the stones map their whole surface once onto
  the texture, and this keeps the old rock's speckles at their own size on a stone of about 40 cm.
- StoneRock_Normal.png, StoneRock_Mask.png: the stones' own normal map and occlusion (the pack mask's G), at find size.
1024 px each (a find a metre away needs no more), into Content/BuriedProps/Stones. Run from the repository root (needs
Pillow and numpy) after reimporting the Mountains pack."""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.append(str(Path(__file__).resolve().parent.parent / 'mining-pack'))
from seamless import tileable, weight  # noqa: E402

colour_source = Path('art/plain-rock/rock_colour.png')
stones = Path('unity/Assets/BK/PureNature_Mountains/Models/Rocks/Textures')
target = Path('unity/Assets/Content/BuriedProps/Stones')
size, fade, repeats = 1024, .18, 3


def largest_square(covered):
    """(row, column, side) of the largest square of covered texels (dynamic programming over the mask)."""
    h, w = covered.shape
    best = (0, 0, 0)
    run = np.zeros(w + 1, dtype=np.int32)
    for i in range(h):
        row = np.zeros(w + 1, dtype=np.int32)
        for j in range(w):
            if covered[i, j]:
                row[j + 1] = 1 + min(run[j], run[j + 1], row[j])
                if row[j + 1] > best[2]:
                    best = (i - row[j + 1] + 1, j - row[j + 1] + 1, row[j + 1])
        run = row
    return best


rock = Image.open(colour_source).convert('RGB')
small = rock.reduce(4)
covered = np.asarray(small, dtype=np.float64).sum(-1) > 15 * 3
i, j, side = largest_square(covered)
# Back to full size, a few texels inside the island's edge.
i, j, side = i * 4 + 6, j * 4 + 6, side * 4 - 12
tile = -(-size // repeats)
crop = rock.crop((j, i, j + side, i + side)).resize((tile, tile), Image.LANCZOS)
colour = tileable(np.asarray(crop, dtype=np.float64) / 255, weight(tile, fade))
colour = np.tile(colour, (repeats, repeats, 1))
target.mkdir(parents=True, exist_ok=True)
Image.fromarray(np.uint8(np.clip(colour, 0, 1) * 255 + .5)).resize((size, size), Image.LANCZOS).save(target / 'StoneRock_Albedo.png')
Image.open(stones / 'Stones_n.png').convert('RGB').resize((size, size), Image.LANCZOS).save(target / 'StoneRock_Normal.png')
occlusion = np.asarray(Image.open(stones / 'Stones_mask.png').convert('RGBA').resize((size, size), Image.LANCZOS))[..., 1]
full = np.full(occlusion.shape, 255, np.uint8)
Image.fromarray(np.dstack([np.zeros_like(full), occlusion, np.zeros_like(full), full])).save(target / 'StoneRock_Mask.png')
print(f'square {side}px at ({j}, {i}); maps written to {target}')
