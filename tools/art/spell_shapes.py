"""Original Starbind spell silhouettes. Image coordinates are y-down."""
import math

import numpy as np

from pixlib import c, ellipse, line, polar, poly, px, shaded_ellipse

FIRE = tuple(map(c, ("#641f3c", "#c53842", "#f66a32", "#ffab43", "#ffe37b", "#fff9d4")))
ICE = tuple(map(c, ("#233367", "#4164b6", "#53aad3", "#8ee4f5", "#c7f6ff", "#f6ffff")))
ARCANE = tuple(map(c, ("#442374", "#7641b9", "#ac63ed", "#d4a0ff", "#f4d4ff", "#fff7ff")))
WIND = tuple(map(c, ("#1c6069", "#319395", "#56c1ae", "#9bf0d3", "#c4fff2", "#f3fffd")))
VOLT = tuple(map(c, ("#382456", "#7745af", "#b47bf1", "#e6b8ff", "#fff1a2", "#fffdf0")))


def alpha(color, opacity):
    return (*color[:3], round(255 * max(0, min(1, opacity))))


def stroke(img, points, color, width=1):
    for a, b in zip(points, points[1:]):
        line(img, *a, *b, color, width)


def glint(img, x, y, r, ramp, tall=1.0):
    pts = [(x-r, y), (x-1, y-1), (x, y-r*tall), (x+1, y-1), (x+r, y),
           (x+1, y+1), (x, y+r*tall), (x-1, y+1)]
    poly(img, pts, ramp[3])
    line(img, x-r*0.6, y, x+r*0.6, y, ramp[4])
    line(img, x, y-r*tall*0.7, x, y+r*tall*0.7, ramp[5])


def ribbon(img, cx, cy, rx, ry, start, span, thickness, ramp, opacity=1, lean=0):
    """A broken, tapered band on the ground or wrapped around a column."""
    count = max(8, int(abs(span) * rx * 1.3))
    for i in range(count + 1):
        q = i / count
        a = start + span * q
        x, y = cx + rx * math.cos(a), cy + ry * math.sin(a) + lean * q
        weight = math.sin(math.pi * q) ** 0.7
        thick = 1 + int(thickness * weight)
        back = math.sin(a) < 0
        line(img, x, y, x, y + thick - 1, alpha(ramp[1 if back else 2], opacity * (0.4 if back else 0.8)))
        if q > 0.18 and q < 0.92:
            px(img, x, y, alpha(ramp[3 if back else 4], opacity * (0.66 if back else 1)))
        if 0.48 < q < 0.69 and not back:
            px(img, x, y, alpha(ramp[5], opacity))


def fireball(t, i, img):
    spin = i * math.tau / 8
    rr = np.random.default_rng(22)
    for j in range(6):
        x = 7 + j * 2.5 + math.sin(spin + j) * 2
        y = 18 + rr.uniform(-7, 7) + math.sin(spin + j * 1.2) * 2
        line(img, x-2, y, x, y, FIRE[1+j % 3])
    wobble = math.sin(spin) * 2
    poly(img, [(4, 21+wobble), (15, 19), (9, 12), (20, 14), (25, 9), (34, 10),
               (39, 15), (41, 19), (37, 24), (32, 27), (21, 25), (10, 28), (17, 22)], FIRE[0])
    poly(img, [(8, 21+wobble), (23, 15), (17, 12+wobble), (30, 12), (37, 13), (40, 18),
               (36, 23), (30, 25), (18, 23), (13, 26), (22, 20)], FIRE[2])
    poly(img, [(17, 20), (28, 13), (36, 14), (39, 18), (35, 22), (30, 23), (22, 21+wobble)], FIRE[3])
    poly(img, [(24, 18), (30, 14), (36, 15), (38, 18), (34, 21), (27, 20)], FIRE[4])
    poly(img, [(29, 17), (33, 15), (37, 17), (36, 19), (32, 20)], FIRE[5])
    return img


def ice_shard(t, i, img):
    x = i % 3
    poly(img, [(7, 18), (16, 11), (27, 9), (49, 17), (30, 25), (14, 24)], ICE[0])
    poly(img, [(10, 18), (28, 10), (47, 17), (26, 17)], ICE[3])
    poly(img, [(10, 18), (26, 17), (47, 17), (29, 24), (15, 23)], ICE[1])
    poly(img, [(19, 17), (28, 11), (47, 17), (32, 18)], ICE[4])
    stroke(img, [(28, 11), (47, 17), (25+x*3, 17)], ICE[5])
    stroke(img, [(14, 22), (26, 17), (31, 22)], ICE[2])
    line(img, 4+x, 12, 12+x, 12, alpha(ICE[3], 0.8))
    line(img, 3-x, 25, 7, 24, ICE[2])
    if i % 4 == 1:
        glint(img, 30, 12, 3, ICE)
    return img


