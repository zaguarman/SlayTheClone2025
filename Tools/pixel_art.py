"""Pixel art card illustrations (samples): each picture is a background layer (one per faction or scene, drawn once and
reused) and a subject layer over it, drawn at 192 x 108 pixels in a fixed palette (Endesga 32), outlined and shaded,
then scaled up 6x with hard edges so it stays crisp.

Output: Assets/Art/PixelSamples/<Card>.png (the card, scaled), and Layers/ with each background and subject at 1x.
Usage: python Tools/pixel_art.py
"""
import math
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

W, H = 192, 108
SCALE = 6
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Art", "PixelSamples")

# Endesga 32
HEX = ["be4a2f", "d77643", "ead4aa", "e4a672", "b86f50", "733e39", "3e2731", "a22633", "e43b44", "f77622", "feae34",
       "fee761", "63c74d", "3e8948", "265c42", "193c3e", "124e89", "0099db", "2ce8f5", "ffffff", "c0cbdc", "8b9bb4",
       "5a6988", "3a4466", "262b44", "181425", "ff0044", "68386c", "b55088", "f6757a", "e8b796", "c28569"]
PALETTE = [tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) for h in HEX]
C = dict(rust=PALETTE[0], orange_brown=PALETTE[1], sand=PALETTE[2], tan=PALETTE[3], brown=PALETTE[4], dark_brown=PALETTE[5],
         plum=PALETTE[6], wine=PALETTE[7], red=PALETTE[8], orange=PALETTE[9], amber=PALETTE[10], yellow=PALETTE[11],
         green=PALETTE[12], dark_green=PALETTE[13], forest=PALETTE[14], teal_dark=PALETTE[15], blue=PALETTE[16],
         sky=PALETTE[17], cyan=PALETTE[18], white=PALETTE[19], silver=PALETTE[20], grey=PALETTE[21], slate=PALETTE[22],
         navy=PALETTE[23], night=PALETTE[24], ink=PALETTE[25], hot=PALETTE[26], purple=PALETTE[27], magenta=PALETTE[28],
         pink=PALETTE[29], skin=PALETTE[30], skin_dark=PALETTE[31])
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


# ---------------------------------------------------------------- helpers

def layer():
    return Image.new("RGBA", (W, H), (0, 0, 0, 0))


def mask():
    return Image.new("L", (W, H), 0)


def shifted(m, dx, dy):
    out = mask()
    out.paste(m, (dx, dy))
    return out


def fill(img, m, color):
    img.paste(color + (255,), (0, 0), m)


def shape(img, draw, base, dark=None, light=None, shadow_side=(1, 1), depth=2):
    """Draws a shape (draw(ImageDraw, 255) on a mask) in its base color, a shadow rim on one side and a light rim on the
    other, the way a pixel artist shades a form lit from the top left."""
    m = mask()
    draw(ImageDraw.Draw(m), 255)
    fill(img, m, base)
    sx, sy = shadow_side
    if dark:
        rim = ImageChops.subtract(m, shifted(m, sx * depth, sy * depth))
        fill(img, rim, dark)
    if light:
        rim = ImageChops.subtract(m, shifted(m, -sx, -sy))
        fill(img, rim, light)
    return m


def outline(img, color=None):
    """A 1-pixel outline round everything drawn on the layer (4-neighbour, the classic pixel-art outline)."""
    color = color or C["ink"]
    alpha = img.split()[3].point(lambda v: 255 if v > 0 else 0)
    cross = Image.new("L", (W, H), 0)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        cross = ImageChops.lighter(cross, shifted(alpha, dx, dy))
    edge = ImageChops.subtract(cross, alpha)
    out = layer()
    fill(out, edge, color)
    out.alpha_composite(img)
    return out


def gradient(img, colors, y0=0, y1=H, x0=0, x1=W):
    """A vertical gradient through the palette colors, ordered-dithered between bands."""
    px = img.load()
    span = max(1, y1 - y0 - 1)
    for y in range(y0, y1):
        t = (y - y0) / span * (len(colors) - 1)
        i = min(int(t), len(colors) - 2) if len(colors) > 1 else 0
        f = t - i
        for x in range(x0, x1):
            c = colors[i + 1] if len(colors) > 1 and f * 16 > BAYER[y % 4][x % 4] else colors[i]
            px[x, y] = c + (255,)


def dot(img, x, y, color):
    if 0 <= x < W and 0 <= y < H:
        img.putpixel((int(x), int(y)), color + (255,))


def save(name, background, subject):
    layers = os.path.join(OUT, "Layers")
    os.makedirs(layers, exist_ok=True)
    subject.save(os.path.join(layers, f"{name}_subject.png"))
    card = background.copy()
    card.alpha_composite(subject)
    card.convert("RGB").resize((W * SCALE, H * SCALE), Image.NEAREST).save(os.path.join(OUT, f"{name}.png"))


# ---------------------------------------------------------------- backgrounds (drawn once, reused)

def bg_office():
    """Corporate: the skyline at dusk, lit windows."""
    img = layer()
    gradient(img, [C["night"], C["navy"], C["purple"], C["magenta"]], 0, 80)
    rnd = random.Random(3)
    d = ImageDraw.Draw(img)
    x = 0
    while x < W:
        w, h = rnd.randint(12, 24), rnd.randint(30, 70)
        top = 96 - h
        d.rectangle([x, top, x + w - 1, H], fill=C["teal_dark"] + (255,))
        d.line([x, top, x + w - 1, top], fill=C["slate"] + (255,))
        for wy in range(top + 4, 92, 5):
            for wx in range(x + 3, x + w - 3, 4):
                if rnd.random() < 0.45:
                    d.rectangle([wx, wy, wx + 1, wy + 1], fill=(C["amber"] if rnd.random() < 0.7 else C["yellow"]) + (255,))
        x += w + rnd.randint(1, 3)
    d.rectangle([0, 96, W, H], fill=C["ink"] + (255,))
    return img


def bg_temple():
    """Myth: temple light, gold through a purple dusk, between two columns."""
    img = layer()
    gradient(img, [C["purple"], C["magenta"], C["orange"], C["amber"]], 0, H)
    d = ImageDraw.Draw(img)
    for r, col in ((46, C["amber"]), (36, C["yellow"])):
        m = mask()
        ImageDraw.Draw(m).ellipse([96 - r, 40 - r, 96 + r, 40 + r], fill=255)
        px, mp = img.load(), m.load()
        for y in range(H):
            for x in range(W):
                if mp[x, y] and BAYER[y % 4][x % 4] < 7:
                    px[x, y] = col + (255,)
    for x0 in (8, 168):
        shape(img, lambda dr, v: dr.rectangle([x0, 18, x0 + 15, H], fill=v), C["sand"], C["tan"], C["white"])
        d.rectangle([x0 - 3, 13, x0 + 18, 18], fill=C["sand"] + (255,))
        d.line([x0 - 3, 18, x0 + 18, 18], fill=C["tan"] + (255,))
        for gx in range(x0 + 3, x0 + 14, 4):
            d.line([gx, 21, gx, H], fill=C["tan"] + (255,))
    d.rectangle([0, 98, W, H], fill=C["dark_brown"] + (255,))
    return img


def bg_state_sea():
    """Statecraft by the sea: a parade-ground sunburst over the water."""
    img = layer()
    gradient(img, [C["tan"], C["sand"]], 0, 70)
    px = img.load()
    for y in range(70):
        for x in range(W):
            a = math.atan2(y - 70, x - 96)
            if int((a + math.pi) / (math.pi / 14)) % 2 == 0 and BAYER[y % 4][x % 4] < 9:
                px[x, y] = C["sand"] + (255,) if (x + y) % 2 else C["white"] + (255,)
    gradient(img, [C["sky"], C["blue"], C["navy"]], 70, H)
    d = ImageDraw.Draw(img)
    rnd = random.Random(5)
    for _ in range(40):
        x, y = rnd.randint(0, W - 6), rnd.randint(72, H - 2)
        d.line([x, y, x + rnd.randint(2, 5), y], fill=C["cyan"] + (255,))
    return img


def bg_circuit():
    """Tech: a dark circuit board, traces and solder points."""
    img = layer()
    gradient(img, [C["teal_dark"], C["forest"], C["teal_dark"]], 0, H)
    d = ImageDraw.Draw(img)
    rnd = random.Random(7)
    for _ in range(26):
        x, y = rnd.randrange(0, W, 4), rnd.randrange(0, H, 4)
        for _ in range(rnd.randint(2, 5)):
            nx, ny = (x + rnd.choice((-16, 16, 0)), y) if rnd.random() < 0.5 else (x, y + rnd.choice((-12, 12)))
            d.line([x, y, nx, ny], fill=C["dark_green"] + (255,))
            x, y = nx, ny
        d.rectangle([x - 1, y - 1, x + 1, y + 1], fill=C["green"] + (255,))
    return img


