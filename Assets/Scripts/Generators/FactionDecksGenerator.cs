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
            ("Capital - Hustle Rush", "CorporateRushDeck", "Your Startup's Founder", "Moving fast. Breaking you.", "Aggro", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "DMCA", "DMCA", "High-Frequency Trading Scam", "High-Frequency Trading Scam", "Venture Capital", "Venture Capital", "Fat Tony", "Fat Tony", "Teflon Don", "Teflon Don", "Hostile Takeover", "Team Building Exercise", "Flash Crash", "Resignation Letter", "Valet", "Internal Protocol: Quota Crusher Mindset", "Corporate Bailouts", "Headhunter", "Legal Department", "Casino", "Middle Manager", "Taxpayer Funds Venture Capital Profits", "Bloomberg Terminal", "Tax-Exempt Corruption", "Charles Ponzi", "Hedonic Treadmillionaire", "Day Trader", "Micromanager", "Multimillionaire Philanthropist", "Golden Parachute", "Insurance Policy", "Counterfeit", "Multilevel Marketing Seller", "Dead Cat Bounce", "NDA", "Moral Bankruptcy",
            }),
            ("Establishment - Shock and Awe", "StatecraftAggroDeck", "The Minister of Defence", "Peace through superior firepower.", "Aggro", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Internal Revenue Service", "Internal Revenue Service", "Riot Police", "Riot Police", "Due Process", "Due Process", "Man o' War", "Man o' War", "Congruent Use of Force", "Congruent Use of Force", "Panem Et Circenses", "Sapper", "Bomb Squad", "Presidential Pardon", "Raised Security Level", "Si Vis Pacem, Para Bellum", "Police Officer Productivity Target", "Mass Emigration", "Preemptive Strike", "Mercenaries", "Violence Monopoly", "Spin Doctor", "Demagogue", "Curfew", "Security Guard", "Filibuster", "Civil Servant", "Drill Sergeant", "Weapons of Mass Distraction", "Peace Force", "Mutually Assured Destruction", "Spite-Elected President", "Martial Law", "Border Crossing",
            }),
            ("Digital Society - Underdogs", "ZeitgeistSwarmDeck", "Your Neighbourhood", "Nobody believed in them. Correctly.", "Swarm", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "The Friends We Made Along the Way", "The Friends We Made Along the Way", "Underdog", "Underdog", "Mental Health Day", "Mental Health Day", "Backup Dancer", "Backup Dancer", "Ol' Reliable", "Ol' Reliable", "Spite Voter", "Weaponized Incompetence", "Plot Armor", "Universal Basic Income", "One-Trick Pony", "Anti-Vaxxer", "Groundhog Day", "Absentee Parents", "Reality TV Star", "Calm Your Tits", "Karen", "Not on My Watch", "Hipster", "Delivery Cyclist", "Scam Artist", "No Glove, No Love", "Freaky Friday", "Lost Generation", "Florida Man", "Tourist Trap", "Drug Smuggler", "Hikikomori", "Strike", "Rush Hour", "Generational Trauma", "Fidget Spinner",
            }),
            ("Enlightenment - Lab Grown", "UtopiaEngineDeck", "The Gene Therapist", "Side effects may include winning.", "Engine", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Stasis Field", "Stasis Field", "Black Hole", "Black Hole", "Genetically Modified Organism", "Genetically Modified Organism", "Ankylorhiza", "Ankylorhiza", "Exponential Growth", "Exponential Growth", "Research Lab", "Vaccine", "Biohacking", "Resurrected T-Rex", "Hypermetamorphosis", "Placebo", "Blood Transfusion", "Accelerationism", "Gene Editing", "Extremophile", "Biohacker", "White Dwarf", "Parabiosis", "Seasteading", "Chimera", "Antimatter", "Quark", "Prion Disease", "Mathematician", "Perpetual Motion Machine", "Clone", "Quantum Physicist", "Climate Change", "Scientific Method", "Heimlich Maneuver",
            }),
            ("Digital Society - Influencer Economy", "DigitalSocietyMidrangeDeck", "Your Favourite Streamer", "Like, subscribe, surrender.", "Midrange", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Its Been 84 Years...", "Its Been 84 Years...", "Clickbait", "Clickbait", "Thirst Trap", "Thirst Trap", "Dogpile", "Dogpile", "Good Boi", "Good Boi", "Keyboard Warrior", "Cancelled", "Meta Group Chat", "Content Creator", "Selfie Esteem", "Its A Trap", "Influencer", "Console Memory Card", "Short King", "Catfish", "U Mad Bro?", "Conspiracy Theorist", "Chronically Online", "Crypto \"Entrepreneur\"", "Doomscrolling", "Rage Bait", "Social Panopticon", "NPC", "Digital Nomad", "Keyboard Warrior",
            }),
            ("Establishment - Black Budget", "ShadowOpsControlDeck", "The Station Chief", "This meeting never happened.", "Control", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Counter-Surveillance", "Counter-Surveillance", "Secret Anthrax Laboratory", "Secret Anthrax Laboratory", "Arsonist", "Arsonist", "Supply Chain Attack", "Supply Chain Attack", "Involuntary Free Polonium Giveaway", "Involuntary Free Polonium Giveaway", "Sabotage", "The Golden Gun", "Perfectly Timed Camera Malfunction", "Polonium Umbrella", "False Flag Operation", "Glomar Response", "Tracking Beacon", "Femme Fatale", "Sleeper Agent", "MK Ultra Test Subject", "Political Decoy", "Burn Notice", "Exploding Pen", "Human Intelligence", "Signal Jammer", "Control Room", "Deep Cover", "Embassy", "Wet Work", "If I Told You, I'd Have to Kill You", "Plausible Deniability", "Smoke And Mirrors", "Operation Paperclip", "Extraction", "Dead Drop", "Compromised Agent",
            }),
            ("Capital - Threat Model", "TechComboDeck", "The Red Team", "We found 47 vulnerabilities. You are 46 of them.", "Combo", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "DDOS Attack", "DDOS Attack", "Ransomware Attack", "Ransomware Attack", "Code Review", "Code Review", "SysAdmin", "SysAdmin", "Scope Creep", "Scope Creep", "Antivirus", "Ctrl + Y", "Sex Clanker", "Ctrl + Z", "Buffering", "Legacy Code", "Script Kiddie", "Suspicious USB Stick in the Parking Lot", "Threat Hunter", "Man-in-the-Middle", "End-to-End Encryption", "EMP Drone", "Web Scraping", "Technical Debt", "Meatproxy As A Service", "Bomber Drone", "Pair Programming", "Antivirus", "Ctrl + Y", "Sex Clanker",
            }),
            ("Establishment - Blessed Martyrs", "MythEngineDeck", "The Abbess", "Blessed are the meek. Briefly.", "Engine", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "The Last Shall Be First", "The Last Shall Be First", "Berserker", "Berserker", "Publicly Funded Faith Schools", "Publicly Funded Faith Schools", "Heretic", "Heretic", "Omen Reading", "Omen Reading", "Gracias Jesús", "Monkey Hand", "Reincarnation", "Deus Vult", "Here Be Dragons", "Tutankhamun", "Holy Pigeon", "Buddy Christ", "The Holy Grail", "Omniscient Space-Time Bending Bearded Wizard", "Zealot", "Thoughts and Prayers", "Curse Of Hubris", "Mox Nox", "Blessing in Disguise", "Novena", "Living Dead", "Holy Hand Grenade of Antioch", "Mithril", "Holy Ghost", "Memento Mori", "Cultist", "Religious Instrumentalization", "The Chosen One", "Amen", "Phoenix Feather",
            }),
            ("Enlightenment - Mind Games", "MindControlDeck", "Your Therapist", "And how does losing make you feel?", "Control", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Freudian Slip", "The ELIZA Effect", "Epistemology", "Phronetic", "Tolerance Paradox", "Ontology", "God Is Dead", "Tabula Rasa", "Tabula Rasa", "Roko's Basilisk", "Roko's Basilisk", "Ship of Theseus", "Ship of Theseus", "Hyperbole", "Hyperbole", "Red Herring", "Red Herring", "Psychiatrists", "Trolley Problem", "Introvert", "Cyberpsychosis", "Socrates", "Empath", "Liminal Space", "Sleepwalker", "Yesn't", "Projection", "Maslow's Pyramid", "Pessimist", "Lucid Dreaming", "Imposter Syndrome", "Slow Thinking", "Momentum", "Hold That Thought", "Reality Check", "Domino Effect", "Schrodinger's Cat", "Survivorship Bias", "Controlled Chaos",
            }),
            // Two factions that play well together (Faction Compendium 5, Example Two-Faction Decks)
            ("Establishment + Capital - Prosperity Gospel", "ProsperityGospelDeck", "The Televangelist", "Your donation is tax-deductible. Your soul is not.", "Combo", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Omen Reading", "Omen Reading", "DMCA", "DMCA", "The Last Shall Be First", "The Last Shall Be First", "Holy War", "Holy War", "Berserker", "Berserker", "Monkey Hand", "Necronomicon", "High-Frequency Trading Scam", "Sacrificial Ritual", "Venture Capital", "Inflation", "Resignation Letter", "Deus Vult", "Hostile Takeover", "Curse Of Hubris", "Living Dead", "Fat Tony", "Grim Reaper", "Heretic", "Holy Pigeon", "Unpaid Intern", "Multimillionaire Philanthropist", "Here Be Dragons", "Tutankhamun", "Reincarnation", "Seniority Priority", "Private Equity", "Pluriemployed Hustler", "Business Operating Expenses", "Escape Goat", "Usury",
            }),
            ("Digital Society + Establishment - Panopticon", "PanopticonDeck", "The Data Broker", "We know what you did. We sold it.", "Combo", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Counter-Surveillance", "Counter-Surveillance", "Social Engineering", "Social Engineering", "Glomar Response", "Glomar Response", "Clickbait", "Clickbait", "Secret Anthrax Laboratory", "Secret Anthrax Laboratory", "Eavesdrop", "Sabotage", "Frame Job", "Perfectly Timed Camera Malfunction", "Tracking Beacon", "Control Room", "Tapped Communications", "Keyboard Warrior", "Hollowed-Out Volcano Lair", "HAARP", "Burn Notice", "Its Been 84 Years...", "Arsonist", "Whistleblower", "Wet Work", "Meta Group Chat", "Drug Pigeon", "Lone Gunman", "Thirst Trap", "Deep Cover", "Cleaner", "Humanitarian Trafficking Action", "Never Let Them Know Your Next Move", "Witness Protection Program", "Licence to Kill", "Nonchalant Agent",
            }),
            ("Capital + Digital Society - Hustle Culture", "HustleCultureDeck", "The Motivational Speaker", "Rise and grind. Mostly grind.", "Aggro", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Antihero", "Antihero", "Football Hooligans", "Football Hooligans", "Reality TV Star", "Reality TV Star", "Bodybuilder", "Bodybuilder", "Hipster", "Hipster", "Internal Protocol: Quota Crusher Mindset", "DMCA", "Ol' Reliable", "Business Expense", "Team Building Exercise", "The Friends We Made Along the Way", "Spite Voter", "Fat Tony", "Absentee Parents", "One-Trick Pony", "Valet", "Backup Dancer", "Teflon Don", "Calm Your Tits", "Groundhog Day", "Corporate Bailouts", "Delivery Cyclist", "Universal Basic Income", "Hostile Takeover", "Boomer", "3rd Parallel Job", "Shell Company", "Unicorn", "Cleaning Robot", "Laissez-Faire", "Big Four",
            }),
            ("Establishment + Capital - Military-Industrial Complex", "MilitaryIndustrialDeck", "The Defence Contractor", "Peace is bad for business.", "Midrange", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Man o' War", "Man o' War", "Internal Revenue Service", "Internal Revenue Service", "Diplomat", "Diplomat", "King Emeritus", "King Emeritus", "Presidential Pardon", "Presidential Pardon", "Riot Police", "Raised Security Level", "Si Vis Pacem, Para Bellum", "Panem Et Circenses", "Civil Servant", "Legal Department", "Congruent Use of Force", "Corporate Bailouts", "Due Process", "Ceasefire", "Supply Depot", "Mercenaries", "Sapper", "Mass Emigration", "Supreme Court", "Spin Doctor", "Peace Force", "Preemptive Strike", "Bomb Squad", "DMCA", "Overtime", "Work Shift Rotation", "Margin Call", "Solidarity Towards The Small Businessman", "The Invisible Hand",
            }),
            ("Enlightenment + Establishment - Frankenstein", "FrankensteinDeck", "The Mad Scientist", "It's alive. Again. And again.", "Engine", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Genetically Modified Organism", "Genetically Modified Organism", "Stasis Field", "Stasis Field", "Black Hole", "Black Hole", "Exponential Growth", "Exponential Growth", "Gracias Jesús", "Gracias Jesús", "The Holy Grail", "Ankylorhiza", "Phoenix", "Living Dead", "Templar Knight", "Gene Editing", "Genesis", "Publicly Funded Faith Schools", "Omen Reading", "Sacrificial Ritual", "Die by the Sword", "Exorcism", "Overpopulation", "Monkey Hand", "Reincarnation", "Omniscient Space-Time Bending Bearded Wizard", "Zealot", "Resurrected T-Rex", "Berserker", "Frankenstein", "Martyr", "Excess of Bread and Wine", "The Lost Ark", "Killer Rabbit of Caerbannog", "Horsemen Of Apocalypse", "The Omen",
            }),
            ("Capital + Digital Society - The Algorithm", "AlgorithmDeck", "The Recommendation Engine", "Because you watched: losing.", "Swarm", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "DDOS Attack", "DDOS Attack", "Scope Creep", "Scope Creep", "Ransomware Attack", "Ransomware Attack", "SysAdmin", "SysAdmin", "Sex Clanker", "Sex Clanker", "Clickbait", "Dogpile", "Ctrl + Y", "Thirst Trap", "Its Been 84 Years...", "Antivirus", "Script Kiddie", "Code Review", "Ctrl + Z", "Suspicious USB Stick in the Parking Lot", "Short King", "Selfie Esteem", "Cancelled", "Content Creator", "Keyboard Warrior", "Legacy Code", "Threat Hunter", "Its A Trap", "Meta Group Chat", "End-to-End Encryption",
            }),
            ("Establishment + Enlightenment - Psyop", "PsyopDeck", "The Handler", "You were never here. Neither were we.", "Control", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Roko's Basilisk", "Roko's Basilisk", "Counter-Surveillance", "Counter-Surveillance", "The Golden Gun", "The Golden Gun", "Femme Fatale", "Femme Fatale", "Involuntary Free Polonium Giveaway", "Involuntary Free Polonium Giveaway", "Tabula Rasa", "Secret Anthrax Laboratory", "Asset Recruitment", "Red Herring", "Polonium Umbrella", "Social Engineering", "False Flag Operation", "Trolley Problem", "Supply Chain Attack", "Introvert", "Cyberpsychosis", "Sabotage", "Ship of Theseus", "Death By \"Natural Causes\"", "Arsonist", "MK Ultra Test Subject", "Psychiatrists", "Pessimist", "Socrates", "Empath", "Counter-Intelligence", "Dead Man's Switch", "Devil's Breath", "Ghost Army", "Shaken, Not Stirred",
            }),
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
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Venture Capital", "Venture Capital", "High-Frequency Trading Scam", "High-Frequency Trading Scam", "Headhunter", "Headhunter", "DMCA", "DMCA", "Team Building Exercise", "Team Building Exercise", "Hostile Takeover", "Casino", "Taxpayer Funds Venture Capital Profits", "Internal Protocol: Quota Crusher Mindset", "Just in Time", "Corporate Bailouts", "Inflation", "Flash Crash", "Fat Tony", "Multimillionaire Philanthropist", "Tax-Exempt Corruption", "Day Trader", "Satoshi Nakamoto", "Hedonic Treadmillionaire", "Golden Parachute", "Micromanager", "Charles Ponzi", "Middle Manager", "Teflon Don", "Unpaid Intern", "Job Rotation", "Peace the Frontier", "Inhuman Resources", "Mass Layoffs", "Inheritance", "C-Suite",
            }),
            // Big bodies that buff each other on arrival, and Mutually Assured Destruction that they survive
            // Its control list: big bodies that buff each other, mines, shields and paperwork, and Mutually Assured Destruction
            [Faction.Statecraft] = ("Establishment - Order & Paperwork", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Internal Revenue Service", "Internal Revenue Service", "Si Vis Pacem, Para Bellum", "Si Vis Pacem, Para Bellum", "Panem Et Circenses", "Panem Et Circenses", "Man o' War", "Man o' War", "King Emeritus", "King Emeritus", "Presidential Pardon", "Violence Monopoly", "Raised Security Level", "Due Process", "Police Officer Productivity Target", "Riot Police", "Diplomat", "Peace Force", "Red Tape", "Preemptive Strike", "Ministry for Equality", "Congruent Use of Force", "Kakistocracy", "Lost Landmine", "Sapper", "Supreme Court", "Ceasefire", "Demagogue", "Bomb Squad", "Curfew", "Drone Operator", "Golden Dome", "Protocol Enforcers", "Doomsday Clock",
            }),
            // Chaos on both boards that its sturdy creatures shrug off, and Double Tap on a big hitter
            [Faction.Offline] = ("Digital Society - Moods of Modern Life", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Hipster", "Hipster", "Reality TV Star", "Reality TV Star", "Ol' Reliable", "Ol' Reliable", "The Friends We Made Along the Way", "The Friends We Made Along the Way", "Antihero", "Antihero", "Backup Dancer", "Boomer", "Karen", "Football Hooligans", "Spite Voter", "This Is Fine", "Plot Armor", "One-Trick Pony", "Chain Smoker", "Bodybuilder", "Underdog", "Zeitgeist", "Weaponized Incompetence", "Absentee Parents", "Anti-Vaxxer", "Universal Basic Income", "Mental Health Day", "Toxic Positivity", "Multipass", "Not on My Watch", "Clingy Ex", "One for the Road", "YOLO", "Russian Roulette", "We Are Not The Same", "Listen Kid", "Camper Van",
            }),
            // A swarm of cheap posters piling onto one target; the Online subfaction's deck (was a second Digital Society deck)
            [Faction.Online] = ("Digital Society - Terminally Online", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Clickbait", "Clickbait", "Thirst Trap", "Thirst Trap", "Its Been 84 Years...", "Its Been 84 Years...", "Keyboard Warrior", "Keyboard Warrior", "Meta Group Chat", "Meta Group Chat", "Dogpile", "Cancelled", "Selfie Esteem", "Content Creator", "Conspiracy Theorist", "Its A Trap", "Chronically Online", "Doomscrolling", "Crypto \"Entrepreneur\"", "Influencer", "Rage Bait", "Short King", "Good Boi", "Console Memory Card", "U Mad Bro?", "Catfish", "NPC", "Social Panopticon", "Digital Nomad", "Dogpile",
            }),
            // Accelerationism: creatures taken to the brink, then boom; Black Hole and Splitting The Atom clear the way
            [Faction.Utopia] = ("Enlightenment - Results May Vary", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Black Hole", "Black Hole", "Ankylorhiza", "Ankylorhiza", "Genetically Modified Organism", "Genetically Modified Organism", "Stasis Field", "Stasis Field", "Resurrected T-Rex", "Resurrected T-Rex", "Antimatter", "Chimera", "Accelerationism", "Biohacking", "Exponential Growth", "Prion Disease", "Cold Fusion", "Femtosecond Laser", "Splitting The Atom", "Absolute Zero", "Blood Transfusion", "Research Lab", "Particle Accelerator", "Biohacker", "Vaccine", "Geomagnetic Reversal", "Evolution Theory", "Hypermetamorphosis", "Pioneer", "White Dwarf", "Peer Review", "Philosophy PhD", "Surveillance Giraffe", "Mutation", "Longevity Hub",
            }),
            // Double-O Section's agents and gadgets with Black Budget's best: Ambush, licences, Last Wills that kill
            [Faction.ShadowOps] = ("Establishment - Licence to Kill", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Counter-Surveillance", "Counter-Surveillance", "Arsonist", "Arsonist", "Secret Anthrax Laboratory", "Secret Anthrax Laboratory", "The Golden Gun", "The Golden Gun", "False Flag Operation", "False Flag Operation", "Involuntary Free Polonium Giveaway", "Polonium Umbrella", "Supply Chain Attack", "If I Told You, I'd Have to Kill You", "MK Ultra Test Subject", "HAARP", "Exploding Pen", "Femme Fatale", "Human Intelligence", "Social Engineering", "Die Another Day", "Sabotage", "Fell Out of a Window", "Wet Work", "Drug Pigeon", "Lone Gunman", "Deep Cover", "Signal Jammer", "Sleeper Agent", "Political Decoy", "Hostage Situation", "Mole", "Coup d'État", "He Didn't Kill Himself", "Bilderberg Membership", "GoldenEye",
            }),
            // Its hardest-hitting creatures, stuns and Watchdogs
            [Faction.Tech] = ("Capital - Move Fast and Break Things", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "DDOS Attack", "DDOS Attack", "SysAdmin", "SysAdmin", "Scope Creep", "Scope Creep", "Sex Clanker", "Sex Clanker", "Ransomware Attack", "Ransomware Attack", "Legacy Code", "Ctrl + Y", "Antivirus", "Script Kiddie", "Code Review", "Technical Debt", "Ctrl + Z", "Suspicious USB Stick in the Parking Lot", "Web Scraping", "Meatproxy As A Service", "End-to-End Encryption", "Buffering", "Bomber Drone", "Threat Hunter", "Man-in-the-Middle", "EMP Drone", "Pair Programming", "Legacy Code", "Ctrl + Y", "Antivirus",
            }),
            // Holy War and the wrath cards, blessed knights, and sacrifices that pay
            [Faction.Myth] = ("Establishment - Old Gods, New Tricks", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Here Be Dragons", "Here Be Dragons", "Mox Nox", "Mox Nox", "Berserker", "Berserker", "Curse Of Hubris", "Curse Of Hubris", "Holy Hand Grenade of Antioch", "Holy Hand Grenade of Antioch", "Die by the Sword", "Grim Reaper", "Necronomicon", "The Last Shall Be First", "Blessing in Disguise", "Holy War", "Lance Of Longinus", "Tutankhamun", "Buddy Christ", "Exorcism", "Reincarnation", "Omen Reading", "The Great Flood", "Taqiyya", "Publicly Funded Faith Schools", "Black Knight", "Pyramids", "Heretic", "The Lobster Professor", "The Holy Grail", "Tithe", "Mummy", "The Wicker Man", "Vampire", "Pope", "God's Grace", "Ambrosia",
            }),
            // Sturdy creatures that wear the enemy down, and stat cuts that kill
            [Faction.Mind] = ("Enlightenment - All in Your Head", new[] {
                // Rebuilt 2026-10-17 from the studies' numbers (Tools/propose_decks.py)
                "Freudian Slip", "The ELIZA Effect", "Epistemology", "Phronetic", "Tolerance Paradox", "Ontology", "God Is Dead", "Roko's Basilisk", "Roko's Basilisk", "Cyberpsychosis", "Cyberpsychosis", "Ship of Theseus", "Ship of Theseus", "Tabula Rasa", "Tabula Rasa", "Red Herring", "Red Herring", "Trolley Problem", "Pessimist", "Introvert", "Imposter Syndrome", "Hyperbole", "Psychiatrists", "Socrates", "Empath", "Lucid Dreaming", "Liminal Space", "Sleepwalker", "Yesn't", "Projection", "Maslow's Pyramid", "Reflect", "Schrodinger's Cat", "Mimetic Theory", "Slow Thinking", "Momentum", "Hold That Thought", "Monkey See, Monkey Do", "Legerdemain",
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
