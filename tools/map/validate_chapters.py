"""Prove every chapter map can be finished: BFS over walkable cells with every mechanism blocker closed,
then open a blocker as soon as all of its triggers are reachable, and repeat until nothing changes.
Passes when the boss arena trigger, every side-quest note/NPC and every trigger cell were reached, and
when the entrance cannot reach the arena with every blocker closed (the mechanics really gate it).

Blockers: PowerGate rects ("gate"), the lever bridge gap ("bridge_stone"). Boss-arena gates start open.
Triggers: cart (rubble), crystals per circuit, beam mirrors (puzzle), trial seals (citadel hall).
Usage: python3 tools/map/validate_chapters.py <repo_root>   (exit code 1 on failure)
"""
import json, os, sys
from collections import deque

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."


def load(ident):
    return json.load(open(os.path.join(ROOT, "Assets/_Game/Data/Maps", ident + ".json")))


def rect_cells(cx, cy, w, h, pad=0.25):
    """Cells a centre-sized box collider covers (with the player's half-width as padding)."""
    x0, x1 = cx - w / 2 - pad, cx + w / 2 + pad
    y0, y1 = cy - h / 2 - pad, cy + h / 2 + pad
    return {(x, y) for x in range(int(x0), int(x1) + 1) for y in range(int(y0), int(y1) + 1)
            if x + 1 > x0 and x < x1 and y + 1 > y0 and y < y1}


def validate(ident):
    d = load(ident)
    W, H = d["width"], d["height"]
    coll = d["layers"]["collision"]
    free = {(x, y) for y in range(H) for x in range(W) if coll[H - 1 - y][x] != "#"}
    blockers, triggers = {}, {}
    for o in d["objects"]:
        k = o["kind"]
        if k == "gate":
            blockers.setdefault(o["gate"], set()).update(rect_cells(o["x"], o["y"], o["w"], o["h"]))
        elif k == "bridge_stone":
            blockers["lever_" + ident] = rect_cells(o["x"], o["y"], o["gap"][0], o["gap"][1])
            triggers.setdefault("lever_" + ident, []).append((o["lever"][0], o["lever"][1]))
        elif k == "cart":
            triggers.setdefault(o["gate"], []).append((o["x"], o["y"] - 0.2))
        elif k == "crystal":
            triggers.setdefault(o["circuit"], []).append((o["x"], o["y"] - 0.1))
        elif k == "beam":
            for mi in o["mirrors"]:
                triggers.setdefault(o["gate"], []).append((mi["x"] + 0.5, mi["y"] + 0.2))
        elif k == "trial":
            triggers.setdefault(o["gate"], []).append((o["x"], o["y"] - 0.3))
    arena = next(o for o in d["objects"] if o["kind"] == "boss_arena")
    ax, ay, aw, ah = arena["trigger"]
    arena_cells = {(x, y) for x in range(int(ax), int(ax + aw)) for y in range(int(ay), int(ay + ah))} & free
    side = [(o["x"], o["y"]) for o in d["objects"] if o["kind"] in ("note",) or (o["kind"] == "npc" and o.get("quest"))]
    start = tuple(int(v) for v in (d["spawns"][0]["x"], d["spawns"][0]["y"]))

    def near(reach, p, r=1.2):
        return any((x + 0.5 - p[0]) ** 2 + (y + 0.5 - p[1]) ** 2 <= r * r for x, y in reach)

    def bfs(closed):
        blocked = set().union(*[blockers[g] for g in closed]) if closed else set()
        seen, q = {start}, deque([start])
        while q:
            x, y = q.popleft()
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                n = (x + dx, y + dy)
                if n in free and n not in blocked and n not in seen:
                    seen.add(n)
                    q.append(n)
        return seen

    closed = set(blockers)
    gated = not (bfs(closed) & arena_cells)
    order = []
    while True:
        reach = bfs(closed)
        opened = [g for g in closed if g in triggers and all(near(reach, t) for t in triggers[g])]
        if not opened:
            break
        for g in opened:
            closed.discard(g)
            order.append(g)
    reach = bfs(closed)
    ok_arena = bool(reach & arena_cells)
    ok_side = all(near(reach, p) for p in side)
    all_triggers = [t for ts in triggers.values() for t in ts]
    ok_triggers = all(near(reach, t) for t in all_triggers)
    ok = ok_arena and ok_side and ok_triggers and gated and not closed
    print(f"{ident:12s} {'PASS' if ok else 'FAIL'}  open order {order}  arena {ok_arena}  side quests {ok_side}  "
          f"triggers {ok_triggers}  gated-without-mechanics {gated}  still closed {sorted(closed)}  reachable cells {len(reach)}")
    return ok


if __name__ == "__main__":
    results = [validate(i) for i in ("mine", "lake", "observatory", "citadel")]
    sys.exit(0 if all(results) else 1)
