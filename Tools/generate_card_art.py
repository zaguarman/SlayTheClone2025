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