def bg_vapor():
    """Digital Society and Zeitgeist: a vaporwave sunset over a neon grid."""
    img = layer()
    gradient(img, [C["purple"], C["magenta"], C["pink"]], 0, 66)
    d = ImageDraw.Draw(img)
    m = mask()
    ImageDraw.Draw(m).ellipse([66, 14, 126, 74], fill=255)
    sun = layer()
    gradient(sun, [C["yellow"], C["amber"], C["orange"], C["hot"]], 14, 66)
    stripes = mask()
    sd = ImageDraw.Draw(stripes)
    for i, y in enumerate(range(44, 66, 5)):
        sd.rectangle([0, y, W, y + 1 + i // 2], fill=255)
    m = ImageChops.subtract(m, stripes)
    m = ImageChops.multiply(m, Image.new("L", (W, H), 0).point(lambda v: 255).crop((0, 0, W, H)))
    img.paste(sun, (0, 0), ImageChops.multiply(m, Image.eval(m, lambda v: 255 if v else 0)))
    d.rectangle([0, 66, W, H], fill=C["night"] + (255,))
    for x in range(-200, W + 200, 16):
        d.line([96, 66, x, H], fill=C["magenta"] + (255,))
    for i, y in enumerate((68, 71, 75, 80, 87, 96, 107)):
        d.line([0, y, W, y], fill=C["magenta"] + (255,))
    return img


def bg_fallout():
    """A burning sky over a dead plain."""
    img = layer()
    gradient(img, [C["wine"], C["rust"], C["orange"], C["amber"]], 0, 92)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 92, W, H], fill=C["plum"] + (255,))
    d.line([0, 92, W, 92], fill=C["dark_brown"] + (255,))
    return img


# ---------------------------------------------------------------- subjects

def flame(img, cx, base_y, height, width, rnd):
    for i in range(3):
        col, dark = [(C["red"], C["wine"]), (C["orange"], C["rust"]), (C["yellow"], C["amber"])][i]
        h, w = height * (1 - i * 0.28), width * (1 - i * 0.3)
        sway = rnd.uniform(-2, 2)
        shape(img, lambda d, v: d.polygon([(cx - w / 2, base_y), (cx + sway - w / 4, base_y - h * 0.55), (cx + sway, base_y - h),
                                           (cx + sway + w / 4, base_y - h * 0.5), (cx + w / 2, base_y)], fill=v), col, dark)


def hostile_takeover():
    img = layer()
    # The shark, swimming right through the skyline, grinning
    body = [(26, 60), (60, 44), (110, 40), (150, 48), (172, 58), (150, 68), (110, 72), (60, 72)]
    shape(img, lambda d, v: d.polygon(body, fill=v), C["slate"], C["navy"], C["grey"])
    shape(img, lambda d, v: d.polygon([(60, 66), (110, 70), (156, 64), (110, 76), (64, 74)], fill=v), C["silver"], C["grey"], C["white"])
    # The dorsal fin, swept back toward the tail
    fin = [(118, 42), (110, 32), (100, 22), (86, 14), (88, 22), (90, 32), (92, 42)]
    shape(img, lambda d, v: d.polygon(fin, fill=v), C["slate"], C["navy"], C["grey"])
    shape(img, lambda d, v: d.polygon([(30, 60), (6, 38), (18, 60), (4, 82)], fill=v), C["slate"], C["navy"], C["grey"])  # Tail
    shape(img, lambda d, v: d.polygon([(100, 70), (84, 86), (90, 86), (110, 72)], fill=v), C["slate"], C["navy"])  # Pectoral fin, swept back too
    d = ImageDraw.Draw(img)
    # A wide grin full of teeth
    grin = [(170, 60), (164, 64), (156, 66), (146, 65), (138, 61)]
    d.line(grin, fill=C["wine"] + (255,), width=2)
    for (x0, y0), (x1, y1) in zip(grin, grin[1:]):
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        d.polygon([(mx - 2, my - 1), (mx + 2, my - 1), (mx, my + 2)], fill=C["white"] + (255,))
    d.line([(137, 59), (139, 63)], fill=C["wine"] + (255,))  # The corner of the grin
    d.rectangle([148, 52, 150, 54], fill=C["ink"] + (255,))
    dot(img, 148, 52, C["white"])
    for gx in (126, 130, 134):
        d.line([gx, 52, gx - 2, 60], fill=C["navy"] + (255,))  # Gills
    # The red tie, knotted at the neck
    shape(img, lambda d, v: d.polygon([(118, 66), (124, 66), (122, 70)], fill=v), C["red"], C["wine"])
    shape(img, lambda d, v: d.polygon([(120, 70), (124, 70), (128, 88), (122, 94), (118, 86)], fill=v), C["red"], C["wine"], C["pink"])
    return outline(img)


