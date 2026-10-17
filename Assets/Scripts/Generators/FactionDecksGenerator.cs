#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// Writes the card assets from the card files (cards as data, the user, 2026-10-17: Assets/Cards/&lt;Subfaction&gt;.json, see
    /// CardFiles), builds each subfaction's best deck and the second decks and pairs listed below, the Workbench and the
    /// Mechanics Test deck that mixes them all to try every mechanic, and fills the menu's deck catalog: the decks, their
    /// opponents, the test decks. Each card's notes say what it's for (overview in Assets/Documentation/Starter Faction Decks.txt).
    /// Cards go to Assets/Scriptables/Cards/&lt;Subfaction&gt;/, their art comes from Tools/generate_card_art.py.
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
                Offer(Build(Faction.Corporate, written), "Your Manager", "Has scheduled a quick sync about your attitude."),
                Offer(Build(Faction.Statecraft, written), "The Ministry of Forms", "Your existence is pending approval."),
                // Digital Society's two subfactions (the user, 2026-10-17): Offline, the culture itself (the old Zeitgeist cards; the user chose Offline
                // over Mainstream, as it pairs with Online), and Online, the internet's (the old Digital Society cards)
                Offer(Build(Faction.Offline, written), "Your Group Chat", "47 unread messages. All of them are about you."),
                Offer(Build(Faction.Online, written), "Your Feed", "Curated just for you. Every post is about how wrong you are."),
                Offer(Build(Faction.Utopia, written), "A Billionaire Bound For Mars", "Will save humanity. Shipping not included."),
                Offer(Build(Faction.ShadowOps, written), "Your Quartermaster", "Please return the car in one piece. You never do."),
                Offer(Build(Faction.Tech, written), "The IT Department", "Have you tried turning your strategy off and on again?"),
                Offer(Build(Faction.Myth, written), "The Oracle", "Saw this loss coming. Didn't warn you."),
                Offer(Build(Faction.Mind, written), "Your Inner Critic", "Knows exactly what to say. Always the worst thing."),
            };

            foreach (var pair in PairDecks) {
                var deck = WriteDeck(pair.Name, pair.Cards.Select(name => written[name]).ToList(), $"{DecksFolder}/{pair.File}.asset", pair.Strategy);
                offered.Add(Offer(deck, pair.Opponent, pair.Tagline));
            }

            WriteCards(Faction.None, CardFilesMenu.Load(Faction.None), written); // An adventure's potions (Potions.json), in no deck
            var potions = CardFilesMenu.Load(Faction.None).Select(p => written[p.name]).ToList();
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
        private static readonly string[] Focus = { "Digital Society - Terminally Online", "Capital - Shareholder Value", "Establishment - Old Gods, New Tricks", "Digital Society - Moods of Modern Life" };

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
            ("Capital - Hustle Rush", "CorporateRushDeck", "Your Startup's Founder", "Moving fast. Breaking you.", "Aggro", new[] { "Micromanager", "Micromanager", "Cleaning Robot", "Cleaning Robot", "Multilevel Marketing Seller", "Multilevel Marketing Seller", "Bloomberg Terminal", "Bloomberg Terminal", "Charles Ponzi", "Charles Ponzi", "Day Trader", "Day Trader", "Middle Manager", "Middle Manager", "Shell Company", "Shell Company", "Flash Crash", "Flash Crash", "Hostile Takeover", "Hostile Takeover", "Team Building Exercise", "Team Building Exercise", "High-Frequency Trading Scam", "High-Frequency Trading Scam", "Inheritance", "Inheritance", "Venture Capital", "Venture Capital", "Valet", "Valet", "DMCA", "DMCA", "3rd Parallel Job", "3rd Parallel Job", "Legal Department", "Legal Department", "Job Rotation", "Job Rotation", "Resignation Letter", "Resignation Letter", "Teflon Don", "Teflon Don", "Insurance Policy", "Insurance Policy", "Seniority Priority", "Seniority Priority" }),
            ("Establishment - Shock and Awe", "StatecraftAggroDeck", "The Minister of Defence", "Peace through superior firepower.", "Aggro", new[] { "Mercenaries", "Mercenaries", "Sherman Tank", "Sherman Tank", "Police Officer Productivity Target", "Police Officer Productivity Target", "Spite-Elected President", "Spite-Elected President", "Demagogue", "Demagogue", "Man o' War", "Man o' War", "Preemptive Strike", "Preemptive Strike", "Raised Security Level", "Raised Security Level", "Martial Law", "Martial Law", "Panem Et Circenses", "Weapons of Mass Distraction", "Security Guard", "Security Guard", "Mass Emigration", "Mass Emigration", "Filibuster", "Filibuster", "Curfew", "Curfew", "Spin Doctor", "Spin Doctor", "Civil Servant", "Civil Servant", "Bomb Squad", "Bomb Squad", "Riot Police", "Riot Police", "Drill Sergeant", "Drill Sergeant", "Sapper", "Sapper", "Mutually Assured Destruction", "Mutually Assured Destruction", "Due Process", "Due Process", "Congruent Use of Force" }),
            ("Digital Society - Underdogs", "ZeitgeistSwarmDeck", "Your Neighbourhood", "Nobody believed in them. Correctly.", "Swarm", new[] { "Underdog", "Underdog", "Plot Armor", "Plot Armor", "Scam Artist", "Scam Artist", "Delivery Cyclist", "Delivery Cyclist", "Drug Smuggler", "Drug Smuggler", "Hikikomori", "Hikikomori", "Not on My Watch", "Karen", "Karen", "No Glove, No Love", "No Glove, No Love", "One for the Road", "One for the Road", "Rush Hour", "Generational Trauma", "Generational Trauma", "Mental Health Day", "YOLO", "The Friends We Made Along the Way", "The Friends We Made Along the Way", "Absentee Parents", "Absentee Parents", "Ol' Reliable", "Ol' Reliable", "Weaponized Incompetence", "Listen Kid", "Listen Kid", "Lost Generation", "Lost Generation", "Spite Voter", "Spite Voter", "Camper Van", "Universal Basic Income", "Calm Your Tits", "Tourist Trap", "Groundhog Day", "Freaky Friday", "Florida Man", "One-Trick Pony", "Clingy Ex", "Backup Dancer", "Anti-Vaxxer" }),
            ("Enlightenment - Lab Grown", "UtopiaEngineDeck", "The Gene Therapist", "Side effects may include winning.", "Engine", new[] { "Quark", "Quark", "Ankylorhiza", "Ankylorhiza", "Particle Accelerator", "Particle Accelerator", "Cold Fusion", "Cold Fusion", "Hypermetamorphosis", "Hypermetamorphosis", "Resurrected T-Rex", "Resurrected T-Rex", "Antimatter", "Antimatter", "Biohacker", "Biohacker", "Biohacking", "Biohacking", "Mutation", "Mutation", "Exponential Growth", "Exponential Growth", "Seasteading", "Seasteading", "Blood Transfusion", "Blood Transfusion", "Stasis Field", "Stasis Field", "Gene Editing", "Gene Editing", "Heimlich Maneuver", "Heimlich Maneuver", "Blood Transfusion", "Blood Transfusion", "Peer Review", "Peer Review", "Extremophile", "Mathematician", "Parabiosis", "Perpetual Motion Machine", "Philosophy PhD", "Placebo", "Research Lab", "Vaccine", "White Dwarf" }),
            ("Digital Society - Influencer Economy", "DigitalSocietyMidrangeDeck", "Your Favourite Streamer", "Like, subscribe, surrender.", "Midrange", new[] { "Thirst Trap", "Thirst Trap", "Content Creator", "Content Creator", "Short King", "Short King", "Good Boi", "Good Boi", "Its Been 84 Years...", "Its Been 84 Years...", "Catfish", "Catfish", "Meta Group Chat", "Meta Group Chat", "Cancelled", "Cancelled", "Social Panopticon", "Social Panopticon", "Its A Trap", "Dogpile", "Dogpile", "Console Memory Card", "Selfie Esteem", "Selfie Esteem", "Influencer", "Influencer", "U Mad Bro?", "U Mad Bro?" }),
            ("Establishment - Black Budget", "ShadowOpsControlDeck", "The Station Chief", "This meeting never happened.", "Control", new[] { "Embassy", "Embassy", "Mole", "Mole", "Arsonist", "Arsonist", "Cleaner", "Cleaner", "Compromised Agent", "Compromised Agent", "Political Decoy", "Political Decoy", "Control Room", "Control Room", "Signal Jammer", "Signal Jammer", "Counter-Surveillance", "Counter-Surveillance", "Supply Chain Attack", "Supply Chain Attack", "Secret Anthrax Laboratory", "Secret Anthrax Laboratory", "Human Intelligence", "Human Intelligence", "Sabotage", "Sabotage", "Perfectly Timed Camera Malfunction", "Perfectly Timed Camera Malfunction", "Tracking Beacon", "Tracking Beacon", "Plausible Deniability", "Plausible Deniability", "Hostage Situation", "Hostage Situation", "Bilderberg Membership", "Burn Notice", "Dead Drop", "Extraction", "Ghost Army", "Glomar Response", "Never Let Them Know Your Next Move", "Sleeper Agent", "Witness Protection Program" }),
            ("Capital - Threat Model", "TechComboDeck", "The Red Team", "We found 47 vulnerabilities. You are 46 of them.", "Combo", new[] { "Suspicious USB Stick in the Parking Lot", "Suspicious USB Stick in the Parking Lot", "Technical Debt", "Technical Debt", "Scope Creep", "Scope Creep", "SysAdmin", "SysAdmin", "Threat Hunter", "Threat Hunter", "Script Kiddie", "Script Kiddie", "Legacy Code", "Legacy Code", "Sex Clanker", "Sex Clanker", "Ransomware Attack", "Ransomware Attack", "Code Review", "Code Review", "Ctrl + Y", "Ctrl + Y", "Man-in-the-Middle", "Man-in-the-Middle", "Antivirus", "Antivirus", "DDOS Attack", "DDOS Attack", "Pair Programming", "Pair Programming", "EMP Drone", "EMP Drone", "Buffering", "Buffering", "Ctrl + Z", "End-to-End Encryption" }),
            ("Establishment - Blessed Martyrs", "MythEngineDeck", "The Abbess", "Blessed are the meek. Briefly.", "Engine", new[] { "Pyramids", "Pyramids", "Heretic", "Heretic", "Reincarnation", "Reincarnation", "Black Knight", "Black Knight", "Cultist", "Cultist", "Here Be Dragons", "Here Be Dragons", "Buddy Christ", "Buddy Christ", "Zealot", "Zealot", "Deus Vult", "Deus Vult", "Gracias Jesús", "Gracias Jesús", "Mithril", "Mithril", "Ambrosia", "Ambrosia", "Excess of Bread and Wine", "Excess of Bread and Wine", "Monkey Hand", "Monkey Hand", "Publicly Funded Faith Schools", "Publicly Funded Faith Schools", "Thoughts and Prayers", "Thoughts and Prayers", "Holy Ghost", "Holy Ghost", "Tithe", "Tithe", "Berserker", "Berserker", "Tutankhamun", "Tutankhamun", "Holy Pigeon", "Holy Pigeon", "The Last Shall Be First", "The Last Shall Be First", "Novena", "Omniscient Space-Time Bending Bearded Wizard" }),
            ("Enlightenment - Mind Games", "MindControlDeck", "Your Therapist", "And how does losing make you feel?", "Control", new[] { "Socrates", "Socrates", "Roko's Basilisk", "Roko's Basilisk", "Schrodinger's Cat", "Schrodinger's Cat", "Introvert", "Introvert", "Ship of Theseus", "Ship of Theseus", "Legerdemain", "Legerdemain", "Maslow's Pyramid", "Maslow's Pyramid", "Yesn't", "Yesn't", "Red Herring", "Red Herring", "Survivorship Bias", "Momentum", "Trolley Problem", "Trolley Problem", "Lucid Dreaming", "Hold That Thought", "Hold That Thought", "Psychiatrists", "Psychiatrists", "Projection", "Hyperbole", "Hyperbole", "Domino Effect", "Domino Effect", "Controlled Chaos", "Controlled Chaos", "Liminal Space", "Liminal Space", "Monkey See, Monkey Do", "Monkey See, Monkey Do", "Sleepwalker", "Sleepwalker", "Empath", "Empath", "Tabula Rasa", "Tabula Rasa", "Reality Check", "Reality Check", "Slow Thinking", "Slow Thinking" }),
            // Two factions that play well together (Faction Compendium 5, Example Two-Faction Decks)
            ("Establishment + Capital - Prosperity Gospel", "ProsperityGospelDeck", "The Televangelist", "Your donation is tax-deductible. Your soul is not.", "Combo", new[] { "Sacrificial Ritual", "Sacrificial Ritual", "Omen Reading", "Necronomicon", "Necronomicon", "Cultist", "Cultist", "Unpaid Intern", "Unpaid Intern", "Golden Parachute", "Golden Parachute", "Charles Ponzi", "Day Trader", "Day Trader", "Fat Tony", "Fat Tony", "Martyr", "Martyr", "Tax-Exempt Corruption", "Monkey Hand", "Holy War", "Holy War", "Hostile Takeover", "Hostile Takeover", "Inhuman Resources", "Multimillionaire Philanthropist" }),
            ("Digital Society + Establishment - Panopticon", "PanopticonDeck", "The Data Broker", "We know what you did. We sold it.", "Combo", new[] { "Meta Group Chat", "Meta Group Chat", "Social Panopticon", "Social Panopticon", "Cancelled", "Cancelled", "Control Room", "Control Room", "Wet Work", "Wet Work", "Perfectly Timed Camera Malfunction", "Perfectly Timed Camera Malfunction", "Tapped Communications", "Keyboard Warrior", "Keyboard Warrior", "Chronically Online", "Chronically Online", "Licence to Kill", "Licence to Kill", "Cleaner", "Cleaner", "Dogpile", "Dogpile", "Eavesdrop", "Frame Job", "Humanitarian Trafficking Action", "Whistleblower" }),
            ("Capital + Digital Society - Hustle Culture", "HustleCultureDeck", "The Motivational Speaker", "Rise and grind. Mostly grind.", "Aggro", new[] { "Day Trader", "Day Trader", "Pluriemployed Hustler", "Pluriemployed Hustler", "Bodybuilder", "Bodybuilder", "Football Hooligans", "Football Hooligans", "Reality TV Star", "Reality TV Star", "Antihero", "Antihero", "Overtime", "Overtime", "YOLO", "YOLO", "Toxic Positivity", "Toxic Positivity", "Hostile Takeover", "Hostile Takeover", "Team Building Exercise", "Team Building Exercise", "Mass Layoffs", "Business Expense", "C-Suite", "Internal Protocol: Quota Crusher Mindset" }),
            ("Establishment + Capital - Military-Industrial Complex", "MilitaryIndustrialDeck", "The Defence Contractor", "Peace is bad for business.", "Midrange", new[] { "Man o' War", "Man o' War", "Diplomat", "Diplomat", "Sherman Tank", "Sherman Tank", "Fat Tony", "Fat Tony", "Cleaning Robot", "Cleaning Robot", "Mercenaries", "Mercenaries", "Preemptive Strike", "Preemptive Strike", "Inflation", "Inflation", "Margin Call", "Margin Call", "Raised Security Level", "Supply Depot", "Taxpayer Funds Venture Capital Profits", "Taxpayer Funds Venture Capital Profits", "Corporate Bailouts", "Unicorn" }),
            ("Enlightenment + Establishment - Frankenstein", "FrankensteinDeck", "The Mad Scientist", "It's alive. Again. And again.", "Engine", new[] { "Living Dead", "Living Dead", "Phoenix", "Phoenix", "Reincarnation", "Reincarnation", "Frankenstein", "Frankenstein", "Genetically Modified Organism", "Genetically Modified Organism", "Gene Editing", "Gene Editing", "Mutation", "Mutation", "Phoenix Feather", "Phoenix Feather", "Exponential Growth", "The Holy Grail", "Templar Knight", "Templar Knight", "Resurrected T-Rex", "Resurrected T-Rex", "Genesis", "Vampire", "Clone", "Overpopulation" }),
            ("Capital + Digital Society - The Algorithm", "AlgorithmDeck", "The Recommendation Engine", "Because you watched: losing.", "Swarm", new[] { "Script Kiddie", "Script Kiddie", "NPC", "NPC", "Short King", "Short King", "Digital Nomad", "Digital Nomad", "Threat Hunter", "Threat Hunter", "Crypto \"Entrepreneur\"", "Crypto \"Entrepreneur\"", "Pair Programming", "Pair Programming", "Clickbait", "Clickbait", "Dogpile", "Dogpile", "DDOS Attack", "DDOS Attack", "Social Panopticon", "Ransomware Attack" }),
            ("Establishment + Enlightenment - Psyop", "PsyopDeck", "The Handler", "You were never here. Neither were we.", "Control", new[] { "Femme Fatale", "Femme Fatale", "Deep Cover", "Deep Cover", "Roko's Basilisk", "Roko's Basilisk", "Introvert", "Introvert", "Red Herring", "Red Herring", "Counter-Intelligence", "Counter-Intelligence", "Sabotage", "Legerdemain", "Legerdemain", "Imposter Syndrome", "Imposter Syndrome", "Smoke And Mirrors", "Trolley Problem", "Trolley Problem", "The Golden Gun", "Asset Recruitment", "Coup d'État", "Devil's Breath", "Operation Paperclip", "Social Engineering" }),
        };

        // The strategy each faction's best deck plays, for the balance study's strategy table
        private static readonly Dictionary<Faction, string> BestDeckStrategies = new Dictionary<Faction, string> {
            [Faction.Corporate] = "Engine", [Faction.Statecraft] = "Control", [Faction.Offline] = "Midrange", [Faction.Online] = "Swarm", [Faction.Utopia] = "Aggro",
            [Faction.ShadowOps] = "Aggro", [Faction.Tech] = "Midrange", [Faction.Myth] = "Control",
            [Faction.Mind] = "Control",
        };

        // Each faction's deck (2026-10-14): its best 20 cards and combos, picked from the AI games' card stats (damage, kills,
        // win share per card), before any balance study. Two copies at most. The old decks (and the two-faction ones) are gone
        private static readonly Dictionary<Faction, (string Name, string[] Cards)> BestDecks = new Dictionary<Faction, (string, string[])> {
            // Board-wide damage that draws cards, removal, and bodies that grow
            [Faction.Corporate] = ("Capital - Shareholder Value", new[] {
                "Margin Call", "Margin Call", "Hostile Takeover", "Hostile Takeover", "Inflation", "Inflation", "Dead Cat Bounce", "Dead Cat Bounce",
                "Taxpayer Funds Venture Capital Profits", "Taxpayer Funds Venture Capital Profits", "Team Building Exercise", "Casino",
                "Fat Tony", "Fat Tony", "Cleaning Robot", "Cleaning Robot", "Day Trader", "Micromanager", "Charles Ponzi", "Pluriemployed Hustler",
                "Peace the Frontier",
                // Added 2026-10-16 (the balance plan): what its decks needed most
                "Hedonic Treadmillionaire", "Hedonic Treadmillionaire", "Moral Bankruptcy", "Moral Bankruptcy", "Flash Crash",
                // Added 2026-10-16 (the meta picks): the new mechanics' cards that fit this deck
                "Usury", "Usury", "Big Four", "Big Four",
                // Every card in a deck (the user, 2026-10-17): the ones that had none, one copy each, to rebalance later
                "Business Operating Expenses", "Counterfeit", "Escape Goat", "Headhunter", "Just in Time", "Laissez-Faire", "NDA", "Private Equity", "Satoshi Nakamoto", "Solidarity Towards The Small Businessman", "The Invisible Hand", "Work Shift Rotation",
            }),
            // Big bodies that buff each other on arrival, and Mutually Assured Destruction that they survive
            // Its control list: big bodies that buff each other, mines, shields and paperwork, and Mutually Assured Destruction
            [Faction.Statecraft] = ("Establishment - Order & Paperwork", new[] {
                "Doomsday Clock", "Preemptive Strike", "Lost Landmine", "Ceasefire", "Red Tape", "Raised Security Level",
                "Panem Et Circenses", "Martial Law", "Supply Depot", "Man o' War", "Diplomat", "Spite-Elected President", "Supreme Court",
                "Demagogue", "King Emeritus", "Ministry for Equality", "Mercenaries", "Police Officer Productivity Target",
                "Protocol Enforcers", "Internal Revenue Service",
                // Added 2026-10-16 (the meta picks): the new mechanics' cards that fit this deck
                "Border Crossing", "Border Crossing", "Peace Force", "Peace Force", "Si Vis Pacem, Para Bellum",
                // Every card in a deck (the user, 2026-10-17): the ones that had none, one copy each, to rebalance later
                "Drone Operator", "Golden Dome", "Kakistocracy", "Presidential Pardon", "Violence Monopoly",
            }),
            // Chaos on both boards that its sturdy creatures shrug off, and Double Tap on a big hitter
            [Faction.Offline] = ("Digital Society - Moods of Modern Life", new[] {
                "This Is Fine", "This Is Fine", "Russian Roulette", "Russian Roulette", "Football Hooligans", "Football Hooligans",
                "Hipster", "Hipster", "Boomer", "Boomer", "Karen", "Karen", "Antihero", "Antihero", "Bodybuilder", "Bodybuilder",
                "Chain Smoker", "Reality TV Star", "Toxic Positivity", "Toxic Positivity",
                // Added 2026-10-16 (the balance plan): what its decks needed most
                "Ol' Reliable", "Ol' Reliable", "Listen Kid", "Listen Kid",
                // Added 2026-10-16 (the meta picks): the new mechanics' cards that fit this deck
                "Fidget Spinner", "Fidget Spinner",
                // Every card in a deck (the user, 2026-10-17): the ones that had none, one copy each, to rebalance later
                "Multipass", "Strike", "We Are Not The Same", "Zeitgeist",
            }),
            // A swarm of cheap posters piling onto one target; the Online subfaction's deck (was a second Digital Society deck)
            [Faction.Online] = ("Digital Society - Terminally Online", new[] {
                "Clickbait", "Clickbait", "Dogpile", "Dogpile", "Keyboard Warrior", "Keyboard Warrior", "NPC", "NPC",
                "Crypto \"Entrepreneur\"", "Crypto \"Entrepreneur\"", "Digital Nomad", "Digital Nomad", "Meta Group Chat", "Meta Group Chat",
                "Conspiracy Theorist", "Conspiracy Theorist", "Rage Bait", "Cancelled", "Social Panopticon", "Chronically Online",
                // Every card in a deck (the user, 2026-10-17): the ones that had none, one copy each, to rebalance later
                "Doomscrolling",
            }),
            // Accelerationism: creatures taken to the brink, then boom; Black Hole and Splitting The Atom clear the way
            [Faction.Utopia] = ("Enlightenment - Results May Vary", new[] {
                "Accelerationism", "Accelerationism", "Black Hole", "Black Hole", "Biohacking", "Biohacking", "Splitting The Atom", "Splitting The Atom",
                "Cold Fusion", "Cold Fusion", "Antimatter", "Antimatter", "Resurrected T-Rex", "Resurrected T-Rex",
                "Blood Transfusion", "Evolution Theory", "Scientific Method", "Particle Accelerator", "Chimera", "Frankenstein",
                // Added 2026-10-16 (the balance plan): what its decks needed most
                "Femtosecond Laser", "Femtosecond Laser", "Longevity Hub", "Longevity Hub", "Ankylorhiza",
                // Added 2026-10-16 (the meta picks): the new mechanics' cards that fit this deck
                "Biohacker", "Biohacker", "Prion Disease", "Prion Disease",
                // Every card in a deck (the user, 2026-10-17): the ones that had none, one copy each, to rebalance later
                "Absolute Zero", "Climate Change", "Geomagnetic Reversal", "Pioneer", "Quantum Physicist", "Surveillance Giraffe",
            }),
            // Double-O Section's agents and gadgets with Black Budget's best: Ambush, licences, Last Wills that kill
            [Faction.ShadowOps] = ("Establishment - Licence to Kill", new[] {
                "Femme Fatale", "Femme Fatale", "If I Told You, I'd Have to Kill You", "If I Told You, I'd Have to Kill You",
                "Licence to Kill", "Licence to Kill", "Cleaner", "Cleaner", "MK Ultra Test Subject", "MK Ultra Test Subject",
                "Dead Man's Switch", "Dead Man's Switch", "Die Another Day", "Shaken, Not Stirred", "The Golden Gun", "False Flag Operation",
                "GoldenEye", "Supply Chain Attack", "Wet Work", "Human Intelligence",
                // Added 2026-10-16 (the balance plan): what its decks needed most
                "Exploding Pen", "Exploding Pen", "Arsonist", "Arsonist", "Secret Anthrax Laboratory", "Drug Pigeon", "Drug Pigeon",
                // Every card in a deck (the user, 2026-10-17): the ones that had none, one copy each, to rebalance later
                "Death By \"Natural Causes\"", "Fell Out of a Window", "HAARP", "He Didn't Kill Himself", "Hollowed-Out Volcano Lair", "Involuntary Free Polonium Giveaway", "Lone Gunman", "Nonchalant Agent", "Polonium Umbrella",
            }),
            // Its hardest-hitting creatures, stuns and Watchdogs
            [Faction.Tech] = ("Capital - Move Fast and Break Things", new[] {
                "Technical Debt", "Technical Debt", "Legacy Code", "Legacy Code", "Scope Creep", "Scope Creep", "Script Kiddie", "Script Kiddie",
                "SysAdmin", "SysAdmin", "Sex Clanker", "Sex Clanker", "DDOS Attack", "DDOS Attack", "Ransomware Attack", "Ransomware Attack",
                "Antivirus", "Man-in-the-Middle", "Web Scraping", "Meatproxy As A Service",
                // Every card in a deck (the user, 2026-10-17): the ones that had none, one copy each, to rebalance later
                "Bomber Drone",
            }),
            // Holy War and the wrath cards, blessed knights, and sacrifices that pay
            [Faction.Myth] = ("Establishment - Old Gods, New Tricks", new[] {
                "Holy War", "Holy War", "Lance Of Longinus", "Lance Of Longinus", "Horsemen Of Apocalypse", "Horsemen Of Apocalypse",
                "Templar Knight", "Templar Knight", "Black Knight", "Black Knight", "The Wicker Man", "Necronomicon", "The Great Flood",
                "Holy Hand Grenade of Antioch", "The Chosen One", "Buddy Christ", "Living Dead", "Necronomicon", "Phoenix Feather", "Phoenix Feather",
                "Pyramids", "Amen",
                // Added 2026-10-16 (the balance plan): what its decks needed most
                "Grim Reaper", "Grim Reaper", "Die by the Sword", "Die by the Sword", "Here Be Dragons",
                // Every card in a deck (the user, 2026-10-17): the ones that had none, one copy each, to rebalance later
                "Blessing in Disguise", "Curse Of Hubris", "Exorcism", "God's Grace", "Killer Rabbit of Caerbannog", "Memento Mori", "Mox Nox", "Mummy", "Pope", "Religious Instrumentalization", "Taqiyya", "The Lobster Professor", "The Lost Ark", "The Omen",
            }),
            // Sturdy creatures that wear the enemy down, and stat cuts that kill
            [Faction.Mind] = ("Enlightenment - All in Your Head", new[] {
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

        #region Assets
        // Reads the subfaction's card file (Assets/Cards/<Subfaction>.json, see CardFiles) and writes its cards; returns its best deck
        private static DeckScriptableObject Build(Faction faction, Dictionary<string, CardDataScriptableObject> written) {
            WriteCards(faction, CardFilesMenu.Load(faction), written);
            var (name, list) = BestDecks[faction];
            return WriteDeck(name, list.Select(card => written[card]).ToList(), $"{DecksFolder}/{faction}Deck.asset", BestDeckStrategies[faction]);
        }

        private static DeckCatalog.Opponent Offer(DeckScriptableObject deck, string opponent, string tagline) =>
            new DeckCatalog.Opponent { name = opponent, tagline = tagline, deck = deck };

        // Writes the file's cards to the subfaction's folder, which then holds exactly these
        private static void WriteCards(Faction faction, List<CardFiles.CardSpec> cards, Dictionary<string, CardDataScriptableObject> written) {
            string folder = $"{CardsRoot}/{faction}";
            CreateFolderIfNeeded(CardsRoot, faction.ToString());
            CreateFolderIfNeeded("Assets/Scriptables", "Decks");

            // A name defined twice (the user, 2026-10-17): the second card would overwrite the first's file in silence, as
            // Blood Transfusion, Thoughts and Prayers and Mutually Assured Destruction did for a day. Nothing is written.
            var twice = cards.GroupBy(c => c.name).Where(g => g.Count() > 1).Select(g => $"{g.Key} ({faction}, twice)")
                .Concat(cards.Where(c => written.ContainsKey(c.name)).Select(c => $"{c.name} ({faction}, and {written[c.name].faction} already)")).ToList();
            if (twice.Count > 0) throw new System.InvalidOperationException($"Cards defined twice, rename one of each: {string.Join("; ", twice)}");

            var all = new List<CardDataScriptableObject>();
            // Fake faces first, then tokens, so the cards that point at them (a disguise, a decoy) find them written
            foreach (var card in cards.OrderBy(c => !string.IsNullOrEmpty(c.faction) ? 0 : c.token ? 1 : 2)) {
                var asset = WriteCard(card, faction, $"{folder}/{AssetName(card.name)}.asset", written);
                written[card.name] = asset;
                all.Add(asset);
            }
            RemoveCardsNotIn(folder, all);
        }

        /// <summary>A card's asset and picture file name: its name's letters and digits ("Crypto \"Entrepreneur\"" is CryptoEntrepreneur).</summary>
        public static string AssetName(string name) => new string(name.Where(char.IsLetterOrDigit).ToArray());

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

        private static CardDataScriptableObject WriteCard(CardFiles.CardSpec card, Faction faction, string path, Dictionary<string, CardDataScriptableObject> written) {
            var asset = AssetDatabase.LoadAssetAtPath<CardDataScriptableObject>(path);
            bool isNew = asset == null;
            if (isNew) {
                // Creating the instance gives it a fresh cardId; an existing asset keeps its id
                asset = CardFiles.IsCreature(card)
                    ? ScriptableObject.CreateInstance<CreatureCardScriptableObject>()
                    : (CardDataScriptableObject)ScriptableObject.CreateInstance<SpellCardScriptableObject>();
            }
            var problems = new List<string>();
            CardFiles.Apply(card, asset, faction, name => written.TryGetValue(name, out var found) ? found : null, problems.Add);
            if (problems.Count > 0) throw new System.InvalidOperationException($"{path}: {string.Join("; ", problems)}"); // The game's own files are held to their words
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
