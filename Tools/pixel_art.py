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
    # The shark, swimming right through the skyline, jaws open
    body = [(26, 60), (60, 44), (110, 40), (150, 48), (170, 58), (150, 66), (110, 72), (60, 72)]
    shape(img, lambda d, v: d.polygon(body, fill=v), C["slate"], C["navy"], C["grey"])
    shape(img, lambda d, v: d.polygon([(60, 66), (110, 70), (150, 64), (110, 76), (64, 74)], fill=v), C["silver"], C["grey"], C["white"])
    shape(img, lambda d, v: d.polygon([(92, 42), (104, 18), (116, 42)], fill=v), C["slate"], C["navy"], C["grey"])  # Dorsal fin
    shape(img, lambda d, v: d.polygon([(30, 60), (8, 40), (16, 60), (6, 80)], fill=v), C["slate"], C["navy"], C["grey"])  # Tail
    shape(img, lambda d, v: d.polygon([(96, 70), (84, 84), (104, 72)], fill=v), C["slate"], C["navy"])  # Pectoral fin
    d = ImageDraw.Draw(img)
    d.polygon([(150, 56), (174, 52), (176, 64), (150, 64)], fill=C["wine"] + (255,))  # The open jaw
    for x in range(152, 174, 4):
        d.polygon([(x, 56), (x + 2, 60), (x + 4, 56)], fill=C["white"] + (255,))
        d.polygon([(x, 64), (x + 2, 60), (x + 4, 64)], fill=C["white"] + (255,))
    d.rectangle([140, 49, 142, 51], fill=C["ink"] + (255,))
    dot(img, 140, 49, C["white"])
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


def unicorn():
    img = layer()
    rnd = random.Random(4)
    d = ImageDraw.Draw(img)
    # The falling chart behind
    pts = [(10, 30), (40, 26), (62, 40), (90, 34), (120, 58), (150, 54), (182, 84)]
    d.line(pts, fill=C["red"] + (255,), width=2)
    d.polygon([(182, 84), (176, 76), (186, 78)], fill=C["red"] + (255,))
    # Flames under its hooves
    for fx in range(52, 150, 12):
        flame(img, fx, 100, rnd.randint(14, 22), 14, rnd)
    # Body, legs, neck, head
    for lx in (70, 80, 112, 122):
        shape(img, lambda dr, v: dr.rectangle([lx, 66, lx + 5, 92], fill=v), C["white"], C["silver"])
        d.rectangle([lx, 90, lx + 5, 93], fill=C["amber"] + (255,))
    shape(img, lambda dr, v: dr.ellipse([62, 46, 132, 74], fill=v), C["white"], C["silver"])
    shape(img, lambda dr, v: dr.polygon([(118, 52), (130, 28), (142, 30), (132, 58)], fill=v), C["white"], C["silver"])
    shape(img, lambda dr, v: dr.ellipse([128, 20, 152, 36], fill=v), C["white"], C["silver"])
    d.rectangle([140, 25, 141, 26], fill=C["ink"] + (255,))
    shape(img, lambda dr, v: dr.polygon([(134, 22), (146, 2), (140, 22)], fill=v), C["amber"], C["orange_brown"], C["yellow"])
    # The rainbow mane and tail
    for i, col in enumerate((C["red"], C["orange"], C["yellow"], C["green"], C["sky"], C["purple"])):
        d.line([(126 - i, 24 + i * 2), (114 - i * 2, 40 + i * 2), (118 - i, 52)], fill=col + (255,), width=2)
        d.line([(62, 54 + i * 2), (48 - i, 60 + i * 2), (44 - i, 76 + i)], fill=col + (255,), width=2)
    return outline(img)


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
    # The cat, ginger, leaning in
    shape(img, lambda dr, v: dr.ellipse([22, 50, 84, 104], fill=v), C["orange"], C["rust"], C["amber"])
    shape(img, lambda dr, v: dr.ellipse([40, 24, 92, 70], fill=v), C["orange"], C["rust"], C["amber"])
    for ex in (46, 80):
        shape(img, lambda dr, v: dr.polygon([(ex - 8, 34), (ex - 2, 14), (ex + 6, 30)], fill=v), C["orange"], C["rust"])
        d.polygon([(ex - 5, 31), (ex - 2, 20), (ex + 3, 30)], fill=C["pink"] + (255,))
    for ex in (56, 76):
        d.line([ex - 3, 44, ex, 42, ex + 3, 44], fill=C["ink"] + (255,))  # Happy closed eyes
    d.polygon([(64, 50), (68, 50), (66, 53)], fill=C["pink"] + (255,))
    for wy in (52, 55):
        d.line([86, wy, 98, wy - 2], fill=C["sand"] + (255,))
    for sx in range(30, 80, 9):
        d.line([sx, 60, sx + 4, 70], fill=C["rust"] + (255,))
    # The fish, blue, nose to nose
    fish = [(104, 52), (130, 38), (156, 46), (164, 56), (156, 66), (130, 72)]
    shape(img, lambda dr, v: dr.polygon(fish, fill=v), C["sky"], C["blue"], C["cyan"])
    shape(img, lambda dr, v: dr.polygon([(156, 56), (182, 38), (176, 56), (182, 74)], fill=v), C["sky"], C["blue"], C["cyan"])
    shape(img, lambda dr, v: dr.polygon([(128, 40), (140, 26), (146, 44)], fill=v), C["sky"], C["blue"])
    d.rectangle([116, 49, 118, 51], fill=C["ink"] + (255,))
    dot(img, 116, 49, C["white"])
    for sx in range(124, 154, 6):
        d.arc([sx, 50, sx + 6, 58], 300, 60, fill=C["blue"] + (255,))
    d.ellipse([105, 56, 108, 59], fill=C["pink"] + (255,))
    img = outline(img)
    # A heart above them
    heart = layer()
    hd = ImageDraw.Draw(heart)
    hd.ellipse([86, 4, 96, 14], fill=C["hot"] + (255,))
    hd.ellipse([94, 4, 104, 14], fill=C["hot"] + (255,))
    hd.polygon([(87, 11), (103, 11), (95, 22)], fill=C["hot"] + (255,))
    hd.point([(89, 7), (90, 7)], fill=C["white"] + (255,))
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
