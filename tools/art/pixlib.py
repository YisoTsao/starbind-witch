"""Tiny numpy pixel-art drawing helpers shared by the draft generators.
Images are uint8 arrays shaped (H, W, 4); y grows downward (image space)."""
import math
import numpy as np


def c(h, a=255):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)] + [a], np.uint8)


def canvas(w, h):
    return np.zeros((h, w, 4), np.uint8)


def px(img, x, y, col):
    x, y = int(round(x)), int(round(y))
    if 0 <= y < img.shape[0] and 0 <= x < img.shape[1]:
        img[y, x] = col


def rect(img, x0, y0, x1, y1, col):
    """Inclusive rectangle."""
    H, W = img.shape[:2]
    x0, x1 = max(0, int(x0)), min(W - 1, int(x1))
    y0, y1 = max(0, int(y0)), min(H - 1, int(y1))
    if x0 <= x1 and y0 <= y1:
        img[y0:y1 + 1, x0:x1 + 1] = col


def ellipse(img, cx, cy, rx, ry, col, mask=None):
    H, W = img.shape[:2]
    ys, xs = np.mgrid[0:H, 0:W]
    m = ((xs + 0.5 - cx) / max(rx, 0.01)) ** 2 + ((ys + 0.5 - cy) / max(ry, 0.01)) ** 2 <= 1.0
    if mask is not None:
        m &= mask
    img[m] = col
    return m


def shaded_ellipse(img, cx, cy, rx, ry, ramp, light=(-0.6, -0.8)):
    """Fill with a 3+ tone ramp (light -> dark) by the surface normal against the light."""
    H, W = img.shape[:2]
    ys, xs = np.mgrid[0:H, 0:W]
    dx = (xs + 0.5 - cx) / max(rx, 0.01)
    dy = (ys + 0.5 - cy) / max(ry, 0.01)
    m = dx * dx + dy * dy <= 1.0
    d = -(dx * light[0] + dy * light[1])  # -1 lit .. 1 dark
    idx = np.clip(((d + 1) / 2 * len(ramp)).astype(int), 0, len(ramp) - 1)
    for i, col in enumerate(ramp):
        img[m & (idx == i)] = col
    return m


def poly(img, pts, col):
    """Filled polygon (even-odd) given [(x, y), ...] in pixel coords."""
    H, W = img.shape[:2]
    ys, xs = np.mgrid[0:H, 0:W]
    xs = xs + 0.5
    ys = ys + 0.5
    inside = np.zeros((H, W), bool)
    n = len(pts)
    for i in range(n):
        x1, y1 = pts[i]
        x2, y2 = pts[(i + 1) % n]
        cond = ((y1 > ys) != (y2 > ys))
        xint = (x2 - x1) * (ys - y1) / ((y2 - y1) if y2 != y1 else 1e-9) + x1
        inside ^= cond & (xs < xint)
    img[inside] = col
    return inside


def line(img, x0, y0, x1, y1, col, w=1):
    n = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
    for i in range(n + 1):
        t = i / max(n, 1)
        x = x0 + (x1 - x0) * t
        y = y0 + (y1 - y0) * t
        if w <= 1:
            px(img, x, y, col)
        else:
            rect(img, x - w // 2, y - w // 2, x + (w - 1) // 2, y + (w - 1) // 2, col)


def outline(img, col, diag=False):
    """Draw an outline into transparent pixels touching opaque ones."""
    a = img[:, :, 3] > 0
    n = np.zeros_like(a)
    n[1:, :] |= a[:-1, :]; n[:-1, :] |= a[1:, :]; n[:, 1:] |= a[:, :-1]; n[:, :-1] |= a[:, 1:]
    if diag:
        n[1:, 1:] |= a[:-1, :-1]; n[:-1, :-1] |= a[1:, 1:]; n[1:, :-1] |= a[:-1, 1:]; n[:-1, 1:] |= a[1:, :-1]
    img[n & ~a] = col
    return img


def blit(dst, src, x, y):
    """Alpha-over src onto dst with top-left at (x, y)."""
    h, w = src.shape[:2]
    H, W = dst.shape[:2]
    for yy in range(h):
        ty = y + yy
        if not 0 <= ty < H:
            continue
        for xx in range(w):
            tx = x + xx
            if 0 <= tx < W and src[yy, xx, 3] > 0:
                if src[yy, xx, 3] == 255 or dst[ty, tx, 3] == 0:
                    dst[ty, tx] = src[yy, xx]
                else:
                    a = src[yy, xx, 3] / 255.0
                    dst[ty, tx, :3] = (src[yy, xx, :3] * a + dst[ty, tx, :3] * (1 - a)).astype(np.uint8)
                    dst[ty, tx, 3] = max(dst[ty, tx, 3], src[yy, xx, 3])


def flip(img):
    return img[:, ::-1].copy()


def strip(frames):
    return np.concatenate(frames, axis=1)


def tint(img, col, k):
    out = img.copy()
    m = out[:, :, 3] > 0
    out[m, :3] = (out[m, :3] * (1 - k) + np.array(col[:3]) * k).astype(np.uint8)
    return out


def ring(img, cx, cy, r0, r1, col):
    H, W = img.shape[:2]
    ys, xs = np.mgrid[0:H, 0:W]
    d = np.sqrt((xs + 0.5 - cx) ** 2 + (ys + 0.5 - cy) ** 2)
    m = (d >= r0) & (d <= r1)
    img[m] = col
    return m


def rng(seed):
    return np.random.default_rng(seed)


def dither_alpha(img, alpha, seed=0):
    """Pixel-art friendly fade: drop pixels with a stable bayer pattern instead of partial alpha."""
    bayer = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
    H, W = img.shape[:2]
    b = np.tile(bayer, (H // 4 + 1, W // 4 + 1))[:H, :W]
    out = img.copy()
    out[b >= alpha] = 0
    return out


def polar(cx, cy, r, ang):
    return cx + math.cos(ang) * r, cy + math.sin(ang) * r