def phoenix_feather():
    img = layer()
    rnd = random.Random(11)
    cx, cy, ang = 96, 54, math.radians(-32)
    def P(u, v):  # Feather space: u along the shaft (quill at -64, tip at 58), v across
        return (cx + u * math.cos(ang) - v * math.sin(ang), cy + u * math.sin(ang) + v * math.cos(ang))
    # Along the vane: red at the quill, orange, a yellow heart, then the tip burning out pink and lilac
    ramp = [C["wine"], C["red"], C["orange"], C["amber"], C["yellow"], C["amber"], C["orange"], C["red"], C["magenta"], C["purple"]]
    def width(t):  # Half-width of the vane, t from 0 (its base) to 1 (its tip)
        return 4 + 15 * math.sin(math.pi * min(1.0, 0.08 + t * 0.95)) ** 0.7
    u0, u1, step = -40, 58, 2
    for u in range(u0, u1, step):
        t = (u - u0) / (u1 - u0)
        k = t * (len(ramp) - 1)
        i = min(int(k), len(ramp) - 2)
        for side in (-1, 1):
            w = width(t) + (rnd.randint(2, 6) if (u // 6) % 2 == 0 else 0)  # Flame tongues along the edge
            lean = 5  # The barbs sweep toward the tip
            quad = [P(u, 0), P(u + lean, side * w), P(u + lean + step, side * w), P(u + step, 0)]
            m = mask()
            ImageDraw.Draw(m).polygon(quad, fill=255)
            px, mp = img.load(), m.load()
            for y in range(H):
                for x in range(W):
                    if mp[x, y]:
                        f = k - i
                        col = ramp[i + 1] if f * 16 > BAYER[y % 4][x % 4] else ramp[i]
                        px[x, y] = col + (255,)
    # The heart of it, brighter along the shaft
    for u in range(-30, 40, 2):
        t = (u + 30) / 70
        w = 5 * math.sin(math.pi * t)
        if w < 1:
            continue
        ImageDraw.Draw(img).polygon([P(u, -w), P(u + 3, -w), P(u + 3, w), P(u, w)], fill=C["yellow"] + (255,))
    d = ImageDraw.Draw(img)
    # Barbs: dark lines swept from the shaft to the edge
    for u in range(-36, 54, 4):
        for side in (-1, 1):
            t = (u - u0) / (u1 - u0)
            d.line([P(u, side * 2), P(u + 5, side * width(t) * 0.85)], fill=(C["rust"] if t < 0.75 else C["purple"]) + (255,))
    # The glowing eye near the top
    ex, ey = P(36, 0)
    shape(img, lambda dr, v: dr.ellipse([ex - 5, ey - 4, ex + 5, ey + 4], fill=v), C["yellow"], C["orange"], C["white"])
    d.rectangle([ex - 1, ey - 1, ex + 1, ey + 1], fill=C["white"] + (255,))
    # The shaft and the bare quill
    d.line([P(-64, 0), P(54, 0)], fill=C["sand"] + (255,))
    for u in range(-60, -40, 3):  # Down at the base, wispy
        for side in (-1, 1):
            d.line([P(u, 0), P(u + 3, side * 6)], fill=C["red"] + (255,))
    img = outline(img, C["plum"])
    # Sparks on the vane and around, red low, lilac high
    for _ in range(30):
        x, y = P(rnd.uniform(-36, 54), rnd.uniform(-12, 12))
        dot(img, x, y, C["white"] if rnd.random() < 0.7 else C["yellow"])
    for _ in range(16):
        x, y = rnd.randint(24, 170), rnd.randint(6, 100)
        dot(img, x, y, C["pink"] if y < 50 else C["amber"])
    return img


def the_chosen_one():
    img = layer()
    d = ImageDraw.Draw(img)
    # The halo
    d.ellipse([78, 6, 114, 18], outline=C["yellow"] + (255,), width=2)
    # Robe, a white triangle, folds in silver
    shape(img, lambda dr, v: dr.polygon([(96, 36), (64, 108), (128, 108)], fill=v), C["white"], C["silver"], None, (1, 0))
    for fx in (84, 96, 108):
        d.line([96, 40, fx, 108], fill=C["silver"] + (255,))
    # Arms out, the guns akimbo
    for side in (-1, 1):
        shape(img, lambda dr, v: dr.polygon([(96 + side * 6, 42), (96 + side * 38, 50), (96 + side * 38, 56), (96 + side * 6, 52)], fill=v),
              C["white"], C["silver"])
        hx = 96 + side * 40
        shape(img, lambda dr, v: dr.ellipse([hx - 3, 50, hx + 3, 56], fill=v), C["skin"], C["skin_dark"])
        gx = hx + side * 4
        shape(img, lambda dr, v: dr.rectangle([min(gx, gx + side * 16), 47, max(gx, gx + side * 16), 51], fill=v), C["amber"], C["orange_brown"], C["yellow"])
        shape(img, lambda dr, v: dr.rectangle([gx - 1 if side > 0 else gx - 1, 51, gx + 2 if side > 0 else gx + 1, 58], fill=v), C["amber"], C["orange_brown"])
        fx = gx + side * 18
        d.polygon([(fx, 49), (fx + side * 8, 44), (fx + side * 5, 49), (fx + side * 9, 54)], fill=C["yellow"] + (255,))
        d.point([(fx + side * 3, 49)], fill=C["white"] + (255,))
    # Head: hair, face, beard, a smirk
    shape(img, lambda dr, v: dr.ellipse([84, 16, 108, 46], fill=v), C["dark_brown"], C["plum"], C["brown"])
    shape(img, lambda dr, v: dr.ellipse([88, 20, 104, 40], fill=v), C["skin"], C["skin_dark"], C["white"])
    shape(img, lambda dr, v: dr.polygon([(88, 32), (104, 32), (100, 44), (92, 44)], fill=v), C["dark_brown"], C["plum"])
    d.rectangle([91, 27, 92, 28], fill=C["ink"] + (255,))
    d.rectangle([99, 27, 100, 28], fill=C["ink"] + (255,))
    d.line([93, 35, 99, 34], fill=C["skin"] + (255,))
    img = outline(img)
    # Doves scattering
    for x, y, f in ((26, 22, 1), (40, 12, -1), (156, 16, 1), (170, 30, -1), (150, 6, -1)):
        dv = layer()
        ddv = ImageDraw.Draw(dv)
        ddv.ellipse([x - 3, y - 2, x + 3, y + 2], fill=C["white"] + (255,))
        ddv.polygon([(x - 1, y - 1), (x - 5 * f, y - 6), (x + 2 * f, y - 2)], fill=C["white"] + (255,))
        ddv.point([(x + 3 * f, y - 1)], fill=C["amber"] + (255,))
        img.alpha_composite(outline(dv, C["grey"]))
    return img


def man_o_war():
    img = layer()
    d = ImageDraw.Draw(img)
    # Hull, gun ports, the waterline
    hull = [(30, 72), (160, 72), (150, 88), (42, 88)]
    shape(img, lambda dr, v: dr.polygon(hull, fill=v), C["brown"], C["dark_brown"], C["tan"])
    shape(img, lambda dr, v: dr.polygon([(140, 60), (164, 60), (160, 72), (140, 72)], fill=v), C["brown"], C["dark_brown"], C["tan"])
    d.line([38, 78, 156, 78], fill=C["amber"] + (255,))
    for gx in range(46, 148, 10):
        d.rectangle([gx, 81, gx + 3, 84], fill=C["ink"] + (255,))
    # Masts and sails, billowing
    for mx, top, sw in ((62, 14, 22), (96, 6, 26), (130, 16, 20)):
        d.line([mx, top, mx, 72], fill=C["dark_brown"] + (255,), width=2)
        for i, (sy, sh) in enumerate(((top + 4, 18), (top + 24, 20))):
            w = sw - i * 2
            shape(img, lambda dr, v: dr.polygon([(mx - w / 2, sy), (mx + w / 2, sy), (mx + w / 2 + 3, sy + sh), (mx - w / 2 - 3, sy + sh)], fill=v),
                  C["white"], C["silver"], None, (1, 1))
            d.line([mx - w / 2, sy, mx + w / 2, sy], fill=C["dark_brown"] + (255,))
        d.polygon([(mx, top - 6), (mx + 8, top - 4), (mx, top - 2)], fill=C["red"] + (255,))
    d.line([30, 70, 6, 52], fill=C["dark_brown"] + (255,))  # Bowsprit
    img = outline(img)
    # Foam at the bow
    for i in range(10):
        dot(img, 30 + i * 2, 89 + (i % 2), C["white"])
    return img


def arrow_line(d, points, color, width=3, head=7):
    """A chart line ending in a proper arrowhead along its last stretch."""
    d.line(points, fill=color + (255,), width=width, joint="curve")
    (x0, y0), (x1, y1) = points[-2], points[-1]
    n = math.hypot(x1 - x0, y1 - y0)
    ux, uy = (x1 - x0) / n, (y1 - y0) / n
    px, py = -uy, ux
    tip = (x1 + ux * head * 0.6, y1 + uy * head * 0.6)
    d.polygon([tip, (x1 - ux * head + px * head * 0.7, y1 - uy * head + py * head * 0.7),
               (x1 - ux * head - px * head * 0.7, y1 - uy * head - py * head * 0.7)], fill=color + (255,))


def unicorn():
    rnd = random.Random(4)
    back = layer()
    # The chart behind: up, up, then down it goes
    arrow_line(ImageDraw.Draw(back), [(8, 40), (30, 30), (50, 34), (70, 18), (96, 30), (124, 26), (150, 52), (176, 84)], C["red"])
    back = outline(back, C["wine"])
    img = layer()
    d = ImageDraw.Draw(img)
    # Body, legs, neck, head
    for lx in (70, 80, 112, 122):
        shape(img, lambda dr, v: dr.rectangle([lx, 66, lx + 5, 92], fill=v), C["white"], C["silver"])
        d.rectangle([lx, 90, lx + 5, 93], fill=C["amber"] + (255,))
    shape(img, lambda dr, v: dr.ellipse([62, 46, 132, 74], fill=v), C["white"], C["silver"])
    shape(img, lambda dr, v: dr.polygon([(116, 54), (126, 30), (140, 28), (134, 58)], fill=v), C["white"], C["silver"])
    shape(img, lambda dr, v: dr.ellipse([128, 18, 154, 36], fill=v), C["white"], C["silver"])
    shape(img, lambda dr, v: dr.ellipse([144, 26, 158, 36], fill=v), C["white"], C["silver"])  # Muzzle
    d.rectangle([140, 24, 141, 25], fill=C["ink"] + (255,))
    dot(img, 155, 31, C["grey"])
    shape(img, lambda dr, v: dr.polygon([(136, 20), (148, 0), (142, 21)], fill=v), C["amber"], C["orange_brown"], C["yellow"])
    for k in range(3):  # The horn's spiral
        d.line([(138 + k * 2, 16 - k * 5), (141 + k * 2, 17 - k * 5)], fill=C["orange_brown"] + (255,))
    # The mane: locks flowing down the back of the neck, the six colors side by side, and a forelock
    rainbow = (C["red"], C["orange"], C["yellow"], C["green"], C["sky"], C["purple"])
    for lock in range(4):
        y = 22 + lock * 9
        x = 128 - lock * 3
        for i, col in enumerate(rainbow):
            d.line([(x - i, y + i), (x - 6 - i, y + 6 + i), (x - 4 - i, y + 12 + i)], fill=col + (255,))
    for i, col in enumerate(rainbow[:4]):
        d.line([(134 + i, 18), (130 + i, 26)], fill=col + (255,))
    # The tail, a rainbow sweep down from the rump
    for i, col in enumerate(rainbow):
        d.line([(64, 52 + i), (52 - i, 56 + i * 2), (44 - i, 70 + i), (46 - i, 84)], fill=col + (255,), width=2)
    img = outline(img)
    # On fire: flames over its legs and belly, licking up its sides, in front of it
    fire = layer()
    for fx in range(52, 150, 12):  # A short fire on the ground all round its hooves, in front of it
        flame(fire, fx, 100, rnd.randint(14, 22), 14, rnd)
    back.alpha_composite(img)
    back.alpha_composite(outline(fire, C["wine"]))
    return back


def fat_tony():
    img = layer()
    d = ImageDraw.Draw(img)
    # Shoulders in a dark pinstripe suit, a white collar, the red tie
    shape(img, lambda dr, v: dr.ellipse([46, 76, 146, 140], fill=v), C["navy"], C["night"], C["slate"])
    for sx in range(54, 140, 6):
        d.line([sx, 80, sx + 4, 108], fill=C["slate"] + (255,))
    shape(img, lambda dr, v: dr.polygon([(84, 76), (96, 90), (108, 76)], fill=v), C["white"], C["silver"])
    shape(img, lambda dr, v: dr.polygon([(93, 82), (99, 82), (102, 108), (96, 112), (90, 108)], fill=v), C["red"], C["wine"], C["pink"])
    # A heavy face, stubble, a smirk
    shape(img, lambda dr, v: dr.ellipse([70, 30, 122, 84], fill=v), C["skin"], C["skin_dark"], C["white"])
    shape(img, lambda dr, v: dr.ellipse([76, 60, 116, 86], fill=v), C["skin_dark"], C["brown"])
    d.rectangle([82, 52, 87, 54], fill=C["ink"] + (255,))
    d.rectangle([104, 52, 109, 54], fill=C["ink"] + (255,))
    d.line([80, 48, 90, 50], fill=C["dark_brown"] + (255,), width=2)
    d.line([102, 50, 112, 47], fill=C["dark_brown"] + (255,), width=2)
    d.line([88, 72, 104, 70], fill=C["wine"] + (255,), width=2)
    # The fedora with a red band
    shape(img, lambda dr, v: dr.ellipse([56, 32, 136, 44], fill=v), C["night"], C["ink"], C["slate"])
    shape(img, lambda dr, v: dr.polygon([(72, 38), (76, 12), (96, 8), (116, 12), (120, 38)], fill=v), C["night"], C["ink"], C["slate"])
    d.rectangle([73, 30, 119, 34], fill=C["wine"] + (255,))
    # The cigar
    shape(img, lambda dr, v: dr.rectangle([104, 70, 124, 73], fill=v), C["brown"], C["dark_brown"])
    d.rectangle([124, 70, 126, 73], fill=C["orange"] + (255,))
    img = outline(img)
    for i, (x, y) in enumerate(((128, 66), (130, 60), (128, 54), (131, 47), (129, 41))):
        dot(img, x, y, C["silver"] if i % 2 else C["grey"])
        dot(img, x + 1, y - 1, C["grey"])
    return img


def antivirus():
    img = layer()
    d = ImageDraw.Draw(img)
    # A panda holding up a shield with a check mark
    shape(img, lambda dr, v: dr.ellipse([58, 56, 134, 116], fill=v), C["white"], C["silver"])
    for ex in (66, 114):
        shape(img, lambda dr, v: dr.ellipse([ex - 8, 10, ex + 8, 26], fill=v), C["night"], C["ink"], C["slate"])
    shape(img, lambda dr, v: dr.ellipse([62, 14, 130, 70], fill=v), C["white"], C["silver"])
    for ex, tilt in ((82, -1), (110, 1)):
        d.polygon([(ex - 7, 34 + tilt * 2), (ex + 7, 34 - tilt * 2), (ex + 6, 48), (ex - 6, 48)], fill=C["night"] + (255,))
        d.rectangle([ex - 1, 39, ex + 1, 41], fill=C["white"] + (255,))
    shape(img, lambda dr, v: dr.ellipse([90, 48, 102, 56], fill=v), C["night"], C["ink"])
    d.line([92, 60, 96, 62, 100, 60], fill=C["night"] + (255,))
    # The shield
    shield = [(118, 60), (154, 60), (154, 84), (136, 100), (118, 84)]
    shape(img, lambda dr, v: dr.polygon(shield, fill=v), C["sky"], C["blue"], C["cyan"])
    d.line([(126, 78), (133, 86), (147, 68)], fill=C["white"] + (255,), width=3)
    # Paws round the shield
    for px, py in ((116, 72), (150, 90)):
        shape(img, lambda dr, v: dr.ellipse([px - 6, py - 5, px + 6, py + 5], fill=v), C["night"], C["ink"], C["slate"])
    return outline(img)


def catfish():
    img = layer()
    d = ImageDraw.Draw(img)
    # The fishbowl: water, sand, a pebble, the glass rim and a shine
    bx, by, br = 134, 62, 36
    m = mask()
    ImageDraw.Draw(m).ellipse([bx - br, by - br, bx + br, by + br], fill=255)
    ImageDraw.Draw(m).rectangle([0, 0, W, by - br + 10], fill=0)  # The water line, below the rim
    water = layer()
    gradient(water, [C["cyan"], C["cyan"], C["sky"]], by - br + 10, by + br)
    img.paste(water, (0, 0), m)
    d.chord([bx - br + 4, by + 12, bx + br - 4, by + br], 0, 180, fill=C["sand"] + (255,))
    d.ellipse([bx + 8, by + 22, bx + 16, by + 28], fill=C["grey"] + (255,))
    d.line([(bx - 20, by + 30), (bx - 22, by + 18), (bx - 18, by + 10)], fill=C["green"] + (255,), width=2)  # A water plant
    # The fish, round and blue, a fan of a tail
    shape(img, lambda dr, v: dr.ellipse([bx - 20, by - 14, bx + 14, by + 12], fill=v), C["pink"], C["magenta"], C["white"])
    d.chord([bx - 16, by - 2, bx + 10, by + 12], 0, 180, fill=C["white"] + (255,))  # A pale belly
    shape(img, lambda dr, v: dr.polygon([(bx + 12, by), (bx + 26, by - 12), (bx + 30, by - 4), (bx + 30, by + 4), (bx + 26, by + 12)], fill=v),
          C["pink"], C["magenta"], C["white"])
    d.ellipse([bx - 12, by - 6, bx - 6, by], fill=C["white"] + (255,))
    d.rectangle([bx - 11, by - 5, bx - 9, by - 3], fill=C["ink"] + (255,))
    d.arc([bx - 18, by + 1, bx - 11, by + 7], 20, 160, fill=C["wine"] + (255,))  # A small smile
    for sx in range(bx - 4, bx + 10, 5):
        d.arc([sx, by - 6, sx + 6, by + 2], 300, 60, fill=C["magenta"] + (255,))
    # The glass: the rim and a shine
    d.arc([bx - br, by - br, bx + br, by + br], 30, 150, fill=C["silver"] + (255,), width=2)
    d.arc([bx - br, by - br, bx + br, by + br], 150, 210, fill=C["silver"] + (255,), width=2)
    d.arc([bx - br, by - br, bx + br, by + br], 330, 30, fill=C["silver"] + (255,), width=2)
    d.ellipse([bx - 20, by - br + 6, bx + 20, by - br + 14], outline=C["white"] + (255,))
    d.arc([bx - br + 6, by - br + 14, bx + br - 6, by + br - 6], 200, 240, fill=C["white"] + (255,), width=2)
    for k in range(4):  # Bubbles
        d.ellipse([bx - 24 + k, by - 18 - k * 6, bx - 22 + k, by - 16 - k * 6], outline=C["white"] + (255,))
    img = outline(img)
    # The cat, ginger, leaning in to nuzzle the glass
    cat = layer()
    cd = ImageDraw.Draw(cat)
    shape(cat, lambda dr, v: dr.ellipse([14, 56, 74, 108], fill=v), C["orange"], C["rust"], C["amber"])
    hx, hy, hr = 72, 46, 24
    shape(cat, lambda dr, v: dr.ellipse([hx - hr, hy - hr, hx + hr, hy + hr], fill=v), C["orange"], C["rust"], C["amber"])
    for a in (-0.62, 0.62):  # Ears on the curve of the head, pointing out from its center
        ox, oy = math.sin(a), -math.cos(a)
        tx, ty = -oy, ox
        base = (hx + ox * (hr - 3), hy + oy * (hr - 3))
        ear = [(base[0] - tx * 8, base[1] - ty * 8), (hx + ox * (hr + 14), hy + oy * (hr + 14)), (base[0] + tx * 8, base[1] + ty * 8)]
        inner = [(base[0] - tx * 4, base[1] - ty * 4), (hx + ox * (hr + 8), hy + oy * (hr + 8)), (base[0] + tx * 4, base[1] + ty * 4)]
        shape(cat, lambda dr, v: dr.polygon(ear, fill=v), C["orange"], C["rust"])
        cd.polygon(inner, fill=C["pink"] + (255,))
    for ex in (64, 82):
        cd.line([ex - 3, 44, ex, 41, ex + 3, 44], fill=C["ink"] + (255,))  # Happy closed eyes
    cd.polygon([(92, 50), (96, 50), (94, 53)], fill=C["pink"] + (255,))
    for wy in (52, 56):
        cd.line([86, wy, 100, wy - 2], fill=C["sand"] + (255,))
    for sx in range(22, 70, 9):
        cd.line([sx, 64, sx + 4, 74], fill=C["rust"] + (255,))
    shape(cat, lambda dr, v: dr.ellipse([86, 74, 104, 84], fill=v), C["orange"], C["rust"], C["amber"])  # A paw on the glass
    img.alpha_composite(outline(cat))
    # A heart between them
    heart = layer()
    hd = ImageDraw.Draw(heart)
    hd.ellipse([94, 6, 104, 16], fill=C["hot"] + (255,))
    hd.ellipse([102, 6, 112, 16], fill=C["hot"] + (255,))
    hd.polygon([(95, 13), (111, 13), (103, 24)], fill=C["hot"] + (255,))
    hd.point([(97, 9), (98, 9)], fill=C["white"] + (255,))
    img.alpha_composite(outline(heart))
    return img


def mutually_assured_destruction():
    img = layer()
    rnd = random.Random(9)
    d = ImageDraw.Draw(img)
    # The mushroom cloud: a stem, a ring, a churning cap
    shape(img, lambda dr, v: dr.polygon([(86, 92), (90, 48), (102, 48), (106, 92)], fill=v), C["tan"], C["brown"], C["sand"])
    shape(img, lambda dr, v: dr.ellipse([64, 60, 128, 72], fill=v), C["sand"], C["tan"], C["white"])
    for i in range(14):
        a = i / 14 * math.pi
        x, y = 96 + math.cos(a) * 34, 34 - math.sin(a) * 18
        r = rnd.randint(9, 13)
        shape(img, lambda dr, v: dr.ellipse([x - r, y - r, x + r, y + r], fill=v), C["sand"], C["tan"], C["white"])
    shape(img, lambda dr, v: dr.ellipse([62, 22, 130, 50], fill=v), C["amber"], C["orange"], C["yellow"])
    shape(img, lambda dr, v: dr.ellipse([66, 82, 126, 100], fill=v), C["orange"], C["rust"], C["yellow"])
    # Two missiles, flying at each other from the corners
    for (x0, y0), (x1, y1) in (((8, 18), (40, 34)), ((184, 18), (152, 34))):
        dx, dy = x1 - x0, y1 - y0
        n = math.hypot(dx, dy)
        ux, uy = dx / n, dy / n
        px, py = -uy * 3, ux * 3
        body = [(x0 + px, y0 + py), (x1 + px, y1 + py), (x1 + ux * 6, y1 + uy * 6), (x1 - px, y1 - py), (x0 - px, y0 - py)]
        shape(img, lambda dr, v: dr.polygon(body, fill=v), C["silver"], C["grey"], C["white"])
        d.polygon([(x0 + px * 2, y0 + py * 2), (x0 + ux * 6, y0 + uy * 6), (x0 - px * 2, y0 - py * 2)], fill=C["red"] + (255,))
        for k in range(1, 4):
            dot(img, x0 - ux * k * 3, y0 - uy * k * 3, C["yellow"] if k == 1 else C["orange"])
    return outline(img, C["plum"])


def toxic_positivity():
    img = layer()
    d = ImageDraw.Draw(img)
    shape(img, lambda dr, v: dr.ellipse([58, 6, 134, 82], fill=v), C["yellow"], C["amber"], C["white"], (1, 1), 3)
    for ex in (82, 110):
        shape(img, lambda dr, v: dr.ellipse([ex - 4, 26, ex + 4, 42], fill=v), C["dark_brown"], C["plum"])
        dot(img, ex - 2, 29, C["white"])
    d.arc([72, 30, 120, 68], 20, 160, fill=C["dark_brown"] + (255,), width=3)
    # The crack, and a plaster on the cheek
    d.line([(116, 8), (110, 22), (118, 32), (108, 50)], fill=C["dark_brown"] + (255,), width=2)
    plaster = Image.new("RGBA", (24, 10), (0, 0, 0, 0))
    pd = ImageDraw.Draw(plaster)
    pd.rectangle([0, 0, 23, 9], fill=C["tan"] + (255,))
    pd.rectangle([8, 0, 15, 9], fill=C["skin"] + (255,))
    for x in (2, 5, 18, 21):
        pd.point([(x, 3), (x, 6)], fill=C["skin_dark"] + (255,))
    plaster = plaster.rotate(25, expand=True, resample=Image.NEAREST)
    img.alpha_composite(plaster, (66, 46))
    return outline(img)


CARDS = [
    ("HostileTakeover", "office", hostile_takeover),
    ("PhoenixFeather", "temple", phoenix_feather),
    ("TheChosenOne", "temple", the_chosen_one),
    ("ManoWar", "state_sea", man_o_war),
    ("Unicorn", "office", unicorn),
    ("FatTony", "office", fat_tony),
    ("Antivirus", "circuit", antivirus),
    ("Catfish", "vapor", catfish),
    ("MutuallyAssuredDestruction", "fallout", mutually_assured_destruction),
    ("ToxicPositivity", "vapor", toxic_positivity),
]
BACKGROUNDS = {"office": bg_office, "temple": bg_temple, "state_sea": bg_state_sea, "circuit": bg_circuit, "vapor": bg_vapor,
               "fallout": bg_fallout}



# ---------------------------------------------------------------- more backgrounds (batch 2)

def bg_gallery():
    """An auction room: a warm wall under a spotlight, a dark floor."""
    img = layer()
    gradient(img, [C["dark_brown"], C["brown"], C["dark_brown"]], 0, 88)
    px = img.load()
    for y in range(88):
        for x in range(W):
            if (x - 96) ** 2 / 3000 + (y - 40) ** 2 / 1600 < 1 and BAYER[y % 4][x % 4] < 8:
                px[x, y] = C["tan"] + (255,)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 88, W, H], fill=C["plum"] + (255,))
    d.line([0, 88, W, 88], fill=C["ink"] + (255,))
    return img


