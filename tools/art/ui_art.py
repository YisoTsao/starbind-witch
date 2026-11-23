"""Generate DRAFT pixel UI art: 9-slice frames (panel, slot, bar, keycap), bar fills, map markers and
the 32x32 skill / item icons (replacing the 16x16 ICON_* of gen_game_drafts.py).

Status of every output: Needs Cleanup (procedural draft, not production art; ASSET_GAPS.md).
Palette: witch violet + gold (ART_DIRECTION.md), shared dark outline. Drawn for an integer 3x UI scale
(1 art px = 3 UI px at the 1920x1080 reference canvas; UIKit.Skin sets the multiplier).

Outputs: Assets/_Game/Art/UI/Draft/{UI_*,ICON_*}_draft.png and Assets/_Game/Art/ui_manifest.json
(read by Starbind.Editor.GameContentBuilder; `border` = 9-slice border in px).

Usage: python3 tools/art/ui_art.py <repo_root>
"""
import json, math, os, sys
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixlib import c, canvas, px, rect, ellipse, shaded_ellipse, poly, line, outline, ring, polar  # noqa

OUT = c("#1a1226")
GOLD = [c("#fff4c2"), c("#f6d27a"), c("#d9a248"), c("#a46d2c"), c("#6a4220")]
VIO = [c("#efe0ff"), c("#c9a2f2"), c("#9b6ad8"), c("#6c43ad"), c("#452a78"), c("#2a1a4a")]
NIGHT = [c("#2a1f44"), c("#21183a"), c("#1a1230"), c("#140e26"), c("#0e091c")]
FIRE = [c("#fffbe0"), c("#ffe27a"), c("#ffa938"), c("#f06424"), c("#b0301c"), c("#6e1a18")]
ICE = [c("#ffffff"), c("#dcf7ff"), c("#9ee0fa"), c("#5aa8e0"), c("#3466b0"), c("#22407a")]
BOLT = [c("#ffffff"), c("#fffab0"), c("#ffe24a"), c("#e0a92a"), c("#b98aff")]
WIND = [c("#ffffff"), c("#ddfff2"), c("#9aeed0"), c("#56c0a0"), c("#2f8a78")]
ARC = [c("#ffffff"), c("#f2dcff"), c("#cf96ff"), c("#9a5ae8"), c("#6432b4"), c("#3e1e78")]
WOOD = [c("#e8b878"), c("#c08850"), c("#8e5c36"), c("#5e3a26")]
MANIFEST = []


def save(name, img, border=0, pivot=(0.5, 0.5)):
    d = os.path.join(ROOT, "Assets/_Game/Art/UI/Draft")
    os.makedirs(d, exist_ok=True)
    Image.fromarray(img).save(os.path.join(d, name + ".png"))
    h, w = img.shape[:2]
    MANIFEST.append({"name": name, "path": f"Assets/_Game/Art/UI/Draft/{name}.png", "w": w, "h": h, "frames": 1,
                     "pivotX": int(round(pivot[0] * w)), "pivotY": int(round(pivot[1] * h)), "fps": 0, "border": border})


# ------------------------------------------------------------------ helpers
def halo(img, col, dither=True):
    """One-pixel soft glow around the opaque shape (checker-dithered so it stays pixel art)."""
    a = img[:, :, 3] > 0
    n = np.zeros_like(a)
    n[1:, :] |= a[:-1, :]; n[:-1, :] |= a[1:, :]; n[:, 1:] |= a[:, :-1]; n[:, :-1] |= a[:, 1:]
    ring_ = n & ~a
    if dither:
        ys, xs = np.mgrid[0:img.shape[0], 0:img.shape[1]]
        ring_ &= (xs + ys) % 2 == 0
    img[ring_] = col
    return img


def sparkle(img, x, y, col=c("#ffffff"), r=1):
    px(img, x, y, col)
    for d in range(1, r + 1):
        for dx, dy in ((d, 0), (-d, 0), (0, d), (0, -d)):
            px(img, x + dx, y + dy, col if d < r else c("#ffffff", 160))