def orbit_blade(t, i, img):
    poly(img, [(6, 24), (19, 12), (36, 9), (44, 12), (33, 16), (24, 24), (16, 30)], ICE[0])
    poly(img, [(7, 24), (20, 13), (35, 10), (42, 12), (26, 17), (15, 27)], ICE[3])
    poly(img, [(15, 27), (26, 17), (42, 12), (30, 19), (19, 29)], ICE[1])
    stroke(img, [(9, 23), (21, 14), (35, 11), (41, 12)], ICE[5])
    stroke(img, [(15, 27), (22, 24), (26, 17)], ICE[2])
    return img


def star_projectile(ramp, style):
    def draw(t, i, img):
        ph = i * math.tau / 12
        cx, cy = 26, 20
        if style == "charge":
            poly(img, [(4, 20), (20, 11), (30, 5), (53, 20), (30, 35), (20, 29)], ramp[0])
            poly(img, [(7, 20), (24, 13), (30, 8), (50, 20), (30, 32), (24, 26)], ramp[2])
            poly(img, [(18, 20), (32, 11), (48, 20), (32, 28)], ramp[4])
            poly(img, [(24, 20), (33, 14), (46, 20), (33, 24)], ramp[5])
            ribbon(img, 25, 20, 14, 8, ph, 2.5, 1, ramp, 0.7)
        else:
            for j in range(2):
                ribbon(img, cx, cy, 14+j*3, 7+j*5, ph+j*math.pi, 2.6, 1, ramp, 0.86)
            if style == "diamond":
                poly(img, [(16, cy), (cx, 10), (37, cy), (cx, 29)], ramp[1])
                poly(img, [(18, cy), (cx, 11), (35, cy), (cx, 22)], ramp[3])
                line(img, 20, cy, cx, 12, ramp[5])
                glint(img, cx, cy, 5, ramp, 1.05)
            else:
                glint(img, cx, cy, 11 + math.sin(ph)*2, ramp, 1.03)
            for j in range(3):
                a = ph + j * math.tau / 3
                px(img, cx + math.cos(a)*19, cy + math.sin(a)*14, ramp[3+j % 2])
        return img
    return draw


def sigil(ramp, charged=False):
    def draw(t, i, img):
        cx, cy = img.shape[1] / 2, img.shape[0] / 2
        r = cx - 7
        ph = i * math.tau / 12
        for j in range(4):
            a = ph + j * math.pi / 2
            ribbon(img, cx, cy, r, r*0.49, a, 1.25, 2, ramp, 0.66)
            ribbon(img, cx, cy, r*0.76, r*0.35, -a, 1.12, 1, ramp, 0.55)
            x, y = cx + r * math.cos(a), cy + r*0.49 * math.sin(a)
            glint(img, x, y, 3 if charged else 4, ramp, 0.8)
        points = [(cx+r*0.52*math.cos(ph+j*math.pi/2), cy+r*0.29*math.sin(ph+j*math.pi/2)) for j in range(5)]
        stroke(img, points, alpha(ramp[3], 0.55))
        if charged:
            for j in range(6):
                a = j * math.tau/6 - ph
                x, y = cx + r*0.58 * math.cos(a), cy + r*0.42 * math.sin(a)
                px(img, x, y, ramp[4])
        return img
    return draw


def wind_ground(t, i, img):
    ph = i * math.tau / 16
    for j in range(5):
        a = ph + j * math.tau / 5
        ribbon(img, 48, 29, 42, 26, a, 0.94, 1, WIND, 0.8)
        ribbon(img, 48, 29, 29, 18, a+0.46, 0.8, 2, WIND, 0.50)
        px(img, 48 + 38 * math.cos(a+1.03), 29 + 24 * math.sin(a+1.03), WIND[3])
    return img


