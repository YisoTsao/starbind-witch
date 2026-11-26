"""Moonveil Forest (紫幻森林) creature and mechanism art, drawn in code (PROC-DRAFT, nothing cut from references).

  boss_frames()      Eclipse Mushroom King (月蝕菇王), purple Moonveil version, 96x96 x6: idle 0-3, windup 4-5
  vine_sprites()     burnable vine strand (32x44): idle, burn 0-2, burnt
  lever_sprites()    bridge lever (24x32): off, on
  shadow_sprites()   ground-contact shadows (S/M/L/XL) for props

Palette: BIOME_DESIGN.md §2.1 (violet canopy, lilac glow, warm gold accents, blue-violet shadows).
Used by tools/art/moonveil_art.py (vine/lever/shadows -> moonveil_manifest.json) and
tools/art/gen_game_drafts.py (boss sheet, ENM_mushroom_king_draft.png keeps its name and import settings).
"""
import math
import numpy as np

from pixlib import c, canvas, px, rect, ellipse, shaded_ellipse, poly, line, outline, strip, polar

OUTLINE = c("#1e1030")


def _ramp(img, mask, ramp, shade):
    """Fill `mask` with ramp[idx] where idx = shade (0 light .. 1 dark) quantised to the ramp."""
    idx = np.clip((shade * len(ramp)).astype(int), 0, len(ramp) - 1)
    for i, col in enumerate(ramp):
        img[mask & (idx == i)] = col


def _grid(img):
    H, W = img.shape[:2]
    ys, xs = np.mgrid[0:H, 0:W]
    return xs + 0.5, ys + 0.5


# =============================================================== boss
CAP = [c("#f0d2ff"), c("#d3a2f4"), c("#b077e4"), c("#8c52cc"), c("#6a37a8"), c("#4c2582"), c("#341760")]
GILL = [c("#5a2e8a"), c("#43206c"), c("#2e1450")]
STEM = [c("#f1e6f6"), c("#d9c6ea"), c("#b6a0d2"), c("#8e78b2"), c("#68548c")]
ROOT = [c("#8a6a9a"), c("#644a78"), c("#432f56")]
MOSS = [c("#7d8fd0"), c("#5a64a8"), c("#3e4280")]
GOLD = [c("#fff3c0"), c("#ffd86a"), c("#d89a3a")]
GLOW = [c("#ffffff"), c("#d8f6ff"), c("#9fe2f4"), c("#6fb6e6")]
PINKG = [c("#ffe0f6"), c("#f4a6e0"), c("#d26ac0")]


