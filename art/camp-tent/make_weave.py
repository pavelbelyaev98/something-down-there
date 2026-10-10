"""Tiling detail normal map of heavy cotton duck: a plain weave, 16 threads each way across the texture (about
12 mm), warp and weft passing over and under each other. Writes Tent_Weave_Normal.png (OpenGL/Unity, +Y up).
Run with plain Python (numpy, Pillow)."""
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parents[2] / 'unity/Assets/Content/Camp/Tent/Textures/Tent_Weave_Normal.png'
SIZE, THREADS = 256, 16
TILE_M = .012                     # metres the texture covers

u = (np.arange(SIZE) + .5) / SIZE * THREADS
U, V = np.meshgrid(u, u)
iu, iv = np.floor(U).astype(int), np.floor(V).astype(int)
fu, fv = U - iu, V - iv
over = ((iu + iv) % 2 == 0)       # where the warp crosses over the weft
warp = np.cos((fu - .5) * np.pi) ** .6 * np.sin(fv * np.pi) ** .3   # threads running along V
weft = np.cos((fv - .5) * np.pi) ** .6 * np.sin(fu * np.pi) ** .3   # threads running along U
height = np.where(over, warp * 1.0 + weft * .55, weft * 1.0 + warp * .55)
height *= TILE_M / THREADS * .35  # thread relief in metres
step = TILE_M / SIZE
dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) / (2 * step)
dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) / (2 * step)
n = np.stack([-dx, -dy, np.ones_like(dx)], axis=-1)
n /= np.linalg.norm(n, axis=-1, keepdims=True)
img = np.clip((n * .5 + .5) * 255 + .5, 0, 255).astype(np.uint8)
OUT.parent.mkdir(parents=True, exist_ok=True)
Image.fromarray(img[::-1], 'RGB').save(OUT)  # image rows run top-down; +Y is up the texture
print(OUT, 'tile_m', TILE_M)
