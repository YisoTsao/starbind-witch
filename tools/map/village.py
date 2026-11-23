"""Full Starlumen Village (星露村) 48 x 40 layout (WORLD_STRUCTURE.md §2).

Embeds the Phase-0 north block (village_north.json, 28 x 20, made from reference A) at offset
(DX, DY) = (10, 20) and extends it: west plateau + river flowing south, wooden bridge with the archer
on the west bank, the priest on the south road, the knight's training ground (dummies, targets) in the
south-east, a star plate to the east and the forest exit at the south edge.
Writes Assets/_Game/Data/Maps/village.json (read by Starbind.Editor.GameContentBuilder).
Usage: python3 tools/map/village.py <repo_root>
"""
import json, math, os, sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
W, H, DX, DY = 48, 40, 10, 20
north = json.load(open(os.path.join(ROOT, "Assets/_Game/Data/Maps/village_north.json")))
NW, NH = north["width"], north["height"]


def grid(ch="."):
    return [[ch] * W for _ in range(H)]  # [r][x], r = 0 is the top row (y = H - 1)


def rc(y):
    return H - 1 - y


def setc(g, x, y, ch):
    if 0 <= x < W and 0 <= y < H:
        g[rc(y)][x] = ch


def fill(g, x0, x1, y0, y1, ch):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            setc(g, x, y, ch)


def get(g, x, y):
    return g[rc(y)][x] if 0 <= x < W and 0 <= y < H else "#"


water, path, cliff, coll = grid(), grid(), grid(), grid()

# ---------------------------------------------------------------- north block (from reference A)
for name, g in (("water", water), ("path", path), ("cliff", cliff), ("collision", coll)):
    rows = north["layers"][name]
    for r, row in enumerate(rows):
        ny = NH - 1 - r
        for nx, ch in enumerate(row):
            if ch == ".":
                continue
            # the block's own perimeter collision is replaced by the big map's perimeter
            if name == "collision" and (nx in (0, NW - 1) or ny == 0) and water[rc(ny + DY)][nx + DX] != "~" \
                    and cliff[rc(ny + DY)][nx + DX] == ".":
                continue
            setc(g, nx + DX, ny + DY, ch)

# ---------------------------------------------------------------- west plateau + river
fill(cliff, 0, 9, 35, 39, "R")
fill(cliff, 0, 9, 34, 34, "F")

# ---------------------------------------------------------------- 2-tile-tall cliff faces (ASSET_GAPS TILE_cliff)
# A face row under a plateau grows up into the plateau's first row (the plateau pond moves up with it);
# a free-standing ledge face grows down one row and gets collision there.
NEW_FACE = set()
for x in range(W):
    for y in range(H):
        if get(cliff, x, y) in "FW" and get(cliff, x, y + 1) == "R" and get(cliff, x, y - 1) not in "FW":
            setc(cliff, x, y + 1, get(cliff, x, y))
            NEW_FACE.add((x, y + 1))
        elif get(cliff, x, y) in "FW" and get(cliff, x, y + 1) == "." and get(cliff, x, y - 1) == "." \
                and (x, y) not in NEW_FACE:
            setc(cliff, x, y - 1, get(cliff, x, y))
            setc(coll, x, y - 1, "#")
            NEW_FACE.add((x, y - 1))
for x in range(W):                      # pond on the plateau: rows directly above the face move up one row
    for y in range(H - 2, -1, -1):
        if (x, y) in NEW_FACE and get(water, x, y) == "~":
            yy = y
            while get(water, x, yy + 1) == "~":
                yy += 1
            setc(water, x, yy + 1, "~")
            setc(water, x, y, ".")
# river continues from the block bottom (x 12..15 at y 20) to the south edge, bending west
fill(water, 12, 15, 12, 19, "~")
fill(water, 11, 14, 6, 11, "~")
fill(water, 10, 13, 0, 5, "~")
# small pond on the west bank
fill(water, 3, 6, 25, 27, "~")