def _boss_frame(i):
    img = canvas(96, 96)
    xs, ys = _grid(img)
    windup = i >= 4
    bob = [0, -1, -2, -1, 1, 2][i]                     # body squash/bob
    lift = [0, 0, -1, 0, -4, -5][i]                    # cap rises during the windup
    sway = [0, 1, 0, -1, 0, 0][i]                      # cap sways in idle

    # --- roots / feet (drawn first, the stem covers their tops)
    for k, (x0, x1) in enumerate(((38, 24), (44, 36), (52, 60), (58, 72))):
        line(img, x0, 82, x1, 92, ROOT[1], 4)
        line(img, x0 - 1, 82, x1 - 1, 91, ROOT[0], 1)
        px(img, x1 + (-1 if x1 < 48 else 1), 92, ROOT[2])

    # --- stem body: bulb, lit from the upper left
    scx, scy = 48, 70 + bob
    body = ((xs - scx) / 19) ** 2 + ((ys - scy) / 21) ** 2 <= 1
    body &= ys < 89
    sh = np.clip(0.45 + (xs - scx) / 38 + (ys - scy) / 70, 0, 0.999)
    _ramp(img, body, STEM, sh)
    # vertical fibre lines on the stem
    for fx in (38, 44, 53, 58):
        for yy in range(60 + bob, 86, 2):
            if img[yy, fx, 3]:
                img[yy, fx] = STEM[min(4, 2 + (fx > 50))]

    # --- arms (root arms): down in idle, raised during the windup
    for side in (-1, 1):
        sx = scx + side * 16
        if windup:
            pts = [(sx, 68 + bob), (sx + side * 9, 60 + bob), (sx + side * 12, 48 + bob)]
        else:
            pts = [(sx, 68 + bob), (sx + side * 8, 74 + bob + (i % 2)), (sx + side * 9, 82 + bob)]
        for a, b in zip(pts, pts[1:]):
            line(img, a[0], a[1], b[0], b[1], ROOT[1], 3)
            line(img, a[0], a[1] - 1, b[0], b[1] - 1, ROOT[0], 1)
        hx, hy = pts[-1]
        for k in range(3):                                    # three twig fingers
            ang = (-math.pi / 2 if windup else math.pi / 2) + (k - 1) * 0.55
            fx, fy = polar(hx, hy, 3.2, ang)
            line(img, hx, hy, fx, fy, ROOT[2], 1)

    # --- face (on the stem)
    ey = 67 + bob
    eye = PINKG if windup else GOLD
    for ex in (41, 55):
        ellipse(img, ex, ey, 3.6, 2.2 if not windup else 1.6, OUTLINE)
        ellipse(img, ex, ey, 2.6, 1.4 if not windup else 1.0, eye[1])
        px(img, ex - 1, ey - 1, eye[0])
    for ex, d in ((41, 1), (55, -1)):                         # angry brows
        line(img, ex - 3 * d, ey - 5, ex + 3 * d, ey - 3, STEM[4], 1)
    mh = 3.2 if windup else 1.6
    ellipse(img, 48, 76 + bob, 6.5, mh, c("#2a0f3c"))
    if windup:                                                 # fangs
        for tx in (44, 47, 50, 53):
            px(img, tx, 74 + bob, STEM[0]); px(img, tx, 75 + bob, STEM[1])
    # blush glow under the eyes
    for bx in (37, 59):
        px(img, bx, ey + 3, PINKG[2]); px(img, bx + 1, ey + 3, PINKG[2])

    # --- moss collar where cap meets stem
    for k in range(13):
        mx = 30 + k * 3
        my = 56 + bob + (k % 2)
        ellipse(img, mx, my, 2.6, 2.2, MOSS[1 + (k % 2)])
        px(img, mx - 1, my - 1, MOSS[0])

    # --- cap
    ccx, ccy = 48 + sway, 34 + lift + bob
    rx, ry = 45, 27
    dx, dy = (xs - ccx) / rx, (ys - ccy) / ry
    lower = ccy + 12 - 7 * dx * dx                         # underside curves up toward the rim
    dome = (dx * dx + dy * dy <= 1) & (ys <= lower)
    # gills: the flattened underside, a dark band with radial lines
    under = ((xs - ccx) / (rx - 2)) ** 2 + ((ys - (ccy + 12)) / 6.5) ** 2 <= 1
    under &= ys > ccy + 9
    img[under] = GILL[1]
    ang = np.arctan2(ys - (ccy + 6), xs - ccx)
    rays = (np.abs(np.sin(ang * 14)) < 0.22) & under
    img[rays] = GILL[0]
    img[under & (ys > ccy + 15)] = GILL[2]
    # dome shading: light from the upper left, darker toward the rim
    nz = np.sqrt(np.clip(1 - dx * dx - dy * dy, 0, 1))
    lit = -0.55 * dx - 0.62 * dy + 0.58 * nz
    shade = np.clip(0.62 - lit * 0.62, 0, 0.999)
    _ramp(img, dome, CAP, shade)
    # rim: magenta lip along the lower edge of the dome
    rim = dome & (ys > lower - 4)
    img[rim] = c("#a3479e")
    img[rim & (xs < ccx - 10)] = c("#c463b6")
    # specular streak (upper left)
    streak = ((xs - (ccx - 18)) / 9) ** 2 + ((ys - (ccy - 14)) / 4) ** 2 <= 1
    img[streak & dome] = CAP[0]

    # glowing spots (skip the emblem area)
    spots = [(-30, 2, 4.5, 3.2), (-20, -12, 3.8, 2.8), (21, -11, 4.0, 3.0), (31, 3, 4.6, 3.2),
             (-9, 3, 3.0, 2.2), (11, 4, 3.0, 2.2), (-37, -3, 2.4, 2.0), (38, -4, 2.4, 2.0)]
    for k, (ox, oy, sx, sy) in enumerate(spots):
        x, y = ccx + ox, ccy + oy
        m = ellipse(img, x, y, sx, sy, GLOW[3], mask=dome)
        ellipse(img, x - 0.6, y - 0.6, sx * 0.7, sy * 0.65, GLOW[2] if not windup else PINKG[1], mask=m)
        px(img, x - sx * 0.35, y - sy * 0.35, GLOW[0])

    # eclipse emblem: dark moon disc with a gold corona, slightly offset crescent highlight
    ex, ey2 = ccx, ccy - 9
    corona = ellipse(img, ex, ey2, 8.6, 8.6, GOLD[2], mask=dome)
    ellipse(img, ex, ey2, 7.6, 7.6, GOLD[1], mask=corona)
    ellipse(img, ex, ey2, 6.3, 6.3, c("#1a0b2c"), mask=corona)
    ring = ((xs - ex) ** 2 + (ys - ey2) ** 2 <= 6.3 ** 2) & ((xs - ex - 2.0) ** 2 + (ys - ey2 + 1.0) ** 2 > 5.6 ** 2)
    img[ring & dome] = GOLD[0]

    # crown: five gold points on top of the cap
    top = ccy - ry + 1
    for k, ox in enumerate((-10, -5, 0, 5, 10)):
        h = 6 if k == 2 else 4 if k in (1, 3) else 3
        poly(img, [(ex + ox - 2.2, top + 3), (ex + ox, top - h), (ex + ox + 2.2, top + 3)], GOLD[1])
        px(img, ex + ox - 1, top - h + 2, GOLD[0])
    rect(img, ex - 12, top + 2, ex + 12, top + 4, GOLD[2])
    rect(img, ex - 12, top + 2, ex + 12, top + 2, GOLD[1])
    for k, ox in enumerate((-8, 0, 8)):
        px(img, ex + ox, top + 3, c("#8a5ad8") if k != 1 else PINKG[1])

    outline(img, OUTLINE)

    # windup: spore sparks around the cap (drawn outside the outline, a ring of bright motes)
    if windup:
        for k in range(10):
            a = math.pi + k * math.pi / 9
            x, y = polar(ccx, ccy + 4, 47 + (i - 4) * 2, a)
            px(img, x, y, PINKG[0]); px(img, x + 1, y, PINKG[1]); px(img, x, y + 1, PINKG[1])
    return img