def shade_mask(img, m, ramp, light=(-0.7, -0.7)):
    ys, xs = np.nonzero(m)
    if len(xs) == 0:
        return
    cx, cy = xs.mean(), ys.mean()
    rx = max(xs.max() - xs.min(), 1) / 2 + 0.5
    ry = max(ys.max() - ys.min(), 1) / 2 + 0.5
    d = -(((xs - cx) / rx) * light[0] + ((ys - cy) / ry) * light[1])
    idx = np.clip(((d + 1) / 2 * len(ramp)).astype(int), 0, len(ramp) - 1)
    for i, col in enumerate(ramp):
        s = idx == i
        img[ys[s], xs[s]] = col


def mask_poly(w, h, pts):
    tmp = canvas(w, h)
    return poly(tmp, pts, c("#ffffff"))


def mask_ellipse(w, h, cx, cy, rx, ry):
    ys, xs = np.mgrid[0:h, 0:w]
    return ((xs + 0.5 - cx) / rx) ** 2 + ((ys + 0.5 - cy) / ry) ** 2 <= 1


def finish(img, glow=None):
    outline(img, OUT)
    if glow is not None:
        halo(img, glow)
    return img


# ------------------------------------------------------------------ icons (32x32)
S = 32


def comet(img, cx, cy, r, ramp, tail_dir=(-1, 1), tail=9):
    """Fireball / missile: shaded head plus a flame tail of three tongues."""
    tx, ty = tail_dir
    n = math.hypot(tx, ty); tx, ty = tx / n, ty / n
    px_, py_ = -ty, tx
    for k, (off, ln) in enumerate(((-0.55, 0.75), (0.0, 1.0), (0.55, 0.7))):
        base_l = (cx + px_ * r * off * 1.2, cy + py_ * r * off * 1.2)
        tip = (cx + tx * tail * ln + px_ * off * 2, cy + ty * tail * ln + py_ * off * 2)
        a = (base_l[0] + px_ * r * 0.55, base_l[1] + py_ * r * 0.55)
        b = (base_l[0] - px_ * r * 0.55, base_l[1] - py_ * r * 0.55)
        poly(img, [a, tip, b], ramp[3 if k != 1 else 2])
        poly(img, [((a[0] + base_l[0]) / 2, (a[1] + base_l[1]) / 2), ((tip[0] + base_l[0]) / 2, (tip[1] + base_l[1]) / 2),
                   ((b[0] + base_l[0]) / 2, (b[1] + base_l[1]) / 2)], ramp[2 if k != 1 else 1])
    shaded_ellipse(img, cx, cy, r, r, ramp[:5])
    px(img, cx - r * 0.4, cy - r * 0.45, ramp[0]); px(img, cx - r * 0.4 + 1, cy - r * 0.45, ramp[0])


def icon_fire_bolt():
    img = canvas(S, S)
    comet(img, 19, 13, 7, FIRE, (-1, 1), 15)
    for x, y in ((25, 22), (8, 9), (27, 6)):
        px(img, x, y, FIRE[1])
    return finish(img, FIRE[4])


def icon_split_flame():
    img = canvas(S, S)
    for (x, y, r, d) in ((16, 9, 5, (0, 1)), (8, 21, 4, (1, -0.6)), (24, 21, 4, (-1, -0.6))):
        comet(img, x, y, r, FIRE, d, 7)
    ellipse(img, 16, 18, 2, 2, FIRE[0])
    return finish(img, FIRE[4])


def icon_piercing_ice():
    img = canvas(S, S)
    m = mask_poly(S, S, [(29, 2), (20, 8), (4, 28), (6, 30), (24, 12)])
    ys, xs = np.nonzero(m)
    img[ys, xs] = ICE[3]
    side = (xs + ys) < 32
    img[ys[side], xs[side]] = ICE[1]
    img[ys[~side & ((xs + ys) < 35)], xs[~side & ((xs + ys) < 35)]] = ICE[2]
    line(img, 28, 3, 7, 27, ICE[0])
    for x, y in ((10, 10), (22, 24), (6, 16)):   # frost shards
        poly(img, [(x, y - 2), (x + 1.5, y), (x, y + 2), (x - 1.5, y)], ICE[2])
        px(img, x, y - 1, ICE[0])
    sparkle(img, 27, 4, ICE[0], 2)
    return finish(img, ICE[4])


