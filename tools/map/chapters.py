"""Chapters 2-5 maps (STORY_WORLD_QUESTS §3.6 / §4.4, CHAPTER_PLAN): 星核礦坑 64x80, 星輝湖 80x64,
失落觀星台 64x96, 黑月城塞 96x96.

Each map follows its approved L1 graybox (tools/map/chapter_grayboxes.py): the same six rooms at the
same anchors joined by the same edges, carved organically from solid ground like the forest.
Room 1 (index 0) is the entrance at the south, room 5 (index 4) the boss arena, room 6 (index 5) the
side-quest dead end. Mechanics sit on the routes so the critical path always has a solution:
  mine         mine cart blasts the rubble between 礦車爆破點 and 晶礦大廳; two conductive crystals
               open the barrier before 核心礦室; a lever repairs the chasm bridge (loop shortcut).
  lake         islands in a lake; the west and east light bridges to 水晶遺跡 each need their island's
               two pylons lit (either side is enough).
  observatory  refraction puzzle in 折射鏡台: flip two mirrors so the beam reaches the receiver and the
               star door on the snow road to 觀星儀廣場 opens.
  citadel      three tower trials (自然 / 月影 / 星痕); all three open both doors of 中央大廳.
Layers: water ('~' hazard: chasm / lake / ice / void), path ('p'), solid ('S' impassable top),
collision ('#'), spawn ('s' spawnable, 'w' walkable only, '.' blocked). Decor is seeded.
Writes Assets/_Game/Data/Maps/<id>.json. Usage: python3 tools/map/chapters.py <repo_root>
"""
import json, math, os, sys
import numpy as np

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
OUT = os.path.join(ROOT, "Assets/_Game/Data/Maps")


