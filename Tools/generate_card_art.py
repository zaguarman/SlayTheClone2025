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


def fat_tony():
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


def ministry_of_love():
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
    defs, body = bg_vapor()
    body += flames(176, 200, 9, 40, 50)
    body += path("M126 200 C 126 160 226 160 226 200 Z", "#3a3a3a")
    body += ellipse(176, 104, 40, 50, "#f0f0f0", 'stroke="#222" stroke-width="2.5"')
    body += stroke("M150 92 L166 98 M202 92 L186 98", "#222", 2.5)
    body += circle(160, 104, 2.5, "#222") + circle(192, 104, 2.5, "#222")
    body += stroke("M166 134 Q 176 138 186 132", "#222", 2)
    body += path("M222 150 L246 150 L244 172 L224 172 Z", "#ffffff", 'stroke="#222" stroke-width="2"') + stroke("M246 156 q 8 4 0 10", "#ffffff", 3)
    return svg(defs, body)


def strike():
    defs, body = bg_vapor()
    for x, top, color in ((90, 30, "#fff7e0"), (176, 16, "#ffe0f0"), (262, 34, "#e0f0ff")):
        body += sign(x, top, 70, 40, color)
        body += stroke(f"M{x - 20} {top + 14} L{x + 20} {top + 26} M{x + 20} {top + 14} L{x - 20} {top + 26}", "#d0342c", 5)
    for i, x in enumerate((60, 130, 200, 290)):
        body += person(x, 200, 0.9, SKIN[i], ("#e76f51", "#457b9d", "#8ab17d", "#f2c94c")[i], face="shout", brows="angry")
    return svg(defs, body)


def russian_roulette():
    defs, body = bg_vapor()
    body += circle(176, 100, 70, "#5a5a6a", 'stroke="#2a2a36" stroke-width="5"') + circle(176, 100, 14, "#2a2a36")
    for i in range(6):
        a = math.radians(-90 + i * 60)
        x, y = 176 + 42 * math.cos(a), 100 + 42 * math.sin(a)
        body += circle(x, y, 15, "#111")
        if i == 0:
            body += circle(x, y, 11, "#d4af37") + circle(x, y, 5, "#a87a1a")
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

def cleaning_robot_premium():
    defs, body = bg_office()
    body += rect(0, 170, W, 30, "#3a3f4a")
    for i, (x, y) in enumerate(((26, 178), (50, 168), (34, 156), (72, 182), (16, 166), (60, 150))):
        body += rect(x - 12, y - 2, 26, 10, "#8a6a4a") + person(x, y - 2, 0.32, SKIN[i % 4], ("#3d5a80", "#a04a4a", "#4a8a5a")[i % 3])
    body += speed_lines(270, 150, 4, 50, "#ffffff")
    body += ellipse(200, 176, 78, 14, "#11141c", 'opacity="0.5"') + ellipse(200, 160, 76, 22, "#e8ecf2", 'stroke="#9aa3b0" stroke-width="3"')
    body += ellipse(200, 154, 56, 13, "#cfd6e0") + circle(200, 154, 8, "#4fc3ff") + circle(200, 154, 4, "#ffffff")
    body += rect(120, 154, 26, 8, "#9aa3b0", 'rx="3"')
    body += popup(220, 40, 110, 50) + poly([(230, 90), (244, 90), (236, 104)], "#ffffff")
    return svg(defs, body)


def inflation():
    defs, body = bg_office(chart="up")
    body += rect(40, 60, 170, 64, "#7a3a1a", 'rx="8" stroke="#3a1a0a" stroke-width="3"') + rect(40, 82, 170, 20, "#d4af37")
    body += rect(60, 136, 80, 30, "#5a2e12", 'rx="3" stroke="#2a1206" stroke-width="2"')
    for x in (80, 100, 120):
        body += stroke(f"M{x} 136 L{x} 166", "#3a1a08", 2)
    body += stroke("M150 151 L200 151", "#ffffff", 3, 'stroke-dasharray="6 5"')
    for i in range(3):
        x, y, a = 236 + i * 10, 70 + i * 26, -8 + i * 6
        tag = rect(-34, -14, 68, 28, ("#ffe066", "#ffb04a", "#ff6a4a")[i], 'rx="4" stroke="#333" stroke-width="1.5"')
        tag += "".join(coin(-20 + k * 13, 0, 6) for k in range(i + 1))
        body += group(tag, f"translate({x} {y}) rotate({a})")
    return svg(defs, body)


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