def icon_chain_spark():
    img = canvas(S, S)
    pts = [(19, 1), (10, 13), (17, 14), (8, 30), (24, 11), (16, 11), (24, 1)]
    poly(img, pts, BOLT[2])
    poly(img, [(19, 3), (13, 12), (18, 13), (12, 24), (21, 12), (15, 11), (21, 3)], BOLT[1])
    line(img, 18, 4, 14, 11, BOLT[0])
    for x, y in ((5, 8), (27, 22)):   # chained orbs
        shaded_ellipse(img, x, y, 3, 3, [BOLT[0], BOLT[1], BOLT[2], BOLT[3]])
    line(img, 7, 10, 10, 13, BOLT[4]); line(img, 25, 20, 21, 16, BOLT[4])
    return finish(img, BOLT[4])


def icon_wind_pull():
    img = canvas(S, S)
    for k, (r0, col) in enumerate(((12, WIND[3]), (8.5, WIND[2]), (5, WIND[1]))):
        for t in np.linspace(0, math.pi * 1.45, 60):
            a = t + k * 2.1
            r = r0 - t * 1.1
            x, y = 16 + math.cos(a) * r, 16 + math.sin(a) * r * 0.9
            px(img, x, y, col); px(img, x + 0.6, y + 0.6, col)
    ellipse(img, 16, 16, 2, 2, WIND[0])
    for x, y in ((25, 6), (6, 25)):   # leaves
        poly(img, [(x, y - 2), (x + 3, y), (x, y + 2), (x - 1, y)], c("#7fcf6a")); px(img, x, y, c("#c9f29a"))
    return finish(img, WIND[4])


def icon_frost_orbit():
    img = canvas(S, S)
    ys, xs = np.mgrid[0:S, 0:S]
    d = ((xs + 0.5 - 16) / 12.5) ** 2 + ((ys + 0.5 - 16) / 9) ** 2
    img[(d <= 1) & (d >= 0.78)] = ICE[3]
    img[(d <= 0.86) & (d >= 0.8) & (ys < 16)] = ICE[2]
    shaded_ellipse(img, 16, 16, 4.5, 4.5, ICE[:5])
    for a in (-0.6, 1.5, 3.6):
        x, y = 16 + math.cos(a) * 12, 16 + math.sin(a) * 8.6
        poly(img, [(x, y - 4), (x + 2.6, y), (x, y + 4), (x - 2.6, y)], ICE[2])
        poly(img, [(x, y - 3), (x + 1, y), (x, y + 2), (x - 1.5, y)], ICE[1])
        px(img, x - 1, y - 1, ICE[0])
    sparkle(img, 14, 14, ICE[0], 1)
    return finish(img, ICE[4])


def icon_thunder_pulse():
    img = canvas(S, S)
    ring(img, 16, 16, 12, 13.5, BOLT[3])
    ring(img, 16, 16, 8.5, 9.5, BOLT[2])
    poly(img, [(18, 4), (10, 17), (15, 17), (12, 28), (22, 13), (17, 13), (21, 4)], BOLT[1])
    line(img, 18, 6, 13, 15, BOLT[0])
    for a in range(8):
        x, y = polar(16, 16, 14.5, a * math.pi / 4)
        px(img, x, y, BOLT[1])
    return finish(img, BOLT[4])


def icon_homing():
    img = canvas(S, S)
    for (x, y, r) in ((7, 25, 3.2), (16, 16, 3.8), (25, 7, 4.4)):
        for k in range(6):   # curved trail
            tx, ty = x - 2 - k * 1.2, y + 1 + k * 0.6 - (k * k) * 0.12
            px(img, tx, ty, ARC[3] if k < 3 else ARC[4])
        shaded_ellipse(img, x, y, r, r, ARC[:5])
        px(img, x - 1, y - 1, ARC[0])
    return finish(img, ARC[5])