class Map:
    def __init__(self, ident, w, h, seed):
        self.id, self.W, self.H = ident, w, h
        self.rng = np.random.default_rng(seed)
        self.seed = seed
        self.open = np.zeros((h, w), bool)
        self.path = np.zeros((h, w), bool)
        self.water = np.zeros((h, w), bool)
        self.bridge = np.zeros((h, w), bool)    # walkable over water (gated bridges / causeways)
        self.nospawn = np.zeros((h, w), bool)
        self.props, self.objects, self.spawns, self.zones = [], [], [], []
        self.ys, self.xs = np.mgrid[0:h, 0:w]

    # ---------------------------------------------------------------- carving
    def ell(self, cx, cy, rx, ry, m, val=True):
        m[(((self.xs + 0.5 - cx) / rx) ** 2 + ((self.ys + 0.5 - cy) / ry) ** 2) <= 1] = val

    def box(self, x0, x1, y0, y1, m, val=True):
        m[max(0, y0):min(self.H, y1 + 1), max(0, x0):min(self.W, x1 + 1)] = val

    def stroke(self, pts, width, m, wobble=0.0):
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            n = int(max(abs(x1 - x0), abs(y1 - y0)) * 3) + 1
            for i in range(n + 1):
                t = i / n
                wv = math.sin(t * math.pi * 2 + x0 * 0.3) * wobble
                self.ell(x0 + (x1 - x0) * t + wv, y0 + (y1 - y0) * t, width / 2, width / 2, m)

    @property
    def walk(self):
        return (self.open & ~self.water) | self.bridge

    # ---------------------------------------------------------------- decor
    def edge_props(self, sprites, step=(1.8, 1.7), near=3.0, keep_far=0.45, fade=True, box=None):
        walk = self.walk
        for gy in np.arange(0.6, self.H, step[1]):
            for gx in np.arange(0.5, self.W, step[0]):
                x = gx + self.rng.uniform(-0.5, 0.5) + (0.9 if int(gy / step[1]) % 2 else 0)
                y = gy + self.rng.uniform(-0.3, 0.3)
                xi, yi = int(x), int(y)
                if not (0 <= xi < self.W and 0 <= yi < self.H) or walk[yi, xi] or self.water[yi, xi]:
                    continue
                x0, x1, y0, y1 = max(0, int(x - near)), min(self.W, int(x + near) + 1), max(0, int(y - near)), min(self.H, int(y + near) + 1)
                if not walk[y0:y1, x0:x1].any():
                    if self.rng.random() < keep_far:
                        continue
                    continue
                name = sprites[int(self.rng.integers(0, len(sprites)))]
                p = {"sprite": name, "x": round(float(x), 2), "y": round(float(y), 2), "fade": fade}
                if box:
                    p["box"] = box
                self.props.append(p)

    def scatter(self, choices, n, avoid=()):
        walk = self.walk
        for _ in range(n):
            x, y = self.rng.uniform(2, self.W - 2), self.rng.uniform(2, self.H - 2)
            xi, yi = int(x), int(y)
            if not walk[yi, xi] or self.path[yi, xi] or self.bridge[yi, xi] or self.nospawn[yi, xi]:
                continue
            if any(math.hypot(x - ax, y - ay) < r for ax, ay, r in avoid):
                continue
            k = self.rng.random()
            acc = 0
            for sprite, w, box in choices:
                acc += w
                if k < acc:
                    p = {"sprite": sprite, "x": round(x, 2), "y": round(y, 2), "fade": False}
                    if box:
                        p["box"] = box
                    self.props.append(p)
                    break

    # ---------------------------------------------------------------- output
    def rows(self, fn):
        return ["".join(fn(x, self.H - 1 - r) for x in range(self.W)) for r in range(self.H)]

    def write(self, biome, title, objective, dim, mouth):
        walk = self.walk
        blocked = ~walk
        mx0, mx1 = mouth
        def coll(x, y):
            if self.bridge[y, x]:
                return "."
            if blocked[y, x] or x in (0, self.W - 1) or y == self.H - 1 or (y == 0 and not mx0 <= x <= mx1):
                return "#"
            return "."
        solid = blocked & ~self.water
        layers = {
            "water": self.rows(lambda x, y: "~" if self.water[y, x] else "."),
            "path": self.rows(lambda x, y: "p" if self.path[y, x] and walk[y, x] else "."),
            "solid": self.rows(lambda x, y: "S" if solid[y, x] else "."),
            "collision": self.rows(coll),
            "spawn": self.rows(lambda x, y: "." if blocked[y, x] else ("w" if self.nospawn[y, x] or self.bridge[y, x] else "s")),
        }
        data = {"id": self.id, "width": self.W, "height": self.H, "seed": self.seed, "biome": biome,
                "layers": layers, "props": self.props, "objects": self.objects, "spawns": self.spawns, "zones": self.zones,
                "scene": {"title": title, "dim": dim, "safe": False, "objective": objective, "chapter": self.id}}
        os.makedirs(OUT, exist_ok=True)
        json.dump(data, open(os.path.join(OUT, self.id + ".json"), "w"), indent=1, ensure_ascii=False)
        spawnable = int((walk & ~self.nospawn & ~self.bridge).sum())
        print(f"{self.id}: {self.W}x{self.H}, walkable {int(walk.sum())}, spawnable {spawnable}, props {len(self.props)}, objects {len(self.objects)}")


def zone(m, ident, name, cx, cy, rx, ry, nospawn=False):
    m.zones.append({"id": ident, "name": name, "rect": [cx - rx, cy - ry, rx * 2, ry * 2], "noSpawn": nospawn})


def arena(m, cx, cy, r, gates, gate_sprite, pillar):
    m.ell(cx, cy, r, r * 0.85, m.open)
    m.ell(cx, cy, r + 1.2, r * 0.85 + 1.2, m.nospawn)
    for k in range(8):
        a = k * math.pi / 4 + math.pi / 8
        m.props.append({"sprite": pillar, "x": round(cx + math.cos(a) * (r + 0.6), 2), "y": round(cy + math.sin(a) * (r * 0.85 + 0.4), 2), "fade": True})
    return {"kind": "boss_arena", "trigger": [cx - r + 1, cy - r * 0.85 + 1, 2 * r - 2, 1.7 * r - 2], "bossSpawn": [cx, cy + 1],
            "gates": gates, "sprite": gate_sprite}


