"""Village north block (星露村・魔女小屋外圍) layout, derived from reference image A.

A's native 520x300 art pixels are placed with its left edge at tile x=6 and its top at the map top,
so every prop extracted from A sits exactly where A shows it: world = (OX + nx/32, H - ny/32).
The rest of the 28x20 map extends A to the west (plateau), south (river, path to forest) and east.
Writes Assets/_Game/Data/Maps/village_north.json (read by Starbind.Editor.VillageSceneBuilder).
Usage: python3 tools/map/village_north.py <repo_root>
"""
import json, os, sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
W, H, OX = 28, 20, 6
man = json.load(open(os.path.join(ROOT, "Assets/_Game/Art/Props/Draft/extract_manifest.json")))


def A(nx, ny):
    return round(OX + nx / 32.0, 3), round(H - ny / 32.0, 3)


def grid(ch="."):
    return [[ch] * W for _ in range(H)]  # [r][x], r=0 is the TOP row (y = H-1)


def rect(g, x0, x1, r0, r1, ch):
    for r in range(r0, r1 + 1):
        for x in range(x0, x1 + 1):
            if 0 <= r < H and 0 <= x < W:
                g[r][x] = ch


water, path, cliff, coll = grid(), grid(), grid(), grid()
# west plateau (blocked highland) with the upper pond; face + waterfall below it (A: x 0-3.6 tiles)
rect(cliff, 0, 9, 0, 4, "R")
rect(water, 4, 8, 3, 4, "~")
rect(cliff, 0, 9, 5, 5, "F"); cliff[5][7] = "W"
# lower pool (A) and the river flowing south to the map edge
rect(water, 3, 9, 6, 8, "~"); rect(water, 2, 7, 9, 10, "~"); rect(water, 3, 5, 11, 19, "~")
rect(water, 2, 4, 15, 19, "~")
# south cliff (A bottom right): the village sits above, lower meadow below
rect(cliff, 15, 27, 8, 8, "F")
# path: cottage door -> junction -> east branch / south to the forest exit (A: Y-shaped dirt road)
rect(path, 13, 14, 3, 4, "p"); rect(path, 12, 18, 5, 5, "p"); rect(path, 17, 19, 4, 4, "p")
rect(path, 11, 14, 6, 6, "p"); rect(path, 11, 12, 7, 19, "p"); rect(path, 13, 13, 7, 7, "p")

# collision: plateau, faces, water, map perimeter
for r in range(H):
    for x in range(W):
        if cliff[r][x] in "RFW" or water[r][x] == "~" or r in (0, H - 1) or x in (0, W - 1):
            coll[r][x] = "#"


def prop(sprite, x, y, collider=None, fade=False, extra=None):
    d = {"sprite": sprite, "x": x, "y": y, "fade": fade}
    if collider:
        d["box"] = collider  # [width, height, offsetX, offsetY] in tiles, relative to the pivot
    if extra:
        d.update(extra)
    return d


def from_A(sprite, collider=None, fade=False, extra=None):
    nx, ny = man[sprite]["native_pivot"]
    x, y = A(nx, ny)
    return prop(sprite, x, y, collider, fade, extra)


