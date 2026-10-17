#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Zeitgeist.Enums;

namespace Zeitgeist {
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
        private const TargetModifier Others = TargetModifier.Others; // Not itself (the user, 2026-10-16)
        private const TargetModifier Facing = TargetModifier.Opposite;

        [MenuItem("Cards/Generate Faction Decks")]
        public static void GenerateFactionDecks() {
            var written = new Dictionary<string, CardDataScriptableObject>();
            var offered = new List<DeckCatalog.Opponent> {
                Offer(Build(Faction.Corporate, CorporateCards(), written), "Your Manager", "Has scheduled a quick sync about your attitude."),
                Offer(Build(Faction.Statecraft, StatecraftCards(), written), "The Ministry of Forms", "Your existence is pending approval."),
                // Digital Society (the user, 2026-10-17): Zeitgeist (the culture itself) and Digital Society (the internet's) as one faction
                Offer(Build(Faction.DigitalSociety, ZeitgeistCards().Concat(DigitalSocietyCards()).ToList(), written), "Your Group Chat", "47 unread messages. All of them are about you."),
                Offer(Build(Faction.Utopia, UtopiaCards(), written), "A Billionaire Bound For Mars", "Will save humanity. Shipping not included."),
                Offer(Build(Faction.ShadowOps, ShadowOpsCards(), written), "Your Quartermaster", "Please return the car in one piece. You never do."),
                Offer(Build(Faction.Tech, TechCards(), written), "The IT Department", "Have you tried turning your strategy off and on again?"),
                Offer(Build(Faction.Myth, MythCards(), written), "The Oracle", "Saw this loss coming. Didn't warn you."),
                Offer(Build(Faction.Mind, MindCards(), written), "Your Inner Critic", "Knows exactly what to say. Always the worst thing."),
            };

            foreach (var pair in PairDecks) {
                var deck = WriteDeck(pair.Name, pair.Cards.Select(name => written[name]).ToList(), $"{DecksFolder}/{pair.File}.asset", pair.Strategy);
                offered.Add(Offer(deck, pair.Opponent, pair.Tagline));
            }

            WriteCards(Faction.None, PotionCards(), written); // An adventure's potions, in no deck
            var potions = PotionCards().Select(p => written[p.Name]).ToList();
            var testDeck = WriteDeck("Mechanics Test", MechanicsTestCards.Select(name => written[name]).ToList(), $"{DecksFolder}/MechanicsTestDeck.asset");
            var workbench = WriteDeck(WorkbenchName, WorkbenchCards().Select(name => written[name]).ToList(), $"{DecksFolder}/WorkbenchDeck.asset");
            FillCatalog(offered, potions, workbench, testDeck);
            SetFocus(offered.Select(o => o.deck).Append(workbench).Append(testDeck));
            AssetDatabase.SaveAssets();
            Debug.Log("Generated the faction decks, the Workbench and the Mechanics Test deck. Run Cards > Assign Card Art to link their art.");
        }

        // The cards being worked on now, to test in a match: two of each, filled up to 20 with their faction's best deck. It's
        // first in the menus. Change it with the work
        // Here (the user, 2026-10-17): the second Pokémon batch, the ability creatures and the Destiny Bonds
        private static readonly (Faction Faction, string[] Cards) Workbench = (Faction.Statecraft, new[] {
            "Legal Department", "Civil Servant", "Bomb Squad", "Sleepwalker", "Riot Police", "One-Trick Pony", "Drill Sergeant", "Clingy Ex",
            "Empath", "Berserker", "Backup Dancer", "EMP Drone", "Tutankhamun", "Teflon Don", "Sapper", "Anti-Vaxxer",
            "Mutually Assured Destruction", "Holy Pigeon", "Tithe", "Job Rotation", "Drone Operator" });
        private static string WorkbenchName => Workbench.Cards.Length <= 3 ? $"Workbench - {string.Join(", ", Workbench.Cards)}" : "Workbench - New Cards";

        // The decks being worked on now, the most important first: the menus list them right after the Workbench, in this
        // order, then the rest by when their cards last changed. Change it with the work
        private static readonly string[] Focus = { "Digital Society - Terminally Online", "Corporate - Shareholder Value", "Myth - Old Gods, New Tricks", "Digital Society - Moods of Modern Life" };

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
        private static readonly (string Name, string File, string Opponent, string Tagline, string Strategy, string[] Cards)[] PairDecks = {
            // A second deck for each faction, another strategy than its best one (the user, 2026-10-16: decks combining different
            // strategies, then a tournament)
            // The Maneuvers (2026-10-17) joined each faction's second deck, two copies each: Valet, Mass Emigration, Tourist Trap,
            // Heimlich Maneuver, Domino Effect and Controlled Chaos.
            // Black Budget, Hustle Rush, Lab Grown, Threat Model and Blessed Martyrs rebuilt 2026-10-17 from the balance study's card
            // numbers (Faction Balance Plan, step 10 of the next picks): each keeps its strategy, 8 creatures and 7 spells, two of each
            ("Corporate - Hustle Rush", "CorporateRushDeck", "Your Startup's Founder", "Moving fast. Breaking you.", "Aggro", new[] { "Micromanager", "Micromanager", "Cleaning Robot", "Cleaning Robot", "Multilevel Marketing Seller", "Multilevel Marketing Seller", "Bloomberg Terminal", "Bloomberg Terminal", "Charles Ponzi", "Charles Ponzi", "Day Trader", "Day Trader", "Middle Manager", "Middle Manager", "Shell Company", "Shell Company", "Flash Crash", "Flash Crash", "Hostile Takeover", "Hostile Takeover", "Team Building Exercise", "Team Building Exercise", "High-Frequency Trading Scam", "High-Frequency Trading Scam", "Inheritance", "Inheritance", "Venture Capital", "Venture Capital", "Valet", "Valet", "DMCA", "DMCA", "3rd Parallel Job", "3rd Parallel Job", "Legal Department", "Legal Department", "Job Rotation", "Job Rotation", "Resignation Letter", "Resignation Letter", "Teflon Don", "Teflon Don", "Insurance Policy", "Insurance Policy", "Seniority Priority", "Seniority Priority" }),
            ("Statecraft - Shock and Awe", "StatecraftAggroDeck", "The Minister of Defence", "Peace through superior firepower.", "Aggro", new[] { "Mercenaries", "Mercenaries", "Sherman Tank", "Sherman Tank", "Police Officer Productivity Target", "Police Officer Productivity Target", "Spite-Elected President", "Spite-Elected President", "Demagogue", "Demagogue", "Man o' War", "Man o' War", "Preemptive Strike", "Preemptive Strike", "Raised Security Level", "Raised Security Level", "Martial Law", "Martial Law", "Panem Et Circenses", "Weapons of Mass Distraction", "Security Guard", "Security Guard", "Mass Emigration", "Mass Emigration", "Filibuster", "Filibuster", "Curfew", "Curfew", "Spin Doctor", "Spin Doctor", "Civil Servant", "Civil Servant", "Bomb Squad", "Bomb Squad", "Riot Police", "Riot Police", "Drill Sergeant", "Drill Sergeant", "Sapper", "Sapper", "Mutually Assured Destruction", "Mutually Assured Destruction", "Due Process", "Due Process" }),
            ("Digital Society - Terminally Online", "TerminallyOnlineDeck", "Your Feed", "Curated just for you. Every post is about how wrong you are.", "Swarm", new[] { "Clickbait", "Clickbait", "Dogpile", "Dogpile", "Keyboard Warrior", "Keyboard Warrior", "NPC", "NPC", "Crypto \"Entrepreneur\"", "Crypto \"Entrepreneur\"", "Digital Nomad", "Digital Nomad", "Meta Group Chat", "Meta Group Chat", "Conspiracy Theorist", "Conspiracy Theorist", "Rage Bait", "Cancelled", "Social Panopticon", "Chronically Online" }), // Was Digital Society's best deck (2026-10-17)
            ("Digital Society - Underdogs", "ZeitgeistSwarmDeck", "Your Neighbourhood", "Nobody believed in them. Correctly.", "Swarm", new[] { "Underdog", "Underdog", "Plot Armor", "Plot Armor", "Scam Artist", "Scam Artist", "Delivery Cyclist", "Delivery Cyclist", "Drug Smuggler", "Drug Smuggler", "Hikikomori", "Hikikomori", "Not on My Watch", "Karen", "Karen", "No Glove, No Love", "No Glove, No Love", "One for the Road", "One for the Road", "Rush Hour", "Generational Trauma", "Generational Trauma", "Mental Health Day", "YOLO", "The Friends We Made Along the Way", "The Friends We Made Along the Way", "Absentee Parents", "Absentee Parents", "Ol' Reliable", "Ol' Reliable", "Weaponized Incompetence", "Listen Kid", "Listen Kid", "Lost Generation", "Lost Generation", "Spite Voter", "Spite Voter", "Camper Van", "Universal Basic Income", "Calm Your Tits", "Tourist Trap", "Groundhog Day", "Freaky Friday", "Florida Man", "One-Trick Pony", "Clingy Ex", "Backup Dancer", "Anti-Vaxxer" }),
            ("Utopia - Lab Grown", "UtopiaEngineDeck", "The Gene Therapist", "Side effects may include winning.", "Engine", new[] { "Quark", "Quark", "Ankylorhiza", "Ankylorhiza", "Particle Accelerator", "Particle Accelerator", "Cold Fusion", "Cold Fusion", "Hypermetamorphosis", "Hypermetamorphosis", "Resurrected T-Rex", "Resurrected T-Rex", "Antimatter", "Antimatter", "Biohacker", "Biohacker", "Biohacking", "Biohacking", "Mutation", "Mutation", "Exponential Growth", "Exponential Growth", "Seasteading", "Seasteading", "Blood Transfusion", "Blood Transfusion", "Stasis Field", "Stasis Field", "Gene Editing", "Gene Editing", "Heimlich Maneuver", "Heimlich Maneuver", "Blood Transfusion", "Blood Transfusion", "Peer Review", "Peer Review" }),
            ("Digital Society - Influencer Economy", "DigitalSocietyMidrangeDeck", "Your Favourite Streamer", "Like, subscribe, surrender.", "Midrange", new[] { "Thirst Trap", "Thirst Trap", "Content Creator", "Content Creator", "Short King", "Short King", "Good Boi", "Good Boi", "Its Been 84 Years...", "Its Been 84 Years...", "Catfish", "Catfish", "Meta Group Chat", "Meta Group Chat", "Cancelled", "Cancelled", "Social Panopticon", "Social Panopticon", "Its A Trap", "Dogpile", "Dogpile", "Console Memory Card", "Selfie Esteem", "Selfie Esteem", "Influencer", "Influencer", "U Mad Bro?", "U Mad Bro?" }),
            ("Shadow Ops. - Black Budget", "ShadowOpsControlDeck", "The Station Chief", "This meeting never happened.", "Control", new[] { "Embassy", "Embassy", "Mole", "Mole", "Arsonist", "Arsonist", "Cleaner", "Cleaner", "Compromised Agent", "Compromised Agent", "Political Decoy", "Political Decoy", "Control Room", "Control Room", "Signal Jammer", "Signal Jammer", "Counter-Surveillance", "Counter-Surveillance", "Supply Chain Attack", "Supply Chain Attack", "Secret Anthrax Laboratory", "Secret Anthrax Laboratory", "Human Intelligence", "Human Intelligence", "Sabotage", "Sabotage", "Perfectly Timed Camera Malfunction", "Perfectly Timed Camera Malfunction", "Tracking Beacon", "Tracking Beacon", "Plausible Deniability", "Plausible Deniability", "Hostage Situation", "Hostage Situation" }),
            ("Tech - Threat Model", "TechComboDeck", "The Red Team", "We found 47 vulnerabilities. You are 46 of them.", "Combo", new[] { "Suspicious USB Stick in the Parking Lot", "Suspicious USB Stick in the Parking Lot", "Technical Debt", "Technical Debt", "Scope Creep", "Scope Creep", "SysAdmin", "SysAdmin", "Threat Hunter", "Threat Hunter", "Script Kiddie", "Script Kiddie", "Legacy Code", "Legacy Code", "Sex Clanker", "Sex Clanker", "Ransomware Attack", "Ransomware Attack", "Code Review", "Code Review", "Ctrl + Y", "Ctrl + Y", "Man-in-the-Middle", "Man-in-the-Middle", "Antivirus", "Antivirus", "DDOS Attack", "DDOS Attack", "Pair Programming", "Pair Programming", "EMP Drone", "EMP Drone", "Buffering", "Buffering" }),
            ("Myth - Blessed Martyrs", "MythEngineDeck", "The Abbess", "Blessed are the meek. Briefly.", "Engine", new[] { "Pyramids", "Pyramids", "Heretic", "Heretic", "Reincarnation", "Reincarnation", "Black Knight", "Black Knight", "Cultist", "Cultist", "Here Be Dragons", "Here Be Dragons", "Buddy Christ", "Buddy Christ", "Zealot", "Zealot", "Deus Vult", "Deus Vult", "Gracias Jesús", "Gracias Jesús", "Mithril", "Mithril", "Ambrosia", "Ambrosia", "Excess of Bread and Wine", "Excess of Bread and Wine", "Monkey Hand", "Monkey Hand", "Publicly Funded Faith Schools", "Publicly Funded Faith Schools", "Thoughts and Prayers", "Thoughts and Prayers", "Holy Ghost", "Holy Ghost", "Tithe", "Tithe", "Berserker", "Berserker", "Tutankhamun", "Tutankhamun", "Holy Pigeon", "Holy Pigeon", "The Last Shall Be First", "The Last Shall Be First" }),
            ("Mind - Mind Games", "MindControlDeck", "Your Therapist", "And how does losing make you feel?", "Control", new[] { "Socrates", "Socrates", "Roko's Basilisk", "Roko's Basilisk", "Schrodinger's Cat", "Schrodinger's Cat", "Introvert", "Introvert", "Ship of Theseus", "Ship of Theseus", "Legerdemain", "Legerdemain", "Maslow's Pyramid", "Maslow's Pyramid", "Yesn't", "Yesn't", "Red Herring", "Red Herring", "Survivorship Bias", "Momentum", "Trolley Problem", "Trolley Problem", "Lucid Dreaming", "Hold That Thought", "Hold That Thought", "Psychiatrists", "Psychiatrists", "Projection", "Hyperbole", "Hyperbole", "Domino Effect", "Domino Effect", "Controlled Chaos", "Controlled Chaos", "Liminal Space", "Liminal Space", "Monkey See, Monkey Do", "Monkey See, Monkey Do", "Sleepwalker", "Sleepwalker", "Empath", "Empath", "Tabula Rasa", "Tabula Rasa", "Reality Check", "Reality Check", "Slow Thinking", "Slow Thinking" }),
            // Two factions that play well together (Faction Compendium 5, Example Two-Faction Decks)
            ("Myth + Corporate - Prosperity Gospel", "ProsperityGospelDeck", "The Televangelist", "Your donation is tax-deductible. Your soul is not.", "Combo", new[] { "Sacrificial Ritual", "Sacrificial Ritual", "Omen Reading", "Necronomicon", "Necronomicon", "Cultist", "Cultist", "Unpaid Intern", "Unpaid Intern", "Golden Parachute", "Golden Parachute", "Charles Ponzi", "Day Trader", "Day Trader", "Fat Tony", "Fat Tony", "Martyr", "Martyr", "Tax-Exempt Corruption", "Monkey Hand", "Holy War", "Holy War", "Hostile Takeover", "Hostile Takeover" }),
            ("Digital Society + Shadow Ops. - Panopticon", "PanopticonDeck", "The Data Broker", "We know what you did. We sold it.", "Combo", new[] { "Meta Group Chat", "Meta Group Chat", "Social Panopticon", "Social Panopticon", "Cancelled", "Cancelled", "Control Room", "Control Room", "Wet Work", "Wet Work", "Perfectly Timed Camera Malfunction", "Perfectly Timed Camera Malfunction", "Tapped Communications", "Keyboard Warrior", "Keyboard Warrior", "Chronically Online", "Chronically Online", "Licence to Kill", "Licence to Kill", "Cleaner", "Cleaner", "Dogpile", "Dogpile" }),
            ("Corporate + Digital Society - Hustle Culture", "HustleCultureDeck", "The Motivational Speaker", "Rise and grind. Mostly grind.", "Aggro", new[] { "Day Trader", "Day Trader", "Pluriemployed Hustler", "Pluriemployed Hustler", "Bodybuilder", "Bodybuilder", "Football Hooligans", "Football Hooligans", "Reality TV Star", "Reality TV Star", "Antihero", "Antihero", "Overtime", "Overtime", "YOLO", "YOLO", "Toxic Positivity", "Toxic Positivity", "Hostile Takeover", "Hostile Takeover", "Team Building Exercise", "Team Building Exercise", "Mass Layoffs", "Business Expense" }),
            ("Statecraft + Corporate - Military-Industrial Complex", "MilitaryIndustrialDeck", "The Defence Contractor", "Peace is bad for business.", "Midrange", new[] { "Man o' War", "Man o' War", "Diplomat", "Diplomat", "Sherman Tank", "Sherman Tank", "Fat Tony", "Fat Tony", "Cleaning Robot", "Cleaning Robot", "Mercenaries", "Mercenaries", "Preemptive Strike", "Preemptive Strike", "Inflation", "Inflation", "Margin Call", "Margin Call", "Raised Security Level", "Supply Depot", "Taxpayer Funds Venture Capital Profits", "Taxpayer Funds Venture Capital Profits" }),
            ("Utopia + Myth - Frankenstein", "FrankensteinDeck", "The Mad Scientist", "It's alive. Again. And again.", "Engine", new[] { "Living Dead", "Living Dead", "Phoenix", "Phoenix", "Reincarnation", "Reincarnation", "Frankenstein", "Frankenstein", "Genetically Modified Organism", "Genetically Modified Organism", "Gene Editing", "Gene Editing", "Mutation", "Mutation", "Phoenix Feather", "Phoenix Feather", "Exponential Growth", "The Holy Grail", "Templar Knight", "Templar Knight", "Resurrected T-Rex", "Resurrected T-Rex" }),
            ("Tech + Digital Society - The Algorithm", "AlgorithmDeck", "The Recommendation Engine", "Because you watched: losing.", "Swarm", new[] { "Script Kiddie", "Script Kiddie", "NPC", "NPC", "Short King", "Short King", "Digital Nomad", "Digital Nomad", "Threat Hunter", "Threat Hunter", "Crypto \"Entrepreneur\"", "Crypto \"Entrepreneur\"", "Pair Programming", "Pair Programming", "Clickbait", "Clickbait", "Dogpile", "Dogpile", "DDOS Attack", "DDOS Attack", "Social Panopticon", "Ransomware Attack" }),
            ("Shadow Ops. + Mind - Psyop", "PsyopDeck", "The Handler", "You were never here. Neither were we.", "Control", new[] { "Femme Fatale", "Femme Fatale", "Deep Cover", "Deep Cover", "Roko's Basilisk", "Roko's Basilisk", "Introvert", "Introvert", "Red Herring", "Red Herring", "Counter-Intelligence", "Counter-Intelligence", "Sabotage", "Legerdemain", "Legerdemain", "Imposter Syndrome", "Imposter Syndrome", "Smoke And Mirrors", "Trolley Problem", "Trolley Problem", "The Golden Gun" }),
        };

        // The strategy each faction's best deck plays, for the balance study's strategy table
        private static readonly Dictionary<Faction, string> BestDeckStrategies = new Dictionary<Faction, string> {
            [Faction.Corporate] = "Engine", [Faction.Statecraft] = "Control", [Faction.DigitalSociety] = "Midrange", [Faction.Utopia] = "Aggro",
            [Faction.ShadowOps] = "Aggro", [Faction.Tech] = "Midrange", [Faction.Myth] = "Control",
            [Faction.Mind] = "Control",
        };