# =================================================================== 星核礦坑 64x80
def mine():
    m = Map("mine", 64, 80, 64080)
    R = [(15, 7), (15, 28), (44, 37), (44, 54), (29, 72), (8, 47)]
    m.ell(15, 8, 9, 6.5, m.open); m.box(12, 18, 0, 3, m.open)                 # Z01 礦口
    m.ell(16, 28, 9, 6.5, m.open)                                              # Z02 礦車爆破點
    m.ell(44, 37, 11, 8, m.open)                                               # Z03 晶礦大廳
    m.ell(44, 54, 10, 6.5, m.open)                                             # Z04 斷橋深谷
    m.ell(8, 47, 5, 5, m.open)                                                 # Z06 受困礦工
    m.stroke([(15, 12), (14, 18), (16, 23)], 5, m.open, 0.8)                   # 0-1
    m.stroke([(23, 29), (30, 29), (35, 33)], 4, m.open)                        # 1-2 (rubble at x=28, straight section)
    m.stroke([(44, 44), (44, 48)], 5, m.open)                                  # 2-3
    m.stroke([(38, 57), (30, 57), (30, 66)], 4, m.open)                        # 3-4 (crystal barrier on the straight x=30 section)
    m.stroke([(36, 52), (28, 51), (21, 50), (20, 44), (18, 38), (17, 34)], 4, m.open, 0.4)  # 3-1 loop (chasm bridge at x=20)
    m.stroke([(11, 33), (9, 38), (8, 42)], 3, m.open)                          # 1-5 side
    obj = arena(m, 29, 72, 8.5, [[30.0, 64.2, 5.4, 1.2]], "PROP_rubble_draft", "PROP_mine_crystal_draft")
    # chasm across the vertical part of the loop corridor; the stone bridge (x 19-20) is freed by the lever
    band = np.zeros_like(m.open); m.box(14, 26, 44, 46, band)
    m.water |= band & m.open
    m.box(19, 20, 43, 47, m.bridge)
    # underground pool in the crystal hall (decor + hazard)
    m.ell(50, 40, 3, 2, m.water)
    # paths: rails and the miners' road
    m.stroke([(15, 0), (15, 12), (14, 18), (16, 23), (16, 28), (23, 29), (30, 29), (35, 33), (44, 37), (44, 48), (44, 54), (38, 57), (30, 57), (30, 66)], 1.8, m.path)
    m.path &= m.open & ~m.water
    # spawn safety
    m.ell(15, 4, 7, 4.5, m.nospawn); m.ell(8, 47, 5, 5, m.nospawn); m.box(18, 30, 26, 32, m.nospawn)
    m.objects += [
        {"kind": "cart", "x": 18.5, "y": 29.0, "stop": [26.4, 29.0], "gate": "mine_rubble"},
        {"kind": "gate", "gate": "mine_rubble", "x": 28.0, "y": 29.0, "w": 1.4, "h": 5.2, "sprite": "PROP_rubble_draft", "count": 2,
         "openSprite": "", "notice": "落石炸開了！通往晶礦大廳"},
        {"kind": "altar", "x": 44.0, "y": 38.0, "sprite": "PROP_crystal_altar_draft", "wave": 30, "notice": "晶礦共鳴：礦坑怪潮來襲！"},
        {"kind": "crystal", "x": 39.0, "y": 55.5, "circuit": "mine_core", "sprite": "PROP_crystal_node_draft", "litSprite": "PROP_crystal_node_lit_draft"},
        {"kind": "crystal", "x": 50.0, "y": 52.0, "circuit": "mine_core", "sprite": "PROP_crystal_node_draft", "litSprite": "PROP_crystal_node_lit_draft"},
        {"kind": "gate", "gate": "mine_core", "x": 30.0, "y": 60.6, "w": 5.4, "h": 1.2, "sprite": "PROP_dark_barrier_draft", "count": 3,
         "openSprite": "", "notice": "導電水晶亮起，屏障消失了"},
        {"kind": "bridge_stone", "x": 20.0, "y": 45.0, "gap": [2.2, 3.2], "lever": [19.0, 39.8]},
        {"kind": "npc", "id": "miner", "sheet": "NPC_villager_05_ref", "name": "礦工杜克", "x": 7.5, "y": 48.0, "facing": "down",
         "lines": ["救命……落石把出口堵住了，我在這裡困了三天。", "謝謝你，魔女！晶核魔像在最深處的核心礦室，礦脈就是被它吸乾的。",
                   "這些星砂請收下。對了——魔像會吸收雷電，別用雷打它，除非你想讓它過載。"],
         "after": ["我先在這裡喘口氣。祝你好運，魔女。"], "quest": "rescue_miner", "reward": "sand:20"},
        {"kind": "sign", "x": 18.5, "y": 5.0, "name": "礦坑告示",
         "lines": ["Z01 礦口。北：礦車爆破點。礦車可以炸開落石。", "核心礦室前有晶能屏障，點亮兩座導電水晶即可通過（雷擊或按 F）。",
                   "深谷有座斷橋，拉桿可以修復，從那裡也能回到礦車道。"]},
        obj,
    ]
    for (x, y) in ((12, 9), (18, 10), (12, 26), (20, 26), (38, 34), (50, 34), (40, 52), (48, 57)):
        m.props.append({"sprite": "PROP_lamp_draft", "x": x + 0.5, "y": y, "fade": False})
    m.spawns += [{"id": "spawn_from_village", "x": 15.0, "y": 2.4}, {"id": "spawn_default", "x": 15.0, "y": 2.4}]
    zone(m, "Z05", "Z05 核心礦室", 29, 72, 10, 8.5, True)
    zone(m, "Z06", "Z06 受困礦工", 8, 47, 6, 6)
    zone(m, "Z04", "Z04 斷橋深谷", 44, 54, 12, 7)
    zone(m, "Z03", "Z03 晶礦大廳", 44, 37, 13, 9)
    zone(m, "Z02", "Z02 礦車爆破點", 16, 28, 12, 9)
    zone(m, "Z01", "Z01 礦口", 15, 8, 12, 9)
    m.edge_props(["PROP_mine_rock_draft", "PROP_mine_rock_draft", "PROP_mine_crystal_draft", "PROP_mine_support_draft"], step=(2.2, 2.0), near=2.2)
    m.scatter([("PROP_crate_draft", 0.3, [0.7, 0.35, 0, 0.18]), ("PROP_mine_rock_draft", 0.3, [1.0, 0.4, 0, 0.2]),
               ("PROP_crystal_cluster_draft", 0.4, None)], 50, avoid=[(29, 72, 8), (18.5, 29.4, 3), (44, 38, 3)])
    m.write("mine", "星核礦坑", "穿過礦坑，前往核心礦室", 0.12, (12, 18))


