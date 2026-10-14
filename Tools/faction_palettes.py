"""
The game's art style in colours: one palette per faction, for the pixel card art.

Every palette is 16 colours:
  - 8 shared colours, from the art style reference (a shop at night): the night's shadows (navy, slate, dusk mauve),
    a skin ramp and a parchment highlight. Every card sits in the same night, so a hand of mixed factions still matches.
  - 5 colours of the faction's light: the one light source of its pictures, from deep to hot, around the faction's colour
    on the table (UITheme.FactionColor). Its dark end leans toward the night's blue, its hot end toward white.
  - 3 colours of the faction's second accent, a small counterpoint to the light.

Writes Tools/palettes/<Faction>.gpl (Aseprite / GIMP), <Faction>.png (1 pixel per colour, for Retro Diffusion's
input_palette) and palettes_preview.png (every palette, with a lit sphere in each faction's light).

Usage: python Tools/faction_palettes.py
"""
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "palettes")

# The night every card shares: shadows, skin, highlight
SHARED = [
    ("Night", 0x060B29), ("Deep Navy", 0x1A1F44), ("Slate", 0x28355E), ("Dusk Mauve", 0x54405B),
    ("Skin Shadow", 0x825158), ("Skin", 0xC98A6B), ("Skin Light", 0xF0C7A0), ("Parchment", 0xF3E9D2),
]

# Each faction: its light (deep to hot; the middle one is its colour on the table) and its second accent
FACTIONS = {
    "Myth": ("Candlelight and church gold; blood", [0x4A2A1E, 0x8A5A24, 0xC9A23D, 0xE8CF6A, 0xFFF4C2], [0x5A1424, 0x9E2B35, 0xD4544A]),
    "Statecraft": ("Propaganda red and searchlights; olive drab", [0x4A1418, 0x8C2026, 0xD9483B, 0xF07A5A, 0xFFC9A8], [0x3B4228, 0x5F6B3A, 0x8F9A5A]),
    "ShadowOps": ("Cold moonlight on steel; a red laser dot", [0x2B3440, 0x4F5C6B, 0x7C8A99, 0xB4C2CF, 0xE6EEF5], [0x6E1418, 0xC0262B, 0xFF5A4A]),
    "Corporate": ("Fluorescent office and money green; gold coins", [0x0F3B33, 0x1F6E57, 0x3FBF8F, 0x86E3B5, 0xD6FFE6], [0x7D5A1E, 0xC9962E, 0xF2D06B]),
    "Tech": ("Monitor glow; amber status LEDs", [0x0D2F4A, 0x1A5F86, 0x3DA9D6, 0x7FD8F0, 0xD4F7FF], [0x6B3A12, 0xD9822B, 0xFFC266]),
    "DigitalSociety": ("Phone-screen blue; notification red", [0x1C2560, 0x34479E, 0x5D7BD6, 0x9DB3F5, 0xE0E8FF], [0x7A1C3C, 0xE0405E, 0xFF8FA3]),
    "Zeitgeist": ("Neon-sign magenta; sodium streetlight orange", [0x4A1650, 0x8C2B8A, 0xD95DD0, 0xF59AE8, 0xFFD9F8], [0x7A2E2A, 0xE0703A, 0xFFB35C]),
    "Mind": ("Dreamlike violet; an eerie teal", [0x2E1C5E, 0x5A3A9E, 0x9A6CD6, 0xC9A6F2, 0xF0E2FF], [0x16504F, 0x2F9E8F, 0x8FE0C8]),
    "Utopia": ("Clean lab white light; bioluminescent lime", [0x5E5A3A, 0xA39D63, 0xE8E0A0, 0xF7F3CF, 0xFFFFFF], [0x2F5E1F, 0x6FBF3A, 0xC4F06A]),
}


def rgb(value):
    return (value >> 16 & 255, value >> 8 & 255, value & 255)


def palette(faction):
    _, light, accent = FACTIONS[faction]
    names = ["Light Deep", "Light Dark", "Light", "Light Bright", "Light Hot"]
    return SHARED + list(zip(names, light)) + list(zip(["Accent Dark", "Accent", "Accent Bright"], accent))


def write_gpl(faction, colours):
    with open(os.path.join(OUT, f"{faction}.gpl"), "w", newline="\n") as f:
        f.write(f"GIMP Palette\nName: {faction}\nColumns: 8\n#\n")
        for name, value in colours:
            r, g, b = rgb(value)
            f.write(f"{r:3d} {g:3d} {b:3d}\t{name}\n")


def write_png(faction, colours):
    img = Image.new("RGB", (len(colours), 1))
    for i, (_, value) in enumerate(colours):
        img.putpixel((i, 0), rgb(value))
    img.save(os.path.join(OUT, f"{faction}.png"))


def lit_sphere(colours, size):
    """A sphere on the night, lit from the upper left in the faction's light: how its ramp reads."""
    img = Image.new("RGB", (size, size), rgb(SHARED[0][1]))
    ramp = [SHARED[1][1], SHARED[2][1]] + [v for _, v in colours[8:13]]
    c, r = size / 2, size * 0.4
    for y in range(size):
        for x in range(size):
            dx, dy = (x - c) / r, (y - c) / r
            d2 = dx * dx + dy * dy
            if d2 > 1:
                continue
            nz = (1 - d2) ** 0.5
            lum = max(0.0, (-dx * 0.6 - dy * 0.6 + nz * 0.55))  # Light from the upper left
            img.putpixel((x, y), rgb(ramp[min(len(ramp) - 1, int(lum * len(ramp)))]))
    return img


def preview(path):
    cell, sphere, pad = 22, 72, 12
    width = pad * 3 + sphere + cell * 16
    row = sphere + pad
    img = Image.new("RGB", (width, pad + row * len(FACTIONS) + 4), (16, 18, 30))
    draw = ImageDraw.Draw(img)
    for i, faction in enumerate(FACTIONS):
        y = pad + i * row
        colours = palette(faction)
        img.paste(lit_sphere(colours, sphere), (pad, y))
        x0 = pad * 2 + sphere
        draw.text((x0, y), f"{faction}: {FACTIONS[faction][0]}", fill=(243, 233, 210))
        for j, (_, value) in enumerate(colours):
            x = x0 + j * cell + (6 if j >= 8 else 0) + (6 if j >= 13 else 0)
            draw.rectangle([x, y + 20, x + cell - 3, y + 20 + cell * 2], fill=rgb(value))
    img = img.resize((img.width * 2, img.height * 2), Image.NEAREST)
    img.save(path)


def main():
    os.makedirs(OUT, exist_ok=True)
    for faction in FACTIONS:
        colours = palette(faction)
        write_gpl(faction, colours)
        write_png(faction, colours)
    preview(os.path.join(OUT, "palettes_preview.png"))
    print(f"{len(FACTIONS)} palettes written to {OUT}")


if __name__ == "__main__":
    main()