# ---------------------------------------------------------------- paths
fill(path, 21, 22, 0, 19, "p")          # main south road to the forest exit
fill(path, 20, 25, 0, 2, "p")           # widened exit mouth
fill(path, 16, 20, 15, 16, "p")         # west branch to the bridge
fill(path, 3, 11, 15, 16, "p")          # west bank path
fill(path, 4, 8, 12, 14, "p")           # archer's clearing
fill(path, 23, 31, 11, 12, "p")         # east branch to the training ground
fill(path, 31, 43, 8, 16, "p")          # training ground sand pad
fill(path, 23, 42, 22, 22, "p")         # east road to the star plate
fill(path, 40, 44, 21, 23, "p")
fill(path, 17, 20, 9, 10, "p")          # priest's shrine nook
BRIDGE = (12, 15, 15, 16)               # x0, x1, y0, y1: bridge deck cells (walkable over water)

# ---------------------------------------------------------------- collision
for y in range(H):
    for x in range(W):
        if water[rc(y)][x] == "~" or cliff[rc(y)][x] in "RFW":
            setc(coll, x, y, "#")
        if x in (0, W - 1) or y in (0, H - 1):
            if not (y == 0 and 20 <= x <= 25):   # forest exit stays open
                setc(coll, x, y, "#")
fill(coll, BRIDGE[0], BRIDGE[1], BRIDGE[2], BRIDGE[3], ".")

# ---------------------------------------------------------------- props
TRUNK = [0.55, 0.3, 0, 0.15]
OAK = [0.6, 0.3, 0, 0.15]


def prop(sprite, x, y, box=None, fade=False, **extra):
    d = {"sprite": sprite, "x": round(x, 3), "y": round(y, 3), "fade": fade}
    if box:
        d["box"] = box
    d.update(extra)
    return d


def walkable_cell(x, y):
    xi, yi = int(x), int(y)
    return get(path, xi, yi) != "p" and get(water, xi, yi) != "~" and get(cliff, xi, yi) == "."


props = []
for i, p in enumerate(north["props"]):
    q = dict(p)
    q["x"] = round(p["x"] + DX, 3)
    q["y"] = round(p["y"] + DY, 3)
    # Phase-0 extension props (index 17+) that now sit on a new path/river are dropped
    if i >= 17 and not walkable_cell(q["x"], q["y"]):
        continue
    props.append(q)

sign = next(p for p in props if p["sprite"] == "PROP_sign_draft")

# border forest (natural map edge); skip paths, water, cliffs
border = []
for x in range(1, W - 1, 3):
    border += [(x + 0.5, 1.2), (x + 1.5, 38.5) if x > 36 else None]
for y in range(2, 34, 3):
    border += [(1.5, y + 0.4), (W - 2.0, y + 0.6)]
for b in border:
    if b is None:
        continue
    x, y = b
    if 18.5 < x < 27 and y < 4:       # exit mouth
        continue
    if not walkable_cell(x, y) or not walkable_cell(x, y + 0.6):
        continue
    hsh = (int(x * 10) * 73856093 ^ int(y * 10) * 19349663) & 0xFFFF
    kind = ["PROP_tree_green_m_draft", "PROP_tree_green_b_draft", "PROP_tree_green_s_draft", "PROP_tree_green_m_draft",
            "PROP_tree_green_b_draft", "PROP_tree_green_s_draft", "PROP_tree_blossom_draft"][hsh % 7]
    props.append(prop(kind, x, y, [0.7, 0.35, 0, 0.18] if "blossom" in kind else TRUNK, True))