def boss_frames():
    """Eclipse Mushroom King (Moonveil), 96x96 x6: idle 0-3, windup 4-5. Pivot 4 px from the bottom."""
    return strip([_boss_frame(i) for i in range(6)])


# =============================================================== vine (burnable shortcut + boss gate)
VSTEM = [c("#6a52a6"), c("#4b3a82"), c("#33285e")]
VLEAF = [c("#b7a6ee"), c("#8a7ad0"), c("#6658ac"), c("#46418a")]
VFLOW = [c("#ffe0f6"), c("#f4a6e0"), c("#c95ab8")]
FIRE = [c("#fff3c0"), c("#ffd86a"), c("#ff9a3a"), c("#e0582a"), c("#8a2a2a")]
CHAR = [c("#4a3a44"), c("#2e2230"), c("#1c1420")]


def _vine_base(seed=5):
    img = canvas(32, 44)
    r = np.random.default_rng(seed)
    strands = []
    for k in range(6):
        x0 = 4 + k * 4.6
        pts = [(x0 + math.sin(y * 0.28 + k * 1.7) * 2.2, y) for y in range(3, 44)]
        strands.append(pts)
        col = VSTEM[1] if k % 2 else VSTEM[2]
        for (x, y) in pts:
            rect(img, x, y, x + 1, y, col)
            px(img, x, y, VSTEM[0] if y % 3 == 0 else col)
    # leaves: small 3-tone hearts along the strands
    for pts in strands:
        for _ in range(4):
            x, y = pts[int(r.integers(2, len(pts) - 4))]
            side = 1 if r.random() < 0.5 else -1
            lx = x + side * 2
            ellipse(img, lx, y, 2.2, 1.6, VLEAF[2])
            px(img, lx - 1, y - 1, VLEAF[0]); px(img, lx, y - 1, VLEAF[1])
            px(img, lx + side, y + 1, VLEAF[3])
    # thorns
    for pts in strands[::2]:
        for y in range(8, 40, 7):
            x, _ = pts[y - 3]
            px(img, x + 2, y, c("#d8c8f0"))
    # glowing flowers
    for _ in range(5):
        x, y = r.uniform(5, 27), r.uniform(5, 38)
        ellipse(img, x, y, 1.6, 1.6, VFLOW[1])
        px(img, x, y, VFLOW[0]); px(img, x + 1, y + 1, VFLOW[2])
    return img


