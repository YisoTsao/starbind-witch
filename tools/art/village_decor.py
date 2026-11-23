"""Generate DRAFT witch-village decoration art: ground decals, witch props, lantern posts, fences.

Status of every output: Needs Cleanup (procedural draft, not production art; ASSET_GAPS.md).
Nothing here is cut from the AI reference images. Palette = reference A (ART_DIRECTION.md) plus a
witch accent ramp (violet / gold / lantern glow); every prop has the shared dark-violet outline and
top-left light so it sits with the props cut from reference A.

Outputs: Assets/_Game/Art/Props/Draft/{DEC_*,PROP_*}_draft.png and Assets/_Game/Art/decor_manifest.json
(read by Starbind.Editor.GameContentBuilder like draft_manifest.json).
  DEC_*   ground decals (GroundDecal layer, no collider): grass tufts, flower clusters, clover,
          pebbles, small mushrooms, fallen petals, lily pads, light / dark grass patches, mote
  PROP_*  world props (World layer, y-sorted): lantern post, pumpkins, cauldron, herb rack,
          crystal cluster, candles, potion crate, broom
`fence(w, h)` is also used by extract_drafts.py to redraw the reference fences (their crops kept
grass between the rails).

Usage: python3 tools/art/village_decor.py <repo_root>
"""
import json, math, os, sys
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixlib import c, canvas, px, rect, ellipse, shaded_ellipse, poly, line, outline, blit, rng  # noqa

OUT = c("#2b2236")
OUT_SOFT = c("#3e3048")
WOOD = [c("#efc184"), c("#d39a5f"), c("#a86d45"), c("#7a4a34"), c("#4e3030")]
GRASS = dict(hi=c("#c6dc73"), light=c("#a6c955"), base=c("#92ba53"), dark=c("#7eab4d"), edge=c("#6f9a48"), deep=c("#4f7d4a"), deeper=c("#3d6342"))
VIOLET = [c("#e4c9fa"), c("#bf93ea"), c("#9262cf"), c("#6a40a6"), c("#45296f")]
GOLD = [c("#fff1b8"), c("#f6cf6e"), c("#d79a42"), c("#9a6330")]
GLOW = [c("#fffbe6"), c("#ffeaa6"), c("#ffc861")]
IRON = [c("#7b7398"), c("#58507a"), c("#3d3658"), c("#28223b")]
STONE = [c("#eef0f4"), c("#c7c9d6"), c("#9d9db3"), c("#6f6c87")]
FLOWERS = {
    "pink": [c("#ffe0ef"), c("#f7a1c8"), c("#d9679f")],
    "lilac": [c("#efe0ff"), c("#bfa3f2"), c("#8566cf")],
    "white": [c("#ffffff"), c("#e6eaf2"), c("#aeb6c9")],
    "yellow": [c("#fff6b8"), c("#f7d455"), c("#c9962c")],
    "blue": [c("#dff3ff"), c("#8fc8f2"), c("#4f86c9")],
}
CENTER = c("#ffd65c")
MANIFEST = []


# ------------------------------------------------------------------ helpers
def ramp_fill(img, mask, ramp, light=(-0.7, -0.7), cx=None, cy=None):
    """Shade an arbitrary mask with a ramp by the direction from the mask centre (top-left lit)."""
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return
    cx = xs.mean() if cx is None else cx
    cy = ys.mean() if cy is None else cy
    rx = max(xs.max() - xs.min(), 1) / 2 + 0.5
    ry = max(ys.max() - ys.min(), 1) / 2 + 0.5
    d = -(((xs - cx) / rx) * light[0] + ((ys - cy) / ry) * light[1])
    idx = np.clip(((d + 1) / 2 * len(ramp)).astype(int), 0, len(ramp) - 1)
    for i, col in enumerate(ramp):
        sel = idx == i
        img[ys[sel], xs[sel]] = col


def sel_outline(img, col=OUT):
    """Outline, then lighten outline pixels that face the top-left light (selective outline)."""
    a = img[:, :, 3] > 0
    outline(img, col)
    return img


