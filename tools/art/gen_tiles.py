"""Generate DRAFT 32x32 terrain tiles in the palette of reference image A.

Status of every output: Needs Cleanup (draft). Not production art (ASSET_GAPS.md).
Outputs (art-source/_drafts/tiles/ and Assets/_Game/Art/Tilesets/Village/Draft/):
  TILE_grass_draft.png        4 variants (128x32). Edges are plain single-pixel texture, so any
                              variant sits next to any other without a seam.
  TILE_path_draft_256.png     256 autotile variants (512x512), index = 8-neighbour mask
  TILE_water_draft_256.png    same layout
                              mask bits: N=1 NE=2 E=4 SE=8 S=16 SW=32 W=64 NW=128
  TILE_cliff_draft.png        cliff face  [mid, left_end, right_end, single] + waterfall (160x32)
  TILE_rim_draft.png          plateau rim, 8 variants, index = N*1 + E*2 + W*4 (256x32)

Autotile edges are computed from a 3x3-cell neighbourhood with noise that is periodic in 32 px,
so the pixels on both sides of a shared tile edge are computed from the same data: seamless by
construction (verified by tools/art/seam_test.py).

Usage: python3 tools/art/gen_tiles.py <repo_root>
"""
import os, sys
import numpy as np
from PIL import Image

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
T = 32
RNG = np.random.default_rng(20261121)


def hexc(h):
    return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)] + [255], np.uint8)


P = {k: hexc(v) for k, v in dict(
    g_base="#92ba53", g_dark="#86b151", g_edge="#6f9a48", g_light="#a6c955", g_hi="#bdd36a", g_deep="#4f7d4a",
    d_base="#efb97a", d_light="#f5c080", d_dark="#e4ae71", d_edge="#d29a62", d_deep="#b98257",
    w_base="#3799bd", w_deep="#2e6a97", w_light="#42a5c4", w_foam="#9dccbe", w_white="#e3f7fa", w_line="#2a5a7a",
    b_bank="#a5765a", b_dark="#6e4c4a",
    c_hi="#ecc17a", c_mid="#b58d67", c_mid2="#9d6c61", c_dark="#86605e", c_darker="#654855", c_deepest="#4a3445",
    f_0="#2674a9", f_1="#3997be", f_2="#49acd0", f_3="#7fd7ea", f_4="#e3f7fa",
).items()}
CLEAR = np.zeros(4, np.uint8)


