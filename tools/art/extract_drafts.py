"""Extract DRAFT sprites from reference image A (紫幻森林巫女村落.png).

Status of every output: Needs Cleanup (draft). Not production art.
Steps: resample A on its measured pixel grid (3.212 x 3.126 screen px per art pixel) to native
~520x300, crop each object, remove grass/path background by flood fill from the crop border,
write RGBA PNG + pivot metadata (art-source/_drafts/extract_manifest.json).

Usage: python3 tools/art/extract_drafts.py <repo_root>
"""
import json, os, sys
from collections import deque
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from village_decor import fence  # noqa: E402

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
SRC = os.path.join(ROOT, "references/environment/紫幻森林巫女村落.png")
OUT = os.path.join(ROOT, "art-source/_drafts/extracted")
GRID = dict(px=3.212, phx=0.75, py=3.126, phy=1.70)  # measured 2026-11-21

# Objects: name -> (x0, y0, x1, y1) in native px, pivot_y (native px row of ground contact), extra
OBJECTS = {
    "PROP_cottage_draft":        ((150, 0, 352, 110), 94),
    "PROP_tree_blossom_draft":   ((404, 62, 480, 162), 156, {"min_frac": 0.3, "post": "blossom"}),
    "PROP_tree_green_s_draft":   ((324, 168, 368, 216), 212),
    "PROP_tree_green_m_draft":   ((216, 226, 278, 290), 284),
    "PROP_tree_green_b_draft":   ((108, 108, 158, 154), 150, {"erase": [(22, 0, 49, 15, "bright"), (41, 0, 49, 45, "all"), (26, 0, 30, 6, "all"), (37, 6, 41, 14, "all")]}),
    "PROP_rock_l_draft":         ((148, 120, 182, 152), 148, {"erase": [(0, 0, 9, 10, "all")]}),
    "PROP_rock_s_draft":         ((352, 230, 378, 254), 251),
    "PROP_rock_m_draft":         ((478, 126, 506, 152), 149),
    "PROP_stump_draft":          ((284, 210, 320, 242), 238, {"path": False}),
    "PROP_mushroom_glow_draft":  ((318, 230, 352, 262), 258, {"erase": [(0, 0, 8, 5, "all")]}),
    # fences: the crops keep grass between the rails, so they are redrawn at the same size / pivot
    "PROP_fence_long_draft":     ((346, 48, 440, 70), 68, {"min_frac": 0.3, "redraw": "fence"}),
    "PROP_fence_mid_draft":      ((112, 154, 184, 186), 181, {"min_frac": 0.3, "redraw": "fence"}),
    "PROP_fence_east_draft":     ((424, 176, 520, 197), 192, {"min_frac": 0.3, "redraw": "fence"}),
    "PROP_sign_draft":           ((126, 172, 160, 232), 228),
    "PROP_stall_draft":          ((328, 70, 406, 142), 138, {"min_frac": 0.3, "erase": [(70, 22, 78, 52, "pink")]}),
    "PROP_crystal_fountain_draft": ((426, 190, 480, 254), 248, {"erase": [(0, 0, 54, 5, "all")]}),
    "PROP_flowerbush_draft":     ((224, 212, 268, 238), 236),
    "CHR_witch_front_draft":     ((244, 136, 276, 174), 170),
}

def native():
    A = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)
    nx = int((A.shape[1] - GRID["phx"]) / GRID["px"]); ny = int((A.shape[0] - GRID["phy"]) / GRID["py"])
    out = np.zeros((ny, nx, 3), np.float32)
    for j in range(ny):
        y0 = int(np.floor(GRID["phy"] + (j + 0.5) * GRID["py"] - 0.5))
        for i in range(nx):
            x0 = int(np.floor(GRID["phx"] + (i + 0.5) * GRID["px"] - 0.5))
            out[j, i] = A[max(y0, 0):y0 + 2, max(x0, 0):x0 + 2].reshape(-1, 3).mean(0)
    return out.clip(0, 255).astype(np.uint8)


def hsv(rgb):
    c = rgb.astype(np.float32) / 255.0
    mx, mn = c.max(-1), c.min(-1); d = mx - mn + 1e-6
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    return h, np.where(mx > 0, d / (mx + 1e-6), 0), mx


def is_bg(rgb, loose, use_path=True):
    """Grass (hue 55-105, bright) or dirt path (hue 22-48, very bright). `loose` admits darker grass blades."""
    h, s, v = hsv(rgb)
    vmin = 0.36 if loose else 0.46
    grass = (h > 55) & (h < 105) & (s > 0.28) & (v > vmin)
    path = (h > 22) & (h < 48) & (s > 0.3) & (s < 0.65) & (v > 0.78)
    return grass | (path & use_path)


def extract(N, box, opts):
    x0, y0, x1, y1 = box
    crop = N[y0:y1, x0:x1]
    h, w = crop.shape[:2]
    bg = is_bg(crop, opts.get("loose", True), opts.get("path", True))
    seen = np.zeros_like(bg)
    q = deque((y, x) for y in range(h) for x in (0, w - 1)) ; q.extend((y, x) for x in range(w) for y in (0, h - 1))
    while q:
        y, x = q.popleft()
        if not (0 <= y < h and 0 <= x < w) or seen[y, x] or not bg[y, x]:
            continue
        seen[y, x] = True
        q.extend(((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)))
    alpha = np.where(seen, 0, 255).astype(np.uint8)
    # drop isolated specks (opaque pixels with <2 opaque 4-neighbours)
    op = alpha > 0
    nb = np.zeros_like(op, int)
    nb[1:] += op[:-1]; nb[:-1] += op[1:]; nb[:, 1:] += op[:, :-1]; nb[:, :-1] += op[:, 1:]
    alpha[op & (nb < 2)] = 0
    alpha = keep_largest(alpha, opts.get("min_frac", 0.04))
    return np.dstack([crop, alpha])


