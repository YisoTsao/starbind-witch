"""Moonveil Forest (紫幻森林) DRAFT art: terrain tiles, detail overlays and props for Witch World Studio.

Status of every output: REF-DRAFT (Needs Cleanup). Not production art — see docs/MISSING_ASSETS.md.

Terrain (seamless by construction, checked by check_seams() below):
  Palettes are sampled (k-means) from references/03_moonveil_forest_tileset.png; textures are drawn
  procedurally with those palettes, because the reference tiles are not a seamless autotile set.
  TILE_mv_grass_macro.png   128x128, sliced 4x4: one 128 px periodic texture (PeriodicTile picks by x%4,y%4)
  TILE_mv_dark_256.png      dark forest-floor overlay, 256 masks (N=1 NE=2 E=4 SE=8 S=16 SW=32 W=64 NW=128)
  TILE_mv_path_256.png      dirt road
  TILE_mv_paver_256.png     ruined stone pavers (plazas, altar, boss ring)
  TILE_mv_water_256.png     starlit water with a stone shore
  TILE_mv_cliff.png         2-tile-tall purple cliff face [upper mid,L,R,single][lower ...][waterfall up, low]
  TILE_mv_rim.png           plateau rim, index = N*1 + E*2 + W*4
  TILE_mv_detail.png        transparent 32x32 ground details (see DETAILS)

Props: cut from references/04_moonveil_forest_props.png (alpha > 200, hand-placed boxes), downscaled per
category so the proportions match references/02 (witch ~34 px tall), and quantised to one shared palette.
The references are read only; outputs are derived drafts that must be replaced by hand-made pixel art.

Writes Assets/_Game/Art/Biomes/Moonveil/Draft/*.png and Assets/_Game/Art/Biomes/Moonveil/moonveil_manifest.json.
Usage: python3 tools/art/moonveil_art.py <repo_root>
"""
import json, os, sys
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
import gen_tiles as g  # noqa: E402  (edge_field, periodic_noise, DIRS, cliff face drawing)
import moonveil_creatures as mc  # noqa: E402  (boss, vine, lever, shadows: drawn in code)

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
T = 32
OUT_REL = "Assets/_Game/Art/Biomes/Moonveil/Draft/"
OUT = os.path.join(ROOT, OUT_REL)
SRC_TILES = os.path.join(ROOT, "references/03_moonveil_forest_tileset.png")
SRC_PROPS = os.path.join(ROOT, "references/04_moonveil_forest_props.png")
CLEAR = np.zeros(4, np.uint8)


# ------------------------------------------------------------------ palette sampling
def kmeans(pixels, k, iters=14):
    """Deterministic k-means on RGB rows; initial centres are luminance quantiles. Returns centres sorted dark->light."""
    px = pixels.astype(np.float32)
    lum = px @ np.array([0.299, 0.587, 0.114], np.float32)
    order = np.argsort(lum)
    cent = px[order[np.linspace(0, len(px) - 1, k).astype(int)]].copy()
    for _ in range(iters):
        d = ((px[:, None, :] - cent[None]) ** 2).sum(-1)
        a = d.argmin(1)
        for i in range(k):
            sel = px[a == i]
            if len(sel):
                cent[i] = sel.mean(0)
    cent = cent[np.argsort(cent @ np.array([0.299, 0.587, 0.114], np.float32))]
    return np.clip(cent, 0, 255).astype(np.uint8)


def sample_palette(box, k):
    im = np.asarray(Image.open(SRC_TILES).convert("RGBA"))
    x, y, w, h = box
    c = im[y:y + h, x:x + w]
    rgb = c[c[..., 3] > 200][:, :3]
    return [np.array(list(p) + [255], np.uint8) for p in kmeans(rgb, k)]


def rgba(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)] + [255], np.uint8)


# Source boxes on reference 03 (x, y, w, h), chosen on the alpha>200 preview.
GRASS = [np.clip(c.astype(int) * [1.16, 1.2, 1.08, 1], 0, 255).astype(np.uint8) for c in sample_palette((40, 45, 80, 80), 8)]  # lifted: ref 02 lawn reads brighter than the sheet
DARK = sample_palette((455, 45, 80, 80), 7)
DIRT = sample_palette((1085, 70, 70, 40), 6)
WATER = sample_palette((405, 285, 75, 75), 7)
PAVER = sample_palette((30, 282, 80, 80), 8)
STONE = sample_palette((565, 520, 80, 90), 8)
PINK, PINK_L, VIOLET, VIOLET_L, GLOW = rgba("#d77fd0"), rgba("#f4c4ef"), rgba("#9a5ad8"), rgba("#c79bf2"), rgba("#e7c8ff")
OUTLINE = rgba("#24182e")


