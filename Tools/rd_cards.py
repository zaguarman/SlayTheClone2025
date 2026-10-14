"""
Card art from Retro Diffusion (retrodiffusion.ai), in the game's art style: 256 x 144 pixel art at night, each card in its
faction's palette (Tools/faction_palettes.py), the style of the reference picture (Tools/palettes/style_reference.png).

Claude drives the generation through the Retro Diffusion MCP server; this script does everything around it:

  python Tools/rd_cards.py prompt <Card>          The request for a card: its prompt, palette and settings, from BRIEFS.
  python Tools/rd_cards.py fetch <Card> <url>...  Downloads the takes into ArtArchive/RetroDiffusion/<Card>/, snaps each
                                                  pixel exactly to the faction's palette (it reports how many moved) and
                                                  writes sheet.png, the takes side by side, to pick from.
  python Tools/rd_cards.py accept <Card> <n>      Take n becomes the card's art: Assets/Art/PixelCards/<Card>.png (5x,
                                                  nearest). The picture it replaces is archived (old pixel art in
                                                  ArtArchive/PixelCards, vector art in ArtArchive/Cards). Then run
                                                  Cards > Assign Card Art in Unity.

The rules that keep the takes on brief (learned from the first tries):
  - The prompt is sent verbatim (bypass_prompt_expansion): Retro Diffusion's own rewriting turned "smoking a steel chain"
    into an ordinary cigarette. So the prompt itself says everything, the gag first.
  - The gag is described as physical objects (what it is made of, its shape, where it is), never as a pun to decode.
  - No text in the picture: the model can't spell. Words a card needs are written on afterwards in the pixel font.
  - Only colours the faction's palette has: a subject that needs another colour is described in palette colours.
  - Four takes per request (num_images=4); every take is checked against the brief's MUST list before one is picked.
"""
import base64
import io
import json
import os
import shutil
import sys
import urllib.request

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import faction_palettes  # noqa: E402

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
PALETTES = os.path.join(ROOT, "Tools", "palettes")
STYLE_REFERENCE = os.path.join(PALETTES, "style_reference.png")
TAKES = os.path.join(ROOT, "ArtArchive", "RetroDiffusion")
PIXEL_CARDS = os.path.join(ROOT, "Assets", "Art", "PixelCards")
PIXEL_ARCHIVE = os.path.join(ROOT, "ArtArchive", "PixelCards")
VECTOR_CARDS = os.path.join(ROOT, "Assets", "Art", "Cards")
VECTOR_ARCHIVE = os.path.join(ROOT, "ArtArchive", "Cards")
GENERATED = os.path.join(ROOT, "Tools", "generated_cards.txt")  # Cards whose art came from here: pixel_art.py leaves them

W, H = 256, 144
SCALE = 5  # Stored 5x (1280 x 720) so Unity's filtering keeps the pixels square
STYLE = "rd_pro__painterly"

