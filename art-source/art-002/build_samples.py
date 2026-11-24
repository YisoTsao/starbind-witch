"""Build review-only sprite overrides for an isolated copy of the Unity project."""
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

HERE = Path(__file__).resolve().parent
ART = HERE.parents[1] / "Assets/_Game/Art"
OUTPUT = HERE / "overlay/Assets/_Game/Art"


def write(relative, image):
    with Image.open(ART / relative) as source:
        assert source.size == image.size, relative
    dest = OUTPUT / relative
    dest.parent.mkdir(parents=True, exist_ok=True)
    image.save(dest)


def canopy(kind):
    relative = f"Props/Draft/PROP_forest_{kind}_draft.png"
    with Image.open(ART / relative) as source:
        image = source.convert("RGBA")
    # Paint only within the existing foliage silhouette; the trunk and anchor stay aligned.
    mask = Image.new("L", image.size)
    mask.putdata([255 if a and g > r and g > b else 0 for r, g, b, a in image.getdata()])
    clusters = image.copy()
    draw = ImageDraw.Draw(clusters)
    rng = random.Random(kind)
    palette = ["#285440", "#407344", "#619348", "#91b653"]
    w, h = image.size
    for y in range(0, h, 5):
        for x in range(-4, w, 7):
            cx, cy = x + rng.randrange(5), y + rng.randrange(4)
            tone = max(0, min(2, round(2.4 - cx / w - cy / h + rng.uniform(-0.6, 0.5))))
            draw.ellipse((cx-4, cy-3, cx+5, cy+3), fill=palette[tone])
            draw.line((cx-2, cy-2, cx+1, cy-2), fill=palette[tone+1])
    write(relative, Image.composite(clusters, image, mask))


def grass():
    image = Image.new("RGBA", (128, 32), "#648749")
    draw = ImageDraw.Draw(image)
    for variant in range(4):
        rng = random.Random(variant)
        for _ in range(3):
            x = variant*32 + rng.randrange(2, 23)
            y = rng.randrange(3, 28)
            draw.polygon([(x,y),(x+3,y-1),(x+7,y-1),(x+5,y+1),(x,y+1)], fill="#608347")
    write("Tilesets/Forest/Draft/TILE_forest_grass_draft.png", image)


def witch():
    relative = "Characters/Ref/CHR_witch_ref.png"
    with Image.open(ART / relative) as src:
        pixels = src.convert("RGBA")
    alpha = pixels.getchannel("A").point(lambda a: 255 if a >= 128 else 0)
    # An inner ink edge preserves every frame's bounds and the foot anchor.
    interior = alpha.filter(ImageFilter.MinFilter(3))
    ink = Image.new("RGBA", pixels.size, "#493449")
    result = Image.composite(pixels, ink, interior)
    result.putalpha(alpha)
    write(relative, result)


def flame(draw, x, y, phase):
    tip = round(2 * math.sin(phase))
    draw.polygon([(x-16,y-5+tip),(x-6,y-3),(x-9,y-9),(x+4,y-6),
                  (x+9,y-2),(x+9,y+3),(x+4,y+7),(x-4,y+7),(x-12,y+3),
                  (x-5,y+2)], fill="#aa423b")
    draw.polygon([(x-12,y-4+tip),(x-2,y-1),(x-5,y-6),(x+5,y-3),
                  (x+7,y+1),(x+3,y+5),(x-3,y+4)], fill="#ff913e")
    draw.polygon([(x-3,y-2),(x+4,y),(x+5,y+2),(x+1,y+3)], fill="#fff0a1")


def fire():
    sheet = Image.new("RGBA", (384,36))
    for i in range(8):
        flame(ImageDraw.Draw(sheet), i*48+30, 18, i*math.tau/8)
    write("VFX/Draft/VFX_fireball_polish.png",sheet)
    icon = Image.new("RGBA", (32,32))
    flame(ImageDraw.Draw(icon),18,15,1)
    write("UI/Draft/ICON_fire_bolt_draft.png",icon.transpose(Image.Transpose.ROTATE_90))


if __name__ == "__main__":
    for name in ("oak", "pine", "dark"):
        canopy(name)
    grass()
    witch()
    fire()