def vine_sprites():
    """Returns dict name -> image: vine, vine_burn_0..2, vine_burnt (all 32x44, pivot bottom)."""
    base = _vine_base()
    out = {"vine": outline(base.copy(), OUTLINE)}
    H = base.shape[0]
    r = np.random.default_rng(17)
    for f in range(3):
        img = base.copy()
        burn_to = H - int(H * (0.35 + 0.3 * f))           # flame front rises
        a = img[..., 3] > 0
        ys = np.arange(H)[:, None] * np.ones((1, 32))
        charred = a & (ys >= burn_to + 6)
        img[charred] = CHAR[1]
        img[a & (ys >= burn_to + 6) & ((np.arange(32)[None, :] + ys.astype(int)) % 3 == 0)] = CHAR[0]
        front = a & (ys >= burn_to) & (ys < burn_to + 6)
        img[front] = FIRE[2]
        # flames licking upward from the front
        for _ in range(9):
            x = int(r.integers(3, 29))
            h = int(r.integers(4, 10))
            for k in range(h):
                yy = burn_to - k
                if 0 <= yy < H:
                    col = FIRE[0] if k > h - 2 else FIRE[1] if k > h // 2 else FIRE[2]
                    w = 1 if k > h // 2 else 2
                    rect(img, x - (w // 2), yy, x + (w - 1) // 2 + (f % 2), yy, col)
        # embers
        for _ in range(6):
            px(img, r.integers(2, 30), r.integers(max(0, burn_to - 16), burn_to), FIRE[1])
        out[f"vine_burn_{f}"] = outline(img, OUTLINE)
    burnt = canvas(32, 44)
    for k in range(6):                                    # charred stubs and ash
        x0 = 4 + k * 4.6
        h = 5 + (k * 7) % 6
        rect(burnt, x0, 43 - h, x0 + 1, 43, CHAR[1])
        px(burnt, x0, 43 - h, CHAR[0])
    for x in range(2, 30, 3):
        px(burnt, x, 43, CHAR[2]); px(burnt, x + 1, 42, c("#5a4a56"))
    px(burnt, 9, 40, FIRE[2]); px(burnt, 22, 41, FIRE[3])
    out["vine_burnt"] = outline(burnt, OUTLINE)
    return out


# =============================================================== lever
def lever_sprites():
    """dict: lever (off, crystal dim, handle left), lever_on (crystal lit, handle right). 24x32, pivot bottom."""
    out = {}
    stone = [c("#cfc6e6"), c("#a59cc6"), c("#7c74a2"), c("#58507c"), c("#3c3558")]
    wood = [c("#b08458"), c("#7a5236"), c("#553824")]
    for name, on in (("lever", False), ("lever_on", True)):
        img = canvas(24, 32)
        # carved stone base (3/4 view: top face + front face)
        rect(img, 3, 22, 20, 30, stone[2]); rect(img, 3, 22, 4, 30, stone[1]); rect(img, 19, 22, 20, 30, stone[3])
        rect(img, 3, 30, 20, 30, stone[4])
        ellipse(img, 12, 21.5, 9.5, 3.2, stone[1]); ellipse(img, 12, 21, 8, 2.4, stone[0])
        rect(img, 8, 25, 15, 25, stone[3]); px(img, 11, 27, c("#9a7ad8")); px(img, 12, 27, c("#9a7ad8"))  # moon rune
        ellipse(img, 12, 21, 3, 1.2, stone[4])                                            # slot
        # handle
        tip = (19, 7) if on else (5, 7)
        line(img, 12, 21, tip[0], tip[1], wood[1], 2)
        line(img, 12 - 1, 21, tip[0] - 1, tip[1], wood[0], 1)
        # crystal knob
        gem = [c("#ffffff"), c("#d8f6ff"), c("#7fd7ea"), c("#3a8ac0")] if on else [c("#b8a6d8"), c("#8a72b8"), c("#5e4a8a"), c("#3e2e62")]
        shaded_ellipse(img, tip[0], tip[1] - 1, 3.2, 3.6, gem)
        px(img, tip[0] - 1, tip[1] - 3, gem[0])
        # moss tuft at the base
        for x in (4, 6, 17, 19):
            px(img, x, 29, c("#5a64a8")); px(img, x, 28, c("#7d8fd0"))
        outline(img, OUTLINE)
        if on:                                            # small glow motes
            for (x, y) in ((tip[0] - 4, tip[1] - 4), (tip[0] + 3, tip[1] - 5), (tip[0] - 3, tip[1] + 3)):
                px(img, x, y, c("#d8f6ff"))
        out[name] = img
    return out


# =============================================================== ground shadows
def shadow_sprites():
    """Ground-contact shadows: flat blue-violet ellipses, two alpha steps (core + rim), hard pixel edges.
    Sizes in px: S 24x8, M 40x12, L 64x18, XL 96x26."""
    out = {}
    for name, (w, h) in {"shadow_s": (24, 8), "shadow_m": (40, 12), "shadow_l": (64, 18), "shadow_xl": (96, 26)}.items():
        img = canvas(w, h)
        outer = ellipse(img, w / 2, h / 2, w / 2, h / 2, c("#140a28", 70))
        ellipse(img, w / 2, h / 2, w / 2 * 0.72, h / 2 * 0.66, c("#140a28", 110), mask=outer)
        out[name] = img
    return out


if __name__ == "__main__":
    import os, sys
    from PIL import Image
    d = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(d, exist_ok=True)
    Image.fromarray(boss_frames()).save(os.path.join(d, "boss.png"))
    for k, v in {**vine_sprites(), **lever_sprites(), **shadow_sprites()}.items():
        Image.fromarray(v).save(os.path.join(d, k + ".png"))
