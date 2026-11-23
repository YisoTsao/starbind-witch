"""Generate DRAFT 32x32 terrain tiles in the palette of reference image A.

Status of every output: Needs Cleanup (draft). Not production art (ASSET_GAPS.md).
Outputs (art-source/_drafts/tiles/ and Assets/_Game/Art/Tilesets/Village/Draft/):
  TILE_grass_draft.png        8 variants (256x32). Edges are plain single-pixel texture, so any
                              variant sits next to any other without a seam.
  TILE_path_draft_256.png     256 autotile variants (512x512), index = 8-neighbour mask
  TILE_water_draft_256.png    same layout
                              mask bits: N=1 NE=2 E=4 SE=8 S=16 SW=32 W=64 NW=128
  TILE_cliff_draft.png        2-tile-tall cliff face: upper [mid, left_end, right_end, single], lower [same],
                              waterfall upper + lower (320x32)
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
    # cliff stone: cool grey-violet with a warm top light (was tan/brown in vertical columns and read as a
    # wooden fence from a distance, ASSET_GAPS TILE_cliff)
    c_hi="#d8d2e0", c_mid="#aaa2bc", c_mid2="#8e86a4", c_dark="#736a8c", c_darker="#564f6e", c_deepest="#3a3450",
    c_warm="#e8d6c0", moss="#6f9a48", moss_l="#92ba53",
    fl_w="#ffffff", fl_w2="#e3dcef", pb_l="#e9d8b8", pb_d="#a9805e",
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
GRASS_VARIANTS = 8


def blade(img, x, y, tall=False):
    """Small grass-blade mark as in reference A: light tip, two lit sides, dark root."""
    img[y, x] = P["g_hi"]
    img[y + 1, x - 1] = P["g_light"]; img[y + 1, x + 1] = P["g_light"]
    img[y + 1, x] = P["g_dark"]
    img[y + 2, x] = P["g_edge"]
    if tall:
        img[y + 2, x - 1] = P["g_edge"]; img[y + 2, x + 1] = P["g_edge"]
        img[y + 3, x] = P["g_deep"]


def grass_tile(variant):
    """Seamless by construction: edge pixels are plain per-pixel noise; all marks sit in the interior.
    Variants 0-3 plain texture, 4-5 denser blades, 6 clover, 7 tiny white flowers."""
    rng = np.random.default_rng(100 + variant)
    img = np.tile(P["g_base"], (T, T, 1))
    r = rng.random((T, T))
    img[r < 0.13] = P["g_dark"]
    img[r > 0.90] = P["g_light"]
    # dark 'w' clumps (2-3 px) break the flat colour like the painted grass in reference A
    for _ in range(3 + variant % 3):
        x, y = rng.integers(3, T - 5), rng.integers(3, T - 4)
        img[y, x] = P["g_edge"]; img[y + 1, x + 1] = P["g_edge"]; img[y, x + 2] = P["g_edge"]
        img[y - 1, x + 1] = P["g_dark"]
    n_blades = 4 + variant if variant < 6 else 4
    for _ in range(n_blades):
        x, y = rng.integers(3, T - 4), rng.integers(3, T - 5)
        blade(img, x, y, tall=rng.random() < 0.35)
    if variant == 6:
        for _ in range(4):
            x, y = rng.integers(4, T - 5), rng.integers(4, T - 5)
            img[y, x] = P["g_light"]; img[y, x + 1] = P["g_dark"]; img[y + 1, x] = P["g_dark"]; img[y + 1, x + 1] = P["g_edge"]
            img[y - 1, x] = P["g_hi"]
    if variant == 7:
        for _ in range(3):
            x, y = rng.integers(4, T - 5), rng.integers(4, T - 5)
            img[y, x] = P["fl_w"]; img[y - 1, x] = P["fl_w"]; img[y, x - 1] = P["fl_w2"]; img[y, x + 1] = P["fl_w2"]
            img[y + 1, x] = P["g_edge"]
    return img


def dirt_fill(rng):
    img = np.tile(P["d_base"], (T, T, 1))
    r = rng.random((T, T))
    img[r < 0.10] = P["d_dark"]
    img[r > 0.90] = P["d_light"]
    img[(r > 0.5) & (r < 0.515)] = P["d_edge"]
    for _ in range(rng.integers(1, 4)):   # pebbles, interior only
        x, y = rng.integers(3, T - 4), rng.integers(3, T - 4)
        img[y, x] = P["pb_l"]; img[y, x + 1] = P["d_light"]; img[y + 1, x] = P["pb_d"]; img[y + 1, x + 1] = P["d_deep"]
    return img


def water_fill(rng):
    """Flat water with short horizontal ripples (interior only, so no pattern repeats per tile)."""
    img = np.tile(P["w_base"], (T, T, 1))
    r = rng.random((T, T))
    img[r < 0.015] = P["w_deep"]
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


TUFT_NOISE = periodic_noise(octaves=((4, 1.0), (2, 0.5)), seed=21)


def path_tile(mask, rng):
    """Dirt with a darker rim and grass tufts creeping over the edge. The tuft field is periodic in
    32 px and the edge field is continuous across cells, so tile edges stay seamless."""
    t = edge_field(mask)
    img = dirt_fill(rng)
    out = np.zeros((T, T, 4), np.uint8)
    inside = t > 0.08
    out[inside] = img[inside]
    out[(t > 0) & (t <= 0.08)] = P["d_edge"]
    out[(t > 0.08) & (t <= 0.12)] = P["d_dark"]
    out[(t > -0.07) & (t <= 0)] = P["g_edge"]
    creep = TUFT_NOISE > 0.42
    out[creep & (t > 0) & (t <= 0.05)] = P["g_base"]
    out[creep & (t > 0.05) & (t <= 0.1)] = P["g_edge"]
    out[(TUFT_NOISE > 0.62) & (t > 0) & (t <= 0.04)] = P["g_light"]
    return out


SPARK_NOISE = periodic_noise(octaves=((2, 1.0),), seed=31)


def water_tile(mask, rng):
    t = edge_field(mask)
    img = water_fill(rng)
    out = np.zeros((T, T, 4), np.uint8)
    inside = t > 0.13
    out[inside] = img[inside]
    out[(t > 0.13) & (t <= 0.22) & (img[..., 0] == P["w_base"][0])] = P["w_light"]   # shallow band
    sp = (SPARK_NOISE > 0.86) & (t > 0.3)
    out[sp] = P["w_white"]
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
# The face is two tiles tall (64 px): drawn as one image and split into an upper tile (grass lip) and a
# lower tile (cliff foot with contact shadow), so stone courses run across the seam.
# Staggered courses of rounded boulders (x0, width) per course; every course tiles with period 32
# (stones that cross x=32 wrap to x=0). Widths differ so no vertical seam lines up between courses.
FACE_H = 2 * T
_PATTERNS = [
    [(0, 12, 1, 0), (12, 9, 0, 1), (21, 11, 2, 0)],
    [(-5, 10, 2, 0), (5, 13, 1, -1), (18, 6, 3, 0), (24, 8, 1, 1)],
    [(1, 8, 2, 0), (9, 11, 1, 0), (20, 5, 2, 0), (25, 8, 0, 0)],
    [(-3, 14, 1, 0), (11, 7, 2, 1), (18, 10, 0, 0), (28, 7, 1, 0)],
]
_TOPS = [5, 13, 23, 30, 40, 47, 55]                                     # uneven course heights
COURSES = [(t, t + h, _PATTERNS[k % 4]) for k, (t, h) in enumerate(zip(_TOPS, [10, 12, 9, 12, 9, 10, 9]))]
STONE_RAMP = ["c_warm", "c_hi", "c_mid", "c_mid", "c_mid2", "c_dark", "c_darker"]
SIDE_RAMP = ["g_edge", "c_hi", "c_hi", "c_mid", "c_mid", "c_mid2", "c_mid2", "c_dark", "c_darker", "c_deepest"]
SIDE_W = 10                                                       # matches the rim's side wall


def boulder(img, x0, w, y0, y1, rng, shade):
    """Rounded stone: lit top-left, dark bottom-right, mortar outline, 2 px rounded corners, wraps horizontally.
    `shade` darkens lower courses (ambient occlusion toward the cliff foot)."""
    h = y1 - y0
    cx, cy = (w - 1) / 2, (h - 1) / 2
    for yy in range(y0, y1):
        for i in range(w):
            dx, dy = abs(i - cx) / max(cx, 1), abs(yy - y0 - cy) / max(cy, 1)
            r = dx ** 4 + dy ** 4                                  # superellipse: rounded rectangle
            if r > 1.0:
                continue
            x = (x0 + i) % T
            if r > 0.62:
                img[yy, x] = P["c_deepest"]                       # outline
                continue
            u, v = i / max(w - 1, 1), (yy - y0) / max(h - 1, 1)
            k = int((u * 0.45 + v * 0.95) * 3.2) + shade
            img[yy, x] = P[STONE_RAMP[min(k, len(STONE_RAMP) - 1)]]
    if w > 8 and rng.random() < 0.6:                              # small crack
        cxp = (x0 + int(w * rng.uniform(0.4, 0.7))) % T
        img[y0 + 3:y0 + 3 + max(2, h // 3), cxp] = P["c_darker"]
        img[y0 + 3, (cxp + 1) % T] = P["c_hi"]


LIP = [3, 4, 4, 3, 5, 4, 3, 3, 4, 5, 4, 3, 4, 4, 3, 5, 4, 3, 4, 4, 5, 3, 3, 4, 4, 5, 4, 3, 4, 3, 4, 4]


SIDE_COURSES = [(0, 6), (5, 3, ), (12, 7), (18, 4), (26, 7)]   # (y offset in a 32 px period, stagger)


def side_stones(img, side, y_from, y_to, shade=2):
    """Side of the cliff seen at a glancing angle: narrow stacked stones in shadow (darker than the
    front face so the corner reads as the rock turning away, not as a post). Period 32 vertically."""
    rng = np.random.default_rng(91)
    x0 = T - SIDE_W if side == "E" else 0
    img[y_from:y_to, x0:x0 + SIDE_W] = P["c_deepest"]
    for base in range(y_from - 32, y_to, 32):
        for k, (yo, stag) in enumerate(SIDE_COURSES):
            y0 = base + yo
            y1 = base + (SIDE_COURSES[k + 1][0] if k + 1 < len(SIDE_COURSES) else 32) + 1
            for sx, sw in ((0, stag), (stag, SIDE_W - stag)):
                if sw < 3:
                    continue
                tmp = np.zeros((max(1, y1 - y0) + 2, T, 4), np.uint8)
                boulder(tmp, sx, sw, 0, y1 - y0, rng, shade)
                for yy in range(tmp.shape[0]):
                    ty = y0 + yy
                    if y_from <= ty < y_to:
                        for xx in range(SIDE_W):
                            if tmp[yy, xx, 3] > 0:
                                img[ty, x0 + xx] = tmp[yy, xx]
    outer = x0 + SIDE_W - 1 if side == "E" else x0
    img[y_from:y_to, outer] = P["c_deepest"]                       # silhouette against the grass


def side_wall(img, side):
    """Corner: the cliff turns away from the viewer. Shadowed side stones continue the rim's side wall
    down to the ground, with a rounded foot."""
    H = img.shape[0]
    side_stones(img, side, 0, H - 1)
    seam = T - SIDE_W - 1 if side == "E" else SIDE_W               # shadow seam where the front face starts
    img[LIP[seam] + 1:H - 1, seam] = P["c_deepest"]
    for y in range(H - 4, H):                                      # rounded foot: outer columns tuck in
        n = y - (H - 4) + 1
        xs = range(T - n, T) if side == "E" else range(0, n)
        for x in xs:
            img[y, x] = CLEAR
    img[H - 1, :] = np.where(img[H - 1, :, 3:4] > 0, P["g_deep"], CLEAR)


def cliff_face_tall(left_end, right_end):
    """64 px face: returns (upper, lower) tiles."""
    rng = np.random.default_rng(55)
    img = np.zeros((FACE_H, T, 4), np.uint8)
    img[4:FACE_H - 1, :] = P["c_deepest"]                          # mortar / deep shadow behind the stones
    for shade, (y0, y1, stones) in enumerate(COURSES):
        for x0, w, jt, jb in stones:
            boulder(img, x0, w, y0 + jt, min(FACE_H - 1, y1 + jb), rng, min(shade, 4) // 2)
    img[5:7, :][img[5:7, :, 3] > 0] = P["c_darker"]                # shade under the overhanging lip
    for x, y in ((3, 7), (4, 7), (5, 6), (14, 15), (15, 14), (16, 15), (26, 8), (27, 8), (8, 24), (9, 23), (28, 23),
                 (20, 41), (21, 41), (6, 50), (25, 57), (26, 56)):  # moss, more near the damp foot
        if img[y, x, 3] > 0:
            img[y, x] = P["moss"]
    img[7, 4] = P["moss_l"]; img[14, 15] = P["moss_l"]; img[56, 26] = P["moss_l"]
    for x in range(T):                                             # grass lip from the plateau above
        img[0:LIP[x], x] = P["g_base"]
        img[LIP[x], x] = P["g_deep"]
    for x, n in ((6, 3), (13, 2), (21, 4), (29, 2)):              # hanging tufts / vines
        img[LIP[x]:LIP[x] + n, x] = P["g_edge"]; img[LIP[x] + n, x] = P["g_deep"]
    for x, n in ((21, 14), (6, 9)):                                # long vines down the face
        for y in range(LIP[x] + 4, LIP[x] + 4 + n):
            img[y, x + (y // 4) % 2] = P["g_edge"] if y % 3 else P["g_deep"]
    img[1, 2::5] = P["g_light"]; img[0, 4::7] = P["g_hi"]
    for x in range(T):                                             # foot: grass tufts creeping onto the rock
        h = (3, 1, 2, 0, 2, 3, 1, 0)[x % 8]
        img[FACE_H - 1 - h:FACE_H - 1, x] = P["g_edge"] if h > 1 else img[FACE_H - 2, x]
    img[FACE_H - 1, :] = P["g_deep"]                               # contact shadow on the ground below
    if left_end:
        side_wall(img, "W")
    if right_end:
        side_wall(img, "E")
    return img[:T].copy(), img[T:].copy()


def cliff_face(left_end, right_end):
    """Upper tile only (kept for callers that want a one-tile face)."""
    return cliff_face_tall(left_end, right_end)[0]


def waterfall_tall():
    up, lo = cliff_face_tall(False, False)
    img = np.concatenate([up, lo], axis=0)
    n = periodic_noise(seed=11)
    for x in range(4, T - 4):
        for y in range(2, FACE_H):
            v = n[y % T, x] + (0.5 if x % 5 == 0 else 0) + (0.3 if (y + x * 3) % 11 == 0 else 0)
            img[y, x] = P["f_4"] if v > 0.8 else P["f_3"] if v > 0.35 else P["f_2"] if v > -0.1 else P["f_1"] if v > -0.5 else P["f_0"]
    img[FACE_H - 7:, 3:T - 3][::2, ::2] = P["f_4"]                  # splash at the foot
    img[FACE_H - 4:, 2:T - 2][1::2, 1::2] = P["f_3"]
    img[2:6, 3] = P["f_0"]; img[2:6, T - 4] = P["f_0"]
    return img[:T].copy(), img[T:].copy()


def rim(n_open, e_open, w_open):
    """Plateau edge drawn inside the plateau cell (top surface stays transparent: grass shows).
    Side edges show the cliff's side as a 10 px rock wall, as in reference A."""
    rng = np.random.default_rng(77)
    img = np.zeros((T, T, 4), np.uint8)
    if n_open:
        img[0:2, :] = P["c_darker"]; img[2, :] = P["g_edge"]
    for side, open_ in (("E", e_open), ("W", w_open)):
        if open_:
            side_stones(img, side, 0, T, shade=1)
    return img


