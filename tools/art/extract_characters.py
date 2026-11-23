"""Extract DRAFT character sheets from the user's chibi reference sheets (references/tilesets/, read-only).

Status of every output: Needs Cleanup (draft cut from AI reference sheets, not production art).
Sources (5 x 9 grid; row triplets side / up / down, each: action, walk, idle):
  Chibi_Mage_Pixel_Art Sprite_Sheet.png   -> CHR_witch_ref    (the player witch)
  Chibi_Knight_Pixel_Art Sprite_Sheet.png -> NPC_prince_ref   (the prince NPC)
  Chibi_Villager_Pixel_Sprite_Sheet.png   -> NPC_villager_XX_ref (palette / accessory variants)

Steps: foreground mask per sheet (alpha for the villager; alpha minus the gray sticker halo and
ground shadow for the mage; flood fill of the near-white background for the knight), connected
components assigned to grid cells, box-median resample by 187/48 into 64 x 56 frames with the feet
on a shared baseline per row, per-sheet palette reduction.

Output layout ("v2", 36 frames, 12 per direction, directions down / up / side-right):
  0-1 idle, 2-6 walk (walkFrames used), 7-11 action.
Writes Assets/_Game/Art/Characters/Ref/*.png and Assets/_Game/Art/character_manifest.json, which
the Unity builder merges with draft_manifest.json.

Usage: python3 tools/art/extract_characters.py <repo_root> [--preview <dir>]
"""
import colorsys, json, os, sys
from collections import deque
import numpy as np
from PIL import Image

ROOT = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "."
PREVIEW = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None
REF = os.path.join(ROOT, "references/tilesets")
OUT = os.path.join(ROOT, "Assets/_Game/Art/Characters/Ref")
MANIFEST = os.path.join(ROOT, "Assets/_Game/Art/character_manifest.json")

COLS, ROWS = 5, 9
SCALE = 187.0 / 48.0      # source px per game px (keeps the witch ~40 px tall like the old draft)
FW, FH = 64, 56           # frame size; wide enough for the staff / sword swings
FEET = 4                  # pivot: feet rest 4 px above the bottom edge

# per direction: (idle row, walk row, action row) inside the side/up/down triplet
DIRS = {"down": 6, "up": 3, "side": 0}


# ---------------------------------------------------------------- masks
def mask_alpha(a):
    return a[..., 3] >= 128


def mask_mage(a):
    """Alpha matte includes a gray sticker halo and a gray ground shadow; peel them from outside."""
    fg = a[..., 3] >= 140
    rgb = a[..., :3].astype(int)
    mx, mn = rgb.max(2), rgb.min(2)
    grayish = (mx - mn < 34) & (mx > 70) & (mx < 215)
    return peel(fg, grayish)


def mask_knight(a):
    rgb = a[..., :3].astype(int)
    mx, mn = rgb.max(2), rgb.min(2)
    light = (mn > 196) & (mx - mn < 26)
    shadowish = (mx - mn < 22) & (mn > 150)
    fg = ~peel(np.zeros(light.shape, bool), light | shadowish, invert=True)
    return fg


def peel(fg, removable, invert=False):
    """Flood fill from the background (or the image border when invert) through removable pixels."""
    h, w = removable.shape
    bg = np.zeros((h, w), bool)
    q = deque()
    if invert:
        for x in range(w):
            for y in (0, h - 1):
                if removable[y, x] and not bg[y, x]:
                    bg[y, x] = True; q.append((y, x))
        for y in range(h):
            for x in (0, w - 1):
                if removable[y, x] and not bg[y, x]:
                    bg[y, x] = True; q.append((y, x))
    else:
        ys, xs = np.nonzero(~fg)
        bg[ys, xs] = True
        # seed only background pixels touching a removable pixel
        border = np.zeros_like(bg)
        border[:-1] |= removable[1:] & fg[1:]; border[1:] |= removable[:-1] & fg[:-1]
        border[:, :-1] |= removable[:, 1:] & fg[:, 1:]; border[:, 1:] |= removable[:, :-1] & fg[:, :-1]
        for y, x in zip(*np.nonzero(border & bg)):
            q.append((y, x))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not bg[ny, nx] and removable[ny, nx]:
                bg[ny, nx] = True
                q.append((ny, nx))
    return bg if invert else (fg & ~bg)


