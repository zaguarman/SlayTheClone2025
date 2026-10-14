"""Writes Assets/Documentation/Card Art Briefs.txt: every card's picture brief, by faction.

The brief is the card's .Picture(...) in FactionDecksGenerator.cs; for cards without one, the comment that opens its
drawing in generate_card_art.py. Descriptions the user gave for a card's art are kept in USER_NOTES below and shown with
the brief: they win over it when the picture is redrawn (pixel art included).

Usage: python Tools/art_briefs.py
"""
import os
import re

ROOT = os.path.join(os.path.dirname(__file__), "..")
GENERATOR = os.path.join(ROOT, "Assets", "Scripts", "Generators", "FactionDecksGenerator.cs")
ART = os.path.join(ROOT, "Tools", "generate_card_art.py")
OUT = os.path.join(ROOT, "Assets", "Documentation", "Card Art Briefs.txt")

# What the user asked for, in their words (dated), per card name. Add to it whenever the user describes a card's art.
USER_NOTES = {
    "HAARP": ["2026-10-14: an underground installation with a device like a strong laser pointed at an immense hole in the "
              "ground, toward the Earth's center (an earthquake card)."],
    "Phoenix Feather": ["2026-10-14: only the feather, in the colors of the reference (a watercolor feather: red at the "
                        "quill, orange and yellow, flame-like barbs, a glowing eye near the top, the tip burning lilac)."],
    "The Chosen One": ["2026-10-14: delete the ball of fire; let Jesus with guns steal the show."],
    "Multipass": ["2026-10-14: after the film still (The Fifth Element)."],
    # From earlier sessions
    "Dogpile": ["A big pile of dogs on top of someone."],
    "Generational Trauma": ["The meme of the woman holding something above water in a swimming pool with a skeleton at the bottom, "
                            "with the generations: the one before the boomers holds the boomers above water, the next "
                            "generations drowned underwater."],
    "Its A Trap": ["Homer and the free boat: the trap where they want to get him to the police office to give him a fine."],
    "Burn Notice": ["A Banksy painting shredding itself."],
    "Antivirus": ["Put a panda on it."],
    "Counter-Intelligence": ["Like the first Counter-Strike cover, but instead of weapons they have thick glasses and "
                             "calculators aimed at each other."],
    "Catfish": ["A cat and a fish loving each other."],
    "Placebo": ["Put a spoon of sugar."],
    "Business Expense": ["A guy who looks like a CEO, in a full suit, going to jail."],
    "We Are Not The Same": ["The Spider-Man meme, pointing at each other."],
    "Meta Group Chat": ["Redo it with a WhatsApp-like feel."],
    "Control Room": ["Delete the guy floating in the control room."],
    "Sex Clanker": ["Use the old No Glove No Love picture for it."],
    "MK Ultra Test Subject": ["Make it look like the Unabomber before going into the woods."],
    "Genesis": ["Like the reference picture the user gave."],
    "Doomscrolling": ["A guy like the wojak meme, but on a phone instead of a computer."],
    "The Holy Grail": ["Remove the pin."],
    "The Lost Ark": ["Make the guy look more like Indiana Jones."],
    "Omen Reading": ["The reader more centered at the table, his body showing."],
    "Fat Tony": ["Keep it as it is (a historical figure)."],
}
# The older drawings had no brief written down: what they show, described from the pictures (2026-10-14)
DRAWN_BRIEFS = {
    "Day Trader": "A sweating trader in a headset, worried, under three wall screens of stock charts, two up and one crashing; office towers behind.",
    "Pluriemployed Hustler": "A delivery worker in a teal cap running past (speed lines) with a delivery box, a row of five rating stars above him; office towers behind.",
    "Unicorn": "A white unicorn startup with a gold horn and a rainbow mane, standing on fire, a red chart line falling behind it; office towers behind.",
    "Fat Tony": "A mobster in a fedora, dark shirt and red tie, smirking with a smoking cigar; office towers behind.",
    "Charles Ponzi": "A smiling little man in a suit beside a pyramid of champagne glasses, the top one overflowing with gold coins; office towers behind.",
    "Hostile Takeover": "A grey shark in a red tie swimming through the office towers, jaws open, a red chart line falling behind it.",
    "Mass Layoffs": "A cardboard box of desk belongings (a plant, a mug) on its way to the exit door, a red chart line falling; office towers behind.",
    "Team Building Exercise": "Three glum coworkers in party hats under bunting; office towers behind.",
    "Spite-Elected President": "A shouting blond president at a podium with a gold star, an American flag behind him; sunburst.",
    "Internal Revenue Service": "A tax collector in a top hat and round glasses beside a money bag and gold coins; sunburst.",
    "Mercenaries": "A soldier in a helmet and sunglasses carrying a rifle, running (speed lines), gold coins at his feet; sunburst.",
    "Man o' War": "A three-masted wooden warship with white sails and a row of gun ports on the sea; sunburst.",
    "Supreme Court": "A white columned courthouse with a judge's gavel beside it; sunburst.",
    "Protocol Enforcers": "Two bodyguards in black suits and dark glasses with earpieces behind a gold velvet rope; sunburst.",
    "Demagogue": "A grinning man in red shouting through a megaphone, sound waves spreading; sunburst.",
    "Martial Law": "A barred jail window under a crescent moon at night, searchlights sweeping.",
    "Mutually Assured Destruction": "A mushroom cloud on an orange sky, two missiles flying toward each other from opposite corners.",
    "Boomer": "An angry old man with white hair and glasses shaking his fist; a vaporwave sunset and grid behind him, a cloud.",
    "Hipster": "A bearded man in a beanie and round glasses with a takeaway coffee; vaporwave sunset and grid.",
    "Underdog": "A sad brown puppy with a bandage on its head; vaporwave sunset and grid.",
    "Hikikomori": "A pale recluse in headphones lit by the glow of a tablet; vaporwave sunset and grid.",
    "Reality TV Star": "A woman in sunglasses and a pink top under a spotlight and sparkles, faceless grey extras around her; vaporwave sunset and grid.",
    "Football Hooligans": "Three shouting fans in green with scarves, a flare burning behind them; vaporwave sunset and grid.",
    "Antihero": "A scowling figure in a dark hood with a cigarette; vaporwave sunset and grid.",
    "Bodybuilder": "A grinning bodybuilder in a headband lifting a barbell overhead, an energy drink can with a lightning bolt beside him; vaporwave sunset and grid.",
    "Toxic Positivity": "A huge yellow smiley face with a crack running through it and a sticking plaster on its cheek; vaporwave background.",
}

