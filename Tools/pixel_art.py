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
    clock = layer()
    shape(clock, lambda dr, v: dr.rectangle([10, 52, 50, 56], fill=v), C["brown"], C["dark_brown"], C["tan"])  # The shelf
    shape(clock, lambda dr, v: dr.rounded_rectangle([14, 36, 46, 52], 2, fill=v), C["night"], C["ink"], C["slate"])
    pixel_text(clock, 17, 41, "3:00", C["hot"])
    pixel_text(clock, 34, 41, "AM", C["red"])
    img.alpha_composite(outline(clock))
    d = ImageDraw.Draw(img)
    # Bojak: the bald wojak, a huge dome of a head with the face small at the bottom of it, lit blue by the phone
    shape(img, lambda dr, v: dr.ellipse([50, 80, 142, 150], fill=v), C["slate"], C["navy"], C["grey"])  # Hunched, in a grey tee
    for ex in (65, 127):  # Ears, low on the head
        shape(img, lambda dr, v: dr.ellipse([ex - 4, 46, ex + 4, 60], fill=v), C["sand"], C["tan"])
    shape(img, lambda dr, v: dr.ellipse([64, 2, 128, 84], fill=v), C["sand"], C["tan"], C["white"])
    for y, (x0, x1) in ((22, (84, 108)), (27, (82, 110))):  # Forehead creases
        d.arc([x0, y, x1, y + 6], 200, 340, fill=C["tan"] + (255,))
    d.line([(78, 47), (90, 44)], fill=C["ink"] + (255,))  # Brows, sad
    d.line([(102, 44), (114, 47)], fill=C["ink"] + (255,))
    for ex in (85, 107):
        d.arc([ex - 6, 48, ex + 6, 56], 200, 340, fill=C["ink"] + (255,))  # Heavy lids
        d.rectangle([ex - 1, 51, ex, 52], fill=C["ink"] + (255,))
        d.arc([ex - 6, 51, ex + 6, 59], 20, 160, fill=C["tan"] + (255,))  # Bags under them
        d.arc([ex - 5, 54, ex + 5, 62], 30, 150, fill=C["tan"] + (255,))
    d.line([(96, 50), (94, 61), (98, 62)], fill=C["ink"] + (255,))  # The nose
    d.arc([90, 67, 102, 73], 200, 340, fill=C["ink"] + (255,))  # A small frown
    d.arc([88, 73, 104, 81], 30, 150, fill=C["tan"] + (255,))  # The chin
    # The phone, its black back to us, held in both hands
    shape(img, lambda dr, v: dr.rounded_rectangle([82, 78, 110, 106], 3, fill=v), C["night"], C["ink"], C["slate"])
    d.ellipse([86, 81, 90, 85], fill=C["slate"] + (255,))
    for hx in (80, 112):
        shape(img, lambda dr, v: dr.ellipse([hx - 6, 86, hx + 6, 100], fill=v), C["sand"], C["tan"])
    return outline(img)


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
    smoke = layer()  # Exhaust smoke billowing out behind it
    for x, y, r, col in ((22, 60, 10, C["grey"]), (12, 46, 12, C["silver"]), (28, 44, 8, C["silver"]), (6, 30, 10, C["white"]),
                         (20, 26, 8, C["silver"]), (34, 56, 7, C["grey"])):
        shape(smoke, lambda dr, v: dr.ellipse([x - r, y - r, x + r, y + r], fill=v), col, C["grey"], C["white"])
    smoke = outline(smoke, C["slate"])
    smoke.alpha_composite(img)
    img = smoke
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
    return img


def crusader_skeleton(img, x, y, pose, helm=True, flip=1):
    """A dead crusader: a skeleton in a white tabard with a red cross, a bucket helm (or a bare skull). pose: "sitting"
    against the rock, slumped; "lying" flat on its back."""
    d = ImageDraw.Draw(img)
    bone, cross = C["white"], C["red"]
    if pose == "sitting":
        # Legs out in front, the torso slumped, the head tipped over
        d.line([(x, y), (x + flip * 12, y + 2), (x + flip * 14, y + 8)], fill=bone + (255,), width=2)
        d.line([(x + 2, y - 2), (x + flip * 10, y - 4), (x + flip * 12, y + 4)], fill=bone + (255,), width=2)
        shape(img, lambda dr, v: dr.polygon([(x - 5, y), (x + 5, y), (x + 4, y - 16), (x - 4, y - 16)], fill=v), C["white"], C["silver"])
        d.line([(x, y - 14), (x, y - 4)], fill=cross + (255,), width=2)
        d.line([(x - 3, y - 11), (x + 3, y - 11)], fill=cross + (255,), width=2)
        d.line([(x - 4, y - 14), (x - 8, y - 4)], fill=bone + (255,))  # An arm hanging down
        d.line([(x + 4, y - 14), (x + flip * 9, y - 6)], fill=bone + (255,))
        hx, hy = x - flip * 2, y - 21
    else:
        # On its back, the head toward the cave, a sword beside it
        shape(img, lambda dr, v: dr.polygon([(x, y - 4), (x + flip * 16, y - 4), (x + flip * 16, y + 3), (x, y + 3)], fill=v), C["white"], C["silver"])
        d.line([(x + flip * 6, y - 3), (x + flip * 6, y + 2)], fill=cross + (255,), width=2)
        d.line([(x + flip * 3, y), (x + flip * 9, y)], fill=cross + (255,), width=2)
        for dy in (-2, 2):
            d.line([(x + flip * 16, y + dy), (x + flip * 28, y + dy * 2)], fill=bone + (255,), width=2)
        d.line([(x + flip * 4, y - 4), (x + flip * 2, y - 10)], fill=bone + (255,))
        d.line([(x - flip * 2, y + 6), (x + flip * 24, y + 6)], fill=C["silver"] + (255,))  # The sword
        d.line([(x + flip * 2, y + 4), (x + flip * 2, y + 8)], fill=C["amber"] + (255,))
        hx, hy = x - flip * 5, y - 1
    if helm:  # The great helm: a flat-topped bucket with an eye slit and a cross of breathing holes
        shape(img, lambda dr, v: dr.rectangle([hx - 5, hy - 5, hx + 5, hy + 5], fill=v), C["grey"], C["slate"], C["silver"])
        d.line([(hx - 4, hy - 1), (hx + 4, hy - 1)], fill=C["ink"] + (255,))
        d.line([(hx, hy + 1), (hx, hy + 4)], fill=C["ink"] + (255,))
    else:
        d.ellipse([hx - 4, hy - 4, hx + 4, hy + 4], fill=bone + (255,))
        d.rectangle([hx - 2, hy - 1, hx - 1, hy], fill=C["ink"] + (255,))
        d.rectangle([hx + 1, hy - 1, hx + 2, hy], fill=C["ink"] + (255,))