extra = [
    # west bank
    prop("PROP_tree_blossom_draft", 2.6, 22.6, [0.7, 0.35, 0, 0.18], True),
    prop("PROP_tree_green_b_draft", 7.8, 21.5, TRUNK, True),
    prop("PROP_rock_l_draft", 6.4, 19.6, [0.9, 0.5, 0, 0.25]),   # clear of 貝克 (villager_08)
    prop("PROP_flowerbush_draft", 2.8, 13.2), prop("PROP_flowerbush_draft", 9.2, 12.6),
    prop("PROP_tree_green_m_draft", 3.0, 8.4, TRUNK, True),
    prop("PROP_tree_green_m_draft", 7.4, 6.6, TRUNK, True),
    prop("PROP_lantern_post_draft", 10.6, 17.3, [0.25, 0.2, 0, 0.1]),
    prop("PROP_lantern_post_draft", 16.6, 17.3, [0.25, 0.2, 0, 0.1]),
    # south road
    prop("PROP_tree_blossom_draft", 17.2, 5.6, [0.7, 0.35, 0, 0.18], True),
    prop("PROP_tree_green_b_draft", 26.4, 5.2, TRUNK, True),
    prop("PROP_bush_draft", 24.6, 8.4), prop("PROP_bush_draft", 18.2, 13.2),
    prop("PROP_lantern_post_draft", 23.6, 3.4, [0.25, 0.2, 0, 0.1]), prop("PROP_lantern_post_draft", 19.4, 3.4, [0.25, 0.2, 0, 0.1]),
    prop("PROP_flowerbush_draft", 16.6, 8.2), prop("PROP_flowerbush_draft", 19.2, 11.8),
    prop("PROP_starplate_draft", 18.6, 9.9),   # priest's small shrine plate (decor)
    # training ground (fence ring + crates)
    prop("PROP_fence_long_draft", 34.0, 17.2, [2.9, 0.3, 0, 0.15]),
    prop("PROP_fence_long_draft", 40.6, 17.2, [2.9, 0.3, 0, 0.15]),
    prop("PROP_fence_mid_draft", 33.2, 7.6, [2.1, 0.3, 0, 0.15]),
    prop("PROP_fence_mid_draft", 41.4, 7.6, [2.1, 0.3, 0, 0.15]),
    prop("PROP_crate_draft", 43.4, 15.6, [0.6, 0.4, 0, 0.2]), prop("PROP_crate_draft", 44.2, 15.0, [0.6, 0.4, 0, 0.2]),
    prop("PROP_archery_target_draft", 33.4, 9.4, [0.7, 0.3, 0, 0.15]),
    prop("PROP_archery_target_draft", 41.6, 9.4, [0.7, 0.3, 0, 0.15]),
    prop("PROP_lantern_post_draft", 30.6, 13.6, [0.25, 0.2, 0, 0.1]),
    # east side
    prop("PROP_tree_blossom_draft", 44.4, 27.6, [0.7, 0.35, 0, 0.18], True),
    prop("PROP_tree_green_m_draft", 40.0, 29.0, TRUNK, True),
    prop("PROP_tree_green_b_draft", 45.0, 33.6, TRUNK, True),
    prop("PROP_tree_green_s_draft", 41.6, 35.4, TRUNK, True),
    prop("PROP_rock_m_draft", 38.6, 26.2, [0.7, 0.4, 0, 0.2]),
    prop("PROP_flowerbush_draft", 39.4, 20.0), prop("PROP_flowerbush_draft", 45.0, 20.6),
    prop("PROP_tree_green_s_draft", 28.0, 18.4, TRUNK, True),
    prop("PROP_tree_green_m_draft", 35.2, 20.0, TRUNK, True),
    prop("PROP_bush_draft", 29.4, 15.8),
    # west plateau top decoration
    prop("PROP_tree_green_m_draft", 4.0, 36.4, TRUNK, True),
    prop("PROP_tree_blossom_draft", 7.4, 37.2, [0.7, 0.35, 0, 0.18], True),
]
props += [p for p in extra if walkable_cell(p["x"], p["y"])]