def lost_landmine():
    defs, body = bg_sky()
    body += rect(20, 140, 312, 60, "#e8d39a", 'stroke="#8a5a2a" stroke-width="6"')
    body += ellipse(206, 160, 42, 12, "#5a5a3a") + ellipse(206, 154, 32, 9, "#8a5a2a") + circle(206, 150, 6, "#3a3a2a")
    for x, y in ((190, 156), (222, 158), (212, 152)):
        body += circle(x, y, 3, "#b0632a", 'opacity="0.8"')
    body += rect(270, 150, 22, 24, "#e84a4a", 'rx="3"') + stroke("M290 152 C 300 140 300 160 292 166", "#e84a4a", 3)
    body += stroke("M250 172 L276 150", "#4a8ad0", 4) + ellipse(278, 148, 6, 4, "#4a8ad0")
    body += sign(90, 66, 84, 50, "#d8d0b0")
    body += stroke("M60 92 L72 104 L94 76", "#3a8a3a", 5) + stroke("M102 82 L124 82 M102 92 L120 92 M102 102 L116 102", "#777", 3)
    return svg(defs, body)


def raised_security_level():
    defs, body = bg_state("#e08a1a")
    for i, color in enumerate(("#c0392b", "#e67e22", "#f1c40f", "#3a6ad0", "#3a9a4a")):
        body += rect(36, 24 + i * 26, 84, 24, color, 'stroke="#333" stroke-width="2"')
    body += arrow(150, 63, 126, 63, "#222", 6)
    body += person(170, 196, 1.0, SKIN[0], "#5a5a6a", face="flat", tie="#2a2a4a", hair="#5a4a3a")
    body += stroke("M150 160 L132 70", SKIN[0], 6)
    body += rect(236, 60, 90, 140, "#6a4a2a", 'stroke="#3a2a1a" stroke-width="3"')
    body += person(282, 200, 1.7, SKIN[2], "#8a929c", face="flat", hat="helmet")
    body += rect(240, 120, 44, 70, "#aab2bc", 'rx="8" stroke="#555" stroke-width="3"') + poly(star_points(262, 154, 12, 5), "#f1c40f")
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


def plot_armor():
    defs, body = bg_vapor()
    body += poly(star_points(84, 112, 96, 52, 14), "#ff7a1a") + poly(star_points(84, 112, 60, 32, 12, -70), "#ffe14a")
    body += person(220, 200, 1.4, SKIN[1], "#2a2a2a", face="smirk", glasses="shades", hair="#3a2a1a")
    for i in range(4):
        y = 60 + i * 28
        body += stroke(f"M150 {y} C 180 {y - 12} 250 {y - 30} 290 {y + 4}", "#ffffff", 1.5, 'stroke-dasharray="3 5" opacity="0.6"')
        body += ellipse(292, y + 4, 6, 3, "#d4af37")
    body += rect(296, 150, 46, 40, "#222", 'stroke="#eee" stroke-width="2"')
    body += group("".join(rect(k * 12, 0, 6, 10, "#eee") for k in range(4)) + rect(0, 0, 46, 10, "none", 'stroke="#eee" stroke-width="1"'), "translate(296 136) rotate(-12)")
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
    defs, body = bg_sky(top="#5ac8f0", bottom="#fff0c0")
    body += rect(0, 150, W, 50, "#f6e6b4") + path("M0 150 C 80 140 160 158 240 146 C 290 140 330 150 352 146 L352 150 L0 150 Z", "#3aa0d0")
    body += group(ellipse(0, 0, 110, 16, "#ff7a5a", 'stroke="#c04a2a" stroke-width="3"') + stroke("M-100 0 L100 0", "#ffffff", 3), "translate(170 168) rotate(-6)")
    body += rect(134, 124, 76, 38, "#c8ccd4", 'rx="3" stroke="#555" stroke-width="2"') + rect(140, 128, 64, 30, "#3a6ad0")
    body += circle(266, 160, 16, "#7a4a22") + circle(262, 154, 3, "#3a2210")
    for k, r in enumerate((10, 20, 30)):
        body += path(f"M{290 - r} {70} A{r} {r} 0 0 1 {290 + r} 70", "none", f'stroke="{"#ffffff" if k == 0 else "#ffffff"}" stroke-width="5" opacity="{1 if k == 0 else 0.25}"')
    body += circle(290, 74, 4, "#ffffff")
    return svg(defs, body)