def strip(tiles):
    return np.concatenate(tiles, axis=1)


def main():
    out_src = os.path.join(ROOT, "art-source/_drafts/tiles")
    out_unity = os.path.join(ROOT, "Assets/_Game/Art/Tilesets/Village/Draft")
    os.makedirs(out_src, exist_ok=True); os.makedirs(out_unity, exist_ok=True)
    FACES = [cliff_face_tall(False, False), cliff_face_tall(True, False), cliff_face_tall(False, True), cliff_face_tall(True, True)]
    files = {
        "TILE_grass_draft.png": strip([grass_tile(v) for v in range(GRASS_VARIANTS)]),
        "TILE_path_draft_256.png": sheet256(path_tile),
        "TILE_water_draft_256.png": sheet256(water_tile),
        # [upper: mid, left_end, right_end, single] [lower: same] [waterfall upper, lower]
        "TILE_cliff_draft.png": strip([f[0] for f in FACES] + [f[1] for f in FACES] + list(waterfall_tall())),
        "TILE_rim_draft.png": strip([rim(bool(i & 1), bool(i & 2), bool(i & 4)) for i in range(8)]),
    }
    for name, arr in files.items():
        for d in (out_src, out_unity):
            Image.fromarray(arr).save(os.path.join(d, name))
        print(name, arr.shape[1], "x", arr.shape[0])


if __name__ == "__main__":
    main()