# =================================================================== 星輝湖 80x64
def lake():
    m = Map("lake", 80, 64, 80064)
    m.open[:] = True
    m.water[:] = True
    land = np.zeros_like(m.open)
    m.ell(40, 6, 12, 6.5, land); m.box(35, 45, 0, 3, land)                   # Z01 湖畔營地
    m.ell(15, 24, 8, 6.5, land)                                                # Z02 西浮島
    m.ell(40, 39, 9, 7, land)                                                  # Z03 水晶遺跡
    m.ell(64, 23, 8, 6.5, land)                                                # Z04 東浮島
    m.ell(70, 40, 5, 4, land)                                                  # Z06 湖岸殘頁
    m.stroke([(34, 8), (26, 12), (19, 18)], 4, land, 0.6)                      # 0-1 causeway
    m.stroke([(46, 8), (54, 12), (61, 18)], 4, land, 0.6)                      # 0-3 causeway
    m.stroke([(67, 29), (69, 36)], 3, land)                                    # 3-5
    obj = arena(m, 40, 56, 8.0, [[40.0, 48.2, 3.8, 1.2]], "PROP_lake_crystal_draft", "PROP_ruin_pillar_draft")
    m.ell(40, 56, 8, 6.8, land)
    m.stroke([(40, 45), (40, 49)], 3, land)                                    # 2-4 causeway into the heart
    m.stroke([(15, 28), (15, 36)], 4, land)                                    # west island north spur
    m.stroke([(64, 27), (64, 36)], 4, land)                                    # east island north spur
    m.water &= ~land
    # straight light bridges over water (gated): west spur -> ruins, east spur -> ruins, y 35-37
    m.box(17, 31, 35, 37, m.bridge)
    m.box(49, 62, 35, 37, m.bridge)
    m.bridge &= m.water
    m.stroke([(40, 0), (40, 6), (34, 8), (26, 12), (19, 18), (15, 24)], 1.6, m.path)
    m.stroke([(40, 6), (46, 8), (54, 12), (61, 18), (64, 23)], 1.6, m.path)
    m.stroke([(40, 39), (40, 49), (40, 56)], 1.6, m.path)
    m.path &= land
    m.ell(40, 4, 9, 5, m.nospawn); m.ell(70, 40, 5, 4, m.nospawn)
    m.open = land | m.bridge   # bridge cells keep their water look; collision is freed by `bridge`
    m.objects += [
        {"kind": "gate", "gate": "lake_west", "x": 18.0, "y": 36.5, "w": 1.0, "h": 3.6, "sprite": "PROP_light_bridge_off_draft", "count": 15,
         "span": [17.5, 36.5, 31.5, 36.5], "openSprite": "PROP_light_bridge_on_draft", "notice": "西側光橋亮起！"},
        {"kind": "gate", "gate": "lake_east", "x": 62.0, "y": 36.5, "w": 1.0, "h": 3.6, "sprite": "PROP_light_bridge_off_draft", "count": 14,
         "span": [49.5, 36.5, 62.5, 36.5], "openSprite": "PROP_light_bridge_on_draft", "notice": "東側光橋亮起！"},
    ]
    for gate, pts in (("lake_west", ((11.5, 26.5), (18.5, 21.0))), ("lake_east", ((61.0, 26.5), (67.5, 21.0)))):
        for (x, y) in pts:
            m.objects.append({"kind": "crystal", "x": x, "y": y, "circuit": gate, "sprite": "PROP_pylon_draft", "litSprite": "PROP_pylon_lit_draft"})
    m.objects += [
        {"kind": "altar", "x": 40.0, "y": 40.0, "sprite": "PROP_lake_crystal_draft", "wave": 32, "notice": "水晶遺跡甦醒：湖中的影子湧出！"},
        {"kind": "npc", "id": "ella", "sheet": "NPC_ella_draft", "name": "艾拉", "x": 36.0, "y": 7.6, "facing": "down",
         "lines": ["露米，你來了。這片湖……倒映的是另一片星空。",
                   "我得向你坦白：年輕時，是我用月影魔法封印了那本魔典。如今封印正在失效。",
                   "湖心的影子會模仿你的魔法。點亮浮島上的導電塔，光橋就會架起來——西邊或東邊都行。"],
         "after": ["小心湖影。它會用你自己的法術對付你。"], "quest": "talked_ella_lake"},
        {"kind": "note", "id": "lake_page", "x": 71.0, "y": 41.0, "sprite": "PROP_note_draft", "name": "湖岸殘頁",
         "lines": ["一頁被湖水浸濕卻仍在發光的書頁。", "「……月影封印需要持有者的心願才能維持。」字跡是艾拉的。"],
         "quest": "lake_page", "reward": "page"},
        obj,
    ]
    m.props += [{"sprite": "PROP_tent_draft", "x": 45.5, "y": 8.2, "fade": True, "box": [1.6, 0.5, 0, 0.25]},
                {"sprite": "PROP_lamp_draft", "x": 33.0, "y": 5.0, "fade": False}, {"sprite": "PROP_lamp_draft", "x": 47.5, "y": 5.0, "fade": False}]
    for k in range(6):
        a = k * math.pi / 3 + math.pi / 6
        m.props.append({"sprite": "PROP_ruin_pillar_broken_draft" if k % 2 else "PROP_ruin_pillar_draft",
                        "x": round(40 + math.cos(a) * 6.5, 2), "y": round(39 + math.sin(a) * 5.0, 2), "fade": True, "box": [0.6, 0.35, 0, 0.18]})
    m.spawns += [{"id": "spawn_from_village", "x": 40.0, "y": 2.4}, {"id": "spawn_default", "x": 40.0, "y": 2.4}]
    zone(m, "Z05", "Z05 湖心", 40, 56, 10, 8, True)
    zone(m, "Z06", "Z06 湖岸殘頁", 70, 40, 6, 5)
    zone(m, "Z03", "Z03 水晶遺跡", 40, 39, 11, 8)
    zone(m, "Z02", "Z02 西浮島", 15, 24, 11, 9)
    zone(m, "Z04", "Z04 東浮島", 64, 23, 11, 9)
    zone(m, "Z01", "Z01 湖畔營地", 40, 6, 14, 8)
    # shore dressing: reeds on land next to water, willows and crystals on island rims
    walk = m.walk
    for y in range(1, m.H - 1):
        for x in range(1, m.W - 1):
            if land[y, x] and not m.path[y, x] and m.water[y - 1:y + 2, x - 1:x + 2].any() and m.rng.random() < 0.16:
                m.props.append({"sprite": "PROP_reed_draft", "x": round(x + m.rng.uniform(0.2, 0.8), 2), "y": round(y + m.rng.uniform(0.1, 0.6), 2), "fade": False})
    m.scatter([("PROP_willow_draft", 0.25, [0.6, 0.35, 0, 0.18]), ("PROP_lake_crystal_draft", 0.25, [0.6, 0.3, 0, 0.15]),
               ("PROP_bush_draft", 0.5, None)], 70, avoid=[(40, 56, 8), (40, 39, 4), (40, 6, 4)])
    m.write("lake", "星輝湖", "點亮導電塔，渡過浮島前往湖心", 0.1, (35, 45))