def dogpile():
    defs, body = bg_digital()
    dogs = ((110, 160, "#e8a04a"), (176, 158, "#f2d28a"), (242, 162, "#2a2a2a"), (140, 132, "#c88a3a"), (212, 130, "#f2d28a"), (176, 106, "#e8a04a"))
    for x, y, color in dogs:
        body += ellipse(x, y, 40, 20, color, 'stroke="#3a2a1a" stroke-width="2"') + circle(x + 30, y - 10, 13, color, 'stroke="#3a2a1a" stroke-width="2"')
        body += circle(x + 34, y - 12, 2.5, "#111") + ellipse(x + 40, y - 6, 4, 3, "#111") + ellipse(x + 22, y - 18, 5, 8, color, 'stroke="#3a2a1a" stroke-width="1.5"')
    body += ellipse(180, 80, 24, 16, "#e8d8b0", 'stroke="#3a2a1a" stroke-width="2"') + circle(196, 72, 12, "#e8d8b0", 'stroke="#3a2a1a" stroke-width="2"')
    body += ellipse(200, 74, 7, 6, "#3a2a1a") + circle(194, 68, 2, "#fff") + circle(203, 68, 2, "#fff")
    body += rect(60, 166, 10, 26, SKIN[0]) + circle(65, 162, 9, SKIN[0])
    for k in range(4):
        body += rect(57 + k * 4.5, 146, 3.5, 14, SKIN[0], 'rx="1.5"')
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


def keyboard_warrior():
    defs, body = bg_digital()
    body += path("M120 90 C 80 140 90 190 100 200 L252 200 C 262 190 272 140 232 90 Z", "#c0392b")
    body += person(176, 200, 1.6, SKIN[0], "#5a6a7a", face="shout", brows="angry", hat="hood")
    body += path("M146 90 C 146 56 206 56 206 90", "none", 'stroke="#222" stroke-width="7"') + rect(136, 82, 14, 22, "#222", 'rx="4"') + rect(202, 82, 14, 22, "#222", 'rx="4"')
    body += stroke("M136 150 L118 46 M216 150 L234 46", SKIN[0], 10)
    keys = "".join(rect(-56 + c * 10, -8 + r * 8, 8, 6, "#ddd") for r in range(2) for c in range(11))
    body += group(rect(-62, -14, 124, 28, "#333", 'rx="4"') + keys, "translate(176 36) rotate(-4)")
    for k in range(3):
        body += rect(140 + k * 26, 186, 10, 14, "#5adf5a", 'stroke="#1a5a1a" stroke-width="1"')
    body += circle(158, 140, 6, "#8a7a5a", 'opacity="0.6"') + circle(192, 158, 4, "#8a7a5a", 'opacity="0.6"')
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


def rage_bait():
    defs = lin("bg", ["#2a0a14", "#5a1020"]) + rad("red", [("#ff3a3a", 0.6), ("#ff3a3a", 0)])
    body = rect(0, 0, W, H, "url(#bg)") + circle(176, 90, 140, "url(#red)")
    body += circle(176, 92, 82, SKIN[0])
    for x in (146, 206):
        body += ellipse(x, 84, 22, 15, "#ffffff") + circle(x, 86, 9, "#3a1a0a") + circle(x + 3, 84, 3.5, "#ff2a2a")
    body += stroke("M118 62 L166 74 M234 62 L186 74", "#3a1a0a", 7)
    body += ellipse(176, 136, 22, 10, "#5a1a1a")
    body += rect(244, 150, 64, 50, SKIN[0], 'rx="18"') + phone(276, 156, 1.1, "#1c2a4a") + circle(286, 140, 7, "#ff2a2a") + circle(286, 140, 3, "#ffffff")
    return svg(defs, body)