def icon_starfire():
    img = canvas(S, S)
    pts = [polar(16, 16.5, 14 if k % 2 == 0 else 6, -math.pi / 2 + k * math.pi / 5) for k in range(10)]
    m = mask_poly(S, S, pts)
    shade_mask(img, m, [GOLD[0], GOLD[1], GOLD[1], GOLD[2], GOLD[3]])
    inner = [polar(16, 16.5, 8 if k % 2 == 0 else 3.5, -math.pi / 2 + k * math.pi / 5) for k in range(10)]
    poly(img, inner, GOLD[0])
    for x, y in ((5, 5), (27, 26), (28, 8)):
        sparkle(img, x, y, c("#ffffff"), 1)
    return finish(img, GOLD[3])


def icon_overdrive():
    img = canvas(S, S)
    ring(img, 16, 16, 11.5, 13.5, ARC[3])
    ring(img, 16, 16, 12.2, 12.8, ARC[2])
    for k in range(8):   # runes on the ring
        x, y = polar(16, 16, 12.5, k * math.pi / 4 + 0.39)
        px(img, x, y, ARC[0])
    pts = [polar(16, 16, 10 if k % 2 == 0 else 3.5, -math.pi / 2 + k * math.pi / 8) for k in range(16)]
    m = mask_poly(S, S, pts)
    shade_mask(img, m, [c("#ffffff"), GOLD[0], GOLD[1], GOLD[2]])
    ellipse(img, 16, 16, 2.5, 2.5, c("#ffffff"))
    return finish(img, ARC[5])


def icon_staff():
    img = canvas(S, S)
    line(img, 5, 29, 19, 12, WOOD[2], 3)   # shaft
    line(img, 4, 28, 18, 11, WOOD[1], 1)
    line(img, 6, 29, 20, 13, WOOD[3], 1)
    for k in (8, 14):   # wrappings
        x, y = 5 + k * 14 / 17, 29 - k
        line(img, x - 1, y - 1, x + 2, y + 1, VIO[2], 1)
    rect(img, 17, 11, 20, 14, GOLD[2]); px(img, 17, 11, GOLD[0])   # ferrule
    # crescent moon head cradling an orb
    m = mask_ellipse(S, S, 23, 8, 7, 7) & ~mask_ellipse(S, S, 26, 6, 6, 6)
    shade_mask(img, m, [GOLD[0], GOLD[1], GOLD[2], GOLD[3]])
    shaded_ellipse(img, 24.5, 7.5, 3.5, 3.5, ARC[:5])
    px(img, 23, 6, c("#ffffff"))
    sparkle(img, 29, 15, ARC[1], 1)
    return finish(img, ARC[5])


def icon_dash():
    img = canvas(S, S)
    # winged boot
    m = mask_poly(S, S, [(14, 8), (20, 8), (20, 20), (27, 22), (28, 26), (13, 26)])
    shade_mask(img, m, [c("#b07a58"), c("#8a5a40"), c("#6a4230"), c("#4a2c22")])
    rect(img, 13, 24, 28, 26, c("#3a2420")); rect(img, 14, 9, 19, 10, GOLD[1])
    for k, (y, ln) in enumerate(((9, 8), (13, 10), (17, 8))):   # wing feathers
        poly(img, [(14, y), (14 - ln, y + 1 - k), (14 - ln + 2, y + 3), (14, y + 3)], [c("#ffffff"), c("#e4ecff"), c("#c4cfee")][k])
    for y, x0 in ((21, 2), (25, 4), (29, 7)):   # speed lines
        line(img, x0, y, x0 + 6, y, WIND[2])
    return finish(img, WIND[4])