def save(name, img, pivot_y=1, pivot_x=None, kind="prop"):
    # even dimensions (ART_DIRECTION §3): pad the top / right with transparent pixels; the pivot
    # counts from the bottom-left so it does not move
    h, w = img.shape[:2]
    img = np.pad(img, ((h % 2, 0), (0, w % 2), (0, 0)))
    d = os.path.join(ROOT, "Assets/_Game/Art/Props/Draft")
    os.makedirs(d, exist_ok=True)
    Image.fromarray(img).save(os.path.join(d, name + ".png"))
    h, w = img.shape[:2]
    MANIFEST.append({"name": name, "path": f"Assets/_Game/Art/Props/Draft/{name}.png", "w": w, "h": h, "frames": 1,
                     "pivotX": w // 2 if pivot_x is None else pivot_x, "pivotY": h // 2 if pivot_y is None else pivot_y,
                     "fps": 0, "note": kind})


# ------------------------------------------------------------------ ground decals
def tuft(seed, w=12, h=8, blades=7, flower=None):
    r = rng(seed)
    img = canvas(w, h)
    cx = w / 2
    for i in range(blades):
        bx = cx + (i - (blades - 1) / 2) * (w - 3) / blades + r.uniform(-0.5, 0.5)
        lean = (bx - cx) / (w / 2) * 2.2 + r.uniform(-0.6, 0.6)
        bh = r.integers(h - 4, h)
        for k in range(bh):
            t = k / max(bh - 1, 1)
            x = bx + lean * t * t
            y = h - 1 - k
            col = GRASS["deep"] if t < 0.3 else GRASS["edge"] if t < 0.6 else GRASS["light"] if t < 0.9 else GRASS["hi"]
            px(img, x, y, col)
    if flower:
        f = FLOWERS[flower]
        fx, fy = int(cx + r.integers(-2, 3)), 1
        px(img, fx, fy, f[1]); px(img, fx - 1, fy + 1, f[1]); px(img, fx + 1, fy + 1, f[2]); px(img, fx, fy + 2, f[2])
        px(img, fx, fy + 1, CENTER)
    # contact shadow row
    for x in range(1, w - 1):
        if img[h - 1, x, 3] and not img[h - 1, x - 1, 3]:
            img[h - 1, x - 1] = GRASS["deeper"]
    return img


