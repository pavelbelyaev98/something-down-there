"""Survey tape textures for the dig plot boundary (plain python with Pillow; no Blender or numpy needed).

Banded: the classic red/white barrier tape in 25 cm blocks, one tile = 0.5 m x the tape's 7 cm width, with
plastic-film creases across it, a little grime and a faint edge line; the normal map is the creases.
"""
import math
import random
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(r'C:/Users/pavel/Desktop/Dev/CompanyProjects/something-down-there')
OUT = ROOT / 'unity/Assets/Content/Lakebed/Boundary'
OUT.mkdir(parents=True, exist_ok=True)
PX_PER_M = 2048
RED, WHITE, DUST = (200, 22, 16), (238, 236, 228), (150, 125, 95)


def smooth(t):
    return t * t * (3 - 2 * t)


def noise(w, h, cells_x, cells_y, seed):
    """Tileable value noise as a list of rows."""
    rng = random.Random(seed)
    grid = [[rng.random() for _ in range(cells_x)] for _ in range(cells_y)]
    rows = []
    for y in range(h):
        fy = y / h * cells_y
        y0 = int(fy); ty = smooth(fy - y0)
        r0, r1 = grid[y0 % cells_y], grid[(y0 + 1) % cells_y]
        row = []
        for x in range(w):
            fx = x / w * cells_x
            x0 = int(fx); tx = smooth(fx - x0)
            a, b = r0[x0 % cells_x], r0[(x0 + 1) % cells_x]
            c, d = r1[x0 % cells_x], r1[(x0 + 1) % cells_x]
            row.append((a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty)
        rows.append(row)
    return rows


def creases(w, h, length_m, seed):
    """Thin creases across the tape where the film has been folded, as a height map (0..1)."""
    rng = random.Random(seed)
    height = [[0.0] * w for _ in range(h)]
    for _ in range(int(length_m * 26)):
        x = rng.uniform(0, w)
        lean = rng.uniform(-.25, .25) * h
        depth = rng.uniform(.3, 1)
        width = rng.uniform(1.2, 3.5)
        reach = int(width * 3) + 2
        for y in range(h):
            centre = x + lean * (y / h - .5)
            for dx in range(-reach, reach + 1):
                px = int(centre) + dx
                d = px - centre
                height[y][px % w] -= depth * math.exp(-(d / width) ** 2)
    soft = noise(w, h, max(1, int(length_m * 12)), 2, seed + 1)
    lo = min(min(r) for r in height)
    hi = max(max(r) for r in height)
    return [[(height[y][x] - lo) / max(hi - lo, 1e-6) * .75 + .25 * soft[y][x] for x in range(w)] for y in range(h)]


def normal_map(height, strength):
    h, w = len(height), len(height[0])
    image = Image.new('RGB', (w, h))
    data = []
    for y in range(h):
        for x in range(w):
            gx = (height[y][(x + 1) % w] - height[y][x - 1]) * .5
            gy = (height[min(y + 1, h - 1)][x] - height[max(y - 1, 0)][x]) * .5
            nx, ny, nz = -gx * strength, gy * strength, 1.0
            length = math.sqrt(nx * nx + ny * ny + nz * nz)
            data.append(tuple(int((v / length * .5 + .5) * 255) for v in (nx, ny, nz)))
    image.putdata(data)
    return image


def finish(base, height, seed):
    w, h = base.size
    grime = noise(w, h, 18, 3, seed)
    pixels = base.load()
    for y in range(h):
        edge = abs(y - h * .07) < h * .012 or abs(y - h * .93) < h * .012
        for x in range(w):
            r, g, b = pixels[x, y]
            shade = .9 + .1 * height[y][x]
            dirt = min(1, max(0, (grime[y][x] - .55) / .35)) * .22
            k = shade * (1 - dirt) * (.88 if edge else 1)
            pixels[x, y] = tuple(int(min(255, c * k + d * dirt)) for c, d in zip((r, g, b), DUST))
    return base


def blocks(w, h, length_m):
    image = Image.new('RGB', (w, h), WHITE)
    draw = ImageDraw.Draw(image)
    block = int(.25 / length_m * w)
    for x in range(0, w, block * 2):
        draw.rectangle((x, 0, x + block - 1, h), fill=RED)
    return image


def banded():
    length, width = .5, .07
    w, h = int(length * PX_PER_M), int(width * PX_PER_M)
    height = creases(w, h, length, 3)
    finish(blocks(w, h, length), height, 4).save(OUT / 'SurveyTapeBanded_Albedo.png')
    normal_map(height, 3.5).save(OUT / 'SurveyTapeBanded_Normal.png')


banded()
print('ok', sorted(p.name for p in OUT.glob('SurveyTape*.png')))
