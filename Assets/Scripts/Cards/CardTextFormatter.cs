using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Builds the printed text of a card from its data, in the style of Magic: The Gathering and Slay the Spire:
    /// a line of keywords first (movement, ability, and keyword abilities like Retaliate 1 or Decay 1 for effects that
    /// keep coming back), then the rest as short templated clauses led by a trigger keyword ("Dawn: draw 1."): what it does,
    /// then whom it targets, with the targeting keywords (target, all friendly, random enemy...).
    /// Every keyword is a link to its explanation (<see cref="Keywords"/>), shown when it's hovered on an enlarged card.
    /// </summary>
    public static class CardTextFormatter {

        public static string GetTypeLine(CardData data) {
            string type = data is SpellData ? "Spell" : "Creature";
            return data.faction == Faction.None ? type : $"{type} — {FactionName(data.faction)}";
        }

        public static string FactionName(Faction faction) {
            switch (faction) {
                case Faction.ShadowOps: return "Shadow Ops.";
                case Faction.DigitalSociety: return "Digital Society";
                default: return faction.ToString();
            }
        }

        public static string GetRulesText(CardData data) {
            var creature = data as CreatureData;
            bool isSpell = data is SpellData;
            var keywords = new List<string>();
            if (creature != null) {
                if (creature.movement.HasFlag(MovementKeyword.Leapfrog)) keywords.Add(Keywords.Link("Leapfrog"));
                if (creature.movement.HasFlag(MovementKeyword.Swap)) keywords.Add(Keywords.Link("Swap"));
                if (creature.ability != CreatureAbility.None) keywords.Add(AbilityName(creature.ability));
            }

            var sentences = new List<string>();
            foreach (var effect in data.effects ?? new List<CardEffect>()) {
                string keyword = isSpell ? null : KeywordAbility(effect);
                if (keyword != null) keywords.Add(keyword);
                else {
                    string sentence = DescribeEffect(effect, isSpell);
                    if (sentence != null) sentences.Add(sentence);
                }
            }

            string keywordLine = keywords.Count > 0 ? string.Join(", ", keywords) + "." : "";
            return string.Join("\n", new[] { keywordLine }.Concat(sentences).Where(line => !string.IsNullOrEmpty(line)));
        }

        /// <summary>The rules text of these effects alone (no keyword line for the card's movement or ability).</summary>
        public static string GetRulesText(IEnumerable<CardEffect> effects, bool isSpell) {
            if (effects == null) return "";
            var lines = effects.Select(e => (!isSpell ? KeywordAbility(e) : null) ?? DescribeEffect(e, isSpell)).Where(l => l != null);
            return string.Join("\n", lines);
        }

        /// <summary>A creature's ability as printed: its name, a link to what it does.</summary>
        public static string AbilityLine(CreatureAbility ability) => ability == CreatureAbility.None ? "" : AbilityName(ability) + ".";

        private static string AbilityName(CreatureAbility ability) => Keywords.Link(Abilities.NameOf(ability));

        #region Keyword abilities
        // Effects that keep coming back get a keyword, like Magic's "Lifelink" or Slay the Spire's "Retain".
        // Only an effect that is exactly the pattern becomes the keyword; anything more is written out.
        private static string KeywordAbility(CardEffect effect) {
            if (effect?.actions == null || effect.actions.Count != 1) return null;
            var action = effect.actions[0];
            bool plain = action.targetModifier == TargetModifier.None;
            if (!plain) return null;

            if (effect.oncePerCombat) {
                return effect.trigger == EffectTrigger.OnAboutToDie && action.actionType == ActionType.Heal && action.targetType == TargetType.Self
                    ? Keywords.Link("Second Wind") : null;
            }
            switch (effect.trigger) {
                case EffectTrigger.OnDamage when action.actionType == ActionType.Damage && action.targetType == TargetType.Enemy:
                    return $"{Keywords.Link("Retaliate")} {action.value}";
                case EffectTrigger.EndOfTurn when action.targetType == TargetType.Self && action.actionType == ActionType.Damage:
                    return $"{Keywords.Link("Decay")} {action.value}";
                case EffectTrigger.EndOfTurn when action.targetType == TargetType.Self && action.actionType == ActionType.Heal:
                    return $"{Keywords.Link("Regenerate")} {action.value}";
                case EffectTrigger.StartOfTurn when action.targetType == TargetType.Self && action.actionType == ActionType.ModifyStat
                                                    && action.modifyAttack && action.modifyHealth && !action.modifySpeed
                                                    && action.value > 0 && action.statusDuration <= 0:
                    return $"{Keywords.Link("Grow")} {action.value}";
                default:
                    return null;
            }
        }
        #endregion

        #region Sentences
        // Short templated clauses: what it does first, then whom ("3 damage to target", "Doom 3 on all enemy creatures").
        // The same damage or healing to every creature and both players is "everything".
        // A number after a status is its Days ("Doom 3"); a status with a strength shows the Days with the calendar
        // ("Burn 2 [calendar]3": 2 damage, 3 Days). The rest of the meaning lives in the keywords' explanations.
        private static string DescribeEffect(CardEffect effect, bool isSpell) {
            var actions = effect.actions.Where(a => DescribeAction(a, isSpell) != null).ToList();
            if (actions.Count == 0) return null;

            // Retaliation: an enemy-targeted Hurt effect lands on whoever damaged this creature (see EffectResolver)
            bool retaliates = !isSpell && effect.trigger == EffectTrigger.OnDamage
                              && actions.All(a => a.targetType == TargetType.Enemy && a.targetModifier == TargetModifier.None);
            (string icon, string text) trigger = isSpell ? default : retaliates ? (EffectIcons.Hurt, Keywords.Link("Retaliate")) : DescribeTrigger(effect);
            var clauses = new List<string>();
            foreach (var group in GroupForEverything(actions)) {
                clauses.Add(group.Count > 1
                    ? DescribeAction(group[0], isSpell, Keywords.Link("Everything", "everything"))
                    : DescribeAction(group[0], isSpell, retaliates ? "it" : null));
            }
            string body = JoinClauses(clauses);
            string once = effect.oncePerCombat ? "Once per combat, " : "";

            if (trigger.text == null) return CapitalizeRich(once + body) + ".";
            string lead = trigger.text.StartsWith("<") ? $"{trigger.text}:" : $"{trigger.text},";
            return $"{EffectIcons.Tag(trigger.icon)}{(once.Length > 0 ? once + LowerFirst(lead) : lead)} {body}.";
        }

        // Damage or healing of the same amount to all creatures, you and the enemy player reads as one clause: "everything"
        private static List<List<EffectAction>> GroupForEverything(List<EffectAction> actions) {
            var groups = actions.Select(a => new List<EffectAction> { a }).ToList();
            foreach (var type in new[] { ActionType.Damage, ActionType.Heal }) {
                foreach (int value in actions.Where(a => a.actionType == type).Select(a => a.value).Distinct().ToList()) {
                    bool Each(EffectAction a, TargetType target) => a.actionType == type && a.value == value && a.targetType == target
                                                                   && a.targetModifier == TargetModifier.AllSameTypeTargets;
                    var parts = new[] { TargetType.AllCreatures, TargetType.Player, TargetType.Enemy }
                        .Select(t => actions.FirstOrDefault(a => Each(a, t))).ToList();
                    if (parts.Any(p => p == null)) continue;
                    int first = groups.FindIndex(g => g.Count == 1 && parts.Contains(g[0]));
                    groups.RemoveAll(g => g.Count == 1 && parts.Contains(g[0]));
                    groups.Insert(first, parts);
                }
            }
            return groups;
        }

        private static (string icon, string text) DescribeTrigger(CardEffect effect) {
            switch (effect.trigger) {
                case EffectTrigger.OnPlay: return (EffectIcons.Enter, Keywords.Link("Arrival"));
                case EffectTrigger.OnDeath: return (EffectIcons.Death, Keywords.Link("Last Will"));
                case EffectTrigger.OnAboutToDie: return (EffectIcons.Death, "When this would die");
                case EffectTrigger.OnDamage:
                    // Chain/spread effects fire when this creature's attack lands; the rest when it is damaged
                    return effect.actions.Any(a => a.targetModifier != TargetModifier.None)
                        ? (EffectIcons.Attack, Keywords.Link("On Hit"))
                        : (EffectIcons.Hurt, Keywords.Link("Hurt"));
                case EffectTrigger.StartOfTurn: return (EffectIcons.Dawn, Keywords.Link("Dawn"));
                case EffectTrigger.EndOfTurn: return (EffectIcons.Dusk, Keywords.Link("Dusk"));
                case EffectTrigger.ActionAttempted: return (EffectIcons.Attack, "Whenever this acts");
                default: return (null, null);
            }
        }

        // One clause; target overrides who it lands on ("it": whoever damaged this creature)
        private static string DescribeAction(EffectAction action, bool isSpell, string targetOverride = null) {
            string target = targetOverride ?? DescribeTarget(action, isSpell);
            int value = action.value;

            switch (action.actionType) {
                case ActionType.Damage:
                    if (action.targetModifier.HasFlag(TargetModifier.Chained))
                        return $"{value} damage to its target, chaining to 2 more at half each jump";
                    return $"{value} damage to {target}";
                case ActionType.Heal:
                    return $"heal {value} to {target}";
                case ActionType.Draw:
                    return action.targetType == TargetType.Enemy ? $"enemy draws {value}" : $"draw {value}";
                case ActionType.Summon:
                    return $"{Keywords.Link("Summon", "summon")} {value}";
                case ActionType.Armor:
                    return $"{target} +{value}{Keywords.Link("Armor", EffectIcons.Tag(EffectIcons.Armor))}";
                case ActionType.Stun:
                    return $"{Keywords.Link("Exhausted", "exhaust")} {target}";
                case ActionType.ModifyStat:
                    return $"{target} {DescribeStatChange(action)}";
                case ActionType.ApplyStatus:
                    return DescribeStatus(action, target);
                case ActionType.DelayDeployment:
                    return $"+{value} {Keywords.Link("Deployment Time")} to {target}";
                case ActionType.PlaceSlotEffect:
                    return $"{DescribeSlotEffect(action)} on {SlotTarget(action, isSpell)}";
                case ActionType.CleanseSlot:
                    return $"clear the {Keywords.Link("Slot Effect")} on {SlotTarget(action, isSpell)}";
                case ActionType.CreateWatchdog:
                    return DescribeWatchdog(action, target);
                case ActionType.Reveal: // Instant: it resolves as soon as it's played
                    if (action.targetType is TargetType.Player or TargetType.Enemy) return $"{Keywords.Link("Reveal", "reveal")} {target}'s {Keywords.Link("Watchdog")} conditions";
                    return action.targetModifier.HasFlag(TargetModifier.Random)
                        ? $"{Keywords.Link("Reveal", "reveal")} {target}"
                        : $"{Keywords.Link("Reveal", "reveal")} {SlotTarget(action, isSpell)}";
                case ActionType.Cleanse: // A Cleanse removes the bad; a Purge removes everything
                    string what = action.cleanse switch {
                        CleanseTarget.StatModifications => " stat changes on",
                        CleanseTarget.Both => " statuses and stat changes on",
                        _ => ""
                    };
                    return action.purge ? $"{Keywords.Link("Purge", "purge")}{what} {target}" : $"{Keywords.Link("Cleanse", "cleanse")}{what} {target}";
                default:
                    return null;
            }
        }

        // "target" (what it's dropped on, either side) and "random" are keywords; groups ("all enemy creatures"), players
        // and the creature itself are plain words.
        private static string DescribeTarget(EffectAction action, bool isSpell) {
            if (action.targetModifier.HasFlag(TargetModifier.Random)) {
                switch (action.targetType) {
                    case TargetType.EnemyCreatures: return "a " + Keywords.Link("Random", "random") + " enemy creature";
                    case TargetType.FriendlyCreatures: return "a " + Keywords.Link("Random", "random") + " friendly creature";
                    case TargetType.AllCreatures: return "a " + Keywords.Link("Random", "random") + " creature";
                }
            }

            string adjacent = action.targetModifier.HasFlag(TargetModifier.WithAdjacent) ? " and its neighbors" : "";
            // Spells resolve against whatever they are dropped on unless they hit a whole group
            bool hitsGroup = action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets);
            if (isSpell && !hitsGroup && action.targetType is not (TargetType.Player or TargetType.Enemy)) {
                return Keywords.Link("Target", "target") + adjacent;
            }

            switch (action.targetType) {
                case TargetType.Self: return "this" + adjacent;
                case TargetType.Player: return "you";
                case TargetType.Enemy: return "enemy player";
                case TargetType.FriendlyCreatures: return "all friendly creatures";
                case TargetType.EnemyCreatures: return "all enemy creatures";
                case TargetType.AllCreatures: return "all creatures";
                default: return Keywords.Link("Target", "target");
            }
        }

        // "+1 Attack", "+4 Attack [calendar]1" for a Day; a Health decrease is Health Loss, so it's a link to that rule
        private static string DescribeStatChange(EffectAction action) {
            string sign = action.value >= 0 ? "+" : "";
            var stats = new List<string>();
            if (action.modifyAttack) stats.Add("Attack");
            if (action.modifyHealth) stats.Add("Health");
            if (action.modifySpeed) stats.Add("Speed");
            if (stats.Count == 0) return "no change";
            string change = $"{sign}{action.value} {string.Join("/", stats)}";
            if (action.value < 0 && action.modifyHealth) change = Keywords.Link("Health Loss", change);
            return change + DaysTag(action.statusDuration);
        }

        // A status by its keyword: the number is its Days ("Doom 3"), or its strength and then its Days ("Burn 2 [calendar]3")
        private static string DescribeStatus(EffectAction action, string target) {
            int days = action.statusDuration;
            int potency = System.Math.Max(1, action.statusPotency);
            switch (action.statusEffectToApply) {
                case StatusEffectType.Exhausted: return $"{Keywords.Link("Exhausted", "exhaust")} {target}";
                case StatusEffectType.Burned: return $"{Keywords.Link("Burned", $"Burn {potency}")}{DaysTag(days)} on {target}";
                case StatusEffectType.Doomed: return $"{Keywords.Link("Doomed", $"Doom {days}")} on {target}";
                case StatusEffectType.Blessed: return $"{Keywords.Link("Blessed", $"Bless {potency}")} on {target}";
                case StatusEffectType.HeatSignature: return $"{Keywords.Link("Heat Signal", $"Heat Signal {potency}")} on {target}";
                case StatusEffectType.Concealed: return $"{Keywords.Link("Concealed", $"Conceal {days}")} on {target}";
                case StatusEffectType.Revealed: return $"{Keywords.Link("Revealed", $"Revealed {days}")} on {target}";
                case StatusEffectType.Encrypted: return $"{Keywords.Link("Encrypted", $"Encrypt {days}")} on {target}";
                default: return null;
            }
        }

        // " [calendar]3": how many Days a timed change lasts. Nothing for a lasting one
        private static string DaysTag(int days) => days > 0 ? " " + Keywords.Link("Days", $"{EffectIcons.Tag(EffectIcons.Days)}{days}") : "";

        // The GDD card format: "Watchdog: [condition] -> [effect], N charges (timing)"
        private static string DescribeWatchdog(EffectAction action, string target) {
            var spec = action.watchdog;
            string whose = spec.side switch { WatchdogSide.Enemy => "enemy ", WatchdogSide.Friendly => "friendly ", _ => "" };
            string what = spec.watches switch {
                WatchedAction.Attack => "attacks", WatchedAction.Spell => "spells", WatchedAction.Move => "moves",
                WatchedAction.Ability => "abilities", WatchedAction.Summon => "summons", _ => "statuses"
            };
            bool watchesCreature = action.targetType is not (TargetType.Player or TargetType.Enemy);
            string by = watchesCreature ? $" by {target}" : "";
            string atMost = spec.atMost > 0 ? $" of {spec.atMost} or less" : "";
            string effect = spec.effect switch {
                WatchdogEffect.Nullify => Keywords.Link("Nullify"), WatchdogEffect.Redirect => Keywords.Link("Redirect"),
                WatchdogEffect.Reveal => Keywords.Link("Reveal"),
                _ => Keywords.Link("Reorder", spec.moveEarlier ? "move it first" : "move it last")
            };
            int charges = System.Math.Max(1, spec.charges);
            string pick = spec.timing == WatchdogTiming.Briefing && spec.pick != BriefingPick.First ? $"{spec.pick.ToString().ToLower()} " : "";
            string timing = spec.timing == WatchdogTiming.Briefing ? Keywords.Link("Briefing") : Keywords.Link("Monitored");
            return $"{Keywords.Link("Watchdog")}: {pick}{whose}{what}{atMost}{by} -> {effect}, {charges} charge{(charges == 1 ? "" : "s")} ({timing}{DaysTag(action.statusDuration)})";
        }

        // Slot Effects aim at the slot, whoever stands in it
        private static string SlotTarget(EffectAction action, bool isSpell) =>
            isSpell || action.targetType != TargetType.Self ? "target slot" : "this creature's slot";

        // "Landmine 3" (its damage), "Temporal Shield 2" (its Days)
        private static string DescribeSlotEffect(EffectAction action) {
            string days = action.statusDuration > 0 ? $" {action.statusDuration}" : "";
            return action.slotEffect switch {
                SlotEffectType.Landmine => Keywords.Link("Landmine", $"Landmine {action.value}"),
                SlotEffectType.BarbedSnare => Keywords.Link("Barbed Snare", $"Barbed Snare {action.value}"),
                SlotEffectType.TemporalShield => Keywords.Link("Temporal Shield", $"Temporal Shield{days}"),
                SlotEffectType.ScorchedEarth => Keywords.Link("Scorched Earth") + DaysTag(action.statusDuration),
                _ => "nothing"
            };
        }

        public static string SlotEffectIcon(SlotEffectType type) => type switch {
            SlotEffectType.Landmine => EffectIcons.Damage,
            SlotEffectType.BarbedSnare => EffectIcons.Hurt,
            SlotEffectType.TemporalShield => EffectIcons.Armor,
            SlotEffectType.ScorchedEarth => EffectIcons.Burn,
            _ => null
        };
        #endregion

        private static string JoinClauses(IList<string> clauses) {
            if (clauses.Count == 1) return clauses[0];
            return string.Join(", ", clauses.Take(clauses.Count - 1)) + " and " + clauses[clauses.Count - 1];
        }


        // Upper-cases the first letter that is read, skipping rich-text tags and icons in front of it
        private static string CapitalizeRich(string text) {
            bool inTag = false;
            for (int i = 0; i < (text?.Length ?? 0); i++) {
                char c = text[i];
                if (c == '<') inTag = true;
                else if (c == '>') inTag = false;
                else if (!inTag && !char.IsWhiteSpace(c)) {
                    // "3 damage to target" starts with its number: nothing to capitalize
                    return char.IsLetter(c) ? text.Substring(0, i) + char.ToUpper(c) + text.Substring(i + 1) : text;
                }
            }
            return text;
        }

        private static string LowerFirst(string text) => text.StartsWith("<") ? text : char.ToLower(text[0]) + text.Substring(1);
    }
}