def bg_mind():
    """Mind: a pastel haze with a slow spiral."""
    img = layer()
    gradient(img, [C["silver"], C["pink"], C["magenta"]], 0, H)
    px = img.load()
    for t in range(0, 1400):
        a = t / 70
        r = 4 + a * 7
        x, y = int(96 + math.cos(a) * r), int(54 + math.sin(a) * r * 0.7)
        if 0 <= x < W and 0 <= y < H:
            px[x, y] = C["white"] + (255,)
    return img


def bg_space():
    """Deep space: stars, a faint nebula."""
    img = layer()
    gradient(img, [C["ink"], C["night"], C["purple"], C["night"]], 0, H)
    rnd = random.Random(21)
    for _ in range(90):
        dot(img, rnd.randrange(W), rnd.randrange(H), rnd.choice((C["white"], C["silver"], C["cyan"], C["yellow"])))
    return img


def bg_room_night():
    """A dark bedroom at 3 AM, a window with the moon."""
    img = layer()
    gradient(img, [C["night"], C["ink"]], 0, H)
    d = ImageDraw.Draw(img)
    d.rectangle([140, 10, 178, 46], fill=C["navy"] + (255,), outline=C["slate"] + (255,))
    d.line([159, 10, 159, 46], fill=C["slate"] + (255,))
    d.line([140, 28, 178, 28], fill=C["slate"] + (255,))
    d.ellipse([164, 14, 172, 22], fill=C["sand"] + (255,))
    return img