        // Each faction's deck (2026-10-14): its best 20 cards and combos, picked from the AI games' card stats (damage, kills,
        // win share per card), before any balance study. Two copies at most. The old decks (and the two-faction ones) are gone
        private static readonly Dictionary<Faction, (string Name, string[] Cards)> BestDecks = new Dictionary<Faction, (string, string[])> {
            // Board-wide damage that draws cards, removal, and bodies that grow
            [Faction.Corporate] = ("Corporate - Shareholder Value", new[] {
                "Margin Call", "Margin Call", "Hostile Takeover", "Hostile Takeover", "Inflation", "Inflation", "Dead Cat Bounce", "Dead Cat Bounce",
                "Taxpayer Funds Venture Capital Profits", "Taxpayer Funds Venture Capital Profits", "Team Building Exercise", "Casino",
                "Fat Tony", "Fat Tony", "Cleaning Robot", "Cleaning Robot", "Day Trader", "Micromanager", "Charles Ponzi", "Pluriemployed Hustler",
                "Peace the Frontier",
                // Added 2026-10-16 (the balance plan): what its decks needed most
                "Hedonic Treadmillionaire", "Hedonic Treadmillionaire", "Moral Bankruptcy", "Moral Bankruptcy", "Flash Crash",
                // Added 2026-10-16 (the meta picks): the new mechanics' cards that fit this deck
                "Usury", "Usury", "Big Four", "Big Four",
            }),
            // Big bodies that buff each other on arrival, and Mutually Assured Destruction that they survive
            // Its control list: big bodies that buff each other, mines, shields and paperwork, and Mutually Assured Destruction
            [Faction.Statecraft] = ("Statecraft - Order & Paperwork", new[] {
                "Doomsday Clock", "Preemptive Strike", "Lost Landmine", "Ceasefire", "Red Tape", "Raised Security Level",
                "Panem Et Circenses", "Martial Law", "Supply Depot", "Man o' War", "Diplomat", "Spite-Elected President", "Supreme Court",
                "Demagogue", "King Emeritus", "Ministry for Equality", "Mercenaries", "Police Officer Productivity Target",
                "Protocol Enforcers", "Internal Revenue Service",
                // Added 2026-10-16 (the meta picks): the new mechanics' cards that fit this deck
                "Border Crossing", "Border Crossing", "Peace Force", "Peace Force", "Si Vis Pacem, Para Bellum",
            }),
            // Chaos on both boards that its sturdy creatures shrug off, and Double Tap on a big hitter
            [Faction.DigitalSociety] = ("Digital Society - Moods of Modern Life", new[] {
                "This Is Fine", "This Is Fine", "Russian Roulette", "Russian Roulette", "Football Hooligans", "Football Hooligans",
                "Hipster", "Hipster", "Boomer", "Boomer", "Karen", "Karen", "Antihero", "Antihero", "Bodybuilder", "Bodybuilder",
                "Chain Smoker", "Reality TV Star", "Toxic Positivity", "Toxic Positivity",
                // Added 2026-10-16 (the balance plan): what its decks needed most
                "Ol' Reliable", "Ol' Reliable", "Listen Kid", "Listen Kid",
                // Added 2026-10-16 (the meta picks): the new mechanics' cards that fit this deck
                "Fidget Spinner", "Fidget Spinner",
            }),
            // Accelerationism: creatures taken to the brink, then boom; Black Hole and Splitting The Atom clear the way
            [Faction.Utopia] = ("Utopia - Results May Vary", new[] {
                "Accelerationism", "Accelerationism", "Black Hole", "Black Hole", "Biohacking", "Biohacking", "Splitting The Atom", "Splitting The Atom",
                "Cold Fusion", "Cold Fusion", "Antimatter", "Antimatter", "Resurrected T-Rex", "Resurrected T-Rex",
                "Blood Transfusion", "Evolution Theory", "Scientific Method", "Particle Accelerator", "Chimera", "Frankenstein",
                // Added 2026-10-16 (the balance plan): what its decks needed most
                "Femtosecond Laser", "Femtosecond Laser", "Longevity Hub", "Longevity Hub", "Ankylorhiza",
                // Added 2026-10-16 (the meta picks): the new mechanics' cards that fit this deck
                "Biohacker", "Biohacker", "Prion Disease", "Prion Disease",
            }),
            // Double-O Section's agents and gadgets with Black Budget's best: Ambush, licences, Last Wills that kill
            [Faction.ShadowOps] = ("Shadow Ops. - Licence to Kill", new[] {
                "Femme Fatale", "Femme Fatale", "If I Told You, I'd Have to Kill You", "If I Told You, I'd Have to Kill You",
                "Licence to Kill", "Licence to Kill", "Cleaner", "Cleaner", "MK Ultra Test Subject", "MK Ultra Test Subject",
                "Dead Man's Switch", "Dead Man's Switch", "Die Another Day", "Shaken, Not Stirred", "The Golden Gun", "False Flag Operation",
                "GoldenEye", "Supply Chain Attack", "Wet Work", "Human Intelligence",
                // Added 2026-10-16 (the balance plan): what its decks needed most
                "Exploding Pen", "Exploding Pen", "Arsonist", "Arsonist", "Secret Anthrax Laboratory", "Drug Pigeon", "Drug Pigeon",
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
                "Holy Hand Grenade of Antioch", "The Chosen One", "Buddy Christ", "Living Dead", "Necronomicon", "Phoenix Feather", "Phoenix Feather",
                "Pyramids", "Amen",
                // Added 2026-10-16 (the balance plan): what its decks needed most
                "Grim Reaper", "Grim Reaper", "Die by the Sword", "Die by the Sword", "Here Be Dragons",
            }),
            // Sturdy creatures that wear the enemy down, and stat cuts that kill
            [Faction.Mind] = ("Mind - All in Your Head", new[] {
                "Pessimist", "Pessimist", "Introvert", "Introvert", "Ship of Theseus", "Ship of Theseus", "Reflect", "Cyberpsychosis",
                "Trolley Problem", "Trolley Problem", "Roko's Basilisk", "Roko's Basilisk", "Imposter Syndrome", "Imposter Syndrome",
                "Schrodinger's Cat", "Schrodinger's Cat", "Mimetic Theory", "Mimetic Theory", "Lucid Dreaming", "Red Herring",
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
            // The second Pokémon batch (the user, 2026-10-17): Rocky Helmet, Skill Swap, Memento, Good as Gold, Destiny Bond, Trick Room
            Creature("Legal Department", 2, 7, speed: 1, deploy: 0,
                "You'll be hearing from us. Repeatedly. At your home address.",
                "Litigious (Pokémon's Rocky Helmet): a creature that attacks it takes 2 damage. A wall that sues.")
                .With(CreatureAbility.Litigious)
                .Picture("A phalanx of identical lawyers in grey suits behind a long desk, each holding up a thick folder like a riot shield, a fist that punched the desk recoiling with a CEASE AND DESIST stapled to it."),
            Spell("Job Rotation", TargetType.AllCreatures,
                "Everyone will now be doing someone else's job, badly. It builds character.",
                "Skill Swap: target creature and the creature facing it swap abilities, for the combat. Their Signal Jammer on your Bodyguard's side of the table, and the other way round.",
                Act(ActionType.SwapAbilities, 0, TargetType.AllCreatures, TargetModifier.None))
                .Picture("An office where the surgeon in scrubs is fixing the printer, the IT guy in a hoodie is holding a scalpel over a patient, and the HR woman is piloting a forklift through the open-plan floor, an EMPLOYEE ROTATION WEEK banner above them."),
            Spell("Resignation Letter", TargetType.EnemyCreatures,
                "I quit. Effective immediately. Also I took the client list.",
                "Memento: your weakest creature is sacrificed (an additional cost), and target enemy creature gets -3 Attack until it leaves play. Its exit takes the wind out of them.",
                Buff(-3, TargetType.EnemyCreatures, attack: true, health: false))
                .Sacrificing(1)
                .Picture("A man walking out of a glass tower with a cardboard box of his things and a huge grin, a letter fluttering behind him, while through the window his former boss sits slumped at a desk buried under the work he left."),
            Creature("Teflon Don", 4, 5, speed: 2, deploy: 1,
                "Charged forty times. Convicted of being fabulous.",
                "Teflon (Pokémon's Good as Gold): enemy statuses don't land on it. Every Mafia theme is Corporate: legality is a business with a high entry price.")
                .With(CreatureAbility.Teflon)
                .Picture("A silver-haired mob boss in a camel coat and pinstripes strolling out of a courthouse waving to cameras, indictment papers sliding off his coat like water off a non-stick pan, two lawyers scooping them up behind him."),
            Spell("Insurance Policy", TargetType.FriendlyCreatures,
                "In the event of death, the beneficiary receives the killer.",
                "Destiny Bond: Mutually Assured for 3 Days on target friendly creature: the creature that kills it is destroyed too. The policy pays out in kind.",
                Status(StatusEffectType.MutuallyAssured, 3, TargetType.FriendlyCreatures))
                .Picture("An insurance agent in a beige suit sliding a policy across the desk to a nervous creature, the fine print magnified under a glass reading WHOEVER KILLS YOU, WE KILL, a tiny coffin-shaped paperweight on the desk."),
            Spell("Seniority Priority", TargetType.Player,
                "The slowest to adapt go first. It's called experience.",
                "Trend for 3 Days (Trick Room, Corporate's flavour): Rush Hour, the slowest act first.",
                SetTrend(Trend.RushHour, 3))
                .Picture("A boardroom where a very old executive with a walking frame is being ushered to the head of the table past a queue of young employees frozen mid-stride, a plaque on the wall reading 40 YEARS OF SERVICE."),
            // The Pokémon picks (the user, 2026-10-17): Imprison as the DMCA, Belly Drum as a third job
            Spell("DMCA", TargetType.EnemyCreatures,
                "This creature has been claimed by its rights holder. Your appeal has been automatically denied.",
                "Imprison (the user, 2026-10-17): target enemy creature is destroyed (taken down), and for 2 Days the enemy can't play a card that shares its name with a creature on the board, either side's (every deck runs two copies: the second waits while a copy stands).",
                Destroy(TargetType.EnemyCreatures), Act(ActionType.Seal, 2, TargetType.Enemy, TargetModifier.None))
                .Picture("A lawyer in a sharp suit slapping a huge red COPYRIGHT STRIKE sticker over the face of a creature on a giant video thumbnail, a second identical thumbnail beside it greyed out with CLAIMED across it, a tiny APPEAL button crossed out."),
            Spell("3rd Parallel Job", TargetType.FriendlyCreatures,
                "Sleep is for people with one income.",
                "Belly Drum (the user, 2026-10-17): target friendly creature loses half its Health and its Attack doubles. The hustle: its Health for a monster.",
                Act(ActionType.DoubleDown, 0, TargetType.FriendlyCreatures, TargetModifier.None))
                .Picture("A hollow-eyed man at three desks pushed together, a laptop and a different lanyard on each, three video calls on mute, an IV drip of coffee into his arm, the wall clock at 3:00, his biceps absurdly huge from typing."),
            Spell("Overtime", TargetType.FriendlyCreatures,
                "Just this once. Every week.",
                "The user, 2026-10-16: a creature still deploying of yours comes in up to 2 Days sooner, and pays for it with Burnout: it's destroyed at the end of the first Day it's ready, so it gets that one Day to act. A Cleanse saves it. Corporate's tempo: a big body now, for one Day.",
                Requiring(Act(ActionType.Rush, 2, TargetType.FriendlyCreatures, TargetModifier.None), TargetRequisite.Deploying))
                .Picture("An office worker at a desk late at night, surrounded by empty coffee cups and energy drink cans, typing furiously with smoke rising from the keyboard, eyes bloodshot and wide open, a wall clock showing 3 a.m. and a calendar with every weekend crossed out."),
            Spell("Peace the Frontier", TargetType.Enemy,
                "Please, regulate us. Only us can afford it. Also, could someone lend us a few billion?",
                "The user, 2026-10-16: frontier AI labs lobbying the government for rules strict enough that no newcomer can compete, while they burn through cash and play the victim. Regulatory capture: every enemy creature still deploying waits 1 more Day; the lobbying isn't free, you take 2 damage.",
                Requiring(Act(ActionType.DelayDeployment, 1, TargetType.EnemyCreatures, Each), TargetRequisite.Deploying),
                Damage(2, TargetType.Player))
                .Picture("Two tech executives in hoodies at a congressional hearing table, one tearfully holding up a sign reading REGULATE US (ONLY), the other quietly sliding a rulebook titled BARRIERS TO ENTRY across to a smiling senator, a burning pile of cash under the table."),
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
            Creature("Counterfeit", 1, 2, speed: 2, deploy: 0,
                "Genuine replica. 100% authentic copy.",
                "Mechanics test: Wannabe (Active, hold Shift as you drag it onto a creature): its Attack, Health and Speed become that creature's.")
                .With(CreatureAbility.Wannabe).NotInStarterDeck()
                .Picture("A knockoff superhero in a cheap costume with a misspelled logo on the chest, flexing proudly in front of a mirror that shows a real muscled hero."),
            Creature("Private Equity", 3, 4, speed: 1, deploy: 1,
                "We don't buy companies. We buy what they used to be worth.",
                "Mechanics test: Credit Grab (Active, once per combat): steals target creature's stat raises. Buff an enemy, or let them, then take it.")
                .With(CreatureAbility.CreditGrab).NotInStarterDeck()
                .Picture("A vampire banker in a pinstripe suit sucking gold coins out of a little family shop through a long straw."),
            Creature("Shell Company", 1, 4, speed: 2, deploy: 0,
                "Registered in four countries. Exists in none.",
                "Mechanics test: Shell Game (Active, hold Shift as you drag it onto a creature): that creature trades places with the one to its right. Moves an enemy into a planned attack, or out of the way of one.")
                .With(CreatureAbility.ShellGame).NotInStarterDeck()
                .Picture("A street hustler playing the shell game with three walnut shells, each stamped with a different tax haven's flag, a gold coin under one."),
            Creature("Golden Parachute", 3, 3, speed: 1, deploy: 0,
                "Ousted with a forty-million bonus. Failing upward, gently.",
                "Replaced (the user, 2026-10-15: a new trigger, being replaced isn't dying): when a summon takes its slot, you draw 2. The executive leaves with the payout; send in the next one.",
                On(EffectTrigger.OnReplaced, Draw(2)))
                .Picture("A grinning CEO floating down on a golden parachute stitched from banknotes, a briefcase overflowing with cash in hand, while the company tower he leaves behind burns and the staff watch from the windows."),
            Creature("Work Shift Rotation", 3, 3, speed: 1, deploy: 1,
                "Same job, new face. The vest is still warm.",
                "Handover: when a summon replaces it, the newcomer takes over its stat changes and statuses. Buff it, then send in the next shift.")
                .With(CreatureAbility.Handover)
                .Picture("Two warehouse workers at the clocking-in machine: the exhausted one hands his hi-vis vest, still warm, to the fresh one."),
            Creature("Unpaid Intern", 1, 2, speed: 3, deploy: 0,
                "Paid in experience. The experience is this.",
                "Future: a Worker token created by other cards rather than drafted. Fodder for sacrifice costs.",
                On(EffectTrigger.OnDeath, Draw(1))),
            Creature("Inhuman Resources", 1, 4, speed: 1, deploy: 1,
                "We're here for you. \"You\" meaning the company.",
                "Future: Layoffs. Destroy a friendly Worker as an additional cost; schedule 2 draws.",
                On(EffectTrigger.StartOfTurn, Damage(1, TargetType.FriendlyCreatures, AtRandom), Draw(1))),
            Creature("Micromanager", 3, 3, speed: 1, deploy: 1,
                "Per my last email. And the one before. And the meeting about the emails.",
                "Future: gives a friendly Worker Coffeinated instead of a plain attack buff.",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, AtRandom | Others))),
            Creature("Multilevel Marketing Seller", 2, 2, speed: 2, deploy: 0,
                "It's not a pyramid. It's a triangle of opportunity.",
                "Future: summons an Unpaid Intern token. Each recruit pays a share of its draws upward.",
                On(EffectTrigger.OnPlay, Summon(1))),
            Creature("Day Trader", 3, 2, speed: 3, deploy: 0,
                "Time is money, friend. Up 300% by noon, rent due at five.",
                "Insider Job (insider trading): its damage ignores Armor. Future: Investment. Pays out or loses cards depending on the Day.",
                On(EffectTrigger.OnPlay, Draw(1)),
                On(EffectTrigger.OnDeath, Draw(1, TargetType.Enemy)))
                .With(CreatureAbility.InsiderJob),
            Creature("Pluriemployed Hustler", 3, 3, speed: 2, deploy: 0,
                "Three jobs, two side hustles, one kidney left.",
                "Penny Stock: small bets that pay double, so hits of 3 or less are doubled. Future: Coffeinated, then Burnout (the Hustle Culture pairing with Zeitgeist).",
                On(EffectTrigger.OnPlay, Draw(2)),
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self)))
                .With(CreatureAbility.PennyStock),
            Creature("Satoshi Nakamoto", 0, 5, speed: 1, deploy: 1,
                "Mined a million coins. Never spent one. Never seen again.",
                "Number go up: each Dawn, +1 Attack while it stays in play. Nobody knows who he is (Undercover: his plans show as ??? to the enemy). Starts harmless; leave him alone and he's a whale (the user, 2026-10-15).",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.Self, attack: true, health: false)))
                .With(CreatureAbility.Undercover)
                .Picture("A figure in a grey hoodie whose face is only a swirl of fog with a question mark in it, sitting cross-legged on a mountain of gold coins, a laptop glowing on his knees and a price chart behind him shooting straight up."),
            Creature("Unicorn", 1, 8, speed: 1, deploy: 1,
                "Valued at a billion. Burns a million a day. Profitable next year, every year.",
                "Future: Unicorn (Enterprise) from the card examples. Burn rate stays: Corporate can't heal it.",
                On(EffectTrigger.StartOfTurn, Draw(1)),
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self))),
            Creature("Multimillionaire Philanthropist", 2, 6, speed: 1, deploy: 1,
                "Gave away 0.01% of his fortune. Wrote off 100% of it.",
                "Future: a Contract. The enemy gets the gift now, you get the tax break on a later Day.",
                On(EffectTrigger.OnPlay, Buff(1, TargetType.EnemyCreatures, attack: false, health: true), Draw(2))),
            Creature("Fat Tony", 4, 5, speed: 1, deploy: 1,
                "Nothing personal. It's just business. Mostly the personal kind.",
                "Future: An Offer You Can't Refuse. Recruits a damaged enemy creature instead of hurting it.",
                On(EffectTrigger.OnPlay, Damage(2, TargetType.EnemyCreatures, AtRandom))),
            Creature("Charles Ponzi", 2, 3, speed: 2, deploy: 1,
                "Early investors were extremely satisfied.",
                "Future: pays draws from your next Days until it collapses, then takes them all back.",
                On(EffectTrigger.StartOfTurn, Draw(1)),
                On(EffectTrigger.OnDeath, Damage(3, TargetType.Player))),
            Spell("Margin Call", TargetType.AllCreatures,
                "Your position has been liquidated. Everyone's has. Have a nice day.",
                "Prophecy 1-3 (the user, 2026-10-15): it comes in 1 to 3 Days, nobody knows which. Future: Capital cost, discard your hand; each creature takes damage equal to the cards discarded.",
                Damage(2, TargetType.AllCreatures, Each), Draw(2)) // Back to 2 (cut on 2026-10-14; Corporate fell to the bottom)
                .Foretold(1, 3),
            Spell("Hostile Takeover", TargetType.EnemyCreatures,
                "We're not buying your company. We're buying its funeral.",
                "Future: Capital cost, discard 2 cards. Placeholder cost: the enemy draws a card.",
                Damage(4, TargetType.EnemyCreatures), Draw(1, TargetType.Enemy)), // Back to 4 (cut on 2026-10-14)
            Spell("Mass Layoffs", TargetType.FriendlyCreatures,
                "We were never a family. Please return your badge.",
                "Future: as an additional cost, destroy any number of your Workers; draw 1 per Worker next Day.",
                Damage(2, TargetType.FriendlyCreatures, Each), Draw(3)),
            Spell("Team Building Exercise", TargetType.AllCreatures,
                "Attendance is voluntary. Attendance is tracked.",
                "Target side, yours or theirs (the user, 2026-10-15): every creature there takes 1 damage and gets +1 Attack. Everyone goes, management included. Future: Coffeinated, followed by Burnout.",
                Damage(1, TargetType.AllCreatures, Each | TargetModifier.DroppedSide),
                Buff(1, TargetType.AllCreatures, attack: true, health: false, Each | TargetModifier.DroppedSide)),
            Spell("Dead Cat Bounce", TargetType.AllCreatures,
                "It's recovering! It's recovering! It's... no.",
                "Target creature, either side (the user, 2026-10-15): it heals 2 now, the bounce, and takes 5 damage at the next Dawn, the crash.",
                Heal(2, TargetType.AllCreatures), Later(Damage(5, TargetType.AllCreatures), 1)),
            Spell("Business Expense", TargetType.Player,
                "The yacht was a business expense. So was the lawyer. So is the appeal.",
                "Draw 2 next Day, and pay the fine now: 2 damage to you. Future: Capital cost, discard a card instead.",
                Draw(2), Damage(2, TargetType.Player, Each))
                .NotInStarterDeck()
                .Picture("A CEO in a full three-piece suit, briefcase still in hand, being walked into a jail cell by a guard, while he hands the guard a receipt stamped BUSINESS EXPENSE."),
            Spell("Business Operating Expenses", TargetType.FriendlyCreatures,
                "Two years in a federal prison camp. Priced in. The stock went up.",
                "Jail time as overhead (the user, 2026-10-15): target friendly creature goes dark until the next Dawn (it does its time) and you draw 2 next Day. Business Expense is the perks and the fine; this is the cell, written off.",
                Act(ActionType.Vanish, 0, TargetType.FriendlyCreatures, TargetModifier.None), Draw(2))
                .Picture("A smiling executive in an orange jumpsuit with a silk tie, checking into a cushy white-collar prison camp with a tennis court and a golf green, an assistant wheeling his luggage behind him; a sign over the gate reads COST OF DOING BUSINESS."),
            Spell("Taxpayer Funds Venture Capital Profits", TargetType.Player,
                "Privatize the gains. Socialize the bill.",
                "The gains are yours alone: draw 2 with the next Day's hand. The bill is everyone's: 1 damage to every creature on the board, yours included.",
                Draw(2), Damage(1, TargetType.AllCreatures, Each)),
            Creature("Cleaning Robot", 3, 7, speed: 1, deploy: 1,
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
                "Everyone's money is worth less, theirs more so: 2 damage to every enemy creature and 1 to each of yours. Prophecy 2 (the user, 2026-10-15): it hits two Days later, as prices catch up.",
                Damage(2, TargetType.EnemyCreatures, Each), Damage(1, TargetType.FriendlyCreatures, Each))
                .Foretold(2)
                .Picture("A chocolate bar in its old-size wrapper, the bar inside visibly half as big, and a supermarket price tag re-stickered three times, each price higher."),
            Spell("Escape Goat", TargetType.EnemyCreatures,
                "It wasn't us. It was the goat. Everyone agrees it was the goat.",
                "Heat Signature 2 on target creature (random effects and redirected attacks go to it), and draw a card. Corporate since 2026-10-15 (the user): when something goes wrong, someone else takes the blame, and the damage finds the goat.",
                Status(StatusEffectType.HeatSignature, 1, TargetType.EnemyCreatures, potency: 2), Draw(1))
                .Picture("The escape goat: a white goat scrambling over a wooden fence, front legs over the top rail, looking back in a sweat."),
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
            Creature("C-Suite", 4, 4, speed: 1, deploy: 1,
                "Record profits this quarter. The savings will trickle down to you shortly.",
                "Collateral Damage (trample): when its attack is more than a creature's Health, the rest goes on to that creature's player. The top of Corporate's hierarchy; it costs capital: discard a card.")
                .With(CreatureAbility.CollateralDamage)
                .Discarding(1)
                .Picture("A champagne tower of glasses: five executives in suits at the top glass, toasting, the champagne overflowing past them down the tiers, until a line of workers at the bottom catch the last drips in paper cups."),
            // The user, 2026-10-16: the cards this faction's decks needed most, from the balance study (sturdy bodies, reliable
            // damage and removal, board damage, Armor and healing: what wins; see Faction Balance Plan)
            Spell("Venture Capital", TargetType.FriendlyCreatures,
                "We're not investing in you. We're investing in your exit.",
                "Persistent growth (Corporate's secondary): target creature +2 Attack/Health for good, and you draw 1 next Day.",
                Buff(2, TargetType.FriendlyCreatures, attack: true, health: true), Draw(1))
                .Picture("A man in a fleece vest pouring a bucket of gold coins into a lanky startup founder's head through a funnel, the founder growing taller and more delusional."),
            Spell("Corporate Bailouts", TargetType.FriendlyCreatures,
                "Losses are public. Profits are private. Simple.",
                "Discard 1 (Corporate's cost): Armor 3 on every friendly creature and you heal 3. Too big to fail.",
                Armor(3, TargetType.FriendlyCreatures, Each), Heal(3, TargetType.Player))
                .Discarding(1)
                .Picture("A sinking cruise ship branded MEGABANK being lifted out of the sea by a giant hand made of taxpayers' wallets, the executives on deck popping champagne."),
            Spell("Flash Crash", TargetType.Enemy,
                "Down 1,000 points in 36 minutes. The robots apologise.",
                "Board damage (it wins 55%): 2 damage to every enemy creature. Discard 1. The 2010 crash.",
                Damage(2, TargetType.EnemyCreatures, Each))
                .Discarding(1)
                .Picture("A stock chart dropping off a cliff into a canyon, tiny traders in suits falling with it, a trading robot at the top shrugging."),
            Spell("Just in Time", TargetType.FriendlyCreatures,
                "Zero inventory. Zero slack. Zero idea what happens when the ship gets stuck.",
                "Tempo (Corporate is a timing faction): target creature still deploying is ready 1 Day sooner, and you draw 1 next Day. Unlike Overtime it costs no Burnout.",
                Requiring(Act(ActionType.Rush, 1, TargetType.FriendlyCreatures, TargetModifier.None), TargetRequisite.Deploying), Draw(1))
                .Picture("A delivery van screeching up to a factory door exactly as the last box on the assembly line runs out, a manager holding a stopwatch at 0:00."),
            Creature("Hedonic Treadmillionaire", 4, 6, speed: 1, deploy: 1,
                "Third yacht. Still not happy. Buying a fourth.",
                "A big body for a faction of small ones (Day Trader 3/1, Unpaid Intern 1/1): Arrival: you draw 1 next Day.",
                On(EffectTrigger.OnPlay, Draw(1)))
                .Picture("A billionaire in a gold tracksuit running on a treadmill that is itself on a yacht on a bigger yacht, sweating, his smile not moving."),
            Spell("Moral Bankruptcy", TargetType.EnemyCreatures,
                "Ethics is a cost centre.",
                "Removal: destroy target creature with 3 Attack or less. Corporate had damage but no clean kill.",
                Requiring(Act(ActionType.Destroy, 3, TargetType.EnemyCreatures, TargetModifier.None), TargetRequisite.AttackAtMost))
                .Picture("A banker stamping BANKRUPT across a smiling worker's portrait, the worker's chair already being carried away by movers."),
            // The meta picks (the user, 2026-10-16: "implement all of your ideas", from the Pokémon and Slay the Spire report):
            // Ripening, Laid Off, Toxic (merged into Burn since) and the statuses that help their owner, Grudge, side hazards, Sucker Punch, Hit and Run,
            // Policies, Shield Bash, Strip Search, On Trend. Names from the Brainstorm List
            Spell("Usury", TargetType.Enemy,
                "Interest on the interest on the interest. Compounded daily, forgiven never.",
                "Ripening board damage: 1 to every enemy creature, 1 more for each Dawn it's kept in your hand (up to 4). Corporate's patience pays (Slay the Spire's Retain cards that grow).",
                Damage(1, TargetType.EnemyCreatures, Each))
                .Ripening()
                .Picture("A loan shark in a pinstripe suit feeding coins into a piggy bank swollen to the size of a house, a tiny debtor pinned under its trotter."),
            Creature("Middle Manager", 2, 3, speed: 1, deploy: 0,
                "Restructured. Again. Still somehow on the payroll.",
                "Laid Off (creatures leave the hand at every Dawn now): when it's discarded from your hand, draw a card. Unplayed, it still pays; a discard cost paid with it costs nothing.",
                On(EffectTrigger.OnDiscarded, Draw(1)))
                .Picture("A middle manager carrying a cardboard box of desk things out of the office, a 'Synergy' mug on top, already on the phone to get rehired as a consultant."),
            Creature("Big Four", 2, 4, speed: 1, deploy: 0,
                "Four firms. Every audit. No findings.",
                "Grudge (Kingambit's Supreme Overlord): +1 Attack for each friendly creature that died this combat, up to +5. The workers Corporate spends become its leader's raise.")
                .With(CreatureAbility.Grudge)
                .Picture("Four identical accountants in grey suits shoulder to shoulder at a long table, the shredder behind them overflowing, a fifth chair empty with a name card reading 'Arthur Andersen'."),
            Creature("Bloomberg Terminal", 2, 4, speed: 2, deploy: 0,
                "Twenty-five grand a year to watch other people's money move.",
                "On Trend (Protosynthesis): +2 Attack while a Trend is on. With Casino it rides the house's weather.")
                .With(CreatureAbility.OnTrend)
                .Picture("A black two-screen trading terminal with its keyboard of colored keys, every chart spiking green, a trader's sweaty finger hovering over the yellow BUY key."),
            Spell("Internal Protocol: Quota Crusher Mindset", TargetType.Player,
                "Crush it. Then crush it again. The quota is a feeling.",
                "Policy (Slay the Spire's Powers): your creatures +1 Attack this Day, and again at each Dawn, for the rest of the combat. An engine for Corporate's wide boards.",
                Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, modifier: Each, days: 1))
                .Policy()
                .Picture("A motivational poster over a cubicle farm, a lone runner on a mountain top under the words QUOTA CRUSHER, the workers below asleep on their keyboards."),
            // The next picks (the user, 2026-10-16): Gear (held items), Provoked (Taunt), Combo (chaining spells)
            Spell("High-Frequency Trading Scam", TargetType.Player,
                "Buy at a millisecond, sell at the next. Repeat until regulated. Nobody ever regulates.",
                "Combo (Slay the Spire's Finisher): draw a card next Day, 1 more for each other spell you play this Day. Corporate's spells feed its draw.",
                Draw(1))
                .Combo()
                .Picture("A server rack in a basement next to the stock exchange, cables running under the street, a trader's face reflected in a screen of numbers moving too fast to read."),
            // Maneuvers (the user, 2026-10-17): spells that only move creatures, in three flavours by whose: Ambivalent (either
            // side's), Disruption (the enemy's), Logistics (yours). Push and Pull: a step back or forward; Swap: with its partner;
            // Shuffle: a whole side at random. Names from the Brainstorm List
            Spell("Valet", TargetType.FriendlyCreatures,
                "Premium parking, billed monthly. Your car has been moved for your convenience.",
                "Logistics Maneuver: target friendly creature trades places with its partner in the formation (Flank with Flank, Headquarters with Headquarters, the Vanguard with the left Flank). A megacorp's valet subscription, moving your pieces.",
                SwapPositions(TargetType.FriendlyCreatures))
                .Picture("A valet in a gold-trimmed uniform with a megacorp logo swapping two parked cars with a forklift, under a sign reading PREMIUM PARKING - SUBSCRIBERS ONLY."),
        };
        #endregion

        #region Statecraft
        // Pays with bureaucracy: its creatures take longer to deploy than similar ones, and its spells tie up its own side.
        // Can't hide anything. Engine: armor, area damage, slow but sturdy bodies, disables.
        private static List<CardDef> StatecraftCards() => new List<CardDef> {
            // The second Pokémon batch (the user, 2026-10-17): Protect, Rapid Spin, Assault Vest, Intimidate, Toxic Debris, Destiny Bond, Trick Room
            Creature("Civil Servant", 2, 6, speed: 1, deploy: 0,
                "Not my department. Not my desk. Not my problem.",
                "Hunker Down (Pokémon's Protect): on a Day it plans nothing, it takes no damage. The wall that works by not working.")
                .With(CreatureAbility.HunkerDown)
                .Picture("A clerk behind a counter whose window has just slammed shut with a CLOSED sign, arms folded, a cup of tea steaming, a queue of furious citizens' fists bouncing off the glass as if it were a force field."),
            Creature("Bomb Squad", 2, 5, speed: 1, deploy: 0,
                "Cut the red wire. Or the blue one. It's a coin toss with a pension.",
                "Bomb Disposal (Pokémon's Rapid Spin): the Slot Effect on a slot it enters is cleared before it goes off: Landmines, Snares, Checkpoints, a Shield, Scorched Earth, a Prayer.")
                .With(CreatureAbility.BombDisposal)
                .Picture("A technician in a bulky green bomb suit kneeling over a cartoon landmine with wire cutters, a tiny sweat drop, a robot on tank treads holding a torch for him, the rest of the squad watching from very far away through binoculars."),
            Creature("Drone Operator", 2, 3, speed: 2, deploy: 1,
                "Nine to five. Nevada. Twelve time zones from the target.",
                "Artillery (the formation's reach, GDD 2.4; the user, 2026-10-17): it attacks at a distance of exactly 4, never nearer: their Headquarters from a Flank, their Flanks from Headquarters. The one way to hit Headquarters with an attack.")
                .With(CreatureAbility.Artillery)
                .Picture("A drone pilot in a headset in an air-conditioned trailer in the desert, a coffee and a family photo on the console, the screen showing a tiny palace under a targeting reticle twelve time zones away, a DAY SHIFT rota pinned to the wall."),
            Creature("Riot Police", 3, 5, speed: 1, deploy: 0,
                "Protecting the public from the public.",
                "Riot Gear (Pokémon's Assault Vest): each Dawn, +2 Armor. Armor fades 1 at each Dusk, so it keeps a plate on.")
                .With(CreatureAbility.RiotGear)
                .Picture("A line of riot police in black armour and helmets behind transparent shields, each shield plastered with a different sticker: HAVE A NICE DAY, BABY ON BOARD, I BRAKE FOR PROTESTS."),
            Creature("Drill Sergeant", 3, 4, speed: 2, deploy: 0,
                "I can't HEAR you. Good. Stay that way.",
                "Intimidate: Arrival: -2 Attack to the enemy creature facing it, until it leaves play.",
                On(EffectTrigger.OnPlay, Buff(-2, TargetType.EnemyCreatures, attack: true, health: false, modifier: Facing)))
                .Picture("A drill sergeant in a campaign hat screaming an inch from the face of a huge enemy brute who has shrunk to half his size, knees knocking, his club drooping, spit flying in an arc."),
            Creature("Sapper", 2, 5, speed: 1, deploy: 0,
                "Where there's a hole, there's a way.",
                "Shrapnel (Pokémon's Toxic Debris): when an attack hits it, a Landmine 2 lands on a random empty enemy slot. Hitting it seeds their side.")
                .With(CreatureAbility.Shrapnel)
                .Picture("A soldier with a shovel and a satchel of mines diving out of a trench as a shell bursts beside him, mines flying out of the satchel in a lazy arc toward the enemy's side of the field, each with a little parachute."),
            Spell("Mutually Assured Destruction", TargetType.FriendlyCreatures,
                "If we go, you go. That's the whole plan. There is no other plan.",
                "Destiny Bond (the user, 2026-10-17): Mutually Assured for 2 Days on target friendly creature: the creature that kills it is destroyed too.",
                Status(StatusEffectType.MutuallyAssured, 2, TargetType.FriendlyCreatures))
                .Picture("Two generals at either end of a war room table, each with a finger on a big red button, wired to the same bomb between them, both smiling politely, a Cold War map glowing behind them."),
            Spell("Due Process", TargetType.Player,
                "Justice will be served. In the order the forms were received.",
                "Trend for 3 Days (Trick Room, Statecraft's flavour): Rush Hour, the slowest act first.",
                SetTrend(Trend.RushHour, 3))
                .Picture("A courthouse corridor where a snail with a briefcase is called into court first while a sprinter in running gear waits on a bench with a ticket reading 8,241, the wall clock with no hands."),
            // The Pokémon picks (the user, 2026-10-17): Quash as a Filibuster, Gravity as a Curfew, the spokesman's dodge
            Spell("Filibuster", TargetType.EnemyCreatures,
                "The senator will now read the phone book. All of it. Slowly.",
                "Quash (the user, 2026-10-17: Take a Number, renamed): target enemy creature acts last this Day, after every other creature's attack, move or ability. Your hits land before its.",
                Status(StatusEffectType.Stalled, 1, TargetType.EnemyCreatures))
                .Picture("An old senator at a podium reading from a phone book as thick as a pillow, hour 14 on the chamber clock, the benches full of sleeping politicians, one with a newspaper over his face, a clerk's eyes rolled back."),
            Spell("Curfew", TargetType.AllCreatures,
                "Everyone indoors by ten. Yes, you too. Especially you.",
                "Gravity (the user, 2026-10-17: a logistics shutdown): for 2 Days nobody moves: every creature on the board is Rooted. Moves, swaps and Maneuvers all stop; a Spin or a Shuffle leaves everyone where they are.",
                Status(StatusEffectType.Rooted, 2, TargetType.AllCreatures))
                .Picture("An empty city street at night under a CURFEW 22:00 sign, every window lit with a face pressed to the glass, a lone police car's light sweeping the road, and one cat sitting in the middle of it, the only one allowed out."),
            Creature("Spin Doctor", 2, 5, speed: 2, deploy: 0,
                "That's a great question. Here's the answer to a different one.",
                "Next Question (the user, 2026-10-17: the politician dodging the uncomfortable question): once a Day, when an attack is about to hit it, it steps into an empty slot of its side and the attack hits nothing. A tank that keeps its Health as long as it has room to sidestep.")
                .With(CreatureAbility.NextQuestion)
                .Picture("A press secretary in a tight suit leaning sideways out of frame at a podium as a forest of microphones lunges at where his head was, a NEXT QUESTION speech bubble, the reporters' notepads reading WHERE IS THE MONEY."),
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
            Creature("Mercenaries", 4, 5, speed: 3, deploy: 0,
                "Loyal to whoever pays next.",
                "Future: Recruitment bait. When it dies, the enemy may Recruit it instead.",
                On(EffectTrigger.OnDeath, Draw(1, TargetType.Enemy))),
            Creature("Man o' War", 5, 8, speed: 1, deploy: 1,
                "Gunboat diplomacy, minus the diplomacy.",
                "Future: multi-slot (takes 2 slots) and Heavy.",
                On(EffectTrigger.OnPlay, Damage(1, TargetType.EnemyCreatures))),
            Creature("Ministry for Equality", 1, 6, speed: 1, deploy: 1,
                "We love everybody. Please stop resisting our love.",
                "Rooted while deploying (the user, 2026-10-16): a building going up can't be moved, which keeps its strong Dawn stun in check. Future: Red Tape every Dawn: +1 Deployment Time on a random enemy creature.",
                On(EffectTrigger.StartOfTurn, Stun(1, TargetType.EnemyCreatures, AtRandom)))
                .RootedWhileDeploying(),
            Creature("Supreme Court", 0, 7, speed: 1, deploy: 1,
                "Appointed for life. Accountable for nothing.",
                "Return to Sender: enemy statuses bounce back to whoever sent them. Future: Fortifying. Adjacent friendly creatures can't be moved by enemy effects.",
                On(EffectTrigger.OnPlay, Armor(2, TargetType.FriendlyCreatures, Others)))
                .With(CreatureAbility.ReturnToSender),
            Creature("Protocol Enforcers", 2, 5, speed: 1, deploy: 0,
                "Please stand behind the line. Which line? This line. Now that line.",
                "Future: Keep Calm on friendly creatures each time it's hit.",
                On(EffectTrigger.OnDamage, Armor(1, TargetType.FriendlyCreatures, Others))),
            Creature("Demagogue", 2, 4, speed: 2, deploy: 1,
                "Says what you think. Thinks what he's told.",
                "The user, 2026-10-16: +2 Attack to the other friendly creatures when ready, not itself; 1 Day to deploy, and Irreplaceable, its drawback: no summon can take its slot. Future: Loaded Language.",
                On(EffectTrigger.OnPlay, Buff(2, TargetType.FriendlyCreatures, attack: true, health: false, Others)))
                .With(CreatureAbility.Irreplaceable),
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
            Spell("Doomsday Clock", TargetType.AllCreatures,
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
                On(EffectTrigger.OnPlay, Armor(2, TargetType.FriendlyCreatures, Each | Others)))
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
                "Mechanics test: Shield 2 on target creature (was a slot's, as Temporal Shield; the user, 2026-10-16: a slot can't have a shield, only a creature): for 2 Days, counting this one, it takes no combat damage, but spells and abilities still hurt it. It goes with the creature.",
                Status(StatusEffectType.Shielded, 2, TargetType.FriendlyCreatures))
                .Picture("Two generals shaking hands for the cameras in front of a CEASEFIRE banner, each hiding a grenade behind his back."),
            Spell("Violence Monopoly", TargetType.Player,
                "Only one of us is allowed to hit people. Guess which.",
                "The state's monopoly on violence: a Monitored Watchdog nullifies the next 2 enemy attacks before they land.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Attack, side = WatchdogSide.Enemy, effect = WatchdogEffect.Nullify, charges = 2 }, TargetType.Enemy))
                .NotInStarterDeck()
                .Picture("The board-game tycoon (top hat, white moustache, tailcoat) as the police: a badge on his chest, a baton raised in his white glove, glaring straight at you from beside the GO TO JAIL corner."),
            // The meta picks (the user, 2026-10-16: "implement all of your ideas", from the Pokémon and Slay the Spire report):
            // Ripening, Laid Off, Toxic (merged into Burn since) and the statuses that help their owner, Grudge, side hazards, Sucker Punch, Hit and Run,
            // Policies, Shield Bash, Strip Search, On Trend. Names from the Brainstorm List
            Spell("Border Crossing", TargetType.Enemy,
                "Papers. Purpose of visit. Papers again.",
                "A side hazard (Pokémon's Stealth Rock): Checkpoint 2 on every enemy slot for 3 Days, so every creature that enters their side takes 2. Statecraft's slow, sure clock; Code Review and Projection answer it.",
                SideHazard(SlotEffectType.Checkpoint, 2, 3, TargetType.EnemyCreatures))
                .Picture("A border checkpoint at night: a striped barrier, a bored officer in a booth stamping passports, a queue of strange creatures waiting under the floodlights, a 'Welcome' sign half fallen off."),
            Creature("Peace Force", 2, 5, speed: 1, deploy: 0,
                "We come in peace. Mostly with shields.",
                "Shield Bash (Body Press): its Attack goes up by its Armor. Its Arrival gives it 3 Armor, so it hits for 5 on its first Day and less as the Armor fades.",
                On(EffectTrigger.OnPlay, Armor(3, TargetType.Self)))
                .With(CreatureAbility.ShieldBash)
                .Picture("A line of blue-helmeted peacekeepers behind riot shields, the front one shoving his shield into a protester's face, a banner reading 'PEACE' behind them."),
            Creature("Security Guard", 3, 4, speed: 1, deploy: 0,
                "Please remove your shoes, your belt and your dignity.",
                "Strip Search (Knock Off): its attack on a creature with a status or a stat change Purges it first, good and bad alike, and hits 2 harder. An answer to buffed and Blessed creatures.")
                .With(CreatureAbility.StripSearch)
                .Picture("An airport security guard snapping on a blue latex glove with a grin, a traveler in socks with arms raised in the body scanner, a bin of confiscated water bottles overflowing behind."),
            Spell("Si Vis Pacem, Para Bellum", TargetType.Player,
                "If you want peace, prepare for war. If you want war, also prepare for war.",
                "Policy: your creatures +1 Armor, and again at each Dawn, for the rest of the combat (Armor wins in the balance study).",
                Armor(1, TargetType.FriendlyCreatures, Each))
                .Policy()
                .Picture("A Roman general signing a peace treaty with one hand while the other points at a row of catapults being built behind the tent."),
            // Maneuvers (the user, 2026-10-17): spells that only move creatures, in three flavours by whose: Ambivalent (either
            // side's), Disruption (the enemy's), Logistics (yours). Push and Pull: a step back or forward; Swap: with its partner;
            // Shuffle: a whole side at random. Names from the Brainstorm List
            Spell("Mass Emigration", TargetType.Enemy,
                "The best and brightest left. The rest are rearranging the furniture.",
                "Disruption Maneuver: every enemy creature changes slots at random. Their formation scattered: the Vanguard may end up at the back, Headquarters in front.",
                Act(ActionType.Shuffle, 0, TargetType.Enemy, TargetModifier.None))
                .Picture("A crowd with suitcases running in every direction at once past a signpost pointing every way, while a minister at a podium announces that the economy has never been stronger; behind them, Istanbul's Hagia Sophia over the Bosphorus at sunset."),
        };
        #endregion

        #region Zeitgeist
        // Pays with self-harm: damage to its own creatures and to you. Fast (Deployment Time 0-1), buffs that burn out.
        // Can't use Intelligence. Engine: cheap bodies that spike and fizzle, chaos that hits everyone.
        private static List<CardDef> ZeitgeistCards() => new List<CardDef> {
            // The second Pokémon batch (the user, 2026-10-17): the Choice items, Shadow Tag, Dancer, and healing that hurts
            Creature("One-Trick Pony", 5, 3, speed: 2, deploy: 0,
                "It does the one thing. Ask it for the other thing and watch it do the one thing.",
                "Fixated (Pokémon's Choice items): once it attacks, it repeats that attack every Day until it leaves: the same slot, whatever moves there. Big numbers for no choices.")
                .With(CreatureAbility.Fixated)
                .Picture("A pony in a tiny circus hat standing on its hind legs on a stool, doing the exact same trick in three frames in a row, the ringmaster's cue cards for other tricks piling up ignored on the sawdust."),
            Creature("Clingy Ex", 2, 4, speed: 2, deploy: 0,
                "We're not done talking. We'll never be done talking.",
                "Clingy (Pokémon's Shadow Tag): the enemy creature facing it can't move, and nothing moves it: Rooted as long as the Ex faces it.")
                .With(CreatureAbility.Clingy)
                .Picture("A person clamped around the leg of someone trying to walk away, dragged along the pavement, phone in hand showing 47 missed calls, the other one staring straight ahead with dead eyes."),
            Creature("Backup Dancer", 2, 3, speed: 2, deploy: 0,
                "Three steps behind the star, five steps ahead of the choreography.",
                "Backup Dancer (Pokémon's Dancer): when a friendly creature beside it attacks, it attacks the same slot too, if it planned nothing. Once a Day.")
                .With(CreatureAbility.BackupDancer)
                .Picture("A pop star mid-high-kick on a stage, and beside her a backup dancer in sequins doing the exact same kick a half-beat behind, both kicks landing on a stagehand's face at the edge of the stage."),
            Creature("Anti-Vaxxer", 3, 5, speed: 2, deploy: 0,
                "I've done my own research. It was a video.",
                "Do Your Own Research: healing damages it instead. Hand it a cure and it takes the hit; the enemy's healing on it hurts it too.")
                .With(CreatureAbility.DoYourOwnResearch)
                .Picture("A woman in a SPIRITUAL NOT MEDICAL t-shirt slapping a syringe out of a doctor's hand, a crystal pendant swinging, while a tiny cartoon virus on her shoulder gives a thumbs up."),
            // The Pokémon picks (the user, 2026-10-17): Encore as Groundhog Day, Trick as Freaky Friday, Metronome as Florida Man
            Spell("Groundhog Day", TargetType.EnemyCreatures,
                "Okay, campers, rise and shine! And don't forget your booties, 'cause it's cold out there today.",
                "Encore (the user, 2026-10-17): target enemy creature repeats today's action for the next 2 Days: the same attack on the same slot, the same move or ability, and nothing else. Its plan is made for it at Dawn, so everyone sees it coming. Did nothing today: it does nothing for 2 Days.",
                Status(StatusEffectType.Looping, 2, TargetType.EnemyCreatures))
                .Picture("A man in a hotel bed slapping an alarm clock that reads 6:00, the same clock, the same bed, the same groundhog in a tiny top hat on the windowsill, three identical frames of it stacked like a film strip, his face more desperate in each."),
            Spell("Freaky Friday", TargetType.AllCreatures,
                "Mom, I have your job today. Also your knees.",
                "Trick, turned to stats (the user, 2026-10-17): target creature and the creature facing it trade Attack and Health, each waking up in the other's body. Aim it at their glass cannon facing your wall, or the other way round.",
                Act(ActionType.TradeStats, 0, TargetType.AllCreatures, TargetModifier.None))
                .Picture("A mother and her teenage daughter facing each other across a breakfast table, a broken fortune cookie between them, their faces swapped: the mother with a nose ring and headphones, the daughter in reading glasses holding a mug reading WORLD'S BEST MOM, both screaming."),
            Creature("Florida Man", 3, 4, speed: 2, deploy: 0,
                "Florida Man does something. Again. Nobody knows what until it's done.",
                "Wild Card (the user, 2026-10-17, Pokémon's Metronome): its attack on a creature does one of these, at random: hits twice, Burns it 2 instead, Exhausts it instead, or nothing at all. Against the player, a plain hit.")
                .With(CreatureAbility.WildCard)
                .Picture("A sunburnt shirtless man with a mullet and jorts wrestling an alligator in a supermarket car park, a beer can in his free hand, a shopping trolley on fire behind him, a news ticker at the bottom reading FLORIDA MAN STRIKES AGAIN."),
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
                "My grandfather smoked a pack a day his whole life and lived to 90.",
                "Recoil: +2 damage on its attacks, and it takes 1 damage after each one. A mood that burns it out.")
                .With(CreatureAbility.Recoil)
                .Picture("A man chained by the neck to a giant cigarette pack like a dog on a leash, and happy about it: grinning, a cigarette in his mouth, a cloud of smoke around his head. The pack is a classic red-and-white one (a red chevron lid over a white body, a small gold crest, \"FILTER CIGARETTES\" on the red band), no brand name."),
            Creature("Karen", 4, 5, speed: 1, deploy: 0,
                "Would like to speak to your manager. And theirs. And God's.",
                "Mood Swings (the user, 2026-10-17, Pokémon's Moody): each Dawn, +2 to one of its stats and -1 to another, at random. Future: Impatient. Escalates every Day nobody gives it an intent.",
                On(EffectTrigger.OnDamage, Buff(1, TargetType.Self, attack: true, health: false)))
                .With(CreatureAbility.MoodSwings),
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
                "Mood Swings (the user, 2026-10-17: 'and others'): famous for the drama; each Dawn, +2 to one of its stats and -1 to another, at random. Future: Plot Armor. Survives a lethal hit once (OnAboutToDie).",
                On(EffectTrigger.OnPlay, Buff(2, TargetType.Self, attack: true, health: true), Damage(1, TargetType.FriendlyCreatures)))
                .With(CreatureAbility.MoodSwings),
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
            // The user, 2026-10-16: the cards this faction's decks needed most, from the balance study (sturdy bodies, reliable
            // damage and removal, board damage, Armor and healing: what wins; see Faction Balance Plan)
            Creature("Listen Kid", 2, 5, speed: 1, deploy: 0,
                "Back in my day we had one channel and we liked it.",
                "A sturdy body: Arrival: the creature facing it is Exhausted (it gets a lecture). An old-timer's unasked-for life advice.",
                On(EffectTrigger.OnPlay, Stun(1, TargetType.EnemyCreatures, Facing)))
                .Picture("An old man on a porch rocking chair pointing his cane at a terrified teenager, a speech bubble filled with tiny drawings of horses, rotary phones and a nickel."),
            Spell("Ol' Reliable", TargetType.EnemyCreatures,
                "The one thing that always works. Used again.",
                "Plain removal for Zeitgeist: 2 damage to target creature or player, and you draw 1 next Day.",
                Damage(2, TargetType.EnemyCreatures), Draw(1)) // 2, not 3: Zeitgeist went to the top with it
                .Picture("A worn-out frying pan hanging alone on a kitchen wall with a halo around it, a dented shape of a burglar's head in its bottom."),
            Spell("Weaponized Incompetence", TargetType.Enemy,
                "Oh no, I did the dishes wrong again. Guess you'll have to do them forever.",
                "Shuts down an engine (Zeitgeist's strikes): the strongest enemy creature is Exhausted.",
                Stun(1, TargetType.EnemyCreatures, StrongestOne))
                .Picture("A man holding a plate he has 'washed' that is still covered in food, looking helpless, while his partner behind him quietly takes over the whole sink."),
            Creature("Absentee Parents", 4, 4, speed: 2, deploy: 0,
                "Gone for cigarettes. Back for the inheritance.",
                "An honest body: 4/4, ready at once, Speed 2. Last Will: you draw 1 next Day.",
                On(EffectTrigger.OnDeath, Draw(1)))
                .Picture("A family portrait on a mantelpiece with two parent-shaped holes cut out of it, a postcard from Ibiza tucked into the frame."),
            Spell("The Friends We Made Along the Way", TargetType.FriendlyCreatures,
                "We lost the treasure. We kept each other. Mostly.",
                "Every friendly creature gets +1 Attack and +2 Health, for good: Zeitgeist's swarm finally sticks around.",
                Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, Each), Buff(2, TargetType.FriendlyCreatures, attack: false, health: true, Each))
                .Picture("A ragtag group of adventurers around a campfire laughing, an empty treasure chest behind them, a map with the X scribbled out."),
            // The meta picks (the user, 2026-10-16: "implement all of your ideas", from the Pokémon and Slay the Spire report):
            // Ripening, Laid Off, Toxic (merged into Burn since) and the statuses that help their owner, Grudge, side hazards, Sucker Punch, Hit and Run,
            // Policies, Shield Bash, Strip Search, On Trend. Names from the Brainstorm List
            Creature("Spite Voter", 3, 3, speed: 2, deploy: 0,
                "Didn't like either of them. Voted for the worst one, out of principle.",
                "Laid Off: when it's discarded from your hand, 2 damage to a random enemy creature. Zeitgeist reacts: even the creatures it doesn't play hit back.",
                On(EffectTrigger.OnDiscarded, Damage(2, TargetType.EnemyCreatures, AtRandom)))
                .Picture("A grumpy man in a voting booth gleefully ticking the box of a clown candidate, a 'Don't Blame Me' badge pinned to his cardigan."),
            // It spins (the user, 2026-10-17); it was a 2/3, Speed 3 On Trend creature (+2 Attack while a Trend is on, a fad):
            // Bloomberg Terminal keeps On Trend
            Creature("Fidget Spinner", 2, 3, speed: 3, deploy: 0,
                "2017's must-have. 2018's landfill.",
                "Arrival: the enemy's Vanguard and Flanks spin clockwise, one slot round. Arriving the Day it's played, it scrambles where that Day's attacks land.",
                On(EffectTrigger.OnPlay, Act(ActionType.Spin, 0, TargetType.Enemy, TargetModifier.None)))
                .Picture("A giant fidget spinner whirling on a school desk, sparks flying off its bearings, a crowd of kids staring at it hypnotized, a teacher's 'Confiscated' box in the corner."),
            Creature("Camper Van", 2, 4, speed: 2, deploy: 0,
                "Van life: no rent, no fixed address, no witnesses.",
                "Hit and Run (U-turn): after its attack it goes back to your hand, fresh, with the next Day's, and its slot is free for the next summon.")
                .With(CreatureAbility.HitAndRun)
                .Picture("A painted hippie camper van speeding away from a dented mailbox, a dreamcatcher swinging from the mirror, the couple inside filming the getaway for their followers."),
            Creature("Lost Generation", 2, 4, speed: 1, deploy: 0,
                "Born into one crisis, graduated into the next.",
                "Grudge: +1 Attack for each friendly creature that died this combat, up to +5. What makes Zeitgeist's swarm stick: every friend lost makes it angrier.")
                .With(CreatureAbility.Grudge)
                .Picture("Tired millennials in graduation gowns sitting on the steps of a closed bank, their diplomas folded into paper boats floating down the gutter."),
            Spell("Universal Basic Income", TargetType.Player,
                "Free money. Everyone's furious about it.",
                "Policy: draw a card, and again at each Dawn, for the rest of the combat. Zeitgeist's engine: more cards, every Day.",
                Draw(1))
                .Policy()
                .Picture("A long line of citizens at an ATM spitting out identical small banknotes, a politician in the background tearing his hair out, a man in the line buying a lottery ticket with his."),
            // The next picks (the user, 2026-10-16): Gear (held items), Provoked (Taunt), Combo (chaining spells)
            Spell("Calm Your Tits", TargetType.EnemyCreatures,
                "Said nobody, ever, successfully.",
                "Provoke 2 (Taunt, Follow Me) on target creature: for the rest of this Day and the next 2, it attacks the creature facing it and can't use its Active ability; and you draw a card next Day. Put a wall in front of it.",
                Status(StatusEffectType.Provoked, 2, TargetType.EnemyCreatures), Draw(1))
                .Picture("A man at a family barbecue telling his furious aunt to calm down, everyone around them freezing mid-bite, the aunt's face going from red to purple."),
            // Maneuvers (the user, 2026-10-17): spells that only move creatures, in three flavours by whose: Ambivalent (either
            // side's), Disruption (the enemy's), Logistics (yours). Push and Pull: a step back or forward; Swap: with its partner;
            // Shuffle: a whole side at random. Names from the Brainstorm List
            Spell("Tourist Trap", TargetType.EnemyCreatures,
                "Authentic local cuisine. Twenty-two euros a coffee.",
                "Disruption Maneuver: pull target enemy creature a step forward (Headquarters to its Flank, a Flank to the Vanguard); a creature already there trades places with it. Drags their important people into reach.",
                Act(ActionType.Pull, 0, TargetType.EnemyCreatures, TargetModifier.None))
                .Picture("A sunburnt tourist in socks and sandals lured off the beach by a neon sign reading AUTHENTIC, into a beach restaurant where a waiter already holds out a bill a metre long, a palm and a parasol behind."),
        };
        #endregion

        #region Factions without a deck yet
        // Digital Society: a swarm of cheap bodies, effects that spread, many creatures piling on one target
        private static List<CardDef> DigitalSocietyCards() => new List<CardDef> {
            Creature("Its Been 84 Years...", 4, 5, speed: 1, deploy: 0,
                "Sorry I'm late. Traffic was terrible. And the century.",
                "Sorry I'm Late (the user, 2026-10-16, a meme ability): played onto a friendly creature, it arrives after the attacks, so the creature it replaces takes the Day's hits first. A big body that walks in fresh.")
                .With(CreatureAbility.SorryImLate)
                .Picture("A very old lady in a cardigan wheeled in on a hospital bed through a doorway, a tiny party hat on, a wall clock behind her covered in cobwebs, a dusty birthday cake on the table with 84 candles burnt down to stubs."),
            Creature("Good Boi", 1, 6, speed: 1, deploy: 0,
                "He protec. He attac. But most importantly, he gets snac.",
                "He Protec (the user, 2026-10-16, a meme ability): when a summon replaces it, it stays through the attacks and takes the Day's hits, and the newcomer arrives after them. Sturdy, to soak the Day before it hands over.")
                .With(CreatureAbility.HeProtec)
                .Picture("A fluffy shiba dog in a tiny knight's helmet holding up a pot lid as a shield, standing in front of a crying smaller creature, a dog biscuit tucked behind its ear."),
            Spell("Console Memory Card", TargetType.Player,
                "Your console has the memory of a goldfish. 8 MB of remembering, sold separately.",
                "Load your save (the user, 2026-10-15, after the PS2 memory card meme): send for any card from your discard pile; it joins your next hand. The first Send For that reaches the discard pile instead of the deck.",
                SendForCard(SendForFilter.Any, fromDiscard: true))
                .Picture("A chunky black console memory card, 8 MB MEMORY CARD on its label, with a goldfish in a tiny bowl taped on top for the console's memory, a glowing save icon floating out of the card."),
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
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, AtRandom | Others),
                    Status(StatusEffectType.HeatSignature, 1, TargetType.EnemyCreatures, potency: 1, modifier: AtRandom)))
                .Picture("A messenger group chat in the familiar green-and-beige look: THE JURY (47 members), gavels and skulls with blue read ticks under a blurry photo of one guy, while the guy himself sweats alone outside the phone."),
            Spell("Social Panopticon", TargetType.EnemyCreatures,
                "Nobody is watching you. Everybody is.",
                "Every enemy creature is Revealed for 2 Days (its plans can be seen, Watchdogs can match them) and gets Heat Signature 1. Future: a Trend where nothing can be Concealed.",
                Status(StatusEffectType.Revealed, 2, TargetType.EnemyCreatures, modifier: Each),
                Status(StatusEffectType.HeatSignature, 1, TargetType.EnemyCreatures, potency: 1, modifier: Each))
                .Picture("A real panopticon seen from inside: a ring of barred cells on two floors around a central watchtower with a single recording eye, every inmate lit by their own phone."),
            // The user, 2026-10-16: more cards for the smaller factions (leapers, swappers, tanks, combinations)
            Creature("Short King", 2, 2, speed: 3, deploy: 0,
                "Five foot four. Six foot in the bio.",
                "A leaper: Leapfrog, it jumps over creatures to an empty slot. Arrival: +2 Attack this Day, a big entrance. Fast and cheap, it hops to the target the rest of the swarm is piling on (Dogpile).",
                On(EffectTrigger.OnPlay, Buff(2, TargetType.Self, attack: true, health: false, days: 1)))
                .With(MovementKeyword.Leapfrog)
                .NotInStarterDeck()
                .Picture("A short man wearing a paper crown standing on a stack of phone books to take a selfie at arm's length, the profile on his phone screen reading 6 FT."),
            Creature("Content Creator", 1, 3, speed: 2, deploy: 0,
                "Smash that like button. Smash it. Please.",
                "A swapper: Swap, it trades places with a friendly creature however far. Each Dawn it goes live: Heat Signature 1 on itself, for good (Digital Society pays with exposure), so random effects and Homing ones come to it. Swap it next to the creature you want spared, or in front of the hit.",
                On(EffectTrigger.StartOfTurn, Status(StatusEffectType.HeatSignature, 1, TargetType.Self, potency: 1)))
                .With(MovementKeyword.Swap)
                .NotInStarterDeck()
                .Picture("A streamer in a headset under a ring light screaming at the camera with a shocked face and a giant red arrow pointing at nothing, the thumbnail on the monitor behind showing the same face, 100K in gold letters."),
            Creature("Thirst Trap", 1, 5, speed: 1, deploy: 0,
                "Not looking for attention. Posted it 14 times.",
                "A tank: Arrival: Heat Signature 2 on itself, so random effects and Homing ones go to it rather than to the others. Each Dawn a random other friendly creature gets +1 Attack this Day (its simps fight harder).",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.HeatSignature, 1, TargetType.Self, potency: 2)),
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, AtRandom | Others, days: 1)))
                .NotInStarterDeck()
                .Picture("A steel bear trap open on a beach towel, a ring light and a phone on a tripod set up in front of it, the phone screen filling with hearts, and a glass of water standing beside it."),
            // The meta picks (the user, 2026-10-16: "implement all of your ideas", from the Pokémon and Slay the Spire report):
            // Ripening, Laid Off, Toxic (merged into Burn since) and the statuses that help their owner, Grudge, side hazards, Sucker Punch, Hit and Run,
            // Policies, Shield Bash, Strip Search, On Trend. Names from the Brainstorm List
            Spell("Selfie Esteem", TargetType.FriendlyCreatures,
                "Forty takes. One filter. Zero self-worth.",
                "Ripening: target creature +1 Attack and +1 Health, 1 more of each for each Dawn it's kept in your hand (up to +4/+4).",
                Buff(1, TargetType.FriendlyCreatures, attack: true, health: true))
                .Ripening()
                .Picture("A girl holding a selfie stick at arm's length, the phone screen showing a perfect filtered face while the real one is crying, a ring light glowing."),
            Creature("Influencer", 2, 3, speed: 2, deploy: 0,
                "Partnered with whatever's trending. Allergic to whatever isn't.",
                "On Trend: +2 Attack while a Trend is on. Digital Society lives on what's trending.")
                .With(CreatureAbility.OnTrend)
                .Picture("An influencer posing in front of a melting glacier with a branded water bottle, an assistant holding a reflector, the caption 'so grateful' on the phone."),
            // The next picks (the user, 2026-10-16): Gear (held items), Provoked (Taunt), Combo (chaining spells)
            Creature("U Mad Bro?", 2, 4, speed: 2, deploy: 0,
                "Didn't read your argument. Disagrees anyway. Has posted the trollface.",
                "Arrival: Provoke 2 on the creature facing it (Taunt): it has to attack this troll for the next Days, and can't use its Active ability. A sturdy body that pulls the hits.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Provoked, 2, TargetType.EnemyCreatures, modifier: Facing)))
                .Picture("A smug teenager in a gaming chair, trollface on his hoodie, typing 'u mad bro?' while a wall of furious replies piles up on his screen."),
        };

        // Shadow Ops.: fragile agents, hidden plans, traps
        private static List<CardDef> ShadowOpsCards() => new List<CardDef> {
            // The second Pokémon batch (the user, 2026-10-17): Destiny Bond, the hostage's version
            Spell("Hostage Situation", TargetType.FriendlyCreatures,
                "Nobody move, or the creature gets it. And then you get it.",
                "Destiny Bond: Mutually Assured for 2 Days and Heat Signature 2 on target friendly creature: their random and redirected attacks go to it, and the creature that kills it is destroyed too.",
                Status(StatusEffectType.MutuallyAssured, 2, TargetType.FriendlyCreatures), Status(StatusEffectType.HeatSignature, 1, TargetType.FriendlyCreatures, potency: 2))
                .Picture("A masked agent holding a very calm creature hostage in a bank lobby, a dead man's switch in his hand wired to every other creature in the room, a negotiator outside with a megaphone and a sandwich."),
            // The Pokémon pick (the user, 2026-10-17): Wide Guard as Plausible Deniability
            Spell("Plausible Deniability", TargetType.Player,
                "I have no recollection of that creature, that Day, or this conversation.",
                "Wide Guard (the user, 2026-10-17): this Day, effects that hit more than one creature skip your creatures (area spells, Spray and Pray, a Hipster's arrival), like Too Mainstream for the whole side.",
                Status(StatusEffectType.Deniable, 1, TargetType.FriendlyCreatures, modifier: Each))
                .Picture("A man in a grey suit at a hearing shrugging with both palms up in front of a wall of documents so redacted they are solid black bars, a speech bubble reading I DON'T RECALL, the committee's questions bouncing off him as little paper planes."),
            Creature("Cleaner", 2, 3, speed: 3, deploy: 0,
                "Two shots. One invoice.",
                "Mechanics test: Double Tap. It attacks twice: a second hit right after the first.")
                .With(CreatureAbility.DoubleTap)
                .Picture("A calm man in a cleaning company jumpsuit, a mop in one hand and a silenced pistol in the other, two neat holes in a target behind him."),
            Creature("Political Decoy", 1, 6, speed: 1, deploy: 0,
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
                "A counter (the user, 2026-10-17): the first enemy attack this Day is redirected to a random creature (the one with the most Heat first). A Briefing Watchdog, online the Day it's played, for that Day only.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Attack, side = WatchdogSide.Enemy, effect = WatchdogEffect.Redirect,
                    charges = 1, timing = WatchdogTiming.Briefing, pick = BriefingPick.First }, TargetType.Enemy, days: 1))
                .Picture("A remake of the first Counter-Strike box cover: a counter-terrorist and a terrorist in a tense standoff in a dusty corridor, but both are accountants in thick glasses, aiming pocket calculators at each other like rifles, ledgers fluttering around them."),
            Creature("Mole", 3, 3, speed: 2, deploy: 0,
                "Twenty years with the company. Twenty-one with the other one.",
                "Mechanics test: Underground (both ways). When it's attacked, the attacker's buffs and debuffs are ignored; when it attacks, the defender's temporary Health changes are.")
                .With(CreatureAbility.Underground)
                .Picture("A mild office clerk in a cardigan and lanyard at his desk, a mole's snout and claws poking out of his sleeves, a tunnel opening under his chair that leads to a foreign embassy's flag."),
            Creature("Signal Jammer", 2, 5, speed: 1, deploy: 0,
                "No bars. No calls. No witnesses.",
                "Mechanics test: Signal Jammer. The enemy creature directly opposite has its ability switched off (its triggered effects still work).")
                .With(CreatureAbility.SignalJammer)
                .Picture("An agent in a black suit with a backpack bristling with antennas, standing in a busy street where everyone stares in horror at phones showing \"No Service\"."),
            Creature("Deep Cover", 2, 3, speed: 2, deploy: 0,
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
                "Extreme Prejudice: its attack destroys the creature it hits, whatever its Health or Armor, and then it's gone too: one hit, one roadblock. Untouchable or a Shield stops the hit; an empty slot, it just hits the player for 1.")
                .With(CreatureAbility.ExtremePrejudice)
                .Picture("A hitman in a dripping raincoat under a black umbrella in the rain, pressing a red TERMINATED stamp onto a personnel file with a photo clipped to it."),
            Spell("Perfectly Timed Camera Malfunction", TargetType.EnemyCreatures,
                "Tragic. The one minute the cameras were down.",
                "Destroy target enemy creature if it's Revealed, whatever its Health or Armor (Untouchable still saves it). Set it up with Tapped Communications, which Reveals at once.",
                Destroy(TargetType.EnemyCreatures, TargetRequisite.Revealed))
                .SetUpBy("Tapped Communications")
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
            Creature("Whistleblower", 2, 2, speed: 3, deploy: 0,
                "Leaked everything. Now lives in an embassy closet.",
                "Dead Man's Switch: when it dies, every enemy creature is Revealed for 2 Days.",
                On(EffectTrigger.OnDeath, Status(StatusEffectType.Revealed, 2, TargetType.EnemyCreatures)))
                .Picture("A man in a hoodie blowing a sports whistle, classified documents spilling out of every pocket."),
            Spell("Involuntary Free Polonium Giveaway", TargetType.EnemyCreatures,
                "A gift from an old friend. Do not open indoors.",
                "1 damage now, then Burned for 3 Days. Future: Spontaneous Combustion.",
                Damage(1, TargetType.EnemyCreatures), Status(StatusEffectType.Burned, 3, TargetType.EnemyCreatures, potency: 1))
                .Picture("A gentleman in a bowler hat at a bus stop, the tip of his black umbrella dripping glowing green drops that sizzle on the pavement."),
            Creature("Control Room", 1, 5, speed: 1, deploy: 0,
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
            Spell("False Flag Operation", TargetType.AllCreatures,
                "They attacked us. Well, someone did. Same thing.",
                "Destroy target creature (its Last Will happens); the other creatures on its side get +2 Attack while they stay in play, rallied by the outrage (the user, 2026-10-15: only the side where it was destroyed). Your own, to rally your side; theirs, to remove a threat at a price. If it doesn't die (the Destroy stopped or denied), no rally either (the user, 2026-10-16).",
                Destroy(TargetType.AllCreatures), OnlyIfItDies(Buff(2, TargetType.AllCreatures, attack: true, health: false, TargetModifier.AlliesOfTarget)))
                .NotInStarterDeck()
                .Picture("A soldier in an enemy uniform two sizes too small, its flag sewn on upside down, setting fire to his own side's outpost while a film crew records it and a general reads an outrage speech from a card."),
            Spell("Never Let Them Know Your Next Move", TargetType.FriendlyCreatures,
                "Everyone gets a medal. He gets all of them.",
                "Every other friendly creature hands its stat raises to target friendly creature (the timed, the lasting and the ones for good), and it's Concealed for 2 Days: the fatal blow nobody sees coming. After False Flag Operation, it's +2/+2 from each.",
                Act(ActionType.Funnel, 0, TargetType.FriendlyCreatures, TargetModifier.None), Status(StatusEffectType.Concealed, 2, TargetType.FriendlyCreatures))
                .NotInStarterDeck()
                .SetUpBy("False Flag Operation")
                .Picture("A row of agents in identical grey suits passing their pistols, knives and grenades down the line to one man at the end, who disappears under a mountain of weapons with only his sunglasses showing."),
            Spell("Frame Job", TargetType.FriendlyCreatures,
                "The fingerprints were his. We checked. We put them there.",
                "Target friendly creature passes every status it has (Revealed, Heat Signature, Burned, Doomed... and its good ones too: no status is good or bad by itself, the user, 2026-10-15) to the enemy creature facing it, with the Days they have left. Nothing faces it: nothing happens.",
                Act(ActionType.PassHarmfulStatuses, 0, TargetType.FriendlyCreatures, TargetModifier.None))
                .NotInStarterDeck()
                .Picture("A smiling agent pressing a sleeping man's thumb onto a smoking gun with tweezers, a police lineup board behind them already labelled with the sleeper's name."),
            Spell("Devil's Breath", TargetType.FriendlyCreatures,
                "One puff of scopolamine and he helped carry his own safe to our van.",
                "Target friendly creature takes the stat raises and every status of the enemy creature facing it (all of them since 2026-10-15: no status is good or bad by itself, so a Burn or a Doom comes along too). Its victims hand everything over, smiling.",
                Act(ActionType.StealStatChanges, 0, TargetType.FriendlyCreatures, TargetModifier.None),
                Act(ActionType.StealGoodStatuses, 0, TargetType.FriendlyCreatures, TargetModifier.None))
                .NotInStarterDeck()
                .Picture("A woman blowing a puff of glittering white powder off her palm into a businessman's face; dazed and smiling, he hands her his watch, his wallet and the keys to an armored car."),
            Spell("He Didn't Kill Himself", TargetType.FriendlyCreatures,
                "Officially: suicide. Unofficially: on a beach in Belize, rereading the files.",
                "Target friendly creature fakes its death: its Last Will happens (Dead Man's Switch destroys their strongest), then it goes back to your hand, in the next Day's hand.",
                Act(ActionType.FakeDeath, 0, TargetType.FriendlyCreatures, TargetModifier.None))
                .NotInStarterDeck()
                .SetUpBy("Dead Man's Switch")
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
                .SetUpBy("Tapped Communications")
                .Picture("A handler in a dim bar sliding a thick envelope and a photograph across the table to a nervous soldier; the photo shows the soldier with a woman who isn't his wife."),
            Creature("If I Told You, I'd Have to Kill You", 3, 4, speed: 2, deploy: 0,
                "It's classified. So are you, now.",
                "When Revealed (Revealed, or a Reveal shows it): 4 damage to whoever revealed it, the creature whose effect it was or else its player. Allergic to Light: each time, it takes 2 damage itself.",
                On(EffectTrigger.OnRevealed, Damage(4, TargetType.Enemy)))
                .With(CreatureAbility.AllergicToLight)
                .NotInStarterDeck()
                .Picture("A spy in a dark suit leaning in at a cocktail party to whisper into an eager journalist's ear, while under the table his other hand quietly screws a silencer onto a pistol."),
            Creature("Lone Gunman", 2, 3, speed: 1, deploy: 0,
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
                "A Briefing Watchdog: from the Day it's set, the strongest enemy attack backfires: it doesn't happen, and the attacker takes its own Attack as damage. 1 charge.",
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
                "Borrowed Time on target friendly creature (the user, 2026-10-16): this Day it can't die (damage leaves it at 0 Health, a Destroy fails); at this Day's Dusk, at 0 Health, it's brought back to 1 and Borrowed Time ends. Not two Days of attacks.",
                Status(StatusEffectType.BorrowedTime, 1, TargetType.FriendlyCreatures))
                .NotInStarterDeck()
                .Picture("A spy in a torn tuxedo surfing a towering wave on a car door past a collapsing ice palace, checking his wristwatch."),
            Spell("Licence to Kill", TargetType.FriendlyCreatures,
                "Paperwork for murder, signed in triplicate. Valid until used.",
                "Licensed: target friendly creature's next attack on a creature destroys it, whatever its Health or Armor (Untouchable or a Shield stops the hit, and spends the licence). It keeps it until it's used.",
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
            Creature("Femme Fatale", 2, 4, speed: 2, deploy: 0,
                "She didn't kill him. She just held him very, very close.",
                "Each Dusk she squeezes the enemy creature facing her: 1 damage to it, and she heals 1. Nothing facing her: nothing happens. Ambush: she arrives as ??? and deals double while nobody knows who she is.",
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.EnemyCreatures, Facing), Heal(1, TargetType.Self)))
                .With(CreatureAbility.Ambush)
                .NotInStarterDeck()
                .Picture("A glamorous woman in a black evening gown smiling over her cocktail at a casino table, while the red-faced general in the chair beside her is squeezed between her crossed legs, his medals popping off."),
            Creature("MK Ultra Test Subject", 3, 3, speed: 1, deploy: 0,
                "Volunteered for a Harvard study in 1959. Doesn't remember the rest.",
                "Each Dawn it attacks a random creature, on either side (never itself): strong, cheap, uncontrolled. It can still attack as you plan.",
                On(EffectTrigger.StartOfTurn, Strike(TargetType.AllCreatures, AtRandom)))
                .NotInStarterDeck()
                .Picture("Ted Kaczynski as the young Harvard math prodigy, before the woods: clean-cut, short neat hair, white shirt and narrow tie, strapped into a 1950s lab chair with electrodes taped to his temples, a blackboard of equations behind him and a psychologist with a clipboard in the corner."),
            // The user, 2026-10-16: the cards this faction's decks needed most, from the balance study (sturdy bodies, reliable
            // damage and removal, board damage, Armor and healing: what wins; see Faction Balance Plan)
            Spell("Exploding Pen", TargetType.EnemyCreatures,
                "Click once to write. Click three times to stop writing forever.",
                "Reliable damage at last: 3 to target creature or player, and 2 more if it's Revealed. Shadow Ops. had only conditional kills; this is its plain answer, and its starter deck's.",
                Damage(3, TargetType.EnemyCreatures), Requiring(Damage(2, TargetType.EnemyCreatures), TargetRequisite.Revealed))
                .Picture("A fountain pen lying on a desk with its cap off and a lit fuse curling out of the nib, a spy's hand sliding it across to a villain who is already reaching for it."),
            Creature("Drug Pigeon", 2, 2, speed: 3, deploy: 0,
                "Coo. Coo. Cocaine.",
                "A leaper (Leapfrog wins 60% in the study): it jumps over creatures to an empty slot, and its Arrival sends for a card among the top 3 of your deck. An old prison-smuggling trick.",
                On(EffectTrigger.OnPlay, SendForCard(SendForFilter.Any, top: 3)))
                .With(MovementKeyword.Leapfrog)
                .Picture("A pigeon in tiny aviator goggles flying over a prison wall, a little backpack strapped to its chest bulging with white packets, a guard in the watchtower looking the other way."),
            Creature("Embassy", 0, 8, speed: 1, deploy: 1,
                "Sovereign soil. Bulletproof glass. Excellent canapés.",
                "A wall for a faction of glass cannons: Unbothered (enemy effects can't lower its stats), and each Dawn a random other friendly creature is Concealed for the Day (diplomatic cover).",
                On(EffectTrigger.StartOfTurn, Status(StatusEffectType.Concealed, 1, TargetType.FriendlyCreatures, modifier: AtRandom | Others)))
                .With(CreatureAbility.Unbothered)
                .Picture("A grand embassy building with a flag over the door, a man in a trench coat climbing in through a back window while the ambassador smiles for photographers on the front steps."),
            Spell("Secret Anthrax Laboratory", TargetType.Enemy,
                "Rebirth Island. The sea dried up. The anthrax didn't.",
                "Shadow Ops.' board damage: 2 to every enemy creature, and they're Revealed for the Day, so Perfectly Timed Camera Malfunction and Asset Recruitment can follow up. Vozrozhdeniya, the Soviet bioweapons island.",
                Damage(2, TargetType.EnemyCreatures, Each), Status(StatusEffectType.Revealed, 1, TargetType.EnemyCreatures, modifier: Each))
                .Picture("An abandoned concrete lab on a cracked dry seabed, rusty ships stranded in the sand around it, a lone scientist in a gas mask locking the door and pocketing the key."),
            Spell("Tracking Beacon", TargetType.EnemyCreatures,
                "It's not a bug. It's a feature.",
                "Revealed 2 and Heat Signature 1 on target creature, and you draw 1 next Day: it lights it up for Perfectly Timed Camera Malfunction, Exploding Pen and every random hit.",
                Status(StatusEffectType.Revealed, 2, TargetType.EnemyCreatures), Status(StatusEffectType.HeatSignature, 1, TargetType.EnemyCreatures, potency: 1), Draw(1))
                .Picture("A tiny blinking red device stuck to the back of a henchman's collar, a map on a laptop screen in the background showing his dot moving through the city."),
            Creature("Arsonist", 3, 3, speed: 2, deploy: 0,
                "Insurance fraud is a team sport.",
                "A body that hits on arrival: 2 damage to the creature facing it. Shadow Ops.' creatures were 1/3s and 1/4s; this one trades.",
                On(EffectTrigger.OnPlay, Damage(2, TargetType.EnemyCreatures, Facing)))
                .Picture("A man in a suit holding a briefcase and a lit match in front of a failing shop, a fire insurance policy sticking out of his breast pocket."),
            Spell("Counter-Surveillance", TargetType.Player,
                "We watch the people who watch the people.",
                "A Monitored Watchdog (Watchdogs win 63% in the study): the next enemy attack backfires, nullified, and the attacker takes its own Attack. 1 charge. Sabotage does it with a Briefing; this one catches attacks already planned.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Attack, side = WatchdogSide.Enemy, effect = WatchdogEffect.Backfire, charges = 1 }, TargetType.Enemy))
                .Picture("A spy in a dark room watching a wall of monitors, every screen showing another spy watching him back, one of them waving."),
            // The meta picks (the user, 2026-10-16: "implement all of your ideas", from the Pokémon and Slay the Spire report):
            // Ripening, Laid Off, Toxic (merged into Burn since) and the statuses that help their owner, Grudge, side hazards, Sucker Punch, Hit and Run,
            // Policies, Shield Bash, Strip Search, On Trend. Names from the Brainstorm List
            Creature("Compromised Agent", 2, 2, speed: 2, deploy: 0,
                "Her cover's blown. Her friends' covers aren't. Yet.",
                "Laid Off: when it's discarded from your hand, a random friendly creature is Concealed 2 Days. A burned agent covers the ones still out there.",
                On(EffectTrigger.OnDiscarded, Status(StatusEffectType.Concealed, 2, TargetType.FriendlyCreatures, modifier: AtRandom)))
                .Picture("A spy in a trench coat mobbed by paparazzi in a train station, while behind her three other agents slip away unnoticed in the confusion."),
            Spell("Polonium Umbrella", TargetType.EnemyCreatures,
                "A quick jab at a bus stop. A slow week in hospital.",
                "Burn 3 for 3 Days on target creature: 9 damage in all. Plain, slow removal with a spy's flair (the Markov umbrella).",
                Status(StatusEffectType.Burned, 3, TargetType.EnemyCreatures, potency: 3))
                .Picture("A gentleman in a bowler hat at a London bus stop pressing the tip of his black umbrella into a stranger's calf, a faint green glow at the tip, the stranger reading his newspaper."),
            Creature("Nonchalant Agent", 3, 2, speed: 1, deploy: 0,
                "Never draws first. Never draws second either.",
                "Sucker Punch: its attack on a creature goes before every other, but only if that creature planned an attack; otherwise it holds back. Read their plans (Fog of War makes it a gamble) and punch first.")
                .With(CreatureAbility.SuckerPunch)
                .Picture("An agent in a tuxedo leaning on a casino bar sipping a martini without looking, his free hand already flicking a pistol from his sleeve at a henchman who has only half drawn."),
        };

        // Tech: rewires the queue
        private static List<CardDef> TechCards() => new List<CardDef> {
            // The second Pokémon batch (the user, 2026-10-17): Neutralizing Gas, Trick Room
            Creature("EMP Drone", 1, 4, speed: 2, deploy: 0,
                "No signal. No abilities. No complaints: nobody can call to complain.",
                "Jamming Field (Pokémon's Neutralizing Gas): every other creature's ability is switched off while it stands, on either side. Yours too.")
                .With(CreatureAbility.JammingField)
                .Picture("A quadcopter drone hovering over the battlefield emitting concentric rings of static, every creature below holding a phone with no bars, their glowing ability icons flickering out one by one."),
            Spell("Buffering", TargetType.Player,
                "99%. 99%. 99%. 99%.",
                "Trend for 3 Days (Trick Room, Tech's flavour): Rush Hour, the slowest act first.",
                SetTrend(Trend.RushHour, 3))
                .Picture("A battlefield frozen under a giant spinning loading wheel, every creature caught mid-leap with a BUFFERING 99% label, the one dial-up modem in the corner going first."),
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
                "Each Dawn it cleanses your creatures' statuses, all of them (Tech owns Cleanse; since 2026-10-15 a Cleanse takes the good ones too).",
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
            // The user, 2026-10-16: more cards for the smaller factions, leaning on the mechanics there are few of (leapers,
            // swappers, healers, tanks) and on combinations
            Creature("Threat Hunter", 2, 4, speed: 2, deploy: 1,
                "Assume breach. Then go looking for it.",
                "Homing (the user, 2026-10-16): its attacks lock on the enemy creature in the slot it's aimed at and follow it if it moves. Arrival: Heat Signature 1 on a random enemy creature, which then pulls every Homing effect, its own included: aim at anything and the hottest one takes it. Tech's Heat and Homing in one card.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.HeatSignature, 1, TargetType.EnemyCreatures, potency: 1, modifier: AtRandom)))
                .Homing()
                .NotInStarterDeck()
                .Picture("A security analyst in a headlamp crawling on hands and knees through a tunnel of server cables with a magnifying glass, following a trail of glowing red footprints to a tiny hooded hacker hiding behind a rack."),
            Spell("Ctrl + Z", TargetType.FriendlyCreatures,
                "Undo. Undo. Undo. Undo the undo.",
                "A friendly creature goes back to your hand (with the next Day's hand), and you draw 1 more next Day. Played again, its Arrival happens again, and it comes back fresh: a reset for a hurt creature, a second Arrival for one with a good one (Threat Hunter, Sex Clanker). Unlike Shadow Ops.' Extraction, no new face, a card instead.",
                Act(ActionType.ReturnToHand, 0, TargetType.FriendlyCreatures, TargetModifier.None), Draw(1))
                .NotInStarterDeck()
                .SetUpBy("Threat Hunter", "Sex Clanker")
                .Picture("A huge curved UNDO arrow pulling a startled knight backwards off a battlefield into a playing card held up by a hand, the keys CTRL and Z glowing on a keyboard below."),
            Spell("Pair Programming", TargetType.FriendlyCreatures,
                "One types, one watches. Both bill the hours.",
                "A swapper: target friendly creature trades places with its partner in the formation, then it and the creatures beside it where it lands get +1 Attack this Day. Lines up a pair on one lane, or pulls a hurt creature out of one.",
                SwapPositions(TargetType.FriendlyCreatures), Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, TargetModifier.WithAdjacent, days: 1))
                .NotInStarterDeck()
                .Picture("Two developers squeezed at one tiny desk sharing a single keyboard: one types with a coffee in his other hand, the other leans over his shoulder pointing at a bug on the screen, a timesheet pinned between them billing 16 hours."),
            Creature("Suspicious USB Stick in the Parking Lot", 0, 1, speed: 1, deploy: 0,
                "SALARIES 2026. Do not open. Definitely open.",
                "A Gift (Stuxnet reportedly got into Iran's nuclear plant on USB sticks): you play it into an empty enemy slot and it joins their side. On arrival, a random other creature of its new side is Exhausted (infected), and it carries Heat Signature 1, pulling their random effects and your Homing ones.",
                On(EffectTrigger.OnPlay, Stun(1, TargetType.FriendlyCreatures, AtRandom | Others), Status(StatusEffectType.HeatSignature, 1, TargetType.Self, potency: 1)))
                .Gift()
                .NotInStarterDeck()
                .Picture("A lone USB stick lying on the asphalt of an empty parking lot under a streetlight, labelled SALARIES 2026 in marker, an employee in a lanyard bending down to pick it up, a nuclear plant's cooling tower behind the fence."),
            // The meta picks (the user, 2026-10-16: "implement all of your ideas", from the Pokémon and Slay the Spire report):
            // Ripening, Laid Off, Toxic (merged into Burn since) and the statuses that help their owner, Grudge, side hazards, Sucker Punch, Hit and Run,
            // Policies, Shield Bash, Strip Search, On Trend. Names from the Brainstorm List
            Spell("Code Review", TargetType.Player,
                "LGTM. (Didn't read it.)",
                "The sweeper (Rapid Spin, Defog): clears the Slot Effects on every one of your slots (Border Crossing, Landmines, Scorched Earth), and you draw a card next Day.",
                SideSweep(TargetType.FriendlyCreatures), Draw(1))
                .Picture("Two developers at a desk, one pointing at a screen full of red warning markers, the other shrugging and clicking 'Approve' with a coffee in hand."),
            Creature("Bomber Drone", 3, 2, speed: 3, deploy: 0,
                "Delivered in 30 minutes, or your village is free.",
                "Hit and Run: after its attack it goes back to your hand, fresh, with the next Day's. It strikes and is gone before the answer comes.")
                .With(CreatureAbility.HitAndRun)
                .Picture("A quadcopter drone with a delivery company's logo dropping a small bomb on a tank and zipping away, a 5-star rating popping up on its operator's phone below."),
            // The next picks (the user, 2026-10-16): Gear (held items), Provoked (Taunt), Combo (chaining spells)
            Spell("Ctrl + Y", TargetType.EnemyCreatures,
                "Redo. And again. And again.",
                "Combo: 1 damage to target creature or player, 1 more for each other spell you play this Day. Tech's spells pile up into a finisher.",
                Damage(1, TargetType.EnemyCreatures))
                .Combo()
                .Picture("A keyboard with the Y key worn down to a nub, a hand mashing Ctrl + Y while the same explosion repeats on the screen behind it."),
        };

        // Myth: faith, death and prophecy. Creatures that rise again (Revive) or die for the cause (Doomed), sacrifices that pay
        // for miracles (Sacrifice), prophecies that come true Days later (Prophecy), converts (control change), and wrath on
        // the unbelievers (Holy War). Pays with its own creatures; can't move them.
        private static List<CardDef> MythCards() => new List<CardDef> {
            // The second Pokémon batch (the user, 2026-10-17): Leech Seed, Berserk, Perish Body, the Holy Pigeon, Trick Room
            Spell("Tithe", TargetType.EnemyCreatures,
                "Ten percent, every Sunday, forever. God's own subscription.",
                "Leech Seed (the user, 2026-10-17): Leech 1 for 3 Days on target enemy creature: at each Dusk it takes 1 and you heal 1.",
                Status(StatusEffectType.Leeched, 3, TargetType.EnemyCreatures, potency: 1))
                .Picture("A collection plate on a very long pole reaching from a gilded pulpit all the way across the battlefield into an enemy creature's pocket, coins flowing back along it into a fat bishop's lap."),
            Creature("Berserker", 3, 6, speed: 2, deploy: 0,
                "Pain is just the body's way of asking for more.",
                "Berserk: +3 Attack at half Health or less. The worse it gets, the better he does.")
                .With(CreatureAbility.Berserk)
                .Picture("A bare-chested Norse warrior with a bear pelt over his head, arrows sticking out of him like a pincushion, grinning wider with each one, swinging an axe in each hand, foam on his beard."),
            Creature("Tutankhamun", 2, 7, speed: 1, deploy: 1,
                "Death shall come on swift wings to him who disturbs the peace of the king. Swift-ish.",
                "Pharaoh's Curse (Pokémon's Perish Body): a creature that attacks it is Doomed 3, and so is the pharaoh (the Carnarvon curse: Mummy, the Hive Mind test creature, keeps its name). Three Days of growing dread for both.")
                .With(CreatureAbility.PharaohsCurse)
                .Picture("A bandaged mummy sitting up in its sarcophagus pointing a bony finger at a tomb raider in a pith helmet, both of them with a little hourglass over their heads showing the same three grains of sand."),
            Creature("Holy Pigeon", 1, 3, speed: 3, deploy: 0,
                "The Holy Spirit, as depicted. Please do not feed it.",
                "Leapfrog (the user, 2026-10-17: 'make also the holy pigeon'): it flies over creatures to an empty slot. Arrival: Bless 1 on every friendly creature: a ward against the next status.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Blessed, 0, TargetType.FriendlyCreatures, potency: 1, modifier: Each)))
                .With(MovementKeyword.Leapfrog)
                .Picture("A fat grey city pigeon with a golden halo and two tiny angel wings perched on a cathedral gargoyle, a beam of holy light on it, a crumb of communion wafer in its beak, a saint's statue below it freshly anointed."),
            Spell("The Last Shall Be First", TargetType.Player,
                "And the first shall wait their turn, like everyone else.",
                "Trend for 3 Days (Trick Room, Myth's flavour, after the verse): Rush Hour, the slowest act first.",
                SetTrend(Trend.RushHour, 3))
                .Picture("The pearly gates with a velvet rope and a bouncer angel waving through a line of old beggars, cripples and a snail while a procession of kings and CEOs with golden tickets is held at the back."),
            // The Pokémon picks (the user, 2026-10-17): Wish as Thoughts and Prayers, Wonder Guard as the Holy Ghost
            Spell("Novena", TargetType.FriendlyCreatures,
                "Nine days of prayer. Delivery on the tenth.",
                "Wish (the user, 2026-10-17): a Prayer on target friendly creature's slot for 2 Days: when they run out, at the end of the next Day, whoever stands in that slot heals 6. Help that arrives late, for whoever is still there to receive it.",
                PlaceSlotEffect(SlotEffectType.Prayer, 6, 2, TargetType.FriendlyCreatures))
                .Picture("A hospital bed with a bandaged creature in it, a GET WELL SOON balloon drifting in through the window two days late, and outside, a crowd of politicians with their hands clasped and eyes closed, each holding a phone showing the hashtag #thoughtsandprayers."),
            Creature("Holy Ghost", 3, 1, speed: 2, deploy: 1,
                "Thou canst not touch what thou canst not see. Thou canst, however, light a candle.",
                "Wonder Guard (the user, 2026-10-17: 'would be better for holy'): Immaculate. Attacks and effects do nothing to it; only a status (Burn) hurts it, and with 1 Health any Burn is the end. Doomed and Destroy still take it.")
                .With(CreatureAbility.Immaculate)
                .Picture("A translucent white sheet-ghost with a golden halo and two round eye holes, hovering serenely in a church aisle while arrows and bullets pass straight through it and clatter on the floor behind, a dove on its head, and in the corner a single lit match it eyes with real fear."),
            Creature("Taqiyya", 2, 2, speed: 1, deploy: 2,
                "The smile is sincere. The rest is between me and God.",
                "The user, 2026-10-16: a poisoned gift. Played on your side, it deploys for 2 Days; on arrival it goes over to the enemy, into the slot opposite, and whatever stands there is discarded. There it's a Turncoat: no summon can replace it and they can't order it to attack; each Dawn it attacks a random other creature of its new side, Reveals another (you see its plans) and takes 1 Attack off another. Low Health: they can knock it out easily (Friendly Fire, an effect).",
                On(EffectTrigger.OnPlay, Act(ActionType.Defect, 0, TargetType.Self, TargetModifier.None)),
                On(EffectTrigger.StartOfTurn,
                    Strike(TargetType.FriendlyCreatures, AtRandom | Others),
                    Status(StatusEffectType.Revealed, 1, TargetType.FriendlyCreatures, modifier: AtRandom | Others),
                    Buff(-1, TargetType.FriendlyCreatures, attack: true, health: false, AtRandom | Others)))
                .With(CreatureAbility.Turncoat)
                .NotInStarterDeck()
                .Picture("A serene robed figure bowing slightly and offering an ornate gift box tied with a gold ribbon, both hands extended in welcome; on the wall behind, the box's shadow is a coiled snake."),
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
            Creature("Mummy", 2, 6, speed: 1, deploy: 1,
                "Touch the tomb, get the curse. It was in the fine print.",
                "Mechanics test: Hive Mind. A creature that attacks it gains Hive Mind, replacing its own ability.")
                .With(CreatureAbility.HiveMind)
                .NotInStarterDeck()
                .Picture("A museum mummy shaking hands with a smiling tourist, its bandages unravelling and winding up the tourist's arm, a \"Please Do Not Touch\" sign knocked to the floor."),
            Creature("Omniscient Space-Time Bending Bearded Wizard", 3, 9, speed: 1, deploy: 2,
                "Works in mysterious ways. Mostly late.",
                "Each Dawn, Blessed on a random friendly creature.",
                On(EffectTrigger.StartOfTurn, Status(StatusEffectType.Blessed, 0, TargetType.FriendlyCreatures, potency: 1, modifier: AtRandom | Others)))
                .NotInStarterDeck()
                .Picture("An old bearded man on a cloud in a bathrobe and slippers, lazily pointing a TV remote at a tiny spinning universe."),
            Creature("Cultist", 2, 2, speed: 2, deploy: 0,
                "The end is near. Brunch is at eleven.",
                "Fodder: when it dies, every friendly creature gets +1 Attack for good. The first to go when a Sacrifice is paid.",
                On(EffectTrigger.OnDeath, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, Each)))
                .Picture("A hooded cultist holding a THE END IS NEAR sign in one hand and a pumpkin spice latte in the other."),
            Creature("Phoenix", 3, 2, speed: 2, deploy: 1,
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
                "Destroy target enemy creature; your weakest creature is sacrificed to play it. Black magic always takes something. With a Phoenix Feather the same Day, the feathered creature pays (one offering, every rite), and still rises.",
                Destroy(TargetType.EnemyCreatures))
                .Sacrificing(1)
                .SetUpBy("Phoenix Feather")
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

            Creature("Templar Knight", 3, 5, speed: 1, deploy: 1,
                "Took a vow of poverty. Owns half of Europe.",
                "When ready: Blessed 2 on itself, so the next two statuses aimed at it are wasted.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Blessed, 0, TargetType.Self, potency: 2)))
                .Picture("A knight in chainmail and a white mantle with a red cross, sword raised, standing in front of a medieval bank counter piled with gold coins and promissory notes."),
            Creature("Zealot", 4, 3, speed: 2, deploy: 0,
                "Will die for the cause. Will tell you about it first.",
                "When ready: Doomed 3 on itself. It grows +1 Attack each Dawn, then its Last Rites strike and it dies for the cause.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Doomed, 3, TargetType.Self)))
                .Picture("A wild-eyed hermit in sackcloth with a long beard, waving a burning torch and a hand-painted REPENT sign on a busy city sidewalk."),
            Creature("Vampire", 2, 4, speed: 1, deploy: 1,
                "Doesn't drink. Just takes a little off the top. Every night.",
                "Strength Sap (the user, 2026-10-15), kept fair: each Dawn the enemy creature facing it loses 1 Attack while it stays in play, and the Vampire heals 1. Only the one in front of it, so stepping out of its lane stops it; Allergic to Light, the Reveal it fears.",
                On(EffectTrigger.StartOfTurn, Buff(-1, TargetType.EnemyCreatures, attack: true, health: false, Facing), Heal(1, TargetType.Self)))
                .With(CreatureAbility.AllergicToLight)
                .Picture("A pale aristocrat in a cape at a blood-donation clinic, sipping from a sleeping gym bro's arm through a bendy straw, the bro's bicep visibly deflating like a balloon."),
            Creature("Living Dead", 3, 3, speed: 1, deploy: 0,
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
            Creature("Buddy Christ", 2, 5, speed: 1, deploy: 0,
                "Winks. Points. Forgives.",
                "Each Dawn, every friendly creature heals 1.",
                On(EffectTrigger.StartOfTurn, Heal(1, TargetType.FriendlyCreatures, Each | Others)))
                .Picture("A smiling plastic Jesus statue winking and giving a big thumbs up and a finger point, on a church lawn next to a sign reading CATHOLICISM: WOW!"),
            Creature("Pope", 1, 7, speed: 1, deploy: 1,
                "Infallible. Bulletproof glass, just in case.",
                "Each Dawn, Blessed 1 on your strongest creature.",
                On(EffectTrigger.StartOfTurn, Status(StatusEffectType.Blessed, 0, TargetType.FriendlyCreatures, potency: 1, modifier: StrongestOne | Others)))
                .Picture("The Pope waving from a glass-domed popemobile, sprinkling holy water from a garden hose over a crowd of creatures."),
            Creature("The Lobster Professor", 1, 6, speed: 1, deploy: 1,
                "Clean your room, bucko. Then buy the seminar.",
                "Each Dawn, your strongest creature gets +1 Attack for as long as it's in play: the dominance hierarchy, as preached.",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, StrongestOne | Others)))
                .Picture("A gaunt professor in a three-piece suit lecturing tearfully at a lectern, pointing at a projected slide of a lobster sitting on a throne, the audience taking notes, a messy bedroom visible through a door behind him."),
            Spell("Amen", TargetType.Player,
                "And so it is. Go in peace. Now.",
                "Amen (the user, 2026-10-16): it ends the Day when it resolves, with the spells: every other action still in the queue, both sides', is discarded (their cards go to the discard pile), so no attack lands that Day. Single Use: it burns out.",
                Act(ActionType.EndDay, 0, TargetType.Player, TargetModifier.None))
                .SingleUse()
                .Picture("A choir of robed monks in a stone chapel snapping a giant hymn book shut with a thunderclap, every candle in the room blowing out at once, a single wisp of smoke curling up from each."),
            Creature("Pyramids", 0, 10, speed: 1, deploy: 2,
                "Built by aliens, apparently. Or by paid workers on beer rations.",
                "Pyramids (the user, 2026-10-16): a monument, not a fighter. 0 Attack, 10 Health, two Days to build. Safe Space: each Dawn it and the friendly creatures beside it gain Bless, the pharaoh's eternal protection. Myth holds the status quo.")
                .With(CreatureAbility.SafeSpace)
                .RootedWhileDeploying()
                .Picture("The three pyramids of Giza at golden sunset, the biggest one's capstone glowing with a holy light that rains down on two tiny knights standing beside it, a bored camel in the foreground."),
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
                "Target friendly creature is sacrificed (its Last Will happens; a Blessing can't stop it) and rises again at the Dawn 3 Days later, ready, full Health. Three days, as the scripture says. Myth's loophole: its offering pays a Necronomicon's Sacrifice the same Day, one death for both.",
                Act(ActionType.Resurrect, 3, TargetType.FriendlyCreatures, TargetModifier.None))
                .Picture("Only the feather, watercolour on paper: red at the quill through orange to a yellow heart, its barbs licking out like flames, a glowing eye near the top, the tip burning out lilac, sparks all round."),
            Creature("The Omen", 1, 4, speed: 1, deploy: 2,
                "He's not a bad kid. He's the Antichrist.",
                "When ready: Doomed 2 on the strongest enemy creature.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Doomed, 2, TargetType.EnemyCreatures, modifier: StrongestOne)))
                .NotInStarterDeck()
                .Picture("A small boy in a neat suit on a tricycle in a long mansion hallway, staring straight at the viewer, a black dog sitting beside him and a 666 birthmark peeking from his hairline."),
            // The user, 2026-10-16: the cards this faction's decks needed most, from the balance study (sturdy bodies, reliable
            // damage and removal, board damage, Armor and healing: what wins; see Faction Balance Plan)
            Creature("Grim Reaper", 4, 5, speed: 2, deploy: 1,
                "Not today. Probably.",
                "A body that kills on arrival: 3 damage to the creature facing it. Myth's creatures were slow and soft; this one isn't.",
                On(EffectTrigger.OnPlay, Damage(3, TargetType.EnemyCreatures, Facing)))
                .Picture("The Grim Reaper in his black hood waiting in a hospital corridor, checking his watch, a number ticket in his bony hand, a sign above him reading NOW SERVING 42."),
            Spell("Blessing in Disguise", TargetType.FriendlyCreatures,
                "It looked like a disaster. It was a disaster. But a blessed one.",
                "Myth holds the board (a status quo faction): every friendly creature heals 3 and gains Bless 1.",
                Heal(3, TargetType.FriendlyCreatures, Each), Status(StatusEffectType.Blessed, 1, TargetType.FriendlyCreatures, potency: 1, modifier: Each))
                .Picture("The farmer's story: his horse has run away and the neighbours cry WHAT BAD LUCK!; the old farmer in a straw hat shrugs, MAYBE, a soft halo over him, while over the hill his horse is already coming back with a herd of wild horses."),
            Spell("Die by the Sword", TargetType.EnemyCreatures,
                "Live by it, too, if you can.",
                "Removal with a reward: 4 damage to target creature, and if it dies, you heal 3.",
                Damage(4, TargetType.EnemyCreatures), OnlyIfItDies(Heal(3, TargetType.Player)))
                .Picture("A knight run through by his own sword which bounced back off a mirror, a smug monk holding the mirror."),
            Spell("Mithril", TargetType.FriendlyCreatures,
                "Light as a feather. Hard as dragon scales. Worth a Shire.",
                "Armor 4 and Bless 1 on target creature: Myth's knights, made to last.",
                Armor(4, TargetType.FriendlyCreatures), Status(StatusEffectType.Blessed, 1, TargetType.FriendlyCreatures, potency: 1))
                .Picture("A silver chain-mail shirt glittering on a velvet cushion in a dwarven forge, a tiny hobbit trying it on and a dragon peering in through the window."),
            Creature("Here Be Dragons", 5, 7, speed: 1, deploy: 2,
                "The map ends here. The dragon doesn't.",
                "A big body: Arrival: 1 damage to every enemy creature. The edge of the old maps; its art is an ASCII dragon.",
                On(EffectTrigger.OnPlay, Damage(1, TargetType.EnemyCreatures, Each)))
                .Picture("An old parchment sea map ending in a ragged edge, beyond it a big dragon drawn in green ASCII characters on a black terminal screen, breathing fire made of # signs."),
            Spell("Gracias Jesús", TargetType.Player,
                "He brought the food in from the fields. You're welcome.",
                "You heal 3 and draw 2 next Day: the meme of Jesus carrying the harvest in.",
                Heal(3, TargetType.Player), Draw(2))
                .Picture("Jesus in a straw hat carrying two big baskets of vegetables in from the fields at sunset, a family at the farmhouse table clapping."),
            // The meta picks (the user, 2026-10-16: "implement all of your ideas", from the Pokémon and Slay the Spire report):
            // Ripening, Laid Off, Toxic (merged into Burn since) and the statuses that help their owner, Grudge, side hazards, Sucker Punch, Hit and Run,
            // Policies, Shield Bash, Strip Search, On Trend. Names from the Brainstorm List
            Creature("Heretic", 3, 5, speed: 2, deploy: 0,
                "Said the Earth was round. Got the stake. Said it louder.",
                "Martyrdom: while Doomed, everything it deals is doubled. Its Arrival Dooms it 3 Days: three Days of double hits, growing each Dawn, then it burns. Doomed finally has an owner who wants it.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Doomed, 3, TargetType.Self)))
                .With(CreatureAbility.Martyrdom)
                .Picture("A heretic tied to a stake on a pile of firewood, laughing, swinging a broken chain at the inquisitors as the first flames catch, a placard reading 'The Earth is round' nailed above him."),
            Spell("Ambrosia", TargetType.FriendlyCreatures,
                "Food of the gods. Gluten-free, apparently.",
                "Ripening: heal 2 and +1 Attack to target creature, 1 more of each for each Dawn it's kept in your hand (up to heal 5 and +4). Myth's patience.",
                Heal(2, TargetType.FriendlyCreatures), Buff(1, TargetType.FriendlyCreatures, attack: true, health: false))
                .Ripening()
                .Picture("A golden bowl of glowing honey on an Olympus banquet table, Zeus in sunglasses dipping a finger in, the jar's label reading 'Organic, Artisanal, Divine'."),
            Spell("Publicly Funded Faith Schools", TargetType.Player,
                "Taxpayers fund the prayers. God funds nothing.",
                "Policy: Bless 1 on a random friendly creature, and again at each Dawn, for the rest of the combat. Blessed (it wards off the next status or destroy) finally piles up.",
                Status(StatusEffectType.Blessed, 1, TargetType.FriendlyCreatures, potency: 1, modifier: AtRandom))
                .Policy()
                .Picture("A classroom of kids in uniforms praying over their exam papers instead of writing, a nun at the front under a 'Funded by your taxes' banner hung over the crucifix."),
        };

        // Send For: a card from the deck (of this kind, among its top cards if top > 0) joins the next hand
        private static EffectAction SendForCard(SendForFilter filter, int top = 0, bool fromDiscard = false) {
            var action = Act(ActionType.SendFor, top, TargetType.Player, TargetModifier.None);
            action.sendFor = filter;
            action.fromDiscard = fromDiscard;
            return action;
        }

        // Revive: once per combat, its own Last Will raises it at the next Dawn
        private static CardEffect Revive() => OncePerCombat(On(EffectTrigger.OnDeath, Act(ActionType.Revive, 0, TargetType.Self, TargetModifier.None)));

        // Mind: wins without raw damage
        private static List<CardDef> MindCards() => new List<CardDef> {
            // The second Pokémon batch (the user, 2026-10-17): Sleep Talk, Synchronize, Haze (area and target), Trick Room
            Creature("Sleepwalker", 3, 4, speed: 2, deploy: 0,
                "Do not wake it. Do not stand in front of it either.",
                "Autopilot (Pokémon's Sleep Talk): Exhausted, it still attacks: a random enemy slot. A stun doesn't stop it, it just stops aiming.")
                .With(CreatureAbility.Autopilot)
                .Picture("A man in striped pyjamas and a nightcap sleepwalking across a battlefield with his arms out, eyes shut, a candle in one hand, swinging a pillow that knocks a helmeted soldier clean off his feet."),
            Creature("Empath", 2, 5, speed: 2, deploy: 0,
                "I feel your pain. Now you feel mine. It's only fair.",
                "Empathy (Pokémon's Synchronize): an enemy status that lands on it lands on the creature that sent it too; from a spell, on a random enemy creature.")
                .With(CreatureAbility.Empathy)
                .Picture("A woman in a cardigan with her hand on her heart, tears in her eyes, mirrored exactly by the enemy soldier opposite who has suddenly burst into the same tears, both of them on fire from the same Burn."),
            Spell("Tabula Rasa", TargetType.AllCreatures,
                "Blank slate. Everyone back to who they were printed as.",
                "Haze, the area (the user, 2026-10-17): every creature on the board loses its stat changes, raises and cuts alike. Statuses stay.",
                Cleanse(CleanseTarget.StatModifications, TargetType.AllCreatures))
                .Picture("A giant blackboard eraser sweeping across a battlefield drawn in chalk, the creatures' scribbled +2s and -1s vanishing into white dust, a philosopher in a toga holding the eraser."),
            Spell("Reality Check", TargetType.AllCreatures,
                "You're not that strong. You were never that strong. Sit down.",
                "Haze, on a target (the user, 2026-10-17): target creature loses its stat changes, raises and cuts alike. Statuses stay.",
                Cleanse(CleanseTarget.StatModifications, TargetType.AllCreatures))
                .Picture("A puffed-up bodybuilder flexing in front of a mirror, and in the mirror a skinny man in the same pose; a therapist beside him tapping the glass with a pen."),
            Spell("Slow Thinking", TargetType.Player,
                "System 2 would like a word. It will take a while.",
                "Trend for 3 Days (Trick Room, Mind's flavour, after Kahneman): Rush Hour, the slowest act first.",
                SetTrend(Trend.RushHour, 3))
                .Picture("A chess clock between a hare bouncing impatiently and a tortoise in reading glasses still considering its first move, the hare's clock run out, the tortoise's barely started, a referee raising the tortoise's arm."),
            // The Pokémon picks (the user, 2026-10-17): Wonder Room as a Liminal Space, Ditto as Monkey See, Monkey Do
            Spell("Liminal Space", TargetType.Player,
                "You have been here before. You have never been here. The carpet remembers you.",
                "Trend for 2 Days (the user, 2026-10-17: Attack and Health swap): every creature's Attack and Health trade places, the ones on the board now and each one that enters while it trends; when it ends they trade back. Walls become cannons and cannons walls, for two Days.",
                SetTrend(Trend.LiminalSpace, 2))
                .Picture("An endless empty office corridor in sickly yellow light, damp beige carpet, humming fluorescent tubes, one door ajar onto another identical corridor, a lone figure far away with its head turned the wrong way round, no exit anywhere."),
            Spell("Monkey See, Monkey Do", TargetType.EnemyCreatures,
                "Imitation is the sincerest form of having no ideas.",
                "Ditto (the user, 2026-10-17): a fresh copy of target enemy creature, straight from its card, appears in the slot facing it on your side; whatever stood there is discarded. Their best creature, now yours too, staring at itself. The copy is a token.",
                Act(ActionType.Mimic, 0, TargetType.EnemyCreatures, TargetModifier.None))
                .Picture("A chimpanzee in a lab coat and a scientist in a lab coat facing each other through an empty mirror frame, each copying the other's pose exactly, clipboard for clipboard, down to the raised eyebrow; a banana on the scientist's clipboard."),
            Spell("Lucid Dreaming", TargetType.Player,
                "You know it's a dream. You still can't find the bathroom.",
                "Mind's scouting: send for any card among the top 3 of your deck; it joins your next hand.",
                SendForCard(SendForFilter.Any, top: 3))
                .Picture("A sleeper floating above their bed in pyjamas, calmly picking cards out of a swirling dreamscape of melting clocks and staircases."),
            Spell("Momentum", TargetType.FriendlyCreatures,
                "Once you get going you can't stop. That was the plan. Stopping wasn't.",
                "Tailwind (the user, 2026-10-15: cards like Pokemon's Tailwind): +1 Speed to all your creatures for 3 Days. Spells land before attacks, so today's attacks already go sooner.",
                Buff(1, TargetType.FriendlyCreatures, attack: false, health: false, Each, days: 3, speed: true))
                .Picture("A man in a business suit sprinting downhill, legs a cartoon blur, a gale of motivational posters (HUSTLE, NEVER STOP) blowing at his back, the edge of a cliff just ahead."),
            Spell("Survivorship Bias", TargetType.FriendlyCreatures,
                "Every plane that came back had holes in the same places. Armor those, said everyone who never saw the others.",
                "Focus Sash (the user, 2026-10-15: cards like Pokemon's Focus Sash): Survivor on target friendly creature: from full Health, a hit that would kill it leaves it at 1, once. Hurt first, and it doesn't help.",
                Status(StatusEffectType.Survivor, 1, TargetType.FriendlyCreatures))
                .Picture("A WWII bomber back on the runway, riddled with red dots of bullet holes in the famous diagram's pattern, its smug pilot on the wing giving a thumbs-up with one eyebrow singed off."),
            Spell("Yesn't", TargetType.Player,
                "It happened. It also didn't. Both are true until you stop thinking about it.",
                "Mind's denial (the user, 2026-10-15): a Watchdog that nullifies the next 2 enemy spells that deal damage, Monitored, so it catches ones planned before it. Narrower than Antivirus (any spell, 1 charge), so 2 charges: the blow lands and doesn't, and the mind picks doesn't.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Spell, side = WatchdogSide.Enemy, effect = WatchdogEffect.Nullify, charges = 2, damaging = true }, TargetType.Enemy))
                .Picture("A man calmly sipping tea while a fireball hits him, drawn twice over itself like a double exposure: in one he is burnt to a crisp, in the other untouched, a thought bubble above him reading YES / NO with both boxes ticked."),
            Spell("Cyberpsychosis", TargetType.EnemyCreatures,
                "Too much chrome, too little self. The feed was the last thing holding it together.",
                "2 damage to target enemy creature, and 3 more if it's Digital Society: the terminally online crack first (the user, 2026-10-15).",
                Damage(2, TargetType.EnemyCreatures), Requiring(Damage(3, TargetType.EnemyCreatures), TargetRequisite.Online))
                .Picture("A man in a hoodie with glowing cables plugged into the back of his skull, eyes spinning with notification badges, screaming at a phone that screams back, sparks flying from his chrome arm."),
            Spell("Reflect", TargetType.AllCreatures,
                "Mirror neurons: you yawn, I yawn. You panic, I panic.",
                "Target creature copies every status of the creature facing it, good and harmful, with the Days they have left; that one keeps its own. Like Mimetic Theory, it works between the creature it's dropped on and the one facing it, so there's one target to pick (the user, 2026-10-15).",
                Act(ActionType.CopyStatuses, 0, TargetType.AllCreatures, TargetModifier.None))
                .Picture("A woman in front of a tall mirror, her reflection wearing a different man's bandages, halo and storm cloud: in the room he stands beside her with exactly those, and now she has them too."),
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
            // A decoy tank (the user, 2026-10-17; it was a Watchdog spell): hidden, hot, and costly to find out
            Creature("Red Herring", 1, 7, speed: 1, deploy: 0,
                "Look over there. No, the fish.",
                "Undercover: it enters as ??? to the enemy. Its Arrival puts Heat Signature 2 on itself, so random effects, Homing ones and redirected attacks go to it. When Revealed: whoever revealed it is Exhausted; revealed by a spell, the enemy draws 1 fewer card with the next hand. Chasing it costs them their momentum.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.HeatSignature, 1, TargetType.Self, potency: 2)),
                On(EffectTrigger.OnRevealed, Stun(1, TargetType.Enemy), Act(ActionType.CutDraws, 1, TargetType.Enemy, TargetModifier.None)))
                .With(CreatureAbility.Undercover)
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
            // The user, 2026-10-16: more cards for the smaller factions (tanks, healers, swappers, combinations)
            Creature("Socrates", 1, 6, speed: 1, deploy: 1,
                "I know that I know nothing. You, apparently, know even less.",
                "A tank that asks questions: each Dawn the enemy creature facing it is Revealed for the Day (the Socratic method: its plans show). No damage, which Mind can't deal; it just stands there, hard to kill, and makes the creature in front of it explain itself.",
                On(EffectTrigger.StartOfTurn, Status(StatusEffectType.Revealed, 1, TargetType.EnemyCreatures, modifier: Facing)))
                .NotInStarterDeck()
                .Picture("Socrates in a white toga, bald with a big beard, cornering a sweating man in a business suit against a marble column with one raised finger, a huge question mark in his speech bubble, a cup of hemlock waiting on a pedestal behind."),
            Spell("Maslow's Pyramid", TargetType.Player,
                "Food, shelter, safety, love, esteem. And WiFi, underneath all of it.",
                "A healer, double-edged as Mind's cards are: every creature on the board heals 3, theirs as well as yours. Best when yours are the hurt ones.",
                Heal(3, TargetType.AllCreatures, Each))
                .NotInStarterDeck()
                .Picture("Maslow's pyramid as a great stone pyramid in layers, tiny people at the bottom fighting over bread and water, one man meditating smugly alone at the glowing top, and a WiFi symbol carved into a new layer under the base."),
            Spell("Legerdemain", TargetType.EnemyCreatures,
                "Watch the left hand. No, the other left.",
                "A swapper for the other side (telekinesis, sleight of hand): target enemy creature trades places with its partner in the formation, so their attacks and spells aimed at a slot find someone else there, and you draw 1 next Day. Moves resolve first, so it can undo a plan they made today.",
                SwapPositions(TargetType.EnemyCreatures), Draw(1))
                .NotInStarterDeck()
                .Picture("A magician's white-gloved hands doing the cups-and-ball trick on a velvet table, but under the cups are tiny soldiers being shuffled around, one cup lifted to show the soldier is gone."),
            // The meta picks (the user, 2026-10-16: "implement all of your ideas", from the Pokémon and Slay the Spire report):
            // Ripening, Laid Off, Toxic (merged into Burn since) and the statuses that help their owner, Grudge, side hazards, Sucker Punch, Hit and Run,
            // Policies, Shield Bash, Strip Search, On Trend. Names from the Brainstorm List
            Spell("Hold That Thought", TargetType.Player,
                "It'll be brilliant. Later. Much later.",
                "Ripening: draw a card next Day, 1 more for each Dawn it's kept in your hand (up to 4). The marshmallow test as a card.",
                Draw(1))
                .Ripening()
                .Picture("A man frozen mid-sentence with a finger raised, a glowing lightbulb above his head stuck behind a pause icon, everyone at the dinner table waiting."),
            Spell("Projection", TargetType.Player,
                "It's not my problem. It's yours. It was always yours.",
                "Court Change: the Slot Effects on each side trade places with the other side's, slot for slot. What they set up against you now waits for them.",
                Act(ActionType.SwapSlotEffects, 0, TargetType.Player, TargetModifier.None))
                .NotInStarterDeck() // A late card for a deep Mind deck (the user, 2026-10-17: kept, depth arrives gradually)
                .Picture("A man on a therapist's couch pointing at her, a film projector behind him throwing his own dark shadow-monster onto the wall behind her chair."),
            Creature("Psychiatrists", 3, 4, speed: 1, deploy: 0,
                "Your issues are fascinating. That'll be 200 an hour.",
                "Strip Search: its attack on a creature with a status or a stat change Purges it first and hits 2 harder. They strip away everything you were carrying.")
                .With(CreatureAbility.StripSearch)
                .Picture("Two psychiatrists in armchairs taking notes while a patient on the couch has his emotional baggage (suitcases labelled 'Mom', 'Ex', 'Work') taken away one by one."),
            // The next picks (the user, 2026-10-16): Gear (held items), Provoked (Taunt), Combo (chaining spells)
            Spell("Hyperbole", TargetType.EnemyCreatures,
                "The worst card ever printed in the history of the universe.",
                "Combo: target creature -1 Attack this Day, 1 more for each other spell you play this Day. Mind's words grow with every one said.",
                Buff(-1, TargetType.EnemyCreatures, attack: true, health: false, days: 1))
                .Combo()
                .Picture("A man telling a story at a bar with his arms spread wider and wider, the fish he's describing growing from a sardine to a whale in thought bubbles behind him."),
            // Maneuvers (the user, 2026-10-17): spells that only move creatures, in three flavours by whose: Ambivalent (either
            // side's), Disruption (the enemy's), Logistics (yours). Push and Pull: a step back or forward; Swap: with its partner;
            // Shuffle: a whole side at random. Names from the Brainstorm List
            Spell("Domino Effect", TargetType.AllCreatures,
                "One falls back, and everyone behind it finds out.",
                "Ambivalent Maneuver: push target creature, on either side, a step back (the Vanguard to a Flank, a Flank to its Headquarters slot); a creature already there trades places with it. Clear your own front, or knock theirs off it.",
                Act(ActionType.Push, 0, TargetType.AllCreatures, TargetModifier.None))
                .Picture("The housing market as dominoes: a row of houses for sale, price tags dropping, falling one into the next, the first tipped by a pointing finger marked RATES, the last landing on a banker spilling his coffee, a price chart crashing behind."),
            Spell("Controlled Chaos", TargetType.AllCreatures,
                "Everything is going exactly to plan. Which plan, though.",
                "Ambivalent Maneuver: every creature on the side it's played on changes slots at random. Scramble their formation, or roll the dice on yours.",
                Act(ActionType.Shuffle, 0, TargetType.AllCreatures, TargetModifier.DroppedSide))
                .Picture("A calm psychiatrist conducting an orchestra of patients who have all swapped instruments, the drummer on the violin, the sheet music flying through the air."),
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
            // The second Pokémon batch (the user, 2026-10-17): Pain Split, and Peer Review as a reveal
            Spell("Parabiosis", TargetType.AllCreatures,
                "Two mice, one bloodstream. Neither was consulted.",
                "Pain Split (the user, 2026-10-17): target creature and the creature facing it share their Health out equally (the odd point to the one that had less). Not damage, not healing: nothing answers it.",
                Act(ActionType.SplitPain, 0, TargetType.AllCreatures, TargetModifier.None))
                .Picture("Two hospital beds facing each other with a single blood bag hung between them, the tube running from a huge glowing-pink healthy creature to a grey wilted one, both ending up the same shade of medium pink."),
            Spell("Peer Review", TargetType.EnemyCreatures,
                "Interesting results. Shame they don't replicate.",
                "The user, 2026-10-17 (Peer Review kept for another archetype: a reveal plus an effect): reveal target enemy creature's slot, its plans and what hides there, and its findings are retracted: Blackout 2, its ability and effects switched off.",
                Reveal(TargetType.EnemyCreatures), Status(StatusEffectType.Blackout, 2, TargetType.EnemyCreatures))
                .Picture("Three reviewers in lab coats with magnifying glasses peering at a scientist's poster, red RETRACTED stamps hammering onto it, the scientist's hidden second poster behind it dragged into the light."),
            Spell("Geomagnetic Reversal", TargetType.EnemyCreatures,
                "North is south now. The compass apologises. The pigeons do not.",
                "The user, 2026-10-16: target creature's Attack and Health trade places, on either side. Its Attack becomes its Health as it is now, and its Health its Attack: a glass cannon becomes a wall, a wall a glass cannon, and a creature with no Attack is left with no Health and dies.",
                Act(ActionType.SwapStats, 0, TargetType.EnemyCreatures, TargetModifier.None))
                .NotInStarterDeck()
                .Picture("A globe with its magnetic field lines flipping over, the N and S labels swapping places mid-air, a scientist staring at a compass whose needle spins wildly, and a flock of birds flying in confused loops."),
            Spell("Research Lab", TargetType.Player,
                "Results in five years. Funding for two.",
                "Utopia's research: send for a spell from your deck; it joins your next hand.",
                SendForCard(SendForFilter.Spell))
                .Picture("A gleaming laboratory with scientists in white coats pulling glowing cards out of test tubes and a centrifuge, a grant deadline clock ticking on the wall."),
            Creature("Clone", 3, 3, speed: 2, deploy: 1,
                "Twice the sheep. Half the soul.",
                "When it enters, summon a random creature from your deck. Future: Clone (copies a friendly creature).",
                On(EffectTrigger.OnPlay, Summon(1)))
                .Picture("Dolly the sheep in a lab coat and goggles proudly presenting a row of identical sheep behind her."),
            Creature("Hypermetamorphosis", 2, 3, speed: 1, deploy: 1,
                "Caterpillar, cocoon, middle manager. Nature is healing.",
                "+1/+1 for good each Dawn. Future: Evolution (each step unlocks an ability).",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.Self, attack: true, health: true)))
                .Picture("Left to right: a caterpillar, a cocoon, and a hatched office worker in a tie with butterfly wings, briefcase in hand, dead inside."),
            Creature("Extremophile", 2, 5, speed: 1, deploy: 1,
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
            Creature("Mathematician", 1, 4, speed: 1, deploy: 1,
                "Showed her work. Nobody checked it.",
                "Each Dawn a random enemy creature gets -1 Attack for good.",
                On(EffectTrigger.StartOfTurn, Buff(-1, TargetType.EnemyCreatures, attack: true, health: false, AtRandom)))
                .NotInStarterDeck()
                .Picture("The confused math lady meme: a woman squinting while equations, triangles and integrals float around her head."),
            Creature("Quantum Physicist", 2, 4, speed: 2, deploy: 1,
                "In two places at once. Late to both.",
                "Swap. Whoever hits it is Exhausted: observing it collapses them.",
                On(EffectTrigger.OnDamage, Stun(1, TargetType.Enemy)))
                .With(MovementKeyword.Swap)
                .NotInStarterDeck()
                .Picture("A physicist split down the middle, one half of him see-through, holding two coffees and checking two watches."),
            Creature("Perpetual Motion Machine", 0, 6, speed: 1, deploy: 2,
                "Runs forever. The patent office stopped answering in 1911.",
                "A draw each Dawn, and it heals 1 each Dusk.",
                On(EffectTrigger.StartOfTurn, Draw(1)),
                On(EffectTrigger.EndOfTurn, Heal(1, TargetType.Self)))
                .Picture("A hamster running in a wheel that powers a fan, which blows on the hamster, which keeps running."),
            Creature("Particle Accelerator", 3, 7, speed: 1, deploy: 2,
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
            Creature("Surveillance Giraffe", 2, 7, speed: 1, deploy: 1,
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
                "Cleanses its statuses, all of them; side effect: -1 Attack for this Day (sore arm).",
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
                "Shield 3 on target creature: for 3 Days, counting this one, it takes no combat damage. It goes with the creature (the user, 2026-10-16: a slot can't have a shield).",
                Status(StatusEffectType.Shielded, 3, TargetType.FriendlyCreatures))
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
                "Gamble, accelerationism's way: every outcome pays and every one costs (the user: drawbacks, 2026-10-16). Your creatures take 2 and get +3 Attack for good (on the brink, then boom); or progress for everyone, +2/+2 for good to every creature, the enemy's too; or Borrowed Time this Day, then 3 damage to each: on the brink they deal double, and any left at 0 are back to 1 at Dusk (the user's Borrowed Time, 2026-10-16).",
                new[] { Damage(2, TargetType.FriendlyCreatures, Each), Buff(3, TargetType.FriendlyCreatures, attack: true, health: false, Each) },
                new[] { Buff(2, TargetType.AllCreatures, attack: true, health: true, Each) },
                new[] { Status(StatusEffectType.BorrowedTime, 1, TargetType.FriendlyCreatures, modifier: Each), Damage(3, TargetType.FriendlyCreatures, Each) })
                .Picture("A tech founder in a hoodie flooring the accelerator of a rocket-powered car toward a cliff edge, the road on fire behind him, a bridge of glowing circuit boards assembling itself in mid-air just ahead of the wheels."),
            Creature("Cold Fusion", 5, 6, speed: 1, deploy: 1,
                "Clean, limitless energy. Slight meltdown risk. Mostly fine.",
                "On the Brink: at 2 Health or less it deals double. The reactor runs hot: 1 damage to itself each Dusk.",
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self)))
                .With(CreatureAbility.Brink)
                .Picture("A tabletop fusion reactor glowing white-hot in a jar on a garage workbench, the warning gauge in the red, two engineers in sunglasses giving a thumbs-up."),
            Creature("Antimatter", 5, 5, speed: 2, deploy: 1,
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
            Creature("Scientific Method", 1, 5, speed: 1, deploy: 1,
                "Hypothesis, experiment, grant renewal.",
                "Each Dawn, +1 Attack for good on a random friendly creature (peer reviewed).",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, AtRandom | Others)))
                .Picture("A scientist in goggles carefully pouring a glowing liquid into a beaker on a bench crowded with labelled flasks, a whiteboard behind her reading HYPOTHESIS → EXPERIMENT → GRANT."),
            Creature("Frankenstein", 3, 6, speed: 1, deploy: 1,
                "It's alive! It's ALIVE! It's suing.",
                "An abomination that grows: +1/+1 for good each Dawn. Side effect: it arrives Burned for 2 Days (the lightning).",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Burned, 2, TargetType.Self, potency: 1)),
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.Self, attack: true, health: true)))
                .Picture("A towering stitched-together monster sitting up on a lab table under a lightning bolt, bolts in its neck smoking, the mad scientist cheering with both arms up."),
            Creature("Chimera", 4, 5, speed: 2, deploy: 1,
                "Lion, goat, snake. The ethics committee was on holiday.",
                "When ready: +2/+2 for good on a random friendly creature, maybe itself (a graft); side effect: it loses 2 Health for good.",
                On(EffectTrigger.OnPlay, Buff(2, TargetType.FriendlyCreatures, attack: true, health: true, AtRandom | Others), Buff(-1, TargetType.Self, attack: false, health: true)))
                .Picture("A lion with a goat's head sprouting from its back and a snake for a tail, all three wearing hospital ID wristbands, in a lab enclosure with a sticky note: DO NOT FEED AFTER MIDNIGHT."),
            Creature("Resurrected T-Rex", 6, 7, speed: 1, deploy: 1,
                "Sixty-six million years asleep. Woke up hungry.",
                "A huge abomination, On the Brink. Side effect: its cloned DNA is unstable, it loses 1 Health for good each Dusk, until it hits twice as hard.",
                On(EffectTrigger.EndOfTurn, Buff(-1, TargetType.Self, attack: false, health: true)))
                .With(CreatureAbility.Brink)
                .Picture("A T-Rex skeleton half covered in new flesh breaking out of a museum display, the DNA sequencer that brought it back still beeping, a guide pointing at the exit."),
            // The user, 2026-10-16: the cards this faction's decks needed most, from the balance study (sturdy bodies, reliable
            // damage and removal, board damage, Armor and healing: what wins; see Faction Balance Plan)
            Creature("Longevity Hub", 2, 6, speed: 1, deploy: 1,
                "Live forever. Subscription renews monthly.",
                "Utopia's healer (its primary mechanic): each Dawn every other friendly creature heals 2. Pairs with Brink creatures that hurt themselves.",
                On(EffectTrigger.StartOfTurn, Heal(2, TargetType.FriendlyCreatures, Each | Others)))
                .Picture("A gleaming white clinic in the desert where billionaires lie in pods drinking smoothies through tubes, a young intern on a treadmill in the corner wired to them by a blood line."),
            Spell("Femtosecond Laser", TargetType.EnemyCreatures,
                "Faster than a thought. Cleaner than a scalpel.",
                "Clean removal for Utopia: 4 damage to target creature or player, with no cost to you (Black Hole and Splitting The Atom both hurt you).",
                Damage(4, TargetType.EnemyCreatures))
                .Picture("A laser beam the thickness of a hair slicing a falling apple into perfect quarters in mid-air, a physicist in safety goggles timing it with a stopwatch."),
            Creature("Quark", 2, 3, speed: 2, deploy: 0,
                "Up, down, charm, strange. Mostly strange.",
                "A leaper that grows: Leapfrog, and Grow 1 (+1/+1 each Dawn, for good: persistent growth, Utopia's primary). Cheap, ready at once.",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.Self, attack: true, health: true)))
                .With(MovementKeyword.Leapfrog)
                .Picture("Three tiny glowing particles in colored hats bouncing on a trampoline inside an atom, one of them flipping off the edge with a grin."),
            Spell("Seasteading", TargetType.FriendlyCreatures,
                "No taxes. No laws. No lifeboats.",
                "Armor 3 on every friendly creature (Armor wins 65% in the study; Utopia's primary). A libertarian floating city.",
                Armor(3, TargetType.FriendlyCreatures, Each))
                .Picture("A floating hexagonal platform city in the middle of the ocean, tech-bros sunbathing on the deck while one corner quietly sinks and a lone lifeguard chair stands empty."),
            Creature("Ankylorhiza", 5, 7, speed: 1, deploy: 2,
                "Twenty-five million years extinct. Still holds a grudge.",
                "A big body for a faction whose bodies burn out: Arrival: Armor 3 on itself. An extinct toothed whale, de-extinct.",
                On(EffectTrigger.OnPlay, Armor(3, TargetType.Self)))
                .Picture("A huge prehistoric whale with a crocodile's teeth bursting out of a lab aquarium tank, water and glass everywhere, a scientist in a lab coat clutching a sign reading DE-EXTINCTION: PHASE 1."),
            Creature("White Dwarf", 2, 6, speed: 1, deploy: 0,
                "Small. Very white. Incredibly dense.",
                "A dense wall: Unbothered (enemy effects can't lower its stats; the study's best ability, 65%). Ready at once.")
                .With(CreatureAbility.Unbothered)
                .Picture("A dwarf like those of The Lord of the Rings standing in space, glowing white like the star: a long braided beard, a winged helm, a double-bladed axe, chain mail, the planets around him bending toward him, unbothered."),
            // The meta picks (the user, 2026-10-16: "implement all of your ideas", from the Pokémon and Slay the Spire report):
            // Ripening, Laid Off, Toxic (merged into Burn since) and the statuses that help their owner, Grudge, side hazards, Sucker Punch, Hit and Run,
            // Policies, Shield Bash, Strip Search, On Trend. Names from the Brainstorm List
            Creature("Biohacker", 3, 4, speed: 1, deploy: 0,
                "Microdoses, cold plunges, and a chip in his hand that opens the fridge.",
                "Hormesis (Poison Heal): Burn heals it instead of hurting it. Its Arrival gives it Burn 2 for 3 Days, so it heals 2 each Dusk. Utopia's self-harm becomes fuel.",
                On(EffectTrigger.OnPlay, Status(StatusEffectType.Burned, 3, TargetType.Self, potency: 2)))
                .With(CreatureAbility.Hormesis)
                .Picture("A shirtless tech bro in an ice bath full of glowing green sludge, sensors taped all over his chest, his smartwatch showing 'biological age: 19'."),
            Spell("Prion Disease", TargetType.EnemyCreatures,
                "Misfolded, contagious, and in no hurry.",
                "Burn 3 for 3 Days on target creature: 9 damage in all. Slow removal.",
                Status(StatusEffectType.Burned, 3, TargetType.EnemyCreatures, potency: 3))
                .Picture("A cow in a lab coat staring blankly into a microscope while its own brain, full of holes like Swiss cheese, floats in a jar beside it."),
            // Maneuvers (the user, 2026-10-17): spells that only move creatures, in three flavours by whose: Ambivalent (either
            // side's), Disruption (the enemy's), Logistics (yours). Push and Pull: a step back or forward; Swap: with its partner;
            // Shuffle: a whole side at random. Names from the Brainstorm List
            Spell("Heimlich Maneuver", TargetType.FriendlyCreatures,
                "Science saves lives. Ribs, less so.",
                "Logistics Maneuver: push target friendly creature a step back (the Vanguard to a Flank, a Flank to its Headquarters slot); a creature already there trades places with it. Gets a hurt one out of the front line.",
                Act(ActionType.Push, 0, TargetType.FriendlyCreatures, TargetModifier.None))
                .Picture("A scientist in a lab coat performing the Heimlich maneuver on a robot, a glowing chip flying out of its mouth across the lab while a colleague times it on a clipboard."),
        };
        #endregion

        #region Card definitions
        private class CardDef {
            public string Name, Flavor, Notes, ArtDescription;
            public MovementKeyword Movement;
            public CreatureAbility Ability;
            public bool IsSpell;
            public bool IsGift; // Played into an empty enemy slot, it joins their side
            public bool IsRootedWhileDeploying; // It can't move nor be moved until it's ready
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
            public bool IsHoming; // Homing: locks on the enemy creature it's aimed at (a spell) or attacks (a creature)

            public CardDef Homing() {
                IsHoming = true;
                return this;
            }
            public int SacrificeCost; // Additional cost: its side's weakest creatures die when it resolves
            public int ProphecyDays; // A spell that comes true this many Days later
            public int ProphecyLatest; // Or at random, up to this many
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

            // Prophecy N: the spell comes true at the Dawn N Days after it's played; Prophecy N-M, a random number of Days between
            public CardDef Foretold(int days, int latest = 0) {
                ProphecyDays = days;
                ProphecyLatest = latest;
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

            public bool IsRipening; // Ripening: +1 to its numbers for each Dawn it's kept in your hand
            public bool IsPolicy;   // Policy: it happens again each Dawn, for the rest of the combat
            public bool IsPotion;   // An adventure's potion: Single Use, carried from fight to fight

            public CardDef Ripening() {
                IsRipening = true;
                return this;
            }

            public CardDef Policy() {
                IsPolicy = true;
                return this;
            }

            public bool IsCombo; // Combo: +1 to its numbers for each other spell you play the same Day

            public CardDef Combo() {
                IsCombo = true;
                return this;
            }

            public string[] SetUpByNames; // The cards made to come before it (an intended combo): it glows in the hand once one is played

            // An intended combo (the user, 2026-10-17): these cards, played first from your hand today, set this one up. Only real
            // payoffs: the second card clearly better, or only working, after the first
            public CardDef SetUpBy(params string[] cards) {
                SetUpByNames = cards;
                return this;
            }

            public CardDef Potion() {
                IsPotion = true;
                IsSingleUse = true;
                Extra = true;
                return this;
            }

            // A building going up: Rooted while it deploys (the user, 2026-10-16), so nothing moves it until it's ready
            public CardDef RootedWhileDeploying() {
                IsRootedWhileDeploying = true;
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
        // A part that happens only if the spell's target died (False Flag Operation's rally)
        private static EffectAction OnlyIfItDies(EffectAction action) {
            action.onlyIfTargetDies = true;
            return action;
        }

        // A part of a spell that waits: it lands at the Dawn this many Days on, on what the spell was aimed at
        private static EffectAction Later(EffectAction action, int days) {
            action.delayDays = days;
            return action;
        }
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

        // A side hazard: the Slot Effect on every slot of a side (Border Crossing's Checkpoint)
        private static EffectAction SideHazard(SlotEffectType type, int value, int days, TargetType side) {
            var action = PlaceSlotEffect(type, value, days, side);
            action.targetModifier = Each;
            return action;
        }

        // The sweeper: clears the Slot Effects on every slot of a side (Code Review)
        private static EffectAction SideSweep(TargetType side) => Act(ActionType.CleanseSlot, 0, side, Each);

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
        private static EffectAction Buff(int value, TargetType target, bool attack, bool health, TargetModifier modifier = TargetModifier.None, int days = 0, bool speed = false) {
            var action = Act(ActionType.ModifyStat, value, target, modifier);
            action.modifyAttack = attack;
            action.modifyHealth = health;
            action.modifySpeed = speed;
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

        // Gear: a second ability while the creature stays in play (a held item)
        private static EffectAction Equip(CreatureAbility gear, TargetType target) {
            var action = Act(ActionType.Equip, 0, target, TargetModifier.None);
            action.ability = gear;
            return action;
        }

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


        #region Shelved
        // Shelved as a maybe (the user, 2026-10-17: "im not sure if i want to implement it as i may be going into scope creep
        // territory"): Gear, held items as a creature's second ability. Out of the decks and the game; the rules (Equip, Hover,
        // Self-Repair) stay in the code, so bringing it back is moving these into their factions' lists again.
        private static List<CardDef> ShelvedCards() => new List<CardDef> {
            Spell("Smartwatch", TargetType.FriendlyCreatures,
                "Tracks your steps, your sleep, your heart rate and your productivity. Reports three of them to HR.",
                "Gear (Pokémon's Life Orb): Recoil while it stays in play, +2 damage on its attacks and 1 damage to itself after each. A megacorp's subscription device, the hustle strapped to a wrist.",
                Equip(CreatureAbility.Recoil, TargetType.FriendlyCreatures))
                .Picture("A sleek smartwatch on a sweating wrist, its screen flashing 'Stand up! Productivity -3%', a subscription renewal notice sliding in underneath."),
            Spell("Exoskeleton", TargetType.FriendlyCreatures,
                "Standard issue. Batteries not included. Batteries never included.",
                "Gear (Pokémon's Assault Vest): Unbothered while it stays in play, enemy effects can't lower its stats. On top of the creature's own ability.",
                Equip(CreatureAbility.Unbothered, TargetType.FriendlyCreatures))
                .Picture("A soldier in a powered military exoskeleton standing guard at a checkpoint, an extension cord trailing from his back to a wall socket in the guard booth."),
            Spell("Q Branch", TargetType.FriendlyCreatures,
                "Now pay attention, 007.",
                "Gear: Insider Job while it stays in play, its damage ignores Armor. The quartermaster's gadgets, for the faction of glass cannons that meets walls.",
                Equip(CreatureAbility.InsiderJob, TargetType.FriendlyCreatures))
                .Picture("An old quartermaster in a lab coat demonstrating an exploding briefcase to a bored agent, a test dummy in the background missing its head."),
            Spell("Hovercraft", TargetType.FriendlyCreatures,
                "Floats over the problem. Never solves it.",
                "Gear (Pokémon's Heavy-Duty Boots): Hover while it stays in play, Slot Effects don't touch it (no Landmine, Barbed Snare or Checkpoint as it enters or leaves).",
                Equip(CreatureAbility.Hover, TargetType.FriendlyCreatures))
                .Picture("A sleek hovercraft gliding over a minefield, every mine underneath it blinking red and doing nothing, a deminer watching with his mouth open."),
            Spell("Nanobots", TargetType.FriendlyCreatures,
                "Billions of tiny repairmen. One tiny union.",
                "Gear (Pokémon's Leftovers): Self-Repair while it stays in play, it heals 2 each Dawn.",
                Equip(CreatureAbility.SelfRepair, TargetType.FriendlyCreatures))
                .Picture("A swarm of glittering nanobots stitching a robot's torn arm back together, a few of them on a tiny picket line holding 'Fair Wages' signs."),
        };
        #endregion

        #region Potions
        // An adventure's potions (the user, 2026-10-16, from Slay the Spire): Single Use spells carried from fight to fight, in the
        // opening hand, not counted among the spells. Plain things, faction-free: the Brainstorm List's Neutral Resources
        private static List<CardDef> PotionCards() => new List<CardDef> {
            Spell("Nicotine", TargetType.Player, "Calms the nerves. Shortens the rest.", "Potion: draw 2 next Day.", Draw(2))
                .Potion().Picture("A crumpled pack of cigarettes on a café table, one lit in the ashtray, a ribbon of smoke curling into a question mark."),
            Spell("Stimulant Drug", TargetType.FriendlyCreatures, "Focus, energy, and a heart rate of 190.", "Potion: target creature +2 Attack this Day.",
                Buff(2, TargetType.FriendlyCreatures, attack: true, health: false, days: 1))
                .Potion().Picture("A pill organizer with every compartment full of the same blue pill, a student's desk behind it buried in energy drink cans at 4 a.m."),
            Spell("Superglue", TargetType.FriendlyCreatures, "Holds anything together. Fingers, mostly.", "Potion: target creature +5 Armor.",
                Armor(5, TargetType.FriendlyCreatures))
                .Potion().Picture("A tube of superglue squeezed over a cracked vase, two fingers already stuck to it."),
            Spell("Soylent", TargetType.Player, "All the nutrients. None of the joy.", "Potion: heal 6 to you.", Heal(6, TargetType.Player))
                .Potion().Picture("A beige bottle of meal-replacement drink on an empty dining table, a lonely fork beside it."),
            Spell("Flamethrower", TargetType.EnemyCreatures, "Not a flamethrower. Legally.", "Potion: 4 damage to target creature or player.",
                Damage(4, TargetType.EnemyCreatures))
                .Potion().Picture("A flamethrower with a novelty-store price tag, a jet of fire roaring out of it across a garden barbecue."),
            Spell("Eject Button", TargetType.FriendlyCreatures, "Do not press. Unless.", "Potion: target creature of yours goes back to your hand, with the next Day's.",
                Act(ActionType.ReturnToHand, 0, TargetType.FriendlyCreatures, TargetModifier.None))
                .Potion().Picture("A big red button under a flip-up cover on a cockpit panel, a pilot's seat already rocketing out of the frame."),
            Spell("Stealth Suit", TargetType.Player, "Comes in black, darker black, and invisible.", "Potion: Conceal 2 on your creatures.",
                Status(StatusEffectType.Concealed, 2, TargetType.FriendlyCreatures, modifier: Each))
                .Potion().Picture("An empty mannequin in a shop window wearing a suit you can only see by its outline, the price tag floating in mid-air."),
            Spell("Creatine", TargetType.FriendlyCreatures, "Gains. Water weight, but gains.", "Potion: target creature +1 Attack and +1 Health.",
                Buff(1, TargetType.FriendlyCreatures, attack: true, health: true))
                .Potion().Picture("A giant tub of white powder with a flexing cartoon bicep on its label, a scoop sticking out of it."),
        };
        #endregion

        #region Assets
        private static DeckScriptableObject Build(Faction faction, List<CardDef> cards, Dictionary<string, CardDataScriptableObject> written) {
            WriteCards(faction, cards, written);
            var (name, list) = BestDecks[faction];
            return WriteDeck(name, list.Select(card => written[card]).ToList(), $"{DecksFolder}/{faction}Deck.asset", BestDeckStrategies[faction]);
        }

        private static DeckCatalog.Opponent Offer(DeckScriptableObject deck, string opponent, string tagline) =>
            new DeckCatalog.Opponent { name = opponent, tagline = tagline, deck = deck };

        // Writes the faction's cards to its folder, which then holds exactly these; returns its starter deck
        private static List<CardDataScriptableObject> WriteCards(Faction faction, List<CardDef> cards, Dictionary<string, CardDataScriptableObject> written) {
            string folder = $"{CardsRoot}/{faction}";
            CreateFolderIfNeeded(CardsRoot, faction.ToString());
            CreateFolderIfNeeded("Assets/Scriptables", "Decks");

            // A name defined twice (the user, 2026-10-17): the second card would overwrite the first's file in silence, as
            // Blood Transfusion, Thoughts and Prayers and Mutually Assured Destruction did for a day. Nothing is written.
            var twice = cards.GroupBy(c => c.Name).Where(g => g.Count() > 1).Select(g => $"{g.Key} ({faction}, twice)")
                .Concat(cards.Where(c => written.ContainsKey(c.Name)).Select(c => $"{c.Name} ({faction}, and {written[c.Name].faction} already)")).ToList();
            if (twice.Count > 0) throw new System.InvalidOperationException($"Cards defined twice, rename one of each: {string.Join("; ", twice)}");

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
        private static void FillCatalog(List<DeckCatalog.Opponent> offered, List<CardDataScriptableObject> potions, params DeckScriptableObject[] testDeckList) {
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
            var potionList = serializedCatalog.FindProperty("potions");
            potionList.arraySize = potions?.Count ?? 0;
            for (int i = 0; i < potionList.arraySize; i++) potionList.GetArrayElementAtIndex(i).objectReferenceValue = potions[i];
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
            asset.prophecyLatest = card.ProphecyLatest;
            asset.gamble = card.IsGamble;
            asset.homing = card.IsHoming;
            asset.ripening = card.IsRipening;
            asset.policy = card.IsPolicy;
            asset.potion = card.IsPotion;
            asset.combo = card.IsCombo;
            asset.setUpBy = card.SetUpByNames;
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
                    creature.rootedWhileDeploying = card.IsRootedWhileDeploying;
                    break;
                case SpellCardScriptableObject spell:
                    spell.defaultTargetType = card.SpellTarget;
                    break;
            }

            if (isNew) AssetDatabase.CreateAsset(asset, path);
            else EditorUtility.SetDirty(asset);
            return asset;
        }

        private static DeckScriptableObject WriteDeck(string deckName, List<CardDataScriptableObject> cards, string path, string strategy = "") {
            var deck = AssetDatabase.LoadAssetAtPath<DeckScriptableObject>(path);
            if (deck == null) {
                deck = ScriptableObject.CreateInstance<DeckScriptableObject>();
                AssetDatabase.CreateAsset(deck, path);
            }

            // Its cards changed (or it's new): it's the most recently updated, first in the menus
            bool changed = !deck.Cards.SequenceEqual(cards) || deck.LastUpdated == System.DateTime.MinValue;
            var serializedDeck = new SerializedObject(deck);
            serializedDeck.FindProperty("deckName").stringValue = deckName;
            serializedDeck.FindProperty("strategy").stringValue = strategy ?? "";
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