# ---------------------------------------------------------------- witch-village dressing (tools/art/village_decor.py)
NPC_HOMES = [(22.2, 35.4), (37.0, 15.6), (18.6, 10.6), (6.2, 13.6), (33.2, 15.4), (34.6, 36.0), (32.6, 33.4), (24.4, 19.0),
             (34.6, 23.0), (38.6, 34.4), (33.2, 27.0), (8.0, 22.6), (8.8, 18.4), (24.8, 6.2), (41.6, 9.6), (19.6, 6.8),
             (28.6, 4.8), (42.5, 22.6), (34.6, 12.6), (37.0, 11.6), (39.4, 12.6), (37.0, 9.2)]
SPAWN_CLEAR = [(22.5, 3.2)] + [(s["x"] + DX, s["y"] + DY) for s in north["spawns"]]


def free(x, y, r=1.0, allow_path=False):
    """Grass cell (or path when allowed), clear of solid props, NPC stand points and spawns."""
    xi, yi = int(x), int(y)
    if get(water, xi, yi) == "~" or get(cliff, xi, yi) != "." or get(coll, xi, yi) == "#":
        return False
    if not allow_path and get(path, xi, yi) == "p":
        return False
    for p in props:
        if p.get("layer"):
            continue
        rr = r + (1.4 if "tree" in p["sprite"] else 3.0 if "cottage" in p["sprite"] else 1.2 if "stall" in p["sprite"] else 0.4)
        if (p["x"] - x) ** 2 + (p["y"] - y) ** 2 < rr * rr:
            return False
    return all((a - x) ** 2 + (b - y) ** 2 >= 1.1 ** 2 for a, b in NPC_HOMES + SPAWN_CLEAR)


import random

WITCH_PROPS = [   # sprite, collider, candidate spots (first free one wins)
    # the witch's yard east of the stall (the cottage front itself is full: path, fences, rocks)
    ("PROP_cauldron_draft", [0.6, 0.3, 0, 0.12], [(38.4, 36.0)]),
    ("PROP_herb_rack_draft", [0.8, 0.25, 0, 0.1], [(44.8, 36.0)]),
    ("PROP_broom_draft", None, [(39.6, 35.6)]),
    ("PROP_potion_crate_draft", [0.6, 0.3, 0, 0.12], [(33.5, 34.5)]),
    ("PROP_candles_draft", None, [(40.6, 36.4)]),
    ("PROP_pumpkin_draft", [0.45, 0.25, 0, 0.1], [(35.6, 31.4), (36.4, 30.2)]),
    ("PROP_pumpkin_violet_draft", [0.45, 0.25, 0, 0.1], [(36.9, 31.6), (37.6, 30.4)]),
    ("PROP_pumpkin_draft", [0.45, 0.25, 0, 0.1], [(37.8, 32.4), (38.6, 31.0)]),
    ("PROP_pumpkin_violet_draft", [0.45, 0.25, 0, 0.1], [(33.0, 30.0)]),
    ("PROP_pumpkin_draft", [0.45, 0.25, 0, 0.1], [(9.8, 10.4), (2.6, 16.6)]),
    ("PROP_crystal_cluster_draft", [0.4, 0.25, 0, 0.1], [(40.6, 24.4), (40.4, 20.4)]),
    ("PROP_crystal_cluster_draft", [0.4, 0.25, 0, 0.1], [(44.6, 24.6), (45.2, 21.0)]),
    ("PROP_crystal_cluster_draft", [0.4, 0.25, 0, 0.1], [(31.8, 30.6), (28.4, 31.0)]),
    ("PROP_crystal_cluster_draft", [0.4, 0.25, 0, 0.1], [(16.6, 11.6), (16.4, 9.0)]),
    ("PROP_candles_draft", None, [(20.2, 10.4), (17.2, 10.2)]),
    ("PROP_lantern_post_draft", [0.25, 0.2, 0, 0.1], [(20.4, 27.4), (20.3, 26.0)]),
    ("PROP_lantern_post_draft", [0.25, 0.2, 0, 0.1], [(23.7, 21.2), (23.8, 23.8)]),
    ("PROP_lantern_post_draft", [0.25, 0.2, 0, 0.1], [(40.4, 21.0), (39.8, 24.0)]),
    ("PROP_lantern_post_draft", [0.25, 0.2, 0, 0.1], [(26.2, 23.6), (28.0, 23.6)]),
]
def nearby(spots, step=0.4, rings=6):
    """The listed spots, then rings around the first one (deterministic search order)."""
    yield from spots
    x0, y0 = spots[0]
    for k in range(1, rings + 1):
        for i in range(8 * k):
            a = 2 * math.pi * i / (8 * k)
            yield x0 + math.cos(a) * k * step, y0 + math.sin(a) * k * step