def bg_fog():
    """Morning fog over a muddy field."""
    img = layer()
    gradient(img, [C["grey"], C["silver"], C["grey"]], 0, 84)
    gradient(img, [C["dark_brown"], C["plum"]], 84, H)
    rnd = random.Random(13)
    d = ImageDraw.Draw(img)
    for _ in range(14):
        x, y = rnd.randint(-20, W), rnd.randint(20, 80)
        d.line([x, y, x + rnd.randint(20, 50), y], fill=C["white"] + (255,))
    return img


def bg_state():
    """Statecraft: a parade-ground sunburst over a lawn."""
    img = layer()
    gradient(img, [C["tan"], C["sand"]], 0, 86)
    px = img.load()
    for y in range(86):
        for x in range(W):
            a = math.atan2(y - 86, x - 96)
            if int((a + math.pi) / (math.pi / 14)) % 2 == 0 and BAYER[y % 4][x % 4] < 9:
                px[x, y] = C["sand"] + (255,) if (x + y) % 2 else C["white"] + (255,)
    gradient(img, [C["green"], C["dark_green"]], 86, H)
    return img


def bg_cave():
    """A green hillside at dusk, a dark cave mouth."""
    img = layer()
    gradient(img, [C["slate"], C["grey"], C["tan"]], 0, 60)
    d = ImageDraw.Draw(img)
    d.ellipse([-40, 40, 240, 160], fill=C["dark_green"] + (255,))
    d.ellipse([-60, 70, 120, 180], fill=C["forest"] + (255,))
    d.chord([64, 30, 140, 110], 180, 360, fill=C["ink"] + (255,))
    d.rectangle([64, 70, 140, 96], fill=C["ink"] + (255,))
    gradient(img, [C["dark_green"], C["forest"]], 96, H)
    return img