TRUNK = [0.55, 0.3, 0, 0.15]
props = [
    from_A("PROP_cottage_draft", [5.6, 2.6, 0, 1.3], extra={"door": {"dx": 0.34, "dy": -0.25, "target": "Workshop", "spawn": "spawn_from_village"}}),
    from_A("PROP_tree_blossom_draft", [0.7, 0.35, 0, 0.18], fade=True),
    from_A("PROP_tree_green_s_draft", TRUNK, fade=True),
    from_A("PROP_tree_green_m_draft", TRUNK, fade=True),
    from_A("PROP_tree_green_b_draft", TRUNK, fade=True),
    from_A("PROP_rock_l_draft", [0.9, 0.5, 0, 0.25]),
    from_A("PROP_rock_s_draft", [0.5, 0.3, 0, 0.15]),
    from_A("PROP_rock_m_draft", [0.7, 0.4, 0, 0.2]),
    from_A("PROP_stump_draft", [0.9, 0.45, 0, 0.22]),
    from_A("PROP_mushroom_glow_draft"),
    from_A("PROP_fence_long_draft", [2.9, 0.3, 0, 0.15]),
    from_A("PROP_fence_mid_draft", [2.1, 0.3, 0, 0.15]),
    from_A("PROP_fence_east_draft", [3.0, 0.3, 0, 0.15]),
    from_A("PROP_sign_draft", [0.35, 0.25, 0, 0.12]),
    from_A("PROP_stall_draft", [2.3, 1.0, 0, 0.5], fade=True),
    from_A("PROP_crystal_fountain_draft", [1.5, 0.9, 0, 0.45]),
    from_A("PROP_flowerbush_draft"),
    # extension beyond A (reusing the same drafts)
    prop("PROP_tree_blossom_draft", 2.4, 16.6, [0.7, 0.35, 0, 0.18], True),
    prop("PROP_tree_green_b_draft", 8.3, 17.1, TRUNK, True),
    prop("PROP_tree_green_m_draft", 21.6, 17.3, TRUNK, True),
    prop("PROP_tree_green_s_draft", 24.2, 18.4, TRUNK, True),
    prop("PROP_tree_blossom_draft", 25.6, 14.2, [0.7, 0.35, 0, 0.18], True),
    prop("PROP_tree_green_b_draft", 24.0, 12.1, TRUNK, True),
    prop("PROP_rock_m_draft", 26.3, 16.6, [0.7, 0.4, 0, 0.2]),
    prop("PROP_tree_green_m_draft", 8.6, 4.2, TRUNK, True),
    prop("PROP_tree_green_s_draft", 9.4, 1.8, TRUNK, True),
    prop("PROP_tree_blossom_draft", 16.6, 6.2, [0.7, 0.35, 0, 0.18], True),
    prop("PROP_tree_green_b_draft", 21.4, 7.6, TRUNK, True),
    prop("PROP_tree_green_m_draft", 25.2, 4.6, TRUNK, True),
    prop("PROP_tree_green_s_draft", 19.4, 2.4, TRUNK, True),
    prop("PROP_rock_l_draft", 14.8, 3.0, [0.9, 0.5, 0, 0.25]),
    prop("PROP_rock_s_draft", 22.6, 5.4, [0.5, 0.3, 0, 0.15]),
    prop("PROP_stump_draft", 7.6, 6.9, [0.9, 0.45, 0, 0.22]),
    prop("PROP_flowerbush_draft", 15.2, 9.3), prop("PROP_flowerbush_draft", 20.2, 9.6),
    prop("PROP_flowerbush_draft", 9.8, 8.6), prop("PROP_flowerbush_draft", 23.4, 2.0),
    prop("PROP_mushroom_glow_draft", 17.9, 3.1),
    prop("PROP_fence_mid_draft", 24.8, 9.6, [2.1, 0.3, 0, 0.15]),
]

wx, wy = A(*man["CHR_witch_front_draft"]["native_pivot"])
data = {
    "id": "village_north", "width": W, "height": H, "status": "draft layout from reference A",
    "layers": {k: ["".join(row) for row in g] for k, g in
               (("water", water), ("path", path), ("cliff", cliff), ("collision", coll))},
    "props": props,
    "spawns": [{"id": "spawn_square", "x": wx, "y": wy}, {"id": "spawn_from_workshop", "x": 14.53, "y": 16.4}],
    "player_sprite": "CHR_witch_front_draft",
}
os.makedirs(os.path.join(ROOT, "Assets/_Game/Data/Maps"), exist_ok=True)
json.dump(data, open(os.path.join(ROOT, "Assets/_Game/Data/Maps/village_north.json"), "w"), indent=1)
merged = grid()
for r in range(H):
    for x in range(W):
        merged[r][x] = (cliff[r][x] if cliff[r][x] != "." else water[r][x] if water[r][x] != "." else path[r][x])
print("\n".join("%2d %s" % (H - 1 - r, "".join(row)) for r, row in enumerate(merged)))
print("spawn", wx, wy, "props", len(props))
