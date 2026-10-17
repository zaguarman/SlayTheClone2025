using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// Cards as data, not code (the user, 2026-10-17): the game's own cards live in JSON files, Assets/Cards/&lt;Subfaction&gt;.json
    /// (Potions.json for the adventure's potions, Shelved.json for cards kept out of the game), in the same format as a mod's
    /// (see 'Modding.txt'). Cards > Generate Faction Decks reads them and writes the card assets; <see cref="Mods"/> reads a
    /// mod's at run time. <see cref="Apply"/> puts a card's fields onto an asset, <see cref="ToSpec"/> reads them back, and
    /// <see cref="Write"/> prints a file compactly (fields at their defaults left out), while <see cref="Read"/> parses one.
    /// </summary>
    public static class CardFiles {
        #region The file format
        [Serializable]
        public class CardFile {
            public string mod;    // A mod's name (it prefixes its cards' ids and names its decks' opponent); the game's own files: the subfaction
            public string author;
            public List<CardSpec> cards = new List<CardSpec>();
            public List<DeckSpec> decks = new List<DeckSpec>();
        }

        [Serializable]
        public class CardSpec {
            public string name;
            public string type = "creature";   // creature or spell
            public string faction;             // Corporate, Statecraft, Online, Offline, Myth, ShadowOps, Tech, Mind, Utopia; None for neutral. The game's files: the file's, unless a fake face says otherwise
            public string flavor;              // The line under the picture
            public string notes;               // Design notes (not printed)
            public string picture;             // What the picture shows: the brief its art is drawn from
            public string art;                 // A mod's PNG beside the file, 352x200 reads best
            public int attack, health;         // A creature's
            public int speed = 1;
            public int deploy;                 // Deployment Time
            public string ability = "None";    // A creature's ability, by its code name (Litigious, HunkerDown, ...)
            public string movement = "None";   // Leapfrog or Swap
            public bool gift;                  // Played into an empty enemy slot, it joins their side
            public bool rooted;                // Rooted while it deploys
            public bool token;                 // Made by an effect, never in a deck
            public string disguise;            // A token's: the card the other side sees it as
            public string target = "EnemyCreatures"; // A spell's aim: Player, Enemy, AllCreatures, FriendlyCreatures, EnemyCreatures
            public bool singleUse;
            public int discard, sacrifice, prophecy, prophecyLatest, doom;
            public bool discardChosen;         // The discard picked by hand rather than at random
            public bool gamble;                // Only one of its effects happens, at random
            public bool homing, ripening, policy, combo, potion;
            public List<string> setUpBy = new List<string>(); // The cards that set it up (an intended combo)
            public List<EffectSpec> effects = new List<EffectSpec>();
        }

        [Serializable]
        public class EffectSpec {
            public string trigger = "OnPlay";  // OnPlay (a creature's Arrival, a spell's cast), OnDeath, OnDamage, StartOfTurn, EndOfTurn, OnAboutToDie, OnRevealed, OnReplaced, OnDiscarded
            public bool once;                  // Once per combat
            public List<ActionSpec> actions = new List<ActionSpec>();
        }

        [Serializable]
        public class ActionSpec {
            public string action = "Damage";   // Damage, Heal, Draw, Summon, Armor, Stun, ModifyStat, ApplyStatus, Cleanse, PlaceSlotEffect, SetTrend, Destroy, ... (Enums.ActionType)
            public int value;
            public string target = "EnemyCreatures";
            public string modifier = "None";   // None, WithAdjacent, AllSameTypeTargets, Random, Strongest, Opposite, ... (several with |)
            public string status = "None";     // ApplyStatus: Burned, Exhausted, Blessed, Rooted, ...
            public int days, potency;
            public string slotEffect = "None"; // PlaceSlotEffect: Landmine, BarbedSnare, Shield, ScorchedEarth, Checkpoint, Prayer
            public string trend = "None";      // SetTrend: Zeitgeist, RushHour, SurvivalOfTheFittest, VolcanoLair, Casino, LiminalSpace
            public string ability = "None";    // GrantAbility, Equip
            public string requires = "None";   // A requisite on the target: Revealed, Deploying, Online, ...
            public bool attackStat = true, healthStat = true, speedStat; // ModifyStat: which stats
            public string cleanse = "Statuses"; // Cleanse: Statuses, StatModifications, Both
            public bool purge;                 // Cleanse: the good too
            public string sendFor = "Any";     // SendFor: Any, Creature, Spell (value: only among the top so many of the deck)
            public bool fromDiscard;           // SendFor: from the discard pile
            public int delay;                  // A spell's part that waits this many Days
            public bool onlyIfTargetDies;
            public string decoy;               // SummonDecoys: the token's name
            public WatchdogFields watchdog = new WatchdogFields();
        }

        [Serializable]
        public class WatchdogFields {
            public string watches = "Attack";  // Attack, Spell, Move, Ability, Summon, Status, Intelligence
            public string side = "Enemy";      // Enemy, Friendly, Any
            public int atMost;                 // "Attack 3 or less"; 0: any
            public string effect = "Nullify";  // Nullify, Redirect, Reorder, Reveal, Backfire
            public int charges;                // 0 counts as 1
            public string timing = "OnWatch";  // OnWatch, Briefing
            public string pick = "First";      // Briefing only: First, Strongest, Every
            public bool moveEarlier, damaging;
        }

        [Serializable]
        public class DeckSpec {
            public string name;
            public string opponent;  // Who brings it (the mod's name if empty)
            public string tagline;
            public string strategy;
            public List<string> cards = new List<string>(); // Card names: the file's own, or any of the game's
        }
        #endregion

        /// <summary>The game's own card files, in the order the subfactions are listed; Potions last. Shelved.json isn't loaded.</summary>
        public static readonly string[] GameFiles = { "Corporate", "Statecraft", "Offline", "Online", "ShadowOps", "Tech", "Myth", "Mind", "Utopia", "Potions" };
        public const string Folder = "Assets/Cards";

        public static CardFile Read(string json) {
            var file = JsonUtility.FromJson<CardFile>(json);
            if (file == null) throw new InvalidOperationException("not a card file");
            return file;
        }

        #region Onto an asset
        /// <summary>
        /// Puts the card's fields onto the asset (its id, name object and art are the caller's). <paramref name="fileFaction"/> is
        /// the card's unless the file says otherwise; <paramref name="find"/> finds a card by name (a token's disguise, a decoy);
        /// <paramref name="problem"/> hears about names the game doesn't know.
        /// </summary>
        public static void Apply(CardSpec card, CardDataScriptableObject asset, Faction fileFaction, Func<string, CardDataScriptableObject> find, Action<string> problem) {
            string where = card.name;
            asset.cardName = card.name;
            asset.description = card.flavor ?? "";
            asset.designNotes = card.notes ?? "";
            asset.artDescription = card.picture ?? "";
            asset.faction = string.IsNullOrWhiteSpace(card.faction) ? fileFaction : Enum(card.faction, fileFaction, $"{where}: faction", problem);
            asset.discardCost = Math.Max(0, card.discard);
            asset.discardChosen = card.discardChosen;
            asset.singleUse = card.singleUse || card.potion;
            asset.sacrifice = Math.Max(0, card.sacrifice);
            asset.doomCost = Math.Max(0, card.doom);
            asset.prophecy = Math.Max(0, card.prophecy);
            asset.prophecyLatest = Math.Max(0, card.prophecyLatest);
            asset.gamble = card.gamble;
            asset.homing = card.homing;
            asset.ripening = card.ripening;
            asset.policy = card.policy;
            asset.combo = card.combo;
            asset.potion = card.potion;
            asset.setUpBy = card.setUpBy != null && card.setUpBy.Count > 0 ? card.setUpBy.ToArray() : null;
            bool creature = asset is CreatureCardScriptableObject;
            asset.ReplaceEffects((card.effects ?? new List<EffectSpec>()).Select(e => MakeEffect(e, creature, where, find, problem)));
            switch (asset) {
                case CreatureCardScriptableObject c:
                    c.attack = Math.Max(0, card.attack);
                    c.health = Math.Max(1, card.health);
                    c.speed = Math.Max(1, card.speed);
                    c.turnsUntilOperative = Math.Max(0, card.deploy);
                    c.ability = Enum(card.ability, CreatureAbility.None, $"{where}: ability", problem);
                    c.movement = Enum(card.movement, MovementKeyword.None, $"{where}: movement", problem);
                    c.gift = card.gift;
                    c.rootedWhileDeploying = card.rooted;
                    c.token = card.token;
                    c.disguise = string.IsNullOrWhiteSpace(card.disguise) ? null : Found(card.disguise, find, $"{where}: disguise", problem) as CreatureCardScriptableObject;
                    break;
                case SpellCardScriptableObject s:
                    s.defaultTargetType = Enum(card.target, TargetType.EnemyCreatures, $"{where}: target", problem);
                    break;
            }
        }

        public static bool IsCreature(CardSpec card) => !string.Equals(card.type, "spell", StringComparison.OrdinalIgnoreCase);

        private static CardEffect MakeEffect(EffectSpec effect, bool creature, string where, Func<string, CardDataScriptableObject> find, Action<string> problem) => new CardEffect {
            effectType = creature ? EffectType.Immediate : EffectType.Triggered, // As the generator always made them
            trigger = Enum(effect.trigger, EffectTrigger.OnPlay, $"{where}: trigger", problem),
            oncePerCombat = effect.once,
            actions = (effect.actions ?? new List<ActionSpec>()).Select(a => MakeAction(a, where, find, problem)).ToList()
        };

        private static EffectAction MakeAction(ActionSpec a, string where, Func<string, CardDataScriptableObject> find, Action<string> problem) {
            var w = a.watchdog ?? new WatchdogFields();
            var action = new EffectAction {
                actionType = Enum(a.action, ActionType.Damage, $"{where}: action", problem),
                value = a.value,
                targetType = Enum(a.target, TargetType.EnemyCreatures, $"{where}: target", problem),
                targetModifier = Flags(a.modifier, where, problem),
                statusEffectToApply = Enum(a.status, StatusEffectType.None, $"{where}: status", problem),
                statusDuration = a.days,
                statusPotency = a.potency,
                slotEffect = Enum(a.slotEffect, SlotEffectType.None, $"{where}: slot effect", problem),
                trend = Enum(a.trend, Trend.None, $"{where}: trend", problem),
                ability = Enum(a.ability, CreatureAbility.None, $"{where}: ability", problem),
                requires = Enum(a.requires, TargetRequisite.None, $"{where}: requires", problem),
                modifyAttack = a.attackStat,
                modifyHealth = a.healthStat,
                modifySpeed = a.speedStat,
                cleanse = Enum(a.cleanse, CleanseTarget.Statuses, $"{where}: cleanse", problem),
                purge = a.purge,
                sendFor = Enum(a.sendFor, SendForFilter.Any, $"{where}: send for", problem),
                fromDiscard = a.fromDiscard,
                delayDays = a.delay,
                onlyIfTargetDies = a.onlyIfTargetDies,
                watchdog = new WatchdogSpec {
                    watches = Enum(w.watches, WatchedAction.Attack, $"{where}: watchdog", problem),
                    side = Enum(w.side, WatchdogSide.Enemy, $"{where}: watchdog side", problem),
                    atMost = w.atMost,
                    effect = Enum(w.effect, WatchdogEffect.Nullify, $"{where}: watchdog effect", problem),
                    charges = w.charges,
                    timing = Enum(w.timing, WatchdogTiming.OnWatch, $"{where}: watchdog timing", problem),
                    pick = Enum(w.pick, BriefingPick.First, $"{where}: watchdog pick", problem),
                    moveEarlier = w.moveEarlier,
                    damaging = w.damaging
                }
            };
            if (!string.IsNullOrWhiteSpace(a.decoy)) action.decoy = Found(a.decoy, find, $"{where}: decoy", problem) as CreatureCardScriptableObject;
            if (action.actionType == ActionType.Stun && action.value <= 0) action.value = 1;
            return action;
        }

        private static CardDataScriptableObject Found(string name, Func<string, CardDataScriptableObject> find, string where, Action<string> problem) {
            var card = find?.Invoke(name);
            if (card == null) problem?.Invoke($"{where}: no card called '{name}' (it must come earlier in the file, or be the game's)");
            return card;
        }

        // A name from the file as one of the game's own, any case; a wrong one is reported and the fallback used
        public static T Enum<T>(string name, T fallback, string where, Action<string> problem) where T : struct {
            if (string.IsNullOrWhiteSpace(name)) return fallback;
            if (System.Enum.TryParse(name.Replace(" ", "").Replace("-", "").Replace("'", ""), true, out T value)) return value;
            problem?.Invoke($"{where}: '{name}' isn't one of the game's {typeof(T).Name}s; {fallback} used");
            return fallback;
        }

        private static TargetModifier Flags(string names, string where, Action<string> problem) {
            var result = TargetModifier.None;
            foreach (var part in (names ?? "").Split('|')) result |= Enum(part.Trim(), TargetModifier.None, $"{where}: modifier", problem);
            return result;
        }
        #endregion

        #region From an asset
        /// <summary>The asset's fields as a card of a file whose cards are <paramref name="fileFaction"/>'s (its own faction written only when it differs: a fake face).</summary>
        public static CardSpec ToSpec(CardDataScriptableObject asset, Faction fileFaction) {
            var card = new CardSpec {
                name = asset.cardName,
                type = asset is CreatureCardScriptableObject ? "creature" : "spell",
                faction = asset.faction == fileFaction ? null : asset.faction.ToString(),
                flavor = asset.description,
                notes = asset.designNotes,
                picture = asset.artDescription,
                singleUse = asset.singleUse && !asset.potion,
                discard = asset.discardCost,
                discardChosen = asset.discardChosen,
                sacrifice = asset.sacrifice,
                doom = asset.doomCost,
                prophecy = asset.prophecy,
                prophecyLatest = asset.prophecyLatest,
                gamble = asset.gamble,
                homing = asset.homing,
                ripening = asset.ripening,
                policy = asset.policy,
                combo = asset.combo,
                potion = asset.potion,
                setUpBy = asset.setUpBy?.ToList() ?? new List<string>(),
                effects = asset.Effects.Select(ToSpec).ToList()
            };
            switch (asset) {
                case CreatureCardScriptableObject c:
                    card.attack = c.attack;
                    card.health = c.health;
                    card.speed = c.speed;
                    card.deploy = c.turnsUntilOperative;
                    card.ability = c.ability.ToString();
                    card.movement = c.movement.ToString();
                    card.gift = c.gift;
                    card.rooted = c.rootedWhileDeploying;
                    card.token = c.token;
                    card.disguise = c.disguise != null ? c.disguise.cardName : null;
                    break;
                case SpellCardScriptableObject s:
                    card.target = s.defaultTargetType.ToString();
                    break;
            }
            return card;
        }

        private static EffectSpec ToSpec(CardEffect effect) => new EffectSpec {
            trigger = effect.trigger.ToString(),
            once = effect.oncePerCombat,
            actions = effect.actions.Select(ToSpec).ToList()
        };

        private static ActionSpec ToSpec(EffectAction a) => new ActionSpec {
            action = a.actionType.ToString(),
            value = a.value,
            target = a.targetType.ToString(),
            modifier = a.targetModifier == TargetModifier.None ? "None" : string.Join("|", System.Enum.GetValues(typeof(TargetModifier)).Cast<TargetModifier>().Where(f => f != TargetModifier.None && a.targetModifier.HasFlag(f)).Select(f => f.ToString())),
            status = a.statusEffectToApply.ToString(),
            days = a.statusDuration,
            potency = a.statusPotency,
            slotEffect = a.slotEffect.ToString(),
            trend = a.trend.ToString(),
            ability = a.ability.ToString(),
            requires = a.requires.ToString(),
            attackStat = a.modifyAttack,
            healthStat = a.modifyHealth,
            speedStat = a.modifySpeed,
            cleanse = a.cleanse.ToString(),
            purge = a.purge,
            sendFor = a.sendFor.ToString(),
            fromDiscard = a.fromDiscard,
            delay = a.delayDays,
            onlyIfTargetDies = a.onlyIfTargetDies,
            decoy = a.decoy != null ? a.decoy.cardName : null,
            watchdog = new WatchdogFields {
                watches = a.watchdog.watches.ToString(), side = a.watchdog.side.ToString(), atMost = a.watchdog.atMost, effect = a.watchdog.effect.ToString(),
                charges = a.watchdog.charges, timing = a.watchdog.timing.ToString(), pick = a.watchdog.pick.ToString(), moveEarlier = a.watchdog.moveEarlier, damaging = a.watchdog.damaging
            }
        };
        #endregion

        #region Writing
        /// <summary>The file as JSON, one card a block, a field left out when it's at its default, so the files read like card lists.</summary>
        public static string Write(CardFile file) {
            var text = new StringBuilder();
            text.Append("{\n");
            text.Append($"  \"mod\": {Quote(file.mod)}");
            if (!string.IsNullOrEmpty(file.author)) text.Append($",\n  \"author\": {Quote(file.author)}");
            text.Append(",\n  \"cards\": [\n");
            for (int i = 0; i < file.cards.Count; i++) {
                text.Append(WriteCard(file.cards[i]));
                text.Append(i < file.cards.Count - 1 ? ",\n" : "\n");
            }
            text.Append("  ]");
            if (file.decks != null && file.decks.Count > 0) {
                text.Append(",\n  \"decks\": [\n");
                for (int i = 0; i < file.decks.Count; i++) {
                    var deck = file.decks[i];
                    text.Append("    { ");
                    text.Append(string.Join(", ", new[] {
                        Field("name", deck.name), Field("opponent", deck.opponent), Field("tagline", deck.tagline), Field("strategy", deck.strategy),
                        $"\"cards\": [{string.Join(", ", (deck.cards ?? new List<string>()).Select(Quote))}]"
                    }.Where(f => f != null)));
                    text.Append(i < file.decks.Count - 1 ? " },\n" : " }\n");
                }
                text.Append("  ]");
            }
            text.Append("\n}\n");
            return text.ToString();
        }

        private static string WriteCard(CardSpec c) {
            bool creature = IsCreature(c);
            var head = new List<string> { Field("name", c.name), $"\"type\": \"{(creature ? "creature" : "spell")}\"" };
            if (!string.IsNullOrEmpty(c.faction)) head.Add(Field("faction", c.faction));
            if (creature) {
                head.Add($"\"attack\": {c.attack}");
                head.Add($"\"health\": {c.health}");
                head.Add($"\"speed\": {c.speed}");
                head.Add($"\"deploy\": {c.deploy}");
                if (c.ability != "None") head.Add(Field("ability", c.ability));
                if (c.movement != "None") head.Add(Field("movement", c.movement));
                if (c.gift) head.Add("\"gift\": true");
                if (c.rooted) head.Add("\"rooted\": true");
                if (c.token) head.Add("\"token\": true");
                if (!string.IsNullOrEmpty(c.disguise)) head.Add(Field("disguise", c.disguise));
            }
            else {
                head.Add(Field("target", c.target));
            }
            if (c.singleUse) head.Add("\"singleUse\": true");
            if (c.discard != 0) head.Add($"\"discard\": {c.discard}");
            if (c.discardChosen) head.Add("\"discardChosen\": true");
            if (c.sacrifice != 0) head.Add($"\"sacrifice\": {c.sacrifice}");
            if (c.doom != 0) head.Add($"\"doom\": {c.doom}");
            if (c.prophecy != 0) head.Add($"\"prophecy\": {c.prophecy}");
            if (c.prophecyLatest != 0) head.Add($"\"prophecyLatest\": {c.prophecyLatest}");
            if (c.gamble) head.Add("\"gamble\": true");
            if (c.homing) head.Add("\"homing\": true");
            if (c.ripening) head.Add("\"ripening\": true");
            if (c.policy) head.Add("\"policy\": true");
            if (c.combo) head.Add("\"combo\": true");
            if (c.potion) head.Add("\"potion\": true");
            if (c.setUpBy != null && c.setUpBy.Count > 0) head.Add($"\"setUpBy\": [{string.Join(", ", c.setUpBy.Select(Quote))}]");
            if (!string.IsNullOrEmpty(c.art)) head.Add(Field("art", c.art));

            var text = new StringBuilder();
            text.Append("    {\n      ").Append(string.Join(", ", head));
            if (!string.IsNullOrEmpty(c.flavor)) text.Append(",\n      ").Append(Field("flavor", c.flavor));
            if (!string.IsNullOrEmpty(c.notes)) text.Append(",\n      ").Append(Field("notes", c.notes));
            if (!string.IsNullOrEmpty(c.picture)) text.Append(",\n      ").Append(Field("picture", c.picture));
            if (c.effects != null && c.effects.Count > 0) {
                text.Append(",\n      \"effects\": [\n");
                for (int i = 0; i < c.effects.Count; i++) {
                    var e = c.effects[i];
                    text.Append("        { ").Append(Field("trigger", e.trigger));
                    if (e.once) text.Append(", \"once\": true");
                    text.Append(", \"actions\": [\n");
                    for (int j = 0; j < e.actions.Count; j++) {
                        text.Append("          { ").Append(WriteAction(e.actions[j])).Append(j < e.actions.Count - 1 ? " },\n" : " }\n");
                    }
                    text.Append("        ] }").Append(i < c.effects.Count - 1 ? ",\n" : "\n");
                }
                text.Append("      ]");
            }
            text.Append("\n    }");
            return text.ToString();
        }

        private static string WriteAction(ActionSpec a) {
            var fields = new List<string> { Field("action", a.action) };
            if (a.value != 0) fields.Add($"\"value\": {a.value}");
            fields.Add(Field("target", a.target));
            if (a.modifier != "None") fields.Add(Field("modifier", a.modifier));
            if (a.status != "None") fields.Add(Field("status", a.status));
            if (a.days != 0) fields.Add($"\"days\": {a.days}");
            if (a.potency != 0) fields.Add($"\"potency\": {a.potency}");
            if (a.slotEffect != "None") fields.Add(Field("slotEffect", a.slotEffect));
            if (a.trend != "None") fields.Add(Field("trend", a.trend));
            if (a.ability != "None") fields.Add(Field("ability", a.ability));
            if (a.requires != "None") fields.Add(Field("requires", a.requires));
            if (a.action == "ModifyStat" || !a.attackStat || !a.healthStat || a.speedStat) {
                fields.Add($"\"attackStat\": {(a.attackStat ? "true" : "false")}");
                fields.Add($"\"healthStat\": {(a.healthStat ? "true" : "false")}");
                if (a.speedStat) fields.Add("\"speedStat\": true");
            }
            if (a.cleanse != "Statuses") fields.Add(Field("cleanse", a.cleanse));
            if (a.purge) fields.Add("\"purge\": true");
            if (a.sendFor != "Any") fields.Add(Field("sendFor", a.sendFor));
            if (a.fromDiscard) fields.Add("\"fromDiscard\": true");
            if (a.delay != 0) fields.Add($"\"delay\": {a.delay}");
            if (a.onlyIfTargetDies) fields.Add("\"onlyIfTargetDies\": true");
            if (!string.IsNullOrEmpty(a.decoy)) fields.Add(Field("decoy", a.decoy));
            if (a.action == "CreateWatchdog") {
                var w = a.watchdog ?? new WatchdogFields();
                var dog = new List<string> { Field("watches", w.watches), Field("side", w.side), Field("effect", w.effect) };
                if (w.atMost != 0) dog.Add($"\"atMost\": {w.atMost}");
                if (w.charges != 0) dog.Add($"\"charges\": {w.charges}");
                if (w.timing != "OnWatch") dog.Add(Field("timing", w.timing));
                if (w.pick != "First") dog.Add(Field("pick", w.pick));
                if (w.moveEarlier) dog.Add("\"moveEarlier\": true");
                if (w.damaging) dog.Add("\"damaging\": true");
                fields.Add($"\"watchdog\": {{ {string.Join(", ", dog)} }}");
            }
            return string.Join(", ", fields);
        }

        private static string Field(string name, string value) => value == null ? null : $"\"{name}\": {Quote(value)}";

        private static string Quote(string value) {
            var text = new StringBuilder("\"");
            foreach (char ch in value ?? "") {
                switch (ch) {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': break;
                    case '\t': text.Append("\\t"); break;
                    default: text.Append(ch); break;
                }
            }
            return text.Append('"').ToString();
        }
        #endregion
    }
}