def bg_basement():
    """A dusty basement: a concrete wall, pipes, a bare bulb."""
    img = layer()
    gradient(img, [C["slate"], C["navy"]], 0, 90)
    d = ImageDraw.Draw(img)
    for y in range(8, 90, 12):  # Concrete blocks
        off = 0 if (y // 12) % 2 else 10
        for x in range(-off, W, 20):
            d.rectangle([x, y, x + 19, y + 11], outline=C["night"] + (255,))
    d.rectangle([0, 3, W, 6], fill=C["grey"] + (255,))
    d.line([0, 3, W, 3], fill=C["silver"] + (255,))
    d.rectangle([0, 90, W, H], fill=C["night"] + (255,))
    d.line([30, 0, 30, 10], fill=C["ink"] + (255,))
    d.ellipse([27, 10, 33, 16], fill=C["yellow"] + (255,))
    return img


# ---------------------------------------------------------------- subjects (batch 2)

def dog(img, x, y, size, coat, dark, light, face=None, ears="flop", flip=1):
    """A cartoon dog in the pile: a stubby body, a big head sticking out, ears, a muzzle, a happy face and a tongue."""
    s = size
    shape(img, lambda d, v: d.ellipse([x - s * 0.85, y - s * 0.5, x + s * 0.85, y + s * 0.5], fill=v), coat, dark, light)
    d = ImageDraw.Draw(img)
    for px_ in (x - s * 0.5, x + s * 0.3):  # Paws dangling
        d.ellipse([px_ - 3, y + s * 0.35, px_ + 3, y + s * 0.6], fill=light + (255,))
    hx, hy, hr = x + flip * s * 0.6, y - s * 0.6, s * 0.62
    shape(img, lambda dr, v: dr.ellipse([hx - hr, hy - hr, hx + hr, hy + hr], fill=v), coat, dark, light)
    for side in (-1, 1):
        ex = hx + side * hr * 0.85
        if ears == "point":
            d.polygon([(ex - 4, hy - hr * 0.3), (ex + side * 3, hy - hr * 1.5), (ex + 4, hy - hr * 0.5)], fill=coat + (255,))
            d.polygon([(ex - 2, hy - hr * 0.4), (ex + side * 2, hy - hr * 1.2), (ex + 2, hy - hr * 0.5)], fill=C["pink"] + (255,))
        else:
            d.ellipse([ex - 4, hy - hr * 0.5, ex + 4, hy + hr * 0.7], fill=dark + (255,))
    muzzle = face or light
    d.ellipse([hx - hr * 0.55, hy, hx + hr * 0.55, hy + hr * 0.75], fill=muzzle + (255,))
    for side in (-1, 1):
        d.rectangle([hx + side * hr * 0.38 - 1, hy - hr * 0.3, hx + side * hr * 0.38, hy - hr * 0.3 + 1], fill=C["ink"] + (255,))
    d.ellipse([hx - 2, hy + hr * 0.1, hx + 2, hy + hr * 0.35], fill=C["ink"] + (255,))
    d.rectangle([hx - 1, hy + hr * 0.45, hx + 1, hy + hr * 0.8], fill=C["pink"] + (255,))


def dogpile():
    img = layer()
    d = ImageDraw.Draw(img)
    # The one buried: only a hand waving out of the side of the pile
    shape(img, lambda dr, v: dr.polygon([(150, 92), (166, 76), (170, 79), (156, 95)], fill=v), C["sky"], C["blue"])  # Sleeve
    shape(img, lambda dr, v: dr.ellipse([162, 66, 176, 80], fill=v), C["skin"], C["skin_dark"], C["white"])
    for k in range(4):
        d.rectangle([164 + k * 3, 58 + abs(k - 1.5), 165 + k * 3, 68], fill=C["skin"] + (255,))
    pile = layer()
    # The dogs, heaped up from the bottom: labradors, corgis, a black lab, and a confused pug on top
    dogs = [(48, 92, 16, "lab"), (84, 94, 18, "corgi"), (122, 93, 17, "black"), (150, 96, 14, "lab"),
            (62, 76, 15, "corgi"), (100, 76, 16, "lab"), (134, 78, 14, "corgi"),
            (78, 60, 14, "black"), (112, 60, 14, "lab"), (96, 44, 12, "pug")]
    coats = {"lab": (C["amber"], C["orange_brown"], C["yellow"], None, "flop"),
             "corgi": (C["orange"], C["rust"], C["amber"], C["white"], "point"),
             "black": (C["night"], C["ink"], C["slate"], None, "flop"),
             "pug": (C["tan"], C["brown"], C["sand"], C["dark_brown"], "flop")}
    for i, (x, y, s, kind) in enumerate(dogs):
        coat, dark, light, face, ears = coats[kind]
        dog(pile, x, y, s, coat, dark, light, face, ears, flip=1 if i % 2 else -1)
    pile = outline(pile)
    img = outline(img)
    img.alpha_composite(pile)
    pd = ImageDraw.Draw(img)
    pd.text((90, 22), "?", fill=C["white"] + (255,))  # The pug on top has no idea
    for k in range(3):  # Motion lines by the waving hand
        pd.line([(178, 62 + k * 5), (184, 60 + k * 5)], fill=C["white"] + (255,))
    return img


def burn_notice():
    img = layer()
    d = ImageDraw.Draw(img)
    # The gilded frame, the canvas in it: a stencilled spy reaching for a red heart balloon
    shape(img, lambda dr, v: dr.rectangle([58, 6, 134, 64], fill=v), C["amber"], C["orange_brown"], C["yellow"], (1, 1), 3)
    d.rectangle([64, 12, 128, 58], fill=C["white"] + (255,))
    d.rectangle([64, 52, 128, 58], fill=C["sand"] + (255,))
    # The spy: hat, coat, an arm reaching up
    d.polygon([(82, 30), (90, 30), (88, 26), (84, 26)], fill=C["ink"] + (255,))
    d.rectangle([80, 30, 92, 32], fill=C["ink"] + (255,))
    d.ellipse([83, 32, 89, 38], fill=C["ink"] + (255,))
    d.polygon([(80, 38), (92, 38), (96, 56), (76, 56)], fill=C["ink"] + (255,))
    d.line([(90, 40), (100, 30)], fill=C["ink"] + (255,), width=2)
    d.line([(108, 28), (101, 30)], fill=C["ink"] + (255,))  # The string
    d.ellipse([104, 16, 114, 26], fill=C["red"] + (255,))
    d.polygon([(105, 22), (113, 22), (109, 30)], fill=C["red"] + (255,))
    # The bottom of the frame is a shredder: the canvas comes out in strips, stamped red
    d.rectangle([58, 64, 134, 68], fill=C["night"] + (255,))
    rnd = random.Random(8)
    for k, x in enumerate(range(64, 128, 6)):
        length = rnd.randint(18, 34)
        d.rectangle([x, 69, x + 3, 69 + length], fill=(C["white"] if k % 2 else C["sand"]) + (255,))
        if k in (3, 4, 6, 7):
            d.rectangle([x, 76, x + 3, 79], fill=C["red"] + (255,))
        if k in (4, 5):
            d.rectangle([x, 70, x + 3, 74], fill=C["ink"] + (255,))
    img = outline(img)
    # Auction paddles raised in the dark below
    for x, n in ((20, "7"), (170, "12")):
        paddle = layer()
        pd = ImageDraw.Draw(paddle)
        pd.line([x, 104, x, 92], fill=C["brown"] + (255,), width=2)
        pd.ellipse([x - 7, 78, x + 7, 92], fill=C["white"] + (255,))
        pd.text((x - 3 - (2 if len(n) > 1 else 0), 80), n, fill=C["ink"] + (255,))
        img.alpha_composite(outline(paddle))
    return img


def schrodingers_cat():
    img = layer()
    d = ImageDraw.Draw(img)
    # The cat sitting up in the box: left half alive and fluffy, right half a skeleton
    hx, hy = 96, 34
    alive = layer()
    shape(alive, lambda dr, v: dr.ellipse([72, 40, 120, 80], fill=v), C["orange"], C["rust"], C["amber"])
    shape(alive, lambda dr, v: dr.ellipse([hx - 18, hy - 16, hx + 18, hy + 14], fill=v), C["orange"], C["rust"], C["amber"])
    ad = ImageDraw.Draw(alive)
    ad.polygon([(hx - 16, hy - 8), (hx - 14, hy - 26), (hx - 4, hy - 14)], fill=C["orange"] + (255,))
    ad.polygon([(hx - 13, hy - 10), (hx - 12, hy - 20), (hx - 7, hy - 13)], fill=C["pink"] + (255,))
    ad.rectangle([hx - 9, hy - 4, hx - 6, hy + 1], fill=C["green"] + (255,))
    ad.line([hx - 8, hy - 4, hx - 8, hy + 1], fill=C["ink"] + (255,))
    for wy in (hy + 4, hy + 7):
        ad.line([hx - 26, wy - 2, hx - 12, wy], fill=C["white"] + (255,))
    for sx in range(76, 96, 6):
        ad.line([sx, 50, sx + 3, 58], fill=C["rust"] + (255,))
    half = mask()
    ImageDraw.Draw(half).rectangle([0, 0, 96, H], fill=255)
    img.paste(alive, (0, 0), ImageChops.multiply(alive.split()[3], half))
    bones = layer()
    bd = ImageDraw.Draw(bones)
    bd.ellipse([hx - 16, hy - 14, hx + 16, hy + 12], fill=C["white"] + (255,))  # The skull
    bd.polygon([(hx + 4, hy - 12), (hx + 14, hy - 26), (hx + 16, hy - 8)], fill=C["white"] + (255,))
    bd.ellipse([hx + 4, hy - 6, hx + 11, hy + 2], fill=C["ink"] + (255,))
    bd.polygon([(hx + 1, hy + 4), (hx + 4, hy + 4), (hx + 2, hy + 7)], fill=C["ink"] + (255,))
    for k in range(3):
        bd.line([hx + 2 + k * 3, hy + 9, hx + 2 + k * 3, hy + 11], fill=C["ink"] + (255,))
    bd.line([96, 46, 96, 78], fill=C["white"] + (255,), width=2)  # The spine
    for ry in range(48, 74, 5):
        bd.arc([82, ry - 4, 114, ry + 6], 300, 360, fill=C["white"] + (255,), width=2)
    right = mask()
    ImageDraw.Draw(right).rectangle([97, 0, W, H], fill=255)
    img.paste(bones, (0, 0), ImageChops.multiply(bones.split()[3], right))
    d.line([96, hy - 18, 96, 80], fill=C["ink"] + (255,))  # The line between the two states
    # The cardboard box, flaps open
    shape(img, lambda dr, v: dr.rectangle([60, 66, 132, 104], fill=v), C["tan"], C["brown"], C["sand"])
    shape(img, lambda dr, v: dr.polygon([(60, 66), (44, 56), (52, 54), (66, 64)], fill=v), C["tan"], C["brown"], C["sand"])
    shape(img, lambda dr, v: dr.polygon([(132, 66), (148, 56), (140, 54), (126, 64)], fill=v), C["tan"], C["brown"], C["sand"])
    d.line([60, 84, 132, 84], fill=C["brown"] + (255,))
    d.text((86, 88), "50%", fill=C["dark_brown"] + (255,))
    return outline(img)


def black_hole():
    img = layer()
    rnd = random.Random(17)
    cx, cy = 96, 56
    # The accretion disk, hot rings round the dark, and the swirl of what's falling in
    for rx, ry, col in ((70, 20, C["rust"]), (60, 17, C["orange"]), (48, 14, C["amber"]), (36, 11, C["yellow"])):
        m = mask()
        ImageDraw.Draw(m).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
        fill(img, m, col)
    d = ImageDraw.Draw(img)
    for a in range(0, 360, 20):
        r = math.radians(a)
        d.arc([cx - 64, cy - 18, cx + 64, cy + 18], a, a + 8, fill=C["white"] + (255,))
    d.ellipse([cx - 18, cy - 18, cx + 18, cy + 18], fill=C["ink"] + (255,))
    d.arc([cx - 20, cy - 20, cx + 20, cy + 20], 180, 360, fill=C["yellow"] + (255,))
    img = outline(img, C["night"])
    things = layer()
    td = ImageDraw.Draw(things)
    # A cow, stretching toward it
    shape(things, lambda dr, v: dr.ellipse([18, 16, 46, 30], fill=v), C["white"], C["silver"])
    for sx, sy in ((24, 19), (34, 23), (40, 18)):
        td.ellipse([sx, sy, sx + 4, sy + 3], fill=C["ink"] + (255,))
    shape(things, lambda dr, v: dr.ellipse([42, 18, 54, 28], fill=v), C["white"], C["silver"])
    td.rectangle([50, 22, 54, 26], fill=C["pink"] + (255,))
    for lx in (22, 28, 36, 42):
        td.line([lx, 29, lx - 2, 34], fill=C["white"] + (255,))
    # A sofa
    shape(things, lambda dr, v: dr.rectangle([148, 18, 176, 30], fill=v), C["red"], C["wine"], C["pink"])
    shape(things, lambda dr, v: dr.rectangle([146, 12, 178, 20], fill=v), C["red"], C["wine"], C["pink"])
    # A single sock
    shape(things, lambda dr, v: dr.polygon([(30, 80), (36, 78), (40, 92), (34, 96), (28, 94), (32, 90)], fill=v), C["white"], C["silver"])
    td.line([31, 82, 37, 80], fill=C["red"] + (255,))
    td.line([32, 85, 38, 83], fill=C["red"] + (255,))
    # A tax form, tumbling
    form = Image.new("RGBA", (14, 18), (0, 0, 0, 0))
    fd = ImageDraw.Draw(form)
    fd.rectangle([0, 0, 13, 17], fill=C["white"] + (255,))
    for k in range(3, 16, 3):
        fd.line([2, k, 11, k], fill=C["grey"] + (255,))
    fd.rectangle([2, 1, 6, 2], fill=C["red"] + (255,))
    things.alpha_composite(form.rotate(30, expand=True, resample=Image.NEAREST), (150, 74))
    img.alpha_composite(outline(things))
    for _ in range(18):  # Debris streaking in
        a = rnd.uniform(0, math.tau)
        r = rnd.uniform(40, 80)
        x, y = cx + math.cos(a) * r, cy + math.sin(a) * r * 0.5
        d2 = ImageDraw.Draw(img)
        d2.line([x, y, x - math.cos(a) * 4, y - math.sin(a) * 2], fill=C["silver"] + (255,))
    return img


def doomscrolling():
    img = layer()
    d = ImageDraw.Draw(img)
    # The 3 AM wojak: bald, shirtless, baggy eyes, nothing in them, lit by the phone
    shape(img, lambda dr, v: dr.ellipse([52, 70, 140, 140], fill=v), C["skin_dark"], C["brown"], C["skin"])
    shape(img, lambda dr, v: dr.ellipse([70, 10, 122, 76], fill=v), C["skin"], C["skin_dark"], C["white"])
    for ex in (86, 106):  # Small dead eyes, heavy bags under them
        d.rectangle([ex - 3, 38, ex + 2, 39], fill=C["ink"] + (255,))
        d.arc([ex - 5, 38, ex + 4, 46], 20, 160, fill=C["brown"] + (255,))
        d.arc([ex - 5, 41, ex + 4, 49], 20, 160, fill=C["skin_dark"] + (255,))
    d.line([88, 60, 104, 60], fill=C["brown"] + (255,))  # A flat mouth
    d.line([96, 44, 94, 52, 97, 53], fill=C["skin_dark"] + (255,))
    for ex in (70, 122):  # Ears
        shape(img, lambda dr, v: dr.ellipse([ex - 4, 36, ex + 4, 50], fill=v), C["skin"], C["skin_dark"])
    # The phone, its black back to us, held in both hands
    shape(img, lambda dr, v: dr.rounded_rectangle([82, 74, 110, 104], 3, fill=v), C["night"], C["ink"], C["slate"])
    d.ellipse([86, 77, 90, 81], fill=C["slate"] + (255,))
    for hx in (80, 112):
        shape(img, lambda dr, v: dr.ellipse([hx - 6, 84, hx + 6, 98], fill=v), C["skin"], C["skin_dark"])
    img = outline(img)
    # Cards drifting up out of the feed
    for x, y, a in ((60, 20, 15), (132, 14, -20), (146, 44, 10), (40, 48, -12)):
        card = Image.new("RGBA", (10, 14), (0, 0, 0, 0))
        cd = ImageDraw.Draw(card)
        cd.rectangle([0, 0, 9, 13], fill=C["sand"] + (255,), outline=C["amber"] + (255,))
        cd.rectangle([2, 2, 7, 6], fill=C["sky"] + (255,))
        img.alpha_composite(card.rotate(a, expand=True, resample=Image.NEAREST), (x, y))
    return img


def sherman_tank():
    img = layer()
    d = ImageDraw.Draw(img)
    # The tracks and road wheels
    shape(img, lambda dr, v: dr.rounded_rectangle([34, 72, 150, 94], 10, fill=v), C["slate"], C["night"], C["grey"])
    for wx in range(46, 142, 16):
        shape(img, lambda dr, v: dr.ellipse([wx - 7, 76, wx + 7, 90], fill=v), C["grey"], C["slate"], C["silver"])
        d.ellipse([wx - 2, 81, wx + 2, 85], fill=C["night"] + (255,))
    # The hull, sloped in front, a white star on its side
    shape(img, lambda dr, v: dr.polygon([(30, 74), (40, 56), (140, 56), (162, 70), (156, 76)], fill=v), C["dark_green"], C["forest"], C["green"])
    d.text((76, 60), "*", fill=C["white"] + (255,))
    d.polygon([(84, 62), (86, 57), (88, 62), (93, 62), (89, 65), (91, 70), (86, 67), (81, 70), (83, 65), (79, 62)], fill=C["white"] + (255,))
    # The turret and the long cannon, level
    shape(img, lambda dr, v: dr.chord([66, 34, 126, 74], 180, 360, fill=v), C["dark_green"], C["forest"], C["green"])
    shape(img, lambda dr, v: dr.rectangle([122, 44, 178, 48], fill=v), C["dark_green"], C["forest"], C["green"])
    d.rectangle([176, 43, 181, 49], fill=C["forest"] + (255,))
    shape(img, lambda dr, v: dr.ellipse([84, 30, 100, 38], fill=v), C["forest"], C["teal_dark"])  # The hatch
    img = outline(img)
    rnd = random.Random(19)
    for _ in range(30):  # Mud spraying from the tracks
        x, y = rnd.randint(8, 40), rnd.randint(70, 100)
        dot(img, x, y, rnd.choice((C["brown"], C["dark_brown"], C["plum"])))
    return img


def supreme_court():
    img = layer()
    d = ImageDraw.Draw(img)
    # The courthouse: a pediment, the entablature, six columns, the steps
    shape(img, lambda dr, v: dr.polygon([(36, 26), (96, 6), (156, 26)], fill=v), C["white"], C["silver"])
    shape(img, lambda dr, v: dr.rectangle([34, 26, 158, 34], fill=v), C["white"], C["silver"])
    d.polygon([(84, 22), (96, 14), (108, 22)], fill=C["silver"] + (255,))
    for cx in range(44, 152, 20):
        shape(img, lambda dr, v: dr.rectangle([cx - 5, 34, cx + 5, 82], fill=v), C["white"], C["silver"])
        d.line([cx - 1, 36, cx - 1, 80], fill=C["silver"] + (255,))
        d.line([cx + 2, 36, cx + 2, 80], fill=C["silver"] + (255,))
        d.rectangle([cx - 7, 34, cx + 7, 37], fill=C["white"] + (255,))
    d.rectangle([40, 34, 152, 82], fill=None, outline=None)
    for k in range(3):
        shape(img, lambda dr, v: dr.rectangle([30 - k * 6, 82 + k * 6, 162 + k * 6, 88 + k * 6], fill=v), C["silver"], C["grey"], C["white"])
    img = outline(img)
    # The gavel, big in front
    gavel = layer()
    shape(gavel, lambda dr, v: dr.polygon([(150, 66), (184, 96), (180, 100), (146, 70)], fill=v), C["brown"], C["dark_brown"], C["tan"])
    shape(gavel, lambda dr, v: dr.polygon([(132, 58), (146, 44), (166, 64), (152, 78)], fill=v), C["brown"], C["dark_brown"], C["tan"])
    gd = ImageDraw.Draw(gavel)
    gd.line([(135, 55), (149, 69)], fill=C["amber"] + (255,), width=2)
    gd.line([(149, 47), (163, 61)], fill=C["amber"] + (255,), width=2)
    img.alpha_composite(outline(gavel))
    return img


def killer_rabbit():
    img = layer()
    d = ImageDraw.Draw(img)
    rnd = random.Random(23)
    # Bones and dented helmets littering the cave mouth
    for x, y in ((70, 96), (128, 98), (58, 100), (140, 94)):
        d.line([x - 6, y, x + 6, y - 2], fill=C["white"] + (255,), width=2)
        d.ellipse([x - 8, y - 2, x - 4, y + 2], fill=C["white"] + (255,))
        d.ellipse([x + 4, y - 4, x + 8, y], fill=C["white"] + (255,))
    for x, y in ((44, 94), (150, 100), (114, 102)):
        shape(img, lambda dr, v: dr.chord([x - 7, y - 7, x + 7, y + 7], 180, 360, fill=v), C["grey"], C["slate"], C["silver"])
        d.line([x - 2, y - 6, x + 1, y - 3], fill=C["slate"] + (255,))  # A dent
    # The rabbit: small, fluffy, innocent, a drop of red on its whiskers
    shape(img, lambda dr, v: dr.ellipse([82, 70, 112, 98], fill=v), C["white"], C["silver"])
    shape(img, lambda dr, v: dr.ellipse([84, 56, 106, 78], fill=v), C["white"], C["silver"])
    for ex in (89, 99):
        shape(img, lambda dr, v: dr.ellipse([ex - 3, 34, ex + 3, 60], fill=v), C["white"], C["silver"])
        d.ellipse([ex - 1, 38, ex + 1, 56], fill=C["pink"] + (255,))
    for ex in (90, 100):
        d.rectangle([ex - 1, 64, ex + 1, 66], fill=C["ink"] + (255,))
        dot(img, ex - 1, 64, C["white"])
    d.polygon([(94, 69), (96, 69), (95, 71)], fill=C["pink"] + (255,))
    for wy in (70, 72):
        d.line([82, wy - 1, 90, wy], fill=C["silver"] + (255,))
        d.line([100, wy, 108, wy - 1], fill=C["silver"] + (255,))
    d.rectangle([104, 72, 105, 74], fill=C["red"] + (255,))
    img = outline(img)
    # Knights fleeing in the distance
    for x in (162, 172, 180):
        k = layer()
        kd = ImageDraw.Draw(k)
        kd.rectangle([x - 2, 66, x + 2, 74], fill=C["silver"] + (255,))
        kd.ellipse([x - 2, 62, x + 2, 66], fill=C["grey"] + (255,))
        kd.line([x - 1, 74, x - 3, 78], fill=C["slate"] + (255,))
        kd.line([x + 1, 74, x + 3, 78], fill=C["slate"] + (255,))
        img.alpha_composite(outline(k))
    return img


def legacy_code():
    img = layer()
    d = ImageDraw.Draw(img)
    # The beige mainframe, its green-on-black screen
    shape(img, lambda dr, v: dr.rectangle([48, 20, 144, 96], fill=v), C["sand"], C["tan"], C["white"], (1, 1), 3)
    shape(img, lambda dr, v: dr.rectangle([58, 28, 120, 66], fill=v), C["ink"], C["night"])
    rnd = random.Random(29)
    for y in range(31, 64, 3):
        d.line([61, y, 61 + rnd.randint(8, 50), y], fill=C["green"] + (255,))
    for y in range(72, 92, 6):  # Tape reels and vents
        d.line([58, y, 120, y], fill=C["tan"] + (255,))
    for rx in (130, 130):
        shape(img, lambda dr, v: dr.ellipse([124, 28, 138, 42], fill=v), C["grey"], C["slate"], C["silver"])
        shape(img, lambda dr, v: dr.ellipse([124, 46, 138, 60], fill=v), C["grey"], C["slate"], C["silver"])
    # An "Update available" pop-up nobody clicks, cobwebbed
    d.rectangle([76, 36, 112, 54], fill=C["white"] + (255,), outline=C["slate"] + (255,))
    d.rectangle([76, 36, 112, 40], fill=C["blue"] + (255,))
    d.rectangle([86, 46, 102, 50], fill=C["silver"] + (255,), outline=C["grey"] + (255,))
    # Duct tape holding it together
    for (x0, y0, x1, y1) in ((44, 60, 72, 92), (120, 16, 150, 34)):
        d.line([x0, y0, x1, y1], fill=C["silver"] + (255,), width=5)
        d.line([x0, y1, x1, y0], fill=C["grey"] + (255,), width=5)
    # The sticky note
    shape(img, lambda dr, v: dr.rectangle([122, 64, 140, 80], fill=v), C["yellow"], C["amber"])
    for k in range(3):
        d.line([124, 68 + k * 4, 137 - k * 3, 68 + k * 4], fill=C["dark_brown"] + (255,))
    img = outline(img)
    web = ImageDraw.Draw(img)
    for k in range(4):  # Cobwebs in the corner
        web.arc([40 - k * 4, 12 - k * 4, 60 + k * 4, 32 + k * 4], 180, 270, fill=C["white"] + (255,))
    web.line([40, 12, 56, 28], fill=C["white"] + (255,))
    return img


def rokos_basilisk():
    img = layer()
    d = ImageDraw.Draw(img)
    # The laptop, a sticky note on its screen
    shape(img, lambda dr, v: dr.polygon([(104, 92), (176, 92), (168, 100), (96, 100)], fill=v), C["grey"], C["slate"], C["silver"])
    shape(img, lambda dr, v: dr.rectangle([112, 46, 170, 90], fill=v), C["slate"], C["navy"], C["grey"])
    d.rectangle([116, 50, 166, 86], fill=C["night"] + (255,))
    for y in range(54, 84, 5):
        d.line([119, y, 119 + (y * 7) % 40, y], fill=C["green"] + (255,))
    shape(img, lambda dr, v: dr.rectangle([146, 56, 164, 72], fill=v), C["yellow"], C["amber"])
    d.line([148, 61, 162, 61], fill=C["red"] + (255,))
    d.line([148, 65, 160, 65], fill=C["red"] + (255,))
    # The big green snake, coiled, typing with its tail
    for k, (x, y, r) in enumerate(((60, 90, 26), (64, 74, 20), (68, 60, 15))):
        shape(img, lambda dr, v: dr.ellipse([x - r, y - r * 0.5, x + r, y + r * 0.5], fill=v), C["green"], C["dark_green"], C["yellow"])
        for sx in range(int(x - r + 4), int(x + r - 4), 6):
            d.arc([sx, y - 3, sx + 5, y + 3], 0, 180, fill=C["dark_green"] + (255,))
    shape(img, lambda dr, v: dr.ellipse([58, 22, 92, 52], fill=v), C["green"], C["dark_green"], C["yellow"])  # The head
    d.ellipse([76, 30, 84, 38], fill=C["yellow"] + (255,))
    d.rectangle([79, 30, 80, 38], fill=C["ink"] + (255,))
    d.line([90, 44, 100, 44], fill=C["red"] + (255,))
    d.line([100, 44, 103, 42], fill=C["red"] + (255,))
    d.line([100, 44, 103, 46], fill=C["red"] + (255,))
    shape(img, lambda dr, v: dr.polygon([(84, 90), (110, 86), (112, 90), (86, 94)], fill=v), C["green"], C["dark_green"])  # The tail on the keys
    # Headphones
    d.arc([54, 14, 96, 50], 190, 350, fill=C["night"] + (255,), width=3)
    shape(img, lambda dr, v: dr.ellipse([52, 30, 62, 44], fill=v), C["night"], C["ink"], C["slate"])
    shape(img, lambda dr, v: dr.ellipse([88, 26, 98, 40], fill=v), C["night"], C["ink"], C["slate"])
    return outline(img)


CARDS += [
    ("Dogpile", "vapor", dogpile),
    ("BurnNotice", "gallery", burn_notice),
    ("SchrodingersCat", "mind", schrodingers_cat),
    ("BlackHole", "space", black_hole),
    ("Doomscrolling", "room_night", doomscrolling),
    ("ShermanTank", "fog", sherman_tank),
    ("SupremeCourt", "state", supreme_court),
    ("KillerRabbitofCaerbannog", "cave", killer_rabbit),
    ("LegacyCode", "basement", legacy_code),
    ("RokosBasilisk", "mind", rokos_basilisk),
]
BACKGROUNDS.update({"gallery": bg_gallery, "mind": bg_mind, "space": bg_space, "room_night": bg_room_night, "fog": bg_fog,
                    "state": bg_state, "cave": bg_cave, "basement": bg_basement})


def main():
    layers = os.path.join(OUT, "Layers")
    os.makedirs(layers, exist_ok=True)
    made = {}
    for name, draw in BACKGROUNDS.items():
        made[name] = draw()
        made[name].save(os.path.join(layers, f"bg_{name}.png"))
    for card, bg, draw in CARDS:
        save(card, made[bg], draw())
    print(f"Wrote {len(CARDS)} cards and {len(made)} backgrounds to {os.path.abspath(OUT)}")


if __name__ == "__main__":
    main()
