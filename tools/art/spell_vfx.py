"""Generate the pixel spell flipbooks used by GameContentBuilder.

Usage: python3 tools/art/spell_vfx.py [repo_root]
"""
import json, math, os, sys
import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import spell_shapes as shapes

from pixlib import c, canvas, px, rect, ellipse, shaded_ellipse, poly, line, ring, dither_alpha, polar, strip, outline  # noqa

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.abspath(os.path.join(os.path.dirname(__file__), "../.."))
OUT_DIR = os.path.join(ROOT, "Assets/_Game/Art/VFX/Draft")
MANIFEST = []

FIRE = list(reversed(shapes.FIRE))
SMOKE = [c("#6a5a78"), c("#4a3e5a"), c("#33283f")]
ICE = [c("#ffffff"), c("#d8f6ff"), c("#8ad8f8"), c("#4a9ad8"), c("#2a5aa0")]
BOLT = [c("#ffffff"), c("#fff8c0"), c("#ffe040"), c("#c8a0ff"), c("#7a50e0")]
WIND = [c("#ffffff"), c("#d8fff0"), c("#90e8c8"), c("#50b898"), c("#2e7a6a")]
WHITE = [c("#ffffff"), c("#e8e8f0"), c("#c0c0d0"), c("#9090a8")]


def frames(n, w, h, fn):
    return strip([fn(i / max(n - 1, 1), i, canvas(w, h)) for i in range(n)])


def save(name, img, n, fps, pivot=(0.5, 0.5), variant=False):
    os.makedirs(OUT_DIR, exist_ok=True)
    file_name = name + ("_polish.png" if variant else "_draft.png")
    Image.fromarray(img).save(os.path.join(OUT_DIR, file_name))
    fw, fh = img.shape[1] // n, img.shape[0]
    MANIFEST.append({"name": name if variant else name + "_draft", "path": f"Assets/_Game/Art/VFX/Draft/{file_name}", "w": fw, "h": fh,
                     "frames": n, "pivotX": int(round(pivot[0] * fw)), "pivotY": int(round(pivot[1] * fh)), "fps": fps})


def jag(rr, x0, y0, x1, y1, steps, amp):
    """Jagged polyline points from (x0,y0) to (x1,y1)."""
    pts = [(x0, y0)]
    for k in range(1, steps):
        t = k / steps
        nx, ny = -(y1 - y0), (x1 - x0)
        ln = math.hypot(nx, ny) or 1
        o = rr.uniform(-amp, amp)
        pts.append((x0 + (x1 - x0) * t + nx / ln * o, y0 + (y1 - y0) * t + ny / ln * o))
    pts.append((x1, y1))
    return pts


def polyline(img, pts, col, w=1):
    for (a, b), (cc, d) in zip(pts, pts[1:]):
        line(img, a, b, cc, d, col, w)


# ------------------------------------------------------------------ v2 cel-shading helpers
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


