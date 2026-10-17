"""Writes Assets/Documentation/Card Art Briefs.txt: every card's picture brief, by faction.

The brief is the card's "picture" in Assets/Cards/<Subfaction>.json; for cards without one, the comment that opens its
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
    "Spite Voter": ["2026-10-17: the user's reference, the Raiders of the Lost Ark idol-swap meme: VOTERS (Indiana Jones, weighing it up at the temple pedestal) swapping the golden idol, labelled CORRUPT POLITICIAN, for another golden idol labelled CORRUPT POLITICIAN.", "2026-10-17: write CORRUPT POLITICIAN on each idol; the characters on the same spots and the same camera angle as the reference (the lower panel: the adventurer behind the pedestal, centre, the swapped idol in his left hand, the pedestal's to the right).", "2026-10-17: that version, with the head lowered; no 'Not Me' badge."],
    "White Dwarf": ["2026-10-17: a dwarf like in The Lord of the Rings."],
    "Blessing in Disguise": ["2026-10-17: something that references the story: a farmer's horse runs away; his neighbours say 'What bad luck!'; the farmer replies 'Maybe.'"],
    "The Friends We Made Along the Way": ["2026-10-17: Jesus with two guns instead of the cultist."],
    "Domino Effect": ["2026-10-17: add a housing market flavour."],
    "Tourist Trap": ["2026-10-17: put a beach."],
    "Mass Emigration": ["2026-10-17: put Istanbul's Hagia Sophia (the user's photo: the Sultan Ahmed Mosque at sunset over the Bosphorus, six minarets).",
                        "2026-10-17: then, told the stories of both: the Hagia Sophia (Claude's recommendation, the building that changed hands) over the Blue Mosque."],
    "Strike": ["2026-10-14: a corrupt head of the works council eating lobster while the workers get nothing; the boss is happy."],
    "Mental Health Day": ["2026-10-14: it criticises how superficially the topic is treated: a nice day to have, but zero measures from the "
                          "government to tackle mental health problems; and to rub salt in the wound, the only people backing it are cringe "
                          "mystic, shamanic, ayurveda- and reiki-loving Karens."],
    "Console Memory Card": ["2026-10-15: after the meme of the PS2 8 MB memory card: the save data lives on the card because "
                            "the console has the memory of a goldfish."],
    "Scope Creep": ["2026-10-14: a Gantt chart instead of the creep with the spyglass."],
    "Mox Nox": ["2026-10-14: some cards are just Latin words the user finds interesting; they don't need a joke."],
    "HAARP": ["2026-10-14: an underground installation with a device like a strong laser pointed at an immense hole in the "
              "ground, toward the Earth's center (an earthquake card)."],
    "Phoenix Feather": ["2026-10-14: only the feather, in the colors of the reference (a watercolor feather: red at the "
                        "quill, orange and yellow, flame-like barbs, a glowing eye near the top, the tip burning lilac)."],
    "The Chosen One": ["2026-10-14: delete the ball of fire; let Jesus with guns steal the show."],
    "Multipass": ["2026-10-14: after the film still (The Fifth Element)."],
    "Unicorn": ["2026-10-14 (pixel art): rework the mane and the chart's downturn arrow behind; the fire layer on top of the "
                "unicorn, so it looks like it's on fire.",
                "2026-10-14: then: keep only the first, shorter fire on the ground around it; no fire climbing over it.",
                "2026-10-14: then: that short fire in front of the unicorn."],
    "Schrodinger's Cat": ["2026-10-14 (pixel art): no 50% printed on the box."],
    "Killer Rabbit of Caerbannog": ["2026-10-14 (pixel art): skeletons in crusader armor around the bunny's lair; the bunny smaller, same design."],
    "Supreme Court": ["2026-10-14 (pixel art): no gavel."],
    "Sherman Tank": ["2026-10-14 (pixel art): smoke behind the tank."],
    "Legacy Code": ["2026-10-14 (pixel art): a scroll with HERE BE DRAGONS in ASCII art (the user's reference: the dragon by snd).",
                    "2026-10-14: no cobweb (it read as a wifi signal) and no duct tape on the computer."],
    "Golden Dome": ["2026-10-14 (pixel art): redo it; Netanyahu as the man standing there (the user's reference photo: silver hair "
                    "swept back, dark suit, white shirt, red tie, an Israeli flag pin, a smirk).", "2026-10-14: no flag pin."],
    "Surveillance Giraffe": ["2026-10-14 (pixel art): keep its shape; no blobs sticking out of it; no camera (the white thing) on its head.",
                             "2026-10-14: just absurd, and that's fine: nice to have a giraffe around. No faction cue needed."],
    "Keyboard Warrior": ["2026-10-14 (pixel art): like Rambo but fat, holding the keyboard as if it were a sword.",
                         "2026-10-14: the arms in the belly's lighter skin tone, smooth where they meet the shoulders; the "
                         "face the typical redditor meme face (the user's references: a tall bald dome, glasses, brow "
                         "creases, a smug toothy grin, a neckbeard), in our own colors, not the reference's.",
                         "2026-10-14: the head exactly like the user's cropped soyjak: tilted back, three-quarters to the "
                         "right, glasses up on the face, the huge open mouth with its top teeth, the scraggly neckbeard; "
                         "the body back as it was, only the arms redone.", "2026-10-14: redo the face (it came out rough).",
                         "2026-10-14: the redo was worse: keep the earlier soyjak head."],
    "Black Knight": ["2026-10-14 (pixel art): no speech bubble; like the older picture, with King Arthur and Patsy there; the "
                     "forest has no water."],
    "Particle Accelerator": ["2026-10-14 (pixel art): after the user's photo of the LHC tunnel: the blue magnet pipe running off "
                             "down the curved tunnel, steel interconnects, cables, someone lying under it working, a white line "
                             "on the floor."],
    "Cleaning Robot": ["2026-10-14 (pixel art): after the pool-cleaning robot in Love, Death & Robots (the user's still): flat "
                       "shading, a white angular hull at the water line of a cyan-tiled pool at night, a round disc head on an arm.",
                       "2026-10-14: make it like a Google Nest instead, more Google-themed: they take your data, and having one at home is handing it over for free."],
    "Templar Knight": ["2026-10-14 (pixel art): holding a shield with the cross; the treasure one cohesive pile, no coins flying about."],
    "Hostile Takeover": ["2026-10-14 (pixel art): the mouth a smile instead of an open box; the dorsal fin swept back, not a pyramid."],
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
    "Catfish": ["A cat and a fish loving each other.",
                "2026-10-14 (pixel art): the fish less pointy, less like a shark, in its water container (a fishbowl); the "
                "cat's ears aligned with its head.", "2026-10-14: no round fin blob on top of the fish."],
    "Placebo": ["Put a spoon of sugar.",
                "2026-10-14 (pixel art): only the spoon holding sugar, like the user's photo (a heaped spoonful, the handle "
                "rising to the right); keep the pink background.",
                "2026-10-14: more contrast between the spoon and the sugar; the end of the handle out of sight at this angle."],
    "Business Expense": ["A guy who looks like a CEO, in a full suit, going to jail."],
    "We Are Not The Same": ["The Spider-Man meme, pointing at each other."],
    "Meta Group Chat": ["Redo it with a WhatsApp-like feel."],
    "Control Room": ["Delete the guy floating in the control room."],
    "Sex Clanker": ["Use the old No Glove No Love picture for it.",
                    "2026-10-14: that picture (the knight on the heart-shaped bed) is Sex Clanker's own now, named after it; keep it."],
    "MK Ultra Test Subject": ["Make it look like the Unabomber before going into the woods."],
    "Genesis": ["Like the reference picture the user gave."],
    "Doomscrolling": ["A guy like the wojak meme, but on a phone instead of a computer.",
                      "2026-10-14 (pixel art): a clock showing 3:00 AM behind him.",
                      "2026-10-14: no cards flying about; the guy like Bojak from the memes (the bald wojak).",
                      "2026-10-14: the guy like Wojak: the doomer (the user's reference: a black beanie, purple bags under "
                      "tired eyes, stubble, a cigarette with its smoke, a black hoodie).",
                      "2026-10-14: the head in Wojak's shape (a dome over wide cheeks), not a box.",
                      "2026-10-14: redo the Wojak face; it was completely off.",
                      "2026-10-14: the redo was worse: keep the earlier one (the dome over wide cheeks)."],
    "This Is Fine": ["2026-10-14 (pixel art): no chair post (the 'log') behind the dog."],
    "Holy Hand Grenade of Antioch": ["2026-10-14 (pixel art): no pin; the priest holds up two fingers, not three."],
    "Trolley Problem": ["2026-10-14 (pixel art): like the classic drawing (the user's reference): side on, the trolley, the track "
                        "splitting, a man at the lever, one person tied on one branch and five on the other.",
                        "2026-10-14: the man at the lever stands back from the tracks, beyond the split, never between them."],
    "Script Kiddie": ["2026-10-14 (pixel art): a baby sitting in that gaming chair, looking towards us; the rest as it was."],
    "Frankenstein": ["2026-10-14 (pixel art): it was missing the monster's legs."],
    "Rage Bait": ["2026-10-14 (pixel art): an angry fish going for a bait.",
                  "2026-10-14: correct the shape of the mouth's shadow, the fins and the scales."],
    "Russian Roulette": ["2026-10-14 (pixel art): the skull centred in its slice."],
    "Monkey Hand": ["2026-10-14 (pixel art): remake it after the user's reference, the monkey's paw scene from a cartoon: in "
                    "a bazaar, a man in a red fez holding the shrivelled paw up at a cringing old merchant in a grey turban "
                    "(drawn as our own characters, not the cartoon's)."],
    "Red Herring": ["2026-10-14 (pixel art): no suit and no fin pulling at it; keep the hat, centred on the head."],
    "Sleeper Agent": ["2026-10-14 (pixel art): Agent Smith from The Matrix (the user's still) asleep on a table: drawn as the "
                      "Matrix-agent look (dark suit, slicked hair, slim black shades, coiled earpiece), falling green code behind."],
    "Hollowed-Out Volcano Lair": ["2026-10-14 (pixel art): like the You Only Live Twice set (the user's still): inside the "
                                  "crater, the round opening to the sky, a helicopter, the rocket in its gantry, the helipad, "
                                  "the world map, the control room, the crew in their jumpsuits and white helmets."],
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

# Cards whose art the user has looked at and approved as it is, with no note of their own (dated, in their words). A card with
# neither a USER note nor an entry here had its art direction decided by Claude alone: the doc marks it TO SUPERVISE
# (the user, 2026-10-17: "i want to keep track to ensure i personally supervised all of them").
SUPERVISED = {
}

# Ideas not yet cards, with the art the user asked for
USER_NOTES_BRAINSTORM = {
    "Conspiracies That Turned Out to Be True": ["2026-10-14: the conspiracy guy pointing at a wall of papers joined by red "
                                                "string (the Always Sunny meme)."],
    "Here Be Dragons": ["An ASCII-art dragon, like the reference given."],
    "No Glove No Love": ["A woman saying no-no with her finger, a bit sensual (its old picture went to Sex Clanker)."],
    "Disarm": ["2026-10-15: a guy whose arms have fallen off onto the floor while a policeman shouts \"Put your hands up! Police!\""],
}

# How every picture is made, the user's standing rules for the art
ART_RULES = [
    "Never redraw or tweak a card's art unless the user asks for that card; quirky is fine. Ask before drawing cards Claude proposes.",
    "Keep the background apart from the subject (the character or thing portrayed): one background per faction or scene,",
    "  drawn once and reused, the subject on its own layer over it.",
    "The user's notes (USER) win over the brief when a picture is redrawn.",
    "2026-10-14: no more pixel art for now. The cards use the vector (SVG) art again; the pixel pictures already made are",
    "  kept (Assets/Art/PixelCards, ArtArchive/RetroDiffusion) but not assigned.",
]

FACTION_NAMES = {"Corporate": "Corporate", "Statecraft": "Statecraft", "Offline": "Offline", "Online": "Online", "ShadowOps": "Shadow Ops.", "Tech": "Tech", "Myth": "Myth",
                 "Mind": "Mind", "Utopia": "Utopia"}


CARDS_DIR = os.path.join(ROOT, "Assets", "Cards")
CARD_FILES = ["Corporate", "Statecraft", "Offline", "Online", "ShadowOps", "Tech", "Myth", "Mind", "Utopia", "Shelved", "Potions"]


def cards_by_faction():
    """Each card file's cards (cards as data, 2026-10-17): (name, Creature or Spell, the picture brief or None)."""
    import json
    result = []
    for stem in CARD_FILES:
        with open(os.path.join(CARDS_DIR, stem + ".json"), encoding="utf-8") as f:
            data = json.load(f)
        cards = [(c["name"], "Spell" if c.get("type", "creature") == "spell" else "Creature", c.get("picture") or None) for c in data["cards"]]
        result.append((FACTION_NAMES.get(stem, stem), cards))
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
    art = open(ART, encoding="utf-8").read()
    comments = drawing_comments(art)
    drawings = drawing_of(art)
    lines = ["Card Art Briefs", "",
             "Every card's picture: the brief it's drawn from, and the user's own notes where they gave some (those win when",
             "the picture is redrawn). Written by Tools/art_briefs.py from the card files' 'picture' briefs and the",
             "drawing comments in generate_card_art.py; the user's notes live in that script. Rerun it after any card or art",
             "change.", "", "RULES", ""] + [f"  {rule}" for rule in ART_RULES] + [""]
    missing = 0
    factions = cards_by_faction()
    unchecked = [(faction, name) for faction, cards in factions for name, _, _ in cards
                 if name not in USER_NOTES and name not in SUPERVISED]
    lines += ["TO SUPERVISE", "",
              f"  {len(unchecked)} cards whose art direction Claude decided alone: no note from the user and no sign-off yet. Once",
              "  the user has checked one, its approval goes into SUPERVISED in Tools/art_briefs.py (or their changes into USER_NOTES).", ""]
    for faction, _ in factions:
        names = [name for f, name in unchecked if f == faction]
        if names:
            lines.append(f"  {faction} ({len(names)}): " + ", ".join(names))
    lines.append("")
    for faction, cards in factions:
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
            if name in SUPERVISED:
                lines.append(f"    SUPERVISED: {SUPERVISED[name]}")
            elif name not in USER_NOTES:
                lines.append("    TO SUPERVISE: Claude's own art direction, not checked by the user yet")
    lines += ["", "IDEAS NOT YET CARDS (Brainstorm List)", ""]
    for name, notes in USER_NOTES_BRAINSTORM.items():
        lines.append(name)
        for note in notes:
            lines.append(f"    USER: {note}")
    open(OUT, "w", encoding="utf-8", newline="\r\n").write("\n".join(lines) + "\n")
    print(f"Wrote {OUT}; {missing} card(s) without a brief, {len(unchecked)} to supervise")


if __name__ == "__main__":
    main()
