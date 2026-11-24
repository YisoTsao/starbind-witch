"""Index imported art, audio, and fonts using their Unity GUIDs and existing authoring manifests."""
import csv
import json
import re
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / "Assets/_Game"


def generate():
    records = {}
    for manifest in sorted((GAME / "Art").glob("*manifest.json")):
        for item in json.loads(manifest.read_text()).get("items", []):
            if "path" in item:
                records[item["path"]] = (manifest.name, item)
    files = sorted((GAME / "Art").rglob("*.png"))
    files += sorted((GAME / "Audio").rglob("*.wav"))
    files += sorted((GAME / "Resources/Fonts").glob("*.ttf"))
    with (ROOT / "docs/ASSET_MANIFEST.csv").open("w", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(("guid","path","category","format","dimensions","frameSize","frames","fps","pivotPixels","sourceRecord","license","status","gate"))
        for path in files:
            rel = path.relative_to(ROOT).as_posix()
            guid = re.search(r"^guid: (\w+)",Path(str(path)+".meta").read_text(),re.M)[1]
            source,item = records.get(rel,("Unity importer",{}))
            size = ""
            if path.suffix == ".png":
                with Image.open(path) as im:
                    size = f"{im.width}x{im.height}"
            font = path.suffix == ".ttf"
            writer.writerow((guid,rel,path.relative_to(GAME).parts[1],path.suffix[1:],size,
                             f"{item['w']}x{item['h']}" if "w" in item and "h" in item else "",
                             item.get("frames",""),item.get("fps",""),
                             f"{item['pivotX']},{item['pivotY']}" if "pivotX" in item else "",
                             source,"FusionPixel12_OFL.txt" if font else "review before release",
                             "integrated font" if font else "demo","G4" if font else "asset review"))


if __name__ == "__main__":
    generate()