def funnel(ramp, fire):
    def draw(t, i, img):
        ph = i * math.tau / 16
        cx, ground = img.shape[1] / 2, img.shape[0] - 15
        height = 73 if fire else 57
        # Translucent body under the crisp spiral edges. The noisy edge and lit side
        # move with the turns, leaving room to see through the wind.
        ys, xs = np.mgrid[0:img.shape[0], 0:img.shape[1]]
        lift = np.clip((ground - ys) / height, 0, 1)
        spine = cx + np.sin(lift*5-ph) * lift * 9
        width = (8 if fire else 6) + lift**1.25 * (30 if fire else 35)
        side = (xs - spine) / width
        shell = np.clip(1 - np.abs(side), 0, 1)
        term = np.sin(lift*29 + side*3.6 - ph*2) * 0.5 + 0.5
        vertical_fade = np.sin(np.pi * lift)**0.55
        opacity = (shell**0.9) * vertical_fade * (0.3 + 0.7 * term)
        img[:, :, :3] = (ramp[1 if not fire else 0][:3] + (
            ramp[3][:3].astype(float) - ramp[1 if not fire else 0][:3]) * term[..., None])
        img[:, :, 3] = np.rint(opacity * (140 if fire else 91)).astype(np.uint8)
        for j in range(5):
            ribbon(img, cx, ground, cx-9, 9, ph+j*math.tau/5, 0.95, 2, ramp, 0.75)
        for j in range(7):
            level = j / 7
            y = ground - level * height
            radius = (10 if fire else 7) + level**1.25 * (27 if fire else 30)
            bend = math.sin(level*5-ph) * level * 9
            for k in range(2):
                a = ph + k*math.pi + level*5.4
                ribbon(img, cx+bend, y, radius, 4+level*5, a, 2.35, (6 if fire else 3)+level, ramp,
                       (1.0 if fire else 0.68) - level * 0.27, -height/8)
        rr = np.random.default_rng(82)
        for j in range(30):
            level = ((j / 30) + i / 16) % 1
            a = rr.uniform(0, math.tau) + ph
            rad = (13 + (25 if fire else 32)*level) * rr.uniform(0.78, 1.21)
            x = cx + math.cos(a) * rad + math.sin(level*5-ph) * level * 9
            y = ground - level * (height+9) + math.sin(a)*6
            line(img, x, y, x+math.sin(a)*3, y+(3 if fire else 1),
                 alpha(ramp[2+j%4], math.sin(math.pi * level) ** 0.55))
            if j % 5 == 0 and 0.12 < level < 0.91:
                px(img, x-1, y-1, alpha(ramp[5], math.sin(math.pi * level)))
        return img
    return draw


def staff_slash(t, i, img):
    if i == 8:
        return img
    cx, cy = 31, 40
    r = 21 + 4 * i
    span = 1.05 + min(i, 3) * 0.30
    for j in range(3):
        for step in range(42):
            q = step / 41
            a = -span * (1-q) + 0.45 + i*0.14 - j*0.12
            thick = math.sin(math.pi*q)**1.2 * max(1, 5-i*0.65-j)
            x, y = polar(cx, cy, r-j*4, a)
            x2, y2 = polar(cx, cy, r-j*4-thick, a)
            line(img, x, y, x2, y2, alpha(ARCANE[5 if j == 0 and q > 0.25 else 3-j], (1-t)*(1-j*0.25)))
    return img


def arc_burst(ramp, mode):
    def draw(t, i, img):
        if t == 1:
            return img
        cx, cy = img.shape[1]/2, img.shape[0]*0.67
        rr = np.random.default_rng(28)
        falloff = (1-t)**0.8
        r = 6 + (cx-13) * (1-(1-t)**2)
        if mode == "steam":
            for j in range(15):
                a, v = rr.uniform(0, math.tau), rr.uniform(0.5, 1.2)
                x, y = polar(cx, cy, r*v*0.8, a)
                y = cy + (y-cy)*0.36 - t * rr.uniform(16, 44)
                rad = (3+11*math.sin(t*math.pi*0.86)) * rr.uniform(0.45, 1)
                tones = ("#effaff", "#abb4d2", "#667994") if j % 3 else ("#fff3dd", "#c1a8bd", "#817087")
                shaded_ellipse(img, x, y, rad*1.1, rad*0.65, [alpha(c(v), falloff*a) for v, a in zip(tones, (0.72, 0.47, 0.34))])
        if mode == "overload":
            for j in range(6):
                a = j * math.tau / 6 + t * 0.7
                ribbon(img, cx, cy, r*0.76, r*0.43, a, 0.65, 4*falloff, FIRE, falloff, -12*falloff)
        for j in range(14):
            a = j*math.tau/14 + rr.uniform(-0.25, 0.25)
            x, y = polar(cx, cy, r * rr.uniform(0.60, 1), a)
            y = cy + (y-cy)*0.46 - t * rr.uniform(4, 17)
            dx, dy = math.cos(a), math.sin(a)
            if mode == "shards":
                d = rr.uniform(3, 9) * falloff
                poly(img, [(x+dx*d, y+dy*d), (x-dy*3*falloff, y+dx*3*falloff),
                           (x-dx*d*0.5, y-dy*d*0.5), (x+dy*2, y-dx*2)], alpha(ramp[2+j % 3], falloff))
                line(img, x, y, x+dx*d, y+dy*d, alpha(ramp[5], falloff))
            elif mode != "steam" and j % 2 == 0:
                p0 = (cx+dx*r*0.25, cy+dy*r*0.18)
                pts = [p0, (x-dx*5-dy*4, y-dy*3+dx*4), (x, y), (x+dx*8, y+dy*5)]
                stroke(img, pts, alpha(ramp[1], falloff), 3 if i < 4 else 1)
                stroke(img, pts, alpha(ramp[4 if j % 3 else 5], falloff))
            else:
                line(img, x-dx*4, y-dy*3, x, y, alpha(ramp[3+j % 2], falloff))
        if i < 4:
            glint(img, cx, cy, 11-i*2, ramp, 1.1)
        return img
    return draw