def bayer(h, w):
    return np.tile(BAYER, (h // 4 + 1, w // 4 + 1))[:h, :w]


def blob_field(w, h, cx, cy, R, seed, lobes=((5, 0.13), (8, 0.08), (13, 0.05)), squash=1.0):
    """Normalised distance field of a lumpy blob: < 1 inside. Lobes give an organic, hand-cut silhouette."""
    ys, xs = np.mgrid[0:h, 0:w]
    dx, dy = xs + 0.5 - cx, (ys + 0.5 - cy) / squash
    ang = np.arctan2(dy, dx)
    rr = np.random.default_rng(seed)
    rad = np.ones_like(ang)
    for k, a in lobes:
        rad += a * np.sin(k * ang + rr.uniform(0, math.tau))
    return np.hypot(dx, dy) / (R * rad)


def cel(img, field, ramp, light=(-0.35, -0.4), cx=None, cy=None, R=None, band_dither=True):
    """Fill field<1 with banded cel shading: brightest band toward the light, one dithered pixel row
    between bands (classic pixel-VFX look). Returns the mask."""
    h, w = img.shape[:2]
    m = field < 1
    ys, xs = np.mgrid[0:h, 0:w]
    v = field.copy()
    if cx is not None:                           # shift the hot core toward the light
        v = np.hypot((xs + 0.5 - (cx + light[0] * R)) / R, (ys + 0.5 - (cy + light[1] * R)) / R) * 0.6 + field * 0.3
    n = len(ramp)
    t = np.clip(v, 0, 0.999) * n
    if band_dither:
        t = t + (bayer(h, w) - 0.5) * 0.55
    idx = np.clip(t.astype(int), 0, n - 1)
    for k in range(n):
        img[m & (idx == k)] = ramp[k]
    return m


def layer(w, h):
    return canvas(w, h)


def over(dst, src):
    m = src[:, :, 3] > 0
    dst[m] = src[m]
    return dst


def ease_out(t):
    return 1 - (1 - t) ** 3


def spark(img, x, y, dx, dy, col, tail_col, ln):
    """Chunky 2px spark with a 1px tail streak."""
    line(img, x, y, x - dx * ln, y - dy * ln, tail_col)
    rect(img, x, y, x + 1, y + 1, col)


# ------------------------------------------------------------------ fire blast (96x96, 12 frames) v2
FIRE_OL = c("#3a1018")


def blast(t, i, img):
    W, C = 96, 48
    rr = np.random.default_rng(100)
    if i == 0:                                     # hot contact pin
        ellipse(img, C, C, 6, 6, FIRE[0])
        for k in range(4):
            a = k * math.pi / 2 + math.pi / 4
            x, y = polar(C, C, 22, a)
            line(img, C, C, x, y, FIRE[0], 2)
        for k in range(4):
            x, y = polar(C, C, 12, k * math.pi / 2)
            line(img, C, C, x, y, FIRE[1])
        return img
    if i == 1:                                     # the hot front outruns the core
        f = blob_field(W, W, C, C, 25, 3, ((7, 0.16), (11, 0.08)))
        cel(img, f, [FIRE[0], FIRE[1], FIRE[1], FIRE[2]], cx=C, cy=C, R=25)
        outline(img, FIRE[2])
        outline(img, FIRE[3])
        return img
    k = i - 2                                      # 0..9
    tt = k / 9
    R = 20 + ease_out(min(1, tt * 1.6)) * 16
    lift = tt * 10
    cy = C - lift
    # flame tongues behind the ball, shrinking as it burns out
    if k < 6:
        tong = layer(W, W)
        for j in range(9):
            a = j * math.tau / 9 + 0.3 + rr.uniform(-0.15, 0.15)
            ln = R * (1.45 - k * 0.07) * (0.85 + 0.3 * ((j * 37) % 5) / 4)
            bw = 0.32
            p0 = polar(C, cy, R * 0.6, a - bw)
            p1 = polar(C, cy, ln, a + 0.12)
            p2 = polar(C, cy, R * 0.6, a + bw)
            poly(tong, [p0, p1, p2], FIRE[2] if j % 2 else FIRE[3])
        outline(tong, FIRE_OL)
        over(img, tong)
    # the fireball: hot ramp early, cooling to embers + smoke later
    if k < 7:
        ball = layer(W, W)
        f = blob_field(W, W, C, cy, R, 11 + k // 2)
        heat = min(3, k // 2)
        ramp = [FIRE[0], FIRE[1], FIRE[2], FIRE[3], FIRE[4]][heat:]
        if k >= 4:
            ramp = [FIRE[2], FIRE[3], FIRE[4], SMOKE[0], SMOKE[1]][k - 4:]
        cel(ball, f, ramp, cx=C, cy=cy, R=R)
        if k >= 3:                                 # the ball tears open from the middle
            hole = blob_field(W, W, C + 2, cy + 3, R * (0.15 + (k - 3) * 0.2), 40 + k)
            ball[hole < 1] = 0
        outline(ball, FIRE_OL)
        over(img, ball)
    # smoke puffs rising and dispersing
    if k >= 3:
        sm = layer(W, W)
        for j in range(7):
            a = j * math.tau / 7 + 0.5
            d = R * (0.75 + (k - 3) * 0.08)
            x, y = polar(C, cy, d, a)
            y -= (k - 3) * 2.5
            rad = 9 - (k - 3) * 0.9 + (j % 3)
            if rad > 2:
                cel(sm, blob_field(W, W, x, y, rad, 60 + j, ((4, 0.1),)), SMOKE, cx=x, cy=y, R=rad)
        outline(sm, c("#1e1626"))
        sm[:, :, 3] = (sm[:, :, 3] * (0.62 - max(0, k - 5) * 0.12)).astype(np.uint8)
        sm[img[:, :, 3] > 0] = 0                    # stay behind the ball
        over(img, sm)
    # debris sparks
    for j in range(12):
        a = j * 0.53 + 0.2
        d = R + 6 + tt * 26
        x, y = polar(C, C, d, a)
        y += tt * tt * 14                           # gravity
        if k < 8:
            spark(img, x, y, math.cos(a), math.sin(a), FIRE[1] if k < 4 else FIRE[3], FIRE[3] if k < 4 else FIRE[4], 4 - k * 0.3)
    return dither_alpha(img, 1.0 - max(0, k - 7) * 0.28) if k > 7 else img


# ------------------------------------------------------------------ shockwave ring (64x64, 8 frames, tintable) v2
def shockwave(t, i, img):
    C = 32
    r = 5 + ease_out(t) * 26
    w = max(1.5, 7 - t * 6)
    H = 64
    ys, xs = np.mgrid[0:H, 0:H]
    d = np.hypot(xs + 0.5 - C, (ys + 0.5 - C) / 0.78)       # slightly squashed: reads as a ground wave
    band = (d <= r) & (d >= r - w)
    inner = (d < r - w) & (d >= r - w - 4)
    img[band] = WHITE[1]
    img[(d <= r) & (d >= r - 1.2)] = WHITE[0]                # bright leading edge
    img[(d < r - w + 1.2) & band] = WHITE[2]
    trail = inner & (bayer(H, H) < 0.5 - (d - (r - w - 4)) * -0.08)
    img[trail] = WHITE[3]                                    # dithered wake behind the ring
    if t > 0.55:                                            # breaks into dashes as it fades
        ang = np.arctan2(ys - C, xs - C)
        img[(np.sin(ang * 12 + i) < -0.3)] = 0
    return dither_alpha(img, 1.25 - t * 0.8)


# ------------------------------------------------------------------ star flash / glint (48x48, 5 frames, tintable)
def flash(t, i, img):
    C = 24
    L = [22, 20, 15, 10, 5][i]
    l2 = L * 0.55
    for (dx, dy) in ((1, 0), (0, 1)):              # long cross
        line(img, C - dx * L, C - dy * L, C + dx * L, C + dy * L, WHITE[1], 3 if i < 2 else 1)
        line(img, C - dx * L, C - dy * L, C + dx * L, C + dy * L, WHITE[0], 1)
    for (dx, dy) in ((1, 1), (1, -1)):             # short diagonals
        line(img, C - dx * l2 * 0.7, C - dy * l2 * 0.7, C + dx * l2 * 0.7, C + dy * l2 * 0.7, WHITE[2])
    ellipse(img, C, C, max(1.5, 7 - i * 1.5), max(1.5, 7 - i * 1.5), WHITE[0])
    if i == 0:
        ring(img, C, C, 10, 11, WHITE[1])
    return img


# ------------------------------------------------------------------ sky lightning strike (48x192, 9 frames) v2
def _bolt(img, rr, ground, glow_w, core_w, glow, core, hot, dx=0):
    main = jag(rr, 24 + rr.uniform(-8, 8) + dx, 0, 24 + dx * 0.3, ground, 18, 7)
    branches = []
    for k in range(5):
        st = main[rr.integers(3, 15)]
        ex = st[0] + rr.choice([-1, 1]) * rr.uniform(8, 19)
        branches.append(jag(rr, st[0], st[1], ex, st[1] + rr.uniform(14, 34), 4, 3))
    polyline(img, main, glow, glow_w)
    for b in branches:
        polyline(img, b, glow, max(1, glow_w // 2))
    polyline(img, main, core, core_w)
    for b in branches:
        polyline(img, b, hot, 1)
    polyline(img, main, hot, 1)


def sky_bolt(t, i, img):
    ground = 192 - 14
    rr = np.random.default_rng(7 + i * 13)
    if i == 2:                                     # stepped afterimage
        for k in range(3):
            pts = jag(rr, 24 + rr.uniform(-10, 10), 0, 24 + rr.uniform(-12, 12), 70 + k * 14, 7, 5)
            polyline(img, pts, BOLT[4], 1)
        return img
    if i in (0, 1, 4):                             # main strokes (5 = re-strike, a bit thinner)
        _bolt(img, rr, ground, 9 if i == 0 else 6 if i == 1 else 3, 3, BOLT[3], BOLT[1], BOLT[0], dx=-3 if i == 4 else 0)
    if i == 3:                                     # afterimage
        _bolt(img, np.random.default_rng(20), ground, 3, 1, BOLT[4], BOLT[3], BOLT[3])
    # ground impact
    if i in (0, 1, 4):
        dome = layer(48, 192)
        rad = 17 if i == 0 else 13 if i == 1 else 8
        cel(dome, blob_field(48, 192, 24, ground, rad, 5 + i, ((9, 0.12),), squash=0.42),
            [BOLT[0], BOLT[1], BOLT[2]], band_dither=False)
        outline(dome, BOLT[3])
        over(img, dome)
        polyline(img, jag(rr, 24, ground, 24, ground - 4, 2, 0), BOLT[0], 3)
    elif i >= 3:
        ring_r = 8 + (i - 3) * 3.5
        for k in range(18):
            a = k * math.tau / 18
            x, y = 24 + math.cos(a) * ring_r * 1.15, ground + math.sin(a) * ring_r * 0.42
            px(img, x, y, BOLT[1 if k % 2 else 2])
        for k in range(7):                         # crackles running along the ground
            a = k * math.tau / 7 + i
            pts = jag(rr, 24 + math.cos(a) * 3, ground + math.sin(a) * 1.5,
                      24 + math.cos(a) * (ring_r + 6), ground + math.sin(a) * (ring_r + 6) * 0.42, 3, 2)
            polyline(img, pts, BOLT[2] if i < 6 else BOLT[3])
        if i >= 6:                                 # ozone smoke curling up
            for k in range(4):
                x = 24 + (k - 1.5) * 6
                y = ground - 4 - (i - 6) * 5 - k % 2 * 3
                ellipse(img, x, y, 3, 3, SMOKE[k % 2])
    if i >= 7:
        return dither_alpha(img, 1.0 - (i - 6) * 0.3)
    if i in (0, 1, 3, 4):
        # A narrow halo follows the actual branching stroke, including its bends.
        haze = Image.new("RGBA", (img.shape[1], img.shape[0]), (172, 92, 255, 0))
        haze.putalpha(Image.fromarray(img[:, :, 3]).filter(ImageFilter.GaussianBlur(3.1)))
        return np.asarray(Image.alpha_composite(haze, Image.fromarray(img)))
    return img


# ------------------------------------------------------------------ rotating magic circle (64x64, 8 frames, tintable)
def magic_circle(t, i, img):
    C = 32
    rot = i * math.pi / 16
    ring(img, C, C, 29, 30.5, WHITE[0])
    ring(img, C, C, 24, 25, WHITE[1])
    ring(img, C, C, 12, 13, WHITE[1])
    hexa = [polar(C, C, 24, rot + k * math.pi / 3) for k in range(6)]
    for k in range(6):                             # hexagram
        a, b = hexa[k], hexa[(k + 2) % 6]
        line(img, a[0], a[1], b[0], b[1], WHITE[2])
    for k in range(12):                            # rune ticks in the outer band
        a = -rot * 1.5 + k * math.pi / 6
        x0, y0 = polar(C, C, 25.5, a)
        x1, y1 = polar(C, C, 28.5, a + 0.08)
        line(img, x0, y0, x1, y1, WHITE[0])
        x, y = polar(C, C, 27, a + 0.26)
        px(img, x, y, WHITE[1])
    for k in range(6):
        x, y = polar(C, C, 24, rot + k * math.pi / 3)
        ellipse(img, x, y, 1.6, 1.6, WHITE[0])
    # squash for the top-down ground plane
    out = canvas(64, 64)
    for y in range(64):
        sy = int(C + (y - C) / 0.62)
        if 0 <= sy < 64:
            out[y] = img[sy]
    return out


# ------------------------------------------------------------------ frost nova (96x96, 10 frames) v2
ICE_OL = c("#1a3270")


def _shard(img, bx, by, a, L, w):
    """Faceted ice crystal rising from the ground plane: light left facet, dark right facet, white edge."""
    tx, ty = bx + math.cos(a) * L * 0.45, by + math.sin(a) * L * 0.25 - L
    nx, ny = -math.sin(a) * w, math.cos(a) * w * 0.5
    bl, br = (bx - nx - w * 0.3, by - ny), (bx + nx + w * 0.3, by + ny)
    poly(img, [bl, (tx, ty), (bx, by + 1)], ICE[1])
    poly(img, [(bx, by + 1), (tx, ty), br], ICE[3])
    line(img, bx, by, tx, ty, ICE[0])


def frost_nova(t, i, img):
    W = 96
    C, G = 48, 56                                  # centre / ground line of the nova
    rr = np.random.default_rng(31)
    if i == 0:
        cel(img, blob_field(W, W, C, G, 20, 2, ((6, 0.14),)), [ICE[0], ICE[0], ICE[1]], band_dither=False)
        outline(img, ICE[2])
        return img
    k = i - 1                                      # 0..8
    grow = ease_out(min(1, (k + 1) / 4))
    # frozen ground patch
    patch = layer(W, W)
    pr = 14 + grow * 26
    cel(patch, blob_field(W, W, C, G, pr, 9, ((7, 0.12), (11, 0.06)), squash=0.5), [ICE[2], ICE[3], ICE[4]])
    outline(patch, ICE[1])
    over(img, dither_alpha(patch, 1.4 - max(0, k - 4) * 0.25, seed=1))
    if k <= 4:                                     # crystals burst out, back row first
        sh = layer(W, W)
        angs = sorted([j * math.tau / 10 + rr.uniform(-0.2, 0.2) for j in range(10)], key=lambda a: math.sin(a))
        for j, a in enumerate(angs):
            d = pr * (0.45 + 0.35 * ((j * 7) % 4) / 3)
            bx, by = C + math.cos(a) * d, G + math.sin(a) * d * 0.5
            L = (14 + (j % 3) * 6) * grow
            _shard(sh, bx, by, a, L, 3 + (j % 2))
        _shard(sh, C, G, -math.pi / 2, 26 * grow, 5)   # centre spire
        outline(sh, ICE_OL)
        over(img, sh)
    else:                                          # shatter: shards become fragments flying outward
        f = (k - 4) / 4
        for j in range(26):
            a = j * 0.7 + rr.uniform(0, 0.3)
            d = 10 + f * 34 + (j % 5) * 3
            x, y = C + math.cos(a) * d, G - 12 + math.sin(a) * d * 0.6 + f * f * 18
            col = ICE[j % 3]
            poly(img, [(x, y - 2), (x + 2, y), (x, y + 2), (x - 1, y)], col)
        img = dither_alpha(img, 1.2 - f * 0.9, seed=3)
    # frost glints
    for j in range(10):
        a = rr.uniform(0, math.tau)
        x, y = C + math.cos(a) * (pr + rr.uniform(0, 8)), G + math.sin(a) * (pr + rr.uniform(0, 8)) * 0.5 - rr.uniform(0, 20)
        if (j + k) % 3 == 0:
            line(img, x - 2, y, x + 2, y, ICE[0]); line(img, x, y - 2, x, y + 2, ICE[0])
    return img


# ------------------------------------------------------------------ wind blades (64x64, 8 frames) v2
def wind_blades(t, i, img):
    C = 32
    H = 64
    ys, xs = np.mgrid[0:H, 0:H]
    dx, dy = xs + 0.5 - C, (ys + 0.5 - C) / 0.72
    d = np.hypot(dx, dy)
    ang = np.arctan2(dy, dx)
    spin = i * 0.62
    r = 12 + ease_out(t) * 12
    for k in range(3):                             # three crescent slashes: thick head, tapering tail
        base = k * math.tau / 3 + spin
        rel = (ang - base) % math.tau              # 0 at head, growing along the tail
        span = 1.35
        on = rel < span
        thick = np.where(on, 4.0 * np.clip(1 - rel / span, 0, 1) ** 0.7 + 0.4, 0)
        m = on & (np.abs(d - r) < thick)
        tpos = rel / span
        img[m & (tpos > 0.6)] = WIND[3]
        img[m & (tpos <= 0.6)] = WIND[2]
        img[m & (tpos <= 0.3)] = WIND[1]
        img[m & (tpos <= 0.3) & (d - r > thick * 0.2)] = WIND[0]   # bright outer cutting edge
    tail = (img[:, :, 3] > 0) & (img[:, :, 0] == WIND[3][0]) & (bayer(H, H) > 0.55)
    img[tail] = 0
    outline(img, c("#1e4e46"))
    for k in range(6):                             # speed lines and leaves
        a = k * 1.05 + spin * 1.3
        x, y = C + math.cos(a) * (r + 9), C + math.sin(a) * (r + 9) * 0.72
        x2, y2 = C + math.cos(a + 0.35) * (r + 9), C + math.sin(a + 0.35) * (r + 9) * 0.72
        line(img, x, y, x2, y2, WIND[1] if k % 2 else WIND[2])
        if k % 2 == 0:
            rect(img, x2, y2, x2 + 1, y2, c("#7ac860")); px(img, x2 + 2, y2 - 1, c("#4a9a48"))
    return dither_alpha(img, 1.3 - t * 0.7) if t > 0.6 else img


# ------------------------------------------------------------------ scorch mark (48x32, 6 frames, slow fade)
def scorch(t, i, img):
    rr = np.random.default_rng(5)
    ellipse(img, 24, 16, 20, 11, c("#2a1a24", 200))
    ellipse(img, 24, 16, 14, 7, c("#1a1018", 220))
    for k in range(10):
        a = k * 0.63
        x, y = 24 + math.cos(a) * rr.uniform(14, 22), 16 + math.sin(a) * rr.uniform(7, 12)
        px(img, x, y, c("#2a1a24", 200))
    if i < 3:                                       # glowing embers cool down
        for k in range(8):
            x, y = 24 + rr.uniform(-14, 14), 16 + rr.uniform(-6, 6)
            px(img, x, y, FIRE[2] if i == 0 else FIRE[3] if i == 1 else FIRE[4])
    return dither_alpha(img, 1.25 - t)


# ------------------------------------------------------------------ soft light bloom (32x32, 1 frame, tintable)
def bloom(t, i, img):
    for r, a in ((16, 30), (13, 55), (10, 90), (7, 140), (4, 210)):
        ellipse(img, 16, 16, r, r, c("#ffffff", a))
    return img


def glow(t, i, img):
    y, x = np.mgrid[0:32, 0:32]
    r = np.hypot(x - 15.5, y - 15.5) / 15.5
    img[:, :, :3] = 255
    img[:, :, 3] = (255 * np.maximum(0, 1 - r ** 1.4) ** 3).astype(np.uint8)
    return img


def main():
    save("VFX_blast", frames(12, 96, 96, blast), 12, 22)
    save("VFX_shockwave", frames(8, 64, 64, shockwave), 8, 24)
    save("VFX_flash", frames(5, 48, 48, flash), 5, 24)
    save("VFX_sky_bolt", frames(9, 48, 192, sky_bolt), 9, 22, pivot=(0.5, 14 / 192))
    save("VFX_magic_circle", frames(8, 64, 64, magic_circle), 8, 16)
    save("VFX_frost_nova", frames(10, 96, 96, frost_nova), 10, 20, pivot=(0.5, 1 - 56 / 96))
    save("VFX_wind_blades", frames(8, 64, 64, wind_blades), 8, 18)
    save("VFX_scorch", frames(6, 48, 32, scorch), 6, 5)
    save("VFX_bloom", frames(1, 32, 32, bloom), 1, 1)
    variants = [
        ("spell_glow", 1, 32, 32, 1, glow, (0.5, 0.5)),
        ("fireball", 8, 48, 36, 24, shapes.fireball, (0.63, 0.5)),
        ("ice_shard", 8, 56, 36, 24, shapes.ice_shard, (0.52, 0.5)),
        ("frost_blade", 1, 52, 40, 1, shapes.orbit_blade, (0.5, 0.5)),
        ("homing", 12, 52, 40, 24, shapes.star_projectile(shapes.ARCANE, "diamond"), (0.5, 0.5)),
        ("starfire", 12, 52, 40, 24, shapes.star_projectile(shapes.FIRE, "star"), (0.5, 0.5)),
        ("magic_bolt", 12, 60, 40, 24, shapes.star_projectile(shapes.ARCANE, "charge"), (0.53, 0.5)),
        ("charge", 12, 64, 56, 20, shapes.sigil(shapes.ARCANE, True), (0.5, 0.5)),
        ("slash", 9, 96, 80, 27, shapes.staff_slash, (31/96, 0.5)),
        ("spell_aura", 12, 104, 66, 18, shapes.sigil(shapes.VOLT, True), (0.5, 0.5)),
        ("vortex", 16, 96, 64, 24, shapes.wind_ground, (0.5, 35/64)),
        ("wind_funnel", 16, 112, 96, 24, shapes.funnel(shapes.WIND, False), (0.5, 15/96)),
        ("fire_tornado", 16, 120, 112, 24, shapes.funnel(shapes.FIRE, True), (0.5, 15/112)),
        ("spell_pulse", 10, 96, 76, 26, shapes.arc_burst(shapes.VOLT, "pulse"), (0.5, 25/76)),
        ("steam", 12, 112, 96, 22, shapes.arc_burst(shapes.ICE, "steam"), (0.5, 32/96)),
        ("overload", 12, 112, 96, 24, shapes.arc_burst(shapes.FIRE, "overload"), (0.5, 32/96)),
        ("shatter", 12, 112, 96, 24, shapes.arc_burst(shapes.ICE, "shards"), (0.5, 32/96)),
    ]
    for name, n, w, h, fps, draw, pivot in variants:
        save("VFX_" + name, frames(n, w, h, draw), n, fps, pivot, variant=True)
    with open(os.path.join(ROOT, "Assets/_Game/Art/spell_vfx_manifest.json"), "w") as f:
        json.dump({"items": MANIFEST}, f, indent=1)
    print(len(MANIFEST), "spell vfx flipbooks")


if __name__ == "__main__":
    main()
