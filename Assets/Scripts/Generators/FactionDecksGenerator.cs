#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Builds the starter faction decks (Corporate, Statecraft, Zeitgeist) from the card lists below, the cards of factions
    /// that don't have a deck yet, and the Mechanics Test deck that mixes them to try every mechanic.
    /// They only use effects the rules already carry out; each card's designNotes say what it should become once the
    /// GDD mechanic it stands in for exists (overview in Assets/Documentation/Starter Faction Decks.txt).
    /// Cards go to Assets/Scriptables/Cards/&lt;Faction&gt;/, their art comes from Tools/generate_card_art.py.
    /// Running it again updates the cards in place and keeps their ids and art.
    /// </summary>
    public static class FactionDecksGenerator {
        private const string CardsRoot = "Assets/Scriptables/Cards";
        private const string DecksFolder = "Assets/Scriptables/Decks";

        // Spells aim at whatever they are dropped on; these make an action pick its own targets instead
        private const TargetModifier Each = TargetModifier.AllSameTypeTargets;
        private const TargetModifier AtRandom = TargetModifier.Random;

        [MenuItem("Cards/Generate Faction Decks")]
        public static void GenerateFactionDecks() {
            var written = new Dictionary<string, CardDataScriptableObject>();
            Build(Faction.Corporate, "Corporate - Shareholder Value", CorporateCards(), written);
            Build(Faction.Statecraft, "Statecraft - Order & Paperwork", StatecraftCards(), written);
            Build(Faction.Zeitgeist, "Zeitgeist - Moods of Modern Life", ZeitgeistCards(), written);

            // Factions whose decks don't exist yet: their cards wait in their folders, and play in the Mechanics Test deck
            WriteCards(Faction.DigitalSociety, DigitalSocietyCards(), written);
            WriteCards(Faction.ShadowOps, ShadowOpsCards(), written);
            WriteCards(Faction.Tech, TechCards(), written);
            WriteCards(Faction.Myth, MythCards(), written);
            WriteCards(Faction.Mind, MindCards(), written);

            var testDeck = WriteDeck("Mechanics Test", MechanicsTestCards.Select(name => written[name]).ToList(), $"{DecksFolder}/MechanicsTestDeck.asset");
            RegisterTestDeck(testDeck);
            AssetDatabase.SaveAssets();
            Debug.Log("Generated the faction decks and the Mechanics Test deck. Run Cards > Assign Card Art to link their art.");
        }

        // One or more cards for each mechanic the rules carry out (movement, Exhausted, Doomed, Health Loss, Armor, Homing,
        // the death sequence, Status Timing, Deployment Time), from every faction: pick it to try them all in one match
        private static readonly string[] MechanicsTestCards = {
            "Delivery Cyclist", "Digital Nomad", "Political Decoy", "Cleaning Robot Premium",
            "DDOS Attack", "Dogpile", "Memento Mori", "Martyr", "Inflation", "Generational Trauma",
            "Raised Security Level", "Diplomat", "Keyboard Warrior", "Rage Bait",
            "Hikikomori", "Karen", "Reality TV Star", "Fat Tony", "Martial Law", "Hostile Takeover",
        };

        #region Corporate
        // Pays with capital (future: discard cards). Until discarding exists, its strongest cards leak value to the enemy.
        // Can't heal. Engine: card draw, workers that die usefully, money that hurts someone else first.
        private static List<CardDef> CorporateCards() => new List<CardDef> {
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
                "Future: Investment. Pays out or loses cards depending on the Day.",
                On(EffectTrigger.OnPlay, Draw(1)),
                On(EffectTrigger.OnDeath, Draw(1, TargetType.Enemy))),
            Creature("Pluriemployed Hustler", 2, 3, speed: 2, deploy: 0,
                "Three jobs, two side hustles, one kidney left.",
                "Future: Coffeinated, then Burnout (the Hustle Culture pairing with Zeitgeist).",
                On(EffectTrigger.OnPlay, Draw(2)),
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self))),
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
            Creature("Cleaning Robot Premium", 2, 7, speed: 1, deploy: 1,
                "Cleans 30% of your floor. The other 70% is a Premium feature.",
                "Mechanics test: a sturdy pusher. Pushing (every creature can), no chain pushes, blocked pushes (\"needs Swap\") and the automatic move order.")
                .Picture("A sleek, glossy robot vacuum with a glowing megacorp logo, shoving a pile of tiny office workers and their desks into a corner. A \"Subscription expired: suction limited\" pop-up flashes on its screen."),
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
            Creature("King Emeritus", 1, 6, speed: 1, deploy: 2,
                "Abdicated in disgrace. Kept the yacht, the pension and the Swiss account.",
                "Future: Diplomatic Immunity. Can't be targeted by enemy spells.",
                On(EffectTrigger.StartOfTurn, Heal(1, TargetType.Player))),
            Creature("Spite-Elected President", 3, 4, speed: 2, deploy: 2,
                "Voted in to make everyone else miserable. Delivered on day one.",
                "Future: a Law (Location) for 3 Days that hurts both sides each Dawn.",
                On(EffectTrigger.OnPlay, Damage(1, TargetType.AllCreatures))),
            Creature("Police Officer Productivity Target", 2, 3, speed: 2, deploy: 1,
                "Ten arrests a week. Guilt is optional.",
                "Future: Priority attacks against creatures that moved this Day.",
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
                "Future: Fortifying. Adjacent friendly creatures can't be moved by enemy effects.",
                On(EffectTrigger.OnPlay, Armor(2, TargetType.FriendlyCreatures))),
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
                "Future: +1 Deployment Time on an enemy creature, or Priority -1 on its action.",
                Stun(1, TargetType.EnemyCreatures)),
            Spell("Preemptive Strike", TargetType.EnemyCreatures,
                "They were going to attack us. Probably. Eventually. Somebody said so.",
                "Future: resolves with +1 Priority (Action).",
                Damage(3, TargetType.EnemyCreatures)),
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
        };
        #endregion

        #region Zeitgeist
        // Pays with self-harm: damage to its own creatures and to you. Fast (Deployment Time 0-1), buffs that burn out.
        // Can't use Intelligence. Engine: cheap bodies that spike and fizzle, chaos that hits everyone.
        private static List<CardDef> ZeitgeistCards() => new List<CardDef> {
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
                "Future: Hipster status. Hurts only creatures that share a name with another creature (too mainstream).",
                On(EffectTrigger.OnPlay, Damage(1, TargetType.AllCreatures))),
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
            Spell("YOLO", TargetType.FriendlyCreatures,
                "Look Mom, no hands... Look Mom, no teef!",
                "Future: +1 Priority on its attack this Day, then the creature is Banished at Dusk.",
                Buff(4, TargetType.FriendlyCreatures, attack: true, health: false), Damage(2, TargetType.Player, Each)),
            Spell("This Is Fine", TargetType.FriendlyCreatures,
                "Everything is on fire. The coffee is still warm, though.",
                "Future: a Trend (Location). Each Dusk, every creature takes 1 damage and yours shrug it off.",
                Buff(2, TargetType.FriendlyCreatures, attack: false, health: true, Each), Damage(2, TargetType.AllCreatures, Each)),
            Spell("Strike", TargetType.EnemyCreatures,
                "Nobody works. Not them. Especially not us.",
                "Future: a Strike. Stops Each Dawn triggers (engines) on both sides for 2 Days.",
                Stun(1, TargetType.EnemyCreatures, Each), Stun(1, TargetType.FriendlyCreatures, Each)),
            Spell("Russian Roulette", TargetType.AllCreatures,
                "Five empty chambers. Your odds are great. Probably.",
                "Future: you pick the first slot; each Dawn it moves to the next one until it fires.",
                Damage(4, TargetType.AllCreatures, AtRandom)),
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
        };
        #endregion

        #region Factions without a deck yet
        // Digital Society: a swarm of cheap bodies, effects that spread, many creatures piling on one target
        private static List<CardDef> DigitalSocietyCards() => new List<CardDef> {
            Creature("Digital Nomad", 2, 2, speed: 2, deploy: 0,
                "Works from Bali. Works from Lisbon. Works, allegedly.",
                "Mechanics test: Leapfrog on a cheap, fast creature.")
                .With(MovementKeyword.Leapfrog)
                .Picture("A laptop balanced on a surfboard on a white beach, a coconut beside it, the Wi-Fi icon showing a single bar."),
            Creature("Keyboard Warrior", 1, 3, speed: 2, deploy: 0,
                "Has never lost an argument. Has never left the house.",
                "Mechanics test: OnDamaged fires on every hit, even one Armor absorbs or a killing blow, and still lands when it dies. Exhausted turns it off.",
                On(EffectTrigger.OnDamage, Damage(1, TargetType.Enemy)))
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
        };

        // Shadow Ops.: fragile agents, hidden plans, traps
        private static List<CardDef> ShadowOpsCards() => new List<CardDef> {
            Creature("Political Decoy", 0, 6, speed: 1, deploy: 1,
                "The real one is in a bunker. Or is he?",
                "Mechanics test: Swap trades places with a friendly creature at any distance, over the creatures in between, even one that can't act.")
                .With(MovementKeyword.Swap)
                .Picture("Two identical politicians in matching suits: one waves from a balcony, the other ducks behind the curtain."),
        };

        // Tech: rewires the queue
        private static List<CardDef> TechCards() => new List<CardDef> {
            Spell("DDOS Attack", TargetType.EnemyCreatures,
                "Ten thousand requests a second. All of them from fridges.",
                "Mechanics test: Exhausted cancels the target's planned actions (spells land before attacks), lasts the rest of the Day and all of the next, turns its abilities off and adds +1 to every hit (Exposed). Aimed at an enemy slot it's Fixed: the enemy can dodge by moving.",
                Stun(1, TargetType.EnemyCreatures))
                .Picture("A server rack melting under an avalanche of tiny envelope icons, smart fridges and toasters in the background, each with a red glowing eye."),
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
        };

        // Mind: wins without raw damage
        private static List<CardDef> MindCards() => new List<CardDef> {
            Spell("Generational Trauma", TargetType.EnemyCreatures,
                "Your grandfather's, your father's, and now yours. A family heirloom.",
                "Mechanics test: Health Loss is permanent and stays on the card until it's reshuffled. It only affects creatures, so it fizzles on an empty slot. Not damage, which fits Mind.",
                Buff(-2, TargetType.EnemyCreatures, attack: false, health: true))
                .Picture("The swimming-pool meme: the Silent Generation stands in the pool holding a smiling Boomer high above the water, while on the bottom Gen X and the generations after them sit as skeletons, still waiting."),
        };
        #endregion

        #region Card definitions
        private class CardDef {
            public string Name, Flavor, Notes, ArtDescription;
            public MovementKeyword Movement;
            public bool IsSpell;
            public int Attack, Health, Speed, DeploymentTime;
            public TargetType SpellTarget;
            public List<CardEffect> Effects;
            public int CopyCount = 1;

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
        private static EffectAction Heal(int value, TargetType target, TargetModifier modifier = TargetModifier.None) => Act(ActionType.Heal, value, target, modifier);
        private static EffectAction Armor(int value, TargetType target, TargetModifier modifier = TargetModifier.None) => Act(ActionType.Armor, value, target, modifier);
        private static EffectAction Stun(int turns, TargetType target, TargetModifier modifier = TargetModifier.None) => Act(ActionType.Stun, turns, target, modifier);
        private static EffectAction Draw(int count, TargetType player = TargetType.Player) => Act(ActionType.Draw, count, player, TargetModifier.None);
        private static EffectAction Summon(int count) => Act(ActionType.Summon, count, TargetType.Player, TargetModifier.None);

        private static EffectAction Status(StatusEffectType status, int days, TargetType target, int potency = 0, TargetModifier modifier = TargetModifier.None) {
            var action = Act(ActionType.ApplyStatus, 0, target, modifier);
            action.statusEffectToApply = status;
            action.statusDuration = days;
            action.statusPotency = potency;
            return action;
        }

        // Lasts while the creature stays on the battlefield; negative values are debuffs
        private static EffectAction Buff(int value, TargetType target, bool attack, bool health, TargetModifier modifier = TargetModifier.None) {
            var action = Act(ActionType.ModifyStat, value, target, modifier);
            action.modifyAttack = attack;
            action.modifyHealth = health;
            action.modifySpeed = false;
            return action;
        }
        #endregion

        #region Assets
        private static void Build(Faction faction, string deckName, List<CardDef> cards, Dictionary<string, CardDataScriptableObject> written) {
            var deckCards = WriteCards(faction, cards, written);
            WriteDeck(deckName, deckCards, $"{DecksFolder}/{faction}Deck.asset");
        }

        // Writes the faction's cards to its folder, which then holds exactly these
        private static List<CardDataScriptableObject> WriteCards(Faction faction, List<CardDef> cards, Dictionary<string, CardDataScriptableObject> written) {
            string folder = $"{CardsRoot}/{faction}";
            CreateFolderIfNeeded(CardsRoot, faction.ToString());
            CreateFolderIfNeeded("Assets/Scriptables", "Decks");

            var deckCards = new List<CardDataScriptableObject>();
            foreach (var card in cards) {
                var asset = WriteCard(card, faction, $"{folder}/{card.AssetName}.asset");
                written[card.Name] = asset;
                deckCards.AddRange(Enumerable.Repeat(asset, card.CopyCount));
            }
            RemoveCardsNotIn(folder, deckCards);
            return deckCards;
        }

        // The menu offers test decks next to the faction decks (see DeckCatalog.TestDecks)
        private static void RegisterTestDeck(DeckScriptableObject deck) {
            var guids = AssetDatabase.FindAssets("t:DeckCatalog");
            if (guids.Length != 1) {
                Debug.LogError($"Expected one deck catalog, found {guids.Length}; the Mechanics Test deck isn't offered in the menu.");
                return;
            }
            var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var serializedCatalog = new SerializedObject(catalog);
            var testDecks = serializedCatalog.FindProperty("testDecks");
            testDecks.arraySize = 1;
            testDecks.GetArrayElementAtIndex(0).objectReferenceValue = deck;
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
