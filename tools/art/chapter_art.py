"""Generate the DRAFT art for chapters 2-5 (星核礦坑, 星輝湖, 失落觀星台, 黑月城塞): biome tiles, enemies,
bosses, props, mechanism sprites, spell VFX and the new spell icons.

Style follows the G2-approved samples (ART_002_SAMPLES.md, art-source/art-002/build_samples.py): dark
purple-brown ink outlines, light from the top-left, two close ground tones with sparse interior marks,
flat RGBA (alpha 0/255 only). Ground, path and water autotiles reuse gen_tiles.py with a biome palette,
so every path/water sheet is seamless by the same construction (tools/art/seam_test.py).

Status of every output: Needs Cleanup (procedural draft, not production art; ASSET_GAPS.md). Nothing is
cut from the AI reference images.
Outputs: Assets/_Game/Art/{Tilesets/<Biome>,Enemies,Props,VFX,UI}/Draft/*.png and
Assets/_Game/Art/chapter_manifest.json (read by Starbind.Editor.GameContentBuilder).

Usage: python3 tools/art/chapter_art.py <repo_root>
"""
import json, math, os, sys
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixlib import *  # noqa
import gen_tiles as gt
import gen_game_drafts as gd

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
ART = os.path.join(ROOT, "Assets/_Game/Art")
MANIFEST = []
OUT = c("#2b2236")
INK = c("#493449")   # G2 inner ink edge
T = 32


def save(group, name, img, frames=1, pivot=(0.5, 0.0), fps=10, w=None, h=None):
    d = os.path.join(ART, group, "Draft")
    os.makedirs(d, exist_ok=True)
    assert set(np.unique(img[:, :, 3])) <= {0, 255}, name + " has partial alpha"
    Image.fromarray(img).save(os.path.join(d, name + ".png"))
    fw = w or img.shape[1] // frames
    fh = h or img.shape[0]
    px_ = int(round(pivot[0] * fw))
    py_ = int(round(pivot[1] * fh)) if 0 < pivot[1] < 1 else int(pivot[1])
    MANIFEST.append({"name": name, "path": f"Assets/_Game/Art/{group}/Draft/{name}.png", "w": fw, "h": fh,
                     "frames": frames, "pivotX": px_, "pivotY": py_, "fps": fps})


def hsv_shift(img, hue=0.0, sat=1.0, val=1.0, keep=(OUT,)):
    """Recolour every opaque non-outline pixel in HSV (hue in turns)."""
    out = img.copy()
    m = img[:, :, 3] > 0
    for k in keep:
        m &= ~np.all(img[:, :, :3] == k[:3], axis=2)
    rgb = img[:, :, :3].astype(float) / 255
    mx, mn = rgb.max(2), rgb.min(2)
    d = mx - mn + 1e-9
    h = np.where(mx == rgb[..., 0], ((rgb[..., 1] - rgb[..., 2]) / d) % 6,
                 np.where(mx == rgb[..., 1], (rgb[..., 2] - rgb[..., 0]) / d + 2, (rgb[..., 0] - rgb[..., 1]) / d + 4)) / 6
    s = np.where(mx > 0, (mx - mn) / (mx + 1e-9), 0)
    v = mx
    h = (h + hue) % 1
    s = np.clip(s * sat, 0, 1)
    v = np.clip(v * val, 0, 1)
    i = np.floor(h * 6).astype(int) % 6
    f = h * 6 - np.floor(h * 6)
    p, q, t_ = v * (1 - s), v * (1 - f * s), v * (1 - (1 - f) * s)
    r = np.choose(i, [v, q, p, p, t_, v]); g = np.choose(i, [t_, v, v, q, p, p]); b = np.choose(i, [p, p, t_, v, v, q])
    new = (np.stack([r, g, b], 2) * 255).round().astype(np.uint8)
    out[m, :3] = new[m]
    return out


def frames_of(sheet, n):
    w = sheet.shape[1] // n
    return [sheet[:, k * w:(k + 1) * w].copy() for k in range(n)]


# =============================================================== biome tiles
BIOMES = {
    # ground (base, dark, edge, light, hi, deep), path/dirt (base, light, dark, edge, deep), water, bank, solid ramp
    "mine": dict(g=("#4a4560", "#443f59", "#38334c", "#544e6a", "#635b7c", "#2c283c"),
                 d=("#7a6a58", "#8a7864", "#6e604f", "#5e5244", "#4a4036"),
                 w=("#283a78", "#1c2858", "#34509a", "#6a7ac8", "#b8c4f0", "#182048"), bank=("#3a3048", "#241c30"),
                 solid=("#2c2638", "#3a3248", "#211c2c", "#8a6ad8", "#c8a8ff"), marks="crystal"),
    "lake": dict(g=("#5c8a78", "#558270", "#456a5e", "#689684", "#7eac94", "#365448"),
                 d=("#b8c2d4", "#c8d0e0", "#a8b2c6", "#8e98ae", "#6e7890"),
                 w=("#3a6ab4", "#284e8a", "#5084c8", "#9ac0ec", "#e0f0ff", "#203e70"), bank=("#4a6a62", "#2e4442"),
                 solid=("#2c4050", "#38505e", "#1e2e3a", "#7ab8d8", "#c8ecff"), marks="reed"),
    "observatory": dict(g=("#e2eaf2", "#d6e0ec", "#bccadc", "#eef4fa", "#ffffff", "#a4b2c8"),
                        d=("#9a94a6", "#aaa4b6", "#8c8698", "#767084", "#5e586c"),
                        w=("#8ab8dc", "#6a98c4", "#a6cceb", "#cfe6f6", "#ffffff", "#5a84ae"), bank=("#9aa8c0", "#6e7a94"),
                        solid=("#5a5c70", "#6e7086", "#46485a", "#e8eef6", "#ffffff"), marks="snow"),
    "citadel": dict(g=("#3e304e", "#382a48", "#2c2040", "#463858", "#56466a", "#221830"),
                    d=("#6e4e86", "#7e5c96", "#624478", "#4e3662", "#3a284a"),
                    w=("#140a22", "#0a0414", "#22143a", "#4a2e7a", "#9a7ad0", "#06020c"), bank=("#2a1e3a", "#160e22"),
                    solid=("#1e142a", "#2a1e3a", "#140c1e", "#6e4e9a", "#b090e0"), marks="brick"),
}


def set_palette(b):
    spec = BIOMES[b]
    keys_g = ["g_base", "g_dark", "g_edge", "g_light", "g_hi", "g_deep"]
    keys_d = ["d_base", "d_light", "d_dark", "d_edge", "d_deep"]
    keys_w = ["w_base", "w_deep", "w_light", "w_foam", "w_white", "w_line"]
    saved = {k: gt.P[k].copy() for k in keys_g + keys_d + keys_w + ["b_bank", "b_dark", "pb_l", "pb_d"]}
    for k, v in zip(keys_g, spec["g"]): gt.P[k] = gt.hexc(v)
    for k, v in zip(keys_d, spec["d"]): gt.P[k] = gt.hexc(v)
    for k, v in zip(keys_w, spec["w"]): gt.P[k] = gt.hexc(v)
    gt.P["b_bank"], gt.P["b_dark"] = gt.hexc(spec["bank"][0]), gt.hexc(spec["bank"][1])
    gt.P["pb_l"], gt.P["pb_d"] = gt.hexc(spec["d"][1]), gt.hexc(spec["d"][4])
    return saved