def icon_potion(kind):
    liq = {"hp": [c("#ffb0b8"), c("#ff5a6a"), c("#d02a44"), c("#8a1430")],
           "mp": [c("#b8d8ff"), c("#5a9aff"), c("#2a5ad0"), c("#18308a")]}[kind]
    img = canvas(S, S)
    body = mask_ellipse(S, S, 16, 20, 10, 9.5)
    glass = c("#e8f0ff", 255)
    img[body] = c("#cfd8f0")
    liquid = body & mask_ellipse(S, S, 16, 20.5, 8.6, 8.2)
    ys, xs = np.mgrid[0:S, 0:S]
    liquid &= ys >= 15
    shade_mask(img, liquid, liq)
    for x, y in ((12, 22), (18, 25), (20, 19)):   # bubbles
        px(img, x, y, liq[0])
    rect(img, 13, 6, 18, 11, c("#dfe6f6")); rect(img, 13, 6, 13, 11, glass)   # neck
    rect(img, 12, 10, 19, 11, c("#b8c2dc"))
    rect(img, 13, 2, 18, 6, WOOD[1]); rect(img, 13, 2, 14, 6, WOOD[0]); rect(img, 17, 2, 18, 6, WOOD[2])   # cork
    line(img, 10, 15, 9, 22, c("#ffffff"))   # glass highlight
    px(img, 11, 14, c("#ffffff"))
    return finish(img, liq[3])


ICONS = {
    "fire_bolt": icon_fire_bolt, "split_flame": icon_split_flame, "piercing_ice": icon_piercing_ice,
    "chain_spark": icon_chain_spark, "wind_pull": icon_wind_pull, "frost_orbit": icon_frost_orbit,
    "thunder_pulse": icon_thunder_pulse, "homing": icon_homing, "starfire": icon_starfire,
    "overdrive": icon_overdrive, "staff": icon_staff, "dash": icon_dash,
    "potion_hp": lambda: icon_potion("hp"), "potion_mp": lambda: icon_potion("mp"),
}


def icons():
    return {"ICON_" + k: fn() for k, fn in ICONS.items()}


# ------------------------------------------------------------------ 9-slice frames
def vgrad(img, x0, y0, x1, y1, ramp):
    h = y1 - y0 + 1
    for y in range(y0, y1 + 1):
        t = (y - y0) / max(h - 1, 1)
        img[y, x0:x1 + 1] = ramp[min(int(t * len(ramp)), len(ramp) - 1)]


def corner_gem(img, x, y):
    for dx, dy, col in ((0, -2, GOLD[1]), (-1, -1, GOLD[1]), (0, -1, GOLD[0]), (1, -1, GOLD[2]), (-2, 0, GOLD[1]), (-1, 0, VIO[1]),
                        (0, 0, VIO[0]), (1, 0, VIO[2]), (2, 0, GOLD[3]), (-1, 1, GOLD[2]), (0, 1, VIO[3]), (1, 1, GOLD[3]), (0, 2, GOLD[3])):
        px(img, x + dx, y + dy, col)


def panel(w=48, h=48, gems=True):
    img = canvas(w, h)
    vgrad(img, 0, 0, w - 1, h - 1, [NIGHT[0], NIGHT[1], NIGHT[1], NIGHT[2], NIGHT[2], NIGHT[3]])
    # (no texture in the centre: the 9-slice centre is stretched, a pattern would turn into blotches)
    # frame: dark rim, gold double border, inner violet line
    img[0, :] = OUT; img[-1, :] = OUT; img[:, 0] = OUT; img[:, -1] = OUT
    img[1, 1:-1] = GOLD[1]; img[1:-1, 1] = GOLD[1]
    img[-2, 1:-1] = GOLD[3]; img[1:-1, -2] = GOLD[3]
    img[2, 2:-2] = GOLD[2]; img[2:-2, 2] = GOLD[2]
    img[-3, 2:-2] = GOLD[4]; img[2:-2, -3] = GOLD[4]
    img[3, 3:-3] = OUT; img[3:-3, 3] = OUT; img[-4, 3:-3] = OUT; img[3:-3, -4] = OUT
    img[4, 4:-4] = VIO[4]; img[4:-4, 4] = VIO[4]
    img[1, 1] = GOLD[0]; img[1, 2] = GOLD[0]; img[2, 1] = GOLD[0]
    if gems:
        for x, y in ((4, 4), (w - 5, 4), (4, h - 5), (w - 5, h - 5)):
            corner_gem(img, x, y)
    return img