def periodic_noise(size, cell, seed, octaves=3):
    """Value noise periodic in `size`, range about -1..1 (cells must divide size)."""
    rng = np.random.default_rng(seed)
    out = np.zeros((size, size))
    amp, tot = 1.0, 0.0
    for _ in range(octaves):
        n = max(1, size // cell)
        gr = rng.uniform(-1, 1, (n, n))
        ys, xs = np.mgrid[0:size, 0:size] / cell
        x0, y0 = xs.astype(int), ys.astype(int)
        fx, fy = xs - x0, ys - y0
        fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
        a = gr[y0 % n, x0 % n]; b = gr[y0 % n, (x0 + 1) % n]
        c = gr[(y0 + 1) % n, x0 % n]; d = gr[(y0 + 1) % n, (x0 + 1) % n]
        out += amp * ((a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy)
        tot += amp
        amp *= 0.5
        cell = max(1, cell // 2)
    return out / tot


# ------------------------------------------------------------------ fills (periodic)
def leafy_fill(size, pal, seed, density=0.55):
    """Dense clover/leaf texture like reference 03: small lit-top leaf clusters over a mid tone,
    brightness modulated by low-frequency noise. Periodic in `size` (stamps wrap)."""
    rng = np.random.default_rng(seed)
    n = len(pal)
    low = periodic_noise(size, max(8, size // 4), seed + 1, 2)
    img = np.zeros((size, size, 4), np.uint8)
    base = np.clip((2 + low * 1.4).round().astype(int), 1, n - 3)
    for i in range(n):
        img[base == i] = pal[i]
    count = int(size * size * density / 3)
    for _ in range(count):
        x, y = int(rng.integers(0, size)), int(rng.integers(0, size))
        lv = int(np.clip(round(3 + low[y, x] * 2 + rng.normal(0, 0.7)), 1, n - 1))
        shape = rng.integers(0, 3)
        pts = [(0, 0), (1, 0), (0, 1)] if shape == 0 else [(0, 0), (1, 0), (1, 1)] if shape == 1 else [(0, 0), (0, 1), (1, 1)]
        for dx, dy in pts:
            img[(y + dy) % size, (x + dx) % size] = pal[lv]
        img[y % size, x % size] = pal[min(n - 1, lv + 1)]              # lit top-left pixel
        img[(y + 2) % size, (x + 1) % size] = pal[max(0, lv - 2)]      # shade under the cluster
    return img


def meadow_fill(size, pal, seed):
    """Lawn in the manner of references/02 (v2): broad light/shade patches, then many 3-blade tufts
    (dark root, mid blades, lit tips) and clover clumps in the lighter patches. Periodic in `size`."""
    rng = np.random.default_rng(seed)
    n = len(pal)
    low = periodic_noise(size, max(8, size // 4), seed + 1, 2)
    mid = periodic_noise(size, 16, seed + 2, 2)
    img = np.zeros((size, size, 4), np.uint8)
    jitter = rng.normal(0, 0.38, (size, size))                         # dithered patch borders, no hard blobs
    base = np.clip((2.4 + low * 1.4 + mid * 0.5 + jitter).round().astype(int), 1, n - 4)
    for i in range(n):
        img[base == i] = pal[i]

    def put(x, y, lv):
        img[y % size, x % size] = pal[int(np.clip(lv, 0, n - 1))]

    # grass tufts: three short blades fanning up from a dark root
    for _ in range(size * size // 9):
        x, y = int(rng.integers(0, size)), int(rng.integers(0, size))
        lv = int(np.clip(round(3.2 + low[y, x] * 1.8 + rng.normal(0, 0.7)), 2, n - 2))
        put(x, y + 1, lv - 2)                          # root shadow
        put(x - 1, y, lv); put(x, y, lv); put(x + 1, y, lv - 1)
        put(x - 1, y - 1, lv + 1); put(x + 1, y - 1, lv)
        put(x, y - 2, lv + 2)                          # tallest blade, lit tip
        if rng.random() < 0.35:
            put(x + 2, y - 1, lv + 1)
    # clover clumps in the lit patches (3 round leaves, darker centre)
    for _ in range(size * size // 160):
        x, y = int(rng.integers(0, size)), int(rng.integers(0, size))
        if low[y, x] < -0.05:
            continue
        lv = n - 3
        for dx, dy in ((0, -1), (-1, 0), (1, 0)):
            put(x + dx, y + dy, lv + 1)
            put(x + dx * 2, y + dy, lv)
        put(x, y, lv - 2); put(x, y + 1, lv - 3)
    # sparse bright specks (dew / tiny flowers)
    for _ in range(size * size // 260):
        x, y = int(rng.integers(0, size)), int(rng.integers(0, size))
        put(x, y, n - 1)
    return img


def dirt_fill(size, seed):
    rng = np.random.default_rng(seed)
    low = periodic_noise(size, 16, seed + 3, 2)
    r = rng.random((size, size))
    lv = np.clip((2.5 + low * 1.2 + (r - 0.5) * 1.6).round().astype(int), 1, len(DIRT) - 2)
    img = np.zeros((size, size, 4), np.uint8)
    for i, c in enumerate(DIRT):
        img[lv == i] = c
    for _ in range(size * size // 90):                                 # pebbles (wrap)
        x, y = int(rng.integers(0, size)), int(rng.integers(0, size))
        img[y, x] = DIRT[-1]; img[(y + 1) % size, x] = DIRT[0]; img[y, (x + 1) % size] = DIRT[-2]
    return img


def water_fill(size, seed):
    rng = np.random.default_rng(seed)
    low = periodic_noise(size, 16, seed + 5, 2)
    img = np.zeros((size, size, 4), np.uint8)
    lv = np.clip((2 + low * 1.3).round().astype(int), 1, 3)
    for i in range(4):
        img[lv == i] = WATER[i]
    for _ in range(size * size // 70):                                 # ripples
        x, y, l = int(rng.integers(0, size)), int(rng.integers(0, size)), int(rng.integers(2, 6))
        for k in range(l):
            img[y, (x + k) % size] = WATER[4]
        img[(y + 1) % size, (x + 1) % size] = WATER[1]
    for _ in range(max(1, size * size // 420)):                        # 4-point star sparkles
        x, y = int(rng.integers(0, size)), int(rng.integers(0, size))
        img[y, x] = rgba("#f2eaff")
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            img[(y + dy) % size, (x + dx) % size] = WATER[-1]
    return img


def voronoi_periodic(n, seed, size=T):
    """Periodic Voronoi on a size x size torus: (cell id, distance to the cell border)."""
    rng = np.random.default_rng(seed)
    pts = rng.uniform(0, size, (n, 2))
    ys, xs = np.mgrid[0:size, 0:size] + 0.5
    best, second, idx = np.full((size, size), 1e9), np.full((size, size), 1e9), np.zeros((size, size), int)
    for i, (px_, py_) in enumerate(pts):
        for ox in (-size, 0, size):
            for oy in (-size, 0, size):
                d = np.hypot(xs - px_ - ox, ys - py_ - oy)
                closer = d < best
                second = np.where(closer, best, np.minimum(second, d))
                idx = np.where(closer, i, idx)
                best = np.where(closer, d, best)
    return idx, (second - best) / 2


def flagstone_fill(size, seed):
    """Irregular ruin flagstones (v2): periodic Voronoi stones with bevelled edges (lit top-left,
    shaded bottom-right), per-stone tone, speckle, a few cracks, moss in the joints."""
    rng = np.random.default_rng(seed)
    idx, edge = voronoi_periodic(6, seed, size)
    n = len(PAVER)
    tone = rng.integers(3, n - 2, idx.max() + 1)
    lv = tone[idx].astype(int)
    joint = edge < 0.9
    up = np.roll(joint, 1, 0) | np.roll(joint, 1, 1)                 # border above / left -> lit bevel
    dn = np.roll(joint, -1, 0) | np.roll(joint, -1, 1)               # border below / right -> shade
    lv = lv + (up & ~joint) - (dn & ~joint)
    lv = lv + (rng.random((size, size)) < 0.08) - (rng.random((size, size)) < 0.08)
    lv = np.clip(lv, 2, n - 1)
    img = np.zeros((size, size, 4), np.uint8)
    for i, c_ in enumerate(PAVER):
        img[lv == i] = c_
    img[joint] = PAVER[1]
    img[joint & (np.roll(edge, 1, 0) < 0.5)] = PAVER[0]
    for cell in range(idx.max() + 1):                                 # hairline cracks in some stones
        if rng.random() > 0.4:
            continue
        ys, xs = np.nonzero((idx == cell) & (edge > 2.5))
        if len(ys) == 0:
            continue
        k = int(rng.integers(0, len(ys)))
        y, x = int(ys[k]), int(xs[k])
        for step in range(int(rng.integers(3, 6))):
            img[y % size, x % size] = PAVER[1]
            x += int(rng.integers(-1, 2)); y += 1
    moss = periodic_noise(size, 8, seed + 9, 2)
    img[joint & (moss > 0.1)] = GRASS[2]
    img[joint & (moss > 0.45)] = GRASS[3]
    creep = (edge < 1.8) & ~joint & (moss > 0.5)
    img[creep] = GRASS[2]
    return img


def paver_fill(size, seed):
    """v1 (kept for reference): square-ish stones (period 16 px rows, staggered), mortar in the darkest paver tone, moss in cracks."""
    rng = np.random.default_rng(seed)
    img = np.zeros((size, size, 4), np.uint8)
    img[:] = PAVER[0]
    st = 16                                                           # ~2x2 stones per tile, like ref 03
    for row in range(size // st):
        off = (st // 2) * (row % 2)
        for col in range(size // st):
            x0, y0 = col * st + off, row * st
            shade = int(rng.integers(3, 6))
            if rng.random() < 0.14:                                   # missing stone: moss shows through
                for yy in range(1, st):
                    for xx in range(1, st):
                        img[(y0 + yy) % size, (x0 + xx) % size] = GRASS[2 + (xx + yy) % 2]
                continue
            for yy in range(1, st):
                for xx in range(1, st):
                    e = st - 1
                    corner = (xx, yy) in {(1, 1), (2, 1), (1, 2), (e, 1), (e - 1, 1), (e, 2), (1, e), (1, e - 1), (2, e), (e, e), (e - 1, e), (e, e - 1)}
                    if corner:
                        continue
                    lv = shade + (1 if (xx <= 2 or yy <= 2) else 0) - (1 if (xx >= st - 2 or yy >= st - 2) else 0)
                    img[(y0 + yy) % size, (x0 + xx) % size] = PAVER[min(len(PAVER) - 1, lv)]
            if rng.random() < 0.5:                                    # crack
                cx = (x0 + int(rng.integers(4, st - 4))) % size
                for k in range(int(rng.integers(3, 7))):
                    img[(y0 + 3 + k) % size, (cx + k // 3) % size] = PAVER[1]
    moss = periodic_noise(size, 8, seed + 9, 2) > 0.35
    mortar = (img[..., :3] == PAVER[0][:3]).all(-1)
    img[moss & mortar] = GRASS[2]
    return img


# ------------------------------------------------------------------ autotiles
SHORE_CELLS = None


def shore_stones():
    """Periodic 32 px cellular pattern used for the stone ring around water: (cell id, distance to cell edge)."""
    rng = np.random.default_rng(404)
    pts = rng.uniform(0, T, (14, 2))
    ys, xs = np.mgrid[0:T, 0:T] + 0.5
    best, second, idx = np.full((T, T), 1e9), np.full((T, T), 1e9), np.zeros((T, T), int)
    for i, (px, py) in enumerate(pts):
        for ox in (-T, 0, T):
            for oy in (-T, 0, T):
                d = np.hypot(xs - px - ox, ys - py - oy)
                closer = d < best
                second = np.where(closer, best, np.minimum(second, d))
                idx = np.where(closer, i, idx)
                best = np.where(closer, d, best)
    return idx, second - best, best


def autotile_sheet(fn):
    sheet = np.zeros((16 * T, 16 * T, 4), np.uint8)
    for mask in range(256):
        r, c = divmod(mask, 16)
        sheet[r * T:(r + 1) * T, c * T:(c + 1) * T] = fn(mask)
    return sheet


DIRT_FILL = None
DARK_FILL = None
PAVER_FILL = None
WATER_FILL = None
TUFT1 = periodic_noise(T, 8, 21, 2)


def per(a, shape):
    """Tile a 32-periodic array to `shape` (so one function shades a single tile or a whole map)."""
    reps = (shape[0] // T + 1, shape[1] // T + 1) + (1,) * (a.ndim - 2)
    return np.tile(a, reps)[:shape[0], :shape[1]]


# Every shade_* maps an edge field t (any size, multiple of 32) to RGBA. Tiles call it with the 32x32
# field of one mask; check_seams() calls it with the field of a whole map, so equality proves no seams.
def shade_path(t):
    out = np.zeros(t.shape + (4,), np.uint8)
    fill, tuft = per(DIRT_FILL, t.shape), per(TUFT1, t.shape)
    inside = t > 0.07
    out[inside] = fill[inside]
    out[(t > 0.0) & (t <= 0.07)] = DIRT[1]
    out[(t > -0.06) & (t <= 0.0)] = GRASS[1]
    out[(tuft > 0.3) & (t > 0) & (t <= 0.06)] = GRASS[3]
    out[(tuft > 0.55) & (t > 0) & (t <= 0.04)] = GRASS[5]
    return out


def shade_dark(t):
    out = np.zeros(t.shape + (4,), np.uint8)
    fill = per(DARK_FILL, t.shape)
    ys, xs = np.mgrid[0:t.shape[0], 0:t.shape[1]]
    checker, sparse = (xs + ys) % 2 == 0, (xs * 3 + ys) % 4 == 0
    inside = t > 0.06
    out[inside] = fill[inside]
    band_in = (t > 0) & (t <= 0.06) & checker
    out[band_in] = fill[band_in]
    out[(t > -0.08) & (t <= 0) & sparse] = DARK[2]
    return out


def shade_paver(t):
    out = np.zeros(t.shape + (4,), np.uint8)
    fill, tuft = per(PAVER_FILL, t.shape), per(TUFT1, t.shape)
    rough = tuft * 0.06
    inside = t > 0.05 + rough
    out[inside] = fill[inside]
    edge = (t > -0.02 + rough) & (t <= 0.05 + rough)
    out[edge] = GRASS[2]
    out[edge & (tuft > 0.2)] = GRASS[4]
    return out


def shade_water(t):
    idx, edge_d = per(SHORE_CELLS[0], t.shape), per(SHORE_CELLS[1], t.shape)
    out = np.zeros(t.shape + (4,), np.uint8)
    fill = per(WATER_FILL, t.shape)
    inside = t > 0.15
    out[inside] = fill[inside]
    out[(t > 0.15) & (t <= 0.19)] = WATER[1]                          # dark rim under the stones
    stone = (t > -0.06) & (t <= 0.15)
    shade = 3 + (idx % 3)
    for lv in range(len(STONE)):
        out[stone & (shade == lv)] = STONE[lv]
    up = np.roll(edge_d, 1, 0)                                         # periodic field: roll stays seamless
    out[stone & (edge_d > 2.2) & (up <= 2.2)] = STONE[-1]             # lit top edge of each stone
    out[stone & (edge_d <= 1.0)] = STONE[0]                           # gaps between stones
    out[(t > -0.08) & (t <= -0.06)] = GRASS[1]                         # damp grass rim
    return out


# Edge field per material: (blur radius R, inward offset OFF, jaggedness AMP). Same maths as
# gen_tiles.edge_field, but a wider R gives room for a stone shore; at every cell border
# t <= -OFF + AMP stays below each material's lowest band, so empty neighbours draw nothing.
FIELD = {"dark": (6, 0.28, 0.16), "path": (7, 0.30, 0.2), "paver": (6, 0.28, 0.16), "water": (12, 0.26, 0.15)}
EDGE_NOISE_1 = periodic_noise(T, 8, 3, 3)


def field_from(big, R, OFF, AMP, reps):
    k = 2 * R + 1
    c = np.pad(np.cumsum(np.cumsum(np.pad(big, R, mode="constant"), 0), 1), ((1, 0), (1, 0)))
    s = (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)
    return s - 0.5 - OFF - AMP * np.tile(EDGE_NOISE_1, reps)


def edge_field(mask, key):
    R, OFF, AMP = FIELD[key]
    m = g.neighbourhood(mask)
    return field_from(m, R, OFF, AMP, (3, 3))[T:2 * T, T:2 * T]


def mask_tile(shade, key):
    return lambda mask: shade(edge_field(mask, key))


# ------------------------------------------------------------------ cliff (reuses the village cliff drawing, recoloured)
def recolour_cliff():
    """Palette for the shared side-wall/rim helpers in gen_tiles, then the Moonveil columnar face."""
    P = g.P
    s = STONE
    P.update(c_hi=s[6], c_warm=s[7], c_mid=s[5], c_mid2=s[4], c_dark=s[3], c_darker=s[2], c_deepest=s[0],
             moss=DARK[3], moss_l=GRASS[4], g_base=GRASS[3], g_edge=GRASS[2], g_light=GRASS[5], g_hi=GRASS[6],
             g_deep=GRASS[0], g_dark=GRASS[2],
             f_0=WATER[1], f_1=WATER[2], f_2=WATER[4], f_3=WATER[5], f_4=rgba("#eef0ff"))
    faces = [column_face(False, False), column_face(True, False), column_face(False, True), column_face(True, True)]
    cliff = np.concatenate([f[0] for f in faces] + [f[1] for f in faces] + list(column_waterfall()), axis=1)
    rim = np.concatenate([g.rim(bool(i & 1), bool(i & 2), bool(i & 4)) for i in range(8)], axis=1)
    stairs = [stair_face(False, False), stair_face(True, False), stair_face(False, True), stair_face(True, True)]
    stair_sheet = np.concatenate([f[0] for f in stairs] + [f[1] for f in stairs], axis=1)
    return cliff, rim, stair_sheet


# Columnar basalt like references/03 row 3: vertical columns (period 32 px), lit left edge, shadowed right
# edge, rounded tops under a grass lip, a few horizontal joints, moss and vines. 64 px tall (two tiles).
COLUMNS = [(0, 7), (7, 9), (16, 6), (22, 10)]       # (x0, width), wraps at 32
FACE_H = 2 * T
LIP2 = [4, 5, 5, 4, 6, 5, 4, 4, 5, 6, 5, 4, 5, 5, 4, 6, 5, 4, 5, 5, 6, 4, 4, 5, 5, 6, 5, 4, 5, 4, 5, 5]


def column_face(left_end, right_end):
    rng = np.random.default_rng(57)
    img = np.zeros((FACE_H, T, 4), np.uint8)
    img[3:FACE_H - 1, :] = STONE[0]
    for k, (x0, w) in enumerate(COLUMNS):
        top = 6 + (k * 3) % 5
        joints = [top + 14 + (k * 7) % 9, top + 33 + (k * 5) % 7]
        for yy in range(top, FACE_H - 2):
            for i in range(w):
                x = (x0 + i) % T
                if i == w - 1:
                    img[yy, x] = STONE[0]                          # gap between columns
                    continue
                if yy == top and i in (0, w - 2):
                    continue                                       # rounded column head
                lv = 5 if i == 0 else 3 if i >= w - 3 else 4
                if yy <= top + 1:
                    lv = 6                                         # lit column head
                if yy > FACE_H - 14:
                    lv -= 1                                        # damp, darker foot
                if yy in joints:
                    lv = 1
                elif yy - 1 in joints and i < w - 2:
                    lv = min(lv + 1, 6)
                img[yy, x] = STONE[max(1, lv)]
        if rng.random() < 0.7:                                     # hairline crack
            cx = (x0 + 2 + int(rng.integers(0, max(1, w - 4)))) % T
            for yy in range(top + 6, top + 6 + int(rng.integers(5, 12))):
                img[yy, cx] = STONE[2]
    for x in range(T):                                             # grass lip of the plateau
        img[0:LIP2[x], x] = GRASS[3]
        img[LIP2[x], x] = GRASS[0]
        img[0, x] = GRASS[5] if x % 5 == 1 else GRASS[3]
    for x, n in ((3, 9), (12, 5), (19, 13), (27, 6)):              # hanging moss / vines
        for yy in range(LIP2[x] + 1, LIP2[x] + 1 + n):
            img[yy, x + (yy // 5) % 2] = GRASS[2] if yy % 3 else DARK[3]
        img[LIP2[x] + n, x] = GRASS[4]
    for x in range(T):                                             # foot: grass tufts and pebbles, contact shadow
        h = (3, 1, 2, 0, 2, 3, 1, 0)[x % 8]
        if h:
            img[FACE_H - 1 - h:FACE_H - 1, x] = GRASS[2]
    for x in (5, 6, 21, 22, 23):
        img[FACE_H - 4, x] = STONE[5]; img[FACE_H - 3, x] = STONE[3]
    img[FACE_H - 1, :] = GRASS[0]
    if left_end:
        g.side_wall(img, "W")
    if right_end:
        g.side_wall(img, "E")
    return img[:T].copy(), img[T:].copy()


def column_waterfall():
    up, lo = column_face(False, False)
    img = np.concatenate([up, lo], axis=0)
    n = periodic_noise(T, 8, 11, 2)
    for x in range(4, T - 4):
        for y in range(2, FACE_H):
            v = n[y % T, x] + (0.5 if x % 5 == 0 else 0) + (0.3 if (y + x * 3) % 11 == 0 else 0)
            img[y, x] = rgba("#eef0ff") if v > 0.8 else WATER[5] if v > 0.35 else WATER[4] if v > -0.1 else WATER[2] if v > -0.5 else WATER[1]
    img[FACE_H - 7:, 3:T - 3][::2, ::2] = rgba("#eef0ff")
    img[FACE_H - 4:, 2:T - 2][1::2, 1::2] = WATER[5]
    return img[:T].copy(), img[T:].copy()


def stair_face(left_end, right_end):
    """Stone steps cut into the 2-tile cliff (8 px rise): lit tread, dark riser; columns form the cheeks."""
    img = np.zeros((FACE_H, T, 4), np.uint8)
    for step in range(FACE_H // 8):
        y0 = step * 8
        img[y0:y0 + 3, :] = PAVER[6]                               # tread
        img[y0, :] = PAVER[7]
        img[y0 + 3:y0 + 8, :] = PAVER[3]                           # riser
        img[y0 + 7, :] = PAVER[1]
        for x in range(4 + step * 5 % 7, T, 11):                    # slab joints, staggered
            img[y0 + 3:y0 + 8, x] = PAVER[1]
        for x in ((step * 13) % T, (step * 13 + 17) % T):         # moss in the corners
            img[y0 + 2, x] = GRASS[3]
    img[FACE_H - 1, :] = GRASS[0]
    if left_end:
        g.side_wall(img, "W")
    if right_end:
        g.side_wall(img, "E")
    return img[:T].copy(), img[T:].copy()


# ------------------------------------------------------------------ extra props drawn here (not cut from ref 04)
def broken_bridge(bridge):
    """The cut wooden bridge with its middle planks gone (lever puzzle); jagged plank ends."""
    img = bridge.copy()
    h, w = img.shape[:2]
    a, b = int(w * 0.38), int(w * 0.62)
    rng = np.random.default_rng(3)
    img[:, a + 1:b - 1] = CLEAR
    for x in (a, a + 1, b - 2, b - 1):                            # splintered ends
        for y in range(h):
            if img[y, x, 3] and rng.random() < 0.35:
                img[y, x] = CLEAR
    return img


def bridge_vertical(bridge):
    """North-south wooden bridge (3 tiles wide) drawn from the horizontal bridge's palette."""
    px = bridge[bridge[..., 3] > 0][:, :3].astype(int)
    lum = px @ np.array([3, 6, 1])
    order = np.argsort(lum)
    tones = [np.array(list(px[order[int(len(order) * q)]]) + [255], np.uint8) for q in (0.05, 0.25, 0.5, 0.75, 0.93)]
    W, H = 96, 150
    img = np.zeros((H, W, 4), np.uint8)
    for y in range(4, H - 4):                                      # planks across the deck
        k = (y // 6) % 3
        img[y, 12:W - 12] = tones[2 + (k == 1)] if y % 6 else tones[0]
        if y % 6 == 1:
            img[y, 12:W - 12] = tones[4]
    for x0 in (4, W - 12):                                         # rails and posts
        img[2:H - 2, x0:x0 + 8] = tones[1]
        img[2:H - 2, x0 + 2:x0 + 6] = tones[3]
        for y in range(2, H - 10, 30):
            img[y:y + 10, x0:x0 + 8] = tones[0]
            img[y + 1:y + 3, x0 + 1:x0 + 7] = tones[4]
    img[H - 4:, 12:W - 12] = tones[0]                              # shadow under the south end
    return img


def magic_circle():
    """Boss-clearing summoning circle (references/01): rings, hexagram, rune ticks. Ground decal, 160 px."""
    S = 160
    img = np.zeros((S, S, 4), np.uint8)
    ys, xs = np.mgrid[0:S, 0:S] + 0.5
    c = S / 2
    r = np.hypot(xs - c, ys - c)
    glow, bright, dim = rgba("#c9a4ff"), rgba("#f1e4ff"), rgba("#7d5aa8")
    for rad, col in ((76, glow), (73, dim), (58, glow), (30, bright)):
        img[(r >= rad - 1) & (r < rad + 0.6)] = col
    ang = np.arctan2(ys - c, xs - c)
    ticks = (r > 61) & (r < 71) & ((np.degrees(ang) % 15) < 2.2)
    img[ticks] = dim
    for k in range(2):                                             # hexagram: two triangles
        pts = [(c + 58 * np.cos(np.radians(90 + 120 * i + 60 * k)), c + 58 * np.sin(np.radians(90 + 120 * i + 60 * k))) for i in range(3)]
        for i in range(3):
            (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % 3]
            for t in np.linspace(0, 1, 160):
                x, y = int(x0 + (x1 - x0) * t), int(y0 + (y1 - y0) * t)
                img[y, x] = glow
    for i in range(12):                                            # rune dots on the outer band
        a = np.radians(i * 30 + 7)
        x, y = int(c + 66 * np.cos(a)), int(c + 66 * np.sin(a))
        img[y - 1:y + 2, x - 1:x + 2] = bright
    return img


# ------------------------------------------------------------------ ground detail overlays (32x32 transparent)
def flower(img, x, y, petal, centre):
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        img[y + dy, x + dx] = petal
    img[y, x] = centre
    img[y + 2, x] = GRASS[1]


def detail(kind, variant):
    rng = np.random.default_rng(700 + kind * 10 + variant)
    img = np.zeros((T, T, 4), np.uint8)
    pos = [(int(rng.integers(4, T - 5)), int(rng.integers(4, T - 6))) for _ in range(5)]
    if kind == 0:                    # pink flowers
        for x, y in pos[:3 + variant]:
            flower(img, x, y, PINK, PINK_L)
    elif kind == 1:                  # violet flowers
        for x, y in pos[:3 + variant]:
            flower(img, x, y, VIOLET, VIOLET_L)
    elif kind == 2:                  # clover / grass tufts
        for x, y in pos:
            img[y, x] = GRASS[6]; img[y, x + 1] = GRASS[5]; img[y + 1, x] = GRASS[4]; img[y + 1, x + 1] = GRASS[2]
            img[y - 1, x + 1] = GRASS[5]; img[y + 2, x] = GRASS[0]
    elif kind == 3:                  # pebbles
        for x, y in pos[:3 + variant]:
            img[y, x:x + 2] = STONE[6]; img[y + 1, x:x + 2] = STONE[3]; img[y + 1, x + 2] = STONE[1]
    elif kind == 4:                  # tiny glowing mushrooms
        for x, y in pos[:2 + variant]:
            img[y, x - 1:x + 2] = VIOLET_L; img[y - 1, x] = GLOW; img[y + 1, x] = rgba("#e8dcef"); img[y + 2, x] = GRASS[0]
    elif kind == 5:                  # fallen petals
        for _ in range(9 + 3 * variant):
            x, y = int(rng.integers(2, T - 2)), int(rng.integers(2, T - 2))
            img[y, x] = PINK if rng.random() < 0.5 else VIOLET_L
    return img


DETAILS = ["flowers_pink", "flowers_violet", "clover", "pebbles", "glowcaps", "petals"]


# ------------------------------------------------------------------ props
# name: (box on ref 04, scale, category, footprint collider [w, h, ox, oy] in tiles or None, fade, light)
# light: (r, g, b, radius, intensity, ox, oy) ; scales follow ref 02 proportions (witch ~34 px).
TREE, BIG, MID, SMALL = 0.5, 0.42, 0.34, 0.24
PROPS = {
    "tree_lantern":   ((0, 0, 337, 289), TREE, "tree", [1.2, 0.5, 0, 0.35], True, (1.0, 0.78, 0.45, 2.6, 0.8, 1.2, 1.9)),
    "tree_purple_a":  ((326, 10, 264, 275), TREE, "tree", [1.1, 0.5, 0, 0.35], True, None),
    "tree_wisteria":  ((586, 19, 287, 265), TREE, "tree", [1.1, 0.5, 0, 0.35], True, None),
    "tree_purple_b":  ((857, 17, 249, 267), TREE, "tree", [1.0, 0.5, 0, 0.35], True, None),
    "tree_blossom":   ((1080, 35, 219, 241), TREE, "tree", [0.6, 0.4, 0, 0.3], True, None),
    "tree_pine":      ((1289, 18, 159, 256), TREE, "tree", [0.6, 0.4, 0, 0.3], True, None),
    "tree_dead":      ((12, 284, 254, 162), BIG, "tree", [0.8, 0.4, 0, 0.3], True, None),
    "log_hollow":     ((262, 311, 255, 133), MID, "deadwood", [2.0, 0.5, 0, 0.35], False, None),
    "stump_roots":    ((523, 292, 259, 161), MID, "deadwood", [1.4, 0.5, 0, 0.35], False, None),
    "bush_dark":      ((790, 311, 153, 117), SMALL, "bush", None, False, None),
    "bush_flower":    ((950, 306, 164, 123), SMALL, "bush", None, False, None),
    "bush_green":     ((1121, 304, 169, 122), SMALL, "bush", None, False, None),
    "bush_berry":     ((1293, 307, 155, 121), SMALL, "bush", None, False, None),
    "rock_large":     ((15, 445, 205, 125), MID, "rock", [1.6, 0.6, 0, 0.35], False, None),
    "rock_cluster":   ((234, 469, 155, 94), MID, "rock", [1.2, 0.45, 0, 0.25], False, None),
    "rock_mossy":     ((411, 481, 126, 84), MID, "rock", [1.0, 0.4, 0, 0.22], False, None),
    "rock_small":     ((555, 493, 143, 70), SMALL, "rock", None, False, None),
    "flowers_pink":   ((742, 457, 158, 101), SMALL, "flower", None, False, None),
    "flowers_violet": ((916, 458, 163, 102), SMALL, "flower", None, False, None),
    "lavender":       ((1105, 438, 155, 119), SMALL, "flower", None, False, None),
    "bellflower":     ((1285, 443, 138, 115), SMALL, "flower", None, False, None),
    "mushroom_giant": ((10, 569, 152, 118), MID, "mushroom", [0.8, 0.35, 0, 0.2], False, (0.75, 0.5, 1.0, 2.2, 0.85, 0, 0.6)),
    "mushroom_pair":  ((191, 585, 111, 98), SMALL + 0.04, "mushroom", None, False, (0.75, 0.5, 1.0, 1.4, 0.7, 0, 0.4)),
    "mushroom_tall":  ((333, 572, 114, 108), SMALL + 0.04, "mushroom", None, False, (0.75, 0.5, 1.0, 1.4, 0.7, 0, 0.5)),
    "mushroom_shelf": ((461, 579, 151, 101), SMALL + 0.04, "mushroom", None, False, (0.75, 0.5, 1.0, 1.2, 0.55, 0, 0.3)),
    "crystal_violet": ((640, 555, 140, 125), MID, "crystal", [0.9, 0.4, 0, 0.22], False, (0.72, 0.5, 1.0, 2.4, 0.9, 0, 0.8)),
    "crystal_blue":   ((806, 579, 109, 101), MID, "crystal", [0.7, 0.35, 0, 0.2], False, (0.55, 0.65, 1.0, 2.0, 0.85, 0, 0.6)),
    "crystal_small":  ((941, 591, 105, 89), SMALL + 0.04, "crystal", None, False, (0.72, 0.5, 1.0, 1.3, 0.7, 0, 0.35)),
    "reeds":          ((1072, 567, 136, 122), SMALL + 0.04, "shore", None, False, None),
    "lilypads":       ((1225, 583, 212, 99), SMALL, "water", None, False, None),
    "pillar_tall":    ((15, 688, 86, 176), BIG - 0.04, "ruin", [0.8, 0.4, 0, 0.22], True, None),
    "pillar_short":   ((101, 750, 68, 114), BIG - 0.04, "ruin", [0.8, 0.4, 0, 0.22], False, None),
    "pillars_broken": ((170, 718, 220, 145), BIG - 0.04, "ruin", [2.0, 0.5, 0, 0.3], False, None),
    "arch_ruin":      ((395, 682, 211, 179), BIG, "ruin", None, True, None),
    "shrine_moon":    ((626, 680, 214, 182), BIG, "ruin", None, True, (0.95, 0.78, 0.5, 2.4, 0.8, 0, 0.6)),
    "stone_circle":   ((857, 725, 223, 129), MID, "ruin", None, False, None),
    "lamp_post":      ((1087, 712, 90, 143), MID + 0.04, "light", [0.35, 0.25, -0.25, 0.12], False, (1.0, 0.78, 0.45, 2.4, 0.85, 0.35, 1.05)),
    "lamp_crook":     ((1193, 711, 86, 143), MID + 0.04, "light", [0.35, 0.25, -0.1, 0.12], False, (1.0, 0.78, 0.45, 2.4, 0.85, -0.1, 0.95)),
    "lantern_hang":   ((1293, 720, 51, 112), MID, "light", None, False, (1.0, 0.78, 0.45, 1.6, 0.7, 0, 0.5)),
    "lantern_stone":  ((1361, 736, 64, 117), MID, "light", [0.45, 0.3, 0, 0.15], False, (1.0, 0.78, 0.45, 2.0, 0.8, 0, 0.8)),
    "signpost":       ((24, 878, 117, 185), MID, "camp", [0.35, 0.25, 0, 0.12], False, None),
    "bridge":         ((132, 903, 296, 170), BIG + 0.02, "bridge", None, False, None),
    "tent":           ((445, 860, 375, 226), BIG, "camp", [3.6, 1.0, 0, 0.6], True, (1.0, 0.78, 0.45, 2.6, 0.8, -1.4, 2.0)),
    "campfire_set":   ((821, 881, 303, 192), MID, "camp", [0.9, 0.5, 0.2, 0.4], False, (1.0, 0.62, 0.3, 3.2, 1.0, 0.25, 0.6)),
    "merchant_cart":  ((1127, 857, 302, 214), BIG, "camp", [3.2, 0.9, 0, 0.5], False, (1.0, 0.78, 0.45, 2.2, 0.75, -1.6, 2.0)),
}


def drop_specks(mask, min_px):
    """Remove detached opaque islands smaller than min_px (stray petals/leaves of the AI sheet)."""
    H, W = mask.shape
    seen = np.zeros_like(mask)
    out = mask.copy()
    for y0 in range(H):
        for x0 in range(W):
            if not mask[y0, x0] or seen[y0, x0]:
                continue
            stack, comp = [(y0, x0)], []
            seen[y0, x0] = True
            while stack:
                y, x = stack.pop()
                comp.append((y, x))
                for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                    if 0 <= ny < H and 0 <= nx < W and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if len(comp) < min_px:
                for y, x in comp:
                    out[y, x] = False
    return out


def despeckle(img, passes=2):
    """Pixel cleanup after downscaling (v2): an opaque pixel whose four neighbours all share one colour
    different from it takes that colour. Removes the single-pixel noise LANCZOS leaves in foliage so
    clusters read as deliberate pixel clumps."""
    out = img.copy()
    for _ in range(passes):
        rgb = out[..., :3].astype(np.int32)
        key = (rgb[..., 0] << 16) | (rgb[..., 1] << 8) | rgb[..., 2]
        a = out[..., 3] > 0
        n, s_, e, w = np.roll(key, 1, 0), np.roll(key, -1, 0), np.roll(key, -1, 1), np.roll(key, 1, 1)
        an = np.roll(a, 1, 0) & np.roll(a, -1, 0) & np.roll(a, -1, 1) & np.roll(a, 1, 1)
        same = (n == s_) & (n == e) & (n == w) & (n != key) & a & an
        same[0, :] = same[-1, :] = False; same[:, 0] = same[:, -1] = False
        ys, xs = np.nonzero(same)
        out[ys, xs, :3] = np.roll(out, 1, 0)[ys, xs, :3]
    return out


def extract_props():
    src = np.asarray(Image.open(SRC_PROPS).convert("RGBA")).astype(np.float32)
    cut = {}
    for name, (box, scale, *_rest) in PROPS.items():
        x, y, w, h = box
        c = src[y:y + h, x:x + w].copy()
        a = (c[..., 3] > 200).astype(np.float32)
        ys, xs = np.nonzero(a)
        c = c[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        a = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        ow, oh = max(4, round(c.shape[1] * scale)), max(4, round(c.shape[0] * scale))
        pre = np.dstack([c[..., :3] * a[..., None], a * 255])          # premultiplied: no halo from the dark matte
        im = Image.fromarray(pre.clip(0, 255).astype(np.uint8)).resize((ow, oh), Image.LANCZOS)
        arr = np.asarray(im).astype(np.float32)
        al = arr[..., 3:4] / 255.0
        rgb = np.where(al > 0.01, arr[..., :3] / np.maximum(al, 1e-3), 0)
        alpha = drop_specks(arr[..., 3] > 110, 14)
        cut[name] = (np.clip(rgb, 0, 255), alpha)
    allpx = np.concatenate([rgb[a] for rgb, a in cut.values()])
    pal = kmeans(allpx[np.random.default_rng(1).choice(len(allpx), min(60000, len(allpx)), replace=False)], 64).astype(np.float32)
    out = {}
    for name, (rgb, alpha) in cut.items():
        flat = rgb.reshape(-1, 3)
        idx = np.argmin(((flat[:, None, :] - pal[None]) ** 2).sum(-1), 1)
        q = pal[idx].reshape(rgb.shape).astype(np.uint8)
        img = np.zeros(rgb.shape[:2] + (4,), np.uint8)
        img[..., :3] = q
        img[..., 3] = np.where(alpha, 255, 0)
        # 1 px dark outline where the silhouette meets transparency (reads on the grass like ref 02)
        al = alpha
        edge = al & ~(np.roll(al, 1, 0) & np.roll(al, -1, 0) & np.roll(al, 1, 1) & np.roll(al, -1, 1))
        edge[0, :] |= al[0, :]; edge[-1, :] |= al[-1, :]; edge[:, 0] |= al[:, 0]; edge[:, -1] |= al[:, -1]
        dark = (img[..., :3].astype(int).sum(-1) < 140)
        img[edge & ~dark, :3] = (img[edge & ~dark, :3] * 0.55).astype(np.uint8)
        out[name] = despeckle(img) if min(img.shape[:2]) >= 24 else img
    return out


# ------------------------------------------------------------------ checks
def check_seams(sheet, shade, name):
    """Seam test (as tools/art/seam_test.py): assemble a random map from the 256 tiles and compare it
    with ONE global render of the same map (global edge field -> same shade function). Any differing
    pixel away from the outer map border is a seam. Returns the mismatch count."""
    rng = np.random.default_rng(9)
    Wm, Hm = 20, 14
    m = (rng.random((Hm, Wm)) < 0.5).astype(np.uint8)
    m[3:9, 4:12] = 1; m[5, 7] = 0

    def mask_at(x, y):
        v = 0
        for _, bit, dx, dy in g.DIRS:
            xx, yy = x + dx, y + dy
            if 0 <= xx < Wm and 0 <= yy < Hm and m[yy, xx]:
                v |= bit
        return v

    asm = np.zeros((Hm * T, Wm * T, 4), np.uint8)
    for y in range(Hm):
        for x in range(Wm):
            if m[y, x]:
                k = mask_at(x, y); r, c = divmod(k, 16)
                asm[y * T:(y + 1) * T, x * T:(x + 1) * T] = sheet[r * T:(r + 1) * T, c * T:(c + 1) * T]
    big = np.kron(m, np.ones((T, T)))
    R, OFF, AMP = FIELD[name]
    t = field_from(big, R, OFF, AMP, (Hm, Wm))
    want = shade(t)
    want[big == 0] = 0                                                # only cells that carry the tile draw
    inner = np.zeros(big.shape, bool); inner[T:-T, T:-T] = True
    bad = int(((want != asm).any(-1) & inner).sum())
    print(f"  seam check {name}: {bad} mismatched pixels of {int(inner.sum())}")
    return bad


def check_open_borders(sheet, name):
    """A side whose neighbour lacks the tile must stay transparent on that border (nothing to meet)."""
    bad = 0
    for mask in range(256):
        r, c = divmod(mask, 16)
        a = sheet[r * T:(r + 1) * T, c * T:(c + 1) * T, 3]
        bad += int(not mask & 1 and a[0, 8:24].any()) + int(not mask & 16 and a[T - 1, 8:24].any())
        bad += int(not mask & 4 and a[8:24, T - 1].any()) + int(not mask & 64 and a[8:24, 0].any())
    print(f"  open-border check {name}: {bad} tiles draw on an open side")
    return bad


def save(arr, name):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(arr).save(os.path.join(OUT, name))
    os.makedirs(os.path.join(ROOT, "art-source/_drafts/moonveil"), exist_ok=True)
    Image.fromarray(arr).save(os.path.join(ROOT, "art-source/_drafts/moonveil", name))


def main():
    global DIRT_FILL, DARK_FILL, PAVER_FILL, WATER_FILL, SHORE_CELLS
    DIRT_FILL = dirt_fill(T, 31)
    DARK_FILL = leafy_fill(T, DARK, 41, 0.6)
    PAVER_FILL = flagstone_fill(T, 51)
    WATER_FILL = water_fill(T, 61)
    SHORE_CELLS = shore_stones()
    items = []

    grass = meadow_fill(128, GRASS, 11)
    save(grass, "TILE_mv_grass_macro.png")
    items.append({"name": "TILE_mv_grass_macro", "path": OUT_REL + "TILE_mv_grass_macro.png", "kind": "periodic", "cols": 4, "rows": 4})
    worst = 0
    for key, shade in (("dark", shade_dark), ("path", shade_path), ("paver", shade_paver), ("water", shade_water)):
        sheet = autotile_sheet(mask_tile(shade, key))
        worst = max(worst, check_seams(sheet, shade, key) + check_open_borders(sheet, key))
        save(sheet, f"TILE_mv_{key}_256.png")
        items.append({"name": f"TILE_mv_{key}_256", "path": OUT_REL + f"TILE_mv_{key}_256.png", "kind": "mask256", "cols": 16, "rows": 16})
    cliff, rim, stairs = recolour_cliff()
    save(cliff, "TILE_mv_cliff.png")
    save(rim, "TILE_mv_rim.png")
    save(stairs, "TILE_mv_stairs.png")
    items.append({"name": "TILE_mv_stairs", "path": OUT_REL + "TILE_mv_stairs.png", "kind": "stairs", "cols": 8, "rows": 1})
    items.append({"name": "TILE_mv_cliff", "path": OUT_REL + "TILE_mv_cliff.png", "kind": "cliff", "cols": 10, "rows": 1})
    items.append({"name": "TILE_mv_rim", "path": OUT_REL + "TILE_mv_rim.png", "kind": "rim", "cols": 8, "rows": 1})
    det = np.concatenate([detail(k, v) for k in range(len(DETAILS)) for v in range(2)], axis=1)
    save(det, "TILE_mv_detail.png")
    items.append({"name": "TILE_mv_detail", "path": OUT_REL + "TILE_mv_detail.png", "kind": "detail", "cols": len(DETAILS) * 2, "rows": 1,
                  "labels": DETAILS})

    props = []
    cut = extract_props()
    drawn = {"bridge_broken": broken_bridge(cut["bridge"]), "bridge_vertical": bridge_vertical(cut["bridge"]), "magic_circle": magic_circle()}
    # code-drawn mechanisms and ground shadows (PROC-DRAFT, tools/art/moonveil_creatures.py)
    mech = {**mc.vine_sprites(), **mc.lever_sprites()}
    shadows = mc.shadow_sprites()
    drawn.update(mech)
    drawn.update(shadows)
    boss_dir = os.path.join(ROOT, "Assets/_Game/Art/Enemies/Draft")
    os.makedirs(boss_dir, exist_ok=True)
    Image.fromarray(mc.boss_frames()).save(os.path.join(boss_dir, "ENM_mushroom_king_draft.png"))  # same 576x96 sheet, purple
    for name, img in list(cut.items()) + list(drawn.items()):
        default_cat = "bridge" if name.startswith("bridge") else "mechanism" if name in mech else "shadow" if name in shadows else "decal"
        box, scale, cat, foot, fade, light = PROPS.get(name, (None, 1, default_cat, None, False, None))
        fname = f"PROP_mv_{name}.png"
        save(img, fname)
        h, w = img.shape[:2]
        # pivot: bottom-centre, lifted to the lowest opaque row of the middle third (trunk / base contact)
        mid = img[:, w // 3: 2 * w // 3, 3] > 0
        rows = np.nonzero(mid.any(1))[0]
        base = int(rows.max()) if len(rows) else h - 1
        pivot_y = max(0, h - 1 - base + (2 if cat in ("tree", "ruin", "camp") else 1))
        if cat in ("decal", "shadow"):
            pivot_y = h // 2                                       # ground decals are centred on their spot
        entry = {"name": f"PROP_mv_{name}", "path": OUT_REL + fname, "w": int(w), "h": int(h), "frames": 1,
                 "pivotX": int(w // 2), "pivotY": int(pivot_y), "category": cat, "fade": bool(fade),
                 "box": foot or [], "light": list(light) if light else []}
        props.append(entry)
        print(f"  {fname:34s} {w:4d}x{h:<4d} {cat}")
    man = {"status": "REF-DRAFT: derived from references/03 (palettes) and 04 (cut props). Replace before release.",
           "tiles": items, "props": props}
    with open(os.path.join(ROOT, "Assets/_Game/Art/Biomes/Moonveil/moonveil_manifest.json"), "w") as f:
        json.dump(man, f, indent=1)
    print(f"tiles {len(items)}, props {len(props)}, seam mismatches {worst}")
    if worst:
        sys.exit("SEAM_TEST FAIL")


if __name__ == "__main__":
    main()