# ---------------------------------------------------------------- Mind cards

def generational_trauma():
    defs, body = bg_mind()
    body += rect(0, 92, W, 108, "#4aa8d8", 'opacity="0.9"') + rect(0, 92, W, 6, "#d8f4ff")
    body += person(176, 176, 1.0, SKIN[0], "#4a4a5a", face="flat", hair=OLD_HAIR)
    body += stroke("M154 136 L142 84 M198 136 L210 84", SKIN[0], 8)
    body += person(176, 88, 0.75, SKIN[0], "#e86a4a", face="grin", hair=OLD_HAIR)
    for x in (54, 110, 250, 304):
        body += skull(x, 170, 0.8) + stroke(f"M{x} 186 L{x} 198 M{x - 8} 190 L{x + 8} 190 M{x - 8} 195 L{x + 8} 195", "#f2efe6", 2)
    return svg(defs, body)


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


def the_holy_grail():
    defs, body = bg_myth()
    body += rect(130, 40, 92, 110, "#bfe8ff", 'opacity="0.3" stroke="#e8f8ff" stroke-width="3"')
    body += ellipse(176, 136, 40, 10, "#a0182a") + path("M156 82 L196 82 C 196 104 186 112 178 114 L178 126 L188 132 L164 132 L174 126 L174 114 C 166 112 156 104 156 82 Z", "#ffd94a", 'stroke="#8a6a1a" stroke-width="2"')
    body += stroke("M142 52 L158 68 M150 50 L162 62", "#ffffff", 2, 'opacity="0.7"')
    for k in range(4):
        x = 250 + k * 28
        body += person(x, 200, 0.75, SKIN[k % 4], "#8a929c", face="flat", hat="helmet")
        body += rect(x - 6, 184, 12, 9, "#f4ecd8", 'stroke="#555" stroke-width="1"')
    body += rect(14, 120, 90, 80, "#e8dcc0", 'stroke="#8a7a5a" stroke-width="2"') + rect(14, 120, 90, 12, "#c0392b")
    for k in range(3):
        body += path(f"M{30 + k * 26} 150 L{46 + k * 26} 150 C {46 + k * 26} 162 {40 + k * 26} 166 {38 + k * 26} 168 L{38 + k * 26} 176 L{42 + k * 26} 180 L{34 + k * 26} 180 L{38 + k * 26} 176 L{38 + k * 26} 168 C {36 + k * 26} 166 {30 + k * 26} 162 {30 + k * 26} 150 Z", "#bbbbbb")
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


def deep_cover():
    defs, body = bg_shadow()
    body += rect(146, 20, 60, 160, "#cfe4f0", 'opacity="0.35" stroke="#d4af37" stroke-width="5"')
    for cx, flip, skin, other in ((90, 1, SKIN[0], SKIN[2]), (262, -1, SKIN[2], SKIN[0])):
        body += person(cx, 200, 1.4, skin, "#2a2a3a", face="flat", tie="#555")
        mask = ellipse(0, 0, 22, 26, other, 'stroke="#8a6a4a" stroke-width="1.5"') + circle(-8, -4, 3, "#222") + circle(8, -4, 3, "#222") + stroke("M-6 10 Q 0 14 6 10", "#222", 2)
        body += group(mask, f"translate({cx + flip * 26} 110) rotate({flip * 20})")
        body += stroke(f"M{cx + flip * 30} 170 L{cx + flip * 40} 130", "#2a2a3a", 9)
    return svg(defs, body)


