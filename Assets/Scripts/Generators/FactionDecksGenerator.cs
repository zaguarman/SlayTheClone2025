#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Builds the starter faction decks (Corporate, Statecraft, Zeitgeist) from the card lists below.
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
            Build(Faction.Corporate, "Corporate - Shareholder Value", CorporateCards());
            Build(Faction.Statecraft, "Statecraft - Order & Paperwork", StatecraftCards());
            Build(Faction.Zeitgeist, "Zeitgeist - Moods of Modern Life", ZeitgeistCards());
            AssetDatabase.SaveAssets();
            Debug.Log("Generated the Corporate, Statecraft and Zeitgeist decks. Run Cards > Assign Card Art to link their art.");
        }

        #region Corporate
        // Pays with capital (future: discard cards). Until discarding exists, its strongest cards leak value to the enemy.
        // Can't heal. Engine: card draw, cheap workers that die usefully, stat growth.
        private static List<CardDef> CorporateCards() => new List<CardDef> {
            Creature("Unpaid Intern", 1, 1, speed: 3, deploy: 0,
                "Paid in experience. The experience is this.",
                "Future: a Worker token created by other cards rather than drafted. Fodder for Layoffs-style sacrifice costs.",
                On(EffectTrigger.OnDeath, Draw(1))).Copies(3),
            Creature("Gig Courier", 3, 1, speed: 3, deploy: 0,
                "Five stars. Zero benefits.",
                "Future: Contract worker. Leaves play on its own after 3 Days, the gig is over.").Copies(2),
            Creature("Headhunter", 1, 2, speed: 2, deploy: 0,
                "Exciting opportunity! Fast-paced, unpaid, like a family.",
                "Future: summons an Unpaid Intern token instead of pulling a creature from the deck.",
                On(EffectTrigger.OnPlay, Summon(1))),
            Creature("Middle Manager", 2, 3, speed: 1, deploy: 1,
                "Synergizes the deliverables. Someone else delivers them.",
                "Future: gives a friendly Worker Coffeinated instead of a plain attack buff.",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, AtRandom))),
            Creature("Venture Capitalist", 1, 4, speed: 1, deploy: 1,
                "Invests early. Exits before the fire.",
                "Future: Investment. Pay now (discard a card), get a Persistent Stat Modification on a later Day.",
                On(EffectTrigger.StartOfTurn, Buff(1, TargetType.FriendlyCreatures, attack: true, health: true, AtRandom))),
            Creature("Unicorn Startup", 1, 8, speed: 1, deploy: 2,
                "Valued at a billion. Burns a million a day. Profitable next year, every year.",
                "Future: Unicorn (Enterprise) from the card examples. Burn rate stays: Corporate can't heal it.",
                On(EffectTrigger.StartOfTurn, Draw(1)),
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self))),
            Creature("Golden Parachute CEO", 3, 4, speed: 1, deploy: 2,
                "Fired for gross incompetence. Severance: your pension fund.",
                "Future: OnDied also gives a random friendly creature a Persistent buff (failing upward into the next role).",
                On(EffectTrigger.OnDeath, Draw(2))),
            Spell("Layoffs", TargetType.FriendlyCreatures,
                "We were never a family. Please return your badge.",
                "Future: as an additional cost, destroy any number of your Workers; draw 1 per Worker next Day.",
                Damage(2, TargetType.FriendlyCreatures, Each), Draw(3)),
            Spell("Team-Building Retreat", TargetType.FriendlyCreatures,
                "Attendance is voluntary. Attendance is tracked.",
                "Future: Coffeinated on each friendly creature, followed by Burnout.",
                Buff(1, TargetType.FriendlyCreatures, attack: true, health: false, Each)),
            Spell("Trickle-Down Economics", TargetType.FriendlyCreatures,
                "It will trickle down any quarter now.",
                "Future: targets your strongest creature on purpose. The poor stay poor.",
                Buff(3, TargetType.FriendlyCreatures, attack: true, health: true, AtRandom)),
            Spell("Performance Review", TargetType.EnemyCreatures,
                "Meets expectations. The expectations were lowered.",
                "Future: a Persistent -2 Attack, as Corporate owns stat changes that stick.",
                Buff(-2, TargetType.EnemyCreatures, attack: true, health: false)),
            Spell("Paywall", TargetType.EnemyCreatures,
                "Subscribe to keep existing.",
                "Future: Paywall. For 3 Days, the enemy's Deployment Time 0 creatures cost them a card draw.",
                Damage(1, TargetType.EnemyCreatures, Each)),
            Spell("Quarterly Earnings Call", TargetType.Player,
                "Record profits! Unrelated: the layoffs.",
                "Future: Capital cost, discard a card. Placeholder cost: the enemy hears about it too.",
                Draw(2), Draw(1, TargetType.Enemy)),
            Spell("Hostile Takeover", TargetType.EnemyCreatures,
                "We're not buying your company. We're buying its funeral.",
                "Future: Capital cost, discard 2 cards. Placeholder cost: the enemy draws a card.",
                Damage(4, TargetType.EnemyCreatures), Draw(1, TargetType.Enemy)),
        };
        #endregion

        #region Statecraft
        // Pays with bureaucracy: its creatures take a turn longer to deploy than similar ones, and its spells tie up its
        // own side. Can't hide anything. Engine: armor, area damage, slow but sturdy bodies.
        private static List<CardDef> StatecraftCards() => new List<CardDef> {
            Creature("Conscript", 2, 3, speed: 1, deploy: 1,
                "Volunteered. The paperwork says so.",
                "Future: Recruitment. Conscription takes enemy creatures, and this is what they become.",
                On(EffectTrigger.OnPlay, Armor(1, TargetType.Self))).Copies(3),
            Creature("Border Guard", 1, 5, speed: 1, deploy: 1,
                "Papers, please. No, the other papers.",
                "Future: Anchored on itself and Fortifying the slots next to it.",
                On(EffectTrigger.OnPlay, Armor(1, TargetType.FriendlyCreatures))),
            Creature("Tax Collector", 1, 3, speed: 2, deploy: 1,
                "Death and taxes. He handles the one you can appeal.",
                "Future: taxes enemy card plays instead of plain damage (Red Tape on the economy).",
                On(EffectTrigger.StartOfTurn, Damage(1, TargetType.Enemy))),
            Creature("Blitz Sergeant", 3, 2, speed: 3, deploy: 1,
                "Shoots first. Files the report never.",
                "Future: its attacks get +1 Priority (Action) instead of relying on Speed."),
            Creature("Propaganda Minister", 1, 3, speed: 2, deploy: 1,
                "Morale has never been higher. That is an order.",
                "Future: Keep Calm on friendly creatures.",
                On(EffectTrigger.OnPlay, Buff(1, TargetType.FriendlyCreatures, attack: true, health: false))),
            Creature("Public Hospital", 0, 4, speed: 1, deploy: 2,
                "Free for everyone. Current waiting list: two turns.",
                "Future: an Automated Unit. Healing is Statecraft's secondary, so keep it slow.",
                On(EffectTrigger.StartOfTurn, Heal(1, TargetType.FriendlyCreatures))),
            Creature("Eternal Senator", 1, 5, speed: 1, deploy: 2,
                "Ninth term. Still hasn't read the bill.",
                "Future: Gerontocratic Decree. Can't be removed by enemy effects while you control another Statecraft creature.",
                On(EffectTrigger.StartOfTurn, Heal(1, TargetType.Player))),
            Creature("Main Battle Tank", 4, 6, speed: 1, deploy: 2,
                "Over budget, behind schedule, on your lawn.",
                "Future: multi-slot (takes 2 slots) and Heavy.",
                On(EffectTrigger.OnPlay, Damage(1, TargetType.EnemyCreatures))),
            Spell("Red Tape", TargetType.EnemyCreatures,
                "Your request is being processed. Please hold.",
                "Future: +1 Deployment Time on an enemy creature, or Priority -1 on its action.",
                Stun(1, TargetType.EnemyCreatures)),
            Spell("Artillery Barrage", TargetType.EnemyCreatures,
                "Collateral damage within acceptable parameters.",
                "Future: Static area damage on 3 adjacent slots, the swarm counter.",
                Damage(2, TargetType.EnemyCreatures, Each)),
            Spell("Curfew", TargetType.FriendlyCreatures,
                "For your safety, nobody moves. Ever.",
                "Future: a Law (Location) for 3 Days. The stun stands in for the bureaucracy drawback.",
                Armor(3, TargetType.FriendlyCreatures, Each), Stun(1, TargetType.FriendlyCreatures, Each)),
            Spell("Conscription", TargetType.Player,
                "Congratulations! You have been selected.",
                "Future: Recruitment. Take control of an enemy creature; capture it if it survives the combat.",
                Summon(2)),
            Spell("Preemptive Strike", TargetType.EnemyCreatures,
                "They were going to attack us. Probably. Eventually.",
                "Future: resolves with +1 Priority (Action).",
                Damage(3, TargetType.EnemyCreatures)),
            Spell("Austerity Measures", TargetType.AllCreatures,
                "Everyone tightens their belts. Starting after the gala.",
                "Future: Kakistocracy-style Law: creatures entering with 3+ Attack get -1 Attack for 3 Days.",
                Buff(-1, TargetType.AllCreatures, attack: true, health: false, Each)),
            Spell("State of Emergency", TargetType.Player,
                "Rights temporarily suspended. Temporarily.",
                "Future: a Law that also adds 1 Deployment Time to your next creature (the paperwork).",
                Heal(4, TargetType.Player, Each), Armor(1, TargetType.FriendlyCreatures, Each)),
        };
        #endregion

        #region Zeitgeist
        // Pays with self-harm: damage to its own creatures and to you. Fast (Deployment Time 0-1), buffs that burn out.
        // Can't use Intelligence. Engine: cheap bodies that spike and fizzle, sweeping chaos.
        private static List<CardDef> ZeitgeistCards() => new List<CardDef> {
            Creature("Doomscroller", 1, 3, speed: 2, deploy: 0,
                "Just one more post. Just one more. Just...",
                "Future: Earworm-style mood. Gains Impatient once it runs out of Health to spend.",
                On(EffectTrigger.EndOfTurn, Buff(1, TargetType.Self, attack: true, health: false), Damage(1, TargetType.Self))).Copies(2),
            Creature("Wojak", 1, 5, speed: 1, deploy: 0,
                "It's fine. I'm fine. Everything is fine.",
                "Future: Wojak Living The Dream from the card examples.",
                On(EffectTrigger.OnDamage, Buff(1, TargetType.Self, attack: true, health: false))),
            Creature("Grindset Bro", 4, 2, speed: 3, deploy: 0,
                "Sleep is a scam invented by Big Mattress.",
                "Future: Coffeinated, then Burnout (the Hustle Culture pairing with Corporate).",
                On(EffectTrigger.EndOfTurn, Damage(1, TargetType.Self))).Copies(2),
            Creature("Hipster", 2, 2, speed: 2, deploy: 0,
                "Liked this battlefield before it got so crowded.",
                "Future: Hipster status. Hurts only creatures that share a name with another creature (too mainstream).",
                On(EffectTrigger.OnPlay, Damage(1, TargetType.AllCreatures))),
            Creature("Underdog", 1, 1, speed: 3, deploy: 0,
                "Nobody believed in him. Correctly. Still, very inspiring.",
                "Future: Underdog. A bonus while you have fewer creatures or less Health than the enemy.",
                On(EffectTrigger.OnDeath, Buff(2, TargetType.FriendlyCreatures, attack: true, health: false))).Copies(2),
            Creature("Main Character", 3, 3, speed: 2, deploy: 1,
                "Everyone else is an NPC. The NPCs are not thrilled.",
                "Future: Plot Armor. Survives a lethal hit once (OnAboutToDie).",
                On(EffectTrigger.OnPlay, Buff(2, TargetType.Self, attack: true, health: true), Damage(1, TargetType.FriendlyCreatures))),
            Creature("Quiet Quitter", 2, 6, speed: 1, deploy: 0,
                "Does exactly what the contract says. The contract says nothing.",
                "Future: Slacking Off. Can't be given an intent on the Days it doesn't feel like it."),
            Spell("YOLO", TargetType.FriendlyCreatures,
                "Look Mom, no hands... Look Mom, no teef!",
                "Future: +1 Priority on its attack this Day, then the creature is Banished at Dusk.",
                Buff(4, TargetType.FriendlyCreatures, attack: true, health: false), Damage(2, TargetType.Player, Each)),
            Spell("Rage Quit", TargetType.EnemyCreatures,
                "gg ez. Uninstalling. Reinstalling.",
                "Future: self-harm lands on a friendly creature (Impatient) instead of on you.",
                Damage(4, TargetType.EnemyCreatures), Damage(2, TargetType.Player, Each)),
            Spell("Existential Dread", TargetType.AllCreatures,
                "Nothing matters. Equally, at least.",
                "Future: a Trend (Location) that rewards creatures with no intents for X Days.",
                Damage(2, TargetType.AllCreatures, Each)),
            Spell("Touch Grass", TargetType.FriendlyCreatures,
                "Ugh. Fine. It's... actually nice?",
                "Future: Cleanse a mood from each friendly creature instead.",
                Buff(2, TargetType.FriendlyCreatures, attack: false, health: true, Each)),
            Spell("General Strike", TargetType.EnemyCreatures,
                "Nobody works. Not them. Especially not us.",
                "Future: Strike. Stops Each Dawn triggers (engines) on both sides for 2 Days.",
                Stun(1, TargetType.EnemyCreatures, Each), Stun(1, TargetType.FriendlyCreatures, Each)),
            Spell("Retail Therapy", TargetType.FriendlyCreatures,
                "Bought happiness. Return window: one turn.",
                "Future: the buff is Temporary (2 Days); the damage to you stays.",
                Buff(2, TargetType.FriendlyCreatures, attack: true, health: true), Damage(2, TargetType.Player, Each)),
        };
        #endregion

        #region Card definitions
        private class CardDef {
            public string Name, Flavor, Notes;
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
        private static void Build(Faction faction, string deckName, List<CardDef> cards) {
            string folder = $"{CardsRoot}/{faction}";
            CreateFolderIfNeeded(CardsRoot, faction.ToString());
            CreateFolderIfNeeded("Assets/Scriptables", "Decks");

            var deckCards = new List<CardDataScriptableObject>();
            foreach (var card in cards) {
                var asset = WriteCard(card, faction, $"{folder}/{card.AssetName}.asset");
                deckCards.AddRange(Enumerable.Repeat(asset, card.CopyCount));
            }
            WriteDeck(deckName, deckCards, $"{DecksFolder}/{faction}Deck.asset");
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
            asset.faction = faction;
            asset.SetEffects(card.Effects);
            switch (asset) {
                case CreatureCardScriptableObject creature:
                    creature.attack = card.Attack;
                    creature.health = card.Health;
                    creature.speed = card.Speed;
                    creature.turnsUntilOperative = card.DeploymentTime;
                    break;
                case SpellCardScriptableObject spell:
                    spell.defaultTargetType = card.SpellTarget;
                    break;
            }

            if (isNew) AssetDatabase.CreateAsset(asset, path);
            else EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void WriteDeck(string deckName, List<CardDataScriptableObject> cards, string path) {
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
        }

        private static void CreateFolderIfNeeded(string parent, string name) {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
        #endregion
    }
}
#endif
