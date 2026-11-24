"""Moonshade Forest (月影森林) 64 x 64 hand-made skeleton (WORLD_STRUCTURE.md §4, GDD §11.2).

Z01 entrance -> fork: Z02 mushroom lane (west) / chest branch (east) -> Z03 star altar clearing ->
Z04 river with a broken stone bridge (lever repairs it) and a vine-blocked ford shortcut (burn with fire)
-> Z05 boss arena. Open ground is carved from a solid forest; everything else is blocked tree wall.
Spawn grid: 's' spawnable, 'w' walkable but no spawning (entrance, bridge, arena), '.' blocked.
Decor placement is seeded (same seed + same version = same map).
Writes Assets/_Game/Data/Maps/forest.json. Usage: python3 tools/map/forest.py <repo_root>
"""
import json, math, os, sys
import numpy as np

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
W = H = 64
SEED = 64064
rng = np.random.default_rng(SEED)

open_ = np.zeros((H, W), bool)   # [y][x], y up
path = np.zeros((H, W), bool)
water = np.zeros((H, W), bool)
nospawn = np.zeros((H, W), bool)


def disk(cx, cy, r, m, val=True):
    ys, xs = np.mgrid[0:H, 0:W]
    m[((xs + 0.5 - cx) ** 2 + (ys + 0.5 - cy) ** 2) <= r * r] = val


def ell(cx, cy, rx, ry, m, val=True):
    ys, xs = np.mgrid[0:H, 0:W]
    m[(((xs + 0.5 - cx) / rx) ** 2 + ((ys + 0.5 - cy) / ry) ** 2) <= 1] = val


def box(x0, x1, y0, y1, m, val=True):
    m[max(0, y0):min(H, y1 + 1), max(0, x0):min(W, x1 + 1)] = val


def stroke(pts, width, m):
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = int(max(abs(x1 - x0), abs(y1 - y0)) * 3) + 1
        for i in range(n + 1):
            t = i / n
            disk(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, width / 2, m)


# ---------------------------------------------------------------- carve open ground
ell(32, 8, 11, 7.5, open_)                         # Z01 entrance clearing
box(29, 35, 0, 3, open_)                           # entrance mouth (from the village)
stroke([(32, 13), (30, 17), (20, 21), (16, 26), (20, 31), (27, 34)], 6, open_)    # Z02 mushroom lane
ell(17, 24, 7, 5.5, open_)                         # Z02 wide part
stroke([(33, 13), (40, 18), (48, 21), (50, 26), (42, 31), (36, 34)], 5, open_)    # east branch
ell(50, 23, 5, 4.5, open_)                         # chest nook
ell(32, 39, 10.5, 7.5, open_)                      # Z03 altar clearing (>= 12 x 12)
stroke([(32, 45), (32, 47)], 5, open_)             # to the bridge
box(30, 33, 46, 53, open_)                         # bridge approach + bridge
stroke([(25, 41), (18, 43), (13, 45), (12.5, 47)], 3, open_)                      # vine shortcut to the ford
box(11, 14, 46, 53, open_)                         # ford
stroke([(12.5, 52), (16, 54), (24, 56)], 3, open_)
disk(32, 58.5, 8.2, open_)                         # Z05 boss arena (>= 14 x 12)
box(29, 35, 52, 53, open_)

# river across the whole map (Z04)
box(0, 63, 48, 50, water)
water &= True
# west pond in the mushroom lane
ell(12, 21, 2.6, 1.8, water)

# paths (visual dirt road on the main route)
stroke([(32, 0), (32, 13), (30, 17), (20, 21), (16, 26), (20, 31), (27, 34), (32, 36)], 2.2, path)
stroke([(33, 13), (40, 18), (48, 21), (50, 25)], 1.8, path)
stroke([(32, 42), (32, 53), (32, 55)], 2.2, path)
stroke([(25, 41), (18, 43), (13, 45), (12.5, 52), (16, 54), (24, 56)], 1.4, path)
path &= open_
path[water] = False
# ford stepping path drawn over the water (walkable)
ford = np.zeros((H, W), bool)
box(12, 13, 48, 50, ford)
path |= ford