for sprite, box, spots in WITCH_PROPS:
    allow = "candles" in sprite
    spot = next(((round(x, 2), round(y, 2)) for x, y in nearby(spots) if free(x, y, 0.7, allow)), None)
    if spot is None:
        print("no free spot for", sprite, spots)
        continue
    props.append(prop(sprite, spot[0], spot[1], box))


# second, staggered row of the border forest + small meadow props in the open fields
inner = [(3.2, y + 1.9) for y in range(2, 32, 3)] + [(W - 3.6, y + 2.0) for y in range(2, 32, 3)] + \
        [(x + 2.0, 2.8) for x in range(1, W - 3, 3)]
for x, y in inner:
    if 18 < x < 27 and y < 5:
        continue
    if free(x, y, 0.9):
        hsh = (int(x * 10) * 83492791 ^ int(y * 10) * 2971215073) & 0xFFFF
        kind = ["PROP_tree_green_s_draft", "PROP_tree_green_m_draft", "PROP_tree_green_b_draft", "PROP_tree_green_s_draft",
                "PROP_tree_blossom_draft"][hsh % 5]
        props.append(prop(kind, x, y, [0.7, 0.35, 0, 0.18] if "blossom" in kind else TRUNK, True))
RM = random.Random(7)
for gy in range(4, H - 4, 4):
    for gx in range(4, W - 4, 4):
        x, y = gx + RM.uniform(0, 3), gy + RM.uniform(0, 3)
        if RM.random() < 0.45 and free(x, y, 1.6):
            sprite, box = RM.choice([("PROP_flowerbush_draft", None), ("PROP_rock_m_draft", [0.7, 0.4, 0, 0.2]), ("PROP_rock_s_draft", [0.5, 0.3, 0, 0.15]),
                                     ("PROP_flowerbush_draft", None), ("PROP_mushroom_glow_draft", None), ("PROP_stump_draft", [0.9, 0.45, 0, 0.22])])
            props.append(prop(sprite, round(x, 2), round(y, 2), box))


def decal(sprite, x, y, order):
    return {"sprite": sprite, "x": round(x, 3), "y": round(y, 3), "fade": False, "layer": "GroundDecal", "order": order}


# Ground decals: deterministic jittered grid. Large light / dark patches break the 32 px tile grid;
# tufts, flowers and clover cluster by a coarse hash so meadows read as meadows, not noise.
import random
R = random.Random(20261121)
decals = []


def cluster(x, y, salt):
    v = (int(x / 5) * 92821 ^ int(y / 5) * 68917 ^ salt * 1234567) & 0xFFFF
    return v / 65535.0


