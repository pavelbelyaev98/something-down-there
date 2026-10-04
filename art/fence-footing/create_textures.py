"""Cast concrete for the dig boundary's post footings (plain python with Pillow; no Blender or numpy needed).

BoundaryFooting: one strip wraps round a footing's sides (u, tiling) from its buried foot to its top (v, clamped,
0 at the bottom): grey concrete mottled by the pour, fine pores and a little exposed aggregate, the collar's soil
splashed up from the ground line and soaked into the buried part, and a few faint drip stains from the top.
BoundaryFootingTop: the same concrete, clean and tiling both ways, laid flat on the footings' tops.
Colour only, no light: the normal maps carry the pores and the aggregate.
"""
import math
import random
from pathlib import Path
from PIL import Image

ROOT = Path(r'C:/Users/pavel/Desktop/Dev/CompanyProjects/something-down-there')
OUT = ROOT / 'unity/Assets/Content/Lakebed/Boundary'
OUT.mkdir(parents=True, exist_ok=True)
CONCRETE, SOIL, DRIP = (176, 171, 162), (116, 84, 60), (122, 118, 110)


def smooth(t):
    return t * t * (3 - 2 * t)


def mix(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


class Sheet:
    """A w x h texture that always tiles in u and, if `wrap_v`, in v too."""

    def __init__(self, w, h, wrap_v):
        self.w, self.h, self.wrap_v = w, h, wrap_v

    def row(self, y):
        return y % self.h if self.wrap_v else min(max(y, 0), self.h - 1)

    def noise(self, cells_x, cells_y, seed):
        rng = random.Random(seed)
        grid = [[rng.random() for _ in range(cells_x)] for _ in range(cells_y + 1)]
        if self.wrap_v:
            grid[cells_y] = grid[0]
        rows = []
        for y in range(self.h):
            fy = y / self.h * cells_y
            y0 = int(fy); ty = smooth(fy - y0)
            r0, r1 = grid[y0], grid[min(y0 + 1, cells_y)]
            row = []
            for x in range(self.w):
                fx = x / self.w * cells_x
                x0 = int(fx); tx = smooth(fx - x0)
                a, b = r0[x0 % cells_x], r0[(x0 + 1) % cells_x]
                c, d = r1[x0 % cells_x], r1[(x0 + 1) % cells_x]
                row.append((a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty)
            rows.append(row)
        return rows

    def splat(self, field, cx, cy, radius, amount):
        """Adds a soft round dent or bump."""
        reach = int(radius * 2) + 1
        for dy in range(-reach, reach + 1):
            y = int(cy) + dy
            if not self.wrap_v and (y < 0 or y >= self.h):
                continue
            for dx in range(-reach, reach + 1):
                d = math.hypot(dx + int(cx) - cx, dy + int(cy) - cy)
                if d <= radius * 2:
                    field[y % self.h][(int(cx) + dx) % self.w] += amount * math.exp(-(d / radius) ** 2)

    def normal_map(self, height, strength):
        image = Image.new('RGB', (self.w, self.h))
        px = image.load()
        for y in range(self.h):
            for x in range(self.w):
                hx = height[y][(x + 1) % self.w] - height[y][(x - 1) % self.w]
                hy = height[self.row(y - 1)][x] - height[self.row(y + 1)][x]
                nx, ny, nz = -hx * strength, -hy * strength, 1.0
                length = math.sqrt(nx * nx + ny * ny + nz * nz)
                px[x, y] = tuple(int((c / length * .5 + .5) * 255) for c in (nx, ny, nz))
        return image


def concrete(sheet, seed, pores, aggregate):
    """The cast surface: pour mottling, cement grain, pores and exposed stones as (height, tone, stones)."""
    rng = random.Random(seed)
    pour, grain = sheet.noise(6, max(2, sheet.h * 6 // sheet.w), seed + 1), sheet.noise(48, max(8, sheet.h * 48 // sheet.w), seed + 2)
    height = [[(grain[y][x] - .5) * .25 for x in range(sheet.w)] for y in range(sheet.h)]
    tone = [[.2 * pour[y][x] - .1 for x in range(sheet.w)] for y in range(sheet.h)]
    for _ in range(pores):
        cx, cy, r = rng.uniform(0, sheet.w), rng.uniform(0, sheet.h), rng.uniform(.5, 1.3)
        sheet.splat(height, cx, cy, r, -.9)
        sheet.splat(tone, cx, cy, r, -.13)
    stones = []
    for _ in range(aggregate):
        cx, cy, r = rng.uniform(0, sheet.w), rng.uniform(0, sheet.h), rng.uniform(1.2, 3.2)
        sheet.splat(height, cx, cy, r, .7)
        stones.append((cx, cy, r, rng.choice([(150, 140, 128), (190, 186, 178), (132, 128, 122)])))
    return height, tone, stones, rng


def paint_stones(sheet, px, stones, soil_at):
    for cx, cy, r, stone in stones:
        for dy in range(-int(r) - 1, int(r) + 2):
            yy = int(cy) + dy
            if not sheet.wrap_v and (yy < 0 or yy >= sheet.h):
                continue
            yy %= sheet.h
            for dx in range(-int(r) - 1, int(r) + 2):
                if math.hypot(dx, dy) <= r:
                    xx = (int(cx) + dx) % sheet.w
                    px[xx, yy] = tuple(int(c) for c in mix(stone, SOIL, soil_at(yy) * .7))


def sides():
    sheet = Sheet(512, 256, False)
    height, tone, stones, rng = concrete(sheet, 41, 2000, 170)
    coarse, fine = sheet.noise(22, 10, 3), sheet.noise(64, 28, 4)
    # Drip stains from the top, faint and uneven.
    drips = [(rng.uniform(0, sheet.w), rng.uniform(3, 8), rng.uniform(.25, .6), rng.uniform(.06, .14)) for _ in range(7)]

    def soak(y, breakup=0.0):
        v = 1 - (y + .5) / sheet.h
        return 1 - smooth(min(1, max(0, (v - .3) / .32 + breakup)))

    albedo = Image.new('RGB', (sheet.w, sheet.h))
    px = albedo.load()
    for y in range(sheet.h):
        v = 1 - (y + .5) / sheet.h
        for x in range(sheet.w):
            colour = tuple(max(0, c * (1 + tone[y][x])) for c in CONCRETE)
            # Soil: soaked into the buried foot, a darker line at the ground (about v .35-.45), splashed above.
            line = math.exp(-((v - .4) / .05) ** 2) * .25
            splash = (.6 * coarse[y][x] + .4 * fine[y][x] - .5) * .35
            colour = mix(colour, SOIL, min(.92, soak(y, splash) * .8 + line))
            for cx, width, length, strength in drips:
                du = min(abs(x - cx), sheet.w - abs(x - cx))
                if du < width * 2 and v > 1 - length:
                    colour = mix(colour, DRIP, strength * (v - (1 - length)) / length * math.exp(-(du / width) ** 2))
            px[x, y] = tuple(int(max(0, min(255, c))) for c in colour)
    paint_stones(sheet, px, stones, soak)
    albedo.save(OUT / 'BoundaryFooting_Albedo.png')
    sheet.normal_map(height, 2.2).save(OUT / 'BoundaryFooting_Normal.png')


def top():
    sheet = Sheet(256, 256, True)
    height, tone, stones, _ = concrete(sheet, 57, 1000, 85)
    albedo = Image.new('RGB', (sheet.w, sheet.h))
    px = albedo.load()
    for y in range(sheet.h):
        for x in range(sheet.w):
            px[x, y] = tuple(int(max(0, min(255, c * (1 + tone[y][x])))) for c in CONCRETE)
    paint_stones(sheet, px, stones, lambda y: 0)
    albedo.save(OUT / 'BoundaryFootingTop_Albedo.png')
    sheet.normal_map(height, 2.2).save(OUT / 'BoundaryFootingTop_Normal.png')


if __name__ == '__main__':
    sides()
    top()
    print('wrote', OUT)