def flower_cluster(colour, seed, w=14, h=11, n=5):
    r = rng(seed)
    img = canvas(w, h)
    f = FLOWERS[colour]
    # leaves first
    for _ in range(n + 2):
        x, y = r.integers(2, w - 2), r.integers(h // 2, h - 1)
        px(img, x, y, GRASS["deep"]); px(img, x + 1, y, GRASS["edge"]); px(img, x, y - 1, GRASS["light"])
    placed = []
    tries = 0
    while len(placed) < n and tries < 200:
        tries += 1
        x, y = r.integers(2, w - 2), r.integers(2, h - 3)
        if any(abs(x - a) < 3 and abs(y - b) < 3 for a, b in placed):
            continue
        placed.append((x, y))
        px(img, x, y + 2, GRASS["deep"])
        px(img, x, y - 1, f[0]); px(img, x - 1, y, f[1]); px(img, x + 1, y, f[2]); px(img, x, y + 1, f[2])
        px(img, x, y, CENTER if colour != "yellow" else c("#c9862c"))
    return img


def clover(seed):
    r = rng(seed)
    img = canvas(14, 9)
    for _ in range(6):
        x, y = r.integers(2, 12), r.integers(2, 7)
        for dx, dy, col in ((0, -1, GRASS["light"]), (-1, 0, GRASS["base"]), (1, 0, GRASS["edge"]), (0, 0, GRASS["dark"])):
            px(img, x + dx, y + dy, col)
        px(img, x, y + 1, GRASS["deep"])
    return img


def pebbles(seed):
    r = rng(seed)
    img = canvas(14, 8)
    for i in range(3):
        x, y = 2 + i * 4 + r.integers(0, 2), r.integers(2, 5)
        rx, ry = r.uniform(1.4, 2.4), r.uniform(1.0, 1.6)
        m = np.zeros(img.shape[:2], bool)
        ys, xs = np.mgrid[0:8, 0:14]
        m = ((xs + 0.5 - x) / rx) ** 2 + ((ys + 0.5 - y) / ry) ** 2 <= 1
        ramp_fill(img, m, STONE[1:])
        px(img, x - 1, y - 1, STONE[0])
    a = img[:, :, 3] > 0
    sh = np.zeros_like(a); sh[1:, :] = a[:-1, :]
    img[sh & ~a] = GRASS["deep"]
    return img


def tiny_shrooms(seed, cap="red"):
    caps = {"red": [c("#ff8d7a"), c("#e2483f"), c("#a52a3a")], "violet": VIOLET[1:4]}[cap]
    r = rng(seed)
    img = canvas(12, 10)
    for (x, y, s) in ((4, 7, 2), (8, 8, 1)):
        rect(img, x, y - 1, x, y + 1, c("#f3e6d0")); px(img, x + 1, y, c("#cdb79a"))
        m = np.zeros(img.shape[:2], bool)
        ys, xs = np.mgrid[0:10, 0:12]
        m = ((xs + 0.5 - (x + 0.5)) / (s + 1.2)) ** 2 + ((ys + 0.5 - (y - 1.5)) / (s * 0.7 + 0.8)) ** 2 <= 1
        m &= ys <= y - 1
        ramp_fill(img, m, caps)
        if cap == "red":
            px(img, x - 1, y - 2 - (s > 1), c("#fff4ea"))
        else:
            px(img, x, y - 2 - (s > 1), c("#f6e9ff"))
    outline(img, OUT_SOFT)
    return img


def petals(seed, w=28, h=16, n=16):
    r = rng(seed)
    img = canvas(w, h)
    for _ in range(n):
        x, y = r.normal(w / 2, w / 4.5), r.normal(h / 2, h / 4.5)
        col = FLOWERS["pink"][r.integers(0, 3)]
        px(img, x, y, col)
        if r.random() < 0.5:
            px(img, x + 1, y, FLOWERS["pink"][2])
    return img


def lily(seed, bloom=False):
    img = canvas(14, 10)
    ys, xs = np.mgrid[0:10, 0:14]
    m = ((xs + 0.5 - 7) / 6) ** 2 + ((ys + 0.5 - 5) / 3.6) ** 2 <= 1
    ang = np.arctan2(ys + 0.5 - 5, xs + 0.5 - 7)
    m &= ~((ang > -0.35) & (ang < 0.25) & (xs > 7))  # notch
    ramp_fill(img, m, [c("#b8de7a"), c("#86c25a"), c("#5d9a4c"), c("#3f7448")])
    px(img, 4, 3, c("#d6f09a"))
    outline(img, c("#245a6e"))
    if bloom:
        f = FLOWERS["pink"]
        for dx, dy, col in ((0, -1, f[0]), (-1, 0, f[1]), (1, 0, f[1]), (0, 0, CENTER), (-1, -1, f[0]), (1, -1, f[2])):
            px(img, 5 + dx, 5 + dy, col)
    return img


BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


def grass_patch(seed, w, h, col_a, col_b):
    """Soft irregular blob with a dithered rim: breaks the tile grid into larger light / dark areas."""
    r = rng(seed)
    img = canvas(w, h)
    ys, xs = np.mgrid[0:h, 0:w]
    d = ((xs + 0.5 - w / 2) / (w / 2)) ** 2 + ((ys + 0.5 - h / 2) / (h / 2)) ** 2
    ang = np.arctan2(ys - h / 2, xs - w / 2)
    wob = sum(r.uniform(0.04, 0.1) * np.sin(k * ang + r.uniform(0, 6.3)) for k in (2, 3, 5))
    d = d + wob
    b = np.tile(BAYER, (h // 4 + 1, w // 4 + 1))[:h, :w]
    core = d < 0.55
    rim = (d >= 0.55) & (d < 1.0) & (b > (d - 0.55) / 0.45)
    img[core | rim] = col_a
    # sparse speckle of the second tone inside
    sp = r.random((h, w)) < 0.12
    img[(core) & sp] = col_b
    return img


def mote():
    img = canvas(5, 5)
    img[2, 1:4] = c("#f3ffb0", 200); img[1:4, 2] = c("#f3ffb0", 200)
    img[2, 2] = c("#ffffff")
    return img


# ------------------------------------------------------------------ fences
def fence(w, h, seed=0):
    """Rail fence drawn over the full width: posts every ~17 px, two rails, grass at the post feet."""
    img = canvas(w, h)
    r = rng(seed)
    post_w = 4
    n = max(2, round((w - post_w) / 17) + 1)
    xs = [round(i * (w - post_w) / (n - 1)) for i in range(n)]
    top = max(1, h - 20)
    rail_y = [top + 5, top + 11]
    for ry in rail_y:   # rails behind posts
        rect(img, 0, ry, w - 1, ry, WOOD[1]); rect(img, 0, ry + 1, w - 1, ry + 1, WOOD[2]); rect(img, 0, ry + 2, w - 1, ry + 2, WOOD[3])
        for x in range(2, w - 2, 5):   # grain
            if r.random() < 0.5:
                px(img, x, ry + 1, WOOD[3])
        for x in range(3, w - 2, 7):
            px(img, x, ry, WOOD[0])
    for x0 in xs:
        rect(img, x0, top + 1, x0 + post_w - 1, h - 3, WOOD[2])
        rect(img, x0, top + 1, x0, h - 3, WOOD[0])
        rect(img, x0 + 1, top + 1, x0 + 1, h - 3, WOOD[1])
        rect(img, x0 + post_w - 1, top + 1, x0 + post_w - 1, h - 3, WOOD[3])
        rect(img, x0 + 1, top, x0 + post_w - 2, top, WOOD[1])   # rounded cap
        px(img, x0 + 1, top + 4, WOOD[3]); px(img, x0 + 2, top + 9, WOOD[3])   # knots
        for ry in rail_y:   # nail heads
            px(img, x0 + 2, ry + 1, c("#e8d8b0"))
    outline(img, OUT)
    for x0 in xs:   # grass at the post feet
        for dx, k in ((-1, 2), (0, 3), (post_w, 2), (post_w - 1, 3), (1, 1)):
            for j in range(k):
                px(img, x0 + dx, h - 1 - j, GRASS["edge"] if j < k - 1 else GRASS["light"])
    return img


# ------------------------------------------------------------------ witch props
def lantern_post():
    w, h = 20, 46
    img = canvas(w, h)
    x = 6
    rect(img, x - 2, h - 4, x + 2, h - 2, IRON[2]); rect(img, x - 2, h - 4, x + 1, h - 4, IRON[1])   # foot
    rect(img, x - 1, 8, x, h - 4, IRON[2]); rect(img, x - 1, 8, x - 1, h - 5, IRON[1])
    rect(img, x + 1, 9, x + 1, h - 5, IRON[3])
    # curled arm over to the right
    for (ax, ay) in ((x + 1, 9), (x + 2, 8), (x + 3, 8), (x + 4, 8), (x + 5, 8), (x + 6, 8), (x + 7, 9), (x + 8, 10)):
        px(img, ax, ay, IRON[1]); px(img, ax, ay + 1, IRON[3])
    px(img, x - 2, 10, IRON[1]); px(img, x - 3, 11, IRON[2]); px(img, x - 3, 12, IRON[1])   # small curl
    # star finial
    sx, sy = x, 4
    for dx, dy, col in ((0, -2, GOLD[0]), (0, -1, GOLD[1]), (-1, 0, GOLD[1]), (0, 0, GOLD[0]), (1, 0, GOLD[2]), (-2, 0, GOLD[2]),
                        (2, 0, GOLD[2]), (0, 1, GOLD[1]), (-1, 2, GOLD[2]), (1, 2, GOLD[3]), (0, 2, GOLD[2])):
        px(img, sx + dx, sy + dy, col)
    rect(img, x - 1, 6, x, 7, IRON[1])
    # hanging lantern
    lx, ly = x + 8, 12
    px(img, lx, ly, IRON[2])
    rect(img, lx - 3, ly + 1, lx + 3, ly + 2, VIOLET[3]); rect(img, lx - 2, ly + 1, lx + 1, ly + 1, VIOLET[2])
    rect(img, lx - 3, ly + 3, lx + 3, ly + 10, GOLD[2])
    rect(img, lx - 2, ly + 3, lx + 2, ly + 9, GLOW[1])
    rect(img, lx - 1, ly + 4, lx + 1, ly + 8, GLOW[0])
    rect(img, lx, ly + 3, lx, ly + 9, GOLD[3])   # frame bar
    px(img, lx - 1, ly + 6, GLOW[0])
    rect(img, lx - 3, ly + 11, lx + 3, ly + 11, VIOLET[3]); rect(img, lx - 1, ly + 12, lx + 1, ly + 12, VIOLET[4])
    rect(img, lx - 3, ly + 3, lx - 3, ly + 10, GOLD[1])
    outline(img, OUT)
    # grass at the foot
    for dx, k in ((-3, 2), (-2, 3), (3, 3), (4, 2)):
        for j in range(k):
            px(img, x + dx, h - 1 - j, GRASS["edge"] if j < k - 1 else GRASS["light"])
    return img


def pumpkin(colour="orange"):
    ramps = {"orange": [c("#ffcf80"), c("#ffa547"), c("#e7772d"), c("#b3501f"), c("#7a3420")],
             "violet": [c("#e7c9ff"), c("#b98ae8"), c("#8c5ac9"), c("#643c9c"), c("#3f2766")]}
    rp = ramps[colour]
    w, h = 18, 15
    img = canvas(w, h)
    ys, xs = np.mgrid[0:h, 0:w]
    for cx, rx in ((5.5, 4.2), (12.5, 4.2), (9, 4.8)):   # three lobes, centre lobe last (front)
        m = ((xs + 0.5 - cx) / rx) ** 2 + ((ys + 0.5 - 9) / 5.2) ** 2 <= 1
        ramp_fill(img, m, rp[:4], cx=cx - 1, cy=7.5)
    for cx in (7, 11):   # rib grooves
        for y in range(6, 14):
            if img[y, int(cx), 3]:
                img[y, int(cx)] = rp[3]
    rect(img, 8, 2, 9, 4, c("#6b8a3a")); px(img, 9, 1, c("#4f6a2e")); px(img, 8, 2, c("#8fb350"))   # stem
    px(img, 10, 3, c("#7fb04c")); px(img, 11, 3, c("#5d9a4c")); px(img, 12, 2, c("#5d9a4c"))   # leaf
    outline(img, OUT)
    return img


def cauldron():
    w, h = 26, 24
    img = canvas(w, h)
    ys, xs = np.mgrid[0:h, 0:w]
    # little fire logs underneath
    rect(img, 5, 20, 20, 21, WOOD[2]); rect(img, 5, 20, 20, 20, WOOD[1]); rect(img, 9, 21, 16, 22, WOOD[3])
    for fx, fh in ((8, 3), (12, 4), (16, 3)):
        rect(img, fx, 20 - fh, fx + 1, 19, c("#ff9a3c")); px(img, fx, 20 - fh, c("#ffe07a")); px(img, fx + 1, 19, c("#ffcf5a"))
    body = ((xs + 0.5 - 13) / 10) ** 2 + ((ys + 0.5 - 12.5) / 7.5) ** 2 <= 1
    body &= ys >= 8
    ramp_fill(img, body, IRON, cx=11, cy=10)
    rect(img, 6, 19, 7, 20, IRON[3]); rect(img, 18, 19, 19, 20, IRON[3])   # legs
    rim = ((xs + 0.5 - 13) / 10.5) ** 2 + ((ys + 0.5 - 7.5) / 3) ** 2 <= 1
    img[rim] = IRON[1]
    inner = ((xs + 0.5 - 13) / 8.6) ** 2 + ((ys + 0.5 - 7.5) / 2) ** 2 <= 1
    img[inner] = c("#5fc45a")
    img[inner & (ys < 7)] = c("#98ec72")
    for bx, by, r_ in ((9, 6, 1), (15, 5, 1.5), (18, 7, 1)):   # bubbles
        m = ((xs + 0.5 - bx) / r_) ** 2 + ((ys + 0.5 - by) / r_) ** 2 <= 1
        img[m] = c("#b6f58a")
        px(img, bx - 0.5, by - r_ + 0.5, c("#eaffd0"))
    px(img, 15, 2, c("#b6f58a")); px(img, 11, 1, c("#d6ffb0"))
    rect(img, 6, 6, 8, 6, IRON[0])   # rim highlight
    outline(img, OUT)
    return img


def herb_rack():
    w, h = 30, 30
    img = canvas(w, h)
    for x0, x1 in ((3, 7), (26, 22)):   # A-frame legs
        line(img, x0, h - 2, (x0 + x1) / 2 + (2 if x0 < 15 else -2), 4, WOOD[2], 2)
    rect(img, 2, 5, w - 3, 6, WOOD[1]); rect(img, 2, 7, w - 3, 7, WOOD[3])   # top bar
    px(img, 2, 5, WOOD[0]); rect(img, 3, 5, w - 4, 5, WOOD[0])
    herbs = [(6, c("#8fbf5a"), c("#5d8a42")), (10, c("#b48be8"), c("#7a52b8")), (14, c("#e0c76a"), c("#a88c3a")),
             (18, c("#7fb06a"), c("#4f7d4a")), (22, c("#d38ab4"), c("#9a5a86"))]
    for i, (hx, a, b) in enumerate(herbs):
        L = 7 + (i % 2) * 3
        px(img, hx, 8, c("#cbb48a")); px(img, hx, 9, c("#cbb48a"))
        for k in range(L):
            wdt = 1 + min(k, L - 1 - k, 2)
            for dx in range(-wdt // 2, wdt - wdt // 2):
                px(img, hx + dx, 10 + k, a if dx <= 0 and k % 3 != 2 else b)
    outline(img, OUT)
    return img


def crystal_cluster():
    w, h = 18, 20
    img = canvas(w, h)
    shards = [((4, 19), (2, 10), (5, 6), (8, 12), (8, 19)),
              ((7, 19), (7, 7), (10, 1), (13, 7), (13, 19)),
              ((11, 19), (12, 12), (15, 9), (16, 13), (15, 19))]
    for k, pts in enumerate(shards):
        m = poly(img, pts, VIOLET[2])
        ys, xs = np.nonzero(m)
        mid = (xs.min() + xs.max()) / 2
        img[ys[xs < mid], xs[xs < mid]] = VIOLET[1]
        img[ys[xs > mid + 0.6], xs[xs > mid + 0.6]] = VIOLET[3]
        top = ys.min()
        for y in range(top + 2, top + 6):
            px(img, int(mid) - 1, y, VIOLET[0])
    px(img, 9, 3, c("#ffffff")); px(img, 4, 9, c("#ffffff"))
    outline(img, OUT)
    rect(img, 2, h - 1, w - 3, h - 1, STONE[3])
    return img


def candles():
    w, h = 14, 14
    img = canvas(w, h)
    for x, top in ((3, 6), (7, 3), (10, 7)):
        rect(img, x - 1, top, x + 1, h - 2, c("#f3e9d2")); rect(img, x + 1, top, x + 1, h - 2, c("#cdbb98"))
        px(img, x - 1, top, c("#ffffff"))
        px(img, x, top - 1, c("#3a2a2a"))
        px(img, x, top - 2, c("#ffcf5a")); px(img, x, top - 3, c("#fff6c0"))
        px(img, x - 1, top + 2, c("#e6d6b4"))   # drip
    rect(img, 1, h - 2, w - 2, h - 2, STONE[2])
    outline(img, OUT)
    return img


def potion_crate():
    w, h = 22, 18
    img = canvas(w, h)
    rect(img, 1, 8, w - 2, h - 2, WOOD[2]); rect(img, 1, 8, w - 2, 8, WOOD[1]); rect(img, 1, h - 2, w - 2, h - 2, WOOD[3])
    rect(img, 1, 12, w - 2, 12, WOOD[3]); rect(img, 1, 8, 1, h - 2, WOOD[1])
    for bx, col, tall in ((5, FLOWERS["pink"], 5), (10, [c("#c9f2ff"), c("#6fc8e8"), c("#3d8ab8")], 7), (15, VIOLET, 6)):
        top = 8 - tall
        rect(img, bx - 1, top + 2, bx + 1, 8, col[1]); rect(img, bx + 1, top + 2, bx + 1, 8, col[2])
        px(img, bx - 1, top + 3, col[0])
        rect(img, bx, top, bx, top + 1, c("#b98a5a"))   # cork
    outline(img, OUT)
    return img


def broom():
    w, h = 12, 34
    img = canvas(w, h)
    line(img, 7, 1, 5, 22, WOOD[2], 2)
    px(img, 7, 1, WOOD[1]); px(img, 6, 5, WOOD[1]); px(img, 6, 10, WOOD[1])
    rect(img, 3, 22, 7, 23, VIOLET[3])
    for k in range(9):   # bristles fan out
        y = 24 + k
        half = 2 + k // 2
        for dx in range(-half, half + 1):
            px(img, 5 + dx, y, c("#e6c676") if (dx + k) % 3 else c("#b89148"))
    outline(img, OUT)
    return img


def main():
    MANIFEST.clear()
    for i, (fl, s) in enumerate(((None, 1), (None, 2), ("white", 3), (None, 4), ("pink", 5))):
        save(f"DEC_tuft_{i}_draft", tuft(40 + s, flower=fl), pivot_y=0, kind="decal")
    for i, colour in enumerate(("pink", "lilac", "white", "yellow", "blue")):
        save(f"DEC_flowers_{colour}_draft", flower_cluster(colour, 60 + i), pivot_y=None, kind="decal")
    save("DEC_clover_draft", clover(70), pivot_y=None, kind="decal")
    save("DEC_pebbles_0_draft", pebbles(71), pivot_y=None, kind="decal")
    save("DEC_pebbles_1_draft", pebbles(72), pivot_y=None, kind="decal")
    save("DEC_shrooms_red_draft", tiny_shrooms(73, "red"), pivot_y=1, kind="decal")
    save("DEC_shrooms_violet_draft", tiny_shrooms(74, "violet"), pivot_y=1, kind="decal")
    save("DEC_petals_draft", petals(75), pivot_y=None, kind="decal")
    save("DEC_lily_0_draft", lily(76), pivot_y=None, kind="decal")
    save("DEC_lily_1_draft", lily(77, bloom=True), pivot_y=None, kind="decal")
    save("DEC_patch_light_draft", grass_patch(80, 56, 36, GRASS["light"], GRASS["hi"]), pivot_y=None, kind="decal")
    save("DEC_patch_light_s_draft", grass_patch(81, 36, 24, GRASS["light"], GRASS["base"]), pivot_y=None, kind="decal")
    save("DEC_patch_dark_draft", grass_patch(82, 52, 34, GRASS["dark"], GRASS["edge"]), pivot_y=None, kind="decal")
    save("DEC_mote_draft", mote(), pivot_y=None, kind="vfx")
    save("PROP_lantern_post_draft", lantern_post(), pivot_x=6, pivot_y=2)
    save("PROP_pumpkin_draft", pumpkin("orange"), pivot_y=2)
    save("PROP_pumpkin_violet_draft", pumpkin("violet"), pivot_y=2)
    save("PROP_cauldron_draft", cauldron(), pivot_y=2)
    save("PROP_herb_rack_draft", herb_rack(), pivot_y=2)
    save("PROP_crystal_cluster_draft", crystal_cluster(), pivot_y=1)
    save("PROP_candles_draft", candles(), pivot_y=1)
    save("PROP_potion_crate_draft", potion_crate(), pivot_y=1)
    save("PROP_broom_draft", broom(), pivot_y=1)
    with open(os.path.join(ROOT, "Assets/_Game/Art/decor_manifest.json"), "w") as f:
        json.dump({"items": MANIFEST}, f, indent=1)
    print(len(MANIFEST), "decor sprites")


ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
if __name__ == "__main__":
    main()
