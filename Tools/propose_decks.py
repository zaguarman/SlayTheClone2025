"""Rebuilds the decks from the numbers (the user, 2026-10-17: "redo all decks to include the new mechanics and to have enough
variation"): each deck keeps its name, its strategy and its subfactions, and gets the cards of those subfactions that the
studies rate best for that strategy. The shuffled tournament (Assets/Resources/ShuffledScores.json) says each card's potential
free of the decks we built; the balance report (CardScores.json) its tier in the built decks; the card files say what each is.

  python Tools/propose_decks.py            writes Assets/Documentation/Deck Proposals.txt: each deck before and after
  python Tools/propose_decks.py --apply    also rewrites the deck lists in FactionDecksGenerator.cs (then run Cards > Generate Faction Decks)

Rules: 30 cards a deck, 16 creatures and 14 spells or near it, two copies at most (the best five twice), every
card of the pool in at least one deck (one that had no deck goes to its subfaction's thinnest deck), tokens and fake faces
never. A deck's strategy favours the roles it lives on (an Aggro deck beaters and face damage, a Control deck removal and
walls...), and a card already in two decks yields to one with no deck yet, so two decks of one subfaction come out different.
"""
import io
import json
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
GEN = os.path.join(ROOT, "Assets", "Scripts", "Generators", "FactionDecksGenerator.cs")
CARDS = os.path.join(ROOT, "Assets", "Cards")
SHUFFLED = os.path.join(ROOT, "Assets", "Resources", "ShuffledScores.json")
SCORES = os.path.join(ROOT, "Assets", "Resources", "CardScores.json")
OUT = os.path.join(ROOT, "Assets", "Documentation", "Deck Proposals.txt")

DECK_SIZE, CREATURES, MOST_COPIES, DOUBLED = 30, 16, 2, 5
FILE_FACTION = {"Corporate": "Corporate", "Statecraft": "Statecraft", "Offline": "Offline", "Online": "Online", "ShadowOps": "ShadowOps",
                "Tech": "Tech", "Myth": "Myth", "Mind": "Mind", "Utopia": "Utopia"}
UMBRELLA = {"Statecraft": "Establishment", "Myth": "Establishment", "ShadowOps": "Establishment", "Corporate": "Capital", "Tech": "Capital",
            "Utopia": "Enlightenment", "Mind": "Enlightenment", "Online": "Digital Society", "Offline": "Digital Society"}
# The roles each strategy lives on (the shuffled report's role names), with how much they count
STRATEGY_ROLES = {
    "Aggro": {"Beater": 2.0, "Face damage": 2.0, "Removal": 1.0, "Accelerator": 0.5, "Mobile": 0.5},
    "Control": {"Removal": 2.0, "Wall": 1.5, "Debuff": 1.5, "Set-up": 1.0, "Support (heal": 1.0, "Board damage": 1.0},
    "Engine": {"Accelerator": 2.0, "Support": 1.0, "Set-up": 1.0, "Swarm": 0.5, "Logistics": 0.5},
    "Swarm": {"Swarm": 2.0, "Support (buffs)": 1.5, "Beater": 0.5, "Board damage": -1.0, "Support (statuses": 1.0},
    "Midrange": {"Beater": 1.5, "Wall": 1.5, "Support": 1.0, "Removal": 1.0, "Ability creature": 0.5},
    "Combo": {"Set-up": 1.5, "Trickery": 1.5, "Accelerator": 1.0, "Debuff": 0.5, "Intel": 0.5},
}
CONTINUITY = 1.5  # A deck's own card over a stranger of nearly the same worth
SPREAD = 1.5      # A card already in two decks yields to one with no deck yet
TIER_VALUE = {"S": 2.0, "A": 1.0, "B": 0.0, "C": -1.0, "D": -2.0}


def load_cards():
    cards = {}
    for stem, faction in FILE_FACTION.items():
        with open(os.path.join(CARDS, stem + ".json"), encoding="utf-8") as f:
            data = json.load(f)
        for c in data["cards"]:
            if c.get("token") or c.get("faction"):  # Tokens and fake faces are never dealt
                continue
            cards[c["name"]] = {"faction": faction, "creature": c.get("type", "creature") != "spell", "deploy": c.get("deploy", 0),
                                "attack": c.get("attack", 0), "potential": 0.5, "games": 0, "impact": 0.0, "roles": "", "tier": None}
    with open(SHUFFLED, encoding="utf-8") as f:
        for e in json.load(f)["cards"]:
            if e["card"] in cards:
                cards[e["card"]].update(potential=e["winRate"], games=e["games"], impact=e["impact"], roles=e["roles"])
    with open(SCORES, encoding="utf-8") as f:
        for e in json.load(f)["cards"]:
            if e["card"] in cards:
                cards[e["card"]]["tier"] = e.get("tier")
    return cards


def value(card, strategy):
    """How good the card looks for a deck of this strategy: its potential (centred), its tier, its roles' fit."""
    weight = min(1.0, card["games"] / 40.0)  # Few games: the potential counts less
    v = (card["potential"] - 0.5) * 12 * weight
    if card["tier"]:
        v += TIER_VALUE.get(card["tier"], 0) * 0.8
    fit = 0.0
    for role, worth in STRATEGY_ROLES.get(strategy, {}).items():
        if role in card["roles"]:
            fit += worth
    return v + fit


