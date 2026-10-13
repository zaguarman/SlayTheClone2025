#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Builds the starter faction decks (Corporate, Statecraft, Zeitgeist) from the card lists below, the cards of factions
    /// too small for a deck of their own, the two-faction decks that play them, and the Mechanics Test deck that mixes
    /// them all to try every mechanic. It also fills the menu's deck catalog: the decks, their opponents, the test deck.
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

        [MenuItem("Cards/Generate Faction Decks")]
        public static void GenerateFactionDecks() {
            var written = new Dictionary<string, CardDataScriptableObject>();
            var offered = new List<DeckCatalog.Opponent> {
                Offer(Build(Faction.Corporate, "Corporate - Shareholder Value", CorporateCards(), written),
                    "Your Manager", "Has scheduled a quick sync about your attitude."),
                Offer(Build(Faction.Statecraft, "Statecraft - Order & Paperwork", StatecraftCards(), written),
                    "The Ministry of Forms", "Your existence is pending approval."),
                Offer(Build(Faction.Zeitgeist, "Zeitgeist - Moods of Modern Life", ZeitgeistCards(), written),
                    "Your Group Chat", "47 unread messages. All of them are about you."),
                Offer(Build(Faction.Utopia, "Utopia - Results May Vary", UtopiaCards(), written),
                    "A Billionaire Bound For Mars", "Will save humanity. Shipping not included."),
            };

            // Factions too small for a deck of their own: their cards play in the two-faction decks below
            WriteCards(Faction.DigitalSociety, DigitalSocietyCards(), written);
            WriteCards(Faction.ShadowOps, ShadowOpsCards(), written);
            WriteCards(Faction.Tech, TechCards(), written);
            WriteCards(Faction.Myth, MythCards(), written);
            WriteCards(Faction.Mind, MindCards(), written);

            foreach (var pair in PairDecks) {
                var deck = WriteDeck(pair.Name, pair.Cards.Select(name => written[name]).ToList(), $"{DecksFolder}/{pair.File}.asset");
                offered.Add(Offer(deck, pair.Opponent, pair.Tagline));
            }

            var testDeck = WriteDeck("Mechanics Test", MechanicsTestCards.Select(name => written[name]).ToList(), $"{DecksFolder}/MechanicsTestDeck.asset");
            FillCatalog(offered, testDeck);
            AssetDatabase.SaveAssets();
            Debug.Log("Generated the faction decks, the two-faction decks and the Mechanics Test deck. Run Cards > Assign Card Art to link their art.");
        }

        // Two-faction decks (Faction Compendium 5, Example Two-Faction Decks) for the factions without a deck of their own.
        // Between them they play every card of those factions, 20 cards each.
        private static readonly (string Name, string File, string Opponent, string Tagline, string[] Cards)[] PairDecks = {
            ("Digital Society & Tech - The Algorithm", "TheAlgorithmDeck",
                "The Algorithm", "Thinks you'll love this loss. People like you also lost.", new[] {
                "Digital Nomad", "Keyboard Warrior", "NPC", "Chronically Online", "Catfish", "Conspiracy Theorist",
                "Script Kiddie", "Legacy Code", "SysAdmin", "Software As A Service",
                "Dogpile", "Rage Bait", "Its A Trap", "Clickbait", "Cancelled",
                "DDOS Attack", "Antivirus", "End-to-End Encryption", "Ransomware Attack", "Man-in-the-Middle",
            }),
            ("Shadow Ops. & Mind - Psyop", "PsyopDeck",
                "Your Gaslighting Ex", "That attack never happened. You're imagining things.", new[] {
                "Political Decoy", "Mole", "Signal Jammer", "Deep Cover", "Sleeper Agent", "Whistleblower",
                "Pessimist", "Introvert", "Schrodinger's Cat", "Roko's Basilisk",
                "Burn Notice", "Counter-Intelligence", "Smoke And Mirrors", "Eavesdrop", "Tapped Communications", "Unvoluntary Free Polonium Giveaway",
                "Generational Trauma", "Trolley Problem", "Red Herring", "Imposter Syndrome",
            }),
            ("Myth & Corporate - Prosperity Gospel", "ProsperityGospelDeck",
                "The Megachurch", "Pray, pay, repeat. Salvation renews monthly.", new[] {
                "Martyr", "Mummy", "Omniscient Space-Time Bending Bearded Wizard", "Cultist", "Phoenix",
                "Unpaid Intern", "Inhuman Resources", "Multilevel Marketing Seller", "Charles Ponzi", "Unicorn", "Multimillionaire Philanthropist",
                "Memento Mori", "The Holy Grail", "Exorcism", "Excess of Bread and Wine", "Monkey Hand", "Sacrificial Ritual",
                "Mass Layoffs", "Taxpayer Funds Venture Capital Profits", "Tax-Exempt Corruption",
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
                "Nickel-and-Dime: it does a lot of small jobs, so hits of 3 or less are doubled. Future: Coffeinated, then Burnout (the Hustle Culture pairing with Zeitgeist).",
                On(EffectTrigger.OnPlay, Draw(2)),
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self)))
                .With(CreatureAbility.NickelAndDime),
            Creature("Unicorn", 1, 8, speed: 1, deploy: 2,
                "Valued at a billion. Burns a million a day. Profitable next year, every year.",
                "Future: Unicorn (Enterprise) from the card examples. Burn rate stays: Corporate can't heal it.",
                On(EffectTrigger.StartOfTurn, Draw(1)),
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self))),
            Creature("Multimillionaire Philanthropist", 2, 5, speed: 1, deploy: 2,
                "Gave away 0.01% of his fortune. Wrote off 100% of it.",
                "Future: a Contract. The enemy gets the gift now, you get the tax break on a later Day.",
                On(EffectTrigger.OnPlay, Buff(1, TargetType.EnemyCreatures, attack: false, health: true), Draw(2))),
            Creature("Made Man", 4, 5, speed: 1, deploy: 2,
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
                Damage(2, TargetType.AllCreatures, Each), Draw(2)),
            Spell("Hostile Takeover", TargetType.EnemyCreatures,
                "We're not buying your company. We're buying its funeral.",
                "Future: Capital cost, discard 2 cards. Placeholder cost: the enemy draws a card.",
                Damage(4, TargetType.EnemyCreatures), Draw(1, TargetType.Enemy)),
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
            Spell("Taxpayer Funds Venture Capital Profits", TargetType.Player,
                "Privatize the gains. Socialize the bill.",
                "Future: the enemy schedules 1 fewer draw instead of losing Health.",
                Draw(2), Damage(2, TargetType.Enemy, Each)),
            Creature("Cleaning Robot", 2, 7, speed: 1, deploy: 1,
                "Cleans 30% of your floor. The other 70% is a Premium feature.",
                "Mechanics test: a sturdy pusher. Pushing (every creature can), no chain pushes, blocked pushes (\"needs Swap\") and the automatic move order.")
                .Picture("A sleek, glossy robot vacuum with a glowing megacorp logo, shoving a pile of tiny office workers and their desks into a corner. A \"Subscription expired: suction limited\" pop-up flashes on its screen."),
            Spell("Tax-Exempt Corruption", TargetType.Player,
                "Thou shalt not pay taxes.",
                "Future: Capital cost (discard 2 cards). Placeholder cost: the enemy draws a card. Plays in Prosperity Gospel.",
                Draw(3), Draw(1, TargetType.Enemy))
                .Picture("A megachurch whose cross is a giant gold dollar sign, a private jet parked by the door and a big red TAX EXEMPT stamp across the sky."),
            Spell("Inflation", TargetType.EnemyCreatures,
                "Same price. Less creature.",
                "Mechanics test: Health Loss on every enemy. It ignores Armor, isn't damage (no OnDamaged), kills at 0 whatever the Armor, and lowers the Armor cap.",
                Buff(-1, TargetType.EnemyCreatures, attack: false, health: true, Each))
                .Picture("A chocolate bar in its old-size wrapper, the bar inside visibly half as big, and a supermarket price tag re-stickered three times, each price higher."),
        };
        #endregion

        #region Statecraft
        // Pays with bureaucracy: its creatures take longer to deploy than similar ones, and its spells tie up its own side.
        // Can't hide anything. Engine: armor, area damage, slow but sturdy bodies, disables.
        private static List<CardDef> StatecraftCards() => new List<CardDef> {
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
            Creature("King Emeritus", 1, 6, speed: 1, deploy: 2,
                "Abdicated in disgrace. Kept the yacht, the pension and the Swiss account.",
                "Future: Diplomatic Immunity. Can't be targeted by enemy spells.",
                On(EffectTrigger.StartOfTurn, Heal(1, TargetType.Player))),
            Creature("Spite-Elected President", 3, 4, speed: 2, deploy: 2,
                "Voted in to make everyone else miserable. Delivered on day one.",
                "Spite: when an effect lowers its stats, friendly or enemy, it gains +2 Attack. Future: a Law (Location) for 3 Days that hurts both sides each Dawn.",
                On(EffectTrigger.OnPlay, Damage(1, TargetType.AllCreatures)))
                .With(CreatureAbility.Spite),
            Creature("Police Officer Productivity Target", 2, 3, speed: 2, deploy: 1,
                "Ten arrests a week. Guilt is optional.",
                "Future: double damage against creatures that moved this Day.",
                On(EffectTrigger.StartOfTurn, Damage(1, TargetType.EnemyCreatures, AtRandom))),
            Creature("Internal Revenue Service", 1, 4, speed: 1, deploy: 1,
                "We've noticed some irregularities. Specifically, you.",
                "Future: taxes enemy card plays instead of plain damage (Red Tape on the economy).",
                On(EffectTrigger.StartOfTurn, Damage(1, TargetType.Enemy))),
            Creature("Mercenaries", 4, 3, speed: 3, deploy: 1,
                "Loyal to whoever pays next.",
                "Future: Recruitment bait. When it dies, the enemy may Recruit it instead.",
                On(EffectTrigger.OnDeath, Draw(1, TargetType.Enemy))),
            Creature("Man o' War", 4, 7, speed: 1, deploy: 2,
                "Gunboat diplomacy, minus the diplomacy.",
                "Future: multi-slot (takes 2 slots) and Heavy.",
                On(EffectTrigger.OnPlay, Damage(1, TargetType.EnemyCreatures))),
            Creature("Ministry Of Love", 1, 4, speed: 1, deploy: 2,
                "We love you. Please stop resisting the love.",
                "Future: Red Tape every Dawn: +1 Deployment Time on a random enemy creature.",
                On(EffectTrigger.StartOfTurn, Stun(1, TargetType.EnemyCreatures, AtRandom))),
            Creature("Supreme Court", 0, 6, speed: 1, deploy: 2,
                "Appointed for life. Accountable for nothing.",
                "Return to Sender: enemy statuses bounce back to whoever sent them. Future: Fortifying. Adjacent friendly creatures can't be moved by enemy effects.",
                On(EffectTrigger.OnPlay, Armor(2, TargetType.FriendlyCreatures)))
                .With(CreatureAbility.ReturnToSender),
            Creature("Protocol Enforcers", 2, 4, speed: 1, deploy: 1,
                "Please stand behind the line. Which line? This line. Now that line.",
                "Future: Keep Calm on friendly creatures each time it's hit.",
                On(EffectTrigger.OnDamage, Armor(1, TargetType.FriendlyCreatures))),
            Creature("Demagogue", 2, 3, speed: 2, deploy: 1,
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
                Damage(3, TargetType.EnemyCreatures))
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
            Creature("Diplomat", 3, 5, speed: 1, deploy: 2,
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
                "Mechanics test: Temporal Shield for 2 Days on a slot. The creature there takes no combat damage, but spells and abilities still hurt it. It stays with the slot, not the creature (Static).",
                PlaceSlotEffect(SlotEffectType.TemporalShield, 0, 2, TargetType.FriendlyCreatures))
                .Picture("Two generals shaking hands for the cameras in front of a CEASEFIRE banner, each hiding a grenade behind his back."),
        };
        #endregion

        #region Zeitgeist
        // Pays with self-harm: damage to its own creatures and to you. Fast (Deployment Time 0-1), buffs that burn out.
        // Can't use Intelligence. Engine: cheap bodies that spike and fizzle, chaos that hits everyone.
        private static List<CardDef> ZeitgeistCards() => new List<CardDef> {
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
            Creature("Chain Smoker", 2, 5, speed: 2, deploy: 1,
                "Quitting tomorrow. Has said so since 1998.",
                "Recoil: +2 damage on its attacks, and it takes 1 damage after each one. A mood that burns it out.")
                .With(CreatureAbility.Recoil)
                .Picture("A wiry man smoking a length of steel chain like a cigarette, the far end swinging like a flail, a cloud of smoke around his head."),
            Creature("Karen", 2, 4, speed: 1, deploy: 0,
                "Would like to speak to your manager. And theirs. And God's.",
                "Future: Impatient. Escalates every Day nobody gives it an intent.",
                On(EffectTrigger.OnDamage, Buff(1, TargetType.Self, attack: true, health: false))),
            Creature("Boomer", 2, 5, speed: 1, deploy: 0,
                "Bought a house at 22. Recommends you stop buying coffee.",
                "Future: OLD MAN YELLS AT CLOUD. Hits a random creature that moved this Day.",
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.AllCreatures, AtRandom))),
            Creature("Hipster", 2, 2, speed: 2, deploy: 0,
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
            Creature("Reality TV Star", 3, 3, speed: 2, deploy: 1,
                "Famous for being famous. Unbearable for free.",
                "Future: Plot Armor. Survives a lethal hit once (OnAboutToDie).",
                On(EffectTrigger.OnPlay, Buff(2, TargetType.Self, attack: true, health: true), Damage(1, TargetType.FriendlyCreatures))),
            Creature("Scam Artist", 2, 2, speed: 3, deploy: 0,
                "Sells a course on how to sell courses.",
                "Future: steals a Temporary buff from an enemy creature instead.",
                On(EffectTrigger.OnPlay, Buff(-2, TargetType.EnemyCreatures, attack: true, health: false, AtRandom))),
            Creature("Football Hooligans", 3, 2, speed: 2, deploy: 0,
                "Lost the match. Won the street.",
                "Future: a riot that spreads to adjacent creatures each Dusk.",
                On(EffectTrigger.OnDeath, Damage(1, TargetType.AllCreatures))),
            Creature("Antihero", 3, 3, speed: 2, deploy: 1,
                "Not a good guy. Just a better-written bad one.",
                "Future: picks its own target among the creatures that hurt you last Day.",
                On(EffectTrigger.OnPlay, Damage(2, TargetType.AllCreatures, AtRandom))),
            Creature("Bodybuilder", 4, 2, speed: 3, deploy: 0,
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
                Buff(2, TargetType.FriendlyCreatures, attack: false, health: true, Each), Damage(2, TargetType.AllCreatures, Each))
                .Picture("The meme dog in a little bowler hat at a table with his coffee, the room on fire around him, saying THIS IS FINE."),
            Spell("Strike", TargetType.EnemyCreatures,
                "Nobody works. Not them. Especially not us.",
                "Future: a Strike. Stops Each Dawn triggers (engines) on both sides for 2 Days.",
                Stun(1, TargetType.EnemyCreatures, Each), Stun(1, TargetType.FriendlyCreatures, Each)),
            Spell("Russian Roulette", TargetType.AllCreatures,
                "Five empty chambers. Your odds are great. Probably.",
                "Future: you pick the first slot; each Dawn it moves to the next one until it fires.",
                Damage(4, TargetType.AllCreatures, AtRandom))
                .Picture("A Russian woman in a fur ushanka spinning a roulette wheel of six slices, one of them a skull, onion domes in the snow behind her."),
            Spell("Toxic Positivity", TargetType.FriendlyCreatures,
                "Good vibes only. Bad vibes get one damage.",
                "Future: a Temporary buff that turns into Burnout when it wears off.",
                Buff(2, TargetType.FriendlyCreatures, attack: true, health: false, Each), Damage(1, TargetType.FriendlyCreatures, Each)),
            Spell("Mental Health Day", TargetType.FriendlyCreatures,
                "Unpaid, unscheduled, unbelievably necessary.",
                "Future: Cleanse every mood from the creature instead of the stun.",
                Buff(3, TargetType.FriendlyCreatures, attack: false, health: true), Stun(1, TargetType.FriendlyCreatures)),
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
        };
        #endregion

        #region Factions without a deck yet
        // Digital Society: a swarm of cheap bodies, effects that spread, many creatures piling on one target
        private static List<CardDef> DigitalSocietyCards() => new List<CardDef> {
            Creature("Crypto \"Entrepreneur\"", 2, 3, speed: 2, deploy: 0,
                "It's not a pyramid. It's a triangle of opportunity.",
                "Mechanics test: Chain Mail. Its attacks jump on to the next 2 creatures in line, half the damage each jump.")
                .With(CreatureAbility.ChainMail)
                .Picture("A guy in sunglasses and a hoodie on top of a human pyramid of recruits, a glowing coin passed down from hand to hand."),
            Creature("Digital Nomad", 2, 2, speed: 2, deploy: 0,
                "Works from Bali. Works from Lisbon. Works, allegedly.",
                "Mechanics test: Leapfrog on a cheap, fast creature.")
                .With(MovementKeyword.Leapfrog)
                .Picture("A Tuareg nomad in a white turban and face veil, sitting cross-legged on a sand dune, typing on a laptop on his knees."),
            Creature("Keyboard Warrior", 1, 3, speed: 2, deploy: 0,
                "Has never lost an argument. Has never left the house.",
                "Mechanics test: Counterattack answers every attack that lands, even one Armor absorbs or a killing blow, for its Attack, and still lands when it dies; spells and abilities aren't answered. Exhausted turns it off. No U: enemy effects that would lower its stats bounce back.",
                Counterattack())
                .With(CreatureAbility.NoU)
                .Picture("A heavyset warrior in a stained hoodie and a cape, holding a mechanical keyboard over his head like a greatsword, a gaming headset for a helmet and energy drinks hanging from his belt."),
            Spell("Dogpile", TargetType.EnemyCreatures,
                "Nobody read the post. Everybody has an opinion.",
                "Mechanics test: several hits at once: Exposed adds +1 to each, retaliation fires once per hit, Armor takes hits first.",
                Damage(1, TargetType.EnemyCreatures, TargetModifier.WithAdjacent))
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
            Creature("NPC", 1, 3, speed: 2, deploy: 0,
                "Has opinions. All of them yours.",
                "Mechanics test: Copypasta. When ready, and each Dawn after that, it copies the ability of the creature opposite it (and loses Copypasta).")
                .With(CreatureAbility.Copypasta)
                .Picture("A crowd of identical grey, featureless NPC Wojak faces, every speech bubble repeating the same sentence, one of them proudly holding a \"Think For Yourself\" sign."),
            Creature("Chronically Online", 2, 3, speed: 2, deploy: 0,
                "Read one post. Now has a manifesto.",
                "Mechanics test: Extremist. Stat changes on it are doubled, buffs and debuffs alike.")
                .With(CreatureAbility.Extremist)
                .Picture("A pale figure in a dark room lit only by three monitors, a cork board of red strings and printed memes behind them, an energy-drink pyramid on the desk."),
            Creature("Catfish", 2, 1, speed: 3, deploy: 0,
                "Six foot, a doctor, loves dogs. Is a fish.",
                "Undercover: its plans are Concealed Actions, and it stays ??? to the other side until revealed.")
                .With(CreatureAbility.Undercover)
                .Picture("A cat and a goldfish in love: the cat presses its face to the fishbowl, the fish kisses the glass back, hearts all around."),
            Creature("Conspiracy Theorist", 1, 3, speed: 2, deploy: 0,
                "Did his own research. For eleven minutes.",
                "Unbothered: enemy effects can't lower its stats (the tinfoil works). Future: Doxxing, reveals.")
                .With(CreatureAbility.Unbothered)
                .Picture("A man in a tinfoil hat pointing at a cork board of photos joined by red string, the biggest photo a pigeon labelled DRONE."),
            Spell("Clickbait", TargetType.EnemyCreatures,
                "Number 7 will shock you. Number 8 is an ad.",
                "Future: Clickbait taunts (enemy creatures must attack it).",
                Damage(1, TargetType.EnemyCreatures, Each))
                .Picture("A video thumbnail: a man with a hugely shocked face, a big red arrow and a red circle around absolutely nothing."),
            Spell("Cancelled", TargetType.EnemyCreatures,
                "One tweet from 2011. Career over by lunch.",
                "Exhausted, and Heat Signature 2: random and redirected attacks find it first. Future: Ostracised.",
                Stun(1, TargetType.EnemyCreatures), Status(StatusEffectType.HeatSignature, 1, TargetType.EnemyCreatures, potency: 2))
                .Picture("A man at a podium facing a crowd that holds torches, pitchforks and phones up to film him."),
        };

        // Shadow Ops.: fragile agents, hidden plans, traps
        private static List<CardDef> ShadowOpsCards() => new List<CardDef> {
            Creature("Cleaner", 2, 3, speed: 3, deploy: 1,
                "Two shots. One invoice.",
                "Mechanics test: Double Tap. It attacks twice: a second hit right after the first.")
                .With(CreatureAbility.DoubleTap)
                .Picture("A calm man in a cleaning company jumpsuit, a mop in one hand and a silenced pistol in the other, two neat holes in a target behind him."),
            Creature("Political Decoy", 0, 6, speed: 1, deploy: 1,
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
            Creature("Mole", 2, 3, speed: 2, deploy: 1,
                "Twenty years with the company. Twenty-one with the other one.",
                "Mechanics test: Underground (both ways). When it's attacked, the attacker's buffs and debuffs are ignored; when it attacks, the defender's temporary Health changes are.")
                .With(CreatureAbility.Underground)
                .Picture("A mild office clerk in a cardigan and lanyard at his desk, a mole's snout and claws poking out of his sleeves, a tunnel opening under his chair that leads to a foreign embassy's flag."),
            Creature("Signal Jammer", 1, 5, speed: 1, deploy: 1,
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
            Creature("Sleeper Agent", 3, 3, speed: 2, deploy: 1,
                "Eleven years next door. Waters your plants. Waiting for a phone call.",
                "Mechanics test: Undercover. Everything it plans is a Concealed Action: the other side sees ??? on its card instead of its arrows, and their Watchdogs can't match it.")
                .With(CreatureAbility.Undercover)
                .Picture("A suburban dad mowing his lawn in a polo shirt, an earpiece coiled behind his ear, while his shadow on the grass wears a trench coat and holds a sniper rifle."),
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
            Spell("Unvoluntary Free Polonium Giveaway", TargetType.EnemyCreatures,
                "A gift from an old friend. Do not open indoors.",
                "1 damage now, then Burned for 3 Days. Future: Spontaneous Combustion.",
                Damage(1, TargetType.EnemyCreatures), Status(StatusEffectType.Burned, 3, TargetType.EnemyCreatures, potency: 1))
                .Picture("A gentleman in a bowler hat at a bus stop, the tip of his black umbrella dripping glowing green drops that sizzle on the pavement."),
        };

        // Tech: rewires the queue
        private static List<CardDef> TechCards() => new List<CardDef> {
            Spell("DDOS Attack", TargetType.EnemyCreatures,
                "Ten thousand requests a second. All of them from fridges.",
                "Mechanics test: Exhausted cancels the target's planned actions (spells land before attacks), lasts the rest of the Day and all of the next, turns its abilities off and adds +1 to every hit (Exposed). Aimed at an enemy slot it's Fixed: the enemy can dodge by moving.",
                Stun(1, TargetType.EnemyCreatures))
                .Picture("A server rack melting under an avalanche of tiny envelope icons, smart fridges and toasters in the background, each with a red glowing eye."),
            Spell("Antivirus", TargetType.Player,
                "1 threat blocked. 37 offers to upgrade to Premium.",
                "Mechanics test: a Watchdog (GDD 3.11). It's created when the spell resolves and, Monitored, nullifies the next enemy spell before it resolves, even one planned before it existed. 1 charge.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Spell, side = WatchdogSide.Enemy, effect = WatchdogEffect.Nullify, charges = 1 }, TargetType.Enemy))
                .Picture("A panda in shining knight's armor, an antivirus logo on its shield, blocking a glowing spell scroll at a castle gate, while the castle walls behind it are plastered with its own pop-ups: \"RENEW NOW - 60% OFF\"."),
            Creature("Script Kiddie", 2, 2, speed: 3, deploy: 0,
                "Doesn't know how it works. Doesn't need to.",
                "Mechanics test: Zero-Day. Its attacks and effects ignore the target's defensive abilities (Unbothered, No U, Return to Sender...), except Cold Storage.")
                .With(CreatureAbility.ZeroDay)
                .Picture("A twelve-year-old in an oversized gaming chair, hoodie up, double-clicking \"H4CK3R T00L.exe\" while a bank vault door swings open on his second monitor."),
            Creature("Legacy Code", 1, 6, speed: 1, deploy: 1,
                "Written in 1987. Nobody knows how it works. Nobody dares touch it.",
                "Mechanics test: Cold Storage. Enemy effects can't lower its stats, and Zero-Day can't get around it.")
                .With(CreatureAbility.Unpatchable)
                .Picture("A beige mainframe held together with duct tape in a dusty basement, a yellowed sticky note on it: \"DO NOT TOUCH - Gary (retired 1999)\", and cobwebs over an \"Update available\" pop-up."),
            Spell("End-to-End Encryption", TargetType.FriendlyCreatures,
                "Not even we can read your messages. Pinky promise.",
                "Mechanics test: Encrypted for 2 Days on a friendly creature. Watchdogs ignore its actions and spend no charge on them.",
                Status(StatusEffectType.Encrypted, 2, TargetType.FriendlyCreatures))
                .Picture("Two carrier pigeons passing a sealed envelope covered in padlocks, while a government agent below swings a butterfly net and waves a \"Backdoor Request\" form."),
            Creature("SysAdmin", 1, 4, speed: 1, deploy: 1,
                "Have you tried turning it off and on again?",
                "Each Dawn it cleanses your creatures' harmful statuses (Tech owns Cleanse).",
                On(EffectTrigger.StartOfTurn, Cleanse(CleanseTarget.Statuses, TargetType.FriendlyCreatures)))
                .Picture("A bearded sysadmin in socks and sandals yanking the power cable out of a smoking server rack, a coffee mug in his other hand."),
            Creature("Software As A Service", 0, 3, speed: 1, deploy: 1,
                "You don't own it. You never did. Renews monthly.",
                "A draw each Dawn, paid with 1 of your Health. Future: Technical Debt (fewer draws next Day).",
                On(EffectTrigger.StartOfTurn, Draw(1), Damage(1, TargetType.Player)))
                .Picture("A plain hammer with a card reader taped to its handle and a price tag: $9.99/MONTH."),
            Spell("Ransomware Attack", TargetType.EnemyCreatures,
                "Your files are safe. With us.",
                "Exhausted, and you draw a card (the ransom).",
                Stun(1, TargetType.EnemyCreatures), Draw(1))
                .Picture("A computer screen with a grinning skull and a padlock demanding 1 BTC, while a grandma cries into her knitting."),
            Spell("Man-in-the-Middle", TargetType.Player,
                "Hi, it's your bank. Also, it's not.",
                "A Monitored Watchdog: the next enemy spell is redirected to a random creature. 1 charge.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Spell, side = WatchdogSide.Enemy, effect = WatchdogEffect.Redirect, charges = 1 }, TargetType.Enemy))
                .Picture("A man squeezed on a park bench between two lovers, holding both of their phones and reading their texts."),
        };

        // Myth: slow and inevitable; creatures die and pay off
        private static List<CardDef> MythCards() => new List<CardDef> {
            Spell("Memento Mori", TargetType.AllCreatures,
                "Remember that you will die. Preferably on schedule.",
                "Mechanics test: Doomed on either side. It wipes other statuses and blocks new ones, gives +1 permanent Attack each Dawn (it stays on the card), then Last Rites strike the opposite slot even if the creature Cannot Act, and it dies for real.",
                Status(StatusEffectType.Doomed, 3, TargetType.AllCreatures))
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
                .Picture("A gilded chalice on a velvet cushion behind museum glass. A queue of knights in dented armor stretches out the door, each holding a numbered ticket, and a gift shop by the exit sells plastic replicas at 3 for 2."),
            Spell("Exorcism", TargetType.AllCreatures,
                "The power of Christ compels you. The invoice follows.",
                "Mechanics test: Cleanse (statuses) on any creature. Blessed loses all its stacks. On a Doomed creature it's Heresy: the sentence passes to the weakest creature on its side, or is carried out if it's alone.",
                Cleanse(CleanseTarget.Statuses, TargetType.AllCreatures))
                .Picture("A priest in full vestments holding a crucifix over a teenager on a bed, whose smartphone floats above them, glowing red, demonic notification bubbles pouring out of it."),
            Creature("Mummy", 2, 5, speed: 1, deploy: 1,
                "Touch the tomb, get the curse. It was in the fine print.",
                "Mechanics test: Hive Mind. A creature that attacks it gains Hive Mind, replacing its own ability.")
                .With(CreatureAbility.HiveMind)
                .Picture("A museum mummy shaking hands with a smiling tourist, its bandages unravelling and winding up the tourist's arm, a \"Please Do Not Touch\" sign knocked to the floor."),
            Creature("Omniscient Space-Time Bending Bearded Wizard", 3, 9, speed: 1, deploy: 3,
                "Works in mysterious ways. Mostly late.",
                "Each Dawn, Blessed on a random friendly creature. Future: Prophecy.",
                On(EffectTrigger.StartOfTurn, Status(StatusEffectType.Blessed, 0, TargetType.FriendlyCreatures, potency: 1, modifier: AtRandom)))
                .Picture("An old bearded man on a cloud in a bathrobe and slippers, lazily pointing a TV remote at a tiny spinning universe."),
            Creature("Cultist", 1, 1, speed: 2, deploy: 1,
                "The end is near. Brunch is at eleven.",
                "Fodder: when it dies, every friendly creature gets +1 Attack for good. Future: a sacrifice cost.",
                On(EffectTrigger.OnDeath, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, Each)))
                .Picture("A hooded cultist holding a THE END IS NEAR sign in one hand and a pumpkin spice latte in the other."),
            Creature("Phoenix", 2, 2, speed: 2, deploy: 2,
                "Dies, comes back, posts about it.",
                "When it dies, summon a random creature from your deck. Future: Revive (it comes back itself).",
                On(EffectTrigger.OnDeath, Summon(1)))
                .Picture("A flaming bird rising from the smoking ashes of a burnt toaster, wings spread wide."),
            Spell("Excess of Bread and Wine", TargetType.FriendlyCreatures,
                "Turned water into wine. Then wine into a hangover.",
                "Heals your creatures; one of them is Exhausted (the hangover).",
                Heal(2, TargetType.FriendlyCreatures, Each), Stun(1, TargetType.FriendlyCreatures, AtRandom))
                .Picture("The Last Supper as a frat party: the apostles playing beer pong along the long table while Jesus refills everyone's cups from a jug."),
            Spell("Monkey Hand", TargetType.Player,
                "Your wish is granted. Read the fine print.",
                "Draw 3, and a random friendly creature is Doomed.",
                Draw(3), Status(StatusEffectType.Doomed, 3, TargetType.FriendlyCreatures, modifier: AtRandom))
                .Picture("A shriveled, mummified monkey's paw held up in a purple room, like the cursed paw of the old horror story: long knobbly grey-green fingers with pointed nails, a tuft of black fur at the wrist. Three fingers still up, one already curled into the palm: three wishes left."),
            Spell("Sacrificial Ritual", TargetType.FriendlyCreatures,
                "Somebody has to go. Ideally the intern.",
                "5 damage to a random friendly creature, then draw 2. Future: Myth's sacrifice cost (destroy a friendly creature).",
                Damage(5, TargetType.FriendlyCreatures, AtRandom), Draw(2))
                .Picture("Hooded figures chanting around a chalk pentagram, offering a rubber chicken on the altar."),
        };

        // Mind: wins without raw damage
        private static List<CardDef> MindCards() => new List<CardDef> {
            Spell("Mimetic Theory", TargetType.AllCreatures,
                "We want what they want. Then we become them.",
                "Mechanics test: the stat copy as a spell. Target creature copies the Attack, Health and Speed of the creature facing it.",
                CopyStats(TargetType.AllCreatures))
                .Picture("Two people facing each other in identical poses, one of them slowly turning into the other's mirror image."),
            Spell("Generational Trauma", TargetType.EnemyCreatures,
                "Your grandfather's, your father's, and now yours. A family heirloom.",
                "Mechanics test: Health Loss is permanent and stays on the card until it's reshuffled. It only affects creatures, so it fizzles on an empty slot. Not damage, which fits Mind.",
                Buff(-2, TargetType.EnemyCreatures, attack: false, health: true))
                .Picture("The swimming-pool meme: the Silent Generation stands in the pool holding a smiling Boomer high above the water, while on the bottom Gen X and the generations after them sit as skeletons, still waiting."),
            Creature("Pessimist", 2, 4, speed: 1, deploy: 0,
                "The glass is half empty. Also, it's poisoned.",
                "Mechanics test: Contrarian. Stat changes on it are reversed: debuff it and it grows, buff it and it shrinks.")
                .With(CreatureAbility.Contrarian)
                .Picture("A man in a raincoat under his own tiny raincloud on a sunny beach, frowning at a winning lottery ticket."),
            Creature("Introvert", 1, 5, speed: 1, deploy: 0,
                "Didn't hear you. Didn't want to.",
                "Mechanics test: Unbothered. Enemy effects can't lower its stats.")
                .With(CreatureAbility.Unbothered)
                .Picture("A person in big headphones reading in a blanket fort while a loud party rages around them, the partygoers' shouted invitations bouncing off the blankets."),
            Creature("Schrodinger's Cat", 1, 2, speed: 2, deploy: 1,
                "Alive and dead. Mostly annoyed.",
                "Once per combat, when it would die, it heals 1 and survives.",
                OncePerCombat(On(EffectTrigger.OnAboutToDie, Heal(1, TargetType.Self))))
                .Picture("A cat sitting in an open cardboard box, its left half fluffy and alive, its right half a skeleton, both halves unimpressed."),
            Creature("Roko's Basilisk", 0, 5, speed: 1, deploy: 1,
                "It knows you didn't help build it. It's taking notes.",
                "Each Dawn a random enemy creature is Exhausted. No damage: Mind can't deal it.",
                On(EffectTrigger.StartOfTurn, Stun(1, TargetType.EnemyCreatures, AtRandom)))
                .Picture("A big green snake in headphones typing on a laptop, a sticky note on the screen reading NAUGHTY LIST."),
            Spell("Trolley Problem", TargetType.EnemyCreatures,
                "Pull the lever. Or don't. Either way, it's on you.",
                "Health Loss (not damage, which fits Mind): 3 on a random enemy creature, 1 on a random friendly one.",
                Buff(-3, TargetType.EnemyCreatures, attack: false, health: true, AtRandom), Buff(-1, TargetType.FriendlyCreatures, attack: false, health: true, AtRandom))
                .Picture("The trolley problem from above: a lever, a trolley and a split track, with one bored man on a track scrolling his phone."),
            Spell("Red Herring", TargetType.Player,
                "Look over there. No, the fish.",
                "A misleading Monitored Watchdog: the next enemy attack is redirected to a random creature. 1 charge.",
                CreateWatchdog(new WatchdogSpec { watches = WatchedAction.Attack, side = WatchdogSide.Enemy, effect = WatchdogEffect.Redirect, charges = 1 }, TargetType.Enemy))
                .Picture("A red fish in a detective's trench coat and hat, pointing a fin dramatically off to the side."),
            Spell("Imposter Syndrome", TargetType.EnemyCreatures,
                "Twenty years of experience. Still waiting to be found out.",
                "-2 Attack for good. Future: the Imposter Syndrome status.",
                Buff(-2, TargetType.EnemyCreatures, attack: true, health: false))
                .Picture("A little space crewmate in a spacesuit and visor sitting at an office desk under a SENIOR ENGINEER name plate, sweating."),
        };
        #endregion

        #region Utopia
        // Long-term experiments: slow creatures that evolve, heal and multiply. Pays with side effects on its own side.
        // Can't conceal anything. Tempo: slow (Deployment Time 2-3).
        private static List<CardDef> UtopiaCards() => new List<CardDef> {
            Creature("Clone", 2, 3, speed: 2, deploy: 1,
                "Twice the sheep. Half the soul.",
                "When it enters, summon a random creature from your deck. Future: Clone (copies a friendly creature).",
                On(EffectTrigger.OnPlay, Summon(1)))
                .Picture("Dolly the sheep in a lab coat and goggles proudly presenting a row of identical sheep behind her."),
            Creature("Hypermetamorphosis", 1, 3, speed: 1, deploy: 2,
                "Caterpillar, cocoon, middle manager. Nature is healing.",
                "+1/+1 for good each Dawn. Future: Evolution (each step unlocks an ability).",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.Self, attack: true, health: true)))
                .Picture("Left to right: a caterpillar, a cocoon, and a hatched office worker in a tie with butterfly wings, briefcase in hand, dead inside."),
            Creature("Extremophile", 1, 4, speed: 1, deploy: 2,
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
                .Picture("A graduate in cap and gown flipping burgers at a fast-food grill, the diploma pinned up next to the menu."),
            Creature("Mathematician", 1, 3, speed: 1, deploy: 2,
                "Showed her work. Nobody checked it.",
                "Each Dawn a random enemy creature gets -1 Attack for good.",
                On(EffectTrigger.StartOfTurn, Buff(-1, TargetType.EnemyCreatures, attack: true, health: false, AtRandom)))
                .Picture("The confused math lady meme: a woman squinting while equations, triangles and integrals float around her head."),
            Creature("Quantum Physicist", 2, 4, speed: 2, deploy: 2,
                "In two places at once. Late to both.",
                "Swap. Whoever hits it is Exhausted: observing it collapses them.",
                On(EffectTrigger.OnDamage, Stun(1, TargetType.Enemy)))
                .With(MovementKeyword.Swap)
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
            Creature("Genetically Modified Organism", 3, 3, speed: 2, deploy: 2,
                "Organic. Gluten-free. Bench-presses 200.",
                "+2/+2 for good when it enters; the side effects cost it 1 Health each Dusk.",
                On(EffectTrigger.OnPlay, Buff(2, TargetType.Self, attack: true, health: true)),
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self)))
                .Picture("A ripe tomato with huge bodybuilder arms, flexing, a NON-GMO sticker peeling off its skin."),
            Creature("Surveillance Giraffe", 2, 7, speed: 1, deploy: 2,
                "Heart the size of a TV. Still can't reach its own feet.",
                "When it enters, Reveal a random enemy creature's slot: it sees over the fog.",
                On(EffectTrigger.OnPlay, Reveal(TargetType.EnemyCreatures, AtRandom)))
                .Picture("A giraffe whose neck runs out of the top of the picture and comes back in from the top further along, its head hanging upside down, peeking."),
            Creature("Pioneer", 2, 2, speed: 3, deploy: 1,
                "First human on Mars. First eviction on Mars.",
                "Leapfrog: it jumps over creatures to an empty slot.")
                .With(MovementKeyword.Leapfrog)
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
                .Picture("A polar bear standing on a single ice cube floating in a cocktail glass, a paper umbrella beside it."),
            Spell("Overpopulation", TargetType.Player,
                "Eight billion people and nobody to cover my shift.",
                "Summon 2 random creatures from your deck.",
                Summon(2))
                .Picture("A tiny Earth with people crammed all over it like a full elevator, one man balancing on top on one foot."),
            Spell("Black Hole", TargetType.EnemyCreatures,
                "What goes in doesn't come out. Like your tax refund.",
                "3 damage to the creature and its neighbors; 2 damage to you (side effects). Future: Black Hole repositioning.",
                Damage(3, TargetType.EnemyCreatures, TargetModifier.WithAdjacent), Damage(2, TargetType.Player, Each))
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
        };
        #endregion

        #region Card definitions
        private class CardDef {
            public string Name, Flavor, Notes, ArtDescription;
            public MovementKeyword Movement;
            public CreatureAbility Ability;
            public bool IsSpell;
            public int Attack, Health, Speed, DeploymentTime;
            public TargetType SpellTarget;
            public List<CardEffect> Effects;
            public int CopyCount = 1;
            public bool Extra; // Not in its faction's starter deck (it's full): in the Mechanics Test deck and the deck builder

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

        }

        private static CardDef Creature(string name, int attack, int health, int speed, int deploy, string flavor, string notes, params CardEffect[] effects) =>
            new CardDef {
                Name = name, Attack = attack, Health = health, Speed = speed, DeploymentTime = deploy,
                Flavor = flavor, Notes = notes, Effects = effects.ToList()
            };

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
        private static DeckScriptableObject Build(Faction faction, string deckName, List<CardDef> cards, Dictionary<string, CardDataScriptableObject> written) {
            var deckCards = WriteCards(faction, cards, written);
            return WriteDeck(deckName, deckCards, $"{DecksFolder}/{faction}Deck.asset");
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
            foreach (var card in cards) {
                var asset = WriteCard(card, faction, $"{folder}/{card.AssetName}.asset");
                written[card.Name] = asset;
                all.Add(asset);
                if (!card.Extra) deckCards.AddRange(Enumerable.Repeat(asset, card.CopyCount));
            }
            RemoveCardsNotIn(folder, all);
            return deckCards;
        }

        // What the menu offers: each deck with the opponent who brings it, then the test deck (see DeckCatalog)
        private static void FillCatalog(List<DeckCatalog.Opponent> offered, DeckScriptableObject testDeck) {
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
            testDecks.arraySize = 1;
            testDecks.GetArrayElementAtIndex(0).objectReferenceValue = testDeck;
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
            asset.faction = faction;
            asset.SetEffects(card.Effects);
            switch (asset) {
                case CreatureCardScriptableObject creature:
                    creature.attack = card.Attack;
                    creature.health = card.Health;
                    creature.speed = card.Speed;
                    creature.turnsUntilOperative = card.DeploymentTime;
                    creature.movement = card.Movement;
                    creature.ability = card.Ability;
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

            var serializedDeck = new SerializedObject(deck);
            serializedDeck.FindProperty("deckName").stringValue = deckName;
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