def components(m):
    """4-connected labels (numpy BFS; sheets are small)."""
    h, w = m.shape
    lab = np.zeros((h, w), np.int32)
    n = 0
    for sy, sx in zip(*np.nonzero(m)):
        if lab[sy, sx]:
            continue
        n += 1
        lab[sy, sx] = n
        q = deque([(sy, sx)])
        while q:
            y, x = q.popleft()
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < w and m[ny, nx] and not lab[ny, nx]:
                    lab[ny, nx] = n
                    q.append((ny, nx))
    return lab, n


# ---------------------------------------------------------------- frames
def split_cells(a, fg):
    """Returns cells[row][col] = boolean mask of the source pixels belonging to that frame."""
    h, w = fg.shape
    cw, ch = w / COLS, h / ROWS
    lab, n = components(fg)
    cells = [[np.zeros_like(fg) for _ in range(COLS)] for _ in range(ROWS)]
    for i in range(1, n + 1):
        ys, xs = np.nonzero(lab == i)
        if len(ys) < 12:
            continue  # speckle
        bw, bh = xs.max() - xs.min() + 1, ys.max() - ys.min() + 1
        if len(ys) / max(bw, bh) < 4.5:
            continue  # sheet grid lines and stray slivers
        cx = min(COLS - 1, int(xs.mean() / cw))
        cy = min(ROWS - 1, int(ys.mean() / ch))
        cells[cy][cx][ys, xs] = True
    return cells, cw, ch


def resample(a, m, ox, oy):
    """64x56 frame whose top-left sits at source (ox, oy); box median over foreground pixels."""
    out = np.zeros((FH, FW, 4), np.uint8)
    H, W = m.shape
    for ty in range(FH):
        y0, y1 = int(round(oy + ty * SCALE)), int(round(oy + (ty + 1) * SCALE))
        if y1 <= 0 or y0 >= H:
            continue
        y0, y1 = max(0, y0), min(H, y1)
        for tx in range(FW):
            x0, x1 = int(round(ox + tx * SCALE)), int(round(ox + (tx + 1) * SCALE))
            if x1 <= 0 or x0 >= W:
                continue
            x0, x1 = max(0, x0), min(W, x1)
            box = m[y0:y1, x0:x1]
            if box.size == 0 or box.mean() < 0.45:
                continue
            px = a[y0:y1, x0:x1, :3][box]
            # darkest-third bias keeps 1-px outlines that a plain median would wash out
            lum = px.astype(int).sum(1)
            if (lum < 200).mean() > 0.3:
                px = px[lum < np.percentile(lum, 50) + 1]
            out[ty, tx, :3] = np.median(px, 0)
            out[ty, tx, 3] = 255
    return out


def frames_for(a, fg, walk_counts):
    cells, cw, ch = split_cells(a, fg)
    result, walk = {}, {}
    for d, base in DIRS.items():
        rows = {"action": base, "walk": base + 1, "idle": base + 2}
        # shared baseline per direction: median of the idle/walk feet rows
        bottoms, centers = [], []
        for r in (rows["idle"], rows["walk"]):
            for c in range(COLS):
                ys, xs = np.nonzero(cells[r][c])
                if len(ys):
                    bottoms.append(ys.max() - r * ch)
                    centers.append(xs.mean() - c * cw)
        bottom = float(np.median(bottoms))
        cxo = float(np.median(centers))
        seq = []
        counts = {"idle": 2, "walk": walk_counts[d], "action": 5}
        for kind, slots in (("idle", 2), ("walk", 5), ("action", 5)):
            r = rows[kind]
            for i in range(slots):
                c = min(i, counts[kind] - 1)
                ox = c * cw + cxo - FW / 2 * SCALE
                oy = r * ch + bottom - (FH - FEET) * SCALE
                seq.append(resample(a, cells[r][c], ox, oy))
        result[d] = seq
        walk[d] = counts["walk"]
    frames = result["down"] + result["up"] + result["side"]
    return frames, min(walk.values())