def slot(w=32, h=32):
    img = canvas(w, h)
    vgrad(img, 0, 0, w - 1, h - 1, [NIGHT[3], NIGHT[2], NIGHT[2], NIGHT[1]])
    img[0, :] = OUT; img[-1, :] = OUT; img[:, 0] = OUT; img[:, -1] = OUT
    img[1, 1:-1] = GOLD[1]; img[1:-1, 1] = GOLD[1]; img[-2, 1:-1] = GOLD[3]; img[1:-1, -2] = GOLD[3]
    img[2, 2:-2] = GOLD[3]; img[2:-2, 2] = GOLD[3]; img[-3, 2:-2] = GOLD[2]; img[2:-2, -3] = GOLD[2]   # bevel inward
    img[3, 3:-3] = OUT; img[3:-3, 3] = OUT
    img[4, 4:-4] = NIGHT[4]                                                                          # inner shadow
    for x, y in ((2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3)):   # rivets
        img[y, x] = GOLD[0]
    return img


def bar_frame(w=32, h=12):
    img = canvas(w, h)
    img[1:-1, 1:-1] = c("#0c0816")
    img[0, 2:-2] = OUT; img[-1, 2:-2] = OUT; img[2:-2, 0] = OUT; img[2:-2, -1] = OUT
    img[1, 1] = OUT; img[1, -2] = OUT; img[-2, 1] = OUT; img[-2, -2] = OUT
    img[1, 2:-2] = GOLD[2]; img[-2, 2:-2] = GOLD[4]; img[2:-2, 1] = GOLD[2]; img[2:-2, -2] = GOLD[4]
    img[2, 2:-2] = c("#05030a")   # inner top shadow
    return img


def bar_fill(ramp):
    """6 x 8 vertical gradient, used as a horizontally Filled image: highlight row, body, shadow."""
    img = canvas(6, 8)
    rows = [ramp[0], ramp[1], ramp[1], ramp[2], ramp[2], ramp[2], ramp[3], ramp[4]]
    for y, col in enumerate(rows):
        img[y, :] = col
    img[1, ::3] = ramp[0]
    return img


def keycap(w=16, h=12):
    img = canvas(w, h)
    img[1:-2, 1:-1] = c("#2e2448")
    img[1, 1:-1] = c("#4a3c6e")
    img[-2, 1:-1] = c("#18122a")
    img[0, 1:-1] = GOLD[2]; img[-1, 1:-1] = GOLD[4]; img[1:-1, 0] = GOLD[2]; img[1:-1, -1] = GOLD[4]
    return img


def ornament_moon():
    img = canvas(22, 12)
    m = mask_ellipse(22, 12, 11, 6, 5, 5) & ~mask_ellipse(22, 12, 13, 5, 4.4, 4.4)
    shade_mask(img, m, [GOLD[0], GOLD[1], GOLD[2]])
    for x, y in ((3, 6), (19, 6)):
        sparkle(img, x, y, GOLD[1], 1)
    line(img, 5, 6, 7, 6, GOLD[2]); line(img, 15, 6, 17, 6, GOLD[2])
    return finish(img)


# ------------------------------------------------------------------ map markers
def marker_player():
    img = canvas(11, 11)
    poly(img, [(5.5, 0.5), (10, 10), (5.5, 7.5), (1, 10)], GOLD[1])
    poly(img, [(5.5, 2), (8, 8), (5.5, 6.5)], GOLD[0])
    return finish(img)


def marker_dot(col, light, r=2.6, size=7):
    img = canvas(size, size)
    shaded_ellipse(img, size / 2, size / 2, r, r, [light, col, col])
    return finish(img)


def marker_star():
    img = canvas(11, 11)
    poly(img, [polar(5.5, 5.8, 5 if k % 2 == 0 else 2, -math.pi / 2 + k * math.pi / 5) for k in range(10)], GOLD[1])
    px(img, 5, 4, GOLD[0])
    return finish(img)