# Each card: its faction, the scene, the gag, what must be visible, and what the picture is lit by.
# Everything is said in the faction palette's colours (see faction_palettes.FACTIONS).
BRIEFS = {
    "ChainSmoker": {
        "faction": "Zeitgeist",
        "scene": "A dark street at night outside a bar, a brick wall behind, wet cobblestones.",
        "gag": "A wiry man in a worn leather jacket holds a heavy grey steel chain to his lips and smokes it like a "
               "cigarette: thick oval steel links, each link as big as his thumb, the end link in his mouth glowing "
               "orange like a lit cigarette tip, the rest of the chain hanging down in a loose loop to his knees.",
        "details": "Grey smoke rises from the glowing end link and curls round his head.",
        "light": "a magenta neon tube over the bar door and an orange streetlight; the light spills on the cobbles",
        "must": ["a steel chain (links clearly visible) in his mouth instead of a cigarette", "glowing end link",
                 "smoke", "magenta neon light", "no lettering anywhere"],
    },
    "MeatproxyAsAService": {
        "faction": "Tech",
        "scene": "A dark open-plan office at night, rows of empty desks fading into shadow behind.",
        "gag": "A big raw steak sits upright in a black office chair at a desk like an office worker, wearing a "
               "small headset; the steak is pink and rosy with a pale fat rim, raw, not cooked.",
        "details": "On the computer monitor two document windows side by side with an arrow from one to the other. "
                   "A small blank paper price tag hangs from the monitor's corner.",
        "light": "the monitor's cyan glow, lighting the steak from the front; small amber lights on the computer under the desk",
        "must": ["a raw steak (pink, not brown) in the chair", "headset", "monitor with two windows and an arrow",
                 "price tag", "no lettering anywhere"],
    },
    "PhilosophyPhD": {
        "faction": "Utopia",
        "scene": "A fast-food kitchen at 3 a.m., steel counters and a hot grill, a dark street outside the window.",
        "gag": "A young graduate in a full black graduation gown and square mortarboard cap with a tassel flips "
               "burgers on the grill with a spatula, staring blankly ahead.",
        "details": "A framed diploma hangs on the wall next to the menu board; a glowing lime sign in the window.",
        "light": "harsh white strip lights overhead and the lime glow of the window sign",
        "must": ["graduation gown and mortarboard", "burgers on a grill and a spatula", "framed diploma",
                 "blank stare", "no lettering anywhere"],
    },
    "TheChosenOne": {
        "faction": "Myth",
        "scene": "The inside of a temple at night, tall candles and a stained-glass window behind.",
        "gag": "Jesus in white robes and sandals, long brown hair and beard, a glowing golden halo, stands in the middle "
               "firing two golden submachine guns akimbo, one in each hand, bright muzzle flashes.",
        "details": "White doves scatter around him. He is the centre of attention.",
        "light": "golden candlelight and the gold muzzle flashes lighting him from the front",
        "must": ["Jesus with halo, robes and beard", "two guns, one in each hand", "muzzle flashes", "doves",
                 "no lettering anywhere"],
    },
    "FemmeFatale": {
        "faction": "ShadowOps",
        "scene": "A casino at night, a card table under a single hanging lamp, the room dark around it.",
        "gag": "A glamorous woman in a black evening gown sits at the table smiling over her cocktail; beside her a "
               "red-faced general in a uniform full of medals is squeezed between her crossed legs, his medals "
               "popping off his chest into the air.",
        "details": "Cigarette smoke hangs in the lamp's beam; a red ember glows at the tip of her long cigarette holder.",
        "light": "one cold white lamp hanging over the table, and the red ember",
        "must": ["woman in a black gown", "general squeezed between her legs", "medals flying off", "cocktail",
                 "red ember", "no lettering anywhere"],
    },
}

FRAMING = ("Front view at eye level, like a stage. The main subject large and centred, filling the middle of a wide "
           "frame, the background simple and dark.")
STYLE_WORDS = ("Night scene: deep navy and indigo shadows, everything lit only by {light}. Soft painterly shading, no "
               "black outlines, little dithering, muted colours except the light.")
NO_TEXT = "No text, no letters, no numbers, no writing, no logos anywhere in the picture."


def brief(card):
    if card not in BRIEFS:
        sys.exit(f"No brief for {card}. Known: {', '.join(BRIEFS)}")
    return BRIEFS[card]


def prompt(card):
    b = brief(card)
    return " ".join([b["gag"], b["details"], b["scene"], FRAMING, STYLE_WORDS.format(light=b["light"]), NO_TEXT])


def palette_colours(faction):
    return [faction_palettes.rgb(v) for _, v in faction_palettes.palette(faction)]


def b64(path):
    return base64.b64encode(open(path, "rb").read()).decode()


def show_request(card):
    b = brief(card)
    request = {
        "prompt": prompt(card),
        "prompt_style": STYLE,
        "width": W, "height": H,
        "num_images": 4,
        "bypass_prompt_expansion": True,
        "input_palette": b64(os.path.join(PALETTES, f"{b['faction']}.png")),
        "reference_images": ["<Tools/palettes/style_reference.png>"],
    }
    print(json.dumps(request, indent=2))
    print("\nMUST show:\n  - " + "\n  - ".join(b["must"]))


def snap(img, colours):
    """Every pixel to the nearest palette colour (weighted RGB): nothing off the palette survives."""
    img = img.convert("RGB")
    cache, moved = {}, 0
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            c = px[x, y]
            if c not in cache:
                cache[c] = min(colours, key=lambda p: _distance(c, p))
            if cache[c] != c:
                if _distance(c, cache[c]) > 300:  # More than a rounding slip: a colour the palette didn't have
                    moved += 1
                px[x, y] = cache[c]
    return img, moved


def _distance(a, b):
    mean_red = (a[0] + b[0]) / 2
    dr, dg, db = a[0] - b[0], a[1] - b[1], a[2] - b[2]
    return (2 + mean_red / 256) * dr * dr + 4 * dg * dg + (2 + (255 - mean_red) / 256) * db * db


