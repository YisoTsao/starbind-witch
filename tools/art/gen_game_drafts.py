"""Generate the DRAFT art for Phase 1-5 gameplay: witch (4 directions), NPCs, enemies, boss,
props (village extension, forest, workshop interior), pickups, VFX flipbooks, UI icons, and the
forest / interior tiles.

Status of every output: Needs Cleanup (procedural draft, not production art; ASSET_GAPS.md).
Nothing here is cut from the AI reference images. Palette follows reference A (ART_DIRECTION.md).
Outputs: Assets/_Game/Art/<Group>/Draft/*.png and Assets/_Game/Art/draft_manifest.json, which the
Unity builder (Starbind.Editor.GameContentBuilder) reads for frame size, frame count and pivot.

Usage: python3 tools/art/gen_game_drafts.py <repo_root>
"""
import json, math, os, sys
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixlib import *  # noqa

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
ART = os.path.join(ROOT, "Assets/_Game/Art")
MANIFEST = []

OUT = c("#2b2236")          # universal dark outline (A uses dark purple-brown outlines)
OUT_SOFT = c("#3e3048")


def save(group, name, img, fw=None, fh=None, frames=1, pivot=(0.5, 0.0), fps=10, extra=None):
    """pivot: (fraction of frame width, pixels from the bottom) or (fx, fy) fractions if fy < 1."""
    d = os.path.join(ART, group, "Draft")
    os.makedirs(d, exist_ok=True)
    Image.fromarray(img).save(os.path.join(d, name + ".png"))
    fw = fw or img.shape[1] // frames
    fh = fh or img.shape[0]
    px_ = int(round(pivot[0] * fw))
    py_ = int(round(pivot[1] * fh)) if 0 < pivot[1] < 1 else int(pivot[1])
    e = {"name": name, "path": f"Assets/_Game/Art/{group}/Draft/{name}.png", "w": fw, "h": fh,
         "frames": frames, "pivotX": px_, "pivotY": py_, "fps": fps}
    if extra:
        e.update(extra)
    MANIFEST.append(e)


# =============================================================== people (witch + NPCs)
FW, FH = 48, 48
FEET = 43  # last opaque row; pivot = 4 px from the bottom