def ground_tile(b, variant):
    """G2 ground: base colour, a sparse second tone, a few interior marks (edges plain -> seamless)."""
    spec = BIOMES[b]
    base, dark, edge, light, hi, deep = [c(x) for x in spec["g"]]
    r = np.random.default_rng(700 + variant * 13 + len(b))
    img = np.tile(base, (T, T, 1))
    n = r.random((T, T))
    img[n < 0.08] = dark
    img[n > 0.95] = light
    kind = spec["marks"]
    for _ in range(3 + variant):
        x, y = int(r.integers(3, T - 6)), int(r.integers(3, T - 4))
        if kind == "crystal":          # gravel and a tiny crystal glint
            img[y, x] = edge; img[y, x + 1] = dark; img[y + 1, x + 1] = deep
            if r.random() < 0.3:
                img[y - 1, x + 3] = c("#8a6ad8"); img[y, x + 3] = c("#5a48a0")
        elif kind == "reed":           # G2 leaf mark in a cooler green
            img[y, x:x + 5] = dark; img[y - 1, x + 1:x + 3] = light
        elif kind == "snow":           # wind ripples
            img[y, x:x + 4] = edge; img[y - 1, x + 1:x + 3] = hi
        else:                          # cracked flagstone speck
            img[y, x] = deep; img[y, x + 1] = edge
    if kind == "brick":                # flagstone grid on the tile border: tiles join into a slab grid
        img[0, :] = deep; img[:, 0] = deep
        img[16, 0:16] = edge if variant % 2 else img[16, 0:16]
    return img


def solid_tile(b, variant):
    """Impassable top (cave rock / lake cliff / snowy crag / ruin wall): rounded stones lit top-left."""
    base, light, dark, glint, glint2 = [c(x) for x in BIOMES[b]["solid"]]
    kind = BIOMES[b]["marks"]
    r = np.random.default_rng(900 + variant + len(b) * 7)
    img = np.tile(base, (T, T, 1))
    n = r.random((T, T))
    img[n < 0.1] = dark
    if kind == "brick":
        for row in range(4):
            y0 = row * 8
            img[y0, :] = dark
            off = 8 if (row + variant) % 2 else 0
            for xx in range(off, T, 16):
                img[y0:y0 + 8, xx % T] = dark
            for xx in range(T):
                if (xx - off) % 16 not in (0,):
                    img[y0 + 1, xx] = light if (xx + row) % 5 else base
    else:
        for _ in range(5):
            cx, cy = r.uniform(5, T - 5), r.uniform(5, T - 5)
            rx, ry = r.uniform(3, 6), r.uniform(2.5, 4.5)
            shaded_ellipse(img, cx, cy, rx, ry, [light, base, base, dark])
    if kind == "crystal" and variant % 2 == 0:
        x, y = int(r.integers(8, 22)), int(r.integers(10, 22))
        poly(img, [(x, y + 6), (x + 2, y - 3), (x + 4, y + 6)], glint)
        line(img, x + 2, y - 2, x + 2, y + 4, glint2)
    if kind == "snow":
        m = n > 0.55
        img[:10][m[:10]] = glint
        img[:6] = np.where(r.random((6, T, 1)) < 0.85, glint2, img[:6])
    if kind == "reed" and variant % 2:
        for k in range(3):
            x = int(r.integers(4, 28))
            line(img, x, 26, x + 1, 18, c("#5c8a62")); px(img, x + 1, 17, c("#a8c87a"))
    return img


def biome_tiles():
    for b in BIOMES:
        d = os.path.join(ART, "Tilesets", b.capitalize(), "Draft")
        os.makedirs(d, exist_ok=True)
        saved = set_palette(b)
        Image.fromarray(strip([ground_tile(b, v) for v in range(4)])).save(os.path.join(d, f"TILE_{b}_ground_draft.png"))
        Image.fromarray(gt.sheet256(gt.path_tile)).save(os.path.join(d, f"TILE_{b}_path_draft_256.png"))
        Image.fromarray(gt.sheet256(gt.water_tile)).save(os.path.join(d, f"TILE_{b}_water_draft_256.png"))
        Image.fromarray(strip([solid_tile(b, v) for v in range(4)])).save(os.path.join(d, f"TILE_{b}_solid_draft.png"))
        for k, v in saved.items():
            gt.P[k] = v


# =============================================================== enemies
def crystal_mite(body, spike):
    out = []
    for i in range(4):
        img = canvas(24, 20)
        bob = [0, -1, 0, 1][i]
        for k, x in enumerate((6, 10, 14, 18)):
            line(img, x, 13 + bob, x + (-2 if k < 2 else 2), 18 - (i + k) % 2, OUT_SOFT)
        shaded_ellipse(img, 12, 12 + bob, 8, 5, body)
        poly(img, [(8, 10 + bob), (11, 1 + bob), (14, 10 + bob)], spike[1])
        poly(img, [(9, 10 + bob), (11, 3 + bob), (11, 10 + bob)], spike[0])
        poly(img, [(14, 10 + bob), (17, 4 + bob), (18, 11 + bob)], spike[2])
        px(img, 7, 12 + bob, c("#ffe080")); px(img, 9, 12 + bob, c("#ffe080"))
        outline(img, OUT)
        out.append(img)
    return strip(out)


OUT_SOFT = c("#3e3048")