def parse_decks(source):
    """The generator's decks: (name, strategy, cards, kind, span) for BestDecks and PairDecks."""
    decks = []
    best = re.search(r"BestDecks = new Dictionary<Faction, \(string, string\[\]\)> \{(.*?)\n        \};", source, re.S)
    for m in re.finditer(r'\[Faction\.(\w+)\] = \("([^"]+)", new\[\] \{(.*?)\}\),', best.group(1), re.S):
        decks.append({"faction": m.group(1), "name": m.group(2), "cards": re.findall(r'"((?:[^"\\]|\\.)*)"', m.group(3)),
                      "kind": "best", "span": (best.start(1) + m.start(3), best.start(1) + m.end(3))})
    strategies = dict(re.findall(r'\[Faction\.(\w+)\] = "(\w+)"', re.search(r"BestDeckStrategies = new Dictionary<Faction, string> \{(.*?)\};", source, re.S).group(1)))
    for d in decks:
        d["strategy"] = strategies.get(d["faction"], "Midrange")
    pairs = re.search(r"PairDecks = \{(.*?)\n        \};", source, re.S)
    for m in re.finditer(r'\("([^"]+)", "(\w+)", "([^"]*)", "([^"]*)", "(\w+)", new\[\] \{(.*?)\}\),', pairs.group(1), re.S):
        decks.append({"faction": None, "name": m.group(1), "file": m.group(2), "strategy": m.group(5), "cards": re.findall(r'"((?:[^"\\]|\\.)*)"', m.group(6)),
                      "kind": "pair", "span": (pairs.start(1) + m.start(6), pairs.start(1) + m.end(6))})
    for d in decks:
        d["cards"] = [c.replace('\\"', '"') for c in d["cards"]]
    return decks


def propose(decks, cards):
    used = {}
    for deck in decks:
        subfactions = sorted({cards[c]["faction"] for c in deck["cards"] if c in cards})
        if deck["faction"] and deck["faction"] not in subfactions:
            subfactions.append(deck["faction"])
        deck["subfactions"] = subfactions
        pool = [n for n, c in cards.items() if c["faction"] in subfactions]
        ranked = sorted(pool, key=lambda n: -value(cards[n], deck["strategy"]))
        creatures = [n for n in ranked if cards[n]["creature"]]
        spells = [n for n in ranked if not cards[n]["creature"]]
        # The deck's own best cards keep their place ahead of a stranger of the same worth: continuity
        own = set(deck["cards"])
        def worth(n):
            w = value(cards[n], deck["strategy"])
            if n in own: w += CONTINUITY
            w -= SPREAD * max(0, used.get(n, 0) - 1)
            return w
        creatures.sort(key=lambda n: -worth(n))
        spells.sort(key=lambda n: -worth(n))
        want_creatures = CREATURES if len(creatures) >= CREATURES else len(creatures)
        want_spells = DECK_SIZE - want_creatures
        picks = creatures[:want_creatures] + spells[:want_spells]
        # Two copies of the best third, one of the rest, up to the size
        picks.sort(key=lambda n: -worth(n))
        twice = picks[:DOUBLED]
        new = []
        for n in picks:
            new.append(n)
            if n in twice and len(new) < DECK_SIZE:
                new.append(n)
        new = new[:DECK_SIZE] if len(new) > DECK_SIZE else new
        while len(new) < DECK_SIZE and picks:  # Short pools: a second copy of the next best
            extra = next((n for n in picks if new.count(n) < MOST_COPIES), None)
            if extra is None:
                break
            new.append(extra)
        deck["new"] = new
        for n in set(new):
            used[n] = used.get(n, 0) + 1
    # Every card in a deck: an orphan goes to its subfaction's thinnest deck (one copy, over the size)
    for name, card in cards.items():
        if name in used:
            continue
        homes = [d for d in decks if card["faction"] in d["subfactions"]]
        if not homes:
            continue
        home = min(homes, key=lambda d: len(d["new"]))
        home["new"].append(name)
        used[name] = 1
    return decks


def report(decks, cards):
    lines = ["DECK PROPOSALS (Tools/propose_decks.py): each deck from the numbers, before and after", ""]
    for deck in decks:
        before, after = deck["cards"], deck["new"]
        kept = [n for n in dict.fromkeys(after) if n in before]
        added = [n for n in dict.fromkeys(after) if n not in before]
        dropped = [n for n in dict.fromkeys(before) if n not in after]
        lines.append(f"== {deck['name']} ({deck['strategy']}; {' + '.join(deck['subfactions'])}): {len(before)} -> {len(after)} cards")
        lines.append(f"   kept {len(kept)}: " + ", ".join(kept))
        lines.append(f"   added {len(added)}: " + ", ".join(f"{n} ({cards[n]['potential']:.0%}, {cards[n]['tier'] or '-'})" for n in added))
        lines.append(f"   dropped {len(dropped)}: " + ", ".join(f"{n} ({cards[n]['potential']:.0%}, {cards[n]['tier'] or '-'})" for n in dropped if n in cards))
        lines.append("")
    return "\n".join(lines)


def literal(name):
    return '"' + name.replace('"', '\\"') + '"'


def apply(source, decks):
    """Writes the new lists into the generator, from the last deck to the first so the spans stay right."""
    for deck in sorted(decks, key=lambda d: -d["span"][0]):
        a, b = deck["span"]
        body = "\n                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)\n                " + ", ".join(literal(n) for n in deck["new"]) + ",\n            "
        source = source[:a] + body + source[b:]
    return source


def main():
    cards = load_cards()
    source = io.open(GEN, encoding="utf-8").read()
    decks = propose(parse_decks(source), cards)
    text = report(decks, cards)
    io.open(OUT, "w", encoding="utf-8", newline="\n").write(text)
    print(text[:4000])
    print(f"... written to {os.path.normpath(OUT)}")
    if "--apply" in sys.argv:
        io.open(GEN, "w", encoding="utf-8", newline="").write(apply(source, decks))
        print("The generator's deck lists are rewritten: run Cards > Generate Faction Decks")


if __name__ == "__main__":
    main()
