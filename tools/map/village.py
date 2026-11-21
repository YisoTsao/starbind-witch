"""Full Starlumen Village (星露村) 48 x 40 layout (WORLD_STRUCTURE.md §2).

Embeds the Phase-0 north block (village_north.json, 28 x 20, made from reference A) at offset
(DX, DY) = (10, 20) and extends it: west plateau + river flowing south, wooden bridge with the archer
on the west bank, the priest on the south road, the knight's training ground (dummies, targets) in the
south-east, a star plate to the east and the forest exit at the south edge.
Writes Assets/_Game/Data/Maps/village.json (read by Starbind.Editor.GameContentBuilder).
Usage: python3 tools/map/village.py <repo_root>
"""
import json, os, sys

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
    kind = ["PROP_village_round_draft", "PROP_tree_green_b_draft", "PROP_tree_green_m_draft"][int(x * 7 + y * 3) % 3]
    props.append(prop(kind, x, y, OAK if "round" in kind else TRUNK, True))

extra = [
    # west bank
    prop("PROP_tree_blossom_draft", 2.6, 22.6, [0.7, 0.35, 0, 0.18], True),
    prop("PROP_village_round_draft", 7.8, 21.5, OAK, True),
    prop("PROP_rock_l_draft", 8.6, 18.4, [0.9, 0.5, 0, 0.25]),
    prop("PROP_flowerbush_draft", 2.8, 13.2), prop("PROP_flowerbush_draft", 9.2, 12.6),
    prop("PROP_village_round_draft", 3.0, 8.4, OAK, True),
    prop("PROP_tree_green_m_draft", 7.4, 6.6, TRUNK, True),
    prop("PROP_lamp_draft", 10.6, 17.3, [0.25, 0.2, 0, 0.1]),
    prop("PROP_lamp_draft", 16.6, 17.3, [0.25, 0.2, 0, 0.1]),
    # south road
    prop("PROP_tree_blossom_draft", 17.2, 5.6, [0.7, 0.35, 0, 0.18], True),
    prop("PROP_village_round_draft", 26.4, 5.2, OAK, True),
    prop("PROP_bush_draft", 24.6, 8.4), prop("PROP_bush_draft", 18.2, 13.2),
    prop("PROP_lamp_draft", 23.6, 3.4, [0.25, 0.2, 0, 0.1]), prop("PROP_lamp_draft", 19.4, 3.4, [0.25, 0.2, 0, 0.1]),
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
    prop("PROP_lamp_draft", 30.6, 13.6, [0.25, 0.2, 0, 0.1]),
    # east side
    prop("PROP_tree_blossom_draft", 44.4, 27.6, [0.7, 0.35, 0, 0.18], True),
    prop("PROP_village_round_draft", 40.0, 29.0, OAK, True),
    prop("PROP_tree_green_b_draft", 45.0, 33.6, TRUNK, True),
    prop("PROP_village_round_draft", 41.6, 35.4, OAK, True),
    prop("PROP_rock_m_draft", 38.6, 26.2, [0.7, 0.4, 0, 0.2]),
    prop("PROP_flowerbush_draft", 39.4, 20.0), prop("PROP_flowerbush_draft", 45.0, 20.6),
    prop("PROP_tree_green_s_draft", 28.0, 18.4, TRUNK, True),
    prop("PROP_tree_green_m_draft", 35.2, 20.0, TRUNK, True),
    prop("PROP_bush_draft", 29.4, 15.8),
    # west plateau top decoration
    prop("PROP_village_round_draft", 4.0, 36.4, OAK, True),
    prop("PROP_tree_blossom_draft", 7.4, 37.2, [0.7, 0.35, 0, 0.18], True),
]
props += [p for p in extra if walkable_cell(p["x"], p["y"])]

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
    {"kind": "dummy", "x": 34.6, "y": 12.6}, {"kind": "dummy", "x": 37.0, "y": 11.6}, {"kind": "dummy", "x": 39.4, "y": 12.6},
    {"kind": "dummy", "x": 37.0, "y": 9.2},
    {"kind": "sign", "x": sign["x"], "y": sign["y"], "name": "路牌",
     "lines": ["↑ 魔女小屋　→ 傳送星盤　↓ 星露村南路・月影森林　← 溪畔小橋"]},
    {"kind": "sign_post", "sprite": "PROP_sign_draft", "x": 24.2, "y": 4.0, "name": "森林告示牌",
     "lines": ["前方：月影森林。", "目前異常：怪物增加、蘑菇毒化。請攜帶藥水（1 / 2 鍵使用）。"]},
    {"kind": "starplate", "x": 42.5, "y": 22.6, "target": "spawn_from_workshop"},
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

data = {"id": "village", "width": W, "height": H, "status": "draft layout (north block from reference A + extension)",
        "biome": "village",
        "layers": {k: ["".join(r) for r in g] for k, g in (("water", water), ("path", path), ("cliff", cliff), ("collision", coll))},
        "props": props, "objects": objects, "spawns": spawns, "zones": zones,
        "scene": {"title": "星露村", "dim": 0.0, "safe": True, "objective": "去訓練場練習，然後從南路前往月影森林"}}
out = os.path.join(ROOT, "Assets/_Game/Data/Maps/village.json")
json.dump(data, open(out, "w"), indent=1, ensure_ascii=False)
merged = [[(cliff[r][x] if cliff[r][x] != "." else water[r][x] if water[r][x] != "." else path[r][x]) for x in range(W)] for r in range(H)]
print("\n".join("%2d %s" % (H - 1 - r, "".join(row)) for r, row in enumerate(merged)))
print("props", len(props), "objects", len(objects))