# bridge cells (walkable only after the lever repairs the gap)
BRIDGE = (31, 32, 47, 52)
bridge_cells = np.zeros((H, W), bool)
box(BRIDGE[0], BRIDGE[1], BRIDGE[2], BRIDGE[3], bridge_cells)

nospawn_mask = np.zeros((H, W), bool)
ell(32, 3, 8, 6, nospawn_mask)                     # entrance safety
disk(32, 58.5, 9.5, nospawn_mask)                  # boss arena
nospawn_mask |= bridge_cells | ford
box(0, 63, 47, 52, nospawn_mask)                   # river banks

walk = open_ & ~water
walk |= bridge_cells | ford
blocked = ~walk

# ---------------------------------------------------------------- layers (row 0 = top)
def rows(fn):
    return ["".join(fn(x, H - 1 - r) for x in range(W)) for r in range(H)]


layers = {
    "water": rows(lambda x, y: "~" if water[y, x] and not ford[y, x] else "."),
    "path": rows(lambda x, y: "p" if path[y, x] else "."),
    "collision": rows(lambda x, y: "#" if (blocked[y, x] or x in (0, W - 1) or y == H - 1 or (y == 0 and not 29 <= x <= 35)) and not bridge_cells[y, x] else "."),
    "spawn": rows(lambda x, y: "." if blocked[y, x] else ("w" if nospawn_mask[y, x] else "s")),
}

# ---------------------------------------------------------------- decor (seeded)
props = []
TREES = [("PROP_forest_oak_draft", 0.6), ("PROP_forest_pine_draft", 0.45), ("PROP_forest_dark_draft", 0.55),
         ("PROP_tree_green_b_draft", 0.5)]


def near_open(x, y, r):
    x0, x1, y0, y1 = max(0, int(x - r)), min(W, int(x + r) + 1), max(0, int(y - r)), min(H, int(y + r) + 1)
    return walk[y0:y1, x0:x1].any()


# trees: on blocked cells, denser near the walkable edge so the wall reads as forest
for gy in np.arange(0.6, H, 1.7):
    for gx in np.arange(0.5, W, 1.9):
        x = gx + rng.uniform(-0.5, 0.5) + (0.9 if int(gy / 1.7) % 2 else 0)
        y = gy + rng.uniform(-0.3, 0.3)
        xi, yi = int(x), int(y)
        if not (0 <= xi < W and 0 <= yi < H) or walk[yi, xi] or water[yi, xi]:
            continue
        if not near_open(x, y, 3.2) and rng.random() < 0.55:
            continue  # deep forest: sparser (cheaper), still covered by canopy
        name, _ = TREES[int(rng.integers(0, len(TREES)))]
        props.append({"sprite": name, "x": round(float(x), 2), "y": round(float(y), 2), "fade": True})

decor = []
for _ in range(70):
    x, y = rng.uniform(2, W - 2), rng.uniform(2, H - 2)
    xi, yi = int(x), int(y)
    if not walk[yi, xi] or path[yi, xi] or bridge_cells[yi, xi] or ford[yi, xi] or nospawn_mask[yi, xi] and not (52 < y):
        continue
    if 52 < y and math.hypot(x - 32, y - 58.5) < 7:   # keep the boss arena clear (GDD §10.3)
        continue
    k = rng.random()
    if k < 0.35:
        decor.append({"sprite": "PROP_glow_shroom_draft", "x": round(x, 2), "y": round(y, 2), "fade": False})
    elif k < 0.55:
        decor.append({"sprite": "PROP_mushroom_glow_draft", "x": round(x, 2), "y": round(y, 2), "fade": False})
    elif k < 0.7:
        decor.append({"sprite": "PROP_rock_dark_draft", "x": round(x, 2), "y": round(y, 2), "fade": False, "box": [0.7, 0.35, 0, 0.18]})
    elif k < 0.85:
        decor.append({"sprite": "PROP_bush_draft", "x": round(x, 2), "y": round(y, 2), "fade": False})
    else:
        decor.append({"sprite": "PROP_log_draft", "x": round(x, 2), "y": round(y, 2), "fade": False, "box": [1.2, 0.35, 0, 0.18]})