def killer_rabbit():
    img = layer()
    d = ImageDraw.Draw(img)
    rnd = random.Random(23)
    # Bones and dented helmets littering the cave mouth
    for x, y in ((70, 96), (128, 98), (58, 100), (140, 94)):
        d.line([x - 6, y, x + 6, y - 2], fill=C["white"] + (255,), width=2)
        d.ellipse([x - 8, y - 2, x - 4, y + 2], fill=C["white"] + (255,))
        d.ellipse([x + 4, y - 4, x + 8, y], fill=C["white"] + (255,))
    for x, y in ((150, 100),):
        shape(img, lambda dr, v: dr.chord([x - 7, y - 7, x + 7, y + 7], 180, 360, fill=v), C["grey"], C["slate"], C["silver"])
        d.line([x - 2, y - 6, x + 1, y - 3], fill=C["slate"] + (255,))  # A dent
    # Crusaders who tried: one slumped against each side of the cave mouth, one flat on his back, a fourth helmless
    crusader_skeleton(img, 56, 92, "sitting", helm=True, flip=1)
    crusader_skeleton(img, 140, 90, "sitting", helm=False, flip=-1)
    crusader_skeleton(img, 30, 100, "lying", helm=True, flip=1)
    crusader_skeleton(img, 170, 100, "lying", helm=True, flip=-1)
    # The rabbit: small, fluffy, innocent, a drop of red on its whiskers
    shape(img, lambda dr, v: dr.ellipse([87, 78, 108, 98], fill=v), C["white"], C["silver"])
    shape(img, lambda dr, v: dr.ellipse([88, 68, 104, 84], fill=v), C["white"], C["silver"])
    for ex in (92, 99):
        shape(img, lambda dr, v: dr.ellipse([ex - 2, 52, ex + 2, 71], fill=v), C["white"], C["silver"])
        d.line([ex, 55, ex, 68], fill=C["pink"] + (255,))
    for ex in (93, 99):
        d.rectangle([ex - 1, 74, ex, 75], fill=C["ink"] + (255,))
        dot(img, ex - 1, 74, C["white"])
    d.rectangle([96, 78, 96, 78], fill=C["pink"] + (255,))
    for wy in (78, 80):
        d.line([85, wy - 1, 92, wy], fill=C["silver"] + (255,))
        d.line([100, wy, 107, wy - 1], fill=C["silver"] + (255,))
    d.rectangle([102, 80, 103, 81], fill=C["red"] + (255,))
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


# A 3x5 pixel font for the few words drawn into pictures (clocks, signs, scroll titles)
FONT3X5 = {
    "0": ["111", "101", "101", "101", "111"], "1": ["010", "110", "010", "010", "111"], "2": ["111", "001", "111", "100", "111"],
    "3": ["111", "001", "111", "001", "111"], "4": ["101", "101", "111", "001", "001"], "5": ["111", "100", "111", "001", "111"],
    "6": ["111", "100", "111", "101", "111"], "7": ["111", "001", "010", "010", "010"], "8": ["111", "101", "111", "101", "111"],
    "9": ["111", "101", "111", "001", "111"], ":": ["0", "1", "0", "1", "0"], " ": ["00", "00", "00", "00", "00"],
    "A": ["010", "101", "111", "101", "101"], "B": ["110", "101", "110", "101", "110"], "D": ["110", "101", "101", "101", "110"],
    "E": ["111", "100", "110", "100", "111"], "G": ["011", "100", "101", "101", "011"], "H": ["101", "101", "111", "101", "101"],
    "M": ["101", "111", "111", "101", "101"], "N": ["101", "111", "111", "111", "101"], "O": ["010", "101", "101", "101", "010"],
    "R": ["110", "101", "110", "101", "101"], "S": ["011", "100", "010", "001", "110"],
    "C": ["011", "100", "100", "100", "011"], "F": ["111", "100", "110", "100", "100"], "I": ["111", "010", "010", "010", "111"],
    "K": ["101", "110", "100", "110", "101"], "L": ["100", "100", "100", "100", "111"], "P": ["110", "101", "110", "100", "100"],
    "T": ["111", "010", "010", "010", "010"], "U": ["101", "101", "101", "101", "111"], "W": ["101", "101", "111", "111", "101"],
    "X": ["101", "101", "010", "101", "101"], "Y": ["101", "101", "010", "010", "010"], "'": ["1", "1", "0", "0", "0"],
}


def pixel_text(img, x, y, text, color, scale=1):
    """Writes text in the 3x5 pixel font, one pixel (or scale x scale) per dot."""
    px = img.load()
    for ch in text:
        glyph = FONT3X5.get(ch.upper(), FONT3X5[" "])
        for gy, row in enumerate(glyph):
            for gx, bit in enumerate(row):
                if bit == "1":
                    for sy in range(scale):
                        for sx in range(scale):
                            xx, yy = x + gx * scale + sx, y + gy * scale + sy
                            if 0 <= xx < W and 0 <= yy < H:
                                px[xx, yy] = color + (255,)
        x += (len(glyph[0]) + 1) * scale


# "Here be dragons", ASCII art by snd, the user's reference: drawn one dot per character on the Legacy Code scroll
HERE_BE_DRAGONS = r"""
 <>=======()
(/\___   /|\\          ()==========<>_
      \_/ | \\        //|\   ______/ \)
        \_|  \\      // | \_/
          \|\/|\_   //  /\/
           (oo)\ \_//  /
          //_/\_\/ /  |
         @@/  |=\  \  |
              \_=\_ \ |
                \==\ \|\_ snd
             __(\===\(  )\
            (((~) __(_/   |
                 (((~) \  /
                 ______/ /
                 '------'
"""


def legacy_code():
    frame = layer()
    d = ImageDraw.Draw(frame)
    # The beige mainframe, its green-on-black screen
    shape(frame, lambda dr, v: dr.rectangle([48, 20, 144, 96], fill=v), C["sand"], C["tan"], C["white"], (1, 1), 3)
    shape(frame, lambda dr, v: dr.rectangle([58, 28, 120, 66], fill=v), C["ink"], C["night"])
    rnd = random.Random(29)
    for y in range(31, 64, 3):
        d.line([61, y, 61 + rnd.randint(8, 50), y], fill=C["green"] + (255,))
    for y in range(72, 92, 6):  # Vents
        d.line([58, y, 120, y], fill=C["tan"] + (255,))
    shape(frame, lambda dr, v: dr.ellipse([124, 28, 138, 42], fill=v), C["grey"], C["slate"], C["silver"])  # Tape reels
    shape(frame, lambda dr, v: dr.ellipse([124, 46, 138, 60], fill=v), C["grey"], C["slate"], C["silver"])
    # An "Update available" pop-up nobody clicks
    d.rectangle([76, 36, 112, 54], fill=C["white"] + (255,), outline=C["slate"] + (255,))
    d.rectangle([76, 36, 112, 40], fill=C["blue"] + (255,))
    d.rectangle([86, 46, 102, 50], fill=C["silver"] + (255,), outline=C["grey"] + (255,))
    # The sticky note
    shape(frame, lambda dr, v: dr.rectangle([122, 64, 140, 80], fill=v), C["yellow"], C["amber"])
    for k in range(3):
        d.line([124, 68 + k * 4, 137 - k * 3, 68 + k * 4], fill=C["dark_brown"] + (255,))
    frame = outline(frame)
    img = layer()
    img.alpha_composite(frame, (-26, 0))  # The mainframe to the left, the scroll on the wall beside it
    # The scroll: HERE BE DRAGONS over the old ASCII dragon, pinned up where the docs should be
    scroll = layer()
    sx0, sy0, sx1, sy1 = 126, 8, 188, 88
    shape(scroll, lambda dr, v: dr.rectangle([sx0, sy0 + 3, sx1, sy1 - 3], fill=v), C["sand"], C["tan"], C["white"])
    for yy in (sy0, sy1 - 6):  # The rolled ends
        shape(scroll, lambda dr, v: dr.rounded_rectangle([sx0 - 3, yy, sx1 + 3, yy + 6], 3, fill=v), C["tan"], C["brown"], C["sand"])
    pixel_text(scroll, sx0 + 4, sy0 + 10, "HERE BE", C["dark_brown"])
    pixel_text(scroll, sx0 + 4, sy0 + 17, "DRAGONS", C["red"])
    art = [line for line in HERE_BE_DRAGONS.split("\n") if line.strip()]
    ax, ay = sx0 + 6, sy0 + 26
    spx = scroll.load()
    for row, line in enumerate(art):
        for col, ch in enumerate(line):
            if ch != " " and col < (sx1 - ax - 3):
                for k in range(2):  # One dot per character, two pixels tall like a letter
                    if row * 3 + k < (sy1 - ay - 8):
                        spx[ax + col, ay + row * 3 + k] = (C["brown"] if ch not in "@o" else C["red"]) + (255,)
    scroll = outline(scroll)
    sd = ImageDraw.Draw(scroll)
    sd.ellipse([155, 9, 159, 13], fill=C["red"] + (255,))  # The pin
    img.alpha_composite(scroll)
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



