"""Writes the crystal crack mask (115, user 2026-10-06: a crystal being broken should crack, not shrink):
unity/Assets/Content/Caverns/CrystalCracks.png, 1024 px, tiling. White lines of branching fractures; each line's alpha
is when it appears, early cracks near 1 and late ones near the floor, nothing elsewhere. The crack overlay clips at a
threshold that falls as the crystal takes work (CavernCrystal), so the fractures run out from a few starts and branch
until the crystal breaks.
Run from the repository root (needs Pillow and numpy)."""
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

size = 1024
target = Path('unity/Assets/Content/Caverns/CrystalCracks.png')
rng = np.random.default_rng(29)
# Starts, steps (px), branching, wander and kinks: a few long, straight fractures that bend sharply now and then and
# fork, each fork appearing a little later.
starts, step, steps, branch, turn, kink = 6, 14, 48, .09, .05, .1
first, last = .98, .06

order = Image.new('F', (size * 3, size * 3), 0)
draw = ImageDraw.Draw(order)


def crack(x, y, angle, birth, length, width):
    for i in range(length):
        nx, ny = x + math.cos(angle) * step, y + math.sin(angle) * step
        when = birth + (i / steps) * .5
        value = first - (first - last) * min(1, when)
        draw.line([(x, y), (nx, ny)], fill=value, width=max(1, int(round(width))))
        x, y = nx, ny
        angle += rng.normal(0, turn) + (rng.choice([-1, 1]) * rng.uniform(.3, .7) if rng.random() < kink else 0)
        width *= .995
        if rng.random() < branch and width > 1.2:
            crack(x, y, angle + rng.choice([-1, 1]) * rng.uniform(.5, 1.1), when + .05, int(length * rng.uniform(.3, .6)), width * .7)


for _ in range(starts):
    cx, cy = rng.uniform(size, 2 * size, 2)
    a = rng.uniform(0, 2 * math.pi)
    crack(cx, cy, a, rng.uniform(0, .25), steps, rng.uniform(2.5, 3.5))
    crack(cx, cy, a + math.pi + rng.normal(0, .3), rng.uniform(0, .25), int(steps * .7), rng.uniform(2, 3))

# Fold the 3 x 3 canvas onto one tile so the mask wraps.
big = np.asarray(order, dtype=np.float32)
tile = np.zeros((size, size), np.float32)
for i in range(3):
    for j in range(3):
        tile = np.maximum(tile, big[i * size:(i + 1) * size, j * size:(j + 1) * size])
alpha = Image.fromarray(np.uint8(np.clip(tile, 0, 1) * 255 + .5)).filter(ImageFilter.MaxFilter(3))
rgba = Image.merge('RGBA', [Image.new('L', (size, size), 255)] * 3 + [alpha])
target.parent.mkdir(parents=True, exist_ok=True)
rgba.save(target)
print('wrote', target, f'(cracks on {np.mean(np.asarray(alpha) > 0):.1%} of the tile)')