def periodic_noise(size=T, octaves=((8, 1.0), (4, 0.5)), seed=0):
    """Smooth value noise, periodic in `size` on both axes, range about -1..1."""
    rng = np.random.default_rng(seed)
    out = np.zeros((size, size))
    for cell, amp in octaves:
        n = size // cell
        g = rng.uniform(-1, 1, (n, n))
        ys, xs = np.mgrid[0:size, 0:size] / cell
        x0, y0 = xs.astype(int), ys.astype(int)
        fx, fy = xs - x0, ys - y0
        fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
        a = g[y0 % n, x0 % n]; b = g[y0 % n, (x0 + 1) % n]
        c = g[(y0 + 1) % n, x0 % n]; d = g[(y0 + 1) % n, (x0 + 1) % n]
        out += amp * ((a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy)
    return out / sum(a for _, a in octaves)


# ------------------------------------------------------------------ base textures (single tile)
def grass_tile(variant):
    rng = np.random.default_rng(100 + variant)
    img = np.tile(P["g_base"], (T, T, 1))
    r = rng.random((T, T))
    img[r < 0.16] = P["g_dark"]
    img[(r > 0.84)] = P["g_light"]
    # tufts only in the interior so variants never break across tile edges
    for _ in range(5 + variant):
        x, y = rng.integers(3, T - 4), rng.integers(3, T - 4)
        img[y, x] = P["g_hi"]; img[y + 1, x - 1] = P["g_light"]; img[y + 1, x + 1] = P["g_light"]
        img[y + 2, x] = P["g_edge"]
    if variant == 3:  # a few darker clover pixels
        for _ in range(4):
            x, y = rng.integers(4, T - 5), rng.integers(4, T - 5)
            img[y:y + 2, x:x + 2] = P["g_dark"]; img[y + 1, x + 1] = P["g_edge"]
    return img


def dirt_fill(rng):
    img = np.tile(P["d_base"], (T, T, 1))
    r = rng.random((T, T))
    img[r < 0.10] = P["d_dark"]
    img[r > 0.90] = P["d_light"]
    img[(r > 0.5) & (r < 0.515)] = P["d_edge"]
    return img


def water_fill(rng):
    """Flat water with short horizontal ripples (interior only, so no pattern repeats per tile)."""
    img = np.tile(P["w_base"], (T, T, 1))
    r = rng.random((T, T))
    img[r < 0.05] = P["w_deep"]
    for _ in range(rng.integers(3, 6)):
        x, y, l = rng.integers(2, T - 8), rng.integers(2, T - 3), rng.integers(3, 7)
        img[y, x:x + l] = P["w_light"]
        if l > 4:
            img[y, x + 1:x + l - 1] = P["w_foam"]
        img[y + 1, x + 1:x + l - 1] = P["w_deep"]
    return img


# ------------------------------------------------------------------ 256-mask autotiles
DIRS = [("N", 1, 0, -1), ("NE", 2, 1, -1), ("E", 4, 1, 0), ("SE", 8, 1, 1),
        ("S", 16, 0, 1), ("SW", 32, -1, 1), ("W", 64, -1, 0), ("NW", 128, -1, -1)]
EDGE_NOISE_1 = periodic_noise(octaves=((8, 1.0), (4, 0.6), (2, 0.35)), seed=3)
EDGE_NOISE = np.tile(EDGE_NOISE_1, (3, 3))
R = 6      # blur radius: rounds corners and gives a distance proxy across the edge
OFF = 0.28  # pushes the whole visible edge inside the terrain cell (cells without the tile draw nothing)
AMP = 0.14  # edge jaggedness; at the cell border t <= -OFF + AMP = -0.14 < every band threshold


def neighbourhood(mask):
    m = np.zeros((3 * T, 3 * T))
    m[T:2 * T, T:2 * T] = 1
    for _, bit, dx, dy in DIRS:
        if mask & bit:
            m[(1 + dy) * T:(2 + dy) * T, (1 + dx) * T:(2 + dx) * T] = 1
    return m


def edge_field(mask):
    """t > 0 inside the terrain, t < 0 outside; |t| grows ~0.11 per pixel near the edge."""
    m = neighbourhood(mask)
    k = 2 * R + 1
    c = np.cumsum(np.cumsum(np.pad(m, R, mode="edge"), 0), 1)
    c = np.pad(c, ((1, 0), (1, 0)))
    s = (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)
    t = s - 0.5 - OFF - AMP * EDGE_NOISE
    return t[T:2 * T, T:2 * T]


def path_tile(mask, rng):
    t = edge_field(mask)
    img = dirt_fill(rng)
    out = np.zeros((T, T, 4), np.uint8)
    inside = t > 0.08
    out[inside] = img[inside]
    out[(t > 0) & (t <= 0.08)] = P["d_edge"]
    out[(t > -0.07) & (t <= 0)] = P["g_edge"]
    return out


def water_tile(mask, rng):
    t = edge_field(mask)
    img = water_fill(rng)
    out = np.zeros((T, T, 4), np.uint8)
    inside = t > 0.13
    out[inside] = img[inside]
    out[(t > 0.06) & (t <= 0.13)] = P["w_foam"]
    out[(t > 0.0) & (t <= 0.06)] = P["w_line"]
    out[(t > -0.07) & (t <= 0.0)] = P["b_bank"]
    out[(t > -0.13) & (t <= -0.07)] = P["b_dark"]
    return out


def sheet256(fn):
    sheet = np.zeros((16 * T, 16 * T, 4), np.uint8)
    for mask in range(256):
        rng = np.random.default_rng(1000 + mask)
        r, c = divmod(mask, 16)
        sheet[r * T:(r + 1) * T, c * T:(c + 1) * T] = fn(mask, rng)
    return sheet


# ------------------------------------------------------------------ cliff face, rim, waterfall
PILLARS = [(0, 7, 5), (7, 15, 3), (15, 23, 6), (23, 32, 4)]  # (x0, x1, top row) period 32
ROCK_RAMP = ["c_hi", "c_hi", "c_mid", "c_mid", "c_mid2", "c_dark", "c_dark", "c_darker"]


def pillar(img, x0, x1, top, bottom, rng):
    """One rounded rock column: light left, dark right, rounded top/bottom corners, a crack or two."""
    w = x1 - x0
    for x in range(x0, x1):
        u = (x - x0) / max(w - 1, 1)
        img[top:bottom, x] = P[ROCK_RAMP[min(int(u * len(ROCK_RAMP)), len(ROCK_RAMP) - 1)]]
    img[top:bottom, x1 - 1] = P["c_deepest"]                      # gap to the next column
    img[top, x0:x1 - 1] = P["c_hi"]; img[top, x0] = CLEAR if top > 4 else img[top, x0]
    img[top, x1 - 2] = P["c_dark"]
    img[bottom - 1, x0:x1 - 1] = P["c_darker"]
    for _ in range(rng.integers(1, 3)):                           # horizontal cracks
        y = rng.integers(top + 4, bottom - 3)
        img[y, x0 + 1:x1 - 2] = P["c_darker"]
        img[y + 1, x0 + 1:x0 + 3] = P["c_hi"]


def cliff_face(left_end, right_end):
    rng = np.random.default_rng(55)
    img = np.zeros((T, T, 4), np.uint8)
    for x0, x1, top in PILLARS:
        pillar(img, x0, x1, top, T - 2, rng)
        img[4:top, x0:x1] = P["c_darker"]                         # shadow under the lip
    # grass lip from the plateau above: jagged lower edge, dark outline
    lip = [3, 4, 4, 3, 5, 4, 3, 3, 4, 5, 4, 3, 4, 4, 3, 5, 4, 3, 4, 4, 5, 3, 3, 4, 4, 5, 4, 3, 4, 3, 4, 4]
    for x in range(T):
        img[0:lip[x], x] = P["g_base"]
        img[lip[x], x] = P["g_deep"]
    img[1, 2::5] = P["g_light"]; img[0, 4::7] = P["g_hi"]
    img[T - 2, :] = P["c_deepest"]                                # base shadow
    img[T - 1, :] = P["g_deep"]                                   # contact with the ground below
    if left_end:
        img[:, 0:3] = CLEAR
        img[lip[3]:T - 1, 3] = P["c_deepest"]
        img[0:lip[3], 3] = P["g_edge"]
    if right_end:
        img[:, T - 3:] = CLEAR
        img[lip[T - 4]:T - 1, T - 4] = P["c_deepest"]
        img[0:lip[T - 4], T - 4] = P["g_edge"]
    return img


def waterfall():
    img = cliff_face(False, False)
    n = periodic_noise(seed=11)
    for x in range(4, T - 4):
        for y in range(2, T):
            v = n[y % T, x] + (0.5 if x % 5 == 0 else 0)
            img[y, x] = P["f_4"] if v > 0.8 else P["f_3"] if v > 0.35 else P["f_2"] if v > -0.1 else P["f_1"] if v > -0.5 else P["f_0"]
    img[T - 5:, 4:T - 4][::2, ::2] = P["f_4"]  # splash
    img[2:4, 3] = P["f_0"]; img[2:4, T - 4] = P["f_0"]
    return img


def rim(n_open, e_open, w_open):
    """Plateau edge drawn inside the plateau cell (top surface stays transparent: grass shows).
    Side edges show the cliff's side as a 10 px rock wall, as in reference A."""
    rng = np.random.default_rng(77)
    img = np.zeros((T, T, 4), np.uint8)
    if n_open:
        img[0:2, :] = P["c_darker"]; img[2, :] = P["g_edge"]
    for side, open_ in (("E", e_open), ("W", w_open)):
        if not open_:
            continue
        x0 = T - 10 if side == "E" else 0
        for y in range(T):
            for i in range(10):
                k = i if side == "E" else 9 - i  # k=0 at the grass side
                ramp = ["g_edge", "c_hi", "c_hi", "c_mid", "c_mid", "c_mid2", "c_mid2", "c_dark", "c_darker", "c_deepest"]
                img[y, x0 + i] = P[ramp[k]]
        for y in (5, 13, 22, 29):  # stacked-stone joints, period 32 so cells stack seamlessly
            xs = range(x0 + 1, x0 + 9) if side == "E" else range(x0 + 1, x0 + 9)
            for x in xs:
                img[y, x] = P["c_darker"]
            img[y + 1, (x0 + 2) if side == "E" else (x0 + 6)] = P["c_hi"]
    return img


def strip(tiles):
    return np.concatenate(tiles, axis=1)


def main():
    out_src = os.path.join(ROOT, "art-source/_drafts/tiles")
    out_unity = os.path.join(ROOT, "Assets/_Game/Art/Tilesets/Village/Draft")
    os.makedirs(out_src, exist_ok=True); os.makedirs(out_unity, exist_ok=True)
    files = {
        "TILE_grass_draft.png": strip([grass_tile(v) for v in range(4)]),
        "TILE_path_draft_256.png": sheet256(path_tile),
        "TILE_water_draft_256.png": sheet256(water_tile),
        "TILE_cliff_draft.png": strip([cliff_face(False, False), cliff_face(True, False),
                                       cliff_face(False, True), cliff_face(True, True), waterfall()]),
        "TILE_rim_draft.png": strip([rim(bool(i & 1), bool(i & 2), bool(i & 4)) for i in range(8)]),
    }
    for name, arr in files.items():
        for d in (out_src, out_unity):
            Image.fromarray(arr).save(os.path.join(d, name))
        print(name, arr.shape[1], "x", arr.shape[0])


if __name__ == "__main__":
    main()