def quantize(frames, colors=72):
    strip = np.concatenate(frames, 1)
    alpha = strip[..., 3]
    rgb = Image.fromarray(strip[..., :3])
    q = rgb.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
    out = np.dstack([np.array(q), alpha])
    out[alpha == 0] = 0
    return [out[:, i * FW:(i + 1) * FW] for i in range(len(frames))]


def clean(frames):
    """Drop isolated opaque pixels (resample speckle)."""
    res = []
    for f in frames:
        f = f.copy()
        op = f[..., 3] > 0
        nb = np.zeros(op.shape, int)
        p = np.pad(op, 1)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy or dx:
                    nb += p[1 + dy:1 + dy + op.shape[0], 1 + dx:1 + dx + op.shape[1]]
        f[op & (nb <= 1)] = 0
        res.append(f)
    return res


# ---------------------------------------------------------------- villager variants
def hsv(img):
    rgb = img[..., :3].astype(float) / 255
    mx, mn = rgb.max(2), rgb.min(2)
    v = mx
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    d = np.maximum(mx - mn, 1e-6)
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    return h, s, v


def regions(f):
    """Classify villager pixels: hair, skin, vest, shirt, pants, boots/other (by colour + height)."""
    op = f[..., 3] > 0
    h, s, v = hsv(f)
    ys = np.nonzero(op.any(1))[0]
    if len(ys) == 0:
        return {}
    top, bot = ys.min(), ys.max()
    rel = (np.arange(FH)[:, None] - top) / max(1, bot - top)
    rel = np.broadcast_to(rel, op.shape)
    brownish = (h >= 8) & (h <= 45)
    face = rel < 0.44
    skin = op & brownish & (v > 0.74) & (s > 0.16) & (s < 0.6) & (face | (s >= 0.3)) & (rel < 0.85)
    shirt = op & ~skin & (s < 0.3) & (v > 0.66) & (rel > 0.44) & (rel < 0.8)
    pants = op & (h > 33) & (h < 120) & (s > 0.15) & (v < 0.7) & (rel > 0.6)
    hair = op & brownish & ~skin & (rel < 0.48) & (v > 0.22)
    vest = op & ((h < 22) | (h > 340)) & (s > 0.35) & (rel >= 0.4) & (rel < 0.82) & ~skin
    return {"hair": hair, "skin": skin, "shirt": shirt, "pants": pants, "vest": vest, "rel": rel}


def recolor(f, mask, target_rgb, keep_value=True):
    """Map the masked pixels' brightness onto a ramp built around target_rgb."""
    if not mask.any():
        return
    _, _, v = hsv(f)
    vs = v[mask]
    ref = np.median(vs)
    tr = np.array(target_rgb, float) / 255
    th, ts, tv = colorsys.rgb_to_hsv(*tr)
    k = np.clip(vs / max(ref, 1e-3), 0.25, 1.6)
    nv = np.clip(tv * k, 0, 1)
    ns = np.clip(ts * (1.15 - 0.3 * (k - 1)), 0, 1)
    out = np.array([colorsys.hsv_to_rgb(th, ss, vv) for ss, vv in zip(ns, nv)]) * 255
    f[mask, :3] = out.astype(np.uint8)


