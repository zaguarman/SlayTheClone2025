"""Generates the card illustrations as SVG files.

Output: Assets/Art/Cards/<Theme>/<CardAssetName>.svg (one per card asset in Assets/Scriptables/Cards)
and Assets/Art/Icons/EffectIcons.svg (the icon sheet used inside card text), and the Fog of War card faces in
Assets/Resources/FogOfWar/ (a creature you don't know, and the back of a card in the enemy's hand).
After running, use the Unity menus "Cards > Assign Card Art" and "Cards > Build Effect Icons".

Usage: python Tools/generate_card_art.py
"""
import math
import os

W, H = 352, 200  # Matches the card art box aspect ratio (144 x 82)
OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "Art", "Cards")
# Pixel art is paused (2026-10-14): every card shows its vector art again. Set USE_PIXEL_ART to True to go back: a card
# with pixel art (Tools/pixel_art.py) then shows that instead, and its vector art is written to the archive, outside Assets
USE_PIXEL_ART = False
PIXEL_DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "Art", "PixelCards")
ARCHIVE_DIR = os.path.join(os.path.dirname(__file__), "..", "ArtArchive", "Cards")


# ---------------------------------------------------------------- SVG helpers

def svg(defs, body):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}">'
            f'<defs>{defs}</defs>{body}</svg>\n')


def stops(colors):
    out = []
    for i, c in enumerate(colors):
        color, opacity = (c if isinstance(c, tuple) else (c, 1))
        offset = i / (len(colors) - 1) if len(colors) > 1 else 0
        out.append(f'<stop offset="{offset:.2f}" stop-color="{color}" stop-opacity="{opacity}"/>')
    return "".join(out)


def lin(gid, colors, x1=0, y1=0, x2=0, y2=1):
    return f'<linearGradient id="{gid}" x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}">{stops(colors)}</linearGradient>'


def rad(gid, colors, cx=0.5, cy=0.5, r=0.5):
    return f'<radialGradient id="{gid}" cx="{cx}" cy="{cy}" r="{r}">{stops(colors)}</radialGradient>'


def circle(cx, cy, r, fill, extra=""):
    return f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="{r:.1f}" fill="{fill}" {extra}/>'


def ellipse(cx, cy, rx, ry, fill, extra=""):
    return f'<ellipse cx="{cx:.1f}" cy="{cy:.1f}" rx="{rx:.1f}" ry="{ry:.1f}" fill="{fill}" {extra}/>'


def path(d, fill="none", extra=""):
    return f'<path d="{d}" fill="{fill}" {extra}/>'


def stroke(d, color, width, extra=""):
    return (f'<path d="{d}" fill="none" stroke="{color}" stroke-width="{width}" '
            f'stroke-linecap="round" stroke-linejoin="round" {extra}/>')


def poly(points, fill, extra=""):
    pts = " ".join(f"{x:.1f},{y:.1f}" for x, y in points)
    return f'<polygon points="{pts}" fill="{fill}" {extra}/>'


def rect(x, y, w, h, fill, extra=""):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill}" {extra}/>'


def group(content, transform, opacity=1):
    return f'<g transform="{transform}" opacity="{opacity}">{content}</g>'


def eye(cx, cy, r, iris="#111", glint=True):
    out = circle(cx, cy, r, "#fff") + circle(cx + r * 0.15, cy + r * 0.1, r * 0.6, iris)
    if glint:
        out += circle(cx + r * 0.35, cy - r * 0.25, r * 0.22, "#fff")
    return out


def star_points(cx, cy, r_out, r_in, n=5, rot=-90):
    pts = []
    for i in range(n * 2):
        r = r_out if i % 2 == 0 else r_in
        a = math.radians(rot + i * 180 / n)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def bolt(x, y, s, color="#ffe45c"):
    pts = [(0, 0), (10, 0), (5, 14), (13, 14), (-2, 36), (2, 19), (-6, 19)]
    return poly([(x + px * s, y + py * s) for px, py in pts], color, 'stroke="#fff6b0" stroke-width="1"')


def plus(cx, cy, s, color):
    return (rect(cx - s * 0.3, cy - s, s * 0.6, s * 2, color, 'rx="2"')
            + rect(cx - s, cy - s * 0.3, s * 2, s * 0.6, color, 'rx="2"'))


def bubbles(seed, count=9, color="#bfe8ff"):
    out = []
    for i in range(count):
        x = (seed * 37 + i * 71) % W
        y = 20 + (seed * 53 + i * 47) % (H - 60)
        r = 1.5 + (i * 7 + seed) % 4
        out.append(circle(x, y, r, "none", f'stroke="{color}" stroke-width="1" opacity="0.55"'))
    return "".join(out)


# ---------------------------------------------------------------- Backgrounds

def bg_water(top="#2f8fd0", bottom="#08213d"):
    defs = lin("bg", [top, bottom]) + lin("ray", [("#ffffff", 0.22), ("#ffffff", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    for x0, w in ((40, 40), (140, 26), (230, 48), (310, 22)):
        body += poly([(x0, 0), (x0 + w, 0), (x0 + w * 2.2, H), (x0 + w * 0.6, H)], "url(#ray)")
    body += path(f"M0 {H} L0 172 C 60 160 110 178 170 168 C 230 158 290 176 {W} 164 L{W} {H} Z", "#0b2f45")
    body += path(f"M0 {H} L0 186 C 80 178 140 192 210 184 C 270 178 320 190 {W} 182 L{W} {H} Z", "#06202f")
    return defs, body


def bg_deep():
    defs = lin("bg", ["#0d2a52", "#02060f"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(24):
        body += circle((i * 97) % W, (i * 61) % H, 0.8 + i % 3 * 0.5, "#7fd6ff", f'opacity="{0.15 + (i % 4) * 0.08:.2f}"')
    return defs, body


def bg_sky(top="#86c9f2", bottom="#fbe2b0", sun=True):
    defs = lin("bg", [top, bottom]) + rad("sunGlow", [("#fff5c8", 0.8), ("#fff5c8", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    if sun:
        body += circle(70, 150, 60, "url(#sunGlow)") + circle(70, 150, 24, "#fff1b8")
    body += poly([(0, 200), (0, 150), (60, 118), (120, 152), (190, 108), (260, 150), (320, 122), (352, 140), (352, 200)], "#8fa9c8", 'opacity="0.55"')
    for cx, cy, s in ((60, 40, 1.0), (250, 28, 0.8), (320, 70, 0.6)):
        body += cloud(cx, cy, s)
    return defs, body


def cloud(cx, cy, s, color="#ffffff", opacity=0.9):
    puffs = ellipse(0, 0, 26, 12, color) + ellipse(-18, 4, 16, 10, color) + ellipse(18, 4, 18, 10, color) + ellipse(4, -8, 14, 10, color)
    return group(puffs, f"translate({cx} {cy}) scale({s})", opacity)


# ---------------------------------------------------------------- Shared creatures

def octopus(cx, cy, s, color, dark, tentacles=8):
    out = ""
    for i in range(tentacles):
        a = -170 + i * (160 / (tentacles - 1))
        r = math.radians(a)
        x1, y1 = cx + 20 * s * math.cos(r), cy + 18 * s
        x2 = cx + 70 * s * math.cos(r)
        y2 = cy + 34 * s + 30 * s * abs(math.sin(r))
        x3 = x2 + 18 * s * math.cos(r + 1.4)
        y3 = y2 - 18 * s
        out += stroke(f"M{x1:.1f} {y1:.1f} C {x1 + (x2 - x1) * 0.4:.1f} {y2:.1f} {x2:.1f} {y2 + 8 * s:.1f} {x3:.1f} {y3:.1f}", color, 9 * s)
        for t in (0.4, 0.7):
            out += circle(x1 + (x2 - x1) * t, y1 + (y2 - y1) * t + 4 * s, 1.6 * s, dark)
    out += path(f"M{cx - 34 * s:.1f} {cy + 22 * s:.1f} C {cx - 40 * s:.1f} {cy - 36 * s:.1f} {cx + 40 * s:.1f} {cy - 36 * s:.1f} {cx + 34 * s:.1f} {cy + 22 * s:.1f} Z", color)
    out += ellipse(cx - 12 * s, cy - 12 * s, 9 * s, 6 * s, "#ffffff", 'opacity="0.18"')
    out += eye(cx - 12 * s, cy + 6 * s, 6 * s) + eye(cx + 12 * s, cy + 6 * s, 6 * s)
    return out


def jellyfish(cx, cy, s, color, glow):
    out = circle(cx, cy, 30 * s, glow)
    for i in range(5):
        x = cx - 16 * s + i * 8 * s
        out += stroke(f"M{x:.1f} {cy + 4 * s:.1f} c {-6 * s:.1f} {12 * s:.1f} {6 * s:.1f} {20 * s:.1f} 0 {34 * s:.1f} s {6 * s:.1f} {16 * s:.1f} 0 {24 * s:.1f}", color, 1.8 * s, 'opacity="0.85"')
    out += path(f"M{cx - 22 * s:.1f} {cy + 6 * s:.1f} C {cx - 24 * s:.1f} {cy - 26 * s:.1f} {cx + 24 * s:.1f} {cy - 26 * s:.1f} {cx + 22 * s:.1f} {cy + 6 * s:.1f} "
                f"Q {cx + 16 * s:.1f} {cy + 2 * s:.1f} {cx + 11 * s:.1f} {cy + 7 * s:.1f} Q {cx + 5 * s:.1f} {cy + 2 * s:.1f} {cx:.1f} {cy + 7 * s:.1f} "
                f"Q {cx - 5 * s:.1f} {cy + 2 * s:.1f} {cx - 11 * s:.1f} {cy + 7 * s:.1f} Q {cx - 16 * s:.1f} {cy + 2 * s:.1f} {cx - 22 * s:.1f} {cy + 6 * s:.1f} Z",
                color, 'opacity="0.9"')
    out += ellipse(cx - 7 * s, cy - 8 * s, 7 * s, 4 * s, "#ffffff", 'opacity="0.35"')
    return out


def wave(x0, y0, s, color, foam="#e8f8ff"):
    d = (f"M{x0:.1f} {y0 + 80 * s:.1f} C {x0 + 30 * s:.1f} {y0 + 20 * s:.1f} {x0 + 90 * s:.1f} {y0 - 20 * s:.1f} {x0 + 150 * s:.1f} {y0:.1f} "
         f"C {x0 + 190 * s:.1f} {y0 + 14 * s:.1f} {x0 + 196 * s:.1f} {y0 + 52 * s:.1f} {x0 + 160 * s:.1f} {y0 + 58 * s:.1f} "
         f"C {x0 + 140 * s:.1f} {y0 + 60 * s:.1f} {x0 + 130 * s:.1f} {y0 + 40 * s:.1f} {x0 + 146 * s:.1f} {y0 + 32 * s:.1f} "
         f"C {x0 + 120 * s:.1f} {y0 + 30 * s:.1f} {x0 + 90 * s:.1f} {y0 + 60 * s:.1f} {x0 + 80 * s:.1f} {y0 + 110 * s:.1f} Z")
    out = path(d, color)
    out += stroke(f"M{x0 + 60 * s:.1f} {y0 + 22 * s:.1f} C {x0 + 100 * s:.1f} {y0 - 8 * s:.1f} {x0 + 160 * s:.1f} {y0 - 4 * s:.1f} {x0 + 176 * s:.1f} {y0 + 28 * s:.1f}", foam, 6 * s)
    for i in range(6):
        out += circle(x0 + (150 + i * 9) * s, y0 + (4 + (i % 3) * 7) * s, (3 + i % 2) * s, foam)
    return out


# ---------------------------------------------------------------- Water cards

def abyssal_call():
    defs, body = bg_deep()
    defs += rad("vortex", [("#8b5cf6", 0.9), ("#3b1d7a", 0.6), ("#02060f", 0)])
    body += ellipse(176, 150, 150, 46, "url(#vortex)")
    for i in range(5):
        body += stroke(f"M{176 - 130 + i * 12} {150 + i * 2} A {130 - i * 24} {38 - i * 6} 0 0 1 {176 + 130 - i * 12} {150 - i * 2}", "#b9a3ff", 1.5, f'opacity="{0.25 + i * 0.12:.2f}"')
    for side, x in ((-1, 120), (1, 232)):
        body += stroke(f"M{x} 160 C {x + side * 30} 120 {x - side * 20} 80 {x + side * 20} 40 q {side * 10} -10 {side * 4} -22", "#4a2a7a", 12)
        body += stroke(f"M{x} 160 C {x + side * 30} 120 {x - side * 20} 80 {x + side * 20} 40", "#7b52c4", 3, 'opacity="0.6"')
    body += ellipse(176, 108, 40, 24, "#d9c8ff", 'opacity="0.2"')
    body += ellipse(176, 108, 26, 15, "#f7f1ff") + ellipse(176, 108, 9, 14, "#7c3aed") + ellipse(176, 108, 3, 10, "#120526")
    return svg(defs, body)


def abyssal_cucumber():
    defs, body = bg_water("#2a7fb8", "#0a2a40")
    defs += lin("cuc", ["#d1683f", "#7a2c1a"])
    body += path("M70 150 C 90 108 170 100 230 112 C 280 120 300 140 286 156 C 270 172 200 170 140 168 C 100 167 64 166 70 150 Z", "url(#cuc)")
    for i in range(11):
        x = 92 + i * 18
        y = 118 + abs(5 - i) * 2
        body += circle(x, y, 5, "#f0a070") + circle(x, y, 2.2, "#ffd2a8")
    for i in range(6):
        body += stroke(f"M{286} {146 + i * 2} q {14} {-14 + i * 6} {26} {-6 + i * 5}", "#f6c29a", 2)
    body += circle(268, 132, 4, "#1a0a05")
    body += bubbles(3)
    return svg(defs, body)


def coral_builder():
    defs, body = bg_water()
    for x, color, h in ((70, "#ff7b8a", 90), (120, "#ffa65c", 70), (250, "#ff6f9e", 100), (300, "#f7c45a", 64)):
        body += stroke(f"M{x} 176 L{x} {176 - h}", color, 9)
        body += stroke(f"M{x} {176 - h * 0.45} q -22 -10 -26 -36", color, 7)
        body += stroke(f"M{x} {176 - h * 0.65} q 20 -8 24 -30", color, 7)
        body += circle(x, 176 - h, 6, color) + circle(x - 26, 176 - h * 0.45 - 36, 5, color) + circle(x + 24, 176 - h * 0.65 - 30, 5, color)
    # Crab carrying a coral block
    body += rect(150, 92, 52, 26, "#ff9aa8", 'rx="5" stroke="#d65f74" stroke-width="2"')
    for i in range(4):
        body += circle(160 + i * 11, 104, 3, "#ffd3da")
    for side in (-1, 1):
        for i in range(3):
            body += stroke(f"M{176 + side * 22} {150 + i * 5} l {side * 18} {6 + i * 3} l {side * 6} {10}", "#c2412b", 4)
        body += stroke(f"M{176 + side * 26} 140 q {side * 16} -16 {side * 6} -30", "#c2412b", 6)
        body += circle(176 + side * 22, 108, 9, "#e2553a")
    body += ellipse(176, 150, 32, 18, "#e2553a")
    body += ellipse(170, 144, 14, 6, "#ffffff", 'opacity="0.2"')
    body += stroke("M168 134 l -3 -10 M184 134 l 3 -10", "#c2412b", 3) + eye(165, 122, 4) + eye(187, 122, 4)
    body += bubbles(5)
    return svg(defs, body)


def coral_shield():
    defs, body = bg_water("#3aa0d8", "#0d3557")
    defs += lin("shield", ["#ffb3a7", "#e0566f"]) + rad("aura", [("#9ff3ff", 0.55), ("#9ff3ff", 0)])
    body += ellipse(176, 100, 110, 90, "url(#aura)")
    body += path("M176 22 C 210 34 240 34 250 40 C 250 110 226 152 176 182 C 126 152 102 110 102 40 C 112 34 142 34 176 22 Z", "url(#shield)", 'stroke="#fff1e8" stroke-width="5"')
    for x, h in ((150, 70), (176, 90), (202, 66)):
        body += stroke(f"M{x} 160 L{x} {160 - h}", "#fff1e8", 5, 'opacity="0.9"')
        body += stroke(f"M{x} {160 - h * 0.5} q -12 -6 -14 -22 M{x} {160 - h * 0.7} q 12 -6 14 -20", "#fff1e8", 4, 'opacity="0.9"')
    body += bubbles(8, 12, "#e6fbff")
    return svg(defs, body)


def crashing_wave():
    defs, body = bg_water("#4bb2e6", "#0e3f6b")
    defs += lin("wave", ["#5fd0ff", "#1166a8"])
    body += wave(70, 40, 1.15, "url(#wave)")
    body += eye(210, 92, 7, "#0b3d66") + eye(236, 88, 7, "#0b3d66")
    body += stroke("M204 80 l 12 4 M230 76 l 12 -2", "#0b3d66", 3)
    for i in range(10):
        body += circle(40 + i * 30, 168 - (i % 3) * 6, 4 + i % 3, "#e8f8ff", 'opacity="0.8"')
    return svg(defs, body)


def dancing_dolphin():
    defs, body = bg_water("#46b5ea", "#0b3a66")
    defs += lin("dol", ["#9fc8e6", "#4f7fa8"])
    body += stroke("M30 150 C 90 138 140 162 200 150 C 260 138 300 160 340 150", "#e8f8ff", 3, 'opacity="0.7"')
    dolphin = (
        path("M60 122 C 100 70 200 50 262 82 C 280 90 296 92 312 88 C 302 100 286 104 270 104 C 236 124 160 132 112 124 L 82 146 L 88 122 L 52 112 Z", "url(#dol)")
        + path("M120 120 C 160 128 220 120 262 100 C 230 128 170 134 120 120 Z", "#e9f4fb")
        + path("M178 66 C 184 44 196 36 208 34 C 202 46 202 58 206 70 Z", "#5d8db6")
        + path("M196 108 C 204 122 200 132 188 140 C 186 128 186 118 190 108 Z", "#5d8db6")
        + eye(268, 86, 4)
        + stroke("M276 96 q 14 2 30 -4", "#36597a", 2))
    body += group(dolphin, "rotate(-10 176 100)")
    for i in range(8):
        body += circle(70 + i * 9, 150 - (i % 3) * 8, 3, "#e8f8ff")
    return svg(defs, body)


def deep_sea_angler():
    defs, body = bg_deep()
    defs += rad("lure", [("#fffbc2", 1), ("#ffe45c", 0.7), ("#ffe45c", 0)])
    body += circle(250, 44, 40, "url(#lure)") + circle(250, 44, 8, "#fffbc2")
    body += stroke("M170 78 C 190 30 230 26 250 44", "#3d5a7a", 4)
    body += path("M70 110 C 80 60 190 50 230 96 C 250 120 240 150 210 160 C 170 172 120 168 92 150 L 50 170 L 60 130 L 40 100 Z", "#2c3e5a")
    body += path("M120 118 C 160 124 200 120 236 110 C 228 140 200 150 160 150 C 140 150 126 140 120 118 Z", "#0a0f1a")
    body += poly([(126, 120), (134, 134), (142, 120), (150, 134), (158, 120), (166, 134), (174, 120), (182, 134), (190, 120), (198, 134), (206, 118)], "#f5f0e0")
    body += poly([(136, 150), (144, 138), (152, 150), (160, 138), (168, 150), (176, 138), (184, 150)], "#f5f0e0")
    body += eye(200, 92, 8, "#1b2a40")
    body += path("M96 96 C 110 80 130 80 144 88", "none", 'stroke="#4d6b90" stroke-width="3"')
    return svg(defs, body)


def electric_eel():
    defs, body = bg_water("#1f6a9e", "#06192d")
    defs += lin("eel", ["#c9e86a", "#5f8f2c"], 0, 0, 1, 0)
    body += bolt(40, 30, 1.6) + bolt(300, 26, 1.3) + bolt(250, 120, 1.1) + bolt(90, 120, 1.0)
    body += stroke("M30 160 C 80 90 120 180 170 112 S 250 60 296 92", "#3d6b1f", 22)
    body += stroke("M30 160 C 80 90 120 180 170 112 S 250 60 296 92", "url(#eel)", 16)
    for i in range(7):
        body += circle(60 + i * 32, 130 - (i % 2) * 18, 2.5, "#f5ff9e")
    body += ellipse(300, 92, 22, 15, "#7fab3a", 'transform="rotate(25 300 92)"')
    body += eye(306, 86, 5, "#203a10") + stroke("M300 102 q 12 4 20 -2", "#203a10", 2)
    return svg(defs, body)


def guardian_whale():
    defs, body = bg_water("#3b8fc8", "#0a2643")
    defs += lin("whale", ["#5b7fa6", "#2c4767"]) + rad("aura", [("#bff3ff", 0.35), ("#bff3ff", 0)])
    body += ellipse(176, 104, 170, 90, "url(#aura)")
    body += path("M40 110 C 50 60 170 40 250 64 C 290 76 310 100 300 124 C 290 146 240 156 180 154 C 130 152 90 144 64 130 L 26 156 L 30 116 L 6 92 Z", "url(#whale)")
    body += path("M120 140 C 170 152 240 148 290 128 C 270 152 220 158 170 154 C 150 152 132 148 120 140 Z", "#c9d9e8")
    for i in range(5):
        body += stroke(f"M{170 + i * 18} {146 - i} l 8 8", "#93a9c0", 2)
    body += eye(258, 100, 6, "#18283c")
    body += path("M190 120 C 204 136 200 150 186 160 C 182 146 182 132 190 120 Z", "#466489")
    return svg(defs, body)


def healing_rain():
    defs, body = bg_sky("#9ad3f0", "#d8f3e8", sun=False)
    defs += lin("drop", ["#c8f7ff", "#3fb6e0"])
    for cx, cy, s in ((110, 46, 1.6), (220, 40, 1.8), (170, 56, 1.4)):
        body += cloud(cx, cy, s, "#f4f9ff", 0.97)
    for i in range(14):
        x = 70 + (i * 23) % 220
        y = 92 + (i * 31) % 80
        body += path(f"M{x} {y} C {x - 6} {y + 10} {x - 6} {y + 16} {x} {y + 18} C {x + 6} {y + 16} {x + 6} {y + 10} {x} {y} Z", "url(#drop)")
    body += plus(80, 150, 10, "#5fd38a") + plus(280, 130, 8, "#5fd38a") + plus(176, 172, 7, "#5fd38a")
    return svg(defs, body)


def jellyfish_swarm():
    defs, body = bg_deep()
    defs += rad("glowA", [("#ff8ae2", 0.45), ("#ff8ae2", 0)]) + rad("glowB", [("#9be8ff", 0.45), ("#9be8ff", 0)])
    body += jellyfish(110, 70, 1.3, "#ff9fe5", "url(#glowA)")
    body += jellyfish(250, 60, 1.0, "#a7ecff", "url(#glowB)")
    body += jellyfish(190, 120, 0.85, "#d3a6ff", "url(#glowA)")
    body += jellyfish(300, 140, 0.6, "#ff9fe5", "url(#glowA)")
    body += bolt(160, 40, 0.6, "#fff6a8") + bolt(60, 130, 0.5, "#fff6a8")
    return svg(defs, body)


def kraken():
    defs, body = bg_deep()
    defs += lin("kr", ["#b23a48", "#5e1520"])
    # Tentacles rise from the deep on both sides and curl outward at the tips
    for x, curl in ((34, -1), (92, -1), (260, 1), (318, 1)):
        d = f"M{x} 200 C {x - curl * 30} 150 {x + curl * 34} 110 {x + curl * 6} 66 C {x - curl * 10} 40 {x + curl * 24} 30 {x + curl * 30} 48"
        body += stroke(d, "#5e1520", 17) + stroke(d, "url(#kr)", 13)
        for t in range(4):
            body += circle(x - curl * (6 + t * 3), 182 - t * 30, 2.4, "#f2a8b0")
    for x, curl in ((140, -1), (212, 1)):
        body += stroke(f"M{x} 150 C {x + curl * 10} 176 {x + curl * 40} 186 {x + curl * 56} 170", "url(#kr)", 12)
    body += path("M120 130 C 110 60 242 60 232 130 C 222 150 130 150 120 130 Z", "url(#kr)")
    body += eye(152, 112, 10, "#ffcf3a") + eye(200, 112, 10, "#ffcf3a")
    body += stroke("M140 96 l 22 8 M212 96 l -22 8", "#3a0a10", 4)
    # Ship
    body += path("M262 46 L 318 46 L 308 58 L 272 58 Z", "#3a2a1c") + stroke("M290 46 L290 18", "#3a2a1c", 2) + poly([(290, 20), (308, 38), (290, 38)], "#e8dcc0")
    return svg(defs, body)


def mermaid_healer():
    defs, body = bg_water("#55c1e8", "#0e4470")
    defs += lin("tail", ["#3fd6b0", "#1b7a8a"]) + rad("heal", [("#b5ffd0", 0.6), ("#b5ffd0", 0)])
    body += circle(176, 92, 80, "url(#heal)")
    body += path("M166 104 C 150 130 160 160 196 170 C 222 176 246 170 260 160 C 236 158 214 156 200 146 C 192 136 190 118 186 104 Z", "url(#tail)")
    body += path("M256 160 C 276 140 290 146 300 136 C 296 156 290 168 262 172 C 286 178 294 190 300 196 C 280 194 266 184 256 176 Z", "#2fb39a")
    body += path("M162 70 C 160 90 162 104 168 110 L 188 110 C 194 100 196 86 192 70 Z", "#f2c7a5")
    body += circle(176, 52, 16, "#f2c7a5")
    body += path("M158 50 C 156 22 196 22 196 48 C 204 70 200 96 190 104 C 192 80 188 62 176 56 C 166 64 162 84 158 100 C 150 86 152 64 158 50 Z", "#c94f7c")
    body += eye(170, 52, 2.6, "#1d4f6b") + eye(182, 52, 2.6, "#1d4f6b")
    body += stroke("M172 62 q 4 3 8 0", "#a8505a", 1.5)
    body += stroke("M164 78 C 150 70 140 60 132 52", "#f2c7a5", 6) + plus(124, 44, 10, "#7dffb0")
    body += plus(240, 60, 6, "#b5ffd0") + plus(110, 120, 5, "#b5ffd0")
    return svg(defs, body)


def octopus_defender():
    defs, body = bg_water("#3d98cf", "#0b2d4d")
    body += octopus(176, 84, 1.25, "#b45bd6", "#e7b8f5")
    body += path("M248 112 C 266 112 284 116 290 120 C 290 150 278 168 262 178 C 246 168 234 150 234 120 C 238 116 244 112 248 112 Z", "#c9a24a", 'stroke="#fff1c2" stroke-width="3"')
    body += stroke("M262 124 L262 166 M248 140 L276 140", "#fff1c2", 3)
    body += bubbles(11)
    return svg(defs, body)


def seahorse_scout():
    defs, body = bg_water("#4cc0e0", "#0d4060")
    defs += lin("sh", ["#ffd166", "#f08a24"])
    for x, h in ((60, 120), (80, 90), (300, 130), (320, 100)):
        body += stroke(f"M{x} 200 C {x - 14} {200 - h * 0.4} {x + 14} {200 - h * 0.7} {x} {200 - h}", "#2f9e6b", 7)
    seahorse = (
        path("M176 40 C 196 36 212 46 214 60 L 244 58 L 244 68 L 214 72 C 210 84 200 90 196 100 "
             "C 214 112 220 134 206 150 C 194 164 172 162 168 148 C 164 136 176 128 186 136 "
             "C 194 142 194 152 186 154 C 196 150 198 136 188 126 C 172 112 160 104 158 86 C 156 66 162 46 176 40 Z", "url(#sh)")
        + path("M160 52 C 150 60 150 76 158 86 C 152 70 156 60 166 50 Z", "#e07a1c")
        + eye(198, 56, 4, "#5a2a00"))
    for i in range(5):
        seahorse += stroke(f"M{168 - i * 2} {66 + i * 14} l -10 -2", "#f6b84a", 3)
    body += seahorse
    body += circle(250, 70, 8, "none", 'stroke="#e8f8ff" stroke-width="2"') + stroke("M256 76 l 8 8", "#e8f8ff", 2)
    body += bubbles(13)
    return svg(defs, body)


def tidal_surge():
    defs, body = bg_water("#2f9fe0", "#09335c")
    defs += lin("surge", ["#7fe0ff", "#0f5d9e"])
    body += wave(-20, 20, 1.6, "url(#surge)")
    body += wave(150, 70, 0.9, "#2a86c4")
    for i in range(14):
        body += circle(20 + i * 24, 180 - (i % 4) * 7, 3 + i % 3, "#e8f8ff", 'opacity="0.85"')
    return svg(defs, body)


def tidepool_star():
    defs, body = bg_water("#6fd0e6", "#1d6a86")
    body += ellipse(176, 150, 170, 40, "#4a8a96", 'opacity="0.6"')
    for x, y, r in ((50, 160, 28), (300, 150, 34), (90, 178, 20)):
        body += ellipse(x, y, r, r * 0.6, "#5b6b70")
    body += poly(star_points(176, 104, 66, 28), "#ff8c5a", 'stroke="#ff8c5a" stroke-width="14" stroke-linejoin="round"')
    for i in range(5):
        a = math.radians(-90 + i * 72)
        for d in (16, 30, 44):
            body += circle(176 + d * math.cos(a), 104 + d * math.sin(a), 3, "#ffd2a8")
    body += plus(270, 50, 7, "#d6fff0") + plus(80, 60, 5, "#d6fff0")
    return svg(defs, body)


def tsunami_turtle():
    defs, body = bg_water("#38a8de", "#0a3358")
    defs += lin("sea", ["#66d6ff", "#1270b0"])
    body += wave(-10, 10, 1.3, "url(#sea)")
    turtle = ""
    for side in (-1, 1):
        turtle += path(f"M{176 + side * 40} 112 C {176 + side * 80} 96 {176 + side * 96} 110 {176 + side * 100} 126 C {176 + side * 80} 126 {176 + side * 60} 124 {176 + side * 40} 128 Z", "#6fae5a")
        turtle += ellipse(176 + side * 46, 160, 16, 8, "#6fae5a", f'transform="rotate({side * 30} {176 + side * 46} 160)"')
    turtle += ellipse(176, 132, 58, 38, "#3f7a3a")
    for x, y in ((156, 120), (196, 120), (176, 108), (176, 140), (146, 142), (206, 142)):
        turtle += poly([(x + 11 * math.cos(math.radians(a)), y + 11 * math.sin(math.radians(a))) for a in range(0, 360, 60)], "#5a9a4a", 'stroke="#2f5e2b" stroke-width="2"')
    turtle += ellipse(242, 120, 22, 16, "#7cbc66") + eye(250, 114, 4)
    body += turtle
    return svg(defs, body)


def whirlpool():
    defs, body = bg_water("#2a8fcf", "#071f3a")
    defs += rad("core", [("#02101f", 1), ("#0d4a7a", 0.6), ("#0d4a7a", 0)])
    body += ellipse(176, 104, 160, 80, "url(#core)")
    for i in range(7):
        rx, ry = 150 - i * 20, 72 - i * 9.5
        body += stroke(f"M{176 - rx} {104} A {rx} {ry} 0 0 1 {176 + rx * 0.7:.1f} {104 - ry * 0.7:.1f}", "#9fe6ff", 3 - i * 0.25, f'opacity="{0.9 - i * 0.1:.2f}"')
        body += stroke(f"M{176 + rx} {104} A {rx} {ry} 0 0 1 {176 - rx * 0.7:.1f} {104 + ry * 0.7:.1f}", "#5cc6f0", 3 - i * 0.25, f'opacity="{0.8 - i * 0.1:.2f}"')
    body += bubbles(17, 12, "#cfefff")
    return svg(defs, body)


# ---------------------------------------------------------------- Faction backgrounds
# Minimal art for the faction decks: one backdrop per faction, a cartoon person or one prop in front.

def bg_office(chart=None):
    """Corporate: a dusk skyline of lit office windows, optionally with a stock line ('up' or 'down')."""
    defs = lin("bg", ["#6f8db0", "#1c2838"])
    body = rect(0, 0, W, H, "url(#bg)")
    towers = ((0, 40, 110), (44, 30, 140), (78, 46, 95), (128, 34, 150), (166, 50, 120), (220, 28, 160), (252, 44, 105), (300, 52, 135))
    for i, (x, w, h) in enumerate(towers):
        body += rect(x, H - h, w, h, "#16212f", 'opacity="0.6"')
        for wy in range(H - h + 8, H - 12, 14):
            for wx in range(x + 5, x + w - 6, 9):
                if (wx * 7 + wy * 3 + i) % 5:
                    body += rect(wx, wy, 4, 6, "#ffe9a8", 'opacity="0.3"')
    if chart:
        ys = (150, 132, 140, 112, 120, 90, 98, 62) if chart == "up" else (60, 78, 70, 100, 92, 124, 116, 150)
        pts = " ".join(f"{20 + i * 44} {y}" for i, y in enumerate(ys))
        body += stroke(f"M{pts}", "#5fe08a" if chart == "up" else "#ff5a5a", 4, 'opacity="0.8"')
    return defs, body


def bg_state(burst="#b8322a"):
    """Statecraft: a propaganda poster, rays bursting from behind the subject over a drab ground."""
    defs = lin("bg", ["#e3d3a4", "#9c8858"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(0, 24, 2):
        a1, a2 = math.radians(i * 15), math.radians((i + 1) * 15)
        body += poly([(176, 110), (176 + 400 * math.cos(a1), 110 + 400 * math.sin(a1)), (176 + 400 * math.cos(a2), 110 + 400 * math.sin(a2))], burst, 'opacity="0.22"')
    body += rect(0, 172, W, 28, "#4a4f2e")
    return defs, body


def bg_vapor():
    """Zeitgeist: a vaporwave sunset over a neon grid."""
    defs = lin("bg", ["#ff7ad9", "#8a4dff", "#22104a"]) + lin("sun", ["#ffe66b", "#ff5fa2"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += circle(176, 118, 64, "url(#sun)")
    for i in range(5):
        body += rect(100, 106 + i * 12, 152, 3 + i, "#6a3ad0", 'opacity="0.9"')
    body += rect(0, 140, W, 60, "#1a0b3d")
    for i in range(-6, 7):
        body += stroke(f"M176 140 L{176 + i * 60} {H}", "#ff4fd8", 1.2, 'opacity="0.7"')
    for y in (146, 156, 170, 190):
        body += stroke(f"M0 {y} L{W} {y}", "#ff4fd8", 1.2, 'opacity="0.7"')
    return defs, body


# ---------------------------------------------------------------- People

SKIN = ("#f2c8a0", "#d9a07a", "#a8704a", "#7a4a2e")
OLD_HAIR = "#e8e8e8"  # White tufts at the sides of a bald head


def person(cx, base, s=1.0, skin=SKIN[0], shirt="#3d5a80", face="smile", hair=None, hat=None, tie=None,
           glasses=None, brows=None, beard=None):
    """A bust: shoulders resting on y=base, head on top. Returns the SVG; the head is at (cx, base - 64*s), radius 20*s."""
    hx, hy, r = cx, base - 64 * s, 20 * s
    out = path(f"M{cx - 34 * s:.1f} {base:.1f} C {cx - 34 * s:.1f} {base - 46 * s:.1f} {cx + 34 * s:.1f} {base - 46 * s:.1f} {cx + 34 * s:.1f} {base:.1f} Z", shirt)
    out += rect(cx - 6 * s, hy + r - 4 * s, 12 * s, 12 * s, skin)
    if tie:
        out += poly([(cx - 4 * s, base - 36 * s), (cx + 4 * s, base - 36 * s), (cx + 6 * s, base - 8 * s), (cx, base - 2 * s), (cx - 6 * s, base - 8 * s)], tie)
    if hair and hair != OLD_HAIR and hat not in ("helmet", "beanie"):
        out += path(f"M{hx - r - 2 * s:.1f} {hy + 4 * s:.1f} C {hx - r:.1f} {hy - r * 1.5:.1f} {hx + r:.1f} {hy - r * 1.5:.1f} {hx + r + 2 * s:.1f} {hy + 4 * s:.1f} Z", hair)
    out += circle(hx, hy, r, skin)
    if hair == OLD_HAIR:
        out += circle(hx - r, hy, 7 * s, hair) + circle(hx + r, hy, 7 * s, hair)
    if beard:
        out += path(f"M{hx - r:.1f} {hy:.1f} C {hx - r:.1f} {hy + r * 1.6:.1f} {hx + r:.1f} {hy + r * 1.6:.1f} {hx + r:.1f} {hy:.1f} C {hx + r * 0.5:.1f} {hy + r * 0.7:.1f} {hx - r * 0.5:.1f} {hy + r * 0.7:.1f} {hx - r:.1f} {hy:.1f} Z", beard)
    out += _face(hx, hy, s, face, brows)
    if glasses:
        lens = "#111" if glasses == "shades" else "none"
        out += circle(hx - 7 * s, hy - 2 * s, 6 * s, lens, f'stroke="#222" stroke-width="{2 * s:.1f}"')
        out += circle(hx + 7 * s, hy - 2 * s, 6 * s, lens, f'stroke="#222" stroke-width="{2 * s:.1f}"')
        out += stroke(f"M{hx - 1 * s:.1f} {hy - 2 * s:.1f} L{hx + 1 * s:.1f} {hy - 2 * s:.1f}", "#222", 2 * s)
    out += _hat(hx, hy, r, s, hat)
    return out


def _face(hx, hy, s, face, brows):
    if face == "closed":
        out = stroke(f"M{hx - 10 * s:.1f} {hy - 2 * s:.1f} q 3 3 6 0 M{hx + 4 * s:.1f} {hy - 2 * s:.1f} q 3 3 6 0", "#222", 1.8 * s)
    else:
        out = circle(hx - 7 * s, hy - 2 * s, 2.4 * s, "#222") + circle(hx + 7 * s, hy - 2 * s, 2.4 * s, "#222")
    mouths = {
        "smile": f"M{hx - 7 * s:.1f} {hy + 7 * s:.1f} Q {hx:.1f} {hy + 13 * s:.1f} {hx + 7 * s:.1f} {hy + 7 * s:.1f}",
        "frown": f"M{hx - 7 * s:.1f} {hy + 11 * s:.1f} Q {hx:.1f} {hy + 5 * s:.1f} {hx + 7 * s:.1f} {hy + 11 * s:.1f}",
        "flat": f"M{hx - 6 * s:.1f} {hy + 9 * s:.1f} L {hx + 6 * s:.1f} {hy + 9 * s:.1f}",
        "wavy": f"M{hx - 8 * s:.1f} {hy + 9 * s:.1f} l 4 -3 l 4 3 l 4 -3 l 4 3",
        "smirk": f"M{hx - 6 * s:.1f} {hy + 9 * s:.1f} Q {hx + 2 * s:.1f} {hy + 11 * s:.1f} {hx + 8 * s:.1f} {hy + 5 * s:.1f}",
        "closed": f"M{hx - 5 * s:.1f} {hy + 9 * s:.1f} L {hx + 5 * s:.1f} {hy + 9 * s:.1f}",
    }
    if face == "grin":
        out += path(f"M{hx - 8 * s:.1f} {hy + 6 * s:.1f} L {hx + 8 * s:.1f} {hy + 6 * s:.1f} Q {hx:.1f} {hy + 17 * s:.1f} {hx - 8 * s:.1f} {hy + 6 * s:.1f} Z", "#fff", 'stroke="#222" stroke-width="1.5"')
    elif face == "shout":
        out += ellipse(hx, hy + 10 * s, 5 * s, 6 * s, "#5a1a1a")
    else:
        out += stroke(mouths[face], "#222", 1.8 * s)
    if brows == "angry":
        out += stroke(f"M{hx - 12 * s:.1f} {hy - 10 * s:.1f} L{hx - 3 * s:.1f} {hy - 6 * s:.1f} M{hx + 12 * s:.1f} {hy - 10 * s:.1f} L{hx + 3 * s:.1f} {hy - 6 * s:.1f}", "#222", 2.2 * s)
    elif brows == "sad":
        out += stroke(f"M{hx - 12 * s:.1f} {hy - 6 * s:.1f} L{hx - 3 * s:.1f} {hy - 10 * s:.1f} M{hx + 12 * s:.1f} {hy - 6 * s:.1f} L{hx + 3 * s:.1f} {hy - 10 * s:.1f}", "#222", 2.2 * s)
    return out


def _hat(hx, hy, r, s, hat):
    if hat == "helmet":
        return (path(f"M{hx - r - 4 * s:.1f} {hy - 2 * s:.1f} C {hx - r:.1f} {hy - r * 1.7:.1f} {hx + r:.1f} {hy - r * 1.7:.1f} {hx + r + 4 * s:.1f} {hy - 2 * s:.1f} Z", "#55603a")
                + rect(hx - r - 6 * s, hy - 4 * s, (r + 6 * s) * 2, 4 * s, "#3e4728"))
    if hat == "cap":
        return (path(f"M{hx - r:.1f} {hy - 6 * s:.1f} C {hx - r:.1f} {hy - r * 1.5:.1f} {hx + r:.1f} {hy - r * 1.5:.1f} {hx + r:.1f} {hy - 6 * s:.1f} Z", "#2aa198")
                + ellipse(hx + r * 0.9, hy - 6 * s, 12 * s, 3 * s, "#1f7f78"))
    if hat == "peaked":
        return (rect(hx - r, hy - r - 2 * s, r * 2, 12 * s, "#3e4f6a", 'rx="3"') + ellipse(hx, hy - r + 10 * s, r + 4 * s, 4 * s, "#1e2635")
                + circle(hx, hy - r + 4 * s, 3 * s, "#f2c94c"))
    if hat == "tophat":
        return rect(hx - r * 0.7, hy - r * 2.2, r * 1.4, r * 1.4, "#1a1a1a") + rect(hx - r - 2 * s, hy - r * 0.85, r * 2 + 4 * s, 4 * s, "#1a1a1a")
    if hat == "beanie":
        return path(f"M{hx - r - 1 * s:.1f} {hy - 4 * s:.1f} C {hx - r:.1f} {hy - r * 1.9:.1f} {hx + r:.1f} {hy - r * 1.9:.1f} {hx + r + 1 * s:.1f} {hy - 4 * s:.1f} Z", "#c0563a")
    if hat == "crown":
        pts = [(hx - r, hy - r * 0.6), (hx - r, hy - r * 1.6), (hx - r * 0.5, hy - r * 1.1), (hx, hy - r * 1.8),
               (hx + r * 0.5, hy - r * 1.1), (hx + r, hy - r * 1.6), (hx + r, hy - r * 0.6)]
        return poly(pts, "#f2c94c", 'stroke="#a87a1a" stroke-width="2"') + circle(hx, hy - r * 1.2, 3 * s, "#d0342c")
    if hat == "fedora":
        return (ellipse(hx, hy - r * 0.7, r * 1.6, 5 * s, "#2a2a2a")
                + path(f"M{hx - r * 0.9:.1f} {hy - r * 0.7:.1f} L{hx - r * 0.8:.1f} {hy - r * 1.7:.1f} Q {hx:.1f} {hy - r * 1.4:.1f} {hx + r * 0.8:.1f} {hy - r * 1.7:.1f} L{hx + r * 0.9:.1f} {hy - r * 0.7:.1f} Z", "#2a2a2a")
                + rect(hx - r * 0.9, hy - r * 0.95, r * 1.8, 4 * s, "#8a1e1e"))
    if hat == "hood":
        return (circle(hx, hy + 2 * s, r + 5 * s, "none", f'stroke="#1e1e2a" stroke-width="{10 * s:.1f}"')
                + poly([(hx - 8 * s, hy - r - 6 * s), (hx, hy - r - 18 * s), (hx + 8 * s, hy - r - 6 * s)], "#1e1e2a"))
    if hat == "bob":
        hair = "#e8c86a"
        return (path(f"M{hx - r - 3 * s:.1f} {hy - 2 * s:.1f} C {hx - r:.1f} {hy - r * 1.6:.1f} {hx + r:.1f} {hy - r * 1.6:.1f} {hx + r + 3 * s:.1f} {hy - 2 * s:.1f} L{hx + r * 0.2:.1f} {hy - r * 0.8:.1f} Z", hair)
                + rect(hx - r - 4 * s, hy - 6 * s, 9 * s, r + 14 * s, hair, 'rx="3"')
                + rect(hx + r - 5 * s, hy - 6 * s, 9 * s, r + 2 * s, hair, 'rx="3"'))
    if hat == "headband":
        return rect(hx - r, hy - 12 * s, r * 2, 5 * s, "#ff3a5a")
    return ""


def badge(cx, cy, s=1.0, color="#ffffff"):
    return stroke(f"M{cx - 10 * s:.1f} {cy - 26 * s:.1f} L{cx:.1f} {cy:.1f} L{cx + 10 * s:.1f} {cy - 26 * s:.1f}", "#c03a3a", 2 * s) + rect(cx - 7 * s, cy, 14 * s, 18 * s, color, 'rx="2" stroke="#555" stroke-width="1"')


def coin(cx, cy, r=8):
    return circle(cx, cy, r, "#f2c94c", 'stroke="#c9952a" stroke-width="2"') + stroke(f"M{cx - r * 0.3:.1f} {cy - r * 0.4:.1f} L{cx + r * 0.3:.1f} {cy + r * 0.4:.1f}", "#c9952a", 2)


def sweat(cx, cy, s=1.0):
    return path(f"M{cx:.1f} {cy:.1f} C {cx - 4 * s:.1f} {cy + 6 * s:.1f} {cx - 3 * s:.1f} {cy + 10 * s:.1f} {cx:.1f} {cy + 10 * s:.1f} C {cx + 3 * s:.1f} {cy + 10 * s:.1f} {cx + 4 * s:.1f} {cy + 6 * s:.1f} {cx:.1f} {cy:.1f} Z", "#8fd8ff")


def speed_lines(x, y, n=4, length=40, color="#ffffff"):
    return "".join(stroke(f"M{x} {y + i * 12} l -{length - i * 6} 0", color, 3, 'opacity="0.7"') for i in range(n))


def flames(cx, base, n=5, spread=24, h=30):
    out = ""
    for i in range(n):
        x = cx + (i - (n - 1) / 2) * spread
        hh = h * (0.7 + 0.3 * ((i * 7) % 3) / 2)
        out += path(f"M{x - 10:.1f} {base} C {x - 14:.1f} {base - hh * 0.5:.1f} {x:.1f} {base - hh * 0.6:.1f} {x:.1f} {base - hh:.1f} C {x + 6:.1f} {base - hh * 0.6:.1f} {x + 14:.1f} {base - hh * 0.4:.1f} {x + 10:.1f} {base} Z", "#ff7a1a")
        out += path(f"M{x - 5:.1f} {base} C {x - 6:.1f} {base - hh * 0.3:.1f} {x:.1f} {base - hh * 0.4:.1f} {x:.1f} {base - hh * 0.6:.1f} C {x + 4:.1f} {base - hh * 0.4:.1f} {x + 6:.1f} {base - hh * 0.2:.1f} {x + 5:.1f} {base} Z", "#ffd84a")
    return out


def zzz(x, y, s=1.0, color="#ffffff"):
    out = ""
    for i in range(3):
        k = s * (1 - i * 0.25)
        ox, oy = x + i * 16 * s, y - i * 16 * s
        out += stroke(f"M{ox:.1f} {oy:.1f} l {10 * k:.1f} 0 l {-10 * k:.1f} {10 * k:.1f} l {10 * k:.1f} 0", color, 2.5)
    return out


def sign(cx, top, w=56, h=34, color="#fff7e0", pole="#8a5a2a"):
    return stroke(f"M{cx} {top + h} L{cx} {top + h + 50}", pole, 5) + rect(cx - w / 2, top, w, h, color, 'stroke="#333" stroke-width="2"')


def arrow(x1, y1, x2, y2, color, width=8):
    a = math.atan2(y2 - y1, x2 - x1)
    head = [(x2 + 14 * math.cos(a), y2 + 14 * math.sin(a)),
            (x2 + 12 * math.cos(a + 2.3), y2 + 12 * math.sin(a + 2.3)),
            (x2 + 12 * math.cos(a - 2.3), y2 + 12 * math.sin(a - 2.3))]
    return stroke(f"M{x1} {y1} L{x2} {y2}", color, width) + poly(head, color)


# ---------------------------------------------------------------- Corporate cards

def unpaid_intern():
    defs, body = bg_office()
    body += person(176, 200, 1.5, SKIN[1], "#9ab0c8", face="wavy", hair="#4a2e1a", brows="sad")
    body += badge(176, 160, 1.2)
    # A tray of coffees for everyone but them
    body += rect(222, 150, 90, 8, "#7a5a3a", 'rx="2"')
    for x in (232, 254, 276, 298):
        body += path(f"M{x - 8} 150 L{x - 6} 128 L{x + 6} 128 L{x + 8} 150 Z", "#f4efe6") + rect(x - 8, 124, 16, 5, "#6a4a2a")
    body += sweat(212, 84, 1.3)
    return svg(defs, body)


def inhuman_resources():
    defs, body = bg_office("down")
    body += person(130, 200, 1.5, SKIN[0], "#7a6a8a", face="smile", hair="#3a2a2a", glasses="clear", tie="#c03a3a")
    # A stack of pink slips and the stamp that approves them
    for i in range(4):
        body += rect(206 + i * 6, 120 - i * 8, 70, 46, "#ffb3c6", f'stroke="#c06a80" stroke-width="2" transform="rotate({-8 + i * 5} {241 + i * 6} {143 - i * 8})"')
    body += rect(268, 52, 34, 40, "#6a4a2a", 'rx="4"') + rect(260, 92, 50, 14, "#c03a3a", 'rx="3"')
    return svg(defs, body)


def micromanager():
    defs, body = bg_office()
    body += person(150, 200, 1.6, SKIN[0], "#6c7a89", face="smirk", hair="#6a4a2a", tie="#264653", glasses="clear")
    # Magnifying glass over every little thing
    body += circle(262, 82, 34, "#cfe8ff", 'opacity="0.45" stroke="#2b2b2b" stroke-width="7"')
    body += stroke("M286 106 L322 146", "#2b2b2b", 10)
    body += circle(256, 78, 4, "#2b2b2b") + circle(270, 90, 3, "#2b2b2b")
    return svg(defs, body)


def multilevel_marketing_seller():
    defs, body = bg_office("up")
    # The pyramid of recruits behind the seller
    for row in range(4):
        for i in range(row + 1):
            x = 272 - row * 18 + i * 36
            body += circle(x, 40 + row * 36, 11, SKIN[(row + i) % 4]) + path(f"M{x - 14} {62 + row * 36} C {x - 14} {50 + row * 36} {x + 14} {50 + row * 36} {x + 14} {62 + row * 36} Z", ("#e76f51", "#457b9d", "#8ab17d")[(row + i) % 3])
    body += person(120, 200, 1.6, SKIN[1], "#ff5fa2", face="grin", hair="#d8b04a")
    return svg(defs, body)


def day_trader():
    defs, body = bg_office()
    for i, (x, color, up) in enumerate(((60, "#5fe08a", True), (176, "#ff5a5a", False), (292, "#5fe08a", True))):
        body += rect(x - 52, 26, 104, 70, "#111a24", 'rx="4" stroke="#55606e" stroke-width="3"')
        ys = (84, 70, 76, 52, 40) if up else (40, 52, 46, 70, 84)
        pts = " ".join(f"{x - 44 + j * 22} {y}" for j, y in enumerate(ys))
        body += stroke(f"M{pts}", color, 3)
    body += person(176, 200, 1.4, SKIN[0], "#3a4a5a", face="wavy", hair="#2a1a0a", brows="sad")
    body += path("M152 108 C 152 80 200 80 200 108", "none", 'stroke="#222" stroke-width="4"') + rect(146, 104, 10, 16, "#222", 'rx="3"')
    body += sweat(206, 104, 1.2)
    return svg(defs, body)


def pluriemployed_hustler():
    defs, body = bg_office()
    body += rect(196, 82, 76, 70, "#2ec4b6", 'rx="8" stroke="#1a8a80" stroke-width="3"')
    body += circle(234, 117, 14, "#ffffff", 'opacity="0.8"') + poly(star_points(234, 117, 10, 4), "#2ec4b6")
    body += person(176, 200, 1.5, SKIN[2], "#2ec4b6", face="flat", hat="cap", brows="sad")
    body += speed_lines(110, 110, 4, 60)
    for i in range(5):
        body += poly(star_points(40 + i * 22, 40, 9, 4), "#f2c94c")
    return svg(defs, body)


def unicorn():
    defs, body = bg_office("down")
    for i, color in enumerate(("#ff5a5a", "#ffb03a", "#ffe45a", "#5ad07a", "#5aaaff", "#9a6aff")):
        body += stroke(f"M120 {150 - i * 8} C 100 {110 - i * 10} 130 {70 - i * 6} 150 {60 - i * 4}", color, 7)
    body += path("M150 170 C 140 120 150 80 190 66 C 230 56 262 76 268 100 C 272 116 256 122 240 116 C 226 112 214 120 214 136 L 216 170 Z", "#fbf6ff", 'stroke="#d0c4e8" stroke-width="3"')
    body += poly([(198, 70), (226, 18), (214, 74)], "#f2c94c", 'stroke="#c9952a" stroke-width="2"')
    body += eye(232, 88, 5) + circle(256, 106, 2.5, "#9a8ab8")
    body += flames(196, 196, 6, 22, 36)
    return svg(defs, body)


def multimillionaire_philanthropist():
    defs, body = bg_office("up")
    body += person(130, 200, 1.5, SKIN[0], "#1a1a1a", face="smirk", hat="tophat", tie="#d4af37")
    # A giant ceremonial cheque, mostly tax deduction
    body += rect(186, 70, 150, 70, "#fffdf2", 'stroke="#8a7a5a" stroke-width="3"')
    body += stroke("M198 90 L280 90 M198 104 L318 104", "#bbb39a", 3) + stroke("M286 126 q 10 -14 20 0 q 10 14 20 0", "#2a3a8a", 3)
    for x, y in ((206, 166), (226, 176), (300, 170)):
        body += coin(x, y, 9)
    return svg(defs, body)


def made_man():
    defs, body = bg_office()
    body += person(176, 200, 1.9, SKIN[1], "#2b2b3a", face="smirk", tie="#8a1e1e", brows="angry", hat="fedora")
    body += rect(196, 134, 30, 7, "#7a4a2a", 'rx="2"') + circle(228, 137, 3.5, "#ff7a1a")
    body += stroke("M232 130 q 6 -10 0 -20 q -6 -10 2 -20", "#dddddd", 2, 'opacity="0.6"')
    return svg(defs, body)


def charles_ponzi():
    defs, body = bg_office()
    rows = ((176, 52),), ((150, 92), (202, 92)), ((124, 132), (176, 132), (228, 132))
    for r, row in enumerate(rows):
        for x, y in row:
            body += path(f"M{x - 20} {y} L{x + 20} {y} L{x + 6} {y + 22} L{x - 6} {y + 22} Z", "#e8f4ff", 'opacity="0.75" stroke="#ffffff" stroke-width="2"')
            body += stroke(f"M{x} {y + 22} L{x} {y + 36} M{x - 10} {y + 38} L{x + 10} {y + 38}", "#ffffff", 3)
            if r == 0:
                body += path(f"M{x - 18} {y + 2} L{x + 18} {y + 2} L{x + 6} {y + 20} L{x - 6} {y + 20} Z", "#f2c94c")
    for x, y in ((150, 40), (196, 44), (204, 70), (146, 74)):
        body += circle(x, y, 3.5, "#f2c94c")
    body += person(300, 200, 1.0, SKIN[0], "#1a1a1a", face="smirk", hair="#2b2b2b", tie="#d4af37")
    return svg(defs, body)


def margin_call():
    defs, body = bg_office("down")
    # An old desk phone that won't stop ringing
    body += path("M110 170 L130 110 L222 110 L242 170 Z", "#1a1a1a")
    body += rect(150, 128, 52, 30, "#3a3a3a", 'rx="4"')
    for i in range(3):
        for j in range(3):
            body += rect(156 + j * 16, 132 + i * 8, 10, 5, "#dddddd", 'rx="1"')
    body += path("M100 104 C 100 84 252 84 252 104 L 238 112 C 230 100 122 100 114 112 Z", "#1a1a1a")
    for side in (-1, 1):
        for i in range(3):
            r = 30 + i * 14
            body += path(f"M{176 + side * (90 + i * 14)} {70 - i * 4} q {side * 10} {-r * 0.4:.0f} 0 {-r * 0.8:.0f}", "none", 'stroke="#ff5a5a" stroke-width="4"')
    body += arrow(176, 18, 176, 70, "#ff5a5a", 9)
    return svg(defs, body)


def hostile_takeover():
    defs, body = bg_office("down")
    body += path("M40 150 C 80 100 200 92 290 120 C 320 130 330 150 316 160 C 260 176 120 180 40 150 Z", "#7d8a99")
    body += poly([(150, 104), (190, 40), (206, 102)], "#6a7686")
    body += path("M250 150 L314 150 L300 164 L262 164 Z", "#ffffff")
    for i in range(6):
        body += poly([(258 + i * 9, 150), (262 + i * 9, 158), (266 + i * 9, 150)], "#7d8a99")
    body += circle(282, 128, 4, "#111")
    body += poly([(228, 160), (240, 160), (244, 196), (234, 200), (224, 196)], "#e63946")
    body += rect(222, 152, 24, 10, "#ffffff")
    return svg(defs, body)


def mass_layoffs():
    defs, body = bg_office("down")
    body += path("M110 112 L242 112 L230 186 L122 186 Z", "#c8955a", 'stroke="#8a5a2a" stroke-width="3"')
    body += path("M110 112 L92 92 L224 92 L242 112 Z", "#dcae74", 'stroke="#8a5a2a" stroke-width="3"')
    body += rect(198, 70, 24, 34, "#ffffff", 'rx="3"') + stroke("M222 80 q 10 6 0 14", "#ffffff", 4)
    body += path("M140 104 C 120 60 150 50 150 104 Z", "#5aa04a") + path("M150 104 C 160 50 186 64 160 104 Z", "#6ab85a")
    body += rect(140, 98, 22, 16, "#b0603a")
    body += rect(270, 60, 60, 112, "#3a4a5a", 'stroke="#222" stroke-width="3"') + circle(318, 120, 4, "#f2c94c")
    return svg(defs, body)


def team_building_exercise():
    defs, body = bg_office()
    for i in range(9):
        x = 20 + i * 40
        body += poly([(x, 20), (x + 36, 20), (x + 18, 42)], ("#e63946", "#f2c94c", "#2a9d8f")[i % 3])
    body += stroke("M10 20 L342 20", "#ffffff", 2)
    for i, (x, sk) in enumerate(((96, SKIN[0]), (176, SKIN[2]), (256, SKIN[1]))):
        body += person(x, 200, 1.15, sk, ("#e76f51", "#457b9d", "#8ab17d")[i], face="flat", brows="sad")
        body += poly([(x - 12, 102), (x + 12, 102), (x, 72)], ("#f2c94c", "#e63946", "#2a9d8f")[i])
    return svg(defs, body)


def dead_cat_bounce():
    defs, body = bg_office()
    body += stroke("M20 30 L120 150 L150 112 L180 176 L340 190", "#ff5a5a", 6)
    # The cat, mid-bounce, not enjoying it
    cx, cy = 150, 86
    body += ellipse(cx, cy + 14, 22, 14, "#3a3a3a") + circle(cx, cy - 6, 14, "#3a3a3a")
    body += poly([(cx - 13, cy - 12), (cx - 8, cy - 28), (cx - 2, cy - 16)], "#3a3a3a") + poly([(cx + 13, cy - 12), (cx + 8, cy - 28), (cx + 2, cy - 16)], "#3a3a3a")
    body += stroke(f"M{cx - 8} {cy - 8} l 5 5 m 0 -5 l -5 5 M{cx + 3} {cy - 8} l 5 5 m 0 -5 l -5 5", "#ffffff", 1.8)
    body += stroke(f"M{cx + 20} {cy + 18} q 18 -6 14 -28", "#3a3a3a", 5)
    for x in (124, 176):
        body += stroke(f"M{x} {cy + 34} l 0 10", "#ffffff", 2, 'opacity="0.7"')
    return svg(defs, body)


def taxpayer_funds_venture_capital_profits():
    defs, body = bg_office("up")
    for i, h in enumerate((40, 62, 54, 90, 120)):
        body += rect(150 + i * 36, 180 - h, 26, h, "#5fe08a", 'stroke="#1f7a4a" stroke-width="2"')
    # The public pays in, one coin at a time
    for i, x in enumerate((30, 66, 102)):
        body += person(x, 200, 0.75, SKIN[i], ("#8ab17d", "#457b9d", "#e76f51")[i], face="frown")
        body += coin(x + 12, 110 - i * 14, 7)
    body += arrow(120, 100, 200, 60, "#f2c94c", 5)
    return svg(defs, body)


# ---------------------------------------------------------------- Statecraft cards

def king_emeritus():
    defs, body = bg_state("#2a3a8a")
    # The yacht he kept
    body += path("M196 170 L340 170 L322 190 L210 190 Z", "#f4f4f4", 'stroke="#555" stroke-width="2"') + rect(236, 146, 70, 24, "#e8e8e8", 'stroke="#555" stroke-width="2"')
    body += person(130, 200, 1.5, "#e8c8b0", "#7a1a2a", face="smirk", hair=OLD_HAIR, hat="crown", glasses="shades")
    return svg(defs, body)


def spite_elected_president():
    defs, body = bg_state()
    for i in range(5):
        body += rect(40, 40 + i * 12, 80, 12, "#b8322a" if i % 2 == 0 else "#f4ecd8")
    body += rect(40, 40, 32, 30, "#2a3a8a")
    body += person(190, 186, 1.5, SKIN[0], "#1a1a3a", face="shout", hair="#f2c94c", tie="#d0342c", brows="angry")
    body += rect(130, 150, 120, 50, "#5a3a22", 'stroke="#3a2a12" stroke-width="3"') + poly(star_points(190, 172, 12, 5), "#f2c94c")
    return svg(defs, body)


def police_officer_productivity_target():
    defs, body = bg_state("#3e4f6a")
    body += person(130, 200, 1.5, SKIN[1], "#3e4f6a", face="smirk", hat="peaked")
    # The quota: tally marks, each a person
    body += rect(208, 56, 112, 132, "#7a5a3a", 'rx="6"') + rect(216, 70, 96, 112, "#ffffff")
    for group_index in range(3):
        y = 86 + group_index * 32
        for i in range(4):
            body += stroke(f"M{228 + i * 12} {y} l 0 22", "#1a1a1a", 3)
        body += stroke(f"M222 {y + 20} L{276} {y + 2}", "#c0392b", 3)
    return svg(defs, body)


def internal_revenue_service():
    defs, body = bg_state("#5a6a3a")
    body += person(150, 200, 1.5, SKIN[0], "#2b2b2b", face="smirk", hat="tophat", glasses="clear")
    body += path("M236 180 C 206 180 206 132 236 122 L 228 108 L 252 108 L 244 122 C 274 132 274 180 244 180 Z", "#c8a060", 'stroke="#8a6a2a" stroke-width="3"')
    for x, y in ((290, 172), (304, 160), (286, 150)):
        body += coin(x, y, 9)
    return svg(defs, body)


def mercenaries():
    defs, body = bg_state("#4a4f2e")
    body += speed_lines(110, 100, 5, 80, "#4a4f2e")
    body += person(176, 200, 1.6, SKIN[2], "#55603a", face="smirk", hat="helmet", glasses="shades")
    body += stroke("M210 160 L300 132", "#2b2b2b", 9)
    for x, y in ((72, 150), (86, 166), (60, 170)):
        body += coin(x, y, 10)
    return svg(defs, body)


def man_o_war():
    defs, body = bg_state("#2a4a7a")
    body += rect(0, 168, W, 32, "#2a4a7a")
    body += path("M60 140 L292 140 L270 178 L82 178 Z", "#6a4422", 'stroke="#3a220e" stroke-width="3"')
    for i in range(7):
        body += rect(96 + i * 26, 150, 12, 9, "#1a1a1a")
    for x, h in ((120, 100), (176, 120), (232, 96)):
        body += stroke(f"M{x} 140 L{x} {140 - h}", "#3a220e", 5)
        body += path(f"M{x - 34} {150 - h} Q {x} {130 - h} {x + 34} {150 - h} L {x + 30} {126 - h * 0.25:.0f} Q {x} {138 - h * 0.25:.0f} {x - 30} {126 - h * 0.25:.0f} Z", "#f4ecd8", 'stroke="#a89878" stroke-width="2"')
    body += poly([(176, 20), (204, 26), (176, 32)], "#b8322a")
    return svg(defs, body)


def ministry_for_equality():
    defs, body = bg_state("#5a5a6a")
    body += rect(76, 40, 200, 140, "#8a8a96", 'stroke="#4a4a56" stroke-width="3"')
    for row in range(3):
        for col in range(5):
            body += rect(92 + col * 36, 110 + row * 22, 16, 12, "#2a2a36")
    body += path("M176 98 C 140 70 140 40 164 40 C 172 40 176 48 176 52 C 176 48 180 40 188 40 C 212 40 212 70 176 98 Z", "#ff6a9a", 'stroke="#c03a6a" stroke-width="3"')
    # Barbed wire along the roof
    body += stroke("M60 36 L292 36", "#3a3a3a", 2)
    for i in range(12):
        x = 66 + i * 20
        body += stroke(f"M{x - 4} 32 l 8 8 m 0 -8 l -8 8", "#3a3a3a", 2)
    return svg(defs, body)


def supreme_court():
    defs, body = bg_state("#7a6a4a")
    body += poly([(70, 66), (176, 22), (282, 66)], "#e8e2d0", 'stroke="#8a8270" stroke-width="3"')
    body += rect(70, 66, 212, 12, "#d8d2c0")
    for i in range(6):
        body += rect(84 + i * 36, 78, 16, 82, "#f4efe0", 'stroke="#8a8270" stroke-width="2"')
    body += rect(62, 160, 228, 14, "#d8d2c0", 'stroke="#8a8270" stroke-width="2"')
    body += rect(286, 126, 46, 18, "#6a4422", 'rx="4" transform="rotate(-30 309 135)"') + stroke("M300 142 L280 176", "#6a4422", 6)
    return svg(defs, body)


def protocol_enforcers():
    defs, body = bg_state("#2a2a2a")
    for x in (120, 232):
        body += person(x, 200, 1.4, SKIN[0] if x < 176 else SKIN[2], "#111111", face="flat", tie="#111111", glasses="shades", hair="#1a1a1a")
        body += path(f"M{x - 4} {200 - 36 * 1.4:.0f} L{x} {200 - 30 * 1.4:.0f} L{x + 4} {200 - 36 * 1.4:.0f} Z", "#ffffff")
    body += rect(20, 176, 312, 8, "#f2c94c") + rect(20, 176, 312, 8, "none", 'stroke="#1a1a1a" stroke-width="1"')
    return svg(defs, body)


def demagogue():
    defs, body = bg_state()
    body += person(130, 200, 1.5, SKIN[0], "#8a1e1e", face="grin", hair="#2b2b2b")
    body += path("M160 120 L230 90 L230 160 L160 130 Z", "#d0d0d0", 'stroke="#555" stroke-width="3"') + rect(150, 118, 14, 14, "#555555")
    for i in range(3):
        body += path(f"M{250 + i * 18} {90 - i * 6} Q {270 + i * 20} 125 {250 + i * 18} {160 + i * 6}", "none", 'stroke="#8a1e1e" stroke-width="4"')
    return svg(defs, body)


def red_tape():
    defs, body = bg_state("#7a6a4a")
    for i in range(5):
        body += rect(116 + (i % 2) * 6, 172 - i * 22, 120, 20, "#f4ecd8", 'stroke="#a89878" stroke-width="2"')
    body += path("M90 60 C 160 20 220 160 270 80 C 300 40 230 30 200 90 C 170 150 100 150 120 100 C 140 50 250 140 280 150", "none", 'stroke="#d0342c" stroke-width="7"')
    body += rect(240, 34, 40, 28, "#6a4a2a", 'rx="4"') + rect(246, 62, 28, 10, "#d0342c")
    return svg(defs, body)


def preemptive_strike():
    defs, body = bg_state()
    body += circle(240, 100, 54, "none", 'stroke="#b8322a" stroke-width="6"') + circle(240, 100, 30, "none", 'stroke="#b8322a" stroke-width="5"')
    body += stroke("M240 32 L240 168 M172 100 L308 100", "#b8322a", 3)
    body += path("M40 150 L130 110 L140 120 L50 160 Z", "#d0d0d0", 'stroke="#555" stroke-width="2"')
    body += poly([(130, 110), (156, 104), (140, 120)], "#b8322a") + flames(36, 168, 1, 0, 26)
    return svg(defs, body)


def martial_law():
    defs = lin("bg", ["#0e1430", "#2a2a4a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x, a in ((60, -20), (300, 20)):
        body += poly([(x, 200), (x - 10 + a * 6, 0), (x + 50 + a * 6, 0)], "#fff7c0", 'opacity="0.18"')
    body += circle(176, 50, 22, "#f4f0d0") + circle(186, 44, 20, "#0e1430")
    body += rect(96, 80, 160, 100, "#3a3a5a", 'stroke="#111" stroke-width="3"') + rect(106, 90, 140, 80, "#ffcf5a", 'opacity="0.6"')
    for i in range(7):
        body += rect(112 + i * 20, 80, 6, 100, "#222")
    body += circle(176, 140, 10, "#222") + rect(170, 140, 12, 30, "#222")
    return svg(defs, body)


def kakistocracy():
    defs, body = bg_state()
    # A golden throne, and the least qualified occupant available
    body += path("M110 190 L110 60 C 110 30 242 30 242 60 L242 190 Z", "#d4af37", 'stroke="#8a6a1a" stroke-width="4"')
    body += rect(96, 130, 160, 24, "#b8322a", 'rx="6"')
    body += person(176, 160, 1.3, "#f4f4f4", "#5aaaff", face="grin")
    body += circle(176, 160 - 64 * 1.3 + 4, 4.5, "#ff3a3a")
    body += poly([(156, 60), (196, 60), (176, 4)], "#f4f4f4", 'stroke="#555" stroke-width="2"')
    return svg(defs, body)


def mutually_assured_destruction():
    defs = rad("bg", ["#fff3c0", "#ff8a2a", "#5a1a0a"], cy=0.6, r=0.8)
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(0, 176, W, 24, "#2a1a0a")
    body += path("M160 176 C 160 140 150 120 150 100 L202 100 C 202 120 192 140 192 176 Z", "#c8a080")
    body += ellipse(176, 80, 76, 40, "#d8b090") + ellipse(176, 62, 54, 30, "#e8c8a8") + ellipse(176, 92, 90, 18, "#b88a6a", 'opacity="0.7"')
    for side in (-1, 1):
        body += path(f"M{176 + side * 170} 30 L{176 + side * 120} 60 L{176 + side * 112} 52 L{176 + side * 160} 22 Z", "#e8e8e8", 'stroke="#555" stroke-width="2"')
    return svg(defs, body)


def panem_et_circenses():
    defs, body = bg_state("#b8322a")
    # The arena in the back, free bread in front
    body += path("M40 170 L40 70 C 120 40 232 40 312 70 L312 170 Z", "#d8c8a0", 'stroke="#8a7a5a" stroke-width="3"')
    for row, y in enumerate((80, 120)):
        for i in range(7):
            x = 62 + i * 40
            body += path(f"M{x} {y + 34} L{x} {y + 12} C {x} {y} {x + 22} {y} {x + 22} {y + 12} L{x + 22} {y + 34} Z", "#6a5a3a")
    body += ellipse(176, 168, 90, 22, "#d8954a", 'stroke="#8a5a1a" stroke-width="3"')
    for i in range(4):
        body += stroke(f"M{120 + i * 36} 160 q 10 -8 20 0", "#8a5a1a", 3)
    return svg(defs, body)


# ---------------------------------------------------------------- Zeitgeist cards

def karen():
    defs, body = bg_vapor()
    body += person(150, 200, 1.6, SKIN[0], "#7ac0c0", face="shout", hat="bob", brows="angry")
    # The finger, pointed at the manager's office
    body += stroke("M200 160 L266 110", SKIN[0], 14) + stroke("M262 112 L300 86", SKIN[0], 6)
    body += rect(296, 40, 44, 70, "#3a3a5a", 'stroke="#111" stroke-width="2"') + circle(330, 76, 3, "#f2c94c")
    return svg(defs, body)


def boomer():
    defs, body = bg_vapor()
    body += cloud(270, 46, 1.6, "#ffffff", 0.95)
    body += person(140, 200, 1.6, "#e8c8b0", "#8a7a5a", face="shout", hair=OLD_HAIR, brows="angry", glasses="clear")
    body += stroke("M196 150 L238 90", "#e8c8b0", 12)
    for i in range(3):
        body += path(f"M{214 + i * 12} {64 - i * 10} q 10 4 20 -4", "none", 'stroke="#ffffff" stroke-width="3"')
    return svg(defs, body)


def hipster():
    defs, body = bg_vapor()
    body += person(170, 200, 1.6, SKIN[0], "#8a4a2a", face="flat", hat="beanie", glasses="clear", beard="#6a3a1a")
    body += path("M244 140 L276 140 L272 184 L248 184 Z", "#f4ecd8", 'stroke="#6a4a2a" stroke-width="2"')
    body += rect(246, 154, 28, 14, "#6a4a2a")
    body += stroke("M252 132 q 6 -10 0 -18 M266 132 q 6 -10 0 -18", "#ffffff", 2, 'opacity="0.7"')
    return svg(defs, body)


def underdog():
    defs, body = bg_vapor()
    body += ellipse(176, 160, 46, 26, "#c8955a")
    body += circle(176, 112, 34, "#c8955a")
    body += ellipse(146, 104, 12, 26, "#8a5a2a", 'transform="rotate(20 146 104)"') + ellipse(206, 104, 12, 26, "#8a5a2a", 'transform="rotate(-20 206 104)"')
    body += eye(164, 108, 9, "#2a1a0a") + eye(190, 108, 9, "#2a1a0a")
    body += ellipse(177, 126, 7, 5, "#2a1a0a")
    body += rect(186, 82, 26, 8, "#fff7e0", 'transform="rotate(-25 199 86)"')
    body += stroke("M148 118 q -2 10 -6 14", "#8fd8ff", 2.5)
    return svg(defs, body)


def hikikomori():
    defs, body = bg_vapor()
    defs += rad("glow", [("#8fe8ff", 0.7), ("#8fe8ff", 0)])
    body += ellipse(176, 110, 90, 70, "url(#glow)")
    body += person(176, 200, 1.6, "#bfe0f0", "#3a3a5a", face="flat", hair="#2a1a3a")
    body += ellipse(165, 90, 5, 2, "#7a6a9a") + ellipse(187, 90, 5, 2, "#7a6a9a")
    body += rect(150, 140, 52, 60, "#111", 'rx="6"') + rect(155, 146, 42, 48, "#8fe8ff")
    return svg(defs, body)


def reality_tv_star():
    defs, body = bg_vapor()
    defs += lin("spot", [("#fffbe0", 0.7), ("#fffbe0", 0.05)])
    body += poly([(156, 0), (196, 0), (256, 200), (96, 200)], "url(#spot)")
    for x in (60, 100, 252, 292):
        body += person(x, 200, 0.8, "#7a6a9a", "#4a3a6a", face="flat")
    body += person(176, 200, 1.6, SKIN[2], "#ff3a8a", face="smirk", hair="#1a1a1a", glasses="shades")
    for x, y, r in ((120, 40, 10), (240, 60, 8), (220, 24, 6)):
        body += poly(star_points(x, y, r, r * 0.35, 4), "#ffffff")
    return svg(defs, body)


def scam_artist():
    defs, body = bg_vapor()
    body += person(140, 200, 1.6, SKIN[1], "#2a2a2a", face="grin", hair="#1a1a1a", glasses="shades")
    body += path("M110 152 Q 140 176 170 152", "none", 'stroke="#f2c94c" stroke-width="5"')
    # The course: a book about selling books
    body += rect(214, 92, 70, 92, "#ff5fa2", 'stroke="#111" stroke-width="3"') + rect(222, 102, 54, 20, "#ffe45a")
    body += poly(star_points(249, 148, 16, 7), "#ffe45a")
    return svg(defs, body)


def football_hooligans():
    defs, body = bg_vapor()
    body += ellipse(300, 60, 40, 60, "#ff3a3a", 'opacity="0.35"') + rect(296, 100, 8, 40, "#ff7a1a")
    for i, x in enumerate((70, 150, 230)):
        body += person(x, 200, 1.15, SKIN[i], "#1a5a2a", face="shout", brows="angry", hair="#2a1a0a")
        for j in range(4):
            body += rect(x - 24 + j * 12, 150, 12, 10, "#ffffff" if j % 2 == 0 else "#1a5a2a")
    return svg(defs, body)


def antihero():
    defs, body = bg_vapor()
    body += person(176, 200, 1.6, SKIN[0], "#2a2a3a", face="frown", hat="hood", brows="angry")
    body += rect(194, 128, 26, 5, "#f4f4f4", 'rx="2"') + circle(222, 130, 3, "#ff7a1a")
    body += stroke("M224 124 q 6 -10 0 -20", "#dddddd", 2, 'opacity="0.6"')
    return svg(defs, body)


def bodybuilder():
    defs, body = bg_vapor()
    body += person(176, 200, 1.6, SKIN[1], "#1a1a1a", face="grin", hat="headband", brows="angry")
    body += stroke("M96 70 L256 70", "#888888", 6)
    for x in (92, 260):
        body += rect(x - 8, 50, 16, 40, "#333333", 'rx="3"')
    body += stroke("M118 150 L104 76 M234 150 L248 76", SKIN[1], 12)
    body += rect(286, 120, 24, 50, "#5aff7a", 'rx="4" stroke="#1a1a1a" stroke-width="2"') + bolt(292, 128, 0.8, "#1a1a1a")
    return svg(defs, body)


def yolo():
    defs, body = bg_vapor()
    body += rect(110, 132, 140, 12, "#f2c94c", 'rx="6"') + circle(130, 150, 8, "#333") + circle(230, 150, 8, "#333")
    body += path("M120 116 L210 116 L226 124 L210 132 L120 132 Z", "#e8e8e8", 'stroke="#555" stroke-width="2"')
    body += flames(100, 130, 1, 0, 30)
    body += path("M110 120 L70 112 L70 136 L110 128 Z", "#ff7a1a")
    body += person(176, 120, 1.0, SKIN[1], "#ff3a8a", face="grin", hat="cap")
    body += speed_lines(60, 40, 4, 50)
    return svg(defs, body)


def this_is_fine():
    defs = lin("bg", ["#c8603a", "#7a2a1a"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(0, 0, W, 34, "#3a2a2a", 'opacity="0.8"')
    for x in (40, 120, 220, 300):
        body += cloud(x, 24, 0.9, "#2a2020", 0.8)
    body += flames(40, 200, 3, 26, 70) + flames(312, 200, 3, 26, 70) + flames(176, 60, 4, 30, 24)
    body += rect(108, 166, 150, 34, "#6a4a2a")
    # The dog, in its little hat, with its coffee
    body += path("M126 200 C 126 132 226 132 226 200 Z", "#e8b45a", 'stroke="#8a5a2a" stroke-width="2"')
    body += ellipse(176, 146, 22, 16, "#e8b45a")  # The neck: the head sits on the body
    body += ellipse(140, 112, 14, 30, "#8a5a2a", 'transform="rotate(20 140 112)"')
    body += ellipse(212, 112, 14, 30, "#8a5a2a", 'transform="rotate(-20 212 112)"')
    body += ellipse(176, 110, 38, 34, "#e8b45a", 'stroke="#8a5a2a" stroke-width="2"')
    body += ellipse(176, 126, 20, 14, "#f6d8a0")
    body += ellipse(176, 118, 7, 5, "#222")
    body += circle(162, 102, 4, "#222") + circle(190, 102, 4, "#222")
    body += stroke("M168 132 Q 176 138 184 132", "#222", 2)
    body += ellipse(176, 78, 26, 5, "#1a1a1a") + path("M158 78 C 158 58 194 58 194 78 Z", "#1a1a1a")
    body += path("M214 152 L238 152 L236 176 L216 176 Z", "#ffffff", 'stroke="#222" stroke-width="2"') + stroke("M238 158 q 8 4 0 10", "#ffffff", 3)
    body += path("M196 168 C 204 160 214 160 218 166", "none", 'stroke="#e8b45a" stroke-width="10" stroke-linecap="round"')
    # The speech bubble
    body += path("M206 44 L326 44 L326 80 L248 80 L230 96 L234 80 L206 80 Z", "#ffffff", 'stroke="#222" stroke-width="2"')
    body += lettering("THIS IS FINE", 266, 56, 11, "#222", width=1.8)
    return svg(defs, body)


def strike():
    # The head of the works council in a lobster bib, feasting at the boardroom table behind his WORKS COUNCIL nameplate,
    # while the beaming CEO pats him on the shoulder; through the window the strikers stand in the rain with their
    # placards, each handed a single soggy biscuit
    defs = lin("bg", ["#5a3a28", "#2e1c12"]) + lin("rain", ["#7a8a9a", "#3e4a5a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in range(22, W, 44):  # Wood panelling
        body += stroke(f"M{x} 0 L{x} 140", "#24160c", 2, 'opacity="0.6"')
    # The window, the rain and the strikers outside
    wx, wy, ww, wh = 10, 12, 140, 100
    body += rect(wx, wy, ww, wh, "url(#rain)")
    for i, x in enumerate((30, 62, 94, 126)):
        body += stroke(f"M{x + 10} {wy + 18} L{x + 10} {wy + 64}", "#6a4a2a", 3)
        body += rect(x - 4, wy + 10, 28, 16, "#fff7e0", 'stroke="#333" stroke-width="1"')
        body += stroke(f"M{x + 2} {wy + 15} L{x + 18} {wy + 21} M{x + 18} {wy + 15} L{x + 2} {wy + 21}", "#d0342c", 2.5)
        body += person(x, wy + wh, 0.46, SKIN[i % 4], ("#e76f51", "#457b9d", "#8ab17d", "#9a7aa0")[i], face="frown", brows="sad")
        body += circle(x - 12, wy + wh - 14, 3.2, "#c8a060", 'stroke="#7a5a2a" stroke-width="1"')  # The biscuit
    for i in range(30):
        x, y = wx + 6 + (i * 37) % (ww - 12), wy + 4 + (i * 23) % (wh - 16)
        body += stroke(f"M{x} {y} l -3 9", "#d8e8f8", 1.2, 'opacity="0.75"')
    body += rect(wx, wy, ww, wh, "none", 'stroke="#1a0e06" stroke-width="6"')
    body += stroke(f"M{wx + ww / 2} {wy} L{wx + ww / 2} {wy + wh} M{wx} {wy + wh / 2} L{wx + ww} {wy + wh / 2}", "#1a0e06", 3)
    # The union boss: bib on, a claw at his mouth
    body += person(196, 150, 1.25, SKIN[1], "#4a4a5a", face="grin", hair="#3a2a1a", tie="#8a1e1e")
    body += poly([(178, 96), (214, 96), (222, 140), (170, 140)], "#ffffff", 'stroke="#999" stroke-width="1.5"')
    body += ellipse(196, 120, 9, 5, "#d0342c") + stroke("M187 118 l -6 -6 M205 118 l 6 -6", "#d0342c", 2.5)
    body += stroke("M222 132 C 232 116 226 100 214 92", "#4a4a5a", 10) + ellipse(212, 88, 9, 5, "#d0342c", 'transform="rotate(-30 212 88)"')
    # The CEO, beaming, a hand on his shoulder
    body += person(300, 150, 1.15, SKIN[0], "#1a1a1a", face="grin", hair=OLD_HAIR, tie="#c0392b")
    body += stroke("M272 118 C 256 108 240 106 226 108", "#1a1a1a", 10) + circle(224, 108, 6, SKIN[0])
    # The table: the lobster, the champagne, the nameplate
    body += rect(0, 138, W, 62, "#6a3a1a") + rect(0, 136, W, 6, "#8a4e24")
    body += ellipse(262, 156, 36, 9, "#f4f4f4", 'stroke="#bbb" stroke-width="1"')
    body += ellipse(262, 150, 22, 8, "#d0342c") + stroke("M240 150 L232 146 M240 152 L232 156", "#d0342c", 3)
    body += ellipse(290, 146, 8, 5, "#d0342c") + ellipse(288, 156, 7, 4, "#d0342c")
    for k in range(3):
        body += stroke(f"M{250 + k * 8} 144 L{250 + k * 8} 156", "#a8221a", 1.2)
    body += poly([(152, 116), (164, 116), (158, 132)], "#fff2b0", 'stroke="#c8b060" stroke-width="1"') + stroke("M158 132 L158 144 M152 144 L164 144", "#c8c8c8", 1.6)
    body += rect(60, 152, 112, 18, "#d4af37", 'stroke="#8a6a1a" stroke-width="1.5"')
    body += lettering("WORKS COUNCIL", 116, 157, 8, "#2a1a0a", width=1.4)
    return svg(defs, body)


def ushanka(hx, hy, r):
    # A big fluffy fur hat: a round crown, a thick folded-up front and ear flaps hanging down
    fur, edge = "#8a6040", 'stroke="#4a301a" stroke-width="2"'
    out = ellipse(hx - r * 1.1, hy + r * 0.35, r * 0.42, r * 0.8, fur, edge) + ellipse(hx + r * 1.1, hy + r * 0.35, r * 0.42, r * 0.8, fur, edge)
    out += ellipse(hx, hy - r * 1.05, r * 1.25, r * 0.95, "#6a4428", edge)
    for k in range(7):
        out += circle(hx - r * 1.2 + k * r * 0.4, hy - r * 0.55, r * 0.3, fur, edge)
    out += rect(hx - r * 1.25, hy - r * 0.8, r * 2.5, r * 0.5, fur)
    out += poly(star_points(hx, hy - r * 0.6, r * 0.24, r * 0.1, 5), "#d0342c", 'stroke="#7a1a1a" stroke-width="1"')
    return out


def russian_roulette():
    defs = lin("bg", ["#5a1a1a", "#1a0a0a"])
    body = rect(0, 0, W, H, "url(#bg)")
    # Onion domes in the snow
    for x, h in ((26, 60), (60, 80), (326, 66)):
        body += rect(x - 10, H - h, 20, h, "#2a1010") + path(f"M{x - 16} {H - h} C {x - 16} {H - h - 20} {x} {H - h - 24} {x} {H - h - 36} C {x} {H - h - 24} {x + 16} {H - h - 20} {x + 16} {H - h} Z", "#3a1414")
    for i in range(20):
        body += circle((i * 53) % W, (i * 37) % 170, 1.6, "#ffffff", 'opacity="0.6"')
    # The wheel: six slices, one of them a skull
    cx, cy, r = 236, 100, 74
    for i in range(6):
        a1, a2 = math.radians(-90 + i * 60), math.radians(-30 + i * 60)
        color = "#c0392b" if i % 2 else "#1a1a1a"
        body += path(f"M{cx} {cy} L{cx + r * math.cos(a1):.1f} {cy + r * math.sin(a1):.1f} A {r} {r} 0 0 1 {cx + r * math.cos(a2):.1f} {cy + r * math.sin(a2):.1f} Z", color, 'stroke="#d4af37" stroke-width="2"')
    am = math.radians(-60)
    body += skull(cx + 46 * math.cos(am), cy + 46 * math.sin(am) - 4, 0.9)
    body += circle(cx, cy, r, "none", 'stroke="#d4af37" stroke-width="5"') + circle(cx, cy, 10, "#d4af37")
    body += poly([(cx - 9, 14), (cx + 9, 14), (cx, 32)], "#f2efe6", 'stroke="#555" stroke-width="1.5"')
    body += path(f"M{cx - 92} {cy + 30} A 96 96 0 0 1 {cx - 70} {cy - 66}", "none", 'stroke="#ffffff" stroke-width="3" opacity="0.6"')
    # The lady who spins it
    body += person(86, 200, 1.35, SKIN[0], "#2a4a8a", face="smirk", brows="angry")
    hx, hy = 86, 200 - 64 * 1.35
    body += ushanka(hx, hy, 20 * 1.35)
    body += circle(hx - 9, hy + 6, 4, "#ff6a6a", 'opacity="0.6"') + circle(hx + 9, hy + 6, 4, "#ff6a6a", 'opacity="0.6"')
    body += stroke("M112 160 C 132 140 150 132 166 128", SKIN[0], 9)
    return svg(defs, body)


def toxic_positivity():
    defs, body = bg_vapor()
    body += circle(176, 100, 74, "#ffe45a", 'stroke="#c9952a" stroke-width="4"')
    body += ellipse(150, 80, 8, 14, "#2a1a0a") + ellipse(202, 80, 8, 14, "#2a1a0a")
    body += path("M126 112 Q 176 172 226 112", "none", 'stroke="#2a1a0a" stroke-width="7"')
    body += path("M196 30 L186 66 L204 84 L188 120", "none", 'stroke="#2a1a0a" stroke-width="4"')
    body += rect(112, 128, 44, 16, "#f4c8a0", 'rx="6" transform="rotate(-25 134 136)"')
    return svg(defs, body)


def mental_health_day():
    # A smiling politician cuts the ribbon on a MENTAL HEALTH DAY banner in front of a boarded-up clinic (CLOSED: BUDGET
    # CUTS); the only help on offer is a reiki-and-ayurveda lady waving burning sage over an exhausted office worker, her
    # price board beside her
    defs = lin("bg", ["#9ab4c8", "#e0d0b8"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 176, W, 24, "#8a8a8a")
    # The clinic, boarded up
    body += rect(0, 46, 150, 130, "#c8b8a0") + rect(0, 46, 150, 6, "#a89880")
    body += rect(14, 54, 110, 18, "#ffffff", 'stroke="#666" stroke-width="1"') + lettering("CLINIC", 69, 58, 10, "#2a5a8a")
    body += rect(50, 104, 46, 72, "#6a4a3a")
    for y, a in ((118, -12), (146, 10)):
        body += rect(40, y, 66, 10, "#b08a5a", f'stroke="#6a4a2a" stroke-width="1" transform="rotate({a} 73 {y + 5})"')
    body += rect(8, 84, 30, 26, "#4a5a6a") + stroke("M8 84 L38 110 M38 84 L8 110", "#b08a5a", 5)
    body += rect(104, 84, 42, 40, "#fff7a0", 'stroke="#888" stroke-width="1"')
    body += lettering("CLOSED", 125, 88, 6, "#c0392b", width=1.2) + lettering("BUDGET", 125, 100, 6, "#c0392b", width=1.2)
    body += lettering("CUTS", 125, 112, 6, "#c0392b", width=1.2)
    # The banner
    body += stroke("M0 14 L52 22 M300 22 L352 14", "#555", 1.5)
    body += rect(52, 10, 248, 26, "#2a7ad0", 'stroke="#1a4a8a" stroke-width="2"')
    body += lettering("MENTAL HEALTH DAY", 176, 16, 12, "#ffffff", width=2)
    # The politician cutting the ribbon
    body += stroke("M14 152 L128 152", "#d0342c", 4) + stroke("M134 152 L144 162", "#d0342c", 4)
    body += person(170, 200, 1.1, SKIN[0], "#1a2a4a", face="grin", hair="#5a4a3a", tie="#c0392b")
    body += stroke("M150 172 C 144 164 138 158 136 154", "#1a2a4a", 9)
    body += stroke("M136 154 L118 146 M136 154 L118 160", "#b8b8b8", 3) + circle(136, 154, 3, "#555")
    # The exhausted worker, and the lady with the sage
    body += person(240, 200, 0.95, SKIN[2], "#d8d8e8", face="flat", brows="sad", tie="#3a3a5a") + sweat(256, 134)
    body += person(312, 200, 1.05, SKIN[0], "#8a4aa0", face="smile", hat="bob")
    for k, color in enumerate(("#c080ff", "#80e0ff", "#ff9ad0")):
        body += poly([(300 + k * 10, 162), (304 + k * 10, 156), (308 + k * 10, 162), (304 + k * 10, 170)], color, 'stroke="#555" stroke-width="0.8"')
    body += stroke("M290 168 C 280 156 274 146 270 138", "#8a4aa0", 9)
    body += stroke("M270 138 L258 118", "#6a7a4a", 5) + circle(257, 116, 3, "#ff8a3a")
    for k in range(3):
        body += path(f"M{256 - k * 4} {110 - k * 10} q -8 -6 0 -10 q 8 -4 0 -10", "none", 'stroke="#d8d8d8" stroke-width="2.5" opacity="0.8"')
    body += rect(306, 52, 42, 40, "#fff7e0", 'stroke="#6a2a8a" stroke-width="1.5"')
    body += lettering("AURA", 327, 58, 7, "#6a2a8a", width=1.3) + lettering("90 EUR", 327, 74, 6, "#6a2a8a", width=1.2)
    return svg(defs, body)


# ---------------------------------------------------------------- Backgrounds of the factions without a deck yet

def bg_digital():
    """Digital Society: the glow of a feed, app blue to notification pink, with faint floating bubbles."""
    defs = lin("bg", ["#2a3d8f", "#7a3aa0", "#e0457b"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i, (x, y) in enumerate(((30, 30), (300, 40), (60, 150), (320, 150), (180, 20), (120, 80))):
        body += circle(x, y, 10 + i % 3 * 4, "#ffffff", 'opacity="0.1"')
    return defs, body


def bg_shadow():
    """Shadow Ops: a noir night, a searchlight sweeping over dark rooftops."""
    defs = lin("bg", ["#0b1020", "#1d2740"]) + lin("beam", [("#fff7c8", 0.28), ("#fff7c8", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    body += poly([(300, 0), (330, 0), (200, 200), (90, 200)], "url(#beam)")
    for x, w, h in ((0, 60, 40), (56, 40, 60), (92, 70, 34), (160, 50, 52), (206, 64, 30), (266, 40, 58), (302, 50, 40)):
        body += rect(x, H - h, w, h, "#05070d")
    return defs, body


def bg_tech():
    """Tech: a circuit board, green traces on dark."""
    defs = lin("bg", ["#0d2a24", "#06120f"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(9):
        y, x = 12 + i * 22, 40 + (i * 37) % 160
        body += stroke(f"M0 {y} L{x} {y} L{x + 16} {y + 12} L{W} {y + 12}", "#1f6b4f", 2, 'opacity="0.7"')
        body += circle(x, y, 3, "#3fbf8a", 'opacity="0.8"')
    return defs, body


def bg_myth():
    """Myth: temple light, gold through a purple dusk, between two columns."""
    defs = lin("bg", ["#3a1f5c", "#a8742a"]) + rad("halo", [("#ffe9a0", 0.7), ("#ffe9a0", 0)])
    body = rect(0, 0, W, H, "url(#bg)") + circle(176, 70, 110, "url(#halo)")
    for x in (24, 300):
        body += rect(x, 30, 28, 170, "#e8dcc0", 'opacity="0.3"') + rect(x - 6, 24, 40, 10, "#e8dcc0", 'opacity="0.3"')
    return defs, body


def bg_mind():
    """Mind: a dreamy pastel haze with a slow spiral."""
    defs = lin("bg", ["#b9a6e8", "#f4c6d6"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += stroke("M176 100 m-10 0 a10 10 0 1 1 20 0 a20 20 0 1 1 -40 0 a30 30 0 1 1 60 0 a40 40 0 1 1 -80 0 "
                   "a50 50 0 1 1 100 0 a60 60 0 1 1 -120 0", "#ffffff", 3, 'opacity="0.25"')
    return defs, body


# ---------------------------------------------------------------- Small props shared by the cards below

def skull(cx, cy, s=1.0, color="#f2efe6"):
    return (circle(cx, cy, 16 * s, color) + rect(cx - 9 * s, cy + 8 * s, 18 * s, 12 * s, color, 'rx="3"')
            + circle(cx - 6 * s, cy, 4.5 * s, "#222") + circle(cx + 6 * s, cy, 4.5 * s, "#222")
            + poly([(cx, cy + 4 * s), (cx - 3 * s, cy + 9 * s), (cx + 3 * s, cy + 9 * s)], "#222"))


def phone(cx, cy, s=1.0, screen="#4fc3ff"):
    return (rect(cx - 12 * s, cy - 22 * s, 24 * s, 44 * s, "#1b1b22", 'rx="4"')
            + rect(cx - 10 * s, cy - 18 * s, 20 * s, 34 * s, screen, 'rx="2"'))


def padlock(cx, cy, s=1.0, color="#d4af37"):
    return (stroke(f"M{cx - 6 * s:.1f} {cy:.1f} L{cx - 6 * s:.1f} {cy - 6 * s:.1f} C {cx - 6 * s:.1f} {cy - 14 * s:.1f} "
                   f"{cx + 6 * s:.1f} {cy - 14 * s:.1f} {cx + 6 * s:.1f} {cy - 6 * s:.1f} L{cx + 6 * s:.1f} {cy:.1f}", "#888", 3 * s)
            + rect(cx - 9 * s, cy - 2 * s, 18 * s, 14 * s, color, 'rx="2"') + circle(cx, cy + 4 * s, 2 * s, "#222"))


def popup(x, y, w=64, h=34, bar="#d0342c"):
    return (rect(x, y, w, h, "#ffffff", 'rx="4" stroke="#333" stroke-width="1.5"') + rect(x, y, w, 9, bar, 'rx="2"')
            + rect(x + 6, y + 15, w - 12, 4, "#999") + rect(x + 6, y + 23, w * 0.6, 4, "#999"))


def tank(cx, cy, s=1.0, color="#5a6a3a"):
    return (rect(cx - 30 * s, cy - 8 * s, 60 * s, 16 * s, color, 'rx="6"') + rect(cx - 16 * s, cy - 20 * s, 30 * s, 14 * s, color, 'rx="4"')
            + rect(cx + 12 * s, cy - 16 * s, 30 * s, 5 * s, color) + ellipse(cx, cy + 9 * s, 32 * s, 6 * s, "#2a2a2a"))


def envelope(cx, cy, s=1.0, color="#f4f4f4"):
    return (rect(cx - 10 * s, cy - 7 * s, 20 * s, 14 * s, color, 'stroke="#555" stroke-width="1"')
            + stroke(f"M{cx - 10 * s:.1f} {cy - 7 * s:.1f} L{cx:.1f} {cy + 1 * s:.1f} L{cx + 10 * s:.1f} {cy - 7 * s:.1f}", "#555", 1))


def boat(cx, cy, s=1.0):
    return (path(f"M{cx - 16 * s:.1f} {cy:.1f} L{cx + 16 * s:.1f} {cy:.1f} L{cx + 10 * s:.1f} {cy + 8 * s:.1f} L{cx - 10 * s:.1f} {cy + 8 * s:.1f} Z", "#2a5aa0")
            + stroke(f"M{cx:.1f} {cy:.1f} L{cx:.1f} {cy - 20 * s:.1f}", "#5a3a22", 2 * s)
            + poly([(cx + 1 * s, cy - 20 * s), (cx + 14 * s, cy - 4 * s), (cx + 1 * s, cy - 4 * s)], "#ffffff", 'stroke="#555" stroke-width="1"'))


# ---------------------------------------------------------------- Corporate, Statecraft and Zeitgeist cards added later


def ceasefire():
    defs, body = bg_state("#3a6a3a")
    body += rect(56, 16, 240, 34, "#f4ecd8", 'stroke="#555" stroke-width="2"')
    body += path("M160 34 C 168 24 182 24 186 30 C 196 26 204 30 200 36 C 190 38 176 44 164 40 Z", "#ffffff", 'stroke="#555" stroke-width="1.5"')
    body += stroke("M186 34 C 200 40 210 38 220 42", "#5a8a3a", 3)
    for x, y in ((24, 24), (330, 30)):
        body += poly(star_points(x, y, 12, 4, 6), "#fff7c0")
    for cx, flip in ((116, 1), (236, -1)):
        body += person(cx, 196, 1.2, SKIN[0 if flip > 0 else 2], "#4a5a2e", face="grin", hat="peaked")
        body += circle(cx - flip * 44, 168, 10, "#3a4a2a") + rect(cx - flip * 44 - 3, 154, 6, 6, "#888") + circle(cx - flip * 44 + 6, 154, 3, "none", 'stroke="#888" stroke-width="2"')
    body += stroke("M146 166 L206 166", "#4a5a2e", 12) + ellipse(176, 164, 14, 9, SKIN[1])
    return svg(defs, body)


def diplomat():
    defs, body = bg_state("#2a3a8a")
    for x, hair in ((60, OLD_HAIR), (292, "#7a6a5a")):
        body += rect(x - 30, 20, 60, 70, "#d4af37", 'stroke="#8a6a1a" stroke-width="3"') + rect(x - 24, 26, 48, 58, "#2a2a3a")
        body += person(x, 84, 0.6, SKIN[0], "#1a1a3a", face="flat", hair=hair, tie="#d0342c")
    body += rect(130, 60, 92, 110, "#5a2e1a", 'rx="12"')
    body += person(176, 170, 1.2, SKIN[0], "#1a1a3a", face="smirk", hair="#f2c94c", tie="#d0342c")
    body += rect(0, 160, W, 40, "#5a3a22")
    body += ellipse(240, 154, 16, 8, "#111") + ellipse(268, 150, 16, 8, "#111")
    body += stroke("M200 140 L240 150 M206 136 L268 146", "#1a1a3a", 10)
    body += rect(96, 172, 70, 14, "#d4af37", 'stroke="#8a6a1a" stroke-width="1.5"')
    return svg(defs, body)


def delivery_cyclist():
    defs, body = bg_vapor()
    for i, color in enumerate(("#e84a4a", "#4a8ad0", "#f2c94c", "#5aaa5a", "#aa5ad0")):
        x = 10 + i * 70
        body += rect(x, 158, 62, 22, color, 'rx="8"') + rect(x + 12, 146, 36, 14, color, 'rx="5"') + circle(x + 14, 182, 7, "#222") + circle(x + 48, 182, 7, "#222")
    for wx in (140, 210):
        body += circle(wx, 110, 18, "none", 'stroke="#111" stroke-width="5"')
    body += stroke("M140 110 L170 88 L210 110 M170 88 L160 72 M190 70 L200 82 L170 88", "#e0e0e0", 4)
    body += rect(116, 44, 44, 40, "#ff7a1a", 'rx="4" stroke="#8a3a0a" stroke-width="2"')
    body += person(170, 88, 0.7, SKIN[1], "#3aa0e0", face="smile", hat="helmet")
    body += phone(196, 64, 0.6, "#ffffff")
    for k in range(5):
        body += poly(star_points(190 + k * 3.2, 62, 2.4, 1, 5), "#f2c94c" if k == 0 else "#bbb")
    body += speed_lines(100, 90, 3, 30)
    return svg(defs, body)


# ---------------------------------------------------------------- Digital Society cards

def chronically_online():
    defs = lin("bg", ["#0a0c18", "#141a30"]) + rad("glow", [("#5ab0ff", 0.45), ("#5ab0ff", 0)])
    body = rect(0, 0, W, H, "url(#bg)") + rect(50, 6, 252, 54, "#a8743a", 'stroke="#5a3a1a" stroke-width="3"')
    pins = ((70, 18), (140, 30), (210, 16), (280, 36), (110, 48), (250, 50))
    for (x1, y1), (x2, y2) in zip(pins, pins[2:] + pins[:2]):
        body += stroke(f"M{x1} {y1} L{x2} {y2}", "#d0342c", 1.5)
    for x, y in pins:
        body += rect(x - 10, y - 7, 20, 14, "#f4f4f4") + circle(x, y - 7, 2.5, "#d0342c")
    body += circle(176, 120, 120, "url(#glow)")
    for x in (96, 176, 256):
        body += rect(x - 34, 70, 68, 46, "#111", 'stroke="#333" stroke-width="3"') + rect(x - 30, 74, 60, 38, "#4fa8ff", 'opacity="0.85"')
    body += person(176, 200, 1.1, "#e8e4f0", "#2a2a2a", face="flat")
    for row, count in enumerate((4, 3, 2, 1)):
        for k in range(count):
            body += rect(282 + k * 12 + row * 6, 186 - row * 16, 10, 16, "#5adf5a", 'stroke="#1a5a1a" stroke-width="1"')
    return svg(defs, body)


def digital_nomad():
    defs = lin("bg", ["#7ab8e8", "#cfe4f2"]) + lin("sand", ["#f0b070", "#d8884a"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += path("M0 130 C 60 110 120 120 170 132 C 230 116 300 108 352 126 L352 200 L0 200 Z", "#e8a868")
    body += path("M0 160 C 90 140 200 150 352 156 L352 200 L0 200 Z", "url(#sand)")
    # A Tuareg in a white turban and veil, cross-legged on the dune, laptop on his knees
    robe = "#e6dcc4"
    body += path("M96 196 C 100 160 130 150 176 150 C 222 150 252 160 256 196 Z", robe, 'stroke="#a89878" stroke-width="2"')
    body += path("M140 150 C 136 110 150 92 176 92 C 202 92 216 110 212 150 Z", robe, 'stroke="#a89878" stroke-width="2"')
    for x in (156, 170, 184, 198):
        body += stroke(f"M{x} 104 L{x - 4} 150", "#c8bca0", 1.5)
    body += stroke("M176 96 L176 140", "#8a6a4a", 2)
    body += circle(176, 70, 22, "#8a5a3a")
    body += path("M150 66 C 150 34 202 34 202 66 C 196 52 156 52 150 66 Z", "#f6f2ea", 'stroke="#c8c0b0" stroke-width="2"')
    body += path("M152 60 C 160 44 192 44 200 60 L200 66 L152 66 Z", "#f6f2ea")
    body += path("M152 74 C 156 96 196 96 200 74 L200 90 C 190 104 162 104 152 90 Z", "#f6f2ea", 'stroke="#c8c0b0" stroke-width="2"')
    body += stroke("M156 54 Q 176 48 196 54 M154 80 Q 176 88 198 80", "#d8d0c0", 1.5)
    body += ellipse(167, 67, 3, 2, "#222") + ellipse(185, 67, 3, 2, "#222")
    body += stroke("M162 62 L171 63 M181 63 L190 62", "#3a2a1a", 1.8)
    body += rect(150, 160, 52, 4, "#9aa0a8") + path("M154 160 L148 128 L204 128 L198 160 Z", "#c8ccd4", 'stroke="#555" stroke-width="2"')
    body += path("M156 156 L152 132 L200 132 L196 156 Z", "#3a6ad0")
    body += stroke("M128 168 C 140 158 148 156 154 158 M224 168 C 212 158 204 156 198 158", "#8a5a3a", 6)
    return svg(defs, body)


def its_a_trap():
    defs = lin("bg", ["#8a9ab0", "#5a6a80"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(250, 30, 96, 140, "#3a4250") + "".join(rect(256 + k * 14, 50, 4, 120, "#9aa2b0") for k in range(6))
    body += rect(244, 14, 108, 22, "#f4ecd8") + boat(298, 26, 0.5)
    body += person(206, 150, 1.1, SKIN[0], "#2a3a6a", face="grin", hat="peaked", brows="angry")
    body += rect(130, 140, 140, 60, "#6a4a2a", 'stroke="#3a2a1a" stroke-width="3"')
    for k in range(4):
        body += group(rect(-18, -12, 36, 24, "#ffffff", 'stroke="#555" stroke-width="1"') + stroke("M-12 -4 L12 -4 M-12 4 L6 4", "#d0342c", 2), f"translate({176 + k * 2} {136 - k * 5}) rotate({k * 5 - 6})")
    body += person(70, 200, 1.4, "#f7d33a", "#ffffff", face="grin", beard="#b0a070")
    body += stroke("M96 150 L120 104", "#f7d33a", 10)
    body += rect(104, 74, 40, 30, "#ffffff", 'stroke="#555" stroke-width="2"') + boat(124, 92, 0.6)
    return svg(defs, body)


def npc():
    defs = lin("bg", ["#c8c8c8", "#8a8a8a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for row, (y, s) in enumerate(((120, 0.7), (160, 0.85), (204, 1.0))):
        for k in range(6):
            x = 30 + k * 60 + (row % 2) * 30
            if row == 2 and k == 2:
                continue
            body += person(x, y, s, "#d0d0d0", "#7a7a7a", face="flat")
    for x, y in ((40, 14), (150, 8), (260, 16)):
        body += rect(x, y, 72, 26, "#ffffff", 'rx="8" stroke="#555" stroke-width="1.5"') + stroke(f"M{x + 8} {y + 10} L{x + 64} {y + 10} M{x + 8} {y + 18} L{x + 46} {y + 18}", "#777", 3)
        body += poly([(x + 20, y + 26), (x + 30, y + 26), (x + 20, y + 36)], "#ffffff")
    body += person(176, 204, 1.0, "#d0d0d0", "#7a7a7a", face="smile")
    body += sign(206, 60, 70, 40, "#ffffff")
    body += stroke("M180 72 L232 72 M180 82 L222 82 M180 92 L228 92", "#d0342c", 3)
    return svg(defs, body)


# ---------------------------------------------------------------- Mind cards


def introvert():
    defs, body = bg_mind()
    for i, (x, y) in enumerate(((40, 120), (300, 110), (70, 60), (290, 50))):
        body += person(x, y + 60, 0.8, SKIN[i % 4], ("#e84a4a", "#4a8ad0", "#f2c94c", "#aa5ad0")[i], face="shout")
        body += stroke(f"M{x + 20} {y} C {x + 50} {y - 10} {x + 60} {y + 10} {120 if x < 176 else 232} {y + 24}", "#ffffff", 2, 'stroke-dasharray="4 4"')
    body += poly([(100, 196), (176, 60), (252, 196)], "#e8a0b8", 'stroke="#a05a7a" stroke-width="4"')
    body += poly([(150, 196), (176, 120), (202, 196)], "#5a3a5a")
    body += person(176, 196, 0.7, SKIN[1], "#4a6a8a", face="closed")
    body += path("M160 140 C 160 120 192 120 192 140", "none", 'stroke="#222" stroke-width="4"') + circle(160, 146, 6, "#222") + circle(192, 146, 6, "#222")
    body += rect(162, 172, 28, 18, "#f4ecd8", 'stroke="#555" stroke-width="1"')
    for x, y in ((120, 40), (240, 30), (60, 20)):
        body += circle(x, y, 4, "#ffffff") + stroke(f"M{x + 4} {y} L{x + 4} {y - 14}", "#ffffff", 2)
    return svg(defs, body)


def pessimist():
    defs, body = bg_sky(top="#4ac0f0", bottom="#ffe8a8")
    body += rect(0, 160, W, 40, "#f6e6b4")
    body += cloud(176, 30, 0.9, "#6a6a7a", 1)
    for k in range(6):
        body += stroke(f"M{150 + k * 10} 46 L{146 + k * 10} 70", "#4a8ad0", 2)
    body += person(176, 196, 1.3, SKIN[0], "#f2c94c", face="frown")
    body += path("M152 106 C 152 80 200 80 200 106 Z", "#f2c94c", 'stroke="#b08a1a" stroke-width="2"') + ellipse(176, 106, 40, 8, "#f2c94c", 'stroke="#b08a1a" stroke-width="2"')
    body += stroke("M200 160 L236 140", "#f2c94c", 10)
    body += rect(226, 112, 46, 30, "#ffffff", 'stroke="#555" stroke-width="2"') + poly(star_points(249, 127, 9, 4), "#d4af37")
    return svg(defs, body)


# ---------------------------------------------------------------- Myth cards

def exorcism():
    defs, body = bg_myth()
    body += rect(176, 140, 160, 40, "#e8dcc0", 'stroke="#8a7a5a" stroke-width="3"') + rect(176, 130, 40, 20, "#ffffff", 'rx="6"')
    body += person(250, 176, 0.9, SKIN[0], "#4a6a8a", face="shout")
    body += person(84, 200, 1.3, SKIN[1], "#1a1a1a", face="frown", brows="angry")
    body += rect(78, 124, 12, 8, "#ffffff")
    body += stroke("M108 150 L138 100", "#1a1a1a", 10) + rect(134, 66, 8, 44, "#d4af37") + rect(124, 78, 28, 7, "#d4af37")
    body += circle(250, 70, 40, "#ff3a3a", 'opacity="0.25"') + phone(250, 72, 1.0, "#ff3a3a")
    for i, (x, y) in enumerate(((210, 40), (290, 36), (224, 104), (300, 98), (250, 20))):
        body += circle(x, y, 8 - i % 2 * 2, "#d0342c") + poly([(x - 3, y - 4), (x, y - 12), (x + 3, y - 4)], "#d0342c")
    return svg(defs, body)


def martyr():
    defs, body = bg_myth()
    body += ellipse(140, 46, 34, 9, "none", 'stroke="#ffd94a" stroke-width="5"') + rect(164, 34, 22, 14, "#ffe066", 'rx="2" stroke="#8a6a1a" stroke-width="1"')
    body += path("M110 200 L120 110 L160 110 L170 200 Z", "#f4f4f4")
    body += stroke("M118 120 L50 98 M162 120 L230 98", "#f4f4f4", 14)
    body += person(140, 140, 1.1, SKIN[0], "#f4f4f4", face="smile", hair="#6a4a2a", beard="#6a4a2a")
    body += path("M118 70 C 112 100 116 120 122 130 L130 80 Z M162 70 C 168 100 164 120 158 130 L150 80 Z", "#6a4a2a")
    body += rect(240, 60, 96, 70, "#ffffff", 'rx="6" stroke="#555" stroke-width="2"') + rect(240, 60, 96, 14, "#3a8a3a", 'rx="4"')
    body += stroke("M262 92 L270 92 L278 112 L306 112 L312 96 L274 96", "#333", 3) + circle(282, 118, 3, "#333") + circle(302, 118, 3, "#333")
    return svg(defs, body)


def memento_mori():
    defs, body = bg_myth()
    body += rect(220, 20, 90, 80, "#f4ecd8", 'stroke="#555" stroke-width="2"') + rect(220, 20, 90, 14, "#c0392b")
    for r in range(3):
        for c in range(4):
            x, y = 228 + c * 21, 40 + r * 19
            body += rect(x, y, 16, 15, "#ffffff", 'stroke="#bbb" stroke-width="1"')
            if r == 0 and c < 3:
                body += stroke(f"M{x + 2} {y + 2} L{x + 14} {y + 13} M{x + 14} {y + 2} L{x + 2} {y + 13}", "#d0342c", 2.5)
    body += rect(0, 150, W, 50, "#5a3a22")
    body += skull(120, 120, 1.8)
    body += rect(196, 92, 40, 6, "#8a6a2a") + rect(196, 144, 40, 6, "#8a6a2a")
    body += path("M200 98 L232 98 L216 120 L232 144 L200 144 L216 120 Z", "#e8f4ff", 'stroke="#8a6a2a" stroke-width="2"')
    body += poly([(206, 140), (226, 140), (216, 128)], "#e8c070") + poly([(210, 102), (222, 102), (216, 110)], "#e8c070")
    return svg(defs, body)


def mummy():
    defs, body = bg_myth()
    body += person(110, 200, 1.4, "#d9cfa8", "#d9cfa8", face="flat")
    for k in range(6):
        body += stroke(f"M70 {120 + k * 14} L150 {114 + k * 14}", "#a89870", 3)
    body += circle(102, 108, 4, "#3a2a1a") + circle(118, 108, 4, "#3a2a1a")
    body += person(256, 200, 1.3, SKIN[0], "#e84a4a", face="grin", hat="cap")
    body += stroke("M140 156 L224 156", "#d9cfa8", 12) + ellipse(196, 156, 10, 8, SKIN[0])
    body += stroke("M196 150 C 210 140 220 166 232 150 C 244 136 250 160 262 150", "#d9cfa8", 4)
    body += rect(276, 150, 22, 16, "#333", 'rx="3"') + circle(287, 158, 5, "#88c")
    body += group(rect(-30, -14, 60, 28, "#f4ecd8", 'stroke="#555" stroke-width="2"') + rect(-24, -6, 48, 4, "#d0342c") + rect(-24, 2, 34, 4, "#999"), "translate(56 186) rotate(-70)")
    return svg(defs, body)


# ---------------------------------------------------------------- Shadow Ops. cards

def burn_notice():
    defs, body = bg_shadow()
    body += rect(116, 14, 120, 90, "#d4af37", 'stroke="#8a6a1a" stroke-width="5"') + rect(126, 24, 100, 70, "#f4ecd8")
    body += person(156, 92, 0.6, "#111", "#111", face="flat") + stroke("M168 70 L196 40", "#555", 1.5)
    body += path("M196 30 C 186 20 202 14 204 24 C 206 14 222 20 212 30 L204 40 Z", "#d0342c")
    for k in range(9):
        body += rect(128 + k * 11, 104, 8, 26 + (k % 3) * 8, "#f4ecd8", 'stroke="#bbb" stroke-width="0.8"')
    for i, x in enumerate((40, 90, 260, 310)):
        body += person(x, 210, 0.9, "#141824", "#141824", face="flat")
    body += person(176, 214, 1.0, SKIN[0], "#3a3226", face="frown", hat="fedora")
    return svg(defs, body)


def counter_intelligence():
    defs = lin("bg", ["#e8c890", "#a8784a"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 0, 60, H, "#8a6a3a", 'opacity="0.5"') + rect(292, 0, 60, H, "#8a6a3a", 'opacity="0.5"')
    for cx, flip in ((96, 1), (256, -1)):
        body += person(cx, 200, 1.3, SKIN[0 if flip > 0 else 2], "#f4f4f4", face="flat", glasses="round", brows="angry", tie="#2a3a6a")
        body += stroke(f"M{cx + flip * 20} 160 L{cx + flip * 64} 130", "#f4f4f4", 10)
        body += group(rect(-12, -16, 24, 32, "#333", 'rx="3"') + rect(-9, -12, 18, 7, "#9ad0a0") + "".join(rect(-9 + c * 7, -2 + r * 6, 5, 4, "#ccc") for r in range(3) for c in range(3)),
                      f"translate({cx + flip * 70} 126) rotate({flip * 70})")
    for x, y, a in ((150, 30, 20), (200, 50, -30), (176, 90, 10), (120, 70, -15), (230, 24, 35)):
        body += group(rect(-10, -7, 20, 14, "#ffffff", 'stroke="#999" stroke-width="1"') + stroke("M-6 -2 L6 -2 M-6 2 L4 2", "#3a8a3a", 1.5), f"translate({x} {y}) rotate({a})")
    return svg(defs, body)


# ---------------------------------------------------------------- Tech cards


def script_kiddie():
    defs, body = bg_tech()
    body += rect(210, 30, 120, 80, "#111", 'stroke="#333" stroke-width="3"') + circle(270, 70, 30, "#888", 'stroke="#555" stroke-width="4"')
    body += path("M270 40 L300 40 L300 100 L270 100 Z", "#666") + "".join(rect(250 + k * 10, 92, 8, 6, "#f2c94c") for k in range(4))
    body += rect(30, 30, 120, 80, "#111", 'stroke="#333" stroke-width="3"') + rect(36, 36, 108, 68, "#0a1a0a")
    body += skull(90, 64, 0.9, "#5adf5a") + rect(60, 90, 60, 8, "#5adf5a", 'opacity="0.5"')
    body += path("M120 200 L120 120 C 120 96 232 96 232 120 L232 200 Z", "#c0392b", 'stroke="#111" stroke-width="3"')
    body += person(176, 200, 1.0, SKIN[0], "#3a3a4a", face="grin", hat="hood")
    body += stroke("M196 176 L240 166", "#3a3a4a", 7) + ellipse(246, 166, 8, 10, "#ddd")
    return svg(defs, body)


# ---------------------------------------------------------------- Lettering
# Unity's SVG import draws no <text>, so words are drawn as strokes: each letter is polylines on a 4 x 6 grid

STROKE_FONT = {
    "A": [[(0, 6), (2, 0), (4, 6)], [(1, 3.6), (3, 3.6)]],
    "B": [[(0, 6), (0, 0), (3, 0), (4, 1), (4, 2), (3, 3), (0, 3)], [(3, 3), (4, 4), (4, 5), (3, 6), (0, 6)]],
    "C": [[(4, 1), (3, 0), (1, 0), (0, 1), (0, 5), (1, 6), (3, 6), (4, 5)]],
    "D": [[(0, 0), (0, 6), (3, 6), (4, 5), (4, 1), (3, 0), (0, 0)]],
    "E": [[(4, 0), (0, 0), (0, 6), (4, 6)], [(0, 3), (3, 3)]],
    "F": [[(4, 0), (0, 0), (0, 6)], [(0, 3), (3, 3)]],
    "G": [[(4, 1), (3, 0), (1, 0), (0, 1), (0, 5), (1, 6), (3, 6), (4, 5), (4, 3), (2, 3)]],
    "H": [[(0, 0), (0, 6)], [(4, 0), (4, 6)], [(0, 3), (4, 3)]],
    "I": [[(1, 0), (3, 0)], [(2, 0), (2, 6)], [(1, 6), (3, 6)]],
    "J": [[(4, 0), (4, 5), (3, 6), (1, 6), (0, 5)]],
    "K": [[(0, 0), (0, 6)], [(4, 0), (0, 3.4), (4, 6)]],
    "L": [[(0, 0), (0, 6), (4, 6)]],
    "M": [[(0, 6), (0, 0), (2, 3), (4, 0), (4, 6)]],
    "N": [[(0, 6), (0, 0), (4, 6), (4, 0)]],
    "O": [[(1, 0), (3, 0), (4, 1), (4, 5), (3, 6), (1, 6), (0, 5), (0, 1), (1, 0)]],
    "P": [[(0, 6), (0, 0), (3, 0), (4, 1), (4, 2), (3, 3), (0, 3)]],
    "Q": [[(1, 0), (3, 0), (4, 1), (4, 5), (3, 6), (1, 6), (0, 5), (0, 1), (1, 0)], [(2.4, 4.4), (4, 6.2)]],
    "R": [[(0, 6), (0, 0), (3, 0), (4, 1), (4, 2), (3, 3), (0, 3)], [(2, 3), (4, 6)]],
    "S": [[(4, 1), (3, 0), (1, 0), (0, 1), (0, 2), (1, 3), (3, 3), (4, 4), (4, 5), (3, 6), (1, 6), (0, 5)]],
    "T": [[(0, 0), (4, 0)], [(2, 0), (2, 6)]],
    "U": [[(0, 0), (0, 5), (1, 6), (3, 6), (4, 5), (4, 0)]],
    "V": [[(0, 0), (2, 6), (4, 0)]],
    "W": [[(0, 0), (1, 6), (2, 3), (3, 6), (4, 0)]],
    "X": [[(0, 0), (4, 6)], [(4, 0), (0, 6)]],
    "Y": [[(0, 0), (2, 3), (4, 0)], [(2, 3), (2, 6)]],
    "Z": [[(0, 0), (4, 0), (0, 6), (4, 6)]],
    "!": [[(2, 0), (2, 4)], [(2, 5.6), (2, 6)]],
}


def lettering(text, cx, top, height, color, width=None, outline=None):
    """Words drawn as strokes, centred on cx. An outline (a darker, wider stroke underneath) keeps them readable on busy art."""
    unit = height / 6
    width = width or max(1.5, height / 7)
    advance = 5.6 * unit
    x0 = cx - (len(text) * advance - 1.6 * unit) / 2
    d = ""
    for i, ch in enumerate(text.upper()):
        for line in STROKE_FONT.get(ch, []):
            d += "M" + " L".join(f"{x0 + i * advance + x * unit:.1f} {top + y * unit:.1f}" for x, y in line) + " "
    if not d:
        return ""
    under = stroke(d, outline, width * 2.2) if outline else ""
    return under + stroke(d, color, width)


# ---------------------------------------------------------------- Redrawn cards

def dog(cx, cy, s, fur, ear, face=1, muzzle=None):
    line = 'stroke="#3a2a1a" stroke-width="2"'
    f = face
    out = stroke(f"M{cx - f * 32 * s:.1f} {cy - 4 * s:.1f} Q {cx - f * 46 * s:.1f} {cy - 10 * s:.1f} {cx - f * 44 * s:.1f} {cy - 24 * s:.1f}", fur, 6 * s)
    for dx in (-22, -10, 12, 24):
        out += rect(cx + f * dx * s - 4 * s, cy + 8 * s, 8 * s, 14 * s, fur, f'rx="3" {line}')
    out += ellipse(cx, cy, 36 * s, 18 * s, fur, line)
    out += circle(cx + f * 32 * s, cy - 14 * s, 16 * s, fur, line)
    out += ellipse(cx + f * 44 * s, cy - 8 * s, 10 * s, 7 * s, muzzle or fur, line)
    out += circle(cx + f * 52 * s, cy - 11 * s, 3.5 * s, "#222") + circle(cx + f * 36 * s, cy - 19 * s, 2.6 * s, "#222")
    out += ellipse(cx + f * 22 * s, cy - 16 * s, 6 * s, 12 * s, ear, line)
    out += ellipse(cx + f * 44 * s, cy - 1 * s, 3 * s, 4.5 * s, "#e86a8a")
    return out


def dogpile():
    defs, body = bg_digital()
    body += rect(54, 150, 10, 40, SKIN[0]) + circle(59, 146, 9, SKIN[0])
    for k in range(4):
        body += rect(51 + k * 4.5, 128, 3.5, 16, SKIN[0], 'rx="1.5"')
    body += stroke("M40 128 Q 34 136 40 144 M78 128 Q 84 136 78 144", "#ffffff", 2, 'opacity="0.7"')
    body += dog(120, 166, 1.0, "#e8a04a", "#a8602a") + dog(232, 168, 1.0, "#2a2a2a", "#111", face=-1) + dog(176, 172, 0.9, "#f2d28a", "#c8a050")
    body += dog(150, 132, 0.9, "#c88a3a", "#7a4a1a", face=-1) + dog(208, 130, 0.9, "#f2d28a", "#c8a050")
    body += dog(176, 96, 0.8, "#e8d8b0", "#3a2a1a", muzzle="#3a2a1a")
    return svg(defs, body)


def keyboard_warrior():
    defs, body = bg_digital()
    body += path("M110 96 C 70 150 80 196 90 200 L262 200 C 272 196 282 150 242 96 Z", "#c0392b")
    body += ellipse(176, 176, 84, 60, "#5a6a7a") + ellipse(176, 186, 60, 40, "#6e7e8e") + ellipse(176, 178, 6, 4, "#3a4a5a")
    body += circle(150, 168, 7, "#8a7a5a", 'opacity="0.6"') + circle(200, 196, 5, "#8a7a5a", 'opacity="0.6"')
    body += rect(108, 192, 136, 8, "#2a2a2a")
    for k in range(3):
        body += rect(118 + k * 18, 186, 10, 14, "#5adf5a", 'stroke="#1a5a1a" stroke-width="1"')
    body += ellipse(176, 112, 30, 14, SKIN[0])
    body += circle(176, 88, 28, SKIN[0])
    body += path("M146 90 C 146 50 206 50 206 90", "none", 'stroke="#222" stroke-width="7"') + rect(138, 80, 14, 22, "#222", 'rx="4"') + rect(200, 80, 14, 22, "#222", 'rx="4"')
    body += circle(166, 84, 3, "#222") + circle(186, 84, 3, "#222") + ellipse(176, 100, 7, 6, "#5a1a1a")
    body += stroke("M158 74 L170 79 M194 74 L182 79", "#222", 3)
    body += stroke("M112 150 C 100 160 104 176 120 178", SKIN[0], 14)
    body += stroke("M240 150 L262 112", "#5a6a7a", 16)
    blade = rect(-10, -96, 20, 92, "#2a2a2a", 'rx="3" stroke="#111" stroke-width="2"')
    blade += "".join(rect(-6 + c * 7, -90 + r * 8, 5, 6, "#d8d8d8", 'rx="1"') for r in range(11) for c in range(2))
    blade += rect(-20, -4, 40, 8, "#555", 'rx="3"') + rect(-4, 4, 8, 22, "#222") + stroke("M0 26 C 6 34 -6 40 2 48", "#222", 2)
    body += group(blade, "translate(264 112) rotate(18)")
    body += circle(262, 112, 9, SKIN[0])
    return svg(defs, body)


def inflation():
    defs, body = bg_office(chart="up")
    defs += rad("shine", [("#ffffff", 0.7), ("#ffffff", 0)])
    body += stroke("M176 152 C 170 166 184 176 176 190", "#ddd", 2)
    body += path("M176 14 C 222 14 244 52 244 82 C 244 116 210 144 176 150 C 142 144 108 116 108 82 C 108 52 130 14 176 14 Z", "#d0342c", 'stroke="#8a1a1a" stroke-width="3"')
    body += poly([(170, 150), (182, 150), (176, 158)], "#8a1a1a")
    body += ellipse(150, 52, 18, 26, "url(#shine)")
    body += circle(176, 84, 26, "#f2c94c", 'stroke="#a87a1a" stroke-width="3"') + circle(176, 84, 18, "none", 'stroke="#a87a1a" stroke-width="2"')
    body += stroke("M176 72 L176 96 M170 76 Q 182 76 182 82 Q 170 84 170 90 Q 182 90 182 94", "#a87a1a", 3)
    for x, y in ((92, 40), (266, 54), (84, 112), (272, 120)):
        body += stroke(f"M{x} {y} L{x + (10 if x > 176 else -10)} {y - 6}", "#ffffff", 2, 'opacity="0.6"')
    return svg(defs, body)


def cleaning_robot():
    # A smart-home robot vacuum in the style of a Google Nest (a soft fabric puck with four coloured light dots), its
    # camera eye glowing, sucking up the family's photos, letters and diary while a beam uploads a floor plan of the house
    # to a cloud. Its pop-up: FREE CLEANING. YOU'RE THE PRODUCT
    defs = lin("bg", ["#f0e8dc", "#d8ccb8"]) + lin("floor", ["#b08a5a", "#8a6a42"]) + lin("beam", [("#8ac8ff", 0.05), ("#8ac8ff", 0.5)])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 128, W, 72, "url(#floor)")
    for k in range(6):
        body += stroke(f"M0 {134 + k * 12} L{W} {134 + k * 12}", "#7a5a34", 1.2, 'opacity="0.6"')
    body += rect(0, 124, W, 6, "#f4ecd8")
    # The upload: a beam from the robot to a cloud, a floor plan of the house riding it
    body += poly([(196, 140), (214, 140), (300, 40), (270, 40)], "url(#beam)")
    body += cloud(286, 34, 1.2)
    body += group(rect(-20, -14, 40, 28, "#2a5aa0", 'stroke="#ffffff" stroke-width="1.5"')
                  + stroke("M-20 -2 L4 -2 L4 14 M4 -14 L4 -6 M-6 -2 L-6 14", "#ffffff", 1.4), "translate(246 84) rotate(-12)")
    # What it swallows: photos, letters, the diary
    for x, y, a in ((40, 152, -10), (74, 140, 14)):
        body += group(rect(-11, -9, 22, 18, "#ffffff", 'stroke="#888" stroke-width="1"') + rect(-8, -6, 16, 10, "#8ac8ff")
                      + circle(0, -1, 3, SKIN[0]), f"translate({x} {y}) rotate({a})")
    body += envelope(60, 178, 1.1) + envelope(108, 182, 0.9)
    body += group(rect(-10, -13, 20, 26, "#c0392b", 'stroke="#6a1a1a" stroke-width="1"') + path("M0 4 C -6 -2 -2 -8 0 -3 C 2 -8 6 -2 0 4 Z", "#ffd0d8"),
                  "translate(28 182) rotate(-20)")
    body += speed_lines(126, 140, 3, 22, "#ffffff")
    # The robot: a fabric puck with the four dots and a camera eye
    body += ellipse(200, 182, 76, 10, "#000000", 'opacity="0.2"')
    body += path("M128 158 C 128 182 272 182 272 158 L272 168 C 272 192 128 192 128 168 Z", "#bfb8ac")
    body += ellipse(200, 158, 72, 20, "#e8e4dc", 'stroke="#bfb8ac" stroke-width="2"')
    for k, color in enumerate(("#4285f4", "#ea4335", "#fbbc05", "#34a853")):
        body += circle(182 + k * 12, 156, 4, color)
    body += circle(150, 176, 12, "#ff3a3a", 'opacity="0.25"') + circle(150, 176, 7, "#1a1a1a") + circle(150, 176, 3.2, "#ff3a3a")
    # The pop-up
    body += rect(16, 26, 150, 40, "#ffffff", 'rx="5" stroke="#333" stroke-width="1.5"') + rect(16, 26, 150, 9, "#4285f4", 'rx="3"')
    body += lettering("FREE CLEANING.", 91, 40, 7, "#222", width=1.3)
    body += lettering("YOU'RE THE PRODUCT", 91, 52, 6, "#ea4335", width=1.2)
    body += path("M120 66 L132 66 L150 120 Z", "#ffffff", 'stroke="#333" stroke-width="1.5"')
    return svg(defs, body)


def generational_trauma():
    defs = lin("bg", ["#9ad8f0", "#e8f6ff"]) + lin("water", ["#4aa8d8", "#1a5a8a"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(0, 120, W, 80, "url(#water)") + rect(0, 118, W, 5, "#d8f4ff")
    body += person(96, 186, 1.1, SKIN[0], "#4a4a5a", face="frown", hair=OLD_HAIR)
    body += rect(0, 120, 200, 4, "#d8f4ff", 'opacity="0.6"')
    body += stroke("M74 136 L66 66 M118 136 L126 66", SKIN[0], 9)
    # The Boomer, held up high and dry, glances down at the skeletons and couldn't care less (smug, half-lidded, cocktail in hand)
    body += person(96, 76, 0.86, SKIN[0], "#e86a4a", face="smirk", hair=OLD_HAIR)
    hx, hy, s = 96, 76 - 64 * 0.86, 0.86
    for ex in (hx - 7 * s, hx + 7 * s):
        body += ellipse(ex, hy - 2 * s, 4.4 * s, 3.4 * s, "#ffffff") + circle(ex + 2.2 * s, hy - 0.6 * s, 2.3 * s, "#222")
        body += stroke(f"M{ex - 4.5 * s:.1f} {hy - 3.6 * s:.1f} L{ex + 4.5 * s:.1f} {hy - 3 * s:.1f}", SKIN[0], 2.6 * s)
    body += stroke("M116 66 L132 56", "#e86a4a", 6)
    body += poly([(128, 40), (148, 40), (138, 52)], "#bfe8ff", 'stroke="#555" stroke-width="1"') + stroke("M138 52 L138 62 M132 62 L144 62", "#555", 1.5)
    body += path("M140 38 C 142 26 156 26 158 34 Z", "#ff5a8a") + stroke("M149 32 L144 44", "#555", 1)
    body += lettering("BOOMER", 230, 22, 14, "#ffffff", outline="#7a3a2a")
    body += lettering("SILENT", 160, 170, 11, "#ffffff", outline="#1a3a5a")
    for x, name in ((226, "GEN X"), (274, "GEN Y"), (322, "GEN Z")):
        body += skull(x, 164, 0.75) + stroke(f"M{x} 178 L{x} 194 M{x - 8} 183 L{x + 8} 183 M{x - 8} 189 L{x + 8} 189", "#f2efe6", 2)
        body += lettering(name, x, 134, 8, "#ffffff", outline="#1a3a5a")
    return svg(defs, body)


def rage_bait():
    defs, body = bg_water()
    body += stroke("M176 0 L176 70", "#e8e8e8", 2)
    body += path("M176 70 L176 104 C 176 120 196 120 196 106 L196 100", "none", 'stroke="#9aa2b0" stroke-width="4"') + poly([(196, 98), (192, 106), (200, 104)], "#9aa2b0")
    body += circle(176, 86, 24, "#e8402a", 'stroke="#8a1a0a" stroke-width="3"')
    body += stroke("M162 78 L172 83 M190 78 L180 83", "#2a0a0a", 3) + circle(168, 86, 3, "#2a0a0a") + circle(184, 86, 3, "#2a0a0a")
    body += path("M166 100 Q 176 92 186 100", "none", 'stroke="#2a0a0a" stroke-width="3"')
    body += circle(196, 66, 9, "#ffffff", 'stroke="#d0342c" stroke-width="2"') + rect(195, 60, 2, 7, "#d0342c") + rect(195, 69, 2, 2, "#d0342c")
    body += path("M40 140 C 70 110 120 112 140 134 L156 122 L152 156 L140 146 C 118 168 70 168 40 140 Z", "#7a9a5a", 'stroke="#3a5a2a" stroke-width="2"')
    body += circle(70, 134, 5, "#ffffff") + circle(71, 135, 2.5, "#111") + stroke("M62 124 L76 128", "#111", 2.5)
    body += path("M40 140 L56 132 L56 150 Z", "#3a1a1a")
    return svg(defs, body)


def the_holy_grail():
    defs, body = bg_myth()
    for i in range(12):
        a = math.radians(i * 30)
        body += stroke(f"M{176 + 50 * math.cos(a):.1f} {84 + 50 * math.sin(a):.1f} L{176 + 120 * math.cos(a):.1f} {84 + 120 * math.sin(a):.1f}", "#fff3b0", 3, 'opacity="0.35"')
    body += rect(136, 150, 80, 50, "#8a8070", 'stroke="#5a5040" stroke-width="2"') + rect(128, 144, 96, 10, "#a89c88")
    body += path("M140 40 L212 40 C 212 82 196 98 182 102 L182 124 L200 136 L152 136 L170 124 L170 102 C 156 98 140 82 140 40 Z", "#ffd94a", 'stroke="#a8801a" stroke-width="3"')
    body += ellipse(176, 40, 36, 8, "#e8b830", 'stroke="#a8801a" stroke-width="2"') + rect(146, 60, 60, 6, "#e84a6a", 'opacity="0.7"')
    body += stroke("M152 50 C 156 70 162 82 170 90", "#fff7c0", 3, 'opacity="0.8"')
    for x, y in ((110, 40), (244, 60), (230, 24), (120, 100)):
        body += poly(star_points(x, y, 7, 2.5, 4), "#fff7c0")
    return svg(defs, body)


def sleeper_agent():
    defs = lin("bg", ["#1a2240", "#0c1020"]) + rad("screen", [("#7ab8ff", 0.5), ("#7ab8ff", 0)])
    body = rect(0, 0, W, H, "url(#bg)") + rect(250, 20, 70, 50, "#2a3460") + stroke("M285 20 L285 70 M250 45 L320 45", "#141a30", 3)
    body += circle(150, 70, 90, "url(#screen)")
    body += rect(96, 22, 112, 76, "#111", 'stroke="#333" stroke-width="3"') + rect(102, 28, 100, 64, "#3a7ad0") + rect(146, 98, 12, 18, "#333")
    body += rect(0, 140, W, 60, "#5a3a22")
    body += rect(110, 132, 120, 14, "#d8d8d8", 'rx="2"') + "".join(rect(116 + c * 11, 135, 8, 3, "#aaa") for c in range(10))
    body += path("M120 150 C 120 112 236 112 236 150 Z", "#3a3a4a")
    body += circle(176, 118, 24, SKIN[1]) + path("M152 112 C 156 92 196 92 200 112 Z", "#3a2a1a")
    body += stroke("M164 120 q 4 3 8 0 M180 120 q 4 3 8 0", "#222", 2) + ellipse(184, 132, 3, 4, "#8ad0ff", 'opacity="0.8"')
    body += path("M128 148 C 140 136 160 136 176 140 C 192 136 212 136 224 148 Z", "#3a3a4a")
    body += rect(262, 116, 22, 26, "#e8e8e8", 'rx="3"') + stroke("M284 122 C 294 122 294 136 284 136", "#e8e8e8", 3)
    body += lettering("Z", 222, 60, 16, "#ffffff") + lettering("Z", 242, 42, 12, "#ffffff") + lettering("Z", 258, 28, 9, "#ffffff")
    return svg(defs, body)


def eavesdrop():
    defs = lin("bg", ["#c89a5a", "#8a6232"])
    body = rect(0, 0, W, H, "url(#bg)")
    for k in range(7):
        y = 6 + k * 28
        body += stroke(f"M0 {y} L{W} {y}", "#6a4a22", 2.5) + stroke(f"M{(k * 97) % 300 + 20} {y} L{(k * 97) % 300 + 20} {y + 28}", "#6a4a22", 2)
    body += ellipse(176, 110, 64, 48, "#000000", 'opacity="0.18"')
    body += path("M150 60 C 150 30 216 26 222 70 C 226 100 204 112 200 132 C 196 152 168 160 158 142 C 150 128 168 124 172 110 C 176 98 150 92 150 60 Z", SKIN[0], 'stroke="#a8704a" stroke-width="3"')
    body += path("M168 62 C 170 46 204 44 206 70 C 208 88 192 94 188 108", "none", 'stroke="#c8885a" stroke-width="5"')
    body += path("M176 70 C 184 62 196 70 188 84", "none", 'stroke="#c8885a" stroke-width="4"')
    for r in (16, 28, 40):
        body += path(f"M{110 - r} {150} A{r} {r} 0 0 1 {110} {150 - r}", "none", 'stroke="#ffffff" stroke-width="3" opacity="0.5"')
    return svg(defs, body)


def political_decoy():
    defs, body = bg_state("#2a3a8a")
    body += stroke("M176 200 L176 60", "#8a5a2a", 8)
    body += path("M128 96 L224 96 L232 170 L120 170 Z", "#1a1a3a") + poly([(166, 96), (186, 96), (176, 130)], "#f4f4f4") + poly([(172, 100), (180, 100), (182, 140), (176, 148), (170, 140)], "#d0342c")
    body += stroke("M128 100 L84 120 M224 100 L268 120", "#1a1a3a", 16)
    for x, dx in ((80, -1), (272, 1)):
        for k in range(5):
            body += stroke(f"M{x} 122 L{x + dx * (8 + k * 3)} {112 + k * 6}", "#e8c860", 2.5)
    for k in range(5):
        body += stroke(f"M{160 + k * 8} 96 L{156 + k * 9} 86", "#e8c860", 2.5)
    body += ellipse(176, 64, 26, 30, "#d8b880", 'stroke="#8a6a3a" stroke-width="2"')
    body += stroke("M164 58 L170 62 M170 58 L164 62 M182 58 L188 62 M188 58 L182 62", "#3a2a1a", 2)
    body += path("M162 74 Q 176 84 190 74", "none", 'stroke="#3a2a1a" stroke-width="2" stroke-dasharray="3 3"')
    body += stroke("M152 46 L148 38 M162 38 L160 30 M176 36 L176 28 M190 38 L192 30 M200 46 L206 40", "#e8c860", 2.5)
    body += rect(126, 160, 100, 40, "#6a4a2a", 'stroke="#3a2a1a" stroke-width="3"') + poly(star_points(176, 180, 10, 4), "#f2c94c")
    return svg(defs, body)


def signal_jammer():
    defs = lin("bg", ["#1a2236", "#0c1020"])
    body = rect(0, 0, W, H, "url(#bg)")
    for k, r in enumerate((26, 52, 78)):
        body += path(f"M{176 - r * 1.1:.1f} {150 - r * 0.6:.1f} A{r * 1.25:.1f} {r * 1.25:.1f} 0 0 1 {176 + r * 1.1:.1f} {150 - r * 0.6:.1f}", "none", 'stroke="#c8d0e0" stroke-width="12" stroke-linecap="round"')
    body += circle(176, 156, 11, "#c8d0e0")
    body += circle(176, 110, 84, "none", 'stroke="#d0342c" stroke-width="12"') + stroke("M117 51 L235 169", "#d0342c", 12)
    return svg(defs, body)


def mole():
    defs = lin("bg", ["#a8d8f0", "#e8f6ff"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 120, W, 80, "#6a4a2a") + rect(0, 112, W, 12, "#5aaa4a")
    body += path("M96 200 C 100 130 252 130 256 200 Z", "#8a5a32")
    body += ellipse(176, 116, 50, 48, "#3a3238") + ellipse(176, 132, 40, 30, "#4a4248")
    body += circle(158, 104, 3.5, "#111") + circle(194, 104, 3.5, "#111")
    body += ellipse(176, 122, 12, 9, "#f2a0b0") + circle(172, 121, 2, "#a05a6a") + circle(180, 121, 2, "#a05a6a")
    for x, flip in ((128, -1), (224, 1)):
        body += ellipse(x, 150, 18, 12, "#f2a0b0", 'stroke="#c87a8a" stroke-width="1.5"')
        for k in range(4):
            body += stroke(f"M{x + flip * 6 + (k - 1.5) * 7:.1f} 158 L{x + flip * 10 + (k - 1.5) * 8:.1f} 168", "#f4ecd8", 3)
    for x, y in ((112, 172), (240, 176), (200, 186)):
        body += circle(x, y, 4, "#6a4a2a")
    return svg(defs, body)


def deep_cover():
    defs, body = bg_deep()
    body += path("M60 110 C 90 70 200 64 250 96 C 270 110 270 130 250 140 C 200 166 100 160 60 128 L 22 148 L 30 110 L 18 80 Z", "#1c2638")
    body += path("M190 104 C 220 100 250 106 268 116 C 256 128 230 136 200 132 Z", "#0a0f1a")
    for x in range(198, 262, 10):
        body += poly([(x, 108), (x + 3, 124), (x + 6, 108)], "#f5f0e0")
    body += poly([(206, 130), (209, 114), (212, 130)], "#f5f0e0") + poly([(236, 130), (239, 112), (242, 130)], "#f5f0e0")
    for k in range(8):
        body += circle(70 + k * 20, 140 - (k % 2) * 4, 2.5, "#5ad0ff")
    body += circle(214, 92, 7, "#ffffff") + circle(216, 93, 4, "#1b2a40")
    body += stroke("M120 76 C 110 50 130 40 150 46", "#2a3a50", 3) + circle(150, 46, 6, "#5ad0ff") + circle(150, 46, 14, "#5ad0ff", 'opacity="0.25"')
    return svg(defs, body)


def smoke_and_mirrors():
    defs = lin("bg", ["#3a3f4a", "#22262e"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += circle(176, 100, 84, "#ffffff")
    body += rect(120, 96, 100, 18, "#f4f4f4", 'stroke="#555" stroke-width="2"') + rect(196, 96, 24, 18, "#d88a3a") + rect(116, 96, 6, 18, "#ff6a2a")
    body += stroke("M116 92 C 104 80 126 72 112 58 C 102 48 120 40 112 30", "#9aa2b0", 5, 'opacity="0.8"')
    body += circle(176, 100, 84, "none", 'stroke="#d0342c" stroke-width="14"') + stroke("M117 41 L235 159", "#d0342c", 14)
    return svg(defs, body)


def tapped_communications():
    defs = lin("bg", ["#e8d8b0", "#c8a870"])
    body = rect(0, 0, W, H, "url(#bg)")
    # The other side of the call: a server, across a lightning split
    body += poly([(200, 0), (W, 0), (W, H), (150, H)], "#0e1a24")
    for i in range(6):
        body += stroke(f"M{196 + i * 10} 0 L{146 + i * 10} 200", "#1a3a4a", 1, 'opacity="0.3"')
    body += server_rack(240, 24, 84, 156)
    body += circle(282, 14, 6, "#ff3a3a")
    body += poly([(206, 0), (176, 70), (196, 74), (160, 140), (182, 142), (146, 200), (162, 200), (204, 132), (184, 128), (220, 62), (198, 58), (224, 0)], "#ffe23a", 'stroke="#ffffff" stroke-width="2"')
    # A guy on the phone, chatting away
    body += person(84, 200, 1.4, SKIN[1], "#5a7a3a", face="grin", hair="#3a2a1a")
    body += rect(104, 88, 14, 30, "#1b1b22", 'rx="4" transform="rotate(15 111 103)"')
    body += stroke("M118 150 L112 112", SKIN[1], 9)
    for k in range(3):
        body += path(f"M{126 + k * 8} {80 - k * 4} A {10 + k * 8} {10 + k * 8} 0 0 1 {126 + k * 8} {110 + k * 4}", "none", 'stroke="#3a2a1a" stroke-width="2.5"')
    return svg(defs, body)


def raised_security_level():
    defs, body = bg_state("#e08a1a")
    colors = ("#3a9a4a", "#3a6ad0", "#f1c40f", "#e67e22", "#c0392b")
    for i, color in enumerate(colors):
        a1, a2 = math.radians(180 + i * 36), math.radians(180 + (i + 1) * 36)
        body += path(f"M176 150 L{176 + 110 * math.cos(a1):.1f} {150 + 110 * math.sin(a1):.1f} A110 110 0 0 1 {176 + 110 * math.cos(a2):.1f} {150 + 110 * math.sin(a2):.1f} Z", color, 'stroke="#333" stroke-width="2"')
    body += circle(176, 150, 46, "#e3d3a4", 'stroke="#333" stroke-width="2"')
    a = math.radians(180 + 160)
    body += stroke(f"M176 150 L{176 + 100 * math.cos(a):.1f} {150 + 100 * math.sin(a):.1f}", "#222", 7) + circle(176, 150, 10, "#222")
    body += arrow(310, 170, 310, 70, "#c0392b", 12)
    body += rect(0, 172, W, 28, "#4a4f2e")
    return svg(defs, body)


def antivirus():
    defs, body = bg_tech()
    body += ellipse(176, 176, 70, 40, "#f4f4f4", 'stroke="#222" stroke-width="2"') + ellipse(128, 178, 18, 22, "#111") + ellipse(224, 178, 18, 22, "#111")
    body += circle(132, 50, 18, "#111") + circle(220, 50, 18, "#111")
    body += circle(176, 92, 56, "#f4f4f4", 'stroke="#222" stroke-width="2"')
    body += group(ellipse(0, 0, 14, 20, "#111"), "translate(154 88) rotate(-25)") + group(ellipse(0, 0, 14, 20, "#111"), "translate(198 88) rotate(25)")
    body += circle(156, 86, 5, "#ffffff") + circle(196, 86, 5, "#ffffff") + circle(157, 87, 2.5, "#111") + circle(195, 87, 2.5, "#111")
    body += ellipse(176, 110, 9, 6, "#111") + path("M166 120 Q 176 128 186 120", "none", 'stroke="#111" stroke-width="2.5"')
    return svg(defs, body)


def lost_landmine():
    defs = lin("bg", ["#cfe4f0", "#f4f8fa"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 168, W, 32, "#c8a878")
    body += poly([(116, 36), (176, 8), (236, 36)], "#e8eef4", 'stroke="#8a9aa8" stroke-width="2"') + rect(170, 2, 12, 10, "#2a5aa0")
    body += rect(116, 36, 120, 150, "#f8fbff", 'stroke="#8a9aa8" stroke-width="2"') + rect(116, 36, 120, 22, "#2a5aa0")
    body += lettering("MISSING", 176, 66, 13, "#d0342c")
    body += rect(136, 90, 80, 60, "#e8e0d0", 'stroke="#555" stroke-width="2"')
    body += ellipse(176, 132, 28, 9, "#5a5a3a") + ellipse(176, 126, 21, 7, "#8a5a2a") + circle(176, 122, 5, "#3a3a2a")
    body += stroke("M136 162 L216 162 M136 172 L200 172", "#999", 3)
    body += path("M248 40 C 256 52 264 60 264 70 C 264 82 248 82 248 70 C 248 60 248 52 248 40 Z", "#2a5aa0", 'opacity="0.3"')
    return svg(defs, body)


def legacy_code():
    defs = lin("bg", ["#3a2a1a", "#1a120a"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(60, 30, 232, 140, "#e8d4a0", 'stroke="#a8884a" stroke-width="2"')
    for x in (60, 292):
        body += ellipse(x, 100, 14, 76, "#c8a868", 'stroke="#8a6a3a" stroke-width="3"') + ellipse(x, 100, 5, 70, "#a8884a")
    glyphs = ("M0 0 L6 8 L0 16", "M6 0 L0 8 L6 16", "M0 8 L10 8", "M2 0 C 10 4 -4 12 8 16", "M0 0 L0 16 M0 8 L8 8", "M4 0 A4 4 0 1 1 4 8 L4 16")
    for row in range(5):
        x = 86
        for k in range(9 - (row % 3)):
            g = glyphs[(row * 7 + k * 3) % len(glyphs)]
            body += group(path(g, "none", 'stroke="#5a3a1a" stroke-width="2"'), f"translate({x} {44 + row * 24})")
            x += 20 + (k % 3) * 2
    for x, y in ((90, 160), (250, 40), (270, 150)):
        body += stroke(f"M{x} {y} l 8 -6 l 4 8 l 6 -4", "#8a6a3a", 1.5)
    return svg(defs, body)


def ddos_attack():
    defs, body = bg_tech()
    body += rect(76, 0, 200, 200, "#e8ecf0", 'rx="10" stroke="#9aa2b0" stroke-width="4"') + stroke("M76 80 L276 80", "#9aa2b0", 4)
    body += rect(254, 20, 8, 46, "#b8bcc4", 'rx="4"') + rect(254, 96, 8, 70, "#b8bcc4", 'rx="4"')
    body += rect(110, 18, 120, 50, "#111", 'rx="6"')
    for i in range(6):
        body += envelope(126 + (i % 3) * 44, 32 + (i // 3) * 22, 0.7, "#ff8a8a")
    body += circle(142, 112, 16, "#2a0a0a") + circle(210, 112, 16, "#2a0a0a") + circle(142, 112, 9, "#ff2a2a") + circle(210, 112, 9, "#ff2a2a")
    body += circle(142, 112, 20, "#ff2a2a", 'opacity="0.25"') + circle(210, 112, 20, "#ff2a2a", 'opacity="0.25"')
    body += stroke("M126 92 L156 100 M226 92 L196 100", "#2a2a2a", 5)
    for x, y, c in ((110, 160, "#f2c94c"), (232, 150, "#5aaa5a"), (160, 176, "#4a8ad0")):
        body += rect(x, y, 22, 16, c, 'rx="3"')
    for i in range(6):
        body += envelope(20 + (i * 53) % 330, 150 + (i * 17) % 40, 0.9)
    return svg(defs, body)


def end_to_end_encryption():
    defs = lin("bg", ["#000a04", "#00140a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for col in range(22):
        x = 8 + col * 16
        for row in range(10):
            y = 6 + row * 19 + (col * 7) % 12
            shade = 0.25 + ((col * 3 + row * 5) % 7) / 10
            d = ("M0 0 L6 0 L6 10", "M0 0 L0 10 L6 10", "M3 0 L3 10 M0 5 L6 5", "M0 0 L6 10", "M0 10 L6 0 M0 0 L3 4")[(col + row * 3) % 5]
            body += group(path(d, "none", f'stroke="#3aff7a" stroke-width="1.6" opacity="{min(shade, 1):.2f}"'), f"translate({x} {y})")
    body += person(176, 200, 1.5, SKIN[1], "#0a0a0a", face="flat", glasses="shades", tie="#0a0a0a")
    body += circle(176, 104, 30, "#3aff7a", 'opacity="0.12"')
    body += stroke("M156 101 L196 101", "#3aff7a", 2, 'opacity="0.7"')
    return svg(defs, body)


def plot_armor():
    defs, body = bg_vapor()
    defs += lin("steel", ["#e8ecf2", "#9aa2b0", "#6a7280"], 0, 0, 1, 1)
    body += path("M96 0 L256 0 L264 200 L88 200 Z", "url(#steel)", 'stroke="#4a505c" stroke-width="3"')
    body += path("M176 10 L176 190", "none", 'stroke="#5a6270" stroke-width="2" opacity="0.6"')
    for y in (150, 172):
        body += path(f"M100 {y} Q 176 {y + 12} 252 {y}", "none", 'stroke="#5a6270" stroke-width="3"')
    for x, y in ((112, 20), (240, 20), (112, 130), (240, 130)):
        body += circle(x, y, 4, "#5a6270")
    body += lettering("PLOT", 176, 70, 30, "#3a404c", width=6)
    body += stroke("M118 28 L140 50", "#ffffff", 4, 'opacity="0.5"')
    body += path("M0 0 L90 0 L108 60 L80 200 L0 200 Z", "#1c1c28") + path("M352 0 L262 0 L244 60 L272 200 L352 200 Z", "#1c1c28")
    body += path("M90 0 L108 60 L96 66 L78 0 Z", "#2c2c3c") + path("M262 0 L244 60 L256 66 L274 0 Z", "#2c2c3c")
    # The hands holding the jacket open, Superman style
    for side in (-1, 1):
        x = 176 + side * 84
        fist = (rect(-16, -20, 32, 40, SKIN[1], 'rx="10" stroke="#8a5a3a" stroke-width="2"')
                + stroke("M-14 -8 L6 -8 M-14 4 L6 4", "#8a5a3a", 1.8)
                + rect(4 if side < 0 else -20, -26, 16, 16, SKIN[1], 'rx="7" stroke="#8a5a3a" stroke-width="2"'))
        body += group(fist, f"translate({x} 96) rotate({side * 8})")
        body += path(f"M{x + side * 14} 116 L{x + side * 40} 200 L{x + side * 74} 200 L{x + side * 30} 110 Z", "#1c1c28")
    return svg(defs, body)


# ---------------------------------------------------------------- Effect icons
# One 128x128 cell per icon. "Cards > Build Effect Icons" in Unity reads each <g id="..." transform="translate(x y)">
# from the sheet to build the TextMeshPro sprite asset used as <sprite name="..."> in card text.

ICON = 128
ICON_COLUMNS = 8
OUTLINE = 'stroke="#1b1b22" stroke-width="5" stroke-linejoin="round"'


def icon_star4(color):
    return poly([(64, 8), (78, 50), (120, 64), (78, 78), (64, 120), (50, 78), (8, 64), (50, 50)], color, OUTLINE)


def icon_skull():
    return (path("M64 10 C 30 10 14 34 18 62 C 20 76 28 82 34 86 L 34 108 L 94 108 L 94 86 C 100 82 108 76 110 62 C 114 34 98 10 64 10 Z", "#f2efe6", OUTLINE)
            + ellipse(44, 60, 13, 15, "#1b1b22") + ellipse(84, 60, 13, 15, "#1b1b22")
            + poly([(64, 72), (56, 88), (72, 88)], "#1b1b22")
            + stroke("M50 100 L50 108 M64 100 L64 108 M78 100 L78 108", "#1b1b22", 4))


def icon_sword(angle):
    blade = (poly([(60, 10), (68, 10), (70, 82), (58, 82)], "#dfe5ee", OUTLINE)
             + rect(40, 80, 48, 10, "#c9a24a", f'rx="4" {OUTLINE}')
             + rect(58, 90, 12, 24, "#7a4a24", f'rx="3" {OUTLINE}'))
    return group(blade, f"rotate({angle} 64 64)")


def icon_heart(color, extra=""):
    return path("M64 112 C 28 86 8 64 12 40 C 16 18 44 10 64 34 C 84 10 112 18 116 40 C 120 64 100 86 64 112 Z", color, OUTLINE) + extra


def icon_shield(color):
    return path("M64 8 C 82 18 100 20 112 22 C 112 70 96 100 64 120 C 32 100 16 70 16 22 C 28 20 46 18 64 8 Z", color, OUTLINE) + \
        stroke("M64 26 L64 100 M36 50 L92 50", "#ffffff", 6, 'opacity="0.6"')


def icon_bolt(color):
    return poly([(70, 6), (26, 72), (58, 72), (46, 122), (102, 50), (68, 50), (86, 6)], color, OUTLINE)


def icon_arrow(color, up=True):
    pts = [(64, 10), (112, 60), (82, 60), (82, 118), (46, 118), (46, 60), (16, 60)]
    if not up:
        pts = [(x, 128 - y) for x, y in pts]
    return poly(pts, color, OUTLINE)


def icon_eye(iris):
    return (path("M6 64 C 30 24 98 24 122 64 C 98 104 30 104 6 64 Z", "#f2efe6", OUTLINE)
            + circle(64, 64, 24, iris, OUTLINE) + circle(64, 64, 10, "#1b1b22") + circle(56, 56, 5, "#ffffff"))


def build_icons():
    return {
        "enter": icon_star4("#ffd257"),
        "death": icon_skull(),
        "attack": icon_sword(-35) + icon_sword(35),
        "hurt": icon_heart("#d8434f", stroke("M60 34 L72 56 L56 70 L70 96", "#1b1b22", 6)),
        "dawn": circle(64, 84, 34, "#ffb347", OUTLINE) + rect(8, 84, 112, 36, "#5a6b8c", OUTLINE)
                + "".join(stroke(f"M{64 + 50 * math.cos(math.radians(a)):.1f} {84 - 50 * math.sin(math.radians(a)):.1f} L{64 + 62 * math.cos(math.radians(a)):.1f} {84 - 62 * math.sin(math.radians(a)):.1f}", "#ffb347", 7) for a in (30, 60, 90, 120, 150)),
        "dusk": path("M78 12 C 40 16 18 48 24 80 C 30 108 62 124 92 114 C 64 108 46 84 50 56 C 54 34 66 20 78 12 Z", "#cfd8ff", OUTLINE)
                + poly(star_points(96, 40, 12, 5, 5), "#fff1a8"),
        "damage": poly(star_points(64, 64, 58, 26, 8, -90), "#ff6a3a", OUTLINE) + poly(star_points(64, 64, 30, 14, 8, -67), "#ffd257"),
        "heal": icon_heart("#3fbf6a") + rect(56, 36, 16, 50, "#ffffff", 'rx="3"') + rect(39, 53, 50, 16, "#ffffff", 'rx="3"'),
        "draw": rect(20, 22, 60, 84, "#7fa7d9", f'rx="8" {OUTLINE} transform="rotate(-12 50 64)"')
                + rect(48, 22, 60, 84, "#b9d3f2", f'rx="8" {OUTLINE} transform="rotate(10 78 64)"'),
        "summon": circle(64, 64, 54, "#5b2fa0", OUTLINE)
                  + "".join(stroke(f"M64 64 m{r} 0 a{r} {r} 0 1 0 {-r} {r}", "#c9a8ff", 6) for r in (12, 26, 40)),
        "armor": icon_shield("#4f8fd6"),
        "paralyze": icon_bolt("#ffe14a"),
        "buff": icon_arrow("#ff9a2e"),
        "burn": path("M64 120 C 30 116 16 88 26 62 C 32 46 44 40 46 18 C 60 30 64 44 62 58 C 72 46 76 34 74 20 C 100 40 112 70 102 94 C 96 110 82 120 64 120 Z", "#ff6a1a", OUTLINE)
                + path("M64 112 C 48 110 42 96 48 84 C 52 76 58 72 60 62 C 72 74 80 86 78 98 C 76 106 70 112 64 112 Z", "#ffd257"),
        "poison": path("M64 8 C 80 40 108 60 108 86 C 108 108 88 122 64 122 C 40 122 20 108 20 86 C 20 60 48 40 64 8 Z", "#6bd04a", OUTLINE)
                  + circle(52, 82, 8, "#1b1b22") + circle(76, 82, 8, "#1b1b22"),
        "freeze": "".join(stroke(f"M{64 - 54 * math.cos(math.radians(a)):.1f} {64 - 54 * math.sin(math.radians(a)):.1f} L{64 + 54 * math.cos(math.radians(a)):.1f} {64 + 54 * math.sin(math.radians(a)):.1f}", "#1b1b22", 16) for a in (0, 60, 120))
                  + "".join(stroke(f"M{64 - 54 * math.cos(math.radians(a)):.1f} {64 - 54 * math.sin(math.radians(a)):.1f} L{64 + 54 * math.cos(math.radians(a)):.1f} {64 + 54 * math.sin(math.radians(a)):.1f}", "#bfe9ff", 8) for a in (0, 60, 120)),
        "chain": group(icon_bolt("#7fe7ff"), "translate(-18 0) scale(0.8)") + group(icon_bolt("#7fe7ff"), "translate(44 26) scale(0.65)"),
        "speed": poly([(14, 20), (58, 64), (14, 108), (34, 108), (78, 64), (34, 20)], "#5fd0ff", OUTLINE)
                 + poly([(54, 20), (98, 64), (54, 108), (74, 108), (118, 64), (74, 20)], "#5fd0ff", OUTLINE),
        "sword": icon_sword(0),
        "heart": icon_heart("#e0414f"),
        "debuff": icon_arrow("#a46ae0", up=False),
        # Doomed: an hourglass running out, sand the colour of dried blood
        "doom": rect(26, 8, 76, 12, "#5a3a24", f'rx="4" {OUTLINE}') + rect(26, 108, 76, 12, "#5a3a24", f'rx="4" {OUTLINE}')
                + path("M34 20 L94 20 C 94 46 72 56 66 64 C 72 72 94 82 94 108 L34 108 C 34 82 56 72 62 64 C 56 56 34 46 34 20 Z", "#e8eef5", OUTLINE)
                + poly([(48, 104), (80, 104), (64, 84)], "#9b1c2a") + poly([(54, 34), (74, 34), (64, 48)], "#9b1c2a"),
        # Blessed: a golden halo over a small four-point sparkle
        "bless": ellipse(64, 40, 46, 16, "none", 'stroke="#1b1b22" stroke-width="16"') + ellipse(64, 40, 46, 16, "none", 'stroke="#ffd94a" stroke-width="8"')
                 + poly([(64, 62), (72, 88), (98, 96), (72, 104), (64, 128), (56, 104), (30, 96), (56, 88)], "#fff1a8", OUTLINE),
        # Cleanse: a clear water drop with a shine
        "cleanse": path("M64 8 C 80 40 108 60 108 86 C 108 108 88 122 64 122 C 40 122 20 108 20 86 C 20 60 48 40 64 8 Z", "#8fd8ff", OUTLINE)
                   + path("M46 84 C 46 70 54 60 60 54", "none", 'stroke="#ffffff" stroke-width="8" stroke-linecap="round"'),
        # Heat Signature: a thermal-camera blob, hot core in cold surroundings
        "heat": rect(8, 8, 112, 112, "#2b2f6b", f'rx="18" {OUTLINE}') + ellipse(64, 66, 46, 50, "#d8434f")
                + ellipse(64, 70, 32, 36, "#ff9a2e") + ellipse(64, 74, 18, 20, "#ffe14a") + ellipse(64, 76, 7, 8, "#ffffff"),
        # Concealed: an eye struck through
        "conceal": icon_eye("#8a93a8") + stroke("M18 110 L110 18", "#1b1b22", 16) + stroke("M18 110 L110 18", "#d8434f", 8),
        # Revealed: a wide open eye
        "reveal": icon_eye("#ffd257"),
        # Days: a sand watch, for how long something lasts. Golden sand in a wooden frame (Doom's runs red)
        "days": rect(22, 6, 84, 14, "#a8743a", f'rx="5" {OUTLINE}') + rect(22, 108, 84, 14, "#a8743a", f'rx="5" {OUTLINE}')
                + stroke("M30 20 L30 108", "#1b1b22", 9) + stroke("M98 20 L98 108", "#1b1b22", 9)
                + stroke("M30 20 L30 108", "#c48a48", 4) + stroke("M98 20 L98 108", "#c48a48", 4)
                + path("M38 20 L90 20 C 90 44 72 54 67 64 C 72 74 90 84 90 108 L38 108 C 38 84 56 74 61 64 C 56 54 38 44 38 20 Z", "#dff1ff", OUTLINE)
                + path("M46 104 L82 104 C 80 92 70 86 64 80 C 58 86 48 92 46 104 Z", "#ffc93a")
                + poly([(50, 36), (78, 36), (64, 52)], "#ffc93a") + stroke("M64 54 L64 80", "#ffc93a", 3),
        # Encrypted: a padlock
        "encrypt": path("M38 58 L38 40 C 38 20 52 10 64 10 C 76 10 90 20 90 40 L90 58", "none", 'stroke="#1b1b22" stroke-width="20"')
                   + path("M38 58 L38 40 C 38 20 52 10 64 10 C 76 10 90 20 90 40 L90 58", "none", 'stroke="#b9c3d6" stroke-width="10"')
                   + rect(22, 56, 84, 64, "#c9a24a", f'rx="10" {OUTLINE}') + circle(64, 82, 9, "#1b1b22") + rect(60, 86, 8, 20, "#1b1b22"),
    }


def icon_sheet():
    icons = build_icons()
    rows = (len(icons) + ICON_COLUMNS - 1) // ICON_COLUMNS
    width, height = ICON_COLUMNS * ICON, rows * ICON
    body = ""
    for i, (name, content) in enumerate(icons.items()):
        x, y = (i % ICON_COLUMNS) * ICON, (i // ICON_COLUMNS) * ICON
        body += f'<g id="{name}" transform="translate({x} {y})">{content}</g>'
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="{width}" height="{height}">'
            f'{body}</svg>\n')


# ---------------------------------------------------------------- Fog of War card faces
# Whole cards (the card is 160 x 200), drawn at twice the size. Unity's SVG importer draws no text, so they have none.

CW, CH = 320, 400
BRASS = "#c9a24a"
NAVY = "#1a1f30"
INK = "#07080d"
CREAM = "#efe6cf"


def card_svg(defs, body):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {CW} {CH}" width="{CW}" height="{CH}">'
            f'<defs>{defs}</defs>{body}</svg>\n')


# A bold question mark (flat-cut hook end, tapering curve, round dot), traced from the reference the design was given,
# in its 600 x 600 box. Drawn at `scale` with the top-left of that box at (x, y)
QUESTION_MARK = ("M172 210 C 172 130 230 82 307 82 C 390 82 440 135 440 200 C 440 260 400 290 370 320 "
                 "C 350 340 340 360 340 398 L 262 398 L 262 370 C 262 320 280 290 315 255 "
                 "C 345 225 365 210 365 195 C 365 165 340 150 307 150 C 270 150 247 175 247 210 Z")


def question_mark(x, y, scale, color):
    return group(path(QUESTION_MARK, color) + circle(301, 481, 49, color), f"translate({x:.1f} {y:.1f}) scale({scale})")


def unknown_creature_svg():
    # Minimalist: a spell card's layout (no stats), every part of it empty, in soft greys, with three question marks
    # in the middle of the art box
    line = 'stroke="#2c2c2c" stroke-width="1.5"'
    body = rect(3, 3, CW - 6, CH - 6, "#161616", 'rx="16" stroke="#333333" stroke-width="3"')
    body += rect(18, 18, CW - 36, 40, "none", f'rx="4" {line}')       # Name
    body += rect(18, 64, CW - 36, 172, "none", f'rx="4" {line}')      # Art
    body += rect(18, 242, CW - 36, 30, "none", f'rx="3" {line}')      # Type
    body += rect(18, 278, CW - 36, 104, "none", f'rx="4" {line}')     # Text
    # Each mark spans x 172-440 and y 82-530 of its box
    scale, gap, middle = 0.2, 70, 64 + 172 / 2
    mark_w, mark_h = (440 - 172) * scale, (530 - 82) * scale
    left = (CW - (3 * mark_w + 2 * (gap - mark_w))) / 2
    for i in range(3):
        body += question_mark(left + i * gap - 172 * scale, middle - mark_h / 2 - 82 * scale, scale, "#9a9a9a")
    return card_svg("", body)


def card_frame():
    return (rect(3, 3, CW - 6, CH - 6, NAVY, f'rx="16" stroke="{BRASS}" stroke-width="6"')
            + rect(12, 12, CW - 24, CH - 24, "none", f'rx="10" stroke="{BRASS}" stroke-width="1.5" opacity="0.6"'))


def card_back_svg():
    defs = rad("glow", ["#2c3557", NAVY], cx=0.5, cy=0.47, r=0.6)
    body = card_frame() + rect(18, 18, CW - 36, CH - 36, "url(#glow)", f'rx="8" stroke="{BRASS}" stroke-width="2"')
    # A faint diamond lattice, like the table
    for row in range(9):
        for col in range(7):
            cx, cy = 40 + col * 40, 44 + row * 40
            body += poly([(cx, cy - 14), (cx + 14, cy), (cx, cy + 14), (cx - 14, cy)], "none", f'stroke="{BRASS}" stroke-width="1.5" opacity="0.16"')
    # Brass corner brackets
    for x, y, sx, sy in ((26, 26, 1, 1), (CW - 26, 26, -1, 1), (26, CH - 26, 1, -1), (CW - 26, CH - 26, -1, -1)):
        body += stroke(f"M{x} {y + 34 * sy} L{x} {y} L{x + 34 * sx} {y}", BRASS, 5)
    # The seal: a keyhole in a ringed medallion, with four points around it
    for angle in (0, 90, 180, 270):
        body += group(poly([(0, -96), (8, -76), (0, -70), (-8, -76)], BRASS), f"translate(160 200) rotate({angle})")
    body += circle(160, 200, 64, NAVY, f'stroke="{BRASS}" stroke-width="6"') + circle(160, 200, 52, "none", f'stroke="{BRASS}" stroke-width="2"')
    body += circle(160, 188, 17, INK) + poly([(150, 196), (170, 196), (178, 236), (142, 236)], INK)
    return card_svg(defs, body)


def write_fog_faces():
    folder = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "FogOfWar"))
    os.makedirs(folder, exist_ok=True)
    for name, draw in (("UnknownCreature", unknown_creature_svg), ("CardBack", card_back_svg)):
        with open(os.path.join(folder, f"{name}.svg"), "w", encoding="utf-8", newline="\n") as f:
            f.write(draw())


# ---------------------------------------------------------------- More lettering: digits and a few signs

STROKE_FONT.update({
    "0": [[(1, 0), (3, 0), (4, 1), (4, 5), (3, 6), (1, 6), (0, 5), (0, 1), (1, 0)], [(0.6, 5), (3.4, 1)]],
    "1": [[(1, 1.2), (2, 0), (2, 6)], [(1, 6), (3, 6)]],
    "2": [[(0, 1), (1, 0), (3, 0), (4, 1), (4, 2.2), (0, 6), (4, 6)]],
    "3": [[(0, 0), (4, 0), (2, 2.6), (3, 2.6), (4, 3.6), (4, 5), (3, 6), (1, 6), (0, 5)]],
    "4": [[(3, 6), (3, 0), (0, 4.2), (4, 4.2)]],
    "5": [[(4, 0), (0, 0), (0, 2.6), (3, 2.6), (4, 3.6), (4, 5), (3, 6), (0, 6)]],
    "6": [[(3.4, 0), (1, 0), (0, 1), (0, 5), (1, 6), (3, 6), (4, 5), (4, 3.8), (3, 2.8), (0, 2.8)]],
    "7": [[(0, 0), (4, 0), (1.4, 6)]],
    "8": [[(1, 0), (3, 0), (4, 1), (4, 2), (3, 3), (1, 3), (0, 2), (0, 1), (1, 0)],
          [(1, 3), (3, 3), (4, 4), (4, 5), (3, 6), (1, 6), (0, 5), (0, 4), (1, 3)]],
    "9": [[(4, 3.2), (1, 3.2), (0, 2.2), (0, 1), (1, 0), (3, 0), (4, 1), (4, 5), (3, 6), (0.6, 6)]],
    "$": [[(4, 1.2), (3, 0.6), (1, 0.6), (0, 1.6), (0.6, 2.6), (3.4, 3.4), (4, 4.4), (3, 5.4), (1, 5.4), (0, 4.8)], [(2, -0.4), (2, 6.4)]],
    "-": [[(0.6, 3), (3.4, 3)]],
    "=": [[(0.6, 2.2), (3.4, 2.2)], [(0.6, 3.8), (3.4, 3.8)]],
    ".": [[(1.8, 5.6), (2.2, 6)]],
    "/": [[(0, 6), (4, 0)]],
    "?": [[(0, 1), (1, 0), (3, 0), (4, 1), (4, 2), (2, 3.4), (2, 4.4)], [(2, 5.6), (2, 6)]],
    "+": [[(2, 1.5), (2, 4.5)], [(0.5, 3), (3.5, 3)]],
    "'": [[(2, 0), (2, 1.6)]],
})


# ---------------------------------------------------------------- Utopia

def bg_utopia():
    """Utopia: a clean white lab, a mint glow and a faint honeycomb (Hexagons)."""
    defs = lin("bg", ["#effcf8", "#9fd8cf"])
    body = rect(0, 0, W, H, "url(#bg)")
    for row in range(6):
        for col in range(11):
            cx, cy = 10 + col * 36 + (18 if row % 2 else 0), 10 + row * 31
            pts = [(cx + 15 * math.cos(math.radians(a)), cy + 15 * math.sin(math.radians(a))) for a in range(30, 390, 60)]
            body += poly(pts, "none", 'stroke="#ffffff" stroke-width="2" opacity="0.55"')
    body += rect(0, 172, W, 28, "#c6e4de")
    return defs, body


def bg_space(top="#0b0f2a", bottom="#2a1450"):
    defs = lin("bg", [top, bottom])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(40):
        x, y = (i * 83) % W, (i * 47 + i * i) % H
        body += circle(x, y, 0.8 + (i % 3) * 0.5, "#ffffff", f'opacity="{0.4 + (i % 4) * 0.15:.2f}"')
    return defs, body


def sheep(cx, cy, s=1.0, wool="#f7f7f2", face="#3a3a3a"):
    out = ""
    for dx in (-12, 10):
        out += rect(cx + dx * s - 3 * s, cy + 8 * s, 6 * s, 18 * s, face, 'rx="2"')
    for dx, dy in ((-16, 0), (0, -7), (14, -1), (-8, 8), (8, 8), (0, 2)):
        out += circle(cx + dx * s, cy + dy * s, 12 * s, wool, 'stroke="#d6d6cc" stroke-width="1"')
    out += ellipse(cx + 26 * s, cy - 6 * s, 9 * s, 11 * s, face)
    out += ellipse(cx + 19 * s, cy - 14 * s, 6 * s, 3 * s, face)
    out += circle(cx + 29 * s, cy - 9 * s, 2 * s, "#ffffff") + circle(cx + 29.5 * s, cy - 9 * s, 1 * s, "#111")
    return out


def clone():
    defs, body = bg_utopia()
    for x in (34, 84, 268, 318):
        body += sheep(x, 108, 0.75)
    body += sheep(150, 132, 1.6)
    # Dolly's lab coat and goggles
    body += path("M134 126 L160 126 L168 168 L126 168 Z", "#ffffff", 'stroke="#b8cfc9" stroke-width="2"')
    body += stroke("M147 126 L147 168", "#b8cfc9", 1.5)
    hx, hy = 150 + 26 * 1.6, 132 - 6 * 1.6
    body += stroke(f"M{hx - 12} {hy - 8} L{hx + 14} {hy - 8}", "#2f7fa6", 3)
    body += circle(hx + 5, hy - 5, 6, "#bfefff", 'stroke="#2f7fa6" stroke-width="2.5" opacity="0.9"')
    body += eye(hx + 5, hy - 5, 2.6)
    # A clipboard: she counts herself
    body += rect(96, 120, 22, 30, "#c99a5a", 'rx="2"') + rect(99, 125, 16, 22, "#ffffff")
    for y in (130, 135, 140):
        body += stroke(f"M101 {y} L113 {y}", "#999", 1.2)
    return svg(defs, body)


def caterpillar(x, y, s=1.0):
    out = ""
    for i in range(5):
        out += circle(x + i * 12 * s, y - (4 if i % 2 else 0) * s, 8 * s, "#7ac943", 'stroke="#4e8a2a" stroke-width="1.5"')
    out += circle(x + 60 * s, y - 6 * s, 10 * s, "#8ad653", 'stroke="#4e8a2a" stroke-width="1.5"')
    out += circle(x + 63 * s, y - 9 * s, 2 * s, "#111") + stroke(f"M{x + 60 * s:.1f} {y - 16 * s:.1f} l -3 -8 M{x + 66 * s:.1f} {y - 15 * s:.1f} l 3 -8", "#4e8a2a", 1.5)
    return out


def hypermetamorphosis():
    defs, body = bg_utopia()
    body += stroke("M0 34 L352 34", "#8a5a2a", 5)
    body += caterpillar(16, 150, 0.9)
    body += arrow(84, 120, 108, 104, "#5aa89a", 5)
    body += stroke("M146 34 L146 54", "#bfae8a", 2)
    body += ellipse(146, 92, 18, 38, "#b08a5a", 'stroke="#7a5a3a" stroke-width="2"')
    for y in (70, 84, 98, 112):
        body += stroke(f"M130 {y} Q 146 {y + 6} 162 {y}", "#7a5a3a", 1.5)
    body += arrow(176, 104, 200, 120, "#5aa89a", 5)
    # The butterfly that came out of it
    for sx in (-1, 1):
        body += ellipse(270 + sx * 44, 112, 36, 26, "#f2a23a", f'stroke="#5a3a1a" stroke-width="2" transform="rotate({sx * -20} {270 + sx * 44} 112)"')
        body += ellipse(270 + sx * 38, 150, 22, 16, "#a46ad6", 'stroke="#5a3a1a" stroke-width="2"')
        body += circle(270 + sx * 52, 106, 7, "#fff3c8")
    body += person(270, 200, 1.05, SKIN[0], "#4a5568", face="flat", hair="#3a2a1a", tie="#c0392b", brows="sad")
    body += rect(300, 160, 34, 24, "#5a3a22", 'rx="3"') + rect(310, 155, 14, 6, "none", 'stroke="#5a3a22" stroke-width="3"')
    return svg(defs, body)


def extremophile():
    defs = lin("bg", ["#3a1408", "#8a2a10"]) + lin("lava", ["#ffd23a", "#ff6a10", "#c0280a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x, y in ((40, 40), (300, 30), (180, 20)):
        body += circle(x, y, 3, "#ffb03a", 'opacity="0.6"')
    cx, cy = 160, 128
    body += ellipse(cx, cy, 70, 38, "#d6c2b8", 'stroke="#8a7066" stroke-width="2.5"')
    for dx in (-36, -10, 16, 40):
        body += stroke(f"M{cx + dx} {cy - 36} Q {cx + dx + 8} {cy} {cx + dx} {cy + 36}", "#a88c80", 2)
    body += circle(cx + 66, cy - 10, 26, "#dcc8be", 'stroke="#8a7066" stroke-width="2.5"')
    body += circle(cx + 90, cy - 6, 8, "#c4aca2", 'stroke="#8a7066" stroke-width="2"') + circle(cx + 92, cy - 6, 3, "#5a3a3a")
    # Sunglasses
    body += ellipse(cx + 58, cy - 18, 10, 7, "#111") + ellipse(cx + 80, cy - 18, 9, 7, "#111")
    body += stroke(f"M{cx + 46} {cy - 20} L{cx + 89} {cy - 20}", "#111", 2.5)
    # A tiny cocktail in a stubby claw
    body += stroke(f"M{cx + 30} {cy + 2} Q {cx + 54} {cy + 6} {cx + 66} {cy - 6}", "#a88c80", 8)
    body += poly([(cx + 60, cy - 40), (cx + 84, cy - 40), (cx + 72, cy - 24)], "#bff0ff", 'stroke="#fff" stroke-width="1.5" opacity="0.9"')
    body += stroke(f"M{cx + 72} {cy - 24} L{cx + 72} {cy - 10}", "#ffffff", 2)
    body += path(f"M{cx + 74} {cy - 40} L{cx + 84} {cy - 60} Q {cx + 96} {cy - 56} {cx + 98} {cy - 46} Z", "#ff5a8a")
    # Lava up to its belly
    body += path("M0 150 Q 40 140 80 150 T 160 150 T 240 150 T 320 150 L352 148 L352 200 L0 200 Z", "url(#lava)")
    for x in (40, 120, 250, 320):
        body += circle(x, 160, 5, "#ffe46a", 'opacity="0.8"')
    return svg(defs, body)


def mortarboard(hx, hy, r):
    return (poly([(hx - r * 1.5, hy - r * 0.9), (hx, hy - r * 1.5), (hx + r * 1.5, hy - r * 0.9), (hx, hy - r * 0.4)], "#1a1a22")
            + rect(hx - r * 0.7, hy - r * 0.95, r * 1.4, r * 0.45, "#1a1a22")
            + stroke(f"M{hx} {hy - r * 0.95} L{hx + r * 1.2} {hy - r * 0.7} L{hx + r * 1.2} {hy - r * 0.1}", "#f2c94c", 2.5))


def philosophy_phd():
    defs = lin("bg", ["#f2d06a", "#d9822b"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(20, 14, 150, 50, "#3a2a1a", 'rx="4"')
    for i, y in enumerate((24, 36, 48)):
        body += rect(30, y, 70, 5, "#f4e3b0") + rect(130, y, 26, 5, "#f2c94c")
    # The diploma, pinned next to the menu
    body += rect(196, 18, 62, 40, "#fffaf0", 'stroke="#c9a24a" stroke-width="2"') + lettering("PHD", 227, 28, 12, "#3a2a1a")
    body += circle(246, 48, 6, "#c0392b")
    body += person(130, 200, 1.4, SKIN[2], "#d0342c", face="flat", hair="#1a1a1a", brows="sad")
    body += mortarboard(130, 200 - 64 * 1.4, 20 * 1.4)
    # The grill and a spatula
    body += rect(196, 150, 150, 24, "#3a3a3a") + rect(196, 174, 150, 26, "#2a2a2a")
    for x in (220, 262, 304):
        body += ellipse(x, 146, 18, 7, "#6a3a1a")
    body += stroke("M168 150 L232 132", "#555", 5) + rect(228, 122, 26, 16, "#aaa", 'transform="rotate(-15 241 130)"')
    body += stroke("M262 132 q 4 -10 0 -18 M306 132 q -4 -10 0 -18", "#ffffff", 2, 'opacity="0.6"')
    return svg(defs, body)


def mathematician():
    defs, body = bg_mind()
    body += person(176, 200, 1.6, SKIN[0], "#6a4a9a", face="flat", hat="bob", brows="sad")
    body += poly([(40, 60), (90, 60), (40, 100)], "none", 'stroke="#ffffff" stroke-width="3"')
    body += circle(300, 70, 22, "none", 'stroke="#ffffff" stroke-width="3"') + stroke("M300 70 L320 62", "#ffffff", 2)
    body += stroke("M70 150 C 60 110 90 110 80 80", "#ffffff", 3)
    body += lettering("E=MC2", 260, 120, 14, "#ffffff", outline="#6a4a9a")
    body += lettering("A2+B2", 96, 14, 12, "#ffffff", outline="#6a4a9a")
    body += lettering("X/0?", 290, 18, 14, "#ffffff", outline="#6a4a9a")
    body += lettering("3.14", 60, 118, 11, "#ffffff", outline="#6a4a9a")
    return svg(defs, body)


def quantum_physicist():
    defs, body = bg_utopia()
    twin = person(0, 0, 1.3, SKIN[0], "#ffffff", face="smirk", hair=OLD_HAIR, glasses="round")
    cup = rect(-48, -40, 16, 20, "#8a5a3a", 'rx="2"') + rect(-50, -44, 20, 5, "#ffffff")
    body += group(twin + cup, "translate(120 200)")
    body += group(twin + cup, "translate(240 200)", 0.35)
    body += stroke("M180 10 L180 190", "#5aa89a", 2, 'stroke-dasharray="6 6"')
    body += lettering("?", 180, 20, 18, "#5aa89a")
    return svg(defs, body)


def perpetual_motion_machine():
    defs, body = bg_utopia()
    cx, cy = 120, 104
    body += circle(cx, cy, 64, "none", 'stroke="#8a9aa8" stroke-width="7"')
    for a in range(0, 360, 45):
        body += stroke(f"M{cx} {cy} L{cx + 62 * math.cos(math.radians(a)):.1f} {cy + 62 * math.sin(math.radians(a)):.1f}", "#a8b4c0", 2)
    body += stroke(f"M{cx} {cy} L{cx - 30} 190 M{cx} {cy} L{cx + 30} 190", "#6a7a88", 5)
    # The hamster, running
    hx, hy = cx, cy + 40
    body += ellipse(hx, hy, 24, 15, "#d99a5a", 'stroke="#8a5a2a" stroke-width="1.5"')
    body += circle(hx + 20, hy - 6, 11, "#e2a86a", 'stroke="#8a5a2a" stroke-width="1.5"') + circle(hx + 16, hy - 16, 4, "#f2b8a0")
    body += circle(hx + 24, hy - 8, 2, "#111") + circle(hx + 30, hy - 4, 2, "#e86a8a")
    body += stroke(f"M{hx - 12} {hy + 12} l -6 6 M{hx + 10} {hy + 12} l 6 6", "#8a5a2a", 2.5)
    body += speed_lines(hx - 30, hy - 6, 3, 18, "#8a5a2a")
    # The wire to the fan, and the wind blowing back on the hamster
    body += stroke(f"M{cx} {cy} C 190 150 220 150 260 150", "#333", 2.5)
    fx, fy = 276, 96
    body += stroke(f"M{fx} {fy + 30} L{fx} 150", "#555", 6) + ellipse(fx, 152, 24, 6, "#555")
    body += circle(fx, fy, 34, "#e8eef2", 'stroke="#8a9aa8" stroke-width="3"')
    for a in (0, 120, 240):
        body += ellipse(fx + 14 * math.cos(math.radians(a)), fy + 14 * math.sin(math.radians(a)), 14, 7, "#5ab0d6",
                        f'transform="rotate({a} {fx + 14 * math.cos(math.radians(a)):.1f} {fy + 14 * math.sin(math.radians(a)):.1f})"')
    body += circle(fx, fy, 5, "#333")
    for y in (80, 98, 116):
        body += stroke(f"M236 {y} Q 214 {y - 6} 196 {y + 4}", "#5ab0d6", 2.5, 'opacity="0.8"')
    return svg(defs, body)


def particle_accelerator():
    defs = lin("bg", ["#7ab85a", "#4a8a3a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x, y in ((30, 30), (320, 170), (40, 170), (316, 26)):
        body += circle(x, y, 12, "#3a6a2a", 'opacity="0.7"')
    body += ellipse(176, 100, 130, 80, "none", 'stroke="#c8ccd0" stroke-width="16"')
    body += ellipse(176, 100, 130, 80, "none", 'stroke="#6a7078" stroke-width="3"')
    gx, gy = 176 + 130 * math.cos(math.radians(-30)), 100 + 80 * math.sin(math.radians(-30))
    body += circle(gx, gy, 14, "#fff7a0", 'opacity="0.5"') + circle(gx, gy, 6, "#ffe23a")
    # The scientist on a bicycle, chasing it
    bx, by = 176 + 130 * math.cos(math.radians(200)), 100 + 80 * math.sin(math.radians(200))
    body += circle(bx - 8, by + 8, 6, "none", 'stroke="#222" stroke-width="2"') + circle(bx + 8, by + 8, 6, "none", 'stroke="#222" stroke-width="2"')
    body += stroke(f"M{bx - 8} {by + 8} L{bx} {by} L{bx + 8} {by + 8} M{bx} {by} L{bx} {by - 10}", "#222", 2)
    body += rect(bx - 4, by - 18, 9, 12, "#ffffff") + circle(bx, by - 22, 5, SKIN[0]) + circle(bx, by - 26, 4, OLD_HAIR)
    body += speed_lines(bx - 14, by - 12, 2, 16, "#ffffff")
    body += rect(150, 86, 52, 28, "#e8eaec", 'stroke="#6a7078" stroke-width="2"') + rect(170, 100, 12, 14, "#6a7078")
    return svg(defs, body)


def tomato_arm(x, y, flip):
    f = flip
    return path(f"M{x} {y} C {x + f * 24} {y + 4} {x + f * 40} {y - 4} {x + f * 44} {y - 26} C {x + f * 56} {y - 30} {x + f * 58} {y - 44} {x + f * 48} {y - 52} "
                f"C {x + f * 40} {y - 46} {x + f * 30} {y - 34} {x + f * 30} {y - 22} C {x + f * 22} {y - 18} {x + f * 12} {y - 14} {x} {y - 14} Z",
                "#e0402a", 'stroke="#9a2010" stroke-width="2"')


def genetically_modified_organism():
    defs, body = bg_utopia()
    body += tomato_arm(130, 116, -1) + tomato_arm(222, 116, 1)
    body += circle(176, 118, 54, "#e8462e", 'stroke="#9a2010" stroke-width="3"')
    body += ellipse(156, 96, 14, 9, "#ff8a6a", 'opacity="0.7"')
    body += poly(star_points(176, 66, 22, 8, 5), "#3a9a3a") + rect(173, 52, 6, 12, "#2a7a2a")
    body += eye(160, 112, 7) + eye(192, 112, 7)
    body += stroke("M158 102 L168 106 M194 102 L184 106", "#222", 3)
    body += path("M160 132 L192 132 Q 176 148 160 132 Z", "#ffffff", 'stroke="#222" stroke-width="1.5"')
    # The NON-GMO sticker, peeling off
    body += circle(214, 146, 16, "#ffffff", 'stroke="#3a9a3a" stroke-width="2"')
    body += lettering("NON", 214, 138, 6, "#3a9a3a", width=1.4) + lettering("GMO", 214, 147, 6, "#3a9a3a", width=1.4)
    body += path("M222 158 Q 236 156 232 166 Z", "#e8e8e8")
    return svg(defs, body)


def giraffe():
    defs, body = bg_sky("#9fd2f2", "#f4e6b8", sun=False)
    body += cloud(240, 20, 1.0) + cloud(60, 30, 0.8)
    spots = "#a8642a"
    # Body and legs at the bottom left
    for x in (40, 62, 110, 130):
        body += rect(x, 168, 9, 40, "#e8b04a")
    body += ellipse(88, 160, 62, 26, "#e8b04a")
    # The neck: up and out of the picture...
    body += path("M118 150 C 130 110 132 60 138 -5 L162 -5 C 158 60 154 112 146 156 Z", "#e8b04a")
    # ...and back down from the top, the head hanging upside down
    body += path("M236 -5 L262 -5 C 262 40 260 80 256 104 L238 104 C 236 80 236 40 236 -5 Z", "#e8b04a")
    for x, y in ((70, 154), (98, 166), (118, 148), (140, 120), (145, 70), (148, 20), (246, 14), (250, 54), (246, 88)):
        body += ellipse(x, y, 8, 6, spots)
    body += ellipse(247, 124, 18, 28, "#eebb5a")
    body += ellipse(247, 148, 12, 8, "#d89a4a")
    body += stroke("M240 98 L236 86 M254 98 L258 86", "#a8642a", 3) + circle(236, 86, 3, "#a8642a") + circle(258, 86, 3, "#a8642a")
    body += ellipse(228, 112, 9, 4, "#eebb5a") + ellipse(266, 112, 9, 4, "#eebb5a")
    body += eye(240, 132, 4.5) + eye(254, 132, 4.5)
    body += circle(244, 150, 1.6, "#5a3a1a") + circle(250, 150, 1.6, "#5a3a1a")
    return svg(defs, body)


def astronaut(cx, base, s=1.0):
    out = path(f"M{cx - 34 * s:.1f} {base:.1f} C {cx - 34 * s:.1f} {base - 46 * s:.1f} {cx + 34 * s:.1f} {base - 46 * s:.1f} {cx + 34 * s:.1f} {base:.1f} Z", "#f2f2f2", 'stroke="#aab" stroke-width="2"')
    out += rect(cx - 12 * s, base - 30 * s, 24 * s, 16 * s, "#c8d0d8", 'rx="3"')
    out += circle(cx, base - 64 * s, 26 * s, "#f2f2f2", 'stroke="#aab" stroke-width="2"')
    out += ellipse(cx + 2 * s, base - 64 * s, 18 * s, 14 * s, "#2a4a8a") + ellipse(cx + 8 * s, base - 70 * s, 6 * s, 3 * s, "#9fd8ff", 'opacity="0.8"')
    return out


def pioneer():
    defs = lin("bg", ["#f0a060", "#c8501e"]) + lin("ground", ["#b8481a", "#7a2a10"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += circle(300, 30, 14, "#e8e0d0", 'opacity="0.8"') + circle(60, 24, 6, "#e8e0d0", 'opacity="0.7"')
    body += path("M0 150 Q 90 130 180 150 T 352 146 L352 200 L0 200 Z", "url(#ground)")
    body += circle(60, 170, 8, "#8a3a14") + circle(320, 176, 6, "#8a3a14")
    body += astronaut(120, 200, 1.2)
    body += stroke("M160 70 L160 170", "#ccc", 4) + rect(160, 70, 52, 34, "#ffffff", 'stroke="#aaa" stroke-width="1"')
    body += circle(186, 87, 9, "#c0392b")
    body += sign(280, 72, 110, 50)
    body += lettering("FOR RENT", 280, 78, 11, "#c0392b") + lettering("$9999", 280, 96, 13, "#222")
    return svg(defs, body)


def placebo():
    defs, body = bg_utopia()
    body += ellipse(124, 126, 80, 46, "#ffffff", 'stroke="#c8d8d4" stroke-width="3"')
    body += stroke("M60 126 L188 126", "#d8e4e0", 3)
    body += lettering("MIRACLE", 124, 96, 14, "#2f9f8a")
    # A spoonful of sugar helps the medicine go down
    body += stroke("M96 182 L206 194", "#8a96a2", 11) + stroke("M96 182 L206 194", "#d6dde4", 6)
    body += ellipse(70, 178, 32, 15, "#d6dde4", 'stroke="#8a96a2" stroke-width="3"')
    body += path("M42 176 C 48 156 92 156 98 176 Z", "#ffffff", 'stroke="#c8d0d8" stroke-width="2"')
    for x, y in ((52, 170), (64, 164), (78, 168), (88, 173), (60, 174)):
        body += rect(x - 3, y - 3, 6, 6, "#eef3f7", 'stroke="#c8d0d8" stroke-width="1"')
    for x, y in ((40, 152), (102, 156)):
        body += poly(star_points(x, y, 6, 2, 4), "#ffffff")
    body += person(270, 200, 1.2, SKIN[1], "#ffffff", face="grin", hair="#3a2a1a")
    # A thin arm, flexed in triumph, and the sparkle of a bicep that isn't there
    body += stroke("M290 170 Q 318 160 312 120 Q 300 106 296 96", SKIN[1], 7)
    body += circle(312, 128, 4, SKIN[1])
    for x, y in ((326, 112), (336, 132), (322, 92)):
        body += poly(star_points(x, y, 7, 2.5, 4), "#ffd23a")
    return svg(defs, body)


def vaccine():
    defs, body = bg_utopia()
    syringe = (rect(-90, -16, 150, 32, "#e8f6ff", 'rx="5" stroke="#8aa8b8" stroke-width="3"')
               + rect(-84, -10, 96, 20, "#6ad0a0", 'opacity="0.85"')
               + rect(60, -6, 50, 12, "#c8d0d8") + rect(110, -24, 10, 48, "#8aa8b8", 'rx="2"')
               + stroke("M-90 0 L-140 0", "#9aa8b0", 3)
               + rect(-106, -22, 6, 44, "#8aa8b8"))
    for x in (-60, -36, -12):
        syringe += stroke(f"M{x} -16 L{x} -6", "#8aa8b8", 2)
    body += group(syringe, "translate(170 112) rotate(-24)")
    # The 5G antenna on the plunger, at full bars
    ax, ay = 274, 58
    body += stroke(f"M{ax} {ay + 18} L{ax} {ay - 12}", "#555", 3) + circle(ax, ay - 14, 4, "#d0342c")
    for i in range(1, 4):
        body += stroke(f"M{ax - 7 * i} {ay - 14 - 6 * i} A {9 * i} {9 * i} 0 0 1 {ax + 7 * i} {ay - 14 - 6 * i}", "#2f9f8a", 2.5)
    for i, h in enumerate((8, 14, 20, 26)):
        body += rect(300 + i * 10, 40 - h, 7, h, "#2f9f8a")
    body += lettering("5G", 316, 48, 12, "#2f9f8a")
    return svg(defs, body)


def polar_bear(cx, cy, s=1.0):
    out = ellipse(cx, cy, 26 * s, 16 * s, "#fbfbf6", 'stroke="#c8ccd0" stroke-width="1.5"')
    for dx in (-16, -6, 8, 18):
        out += rect(cx + dx * s - 4 * s, cy + 8 * s, 8 * s, 12 * s, "#fbfbf6", 'stroke="#c8ccd0" stroke-width="1"')
    out += circle(cx + 26 * s, cy - 10 * s, 12 * s, "#fbfbf6", 'stroke="#c8ccd0" stroke-width="1.5"')
    out += circle(cx + 20 * s, cy - 20 * s, 4 * s, "#fbfbf6", 'stroke="#c8ccd0" stroke-width="1"')
    out += circle(cx + 30 * s, cy - 12 * s, 1.8 * s, "#111") + circle(cx + 37 * s, cy - 8 * s, 2.6 * s, "#111")
    return out


def climate_change():
    defs, body = bg_sky("#ffb84a", "#ff6a3a")
    body += circle(290, 40, 34, "#fff0a0") + circle(290, 40, 46, "#fff0a0", 'opacity="0.25"')
    body += poly([(96, 60), (256, 60), (176, 150)], "#bff0ff", 'stroke="#ffffff" stroke-width="3" opacity="0.85"')
    body += path("M110 74 L242 74 L176 146 Z", "#ff8aa8", 'opacity="0.55"')
    body += stroke("M176 150 L176 186 M146 188 L206 188", "#ffffff", 5)
    body += rect(150, 52, 40, 30, "#e8fbff", 'rx="5" stroke="#9fd8ef" stroke-width="2"')
    body += polar_bear(162, 34, 0.7)
    body += sweat(196, 18, 1.2)
    body += stroke("M228 76 L250 20", "#8a5a2a", 2.5) + path("M228 26 Q 250 4 272 26 Z", "#3ab0d6")
    return svg(defs, body)


def overpopulation():
    defs, body = bg_space()
    body += circle(176, 168, 80, "#2a7ad0")
    body += path("M120 120 C 140 100 170 110 160 130 C 150 150 120 140 120 120 Z", "#3aa04a")
    body += path("M200 130 C 230 116 250 140 230 156 C 210 166 196 150 200 130 Z", "#3aa04a")
    for row, (r, n) in enumerate(((86, 15), (96, 13), (106, 11))):
        for i in range(n):
            a = math.radians(-160 + i * 140 / (n - 1))
            x, y = 176 + r * math.cos(a), 168 + r * math.sin(a)
            body += circle(x, y, 7, SKIN[(i + row) % 4], 'stroke="#2a2a2a" stroke-width="1"')
    # One man on top of everyone, on one foot
    body += circle(176, 40, 7, SKIN[0], 'stroke="#2a2a2a" stroke-width="1"')
    body += stroke("M176 47 L176 64 M176 64 L176 74 M176 64 L188 66 M176 52 L162 46 M176 52 L190 44", "#2a2a2a", 3)
    return svg(defs, body)


def cow(cx, cy, s=1.0):
    out = ellipse(cx, cy, 20 * s, 12 * s, "#ffffff", 'stroke="#555" stroke-width="1"')
    out += ellipse(cx - 6 * s, cy - 2 * s, 6 * s, 4 * s, "#222") + ellipse(cx + 8 * s, cy + 4 * s, 5 * s, 3 * s, "#222")
    out += circle(cx + 22 * s, cy - 6 * s, 7 * s, "#ffffff", 'stroke="#555" stroke-width="1"') + ellipse(cx + 26 * s, cy - 3 * s, 4 * s, 3 * s, "#f2a8b8")
    for dx in (-12, -4, 6, 14):
        out += rect(cx + dx * s, cy + 9 * s, 3 * s, 8 * s, "#ffffff", 'stroke="#555" stroke-width="0.8"')
    return out


def black_hole():
    defs, body = bg_space("#05030f", "#1a0a2a")
    defs += rad("disk", [("#ffd23a", 0.9), ("#ff6a1a", 0.6), ("#5a1a8a", 0)])
    body += ellipse(176, 100, 150, 46, "url(#disk)")
    for r in (40, 70, 100, 130):
        body += stroke(f"M{176 - r} 100 A {r} {r * 0.4} 0 0 1 {176 + r} 100", "#ffb03a", 1.5, 'opacity="0.5"')
    body += circle(176, 100, 30, "#000000")
    body += group(cow(0, 0, 1.0), "translate(70 52) rotate(-20)")
    body += group(rect(-20, -8, 40, 14, "#c0392b", 'rx="4"') + rect(-22, -16, 10, 22, "#a8302a", 'rx="3"') + rect(12, -16, 10, 22, "#a8302a", 'rx="3"'), "translate(276 60) rotate(25) scale(0.8)")
    body += group(path("M0 0 L8 0 L8 16 L16 20 L14 26 L0 22 Z", "#f2f2f2", 'stroke="#c0392b" stroke-width="2"'), "translate(110 150) rotate(-40)")
    body += group(rect(0, 0, 18, 24, "#ffffff", 'stroke="#888" stroke-width="1"') + stroke("M3 6 L15 6 M3 11 L15 11 M3 16 L12 16", "#888", 1), "translate(236 136) rotate(30) scale(0.8)")
    return svg(defs, body)


def stasis_field():
    defs, body = bg_utopia()
    body += person(176, 200, 1.5, SKIN[0], "#5a7aa0", face="shout", hair="#6a4a2a")
    body += path("M146 106 Q 150 96 160 100", "none", 'stroke="#222" stroke-width="2"') + path("M192 100 Q 200 96 206 106", "none", 'stroke="#222" stroke-width="2"')
    for x, y, r in ((214, 110, 3), (228, 100, 2.5), (238, 116, 3.5), (222, 124, 2), (246, 104, 2)):
        body += circle(x, y, r, "#7ac8ff")
    body += rect(86, 28, 180, 172, "#5ad67a", 'rx="14" opacity="0.38"')
    body += rect(86, 28, 180, 172, "none", 'rx="14" stroke="#2a9a4a" stroke-width="3" opacity="0.7"')
    body += stroke("M100 44 L120 44 M100 56 L110 56", "#ffffff", 4, 'opacity="0.7"')
    return svg(defs, body)


def absolute_zero():
    defs = lin("bg", ["#d8f0ff", "#8ac0e8"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(14):
        x, y = (i * 61) % W, (i * 37) % 150
        body += stroke(f"M{x - 5} {y} L{x + 5} {y} M{x} {y - 5} L{x} {y + 5}", "#ffffff", 1.5)
    body += rect(250, 26, 70, 40, "#f4f8fb", 'rx="4" stroke="#8aa0b8" stroke-width="2"')
    body += lettering("-273", 285, 36, 16, "#2a6ab0")
    body += person(150, 190, 1.45, "#bfe0ff", "#9fc8e8", face="frown", hair="#e8f6ff", brows="sad")
    hx, hy = 150, 190 - 64 * 1.45
    for dx in (-6, 2, 9):
        body += poly([(hx + dx - 3, hy + 16), (hx + dx + 3, hy + 16), (hx + dx, hy + 30)], "#ffffff", 'stroke="#9fd8ff" stroke-width="1"')
    body += rect(60, 170, 250, 30, "#8a6a4a")
    body += rect(216, 136, 38, 36, "#cdeeff", 'rx="4" opacity="0.9" stroke="#ffffff" stroke-width="2"')
    body += rect(226, 146, 18, 20, "#6a4a3a", 'rx="2"')
    return svg(defs, body)


def atom(cx, cy, s=1.0):
    out = ""
    for a in (0, 60, 120):
        out += ellipse(cx, cy, 34 * s, 12 * s, "none", f'stroke="#7ad6ff" stroke-width="2.5" transform="rotate({a} {cx} {cy})"')
    out += circle(cx, cy, 10 * s, "#ffe23a", 'stroke="#ff9a1a" stroke-width="2"')
    return out


def splitting_the_atom():
    defs, body = bg_sky("#9fc0d8", "#e8d8b0", sun=False)
    # The mushroom cloud, already rising
    body += rect(292, 70, 22, 110, "#b8a890") + ellipse(303, 64, 46, 28, "#c8b8a0") + ellipse(303, 52, 30, 18, "#d8c8b0")
    body += rect(0, 172, W, 28, "#5a8a3a")
    for x in (20, 60, 330):
        body += poly([(x, 172), (x + 14, 120), (x + 28, 172)], "#2a6a2a")
    body += ellipse(176, 160, 40, 10, "#c8a070") + rect(136, 160, 80, 26, "#8a5a2a") + ellipse(176, 160, 40, 10, "#d8b080", 'stroke="#8a5a2a" stroke-width="2"')
    body += atom(176, 138, 1.0)
    body += stroke("M176 112 L172 126 L180 132 L176 146", "#ffffff", 2.5)
    body += person(90, 200, 1.25, SKIN[1], "#b8322a", face="grin", hair="#5a3a1a", beard="#6a4a2a", hat="beanie")
    body += stroke("M86 138 L106 150", "#222", 2, 'opacity="0.6"')
    body += stroke("M110 136 L162 96", "#8a5a2a", 6)
    body += poly([(152, 88), (174, 94), (168, 112), (156, 106)], "#b8c0c8", 'stroke="#555" stroke-width="2"')
    body += speed_lines(196, 92, 3, 22, "#ffffff")
    return svg(defs, body)


def dna(x0, y0, length, amp, color1, color2, gap=None):
    out = ""
    pts1, pts2 = [], []
    for i in range(41):
        t = i / 40
        x = x0 + t * length
        y1 = y0 + amp * math.sin(t * math.pi * 4)
        y2 = y0 - amp * math.sin(t * math.pi * 4)
        pts1.append((x, y1))
        pts2.append((x, y2))
        if i % 3 == 0 and not (gap and gap[0] <= x <= gap[1]):
            out += stroke(f"M{x:.1f} {y1:.1f} L{x:.1f} {y2:.1f}", "#ffffff", 2.5, 'opacity="0.8"')
    for pts, color in ((pts1, color1), (pts2, color2)):
        segs = [[]]
        for x, y in pts:
            if gap and gap[0] <= x <= gap[1]:
                if segs[-1]:
                    segs.append([])
                continue
            segs[-1].append((x, y))
        for seg in segs:
            if len(seg) > 1:
                out += stroke("M" + " L".join(f"{x:.1f} {y:.1f}" for x, y in seg), color, 6)
    return out


def gene_editing():
    defs, body = bg_utopia()
    body += dna(20, 100, 312, 36, "#e8462e", "#2a7ad0", gap=(160, 196))
    # Scissors, mid-snip
    body += group(ellipse(-18, -6, 10, 7, "none", 'stroke="#d0342c" stroke-width="4"') + ellipse(-18, 10, 10, 7, "none", 'stroke="#d0342c" stroke-width="4"')
                  + poly([(-8, -4), (40, 4), (-6, 2)], "#c8d0d8", 'stroke="#555" stroke-width="1"')
                  + poly([(-8, 8), (40, 0), (-6, 2)], "#dce2e8", 'stroke="#555" stroke-width="1"'), "translate(132 40) rotate(20)")
    # A unicorn horn taped into the gap
    body += poly([(178, 66), (190, 128), (166, 128)], "#f6e8ff", 'stroke="#c08ad6" stroke-width="2"')
    for y in (82, 98, 114):
        body += stroke(f"M{178 - (y - 66) * 0.2:.1f} {y} L{178 + (y - 66) * 0.2:.1f} {y - 6}", "#c08ad6", 2)
    body += rect(150, 120, 56, 12, "#f2e2b0", 'opacity="0.9" transform="rotate(-6 178 126)"')
    return svg(defs, body)


# ---------------------------------------------------------------- Digital Society additions

def catfish():
    defs, body = bg_digital()
    # A goldfish in its bowl and a cat kissing it through the glass, hearts everywhere
    body += ellipse(232, 120, 66, 60, "#bfe8ff", 'opacity="0.55" stroke="#ffffff" stroke-width="3"')
    body += rect(186, 64, 92, 10, "#ffffff", 'rx="5" opacity="0.7"')
    body += path("M168 150 Q 232 136 296 150 L292 170 Q 232 182 172 170 Z", "#5ab0e0", 'opacity="0.5"')
    body += ellipse(226, 118, 30, 20, "#ff8a2a", 'stroke="#c0581a" stroke-width="2"')
    body += poly([(254, 118), (276, 102), (276, 134)], "#ff8a2a", 'stroke="#c0581a" stroke-width="2"')
    body += path("M222 100 Q 232 88 240 100 Z", "#ff8a2a")
    body += circle(212, 114, 4, "#222") + circle(213, 113, 1.4, "#ffffff")
    body += path("M196 120 Q 200 124 196 128", "none", 'stroke="#c0392b" stroke-width="2.5"')
    body += ellipse(206, 124, 5, 3, "#ff6a8a", 'opacity="0.7"')
    # The cat, pressed against the glass
    body += path("M40 200 C 40 140 70 120 108 120 C 140 120 156 150 156 200 Z", "#f2a24a", 'stroke="#a86a2a" stroke-width="2"')
    body += circle(118, 100, 40, "#f2a24a", 'stroke="#a86a2a" stroke-width="2"')
    body += poly([(88, 76), (84, 42), (110, 64)], "#f2a24a", 'stroke="#a86a2a" stroke-width="2"')
    body += poly([(136, 66), (152, 40), (156, 76)], "#f2a24a", 'stroke="#a86a2a" stroke-width="2"')
    body += stroke("M100 96 q 6 -6 12 0 M128 96 q 6 -6 12 0", "#3a2a1a", 2.5)
    body += path("M150 110 Q 160 106 166 112 Q 160 118 150 114 Z", "#ff6a8a")
    body += ellipse(108, 112, 7, 4, "#ff8aa8", 'opacity="0.7"')
    body += stroke("M150 104 l 22 -6 M150 112 l 22 2", "#a86a2a", 1.5)
    body += path("M140 160 C 160 150 172 140 176 128", "none", 'stroke="#f2a24a" stroke-width="14" stroke-linecap="round"')
    for x, y, k in ((176, 40, 1.0), (206, 22, 0.7), (150, 26, 0.6), (300, 40, 0.8)):
        body += path(f"M{x} {y + 6 * k} C {x - 8 * k} {y - 4 * k} {x - 18 * k} {y + 4 * k} {x} {y + 18 * k} C {x + 18 * k} {y + 4 * k} {x + 8 * k} {y - 4 * k} {x} {y + 6 * k} Z", "#ff4a8a")
    return svg(defs, body)


def conspiracy_theorist():
    defs = lin("bg", ["#2a2030", "#151018"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(150, 14, 192, 140, "#a8743a", 'stroke="#6a4a22" stroke-width="4"')
    photos = ((166, 26), (232, 22), (296, 30), (172, 100), (300, 104))
    for x, y in photos:
        body += rect(x, y, 34, 28, "#f4f0e6", 'transform="rotate(-4 {0} {1})"'.format(x + 17, y + 14))
        body += circle(x + 17, y + 2, 3, "#d0342c")
    # The star exhibit: a pigeon
    body += rect(222, 66, 58, 50, "#f4f0e6")
    body += ellipse(250, 92, 16, 10, "#8a8aa0") + circle(262, 84, 6, "#8a8aa0") + poly([(267, 84), (274, 86), (267, 88)], "#e8a040")
    body += lettering("DRONE", 251, 104, 7, "#d0342c", width=1.6)
    for (x1, y1), (x2, y2) in (((183, 30), (251, 70)), ((249, 26), (251, 70)), ((313, 34), (251, 70)), ((189, 104), (251, 70)), ((317, 108), (251, 70)), ((183, 30), (313, 34))):
        body += stroke(f"M{x1} {y1} L{x2} {y2}", "#d0342c", 2)
    body += person(90, 200, 1.35, SKIN[0], "#5a6a3a", face="grin", glasses="round", brows="angry")
    hx, hy = 90, 200 - 64 * 1.35
    body += poly([(hx - 30, hy - 14), (hx + 30, hy - 14), (hx + 4, hy - 64)], "#d8dce0", 'stroke="#9aa0a8" stroke-width="2"')
    body += stroke(f"M{hx - 14} {hy - 26} l 8 -4 l 6 6 l 8 -6 M{hx - 4} {hy - 44} l 6 4 l 6 -6", "#9aa0a8", 1.5)
    body += stroke("M118 150 L196 104", SKIN[0], 8)
    return svg(defs, body)


def clickbait():
    defs = lin("bg", ["#ffe23a", "#ff8a1a"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += person(110, 210, 2.1, SKIN[0], "#2a6ad0", face="shout", hair="#3a2a1a", brows="sad")
    body += stroke("M60 140 Q 66 120 74 112 M160 140 Q 154 120 146 112", SKIN[0], 12)
    body += circle(276, 120, 40, "none", 'stroke="#ff1a1a" stroke-width="8"')
    body += arrow(200, 34, 248, 88, "#ff1a1a", 10)
    body += lettering("OMG!", 250, 20, 22, "#ffffff", outline="#ff1a1a")
    return svg(defs, body)


def cancelled():
    defs = lin("bg", ["#1a1030", "#3a2040"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += person(176, 136, 1.2, SKIN[0], "#4a5a7a", face="frown", hair="#5a3a1a", brows="sad")
    body += sweat(206, 46, 1.3)
    body += poly([(136, 100), (216, 100), (206, 150), (146, 150)], "#6a4a2a", 'stroke="#3a2a1a" stroke-width="2"')
    body += stroke("M176 100 L176 86", "#555", 3) + ellipse(176, 82, 5, 7, "#333")
    for i, x in enumerate(range(10, 352, 38)):
        body += circle(x, 172, 16, "#0c0812") + path(f"M{x - 26} 200 C {x - 26} 176 {x + 26} 176 {x + 26} 200 Z", "#0c0812")
        if i % 3 == 0:
            body += stroke(f"M{x + 14} 180 L{x + 22} 132", "#5a3a1a", 3) + flames(x + 22, 134, 1, 0, 18)
        elif i % 3 == 1:
            body += stroke(f"M{x - 12} 180 L{x - 18} 130 M{x - 26} 132 L{x - 10} 128 M{x - 26} 132 L{x - 28} 120 M{x - 18} 130 L{x - 19} 118 M{x - 10} 128 L{x - 9} 116", "#8a8a8a", 2.5)
        else:
            body += rect(x + 8, 136, 12, 20, "#1b1b22", 'rx="2"') + rect(x + 10, 139, 8, 14, "#9fe0ff")
    return svg(defs, body)


# ---------------------------------------------------------------- Tech additions

def server_rack(x, y, w, h):
    out = rect(x, y, w, h, "#1a1e24", 'stroke="#3a4048" stroke-width="2"')
    for i, yy in enumerate(range(y + 8, y + h - 10, 16)):
        out += rect(x + 6, yy, w - 12, 12, "#2a3038")
        out += circle(x + w - 14, yy + 6, 2.5, "#3aff7a" if i % 3 else "#ff3a3a")
    return out


def sysadmin():
    defs, body = bg_tech()
    body += server_rack(220, 30, 90, 150)
    body += cloud(262, 30, 0.9, "#8a8a8a", 0.8) + cloud(290, 14, 0.7, "#6a6a6a", 0.7)
    body += person(110, 200, 1.4, SKIN[0], "#2a2a3a", face="flat", hair="#5a3a1a", beard="#6a4a2a", glasses="round")
    # The cable, yanked out
    body += stroke("M228 140 C 200 150 190 120 160 130", "#111", 4)
    body += rect(150, 124, 14, 12, "#333") + stroke("M150 127 l -6 0 M150 133 l -6 0", "#c8c8c8", 2)
    body += stroke("M140 160 L156 132", SKIN[0], 9)
    body += rect(40, 140, 24, 30, "#ffffff", 'rx="3" stroke="#aaa" stroke-width="2"') + stroke("M64 148 q 10 4 0 14", "#aaa", 3)
    body += lettering("IT", 52, 148, 9, "#2a6ad0")
    return svg(defs, body)


def meatproxy_as_a_service():
    # A raw steak at a desk copy-pasting text from one window to another, a $9.99/MONTH tag on the monitor
    defs, body = bg_tech()
    body += rect(0, 168, W, 32, "#5a4632")  # The desk
    body += rect(150, 8, 196, 128, "#2a2a30", 'rx="6"') + rect(158, 16, 180, 112, "#dfe6ee", 'rx="3"')
    body += rect(240, 136, 16, 20, "#2a2a30") + rect(222, 154, 52, 6, "#2a2a30", 'rx="3"')
    for x, bar in ((164, "#3b5bdb"), (252, "#2f9e44")):  # Two windows
        body += rect(x, 24, 80, 96, "#ffffff", 'stroke="#8a94a0" stroke-width="1.5"') + rect(x, 24, 80, 10, bar)
        for k in range(5):
            body += rect(x + 6, 42 + k * 14, 60 - (k % 3) * 12, 5, "#9aa4b0")
    body += rect(170, 54, 48, 7, "#4fa3ff", 'opacity="0.6"')  # The selected line...
    body += path("M222 58 Q 240 40 258 58", "none", 'stroke="#d0342c" stroke-width="2.5" stroke-dasharray="4 3"')
    body += poly([(258, 58), (250, 54), (254, 63)], "#d0342c")  # ...pasted into the other window
    body += rect(258, 54, 48, 7, "#4fa3ff", 'opacity="0.6"')
    body += path("M300 104 L336 104 L342 112 L336 120 L300 120 Z", "#fff7e0", 'stroke="#333" stroke-width="1.5"')
    body += lettering("$9.99", 320, 108, 6, "#c0392b", width=1.2)
    # The steak: a marbled slab with a bone, little arms on the keyboard
    body += rect(110, 166, 120, 12, "#cfd4da", 'rx="3" stroke="#6a7280" stroke-width="1.5"')
    steak = (path("M-56 0 C -60 -40 -10 -58 30 -46 C 66 -36 70 6 44 26 C 14 48 -50 40 -56 0 Z", "#b8323a", 'stroke="#6a1a1e" stroke-width="3"')
             + path("M-40 -6 C -24 -20 0 -10 14 -24 M-30 14 C -10 4 10 18 34 2", "none", 'stroke="#f2d6c8" stroke-width="4" opacity="0.8"')
             + circle(-6, 12, 10, "#f2efe6", 'stroke="#c8c0b0" stroke-width="2"'))
    body += group(steak, "translate(84 130)")
    body += circle(68, 112, 7, "#ffffff") + circle(92, 110, 7, "#ffffff") + circle(70, 113, 3, "#222") + circle(94, 111, 3, "#222")
    body += stroke("M118 142 Q 134 156 146 166 M110 152 Q 150 170 196 168", "#8a2228", 4)
    return svg(defs, body)


def monitor(x, y, w, h, screen):
    return (rect(x, y, w, h, "#2a2a30", 'rx="6"') + rect(x + 8, y + 8, w - 16, h - 16, screen, 'rx="3"')
            + rect(x + w / 2 - 10, y + h, 20, 16, "#2a2a30") + rect(x + w / 2 - 30, y + h + 14, 60, 6, "#2a2a30", 'rx="3"'))


def ransomware_attack():
    defs = lin("bg", ["#2a1a1a", "#120a0a"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += monitor(30, 20, 190, 130, "#a01a1a")
    body += skull(100, 64, 1.3) + padlock(166, 70, 1.6, "#f2c94c")
    body += lettering("PAY 1 BTC", 125, 112, 13, "#ffffff")
    body += person(286, 200, 1.25, SKIN[0], "#9a6ab0", face="frown", hair=OLD_HAIR, glasses="round", brows="sad")
    body += path("M266 112 Q 263 122 266 128 Q 269 122 266 112 Z", "#7ac8ff") + path("M306 112 Q 303 122 306 128 Q 309 122 306 112 Z", "#7ac8ff")
    body += circle(286, 104, 10, OLD_HAIR, 'transform="translate(0 -22)"')
    body += circle(250, 182, 12, "#e86a8a") + stroke("M240 160 L262 196 M262 160 L242 196", "#c8c8c8", 2.5)
    return svg(defs, body)


def man_in_the_middle():
    defs, body = bg_sky("#a8dcf2", "#e8f4d0", sun=False)
    body += rect(0, 168, W, 32, "#6aa84a")
    body += rect(40, 150, 272, 10, "#8a5a2a") + rect(40, 128, 272, 10, "#8a5a2a")
    for x in (56, 296):
        body += rect(x, 160, 8, 30, "#5a3a1a")
    body += person(80, 172, 1.05, SKIN[1], "#e86a8a", face="smile", hat="bob")
    body += person(272, 172, 1.05, SKIN[2], "#3a6ab0", face="smile", hair="#1a1a1a")
    body += person(176, 172, 1.2, SKIN[0], "#4a4a4a", face="smirk", hair="#3a2a1a", glasses="shades")
    body += phone(150, 140, 0.8, "#9fe0ff") + phone(202, 140, 0.8, "#9fe0ff")
    for x, y in ((112, 70), (244, 70)):
        body += path(f"M{x} {y} C {x - 6} {y - 8} {x - 16} {y - 2} {x - 8} {y + 6} L{x} {y + 14} L{x + 8} {y + 6} C {x + 16} {y - 2} {x + 6} {y - 8} {x} {y} Z", "#ff4a6a")
    return svg(defs, body)


# ---------------------------------------------------------------- Shadow Ops additions

def whistleblower():
    defs, body = bg_shadow()
    body += person(170, 200, 1.5, SKIN[1], "#3a3a44", face="flat", hat="hood")
    body += ellipse(182, 110, 10, 6, "#c8ccd0", 'stroke="#777" stroke-width="1.5"') + circle(190, 110, 3, "#555")
    body += stroke("M150 120 Q 170 150 190 116", "#d0342c", 2)
    body += stroke("M200 104 l 14 -6 M202 112 l 16 0 M200 120 l 14 6", "#ffffff", 2)
    for x, y, a in ((60, 60, -20), (90, 120, 15), (260, 50, 25), (290, 120, -10), (230, 160, 40), (40, 150, -35)):
        page = rect(-14, -18, 28, 36, "#f4f0e6", 'stroke="#888" stroke-width="1"') + rect(-10, -12, 20, 3, "#d0342c")
        page += stroke("M-10 -4 L10 -4 M-10 2 L10 2 M-10 8 L6 8", "#888", 1.2)
        body += group(page, f"translate({x} {y}) rotate({a})")
    body += lettering("SECRET", 260, 18, 9, "#d0342c", outline="#0b1020")
    return svg(defs, body)


def bowler(hx, hy, r):
    return (ellipse(hx, hy - r * 0.7, r * 1.5, r * 0.25, "#1a1a1a")
            + path(f"M{hx - r * 0.95:.1f} {hy - r * 0.7:.1f} C {hx - r:.1f} {hy - r * 1.9:.1f} {hx + r:.1f} {hy - r * 1.9:.1f} {hx + r * 0.95:.1f} {hy - r * 0.7:.1f} Z", "#1a1a1a"))


def polonium_umbrella():
    defs = lin("bg", ["#5a6470", "#2a3038"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(18):
        x, y = (i * 47) % W, (i * 29) % 160
        body += stroke(f"M{x} {y} l -4 12", "#aab4c0", 1.2, 'opacity="0.5"')
    body += rect(0, 176, W, 24, "#3a3f46")
    body += stroke("M300 40 L300 176", "#8a949c", 5) + rect(276, 30, 48, 30, "#2a6ad0", 'rx="4"') + lettering("BUS", 300, 38, 12, "#ffffff")
    body += person(150, 200, 1.4, SKIN[0], "#2a2a2e", face="smirk", hair="#5a5a5a", brows="angry")
    body += bowler(150, 200 - 64 * 1.4, 20 * 1.4)
    body += stroke("M186 160 L246 168", "#111", 7) + path("M184 156 Q 172 150 176 162", "none", 'stroke="#111" stroke-width="4"')
    body += poly([(246, 164), (262, 170), (246, 174)], "#888")
    for x, y in ((266, 178), (262, 186), (270, 190)):
        body += circle(x, y, 3, "#7aff4a")
    body += ellipse(266, 194, 22, 5, "#5aff2a", 'opacity="0.7"')
    body += stroke("M254 192 l -4 -8 M266 190 l 0 -10 M278 192 l 4 -8", "#9aff7a", 1.5)
    return svg(defs, body)


# ---------------------------------------------------------------- Mind additions

def schrodingers_cat():
    defs, body = bg_mind()
    body += poly([(80, 110), (272, 110), (262, 196), (90, 196)], "#c8935a", 'stroke="#8a5a2a" stroke-width="3"')
    body += poly([(80, 110), (60, 80), (150, 84), (176, 110)], "#d8a46a", 'stroke="#8a5a2a" stroke-width="2"')
    body += poly([(272, 110), (296, 82), (210, 86), (176, 110)], "#d8a46a", 'stroke="#8a5a2a" stroke-width="2"')
    # Left half alive, right half bones
    body += path("M176 40 C 136 40 126 76 130 100 C 134 118 156 124 176 124 Z", "#f2a24a", 'stroke="#8a5a2a" stroke-width="2"')
    body += poly([(136, 64), (140, 28), (162, 48)], "#f2a24a", 'stroke="#8a5a2a" stroke-width="2"')
    body += path("M176 40 C 216 40 226 76 222 100 C 218 118 196 124 176 124 Z", "#f2efe6", 'stroke="#8a8a80" stroke-width="2"')
    body += poly([(216, 64), (212, 28), (190, 48)], "#f2efe6", 'stroke="#8a8a80" stroke-width="2"')
    body += ellipse(158, 80, 8, 7, "#9ad04a") + rect(150, 74, 16, 6, "#f2a24a")
    body += ellipse(196, 80, 9, 8, "#222")
    body += stroke("M150 102 l -24 -4 M150 108 l -24 2", "#8a5a2a", 1.5)
    body += stroke("M176 98 L176 108 M166 112 Q 176 116 186 112", "#5a3a2a", 2)
    body += stroke("M196 102 L206 102 M194 108 L210 108", "#8a8a80", 2)
    body += stroke("M176 40 L176 124", "#8a5a2a", 1.5, 'stroke-dasharray="4 3"')
    return svg(defs, body)


def rokos_basilisk():
    defs, body = bg_mind()
    body += stroke("M30 190 C 40 120 120 170 140 120 C 160 70 220 90 236 70", "#3a8a3a", 30)
    body += stroke("M30 190 C 40 120 120 170 140 120 C 160 70 220 90 236 70", "#5aaa4a", 18)
    body += ellipse(252, 64, 30, 22, "#5aaa4a", 'stroke="#2a6a2a" stroke-width="3"')
    body += eye(262, 56, 6, "#c0392b")
    body += stroke("M280 70 l 14 2 l 4 -4 M294 72 l 4 4", "#c0392b", 2)
    body += path("M222 50 A 32 32 0 0 1 284 40", "none", 'stroke="#222" stroke-width="5"')
    body += ellipse(224, 58, 7, 11, "#222") + ellipse(282, 46, 7, 11, "#222")
    body += poly([(170, 160), (290, 160), (300, 176), (160, 176)], "#8a949c") + rect(184, 104, 92, 58, "#2a2a30", 'rx="3"')
    body += rect(190, 110, 80, 46, "#9fe0ff")
    body += rect(214, 116, 42, 34, "#ffe26a", 'transform="rotate(-6 235 133)"')
    body += lettering("NAUGHTY", 235, 122, 5, "#c0392b", width=1.2) + lettering("LIST", 235, 134, 7, "#c0392b", width=1.4)
    return svg(defs, body)


def trolley_problem():
    defs = lin("bg", ["#8ac06a", "#6aa04a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for d in ("M0 104 L150 104 C 200 104 220 50 352 50", "M0 104 L150 104 C 200 104 220 158 352 158"):
        body += stroke(d, "#8a6a4a", 22) + stroke(d, "#5a5a5a", 3, 'transform="translate(0 -7)"') + stroke(d, "#5a5a5a", 3, 'transform="translate(0 7)"')
    body += rect(30, 84, 70, 40, "#d0342c", 'rx="6" stroke="#7a1a1a" stroke-width="2"')
    for x in (40, 58, 76):
        body += rect(x, 90, 12, 12, "#bfe8ff")
    body += stroke("M150 132 L170 160", "#555", 5) + circle(150, 132, 6, "#d0342c") + rect(160, 156, 22, 8, "#555")
    for i in range(5):
        x = 260 + i * 16
        body += circle(x, 42, 5, SKIN[i % 4]) + rect(x - 4, 47, 8, 14, "#4a6a9a", 'rx="2"')
    body += stroke("M240 46 L340 46", "#c8a050", 2)
    body += circle(282, 152, 6, SKIN[0]) + rect(288, 148, 30, 10, "#4a4a4a", 'rx="3"') + rect(274, 140, 10, 7, "#1b1b22") + rect(275, 141, 8, 5, "#9fe0ff")
    return svg(defs, body)


def red_herring():
    defs, body = bg_shadow()
    body += poly([(80, 110), (40, 80), (44, 140)], "#c0392b")
    body += ellipse(150, 110, 80, 40, "#d84a3a", 'stroke="#8a1a1a" stroke-width="2"')
    body += path("M104 90 L196 90 L210 150 L96 150 Z", "#c8a878", 'stroke="#7a5a3a" stroke-width="2"')
    body += stroke("M150 90 L150 150 M104 90 L130 120 M196 90 L172 120", "#7a5a3a", 2)
    body += circle(140, 110, 3, "#5a3a1a") + circle(140, 130, 3, "#5a3a1a")
    body += eye(204, 98, 8)
    body += ellipse(206, 72, 40, 6, "#8a7a62", 'stroke="#3a3020" stroke-width="2"') + path("M180 72 L184 46 Q 206 54 228 46 L232 72 Z", "#9a8a70", 'stroke="#3a3020" stroke-width="2"') + rect(182, 64, 50, 5, "#3a3020")
    body += path("M226 116 Q 236 118 226 124", "none", 'stroke="#8a1a1a" stroke-width="2"')
    body += path("M196 130 C 240 140 280 120 300 100 L306 110 C 290 132 250 152 200 144 Z", "#d84a3a", 'stroke="#8a1a1a" stroke-width="2"')
    body += speed_lines(330, 84, 3, 16, "#fff7c8")
    return svg(defs, body)


def crewmate(cx, cy, s=1.0, color="#d0342c"):
    out = rect(cx - 42 * s, cy - 20 * s, 18 * s, 50 * s, color, 'rx="6" stroke="#222" stroke-width="3"')
    out += path(f"M{cx - 30 * s:.1f} {cy + 50 * s:.1f} L{cx - 30 * s:.1f} {cy - 20 * s:.1f} C {cx - 30 * s:.1f} {cy - 70 * s:.1f} {cx + 34 * s:.1f} {cy - 70 * s:.1f} {cx + 34 * s:.1f} {cy - 20 * s:.1f} L{cx + 34 * s:.1f} {cy + 50 * s:.1f} Z",
                color, 'stroke="#222" stroke-width="3"')
    out += rect(cx - 8 * s, cy - 40 * s, 48 * s, 26 * s, "#9fdcef", 'rx="12" stroke="#222" stroke-width="3"')
    out += rect(cx + 6 * s, cy - 36 * s, 18 * s, 6 * s, "#ffffff", 'rx="3" opacity="0.8"')
    return out


def imposter_syndrome():
    defs, body = bg_office()
    body += crewmate(170, 120, 1.15)
    body += sweat(222, 66, 1.4) + sweat(132, 58, 1.1)
    body += rect(60, 150, 240, 50, "#6a4a2a")
    body += rect(120, 132, 116, 30, "#2a2a2a", 'rx="2"') + rect(124, 136, 108, 22, "#c9a24a")
    body += lettering("SENIOR", 178, 138, 6, "#2a2a2a", width=1.3) + lettering("ENGINEER", 178, 147, 6, "#2a2a2a", width=1.3)
    return svg(defs, body)


# ---------------------------------------------------------------- Myth additions

def omniscient_space_time_bending_bearded_wizard():
    defs, body = bg_myth()
    body += cloud(176, 186, 3.2, "#ffffff", 0.95)
    body += person(150, 180, 1.45, SKIN[0], "#9fc3e0", face="flat", hair=OLD_HAIR, beard="#ffffff", brows="sad")
    body += stroke("M128 160 L150 180 L172 160", "#7aa0c0", 3)
    body += stroke("M186 150 L236 120", SKIN[0], 9)
    body += rect(232, 108, 14, 26, "#222", 'rx="3" transform="rotate(-30 239 121)"') + circle(240, 112, 3, "#d0342c")
    # A tiny universe, spinning
    body += stroke("M300 56 m-6 0 a6 6 0 1 1 12 0 a12 12 0 1 1 -24 0 a18 18 0 1 1 36 0", "#ffe9a0", 2.5)
    for x, y in ((280, 36), (322, 40), (290, 80), (318, 72)):
        body += poly(star_points(x, y, 5, 2, 4), "#ffffff")
    body += stroke("M250 112 l 18 -20 M256 120 l 26 -14", "#ffe9a0", 1.5, 'opacity="0.7" stroke-dasharray="3 3"')
    return svg(defs, body)


def cultist():
    defs, body = bg_myth()
    body += person(166, 200, 1.5, SKIN[1], "#2a1a3a", face="closed", hat="hood")
    body += stroke("M66 180 L66 66", "#8a5a2a", 6) + rect(4, 6, 124, 64, "#f4ecd8", 'stroke="#333" stroke-width="3"')
    body += lettering("THE END", 66, 13, 19, "#c0392b", width=3.6) + lettering("IS NEAR", 66, 42, 19, "#c0392b", width=3.6)
    # A pumpkin spice latte
    body += path("M236 120 L272 120 L266 182 L242 182 Z", "#f4f0e6", 'stroke="#aaa" stroke-width="2"')
    body += rect(240, 140, 30, 18, "#c87a3a") + ellipse(254, 118, 20, 6, "#ffffff") + rect(236, 112, 36, 7, "#ffffff", 'rx="3"')
    body += stroke("M214 170 L240 160", SKIN[1], 9)
    return svg(defs, body)


def phoenix(cx, cy, s=1.0):
    out = ""
    for f in (-1, 1):
        out += path(f"M{cx} {cy} C {cx + f * 40 * s} {cy - 10 * s} {cx + f * 80 * s} {cy - 50 * s} {cx + f * 96 * s} {cy - 70 * s} "
                    f"C {cx + f * 70 * s} {cy - 40 * s} {cx + f * 70 * s} {cy - 20 * s} {cx + f * 60 * s} {cy} "
                    f"C {cx + f * 50 * s} {cy + 10 * s} {cx + f * 20 * s} {cy + 10 * s} {cx} {cy + 6 * s} Z", "#ff7a1a", 'stroke="#ffd23a" stroke-width="2"')
        out += path(f"M{cx} {cy} C {cx + f * 30 * s} {cy - 6 * s} {cx + f * 50 * s} {cy - 26 * s} {cx + f * 60 * s} {cy - 40 * s} C {cx + f * 40 * s} {cy - 20 * s} {cx + f * 30 * s} {cy} {cx} {cy + 4 * s} Z", "#ffd23a")
    out += ellipse(cx, cy + 4 * s, 14 * s, 26 * s, "#ff5a1a")
    out += circle(cx, cy - 26 * s, 10 * s, "#ff7a1a") + poly([(cx + 8 * s, cy - 28 * s), (cx + 20 * s, cy - 24 * s), (cx + 8 * s, cy - 20 * s)], "#ffd23a")
    out += circle(cx + 3 * s, cy - 28 * s, 2 * s, "#222")
    out += flames(cx, cy + 50 * s, 3, 10 * s, 26 * s)
    return out


def phoenix_card():
    defs = lin("bg", ["#3a1a10", "#1a0a08"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += ellipse(176, 190, 110, 16, "#4a4a4a")
    body += rect(126, 148, 100, 42, "#2a2a2a", 'rx="10" stroke="#555" stroke-width="2"')
    body += rect(146, 146, 22, 6, "#111") + rect(184, 146, 22, 6, "#111") + rect(226, 160, 10, 6, "#555")
    for x, y in ((150, 120), (200, 110), (176, 96)):
        body += cloud(x, y, 0.5, "#5a5a5a", 0.6)
    body += phoenix(176, 80, 1.0)
    return svg(defs, body)


def excess_of_bread_and_wine():
    defs, body = bg_myth()
    for i, x in enumerate((40, 92, 260, 312)):
        body += person(x, 150, 0.8, SKIN[i % 4], ("#6a8ac8", "#c86a6a", "#6ac88a", "#c8a86a")[i], face="grin", hair="#4a2a1a", beard="#4a2a1a")
    body += circle(176, 52, 26, "#ffe46a", 'opacity="0.8"')
    body += person(176, 150, 1.0, SKIN[1], "#f4f0e6", face="grin", hair="#6a4a2a", beard="#6a4a2a")
    body += rect(10, 146, 332, 54, "#8a5a2a") + rect(10, 146, 332, 8, "#a8743a")
    # The jug, pouring
    body += path("M196 104 C 186 104 186 128 196 132 L222 132 C 232 128 232 104 222 104 Z", "#8a3a4a")
    body += stroke("M224 112 Q 250 116 252 138", "#a0283a", 4)
    body += rect(244, 134, 14, 14, "#d0342c")
    # Beer pong
    for x, y in ((40, 136), (56, 136), (72, 136), (48, 126), (64, 126), (56, 116)):
        body += path(f"M{x - 7} {y} L{x + 7} {y} L{x + 5} {y + 14} L{x - 5} {y + 14} Z", "#d0342c", 'stroke="#ffffff" stroke-width="1.5"')
    body += circle(110, 96, 4, "#ffffff") + stroke("M88 128 Q 98 90 110 96", "#ffffff", 1.5, 'stroke-dasharray="3 3"')
    return svg(defs, body)


def work_shift_rotation():
    # Two warehouse workers at the clocking-in machine: the worn-out one hands his hi-vis vest to the fresh one
    defs = lin("bg", ["#c9c2b0", "#8a8478"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 150, W, 50, "#6a645a")
    for x in range(10, W, 60):  # Warehouse shelving
        body += rect(x, 20, 46, 120, "none", 'stroke="#5a554c" stroke-width="3"') + rect(x + 6, 70, 34, 18, "#b08a5a")
    body += rect(160, 54, 32, 52, "#3a3f48", 'rx="4"') + rect(166, 60, 20, 12, "#9fe0a0") + rect(170, 80, 12, 4, "#111")
    body += person(96, 200, 1.25, SKIN[1], "#e8d24a", face="frown", brows="angry")  # The worn-out shift, bags under his eyes
    body += stroke("M84 118 q 5 3 10 0 M100 118 q 5 3 10 0", "#6a4a5a", 2)
    body += person(256, 200, 1.25, SKIN[2], "#4a6a8a", face="grin", hair="#2a1a10")
    vest = poly([(140, 140), (176, 132), (212, 140), (206, 186), (146, 186)], "#ff9a1a", 'stroke="#8a4a10" stroke-width="2"')
    body += vest + rect(148, 158, 56, 6, "#e8f0f0") + rect(148, 170, 56, 6, "#e8f0f0")
    body += speed_lines(176, 120, 3, 12, "#ffffff")
    return svg(defs, body)


def chain_smoker():
    # A wiry man smoking a length of steel chain like a cigarette, the far end swinging like a flail
    defs = lin("bg", ["#3a3a46", "#16161c"])
    body = rect(0, 0, W, H, "url(#bg)")
    for cx, cy, r in ((230, 60, 26), (262, 40, 20), (290, 26, 16), (210, 34, 14)):
        body += circle(cx, cy, r, "#9a9aa6", 'opacity="0.5"')
    body += person(150, 200, 1.4, SKIN[0], "#5a4a3a", face="smirk", hair="#4a3a2a")
    links = [(186, 118), (200, 110), (214, 104), (228, 102), (242, 106), (256, 116), (266, 130), (272, 146), (274, 162)]
    for i, (x, y) in enumerate(links):
        body += ellipse(x, y, 8, 5, "none", f'stroke="#b8bcc8" stroke-width="3" transform="rotate({30 if i % 2 else -20} {x} {y})"')
    body += circle(186, 118, 4, "#ff6a1a") + circle(186, 118, 2, "#ffe14a")
    body += speed_lines(290, 170, 3, 14, "#d8dce8")
    return svg(defs, body)


def monkey_hand():
    # A shriveled, mummified monkey's paw held up in a purple room: long knobbly fingers with pointed nails, a tuft of
    # black fur at the wrist. Three fingers still up (wishes left), the fourth already curled into the palm
    defs = lin("bg", ["#a27cc0", "#6a4288"]) + rad("glow", [("#b8ff8a", 0.45), ("#b8ff8a", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in range(0, W, 22):  # Wallpaper stripes
        body += rect(x, 0, 10, H, "#ffffff", 'opacity="0.05"')
    body += rect(14, 0, 64, 74, "#6a3a1a") + rect(22, 0, 48, 66, "#3a6a4a") + stroke("M30 52 q 12 -20 30 -6", "#2a4a3a", 3)
    body += circle(176, 84, 96, "url(#glow)")
    skin, shade, line, nail = "#93a374", "#6f7d55", "#2b3320", "#ddd3a0"
    # Wrist, palm and the wrinkles across it
    body += path("M152 186 C 150 176 146 170 142 162 C 132 140 134 112 146 98 L 210 96 C 222 112 220 140 212 162 C 208 170 204 176 202 186 Z",
                 skin, f'stroke="{line}" stroke-width="3"')
    body += path("M150 150 C 160 160 196 160 206 148 L 204 160 C 194 170 162 170 152 160 Z", shade, 'opacity="0.7"')
    for d in ("M154 118 q 22 6 50 -2", "M156 134 q 20 8 46 0", "M160 108 l 6 30", "M196 106 l -4 32", "M178 104 l 0 40"):
        body += stroke(d, line, 1.4, 'opacity="0.7"')
    # Fingers: bony segments with swollen knuckles, a pointed nail at the tip
    fingers = [
        [(152, 104), (148, 72), (145, 48), (146, 26)],
        [(170, 100), (170, 64), (171, 38), (173, 14)],
        [(188, 100), (191, 66), (194, 42), (197, 22)],
        [(205, 106), (222, 94), (229, 108), (219, 120)],  # Curled: a wish already used
        [(146, 132), (124, 120), (113, 100), (110, 82)],  # The thumb
    ]
    for width, color, knuckle in ((16, line, 8.5), (11, skin, 6.6)):
        for pts in fingers:
            d = "M" + " L ".join(f"{x} {y}" for x, y in pts)
            body += stroke(d, color, width)
            for x, y in pts[1:-1]:
                body += circle(x, y, knuckle, color)
    for pts in fingers:
        (x1, y1), (x2, y2) = pts[-2], pts[-1]
        length = math.hypot(x2 - x1, y2 - y1)
        ux, uy = (x2 - x1) / length, (y2 - y1) / length
        tip = (x2 + ux * 9, y2 + uy * 9)
        body += poly([(x2 - uy * 5, y2 + ux * 5), tip, (x2 + uy * 5, y2 - ux * 5)], nail, f'stroke="{line}" stroke-width="1.5"')
        for x, y in pts[1:-1]:  # Wrinkles over the knuckles
            body += stroke(f"M{x - 4} {y - 1} q 4 2 8 0", line, 1.2)
    # A tuft of black fur at the wrist, on a blue mount
    body += ellipse(176, 198, 46, 9, "#3a5aa8", 'stroke="#1a2a5a" stroke-width="2"')
    fur = [(124, 200)]
    for k in range(13):
        x = 128 + k * 8
        fur += [(x, 180 + (k % 2) * 4), (x + 4, 166 + (k % 3) * 3)]
    fur += [(232, 182), (228, 200)]
    body += poly(fur, "#15121a", 'stroke="#000" stroke-width="1.5"')
    for k in range(10):
        body += stroke(f"M{134 + k * 9} 196 l 3 -12", "#3a3440", 1.2)
    return svg(defs, body)

def rubber_chicken(cx, cy, s=1.0):
    out = ellipse(cx, cy, 34 * s, 12 * s, "#ffe23a", 'stroke="#c8a010" stroke-width="2"')
    out += path(f"M{cx + 30 * s:.1f} {cy - 4 * s:.1f} C {cx + 50 * s:.1f} {cy - 10 * s:.1f} {cx + 54 * s:.1f} {cy - 30 * s:.1f} {cx + 50 * s:.1f} {cy - 40 * s:.1f}", "none", f'stroke="#ffe23a" stroke-width="{9 * s:.1f}" stroke-linecap="round"')
    out += circle(cx + 50 * s, cy - 42 * s, 9 * s, "#ffe23a", 'stroke="#c8a010" stroke-width="2"')
    out += poly([(cx + 44 * s, cy - 50 * s), (cx + 48 * s, cy - 58 * s), (cx + 54 * s, cy - 50 * s), (cx + 58 * s, cy - 56 * s), (cx + 58 * s, cy - 46 * s)], "#e8302a")
    out += poly([(cx + 57 * s, cy - 44 * s), (cx + 70 * s, cy - 40 * s), (cx + 57 * s, cy - 36 * s)], "#ff8a1a")
    out += stroke(f"M{cx + 46 * s:.1f} {cy - 46 * s:.1f} l 6 4 M{cx + 52 * s:.1f} {cy - 46 * s:.1f} l -6 4", "#222", 1.5)
    out += stroke(f"M{cx - 20 * s:.1f} {cy + 10 * s:.1f} l -4 12 M{cx - 6 * s:.1f} {cy + 10 * s:.1f} l 2 12", "#ff8a1a", 3)
    return out


def sacrificial_ritual():
    defs = lin("bg", ["#1a0a14", "#3a1420"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += poly(star_points(176, 168, 80, 30, 5), "none", 'stroke="#f2efe6" stroke-width="2.5" transform="translate(0 168) scale(1 0.35) translate(0 -168)"')
    body += ellipse(176, 168, 92, 32, "none", 'stroke="#f2efe6" stroke-width="2.5"')
    body += rect(126, 118, 100, 32, "#5a5a62", 'stroke="#2a2a30" stroke-width="2"')
    body += rubber_chicken(158, 108, 0.9)
    for x in (40, 312):
        body += person(x, 200, 1.1, SKIN[2], "#2a0a1a", face="closed", hat="hood")
    for x in (90, 262):
        body += rect(x - 4, 150, 8, 22, "#f4ecd8") + flames(x, 150, 1, 0, 14)
    return svg(defs, body)


# ---------------------------------------------------------------- Corporate addition

def jet(cx, cy, s=1.0):
    out = ellipse(cx, cy, 70 * s, 12 * s, "#e8ecf0", 'stroke="#8a949c" stroke-width="2"')
    out += poly([(cx - 10 * s, cy), (cx + 20 * s, cy), (cx - 20 * s, cy + 34 * s), (cx - 34 * s, cy + 34 * s)], "#c8d0d8", 'stroke="#8a949c" stroke-width="1.5"')
    out += poly([(cx - 60 * s, cy - 6 * s), (cx - 74 * s, cy - 30 * s), (cx - 62 * s, cy - 30 * s), (cx - 46 * s, cy - 6 * s)], "#c8d0d8", 'stroke="#8a949c" stroke-width="1.5"')
    for i in range(6):
        out += circle(cx - 30 * s + i * 12 * s, cy - 3 * s, 2.6 * s, "#4a6a8a")
    out += path(f"M{cx + 60 * s:.1f} {cy - 6 * s:.1f} Q {cx + 76 * s:.1f} {cy:.1f} {cx + 60 * s:.1f} {cy + 6 * s:.1f}", "#4a6a8a")
    return out


def tax_exempt_corruption():
    defs, body = bg_office("up")
    body += rect(60, 90, 150, 110, "#f4ecd8", 'stroke="#8a7a5a" stroke-width="2"')
    body += poly([(50, 92), (135, 40), (220, 92)], "#c9a24a", 'stroke="#8a6a2a" stroke-width="2"')
    body += rect(120, 140, 30, 60, "#6a4a2a", 'rx="12"')
    for x in (78, 172):
        body += rect(x, 110, 20, 30, "#9fd8ff", 'rx="10"')
    body += stroke("M135 40 L135 24", "#c9a24a", 4)
    body += lettering("$", 135, -4, 30, "#f2c94c", width=6, outline="#8a6a2a")
    body += jet(272, 170, 0.9)
    body += group(rect(-80, -16, 160, 32, "none", 'stroke="#e8302a" stroke-width="4" rx="4"') + lettering("TAX EXEMPT", 0, -9, 16, "#e8302a"),
                  "translate(250 60) rotate(-12)", 0.9)
    return svg(defs, body)


def zeitgeist_world():
    # The spirit of the times: a huge pale ghost made of glowing hashtags and likes floats over a crowd lit by their phones
    defs = lin("bg", ["#2a1650", "#0e0a24"]) + rad("aura", [("#ff7ad9", 0.55), ("#ff7ad9", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    body += circle(176, 70, 110, "url(#aura)")
    # The ghost: a rounded head and a wavy tail
    body += path("M120 120 C 116 40 236 40 232 120 L 232 150 Q 218 136 204 150 Q 190 164 176 150 Q 162 136 148 150 Q 134 164 120 150 Z",
                 "#f4e9ff", 'opacity="0.92"')
    body += circle(158, 92, 9, "#2a1650") + circle(194, 92, 9, "#2a1650") + circle(161, 89, 3, "#ffffff") + circle(197, 89, 3, "#ffffff")
    body += path("M162 114 Q 176 126 190 114", "none", 'stroke="#2a1650" stroke-width="4" stroke-linecap="round"')
    # Trends orbiting it: hashtags, hearts and thumbs
    for x, y, c in ((84, 46, "#ffd23f"), (262, 40, "#4fc3ff"), (60, 104, "#7dff9a"), (292, 98, "#ff7ad9")):
        body += stroke(f"M{x - 6} {y - 10} L{x - 9} {y + 10} M{x + 6} {y - 10} L{x + 3} {y + 10} M{x - 12} {y - 3} L{x + 10} {y - 3} M{x - 13} {y + 4} L{x + 9} {y + 4}", c, 3)
    for x, y in ((112, 28), (240, 18)):
        body += path(f"M{x} {y + 8} C {x - 12} {y - 2} {x - 4} {y - 12} {x} {y - 3} C {x + 4} {y - 12} {x + 12} {y - 2} {x} {y + 8} Z", "#ff4f6d")
    # The crowd, faces lit blue by their phones
    for i, cx in enumerate(range(26, W, 44)):
        body += person(cx, H + 6, 0.8, SKIN[i % len(SKIN)], ["#3d5a80", "#5a3d80", "#2f6b5a", "#80503d"][i % 4], face="flat")
        body += phone(cx + 8, H - 34, 0.45, "#7fdcff")
    return svg(defs, body)


def rush_hour():
    # Gridlocked traffic at dusk, horns blaring, while a snail with a briefcase glides past in the empty bus lane
    defs = lin("bg", ["#f39a5a", "#5a2e5a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x, h in ((10, 70), (52, 96), (98, 60), (140, 110), (196, 80), (244, 120), (300, 74)):
        body += rect(x, 140 - h, 40, h, "#3a2440")
        for wy in range(146 - h, 132, 14):
            body += rect(x + 8, wy, 8, 6, "#ffd27a", 'opacity="0.7"') + rect(x + 24, wy, 8, 6, "#ffd27a", 'opacity="0.4"')
    body += rect(0, 132, W, 68, "#2b2b33")
    body += rect(0, 176, W, 24, "#8a3030") + stroke("M0 176 L352 176", "#f2f2f2", 2)  # The bus lane
    for i in range(0, W, 30):
        body += rect(i, 150, 16, 3, "#f2e27a")
    for i, x in enumerate(range(-10, W, 62)):
        color = ["#c0392b", "#2e86de", "#f1c40f", "#27ae60", "#8e44ad", "#e67e22"][i % 6]
        body += rect(x, 136, 54, 22, color, 'rx="6"') + rect(x + 10, 128, 32, 14, color, 'rx="5"')
        body += rect(x + 14, 131, 11, 8, "#bfe6ff") + rect(x + 28, 131, 11, 8, "#bfe6ff")
        body += circle(x + 12, 160, 6, "#111") + circle(x + 42, 160, 6, "#111")
        body += circle(x + 54, 144, 3, "#ffef9a")
    for x, y in ((60, 112), (186, 106), (300, 114)):  # Honks
        body += stroke(f"M{x} {y} l 8 -8 M{x + 6} {y + 4} l 10 -4 M{x + 2} {y - 4} l 2 -10", "#ffffff", 2.5)
    # The snail, winning
    body += ellipse(176, 192, 34, 6, "#7a5a3a") + path("M146 192 C 146 162 196 156 200 180 C 202 192 186 196 176 190", "#c98b3a", 'stroke="#6a4420" stroke-width="3"')
    body += path("M160 186 C 160 172 186 170 186 182", "none", 'stroke="#6a4420" stroke-width="3"')
    body += stroke("M210 190 L 214 172 M216 190 L 222 174", "#7a5a3a", 3) + circle(214, 170, 3, "#111") + circle(222, 172, 3, "#111")
    body += rect(126, 178, 18, 12, "#3a2a1a", 'rx="2"') + stroke("M131 178 q 4 -6 8 0", "#3a2a1a", 2)
    body += speed_lines(120, 186, 3, 18, "#ffffff")
    return svg(defs, body)


# ---------------------------------------------------------------- Mechanics test cards (2026-10-13)
def golden_dome():
    # A shimmering golden dome of light over one fat senator on a deckchair, missiles raining on the city outside it
    defs = lin("bg", ["#5a2a2a", "#1a0e14"]) + rad("dome", [("#ffe9a0", 0.55), ("#ffc94a", 0.25)])
    body = rect(0, 0, W, H, "url(#bg)")
    for x, h in ((0, 60), (40, 90), (92, 50), (250, 100), (300, 70), (330, 84)):
        body += rect(x, H - h, 40, h, "#2a1a24")
    for x, y in ((40, 30), (90, 60), (280, 20), (320, 64), (230, 44)):
        body += stroke(f"M{x - 18} {y - 30} L{x} {y}", "#ffb347", 3, 'opacity="0.8"') + circle(x, y, 5, "#ff6a1a")
    for x, y in ((60, 160), (300, 150)):
        body += circle(x, y, 18, "#ff8a2a", 'opacity="0.7"') + circle(x, y, 10, "#ffe14a")
    body += path("M86 190 C 86 70 266 70 266 190 Z", "url(#dome)", 'stroke="#ffd36a" stroke-width="3" opacity="0.9"')
    body += stroke("M150 190 L150 156 M200 190 L200 156 M146 158 L204 158", "#6a4a2a", 4)  # Deckchair
    body += person(176, 186, 0.9, SKIN[0], "#2a3a6a", face="smirk", hair="#ddd", tie="#c0392b")
    body += ellipse(176, 176, 26, 14, "#2a3a6a")  # The belly
    return svg(defs, body)


def presidential_pardon():
    # A smiling president signs a giant pardon for a creature in an orange jumpsuit still holding a smoking gun
    defs = lin("bg", ["#1e3a6a", "#0c1a34"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(7):
        body += rect(0, i * 14, 120, 7, "#b8322a" if i % 2 == 0 else "#f2f2f2", 'opacity="0.25"')
    body += rect(150, 60, 140, 110, "#f4ecd2", 'stroke="#8a7a50" stroke-width="2"')
    body += rect(166, 72, 108, 10, "#6a5a3a") + rect(166, 94, 90, 5, "#9a8a6a") + rect(166, 106, 100, 5, "#9a8a6a")
    body += stroke("M170 150 C 190 130 200 160 220 140 S 250 150 268 132", "#1a1a6a", 3)  # The flourish
    body += circle(262, 152, 12, "#c0392b", 'opacity="0.85"')
    body += person(84, H, 1.2, SKIN[0], "#1a1a2a", face="grin", hair="#f2c46a", tie="#c0392b")
    body += stroke("M118 150 L176 142", "#1a1a2a", 8) + stroke("M172 143 L186 139", "#d4af37", 3)  # Arm and pen
    body += person(316, H, 0.95, SKIN[1], "#ff7a1a", face="smirk")
    body += rect(296, 120, 22, 8, "#333") + rect(296, 126, 6, 10, "#333")
    body += path("M294 116 q -6 -10 2 -18 q 6 -8 0 -16", "none", 'stroke="#ccc" stroke-width="2" opacity="0.7"')
    return svg(defs, body)


def congruent_use_of_force():
    # A soldier fires a huge gatling gun at a single mosquito; the whole street behind it is riddled with holes
    defs = lin("bg", ["#c8b48a", "#6a5a3a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in range(150, W, 64):
        body += rect(x, 40, 56, 120, "#a89070", 'stroke="#6a5a3a" stroke-width="2"')
    for i in range(40):
        x, y = 160 + (i * 37) % 180, 50 + (i * 53) % 100
        body += circle(x, y, 3, "#2a2018")
    body += person(70, H, 1.3, SKIN[2], "#4a5a2a", face="shout", hat="helmet")
    body += rect(90, 120, 70, 18, "#3a3a3a", 'rx="4"')
    for dy in (-6, 0, 6):
        body += rect(158, 126 + dy, 26, 4, "#555")
    for i, (x, y) in enumerate(((200, 120), (230, 100), (260, 140), (300, 90), (330, 126))):
        body += stroke(f"M{x - 14} {y} L{x} {y}", "#ffe14a", 2)
    body += circle(240, 70, 3, "#111") + ellipse(236, 66, 5, 3, "#cfe8ff", 'opacity="0.8"') + ellipse(244, 66, 5, 3, "#cfe8ff", 'opacity="0.8"')
    body += stroke("M240 72 l 0 6", "#111", 1)  # The mosquito, untouched
    return svg(defs, body)


def weapons_of_mass_distraction():
    # A giant stream on a public screen holds the crowd: a blocky-game YouTuber screaming at his pickaxe, and a grinning
    # giveaway YouTuber in a hoodie waving a giant check. Behind it, soldiers quietly shuffle the enemy's tents around
    defs = lin("bg", ["#1e3050", "#0a1222"]) + rad("glow", [("#8fd0ff", 0.35), ("#8fd0ff", 0)])
    body = rect(0, 0, W, H, "url(#bg)") + circle(122, 92, 150, "url(#glow)")
    # The enemy camp, being rearranged
    for x, c in ((262, "#6a7a3a"), (300, "#7a6a3a"), (338, "#5a6a4a")):
        body += poly([(x - 20, 96), (x, 66), (x + 20, 96)], c, 'stroke="#2a2a1a" stroke-width="2"')
    body += stroke("M262 56 q 38 -22 76 0", "#ffffff", 2, 'opacity="0.7"') + stroke("M338 56 l -7 -6 M338 56 l -9 2", "#ffffff", 2)
    for x in (282, 322):
        body += person(x, 112, 0.42, SKIN[1], "#4a5a2a", face="flat", hat="helmet")
    # The screen, two streams side by side
    body += rect(8, 10, 230, 150, "#111", 'rx="6" stroke="#d4af37" stroke-width="4"')
    body += stroke("M80 160 L70 200 M166 160 L176 200", "#3a3a3a", 6)
    # Left: the blocky game, a grass-and-dirt world and a square-headed streamer mid-scream
    body += rect(14, 16, 108, 138, "#7ec8f0")
    for i, x in enumerate(range(14, 122, 18)):
        body += rect(x, 118, 18, 12, "#5aa83a") + rect(x, 130, 18, 24, "#8a5a32") + rect(x + 4, 136 + (i % 2) * 6, 5, 5, "#6a4224")
    body += rect(34, 42, 40, 40, "#e0a878", 'stroke="#5a3a20" stroke-width="2"') + rect(34, 42, 40, 12, "#4a2e18")
    body += rect(40, 58, 8, 6, "#ffffff") + rect(60, 58, 8, 6, "#ffffff") + rect(43, 59, 4, 5, "#3a5ad0") + rect(63, 59, 4, 5, "#3a5ad0")
    body += rect(46, 70, 16, 9, "#5a1a1a")  # Screaming
    body += rect(30, 82, 48, 36, "#2ab0b0") + rect(22, 84, 10, 26, "#2ab0b0") + rect(76, 84, 10, 26, "#e0a878")
    body += stroke("M86 98 L106 70", "#8a5a32", 5) + poly([(96, 64), (116, 68), (112, 76), (100, 72)], "#9aa0a8", 'stroke="#555" stroke-width="1.5"')
    body += rect(18, 20, 28, 12, "#e02a2a", 'rx="2"') + lettering("LIVE", 32, 23, 6, "#ffffff", width=1.2)
    # Right: the giveaway, a giant check in his hands
    body += rect(126, 16, 106, 138, "#2a6ad0")
    for i in range(6):
        body += rect(126 + i * 18, 16, 9, 138, "#ffffff", 'opacity="0.06"')
    body += person(170, 154, 1.0, SKIN[0], "#3a8ad8", face="grin", hair="#6a4a2a", beard="#6a4a2a")
    body += path("M149 88 C 150 64 190 64 191 88 C 184 78 156 78 149 88 Z", "#6a4a2a")  # Short hair on top
    body += rect(136, 104, 92, 40, "#ffffff", 'stroke="#2a6a3a" stroke-width="2" transform="rotate(-6 182 124)"')
    body += stroke("M146 118 L196 114 M146 128 L210 122 M200 136 L222 133", "#2a8a4a", 2.5, 'transform="rotate(-6 182 124)"')
    body += lettering("FREE", 212, 108, 8, "#2a8a4a", width=1.6)
    for x, y in ((136, 30), (214, 40), (150, 60)):
        body += rect(x, y, 14, 8, "#7ad07a", 'stroke="#2a6a3a" stroke-width="1" transform="rotate(20 {} {})"'.format(x, y))
    # The crowd, watching instead of the camp
    for i, x in enumerate(range(10, W, 34)):
        body += ellipse(x, 196, 16, 18, "#0c0c14") + ellipse(x, 172, 10, 11, "#0c0c14")
    return svg(defs, body)

def counterfeit():
    # A knockoff superhero in a cheap costume with a misspelled chest logo, flexing in a mirror that shows a real hero
    defs = lin("bg", ["#e8d8f0", "#a888c0"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(196, 20, 130, 170, "#cfe6f2", 'stroke="#8a6a3a" stroke-width="6" rx="8"')
    body += person(261, 186, 1.35, SKIN[0], "#2e5ad0", face="grin", hair="#2a1a0a")
    body += path("M228 152 C 214 132 220 112 236 116 M294 152 C 308 132 302 112 286 116", "none", 'stroke="#2e5ad0" stroke-width="12" stroke-linecap="round"')
    body += poly([(250, 150), (272, 150), (261, 166)], "#d4af37")
    body += person(110, 196, 1.2, SKIN[3], "#6a8ad0", face="grin", hair="#6a4a2a")
    body += path("M80 166 C 70 150 76 134 88 138", "none", 'stroke="#6a8ad0" stroke-width="8" stroke-linecap="round"')
    body += rect(96, 158, 28, 14, "#e8e04a", 'rx="3"') + stroke("M100 165 l 6 0 M108 162 l 0 6 M113 165 l 7 0", "#c0392b", 2)  # The wrong logo
    body += stroke("M128 150 q 6 -2 8 2", "#ffffff", 2)  # Sweat from flexing
    return svg(defs, body)


def private_equity():
    # A vampire banker in a pinstripe suit sucks gold coins out of a little family shop through a long straw
    defs = lin("bg", ["#2a1a3a", "#0e0814"])
    body = rect(0, 0, W, H, "url(#bg)") + circle(300, 40, 24, "#f2ecd0", 'opacity="0.9"')
    body += rect(196, 96, 120, 94, "#c88a4a", 'stroke="#5a3a1a" stroke-width="3"') + poly([(190, 98), (256, 60), (322, 98)], "#8a3a2a")
    body += rect(216, 130, 30, 60, "#5a3a1a") + rect(260, 120, 40, 30, "#bfe6ff", 'stroke="#5a3a1a" stroke-width="2"')
    body += rect(222, 104, 70, 14, "#f2ecd0") + rect(228, 109, 58, 4, "#8a3a2a")
    body += person(90, H, 1.35, "#dfe4ea", "#2a2a3a", face="smirk", hair="#111", tie="#8a1a1a")
    for x in range(64, 120, 8):
        body += stroke(f"M{x} {H - 50} L{x} {H}", "#5a5a6a", 1)
    body += poly([(80, 112), (84, 120), (88, 112)], "#ffffff") + poly([(94, 112), (98, 120), (102, 112)], "#ffffff")  # Fangs
    body += stroke("M104 110 C 140 100 180 130 230 150", "#e8e0c0", 4)
    for x, y in ((140, 108), (172, 118), (204, 136)):
        body += circle(x, y, 5, "#ffd23f", 'stroke="#a07a1a" stroke-width="1"')
    return svg(defs, body)


def shell_company():
    # A street hustler plays the shell game with three walnut shells, each stamped with a tax haven's flag
    defs = lin("bg", ["#3a6a5a", "#16302a"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(40, 130, 272, 70, "#6a4a2a") + rect(40, 128, 272, 8, "#8a6a3a")
    body += person(176, 120, 1.0, SKIN[1], "#3a3a3a", face="smirk", hair="#111", hat="cap")
    body += stroke("M146 100 L118 140 M206 100 L236 140", "#3a3a3a", 9)
    for x, flag in ((112, ("#c0392b", "#f2f2f2")), (176, ("#2e86de", "#f1c40f")), (240, ("#27ae60", "#f2f2f2"))):
        body += path(f"M{x - 22} 162 C {x - 22} 132 {x + 22} 132 {x + 22} 162 Z", "#a87a4a", 'stroke="#5a3a1a" stroke-width="2"')
        body += rect(x - 8, 142, 16, 10, flag[0]) + rect(x - 8, 146, 16, 3, flag[1])
    body += circle(208, 170, 7, "#ffd23f", 'stroke="#a07a1a" stroke-width="1.5"')
    body += stroke("M150 176 q 26 12 52 0 M210 182 q 20 8 40 -4", "#ffffff", 2, 'opacity="0.6"')
    return svg(defs, body)


def cleaner():
    # A calm man in a cleaning company jumpsuit: a mop in one hand, a silenced pistol in the other, two neat holes behind
    defs = lin("bg", ["#d8dce0", "#9aa2aa"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in range(0, W, 32):
        body += stroke(f"M{x} 0 L{x} 150", "#c0c6cc", 1)
    body += rect(0, 150, W, 50, "#8a9098")
    body += circle(276, 64, 34, "#ffffff", 'stroke="#c0392b" stroke-width="4"') + circle(276, 64, 20, "none", 'stroke="#c0392b" stroke-width="4"')
    body += circle(272, 60, 4, "#111") + circle(281, 67, 4, "#111")
    body += person(150, H, 1.35, SKIN[0], "#3a6aa0", face="flat", hair="#555")
    body += rect(132, 150, 36, 12, "#f2f2f2") + rect(136, 153, 28, 6, "#3a6aa0")
    body += stroke("M108 196 L 60 60", "#8a6a3a", 5) + path("M44 54 L76 54 L72 70 L48 70 Z", "#d8d0c0")
    body += stroke("M186 150 L214 128", "#3a6aa0", 9) + rect(212, 118, 22, 10, "#222") + rect(232, 120, 22, 6, "#444")
    return svg(defs, body)


def crypto_entrepreneur():
    # A guy in sunglasses and a hoodie on top of a human pyramid of recruits, a glowing coin passed down hand to hand
    defs = lin("bg", ["#1a1040", "#3a1a6a"]) + rad("coin", [("#ffe67a", 0.9), ("#ffe67a", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in range(10, W, 40):
        body += stroke(f"M{x} 0 L{x + 10} {H}", "#5a3aa0", 1, 'opacity="0.4"')
    rows = [(176, 92, 1), (148, 136, 2), (120, 180, 3), (92, 224, 4)]
    for y_, base, count in []:
        pass
    for i, (y, n) in enumerate(((196, 5), (156, 4), (116, 3))):
        for k in range(n):
            cx = 176 + (k - (n - 1) / 2) * 52
            body += person(cx, y + 4, 0.62, SKIN[(k + i) % len(SKIN)], ["#3d5a80", "#5a3d80", "#2f6b5a"][(k + i) % 3], face="frown", brows="sad")
    body += person(176, 78, 0.8, SKIN[0], "#222", face="grin", hair="#111", glasses="shades")
    for x, y in ((150, 108), (122, 150), (96, 192)):
        body += circle(x, y, 14, "url(#coin)") + circle(x, y, 6, "#ffd23f", 'stroke="#a07a1a" stroke-width="1"')
    return svg(defs, body)


def not_on_my_watch():
    # A fierce grandma in a crossing-guard vest holds up a stop sign, nervous creatures huddled behind her in a blanket fort
    defs = lin("bg", ["#8ad0e8", "#e8f4d0"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(0, 160, W, 40, "#7aa05a")
    body += path("M190 170 L210 90 L330 90 L346 170 Z", "#e88aa0", 'stroke="#a04a60" stroke-width="3"')
    body += stroke("M210 90 L200 170 M330 90 L340 170", "#a04a60", 3)
    for x, s in ((236, 0.55), (276, 0.6), (312, 0.5)):
        body += person(x, 170, s, SKIN[1], "#5a8ad0", face="frown", brows="sad")
    body += person(110, H, 1.3, SKIN[0], "#f2d23a", face="frown", brows="angry", hair="#e8e8e8")
    body += stroke("M96 160 L130 186 M124 160 L96 186", "#c8c8c8", 4, 'opacity="0.8"')  # Reflective stripes
    body += stroke("M74 150 L52 60", "#666", 5)
    body += poly([(38, 38), (52, 30), (68, 38), (72, 54), (64, 68), (48, 72), (36, 62), (32, 48)], "#c0392b", 'stroke="#ffffff" stroke-width="3"')
    body += rect(38, 48, 30, 6, "#ffffff")
    return svg(defs, body)


def one_for_the_road():
    # A pub regular raises two pints at once under a CLOSING TIME sign, the barman already putting chairs on tables
    defs = lin("bg", ["#5a3a1a", "#2a1a0a"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(0, 140, W, 60, "#3a2410") + rect(0, 136, W, 8, "#8a5a2a")
    body += rect(200, 16, 130, 26, "#c0392b", 'rx="4"') + rect(210, 26, 110, 6, "#ffd23f")
    body += person(140, H, 1.3, SKIN[2], "#2f6b5a", face="grin", hair="#6a3a1a")
    for x in (84, 196):
        body += rect(x - 14, 82, 28, 40, "#e8a83a", 'opacity="0.9" stroke="#ffffff" stroke-width="2"') + rect(x - 14, 76, 28, 10, "#fff6e0")
    body += stroke("M108 150 L92 122 M172 150 L190 122", "#2f6b5a", 9)
    body += person(290, 150, 0.7, SKIN[0], "#f2f2f2", face="flat", hair="#333")
    body += stroke("M262 112 L270 96 L300 96 L306 112 M270 96 L270 80 M300 96 L300 80", "#6a4a2a", 4)  # A chair upside down
    return svg(defs, body)


def mimetic_theory():
    # Two people face each other in identical poses, one of them slowly turning into the other's mirror image
    defs = lin("bg", ["#2a1a4a", "#6a3a8a"]) + lin("fade", ["#c8a0ff", "#7a5aa0"], x2=1, y2=0)
    body = rect(0, 0, W, H, "url(#bg)")
    body += stroke("M176 10 L176 190", "#e8d8ff", 2, 'stroke-dasharray="6 6" opacity="0.6"')
    body += person(100, H, 1.4, SKIN[1], "#d06a3a", face="smile", hair="#3a2a1a")
    body += group(person(252, H, 1.4, SKIN[1], "url(#fade)", face="smile", hair="#3a2a1a"), "translate(0 0)", 0.85)
    for i in range(6):
        y = 30 + i * 22
        body += circle(176 + (i % 2) * 8 - 4, y, 3, "#ffe6ff", 'opacity="0.7"')
    body += stroke("M128 60 Q 176 30 224 60", "#ffe6ff", 2, 'opacity="0.6"') + stroke("M218 54 l 6 6 l -8 2", "#ffe6ff", 2)
    return svg(defs, body)


def business_expense():
    # A CEO in a full three-piece suit, briefcase in hand, walked into a jail cell by a guard, handing him a receipt
    # stamped BUSINESS EXPENSE
    defs = lin("bg", ["#8a8f96", "#5a5f66"])
    body = rect(0, 0, W, H, "url(#bg)")
    for y in range(10, 150, 28):
        body += stroke(f"M0 {y} L{W} {y}", "#6a6f76", 1, 'opacity="0.6"')
    body += rect(0, 160, W, 40, "#4a4e54")
    body += rect(196, 20, 150, 150, "#2a2d33", 'opacity="0.55"')  # The cell
    for x in range(202, 346, 18):
        body += rect(x, 14, 7, 156, "#3a3e44", 'stroke="#1f2226" stroke-width="1.5"')
    body += rect(196, 14, 152, 8, "#3a3e44") + rect(196, 92, 152, 6, "#3a3e44")
    body += person(84, H, 1.25, SKIN[2], "#2a3f6a", face="flat", hat="peaked")  # The guard
    body += stroke("M108 150 L150 140", "#2a3f6a", 9)
    body += person(170, H, 1.3, SKIN[0], "#1c1c22", face="grin", tie="#c0392b", hair="#9aa0a6")  # The CEO
    body += poly([(156, 156), (166, 176), (170, 158)], "#ffffff") + poly([(184, 156), (174, 176), (170, 158)], "#ffffff")
    body += rect(196, 150, 34, 26, "#5a3a1e", 'rx="3"') + rect(207, 145, 12, 6, "none", 'stroke="#3a2412" stroke-width="2"')
    body += stroke("M146 160 L126 110", "#1c1c22", 8)  # The arm with the receipt, held up between them
    body += group(rect(-22, -30, 44, 60, "#fbf8ee", 'stroke="#999" stroke-width="1"')
                  + "".join(rect(-16, -22 + k * 8, 30 - (k % 2) * 10, 3, "#aaa") for k in range(4))
                  + group(rect(-24, -8, 48, 14, "none", 'stroke="#c0392b" stroke-width="2"') + lettering("EXPENSE", 0, -5, 6, "#c0392b", width=1.3), "rotate(-14)"),
                  "translate(124 66) rotate(-8)")
    return svg(defs, body)


def we_are_not_the_same():
    # The meme: two identical masked heroes in red suits with web lines, pointing at each other in a dim warehouse
    defs = lin("bg", ["#2a2620", "#4a4236"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in range(0, W, 44):
        body += rect(x, 0, 40, 120, "#3a342a", 'opacity="0.5"')
    body += rect(0, 150, W, 50, "#2a2620")

    def hero(cx, facing):
        out = path(f"M{cx - 46} 200 C {cx - 46} 130 {cx + 46} 130 {cx + 46} 200 Z", "#c62828")  # Shoulders
        out += path(f"M{cx - 20} 200 L{cx - 12} 150 L{cx + 12} 150 L{cx + 20} 200 Z", "#1e4fa0")
        out += ellipse(cx, 104, 30, 36, "#c62828")  # The masked head
        for k in range(-2, 3):
            out += stroke(f"M{cx} 70 Q {cx + k * 14} 104 {cx + k * 10} 140", "#7a1414", 1.2)
        out += stroke(f"M{cx - 30} 100 Q {cx} 92 {cx + 30} 100 M{cx - 26} 116 Q {cx} 110 {cx + 26} 116", "#7a1414", 1.2)
        for side in (-1, 1):
            out += path(f"M{cx + side * 4} 98 Q {cx + side * 20} 84 {cx + side * 24} 100 Q {cx + side * 14} 110 {cx + side * 4} 98 Z",
                        "#ffffff", 'stroke="#111" stroke-width="2.5"')
        hand = cx + facing * 56  # The pointing arm
        out += stroke(f"M{cx + facing * 30} 162 L{hand} 132", "#c62828", 14)
        out += circle(hand, 132, 9, "#c62828") + stroke(f"M{hand} 132 L{hand + facing * 16} 124", "#c62828", 6)
        return out

    body += hero(78, 1) + hero(274, -1)
    return svg(defs, body)


def reincarnation():
    # The wheel of rebirth: a worm on the left, a dog on the right, an arrow arching from one life to the next
    defs, body = bg_myth()
    wheel = circle(176, 92, 58, "none", 'stroke="#f2c84a" stroke-width="6" opacity="0.55"') + circle(176, 92, 12, "#f2c84a", 'opacity="0.55"')
    for k in range(8):
        a = k * math.pi / 4
        x, y = 176 + 58 * math.cos(a), 92 + 58 * math.sin(a)
        wheel += stroke(f"M176 92 L{x:.1f} {y:.1f}", "#f2c84a", 4, 'opacity="0.55"')
        wheel += circle(176 + 70 * math.cos(a), 92 + 70 * math.sin(a), 6, "#f2c84a", 'opacity="0.55"')
    body += wheel
    body += rect(0, 168, W, 32, "#5a3e24") + ellipse(86, 170, 50, 8, "#3e2a18")
    # The worm, last life
    body += stroke("M50 166 C 58 146 74 146 80 160 C 86 172 100 172 106 156 C 110 146 120 146 124 152", "#e88aa0", 12)
    body += stroke("M60 154 L62 160 M74 150 L76 157 M92 166 L93 160", "#c86a80", 2)
    body += circle(124, 150, 7, "#e88aa0") + circle(126, 148, 1.8, "#222")
    # The arrow from one life to the next
    body += path("M110 112 C 140 40 220 40 244 98", "none", 'stroke="#fff3c8" stroke-width="5" stroke-dasharray="10 6"')
    body += poly([(234, 96), (252, 108), (252, 88)], "#fff3c8")
    # The dog, this life
    dog = "#c8873e"
    body += ellipse(270, 154, 36, 22, dog) + rect(246, 158, 12, 22, dog, 'rx="5"') + rect(282, 158, 12, 22, dog, 'rx="5"')
    body += path("M302 150 C 322 140 326 120 318 112", "none", f'stroke="{dog}" stroke-width="8" stroke-linecap="round"')
    body += circle(248, 124, 24, dog)
    body += path("M228 110 C 218 120 220 142 230 146 C 236 132 236 118 228 110 Z", "#8a5424")
    body += path("M268 110 C 278 120 276 142 266 146 C 260 132 260 118 268 110 Z", "#8a5424")
    body += ellipse(240, 136, 12, 9, "#f2d2a0") + ellipse(238, 130, 5, 4, "#222")
    body += circle(240, 118, 3.5, "#222") + circle(256, 118, 3.5, "#222") + circle(241, 117, 1.2, "#fff") + circle(257, 117, 1.2, "#fff")
    body += path("M236 142 Q 240 158 246 142 Z", "#e8607a")
    body += ellipse(176, 182, 26, 7, "#f4a8c0", 'opacity="0.85"') + path("M162 182 Q 176 160 190 182 Z", "#f8c8d8")  # A lotus between them
    return svg(defs, body)


def technical_debt():
    # Ship it now, pay later: a duct-taped laptop beside an empty, cracked piggy bank and the dashed shapes of the
    # banknotes that are missing, a sticky note reading TODO
    defs, body = bg_tech()
    body += rect(0, 170, W, 30, "#2a2f2c")
    body += rect(40, 104, 120, 70, "#3a3f46", 'rx="5"') + rect(48, 112, 104, 54, "#0d1f1a")
    for k in range(4):
        body += rect(56, 120 + k * 11, 40 + (k * 23) % 44, 4, "#3fbf8a", 'opacity="0.8"')
    body += rect(28, 172, 144, 8, "#2a2f36", 'rx="3"')
    body += group(rect(-40, -6, 80, 12, "#b8b8b0", 'opacity="0.9"'), "translate(100 112) rotate(-28)")
    body += group(rect(-40, -6, 80, 12, "#b8b8b0", 'opacity="0.9"'), "translate(100 150) rotate(22)")
    body += rect(120, 86, 34, 30, "#ffe66a", 'transform="rotate(8 137 101)"') + lettering("TODO", 137, 96, 7, "#c0392b", width=1.4)
    # The piggy bank, empty and cracked
    body += ellipse(262, 140, 58, 40, "#f29ab0", 'stroke="#b8607a" stroke-width="3"')
    body += circle(318, 140, 14, "#f29ab0", 'stroke="#b8607a" stroke-width="3"') + circle(314, 138, 2.5, "#7a2a40") + circle(322, 138, 2.5, "#7a2a40")
    body += poly([(232, 104), (242, 86), (250, 104)], "#f29ab0") + circle(286, 126, 3, "#222")
    body += rect(226, 172, 12, 18, "#f29ab0") + rect(286, 172, 12, 18, "#f29ab0")
    body += rect(250, 102, 26, 5, "#7a2a40", 'rx="2"')
    body += stroke("M236 150 L250 140 L244 130 L258 122", "#7a2a40", 2.5)
    for x, y, r in ((190, 40, -8), (250, 30, 10), (300, 58, -14)):  # The banknotes that aren't there
        body += group(rect(-24, -12, 48, 24, "none", 'stroke="#9af0c8" stroke-width="2" stroke-dasharray="5 4"')
                      + lettering("$", 0, -6, 12, "#9af0c8", width=1.6), f"translate({x} {y}) rotate({r})")
    return svg(defs, body)


def scope_creep():
    # A Gantt chart that has outgrown its whiteboard: its bars run off the board, across the wall and out of the window,
    # past the red DEADLINE line, while a smiling manager sticks on one more note: +1 small feature
    defs = lin("bg", ["#e8e4dc", "#c8c0b0"]) + lin("sky", ["#8ac8f0", "#d8f0ff"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(262, 22, 80, 110, "url(#sky)") + rect(262, 22, 80, 110, "none", 'stroke="#6a6a6a" stroke-width="5"')
    body += stroke("M302 22 L302 132", "#6a6a6a", 3)
    body += rect(14, 16, 206, 132, "#ffffff", 'stroke="#9a9a9a" stroke-width="4"')
    for x in range(40, 220, 26):
        body += stroke(f"M{x} 24 L{x} 140", "#dcdcdc", 1)
    rows = ((24, 50, "#4a90d0", None), (40, 70, "#e8a03a", None), (70, 90, "#5ab06a", None),
            (90, 150, "#d05a8a", 248), (110, 196, "#8a6ad0", W), (130, 200, "#e05a4a", W))
    for i, (x0, x1, color, beyond) in enumerate(rows):
        y = 28 + i * 18
        body += rect(x0, y, x1 - x0, 11, color, 'rx="2"')
        if beyond:  # Taped on past the board's edge, across the wall and, for the worst, out of the window
            body += rect(x1, y + 1, beyond - x1, 9, color, 'rx="2" opacity="0.9"')
    body += stroke("M180 18 L180 146", "#d0342c", 3, 'stroke-dasharray="6 4"')
    body += lettering("DEADLINE", 180, 152, 7, "#d0342c", width=1.4)
    # The manager and the new sticky note
    body += rect(120, 112, 30, 28, "#ffe45a", 'transform="rotate(-6 135 126)"')
    body += lettering("+1", 135, 120, 9, "#222", width=1.8)
    body += person(84, 200, 1.05, SKIN[0], "#3d5a80", face="smile", hair="#6a4a2a", tie="#f2c94c")
    body += stroke("M108 166 C 116 152 120 142 124 134", "#3d5a80", 10) + circle(126, 132, 6, SKIN[0])
    return svg(defs, body)


def ship_of_theseus():
    # Every plank replaced until nothing's left of it: the ship is only a dotted outline on the water, and Theseus, in his
    # crested helmet, scratches his head on the shore wondering where his ship went
    defs, body = bg_mind()
    body += path("M0 160 Q 44 150 88 160 T 176 160 T 264 160 T 352 160 L352 200 L0 200 Z", "#7aa6d8", 'opacity="0.85"')
    dots = 'stroke="#ffffff" stroke-width="3" stroke-dasharray="6 6" stroke-linecap="round"'
    body += path("M150 120 L332 120 L308 158 L172 158 Z", "none", dots)
    body += path("M240 120 L240 30", "none", dots)
    body += path("M244 36 C 300 52 300 100 244 112", "none", dots) + path("M236 40 C 190 56 190 98 236 108", "none", dots)
    body += path("M0 170 C 30 150 110 150 130 172 L130 200 L0 200 Z", "#c8b48a")  # The shore
    # Theseus
    x, base = 70, 200
    body += person(x, base, 1.15, SKIN[1], "#b8322a", face="frown", brows="sad")
    hx, hy, r = x, base - 74, 23
    body += path(f"M{hx - r - 3} {hy + 6} C {hx - r - 2} {hy - r * 1.5} {hx + r + 2} {hy - r * 1.5} {hx + r + 3} {hy + 6} L{hx + r - 4} {hy - 2} L{hx - r + 4} {hy - 2} Z", "#c8963a", 'stroke="#7a5a1a" stroke-width="2"')
    body += path(f"M{hx - 7} {hy - 28} C {hx - 9} {hy - 52} {hx - 4} {hy - 60} {hx} {hy - 62} C {hx + 4} {hy - 60} {hx + 9} {hy - 52} {hx + 7} {hy - 28} Z", "#d0342c")  # The crest, front on
    for k in (-4, 0, 4):
        body += stroke(f"M{hx + k} {hy - 30} L{hx + k * 1.2} {hy - 56}", "#a82020", 1.2)
    body += stroke(f"M{x + 26} {base - 30} L{x + 30} {base - 92}", SKIN[1], 9) + circle(x + 28, base - 96, 7, SKIN[1])  # Scratching
    body += circle(126, 46, 22, "#ffffff", 'opacity="0.9"') + circle(106, 74, 6, "#ffffff", 'opacity="0.9"') + circle(98, 86, 3.5, "#ffffff", 'opacity="0.9"')
    body += lettering("?", 126, 32, 26, "#5a4a8a", width=3.5)
    return svg(defs, body)


def scapegoating():
    # The goat makes a break for it: front legs over the top rail of a wooden fence, back legs scrambling, looking back in a
    # sweat
    defs, body = bg_mind()
    body += rect(0, 170, W, 30, "#8ac078")
    wood, dark = "#b08050", "#6a4a2a"
    for x in (30, 120, 232, 322):  # The far posts and the rails
        body += rect(x - 7, 92, 14, 98, wood, f'stroke="{dark}" stroke-width="2"')
    for y in (104, 144):
        body += rect(0, y, W, 12, wood, f'stroke="{dark}" stroke-width="2"')
    goat, line = "#f2efe6", 'stroke="#a8a090" stroke-width="2"'
    g = ""
    g += rect(-40, 14, 9, 40, goat, line) + rect(-24, 16, 9, 38, goat, line)  # Back legs, scrambling on the near side
    g += ellipse(0, 0, 48, 24, goat, line)
    g += path("M-46 -8 l -12 -10 l 4 12 Z", goat, line)  # The tail
    g += path("M34 -10 C 44 -30 54 -40 64 -42 L72 -26 C 62 -22 52 -10 46 4 Z", goat, line)  # The neck
    g += ellipse(70, -40, 18, 12, goat, line)
    g += path("M62 -50 C 54 -70 40 -72 34 -62 C 44 -64 50 -58 56 -48 Z", "#8a7a5a")  # Horns
    g += path("M58 -40 C 46 -44 44 -34 54 -32 Z", goat, line)  # The ear
    g += eye(64, -44, 5) + path("M80 -32 L76 -18 L86 -30 Z", "#d8d0c0")  # Eye looking back, beard
    g += rect(30, 16, 9, 30, goat, line + ' transform="rotate(-60 34 16)"') + rect(42, 12, 9, 30, goat, line + ' transform="rotate(-70 46 12)"')
    body += group(g, "translate(150 98) rotate(-18)")
    for x in (176, 266):  # The near posts, in front of the goat
        body += rect(x - 7, 92, 14, 98, wood, f'stroke="{dark}" stroke-width="2"')
    for x, y in ((108, 70), (96, 86), (122, 60)):
        body += path(f"M{x} {y} q 3 6 0 9 q -3 -3 0 -9 Z", "#8ad0ff")
    return svg(defs, body)


def meta_group_chat():
    # A messenger group chat in the green-and-beige look everyone knows: THE JURY (47 members), gavels and skulls piling up
    # under a blurry photo of one guy, double ticks everywhere; the guy himself sweats alone outside the phone
    defs = lin("bg", ["#1f3a34", "#0e1c1a"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(34, 4, 180, 194, "#111", 'rx="16"') + rect(42, 14, 164, 176, "#ece5dd", 'rx="6"')
    for x, y in ((60, 70), (150, 90), (90, 130), (180, 150), (70, 172), (170, 60)):  # The faint doodle wallpaper
        body += circle(x, y, 6, "none", 'stroke="#d8cfc4" stroke-width="1.5"')
    body += rect(42, 14, 164, 30, "#075e54", 'rx="6"') + rect(42, 34, 164, 10, "#075e54")
    body += circle(60, 29, 9, "#cfd8dc") + lettering("THE JURY", 112, 20, 8, "#ffffff", width=1.4)
    body += lettering("47 MEMBERS", 108, 32, 4, "#b2dfdb", width=0.9)
    # The blurry photo, sent in
    body += rect(52, 52, 82, 50, "#ffffff", 'rx="5"') + rect(56, 56, 74, 38, "#a8aeb8", 'rx="3"')
    body += circle(93, 70, 9, "#7a808a", 'opacity="0.8"') + ellipse(93, 92, 16, 8, "#7a808a", 'opacity="0.8"')

    def ticks(x, y):
        return stroke(f"M{x} {y} l 3 3 l 6 -6 M{x + 5} {y + 3} l 2 0 l 6 -6", "#34b7f1", 1.4)

    for i, y in enumerate((108, 128, 148)):
        mine = i % 2 == 0
        x, w = (118, 80) if mine else (52, 74)
        fill = "#dcf8c6" if mine else "#ffffff"
        body += rect(x, y, w, 16, fill, 'rx="6"')
        body += poly([(x + w - 2, y), (x + w + 6, y), (x + w - 2, y + 6)] if mine else [(x + 2, y), (x - 6, y), (x + 2, y + 6)], fill)
        if mine:  # Gavels, with read ticks
            for k in range(3):
                gx = x + 8 + k * 18
                body += rect(gx, y + 4, 10, 6, "#8a5a2a", 'rx="2"') + stroke(f"M{gx + 5} {y + 8} L{gx + 14} {y + 12}", "#8a5a2a", 2)
            body += ticks(x + w - 16, y + 9)
        else:
            body += skull(x + 14, y + 8, 0.28) + skull(x + 32, y + 8, 0.28) + skull(x + 50, y + 8, 0.28)
    body += rect(46, 168, 128, 16, "#ffffff", 'rx="8"') + circle(190, 176, 9, "#128c7e")  # Typing bar and the mic
    body += rect(187, 170, 6, 9, "#ffffff", 'rx="3"')
    body += person(290, H, 1.15, SKIN[1], "#7a8a9a", face="frown", brows="sad", hair="#3a2a1a")
    for x, y in ((264, 98), (316, 104), (260, 118)):
        body += path(f"M{x} {y} q 3 6 0 9 q -3 -3 0 -9 Z", "#8ad0ff")
    return svg(defs, body)


def social_panopticon():
    # A real panopticon seen from inside: a ring of cells on two floors around a central watchtower, every inmate lit by
    # their own phone, the tower's single eye recording them all
    defs = lin("bg", ["#0b0e1a", "#1e2236"]) + lin("tower", ["#5a5f70", "#2a2e3a"], 0, 0, 1, 0)
    body = rect(0, 0, W, H, "url(#bg)")

    def cell(x, y, w, h, lit):
        out = rect(x, y, w, h, "#14161f", 'stroke="#6a6f80" stroke-width="1.5"')
        out += rect(x + w * 0.25, y + h * 0.35, w * 0.5, h * 0.65, "#2a2e3a")  # Someone in it
        out += circle(x + w / 2, y + h * 0.3, w * 0.16, "#2a2e3a")
        if lit:
            out += rect(x + w * 0.42, y + h * 0.45, w * 0.18, h * 0.2, "#7ad8ff") + circle(x + w / 2, y + h * 0.3, w * 0.16, "#7ad8ff", 'opacity="0.35"')
        for k in range(1, 4):  # The bars
            out += stroke(f"M{x + w * k / 4:.1f} {y} L{x + w * k / 4:.1f} {y + h}", "#8a8fa0", 1)
        return out

    # The far side of the ring: small cells, two floors
    for floor, y in enumerate((30, 66)):
        body += rect(0, y - 4, W, 34, "#3a3e4c")
        for k in range(10):
            body += cell(8 + k * 34, y, 26, 26, (k + floor) % 3 != 1)
    body += rect(0, 96, W, 6, "#4a4e5c")
    # The tower in the middle, its eye on everyone
    body += rect(146, 18, 60, 160, "url(#tower)") + poly([(138, 20), (176, -6), (214, 20)], "#3a3e4c")
    for y in (60, 100, 140):
        body += rect(152, y, 48, 6, "#0b0e1a")
    body += ellipse(176, 40, 20, 11, "#f2efe6") + circle(176, 40, 8, "#2a3a6a") + circle(176, 40, 3.5, "#111") + circle(196, 28, 4, "#e03a3a")
    # The near side: bigger cells, a walkway rail in front
    for floor, y in enumerate((112, 152)):
        for k, x in enumerate((4, 64, 238, 298)):
            body += cell(x, y, 50, 40, (k + floor) % 2 == 0)
    body += rect(0, 150, 140, 3, "#8a8fa0") + rect(212, 150, 140, 3, "#8a8fa0")
    return svg(defs, body)


def control_room():
    # A wall of surveillance monitors, two operators asleep in their headsets with cold coffee
    defs = lin("bg", ["#10141c", "#1c2430"])
    body = rect(0, 0, W, H, "url(#bg)")
    for row, y in enumerate((8, 58)):
        for col, x in enumerate((20, 102, 184, 266)):
            screen = "#2a6a4a" if (row + col) % 2 else "#2a4a6a"
            body += rect(x, y, 70, 44, "#2a2a30", 'rx="4"') + rect(x + 5, y + 5, 60, 34, screen, 'rx="2"')
    body += rect(0, 150, W, 50, "#3a3328")
    for x, skin in ((110, SKIN[1]), (250, SKIN[3])):
        body += person(x, H, 1.0, skin, "#3d4a5a", face="closed")
        body += stroke(f"M{x - 22} {H - 64} C {x - 22} {H - 96} {x + 22} {H - 96} {x + 22} {H - 64}", "#111", 4)
        body += rect(x - 27, H - 70, 9, 14, "#111", 'rx="3"')
    body += rect(160, 160, 18, 22, "#f2efe6", 'rx="2"') + lettering("Z", 120, 98, 12, "#ffffff") + lettering("Z", 268, 92, 14, "#ffffff")
    return svg(defs, body)


def humanitarian_trafficking_action():
    # On a dusty airstrip, a crate stencilled MEDICINE with its lid slid open on rifles; the aid official beams at the
    # camera flashes while, behind the crate, a man in sunglasses counts the cash
    defs = lin("bg", ["#f6d49a", "#e0a060"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += path("M0 60 L120 52 L180 40 L210 44 L180 58 L352 64 L352 72 L0 72 Z", "#b8a890", 'opacity="0.7"')  # A cargo plane
    body += poly([(150, 44), (166, 22), (176, 22), (172, 46)], "#b8a890", 'opacity="0.7"')
    body += rect(0, 150, W, 50, "#c89a62")
    # The smuggler behind the crate
    body += person(250, 150, 1.0, SKIN[2], "#3a3a32", face="smirk", glasses="shades", hair="#1a1a1a")
    for k in range(3):
        body += rect(268 + k * 6, 92 - k * 4, 26, 13, "#5aa05a", 'stroke="#2a6a2a" stroke-width="1" transform="rotate(-20 280 98)"')
    # The crate: MEDICINE outside, rifles inside
    body += rect(150, 112, 150, 78, "#b88a4a", 'stroke="#6a4a22" stroke-width="3"')
    body += lettering("MEDICINE", 225, 156, 10, "#6a4a22", width=2)
    body += rect(214, 168, 22, 6, "#d0342c") + rect(222, 160, 6, 22, "#d0342c")
    body += rect(150, 112, 150, 30, "#2a2418")
    for k in range(4):  # Rifle stocks and barrels
        x = 162 + k * 32
        body += stroke(f"M{x} 138 L{x + 22} 116", "#1a1a1a", 5) + rect(x - 6, 130, 14, 10, "#6a3a1a", 'rx="2" transform="rotate(-45 {} 135)"'.format(x))
    body += poly([(142, 104), (300, 104), (308, 112), (150, 112)], "#c89a5a", 'stroke="#6a4a22" stroke-width="2" transform="rotate(-8 225 108)"')
    # The aid official, waving to the cameras
    body += person(84, H, 1.05, SKIN[0], "#2a6ad0", face="grin", hair="#6a4a2a")
    body += lettering("AID", 84, 172, 9, "#ffffff", width=2)
    body += stroke("M110 158 L132 112", "#2a6ad0", 10) + circle(134, 106, 7, SKIN[0])
    for x, y in ((14, 30), (40, 14), (24, 92)):  # Camera flashes
        body += star_points_poly(x, y)
    return svg(defs, body)


def star_points_poly(cx, cy):
    return poly(star_points(cx, cy, 12, 4, 4), "#ffffff", 'opacity="0.9"')


def no_glove_no_love():
    # A woman wagging her index finger, no, no, motion lines around it; in her other hand a little foil packet held up
    defs = lin("bg", ["#ffd8e8", "#f2a8c8"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x, y in ((40, 40), (300, 30), (60, 150), (320, 140)):
        body += path(f"M{x} {y + 6} C {x - 10} {y - 6} {x - 2} {y - 14} {x} {y - 4} C {x + 2} {y - 14} {x + 10} {y - 6} {x} {y + 6} Z", "#ff6a8a", 'opacity="0.5"')
    body += person(176, H, 1.5, SKIN[1], "#c0306a", face="smirk", brows="angry", hat="bob")
    body += path("M160 120 Q 168 124 176 121", "none", 'stroke="#c0304a" stroke-width="3"')  # Lipstick
    # The wagging hand, raised, index finger up
    body += stroke("M118 200 C 108 170 100 140 96 118", "#c0306a", 18)
    body += rect(84, 92, 26, 26, SKIN[1], 'rx="8"') + rect(90, 58, 9, 38, SKIN[1], 'rx="4"')
    for k in range(3):
        body += path(f"M{76 - k * 8} {70 - k * 2} q -6 10 0 20", "none", 'stroke="#7a3050" stroke-width="2"')
        body += path(f"M{114 + k * 8} {70 - k * 2} q 6 10 0 20", "none", 'stroke="#7a3050" stroke-width="2"')
    # The other hand, holding up the foil packet
    body += stroke("M236 200 C 248 172 256 150 260 132", "#c0306a", 18)
    body += rect(248, 112, 26, 24, SKIN[1], 'rx="8"')
    body += group(rect(-18, -18, 36, 36, "#e8c040", 'stroke="#a8862a" stroke-width="2"') + circle(0, 0, 11, "none", 'stroke="#a8862a" stroke-width="2"'),
                  "translate(262 96) rotate(12)")
    return svg(defs, body)


def sex_clanker():
    # Candlelight and satin: a knight in full shining armor reclines on a red heart-shaped bed, propped on one elbow, a rose
    # held in the visor, one knee up; the heart shield leans against the bed and rose petals are everywhere
    defs = lin("bg", ["#4a1430", "#1e0816"]) + lin("steel", ["#ffffff", "#9aa2b0"]) + rad("glow", [("#ffb070", 0.45), ("#ffb070", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in (34, 318):  # Candles
        body += circle(x, 90, 46, "url(#glow)") + rect(x - 6, 92, 12, 40, "#f4ecd8") + path(f"M{x} 76 q 6 8 0 14 q -6 -6 0 -14 Z", "#ffcf5a")
    body += path("M176 196 C 70 160 40 112 74 92 C 104 74 150 90 176 116 C 202 90 248 74 278 92 C 312 112 282 160 176 196 Z", "#a8122a")
    body += path("M176 196 C 90 166 66 128 84 112 C 120 96 150 120 176 140 C 202 120 232 96 268 112 C 286 128 262 166 176 196 Z", "#c81e3a")
    for x, y in ((120, 160), (210, 168), (150, 182), (246, 150), (100, 138), (230, 186), (190, 150)):
        body += ellipse(x, y, 5, 3, "#ff4a6a", f'transform="rotate({x % 60} {x} {y})"')
    # The knight: lying on its side, head on the left propped on a gauntlet
    body += path("M118 126 C 120 106 200 104 226 120 C 236 134 220 146 196 146 L130 146 C 120 146 116 136 118 126 Z", "url(#steel)", 'stroke="#5a6270" stroke-width="2"')
    body += path("M222 128 L286 112 L296 120 L232 142 Z", "url(#steel)", 'stroke="#5a6270" stroke-width="2"')  # The straight leg
    body += path("M210 124 L252 84 L262 92 L236 132 Z", "url(#steel)", 'stroke="#5a6270" stroke-width="2"')  # The raised knee...
    body += path("M252 84 L290 118 L282 124 L248 96 Z", "url(#steel)", 'stroke="#5a6270" stroke-width="2"')  # ...and its shin
    body += path("M96 148 L106 112 L118 114 L112 150 Z", "url(#steel)", 'stroke="#5a6270" stroke-width="2"')  # The arm it leans on
    body += path("M74 104 C 74 72 128 72 128 104 C 128 118 112 124 101 124 C 90 124 74 118 74 104 Z", "url(#steel)", 'stroke="#5a6270" stroke-width="2"')
    body += rect(84, 98, 40, 5, "#2a2f3a", 'rx="2"') + path("M100 78 C 112 58 132 60 138 72", "none", 'stroke="#d0342c" stroke-width="6"')
    body += stroke("M118 101 L156 92", "#3a7a3a", 2.5) + circle(160, 90, 7, "#d0342c") + circle(157, 87, 4, "#ff5a6a")  # The rose
    body += path("M300 150 L340 150 L338 182 C 334 194 322 198 320 199 C 318 198 306 194 302 182 Z", "url(#steel)", 'stroke="#5a6270" stroke-width="2"')
    body += path("M320 186 C 308 178 304 170 306 164 C 308 158 316 156 320 163 C 324 156 332 158 334 164 C 336 170 332 178 320 186 Z", "#d0342c")
    return svg(defs, body)


def tycoon(cx, base, s=1.0, face="smirk", brows=None, shirt="#1a1a1a", hat="tophat"):
    """The board-game tycoon: bald with white tufts, a big white moustache, a top hat, a tailcoat with a bow tie."""
    hx, hy = cx, base - 64 * s
    out = person(cx, base, s, SKIN[0], shirt, face=face, hair=OLD_HAIR, hat=hat, brows=brows)
    out += path(f"M{hx:.1f} {hy + 4 * s:.1f} C {hx - 8 * s:.1f} {hy + 1 * s:.1f} {hx - 18 * s:.1f} {hy + 4 * s:.1f} {hx - 20 * s:.1f} {hy + 10 * s:.1f} "
                f"C {hx - 12 * s:.1f} {hy + 8 * s:.1f} {hx - 6 * s:.1f} {hy + 9 * s:.1f} {hx:.1f} {hy + 7 * s:.1f} "
                f"C {hx + 6 * s:.1f} {hy + 9 * s:.1f} {hx + 12 * s:.1f} {hy + 8 * s:.1f} {hx + 20 * s:.1f} {hy + 10 * s:.1f} "
                f"C {hx + 18 * s:.1f} {hy + 4 * s:.1f} {hx + 8 * s:.1f} {hy + 1 * s:.1f} {hx:.1f} {hy + 4 * s:.1f} Z", "#f4f4f4", 'stroke="#c8c8c8" stroke-width="1"')
    out += circle(hx, hy + 2 * s, 3.5 * s, "#e8a888")  # The nose
    out += poly([(cx - 9 * s, base - 40 * s), (cx, base - 36 * s), (cx - 9 * s, base - 32 * s)], "#d0342c")
    out += poly([(cx + 9 * s, base - 40 * s), (cx, base - 36 * s), (cx + 9 * s, base - 32 * s)], "#d0342c")
    out += poly([(cx - 10 * s, base - 42 * s), (cx, base - 4 * s), (cx + 10 * s, base - 42 * s)], "#ffffff", 'opacity="0.95"')  # Shirt front
    return out


def violence_monopoly():
    # The board-game tycoon as the police: top hat, white moustache, a badge pinned to the tailcoat, a baton slapped into
    # his palm, glaring straight at you from the GO TO JAIL corner
    defs = lin("bg", ["#cfe6d0", "#9cc8a4"])
    body = rect(0, 0, W, H, "url(#bg)")
    for k, color in enumerate(("#d0342c", "#f2c94c", "#2a6ad0", "#2a9a4a", "#8a3a9a", "#e8803a", "#7ac8e8", "#d0342c")):
        body += rect(k * 44, 0, 44, 30, "#fbf8ee", 'stroke="#2a2a2a" stroke-width="1.5"') + rect(k * 44, 0, 44, 9, color, 'stroke="#2a2a2a" stroke-width="1.5"')
    body += rect(0, 30, 74, 74, "#fbf8ee", 'stroke="#2a2a2a" stroke-width="1.5"')
    body += lettering("GO TO", 37, 44, 9, "#d0342c", width=1.8) + lettering("JAIL", 37, 64, 11, "#d0342c", width=2)
    body += rect(278, 30, 74, 74, "#fbf8ee", 'stroke="#2a2a2a" stroke-width="1.5"')
    for x in range(286, 346, 10):
        body += rect(x, 40, 4, 56, "#2a2a2a")
    body += tycoon(176, H, 1.75, face="frown", brows="angry", shirt="#14182a")
    body += poly(star_points(214, 158, 11, 5, 6), "#f2c94c", 'stroke="#a87a1a" stroke-width="1.5"')  # The badge
    body += stroke("M224 200 C 232 186 240 172 244 160", "#14182a", 22)  # His arm, raised
    body += group(rect(-5, -70, 10, 76, "#1a1a1a", 'rx="4"') + rect(-7, 2, 14, 18, "#3a3a3a", 'rx="3"'), "translate(246 154) rotate(30)")
    body += circle(246, 154, 12, "#ffffff", 'stroke="#c8c8c8" stroke-width="1.5"')  # The white glove on the baton
    return svg(defs, body)


def solidarity_towards_the_small_businessman():
    # A fan-subscription website (ONLYFUNDS): the board-game tycoon poses in a pink bathrobe on webcam, and below, tired
    # workers in hi-vis and scrubs send him their coins
    defs = lin("bg", ["#eaf6fc", "#cfe8f4"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(14, 6, 324, 130, "#ffffff", 'rx="6" stroke="#9ac8e0" stroke-width="2"')
    body += rect(14, 6, 324, 22, "#00aff0", 'rx="6"') + rect(14, 20, 324, 8, "#00aff0")
    body += lettering("ONLYFUNDS", 82, 11, 9, "#ffffff", width=1.8)
    body += rect(24, 34, 186, 96, "#2a2a3a", 'rx="4"')  # The webcam frame
    body += rect(24, 34, 186, 96, "#f0d8e0", 'rx="4" opacity="0.25"')
    body += tycoon(117, 130, 0.95, face="smirk", shirt="#f29ab0")
    body += path("M85 130 L104 96 L117 112 L130 96 L149 130 Z", "#e88aa8")  # The bathrobe's lapels
    body += circle(36, 44, 4, "#e03a3a") + lettering("LIVE", 54, 41, 5, "#ffffff", width=1)
    body += rect(222, 40, 106, 26, "#00aff0", 'rx="13"') + lettering("SUBSCRIBE", 275, 48, 6, "#ffffff", width=1.2)
    body += lettering("TIPS", 252, 78, 7, "#2a2a3a", width=1.4)
    for k in range(5):
        body += coin(240 + (k % 3) * 22, 100 + (k // 3) * 18, 8)
    for k, (x, shirt) in enumerate(((60, "#f2c94c"), (150, "#5ab8a8"), (236, "#e8803a"), (316, "#f2c94c"))):
        body += person(x, H, 0.7, SKIN[k % 4], shirt, face="flat", brows="sad")
        body += coin(x + 18, 150 - k % 2 * 8, 7) + stroke(f"M{x + 18} {140 - k % 2 * 8} l 0 -8", "#c8a040", 2, 'stroke-dasharray="2 3"')
    return svg(defs, body)


def inheritance():
    # Grandma at peace in an open coffin, hands folded on lilies, while the grandson in his best suit grins over her and
    # fans out the banknotes from her will
    defs = lin("bg", ["#4a3a5a", "#2a2032"]) + rad("glow", [("#ffd890", 0.4), ("#ffd890", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in (24, 330):
        body += circle(x, 70, 40, "url(#glow)") + rect(x - 5, 72, 10, 40, "#f4ecd8") + path(f"M{x} 58 q 5 7 0 12 q -5 -5 0 -12 Z", "#ffcf5a")
    body += poly([(10, 150), (40, 116), (232, 116), (252, 150), (232, 186), (40, 186)], "#6a3a1e", 'stroke="#3a1e0e" stroke-width="3"')
    body += poly([(28, 150), (50, 126), (222, 126), (238, 150), (222, 176), (50, 176)], "#f4ecf4")  # The satin lining
    body += ellipse(150, 150, 70, 18, "#d8c8e0")  # Her, under the lace
    body += circle(70, 146, 18, "#f0d8c8") + circle(56, 140, 10, "#e8e8e8") + circle(60, 132, 8, "#e8e8e8")  # Hair in a bun
    body += stroke("M64 144 q 3 2 6 0 M76 144 q 3 2 6 0", "#5a4a4a", 1.4) + path("M68 154 Q 74 157 80 154", "none", 'stroke="#5a4a4a" stroke-width="1.4"')
    body += circle(67, 144, 5, "none", 'stroke="#8a7a6a" stroke-width="1.2"') + circle(80, 144, 5, "none", 'stroke="#8a7a6a" stroke-width="1.2"')
    for x, y in ((128, 136), (140, 130), (152, 138)):
        body += circle(x, y, 6, "#ffffff") + circle(x, y, 2, "#f2c94c")
    body += ellipse(140, 146, 10, 6, "#f0d8c8")  # Folded hands
    body += person(290, H, 1.15, SKIN[0], "#22222a", face="grin", tie="#2a2a2a", hair="#c8963a")
    for k in range(5):
        body += group(rect(-24, -9, 48, 18, "#5aa05a", 'stroke="#2a6a2a" stroke-width="1.5"') + lettering("$", 0, -5, 9, "#2a6a2a", width=1.4),
                      f"translate(250 98) rotate({-40 + k * 14})")
    body += circle(262, 104, 8, SKIN[0])
    body += group(rect(-16, -20, 32, 40, "#fbf3dc", 'stroke="#a8986a" stroke-width="1.5"') + lettering("WILL", 0, -12, 6, "#5a4a2a", width=1.2), "translate(326 118) rotate(10)")
    return svg(defs, body)


CARDS = {
    "Water": {
        "AbyssalCall": abyssal_call, "AbyssalCucumber": abyssal_cucumber, "CoralBuilder": coral_builder,
        "CoralShield": coral_shield, "CrashingWave": crashing_wave, "DancingDolphin": dancing_dolphin,
        "DeepSeaAngler": deep_sea_angler, "ElectricEel": electric_eel, "GuardianWhale": guardian_whale,
        "HealingRain": healing_rain, "JellyfishSwarm": jellyfish_swarm, "Kraken": kraken,
        "MermaidHealer": mermaid_healer, "OctopusDefender": octopus_defender, "SeahorseScout": seahorse_scout,
        "TidalSurge": tidal_surge, "TidepoolStar": tidepool_star, "TsunamiTurtle": tsunami_turtle,
        "Whirlpool": whirlpool,
    },
    "Corporate": {
        "UnpaidIntern": unpaid_intern, "InhumanResources": inhuman_resources, "Micromanager": micromanager,
        "MultilevelMarketingSeller": multilevel_marketing_seller, "DayTrader": day_trader,
        "PluriemployedHustler": pluriemployed_hustler, "Unicorn": unicorn,
        "MultimillionairePhilanthropist": multimillionaire_philanthropist, "FatTony": made_man, "CharlesPonzi": charles_ponzi,
        "MarginCall": margin_call, "HostileTakeover": hostile_takeover, "MassLayoffs": mass_layoffs,
        "TeamBuildingExercise": team_building_exercise, "DeadCatBounce": dead_cat_bounce,
        "TaxpayerFundsVentureCapitalProfits": taxpayer_funds_venture_capital_profits,
        "CleaningRobot": cleaning_robot, "Inflation": inflation, "TaxExemptCorruption": tax_exempt_corruption,
        "WorkShiftRotation": work_shift_rotation,
    },
    "Statecraft": {
        "KingEmeritus": king_emeritus, "SpiteElectedPresident": spite_elected_president,
        "PoliceOfficerProductivityTarget": police_officer_productivity_target,
        "InternalRevenueService": internal_revenue_service, "Mercenaries": mercenaries, "ManoWar": man_o_war,
        "MinistryforEquality": ministry_for_equality, "SupremeCourt": supreme_court, "ProtocolEnforcers": protocol_enforcers,
        "Demagogue": demagogue, "RedTape": red_tape, "PreemptiveStrike": preemptive_strike, "MartialLaw": martial_law,
        "Kakistocracy": kakistocracy, "MutuallyAssuredDestruction": mutually_assured_destruction,
        "PanemEtCircenses": panem_et_circenses,
        "Ceasefire": ceasefire, "Diplomat": diplomat, "LostLandmine": lost_landmine, "RaisedSecurityLevel": raised_security_level,
    },
    "Zeitgeist": {
        "Karen": karen, "Boomer": boomer, "Hipster": hipster, "Underdog": underdog, "Hikikomori": hikikomori,
        "RealityTVStar": reality_tv_star, "ScamArtist": scam_artist, "FootballHooligans": football_hooligans,
        "Antihero": antihero, "Bodybuilder": bodybuilder, "YOLO": yolo, "ThisIsFine": this_is_fine, "Strike": strike,
        "RussianRoulette": russian_roulette, "ToxicPositivity": toxic_positivity, "MentalHealthDay": mental_health_day,
        "DeliveryCyclist": delivery_cyclist, "PlotArmor": plot_armor, "ChainSmoker": chain_smoker,
        "Zeitgeist": zeitgeist_world, "RushHour": rush_hour,
    },
    "DigitalSociety": {
        "Catfish": catfish, "ConspiracyTheorist": conspiracy_theorist, "Clickbait": clickbait, "Cancelled": cancelled,
        "ChronicallyOnline": chronically_online, "DigitalNomad": digital_nomad, "Dogpile": dogpile, "ItsATrap": its_a_trap,
        "KeyboardWarrior": keyboard_warrior, "NPC": npc, "RageBait": rage_bait,
    },
    "Mind": {
        "SchrodingersCat": schrodingers_cat, "RokosBasilisk": rokos_basilisk, "TrolleyProblem": trolley_problem,
        "RedHerring": red_herring, "ImposterSyndrome": imposter_syndrome,
        "Introvert": introvert, "Pessimist": pessimist,
    },
    "Myth": {
        "OmniscientSpaceTimeBendingBeardedWizard": omniscient_space_time_bending_bearded_wizard, "Cultist": cultist, "Phoenix": phoenix_card,
        "ExcessofBreadandWine": excess_of_bread_and_wine, "MonkeyHand": monkey_hand, "SacrificialRitual": sacrificial_ritual,
        "Exorcism": exorcism, "Martyr": martyr, "MementoMori": memento_mori, "Mummy": mummy, "TheHolyGrail": the_holy_grail,
    },
    "ShadowOps": {
        "Whistleblower": whistleblower, "InvoluntaryFreePoloniumGiveaway": polonium_umbrella,
        "BurnNotice": burn_notice, "CounterIntelligence": counter_intelligence, "DeepCover": deep_cover, "Eavesdrop": eavesdrop,
        "Mole": mole, "PoliticalDecoy": political_decoy, "SignalJammer": signal_jammer, "SleeperAgent": sleeper_agent,
        "SmokeAndMirrors": smoke_and_mirrors, "TappedCommunications": tapped_communications,
    },
    "Tech": {
        "SysAdmin": sysadmin, "MeatproxyAsAService": meatproxy_as_a_service, "RansomwareAttack": ransomware_attack,
        "ManintheMiddle": man_in_the_middle,
        "Antivirus": antivirus, "DDOSAttack": ddos_attack, "EndtoEndEncryption": end_to_end_encryption, "LegacyCode": legacy_code,
        "ScriptKiddie": script_kiddie,
    },
    "Utopia": {
        "Clone": clone, "Hypermetamorphosis": hypermetamorphosis, "Extremophile": extremophile, "PhilosophyPhD": philosophy_phd,
        "Mathematician": mathematician, "QuantumPhysicist": quantum_physicist, "PerpetualMotionMachine": perpetual_motion_machine,
        "ParticleAccelerator": particle_accelerator, "GeneticallyModifiedOrganism": genetically_modified_organism, "SurveillanceGiraffe": giraffe,
        "Pioneer": pioneer, "Placebo": placebo, "Vaccine": vaccine, "ClimateChange": climate_change, "Overpopulation": overpopulation,
        "BlackHole": black_hole, "StasisField": stasis_field, "AbsoluteZero": absolute_zero, "SplittingTheAtom": splitting_the_atom,
        "GeneEditing": gene_editing,
    },
}
CARDS["Corporate"].update({"Counterfeit": counterfeit, "PrivateEquity": private_equity, "ShellCompany": shell_company})
CARDS["Statecraft"].update({"GoldenDome": golden_dome, "PresidentialPardon": presidential_pardon,
                            "CongruentUseofForce": congruent_use_of_force, "WeaponsofMassDistraction": weapons_of_mass_distraction})
CARDS["Zeitgeist"].update({"NotonMyWatch": not_on_my_watch, "OnefortheRoad": one_for_the_road})
CARDS["Zeitgeist"].update({"GenerationalTrauma": generational_trauma})  # Was Mind (2026-10-15)
CARDS["DigitalSociety"].update({"CryptoEntrepreneur": crypto_entrepreneur})
CARDS["ShadowOps"].update({"Cleaner": cleaner})
CARDS["Mind"].update({"MimeticTheory": mimetic_theory})
CARDS["DigitalSociety"].update({"MetaGroupChat": meta_group_chat, "SocialPanopticon": social_panopticon})
CARDS["ShadowOps"].update({"ControlRoom": control_room, "HumanitarianTraffickingAction": humanitarian_trafficking_action})
CARDS["Zeitgeist"].update({"NoGloveNoLove": no_glove_no_love, "WeAreNotTheSame": we_are_not_the_same})
CARDS["Corporate"].update({"BusinessExpense": business_expense})
CARDS["Corporate"].update({"SolidarityTowardsTheSmallBusinessman": solidarity_towards_the_small_businessman, "Inheritance": inheritance})
CARDS["Statecraft"].update({"ViolenceMonopoly": violence_monopoly})
CARDS["Myth"].update({"Reincarnation": reincarnation})
CARDS["Tech"].update({"TechnicalDebt": technical_debt, "ScopeCreep": scope_creep, "SexClanker": sex_clanker})
CARDS["Mind"].update({"ShipofTheseus": ship_of_theseus})
CARDS["Corporate"].update({"EscapeGoat": scapegoating})  # Was Mind (2026-10-15); its background is still Mind's haze


def mk_ultra_test_subject():
    # Ted Kaczynski as the young Harvard math prodigy, before the woods: clean-cut, short neat hair, white shirt and narrow
    # tie, strapped into a 1950s lab chair with electrodes taped to his temples, a blackboard of equations behind him and a
    # psychologist with a clipboard in the corner
    defs = lin("bg", ["#e8e0c8", "#bfb59a"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(0, 158, W, 42, "#8a7a62") + stroke("M0 158 L352 158", "#6a5a44", 2)
    # The blackboard, full of his equations
    body += rect(14, 12, 132, 76, "#2e4a3a", 'stroke="#7a5a32" stroke-width="5"')
    chalk = "#eeeee0"
    body += lettering("SUBJECT", 80, 20, 9, chalk, width=1.5)
    body += lettering("KACZYNSKI", 80, 36, 9, chalk, width=1.5)
    body += stroke("M24 54 L136 54 M28 64 q 6 -8 12 0 t 12 0 M58 60 L66 68 M66 60 L58 68 M74 64 L86 64", chalk, 1.4)
    body += lettering("1959", 112, 62, 9, chalk, width=1.4)
    # A memo pinned to the wall: redacted, and stamped CLASSIFIED
    memo = rect(0, 0, 82, 72, "#f6f2e6", 'stroke="#b8b0a0" stroke-width="1"') + circle(41, 5, 3, "#c0392b")
    for i, w in enumerate((60, 46, 64, 38, 54, 44)):
        memo += rect(9, 13 + i * 9, w, 5, "#1a1a1a")
    memo += group(rect(3, 24, 76, 20, "#f6f2e6", 'stroke="#c0392b" stroke-width="2.5" fill-opacity="0.6"')
                  + lettering("CLASSIFIED", 41, 30, 7, "#c0392b", width=1.6), "rotate(-12 41 34)")
    body += group(memo, "translate(258 8) rotate(3)")
    # The machine, its dials and lights
    body += rect(262, 92, 74, 66, "#5a6470", 'rx="4" stroke="#2a3038" stroke-width="2"')
    for x in (282, 316):
        body += circle(x, 112, 12, "#f4f0e0", 'stroke="#222" stroke-width="2"') + stroke(f"M{x} 112 L{x + 7} 104", "#c0392b", 2)
    for i, color in enumerate(("#3ad04a", "#f2c94c", "#d0342c")):
        body += circle(278 + i * 14, 140, 4, color)
    # The lab chair, high-backed, with straps on the arms
    body += rect(130, 64, 92, 136, "#6a4628", 'rx="10" stroke="#3a2614" stroke-width="3"')
    body += rect(116, 160, 120, 14, "#5a3a20", 'rx="4"')
    body += person(176, 200, 1.3, SKIN[0], "#f4f4f0", face="flat", hair="#2a2018", tie="#1e2635")
    hx, hy, r = 176, 200 - 64 * 1.3, 20 * 1.3
    body += rect(122, 162, 16, 6, "#2a1a10") + rect(214, 162, 16, 6, "#2a1a10")  # The straps
    # Short, neat, side-parted dark hair: a young man's, not yet the woods'
    body += path(f"M{hx - r - 1} {hy - 6} C {hx - r - 2} {hy - r - 12} {hx + r + 2} {hy - r - 14} {hx + r + 1} {hy - 8} "
                 f"C {hx + r - 6} {hy - r + 2} {hx - 8} {hy - r + 4} {hx - r - 1} {hy - 6} Z", "#2a2018")
    body += stroke(f"M{hx - 8} {hy - r - 5} q 8 2 22 8", "#4a3a2a", 1.5)
    # Electrodes taped to his temples, wired to the machine
    for side in (-1, 1):
        ex = hx + side * (r - 2)
        body += circle(ex, hy - 6, 5, "#9aa2aa", 'stroke="#444" stroke-width="1.5"')
        body += stroke(f"M{ex - 5} {hy - 11} L{ex + 5} {hy - 1} M{ex + 5} {hy - 11} L{ex - 5} {hy - 1}", "#ffffff", 2)
    body += stroke(f"M{hx + r} {hy - 6} C {hx + 70} {hy - 60} 250 60 266 100", "#222", 2)
    body += stroke(f"M{hx - r} {hy - 6} C {hx - 40} {hy - 70} {hx + 60} {hy - 90} 270 96", "#222", 2)
    # The psychologist, taking notes in the corner
    body += person(52, 200, 0.85, SKIN[1], "#ffffff", face="smirk", hair=OLD_HAIR, glasses="round", tie="#7a2a2a")
    body += rect(66, 150, 26, 32, "#b08a5a", 'rx="2"') + rect(70, 154, 18, 24, "#ffffff")
    for y in (158, 164, 170):
        body += stroke(f"M72 {y} L86 {y}", "#888", 1)
    return svg(defs, body)


CARDS["ShadowOps"].update({"MKUltraTestSubject": mk_ultra_test_subject})


# ---------------------------------------------------------------- Shadow Ops.: Black Budget and Double-O Section (2026-10-14)

def room(top, bottom, floor, floor_y=150):
    """A plain room: a wall in a gradient and a floor from floor_y down."""
    defs = lin("bg", [top, bottom])
    return defs, rect(0, 0, W, H, "url(#bg)") + rect(0, floor_y, W, H - floor_y, floor)


def stamp(text, cx, cy, size, color, angle=-12):
    """A rubber stamp's mark: the word in a box, at an angle."""
    w, h = len(text) * size * 0.95 + 10, size + 10
    return group(rect(cx - w / 2, cy - h / 2, w, h, "none", f'stroke="{color}" stroke-width="2.5"')
                 + lettering(text, cx, cy - size / 2, size, color, width=1.6), f"rotate({angle} {cx} {cy})")


def paper(x, y, w, h, lines=4, color="#f6f2e6", ink="#8a8a8a"):
    out = rect(x, y, w, h, color, 'stroke="#b8b0a0" stroke-width="1"')
    for i in range(lines):
        out += rect(x + 6, y + 8 + i * 7, max(4, w - 12 - (i % 2) * 10), 2.5, ink)
    return out


def agent(cx, base, s=1.0, skin=SKIN[0], face="flat", glasses="shades", hair="#1a1a1a", hat=None, suit="#22262e", tie="#111"):
    """A man in a dark suit, white shirt and narrow tie (sunglasses unless told otherwise)."""
    out = person(cx, base, s, skin, suit, face=face, hair=hair, hat=hat, glasses=glasses)
    out += poly([(cx - 8 * s, base - 41 * s), (cx + 8 * s, base - 41 * s), (cx, base - 22 * s)], "#f4f4f4")
    out += poly([(cx - 2.5 * s, base - 38 * s), (cx + 2.5 * s, base - 38 * s), (cx + 3.5 * s, base - 24 * s), (cx, base - 20 * s), (cx - 3.5 * s, base - 24 * s)], tie)
    return out


def pistol(x, y, s=1.0, color="#2a2a2e", silencer=False):
    """A pistol pointing right, its grip's top left corner at (x, y)."""
    out = rect(x, y - 6 * s, 30 * s, 8 * s, color, 'rx="2"') + poly([(x, y), (x + 9 * s, y), (x + 6 * s, y + 16 * s), (x - 3 * s, y + 16 * s)], color)
    if silencer:
        out += rect(x + 29 * s, y - 5 * s, 22 * s, 6 * s, "#4a4a50", 'rx="2"')
    return out


def dead_mans_switch():
    # A sweating man in a cheap hotel room, his thumb on a big red button wired to his wrist, a briefcase of documents and
    # dynamite at his feet, DO NOT DISTURB on the door
    defs, body = room("#7a6a44", "#4a3e28", "#5a3a2a")
    for x in range(0, W, 22):
        body += rect(x, 0, 8, 150, "#6a5a38", 'opacity="0.6"')
    body += rect(268, 30, 64, 124, "#8a5a32", 'stroke="#4a2e18" stroke-width="3"') + circle(322, 96, 3, "#d4af37")
    body += rect(276, 58, 48, 26, "#f4f0e0", 'stroke="#333" stroke-width="1"')
    body += lettering("DO NOT", 300, 62, 6, "#c0392b", width=1.2) + lettering("DISTURB", 300, 72, 6, "#c0392b", width=1.2)
    body += paper(26, 128, 40, 30, 3) + rect(24, 150, 100, 40, "#3a2618", 'rx="4"') + rect(24, 144, 100, 10, "#4a3020", 'rx="3"')
    for i in range(4):
        body += rect(68 + i * 13, 122, 10, 30, "#c0392b", 'rx="2"') + stroke(f"M{73 + i * 13} 122 l 2 -6", "#ddd", 1.2)
    body += rect(66, 132, 54, 4, "#222")
    body += person(176, 200, 1.3, SKIN[0], "#d8d0b0", face="frown", hair="#4a3a2a", brows="sad", tie="#7a2a2a")
    body += sweat(152, 98, 1.2) + sweat(202, 94, 1.0) + sweat(196, 112, 0.8)
    body += stroke("M206 172 L240 120", "#d8d0b0", 15) + circle(244, 112, 10, SKIN[0])
    body += rect(232, 116, 24, 6, "#555") + rect(230, 92, 28, 14, "#222", 'rx="3"') + ellipse(244, 92, 11, 6, "#e02a2a")
    body += ellipse(246, 88, 6, 4, SKIN[0])
    body += stroke("M232 118 C 200 170 150 110 118 132", "#d0342c", 2)
    return svg(defs, body)


def fell_out_of_a_window():
    # A grand window flung open on onion domes in the snow, curtains billowing, a tiny man in a suit falling past outside,
    # a man in a tracksuit calmly closing it
    defs, body = room("#d8c08a", "#a88a58", "#6a4a2a", 176)
    defs += lin("night", ["#1a2a5a", "#5a6a9a"])
    body += rect(150, 14, 150, 156, "url(#night)", 'stroke="#f4ecd8" stroke-width="8"')
    for x, h in ((166, 40), (206, 56), (250, 46), (284, 36)):
        body += rect(x - 9, 170 - h, 18, h, "#2a3050") + ellipse(x, 170 - h, 11, 12, "#3a5a8a") + stroke(f"M{x} {158 - h} l 0 -10", "#d4af37", 2)
    for i in range(26):
        body += circle(156 + (i * 37) % 140, 20 + (i * 53) % 140, 1.4, "#ffffff", 'opacity="0.8"')
    body += group(person(0, 0, 0.32, SKIN[0], "#22262e", face="shout", hair="#333", tie="#111"), "translate(250 70) rotate(160)")
    body += stroke("M236 40 l -4 -10 M262 46 l 6 -8", "#ffffff", 1.5, 'opacity="0.7"')
    body += path("M150 14 C 120 60 150 110 118 170 L150 170 Z", "#a01e2a") + path("M300 14 C 330 50 304 120 336 170 L300 170 Z", "#a01e2a")
    body += poly([(150, 18), (200, 30), (200, 160), (150, 166)], "#c8d8e8", 'opacity="0.45" stroke="#f4ecd8" stroke-width="4"')
    body += person(84, 200, 1.25, SKIN[0], "#1f3fa0", face="flat", hair="#2a2a2a")
    body += stroke("M50 200 L58 160 M118 200 L110 160", "#ffffff", 3)
    body += stroke("M112 168 L150 120", "#1f3fa0", 13) + circle(152, 116, 8, SKIN[0])
    return svg(defs, body)


def coup_detat():
    # A general in a peaked cap and mirrored sunglasses settling into the still-warm presidential chair, two soldiers swapping
    # the president's portrait for his own, tanks through the window
    defs, body = room("#e8dcc0", "#c0a878", "#7a2a2a")
    defs += lin("sky", ["#9ac8e8", "#e8e0c8"])
    body += rect(250, 20, 86, 90, "url(#sky)", 'stroke="#f4ecd8" stroke-width="6"') + rect(250, 86, 86, 24, "#8a9a6a")
    body += tank(276, 92, 0.45) + tank(316, 98, 0.35)
    body += group(rect(-24, -30, 48, 60, "#d4af37") + rect(-18, -24, 36, 48, "#9ab0c8") + person(0, 24, 0.5, SKIN[0], "#22262e", face="smile", hair=OLD_HAIR, tie="#7a2a2a"),
                  "translate(66 62) rotate(-18)")
    body += rect(126, 22, 52, 64, "#d4af37") + rect(132, 28, 40, 52, "#c8b88a")
    body += person(152, 80, 0.55, SKIN[1], "#4a5a3a", face="smirk", hat="peaked", glasses="shades")
    body += person(30, 200, 0.9, SKIN[1], "#4a5a3a", face="flat", hat="helmet") + stroke("M50 150 L60 70", "#4a5a3a", 9)
    body += person(220, 200, 0.9, SKIN[2], "#4a5a3a", face="flat", hat="helmet") + stroke("M200 150 L178 86", "#4a5a3a", 9)
    body += rect(132, 92, 88, 108, "#5a1e1e", 'rx="12" stroke="#d4af37" stroke-width="3"')
    body += person(176, 200, 1.15, SKIN[1], "#4a5a3a", face="smirk", hat="peaked", glasses="shades")
    for i in range(3):
        body += rect(154 + i * 8, 150, 6, 10, ["#c0392b", "#f2c94c", "#2a6aa0"][i])
    body += rect(100, 176, 152, 24, "#6a3a1a")
    return svg(defs, body)


def operation_paperclip():
    # A giant paperclip hooking a bespectacled scientist by the collar and lifting him from one flag onto another, rocket
    # blueprints fluttering out of his pockets
    defs = lin("bg", ["#c8dcec", "#f4ecd8"])
    body = rect(0, 0, W, H, "url(#bg)")
    # Two made-up flags: a grey one with a gear, a blue one with stars
    for x in (60, 292):
        body += stroke(f"M{x - 34} 200 L{x - 34} 110", "#6a5a4a", 4)
    body += rect(28, 112, 64, 42, "#5a5e66") + circle(60, 133, 11, "none", 'stroke="#d8d8d8" stroke-width="5" stroke-dasharray="4 3"')
    body += rect(260, 112, 64, 42, "#2a4a8a")
    for i in range(6):
        body += poly(star_points(270 + (i % 3) * 22, 124 + (i // 3) * 18, 5, 2), "#ffffff")
    body += stroke("M176 4 L176 34 M160 34 L160 92 C 160 104 192 104 192 92 L192 20 C 192 4 168 4 168 20 L168 84", "#b8c0c8", 6)
    body += group(person(0, 0, 0.9, SKIN[0], "#f4f4f4", face="frown", hair=OLD_HAIR, glasses="round", brows="sad", tie="#333"),
                  "translate(176 160) rotate(6)")
    body += stroke("M160 104 L168 120", "#b8c0c8", 6)
    body += stroke("M150 150 L120 176 M202 150 L232 176", "#f4f4f4", 9)
    for x, y, a in ((96, 70, -20), (250, 56, 18), (230, 120, -8)):
        bp = rect(-18, -12, 36, 24, "#2a5aa0", 'stroke="#ffffff" stroke-width="1"') + stroke("M-10 6 L0 -8 L10 6 Z M-4 6 l 0 4 M4 6 l 0 4", "#ffffff", 1.2)
        body += group(bp, f"translate({x} {y}) rotate({a})")
    return svg(defs, body)


def false_flag_operation():
    # A soldier in an enemy uniform two sizes too small, its flag sewn on upside down, setting fire to his own side's outpost
    # while a film crew records it and a general reads an outrage speech from a card
    defs = lin("bg", ["#f0c890", "#c8885a"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 160, W, 40, "#8a6a42")
    body += rect(196, 92, 92, 70, "#a88a5a", 'stroke="#5a4428" stroke-width="2"') + poly([(190, 94), (242, 60), (294, 94)], "#6a4a2a")
    body += stroke("M300 162 L300 70", "#5a4428", 3) + rect(300, 70, 34, 20, "#2a7a3a")
    body += flames(244, 96, 5, 18, 40)
    body += person(150, 200, 0.95, SKIN[0], "#8a2a2a", face="smirk", hair="#333", hat="helmet")
    body += rect(128, 152, 16, 10, "#f2c94c") + rect(128, 157, 16, 5, "#2a2a8a")
    body += stroke("M176 172 L210 130", "#8a2a2a", 10) + rect(206, 118, 6, 16, "#6a4a2a") + flames(209, 120, 1, 0, 16)
    body += rect(14, 104, 44, 30, "#222", 'rx="4"') + circle(28, 100, 9, "#333") + circle(46, 100, 9, "#333") + rect(56, 112, 14, 12, "#444")
    body += stroke("M22 134 L10 192 M36 134 L36 192 M50 134 L62 192", "#333", 3)
    body += person(96, 200, 0.75, SKIN[2], "#3a3a3a", face="flat", hat="cap")
    body += person(330, 200, 0.85, SKIN[0], "#3e4f6a", face="shout", hat="peaked", brows="angry")
    body += rect(296, 132, 22, 16, "#ffffff", 'stroke="#555" stroke-width="1"')
    return svg(defs, body)


def never_let_them_know_your_next_move():
    # A row of agents in identical grey suits passing their pistols, knives and grenades down the line to one man at the
    # end, who disappears under a mountain of weapons with only his sunglasses showing
    defs, body = room("#3a4250", "#1e232c", "#14181f", 176)
    for i in range(4):
        cx = 36 + i * 52
        body += agent(cx, 200, 0.75, SKIN[i % 3], face="flat", suit="#6a707a")
        body += stroke(f"M{cx + 18} 168 L{cx + 40} 150", "#6a707a", 7)
        body += pistol(cx + 34, 146, 0.45) if i % 2 == 0 else rect(cx + 34, 140, 14, 4, "#b8c0c8") + rect(cx + 30, 139, 6, 6, "#4a2a1a")
    pile = [(252, 190, 0), (300, 188, 30), (276, 170, -20), (316, 166, 60), (246, 160, 90), (292, 150, -40), (270, 136, 10), (306, 132, 120)]
    body += agent(286, 200, 1.0, SKIN[0], face="flat", suit="#6a707a")
    for x, y, a in pile:
        body += group(pistol(-15, 0, 0.8) if a % 20 == 0 else ellipse(0, 0, 9, 11, "#4a5a2a") + rect(-3, -14, 6, 5, "#888"), f"translate({x} {y}) rotate({a})")
    body += rect(270, 112, 32, 9, "#111", 'rx="3"')
    return svg(defs, body)


def frame_job():
    # A smiling agent pressing a sleeping man's thumb onto a smoking gun with tweezers, a police lineup board behind them
    # already labelled with the sleeper's name
    defs, body = room("#d8d4c8", "#a8a498", "#6a665c", 160)
    for i, h in enumerate(("7", "6", "5", "4")):
        y = 24 + i * 22
        body += stroke(f"M150 {y} L340 {y}", "#555", 1.5) + lettering(h, 140, y - 4, 8, "#555", width=1.2)
    body += rect(196, 40, 104, 22, "#222") + lettering("SUSPECT", 248, 46, 9, "#f4f4f4", width=1.5)
    body += rect(0, 150, 170, 50, "#7a5a3a") + rect(4, 128, 92, 28, "#f4f4f4", 'rx="6"')
    body += person(60, 170, 0.9, SKIN[0], "#5a7aa0", face="closed", hair="#6a4a2a") + zzz(84, 104, 0.7, "#555")
    body += stroke("M78 156 L110 150", "#5a7aa0", 8) + circle(112, 149, 6, SKIN[0])
    body += pistol(118, 152, 0.8) + stroke("M146 142 C 150 130 140 124 146 112 C 152 100 144 94 148 84", "#b8b8b8", 2, 'opacity="0.8"')
    body += agent(250, 200, 1.1, SKIN[1], face="grin", glasses=None, hair="#2a2a2a")
    body += stroke("M224 160 L150 146", "#22262e", 10) + stroke("M150 146 L116 146 M150 146 L118 152", "#b8c0c8", 2)
    return svg(defs, body)


def devils_breath():
    # A woman blowing a puff of glittering white powder off her palm into a businessman's face; dazed and smiling, he hands
    # her his watch, his wallet and the keys to an armored car
    defs = lin("bg", ["#2a1a3a", "#6a3a5a"]) + rad("puff", [("#ffffff", 0.85), ("#ffffff", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(7):
        body += circle(30 + i * 50, 30 + (i * 37) % 40, 3, "#f2c94c", 'opacity="0.6"')
    body += person(84, 200, 1.15, SKIN[1], "#8a1a3a", face="smirk", hat="bob", brows=None)
    body += stroke("M108 158 L140 120", "#8a1a3a", 11) + ellipse(146, 114, 12, 5, SKIN[1])
    body += ellipse(196, 100, 40, 26, "url(#puff)")
    for i in range(18):
        body += circle(160 + (i * 29) % 80, 82 + (i * 17) % 38, 1.6, "#ffffff")
    body += person(270, 200, 1.2, SKIN[0], "#2a3a5a", face="grin", hair="#555", tie="#a02a2a")
    body += stroke(f"M248 116 q 6 -6 12 0 M264 116 q 6 -6 12 0", "#222", 1.5)
    body += stroke("M244 164 L212 150", "#2a3a5a", 11)
    body += circle(204, 150, 7, "#d4af37", 'stroke="#8a6a1a" stroke-width="2"') + rect(198, 156, 20, 14, "#5a3a1a", 'rx="2"')
    body += circle(222, 170, 4, "none", 'stroke="#c8c8c8" stroke-width="2"') + rect(225, 168, 12, 3, "#c8c8c8") + rect(226, 172, 8, 3, "#c8c8c8")
    return svg(defs, body)


def he_didnt_kill_himself():
    # A sheet pulled over a body on a morgue gurney, but the toes sticking out wear flip-flops with a fresh tan line, and the
    # toe tag is a postcard from a tropical island
    defs, body = room("#c8d4d8", "#98a8b0", "#6a7880", 168)
    for x in range(0, W, 44):
        body += rect(x, 0, 42, 100, "none", 'stroke="#aab8c0" stroke-width="1.5"')
    body += rect(40, 112, 250, 10, "#a8b0b8") + stroke("M60 122 L60 180 M270 122 L270 180", "#8a929a", 4)
    body += circle(60, 184, 6, "#333") + circle(270, 184, 6, "#333")
    body += path("M40 112 C 60 70 120 76 160 88 C 200 98 240 88 270 92 L290 112 Z", "#f4f4f4", 'stroke="#c8c8c8" stroke-width="1.5"')
    for dx in (0, 16):
        body += ellipse(300 + dx, 98, 9, 16, SKIN[1]) + rect(290 + dx, 108, 22, 5, "#2ab0c0", 'rx="2"') + stroke(f"M{297 + dx} 92 l 5 8 l 5 -8", "#2ab0c0", 2.5)
        body += ellipse(300 + dx, 94, 7, 3, SKIN[0], 'opacity="0.7"')
    body += stroke("M312 108 L300 140", "#888", 1.2)
    card = rect(0, 0, 50, 34, "#ffffff", 'stroke="#888" stroke-width="1"') + rect(3, 3, 44, 20, "#7ad0f0") + rect(3, 18, 44, 5, "#f2d88a")
    card += stroke("M22 18 C 22 10 20 6 18 4 M22 10 q -8 -6 -12 -2 M22 10 q 6 -8 12 -4", "#2a7a3a", 2) + circle(38, 8, 4, "#ffe45c")
    body += group(card, "translate(280 140) rotate(-8)")
    return svg(defs, body)


def helicopter(cx, cy, s=1.0, color="#1a1d24"):
    """A helicopter facing left, its body centred on (cx, cy), the rotor a blur on top."""
    out = ellipse(cx, cy, 40 * s, 18 * s, color) + rect(cx + 30 * s, cy - 5 * s, 50 * s, 8 * s, color)
    out += rect(cx + 76 * s, cy - 16 * s, 6 * s, 20 * s, color) + ellipse(cx - 18 * s, cy - 4 * s, 16 * s, 10 * s, "#3a5a7a", 'opacity="0.8"')
    out += stroke(f"M{cx:.1f} {cy - 18 * s:.1f} L{cx:.1f} {cy - 26 * s:.1f}", color, 4 * s)
    out += ellipse(cx, cy - 27 * s, 70 * s, 3 * s, "#888", 'opacity="0.6"')
    out += stroke(f"M{cx - 24 * s:.1f} {cy + 22 * s:.1f} L{cx + 20 * s:.1f} {cy + 22 * s:.1f}", color, 3 * s)
    return out


def extraction():
    # A black helicopter at night hoisting an agent off a rooftop on a rope ladder, searchlights sweeping, his trench coat
    # flapping and a briefcase handcuffed to his wrist
    defs = lin("bg", ["#06080f", "#1d2740"]) + lin("beam", [("#fff7c8", 0.3), ("#fff7c8", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    body += poly([(40, 200), (70, 200), (230, 0), (170, 0)], "url(#beam)") + poly([(330, 200), (350, 200), (110, 0), (60, 0)], "url(#beam)")
    for i in range(14):
        body += circle((i * 71) % W, (i * 29) % 90, 1, "#ffffff", 'opacity="0.6"')
    body += rect(0, 168, 200, 32, "#05070d") + rect(220, 150, 132, 50, "#05070d")
    body += helicopter(190, 36, 1.1)
    body += stroke("M176 62 L170 150 M186 62 L180 150", "#c8a86a", 2)
    for y in range(72, 150, 10):
        body += stroke(f"M{170 + (y - 62) * -0.07:.1f} {y} L{180 + (y - 62) * -0.07:.1f} {y}", "#c8a86a", 2)
    body += path("M160 120 L148 168 L190 168 L186 118 Z", "#8a6a42")
    body += person(172, 134, 0.6, SKIN[0], "#8a6a42", face="smirk", hair="#2a2a2a", hat="fedora")
    body += stroke("M188 124 L202 140", "#8a6a42", 6) + stroke("M204 142 L210 150", "#c8c8c8", 1.5) + rect(200, 150, 22, 16, "#3a2a1a", 'rx="2"')
    return svg(defs, body)


def witness_protection_program():
    # A man in a terrible fake moustache and a Hawaiian shirt mowing a lawn in a quiet cul-de-sac, his old mugshot pinned to a
    # corkboard behind the window, a federal agent in a parked car pretending to read a newspaper
    defs = lin("bg", ["#9ad0f0", "#e8f4f8"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 130, W, 70, "#6ab04a")
    for x in range(0, W, 18):
        body += stroke(f"M{x} 160 l 4 -8 l 4 8", "#4a903a", 2)
    body += rect(20, 50, 130, 84, "#f4e8d0") + poly([(10, 54), (85, 14), (160, 54)], "#a0523a")
    body += rect(36, 74, 48, 36, "#bfe0f0", 'stroke="#f4f4f4" stroke-width="3"') + rect(42, 80, 36, 24, "#b08a5a")
    body += rect(48, 84, 14, 16, "#f4f4f4") + circle(55, 90, 4, SKIN[0]) + rect(64, 84, 10, 6, "#f6f2e6")
    body += rect(100, 84, 30, 50, "#6a4a2a")
    body += rect(250, 118, 96, 32, "#3a3a44", 'rx="8"') + rect(262, 102, 66, 24, "#3a3a44", 'rx="6"') + rect(268, 106, 54, 16, "#9ab8c8")
    body += circle(270, 152, 9, "#111") + circle(326, 152, 9, "#111") + rect(282, 104, 28, 14, "#f6f2e6") + circle(300, 110, 4, "#111", 'opacity="0.0"')
    body += person(196, 186, 0.95, SKIN[0], "#e8443a", face="flat", hair="#7a5a3a")
    for x, y in ((180, 160), (204, 170), (214, 150), (188, 178)):
        body += circle(x, y, 4, "#f2d84a") + circle(x + 1, y, 1.5, "#f4f4f4")
    body += path("M182 129 C 186 124 206 124 210 129 C 204 134 188 134 182 129 Z", "#3a2a1a")
    body += stroke("M220 168 L250 186", "#7a7a7a", 4) + rect(244, 178, 44, 18, "#c0392b", 'rx="3"') + circle(250, 198, 4, "#111") + circle(282, 198, 4, "#111")
    return svg(defs, body)


def death_by_natural_causes():
    # A coroner signing a death certificate marked NATURAL CAUSES with a flourish, while on the gurney beside him a tiny dart
    # with a red feather sticks out of the dead man's neck
    defs, body = room("#c8d4d8", "#98a8b0", "#6a7880", 168)
    body += rect(150, 120, 196, 10, "#a8b0b8") + stroke("M170 130 L170 184 M326 130 L326 184", "#8a929a", 4)
    body += path("M190 120 C 210 96 300 96 340 108 L340 120 Z", "#f4f4f4")
    body += circle(178, 108, 16, "#d8d0c8") + stroke("M170 104 l 6 0 M182 104 l 6 0", "#555", 1.5)
    body += stroke("M190 112 L204 98", "#6a4a2a", 1.5) + poly([(204, 98), (212, 88), (214, 96)], "#d0342c")
    body += person(76, 200, 1.2, SKIN[1], "#f4f4f4", face="smile", hair=OLD_HAIR, glasses="round")
    body += rect(94, 120, 60, 44, "#f6f2e6", 'stroke="#888" stroke-width="1"')
    body += lettering("NATURAL", 124, 126, 6, "#333", width=1.1) + lettering("CAUSES", 124, 136, 6, "#333", width=1.1)
    body += stroke("M100 156 q 10 -8 18 0 t 18 -2 t 12 -4", "#1a3a8a", 1.5)
    body += stroke("M104 168 L140 150", "#f4f4f4", 9) + stroke("M140 150 L150 138", "#222", 2.5)
    return svg(defs, body)


def asset_recruitment():
    # A handler in a dim bar sliding a thick envelope and a photograph across the table to a nervous soldier; the photo shows
    # the soldier with a woman who isn't his wife
    defs, body = room("#3a2418", "#1a0e08", "#2a1a10", 140)
    defs += rad("lamp", [("#ffd890", 0.6), ("#ffd890", 0)])
    body += circle(176, 30, 120, "url(#lamp)") + stroke("M176 0 L176 20", "#222", 2) + path("M160 30 L192 30 L184 20 L168 20 Z", "#6a4a2a")
    body += agent(70, 170, 1.0, SKIN[0], face="smirk", glasses=None, hat="fedora")
    body += person(286, 170, 1.0, SKIN[2], "#4a5a3a", face="frown", hat="helmet", brows="sad") + sweat(306, 100)
    body += rect(20, 150, 312, 50, "#5a3418")
    body += group(rect(-20, -12, 40, 24, "#d8c8a0", 'stroke="#8a7a5a" stroke-width="1"'), "translate(170 166) rotate(-6)")
    photo = rect(-18, -14, 36, 28, "#f4f4f4") + rect(-15, -11, 30, 19, "#8ab0c8")
    photo += circle(-6, -2, 4, SKIN[2]) + circle(6, -3, 4, SKIN[0]) + path("M3 -4 C 2 -12 12 -12 10 -2 Z", "#c0392b")
    body += group(photo, "translate(210 168) rotate(8)")
    body += stroke("M96 150 L150 164", "#22262e", 9)
    return svg(defs, body)


def if_i_told_you_id_have_to_kill_you():
    # A spy in a dark suit leaning in at a cocktail party to whisper into an eager journalist's ear, while under the table his
    # other hand quietly screws a silencer onto a pistol
    defs = lin("bg", ["#4a2a5a", "#1a1028"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(8):
        body += circle(20 + i * 46, 18 + (i % 3) * 10, 3, "#ffe9a0", 'opacity="0.7"')
    body += person(232, 170, 1.05, SKIN[0], "#7a8a9a", face="grin", hair="#a05a2a")
    body += rect(250, 130, 30, 20, "#f6f2e6") + rect(254, 134, 22, 2, "#888") + rect(254, 140, 18, 2, "#888")
    body += agent(150, 176, 1.05, SKIN[1], face="smirk", glasses=None)
    body += stroke("M176 98 q 10 -4 18 2 M178 92 q 14 -6 24 2", "#ffffff", 1.2, 'opacity="0.6"')
    # The tablecloth, and under it his hands screwing on the silencer
    body += rect(60, 150, 232, 22, "#e8e0d0") + rect(60, 150, 232, 5, "#c8c0b0") + rect(60, 172, 232, 28, "#140c1e")
    body += pistol(100, 186, 1.0, "#3a3a40") + rect(136, 177, 26, 8, "#a8a8b0", 'rx="2"')
    body += circle(104, 190, 6, SKIN[1]) + circle(150, 182, 6, SKIN[1])
    body += stroke("M150 182 q 6 -6 10 0", "#ffffff", 1, 'opacity="0.7"')
    return svg(defs, body)


def lone_gunman():
    # A bewildered young man in a cheap suit shoved in front of flashing press cameras, holding a rifle he has clearly never
    # seen before, a LONE GUNMAN placard round his neck, while two men in dark suits and hats slip away behind a picket fence
    # on a grassy knoll
    defs = lin("bg", ["#8ac0e0", "#d8ecf4"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += path("M190 120 C 240 90 300 92 352 104 L352 200 L190 200 Z", "#6aa04a")
    for x in range(232, 352, 12):
        body += rect(x, 92, 6, 26, "#f4f4f4") + poly([(x, 92), (x + 3, 86), (x + 6, 92)], "#f4f4f4")
    body += rect(228, 100, 124, 4, "#f4f4f4") + rect(228, 110, 124, 4, "#f4f4f4")
    body += group(agent(0, 0, 0.42, SKIN[0], hat="fedora") + agent(22, 0, 0.42, SKIN[0], hat="fedora"), "translate(300 90)")
    body += person(120, 200, 1.15, SKIN[0], "#7a6a5a", face="frown", hair="#3a2a1a", brows="sad", tie="#3a3a3a")
    body += stroke("M60 150 L176 112", "#5a3a1a", 6) + stroke("M150 121 L190 108", "#333", 3)
    body += stroke("M104 112 L92 150 M136 112 L148 150", "#888", 1.2)
    body += rect(76, 148, 88, 24, "#f6f2e6", 'stroke="#555" stroke-width="1"') + lettering("LONE GUNMAN", 120, 156, 7, "#c0392b", width=1.4)
    for x, y in ((20, 60), (44, 100), (210, 150), (16, 140)):
        body += rect(x, y, 24, 16, "#222", 'rx="3"') + circle(x + 12, y + 8, 5, "#555") + rect(x + 2, y - 5, 8, 5, "#333")
        body += poly(star_points(x + 6, y - 8, 10, 3, 8), "#fff7b0", 'opacity="0.85"')
    return svg(defs, body)


def glomar_response():
    # A press officer at a podium with a perfectly blank face, every line on his teleprompter blacked out with thick
    # redaction bars, reporters raising their hands in vain
    defs, body = room("#2a3a5a", "#14203a", "#10182a", 160)
    body += rect(126, 24, 100, 6, "#d4af37")
    body += agent(176, 150, 1.0, SKIN[0], face="flat", glasses=None, hair="#555")
    body += rect(146, 124, 60, 76, "#5a3a22", 'stroke="#3a2412" stroke-width="2"') + circle(176, 150, 12, "#d4af37")
    body += stroke("M160 124 L166 110 M190 124 L184 110", "#222", 2) + circle(166, 108, 3, "#222") + circle(184, 108, 3, "#222")
    body += rect(250, 30, 86, 70, "#111", 'stroke="#555" stroke-width="3"')
    for i in range(5):
        body += rect(258, 40 + i * 11, 70 - (i % 2) * 18, 7, "#000") + rect(258, 40 + i * 11, 70 - (i % 2) * 18, 7, "none", 'stroke="#333" stroke-width="1"')
    for i, x in enumerate((24, 70, 290, 330)):
        body += person(x, 200, 0.7, SKIN[i % 4], ["#8a6a42", "#3a5a8a", "#6a3a5a", "#4a6a4a"][i], face="shout", hair="#333")
        body += stroke(f"M{x + (10 if x < 176 else -10)} 172 L{x + (16 if x < 176 else -16)} 136", ["#8a6a42", "#3a5a8a", "#6a3a5a", "#4a6a4a"][i], 7)
    return svg(defs, body)


def sabotage():
    # A tank that has just fired, its barrel peeled back like a banana and the crew blackened and blinking, while a saboteur
    # in coveralls tiptoes away with an empty sack marked SUGAR - FOR THE FUEL TANK
    defs = lin("bg", ["#c8b890", "#8a7a5a"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 160, W, 40, "#6a5a3a")
    body += rect(60, 112, 140, 36, "#5a6a3a", 'rx="10"') + rect(96, 86, 72, 32, "#5a6a3a", 'rx="8"') + ellipse(130, 150, 76, 12, "#2a2a2a")
    for i in range(5):
        body += circle(76 + i * 28, 150, 9, "#3a3a3a")
    body += rect(166, 96, 30, 10, "#5a6a3a")
    for a in (-50, -10, 30, 70):
        body += group(path("M0 0 C 10 -6 22 -10 30 -4 C 24 0 12 2 0 4 Z", "#6a7a4a"), f"translate(196 101) rotate({a})")
    body += circle(212, 96, 16, "#555", 'opacity="0.6"') + circle(226, 84, 12, "#666", 'opacity="0.5"')
    for dx in (0, 26):
        body += circle(116 + dx, 80, 11, "#2a2a2a") + circle(112 + dx, 78, 3, "#fff") + circle(120 + dx, 78, 3, "#fff")
    body += person(290, 200, 0.85, SKIN[0], "#2a5a8a", face="smirk", hat="beanie")
    body += stroke("M306 156 L330 136", "#2a5a8a", 7) + path("M318 112 L344 112 L340 144 L322 144 Z", "#d8c8a0", 'stroke="#8a7a5a" stroke-width="1"')
    body += lettering("SUGAR", 331, 120, 5, "#5a3a1a", width=1)
    return svg(defs, body)


def ghost_army():
    # Two soldiers carrying a full-size rubber tank over their heads like a canoe across a muddy field at dawn, a third pumping
    # up another with a bicycle pump, while an enemy spotter plane circles above
    defs = lin("bg", ["#f0b080", "#a8c0d8"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 150, W, 50, "#6a5a3a")
    body += group(tank(0, 0, 1.4, "#6a7a4a"), "translate(120 90)")
    for x in (92, 148):
        body += person(x, 200, 0.75, SKIN[0], "#5a6a3a", face="smile", hat="helmet") + stroke(f"M{x - 14} 160 L{x - 18} 108 M{x + 14} 160 L{x + 18} 108", "#5a6a3a", 6)
    body += group(tank(0, 0, 0.9, "#7a8a5a"), "translate(268 150) scale(1 0.6)")
    body += person(330, 200, 0.65, SKIN[1], "#5a6a3a", face="grin", hat="helmet") + stroke("M300 190 L300 158 M290 158 L310 158", "#555", 3)
    body += stroke("M300 186 C 290 186 286 170 268 162", "#222", 2)
    body += group(path("M-14 0 L14 0 L18 -3 L14 -6 L-12 -4 Z M-4 -4 L2 -14 L6 -4 Z M-4 0 L2 10 L6 0 Z", "#3a3a3a"), "translate(270 30)")
    body += circle(270, 30, 40, "none", 'stroke="#ffffff" stroke-width="1" stroke-dasharray="3 5" opacity="0.6"')
    return svg(defs, body)


def inflatable_tank():
    # A rubber tank half-deflated on a field, its barrel drooping, a soldier patching a hole with a bicycle repair kit
    defs = lin("bg", ["#a8c8e0", "#e8ecd8"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 140, W, 60, "#7a9a4a")
    body += path("M60 168 C 60 140 80 134 120 132 C 170 130 230 132 250 150 C 262 162 258 172 240 172 L70 174 Z", "#7a8a5a")
    body += path("M110 134 C 112 112 130 104 160 106 C 186 108 196 118 194 134 Z", "#7a8a5a")
    body += stroke("M190 120 C 220 122 240 136 252 162", "#7a8a5a", 9) + ellipse(254, 166, 6, 4, "#5a6a3a")
    body += stroke("M80 160 q 20 -10 40 0 M150 150 q 20 -8 40 2 M130 118 q 14 -6 28 0", "#5a6a3a", 2)
    body += circle(214, 150, 4, "#222") + rect(206, 142, 16, 10, "#e8d84a", 'rx="2" opacity="0.9"')
    body += stroke("M214 142 l -10 -14 M218 142 l 6 -12", "#ffffff", 1.2, 'opacity="0.8"')
    body += person(290, 200, 0.9, SKIN[0], "#5a6a3a", face="frown", hat="helmet", brows="sad")
    body += stroke("M268 168 L230 152", "#5a6a3a", 8) + rect(250, 178, 26, 16, "#c0392b", 'rx="2"')
    return svg(defs, body)


def sherman_tank():
    # A heavy tank rolling out of the morning fog, cannon level, mud spraying from its tracks
    defs = lin("bg", ["#c8ccd0", "#8a9098"]) + lin("fog", [("#e8ecf0", 0.9), ("#e8ecf0", 0)], 0, 0, 1, 0)
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 150, W, 50, "#5a4a32")
    body += rect(70, 104, 170, 42, "#4a5a32", 'rx="10"') + rect(110, 72, 90, 40, "#4a5a32", 'rx="12"')
    body += rect(196, 84, 120, 10, "#4a5a32") + rect(312, 82, 12, 14, "#3a4a28")
    body += ellipse(155, 150, 96, 16, "#2a2a2a")
    for i in range(7):
        body += circle(80 + i * 25, 150, 11, "#3a3a3a", 'stroke="#555" stroke-width="2"')
    body += poly(star_points(150, 124, 10, 4), "#f4f4f4")
    for i in range(9):
        body += circle(40 + (i * 13) % 40, 150 - (i * 11) % 34, 3 + i % 3, "#4a3a22")
    body += rect(0, 0, 210, H, "url(#fog)")
    return svg(defs, body)


def social_engineering():
    # A smiling man in a hi-vis vest with a clipboard and a fake lanyard holding a door open for a whole office of employees,
    # each one handing him their badge and password on a sticky note as they pass
    defs, body = room("#d8dce0", "#b0b8c0", "#8a9098", 164)
    body += rect(20, 30, 90, 134, "#8ab0c8", 'stroke="#555" stroke-width="3"') + rect(30, 40, 70, 114, "#bcd8e8")
    body += person(140, 200, 1.15, SKIN[1], "#f2d22a", face="grin", hair="#2a2a2a")
    body += stroke("M116 148 L124 168 M164 148 L156 168", "#b0b0b0", 4) + rect(120, 166, 40, 4, "#c0c0c0")
    body += badge(140, 168, 0.8)
    body += stroke("M118 170 L76 140", "#f2d22a", 10) + rect(70, 126, 10, 24, "#888")
    body += rect(160, 150, 24, 32, "#b08a5a") + rect(163, 154, 18, 24, "#ffffff")
    for i in range(3):
        x = 220 + i * 46
        body += person(x, 200, 0.75, SKIN[(i + 2) % 4], ["#3d5a80", "#6a3a5a", "#2f6b5a"][i], face="smile", tie="#555")
        body += rect(x - 30, 150, 14, 14, "#f2e84a") + badge(x - 14, 170, 0.5)
    return svg(defs, body)


def bilderberg_membership():
    # A candlelit dining hall in an alpine hotel, a long table of old men in dinner jackets trading sealed envelopes across the
    # silverware, a doorman outside the window turning away the press
    defs, body = room("#5a2a1a", "#2a1208", "#3a1a0a", 150)
    defs += rad("candle", [("#ffd890", 0.55), ("#ffd890", 0)])
    body += rect(240, 14, 96, 70, "#9ac0e0", 'stroke="#d4af37" stroke-width="4"') + poly([(240, 84), (276, 40), (300, 70), (318, 50), (336, 84)], "#f4f4f4")
    body += person(270, 84, 0.4, SKIN[1], "#1a1a1a", face="flat", hat="peaked") + person(318, 84, 0.35, SKIN[0], "#8a6a42", face="shout")
    for i in range(4):
        x = 40 + i * 62
        body += person(x, 168, 0.75, SKIN[i % 2], "#111", face="smirk", hair=OLD_HAIR, tie="#111")
        body += poly([(x - 6, 128), (x + 6, 128), (x, 136)], "#f4f4f4")
    body += rect(0, 150, W, 50, "#f4ecd8") + rect(0, 150, W, 6, "#e0d8c0")
    for x in (70, 170, 260):
        body += circle(x, 120, 30, "url(#candle)") + rect(x - 3, 128, 6, 22, "#f4ecd8") + ellipse(x, 124, 3, 6, "#ffd84a")
    for x, a in ((110, -10), (200, 8)):
        body += group(envelope(0, 0, 1.2, "#f4ecd8") + circle(0, 1, 3, "#a01e2a"), f"translate({x} 162) rotate({a})")
    for x in (40, 150, 230, 310):
        body += stroke(f"M{x - 12} 176 l 0 14 M{x + 12} 176 l 0 14", "#c8c8c8", 2) + ellipse(x, 182, 9, 4, "#ffffff")
    return svg(defs, body)


def dead_drop():
    # A park bench in autumn, a man in a trench coat feeding pigeons while sliding a hollowed-out brick behind him with his
    # heel, another man on the next bench reading an upside-down newspaper
    defs = lin("bg", ["#f0c890", "#d8a060"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 150, W, 50, "#8a7a5a")
    for x, y in ((40, 40), (110, 20), (300, 34)):
        body += circle(x, y, 26, "#c0562a", 'opacity="0.8"') + stroke(f"M{x} {y + 20} L{x} 150", "#5a3a1a", 5)
    for i in range(12):
        body += ellipse((i * 31) % W, 156 + (i * 13) % 40, 4, 2, ["#c0562a", "#d8a030", "#a03a1a"][i % 3])
    for x0 in (30, 200):
        body += rect(x0, 128, 120, 8, "#7a4a2a") + rect(x0, 112, 120, 6, "#7a4a2a") + stroke(f"M{x0 + 10} 136 L{x0 + 10} 156 M{x0 + 110} 136 L{x0 + 110} 156", "#333", 3)
    body += person(90, 132, 0.8, SKIN[0], "#a08a5a", face="flat", hat="fedora")
    body += stroke("M108 112 L128 124", "#a08a5a", 6) + circle(130, 126, 2, "#d8c890") + circle(134, 130, 2, "#d8c890")
    for x in (40, 150, 168):
        body += ellipse(x, 160, 7, 5, "#8a8a9a") + circle(x - 6, 156, 3, "#8a8a9a")
    body += rect(70, 152, 22, 10, "#a8442a", 'stroke="#6a2a1a" stroke-width="1"') + stroke("M92 156 L104 140", "#3a3a3a", 4)
    body += person(258, 132, 0.8, SKIN[1], "#3a3a44", face="flat", hat="fedora")
    body += group(rect(-26, -16, 52, 34, "#f4f4f0", 'stroke="#888" stroke-width="1"') + lettering("SEMIT", 0, -12, 6, "#222", width=1.1)
                  + rect(-20, 0, 18, 2, "#888") + rect(4, 0, 18, 2, "#888") + rect(-20, 6, 40, 2, "#888"), "translate(258 104) rotate(180)")
    return svg(defs, body)


def multipass():
    # The Multipass shown to the camera, as in the film: on the left the cab driver with buzzed fair hair, stubble, an orange
    # shirt under a dark leather vest; on the right the young woman with the bright orange bob and fringe, in a white top
    # with orange suspenders, holding the Multipass up beside her face; a dim, crowded hall behind them
    defs = lin("bg", ["#3a2e3e", "#1e1a24"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(60):  # The crowd behind, out of focus
        x, y = (i * 47) % W, 20 + (i * 29) % 110
        body += circle(x, y, 9 + (i % 3) * 3, ["#5a4a5e", "#4a3e52", "#6a5862", "#3e3446"][i % 4], 'opacity="0.6"')
    # Him: the vest over the orange shirt, the buzz cut and the stubble
    hx, hy, r = 96, 200 - 64 * 1.3, 20 * 1.3
    body += person(hx, 200, 1.3, SKIN[0], "#2e2a28", face="smirk")
    body += path(f"M{hx - 10} {hy + r + 10} L{hx} {hy + r + 24} L{hx + 10} {hy + r + 10} Z", "#ff7a1a")  # The orange collar
    body += path(f"M{hx - r + 2:.1f} {hy - 6:.1f} C {hx - r + 4:.1f} {hy - r - 4:.1f} {hx + r - 4:.1f} {hy - r - 4:.1f} {hx + r - 2:.1f} {hy - 6:.1f} C {hx + 10:.1f} {hy - r + 2:.1f} {hx - 10:.1f} {hy - r + 2:.1f} {hx - r + 2:.1f} {hy - 6:.1f} Z", "#d8c08a")
    body += path(f"M{hx - r + 4:.1f} {hy + 6:.1f} C {hx - r + 6:.1f} {hy + r + 2:.1f} {hx + r - 6:.1f} {hy + r + 2:.1f} {hx + r - 4:.1f} {hy + 6:.1f} C {hx + 8:.1f} {hy + r - 6:.1f} {hx - 8:.1f} {hy + r - 6:.1f} {hx - r + 4:.1f} {hy + 6:.1f} Z", "#b89a7a", 'opacity="0.55"')
    # Her: the bob behind, then her, then the straight fringe
    gx, gy, gr = 262, 200 - 64 * 1.25, 20 * 1.25
    hair = "#ff5a14"
    body += path(f"M{gx - gr - 7:.1f} {gy + 20:.1f} C {gx - gr - 9:.1f} {gy - gr * 1.75:.1f} {gx + gr + 9:.1f} {gy - gr * 1.75:.1f} {gx + gr + 7:.1f} {gy + 20:.1f} Z", hair)
    body += person(gx, 200, 1.25, SKIN[0], "#f2eee6", face="flat")
    body += rect(gx - gr, gy - gr + 1, gr * 2, 11, hair, 'rx="2"')
    body += stroke(f"M{gx - 16} 158 L{gx - 20} 200", "#ff8a1a", 5) + stroke(f"M{gx + 16} 158 L{gx + 20} 200", "#ff8a1a", 5)
    # The Multipass, held up beside her face
    body += stroke(f"M{gx - 30} 196 L {gx - 52} 128", SKIN[0], 9) + circle(gx - 52, 124, 7, SKIN[0])
    card = rect(-26, -17, 52, 34, "#e8e8f0", 'rx="4" stroke="#8a8a9a" stroke-width="1.5"')
    card += rect(-24, -15, 48, 8, "#3a5ad8") + lettering("MULTI PASS", 0, -14, 4.2, "#ffffff", width=0.9)
    card += rect(-21, -4, 14, 17, "#8ab0e0") + circle(-14, 3, 4, SKIN[0]) + rect(-18, -2, 8, 3, hair)
    card += rect(-4, -3, 24, 3, "#ffb020") + rect(-4, 3, 20, 2, "#6a6a7a") + rect(-4, 8, 16, 2, "#6a6a7a")
    body += group(card, f"translate({gx - 56} 110) rotate(-4)")
    return svg(defs, body)


def casino():
    # A caricature of the tycoon-president in a dark suit and an overlong red tie, his blond hair swept over, grinning and
    # throwing a fistful of chips in the air in front of his gaudy gold-and-purple casino with onion domes and a giant neon
    # sign, a red BANKRUPT stamp across the sign
    defs = lin("bg", ["#1a1240", "#4a1a5a"])
    body = rect(0, 0, W, H, "url(#bg)")
    # The casino: a palace of gold with purple onion domes, lit windows, the neon sign on top
    body += rect(150, 70, 190, 130, "#c9a227") + rect(150, 70, 190, 8, "#f4d35e")
    for x in (170, 245, 320):
        body += path(f"M{x - 18} 70 C {x - 18} 44 {x} 30 {x} 22 C {x} 30 {x + 18} 44 {x + 18} 70 Z", "#7a2a9a") + circle(x, 20, 3, "#f4d35e")
    for row in range(3):
        for col in range(7):
            body += rect(160 + col * 26, 92 + row * 30, 14, 18, "#ffe08a" if (row + col) % 3 else "#ff9ad5", 'rx="7"')
    body += rect(178, 38, 134, 26, "#2a0a3a", 'rx="5" stroke="#ff3ad0" stroke-width="2.5"')
    body += lettering("CASINO", 245, 43, 15, "#ffe14a", width=2.2)
    body += stamp("BANKRUPT", 250, 128, 12, "#e8241c", -10)
    # Him: dark suit, white collar, the long red tie, the orange tan and the swept-over hair
    cx, base, sc = 92, 200, 1.35
    hx, hy, r = cx, base - 64 * sc, 20 * sc
    body += person(cx, base, sc, "#f0a060", "#1e2440", face="grin")
    body += poly([(cx - 8, base - 46), (cx + 8, base - 46), (cx, base - 36)], "#ffffff")
    body += poly([(cx - 5, base - 40), (cx + 5, base - 40), (cx + 8, base), (cx - 8, base)], "#d2202a")  # Down past the belt
    body += path(f"M{hx - r - 3:.1f} {hy - 2:.1f} C {hx - r:.1f} {hy - r * 1.6:.1f} {hx + r * 1.3:.1f} {hy - r * 1.7:.1f} {hx + r + 8:.1f} {hy - r * 0.6:.1f} C {hx + r * 0.4:.1f} {hy - r * 0.9:.1f} {hx - r * 0.2:.1f} {hy - r * 0.5:.1f} {hx - r - 3:.1f} {hy - 2:.1f} Z", "#f5d061")
    body += ellipse(hx - 9, hy - 2, 5, 2.5, "#ffd8b0") + ellipse(hx + 9, hy - 2, 5, 2.5, "#ffd8b0")  # Pale goggle tan
    body += circle(hx - 9, hy - 2, 1.8, "#222") + circle(hx + 9, hy - 2, 1.8, "#222")
    # A fistful of chips thrown up in the air
    body += stroke(f"M{cx + 30} {base - 30} L {cx + 58} {base - 80}", "#1e2440", 10) + circle(cx + 60, base - 84, 7, "#f0a060")
    for i, (x, y) in enumerate([(150, 60), (128, 34), (170, 92), (110, 18), (186, 28), (140, 82)]):
        color = ["#d2202a", "#2a5ad8", "#1a9a4a", "#111111"][i % 4]
        body += circle(x, y, 7, color, 'stroke="#ffffff" stroke-width="1.5" stroke-dasharray="3 2"') + circle(x, y, 3, "#ffffff")
    return svg(defs, body)


def wet_work():
    # A hitman in a dripping raincoat under a black umbrella in the rain, pressing a red TERMINATED stamp onto a personnel file
    # with a photo clipped to it
    defs = lin("bg", ["#2a3440", "#141a22"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(40):
        x, y = (i * 53) % W, (i * 37) % H
        body += stroke(f"M{x} {y} l -3 10", "#9ab0c8", 1, 'opacity="0.5"')
    body += path("M70 70 C 80 20 220 20 230 70 Z", "#111") + stroke("M150 30 L150 140", "#333", 3)
    body += person(150, 200, 1.15, SKIN[0], "#4a5040", face="flat", hat="fedora")
    body += rect(210, 150, 120, 50, "#4a3020")
    body += paper(226, 136, 80, 56, 5, "#e8dcc0") + rect(282, 140, 18, 22, "#f4f4f4") + circle(291, 149, 5, SKIN[1])
    body += stamp("TERMINATED", 262, 168, 8, "#d0342c", -14)
    body += stroke("M176 170 L228 150", "#4a5040", 10) + rect(222, 128, 14, 18, "#6a4a2a") + rect(218, 144, 22, 6, "#333")
    for x in (80, 120, 210):
        body += sweat(x, 76, 0.8)
    return svg(defs, body)


def perfectly_timed_camera_malfunction():
    # A security monitor showing static and SIGNAL LOST at 02:13, while at the edge of the frame a gloved hand pulls the
    # camera's cable out of the wall
    defs, body = room("#2a2e34", "#16181c", "#101214", 176)
    body += rect(60, 24, 200, 140, "#3a3e44", 'rx="10" stroke="#111" stroke-width="3"') + rect(74, 36, 172, 112, "#888")
    for i in range(70):
        x, y = 74 + (i * 37) % 168, 36 + (i * 23) % 108
        body += rect(x, y, 4 + i % 5, 2, ["#ffffff", "#444", "#bbb"][i % 3])
    body += rect(100, 74, 120, 34, "#000", 'opacity="0.75"') + lettering("SIGNAL LOST", 160, 80, 7, "#ffffff", width=1.4)
    body += lettering("02", 140, 94, 6, "#ffffff", width=1.2) + circle(160, 96, 1, "#fff") + circle(160, 100, 1, "#fff") + lettering("13", 180, 94, 6, "#ffffff", width=1.2)
    body += circle(232, 44, 3, "#d0342c")
    body += rect(300, 60, 30, 40, "#c8c8c0", 'rx="3"') + rect(308, 70, 6, 10, "#222") + rect(318, 70, 6, 10, "#222")
    body += stroke("M260 150 C 280 170 300 140 312 110", "#111", 4)
    body += path("M290 90 C 300 84 318 86 322 96 L330 120 L300 126 Z", "#1a1a1a") + stroke("M300 126 L352 150", "#1a1a1a", 18)
    return svg(defs, body)


def the_golden_gun():
    # A gold pistol pieced together from a cigarette case, a lighter, a cufflink and a fountain pen, lying on black velvet
    # beside a single golden bullet engraved with a name
    defs = rad("bg", [("#3a2a3a", 1), ("#0a060a", 1)], 0.5, 0.5, 0.7) + lin("gold", ["#fff0a0", "#d4af37", "#8a6a1a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(6):
        body += stroke(f"M{i * 70} 200 C {i * 70 + 30} 140 {i * 70 + 10} 80 {i * 70 + 50} 0", "#2a1a2a", 18, 'opacity="0.5"')
    gun = rect(0, 0, 76, 26, "url(#gold)", 'rx="5" stroke="#6a4a10" stroke-width="2"')  # The cigarette case
    gun += rect(74, 6, 92, 12, "url(#gold)", 'rx="5" stroke="#6a4a10" stroke-width="2"') + circle(166, 12, 4, "#3a2a10")  # The pen
    gun += rect(14, 24, 30, 54, "url(#gold)", 'rx="6" stroke="#6a4a10" stroke-width="2"')  # The lighter, the grip
    gun += circle(58, 34, 10, "url(#gold)", 'stroke="#6a4a10" stroke-width="2"') + circle(58, 34, 4, "#3a2a10")  # The cufflink
    gun += stroke("M50 26 C 50 44 64 46 66 30", "#6a4a10", 2)
    body += group(gun, "translate(70 60) rotate(-6)")
    body += group(rect(0, 0, 44, 14, "url(#gold)", 'rx="3"') + path("M44 0 C 58 2 58 12 44 14 Z", "url(#gold)") + stroke("M8 7 l 24 0", "#6a4a10", 1.2),
                  "translate(250 150) rotate(-14)")
    body += poly(star_points(120, 70, 8, 2), "#ffffff", 'opacity="0.9"') + poly(star_points(290, 146, 6, 1.5), "#ffffff", 'opacity="0.9"')
    return svg(defs, body)


def die_another_day():
    # A spy in a torn tuxedo surfing a towering wave on a car door past a collapsing ice palace, checking his wristwatch
    defs = lin("bg", ["#9ad0f0", "#e8f4fa"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x, h in ((240, 90), (270, 120), (300, 100), (330, 70)):
        body += poly([(x - 18, 150), (x, 150 - h), (x + 18, 150)], "#d8f0ff", 'stroke="#9ac8e0" stroke-width="2"')
    for x, y in ((262, 60), (298, 40), (316, 80)):
        body += poly([(x, y), (x + 8, y + 4), (x + 4, y + 12)], "#bfe4f8")
    body += path("M0 200 L0 110 C 40 60 120 30 180 60 C 140 60 110 80 120 110 C 160 100 200 120 230 150 L352 160 L352 200 Z", "#1a6aa0")
    body += path("M120 110 C 110 80 140 60 180 60 C 160 70 150 90 160 104 Z", "#e8f8ff")
    body += group(rect(-36, -6, 72, 12, "#7a8a9a", 'rx="4"') + rect(-30, -4, 20, 8, "#9ac8e0") + circle(24, 0, 2, "#333"), "translate(170 150) rotate(-10)")
    body += person(170, 140, 0.75, SKIN[0], "#111", face="flat", hair="#2a2a2a", tie="#111")
    body += poly([(156, 110), (164, 116), (158, 124)], "#f4f4f4") + stroke("M146 118 l 8 6 M188 114 l -6 8", "#f4f4f4", 2)
    body += stroke("M190 112 L204 100", "#111", 7) + circle(206, 98, 5, SKIN[0]) + circle(200, 104, 3.5, "#d4af37")
    body += stroke("M150 114 L132 126", "#111", 7)
    for i in range(8):
        body += circle(40 + i * 30, 140 + (i * 7) % 14, 3, "#ffffff", 'opacity="0.8"')
    return svg(defs, body)


def licence_to_kill():
    # A civil servant in sleeve garters at a cluttered desk stamping a laminated LICENCE TO KILL card with a smirking agent's
    # passport photo, a tray of forms marked 00 beside him
    defs, body = room("#c8c0a0", "#9a9070", "#6a5a3a", 150)
    body += rect(240, 20, 90, 60, "#f4f0e0", 'stroke="#555" stroke-width="2"') + lettering("00", 285, 36, 18, "#1a1a1a", width=3)
    body += person(100, 160, 1.0, SKIN[0], "#f4f4f4", face="flat", hair=OLD_HAIR, glasses="round", tie="#4a5a6a")
    body += rect(72, 132, 10, 6, "#c0392b") + rect(118, 132, 10, 6, "#c0392b")
    body += rect(0, 150, W, 50, "#7a5a3a")
    card = rect(0, 0, 96, 56, "#e8f0f8", 'rx="6" stroke="#9ab0c8" stroke-width="2"') + rect(6, 8, 26, 32, "#ffffff", 'stroke="#888" stroke-width="1"')
    card += circle(19, 20, 7, SKIN[0]) + stroke("M14 23 q 6 3 10 -2", "#222", 1.2) + rect(12, 12, 14, 3, "#2a2a2a")
    card += lettering("LICENCE", 64, 10, 6, "#1a3a6a", width=1.2) + lettering("TO KILL", 64, 22, 6, "#1a3a6a", width=1.2) + rect(40, 36, 48, 3, "#888")
    body += group(card, "translate(150 150) rotate(-4)")
    body += stroke("M124 140 L176 112", "#f4f4f4", 9) + rect(172, 92, 14, 18, "#6a4a2a") + rect(166, 108, 26, 8, "#333")
    for i in range(4):
        body += rect(262, 162 - i * 6, 64, 6, "#f4f0e0", 'stroke="#aaa" stroke-width="1"')
    body += rect(258, 168, 72, 14, "#5a5a6a")
    return svg(defs, body)


def shaken_not_stirred():
    # A bartender shaking a cocktail shaker while explosions rock the casino behind him, an agent in a tuxedo calmly waiting at
    # the bar for his martini, not a hair out of place
    defs = lin("bg", ["#5a1a1a", "#1a0a0a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for cx, cy, r in ((80, 60, 40), (270, 50, 34), (180, 30, 24)):
        body += circle(cx, cy, r, "#ff8a2a", 'opacity="0.8"') + circle(cx, cy, r * 0.6, "#ffd84a") + poly(star_points(cx, cy, r * 1.3, r * 0.7, 9), "#ff5a1a", 'opacity="0.5"')
    body += person(100, 160, 0.95, SKIN[1], "#f4f4f4", face="shout", hair="#333", brows="sad")
    body += poly([(88, 124), (100, 130), (88, 136)], "#111") + poly([(112, 124), (100, 130), (112, 136)], "#111")  # His bow tie
    for dx in (-30, 30):
        body += stroke(f"M{100 + dx} 140 L{100 + dx * 1.1} 96", "#f4f4f4", 8)
    body += rect(92, 70, 16, 30, "#c8c8d0", 'rx="4"') + stroke("M84 76 l -8 -4 M116 76 l 8 -4 M84 90 l -8 2 M116 90 l 8 2", "#ffffff", 1.5)
    body += rect(0, 150, W, 50, "#3a1a0a") + rect(0, 146, W, 8, "#6a3a1a")
    body += agent(260, 200, 1.1, SKIN[0], face="smirk", glasses=None, tie="#111")
    body += poly([(250, 152), (262, 152), (256, 160)], "#111")
    body += path("M196 120 L220 120 L208 134 Z", "#e8f4fa", 'opacity="0.8"') + stroke("M208 134 L208 148 M200 148 L216 148", "#c8d8e0", 2) + circle(212, 124, 3, "#6a9a3a")
    return svg(defs, body)


def golden_eye():
    # A satellite dish shaped like a golden eye in orbit firing a ripple of light at a city at night; below, every window goes
    # dark at once and a man's toaster pops out smoking
    defs = lin("bg", ["#02040c", "#141a3a"]) + lin("gold", ["#fff0a0", "#d4af37", "#8a6a1a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(20):
        body += circle((i * 83) % W, (i * 37) % 80, 1, "#ffffff", 'opacity="0.7"')
    body += ellipse(80, 50, 46, 26, "url(#gold)", 'stroke="#6a4a10" stroke-width="2"') + circle(80, 50, 16, "#1a3a6a") + circle(80, 50, 7, "#000")
    body += circle(76, 46, 3, "#ffffff") + rect(118, 44, 30, 12, "#888") + rect(148, 36, 36, 28, "#2a4a8a", 'stroke="#aaa" stroke-width="1"')
    for r in (40, 70, 100, 130):
        body += path(f"M{80 + r * 0.6:.0f} {50 + r * 0.6:.0f} A {r} {r} 0 0 0 {80 + r:.0f} {50 + r * 0.2:.0f}", "none", 'stroke="#9ad8ff" stroke-width="3" opacity="0.6"')
    for x, w, h in ((150, 40, 70), (194, 30, 96), (228, 44, 60), (276, 36, 84), (316, 36, 50)):
        body += rect(x, H - h, w, h, "#1a1e2a")
        for wy in range(H - h + 8, H - 8, 14):
            body += rect(x + 6, wy, 6, 7, "#2a2e3a") + rect(x + w - 12, wy, 6, 7, "#2a2e3a")
    body += rect(20, 160, 60, 40, "#5a4a3a") + rect(30, 150, 36, 22, "#b8c0c8", 'rx="4"') + rect(38, 140, 8, 14, "#d8a060") + rect(50, 138, 8, 16, "#d8a060")
    body += stroke("M44 136 q -6 -10 2 -18 M54 134 q 6 -12 -2 -20", "#888", 3, 'opacity="0.8"')
    return svg(defs, body)


def femme_fatale():
    # A glamorous woman in a black evening gown smiling over her cocktail at a casino table, while the red-faced general in
    # the chair beside her is squeezed between her crossed legs, his medals popping off
    defs = lin("bg", ["#2a5a3a", "#0a1a10"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(6):
        body += circle(20 + i * 64, 24, 4, "#ffe9a0", 'opacity="0.6"')
    body += rect(0, 150, W, 50, "#1a6a3a") + rect(0, 146, W, 6, "#5a3a1a")
    for x in (40, 60, 290, 310):
        body += ellipse(x, 166, 10, 4, "#c0392b", 'stroke="#f4f4f4" stroke-width="1.5"')
    body += person(120, 160, 1.05, SKIN[0], "#111", face="smirk", hat="bob")
    body += stroke("M96 150 L76 118", "#111", 7) + path("M64 98 L84 98 L74 110 Z", "#e8f4fa", 'opacity="0.85"') + stroke("M74 110 L74 120", "#c8d8e0", 2)
    body += person(236, 166, 1.0, "#e8746a", "#4a5a3a", face="shout", hat="peaked", brows="sad")
    body += sweat(214, 104) + sweat(258, 100)
    # Her leg, in a sheer stocking, wrapped round his waist; a stiletto at the end
    body += stroke("M138 158 C 176 128 226 128 258 146", "#111", 17) + stroke("M138 158 C 176 128 226 128 258 146", "#e8c0a0", 13)
    body += path("M256 140 L274 150 L270 156 L262 154 L262 164 L258 164 L256 152 Z", "#a01e2a")
    for x, y in ((210, 128), (226, 118), (244, 124)):
        body += circle(x, y, 4, "#d4af37") + stroke(f"M{x} {y - 4} l -2 -6", "#c0392b", 2)
    return svg(defs, body)


def router_box(x, y, s=1.0):
    """A cardboard box with a router printed on it and a tiny skull sticker under the barcode."""
    out = rect(x, y, 40 * s, 30 * s, "#c8a06a", 'stroke="#8a6a3a" stroke-width="1"') + rect(x + 6 * s, y + 8 * s, 28 * s, 8 * s, "#3a3a44", 'rx="2"')
    out += stroke(f"M{x + 12 * s:.1f} {y + 8 * s:.1f} l -2 -6 M{x + 28 * s:.1f} {y + 8 * s:.1f} l 2 -6", "#3a3a44", 1.5)
    out += rect(x + 6 * s, y + 20 * s, 14 * s, 6 * s, "#f4f4f4") + circle(x + 30 * s, y + 23 * s, 3 * s, "#f4f4f4") + circle(x + 30 * s, y + 23 * s, 1.2 * s, "#222")
    return out


def supply_chain_attack():
    # A cheerful delivery driver unloading crates of identical routers at a ministry's loading dock, each box with a tiny skull
    # sticker under the barcode, while a line of bureaucrats in grey suits signs for them
    defs, body = room("#b8b0a0", "#8a8478", "#5a5650", 150)
    body += rect(150, 20, 190, 40, "#e8e0d0", 'stroke="#555" stroke-width="2"') + lettering("MINISTRY", 245, 32, 10, "#3a3a3a", width=1.8)
    body += rect(0, 70, 110, 80, "#f4f4f4", 'rx="4"') + rect(4, 76, 40, 30, "#9ab8c8") + circle(24, 152, 12, "#222") + circle(90, 152, 12, "#222")
    body += person(100, 200, 0.95, SKIN[1], "#c0562a", face="grin", hat="cap")
    for i, (x, y) in enumerate(((128, 160), (168, 160), (148, 132), (64, 166))):
        body += router_box(x, y)
    for i in range(3):
        x = 236 + i * 42
        body += agent(x, 200, 0.7, SKIN[(i + 1) % 4], face="flat", glasses=None, hair=OLD_HAIR if i == 1 else "#4a4a4a", suit="#7a7e86")
    body += rect(222, 150, 22, 28, "#b08a5a") + rect(225, 154, 16, 20, "#ffffff") + stroke("M228 168 q 4 -6 8 0 t 6 -2", "#1a3a8a", 1.2)
    return svg(defs, body)


def hollowed_out_volcano_lair():
    # A cutaway of a volcano with a sleek steel base inside: a monorail, a rocket on its launch pad, henchmen in matching
    # jumpsuits, and at the top a bald man in a grey tunic stroking a white cat on a throne behind armored glass
    defs = lin("bg", ["#f0905a", "#6a2a4a"]) + lin("inside", ["#4a5058", "#2a2e34"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 186, W, 14, "#1a3a5a")
    body += poly([(0, 200), (120, 30), (232, 30), (352, 200)], "#4a3a32") + ellipse(176, 30, 56, 8, "#ff6a2a", 'opacity="0.8"')
    body += poly([(40, 196), (136, 56), (216, 56), (312, 196)], "url(#inside)")
    body += stroke("M120 30 C 116 20 130 10 140 18 M232 30 C 238 18 222 8 212 18", "#8a8a8a", 6, 'opacity="0.6"')
    body += rect(166, 70, 18, 70, "#e8e8e8", 'rx="8"') + poly([(166, 76), (175, 56), (184, 76)], "#c0392b") + poly([(162, 140), (166, 124), (166, 140)], "#c0392b") + poly([(188, 140), (184, 124), (184, 140)], "#c0392b")
    body += rect(150, 140, 50, 6, "#888")
    body += stroke("M60 170 L292 170", "#c8c8c8", 4) + rect(90, 156, 40, 14, "#f2c94c", 'rx="6"') + rect(96, 159, 8, 6, "#3a5a7a") + rect(108, 159, 8, 6, "#3a5a7a")
    for x in (220, 246, 270):
        body += person(x, 186, 0.36, SKIN[0], "#f2a01a", face="flat", hat="cap")
    body += rect(112, 84, 46, 34, "#8ab8d8", 'opacity="0.5" stroke="#c8e0f0" stroke-width="2"')
    body += rect(120, 100, 30, 18, "#5a1a1a") + person(135, 116, 0.42, SKIN[0], "#8a8a8a", face="smirk")
    body += ellipse(146, 110, 6, 4, "#ffffff") + circle(150, 106, 3, "#ffffff")
    return svg(defs, body)


def nda():
    # A smiling corporate lawyer holding out a gold pen to a nervous employee whose mouth is closed with a big brass zipper, a
    # towering stack of contracts between them stamped CONFIDENTIAL
    defs, body = bg_office()
    body += rect(140, 70, 70, 100, "#f6f2e6", 'stroke="#b8b0a0" stroke-width="1"')
    for y in range(76, 170, 6):
        body += stroke(f"M142 {y} L208 {y}", "#d8d0c0", 1)
    body += stamp("CONFIDENTIAL", 175, 110, 6, "#c0392b", -16)
    body += agent(70, 200, 1.1, SKIN[0], face="grin", glasses=None, hair="#2a2a2a", tie="#a01e2a")
    body += stroke("M96 160 L134 134", "#22262e", 10) + stroke("M134 134 L156 120", "#d4af37", 4)
    body += person(280, 200, 1.1, SKIN[2], "#7a9ab0", face="closed", hair="#1a1a1a", brows="sad") + sweat(256, 104) + sweat(304, 100)
    hx, hy = 280, 200 - 64 * 1.1
    body += rect(hx - 12, hy + 7, 24, 6, "#b8902a", 'rx="2"') + stroke(f"M{hx - 10} {hy + 10} L{hx + 10} {hy + 10}", "#6a4a10", 1.5, 'stroke-dasharray="2 2"')
    body += rect(hx + 10, hy + 6, 5, 9, "#d4af37")
    return svg(defs, body)

CARDS["ShadowOps"].update({
    "DeadMansSwitch": dead_mans_switch, "FellOutofaWindow": fell_out_of_a_window, "CoupdÉtat": coup_detat,
    "OperationPaperclip": operation_paperclip, "FalseFlagOperation": false_flag_operation,
    "NeverLetThemKnowYourNextMove": never_let_them_know_your_next_move, "FrameJob": frame_job, "DevilsBreath": devils_breath,
    "HeDidntKillHimself": he_didnt_kill_himself, "Extraction": extraction, "WitnessProtectionProgram": witness_protection_program,
    "DeathByNaturalCauses": death_by_natural_causes, "AssetRecruitment": asset_recruitment,
    "IfIToldYouIdHavetoKillYou": if_i_told_you_id_have_to_kill_you, "LoneGunman": lone_gunman, "GlomarResponse": glomar_response,
    "Sabotage": sabotage, "GhostArmy": ghost_army, "InflatableTank": inflatable_tank, "ShermanTank": sherman_tank, 
    "SocialEngineering": social_engineering, "BilderbergMembership": bilderberg_membership, "DeadDrop": dead_drop,
    "WetWork": wet_work, "PerfectlyTimedCameraMalfunction": perfectly_timed_camera_malfunction,
    "TheGoldenGun": the_golden_gun, "DieAnotherDay": die_another_day, "LicencetoKill": licence_to_kill,
    "ShakenNotStirred": shaken_not_stirred, "GoldenEye": golden_eye, "FemmeFatale": femme_fatale,
    "SupplyChainAttack": supply_chain_attack, "HollowedOutVolcanoLair": hollowed_out_volcano_lair,
})
CARDS["Corporate"].update({"NDA": nda, "Casino": casino})
CARDS["Zeitgeist"].update({"Multipass": multipass})


# ---------------------------------------------------------------- Myth, the crusade (2026-10-14): the wrath, the prophecies, the politics
def bucket_helm(hx, hy, r, color="#9aa0a8", cross=None):
    """A crusader's great helm over a head at (hx, hy) of radius r: a flat-topped bucket with an eye slit."""
    out = rect(hx - r - 2, hy - r - 4, 2 * r + 4, 2 * r + 8, color, 'rx="4" stroke="#5a5f66" stroke-width="2"')
    out += rect(hx - r + 2, hy - 4, 2 * r - 4, 4, "#1a1a1a")
    out += stroke(f"M{hx} {hy + 2} L{hx} {hy + r + 2}", "#5a5f66", 2)
    for i in range(3):
        out += circle(hx - 6 + i * 6, hy + 10, 1.4, "#3a3f46")
    if cross:
        out += rect(hx - 1.5, hy - r - 2, 3, r - 6, cross) + rect(hx - 6, hy - r + 4, 12, 3, cross)
    return out


def knight(cx, base, s=1.0, tabard="#f4f0e6", cross="#c0392b", face_left=False, helm="#9aa0a8"):
    """A crusader bust: chainmail arms, a tabard with a cross, a bucket helm."""
    out = path(f"M{cx - 34 * s:.1f} {base:.1f} C {cx - 34 * s:.1f} {base - 46 * s:.1f} {cx + 34 * s:.1f} {base - 46 * s:.1f} {cx + 34 * s:.1f} {base:.1f} Z", "#8a9098")
    out += path(f"M{cx - 22 * s:.1f} {base:.1f} L{cx - 18 * s:.1f} {base - 38 * s:.1f} L{cx + 18 * s:.1f} {base - 38 * s:.1f} L{cx + 22 * s:.1f} {base:.1f} Z", tabard)
    out += rect(cx - 3 * s, base - 34 * s, 6 * s, 30 * s, cross) + rect(cx - 12 * s, base - 24 * s, 24 * s, 6 * s, cross)
    hx, hy, r = cx, base - 64 * s, 20 * s
    out += bucket_helm(hx, hy, r, helm)
    return out


def angel(cx, cy, s=1.0):
    out = ""
    for f in (-1, 1):
        out += path(f"M{cx} {cy} C {cx + f * 30 * s} {cy - 30 * s} {cx + f * 46 * s} {cy - 6 * s} {cx + f * 40 * s} {cy + 14 * s} C {cx + f * 26 * s} {cy + 6 * s} {cx + f * 14 * s} {cy + 10 * s} {cx} {cy + 6 * s} Z", "#ffffff", 'stroke="#d8d0c0" stroke-width="1.5"')
    out += path(f"M{cx - 12 * s} {cy + 30 * s} L{cx - 8 * s} {cy} L{cx + 8 * s} {cy} L{cx + 12 * s} {cy + 30 * s} Z", "#f4f0e6")
    out += circle(cx, cy - 8 * s, 9 * s, SKIN[0]) + ellipse(cx, cy - 20 * s, 10 * s, 3 * s, "none", f'stroke="#ffd23a" stroke-width="{2.5 * s:.1f}"')
    out += circle(cx - 3 * s, cy - 9 * s, 1.3 * s, "#222") + circle(cx + 3 * s, cy - 9 * s, 1.3 * s, "#222") + ellipse(cx, cy - 4 * s, 2 * s, 2.5 * s, "#5a1a1a")
    return out


def banner(x, top, color="#f4f0e6", emblem="#d4af37", flip=False):
    f = -1 if flip else 1
    out = stroke(f"M{x} {top} L{x} {top + 110}", "#6a4a2a", 3)
    out += path(f"M{x} {top + 2} L{x + f * 46} {top + 2} L{x + f * 38} {top + 18} L{x + f * 46} {top + 34} L{x} {top + 34} Z", color, 'stroke="#c8c0a8" stroke-width="1"')
    cx = x + f * 20
    out += rect(cx - 2, top + 8, 4, 22, emblem) + rect(cx - 8, top + 14, 16, 4, emblem)
    return out


def holy_war():
    # Two armies of knights charging each other across a field, both under identical banners with the same golden cross,
    # a bewildered angel in the clouds above holding a scorecard
    defs, body = bg_myth()
    body += rect(0, 160, W, 40, "#5a7a3a")
    body += banner(40, 30) + banner(312, 30, flip=True)
    for i, x in enumerate((50, 96, 140)):
        body += knight(x, 200 - i * 4, 0.75 - i * 0.05, tabard="#f4f0e6", cross="#d4af37")
    for i, x in enumerate((302, 256, 212)):
        body += knight(x, 200 - i * 4, 0.75 - i * 0.05, tabard="#f4f0e6", cross="#d4af37")
    body += stroke("M152 120 L196 92", "#c8c8d0", 4) + stroke("M200 120 L158 92", "#c8c8d0", 4)
    body += cloud(176, 34, 1.2, "#ffffff", 0.95) + angel(176, 30, 0.8)
    body += rect(190, 26, 22, 16, "#f6f2e6", 'stroke="#888" stroke-width="1"') + lettering("?", 201, 28, 10, "#c0392b")
    return svg(defs, body)


def the_great_flood():
    # A wooden ark riding a huge wave, the animals two by two waving from the deck, while a line of enemy soldiers on a
    # hilltop holds umbrellas as the water reaches their chins
    defs = lin("bg", ["#2a3448", "#5a6a7a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(30):
        x, y = (i * 37) % W, (i * 23) % 140
        body += stroke(f"M{x} {y} l -4 12", "#9ab0c8", 1.2, 'opacity="0.6"')
    body += path("M220 200 C 240 150 300 140 352 146 L352 200 Z", "#4a6a3a")  # The hill
    for x in (250, 280, 310, 338):
        body += circle(x, 150, 7, SKIN[1]) + path(f"M{x - 12} 140 C {x - 12} 126 {x + 12} 126 {x + 12} 140 Z", "#1a1a1a") + stroke(f"M{x} 140 L{x} 150", "#1a1a1a", 1.5)
    body += rect(220, 152, 132, 48, "#2a6ab0", 'opacity="0.85"')
    body += path("M0 150 C 60 110 120 110 170 140 C 200 158 220 150 240 160 L240 200 L0 200 Z", "#2a6ab0")
    body += path("M10 140 C 50 112 110 108 150 128", "none", 'stroke="#e8f8ff" stroke-width="4"')
    # The ark
    body += path("M40 112 L200 112 L184 140 L56 140 Z", "#7a4a2a", 'stroke="#4a2a1a" stroke-width="2"')
    body += rect(80, 86, 80, 26, "#9a6a3a", 'stroke="#4a2a1a" stroke-width="2"') + path("M74 88 L120 66 L166 88 Z", "#6a3a1a")
    for x in (92, 112, 132, 152):
        body += rect(x - 5, 94, 10, 10, "#2a1a10")
    # Two by two: giraffes and elephants waving from the deck
    for x in (60, 68):
        body += stroke(f"M{x} 112 L{x - 4} 74", "#e8b84a", 5) + ellipse(x - 6, 72, 7, 4, "#e8b84a")
    for x in (176, 190):
        body += circle(x, 104, 9, "#9aa0a8") + stroke(f"M{x - 6} 108 C {x - 12} 116 {x - 4} 120 {x - 2} 114", "#9aa0a8", 4)
    return svg(defs, body)


def mox_nox():
    # An old sundial in a ruined cloister at dusk, the shadow of its gnomon falling across the Latin words MOX NOX carved in
    # the stone, a black candle burning beside it
    defs = lin("bg", ["#2a1a3a", "#c86a3a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in (20, 120, 240):
        body += path(f"M{x} 200 L{x} 80 C {x} 40 {x + 90} 40 {x + 90} 80 L{x + 90} 200 L{x + 76} 200 L{x + 76} 84 C {x + 76} 56 {x + 14} 56 {x + 14} 84 L{x + 14} 200 Z", "#5a4a5a", 'opacity="0.8"')
    body += rect(136, 150, 80, 50, "#8a7a6a") + ellipse(176, 150, 70, 20, "#b8a890", 'stroke="#6a5a4a" stroke-width="2"')
    for i in range(12):
        import math
        a = math.pi * (0.1 + 0.8 * i / 11)
        body += stroke(f"M{176 + 52 * math.cos(a):.1f} {150 - 14 * math.sin(a):.1f} L{176 + 62 * math.cos(a):.1f} {150 - 17 * math.sin(a):.1f}", "#5a4a3a", 1.5)
    body += poly([(170, 150), (182, 150), (176, 116)], "#6a5a4a")
    body += path("M176 150 L120 160 L124 166 Z", "#2a1a2a", 'opacity="0.6"')  # The long evening shadow
    body += lettering("MOX NOX", 176, 158, 9, "#3a2a1a", width=1.6)
    body += rect(262, 128, 12, 40, "#111") + flames(268, 128, 1, 3, 12) + ellipse(268, 168, 14, 4, "#3a3a3a")
    return svg(defs, body)


def holy_hand_grenade_of_antioch():
    # A golden orb with a jewelled cross on top, held up by a monk reading from an enormous book, three
    # fingers raised, knights ducking behind him
    defs, body = bg_myth()
    for x in (264, 304):
        body += knight(x, 210, 0.7, tabard="#f4f0e6", cross="#c0392b")
    body += person(140, 200, 1.35, SKIN[1], "#6a4a2a", face="shout", hat="hood")
    body += rect(60, 140, 70, 50, "#7a2a1a", 'stroke="#d4af37" stroke-width="2"') + rect(66, 146, 58, 38, "#f4ecd8")
    for y in (152, 160, 168, 176):
        body += stroke(f"M72 {y} L118 {y}", "#8a8a8a", 1)
    # Three fingers up
    body += stroke("M170 160 L200 120", "#6a4a2a", 10) + circle(202, 116, 8, SKIN[1])
    for i in range(3):
        body += rect(196 + i * 5, 96, 4, 14, SKIN[1], 'rx="2"')
    # The holy hand grenade
    body += circle(232, 70, 22, "#d4af37", 'stroke="#a8842a" stroke-width="2"') + rect(210, 66, 44, 6, "#a8842a")
    body += rect(229, 34, 6, 18, "#d4af37") + rect(222, 40, 20, 5, "#d4af37")
    for x, y, c in [(232, 56, "#c0392b"), (218, 72, "#2a5ad8"), (246, 72, "#1a9a4a")]:
        body += circle(x, y, 3, c)
    return svg(defs, body)


def lance_of_longinus():
    # An ancient iron spearhead on a long wooden shaft, glowing faintly, pinned through a spy's trench coat to a brick wall,
    # the spy wriggling out of the sleeves
    defs = lin("bg", ["#6a3a2a", "#3a1f18"])
    body = rect(0, 0, W, H, "url(#bg)")
    for row in range(10):
        for col in range(9):
            x = col * 44 - (22 if row % 2 else 0)
            body += rect(x, row * 22, 42, 20, "#8a4a32", 'stroke="#5a2a1a" stroke-width="1.5"')
    body += path("M70 40 L140 40 L150 190 L60 190 Z", "#b89a6a", 'stroke="#7a6a4a" stroke-width="2"')  # The empty trench coat
    body += stroke("M105 40 L105 190", "#7a6a4a", 2)
    for y in (90, 120, 150):
        body += circle(98, y, 3, "#4a3a2a")
    body += ellipse(108, 76, 60, 30, "#ffe9a0", 'opacity="0.25"')
    body += stroke("M330 160 L120 80", "#7a5a3a", 7) + poly([(126, 82), (96, 64), (114, 92)], "#4a4a52")
    body += agent(250, 200, 1.0, face="frown", glasses=None, suit="#f4f4f4", tie="#2a2a2a")
    body += speed_lines(200, 120, 3, 30, "#ffffff")
    return svg(defs, body)


def necronomicon():
    # A heavy book bound in stitched leather with a screaming face on its cover, lying open on a stone altar, a hooded
    # figure reading from it while a shadowy hand rises out of the pages
    defs, body = bg_myth()
    body += rect(90, 150, 180, 50, "#6a6a6a", 'stroke="#3a3a3a" stroke-width="2"')
    body += path("M100 150 L176 140 L252 150 L252 160 L176 152 L100 160 Z", "#8a6a4a", 'stroke="#4a3a2a" stroke-width="2"')
    body += path("M104 148 L176 138 L248 148", "none", 'stroke="#f4ecd8" stroke-width="5"')
    # The shadowy hand rising from the pages
    body += path("M168 140 C 160 100 150 80 156 60 L162 60 L166 92 L170 52 L176 52 L176 92 L182 56 L188 58 L184 96 L192 70 L198 74 L188 140 Z", "#1a0a2a", 'opacity="0.9"')
    body += circle(176, 100, 40, "#6a2a8a", 'opacity="0.25"')
    # The cover, standing behind: a stitched screaming face
    body += rect(208, 64, 56, 72, "#a08060", 'rx="4" stroke="#5a3a2a" stroke-width="2"')
    body += stroke("M214 70 L258 130 M258 70 L214 130", "#5a3a2a", 1, 'stroke-dasharray="3 3"')
    body += circle(226, 92, 5, "#2a1a10") + circle(246, 92, 5, "#2a1a10") + ellipse(236, 116, 8, 11, "#2a1a10")
    body += person(64, 200, 1.2, SKIN[1], "#1a1a2a", face="closed", hat="hood")
    return svg(defs, body)


def the_wicker_man():
    # A giant wicker figure burning on a cliff at sunset, villagers in animal masks dancing around it, a small policeman in
    # uniform visible inside its chest
    defs = lin("bg", ["#f2a03a", "#7a2a3a"])
    body = rect(0, 0, W, H, "url(#bg)") + circle(70, 120, 40, "#ffd27a", 'opacity="0.8"')
    body += path("M0 170 L352 160 L352 200 L0 200 Z", "#3a2a1a")
    # The wicker man
    wick = "#c8964a"
    body += circle(200, 30, 16, "none", f'stroke="{wick}" stroke-width="4"')
    body += path("M176 50 L224 50 L236 120 L164 120 Z", "none", f'stroke="{wick}" stroke-width="4"')
    for y in (64, 80, 96, 110):
        body += stroke(f"M{168 + (y - 50) * 0.15:.0f} {y} L{232 - (y - 50) * 0.15:.0f} {y}", wick, 2.5)
    body += stroke("M176 56 L140 100 M224 56 L262 100 M180 120 L170 166 M220 120 L232 166", wick, 6)
    body += person(200, 116, 0.55, SKIN[0], "#1a2a5a", face="shout", hat="peaked")
    body += flames(200, 168, 7, 14, 40)
    # Villagers in animal masks dancing
    for x, mask in [(80, "#f4f4f4"), (120, "#8a5a2a"), (290, "#f4f4f4"), (326, "#c86a2a")]:
        body += person(x, 200, 0.7, SKIN[1], "#e8dcc0", face="closed")
        body += path(f"M{x - 12} {155} L{x - 18} {140} L{x - 4} {150} L{x + 4} {150} L{x + 18} {140} L{x + 12} {155} Z", mask)
    return svg(defs, body)


def thoughts_and_prayers():
    # A politician at a podium covered in microphones, eyes closed and hands pressed together in prayer, while behind him
    # an aide quietly shreds a stack of papers labelled REFORM
    defs = lin("bg", ["#1a2a4a", "#3a4a6a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(13):
        body += rect(0, i * 16, W, 8, "#b22234" if i % 2 == 0 else "#f4f4f4", 'opacity="0.12"')
    body += person(250, 180, 1.0, SKIN[0], "#2a2a3a", face="flat", hair="#3a2a1a")
    body += rect(270, 120, 40, 40, "#d0d0d0", 'stroke="#888" stroke-width="2"') + lettering("REFORM", 290, 106, 7, "#ffffff")
    for x in (276, 286, 296):
        body += stroke(f"M{x} 160 L{x} 180", "#f4f4f4", 2)
    body += person(140, 170, 1.35, SKIN[0], "#1e2440", face="closed", hair="#c8c8c8", tie="#c0392b")

    body += rect(80, 140, 120, 60, "#5a3a2a", 'stroke="#3a2a1a" stroke-width="2"') + circle(140, 166, 12, "#d4af37")
    body += poly([(130, 142), (150, 142), (140, 112)], SKIN[0], 'stroke="#b88a6a" stroke-width="1.5"')  # Hands together
    body += stroke("M140 114 L140 140", "#b88a6a", 1.2)
    for i, x in enumerate((96, 112, 128, 152, 168, 184)):
        body += stroke(f"M{x} 140 L{140 + (x - 140) * 0.3:.0f} 118", "#2a2a2a", 2) + rect(140 + (x - 140) * 0.3 - 3, 110, 6, 9, "#2a2a2a", 'rx="2"')
    return svg(defs, body)


def religious_instrumentalization():
    # A politician in a suit posing for cameras in front of a church, holding a bible upside down over his head, while a
    # confused small creature from the other side shuffles over to stand behind him
    defs, body = bg_myth()
    body += rect(190, 50, 130, 150, "#e8dcc0") + poly([(180, 52), (255, 10), (330, 52)], "#a8742a")
    body += rect(250, 0, 10, 30, "#d4af37") + rect(244, 10, 22, 6, "#d4af37")
    body += path("M236 200 L236 130 C 236 110 274 110 274 130 L274 200 Z", "#6a3a1a")
    body += person(140, 200, 1.35, SKIN[0], "#1e2440", face="grin", hair="#d8c08a", tie="#2a5ad8")
    body += stroke("M112 160 L106 46", "#1e2440", 10) + stroke("M168 160 L174 46", "#1e2440", 10)
    body += circle(106, 42, 6, SKIN[0]) + circle(174, 42, 6, SKIN[0])
    body += rect(98, 14, 84, 30, "#1a1a1a", 'rx="3"') + rect(98, 40, 84, 4, "#f4ecd8")  # The bible, held aloft...
    body += rect(138, 20, 4, 18, "#d4af37") + rect(132, 32, 16, 4, "#d4af37")  # ...upside down
    for x, y in [(20, 60), (40, 150)]:
        body += rect(x, y, 30, 20, "#2a2a2a", 'rx="3"') + circle(x + 15, y + 10, 6, "#6a8ab0") + star_points_poly(x + 22, y - 2)
    body += circle(214, 182, 14, "#8a5aa0") + circle(209, 179, 2, "#111") + circle(219, 179, 2, "#111") + lettering("?", 214, 150, 12, "#ffffff")
    return svg(defs, body)


def deus_vult():
    # A crusader in a bucket helmet and white tabard with a red cross shouting with a raised sword, a meme caption bar at
    # the bottom of the picture
    defs, body = bg_myth()
    body += knight(176, 200, 1.5, tabard="#f4f0e6", cross="#c0392b")
    body += stroke("M220 150 L270 40", "#c8c8d0", 7) + rect(258, 56, 30, 6, "#8a6a2a", 'transform="rotate(-66 273 59)"')
    body += circle(214, 152, 8, "#8a9098")
    body += rect(0, 168, W, 32, "#000000", 'opacity="0.35"')
    body += lettering("DEUS VULT", 176, 172, 20, "#ffffff", width=3.4, outline="#000000")
    return svg(defs, body)


def curse_of_hubris():
    # Icarus with melting wax wings falling past a mountain top, a crowd of Greek gods on a cloud filming him on their phones
    defs = lin("bg", ["#5ab0f0", "#fbe2b0"])
    body = rect(0, 0, W, H, "url(#bg)") + circle(300, 40, 30, "#ffd23a")
    body += poly([(0, 200), (90, 90), (180, 200)], "#8a7a6a") + poly([(68, 118), (90, 90), (112, 118)], "#f4f4f4")
    # Icarus upside down, wings dripping
    body += group(person(0, 0, 0.8, SKIN[0], "#f4f0e6", face="shout", hair="#3a2a1a"), "translate(200 80) rotate(160)")
    for f in (-1, 1):
        body += path(f"M204 92 C {204 + f * 30} 72 {204 + f * 50} 66 {204 + f * 62} 78 C {204 + f * 44} 86 {204 + f * 26} 98 204 102 Z", "#f4ecd0", 'stroke="#d8c8a0" stroke-width="1"')
        for k in range(3):
            body += ellipse(204 + f * (26 + k * 12), 96 + k * 8, 2.5, 4, "#f4ecd0")
    body += cloud(290, 150, 1.4, "#ffffff", 0.95)
    for i, x in enumerate((256, 286, 316)):
        body += person(x, 170, 0.55, SKIN[0], "#f4f0e6", face="grin", beard="#f4f4f4" if i == 1 else None)
        body += phone(x - 10, 140, 0.4)
    return svg(defs, body)


CHUNK = {"Myth": {"HolyWar": holy_war, "TheGreatFlood": the_great_flood, "MoxNox": mox_nox,
                  "HolyHandGrenadeofAntioch": holy_hand_grenade_of_antioch, "LanceOfLonginus": lance_of_longinus,
                  "Necronomicon": necronomicon, "TheWickerMan": the_wicker_man, "ThoughtsandPrayers": thoughts_and_prayers,
                  "ReligiousInstrumentalization": religious_instrumentalization, "DeusVult": deus_vult, "CurseOfHubris": curse_of_hubris}}
for _theme, _cards in CHUNK.items():
    CARDS[_theme].update(_cards)


# ---------------------------------------------------------------- Myth, the faithful and the films (2026-10-14)
def jesus(cx, base, s=1.0, face="smile"):
    """A long-haired, bearded Jesus in white robes, with a halo."""
    hx, hy, r = cx, base - 64 * s, 20 * s
    out = path(f"M{hx - r - 4 * s:.1f} {hy + 30 * s:.1f} C {hx - r - 8 * s:.1f} {hy - r * 1.4:.1f} {hx + r + 8 * s:.1f} {hy - r * 1.4:.1f} {hx + r + 4 * s:.1f} {hy + 30 * s:.1f} Z", "#6a4a2a")
    out += person(cx, base, s, SKIN[1], "#f4f0e6", face=face, beard="#6a4a2a")
    out += ellipse(hx, hy - r - 6 * s, r, 5 * s, "none", f'stroke="#ffd23a" stroke-width="{3 * s:.1f}"')
    return out


def rabbit(cx, cy, s=1.0):
    out = ellipse(cx, cy, 26 * s, 20 * s, "#ffffff", 'stroke="#d8d8d8" stroke-width="1.5"')
    out += circle(cx + 18 * s, cy - 14 * s, 14 * s, "#ffffff", 'stroke="#d8d8d8" stroke-width="1.5"')
    for dx in (10, 22):
        out += ellipse(cx + dx * s, cy - 40 * s, 5 * s, 16 * s, "#ffffff", 'stroke="#d8d8d8" stroke-width="1.5"')
        out += ellipse(cx + dx * s, cy - 40 * s, 2 * s, 11 * s, "#f4b0c0")
    out += circle(cx + 24 * s, cy - 16 * s, 2.2 * s, "#c0392b") + circle(cx + 31 * s, cy - 12 * s, 2 * s, "#f4a0b0")
    out += circle(cx - 26 * s, cy - 2 * s, 6 * s, "#ffffff")
    return out


def the_lost_ark():
    # A golden chest with two winged angels on its lid, opened in a desert camp, ghostly light pouring out and melting the
    # faces of enemy officers while an adventurer in a fedora keeps his eyes shut
    defs = lin("bg", ["#e8a050", "#7a3a2a"]) + rad("glow", [("#ffffff", 0.9), ("#bfe8ff", 0)])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 160, W, 40, "#c89a5a")
    body += circle(176, 100, 120, "url(#glow)")
    body += rect(140, 120, 72, 42, "#d4af37", 'stroke="#a8842a" stroke-width="2"') + rect(136, 112, 80, 10, "#e8c84a")
    for f in (-1, 1):
        body += path(f"M{176 + f * 10} 112 C {176 + f * 20} 90 {176 + f * 36} 86 {176 + f * 34} 112 Z", "#e8c84a")
    for i in range(6):
        body += path(f"M176 110 C {140 + i * 14} 70 {120 + i * 22} 40 {110 + i * 26} 10", "none", 'stroke="#e8f8ff" stroke-width="3" opacity="0.6"')
    for x in (264, 304):
        body += person(x, 200, 0.85, "#d8b0a0", "#5a5a4a", face="shout", hat="peaked")
        body += ellipse(x, 140, 10, 8, "#d8b0a0", 'opacity="0.7"')
    body += indiana(66, 200, 1.3)
    return svg(defs, body)


def indiana(cx, base, s=1.0):
    """The adventurer: a battered brown fedora, stubble, a khaki shirt under a worn leather jacket, a satchel strap and a
    coiled bullwhip; eyes shut, as he was told."""
    hx, hy, r = cx, base - 64 * s, 20 * s
    out = person(cx, base, s, SKIN[1], "#d8c8a0", face="closed", hair="#4a3020")
    # The leather jacket: two fronts open over the shirt, wide collar points
    for f in (-1, 1):
        out += path(f"M{cx + f * 34 * s:.1f} {base:.1f} C {cx + f * 34 * s:.1f} {base - 40 * s:.1f} {cx + f * 22 * s:.1f} {base - 46 * s:.1f} {cx + f * 8 * s:.1f} {base - 44 * s:.1f} "
                    f"L{cx + f * 12 * s:.1f} {base:.1f} Z", "#5a3a22", 'stroke="#3a2414" stroke-width="1.5"')
        out += poly([(cx + f * 6 * s, base - 46 * s), (cx + f * 20 * s, base - 42 * s), (cx + f * 14 * s, base - 26 * s)], "#6a4428", 'stroke="#3a2414" stroke-width="1.5"')
        out += poly([(cx + f * 1 * s, base - 44 * s), (cx + f * 7 * s, base - 46 * s), (cx + f * 8 * s, base - 34 * s)], "#c8b890")
    out += stroke(f"M{cx - 24 * s:.1f} {base - 34 * s:.1f} L{cx + 16 * s:.1f} {base:.1f}", "#3a2414", 4 * s)  # The satchel strap
    out += stroke(f"M{cx - 20 * s:.1f} {base - 20 * s:.1f} q 4 -3 8 0 M{cx + 20 * s:.1f} {base - 14 * s:.1f} q 4 -3 8 0", "#7a5434", 1.5, 'opacity="0.8"')  # Scuffs
    # The coiled whip on his hip
    for i in range(3):
        out += ellipse(cx + 30 * s, base - 8 * s - i * 3 * s, 11 * s - i * s, 5 * s, "none", f'stroke="#2a1a0e" stroke-width="{2.4 * s:.1f}"')
    # Stubble, and a firm mouth
    out += path(f"M{hx - r * 0.95:.1f} {hy + 2 * s:.1f} C {hx - r * 0.9:.1f} {hy + r * 1.25:.1f} {hx + r * 0.9:.1f} {hy + r * 1.25:.1f} {hx + r * 0.95:.1f} {hy + 2 * s:.1f} "
                f"C {hx + r * 0.5:.1f} {hy + r * 0.45:.1f} {hx - r * 0.5:.1f} {hy + r * 0.45:.1f} {hx - r * 0.95:.1f} {hy + 2 * s:.1f} Z", "#4a3020", 'opacity="0.3"')
    for i in range(14):
        dx, dy = ((i * 37) % 26 - 13) * s, (12 + (i * 11) % 8) * s
        out += circle(hx + dx, hy + dy, 0.7 * s, "#3a2414", 'opacity="0.6"')
    # The fedora: a wide brim tilted down at the front, a pinched crown, a dark band
    out += path(f"M{hx - r * 1.75:.1f} {hy - r * 0.5:.1f} C {hx - r:.1f} {hy - r * 0.95:.1f} {hx + r:.1f} {hy - r * 0.95:.1f} {hx + r * 1.75:.1f} {hy - r * 0.6:.1f} "
                f"C {hx + r:.1f} {hy - r * 0.35:.1f} {hx - r:.1f} {hy - r * 0.25:.1f} {hx - r * 1.75:.1f} {hy - r * 0.5:.1f} Z", "#7a5a3a", 'stroke="#4a3420" stroke-width="1.5"')
    out += path(f"M{hx - r * 0.85:.1f} {hy - r * 0.75:.1f} L{hx - r * 0.75:.1f} {hy - r * 1.65:.1f} Q {hx:.1f} {hy - r * 1.35:.1f} {hx + r * 0.75:.1f} {hy - r * 1.7:.1f} "
                f"L{hx + r * 0.85:.1f} {hy - r * 0.8:.1f} Z", "#8a6a48", 'stroke="#4a3420" stroke-width="1.5"')
    out += path(f"M{hx - r * 0.85:.1f} {hy - r * 0.75:.1f} L{hx - r * 0.82:.1f} {hy - r:.1f} L{hx + r * 0.82:.1f} {hy - r * 1.05:.1f} L{hx + r * 0.85:.1f} {hy - r * 0.8:.1f} Z", "#3a2a1a")
    out += stroke(f"M{hx:.1f} {hy - r * 1.4:.1f} L{hx + 1 * s:.1f} {hy - r * 1.1:.1f}", "#5a4028", 1.5)
    return out


def templar_knight():
    # A knight in chainmail and a white mantle with a red cross, sword raised, standing in front of a medieval bank counter
    # piled with gold coins and promissory notes
    defs, body = bg_myth()
    body += rect(200, 110, 152, 90, "#6a3a1a", 'stroke="#3a2a1a" stroke-width="2"')
    for i in range(7):
        body += coin(220 + i * 18, 104 - (i % 3) * 4, 9)
    body += paper(264, 80, 40, 26, 3) + paper(300, 70, 40, 26, 3)
    body += knight(120, 200, 1.5, tabard="#f4f0e6", cross="#c0392b")
    body += stroke("M164 150 L204 30", "#d0d0d8", 6) + rect(176, 120, 32, 6, "#8a6a2a", 'transform="rotate(-70 192 123)"')
    return svg(defs, body)


def zealot():
    # A wild-eyed hermit in sackcloth with a long beard, waving a burning torch and a hand-painted REPENT sign on a busy city
    # sidewalk
    defs = lin("bg", ["#8ab0d0", "#d8d0c0"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x, h in [(0, 120), (60, 160), (230, 140), (300, 170)]:
        body += rect(x, H - h, 56, h, "#7a8090")
        for y in range(H - h + 10, H - 20, 22):
            body += rect(x + 8, y, 10, 12, "#c8d8e8") + rect(x + 34, y, 10, 12, "#c8d8e8")
    body += rect(0, 180, W, 20, "#8a8a8a")
    body += person(176, 200, 1.4, SKIN[2], "#a08a5a", face="shout", hair="#8a8a8a", beard="#c8c8c8", brows="angry")
    body += stroke("M130 160 L100 70", "#6a4a2a", 5) + flames(100, 70, 3, 8, 22)
    body += stroke("M224 170 L262 40", "#8a5a2a", 5) + rect(224, 20, 90, 40, "#f4ecd8", 'stroke="#555" stroke-width="2"')
    body += lettering("REPENT", 269, 30, 16, "#c0392b", width=3)
    return svg(defs, body)


def living_dead():
    # A grey zombie in a business suit climbing out of a cemetery grave with its briefcase, a sunrise behind the tombstones
    defs = lin("bg", ["#f2b06a", "#5a4a6a"])
    body = rect(0, 0, W, H, "url(#bg)") + circle(260, 150, 40, "#ffd27a")
    body += rect(0, 150, W, 50, "#3a4a2a")
    for x in (40, 290, 330):
        body += path(f"M{x - 16} 160 L{x - 16} 120 C {x - 16} 100 {x + 16} 100 {x + 16} 120 L{x + 16} 160 Z", "#8a8a90", 'stroke="#5a5a60" stroke-width="2"')
        body += lettering("RIP", x, 118, 7, "#4a4a50")
    body += ellipse(176, 180, 70, 16, "#2a1a10")
    body += person(176, 196, 1.3, "#9ab08a", "#2a2a3a", face="flat", tie="#5a1a1a", hair="#3a3a3a")
    body += circle(167, 112, 3.5, "#ffffff") + circle(185, 112, 3.5, "#ffffff")
    body += stroke("M210 170 L250 150", "#9ab08a", 8) + rect(244, 136, 34, 24, "#5a3a1a", 'rx="3"') + rect(254, 130, 14, 8, "none", 'stroke="#5a3a1a" stroke-width="2"')
    body += stroke("M140 170 L104 150", "#9ab08a", 8)
    return svg(defs, body)


def the_chosen_one():
    # Jesus in white robes and sandals with a halo, firing two golden submachine guns akimbo, doves scattering: he steals
    # the show, nothing behind him but the temple light
    defs, body = bg_myth()
    body += jesus(176, 200, 1.4, face="smirk")
    for f in (-1, 1):
        x0 = 176 + f * 40
        body += stroke(f"M{176 + f * 24} 160 L{x0 + f * 30} 130", "#f4f0e6", 10)
        body += rect(x0 + f * 28 - 14, 116, 28, 10, "#d4af37", 'rx="2"') + rect(x0 + f * 30 - 3, 124, 6, 14, "#a8842a")
        body += poly([(x0 + f * 44, 121), (x0 + f * 58, 114), (x0 + f * 58, 128)], "#ffe45c")
    for x, y in [(240, 40), (270, 60), (300, 30), (120, 30)]:
        body += path(f"M{x} {y} C {x - 10} {y - 8} {x - 16} {y - 2} {x - 20} {y - 6} C {x - 12} {y + 4} {x - 4} {y + 4} {x} {y} C {x + 6} {y - 8} {x + 14} {y - 10} {x + 20} {y - 6} C {x + 12} {y} {x + 6} {y + 4} {x} {y} Z", "#ffffff")
    return svg(defs, body)


def killer_rabbit_of_caerbannog():
    # A small fluffy white rabbit sitting innocently in front of a cave mouth littered with bones and dented helmets, a tiny
    # drop of red on its whiskers, knights fleeing in the distance
    defs = lin("bg", ["#8ab0c8", "#c8b890"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += path("M60 200 C 60 60 290 60 290 200 Z", "#5a5048") + path("M120 200 C 120 110 230 110 230 200 Z", "#1a1410")
    for x, y in [(110, 186), (150, 192), (236, 188), (262, 194)]:
        body += path(f"M{x - 10} {y} L{x + 10} {y - 4}", "none", 'stroke="#f2efe6" stroke-width="4" stroke-linecap="round"') + circle(x - 10, y, 3, "#f2efe6") + circle(x + 10, y - 4, 3, "#f2efe6")
    body += skull(200, 186, 0.5) + path("M80 190 C 80 172 104 172 104 190 Z", "#9aa0a8")
    body += rabbit(170, 176, 1.0)
    for x in (300, 330):
        body += knight(x, 120, 0.35, tabard="#f4f0e6", cross="#c0392b")
    body += speed_lines(280, 90, 3, 20, "#ffffff")
    return svg(defs, body)


def black_knight():
    # A knight in black armor standing defiantly at a forest bridge with no arms left, sword on the ground, shouting at a
    # bemused king who rides an invisible horse to the sound of coconuts
    defs = lin("bg", ["#5a8a4a", "#2a3a1a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in (20, 70, 300, 340):
        body += rect(x, 0, 14, 200, "#3a2a1a") + circle(x + 7, 20, 30, "#3a6a2a")
    body += rect(60, 170, 220, 14, "#7a5a3a")
    # The black knight: no arms, a black helm
    hx = 120
    body += path(f"M{hx - 32} 200 C {hx - 32} 106 {hx + 32} 106 {hx + 32} 200 Z", "#1a1a1e") + bucket_helm(hx, 104, 24, color="#2a2a2e")
    body += circle(hx - 28, 150, 6, "#7a1a1a") + circle(hx + 28, 150, 6, "#7a1a1a")
    body += stroke("M60 188 L120 196", "#c8c8d0", 4)
    body += lettering("'TIS BUT A SCRATCH", 176, 8, 11, "#ffffff", width=2)
    # The king, trotting on nothing, a servant clapping coconuts
    body += person(250, 168, 0.9, SKIN[0], "#7a2a8a", face="frown", hat="crown", beard="#c8a040")
    body += stroke("M238 168 L230 196 M262 168 L270 196", "#7a2a8a", 6)
    body += person(310, 196, 0.7, SKIN[1], "#8a6a3a", face="smile") + circle(296, 150, 7, "#6a4a2a") + circle(324, 150, 7, "#6a4a2a")
    return svg(defs, body)


def buddy_christ():
    # A smiling plastic Jesus statue winking and giving a big thumbs up and a finger point, on a church lawn next to a sign
    # reading CATHOLICISM: WOW!
    defs = lin("bg", ["#8ac8f0", "#e8f0d0"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 170, W, 30, "#6aa04a")
    body += rect(150, 176, 52, 24, "#c8c8c8")
    body += jesus(176, 182, 1.4, face="grin")
    body += path("M168 100 q 6 -4 12 0", "none", 'stroke="#222" stroke-width="2.5"')  # The wink
    body += stroke("M150 150 L120 120", "#f4f0e6", 10) + circle(118, 118, 8, SKIN[1]) + rect(114, 100, 7, 14, SKIN[1], 'rx="3"')
    body += stroke("M202 150 L236 136", "#f4f0e6", 10) + circle(240, 134, 8, SKIN[1]) + rect(244, 131, 16, 5, SKIN[1], 'rx="2"')
    body += sign(300, 100, 80, 46) + lettering("CATHOLICISM", 300, 108, 8, "#2a2a2a", width=1.4) + lettering("WOW!", 300, 122, 12, "#c0392b", width=2.4)
    return svg(defs, body)


def pope():
    # The Pope waving from a glass-domed popemobile, sprinkling holy water from a garden hose over a crowd of creatures
    defs = lin("bg", ["#8ac8f0", "#f4e8c8"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(60, 130, 200, 50, "#f4f4f4", 'rx="10" stroke="#aaa" stroke-width="2"') + circle(100, 182, 14, "#222") + circle(220, 182, 14, "#222")
    body += person(160, 136, 1.1, SKIN[0], "#f4f4f4", face="smile")
    body += path("M146 50 L174 50 L170 24 C 166 16 154 16 150 24 Z", "#f4f4f4", 'stroke="#d4af37" stroke-width="2"') + rect(158, 26, 4, 18, "#d4af37") + rect(153, 32, 14, 4, "#d4af37")
    body += path("M100 130 C 100 50 220 50 220 130 Z", "#bfe8ff", 'opacity="0.35" stroke="#e8f8ff" stroke-width="2"')
    body += stroke("M186 110 C 230 90 260 96 280 110", "#2a8a3a", 4)  # The garden hose
    for i in range(8):
        body += circle(282 + (i % 4) * 12, 120 + (i // 4) * 14 + (i % 2) * 4, 3, "#6ab0f0")
    for x in (290, 326):
        body += circle(x, 188, 14, ["#8a5aa0", "#e8a040"][x % 2]) + circle(x - 4, 184, 2, "#111") + circle(x + 4, 184, 2, "#111")
    return svg(defs, body)


def the_lobster_professor():
    # A gaunt professor in a three-piece suit lecturing tearfully at a lectern, pointing at a projected slide of a lobster
    # sitting on a throne, the audience taking notes, a messy bedroom visible through a door behind him
    defs = lin("bg", ["#3a3a4a", "#1a1a2a"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(190, 16, 150, 100, "#f4f4f4")
    body += rect(244, 60, 44, 44, "#d4af37") + rect(250, 40, 8, 24, "#d4af37") + rect(274, 40, 8, 24, "#d4af37")
    body += ellipse(266, 70, 18, 10, "#d0342c") + circle(256, 58, 6, "#d0342c") + circle(276, 58, 6, "#d0342c")
    body += rect(20, 40, 50, 110, "#5a3a2a") + rect(26, 46, 38, 98, "#8a7a6a")  # The open door to the bedroom...
    body += path("M30 130 L40 120 L52 134 L60 124", "none", 'stroke="#c8a0d0" stroke-width="4"') + circle(46, 100, 6, "#6a9ad0")  # ...and its mess
    body += person(120, 200, 1.35, "#e8c8b0", "#3a3a48", face="frown", hair="#6a6a6a", tie="#5a1a1a")
    body += ellipse(110, 116, 2, 4, "#6ab0f0") + ellipse(132, 118, 2, 4, "#6ab0f0")  # Tears
    body += stroke("M150 160 L196 70", "#3a3a48", 8) + circle(198, 66, 6, "#e8c8b0")
    body += rect(80, 160, 80, 40, "#6a4a2a")
    return svg(defs, body)


def horsemen_of_apocalypse():
    # Four horsemen galloping out of storm clouds, War, Famine, Pestilence and Death, one of them checking a smartphone map,
    # a lightning bolt behind
    defs = lin("bg", ["#2a1a2a", "#7a2a2a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x, y in [(60, 30), (160, 20), (270, 36)]:
        body += cloud(x, y, 1.6, "#3a3a4a", 0.95)
    body += bolt(300, 40, 1.4)
    for i, (horse, rider) in enumerate([("#f4f4f4", "#c0392b"), ("#1a1a1a", "#8a6a3a"), ("#c8c8a0", "#5a8a3a"), ("#8a9a8a", "#2a2a2a")]):
        x, y = 50 + i * 80, 150 - (i % 2) * 12
        body += ellipse(x, y, 30, 14, horse) + stroke(f"M{x + 22} {y - 6} L{x + 36} {y - 26}", horse, 10) + ellipse(x + 40, y - 28, 10, 6, horse)
        for dx in (-20, -8, 10, 22):
            body += stroke(f"M{x + dx} {y + 10} L{x + dx - 6} {y + 34}", horse, 4)
        body += person(x - 4, y - 8, 0.45, SKIN[0] if i < 3 else "#f2efe6", rider, face="flat", hat="hood" if i == 3 else None)
    body += phone(150, 104, 0.45)
    return svg(defs, body)


def the_omen():
    # A small boy in a neat suit on a tricycle in a long mansion hallway, staring straight at the viewer, a black dog
    # sitting beside him and a 666 birthmark peeking from his hairline
    defs = lin("bg", ["#5a3a2a", "#2a1a14"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += path("M120 0 L176 90 L232 0 Z", "#3a2418") + path("M120 200 L176 90 L232 200 Z", "#7a4a2a")  # The hallway vanishing
    for x in (40, 290):
        body += rect(x, 40, 24, 50, "#d4af37", 'opacity="0.6"')
    body += circle(150, 184, 14, "none", 'stroke="#c0392b" stroke-width="4"') + circle(206, 184, 14, "none", 'stroke="#c0392b" stroke-width="4"')
    body += person(176, 176, 1.0, SKIN[0], "#1a1a2a", face="flat", hair="#2a1a10", tie="#5a1a1a")
    body += lettering("666", 176, 94, 6, "#8a2a2a", width=1.2)
    body += dog(270, 170, 1.0, "#111111", "#1a1a1a")
    return svg(defs, body)


def omen_reading():
    # A robed augur reading the entrails of a goat on a stone altar by candlelight, squinting at a calendar pinned to the
    # wall, an assistant taking notes
    defs, body = bg_myth()
    body += rect(260, 30, 60, 60, "#f4ecd8", 'stroke="#555" stroke-width="2"') + rect(260, 30, 60, 12, "#c0392b")
    for r_ in range(3):
        for c in range(4):
            body += rect(266 + c * 13, 48 + r_ * 13, 9, 9, "none", 'stroke="#888" stroke-width="1"')
    body += circle(301, 65, 7, "none", 'stroke="#c0392b" stroke-width="2"')
    # The augur stands behind the middle of the altar, robe down to the stone, both hands in the entrails
    body += person(170, 204, 1.3, SKIN[1], "#f4f0e6", face="frown", hat="hood", beard="#c8c8c8")
    for f in (-1, 1):
        body += path(f"M{170 + f * 34} 160 C {170 + f * 44} 170 {170 + f * 36} 182 {170 + f * 20} 184 L{170 + f * 12} 176 C {170 + f * 20} 174 {170 + f * 22} 168 {170 + f * 18} 158 Z", "#e8e2d4", 'stroke="#c8c0b0" stroke-width="1"')
    body += rect(90, 182, 160, 18, "#7a7a7a", 'stroke="#4a4a4a" stroke-width="2"')
    body += path("M126 183 C 136 168 158 176 170 168 C 182 176 202 166 214 183 Z", "#b04a5a")
    body += stroke("M140 178 q 8 -6 14 0 q 6 6 14 0 M176 176 q 8 -6 16 0", "#7a2a3a", 2)
    for f in (-1, 1):
        body += circle(170 + f * 16, 180, 5, SKIN[1])
    body += rect(60, 154, 8, 22, "#f4ecd8") + flames(64, 154, 1, 3, 10)
    body += person(320, 200, 0.8, SKIN[0], "#6a4a2a", face="flat") + paper(290, 150, 26, 30, 4)
    return svg(defs, body)


def genesis():
    # The Creation of Adam on a cracked fresco: Adam reclining on a green hillside, his arm held out limply; God in a pink
    # robe, white beard streaming, borne along in a billowing red cloak full of cherubs, reaching back; their fingertips
    # almost touch in the middle
    skin, flesh = "#e8b890", "#c88a62"
    body = rect(0, 0, W, H, "#e8dcc4")
    for d in ("M20 0 L30 30 L22 60", "M120 0 L114 22 L126 40", "M210 200 L216 170 L208 140", "M330 200 L322 176"):
        body += stroke(d, "#b8a888", 1)
    body += path("M0 70 C 40 60 60 70 70 90 L60 200 L0 200 Z", "#a8b8c8", 'opacity="0.6"')  # A patch of blue sky behind him
    # Adam's hillside
    body += path("M0 150 C 60 130 130 150 180 200 L0 200 Z", "#6a8a5a") + path("M0 172 C 60 160 110 170 150 200 L0 200 Z", "#7a6a4a")
    # Adam: reclining, head tilted, a knee raised, the far arm resting on it and stretched toward God
    def limb(d, width):
        return stroke(d, flesh, width + 3) + stroke(d, skin, width)
    body += limb("M30 132 L18 170", 10)  # Leaning back on his far elbow
    body += limb("M76 170 L140 182 L178 190", 15)  # The near leg stretched out down the hill
    body += path("M22 128 C 36 116 62 116 72 124 L86 166 C 74 178 44 178 30 168 Z", skin, f'stroke="{flesh}" stroke-width="1.5"')  # Torso
    body += stroke("M40 140 q 8 4 16 0 M46 154 l 0 8", flesh, 1.2, 'opacity="0.7"')
    body += limb("M80 166 L114 134", 16) + limb("M114 134 L124 184", 12)  # The far knee raised
    body += limb("M62 124 L110 124 L148 104", 10)  # His arm resting on the knee, held out
    body += path("M146 100 C 154 98 160 100 164 106 L160 109 C 156 106 152 106 148 108 Z", skin, f'stroke="{flesh}" stroke-width="1.2"')  # A limp hand
    body += stroke("M152 107 L160 114 M149 108 L155 115", flesh, 1.4)
    body += ellipse(40, 106, 13, 15, skin, f'stroke="{flesh}" stroke-width="1.5"') + path("M27 104 C 28 88 50 86 54 100 C 46 96 34 98 27 104 Z", "#7a5030")
    body += stroke("M46 104 l 5 0", "#3a2a1a", 1.5) + stroke("M44 114 q 4 2 7 0", "#8a5040", 1.2)
    body += path("M66 172 C 72 164 84 164 90 170 C 84 178 72 178 66 172 Z", "#5a7a4a")  # A fig leaf
    # The red cloak, a shell full of cherubs
    body += path("M196 60 C 220 6 330 0 352 30 L352 150 C 320 170 250 160 222 130 C 230 110 210 90 196 60 Z", "#9a3a3a")
    body += path("M210 66 C 232 20 320 18 344 42 L344 140 C 312 156 252 148 230 124 C 236 104 222 86 210 66 Z", "#c8a890")
    for x, y, hair in [(244, 46, "#d8a040"), (268, 36, "#c8902a"), (296, 34, "#d8a040"), (324, 44, "#b8802a"), (330, 76, "#d8a040"), (312, 120, "#c8902a"), (262, 126, "#b8802a")]:
        body += circle(x, y, 10, skin, f'stroke="{flesh}" stroke-width="1"') + path(f"M{x - 10} {y - 2} C {x - 10} {y - 14} {x + 10} {y - 14} {x + 10} {y - 2} C {x + 4} {y - 8} {x - 4} {y - 8} {x - 10} {y - 2} Z", hair)
        body += circle(x - 3, y, 1.2, "#3a2a1a") + circle(x + 3, y, 1.2, "#3a2a1a")
    # God: flying left, body in a pink robe trailing to the right, legs out behind
    body += path("M236 84 C 260 70 300 76 330 96 C 344 108 340 126 324 128 C 300 124 270 112 246 108 Z", "#e8c8d0", 'stroke="#b890a0" stroke-width="1.5"')
    body += path("M300 112 C 322 124 340 136 352 140 L352 152 C 334 150 312 138 296 124 Z", skin, f'stroke="{flesh}" stroke-width="1.2"')  # A bare leg
    body += path("M250 88 C 236 90 220 94 200 98 L198 106 C 220 104 238 102 252 100 Z", skin, f'stroke="{flesh}" stroke-width="1.5"')  # The reaching arm
    body += path("M200 97 C 194 96 190 98 188 102 L196 107 L200 106 Z", skin, f'stroke="{flesh}" stroke-width="1.2"')
    body += stroke("M190 101 L176 102", skin, 3.2) + stroke("M190 101 L176 102", flesh, 0.6, 'opacity="0.6"')  # The pointing finger
    # His head: white hair and a long beard streaming back
    body += path("M228 62 C 238 50 256 52 262 62 C 268 76 262 90 252 94 Z", "#e4e4ec")
    body += ellipse(238, 74, 11, 13, skin, f'stroke="{flesh}" stroke-width="1.2"')
    body += path("M228 76 C 230 94 246 100 262 96 C 270 92 266 84 256 80 C 248 86 236 84 228 76 Z", "#f0f0f4", 'stroke="#c0c0cc" stroke-width="1"')
    body += stroke("M231 70 l 6 0", "#3a2a1a", 1.6) + stroke("M228 66 l 9 -2", "#d8d8e0", 2)
    return svg("", body)


CHUNK = {"Myth": {"TheLostArk": the_lost_ark, "TemplarKnight": templar_knight, "Zealot": zealot, "LivingDead": living_dead,
                  "TheChosenOne": the_chosen_one, "KillerRabbitofCaerbannog": killer_rabbit_of_caerbannog, "BlackKnight": black_knight,
                  "BuddyChrist": buddy_christ, "Pope": pope, "TheLobsterProfessor": the_lobster_professor,
                  "HorsemenOfApocalypse": horsemen_of_apocalypse, "TheOmen": the_omen, "OmenReading": omen_reading, "Genesis": genesis}}
for _theme, _cards in CHUNK.items():
    CARDS[_theme].update(_cards)


# ---------------------------------------------------------------- Send For, one per faction (2026-10-14)
def thick_glasses(hx, hy, s=1.0):
    """Chunky black plastic frames (gafas de pasta) over the eyes of a head at (hx, hy)."""
    out = ""
    for dx in (-8, 8):
        out += rect(hx + dx * s - 7 * s, hy - 7 * s, 14 * s, 11 * s, "#ffffff", f'opacity="0.25" rx="{2 * s:.1f}"')
        out += rect(hx + dx * s - 7 * s, hy - 7 * s, 14 * s, 11 * s, "none", f'rx="{2 * s:.1f}" stroke="#111" stroke-width="{3.2 * s:.1f}"')
    out += stroke(f"M{hx - 1 * s:.1f} {hy - 3 * s:.1f} L{hx + 1 * s:.1f} {hy - 3 * s:.1f}", "#111", 3 * s)
    return out


def headhunter():
    # A recruiter in a sharp suit and chunky black-framed glasses holding up the head of a sysadmin to the camera, like a
    # trophy, the sysadmin's beard, round glasses and grumpy face still on it
    defs, body = bg_office()
    hx, base, s = 120, 200, 1.4
    body += person(hx, base, s, SKIN[0], "#1e2440", face="flat", hair="#2a1a10", tie="#c0392b")
    hy, r = base - 64 * s, 20 * s
    body += circle(hx, hy, r, SKIN[0])  # Repaint the face: big frames, the smirk well below them
    body += circle(hx - 13, hy - 1, 3, "#222") + circle(hx + 13, hy - 1, 3, "#222")
    body += thick_glasses(hx, hy - 1, s * 1.2)
    body += stroke(f"M{hx - 9} {hy + 17} Q {hx + 2} {hy + 20} {hx + 11} {hy + 12}", "#222", 2.4)
    # His arm held out toward the camera, the head in his hand
    body += stroke(f"M{hx + 30} 168 L 220 120", "#1e2440", 13)
    hx2, hy2, r2 = 240, 92, 26
    # A little blood dripping from under the beard
    body += path(f"M{hx2 - 14} {hy2 + 28} C {hx2 - 6} {hy2 + 38} {hx2 + 8} {hy2 + 38} {hx2 + 14} {hy2 + 28} Z", "#8a0f0f")
    for dx, length in ((-8, 14), (1, 22), (9, 10)):
        body += path(f"M{hx2 + dx - 2.5} {hy2 + 32} L{hx2 + dx - 2} {hy2 + 32 + length} C {hx2 + dx - 4} {hy2 + 37 + length} {hx2 + dx + 4} {hy2 + 37 + length} {hx2 + dx + 2} {hy2 + 32 + length} L{hx2 + dx + 2.5} {hy2 + 32} Z", "#a01414")
    body += sweat(hx2 + 1, hy2 + 64, 0.6).replace("#8fd8ff", "#a01414")
    body += path(f"M{hx2 - r2 - 2} {hy2 + 2} C {hx2 - r2} {hy2 - r2 * 1.6} {hx2 + r2} {hy2 - r2 * 1.6} {hx2 + r2 + 2} {hy2 + 2} Z", "#5a3a1a")
    body += circle(hx2, hy2, r2, SKIN[0])
    body += path(f"M{hx2 - r2} {hy2} C {hx2 - r2} {hy2 + r2 * 1.7} {hx2 + r2} {hy2 + r2 * 1.7} {hx2 + r2} {hy2} C {hx2 + r2 * 0.5} {hy2 + r2 * 0.7} {hx2 - r2 * 0.5} {hy2 + r2 * 0.7} {hx2 - r2} {hy2} Z", "#6a4a2a")
    for dx in (-9, 9):
        body += circle(hx2 + dx, hy2 - 3, 7, "none", 'stroke="#222" stroke-width="2.4"') + circle(hx2 + dx, hy2 - 3, 2.4, "#222")
    body += stroke(f"M{hx2 - 2} {hy2 - 3} L{hx2 + 2} {hy2 - 3}", "#222", 2.4)
    body += stroke(f"M{hx2 - 8} {hy2 + 13} L{hx2 + 8} {hy2 + 13}", "#222", 2)  # Unimpressed
    body += stroke(f"M{hx2 - 18} {hy2 - 16} L{hx2 - 8} {hy2 - 13} M{hx2 + 18} {hy2 - 16} L{hx2 + 8} {hy2 - 13}", "#222", 2)
    # His hand gripping it by the hair from above
    body += circle(hx2 - 12, hy2 - r2 - 4, 9, SKIN[0]) + rect(hx2 - 20, hy2 - r2 - 2, 26, 7, SKIN[0], 'rx="3"')
    body += rect(300, 150, 40, 30, "#ffffff", 'rx="3" stroke="#aaa" stroke-width="2"') + lettering("HIRED", 320, 160, 7, "#1a9a4a")
    return svg(defs, body)


def doomscrolling():
    # The Living the Dream wojak, MS Paint style: bald, shirtless, baggy-eyed and expressionless at 3:07 AM, only the
    # black back of a phone where the computer used to be, its glow on his face, cards drifting up out of the feed
    ink = "#111111"
    body = rect(0, 0, W, H, "#1a1a2e") + rect(0, 0, W, H, "#ffffff", 'opacity="0.04"')
    body += circle(150, 100, 110, "#9ab8ff", 'opacity="0.12"') + circle(150, 100, 60, "#9ab8ff", 'opacity="0.12"')
    body += lettering("3AM", 44, 14, 16, "#ffffff", width=1.8)
    line = f'stroke="{ink}" stroke-width="2.4" stroke-linejoin="round" stroke-linecap="round"'
    # Shoulders and chest, the meme's few chest marks
    body += path("M50 200 C 56 168 84 156 118 152 L162 152 C 196 156 224 168 230 200 Z", "#ffffff", line)
    body += stroke("M108 170 L108 180 M172 170 L172 180 M132 184 L136 184 M140 190 C 146 192 150 192 156 188", ink, 2)
    # Neck and the egg-shaped head
    body += path("M124 136 L122 156 L160 156 L158 136 Z", "#ffffff", line)
    body += path("M98 84 C 94 30 186 26 184 84 C 184 118 168 142 141 142 C 114 142 98 118 98 84 Z", "#ffffff", line)
    body += path("M98 86 C 90 84 90 100 100 102 M184 86 C 192 84 192 100 182 102", "none", line)  # Ears
    # Tired eyes with purple bags, a little c of a nose, a flat mouth
    for ex in (124, 160):
        body += stroke(f"M{ex - 9} 84 Q {ex} 80 {ex + 9} 84", ink, 2.2) + circle(ex + 1, 87, 2.6, ink)
        body += path(f"M{ex - 9} 92 Q {ex} 98 {ex + 9} 92", "none", 'stroke="#8a4ab0" stroke-width="2.4" opacity="0.8"')
        body += stroke(f"M{ex - 8} 93 Q {ex} 97 {ex + 8} 93", ink, 1.4)
    body += path("M146 96 C 138 98 138 112 147 112", "none", line)
    body += stroke("M128 124 Q 141 121 154 124", ink, 2.2)
    # His arm up, holding the phone in front of his face: we only see its black back
    body += path("M212 200 C 214 176 224 156 236 140 L250 148 C 238 166 232 184 232 200 Z", "#ffffff", line)
    body += rect(222, 70, 40, 74, "#0a0a0a", 'rx="7"') + rect(226, 74, 32, 66, "none", 'rx="5" stroke="#3a3a3a" stroke-width="1.5"')
    body += stroke("M248 80 L248 92", "#555", 3)  # The camera bump
    body += path("M222 118 C 214 118 212 130 220 132 C 214 134 216 144 226 142 L236 140 L236 116 Z", "#ffffff", line)  # Fingers
    body += path("M260 96 C 268 94 270 104 262 106", "none", line)  # The thumb over the top edge, scrolling
    body += stroke("M272 88 l 8 -6 M274 98 l 10 0 M272 108 l 8 6", "#ffffff", 2, 'opacity="0.6"')
    # The feed, cards drifting up out of it
    for i in range(4):
        x, y = 278 + i * 14, 60 - i * 15
        body += rect(x, y, 24, 14, "#f4f4f4", f'rx="2" opacity="{1 - i * 0.2:.2f}"') + rect(x + 2, y + 3, 7, 8, "#e8a0c0", f'opacity="{1 - i * 0.2:.2f}"')
    return svg("", body)


def lucid_dreaming():
    # A sleeper floating above their bed in pyjamas, calmly picking cards out of a swirling dreamscape of melting clocks and
    # staircases
    defs, body = bg_mind()
    body += rect(40, 160, 180, 26, "#6a4a8a", 'rx="4"') + rect(40, 150, 40, 16, "#f4f4f4", 'rx="4"')
    body += group(person(0, 0, 1.0, SKIN[0], "#8ab0e0", face="closed", hair="#3a2a1a"), "translate(140 120) rotate(-80)")
    body += zzz(170, 60, 0.8, "#ffffff")
    # The dreamscape: a melting clock, a staircase to nowhere, cards drifting
    body += path("M250 40 C 290 40 300 70 290 90 C 286 100 280 120 284 130 C 276 120 270 100 262 96 C 240 90 230 60 250 40 Z", "#f4ecd0", 'stroke="#a88a4a" stroke-width="2"')
    body += stroke("M266 56 L266 68 L276 72", "#333", 2)
    for i in range(4):
        body += rect(290 + i * 12, 170 - i * 14, 12, 14 + i * 14, "#c8a0d8")
    for x, y, a in [(210, 60, -15), (230, 110, 20), (196, 30, 10)]:
        body += rect(x, y, 18, 26, "#ffffff", f'rx="2" transform="rotate({a} {x + 9} {y + 13})" stroke="#c8a0d8" stroke-width="2"')
    return svg(defs, body)


def supply_depot():
    # A huge military warehouse with endless shelves of crates, a forklift carrying a soldier in a crate labelled
    # REINFORCEMENTS, a clerk stamping a form at the front desk
    defs = lin("bg", ["#5a5a4a", "#2a2a22"])
    body = rect(0, 0, W, H, "url(#bg)")
    for row in range(3):
        body += rect(0, 40 + row * 44, W, 4, "#7a6a4a")
        for col in range(10):
            body += rect(6 + col * 36, 16 + row * 44, 28, 24, "#8a7a4a", 'stroke="#5a4a2a" stroke-width="1.5"')
    body += rect(150, 120, 90, 16, "#e8b830") + rect(150, 136, 40, 40, "#e8b830") + circle(160, 180, 10, "#222") + circle(226, 180, 10, "#222")
    body += rect(236, 80, 6, 100, "#555") + rect(242, 120, 50, 6, "#555")
    body += rect(244, 76, 52, 44, "#8a7a4a", 'stroke="#5a4a2a" stroke-width="2"') + lettering("REINFORCEMENTS", 270, 108, 4, "#2a2a1a", width=0.9)
    body += circle(270, 80, 10, SKIN[1]) + path("M258 78 C 258 64 282 64 282 78 Z", "#4a5a2a")
    body += rect(30, 150, 90, 50, "#6a4a2a") + person(76, 150, 0.8, SKIN[0], "#4a5a2a", face="flat", hat="peaked")
    body += paper(44, 140, 30, 20, 3) + stamp("OK", 60, 150, 6, "#c0392b", -10)
    return svg(defs, body)


def web_scraping():
    # A cat scratching a laptop screen, claw marks tearing the web page open and spilling out cards and data
    defs, body = bg_tech()
    body += rect(150, 60, 150, 100, "#222", 'rx="6"') + rect(158, 68, 134, 84, "#f4f4f4") + rect(130, 160, 190, 10, "#555", 'rx="3"')
    body += rect(158, 68, 134, 12, "#4a7ad0")
    for y in (90, 102, 114):
        body += stroke(f"M166 {y} L270 {y}", "#bbb", 3)
    for i in range(3):
        body += stroke(f"M{200 + i * 14} 76 L{186 + i * 14} 150", "#c0392b", 3)
    for x, y, a in [(250, 170, 20), (276, 182, -10), (300, 166, 30)]:
        body += rect(x, y, 16, 22, "#ffffff", f'rx="2" stroke="#4a7ad0" stroke-width="2" transform="rotate({a} {x + 8} {y + 11})"')
    # The cat, up on its hind legs
    body += ellipse(110, 150, 30, 40, "#e89a40") + circle(110, 96, 24, "#e89a40")
    body += poly([(92, 80), (96, 60), (106, 76)], "#e89a40") + poly([(114, 76), (124, 60), (128, 80)], "#e89a40")
    body += circle(102, 94, 3, "#1a1a1a") + circle(118, 94, 3, "#1a1a1a") + poly([(108, 102), (112, 102), (110, 105)], "#e86a8a")
    body += stroke("M132 130 L176 104", "#e89a40", 9) + stroke("M176 104 l 6 -4 M176 104 l 7 0 M176 104 l 6 4", "#ffffff", 1.5)
    return svg(defs, body)


def research_lab():
    # A gleaming laboratory with scientists in white coats pulling glowing cards out of test tubes and a centrifuge, a grant
    # deadline clock ticking on the wall
    defs, body = bg_utopia()
    body += circle(300, 40, 22, "#ffffff", 'stroke="#555" stroke-width="3"') + stroke("M300 40 L300 26 M300 40 L310 46", "#c0392b", 2.5)
    body += lettering("GRANT", 300, 66, 6, "#c0392b")
    body += rect(0, 160, W, 40, "#c8d8e0")
    for i, x in enumerate((150, 170, 190, 210)):
        body += rect(x, 110, 12, 50, "#e8f8ff", 'rx="6" stroke="#8ab0c0" stroke-width="2"') + rect(x + 2, 130, 8, 28, ["#6af0c0", "#f0c06a", "#c06af0", "#6ac0f0"][i], 'rx="4"')
    body += rect(250, 120, 60, 40, "#a0b0b8", 'rx="6"') + circle(280, 140, 14, "#d8e0e8")
    body += person(80, 200, 1.2, SKIN[0], "#ffffff", face="smile", glasses="round", hair="#8a6a4a")
    body += stroke("M108 160 L150 120", "#ffffff", 9) + rect(146, 96, 18, 24, "#fffbe0", 'rx="2" stroke="#ffd23a" stroke-width="2"')
    body += circle(155, 108, 18, "#fff8a0", 'opacity="0.35"')
    return svg(defs, body)


def drug_smuggler():
    # A sunburnt backpacker with dreadlocks grinning at an airport security gate, his enormous backpack bulging with cards
    # poking out of every zip
    defs, body = bg_vapor()
    body += rect(250, 40, 16, 160, "#c8c8d0") + rect(320, 40, 16, 160, "#c8c8d0") + rect(250, 36, 86, 12, "#c8c8d0")
    body += circle(293, 60, 6, "#e83a3a")
    body += rect(40, 70, 110, 120, "#3a6a3a", 'rx="20" stroke="#1a3a1a" stroke-width="3"')
    for x, y, a in [(56, 66, -20), (90, 60, 5), (124, 70, 25), (60, 120, -10), (128, 130, 15)]:
        body += rect(x, y, 16, 22, "#ffffff", f'rx="2" stroke="#e86ab0" stroke-width="2" transform="rotate({a} {x + 8} {y + 11})"')
    body += person(180, 200, 1.35, "#f0907a", "#e8c84a", face="grin")
    hx, hy = 180, 200 - 64 * 1.35
    for dx in (-30, -24, -18, 18, 24, 30):
        body += stroke(f"M{hx + dx} {hy - 16} L{hx + dx * 1.3} {hy + 30}", "#6a4a2a", 5)
    body += path(f"M{hx - 28} {hy - 4} C {hx - 28} {hy - 40} {hx + 28} {hy - 40} {hx + 28} {hy - 4} Z", "#6a4a2a")
    body += stroke("M150 150 L120 120", "#c8a040", 6)
    return svg(defs, body)


def human_intelligence():
    # Two spies on a park bench back to back, one reading a newspaper with eye holes cut in it, the other slipping a card
    # into his coat pocket behind the paper, pigeons watching
    defs, body = bg_shadow()
    body += rect(60, 150, 232, 10, "#6a4a2a") + rect(60, 130, 232, 8, "#6a4a2a") + rect(70, 160, 8, 30, "#3a2a1a") + rect(276, 160, 8, 30, "#3a2a1a")
    body += agent(120, 160, 1.0, face="flat", glasses=None, hat="fedora")
    body += rect(90, 80, 70, 56, "#f4f0e6", 'stroke="#aaa" stroke-width="1"') + circle(114, 98, 4, "#111") + circle(132, 98, 4, "#111")
    for y in (112, 120, 128):
        body += stroke(f"M96 {y} L154 {y}", "#999", 2)
    body += agent(232, 160, 1.0, face="smirk", glasses="shades", hat="fedora")
    body += rect(176, 120, 16, 22, "#ffffff", 'rx="2" stroke="#4a4a6a" stroke-width="2"') + stroke("M206 130 L190 128", SKIN[0], 6)
    for x in (30, 320):
        body += ellipse(x, 186, 12, 9, "#8a8a9a") + circle(x + 9, 178, 5, "#8a8a9a") + circle(x + 11, 177, 1.3, "#111")
    return svg(defs, body)


CHUNK = {"Corporate": {"Headhunter": headhunter}, "DigitalSociety": {"Doomscrolling": doomscrolling},
         "Mind": {"LucidDreaming": lucid_dreaming}, "Statecraft": {"SupplyDepot": supply_depot}, "Tech": {"WebScraping": web_scraping},
         "Utopia": {"ResearchLab": research_lab}, "Zeitgeist": {"DrugSmuggler": drug_smuggler}, "ShadowOps": {"HumanIntelligence": human_intelligence}}
for _theme, _cards in CHUNK.items():
    CARDS[_theme].update(_cards)


# ---------------------------------------------------------------- Holy cards, Science, HAARP's earthquake (2026-10-14)
def gods_grace():
    # A battered knight at 0 Health, an empty health bar over his head, held upright by a beam of light from parted clouds,
    # two cherubs propping him up by the armpits, a choir of angels singing behind
    defs, body = bg_myth()
    defs += lin("beam", [("#fff7c8", 0.85), ("#fff7c8", 0.15)])
    body += cloud(96, 16, 1.4, "#f4ecff", 0.9) + cloud(256, 16, 1.4, "#f4ecff", 0.9)
    body += poly([(150, 0), (202, 0), (240, 200), (112, 200)], "url(#beam)")
    for x in (52, 300):
        body += angel(x, 70, 0.55)
        body += stroke(f"M{x - 4} 56 q 4 -6 8 0", "#3a2a1a", 1.2)  # Singing
    body += knight(176, 204, 1.2)
    body += stroke("M150 160 L144 176 M202 160 L208 176", "#6a7078", 3)  # Dents
    body += rect(144, 50, 64, 12, "#2a1a1a", 'rx="3" stroke="#f4f0e6" stroke-width="2"')  # His health bar, empty
    body += lettering("0", 216, 50, 12, "#ff5a4a", width=2.4)
    for f in (-1, 1):
        body += angel(176 + f * 50, 142, 0.6)
        body += stroke(f"M{176 + f * 42} 146 L{176 + f * 30} 150", SKIN[0], 4)  # Hands under his arms
    return svg(defs, body)


def phoenix_feather():
    # Only the feather, watercolour on paper: red at the quill through orange to a yellow heart, its barbs licking out like
    # flames, a glowing eye near the top, the tip burning out lilac, sparks all round
    import random as _r
    rnd = _r.Random(7)
    defs = rad("paper", [("#fff8ec", 1), ("#f3e6d2", 1)], 0.5, 0.45, 0.75)
    defs += rad("warmth", [("#ffc070", 0.45), ("#ffc070", 0)])
    L = 270.0  # The feather's length, quill end at 0, tip at -L (local, pointing up)
    defs += (f'<linearGradient id="vane" gradientUnits="userSpaceOnUse" x1="0" y1="-30" x2="0" y2="{-L}">'
             f'{stops(["#b8161a", "#e8401c", "#ff7a1c", "#ffb02a", "#ff8a1c", "#e0381c", "#a0347a", "#8a5ac8"])}</linearGradient>')
    defs += (f'<linearGradient id="heart" gradientUnits="userSpaceOnUse" x1="0" y1="-60" x2="0" y2="{-L + 60}">'
             f'{stops([("#ffd84a", 0), ("#ffe36a", 0.95), ("#fff2a0", 1), ("#ffd04a", 0.8), ("#ff9a2a", 0)])}</linearGradient>')
    defs += (f'<linearGradient id="tipflame" gradientUnits="userSpaceOnUse" x1="0" y1="{-L + 50}" x2="0" y2="{-L - 30}">'
             f'{stops([("#c86ad0", 0.9), ("#b89ae8", 0.85), ("#e8d8f8", 0.6)])}</linearGradient>')
    defs += rad("eye", [("#ffffff", 1), ("#fff2a0", 1), ("#ffb02a", 1), ("#e8401c", 1)])
    body = rect(0, 0, W, H, "url(#paper)") + ellipse(186, 102, 150, 92, "url(#warmth)")

    def width(t):  # Half-width of the vane along it (t: 0 at the vane's base, 1 at the tip)
        return 38 * math.sin(math.pi * min(1.0, t * 0.92 + 0.05)) ** 0.8 + 4

    def bend(y):  # The rachis curves gently
        return 8 * math.sin(-y / L * math.pi)

    g = []
    base, top = -46.0, -L + 18
    # The vane: each side a run of flame tongues, licking outward and up
    for side in (-1, 1):
        pts = [(bend(base), base)]
        n = 10
        for k in range(n + 1):
            t = k / n
            y = base + (top - base) * t
            w = width(t)
            jitter = rnd.uniform(-3, 4)
            pts.append((bend(y) + side * (w * 0.72), y + 6))
            pts.append((bend(y) + side * (w + 14 + jitter), y - 16 - rnd.uniform(0, 6)))  # A tongue's point, swept up
        pts.append((bend(top), top - 6))
        d = f"M{pts[0][0]:.1f} {pts[0][1]:.1f} " + " ".join(
            f"Q {pts[i][0]:.1f} {pts[i][1]:.1f} {(pts[i][0] + pts[i + 1][0]) / 2:.1f} {(pts[i][1] + pts[i + 1][1]) / 2:.1f}"
            for i in range(1, len(pts) - 1)) + f" L{pts[-1][0]:.1f} {pts[-1][1]:.1f} Z"
        g.append(path(d, "url(#vane)", 'opacity="0.95"'))
    # Loose flames breaking off the edges
    for _ in range(12):
        t = rnd.uniform(0.15, 0.95)
        y = base + (top - base) * t
        side = rnd.choice((-1, 1))
        x0 = bend(y) + side * (width(t) + 6)
        tongue = f"M{x0:.1f} {y:.1f} q {side * 10:.1f} -6 {side * 8:.1f} -20 q {-side * 2:.1f} 10 {-side * 8:.1f} 14 Z"
        g.append(path(tongue, "#e8401c" if t < 0.75 else "#a85ab0", 'opacity="0.7"'))
    # The yellow heart of it
    heart = (f"M{bend(-70):.1f} -70 C {-26} -120 {-20} -190 {bend(-210):.1f} -212 C {22} -190 {28} -120 {bend(-70):.1f} -70 Z")
    g.append(path(heart, "url(#heart)"))
    # Barbs: fine lines from the shaft, swept up and out
    for k in range(26):
        t = 0.04 + k / 26 * 0.9
        y = base + (top - base) * t
        for side in (-1, 1):
            w = width(t) * rnd.uniform(0.7, 0.95)
            col = "#a8141a" if t < 0.3 else "#e8601c" if t < 0.75 else "#8a3a8a"
            g.append(stroke(f"M{bend(y):.1f} {y:.1f} q {side * w * 0.5:.1f} -6 {side * w:.1f} -18", col, 0.9, 'opacity="0.55"'))
    # The tip burning out lilac
    for k, (dx, h, wid) in enumerate([(-10, 46, 12), (6, 58, 14), (16, 38, 10), (-2, 70, 9)]):
        y0 = top + 16
        g.append(path(f"M{dx - wid:.1f} {y0:.1f} C {dx - wid:.1f} {y0 - h * 0.5:.1f} {dx + wid * 0.2:.1f} {y0 - h * 0.7:.1f} {dx + 3:.1f} {y0 - h:.1f} "
                      f"C {dx + wid * 0.6:.1f} {y0 - h * 0.6:.1f} {dx + wid:.1f} {y0 - h * 0.4:.1f} {dx + wid:.1f} {y0:.1f} Z",
                      "url(#tipflame)", f'opacity="{0.75 - k * 0.08:.2f}"'))
    # The eye near the top
    ey = -L * 0.72
    g.append(path(f"M{bend(ey):.1f} {ey + 22:.1f} C {bend(ey) - 14:.1f} {ey + 6:.1f} {bend(ey) - 10:.1f} {ey - 14:.1f} {bend(ey):.1f} {ey - 24:.1f} "
                  f"C {bend(ey) + 10:.1f} {ey - 14:.1f} {bend(ey) + 14:.1f} {ey + 6:.1f} {bend(ey):.1f} {ey + 22:.1f} Z",
                  "url(#eye)", 'stroke="#c8301a" stroke-width="2"'))
    g.append(circle(bend(ey), ey, 3.2, "#ffffff"))
    # Down at the base, wispy
    for k in range(22):
        side = -1 if k % 2 else 1
        y = -30 - k * 0.9
        w = rnd.uniform(14, 26)
        g.append(stroke(f"M{bend(y):.1f} {y:.1f} q {side * w * 0.6:.1f} {rnd.uniform(-8, -2):.1f} {side * w:.1f} {rnd.uniform(-14, -4):.1f}", "#e0401c", 1.0, 'opacity="0.45"'))
    # The shaft and the bare quill
    g.append(stroke(f"M0 0 C 1 -20 {bend(-120):.1f} -120 {bend(top + 10):.1f} {top + 10:.1f}", "#c8401c", 2.6))
    g.append(stroke(f"M0 0 C 1 -20 {bend(-120):.1f} -120 {bend(top + 10):.1f} {top + 10:.1f}", "#ffb06a", 0.9, 'opacity="0.8"'))
    # Sparkles on the vane
    for _ in range(40):
        t = rnd.uniform(0.1, 0.95)
        y = base + (top - base) * t
        x = bend(y) + rnd.uniform(-1, 1) * width(t) * 0.8
        g.append(circle(x, y, rnd.uniform(0.6, 1.4), "#fffbe8", f'opacity="{rnd.uniform(0.6, 1):.2f}"'))
    body += f'<g transform="translate(78 182) rotate(56)">{"".join(g)}</g>'
    # Sparks around it, red low and purple high, like flicked paint
    for _ in range(36):
        x, y = rnd.uniform(40, 330), rnd.uniform(10, 190)
        col = "#8a5ac8" if y < 70 or (x > 240 and y < 110) else "#e8401c"
        body += circle(x, y, rnd.uniform(0.8, 2.2), col, f'opacity="{rnd.uniform(0.35, 0.8):.2f}"')
    return svg(defs, body)


def evolution_theory():
    # Darwin in a lab coat at a chalkboard: the march of progress from ape to man to a huge muscled lab-grown superman, and
    # a tiny weak creature at the start of the line crossed out
    defs, body = bg_utopia()
    body += rect(14, 14, 258, 130, "#8a5a2a") + rect(20, 20, 246, 118, "#2a4a3a")
    chalk = "#f0f0e8"
    # The little runt, crossed out
    body += circle(42, 116, 6, "none", f'stroke="{chalk}" stroke-width="2"') + stroke("M42 122 L42 130 M38 130 l 4 -4 l 4 4", chalk, 2)
    body += stroke("M32 106 L54 132 M54 106 L32 132", "#ff6a5a", 3)
    # The ape, hunched
    body += circle(86, 92, 7, "none", f'stroke="{chalk}" stroke-width="2"') + stroke("M90 98 C 98 104 98 116 92 124 M92 124 L86 134 M92 124 L98 134 M94 104 L80 122", chalk, 2)
    # The man, upright
    body += circle(132, 76, 8, "none", f'stroke="{chalk}" stroke-width="2"') + stroke("M132 84 L132 112 M132 112 L124 134 M132 112 L140 134 M132 92 L122 106 M132 92 L142 106", chalk, 2)
    # The lab-grown superman, all muscle
    body += circle(196, 48, 11, "none", f'stroke="{chalk}" stroke-width="2.4"')
    body += path("M176 66 C 172 60 220 60 216 66 L212 104 L180 104 Z", "none", f'stroke="{chalk}" stroke-width="2.4"')
    body += path("M176 68 C 156 70 152 92 166 96 C 162 86 170 80 178 80 M216 68 C 236 70 240 92 226 96 C 230 86 222 80 214 80", "none", f'stroke="{chalk}" stroke-width="2.4"')
    body += stroke("M184 104 L178 134 M208 104 L214 134 M196 70 L196 100 M184 84 L208 84", chalk, 2.4)
    for x in (62, 108, 158):
        body += stroke(f"M{x} 120 l 10 0 m -4 -4 l 4 4 l -4 4", chalk, 1.6)
    # Darwin, pointing with his chalk
    body += person(306, 204, 1.15, SKIN[0], "#f4f4f4", face="smile", hair=OLD_HAIR, beard="#e8e8e8")
    body += stroke("M280 160 L254 118", "#f4f4f4", 9) + circle(252, 115, 5, SKIN[0]) + stroke("M250 112 L238 100", chalk, 3)
    return svg(defs, body)


def mutation():
    # A lab rat in a cage flexing three muscular arms, a scientist taking notes and quietly sliding a fourth arm onto the
    # clipboard
    defs, body = bg_utopia()
    body += rect(30, 60, 190, 112, "#f4f4f0", 'opacity="0.35"')
    # The rat, side on, its snout to the right
    rat, line = "#b8b8c0", 'stroke="#7a7a84" stroke-width="1.5"'
    body += path("M70 156 C 52 166 50 182 66 188", "none", 'stroke="#f0a0b0" stroke-width="4" stroke-linecap="round"')
    body += ellipse(112, 148, 46, 22, rat, line)
    body += path("M146 132 C 162 122 182 128 194 142 C 182 150 162 156 148 154 Z", rat, line)
    body += circle(152, 128, 9, rat, line) + circle(152, 128, 5, "#f0a0b0") + circle(172, 136, 2.6, "#111") + circle(194, 142, 3, "#f0a0b0")
    body += stroke("M194 142 l 12 -4 M194 144 l 12 2", "#555", 1)
    # Three arms raised in a double-biceps flex: upper arm out, forearm up, a fist, a bulging biceps
    for sx, sy, f in [(92, 134, -1), (114, 128, 1), (134, 132, 1)]:
        ex, ey = sx + f * 22, sy - 10
        fx, fy = ex + f * 2, ey - 26
        body += stroke(f"M{sx} {sy} L{ex} {ey} L{fx} {fy}", "#7a7a84", 11)
        body += stroke(f"M{sx} {sy} L{ex} {ey} L{fx} {fy}", rat, 8)
        body += ellipse((sx + ex) / 2, (sy + ey) / 2 - 5, 9, 7, rat, line)
        body += circle(fx, fy - 2, 6, "#f0a0b0", line)
    # The cage bars in front of it
    for i in range(9):
        body += stroke(f"M{36 + i * 22} 58 L{36 + i * 22} 172", "#8a9aa0", 2.4)
    body += rect(30, 54, 190, 6, "#8a9aa0") + rect(30, 170, 190, 6, "#8a9aa0")
    # The scientist with the clipboard, drawing in a fourth
    body += person(296, 204, 1.1, SKIN[1], "#f4f4f4", face="smirk", hair="#2a1a10", glasses="round")
    body += rect(252, 130, 34, 44, "#c89a5a", 'rx="2"') + rect(256, 136, 26, 34, "#ffffff")
    body += stroke("M262 160 C 262 146 274 142 276 150", "#c0392b", 2) + stroke("M260 144 l 6 0 M260 150 l 4 0", "#888", 1)
    return svg(defs, body)


def exponential_growth():
    # A petri dish overflowing onto the lab bench, the colony spilling over the edge and climbing up the wall, a scientist
    # backing away holding the lid
    defs, body = bg_utopia()
    body += rect(0, 146, W, 54, "#d8dcdc") + rect(0, 146, W, 6, "#b8c0c0")
    body += ellipse(130, 148, 46, 10, "#ffffff", 'stroke="#a8c8c8" stroke-width="2" opacity="0.8"')
    # The colony: a green mass welling out of the dish, spilling over the bench and climbing the wall
    import random as _r
    rng = _r.Random(7)
    blobs = []
    for i in range(140):
        t = rng.random()
        dist = (t ** 0.8) * 150
        ang = rng.uniform(0.08, 0.92) * math.pi
        x = 130 - math.cos(ang) * dist * 1.1
        y = 142 - math.sin(ang) * dist * 0.85
        blobs.append((dist, x, y, max(4, 15 - dist * 0.07)))
    for dist, x, y, r in sorted(blobs, reverse=True):
        body += circle(x, y, r, ["#3aa84a", "#5ac85a", "#7ad86a"][int(x + y) % 3])
        body += circle(x - r * 0.3, y - r * 0.35, r * 0.3, "#c8f0b0", 'opacity="0.7"')
    body += ellipse(130, 148, 46, 8, "none", 'stroke="#ffffff" stroke-width="2" opacity="0.9"')
    # The scientist backing off, the lid held up like a shield
    body += person(300, 204, 1.05, SKIN[2], "#f4f4f4", face="shout", hair="#1a1a1a", glasses="round")
    body += ellipse(262, 130, 8, 26, "#ffffff", 'stroke="#a8c8c8" stroke-width="2" opacity="0.85"') + stroke("M270 150 L284 158", "#f4f4f4", 8)
    body += sweat(322, 118, 1.0)
    return svg(defs, body)


def scientific_method():
    # A scientist in goggles carefully pouring a glowing liquid into a beaker on a crowded bench, a whiteboard behind her:
    # HYPOTHESIS, EXPERIMENT, GRANT
    defs, body = bg_utopia()
    body += rect(166, 14, 176, 96, "#ffffff", 'stroke="#a8a8a8" stroke-width="3"')
    body += lettering("HYPOTHESIS", 254, 24, 10, "#2a5ad8", width=1.6)
    body += lettering("EXPERIMENT", 254, 52, 10, "#2a5ad8", width=1.6)
    body += lettering("GRANT", 254, 80, 13, "#1a9a4a", width=2.2)
    for y in (40, 68):
        body += stroke(f"M254 {y} l 0 8 m -4 -4 l 4 4 l 4 -4", "#c0392b", 2)
    body += rect(0, 150, W, 50, "#a8b8b8") + rect(0, 150, W, 5, "#889898")
    for x, h, c in [(200, 26, "#e85a8a"), (226, 36, "#5ac8e8"), (256, 22, "#f0c040"), (286, 30, "#9a6ad8"), (316, 24, "#5ad89a")]:
        body += rect(x, 150 - h, 16, h, "#e8f4f4", 'rx="3" stroke="#a8c0c0" stroke-width="1.5" opacity="0.9"') + rect(x + 2, 150 - h * 0.55, 12, h * 0.55 - 2, c, 'opacity="0.8"')
    body += person(84, 204, 1.25, SKIN[0], "#f4f4f4", face="flat", hat="bob")
    body += rect(68, 116, 34, 9, "#3a6a8a", 'rx="4" opacity="0.85"')  # Goggles
    # The flask tipped over the beaker, the glowing drip
    body += stroke("M110 160 L136 116", "#f4f4f4", 10)
    body += path("M134 100 L150 92 L162 112 L148 122 Z", "#e8f4f4", 'stroke="#a8c0c0" stroke-width="1.5"') + path("M150 104 L160 110 L150 118 Z", "#7aff6a")
    body += stroke("M160 116 C 164 124 166 128 166 134", "#7aff6a", 3) + circle(166, 134, 14, "#c8ff9a", 'opacity="0.35"')
    body += path("M152 134 L180 134 L184 150 L148 150 Z", "#e8f4f4", 'stroke="#a8c0c0" stroke-width="1.5"') + rect(151, 142, 32, 7, "#7aff6a", 'opacity="0.8"')
    return svg(defs, body)


def frankenstein():
    # A towering stitched-together monster sitting up on a lab table under a lightning bolt, bolts in its neck smoking, the
    # mad scientist cheering with both arms up
    defs = lin("bg", ["#141a2a", "#3a3a5a"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 0, W, 10, "#2a2a3a")
    body += bolt(116, 0, 1.8) + bolt(214, 6, 1.2)
    body += rect(40, 160, 220, 14, "#5a5a6a") + rect(52, 174, 10, 26, "#3a3a4a") + rect(238, 174, 10, 26, "#3a3a4a")
    green = "#7aa86a"
    # The monster sitting up, arms out in front
    body += path("M88 162 C 88 118 196 118 196 162 Z", "#2a2a2a")
    body += stroke("M180 128 L238 120 M176 140 L236 134", green, 12) + stroke("M186 124 l 0 8 M206 122 l 0 8 M226 120 l 0 8", "#2a3a2a", 1.5)
    body += rect(126, 104, 32, 22, green)
    body += rect(112, 46, 60, 66, green, 'rx="8"') + rect(110, 40, 64, 14, "#1a1a1a", 'rx="3"')
    for f in (-1, 1):
        body += rect(142 + f * 40 - 5, 100, 10, 8, "#8a8a8a") + circle(142 + f * 46, 98, 5, "#a8a8a8", 'opacity="0.4"')
    body += stroke("M124 74 l 12 0 M148 74 l 12 0", "#1a1a1a", 3) + stroke("M128 96 l 28 0", "#1a1a1a", 2.4)
    body += stroke("M120 60 L164 60 M126 56 l 0 8 M136 56 l 0 8 M146 56 l 0 8 M156 56 l 0 8", "#2a3a2a", 1.5)
    # The mad scientist, cheering
    body += person(300, 204, 0.95, SKIN[0], "#f4f4f4", face="grin", hair=OLD_HAIR, glasses="round")
    body += stroke("M276 160 L262 118 M324 160 L338 118", "#f4f4f4", 8) + circle(262, 114, 5, SKIN[0]) + circle(338, 114, 5, SKIN[0])
    body += lettering("ALIVE!", 300, 20, 13, "#ffe45c", width=2.2)
    return svg(defs, body)


def chimera():
    # A lion with a goat's head out of its back and a snake for a tail, all three in hospital ID wristbands, in a lab
    # enclosure, a sticky note on the glass: DO NOT FEED AFTER MIDNIGHT
    defs, body = bg_utopia()
    body += rect(0, 168, W, 32, "#b8d0c8")
    tan, mane = "#d8a050", "#a8601a"
    # Snake tail
    body += path("M236 140 C 270 150 280 120 300 112 C 316 106 322 120 312 126", "none", 'stroke="#4a9a4a" stroke-width="9" stroke-linecap="round"')
    body += ellipse(316, 122, 9, 6, "#4a9a4a") + circle(318, 120, 1.5, "#111") + stroke("M324 124 l 8 2 m -3 -2 l 3 2 l -3 1", "#c0392b", 1.4)
    # Body and legs
    for x in (124, 148, 210, 232):
        body += rect(x - 7, 150, 14, 26, tan, 'rx="4"')
    body += ellipse(178, 138, 66, 28, tan)
    body += rect(140, 162, 16, 6, "#ffffff", 'stroke="#8ab0d8" stroke-width="1"')  # Wristband on a leg
    # The goat head out of its back
    body += rect(186, 92, 14, 26, "#e8e4dc") + ellipse(196, 86, 14, 11, "#e8e4dc")
    body += path("M188 78 C 180 64 192 60 194 76 M202 76 C 206 60 218 64 208 80", "none", 'stroke="#8a7a6a" stroke-width="3"')
    body += circle(200, 84, 2, "#111") + path("M206 92 L212 100 L202 96 Z", "#c8c0b0")
    body += rect(186, 104, 14, 5, "#ffffff", 'stroke="#8ab0d8" stroke-width="1"')
    # The lion's head and mane
    body += circle(106, 116, 30, mane) + circle(102, 118, 20, tan)
    body += circle(94, 112, 2.6, "#111") + circle(108, 112, 2.6, "#111") + ellipse(100, 124, 5, 3.4, "#5a3a2a")
    body += stroke("M94 130 q 6 4 12 0", "#5a3a2a", 1.6)
    # The sticky note
    body += rect(258, 20, 78, 56, "#ffe45c", 'transform="rotate(4 297 48)"')
    body += lettering("DO NOT", 297, 28, 9, "#c0392b", width=1.6) + lettering("FEED", 297, 44, 9, "#c0392b", width=1.6)
    body += lettering("MIDNIGHT", 297, 60, 7, "#5a3a2a", width=1.3)
    return svg(defs, body)


def resurrected_t_rex():
    # A T-Rex skeleton half covered in new flesh breaking out of its museum display, the DNA sequencer that brought it back
    # still beeping, a guide pointing at the exit
    defs = lin("bg", ["#e8dcc4", "#c8b898"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 168, W, 32, "#8a6a4a")
    body += rect(20, 150, 210, 18, "#a8a098") + rect(24, 146, 202, 6, "#c8c0b8")  # The plinth
    flesh, bone = "#5a9a4a", "#f4ecd8"
    # The head: the back half bone, the front half new flesh
    body += path("M60 70 C 70 40 150 34 196 50 L200 70 C 160 76 120 82 64 90 Z", flesh)
    body += path("M64 90 C 110 86 160 82 198 78 L196 96 C 160 100 110 104 70 104 Z", flesh)
    for i in range(9):
        x = 150 + i * 5.5
        body += poly([(x, 70 + i * 0.6), (x + 3, 82), (x + 6, 70 + i * 0.6)], "#ffffff")
    body += path("M60 70 C 66 52 96 44 120 46 L118 90 L64 92 Z", bone, 'stroke="#c8b898" stroke-width="2"')
    body += circle(92, 64, 9, "#3a3a3a") + circle(160, 56, 5, "#ffe45c") + circle(160, 56, 2.2, "#111")
    # Neck and spine of bone, the body still skeleton
    body += stroke("M66 96 C 60 116 66 132 90 140 L190 132", bone, 7)
    for i in range(6):
        body += stroke(f"M{100 + i * 16} 138 l -4 12", bone, 3.4)
    body += stroke("M196 132 C 210 128 226 116 238 96", bone, 6)  # The tail
    body += stroke("M118 140 l -6 12 M112 152 l 8 0", bone, 4)
    # Cracks in the glass case it broke out of
    body += stroke("M30 30 L230 30 L230 146 M30 30 L30 146", "#a8c8d8", 2, 'opacity="0.6"')
    body += stroke("M200 30 l 10 20 l -6 14 l 14 20 M210 50 l 16 -6", "#ffffff", 1.6)
    # The DNA sequencer beeping
    body += rect(246, 112, 46, 40, "#5a6070", 'rx="3"') + rect(252, 118, 34, 16, "#1a2a1a") + stroke("M254 126 l 6 0 l 3 -6 l 3 12 l 3 -6 l 12 0", "#5aff5a", 1.4)
    body += stroke("M294 112 l 6 -8 M296 118 l 9 -3", "#c0392b", 1.6)
    # The guide, pointing at the exit
    body += rect(290, 16, 50, 20, "#1a9a4a", 'rx="2"') + lettering("EXIT", 315, 21, 10, "#ffffff", width=1.8)
    body += person(312, 204, 0.9, SKIN[1], "#c0392b", face="shout", hair="#3a2a1a")
    body += stroke("M330 164 L330 44", "#c0392b", 7) + circle(330, 40, 4, SKIN[1])
    return svg(defs, body)


def haarp():
    # Deep under the antenna field, an installation in a cut-away of the ground: a huge laser on a gantry fires straight
    # down an immense shaft toward the glowing core of the Earth, the ground cracking in zigzags around it, a technician with
    # his coffee at the console
    defs = lin("sky", ["#2a1a4a", "#6a4a8a"]) + rad("core", [("#fff2a0", 1), ("#ff8a1a", 0.9), ("#c0341a", 0)])
    defs += lin("beam", [("#ff4a4a", 1), ("#ff2a2a", 0.85)])
    body = rect(0, 0, W, 26, "url(#sky)")
    for i, (y, c) in enumerate([(26, "#6a5a3a"), (60, "#5a4630"), (100, "#4a3626"), (140, "#3a281c")]):
        body += rect(0, y, W, 200 - y, c)
    body += rect(0, 22, W, 6, "#7aa05a")
    for x in range(20, 340, 22):  # The antenna field on the surface
        body += stroke(f"M{x} 22 L{x} 9 M{x - 6} 3 L{x} 9 L{x + 6} 3", "#c8c8d8", 1.4) + circle(x, 9, 1.6, "#ff6a6a")
    body += circle(176, 236, 110, "url(#core)")
    # The shaft, an immense hole down to the core
    body += path("M146 92 L206 92 L232 200 L120 200 Z", "#140c08")
    # The installation: a concrete hall, the gantry and the laser pointing down the shaft
    body += rect(40, 36, 272, 58, "#8a8a90", 'stroke="#5a5a60" stroke-width="2"') + rect(46, 42, 260, 46, "#a8a8b0")
    body += rect(150, 42, 52, 10, "#5a5a60") + rect(160, 52, 32, 26, "#3a3a4a", 'rx="4"') + rect(168, 78, 16, 14, "#2a2a3a")
    body += circle(176, 92, 6, "#ff6a6a")
    body += rect(171, 92, 10, 108, "url(#beam)") + rect(166, 92, 20, 108, "#ff4a4a", 'opacity="0.25"')
    for y in (120, 150, 180):
        body += circle(176, y, 9, "#ffb0b0", 'opacity="0.35"')
    # Zigzag cracks in the rock around the shaft: the earthquake
    for f in (-1, 1):
        x0 = 176 + f * 44
        body += stroke(f"M{x0} 120 l {f * 18} -10 l {f * 6} 14 l {f * 20} -12 l {f * 8} 16 l {f * 22} -10", "#ffb03a", 2)
        body += stroke(f"M{x0 + f * 10} 160 l {f * 16} 8 l {f * 6} -14 l {f * 22} 10 l {f * 8} -12", "#ffb03a", 2)
    # The console and the technician with his coffee
    body += rect(62, 66, 40, 22, "#3a3a4a", 'rx="2"') + circle(72, 74, 3, "#3ad04a") + circle(84, 74, 3, "#d0342c") + rect(66, 80, 30, 4, "#5ab0ff")
    body += person(120, 94, 0.5, SKIN[0], "#c0563a", face="smile", hat="hood")
    body += rect(128, 74, 6, 7, "#f4f4f4", 'rx="1"') + stroke("M131 72 q 2 -4 0 -7", "#ffffff", 1)
    return svg(defs, body)


CHUNK = {"Myth": {"GodsGrace": gods_grace, "PhoenixFeather": phoenix_feather},
         "Utopia": {"EvolutionTheory": evolution_theory, "Mutation": mutation, "ExponentialGrowth": exponential_growth,
                    "ScientificMethod": scientific_method, "Frankenstein": frankenstein, "Chimera": chimera,
                    "ResurrectedTRex": resurrected_t_rex},
         "ShadowOps": {"HAARP": haarp}}
for _theme, _cards in CHUNK.items():
    CARDS[_theme].update(_cards)


# ---------------------------------------------------------------- Utopia's accelerationism (2026-10-14)
def circuit_piece(x, y, w, h, angle=0):
    out = rect(x, y, w, h, "#1f7a4a", 'stroke="#0f4a2a" stroke-width="1.5" rx="2"')
    out += stroke(f"M{x + 4} {y + h / 2} l {w * 0.4} 0 l 4 -4 l {w * 0.3} 0", "#f2c94c", 1.4)
    out += circle(x + w - 6, y + h / 2 - 4, 2, "#f2c94c") + circle(x + 6, y + h / 2, 1.6, "#f2c94c")
    return group(out, f"rotate({angle} {x + w / 2} {y + h / 2})") if angle else out


def accelerationism():
    # A tech founder in a hoodie flooring a rocket car toward a cliff edge, the road on fire behind him, a bridge of glowing
    # circuit boards assembling itself in mid-air just ahead of the wheels
    defs = lin("bg", ["#2a1030", "#e8602a"])
    body = rect(0, 0, W, H, "url(#bg)") + circle(300, 60, 26, "#ffd84a", 'opacity="0.8"')
    body += path("M0 150 L224 150 L232 200 L0 200 Z", "#4a3a3a") + rect(0, 146, 224, 6, "#2a2a2a")
    body += path("M232 200 L240 172 L352 176 L352 200 Z", "#1a0a10")  # The chasm's far side, far below
    body += flames(40, 148, 4, 22, 26)
    # The bridge of circuit boards, the near pieces in place, the far ones still flying in
    for i, (x, y, a) in enumerate([(224, 146, 0), (252, 146, 0), (280, 142, -8), (306, 128, -20), (322, 104, 25)]):
        body += circuit_piece(x, y, 28, 10, a)
        if i >= 2:
            body += speed_lines(x + 30, y - 4, 2, 10, "#9affc0")
    # The rocket car
    body += rect(112, 116, 86, 26, "#d0342c", 'rx="8"') + path("M132 116 L146 96 L178 96 L190 116 Z", "#d0342c")
    body += path("M150 100 L176 100 L184 114 L144 114 Z", "#9ad8ff", 'opacity="0.8"')
    body += circle(132, 144, 10, "#1a1a1a") + circle(182, 144, 10, "#1a1a1a") + circle(132, 144, 4, "#8a8a8a") + circle(182, 144, 4, "#8a8a8a")
    body += rect(96, 120, 18, 14, "#5a5a6a", 'rx="2"') + path("M96 122 L60 116 L72 127 L58 138 L96 132 Z", "#ffb03a") + path("M96 124 L76 122 L84 127 L76 132 L96 130 Z", "#fff2a0")
    # The founder behind the wheel, in a grey hoodie, grinning
    body += circle(164, 104, 9, SKIN[0]) + path("M154 104 C 154 88 174 88 174 104 L174 98 C 170 92 158 92 154 98 Z", "#8a8a96")
    body += stroke("M160 108 q 4 3 8 0", "#222", 1.4) + circle(161, 102, 1.2, "#222") + circle(167, 102, 1.2, "#222")
    return svg(defs, body)


def cold_fusion():
    # A tabletop fusion reactor glowing white-hot in a jar on a garage workbench, the warning gauge in the red, two engineers
    # in sunglasses giving a thumbs-up
    defs = lin("bg", ["#3a3a44", "#1e1e26"]) + rad("hot", [("#ffffff", 1), ("#fff2a0", 0.9), ("#ff8a1a", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in range(10, 352, 40):  # Pegboard
        for y in range(14, 120, 26):
            body += circle(x, y, 2, "#2a2a32")
    body += rect(0, 150, W, 50, "#7a5a3a") + rect(0, 150, W, 6, "#5a4028")
    body += circle(176, 104, 70, "url(#hot)")
    body += rect(150, 70, 52, 80, "#d8eef8", 'rx="10" opacity="0.35" stroke="#ffffff" stroke-width="2"')
    body += rect(146, 64, 60, 10, "#8a8a96", 'rx="3"') + rect(146, 146, 60, 8, "#8a8a96", 'rx="3"')
    body += circle(176, 108, 16, "#ffffff") + circle(176, 108, 26, "#fff2a0", 'opacity="0.6"')
    for a in range(0, 360, 45):
        r1, r2 = 20, 32
        body += stroke(f"M{176 + r1 * math.cos(math.radians(a)):.1f} {108 + r1 * math.sin(math.radians(a)):.1f} L{176 + r2 * math.cos(math.radians(a)):.1f} {108 + r2 * math.sin(math.radians(a)):.1f}", "#ffffff", 2, 'opacity="0.8"')
    # The gauge, needle in the red
    body += circle(240, 118, 18, "#f4f4f4", 'stroke="#555" stroke-width="2"')
    body += path("M226 118 A 14 14 0 0 1 254 118 L240 118 Z", "#e8e8e8") + path("M248 106 A 14 14 0 0 1 254 118 L240 118 Z", "#d0342c")
    body += stroke("M240 118 L252 110", "#111", 2)
    # Two engineers in sunglasses, thumbs up
    for x, skin, shirt in [(56, SKIN[1], "#3d5a80"), (310, SKIN[2], "#c0563a")]:
        body += person(x, 204, 0.9, skin, shirt, face="grin", glasses="shades")
        body += rect(x + (24 if x < 176 else -34), 136, 10, 14, skin, 'rx="3"') + rect(x + (26 if x < 176 else -32), 128, 5, 10, skin, 'rx="2"')
    return svg(defs, body)


def antimatter():
    # A tiny shimmering particle in a magnetic bottle, its mirror image annihilating the lab bench around it, a physicist
    # ducking behind a clipboard
    defs, body = bg_utopia()
    defs += rad("flash", [("#ffffff", 1), ("#c0a0ff", 0.7), ("#c0a0ff", 0)])
    body += rect(0, 146, W, 54, "#a8b8b8") + rect(0, 146, W, 5, "#889898")
    # A jagged bite out of the bench into nothing, crackling violet
    body += circle(220, 150, 40, "url(#flash)")
    body += path("M186 146 L194 156 L190 166 L202 172 L214 168 L224 178 L236 168 L248 170 L246 158 L256 146 Z", "#1a0a2a")
    for a in range(200, 340, 28):
        body += stroke(f"M{220 + 34 * math.cos(math.radians(a)):.1f} {150 + 34 * math.sin(math.radians(a)):.1f} l {8 * math.cos(math.radians(a)):.1f} {8 * math.sin(math.radians(a)):.1f}", "#a070ff", 2)
    # The magnetic bottle on the bench: a glass capsule between two coils, the particle glowing in the middle
    body += rect(86, 96, 74, 40, "#d8eef8", 'rx="20" opacity="0.5" stroke="#ffffff" stroke-width="2"')
    for x in (84, 152):
        body += rect(x, 90, 10, 52, "#b8782a", 'rx="3"') + stroke(f"M{x} 98 l 10 0 M{x} 106 l 10 0 M{x} 114 l 10 0 M{x} 122 l 10 0 M{x} 130 l 10 0", "#8a5a1a", 1.2)
    body += circle(123, 116, 9, "#c0a0ff", 'opacity="0.5"') + circle(123, 116, 4, "#ffffff")
    body += rect(96, 136, 54, 10, "#5a6070")
    # Its mirror image, a dark twin out on the bench, eating it
    body += circle(220, 160, 4, "#ffffff") + circle(220, 160, 8, "none", 'stroke="#a070ff" stroke-width="1.5"')
    # The physicist ducking behind his clipboard
    body += person(312, 200, 0.9, SKIN[0], "#f4f4f4", face="shout", hair="#3a2a1a", glasses="round")
    body += rect(276, 140, 42, 52, "#c89a5a", 'rx="3"') + rect(281, 146, 32, 40, "#ffffff") + stroke("M286 156 l 22 0 M286 164 l 18 0 M286 172 l 20 0", "#888", 1.2)
    body += sweat(334, 116, 0.9)
    return svg(defs, body)


def biohacking():
    # A shirtless tech bro in a garage strapped to car batteries, LED implants glowing under his skin, a smoothie in one hand
    # and a soldering iron in the other
    defs = lin("bg", ["#3a3a44", "#22222a"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 168, W, 32, "#5a5a60")
    body += rect(20, 20, 90, 40, "#2a2a32", 'rx="3"') + lettering("NO LIMITS", 65, 33, 9, "#5affc8", width=1.6)
    # Car batteries, cables clamped to him
    for x in (40, 272):
        body += rect(x, 132, 44, 36, "#1a1a1a", 'rx="3"') + rect(x + 6, 126, 8, 6, "#d0342c") + rect(x + 30, 126, 8, 6, "#3a3a3a")
        body += lettering("12V", x + 22, 144, 8, "#f2c94c", width=1.4)
    body += path("M50 126 C 60 80 120 110 140 128", "none", 'stroke="#d0342c" stroke-width="3"')
    body += path("M306 126 C 300 80 240 110 214 128", "none", 'stroke="#2a2a2a" stroke-width="3"')
    # The tech bro, shirtless, LEDs under his skin
    body += person(176, 204, 1.3, SKIN[0], SKIN[0], face="grin", hair="#6a4a2a", glasses="shades")
    body += stroke("M160 162 q 6 4 12 0 M180 162 q 6 4 12 0 M170 176 l 0 14 M182 176 l 0 14", "#c89a7a", 1.6)
    for x, y in [(152, 150), (200, 150), (166, 184), (190, 184)]:
        body += circle(x, y, 3, "#5affff") + circle(x, y, 7, "#5affff", 'opacity="0.3"')
    body += stroke("M140 128 l -4 -4 M214 128 l 4 -4", "#f2c94c", 2)  # The clamps
    # A green smoothie and a soldering iron, held up
    body += stroke("M136 170 L110 120", SKIN[0], 10) + rect(98, 92, 22, 30, "#7ad86a", 'rx="3"') + rect(106, 80, 3, 14, "#f4f4f4")
    body += stroke("M216 170 L246 118", SKIN[0], 10) + rect(240, 96, 12, 24, "#3a6aa8", 'rx="3"') + stroke("M246 96 L252 78", "#c8c8c8", 3)
    body += circle(253, 76, 3, "#ffb03a") + stroke("M256 72 q 4 -6 0 -12", "#d8d8d8", 1.2)
    return svg(defs, body)


def blood_transfusion():
    # An old billionaire in a recliner hooked up to an IV bag labelled TEEN, reading the stock prices, his skin twenty years
    # younger
    defs = lin("bg", ["#f4ecd8", "#d8c8a8"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 170, W, 30, "#8a5a3a")
    body += rect(250, 18, 80, 60, "#d4af37", 'rx="2"') + rect(256, 24, 68, 48, "#6a8aa8")  # A painting of his yacht
    body += path("M268 58 L312 58 L304 66 L276 66 Z", "#ffffff") + stroke("M290 58 L290 34", "#ffffff", 2)
    # The IV stand and the bag
    body += stroke("M96 186 L96 30 M80 30 L112 30", "#a8a8b0", 3) + stroke("M86 190 l 10 -6 l 10 6", "#a8a8b0", 3)
    body += rect(78, 34, 30, 42, "#c0282a", 'rx="6" opacity="0.9"') + rect(82, 46, 22, 12, "#ffffff")
    body += lettering("TEEN", 93, 48, 8, "#c0282a", width=1.4)
    body += path("M93 76 C 92 110 120 120 150 128", "none", 'stroke="#c0282a" stroke-width="2"')
    # The recliner and the old man in it, white hair, smooth young skin
    body += path("M120 176 L130 104 C 134 92 154 92 156 104 L160 140 L250 140 C 262 140 266 152 256 160 L240 176 Z", "#5a3a6a")
    body += person(182, 166, 1.0, SKIN[0], "#2a2a4a", face="smile", hair=OLD_HAIR)
    body += stroke("M160 146 L148 128", "#2a2a4a", 8) + circle(147, 127, 4, SKIN[0])
    # The stock prices on his tablet, up and up
    body += rect(200, 108, 44, 30, "#1a1a1a", 'rx="3"') + stroke("M204 132 l 8 -6 l 8 3 l 8 -10 l 10 -6", "#3ad04a", 2)
    body += stroke("M196 146 L206 130", "#2a2a4a", 7)
    return svg(defs, body)


CHUNK = {"Utopia": {"Accelerationism": accelerationism, "ColdFusion": cold_fusion, "Antimatter": antimatter,
                    "Biohacking": biohacking, "BloodTransfusion": blood_transfusion}}
for _theme, _cards in CHUNK.items():
    CARDS[_theme].update(_cards)


# ---------------------------------------------------------------- The cards still without art (the user, 2026-10-16)
def business_operating_expenses():
    # A smiling executive in an orange jumpsuit with a silk tie, checking into a cushy white-collar prison camp with a
    # tennis court and a golf green, an assistant wheeling his luggage behind him; a sign over the gate reads COST OF
    # DOING BUSINESS
    defs, body = bg_sky("#9fd8f2", "#e8f4d8")
    body += rect(0, 150, W, 50, "#6ab04a") + ellipse(300, 168, 40, 10, "#8ad06a") + stroke("M300 168 L300 130", "#ffffff", 2)
    body += poly([(300, 130), (318, 136), (300, 142)], "#e83a3a")
    body += rect(196, 140, 80, 20, "#3a8ad0") + stroke("M236 140 L236 160", "#ffffff", 2)  # The tennis court
    body += rect(20, 40, 8, 120, "#8a8a8a") + rect(160, 40, 8, 120, "#8a8a8a") + rect(20, 34, 148, 26, "#f4ecd8", 'stroke="#555" stroke-width="2"')
    body += lettering("COST OF DOING", 94, 38, 7, "#3a3a3a") + lettering("BUSINESS", 94, 49, 7, "#3a3a3a")
    body += person(94, 196, 1.2, SKIN[0], "#ff8a1a", face="grin", hair=OLD_HAIR, tie="#c8a020")
    body += person(176, 196, 0.9, SKIN[2], "#3a3a5a", face="flat", hair="#1a1a1a")
    body += rect(150, 150, 22, 30, "#6a3a2a", 'rx="3"') + rect(172, 158, 22, 24, "#6a3a2a", 'rx="3"')
    body += circle(156, 184, 4, "#222") + circle(188, 186, 4, "#222")
    return svg(defs, body)


def c_suite():
    # A champagne tower of glasses: five executives in suits at the top glass, toasting, the champagne overflowing past
    # them down the tiers, until a line of workers at the bottom catch the last drips in paper cups
    defs, body = bg_office()
    def glass(x, y, s=1.0):
        return poly([(x - 14 * s, y), (x + 14 * s, y), (x + 4 * s, y + 16 * s), (x - 4 * s, y + 16 * s)], "#f8e8a0", 'stroke="#c8b060" stroke-width="1.5" opacity="0.9"') + rect(x - 1.5 * s, y + 16 * s, 3 * s, 8 * s, "#e8e8e8")
    for row, count in enumerate((1, 2, 3, 4)):
        for i in range(count):
            body += glass(176 + (i - (count - 1) / 2) * 32, 40 + row * 26)
    for i, x in enumerate((128, 152, 176, 200, 224)):
        body += person(x, 46, 0.42, SKIN[i % 2], "#1e2635", face="grin", tie="#c03a3a")
    for x in (120, 232):
        body += stroke(f"M{x} 70 C {x} 110 {x} 140 {x} 170", "#f8d870", 3, 'opacity="0.8"')
    for i, x in enumerate((70, 120, 232, 282)):
        body += person(x, 200, 0.7, SKIN[(i + 1) % 4], "#8a8a8a", face="frown", brows="sad")
        body += rect(x + 10, 160, 10, 12, "#ffffff", 'stroke="#aaa" stroke-width="1"')
    return svg(defs, body)


def golden_parachute():
    # A grinning CEO floating down on a golden parachute stitched from banknotes, a briefcase overflowing with cash in
    # hand, while the company tower he leaves behind burns and the staff watch from the windows
    defs, body = bg_sky("#f0a060", "#f8e0b0", sun=False)
    body += rect(250, 40, 70, 160, "#5a6070") + flames(285, 44, n=4, spread=18, h=34)
    for row in range(5):
        for col in range(3):
            body += rect(258 + col * 20, 60 + row * 26, 12, 14, "#ffd890")
            if (row + col) % 2 == 0:
                body += circle(264 + col * 20, 68 + row * 26, 3, "#3a3a3a")
    body += path("M60 70 C 70 10 190 10 200 70 Z", "#f2c94c", 'stroke="#c9952a" stroke-width="2"')
    for x in (90, 130, 170):
        body += rect(x - 10, 34, 20, 12, "#6ab04a", 'opacity="0.85"')
    for x in (62, 100, 160, 198):
        body += stroke(f"M{x} 70 L130 120", "#8a6a2a", 1.5)
    body += person(130, 168, 0.9, SKIN[0], "#1e2635", face="grin", tie="#d4af37")
    body += rect(150, 150, 30, 20, "#4a2a1a", 'rx="2"')
    for x, y in ((154, 146), (162, 142), (172, 147)):
        body += rect(x, y, 10, 6, "#6ab04a")
    return svg(defs, body)


def laissez_faire():
    # A boxing referee lounging in a deck chair in the corner of the ring, reading the business pages, whistle unused,
    # while a giant in a top hat sits on a heap of flattened small boxers and pockets their belts
    defs = lin("bg", ["#2a2a3a", "#4a4a5a"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 140, W, 60, "#d8c8a0")
    for y in (90, 108, 126):
        body += stroke(f"M10 {y} L342 {y}", "#c03a3a", 3)
    body += rect(6, 80, 10, 70, "#8a8a8a") + rect(336, 80, 10, 70, "#8a8a8a")
    # The referee in his deck chair, the paper up
    body += poly([(36, 176), (70, 130), (100, 176)], "none", 'stroke="#6a4a2a" stroke-width="4"') + rect(40, 150, 56, 8, "#3a8ad0")
    body += person(66, 160, 0.7, SKIN[0], "#ffffff", face="closed")
    for i in range(4):
        body += stroke(f"M{56 + i * 4} 130 L{56 + i * 4} 160", "#1a1a1a", 1.5)
    body += rect(74, 116, 34, 26, "#f4f0e6", 'stroke="#999" stroke-width="1"')
    # The giant on the heap
    for x, y in ((210, 182), (250, 186), (290, 180), (230, 170), (270, 172)):
        body += ellipse(x, y, 26, 8, "#e85a5a") + circle(x - 20, y - 4, 6, SKIN[1])
    body += person(250, 168, 1.6, SKIN[0], "#1a1a1a", face="smirk", hat="tophat")
    for i, x in enumerate((300, 316)):
        body += rect(x, 120 + i * 10, 26, 8, "#f2c94c", 'stroke="#c9952a" stroke-width="1"')
    return svg(defs, body)


def peace_the_frontier():
    # Two tech executives in hoodies at a congressional hearing table, one tearfully holding up a sign reading REGULATE US
    # (ONLY), the other quietly sliding a rulebook titled BARRIERS TO ENTRY across to a smiling senator, a burning pile of
    # cash under the table
    defs, body = bg_state("#3a5a8a")
    body += rect(0, 130, W, 70, "#6a4a2a") + rect(0, 126, W, 8, "#8a6a3a")
    body += person(70, 130, 0.9, SKIN[0], "#4a4a5a", face="frown", hat="hood", brows="sad")
    body += sweat(80, 58, 0.8) + sweat(60, 62, 0.8)
    body += rect(28, 20, 90, 34, "#fff7e0", 'stroke="#333" stroke-width="2"') + stroke("M40 54 L44 80 M106 54 L102 80", SKIN[0], 5)
    body += lettering("REGULATE", 73, 24, 8, "#c0392b") + lettering("US ONLY", 73, 38, 8, "#c0392b")
    body += person(160, 130, 0.9, SKIN[1], "#2a2a3a", face="smirk", hat="hood")
    body += rect(186, 112, 50, 16, "#2a4a8a", 'stroke="#1a2a5a" stroke-width="2"') + lettering("BARRIERS", 211, 115, 5, "#ffffff")
    body += person(290, 130, 1.0, SKIN[0], "#1e2635", face="grin", hair=OLD_HAIR, tie="#c03a3a")
    body += rect(140, 160, 70, 18, "#6ab04a") + flames(175, 166, n=4, spread=16, h=26)
    return svg(defs, body)


def satoshi_nakamoto():
    # A figure in a grey hoodie whose face is only a swirl of fog with a question mark in it, sitting cross-legged on a
    # mountain of gold coins, a laptop glowing on his knees and a price chart behind him shooting straight up
    defs, body = bg_office()
    body += stroke("M20 170 L80 150 L130 156 L190 110 L230 120 L270 60 L300 70 L330 10", "#3ad04a", 5)
    body += poly([(60, 200), (176, 120), (292, 200)], "#d4af37")
    for x, y in ((120, 176), (150, 160), (176, 140), (204, 162), (232, 180), (100, 192), (260, 192), (176, 176)):
        body += coin(x, y, 9)
    body += path("M146 126 C 146 80 206 80 206 126 Z", "#8a8a96")  # The hoodie
    body += circle(176, 86, 24, "#8a8a96") + circle(176, 90, 17, "#d8d8e0")
    body += stroke("M168 82 C 168 74 184 74 184 82 C 184 88 176 88 176 94 M176 100 L176 101", "#5a5a6a", 3)
    body += rect(156, 116, 40, 10, "#2a2a3a") + rect(160, 100, 32, 18, "#4fc3ff", 'opacity="0.9"')
    return svg(defs, body)


def the_invisible_hand():
    # An empty white glove floating over a busy market stall, nobody inside it, quietly lifting coins out of the
    # shoppers' pockets and dropping them into a fat banker's top hat
    defs, body = bg_sky("#bfe0f0", "#f4ecd8")
    body += rect(20, 120, 180, 14, "#c0392b") + rect(20, 134, 180, 50, "#a0784a")
    for i, x in enumerate((40, 70, 100, 130, 160, 190)):
        body += path(f"M{x - 10} 120 Q {x} 100 {x + 10} 120 Z", ["#c0392b", "#f4f0e6"][i % 2])
    for x, c in ((50, "#e8a040"), (80, "#d04a3a"), (110, "#6ab04a"), (140, "#e8d040")):
        body += circle(x, 146, 9, c)
    body += person(80, 200, 0.8, SKIN[1], "#5a7aa0", face="smile") + person(150, 200, 0.8, SKIN[0], "#a05a7a", face="smile")
    # The glove, empty at the cuff
    body += path("M228 70 C 220 50 236 40 244 54 L248 30 C 250 22 260 24 258 32 L256 52 L262 26 C 264 18 274 20 272 28 L268 54 L276 34 C 278 26 288 28 286 36 L278 64 C 276 86 256 96 240 92 Z", "#ffffff", 'stroke="#c8c8c8" stroke-width="2"')
    body += ellipse(236, 92, 14, 6, "#e8e8e8")
    for x, y in ((210, 110), (196, 130), (250, 120)):
        body += coin(x, y, 7)
    body += person(300, 200, 1.1, SKIN[0], "#1e2635", face="grin", tie="#d4af37")
    body += rect(268, 100, 40, 10, "#1a1a1a") + rect(276, 110, 24, 4, "#1a1a1a")
    return svg(defs, body)


def console_memory_card():
    # A chunky black console memory card, 8 MB MEMORY CARD on its label, with a goldfish in a tiny bowl taped on top for
    # the console's memory, a glowing save icon floating out of the card
    defs, body = bg_digital()
    body += rect(116, 70, 120, 120, "#1e1e24", 'rx="10" stroke="#3a3a44" stroke-width="3"')
    body += rect(130, 120, 92, 50, "#e8e8f0", 'rx="4"') + lettering("MEMORY", 176, 126, 8, "#1e1e24") + lettering("CARD", 176, 142, 8, "#1e1e24")
    for i in range(8):  # 8 blocks for the 8 MB
        body += rect(134 + i * 11, 160, 8, 6, "#3a6ad0")
    body += circle(176, 60, 30, "#bfe8ff", 'opacity="0.6" stroke="#ffffff" stroke-width="2"')
    body += ellipse(176, 64, 14, 8, "#ff9a2a") + poly([(188, 64), (198, 56), (198, 72)], "#ff9a2a") + circle(170, 62, 2, "#111")
    body += rect(140, 82, 72, 6, "#e8d8a0", 'opacity="0.85"')  # The tape
    body += rect(268, 40, 34, 34, "#3ad0a0", 'rx="4" opacity="0.9"') + rect(276, 40, 18, 12, "#1e1e24") + rect(274, 58, 22, 14, "#ffffff")
    body += circle(285, 57, 28, "#3ad0a0", 'opacity="0.2"')
    return svg(defs, body)


def good_boi():
    # A fluffy shiba dog in a tiny knight's helmet holding up a pot lid as a shield, standing in front of a crying smaller
    # creature, a dog biscuit tucked behind its ear
    defs, body = bg_digital()
    body += rect(0, 168, W, 32, "#3a3a5a")
    body += dog(170, 140, 1.4, "#e8a050", "#c87a30", face=1, muzzle="#fff4e0")
    body += path("M190 104 C 190 82 236 82 236 104 Z", "#9aa0a8", 'stroke="#5a6068" stroke-width="2"') + rect(200, 100, 26, 4, "#5a6068")
    body += ellipse(250, 140, 26, 30, "#a8a8b0", 'stroke="#6a6a72" stroke-width="3"') + circle(250, 140, 5, "#6a6a72")
    body += rect(196, 90, 18, 8, "#c89a5a", 'rx="3" transform="rotate(-20 205 94)"')
    body += circle(80, 160, 16, "#a0d0f0") + circle(74, 156, 2, "#111") + circle(86, 156, 2, "#111")
    body += stroke("M74 160 L72 172 M86 160 L88 172", "#4fa8ff", 2) + stroke("M75 166 Q 80 162 85 166", "#111", 1.5)
    return svg(defs, body)


def its_been_84_years():
    # A very old lady in a cardigan wheeled in on a hospital bed through a doorway, a tiny party hat on, a wall clock
    # behind her covered in cobwebs, a dusty birthday cake on the table with 84 candles burnt down to stubs
    defs, body = bg_digital()
    body += rect(20, 30, 90, 170, "#5a4a3a") + rect(28, 38, 74, 162, "#2a2a3a")  # The doorway
    body += circle(250, 44, 22, "#f4f0e6", 'stroke="#6a6a6a" stroke-width="3"') + stroke("M250 44 L250 30 M250 44 L260 50", "#333", 2)
    body += stroke("M230 24 L270 64 M270 24 L230 64 M226 44 L274 44", "#d8d8d8", 0.8, 'opacity="0.7"')
    body += rect(40, 140, 150, 18, "#f4f4f8", 'stroke="#aab" stroke-width="2"') + circle(56, 166, 7, "#555") + circle(176, 166, 7, "#555")
    body += person(110, 142, 1.0, SKIN[0], "#c8a0c8", face="smile", hair=OLD_HAIR, glasses="round")
    body += poly([(102, 64), (110, 44), (118, 64)], "#ff5fa2") + circle(110, 44, 3, "#ffe45c")
    body += rect(236, 150, 90, 40, "#8a5a3a") + rect(250, 126, 62, 24, "#f4e0c8", 'stroke="#b8a080" stroke-width="2"')
    for i in range(12):  # Candle stubs, too many to count
        body += rect(254 + i * 4.6, 120, 2.6, 6, ["#ff8a8a", "#8ad0ff", "#ffe48a"][i % 3])
    body += stroke("M250 140 L312 140", "#d8c8b0", 3, 'opacity="0.6"')
    return svg(defs, body)


def cyberpsychosis():
    # A man in a hoodie with glowing cables plugged into the back of his skull, eyes spinning with notification badges,
    # screaming at a phone that screams back, sparks flying from his chrome arm
    defs, body = bg_mind()
    body += person(130, 200, 1.4, SKIN[1], "#3a3a4a", face="shout", hat="hood", brows="angry")
    for i, (x2, y2) in enumerate(((20, 40), (40, 10), (10, 90))):
        body += stroke(f"M108 100 C 80 {90 - i * 20} 60 {80 - i * 10} {x2} {y2}", ["#4fc3ff", "#ff4fd8", "#3ad0a0"][i], 4)
    for x in (120, 140):
        body += circle(x, 108, 6, "#e83a3a") + circle(x, 108, 2, "#ffffff")
    body += stroke("M170 160 L220 120", "#c8d0d8", 12) + circle(222, 118, 8, "#c8d0d8")
    for x, y in ((232, 100), (240, 120), (226, 132)):
        body += stroke(f"M{x} {y} l 8 -6 M{x} {y} l 9 3", "#ffe45c", 2)
    body += phone(280, 110, 1.4, "#ff6a6a")
    body += ellipse(280, 116, 7, 9, "#5a1a1a")
    return svg(defs, body)


def momentum():
    # A man in a business suit sprinting downhill, legs a cartoon blur, a gale of motivational posters (HUSTLE, NEVER
    # STOP) blowing at his back, the edge of a cliff just ahead
    defs, body = bg_sky("#9fc8f0", "#f4e8c8")
    body += poly([(0, 110), (290, 170), (290, 200), (0, 200)], "#7a9a5a") + rect(290, 170, 62, 30, "#8ac0e8")
    body += person(220, 150, 1.0, SKIN[0], "#1e2635", face="grin", tie="#c03a3a")
    for i in range(4):
        body += ellipse(212 + i * 6, 160, 14, 5, "#1e2635", 'opacity="0.35"')
    body += speed_lines(180, 100, n=4, length=50, color="#ffffff")
    for x, y, a, word in ((60, 40, -12, "HUSTLE"), (110, 70, 8, "NEVER"), (40, 100, -4, "STOP")):
        body += group(rect(x - 30, y - 12, 60, 24, "#fff7e0", 'stroke="#333" stroke-width="1.5"') + lettering(word, x, y - 6, 9, "#c0392b"), f"rotate({a} {x} {y})")
    return svg(defs, body)


def reflect():
    # A woman in front of a tall mirror, her reflection wearing a different man's bandages, halo and storm cloud: in the
    # room he stands beside her with exactly those, and now she has them too
    defs, body = bg_mind()
    body += rect(180, 20, 110, 170, "#c8a060", 'rx="8"') + rect(188, 28, 94, 154, "#bcd8e8")
    body += person(236, 182, 1.0, SKIN[0], "#a05a8a", face="frown", hat="bob")  # The reflection
    for y in (140, 150):
        body += stroke(f"M212 {y} L260 {y}", "#ffffff", 4)
    body += ellipse(236, 96, 18, 4, "none", 'stroke="#ffd84a" stroke-width="3"') + cloud(236, 66, 0.6, "#6a6a7a", 1)
    body += person(140, 196, 1.15, SKIN[0], "#a05a8a", face="frown", hat="bob")  # Her
    body += ellipse(140, 106, 20, 4, "none", 'stroke="#ffd84a" stroke-width="3"') + cloud(140, 74, 0.7, "#6a6a7a", 1)
    body += stroke("M118 158 L162 158", "#ffffff", 5)
    body += person(60, 196, 1.15, SKIN[2], "#4a6a3a", face="smirk")  # Him
    body += ellipse(60, 106, 20, 4, "none", 'stroke="#ffd84a" stroke-width="3"') + cloud(60, 74, 0.7, "#6a6a7a", 1)
    body += stroke("M38 158 L82 158", "#ffffff", 5)
    return svg(defs, body)


def survivorship_bias():
    # A WWII bomber back on the runway, riddled with red dots of bullet holes in the famous diagram's pattern, its smug
    # pilot on the wing giving a thumbs-up with one eyebrow singed off
    defs, body = bg_sky("#bcd8e8", "#e8e0c8")
    body += rect(0, 160, W, 40, "#6a6a6a") + stroke("M0 180 L352 180", "#ffffff", 3, 'stroke-dasharray="20 14"')
    body += ellipse(176, 120, 120, 16, "#8a9078")  # The fuselage
    body += poly([(120, 116), (250, 116), (280, 70), (240, 70)], "#7a8068") + poly([(120, 124), (250, 124), (280, 168), (240, 168)], "#7a8068")
    body += poly([(56, 116), (40, 92), (66, 112)], "#7a8068")
    for x, y in ((150, 112), (160, 126), (200, 118), (210, 128), (230, 90), (250, 80), (240, 150), (262, 158), (70, 110), (60, 100), (180, 120), (256, 100)):
        body += circle(x, y, 3, "#d0342c")
    body += person(258, 74, 0.7, SKIN[0], "#6a4a2a", face="smirk", brows="angry")
    body += stroke("M276 50 L284 40", SKIN[0], 6) + rect(281, 34, 5, 8, SKIN[0], 'rx="2"')
    return svg(defs, body)


def yesnt():
    # A man calmly sipping tea while a fireball hits him, drawn twice over itself like a double exposure: in one he is
    # burnt to a crisp, in the other untouched, a thought bubble above him reading YES / NO with both boxes ticked
    defs, body = bg_mind()
    body += circle(60, 120, 30, "#ff7a1a", 'opacity="0.9"') + circle(60, 120, 16, "#ffd84a")
    body += speed_lines(36, 106, n=3, length=40, color="#ff9a3a")
    body += group(person(186, 200, 1.3, "#2a2a2a", "#1a1a1a", face="flat"), "translate(-8 0)", 0.55)  # Burnt to a crisp
    body += group(person(186, 200, 1.3, SKIN[0], "#3d5a80", face="closed"), "translate(8 0)", 0.75)  # Untouched
    body += rect(200, 140, 18, 16, "#ffffff", 'rx="3" stroke="#aaa" stroke-width="1"') + stroke("M206 136 q 3 -6 0 -10", "#cccccc", 1.5)
    body += cloud(276, 44, 1.0, "#ffffff", 0.95) + circle(240, 88, 5, "#ffffff") + circle(230, 100, 3, "#ffffff")
    body += lettering("YES", 262, 34, 7, "#333") + lettering("NO", 262, 46, 7, "#333")
    for y in (34, 46):
        body += rect(282, y, 8, 8, "#ffffff", 'stroke="#333" stroke-width="1.5"') + stroke(f"M283 {y + 4} l 2 3 l 5 -6", "#3ad04a", 2)
    return svg(defs, body)


def amen():
    # A choir of robed monks in a stone chapel snapping a giant hymn book shut with a thunderclap, every candle in the room
    # blowing out at once, a single wisp of smoke curling up from each
    defs, body = bg_myth()
    body += rect(0, 0, W, H, "#3a3448", 'opacity="0.6"')
    for x in (40, 312):
        body += rect(x - 14, 20, 28, 180, "#6a6478")
    body += path("M130 40 Q 176 4 222 40 L222 110 L130 110 Z", "#2a2438", 'stroke="#8a84a0" stroke-width="3"')
    for i, x in enumerate((90, 130, 222, 262)):
        body += person(x, 200, 0.9, SKIN[i % 3], "#5a3a2a", face="closed", hat="hood")
    body += rect(146, 120, 60, 40, "#7a2a2a", 'stroke="#3a1a1a" stroke-width="3"') + stroke("M176 120 L176 160", "#d4af37", 3)
    for dx in (-30, 30):
        body += stroke(f"M{176 + dx} 116 l {dx * 0.4} -10 M{176 + dx} 126 l {dx * 0.5} -2", "#ffffff", 2)
    for x in (66, 116, 236, 286):
        body += rect(x - 3, 100, 6, 20, "#f4ecd8") + stroke(f"M{x} 100 q -5 -8 0 -14 q 5 -6 0 -12", "#c8c8d0", 1.5, 'opacity="0.8"')
    return svg(defs, body)


def pyramids():
    # The three pyramids of Giza at golden sunset, the biggest one's capstone glowing with a holy light that rains down on
    # two tiny knights standing beside it, a bored camel in the foreground
    defs, body = bg_sky("#f0a050", "#f8e0a0")
    body += rect(0, 150, W, 50, "#e0b870")
    body += poly([(40, 150), (100, 96), (160, 150)], "#c89a50") + poly([(100, 96), (160, 150), (120, 150)], "#a87a3a")
    body += poly([(120, 150), (210, 50), (300, 150)], "#d0a458") + poly([(210, 50), (300, 150), (240, 150)], "#a87a3a")
    body += poly([(270, 150), (310, 112), (350, 150)], "#c89a50")
    body += circle(210, 50, 10, "#fff8c0") + circle(210, 50, 26, "#fff8c0", 'opacity="0.35"')
    body += poly([(204, 56), (216, 56), (250, 150), (170, 150)], "#fff8c0", 'opacity="0.25"')
    body += knight(186, 158, 0.35) + knight(232, 158, 0.35)
    # The camel, bored
    body += ellipse(70, 172, 34, 14, "#c8964a") + ellipse(62, 158, 12, 10, "#c8964a") + ellipse(80, 158, 12, 10, "#c8964a")
    body += stroke("M100 168 L118 140", "#c8964a", 9) + ellipse(124, 134, 10, 6, "#c8964a")
    body += stroke("M124 132 l 6 0", "#5a3a1a", 1.5)
    for x in (48, 60, 82, 94):
        body += rect(x - 3, 180, 6, 18, "#b8864a")
    return svg(defs, body)


def vampire():
    # A pale aristocrat in a cape at a blood-donation clinic, sipping from a sleeping gym bro's arm through a bendy straw,
    # the bro's bicep visibly deflating like a balloon
    defs, body = bg_myth()
    body += rect(0, 140, W, 60, "#e8e8f0") + rect(150, 20, 120, 20, "#ffffff", 'stroke="#c0392b" stroke-width="2"')
    body += plus(162, 30, 6, "#c0392b") + lettering("DONATE", 220, 24, 8, "#c0392b")
    body += rect(160, 130, 150, 14, "#8aa0c8") + person(230, 136, 1.0, SKIN[1], "#3a8ad0", face="closed")
    body += zzz(250, 60, 0.8, "#5a5a8a")
    body += ellipse(186, 120, 22, 14, SKIN[1], 'opacity="0.9"') + ellipse(178, 110, 10, 6, SKIN[1], 'opacity="0.6"')
    body += path("M60 200 L84 100 L140 100 L164 200 Z", "#1a1a2a") + path("M84 100 L72 86 L152 86 L140 100 Z", "#8a1a2a")
    body += person(112, 160, 1.1, "#e8e8f0", "#1a1a2a", face="smirk", hair="#1a1a1a")
    body += stroke("M124 92 C 140 92 150 100 150 110 L172 114", "#e83a8a", 2.5)
    return svg(defs, body)


CHUNK = {"Corporate": {"BusinessOperatingExpenses": business_operating_expenses, "CSuite": c_suite, "GoldenParachute": golden_parachute,
                       "LaissezFaire": laissez_faire, "PeacetheFrontier": peace_the_frontier, "SatoshiNakamoto": satoshi_nakamoto,
                       "TheInvisibleHand": the_invisible_hand},
         "DigitalSociety": {"ConsoleMemoryCard": console_memory_card, "GoodBoi": good_boi, "ItsBeen84Years": its_been_84_years},
         "Mind": {"Cyberpsychosis": cyberpsychosis, "Momentum": momentum, "Reflect": reflect, "SurvivorshipBias": survivorship_bias, "Yesnt": yesnt},
         "Myth": {"Amen": amen, "Pyramids": pyramids, "Vampire": vampire}}
for _theme, _cards in CHUNK.items():
    CARDS[_theme].update(_cards)


# ---------------------------------------------------------------- New cards (2026-10-16): the smaller factions, and three without art

def heart(cx, cy, s, color, extra=""):
    return path(f"M{cx:.1f} {cy + 6 * s:.1f} C {cx - 10 * s:.1f} {cy - 2 * s:.1f} {cx - 8 * s:.1f} {cy - 10 * s:.1f} {cx:.1f} {cy - 5 * s:.1f} "
                f"C {cx + 8 * s:.1f} {cy - 10 * s:.1f} {cx + 10 * s:.1f} {cy - 2 * s:.1f} {cx:.1f} {cy + 6 * s:.1f} Z", color, extra)


def threat_hunter():
    # A security analyst in a headlamp crawling through a tunnel of server cables with a magnifying glass, following
    # glowing red footprints to a tiny hooded hacker hiding behind a rack
    defs, body = bg_tech()
    defs += rad("lamp", [("#fff6c0", 0.55), ("#fff6c0", 0)])
    body += server_rack(270, 40, 70, 150)
    for k in range(7):  # The cables, hanging in a tunnel
        y = 6 + k * 6
        body += stroke(f"M0 {y} C 90 {y + 40} 200 {y + 30} 352 {y + 8}", ("#2a6ad0", "#d0342c", "#f2c94c", "#3aa86a")[k % 4], 4, 'opacity="0.85"')
    for k, x in enumerate((140, 170, 198, 224, 248)):  # Red footprints
        body += ellipse(x, 170 - (k % 2) * 8, 5, 3, "#ff3a3a", 'opacity="0.9"') + ellipse(x, 170 - (k % 2) * 8, 9, 6, "#ff3a3a", 'opacity="0.25"')
    # The hacker, small, peeking from behind the rack
    body += path("M258 186 C 256 160 266 150 276 150 C 286 150 292 160 290 186 Z", "#1a1a22")
    body += circle(274, 146, 11, "#1a1a22") + ellipse(272, 148, 6, 7, "#0a0a0f") + circle(270, 147, 1.5, "#ff3a3a") + circle(276, 147, 1.5, "#ff3a3a")
    # The analyst crawling, headlamp on
    body += poly([(88, 128), (260, 92), (260, 196), (88, 150)], "url(#lamp)")
    body += path("M20 176 C 30 150 70 142 100 150 L118 172 L24 190 Z", "#3d5a80")
    body += stroke("M96 168 L118 186 M34 182 L22 196", "#2a3a50", 10)
    body += circle(98, 132, 18, SKIN[1]) + path("M80 130 C 80 108 116 108 116 128 Z", "#3a2a1a")
    body += rect(80, 120, 36, 6, "#222") + circle(116, 124, 6, "#fff6c0", 'stroke="#555" stroke-width="2"')
    body += circle(104, 132, 2.2, "#222") + stroke("M100 142 l 8 0", "#222", 1.8)
    body += stroke("M118 150 L140 140", "#3d5a80", 9) + circle(150, 136, 13, "#bfe8ff", 'opacity="0.5" stroke="#8a6a3a" stroke-width="4"')
    body += stroke("M141 145 L132 154", "#8a6a3a", 5)
    return svg(defs, body)


def ctrl_z():
    # A huge curved UNDO arrow pulling a startled knight backwards off a battlefield into a playing card held up by a hand,
    # the keys CTRL and Z glowing on a keyboard below
    defs = lin("bg", ["#1b2a4a", "#0b1222"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(0, 150, W, 50, "#2a2a30") + rect(0, 150, W, 4, "#3a3a44")
    for i, x in enumerate(range(8, 352, 38)):  # The keyboard
        lit = i in (1, 6)
        body += rect(x, 160, 32, 26, "#4fc3ff" if lit else "#3a3a44", 'rx="4"' + (' opacity="0.95"' if lit else ""))
    body += lettering("CTRL", 62, 167, 8, "#0b1222") + lettering("Z", 252, 165, 10, "#0b1222")
    # The playing card held up, and the hand
    body += rect(34, 30, 70, 100, "#f4ecd8", 'rx="6" stroke="#8a7a5a" stroke-width="3"') + rect(42, 38, 54, 84, "none", 'stroke="#c0392b" stroke-width="2" rx="4"')
    body += path("M20 150 C 20 128 36 120 54 122 L80 122 C 90 122 92 134 82 136 L64 138 L60 150 Z", SKIN[0], 'stroke="#b08a6a" stroke-width="2"')
    # The undo arrow, from the knight back to the card
    body += path("M300 60 C 250 0 140 10 104 70", "none", 'stroke="#4fc3ff" stroke-width="16" stroke-linecap="round" opacity="0.85"')
    body += poly([(92, 52), (120, 66), (98, 90)], "#4fc3ff")
    body += knight(272, 142, 0.75, face_left=True)
    body += stroke("M240 120 l -18 -4 M240 132 l -20 2 M244 108 l -16 -10", "#ffffff", 3, 'opacity="0.7"')
    body += lettering("!", 300, 76, 18, "#ffe45c")
    return svg(defs, body)


def pair_programming():
    # Two developers squeezed at one tiny desk sharing one keyboard: one types with a coffee, the other leans over his
    # shoulder pointing at a bug on the screen; a timesheet between them bills 16 hours
    defs, body = bg_tech()
    body += monitor(124, 14, 120, 84, "#14202a")
    for k in range(6):
        body += rect(140, 30 + k * 10, 30 + (k * 23) % 60, 4, "#3fbf8a" if k != 3 else "#ff5a5a")
    body += circle(214, 61, 9, "none", 'stroke="#ff5a5a" stroke-width="2.5"')
    body += rect(90, 150, 190, 12, "#6a4a2a") + rect(140, 140, 80, 10, "#2a2a30", 'rx="2"')  # The tiny desk, one keyboard
    body += person(140, 200, 1.1, SKIN[0], "#3a6ad0", face="flat", hair="#3a2a1a", glasses="round")
    body += stroke("M150 160 L170 146", "#3a6ad0", 9) + rect(100, 120, 14, 20, "#ffffff", 'rx="2"') + stroke("M114 126 q 6 3 0 10", "#ffffff", 2.5)
    body += person(236, 186, 1.2, SKIN[2], "#d0342c", face="shout", hair="#1a1a1a", brows="angry")
    body += stroke("M248 130 L222 80", "#d0342c", 9) + circle(220, 76, 5, SKIN[2])
    body += rect(300, 96, 44, 56, "#ffffff", 'stroke="#555" stroke-width="2"') + lettering("16H", 322, 104, 9, "#c0392b")
    for k in range(3):
        body += rect(306, 120 + k * 8, 32, 3, "#aaaaaa")
    return svg(defs, body)


def suspicious_usb_stick_in_the_parking_lot():
    # A lone USB stick on the asphalt of an empty parking lot under a streetlight, labelled SALARIES 2026, an employee in a
    # lanyard bending down to pick it up, a nuclear plant's cooling tower behind the fence
    defs = lin("bg", ["#1a2236", "#3a3046"]) + rad("light", [("#ffe9a0", 0.55), ("#ffe9a0", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    body += path("M250 112 C 256 80 256 50 246 20 L318 20 C 308 50 308 80 314 112 Z", "#8a8a96")  # The cooling tower
    body += path("M262 20 C 270 6 300 6 306 20", "#c8c8d0", 'opacity="0.5"') + cloud(290, 4, 0.8, "#c8c8d0", 0.5)
    for x in range(0, 352, 16):  # The fence
        body += stroke(f"M{x} 96 L{x + 16} 116 M{x + 16} 96 L{x} 116", "#5a5a66", 1.2)
    body += stroke("M0 96 L352 96 M0 116 L352 116", "#5a5a66", 2)
    body += rect(0, 116, W, 84, "#2a2a30")
    for x in (40, 130, 220, 310):
        body += stroke(f"M{x} 130 L{x - 20} 200", "#e8e8e8", 3, 'opacity="0.6"')
    body += rect(196, 20, 6, 130, "#444") + rect(178, 18, 30, 8, "#555") + poly([(180, 26), (206, 26), (236, 196), (150, 196)], "url(#light)")
    # The stick, and its label
    body += rect(176, 168, 34, 12, "#1a1a22", 'rx="2"') + rect(210, 170, 10, 8, "#c8c8c8") + rect(180, 170, 26, 8, "#fff7e0")
    body += lettering("SALARIES", 193, 171, 3.2, "#c0392b", width=0.9)
    # The employee, bending down to it
    body += path("M70 196 C 70 150 100 120 130 120 L150 130 L120 196 Z", "#5a7a9a")
    body += circle(150, 120, 15, SKIN[1]) + path("M136 116 C 138 102 162 102 164 116 Z", "#3a2a1a")
    body += circle(156, 120, 2, "#222") + stroke("M152 128 l 6 -1", "#222", 1.6)
    body += stroke("M130 132 L170 168", "#5a7a9a", 8) + circle(172, 170, 5, SKIN[1])
    body += stroke("M140 134 L150 156", "#2a6ad0", 2) + rect(146, 154, 10, 12, "#ffffff", 'stroke="#2a6ad0" stroke-width="1.5"')
    return svg(defs, body)


def socrates():
    # Socrates in a white toga, bald with a big beard, cornering a sweating man in a suit against a marble column with one
    # raised finger and a huge question mark; a cup of hemlock waits on a pedestal behind
    defs, body = bg_mind()
    body += rect(0, 168, W, 32, "#e8dcc8")
    body += rect(260, 20, 40, 150, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"') + rect(252, 12, 56, 12, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"')
    for x in (270, 280, 290):
        body += stroke(f"M{x} 26 L{x} 166", "#d8d0c0", 2)
    body += person(282, 196, 1.0, SKIN[0], "#2a2a3a", face="wavy", hair="#5a3a1a", tie="#c0392b", brows="sad")
    body += sweat(304, 120) + sweat(262, 116, 0.8)
    body += rect(30, 120, 30, 50, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"')  # The pedestal and the hemlock
    body += path("M34 120 L56 120 L52 104 L38 104 Z", "#8a6a3a") + ellipse(45, 104, 8, 3, "#3a6a2a")
    # Socrates: toga, bald, big beard, a finger raised
    body += path("M96 200 C 96 140 120 118 160 118 C 200 118 222 140 222 200 Z", "#f6f2ea", 'stroke="#c8c0b0" stroke-width="2"')
    body += stroke("M110 130 C 140 160 180 150 214 190", "#d8d0c0", 3)
    body += circle(160, 82, 26, SKIN[0]) + circle(134, 84, 6, SKIN[0]) + circle(186, 84, 6, SKIN[0])
    body += circle(138, 70, 6, "#d8d8d8") + circle(182, 70, 6, "#d8d8d8")
    body += path("M136 88 C 136 130 184 130 184 88 C 176 102 144 102 136 88 Z", "#d8d8d8")
    body += circle(151, 80, 2.4, "#222") + circle(169, 80, 2.4, "#222") + stroke("M144 72 L156 74 M164 74 L176 72", "#888", 2)
    body += stroke("M210 150 L236 96", "#f6f2ea", 12) + stroke("M236 96 L240 74", SKIN[0], 6)
    body += lettering("?", 214, 14, 34, "#3a2a6a")
    return svg(defs, body)


def maslows_pyramid():
    # Maslow's pyramid as a stone pyramid in layers, tiny people at the bottom fighting over bread and water, one man
    # meditating smugly alone at the glowing top, and a WiFi symbol carved into a new layer under the base
    defs, body = bg_mind()
    defs += rad("glow", [("#fff6c0", 0.9), ("#fff6c0", 0)])
    body += circle(176, 30, 46, "url(#glow)")
    layers = [(150, 186, "#a89878"), (118, 154, "#b8a888"), (86, 122, "#c8b898"), (56, 90, "#d8c8a8"), (30, 60, "#e8d8b8")]
    for top, bottom, color in layers:
        half_t, half_b = (top - 20) * 0.95, (bottom - 20) * 0.95
        body += poly([(176 - half_t, top), (176 + half_t, top), (176 + half_b, bottom), (176 - half_b, bottom)], color, 'stroke="#7a6a50" stroke-width="2"')
    body += rect(14, 186, 324, 14, "#6a5a40") + stroke("M176 196 m -14 0 a 14 14 0 0 1 28 0 M176 196 m -8 0 a 8 8 0 0 1 16 0", "#4fc3ff", 2.5) + circle(176, 196, 2, "#4fc3ff")
    # The smug one at the top, cross-legged
    body += ellipse(176, 28, 14, 6, "#e86a3a") + circle(176, 14, 8, SKIN[0]) + stroke("M172 14 q 2 2 4 0 M178 14 q 2 2 4 0", "#222", 1.2)
    body += stroke("M162 26 l -8 -6 M190 26 l 8 -6", "#e86a3a", 4)
    # The crowd at the bottom, fighting over bread and water
    for k, x in enumerate((50, 76, 104, 248, 276, 302)):
        body += circle(x, 162, 7, SKIN[k % 4]) + rect(x - 6, 168, 12, 16, ("#3a6ad0", "#d0342c", "#3aa86a")[k % 3], 'rx="3"')
        body += stroke(f"M{x - 6} 172 l {-6 if k % 2 else 6} -8", ("#3a6ad0", "#d0342c", "#3aa86a")[k % 3], 3)
    body += ellipse(90, 152, 12, 6, "#c8964a") + rect(286, 142, 8, 14, "#7ac8f0", 'rx="2"')
    return svg(defs, body)


def legerdemain():
    # A magician's white-gloved hands doing the cups-and-ball trick on a velvet table, tiny soldiers under the cups being
    # shuffled, one cup lifted to show the soldier is gone
    defs, body = bg_mind()
    body += path("M0 120 L352 120 L352 200 L0 200 Z", "#5a1a3a") + rect(0, 120, W, 6, "#7a2a5a")
    body += stroke("M60 40 C 120 10 230 10 290 40", "#ffffff", 3, 'opacity="0.6" stroke-dasharray="6 6"')
    body += poly([(286, 34), (298, 44), (282, 46)], "#ffffff", 'opacity="0.6"')
    for x, lifted in ((96, False), (176, True), (256, False)):
        y = 70 if lifted else 100
        body += path(f"M{x - 26} {y + 50} L{x - 18} {y} L{x + 18} {y} L{x + 26} {y + 50} Z", "#c0392b", 'stroke="#7a1a1a" stroke-width="2"')
        body += ellipse(x, y + 50, 26, 6, "#a02a1a") + rect(x - 22, y + 12, 44, 6, "#d4af37")
        if lifted:
            body += ellipse(x, 150, 22, 5, "#3a0a1a", 'opacity="0.6"') + lettering("?", x, 128, 14, "#ffffff")
    # A tiny soldier peeking out under the right cup
    body += circle(276, 146, 6, SKIN[1]) + path("M268 144 C 268 134 284 134 284 144 Z", "#55603a")
    # The white gloves
    body += path("M10 70 C 30 60 56 62 70 74 L64 86 C 50 80 30 82 16 90 Z", "#ffffff", 'stroke="#aaa" stroke-width="2"')
    body += path("M342 46 C 322 36 296 40 286 52 L292 64 C 306 56 326 58 340 66 Z", "#ffffff", 'stroke="#aaa" stroke-width="2"')
    body += stroke("M70 74 L80 70 M286 52 L276 48", "#ffffff", 5)
    return svg(defs, body)


def short_king():
    # A short man in a paper crown standing on a stack of phone books to take a selfie at arm's length, the profile on his
    # phone screen reading 6 FT
    defs, body = bg_digital()
    for k in range(5):  # The phone books
        body += rect(120, 168 - k * 12, 110, 12, ("#f2c94c", "#e8e0c8", "#f2c94c", "#e8e0c8", "#f2c94c")[k], 'stroke="#8a7a3a" stroke-width="1.5"')
    body += path("M146 108 L146 120 L156 120 L156 108 Z M194 108 L194 120 L204 120 L204 108 Z", "#2a2a3a")
    body += person(176, 116, 0.9, SKIN[1], "#e0457b", face="smirk", hair="#1a1a1a")
    body += poly([(160, 42), (166, 30), (172, 40), (176, 26), (180, 40), (186, 30), (192, 42)], "#ffe45c", 'stroke="#c89a1a" stroke-width="1.5"')
    body += stroke("M196 84 L250 50", "#e0457b", 7) + circle(252, 48, 4, SKIN[1])
    body += phone(268, 46, 1.3, "#ffffff")
    body += circle(268, 36, 7, SKIN[1]) + lettering("6 FT", 268, 50, 6, "#2a3d8f")
    for k, (x, y) in enumerate(((300, 100), (316, 80), (290, 130))):
        body += heart(x, y, 0.9 + k * 0.2, "#ff5a8a")
    return svg(defs, body)


def content_creator():
    # A streamer in a headset under a ring light screaming at the camera with a shocked face and a giant red arrow pointing
    # at nothing, the thumbnail on the monitor behind showing the same face, 100K in gold
    defs, body = bg_digital()
    body += monitor(196, 10, 140, 92, "#2a1a3a")
    body += circle(240, 50, 18, SKIN[0]) + ellipse(240, 58, 5, 6, "#5a1a1a") + circle(233, 46, 3, "#222") + circle(247, 46, 3, "#222")
    body += lettering("100K", 296, 40, 12, "#ffd84a", outline="#7a5a10")
    body += poly([(150, 40), (190, 30), (186, 44), (226, 46), (226, 56), (186, 58), (190, 72)], "#ff2a2a", 'stroke="#7a0a0a" stroke-width="2"')
    body += circle(80, 80, 44, "none", 'stroke="#ffffff" stroke-width="8" opacity="0.9"') + circle(80, 80, 44, "none", 'stroke="#fff6c0" stroke-width="16" opacity="0.25"')
    body += person(80, 200, 1.4, SKIN[0], "#7a3aa0", face="shout", hair="#d0342c", brows="sad")
    body += path("M50 100 C 50 70 110 70 110 100", "none", 'stroke="#1a1a22" stroke-width="5"') + rect(44, 96, 10, 16, "#1a1a22", 'rx="3"') + rect(106, 96, 10, 16, "#1a1a22", 'rx="3"')
    body += stroke("M108 110 L120 126", "#1a1a22", 3) + circle(122, 128, 3, "#1a1a22")
    return svg(defs, body)


def thirst_trap():
    # A steel bear trap open on a beach towel, a ring light and a phone on a tripod in front of it, the phone screen
    # filling with hearts, a glass of water standing beside it
    defs, body = bg_digital()
    body += path("M20 140 L300 120 L340 190 L40 200 Z", "#ff8ab0", 'stroke="#ffffff" stroke-width="2"')
    for k in range(5):
        body += stroke(f"M{30 + k * 10} {150 + k * 10} L{306 + k * 7} {130 + k * 12}", "#ffffff", 3, 'opacity="0.6"')
    # The bear trap, open
    body += ellipse(170, 166, 64, 14, "#6a6a72", 'stroke="#3a3a42" stroke-width="3"')
    for k in range(9):
        x = 116 + k * 13
        body += poly([(x, 160), (x + 6, 132 - (k % 2) * 4), (x + 12, 160)], "#b8b8c4", 'stroke="#5a5a66" stroke-width="1.5"')
        body += poly([(x, 172), (x + 6, 194), (x + 12, 172)], "#9a9aa6", 'stroke="#5a5a66" stroke-width="1.5"')
    body += rect(160, 160, 20, 10, "#c8964a", 'rx="2"')
    # Ring light and phone on a tripod
    body += stroke("M262 104 L262 132 M262 132 L250 196 M262 132 L274 196 M262 132 L262 196", "#2a2a30", 3)
    body += circle(262, 70, 34, "none", 'stroke="#ffffff" stroke-width="7"') + phone(262, 76, 1.2, "#ffffff")
    for k, (x, y) in enumerate(((256, 66), (268, 72), (258, 84), (266, 58))):
        body += heart(x, y, 0.5, "#ff2a6a")
    body += rect(56, 100, 22, 40, "#bfe8ff", 'opacity="0.7" stroke="#ffffff" stroke-width="2"') + rect(58, 112, 18, 26, "#7ac8f0", 'opacity="0.8"')
    return svg(defs, body)


def overtime():
    # An office worker at a desk late at night, surrounded by empty coffee cups and energy drink cans, typing furiously with
    # smoke rising from the keyboard, eyes bloodshot and wide open, a wall clock showing 3 a.m. and a calendar with every
    # weekend crossed out
    defs = lin("bg", ["#141a2e", "#2a2440"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += circle(60, 44, 26, "#f4ecd8", 'stroke="#555" stroke-width="3"') + stroke("M60 44 L60 26 M60 44 L72 50", "#222", 3)
    body += lettering("3AM", 60, 76, 7, "#ff5a5a")
    body += rect(268, 14, 70, 66, "#ffffff", 'stroke="#555" stroke-width="2"') + rect(268, 14, 70, 12, "#c0392b")
    for row in range(4):
        for col in range(7):
            x, y = 272 + col * 9.5, 30 + row * 12
            body += rect(x, y, 7, 8, "#e8e8e8")
            if col >= 5:
                body += stroke(f"M{x} {y} L{x + 7} {y + 8} M{x + 7} {y} L{x} {y + 8}", "#c0392b", 1.5)
    body += rect(0, 160, W, 40, "#4a3a2a")
    body += monitor(150, 60, 120, 80, "#bfe8ff")
    body += rect(150, 150, 110, 12, "#2a2a30", 'rx="2"')
    for k in range(3):
        body += stroke(f"M{170 + k * 30} 150 q -6 -12 0 -22 q 6 -10 0 -20", "#c8c8d0", 2.5, 'opacity="0.7"')
    body += person(110, 200, 1.3, SKIN[0], "#3d5a80", face="wavy", hair="#3a2a1a", tie="#c0392b")
    body += circle(101, 115, 5, "#ffffff", 'stroke="#ff3a3a" stroke-width="1.5"') + circle(119, 115, 5, "#ffffff", 'stroke="#ff3a3a" stroke-width="1.5"')
    body += circle(101, 115, 1.8, "#222") + circle(119, 115, 1.8, "#222")
    body += stroke("M130 150 L160 152 M126 158 L170 158", "#3d5a80", 8)
    for x in (20, 36, 300, 316, 330):  # The cups and cans
        body += rect(x, 142, 12, 18, "#ffffff" if x < 100 else "#3aa86a", 'rx="2" stroke="#555" stroke-width="1"')
    return svg(defs, body)


def taqiyya():
    # A serene robed figure bowing slightly and offering an ornate gift box tied with a gold ribbon, both hands extended in
    # welcome; on the wall behind, the box's shadow is a coiled snake
    defs, body = bg_myth()
    body += rect(0, 170, W, 30, "#5a3a2a")
    # The shadow on the wall: a coiled snake
    body += path("M230 150 C 200 150 196 120 226 116 C 256 112 262 90 236 84 C 210 78 206 60 226 52", "none", 'stroke="#1a0a2a" stroke-width="16" stroke-linecap="round" opacity="0.55"')
    body += ellipse(232, 48, 14, 9, "#1a0a2a", 'opacity="0.55"') + stroke("M244 50 l 10 -2 l -4 4 l 6 2", "#1a0a2a", 2, 'opacity="0.55"')
    # The figure, robed, bowing slightly
    body += path("M60 200 C 64 140 90 110 128 112 C 160 114 176 150 178 200 Z", "#e8e0d0", 'stroke="#a89878" stroke-width="2"')
    body += circle(126, 86, 20, SKIN[2]) + path("M104 92 C 100 60 152 60 148 92 C 142 78 110 78 104 92 Z", "#f6f2ea", 'stroke="#c8c0b0" stroke-width="2"')
    body += path("M110 96 C 114 116 138 116 142 96 C 136 108 116 108 110 96 Z", "#3a2a1a")
    body += stroke("M118 86 q 3 2 6 0 M130 86 q 3 2 6 0", "#222", 1.6) + stroke("M120 100 q 6 4 12 0", "#222", 1.6)
    body += stroke("M150 150 L186 136 M140 160 L186 150", "#e8e0d0", 10)
    # The gift box, gold ribbon
    body += rect(186, 116, 46, 40, "#8a1a3a", 'stroke="#5a0a2a" stroke-width="2"') + rect(204, 116, 10, 40, "#d4af37") + rect(186, 130, 46, 8, "#d4af37")
    body += ellipse(202, 112, 10, 6, "#d4af37") + ellipse(216, 112, 10, 6, "#d4af37")
    return svg(defs, body)


def geomagnetic_reversal():
    # A globe with its magnetic field lines flipping over, the N and S labels swapping places mid-air, a scientist staring
    # at a compass whose needle spins wildly, and a flock of birds flying in confused loops
    defs, body = bg_utopia()
    defs += rad("earth", [("#5aa8e0", 1), ("#1a5a9a", 1)])
    body += circle(150, 96, 52, "url(#earth)", 'stroke="#0a3a6a" stroke-width="2"')
    body += path("M118 70 C 130 60 150 66 146 82 C 140 96 120 92 118 70 Z M156 104 C 170 96 186 110 178 126 C 168 138 152 126 156 104 Z", "#3aa86a")
    for k, r in enumerate((70, 88)):
        body += path(f"M150 {96 - 52} C {150 - r} {96 - 70} {150 - r} {96 + 70} 150 {96 + 52}", "none", 'stroke="#d0342c" stroke-width="2.5" opacity="0.7" stroke-dasharray="5 4"')
        body += path(f"M150 {96 - 52} C {150 + r} {96 - 70} {150 + r} {96 + 70} 150 {96 + 52}", "none", 'stroke="#2a6ad0" stroke-width="2.5" opacity="0.7" stroke-dasharray="5 4"')
    body += lettering("S", 150, 14, 14, "#2a6ad0") + lettering("N", 150, 158, 14, "#d0342c")
    body += stroke("M170 22 C 186 34 186 150 172 166", "none", 1) + path("M176 20 C 200 50 200 140 178 168", "none", 'stroke="#555" stroke-width="2" stroke-dasharray="3 4"')
    body += poly([(178, 168), (172, 158), (184, 160)], "#555")
    # The scientist with the compass
    body += person(290, 200, 1.1, SKIN[0], "#ffffff", face="shout", hair=OLD_HAIR, glasses="round", brows="sad")
    body += circle(256, 150, 16, "#f4ecd8", 'stroke="#8a6a3a" stroke-width="3"')
    body += stroke("M244 142 L268 158", "#d0342c", 3) + stroke("M244 158 L268 142", "#2a6ad0", 2, 'opacity="0.6"')
    body += stroke("M270 146 a 18 18 0 0 1 4 12", "#555", 1.5) + stroke("M240 164 a 18 18 0 0 1 -4 -12", "#555", 1.5)
    # The birds, looping
    for k, (x, y) in enumerate(((40, 40), (70, 26), (60, 60), (300, 30), (326, 52))):
        body += stroke(f"M{x - 7} {y} q 4 -6 7 0 q 3 -6 7 0", "#2a2a3a", 2)
    body += stroke("M30 80 C 10 60 60 20 90 40 C 110 54 80 80 60 70", "none", 1) + path("M30 80 C 10 60 60 20 90 40 C 110 54 80 80 60 70", "none", 'stroke="#2a2a3a" stroke-width="1.2" stroke-dasharray="2 4" opacity="0.6"')
    return svg(defs, body)


CHUNK_NEW = {"Tech": {"ThreatHunter": threat_hunter, "CtrlZ": ctrl_z, "PairProgramming": pair_programming,
                      "SuspiciousUSBStickintheParkingLot": suspicious_usb_stick_in_the_parking_lot},
             "Mind": {"Socrates": socrates, "MaslowsPyramid": maslows_pyramid, "Legerdemain": legerdemain},
             "DigitalSociety": {"ShortKing": short_king, "ContentCreator": content_creator, "ThirstTrap": thirst_trap},
             "Corporate": {"Overtime": overtime}, "Myth": {"Taqiyya": taqiyya}, "Utopia": {"GeomagneticReversal": geomagnetic_reversal}}
for _theme, _cards in CHUNK_NEW.items():
    CARDS[_theme].update(_cards)


# ---------------------------------------------------------------- New cards (2026-10-16): the smaller factions, and three without art

def heart(cx, cy, s, color, extra=""):
    return path(f"M{cx:.1f} {cy + 6 * s:.1f} C {cx - 10 * s:.1f} {cy - 2 * s:.1f} {cx - 8 * s:.1f} {cy - 10 * s:.1f} {cx:.1f} {cy - 5 * s:.1f} "
                f"C {cx + 8 * s:.1f} {cy - 10 * s:.1f} {cx + 10 * s:.1f} {cy - 2 * s:.1f} {cx:.1f} {cy + 6 * s:.1f} Z", color, extra)


def threat_hunter():
    # A security analyst in a headlamp crawling through a tunnel of server cables with a magnifying glass, following
    # glowing red footprints to a tiny hooded hacker hiding behind a rack
    defs, body = bg_tech()
    defs += rad("lamp", [("#fff6c0", 0.55), ("#fff6c0", 0)])
    body += server_rack(270, 40, 70, 150)
    for k in range(7):  # The cables, hanging in a tunnel
        y = 6 + k * 6
        body += stroke(f"M0 {y} C 90 {y + 40} 200 {y + 30} 352 {y + 8}", ("#2a6ad0", "#d0342c", "#f2c94c", "#3aa86a")[k % 4], 4, 'opacity="0.85"')
    for k, x in enumerate((140, 170, 198, 224, 248)):  # Red footprints
        body += ellipse(x, 170 - (k % 2) * 8, 5, 3, "#ff3a3a", 'opacity="0.9"') + ellipse(x, 170 - (k % 2) * 8, 9, 6, "#ff3a3a", 'opacity="0.25"')
    # The hacker, small, peeking from behind the rack
    body += path("M258 186 C 256 160 266 150 276 150 C 286 150 292 160 290 186 Z", "#1a1a22")
    body += circle(274, 146, 11, "#1a1a22") + ellipse(272, 148, 6, 7, "#0a0a0f") + circle(270, 147, 1.5, "#ff3a3a") + circle(276, 147, 1.5, "#ff3a3a")
    # The analyst crawling, headlamp on
    body += poly([(88, 128), (260, 92), (260, 196), (88, 150)], "url(#lamp)")
    body += path("M20 176 C 30 150 70 142 100 150 L118 172 L24 190 Z", "#3d5a80")
    body += stroke("M96 168 L118 186 M34 182 L22 196", "#2a3a50", 10)
    body += circle(98, 132, 18, SKIN[1]) + path("M80 130 C 80 108 116 108 116 128 Z", "#3a2a1a")
    body += rect(80, 120, 36, 6, "#222") + circle(116, 124, 6, "#fff6c0", 'stroke="#555" stroke-width="2"')
    body += circle(104, 132, 2.2, "#222") + stroke("M100 142 l 8 0", "#222", 1.8)
    body += stroke("M118 150 L140 140", "#3d5a80", 9) + circle(150, 136, 13, "#bfe8ff", 'opacity="0.5" stroke="#8a6a3a" stroke-width="4"')
    body += stroke("M141 145 L132 154", "#8a6a3a", 5)
    return svg(defs, body)


def ctrl_z():
    # A huge curved UNDO arrow pulling a startled knight backwards off a battlefield into a playing card held up by a hand,
    # the keys CTRL and Z glowing on a keyboard below
    defs = lin("bg", ["#1b2a4a", "#0b1222"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(0, 150, W, 50, "#2a2a30") + rect(0, 150, W, 4, "#3a3a44")
    for i, x in enumerate(range(8, 352, 38)):  # The keyboard
        lit = i in (1, 6)
        body += rect(x, 160, 32, 26, "#4fc3ff" if lit else "#3a3a44", 'rx="4"' + (' opacity="0.95"' if lit else ""))
    body += lettering("CTRL", 62, 167, 8, "#0b1222") + lettering("Z", 252, 165, 10, "#0b1222")
    # The playing card held up, and the hand
    body += rect(34, 30, 70, 100, "#f4ecd8", 'rx="6" stroke="#8a7a5a" stroke-width="3"') + rect(42, 38, 54, 84, "none", 'stroke="#c0392b" stroke-width="2" rx="4"')
    body += path("M20 150 C 20 128 36 120 54 122 L80 122 C 90 122 92 134 82 136 L64 138 L60 150 Z", SKIN[0], 'stroke="#b08a6a" stroke-width="2"')
    # The undo arrow, from the knight back to the card
    body += path("M300 60 C 250 0 140 10 104 70", "none", 'stroke="#4fc3ff" stroke-width="16" stroke-linecap="round" opacity="0.85"')
    body += poly([(92, 52), (120, 66), (98, 90)], "#4fc3ff")
    body += knight(272, 142, 0.75, face_left=True)
    body += stroke("M240 120 l -18 -4 M240 132 l -20 2 M244 108 l -16 -10", "#ffffff", 3, 'opacity="0.7"')
    body += lettering("!", 300, 76, 18, "#ffe45c")
    return svg(defs, body)


def pair_programming():
    # Two developers squeezed at one tiny desk sharing one keyboard: one types with a coffee, the other leans over his
    # shoulder pointing at a bug on the screen; a timesheet between them bills 16 hours
    defs, body = bg_tech()
    body += monitor(124, 14, 120, 84, "#14202a")
    for k in range(6):
        body += rect(140, 30 + k * 10, 30 + (k * 23) % 60, 4, "#3fbf8a" if k != 3 else "#ff5a5a")
    body += circle(214, 61, 9, "none", 'stroke="#ff5a5a" stroke-width="2.5"')
    body += rect(90, 150, 190, 12, "#6a4a2a") + rect(140, 140, 80, 10, "#2a2a30", 'rx="2"')  # The tiny desk, one keyboard
    body += person(140, 200, 1.1, SKIN[0], "#3a6ad0", face="flat", hair="#3a2a1a", glasses="round")
    body += stroke("M150 160 L170 146", "#3a6ad0", 9) + rect(100, 120, 14, 20, "#ffffff", 'rx="2"') + stroke("M114 126 q 6 3 0 10", "#ffffff", 2.5)
    body += person(236, 186, 1.2, SKIN[2], "#d0342c", face="shout", hair="#1a1a1a", brows="angry")
    body += stroke("M248 130 L222 80", "#d0342c", 9) + circle(220, 76, 5, SKIN[2])
    body += rect(300, 96, 44, 56, "#ffffff", 'stroke="#555" stroke-width="2"') + lettering("16H", 322, 104, 9, "#c0392b")
    for k in range(3):
        body += rect(306, 120 + k * 8, 32, 3, "#aaaaaa")
    return svg(defs, body)


def suspicious_usb_stick_in_the_parking_lot():
    # A lone USB stick on the asphalt of an empty parking lot under a streetlight, labelled SALARIES 2026, an employee in a
    # lanyard bending down to pick it up, a nuclear plant's cooling tower behind the fence
    defs = lin("bg", ["#1a2236", "#3a3046"]) + rad("light", [("#ffe9a0", 0.55), ("#ffe9a0", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    body += path("M250 112 C 256 80 256 50 246 20 L318 20 C 308 50 308 80 314 112 Z", "#8a8a96")  # The cooling tower
    body += path("M262 20 C 270 6 300 6 306 20", "#c8c8d0", 'opacity="0.5"') + cloud(290, 4, 0.8, "#c8c8d0", 0.5)
    for x in range(0, 352, 16):  # The fence
        body += stroke(f"M{x} 96 L{x + 16} 116 M{x + 16} 96 L{x} 116", "#5a5a66", 1.2)
    body += stroke("M0 96 L352 96 M0 116 L352 116", "#5a5a66", 2)
    body += rect(0, 116, W, 84, "#2a2a30")
    for x in (40, 130, 220, 310):
        body += stroke(f"M{x} 130 L{x - 20} 200", "#e8e8e8", 3, 'opacity="0.6"')
    body += rect(196, 20, 6, 130, "#444") + rect(178, 18, 30, 8, "#555") + poly([(180, 26), (206, 26), (236, 196), (150, 196)], "url(#light)")
    # The stick, and its label
    body += rect(176, 168, 34, 12, "#1a1a22", 'rx="2"') + rect(210, 170, 10, 8, "#c8c8c8") + rect(180, 170, 26, 8, "#fff7e0")
    body += lettering("SALARIES", 193, 171, 3.2, "#c0392b", width=0.9)
    # The employee, bending down to it
    body += path("M70 196 C 70 150 100 120 130 120 L150 130 L120 196 Z", "#5a7a9a")
    body += circle(150, 120, 15, SKIN[1]) + path("M136 116 C 138 102 162 102 164 116 Z", "#3a2a1a")
    body += circle(156, 120, 2, "#222") + stroke("M152 128 l 6 -1", "#222", 1.6)
    body += stroke("M130 132 L170 168", "#5a7a9a", 8) + circle(172, 170, 5, SKIN[1])
    body += stroke("M140 134 L150 156", "#2a6ad0", 2) + rect(146, 154, 10, 12, "#ffffff", 'stroke="#2a6ad0" stroke-width="1.5"')
    return svg(defs, body)


def socrates():
    # Socrates in a white toga, bald with a big beard, cornering a sweating man in a suit against a marble column with one
    # raised finger and a huge question mark; a cup of hemlock waits on a pedestal behind
    defs, body = bg_mind()
    body += rect(0, 168, W, 32, "#e8dcc8")
    body += rect(260, 20, 40, 150, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"') + rect(252, 12, 56, 12, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"')
    for x in (270, 280, 290):
        body += stroke(f"M{x} 26 L{x} 166", "#d8d0c0", 2)
    body += person(282, 196, 1.0, SKIN[0], "#2a2a3a", face="wavy", hair="#5a3a1a", tie="#c0392b", brows="sad")
    body += sweat(304, 120) + sweat(262, 116, 0.8)
    body += rect(30, 120, 30, 50, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"')  # The pedestal and the hemlock
    body += path("M34 120 L56 120 L52 104 L38 104 Z", "#8a6a3a") + ellipse(45, 104, 8, 3, "#3a6a2a")
    # Socrates: toga, bald, big beard, a finger raised
    body += path("M96 200 C 96 140 120 118 160 118 C 200 118 222 140 222 200 Z", "#f6f2ea", 'stroke="#c8c0b0" stroke-width="2"')
    body += stroke("M110 130 C 140 160 180 150 214 190", "#d8d0c0", 3)
    body += circle(160, 82, 26, SKIN[0]) + circle(134, 84, 6, SKIN[0]) + circle(186, 84, 6, SKIN[0])
    body += circle(138, 70, 6, "#d8d8d8") + circle(182, 70, 6, "#d8d8d8")
    body += path("M136 88 C 136 130 184 130 184 88 C 176 102 144 102 136 88 Z", "#d8d8d8")
    body += circle(151, 80, 2.4, "#222") + circle(169, 80, 2.4, "#222") + stroke("M144 72 L156 74 M164 74 L176 72", "#888", 2)
    body += stroke("M210 150 L236 96", "#f6f2ea", 12) + stroke("M236 96 L240 74", SKIN[0], 6)
    body += lettering("?", 214, 14, 34, "#3a2a6a")
    return svg(defs, body)


def maslows_pyramid():
    # Maslow's pyramid as a stone pyramid in layers, tiny people at the bottom fighting over bread and water, one man
    # meditating smugly alone at the glowing top, and a WiFi symbol carved into a new layer under the base
    defs, body = bg_mind()
    defs += rad("glow", [("#fff6c0", 0.9), ("#fff6c0", 0)])
    body += circle(176, 30, 46, "url(#glow)")
    layers = [(150, 186, "#a89878"), (118, 154, "#b8a888"), (86, 122, "#c8b898"), (56, 90, "#d8c8a8"), (30, 60, "#e8d8b8")]
    for top, bottom, color in layers:
        half_t, half_b = (top - 20) * 0.95, (bottom - 20) * 0.95
        body += poly([(176 - half_t, top), (176 + half_t, top), (176 + half_b, bottom), (176 - half_b, bottom)], color, 'stroke="#7a6a50" stroke-width="2"')
    body += rect(14, 186, 324, 14, "#6a5a40") + stroke("M176 196 m -14 0 a 14 14 0 0 1 28 0 M176 196 m -8 0 a 8 8 0 0 1 16 0", "#4fc3ff", 2.5) + circle(176, 196, 2, "#4fc3ff")
    # The smug one at the top, cross-legged
    body += ellipse(176, 28, 14, 6, "#e86a3a") + circle(176, 14, 8, SKIN[0]) + stroke("M172 14 q 2 2 4 0 M178 14 q 2 2 4 0", "#222", 1.2)
    body += stroke("M162 26 l -8 -6 M190 26 l 8 -6", "#e86a3a", 4)
    # The crowd at the bottom, fighting over bread and water
    for k, x in enumerate((50, 76, 104, 248, 276, 302)):
        body += circle(x, 162, 7, SKIN[k % 4]) + rect(x - 6, 168, 12, 16, ("#3a6ad0", "#d0342c", "#3aa86a")[k % 3], 'rx="3"')
        body += stroke(f"M{x - 6} 172 l {-6 if k % 2 else 6} -8", ("#3a6ad0", "#d0342c", "#3aa86a")[k % 3], 3)
    body += ellipse(90, 152, 12, 6, "#c8964a") + rect(286, 142, 8, 14, "#7ac8f0", 'rx="2"')
    return svg(defs, body)


def legerdemain():
    # A magician's white-gloved hands doing the cups-and-ball trick on a velvet table, tiny soldiers under the cups being
    # shuffled, one cup lifted to show the soldier is gone
    defs, body = bg_mind()
    body += path("M0 120 L352 120 L352 200 L0 200 Z", "#5a1a3a") + rect(0, 120, W, 6, "#7a2a5a")
    body += stroke("M60 40 C 120 10 230 10 290 40", "#ffffff", 3, 'opacity="0.6" stroke-dasharray="6 6"')
    body += poly([(286, 34), (298, 44), (282, 46)], "#ffffff", 'opacity="0.6"')
    for x, lifted in ((96, False), (176, True), (256, False)):
        y = 70 if lifted else 100
        body += path(f"M{x - 26} {y + 50} L{x - 18} {y} L{x + 18} {y} L{x + 26} {y + 50} Z", "#c0392b", 'stroke="#7a1a1a" stroke-width="2"')
        body += ellipse(x, y + 50, 26, 6, "#a02a1a") + rect(x - 22, y + 12, 44, 6, "#d4af37")
        if lifted:
            body += ellipse(x, 150, 22, 5, "#3a0a1a", 'opacity="0.6"') + lettering("?", x, 128, 14, "#ffffff")
    # A tiny soldier peeking out under the right cup
    body += circle(276, 146, 6, SKIN[1]) + path("M268 144 C 268 134 284 134 284 144 Z", "#55603a")
    # The white gloves
    body += path("M10 70 C 30 60 56 62 70 74 L64 86 C 50 80 30 82 16 90 Z", "#ffffff", 'stroke="#aaa" stroke-width="2"')
    body += path("M342 46 C 322 36 296 40 286 52 L292 64 C 306 56 326 58 340 66 Z", "#ffffff", 'stroke="#aaa" stroke-width="2"')
    body += stroke("M70 74 L80 70 M286 52 L276 48", "#ffffff", 5)
    return svg(defs, body)


def short_king():
    # A short man in a paper crown standing on a stack of phone books to take a selfie at arm's length, the profile on his
    # phone screen reading 6 FT
    defs, body = bg_digital()
    for k in range(5):  # The phone books
        body += rect(120, 168 - k * 12, 110, 12, ("#f2c94c", "#e8e0c8", "#f2c94c", "#e8e0c8", "#f2c94c")[k], 'stroke="#8a7a3a" stroke-width="1.5"')
    body += path("M146 108 L146 120 L156 120 L156 108 Z M194 108 L194 120 L204 120 L204 108 Z", "#2a2a3a")
    body += person(176, 116, 0.9, SKIN[1], "#e0457b", face="smirk", hair="#1a1a1a")
    body += poly([(160, 42), (166, 30), (172, 40), (176, 26), (180, 40), (186, 30), (192, 42)], "#ffe45c", 'stroke="#c89a1a" stroke-width="1.5"')
    body += stroke("M196 84 L250 50", "#e0457b", 7) + circle(252, 48, 4, SKIN[1])
    body += phone(268, 46, 1.3, "#ffffff")
    body += circle(268, 36, 7, SKIN[1]) + lettering("6 FT", 268, 50, 6, "#2a3d8f")
    for k, (x, y) in enumerate(((300, 100), (316, 80), (290, 130))):
        body += heart(x, y, 0.9 + k * 0.2, "#ff5a8a")
    return svg(defs, body)


def content_creator():
    # A streamer in a headset under a ring light screaming at the camera with a shocked face and a giant red arrow pointing
    # at nothing, the thumbnail on the monitor behind showing the same face, 100K in gold
    defs, body = bg_digital()
    body += monitor(196, 10, 140, 92, "#2a1a3a")
    body += circle(240, 50, 18, SKIN[0]) + ellipse(240, 58, 5, 6, "#5a1a1a") + circle(233, 46, 3, "#222") + circle(247, 46, 3, "#222")
    body += lettering("100K", 296, 40, 12, "#ffd84a", outline="#7a5a10")
    body += poly([(150, 40), (190, 30), (186, 44), (226, 46), (226, 56), (186, 58), (190, 72)], "#ff2a2a", 'stroke="#7a0a0a" stroke-width="2"')
    body += circle(80, 80, 44, "none", 'stroke="#ffffff" stroke-width="8" opacity="0.9"') + circle(80, 80, 44, "none", 'stroke="#fff6c0" stroke-width="16" opacity="0.25"')
    body += person(80, 200, 1.4, SKIN[0], "#7a3aa0", face="shout", hair="#d0342c", brows="sad")
    body += path("M50 100 C 50 70 110 70 110 100", "none", 'stroke="#1a1a22" stroke-width="5"') + rect(44, 96, 10, 16, "#1a1a22", 'rx="3"') + rect(106, 96, 10, 16, "#1a1a22", 'rx="3"')
    body += stroke("M108 110 L120 126", "#1a1a22", 3) + circle(122, 128, 3, "#1a1a22")
    return svg(defs, body)


def thirst_trap():
    # A steel bear trap open on a beach towel, a ring light and a phone on a tripod in front of it, the phone screen
    # filling with hearts, a glass of water standing beside it
    defs, body = bg_digital()
    body += path("M20 140 L300 120 L340 190 L40 200 Z", "#ff8ab0", 'stroke="#ffffff" stroke-width="2"')
    for k in range(5):
        body += stroke(f"M{30 + k * 10} {150 + k * 10} L{306 + k * 7} {130 + k * 12}", "#ffffff", 3, 'opacity="0.6"')
    # The bear trap, open
    body += ellipse(170, 166, 64, 14, "#6a6a72", 'stroke="#3a3a42" stroke-width="3"')
    for k in range(9):
        x = 116 + k * 13
        body += poly([(x, 160), (x + 6, 132 - (k % 2) * 4), (x + 12, 160)], "#b8b8c4", 'stroke="#5a5a66" stroke-width="1.5"')
        body += poly([(x, 172), (x + 6, 194), (x + 12, 172)], "#9a9aa6", 'stroke="#5a5a66" stroke-width="1.5"')
    body += rect(160, 160, 20, 10, "#c8964a", 'rx="2"')
    # Ring light and phone on a tripod
    body += stroke("M262 132 L250 196 M262 132 L274 196 M262 132 L262 196", "#2a2a30", 3)
    body += circle(262, 70, 34, "none", 'stroke="#ffffff" stroke-width="7"') + phone(262, 76, 1.2, "#ffffff")
    for k, (x, y) in enumerate(((256, 66), (268, 72), (258, 84), (266, 58))):
        body += heart(x, y, 0.5, "#ff2a6a")
    body += rect(56, 100, 22, 40, "#bfe8ff", 'opacity="0.7" stroke="#ffffff" stroke-width="2"') + rect(58, 112, 18, 26, "#7ac8f0", 'opacity="0.8"')
    return svg(defs, body)


def overtime():
    # An office worker at a desk late at night, surrounded by empty coffee cups and energy drink cans, typing furiously with
    # smoke rising from the keyboard, eyes bloodshot and wide open, a wall clock showing 3 a.m. and a calendar with every
    # weekend crossed out
    defs = lin("bg", ["#141a2e", "#2a2440"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += circle(60, 44, 26, "#f4ecd8", 'stroke="#555" stroke-width="3"') + stroke("M60 44 L60 26 M60 44 L72 50", "#222", 3)
    body += lettering("3AM", 60, 76, 7, "#ff5a5a")
    body += rect(268, 14, 70, 66, "#ffffff", 'stroke="#555" stroke-width="2"') + rect(268, 14, 70, 12, "#c0392b")
    for row in range(4):
        for col in range(7):
            x, y = 272 + col * 9.5, 30 + row * 12
            body += rect(x, y, 7, 8, "#e8e8e8")
            if col >= 5:
                body += stroke(f"M{x} {y} L{x + 7} {y + 8} M{x + 7} {y} L{x} {y + 8}", "#c0392b", 1.5)
    body += rect(0, 160, W, 40, "#4a3a2a")
    body += monitor(150, 60, 120, 80, "#bfe8ff")
    body += rect(150, 150, 110, 12, "#2a2a30", 'rx="2"')
    for k in range(3):
        body += stroke(f"M{170 + k * 30} 150 q -6 -12 0 -22 q 6 -10 0 -20", "#c8c8d0", 2.5, 'opacity="0.7"')
    body += person(110, 200, 1.3, SKIN[0], "#3d5a80", face="wavy", hair="#3a2a1a", tie="#c0392b")
    body += circle(101, 115, 5, "#ffffff", 'stroke="#ff3a3a" stroke-width="1.5"') + circle(119, 115, 5, "#ffffff", 'stroke="#ff3a3a" stroke-width="1.5"')
    body += circle(101, 115, 1.8, "#222") + circle(119, 115, 1.8, "#222")
    body += stroke("M130 150 L160 152 M126 158 L170 158", "#3d5a80", 8)
    for x in (20, 36, 300, 316, 330):  # The cups and cans
        body += rect(x, 142, 12, 18, "#ffffff" if x < 100 else "#3aa86a", 'rx="2" stroke="#555" stroke-width="1"')
    return svg(defs, body)


def taqiyya():
    # A serene robed figure bowing slightly and offering an ornate gift box tied with a gold ribbon, both hands extended in
    # welcome; on the wall behind, the box's shadow is a coiled snake
    defs, body = bg_myth()
    body += rect(0, 170, W, 30, "#5a3a2a")
    # The shadow on the wall: a coiled snake
    body += path("M230 150 C 200 150 196 120 226 116 C 256 112 262 90 236 84 C 210 78 206 60 226 52", "none", 'stroke="#1a0a2a" stroke-width="16" stroke-linecap="round" opacity="0.55"')
    body += ellipse(232, 48, 14, 9, "#1a0a2a", 'opacity="0.55"') + stroke("M244 50 l 10 -2 l -4 4 l 6 2", "#1a0a2a", 2, 'opacity="0.55"')
    # The figure, robed, bowing slightly
    body += path("M60 200 C 64 140 90 110 128 112 C 160 114 176 150 178 200 Z", "#e8e0d0", 'stroke="#a89878" stroke-width="2"')
    body += circle(126, 86, 20, SKIN[2]) + path("M104 92 C 100 60 152 60 148 92 C 142 78 110 78 104 92 Z", "#f6f2ea", 'stroke="#c8c0b0" stroke-width="2"')
    body += path("M110 96 C 114 116 138 116 142 96 C 136 108 116 108 110 96 Z", "#3a2a1a")
    body += stroke("M118 86 q 3 2 6 0 M130 86 q 3 2 6 0", "#222", 1.6) + stroke("M120 100 q 6 4 12 0", "#222", 1.6)
    body += stroke("M150 150 L186 136 M140 160 L186 150", "#e8e0d0", 10)
    # The gift box, gold ribbon
    body += rect(186, 116, 46, 40, "#8a1a3a", 'stroke="#5a0a2a" stroke-width="2"') + rect(204, 116, 10, 40, "#d4af37") + rect(186, 130, 46, 8, "#d4af37")
    body += ellipse(202, 112, 10, 6, "#d4af37") + ellipse(216, 112, 10, 6, "#d4af37")
    return svg(defs, body)


def geomagnetic_reversal():
    # A globe with its magnetic field lines flipping over, the N and S labels swapping places mid-air, a scientist staring
    # at a compass whose needle spins wildly, and a flock of birds flying in confused loops
    defs, body = bg_utopia()
    defs += rad("earth", [("#5aa8e0", 1), ("#1a5a9a", 1)])
    body += circle(150, 96, 52, "url(#earth)", 'stroke="#0a3a6a" stroke-width="2"')
    body += path("M118 70 C 130 60 150 66 146 82 C 140 96 120 92 118 70 Z M156 104 C 170 96 186 110 178 126 C 168 138 152 126 156 104 Z", "#3aa86a")
    for k, r in enumerate((70, 88)):
        body += path(f"M150 {96 - 52} C {150 - r} {96 - 70} {150 - r} {96 + 70} 150 {96 + 52}", "none", 'stroke="#d0342c" stroke-width="2.5" opacity="0.7" stroke-dasharray="5 4"')
        body += path(f"M150 {96 - 52} C {150 + r} {96 - 70} {150 + r} {96 + 70} 150 {96 + 52}", "none", 'stroke="#2a6ad0" stroke-width="2.5" opacity="0.7" stroke-dasharray="5 4"')
    body += lettering("S", 150, 14, 14, "#2a6ad0") + lettering("N", 150, 158, 14, "#d0342c")
    body += stroke("M170 22 C 186 34 186 150 172 166", "none", 1) + path("M176 20 C 200 50 200 140 178 168", "none", 'stroke="#555" stroke-width="2" stroke-dasharray="3 4"')
    body += poly([(178, 168), (172, 158), (184, 160)], "#555")
    # The scientist with the compass
    body += person(290, 200, 1.1, SKIN[0], "#ffffff", face="shout", hair=OLD_HAIR, glasses="round", brows="sad")
    body += circle(256, 150, 16, "#f4ecd8", 'stroke="#8a6a3a" stroke-width="3"')
    body += stroke("M244 142 L268 158", "#d0342c", 3) + stroke("M244 158 L268 142", "#2a6ad0", 2, 'opacity="0.6"')
    body += stroke("M270 146 a 18 18 0 0 1 4 12", "#555", 1.5) + stroke("M240 164 a 18 18 0 0 1 -4 -12", "#555", 1.5)
    # The birds, looping
    for k, (x, y) in enumerate(((40, 40), (70, 26), (60, 60), (300, 30), (326, 52))):
        body += stroke(f"M{x - 7} {y} q 4 -6 7 0 q 3 -6 7 0", "#2a2a3a", 2)
    body += stroke("M30 80 C 10 60 60 20 90 40 C 110 54 80 80 60 70", "none", 1) + path("M30 80 C 10 60 60 20 90 40 C 110 54 80 80 60 70", "none", 'stroke="#2a2a3a" stroke-width="1.2" stroke-dasharray="2 4" opacity="0.6"')
    return svg(defs, body)


CHUNK_NEW = {"Tech": {"ThreatHunter": threat_hunter, "CtrlZ": ctrl_z, "PairProgramming": pair_programming,
                      "SuspiciousUSBStickintheParkingLot": suspicious_usb_stick_in_the_parking_lot},
             "Mind": {"Socrates": socrates, "MaslowsPyramid": maslows_pyramid, "Legerdemain": legerdemain},
             "DigitalSociety": {"ShortKing": short_king, "ContentCreator": content_creator, "ThirstTrap": thirst_trap},
             "Corporate": {"Overtime": overtime}, "Myth": {"Taqiyya": taqiyya}, "Utopia": {"GeomagneticReversal": geomagnetic_reversal}}
for _theme, _cards in CHUNK_NEW.items():
    CARDS[_theme].update(_cards)


# ---------------------------------------------------------------- New cards (2026-10-16): the smaller factions, and three without art

def heart(cx, cy, s, color, extra=""):
    return path(f"M{cx:.1f} {cy + 6 * s:.1f} C {cx - 10 * s:.1f} {cy - 2 * s:.1f} {cx - 8 * s:.1f} {cy - 10 * s:.1f} {cx:.1f} {cy - 5 * s:.1f} "
                f"C {cx + 8 * s:.1f} {cy - 10 * s:.1f} {cx + 10 * s:.1f} {cy - 2 * s:.1f} {cx:.1f} {cy + 6 * s:.1f} Z", color, extra)


def threat_hunter():
    # A security analyst in a headlamp crawling through a tunnel of server cables with a magnifying glass, following
    # glowing red footprints to a tiny hooded hacker hiding behind a rack
    defs, body = bg_tech()
    defs += rad("lamp", [("#fff6c0", 0.55), ("#fff6c0", 0)])
    body += server_rack(270, 40, 70, 150)
    for k in range(7):  # The cables, hanging in a tunnel
        y = 6 + k * 6
        body += stroke(f"M0 {y} C 90 {y + 40} 200 {y + 30} 352 {y + 8}", ("#2a6ad0", "#d0342c", "#f2c94c", "#3aa86a")[k % 4], 4, 'opacity="0.85"')
    for k, x in enumerate((140, 170, 198, 224, 248)):  # Red footprints
        body += ellipse(x, 170 - (k % 2) * 8, 5, 3, "#ff3a3a", 'opacity="0.9"') + ellipse(x, 170 - (k % 2) * 8, 9, 6, "#ff3a3a", 'opacity="0.25"')
    # The hacker, small, peeking from behind the rack
    body += path("M258 186 C 256 160 266 150 276 150 C 286 150 292 160 290 186 Z", "#1a1a22")
    body += circle(274, 146, 11, "#1a1a22") + ellipse(272, 148, 6, 7, "#0a0a0f") + circle(270, 147, 1.5, "#ff3a3a") + circle(276, 147, 1.5, "#ff3a3a")
    # The analyst crawling, headlamp on
    body += poly([(88, 128), (260, 92), (260, 196), (88, 150)], "url(#lamp)")
    body += path("M20 176 C 30 150 70 142 100 150 L118 172 L24 190 Z", "#3d5a80")
    body += stroke("M96 168 L118 186 M34 182 L22 196", "#2a3a50", 10)
    body += circle(98, 132, 18, SKIN[1]) + path("M80 130 C 80 108 116 108 116 128 Z", "#3a2a1a")
    body += rect(80, 120, 36, 6, "#222") + circle(116, 124, 6, "#fff6c0", 'stroke="#555" stroke-width="2"')
    body += circle(104, 132, 2.2, "#222") + stroke("M100 142 l 8 0", "#222", 1.8)
    body += stroke("M118 150 L140 140", "#3d5a80", 9) + circle(150, 136, 13, "#bfe8ff", 'opacity="0.5" stroke="#8a6a3a" stroke-width="4"')
    body += stroke("M141 145 L132 154", "#8a6a3a", 5)
    return svg(defs, body)


def ctrl_z():
    # A huge curved UNDO arrow pulling a startled knight backwards off a battlefield into a playing card held up by a hand,
    # the keys CTRL and Z glowing on a keyboard below
    defs = lin("bg", ["#1b2a4a", "#0b1222"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(0, 150, W, 50, "#2a2a30") + rect(0, 150, W, 4, "#3a3a44")
    for i, x in enumerate(range(8, 352, 38)):  # The keyboard
        lit = i in (1, 6)
        body += rect(x, 160, 32, 26, "#4fc3ff" if lit else "#3a3a44", 'rx="4"' + (' opacity="0.95"' if lit else ""))
    body += lettering("CTRL", 62, 167, 8, "#0b1222") + lettering("Z", 252, 165, 10, "#0b1222")
    # The playing card held up, and the hand
    body += rect(34, 30, 70, 100, "#f4ecd8", 'rx="6" stroke="#8a7a5a" stroke-width="3"') + rect(42, 38, 54, 84, "none", 'stroke="#c0392b" stroke-width="2" rx="4"')
    body += path("M20 150 C 20 128 36 120 54 122 L80 122 C 90 122 92 134 82 136 L64 138 L60 150 Z", SKIN[0], 'stroke="#b08a6a" stroke-width="2"')
    # The undo arrow, from the knight back to the card
    body += path("M300 60 C 250 0 140 10 104 70", "none", 'stroke="#4fc3ff" stroke-width="16" stroke-linecap="round" opacity="0.85"')
    body += poly([(92, 52), (120, 66), (98, 90)], "#4fc3ff")
    body += knight(272, 142, 0.75, face_left=True)
    body += stroke("M240 120 l -18 -4 M240 132 l -20 2 M244 108 l -16 -10", "#ffffff", 3, 'opacity="0.7"')
    body += lettering("!", 300, 76, 18, "#ffe45c")
    return svg(defs, body)


def pair_programming():
    # Two developers squeezed at one tiny desk sharing one keyboard: one types with a coffee, the other leans over his
    # shoulder pointing at a bug on the screen; a timesheet between them bills 16 hours
    defs, body = bg_tech()
    body += monitor(124, 14, 120, 84, "#14202a")
    for k in range(6):
        body += rect(140, 30 + k * 10, 30 + (k * 23) % 60, 4, "#3fbf8a" if k != 3 else "#ff5a5a")
    body += circle(214, 61, 9, "none", 'stroke="#ff5a5a" stroke-width="2.5"')
    body += rect(90, 150, 190, 12, "#6a4a2a") + rect(140, 140, 80, 10, "#2a2a30", 'rx="2"')  # The tiny desk, one keyboard
    body += person(140, 200, 1.1, SKIN[0], "#3a6ad0", face="flat", hair="#3a2a1a", glasses="round")
    body += stroke("M150 160 L170 146", "#3a6ad0", 9) + rect(100, 120, 14, 20, "#ffffff", 'rx="2"') + stroke("M114 126 q 6 3 0 10", "#ffffff", 2.5)
    body += person(236, 186, 1.2, SKIN[2], "#d0342c", face="shout", hair="#1a1a1a", brows="angry")
    body += stroke("M248 130 L222 80", "#d0342c", 9) + circle(220, 76, 5, SKIN[2])
    body += rect(300, 96, 44, 56, "#ffffff", 'stroke="#555" stroke-width="2"') + lettering("16H", 322, 104, 9, "#c0392b")
    for k in range(3):
        body += rect(306, 120 + k * 8, 32, 3, "#aaaaaa")
    return svg(defs, body)


def suspicious_usb_stick_in_the_parking_lot():
    # A lone USB stick on the asphalt of an empty parking lot under a streetlight, labelled SALARIES 2026, an employee in a
    # lanyard bending down to pick it up, a nuclear plant's cooling tower behind the fence
    defs = lin("bg", ["#1a2236", "#3a3046"]) + rad("light", [("#ffe9a0", 0.55), ("#ffe9a0", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    body += path("M250 112 C 256 80 256 50 246 20 L318 20 C 308 50 308 80 314 112 Z", "#8a8a96")  # The cooling tower
    body += path("M262 20 C 270 6 300 6 306 20", "#c8c8d0", 'opacity="0.5"') + cloud(290, 4, 0.8, "#c8c8d0", 0.5)
    for x in range(0, 352, 16):  # The fence
        body += stroke(f"M{x} 96 L{x + 16} 116 M{x + 16} 96 L{x} 116", "#5a5a66", 1.2)
    body += stroke("M0 96 L352 96 M0 116 L352 116", "#5a5a66", 2)
    body += rect(0, 116, W, 84, "#2a2a30")
    for x in (40, 130, 220, 310):
        body += stroke(f"M{x} 130 L{x - 20} 200", "#e8e8e8", 3, 'opacity="0.6"')
    body += rect(196, 20, 6, 130, "#444") + rect(178, 18, 30, 8, "#555") + poly([(180, 26), (206, 26), (236, 196), (150, 196)], "url(#light)")
    # The stick, and its label
    body += rect(176, 168, 34, 12, "#1a1a22", 'rx="2"') + rect(210, 170, 10, 8, "#c8c8c8") + rect(180, 170, 26, 8, "#fff7e0")
    body += lettering("SALARIES", 193, 171, 3.2, "#c0392b", width=0.9)
    # The employee, bending down to it
    body += path("M70 196 C 70 150 100 120 130 120 L150 130 L120 196 Z", "#5a7a9a")
    body += circle(150, 120, 15, SKIN[1]) + path("M136 116 C 138 102 162 102 164 116 Z", "#3a2a1a")
    body += circle(156, 120, 2, "#222") + stroke("M152 128 l 6 -1", "#222", 1.6)
    body += stroke("M130 132 L170 168", "#5a7a9a", 8) + circle(172, 170, 5, SKIN[1])
    body += stroke("M140 134 L150 156", "#2a6ad0", 2) + rect(146, 154, 10, 12, "#ffffff", 'stroke="#2a6ad0" stroke-width="1.5"')
    return svg(defs, body)


def socrates():
    # Socrates in a white toga, bald with a big beard, cornering a sweating man in a suit against a marble column with one
    # raised finger and a huge question mark; a cup of hemlock waits on a pedestal behind
    defs, body = bg_mind()
    body += rect(0, 168, W, 32, "#e8dcc8")
    body += rect(260, 20, 40, 150, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"') + rect(252, 12, 56, 12, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"')
    for x in (270, 280, 290):
        body += stroke(f"M{x} 26 L{x} 166", "#d8d0c0", 2)
    body += person(282, 196, 1.0, SKIN[0], "#2a2a3a", face="wavy", hair="#5a3a1a", tie="#c0392b", brows="sad")
    body += sweat(304, 120) + sweat(262, 116, 0.8)
    body += rect(30, 120, 30, 50, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"')  # The pedestal and the hemlock
    body += path("M34 120 L56 120 L52 104 L38 104 Z", "#8a6a3a") + ellipse(45, 104, 8, 3, "#3a6a2a")
    # Socrates: toga, bald, big beard, a finger raised
    body += path("M96 200 C 96 140 120 118 160 118 C 200 118 222 140 222 200 Z", "#f6f2ea", 'stroke="#c8c0b0" stroke-width="2"')
    body += stroke("M110 130 C 140 160 180 150 214 190", "#d8d0c0", 3)
    body += circle(160, 82, 26, SKIN[0]) + circle(134, 84, 6, SKIN[0]) + circle(186, 84, 6, SKIN[0])
    body += circle(138, 70, 6, "#d8d8d8") + circle(182, 70, 6, "#d8d8d8")
    body += path("M136 88 C 136 130 184 130 184 88 C 176 102 144 102 136 88 Z", "#d8d8d8")
    body += circle(151, 80, 2.4, "#222") + circle(169, 80, 2.4, "#222") + stroke("M144 72 L156 74 M164 74 L176 72", "#888", 2)
    body += stroke("M210 150 L236 96", "#f6f2ea", 12) + stroke("M236 96 L240 74", SKIN[0], 6)
    body += lettering("?", 214, 14, 34, "#3a2a6a")
    return svg(defs, body)


def maslows_pyramid():
    # Maslow's pyramid as a stone pyramid in layers, tiny people at the bottom fighting over bread and water, one man
    # meditating smugly alone at the glowing top, and a WiFi symbol carved into a new layer under the base
    defs, body = bg_mind()
    defs += rad("glow", [("#fff6c0", 0.9), ("#fff6c0", 0)])
    body += circle(176, 30, 46, "url(#glow)")
    layers = [(150, 186, "#a89878"), (118, 154, "#b8a888"), (86, 122, "#c8b898"), (56, 90, "#d8c8a8"), (30, 60, "#e8d8b8")]
    for top, bottom, color in layers:
        half_t, half_b = (top - 20) * 0.95, (bottom - 20) * 0.95
        body += poly([(176 - half_t, top), (176 + half_t, top), (176 + half_b, bottom), (176 - half_b, bottom)], color, 'stroke="#7a6a50" stroke-width="2"')
    body += rect(14, 186, 324, 14, "#6a5a40") + stroke("M176 196 m -14 0 a 14 14 0 0 1 28 0 M176 196 m -8 0 a 8 8 0 0 1 16 0", "#4fc3ff", 2.5) + circle(176, 196, 2, "#4fc3ff")
    # The smug one at the top, cross-legged
    body += ellipse(176, 28, 14, 6, "#e86a3a") + circle(176, 14, 8, SKIN[0]) + stroke("M172 14 q 2 2 4 0 M178 14 q 2 2 4 0", "#222", 1.2)
    body += stroke("M162 26 l -8 -6 M190 26 l 8 -6", "#e86a3a", 4)
    # The crowd at the bottom, fighting over bread and water
    for k, x in enumerate((50, 76, 104, 248, 276, 302)):
        body += circle(x, 162, 7, SKIN[k % 4]) + rect(x - 6, 168, 12, 16, ("#3a6ad0", "#d0342c", "#3aa86a")[k % 3], 'rx="3"')
        body += stroke(f"M{x - 6} 172 l {-6 if k % 2 else 6} -8", ("#3a6ad0", "#d0342c", "#3aa86a")[k % 3], 3)
    body += ellipse(90, 152, 12, 6, "#c8964a") + rect(286, 142, 8, 14, "#7ac8f0", 'rx="2"')
    return svg(defs, body)


def legerdemain():
    # A magician's white-gloved hands doing the cups-and-ball trick on a velvet table, tiny soldiers under the cups being
    # shuffled, one cup lifted to show the soldier is gone
    defs, body = bg_mind()
    body += path("M0 120 L352 120 L352 200 L0 200 Z", "#5a1a3a") + rect(0, 120, W, 6, "#7a2a5a")
    body += stroke("M60 40 C 120 10 230 10 290 40", "#ffffff", 3, 'opacity="0.6" stroke-dasharray="6 6"')
    body += poly([(286, 34), (298, 44), (282, 46)], "#ffffff", 'opacity="0.6"')
    for x, lifted in ((96, False), (176, True), (256, False)):
        y = 70 if lifted else 100
        body += path(f"M{x - 26} {y + 50} L{x - 18} {y} L{x + 18} {y} L{x + 26} {y + 50} Z", "#c0392b", 'stroke="#7a1a1a" stroke-width="2"')
        body += ellipse(x, y + 50, 26, 6, "#a02a1a") + rect(x - 22, y + 12, 44, 6, "#d4af37")
        if lifted:
            body += ellipse(x, 150, 22, 5, "#3a0a1a", 'opacity="0.6"') + lettering("?", x, 128, 14, "#ffffff")
    # A tiny soldier peeking out under the right cup
    body += circle(276, 146, 6, SKIN[1]) + path("M268 144 C 268 134 284 134 284 144 Z", "#55603a")
    # The white gloves
    body += path("M10 70 C 30 60 56 62 70 74 L64 86 C 50 80 30 82 16 90 Z", "#ffffff", 'stroke="#aaa" stroke-width="2"')
    body += path("M342 46 C 322 36 296 40 286 52 L292 64 C 306 56 326 58 340 66 Z", "#ffffff", 'stroke="#aaa" stroke-width="2"')
    body += stroke("M70 74 L80 70 M286 52 L276 48", "#ffffff", 5)
    return svg(defs, body)


def short_king():
    # A short man in a paper crown standing on a stack of phone books to take a selfie at arm's length, the profile on his
    # phone screen reading 6 FT
    defs, body = bg_digital()
    for k in range(5):  # The phone books
        body += rect(120, 168 - k * 12, 110, 12, ("#f2c94c", "#e8e0c8", "#f2c94c", "#e8e0c8", "#f2c94c")[k], 'stroke="#8a7a3a" stroke-width="1.5"')
    body += path("M146 108 L146 120 L156 120 L156 108 Z M194 108 L194 120 L204 120 L204 108 Z", "#2a2a3a")
    body += person(176, 116, 0.9, SKIN[1], "#e0457b", face="smirk", hair="#1a1a1a")
    body += poly([(160, 42), (166, 30), (172, 40), (176, 26), (180, 40), (186, 30), (192, 42)], "#ffe45c", 'stroke="#c89a1a" stroke-width="1.5"')
    body += stroke("M196 84 L250 50", "#e0457b", 7) + circle(252, 48, 4, SKIN[1])
    body += phone(268, 46, 1.3, "#ffffff")
    body += circle(268, 36, 7, SKIN[1]) + lettering("6 FT", 268, 50, 6, "#2a3d8f")
    for k, (x, y) in enumerate(((300, 100), (316, 80), (290, 130))):
        body += heart(x, y, 0.9 + k * 0.2, "#ff5a8a")
    return svg(defs, body)


def content_creator():
    # A streamer in a headset under a ring light screaming at the camera with a shocked face and a giant red arrow pointing
    # at nothing, the thumbnail on the monitor behind showing the same face, 100K in gold
    defs, body = bg_digital()
    body += monitor(196, 10, 140, 92, "#2a1a3a")
    body += circle(240, 50, 18, SKIN[0]) + ellipse(240, 58, 5, 6, "#5a1a1a") + circle(233, 46, 3, "#222") + circle(247, 46, 3, "#222")
    body += lettering("100K", 296, 40, 12, "#ffd84a", outline="#7a5a10")
    body += poly([(150, 40), (190, 30), (186, 44), (226, 46), (226, 56), (186, 58), (190, 72)], "#ff2a2a", 'stroke="#7a0a0a" stroke-width="2"')
    body += circle(80, 80, 44, "none", 'stroke="#ffffff" stroke-width="8" opacity="0.9"') + circle(80, 80, 44, "none", 'stroke="#fff6c0" stroke-width="16" opacity="0.25"')
    body += person(80, 200, 1.4, SKIN[0], "#7a3aa0", face="shout", hair="#d0342c", brows="sad")
    body += path("M50 100 C 50 70 110 70 110 100", "none", 'stroke="#1a1a22" stroke-width="5"') + rect(44, 96, 10, 16, "#1a1a22", 'rx="3"') + rect(106, 96, 10, 16, "#1a1a22", 'rx="3"')
    body += stroke("M108 110 L120 126", "#1a1a22", 3) + circle(122, 128, 3, "#1a1a22")
    return svg(defs, body)


def thirst_trap():
    # A steel bear trap open on a beach towel, a ring light and a phone on a tripod in front of it, the phone screen
    # filling with hearts, a glass of water standing beside it
    defs, body = bg_digital()
    body += path("M20 140 L300 120 L340 190 L40 200 Z", "#ff8ab0", 'stroke="#ffffff" stroke-width="2"')
    for k in range(5):
        body += stroke(f"M{30 + k * 10} {150 + k * 10} L{306 + k * 7} {130 + k * 12}", "#ffffff", 3, 'opacity="0.6"')
    # The bear trap, open
    body += ellipse(170, 166, 64, 14, "#6a6a72", 'stroke="#3a3a42" stroke-width="3"')
    for k in range(9):
        x = 116 + k * 13
        body += poly([(x, 160), (x + 6, 132 - (k % 2) * 4), (x + 12, 160)], "#b8b8c4", 'stroke="#5a5a66" stroke-width="1.5"')
        body += poly([(x, 172), (x + 6, 194), (x + 12, 172)], "#9a9aa6", 'stroke="#5a5a66" stroke-width="1.5"')
    body += rect(160, 160, 20, 10, "#c8964a", 'rx="2"')
    # Ring light and phone on a tripod
    body += stroke("M262 132 L250 196 M262 132 L274 196 M262 132 L262 196", "#2a2a30", 3)
    body += circle(262, 70, 34, "none", 'stroke="#ffffff" stroke-width="7"') + phone(262, 76, 1.2, "#ffffff")
    for k, (x, y) in enumerate(((256, 66), (268, 72), (258, 84), (266, 58))):
        body += heart(x, y, 0.5, "#ff2a6a")
    body += rect(56, 100, 22, 40, "#bfe8ff", 'opacity="0.7" stroke="#ffffff" stroke-width="2"') + rect(58, 112, 18, 26, "#7ac8f0", 'opacity="0.8"')
    return svg(defs, body)


def overtime():
    # An office worker at a desk late at night, surrounded by empty coffee cups and energy drink cans, typing furiously with
    # smoke rising from the keyboard, eyes bloodshot and wide open, a wall clock showing 3 a.m. and a calendar with every
    # weekend crossed out
    defs = lin("bg", ["#141a2e", "#2a2440"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += circle(60, 44, 26, "#f4ecd8", 'stroke="#555" stroke-width="3"') + stroke("M60 44 L60 26 M60 44 L72 50", "#222", 3)
    body += lettering("3AM", 60, 76, 7, "#ff5a5a")
    body += rect(268, 14, 70, 66, "#ffffff", 'stroke="#555" stroke-width="2"') + rect(268, 14, 70, 12, "#c0392b")
    for row in range(4):
        for col in range(7):
            x, y = 272 + col * 9.5, 30 + row * 12
            body += rect(x, y, 7, 8, "#e8e8e8")
            if col >= 5:
                body += stroke(f"M{x} {y} L{x + 7} {y + 8} M{x + 7} {y} L{x} {y + 8}", "#c0392b", 1.5)
    body += rect(0, 160, W, 40, "#4a3a2a")
    body += monitor(150, 60, 120, 80, "#bfe8ff")
    body += rect(150, 150, 110, 12, "#2a2a30", 'rx="2"')
    for k in range(3):
        body += stroke(f"M{170 + k * 30} 150 q -6 -12 0 -22 q 6 -10 0 -20", "#c8c8d0", 2.5, 'opacity="0.7"')
    body += person(110, 200, 1.3, SKIN[0], "#3d5a80", face="wavy", hair="#3a2a1a", tie="#c0392b")
    body += circle(101, 115, 5, "#ffffff", 'stroke="#ff3a3a" stroke-width="1.5"') + circle(119, 115, 5, "#ffffff", 'stroke="#ff3a3a" stroke-width="1.5"')
    body += circle(101, 115, 1.8, "#222") + circle(119, 115, 1.8, "#222")
    body += stroke("M130 150 L160 152 M126 158 L170 158", "#3d5a80", 8)
    for x in (20, 36, 300, 316, 330):  # The cups and cans
        body += rect(x, 142, 12, 18, "#ffffff" if x < 100 else "#3aa86a", 'rx="2" stroke="#555" stroke-width="1"')
    return svg(defs, body)


def taqiyya():
    # A serene robed figure bowing slightly and offering an ornate gift box tied with a gold ribbon, both hands extended in
    # welcome; on the wall behind, the box's shadow is a coiled snake
    defs, body = bg_myth()
    body += rect(0, 170, W, 30, "#5a3a2a")
    # The shadow on the wall: a coiled snake
    body += path("M230 150 C 200 150 196 120 226 116 C 256 112 262 90 236 84 C 210 78 206 60 226 52", "none", 'stroke="#1a0a2a" stroke-width="16" stroke-linecap="round" opacity="0.55"')
    body += ellipse(232, 48, 14, 9, "#1a0a2a", 'opacity="0.55"') + stroke("M244 50 l 10 -2 l -4 4 l 6 2", "#1a0a2a", 2, 'opacity="0.55"')
    # The figure, robed, bowing slightly
    body += path("M60 200 C 64 140 90 110 128 112 C 160 114 176 150 178 200 Z", "#e8e0d0", 'stroke="#a89878" stroke-width="2"')
    body += circle(126, 86, 20, SKIN[2]) + path("M104 92 C 100 60 152 60 148 92 C 142 78 110 78 104 92 Z", "#f6f2ea", 'stroke="#c8c0b0" stroke-width="2"')
    body += path("M110 96 C 114 116 138 116 142 96 C 136 108 116 108 110 96 Z", "#3a2a1a")
    body += stroke("M118 86 q 3 2 6 0 M130 86 q 3 2 6 0", "#222", 1.6) + stroke("M120 100 q 6 4 12 0", "#222", 1.6)
    body += stroke("M150 150 L186 136 M140 160 L186 150", "#e8e0d0", 10)
    # The gift box, gold ribbon
    body += rect(186, 116, 46, 40, "#8a1a3a", 'stroke="#5a0a2a" stroke-width="2"') + rect(204, 116, 10, 40, "#d4af37") + rect(186, 130, 46, 8, "#d4af37")
    body += ellipse(202, 112, 10, 6, "#d4af37") + ellipse(216, 112, 10, 6, "#d4af37")
    return svg(defs, body)


def geomagnetic_reversal():
    # A globe with its magnetic field lines flipping over, the N and S labels swapping places mid-air, a scientist staring
    # at a compass whose needle spins wildly, and a flock of birds flying in confused loops
    defs, body = bg_utopia()
    defs += rad("earth", [("#5aa8e0", 1), ("#1a5a9a", 1)])
    body += circle(150, 96, 52, "url(#earth)", 'stroke="#0a3a6a" stroke-width="2"')
    body += path("M118 70 C 130 60 150 66 146 82 C 140 96 120 92 118 70 Z M156 104 C 170 96 186 110 178 126 C 168 138 152 126 156 104 Z", "#3aa86a")
    for k, r in enumerate((70, 88)):
        body += path(f"M150 {96 - 52} C {150 - r} {96 - 70} {150 - r} {96 + 70} 150 {96 + 52}", "none", 'stroke="#d0342c" stroke-width="2.5" opacity="0.7" stroke-dasharray="5 4"')
        body += path(f"M150 {96 - 52} C {150 + r} {96 - 70} {150 + r} {96 + 70} 150 {96 + 52}", "none", 'stroke="#2a6ad0" stroke-width="2.5" opacity="0.7" stroke-dasharray="5 4"')
    body += lettering("S", 150, 14, 14, "#2a6ad0") + lettering("N", 150, 158, 14, "#d0342c")
    body += stroke("M170 22 C 186 34 186 150 172 166", "none", 1) + path("M176 20 C 200 50 200 140 178 168", "none", 'stroke="#555" stroke-width="2" stroke-dasharray="3 4"')
    body += poly([(178, 168), (172, 158), (184, 160)], "#555")
    # The scientist with the compass
    body += person(290, 200, 1.1, SKIN[0], "#ffffff", face="shout", hair=OLD_HAIR, glasses="round", brows="sad")
    body += circle(256, 150, 16, "#f4ecd8", 'stroke="#8a6a3a" stroke-width="3"')
    body += stroke("M244 142 L268 158", "#d0342c", 3) + stroke("M244 158 L268 142", "#2a6ad0", 2, 'opacity="0.6"')
    body += stroke("M270 146 a 18 18 0 0 1 4 12", "#555", 1.5) + stroke("M240 164 a 18 18 0 0 1 -4 -12", "#555", 1.5)
    # The birds, looping
    for k, (x, y) in enumerate(((40, 40), (70, 26), (60, 60), (300, 30), (326, 52))):
        body += stroke(f"M{x - 7} {y} q 4 -6 7 0 q 3 -6 7 0", "#2a2a3a", 2)
    body += stroke("M30 80 C 10 60 60 20 90 40 C 110 54 80 80 60 70", "none", 1) + path("M30 80 C 10 60 60 20 90 40 C 110 54 80 80 60 70", "none", 'stroke="#2a2a3a" stroke-width="1.2" stroke-dasharray="2 4" opacity="0.6"')
    return svg(defs, body)


CHUNK_NEW = {"Tech": {"ThreatHunter": threat_hunter, "CtrlZ": ctrl_z, "PairProgramming": pair_programming,
                      "SuspiciousUSBStickintheParkingLot": suspicious_usb_stick_in_the_parking_lot},
             "Mind": {"Socrates": socrates, "MaslowsPyramid": maslows_pyramid, "Legerdemain": legerdemain},
             "DigitalSociety": {"ShortKing": short_king, "ContentCreator": content_creator, "ThirstTrap": thirst_trap},
             "Corporate": {"Overtime": overtime}, "Myth": {"Taqiyya": taqiyya}, "Utopia": {"GeomagneticReversal": geomagnetic_reversal}}
for _theme, _cards in CHUNK_NEW.items():
    CARDS[_theme].update(_cards)


# ---------------------------------------------------------------- New cards (2026-10-16): the smaller factions, and three without art

def heart(cx, cy, s, color, extra=""):
    return path(f"M{cx:.1f} {cy + 6 * s:.1f} C {cx - 10 * s:.1f} {cy - 2 * s:.1f} {cx - 8 * s:.1f} {cy - 10 * s:.1f} {cx:.1f} {cy - 5 * s:.1f} "
                f"C {cx + 8 * s:.1f} {cy - 10 * s:.1f} {cx + 10 * s:.1f} {cy - 2 * s:.1f} {cx:.1f} {cy + 6 * s:.1f} Z", color, extra)


def threat_hunter():
    # A security analyst in a headlamp crawling through a tunnel of server cables with a magnifying glass, following
    # glowing red footprints to a tiny hooded hacker hiding behind a rack
    defs, body = bg_tech()
    defs += rad("lamp", [("#fff6c0", 0.55), ("#fff6c0", 0)])
    body += server_rack(270, 40, 70, 150)
    for k in range(7):  # The cables, hanging in a tunnel
        y = 6 + k * 6
        body += stroke(f"M0 {y} C 90 {y + 40} 200 {y + 30} 352 {y + 8}", ("#2a6ad0", "#d0342c", "#f2c94c", "#3aa86a")[k % 4], 4, 'opacity="0.85"')
    for k, x in enumerate((140, 170, 198, 224, 248)):  # Red footprints
        body += ellipse(x, 170 - (k % 2) * 8, 5, 3, "#ff3a3a", 'opacity="0.9"') + ellipse(x, 170 - (k % 2) * 8, 9, 6, "#ff3a3a", 'opacity="0.25"')
    # The hacker, small, peeking from behind the rack
    body += path("M258 186 C 256 160 266 150 276 150 C 286 150 292 160 290 186 Z", "#1a1a22")
    body += circle(274, 146, 11, "#1a1a22") + ellipse(272, 148, 6, 7, "#0a0a0f") + circle(270, 147, 1.5, "#ff3a3a") + circle(276, 147, 1.5, "#ff3a3a")
    # The analyst crawling, headlamp on
    body += poly([(88, 128), (260, 92), (260, 196), (88, 150)], "url(#lamp)")
    body += path("M20 176 C 30 150 70 142 100 150 L118 172 L24 190 Z", "#3d5a80")
    body += stroke("M96 168 L118 186 M34 182 L22 196", "#2a3a50", 10)
    body += circle(98, 132, 18, SKIN[1]) + path("M80 130 C 80 108 116 108 116 128 Z", "#3a2a1a")
    body += rect(80, 120, 36, 6, "#222") + circle(116, 124, 6, "#fff6c0", 'stroke="#555" stroke-width="2"')
    body += circle(104, 132, 2.2, "#222") + stroke("M100 142 l 8 0", "#222", 1.8)
    body += stroke("M118 150 L140 140", "#3d5a80", 9) + circle(150, 136, 13, "#bfe8ff", 'opacity="0.5" stroke="#8a6a3a" stroke-width="4"')
    body += stroke("M141 145 L132 154", "#8a6a3a", 5)
    return svg(defs, body)


def ctrl_z():
    # A huge curved UNDO arrow pulling a startled knight backwards off a battlefield into a playing card held up by a hand,
    # the keys CTRL and Z glowing on a keyboard below
    defs = lin("bg", ["#1b2a4a", "#0b1222"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(0, 150, W, 50, "#2a2a30") + rect(0, 150, W, 4, "#3a3a44")
    for i, x in enumerate(range(8, 352, 38)):  # The keyboard
        lit = i in (1, 6)
        body += rect(x, 160, 32, 26, "#4fc3ff" if lit else "#3a3a44", 'rx="4"' + (' opacity="0.95"' if lit else ""))
    body += lettering("CTRL", 62, 167, 8, "#0b1222") + lettering("Z", 252, 165, 10, "#0b1222")
    # The playing card held up, and the hand
    body += rect(34, 30, 70, 100, "#f4ecd8", 'rx="6" stroke="#8a7a5a" stroke-width="3"') + rect(42, 38, 54, 84, "none", 'stroke="#c0392b" stroke-width="2" rx="4"')
    body += path("M20 150 C 20 128 36 120 54 122 L80 122 C 90 122 92 134 82 136 L64 138 L60 150 Z", SKIN[0], 'stroke="#b08a6a" stroke-width="2"')
    # The undo arrow, from the knight back to the card
    body += path("M300 60 C 250 0 140 10 104 70", "none", 'stroke="#4fc3ff" stroke-width="16" stroke-linecap="round" opacity="0.85"')
    body += poly([(92, 52), (120, 66), (98, 90)], "#4fc3ff")
    body += knight(272, 142, 0.75, face_left=True)
    body += stroke("M240 120 l -18 -4 M240 132 l -20 2 M244 108 l -16 -10", "#ffffff", 3, 'opacity="0.7"')
    body += lettering("!", 300, 76, 18, "#ffe45c")
    return svg(defs, body)


def pair_programming():
    # Two developers squeezed at one tiny desk sharing one keyboard: one types with a coffee, the other leans over his
    # shoulder pointing at a bug on the screen; a timesheet between them bills 16 hours
    defs, body = bg_tech()
    body += monitor(124, 14, 120, 84, "#14202a")
    for k in range(6):
        body += rect(140, 30 + k * 10, 30 + (k * 23) % 60, 4, "#3fbf8a" if k != 3 else "#ff5a5a")
    body += circle(214, 61, 9, "none", 'stroke="#ff5a5a" stroke-width="2.5"')
    body += rect(90, 150, 190, 12, "#6a4a2a") + rect(140, 140, 80, 10, "#2a2a30", 'rx="2"')  # The tiny desk, one keyboard
    body += person(140, 200, 1.1, SKIN[0], "#3a6ad0", face="flat", hair="#3a2a1a", glasses="round")
    body += stroke("M150 160 L170 146", "#3a6ad0", 9) + rect(100, 120, 14, 20, "#ffffff", 'rx="2"') + stroke("M114 126 q 6 3 0 10", "#ffffff", 2.5)
    body += person(236, 186, 1.2, SKIN[2], "#d0342c", face="shout", hair="#1a1a1a", brows="angry")
    body += stroke("M248 130 L222 80", "#d0342c", 9) + circle(220, 76, 5, SKIN[2])
    body += rect(300, 96, 44, 56, "#ffffff", 'stroke="#555" stroke-width="2"') + lettering("16H", 322, 104, 9, "#c0392b")
    for k in range(3):
        body += rect(306, 120 + k * 8, 32, 3, "#aaaaaa")
    return svg(defs, body)


def suspicious_usb_stick_in_the_parking_lot():
    # A lone USB stick on the asphalt of an empty parking lot under a streetlight, labelled SALARIES 2026, an employee in a
    # lanyard bending down to pick it up, a nuclear plant's cooling tower behind the fence
    defs = lin("bg", ["#1a2236", "#3a3046"]) + rad("light", [("#ffe9a0", 0.55), ("#ffe9a0", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    body += path("M250 112 C 256 80 256 50 246 20 L318 20 C 308 50 308 80 314 112 Z", "#8a8a96")  # The cooling tower
    body += path("M262 20 C 270 6 300 6 306 20", "#c8c8d0", 'opacity="0.5"') + cloud(290, 4, 0.8, "#c8c8d0", 0.5)
    for x in range(0, 352, 16):  # The fence
        body += stroke(f"M{x} 96 L{x + 16} 116 M{x + 16} 96 L{x} 116", "#5a5a66", 1.2)
    body += stroke("M0 96 L352 96 M0 116 L352 116", "#5a5a66", 2)
    body += rect(0, 116, W, 84, "#2a2a30")
    for x in (40, 130, 220, 310):
        body += stroke(f"M{x} 130 L{x - 20} 200", "#e8e8e8", 3, 'opacity="0.6"')
    body += rect(196, 20, 6, 130, "#444") + rect(178, 18, 30, 8, "#555") + poly([(180, 26), (206, 26), (236, 196), (150, 196)], "url(#light)")
    # The stick, and its label
    body += rect(176, 168, 34, 12, "#1a1a22", 'rx="2"') + rect(210, 170, 10, 8, "#c8c8c8") + rect(180, 170, 26, 8, "#fff7e0")
    body += lettering("SALARIES", 193, 171, 3.2, "#c0392b", width=0.9)
    # The employee, bending down to it
    body += path("M70 196 C 70 150 100 120 130 120 L150 130 L120 196 Z", "#5a7a9a")
    body += circle(150, 120, 15, SKIN[1]) + path("M136 116 C 138 102 162 102 164 116 Z", "#3a2a1a")
    body += circle(156, 120, 2, "#222") + stroke("M152 128 l 6 -1", "#222", 1.6)
    body += stroke("M130 132 L170 168", "#5a7a9a", 8) + circle(172, 170, 5, SKIN[1])
    body += stroke("M140 134 L150 156", "#2a6ad0", 2) + rect(146, 154, 10, 12, "#ffffff", 'stroke="#2a6ad0" stroke-width="1.5"')
    return svg(defs, body)


def socrates():
    # Socrates in a white toga, bald with a big beard, cornering a sweating man in a suit against a marble column with one
    # raised finger and a huge question mark; a cup of hemlock waits on a pedestal behind
    defs, body = bg_mind()
    body += rect(0, 168, W, 32, "#e8dcc8")
    body += rect(260, 20, 40, 150, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"') + rect(252, 12, 56, 12, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"')
    for x in (270, 280, 290):
        body += stroke(f"M{x} 26 L{x} 166", "#d8d0c0", 2)
    body += person(282, 196, 1.0, SKIN[0], "#2a2a3a", face="wavy", hair="#5a3a1a", tie="#c0392b", brows="sad")
    body += sweat(304, 120) + sweat(262, 116, 0.8)
    body += rect(30, 120, 30, 50, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"')  # The pedestal and the hemlock
    body += path("M34 120 L56 120 L52 104 L38 104 Z", "#8a6a3a") + ellipse(45, 104, 8, 3, "#3a6a2a")
    # Socrates: toga, bald, big beard, a finger raised
    body += path("M96 200 C 96 140 120 118 160 118 C 200 118 222 140 222 200 Z", "#f6f2ea", 'stroke="#c8c0b0" stroke-width="2"')
    body += stroke("M110 130 C 140 160 180 150 214 190", "#d8d0c0", 3)
    body += circle(160, 82, 26, SKIN[0]) + circle(134, 84, 6, SKIN[0]) + circle(186, 84, 6, SKIN[0])
    body += circle(138, 70, 6, "#d8d8d8") + circle(182, 70, 6, "#d8d8d8")
    body += path("M136 88 C 136 130 184 130 184 88 C 176 102 144 102 136 88 Z", "#d8d8d8")
    body += circle(151, 80, 2.4, "#222") + circle(169, 80, 2.4, "#222") + stroke("M144 72 L156 74 M164 74 L176 72", "#888", 2)
    body += stroke("M210 150 L236 96", "#f6f2ea", 12) + stroke("M236 96 L240 74", SKIN[0], 6)
    body += lettering("?", 214, 14, 34, "#3a2a6a")
    return svg(defs, body)


def maslows_pyramid():
    # Maslow's pyramid as a stone pyramid in layers, tiny people at the bottom fighting over bread and water, one man
    # meditating smugly alone at the glowing top, and a WiFi symbol carved into a new layer under the base
    defs, body = bg_mind()
    defs += rad("glow", [("#fff6c0", 0.9), ("#fff6c0", 0)])
    body += circle(176, 30, 46, "url(#glow)")
    layers = [(150, 186, "#a89878"), (118, 154, "#b8a888"), (86, 122, "#c8b898"), (56, 90, "#d8c8a8"), (30, 60, "#e8d8b8")]
    for top, bottom, color in layers:
        half_t, half_b = (top - 20) * 0.95, (bottom - 20) * 0.95
        body += poly([(176 - half_t, top), (176 + half_t, top), (176 + half_b, bottom), (176 - half_b, bottom)], color, 'stroke="#7a6a50" stroke-width="2"')
    body += rect(14, 186, 324, 14, "#6a5a40") + stroke("M176 196 m -14 0 a 14 14 0 0 1 28 0 M176 196 m -8 0 a 8 8 0 0 1 16 0", "#4fc3ff", 2.5) + circle(176, 196, 2, "#4fc3ff")
    # The smug one at the top, cross-legged
    body += ellipse(176, 28, 14, 6, "#e86a3a") + circle(176, 14, 8, SKIN[0]) + stroke("M172 14 q 2 2 4 0 M178 14 q 2 2 4 0", "#222", 1.2)
    body += stroke("M162 26 l -8 -6 M190 26 l 8 -6", "#e86a3a", 4)
    # The crowd at the bottom, fighting over bread and water
    for k, x in enumerate((50, 76, 104, 248, 276, 302)):
        body += circle(x, 162, 7, SKIN[k % 4]) + rect(x - 6, 168, 12, 16, ("#3a6ad0", "#d0342c", "#3aa86a")[k % 3], 'rx="3"')
        body += stroke(f"M{x - 6} 172 l {-6 if k % 2 else 6} -8", ("#3a6ad0", "#d0342c", "#3aa86a")[k % 3], 3)
    body += ellipse(90, 152, 12, 6, "#c8964a") + rect(286, 142, 8, 14, "#7ac8f0", 'rx="2"')
    return svg(defs, body)


def legerdemain():
    # A magician's white-gloved hands doing the cups-and-ball trick on a velvet table, tiny soldiers under the cups being
    # shuffled, one cup lifted to show the soldier is gone
    defs, body = bg_mind()
    body += path("M0 120 L352 120 L352 200 L0 200 Z", "#5a1a3a") + rect(0, 120, W, 6, "#7a2a5a")
    body += stroke("M60 40 C 120 10 230 10 290 40", "#ffffff", 3, 'opacity="0.6" stroke-dasharray="6 6"')
    body += poly([(286, 34), (298, 44), (282, 46)], "#ffffff", 'opacity="0.6"')
    for x, lifted in ((96, False), (176, True), (256, False)):
        y = 70 if lifted else 100
        body += path(f"M{x - 26} {y + 50} L{x - 18} {y} L{x + 18} {y} L{x + 26} {y + 50} Z", "#c0392b", 'stroke="#7a1a1a" stroke-width="2"')
        body += ellipse(x, y + 50, 26, 6, "#a02a1a") + rect(x - 22, y + 12, 44, 6, "#d4af37")
        if lifted:
            body += ellipse(x, 150, 22, 5, "#3a0a1a", 'opacity="0.6"') + lettering("?", x, 128, 14, "#ffffff")
    # A tiny soldier peeking out under the right cup
    body += circle(276, 146, 6, SKIN[1]) + path("M268 144 C 268 134 284 134 284 144 Z", "#55603a")
    # The white gloves
    body += path("M10 70 C 30 60 56 62 70 74 L64 86 C 50 80 30 82 16 90 Z", "#ffffff", 'stroke="#aaa" stroke-width="2"')
    body += path("M342 46 C 322 36 296 40 286 52 L292 64 C 306 56 326 58 340 66 Z", "#ffffff", 'stroke="#aaa" stroke-width="2"')
    body += stroke("M70 74 L80 70 M286 52 L276 48", "#ffffff", 5)
    return svg(defs, body)


def short_king():
    # A short man in a paper crown standing on a stack of phone books to take a selfie at arm's length, the profile on his
    # phone screen reading 6 FT
    defs, body = bg_digital()
    for k in range(5):  # The phone books
        body += rect(120, 168 - k * 12, 110, 12, ("#f2c94c", "#e8e0c8", "#f2c94c", "#e8e0c8", "#f2c94c")[k], 'stroke="#8a7a3a" stroke-width="1.5"')
    body += path("M146 108 L146 120 L156 120 L156 108 Z M194 108 L194 120 L204 120 L204 108 Z", "#2a2a3a")
    body += person(176, 116, 0.9, SKIN[1], "#e0457b", face="smirk", hair="#1a1a1a")
    body += poly([(160, 42), (166, 30), (172, 40), (176, 26), (180, 40), (186, 30), (192, 42)], "#ffe45c", 'stroke="#c89a1a" stroke-width="1.5"')
    body += stroke("M196 84 L250 50", "#e0457b", 7) + circle(252, 48, 4, SKIN[1])
    body += phone(268, 46, 1.3, "#ffffff")
    body += circle(268, 36, 7, SKIN[1]) + lettering("6 FT", 268, 50, 6, "#2a3d8f")
    for k, (x, y) in enumerate(((300, 100), (316, 80), (290, 130))):
        body += heart(x, y, 0.9 + k * 0.2, "#ff5a8a")
    return svg(defs, body)


def content_creator():
    # A streamer in a headset under a ring light screaming at the camera with a shocked face and a giant red arrow pointing
    # at nothing, the thumbnail on the monitor behind showing the same face, 100K in gold
    defs, body = bg_digital()
    body += monitor(196, 10, 140, 92, "#2a1a3a")
    body += circle(240, 50, 18, SKIN[0]) + ellipse(240, 58, 5, 6, "#5a1a1a") + circle(233, 46, 3, "#222") + circle(247, 46, 3, "#222")
    body += lettering("100K", 296, 40, 12, "#ffd84a", outline="#7a5a10")
    body += poly([(150, 40), (190, 30), (186, 44), (226, 46), (226, 56), (186, 58), (190, 72)], "#ff2a2a", 'stroke="#7a0a0a" stroke-width="2"')
    body += circle(80, 80, 44, "none", 'stroke="#ffffff" stroke-width="8" opacity="0.9"') + circle(80, 80, 44, "none", 'stroke="#fff6c0" stroke-width="16" opacity="0.25"')
    body += person(80, 200, 1.4, SKIN[0], "#7a3aa0", face="shout", hair="#d0342c", brows="sad")
    body += path("M50 100 C 50 70 110 70 110 100", "none", 'stroke="#1a1a22" stroke-width="5"') + rect(44, 96, 10, 16, "#1a1a22", 'rx="3"') + rect(106, 96, 10, 16, "#1a1a22", 'rx="3"')
    body += stroke("M108 110 L120 126", "#1a1a22", 3) + circle(122, 128, 3, "#1a1a22")
    return svg(defs, body)


def thirst_trap():
    # A steel bear trap open on a beach towel, a ring light and a phone on a tripod in front of it, the phone screen
    # filling with hearts, a glass of water standing beside it
    defs, body = bg_digital()
    body += path("M20 140 L300 120 L340 190 L40 200 Z", "#ff8ab0", 'stroke="#ffffff" stroke-width="2"')
    for k in range(5):
        body += stroke(f"M{30 + k * 10} {150 + k * 10} L{306 + k * 7} {130 + k * 12}", "#ffffff", 3, 'opacity="0.6"')
    # The bear trap, open
    body += ellipse(170, 166, 64, 14, "#6a6a72", 'stroke="#3a3a42" stroke-width="3"')
    for k in range(9):
        x = 116 + k * 13
        body += poly([(x, 160), (x + 6, 132 - (k % 2) * 4), (x + 12, 160)], "#b8b8c4", 'stroke="#5a5a66" stroke-width="1.5"')
        body += poly([(x, 172), (x + 6, 194), (x + 12, 172)], "#9a9aa6", 'stroke="#5a5a66" stroke-width="1.5"')
    body += rect(160, 160, 20, 10, "#c8964a", 'rx="2"')
    # Ring light and phone on a tripod
    body += stroke("M262 132 L250 196 M262 132 L274 196 M262 132 L262 196", "#2a2a30", 3)
    body += circle(262, 70, 34, "none", 'stroke="#ffffff" stroke-width="7"') + phone(262, 76, 1.2, "#ffffff")
    for k, (x, y) in enumerate(((256, 66), (268, 72), (258, 84), (266, 58))):
        body += heart(x, y, 0.5, "#ff2a6a")
    body += rect(56, 100, 22, 40, "#bfe8ff", 'opacity="0.7" stroke="#ffffff" stroke-width="2"') + rect(58, 112, 18, 26, "#7ac8f0", 'opacity="0.8"')
    return svg(defs, body)


def overtime():
    # An office worker at a desk late at night, surrounded by empty coffee cups and energy drink cans, typing furiously with
    # smoke rising from the keyboard, eyes bloodshot and wide open, a wall clock showing 3 a.m. and a calendar with every
    # weekend crossed out
    defs = lin("bg", ["#141a2e", "#2a2440"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += circle(60, 44, 26, "#f4ecd8", 'stroke="#555" stroke-width="3"') + stroke("M60 44 L60 26 M60 44 L72 50", "#222", 3)
    body += lettering("3AM", 60, 76, 7, "#ff5a5a")
    body += rect(268, 14, 70, 66, "#ffffff", 'stroke="#555" stroke-width="2"') + rect(268, 14, 70, 12, "#c0392b")
    for row in range(4):
        for col in range(7):
            x, y = 272 + col * 9.5, 30 + row * 12
            body += rect(x, y, 7, 8, "#e8e8e8")
            if col >= 5:
                body += stroke(f"M{x} {y} L{x + 7} {y + 8} M{x + 7} {y} L{x} {y + 8}", "#c0392b", 1.5)
    body += rect(0, 160, W, 40, "#4a3a2a")
    body += monitor(150, 60, 120, 80, "#bfe8ff")
    body += rect(150, 150, 110, 12, "#2a2a30", 'rx="2"')
    for k in range(3):
        body += stroke(f"M{170 + k * 30} 150 q -6 -12 0 -22 q 6 -10 0 -20", "#c8c8d0", 2.5, 'opacity="0.7"')
    body += person(110, 200, 1.3, SKIN[0], "#3d5a80", face="wavy", hair="#3a2a1a", tie="#c0392b")
    body += circle(101, 115, 5, "#ffffff", 'stroke="#ff3a3a" stroke-width="1.5"') + circle(119, 115, 5, "#ffffff", 'stroke="#ff3a3a" stroke-width="1.5"')
    body += circle(101, 115, 1.8, "#222") + circle(119, 115, 1.8, "#222")
    body += stroke("M130 150 L160 152 M126 158 L170 158", "#3d5a80", 8)
    for x in (20, 36, 300, 316, 330):  # The cups and cans
        body += rect(x, 142, 12, 18, "#ffffff" if x < 100 else "#3aa86a", 'rx="2" stroke="#555" stroke-width="1"')
    return svg(defs, body)


def taqiyya():
    # A serene robed figure bowing slightly and offering an ornate gift box tied with a gold ribbon, both hands extended in
    # welcome; on the wall behind, the box's shadow is a coiled snake
    defs, body = bg_myth()
    body += rect(0, 170, W, 30, "#5a3a2a")
    # The shadow on the wall: a coiled snake
    body += path("M230 150 C 200 150 196 120 226 116 C 256 112 262 90 236 84 C 210 78 206 60 226 52", "none", 'stroke="#1a0a2a" stroke-width="16" stroke-linecap="round" opacity="0.55"')
    body += ellipse(232, 48, 14, 9, "#1a0a2a", 'opacity="0.55"') + stroke("M244 50 l 10 -2 l -4 4 l 6 2", "#1a0a2a", 2, 'opacity="0.55"')
    # The figure, robed, bowing slightly
    body += path("M60 200 C 64 140 90 110 128 112 C 160 114 176 150 178 200 Z", "#e8e0d0", 'stroke="#a89878" stroke-width="2"')
    body += circle(126, 86, 20, SKIN[2]) + path("M104 92 C 100 60 152 60 148 92 C 142 78 110 78 104 92 Z", "#f6f2ea", 'stroke="#c8c0b0" stroke-width="2"')
    body += path("M110 96 C 114 116 138 116 142 96 C 136 108 116 108 110 96 Z", "#3a2a1a")
    body += stroke("M118 86 q 3 2 6 0 M130 86 q 3 2 6 0", "#222", 1.6) + stroke("M120 100 q 6 4 12 0", "#222", 1.6)
    body += stroke("M150 150 L186 136 M140 160 L186 150", "#e8e0d0", 10)
    # The gift box, gold ribbon
    body += rect(186, 116, 46, 40, "#8a1a3a", 'stroke="#5a0a2a" stroke-width="2"') + rect(204, 116, 10, 40, "#d4af37") + rect(186, 130, 46, 8, "#d4af37")
    body += ellipse(202, 112, 10, 6, "#d4af37") + ellipse(216, 112, 10, 6, "#d4af37")
    return svg(defs, body)


def geomagnetic_reversal():
    # A globe with its magnetic field lines flipping over, the N and S labels swapping places mid-air, a scientist staring
    # at a compass whose needle spins wildly, and a flock of birds flying in confused loops
    defs, body = bg_utopia()
    defs += rad("earth", [("#5aa8e0", 1), ("#1a5a9a", 1)])
    body += circle(150, 96, 52, "url(#earth)", 'stroke="#0a3a6a" stroke-width="2"')
    body += path("M118 70 C 130 60 150 66 146 82 C 140 96 120 92 118 70 Z M156 104 C 170 96 186 110 178 126 C 168 138 152 126 156 104 Z", "#3aa86a")
    for k, r in enumerate((70, 88)):
        body += path(f"M150 {96 - 52} C {150 - r} {96 - 70} {150 - r} {96 + 70} 150 {96 + 52}", "none", 'stroke="#d0342c" stroke-width="2.5" opacity="0.7" stroke-dasharray="5 4"')
        body += path(f"M150 {96 - 52} C {150 + r} {96 - 70} {150 + r} {96 + 70} 150 {96 + 52}", "none", 'stroke="#2a6ad0" stroke-width="2.5" opacity="0.7" stroke-dasharray="5 4"')
    body += lettering("S", 150, 14, 14, "#2a6ad0") + lettering("N", 150, 158, 14, "#d0342c")
    body += stroke("M170 22 C 186 34 186 150 172 166", "none", 1) + path("M176 20 C 200 50 200 140 178 168", "none", 'stroke="#555" stroke-width="2" stroke-dasharray="3 4"')
    body += poly([(178, 168), (172, 158), (184, 160)], "#555")
    # The scientist with the compass
    body += person(290, 200, 1.1, SKIN[0], "#ffffff", face="shout", hair=OLD_HAIR, glasses="round", brows="sad")
    body += circle(256, 150, 16, "#f4ecd8", 'stroke="#8a6a3a" stroke-width="3"')
    body += stroke("M244 142 L268 158", "#d0342c", 3) + stroke("M244 158 L268 142", "#2a6ad0", 2, 'opacity="0.6"')
    body += stroke("M270 146 a 18 18 0 0 1 4 12", "#555", 1.5) + stroke("M240 164 a 18 18 0 0 1 -4 -12", "#555", 1.5)
    # The birds, looping
    for k, (x, y) in enumerate(((40, 40), (70, 26), (60, 60), (300, 30), (326, 52))):
        body += stroke(f"M{x - 7} {y} q 4 -6 7 0 q 3 -6 7 0", "#2a2a3a", 2)
    body += stroke("M30 80 C 10 60 60 20 90 40 C 110 54 80 80 60 70", "none", 1) + path("M30 80 C 10 60 60 20 90 40 C 110 54 80 80 60 70", "none", 'stroke="#2a2a3a" stroke-width="1.2" stroke-dasharray="2 4" opacity="0.6"')
    return svg(defs, body)


CHUNK_NEW = {"Tech": {"ThreatHunter": threat_hunter, "CtrlZ": ctrl_z, "PairProgramming": pair_programming,
                      "SuspiciousUSBStickintheParkingLot": suspicious_usb_stick_in_the_parking_lot},
             "Mind": {"Socrates": socrates, "MaslowsPyramid": maslows_pyramid, "Legerdemain": legerdemain},
             "DigitalSociety": {"ShortKing": short_king, "ContentCreator": content_creator, "ThirstTrap": thirst_trap},
             "Corporate": {"Overtime": overtime}, "Myth": {"Taqiyya": taqiyya}, "Utopia": {"GeomagneticReversal": geomagnetic_reversal}}
for _theme, _cards in CHUNK_NEW.items():
    CARDS[_theme].update(_cards)


# ---------------------------------------------------------------- New cards (2026-10-16): the smaller factions, and three without art

def heart(cx, cy, s, color, extra=""):
    return path(f"M{cx:.1f} {cy + 6 * s:.1f} C {cx - 10 * s:.1f} {cy - 2 * s:.1f} {cx - 8 * s:.1f} {cy - 10 * s:.1f} {cx:.1f} {cy - 5 * s:.1f} "
                f"C {cx + 8 * s:.1f} {cy - 10 * s:.1f} {cx + 10 * s:.1f} {cy - 2 * s:.1f} {cx:.1f} {cy + 6 * s:.1f} Z", color, extra)


def threat_hunter():
    # A security analyst in a headlamp crawling through a tunnel of server cables with a magnifying glass, following
    # glowing red footprints to a tiny hooded hacker hiding behind a rack
    defs, body = bg_tech()
    defs += rad("lamp", [("#fff6c0", 0.55), ("#fff6c0", 0)])
    body += server_rack(270, 40, 70, 150)
    for k in range(7):  # The cables, hanging in a tunnel
        y = 6 + k * 6
        body += stroke(f"M0 {y} C 90 {y + 40} 200 {y + 30} 352 {y + 8}", ("#2a6ad0", "#d0342c", "#f2c94c", "#3aa86a")[k % 4], 4, 'opacity="0.85"')
    for k, x in enumerate((140, 170, 198, 224, 248)):  # Red footprints
        body += ellipse(x, 170 - (k % 2) * 8, 5, 3, "#ff3a3a", 'opacity="0.9"') + ellipse(x, 170 - (k % 2) * 8, 9, 6, "#ff3a3a", 'opacity="0.25"')
    # The hacker, small, peeking from behind the rack
    body += path("M258 186 C 256 160 266 150 276 150 C 286 150 292 160 290 186 Z", "#1a1a22")
    body += circle(274, 146, 11, "#1a1a22") + ellipse(272, 148, 6, 7, "#0a0a0f") + circle(270, 147, 1.5, "#ff3a3a") + circle(276, 147, 1.5, "#ff3a3a")
    # The analyst crawling, headlamp on
    body += poly([(88, 128), (260, 92), (260, 196), (88, 150)], "url(#lamp)")
    body += path("M20 176 C 30 150 70 142 100 150 L118 172 L24 190 Z", "#3d5a80")
    body += stroke("M96 168 L118 186 M34 182 L22 196", "#2a3a50", 10)
    body += circle(98, 132, 18, SKIN[1]) + path("M80 130 C 80 108 116 108 116 128 Z", "#3a2a1a")
    body += rect(80, 120, 36, 6, "#222") + circle(116, 124, 6, "#fff6c0", 'stroke="#555" stroke-width="2"')
    body += circle(104, 132, 2.2, "#222") + stroke("M100 142 l 8 0", "#222", 1.8)
    body += stroke("M118 150 L140 140", "#3d5a80", 9) + circle(150, 136, 13, "#bfe8ff", 'opacity="0.5" stroke="#8a6a3a" stroke-width="4"')
    body += stroke("M141 145 L132 154", "#8a6a3a", 5)
    return svg(defs, body)


def ctrl_z():
    # A huge curved UNDO arrow pulling a startled knight backwards off a battlefield into a playing card held up by a hand,
    # the keys CTRL and Z glowing on a keyboard below
    defs = lin("bg", ["#1b2a4a", "#0b1222"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(0, 150, W, 50, "#2a2a30") + rect(0, 150, W, 4, "#3a3a44")
    for i, x in enumerate(range(8, 352, 38)):  # The keyboard
        lit = i in (1, 6)
        body += rect(x, 160, 32, 26, "#4fc3ff" if lit else "#3a3a44", 'rx="4"' + (' opacity="0.95"' if lit else ""))
    body += lettering("CTRL", 62, 167, 8, "#0b1222") + lettering("Z", 252, 165, 10, "#0b1222")
    # The playing card held up, and the hand
    body += rect(34, 30, 70, 100, "#f4ecd8", 'rx="6" stroke="#8a7a5a" stroke-width="3"') + rect(42, 38, 54, 84, "none", 'stroke="#c0392b" stroke-width="2" rx="4"')
    body += path("M20 150 C 20 128 36 120 54 122 L80 122 C 90 122 92 134 82 136 L64 138 L60 150 Z", SKIN[0], 'stroke="#b08a6a" stroke-width="2"')
    # The undo arrow, from the knight back to the card
    body += path("M300 60 C 250 0 140 10 104 70", "none", 'stroke="#4fc3ff" stroke-width="16" stroke-linecap="round" opacity="0.85"')
    body += poly([(92, 52), (120, 66), (98, 90)], "#4fc3ff")
    body += knight(272, 142, 0.75, face_left=True)
    body += stroke("M240 120 l -18 -4 M240 132 l -20 2 M244 108 l -16 -10", "#ffffff", 3, 'opacity="0.7"')
    body += lettering("!", 300, 76, 18, "#ffe45c")
    return svg(defs, body)


def pair_programming():
    # Two developers squeezed at one tiny desk sharing one keyboard: one types with a coffee, the other leans over his
    # shoulder pointing at a bug on the screen; a timesheet between them bills 16 hours
    defs, body = bg_tech()
    body += monitor(124, 14, 120, 84, "#14202a")
    for k in range(6):
        body += rect(140, 30 + k * 10, 30 + (k * 23) % 60, 4, "#3fbf8a" if k != 3 else "#ff5a5a")
    body += circle(214, 61, 9, "none", 'stroke="#ff5a5a" stroke-width="2.5"')
    body += rect(90, 150, 190, 12, "#6a4a2a") + rect(140, 140, 80, 10, "#2a2a30", 'rx="2"')  # The tiny desk, one keyboard
    body += person(140, 200, 1.1, SKIN[0], "#3a6ad0", face="flat", hair="#3a2a1a", glasses="round")
    body += stroke("M150 160 L170 146", "#3a6ad0", 9) + rect(100, 120, 14, 20, "#ffffff", 'rx="2"') + stroke("M114 126 q 6 3 0 10", "#ffffff", 2.5)
    body += person(236, 186, 1.2, SKIN[2], "#d0342c", face="shout", hair="#1a1a1a", brows="angry")
    body += stroke("M248 130 L222 80", "#d0342c", 9) + circle(220, 76, 5, SKIN[2])
    body += rect(300, 96, 44, 56, "#ffffff", 'stroke="#555" stroke-width="2"') + lettering("16H", 322, 104, 9, "#c0392b")
    for k in range(3):
        body += rect(306, 120 + k * 8, 32, 3, "#aaaaaa")
    return svg(defs, body)


def suspicious_usb_stick_in_the_parking_lot():
    # A lone USB stick on the asphalt of an empty parking lot under a streetlight, labelled SALARIES 2026, an employee in a
    # lanyard bending down to pick it up, a nuclear plant's cooling tower behind the fence
    defs = lin("bg", ["#1a2236", "#3a3046"]) + rad("light", [("#ffe9a0", 0.55), ("#ffe9a0", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    body += path("M250 112 C 256 80 256 50 246 20 L318 20 C 308 50 308 80 314 112 Z", "#8a8a96")  # The cooling tower
    body += path("M262 20 C 270 6 300 6 306 20", "#c8c8d0", 'opacity="0.5"') + cloud(290, 4, 0.8, "#c8c8d0", 0.5)
    for x in range(0, 352, 16):  # The fence
        body += stroke(f"M{x} 96 L{x + 16} 116 M{x + 16} 96 L{x} 116", "#5a5a66", 1.2)
    body += stroke("M0 96 L352 96 M0 116 L352 116", "#5a5a66", 2)
    body += rect(0, 116, W, 84, "#2a2a30")
    for x in (40, 130, 220, 310):
        body += stroke(f"M{x} 130 L{x - 20} 200", "#e8e8e8", 3, 'opacity="0.6"')
    body += rect(196, 20, 6, 130, "#444") + rect(178, 18, 30, 8, "#555") + poly([(180, 26), (206, 26), (236, 196), (150, 196)], "url(#light)")
    # The stick, and its label
    body += rect(176, 168, 34, 12, "#1a1a22", 'rx="2"') + rect(210, 170, 10, 8, "#c8c8c8") + rect(180, 170, 26, 8, "#fff7e0")
    body += lettering("SALARIES", 193, 171, 3.2, "#c0392b", width=0.9)
    # The employee, bending down to it
    body += path("M70 196 C 70 150 100 120 130 120 L150 130 L120 196 Z", "#5a7a9a")
    body += circle(150, 120, 15, SKIN[1]) + path("M136 116 C 138 102 162 102 164 116 Z", "#3a2a1a")
    body += circle(156, 120, 2, "#222") + stroke("M152 128 l 6 -1", "#222", 1.6)
    body += stroke("M130 132 L170 168", "#5a7a9a", 8) + circle(172, 170, 5, SKIN[1])
    body += stroke("M140 134 L150 156", "#2a6ad0", 2) + rect(146, 154, 10, 12, "#ffffff", 'stroke="#2a6ad0" stroke-width="1.5"')
    return svg(defs, body)


def socrates():
    # Socrates in a white toga, bald with a big beard, cornering a sweating man in a suit against a marble column with one
    # raised finger and a huge question mark; a cup of hemlock waits on a pedestal behind
    defs, body = bg_mind()
    body += rect(0, 168, W, 32, "#e8dcc8")
    body += rect(260, 20, 40, 150, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"') + rect(252, 12, 56, 12, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"')
    for x in (270, 280, 290):
        body += stroke(f"M{x} 26 L{x} 166", "#d8d0c0", 2)
    body += person(282, 196, 1.0, SKIN[0], "#2a2a3a", face="wavy", hair="#5a3a1a", tie="#c0392b", brows="sad")
    body += sweat(304, 120) + sweat(262, 116, 0.8)
    body += rect(30, 120, 30, 50, "#f4efe4", 'stroke="#c8bca8" stroke-width="2"')  # The pedestal and the hemlock
    body += path("M34 120 L56 120 L52 104 L38 104 Z", "#8a6a3a") + ellipse(45, 104, 8, 3, "#3a6a2a")
    # Socrates: toga, bald, big beard, a finger raised
    body += path("M96 200 C 96 140 120 118 160 118 C 200 118 222 140 222 200 Z", "#f6f2ea", 'stroke="#c8c0b0" stroke-width="2"')
    body += stroke("M110 130 C 140 160 180 150 214 190", "#d8d0c0", 3)
    body += circle(160, 82, 26, SKIN[0]) + circle(134, 84, 6, SKIN[0]) + circle(186, 84, 6, SKIN[0])
    body += circle(138, 70, 6, "#d8d8d8") + circle(182, 70, 6, "#d8d8d8")
    body += path("M136 88 C 136 130 184 130 184 88 C 176 102 144 102 136 88 Z", "#d8d8d8")
    body += circle(151, 80, 2.4, "#222") + circle(169, 80, 2.4, "#222") + stroke("M144 72 L156 74 M164 74 L176 72", "#888", 2)
    body += stroke("M210 150 L236 96", "#f6f2ea", 12) + stroke("M236 96 L240 74", SKIN[0], 6)
    body += lettering("?", 214, 14, 34, "#3a2a6a")
    return svg(defs, body)


def maslows_pyramid():
    # Maslow's pyramid as a stone pyramid in layers, tiny people at the bottom fighting over bread and water, one man
    # meditating smugly alone at the glowing top, and a WiFi symbol carved into a new layer under the base
    defs, body = bg_mind()
    defs += rad("glow", [("#fff6c0", 0.9), ("#fff6c0", 0)])
    body += circle(176, 30, 46, "url(#glow)")
    layers = [(150, 186, "#a89878"), (118, 154, "#b8a888"), (86, 122, "#c8b898"), (56, 90, "#d8c8a8"), (30, 60, "#e8d8b8")]
    for top, bottom, color in layers:
        half_t, half_b = (top - 20) * 0.95, (bottom - 20) * 0.95
        body += poly([(176 - half_t, top), (176 + half_t, top), (176 + half_b, bottom), (176 - half_b, bottom)], color, 'stroke="#7a6a50" stroke-width="2"')
    body += rect(14, 186, 324, 14, "#6a5a40") + stroke("M176 196 m -14 0 a 14 14 0 0 1 28 0 M176 196 m -8 0 a 8 8 0 0 1 16 0", "#4fc3ff", 2.5) + circle(176, 196, 2, "#4fc3ff")
    # The smug one at the top, cross-legged
    body += ellipse(176, 28, 14, 6, "#e86a3a") + circle(176, 14, 8, SKIN[0]) + stroke("M172 14 q 2 2 4 0 M178 14 q 2 2 4 0", "#222", 1.2)
    body += stroke("M162 26 l -8 -6 M190 26 l 8 -6", "#e86a3a", 4)
    # The crowd at the bottom, fighting over bread and water
    for k, x in enumerate((50, 76, 104, 248, 276, 302)):
        body += circle(x, 162, 7, SKIN[k % 4]) + rect(x - 6, 168, 12, 16, ("#3a6ad0", "#d0342c", "#3aa86a")[k % 3], 'rx="3"')
        body += stroke(f"M{x - 6} 172 l {-6 if k % 2 else 6} -8", ("#3a6ad0", "#d0342c", "#3aa86a")[k % 3], 3)
    body += ellipse(90, 152, 12, 6, "#c8964a") + rect(286, 142, 8, 14, "#7ac8f0", 'rx="2"')
    return svg(defs, body)


def legerdemain():
    # A magician's white-gloved hands doing the cups-and-ball trick on a velvet table, tiny soldiers under the cups being
    # shuffled, one cup lifted to show the soldier is gone
    defs, body = bg_mind()
    body += path("M0 120 L352 120 L352 200 L0 200 Z", "#5a1a3a") + rect(0, 120, W, 6, "#7a2a5a")
    body += stroke("M60 40 C 120 10 230 10 290 40", "#ffffff", 3, 'opacity="0.6" stroke-dasharray="6 6"')
    body += poly([(286, 34), (298, 44), (282, 46)], "#ffffff", 'opacity="0.6"')
    for x, lifted in ((96, False), (176, True), (256, False)):
        y = 70 if lifted else 100
        body += path(f"M{x - 26} {y + 50} L{x - 18} {y} L{x + 18} {y} L{x + 26} {y + 50} Z", "#c0392b", 'stroke="#7a1a1a" stroke-width="2"')
        body += ellipse(x, y + 50, 26, 6, "#a02a1a") + rect(x - 22, y + 12, 44, 6, "#d4af37")
        if lifted:
            body += ellipse(x, 150, 22, 5, "#3a0a1a", 'opacity="0.6"') + lettering("?", x, 128, 14, "#ffffff")
    # A tiny soldier peeking out under the right cup
    body += circle(276, 146, 6, SKIN[1]) + path("M268 144 C 268 134 284 134 284 144 Z", "#55603a")
    # The white gloves
    body += path("M10 70 C 30 60 56 62 70 74 L64 86 C 50 80 30 82 16 90 Z", "#ffffff", 'stroke="#aaa" stroke-width="2"')
    body += path("M342 46 C 322 36 296 40 286 52 L292 64 C 306 56 326 58 340 66 Z", "#ffffff", 'stroke="#aaa" stroke-width="2"')
    body += stroke("M70 74 L80 70 M286 52 L276 48", "#ffffff", 5)
    return svg(defs, body)


def short_king():
    # A short man in a paper crown standing on a stack of phone books to take a selfie at arm's length, the profile on his
    # phone screen reading 6 FT
    defs, body = bg_digital()
    for k in range(5):  # The phone books
        body += rect(120, 168 - k * 12, 110, 12, ("#f2c94c", "#e8e0c8", "#f2c94c", "#e8e0c8", "#f2c94c")[k], 'stroke="#8a7a3a" stroke-width="1.5"')
    body += path("M146 108 L146 120 L156 120 L156 108 Z M194 108 L194 120 L204 120 L204 108 Z", "#2a2a3a")
    body += person(176, 116, 0.9, SKIN[1], "#e0457b", face="smirk", hair="#1a1a1a")
    body += poly([(160, 42), (166, 30), (172, 40), (176, 26), (180, 40), (186, 30), (192, 42)], "#ffe45c", 'stroke="#c89a1a" stroke-width="1.5"')
    body += stroke("M196 84 L250 50", "#e0457b", 7) + circle(252, 48, 4, SKIN[1])
    body += phone(268, 46, 1.3, "#ffffff")
    body += circle(268, 36, 7, SKIN[1]) + lettering("6 FT", 268, 50, 6, "#2a3d8f")
    for k, (x, y) in enumerate(((300, 100), (316, 80), (290, 130))):
        body += heart(x, y, 0.9 + k * 0.2, "#ff5a8a")
    return svg(defs, body)


def content_creator():
    # A streamer in a headset under a ring light screaming at the camera with a shocked face and a giant red arrow pointing
    # at nothing, the thumbnail on the monitor behind showing the same face, 100K in gold
    defs, body = bg_digital()
    body += monitor(196, 10, 140, 92, "#2a1a3a")
    body += circle(240, 50, 18, SKIN[0]) + ellipse(240, 58, 5, 6, "#5a1a1a") + circle(233, 46, 3, "#222") + circle(247, 46, 3, "#222")
    body += lettering("100K", 296, 40, 12, "#ffd84a", outline="#7a5a10")
    body += poly([(150, 40), (190, 30), (186, 44), (226, 46), (226, 56), (186, 58), (190, 72)], "#ff2a2a", 'stroke="#7a0a0a" stroke-width="2"')
    body += circle(80, 80, 44, "none", 'stroke="#ffffff" stroke-width="8" opacity="0.9"') + circle(80, 80, 44, "none", 'stroke="#fff6c0" stroke-width="16" opacity="0.25"')
    body += person(80, 200, 1.4, SKIN[0], "#7a3aa0", face="shout", hair="#d0342c", brows="sad")
    body += path("M50 100 C 50 70 110 70 110 100", "none", 'stroke="#1a1a22" stroke-width="5"') + rect(44, 96, 10, 16, "#1a1a22", 'rx="3"') + rect(106, 96, 10, 16, "#1a1a22", 'rx="3"')
    body += stroke("M108 110 L120 126", "#1a1a22", 3) + circle(122, 128, 3, "#1a1a22")
    return svg(defs, body)


def thirst_trap():
    # A steel bear trap open on a beach towel, a ring light and a phone on a tripod in front of it, the phone screen
    # filling with hearts, a glass of water standing beside it
    defs, body = bg_digital()
    body += path("M20 140 L300 120 L340 190 L40 200 Z", "#ff8ab0", 'stroke="#ffffff" stroke-width="2"')
    for k in range(5):
        body += stroke(f"M{30 + k * 10} {150 + k * 10} L{306 + k * 7} {130 + k * 12}", "#ffffff", 3, 'opacity="0.6"')
    # The bear trap, open
    body += ellipse(170, 166, 64, 14, "#6a6a72", 'stroke="#3a3a42" stroke-width="3"')
    for k in range(9):
        x = 116 + k * 13
        body += poly([(x, 160), (x + 6, 132 - (k % 2) * 4), (x + 12, 160)], "#b8b8c4", 'stroke="#5a5a66" stroke-width="1.5"')
        body += poly([(x, 172), (x + 6, 194), (x + 12, 172)], "#9a9aa6", 'stroke="#5a5a66" stroke-width="1.5"')
    body += rect(160, 160, 20, 10, "#c8964a", 'rx="2"')
    # Ring light and phone on a tripod
    body += stroke("M262 132 L250 196 M262 132 L274 196 M262 132 L262 196", "#2a2a30", 3)
    body += circle(262, 70, 34, "none", 'stroke="#ffffff" stroke-width="7"') + phone(262, 76, 1.2, "#ffffff")
    for k, (x, y) in enumerate(((256, 66), (268, 72), (258, 84), (266, 58))):
        body += heart(x, y, 0.5, "#ff2a6a")
    body += rect(56, 100, 22, 40, "#bfe8ff", 'opacity="0.7" stroke="#ffffff" stroke-width="2"') + rect(58, 112, 18, 26, "#7ac8f0", 'opacity="0.8"')
    return svg(defs, body)


def overtime():
    # An office worker at a desk late at night, surrounded by empty coffee cups and energy drink cans, typing furiously with
    # smoke rising from the keyboard, eyes bloodshot and wide open, a wall clock showing 3 a.m. and a calendar with every
    # weekend crossed out
    defs = lin("bg", ["#141a2e", "#2a2440"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += circle(60, 44, 26, "#f4ecd8", 'stroke="#555" stroke-width="3"') + stroke("M60 44 L60 26 M60 44 L72 50", "#222", 3)
    body += lettering("3AM", 60, 76, 7, "#ff5a5a")
    body += rect(268, 14, 70, 66, "#ffffff", 'stroke="#555" stroke-width="2"') + rect(268, 14, 70, 12, "#c0392b")
    for row in range(4):
        for col in range(7):
            x, y = 272 + col * 9.5, 30 + row * 12
            body += rect(x, y, 7, 8, "#e8e8e8")
            if col >= 5:
                body += stroke(f"M{x} {y} L{x + 7} {y + 8} M{x + 7} {y} L{x} {y + 8}", "#c0392b", 1.5)
    body += rect(0, 160, W, 40, "#4a3a2a")
    body += monitor(150, 60, 120, 80, "#bfe8ff")
    body += rect(150, 150, 110, 12, "#2a2a30", 'rx="2"')
    for k in range(3):
        body += stroke(f"M{170 + k * 30} 150 q -6 -12 0 -22 q 6 -10 0 -20", "#c8c8d0", 2.5, 'opacity="0.7"')
    body += person(110, 200, 1.3, SKIN[0], "#3d5a80", face="wavy", hair="#3a2a1a", tie="#c0392b")
    body += circle(101, 115, 5, "#ffffff", 'stroke="#ff3a3a" stroke-width="1.5"') + circle(119, 115, 5, "#ffffff", 'stroke="#ff3a3a" stroke-width="1.5"')
    body += circle(101, 115, 1.8, "#222") + circle(119, 115, 1.8, "#222")
    body += stroke("M130 150 L160 152 M126 158 L170 158", "#3d5a80", 8)
    for x in (20, 36, 300, 316, 330):  # The cups and cans
        body += rect(x, 142, 12, 18, "#ffffff" if x < 100 else "#3aa86a", 'rx="2" stroke="#555" stroke-width="1"')
    return svg(defs, body)


def taqiyya():
    # A serene robed figure bowing slightly and offering an ornate gift box tied with a gold ribbon, both hands extended in
    # welcome; on the wall behind, the box's shadow is a coiled snake
    defs, body = bg_myth()
    body += rect(0, 170, W, 30, "#5a3a2a")
    # The shadow on the wall: a coiled snake
    body += path("M230 150 C 200 150 196 120 226 116 C 256 112 262 90 236 84 C 210 78 206 60 226 52", "none", 'stroke="#1a0a2a" stroke-width="16" stroke-linecap="round" opacity="0.55"')
    body += ellipse(232, 48, 14, 9, "#1a0a2a", 'opacity="0.55"') + stroke("M244 50 l 10 -2 l -4 4 l 6 2", "#1a0a2a", 2, 'opacity="0.55"')
    # The figure, robed, bowing slightly
    body += path("M60 200 C 64 140 90 110 128 112 C 160 114 176 150 178 200 Z", "#e8e0d0", 'stroke="#a89878" stroke-width="2"')
    body += circle(126, 86, 20, SKIN[2]) + path("M104 92 C 100 60 152 60 148 92 C 142 78 110 78 104 92 Z", "#f6f2ea", 'stroke="#c8c0b0" stroke-width="2"')
    body += path("M110 96 C 114 116 138 116 142 96 C 136 108 116 108 110 96 Z", "#3a2a1a")
    body += stroke("M118 86 q 3 2 6 0 M130 86 q 3 2 6 0", "#222", 1.6) + stroke("M120 100 q 6 4 12 0", "#222", 1.6)
    body += stroke("M150 150 L186 136 M140 160 L186 150", "#e8e0d0", 10)
    # The gift box, gold ribbon
    body += rect(186, 116, 46, 40, "#8a1a3a", 'stroke="#5a0a2a" stroke-width="2"') + rect(204, 116, 10, 40, "#d4af37") + rect(186, 130, 46, 8, "#d4af37")
    body += ellipse(202, 112, 10, 6, "#d4af37") + ellipse(216, 112, 10, 6, "#d4af37")
    return svg(defs, body)


def geomagnetic_reversal():
    # A globe with its magnetic field lines flipping over, the N and S labels swapping places mid-air, a scientist staring
    # at a compass whose needle spins wildly, and a flock of birds flying in confused loops
    defs, body = bg_utopia()
    defs += rad("earth", [("#5aa8e0", 1), ("#1a5a9a", 1)])
    body += circle(150, 96, 52, "url(#earth)", 'stroke="#0a3a6a" stroke-width="2"')
    body += path("M118 70 C 130 60 150 66 146 82 C 140 96 120 92 118 70 Z M156 104 C 170 96 186 110 178 126 C 168 138 152 126 156 104 Z", "#3aa86a")
    for k, r in enumerate((70, 88)):
        body += path(f"M150 {96 - 52} C {150 - r} {96 - 70} {150 - r} {96 + 70} 150 {96 + 52}", "none", 'stroke="#d0342c" stroke-width="2.5" opacity="0.7" stroke-dasharray="5 4"')
        body += path(f"M150 {96 - 52} C {150 + r} {96 - 70} {150 + r} {96 + 70} 150 {96 + 52}", "none", 'stroke="#2a6ad0" stroke-width="2.5" opacity="0.7" stroke-dasharray="5 4"')
    body += lettering("S", 150, 14, 14, "#2a6ad0") + lettering("N", 150, 158, 14, "#d0342c")
    body += stroke("M170 22 C 186 34 186 150 172 166", "none", 1) + path("M176 20 C 200 50 200 140 178 168", "none", 'stroke="#555" stroke-width="2" stroke-dasharray="3 4"')
    body += poly([(178, 168), (172, 158), (184, 160)], "#555")
    # The scientist with the compass
    body += person(290, 200, 1.1, SKIN[0], "#ffffff", face="shout", hair=OLD_HAIR, glasses="round", brows="sad")
    body += circle(256, 150, 16, "#f4ecd8", 'stroke="#8a6a3a" stroke-width="3"')
    body += stroke("M244 142 L268 158", "#d0342c", 3) + stroke("M244 158 L268 142", "#2a6ad0", 2, 'opacity="0.6"')
    body += stroke("M270 146 a 18 18 0 0 1 4 12", "#555", 1.5) + stroke("M240 164 a 18 18 0 0 1 -4 -12", "#555", 1.5)
    # The birds, looping
    for k, (x, y) in enumerate(((40, 40), (70, 26), (60, 60), (300, 30), (326, 52))):
        body += stroke(f"M{x - 7} {y} q 4 -6 7 0 q 3 -6 7 0", "#2a2a3a", 2)
    body += stroke("M30 80 C 10 60 60 20 90 40 C 110 54 80 80 60 70", "none", 1) + path("M30 80 C 10 60 60 20 90 40 C 110 54 80 80 60 70", "none", 'stroke="#2a2a3a" stroke-width="1.2" stroke-dasharray="2 4" opacity="0.6"')
    return svg(defs, body)


CHUNK_NEW = {"Tech": {"ThreatHunter": threat_hunter, "CtrlZ": ctrl_z, "PairProgramming": pair_programming,
                      "SuspiciousUSBStickintheParkingLot": suspicious_usb_stick_in_the_parking_lot},
             "Mind": {"Socrates": socrates, "MaslowsPyramid": maslows_pyramid, "Legerdemain": legerdemain},
             "DigitalSociety": {"ShortKing": short_king, "ContentCreator": content_creator, "ThirstTrap": thirst_trap},
             "Corporate": {"Overtime": overtime}, "Myth": {"Taqiyya": taqiyya}, "Utopia": {"GeomagneticReversal": geomagnetic_reversal}}
for _theme, _cards in CHUNK_NEW.items():
    CARDS[_theme].update(_cards)


# ---------------------------------------------------------------- The cards that had no art yet (2026-10-17)

# lettering()'s font with digits and a few signs added; lettering itself keeps its own font so the older pictures don't change
FONT_PLUS = dict(STROKE_FONT)
FONT_PLUS.update({
    "0": [[(1, 0), (3, 0), (4, 1), (4, 5), (3, 6), (1, 6), (0, 5), (0, 1), (1, 0)]],
    "1": [[(1, 1), (2, 0), (2, 6)], [(1, 6), (3, 6)]],
    "2": [[(0, 1), (1, 0), (3, 0), (4, 1), (4, 2), (0, 6), (4, 6)]],
    "3": [[(0, 1), (1, 0), (3, 0), (4, 1), (4, 2), (3, 3), (1.5, 3)], [(3, 3), (4, 4), (4, 5), (3, 6), (1, 6), (0, 5)]],
    "4": [[(3, 6), (3, 0), (0, 4), (4, 4)]],
    "5": [[(4, 0), (0, 0), (0, 3), (3, 3), (4, 4), (4, 5), (3, 6), (0, 6)]],
    "6": [[(4, 1), (3, 0), (1, 0), (0, 1), (0, 5), (1, 6), (3, 6), (4, 5), (4, 4), (3, 3), (0, 3)]],
    "7": [[(0, 0), (4, 0), (1.5, 6)]],
    "8": [[(1, 3), (0, 2), (0, 1), (1, 0), (3, 0), (4, 1), (4, 2), (3, 3), (1, 3), (0, 4), (0, 5), (1, 6), (3, 6), (4, 5), (4, 4), (3, 3)]],
    "9": [[(0, 5), (1, 6), (3, 6), (4, 5), (4, 1), (3, 0), (1, 0), (0, 1), (0, 2), (1, 3), (4, 3)]],
    "?": [[(0, 1), (1, 0), (3, 0), (4, 1), (4, 2), (2, 3.5), (2, 4.4)], [(2, 5.6), (2, 6)]],
    ":": [[(2, 1.6), (2, 2)], [(2, 4.6), (2, 5)]],
    "%": [[(0, 6), (4, 0)], [(0.5, 0.5), (1, 0.5)], [(3, 5.5), (3.5, 5.5)]],
    "-": [[(1, 3), (3, 3)]],
    "+": [[(0.5, 3), (3.5, 3)], [(2, 1.5), (2, 4.5)]],
    "'": [[(2, 0), (2, 1.6)]],
    ".": [[(2, 5.6), (2, 6)]],
    "#": [[(1, 0.5), (1, 5.5)], [(3, 0.5), (3, 5.5)], [(0, 2), (4, 2)], [(0, 4), (4, 4)]],
})


def words(text, cx, top, height, color, width=None, outline=None):
    """lettering() with digits and a few signs."""
    unit = height / 6
    width = width or max(1.5, height / 7)
    advance = 5.6 * unit
    x0 = cx - (len(text) * advance - 1.6 * unit) / 2
    d = ""
    for i, ch in enumerate(text.upper()):
        for line in FONT_PLUS.get(ch, []):
            d += "M" + " L".join(f"{x0 + i * advance + x * unit:.1f} {top + y * unit:.1f}" for x, y in line) + " "
    if not d:
        return ""
    return (stroke(d, outline, width * 2.2) if outline else "") + stroke(d, color, width)


def fish(cx, cy, s=1.0, color="#7ab0d0", face=1):
    """A cartoon fish facing right (face=1) or left (-1)."""
    f = face
    out = ellipse(cx, cy, 16 * s, 9 * s, color, 'stroke="#2a4a6a" stroke-width="1.2"')
    out += poly([(cx - f * 14 * s, cy), (cx - f * 26 * s, cy - 9 * s), (cx - f * 26 * s, cy + 9 * s)], color, 'stroke="#2a4a6a" stroke-width="1.2"')
    return out + circle(cx + f * 8 * s, cy - 2 * s, 2.2 * s, "#ffffff") + circle(cx + f * 8.5 * s, cy - 2 * s, 1.1 * s, "#111")


def bubble(cx, cy, rx, ry, color="#ffffff", tail=None):
    """A speech or thought bubble; tail is the (x, y) its point aims at."""
    out = ""
    if tail:
        tx, ty = tail
        out += poly([(cx - rx * 0.25, cy + ry * 0.5), (cx + rx * 0.15, cy + ry * 0.6), (tx, ty)], color, 'stroke="#333" stroke-width="1.5"')
    out += ellipse(cx, cy, rx, ry, color, 'stroke="#333" stroke-width="1.5"')
    if tail:
        out += poly([(cx - rx * 0.22, cy + ry * 0.45), (cx + rx * 0.12, cy + ry * 0.55), (tx + (cx - tx) * 0.2, ty + (cy - ty) * 0.2)], color)
    return out


def van(cx, base, s=1.0, color="#f4f4f4", stripe="#2a7ad0", face=1):
    """A delivery van facing right (face=1) or left, its wheels on y=base."""
    f = face
    out = rect(cx - 50 * s if f == 1 else cx - 50 * s, base - 50 * s, 100 * s, 40 * s, color, 'rx="5" stroke="#555" stroke-width="1.5"')
    nose = cx + f * 50 * s
    out += path(f"M{nose:.1f} {base - 46 * s:.1f} L{nose + f * 22 * s:.1f} {base - 30 * s:.1f} L{nose + f * 24 * s:.1f} {base - 10 * s:.1f} L{nose:.1f} {base - 10 * s:.1f} Z", color, 'stroke="#555" stroke-width="1.5"')
    out += poly([(nose + f * 2 * s, base - 42 * s), (nose + f * 18 * s, base - 30 * s), (nose + f * 2 * s, base - 30 * s)], "#8ac8f0")
    out += rect(cx - 50 * s, base - 30 * s, 100 * s, 6 * s, stripe)
    for dx in (-30, 40):
        out += circle(cx + f * dx * s, base - 6 * s, 9 * s, "#222") + circle(cx + f * dx * s, base - 6 * s, 4 * s, "#aaa")
    return out


def armchair(cx, base, s=1.0, color="#8a3a2a"):
    """An armchair seen from the front, its feet on y=base; the sitter goes in front of it."""
    out = rect(cx - 30 * s, base - 70 * s, 60 * s, 50 * s, color, f'rx="{10 * s:.1f}"')
    out += rect(cx - 38 * s, base - 36 * s, 16 * s, 30 * s, color, f'rx="{5 * s:.1f}" stroke="#000" stroke-opacity="0.2" stroke-width="1"')
    out += rect(cx + 22 * s, base - 36 * s, 16 * s, 30 * s, color, f'rx="{5 * s:.1f}" stroke="#000" stroke-opacity="0.2" stroke-width="1"')
    return out + rect(cx - 30 * s, base - 8 * s, 4 * s, 8 * s, "#3a2a1a") + rect(cx + 26 * s, base - 8 * s, 4 * s, 8 * s, "#3a2a1a")


def suitcase(cx, cy, s=1.0, color="#8a5a2a", label=None):
    out = rect(cx - 6 * s, cy - 16 * s, 12 * s, 6 * s, "none", f'stroke="#3a2a1a" stroke-width="{2 * s:.1f}" rx="2"')
    out += rect(cx - 18 * s, cy - 11 * s, 36 * s, 24 * s, color, 'rx="3" stroke="#3a2a1a" stroke-width="1.5"')
    if label:
        out += rect(cx - 14 * s, cy - 4 * s, 28 * s, 10 * s, "#f4ecd8") + words(label, cx, cy - 2.5 * s, 7 * s, "#3a2a1a", width=1.2)
    return out


# ---- Corporate

def big_four():
    # Four identical accountants in grey suits at a long table, the shredder behind them overflowing, a fifth chair empty
    # with a name card reading 'Arthur Andersen'
    defs, body = bg_office()
    body += rect(14, 92, 40, 58, "#3a3f46", 'rx="3"') + rect(18, 86, 32, 8, "#2a2e34")
    for i in range(7):  # The shredded strips spilling over
        body += stroke(f"M{20 + i * 4} 88 q {(-1) ** i * 6} -14 {(-1) ** i * 2} -26", "#f4f4f4", 2)
        body += stroke(f"M{20 + i * 5} 150 q {i - 3} 10 {i * 3 - 10} 24", "#f4f4f4", 2)
    body += rect(296, 92, 44, 70, "#4a3a2a", 'rx="6"')  # The empty chair
    for x in (94, 150, 206, 262):
        body += person(x, 168, 0.85, SKIN[0], "#7a8088", face="flat", hair="#3a3a3a", tie="#4a4f56", glasses="round")
    body += rect(0, 150, W, 50, "#5a3e28") + rect(0, 150, W, 5, "#7a5a3a")
    body += poly([(292, 132), (338, 132), (342, 152), (288, 152)], "#f4f4f4", 'stroke="#555" stroke-width="1"')
    body += lettering("ARTHUR", 315, 135, 5, "#333", width=1) + lettering("ANDERSEN", 315, 143, 4.5, "#333", width=1)
    for x in (80, 136, 192, 248):  # The same papers in front of each
        body += paper(x, 158, 28, 20, 2)
    return svg(defs, body)


def bloomberg_terminal():
    # A black two-screen trading terminal, every chart spiking green, a sweaty finger hovering over the yellow BUY key
    defs, body = room("#20242c", "#0c0e12", "#1a1a1e", 150)
    for x in (36, 180):
        body += monitor(x, 18, 136, 96, "#05080c")
        for k in range(3):
            y0 = 40 + k * 22
            body += stroke(f"M{x + 14} {y0 + 14} l 18 -2 l 14 4 l 16 -8 l 12 2 l 16 -10 l 14 -6 l 14 -12", "#3aff7a", 2)
            body += words("+" + str(3 + k * 4) + "%", x + 112, y0 - 6, 5, "#3aff7a", width=1)
    body += rect(30, 152, 292, 40, "#111216", 'rx="6"')
    keys = ("#2a2a30", "#c0392b", "#2a7a3a", "#2a4a9a", "#2a2a30", "#2a2a30")
    for row in range(3):
        for col in range(14):
            x, y = 38 + col * 20, 158 + row * 11
            if row == 1 and col in (10, 11):
                continue
            body += rect(x, y, 16, 8, keys[(col * 3 + row) % len(keys)], 'rx="1.5"')
    body += rect(238, 169, 36, 8, "#f2c94c", 'rx="1.5"') + lettering("BUY", 256, 170, 6, "#111", width=1.3)
    # The trader's hand, finger down over the key
    body += path("M352 92 L300 108 C 288 112 284 120 292 124 L262 128 C 254 130 254 138 262 138 L300 136 C 320 136 340 130 352 126 Z",
                 SKIN[0], 'stroke="#b08a6a" stroke-width="1.5"')
    body += path("M262 128 C 252 130 248 144 254 152 C 258 156 264 154 264 146 Z", SKIN[0], 'stroke="#b08a6a" stroke-width="1.5"')
    body += sweat(272, 120, 0.9) + sweat(318, 138, 0.8) + sweat(246, 150, 0.7)
    return svg(defs, body)


def corporate_bailouts():
    # A sinking cruise ship branded MEGABANK lifted out of the sea by a giant hand made of taxpayers' wallets, the
    # executives on deck popping champagne
    defs = lin("bg", ["#5a6a80", "#a8b8c8"]) + lin("sea", ["#2a5a8a", "#0b2240"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 140, W, 60, "url(#sea)")
    # The hand: a palm and fingers patterned with wallets
    hand = path("M96 200 L110 150 C 110 136 120 128 140 128 L250 128 C 270 128 276 140 270 152 L262 200 Z", "#7a4a2a")
    for i, (x, y) in enumerate(((118, 140), (150, 138), (182, 138), (214, 138), (240, 142), (126, 166), (162, 164), (198, 164), (232, 166),
                                (140, 186), (180, 188), (220, 188))):
        hand += rect(x, y, 26, 16, ("#5a3a20", "#3a2a1a", "#6a4a2a")[i % 3], 'rx="2" stroke="#2a1a0a" stroke-width="1"') + rect(x + 18, y + 5, 6, 6, "#c9a24a", 'rx="1"')
    for x in (100, 278):  # Thumb and little finger cupping the hull
        hand += rect(x - 12, 96, 24, 50, "#7a4a2a", 'rx="12" stroke="#2a1a0a" stroke-width="1.5"')
    body += hand
    ship = poly([(70, 92), (290, 92), (270, 128), (96, 128)], "#f4f4f4", 'stroke="#555" stroke-width="2"')
    ship += rect(70, 104, 220, 8, "#1a3a6a") + lettering("MEGABANK", 182, 114, 9, "#1a3a6a", width=2)
    ship += rect(110, 70, 150, 22, "#e8e8e8", 'stroke="#555" stroke-width="1.5"') + rect(150, 52, 70, 18, "#e8e8e8", 'stroke="#555" stroke-width="1.5"')
    for x in range(118, 256, 14):
        ship += circle(x, 81, 3, "#4a6a8a")
    ship += rect(196, 34, 14, 20, "#c0392b") + rect(196, 34, 14, 5, "#1a1a1a")
    body += group(ship, "rotate(-8 180 100)")
    for x in (130, 164):  # Executives popping champagne
        body += person(x, 76, 0.45, SKIN[0], "#1a1a1a", face="grin", hair=OLD_HAIR, tie="#c0392b")
    body += stroke("M176 50 L192 30", "#1a5a2a", 5) + stroke("M192 30 l 4 -5", "#d4af37", 3)
    for i in range(7):
        body += circle(198 + i * 5, 22 - (i % 3) * 5, 2, "#fff6c0")
    for x in (60, 300):  # The splash around the hand
        body += ellipse(x, 146, 26, 4, "#e8f8ff", 'opacity="0.7"')
    return svg(defs, body)


def flash_crash():
    # A stock chart dropping off a cliff into a canyon, tiny traders falling with it, a trading robot at the top shrugging
    defs = lin("bg", ["#f0a868", "#7a4a8a"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += poly([(0, 96), (200, 96), (214, 200), (0, 200)], "#8a5a3a") + poly([(0, 96), (200, 96), (196, 106), (0, 106)], "#a8744a")
    body += poly([(276, 124), (352, 124), (352, 200), (264, 200)], "#7a4a2a")
    body += poly([(214, 200), (264, 200), (250, 186), (226, 188)], "#2a1a1a")
    body += stroke("M14 80 L44 70 L70 76 L100 58 L128 66 L156 46 L184 52 L206 40 L232 194", "#ff3a3a", 5)
    body += poly([(232, 198), (224, 184), (240, 184)], "#ff3a3a")
    for x, y, a in ((226, 120, 40), (246, 152, -30), (220, 168, 120)):  # The tiny traders falling
        body += group(person(x, y, 0.3, SKIN[0], "#2a2a3a", face="shout", tie="#c0392b"), f"rotate({a} {x} {y - 12})")
    # The robot, shrugging
    body += rect(138, 66, 34, 30, "#b8c0c8", 'rx="4" stroke="#5a6068" stroke-width="2"')
    body += rect(144, 44, 22, 20, "#c8d0d8", 'rx="3" stroke="#5a6068" stroke-width="2"') + stroke("M155 44 L155 36", "#5a6068", 2) + circle(155, 34, 3, "#ff3a3a")
    body += circle(150, 54, 3, "#2a2a2a") + circle(160, 54, 3, "#2a2a2a") + stroke("M150 60 L160 60", "#2a2a2a", 1.5)
    body += stroke("M138 74 L124 66 L120 54 M172 74 L186 66 L190 54", "#5a6068", 4)
    body += words("?", 182, 26, 12, "#ffffff", width=2.2)
    return svg(defs, body)


def hedonic_treadmillionaire():
    # A billionaire in a gold tracksuit on a treadmill on a yacht on a bigger yacht, sweating, his smile not moving
    defs = lin("bg", ["#7ac8f0", "#e8f4fa"]) + lin("sea", ["#2a8ac0", "#0a3a6a"])
    body = rect(0, 0, W, H, "url(#bg)") + circle(300, 34, 18, "#fff2a0") + rect(0, 168, W, 32, "url(#sea)")
    body += poly([(20, 150), (334, 150), (304, 184), (52, 184)], "#f8f8f8", 'stroke="#8a949c" stroke-width="2"') + rect(40, 164, 280, 5, "#1a4a8a")
    body += rect(80, 128, 196, 22, "#f0f0f0", 'stroke="#8a949c" stroke-width="1.5"')
    for x in range(92, 270, 16):
        body += rect(x, 134, 10, 8, "#2a4a6a")
    body += poly([(104, 108), (256, 108), (242, 128), (118, 128)], "#f8f8f8", 'stroke="#8a949c" stroke-width="1.5"') + rect(112, 116, 136, 4, "#d4af37")
    body += rect(140, 96, 80, 12, "#f0f0f0", 'stroke="#8a949c" stroke-width="1.5"')
    # The treadmill on top
    body += rect(132, 88, 96, 8, "#2a2a30", 'rx="3"') + stroke("M214 88 L222 56", "#3a3a40", 4) + rect(208, 48, 30, 12, "#3a3a40", 'rx="2"')
    body += words("99", 223, 50, 7, "#ff5a5a", width=1.2)
    body += person(178, 90, 0.7, SKIN[0], "#e8b830", face="grin", hair="#c8a86a")
    body += stroke("M196 70 L214 60 M160 70 L146 84", "#e8b830", 7)
    for x, y in ((158, 30), (198, 28), (204, 44), (152, 46)):
        body += sweat(x, y, 0.8)
    body += speed_lines(128, 70, 3, 24, "#ffffff")
    return svg(defs, body)


def high_frequency_trading_scam():
    # A server rack in a basement next to the stock exchange, cables running under the street, a trader's face reflected in
    # a screen of numbers moving too fast to read
    defs = lin("bg", ["#8aa0b8", "#5a6a7a"]) + lin("cellar", ["#2a2a2e", "#121214"])
    body = rect(0, 0, W, 60, "url(#bg)") + rect(0, 60, W, 140, "url(#cellar)") + rect(0, 52, W, 10, "#4a4a50")
    body += rect(250, 0, 102, 52, "#d8d0c0") + poly([(244, 0), (352, 0), (352, 6), (244, 6)], "#b8b0a0")
    for x in (262, 286, 310, 334):
        body += rect(x, 10, 8, 42, "#f0e8d8")
    body += lettering("EXCHANGE", 300, 2, 4, "#5a5040", width=1)
    body += server_rack(20, 76, 56, 112)
    for i, c in enumerate(("#ffd23a", "#3aa0ff", "#ff5a5a")):  # Cables under the street to the exchange
        body += stroke(f"M76 {100 + i * 10} L{220 + i * 6} {100 + i * 10} L{220 + i * 6} 68 L{300 + i * 8} 68 L{300 + i * 8} 52", c, 3)
    body += monitor(120, 112, 150, 70, "#06120c")
    for row in range(5):
        for col in range(8):
            body += rect(132 + col * 16 + (row % 2) * 4, 124 + row * 9, 10, 4, "#3aff7a", 'opacity="0.75"')
    body += speed_lines(262, 124, 4, 30, "#3aff7a")
    body += group(person(196, 178, 0.8, SKIN[0], "#2a2a3a", face="grin", hair="#2a2a2a", glasses="round"), "translate(0 0)", 0.25)
    return svg(defs, body)


def internal_protocol_quota_crusher_mindset():
    # A motivational poster over a cubicle farm, a lone runner on a mountain top under QUOTA CRUSHER, the workers below asleep
    defs, body = room("#c8c4b4", "#a8a494", "#6a6a6a", 168)
    body += rect(96, 8, 160, 92, "#1a1a1a") + rect(102, 14, 148, 66, "#4a8ad0")
    body += poly([(110, 80), (176, 22), (242, 80)], "#5a6a7a") + poly([(164, 33), (176, 22), (188, 33), (180, 30), (172, 34)], "#ffffff")
    body += stroke("M176 22 L176 16 M176 18 L171 21 M176 18 L181 15 M176 16 L172 22 M176 16 L180 23", "#1a1a1a", 1.6) + circle(176, 13, 2.2, "#1a1a1a")
    body += lettering("QUOTA CRUSHER", 176, 85, 7, "#ffffff", width=1.4)
    for i, x in enumerate((44, 132, 220, 308)):
        body += person(x, 172, 0.7, SKIN[i % 4], ("#6a7a8a", "#8a6a6a", "#6a8a6a", "#7a6a8a")[i], face="closed", hair=("#3a2a1a", None, "#1a1a1a", "#8a6a3a")[i])
        body += zzz(x + 14, 104, 0.6, "#3a3a6a")
    for x in (0, 88, 176, 264):  # The cubicle walls
        body += rect(x, 140, 84, 60, "#8a8a92", 'stroke="#5a5a62" stroke-width="2"')
    for x in (44, 132, 220, 308):
        body += rect(x - 18, 132, 36, 10, "#2a2a30", 'rx="2"')
    return svg(defs, body)


def just_in_time():
    # A delivery van screeching up to a factory door as the last box on the assembly line runs out, a manager holding a
    # stopwatch at 0:00
    defs, body = room("#9aa0a8", "#6a7078", "#4a4a4a", 170)
    body += rect(150, 40, 110, 130, "#5a6068") + rect(156, 46, 98, 124, "#2a2e34")
    for y in range(46, 170, 12):
        body += stroke(f"M156 {y} L254 {y}", "#3a3e44", 2)
    body += rect(0, 132, 150, 10, "#3a3a40") + rect(0, 142, 150, 28, "#2a2a30")
    for x in range(8, 150, 18):
        body += circle(x, 137, 4, "#7a7a80")
    body += rect(118, 112, 22, 20, "#c8a06a", 'stroke="#8a6a3a" stroke-width="1"') + stroke("M118 122 L140 122", "#8a6a3a", 1)
    body += person(56, 132, 0.8, SKIN[1], "#4a5a7a", face="flat", hair="#2a2a2a", tie="#c0392b")
    body += circle(88, 62, 14, "#f4f4f4", 'stroke="#555" stroke-width="3"') + rect(85, 44, 6, 5, "#555") + words("0:00", 88, 58, 7, "#c0392b", width=1.2)
    body += stroke("M72 98 L84 74", "#4a5a7a", 6)
    body += van(290, 186, 0.62, face=-1)
    body += speed_lines(352, 150, 3, 30, "#ffffff")
    for x in (318, 334):
        body += ellipse(x, 184, 12, 5, "#c8c8c8", 'opacity="0.7"')
    return svg(defs, body)


def middle_manager():
    # A middle manager carrying a box of desk things out of the office, a 'Synergy' mug on top, already on the phone to get
    # rehired as a consultant
    defs, body = bg_office()
    body += person(176, 200, 1.4, SKIN[0], "#5a6a80", face="smile", hair="#8a6a4a", tie="#c0392b")
    body += phone(206, 104, 0.6, "#2a2a30")
    body += stroke("M210 160 L214 124", "#5a6a80", 10)
    body += rect(122, 150, 108, 50, "#c8a06a", 'stroke="#8a6a3a" stroke-width="2"') + stroke("M122 162 L230 162", "#8a6a3a", 1.5)
    body += rect(132, 132, 22, 20, "#2a7a3a") + rect(196, 128, 20, 24, "#f4f4f4", 'stroke="#555" stroke-width="1"')
    body += rect(160, 120, 30, 30, "#ffffff", 'rx="3" stroke="#555" stroke-width="1.5"') + stroke("M190 128 q 8 0 8 7 q 0 7 -8 7", "#555", 2)
    body += lettering("SYNERGY", 175, 132, 4, "#c0392b", width=1)
    body += stroke("M140 132 l -6 -16 M144 132 l 2 -18 M148 132 l 8 -14", "#3a8a3a", 2)  # A desk plant
    body += bubble(290, 48, 54, 22, tail=(222, 96)) + lettering("CONSULTANT", 290, 44, 6, "#333", width=1.3)
    return svg(defs, body)


def moral_bankruptcy():
    # A banker stamping BANKRUPT across a smiling worker's portrait, the worker's chair already being carried away by movers
    defs, body = room("#d8ccb0", "#b8a888", "#6a5a4a", 166)
    body += rect(112, 20, 108, 120, "#8a6a2a", 'stroke="#5a4a1a" stroke-width="3"') + rect(120, 28, 92, 104, "#e8f0f8")
    body += person(166, 132, 0.95, SKIN[2], "#4a7aa0", face="grin", hair="#1a1a1a")
    body += stamp("BANKRUPT", 166, 82, 11, "#c0392b", angle=-18)
    body += person(282, 200, 1.3, SKIN[0], "#1a1a1a", face="smirk", hair="#2a2a2a", tie="#d4af37", brows="angry")
    body += stroke("M252 160 L222 116", "#1a1a1a", 10) + rect(206, 100, 18, 14, "#5a3a20", 'rx="2"') + rect(212, 86, 6, 16, "#5a3a20")
    # The movers with the chair on the left
    body += group(rect(10, 98, 40, 34, "#3a3a6a", 'rx="5"') + rect(10, 132, 40, 8, "#3a3a6a") + stroke("M16 140 L14 156 M44 140 L46 156", "#222", 3), "rotate(-14 30 128)")
    body += person(32, 200, 0.8, SKIN[1], "#c06a2a", face="flat", hat="cap") + person(76, 200, 0.7, SKIN[3], "#c06a2a", face="flat", hat="cap")
    return svg(defs, body)


def smartwatch():
    # A sleek smartwatch on a sweating wrist flashing 'Stand up! Productivity -3%', a subscription renewal sliding in underneath
    defs, body = bg_office()
    body += path("M0 92 C 120 86 240 88 352 98 L352 158 C 240 150 120 148 0 154 Z", SKIN[0], 'stroke="#b08a6a" stroke-width="2"')
    body += rect(146, 52, 60, 150, "#1a1a1e", 'rx="10"')
    body += rect(134, 70, 84, 84, "#2a2a30", 'rx="18" stroke="#5a5a62" stroke-width="3"') + rect(142, 78, 68, 68, "#06080c", 'rx="12"')
    body += lettering("STAND", 176, 86, 7, "#ffd23a", width=1.4) + lettering("UP!", 176, 98, 7, "#ffd23a", width=1.4)
    body += words("-3%", 176, 116, 9, "#ff3a3a", width=1.8)
    body += rect(220, 108, 4, 12, "#5a5a62", 'rx="1"')
    for x, y in ((70, 106), (100, 126), (262, 112), (300, 132), (40, 130)):
        body += sweat(x, y, 0.8)
    body += popup(232, 156, 100, 38, "#2a7ad0") + lettering("RENEW", 282, 172, 6, "#2a2a30", width=1.2)
    body += speed_lines(232, 170, 2, 20, "#ffffff")
    return svg(defs, body)


def usury():
    # A loan shark in a pinstripe suit feeding coins into a piggy bank swollen to the size of a house, a tiny debtor pinned
    # under its trotter
    defs, body = bg_office("up")
    body += ellipse(206, 110, 120, 70, "#f4a8b8", 'stroke="#c06a80" stroke-width="3"')
    body += ellipse(318, 108, 16, 22, "#f8b8c8", 'stroke="#c06a80" stroke-width="2"') + ellipse(316, 102, 4, 6, "#c06a80") + ellipse(316, 116, 4, 6, "#c06a80")
    body += poly([(262, 50), (280, 30), (290, 60)], "#f4a8b8", 'stroke="#c06a80" stroke-width="2"') + circle(290, 80, 4, "#2a2a2a")
    body += rect(176, 42, 40, 6, "#5a2a3a", 'rx="3"')
    for x in (130, 170, 250, 284):
        body += rect(x, 166, 22, 26, "#f4a8b8", 'rx="4" stroke="#c06a80" stroke-width="2"')
    body += person(250, 196, 0.32, SKIN[0], "#8a8a8a", face="wavy", brows="sad")  # Pinned under the trotter
    # The loan shark, feeding a coin into the slot
    shark = person(64, 200, 1.25, "#9aa8b4", "#2a2a3a", face="grin", hair="#1a1a1a", tie="#c0392b", hat="fedora")
    for x in range(30, 104, 9):
        shark += stroke(f"M{x} 200 L{x + 2} 156", "#5a5a6a", 1)
    body += shark + poly([(48, 92), (58, 68), (66, 92)], "#9aa8b4")
    body += stroke("M90 156 L150 66 L184 40", "#2a2a3a", 9) + coin(192, 36, 9)
    return svg(defs, body)


def venture_capital():
    # A man in a fleece vest pouring a bucket of gold coins into a lanky founder's head through a funnel, the founder
    # growing taller and more delusional
    defs, body = bg_office("up")
    # The founder: a long neck, a funnel in his head and stars in his eyes
    body += path("M196 200 C 196 160 268 160 268 200 Z", "#1a1a1a") + rect(226, 80, 12, 92, SKIN[0])
    body += circle(232, 76, 20, SKIN[0]) + path("M212 72 C 214 52 250 52 252 72 Z", "#8a6a3a")
    for dx in (-7, 7):
        body += poly(star_points(232 + dx, 74, 5, 2), "#ffd23a")
    body += path("M222 84 Q 232 94 242 84 Z", "#ffffff", 'stroke="#222" stroke-width="1.2"')
    body += poly([(214, 22), (250, 22), (236, 56), (228, 56)], "#b8c0c8", 'stroke="#5a6068" stroke-width="2"')
    body += arrow(300, 180, 300, 110, "#5fe08a", 5)
    # The VC in his fleece vest, tipping the bucket
    body += person(100, 200, 1.3, SKIN[0], "#7aa0c8", face="smile", hair="#c8a86a")
    body += path("M70 200 L74 150 C 86 140 114 140 126 150 L130 200 L112 200 L104 160 L96 160 L88 200 Z", "#3a4a5a")
    body += stroke("M128 160 L166 40", "#7aa0c8", 9)
    body += group(rect(148, 6, 40, 34, "#8a8a92", 'stroke="#5a5a62" stroke-width="2"') + ellipse(168, 6, 20, 5, "#c9952a"), "rotate(40 168 22)")
    for i, (x, y) in enumerate(((198, 18), (210, 8), (222, 14), (216, 28))):
        body += coin(x, y, 6)
    return svg(defs, body)


# ---- Digital Society

def influencer():
    # An influencer posing in front of a melting glacier with a branded water bottle, an assistant holding a reflector, the
    # caption 'so grateful' on the phone
    defs = lin("bg", ["#8ad0f0", "#e8f8ff"]) + lin("ice", ["#ffffff", "#a8d8f0"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 160, W, 40, "#4a8ab0")
    body += poly([(0, 160), (20, 70), (70, 50), (120, 80), (170, 40), (230, 70), (280, 56), (352, 90), (352, 160)], "url(#ice)")
    for x in (40, 96, 150, 210, 262, 320):
        body += path(f"M{x - 6} 150 Q {x} 176 {x + 6} 150 Z", "#a8d8f0")
        body += ellipse(x, 168, 8, 2, "#ffffff", 'opacity="0.6"')
    body += person(150, 200, 1.35, SKIN[1], "#f4a0c0", face="smile", hat="bob", glasses="shades")
    body += stroke("M120 160 L104 110", "#f4a0c0", 9) + rect(94, 82, 16, 32, "#4ac0e0", 'rx="4"') + rect(97, 76, 10, 8, "#1a6a8a")
    body += lettering("AQUA", 102, 94, 4, "#ffffff", width=1)
    body += ellipse(276, 104, 30, 36, "#f4f0d0", 'stroke="#c9a24a" stroke-width="3"')
    body += person(308, 200, 0.85, SKIN[3], "#5a5a62", face="flat", hat="cap")
    body += phone(46, 140, 1.5, "#ffffff")
    body += rect(34, 120, 24, 22, "#a8d8f0") + lettering("SO", 46, 146, 5, "#333", width=1.1) + lettering("GRATEFUL", 46, 154, 3, "#333", width=0.9)
    return svg(defs, body)


def selfie_esteem():
    # A girl holding a selfie stick at arm's length, the phone showing a perfect filtered face while the real one cries,
    # a ring light glowing
    defs, body = bg_digital()
    body += circle(80, 80, 52, "none", 'stroke="#fff8d0" stroke-width="12" opacity="0.9"') + circle(80, 80, 60, "none", 'stroke="#fff8d0" stroke-width="4" opacity="0.3"')
    body += stroke("M80 132 L80 200", "#2a2a30", 4)
    body += person(160, 200, 1.35, SKIN[0], "#5a3a8a", face="wavy", hat="bob", brows="sad")
    for x in (148, 172):
        body += path(f"M{x} 120 q -2 8 0 14 q 2 -6 0 -14 Z", "#8fd8ff") + stroke(f"M{x} 122 l 0 10", "#8fd8ff", 2)
    body += stroke("M190 156 L290 56", "#3a3a42", 3) + stroke("M190 156 L210 136", "#5a3a8a", 9)
    body += group(phone(0, 0, 1.4, "#ffe8f0"), "translate(298 46) rotate(-20)")
    body += group(circle(0, 2, 10, SKIN[0]) + circle(-4, 0, 1.5, "#222") + circle(4, 0, 1.5, "#222") + stroke("M-4 5 Q 0 9 4 5", "#c0392b", 1.4)
                  + path("M-11 2 C -11 -14 11 -14 11 2 L6 -4 Z", "#e8c86a"), "translate(298 46) rotate(-20)")
    for x, y in ((270, 20), (322, 30), (314, 76)):
        body += poly(star_points(x, y, 5, 2, 4), "#ffffff")
    return svg(defs, body)


def u_mad_bro():
    # A smug teenager in a gaming chair, a trollface on his hoodie, typing 'u mad bro?' while furious replies pile up
    defs, body = room("#1a1430", "#0a0818", "#14101e", 176)
    body += monitor(186, 12, 156, 110, "#0e1222")
    for i in range(6):
        y = 22 + i * 13
        body += rect(198 + (i % 2) * 10, y, 112 - (i % 3) * 14, 10, "#c0392b", 'rx="3"') + lettering("!!!", 214 + (i % 2) * 10, y + 2, 6, "#ffffff", width=1.2)
    body += rect(196, 100, 136, 12, "#2a3a6a", 'rx="3"') + words("U MAD BRO?", 264, 102, 7, "#ffffff", width=1.2)
    body += rect(60, 40, 90, 160, "#c0392b", 'rx="18"') + rect(70, 50, 70, 120, "#1a1a1a", 'rx="12"')
    body += person(104, 200, 1.3, SKIN[0], "#3a3a44", face="smirk", hair="#c8a86a", hat="headband")
    body += circle(104, 172, 15, "#f4f4f4") + path("M93 172 Q 104 186 115 172 Z", "#ffffff", 'stroke="#222" stroke-width="1.5"')
    body += stroke("M96 166 l 5 2 M112 166 l -5 2", "#222", 1.5)
    body += rect(150, 172, 120, 12, "#2a2a30", 'rx="3"') + stroke("M130 178 L170 176 M132 186 L180 182", "#3a3a44", 9)
    return svg(defs, body)


# ---- Mind

def hold_that_thought():
    # A man frozen mid-sentence with a finger raised, a lightbulb above his head stuck behind a pause icon, everyone at the
    # dinner table waiting
    defs, body = bg_mind()
    body += person(70, 166, 0.9, SKIN[2], "#c06a8a", face="flat", hat="bob") + person(282, 166, 0.9, SKIN[0], "#6a8ac0", face="flat", hair="#3a2a1a")
    body += person(176, 186, 1.2, SKIN[0], "#3a6a5a", face="shout", hair="#5a3a1a")
    body += stroke("M210 150 L224 96", "#3a6a5a", 9) + rect(220, 76, 7, 22, SKIN[0], 'rx="3"') + circle(224, 100, 6, SKIN[0])
    body += circle(176, 36, 18, "#fff3a0", 'stroke="#c9a24a" stroke-width="2"') + rect(169, 52, 14, 10, "#8a8a92", 'rx="2"')
    body += circle(176, 36, 26, "#fff3a0", 'opacity="0.3"')
    body += rect(166, 26, 6, 20, "#2a2a30") + rect(180, 26, 6, 20, "#2a2a30")
    body += rect(0, 168, W, 32, "#f4f4f4") + rect(0, 168, W, 4, "#d8d8d8")
    for x in (70, 176, 282):
        body += ellipse(x, 182, 22, 6, "#ffffff", 'stroke="#c8c8c8" stroke-width="1.5"')
    body += words("...", 70, 82, 10, "#5a4a8a", width=2.5) + words("...", 282, 82, 10, "#5a4a8a", width=2.5)
    return svg(defs, body)


def hyperbole():
    # A man telling a story at a bar with his arms spread wider and wider, the fish he describes growing from a sardine to a
    # whale in thought bubbles behind him
    defs, body = room("#5a3a2a", "#2a1a10", "#3a2412", 170)
    for r, (x, y) in ((6, (60, 92)), (10, (52, 66)), (22, (70, 30))):
        body += ellipse(x, y, r * 1.5, r, "#ffffff", 'stroke="#333" stroke-width="1.5"')
    body += fish(70, 30, 0.5, "#a8b8c8")
    body += ellipse(176, 34, 52, 28, "#ffffff", 'stroke="#333" stroke-width="1.5"') + fish(176, 34, 1.3, "#7ab0d0")
    body += ellipse(290, 54, 60, 48, "#ffffff", 'stroke="#333" stroke-width="1.5"')
    body += ellipse(290, 58, 48, 22, "#4a6a9a") + poly([(244, 58), (226, 38), (230, 76)], "#4a6a9a") + circle(320, 52, 3, "#ffffff")
    body += path("M302 78 Q 320 76 334 66", "none", 'stroke="#ffffff" stroke-width="2"') + stroke("M300 36 q -4 -14 -10 -16 M300 36 q 4 -14 10 -16", "#8ad0f0", 2.5)
    body += person(176, 200, 1.3, SKIN[0], "#8a3a2a", face="grin", hair="#3a2a1a")
    body += stroke("M146 162 L50 128 M206 162 L302 128", "#8a3a2a", 10) + circle(46, 126, 7, SKIN[0]) + circle(306, 126, 7, SKIN[0])
    body += rect(0, 176, W, 24, "#7a4a2a") + rect(300, 140, 18, 36, "#ffd23a", 'opacity="0.8" rx="2"') + rect(300, 136, 18, 8, "#ffffff")
    return svg(defs, body)


def projection():
    # A man on a therapist's couch pointing at her, a film projector behind him throwing his own dark shadow-monster onto the
    # wall behind her chair
    defs, body = room("#c8b8a0", "#9a8a72", "#6a4a32", 168)
    body += poly([(316, 92), (8, 10), (8, 150)], "#fff8d0", 'opacity="0.35"')
    body += path("M20 140 C 10 100 30 70 50 60 L44 30 L62 54 C 76 50 90 52 100 60 L112 32 L114 66 C 130 86 126 120 120 140 Z", "#1a1420", 'opacity="0.85"')
    body += circle(60, 82, 5, "#ff3a3a") + circle(92, 82, 5, "#ff3a3a") + path("M58 104 L66 112 L74 104 L82 112 L90 104 L98 112", "none", 'stroke="#ffffff" stroke-width="2"')
    body += armchair(100, 196, 0.95, "#5a7a5a")
    body += person(100, 186, 0.9, SKIN[0], "#7a5a8a", face="frown", hat="bob", glasses="round")
    body += rect(160, 150, 130, 30, "#8a3a2a", 'rx="8"') + rect(150, 132, 20, 48, "#8a3a2a", 'rx="8"')
    body += person(230, 182, 0.95, SKIN[1], "#3a5a7a", face="shout", hair="#2a2a2a", brows="angry")
    body += stroke("M206 150 L150 128", "#3a5a7a", 8) + rect(132, 124, 18, 5, SKIN[1], 'rx="2"')
    body += rect(300, 82, 44, 30, "#2a2a30", 'rx="4"') + circle(300, 97, 9, "#5a5a62") + circle(300, 97, 5, "#fff8d0")
    body += circle(312, 74, 10, "none", 'stroke="#2a2a30" stroke-width="4"') + circle(334, 74, 10, "none", 'stroke="#2a2a30" stroke-width="4"')
    body += stroke("M322 112 L314 168 M322 112 L330 168", "#2a2a30", 3)
    return svg(defs, body)


def psychiatrists():
    # Two psychiatrists in armchairs taking notes while a patient on the couch has his emotional baggage (suitcases labelled
    # 'Mom', 'Ex', 'Work') taken away one by one
    defs, body = room("#d8d0e8", "#b8a8d0", "#7a6a5a", 170)
    body += armchair(46, 196, 0.85, "#5a4a7a") + armchair(306, 196, 0.85, "#5a4a7a")
    body += person(46, 188, 0.8, SKIN[0], "#4a4a52", face="flat", hair=OLD_HAIR, glasses="round", beard="#d8d8d8")
    body += person(306, 188, 0.8, SKIN[2], "#7a3a3a", face="flat", hat="bob", glasses="round")
    for x in (64, 288):
        body += paper(x - 10, 150, 22, 28, 3) + stroke(f"M{x + 8} 148 L{x + 16} 136", "#2a2a2a", 2)
    body += rect(108, 150, 136, 32, "#8a5a3a", 'rx="10"') + rect(100, 130, 22, 52, "#8a5a3a", 'rx="10"')
    body += person(176, 180, 1.0, SKIN[1], "#6a8a6a", face="smile", hair="#5a3a1a")
    body += suitcase(130, 136, 0.75, "#a0522d", "MOM")
    body += group(suitcase(0, 0, 0.75, "#6a5a8a", "EX"), "translate(206 64) rotate(10)") + stroke("M206 50 L206 30", "#555", 1.5, 'stroke-dasharray="3 3"')
    body += group(suitcase(0, 0, 0.75, "#5a6a5a", "WORK"), "translate(252 30) rotate(18)")
    body += arrow(150, 112, 186, 84, "#ffffff", 3) + arrow(220, 52, 236, 42, "#ffffff", 3)
    return svg(defs, body)


# ---- Myth

def ambrosia():
    # A golden bowl of glowing honey on an Olympus banquet table, Zeus in sunglasses dipping a finger in, the jar's label
    # reading 'Organic, Artisanal, Divine'
    defs, body = bg_myth()
    defs += rad("glow", [("#fff2a0", 0.9), ("#fff2a0", 0)])
    body += person(120, 170, 1.3, SKIN[0], "#f4f0e6", face="smile", hair=OLD_HAIR, glasses="shades", beard="#f4f4f4")
    body += path("M86 170 L120 120 L154 170 Z", "#e8e0c8", 'opacity="0.6"')
    body += stroke("M150 150 L196 140", "#f4f0e6", 10) + rect(194, 134, 14, 6, SKIN[0], 'rx="3"')
    body += rect(0, 160, W, 40, "#f4f0e6") + rect(0, 160, W, 6, "#d4af37")
    body += circle(212, 150, 46, "url(#glow)")
    body += path("M176 146 L248 146 C 246 176 178 176 176 146 Z", "#d4af37", 'stroke="#a87a1a" stroke-width="2"') + ellipse(212, 146, 36, 6, "#ffb82a")
    body += rect(204, 176, 16, 6, "#d4af37")
    body += rect(270, 104, 50, 60, "#f8e8b0", 'rx="8" stroke="#c9a24a" stroke-width="2" opacity="0.95"') + rect(266, 96, 58, 10, "#8a5a2a", 'rx="3"')
    body += rect(274, 120, 42, 34, "#ffffff", 'stroke="#c9a24a" stroke-width="1"')
    body += lettering("ORGANIC", 295, 124, 4, "#6a4a1a", width=0.9) + lettering("ARTISANAL", 295, 133, 3.6, "#6a4a1a", width=0.9)
    body += lettering("DIVINE", 295, 143, 4.4, "#c0392b", width=1)
    for x, y in ((30, 40), (330, 50), (250, 30)):
        body += bolt(x, y, 0.5)
    return svg(defs, body)


def blessing_in_disguise():
    # A man sitting in a crashed car in a ditch, a halo of light around him, an angel in the back seat handing him a winning
    # lottery ticket
    defs = lin("bg", ["#2a2a5a", "#8a6a8a"]) + rad("halo", [("#fff6c0", 0.85), ("#fff6c0", 0)])
    body = rect(0, 0, W, H, "url(#bg)") + poly([(0, 140), (352, 120), (352, 200), (0, 200)], "#3a5a2a") + poly([(60, 200), (130, 150), (300, 150), (352, 200)], "#2a3a1a")
    car = rect(70, 100, 220, 50, "#c0392b", 'rx="12" stroke="#7a1a1a" stroke-width="2"')
    car += path("M110 100 L136 66 L240 66 L262 100 Z", "#c0392b", 'stroke="#7a1a1a" stroke-width="2"')
    car += path("M120 98 L140 72 L184 72 L184 98 Z", "#bfe0f0") + path("M192 98 L192 72 L236 72 L252 98 Z", "#bfe0f0")
    car += circle(110, 150, 16, "#222") + circle(250, 150, 16, "#222") + circle(110, 150, 6, "#888") + circle(250, 150, 6, "#888")
    car += stroke("M70 120 l 14 -6 l -6 14 l 12 -4", "#7a1a1a", 2) + rect(286, 110, 8, 12, "#ffd23a")
    body += circle(162, 80, 60, "url(#halo)")
    body += group(car, "rotate(12 180 120)")
    body += circle(158, 86, 13, SKIN[0]) + circle(154, 84, 1.8, "#222") + circle(162, 84, 1.8, "#222") + ellipse(158, 92, 3, 3.5, "#5a1a1a")
    body += path("M146 80 C 146 66 170 66 170 80 Z", "#5a3a1a")
    body += ellipse(158, 66, 13, 4, "none", 'stroke="#ffd23a" stroke-width="2.5"')
    body += group(angel(0, 0, 0.9), "translate(214 92) rotate(12)")
    body += group(rect(-12, -7, 24, 14, "#f4f0c8", 'stroke="#c9a24a" stroke-width="1.5"') + lettering("WIN", 0, -4, 6, "#c0392b", width=1.2), "translate(184 78) rotate(-8)")
    for x in (40, 314):
        body += ellipse(x, 60, 18, 8, "#c8c8c8", 'opacity="0.3"')
    return svg(defs, body)


def die_by_the_sword():
    # A knight run through by his own sword which bounced back off a mirror, a smug monk holding the mirror
    defs, body = bg_myth()
    body += rect(0, 176, W, 24, "#6a5a3a")
    body += knight(96, 200, 1.2)
    body += stroke("M150 104 L52 176", "#c8d0d8", 6) + stroke("M150 104 L52 176", "#ffffff", 2) + rect(150, 90, 6, 24, "#6a4a2a", 'transform="rotate(54 153 102)"')
    body += stroke("M140 96 L162 112", "#d4af37", 6)
    body += path("M162 104 C 200 70 220 70 248 92", "none", 'stroke="#ffffff" stroke-width="2.5" stroke-dasharray="6 5"')
    body += path("M248 110 C 220 128 196 128 166 112", "none", 'stroke="#ffd23a" stroke-width="2.5" stroke-dasharray="6 5"')
    body += poly([(166, 112), (178, 108), (174, 120)], "#ffd23a")
    body += person(300, 200, 1.2, SKIN[0], "#6a4a2a", face="smirk", hair=OLD_HAIR, brows="angry")
    body += circle(300, 123, 9, SKIN[0])
    body += ellipse(258, 102, 18, 30, "#d8e8f0", 'stroke="#d4af37" stroke-width="5"') + stroke("M250 88 l 8 -6 M248 100 l 12 -10", "#ffffff", 2)
    body += rect(254, 132, 8, 22, "#d4af37") + stroke("M276 160 L262 146", "#6a4a2a", 9)
    return svg(defs, body)


def gracias_jesus():
    # Jesus in a straw hat carrying two big baskets of vegetables in from the fields at sunset, a family at the farmhouse
    # table clapping
    defs = lin("bg", ["#f0a050", "#f8d890"])
    body = rect(0, 0, W, H, "url(#bg)") + circle(70, 92, 30, "#fff2b0")
    body += rect(0, 110, W, 90, "#8a9a3a")
    for i in range(6):
        body += stroke(f"M0 {120 + i * 14} L210 {116 + i * 16}", "#5a7a2a", 4)
    body += rect(250, 60, 102, 70, "#c8a878") + poly([(240, 64), (300, 26), (352, 56), (352, 64)], "#8a3a2a") + rect(274, 92, 22, 38, "#6a4a2a")
    body += rect(236, 150, 116, 10, "#8a5a3a") + stroke("M246 160 L246 184 M342 160 L342 184", "#6a4a2a", 4)
    for x, sk in ((258, SKIN[0]), (290, SKIN[1]), (322, SKIN[0])):
        body += person(x, 152, 0.5, sk, ("#4a7aa0", "#c06a2a", "#8a4a8a")[(x // 30) % 3], face="grin")
        body += stroke(f"M{x - 8} 136 l 6 -6 M{x + 8} 136 l -6 -6", "#ffffff", 1.5)
    body += jesus(140, 200, 1.25, face="grin")
    body += ellipse(140, 100, 36, 7, "#e8c860", 'stroke="#a8882a" stroke-width="1.5"') + path("M122 100 C 122 78 158 78 158 100 Z", "#e8c860", 'stroke="#a8882a" stroke-width="1.5"')
    body += rect(122, 92, 36, 4, "#c0392b")
    for bx in (78, 202):
        body += stroke(f"M{140 + (bx - 140) * 0.45} 160 L{bx} 150", "#f4f0e6", 9)
        body += path(f"M{bx - 24} 150 L{bx + 24} 150 L{bx + 18} 186 L{bx - 18} 186 Z", "#a0703a", 'stroke="#6a4a1a" stroke-width="2"')
        body += circle(bx - 12, 146, 8, "#d0342c") + circle(bx + 4, 144, 9, "#5aa03a") + circle(bx + 16, 148, 7, "#e8a020")
        body += poly([(bx - 4, 140), (bx + 2, 124), (bx + 6, 140)], "#ff8a1a") + stroke(f"M{bx + 2} 124 l -3 -6 M{bx + 2} 124 l 3 -6", "#3a8a2a", 2)
    return svg(defs, body)


def grim_reaper():
    # The Grim Reaper waiting in a hospital corridor checking his watch, a number ticket in his bony hand, a sign above him
    # reading NOW SERVING 42
    defs, body = room("#d8ecdc", "#a8c8b0", "#c8c8c0", 160)
    for x in range(0, W, 32):
        body += stroke(f"M{x} 160 L{x - 20} 200", "#a8a8a0", 1)
    body += rect(250, 40, 60, 120, "#8ab0c0", 'stroke="#5a7a8a" stroke-width="2"') + rect(276, 90, 8, 4, "#5a7a8a")
    body += rect(20, 128, 100, 8, "#4a6a8a") + stroke("M30 136 L30 160 M110 136 L110 160", "#3a3a3a", 3)
    body += rect(110, 10, 132, 26, "#1a1a1a", 'rx="3"') + words("NOW SERVING 42", 176, 18, 7, "#ff3a2a", width=1.4)
    # The reaper: a black robe and hood, a skull, the scythe
    body += stroke("M60 30 L60 196", "#6a4a2a", 5) + path("M60 32 C 30 20 4 34 0 60 C 18 42 40 38 60 44 Z", "#c8d0d8", 'stroke="#5a6068" stroke-width="1.5"')
    body += path("M100 200 C 100 120 120 70 160 66 C 200 70 220 120 220 200 Z", "#121214")
    body += path("M128 108 C 126 66 194 66 192 108 C 186 92 134 92 128 108 Z", "#121214")
    body += skull(160, 98, 1.0)
    body += stroke("M134 150 L84 130 L62 122", "#121214", 12) + circle(68, 124, 6, "#f2efe6")
    body += stroke("M186 150 L206 118", "#121214", 12) + rect(198, 106, 18, 14, "#f2efe6", 'rx="2"')
    body += rect(196, 110, 22, 8, "#d4af37", 'rx="2"')
    body += group(rect(-12, -9, 24, 18, "#f4f0c8", 'stroke="#8a7a5a" stroke-width="1"') + words("97", 0, -5, 8, "#1a1a1a", width=1.4), "translate(232 104) rotate(12)")
    return svg(defs, body)


def hashmark(x, y, s, color):
    """One '#' of ASCII art in a cell of side s."""
    return stroke(f"M{x + s * 0.35:.1f} {y:.1f} L{x + s * 0.3:.1f} {y + s:.1f} M{x + s * 0.7:.1f} {y:.1f} L{x + s * 0.65:.1f} {y + s:.1f} "
                  f"M{x:.1f} {y + s * 0.35:.1f} L{x + s:.1f} {y + s * 0.35:.1f} M{x:.1f} {y + s * 0.7:.1f} L{x + s:.1f} {y + s * 0.7:.1f}", color, max(1, s * 0.16))


def here_be_dragons():
    # An old parchment sea map ending in a ragged edge, beyond it a big dragon drawn in green ASCII on a black terminal
    # screen, breathing fire made of # signs
    defs = lin("bg", ["#0a0e0a", "#020402"])
    body = rect(0, 0, W, H, "url(#bg)")
    edge = [(150, 0), (162, 18), (148, 36), (166, 58), (152, 80), (170, 104), (154, 126), (168, 150), (150, 172), (164, 200)]
    body += poly([(0, 0)] + edge + [(0, 200)], "#e8d4a0", 'stroke="#a88a5a" stroke-width="2"')
    body += path("M0 60 C 30 50 40 80 70 70 C 96 62 90 110 60 120 C 30 130 20 150 0 156 Z", "#c8a868", 'stroke="#8a6a3a" stroke-width="1.5"')
    for y in (30, 96, 170):
        body += stroke(f"M{90 if y != 96 else 104} {y} q 6 -5 12 0 q 6 5 12 0", "#7a8aa0", 1.5)
    body += poly(star_points(110, 140, 16, 4, 4), "#a0522d") + circle(110, 140, 3, "#e8d4a0")
    body += boat(100, 50, 0.7)
    body += lettering("HERE BE", 70, 180, 6, "#6a4a1a", width=1.2)
    dragon = (".##..............", "####......#...#..", "#####....###.###.", ".######.########.", "...#############.",
              "....###########..", ".....#########...", "......#.....#....", ".....##....##....")
    cell = 8
    for row, line in enumerate(dragon):
        for col, ch in enumerate(line):
            if ch == "#":
                body += hashmark(212 + col * cell, 40 + row * cell, cell * 0.8, "#3aff6a")
    body += circle(222, 52, 2.2, "#ff3a3a")
    fire = ((0, 3), (1, 2), (1, 3), (1, 4), (2, 1), (2, 2), (2, 3), (2, 4), (2, 5), (3, 2), (3, 3), (3, 4), (4, 1), (4, 3), (4, 5))
    for col, row in fire:
        body += hashmark(204 - col * cell, 40 + row * cell - 4, cell * 0.8, "#ffd23a" if col < 2 else "#ff7a1a")
    body += rect(178, 150, 166, 1, "#3aff6a", 'opacity="0.4"') + words("> RUN DRAGON.EXE", 260, 160, 6, "#3aff6a", width=1.1)
    return svg(defs, body)


def heretic():
    # A heretic tied to a stake on a pile of firewood, laughing, swinging a broken chain at the inquisitors as the flames
    # catch, a placard reading 'The Earth is round' nailed above him
    defs = lin("bg", ["#1a0a1a", "#7a2a1a"])
    body = rect(0, 0, W, H, "url(#bg)") + circle(176, 150, 120, "#ff7a1a", 'opacity="0.15"')
    body += stroke("M176 10 L176 170", "#5a3a1a", 10)
    body += rect(126, 14, 100, 30, "#e8d4a0", 'stroke="#6a4a1a" stroke-width="2"') + circle(176, 20, 2, "#3a3a3a")
    body += lettering("THE EARTH", 176, 18, 7, "#3a2a1a", width=1.3) + lettering("IS ROUND", 176, 30, 7, "#c0392b", width=1.3)
    body += person(176, 156, 1.2, SKIN[0], "#d8c8a8", face="grin", hair="#3a2a1a", beard="#3a2a1a")
    body += stroke("M146 132 L206 128 M146 146 L206 142", "#a8885a", 4)
    body += stroke("M206 116 L236 82", "#d8c8a8", 9)
    for i in range(5):  # The broken chain flying
        body += ellipse(242 + i * 12, 76 - i * 6, 6, 4, "none", f'stroke="#9aa0a8" stroke-width="3" transform="rotate({-20 - i * 8} {242 + i * 12} {76 - i * 6})"')
    for i, x in enumerate(range(100, 260, 22)):
        body += group(ellipse(0, 0, 26, 8, "#7a4a2a", 'stroke="#4a2a1a" stroke-width="2"') + ellipse(22, 0, 4, 6, "#c8a070"), f"translate({x} {170 + (i % 2) * 10}) rotate({(-1) ** i * 8})")
    body += flames(176, 184, 6, 22, 28)
    for x in (40, 312):
        body += person(x, 200, 1.1, SKIN[0], "#5a1a1a", face="shout", hat="hood", brows="angry")
    return svg(defs, body)


def mithril():
    # A silver chain-mail shirt glittering on a velvet cushion in a dwarven forge, a tiny hobbit trying it on and a dragon
    # peering in through the window
    defs, body = room("#3a2418", "#1a0e08", "#2a1a10", 168)
    body += circle(40, 150, 60, "#ff7a1a", 'opacity="0.25"') + rect(0, 120, 70, 48, "#4a3a32") + flames(34, 132, 3, 16, 20)
    body += rect(250, 16, 90, 70, "#2a1a3a", 'stroke="#6a5a4a" stroke-width="6"')
    body += path("M250 86 C 266 40 300 30 340 38 L340 86 Z", "#3a8a4a") + ellipse(296, 58, 16, 10, "#ffd23a") + ellipse(296, 58, 3, 9, "#111")
    for x in (270, 310, 326):
        body += path(f"M{x} 82 l 6 -10 l 6 10 Z", "#2a6a3a")
    body += ellipse(170, 162, 70, 16, "#8a1a3a") + rect(100, 148, 140, 16, "#8a1a3a", 'rx="8"') + circle(100, 162, 4, "#d4af37") + circle(240, 162, 4, "#d4af37")
    shirt = path("M128 64 L150 56 Q 170 72 190 56 L212 64 L232 98 L216 106 L206 88 L206 148 L134 148 L134 88 L124 106 L108 98 Z", "#c8d4e0", 'stroke="#7a8a9a" stroke-width="2"')
    for row in range(8):
        for col in range(9):
            shirt += circle(140 + col * 8 + (row % 2) * 4, 76 + row * 9, 3, "none", 'stroke="#8a9aaa" stroke-width="1"')
    body += shirt
    for x, y in ((122, 52), (226, 70), (180, 46), (150, 126)):
        body += poly(star_points(x, y, 7, 2, 4), "#ffffff")
    body += person(272, 200, 0.7, SKIN[0], "#5a7a3a", face="grin", hair="#6a4a2a")
    for dx in (-12, -4, 4, 12):
        body += circle(272 + dx, 140, 5, "#6a4a2a")
    body += stroke("M252 176 L232 150", "#5a7a3a", 6)
    return svg(defs, body)


def publicly_funded_faith_schools():
    # A classroom of kids in uniforms praying over their exam papers instead of writing, a nun at the front under a 'Funded by
    # your taxes' banner hung over the crucifix
    defs, body = room("#e8dcc0", "#c8b898", "#8a6a4a", 176)
    body += rect(166, 6, 20, 70, "#6a4a2a") + rect(140, 24, 72, 16, "#6a4a2a")
    body += path("M70 30 L282 30 L274 56 L78 56 Z", "#ffffff", 'stroke="#555" stroke-width="1.5"')
    body += lettering("FUNDED BY YOUR TAXES", 176, 38, 7, "#2a5aa0", width=1.4)
    body += person(176, 136, 0.95, SKIN[0], "#1a1a1a", face="smile")
    body += path("M152 82 C 152 54 200 54 200 82 L204 116 L148 116 Z", "#1a1a1a") + circle(176, 75, 15, SKIN[0]) + rect(160, 61, 32, 5, "#ffffff")
    body += circle(171, 74, 1.8, "#222") + circle(181, 74, 1.8, "#222") + stroke("M171 81 Q 176 84 181 81", "#222", 1.4)
    for i, x in enumerate((40, 112, 240, 312)):
        body += person(x, 192, 0.65, SKIN[i % 4], "#2a3a6a", face="closed", hair=("#3a2a1a", "#1a1a1a", "#c8a86a", "#6a3a1a")[i])
        body += poly([(x - 4, 172), (x, 158), (x + 4, 172)], SKIN[i % 4], 'stroke="#555" stroke-width="0.8"')
        body += rect(x - 30, 176, 60, 24, "#a07a4a") + paper(x - 14, 178, 28, 14, 2)
        body += words("?", x + 18, 136, 8, "#5a5a8a", width=1.6)
    return svg(defs, body)


# ---- Potions (items without a faction)

def bg_item(top="#3a3f5a", bottom="#14161f", table="#5a3e28"):
    """The potions: a spotlight on an item standing on a table."""
    defs = lin("bg", [top, bottom]) + rad("spot", [("#ffffff", 0.22), ("#ffffff", 0)])
    return defs, rect(0, 0, W, H, "url(#bg)") + ellipse(176, 110, 150, 100, "url(#spot)") + rect(0, 168, W, 32, table)


def creatine():
    # A giant tub of white powder with a flexing cartoon bicep on its label, a scoop sticking out of it
    defs, body = bg_item("#4a5a6a", "#1a2028")
    body += rect(110, 52, 132, 128, "#f4f4f4", 'rx="10" stroke="#9aa0a8" stroke-width="3"') + rect(104, 40, 144, 20, "#2a2a30", 'rx="6"')
    body += rect(118, 76, 116, 76, "#d0342c", 'rx="4"')
    body += path("M124 140 L124 122 C 136 104 164 100 180 116 L190 122 L192 98 C 192 86 212 86 212 98 L210 132 C 210 140 204 142 196 140 L124 140 Z",
                 SKIN[0], 'stroke="#7a1a1a" stroke-width="2"')
    body += circle(202, 92, 11, SKIN[0], 'stroke="#7a1a1a" stroke-width="2"') + stroke("M146 118 C 154 108 166 108 172 118", "#7a1a1a", 1.5)
    body += lettering("CREATINE", 176, 142, 6, "#ffffff", width=1.3)
    body += ellipse(176, 40, 66, 6, "#ffffff") + path("M130 40 C 150 22 200 22 222 40 Z", "#ffffff")
    body += stroke("M204 34 L250 6", "#3a8ad0", 5) + ellipse(200, 34, 12, 8, "#3a8ad0")
    for x in (90, 260, 78, 272):
        body += circle(x, 168 - (x % 7), 2, "#ffffff", 'opacity="0.7"')
    return svg(defs, body)


def eject_button():
    # A big red button under a flip-up cover on a cockpit panel, a pilot's seat already rocketing out of the frame
    defs = lin("bg", ["#5aa0e0", "#c8e4f8"])
    body = rect(0, 0, W, H, "url(#bg)") + cloud(60, 40, 1.0) + cloud(300, 70, 0.8)
    body += rect(0, 110, W, 90, "#3a3f46") + rect(0, 110, W, 8, "#5a5f66")
    for i, x in enumerate((40, 92, 260, 312)):
        body += circle(x, 150, 20, "#1a1a1e", 'stroke="#8a8a92" stroke-width="3"') + stroke(f"M{x} 150 l {10 - i * 5} -12", "#ffd23a", 2)
    body += rect(140, 140, 72, 44, "#1a1a1e", 'rx="4"')
    body += circle(176, 162, 18, "#d0342c", 'stroke="#7a1a1a" stroke-width="3"') + ellipse(170, 156, 6, 4, "#ff8a8a")
    cover = rect(150, 96, 52, 40, "#ffd23a", 'stroke="#8a7a1a" stroke-width="2" opacity="0.85"')
    for i in range(4):
        cover += poly([(150 + i * 14, 96), (158 + i * 14, 96), (150 + i * 14 + 2, 136), (150 + i * 14 - 6, 136)], "#1a1a1a", 'opacity="0.7"')
    body += cover
    body += lettering("EJECT", 176, 190, 6, "#ffd23a", width=1.3)
    # The seat, rocketing out the top
    body += rect(250, 4, 40, 44, "#4a4a52", 'rx="6"') + rect(246, 40, 48, 14, "#3a3a42", 'rx="4"')
    body += path("M258 56 C 256 74 268 92 270 96 C 274 88 286 74 282 56 Z", "#ff7a1a") + path("M264 56 C 264 68 270 80 270 84 C 272 78 278 66 276 56 Z", "#ffd84a")
    body += path("M270 96 C 262 106 240 104 234 110", "none", 'stroke="#e8e8e8" stroke-width="8" stroke-linecap="round" opacity="0.6"')
    return svg(defs, body)


def flamethrower():
    # A flamethrower with a novelty-store price tag, a jet of fire roaring out of it across a garden barbecue
    defs = lin("bg", ["#8ac8f0", "#e0f4ff"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 150, W, 50, "#5a9a3a")
    for x in range(4, W, 12):
        body += stroke(f"M{x} 150 l -3 -8 M{x + 4} 150 l 2 -7", "#3a7a2a", 1.5)
    body += rect(14, 96, 30, 60, "#c0392b", 'rx="12" stroke="#7a1a1a" stroke-width="2"') + rect(46, 102, 22, 52, "#c0392b", 'rx="10" stroke="#7a1a1a" stroke-width="2"')
    body += stroke("M60 120 C 70 130 80 120 96 118", "#2a2a2a", 4)
    body += rect(90, 108, 70, 14, "#3a3a42", 'rx="3"') + rect(104, 120, 10, 18, "#2a2a30") + rect(156, 110, 14, 10, "#8a8a92")
    body += path("M170 106 C 200 94 236 90 270 100 C 300 96 320 104 344 96 C 336 112 312 112 300 126 C 272 120 240 132 200 124 C 186 124 176 122 170 124 Z", "#ff7a1a")
    body += path("M170 110 C 200 104 236 104 270 110 C 250 118 220 120 170 120 Z", "#ffd84a")
    body += ellipse(270, 150, 36, 18, "#1a1a1e") + rect(234, 136, 72, 4, "#8a8a92") + stroke("M250 166 L240 196 M290 166 L300 196 M270 168 L270 196", "#1a1a1e", 4)
    for x in (252, 268, 284):
        body += rect(x - 6, 130, 12, 6, "#7a2a1a", 'rx="3"')
    body += stroke("M124 108 L132 76", "#555", 1.2) + group(rect(-16, -10, 32, 20, "#ffe45c", 'stroke="#8a7a1a" stroke-width="1.5"') + words("9.99", 0, -4, 7, "#c0392b", width=1.3), "translate(134 66) rotate(-10)")
    return svg(defs, body)


def nicotine():
    # A crumpled pack of cigarettes on a café table, one lit in the ashtray, a ribbon of smoke curling into a question mark
    defs, body = bg_item("#5a4a3a", "#1a1410", "#8a6a4a")
    body += ellipse(176, 172, 150, 20, "#8a6a4a") + ellipse(176, 168, 150, 18, "#a0805a")
    body += group(rect(-24, -32, 48, 64, "#f4f4f4", 'stroke="#8a8a8a" stroke-width="1.5"') + rect(-24, -32, 48, 22, "#d0342c")
                  + stroke("M-18 4 L-6 12 L4 2 L16 10", "#c8c8c8", 1.5) + rect(-14, -40, 6, 10, "#f4ecd8") + rect(-14, -40, 6, 3, "#c8883a"), "translate(110 140) rotate(-18)")
    body += ellipse(236, 164, 34, 10, "#5a6a7a") + ellipse(236, 160, 28, 7, "#2a3038")
    body += rect(216, 154, 48, 5, "#f4ecd8", 'transform="rotate(-8 240 156)"') + rect(256, 148, 8, 5, "#c8883a", 'transform="rotate(-8 240 156)"')
    body += circle(266, 150, 3, "#ff5a1a")
    body += path("M268 146 C 280 130 256 120 268 104 C 278 92 262 80 270 66 C 282 44 314 46 318 64 C 322 82 296 84 294 100 L294 108",
                 "none", 'stroke="#d8d8d8" stroke-width="5" stroke-linecap="round" opacity="0.8"')
    body += circle(294, 122, 4, "#d8d8d8", 'opacity="0.8"')
    return svg(defs, body)


def soylent():
    # A beige bottle of meal-replacement drink on an empty dining table, a lonely fork beside it
    defs, body = bg_item("#6a6a62", "#2a2a26", "#4a3a2a")
    body += rect(0, 130, W, 44, "#f4f0e8") + rect(0, 170, W, 6, "#d8d4c8")
    for x in (40, 312):
        body += ellipse(x, 146, 26, 8, "#ffffff", 'stroke="#c8c8c0" stroke-width="1.5"')
    body += rect(150, 46, 52, 104, "#e8dcc0", 'rx="10" stroke="#a89a7a" stroke-width="2"') + rect(162, 30, 28, 20, "#ffffff", 'rx="3" stroke="#a89a7a" stroke-width="2"')
    body += rect(154, 82, 44, 30, "#ffffff") + lettering("MEAL", 176, 88, 6, "#6a6a62", width=1.2) + words("2.0", 176, 99, 6, "#6a6a62", width=1.2)
    body += stroke("M236 160 L236 116", "#b8b8c0", 4) + stroke("M230 116 L230 102 M236 116 L236 102 M242 116 L242 102", "#b8b8c0", 2) + rect(229, 112, 14, 6, "#b8b8c0", 'rx="2"')
    body += rect(10, 60, 40, 70, "#3a2a1a", 'rx="4"') + rect(302, 60, 40, 70, "#3a2a1a", 'rx="4"')
    return svg(defs, body)


def stealth_suit():
    # An empty mannequin in a shop window wearing a suit you can only see by its outline, the price tag floating in mid-air
    defs, body = bg_item("#c8d0d8", "#8a949c", "#5a5a62")
    body += rect(0, 0, W, 14, "#2a2a30") + rect(0, 0, 10, H, "#2a2a30") + rect(342, 0, 10, H, "#2a2a30")
    body += lettering("SALE", 50, 26, 12, "#d0342c", width=2.2)
    body += rect(150, 160, 52, 8, "#3a3a42") + stroke("M176 160 L176 134", "#3a3a42", 4)
    outline = 'stroke="#ffffff" stroke-width="2" stroke-dasharray="5 4" opacity="0.9"'
    body += path("M130 136 C 130 70 222 70 222 136 Z", "none", outline) + circle(176, 46, 18, "none", outline)
    body += stroke("M118 136 L112 92 L136 76 M234 136 L240 92 L216 76", "#ffffff", 2, 'stroke-dasharray="5 4" opacity="0.9"')
    body += stroke("M166 76 L176 96 L186 76", "#ffffff", 2, 'stroke-dasharray="3 3" opacity="0.9"')
    body += stroke("M260 70 Q 252 52 240 44", "#555", 1)
    body += group(rect(-18, -10, 36, 20, "#fff7e0", 'stroke="#8a7a5a" stroke-width="1.5"') + words("999", 0, -4, 7, "#1a1a1a", width=1.3), "translate(266 80) rotate(14)")
    for x, y in ((60, 120), (300, 140), (280, 30)):
        body += poly(star_points(x, y, 6, 2, 4), "#ffffff", 'opacity="0.8"')
    return svg(defs, body)


def stimulant_drug():
    # A pill organizer with every compartment full of the same blue pill, a student's desk behind it buried in energy drink
    # cans at 4 a.m.
    defs, body = bg_item("#141a30", "#06080f", "#4a3a2a")
    body += monitor(110, 20, 132, 84, "#bfe0ff") + paper(126, 34, 100, 50, 5, "#ffffff")
    body += circle(310, 36, 20, "#f4ecd8", 'stroke="#555" stroke-width="2"') + stroke("M310 36 L302 26 M310 36 L310 22", "#222", 2.5) + words("4AM", 310, 62, 6, "#ff5a5a", width=1.2)
    for i, (x, y) in enumerate(((22, 132), (40, 132), (58, 132), (31, 112), (49, 112), (40, 92), (280, 132), (298, 132), (316, 132), (289, 112))):
        body += rect(x, y, 16, 22, ("#3aa86a", "#2a6ad0", "#d0342c")[i % 3], 'rx="2" stroke="#1a1a1a" stroke-width="1"') + rect(x + 2, y + 6, 12, 5, "#f4f4f4", 'opacity="0.6"')
    body += rect(90, 150, 172, 34, "#e8e8f0", 'rx="5" stroke="#8a8a9a" stroke-width="2"')
    for i, d in enumerate("MTWTFSS"):
        x = 96 + i * 23.5
        body += rect(x, 156, 20, 22, "#f8f8ff", 'rx="3" stroke="#a8a8b8" stroke-width="1"') + lettering(d, x + 10, 140, 6, "#8a8aa0", width=1.2)
        body += ellipse(x + 10, 168, 6, 4, "#3a7af0") + ellipse(x + 8, 166, 2, 1.2, "#bfd8ff")
    return svg(defs, body)


def superglue():
    # A tube of superglue squeezed over a cracked vase, two fingers already stuck to it
    defs, body = bg_item("#e8dcc8", "#a89a80", "#7a5a3a")
    body += path("M146 168 C 110 150 112 100 140 84 L140 64 C 130 60 130 52 140 50 L212 50 C 222 52 222 60 212 64 L212 84 C 240 100 242 150 206 168 Z",
                 "#3a6ab0", 'stroke="#1a3a7a" stroke-width="2.5"')
    body += path("M130 118 C 160 108 192 108 222 118", "none", 'stroke="#f2c94c" stroke-width="5"')
    body += stroke("M176 54 L168 76 L182 92 L170 112 L186 132 L176 156", "#0a1a3a", 2.5)
    body += group(rect(-14, -50, 28, 70, "#f4f4f4", 'rx="4" stroke="#8a8a92" stroke-width="2"') + rect(-14, -50, 28, 16, "#d0342c")
                  + poly([(-6, 20), (6, 20), (2, 34), (-2, 34)], "#d8d8d8") + lettering("GLUE", 0, -22, 6, "#d0342c", width=1.3), "translate(240 40) rotate(150)")
    body += path("M223 64 C 220 70 218 76 214 82", "none", 'stroke="#e8f4ff" stroke-width="3"')
    # The hand on the left, two fingers stuck to the vase
    body += path("M0 120 L60 104 L118 106 C 128 106 128 116 118 118 L74 120 L120 130 C 130 132 128 142 118 142 L62 140 L0 156 Z", SKIN[0], 'stroke="#b08a6a" stroke-width="2"')
    body += stroke("M114 108 l 6 0 M114 132 l 6 0", "#e8f4ff", 3)
    for x, y in ((92, 96), (100, 152)):
        body += words("!", x, y, 10, "#c0392b", width=2)
    return svg(defs, body)


# ---- Shadow Ops

def flash(cx, cy, s=1.0):
    """A camera flash going off."""
    return poly(star_points(cx, cy, 12 * s, 4 * s, 8), "#ffffff", 'opacity="0.9"') + circle(cx, cy, 5 * s, "#fff8c0")


def camera(cx, cy, s=1.0):
    return rect(cx - 10 * s, cy - 7 * s, 20 * s, 14 * s, "#1a1a1e", 'rx="2"') + circle(cx, cy, 4.5 * s, "#5a6a7a", 'stroke="#aaa" stroke-width="1"')


def arsonist():
    # A man in a suit holding a briefcase and a lit match in front of a failing shop, a fire insurance policy sticking out of
    # his breast pocket
    defs, body = bg_shadow()
    body += rect(40, 40, 272, 136, "#5a4a3a") + rect(40, 34, 272, 22, "#3a2a1a") + lettering("DONS DINER", 176, 39, 11, "#c8a060", width=2)
    for x in (60, 236):
        body += rect(x, 70, 56, 70, "#1a1a22") + stroke(f"M{x} 70 L{x + 56} 140 M{x + 56} 70 L{x} 140", "#8a6a3a", 6)
    body += rect(250, 90, 40, 18, "#ffffff", 'transform="rotate(-8 270 99)"') + lettering("CLOSING", 270, 95, 4.5, "#c0392b", width=1)
    body += agent(176, 200, 1.35, SKIN[0], face="smirk", glasses=None, hair="#3a2a1a", tie="#8a1a1a")
    body += rect(186, 150, 18, 24, "#f4ecd8", 'transform="rotate(8 195 162)"') + lettering("FIRE", 196, 154, 4, "#c0392b", width=1)
    body += stroke("M146 166 L112 186", "#22262e", 10) + rect(80, 170, 46, 30, "#4a2a1a", 'rx="3"') + rect(96, 164, 14, 8, "none", 'stroke="#4a2a1a" stroke-width="3"')
    body += stroke("M210 166 L244 130", "#22262e", 10) + circle(246, 126, 6, SKIN[0])
    body += stroke("M248 124 L258 100", "#e8d4a0", 2.5) + circle(258, 98, 3, "#c0392b")
    body += path("M258 96 C 252 88 256 80 258 72 C 262 80 266 88 258 96 Z", "#ff9a1a") + path("M258 94 C 256 90 257 86 258 82 C 260 86 261 90 258 94 Z", "#ffe45c")
    body += circle(258, 86, 18, "#ffb84a", 'opacity="0.25"')
    return svg(defs, body)


def compromised_agent():
    # A spy in a trench coat mobbed by paparazzi in a train station, while behind her three other agents slip away unnoticed
    defs, body = room("#6a5a4a", "#3a3028", "#5a5048", 168)
    for x in (0, 120, 240):
        body += path(f"M{x + 8} 168 L{x + 8} 60 C {x + 8} 10 {x + 112} 10 {x + 112} 60 L{x + 112} 168", "none", 'stroke="#8a7a68" stroke-width="6"')
    body += circle(176, 30, 16, "#f4ecd8", 'stroke="#3a3028" stroke-width="3"') + stroke("M176 30 L176 20 M176 30 L184 34", "#222", 2)
    for i, x in enumerate((262, 290, 318)):  # The others slipping away
        body += group(agent(x, 168, 0.45, SKIN[0], hat="fedora"), "translate(0 0)", 0.75) + stroke(f"M{x - 18} {140 + i * 2} l -14 0 M{x - 18} {148 + i * 2} l -10 0", "#c8b8a0", 2)
    body += person(150, 200, 1.3, SKIN[0], "#c8a868", face="wavy", hat="bob", glasses="shades", brows="sad")
    body += stroke("M150 158 L150 200 M128 162 L140 200 M172 162 L160 200", "#a8884a", 2.5)
    for x, y in ((44, 150), (88, 120), (220, 140), (66, 186), (232, 182)):
        body += person(x, y + 46, 0.6, SKIN[(x // 20) % 4], "#4a4a52", face="shout", hat="cap" if x % 3 else None, hair="#2a2a2a")
        body += camera(x + (14 if x < 150 else -14), y + 2, 1.0) + flash(x + (14 if x < 150 else -14), y - 10, 0.9)
    return svg(defs, body)


def counter_surveillance():
    # A spy in a dark room watching a wall of monitors, every screen showing another spy watching him back, one of them waving
    defs, body = room("#14161c", "#08090c", "#0e0f12", 176)
    for row in range(2):
        for col in range(4):
            x, y = 22 + col * 80, 14 + row * 74
            body += rect(x, y, 74, 64, "#2a2c32", 'rx="3"') + rect(x + 5, y + 5, 64, 54, "#1a3a4a")
            body += agent(x + 37, y + 59, 0.5, SKIN[0], hat="fedora")
            if (row, col) == (0, 2):
                body += stroke(f"M{x + 52} {y + 46} L{x + 60} {y + 26}", "#22262e", 4) + circle(x + 61, y + 24, 3.5, SKIN[0])
                body += stroke(f"M{x + 54} {y + 18} q 4 -2 4 -6 M{x + 66} {y + 18} q -2 -4 0 -8", "#ffffff", 1.2)
    body += rect(0, 160, W, 40, "#1a1a1e")
    body += path("M120 200 C 120 160 232 160 232 200 Z", "#05060a") + circle(176, 150, 28, "#0a0b10") + ellipse(176, 128, 44, 8, "#0a0b10")
    body += path("M152 130 C 152 104 200 104 200 130 Z", "#0a0b10")
    return svg(defs, body)


def pigeon(cx, cy, s=1.0):
    """A grey pigeon in flight, facing right."""
    out = ellipse(cx, cy, 22 * s, 12 * s, "#8a94a0", 'stroke="#4a545e" stroke-width="1.5"')
    out += path(f"M{cx - 4 * s:.1f} {cy - 6 * s:.1f} C {cx - 20 * s:.1f} {cy - 40 * s:.1f} {cx + 6 * s:.1f} {cy - 44 * s:.1f} {cx + 8 * s:.1f} {cy - 6 * s:.1f} Z", "#a8b0bc", 'stroke="#4a545e" stroke-width="1.5"')
    out += poly([(cx - 20 * s, cy), (cx - 36 * s, cy - 8 * s), (cx - 34 * s, cy + 8 * s)], "#6a747e")
    out += circle(cx + 22 * s, cy - 6 * s, 9 * s, "#6a8a7a") + poly([(cx + 30 * s, cy - 6 * s), (cx + 38 * s, cy - 3 * s), (cx + 30 * s, cy - 1 * s)], "#e8a83a")
    return out


def drug_pigeon():
    # A pigeon in tiny aviator goggles flying over a prison wall, a backpack bulging with white packets, a guard in the
    # watchtower looking the other way
    defs, body = bg_shadow()
    body += circle(60, 40, 18, "#f4f0d0", 'opacity="0.8"')
    body += rect(0, 140, 260, 60, "#5a5a62")
    for row in range(4):
        for col in range(14):
            body += rect(col * 20 + (row % 2) * 10, 142 + row * 14, 18, 12, "#6a6a72", 'stroke="#4a4a52" stroke-width="1"')
    for x in range(0, 260, 16):
        body += circle(x, 134, 5, "none", 'stroke="#aab" stroke-width="1.5"')
    body += rect(270, 70, 60, 130, "#4a4a52") + poly([(262, 70), (338, 70), (300, 46)], "#3a3a42") + rect(266, 70, 68, 30, "#2a2a32")
    body += person(300, 100, 0.6, SKIN[1], "#2a3a5a", face="flat", hat="peaked")
    body += pigeon(150, 80, 1.3)
    body += rect(168, 64, 14, 8, "#3a2a1a", 'rx="3"') + circle(186, 68, 5, "#8ad0f0", 'stroke="#3a2a1a" stroke-width="2"')
    body += rect(134, 88, 36, 22, "#6a5a3a", 'rx="5" stroke="#3a2a1a" stroke-width="1.5"')
    for x, y in ((140, 92), (152, 94), (146, 102), (160, 100)):
        body += rect(x, y, 10, 7, "#ffffff", 'rx="2" stroke="#aaa" stroke-width="0.8"')
    return svg(defs, body)


def embassy():
    # A grand embassy with a flag over the door, a man in a trench coat climbing in through a back window while the ambassador
    # smiles for photographers on the front steps
    defs = lin("bg", ["#8ab0d8", "#d8e8f4"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += poly([(40, 60), (240, 60), (140, 24)], "#e8e0d0", 'stroke="#a89a80" stroke-width="2"') + rect(40, 60, 200, 100, "#e8e0d0", 'stroke="#a89a80" stroke-width="2"')
    for x in range(54, 240, 34):
        body += rect(x, 66, 14, 86, "#f4f0e8", 'stroke="#c8c0b0" stroke-width="1"')
    body += rect(118, 110, 44, 50, "#5a3a2a") + stroke("M140 24 L140 0", "#6a6a6a", 2) + rect(140, 2, 30, 18, "#2a5aa0") + rect(140, 8, 30, 6, "#ffffff")
    body += rect(250, 60, 90, 100, "#d8d0c0", 'stroke="#a89a80" stroke-width="2"') + rect(282, 84, 36, 34, "#2a2a32")
    body += stroke("M300 118 L292 150 M306 118 L312 148", "#c8a868", 7) + rect(286, 146, 10, 6, "#1a1a1a") + rect(308, 144, 10, 6, "#1a1a1a")
    body += path("M284 104 L316 104 L318 120 L282 120 Z", "#c8a868")
    for i in range(4):
        body += rect(30 + i * 8, 160 + i * 8, 220 - i * 16, 8, "#c8c0b0")
    body += person(140, 172, 0.8, SKIN[0], "#1a1a2a", face="grin", hair=OLD_HAIR, tie="#c0392b")
    for x in (30, 70, 214, 254):
        body += person(x, 210, 0.7, SKIN[(x // 30) % 4], "#4a4a52", face="flat", hair="#2a2a2a") + camera(x, 156, 1.0) + flash(x, 144, 0.8)
    return svg(defs, body)


def exploding_pen():
    # A fountain pen on a desk with its cap off and a lit fuse curling out of the nib, a spy's hand sliding it across to a
    # villain who is already reaching for it
    defs, body = room("#2a2018", "#140e0a", "#6a4a2a", 90)
    body += rect(0, 90, W, 110, "#7a5a3a") + rect(0, 90, W, 4, "#9a7a5a")
    body += paper(40, 120, 70, 56, 6)
    body += rect(130, 132, 92, 12, "#1a1a2a", 'rx="6"') + rect(130, 132, 16, 12, "#d4af37", 'rx="3"') + poly([(222, 132), (240, 138), (222, 144)], "#d4af37")
    body += rect(150, 150, 50, 9, "#1a1a2a", 'rx="4"') + rect(150, 152, 50, 2, "#d4af37")
    body += path("M240 138 C 252 130 246 116 258 110 C 268 104 266 92 276 88", "none", 'stroke="#c8b898" stroke-width="2.5"')
    body += poly(star_points(278, 86, 9, 3, 6), "#ffd23a") + circle(278, 86, 3, "#ffffff")
    # The spy's hand, cuffed, pushing the pen; the villain's ringed hand reaching
    body += path("M0 120 L70 128 C 84 130 96 132 116 134 C 126 136 124 146 114 146 L70 148 L0 152 Z", SKIN[0], 'stroke="#b08a6a" stroke-width="1.5"')
    body += rect(0, 118, 24, 36, "#f4f4f4") + rect(0, 116, 6, 40, "#1a1a1a")
    body += path("M352 150 L300 156 C 284 158 268 162 256 164 C 246 166 246 176 258 176 L300 174 L352 178 Z", "#c8b0a0", 'stroke="#7a6a5a" stroke-width="1.5"')
    body += rect(276, 160, 8, 8, "#d4af37", 'rx="2"') + circle(280, 164, 2.5, "#c0392b")
    body += rect(320, 148, 32, 32, "#3a1a3a")
    return svg(defs, body)


def nonchalant_agent():
    # An agent in a tuxedo leaning on a casino bar sipping a martini without looking, his free hand flicking a pistol at a
    # henchman who has only half drawn
    defs = lin("bg", ["#5a1420", "#1a0408"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in range(10, W, 40):
        body += circle(x, 22, 4, "#ffd23a", 'opacity="0.7"')
    body += rect(0, 150, 220, 50, "#3a1a10") + rect(0, 146, 220, 8, "#6a3a1a")
    body += agent(110, 196, 1.35, SKIN[0], face="smirk", glasses=None, hair="#2a1a10", suit="#121214", tie="#121214")
    body += poly([(104, 152), (110, 148), (116, 152), (110, 156)], "#121214")
    body += stroke("M80 160 L62 120", "#121214", 10)
    body += path("M46 96 L76 96 L61 112 Z", "#bfe8ff", 'opacity="0.8" stroke="#ffffff" stroke-width="1.5"') + stroke("M61 112 L61 122 M54 122 L68 122", "#ffffff", 2) + circle(64, 102, 3, "#5aa03a")
    body += stroke("M140 160 L180 140", "#121214", 10) + rect(178, 134, 12, 8, "#f4f4f4") + pistol(186, 138, 0.9, "#1a1a1e", silencer=True)
    body += person(300, 200, 1.2, SKIN[2], "#3a3a42", face="shout", hair="#1a1a1a", brows="sad")
    body += stroke("M286 160 L272 176", "#3a3a42", 9) + pistol(250, 176, 0.6, "#2a2a2e")
    body += sweat(320, 106, 1.0)
    return svg(defs, body)


def q_branch():
    # An old quartermaster in a lab coat demonstrating an exploding briefcase to a bored agent, a test dummy in the background
    # missing its head
    defs, body = room("#a8b0b8", "#7a8288", "#5a5f66", 160)
    # The headless test dummy on its stand, a few wires where the head was
    body += stroke("M196 110 L196 160", "#5a5a5a", 4) + rect(180, 156, 32, 6, "#5a5a5a")
    body += path("M162 112 C 162 64 230 64 230 112 Z", "#d8c8a8", 'stroke="#a8987a" stroke-width="2"') + rect(188, 58, 16, 14, "#d8c8a8", 'stroke="#a8987a" stroke-width="2"')
    body += stroke("M190 58 l -6 -10 M196 56 l 0 -12 M202 58 l 6 -10", "#3a3a3a", 2)
    body += person(76, 200, 1.3, SKIN[0], "#f4f4f4", face="grin", hair=OLD_HAIR, glasses="round", tie="#2a5a3a")
    body += stroke("M104 160 L140 150", "#f4f4f4", 9)
    body += rect(130, 132, 52, 34, "#3a2a1a", 'rx="3"') + rect(150, 126, 14, 8, "none", 'stroke="#3a2a1a" stroke-width="3"')
    body += poly(star_points(170, 120, 34, 14, 10), "#ff9a1a") + poly(star_points(170, 120, 22, 9, 10), "#ffe45c")
    body += lettering("BOOM", 170, 114, 9, "#c0392b", width=2)
    body += agent(286, 200, 1.3, SKIN[0], face="flat", glasses=None, hair="#3a2a1a")
    body += stroke("M276 120 L294 120", "#222", 2) + zzz(306, 70, 0.6, "#ffffff")
    return svg(defs, body)


def secret_anthrax_laboratory():
    # An abandoned concrete lab on a cracked dry seabed, rusty ships stranded in the sand, a lone scientist in a gas mask
    # locking the door and pocketing the key
    defs = lin("bg", ["#e8c890", "#f4e4b8"])
    body = rect(0, 0, W, H, "url(#bg)") + circle(300, 30, 16, "#fff8d0")
    body += rect(0, 110, W, 90, "#d8b878")
    for d in ("M10 130 l 30 10 l 20 -6 l 26 14", "M120 180 l 24 -12 l 30 6", "M220 150 l 30 12 l 26 -8 l 30 10", "M30 176 l 20 -10 l 34 4"):
        body += stroke(d, "#a8884a", 2)
    for x, y, a in ((50, 108, -10), (280, 116, 14)):
        body += group(path("M-40 0 L40 0 L30 18 L-30 18 Z", "#8a4a2a", 'stroke="#5a2a1a" stroke-width="2"') + rect(-12, -14, 24, 14, "#7a4a2a") + rect(-4, -24, 6, 10, "#5a2a1a"), f"translate({x} {y}) rotate({a})")
    body += rect(120, 60, 120, 80, "#9a9a92", 'stroke="#6a6a62" stroke-width="2"') + rect(120, 54, 120, 10, "#7a7a72")
    body += rect(196, 90, 30, 50, "#5a5a52") + rect(132, 76, 26, 16, "#2a2a2a") + poly([(140, 110), (156, 110), (148, 96)], "#ffd23a") + circle(148, 106, 2, "#1a1a1a")
    body += person(176, 196, 1.0, SKIN[0], "#e8e8e0", face="flat", hair="#3a3a3a")
    body += ellipse(176, 138, 16, 14, "#3a4a3a") + circle(170, 134, 5, "#bfe0f0", 'stroke="#1a1a1a" stroke-width="2"') + circle(182, 134, 5, "#bfe0f0", 'stroke="#1a1a1a" stroke-width="2"')
    body += circle(176, 152, 7, "#5a5a52", 'stroke="#1a1a1a" stroke-width="2"')
    body += stroke("M196 168 L206 140", "#e8e8e0", 7) + circle(208, 134, 4, "#d4af37") + rect(210, 132, 10, 3, "#d4af37")
    return svg(defs, body)


def tracking_beacon():
    # A tiny blinking red device stuck to the back of a henchman's collar, a laptop in the background showing his dot moving
    # through the city
    defs, body = bg_shadow()
    body += monitor(198, 20, 140, 96, "#1a2a3a")
    for i in range(6):
        body += stroke(f"M{206 + i * 22} 28 L{206 + i * 22} 108", "#3a5a7a", 2) + stroke(f"M206 {32 + i * 14} L330 {32 + i * 14}", "#3a5a7a", 2)
    body += path("M222 100 L222 72 L266 72 L266 44 L310 44", "none", 'stroke="#ff5a5a" stroke-width="2" stroke-dasharray="4 3"') + circle(310, 44, 5, "#ff3a3a")
    body += circle(310, 44, 10, "none", 'stroke="#ff3a3a" stroke-width="1.5" opacity="0.6"')
    # The henchman, from behind: a broad back, a thick neck, a crew cut
    body += path("M20 200 C 20 130 200 130 200 200 Z", "#2a2a30") + rect(90, 100, 40, 40, SKIN[1], 'rx="10"')
    body += circle(110, 82, 34, SKIN[1]) + path("M76 78 C 78 42 142 42 144 78 C 130 66 90 66 76 78 Z", "#2a1a10")
    body += ellipse(76, 86, 6, 10, SKIN[2]) + ellipse(144, 86, 6, 10, SKIN[2])
    body += path("M70 140 L110 152 L150 140 L144 132 L110 140 L76 132 Z", "#f4f4f4")
    body += rect(104, 134, 12, 8, "#2a2a30", 'rx="2"') + circle(110, 138, 2.5, "#ff2a2a")
    for r in (8, 14, 20):
        body += circle(110, 138, r, "none", f'stroke="#ff3a3a" stroke-width="1.5" opacity="{0.9 - r / 30:.2f}"')
    return svg(defs, body)


# ---- Statecraft

def border_crossing():
    # A border checkpoint at night: a striped barrier, a bored officer stamping passports, a queue of strange creatures under
    # the floodlights, a 'Welcome' sign half fallen off
    defs = lin("bg", ["#0a1020", "#2a2a40"]) + lin("beam", [("#fff7c8", 0.35), ("#fff7c8", 0)])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 160, W, 40, "#3a3a3a")
    for x in (60, 290):
        body += stroke(f"M{x} 160 L{x} 20", "#5a5a62", 4) + rect(x - 10, 14, 20, 8, "#d8d8d0") + poly([(x - 10, 22), (x + 10, 22), (x + 50, 160), (x - 50, 160)], "url(#beam)")
    body += rect(20, 90, 70, 70, "#c8c0a8", 'stroke="#6a6a62" stroke-width="2"') + rect(28, 98, 54, 30, "#ffe9a8")
    body += person(55, 130, 0.6, SKIN[0], "#3a4a3a", face="closed", hat="peaked") + stamp("OK", 80, 112, 6, "#c0392b", angle=10)
    body += rect(90, 120, 6, 40, "#5a5a5a")
    for i in range(10):
        body += rect(96 + i * 14, 120, 14, 8, "#c0392b" if i % 2 else "#f4f4f4")
    body += octopus(160, 150, 0.5, "#c06ad0", "#7a3a8a")
    body += cow(222, 146, 1.0)
    body += path("M268 160 C 268 120 300 120 300 160 Z", "#5ad06a") + circle(284, 116, 12, "#5ad06a") + ellipse(279, 114, 3, 5, "#111") + ellipse(289, 114, 3, 5, "#111")
    body += stroke("M278 104 l -4 -10 M290 104 l 4 -10", "#5ad06a", 2) + circle(274, 94, 2.5, "#5ad06a") + circle(294, 94, 2.5, "#5ad06a")
    body += group(rect(-30, -10, 60, 20, "#2a7a3a", 'stroke="#f4f4f4" stroke-width="2"') + lettering("WELCOME", 0, -4, 7, "#f4f4f4", width=1.3), "translate(176 40) rotate(16)")
    body += stroke("M146 30 L150 6 M206 52 L206 70", "#5a5a62", 2)
    return svg(defs, body)


def exoskeleton():
    # A soldier in a powered military exoskeleton standing guard at a checkpoint, an extension cord trailing from his back to a
    # wall socket in the guard booth
    defs, body = bg_state("#3a4a2a")
    body += rect(250, 70, 90, 102, "#c8c0a8", 'stroke="#6a6a62" stroke-width="2"') + rect(262, 82, 66, 34, "#bfe0f0")
    body += rect(300, 136, 18, 22, "#f4f4f4", 'rx="2" stroke="#555" stroke-width="1"') + rect(305, 142, 2, 6, "#222") + rect(311, 142, 2, 6, "#222")
    body += path("M120 150 C 160 190 220 140 250 170 C 270 186 290 170 306 152", "none", 'stroke="#f2a01a" stroke-width="4"')
    body += person(130, 200, 1.4, SKIN[1], "#5a6a3a", face="flat", hat="helmet", glasses="shades")
    frame = 'stroke="#2a2e34" stroke-width="2"'
    for f in (-1, 1):
        body += rect(130 + f * 46 - 12, 112, 24, 40, "#7a828a", f'rx="6" {frame}') + rect(130 + f * 52 - 7, 150, 14, 50, "#7a828a", f'rx="4" {frame}')
        body += circle(130 + f * 46, 120, 6, "#2a2e34") + stroke(f"M{130 + f * 46} 130 L{130 + f * 52} 168", "#c8c8c8", 4)
    body += rect(110, 146, 40, 30, "#5a626a", f'rx="4" {frame}') + circle(130, 161, 7, "#3aff7a")
    return svg(defs, body)


def peace_force():
    # A line of blue-helmeted peacekeepers behind riot shields, the front one shoving his shield into a protester's face, a
    # banner reading 'PEACE' behind them
    defs, body = bg_state("#2a6ab0")
    body += rect(30, 14, 200, 34, "#f4f4f4", 'stroke="#2a6ab0" stroke-width="3"') + lettering("PEACE", 130, 21, 20, "#2a6ab0", width=3)
    body += stroke("M30 14 L30 120 M230 14 L230 120", "#6a4a2a", 3)
    for i, x in enumerate((40, 100, 160, 226)):
        s = 1.0 + i * 0.12
        body += person(x, 200, s, SKIN[i % 4], "#3a4a5a", face="flat")
        hy = 200 - 64 * s
        body += path(f"M{x - 22 * s:.1f} {hy:.1f} C {x - 22 * s:.1f} {hy - 30 * s:.1f} {x + 22 * s:.1f} {hy - 30 * s:.1f} {x + 22 * s:.1f} {hy:.1f} Z", "#4a9ae0") + rect(x - 24 * s, hy - 2 * s, 48 * s, 4 * s, "#2a6ab0")
        body += rect(x - 6 + i * 4, 140 - i * 4, 44 * s, 70, "#d8eef8", 'opacity="0.55" rx="4" stroke="#8ab0c8" stroke-width="2"')
    body += person(306, 200, 1.2, SKIN[0], "#c06a2a", face="shout", hair="#6a3a1a", brows="angry")
    body += stroke("M268 120 l 8 -6 M268 132 l 10 0", "#ffffff", 2.5)
    return svg(defs, body)


def security_guard():
    # An airport security guard snapping on a blue latex glove with a grin, a traveler in socks with arms raised in the body
    # scanner, a bin of confiscated water bottles overflowing behind
    defs, body = room("#e8ecf0", "#c8d0d8", "#a8b0b8", 170)
    body += path("M196 170 L196 40 C 196 20 296 20 296 40 L296 170", "none", 'stroke="#8a949c" stroke-width="14"')
    body += person(246, 170, 0.95, SKIN[2], "#c06a8a", face="frown", hair="#1a1a1a", brows="sad")
    body += stroke("M222 140 L210 70 M270 140 L282 70", "#c06a8a", 8) + circle(209, 64, 6, SKIN[2]) + circle(283, 64, 6, SKIN[2])
    body += rect(226, 176, 14, 10, "#f4f4f4", 'rx="4"') + rect(252, 176, 14, 10, "#f4f4f4", 'rx="4"')
    body += person(100, 200, 1.3, SKIN[0], "#2a3a5a", face="grin", hat="peaked")
    body += badge(84, 160, 0.7, "#d4af37")
    body += stroke("M124 160 L150 112", "#2a3a5a", 9) + path("M144 110 L156 100 L170 90 L166 112 L150 120 Z", "#4aa0f0")
    body += stroke("M76 160 L116 130", "#2a3a5a", 9) + circle(118, 126, 6, SKIN[0])
    body += stroke("M150 112 l 10 -16", "#4aa0f0", 2) + lettering("SNAP", 168, 64, 6, "#2a3a5a", width=1.3)
    body += rect(300, 150, 48, 30, "#5a5a62", 'rx="3"')
    for i, x in enumerate(range(304, 344, 8)):
        body += rect(x, 126 - (i % 2) * 8, 7, 26, "#bfe8ff", 'rx="2" stroke="#6a9ab0" stroke-width="1"') + rect(x + 1, 122 - (i % 2) * 8, 5, 5, "#2a6ad0")
    return svg(defs, body)


def si_vis_pacem_para_bellum():
    # A Roman general signing a peace treaty with one hand while the other points at a row of catapults being built behind
    defs, body = bg_state("#a0522d")
    for x in (40, 110, 300):
        body += rect(x, 120, 50, 8, "#6a4a2a") + stroke(f"M{x + 10} 120 L{x + 36} 70", "#6a4a2a", 5) + circle(x + 38, 66, 7, "#4a3a2a")
        body += circle(x + 8, 132, 6, "#4a3a2a") + circle(x + 42, 132, 6, "#4a3a2a") + stroke(f"M{x + 20} 120 L{x + 24} 96", "#6a4a2a", 3)
    body += person(140, 154, 0.5, SKIN[1], "#8a6a4a", face="flat", hat="cap") + stroke("M152 124 l 10 -12", "#6a6a6a", 3)
    body += person(196, 200, 1.35, SKIN[0], "#a8282a", face="smirk", hair="#3a2a1a")
    hx, hy = 196, 200 - 64 * 1.35
    body += path(f"M{hx - 28} {hy + 2} C {hx - 28} {hy - 40} {hx + 28} {hy - 40} {hx + 28} {hy + 2} Z", "#d4af37", 'stroke="#8a6a1a" stroke-width="2"')
    body += path(f"M{hx - 24} {hy - 28} C {hx - 10} {hy - 52} {hx + 10} {hy - 52} {hx + 24} {hy - 28}", "none", 'stroke="#c0392b" stroke-width="10"')
    body += stroke("M170 160 L120 128 L90 112", "#a8282a", 9) + rect(78, 106, 14, 6, SKIN[0], 'rx="3"')
    body += rect(200, 170, 120, 30, "#6a4a2a") + paper(226, 160, 70, 24, 2, "#f4ecd8") + lettering("PAX", 261, 165, 8, "#5a3a1a", width=1.6)
    body += stroke("M230 166 L250 150", "#a8282a", 9) + stroke("M248 150 L264 128", "#f4f4f4", 2)
    return svg(defs, body)


# ---- Tech

def drone(cx, cy, s=1.0, color="#2a2e34"):
    """A quadcopter seen from the side: a body, two arms and four blurred rotors."""
    out = stroke(f"M{cx - 40 * s:.1f} {cy - 4 * s:.1f} L{cx + 40 * s:.1f} {cy - 4 * s:.1f}", color, 4 * s)
    for dx in (-40, -24, 24, 40):
        out += stroke(f"M{cx + dx * s:.1f} {cy - 4 * s:.1f} L{cx + dx * s:.1f} {cy - 12 * s:.1f}", color, 3 * s)
        out += ellipse(cx + dx * s, cy - 13 * s, 12 * s, 2.5 * s, "#a8b0b8", 'opacity="0.7"')
    return out + rect(cx - 16 * s, cy - 8 * s, 32 * s, 16 * s, color, 'rx="5"') + circle(cx + 10 * s, cy, 2 * s, "#ff3a3a")


def bomber_drone():
    # A quadcopter with a delivery company's logo dropping a small bomb on a tank and zipping away, a 5-star rating popping up
    # on its operator's phone below
    defs, body = bg_tech()
    body += rect(0, 160, W, 40, "#2a3a2a")
    body += tank(110, 156, 1.3)
    body += poly(star_points(110, 132, 40, 16, 12), "#ff9a1a", 'opacity="0.9"') + poly(star_points(110, 132, 24, 10, 12), "#ffe45c")
    body += drone(220, 40, 1.2)
    body += rect(206, 46, 28, 18, "#c8a06a", 'stroke="#8a6a3a" stroke-width="1"') + path("M212 56 Q 220 62 228 56", "none", 'stroke="#1a1a1a" stroke-width="2"')
    body += speed_lines(180, 30, 3, 40, "#aaffcc")
    body += stroke("M150 70 Q 130 90 118 108", "#ffffff", 1.5, 'stroke-dasharray="4 4"')
    body += phone(300, 150, 1.4, "#ffffff")
    for i in range(5):
        body += poly(star_points(287 + i * 6.5, 140, 3.5, 1.4), "#f2c94c")
    body += lettering("WOW", 300, 154, 5, "#3a3a3a", width=1.1)
    return svg(defs, body)


def code_review():
    # Two developers at a desk, one pointing at a screen full of red warning markers, the other shrugging and clicking
    # 'Approve' with a coffee in hand
    defs, body = bg_tech()
    body += monitor(106, 14, 140, 100, "#141820")
    for i in range(7):
        y = 28 + i * 10
        body += rect(124 + (i % 3) * 8, y, 60 + (i * 13) % 40, 4, "#6a9ad0")
        if i % 2 == 0:
            body += circle(118, y + 2, 3.5, "#ff3a3a") + rect(124, y + 5, 70, 1.5, "#ff3a3a")
    body += rect(196, 92, 40, 12, "#3aa86a", 'rx="3"') + lettering("APPROVE", 216, 95, 4, "#ffffff", width=1)
    body += rect(0, 160, W, 40, "#3a2e24")
    body += person(56, 200, 1.2, SKIN[0], "#c06a2a", face="shout", hair="#3a2a1a", glasses="round", brows="angry")
    body += stroke("M84 160 L120 76", "#c06a2a", 8) + rect(116, 66, 7, 14, SKIN[0], 'rx="3"')
    body += person(296, 200, 1.2, SKIN[1], "#3a5a8a", face="smirk", hair="#1a1a1a", beard="#1a1a1a")
    body += stroke("M268 160 L232 104", "#3a5a8a", 8) + circle(228, 100, 6, SKIN[1])
    body += stroke("M324 160 L334 128", "#3a5a8a", 8) + rect(326, 108, 20, 22, "#f4f4f4", 'rx="2" stroke="#555" stroke-width="1"')
    body += stroke("M346 114 q 6 0 6 6 q 0 6 -6 6", "#555", 2) + stroke("M332 104 q -3 -6 0 -10 M340 104 q -3 -6 0 -10", "#c8c8c8", 1.5)
    return svg(defs, body)


def ctrl_y():
    # A keyboard with the Y key worn down to a nub, a hand mashing Ctrl + Y while the same explosion repeats on the screen
    defs, body = bg_tech()
    body += monitor(26, 10, 300, 90, "#1a2030")
    for i, x in enumerate((72, 142, 212, 282)):
        k = 0.6 + i * 0.12
        body += poly(star_points(x, 54, 26 * k, 11 * k, 10), "#ff9a1a") + poly(star_points(x, 54, 15 * k, 6 * k, 10), "#ffe45c")
    body += rect(0, 130, W, 70, "#2a2a30") + rect(0, 130, W, 4, "#3a3a44")
    for row in range(3):
        for col in range(9):
            x, y = 10 + col * 38 + row * 6, 140 + row * 20
            if (row, col) == (0, 5):
                body += rect(x, y + 10, 32, 6, "#5a5a64", 'rx="2"') + lettering("Y", x + 16, y + 2, 6, "#4fc3ff", width=1.4)
                continue
            body += rect(x, y, 32, 16, "#4fc3ff" if (row, col) == (2, 0) else "#3a3a44", 'rx="3"')
    body += lettering("CTRL", 26, 164, 6, "#0b1222", width=1.2)
    body += path("M352 110 L270 118 C 254 120 240 128 226 138 C 218 146 228 152 236 148 L262 138 L300 140 L352 136 Z", SKIN[0], 'stroke="#b08a6a" stroke-width="2"')
    body += stroke("M196 132 l -6 -8 M206 128 l 0 -10 M216 132 l 6 -8", "#ffe45c", 2)
    return svg(defs, body)


def hovercraft():
    # A sleek hovercraft gliding over a minefield, every mine underneath it blinking red and doing nothing, a deminer watching
    # with his mouth open
    defs = lin("bg", ["#8ab0c8", "#d8e4e8"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 140, W, 60, "#8a7a5a")
    for x, y in ((40, 160), (110, 176), (170, 158), (230, 178), (290, 162), (330, 186), (70, 188)):
        body += path(f"M{x - 10} {y} C {x - 10} {y - 10} {x + 10} {y - 10} {x + 10} {y} Z", "#4a4a42") + circle(x, y - 8, 2.5, "#ff2a2a")
        body += circle(x, y - 8, 6, "#ff2a2a", 'opacity="0.3"')
    body += sign(310, 70, 50, 26, "#ffd23a") + skull(310, 82, 0.4, "#1a1a1a")
    body += ellipse(170, 132, 104, 18, "#1a1a1e") + path("M80 128 C 90 96 250 92 268 124 Z", "#e8ecf0", 'stroke="#5a6068" stroke-width="2"')
    body += path("M130 104 C 150 86 200 86 220 104 Z", "#3a6a9a") + rect(80, 120, 190, 6, "#d0342c")
    body += circle(78, 96, 22, "none", 'stroke="#5a6068" stroke-width="5"') + stroke("M78 76 L78 116 M58 96 L98 96", "#5a6068", 3)
    for x in (100, 170, 240):
        body += stroke(f"M{x} 152 l -6 6 M{x + 10} 152 l 0 8", "#e8f4ff", 1.5, 'opacity="0.8"')
    body += speed_lines(64, 120, 3, 30, "#ffffff")
    body += person(30, 140, 0.6, SKIN[1], "#5a6a3a", face="shout", hat="helmet")
    body += stroke("M40 124 L60 150", "#3a3a3a", 2.5) + ellipse(62, 152, 8, 3, "#3a3a3a")
    return svg(defs, body)


# ---- Utopia

def ankylorhiza():
    # A huge prehistoric whale with a crocodile's teeth bursting out of a lab aquarium tank, water and glass everywhere, a
    # scientist clutching a sign reading DE-EXTINCTION: PHASE 1
    defs, body = bg_utopia()
    body += rect(150, 40, 190, 132, "#bfe8f8", 'opacity="0.5" stroke="#8ab0c0" stroke-width="4"')
    for pts in (((150, 60), (130, 40), (160, 70)), ((150, 120), (120, 130), (152, 140)), ((210, 40), (200, 14), (226, 38))):
        body += poly(list(pts), "#e8f8ff", 'stroke="#8ab0c0" stroke-width="1.5"')
    body += path("M340 110 C 300 60 220 56 170 80 L120 92 L110 110 L176 116 C 230 140 300 150 340 130 Z", "#4a6a8a", 'stroke="#2a3a5a" stroke-width="2"')
    body += path("M120 92 L176 98 L176 116 L110 110 Z", "#f4d8d0")
    for i in range(6):
        body += poly([(124 + i * 9, 94 + i), (128 + i * 9, 104 + i), (132 + i * 9, 95 + i)], "#ffffff")
        body += poly([(118 + i * 9, 110), (122 + i * 9, 101), (126 + i * 9, 110)], "#ffffff")
    body += circle(196, 84, 5, "#ffffff") + circle(197, 84, 2.5, "#111")
    body += path("M120 172 C 100 150 116 130 130 128 C 120 150 140 160 150 172 Z", "#7ad0f0", 'opacity="0.8"') + bubbles(4, 10, "#ffffff")
    body += rect(0, 172, W, 28, "#9ad0e8", 'opacity="0.8"')
    body += person(52, 200, 1.1, SKIN[0], "#ffffff", face="shout", hair="#8a6a4a", glasses="round", brows="sad")
    body += rect(14, 88, 76, 30, "#f4f4f4", 'stroke="#555" stroke-width="1.5"')
    body += words("DE-EXTINCTION", 52, 93, 4.4, "#2a6a8a", width=1) + words("PHASE 1", 52, 105, 6, "#c0392b", width=1.2)
    return svg(defs, body)


def biohacker():
    # A shirtless tech bro in an ice bath of glowing green sludge, sensors taped all over his chest, his smartwatch showing
    # 'biological age: 19'
    defs, body = bg_utopia()
    defs += rad("ooze", [("#aaff6a", 0.9), ("#3ac04a", 0.6)])
    body += person(176, 196, 1.45, SKIN[0], SKIN[0], face="grin", hair="#c8a86a", glasses="shades")
    for x, y in ((150, 160), (176, 150), (202, 160), (164, 176), (190, 178)):
        body += rect(x - 5, y - 5, 10, 10, "#f4f4f4", 'stroke="#8a8a8a" stroke-width="1"') + stroke(f"M{x} {y} Q {x + 30} {y - 40} {300} 40", "#3a3a42", 1)
    body += rect(270, 20, 60, 40, "#2a2a32", 'rx="4"') + stroke("M276 42 l 8 0 l 4 -10 l 6 20 l 4 -10 l 24 0", "#3aff7a", 2)
    body += path("M60 150 L292 150 L280 200 L72 200 Z", "url(#ooze)", 'stroke="#c8d8d8" stroke-width="4"')
    for x, y in ((100, 162), (240, 170), (150, 186), (210, 160)):
        body += circle(x, y, 4, "#d8ffb0", 'opacity="0.8"')
    body += stroke("M112 150 L96 112", SKIN[0], 12) + rect(84, 98, 22, 18, "#1a1a1e", 'rx="4"') + words("19", 95, 102, 8, "#3aff7a", width=1.4)
    body += stroke("M250 140 q 10 -20 0 -40", "#e8f8ff", 2, 'opacity="0.8"') + stroke("M120 140 q -6 -14 0 -26", "#e8f8ff", 2, 'opacity="0.8"')
    return svg(defs, body)


def femtosecond_laser():
    # A laser beam the thickness of a hair slicing a falling apple into perfect quarters in mid-air, a physicist in safety
    # goggles timing it with a stopwatch
    defs = lin("bg", ["#1a1e2a", "#0a0c12"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 170, W, 30, "#2a2e38")
    body += rect(10, 74, 50, 26, "#8a949c", 'rx="3" stroke="#5a6068" stroke-width="2"') + rect(56, 82, 10, 10, "#5a6068")
    body += stroke("M66 87 L352 87", "#ff2a4a", 1.2) + stroke("M66 87 L352 87", "#ff2a4a", 5, 'opacity="0.2"')
    pieces = (((-1, -1), -20), ((1, -1), 20), ((-1, 1), -10), ((1, 1), 12))
    for (dx, dy), a in pieces:
        cx, cy = 190 + dx * 10, 87 + dy * 10
        body += group(path(f"M0 0 L{dx * 22} 0 A22 22 0 0 {1 if dx * dy > 0 else 0} 0 {dy * 22} Z", "#d0342c", 'stroke="#8a1a1a" stroke-width="1.5"')
                      + path(f"M0 0 L{dx * 18} 0 A18 18 0 0 {1 if dx * dy > 0 else 0} 0 {dy * 18} Z", "#f8f0d0"), f"translate({cx} {cy}) rotate({a})")
    body += stroke("M196 60 l 4 -12", "#6a4a2a", 3) + path("M200 50 q 10 -8 16 0 q -8 6 -16 0 Z", "#5aa03a")
    body += person(296, 200, 1.2, SKIN[0], "#f4f4f4", face="flat", hair=OLD_HAIR)
    body += rect(270, 112, 52, 14, "#ffd23a", 'rx="6" opacity="0.85" stroke="#8a7a1a" stroke-width="2"')
    body += stroke("M272 160 L256 130", "#f4f4f4", 8) + circle(252, 122, 10, "#f4f4f4", 'stroke="#555" stroke-width="2"') + stroke("M252 122 L252 115", "#c0392b", 1.5)
    return svg(defs, body)


def longevity_hub():
    # A gleaming white clinic in the desert where billionaires lie in pods drinking smoothies through tubes, a young intern on
    # a treadmill wired to them by a blood line
    defs = lin("bg", ["#f0b070", "#f8e0b0"])
    body = rect(0, 0, W, H, "url(#bg)") + circle(300, 34, 20, "#fff4c0") + rect(0, 120, W, 80, "#e0c088")
    body += path("M0 120 C 60 104 120 116 180 108 C 240 100 300 112 352 104 L352 120 Z", "#d8b070")
    body += rect(20, 50, 250, 120, "#f8fafa", 'stroke="#c8d0d0" stroke-width="2"') + rect(20, 50, 250, 10, "#9fd8cf")
    for i, x in enumerate((40, 120, 200)):
        body += rect(x, 96, 64, 30, "#bfe8f0", 'rx="15" stroke="#8ab0c0" stroke-width="2"')
        body += circle(x + 16, 110, 9, SKIN[0]) + circle(x + 14, 108, 1.4, "#222") + circle(x + 20, 108, 1.4, "#222") + circle(x + 9, 108, 4, OLD_HAIR)
        body += stroke(f"M{x + 22} 114 L{x + 40} 80", "#f4a0c0", 2) + rect(x + 36, 66, 10, 16, "#f4a0c0", 'rx="2"')
        body += stroke(f"M{x + 50} 126 C {x + 60} 160 {290 if i == 2 else x + 80} 160 300 150", "#c0182a", 2)
    body += rect(276, 160, 64, 8, "#3a3a42", 'rx="3"') + stroke("M330 160 L334 134", "#3a3a42", 3)
    body += person(304, 162, 0.7, SKIN[1], "#9ab0c8", face="wavy", hair="#3a2a1a", brows="sad") + sweat(320, 100, 0.8)
    return svg(defs, body)


def nanobots():
    # A swarm of glittering nanobots stitching a robot's torn arm back together, a few of them on a tiny picket line holding
    # 'Fair Wages' signs
    defs, body = bg_utopia()
    body += rect(30, 70, 110, 44, "#9aa4ae", 'rx="18" stroke="#5a6068" stroke-width="3"') + rect(176, 70, 120, 44, "#9aa4ae", 'rx="18" stroke="#5a6068" stroke-width="3"')
    body += circle(310, 92, 24, "#9aa4ae", 'stroke="#5a6068" stroke-width="3"')
    for y in (78, 92, 106):
        body += stroke(f"M140 {y} L176 {y + 2}", ["#f2c94c", "#d0342c", "#3a8ad0"][(y // 14) % 3], 2.5)
    body += poly([(136, 70), (146, 82), (138, 96), (148, 110), (140, 114)], "#5a6068") + poly([(180, 70), (172, 84), (182, 98), (172, 114)], "#5a6068")
    for i in range(40):
        x, y = 128 + (i * 37) % 60, 50 + (i * 23) % 80
        body += circle(x, y, 1.6 + (i % 3) * 0.6, "#5ae0c0" if i % 2 else "#c8fff0")
    for i, x in enumerate((210, 246, 282)):  # The picket line
        body += circle(x, 166, 5, "#5a6068") + stroke(f"M{x - 4} 172 L{x - 6} 182 M{x + 4} 172 L{x + 6} 182", "#5a6068", 2)
        body += stroke(f"M{x + 4} 160 L{x + 4} 140", "#8a6a3a", 1.5) + rect(x - 12, 126, 32, 16, "#ffffff", 'stroke="#333" stroke-width="1"')
        body += lettering(("FAIR", "WAGES", "NOW")[i], x + 4, 130, 6, "#c0392b", width=1.1)
    for x, y in ((60, 40), (250, 40), (100, 150)):
        body += poly(star_points(x, y, 6, 2, 4), "#ffffff")
    return svg(defs, body)


def prion_disease():
    # A cow in a lab coat staring blankly into a microscope while its own brain, full of holes like Swiss cheese, floats in a
    # jar beside it
    defs, body = bg_utopia()
    body += rect(0, 150, W, 50, "#c8d8e0")
    body += path("M60 200 C 60 140 180 140 180 200 Z", "#ffffff", 'stroke="#c8c8c8" stroke-width="2"') + stroke("M120 150 L120 200", "#c8c8c8", 2)
    body += ellipse(120, 96, 34, 30, "#ffffff", 'stroke="#555" stroke-width="1.5"') + ellipse(104, 82, 10, 8, "#222") + ellipse(140, 104, 8, 6, "#222")
    body += ellipse(120, 122, 22, 13, "#f2a8b8") + circle(112, 122, 2.5, "#8a4a5a") + circle(128, 122, 2.5, "#8a4a5a")
    body += ellipse(86, 70, 12, 6, "#ffffff", 'stroke="#555" stroke-width="1.5"') + ellipse(154, 70, 12, 6, "#ffffff", 'stroke="#555" stroke-width="1.5"')
    body += poly([(96, 66), (90, 50), (104, 62)], "#e8dcc0") + poly([(144, 66), (150, 50), (136, 62)], "#e8dcc0")
    body += circle(108, 96, 5, "#ffffff", 'stroke="#222" stroke-width="1.5"') + circle(132, 96, 5, "#ffffff", 'stroke="#222" stroke-width="1.5"')
    body += circle(108, 96, 1, "#222") + circle(132, 96, 1, "#222")
    body += rect(180, 140, 50, 10, "#3a3a42") + stroke("M200 140 L214 96", "#3a3a42", 8) + rect(204, 80, 14, 26, "#3a3a42", 'transform="rotate(-20 211 93)"')
    body += rect(250, 70, 72, 90, "#e8f8ff", 'rx="8" opacity="0.7" stroke="#8ab0c0" stroke-width="3"') + rect(246, 62, 80, 12, "#8a949c", 'rx="3"')
    body += path("M262 128 C 252 104 270 86 286 92 C 300 82 318 96 312 114 C 320 128 306 140 290 136 C 278 144 262 140 262 128 Z", "#f4a8b8", 'stroke="#c06a80" stroke-width="2"')
    for x, y, r in ((276, 108, 4), (294, 102, 3.5), (302, 122, 4.5), (282, 126, 3), (292, 116, 2.5)):
        body += circle(x, y, r, "#e8f8ff", 'stroke="#c06a80" stroke-width="1"')
    return svg(defs, body)


def quark():
    # Three tiny glowing particles in colored hats bouncing on a trampoline inside an atom, one flipping off the edge with a grin
    defs, body = bg_space("#140a2a", "#2a0a3a")
    defs += rad("nucleus", [("#ffe9a0", 0.35), ("#ffe9a0", 0)])
    body += circle(176, 100, 96, "url(#nucleus)") + circle(176, 100, 96, "none", 'stroke="#7ad6ff" stroke-width="2" stroke-dasharray="6 6" opacity="0.6"')
    body += ellipse(176, 150, 74, 14, "#1a1a22", 'stroke="#5a5a62" stroke-width="3"') + stroke("M112 154 L104 186 M240 154 L248 186 M150 162 L148 190 M202 162 L204 190", "#5a5a62", 3)
    for (x, y, color, a) in ((140, 90, "#ff4a5a", 0), (182, 60, "#4aff7a", 0), (262, 76, "#4a8aff", 50)):
        q = circle(0, 0, 14, color, 'opacity="0.9"') + circle(0, 0, 22, color, 'opacity="0.25"')
        q += circle(-4, -2, 2, "#111") + circle(4, -2, 2, "#111") + path("M-6 3 Q 0 10 6 3 Z", "#ffffff", 'stroke="#111" stroke-width="1"')
        q += poly([(-10, -10), (10, -10), (0, -28)], color, 'stroke="#ffffff" stroke-width="1.5"') + circle(0, -28, 3, "#ffffff")
        body += group(q, f"translate({x} {y}) rotate({a})")
    body += path("M222 112 C 236 70 262 56 284 70", "none", 'stroke="#ffffff" stroke-width="2" stroke-dasharray="4 4"')
    body += stroke("M140 112 l -4 8 M144 112 l 4 8 M182 82 l 0 10", "#ffffff", 1.5, 'opacity="0.6"')
    return svg(defs, body)


def seasteading():
    # A floating hexagonal platform city in the middle of the ocean, tech-bros sunbathing while one corner quietly sinks and a
    # lone lifeguard chair stands empty
    defs = lin("bg", ["#5ab0e8", "#c8ecfa"]) + lin("sea", ["#1a7ab0", "#0a3a6a"])
    body = rect(0, 0, W, H, "url(#bg)") + circle(50, 34, 18, "#fff8c0") + rect(0, 110, W, 90, "url(#sea)")
    hexagon = [(70, 140), (110, 112), (240, 112), (290, 140), (240, 170), (110, 170)]
    body += group(poly(hexagon, "#e8ecf0", 'stroke="#8a949c" stroke-width="3"') + poly([(p[0], p[1] + 10) for p in hexagon[3:]] + [(70, 150)], "#a8b0b8"),
                  "rotate(7 180 140)")
    body += rect(130, 60, 34, 62, "#f8fafc", 'stroke="#8ab0c0" stroke-width="2"') + rect(170, 40, 30, 82, "#f8fafc", 'stroke="#8ab0c0" stroke-width="2"')
    for y in range(68, 118, 12):
        body += rect(136, y, 22, 6, "#5ab0e8") + rect(176, y - 20, 18, 6, "#5ab0e8")
    body += rect(208, 100, 26, 22, "#f8fafc", 'stroke="#8ab0c0" stroke-width="2"') + poly([(206, 100), (236, 100), (221, 86)], "#3a3a42")
    for x in (102, 118):
        body += rect(x - 12, 132, 26, 6, "#f4a0c0", 'rx="2"') + circle(x - 14, 132, 5, SKIN[0]) + rect(x - 20, 128, 10, 3, "#111")
    body += stroke("M262 158 L262 112 M280 158 L280 112 M262 124 L280 124 M262 140 L280 140", "#f4f4f4", 3) + rect(258, 104, 26, 10, "#d0342c")
    body += path("M236 168 C 260 160 290 156 300 164 C 294 172 260 176 236 174 Z", "#2a8ac0", 'opacity="0.85"')
    for x in (250, 286):
        body += ellipse(x, 166, 10, 2.5, "#e8f8ff", 'opacity="0.8"')
    body += bubbles(2, 6, "#e8f8ff")
    return svg(defs, body)


def white_dwarf():
    # A small, very white man in a turtleneck standing in space, glowing faintly, the planets around him bending toward him
    defs, body = bg_space()
    defs += rad("glow", [("#ffffff", 0.6), ("#bfe0ff", 0)])
    for i in range(1, 7):  # Space itself, curving toward him
        body += path(f"M0 {20 + i * 26} C 120 {20 + i * 26} 150 {110 + (i - 3.5) * 10} 176 {110 + (i - 3.5) * 8} "
                     f"C 202 {110 + (i - 3.5) * 10} 232 {20 + i * 26} 352 {20 + i * 26}", "none", 'stroke="#5a6ac8" stroke-width="1" opacity="0.5"')
    body += circle(176, 110, 80, "url(#glow)")
    for x, y, r, c, sx in ((50, 50, 20, "#d08a4a", 1.25), (300, 46, 16, "#6ab0e0", 1.3), (60, 160, 12, "#c06ad0", 1.4), (300, 156, 22, "#e8c86a", 1.2)):
        body += group(ellipse(0, 0, r * sx, r, c), f"translate({x} {y}) rotate({math.degrees(math.atan2(110 - y, 176 - x)):.0f})")
    body += ellipse(300, 156, 34, 6, "none", 'stroke="#c8a85a" stroke-width="2" transform="rotate(-25 300 156)"')
    body += person(176, 170, 0.8, "#fbfbff", "#1a1a1e", face="flat", hair="#f4f4ff")
    body += rect(167, 130, 18, 10, "#1a1a1e", 'rx="4"')
    return svg(defs, body)


# ---- Zeitgeist

def absentee_parents():
    # A family portrait on a mantelpiece with two parent-shaped holes cut out of it, a postcard from Ibiza tucked into the frame
    defs, body = bg_vapor()
    body += rect(60, 140, 232, 14, "#8a5a3a") + rect(80, 154, 192, 46, "#6a4a2a") + rect(118, 160, 116, 40, "#1a0a1a") + flames(176, 200, 3, 26, 22)
    body += rect(96, 20, 160, 118, "#d4af37", 'stroke="#8a6a1a" stroke-width="3"') + rect(106, 30, 140, 98, "#f4e8d0")
    hole = "#ff7ad9"
    for x in (134, 218):  # The two parents cut out
        body += path(f"M{x - 22} 128 C {x - 22} 96 {x + 22} 96 {x + 22} 128 Z", hole) + circle(x, 70, 15, hole)
        body += path(f"M{x - 22} 128 C {x - 22} 96 {x + 22} 96 {x + 22} 128 Z", "none", 'stroke="#555" stroke-width="1" stroke-dasharray="3 2"')
    body += person(176, 128, 0.55, SKIN[0], "#4a8ac0", face="frown", hair="#8a5a2a") + person(158, 128, 0.42, SKIN[0], "#e86aa0", face="frown", hat="bob")
    body += group(rect(-26, -16, 52, 32, "#5ac0f0", 'stroke="#ffffff" stroke-width="2"') + circle(-12, -4, 6, "#ffd23a")
                  + rect(-26, 4, 52, 12, "#f0d890") + lettering("IBIZA", 8, -12, 6, "#ffffff", width=1.3), "translate(256 34) rotate(16)")
    return svg(defs, body)


def calm_your_tits():
    # A man at a family barbecue telling his furious aunt to calm down, everyone around them freezing mid-bite, the aunt's face
    # going from red to purple
    defs = lin("bg", ["#8ad0f0", "#e8f8ff"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 150, W, 50, "#6ab04a")
    body += ellipse(176, 132, 26, 12, "#1a1a1e") + stroke("M164 140 L156 170 M188 140 L196 170", "#1a1a1e", 3) + stroke("M164 120 q -4 -10 2 -18 M180 118 q 4 -10 -2 -18", "#c8c8c8", 2)
    for x in (30, 322):
        body += person(x, 180, 0.7, SKIN[(x // 30) % 4], "#c8a06a", face="flat", hair="#3a2a1a")
        body += ellipse(x + 14, 136, 10, 6, "#c8883a") + rect(x + 4, 135, 20, 3, "#6a3a1a")
    body += person(96, 200, 1.25, SKIN[0], "#4a8ac0", face="smile", hair="#5a3a1a", glasses="shades")
    body += stroke("M118 160 L140 124 M76 160 L64 128", "#4a8ac0", 8) + circle(141, 118, 7, SKIN[0]) + circle(62, 122, 7, SKIN[0])
    body += person(252, 200, 1.25, "#b04aa0", "#e86a3a", face="shout", hat="bob", brows="angry")
    body += path("M228 108 C 228 92 276 92 276 108 L276 96 L228 96 Z", "#d0342c", 'opacity="0.5"')
    for x, y in ((222, 70), (284, 72), (252, 54)):
        body += stroke(f"M{x} {y} l -4 -10 M{x + 6} {y} l 2 -12", "#ffffff", 2)
    body += bubble(160, 34, 52, 18, tail=(118, 100)) + lettering("CALM DOWN", 160, 30, 6, "#333", width=1.3)
    return svg(defs, body)


def camper_van():
    # A painted hippie camper van speeding away from a dented mailbox, a dreamcatcher swinging from the mirror, the couple
    # inside filming the getaway for their followers
    defs, body = bg_vapor()
    body += stroke("M300 186 L300 150", "#5a3a2a", 5) + group(rect(-16, -12, 32, 22, "#4a6a9a", 'rx="8" stroke="#2a3a5a" stroke-width="2"') + stroke("M-10 -6 l 8 6 l -4 6", "#2a3a5a", 2), "translate(300 140) rotate(24)")
    body += stroke("M318 120 l 8 -6 M322 132 l 10 -2", "#ffffff", 2)
    van_body = path("M40 170 L40 104 C 40 80 60 72 86 72 L200 72 C 222 72 236 90 240 110 L242 170 Z", "#7ad0c0", 'stroke="#2a5a5a" stroke-width="2"')
    van_body += path("M40 122 L242 122 L242 170 L40 170 Z", "#f4ecd8", 'stroke="#2a5a5a" stroke-width="2"')
    van_body += path("M60 80 L104 80 L104 110 L56 110 Z", "#bfe8f0") + rect(116, 80, 46, 30, "#bfe8f0") + rect(172, 80, 40, 30, "#bfe8f0")
    for x, c in ((90, "#ff5fa2"), (140, "#ffd23a"), (190, "#ff7a3a")):
        van_body += circle(x, 146, 8, c) + circle(x, 146, 3, "#ffffff")
    van_body += circle(76, 170, 14, "#1a1a1e") + circle(208, 170, 14, "#1a1a1e") + circle(76, 170, 5, "#c8c8c8") + circle(208, 170, 5, "#c8c8c8")
    body += van_body
    body += circle(80, 96, 8, SKIN[0]) + rect(70, 86, 20, 5, "#e8c86a") + phone(96, 98, 0.35, "#ff5fa2") + circle(132, 96, 8, SKIN[1]) + path("M122 94 C 122 82 142 82 142 94 Z", "#3a2a1a")
    body += stroke("M50 92 L50 104", "#8a6a3a", 1) + circle(50, 110, 6, "none", 'stroke="#8a6a3a" stroke-width="1.5"') + stroke("M46 116 l -2 8 M54 116 l 2 8", "#ff7ad9", 1.5)
    body += speed_lines(262, 120, 4, 40, "#ffffff")
    return svg(defs, body)


def fidget_spinner():
    # A giant fidget spinner whirling on a school desk, sparks flying off its bearings, kids staring at it hypnotized, a
    # teacher's 'Confiscated' box in the corner
    defs, body = room("#d8e0c8", "#b8c0a8", "#8a6a4a", 160)
    body += rect(60, 130, 232, 14, "#c8883a") + stroke("M80 144 L80 196 M272 144 L272 196", "#5a5a5a", 5)
    spinner = ""
    for a in (0, 120, 240):
        x, y = 34 * math.cos(math.radians(a - 90)), 34 * math.sin(math.radians(a - 90))
        spinner += stroke(f"M0 0 L{x:.1f} {y:.1f}", "#ff5fa2", 22) + circle(x, y, 16, "#ff5fa2", 'stroke="#a02a6a" stroke-width="2"') + circle(x, y, 8, "#c8c8d0")
    spinner += circle(0, 0, 12, "#c8c8d0", 'stroke="#6a6a72" stroke-width="2"')
    body += group(spinner, "translate(176 90) rotate(20)")
    body += circle(176, 90, 60, "none", 'stroke="#ffffff" stroke-width="2" stroke-dasharray="10 8" opacity="0.7"')
    for x, y in ((120, 50), (238, 60), (226, 130), (124, 128)):
        body += poly(star_points(x, y, 6, 2, 4), "#ffd23a")
    for i, x in enumerate((36, 316)):
        body += person(x, 200, 0.9, SKIN[i * 2], ("#4a8ac0", "#c0392b")[i], face="flat", hair=("#3a2a1a", "#c8a86a")[i])
        for dx in (-6, 6):
            body += stroke(f"M{x + dx - 3} 140 a 3 3 0 1 1 3 3", "#222", 1.2)
    body += rect(290, 20, 56, 38, "#c8a06a", 'stroke="#8a6a3a" stroke-width="2"') + lettering("CONFISCATED", 318, 34, 3.6, "#c0392b", width=0.9)
    return svg(defs, body)


def listen_kid():
    # An old man on a porch rocking chair pointing his cane at a terrified teenager, a speech bubble full of tiny drawings of
    # horses, rotary phones and a nickel
    defs, body = room("#c8a878", "#a08058", "#7a5a3a", 160)
    for x in range(0, W, 24):
        body += stroke(f"M{x} 160 L{x} 200", "#6a4a2a", 1.5)
    body += rect(10, 60, 14, 100, "#f4ecd8") + rect(330, 60, 14, 100, "#f4ecd8")
    body += path("M40 190 Q 90 204 140 190", "none", 'stroke="#5a3a1a" stroke-width="5"') + rect(46, 80, 80, 80, "#8a5a2a", 'rx="6"')
    body += person(86, 180, 1.15, SKIN[0], "#8a7a6a", face="shout", hair=OLD_HAIR, glasses="round", brows="angry")
    body += stroke("M110 140 L176 112", "#5a3a1a", 4) + stroke("M108 144 L124 134", "#8a7a6a", 8)
    body += person(270, 200, 1.1, SKIN[1], "#5a3a8a", face="wavy", hat="beanie", brows="sad") + sweat(292, 108, 0.9)
    body += bubble(186, 40, 92, 32, tail=(110, 104))
    body += group(ellipse(0, 0, 14, 7, "#8a5a2a") + rect(8, -12, 6, 10, "#8a5a2a") + stroke("M-10 4 l -2 10 M-4 4 l 0 10 M4 4 l 0 10 M10 4 l 2 10", "#8a5a2a", 2.5), "translate(122 40)")
    body += rect(160, 32, 30, 18, "#1a1a1e", 'rx="4"') + circle(175, 41, 6, "#c8c8c8") + path("M158 30 C 160 22 190 22 192 30 Z", "#1a1a1e")
    body += coin(222, 42, 10) + words("5", 222, 36, 8, "#8a6a1a", width=1.5)
    body += lettering("CENTS", 252, 52, 5, "#555", width=1)
    return svg(defs, body)


def lost_generation():
    # Tired millennials in graduation gowns sitting on the steps of a closed bank, their diplomas folded into paper boats
    # floating down the gutter
    defs = lin("bg", ["#8a94a0", "#c8ccd0"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += poly([(40, 40), (312, 40), (176, 8)], "#d8d0c0", 'stroke="#8a8a80" stroke-width="2"') + rect(40, 40, 272, 96, "#d8d0c0", 'stroke="#8a8a80" stroke-width="2"')
    for x in range(56, 312, 40):
        body += rect(x, 48, 14, 88, "#e8e4d8")
    body += rect(150, 70, 52, 66, "#4a4a52") + rect(152, 88, 48, 16, "#f4f4f4") + lettering("CLOSED", 176, 92, 7, "#c0392b", width=1.4)
    body += lettering("BANK", 176, 18, 9, "#6a6a62", width=1.8)
    for i in range(3):
        body += rect(30 + i * 10, 136 + i * 10, 292 - i * 20, 10, "#b8b0a0", 'stroke="#8a8a80" stroke-width="1"')
    for i, x in enumerate((86, 146, 210, 270)):
        y = 166 if i % 2 else 158
        body += person(x, y, 0.7, SKIN[i], "#1a1a22", face=("flat", "closed", "wavy", "flat")[i], hair=("#3a2a1a", "#c8a86a", None, "#1a1a1a")[i], brows="sad")
        body += mortarboard(x, y - 64 * 0.7, 14 * 0.7 * 1.4)
    body += rect(0, 172, W, 28, "#5a5a62") + rect(0, 186, W, 14, "#3a4a5a")
    for x in (60, 170, 280):
        body += poly([(x - 14, 186), (x + 14, 186), (x + 8, 194), (x - 8, 194)], "#f4ecd8") + poly([(x - 2, 186), (x + 2, 186), (x, 176)], "#f4ecd8")
    return svg(defs, body)


def ol_reliable():
    # A worn-out frying pan hanging alone on a kitchen wall with a halo around it, the dented shape of a burglar's head in its bottom
    defs, body = room("#f4ecd8", "#d8ccb0", "#8a6a4a", 176)
    for x in range(0, W, 22):
        for y in range(100, 176, 22):
            body += rect(x + 1, y + 1, 20, 20, "#e8f0f0", 'stroke="#c8d0d0" stroke-width="1"')
    body += circle(176, 26, 4, "#5a5a62") + rect(170, 30, 12, 62, "#3a2a1a", 'rx="4"')
    body += circle(176, 90, 120, "#fff8c0", 'opacity="0.25"')
    body += circle(176, 128, 52, "#3a3a42", 'stroke="#1a1a22" stroke-width="5"') + circle(176, 128, 44, "#4a4a52")
    body += path("M160 150 C 150 126 160 104 178 104 C 196 104 204 120 198 136 L206 140 L196 150 Z", "#2a2a32", 'stroke="#5a5a62" stroke-width="1.5"')
    body += ellipse(176, 64, 40, 8, "none", 'stroke="#ffd23a" stroke-width="4"')
    for a in range(0, 360, 30):
        x, y = 176 + 72 * math.cos(math.radians(a)), 128 + 72 * math.sin(math.radians(a))
        if y < 176:
            body += stroke(f"M{x:.1f} {y:.1f} L{176 + 84 * math.cos(math.radians(a)):.1f} {128 + 84 * math.sin(math.radians(a)):.1f}", "#ffd23a", 2.5, 'opacity="0.7"')
    return svg(defs, body)


def spite_voter():
    # A grumpy man in a voting booth gleefully ticking the box of a clown candidate, a 'Don't Blame Me' badge on his cardigan
    defs, body = bg_vapor()
    body += rect(20, 10, 312, 14, "#2a2a6a") + path("M20 24 L20 200 L60 200 C 50 140 60 80 60 24 Z", "#3a3aa0") + path("M332 24 L332 200 L292 200 C 302 140 292 80 292 24 Z", "#3a3aa0")
    body += rect(186, 70, 120, 100, "#f4f4f4", 'stroke="#555" stroke-width="2"')
    for i, y in enumerate((80, 112, 144)):
        body += rect(194, y, 16, 16, "#ffffff", 'stroke="#333" stroke-width="2"') + rect(216, y + 5, 50, 4, "#999")
        if i == 1:
            body += circle(286, y + 8, 10, "#ffffff", 'stroke="#555" stroke-width="1"') + circle(286, y + 9, 3, "#d0342c") + circle(278, y + 2, 5, "#ff7a1a") + circle(294, y + 2, 5, "#ff7a1a")
            body += stroke(f"M196 {y + 8} l 5 6 l 12 -16", "#d0342c", 3)
    body += person(110, 200, 1.3, SKIN[0], "#8a7a5a", face="grin", hair=OLD_HAIR, glasses="round", brows="angry")
    body += stroke("M136 162 L190 120", "#8a7a5a", 9) + stroke("M190 120 L198 108", "#1a1a1a", 3)
    body += circle(90, 176, 12, "#ffd23a", 'stroke="#8a6a1a" stroke-width="2"') + lettering("NOT", 90, 168, 4.5, "#333", width=1) + lettering("ME", 90, 176, 4.5, "#333", width=1)
    return svg(defs, body)


def the_friends_we_made_along_the_way():
    # A ragtag group of adventurers around a campfire laughing, an empty treasure chest behind them, a map with the X scribbled out
    defs = lin("bg", ["#0a0a24", "#2a1a3a"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 160, W, 40, "#1a2a1a") + circle(176, 170, 90, "#ff9a1a", 'opacity="0.15"')
    for i in range(14):
        body += circle((i * 71) % W, (i * 37) % 90, 1.2, "#ffffff", 'opacity="0.7"')
    body += rect(150, 86, 52, 34, "#8a5a2a", 'stroke="#5a3a1a" stroke-width="2"') + path("M150 86 L156 60 L208 60 L202 86 Z", "#7a4a1a", 'stroke="#5a3a1a" stroke-width="2"')
    body += rect(154, 90, 44, 14, "#1a0a0a") + rect(170, 96, 12, 10, "#d4af37")
    body += knight(60, 196, 0.9)
    body += person(122, 196, 0.8, SKIN[1], "#5a3a8a", face="grin", beard="#d8d8d8", hair=OLD_HAIR)
    body += poly([(104, 150 - 64 * 0.8 + 6), (140, 150 - 64 * 0.8 + 6), (122, 150 - 64 * 0.8 - 32)], "#3a2a6a")
    body += person(232, 196, 0.8, SKIN[2], "#3a6a3a", face="grin", hat="hood")
    body += dog(298, 168, 0.6, "#c8883a", "#8a5a2a", face=-1)
    body += flames(176, 182, 3, 14, 30) + rect(150, 180, 52, 6, "#5a3a1a", 'rx="3"')
    body += rect(20, 20, 70, 50, "#e8d4a0", 'stroke="#a88a5a" stroke-width="2" transform="rotate(-6 55 45)"')
    body += stroke("M30 40 C 44 30 60 50 72 36", "#8a6a3a", 1.5, 'stroke-dasharray="3 3"') + stroke("M66 48 L78 60 M78 48 L66 60", "#c0392b", 3)
    body += stroke("M60 44 Q 72 54 84 42 Q 70 64 60 56", "#1a1a1a", 2)
    return svg(defs, body)


def universal_basic_income():
    # A long line of citizens at an ATM spitting out identical small banknotes, a politician tearing his hair out, a man in the
    # line buying a lottery ticket with his
    defs, body = bg_vapor()
    body += rect(250, 40, 92, 150, "#4a4a5a", 'stroke="#2a2a3a" stroke-width="3"') + rect(262, 52, 68, 40, "#4fc3ff") + lettering("ATM", 296, 62, 10, "#1a1a3a", width=2)
    body += rect(266, 104, 60, 6, "#1a1a1a")
    for i in range(4):
        body += group(rect(-12, -6, 24, 12, "#7ad08a", 'stroke="#3a8a4a" stroke-width="1"') + circle(0, 0, 3, "#3a8a4a"), f"translate({286 - i * 16} {120 + i * 8}) rotate({-10 + i * 12})")
    for i, x in enumerate((50, 100, 150, 200)):
        body += person(x, 200, 0.8, SKIN[i], ("#c06a2a", "#4a8ac0", "#8a4a8a", "#3a8a5a")[i], face=("flat", "smile", "flat", "grin")[i], hair=("#3a2a1a", None, "#c8a86a", "#1a1a1a")[i])
    body += stroke("M180 160 L178 130", "#3a8a5a", 6) + rect(166, 112, 26, 18, "#ffd23a", 'stroke="#8a6a1a" stroke-width="1"') + lettering("LOTTO", 179, 118, 4.4, "#c0392b", width=1)
    body += person(60, 96, 0.6, SKIN[0], "#1a1a2a", face="shout", hair=OLD_HAIR, tie="#c0392b", brows="angry")
    body += stroke("M44 72 L50 50 M76 72 L70 50", "#1a1a2a", 5) + stroke("M50 46 l -4 -8 M70 46 l 4 -8", OLD_HAIR, 2)
    return svg(defs, body)


def weaponized_incompetence():
    # A man holding a plate he has 'washed' that is still covered in food, looking helpless, while his partner behind him
    # quietly takes over the whole sink
    defs, body = room("#e8f0e8", "#c8d8c8", "#8a7a6a", 150)
    for x in range(0, W, 22):
        body += rect(x + 1, 60, 20, 20, "#ffffff", 'stroke="#c8d0d0" stroke-width="1"')
    body += rect(196, 120, 156, 30, "#c8c8d0") + rect(212, 112, 120, 14, "#9aa4ae", 'rx="4"') + stroke("M272 112 L272 92 L286 92", "#8a949c", 4)
    body += person(270, 150, 1.0, SKIN[2], "#c06a8a", face="flat", hat="bob", brows="angry")
    body += stroke("M246 120 L238 112 M294 120 L306 112", "#c06a8a", 7) + rect(298, 96, 18, 14, "#f4d03a", 'rx="3"')
    for i in range(5):
        body += ellipse(330, 110 - i * 5, 16, 3, "#ffffff", 'stroke="#999" stroke-width="1"')
    for x, y in ((240, 100), (300, 92), (262, 88)):
        body += circle(x, y, 4, "#ffffff", 'stroke="#bfe8ff" stroke-width="1.5"')
    body += rect(0, 150, W, 50, "#9a8a7a")
    body += person(110, 200, 1.35, SKIN[0], "#4a6a8a", face="wavy", hair="#5a3a1a", brows="sad")
    body += stroke("M80 160 L56 134 M140 160 L164 134", "#4a6a8a", 9)
    body += ellipse(110, 128, 46, 12, "#ffffff", 'stroke="#999" stroke-width="2"')
    for x, y, r, c in ((96, 126, 6, "#c8883a"), (118, 130, 5, "#d0342c"), (128, 124, 4, "#5aa03a"), (104, 132, 3, "#8a5a2a")):
        body += circle(x, y, r, c)
    body += words("?", 168, 74, 12, "#5a5a8a", width=2.2) + words("?", 52, 82, 9, "#5a5a8a", width=1.8)
    return svg(defs, body)


CHUNK_MISSING = {
    "Corporate": {"BigFour": big_four, "BloombergTerminal": bloomberg_terminal, "CorporateBailouts": corporate_bailouts,
                  "FlashCrash": flash_crash, "HedonicTreadmillionaire": hedonic_treadmillionaire,
                  "HighFrequencyTradingScam": high_frequency_trading_scam,
                  "InternalProtocolQuotaCrusherMindset": internal_protocol_quota_crusher_mindset, "JustinTime": just_in_time,
                  "MiddleManager": middle_manager, "MoralBankruptcy": moral_bankruptcy, "Smartwatch": smartwatch, "Usury": usury,
                  "VentureCapital": venture_capital},
    "DigitalSociety": {"Influencer": influencer, "SelfieEsteem": selfie_esteem, "UMadBro": u_mad_bro},
    "Mind": {"HoldThatThought": hold_that_thought, "Hyperbole": hyperbole, "Projection": projection, "Psychiatrists": psychiatrists},
    "Myth": {"Ambrosia": ambrosia, "BlessinginDisguise": blessing_in_disguise, "DiebytheSword": die_by_the_sword,
             "GraciasJesús": gracias_jesus, "GrimReaper": grim_reaper, "HereBeDragons": here_be_dragons, "Heretic": heretic,
             "Mithril": mithril, "PubliclyFundedFaithSchools": publicly_funded_faith_schools},
    "None": {"Creatine": creatine, "EjectButton": eject_button, "Flamethrower": flamethrower, "Nicotine": nicotine,
             "Soylent": soylent, "StealthSuit": stealth_suit, "StimulantDrug": stimulant_drug, "Superglue": superglue},
    "ShadowOps": {"Arsonist": arsonist, "CompromisedAgent": compromised_agent, "CounterSurveillance": counter_surveillance,
                  "DrugPigeon": drug_pigeon, "Embassy": embassy, "ExplodingPen": exploding_pen, "NonchalantAgent": nonchalant_agent,
                  "PoloniumUmbrella": polonium_umbrella, "QBranch": q_branch, "SecretAnthraxLaboratory": secret_anthrax_laboratory,
                  "TrackingBeacon": tracking_beacon},
    "Statecraft": {"BorderCrossing": border_crossing, "Exoskeleton": exoskeleton, "PeaceForce": peace_force,
                   "SecurityGuard": security_guard, "SiVisPacemParaBellum": si_vis_pacem_para_bellum},
    "Tech": {"BomberDrone": bomber_drone, "CodeReview": code_review, "CtrlY": ctrl_y, "Hovercraft": hovercraft},
    "Utopia": {"Ankylorhiza": ankylorhiza, "Biohacker": biohacker, "FemtosecondLaser": femtosecond_laser,
               "LongevityHub": longevity_hub, "Nanobots": nanobots, "PrionDisease": prion_disease, "Quark": quark,
               "Seasteading": seasteading, "WhiteDwarf": white_dwarf},
    "Zeitgeist": {"AbsenteeParents": absentee_parents, "CalmYourTits": calm_your_tits, "CamperVan": camper_van,
                  "FidgetSpinner": fidget_spinner, "ListenKid": listen_kid, "LostGeneration": lost_generation,
                  "OlReliable": ol_reliable, "SpiteVoter": spite_voter,
                  "TheFriendsWeMadeAlongtheWay": the_friends_we_made_along_the_way,
                  "UniversalBasicIncome": universal_basic_income, "WeaponizedIncompetence": weaponized_incompetence},
}
for _theme, _cards in CHUNK_MISSING.items():
    CARDS.setdefault(_theme, {}).update(_cards)


def main():
    count = archived = 0
    for theme, cards in CARDS.items():
        for name, draw in cards.items():
            replaced = USE_PIXEL_ART and os.path.exists(os.path.join(PIXEL_DIR, f"{name}.png"))
            folder = os.path.normpath(os.path.join(ARCHIVE_DIR if replaced else OUT_DIR, theme))
            os.makedirs(folder, exist_ok=True)
            with open(os.path.join(folder, f"{name}.svg"), "w", encoding="utf-8", newline="\n") as f:
                f.write(draw())
            count += 1
            archived += replaced
    print(f"Wrote {count} card illustrations to {os.path.normpath(OUT_DIR)} ({archived} replaced by pixel art, archived "
          f"in {os.path.normpath(ARCHIVE_DIR)})")

    icon_dir = os.path.normpath(os.path.join(OUT_DIR, "..", "Icons"))
    os.makedirs(icon_dir, exist_ok=True)
    with open(os.path.join(icon_dir, "EffectIcons.svg"), "w", encoding="utf-8", newline="\n") as f:
        f.write(icon_sheet())
    print(f"Wrote effect icon sheet to {icon_dir}")
    write_fog_faces()
    print("Wrote the Fog of War card faces to Assets/Resources/FogOfWar")


if __name__ == "__main__":
    main()