props += decor
# altar ring pillars (Z03) and arena pillars (Z05)
for k in range(6):
    a = k * math.pi / 3 + math.pi / 6
    props.append({"sprite": "PROP_pillar_draft", "x": round(32 + math.cos(a) * 6.2, 2), "y": round(39 + math.sin(a) * 4.6, 2),
                  "fade": True, "box": [0.7, 0.4, 0, 0.2]})
for k in range(8):
    a = k * math.pi / 4 + math.pi / 8
    props.append({"sprite": "PROP_pillar_draft", "x": round(32 + math.cos(a) * 9.0, 2), "y": round(58.5 + math.sin(a) * 6.4, 2),
                  "fade": True})
props.append({"sprite": "PROP_sign_draft", "x": 34.4, "y": 4.2, "fade": False, "box": [0.35, 0.25, 0, 0.12]})

objects = [
    {"kind": "altar", "x": 32.0, "y": 39.0},
    {"kind": "chest", "x": 51.0, "y": 24.4, "page": True},
    {"kind": "chest", "x": 14.4, "y": 26.2, "page": False},
    {"kind": "vine", "x": 12.5, "y": 46.2, "block": [3.0, 0.8]},
    {"kind": "bridge_stone", "x": 32.0, "y": 49.5, "gap": [2.0, 3.0], "lever": [29.2, 45.4]},
    {"kind": "boss_arena", "trigger": [25.0, 54.5, 14.0, 10.0], "bossSpawn": [32.0, 59.5], "gates": [[32.0, 53.2, 4.0, 0.8], [17.5, 54.0, 1.2, 3.0]]},
    {"kind": "sign", "x": 34.4, "y": 4.2, "name": "森林告示牌",
     "lines": ["Z01 森林入口 → 北方岔路：西為蘑菇林道，東為寶箱支線。", "星痕祭壇在北方空地；斷橋旁的拉桿可以修橋。",
               "想撤退：按 Esc 選擇放棄本局（材料只保留一半）。"]},
]
spawns = [{"id": "spawn_from_village", "x": 32.0, "y": 2.4}, {"id": "spawn_default", "x": 32.0, "y": 2.4}]
zones = [
    {"id": "Z05", "name": "Z05 菇王棲地", "rect": [0, 53, 64, 11], "noSpawn": True},
    {"id": "Z04", "name": "Z04 斷裂石橋", "rect": [0, 46, 64, 7]},
    {"id": "Z03", "name": "Z03 星痕祭壇", "rect": [0, 32, 64, 14]},
    {"id": "Z02", "name": "Z02 蘑菇林道", "rect": [0, 15, 64, 17]},
    {"id": "Z01", "name": "Z01 森林入口", "rect": [0, 0, 64, 15]},
]
data = {"id": "forest", "width": W, "height": H, "seed": SEED, "biome": "forest",
        "layers": layers, "props": props, "objects": objects, "spawns": spawns, "zones": zones,
        "scene": {"title": "月影森林", "dim": 0.08, "safe": False, "objective": "向北深入森林", "chapter": "forest"}}
json.dump(data, open(os.path.join(ROOT, "Assets/_Game/Data/Maps/forest.json"), "w"), indent=1, ensure_ascii=False)
merged = []
for r in range(H):
    y = H - 1 - r
    merged.append("".join("B" if bridge_cells[y, x] else "~" if water[y, x] and not ford[y, x] else "p" if path[y, x] else " " if walk[y, x] else "#" for x in range(W)))
print("\n".join("%2d %s" % (H - 1 - r, row) for r, row in enumerate(merged)))
print("props", len(props), "spawnable", int((walk & ~nospawn_mask).sum()))