def keep_largest(alpha, min_frac=0.04):
    """Keep the largest 4-connected opaque component plus any component >= min_frac of it."""
    op = alpha > 0
    lab = np.zeros(op.shape, int); sizes = [0]
    for y0 in range(op.shape[0]):
        for x0 in range(op.shape[1]):
            if op[y0, x0] and not lab[y0, x0]:
                n = len(sizes); sizes.append(0); q = deque([(y0, x0)]); lab[y0, x0] = n
                while q:
                    y, x = q.popleft(); sizes[n] += 1
                    for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                        if 0 <= yy < op.shape[0] and 0 <= xx < op.shape[1] and op[yy, xx] and not lab[yy, xx]:
                            lab[yy, xx] = n; q.append((yy, xx))
    if len(sizes) == 1:
        return alpha
    big = max(sizes)
    keep = [i for i, s in enumerate(sizes) if i and s >= big * min_frac]
    return np.where(np.isin(lab, keep), alpha, 0).astype(np.uint8)


# ---------------------------------------------------------------- cleanup after the crop
# Coordinates are in the trimmed sprite (as written to Assets/_Game/Art/Props/Draft).
def erase(rgba, x0, y0, x1, y1, what):
    """Clear leftovers of neighbouring objects. what: all | bright (flowers, rock) | pink (blossom)."""
    sub = rgba[y0:y1 + 1, x0:x1 + 1]
    h, s, v = hsv(sub[..., :3])
    if what == "all":
        m = np.ones(h.shape, bool)
    elif what == "bright":   # white / pink flowers and grey rock, not the dark-green canopy
        m = (v > 0.62) & ((s < 0.35) | (h > 280) | (h < 20))
    elif what == "pink":
        m = (h > 300) | (h < 10)
    sub[m, 3] = 0


def fix_blossom(rgba):
    """The reference tree's left side is hidden by the stall and its crop holds stall / tree
    leftovers: keep the pink canopy and the trunk, then mirror the right half of the canopy."""
    h, s, v = hsv(rgba[..., :3])
    H, W = rgba.shape[:2]
    ys, xs = np.mgrid[0:H, 0:W]
    canopy = ((h > 250) | (h < 12)) & (s > 0.08) & (ys < 72)
    trunk = (xs > 24) & (xs < 60) & (ys >= 58)
    keep = (canopy | trunk) & (rgba[..., 3] > 0)
    out = rgba.copy()
    out[~keep, 3] = 0
    axis = 41
    for x in range(axis):
        src = 2 * axis - x
        col = out[:68, src] if src < W else np.zeros_like(out[:68, 0])
        out[:68, x] = col
    out[:28, :14, 3] = 0   # branch stubs of the neighbouring trees
    out[:28, 66:, 3] = 0
    out[:, :, 3] = keep_largest(out[:, :, 3], 0.02)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    unity_out = os.path.join(ROOT, "Assets/_Game/Art/Props/Draft")
    os.makedirs(unity_out, exist_ok=True)
    N = native()
    Image.fromarray(N).save(os.path.join(OUT, "_A_native_520x300.png"))
    manifest = {}
    for name, spec in OBJECTS.items():
        box, pivot_row = spec[0], spec[1]
        opts = spec[2] if len(spec) > 2 else {}
        rgba = extract(N, box, opts)
        ys, xs = np.nonzero(rgba[:, :, 3])
        if len(ys) == 0:
            print("EMPTY", name); continue
        print("%-28s %s opaque=%.2f" % (name, opts, len(ys) / rgba[:, :, 3].size))
        t, b, l, r = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        rgba = rgba[t:b, l:r]
        # even dimensions (ART_DIRECTION §3): pad right/top with transparent pixels
        h, w = rgba.shape[:2]
        rgba = np.pad(rgba, ((h % 2, 0), (0, w % 2), (0, 0)))
        h, w = rgba.shape[:2]
        pivot_y_from_bottom = (box[1] + b) - pivot_row  # rows between pivot and sprite bottom
        for e in opts.get("erase", []):
            erase(rgba, *e)
        if opts.get("post") == "blossom":
            rgba = fix_blossom(rgba)
        if opts.get("redraw") == "fence":
            rgba = fence(w, h, seed=w)
        Image.fromarray(rgba).save(os.path.join(OUT, name + ".png"))
        Image.fromarray(rgba).save(os.path.join(unity_out, name + ".png"))
        manifest[name] = {"w": int(w), "h": int(h), "pivot": [0.5, round(float(pivot_y_from_bottom) / h, 4)],
                          "pivot_px": [int(w // 2), int(pivot_y_from_bottom)], "source_box": list(box),
                          "native_pivot": [float(box[0] + l + w / 2.0), float(pivot_row)],
                          "status": "Needs Cleanup (draft)"}
    for d in (OUT, unity_out):
        with open(os.path.join(d, "extract_manifest.json"), "w") as f:
            json.dump(manifest, f, indent=2, ensure_ascii=False)
    # list form for Unity's JsonUtility (no dictionaries)
    items = [dict(name=k, w=v["w"], h=v["h"], pivotX=v["pivot_px"][0], pivotY=v["pivot_px"][1]) for k, v in manifest.items()]
    with open(os.path.join(unity_out, "extract_manifest_unity.json"), "w") as f:
        json.dump({"items": items}, f, indent=1)
    print(json.dumps({k: (v["w"], v["h"], v["pivot_px"]) for k, v in manifest.items()}))


if __name__ == "__main__":
    main()
