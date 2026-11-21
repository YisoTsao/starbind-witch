"""Draft UI prompt icon and editor-only debug tiles.
UI_prompt_F_draft.png: Needs Cleanup (draft UI). DBG_*.png: editor-only collision/marker tiles,
never rendered in Play mode or builds (TECH_ARCHITECTURE.md §6), so they are tools, not art.
Usage: python3 tools/art/gen_ui_debug.py <repo_root>
"""
import os, sys
import numpy as np
from PIL import Image
ROOT = sys.argv[1] if len(sys.argv) > 1 else "."

def c(h, a=255):
    return [int(h[i:i + 2], 16) for i in (1, 3, 5)] + [a]

# 13x14 key cap with an "F" glyph, palette from the brand purple/gold + A outline colour
key = np.zeros((14, 14, 4), np.uint8)  # even size; column 13 stays transparent
key[1:12, 1:12] = c("#f3e9d2"); key[12, 1:12] = c("#b9a27a")          # face + bottom bevel
key[0, 1:12] = key[13, 1:12] = c("#343556"); key[1:13, 0] = key[1:13, 12] = c("#343556")
key[12, 1:12] = c("#8d4c82")
glyph = ["11111", "10000", "11110", "10000", "10000", "10000", "10000"]
for y, row in enumerate(glyph):
    for x, v in enumerate(row):
        if v == "1":
            key[3 + y, 4 + x] = c("#343556")
os.makedirs(os.path.join(ROOT, "Assets/_Game/Art/UI/Draft"), exist_ok=True)
Image.fromarray(key).save(os.path.join(ROOT, "Assets/_Game/Art/UI/Draft/UI_prompt_F_draft.png"))

dbg = os.path.join(ROOT, "Assets/_Game/Tiles/Debug"); os.makedirs(dbg, exist_ok=True)
for name, col in (("DBG_block", "#ff2050"), ("DBG_spawn", "#20c0ff")):
    t = np.zeros((32, 32, 4), np.uint8)
    t[:, :] = c(col, 70)
    for i in range(-32, 32, 6):
        for y in range(32):
            x = y + i
            if 0 <= x < 32:
                t[y, x] = c(col, 160)
    t[0, :] = t[-1, :] = t[:, 0] = t[:, -1] = c(col, 220)
    Image.fromarray(t).save(os.path.join(dbg, name + ".png"))
print("ok")