for gy in range(1, H - 1):
    for gx in range(1, W - 1):
        x, y = gx + R.uniform(0.1, 0.9), gy + R.uniform(0.1, 0.9)
        roll = R.random()
        if not free(x, y, 0.35):
            continue
        if gx % 3 == 0 and gy % 3 == 1 and roll < 0.55:
            decals.append(decal(R.choice(["DEC_patch_light_draft", "DEC_patch_light_s_draft", "DEC_patch_dark_draft"]), x, y, 0))
            continue
        meadow = cluster(x, y, 1)
        if roll < 0.16 + 0.22 * meadow:
            decals.append(decal("DEC_tuft_%d_draft" % R.randrange(5), x, y, 2))
        elif roll < 0.20 + 0.30 * meadow and meadow > 0.45:
            colour = ["pink", "lilac", "white", "yellow", "blue"][int(cluster(x, y, 2) * 5) % 5]
            decals.append(decal("DEC_flowers_%s_draft" % colour, x, y, 2))
        elif roll < 0.25 + 0.30 * meadow:
            decals.append(decal(R.choice(["DEC_clover_draft", "DEC_pebbles_0_draft", "DEC_pebbles_1_draft"]), x, y, 1))
# mushrooms at tree feet, petals under the blossom trees
for p in list(props):
    if "tree_green" in p["sprite"] and R.random() < 0.45:
        x, y = p["x"] + R.choice([-0.9, 0.9]), p["y"] - 0.2
        if free(x, y, 0.2):
            decals.append(decal(R.choice(["DEC_shrooms_red_draft", "DEC_shrooms_violet_draft"]), x, y, 2))
    if "tree_blossom" in p["sprite"]:
        for dx, dy in ((-0.9, -0.3), (0.8, -0.5), (0.0, -0.9)):
            if free(p["x"] + dx, p["y"] + dy, 0.0):
                decals.append(decal("DEC_petals_draft", p["x"] + dx, p["y"] + dy, 1))
# lily pads on open water (all 4 neighbours water, not under the bridge)
for y in range(1, H - 1):
    for x in range(1, W - 1):
        if all(get(water, x + dx, y + dy) == "~" for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1))) \
                and not (BRIDGE[0] - 1 <= x <= BRIDGE[1] + 1 and BRIDGE[2] - 1 <= y <= BRIDGE[3] + 1) and R.random() < 0.12:
            decals.append(decal("DEC_lily_%d_draft" % (R.random() < 0.35), x + R.uniform(0.2, 0.8), y + R.uniform(0.2, 0.8), 3))
props += decals