# Ideas not yet cards, with the art the user asked for
USER_NOTES_BRAINSTORM = {
    "Conspiracies That Turned Out to Be True": ["2026-10-14: the conspiracy guy pointing at a wall of papers joined by red "
                                                "string (the Always Sunny meme)."],
    "Here Be Dragons": ["An ASCII-art dragon, like the reference given."],
    "No Glove No Love": ["A woman saying no-no with her finger, a bit sensual (its old picture went to Sex Clanker)."],
}

# How every picture is made, the user's standing rules for the art
ART_RULES = [
    "Never redraw or tweak a card's art unless the user asks for that card; quirky is fine. Ask before drawing cards Claude proposes.",
    "Keep the background apart from the subject (the character or thing portrayed): one background per faction or scene,",
    "  drawn once and reused, the subject on its own layer over it.",
    "The user's notes (USER) win over the brief when a picture is redrawn.",
]

FACTION_NAMES = {"Corporate": "Corporate", "Statecraft": "Statecraft", "Zeitgeist": "Zeitgeist",
                 "DigitalSociety": "Digital Society", "ShadowOps": "Shadow Ops.", "Tech": "Tech", "Myth": "Myth",
                 "Mind": "Mind", "Utopia": "Utopia"}


def cards_by_faction(source):
    sections = list(re.finditer(r"private static List<CardDef> (\w+)Cards\(\)", source))
    result = []
    for i, section in enumerate(sections):
        end = sections[i + 1].start() if i + 1 < len(sections) else len(source)
        body = source[section.start():end]
        starts = list(re.finditer(r'^\s*(Creature|Spell)\("((?:[^"\\]|\\.)*)"', body, re.M))
        cards = []
        for j, card in enumerate(starts):
            stop = starts[j + 1].start() if j + 1 < len(starts) else len(body)
            chunk = body[card.start():stop]
            picture = re.search(r'\.Picture\("((?:[^"\\]|\\.)*)"\)', chunk)
            name = card.group(2).replace('\\"', '"')
            cards.append((name, card.group(1), picture.group(1).replace('\\"', '"') if picture else None))
        result.append((FACTION_NAMES.get(section.group(1), section.group(1)), cards))
    return result


