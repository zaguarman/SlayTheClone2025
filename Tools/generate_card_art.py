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
    defs, body = bg_vapor()
    for x, top, color in ((90, 30, "#fff7e0"), (176, 16, "#ffe0f0"), (262, 34, "#e0f0ff")):
        body += sign(x, top, 70, 40, color)
        body += stroke(f"M{x - 20} {top + 14} L{x + 20} {top + 26} M{x + 20} {top + 14} L{x - 20} {top + 26}", "#d0342c", 5)
    for i, x in enumerate((60, 130, 200, 290)):
        body += person(x, 200, 0.9, SKIN[i], ("#e76f51", "#457b9d", "#8ab17d", "#f2c94c")[i], face="shout", brows="angry")
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
    defs, body = bg_vapor()
    body += path("M70 120 L120 150 L230 150 L230 170 L110 170 L60 136 Z", "#3a3a5a")
    body += person(110, 168, 1.3, SKIN[0], "#8ab17d", face="closed", hair="#6a4a2a")
    body += path("M150 150 L270 136 L274 150 L150 164 Z", "#2a4a6a") + rect(266, 128, 20, 14, "#222", 'rx="3"')
    body += zzz(150, 60, 1.3)
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
    defs = lin("bg", ["#e8e0d0", "#cfc4b0"])
    body = rect(0, 0, W, H, "url(#bg)") + rect(0, 120, W, 80, "#b08a5a")
    for k in range(8):
        body += stroke(f"M0 {128 + k * 10} L{W} {128 + k * 10}", "#9a7448", 1.5)
    body += rect(0, 112, W, 8, "#f4ecd8")
    for x, y in ((290, 168), (316, 150), (300, 186)):
        body += circle(x, y, 6, "#9a9080", 'opacity="0.8"') + circle(x + 4, y - 3, 4, "#9a9080", 'opacity="0.8"')
    body += stroke("M30 176 L120 176 M40 186 L110 186", "#c8a878", 3, 'opacity="0.7"')
    body += ellipse(176, 182, 70, 12, "#000000", 'opacity="0.2"') + ellipse(176, 164, 68, 22, "#3a3f4a", 'stroke="#22262e" stroke-width="3"')
    body += ellipse(176, 158, 58, 15, "#4a505c") + circle(176, 152, 9, "#22262e") + circle(176, 152, 3, "#5adf5a")
    body += path("M118 160 C 130 148 222 148 234 160", "none", 'stroke="#22262e" stroke-width="3"')
    body += stroke("M224 172 L248 182 M226 166 L252 170", "#888", 2)
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
    # A creep in a trench coat and a low hat peering through a brass spyglass at a couple kissing in a lit window
    defs, body = bg_tech()
    body += rect(196, 22, 136, 120, "#ffe9a8", 'stroke="#3a2f20" stroke-width="6"')  # The window
    body += rect(196, 22, 136, 120, "none", 'stroke="#5a4a32" stroke-width="2"') + stroke("M264 22 L264 142", "#5a4a32", 3)
    body += path("M196 22 L230 22 C 220 60 214 100 206 142 L196 142 Z", "#c0392b", 'opacity="0.85"')  # Curtains
    body += path("M332 22 L298 22 C 308 60 314 100 322 142 L332 142 Z", "#c0392b", 'opacity="0.85"')
    # The couple, in profile, kissing
    body += path("M210 142 C 210 104 254 100 260 142 Z", "#3d5a80") + path("M268 142 C 274 100 318 104 318 142 Z", "#c05a8a")
    body += circle(246, 82, 18, SKIN[0]) + path("M228 78 C 228 56 262 54 262 74 L254 70 C 246 66 236 70 232 84 Z", "#3a2a1a")
    body += circle(282, 82, 18, SKIN[2]) + path("M300 70 C 300 52 266 52 266 72 C 268 96 296 112 304 120 C 302 100 300 84 300 70 Z", "#1a1a1a")
    body += stroke("M248 80 q 4 3 8 0 M272 80 q 4 3 8 0", "#222", 1.6)
    body += path("M264 70 C 258 58 244 62 252 72 C 256 76 264 82 264 82 C 264 82 272 76 276 72 C 284 62 270 58 264 70 Z", "#e8304a", 'transform="translate(0 -26)"')
    # The creep: hat brim low, collar up, eye to the spyglass
    body += path("M40 200 C 40 140 130 140 130 200 Z", "#5a4a32")
    body += poly([(70, 150), (86, 176), (100, 150)], "#3a2f20")
    body += circle(86, 112, 24, SKIN[1]) + rect(56, 92, 60, 8, "#2a2418", 'rx="3"') + rect(66, 70, 40, 26, "#2a2418", 'rx="6"')
    body += path("M62 122 C 70 140 102 140 110 122 L110 132 C 100 148 72 148 62 132 Z", "#5a4a32")
    body += stroke("M78 114 L90 112", "#222", 3)
    body += group(rect(0, -8, 70, 16, "#c8a040", 'rx="4" stroke="#7a5a1a" stroke-width="2"')
                  + rect(70, -11, 28, 22, "#d8b450", 'rx="4" stroke="#7a5a1a" stroke-width="2"')
                  + rect(-6, -6, 8, 12, "#7a5a1a"), "translate(96 110) rotate(-12)")
    body += path("M110 160 C 120 140 140 128 150 116", "none", 'stroke="#5a4a32" stroke-width="12" stroke-linecap="round"')
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
        "GenerationalTrauma": generational_trauma, "Introvert": introvert, "Pessimist": pessimist,
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
CARDS["Mind"].update({"ShipofTheseus": ship_of_theseus, "EscapeGoat": scapegoating})


def main():
    count = 0
    for theme, cards in CARDS.items():
        folder = os.path.normpath(os.path.join(OUT_DIR, theme))
        os.makedirs(folder, exist_ok=True)
        for name, draw in cards.items():
            with open(os.path.join(folder, f"{name}.svg"), "w", encoding="utf-8", newline="\n") as f:
                f.write(draw())
            count += 1
    print(f"Wrote {count} card illustrations to {os.path.normpath(OUT_DIR)}")

    icon_dir = os.path.normpath(os.path.join(OUT_DIR, "..", "Icons"))
    os.makedirs(icon_dir, exist_ok=True)
    with open(os.path.join(icon_dir, "EffectIcons.svg"), "w", encoding="utf-8", newline="\n") as f:
        f.write(icon_sheet())
    print(f"Wrote effect icon sheet to {icon_dir}")
    write_fog_faces()
    print("Wrote the Fog of War card faces to Assets/Resources/FogOfWar")


if __name__ == "__main__":
    main()