# ---------------------------------------------------------------- objects (NPCs, dummies, exits)
objects = [
    {"kind": "bridge", "sprite": "PROP_bridge_wood_draft", "x": 13.5 + 0.5, "y": 16.0,
     "rails": [[14.0, 16.92, 6.0, 0.16], [14.0, 15.08, 6.0, 0.16]]},
    {"kind": "npc", "id": "ella", "sheet": "NPC_ella_draft", "name": "艾拉", "x": 22.2, "y": 35.4, "facing": "down",
     "lines": ["露米，魔典的書頁在發光……這不是好兆頭。",
               "先去東南邊的訓練場找凱恩，練習法杖連擊（左鍵）、蓄力魔彈（按住左鍵或右鍵）與閃避（Space）。",
               "準備好了就從南邊小路進入月影森林。森林深處的月蝕菇王就是異常的源頭。",
               "小屋裡的魔典台可以更換 Q / E 法術；研究桌能用森林帶回的材料做永久研究。"],
     "after": ["你真的打倒了菇王！魔典殘頁上的星痕又亮了一些。",
               "用帶回的星砂到研究桌解鎖新書頁吧，下一次可以嘗試不同的構築。"],
     "quest": "talked_ella"},
    {"kind": "npc", "id": "kain", "sheet": "NPC_knight_draft", "name": "凱恩（騎士）", "x": 37.0, "y": 15.6, "facing": "down",
     "lines": ["來得正好，魔女。木偶不會倒，盡情打吧。",
               "左鍵短按是三段連擊：平掃、回身敲擊、魔力震擊。第三段能擊退敵人並累積共鳴。",
               "按住左鍵（或右鍵）蓄力，放開射出魔彈，蓄越久越強。",
               "Space 閃避有無敵時間；在敵人攻擊打中前閃開，下一發魔彈會變強。",
               "試試火（Q）＋風（E）：風把燃燒的敵人捲起來，就是火焰龍捲！"],
     "quest": "talked_kain"},
    {"kind": "npc", "id": "lia", "sheet": "NPC_priest_draft", "name": "莉亞（祭司）", "x": 18.6, "y": 10.6, "facing": "right",
     "lines": ["願星光守護你。讓我為你治療。", "（HP 與 MP 已完全回復）",
               "森林裡元素相遇會產生反應：火與冰是蒸汽爆裂、火與雷是超載爆燃、冰與雷是碎晶雷暴。"],
     "heals": True},
    {"kind": "npc", "id": "lorne", "sheet": "NPC_archer_draft", "name": "洛恩（弓箭手）", "x": 6.2, "y": 13.6, "facing": "right",
     "lines": ["森林的路我最熟。入口有史萊姆，蘑菇林道的毒孢菇會從遠處吐孢子。",
               "星痕祭壇按 F 會喚來怪潮，撐過去有書頁獎勵。",
               "斷裂石橋旁有拉桿可以修橋；橋邊的藤蔓用火燒掉能走捷徑。",
               "樹根守衛衝鋒前地上會出現紅色警示，往側邊閃！"]},
    {"kind": "npc", "id": "prince", "sheet": "NPC_prince_ref", "name": "王子", "x": 33.2, "y": 15.4, "facing": "right",
     "lines": ["你就是星露村的魔女吧？我奉父王之命，來調查月影森林的異變。",
               "凱恩是我的劍術老師。他說魔女的法杖連擊，可不比騎士的劍慢。",
               "你進森林的時候，我會守住村子。等你帶著好消息回來。"],
     "after": ["月蝕菇王被打倒了？王國欠你一份大恩，魔女。",
               "下次再進森林，換個魔典構築試試吧。我會在這裡等你的故事。"]},
    {"kind": "dummy", "x": 34.6, "y": 12.6}, {"kind": "dummy", "x": 37.0, "y": 11.6}, {"kind": "dummy", "x": 39.4, "y": 12.6},
    {"kind": "dummy", "x": 37.0, "y": 9.2},
    {"kind": "sign", "x": sign["x"], "y": sign["y"], "name": "路牌",
     "lines": ["↑ 魔女小屋　→ 傳送星盤　↓ 星露村南路・月影森林　← 溪畔小橋"]},
    {"kind": "sign_post", "sprite": "PROP_sign_draft", "x": 24.2, "y": 4.0, "name": "森林告示牌",
     "lines": ["前方：月影森林。", "目前異常：怪物增加、蘑菇毒化。請攜帶藥水（1 / 2 鍵使用）。"]},
    {"kind": "starplate", "x": 42.5, "y": 22.6, "target": "spawn_from_workshop"},
] + [
    # ambient villagers (NPC_villager_XX_ref, palette variants of the chibi villager sheet); radius > 0 = strolls
    {"kind": "npc", "id": "villager_" + v[0], "sheet": "NPC_villager_" + v[0] + "_ref", "name": v[1], "x": v[2], "y": v[3],
     "facing": v[4], "radius": v[5], "lines": v[6]}
    for v in [
        ("01", "湯姆（果農）", 34.6, 36.0, "down", 1.4, ["今年的星莓長得特別好。", "可是森林那邊的蘑菇越來越怪，連鳥都不敢靠近了。"]),
        ("02", "馬可（鐵匠學徒）", 32.6, 33.4, "left", 1.4, ["水晶噴泉的水最適合淬火。", "師傅說法杖再好，也要會閃避才活得久。"]),
        ("03", "小麥（農夫）", 24.4, 19.0, "down", 1.4, ["戴草帽可不是為了好看，是太陽真的很大。", "往東走到底就是傳送星盤，能直接回小屋門口。"]),
        ("04", "蘿絲（麵包店）", 34.6, 23.0, "down", 2.0, ["剛出爐的星砂麵包！……啊，你在冒險，等回來再吃吧。"]),
        ("05", "老約翰", 38.6, 34.4, "left", 1.2, ["我年輕時也見過一次月蝕。那年森林裡的菇也是這樣發著紫光。",
                                                "孩子，記得：火和風碰在一起，會捲起火焰龍捲。"]),
        ("06", "卡洛（商隊）", 33.2, 27.0, "right", 2.0, ["商隊這陣子不敢走森林道路，貨都卡在村裡了。"]),
        ("07", "蜜拉（花店）", 8.0, 22.6, "right", 1.8, ["池塘邊的花開了，你要不要帶一朵進森林？", "聽說星痕祭壇附近的花會在夜裡發光。"]),
        ("08", "貝克（漁夫）", 8.8, 18.4, "down", 1.6, ["今天的魚都躲到橋下去了。", "洛恩說森林裡有會吐孢子的蘑菇，離遠一點打比較安全。"]),
        ("09", "諾亞（牧人）", 24.8, 6.2, "left", 1.8, ["羊群最近不肯往南走，牠們一定也感覺到森林不對勁。"]),
        ("10", "提姆（見習騎士）", 41.6, 9.6, "left", 1.6, ["我也想跟凱恩老師一樣厲害！", "木偶打不倒的，你可以放心試新的法術組合。"]),
        ("11", "葛蕾絲婆婆", 19.6, 6.8, "right", 1.2, ["莉亞那孩子的治療很靈驗，受傷了就去找她吧。"]),
        ("12", "歐文（信差）", 28.6, 4.8, "up", 2.4, ["王子殿下也來到村子了，就在訓練場那邊。", "我得趕快把信送出去……可是森林的路被封了。"]),
    ]
] + [
    {"kind": "exit", "rect": [20.0, -1.0, 6.0, 2.2], "scene": "Forest", "spawn": "spawn_from_village", "label": "前往月影森林"},
]

