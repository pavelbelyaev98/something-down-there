"""Tiling detail normal map of corrugated iron: a sine profile, 76 mm pitch, 19 mm deep, four ribs across the texture
(0.304 m), uniform along the ribs. Writes Workshop_Ribs_Normal.png (OpenGL/Unity convention, +Y up).
Run with plain Python (numpy, Pillow)."""
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parents[2] / 'unity/Assets/Content/Camp/Workshop/Textures/Workshop_Ribs_Normal.png'
SIZE, RIBS, PITCH, DEPTH = 256, 4, .076, .019

x = (np.arange(SIZE) + .5) / SIZE * RIBS * PITCH          # metres across
slope = -(DEPTH / 2) * (2 * np.pi / PITCH) * np.sin(2 * np.pi * x / PITCH)
n = np.stack([-slope, np.zeros_like(slope), np.ones_like(slope)], axis=-1)
n /= np.linalg.norm(n, axis=-1, keepdims=True)
row = np.clip((n * .5 + .5) * 255 + .5, 0, 255).astype(np.uint8)
image = np.repeat(row[None, :, :], SIZE, axis=0)
OUT.parent.mkdir(parents=True, exist_ok=True)
Image.fromarray(image, 'RGB').save(OUT)
print(OUT)