def marker_door():
    img = canvas(9, 11)
    rect(img, 1, 3, 7, 10, WOOD[1]); rect(img, 2, 2, 6, 2, WOOD[1]); rect(img, 3, 1, 5, 1, WOOD[1])
    rect(img, 4, 2, 4, 10, WOOD[3]); px(img, 5, 7, GOLD[0])
    return finish(img)


# ------------------------------------------------------------------ upgrade cards / grimoire (2026-11-21)
def card(w=48, h=64):
    """Level-up card: night body with a faint lighter top band, gold double frame, filigree corners."""
    img = panel(w, h, gems=False)
    for x, y, sx, sy in ((5, 5, 1, 1), (w - 6, 5, -1, 1), (5, h - 6, 1, -1), (w - 6, h - 6, -1, -1)):
        for k in range(4):                       # L-shaped filigree with a curl
            px(img, x + sx * k, y, GOLD[1 if k < 2 else 2]); px(img, x, y + sy * k, GOLD[1 if k < 2 else 2])
        px(img, x + sx, y + sy, GOLD[0]); px(img, x + sx * 2, y + sy * 2, GOLD[3])
    return img


def banner(w=24, h=10):
    """White ribbon (tinted at runtime by card kind) with notched ends and a shading row."""
    img = canvas(w, h)
    img[1:-1, :] = c("#f4f0ff"); img[-2, :] = c("#b8b0cc"); img[1, :] = c("#ffffff")
    img[0, 2:-2] = OUT; img[-1, 2:-2] = OUT
    for y in range(h):                           # notches
        d = abs(y - (h - 1) / 2)
        if d < 2:
            img[y, 0:2] = 0; img[y, -2:] = 0
    img[1:-1, 2] = c("#d8d0ea"); img[1:-1, -3] = c("#d8d0ea")
    return img


