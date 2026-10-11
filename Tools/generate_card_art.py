"""Generates the card illustrations as SVG files.

Output: Assets/Art/Cards/<Theme>/<CardAssetName>.svg (one per card asset in Assets/Scriptables/Cards)
and Assets/Art/Icons/EffectIcons.svg (the icon sheet used inside card text).
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


def bg_spider(moon=True):
    defs = lin("bg", ["#40305e", "#0c0814"]) + rad("moonGlow", [("#f4edd0", 0.5), ("#f4edd0", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    if moon:
        body += circle(298, 42, 46, "url(#moonGlow)") + circle(298, 42, 22, "#efe7c8")
        body += circle(291, 37, 4, "#d9cfa8") + circle(304, 49, 3, "#d9cfa8")
    # Corner web
    for a in range(0, 91, 18):
        r = math.radians(a)
        body += stroke(f"M0 0 L{110 * math.cos(r):.1f} {110 * math.sin(r):.1f}", "#cfc6e6", 0.8, 'opacity="0.35"')
    for rr in (30, 55, 80, 105):
        body += stroke(f"M{rr} 0 Q {rr * 0.75:.1f} {rr * 0.75:.1f} 0 {rr}", "#cfc6e6", 0.8, 'opacity="0.3"')
    body += path(f"M0 {H} L0 176 C 70 168 120 182 190 172 C 250 164 300 180 {W} 170 L{W} {H} Z", "#120c1c")
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


def bg_night():
    defs = lin("bg", ["#141a3d", "#4b3b6e"]) + rad("moonGlow", [("#fdf6d8", 0.55), ("#fdf6d8", 0)])
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(30):
        body += circle((i * 113) % W, (i * 37) % 120, 0.7 + i % 3 * 0.4, "#ffffff", f'opacity="{0.4 + (i % 3) * 0.2:.1f}"')
    body += circle(290, 46, 48, "url(#moonGlow)") + circle(290, 46, 24, "#fbf3cf")
    return defs, body


def bg_storm():
    defs = lin("bg", ["#262d40", "#6a7590"])
    body = rect(0, 0, W, H, "url(#bg)")
    for cx, cy, s in ((40, 30, 1.4), (150, 18, 1.2), (270, 34, 1.5), (340, 10, 1.0)):
        body += cloud(cx, cy, s, "#3c4560", 0.95)
    body += bolt(40, 50, 2.0) + bolt(300, 60, 1.6)
    return defs, body


def cloud(cx, cy, s, color="#ffffff", opacity=0.9):
    puffs = ellipse(0, 0, 26, 12, color) + ellipse(-18, 4, 16, 10, color) + ellipse(18, 4, 18, 10, color) + ellipse(4, -8, 14, 10, color)
    return group(puffs, f"translate({cx} {cy}) scale({s})", opacity)


# ---------------------------------------------------------------- Shared creatures

def spider(cx, cy, s, body="#2b2233", abdomen=None, legs=None, eyes="#ff4d4d", pattern="", hairy=False):
    """Top-down spider centered at (cx, cy)."""
    abdomen = abdomen or body
    legs = legs or body
    out = ""
    leg_w = 6 if hairy else 3.5
    # Four legs per side fanning from front to back; knees arch above the body, feet rest below it
    for side in (-1, 1):
        for ang in (-50, -18, 14, 44):
            a = math.radians(ang)
            kx = cx + side * 42 * s * math.cos(a)
            ky = cy - 8 * s + 40 * s * math.sin(a) - 24 * s
            fx = cx + side * 80 * s * math.cos(a)
            fy = cy - 8 * s + 76 * s * math.sin(a) + 20 * s
            out += stroke(f"M{cx:.1f} {cy - 8 * s:.1f} L{kx:.1f} {ky:.1f} L{fx:.1f} {fy:.1f}", legs, leg_w * s)
            if hairy:
                for t in (0.35, 0.65):
                    hx, hy = kx + (fx - kx) * t, ky + (fy - ky) * t
                    out += stroke(f"M{hx:.1f} {hy:.1f} l{side * 4 * s:.1f} {-4 * s:.1f}", legs, 1.4 * s)
    out += ellipse(cx, cy + 22 * s, 30 * s, 36 * s, abdomen)  # abdomen
    out += ellipse(cx - 8 * s, cy + 10 * s, 8 * s, 14 * s, "#ffffff", 'opacity="0.12"')
    out += pattern
    out += ellipse(cx, cy - 12 * s, 18 * s, 15 * s, body)  # cephalothorax
    for ex, ey, er in ((-6, -20, 3.2), (6, -20, 3.2), (-11, -16, 2.2), (11, -16, 2.2), (-3, -15, 1.8), (3, -15, 1.8)):
        out += circle(cx + ex * s, cy + ey * s, er * s, eyes)
    out += stroke(f"M{cx - 4 * s:.1f} {cy - 2 * s:.1f} q {-2 * s:.1f} {6 * s:.1f} {1 * s:.1f} {8 * s:.1f}", "#d9d2e6", 2 * s)
    out += stroke(f"M{cx + 4 * s:.1f} {cy - 2 * s:.1f} q {2 * s:.1f} {6 * s:.1f} {-1 * s:.1f} {8 * s:.1f}", "#d9d2e6", 2 * s)
    return out


def flying_bird(cx, cy, s, body, wing, belly, beak="#f2b134", head=None, tail=None, eye_color="#111"):
    """Bird seen from the front-side with spread wings."""
    head = head or body
    tail = tail or wing
    out = ""
    out += path(f"M{cx - 8 * s:.1f} {cy + 18 * s:.1f} L{cx - 22 * s:.1f} {cy + 48 * s:.1f} L{cx:.1f} {cy + 40 * s:.1f} L{cx + 22 * s:.1f} {cy + 48 * s:.1f} L{cx + 8 * s:.1f} {cy + 18 * s:.1f} Z", tail)
    for side in (-1, 1):
        d = (f"M{cx + side * 10 * s:.1f} {cy - 4 * s:.1f} "
             f"C {cx + side * 50 * s:.1f} {cy - 50 * s:.1f} {cx + side * 110 * s:.1f} {cy - 46 * s:.1f} {cx + side * 140 * s:.1f} {cy - 62 * s:.1f} "
             f"C {cx + side * 128 * s:.1f} {cy - 40 * s:.1f} {cx + side * 120 * s:.1f} {cy - 30 * s:.1f} {cx + side * 112 * s:.1f} {cy - 22 * s:.1f} "
             f"L {cx + side * 120 * s:.1f} {cy - 18 * s:.1f} L {cx + side * 96 * s:.1f} {cy - 10 * s:.1f} L {cx + side * 102 * s:.1f} {cy - 4 * s:.1f} "
             f"L {cx + side * 76 * s:.1f} {cy + 2 * s:.1f} L {cx + side * 80 * s:.1f} {cy + 8 * s:.1f} "
             f"C {cx + side * 50 * s:.1f} {cy + 10 * s:.1f} {cx + side * 24 * s:.1f} {cy + 16 * s:.1f} {cx + side * 10 * s:.1f} {cy + 16 * s:.1f} Z")
        out += path(d, wing)
        out += stroke(f"M{cx + side * 30 * s:.1f} {cy - 14 * s:.1f} C {cx + side * 70 * s:.1f} {cy - 34 * s:.1f} {cx + side * 100 * s:.1f} {cy - 36 * s:.1f} {cx + side * 128 * s:.1f} {cy - 52 * s:.1f}", "#ffffff", 1.5 * s, 'opacity="0.35"')
    out += ellipse(cx, cy + 6 * s, 16 * s, 26 * s, body)
    out += ellipse(cx, cy + 12 * s, 10 * s, 17 * s, belly)
    out += circle(cx, cy - 22 * s, 13 * s, head)
    out += poly([(cx - 5 * s, cy - 19 * s), (cx + 5 * s, cy - 19 * s), (cx, cy - 7 * s)], beak)
    out += eye(cx - 5 * s, cy - 25 * s, 3 * s, eye_color) + eye(cx + 5 * s, cy - 25 * s, 3 * s, eye_color)
    return out


def perched_bird(cx, cy, s, body, wing, belly, beak="#f2b134", head=None, eye_color="#111", facing=1):
    """Side view of a perched bird; facing=1 looks right."""
    head = head or body
    f = facing
    out = ""
    out += path(f"M{cx - f * 22 * s:.1f} {cy + 18 * s:.1f} L{cx - f * 58 * s:.1f} {cy + 50 * s:.1f} L{cx - f * 44 * s:.1f} {cy + 54 * s:.1f} L{cx - f * 12 * s:.1f} {cy + 28 * s:.1f} Z", wing)
    out += ellipse(cx, cy + 10 * s, 30 * s, 24 * s, body, f'transform="rotate({f * -20} {cx} {cy + 10 * s})"')
    out += ellipse(cx + f * 8 * s, cy + 16 * s, 18 * s, 15 * s, belly, f'transform="rotate({f * -20} {cx} {cy + 10 * s})"')
    out += path(f"M{cx + f * 10 * s:.1f} {cy - 2 * s:.1f} C {cx - f * 10 * s:.1f} {cy - 6 * s:.1f} {cx - f * 34 * s:.1f} {cy + 6 * s:.1f} {cx - f * 30 * s:.1f} {cy + 30 * s:.1f} C {cx - f * 10 * s:.1f} {cy + 28 * s:.1f} {cx + f * 8 * s:.1f} {cy + 14 * s:.1f} {cx + f * 10 * s:.1f} {cy - 2 * s:.1f} Z", wing)
    out += circle(cx + f * 20 * s, cy - 16 * s, 15 * s, head)
    out += poly([(cx + f * 32 * s, cy - 20 * s), (cx + f * 48 * s, cy - 12 * s), (cx + f * 32 * s, cy - 9 * s)], beak)
    out += eye(cx + f * 24 * s, cy - 19 * s, 3.4 * s, eye_color)
    for lx in (-4, 6):
        out += stroke(f"M{cx + f * lx * s:.1f} {cy + 30 * s:.1f} l0 {12 * s:.1f}", "#c98a2b", 2.5 * s)
    return out


def branch(y, color="#5a3b22"):
    return (stroke(f"M0 {y} C 90 {y - 8} 200 {y + 10} {W} {y - 4}", color, 9)
            + stroke(f"M230 {y + 2} q 30 -20 60 -24", color, 4)
            + ellipse(296, y - 26, 10, 5, "#4f7d3a", f'transform="rotate(-25 296 {y - 26})"'))


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


# ---------------------------------------------------------------- Spider cards

def broodmother():
    defs, body = bg_spider()
    for x, y in ((60, 150), (84, 162), (300, 154), (276, 166), (320, 172)):
        body += ellipse(x, y, 14, 18, "#e8e0f0") + ellipse(x - 4, y - 6, 4, 6, "#ffffff", 'opacity="0.7"')
    body += spider(176, 92, 1.25, "#3a2440", "#5a2c5a", eyes="#ff5a5a", pattern=ellipse(176, 124, 14, 20, "#a04a9a", 'opacity="0.6"'))
    for x, y in ((110, 178), (240, 182), (150, 186)):
        body += spider(x, y, 0.18, "#2a1a30", eyes="#ff7070")
    return svg(defs, body)


def giant_tarantula():
    defs, body = bg_spider(moon=False)
    body += ellipse(176, 182, 150, 18, "#0a0610")
    body += spider(176, 96, 1.4, "#6b4a2e", "#7a5434", legs="#5a3c24", eyes="#111",
                   pattern=ellipse(176, 128, 18, 22, "#c48a52", 'opacity="0.35"'), hairy=True)
    return svg(defs, body)


def shadow_lurker():
    defs, body = bg_spider(moon=False)
    defs += rad("shadow", [("#000000", 0.9), ("#000000", 0)])
    body += ellipse(176, 100, 150, 90, "url(#shadow)")
    body += group(spider(0, 0, 1.3, "#0d0a12", eyes="#ff2e2e"), "translate(176 92)", 0.9)
    for x, y in ((150, 60), (200, 58), (166, 52), (186, 52)):
        body += circle(x, y, 6, "#ff2e2e", 'opacity="0.35"')
    return svg(defs, body)


def silk_spinner():
    defs, body = bg_spider()
    body += stroke("M176 0 L176 54", "#f0ecff", 1.5)
    body += spider(176, 92, 0.95, "#e0d4f0", "#c6b3e6", legs="#b9a6d9", eyes="#3a1f5a")
    for i in range(5):
        r = 18 + i * 10
        body += stroke(f"M{90 - r} 150 A {r} {r * 0.5} 0 1 1 {90 + r} 150", "#f0ecff", 1.2, 'opacity="0.6"')
    body += ellipse(90, 150, 14, 9, "#f6f2ff")
    return svg(defs, body)


def tunnel_spider():
    defs, body = bg_spider(moon=False)
    body += path("M0 120 C 80 112 260 112 352 120 L352 200 L0 200 Z", "#3a2a1c")
    body += ellipse(176, 140, 90, 30, "#120a06") + ellipse(176, 136, 90, 26, "none", 'stroke="#5a4028" stroke-width="5"')
    body += spider(176, 120, 0.9, "#4a3a2a", "#5c4632", legs="#3e3022", eyes="#ffcc3a")
    body += path("M86 140 C 120 168 232 168 266 140 L266 200 L86 200 Z", "#3a2a1c")
    return svg(defs, body)


def venomous_spider():
    defs, body = bg_spider()
    defs += rad("venom", [("#7dff6a", 0.5), ("#7dff6a", 0)])
    body += ellipse(176, 110, 120, 80, "url(#venom)")
    body += spider(176, 90, 1.15, "#1f3a20", "#2e5a2c", legs="#1a3019", eyes="#c6ff3a",
                   pattern="".join(stroke(f"M{160 + i * 8} {112} l0 30", "#7dff6a", 3, 'opacity="0.8"') for i in range(5)))
    for x, y in ((164, 106), (188, 106)):
        body += path(f"M{x} {y} C {x - 4} {y + 10} {x - 4} {y + 14} {x} {y + 16} C {x + 4} {y + 14} {x + 4} {y + 10} {x} {y} Z", "#7dff6a")
    body += path("M176 172 C 168 182 168 188 176 192 C 184 188 184 182 176 172 Z", "#7dff6a")
    return svg(defs, body)


def web_weaver():
    defs, body = bg_spider()
    cx, cy = 176, 100
    for a in range(0, 360, 30):
        r = math.radians(a)
        body += stroke(f"M{cx} {cy} L{cx + 190 * math.cos(r):.1f} {cy + 120 * math.sin(r):.1f}", "#e9e3ff", 1.3, 'opacity="0.8"')
    for ring in range(1, 7):
        pts = []
        for a in range(0, 361, 30):
            r = math.radians(a)
            pts.append((cx + ring * 26 * math.cos(r), cy + ring * 16 * math.sin(r)))
        d = "M" + " L".join(f"{x:.1f} {y:.1f}" for x, y in pts)
        body += stroke(d, "#e9e3ff", 1, 'opacity="0.7"')
    body += spider(cx, cy - 8, 0.8, "#2d2140", "#4b2f6a", eyes="#ff6ad5", pattern=ellipse(cx, cy + 10, 8, 10, "#ff6ad5", 'opacity="0.5"'))
    return svg(defs, body)


def widow_assassin():
    defs, body = bg_spider()
    hourglass = poly([(166, 112), (186, 112), (178, 124), (186, 136), (166, 136), (174, 124)], "#e01e3c")
    body += spider(176, 92, 1.2, "#0b0b10", "#14141c", legs="#09090d", eyes="#ff3a5a", pattern=hourglass)
    body += stroke("M248 150 L 300 98", "#c9ccd6", 4) + poly([(300, 98), (316, 84), (306, 104)], "#e8eaf0")
    return svg(defs, body)


# ---------------------------------------------------------------- Bird cards

def healing_dove():
    defs, body = bg_sky("#bfe6ff", "#fff6dc")
    defs += rad("halo", [("#fffbe0", 0.9), ("#fffbe0", 0)])
    body += circle(176, 92, 90, "url(#halo)")
    body += flying_bird(176, 96, 0.95, "#f7f7fb", "#e8ecf5", "#ffffff", beak="#f2a65a")
    body += stroke("M180 92 q 22 6 40 -2", "#5f8a3a", 2.5)
    for i, x in enumerate((192, 204, 216)):
        body += ellipse(x, 92 + (i % 2) * 2, 6, 3, "#7fb04a", f'transform="rotate(-20 {x} 92)"')
    body += plus(90, 60, 8, "#7ed6a0") + plus(270, 70, 7, "#7ed6a0")
    return svg(defs, body)


def majestic_griffin():
    defs, body = bg_sky("#7ab8e8", "#f7d79a")
    body += path("M120 150 C 130 120 220 116 240 140 C 250 160 236 178 210 180 L 140 180 C 120 178 112 166 120 150 Z", "#c99a4a")
    body += stroke("M124 160 C 96 160 84 140 96 120", "#c99a4a", 6) + circle(96, 118, 7, "#8a5a24")
    for x in (140, 164, 196, 220):
        body += stroke(f"M{x} 176 l 0 16", "#a87a34", 8)
    body += flying_bird(206, 108, 0.8, "#8a5a24", "#a8743a", "#f0e2c0", beak="#f2b134", head="#fbf6ea")
    return svg(defs, body)


def mischievous_raven():
    defs, body = bg_night()
    body += branch(150)
    body += perched_bird(160, 100, 1.4, "#1d1f2b", "#2b2e40", "#2a2c3a", beak="#3a3a44", eye_color="#c03a2a")
    body += circle(232, 88, 7, "#f2c94c", 'stroke="#c9952a" stroke-width="2"')
    body += stroke("M200 72 l 6 -6 M206 78 l 8 -2", "#ffffff", 1.5, 'opacity="0.7"')
    return svg(defs, body)


def phoenix_hatchling():
    defs, body = bg_sky("#ffb36b", "#ffe9a8", sun=False)
    defs += rad("fire", [("#fff3a0", 1), ("#ff8a2a", 0.8), ("#ff3a1a", 0)])
    body += ellipse(176, 110, 110, 90, "url(#fire)")
    for i in range(7):
        x = 110 + i * 22
        body += path(f"M{x} 150 C {x - 12} 120 {x + 6} 100 {x} {80 - (i % 3) * 12} C {x + 16} 104 {x + 14} 130 {x} 150 Z", "#ff7a2a", 'opacity="0.85"')
    body += path("M120 150 C 120 196 232 196 232 150 L 216 140 L 200 152 L 186 138 L 170 152 L 154 140 L 138 152 Z", "#f4ead2", 'stroke="#d8c8a0" stroke-width="2"')
    body += ellipse(176, 118, 26, 24, "#ff9a3a") + circle(176, 92, 16, "#ffb04a")
    body += poly([(170, 96), (182, 96), (176, 106)], "#ffe066") + eye(169, 88, 4) + eye(183, 88, 4)
    for side in (-1, 1):
        body += path(f"M{176 + side * 20} 112 C {176 + side * 40} 100 {176 + side * 46} 120 {176 + side * 30} 132 Z", "#ff6a1a")
    body += path("M168 78 C 166 64 176 58 176 70 C 180 58 190 64 184 78 Z", "#ff5a1a")
    return svg(defs, body)


def royal_falcon():
    defs, body = bg_sky("#9fd2f2", "#f5e2c0")
    body += branch(160, "#6a4a2a")
    body += perched_bird(170, 108, 1.35, "#6b5a4a", "#4a3c30", "#f2e6d0", beak="#3a3a3a", head="#5a4a3c", eye_color="#2a1a00")
    body += poly([(184, 70), (190, 56), (198, 66), (206, 52), (212, 66), (220, 56), (224, 72)], "#f2c94c", 'stroke="#c9952a" stroke-width="1.5"')
    body += circle(206, 64, 2.5, "#e0405a")
    return svg(defs, body)


def thunderhawk():
    defs, body = bg_storm()
    body += flying_bird(176, 100, 1.0, "#2f3b5c", "#3e4f7a", "#c9d4ea", beak="#f2c94c", eye_color="#fff15c")
    body += bolt(160, 120, 0.9) + bolt(200, 118, 0.8)
    return svg(defs, body)


def war_eagle():
    defs, body = bg_sky("#d98a5a", "#f6d8a0")
    body += flying_bird(176, 92, 1.0, "#5a3a22", "#4a2e1a", "#e8d8b8", beak="#f2b134", head="#fbf8f0")
    body += path("M160 64 C 160 52 192 52 192 64 L 192 72 L 160 72 Z", "#9aa3b2", 'stroke="#5c6474" stroke-width="2"')
    body += stroke("M176 54 L176 44", "#c03a2a", 4)
    for side in (-1, 1):
        body += stroke(f"M{176 + side * 60} 172 L {176 - side * 30} 122", "#c9ccd6", 5)
        body += stroke(f"M{176 + side * 50} 158 l {side * 14} -6", "#7a5a2a", 6)
    return svg(defs, body)


def wise_owl():
    defs, body = bg_night()
    body += branch(168, "#4a3424")
    body += ellipse(176, 116, 50, 56, "#8a6a4a")
    body += ellipse(176, 128, 34, 38, "#d9c4a0")
    for i in range(4):
        body += stroke(f"M{160 + i * 10} {116 + (i % 2) * 8} q 4 4 8 0", "#a8865c", 2)
    body += path("M134 70 L 140 46 L 156 66 Z", "#8a6a4a") + path("M218 70 L 212 46 L 196 66 Z", "#8a6a4a")
    body += circle(176, 80, 40, "#8a6a4a")
    body += circle(160, 80, 15, "#f3e6c4") + circle(192, 80, 15, "#f3e6c4")
    body += eye(160, 80, 10, "#f2a51a") + eye(192, 80, 10, "#f2a51a")
    body += poly([(170, 94), (182, 94), (176, 106)], "#e0a030")
    for x in (162, 190):
        body += stroke(f"M{x} 166 l -4 8 M{x} 166 l 0 9 M{x} 166 l 4 8", "#e0a030", 2.5)
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


def gig_courier():
    defs, body = bg_office()
    body += rect(196, 82, 76, 70, "#2ec4b6", 'rx="8" stroke="#1a8a80" stroke-width="3"')
    body += circle(234, 117, 14, "#ffffff", 'opacity="0.8"') + poly(star_points(234, 117, 10, 4), "#2ec4b6")
    body += person(176, 200, 1.5, SKIN[2], "#2ec4b6", face="flat", hat="cap", brows="sad")
    body += speed_lines(110, 110, 4, 60)
    for i in range(5):
        body += poly(star_points(40 + i * 22, 40, 9, 4), "#f2c94c")
    return svg(defs, body)


def headhunter():
    defs, body = bg_office()
    body += stroke("M150 60 L300 20", "#5a3a22", 5) + stroke("M300 20 L300 112", "#dddddd", 1.5)
    body += stroke("M300 112 q 0 14 -10 12", "#bbbbbb", 3)
    body += rect(282, 112, 34, 44, "#ffffff", 'stroke="#555" stroke-width="2"')
    for y in (122, 130, 138, 146):
        body += stroke(f"M288 {y} L310 {y}", "#999", 2)
    body += person(140, 200, 1.5, SKIN[0], "#2b2d42", face="grin", hair="#d8b04a", tie="#e63946")
    return svg(defs, body)


def middle_manager():
    defs, body = bg_office()
    body += person(150, 200, 1.6, SKIN[0], "#6c7a89", face="smirk", hair="#6a4a2a", tie="#264653", glasses="clear")
    # Pie chart: it's all one slice
    body += circle(268, 90, 40, "#2a9d8f", 'stroke="#fff" stroke-width="3"')
    body += path("M268 90 L268 50 A 40 40 0 0 1 304 72 Z", "#e9c46a", 'stroke="#fff" stroke-width="3"')
    body += rect(244, 140, 32, 30, "#ffffff", 'rx="4"') + stroke("M276 148 q 12 6 0 14", "#ffffff", 4)
    return svg(defs, body)


def venture_capitalist():
    defs, body = bg_office("up")
    body += person(150, 200, 1.6, SKIN[0], "#a8c8e8", face="smirk", hair="#b0a080")
    body += path("M96 200 C 96 166 120 152 138 150 L 150 200 Z M204 200 C 204 166 180 152 162 150 L 150 200 Z", "#1d2b4a")
    for i in range(6):
        body += coin(262 + (i % 2) * 4, 168 - i * 10, 14)
    body += coin(300, 160, 12) + coin(310, 140, 10)
    return svg(defs, body)


def unicorn_startup():
    defs, body = bg_office("down")
    for i, color in enumerate(("#ff5a5a", "#ffb03a", "#ffe45a", "#5ad07a", "#5aaaff", "#9a6aff")):
        body += stroke(f"M120 {150 - i * 8} C 100 {110 - i * 10} 130 {70 - i * 6} 150 {60 - i * 4}", color, 7)
    body += path("M150 170 C 140 120 150 80 190 66 C 230 56 262 76 268 100 C 272 116 256 122 240 116 C 226 112 214 120 214 136 L 216 170 Z", "#fbf6ff", 'stroke="#d0c4e8" stroke-width="3"')
    body += poly([(198, 70), (226, 18), (214, 74)], "#f2c94c", 'stroke="#c9952a" stroke-width="2"')
    body += eye(232, 88, 5) + circle(256, 106, 2.5, "#9a8ab8")
    body += flames(196, 196, 6, 22, 36)
    return svg(defs, body)


def golden_parachute_ceo():
    defs, body = bg_office("down")
    defs += lin("chute", ["#ffe680", "#d9a020"])
    body += path("M86 70 C 86 10 266 10 266 70 C 246 58 226 58 206 70 C 186 58 166 58 146 70 C 126 58 106 58 86 70 Z", "url(#chute)", 'stroke="#b8860b" stroke-width="2"')
    for x in (86, 146, 206, 266):
        body += stroke(f"M{x} 70 L176 128", "#8a6a2a", 1.5)
    body += person(176, 196, 1.2, SKIN[0], "#1a1a1a", face="smirk", hair="#cfcfcf", tie="#d4af37", glasses="shades")
    return svg(defs, body)


def layoffs():
    defs, body = bg_office("down")
    body += path("M110 112 L242 112 L230 186 L122 186 Z", "#c8955a", 'stroke="#8a5a2a" stroke-width="3"')
    body += path("M110 112 L92 92 L224 92 L242 112 Z", "#dcae74", 'stroke="#8a5a2a" stroke-width="3"')
    body += rect(198, 70, 24, 34, "#ffffff", 'rx="3"') + stroke("M222 80 q 10 6 0 14", "#ffffff", 4)
    body += path("M140 104 C 120 60 150 50 150 104 Z", "#5aa04a") + path("M150 104 C 160 50 186 64 160 104 Z", "#6ab85a")
    body += rect(140, 98, 22, 16, "#b0603a")
    body += rect(270, 60, 60, 112, "#3a4a5a", 'stroke="#222" stroke-width="3"') + circle(318, 120, 4, "#f2c94c")
    return svg(defs, body)


def team_building_retreat():
    defs, body = bg_office()
    for i in range(9):
        x = 20 + i * 40
        body += poly([(x, 20), (x + 36, 20), (x + 18, 42)], ("#e63946", "#f2c94c", "#2a9d8f")[i % 3])
    body += stroke("M10 20 L342 20", "#ffffff", 2)
    for i, (x, sk) in enumerate(((96, SKIN[0]), (176, SKIN[2]), (256, SKIN[1]))):
        body += person(x, 200, 1.15, sk, ("#e76f51", "#457b9d", "#8ab17d")[i], face="flat", brows="sad")
        body += poly([(x - 12, 102), (x + 12, 102), (x, 72)], ("#f2c94c", "#e63946", "#2a9d8f")[i])
    return svg(defs, body)


def trickle_down_economics():
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
    return svg(defs, body)


def performance_review():
    defs, body = bg_office("down")
    body += rect(112, 28, 128, 160, "#7a5a3a", 'rx="8"') + rect(122, 44, 108, 136, "#ffffff")
    body += rect(150, 20, 52, 18, "#b0b0b0", 'rx="4"')
    for i in range(5):
        body += poly(star_points(138 + i * 19, 66, 9, 4), "#f2c94c" if i == 0 else "#dddddd")
    for y in (92, 104, 116):
        body += stroke(f"M134 {y} L218 {y}", "#bbbbbb", 3)
    body += arrow(176, 128, 176, 160, "#e63946", 10)
    return svg(defs, body)


def paywall():
    defs, body = bg_office()
    for row in range(7):
        off = 0 if row % 2 == 0 else 24
        for col in range(-1, 8):
            body += rect(off + col * 48 + 2, 40 + row * 22, 44, 18, "#a8483a", 'stroke="#6a2a1a" stroke-width="2"')
    body += rect(146, 92, 60, 52, "#f2c94c", 'rx="6" stroke="#a87a1a" stroke-width="3"')
    body += path("M158 92 L158 74 C 158 52 194 52 194 74 L194 92", "none", 'stroke="#b0b0b0" stroke-width="8"')
    body += rect(166, 110, 20, 5, "#3a2a0a", 'rx="2"')
    return svg(defs, body)


def quarterly_earnings_call():
    defs, body = bg_office("up")
    for i, h in enumerate((40, 62, 54, 90, 120)):
        body += rect(86 + i * 38, 180 - h, 28, h, "#5fe08a", 'stroke="#1f7a4a" stroke-width="2"')
    body += arrow(80, 150, 270, 46, "#ffffff", 7)
    for i in range(14):
        body += rect((i * 53) % W, 14 + (i * 29) % 60, 6, 10, ("#ff5a5a", "#f2c94c", "#5aaaff")[i % 3], f'transform="rotate({i * 25} {(i * 53) % W} {14 + (i * 29) % 60})"')
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


# ---------------------------------------------------------------- Statecraft cards

def conscript():
    defs, body = bg_state()
    body += person(176, 200, 1.6, SKIN[0], "#6b7445", face="wavy", hat="helmet", brows="sad")
    body += sweat(212, 80, 1.3) + sweat(140, 90, 1.0)
    return svg(defs, body)


def border_guard():
    defs, body = bg_state()
    body += rect(40, 112, 280, 12, "#ffffff", 'stroke="#333" stroke-width="2"')
    for i in range(7):
        body += rect(48 + i * 40, 112, 20, 12, "#c0392b")
    body += rect(30, 100, 16, 80, "#555555")
    body += person(176, 200, 1.5, SKIN[1], "#3e4f6a", face="frown", hat="peaked", brows="angry")
    body += rect(230, 132, 24, 32, "#f4e4c0", 'stroke="#555" stroke-width="2"') + rect(234, 138, 10, 10, "#c0392b")
    return svg(defs, body)


def tax_collector():
    defs, body = bg_state("#5a6a3a")
    body += person(150, 200, 1.5, SKIN[0], "#2b2b2b", face="smirk", hat="tophat", glasses="clear")
    body += path("M236 180 C 206 180 206 132 236 122 L 228 108 L 252 108 L 244 122 C 274 132 274 180 244 180 Z", "#c8a060", 'stroke="#8a6a2a" stroke-width="3"')
    for x, y in ((290, 172), (304, 160), (286, 150)):
        body += coin(x, y, 9)
    return svg(defs, body)


def blitz_sergeant():
    defs, body = bg_state()
    body += speed_lines(110, 100, 5, 80, "#4a4f2e")
    body += person(176, 200, 1.6, SKIN[2], "#55603a", face="shout", hat="helmet", brows="angry")
    body += stroke("M210 160 L300 132", "#2b2b2b", 9) + poly(star_points(312, 128, 18, 7, 8), "#ffd84a")
    return svg(defs, body)


def propaganda_minister():
    defs, body = bg_state()
    body += person(130, 200, 1.5, SKIN[0], "#8a1e1e", face="grin", hair="#2b2b2b")
    body += path("M160 120 L230 90 L230 160 L160 130 Z", "#d0d0d0", 'stroke="#555" stroke-width="3"') + rect(150, 118, 14, 14, "#555555")
    for i in range(3):
        body += path(f"M{250 + i * 18} {90 - i * 6} Q {270 + i * 20} 125 {250 + i * 18} {160 + i * 6}", "none", 'stroke="#8a1e1e" stroke-width="4"')
    return svg(defs, body)


def public_hospital():
    defs, body = bg_state("#3a7a6a")
    body += rect(196, 50, 130, 122, "#f4f4f4", 'stroke="#888" stroke-width="3"')
    body += plus(261, 86, 18, "#d0342c")
    body += rect(250, 132, 22, 40, "#7a8a9a")
    for i in range(8):
        x = 20 + i * 22
        body += circle(x, 150, 7, SKIN[i % 4]) + path(f"M{x - 9} 172 C {x - 9} 156 {x + 9} 156 {x + 9} 172 Z", ("#6c7a89", "#8ab17d", "#e76f51")[i % 3])
    body += circle(60, 60, 26, "#ffffff", 'stroke="#333" stroke-width="3"') + stroke("M60 60 L60 42 M60 60 L74 66", "#333", 3)
    return svg(defs, body)


def eternal_senator():
    defs, body = bg_state("#2a3a8a")
    body += person(170, 200, 1.5, "#e8c8b0", "#2a2a3a", face="closed", hair=OLD_HAIR, tie="#2a3a8a", glasses="clear")
    body += rect(96, 150, 150, 50, "#7a5a3a", 'stroke="#4a3a2a" stroke-width="3"')
    body += zzz(222, 74, 1.2, "#2a2a3a")
    return svg(defs, body)


def main_battle_tank():
    defs, body = bg_state()
    body += path("M52 140 L300 140 L286 178 L66 178 Z", "#4a5530", 'stroke="#2a3018" stroke-width="3"')
    for i in range(7):
        body += circle(84 + i * 31, 164, 11, "#2a3018") + circle(84 + i * 31, 164, 4, "#7a8558")
    body += path("M76 140 L96 112 L256 112 L276 140 Z", "#5d6a3c", 'stroke="#2a3018" stroke-width="3"')
    body += path("M130 112 C 130 80 210 80 210 112 Z", "#6b7845", 'stroke="#2a3018" stroke-width="3"')
    body += rect(204, 90, 120, 10, "#3e4728")
    body += poly(star_points(170, 124, 9, 4), "#e3d3a4")
    return svg(defs, body)


def red_tape():
    defs, body = bg_state("#7a6a4a")
    for i in range(5):
        body += rect(116 + (i % 2) * 6, 172 - i * 22, 120, 20, "#f4ecd8", 'stroke="#a89878" stroke-width="2"')
    body += path("M90 60 C 160 20 220 160 270 80 C 300 40 230 30 200 90 C 170 150 100 150 120 100 C 140 50 250 140 280 150", "none", 'stroke="#d0342c" stroke-width="7"')
    body += rect(240, 34, 40, 28, "#6a4a2a", 'rx="4"') + rect(246, 62, 28, 10, "#d0342c")
    return svg(defs, body)


def artillery_barrage():
    defs, body = bg_state()
    for x, y in ((90, 40), (180, 20), (270, 50)):
        body += ellipse(x, y, 8, 18, "#3e4728", f'transform="rotate(20 {x} {y})"')
        body += stroke(f"M{x - 12} {y - 34} l -14 -30", "#ffffff", 2, 'opacity="0.6"')
    for x in (70, 176, 280):
        body += poly(star_points(x, 168, 40, 16, 10), "#ff8a2a") + poly(star_points(x, 168, 22, 10, 10), "#ffe45a")
    return svg(defs, body)


def curfew():
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


def conscription():
    defs, body = bg_state()
    body += rect(86, 56, 180, 116, "#f4ecd8", 'stroke="#8a7a5a" stroke-width="3"')
    body += path("M86 56 L176 120 L266 56", "none", 'stroke="#8a7a5a" stroke-width="3"')
    body += circle(176, 120, 20, "#b8322a") + poly(star_points(176, 120, 12, 5), "#f2c94c")
    body += path("M290 110 L330 110 L330 124 L290 124 Z", SKIN[0]) + path("M270 104 C 270 96 290 96 290 104 L 290 130 L 270 130 Z", "#2a3a8a")
    return svg(defs, body)


def preemptive_strike():
    defs, body = bg_state()
    body += circle(240, 100, 54, "none", 'stroke="#b8322a" stroke-width="6"') + circle(240, 100, 30, "none", 'stroke="#b8322a" stroke-width="5"')
    body += stroke("M240 32 L240 168 M172 100 L308 100", "#b8322a", 3)
    body += path("M40 150 L130 110 L140 120 L50 160 Z", "#d0d0d0", 'stroke="#555" stroke-width="2"')
    body += poly([(130, 110), (156, 104), (140, 120)], "#b8322a") + flames(36, 168, 1, 0, 26)
    return svg(defs, body)


def austerity_measures():
    defs, body = bg_state()
    body += ellipse(176, 120, 80, 56, "#f4a6b8", 'stroke="#c06a80" stroke-width="3"')
    body += circle(240, 100, 26, "#f4a6b8", 'stroke="#c06a80" stroke-width="3"') + ellipse(260, 106, 10, 8, "#e08aa0")
    body += eye(236, 92, 4) + rect(160, 64, 30, 6, "#333")
    for x in (126, 214):
        body += rect(x, 166, 14, 20, "#e08aa0")
    body += path("M136 70 Q 144 120 136 172 L 160 172 Q 168 120 160 66 Z", "#5a3a22")
    body += rect(140, 106, 18, 22, "none", 'stroke="#c9c9c9" stroke-width="4"')
    return svg(defs, body)


def state_of_emergency():
    defs, body = bg_state("#d0342c")
    body += rect(146, 120, 60, 18, "#555555", 'rx="4"')
    body += path("M152 120 C 152 76 200 76 200 120 Z", "#ff3a2a", 'stroke="#8a1a10" stroke-width="3"')
    body += ellipse(166, 98, 6, 10, "#ffb0a0", 'opacity="0.8"')
    for a in range(-160, -10, 25):
        r = math.radians(a)
        body += stroke(f"M{176 + 46 * math.cos(r):.1f} {110 + 46 * math.sin(r):.1f} L{176 + 76 * math.cos(r):.1f} {110 + 76 * math.sin(r):.1f}", "#ff3a2a", 5)
    body += icon_shield_small(60, 120) + icon_shield_small(292, 120)
    return svg(defs, body)


def icon_shield_small(cx, cy):
    return path(f"M{cx} {cy - 30} C {cx + 14} {cy - 24} {cx + 24} {cy - 24} {cx + 26} {cy - 20} C {cx + 26} {cy + 6} {cx + 14} {cy + 22} {cx} {cy + 32} C {cx - 14} {cy + 22} {cx - 26} {cy + 6} {cx - 26} {cy - 20} C {cx - 24} {cy - 24} {cx - 14} {cy - 24} {cx} {cy - 30} Z", "#4f8fd6", 'stroke="#1b3b66" stroke-width="3"')


# ---------------------------------------------------------------- Zeitgeist cards

def doomscroller():
    defs, body = bg_vapor()
    defs += rad("glow", [("#8fe8ff", 0.7), ("#8fe8ff", 0)])
    body += ellipse(176, 110, 90, 70, "url(#glow)")
    body += person(176, 200, 1.6, "#bfe0f0", "#3a3a5a", face="flat", hair="#2a1a3a")
    body += ellipse(165, 90, 5, 2, "#7a6a9a") + ellipse(187, 90, 5, 2, "#7a6a9a")
    body += rect(150, 140, 52, 60, "#111", 'rx="6"') + rect(155, 146, 42, 48, "#8fe8ff")
    return svg(defs, body)


def wojak():
    defs, body = bg_vapor()
    body += flames(176, 200, 9, 40, 50)
    body += path("M126 200 C 126 160 226 160 226 200 Z", "#3a3a3a")
    body += ellipse(176, 104, 40, 50, "#f0f0f0", 'stroke="#222" stroke-width="2.5"')
    body += stroke("M150 92 L166 98 M202 92 L186 98", "#222", 2.5)
    body += circle(160, 104, 2.5, "#222") + circle(192, 104, 2.5, "#222")
    body += stroke("M166 134 Q 176 138 186 132", "#222", 2)
    body += stroke("M154 112 q 4 10 2 16", "#8fd8ff", 2.5)
    return svg(defs, body)


def grindset_bro():
    defs, body = bg_vapor()
    body += person(176, 200, 1.6, SKIN[1], "#1a1a1a", face="grin", hat="headband", brows="angry")
    body += stroke("M96 70 L256 70", "#888888", 6)
    for x in (92, 260):
        body += rect(x - 8, 50, 16, 40, "#333333", 'rx="3"')
    body += stroke("M118 150 L104 76 M234 150 L248 76", SKIN[1], 12)
    body += rect(286, 120, 24, 50, "#5aff7a", 'rx="4" stroke="#1a1a1a" stroke-width="2"') + bolt(292, 128, 0.8, "#1a1a1a")
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


def main_character():
    defs, body = bg_vapor()
    defs += lin("spot", [("#fffbe0", 0.7), ("#fffbe0", 0.05)])
    body += poly([(156, 0), (196, 0), (256, 200), (96, 200)], "url(#spot)")
    for x in (60, 100, 252, 292):
        body += person(x, 200, 0.8, "#7a6a9a", "#4a3a6a", face="flat")
    body += person(176, 200, 1.6, SKIN[2], "#ff3a8a", face="smirk", hair="#1a1a1a", glasses="shades")
    for x, y, r in ((120, 40, 10), (240, 60, 8), (220, 24, 6)):
        body += poly(star_points(x, y, r, r * 0.35, 4), "#ffffff")
    return svg(defs, body)


def quiet_quitter():
    defs, body = bg_vapor()
    body += path("M70 120 L120 150 L230 150 L230 170 L110 170 L60 136 Z", "#3a3a5a")
    body += person(110, 168, 1.3, SKIN[0], "#8ab17d", face="closed", hair="#6a4a2a")
    body += path("M150 150 L270 136 L274 150 L150 164 Z", "#2a4a6a") + rect(266, 128, 20, 14, "#222", 'rx="3"')
    body += zzz(150, 60, 1.3)
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


def rage_quit():
    defs, body = bg_vapor()
    body += path("M96 110 C 96 90 256 90 256 110 L 270 160 C 274 176 240 180 232 160 L 220 140 L 132 140 L 120 160 C 112 180 78 176 82 160 Z", "#2a2a3a", 'stroke="#111" stroke-width="3"')
    body += path("M176 92 L168 116 L184 124 L174 150", "none", 'stroke="#ff4f6a" stroke-width="4"')
    body += circle(226, 112, 6, "#ff4f6a") + circle(240, 124, 6, "#5aaaff") + plus(126, 118, 9, "#888")
    for x, y in ((80, 60), (276, 54)):
        body += stroke(f"M{x - 12} {y - 4} l 8 4 l -8 4 M{x + 12} {y - 4} l -8 4 l 8 4", "#ff2a4a", 4)
    return svg(defs, body)


def existential_dread():
    defs = rad("bg", ["#000000", "#22104a", "#ff7ad9"], r=0.8)
    body = rect(0, 0, W, H, "url(#bg)")
    for i in range(6):
        body += ellipse(176, 90, 150 - i * 22, 60 - i * 8, "none", f'stroke="#b07aff" stroke-width="1.5" opacity="{0.2 + i * 0.1:.1f}"')
    body += ellipse(176, 90, 36, 22, "#ffffff") + circle(176, 90, 14, "#000000")
    body += person(176, 200, 0.6, "#e0d0f0", "#4a3a6a", face="frown")
    return svg(defs, body)


def touch_grass():
    defs, body = bg_vapor()
    body += rect(0, 150, W, 50, "#3aa04a")
    for i in range(40):
        x = 4 + i * 9
        body += poly([(x, 152), (x + 4, 130 - (i * 7) % 16), (x + 8, 152)], "#5ad07a")
    body += path("M150 40 C 150 70 160 110 176 130 L 196 130 C 200 110 196 70 196 40 Z", SKIN[0], 'stroke="#b88a6a" stroke-width="2"')
    for i, x in enumerate((178, 186, 194)):
        body += rect(x - 4, 116 + i, 8, 26, SKIN[0], 'rx="4" stroke="#b88a6a" stroke-width="2"')
    return svg(defs, body)


def general_strike():
    defs, body = bg_vapor()
    for x, top, color in ((90, 30, "#fff7e0"), (176, 16, "#ffe0f0"), (262, 34, "#e0f0ff")):
        body += sign(x, top, 70, 40, color)
        body += stroke(f"M{x - 20} {top + 14} L{x + 20} {top + 26} M{x + 20} {top + 14} L{x - 20} {top + 26}", "#d0342c", 5)
    for i, x in enumerate((60, 130, 200, 290)):
        body += person(x, 200, 0.9, SKIN[i], ("#e76f51", "#457b9d", "#8ab17d", "#f2c94c")[i], face="shout", brows="angry")
    return svg(defs, body)


def retail_therapy():
    defs, body = bg_vapor()
    for x, color, h in ((70, "#ff5fa2", 80), (126, "#5aaaff", 96), (190, "#f2c94c", 70)):
        body += rect(x, 180 - h, 52, h, color, 'stroke="#222" stroke-width="2"')
        body += path(f"M{x + 14} {180 - h} C {x + 14} {160 - h} {x + 38} {160 - h} {x + 38} {180 - h}", "none", 'stroke="#222" stroke-width="3"')
    body += rect(244, 90, 86, 54, "#e8e8e8", 'rx="6" stroke="#555" stroke-width="2"') + rect(244, 102, 86, 10, "#333")
    body += path("M270 90 L282 116 L272 124 L288 144", "none", 'stroke="#d0342c" stroke-width="3"')
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
    "Spiders": {
        "Broodmother": broodmother, "GiantTarantula": giant_tarantula, "ShadowLurker": shadow_lurker,
        "SilkSpinner": silk_spinner, "TunnelSpider": tunnel_spider, "VenomousSpider": venomous_spider,
        "WebWeaver": web_weaver, "WidowAssassin": widow_assassin,
    },
    "Birds": {
        "HealingDove": healing_dove, "MajesticGriffin": majestic_griffin, "MischievousRaven": mischievous_raven,
        "PhoenixHatchling": phoenix_hatchling, "RoyalFalcon": royal_falcon, "Thunderhawk": thunderhawk,
        "WarEagle": war_eagle, "WiseOwl": wise_owl,
    },
    "Corporate": {
        "UnpaidIntern": unpaid_intern, "GigCourier": gig_courier, "Headhunter": headhunter,
        "MiddleManager": middle_manager, "VentureCapitalist": venture_capitalist, "UnicornStartup": unicorn_startup,
        "GoldenParachuteCEO": golden_parachute_ceo, "Layoffs": layoffs, "TeamBuildingRetreat": team_building_retreat,
        "TrickleDownEconomics": trickle_down_economics, "PerformanceReview": performance_review, "Paywall": paywall,
        "QuarterlyEarningsCall": quarterly_earnings_call, "HostileTakeover": hostile_takeover,
    },
    "Statecraft": {
        "Conscript": conscript, "BorderGuard": border_guard, "TaxCollector": tax_collector,
        "BlitzSergeant": blitz_sergeant, "PropagandaMinister": propaganda_minister, "PublicHospital": public_hospital,
        "EternalSenator": eternal_senator, "MainBattleTank": main_battle_tank, "RedTape": red_tape,
        "ArtilleryBarrage": artillery_barrage, "Curfew": curfew, "Conscription": conscription,
        "PreemptiveStrike": preemptive_strike, "AusterityMeasures": austerity_measures, "StateOfEmergency": state_of_emergency,
    },
    "Zeitgeist": {
        "Doomscroller": doomscroller, "Wojak": wojak, "GrindsetBro": grindset_bro,
        "Hipster": hipster, "Underdog": underdog, "MainCharacter": main_character,
        "QuietQuitter": quiet_quitter, "YOLO": yolo, "RageQuit": rage_quit,
        "ExistentialDread": existential_dread, "TouchGrass": touch_grass, "GeneralStrike": general_strike,
        "RetailTherapy": retail_therapy,
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


if __name__ == "__main__":
    main()
