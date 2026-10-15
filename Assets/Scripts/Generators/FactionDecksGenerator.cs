#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Builds a starter deck for each faction from the card lists below (the smaller factions play some cards twice), two
    /// example two-faction decks, and the Mechanics Test deck that mixes them all to try every mechanic. It also fills the menu's deck catalog: the decks, their opponents, the test deck.
    /// They only use effects the rules already carry out; each card's designNotes say what it should become once the
    /// GDD mechanic it stands in for exists (overview in Assets/Documentation/Starter Faction Decks.txt).
    /// Cards go to Assets/Scriptables/Cards/&lt;Faction&gt;/, their art comes from Tools/generate_card_art.py.
    /// Running it again updates the cards in place and keeps their ids and art.
    /// </summary>
    public static class FactionDecksGenerator {
        private const string CardsRoot = "Assets/Scriptables/Cards";
        private const string DecksFolder = "Assets/Scriptables/Decks";
        private const string CardPrefabPath = "Assets/Prefabs/CardPrefab.prefab";

        // Spells aim at whatever they are dropped on; these make an action pick its own targets instead
        private const TargetModifier Each = TargetModifier.AllSameTypeTargets;
        private const TargetModifier AtRandom = TargetModifier.Random;
        private const TargetModifier StrongestOne = TargetModifier.Strongest;
        private const TargetModifier Facing = TargetModifier.Opposite;

        [MenuItem("Cards/Generate Faction Decks")]
        public static void GenerateFactionDecks() {
            var written = new Dictionary<string, CardDataScriptableObject>();
            var offered = new List<DeckCatalog.Opponent> {
                Offer(Build(Faction.Corporate, CorporateCards(), written), "Your Manager", "Has scheduled a quick sync about your attitude."),
                Offer(Build(Faction.Statecraft, StatecraftCards(), written), "The Ministry of Forms", "Your existence is pending approval."),
                Offer(Build(Faction.Zeitgeist, ZeitgeistCards(), written), "Your Group Chat", "47 unread messages. All of them are about you."),
                Offer(Build(Faction.Utopia, UtopiaCards(), written), "A Billionaire Bound For Mars", "Will save humanity. Shipping not included."),
                Offer(Build(Faction.DigitalSociety, DigitalSocietyCards(), written), "Your Feed", "Curated just for you. Every post is about how wrong you are."),
                Offer(Build(Faction.ShadowOps, ShadowOpsCards(), written), "Your Quartermaster", "Please return the car in one piece. You never do."),
                Offer(Build(Faction.Tech, TechCards(), written), "The IT Department", "Have you tried turning your strategy off and on again?"),
                Offer(Build(Faction.Myth, MythCards(), written), "The Oracle", "Saw this loss coming. Didn't warn you."),
                Offer(Build(Faction.Mind, MindCards(), written), "Your Inner Critic", "Knows exactly what to say. Always the worst thing."),
            };

            foreach (var pair in PairDecks) {
                var deck = WriteDeck(pair.Name, pair.Cards.Select(name => written[name]).ToList(), $"{DecksFolder}/{pair.File}.asset");
                offered.Add(Offer(deck, pair.Opponent, pair.Tagline));
            }

            var testDeck = WriteDeck("Mechanics Test", MechanicsTestCards.Select(name => written[name]).ToList(), $"{DecksFolder}/MechanicsTestDeck.asset");
            var workbench = WriteDeck(WorkbenchName, WorkbenchCards().Select(name => written[name]).ToList(), $"{DecksFolder}/WorkbenchDeck.asset");
            FillCatalog(offered, workbench, testDeck);
            SetFocus(offered.Select(o => o.deck).Append(workbench).Append(testDeck));
            AssetDatabase.SaveAssets();
            Debug.Log("Generated the faction decks, the Workbench and the Mechanics Test deck. Run Cards > Assign Card Art to link their art.");
        }

        // The cards being worked on now, to test in a match: two of each, filled up to 20 with their faction's best deck. It's
        // first in the menus. Change it with the work
        private static readonly (Faction Faction, string[] Cards) Workbench = (Faction.Corporate, new[] { "Laissez-Faire", "The Invisible Hand", "C-Suite", "Inflation" });
        private static string WorkbenchName => $"Workbench - {string.Join(", ", Workbench.Cards)}";

        // The decks being worked on now, the most important first: the menus list them right after the Workbench, in this
        // order, then the rest by when their cards last changed. Change it with the work
        private static readonly string[] Focus = { "Corporate - Shareholder Value", "Myth - Old Gods, New Tricks", "Zeitgeist - Moods of Modern Life" };

        private static List<string> WorkbenchCards() {
            var cards = Workbench.Cards.SelectMany(card => new[] { card, card }).ToList();
            cards.AddRange(BestDecks[Workbench.Faction].Cards.Where(card => !Workbench.Cards.Contains(card)).Take(PlayerDecks.MinDeckSize - cards.Count));
            return cards;
        }

        private static void SetFocus(IEnumerable<DeckScriptableObject> decks) {
            foreach (var deck in decks) {
                string deckName = deck.GetDeckName();
                int rank = deckName == WorkbenchName ? 1 : System.Array.IndexOf(Focus, deckName) is int i && i >= 0 ? i + 2 : 0;
                var serializedDeck = new SerializedObject(deck);
                serializedDeck.FindProperty("focus").intValue = rank;
                serializedDeck.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(deck);
            }
        }

        // Example two-faction decks (Faction Compendium 5, Example Two-Faction Decks): pairs that play well together, 20 cards each.
        // And Black Budget, a second Shadow Ops. deck made of its newer cards (kills without fingerprints, stolen assets,
        // staged deaths, decoys, information trades).
        // Panopticon: Digital Society marks the enemy with Heat Signature and Shadow Ops. reveals them, so every random
        // effect finds its target. Prosperity Gospel: Myth's sacrifices feed Corporate's draw engine.
        private static readonly (string Name, string File, string Opponent, string Tagline, string[] Cards)[] PairDecks = { };

        // Each faction's deck (2026-10-14): its best 20 cards and combos, picked from the AI games' card stats (damage, kills,
        // win share per card), before any balance study. Two copies at most. The old decks (and the two-faction ones) are gone
        private static readonly Dictionary<Faction, (string Name, string[] Cards)> BestDecks = new Dictionary<Faction, (string, string[])> {
            // Board-wide damage that draws cards, removal, and bodies that grow
            [Faction.Corporate] = ("Corporate - Shareholder Value", new[] {
                "Margin Call", "Margin Call", "Hostile Takeover", "Hostile Takeover", "Inflation", "Inflation", "Dead Cat Bounce", "Dead Cat Bounce",
                "Taxpayer Funds Venture Capital Profits", "Taxpayer Funds Venture Capital Profits", "Team Building Exercise", "Casino",
                "Fat Tony", "Fat Tony", "Cleaning Robot", "Cleaning Robot", "Day Trader", "Micromanager", "Charles Ponzi", "Pluriemployed Hustler",
            }),
            // Big bodies that buff each other on arrival, and Mutually Assured Destruction that they survive
            // Its control list: big bodies that buff each other, mines, shields and paperwork, and Mutually Assured Destruction
            [Faction.Statecraft] = ("Statecraft - Order & Paperwork", new[] {
                "Mutually Assured Destruction", "Preemptive Strike", "Lost Landmine", "Ceasefire", "Red Tape", "Raised Security Level",
                "Panem Et Circenses", "Martial Law", "Supply Depot", "Man o' War", "Diplomat", "Spite-Elected President", "Supreme Court",
                "Demagogue", "King Emeritus", "Ministry for Equality", "Mercenaries", "Police Officer Productivity Target",
                "Protocol Enforcers", "Internal Revenue Service",
            }),
            // Chaos on both boards that its sturdy creatures shrug off, and Double Tap on a big hitter
            [Faction.Zeitgeist] = ("Zeitgeist - Moods of Modern Life", new[] {
                "This Is Fine", "This Is Fine", "Russian Roulette", "Russian Roulette", "Football Hooligans", "Football Hooligans",
                "Hipster", "Hipster", "Boomer", "Boomer", "Karen", "Karen", "Antihero", "Antihero", "Bodybuilder", "Bodybuilder",
                "Chain Smoker", "Reality TV Star", "Toxic Positivity", "Toxic Positivity",
            }),
            // Accelerationism: creatures taken to the brink, then boom; Black Hole and Splitting The Atom clear the way
            [Faction.Utopia] = ("Utopia - Results May Vary", new[] {
                "Accelerationism", "Accelerationism", "Black Hole", "Black Hole", "Biohacking", "Biohacking", "Splitting The Atom", "Splitting The Atom",
                "Cold Fusion", "Cold Fusion", "Antimatter", "Antimatter", "Resurrected T-Rex", "Resurrected T-Rex",
                "Blood Transfusion", "Evolution Theory", "Scientific Method", "Particle Accelerator", "Chimera", "Frankenstein",
            }),
            // A swarm of cheap bodies and damage that spreads
            [Faction.DigitalSociety] = ("Digital Society - Terminally Online", new[] {
                "Clickbait", "Clickbait", "Dogpile", "Dogpile", "Keyboard Warrior", "Keyboard Warrior", "NPC", "NPC",
                "Crypto \"Entrepreneur\"", "Crypto \"Entrepreneur\"", "Digital Nomad", "Digital Nomad", "Meta Group Chat", "Meta Group Chat",
                "Conspiracy Theorist", "Conspiracy Theorist", "Rage Bait", "Cancelled", "Social Panopticon", "Chronically Online",
            }),
            // Double-O Section's agents and gadgets with Black Budget's best: Ambush, licences, Last Wills that kill
            [Faction.ShadowOps] = ("Shadow Ops. - Licence to Kill", new[] {
                "Femme Fatale", "Femme Fatale", "If I Told You, I'd Have to Kill You", "If I Told You, I'd Have to Kill You",
                "Licence to Kill", "Licence to Kill", "Cleaner", "Cleaner", "MK Ultra Test Subject", "MK Ultra Test Subject",
                "Dead Man's Switch", "Dead Man's Switch", "Die Another Day", "Shaken, Not Stirred", "The Golden Gun", "False Flag Operation",
                "GoldenEye", "Supply Chain Attack", "Wet Work", "Human Intelligence",
            }),
            // Its hardest-hitting creatures, stuns and Watchdogs
            [Faction.Tech] = ("Tech - Move Fast and Break Things", new[] {
                "Technical Debt", "Technical Debt", "Legacy Code", "Legacy Code", "Scope Creep", "Scope Creep", "Script Kiddie", "Script Kiddie",
                "SysAdmin", "SysAdmin", "Sex Clanker", "Sex Clanker", "DDOS Attack", "DDOS Attack", "Ransomware Attack", "Ransomware Attack",
                "Antivirus", "Man-in-the-Middle", "Web Scraping", "Meatproxy As A Service",
            }),
            // Holy War and the wrath cards, blessed knights, and sacrifices that pay
            [Faction.Myth] = ("Myth - Old Gods, New Tricks", new[] {
                "Holy War", "Holy War", "Lance Of Longinus", "Lance Of Longinus", "Horsemen Of Apocalypse", "Horsemen Of Apocalypse",
                "Templar Knight", "Templar Knight", "Black Knight", "Black Knight", "The Wicker Man", "Necronomicon", "The Great Flood",
                "Holy Hand Grenade of Antioch", "The Chosen One", "Buddy Christ", "Killer Rabbit of Caerbannog", "Living Dead", "Mox Nox", "Pope",
            }),
            // Sturdy creatures that wear the enemy down, and stat cuts that kill
            [Faction.Mind] = ("Mind - All in Your Head", new[] {
                "Pessimist", "Pessimist", "Introvert", "Introvert", "Ship of Theseus", "Ship of Theseus", "Escape Goat", "Red Herring",
                "Trolley Problem", "Trolley Problem", "Roko's Basilisk", "Roko's Basilisk", "Imposter Syndrome", "Imposter Syndrome",
                "Schrodinger's Cat", "Schrodinger's Cat", "Mimetic Theory", "Mimetic Theory", "Escape Goat", "Red Herring",
            }),
        };

        // One or more cards for each mechanic the rules carry out (movement, Exhausted, Doomed, Health Loss, Armor, Homing,
        // the death sequence, Status Timing, Deployment Time, Blessed, Cleanse and Heresy, Slot Effects, Watchdogs, abilities,
        // Fog of War), from every faction:
        // pick it to try them all in one match
        private static readonly string[] MechanicsTestCards = {
            "Delivery Cyclist", "Political Decoy",
            "DDOS Attack", "Dogpile", "Memento Mori", "Martyr", "Generational Trauma",
            "Diplomat", "Keyboard Warrior", "Rage Bait", "Antivirus", "Counter-Intelligence",
            "The Holy Grail", "Exorcism", "Plot Armor", "Red Tape",
            "Lost Landmine", "Its A Trap", "Ceasefire", "Burn Notice",
            "Mole", "Signal Jammer", "Deep Cover", "Script Kiddie", "Legacy Code",
            "NPC", "Chronically Online", "Mummy", "Pessimist", "Introvert",
            "Sleeper Agent", "Smoke And Mirrors", "Eavesdrop", "Tapped Communications", "End-to-End Encryption",
            "Work Shift Rotation", "Chain Smoker", "Zeitgeist", "Rush Hour",
            "Golden Dome", "Presidential Pardon", "Counterfeit", "Private Equity", "Congruent Use of Force", "Crypto \"Entrepreneur\"",
            "Cleaner", "Shell Company", "Weapons of Mass Distraction", "Not on My Watch", "One for the Road", "Mimetic Theory",
        };

        #region Corporate
        // Pays with capital (future: discard cards). Until discarding exists, its strongest cards leak value to the enemy.
        // Can't heal. Engine: card draw, workers that die usefully, money that hurts someone else first.
        private static List<CardDef> CorporateCards() => new List<CardDef> {
            Spell("Casino", TargetType.Player,
                "The house always wins. Except the houses he owned.",
                "A Trend for 3 Days: each Dawn the house's wheel spins the Day's weather for both sides (Heatwave, Rain, Hail, Tailwind, Rush Hour, Windfall), like the weather in a Pokemon battle. Corporate bets on tempo: whoever adapts best to the Day wins.",
                SetTrend(Trend.Casino, 3))
                .Picture("A caricature of Donald Trump in a dark suit and an overlong red tie, his blond hair swept over, grinning and throwing a fistful of casino chips in the air, in front of his gaudy gold-and-purple casino with onion domes and a giant neon sign, a red BANKRUPT stamp across the sign."),
            Spell("Headhunter", TargetType.Player,
                "We found you on LinkedIn. We found everyone on LinkedIn.",
                "Corporate's recruiting: Discard 1, then send for a creature from your deck; it joins your next hand.",
                SendForCard(SendForFilter.Creature))
                .Discarding(1, chosen: true)
                .Picture("A slick recruiter in a sharp suit reeling in a startled employee from a rival's office tower with a fishing rod baited with a stock-options contract."),
            Creature("Counterfeit", 1, 1, speed: 2, deploy: 0,
                "Genuine replica. 100% authentic copy.",
                "Mechanics test: Wannabe (Active, hold Shift as you drag it onto a creature): its Attack, Health and Speed become that creature's.")
                .With(CreatureAbility.Wannabe).NotInStarterDeck()
                .Picture("A knockoff superhero in a cheap costume with a misspelled logo on the chest, flexing proudly in front of a mirror that shows a real muscled hero."),
            Creature("Private Equity", 2, 4, speed: 1, deploy: 1,
                "We don't buy companies. We buy what they used to be worth.",
                "Mechanics test: Credit Grab (Active, once per combat): steals target creature's stat raises. Buff an enemy, or let them, then take it.")
                .With(CreatureAbility.CreditGrab).NotInStarterDeck()
                .Picture("A vampire banker in a pinstripe suit sucking gold coins out of a little family shop through a long straw."),
            Creature("Shell Company", 1, 4, speed: 2, deploy: 0,
                "Registered in four countries. Exists in none.",
                "Mechanics test: Shell Game (Active, hold Shift as you drag it onto a creature): that creature trades places with the one to its right. Moves an enemy into a planned attack, or out of the way of one.")
                .With(CreatureAbility.ShellGame).NotInStarterDeck()
                .Picture("A street hustler playing the shell game with three walnut shells, each stamped with a different tax haven's flag, a gold coin under one."),
            Creature("Work Shift Rotation", 2, 3, speed: 1, deploy: 1,
                "Same job, new face. The vest is still warm.",
                "Handover: when a summon replaces it, the newcomer takes over its stat changes and statuses. Buff it, then send in the next shift.")
                .With(CreatureAbility.Handover)
                .Picture("Two warehouse workers at the clocking-in machine: the exhausted one hands his hi-vis vest, still warm, to the fresh one."),
            Creature("Unpaid Intern", 1, 1, speed: 3, deploy: 0,
                "Paid in experience. The experience is this.",
                "Future: a Worker token created by other cards rather than drafted. Fodder for sacrifice costs.",
                On(EffectTrigger.OnDeath, Draw(1))),
            Creature("Inhuman Resources", 1, 4, speed: 1, deploy: 1,
                "We're here for you. \"You\" meaning the company.",
                "Future: Layoffs. Destroy a friendly Worker as an additional cost; schedule 2 draws.",
                On(EffectTrigger.StartOfTurn, Damage(1, TargetType.FriendlyCreatures, AtRandom), Draw(1))),
            Creature("Micromanager", 2, 3, speed: 1, deploy: 1,
                "Per my last email. And the one before. And the meeting about the emails.",
                "Future: gives a friendly Worker Coffeinated instead of a plain attack buff.",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, AtRandom))),
            Creature("Multilevel Marketing Seller", 1, 2, speed: 2, deploy: 0,
                "It's not a pyramid. It's a triangle of opportunity.",
                "Future: summons an Unpaid Intern token. Each recruit pays a share of its draws upward.",
                On(EffectTrigger.OnPlay, Summon(1))),
            Creature("Day Trader", 3, 1, speed: 3, deploy: 0,
                "Up 300% this morning. Rent is due this afternoon.",
                "Insider Job (insider trading): its damage ignores Armor. Future: Investment. Pays out or loses cards depending on the Day.",
                On(EffectTrigger.OnPlay, Draw(1)),
                On(EffectTrigger.OnDeath, Draw(1, TargetType.Enemy)))
                .With(CreatureAbility.InsiderJob),
            Creature("Pluriemployed Hustler", 2, 3, speed: 2, deploy: 0,
                "Three jobs, two side hustles, one kidney left.",
                "Penny Stock: small bets that pay double, so hits of 3 or less are doubled. Future: Coffeinated, then Burnout (the Hustle Culture pairing with Zeitgeist).",
                On(EffectTrigger.OnPlay, Draw(2)),
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self)))
                .With(CreatureAbility.PennyStock),
            Creature("Unicorn", 1, 8, speed: 1, deploy: 2,
                "Valued at a billion. Burns a million a day. Profitable next year, every year.",
                "Future: Unicorn (Enterprise) from the card examples. Burn rate stays: Corporate can't heal it.",
                On(EffectTrigger.StartOfTurn, Draw(1)),
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self))),
            Creature("Multimillionaire Philanthropist", 2, 5, speed: 1, deploy: 2,
                "Gave away 0.01% of his fortune. Wrote off 100% of it.",
                "Future: a Contract. The enemy gets the gift now, you get the tax break on a later Day.",
                On(EffectTrigger.OnPlay, Buff(1, TargetType.EnemyCreatures, attack: false, health: true), Draw(2))),
            Creature("Fat Tony", 4, 5, speed: 1, deploy: 2,
                "Nothing personal. It's just business. Mostly the personal kind.",
                "Future: An Offer You Can't Refuse. Recruits a damaged enemy creature instead of hurting it.",
                On(EffectTrigger.OnPlay, Damage(2, TargetType.EnemyCreatures, AtRandom))),
            Creature("Charles Ponzi", 1, 3, speed: 2, deploy: 1,
                "Early investors were extremely satisfied.",
                "Future: pays draws from your next Days until it collapses, then takes them all back.",
                On(EffectTrigger.StartOfTurn, Draw(1)),
                On(EffectTrigger.OnDeath, Damage(3, TargetType.Player))),
            Spell("Margin Call", TargetType.AllCreatures,
                "Your position has been liquidated. Everyone's has. Have a nice day.",
                "Future: Capital cost, discard your hand; each creature takes damage equal to the cards discarded.",
                Damage(1, TargetType.AllCreatures, Each), Draw(2)),
            Spell("Hostile Takeover", TargetType.EnemyCreatures,
                "We're not buying your company. We're buying its funeral.",
                "Future: Capital cost, discard 2 cards. Placeholder cost: the enemy draws a card.",
                Damage(3, TargetType.EnemyCreatures), Draw(1, TargetType.Enemy)),
            Spell("Mass Layoffs", TargetType.FriendlyCreatures,
                "We were never a family. Please return your badge.",
                "Future: as an additional cost, destroy any number of your Workers; draw 1 per Worker next Day.",
                Damage(2, TargetType.FriendlyCreatures, Each), Draw(3)),
            Spell("Team Building Exercise", TargetType.FriendlyCreatures,
                "Attendance is voluntary. Attendance is tracked.",
                "Future: Coffeinated on each friendly creature, followed by Burnout.",
                Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, Each)),
            Spell("Dead Cat Bounce", TargetType.FriendlyCreatures,
                "It's recovering! It's recovering! It's... no.",
                "Future: a Temporary +3 Attack for 1 Day, then a Persistent -2 Health.",
                Buff(3, TargetType.FriendlyCreatures, attack: true, health: false), Buff(-2, TargetType.FriendlyCreatures, attack: false, health: true)),
            Spell("Business Expense", TargetType.Player,
                "The yacht was a business expense. So was the lawyer. So is the appeal.",
                "Draw 2 next Day, and pay the fine now: 2 damage to you. Future: Capital cost, discard a card instead.",
                Draw(2), Damage(2, TargetType.Player, Each))
                .NotInStarterDeck()
                .Picture("A CEO in a full three-piece suit, briefcase still in hand, being walked into a jail cell by a guard, while he hands the guard a receipt stamped BUSINESS EXPENSE."),
            Spell("Taxpayer Funds Venture Capital Profits", TargetType.Player,
                "Privatize the gains. Socialize the bill.",
                "The gains are yours alone: draw 2 with the next Day's hand. The bill is everyone's: 1 damage to every creature on the board, yours included.",
                Draw(2), Damage(1, TargetType.AllCreatures, Each)),
            Creature("Cleaning Robot", 2, 7, speed: 1, deploy: 1,
                "Cleans 30% of your floor. The other 70% is a Premium feature.",
                "Mechanics test: a sturdy pusher. Pushing (every creature can), no chain pushes, blocked pushes (\"needs Swap\") and the automatic move order.")
                .Picture("A smart-home robot vacuum in the style of a Google Nest (a soft fabric puck with four coloured light dots: blue, red, yellow, green), its single camera eye glowing, sucking up a family's photos, letters and diary from the living-room floor while a beam uploads a floor plan of the house to a cloud. A pop-up on it reads \"Free cleaning. You're the product.\""),
            Spell("Tax-Exempt Corruption", TargetType.Player,
                "Thou shalt not pay taxes.",
                "Future: Capital cost (discard 2 cards). Placeholder cost: the enemy draws a card. Plays in Prosperity Gospel.",
                Draw(3), Draw(1, TargetType.Enemy))
                .Picture("A megachurch whose cross is a giant gold dollar sign, a private jet parked by the door and a big red TAX EXEMPT stamp across the sky."),
            Spell("Solidarity Towards The Small Businessman", TargetType.Player,
                "He's just a little guy. With seven yachts. Every coin helps.",
                "Draw 2, paid for by your workers: 1 damage to each friendly creature. Future: Capital cost.",
                Draw(2), Damage(1, TargetType.FriendlyCreatures, Each))
                .NotInStarterDeck()
                .Picture("A fan-subscription website called ONLYFUNDS: the board-game tycoon (top hat, white moustache) poses in a pink bathrobe on a live webcam, a SUBSCRIBE button and a pile of tips beside him, while tired workers in hi-vis vests and scrubs send him their coins."),
            Spell("Inheritance", TargetType.FriendlyCreatures,
                "Self-made. Grandma did the making.",
                "Old money: target friendly creature gets +2 Attack and +2 Health for good, for doing nothing. Future: when a friendly creature dies, another inherits its stats.",
                Buff(2, TargetType.FriendlyCreatures, attack: true, health: true))
                .NotInStarterDeck()
                .Picture("Grandma at peace in an open coffin, hands folded on lilies, while her grandson in his best suit grins over her, fanning out the banknotes from her will."),
            Spell("NDA", TargetType.EnemyCreatures,
                "You saw nothing. You'll say nothing. Initial here, here and here.",
                "Non-Disclosure Agreement. Discard 1 (Corporate pays with capital), then Blackout for 2 Days on target enemy creature: its ability and its effects (arrival, Dawn and Dusk, retaliation, Last Will) are switched off. Silences the Shadow Ops. tricks and the Myth sacrifices alike.",
                Status(StatusEffectType.Blackout, 2, TargetType.EnemyCreatures))
                .Discarding(1, chosen: true)
                .NotInStarterDeck()
                .Picture("A smiling corporate lawyer holding out a gold pen to a nervous employee whose mouth is closed with a big brass zipper, a towering stack of contracts between them stamped CONFIDENTIAL."),
            Spell("Inflation", TargetType.EnemyCreatures,
                "Same price. Less creature.",
                "Everyone's money is worth less, theirs more so: 2 damage to every enemy creature and 1 to each of yours.",
                Damage(2, TargetType.EnemyCreatures, Each), Damage(1, TargetType.FriendlyCreatures, Each))
                .Picture("A chocolate bar in its old-size wrapper, the bar inside visibly half as big, and a supermarket price tag re-stickered three times, each price higher."),
            // The fake free market (the user, 2026-10-15): two cards on one idea, wealth flowing to the top, from two sides.
            // Laissez-Faire lets it happen to everyone at once; The Invisible Hand does it quietly, on its own side, every Day
            Spell("Laissez-Faire", TargetType.AllCreatures,
                "Let the market decide. The market decided: whoever already had the most.",
                "Hands off: the strongest creature on the board, on either side, takes 1 Attack from every other creature. Nobody steers it, so play it when the biggest is yours. The Invisible Hand does the same, quietly, on your own side each Dusk.",
                Act(ActionType.Consolidate, 1, TargetType.AllCreatures, StrongestOne))
                .Picture("A boxing referee lounging in a deck chair in the corner of the ring, reading the business pages, whistle unused, while a giant in a top hat sits on a heap of flattened small boxers and pockets their belts."),
            Creature("The Invisible Hand", 1, 4, speed: 1, deploy: 1,
                "Nobody has ever seen it. Everybody has felt it in their pocket.",
                "Each Dusk your strongest creature takes 1 Attack from each of your other creatures, this one too: the market sorts itself out. Undercover: you never see it coming. Laissez-Faire does the same once, to the whole board.",
                On(EffectTrigger.EndOfTurn, Act(ActionType.Consolidate, 1, TargetType.FriendlyCreatures, StrongestOne)))
                .With(CreatureAbility.Undercover)
                .Picture("An empty white glove floating over a busy market stall, nobody inside it, quietly lifting coins out of the shoppers' pockets and dropping them into a fat banker's top hat."),
            Creature("C-Suite", 4, 3, speed: 1, deploy: 2,
                "Record profits this quarter. The savings will trickle down to you shortly.",
                "Trickle Down: when its attack is more than a creature's Health, the rest goes on to that creature's player. The top of Corporate's hierarchy; it costs capital: discard a card.")
                .With(CreatureAbility.TrickleDown)
                .Discarding(1)
                .Picture("A champagne tower of glasses: five executives in suits at the top glass, toasting, the champagne overflowing past them down the tiers, until a line of workers at the bottom catch the last drips in paper cups."),
        };
        #endregion

        #region Statecraft
        // Pays with bureaucracy: its creatures take longer to deploy than similar ones, and its spells tie up its own side.
        // Can't hide anything. Engine: armor, area damage, slow but sturdy bodies, disables.
        private static List<CardDef> StatecraftCards() => new List<CardDef> {
            Spell("Supply Depot", TargetType.Player,
                "Requisition form 27-B, in triplicate. Approved by Thursday.",
                "Statecraft's logistics: send for a creature from your deck; it joins your next hand.",
                SendForCard(SendForFilter.Creature))
                .Picture("A huge military warehouse with endless shelves of crates, a forklift carrying a soldier in a crate labelled REINFORCEMENTS, a clerk stamping a form at the front desk."),
            Creature("Golden Dome", 0, 6, speed: 2, deploy: 1,
                "Protects everyone. Some of everyone more than others.",
                "Mechanics test: Bodyguard. It can't attack; drag it onto another friendly creature and that one is Untouchable this Day (no damage at all), then it rests a Day (Cooldown).")
                .With(CreatureAbility.Bodyguard).NotInStarterDeck()
                .Picture("A shimmering golden dome of light over one fat senator on a deckchair, while missiles rain on the rest of the city outside it."),
            Spell("Presidential Pardon", TargetType.FriendlyCreatures,
                "Whatever it did, it didn't.",
                "Mechanics test: the protection as a spell. Untouchable this Day: target creature takes no damage at all.",
                Status(StatusEffectType.Untouchable, 1, TargetType.FriendlyCreatures))
                .NotInStarterDeck()
                .Picture("A smiling president signing a giant pardon with a huge flourish for a creature in an orange jumpsuit still holding a smoking gun."),
            Creature("Congruent Use of Force", 2, 4, speed: 1, deploy: 1,
                "Proportionate. To something, surely.",
                "Mechanics test: Spray and Pray. Its attacks also hit the creatures beside its target, a cone of three slots.")
                .With(CreatureAbility.SprayAndPray).NotInStarterDeck()
                .Picture("A soldier firing a huge gatling gun at a single mosquito, the whole street behind it riddled with holes."),
            Spell("Weapons of Mass Distraction", TargetType.EnemyCreatures,
                "Look over there. No, over there.",
                "Mechanics test: the swap as a spell. Target creature trades places with the one to its right (or to its left at the end of the row).",
                SwapPositions(TargetType.EnemyCreatures))
                .NotInStarterDeck()
                .Picture("A giant public screen holds a crowd: on one half a blocky-game YouTuber screams at his pickaxe, on the other a grinning giveaway YouTuber in a hoodie waves a giant FREE check. Behind it, soldiers quietly shuffle the enemy's tents around."),
            Creature("King Emeritus", 1, 7, speed: 1, deploy: 1,
                "Abdicated in disgrace. Kept the yacht, the pension and the Swiss account.",
                "Future: Diplomatic Immunity. Can't be targeted by enemy spells.",
                On(EffectTrigger.StartOfTurn, Heal(1, TargetType.Player))),
            Creature("Spite-Elected President", 3, 5, speed: 2, deploy: 1,
                "Voted in to make everyone else miserable. Delivered on day one.",
                "Spite: when an effect lowers its stats, friendly or enemy, it gains +2 Attack. Future: a Law (Location) for 3 Days that hurts both sides each Dawn.",
                On(EffectTrigger.OnPlay, Damage(1, TargetType.AllCreatures)))
                .With(CreatureAbility.Spite),
            Creature("Police Officer Productivity Target", 2, 4, speed: 2, deploy: 0,
                "Ten arrests a week. Guilt is optional.",
                "Future: double damage against creatures that moved this Day.",
                On(EffectTrigger.StartOfTurn, Damage(1, TargetType.EnemyCreatures, AtRandom))),
            Creature("Internal Revenue Service", 1, 6, speed: 1, deploy: 0,
                "We've noticed some irregularities. Specifically, you.",
                "Future: taxes enemy card plays instead of plain damage (Red Tape on the economy).",
                On(EffectTrigger.StartOfTurn, Damage(1, TargetType.Enemy))),
            Creature("Mercenaries", 4, 4, speed: 3, deploy: 0,
                "Loyal to whoever pays next.",
                "Future: Recruitment bait. When it dies, the enemy may Recruit it instead.",
                On(EffectTrigger.OnDeath, Draw(1, TargetType.Enemy))),
            Creature("Man o' War", 4, 8, speed: 1, deploy: 1,
                "Gunboat diplomacy, minus the diplomacy.",
                "Future: multi-slot (takes 2 slots) and Heavy.",
                On(EffectTrigger.OnPlay, Damage(1, TargetType.EnemyCreatures))),
            Creature("Ministry for Equality", 1, 6, speed: 1, deploy: 1,
                "We love everybody. Please stop resisting our love.",
                "Future: Red Tape every Dawn: +1 Deployment Time on a random enemy creature.",
                On(EffectTrigger.StartOfTurn, Stun(1, TargetType.EnemyCreatures, AtRandom))),
            Creature("Supreme Court", 0, 7, speed: 1, deploy: 1,
                "Appointed for life. Accountable for nothing.",
                "Return to Sender: enemy statuses bounce back to whoever sent them. Future: Fortifying. Adjacent friendly creatures can't be moved by enemy effects.",
                On(EffectTrigger.OnPlay, Armor(2, TargetType.FriendlyCreatures)))
                .With(CreatureAbility.ReturnToSender),
            Creature("Protocol Enforcers", 2, 5, speed: 1, deploy: 0,
                "Please stand behind the line. Which line? This line. Now that line.",
                "Future: Keep Calm on friendly creatures each time it's hit.",
                On(EffectTrigger.OnDamage, Armor(1, TargetType.FriendlyCreatures))),
            Creature("Demagogue", 2, 4, speed: 2, deploy: 0,
                "Says what you think. Thinks what he's told.",
                "Future: Loaded Language. The buff only lasts while it stays on the battlefield.",
                On(EffectTrigger.OnPlay, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false))),
            Spell("Red Tape", TargetType.EnemyCreatures,
                "Your request is being processed. Please hold. Forever.",
                "Adds 1 Day to an enemy creature's Deployment Time: it sits out the rest of this Day and all of the next, its planned action is cancelled, and its abilities are off until it's ready again. Its entry effects don't happen twice.",
                DelayDeployment(1, TargetType.EnemyCreatures))
                .Picture("A soldier at a deployment checkpoint wrapped head to toe in red tape, while a bored clerk behind the window stamps \"FORM 27-B MISSING\" and a take-a-number display reads \"Now serving: 0001. Yours: 9847\"."),
            Spell("Preemptive Strike", TargetType.EnemyCreatures,
                "They were going to attack us. Probably. Eventually. Somebody said so.",
                "Future: an extra effect against creatures that planned an attack this Day.",
                Damage(4, TargetType.EnemyCreatures))
                .Picture("A general in a war room proudly pointing at a map where a missile hits a quiet village labelled \"Possible Future Threat\", a MISSION ACCOMPLISHED banner behind him and an intelligence report on the table: \"Source: trust me bro\"."),
            Spell("Martial Law", TargetType.FriendlyCreatures,
                "For your safety, nobody moves. Ever.",
                "Future: a Law (Location) for 3 Days. The stun stands in for the bureaucracy drawback.",
                Armor(3, TargetType.FriendlyCreatures, Each), Stun(1, TargetType.FriendlyCreatures, Each)),
            Spell("Kakistocracy", TargetType.AllCreatures,
                "Elect the least competent. What could go wrong?",
                "Future: the Kakistocracy Now! Law. Creatures entering with 3+ Attack or Health get -1 of each for 3 Days.",
                Buff(-1, TargetType.AllCreatures, attack: true, health: false, Each)),
            Spell("Mutually Assured Destruction", TargetType.AllCreatures,
                "Everybody loses. Fairly, for once.",
                "Future: a countdown everyone can see coming, like a Prophecy.",
                Damage(3, TargetType.AllCreatures, Each), Damage(3, TargetType.Player, Each), Damage(3, TargetType.Enemy, Each)),
            Spell("Panem Et Circenses", TargetType.Player,
                "Free bread, free games, free of questions.",
                "Future: a Law that also adds 1 Deployment Time to your next creature (the paperwork).",
                Heal(3, TargetType.Player, Each), Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, Each)),
            Creature("Diplomat", 3, 6, speed: 1, deploy: 1,
                "Third-generation ambassador. Qualifications: his surname.",
                "Mechanics test: Deployment Time 2 counts down at Dusk, never at the Dusk of the Day it arrived; its entry effect waits until it's ready. Armor fades by 1 each Dusk on every creature.",
                On(EffectTrigger.OnPlay, Armor(2, TargetType.FriendlyCreatures, Each)))
                .Picture("A young heir lounging in a leather chair at a summit table, feet up, a gold name plate reading \"AMBASSADOR (BY BIRTH)\". Behind him, a wall of portraits of his father and grandfather in the very same chair."),
            Spell("Raised Security Level", TargetType.FriendlyCreatures,
                "The threat level is now Orange. Nobody knows what Orange means.",
                "Mechanics test: Armor is capped at twice max Health, fades by 1 each Dusk and is lost when the creature leaves play. Aimed at your own creature, it follows it if it moves first (Homing).",
                Armor(6, TargetType.FriendlyCreatures))
                .Picture("A bureaucrat solemnly flipping a color-coded threat sign from yellow to orange, while one guard in comically oversized armor blocks a door."),
            Spell("Lost Landmine", TargetType.EnemyCreatures,
                "The war ended decades ago. Nobody told the mine.",
                "Mechanics test: a hidden Landmine (3 damage) on a slot. The first creature to enter it, summoned or moved in, sets it off; it resolves before anything else (environment action), then it's gone. Only its controller sees it.",
                PlaceSlotEffect(SlotEffectType.Landmine, 3, 0, TargetType.EnemyCreatures))
                .Picture("A rusty landmine half-buried in a children's sandbox, next to a faded government sign: \"AREA CLEARED. PROBABLY. - Ministry of Defence, 1998\"."),
            Spell("Ceasefire", TargetType.FriendlyCreatures,
                "Both sides agree to stop shooting. Starting right after this last volley.",
                "Mechanics test: Temporal Shield for 3 Days on a slot. The creature there takes no combat damage, but spells and abilities still hurt it. It stays with the slot, not the creature (Static).",
                PlaceSlotEffect(SlotEffectType.TemporalShield, 0, 3, TargetType.FriendlyCreatures))
                .Picture("Two generals shaking hands for the cameras in front of a CEASEFIRE banner, each hiding a grenade behind his back."),
            Spell("Violence Monopoly", TargetType.Player,
                "Only one of us is allowed to hit people. Guess which.",
                "The state's monopoly on violence: a Monitored Watchdog nullifies the next 2 enemy attacks before they land.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Attack, side = WatchdogSide.Enemy, effect = WatchdogEffect.Nullify, charges = 2 }, TargetType.Enemy))
                .NotInStarterDeck()
                .Picture("The board-game tycoon (top hat, white moustache, tailcoat) as the police: a badge on his chest, a baton raised in his white glove, glaring straight at you from beside the GO TO JAIL corner."),
        };
        #endregion

        #region Zeitgeist
        // Pays with self-harm: damage to its own creatures and to you. Fast (Deployment Time 0-1), buffs that burn out.
        // Can't use Intelligence. Engine: cheap bodies that spike and fizzle, chaos that hits everyone.
        private static List<CardDef> ZeitgeistCards() => new List<CardDef> {
            Spell("Multipass", TargetType.Player,
                "Leeloo Dallas. Multipass.",
                "Zeitgeist's logistics, a film everyone quotes: send for any card from your deck; it joins your next hand. Was Multi Passport, the Shadow Ops. creature (its Ambush went to Femme Fatale).",
                SendForCard(SendForFilter.Any))
                .Picture("The Multipass shown to the camera, as in the film: on the left the cab driver with buzzed fair hair and stubble, an orange shirt under a dark vest; on the right the young woman with the bright orange bob and fringe, in a white top with orange suspenders, holding the Multipass up beside her face; a dim, crowded hall behind them."),
            Creature("Drug Smuggler", 1, 2, speed: 2, deploy: 0,
                "Just a backpacker. With a very full backpack.",
                "Zeitgeist's logistics: when it arrives, send for any card among the top 3 of your deck; it joins your next hand.",
                On(EffectTrigger.OnPlay, SendForCard(SendForFilter.Any, top: 3)))
                .Picture("A sunburnt backpacker with dreadlocks grinning at an airport security gate, his enormous backpack bulging with cards poking out of every zip."),
            Creature("Not on My Watch", 1, 5, speed: 1, deploy: 1,
                "Feelings are valid. Statuses are not.",
                "Mechanics test: Safe Space. Each Dawn, it and the friendly creatures beside it gain Bless (a ward that stops the next status).")
                .With(CreatureAbility.SafeSpace).NotInStarterDeck()
                .Picture("A fierce grandma in a crossing-guard vest holding up a stop sign, nervous creatures huddled behind her in a blanket fort."),
            Spell("One for the Road", TargetType.FriendlyCreatures,
                "Last one. Then one more.",
                "Mechanics test: an ability as a spell. Target creature gains Double Tap (it attacks twice), replacing its own until its card is reshuffled.",
                GrantAbility(CreatureAbility.DoubleTap, TargetType.FriendlyCreatures))
                .NotInStarterDeck()
                .Picture("A pub regular raising two pints at once under a CLOSING TIME sign, the barman already putting chairs on tables."),
            Creature("Chain Smoker", 4, 6, speed: 2, deploy: 1,
                "Quitting tomorrow. Has said so since 1998.",
                "Recoil: +2 damage on its attacks, and it takes 1 damage after each one. A mood that burns it out.")
                .With(CreatureAbility.Recoil)
                .Picture("A wiry man smoking a length of steel chain like a cigarette, the far end swinging like a flail, a cloud of smoke around his head."),
            Creature("Karen", 4, 5, speed: 1, deploy: 0,
                "Would like to speak to your manager. And theirs. And God's.",
                "Future: Impatient. Escalates every Day nobody gives it an intent.",
                On(EffectTrigger.OnDamage, Buff(1, TargetType.Self, attack: true, health: false))),
            Creature("Boomer", 4, 6, speed: 1, deploy: 0,
                "Bought a house at 22. Recommends you stop buying coffee.",
                "Future: OLD MAN YELLS AT CLOUD. Hits a random creature that moved this Day.",
                On(EffectTrigger.EndOfTurn, Damage(2, TargetType.AllCreatures, AtRandom))),
            Creature("Hipster", 4, 4, speed: 2, deploy: 0,
                "Liked this battlefield before it got so crowded.",
                "Too Mainstream: immune to effects that affect more than one creature. Future: Hipster status. Hurts only creatures that share a name with another creature.",
                On(EffectTrigger.OnPlay, Damage(1, TargetType.AllCreatures)))
                .With(CreatureAbility.TooMainstream),
            Creature("Underdog", 1, 1, speed: 3, deploy: 0,
                "Nobody believed in him. Correctly. Still, very inspiring.",
                "Future: Underdog. A bonus while you have fewer creatures or less Health than the enemy.",
                On(EffectTrigger.OnDeath, Buff(2, TargetType.FriendlyCreatures, attack: true, health: false))),
            Creature("Hikikomori", 1, 7, speed: 1, deploy: 0,
                "Hasn't left the room since 2019. Neither will you.",
                "Future: Slacking Off. Can't be given an intent, but can't be targeted either.",
                On(EffectTrigger.OnPlay, Stun(2, TargetType.Self))),
            Creature("Reality TV Star", 5, 4, speed: 2, deploy: 0,
                "Famous for being famous. Unbearable for free.",
                "Future: Plot Armor. Survives a lethal hit once (OnAboutToDie).",
                On(EffectTrigger.OnPlay, Buff(2, TargetType.Self, attack: true, health: true), Damage(1, TargetType.FriendlyCreatures))),
            Creature("Scam Artist", 2, 2, speed: 3, deploy: 0,
                "Sells a course on how to sell courses.",
                "Future: steals a Temporary buff from an enemy creature instead.",
                On(EffectTrigger.OnPlay, Buff(-2, TargetType.EnemyCreatures, attack: true, health: false, AtRandom))),
            Creature("Football Hooligans", 5, 3, speed: 2, deploy: 0,
                "Lost the match. Won the street.",
                "Future: a riot that spreads to adjacent creatures each Dusk.",
                On(EffectTrigger.OnDeath, Damage(1, TargetType.AllCreatures))),
            Creature("Antihero", 5, 4, speed: 2, deploy: 0,
                "Not a good guy. Just a better-written bad one.",
                "Future: picks its own target among the creatures that hurt you last Day.",
                On(EffectTrigger.OnPlay, Damage(2, TargetType.AllCreatures, AtRandom))),
            Creature("Bodybuilder", 5, 3, speed: 3, deploy: 0,
                "Natural. Definitely natural. Don't check.",
                "Future: Coffeinated, then Burnout. Never skips leg day, skips every rest day.",
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self))),
            Creature("Plot Armor", 2, 2, speed: 2, deploy: 0,
                "The hero can't die. It's only the second act.",
                "Mechanics test: once per combat, when it would die (OnAboutToDie, resolving at once), it heals 1 and survives. The use is gone for the rest of the combat, even after the card is reshuffled.",
                OncePerCombat(On(EffectTrigger.OnAboutToDie, Heal(1, TargetType.Self))))
                .Picture("A close-up of a chest: two hands hold an open jacket wide, Superman style, showing a steel breastplate stamped PLOT."),
            Spell("YOLO", TargetType.FriendlyCreatures,
                "Look Mom, no hands... Look Mom, no teef!",
                "+4 Attack for this Day only, paid with 2 of your Health. Future: the creature is Banished at Dusk.",
                Buff(4, TargetType.FriendlyCreatures, attack: true, health: false, days: 1), Damage(2, TargetType.Player, Each))
                .Picture("A shirtless guy mid-backflip off a garage roof toward a tiny inflatable pool, filming himself with a selfie stick, while his friends below are already dialing for an ambulance."),
            Spell("This Is Fine", TargetType.FriendlyCreatures,
                "Everything is on fire. The coffee is still warm, though.",
                "Future: a Trend (Location). Each Dusk, every creature takes 1 damage and yours shrug it off.",
                Buff(2, TargetType.FriendlyCreatures, attack: false, health: true, Each), Damage(3, TargetType.AllCreatures, Each))
                .Picture("The meme dog in a little bowler hat at a table with his coffee, the room on fire around him, saying THIS IS FINE."),
            Spell("Strike", TargetType.EnemyCreatures,
                "Nobody works. Not them. Especially not us.",
                "Future: a Strike. Stops Each Dawn triggers (engines) on both sides for 2 Days.",
                Stun(1, TargetType.EnemyCreatures, Each), Stun(1, TargetType.FriendlyCreatures, Each))
                .Picture("A corrupt head of the works council in a lobster bib, a WORKS COUNCIL badge on his lapel, feasting on a lobster with champagne at the company's boardroom table; the beaming CEO pats him on the shoulder. Through the window behind them, the striking workers stand in the rain with their placards, each handed a single soggy biscuit."),
            Spell("Russian Roulette", TargetType.AllCreatures,
                "Five empty chambers. Your odds are great. Probably.",
                "Future: you pick the first slot; each Dawn it moves to the next one until it fires.",
                Damage(5, TargetType.AllCreatures, AtRandom))
                .Picture("A Russian woman in a fur ushanka spinning a roulette wheel of six slices, one of them a skull, onion domes in the snow behind her."),
            Spell("Toxic Positivity", TargetType.FriendlyCreatures,
                "Good vibes only. Bad vibes get one damage.",
                "Future: a Temporary buff that turns into Burnout when it wears off.",
                Buff(2, TargetType.FriendlyCreatures, attack: true, health: false, Each), Damage(1, TargetType.FriendlyCreatures, Each)),
            Spell("Mental Health Day", TargetType.FriendlyCreatures,
                "Unpaid, unscheduled, unbelievably necessary.",
                "Future: Cleanse every mood from the creature instead of the stun.",
                Buff(3, TargetType.FriendlyCreatures, attack: false, health: true), Stun(1, TargetType.FriendlyCreatures))
                .Picture("A smiling politician cuts the ribbon on a MENTAL HEALTH DAY banner in front of a boarded-up mental health clinic (CLOSED: BUDGET CUTS). The only help on offer: a reiki-and-ayurveda lady in crystals and a shawl waving burning sage over an exhausted office worker, a price list beside her (AURA CLEANSE 90 EUROS)."),
            Creature("Delivery Cyclist", 1, 3, speed: 3, deploy: 0,
                "Your food is 4 minutes away. It has been 4 minutes away for 40 minutes.",
                "Mechanics test: Leapfrog jumps over creatures to an empty slot; if its landing slot is taken, it stops at the closest empty slot on the way.")
                .With(MovementKeyword.Leapfrog)
                .Picture("A rider with a huge square thermal backpack, mid-hop over a gridlocked row of cars, phone strapped to the handlebars showing a 1-star rating."),
            // Trends (GDD 4.2) are Zeitgeist's signature: the mood of the times sets the rules for everyone
            Spell("Zeitgeist", TargetType.Player,
                "Everyone agrees. Nobody remembers deciding.",
                "Trend for 3 Days: each Day, whoever has fewer creatures draws 1 more card. The mood of the times backs the underdog (Zeitgeist's comebacks).",
                SetTrend(Trend.Zeitgeist, 3))
                .Picture("A huge friendly ghost made of hashtags and likes floating over a crowd whose faces are lit blue by their phones."),
            Spell("Rush Hour", TargetType.Player,
                "Fastest way home: don't.",
                "Trend for 3 Days: the slowest act first, wherever Speed decides the order (like a room where the order is flipped).",
                SetTrend(Trend.RushHour, 3))
                .NotInStarterDeck()
                .Picture("Gridlocked traffic at dusk, every car honking, while a snail with a briefcase glides past in the empty bus lane."),
            Spell("No Glove, No Love", TargetType.FriendlyCreatures,
                "Protect yourself. Everyone else is.",
                "Protection: target friendly creature gets Blessed (the next status that would land is negated) and 2 Armor.",
                Status(StatusEffectType.Blessed, 0, TargetType.FriendlyCreatures, potency: 1), Armor(2, TargetType.FriendlyCreatures))
                .NotInStarterDeck()
                .Picture("A woman wagging her index finger: no, no. In her other hand, a little foil packet held up: this first."),
            Spell("We Are Not The Same", TargetType.FriendlyCreatures,
                "I copy whoever's in front of me. You copy whoever's in front of you. We are not the same.",
                "Your creature copies the Attack, Health and Speed of the enemy creature facing it, while it stays in play. Shrinks a giant into your nobody, or your nobody into a giant.",
                CopyStats(TargetType.FriendlyCreatures))
                .NotInStarterDeck()
                .Picture("The meme: two identical masked heroes in red suits with web lines, pointing at each other in a dim warehouse."),
            Spell("Generational Trauma", TargetType.EnemyCreatures,
                "Your grandfather's, your father's, and now yours. A family heirloom.",
                "Mechanics test: Health Loss is permanent and stays on the card until it's reshuffled. It only affects creatures, so it fizzles on an empty slot. Zeitgeist since 2026-10-15 (the user): the generations are the mood of the times.",
                Buff(-4, TargetType.EnemyCreatures, attack: false, health: true))
                .Picture("The swimming-pool meme: the Silent Generation stands in the pool holding a smiling Boomer high above the water, while on the bottom Gen X and the generations after them sit as skeletons, still waiting."),
        };
        #endregion

        #region Factions without a deck yet
        // Digital Society: a swarm of cheap bodies, effects that spread, many creatures piling on one target
        private static List<CardDef> DigitalSocietyCards() => new List<CardDef> {
            Spell("Doomscrolling", TargetType.Player,
                "Just one more. Just one more. Just one more.",
                "Digital Society's scouting: send for any card among the top 4 of your deck; it joins your next hand. Costs Doom 3: your weakest creature not yet Doomed is Doomed first (the feed eats someone).",
                SendForCard(SendForFilter.Any, top: 4))
                .Dooming(3)
                .Picture("The Living the Dream wojak, MS Paint style: bald, shirtless, baggy-eyed and expressionless at 3AM, holding up a phone (we see its black back) where the meme had a computer, its glow on his face, cards drifting up out of the feed."),
            Creature("Crypto \"Entrepreneur\"", 3, 3, speed: 2, deploy: 0,
                "Funds are safu.",
                "Mechanics test: Chain Mail. Its attacks jump on to the next 2 creatures in line, half the damage each jump.")
                .With(CreatureAbility.ChainMail)
                .Picture("A guy in sunglasses and a hoodie on top of a human pyramid of recruits, a glowing coin passed down from hand to hand."),
            Creature("Digital Nomad", 3, 2, speed: 2, deploy: 0,
                "Works from Bali. Works from Lisbon. Works, allegedly.",
                "Mechanics test: Leapfrog on a cheap, fast creature.")
                .With(MovementKeyword.Leapfrog)
                .Picture("A Tuareg nomad in a white turban and face veil, sitting cross-legged on a sand dune, typing on a laptop on his knees."),
            Creature("Keyboard Warrior", 2, 3, speed: 2, deploy: 0,
                "Has never lost an argument. Has never left the house.",
                "Mechanics test: Counterattack answers every attack that lands, even one Armor absorbs or a killing blow, for its Attack, and still lands when it dies; spells and abilities aren't answered. Exhausted turns it off. No U: enemy effects that would lower its stats bounce back.",
                Counterattack())
                .With(CreatureAbility.NoU)
                .Picture("A heavyset warrior in a stained hoodie and a cape, holding a mechanical keyboard over his head like a greatsword, a gaming headset for a helmet and energy drinks hanging from his belt."),
            Spell("Dogpile", TargetType.EnemyCreatures,
                "Nobody read the post. Everybody has an opinion.",
                "Mechanics test: several hits at once: Exposed adds +1 to each, retaliation fires once per hit, Armor takes hits first.",
                Damage(2, TargetType.EnemyCreatures, TargetModifier.WithAdjacent))
                .Picture("A huge, happy pile of dogs (corgis, labradors, a confused pug on top) burying one person, of whom only a waving hand sticks out."),
            Spell("Rage Bait", TargetType.EnemyCreatures,
                "You won't BELIEVE what they said about you.",
                "Mechanics test: Status Timing. Burn does nothing at the Dusk of the Day it's applied, then ticks for 2 Days.",
                Status(StatusEffectType.Burned, 2, TargetType.EnemyCreatures, potency: 2))
                .Picture("A phone screen with a glowing red notification, reflected in the furious eyes of the person holding it."),
            Spell("Its A Trap", TargetType.EnemyCreatures,
                "FREE PHONE! Just click here. And here. And give us your mother's maiden name.",
                "Mechanics test: a hidden Barbed Snare (2 damage) on a slot. The first creature to leave it, on its own, pushed or swapped, takes the damage. Dying or being replaced isn't leaving.",
                PlaceSlotEffect(SlotEffectType.BarbedSnare, 2, 0, TargetType.EnemyCreatures))
                .Picture("The free-boat sting: a bald, stubbled Homer Simpson strolls into a police station waving a letter, \"CONGRATULATIONS! YOU'VE WON A FREE BOAT!\", while the grinning police chief behind the counter slides him a pile of unpaid fines instead. A \"FREE BOAT PICKUP\" banner hangs over the open door of a holding cell."),
            Creature("NPC", 2, 3, speed: 2, deploy: 0,
                "Has opinions. All of them yours.",
                "Mechanics test: Copypasta. When ready, and each Dawn after that, it copies the ability of the creature opposite it (and loses Copypasta).")
                .With(CreatureAbility.Copypasta)
                .Picture("A crowd of identical grey, featureless NPC Wojak faces, every speech bubble repeating the same sentence, one of them proudly holding a \"Think For Yourself\" sign."),
            Creature("Chronically Online", 3, 3, speed: 2, deploy: 0,
                "Read one post. Now has a manifesto.",
                "Mechanics test: Extremist. Stat changes on it are doubled, buffs and debuffs alike.")
                .With(CreatureAbility.Extremist)
                .Picture("A pale figure in a dark room lit only by three monitors, a cork board of red strings and printed memes behind them, an energy-drink pyramid on the desk."),
            Creature("Catfish", 3, 2, speed: 3, deploy: 0,
                "Six foot, a doctor, loves dogs. Is a fish.",
                "Undercover: its plans are Concealed Actions, and it stays ??? to the other side until revealed.")
                .With(CreatureAbility.Undercover)
                .Picture("A cat and a goldfish in love: the cat presses its face to the fishbowl, the fish kisses the glass back, hearts all around."),
            Creature("Conspiracy Theorist", 2, 4, speed: 2, deploy: 0,
                "Did his own research. For eleven minutes.",
                "Unbothered: enemy effects can't lower its stats (the tinfoil works). Future: Doxxing, reveals.")
                .With(CreatureAbility.Unbothered)
                .Picture("A man in a tinfoil hat pointing at a cork board of photos joined by red string, the biggest photo a pigeon labelled DRONE."),
            Spell("Clickbait", TargetType.EnemyCreatures,
                "Number 7 will shock you. Number 8 is an ad.",
                "Future: Clickbait taunts (enemy creatures must attack it).",
                Damage(2, TargetType.EnemyCreatures, Each))
                .Picture("A video thumbnail: a man with a hugely shocked face, a big red arrow and a red circle around absolutely nothing."),
            Spell("Cancelled", TargetType.EnemyCreatures,
                "One tweet from 2011. Career over by lunch.",
                "Exhausted, and Heat Signature 2: random and redirected attacks find it first. Future: Ostracised.",
                Stun(1, TargetType.EnemyCreatures), Status(StatusEffectType.HeatSignature, 1, TargetType.EnemyCreatures, potency: 2))
                .Picture("A man at a podium facing a crowd that holds torches, pitchforks and phones up to film him."),
            Creature("Meta Group Chat", 2, 4, speed: 2, deploy: 0,
                "If you aren't in it, you're not on the jury, you're on trial.",
                "Each Dawn the in-group gets louder (+1 Attack on a random friendly creature) and someone outside it gets a target on their back (Heat Signature 1 on a random enemy creature). Future: Ostracised.",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, AtRandom),
                    Status(StatusEffectType.HeatSignature, 1, TargetType.EnemyCreatures, potency: 1, modifier: AtRandom)))
                .Picture("A messenger group chat in the familiar green-and-beige look: THE JURY (47 members), gavels and skulls with blue read ticks under a blurry photo of one guy, while the guy himself sweats alone outside the phone."),
            Spell("Social Panopticon", TargetType.EnemyCreatures,
                "Nobody is watching you. Everybody is.",
                "Every enemy creature is Revealed for 2 Days (its plans can be seen, Watchdogs can match them) and gets Heat Signature 1. Future: a Trend where nothing can be Concealed.",
                Status(StatusEffectType.Revealed, 2, TargetType.EnemyCreatures, modifier: Each),
                Status(StatusEffectType.HeatSignature, 1, TargetType.EnemyCreatures, potency: 1, modifier: Each))
                .Picture("A real panopticon seen from inside: a ring of barred cells on two floors around a central watchtower with a single recording eye, every inmate lit by their own phone."),
        };

        // Shadow Ops.: fragile agents, hidden plans, traps
        private static List<CardDef> ShadowOpsCards() => new List<CardDef> {
            Creature("Cleaner", 1, 3, speed: 3, deploy: 0,
                "Two shots. One invoice.",
                "Mechanics test: Double Tap. It attacks twice: a second hit right after the first.")
                .With(CreatureAbility.DoubleTap)
                .Picture("A calm man in a cleaning company jumpsuit, a mop in one hand and a silenced pistol in the other, two neat holes in a target behind him."),
            Creature("Political Decoy", 0, 6, speed: 1, deploy: 0,
                "The real one is in a bunker. Or is he?",
                "Mechanics test: Swap trades places with a friendly creature at any distance, over the creatures in between, even one that can't act.")
                .With(MovementKeyword.Swap)
                .Picture("Two identical politicians in matching suits: one waves from a balcony, the other ducks behind the curtain."),
            Spell("Burn Notice", TargetType.EnemyCreatures,
                "You're no longer one of us. You never were.",
                "Mechanics test: Scorched Earth on a slot. The creature there can't trigger its effects (entry, retaliation, Dawn and Dusk, death) or use its ability while it stands on it. It can still move and attack.",
                PlaceSlotEffect(SlotEffectType.ScorchedEarth, 0, 0, TargetType.EnemyCreatures))
                .Picture("The Banksy auction stunt: a spy's portrait, painted in the style of Girl with Balloon, has just sold at auction, and the canvas is sliding down through a shredder hidden in its gilded frame, the strips stamped BURN NOTICE. In the audience, the spy himself watches his face being shredded."),
            Spell("Counter-Intelligence", TargetType.Player,
                "We knew you knew we knew.",
                "Mechanics test: a Briefing Watchdog (GDD 3.11). From the Day after it's created, before anything resolves, it redirects the strongest enemy attack to a random creature (the one with the most Heat first). 2 charges, one per attack it redirects.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Attack, side = WatchdogSide.Enemy, effect = WatchdogEffect.Redirect,
                    charges = 2, timing = WatchdogTiming.Briefing, pick = BriefingPick.Strongest }, TargetType.Enemy))
                .Picture("A remake of the first Counter-Strike box cover: a counter-terrorist and a terrorist in a tense standoff in a dusty corridor, but both are accountants in thick glasses, aiming pocket calculators at each other like rifles, ledgers fluttering around them."),
            Creature("Mole", 2, 3, speed: 2, deploy: 0,
                "Twenty years with the company. Twenty-one with the other one.",
                "Mechanics test: Underground (both ways). When it's attacked, the attacker's buffs and debuffs are ignored; when it attacks, the defender's temporary Health changes are.")
                .With(CreatureAbility.Underground)
                .Picture("A mild office clerk in a cardigan and lanyard at his desk, a mole's snout and claws poking out of his sleeves, a tunnel opening under his chair that leads to a foreign embassy's flag."),
            Creature("Signal Jammer", 1, 5, speed: 1, deploy: 0,
                "No bars. No calls. No witnesses.",
                "Mechanics test: Signal Jammer. The enemy creature directly opposite has its ability switched off (its triggered effects still work).")
                .With(CreatureAbility.SignalJammer)
                .Picture("An agent in a black suit with a backpack bristling with antennas, standing in a busy street where everyone stares in horror at phones showing \"No Service\"."),
            Creature("Deep Cover", 2, 2, speed: 2, deploy: 0,
                "Undercover so long he forgot whose side he's on. So did they.",
                "Mechanics test: Identity Theft, an active ability: hold Shift while dragging it onto a creature to swap abilities with it. It's its action for the Day. It arrives Concealed for 3 Days.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Concealed, 3, TargetType.Self)))
                .With(CreatureAbility.IdentityTheft)
                .Picture("Two men facing each other in front of a mirror, each peeling off a latex mask of the other's face."),
            Creature("Sleeper Agent", 0, 3, speed: 1, deploy: 0,
                "Eleven years next door. Waters your plants. Waiting for a phone call.",
                "Gift, a poisoned one: you plant it in an empty enemy slot and it joins their side. Each Dusk it sabotages: 1 damage to every creature on its side, itself included, so it lasts three Dusks. It's still your card: it comes back to your discard pile. They can get rid of it sooner by summoning over it.",
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.FriendlyCreatures, Each)))
                .Gift()
                .Picture("A suburban dad mowing his lawn in a polo shirt, an earpiece coiled behind his ear, while his shadow on the grass wears a trench coat and holds a sniper rifle."),
            Spell("Human Intelligence", TargetType.Player,
                "Coffee, a smile, a USB stick. The oldest tradecraft there is.",
                "Shadow Ops.' logistics: send for any card from your deck; it joins your next hand. HUMINT: a source on the inside fetches it.",
                SendForCard(SendForFilter.Any))
                .Picture("Two spies on a park bench back to back, one reading a newspaper with eye holes cut in it, the other slipping a card into his coat pocket behind the paper, pigeons watching."),
            Creature("Wet Work", 1, 4, speed: 3, deploy: 0,
                "Nothing personal. Strictly a line item.",
                "Extreme Prejudice: its attack destroys the creature it hits, whatever its Health or Armor, and then it's gone too: one hit, one roadblock. Untouchable or a Temporal Shield stops the hit; an empty slot, it just hits the player for 1.")
                .With(CreatureAbility.ExtremePrejudice)
                .Picture("A hitman in a dripping raincoat under a black umbrella in the rain, pressing a red TERMINATED stamp onto a personnel file with a photo clipped to it."),
            Spell("Perfectly Timed Camera Malfunction", TargetType.EnemyCreatures,
                "Tragic. The one minute the cameras were down.",
                "Destroy target enemy creature if it's Revealed, whatever its Health or Armor (Untouchable still saves it). Set it up with Tapped Communications, which Reveals at once.",
                Destroy(TargetType.EnemyCreatures, TargetRequisite.Revealed))
                .Picture("A security monitor showing static and SIGNAL LOST at 02:13, while at the edge of the frame a gloved hand pulls the camera's cable out of the wall."),
            Spell("Smoke And Mirrors", TargetType.FriendlyCreatures,
                "Look over there. No, the other there.",
                "Mechanics test: Concealed for 2 Days on a friendly creature. Its plans show as ??? to the other side until it's Revealed.",
                Status(StatusEffectType.Concealed, 2, TargetType.FriendlyCreatures))
                .Picture("A stage magician in a tuxedo vanishing a tank behind a cloud of smoke, while a row of mirrors shows a dozen identical tanks to a baffled general."),
            Spell("Eavesdrop", TargetType.EnemyCreatures,
                "We're not listening. We're just... hearing.",
                "Mechanics test: Reveal, instant. Played on an enemy slot, it resolves at once (during Planning) and shows its caster the slot's hidden Slot Effect and what the creature there plans this Day.",
                Reveal(TargetType.EnemyCreatures))
                .Picture("A spy in the next diner booth pressing a drinking glass to the partition, a tower of empty glasses beside him and a waitress refilling his coffee without looking."),
            Spell("Tapped Communications", TargetType.EnemyCreatures,
                "Your call is important to us. Very important. We're recording it twice.",
                "Mechanics test: Revealed for 2 Days, instant. It cancels Concealed and Undercover: the creature's plans can be seen, and Watchdogs can match them.",
                Status(StatusEffectType.Revealed, 2, TargetType.EnemyCreatures))
                .Picture("Split in two by a lightning bolt: on one side a man chatting happily on his phone, on the other side of the call a blinking server."),
            Creature("Whistleblower", 1, 2, speed: 3, deploy: 0,
                "Leaked everything. Now lives in an embassy closet.",
                "Dead Man's Switch: when it dies, every enemy creature is Revealed for 2 Days.",
                On(EffectTrigger.OnDeath, Status(StatusEffectType.Revealed, 2, TargetType.EnemyCreatures)))
                .Picture("A man in a hoodie blowing a sports whistle, classified documents spilling out of every pocket."),
            Spell("Involuntary Free Polonium Giveaway", TargetType.EnemyCreatures,
                "A gift from an old friend. Do not open indoors.",
                "1 damage now, then Burned for 3 Days. Future: Spontaneous Combustion.",
                Damage(1, TargetType.EnemyCreatures), Status(StatusEffectType.Burned, 3, TargetType.EnemyCreatures, potency: 1))
                .Picture("A gentleman in a bowler hat at a bus stop, the tip of his black umbrella dripping glowing green drops that sizzle on the pavement."),
            Creature("Control Room", 0, 5, speed: 1, deploy: 0,
                "Forty screens, all on. Nobody watching the one that matters.",
                "Each Dawn it Reveals a random enemy slot: its hidden Slot Effect and what the creature there plans that Day. Future: chooses the slot.",
                On(EffectTrigger.StartOfTurn, Reveal(TargetType.EnemyCreatures, AtRandom)))
                .Picture("A dark room with a wall of surveillance monitors, two operators asleep in their chairs with headsets on, cold coffee."),
            Spell("Humanitarian Trafficking Action", TargetType.EnemyCreatures,
                "The crates say MEDICINE. The crates say a lot of things.",
                "An aid package with a tracker inside: heals an enemy creature 2, and gives it Heat Signature 2, so random and redirected attacks find it first.",
                Heal(2, TargetType.EnemyCreatures), Status(StatusEffectType.HeatSignature, 1, TargetType.EnemyCreatures, potency: 2))
                .Picture("On a dusty airstrip, a crate stencilled MEDICINE with its lid slid open on rifles: the aid official waves at the camera flashes while, behind the crate, a man in sunglasses counts the cash."),

            // Black Budget, the second Shadow Ops. deck: kills that leave no fingerprints, stolen assets, staged deaths
            Creature("Dead Man's Switch", 1, 2, speed: 2, deploy: 0,
                "If I stop checking in, the files go to every newspaper. So does the bomb.",
                "Last Will: destroy the strongest enemy creature, whatever killed it, even itself (He Didn't Kill Himself, False Flag Operation).",
                On(EffectTrigger.OnDeath, Destroy(TargetType.EnemyCreatures, modifier: StrongestOne)))
                .NotInStarterDeck()
                .Picture("A sweating man in a cheap hotel room, his thumb pressed down on a big red button wired to his wrist, a briefcase of documents and dynamite at his feet, a DO NOT DISTURB sign on the door."),
            Spell("Fell Out of a Window", TargetType.EnemyCreatures,
                "Tragic. Third one this month. Must be the windows.",
                "Destroy the strongest enemy creature if a slot beside it is empty: it has a window. Make room first.",
                Destroy(TargetType.EnemyCreatures, TargetRequisite.NextToEmptySlot, StrongestOne))
                .NotInStarterDeck()
                .Picture("A grand apartment window flung open, curtains billowing over onion domes in the snow, a tiny man in a suit falling past outside, while a man in a tracksuit inside calmly closes the window."),
            Spell("Coup d'État", TargetType.EnemyCreatures,
                "The General thanks the President for his service. Effective immediately.",
                "Take control of the strongest enemy creature if you have more creatures than they do. It changes sides for good, to the slot facing it.",
                TakeControl(TargetType.EnemyCreatures, StrongestOne, TargetRequisite.Outnumbered))
                .NotInStarterDeck()
                .Picture("A general in a peaked cap and mirrored sunglasses settling into the presidential chair, still warm, while two soldiers swap the president's portrait on the wall for his own; tanks through the window."),
            Spell("Operation Paperclip", TargetType.EnemyCreatures,
                "We didn't take their soldiers. We took their scientists. And their pensions.",
                "Take control of target enemy creature with 2 Attack or less. It changes sides for good, to the slot facing it.",
                TakeControl(TargetType.EnemyCreatures, TargetModifier.None, TargetRequisite.AttackAtMost, value: 2))
                .NotInStarterDeck()
                .Picture("A giant paperclip hooking a bespectacled scientist in a lab coat by the collar and lifting him from one flag onto another, rocket blueprints fluttering out of his pockets."),
            Spell("False Flag Operation", TargetType.FriendlyCreatures,
                "They attacked us. Well, someone did. Same thing.",
                "Destroy target friendly creature (its Last Will happens); every other friendly creature gets +1 Attack and +1 Health while it stays in play. Funnel it all into one with Never Let Them Know Your Next Move.",
                Destroy(TargetType.FriendlyCreatures), Buff(1, TargetType.FriendlyCreatures, attack: true, health: true, Each))
                .NotInStarterDeck()
                .Picture("A soldier in an enemy uniform two sizes too small, its flag sewn on upside down, setting fire to his own side's outpost while a film crew records it and a general reads an outrage speech from a card."),
            Spell("Never Let Them Know Your Next Move", TargetType.FriendlyCreatures,
                "Everyone gets a medal. He gets all of them.",
                "Every other friendly creature hands its stat raises to target friendly creature (the timed, the lasting and the ones for good), and it's Concealed for 2 Days: the fatal blow nobody sees coming. After False Flag Operation, it's +2/+2 from each.",
                Act(ActionType.Funnel, 0, TargetType.FriendlyCreatures, TargetModifier.None), Status(StatusEffectType.Concealed, 2, TargetType.FriendlyCreatures))
                .NotInStarterDeck()
                .Picture("A row of agents in identical grey suits passing their pistols, knives and grenades down the line to one man at the end, who disappears under a mountain of weapons with only his sunglasses showing."),
            Spell("Frame Job", TargetType.FriendlyCreatures,
                "The fingerprints were his. We checked. We put them there.",
                "Target friendly creature passes every harmful status it has (Revealed, Heat Signature, Burned, Doomed...) to the enemy creature facing it, with the Days they have left. Nothing faces it: nothing happens.",
                Act(ActionType.PassHarmfulStatuses, 0, TargetType.FriendlyCreatures, TargetModifier.None))
                .NotInStarterDeck()
                .Picture("A smiling agent pressing a sleeping man's thumb onto a smoking gun with tweezers, a police lineup board behind them already labelled with the sleeper's name."),
            Spell("Devil's Breath", TargetType.FriendlyCreatures,
                "One puff of scopolamine and he helped carry his own safe to our van.",
                "Target friendly creature takes the stat raises and the good statuses (Blessed, Concealed, Encrypted, Untouchable) of the enemy creature facing it. Its victims hand everything over, smiling.",
                Act(ActionType.StealStatChanges, 0, TargetType.FriendlyCreatures, TargetModifier.None),
                Act(ActionType.StealGoodStatuses, 0, TargetType.FriendlyCreatures, TargetModifier.None))
                .NotInStarterDeck()
                .Picture("A woman blowing a puff of glittering white powder off her palm into a businessman's face; dazed and smiling, he hands her his watch, his wallet and the keys to an armored car."),
            Spell("He Didn't Kill Himself", TargetType.FriendlyCreatures,
                "Officially: suicide. Unofficially: on a beach in Belize, rereading the files.",
                "Target friendly creature fakes its death: its Last Will happens (Dead Man's Switch destroys their strongest), then it goes back to your hand, in the next Day's hand.",
                Act(ActionType.FakeDeath, 0, TargetType.FriendlyCreatures, TargetModifier.None))
                .NotInStarterDeck()
                .Picture("A sheet pulled over a body on a morgue gurney, but the toes sticking out wear flip-flops with a fresh tan line, and the toe tag is a postcard from a tropical island."),
            Spell("Extraction", TargetType.FriendlyCreatures,
                "Mission accomplished. Or aborted. Details are classified.",
                "Target friendly creature goes back to your hand, in the next Day's hand. Extracted while Revealed, it comes back with a new face: played again, it arrives Concealed for 2 Days.",
                Act(ActionType.ReturnToHand, 0, TargetType.FriendlyCreatures, TargetModifier.None))
                .NotInStarterDeck()
                .Picture("A black helicopter at night hoisting an agent off a rooftop on a rope ladder, searchlights sweeping, his trench coat flapping and a briefcase handcuffed to his wrist."),
            Spell("Witness Protection Program", TargetType.FriendlyCreatures,
                "New name, new town, new moustache. Same bad feeling.",
                "Target friendly creature goes dark until the next Dawn: whatever lands this Day misses it (board-wide damage included), but its slot is empty meanwhile. It comes back with a new identity: Concealed for 2 Days, unknown to the enemy, its old statuses gone.",
                Act(ActionType.Vanish, 0, TargetType.FriendlyCreatures, TargetModifier.None))
                .NotInStarterDeck()
                .Picture("A man in a terrible fake moustache and a Hawaiian shirt mowing a lawn in a quiet cul-de-sac, his old mugshot pinned to a corkboard behind the window, a federal agent in a parked car pretending to read a newspaper."),
            Spell("Death By \"Natural Causes\"", TargetType.EnemyCreatures,
                "Heart attack. At 34. While jogging. Twice.",
                "Kill Order 2 on target enemy creature: at the Dusk two Days later, it's destroyed (Untouchable saves it). It's secret: the other side doesn't see the spell played nor the Kill Order on their creature. A Cleanse removes it, knowingly or not.",
                Status(StatusEffectType.KillOrder, 2, TargetType.EnemyCreatures))
                .NotInStarterDeck()
                .Picture("A coroner signing a death certificate marked NATURAL CAUSES with a flourish, while on the gurney beside him a tiny dart with a red feather sticks out of the dead man's neck."),
            Spell("Asset Recruitment", TargetType.EnemyCreatures,
                "We'll double your salary. Or tell your wife. Your choice.",
                "Brainwashed 2 on target enemy creature: when it runs out, at Dusk, it changes sides for good, its ability lost. Revealed, it's turned faster: Brainwashed 1. Set it up with Tapped Communications.",
                Status(StatusEffectType.Brainwashed, 2, TargetType.EnemyCreatures),
                Requiring(Status(StatusEffectType.Brainwashed, 1, TargetType.EnemyCreatures), TargetRequisite.Revealed))
                .NotInStarterDeck()
                .Picture("A handler in a dim bar sliding a thick envelope and a photograph across the table to a nervous soldier; the photo shows the soldier with a woman who isn't his wife."),
            Creature("If I Told You, I'd Have to Kill You", 2, 4, speed: 2, deploy: 0,
                "It's classified. So are you, now.",
                "When Revealed (Revealed, or a Reveal shows it): 4 damage to whoever revealed it, the creature whose effect it was or else its player. Allergic to Light: each time, it takes 2 damage itself.",
                On(EffectTrigger.OnRevealed, Damage(4, TargetType.Enemy)))
                .With(CreatureAbility.AllergicToLight)
                .NotInStarterDeck()
                .Picture("A spy in a dark suit leaning in at a cocktail party to whisper into an eager journalist's ear, while under the table his other hand quietly screws a silencer onto a pistol."),
            Creature("Lone Gunman", 1, 3, speed: 1, deploy: 0,
                "Acted alone. Says he didn't. Officially, that settles it.",
                "Fall Guy: when an enemy effect would make another friendly creature Revealed or destroy it (Perfectly Timed Camera Malfunction, Extreme Prejudice...), it lands on the Lone Gunman instead: the official story needs one man who did it all. (Was Scapegoat, too close to Mind's Escape Goat.)")
                .With(CreatureAbility.FallGuy)
                .NotInStarterDeck()
                .Picture("A bewildered young man in a cheap suit shoved in front of flashing press cameras, holding a rifle he has clearly never seen before, a LONE GUNMAN placard hung round his neck, while two men in dark suits and hats slip away behind a picket fence on a grassy knoll."),
            Spell("Glomar Response", TargetType.Enemy,
                "We can neither confirm nor deny the existence of this card.",
                "A Watchdog: the next 2 enemy reveals (a Reveal, Revealed, or a spell that does either, even an instant one) are nullified, and they learn nothing. Monitored: it acts the moment they try.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Intelligence, side = WatchdogSide.Enemy, effect = WatchdogEffect.Nullify, charges = 2 }, TargetType.Enemy))
                .NotInStarterDeck()
                .Picture("A press officer at a podium with a perfectly blank face, every line on his teleprompter blacked out with thick redaction bars, reporters raising their hands in vain."),
            Spell("Sabotage", TargetType.Enemy,
                "Someone filled the cannon with sand. Someone else lit it.",
                "A Briefing Watchdog: from the Day after it's set, the strongest enemy attack backfires: it doesn't happen, and the attacker takes its own Attack as damage. 1 charge.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Attack, side = WatchdogSide.Enemy, effect = WatchdogEffect.Backfire,
                    charges = 1, timing = WatchdogTiming.Briefing, pick = BriefingPick.Strongest }, TargetType.Enemy))
                .NotInStarterDeck()
                .Picture("A tank that has just fired, its barrel peeled back like a banana and the crew blackened and blinking, while a saboteur in coveralls tiptoes away with an empty sack marked SUGAR - FOR THE FUEL TANK."),
            Spell("Ghost Army", TargetType.Player,
                "Ten thousand men, four hundred tanks. Some assembly required. Do not inflate near cacti.",
                "Put 2 Inflatable Tanks (0/1 decoys) in your empty slots: until a Reveal or Revealed shows them, the enemy sees each as a Sherman Tank, 6/6. Big attacks and Destroys get wasted on balloons. Tokens: gone when they leave play.",
                SummonDecoys(2, "Inflatable Tank"))
                .NotInStarterDeck()
                .Picture("Two soldiers carrying a full-size rubber tank over their heads like a canoe across a muddy field at dawn, a third pumping up another with a bicycle pump, while an enemy spotter plane circles above."),
            Creature("Inflatable Tank", 0, 1, speed: 1, deploy: 0,
                "Pressure: 2 bar. Threat level: classified.",
                "A Ghost Army decoy (a token): to the enemy it's a Sherman Tank until it's revealed.")
                .Token(disguise: "Sherman Tank")
                .Picture("A rubber tank half-deflated on a field, its barrel drooping, a soldier patching a hole with a bicycle repair kit."),
            Creature("Sherman Tank", 6, 6, speed: 1, deploy: 0,
                "Thirty tons of steel. Allegedly.",
                "Never a real card: the face a Ghost Army decoy shows the enemy until it's revealed.")
                .FakeFace(Faction.Statecraft)
                .Picture("A heavy tank rolling out of the morning fog, cannon level, mud spraying from its tracks."),
            Spell("HAARP", TargetType.Player,
                "Weather forecast: 100% chance of lightning, localized, on purpose.",
                "Earthquake: from a random slot, a zigzag across the whole board, crossing sides at each column, 1 damage each hit. Every column is hit once, on one side or the other: it finishes off the wounded, and it can shake yours. Nobody's attack: nothing answers it or redirects it. Was the Survival of the Fittest Trend (that Trend has no card now).",
                Act(ActionType.Earthquake, 1, TargetType.Player, TargetModifier.None))
                .NotInStarterDeck()
                .Picture("A field of antennas in the Alaskan tundra under a purple sky, a single bolt of lightning forking down onto one tiny enemy tent miles away, a technician in a parka sipping coffee by the control panel."),
            // Information trades: you need to give information to get some. Discard now, draw more with the next hand
            Spell("Social Engineering", TargetType.Player,
                "Tell them one secret and they'll tell you two.",
                "Discard 1 (a random other card from your hand, given up to play this), then draw 2 with the next Day's hand.",
                Draw(2))
                .Discarding(1)
                .NotInStarterDeck()
                .Picture("A smiling man in a hi-vis vest with a clipboard and a fake lanyard holding a door open for a whole office of employees, each one handing him their badge and password on a sticky note as they pass."),
            Spell("Bilderberg Membership", TargetType.Player,
                "Membership fee: one secret a year. Benefits: all of theirs.",
                "Discard 2 (random other cards from your hand, given up to play this), then draw 4 with the next Day's hand.",
                Draw(4))
                .Discarding(2)
                .NotInStarterDeck()
                .Picture("A candlelit dining hall in an alpine hotel, a long table of old men in dinner jackets trading sealed envelopes across the silverware, a doorman outside the window turning away the press."),
            Spell("Dead Drop", TargetType.Player,
                "Leave the package under the third bench. Take the other one. Don't look inside either.",
                "Discard 3 (random other cards from your hand, given up to play this), then draw 4 with the next Day's hand.",
                Draw(4))
                .Discarding(3, chosen: true)
                .NotInStarterDeck()
                .Picture("A park bench in autumn, a man in a trench coat feeding pigeons while sliding a hollowed-out brick behind him with his heel, another man on the next bench reading an upside-down newspaper."),
            // Double-O Section, the spy-film Shadow Ops.: gadgets, licences and villains with very specific methods
            Spell("The Golden Gun", TargetType.EnemyCreatures,
                "A million a shot. He only ever needs the one.",
                "Destroy target enemy creature, whatever its Health or Armor (Untouchable still saves it). Single Use: one golden bullet; once played, the card is gone for the rest of the combat.",
                Destroy(TargetType.EnemyCreatures))
                .SingleUse()
                .NotInStarterDeck()
                .Picture("A gold pistol pieced together from a cigarette case, a lighter, a cufflink and a fountain pen, lying on black velvet beside a single golden bullet engraved with a name."),
            Spell("Die Another Day", TargetType.FriendlyCreatures,
                "Not today. Probably not tomorrow. Ask again on Thursday.",
                "Borrowed Time on target friendly creature: it can't die (damage leaves it at 0 Health, a Destroy fails) until the Dusk of the next Day; then, if it's at 0 Health, it dies. Heal it in time and it lives.",
                Status(StatusEffectType.BorrowedTime, 1, TargetType.FriendlyCreatures))
                .NotInStarterDeck()
                .Picture("A spy in a torn tuxedo surfing a towering wave on a car door past a collapsing ice palace, checking his wristwatch."),
            Spell("Licence to Kill", TargetType.FriendlyCreatures,
                "Paperwork for murder, signed in triplicate. Valid until used.",
                "Licensed: target friendly creature's next attack on a creature destroys it, whatever its Health or Armor (Untouchable or a Temporal Shield stops the hit, and spends the licence). It keeps it until it's used.",
                Status(StatusEffectType.Licensed, 1, TargetType.FriendlyCreatures))
                .NotInStarterDeck()
                .Picture("A civil servant in sleeve garters at a cluttered desk stamping a laminated LICENCE TO KILL card with a smirking agent's passport photo, a tray of forms marked 00 beside him."),
            Spell("Shaken, Not Stirred", TargetType.FriendlyCreatures,
                "Three measures of gin, one of vodka, half of Kina Lillet. Nerves: none.",
                "Target friendly creature gets +2 Attack this Day, and it's Composed this Day: stuns (Exhausted) don't land on it, and it shakes off one it has.",
                Buff(2, TargetType.FriendlyCreatures, attack: true, health: false, days: 1), Status(StatusEffectType.Composed, 1, TargetType.FriendlyCreatures))
                .NotInStarterDeck()
                .Picture("A bartender shaking a cocktail shaker while explosions rock the casino behind him, an agent in a tuxedo calmly waiting at the bar for his martini, not a hair out of place."),
            Spell("GoldenEye", TargetType.Enemy,
                "One pulse. Every screen, every pacemaker, every smart fridge. Off.",
                "An electromagnetic pulse on the enemy side: Blackout on every enemy creature (its ability and effects are off until the Dusk of the next Day), and every enemy Watchdog is wiped out.",
                Status(StatusEffectType.Blackout, 1, TargetType.EnemyCreatures, modifier: Each), Act(ActionType.WipeWatchdogs, 0, TargetType.Enemy, TargetModifier.None))
                .NotInStarterDeck()
                .Picture("A satellite dish shaped like a golden eye in orbit firing a ripple of light at a city at night; below, every window goes dark at once and a man's toaster pops out smoking."),
            Spell("Supply Chain Attack", TargetType.Enemy,
                "Every router they bought this year came with a free extra. It phones home.",
                "Disrupts the big organizations most: every enemy creature still deploying (Deployment Time left: Statecraft's slow bodies) waits 2 more Days, and the enemy draws 2 fewer cards with their next hand (Corporate's draw engine).",
                Requiring(Act(ActionType.DelayDeployment, 2, TargetType.EnemyCreatures, Each), TargetRequisite.Deploying),
                Act(ActionType.CutDraws, 2, TargetType.Enemy, TargetModifier.None))
                .NotInStarterDeck()
                .Picture("A cheerful delivery driver unloading crates of identical routers at a ministry's loading dock, each box with a tiny skull sticker under the barcode, while a line of bureaucrats in grey suits signs for them."),
            Spell("Hollowed-Out Volcano Lair", TargetType.Player,
                "Monorail, piranha pool, self-destruct button. Planning permission: pending.",
                "Trend for 3 Days, Volcano Lair: each Day (from the Day it's set), on each side the strongest creature hides in a lair: it's Untouchable that Day, as long as another creature of its side stands. Every villain gets one: play it when your boss is the one worth hiding.",
                SetTrend(Trend.VolcanoLair, 3))
                .NotInStarterDeck()
                .Picture("A cutaway of a volcano with a sleek steel base inside: a monorail, a rocket on its launch pad, henchmen in matching jumpsuits, and at the top a bald man in a grey tunic stroking a white cat on a throne behind armored glass."),
            Creature("Femme Fatale", 1, 4, speed: 2, deploy: 0,
                "She didn't kill him. She just held him very, very close.",
                "Each Dusk she squeezes the enemy creature facing her: 1 damage to it, and she heals 1. Nothing facing her: nothing happens. Ambush: she arrives as ??? and deals double while nobody knows who she is.",
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.EnemyCreatures, Facing), Heal(1, TargetType.Self)))
                .With(CreatureAbility.Ambush)
                .NotInStarterDeck()
                .Picture("A glamorous woman in a black evening gown smiling over her cocktail at a casino table, while the red-faced general in the chair beside her is squeezed between her crossed legs, his medals popping off."),
            Creature("MK Ultra Test Subject", 2, 3, speed: 1, deploy: 0,
                "Volunteered for a Harvard study in 1959. Doesn't remember the rest.",
                "Each Dawn it attacks a random creature, on either side (never itself): strong, cheap, uncontrolled. It can still attack as you plan.",
                On(EffectTrigger.StartOfTurn, Strike(TargetType.AllCreatures, AtRandom)))
                .NotInStarterDeck()
                .Picture("Ted Kaczynski as the young Harvard math prodigy, before the woods: clean-cut, short neat hair, white shirt and narrow tie, strapped into a 1950s lab chair with electrodes taped to his temples, a blackboard of equations behind him and a psychologist with a clipboard in the corner."),
        };

        // Tech: rewires the queue
        private static List<CardDef> TechCards() => new List<CardDef> {
            Spell("Web Scraping", TargetType.Player,
                "We respect your robots.txt. We read it, anyway.",
                "Tech's scouting: send for any card among the top 5 of your deck; it joins your next hand.",
                SendForCard(SendForFilter.Any, top: 5))
                .Picture("A cat scratching at a laptop screen, claw marks tearing the web page open and spilling out cards and data."),
            Spell("DDOS Attack", TargetType.EnemyCreatures,
                "Ten thousand requests a second. All of them from fridges.",
                "2 damage, and Exhausted: it cancels the target's planned actions (spells land before attacks), lasts the rest of the Day and all of the next, turns its abilities off and adds +1 to every hit (Exposed). Aimed at an enemy slot it's Fixed: the enemy can dodge by moving.",
                Stun(1, TargetType.EnemyCreatures), Damage(2, TargetType.EnemyCreatures))
                .Picture("A server rack melting under an avalanche of tiny envelope icons, smart fridges and toasters in the background, each with a red glowing eye."),
            Spell("Antivirus", TargetType.Player,
                "1 threat blocked. 37 offers to upgrade to Premium.",
                "Mechanics test: a Watchdog (GDD 3.11). It's created when the spell resolves and, Monitored, nullifies the next enemy spell before it resolves, even one planned before it existed. 1 charge.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Spell, side = WatchdogSide.Enemy, effect = WatchdogEffect.Nullify, charges = 1 }, TargetType.Enemy))
                .Picture("A panda in shining knight's armor, an antivirus logo on its shield, blocking a glowing spell scroll at a castle gate, while the castle walls behind it are plastered with its own pop-ups: \"RENEW NOW - 60% OFF\"."),
            Creature("Script Kiddie", 3, 3, speed: 3, deploy: 0,
                "Doesn't know how it works. Doesn't need to.",
                "Mechanics test: Zero-Day. Its attacks and effects ignore the target's defensive abilities (Unbothered, No U, Return to Sender...), except Cold Storage.")
                .With(CreatureAbility.ZeroDay)
                .Picture("A twelve-year-old in an oversized gaming chair, hoodie up, double-clicking \"H4CK3R T00L.exe\" while a bank vault door swings open on his second monitor."),
            Creature("Legacy Code", 2, 7, speed: 1, deploy: 1,
                "Written in 1987. Nobody knows how it works. Nobody dares touch it.",
                "Mechanics test: Cold Storage. Enemy effects can't lower its stats, and Zero-Day can't get around it.")
                .With(CreatureAbility.Unpatchable)
                .Picture("A beige mainframe held together with duct tape in a dusty basement, a yellowed sticky note on it: \"DO NOT TOUCH - Gary (retired 1999)\", and cobwebs over an \"Update available\" pop-up."),
            Spell("End-to-End Encryption", TargetType.FriendlyCreatures,
                "Not even we can read your messages. Pinky promise.",
                "Mechanics test: Encrypted for 2 Days on a friendly creature. Watchdogs ignore its actions and spend no charge on them.",
                Status(StatusEffectType.Encrypted, 2, TargetType.FriendlyCreatures))
                .Picture("Two carrier pigeons passing a sealed envelope covered in padlocks, while a government agent below swings a butterfly net and waves a \"Backdoor Request\" form."),
            Creature("SysAdmin", 2, 5, speed: 1, deploy: 1,
                "Have you tried turning it off and on again?",
                "Each Dawn it cleanses your creatures' harmful statuses (Tech owns Cleanse).",
                On(EffectTrigger.StartOfTurn, Cleanse(CleanseTarget.Statuses, TargetType.FriendlyCreatures)))
                .Picture("A bearded sysadmin in socks and sandals yanking the power cable out of a smoking server rack, a coffee mug in his other hand."),
            Creature("Meatproxy As A Service", 1, 4, speed: 1, deploy: 1,
                "Powered by AI. The AI is Dave. Dave copies and pastes.",
                "A draw each Dawn, paid with 1 of your Health. Future: Technical Debt (fewer draws next Day).",
                On(EffectTrigger.StartOfTurn, Draw(1), Damage(1, TargetType.Player)))
                .Picture("A raw steak sitting at a desk, copy-pasting text from one window to another on a computer screen, a price tag on the monitor: $9.99/MONTH."),
            Spell("Ransomware Attack", TargetType.EnemyCreatures,
                "Your files are safe. With us.",
                "Exhausted, 2 damage, and you draw a card (the ransom).",
                Stun(1, TargetType.EnemyCreatures), Damage(2, TargetType.EnemyCreatures), Draw(1))
                .Picture("A computer screen with a grinning skull and a padlock demanding 1 BTC, while a grandma cries into her knitting."),
            Spell("Man-in-the-Middle", TargetType.Player,
                "Hi, it's your bank. Also, it's not.",
                "A Monitored Watchdog: the next enemy spell is redirected to a random creature. 1 charge.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Spell, side = WatchdogSide.Enemy, effect = WatchdogEffect.Redirect, charges = 1 }, TargetType.Enemy))
                .Picture("A man squeezed on a park bench between two lovers, holding both of their phones and reading their texts."),
            Creature("Technical Debt", 4, 5, speed: 2, deploy: 0,
                "Ship it now. Someone will fix it later. You are someone.",
                "Decay 1: strong today, it pays for it every Dusk.",
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self)))
                .Picture("A laptop held together with duct tape and a TODO sticky note, beside an empty, cracked piggy bank, the dashed outlines of the missing banknotes floating above."),
            Creature("Scope Creep", 2, 3, speed: 1, deploy: 0,
                "Just one more small feature. And one more.",
                "Grow 1: it gets bigger every Dawn, like the project.",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.Self, attack: true, health: true)))
                .Picture("A Gantt chart on an office whiteboard whose bars have outgrown it: they run off the board, across the wall and out of the window, past the DEADLINE line drawn in red, while a smiling manager sticks on one more note: \"+1 small feature\"."),
            Creature("Sex Clanker", 2, 7, speed: 1, deploy: 0,
                "Batteries not included. Feelings not included either.",
                "Arrival: Heat Signature 3 on itself. It draws every random effect and redirected attack: a decoy too attractive to ignore.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.HeatSignature, 1, TargetType.Self, potency: 3)))
                .NotInStarterDeck()
                .Picture("Candlelight and satin: a figure in full shining plate reclines on a red heart-shaped bed, propped on one elbow, a rose in the visor and one knee up, rose petals everywhere, a heart shield leaning against the bed."),
        };

        // Myth: faith, death and prophecy. Creatures that rise again (Revive) or die for the cause (Doomed), sacrifices that pay
        // for miracles (Sacrifice), prophecies that come true Days later (Prophecy), converts (control change), and wrath on
        // the unbelievers (Holy War). Pays with its own creatures; can't move them.
        private static List<CardDef> MythCards() => new List<CardDef> {
            Spell("Omen Reading", TargetType.Player,
                "The entrails say Tuesday. The goat disagreed.",
                "Myth's ritual: Sacrifice 1, then send for any card from your deck; it joins your next hand.",
                SendForCard(SendForFilter.Any))
                .Sacrificing(1)
                .Picture("A robed augur reading the entrails of a goat on a stone altar by candlelight, squinting at a calendar pinned to the wall, an assistant taking notes."),
            Spell("Memento Mori", TargetType.AllCreatures,
                "Remember that you will die. Preferably on schedule.",
                "Mechanics test: Doomed on either side. It wipes other statuses and blocks new ones, gives +1 permanent Attack each Dawn (it stays on the card), then Last Rites strike the opposite slot even if the creature Cannot Act, and it dies for real.",
                Status(StatusEffectType.Doomed, 3, TargetType.AllCreatures))
                .NotInStarterDeck()
                .Picture("A skull on an office desk beside an hourglass, and a wall calendar with three days crossed out in red."),
            Creature("Martyr", 2, 4, speed: 1, deploy: 1,
                "He died for your sins. Your sins are on backorder.",
                "Mechanics test: its entry effect waits for Deployment Time; dooming itself makes a planned sacrifice that grows stronger; its OnDied effect fires when the sentence is carried out.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Doomed, 2, TargetType.Self)),
                On(EffectTrigger.OnDeath, Heal(3, TargetType.FriendlyCreatures, Each)))
                .Picture("A long-haired, bearded Jesus-like figure in white robes, arms outstretched, a price sticker on his halo. An online-store banner beside him reads \"Salvation: Add to Cart (ships in 3 days)\"."),
            Spell("The Holy Grail", TargetType.FriendlyCreatures,
                "Drink from it and live forever. Terms and conditions apply.",
                "Mechanics test: Blessed 2. Each stack negates the next status that would land, friendly or not, Doomed included. Persistent, up to 3 stacks.",
                Status(StatusEffectType.Blessed, 0, TargetType.FriendlyCreatures, potency: 2))
                .NotInStarterDeck()
                .Picture("A gilded chalice on a velvet cushion behind museum glass. A queue of knights in dented armor stretches out the door, each holding a numbered ticket, and a gift shop by the exit sells plastic replicas at 3 for 2."),
            Spell("Exorcism", TargetType.AllCreatures,
                "The power of Christ compels you. The invoice follows.",
                "Mechanics test: Cleanse (statuses) on any creature. Blessed loses all its stacks. On a Doomed creature it's Heresy: the sentence passes to the weakest creature on its side, or is carried out if it's alone.",
                Cleanse(CleanseTarget.Statuses, TargetType.AllCreatures))
                .NotInStarterDeck()
                .Picture("A priest in full vestments holding a crucifix over a teenager on a bed, whose smartphone floats above them, glowing red, demonic notification bubbles pouring out of it."),
            Creature("Mummy", 2, 5, speed: 1, deploy: 1,
                "Touch the tomb, get the curse. It was in the fine print.",
                "Mechanics test: Hive Mind. A creature that attacks it gains Hive Mind, replacing its own ability.")
                .With(CreatureAbility.HiveMind)
                .NotInStarterDeck()
                .Picture("A museum mummy shaking hands with a smiling tourist, its bandages unravelling and winding up the tourist's arm, a \"Please Do Not Touch\" sign knocked to the floor."),
            Creature("Omniscient Space-Time Bending Bearded Wizard", 3, 9, speed: 1, deploy: 2,
                "Works in mysterious ways. Mostly late.",
                "Each Dawn, Blessed on a random friendly creature.",
                On(EffectTrigger.StartOfTurn, Status(StatusEffectType.Blessed, 0, TargetType.FriendlyCreatures, potency: 1, modifier: AtRandom)))
                .NotInStarterDeck()
                .Picture("An old bearded man on a cloud in a bathrobe and slippers, lazily pointing a TV remote at a tiny spinning universe."),
            Creature("Cultist", 1, 1, speed: 2, deploy: 0,
                "The end is near. Brunch is at eleven.",
                "Fodder: when it dies, every friendly creature gets +1 Attack for good. The first to go when a Sacrifice is paid.",
                On(EffectTrigger.OnDeath, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, Each)))
                .Picture("A hooded cultist holding a THE END IS NEAR sign in one hand and a pumpkin spice latte in the other."),
            Creature("Phoenix", 2, 2, speed: 2, deploy: 1,
                "Dies, comes back, posts about it.",
                "Revive: once per combat it rises again at the next Dawn.",
                Revive())
                .NotInStarterDeck()
                .Picture("A flaming bird rising from the smoking ashes of a burnt toaster, wings spread wide."),
            Spell("Excess of Bread and Wine", TargetType.FriendlyCreatures,
                "Turned water into wine. Then wine into a hangover.",
                "Heals your creatures; one of them is Exhausted (the hangover).",
                Heal(2, TargetType.FriendlyCreatures, Each), Stun(1, TargetType.FriendlyCreatures, AtRandom))
                .NotInStarterDeck()
                .Picture("The Last Supper as a frat party: the apostles playing beer pong along the long table while Jesus refills everyone's cups from a jug."),
            Spell("Monkey Hand", TargetType.Player,
                "Your wish is granted. Read the fine print.",
                "Draw 3, and a random friendly creature is Doomed.",
                Draw(3), Status(StatusEffectType.Doomed, 3, TargetType.FriendlyCreatures, modifier: AtRandom))
                .NotInStarterDeck()
                .Picture("A shriveled, mummified monkey's paw held up in a purple room, like the cursed paw of the old horror story: long knobbly grey-green fingers with pointed nails, a tuft of black fur at the wrist. Three fingers still up, one already curled into the palm: three wishes left."),
            Spell("Sacrificial Ritual", TargetType.Player,
                "Somebody has to go. Ideally the intern.",
                "Sacrifice 1: draw 3. The weakest goes, so a Cultist pays for it and leaves its blessing behind.",
                Draw(3))
                .Sacrificing(1)
                .Picture("Hooded figures chanting around a chalk pentagram, offering a rubber chicken on the altar."),
            Creature("Reincarnation", 1, 1, speed: 2, deploy: 0,
                "Last life, a worm. This life, a good boy. Next life: depends how this one goes.",
                "Rebirth (once per combat it survives death with 1 Health) and Grow 1: each life a little better.",
                OncePerCombat(On(EffectTrigger.OnAboutToDie, Heal(1, TargetType.Self))),
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.Self, attack: true, health: true)))
                .NotInStarterDeck()
                .Picture("The Buddhist wheel of rebirth glowing in a temple: a pink worm on the left, a happy brown dog on the right, a dashed arrow arching from the worm's life to the dog's, a lotus between them."),

            // --- The crusade (2026-10-14) ---
            Spell("Holy War", TargetType.Enemy,
                "God is on our side. He's on theirs too. He's very busy.",
                "Myth's wrath: every enemy creature takes 2 damage; yours take 1 (holy wars have no bystanders).",
                Damage(2, TargetType.EnemyCreatures, Each), Damage(1, TargetType.FriendlyCreatures, Each))
                .Picture("Two armies of medieval knights charging each other across a field, both under identical banners with the same golden cross, a bewildered angel in the clouds above holding a scorecard."),
            Spell("The Great Flood", TargetType.Enemy,
                "Forty days of rain. You were warned. Twice.",
                "Prophecy 2: in two Days, every enemy creature takes 3 damage. They see it coming; you'd better be ready to float.",
                Damage(3, TargetType.EnemyCreatures, Each))
                .Foretold(2)
                .NotInStarterDeck()
                .Picture("A wooden ark riding a huge wave, the animals two by two waving from the deck, while a line of enemy soldiers on a hilltop holds umbrellas as the water reaches their chins."),
            Spell("Mox Nox", TargetType.Enemy,
                "Soon it will be night.",
                "Prophecy 2: at the Dawn two Days on, the strongest enemy creature (picked then) is destroyed.",
                Destroy(TargetType.EnemyCreatures, modifier: StrongestOne))
                .Foretold(2)
                .Picture("An old sundial in a ruined cloister at dusk, the shadow of its gnomon falling across the Latin words MOX NOX carved in the stone, a black candle burning beside it."),
            Spell("Holy Hand Grenade of Antioch", TargetType.EnemyCreatures,
                "Count to three. Not four. Not two. Five is right out.",
                "5 damage to target enemy creature and 2 to the creatures beside it (the user, 2026-10-15).",
                Damage(5, TargetType.EnemyCreatures), Damage(2, TargetType.EnemyCreatures, TargetModifier.Beside))
                .NotInStarterDeck()
                .Picture("A golden orb with a jewelled cross on top, its pin pulled, held up by a monk reading from an enormous book, three fingers raised, knights ducking behind him."),
            Spell("Lance Of Longinus", TargetType.EnemyCreatures,
                "Pierced the side of a god. Your creature is not a problem.",
                "3 damage to target enemy creature.",
                Damage(3, TargetType.EnemyCreatures))
                .Picture("An ancient iron spearhead on a long wooden shaft, glowing faintly, pinned through a spy's trench coat to a brick wall, the spy wriggling out of the sleeves."),
            Spell("Necronomicon", TargetType.EnemyCreatures,
                "Bound in human skin. The reviews are mixed.",
                "Destroy target enemy creature; your weakest creature is sacrificed to play it. Black magic always takes something.",
                Destroy(TargetType.EnemyCreatures))
                .Sacrificing(1)
                .Picture("A heavy book bound in stitched leather with a screaming face on its cover, lying open on a stone altar, a hooded figure reading from it while a shadowy hand rises out of the pages."),
            Spell("The Wicker Man", TargetType.Enemy,
                "For a good harvest. Nothing personal, Sergeant.",
                "Sacrifice 1: every enemy creature takes 2 damage. The film's ending, as a sweeper.",
                Damage(2, TargetType.EnemyCreatures, Each))
                .Sacrificing(1)
                .Picture("A giant wicker figure burning on a cliff at sunset, villagers in animal masks dancing around it and singing, a small policeman in uniform visible inside its chest."),
            Spell("Thoughts and Prayers", TargetType.FriendlyCreatures,
                "Sent. Nothing else will be.",
                "Blessed 1 on every friendly creature. A politician's answer: it changes nothing, except the next status.",
                Status(StatusEffectType.Blessed, 0, TargetType.FriendlyCreatures, potency: 1, modifier: Each))
                .NotInStarterDeck()
                .Picture("A politician at a podium covered in microphones, eyes closed and hands pressed together in prayer, while behind him an aide quietly shreds a stack of papers labelled REFORM."),
            Spell("Religious Instrumentalization", TargetType.EnemyCreatures,
                "Holding the book upside down. It's the gesture that counts.",
                "Conversion: take control of target enemy creature with 2 Attack or less. It joins you, for good.",
                TakeControl(TargetType.EnemyCreatures, TargetModifier.None, TargetRequisite.AttackAtMost, 2))
                .Picture("A politician in a suit posing for cameras in front of a church, holding a bible upside down over his head, while a confused small creature from the other side shuffles over to stand behind him."),
            Spell("Deus Vult", TargetType.FriendlyCreatures,
                "God wills it. God was not consulted.",
                "Every friendly creature gets +2 Attack this Day.",
                Buff(2, TargetType.FriendlyCreatures, attack: true, health: false, Each, days: 1))
                .NotInStarterDeck()
                .Picture("A crusader in a bucket helmet and white tabard with a red cross shouting with a raised sword, a meme caption bar at the bottom of the picture."),
            Spell("Curse Of Hubris", TargetType.Enemy,
                "Flew too close to the sun. Landed in the comments.",
                "The strongest enemy creature gets -3 Attack, for as long as it's in play.",
                Buff(-3, TargetType.EnemyCreatures, attack: true, health: false, StrongestOne))
                .NotInStarterDeck()
                .Picture("Icarus with melting wax wings falling past a mountain top, a crowd of Greek gods on a cloud filming him on their phones."),
            Spell("Genesis", TargetType.Player,
                "Let there be light. And two more creatures.",
                "Summon 2 creatures from your deck.",
                Summon(2))
                .NotInStarterDeck()
                .Picture("A huge bearded hand reaching out of the clouds and pointing at the ground, where two small creatures pop out of the earth blinking, a sun switching on above them like a lamp."),
            Spell("The Lost Ark", TargetType.Enemy,
                "Don't look at it. Seriously, don't.",
                "Every enemy creature takes 3 damage; yours close their eyes (Exhausted).",
                Damage(3, TargetType.EnemyCreatures, Each), Stun(1, TargetType.FriendlyCreatures, Each))
                .NotInStarterDeck()
                .Picture("A golden chest with two winged angels on its lid, opened in a desert camp, ghostly light pouring out and melting the faces of enemy officers while an adventurer in a fedora keeps his eyes shut."),

            Creature("Templar Knight", 2, 5, speed: 1, deploy: 1,
                "Took a vow of poverty. Owns half of Europe.",
                "When ready: Blessed 2 on itself, so the next two statuses aimed at it are wasted.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Blessed, 0, TargetType.Self, potency: 2)))
                .Picture("A knight in chainmail and a white mantle with a red cross, sword raised, standing in front of a medieval bank counter piled with gold coins and promissory notes."),
            Creature("Zealot", 3, 2, speed: 2, deploy: 0,
                "Will die for the cause. Will tell you about it first.",
                "When ready: Doomed 3 on itself. It grows +1 Attack each Dawn, then its Last Rites strike and it dies for the cause.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Doomed, 3, TargetType.Self)))
                .Picture("A wild-eyed hermit in sackcloth with a long beard, waving a burning torch and a hand-painted REPENT sign on a busy city sidewalk."),
            Creature("Living Dead", 2, 3, speed: 1, deploy: 0,
                "Died on Friday. Back on Monday. Still not answering emails.",
                "Revive: once per combat it rises again at the next Dawn.",
                Revive())
                .Picture("A grey zombie in a business suit climbing out of a cemetery grave with its briefcase, a sunrise behind the tombstones."),
            Creature("The Chosen One", 2, 4, speed: 2, deploy: 1,
                "This time he brought backup.",
                "Spray and Pray: its attacks also hit the creatures beside its target. The second coming, armed.",
                new CardEffect[0])
                .With(CreatureAbility.SprayAndPray)
                .Picture("Jesus in white robes and sandals with a halo, firing two golden submachine guns akimbo, doves scattering; nothing behind him but the temple light, he steals the show."),
            Creature("Killer Rabbit of Caerbannog", 3, 2, speed: 3, deploy: 0,
                "Look at the bones!",
                "A harmless-looking bunny that strikes first and hard.",
                new CardEffect[0])
                .Picture("A small fluffy white rabbit sitting innocently in front of a cave mouth littered with bones and dented helmets, a tiny drop of red on its whiskers, knights fleeing in the distance."),
            Creature("Black Knight", 2, 6, speed: 1, deploy: 0,
                "'Tis but a scratch.",
                "Unbothered: enemy effects can't lower its stats. Arms off, legs off: it's only a flesh wound.")
                .With(CreatureAbility.Unbothered)
                .Picture("A knight in black armor standing defiantly at a forest bridge with no arms left, sword on the ground, shouting at a bemused king who rides an invisible horse to the sound of coconuts."),
            Creature("Buddy Christ", 2, 4, speed: 1, deploy: 0,
                "Winks. Points. Forgives.",
                "Each Dawn, every friendly creature heals 1.",
                On(EffectTrigger.StartOfTurn, Heal(1, TargetType.FriendlyCreatures, Each)))
                .Picture("A smiling plastic Jesus statue winking and giving a big thumbs up and a finger point, on a church lawn next to a sign reading CATHOLICISM: WOW!"),
            Creature("Pope", 1, 7, speed: 1, deploy: 1,
                "Infallible. Bulletproof glass, just in case.",
                "Each Dawn, Blessed 1 on your strongest creature.",
                On(EffectTrigger.StartOfTurn, Status(StatusEffectType.Blessed, 0, TargetType.FriendlyCreatures, potency: 1, modifier: StrongestOne)))
                .Picture("The Pope waving from a glass-domed popemobile, sprinkling holy water from a garden hose over a crowd of creatures."),
            Creature("The Lobster Professor", 1, 6, speed: 1, deploy: 1,
                "Clean your room, bucko. Then buy the seminar.",
                "Each Dawn, your strongest creature gets +1 Attack for as long as it's in play: the dominance hierarchy, as preached.",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, StrongestOne)))
                .Picture("A gaunt professor in a three-piece suit lecturing tearfully at a lectern, pointing at a projected slide of a lobster sitting on a throne, the audience taking notes, a messy bedroom visible through a door behind him."),
            Creature("Horsemen Of Apocalypse", 3, 5, speed: 2, deploy: 2,
                "Four riders. One group chat.",
                "When ready (the end was foretold): every enemy creature takes 2 damage.",
                On(EffectTrigger.OnPlay, Damage(2, TargetType.EnemyCreatures, Each)))
                .Picture("Four horsemen galloping out of storm clouds, War, Famine, Pestilence and Death, one of them checking a smartphone map, a lightning bolt behind."),
            Spell("God's Grace", TargetType.Player,
                "Not today, my child. Thursday, maybe.",
                "Die Another Day for the player, holy: Grace until the Dusk of the next Day, your Health can hit 0 and you don't lose. At that Dusk, still at 0 or less, you do. If both players fall to 0 at once, the one in Grace wins.",
                Act(ActionType.Grace, 1, TargetType.Player, TargetModifier.None))
                .Picture("A battered knight at 0 Health, a red health bar empty above his head, held upright by a beam of light from parted clouds, two cherubs propping him up by the armpits, a choir of angels singing behind."),
            Spell("Phoenix Feather", TargetType.FriendlyCreatures,
                "It burns. It rises. It invoices you for the ashes.",
                "Target friendly creature is sacrificed (its Last Will happens; a Blessing can't stop it) and rises again at the Dawn 3 Days later, full Health, Deployment Time again. Three days, as the scripture says.",
                Act(ActionType.Resurrect, 3, TargetType.FriendlyCreatures, TargetModifier.None))
                .Picture("Only the feather, watercolour on paper: red at the quill through orange to a yellow heart, its barbs licking out like flames, a glowing eye near the top, the tip burning out lilac, sparks all round."),
            Creature("The Omen", 1, 4, speed: 1, deploy: 2,
                "He's not a bad kid. He's the Antichrist.",
                "When ready: Doomed 2 on the strongest enemy creature.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Doomed, 2, TargetType.EnemyCreatures, modifier: StrongestOne)))
                .NotInStarterDeck()
                .Picture("A small boy in a neat suit on a tricycle in a long mansion hallway, staring straight at the viewer, a black dog sitting beside him and a 666 birthmark peeking from his hairline."),
        };

        // Send For: a card from the deck (of this kind, among its top cards if top > 0) joins the next hand
        private static EffectAction SendForCard(SendForFilter filter, int top = 0) {
            var action = Act(ActionType.SendFor, top, TargetType.Player, TargetModifier.None);
            action.sendFor = filter;
            return action;
        }

        // Revive: once per combat, its own Last Will raises it at the next Dawn
        private static CardEffect Revive() => OncePerCombat(On(EffectTrigger.OnDeath, Act(ActionType.Revive, 0, TargetType.Self, TargetModifier.None)));

        // Mind: wins without raw damage
        private static List<CardDef> MindCards() => new List<CardDef> {
            Spell("Lucid Dreaming", TargetType.Player,
                "You know it's a dream. You still can't find the bathroom.",
                "Mind's scouting: send for any card among the top 3 of your deck; it joins your next hand.",
                SendForCard(SendForFilter.Any, top: 3))
                .Picture("A sleeper floating above their bed in pyjamas, calmly picking cards out of a swirling dreamscape of melting clocks and staircases."),
            Spell("Mimetic Theory", TargetType.AllCreatures,
                "We want what they want. Then we become them.",
                "Mechanics test: the stat copy as a spell. Target creature copies the Attack, Health and Speed of the creature facing it.",
                CopyStats(TargetType.AllCreatures))
                .Picture("Two people facing each other in identical poses, one of them slowly turning into the other's mirror image."),
            Creature("Pessimist", 5, 5, speed: 1, deploy: 0,
                "The glass is half empty. Also, it's poisoned.",
                "Mechanics test: Contrarian. Stat changes on it are reversed: debuff it and it grows, buff it and it shrinks.")
                .With(CreatureAbility.Contrarian)
                .Picture("A man in a raincoat under his own tiny raincloud on a sunny beach, frowning at a winning lottery ticket."),
            Creature("Introvert", 4, 6, speed: 1, deploy: 0,
                "Didn't hear you. Didn't want to.",
                "Mechanics test: Unbothered. Enemy effects can't lower its stats.")
                .With(CreatureAbility.Unbothered)
                .Picture("A person in big headphones reading in a blanket fort while a loud party rages around them, the partygoers' shouted invitations bouncing off the blankets."),
            Creature("Schrodinger's Cat", 3, 3, speed: 2, deploy: 0,
                "Alive and dead. Mostly annoyed.",
                "Once per combat, when it would die, it heals 1 and survives.",
                OncePerCombat(On(EffectTrigger.OnAboutToDie, Heal(1, TargetType.Self))))
                .Picture("A cat sitting in an open cardboard box, its left half fluffy and alive, its right half a skeleton, both halves unimpressed."),
            Creature("Roko's Basilisk", 2, 6, speed: 1, deploy: 0,
                "It knows you didn't help build it. It's taking notes.",
                "Each Dawn a random enemy creature is Exhausted. No damage: Mind can't deal it.",
                On(EffectTrigger.StartOfTurn, Stun(1, TargetType.EnemyCreatures, AtRandom)))
                .Picture("A big green snake in headphones typing on a laptop, a sticky note on the screen reading NAUGHTY LIST."),
            Spell("Trolley Problem", TargetType.EnemyCreatures,
                "Pull the lever. Or don't. Either way, it's on you.",
                "Health Loss (not damage, which fits Mind): 4 on a random enemy creature, 1 on a random friendly one.",
                Buff(-4, TargetType.EnemyCreatures, attack: false, health: true, AtRandom), Buff(-1, TargetType.FriendlyCreatures, attack: false, health: true, AtRandom))
                .Picture("The trolley problem from above: a lever, a trolley and a split track, with one bored man on a track scrolling his phone."),
            Spell("Red Herring", TargetType.Player,
                "Look over there. No, the fish.",
                "A misleading Monitored Watchdog: the next enemy attack is redirected to a random creature. 1 charge.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Attack, side = WatchdogSide.Enemy, effect = WatchdogEffect.Redirect, charges = 1 }, TargetType.Enemy))
                .Picture("A red fish in a detective's trench coat and hat, pointing a fin dramatically off to the side."),
            Spell("Imposter Syndrome", TargetType.EnemyCreatures,
                "Twenty years of experience. Still waiting to be found out.",
                "-3 Attack and -1 Health for good. Future: the Imposter Syndrome status.",
                Buff(-3, TargetType.EnemyCreatures, attack: true, health: false), Buff(-1, TargetType.EnemyCreatures, attack: false, health: true))
                .Picture("A little space crewmate in a spacesuit and visor sitting at an office desk under a SENIOR ENGINEER name plate, sweating."),
            Creature("Ship of Theseus", 4, 5, speed: 1, deploy: 0,
                "Every plank replaced. Still the same ship. Probably.",
                "Regenerate 1: it repairs itself plank by plank.",
                On(EffectTrigger.EndOfTurn, Heal(1, TargetType.Self)))
                .Picture("Every plank replaced until nothing's left: the ship is only a dotted outline on the water, and Theseus in his crested helmet scratches his head on the shore, wondering where his ship went."),
            Spell("Escape Goat", TargetType.EnemyCreatures,
                "It wasn't us. It was the goat. Everyone agrees it was the goat.",
                "Heat Signature 2 on target creature (random effects and redirected attacks go to it), and draw a card. Mind deals no damage: it makes someone else's damage find the goat.",
                Status(StatusEffectType.HeatSignature, 1, TargetType.EnemyCreatures, potency: 2), Draw(1))
                .Picture("The escape goat: a white goat scrambling over a wooden fence, front legs over the top rail, looking back in a sweat."),
        };
        #endregion

        #region Utopia
        // Science: its creatures grow. Advancements raise their stats for good (gene editing, mutations, exponential growth),
        // the experiments that go too far become abominations (Frankenstein, the Chimera, the T-Rex), and Evolution Theory
        // sets Survival of the Fittest, where the weakest on the board are culled: never Utopia's, once it has grown them.
        // Pays with side effects on its own creatures. Can't conceal anything.
        // Accelerationism, its philosophy: technology solves everything, however bad it gets first. It gambles, takes its own
        // creatures to the brink of death (On the Brink: at 2 Health or less they deal double) and then the breakthrough pays.
        private static List<CardDef> UtopiaCards() => new List<CardDef> {
            Spell("Research Lab", TargetType.Player,
                "Results in five years. Funding for two.",
                "Utopia's research: send for a spell from your deck; it joins your next hand.",
                SendForCard(SendForFilter.Spell))
                .Picture("A gleaming laboratory with scientists in white coats pulling glowing cards out of test tubes and a centrifuge, a grant deadline clock ticking on the wall."),
            Creature("Clone", 2, 3, speed: 2, deploy: 1,
                "Twice the sheep. Half the soul.",
                "When it enters, summon a random creature from your deck. Future: Clone (copies a friendly creature).",
                On(EffectTrigger.OnPlay, Summon(1)))
                .Picture("Dolly the sheep in a lab coat and goggles proudly presenting a row of identical sheep behind her."),
            Creature("Hypermetamorphosis", 1, 3, speed: 1, deploy: 1,
                "Caterpillar, cocoon, middle manager. Nature is healing.",
                "+1/+1 for good each Dawn. Future: Evolution (each step unlocks an ability).",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.Self, attack: true, health: true)))
                .Picture("Left to right: a caterpillar, a cocoon, and a hatched office worker in a tie with butterfly wings, briefcase in hand, dead inside."),
            Creature("Extremophile", 1, 4, speed: 1, deploy: 1,
                "Survived five mass extinctions. Your opinion won't be the sixth.",
                "Unbothered, and it heals 1 each Dusk.",
                On(EffectTrigger.EndOfTurn, Heal(1, TargetType.Self)))
                .With(CreatureAbility.Unbothered)
                .Picture("A chubby tardigrade lounging in a pool of lava in sunglasses, a tiny cocktail in its claw."),
            Creature("Philosophy PhD", 0, 4, speed: 1, deploy: 1,
                "Eight years of Kant. Would you like fries with that?",
                "A draw each Dawn; the student debt costs it 1 Health each Dusk.",
                On(EffectTrigger.StartOfTurn, Draw(1)),
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self)))
                .NotInStarterDeck()
                .Picture("A graduate in cap and gown flipping burgers at a fast-food grill, the diploma pinned up next to the menu."),
            Creature("Mathematician", 1, 3, speed: 1, deploy: 2,
                "Showed her work. Nobody checked it.",
                "Each Dawn a random enemy creature gets -1 Attack for good.",
                On(EffectTrigger.StartOfTurn, Buff(-1, TargetType.EnemyCreatures, attack: true, health: false, AtRandom)))
                .NotInStarterDeck()
                .Picture("The confused math lady meme: a woman squinting while equations, triangles and integrals float around her head."),
            Creature("Quantum Physicist", 2, 4, speed: 2, deploy: 2,
                "In two places at once. Late to both.",
                "Swap. Whoever hits it is Exhausted: observing it collapses them.",
                On(EffectTrigger.OnDamage, Stun(1, TargetType.Enemy)))
                .With(MovementKeyword.Swap)
                .NotInStarterDeck()
                .Picture("A physicist split down the middle, one half of him see-through, holding two coffees and checking two watches."),
            Creature("Perpetual Motion Machine", 0, 6, speed: 1, deploy: 3,
                "Runs forever. The patent office stopped answering in 1911.",
                "A draw each Dawn, and it heals 1 each Dusk.",
                On(EffectTrigger.StartOfTurn, Draw(1)),
                On(EffectTrigger.EndOfTurn, Heal(1, TargetType.Self)))
                .Picture("A hamster running in a wheel that powers a fan, which blows on the hamster, which keeps running."),
            Creature("Particle Accelerator", 3, 7, speed: 1, deploy: 3,
                "Found the God particle. Lost the car keys.",
                "Each Dawn, 2 damage to a random creature on either side (side effects).",
                On(EffectTrigger.StartOfTurn, Damage(2, TargetType.AllCreatures, AtRandom)))
                .Picture("A huge ring-shaped tunnel seen from above, a tiny scientist on a bicycle racing around it after a glowing dot."),
            Creature("Genetically Modified Organism", 3, 3, speed: 2, deploy: 1,
                "Organic. Gluten-free. Bench-presses 200.",
                "+2/+2 for good when it enters; the side effects cost it 1 Health each Dusk.",
                On(EffectTrigger.OnPlay, Buff(2, TargetType.Self, attack: true, health: true)),
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self)))
                .Picture("A ripe tomato with huge bodybuilder arms, flexing, a NON-GMO sticker peeling off its skin."),
            Creature("Surveillance Giraffe", 2, 7, speed: 1, deploy: 2,
                "Heart the size of a TV. Still can't reach its own feet.",
                "When it enters, Reveal a random enemy creature's slot: it sees over the fog.",
                On(EffectTrigger.OnPlay, Reveal(TargetType.EnemyCreatures, AtRandom)))
                .NotInStarterDeck()
                .Picture("A giraffe whose neck runs out of the top of the picture and comes back in from the top further along, its head hanging upside down, peeking."),
            Creature("Pioneer", 2, 2, speed: 3, deploy: 1,
                "First human on Mars. First eviction on Mars.",
                "Leapfrog: it jumps over creatures to an empty slot.")
                .With(MovementKeyword.Leapfrog)
                .NotInStarterDeck()
                .Picture("An astronaut planting a flag on a red desert next to a sign: FOR RENT $9999."),
            Spell("Placebo", TargetType.FriendlyCreatures,
                "Clinically proven to work on people who believe it works.",
                "+3 Health for good.",
                Buff(3, TargetType.FriendlyCreatures, attack: false, health: true))
                .Picture("A sugar pill the size of a melon labelled MIRACLE on a spoonful of sugar, and a skinny man next to it flexing a non-existent bicep, very proud."),
            Spell("Vaccine", TargetType.FriendlyCreatures,
                "Side effects include not dying, and 5G.",
                "Cleanses its harmful statuses; side effect: -1 Attack for this Day (sore arm).",
                Cleanse(CleanseTarget.Statuses, TargetType.FriendlyCreatures), Buff(-1, TargetType.FriendlyCreatures, attack: true, health: false, days: 1))
                .Picture("A syringe with a tiny Wi-Fi antenna on its plunger and full signal bars above it."),
            Spell("Climate Change", TargetType.AllCreatures,
                "The scientists agreed. The weather didn't wait.",
                "Every creature is Burned for 2 Days and gets Heat Signature 1.",
                Status(StatusEffectType.Burned, 2, TargetType.AllCreatures, potency: 1, modifier: Each),
                Status(StatusEffectType.HeatSignature, 1, TargetType.AllCreatures, potency: 1, modifier: Each))
                .NotInStarterDeck()
                .Picture("A polar bear standing on a single ice cube floating in a cocktail glass, a paper umbrella beside it."),
            Spell("Overpopulation", TargetType.Player,
                "Eight billion people and nobody to cover my shift.",
                "Summon 2 random creatures from your deck.",
                Summon(2))
                .NotInStarterDeck()
                .Picture("A tiny Earth with people crammed all over it like a full elevator, one man balancing on top on one foot."),
            Spell("Black Hole", TargetType.EnemyCreatures,
                "What goes in doesn't come out. Like your tax refund.",
                "4 damage to the creature and its neighbors; 2 damage to you (side effects). Future: Black Hole repositioning.",
                Damage(4, TargetType.EnemyCreatures, TargetModifier.WithAdjacent), Damage(2, TargetType.Player, Each))
                .Picture("A swirling black hole sucking in a cow, a sofa, a single sock and a tax form."),
            Spell("Stasis Field", TargetType.FriendlyCreatures,
                "Frozen in time. Like your salary.",
                "Temporal Shield for 3 Days on a slot: the creature there takes no combat damage. Stays with the slot.",
                PlaceSlotEffect(SlotEffectType.TemporalShield, 0, 3, TargetType.FriendlyCreatures))
                .Picture("A man frozen mid-sneeze inside a giant block of green jelly, the droplets hanging in the air."),
            Spell("Absolute Zero", TargetType.AllCreatures,
                "Minus 273. The office AC, as set by Gary.",
                "Every creature is Exhausted.",
                Stun(1, TargetType.AllCreatures, Each))
                .NotInStarterDeck()
                .Picture("A man frozen solid at his desk holding a frozen cup of coffee, the thermostat on the wall reading -273."),
            Spell("Splitting The Atom", TargetType.EnemyCreatures,
                "Science's greatest achievement: a really big bang.",
                "6 damage to a random enemy creature; 2 damage to you (side effects).",
                Damage(6, TargetType.EnemyCreatures, AtRandom), Damage(2, TargetType.Player, Each))
                .Picture("A lumberjack in a flannel shirt splitting a glowing atom on a tree stump with an axe, a mushroom cloud rising behind him."),
            Spell("Gene Editing", TargetType.FriendlyCreatures,
                "Bigger, stronger, slightly radioactive.",
                "+2/+2 for good; side effect: Burned for 2 Days. Future: gene editing as Persistent Stat Modifications.",
                Buff(2, TargetType.FriendlyCreatures, attack: true, health: true), Status(StatusEffectType.Burned, 2, TargetType.FriendlyCreatures, potency: 1))
                .Picture("A pair of scissors cutting a DNA double helix, a strip of tape sticking a unicorn horn onto it."),
            GambleSpell("Accelerationism", TargetType.Player,
                "Whatever the problem, more technology. Whatever the cost, faster.",
                "Gamble, accelerationism's way: every outcome pays, one only after it hurts. Your creatures take 2 and get +3 Attack for good (on the brink, then boom), or +2/+2 for good, or Borrowed Time until the next Dusk.",
                new[] { Damage(2, TargetType.FriendlyCreatures, Each), Buff(3, TargetType.FriendlyCreatures, attack: true, health: false, Each) },
                new[] { Buff(2, TargetType.FriendlyCreatures, attack: true, health: true, Each) },
                new[] { Status(StatusEffectType.BorrowedTime, 1, TargetType.FriendlyCreatures, modifier: Each) })
                .Picture("A tech founder in a hoodie flooring the accelerator of a rocket-powered car toward a cliff edge, the road on fire behind him, a bridge of glowing circuit boards assembling itself in mid-air just ahead of the wheels."),
            Creature("Cold Fusion", 5, 6, speed: 1, deploy: 1,
                "Clean, limitless energy. Slight meltdown risk. Mostly fine.",
                "On the Brink: at 2 Health or less it deals double. The reactor runs hot: 1 damage to itself each Dusk.",
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self)))
                .With(CreatureAbility.Brink)
                .Picture("A tabletop fusion reactor glowing white-hot in a jar on a garage workbench, the warning gauge in the red, two engineers in sunglasses giving a thumbs-up."),
            Creature("Antimatter", 4, 4, speed: 2, deploy: 1,
                "One gram costs 62 trillion dollars. Worth it.",
                "On the Brink: at 2 Health or less it deals double. When ready, 2 damage to itself: it starts on the brink.",
                On(EffectTrigger.OnPlay, Damage(2, TargetType.Self)))
                .With(CreatureAbility.Brink)
                .Picture("A tiny shimmering particle in a magnetic bottle, its mirror image annihilating the lab bench around it, a physicist ducking behind a clipboard."),
            Spell("Biohacking", TargetType.FriendlyCreatures,
                "Ten thousand volts and a nootropic smoothie.",
                "Target friendly creature takes 3 damage, then gets +4 Attack for good. Too weak for it, it dies trying.",
                Damage(3, TargetType.FriendlyCreatures), Buff(4, TargetType.FriendlyCreatures, attack: true, health: false))
                .Picture("A shirtless tech bro in a garage strapped to car batteries, LED implants glowing under his skin, a smoothie in one hand and a soldering iron in the other."),
            Spell("Blood Transfusion", TargetType.FriendlyCreatures,
                "Young blood, old money.",
                "Heal target friendly creature 6: back from the brink, just in time.",
                Heal(6, TargetType.FriendlyCreatures))
                .Picture("An old billionaire in a recliner hooked up to an IV bag labelled TEEN, reading the stock prices, his skin looking twenty years younger."),
            Spell("Evolution Theory", TargetType.Player,
                "Only the fittest survive. We've been making ours fitter.",
                "Trend for 3 Days, Survival of the Fittest: each Dusk the creature with the least Health on the board, either side, takes 3. Utopia's creatures are the ones that grow.",
                SetTrend(Trend.SurvivalOfTheFittest, 3))
                .Picture("Darwin in a lab coat at a chalkboard, the classic march of progress drawn on it from ape to man to a huge muscled lab-grown superman, and a tiny weak creature at the start of the line being crossed out."),
            Spell("Mutation", TargetType.FriendlyCreatures,
                "Three extra arms. One fewer kidney.",
                "+3 Attack for good on target friendly creature; side effect: it loses 1 Health for good.",
                Buff(3, TargetType.FriendlyCreatures, attack: true, health: false), Buff(-1, TargetType.FriendlyCreatures, attack: false, health: true))
                .Picture("A lab rat in a cage flexing three muscular arms, a scientist taking notes and quietly sliding a fourth arm onto the clipboard."),
            Spell("Exponential Growth", TargetType.Player,
                "It doubles every day. That's what worries us.",
                "+1/+1 for good on each of your creatures; side effect: they're Burned for 2 Days.",
                Buff(1, TargetType.FriendlyCreatures, attack: true, health: true, Each), Status(StatusEffectType.Burned, 2, TargetType.FriendlyCreatures, potency: 1, modifier: Each))
                .Picture("A petri dish overflowing onto the lab bench, the colony spilling over the edge and climbing up the wall, a scientist backing away holding the lid."),
            Creature("Scientific Method", 1, 4, speed: 1, deploy: 1,
                "Hypothesis, experiment, grant renewal.",
                "Each Dawn, +1 Attack for good on a random friendly creature (peer reviewed).",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, AtRandom)))
                .Picture("A scientist in goggles carefully pouring a glowing liquid into a beaker on a bench crowded with labelled flasks, a whiteboard behind her reading HYPOTHESIS → EXPERIMENT → GRANT."),
            Creature("Frankenstein", 3, 6, speed: 1, deploy: 2,
                "It's alive! It's ALIVE! It's suing.",
                "An abomination that grows: +1/+1 for good each Dawn. Side effect: it arrives Burned for 2 Days (the lightning).",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Burned, 2, TargetType.Self, potency: 1)),
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.Self, attack: true, health: true)))
                .Picture("A towering stitched-together monster sitting up on a lab table under a lightning bolt, bolts in its neck smoking, the mad scientist cheering with both arms up."),
            Creature("Chimera", 4, 4, speed: 2, deploy: 1,
                "Lion, goat, snake. The ethics committee was on holiday.",
                "When ready: +2/+2 for good on a random friendly creature, maybe itself (a graft); side effect: it loses 2 Health for good.",
                On(EffectTrigger.OnPlay, Buff(2, TargetType.FriendlyCreatures, attack: true, health: true, AtRandom), Buff(-2, TargetType.Self, attack: false, health: true)))
                .Picture("A lion with a goat's head sprouting from its back and a snake for a tail, all three wearing hospital ID wristbands, in a lab enclosure with a sticky note: DO NOT FEED AFTER MIDNIGHT."),
            Creature("Resurrected T-Rex", 6, 7, speed: 1, deploy: 2,
                "Sixty-six million years asleep. Woke up hungry.",
                "A huge abomination, On the Brink. Side effect: its cloned DNA is unstable, it loses 1 Health for good each Dusk, until it hits twice as hard.",
                On(EffectTrigger.EndOfTurn, Buff(-1, TargetType.Self, attack: false, health: true)))
                .With(CreatureAbility.Brink)
                .Picture("A T-Rex skeleton half covered in new flesh breaking out of a museum display, the DNA sequencer that brought it back still beeping, a guide pointing at the exit."),
        };
        #endregion

        #region Card definitions
        private class CardDef {
            public string Name, Flavor, Notes, ArtDescription;
            public MovementKeyword Movement;
            public CreatureAbility Ability;
            public bool IsSpell;
            public bool IsGift; // Played into an empty enemy slot, it joins their side
            public int Attack, Health, Speed, DeploymentTime;
            public TargetType SpellTarget;
            public List<CardEffect> Effects;
            public int CopyCount = 1;
            public bool Extra; // Not in its faction's starter deck (it's full): in the Mechanics Test deck and the deck builder
            public bool IsToken; // Made by an effect (Ghost Army's decoys), never in a deck
            public int DiscardCost; // Additional cost: other cards from the hand, at random, given up to play it
            public bool DiscardChosen; // Paid with cards picked by hand (a calculated play) rather than at random (a chaotic one)
            public bool IsSingleUse; // Once played, gone for the rest of the combat
            public string DisguiseName; // The card the other side sees it as, until it's revealed
            public Faction? ShownFaction; // A fake card's frame: the faction it pretends to be from

            public CardDef NotInStarterDeck() {
                Extra = true;
                return this;
            }

            public string AssetName => new string(Name.Where(char.IsLetterOrDigit).ToArray());

            public CardDef Copies(int count) {
                CopyCount = count;
                return this;
            }

            public CardDef Picture(string description) {
                ArtDescription = description;
                return this;
            }

            public CardDef With(MovementKeyword movement) {
                Movement = movement;
                return this;
            }

            // Its one ability ('Abilities Compendium')
            public CardDef With(CreatureAbility ability) {
                Ability = ability;
                return this;
            }

            // A token: summoned by an effect, never in a deck, gone when it leaves play. disguise: the card the enemy sees it as
            public CardDef Token(string disguise = null) {
                IsToken = true;
                Extra = true;
                DisguiseName = disguise;
                return this;
            }

            // A fake card's face: only ever shown as a decoy's disguise, in another faction's frame
            public CardDef FakeFace(Faction looksLike) {
                Extra = true;
                ShownFaction = looksLike;
                return this;
            }

            // Single Use: once played, it's gone for the rest of the combat instead of discarded
            public CardDef SingleUse() {
                IsSingleUse = true;
                return this;
            }

            public bool IsGamble; // Only one of its effects happens, at random
            public int SacrificeCost; // Additional cost: its side's weakest creatures die when it resolves
            public int ProphecyDays; // A spell that comes true this many Days later
            public int DoomCost; // Additional cost: its side's weakest creature not yet Doomed is Doomed this many Days

            // Sacrifice N: an additional cost, its side's N weakest creatures
            public CardDef Sacrificing(int creatures) {
                SacrificeCost = creatures;
                return this;
            }

            // Doom N: an additional cost, its side's weakest creature not yet Doomed is Doomed N Days
            public CardDef Dooming(int days) {
                DoomCost = days;
                return this;
            }

            // Prophecy N: the spell comes true at the Dawn N Days after it's played
            public CardDef Foretold(int days) {
                ProphecyDays = days;
                return this;
            }

            // Discard N: an additional cost, N random other cards from the hand
            public CardDef Discarding(int cards, bool chosen = false) {
                DiscardCost = cards;
                DiscardChosen = chosen;
                return this;
            }

            // Gift: you play it into an empty enemy slot, where it joins their side
            public CardDef Gift() {
                IsGift = true;
                return this;
            }

        }

        private static CardDef Creature(string name, int attack, int health, int speed, int deploy, string flavor, string notes, params CardEffect[] effects) =>
            new CardDef {
                Name = name, Attack = attack, Health = health, Speed = speed, DeploymentTime = deploy,
                Flavor = flavor, Notes = notes, Effects = effects.ToList()
            };

        // A Gamble: each outcome is an effect of its own, and only one of them happens, at random
        private static CardDef GambleSpell(string name, TargetType aim, string flavor, string notes, params EffectAction[][] outcomes) {
            var card = Spell(name, aim, flavor, notes);
            card.Effects = outcomes.Select(actions => new CardEffect { effectType = EffectType.Triggered, trigger = EffectTrigger.OnPlay, actions = actions.ToList() }).ToList();
            card.IsGamble = true;
            return card;
        }

        // A spell has one moment, being played, so all its actions form one effect
        private static CardDef Spell(string name, TargetType aim, string flavor, string notes, params EffectAction[] actions) =>
            new CardDef {
                Name = name, IsSpell = true, SpellTarget = aim, Flavor = flavor, Notes = notes,
                Effects = new List<CardEffect> {
                    new CardEffect { effectType = EffectType.Triggered, trigger = EffectTrigger.OnPlay, actions = actions.ToList() }
                }
            };

        private static CardEffect On(EffectTrigger trigger, params EffectAction[] actions) =>
            new CardEffect { effectType = EffectType.Immediate, trigger = trigger, actions = actions.ToList() };

        private static EffectAction Act(ActionType type, int value, TargetType target, TargetModifier modifier) =>
            new EffectAction { actionType = type, value = value, targetType = target, targetModifier = modifier };

        private static EffectAction Damage(int value, TargetType target, TargetModifier modifier = TargetModifier.None) => Act(ActionType.Damage, value, target, modifier);
        // Hits back for its Attack whenever an attack lands on it: damage with no value of its own, on the attacker (see EffectResolver)
        private static CardEffect Counterattack() => On(EffectTrigger.OnDamage, Damage(0, TargetType.Enemy));
        private static EffectAction Heal(int value, TargetType target, TargetModifier modifier = TargetModifier.None) => Act(ActionType.Heal, value, target, modifier);
        private static EffectAction Armor(int value, TargetType target, TargetModifier modifier = TargetModifier.None) => Act(ActionType.Armor, value, target, modifier);
        private static EffectAction Stun(int turns, TargetType target, TargetModifier modifier = TargetModifier.None) => Act(ActionType.Stun, turns, target, modifier);
        private static EffectAction Draw(int count, TargetType player = TargetType.Player) => Act(ActionType.Draw, count, player, TargetModifier.None);
        private static EffectAction Summon(int count) => Act(ActionType.Summon, count, TargetType.Player, TargetModifier.None);

        // Kills outright, whatever its Health or Armor (Untouchable still saves it); requires: what the target must be when it lands
        private static EffectAction Destroy(TargetType target, TargetRequisite requires = TargetRequisite.None, TargetModifier modifier = TargetModifier.None) {
            var action = Act(ActionType.Destroy, 0, target, modifier);
            action.requires = requires;
            return action;
        }

        // It changes sides, to whoever's effect it is (GDD 5.4); value is the requisite's number (Attack 2 or less)
        private static EffectAction TakeControl(TargetType target, TargetModifier modifier, TargetRequisite requires = TargetRequisite.None, int value = 0) {
            var action = Act(ActionType.TakeControl, value, target, modifier);
            action.requires = requires;
            return action;
        }

        // Only lands if the target is what it requires when it lands
        private static EffectAction Requiring(EffectAction action, TargetRequisite requires) {
            action.requires = requires;
            return action;
        }

        // Decoy tokens into your empty slots; the token card is found by name when the cards are written
        private static readonly Dictionary<EffectAction, string> DecoyNames = new Dictionary<EffectAction, string>();
        private static EffectAction SummonDecoys(int count, string decoy) {
            var action = Act(ActionType.SummonDecoys, count, TargetType.Player, TargetModifier.None);
            DecoyNames[action] = decoy;
            return action;
        }

        // The creature whose effect it is attacks the target: a real attack
        private static EffectAction Strike(TargetType target, TargetModifier modifier) => Act(ActionType.Attack, 0, target, modifier);

        private static EffectAction PlaceSlotEffect(SlotEffectType type, int value, int days, TargetType target) {
            var action = Act(ActionType.PlaceSlotEffect, value, target, TargetModifier.None);
            action.slotEffect = type;
            action.statusDuration = days;
            return action;
        }

        private static EffectAction CreateWatchdog(WatchdogSpec spec, TargetType target, int days = 0) {
            var action = Act(ActionType.CreateWatchdog, 0, target, TargetModifier.None);
            action.watchdog = spec;
            action.statusDuration = days;
            return action;
        }

        private static EffectAction Cleanse(CleanseTarget what, TargetType target) {
            var action = Act(ActionType.Cleanse, 0, target, TargetModifier.None);
            action.cleanse = what;
            return action;
        }

        private static EffectAction Status(StatusEffectType status, int days, TargetType target, int potency = 0, TargetModifier modifier = TargetModifier.None) {
            var action = Act(ActionType.ApplyStatus, 0, target, modifier);
            action.statusEffectToApply = status;
            action.statusDuration = days;
            action.statusPotency = potency;
            return action;
        }

        // Lasts while the creature stays on the battlefield; negative values are debuffs
        // days: how long it lasts, counting the Day it lands (1 = this Day only); 0 = until it leaves play
        private static EffectAction Buff(int value, TargetType target, bool attack, bool health, TargetModifier modifier = TargetModifier.None, int days = 0) {
            var action = Act(ActionType.ModifyStat, value, target, modifier);
            action.modifyAttack = attack;
            action.modifyHealth = health;
            action.modifySpeed = false;
            action.statusDuration = days;
            return action;
        }

        private static CardEffect OncePerCombat(CardEffect effect) {
            effect.oncePerCombat = true;
            return effect;
        }

        private static EffectAction DelayDeployment(int days, TargetType target) => Act(ActionType.DelayDeployment, days, target, TargetModifier.None);

        // Between two creatures: on a spell, the target and the creature facing it (see EffectResolver)
        private static EffectAction CopyStats(TargetType target) => Act(ActionType.CopyStats, 0, target, TargetModifier.None);
        private static EffectAction SwapPositions(TargetType target) => Act(ActionType.SwapPositions, 0, target, TargetModifier.None);

        private static EffectAction GrantAbility(CreatureAbility ability, TargetType target) {
            var action = Act(ActionType.GrantAbility, 0, target, TargetModifier.None);
            action.ability = ability;
            return action;
        }

        // A Trend card: the whole board's rule for this many Days (GDD 4.2)
        private static EffectAction SetTrend(Trend trend, int days) {
            var action = Act(ActionType.SetTrend, 0, TargetType.Player, TargetModifier.None);
            action.trend = trend;
            action.statusDuration = days;
            return action;
        }

        // Instant: a slot (its hidden Slot Effect and its creature's plans), or a player (their Watchdog conditions)
        private static EffectAction Reveal(TargetType target, TargetModifier modifier = TargetModifier.None) => Act(ActionType.Reveal, 0, target, modifier);
        #endregion

        #region Assets
        private static DeckScriptableObject Build(Faction faction, List<CardDef> cards, Dictionary<string, CardDataScriptableObject> written) {
            WriteCards(faction, cards, written);
            var (name, list) = BestDecks[faction];
            return WriteDeck(name, list.Select(card => written[card]).ToList(), $"{DecksFolder}/{faction}Deck.asset");
        }

        private static DeckCatalog.Opponent Offer(DeckScriptableObject deck, string opponent, string tagline) =>
            new DeckCatalog.Opponent { name = opponent, tagline = tagline, deck = deck };

        // Writes the faction's cards to its folder, which then holds exactly these; returns its starter deck
        private static List<CardDataScriptableObject> WriteCards(Faction faction, List<CardDef> cards, Dictionary<string, CardDataScriptableObject> written) {
            string folder = $"{CardsRoot}/{faction}";
            CreateFolderIfNeeded(CardsRoot, faction.ToString());
            CreateFolderIfNeeded("Assets/Scriptables", "Decks");

            var deckCards = new List<CardDataScriptableObject>();
            var all = new List<CardDataScriptableObject>();
            // Fake faces first, then tokens, so the cards that point at them find them written
            foreach (var card in cards.OrderBy(c => c.ShownFaction != null ? 0 : c.IsToken ? 1 : 2)) {
                foreach (var action in card.Effects.SelectMany(e => e.actions)) {
                    if (DecoyNames.TryGetValue(action, out string decoy)) action.decoy = (CreatureCardScriptableObject)written[decoy];
                }
                var asset = WriteCard(card, faction, $"{folder}/{card.AssetName}.asset");
                if (asset is CreatureCardScriptableObject creatureAsset) {
                    creatureAsset.token = card.IsToken;
                    creatureAsset.disguise = card.DisguiseName != null ? (CreatureCardScriptableObject)written[card.DisguiseName] : null;
                    EditorUtility.SetDirty(creatureAsset);
                }
                written[card.Name] = asset;
                all.Add(asset);
                if (!card.Extra) deckCards.AddRange(Enumerable.Repeat(asset, card.CopyCount));
            }
            RemoveCardsNotIn(folder, all);
            return deckCards;
        }

        // What the menu offers: each deck with the opponent who brings it, then the test decks (see DeckCatalog)
        private static void FillCatalog(List<DeckCatalog.Opponent> offered, params DeckScriptableObject[] testDeckList) {
            var guids = AssetDatabase.FindAssets("t:DeckCatalog");
            if (guids.Length != 1) {
                Debug.LogError($"Expected one deck catalog, found {guids.Length}; the decks aren't offered in the menu.");
                return;
            }
            var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var serializedCatalog = new SerializedObject(catalog);
            var decks = serializedCatalog.FindProperty("decks");
            var opponents = serializedCatalog.FindProperty("opponents");
            decks.arraySize = offered.Count;
            opponents.arraySize = offered.Count;
            for (int i = 0; i < offered.Count; i++) {
                decks.GetArrayElementAtIndex(i).objectReferenceValue = offered[i].deck;
                var opponent = opponents.GetArrayElementAtIndex(i);
                opponent.FindPropertyRelative("name").stringValue = offered[i].name;
                opponent.FindPropertyRelative("tagline").stringValue = offered[i].tagline;
                opponent.FindPropertyRelative("deck").objectReferenceValue = offered[i].deck;
            }
            var testDecks = serializedCatalog.FindProperty("testDecks");
            testDecks.arraySize = testDeckList.Length;
            for (int i = 0; i < testDeckList.Length; i++) testDecks.GetArrayElementAtIndex(i).objectReferenceValue = testDeckList[i];
            serializedCatalog.FindProperty("cardPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CardController>(CardPrefabPath);
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static CardDataScriptableObject WriteCard(CardDef card, Faction faction, string path) {
            var asset = AssetDatabase.LoadAssetAtPath<CardDataScriptableObject>(path);
            bool isNew = asset == null;
            if (isNew) {
                // Creating the instance gives it a fresh cardId; an existing asset keeps its id
                asset = card.IsSpell
                    ? ScriptableObject.CreateInstance<SpellCardScriptableObject>()
                    : (CardDataScriptableObject)ScriptableObject.CreateInstance<CreatureCardScriptableObject>();
            }

            asset.cardName = card.Name;
            asset.description = card.Flavor;
            asset.designNotes = card.Notes;
            asset.artDescription = card.ArtDescription;
            asset.faction = card.ShownFaction ?? faction;
            asset.discardCost = card.DiscardCost;
            asset.discardChosen = card.DiscardChosen;
            asset.singleUse = card.IsSingleUse;
            asset.sacrifice = card.SacrificeCost;
            asset.doomCost = card.DoomCost;
            asset.prophecy = card.ProphecyDays;
            asset.gamble = card.IsGamble;
            asset.SetEffects(card.Effects);
            switch (asset) {
                case CreatureCardScriptableObject creature:
                    creature.attack = card.Attack;
                    creature.health = card.Health;
                    creature.speed = card.Speed;
                    creature.turnsUntilOperative = card.DeploymentTime;
                    creature.movement = card.Movement;
                    creature.ability = card.Ability;
                    creature.gift = card.IsGift;
                    break;
                case SpellCardScriptableObject spell:
                    spell.defaultTargetType = card.SpellTarget;
                    break;
            }

            if (isNew) AssetDatabase.CreateAsset(asset, path);
            else EditorUtility.SetDirty(asset);
            return asset;
        }

        private static DeckScriptableObject WriteDeck(string deckName, List<CardDataScriptableObject> cards, string path) {
            var deck = AssetDatabase.LoadAssetAtPath<DeckScriptableObject>(path);
            if (deck == null) {
                deck = ScriptableObject.CreateInstance<DeckScriptableObject>();
                AssetDatabase.CreateAsset(deck, path);
            }

            // Its cards changed (or it's new): it's the most recently updated, first in the menus
            bool changed = !deck.Cards.SequenceEqual(cards) || deck.LastUpdated == System.DateTime.MinValue;
            var serializedDeck = new SerializedObject(deck);
            serializedDeck.FindProperty("deckName").stringValue = deckName;
            if (changed) serializedDeck.FindProperty("lastUpdated").stringValue = System.DateTime.UtcNow.ToString("o");
            var cardsProp = serializedDeck.FindProperty("cards");
            cardsProp.arraySize = cards.Count;
            for (int i = 0; i < cards.Count; i++) {
                cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            }
            serializedDeck.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(deck);
            Debug.Log($"Deck '{deckName}': {cards.Count} cards ({cards.Distinct().Count()} different)");
            return deck;
        }

        // Cards dropped from the list are deleted, so the folder holds exactly the faction's deck
        private static void RemoveCardsNotIn(string folder, List<CardDataScriptableObject> deckCards) {
            foreach (var guid in AssetDatabase.FindAssets("t:CardDataScriptableObject", new[] { folder })) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (deckCards.Any(card => AssetDatabase.GetAssetPath(card) == path)) continue;
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"Removed {path}: it's no longer in the deck");
            }
        }

        private static void CreateFolderIfNeeded(string parent, string name) {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
        #endregion
    }
}
#endif
