"""Summarizes the game records the telemetry collected: what people (and the AI) play, how it goes, and what the cards do.

Reads every games-*.jsonl in a folder: the collector's (TelemetryData/, the default) or the game's own, before sending
(Logs/Telemetry/pending and sent). Prints:
  1. The games: by mode, how many ended, the first seat's wins, how long they last.
  2. Decks and factions: win share, by the side that played them (games that ended with a winner).
  3. Adventures: runs, how far they got, the rooms they died in.
  4. Cards: plays, damage and kills a play, and the win share of the side that played them (20+ games).

Usage:
    python Tools/telemetry_report.py                       # TelemetryData/
    python Tools/telemetry_report.py Logs/Telemetry/pending --mode match --since 2026-10-16
"""
import argparse
import glob
import json
import os
from collections import Counter, defaultdict


def load(folders, mode=None, since=None):
    records, seen = [], set()
    for folder in folders:
        for path in sorted(glob.glob(os.path.join(folder, "**", "*.jsonl"), recursive=True)):
            with open(path, encoding="utf-8-sig") as f:
                for line in f:
                    try:
                        r = json.loads(line)
                    except ValueError:
                        continue
                    if r.get("id") in seen or (mode and r.get("mode") != mode) or (since and r.get("startedUtc", "") < since):
                        continue
                    seen.add(r.get("id"))
                    records.append(r)
    return records


def share(won, played):
    return f"{won / played:4.0%}" if played else "   -"


def report(records):
    print(f"{len(records)} games")
    print()
    print("1. THE GAMES")
    by_mode = Counter(r.get("mode") for r in records)
    for mode, n in by_mode.most_common():
        games = [r for r in records if r.get("mode") == mode]
        ended = [r for r in games if r.get("ended")]
        decided = [r for r in ended if r.get("winner") in (1, 2)]
        days = sum(r.get("days", 0) for r in ended) / max(1, len(ended))
        print(f"  {mode:<11} {n:6} games, {len(ended)} ended; first seat won {share(sum(r['winner'] == 1 for r in decided), len(decided))}; {days:.1f} Days on average")
    print()

    print("2. DECKS AND FACTIONS (games with a winner)")
    decks, factions = defaultdict(lambda: [0, 0]), defaultdict(lambda: [0, 0])
    for r in records:
        if not r.get("ended") or r.get("winner") not in (1, 2):
            continue
        for side, key in ((1, "player1"), (2, "player2")):
            s = r.get(key, {})
            won = r["winner"] == side
            decks[s.get("deck", "?")][0] += won
            decks[s.get("deck", "?")][1] += 1
            factions[s.get("factions", "?")][0] += won
            factions[s.get("factions", "?")][1] += 1
    for title, table in (("Factions", factions), ("Decks", decks)):
        print(f"  {title}:")
        for name, (won, played) in sorted(table.items(), key=lambda kv: -kv[1][0] / max(1, kv[1][1])):
            print(f"    {name:<50} {share(won, played)}  of {played}")
    print()

    print("3. ADVENTURES")
    runs = defaultdict(list)
    for r in records:
        if r.get("adventure"):
            runs[r["adventure"].get("runId")].append(r)
    if not runs:
        print("  None.")
    deaths = Counter()
    for run in runs.values():
        last = max(run, key=lambda r: r["adventure"].get("row", 0))
        if last.get("ended") and last.get("winner") == 2:
            deaths[f"{last['adventure'].get('room')} in row {last['adventure'].get('row')}"] += 1
    if runs:
        rows = [max(r["adventure"].get("row", 0) for r in run) for run in runs.values()]
        print(f"  {len(runs)} runs, the furthest row reached {sum(rows) / len(rows):.1f} on average; bosses beaten "
              f"{sum(any(r['adventure'].get('room') == 'Boss' and r.get('winner') == 1 for r in run) for run in runs.values())}")
        for where, n in deaths.most_common(10):
            print(f"    died at {where}: {n}")
    print()

    print("4. CARDS (20+ games)")
    cards = defaultdict(lambda: Counter())
    for r in records:
        decided = r.get("ended") and r.get("winner") in (1, 2)
        for c in r.get("cards", []):
            k = cards[c["name"]]
            k["games"] += 1
            k["plays"] += c.get("plays", 0)
            k["damage"] += c.get("faceDamage", 0) + c.get("creatureDamage", 0)
            k["kills"] += c.get("kills", 0)
            if decided:
                k["decided"] += 1
                k["won"] += r["winner"] == c.get("side")
    print(f"    {'Card':<42} {'Games':>6} {'Win%':>5} {'Plays/g':>8} {'Dmg/play':>9} {'Kills/play':>11}")
    for name, k in sorted(cards.items(), key=lambda kv: -kv[1]["won"] / max(1, kv[1]["decided"])):
        if k["games"] < 20:
            continue
        print(f"    {name:<42} {k['games']:6} {share(k['won'], k['decided'])} {k['plays'] / k['games']:8.1f} "
              f"{k['damage'] / max(1, k['plays']):9.1f} {k['kills'] / max(1, k['plays']):11.2f}")


def main():
    parser = argparse.ArgumentParser(description="SlayTheClone telemetry report")
    parser.add_argument("folders", nargs="*", default=[os.path.join(os.path.dirname(__file__), "..", "TelemetryData")])
    parser.add_argument("--mode", help="match, adventure, autopilot or simulation")
    parser.add_argument("--since", help="only games started on or after this date (YYYY-MM-DD)")
    args = parser.parse_args()
    report(load(args.folders, args.mode, args.since))


if __name__ == "__main__":
    main()
