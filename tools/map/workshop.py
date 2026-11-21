"""Witch's cottage interior (Workshop, 14 x 10, WORLD_STRUCTURE.md §3). Top-down 3/4: the top two rows
are wall (top + face); side and bottom walls are wall-top tiles with the door gap at the bottom centre.
Writes Assets/_Game/Data/Maps/workshop.json. Usage: python3 tools/map/workshop.py <repo_root>"""
import json, os, sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
W, H = 14, 10
wall = [["." for _ in range(W)] for _ in range(H)]  # r = 0 top
coll = [["." for _ in range(W)] for _ in range(H)]
for r in range(H):
    for x in range(W):
        y = H - 1 - r
        if y == H - 1 or x in (0, W - 1) or (y == 0 and x not in (6, 7)):
            wall[r][x] = "T"
        elif y == H - 2:
            wall[r][x] = "F"
        if wall[r][x] != ".":
            coll[r][x] = "#"
coll[H - 1][6] = coll[H - 1][7] = "#"  # door gap is blocked; the door is an F interaction

props = [
    {"sprite": "INT_bookshelf_draft", "x": 1.9, "y": 7.05, "fade": False, "box": [1.4, 0.5, 0, 0.25]},
    {"sprite": "INT_bookshelf_draft", "x": 3.5, "y": 7.05, "fade": False, "box": [1.4, 0.5, 0, 0.25]},
    {"sprite": "INT_window_draft", "x": 6.0, "y": 8.5, "fade": False, "layer": "Ground", "order": 60},
    {"sprite": "INT_window_draft", "x": 8.4, "y": 8.5, "fade": False, "layer": "Ground", "order": 60},
    {"sprite": "INT_wardrobe_draft", "x": 10.6, "y": 7.05, "fade": False, "box": [1.1, 0.5, 0, 0.25]},
    {"sprite": "INT_bed_draft", "x": 12.3, "y": 5.1, "fade": False, "box": [1.1, 1.7, 0, 0.9]},
    {"sprite": "INT_rug_draft", "x": 7.0, "y": 4.3, "fade": False, "layer": "GroundDecal"},
    {"sprite": "INT_desk_draft", "x": 2.4, "y": 4.9, "fade": False, "box": [1.3, 0.5, 0, 0.25]},
    {"sprite": "INT_alchemy_draft", "x": 2.2, "y": 1.6, "fade": False, "box": [1.6, 0.6, 0, 0.3]},
    {"sprite": "INT_cauldron_draft", "x": 4.3, "y": 1.4, "fade": False, "box": [0.9, 0.5, 0, 0.25]},
    {"sprite": "INT_lectern_draft", "x": 7.0, "y": 4.6, "fade": False, "box": [0.6, 0.3, 0, 0.15]},
    {"sprite": "INT_table_draft", "x": 10.6, "y": 2.0, "fade": False, "box": [1.3, 0.5, 0, 0.25]},
    {"sprite": "INT_chair_draft", "x": 9.2, "y": 1.8, "fade": False, "box": [0.4, 0.3, 0, 0.15]},
    {"sprite": "INT_chair_draft", "x": 12.0, "y": 1.8, "fade": False, "box": [0.4, 0.3, 0, 0.15]},
    {"sprite": "INT_plant_draft", "x": 12.6, "y": 7.1, "fade": False, "box": [0.5, 0.3, 0, 0.15]},
    {"sprite": "INT_plant_draft", "x": 5.2, "y": 7.1, "fade": False, "box": [0.5, 0.3, 0, 0.15]},
    {"sprite": "INT_door_in_draft", "x": 7.0, "y": 0.25, "fade": False, "layer": "Ground", "order": 60},
]
objects = [
    {"kind": "station", "station": "Lectern", "x": 7.0, "y": 4.4, "radius": 0.7, "prompt": 1.9},
    {"kind": "station", "station": "Research", "x": 2.4, "y": 4.6, "radius": 0.8, "prompt": 1.9},
    {"kind": "station", "station": "Bed", "x": 12.3, "y": 4.4, "radius": 0.9, "prompt": 2.2},
    {"kind": "door", "x": 7.0, "y": 0.7, "scene": "Village", "spawn": "spawn_from_workshop", "radius": 0.7},
]
spawns = [{"id": "spawn_from_village", "x": 7.0, "y": 1.7}, {"id": "spawn_bed", "x": 11.2, "y": 3.6},
          {"id": "spawn_default", "x": 7.0, "y": 2.6}]
data = {"id": "workshop", "width": W, "height": H, "biome": "interior",
        "layers": {"wall": ["".join(r) for r in wall], "collision": ["".join(r) for r in coll]},
        "props": props, "objects": objects, "spawns": spawns, "zones": [{"id": "workshop", "name": "魔女小屋・工坊", "rect": [0, 0, W, H]}],
        "scene": {"title": "魔女小屋・工坊", "dim": 0.0, "safe": True, "objective": "魔典台配置法術・研究桌永久研究・床存檔"}}
json.dump(data, open(os.path.join(ROOT, "Assets/_Game/Data/Maps/workshop.json"), "w"), indent=1, ensure_ascii=False)
print("\n".join("".join(r) for r in wall))