def eavesdrop():
    defs = lin("bg", ["#f0d8a8", "#d8a868"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in (40, 196):
        body += rect(x, 70, 116, 130, "#c0392b", 'rx="10" stroke="#7a1a1a" stroke-width="3"')
    body += rect(166, 40, 14, 160, "#7a1a1a")
    body += person(110, 200, 1.3, SKIN[0], "#3a3226", face="flat", hat="fedora")
    body += stroke("M130 150 L150 110", "#3a3226", 9) + path("M150 92 L166 92 L162 126 L154 126 Z", "#cfe8f0", 'stroke="#888" stroke-width="1.5"')
    for k in range(4):
        body += path(f"M{60 + k * 2} {190 - k * 18} L{76 + k * 2} {190 - k * 18} L{73 + k * 2} {172 - k * 18} L{63 + k * 2} {172 - k * 18} Z", "#cfe8f0", 'stroke="#888" stroke-width="1"')
    body += stroke("M352 30 L290 60", SKIN[1], 10) + path("M276 46 L300 46 L304 70 L272 70 Z", "#3a3a3a") + stroke("M276 60 L262 74", "#5a3a1a", 4)
    return svg(defs, body)


def mole():
    defs, body = bg_office()
    body += rect(0, 150, W, 50, "#7a7060") + ellipse(200, 176, 44, 14, "#111")
    body += rect(186, 160, 30, 10, "#d0342c") + stroke("M186 160 L186 186", "#888", 2) + poly(star_points(194, 165, 3, 1.3), "#f2c94c")
    body += rect(150, 110, 100, 40, "#8a6a4a")
    body += person(120, 160, 1.3, SKIN[0], "#c8a878", face="smile", glasses="round", hair="#7a6a5a")
    body += ellipse(120, 92, 7, 5, "#e88aa0")
    body += stroke("M92 150 L100 132 M148 150 L162 120", "#c8a878", 9)
    for x, y in ((162, 116), (100, 128)):
        body += stroke(f"M{x - 4} {y} L{x - 8} {y - 8} M{x} {y} L{x} {y - 9} M{x + 4} {y} L{x + 8} {y - 8}", "#f4f0e0", 2)
    body += rect(108, 128, 24, 6, "#2a5aa0") + rect(116, 134, 8, 12, "#ffffff")
    return svg(defs, body)


def political_decoy():
    defs, body = bg_state("#2a3a8a")
    body += rect(60, 20, 232, 130, "#e8dcc0", 'stroke="#8a7a5a" stroke-width="3"') + rect(80, 40, 192, 110, "#3a2a2a")
    body += path("M196 40 L272 40 L272 150 C 250 120 230 90 210 40 Z", "#a0182a")
    body += person(140, 150, 1.2, SKIN[0], "#1a1a3a", face="grin", hair="#9a8a7a", tie="#d0342c")
    body += stroke("M164 120 L190 76", "#1a1a3a", 9) + circle(192, 72, 7, SKIN[0])
    body += circle(232, 108, 20 * 0.9, SKIN[0]) + path("M214 102 C 216 82 248 82 250 102 Z", "#9a8a7a") + circle(226, 106, 2, "#222") + circle(238, 106, 2, "#222")
    body += rect(40, 150, 272, 16, "#cfc4a8", 'stroke="#8a7a5a" stroke-width="2"')
    for k in range(9):
        body += rect(48 + k * 30, 166, 8, 34, "#cfc4a8")
    return svg(defs, body)


def signal_jammer():
    defs, body = bg_shadow()
    for i, x in enumerate((40, 96, 256, 312)):
        body += person(x, 200, 0.9, SKIN[i % 4], ("#e84a4a", "#4a8ad0", "#f2c94c", "#5aaa5a")[i], face="frown")
        body += phone(x, 150, 0.5, "#ffffff") + stroke(f"M{x - 4} 144 L{x + 4} 152 M{x + 4} 144 L{x - 4} 152", "#d0342c", 2)
    body += rect(150, 100, 52, 60, "#2a2a2a", 'rx="6"')
    for k, (dx, h) in enumerate(((-18, 70), (-4, 90), (10, 60), (22, 80))):
        body += stroke(f"M{176 + dx} 104 L{176 + dx} {104 - h}", "#888", 3) + circle(176 + dx, 104 - h, 4, "#d0342c")
    body += person(176, 200, 1.3, SKIN[1], "#111", face="flat", glasses="shades")
    for r in (40, 60, 80):
        body += path(f"M{176 - r} {40} A{r} {r} 0 0 1 {176 + r} 40", "none", 'stroke="#d0342c" stroke-width="2" opacity="0.5"')
    return svg(defs, body)


def sleeper_agent():
    defs, body = bg_sky(top="#86c9f2", bottom="#e8f8d0", sun=False)
    body += rect(240, 50, 100, 80, "#e8dcc0") + poly([(232, 52), (290, 14), (348, 52)], "#b0503a") + rect(280, 90, 24, 40, "#6a4a2a")
    body += rect(0, 130, W, 70, "#6abf5a")
    for k in range(8):
        body += stroke(f"M{k * 46} 130 L{k * 46 + 30} 200", "#5aaa4a", 10, 'opacity="0.5"')
    shadow = person(0, 0, 1.0, "#14200f", "#14200f", face="flat", hat="fedora") + stroke("M20 -36 L84 -58", "#14200f", 6)
    body += group(shadow, "translate(52 200) scale(1.7 0.62)", 0.75)
    body += person(110, 170, 1.1, SKIN[0], "#4a8ad0", face="smile", hair="#7a6a5a")
    body += stroke("M128 140 L160 156", "#4a8ad0", 8) + rect(156, 150, 40, 20, "#d0342c", 'rx="4"') + circle(164, 172, 6, "#222") + circle(190, 172, 6, "#222")
    body += path("M128 98 C 136 104 134 116 128 122", "none", 'stroke="#d8d8d8" stroke-width="2"')
    return svg(defs, body)


def smoke_and_mirrors():
    defs = lin("bg", ["#3a0a14", "#14060a"])
    body = rect(0, 0, W, H, "url(#bg)")
    for x in (0, 312):
        body += path(f"M{x} 0 L{x + 40} 0 C {x + 30} 60 {x + 44} 120 {x + 30} 200 L{x} 200 Z", "#8a1a2a")
    for k in range(5):
        body += rect(150 + k * 38, 40, 30, 80, "#cfe4f0", 'opacity="0.45" stroke="#d4af37" stroke-width="2"') + tank(165 + k * 38, 90, 0.35)
    body += cloud(110, 130, 1.1, "#d8d8e0", 0.9) + group(tank(0, 0, 0.9), "translate(96 128)", 0.35)
    body += person(60, 200, 1.1, SKIN[0], "#111", face="smirk", hat="tophat", tie="#ffffff")
    body += stroke("M80 160 L110 140", "#111", 4) + stroke("M110 140 L118 134", "#ffffff", 4)
    body += person(300, 200, 0.9, SKIN[2], "#4a5a2e", face="wavy", hat="peaked")
    return svg(defs, body)


def tapped_communications():
    defs = lin("bg", ["#d8c8a0", "#a89870"])
    body = rect(0, 0, W, H, "url(#bg)")
    body += rect(220, 10, 110, 90, "#7ab0d8", 'stroke="#5a3a22" stroke-width="5"') + stroke("M275 10 L275 100 M220 55 L330 55", "#5a3a22", 4)
    body += rect(0, 120, 240, 80, "#5a3a22")
    body += ellipse(110, 112, 54, 18, "#c0392b") + rect(70, 92, 80, 24, "#c0392b", 'rx="6"') + circle(110, 104, 14, "#f4ecd8") + circle(110, 104, 4, "#c0392b")
    body += path("M60 86 C 60 70 160 70 160 86 L150 92 L70 92 Z", "#c0392b")
    body += stroke("M150 110 C 190 130 180 80 220 90 C 250 96 260 60 280 80 C 300 100 296 150 300 170", "#222", 2.5)
    body += rect(250, 150, 96, 44, "#f4f4f4", 'rx="6" stroke="#555" stroke-width="2"') + circle(270, 196, 8, "#222") + circle(326, 196, 8, "#222")
    body += circle(276, 166, 6, "#e86aa0") + circle(270, 172, 5, "#f2c94c") + circle(282, 172, 5, "#e86aa0")
    body += path("M300 164 C 300 154 320 154 320 164", "none", 'stroke="#222" stroke-width="3"')
    return svg(defs, body)


# ---------------------------------------------------------------- Tech cards

def antivirus():
    defs, body = bg_tech()
    body += rect(20, 40, 312, 160, "#5a5e66") + "".join(rect(20 + k * 40, 26, 24, 16, "#5a5e66") for k in range(8))
    body += popup(30, 60, 60, 30) + popup(262, 56, 64, 30) + popup(40, 120, 54, 28)
    body += path("M130 200 L130 120 C 130 90 222 90 222 120 L222 200 Z", "#2a2a2a")
    body += path("M146 200 L150 130 L202 130 L206 200 Z", "#c8ccd4", 'stroke="#555" stroke-width="2"')
    body += circle(176, 110, 26, "#ffffff", 'stroke="#222" stroke-width="2"') + circle(154, 88, 9, "#111") + circle(198, 88, 9, "#111")
    body += ellipse(166, 108, 7, 9, "#111") + ellipse(186, 108, 7, 9, "#111") + circle(176, 120, 4, "#111")
    body += path("M196 140 L240 140 L240 170 C 240 186 218 196 218 196 C 218 196 196 186 196 170 Z", "#3a8ad0", 'stroke="#ddd" stroke-width="3"')
    body += stroke("M208 166 L216 176 L230 152", "#5adf5a", 5)
    body += rect(270, 110, 46, 20, "#f4ecd8", 'rx="8"') + circle(292, 120, 26, "#aa5ad0", 'opacity="0.3"') + stroke("M268 112 L252 132", "#ffffff", 2)
    return svg(defs, body)


def ddos_attack():
    defs, body = bg_tech()
    for x in (40, 300):
        body += rect(x - 22, 110, 44, 80, "#e8e8e8", 'rx="6"') + circle(x - 8, 128, 5, "#ff2a2a") + circle(x + 8, 128, 5, "#ff2a2a")
    body += rect(250, 150, 30, 22, "#c0c0c0", 'rx="4"') + circle(258, 158, 3, "#ff2a2a") + circle(270, 158, 3, "#ff2a2a")
    body += rect(130, 60, 92, 140, "#1b1f2a", 'stroke="#555" stroke-width="3"')
    for k in range(6):
        body += rect(138, 70 + k * 20, 76, 12, "#2a3040") + circle(146, 76 + k * 20, 2.5, ("#5adf5a", "#ff6a3a")[k % 2])
    body += path("M130 160 C 140 190 150 170 160 196 C 170 176 182 196 196 180 C 206 196 214 176 222 186 L222 200 L130 200 Z", "#1b1f2a")
    for i in range(18):
        body += envelope(100 + (i * 37) % 160, 10 + (i * 23) % 60, 0.8)
    return svg(defs, body)


def end_to_end_encryption():
    defs, body = bg_sky(top="#9ad0f0", bottom="#e8f0ff", sun=False)
    for cx, flip in ((120, 1), (232, -1)):
        body += ellipse(cx, 60, 26, 14, "#5a6272") + circle(cx + flip * 22, 50, 9, "#5a6272") + poly([(cx + flip * 30, 50), (cx + flip * 38, 52), (cx + flip * 30, 54)], "#f2c94c")
        body += circle(cx + flip * 24, 48, 2, "#ffffff") + circle(cx + flip * 24, 48, 1, "#111") + ellipse(cx + flip * 18, 56, 4, 3, "#5aaa8a")
        body += path(f"M{cx - 14} 56 C {cx - 8} 26 {cx + 6} 26 {cx + 12} 50 Z", "#3a4250") + poly([(cx - flip * 24, 58), (cx - flip * 40, 52), (cx - flip * 38, 66)], "#3a4250")
    body += rect(156, 46, 40, 28, "#f4f4f4", 'stroke="#555" stroke-width="1.5"') + padlock(166, 60, 0.6) + padlock(186, 58, 0.6) + padlock(176, 66, 0.5)
    body += rect(0, 170, W, 30, "#6abf5a")
    body += person(176, 200, 1.1, SKIN[0], "#111", face="frown", glasses="shades")
    body += stroke("M196 160 L250 70", "#8a5a2a", 4) + ellipse(256, 62, 20, 14, "none", 'stroke="#ddd" stroke-width="2"')
    body += rect(110, 140, 30, 38, "#ffffff", 'stroke="#555" stroke-width="1.5"') + stroke("M116 150 L134 150 M116 158 L130 158 M116 166 L132 166", "#888", 2)
    return svg(defs, body)


def legacy_code():
    defs = lin("bg", ["#2a2a24", "#14140f"])
    body = rect(0, 0, W, H, "url(#bg)") + stroke("M0 0 L60 40 M0 30 L50 0 M0 60 L80 0 M20 0 C 30 20 40 30 60 40", "#bbb", 1, 'opacity="0.5"')
    body += rect(100, 40, 152, 150, "#d8cfa8", 'stroke="#8a8060" stroke-width="3"')
    for x in (136, 216):
        body += circle(x, 82, 24, "#3a3a3a") + circle(x, 82, 8, "#888")
    for k in range(6):
        body += rect(116 + k * 20, 124, 12, 8, ("#5adf5a", "#ff6a3a", "#f2c94c")[k % 3])
    body += stroke("M104 140 L248 176 M248 140 L104 176", "#9a9a9a", 10, 'opacity="0.9"')
    body += group(rect(-18, -18, 36, 36, "#f2e080") + stroke("M-12 -8 L12 -8 M-12 0 L8 0 M-12 8 L10 8", "#888", 2), "translate(232 60) rotate(8)")
    body += popup(262, 120, 76, 40, "#3a6ad0") + stroke("M262 120 L338 160 M338 120 L262 160 M300 120 L300 160", "#ccc", 1, 'opacity="0.8"')
    return svg(defs, body)


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
        "MultimillionairePhilanthropist": multimillionaire_philanthropist, "FatTony": fat_tony, "CharlesPonzi": charles_ponzi,
        "MarginCall": margin_call, "HostileTakeover": hostile_takeover, "MassLayoffs": mass_layoffs,
        "TeamBuildingExercise": team_building_exercise, "DeadCatBounce": dead_cat_bounce,
        "TaxpayerFundsVentureCapitalProfits": taxpayer_funds_venture_capital_profits,
        "CleaningRobotPremium": cleaning_robot_premium, "Inflation": inflation,
    },
    "Statecraft": {
        "KingEmeritus": king_emeritus, "SpiteElectedPresident": spite_elected_president,
        "PoliceOfficerProductivityTarget": police_officer_productivity_target,
        "InternalRevenueService": internal_revenue_service, "Mercenaries": mercenaries, "ManoWar": man_o_war,
        "MinistryOfLove": ministry_of_love, "SupremeCourt": supreme_court, "ProtocolEnforcers": protocol_enforcers,
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
        "DeliveryCyclist": delivery_cyclist, "PlotArmor": plot_armor,
    },
    "DigitalSociety": {
        "ChronicallyOnline": chronically_online, "DigitalNomad": digital_nomad, "Dogpile": dogpile, "ItsATrap": its_a_trap,
        "KeyboardWarrior": keyboard_warrior, "NPC": npc, "RageBait": rage_bait,
    },
    "Mind": {
        "GenerationalTrauma": generational_trauma, "Introvert": introvert, "Pessimist": pessimist,
    },
    "Myth": {
        "Exorcism": exorcism, "Martyr": martyr, "MementoMori": memento_mori, "Mummy": mummy, "TheHolyGrail": the_holy_grail,
    },
    "ShadowOps": {
        "BurnNotice": burn_notice, "CounterIntelligence": counter_intelligence, "DeepCover": deep_cover, "Eavesdrop": eavesdrop,
        "Mole": mole, "PoliticalDecoy": political_decoy, "SignalJammer": signal_jammer, "SleeperAgent": sleeper_agent,
        "SmokeAndMirrors": smoke_and_mirrors, "TappedCommunications": tapped_communications,
    },
    "Tech": {
        "Antivirus": antivirus, "DDOSAttack": ddos_attack, "EndtoEndEncryption": end_to_end_encryption, "LegacyCode": legacy_code,
        "ScriptKiddie": script_kiddie,
    },
}


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