def fetch(card, urls):
    b = brief(card)
    folder = os.path.join(TAKES, card)
    os.makedirs(folder, exist_ok=True)
    first = len([f for f in os.listdir(folder) if f.startswith("take_") and f.endswith("_raw.png")]) + 1
    colours = palette_colours(b["faction"])
    takes = []
    for i, url in enumerate(urls):
        n = first + i
        raw = Image.open(io.BytesIO(urllib.request.urlopen(url).read())).convert("RGB")
        if raw.size != (W, H):
            raw = raw.resize((W, H), Image.NEAREST)
        raw.save(os.path.join(folder, f"take_{n}_raw.png"))
        snapped, moved = snap(raw.copy(), colours)
        snapped.save(os.path.join(folder, f"take_{n}.png"))
        off = {c for _, c in snapped.getcolors(W * H)} - set(colours)
        bars = borders(snapped)
        print(f"take {n}: {moved / (W * H):.1%} of pixels were off the {b['faction']} palette (snapped), "
              f"{len(snapped.getcolors(W * H))} colours used{'' if not off else ', OFF PALETTE: ' + str(off)}"
              f"{'' if not bars else ', FLAT BARS (' + ', '.join(bars) + '): reject or edit them out'}")
        takes.append(n)
    sheet(card)
    return takes


def borders(img, depth=6):
    """The edges that come out as flat letterbox bars (a fault some takes have): the edges whose outer `depth` pixels
    are all one colour."""
    px = img.load()
    edges = {
        "left": [px[x, y] for x in range(depth) for y in range(H)],
        "right": [px[W - 1 - x, y] for x in range(depth) for y in range(H)],
        "top": [px[x, y] for y in range(depth) for x in range(W)],
        "bottom": [px[x, H - 1 - y] for y in range(depth) for x in range(W)],
    }
    return [edge for edge, colours in edges.items() if len(set(colours)) == 1]


def sheet(card):
    """Every snapped take of the card, 2x, numbered, two to a row."""
    folder = os.path.join(TAKES, card)
    names = sorted((f for f in os.listdir(folder) if f.startswith("take_") and not f.endswith("_raw.png")),
                   key=lambda f: int(f[5:-4]))
    cols, gap, scale = 2, 8, 2
    rows = (len(names) + cols - 1) // cols
    img = Image.new("RGB", (cols * (W * scale + gap) + gap, rows * (H * scale + gap + 14) + gap), (20, 20, 28))
    draw = ImageDraw.Draw(img)
    for i, name in enumerate(names):
        x, y = gap + (i % cols) * (W * scale + gap), gap + (i // cols) * (H * scale + gap + 14)
        draw.text((x, y), name[5:-4], fill=(243, 233, 210))
        img.paste(Image.open(os.path.join(folder, name)).resize((W * scale, H * scale), Image.NEAREST), (x, y + 14))
    img.save(os.path.join(folder, "sheet.png"))
    print(f"Sheet: {os.path.join(folder, 'sheet.png')}")


def accept(card, n):
    b = brief(card)
    take = os.path.join(TAKES, card, f"take_{n}.png")
    art = Image.open(take).convert("RGB").resize((W * SCALE, H * SCALE), Image.NEAREST)
    path = os.path.join(PIXEL_CARDS, f"{card}.png")
    if os.path.exists(path):  # The pixel art it replaces
        folder = os.path.join(PIXEL_ARCHIVE, card)
        os.makedirs(folder, exist_ok=True)
        v = len([f for f in os.listdir(folder) if f.endswith(".png")]) + 1
        shutil.move(path, os.path.join(folder, f"{card}_v{v}.png"))
    art.save(path)
    svg = os.path.join(VECTOR_CARDS, b["faction"], f"{card}.svg")
    if os.path.exists(svg):  # The vector art it replaces
        os.makedirs(os.path.join(VECTOR_ARCHIVE, b["faction"]), exist_ok=True)
        shutil.move(svg, os.path.join(VECTOR_ARCHIVE, b["faction"], f"{card}.svg"))
        if os.path.exists(svg + ".meta"):
            os.remove(svg + ".meta")
    done = set(open(GENERATED).read().split()) if os.path.exists(GENERATED) else set()
    done.add(card)
    open(GENERATED, "w", newline="\n").write("\n".join(sorted(done)) + "\n")
    print(f"{card}: take {n} is its art now ({path}). Run Cards > Assign Card Art in Unity.")


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    command, card = sys.argv[1], sys.argv[2]
    if command == "prompt":
        show_request(card)
    elif command == "fetch":
        fetch(card, sys.argv[3:])
    elif command == "accept":
        accept(card, int(sys.argv[3]))
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