# =================================================================== 失落觀星台 64x96
def observatory():
    m = Map("observatory", 64, 96, 64096)
    m.ell(12, 7, 8, 6.5, m.open); m.box(9, 15, 0, 3, m.open)                   # Z01 山腳
    m.ell(47, 24, 11, 9, m.open)                                               # Z02 折射鏡台
    m.ell(14, 45, 8, 7, m.open)                                                # Z03 雪道
    m.ell(47, 67, 10, 7.5, m.open)                                             # Z04 觀星儀廣場
    m.ell(8, 63, 4.5, 4.5, m.open)                                             # Z06 舊觀測紀錄
    m.stroke([(19, 9), (28, 14), (37, 19)], 4, m.open, 0.6)                    # 0-1
    m.stroke([(38, 30), (28, 36), (20, 41)], 4, m.open, 0.6)                   # 1-2
    m.stroke([(12, 13), (12, 26), (13, 38)], 4, m.open, 1.0)                   # 0-2 (snow road)
    m.stroke([(20, 50), (26, 56), (36, 56), (40, 61)], 4, m.open)              # 2-3 (star door on the straight y=56 section)
    m.stroke([(10, 52), (9, 59)], 3, m.open)                                   # 2-5
    m.stroke([(42, 74), (31, 75), (31, 81)], 4, m.open)                        # 3-4 (straight x=31 section into the tower top)
    obj = arena(m, 31, 87, 8.0, [[31.0, 79.4, 5.4, 1.2]], "PROP_star_door_draft", "PROP_dark_pillar_draft")
    # frozen ponds (ice hazard) on the snow road
    m.ell(22, 46, 2.5, 1.6, m.water); m.ell(52, 70, 2.2, 1.4, m.water)
    m.stroke([(12, 0), (12, 13), (12, 26), (13, 38), (14, 45), (20, 50), (26, 56), (36, 56), (40, 61), (47, 67), (42, 74), (31, 75), (31, 82)], 1.6, m.path)
    m.stroke([(19, 9), (28, 14), (37, 19), (47, 24)], 1.4, m.path)
    m.path &= m.open & ~m.water
    m.ell(12, 4, 7, 4.5, m.nospawn); m.ell(8, 63, 4.5, 4.5, m.nospawn)
    m.objects += [
        {"kind": "beam", "x": 42, "y": 18, "dirX": 0, "dirY": 1, "tx": 52, "ty": 20, "gate": "obs_door",
         "mirrors": [{"x": 42, "y": 28, "kind": 1}, {"x": 52, "y": 28, "kind": 0}]},
        {"kind": "gate", "gate": "obs_door", "x": 31.0, "y": 56.0, "w": 1.2, "h": 5.4, "sprite": "PROP_star_door_draft", "count": 1,
         "openSprite": "", "notice": "星光抵達接收器：雪道上的星門開啟了"},
        {"kind": "altar", "x": 47.0, "y": 68.0, "sprite": "PROP_telescope_draft", "wave": 32, "notice": "觀星儀失控：機械守衛湧出！"},
        {"kind": "note", "id": "records", "x": 8.0, "y": 64.5, "name": "舊觀測紀錄",
         "lines": ["小屋裡堆滿泛黃的觀測紀錄，署名是「第一位星痕魔女」。",
                   "「魔典不是工具。它是活的，它渴望改寫世界的規則。」",
                   "「若有一天它失控，持有者只能選擇：封印它，或者讓它聽命於自己。」"],
         "quest": "observatory_records", "reward": "shard:3"},
        {"kind": "sign", "x": 15.5, "y": 6.0, "name": "山腳告示",
         "lines": ["Z01 山腳。東北：折射鏡台。北：雪道。", "雪道上的星門只接受星光。到折射鏡台按 F 轉動鏡子，把光束導向接收器。"]},
        obj,
    ]
    m.props += [{"sprite": "PROP_snow_hut_draft", "x": 8.0, "y": 65.6, "fade": True, "box": [1.6, 0.5, 0, 0.25]}]
    m.spawns += [{"id": "spawn_from_village", "x": 12.0, "y": 2.4}, {"id": "spawn_default", "x": 12.0, "y": 2.4}]
    zone(m, "Z05", "Z05 塔頂", 31, 87, 10, 8, True)
    zone(m, "Z06", "Z06 舊觀測紀錄", 8, 63, 5, 5)
    zone(m, "Z04", "Z04 觀星儀廣場", 47, 67, 12, 9)
    zone(m, "Z03", "Z03 雪道", 14, 45, 10, 12)
    zone(m, "Z02", "Z02 折射鏡台", 47, 24, 13, 11)
    zone(m, "Z01", "Z01 山腳", 12, 8, 11, 10)
    m.edge_props(["PROP_snow_pine_draft", "PROP_snow_pine_draft", "PROP_snow_rock_draft"], step=(1.9, 1.8), near=3.0)
    m.scatter([("PROP_snow_rock_draft", 0.5, [1.0, 0.4, 0, 0.2]), ("PROP_bush_draft", 0.5, None)], 50,
              avoid=[(31, 87, 8), (47, 24, 10), (47, 68, 3)])
    m.write("observatory", "失落觀星台", "沿雪道上行，調整折射鏡，登上塔頂", 0.04, (9, 15))