# ---------------------------------------------------------------- backgrounds (batch 3)

def bg_jail():
    """A cell: a grey block wall, a barred window, a concrete floor."""
    img = layer()
    gradient(img, [C["grey"], C["slate"]], 0, 88)
    d = ImageDraw.Draw(img)
    for y in range(4, 88, 10):
        off = 0 if (y // 10) % 2 else 12
        for x in range(-off, W, 24):
            d.rectangle([x, y, x + 23, y + 9], outline=C["slate"] + (255,))
    d.rectangle([84, 10, 108, 30], fill=C["sky"] + (255,))
    for x in range(87, 108, 5):
        d.line([x, 10, x, 30], fill=C["night"] + (255,), width=2)
    d.rectangle([0, 88, W, H], fill=C["navy"] + (255,))
    return img


def bg_field():
    """A sunny field: blue sky, clouds, grass."""
    img = layer()
    gradient(img, [C["sky"], C["cyan"]], 0, 78)
    d = ImageDraw.Draw(img)
    for cx, cy in ((30, 18), (150, 12), (100, 30)):
        for dx, r in ((-8, 6), (0, 8), (8, 6)):
            d.ellipse([cx + dx - r, cy - r, cx + dx + r, cy + r], fill=C["white"] + (255,))
    gradient(img, [C["green"], C["dark_green"]], 78, H)
    rnd = random.Random(31)
    for _ in range(40):
        x, y = rnd.randrange(W), rnd.randint(80, H - 2)
        d.line([x, y, x + 1, y - 2], fill=C["forest"] + (255,))
    return img


def bg_forest():
    """A forest glade by day: trunks, the canopy closing overhead, grass and a dirt path. No water."""
    img = layer()
    gradient(img, [C["forest"], C["dark_green"], C["green"]], 0, 84)
    d = ImageDraw.Draw(img)
    rnd = random.Random(37)
    for x in range(-6, W, 20):  # Far trunks, dim
        xx = x + rnd.randint(-4, 4)
        d.rectangle([xx, 6, xx + 3, 84], fill=C["forest"] + (255,))
    for x in range(8, W, 36):  # Near trunks
        xx, w = x + rnd.randint(-6, 6), rnd.randint(6, 9)
        d.rectangle([xx, 0, xx + w, 86], fill=C["dark_brown"] + (255,))
        d.line([xx + 1, 0, xx + 1, 86], fill=C["brown"] + (255,))
        d.polygon([(xx - 3, 86), (xx, 80), (xx + w, 80), (xx + w + 3, 86)], fill=C["dark_brown"] + (255,))  # Roots
    for _ in range(30):  # The canopy
        x, y, r = rnd.randrange(-10, W + 10), rnd.randint(-8, 14), rnd.randint(10, 18)
        d.ellipse([x - r, y - r // 2 - 4, x + r, y + r // 2 + 4], fill=rnd.choice((C["forest"], C["dark_green"], C["teal_dark"])) + (255,))
    gradient(img, [C["green"], C["dark_green"]], 84, H)
    d.polygon([(76, 84), (116, 84), (150, H), (52, H)], fill=C["tan"] + (255,))  # The path
    d.line([(76, 84), (52, H)], fill=C["brown"] + (255,))
    d.line([(116, 84), (150, H)], fill=C["brown"] + (255,))
    for _ in range(50):  # Grass tufts
        x, y = rnd.randrange(W), rnd.randint(86, H - 2)
        if not (60 < x < 140):
            d.line([x, y, x + 1, y - 2], fill=C["forest"] + (255,))
    return img


# ---------------------------------------------------------------- subjects (batch 3)

def man(img, x, y, scale, skin, shirt, hair=None, face="neutral", tie=None, jacket=None):
    """A standing cartoon man, his feet at (x, y)."""
    s = scale
    d = ImageDraw.Draw(img)
    for side in (-1, 1):  # Legs
        shape(img, lambda dr, v: dr.rectangle([x + side * 2 * s - 2 * s, y - 14 * s, x + side * 2 * s + 1 * s, y], fill=v), jacket or C["navy"], C["night"])
    body = jacket or shirt
    shape(img, lambda dr, v: dr.rounded_rectangle([x - 7 * s, y - 32 * s, x + 7 * s, y - 13 * s], 3 * s, fill=v), body, C["night"] if jacket else C["slate"])
    if jacket:
        d.polygon([(x - 3 * s, y - 32 * s), (x + 3 * s, y - 32 * s), (x, y - 24 * s)], fill=shirt + (255,))
    if tie:
        d.polygon([(x - 1 * s, y - 31 * s), (x + 1 * s, y - 31 * s), (x + 1.5 * s, y - 22 * s), (x, y - 20 * s), (x - 1.5 * s, y - 22 * s)], fill=tie + (255,))
    shape(img, lambda dr, v: dr.ellipse([x - 6 * s, y - 45 * s, x + 6 * s, y - 32 * s], fill=v), skin, C["skin_dark"], C["white"])
    if hair:
        d.chord([x - 6 * s, y - 46 * s, x + 6 * s, y - 36 * s], 180, 360, fill=hair + (255,))
    for side in (-1, 1):
        d.rectangle([x + side * 2.5 * s - 0.5, y - 40 * s, x + side * 2.5 * s + 0.5, y - 39 * s], fill=C["ink"] + (255,))
    if face == "smile":
        d.arc([x - 3 * s, y - 39 * s, x + 3 * s, y - 34 * s], 20, 160, fill=C["ink"] + (255,))
    elif face == "shout":
        d.ellipse([x - 1.5 * s, y - 37 * s, x + 1.5 * s, y - 34 * s], fill=C["wine"] + (255,))
    else:
        d.line([x - 2 * s, y - 35 * s, x + 2 * s, y - 35 * s], fill=C["ink"] + (255,))


def placebo():
    img = layer()
    d = ImageDraw.Draw(img)
    # A spoonful of sugar and nothing else: the handle rising off to the top right, the bowl's tip to the left, heaped high
    shape(img, lambda dr, v: dr.polygon([(128, 66), (180, 4), (188, 2), (190, 8), (138, 72)], fill=v), C["silver"], C["grey"], C["white"])
    d.line([(134, 64), (184, 5)], fill=C["white"] + (255,))
    cx, cy, rx, ry = 84, 74, 58, 18
    bowl = [(cx + rx * math.cos(a), cy + ry * math.sin(a) * (0.7 + 0.3 * math.cos(a))) for a in (k * math.pi / 40 for k in range(80))]
    shape(img, lambda dr, v: dr.polygon(bowl, fill=v), C["silver"], C["grey"], C["white"])
    d.line([(x, y - 1) for x, y in bowl[6:36]], fill=C["white"] + (255,))  # The light along its lip
    rnd = random.Random(43)
    top, bottom = [], []
    for x in range(cx - rx + 6, cx + rx - 3):
        f = (x - cx) / rx
        h = math.sqrt(max(0, 1 - f * f))
        top.append((x, cy - 4 - 30 * h ** 1.4 * (1 - 0.1 * f) + rnd.choice((-1, 0, 0, 1))))
        bottom.append((x, cy + 8 * h))
    sugar = shape(img, lambda dr, v: dr.polygon(top + bottom[::-1], fill=v), C["white"], C["silver"])
    sm = sugar.load()
    for _ in range(320):  # Grains, thicker on the shaded side
        x, y = rnd.randint(30, 140), rnd.randint(36, 84)
        if sm[x, y] and rnd.random() < 0.35 + (x - 30) / 160:
            dot(img, x, y, rnd.choice((C["silver"], C["silver"], C["sand"], C["grey"])))
    for x, y in ((62, 54), (86, 46), (106, 60)):  # Glints
        for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
            dot(img, x + dx, y + dy, C["cyan"] if (dx, dy) == (0, 0) else C["sky"])
    return outline(img)


def business_expense():
    img = layer()
    d = ImageDraw.Draw(img)
    # The CEO, three-piece suit, briefcase in hand, walked into the cell...
    man(img, 74, 98, 1.6, C["skin"], C["white"], C["silver"], face="smile", tie=C["red"], jacket=C["night"])
    shape(img, lambda dr, v: dr.rectangle([46, 66, 62, 78], fill=v), C["brown"], C["dark_brown"], C["tan"])  # The briefcase
    d.rectangle([52, 63, 56, 66], outline=C["dark_brown"] + (255,))
    d.line([(64, 58), (54, 64)], fill=C["night"] + (255,), width=3)
    # ...handing the guard a receipt stamped BUSINESS EXPENSE
    man(img, 136, 98, 1.6, C["skin_dark"], C["sky"], C["ink"], face="neutral", jacket=C["blue"])
    d.rectangle([128, 22, 144, 26], fill=C["blue"] + (255,))  # The cap
    d.line([(86, 50), (102, 48)], fill=C["night"] + (255,), width=3)
    shape(img, lambda dr, v: dr.rectangle([100, 38, 114, 56], fill=v), C["white"], C["silver"])
    for k in range(4):
        d.line([102, 42 + k * 3, 112, 42 + k * 3], fill=C["grey"] + (255,))
    d.rectangle([102, 50, 112, 54], outline=C["red"] + (255,))
    d.line([(104, 52), (110, 52)], fill=C["red"] + (255,))
    img = outline(img)
    # The cell door's bars in front of them
    bars = layer()
    bd = ImageDraw.Draw(bars)
    for x in range(10, 40, 7):
        bd.rectangle([x, 0, x + 2, 98], fill=C["grey"] + (255,))
    bd.rectangle([6, 40, 42, 44], fill=C["grey"] + (255,))
    img.alpha_composite(outline(bars, C["night"]))
    return img


def golden_dome():
    img = layer()
    d = ImageDraw.Draw(img)
    # The Golden Dome over the city: a lattice shell of gold light, missiles bursting on it
    cx, base, rx, ry = 66, 98, 64, 64
    dome = mask()
    ImageDraw.Draw(dome).chord([cx - rx, base - ry, cx + rx, base + ry], 180, 360, fill=255)
    px, mp = img.load(), dome.load()
    for y in range(H):
        for x in range(W):
            if mp[x, y] and BAYER[y % 4][x % 4] < 3:
                px[x, y] = C["yellow"] + (255,)
    for k in (1, 2, 3):  # Rings round it
        h = ry * k / 4
        w = rx * math.sqrt(1 - (h / ry) ** 2)
        d.arc([cx - w, base - h - 6 + k, cx + w, base - h + 6 - k], 0, 180, fill=C["amber"] + (255,))
    for s in (0.35, 0.72):  # Ribs over it
        d.arc([cx - rx * s, base - ry, cx + rx * s, base + ry], 180, 360, fill=C["amber"] + (255,))
    d.line([cx, base - ry, cx, base], fill=C["amber"] + (255,))
    d.arc([cx - rx, base - ry, cx + rx, base + ry], 180, 360, fill=C["yellow"] + (255,), width=2)
    for deg, (tx, ty) in ((150, (-14, -10)), (100, (-4, -16)), (55, (10, -14))):  # Missiles, bursting on the shell
        ex, ey = cx + rx * math.cos(math.radians(deg)), base - ry * math.sin(math.radians(deg))
        d.line([ex + tx * 2, ey + ty * 2, ex, ey], fill=C["grey"] + (255,))
        for r, col in ((6, C["orange"]), (4, C["yellow"]), (2, C["white"])):
            d.ellipse([ex - r, ey - r, ex + r, ey + r], fill=col + (255,))
    d.line([10, 4, 18, 14], fill=C["silver"] + (255,), width=2)  # One more on its way
    d.polygon([(17, 14), (20, 13), (21, 18)], fill=C["red"] + (255,))
    # Netanyahu in front of it, pleased with himself: silver hair swept back, dark suit, red tie, the flag pin
    shape(img, lambda dr, v: dr.polygon([(122, H), (126, 80), (138, 70), (174, 70), (186, 80), (190, H)], fill=v), C["night"], C["ink"], C["navy"])
    d.rectangle([150, 58, 162, 72], fill=C["skin_dark"] + (255,))  # The neck
    d.polygon([(146, 70), (166, 70), (156, 94)], fill=C["white"] + (255,))
    d.polygon([(154, 72), (158, 72), (159, 76), (153, 76)], fill=C["red"] + (255,))
    d.polygon([(154, 76), (158, 76), (160, 90), (156, 94), (152, 90)], fill=C["red"] + (255,))
    d.line([(146, 70), (156, 94)], fill=C["navy"] + (255,))
    d.line([(166, 70), (156, 94)], fill=C["navy"] + (255,))
    d.rectangle([169, 80, 174, 84], fill=C["white"] + (255,))
    d.line([169, 80, 174, 80], fill=C["blue"] + (255,))
    d.line([169, 84, 174, 84], fill=C["blue"] + (255,))
    dot(img, 171, 82, C["blue"])
    for ex in (138, 174):  # Ears
        shape(img, lambda dr, v: dr.ellipse([ex - 4, 32, ex + 4, 48], fill=v), C["skin"], C["skin_dark"])
    shape(img, lambda dr, v: dr.ellipse([140, 14, 172, 64], fill=v), C["skin"], C["skin_dark"])
    hair = [(140, 32), (139, 20), (144, 12), (152, 8), (162, 8), (169, 12), (173, 20), (172, 32), (169, 24), (162, 20), (150, 20), (143, 24)]
    shape(img, lambda dr, v: dr.polygon(hair, fill=v), C["silver"], C["grey"], C["white"])
    for x0, x1, y in ((146, 156, 14), (154, 166, 12), (148, 160, 17)):  # Combed back
        d.line([x0, y, x1, y - 2], fill=C["grey"] + (255,))
    d.line([150, 25, 162, 25], fill=C["skin_dark"] + (255,))  # Forehead lines
    d.line([152, 27, 160, 27], fill=C["skin_dark"] + (255,))
    d.line([(145, 31), (152, 30)], fill=C["grey"] + (255,), width=2)  # Brows, one cocked
    d.line([(160, 29), (164, 27), (168, 30)], fill=C["grey"] + (255,), width=2)
    for x0 in (146, 161):  # Narrowed eyes, a little bag under each
        d.line([x0, 34, x0 + 5, 34], fill=C["skin_dark"] + (255,))
        d.line([x0, 35, x0 + 5, 35], fill=C["ink"] + (255,))
        dot(img, x0 + 3, 36, C["dark_brown"])
        d.arc([x0 - 1, 35, x0 + 6, 40], 20, 160, fill=C["skin_dark"] + (255,))
    d.line([(156, 32), (155, 44), (158, 45)], fill=C["skin_dark"] + (255,))  # The nose
    dot(img, 154, 44, C["brown"])
    d.line([(147, 44), (146, 52)], fill=C["skin_dark"] + (255,))  # Lines from the nose down
    d.line([(165, 44), (166, 51)], fill=C["skin_dark"] + (255,))
    d.line([(149, 51), (152, 52), (158, 52), (162, 49)], fill=C["ink"] + (255,))  # The smirk
    d.arc([146, 52, 166, 64], 20, 160, fill=C["skin_dark"] + (255,))  # Jowls
    return outline(img)


def surveillance_giraffe():
    img = layer()
    d = ImageDraw.Draw(img)
    # The legs and body at the bottom, the neck running out of the top of the picture...
    for lx in (52, 62, 92, 102):
        shape(img, lambda dr, v: dr.rectangle([lx, 82, lx + 4, H], fill=v), C["amber"], C["orange_brown"])
    shape(img, lambda dr, v: dr.ellipse([44, 66, 112, 92], fill=v), C["amber"], C["orange_brown"], C["yellow"])
    shape(img, lambda dr, v: dr.polygon([(96, 72), (104, 0), (114, 0), (108, 76)], fill=v), C["amber"], C["orange_brown"], C["yellow"])
    # ...and back in from the top further along, the head hanging upside down, peeking
    shape(img, lambda dr, v: dr.polygon([(136, 0), (146, 0), (144, 46), (138, 46)], fill=v), C["amber"], C["orange_brown"], C["yellow"])
    # The coat: brown patches parted by pale lines, kept inside the body so nothing pokes out of its outline
    coat = img.split()[3].point(lambda v: 255 if v else 0)
    inner = coat
    for dx, dy in ((2, 0), (-2, 0), (0, 2), (0, -2)):
        inner = ImageChops.multiply(inner, shifted(coat, dx, dy))
    rnd = random.Random(53)
    seeds = [(x + rnd.randint(-2, 2), y + rnd.randint(-2, 2)) for y in range(-4, H + 8, 8) for x in range(36, 160, 8)]
    px, im = img.load(), inner.load()
    for y in range(H):
        for x in range(W):
            if im[x, y]:
                ds = sorted((sx - x) ** 2 + (sy - y) ** 2 for sx, sy in seeds[:])[:2]
                if math.sqrt(ds[1]) - math.sqrt(ds[0]) > 1.6:
                    px[x, y] = C["orange_brown"] + (255,)
    shape(img, lambda dr, v: dr.ellipse([128, 42, 156, 66], fill=v), C["amber"], C["orange_brown"], C["yellow"])
    shape(img, lambda dr, v: dr.ellipse([134, 58, 150, 72], fill=v), C["tan"], C["brown"])  # Muzzle, at the bottom: upside down
    for ex in (136, 148):
        d.rectangle([ex - 1, 52, ex + 1, 54], fill=C["ink"] + (255,))
        dot(img, ex - 1, 54, C["white"])
    for ox in (134, 150):  # Ossicones pointing down
        d.line([ox, 44, ox - (2 if ox < 140 else -2), 38], fill=C["brown"] + (255,), width=2)
    return outline(img)


def inflatable_tank():
    img = layer()
    d = ImageDraw.Draw(img)
    # A rubber tank, half-deflated, its barrel drooping
    shape(img, lambda dr, v: dr.ellipse([40, 70, 140, 98], fill=v), C["green"], C["dark_green"], C["yellow"])
    shape(img, lambda dr, v: dr.ellipse([50, 54, 136, 82], fill=v), C["green"], C["dark_green"], C["yellow"])
    shape(img, lambda dr, v: dr.ellipse([70, 40, 110, 64], fill=v), C["green"], C["dark_green"], C["yellow"])
    shape(img, lambda dr, v: dr.polygon([(108, 50), (130, 52), (150, 66), (154, 80), (146, 80), (140, 68), (124, 58), (106, 56)], fill=v),
          C["green"], C["dark_green"], C["yellow"])
    for x in range(52, 132, 14):
        d.ellipse([x, 86, x + 8, 94], outline=C["forest"] + (255,))
    d.line([(96, 48), (100, 44), (104, 48)], fill=C["forest"] + (255,))  # A wrinkle
    d.ellipse([60, 60, 66, 64], fill=C["dark_green"] + (255,))  # The hole
    # The soldier patching it with a bicycle repair kit
    man(img, 40, 98, 1.0, C["skin"], C["dark_green"], C["dark_brown"], face="neutral", jacket=C["forest"])
    d.chord([33, 50, 47, 60], 180, 360, fill=C["forest"] + (255,))
    shape(img, lambda dr, v: dr.rectangle([50, 60, 58, 64], fill=v), C["red"], C["wine"])  # The patch
    d.line([(46, 70), (52, 62)], fill=C["skin"] + (255,), width=2)
    shape(img, lambda dr, v: dr.rectangle([24, 92, 34, 98], fill=v), C["sky"], C["blue"])  # The kit
    img = outline(img)
    for k in range(3):  # Air hissing out
        d2 = ImageDraw.Draw(img)
        d2.arc([62 - k * 5, 54 - k * 5, 70 + k * 3, 62 + k * 3], 200, 280, fill=C["white"] + (255,))
    return img


def keyboard_warrior():
    img = layer()
    d = ImageDraw.Draw(img)
    # Rambo gone soft: long black hair, the red headband, shirtless, a belly, a bandolier, camo trousers
    hair = [(74, 18), (106, 18), (114, 54), (104, 46), (98, 50), (82, 50), (76, 46), (66, 54)]
    shape(img, lambda dr, v: dr.polygon(hair, fill=v), C["night"], C["ink"], C["slate"])
    shape(img, lambda dr, v: dr.rectangle([56, 98, 124, H], fill=v), C["dark_green"], C["forest"], C["green"])
    rnd = random.Random(61)
    for _ in range(10):  # Camo blotches
        x, y = rnd.randint(58, 120), rnd.randint(100, H)
        d.ellipse([x - 3, y - 2, x + 3, y + 2], fill=rnd.choice((C["forest"], C["brown"])) + (255,))
    shape(img, lambda dr, v: dr.ellipse([56, 40, 124, 82], fill=v), C["skin"], C["skin_dark"])  # Chest
    shape(img, lambda dr, v: dr.ellipse([54, 56, 126, 110], fill=v), C["skin"], C["skin_dark"])  # The belly
    for x0 in (66, 92):
        d.arc([x0, 52, x0 + 22, 66], 20, 160, fill=C["skin_dark"] + (255,))
    d.arc([88, 92, 94, 96], 0, 180, fill=C["skin_dark"] + (255,))  # Navel
    d.line([(64, 46), (112, 102)], fill=C["dark_brown"] + (255,), width=5)  # The bandolier
    for k in range(9):
        x, y = 66 + k * 5.3, 48 + k * 6
        d.rectangle([x - 1, y - 2, x + 1, y + 1], fill=C["amber"] + (255,))
        dot(img, x, y - 3, C["silver"])
    # The head: double chin, scowl, gritted teeth, sweating
    shape(img, lambda dr, v: dr.ellipse([76, 14, 104, 48], fill=v), C["skin"], C["skin_dark"], C["white"])
    d.arc([80, 38, 100, 50], 20, 160, fill=C["skin_dark"] + (255,))
    d.chord([74, 10, 106, 30], 180, 360, fill=C["night"] + (255,))
    shape(img, lambda dr, v: dr.rectangle([75, 21, 105, 25], fill=v), C["red"], C["wine"])  # The headband, its tails flying
    shape(img, lambda dr, v: dr.polygon([(76, 22), (58, 18), (56, 24), (68, 26), (76, 25)], fill=v), C["red"], C["wine"])
    shape(img, lambda dr, v: dr.polygon([(76, 24), (60, 30), (63, 33), (76, 26)], fill=v), C["red"], C["wine"])
    d.line([(82, 28), (88, 30)], fill=C["ink"] + (255,))
    d.line([(92, 30), (98, 28)], fill=C["ink"] + (255,))
    for ex in (85, 94):
        d.rectangle([ex, 31, ex + 1, 32], fill=C["ink"] + (255,))
    d.line([(90, 32), (89, 37), (91, 38)], fill=C["skin_dark"] + (255,))
    d.rectangle([86, 41, 94, 43], fill=C["white"] + (255,), outline=C["ink"] + (255,))
    for x, y in ((80, 40), (83, 44), (97, 44), (100, 40), (88, 46), (92, 46)):
        dot(img, x, y, C["skin_dark"])
    shape(img, lambda dr, v: dr.ellipse([101, 28, 103, 32], fill=v), C["sky"], C["blue"])
    img = outline(img)
    # Fat arms, both fists on the keyboard's end, held up and forward like a sword (their own outline, over the body)
    body, img = img, layer()
    d = ImageDraw.Draw(img)
    shape(img, lambda dr, v: dr.line([(62, 48), (62, 64), (84, 72)], fill=v, width=10, joint="curve"), C["skin_dark"], C["brown"], C["skin"], (0, 1), 2)
    shape(img, lambda dr, v: dr.line([(118, 48), (118, 64), (98, 70)], fill=v, width=10, joint="curve"), C["skin_dark"], C["brown"], C["skin"], (0, 1), 2)
    a = math.radians(55)
    ux, uy, nx, ny = math.cos(a), -math.sin(a), math.sin(a), math.cos(a)
    p0x, p0y, L, w = 90 - ux * 4, 72 - uy * 4, 78, 7
    blade = [(p0x + nx * w, p0y + ny * w), (p0x + ux * L + nx * w, p0y + uy * L + ny * w),
             (p0x + ux * L - nx * w, p0y + uy * L - ny * w), (p0x - nx * w, p0y - ny * w)]
    shape(img, lambda dr, v: dr.polygon(blade, fill=v), C["night"], C["ink"], C["slate"])
    for i in range(14, L - 2, 4):
        for j in (-4, 0, 4):
            x, y = p0x + ux * i + nx * j, p0y + uy * i + ny * j
            d.rectangle([x, y, x + 1, y + 1], fill=(C["hot"] if (i, j) in ((30, 0), (54, -4)) else C["grey"]) + (255,))
    d.line([(p0x - 2, p0y + 2), (78, 88), (70, 100), (74, H)], fill=C["ink"] + (255,))  # The cable, a sword's tassel
    for i in (4, 12):
        x, y = p0x + ux * i, p0y + uy * i
        shape(img, lambda dr, v: dr.ellipse([x - 6, y - 5, x + 6, y + 5], fill=v), C["skin"], C["skin_dark"])
    body.alpha_composite(outline(img))
    return body


def black_knight():
    img = layer()
    d = ImageDraw.Draw(img)
    # The Black Knight, armless and still blocking the way, his arm and sword lying on the path
    shape(img, lambda dr, v: dr.rounded_rectangle([4, 98, 28, 103], 2, fill=v), C["night"], C["ink"], C["slate"])
    shape(img, lambda dr, v: dr.ellipse([26, 96, 33, 104], fill=v), C["night"], C["ink"], C["slate"])
    d.line([(10, 94), (36, 90)], fill=C["silver"] + (255,), width=2)
    d.line([(12, 90), (14, 97)], fill=C["amber"] + (255,), width=2)
    for lx in (44, 58):
        shape(img, lambda dr, v: dr.rectangle([lx, 74, lx + 8, 96], fill=v), C["night"], C["ink"], C["slate"])
        shape(img, lambda dr, v: dr.rectangle([lx - 2, 94, lx + 10, 98], fill=v), C["night"], C["ink"], C["slate"])
    shape(img, lambda dr, v: dr.rounded_rectangle([40, 42, 72, 78], 6, fill=v), C["night"], C["ink"], C["slate"])
    for sx in (36, 68):  # Stumps where his arms were
        shape(img, lambda dr, v: dr.ellipse([sx, 44, sx + 8, 54], fill=v), C["night"], C["ink"], C["slate"])
        d.ellipse([sx + 2, 46, sx + 6, 52], fill=C["red"] + (255,))
        d.line([sx + 4, 54, sx + 4, 58], fill=C["red"] + (255,))
    shape(img, lambda dr, v: dr.rectangle([46, 18, 66, 42], fill=v), C["night"], C["ink"], C["slate"])  # The great helm
    d.line([48, 28, 64, 28], fill=C["ink"] + (255,))
    d.line([48, 27, 64, 27], fill=C["slate"] + (255,))
    d.polygon([(56, 12), (60, 18), (52, 18)], fill=C["wine"] + (255,))  # A plume
    # King Arthur, crowned and bearded, his sword out
    man(img, 128, 100, 1.4, C["skin"], C["grey"], None, face="neutral", jacket=C["purple"])
    d.rectangle([125, 66, 131, 72], fill=C["yellow"] + (255,))  # A gold emblem on his tunic
    shape(img, lambda dr, v: dr.polygon([(121, 47), (135, 47), (134, 54), (128, 60), (122, 54)], fill=v), C["amber"], C["orange_brown"])
    d.line([126, 51, 130, 51], fill=C["dark_brown"] + (255,))
    shape(img, lambda dr, v: dr.polygon([(121, 39), (121, 32), (124, 35), (128, 30), (132, 35), (135, 32), (135, 39)], fill=v), C["yellow"], C["amber"])
    dot(img, 128, 36, C["red"])
    d.line([(120, 62), (110, 66)], fill=C["purple"] + (255,), width=3)
    d.line([(108, 65), (88, 50)], fill=C["silver"] + (255,), width=2)
    d.line([(106, 62), (110, 68)], fill=C["amber"] + (255,), width=2)
    # Patsy behind him, banging two halves of a coconut
    man(img, 166, 100, 1.1, C["skin"], C["sand"], C["dark_brown"], face="smile", jacket=C["tan"])
    for box, start, end, lip in (([146, 64, 162, 80], 90, 270, 154), ([170, 64, 186, 80], 270, 90, 178)):  # The coconut halves
        shape(img, lambda dr, v: dr.chord(box, start, end, fill=v), C["dark_brown"], C["plum"], C["brown"])
        d.line([lip, 65, lip, 79], fill=C["white"] + (255,))
    d.line([(158, 72), (154, 72)], fill=C["skin"] + (255,), width=2)  # His hands on them
    d.line([(174, 72), (178, 72)], fill=C["skin"] + (255,), width=2)
    img = outline(img)
    clop = ImageDraw.Draw(img)
    for k in (-1, 1):
        clop.line([(166 + k * 3, 66), (166 + k * 5, 62)], fill=C["white"] + (255,))
    return img


def particle_accelerator():
    img = layer()
    d = ImageDraw.Draw(img)
    # The blue magnet pipe running off down the tunnel, on red stands
    for x in range(36, 104, 22):
        h = 2 + (x - VP[0]) * 0.08
        shape(img, lambda dr, v: dr.rectangle([x - 2 - h / 3, pipe_y(x, 1) - 2, x + 2 + h / 3, pipe_y(x, 1) + h], fill=v), C["red"], C["wine"])
        d.rectangle([x + 4 + h / 2, pipe_y(x, 1) + h - 3, x + 8 + h, pipe_y(x, 1) + h], fill=C["amber"] + (255,))
    for f0, f1, col in ((0, 0.08, C["cyan"]), (0.08, 0.55, C["sky"]), (0.55, 0.85, C["blue"]), (0.85, 1, C["navy"])):
        d.polygon([(VP[0], pipe_y(VP[0], f0)), (104, pipe_y(104, f0)), (104, pipe_y(104, f1)), (VP[0], pipe_y(VP[0], f1))], fill=col + (255,))
    for x in (52, 78):  # Its joints
        d.line([x, pipe_y(x, 0), x, pipe_y(x, 1)], fill=C["navy"] + (255,))
    # The steel interconnect, a little fatter, then its open end, full of cables
    for f0, f1, col in ((0, 0.1, C["white"]), (0.1, 0.55, C["silver"]), (0.55, 0.85, C["grey"]), (0.85, 1, C["slate"])):
        d.polygon([(104, pipe_y(104, f0, 2)), (132, pipe_y(132, f0, 2)), (132, pipe_y(132, f1, 2)), (104, pipe_y(104, f1, 2))], fill=col + (255,))
    for x in (110, 118, 126):  # Bellows
        d.line([x, pipe_y(x, 0.1, 2), x, pipe_y(x, 0.95, 2)], fill=C["grey"] + (255,))
    t0, t1 = pipe_y(132, 0, 2), pipe_y(132, 1, 2)
    shape(img, lambda dr, v: dr.ellipse([126, t0, 140, t1], fill=v), C["silver"], C["grey"], C["white"])
    d.ellipse([129, t0 + 5, 138, t1 - 5], fill=C["ink"] + (255,))
    for k in range(5):
        y = t0 + 10 + k * 8
        d.line([(131, y), (136, y + 3)], fill=C["silver"] + (255,))
    d.ellipse([131, (t0 + t1) / 2 - 3, 136, (t0 + t1) / 2 + 3], outline=C["white"] + (255,))
    # Cables spilling down to the floor
    for k, col in enumerate((C["ink"], C["night"], C["ink"])):
        d.line([(134 + k * 3, t1 - 6), (138 + k * 4, 96), (128 + k * 6, 104)], fill=col + (255,))
    # The next magnet, a huge steel drum, yellow caps along its foot
    for y0, y1, col in ((12, 18, C["white"]), (18, 58, C["silver"]), (58, 86, C["grey"]), (86, 98, C["slate"])):
        d.rectangle([150, y0, W, y1], fill=col + (255,))
    shape(img, lambda dr, v: dr.ellipse([144, 12, 158, 98], fill=v), C["silver"], C["grey"], C["white"])
    d.ellipse([147, 22, 155, 88], fill=C["grey"] + (255,))
    d.rectangle([176, 12, 179, 98], fill=C["amber"] + (255,))  # Tape round it
    for x in (160, 170, 184):
        shape(img, lambda dr, v: dr.ellipse([x, 92, x + 7, 99], fill=v), C["yellow"], C["amber"])
    # Someone lying under it, fixing it: only his legs stick out
    for dy in (0, 5):
        d.line([(176, 98 + dy), (150, 101 + dy)], fill=C["navy"] + (255,), width=4)
        d.line([(162, 98 + dy), (162, 103 + dy)], fill=C["silver"] + (255,))
        shape(img, lambda dr, v: dr.ellipse([142, 98 + dy, 151, 104 + dy], fill=v), C["night"], C["ink"])
    return outline(img, C["night"])


def cleaning_robot():
    img = layer()
    d = ImageDraw.Draw(img)
    # The pool robot, Love, Death & Robots style: a white angular hull half sunk at the water line, flippers below it
    for poly in (((58, 78), (70, 76), (66, 96), (52, 100)), ((86, 74), (98, 72), (96, 92), (82, 96)), ((110, 68), (120, 66), (122, 86), (108, 88))):
        d.polygon(poly, fill=C["grey"] + (255,))
        d.line([poly[1], poly[2]], fill=C["slate"] + (255,), width=2)
    d.polygon([(34, 60), (112, 48), (132, 54), (54, 68)], fill=C["white"] + (255,))  # Top
    d.polygon([(54, 68), (132, 54), (132, 66), (54, 82)], fill=C["silver"] + (255,))  # Side
    d.polygon([(34, 60), (54, 68), (54, 82), (34, 72)], fill=C["grey"] + (255,))  # Back end
    d.polygon([(60, 61), (98, 55), (102, 58), (118, 55), (120, 56), (101, 60), (97, 57), (61, 63)], fill=C["ink"] + (255,))  # Its stripe
    d.line([(60, 75), (126, 63)], fill=C["grey"] + (255,))
    d.rectangle([108, 64, 114, 70], fill=C["ink"] + (255,))
    # The arm, and the big round brush head on it, turned toward us
    d.line([(124, 54), (134, 38), (146, 38)], fill=C["ink"] + (255,), width=2)
    shape(img, lambda dr, v: dr.ellipse([131, 35, 137, 41], fill=v), C["grey"], C["slate"])
    dot(img, 134, 38, C["red"])  # Watching
    shape(img, lambda dr, v: dr.ellipse([152, 16, 176, 60], fill=v), C["grey"], C["slate"])  # The rim
    for y in range(20, 58, 4):
        d.line([170, y, 175, y], fill=C["night"] + (255,))
    shape(img, lambda dr, v: dr.ellipse([144, 16, 168, 60], fill=v), C["silver"], C["grey"], C["white"])
    d.ellipse([150, 30, 162, 46], outline=C["grey"] + (255,))
    d.ellipse([153, 34, 159, 42], fill=C["slate"] + (255,))
    img = outline(img, C["ink"])
    # Under the water line everything goes blue
    sunk = {C["white"]: C["sky"], C["silver"]: C["sky"], C["grey"]: C["blue"], C["slate"]: C["navy"], C["ink"]: C["night"], C["night"]: C["night"]}
    px = img.load()
    for x in range(W):
        wy = water_y(x)
        for y in range(int(wy) + 2, H):
            r, g, b, a = px[x, y]
            if a:
                px[x, y] = sunk.get((r, g, b), C["blue"]) + (255,)
        if px[x, int(wy)][3]:
            px[x, int(wy)] = C["white"] + (255,)
            px[x, int(wy) + 1] = C["cyan"] + (255,)
    return img


def templar_knight():
    img = layer()
    d = ImageDraw.Draw(img)
    # A bank counter, the hoard heaped on it in one great pile, coin stacks and a promissory note in front
    shape(img, lambda dr, v: dr.rectangle([116, 70, 188, 100], fill=v), C["brown"], C["dark_brown"], C["tan"])
    for x in range(120, 186, 10):
        d.line([x, 72, x, 98], fill=C["dark_brown"] + (255,))
    hoard = shape(img, lambda dr, v: dr.chord([118, 34, 186, 106], 180, 360, fill=v), C["yellow"], C["amber"], C["white"])
    hm = hoard.load()
    for row, y in enumerate(range(40, 68, 4)):
        for x in range(120 + (row % 2) * 3, 186, 6):
            if hm[x - 2, y - 1] and hm[x + 2, y + 2] and hm[x, y + 3]:
                d.arc([x - 2, y - 1, x + 2, y + 2], 0, 180, fill=C["amber"] + (255,))
    for x, y in ((140, 46), (158, 52), (132, 60), (170, 62)):
        dot(img, x, y, C["white"])
    d.rectangle([142, 62, 154, 70], fill=C["sand"] + (255,), outline=C["tan"] + (255,))
    d.line([144, 66, 152, 66], fill=C["dark_brown"] + (255,))
    for x, h in ((124, 10), (160, 7), (172, 13)):
        shape(img, lambda dr, v: dr.rectangle([x, 70 - h, x + 7, 70], fill=v), C["yellow"], C["amber"], C["white"])
        for y in range(70 - h + 2, 70, 2):
            d.line([x + 1, y, x + 6, y], fill=C["amber"] + (255,))
    # The Templar: chainmail, a white mantle with a red cross, sword raised
    shape(img, lambda dr, v: dr.polygon([(58, 42), (90, 42), (98, 104), (50, 104)], fill=v), C["white"], C["silver"])
    d.rectangle([70, 54, 78, 82], fill=C["red"] + (255,))
    d.rectangle([62, 62, 86, 68], fill=C["red"] + (255,))
    shape(img, lambda dr, v: dr.ellipse([62, 18, 86, 44], fill=v), C["grey"], C["slate"], C["silver"])  # Mail coif
    shape(img, lambda dr, v: dr.ellipse([67, 26, 81, 40], fill=v), C["skin"], C["skin_dark"])
    for ex in (71, 77):
        d.rectangle([ex, 31, ex + 1, 32], fill=C["ink"] + (255,))
    d.line([72, 37, 77, 37], fill=C["ink"] + (255,))
    d.line([(90, 50), (102, 34)], fill=C["grey"] + (255,), width=4)  # The arm, raised
    d.line([(104, 32), (116, 4)], fill=C["white"] + (255,), width=2)  # The sword
    d.line([(99, 30), (107, 36)], fill=C["amber"] + (255,), width=2)
    # His shield on the other arm: white, the red cross on it
    shape(img, lambda dr, v: dr.polygon([(32, 52), (60, 52), (60, 72), (46, 92), (32, 72)], fill=v), C["white"], C["silver"], None, (1, 1), 2)
    d.rectangle([44, 54, 48, 86], fill=C["red"] + (255,))
    d.rectangle([34, 61, 58, 65], fill=C["red"] + (255,))
    return outline(img)


VP = (24, 46)  # The tunnel's vanishing point


def pipe_y(x, f, grow=0):
    """A point across the accelerator's pipe at x: f = 0 along its top edge, 1 along its bottom, receding to VP."""
    t = (x - VP[0]) / (140 - VP[0])
    top, bot = VP[1] - 2 + t * (26 - VP[1] + 2) - grow, VP[1] + 2 + t * (84 - VP[1] - 2) + grow
    return top + f * (bot - top)


def bg_tunnel():
    """The LHC tunnel: a long concrete tube running off to a vanishing point, cable trays and strip lights overhead, a
    white line down the floor."""
    img = layer()
    vx, vy = VP
    d = ImageDraw.Draw(img)
    gradient(img, [C["silver"], C["grey"]], 0, H)  # The walls
    d.polygon([(0, 0), (W, 0), (W, 6), (vx, vy - 2), (0, 24)], fill=C["slate"] + (255,))  # The ceiling
    floor = layer()
    gradient(floor, [C["grey"], C["silver"]], vy, H)
    fm = mask()
    ImageDraw.Draw(fm).polygon([(vx, vy + 2), (W, 92), (W, H), (0, H), (0, 70)], fill=255)
    img.paste(floor, (0, 0), fm)
    for ex in (70, 110, 150):  # Cable trays along the ceiling
        d.line([(vx, vy - 2), (ex, 0)], fill=C["grey"] + (255,))
        for k in range(1, 8):
            t = (k / 8) ** 2
            x, y = vx + (ex - vx) * t, vy - 2 - (vy - 2) * t
            d.line([x - 1 - 4 * t, y, x + 1 + 4 * t, y], fill=C["grey"] + (255,))
    for k in range(1, 9):  # Strip lights down the middle of the ceiling, tubes along the tunnel, growing as they come near
        t0, t1 = (k / 9) ** 1.7, (k / 9) ** 1.7 + 0.03 + 0.05 * k / 9
        p = [(vx + (96 - vx) * t, vy - 2 - (vy - 2) * t) for t in (t0, min(t1, 1))]
        d.line(p, fill=C["yellow"] + (255,), width=1 + int(t0 * 2))
    for wy in (14, 22):  # Pipework along the far wall
        d.line([(vx, vy - 1), (W, wy)], fill=C["grey"] + (255,))
    d.line([(vx, vy + 2), (72, H)], fill=C["white"] + (255,), width=2)  # The white line down the floor
    return img


def water_y(x):
    """The pool's water line, sloping a little with the coping."""
    return 80 - x * 14 / W


def coping_y(x):
    return 42 - x * 22 / W


def bg_pool():
    """A swimming pool at night, flat-shaded like Love, Death & Robots: the stone coping, cyan tiles above the water line,
    the dark water below it."""
    img = layer()
    gradient(img, [C["ink"], C["night"], C["teal_dark"]], 0, 46)
    px = img.load()
    for x in range(W):
        cy, wy = coping_y(x), water_y(x)
        for y in range(max(0, int(cy) - 5), H):
            v = y - cy
            if v < -2:
                col = C["silver"] if v < -4 else C["white"]
            elif v < 2:
                col = C["grey"] if v < 0 else C["slate"]
            else:
                grout = int(v) % 10 == 0 or (x + int(v * 0.3)) % 13 == 0
                if y < wy:
                    col = C["navy"] if grout else (C["cyan"] if (x // 13 + int(v) // 10) % 5 == 0 else C["sky"])
                else:
                    col = C["navy"] if grout else C["blue"]
                    if (x * 3 + y * 7) % 23 == 0 or ((x - y * 2) % 31 == 0 and BAYER[y % 4][x % 4] < 8):
                        col = C["sky"]  # Light rippling under the water
            px[x, y] = col + (255,)
        for y in (int(wy), int(wy) + 1):  # The water's surface
            px[x, y] = (C["white"] if y == int(wy) else C["cyan"]) + (255,)
    return img


CARDS += [
    ("Placebo", "mind", placebo),
    ("BusinessExpense", "jail", business_expense),
    ("GoldenDome", "office", golden_dome),
    ("SurveillanceGiraffe", "field", surveillance_giraffe),
    ("InflatableTank", "field", inflatable_tank),
    ("KeyboardWarrior", "room_night", keyboard_warrior),
    ("BlackKnight", "forest", black_knight),
    ("ParticleAccelerator", "tunnel", particle_accelerator),
    ("CleaningRobot", "pool", cleaning_robot),
    ("TemplarKnight", "gallery", templar_knight),
]
BACKGROUNDS.update({"jail": bg_jail, "field": bg_field, "forest": bg_forest, "tunnel": bg_tunnel, "pool": bg_pool})


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
