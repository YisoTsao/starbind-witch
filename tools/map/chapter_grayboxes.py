"""Generate the reversible L1 layouts used by the Unity editor's chapter explorer."""
import csv
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
# Dimensions follow STORY_WORLD_QUESTS §3.6. Each coordinate is a room centre.
CHAPTERS = [
    ("forest", "月影森林路線模型", 64, 64, ["入口","蘑菇林道","星痕祭壇","斷裂石橋","菇王棲地","書頁寶箱"],
     [(32,5),(17,21),(32,38),(32,49),(32,59),(49,24)], [(0,1),(1,2),(2,3),(3,4),(0,2),(2,5)]),
    ("mine", "星核礦坑", 64, 80, ["礦口","礦車爆破點","晶礦大廳","斷橋深谷","核心礦室","受困礦工"],
     [(15,7),(15,28),(44,37),(44,54),(29,72),(8,47)], [(0,1),(1,2),(2,3),(3,4),(3,1),(1,5)]),
    ("lake", "星輝湖", 80, 64, ["湖畔營地","西浮島","水晶遺跡","東浮島","湖心","湖岸殘頁"],
     [(40,6),(15,24),(40,39),(64,23),(40,56),(70,39)], [(0,1),(1,2),(2,4),(0,3),(3,2),(3,5)]),
    ("observatory", "失落觀星台", 64, 96, ["山腳","折射鏡台","雪道","觀星儀廣場","塔頂","舊觀測紀錄"],
     [(12,6),(48,24),(14,45),(47,67),(31,88),(8,63)], [(0,1),(1,2),(2,3),(3,4),(0,2),(2,5)]),
    ("citadel", "黑月城塞", 96, 96, ["城門","西塔","北塔","東塔","中央大廳","古塔側室"],
     [(47,8),(17,35),(46,77),(78,37),(47,48),(84,66)], [(0,1),(1,2),(2,3),(3,4),(4,0),(3,5)]),
]


def build(spec):
    ident, title, w, h, labels, points, edges = spec
    grid = [list("#" * w) for _ in range(h)]

    def carve(x, y, radius):
        for row in grid[y-radius:y+radius+1]:
            row[x-radius:x+radius+1] = "." * (2*radius+1)

    for a, b in edges:
        x, y = points[a]
        tx, ty = points[b]
        # Cross sloping routes with short steps, leaving rooms visibly wider than passages.
        steps = max(abs(tx-x), abs(ty-y))
        for i in range(steps+1):
            carve(round(x + (tx-x)*i/steps), round(y + (ty-y)*i/steps), 1)
    for x, y in points:
        carve(x, y, 3)
    return dict(id=ident, title=title, rows=["".join(row) for row in grid],
                anchors=[dict(name=name,x=x,y=y) for name,(x,y) in zip(labels,points)])


if __name__ == "__main__":
    chapters = [build(spec) for spec in CHAPTERS]
    for i, chapter in enumerate(chapters):
        chapter["previous"] = chapters[i-1]["id"] if i else ""
        chapter["next"] = chapters[i+1]["id"] if i+1 < len(chapters) else ""
    target = ROOT / "Assets/_Game/Editor/Graybox/chapters.json"
    target.write_text(json.dumps(dict(chapters=chapters),ensure_ascii=False,indent=2)+"\n")
    with (ROOT/"docs/ZONE_MANIFEST.csv").open("w", newline="") as f:
        table = csv.writer(f)
        table.writerow("chapterId zoneId sizeTiles tileSize exits entrances landmark mainPath sidePaths encounterSlots questSlots artStatus gameplayStatus validationStatus".split())
        for spec,c in zip(CHAPTERS,chapters):
            ident,_,w,h,labels,_,edges = spec
            for n,label in enumerate(labels):
                adjacent = [f"{ident}.{b if a==n else a}" for a,b in edges if n in (a,b)]
                if n==0 and c["previous"]: adjacent.append(c["previous"]+".4")
                if n==4 and c["next"]: adjacent.append(c["next"]+".0")
                routes = "|".join(adjacent)
                # chapter scenes built from these rooms: Forest (hand-made), Mine/Lake/Observatory/Citadel (tools/map/chapters.py)
                table.writerow([ident,f"{ident}.{n}",f"{w}x{h}",32,routes,routes,label,n<5,n==5,n in (2,4),n in (1,5),
                                "procedural draft","playable draft","validate_chapters.py + ChapterPlayTests"])
