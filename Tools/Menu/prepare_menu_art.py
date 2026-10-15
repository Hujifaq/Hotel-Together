#!/usr/bin/env python3
"""Prepares the clay parallax art set for the main menu.

usage: python prepare_menu_art.py <unzipped art folder> <output folder>
- background.png is copied as-is (opaque).
- Characters: the source alpha is ~253 inside the body (slightly see-through) and has faint
  1-15 alpha speckles around the silhouette. Alpha is remapped so the body is fully opaque and
  the speckles vanish, the image is trimmed to its silhouette (+ padding, aspect preserved),
  and edge colours are bled into transparent pixels so bilinear filtering never shows a dark halo.
"""
import sys, os
import numpy as np
from PIL import Image
from scipy import ndimage

src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
Image.open(os.path.join(src, "background.png")).convert("RGB").save(os.path.join(out, "background.png"), optimize=True)

for name in ["housekeeper", "bellhop", "living-guest", "zombie-guest"]:
    im = np.array(Image.open(os.path.join(src, name + ".png")).convert("RGBA")).astype(np.float32)
    a = im[..., 3]
    a = np.clip((a - 14) / (244 - 14), 0, 1)
    # drop tiny disconnected specks
    lab, n = ndimage.label(a > .05)
    if n > 1:
        sizes = ndimage.sum(np.ones_like(a), lab, range(1, n + 1))
        keep = np.isin(lab, 1 + np.where(sizes >= 400)[0])
        a = np.where(keep | (ndimage.binary_dilation(keep, iterations=2) & (a > 0)), a, 0)
    ys, xs = np.where(a > 0)
    pad = 12
    y0, y1, x0, x1 = max(0, ys.min() - pad), min(a.shape[0], ys.max() + 1 + pad), max(0, xs.min() - pad), min(a.shape[1], xs.max() + 1 + pad)
    rgb = im[y0:y1, x0:x1, :3]; a = a[y0:y1, x0:x1]
    # colour bleed: fill transparent pixels with the nearest opaque colour
    solid = a > .5
    idx = ndimage.distance_transform_edt(~solid, return_distances=False, return_indices=True)
    bled = rgb[idx[0], idx[1]]
    rgb = np.where((a > .5)[..., None], rgb, bled)
    outimg = np.dstack([rgb, a * 255]).clip(0, 255).astype(np.uint8)
    Image.fromarray(outimg, "RGBA").save(os.path.join(out, name + ".png"), optimize=True)
    print(name, outimg.shape[1], "x", outimg.shape[0], "aspect", round(outimg.shape[1] / outimg.shape[0], 3))