def person(spec, facing, legs=0, bob=0, cast=False):
    img = canvas(FW, FH)
    cx = 24
    top = FEET - spec.get("height", 40) + 1 + bob
    skin, skin_d = spec["skin"], spec["skin_d"]
    body = spec["body"]  # light, mid, dark
    side = facing == "side"
    back = facing == "up"
    # ---- legs / boots
    boots = spec.get("boots", c("#4a3445"))
    for sgn, off in ((-1, legs), (1, -legs)):
        lx = cx + sgn * (2 if not side else 0) + (off if side else 0)
        ly = FEET - 4 + (0 if side else (-abs(off) // 2 if off * sgn > 0 else 0))
        rect(img, lx - 1, ly, lx + 1 - (1 if side else 0), FEET, boots)
    # ---- robe / body (trapezoid)
    b_top = top + spec.get("head", 11) + 3
    b_bot = FEET - 4
    wt, wb = spec.get("shoulder", 6), spec.get("hem", 9)
    if side:
        wt, wb = wt - 2, wb - 2
    pts = [(cx - wt, b_top), (cx + wt + 1, b_top), (cx + wb + 1, b_bot + 1), (cx - wb, b_bot + 1)]
    poly(img, pts, body[1])
    # shading: left lit, right dark strip
    poly(img, [(cx - wt, b_top), (cx - wt + 2, b_top), (cx - wb + 2, b_bot + 1), (cx - wb, b_bot + 1)], body[0])
    poly(img, [(cx + wt - 1, b_top), (cx + wt + 1, b_top), (cx + wb + 1, b_bot + 1), (cx + wb - 2, b_bot + 1)], body[2])
    if "trim" in spec:
        rect(img, cx - wb, b_bot - 1, cx + wb, b_bot, spec["trim"])
        if not back and not side:
            rect(img, cx, b_top + 2, cx, b_bot - 2, spec["trim"])
    if "belt" in spec:
        rect(img, cx - wt - 1, b_top + 6, cx + wt + 1, b_top + 6, spec["belt"])
    if "cape" in spec and (back or side):
        cw = wb + 1 if back else 3
        cx0 = cx - cw if back else cx - wt - 3
        poly(img, [(cx0, b_top), (cx0 + 2 * cw + (0 if back else 2), b_top), (cx0 + 2 * cw + 2, b_bot), (cx0 - 1, b_bot)], spec["cape"])
    # ---- arms
    arm = spec.get("arm", body[1])
    if cast:
        rect(img, cx - wt - 3, b_top - 4, cx - wt - 2, b_top + 2, arm)
        rect(img, cx + wt + 2, b_top - 4, cx + wt + 3, b_top + 2, arm)
        px(img, cx - wt - 3, b_top - 5, skin); px(img, cx + wt + 3, b_top - 5, skin)
    elif not side:
        swing = legs
        rect(img, cx - wt - 2, b_top + 1 + max(0, swing), cx - wt - 1, b_top + 7 + max(0, swing), arm)
        rect(img, cx + wt + 1, b_top + 1 + max(0, -swing), cx + wt + 2, b_top + 7 + max(0, -swing), body[2])
        px(img, cx - wt - 2, b_top + 8 + max(0, swing), skin); px(img, cx + wt + 2, b_top + 8 + max(0, -swing), skin_d)
    else:
        rect(img, cx - 1 + legs // 2, b_top + 1, cx + 1 + legs // 2, b_top + 7, body[2])
        px(img, cx + legs // 2, b_top + 8, skin)
    # ---- head
    hh = spec.get("head", 11)
    hy = top + 3
    hcx = cx + (1 if side else 0)
    shaded_ellipse(img, hcx, hy + hh / 2, 5.5, hh / 2, [skin, skin, skin_d])
    hair = spec["hair"]
    if back:
        ellipse(img, hcx, hy + hh / 2 - 0.5, 6, hh / 2 + 0.5, hair[1])
        rect(img, hcx - 5, hy + hh // 2, hcx + 5, hy + hh + spec.get("hair_len", 3), hair[1])
        rect(img, hcx - 5, hy + hh // 2, hcx - 4, hy + hh + spec.get("hair_len", 3), hair[0])
    else:
        ellipse(img, hcx, hy + 3, 6, 4, hair[1])  # fringe
        rect(img, hcx - 6, hy + 2, hcx - 5, hy + hh + spec.get("hair_len", 3) - 1, hair[1])
        if not side:
            rect(img, hcx + 5, hy + 2, hcx + 6, hy + hh + spec.get("hair_len", 3) - 1, hair[2])
        px(img, hcx - 3, hy + 2, hair[0]); px(img, hcx - 2, hy + 2, hair[0])
        # eyes
        ey = hy + hh // 2 + 1
        eye = spec.get("eye", c("#3b2a55"))
        if side:
            rect(img, hcx + 2, ey, hcx + 2, ey + 1, eye)
            px(img, hcx + 5, ey + 2, skin_d)
        else:
            rect(img, hcx - 3, ey, hcx - 3, ey + 1, eye)
            rect(img, hcx + 2, ey, hcx + 2, ey + 1, eye)
            px(img, hcx - 4, ey + 2, c("#f3a6a6")); px(img, hcx + 3, ey + 2, c("#f3a6a6"))
        if spec.get("beard"):
            rect(img, hcx - 3, hy + hh - 2, hcx + 3, hy + hh + 2, spec["beard"])
    # ---- headgear
    hat = spec.get("hat")
    if hat == "witch":
        hc = spec["hat_col"]
        brim_y = hy + 2
        ellipse(img, hcx, brim_y + 1, 11, 2.5, hc[2])
        ellipse(img, hcx, brim_y + 0.5, 10, 2, hc[1])
        tipx = hcx + (5 if not back else -5) + (3 if side else 0)
        poly(img, [(hcx - 6, brim_y + 1), (hcx + 7, brim_y + 1), (hcx + 3, brim_y - 7), (tipx, top - 2), (hcx - 2, brim_y - 8)], hc[1])
        poly(img, [(hcx - 6, brim_y + 1), (hcx - 3, brim_y + 1), (hcx - 1, brim_y - 7), (hcx - 2, brim_y - 8)], hc[0])
        rect(img, hcx - 5, brim_y - 1, hcx + 6, brim_y, spec["hat_band"])
        px(img, hcx + 1, brim_y - 1, c("#fff3a8"))
    elif hat == "helmet":
        hc = spec["hat_col"]
        shaded_ellipse(img, hcx, hy + 4, 6.5, 5.5, hc)
        if not back:
            rect(img, hcx - 5, hy + 5, hcx + 5, hy + 6, hc[2])
            rect(img, hcx - 1, hy - 1, hcx + 1, hy + 5, hc[0])
        ellipse(img, hcx, hy - 2, 1.5, 2.5, spec.get("plume", c("#d84a4a")))
    elif hat == "hood":
        hc = spec["hat_col"]
        m = ellipse(img, hcx, hy + 5, 7.5, 7.5, hc[1])
        if not back:
            ellipse(img, hcx, hy + 7, 4.5, 4.5, skin)
            ey = hy + hh // 2 + 1
            rect(img, hcx - 2, ey, hcx - 2, ey + 1, c("#2d3b2a")); rect(img, hcx + 2, ey, hcx + 2, ey + 1, c("#2d3b2a"))
        poly(img, [(hcx - 7, hy + 4), (hcx - 4, hy - 3), (hcx - 1, hy + 1)], hc[0])
    elif hat == "mitre":
        hc = spec["hat_col"]
        poly(img, [(hcx - 5, hy + 2), (hcx + 6, hy + 2), (hcx + 3, hy - 6), (hcx, hy - 8), (hcx - 3, hy - 6)], hc[0])
        rect(img, hcx, hy - 6, hcx, hy + 1, spec["hat_band"])
        rect(img, hcx - 3, hy - 3, hcx + 3, hy - 3, spec["hat_band"])
    # ---- held item
    item = spec.get("item")
    hx = cx + wt + 3 if not side else cx + 4
    if item == "staff":
        sy0 = b_top - (8 if cast else 4)
        rect(img, hx, sy0, hx, FEET - 1, c("#7a5236"))
        rect(img, hx + 1, sy0 + 1, hx + 1, FEET - 2, c("#5a3a2a"))
        shaded_ellipse(img, hx + 0.5, sy0 - 1.5, 2.6, 2.6, [c("#fdf2ff"), c("#c98cff"), c("#7f4fd0")])
    elif item == "sword":
        rect(img, hx, b_top + 2, hx, b_top + 13, c("#dfe6ef")); rect(img, hx - 1, b_top + 9, hx + 1, b_top + 9, c("#c9a24a"))
    elif item == "bow":
        for t in range(-7, 8):
            px(img, hx + 1 + int(2.5 * (1 - (t / 7) ** 2)), b_top + 5 + t, c("#8a5a34"))
        rect(img, hx, b_top - 2, hx, b_top + 12, c("#e8e0c8"))
    elif item == "book":
        rect(img, hx - 1, b_top + 4, hx + 2, b_top + 8, c("#6a3fa0")); rect(img, hx, b_top + 5, hx + 1, b_top + 7, c("#f5e7c1"))
    elif item == "orb":
        shaded_ellipse(img, hx + 0.5, b_top + 6, 2.5, 2.5, [c("#fffbe0"), c("#ffd86b"), c("#c88a2a")])
    outline(img, OUT)
    return img


WITCH = dict(
    skin=c("#f7d9c4"), skin_d=c("#e2b29c"), hair=[c("#e9e4f7"), c("#c8bde6"), c("#9c8fc4")],
    body=[c("#8a5fcf"), c("#6a44ad"), c("#4d2f86")], trim=c("#f2c45a"), belt=c("#f2c45a"),
    hat="witch", hat_col=[c("#7a52c4"), c("#5a3699"), c("#3d2470")], hat_band=c("#f2c45a"),
    cape=c("#3d2470"), item="staff", height=40, hair_len=4, boots=c("#3d2a3a"))

NPCS = {
    "knight": dict(skin=c("#f0c9a8"), skin_d=c("#d4a283"), hair=[c("#8a6a4a"), c("#6a4a32"), c("#4a3222")],
                   body=[c("#dfe6ef"), c("#a9b6c8"), c("#6f7f99")], trim=c("#3b6fc4"), belt=c("#7a5236"),
                   hat="helmet", hat_col=[c("#f2f5fa"), c("#b9c4d6"), c("#7f8ca6")], plume=c("#3b6fc4"),
                   cape=c("#2f5aa8"), item="sword", height=42, shoulder=7, hem=8, hair_len=0),
    "priest": dict(skin=c("#f7d9c4"), skin_d=c("#e2b29c"), hair=[c("#fff2c8"), c("#f0d890"), c("#c8a860")],
                   body=[c("#ffffff"), c("#e8e4f0"), c("#b8b0cc")], trim=c("#e0b040"), belt=c("#e0b040"),
                   hat="mitre", hat_col=[c("#ffffff"), c("#e8e4f0"), c("#b8b0cc")], hat_band=c("#e0b040"),
                   item="orb", height=40, hair_len=6, hem=10),
    "archer": dict(skin=c("#e8c0a0"), skin_d=c("#c99a7c"), hair=[c("#c46a3a"), c("#9a4a28"), c("#6a3018")],
                   body=[c("#7fb060"), c("#5a8a44"), c("#3d6430")], belt=c("#7a5236"),
                   hat="hood", hat_col=[c("#6a9a50"), c("#4a7a38"), c("#30562a")], cape=c("#30562a"),
                   item="bow", height=40, shoulder=5, hem=7),
    "ella": dict(skin=c("#f0d0bc"), skin_d=c("#d0a890"), hair=[c("#ffffff"), c("#dcdce8"), c("#a8a8bc")],
                 body=[c("#9c7ab8"), c("#7a5a9a"), c("#584078")], trim=c("#f2c45a"),
                 hat="witch", hat_col=[c("#5a6aa8"), c("#3e4c88"), c("#2a3466")], hat_band=c("#c8a860"),
                 item="book", height=38, hair_len=7, hem=10),
}


def person_sheet(spec):
    """Row order (left to right, each 48x48):
    down: idle, walk1..4, cast | up: idle, walk1..4, cast | side(right): idle, walk1..4, cast"""
    frames = []
    for facing in ("down", "up", "side"):
        frames.append(person(spec, facing))
        for legs, bob in ((2, 0), (0, -1), (-2, 0), (0, -1)):
            frames.append(person(spec, facing, legs, bob))
        frames.append(person(spec, facing, cast=True))
    return strip(frames)


# =============================================================== enemies
def slime_frames():
    ramp = [c("#c8e87a"), c("#8cc850"), c("#5a9a3a"), c("#3c6a2e")]
    out = []
    for i in range(4):
        img = canvas(28, 24)
        sq = [0, 1.5, 0, -1.5][i]
        rx, ry = 10 + sq, 8 - sq * 0.8
        cy = 22 - ry
        shaded_ellipse(img, 14, cy, rx, ry, ramp)
        # moss on top
        for x in range(int(14 - rx + 3), int(14 + rx - 2), 3):
            px(img, x, cy - ry + 1, c("#3c7a2e")); px(img, x + 1, cy - ry + 2, c("#4c8a34"))
        ey = int(cy) - 1
        rect(img, 10, ey, 11, ey + 2, c("#1d2a1a")); rect(img, 17, ey, 18, ey + 2, c("#1d2a1a"))
        px(img, 10, ey, c("#ffffff")); px(img, 17, ey, c("#ffffff"))
        px(img, 8, int(cy) - 3, c("#f0ffd0"))
        outline(img, OUT)
        out.append(img)
    return strip(out)


def shroom(w, h, cap_ramp, spots, stem, eye, frames=4, scale=1.0, angry=False):
    out = []
    for i in range(frames):
        img = canvas(w, h)
        bob = [0, -1, 0, 1][i % 4]
        cx = w / 2
        sh = int(h * 0.42)
        # cap
        capcy = h - sh - 2 + bob
        m = shaded_ellipse(img, cx, capcy, w * 0.46, h * 0.28, cap_ramp)
        # flatten cap bottom
        rect(img, cx - w * 0.44, capcy + 1, cx + w * 0.44, capcy + h * 0.3, np.zeros(4, np.uint8))
        rect(img, cx - w * 0.42, capcy, cx + w * 0.42, capcy + 1, cap_ramp[-1])
        r = np.random.default_rng(3)
        for _ in range(spots):
            sx = cx + r.uniform(-w * 0.32, w * 0.32)
            sy = capcy - r.uniform(1, h * 0.22)
            if img[int(sy), int(sx), 3]:
                ellipse(img, sx, sy, 1.6 * scale, 1.2 * scale, c("#fbe8ff"))
        # stem
        sw = int(w * 0.22)
        rect(img, cx - sw, h - sh - 1 + bob, cx + sw - 1, h - 2, stem[1])
        rect(img, cx - sw, h - sh - 1 + bob, cx - sw + 1, h - 2, stem[0])
        rect(img, cx + sw - 2, h - sh - 1 + bob, cx + sw - 1, h - 2, stem[2])
        # feet
        rect(img, cx - sw - 1, h - 2, cx - 2, h - 2, stem[2]); rect(img, cx + 1, h - 2, cx + sw, h - 2, stem[2])
        # face on the stem
        fy = h - sh + 3 + bob
        rect(img, cx - 3, fy, cx - 3, fy + 1 + (0 if not angry else -1), eye)
        rect(img, cx + 2, fy, cx + 2, fy + 1 + (0 if not angry else -1), eye)
        if angry:
            px(img, cx - 4, fy - 1, eye); px(img, cx + 3, fy - 1, eye)
        outline(img, OUT)
        out.append(img)
    return strip(out)


def root_guard_frames():
    out = []
    bark = [c("#a07a52"), c("#7a5a3a"), c("#5a3e28"), c("#3e2a1c")]
    for i in range(4):
        img = canvas(44, 48)
        bob = [0, -1, 0, -1][i]
        # legs (roots)
        for k, x in enumerate((12, 18, 26, 32)):
            lift = (i + k) % 2
            line(img, x, 36 + bob, x + (-3 if x < 22 else 3), 46 - lift, bark[2], 3)
        # trunk body
        shaded_ellipse(img, 22, 26 + bob, 14, 15, bark)
        # moss shoulders
        ellipse(img, 13, 15 + bob, 6, 4, c("#5a8a3a")); ellipse(img, 31, 15 + bob, 6, 4, c("#4c7a30"))
        ellipse(img, 22, 11 + bob, 7, 4, c("#6a9a44"))
        # arms
        line(img, 8, 22 + bob, 3, 34 + bob, bark[1], 4); line(img, 36, 22 + bob, 41, 34 + bob, bark[2], 4)
        # glowing eyes + mouth
        rect(img, 16, 22 + bob, 18, 23 + bob, c("#ffd84a")); rect(img, 26, 22 + bob, 28, 23 + bob, c("#ffd84a"))
        rect(img, 18, 29 + bob, 26, 30 + bob, c("#2a1a10"))
        # bark grain
        for y in range(16, 38, 4):
            px(img, 14 + (y % 7), y + bob, bark[3]); px(img, 28 - (y % 5), y + 1 + bob, bark[3])
        outline(img, OUT)
        out.append(img)
    return strip(out)


def wisp_frames():
    out = []
    for i in range(4):
        img = canvas(20, 22)
        a = i * math.pi / 2
        # tail
        for k in range(6):
            x = 10 + math.sin(a + k * 0.8) * (k * 0.4)
            ellipse(img, x, 12 + k * 1.6, 3.5 - k * 0.5, 2, [c("#7fd7ea"), c("#5ab8e0"), c("#3a8ac0"), c("#2a6aa0"), c("#2a5a90"), c("#2a4a80")][k])
        shaded_ellipse(img, 10, 9, 6, 6, [c("#ffffff"), c("#c8f4ff"), c("#7fd7ea"), c("#4aa8d8")])
        rect(img, 7, 8, 8, 9, c("#1d2a4a")); rect(img, 12, 8, 13, 9, c("#1d2a4a"))
        outline(img, c("#1d3a5a"))
        out.append(img)
    return strip(out)


def boss_frames():
    """Eclipse Mushroom King, 96x96: idle x4 + windup x2 (frames 4-5). Moonveil purple version lives in
    moonveil_creatures.py (written by moonveil_art.py); this is the older white draft kept for reference."""
    import moonveil_creatures
    return moonveil_creatures.boss_frames()


def boss_frames_v1():
    out = []
    cap = [c("#c8a0f0"), c("#9a6ad0"), c("#6a3ea8"), c("#48287a"), c("#301a58")]
    stem = [c("#f4ead8"), c("#dccab0"), c("#b49c80")]
    for i in range(6):
        img = canvas(96, 96)
        windup = i >= 4
        bob = [0, -1, -2, -1, 2, 3][i]
        # cap
        ccy = 40 + bob - (3 if windup else 0)
        shaded_ellipse(img, 48, ccy, 44, 24, cap)
        rect(img, 4, ccy + 8, 92, ccy + 30, np.zeros(4, np.uint8))
        rect(img, 6, ccy + 6, 90, ccy + 8, cap[3])
        # eclipse moon crown + spots
        ellipse(img, 48, ccy - 16, 9, 9, c("#fff3c0")); ellipse(img, 52, ccy - 18, 8, 8, cap[2])
        r = np.random.default_rng(9)
        for _ in range(9):
            sx, sy = 48 + r.uniform(-34, 34), ccy - r.uniform(-2, 14)
            if img[int(sy), int(sx), 3] and abs(sx - 50) > 9:
                ellipse(img, sx, sy, 3.5, 2.5, c("#f0d8ff"))
        # roots/feet
        for x in (30, 40, 56, 66):
            line(img, x, 82, x + (-5 if x < 48 else 5), 93, c("#7a5a3a"), 4)
        # stem body
        poly(img, [(32, 50 + bob), (64, 50 + bob), (70, 90), (26, 90)], stem[1])
        poly(img, [(32, 50 + bob), (38, 50 + bob), (32, 90), (26, 90)], stem[0])
        poly(img, [(58, 50 + bob), (64, 50 + bob), (70, 90), (64, 90)], stem[2])
        # gill skirt
        for x in range(30, 67, 3):
            line(img, 48, 52 + bob, x, 58 + bob, c("#c8b0a0"))
        # face
        ey = 64 + bob
        eyec = c("#ff4a6a") if windup else c("#ffd84a")
        ellipse(img, 40, ey, 3.5, 2.5 if not windup else 1.5, eyec); ellipse(img, 56, ey, 3.5, 2.5 if not windup else 1.5, eyec)
        rect(img, 41, ey - 4, 44, ey - 4, c("#4a3020")); rect(img, 52, ey - 4, 55, ey - 4, c("#4a3020"))
        ellipse(img, 48, 74 + bob, 6, 2.5 if not windup else 4, c("#3a1a2a"))
        if windup:
            for k in range(8):
                x, y = polar(48, ccy - 4, 46, math.pi + k * math.pi / 7)
                px(img, x, y, c("#ff8ad0"))
        outline(img, OUT)
        out.append(img)
    return strip(out)


def dummy(kind):
    img = canvas(28, 40)
    if kind == "dummy":
        rect(img, 13, 18, 14, 39, c("#7a5236"))
        rect(img, 4, 18, 23, 20, c("#8a6240"))
        shaded_ellipse(img, 14, 24, 7, 9, [c("#f4dc8a"), c("#d8b860"), c("#b0904a")])
        shaded_ellipse(img, 14, 11, 5.5, 5.5, [c("#f4dc8a"), c("#d8b860"), c("#b0904a")])
        rect(img, 8, 27, 20, 28, c("#8a5a34"))
        px(img, 12, 10, c("#5a3a2a")); px(img, 16, 10, c("#5a3a2a"))
        rect(img, 12, 37, 16, 39, c("#5a3a2a"))
    else:  # archery target
        rect(img, 6, 22, 7, 39, c("#7a5236")); rect(img, 20, 22, 21, 39, c("#7a5236"))
        ellipse(img, 14, 16, 12, 12, c("#f4ead8"))
        ellipse(img, 14, 16, 9, 9, c("#d84a4a"))
        ellipse(img, 14, 16, 6, 6, c("#f4ead8"))
        ellipse(img, 14, 16, 3, 3, c("#d84a4a"))
    outline(img, OUT)
    return img


# =============================================================== props
def tree(w, h, canopy, trunk, seed, kind="round"):
    img = canvas(w, h)
    cx = w / 2
    tw = max(3, w // 9)
    rect(img, cx - tw, h - h * 0.35, cx + tw - 1, h - 2, trunk[1])
    rect(img, cx - tw, h - h * 0.35, cx - tw + 1, h - 2, trunk[0])
    rect(img, cx + tw - 2, h - h * 0.35, cx + tw - 1, h - 2, trunk[2])
    rect(img, cx - tw - 2, h - 2, cx + tw + 1, h - 2, trunk[2])
    r = np.random.default_rng(seed)
    if kind == "pine":
        for k in range(4):
            y = h * 0.12 + k * h * 0.17
            ww = w * (0.18 + k * 0.1)
            poly(img, [(cx, y - h * 0.1), (cx + ww, y + h * 0.16), (cx - ww, y + h * 0.16)], canopy[1])
            poly(img, [(cx, y - h * 0.1), (cx - ww * 0.2, y + h * 0.16), (cx - ww, y + h * 0.16)], canopy[0])
            rect(img, cx - ww + 1, y + h * 0.15, cx + ww - 1, y + h * 0.16, canopy[2])
    else:
        blobs = [(cx, h * 0.38, w * 0.42, h * 0.3)]
        for _ in range(6):
            blobs.append((cx + r.uniform(-w * 0.25, w * 0.25), h * 0.3 + r.uniform(-h * 0.12, h * 0.15), w * r.uniform(0.18, 0.26), h * r.uniform(0.14, 0.2)))
        for bx, by, rx, ry in blobs:
            shaded_ellipse(img, bx, by, rx, ry, canopy)
        for _ in range(int(w * h / 90)):
            x, y = r.integers(2, w - 2), r.integers(2, int(h * 0.65))
            if img[y, x, 3] and img[y - 1, x, 3]:
                img[y, x] = canopy[0] if r.random() < 0.5 else canopy[2]
    outline(img, OUT)
    return img


def simple_prop(name):
    """Small props drawn by hand-coded shapes; returns (img, pivot_px_from_bottom)."""
    if name == "pillar":
        img = canvas(28, 60)
        st = [c("#c8c4d8"), c("#9a96b4"), c("#6e6a8a"), c("#4c4866")]
        rect(img, 6, 8, 21, 55, st[1]); rect(img, 6, 8, 8, 55, st[0]); rect(img, 19, 8, 21, 55, st[2])
        rect(img, 3, 3, 24, 8, st[1]); rect(img, 3, 3, 24, 4, st[0])
        rect(img, 3, 55, 24, 59, st[2]); rect(img, 3, 58, 24, 59, st[3])
        for y in (18, 31, 44):
            rect(img, 7, y, 20, y, st[3])
        ellipse(img, 14, 26, 3, 4, c("#7fd7ea")); px(img, 13, 25, c("#ffffff"))
        for y in range(12, 50, 5):
            px(img, 9 + (y % 3), y, c("#5a8a3a"))
        return outline(img, OUT), 2
    if name == "altar":
        img = canvas(64, 48)
        st = [c("#d8d4e8"), c("#a8a4c4"), c("#78749a"), c("#504c70")]
        ellipse(img, 32, 36, 30, 10, st[2]); ellipse(img, 32, 34, 28, 9, st[1]); ellipse(img, 32, 33, 24, 7, st[0])
        rect(img, 24, 14, 39, 33, st[1]); rect(img, 24, 14, 26, 33, st[0]); rect(img, 37, 14, 39, 33, st[2])
        rect(img, 20, 10, 43, 14, st[1]); rect(img, 20, 10, 43, 10, st[0])
        poly(img, [(32, 0), (37, 6), (32, 12), (27, 6)], c("#f2c45a")); poly(img, [(32, 2), (35, 6), (32, 10), (29, 6)], c("#fff3a8"))
        for k in range(5):
            x, y = polar(32, 34, 24, k * 2 * math.pi / 5)
            px(img, x, y, c("#c98cff"))
        return outline(img, OUT), 6
    if name in ("chest", "chest_open"):
        img = canvas(30, 26)
        wd = [c("#c8884a"), c("#a0683a"), c("#784a28")]
        rect(img, 2, 10, 27, 24, wd[1]); rect(img, 2, 10, 4, 24, wd[0]); rect(img, 25, 10, 27, 24, wd[2])
        rect(img, 2, 15, 27, 16, c("#e8c060"))
        if name == "chest":
            ellipse(img, 15, 10, 13, 6, wd[0]); rect(img, 2, 10, 27, 12, wd[1])
            rect(img, 13, 13, 16, 18, c("#f2d070")); px(img, 14, 16, c("#4a3020"))
        else:
            rect(img, 2, 2, 27, 8, wd[2]); rect(img, 4, 9, 25, 12, c("#2a1a10"))
            for k in range(5):
                px(img, 7 + k * 4, 10, c("#fff3a8"))
        return outline(img, OUT), 1
    if name in ("vine", "vine_burnt"):
        img = canvas(32, 44)
        r = np.random.default_rng(5)
        if name == "vine":
            for k in range(7):
                x0 = 3 + k * 4.3
                pts = [(x0 + math.sin(y * 0.3 + k) * 2, y) for y in range(2, 43)]
                for (x, y) in pts:
                    rect(img, x, y, x + 1, y, c("#3c6a2e") if k % 2 else c("#4c8a34"))
                for _ in range(5):
                    y = r.integers(4, 40)
                    ellipse(img, x0 + 2, y, 2, 1.5, c("#7fb060"))
            for _ in range(6):
                ellipse(img, r.uniform(4, 28), r.uniform(4, 40), 1.5, 1.5, c("#d86ab0"))
        else:
            for k in range(7):
                x0 = 3 + k * 4.3
                for y in range(30 + (k % 3) * 3, 43):
                    rect(img, x0 + math.sin(y * 0.3 + k) * 2, y, x0 + 1 + math.sin(y * 0.3 + k) * 2, y, c("#3a2a24"))
        return outline(img, OUT), 1
    if name in ("lever", "lever_on"):
        img = canvas(20, 26)
        rect(img, 3, 18, 16, 24, c("#78749a")); rect(img, 3, 18, 16, 19, c("#a8a4c4"))
        if name == "lever":
            line(img, 9, 19, 4, 6, c("#7a5236"), 2); ellipse(img, 4, 5, 2.5, 2.5, c("#d84a4a"))
        else:
            line(img, 10, 19, 15, 6, c("#7a5236"), 2); ellipse(img, 15, 5, 2.5, 2.5, c("#5ad84a"))
        return outline(img, OUT), 1
    if name == "lamp":
        img = canvas(16, 50)
        rect(img, 7, 10, 8, 47, c("#4a4458")); rect(img, 4, 46, 11, 48, c("#3a3448"))
        rect(img, 3, 2, 12, 11, c("#4a4458")); rect(img, 5, 4, 10, 9, c("#ffe48a")); rect(img, 6, 5, 9, 8, c("#fff8d0"))
        return outline(img, OUT), 1
    if name == "crate":
        img = canvas(24, 24)
        rect(img, 2, 4, 21, 22, c("#b07a48")); rect(img, 2, 4, 21, 5, c("#d09a60")); rect(img, 2, 21, 21, 22, c("#7a5030"))
        line(img, 3, 6, 20, 20, c("#7a5030")); line(img, 20, 6, 3, 20, c("#7a5030"))
        return outline(img, OUT), 1
    if name == "glow_shroom":
        img = canvas(20, 18)
        for (x, h, col) in ((5, 9, c("#7fd7ea")), (12, 13, c("#c98cff")), (16, 7, c("#7fd7ea"))):
            rect(img, x - 1, 17 - h, x, 16, c("#e8e0d0"))
            ellipse(img, x - 0.5, 17 - h, 3.5, 2.5, col)
            px(img, x - 1, 16 - h, c("#ffffff"))
        return outline(img, OUT), 1
    if name == "bush":
        img = canvas(32, 24)
        for bx, by, rx in ((10, 14, 9), (21, 13, 9), (16, 9, 8)):
            shaded_ellipse(img, bx, by, rx, 8, [c("#5a9a44"), c("#3e7a34"), c("#2c5a2a")])
        for x, y in ((8, 9), (19, 7), (24, 13)):
            px(img, x, y, c("#f2c45a"))
        return outline(img, OUT), 1
    if name == "rock_dark":
        img = canvas(28, 20)
        shaded_ellipse(img, 14, 12, 12, 8, [c("#9a96b4"), c("#6e6a8a"), c("#4c4866")])
        ellipse(img, 9, 7, 4, 2, c("#5a8a3a"))
        return outline(img, OUT), 1
    if name == "log":
        img = canvas(48, 20)
        rect(img, 4, 5, 43, 16, c("#7a5236")); rect(img, 4, 5, 43, 6, c("#a07a52")); rect(img, 4, 15, 43, 16, c("#5a3a2a"))
        ellipse(img, 44, 10.5, 4, 6, c("#c8a070")); ellipse(img, 44, 10.5, 2, 3, c("#a07a52"))
        return outline(img, OUT), 1
    if name == "starplate":
        img = canvas(48, 32)
        ellipse(img, 24, 18, 22, 11, c("#504c70")); ellipse(img, 24, 17, 20, 9.5, c("#78749a"))
        ellipse(img, 24, 17, 16, 7, c("#3d2470"))
        for k in range(6):
            x, y = polar(24, 17, 12, k * math.pi / 3)
            px(img, x, y * 1, c("#f2c45a"))
        poly(img, [(24, 10), (26, 16), (24, 22), (22, 16)], c("#fff3a8"))
        return outline(img, OUT), 4
    raise KeyError(name)


def bridge_village():
    """Horizontal wooden bridge crossing a 4-tile river: 6 tiles x 2 tiles (192x64), ground decal."""
    img = canvas(192, 64)
    wd = [c("#d8a868"), c("#b8884e"), c("#8a6038"), c("#5a3e28")]
    for x in range(0, 192, 8):
        rect(img, x, 14, x + 7, 49, wd[1] if (x // 8) % 2 else wd[0])
        rect(img, x + 7, 14, x + 7, 49, wd[3])
        px(img, x + 2, 20, wd[2]); px(img, x + 4, 42, wd[2])
    rect(img, 0, 10, 191, 13, wd[2]); rect(img, 0, 10, 191, 10, wd[0])  # top rail
    rect(img, 0, 50, 191, 54, wd[2]); rect(img, 0, 54, 191, 55, wd[3])  # bottom rail
    for x in range(4, 192, 28):
        rect(img, x, 4, x + 3, 13, wd[2]); rect(img, x, 48, x + 3, 58, wd[3])
    return img


def bridge_stone(broken):
    """Vertical stone bridge for the forest (2 tiles wide x 6 tall, 64x192)."""
    img = canvas(64, 192)
    st = [c("#c8c4d8"), c("#a8a4c4"), c("#78749a"), c("#504c70")]
    for y in range(0, 192, 12):
        for x in range(10, 54, 16):
            off = 8 if (y // 12) % 2 else 0
            rect(img, x - off, y, x - off + 15, y + 11, st[1])
            rect(img, x - off, y, x - off + 15, y, st[0]); rect(img, x - off + 15, y, x - off + 15, y + 11, st[2])
    rect(img, 0, 0, 9, 191, np.zeros(4, np.uint8)); rect(img, 54, 0, 63, 191, np.zeros(4, np.uint8))
    rect(img, 6, 0, 9, 191, st[2]); rect(img, 54, 0, 57, 191, st[2])
    rect(img, 6, 0, 6, 191, st[0])
    if broken:
        rect(img, 6, 72, 57, 119, np.zeros(4, np.uint8))
        for x in range(6, 58, 3):
            px(img, x, 71 + (x % 4), st[3]); px(img, x, 120 - (x % 3), st[3])
    return img


# ---------------------------------------------------------------- interior furniture
def furniture(name):
    wd = [c("#c8884a"), c("#a0683a"), c("#784a28"), c("#4e301c")]
    if name == "bookshelf":
        img = canvas(48, 64)
        rect(img, 1, 2, 46, 63, wd[2]); rect(img, 1, 2, 46, 4, wd[0]); rect(img, 3, 6, 44, 61, wd[3])
        r = np.random.default_rng(1)
        for sy in (7, 25, 43):
            x = 4
            while x < 43:
                bw = int(r.integers(2, 5))
                bh = int(r.integers(11, 16))
                col = [c("#8a3a4a"), c("#3a5a8a"), c("#5a8a4a"), c("#c8a040"), c("#6a3fa0"), c("#d8d0c0")][r.integers(0, 6)]
                rect(img, x, sy + 16 - bh, x + bw - 1, sy + 15, col)
                px(img, x, sy + 17 - bh, c("#ffffff"))
                x += bw + (1 if r.random() < 0.3 else 0)
            rect(img, 3, sy + 16, 44, sy + 17, wd[1])
        return outline(img, OUT), 1
    if name == "bed":
        img = canvas(40, 60)
        rect(img, 2, 4, 37, 57, wd[2]); rect(img, 2, 4, 37, 10, wd[1]); rect(img, 2, 4, 37, 5, wd[0])
        rect(img, 5, 12, 34, 54, c("#f4ead8"))
        rect(img, 8, 13, 31, 20, c("#ffffff")); rect(img, 8, 20, 31, 20, c("#d8d0c0"))
        rect(img, 5, 26, 34, 54, c("#7a52c4")); rect(img, 5, 26, 34, 28, c("#9a72e4"))
        for x in range(8, 33, 6):
            px(img, x, 40, c("#f2c45a"))
        rect(img, 2, 55, 37, 59, wd[3])
        return outline(img, OUT), 1
    if name == "table":
        img = canvas(48, 36)
        rect(img, 2, 6, 45, 21, wd[1]); rect(img, 2, 6, 45, 8, wd[0]); rect(img, 2, 20, 45, 22, wd[3])
        rect(img, 4, 22, 6, 34, wd[2]); rect(img, 41, 22, 43, 34, wd[2])
        ellipse(img, 14, 11, 4, 2, c("#ffffff")); ellipse(img, 32, 12, 3, 3, c("#d84a4a"))
        rect(img, 23, 3, 24, 9, c("#f4ead8")); px(img, 23, 2, c("#ffd84a"))
        return outline(img, OUT), 1
    if name == "chair":
        img = canvas(16, 26)
        rect(img, 2, 2, 13, 13, wd[1]); rect(img, 2, 2, 13, 3, wd[0])
        rect(img, 1, 13, 14, 17, wd[0]); rect(img, 2, 18, 3, 25, wd[2]); rect(img, 12, 18, 13, 25, wd[2])
        return outline(img, OUT), 1
    if name == "cauldron":
        img = canvas(36, 34)
        line(img, 8, 30, 4, 33, c("#3a3448"), 2); line(img, 28, 30, 32, 33, c("#3a3448"), 2)
        shaded_ellipse(img, 18, 20, 15, 12, [c("#6a6480"), c("#4a4458"), c("#2e2a3a")])
        ellipse(img, 18, 11, 13, 4, c("#2e2a3a")); ellipse(img, 18, 11, 11, 3, c("#7fe07a"))
        for x, y in ((13, 10), (20, 11), (24, 9)):
            ellipse(img, x, y, 1.5, 1.2, c("#d0ffc8"))
        ellipse(img, 18, 32, 7, 2, c("#ff8a3a"))
        return outline(img, OUT), 1
    if name == "alchemy":
        img = canvas(56, 44)
        rect(img, 2, 16, 53, 41, wd[2]); rect(img, 2, 16, 53, 19, wd[0]); rect(img, 4, 22, 51, 39, wd[3])
        for i, col in enumerate((c("#ff6a8a"), c("#6ad8ff"), c("#f2d04a"), c("#7fe07a"))):
            x = 8 + i * 11
            rect(img, x, 6, x + 5, 15, col); rect(img, x + 1, 2, x + 4, 5, c("#e8f0ff")); px(img, x + 1, 8, c("#ffffff"))
        rect(img, 46, 6, 50, 15, c("#c98cff"))
        return outline(img, OUT), 1
    if name == "lectern":
        img = canvas(32, 42)
        rect(img, 13, 16, 18, 39, wd[2]); rect(img, 8, 38, 23, 41, wd[3])
        poly(img, [(3, 14), (28, 14), (25, 22), (6, 22)], wd[1])
        rect(img, 6, 8, 25, 15, c("#4d2f86")); rect(img, 8, 9, 15, 14, c("#f4ead8")); rect(img, 16, 9, 23, 14, c("#f4ead8"))
        poly(img, [(16, 0), (19, 4), (16, 8), (13, 4)], c("#c98cff")); px(img, 16, 3, c("#ffffff"))
        return outline(img, OUT), 1
    if name == "desk":
        img = canvas(48, 40)
        rect(img, 2, 12, 45, 25, wd[1]); rect(img, 2, 12, 45, 14, wd[0]); rect(img, 4, 26, 6, 38, wd[2]); rect(img, 41, 26, 43, 38, wd[2])
        rect(img, 6, 6, 17, 13, c("#f4ead8")); line(img, 8, 8, 15, 8, c("#8a7a6a")); line(img, 8, 10, 13, 10, c("#8a7a6a"))
        shaded_ellipse(img, 33, 7, 6, 6, [c("#e0f0ff"), c("#8ab8e0"), c("#4a7ab0")])
        rect(img, 30, 12, 36, 13, c("#c8a040"))
        return outline(img, OUT), 1
    if name == "wardrobe":
        img = canvas(40, 62)
        rect(img, 2, 2, 37, 59, wd[1]); rect(img, 2, 2, 37, 4, wd[0]); rect(img, 19, 6, 20, 57, wd[3])
        rect(img, 5, 7, 17, 56, wd[2]); rect(img, 22, 7, 34, 56, wd[2])
        px(img, 17, 30, c("#f2c45a")); px(img, 22, 30, c("#f2c45a"))
        return outline(img, OUT), 1
    if name == "rug":
        img = canvas(80, 52)
        ellipse(img, 40, 26, 39, 25, c("#8a3a6a")); ellipse(img, 40, 26, 34, 21, c("#c85a8a")); ellipse(img, 40, 26, 28, 16, c("#8a3a6a"))
        for k in range(8):
            x, y = polar(40, 26, 22, k * math.pi / 4)
            px(img, x, y, c("#f2c45a"))
        poly(img, [(40, 18), (44, 26), (40, 34), (36, 26)], c("#f2c45a"))
        return img, 26
    if name == "window":
        img = canvas(32, 32)
        ellipse(img, 16, 16, 13, 13, c("#7a5236")); ellipse(img, 16, 16, 11, 11, c("#bfe8ff"))
        rect(img, 15, 4, 16, 28, c("#7a5236")); rect(img, 4, 15, 28, 16, c("#7a5236"))
        px(img, 10, 10, c("#ffffff")); px(img, 11, 9, c("#ffffff"))
        return img, 16
    if name == "door_in":
        img = canvas(32, 16)
        rect(img, 2, 0, 29, 15, c("#7a5236")); rect(img, 4, 2, 27, 15, c("#a07a52"))
        rect(img, 4, 8, 27, 9, c("#c8a070"))
        return img, 8
    if name == "plant":
        img = canvas(20, 30)
        rect(img, 5, 20, 14, 28, c("#b0603a")); rect(img, 4, 19, 15, 20, c("#d07a4a"))
        for k in range(5):
            line(img, 10, 20, 10 + (k - 2) * 3, 5 + abs(k - 2) * 3, c("#4c8a34"), 2)
        return outline(img, OUT), 1
    raise KeyError(name)


# =============================================================== pickups
def pickups():
    out = {}
    img = canvas(10, 10)
    poly(img, [(5, 0), (6.5, 3.5), (10, 5), (6.5, 6.5), (5, 10), (3.5, 6.5), (0, 5), (3.5, 3.5)], c("#fff3a8"))
    poly(img, [(5, 2), (6, 4), (8, 5), (6, 6), (5, 8), (4, 6), (2, 5), (4, 4)], c("#ffffff"))
    out["PICK_xp"] = img
    for name, col in (("PICK_potion_hp", c("#e84a5a")), ("PICK_potion_mp", c("#4a8ae8"))):
        img = canvas(12, 14)
        rect(img, 4, 0, 7, 3, c("#c8a070")); rect(img, 3, 3, 8, 4, c("#e8f0ff"))
        ellipse(img, 6, 9, 5, 4.5, col); px(img, 4, 7, c("#ffffff"))
        out[name] = outline(img, OUT)
    img = canvas(14, 16)
    rect(img, 2, 1, 11, 14, c("#f4ead8")); rect(img, 2, 1, 11, 2, c("#ffffff"))
    for y in (5, 8, 11):
        line(img, 4, y, 9, y, c("#8a7a9a"))
    poly(img, [(7, 3), (8, 5), (7, 7), (6, 5)], c("#c98cff"))
    out["PICK_page"] = outline(img, c("#6a3fa0"))
    img = canvas(10, 10)
    shaded_ellipse(img, 5, 5, 4, 4, [c("#e0f0ff"), c("#8ab8e0"), c("#4a7ab0")])
    out["PICK_material"] = outline(img, OUT)
    return out


# =============================================================== VFX flipbooks
def fx_frames(n, w, h, fn):
    return strip([fn(i / max(n - 1, 1), i, canvas(w, h)) for i in range(n)])


FIRE = [c("#fff8d0"), c("#ffe070"), c("#ffa030"), c("#e85020"), c("#a02818")]
ICE = [c("#ffffff"), c("#d8f6ff"), c("#8ad8f8"), c("#4a9ad8"), c("#2a5aa0")]
BOLT = [c("#ffffff"), c("#fff8a0"), c("#ffe040"), c("#c8a0ff")]
ARC = [c("#ffffff"), c("#f0d8ff"), c("#c98cff"), c("#8a50e0"), c("#5a30a8")]
STAR = [c("#ffffff"), c("#fff3a8"), c("#f2c45a"), c("#c88a2a")]
WIND = [c("#ffffff"), c("#d8fff0"), c("#90e8c8"), c("#50b898")]
POISON = [c("#e8ffb0"), c("#b0e060"), c("#8a50c8"), c("#5a2a88")]


def vfx_all():
    v = {}

    def fireball(t, i, img):
        shaded_ellipse(img, 8, 8, 5, 5, FIRE[:4])
        for k in range(3):
            px(img, 2 - (i + k) % 2, 7 + k - 1, FIRE[3]); px(img, 1, 8 + ((i + k) % 3) - 1, FIRE[4])
        px(img, 7, 6, FIRE[0])
        return img
    v["VFX_fireball"] = (fx_frames(4, 16, 16, fireball), 4, 16)

    def explosion(t, i, img):
        r = 6 + t * 18
        if t < 0.85:
            shaded_ellipse(img, 24, 24, r, r, FIRE[max(0, int(t * 3)):] or FIRE[-2:])
            ring(img, 24, 24, r - 1.5, r, FIRE[3] if t < 0.5 else FIRE[4])
        rr = np.random.default_rng(i)
        for k in range(10):
            a = k * 0.63 + rr.uniform(0, 0.3)
            x, y = polar(24, 24, r + 2 + t * 4, a)
            px(img, x, y, FIRE[min(4, 1 + int(t * 4))])
        if t > 0.4:
            img = dither_alpha(img, 1.2 - t)
        return img
    v["VFX_explosion"] = (fx_frames(6, 48, 48, explosion), 6, 18)

    def ice_shard(t, i, img):
        poly(img, [(15, 8), (8, 4), (1, 6), (1, 10), (8, 12)], ICE[2])
        poly(img, [(15, 8), (8, 4), (3, 7), (8, 8)], ICE[1])
        px(img, 12, 7, ICE[0])
        if i == 1:
            px(img, 0, 5, ICE[1]); px(img, 0, 11, ICE[1])
        return img
    v["VFX_ice_shard"] = (fx_frames(2, 16, 16, ice_shard), 2, 12)

    def ice_burst(t, i, img):
        for k in range(8):
            a = k * math.pi / 4 + 0.2
            x0, y0 = polar(20, 20, 2 + t * 6, a)
            x1, y1 = polar(20, 20, 6 + t * 13, a)
            line(img, x0, y0, x1, y1, ICE[1 + (k % 2)], 2)
        ring(img, 20, 20, 4 + t * 14, 5 + t * 14, ICE[0] if t < 0.5 else ICE[2])
        return dither_alpha(img, 1.3 - t)
    v["VFX_ice_burst"] = (fx_frames(5, 40, 40, ice_burst), 5, 16)

    def lightning(t, i, img):
        rr = np.random.default_rng(i * 7 + 1)
        x = 12
        pts = []
        for y in range(0, 64, 6):
            pts.append((x, y)); x = int(np.clip(x + rr.integers(-4, 5), 4, 20))
        pts.append((12, 63))
        if i < 3:
            for (a, b), (cc, d) in zip(pts, pts[1:]):
                line(img, a, b, cc, d, BOLT[2], 3); line(img, a, b, cc, d, BOLT[0], 1)
        ellipse(img, 12, 60, 10 - i * 1.5, 3, BOLT[1] if i < 3 else BOLT[3])
        return img if i < 3 else dither_alpha(img, 0.5)
    v["VFX_lightning"] = (fx_frames(4, 24, 64, lightning), 4, 16)

    def slash(t, i, img):
        # crescent arc opening to the right; the runtime rotates it to the swing direction (pre-drawn, 4 frames)
        a0, a1 = -1.6 + t * 0.6, -1.6 + 1.2 + t * 2.0
        for k in range(30):
            a = a0 + (a1 - a0) * k / 29
            for rr_, col in ((20, ARC[3]), (19, ARC[2]), (18, ARC[1]), (17, ARC[2])):
                x, y = polar(18, 24, rr_ - (abs(k - 15) / 15) * 3, a)
                px(img, x, y, col)
        return img if t < 0.7 else dither_alpha(img, 0.5)
    v["VFX_slash"] = (fx_frames(4, 48, 48, slash), 4, 20)

    def magic_bolt(t, i, img):
        shaded_ellipse(img, 9, 8, 5, 4, ARC[:4])
        for k in range(4):
            px(img, 3 - k + (i % 2), 8 + ((k + i) % 3) - 1, ARC[2 + k % 2])
        poly(img, [(9, 5), (10, 8), (9, 11), (8, 8)], ARC[0])
        return img
    v["VFX_magic_bolt"] = (fx_frames(4, 16, 16, magic_bolt), 4, 16)

    def charge(t, i, img):
        for k in range(6):
            a = k * math.pi / 3 + t * 2
            x, y = polar(16, 16, 13 - t * 9, a)
            ellipse(img, x, y, 1.5, 1.5, ARC[1 + k % 2])
        ring(img, 16, 16, 11 - t * 6, 12 - t * 6, ARC[3])
        return img
    v["VFX_charge"] = (fx_frames(6, 32, 32, charge), 6, 14)

    def missile(t, i, img):
        shaded_ellipse(img, 6, 5, 3, 3, ARC[:3])
        px(img, 1 + i, 5, ARC[3]); px(img, 2, 4 + i, ARC[3])
        return img
    v["VFX_homing"] = (fx_frames(2, 10, 10, missile), 2, 12)

    def starfire(t, i, img):
        a = i * math.pi / 8
        pts = []
        for k in range(10):
            r = 6 if k % 2 == 0 else 2.6
            pts.append(polar(7, 7, r, a + k * math.pi / 5))
        poly(img, pts, STAR[1]); px(img, 7, 7, STAR[0])
        return outline(img, STAR[3])
    v["VFX_starfire"] = (fx_frames(4, 14, 14, starfire), 4, 14)

    def orbit_blade(t, i, img):
        poly(img, [(7, 0), (10, 7), (7, 14), (4, 7)], ICE[2]); poly(img, [(7, 0), (8, 7), (7, 14), (5, 7)], ICE[1]); px(img, 7, 4, ICE[0])
        return outline(img, ICE[4])
    v["VFX_frost_blade"] = (fx_frames(1, 14, 14, orbit_blade), 1, 1)

    def pulse(t, i, img):
        ring(img, 24, 24, 4 + t * 18, 6 + t * 18, BOLT[1] if t < 0.5 else BOLT[3])
        for k in range(6):
            a = k * math.pi / 3 + t
            x0, y0 = polar(24, 24, 3 + t * 10, a)
            x1, y1 = polar(24, 24, 7 + t * 16, a + 0.3)
            line(img, x0, y0, x1, y1, BOLT[2])
        return dither_alpha(img, 1.3 - t)
    v["VFX_thunder_ring"] = (fx_frames(5, 48, 48, pulse), 5, 16)

    def vortex(t, i, img):
        for arm in range(3):
            for k in range(40):
                r = 2 + k * 0.5
                a = arm * 2 * math.pi / 3 + k * 0.22 + i * 0.6
                x, y = polar(24, 24, r, a)
                px(img, x, 24 + (y - 24) * 0.7, WIND[min(3, k // 10)])
        return img
    v["VFX_vortex"] = (fx_frames(6, 48, 48, vortex), 6, 14)

    def tornado(t, i, img):
        for y in range(4, 62):
            u = (62 - y) / 58
            r = 3 + u * 16
            for k in range(3):
                a = y * 0.35 + i * 1.0 + k * 2.1
                x = 24 + math.cos(a) * r
                col = FIRE[min(4, int((1 - u) * 3) + (k % 2))]
                px(img, x, y, col); px(img, x + 1, y, col)
        ellipse(img, 24, 61, 8, 2, FIRE[3])
        return img
    v["VFX_fire_tornado"] = (fx_frames(6, 48, 64, tornado), 6, 14)

    def steam(t, i, img):
        rr = np.random.default_rng(4)
        for k in range(9):
            a = k * 0.7
            d = (3 + t * 18) * rr.uniform(0.6, 1)
            x, y = polar(28, 30, d, a)
            shaded_ellipse(img, x, y - t * 6, 6 - t * 2, 5 - t * 2, [c("#ffffff"), c("#e8eef6"), c("#b8c4d4")])
        return dither_alpha(img, 1.25 - t)
    v["VFX_steam"] = (fx_frames(6, 56, 56, steam), 6, 14)

    def overload(t, i, img):
        r = 6 + t * 24
        shaded_ellipse(img, 32, 32, r, r, [FIRE[0], BOLT[1], FIRE[2], FIRE[3]])
        for k in range(8):
            a = k * math.pi / 4 + i
            x0, y0 = polar(32, 32, r * 0.5, a)
            x1, y1 = polar(32, 32, r + 4, a + 0.2)
            line(img, x0, y0, x1, y1, BOLT[0], 1)
        return dither_alpha(img, 1.2 - t)
    v["VFX_overload"] = (fx_frames(6, 64, 64, overload), 6, 16)

    def shatter(t, i, img):
        for k in range(12):
            a = k * math.pi / 6 + 0.3
            x, y = polar(28, 28, 3 + t * 22, a)
            poly(img, [(x, y - 3), (x + 2, y), (x, y + 3), (x - 2, y)], ICE[1 + k % 2])
        ring(img, 28, 28, 2 + t * 20, 3 + t * 20, BOLT[2])
        return dither_alpha(img, 1.25 - t)
    v["VFX_shatter"] = (fx_frames(6, 56, 56, shatter), 6, 16)

    def hit(t, i, img):
        r = 2 + i * 2
        for k in range(4):
            a = k * math.pi / 2 + math.pi / 4
            x0, y0 = polar(8, 8, r * 0.4, a)
            x1, y1 = polar(8, 8, r, a)
            line(img, x0, y0, x1, y1, c("#ffffff") if i < 2 else c("#fff3a8"))
        if i == 0:
            ellipse(img, 8, 8, 2.5, 2.5, c("#ffffff"))
        return img
    v["VFX_hit"] = (fx_frames(4, 16, 16, hit), 4, 24)

    def poof(t, i, img):
        rr = np.random.default_rng(2)
        for k in range(6):
            a = k * 1.05
            x, y = polar(16, 18, 3 + t * 10, a)
            shaded_ellipse(img, x, y - t * 3, 4.5 - t * 3, 4 - t * 3, [c("#f0e8f8"), c("#c8b8d8"), c("#8a7aa0")])
        return dither_alpha(img, 1.2 - t)
    v["VFX_poof"] = (fx_frames(5, 32, 32, poof), 5, 16)

    def dust(t, i, img):
        ellipse(img, 8 - t * 2, 10 - t * 3, 4 - t * 2, 3 - t * 1.5, c("#e8dcc8"))
        ellipse(img, 10 + t * 2, 11 - t * 2, 3 - t * 1.5, 2.5 - t, c("#d8c8b0"))
        return dither_alpha(img, 1.1 - t * 0.8)
    v["VFX_dust"] = (fx_frames(4, 16, 16, dust), 4, 14)

    def spore_ball(t, i, img):
        shaded_ellipse(img, 5, 5, 4, 4, POISON)
        px(img, 3, 3, POISON[0])
        return outline(img, c("#3a1a58"))
    v["VFX_spore"] = (fx_frames(2, 10, 10, spore_ball), 2, 8)

    def poison_pool(t, i, img):
        ellipse(img, 32, 32, 30, 30, c("#8a50c8", 150))
        ellipse(img, 32, 32, 26, 26, c("#b070e8", 120))
        rr = np.random.default_rng(i)
        for k in range(10):
            x, y = 32 + rr.uniform(-22, 22), 32 + rr.uniform(-22, 22)
            ellipse(img, x, y, 2, 2, c("#e8ffb0", 200))
        return img
    v["VFX_poison_pool"] = (fx_frames(4, 64, 64, poison_pool), 4, 6)

    def telegraph(t, i, img):
        ellipse(img, 32, 32, 31, 31, c("#ff3a3a", 70))
        ring(img, 32, 32, 29, 31, c("#ff5a3a", 230))
        return img
    v["VFX_telegraph_circle"] = (fx_frames(1, 64, 64, telegraph), 1, 1)

    def tele_rect(t, i, img):
        rect(img, 0, 0, 31, 31, c("#ff3a3a", 70)); rect(img, 0, 0, 31, 1, c("#ff5a3a", 230)); rect(img, 0, 30, 31, 31, c("#ff5a3a", 230))
        return img
    v["VFX_telegraph_rect"] = (fx_frames(1, 32, 32, tele_rect), 1, 1)

    def aura(t, i, img):
        for k in range(3):
            r = 10 + ((t + k / 3) % 1) * 20
            ring(img, 32, 40, r, r + 1, STAR[1] if k % 2 else ARC[2])
        for k in range(8):
            a = k * math.pi / 4 + i * 0.4
            x, y = polar(32, 40, 18, a)
            px(img, x, y - i * 2, STAR[0])
        return img
    v["VFX_overdrive_aura"] = (fx_frames(6, 64, 64, aura), 6, 14)

    def levelup(t, i, img):
        for k in range(12):
            a = k * math.pi / 6
            x, y = polar(24, 24, 4 + t * 18, a)
            px(img, x, y, STAR[k % 3]); px(img, x, y - 1, STAR[0])
        ring(img, 24, 24, 3 + t * 16, 4 + t * 16, STAR[2])
        return dither_alpha(img, 1.3 - t)
    v["VFX_levelup"] = (fx_frames(6, 48, 48, levelup), 6, 14)

    def burn(t, i, img):
        for k, x in enumerate((3, 6, 9)):
            h = 4 + ((i + k) % 3) * 2
            poly(img, [(x - 2, 11), (x + 2, 11), (x, 11 - h)], FIRE[2 + k % 2])
            px(img, x, 10, FIRE[1])
        return img
    v["VFX_burn"] = (fx_frames(3, 12, 12, burn), 3, 10)

    def chill(t, i, img):
        for k in range(3):
            x, y = polar(6, 6, 4, k * 2.1 + i)
            px(img, x, y, ICE[0]); px(img, x + 1, y, ICE[2])
        return img
    v["VFX_chill"] = (fx_frames(3, 12, 12, chill), 3, 8)

    def shock(t, i, img):
        rr = np.random.default_rng(i + 30)
        x, y = 2, 6
        for _ in range(4):
            nx, ny = x + 3, int(np.clip(y + rr.integers(-3, 4), 1, 10))
            line(img, x, y, nx, ny, BOLT[1]); x, y = nx, ny
        return img
    v["VFX_shock"] = (fx_frames(3, 14, 12, shock), 3, 14)

    def glow(t, i, img):
        for r, a in ((15, 40), (11, 80), (7, 140), (4, 220)):
            ellipse(img, 16, 16, r, r, c("#ffffff", a))
        return img
    v["VFX_glow"] = (fx_frames(1, 32, 32, glow), 1, 1)

    def spark(t, i, img):
        rect(img, 1, 1, 2, 2, c("#ffffff")); px(img, 0, 1, c("#ffffff", 160)); px(img, 3, 2, c("#ffffff", 160))
        return img
    v["VFX_spark"] = (fx_frames(1, 4, 4, spark), 1, 1)

    def shadow(t, i, img):
        ellipse(img, 8, 3, 7.5, 2.5, c("#1a1028", 90))
        return img
    v["VFX_shadow"] = (fx_frames(1, 16, 6, shadow), 1, 1)

    def portal(t, i, img):
        for k in range(3):
            r = 4 + ((t + k / 3) % 1) * 18
            ring(img, 24, 24, r, r + 1.2, STAR[1] if k % 2 else ARC[1])
        ellipse(img, 24, 24, 4, 4, c("#ffffff"))
        return img
    v["VFX_portal"] = (fx_frames(6, 48, 48, portal), 6, 10)
    return v


# =============================================================== UI icons (16x16)
def icons():
    out = {}

    def icon(name, fn, frame=c("#2b2236")):
        img = canvas(16, 16)
        fn(img)
        out["ICON_" + name] = img

    icon("fire_bolt", lambda i: (shaded_ellipse(i, 8, 9, 5, 5, FIRE[:4]), poly(i, [(5, 6), (8, 0), (11, 6)], FIRE[2])))
    icon("split_flame", lambda i: [shaded_ellipse(i, x, y, 3.2, 3.2, FIRE[:4]) for x, y in ((8, 5), (4, 11), (12, 11))])
    icon("piercing_ice", lambda i: (poly(i, [(15, 1), (8, 5), (1, 15), (11, 8)], ICE[2]), line(i, 15, 1, 4, 12, ICE[0])))
    icon("chain_spark", lambda i: [line(i, a, b, cc, d, BOLT[2], 2) for a, b, cc, d in ((9, 0, 5, 7), (5, 7, 11, 8), (11, 8, 6, 15))])
    icon("wind_pull", lambda i: [ring(i, 8, 8, r, r + 1, WIND[k]) for k, r in enumerate((1, 3.5, 6))])
    icon("frost_orbit", lambda i: (ring(i, 8, 8, 5, 6, ICE[3]), [poly(i, [(x, y - 2), (x + 2, y), (x, y + 2), (x - 2, y)], ICE[1]) for x, y in ((8, 2), (14, 8), (8, 14), (2, 8))]))
    icon("thunder_pulse", lambda i: (ring(i, 8, 8, 5, 7, BOLT[3]), line(i, 9, 2, 6, 8, BOLT[1], 2), line(i, 6, 8, 10, 9, BOLT[1], 2), line(i, 10, 9, 7, 14, BOLT[1], 2)))
    icon("homing", lambda i: [shaded_ellipse(i, x, y, 2.5, 2.5, ARC[:3]) for x, y in ((4, 12), (8, 8), (12, 4))])
    icon("starfire", lambda i: poly(i, [polar(8, 8, 7 if k % 2 == 0 else 3, -math.pi / 2 + k * math.pi / 5) for k in range(10)], STAR[1]))
    icon("overdrive", lambda i: (ring(i, 8, 8, 5, 7, ARC[2]), poly(i, [(8, 2), (10, 7), (15, 8), (10, 9), (8, 14), (6, 9), (1, 8), (6, 7)], STAR[1])))
    icon("staff", lambda i: (line(i, 3, 14, 11, 4, c("#a07a52"), 2), shaded_ellipse(i, 12, 4, 3, 3, ARC[:3])))
    icon("dash", lambda i: [line(i, 2, y, 13, y, WIND[k], 1) for k, y in enumerate((4, 8, 12))])
    for k in list(out):
        outline(out[k], OUT)
    return out


# =============================================================== damage digits (6x8 each, white + dark outline)
DIGITS = ["111 101 101 101 111", "010 110 010 010 111", "111 001 111 100 111", "111 001 111 001 111", "101 101 111 001 001",
          "111 100 111 001 111", "111 100 111 101 111", "111 001 010 010 010", "111 101 111 101 111", "111 101 111 001 111"]


def digits():
    frames = []
    for d in DIGITS:
        img = canvas(6, 8)
        for y, row in enumerate(d.split()):
            for x, v in enumerate(row):
                if v == "1":
                    rect(img, 1 + x, 1 + y, 1 + x, 1 + y, c("#ffffff"))
        outline(img, c("#1a1028"), diag=True)
        frames.append(img)
    return strip(frames)


# =============================================================== tiles
def forest_tiles():
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import gen_tiles as gt
    # forest palette: deeper, bluer greens; darker mossy dirt (same autotile math, so seams stay valid)
    swap = dict(g_base="#5f9a4e", g_dark="#548c48", g_edge="#3f6e40", g_light="#6eaa52", g_hi="#88c060", g_deep="#2f5236",
                d_base="#b89270", d_light="#c8a07a", d_dark="#a88264", d_edge="#8e6c56", d_deep="#6e5040")
    saved = {k: gt.P[k].copy() for k in swap}
    for k, v in swap.items():
        gt.P[k] = gt.hexc(v)
    grass = gt.strip([gt.grass_tile(v) for v in range(4)])
    path = gt.sheet256(gt.path_tile)
    water = gt.sheet256(gt.water_tile)
    for k, v in saved.items():
        gt.P[k] = v
    return grass, path, water


def interior_tiles():
    """floor x4 (wood planks), wall_top, wall_face, wall_face_window_left/right? -> 6 tiles (192x32)."""
    tiles = []
    wd = [c("#c89868"), c("#b8885a"), c("#a87a50"), c("#8a6040")]
    for v in range(4):
        r = np.random.default_rng(40 + v)
        img = canvas(32, 32)
        for row in range(4):
            y0 = row * 8
            col = wd[(row + v) % 3]
            rect(img, 0, y0, 31, y0 + 7, col)
            rect(img, 0, y0 + 7, 31, y0 + 7, wd[3])
            seam = (row * 11 + v * 7) % 32
            rect(img, seam, y0, seam, y0 + 6, wd[3])
            for _ in range(3):
                px(img, r.integers(1, 31), y0 + r.integers(1, 6), wd[(row + v + 1) % 3])
        tiles.append(img)
    top = canvas(32, 32)
    rect(top, 0, 0, 31, 31, c("#4a3445")); rect(top, 0, 28, 31, 31, c("#6a4a5a"))
    for x in range(0, 32, 8):
        px(top, x + 3, 12, c("#5a4050"))
    tiles.append(top)
    face = canvas(32, 32)
    rect(face, 0, 0, 31, 31, c("#d8c8b0"))
    for y in range(0, 20, 5):
        rect(face, 0, y, 31, y, c("#c8b49a"))
    rect(face, 0, 20, 31, 31, c("#8a6040")); rect(face, 0, 20, 31, 21, c("#a87a50")); rect(face, 0, 31, 31, 31, c("#5a3a2a"))
    for x in range(0, 32, 8):
        rect(face, x, 22, x, 30, c("#6a4a30"))
    tiles.append(face)
    return strip(tiles)


# =============================================================== main
def main():
    save("Characters", "CHR_witch_draft", person_sheet(WITCH), FW, FH, 18, (0.5, 4), 8,
         {"rows": "down:0-5 up:6-11 side:12-17 (idle, walk1-4, cast)"})
    for k, spec in NPCS.items():
        save("Characters", f"NPC_{k}_draft", person_sheet(spec), FW, FH, 18, (0.5, 4), 6)
    save("Enemies", "ENM_slime_draft", slime_frames(), 28, 24, 4, (0.5, 2), 8)
    save("Enemies", "ENM_spore_shroom_draft", shroom(28, 32, [c("#f0a0c8"), c("#d86aa8"), c("#a84a88"), c("#78306a")], 5,
                                                        [c("#f4ead8"), c("#dccab0"), c("#b49c80")], c("#3a1a2a")), 28, 32, 4, (0.5, 1), 6)
    save("Enemies", "ENM_root_guard_draft", root_guard_frames(), 44, 48, 4, (0.5, 2), 5)
    save("Enemies", "ENM_wisp_draft", wisp_frames(), 20, 22, 4, (0.5, 3), 10)
    save("Enemies", "ENM_spore_elite_draft", shroom(48, 52, [c("#c8a0f0"), c("#a070d8"), c("#7040b0"), c("#4a2880")], 8,
                                                       [c("#f4ead8"), c("#dccab0"), c("#b49c80")], c("#ff4a6a"), angry=True, scale=1.5), 48, 52, 4, (0.5, 1), 6)
    save("Enemies", "ENM_mushroom_king_draft", boss_frames(), 96, 96, 6, (0.5, 4), 6, {"rows": "idle:0-3 windup:4-5"})
    save("Props", "PROP_training_dummy_draft", dummy("dummy"), pivot=(0.5, 1))
    save("Props", "PROP_archery_target_draft", dummy("target"), pivot=(0.5, 1))
    save("Props", "PROP_bridge_wood_draft", bridge_village(), pivot=(0.5, 0.5))
    save("Props", "PROP_bridge_stone_draft", bridge_stone(False), pivot=(0.5, 0.5))
    save("Props", "PROP_bridge_stone_broken_draft", bridge_stone(True), pivot=(0.5, 0.5))
    for k, (cw, ch, ramp, trunk, kind) in {
        "forest_oak": (72, 84, [c("#4c8a44"), c("#356a38"), c("#24502e"), c("#183a24")], [c("#8a6a4a"), c("#6a4a32"), c("#4a3222")], "round"),
        "forest_pine": (52, 80, [c("#3c7a50"), c("#2a5a40"), c("#1c4030")], [c("#7a5a3a"), c("#5a3e28"), c("#3e2a1c")], "pine"),
        "forest_dark": (64, 72, [c("#5a6a9a"), c("#3e4c7a"), c("#2a3460"), c("#1c2244")], [c("#6a5a6a"), c("#4a3e4c"), c("#2e2632")], "round"),
        "village_round": (56, 60, [c("#a8d070"), c("#7fb05a"), c("#5a8a44"), c("#3e6a34")], [c("#a07a52"), c("#7a5a3a"), c("#5a3e28")], "round"),
    }.items():
        save("Props", f"PROP_{k}_draft", tree(cw, ch, ramp, trunk, hash(k) % 1000, kind), pivot=(0.5, 1))
    for k in ("pillar", "altar", "chest", "chest_open", "vine", "vine_burnt", "lever", "lever_on", "lamp", "crate",
              "glow_shroom", "bush", "rock_dark", "log", "starplate"):
        img, py = simple_prop(k)
        save("Props", f"PROP_{k}_draft", img, pivot=(0.5, py))
    for k in ("bookshelf", "bed", "table", "chair", "cauldron", "alchemy", "lectern", "desk", "wardrobe", "rug", "window", "door_in", "plant"):
        img, py = furniture(k)
        save("Props", f"INT_{k}_draft", img, pivot=(0.5, py))
    for k, img in pickups().items():
        save("Props", k + "_draft", img, pivot=(0.5, 0.5))
    for k, (img, n, fps) in vfx_all().items():
        save("VFX", k + "_draft", img, frames=n, pivot=(0.5, 0.5), fps=fps)
    save("UI", "UI_digits_draft", digits(), 6, 8, 10, (0.5, 0.5))
    # ICON_* (32x32) now come from tools/art/ui_art.py (ui_manifest.json); icons() is kept for reference
    grass, path, water = forest_tiles()
    for name, arr in (("TILE_forest_grass_draft", grass), ("TILE_forest_path_draft_256", path), ("TILE_forest_water_draft_256", water)):
        d = os.path.join(ART, "Tilesets/Forest/Draft"); os.makedirs(d, exist_ok=True)
        Image.fromarray(arr).save(os.path.join(d, name + ".png"))
    d = os.path.join(ART, "Tilesets/Interior/Draft"); os.makedirs(d, exist_ok=True)
    Image.fromarray(interior_tiles()).save(os.path.join(d, "TILE_interior_draft.png"))
    with open(os.path.join(ART, "draft_manifest.json"), "w") as f:
        json.dump({"status": "procedural draft (Needs Cleanup)", "items": MANIFEST}, f, indent=1)
    print("wrote", len(MANIFEST), "sprites")


if __name__ == "__main__":
    main()