HAIR = {"brown": None, "black": (58, 48, 62), "blonde": (226, 186, 96), "red": (196, 88, 52),
        "gray": (178, 172, 180), "navy": (70, 82, 140), "pink": (226, 140, 160), "chestnut": (140, 82, 50)}
SKIN = {"base": None, "tan": (214, 150, 104), "deep": (150, 98, 70), "pale": (252, 220, 200)}
CLOTH = {"red": None, "blue": (70, 100, 170), "green": (70, 130, 80), "purple": (120, 80, 150),
         "gray": (110, 110, 120), "mustard": (196, 150, 60), "teal": (50, 140, 140), "black": (60, 56, 66)}
SHIRT = {"cream": None, "white": (240, 240, 236), "sky": (170, 200, 232), "rose": (236, 190, 196), "sage": (196, 216, 176)}
PANTS = {"olive": None, "brown": (120, 84, 56), "blue": (70, 86, 130), "gray": (120, 120, 128), "maroon": (120, 50, 60)}

# (id, hair, skin, vest, shirt, pants, headwear, apron): kerchiefs recolour the top of the hair, the straw
# hat is drawn, the apron is a light panel over the lower torso on front-facing frames
VILLAGERS = [
    ("01", "brown", "base", "red", "cream", "olive", None, False),
    ("02", "black", "tan", "blue", "white", "gray", None, False),
    ("03", "blonde", "pale", "green", "cream", "brown", "straw", False),
    ("04", "red", "base", "purple", "rose", "blue", None, True),
    ("05", "gray", "base", "gray", "white", "brown", "kerchief_blue", False),
    ("06", "navy", "deep", "mustard", "sky", "olive", None, False),
    ("07", "pink", "pale", "teal", "white", "maroon", None, True),
    ("08", "chestnut", "tan", "black", "sage", "gray", "kerchief_red", False),
    ("09", "black", "deep", "red", "sky", "blue", "straw", False),
    ("10", "blonde", "base", "blue", "sage", "maroon", None, False),
    ("11", "gray", "tan", "green", "white", "brown", "straw", True),
    ("12", "red", "deep", "teal", "cream", "gray", "kerchief_blue", False),
]
HEADWEAR = {"straw": (220, 190, 110), "kerchief_blue": (80, 120, 200), "kerchief_red": (200, 70, 70)}
APRON = (236, 232, 220)


def villager_variant(frames, spec):
    _, hair, skin, vest, shirt, pants, head, apron = spec
    out = []
    for k, f in enumerate(frames):
        f = f.copy()
        r = regions(f)
        if not r:
            out.append(f)
            continue
        if HAIR[hair]:
            recolor(f, r["hair"], HAIR[hair])
        if SKIN[skin]:
            recolor(f, r["skin"], SKIN[skin])
        if CLOTH[vest]:
            recolor(f, r["vest"], CLOTH[vest])
        if SHIRT[shirt]:
            recolor(f, r["shirt"], SHIRT[shirt])
        if PANTS[pants]:
            recolor(f, r["pants"], PANTS[pants])
        if head == "straw":
            straw_hat(f, r["hair"])
        elif head:
            ys = np.nonzero(r["hair"].any(1))[0]
            if len(ys):
                cap = r["hair"] & (np.arange(FH)[:, None] <= ys.min() + max(3, (ys.max() - ys.min()) * 0.38))
                recolor(f, cap, HEADWEAR[head])
        if apron and k < 12:  # front-facing frames only
            body = r["shirt"] | r["vest"]
            xs = np.nonzero(body.any(0))[0]
            if len(xs):
                cx, half = xs.mean(), (xs.max() - xs.min()) * 0.3
                cols = (np.arange(FW)[None, :] >= cx - half) & (np.arange(FW)[None, :] <= cx + half)
                recolor(f, body & cols & (r["rel"] > 0.56), APRON)
        out.append(f)
    return out