def bat(ramp, eye):
    out = []
    for i in range(4):
        img = canvas(30, 22)
        up = [0, 4, 7, 4][i]
        for s in (-1, 1):
            poly(img, [(15, 9), (15 + s * 14, 4 + up), (15 + s * 11, 12 + up // 2), (15 + s * 6, 10)], ramp[2])
            line(img, 15 + s * 4, 9, 15 + s * 13, 5 + up, ramp[1])
        shaded_ellipse(img, 15, 11, 5, 5, ramp)
        poly(img, [(11, 7), (12, 3), (13, 7)], ramp[1]); poly(img, [(17, 7), (18, 3), (19, 7)], ramp[1])
        px(img, 13, 10, eye); px(img, 17, 10, eye)
        outline(img, OUT)
        out.append(img)
    return strip(out)


def frog(ramp, belly):
    out = []
    for i in range(4):
        img = canvas(30, 24)
        sq = [0, 1, 0, -1][i]
        shaded_ellipse(img, 15, 15 - sq, 11, 7 + sq, ramp)
        ellipse(img, 15, 18, 7, 3, belly)
        for x in (8, 22):
            shaded_ellipse(img, x, 8 - sq, 3.5, 3.5, ramp[:3])
            rect(img, x - 1, 7 - sq, x, 8 - sq, c("#1a1a2a")); px(img, x - 1, 7 - sq, c("#ffffff"))
        rect(img, 4, 20, 9, 21, ramp[2]); rect(img, 21, 20, 26, 21, ramp[2])
        line(img, 10, 14, 20, 14, ramp[3])
        outline(img, OUT)
        out.append(img)
    return strip(out)


def knight(armor, trim, visor, w=40, h=48):
    out = []
    for i in range(4):
        img = canvas(w, h)
        bob = [0, -1, 0, -1][i]
        cx = w // 2
        for k, x in enumerate((cx - 6, cx + 3)):
            lift = (i + k) % 2
            rect(img, x, h - 14 + bob, x + 3, h - 2 - lift, armor[2])
        shaded_ellipse(img, cx, h - 22 + bob, 11, 12, armor)
        rect(img, cx - 9, h - 24 + bob, cx + 9, h - 23 + bob, trim)
        shaded_ellipse(img, cx, h - 36 + bob, 7, 7, armor)
        rect(img, cx - 5, h - 37 + bob, cx + 5, h - 35 + bob, visor)
        poly(img, [(cx - 2, h - 44 + bob), (cx, h - 49 + bob), (cx + 2, h - 44 + bob)], trim)
        line(img, cx + 12, h - 30 + bob, cx + 14, h - 8 + bob, c("#d8dcef"), 2)
        rect(img, cx + 10, h - 30 + bob, cx + 17, h - 29 + bob, trim)
        poly(img, [(cx - 16, h - 30 + bob), (cx - 9, h - 32 + bob), (cx - 9, h - 14 + bob), (cx - 14, h - 16 + bob)], armor[1])
        outline(img, OUT)
        out.append(img)
    return strip(out)


def turret(lens, brass):
    out = []
    for i in range(4):
        img = canvas(28, 34)
        for x0, x1 in ((14, 5), (14, 23), (14, 14)):
            line(img, x0, 20, x1, 32, brass[2], 2)
        shaded_ellipse(img, 14, 15, 8, 7, brass)
        shaded_ellipse(img, 14, 14, 5, 5, lens)
        glow = [c("#ffffff"), c("#fff3a8")][i % 2]
        px(img, 12, 12, glow); px(img, 13, 12, glow)
        rect(img, 6, 4, 22, 5, brass[1])
        outline(img, OUT)
        out.append(img)
    return strip(out)


def hound(brass, eye):
    out = []
    for i in range(4):
        img = canvas(40, 28)
        bob = [0, -1, 0, 1][i]
        for k, x in enumerate((9, 14, 26, 31)):
            lift = (i + k) % 2
            line(img, x, 17 + bob, x + (1 if k % 2 else -1), 26 - lift, brass[2], 2)
        shaded_ellipse(img, 20, 15 + bob, 13, 6, brass)
        shaded_ellipse(img, 33, 10 + bob, 6, 5, brass)
        poly(img, [(31, 6 + bob), (33, 1 + bob), (35, 6 + bob)], brass[1])
        px(img, 35, 9 + bob, eye); px(img, 36, 9 + bob, eye)
        ring(img, 18, 14 + bob, 3, 4, brass[3])
        line(img, 7, 12 + bob, 2, 8 + bob, brass[2], 2)
        outline(img, OUT)
        out.append(img)
    return strip(out)


def gear_elite(brass, core):
    out = []
    for i in range(4):
        img = canvas(48, 52)
        cx, cy = 24, 26
        for k in range(10):
            a = k * math.pi / 5 + i * 0.12
            x, y = polar(cx, cy, 20, a)
            ellipse(img, x, y, 3.5, 3.5, brass[2])
        shaded_ellipse(img, cx, cy, 18, 18, brass)
        ring(img, cx, cy, 9, 11, brass[3])
        shaded_ellipse(img, cx, cy, 7, 7, core)
        rect(img, cx - 3, cy - 1, cx + 3, cy, c("#ff4a6a") if i % 2 else c("#ffd84a"))
        rect(img, 15, 44, 19, 50, brass[2]); rect(img, 29, 44, 33, 50, brass[2])
        outline(img, OUT)
        out.append(img)
    return strip(out)


def robed(robe, trim, glow, w=28, h=36, halo=None):
    out = []
    for i in range(4):
        img = canvas(w, h)
        bob = [0, -1, 0, -1][i]
        cx = w / 2
        poly(img, [(cx - 3, 8 + bob), (cx + 3, 8 + bob), (cx + w * 0.38, h - 2), (cx - w * 0.38, h - 2)], robe[1])
        poly(img, [(cx - 3, 8 + bob), (cx - 1, 8 + bob), (cx - w * 0.2, h - 2), (cx - w * 0.38, h - 2)], robe[0])
        poly(img, [(cx + 1, 8 + bob), (cx + 3, 8 + bob), (cx + w * 0.38, h - 2), (cx + w * 0.2, h - 2)], robe[2])
        shaded_ellipse(img, cx, 9 + bob, w * 0.22, w * 0.22, robe)
        ellipse(img, cx, 10 + bob, w * 0.12, w * 0.1, c("#120a1c"))
        px(img, cx - 2, 10 + bob, glow); px(img, cx + 1, 10 + bob, glow)
        rect(img, cx - w * 0.3, h - 4, cx + w * 0.3, h - 3, trim)
        if halo is not None:
            ring(img, cx, 9 + bob, w * 0.3, w * 0.3 + 1.5, halo)
        ellipse(img, cx + w * 0.3, h * 0.55 + bob, 2.5, 2.5, glow)
        outline(img, OUT)
        out.append(img)
    return strip(out)


def spark(ramp):
    out = []
    for i in range(4):
        img = canvas(20, 20)
        r = 6 + (i % 2)
        for k in range(4):
            a = k * math.pi / 2 + i * math.pi / 8
            x, y = polar(10, 10, r + 2, a)
            line(img, 10, 10, x, y, ramp[1], 2)
        shaded_ellipse(img, 10, 10, 4, 4, ramp)
        px(img, 9, 9, c("#ffffff"))
        outline(img, OUT)
        out.append(img)
    return strip(out)


def moth(wing, body):
    out = []
    for i in range(4):
        img = canvas(24, 22)
        up = [0, 3, 5, 3][i]
        for s in (-1, 1):
            shaded_ellipse(img, 12 + s * 6, 8 + up // 2, 6, 5 - up * 0.4, wing)
            shaded_ellipse(img, 12 + s * 5, 14, 4, 3, wing[1:])
            px(img, 12 + s * 6, 8 + up // 2, c("#fff3c0"))
        shaded_ellipse(img, 12, 12, 2.5, 6, body)
        line(img, 11, 5, 9, 1, body[2]); line(img, 13, 5, 15, 1, body[2])
        outline(img, OUT)
        out.append(img)
    return strip(out)


# ---------------------------------------------------------------- bosses (96x96, idle 0-3, windup 4-5)
def golem_frames():
    rock = [c("#8a849c"), c("#6a647e"), c("#4e4862"), c("#38324a"), c("#26223a")]
    crystal = [c("#ffffff"), c("#e0c8ff"), c("#a878f0"), c("#6a44c0")]
    out = []
    for i in range(6):
        img = canvas(96, 96)
        windup = i >= 4
        bob = [0, -1, -2, -1, 2, 3][i]
        for x in (30, 62):
            shaded_ellipse(img, x, 84, 10, 9, rock[1:])
        shaded_ellipse(img, 48, 54 + bob, 30, 28, rock)
        for x, y, s in ((24, 30, 1), (72, 30, -1), (48, 22, 0)):
            poly(img, [(x - 6, y + 10 + bob), (x + s * 2, y - 12 + bob), (x + 6, y + 10 + bob)], crystal[2])
            poly(img, [(x - 5, y + 9 + bob), (x + s * 2, y - 9 + bob), (x - 1, y + 9 + bob)], crystal[1])
        shaded_ellipse(img, 13, 58 + bob, 9, 13, rock[1:]); shaded_ellipse(img, 83, 58 + bob, 9, 13, rock[1:])
        core = crystal if not windup else [c("#ffffff"), c("#fff8a0"), c("#ffe040"), c("#c8a0ff")]
        shaded_ellipse(img, 48, 56 + bob, 9 + (2 if windup else 0), 9 + (2 if windup else 0), core)
        rect(img, 38, 40 + bob, 42, 42 + bob, c("#ffd84a") if not windup else c("#ff4a6a"))
        rect(img, 54, 40 + bob, 58, 42 + bob, c("#ffd84a") if not windup else c("#ff4a6a"))
        for k in range(6):
            x, y = polar(48, 56 + bob, 18, k * math.pi / 3 + 0.3)
            line(img, 48, 56 + bob, x, y, rock[3])
        outline(img, OUT)
        out.append(img)
    return strip(out)


def shadow_frames():
    cloak = [c("#8a7ad8"), c("#5a4aa8"), c("#3a2e7a"), c("#241c50"), c("#140e30")]
    out = []
    for i in range(6):
        img = canvas(96, 96)
        windup = i >= 4
        bob = [0, -1, -2, -1, 1, 2][i]
        for k in range(5):
            ry = 3 + k
            ring(img, 48, 88, 14 + k * 5, 15 + k * 5, [c("#9ac0ec"), c("#6a98d8"), c("#4a6ab0"), c("#2e4a88"), c("#203a70")][k])
        poly(img, [(48, 18 + bob), (74, 86), (22, 86)], cloak[2])
        poly(img, [(48, 18 + bob), (36, 86), (22, 86)], cloak[1])
        poly(img, [(48, 18 + bob), (62, 86), (74, 86)], cloak[3])
        poly(img, [(30, 22 + bob), (48, 2 + bob), (66, 22 + bob), (48, 30 + bob)], cloak[2])
        shaded_ellipse(img, 48, 32 + bob, 11, 11, cloak)
        ellipse(img, 48, 34 + bob, 7, 6, c("#0a0618"))
        eye = c("#ff8ad0") if windup else c("#c8f4ff")
        rect(img, 43, 33 + bob, 45, 34 + bob, eye); rect(img, 51, 33 + bob, 53, 34 + bob, eye)
        for k in range(4):   # mirror shards drifting around it
            x, y = polar(48, 50 + bob, 30, k * math.pi / 2 + i * 0.25)
            poly(img, [(x - 3, y), (x, y - 5), (x + 3, y), (x, y + 4)], c("#c8ecff"))
        if windup:
            ring(img, 48, 52, 36, 37, c("#ff8ad0"))
        outline(img, OUT)
        out.append(img)
    return strip(out)


def guardian_frames():
    brass = [c("#fff0b0"), c("#e8c060"), c("#b88a30"), c("#7a5a20"), c("#4a3414")]
    out = []
    for i in range(6):
        img = canvas(96, 96)
        windup = i >= 4
        bob = [0, -1, -2, -1, 1, 2][i]
        for x0, x1 in ((48, 22), (48, 74), (48, 48)):
            line(img, x0, 60, x1, 93, brass[3], 4)
        shaded_ellipse(img, 48, 88, 20, 5, brass[2:])
        a = i * 0.35
        for k, (rx, ry) in enumerate(((38, 14), (16, 38), (34, 30))):
            ring_e = canvas(96, 96)
            shaded_ellipse(ring_e, 48, 40 + bob, rx, ry, brass[:3])
            shaded_ellipse(ring_e, 48, 40 + bob, rx - 2, ry - 2, [np.zeros(4, np.uint8)])
            m = ring_e[:, :, 3] > 0
            img[m] = ring_e[m]
        core = [c("#ffffff"), c("#fff8d0"), c("#a8e0ff"), c("#4a8ad8")] if not windup else [c("#ffffff"), c("#fff3a8"), c("#ffb040"), c("#e85020")]
        shaded_ellipse(img, 48, 40 + bob, 11, 11, core)
        rect(img, 44, 38 + bob, 52, 39 + bob, c("#1a1a3a"))
        for k in range(8):
            x, y = polar(48, 40 + bob, 44, k * math.pi / 4 + a)
            px(img, x, y, c("#fff3a8"))
        outline(img, OUT)
        out.append(img)
    return strip(out)


def grimoire_frames():
    cover = [c("#8a5ac8"), c("#5e3a9a"), c("#3e246e"), c("#28164a"), c("#180c30")]
    page = [c("#fff8e8"), c("#ecdcc0"), c("#c8b494")]
    out = []
    for i in range(6):
        img = canvas(96, 96)
        windup = i >= 4
        bob = [0, -2, -3, -2, 1, 2][i]
        shaded_ellipse(img, 48, 90, 22, 4, [c("#3a2050"), c("#241634")])
        poly(img, [(8, 30 + bob), (46, 40 + bob), (46, 78 + bob), (10, 66 + bob)], cover[2])
        poly(img, [(88, 30 + bob), (50, 40 + bob), (50, 78 + bob), (86, 66 + bob)], cover[3])
        poly(img, [(12, 30 + bob), (46, 38 + bob), (46, 72 + bob), (14, 62 + bob)], page[1])
        poly(img, [(84, 30 + bob), (50, 38 + bob), (50, 72 + bob), (82, 62 + bob)], page[2])
        for k in range(5):
            line(img, 18, 40 + k * 5 + bob, 42, 46 + k * 5 + bob, c("#8a6a5a"))
            line(img, 54, 46 + k * 5 + bob, 78, 40 + k * 5 + bob, c("#8a6a5a"))
        flap = [0, 6, 10, 6, 14, 18][i]
        poly(img, [(48, 40 + bob), (48 - 20 + flap, 22 + bob - flap // 2), (48 - 8 + flap, 18 + bob), (48, 38 + bob)], page[0])
        shaded_ellipse(img, 48, 30 + bob, 10, 8, [c("#ffffff"), c("#ffd8f0"), c("#c86aa8")])
        ellipse(img, 48, 30 + bob, 4 if not windup else 2, 6, c("#ff4a6a") if windup else c("#2a0a2a"))
        ellipse(img, 48, 10 + bob, 8, 8, c("#fff3c0")); ellipse(img, 52, 8 + bob, 7, 7, cover[1])
        for k in range(6):
            x, y = polar(48, 50 + bob, 42, k * math.pi / 3 + i * 0.2)
            poly(img, [(x - 3, y + 2), (x, y - 4), (x + 3, y + 2)], c("#c8a8ff"))
        outline(img, OUT)
        out.append(img)
    return strip(out)


def enemies():
    v = lambda *h: [c(x) for x in h]
    E = {}
    E["crystal_mite"] = (crystal_mite(v("#9a8ab8", "#6e5e92", "#4a3e6a", "#342a4e"), v("#e0c8ff", "#a878f0", "#7a50c8")), 4, (0.5, 2), 10)
    E["ore_bat"] = (bat(v("#8a78a8", "#5e4e7e", "#3e325a", "#2a2240"), c("#ffd84a")), 4, (0.5, 3), 12)
    E["gem_spitter"] = (gd.shroom(28, 32, v("#c8e8ff", "#7ab8f0", "#4a7ad0", "#2e4a98"), 5, v("#b8b0c8", "#948aa8", "#6e6488"), c("#1a1030")), 4, (0.5, 1), 6)
    E["rock_golemling"] = (hsv_shift(gd.root_guard_frames(), hue=0.62, sat=0.35, val=0.95), 4, (0.5, 2), 5)
    E["crystal_elite"] = (gd.shroom(48, 52, v("#e8d8ff", "#b090f0", "#7a58c8", "#4a3088"), 8, v("#b8b0c8", "#948aa8", "#6e6488"), c("#ff4a6a"), angry=True, scale=1.5), 4, (0.5, 1), 6)
    E["crystal_golem"] = (golem_frames(), 6, (0.5, 4), 6)
    E["lake_slime"] = (hsv_shift(gd.slime_frames(), hue=0.33, sat=0.9), 4, (0.5, 2), 8)
    E["moon_moth"] = (moth(v("#e8f0ff", "#b8c8f0", "#7a8ad0", "#4a5aa0"), v("#d8d0f0", "#9a8ac8", "#5a4a88")), 4, (0.5, 3), 10)
    E["mire_frog"] = (frog(v("#8ac0a8", "#5a9a80", "#3e7460", "#2a5446"), c("#d8e8c0")), 4, (0.5, 2), 6)
    E["reflection_knight"] = (knight(v("#a8c8ec", "#6a8ac4", "#3e5a94", "#283c6a"), c("#e8f4ff"), c("#9ac0ec")), 4, (0.5, 2), 5)
    E["shadow_clone"] = (hsv_shift(gd.wisp_frames(), hue=0.18, sat=0.8, val=0.75, keep=()), 4, (0.5, 3), 10)
    E["shadow_elite"] = (gd.shroom(48, 52, v("#a898e8", "#6a5ab8", "#40348a", "#261e5a"), 8, v("#9ab0d0", "#7088b0", "#4a6088"), c("#c8f4ff"), angry=True, scale=1.5), 4, (0.5, 1), 6)
    E["lake_shadow"] = (shadow_frames(), 6, (0.5, 4), 6)
    E["snow_slime"] = (hsv_shift(gd.slime_frames(), hue=0.32, sat=0.15, val=1.25), 4, (0.5, 2), 8)
    E["frost_wisp"] = (hsv_shift(gd.wisp_frames(), hue=0.0, sat=0.35, val=1.1, keep=()), 4, (0.5, 3), 10)
    E["lens_turret"] = (turret(v("#ffffff", "#c8ecff", "#6aa8e0", "#3a6ab0"), v("#f0d890", "#c8a050", "#8a6a2a", "#5a4418")), 4, (0.5, 1), 4)
    E["clockwork_hound"] = (hound(v("#f0d890", "#c8a050", "#8a6a2a", "#5a4418"), c("#ff6a4a")), 4, (0.5, 2), 9)
    E["gear_elite"] = (gear_elite(v("#f0d890", "#c8a050", "#8a6a2a", "#5a4418"), v("#ffffff", "#a8e0ff", "#4a8ad8")), 4, (0.5, 2), 6)
    E["star_guardian"] = (guardian_frames(), 6, (0.5, 4), 6)
    E["shade_slime"] = (hsv_shift(gd.slime_frames(), hue=0.55, sat=0.6, val=0.55), 4, (0.5, 2), 8)
    E["void_spark"] = (spark(v("#ffffff", "#e0c8ff", "#9a6ae8", "#5a30a8")), 4, (0.5, 3), 12)
    E["dark_acolyte"] = (robed(v("#6a4e8a", "#4a3466", "#2e2044", "#1a1028"), c("#c8a050"), c("#c8a8ff")), 4, (0.5, 1), 6)
    E["eclipse_knight"] = (knight(v("#7a5a9a", "#4e3a6e", "#30224a", "#1c1230"), c("#e8c060"), c("#ff6a8a")), 4, (0.5, 2), 5)
    E["thorn_wisp"] = (hsv_shift(gd.wisp_frames(), hue=-0.25, sat=1.0, val=0.95, keep=()), 4, (0.5, 3), 10)
    E["moon_elite"] = (robed(v("#7a5aa8", "#523a80", "#34245a", "#1e1438"), c("#e8c060"), c("#fff3c0"), 48, 56, halo=c("#fff3c0")), 4, (0.5, 1), 6)
    E["twisted_grimoire"] = (grimoire_frames(), 6, (0.5, 4), 6)
    for k, (img, n, piv, fps) in E.items():
        save("Enemies", f"ENM_{k}_draft", img, n, piv, fps)


# =============================================================== props and mechanisms
def prop_canvas(w, h):
    return canvas(w, h)


def crystal(w, h, ramp, base=None, lit=False):
    img = canvas(w, h)
    if base is not None:
        shaded_ellipse(img, w / 2, h - 5, w * 0.42, 5, base)
    cx = w / 2
    for dx, top, hw in ((-6, h * 0.42, 4), (6, h * 0.36, 4), (0, 2, 6)):
        poly(img, [(cx + dx - hw, h - 5), (cx + dx, top), (cx + dx + hw, h - 5)], ramp[2])
        poly(img, [(cx + dx - hw + 1, h - 6), (cx + dx, top + 2), (cx + dx, h - 6)], ramp[1])
        line(img, cx + dx - 1, top + 4, cx + dx - 1, h - 9, ramp[0])
    if lit:
        for k in range(6):
            x, y = polar(cx, h * 0.45, w * 0.48, k * math.pi / 3)
            px(img, x, y, c("#fff8a0"))
    outline(img, OUT)
    return img


def tree(w, h, ramp, trunk, seed, snow=False, kind="pine"):
    img = gd.tree(w, h, ramp, trunk, seed, kind)
    if snow:
        m = img[:, :, 3] > 0
        rng_ = np.random.default_rng(seed)
        for y in range(0, h - 20, 7):
            row = np.where(m[y])[0]
            if len(row):
                x0, x1 = row.min(), row.max()
                rect(img, x0 + 2, y, x1 - 2, y + 1, c("#ffffff"))
                px(img, int(rng_.integers(x0 + 1, max(x0 + 2, x1))), y + 2, c("#e2eaf2"))
    return img


def boulder_prop(w, h, ramp, glint=None, snow=False, seed=0):
    img = canvas(w, h)
    r = np.random.default_rng(seed)
    for k in range(3):
        cx = w / 2 + (k - 1) * w * 0.22
        shaded_ellipse(img, cx, h - h * 0.35 - (k == 1) * 3, w * 0.28, h * 0.32, ramp)
    if glint is not None:
        poly(img, [(w * 0.62, h * 0.5), (w * 0.68, h * 0.15), (w * 0.74, h * 0.5)], glint[1])
        line(img, w * 0.67, h * 0.2, w * 0.67, h * 0.45, glint[0])
    if snow:
        for x in range(int(w * 0.15), int(w * 0.85)):
            ys = np.where(img[:, x, 3] > 0)[0]
            if len(ys):
                img[ys.min():ys.min() + 2, x] = c("#ffffff")
    outline(img, OUT)
    return img


def pillar(w, h, ramp, cap, broken=False):
    img = canvas(w, h)
    top = h * (0.4 if broken else 0.12)
    rect(img, w * 0.2, top, w * 0.8, h - 4, ramp[1])
    rect(img, w * 0.2, top, w * 0.32, h - 4, ramp[0])
    rect(img, w * 0.68, top, w * 0.8, h - 4, ramp[2])
    rect(img, w * 0.1, h - 6, w * 0.9, h - 2, ramp[2])
    if not broken:
        rect(img, w * 0.1, top - 3, w * 0.9, top, cap)
    else:
        poly(img, [(w * 0.2, top), (w * 0.45, top - 6), (w * 0.6, top + 2), (w * 0.8, top - 3), (w * 0.8, top + 3), (w * 0.2, top + 3)], ramp[1])
    for y in range(int(top) + 6, h - 8, 9):
        line(img, w * 0.25, y, w * 0.75, y, ramp[2])
    outline(img, OUT)
    return img


def props():
    v = lambda *h: [c(x) for x in h]
    P_ = {}
    cry = v("#ffffff", "#c8a8ff", "#8a5ad8", "#5a3aa8")
    rock = v("#6a647e", "#4e4862", "#38324a", "#26223a")
    # mechanisms
    P_["PROP_crystal_node"] = (crystal(28, 40, v("#a8a0c0", "#7a7098", "#524a6e"), rock[1:]), 1)
    P_["PROP_crystal_node_lit"] = (crystal(28, 40, v("#ffffff", "#fff3a8", "#c8a0ff"), rock[1:], lit=True), 1)
    cart = canvas(34, 28)
    rect(cart, 3, 8, 30, 20, c("#8a6a4a")); rect(cart, 3, 8, 30, 10, c("#a8865e")); rect(cart, 3, 18, 30, 20, c("#5a4430"))
    for x in (5, 14, 23):
        rect(cart, x, 9, x + 1, 19, c("#6a6478"))
    for x in (9, 25):
        shaded_ellipse(cart, x, 23, 3.5, 3.5, rock)
    for k in range(5):
        shaded_ellipse(cart, 8 + k * 4.5, 7 - (k % 2), 3, 3, cry[1:])
    rect(cart, 15, 0, 17, 6, c("#c8463a"))   # fuse
    P_["PROP_minecart"] = (outline(cart, OUT), 1)
    for name, horiz in (("PROP_rail_h", True), ("PROP_rail_v", False)):
        r_ = canvas(32, 32)
        for k in range(0, 32, 8):
            if horiz: rect(r_, k + 2, 11, k + 5, 21, c("#6a5038"))
            else: rect(r_, 11, k + 2, 21, k + 5, c("#6a5038"))
        if horiz:
            rect(r_, 0, 12, 31, 13, c("#9a96a8")); rect(r_, 0, 19, 31, 20, c("#9a96a8"))
        else:
            rect(r_, 12, 0, 13, 31, c("#9a96a8")); rect(r_, 19, 0, 20, 31, c("#9a96a8"))
        P_[name] = (r_, 0.5)
    P_["PROP_rubble"] = (boulder_prop(40, 32, rock, cry[1:], seed=4), 1)
    P_["PROP_mine_rock"] = (boulder_prop(36, 30, rock, None, seed=5), 1)
    P_["PROP_mine_crystal"] = (crystal(32, 48, cry[1:], rock[1:]), 1)
    sup = canvas(40, 56)
    rect(sup, 4, 6, 9, 55, c("#8a6a4a")); rect(sup, 31, 6, 36, 55, c("#6a5038")); rect(sup, 2, 4, 38, 10, c("#a8865e"))
    rect(sup, 4, 6, 5, 55, c("#a8865e"))
    P_["PROP_mine_support"] = (outline(sup, OUT), 1)
    P_["PROP_crystal_altar"] = (crystal(48, 44, cry[1:], rock[1:], lit=True), 1)
    # lake
    reed = canvas(20, 30)
    for k, x in enumerate((4, 8, 11, 15)):
        line(reed, x, 29, x + (k % 2), 6 + k * 2, c("#4a7a5a")); px(reed, x + (k % 2), 5 + k * 2, c("#a8c87a"))
        rect(reed, x + (k % 2), 7 + k * 2, x + (k % 2), 10 + k * 2, c("#8a5a3a"))
    P_["PROP_reed"] = (outline(reed, OUT), 1)
    P_["PROP_lake_crystal"] = (crystal(30, 44, v("#ffffff", "#c8ecff", "#6aa8e0", "#3a6ab0"), v("#4a6a62", "#2e4442")), 1)
    P_["PROP_ruin_pillar"] = (pillar(24, 56, v("#c8d0e0", "#9aa4bc", "#6e7890"), c("#e0e8f4")), 1)
    P_["PROP_ruin_pillar_broken"] = (pillar(24, 56, v("#c8d0e0", "#9aa4bc", "#6e7890"), c("#e0e8f4"), True), 1)
    P_["PROP_willow"] = (tree(60, 80, v("#6aa89a", "#4e8a7e", "#386a62", "#264c48"), v("#8a7a6a", "#6a5a4c", "#4a3e34"), 17, kind="round"), 1)
    pyl_off = pillar(20, 52, v("#8a94a8", "#6a7488", "#4a5266"), c("#5a6478"))
    pyl_on = pillar(20, 52, v("#c8ecff", "#7ab8e8", "#4a7ac0"), c("#fff8a0"))
    for img_ in (pyl_off, pyl_on):
        shaded_ellipse(img_, 10, 6, 5, 5, [c("#ffffff"), c("#fff3a8"), c("#c8a0ff")] if img_ is pyl_on else [c("#a8b0c0"), c("#7a849a"), c("#525a6e")])
        outline(img_, OUT)
    P_["PROP_pylon"] = (pyl_off, 1)
    P_["PROP_pylon_lit"] = (pyl_on, 1)
    for name, lit in (("PROP_light_bridge_off", False), ("PROP_light_bridge_on", True)):
        br = canvas(32, 32)
        if lit:
            rect(br, 0, 6, 31, 25, c("#9ad8ff")); rect(br, 0, 6, 31, 8, c("#e0f8ff")); rect(br, 0, 23, 31, 25, c("#5a9ad8"))
            for x in range(2, 32, 8):
                rect(br, x, 12, x + 3, 18, c("#e0f8ff"))
        else:
            for x in range(1, 32, 8):
                rect(br, x, 10, x + 4, 21, c("#5a6478")); rect(br, x, 10, x + 4, 11, c("#7a849a"))
        P_[name] = (br, 0.5)
    tent = canvas(56, 44)
    poly(tent, [(28, 2), (54, 40), (2, 40)], c("#c86a5a")); poly(tent, [(28, 2), (16, 40), (2, 40)], c("#e08a6a"))
    poly(tent, [(28, 14), (36, 40), (20, 40)], c("#3a2030")); rect(tent, 2, 40, 54, 42, c("#8a5a4a"))
    P_["PROP_tent"] = (outline(tent, OUT), 1)
    # observatory
    P_["PROP_snow_pine"] = (tree(52, 80, v("#4a8a6a", "#2e6a50", "#1e4a3a"), v("#7a5a3a", "#5a3e28", "#3e2a1c"), 23, snow=True), 1)
    P_["PROP_snow_rock"] = (boulder_prop(36, 30, v("#9a9cb0", "#74768c", "#56586c", "#3c3e50"), None, snow=True, seed=7), 1)
    brass = v("#f0d890", "#c8a050", "#8a6a2a", "#5a4418")
    emit = canvas(24, 40)
    rect(emit, 8, 16, 15, 38, brass[2]); rect(emit, 4, 36, 19, 39, brass[3])
    shaded_ellipse(emit, 12, 12, 8, 8, brass); shaded_ellipse(emit, 12, 11, 5, 5, v("#ffffff", "#fff3a8", "#f2c45a"))
    P_["PROP_beam_emitter"] = (outline(emit, OUT), 1)
    rec = canvas(24, 40)
    rect(rec, 8, 16, 15, 38, brass[2]); rect(rec, 4, 36, 19, 39, brass[3])
    for k in range(5):
        x, y = polar(12, 11, 8, k * 2 * math.pi / 5 - math.pi / 2)
        line(rec, 12, 11, x, y, brass[1], 2)
    shaded_ellipse(rec, 12, 11, 4, 4, v("#c8ecff", "#6aa8e0", "#3a6ab0"))
    P_["PROP_beam_receiver"] = (outline(rec, OUT), 1)
    mir = canvas(24, 36)
    rect(mir, 10, 22, 13, 34, brass[2]); rect(mir, 6, 33, 17, 35, brass[3])
    poly(mir, [(3, 22), (17, 2), (21, 6), (7, 26)], c("#c8ecff")); line(mir, 6, 21, 17, 5, c("#ffffff"))
    poly(mir, [(3, 22), (7, 26), (8, 25), (4, 21)], brass[1])
    P_["PROP_mirror"] = (outline(mir, OUT), 1)
    door = canvas(32, 48)
    rect(door, 2, 4, 29, 47, c("#5a5c70")); rect(door, 5, 8, 26, 47, c("#3a3c50"))
    for k in range(5):
        x, y = polar(16, 24, 7, k * 2 * math.pi / 5 - math.pi / 2)
        line(door, 16, 24, x, y, c("#f2c45a"))
    ellipse(door, 16, 24, 3, 3, c("#fff3a8"))
    P_["PROP_star_door"] = (outline(door, OUT), 1)
    tele = canvas(64, 64)
    line(tele, 32, 40, 14, 62, brass[3], 3); line(tele, 32, 40, 50, 62, brass[3], 3)
    poly(tele, [(10, 30), (54, 6), (58, 14), (14, 38)], brass[1]); poly(tele, [(10, 30), (54, 6), (55, 9), (11, 33)], brass[0])
    shaded_ellipse(tele, 56, 10, 5, 5, v("#ffffff", "#c8ecff", "#6aa8e0"))
    P_["PROP_telescope"] = (outline(tele, OUT), 1)
    hut = canvas(56, 52)
    rect(hut, 6, 24, 50, 50, c("#8a6a4a")); rect(hut, 6, 24, 10, 50, c("#a8865e"))
    poly(hut, [(2, 26), (28, 4), (54, 26)], c("#e2eaf2")); poly(hut, [(2, 26), (28, 4), (28, 8), (8, 26)], c("#ffffff"))
    rect(hut, 23, 34, 33, 50, c("#3a2a20")); rect(hut, 38, 30, 46, 37, c("#ffe080"))
    P_["PROP_snow_hut"] = (outline(hut, OUT), 1)
    # citadel
    stone = v("#5a4a72", "#3e3054", "#2a2040")
    P_["PROP_dark_pillar"] = (pillar(26, 64, stone, c("#e8c060")), 1)
    P_["PROP_dark_pillar_broken"] = (pillar(26, 64, stone, c("#e8c060"), True), 1)
    braz = canvas(24, 36)
    rect(braz, 9, 16, 14, 34, stone[1]); rect(braz, 5, 33, 18, 35, stone[2])
    shaded_ellipse(braz, 12, 14, 9, 4, stone)
    for k in range(4):
        poly(braz, [(6 + k * 4, 13), (8 + k * 4, 3 + (k % 2) * 3), (10 + k * 4, 13)], [c("#e0c8ff"), c("#9a6ae8")][k % 2])
    P_["PROP_moon_brazier"] = (outline(braz, OUT), 1)
    ban = canvas(24, 52)
    rect(ban, 3, 2, 20, 4, c("#c8a050")); poly(ban, [(5, 4), (18, 4), (18, 46), (11, 40), (5, 46)], c("#4a2a6e"))
    ellipse(ban, 11, 20, 5, 5, c("#fff3c0")); ellipse(ban, 13, 19, 4, 4, c("#4a2a6e"))
    P_["PROP_banner"] = (outline(ban, OUT), 1)
    seal = canvas(44, 40)
    shaded_ellipse(seal, 22, 30, 20, 8, stone)
    ring(seal, 22, 30, 12, 14, c("#c8a8ff")); ring(seal, 22, 30, 5, 6, c("#fff3c0"))
    rect(seal, 19, 4, 25, 28, stone[1]); rect(seal, 19, 4, 20, 28, stone[0])
    poly(seal, [(22, 0), (27, 6), (22, 12), (17, 6)], c("#e0c8ff"))
    P_["PROP_trial_seal"] = (outline(seal, OUT), 1)
    bar = canvas(32, 48)
    for x in range(2, 32, 6):
        rect(bar, x, 2, x + 2, 47, c("#7a4ac8")); rect(bar, x, 2, x, 47, c("#c8a8ff"))
    rect(bar, 0, 20, 31, 22, c("#e0c8ff"))
    P_["PROP_dark_barrier"] = (outline(bar, OUT), 1)
    tower = canvas(72, 112)
    rect(tower, 12, 24, 60, 111, stone[1]); rect(tower, 12, 24, 20, 111, stone[0]); rect(tower, 52, 24, 60, 111, stone[2])
    for x in range(10, 64, 10):
        rect(tower, x, 16, x + 5, 24, stone[1])
    rect(tower, 30, 40, 42, 58, c("#c8a8ff")); rect(tower, 33, 43, 39, 55, c("#fff3c0"))
    rect(tower, 28, 86, 44, 111, c("#140c1e"))
    for y in range(30, 108, 9):
        line(tower, 20, y, 52, y, stone[2])
    P_["PROP_tower"] = (outline(tower, OUT), 1)
    # notes, altar dressing, ambience
    note = canvas(20, 30)
    rect(note, 8, 14, 11, 29, c("#6a5038")); rect(note, 4, 28, 15, 29, c("#4a3828"))
    poly(note, [(2, 4), (17, 2), (18, 15), (3, 16)], c("#fff8e8")); line(note, 5, 7, 14, 6, c("#8a6a5a")); line(note, 5, 10, 14, 9, c("#8a6a5a"))
    px(note, 16, 3, c("#fff3a8"))
    P_["PROP_note"] = (outline(note, OUT), 1)
    for name, (img, pivot) in P_.items():
        save("Props", name + "_draft", img, pivot=(0.5, pivot if pivot == 0.5 else 1))
    snow = canvas(4, 4)
    rect(snow, 1, 0, 2, 3, c("#ffffff")); rect(snow, 0, 1, 3, 2, c("#ffffff"))
    save("Props", "DEC_snowflake_draft", snow, pivot=(0.5, 0.5))


# =============================================================== VFX and icons
def vfx():
    v = lambda *h: [c(x) for x in h]
    CRY = v("#ffffff", "#e0c8ff", "#a878f0", "#6a44c0")
    MOON = v("#ffffff", "#d8ccff", "#8a6ae8", "#4a3aa8")
    STARC = v("#ffffff", "#fff3a8", "#f2c45a", "#c88a2a")
    NAT = v("#f0ffd0", "#b0e060", "#6aa83a", "#3a6a2a")
    WATER = v("#ffffff", "#c8ecff", "#6aa8e0", "#3a6ab0")
    BEAM = v("#ffffff", "#fff8d0", "#ffe080", "#e8b040")

    def shard(t, i, img):
        poly(img, [(1, 6), (10, 3), (15, 6), (10, 9)], CRY[2]); poly(img, [(3, 6), (10, 4), (13, 6)], CRY[1])
        px(img, 11, 5 + i % 2, CRY[0])
        return outline(img, OUT)
    save("VFX", "VFX_crystal_shard_draft", gd.fx_frames(4, 16, 12, shard), 4, (0.5, 0.5), 12)

    def beam(t, i, img):
        rect(img, 0, 4, 23, 7, BEAM[2]); rect(img, 0, 5, 23, 6, BEAM[0 if i % 2 else 1])
        for k in range(0, 24, 6):
            px(img, (k + i * 2) % 24, 3, BEAM[1]); px(img, (k + 3 + i * 2) % 24, 8, BEAM[3])
        return img
    save("VFX", "VFX_beam_seg_draft", gd.fx_frames(4, 24, 12, beam), 4, (0.5, 0.5), 16)

    def bolt(t, i, img):
        shaded_ellipse(img, 13, 7, 5, 4, MOON[:3])
        for k in range(3):
            px(img, 7 - k * 2, 6 + (i + k) % 3 - 1, MOON[2 + k % 2])
        px(img, 12, 6, MOON[0])
        return outline(img, c("#1a1030"))
    save("VFX", "VFX_shadow_bolt_draft", gd.fx_frames(4, 20, 14, bolt), 4, (0.5, 0.5), 12)

    def star(t, i, img):
        for k in range(4):
            a = k * math.pi / 2 + i * math.pi / 8
            x, y = polar(8, 8, 6, a)
            line(img, 8, 8, x, y, STARC[1])
        shaded_ellipse(img, 8, 8, 3, 3, STARC[:3])
        return img
    save("VFX", "VFX_star_shot_draft", gd.fx_frames(4, 16, 16, star), 4, (0.5, 0.5), 14)

    def drop(t, i, img):
        shaded_ellipse(img, 6, 7, 3.5, 3.5 + (i % 2) * 0.5, WATER[:3]); px(img, 5, 5, WATER[0])
        return outline(img, c("#1a2a4a"))
    save("VFX", "VFX_water_drop_draft", gd.fx_frames(4, 12, 12, drop), 4, (0.5, 0.5), 10)

    def thorn(t, i, img):
        n = 6
        r = 4 + t * 12
        for k in range(n):
            a = k * 2 * math.pi / n + 0.3
            x, y = polar(16, 18, r, a)
            line(img, 16, 18, x, y, NAT[2], 2)
            px(img, x, y, NAT[0])
        if t < 0.6:
            shaded_ellipse(img, 16, 18, 4 - t * 3, 4 - t * 3, NAT[:3])
        return dither_alpha(img, 1.0 - max(0, t - 0.7) * 3)
    save("VFX", "VFX_thorn_draft", gd.fx_frames(6, 32, 32, thorn), 6, (0.5, 0.5), 14)

    def hand(t, i, img):
        rise = min(1, t * 2.2)
        h = int(4 + rise * 20)
        for k, dx in enumerate((-6, -2, 2, 6)):
            line(img, 16 + dx * 0.6, 30, 16 + dx, 30 - h + abs(dx) * 0.6, MOON[2 + k % 2], 2)
            px(img, 16 + dx, 30 - h + abs(dx) * 0.6, MOON[1])
        ellipse(img, 16, 30, 10, 2, MOON[3])
        return dither_alpha(img, 1.0 - max(0, t - 0.75) * 4)
    save("VFX", "VFX_shadow_hand_draft", gd.fx_frames(6, 32, 32, hand), 6, (0.5, 0.15), 12)


def icons():
    v = lambda *h: [c(x) for x in h]
    I = {}
    img = canvas(32, 32)
    line(img, 4, 27, 27, 4, c("#ffe080"), 3); line(img, 4, 27, 27, 4, c("#ffffff"))
    shaded_ellipse(img, 6, 25, 4, 4, v("#ffffff", "#c8a0ff", "#8a50e0"))
    I["ICON_prism_beam"] = img
    img = canvas(32, 32)
    for k in range(5):
        x, y = polar(16, 15, 7, k * 2 * math.pi / 5 - math.pi / 2)
        shaded_ellipse(img, x, y, 5, 5, v("#f0ffd0", "#b0e060", "#6aa83a"))
    shaded_ellipse(img, 16, 15, 4, 4, v("#fff3a8", "#e0a040"))
    line(img, 16, 22, 16, 30, c("#3a6a2a"), 2)
    I["ICON_thorn_bloom"] = img
    img = canvas(32, 32)
    for k, dx in enumerate((-8, -3, 2, 7)):
        line(img, 16 + dx * 0.5, 29, 16 + dx, 6 + abs(dx), [c("#8a6ae8"), c("#c8b8ff")][k % 2], 3)
    ellipse(img, 16, 28, 12, 3, c("#4a3aa8"))
    I["ICON_shadow_hand"] = img
    img = canvas(32, 32)
    for k, (x, a) in enumerate(((10, 1.0), (19, 0.7), (26, 0.45))):
        shaded_ellipse(img, x, 16, 5 * a + 1, 4 * a + 1, v("#ffffff", "#d8ccff", "#8a6ae8", "#4a3aa8"))
    I["ICON_moon_echo"] = img
    img = canvas(32, 32)
    for k in range(5):
        x, y = polar(16, 16, 13, k * 2 * math.pi / 5 - math.pi / 2)
        line(img, 16, 16, x, y, c("#f2c45a"), 3)
    shaded_ellipse(img, 16, 16, 6, 6, v("#ffffff", "#fff3a8", "#f2c45a"))
    line(img, 22, 22, 29, 29, c("#fff3a8"))
    I["ICON_star_shot"] = img
    for k, img in I.items():
        outline(img, OUT)
        save("UI", k + "_draft", img, pivot=(0.5, 0.5))


def main():
    biome_tiles()
    enemies()
    props()
    vfx()
    icons()
    with open(os.path.join(ART, "chapter_manifest.json"), "w") as f:
        json.dump({"status": "procedural draft (Needs Cleanup), chapters 2-5", "items": MANIFEST}, f, indent=1)
    print("wrote", len(MANIFEST), "sprites + biome tiles for", ", ".join(BIOMES))


if __name__ == "__main__":
    main()
