"""Synthesize DRAFT sound effects (GDD §12.4) as 16-bit mono WAVs.
Status: placeholder (procedural, license-free, made here); listed in ASSET_GAPS.md.
Usage: python3 tools/audio/gen_sfx.py <repo_root>"""
import os, sys, wave
import numpy as np

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
OUT = os.path.join(ROOT, "Assets/_Game/Audio/SFX/Draft")
SR = 22050
rng = np.random.default_rng(7)


def env(n, a=0.005, r=None):
    t = np.arange(n) / SR
    r = r or (n / SR)
    e = np.minimum(1, t / a) * np.exp(-t / (r / 4))
    return e


def tone(f0, f1, dur, wave_="sine", vol=0.6, decay=None):
    n = int(SR * dur)
    f = np.linspace(f0, f1, n)
    ph = 2 * np.pi * np.cumsum(f) / SR
    if wave_ == "square":
        s = np.sign(np.sin(ph)) * 0.6
    elif wave_ == "saw":
        s = 2 * ((ph / (2 * np.pi)) % 1) - 1
    elif wave_ == "tri":
        s = 2 * np.abs(2 * ((ph / (2 * np.pi)) % 1) - 1) - 1
    else:
        s = np.sin(ph)
    return s * env(n, r=decay or dur) * vol


def noise(dur, vol=0.5, lp=0.3, decay=None):
    n = int(SR * dur)
    x = rng.uniform(-1, 1, n)
    y = np.zeros(n)
    acc = 0
    for i in range(n):
        acc += lp * (x[i] - acc)
        y[i] = acc
    y /= max(1e-6, np.abs(y).max())
    return y * env(n, r=decay or dur) * vol


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def cat(*parts):
    return np.concatenate(parts)


def save(name, s):
    s = np.clip(s, -1, 1)
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "w") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((s * 32000).astype(np.int16).tobytes())


S = {
    "sfx_staff": mix(noise(0.12, 0.5, 0.5), tone(500, 250, 0.1, "tri", 0.3)),
    "sfx_staff_heavy": mix(noise(0.25, 0.7, 0.25), tone(220, 80, 0.25, "square", 0.4)),
    "sfx_hit": mix(noise(0.06, 0.5, 0.6), tone(300, 150, 0.05, "square", 0.25)),
    "sfx_bolt": mix(tone(900, 400, 0.18, "tri", 0.45), noise(0.12, 0.2, 0.4)),
    "sfx_bolt_hit": mix(tone(700, 200, 0.15, "tri", 0.4), noise(0.1, 0.3, 0.5)),
    "sfx_charge": tone(300, 900, 0.35, "sine", 0.3),
    "sfx_fire_cast": mix(noise(0.25, 0.5, 0.15), tone(200, 400, 0.2, "saw", 0.2)),
    "sfx_fire_hit": mix(noise(0.35, 0.7, 0.12), tone(120, 60, 0.3, "square", 0.3)),
    "sfx_ice_cast": mix(tone(1600, 2400, 0.15, "tri", 0.3), tone(2400, 1800, 0.12, "sine", 0.2)),
    "sfx_ice_hit": mix(tone(2600, 1200, 0.12, "tri", 0.35), noise(0.08, 0.2, 0.8)),
    "sfx_thunder": mix(noise(0.5, 0.9, 0.35, 0.4), tone(90, 40, 0.4, "square", 0.4)),
    "sfx_wind": noise(0.8, 0.45, 0.06, 0.8),
    "sfx_tornado": mix(noise(1.0, 0.5, 0.08, 1.0), tone(150, 300, 1.0, "saw", 0.15)),
    "sfx_homing": tone(1200, 1500, 0.08, "sine", 0.25),
    "sfx_overdrive": cat(tone(400, 800, 0.2, "saw", 0.4), mix(tone(800, 1600, 0.6, "tri", 0.4), noise(0.6, 0.3, 0.2))),
    "sfx_overdrive_end": tone(800, 300, 0.4, "tri", 0.3),
    "sfx_potion": cat(tone(600, 900, 0.08, "sine", 0.3), tone(900, 1300, 0.12, "sine", 0.3)),
    "sfx_dash": noise(0.18, 0.4, 0.3),
    "sfx_player_hurt": mix(tone(400, 150, 0.2, "square", 0.45), noise(0.15, 0.4, 0.5)),
    "sfx_enemy_die": mix(tone(500, 100, 0.25, "square", 0.3), noise(0.2, 0.3, 0.3)),
    "sfx_boss_die": mix(noise(1.5, 0.8, 0.1, 1.5), tone(200, 30, 1.5, "saw", 0.4)),
    "sfx_spore": mix(noise(0.15, 0.3, 0.2), tone(300, 500, 0.1, "sine", 0.2)),
    "sfx_charge_enemy": mix(noise(0.4, 0.5, 0.15), tone(80, 160, 0.4, "square", 0.3)),
    "sfx_boss_roar": mix(tone(110, 70, 1.0, "saw", 0.5), noise(1.0, 0.4, 0.1, 1.0)),
    "sfx_summon": cat(tone(300, 600, 0.15, "tri", 0.3), tone(600, 300, 0.25, "tri", 0.3)),
    "sfx_xp": tone(1500, 2200, 0.05, "sine", 0.25),
    "sfx_pickup": cat(tone(800, 800, 0.05, "square", 0.25), tone(1200, 1200, 0.08, "square", 0.25)),
    "sfx_chest": cat(tone(500, 500, 0.08, "tri", 0.3), tone(700, 700, 0.08, "tri", 0.3), tone(1000, 1000, 0.2, "tri", 0.3)),
    "sfx_portal": mix(tone(400, 1200, 0.6, "sine", 0.3), noise(0.6, 0.15, 0.1)),
    "sfx_levelup": cat(tone(523, 523, 0.1, "square", 0.25), tone(659, 659, 0.1, "square", 0.25), tone(784, 784, 0.1, "square", 0.25), tone(1046, 1046, 0.3, "square", 0.25)),
    "sfx_steam": noise(0.6, 0.6, 0.5, 0.5),
    "sfx_overload": mix(noise(0.6, 0.9, 0.2, 0.5), tone(150, 50, 0.5, "square", 0.4)),
    "sfx_shatter": mix(tone(3000, 1500, 0.3, "tri", 0.35), noise(0.3, 0.4, 0.7), tone(100, 60, 0.3, "square", 0.25)),
}
for k, v in S.items():
    save(k, v)
print("wrote", len(S), "sfx to", OUT)