# =================================================================== 黑月城塞 96x96
def citadel():
    m = Map("citadel", 96, 96, 96096)
    m.ell(47, 9, 11, 7, m.open); m.box(43, 51, 0, 3, m.open)                   # Z01 城門
    m.ell(17, 35, 9, 8, m.open)                                                # Z02 西塔
    m.ell(46, 77, 10, 8, m.open)                                               # Z03 北塔
    m.ell(78, 37, 9, 8, m.open)                                                # Z04 東塔
    m.ell(84, 66, 5, 5, m.open)                                                # Z06 古塔側室
    m.stroke([(38, 12), (28, 20), (20, 27)], 5, m.open, 0.8)                   # 0-1
    m.stroke([(18, 43), (22, 58), (37, 72)], 5, m.open, 1.0)                   # 1-2
    m.stroke([(55, 72), (70, 58), (77, 45)], 5, m.open, 1.0)                   # 2-3
    m.stroke([(70, 40), (64, 47), (55, 47)], 4, m.open)                        # 3-4 (hall east door, straight y=47 section)
    m.stroke([(47, 16), (47, 22), (47, 39)], 4, m.open)                        # 4-0 (hall south door)
    m.stroke([(81, 45), (84, 61)], 3, m.open)                                  # 3-5
    obj = arena(m, 47, 48, 8.5, [[47.0, 39.4, 5.2, 1.2], [56.8, 47.0, 1.2, 5.2]], "PROP_dark_barrier_draft", "PROP_dark_pillar_draft")
    # void pits along the outer ring (hazard)
    for (x, y, rx, ry) in ((30, 50, 3, 2), (62, 62, 2.5, 2), (24, 18, 2, 1.5)):
        m.ell(x, y, rx, ry, m.water)
        m.water &= m.open
    m.stroke([(47, 0), (47, 9), (38, 12), (28, 20), (20, 27), (17, 35), (18, 43), (22, 58), (37, 72), (46, 77), (55, 72), (70, 58), (77, 45), (78, 37)], 1.8, m.path)
    m.stroke([(47, 9), (47, 22), (47, 39)], 1.8, m.path)
    m.path &= m.open & ~m.water
    m.ell(47, 5, 8, 4.5, m.nospawn); m.ell(84, 66, 5, 5, m.nospawn)
    for (x, y) in ((17, 35), (46, 77), (78, 37)):
        m.ell(x, y, 3.5, 3, m.nospawn)
    m.objects += [
        {"kind": "gate", "gate": "hall", "x": 47.0, "y": 30.0, "w": 5.4, "h": 1.2, "sprite": "PROP_dark_barrier_draft", "count": 3,
         "openSprite": "", "notice": "三重封印解除：中央大廳開啟了"},
        {"kind": "gate", "gate": "hall", "x": 61.5, "y": 47.0, "w": 1.2, "h": 5.4, "sprite": "PROP_dark_barrier_draft", "count": 1, "openSprite": ""},
        {"kind": "trial", "x": 17.0, "y": 36.0, "gate": "hall", "school": "自然", "wave": 20, "enemies": ["thorn_wisp", "spore_shroom", "thorn_wisp"]},
        {"kind": "trial", "x": 46.0, "y": 78.0, "gate": "hall", "school": "月影", "wave": 20, "enemies": ["shadow_clone", "dark_acolyte", "shade_slime"]},
        {"kind": "trial", "x": 78.0, "y": 38.0, "gate": "hall", "school": "星痕", "wave": 20, "enemies": ["lens_turret", "clockwork_hound", "frost_wisp"]},
        {"kind": "note", "id": "side_room", "x": 84.0, "y": 67.0, "sprite": "PROP_note_draft", "name": "古塔側室",
         "lines": ["石桌上刻著一段遺言：", "「我把魔典分成三個學派封進三座塔。若你走到這裡，代表封印已破。」",
                   "「魔典會變成你最想成為的樣子。小心，它也會變成你最害怕的樣子。」——第一位星痕魔女"],
         "quest": "citadel_side_room", "reward": "page"},
        {"kind": "sign", "x": 51.5, "y": 6.0, "name": "城門石碑",
         "lines": ["Z01 城門。中央大廳被三重封印鎖住。", "西塔：自然試煉。北塔：月影試煉。東塔：星痕試煉。在封印台按 F 開始。"]},
        obj,
    ]
    for (x, y) in ((17, 39), (46, 81), (78, 41)):
        m.props.append({"sprite": "PROP_tower_draft", "x": x, "y": y + 2.5, "fade": True, "box": [2.6, 0.8, 0, 0.4]})
    for (x, y) in ((41, 7), (53, 7), (42, 42), (52, 42), (12, 30), (22, 30), (73, 32), (83, 32)):
        m.props.append({"sprite": "PROP_moon_brazier_draft", "x": x, "y": y, "fade": False, "box": [0.5, 0.3, 0, 0.15]})
    m.props += [{"sprite": "PROP_banner_draft", "x": 43.0, "y": 13.0, "fade": True}, {"sprite": "PROP_banner_draft", "x": 51.0, "y": 13.0, "fade": True}]
    m.spawns += [{"id": "spawn_from_village", "x": 47.0, "y": 2.4}, {"id": "spawn_default", "x": 47.0, "y": 2.4}]
    zone(m, "Z05", "Z05 中央大廳", 47, 48, 10, 8.5, True)
    zone(m, "Z06", "Z06 古塔側室", 84, 66, 6, 6)
    zone(m, "Z02", "Z02 西塔", 17, 35, 11, 11)
    zone(m, "Z03", "Z03 北塔", 46, 77, 12, 10)
    zone(m, "Z04", "Z04 東塔", 78, 37, 11, 11)
    zone(m, "Z01", "Z01 城門", 47, 10, 14, 10)
    m.edge_props(["PROP_dark_pillar_draft", "PROP_dark_pillar_broken_draft"], step=(2.6, 2.4), near=2.0, box=[0.7, 0.4, 0, 0.2])
    m.scatter([("PROP_dark_pillar_broken_draft", 0.3, [0.7, 0.4, 0, 0.2]), ("PROP_crystal_cluster_draft", 0.3, None), ("PROP_rock_dark_draft", 0.4, [0.7, 0.35, 0, 0.18])],
              60, avoid=[(47, 48, 9), (17, 35, 4), (46, 77, 4), (78, 37, 4)])
    m.write("citadel", "黑月城塞", "完成三座古塔的試煉，進入中央大廳", 0.14, (43, 51))


if __name__ == "__main__":
    mine()
    lake()
    observatory()
    citadel()