spawns = [{"id": s["id"], "x": round(s["x"] + DX, 3), "y": round(s["y"] + DY, 3)} for s in north["spawns"]]
spawns.append({"id": "spawn_from_forest", "x": 22.5, "y": 3.2})

zones = [
    {"id": "cottage", "name": "魔女小屋", "rect": [16, 32, 20, 8]},
    {"id": "plateau", "name": "西側高台", "rect": [0, 32, 16, 8]},
    {"id": "bridge", "name": "溪畔小橋", "rect": [0, 0, 18, 32]},
    {"id": "training", "name": "騎士訓練場", "rect": [29, 6, 19, 13]},
    {"id": "exit", "name": "森林出口", "rect": [18, 0, 11, 6]},
    {"id": "square", "name": "星露村・中央廣場", "rect": [0, 0, 48, 40]},
]

# props that the taller cliff faces now cover step down off the face
for p in props:
    while get(cliff, int(p["x"]), int(p["y"])) in "FW":
        p["y"] = round(p["y"] - 1.0, 3)

data = {"id": "village", "width": W, "height": H, "status": "draft layout (north block from reference A + extension)",
        "biome": "village",
        "layers": {k: ["".join(r) for r in g] for k, g in (("water", water), ("path", path), ("cliff", cliff), ("collision", coll))},
        "props": props, "objects": objects, "spawns": spawns, "zones": zones,
        "scene": {"title": "星露村", "dim": 0.0, "safe": True, "objective": "去訓練場練習，再從南路前往森林"}}
out = os.path.join(ROOT, "Assets/_Game/Data/Maps/village.json")
json.dump(data, open(out, "w"), indent=1, ensure_ascii=False)
merged = [[(cliff[r][x] if cliff[r][x] != "." else water[r][x] if water[r][x] != "." else path[r][x]) for x in range(W)] for r in range(H)]
print("\n".join("%2d %s" % (H - 1 - r, "".join(row)) for r, row in enumerate(merged)))
print("props", len(props), "(decals %d)" % len(decals), "objects", len(objects))
