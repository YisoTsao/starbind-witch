"""Seam test for the 256-mask draft autotiles (ASSET_PIPELINE.md §6).

Builds a random 24x16 map, assembles it from the tile sheets, and compares the opaque/transparent
pattern with ONE global render of the same map (no tile boundaries). Any mismatch away from the
outer map border is a seam. Also writes a 4x preview PNG for eyeballing.
Usage: python3 tools/art/seam_test.py <repo_root> [preview.png]
"""
import os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(__file__))
import gen_tiles as g

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None
T, W, H = g.T, 24, 16


def mask_at(m, x, y):
    v = 0
    for _, bit, dx, dy in g.DIRS:
        xx, yy = x + dx, y + dy
        if 0 <= xx < W and 0 <= yy < H and m[yy, xx]:
            v |= bit
    return v


def run(name, sheet_file, alpha_threshold):
    rng = np.random.default_rng(5)
    m = (rng.random((H, W)) < 0.45).astype(np.uint8)
    m[4:10, 6:14] = 1; m[6, 9] = 0  # a solid block with a hole
    sheet = np.asarray(Image.open(os.path.join(ROOT, "Assets/_Game/Art/Tilesets/Village/Draft", sheet_file)))
    asm = np.zeros((H * T, W * T, 4), np.uint8)
    for y in range(H):
        for x in range(W):
            if m[y, x]:
                k = mask_at(m, x, y); r, c = divmod(k, 16)
                asm[y * T:(y + 1) * T, x * T:(x + 1) * T] = sheet[r * T:(r + 1) * T, c * T:(c + 1) * T]
    big = np.kron(m, np.ones((T, T)))
    k = 2 * g.R + 1
    c = np.pad(np.cumsum(np.cumsum(np.pad(big, g.R, mode="constant"), 0), 1), ((1, 0), (1, 0)))
    s = (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)
    t = s - 0.5 - g.OFF - g.AMP * np.tile(g.EDGE_NOISE_1, (H, W))
    want = (t > alpha_threshold) & (big > 0)  # only cells that carry the tile can draw
    got = asm[:, :, 3] > 0
    inner = np.zeros_like(want); inner[T:-T, T:-T] = True
    bad = (want != got) & inner
    print("%-6s mismatched pixels (excluding outer border tiles): %d of %d" % (name, bad.sum(), inner.sum()))
    return asm, bad.sum()


grass = np.asarray(Image.open(os.path.join(ROOT, "Assets/_Game/Art/Tilesets/Village/Draft/TILE_grass_draft.png")))
ok = True
canvas = np.zeros((H * T, W * T, 4), np.uint8)
rng = np.random.default_rng(9)
for y in range(H):
    for x in range(W):
        v = rng.integers(0, 4); canvas[y * T:(y + 1) * T, x * T:(x + 1) * T] = grass[:, v * T:(v + 1) * T]
for name, f, thr in (("path", "TILE_path_draft_256.png", -0.07), ("water", "TILE_water_draft_256.png", -0.13)):
    asm, bad = run(name, f, thr)
    ok &= bad == 0
    if name == "path":
        a = asm[:, :, 3:4] > 0
        canvas = np.where(a, asm, canvas)
if PREVIEW:
    Image.fromarray(canvas).resize((W * T * 2, H * T * 2), Image.NEAREST).save(PREVIEW)
print("SEAM_TEST", "PASS" if ok else "FAIL")
sys.exit(0 if ok else 1)