def divider(w=64, h=7):
    img = canvas(w, h)
    m = h // 2
    for x in range(4, w - 4):
        img[m, x] = GOLD[2] if (x // 2) % 2 else GOLD[3]
    img[m - 1, 6:w - 6] = 0
    poly(img, [(w / 2, 0), (w / 2 + 3.5, m + 0.5), (w / 2, h), (w / 2 - 3.5, m + 0.5)], GOLD[1])
    px(img, w // 2, m - 1, GOLD[0])
    for x in (3, w - 4):
        px(img, x, m, GOLD[1]); px(img, x - 1, m, GOLD[3]); px(img, x + 1, m, GOLD[3])
    return img


def tab(active, w=32, h=16):
    img = canvas(w, h)
    body = [VIO[3], VIO[4], VIO[4]] if active else [NIGHT[1], NIGHT[2], NIGHT[2]]
    vgrad(img, 1, 1, w - 2, h - 1, body)
    edge = GOLD[1] if active else GOLD[3]
    img[0, 2:-2] = OUT; img[1, 1] = OUT; img[1, -2] = OUT; img[2:, 0] = OUT; img[2:, -1] = OUT
    img[1, 2:-2] = edge; img[2:, 1] = edge; img[2:, -2] = GOLD[3] if active else GOLD[4]
    if active:
        img[2, 3:-3] = VIO[1]
    else:
        img[-1, :] = GOLD[3]
    return img


def socket(w=40, h=40):
    """Round gem socket that sits behind a 32x32 icon."""
    img = canvas(w, h)
    cx, cy = w / 2, h / 2
    ellipse(img, cx, cy, 19.5, 19.5, OUT)
    ellipse(img, cx, cy, 18.5, 18.5, GOLD[3])
    ring(img, cx, cy, 17, 18.5, GOLD[2])
    ring(img, cx - 0.6, cy - 0.6, 17.4, 18.4, GOLD[1])
    ellipse(img, cx, cy, 16, 16, OUT)
    shaded_ellipse(img, cx, cy, 15, 15, [NIGHT[0], NIGHT[1], NIGHT[2], NIGHT[3], NIGHT[4]], light=(0.6, 0.6))
    for k in range(8):
        x, y = polar(cx, cy, 18, k * math.pi / 4)
        px(img, x, y, GOLD[0] if k % 2 == 0 else GOLD[2])
    return img


def page(w=32, h=32):
    """Inset page for the grimoire detail column (slightly lighter, thin gold line)."""
    img = canvas(w, h)
    vgrad(img, 0, 0, w - 1, h - 1, [c("#2c2148"), c("#271d40"), c("#221a39")])
    img[0, :] = GOLD[3]; img[-1, :] = GOLD[4]; img[:, 0] = GOLD[3]; img[:, -1] = GOLD[4]
    img[1, 1:-1] = c("#3a2e5c")
    return img


def marker_quest():
    """Objective star: gold star inside a bright ring (blinks at runtime)."""
    img = canvas(15, 15)
    ring(img, 7.5, 7.5, 6, 7.4, GOLD[1])
    poly(img, [polar(7.5, 7.8, 5 if k % 2 == 0 else 2.1, -math.pi / 2 + k * math.pi / 5) for k in range(10)], c("#ffffff"))
    px(img, 7, 6, GOLD[0])
    return finish(img)


def marker_arrow():
    """Edge pointer (points up; rotated at runtime)."""
    img = canvas(13, 13)
    poly(img, [(6.5, 0.5), (12.5, 9.5), (8.5, 8.5), (8.5, 12.5), (4.5, 12.5), (4.5, 8.5), (0.5, 9.5)], GOLD[1])
    poly(img, [(6.5, 2.5), (9.5, 7.5), (6.5, 6.5)], GOLD[0])
    return finish(img)


def main():
    MANIFEST.clear()
    for name, img in icons().items():
        save(name + "_draft", img)
    save("UI_panel_draft", panel(), border=10)
    save("UI_slot_draft", slot(), border=6)
    save("UI_bar_frame_draft", bar_frame(), border=4)
    save("UI_keycap_draft", keycap(), border=4)
    save("UI_moon_ornament_draft", ornament_moon())
    for k, ramp in {"hp": [c("#ffc4c4"), c("#ff6670"), c("#e0384c"), c("#a8203a"), c("#6e1430")],
                    "mp": [c("#cde4ff"), c("#6aa8ff"), c("#3a6ee8"), c("#2446b0"), c("#18287a")],
                    "res": [c("#f6e4ff"), c("#d6a6ff"), c("#a86ae8"), c("#7a40c0"), c("#4e2486")],
                    "xp": [c("#fff6c8"), c("#ffde7a"), c("#e6ac3e"), c("#b07a24"), c("#704a18")],
                    "boss": [c("#ffd0f0"), c("#e07ad0"), c("#b040b0"), c("#7a2488"), c("#4a1458")],
                    "lag": [c("#fff0d8"), c("#ffe0b0"), c("#f0c890"), c("#d0a070"), c("#a07850")]}.items():
        save(f"UI_bar_fill_{k}_draft", bar_fill(ramp))
    save("UI_marker_player_draft", marker_player())
    save("UI_marker_npc_draft", marker_dot(c("#f2c46b"), c("#fff4c2")))
    save("UI_marker_villager_draft", marker_dot(c("#cfc8e0"), c("#ffffff"), 2.2))
    save("UI_marker_enemy_draft", marker_dot(c("#e8384c"), c("#ffb0b0"), 2.2))
    save("UI_marker_boss_draft", marker_dot(c("#b040d0"), c("#ffc0ff"), 4.2, 11))
    save("UI_marker_star_draft", marker_star())
    save("UI_marker_door_draft", marker_door())
    save("UI_card_draft", card(), border=12)
    save("UI_banner_draft", banner(), border=4)
    save("UI_divider_draft", divider())
    save("UI_tab_draft", tab(False), border=6)
    save("UI_tab_active_draft", tab(True), border=6)
    save("UI_socket_draft", socket())
    save("UI_page_draft", page(), border=4)
    save("UI_marker_quest_draft", marker_quest())
    save("UI_marker_arrow_draft", marker_arrow())
    with open(os.path.join(ROOT, "Assets/_Game/Art/ui_manifest.json"), "w") as f:
        json.dump({"items": MANIFEST}, f, indent=1)
    print(len(MANIFEST), "ui sprites")


ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
if __name__ == "__main__":
    main()