def straw_hat(f, hair):
    ys, xs = np.nonzero(hair)
    if not len(ys):
        return
    top = ys.min()
    band = hair & (np.arange(FH)[:, None] <= top + 4)
    recolor(f, band, HEADWEAR["straw"])
    by = top + 5
    row = np.nonzero(hair[min(FH - 1, by)])[0]
    if not len(row):
        return
    x0, x1 = max(0, row.min() - 3), min(FW - 1, row.max() + 3)
    light, mid, dark = (236, 206, 128), (206, 166, 88), (92, 60, 40)
    f[by - 1, x0:x1 + 1] = (*dark, 255)
    f[by, x0:x1 + 1] = (*light, 255)
    f[by + 1, x0:x1 + 1] = (*mid, 255)
    f[by + 2, x0 + 1:x1] = (*dark, 255)
    f[by - 1:by + 2, x0] = (*dark, 255)
    f[by - 1:by + 2, x1] = (*dark, 255)
    f[by - 2, max(0, row.min()):row.max() + 1] = (180, 70, 60, 255)  # hat band


# ---------------------------------------------------------------- main
def sheet(frames):
    return np.concatenate(frames, 1)


def main():
    os.makedirs(OUT, exist_ok=True)
    items = []

    def save(name, frames, walk, fps, note):
        Image.fromarray(sheet(frames)).save(os.path.join(OUT, name + ".png"))
        items.append({"name": name, "path": f"Assets/_Game/Art/Characters/Ref/{name}.png", "w": FW, "h": FH,
                      "frames": len(frames), "pivotX": FW // 2, "pivotY": FEET, "fps": fps,
                      "walkFrames": walk, "layout": "v2", "note": note})

    def load(fname):
        return np.array(Image.open(os.path.join(REF, fname)).convert("RGBA"))

    mage = load("Chibi_Mage_Pixel_Art Sprite_Sheet.png")
    fr, walk = frames_for(mage, mask_mage(mage), {"down": 5, "up": 5, "side": 5})
    save("CHR_witch_ref", clean(quantize(fr)), walk, 8, "player witch, from Chibi_Mage sheet")

    knight = load("Chibi_Knight_Pixel_Art Sprite_Sheet.png")
    fr, walk = frames_for(knight, mask_knight(knight), {"down": 4, "up": 4, "side": 4})
    save("NPC_prince_ref", clean(quantize(fr)), walk, 6, "prince NPC, from Chibi_Knight sheet")

    vil = load("Chibi_Villager_Pixel_Sprite_Sheet.png")
    fr, walk = frames_for(vil, mask_alpha(vil), {"down": 5, "up": 5, "side": 5})
    for spec in VILLAGERS:  # recolour first, then reduce: quantising first merges the olive trousers into brown
        save(f"NPC_villager_{spec[0]}_ref", clean(quantize(villager_variant(fr, spec))), walk, 6,
             "villager variant {}: hair {}, skin {}, vest {}, shirt {}, pants {}, head {}, apron {}".format(*spec))

    with open(MANIFEST, "w", encoding="utf-8") as fp:
        json.dump({"status": "Needs Cleanup (draft cut from references/tilesets chibi sheets)", "items": items},
                  fp, ensure_ascii=False, indent=1)
    if PREVIEW:
        os.makedirs(PREVIEW, exist_ok=True)
        for it in items:
            img = Image.open(os.path.join(ROOT, it["path"]))
            n = it["frames"]
            grid = Image.new("RGBA", (FW * 12, FH * 3), (70, 110, 70, 255))
            for i in range(n):
                fr_ = img.crop((i * FW, 0, (i + 1) * FW, FH))
                grid.alpha_composite(fr_, ((i % 12) * FW, (i // 12) * FH))
            grid.resize((grid.width * 3, grid.height * 3), Image.NEAREST).save(os.path.join(PREVIEW, it["name"] + ".png"))
    print("wrote", len(items), "sheets")


if __name__ == "__main__":
    main()