def drawing_comments(art):
    # Each drawing's comments: the ones that open it (its brief), or else the ones along its body (what each part shows)
    comments = {}
    defs = list(re.finditer(r"^def (\w+)\(\):\n", art, re.M))
    for i, match in enumerate(defs):
        end = defs[i + 1].start() if i + 1 < len(defs) else len(art)
        body = art[match.end():end]
        opening = re.match(r"((?:    #.*\n)+)", body)
        lines = opening.group(1).splitlines() if opening else re.findall(r"^\s+#.*$", body, re.M)
        text = " ".join(line.strip()[1:].strip() for line in lines)
        if text:
            comments[match.group(1)] = text if opening else f"(from the drawing) {text}"
    return comments


def drawing_of(art):
    # The art script's own map from a card's asset name to the function that draws it
    return dict(re.findall(r'"(\w+)":\s*(\w+)\b', art))


def asset_name(card):
    return re.sub(r"[^A-Za-z0-9]", "", card)


def function_name(card):
    return re.sub(r"_+", "_", re.sub(r"[^a-z0-9]+", "_", card.lower())).strip("_")


def main():
    source = open(GENERATOR, encoding="utf-8").read()
    art = open(ART, encoding="utf-8").read()
    comments = drawing_comments(art)
    drawings = drawing_of(art)
    lines = ["Card Art Briefs", "",
             "Every card's picture: the brief it's drawn from, and the user's own notes where they gave some (those win when",
             "the picture is redrawn). Written by Tools/art_briefs.py from the deck generator's .Picture(...) briefs and the",
             "drawing comments in generate_card_art.py; the user's notes live in that script. Rerun it after any card or art",
             "change.", "", "RULES", ""] + [f"  {rule}" for rule in ART_RULES] + [""]
    missing = 0
    for faction, cards in cards_by_faction(source):
        lines += ["", faction.upper(), ""]
        for name, kind, brief in cards:
            if brief is None:
                brief = DRAWN_BRIEFS.get(name) or comments.get(drawings.get(asset_name(name), function_name(name)))
                if brief is None:
                    missing += 1
                    brief = "(no brief yet)"
            lines.append(f"{name} ({kind.lower()})")
            lines.append(f"    {brief}")
            for note in USER_NOTES.get(name, []):
                lines.append(f"    USER: {note}")
    lines += ["", "IDEAS NOT YET CARDS (Brainstorm List)", ""]
    for name, notes in USER_NOTES_BRAINSTORM.items():
        lines.append(name)
        for note in notes:
            lines.append(f"    USER: {note}")
    open(OUT, "w", encoding="utf-8", newline="\r\n").write("\n".join(lines) + "\n")
    print(f"Wrote {OUT}; {missing} card(s) without a brief")


if __name__ == "__main__":
    main()
