using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Builds the printed text of a card from its data, in the style of Magic: The Gathering and Slay the Spire:
    /// a line of keywords first (movement, ability, and keyword abilities like Retaliate 1 or Decay 1 for effects that
    /// keep coming back), then the rest as short templated sentences led by a trigger keyword ("Dawn: draw a card.").
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
        private static string DescribeEffect(CardEffect effect, bool isSpell) {
            var actions = effect.actions.Where(a => DescribeAction(a, isSpell) != null).ToList();
            if (actions.Count == 0) return null;

            // Retaliation: an enemy-targeted When Hurt effect lands on whoever damaged this creature (see EffectResolver)
            bool retaliates = !isSpell && effect.trigger == EffectTrigger.OnDamage
                              && actions.All(a => a.targetType == TargetType.Enemy && a.targetModifier == TargetModifier.None);
            (string icon, string text) trigger = isSpell ? default : retaliates ? (EffectIcons.Hurt, Keywords.Link("Retaliate")) : DescribeTrigger(effect);
            var clauses = actions.Select(a => EffectIcons.Tag(ActionIcon(a)) + DescribeAction(a, isSpell, retaliates ? "it" : null)).ToList();
            string body = JoinClauses(clauses);
            string once = effect.oncePerCombat ? "Once per combat, " : "";

            if (trigger.text == null) return CapitalizeRich(once + body) + ".";
            string lead = trigger.text.StartsWith("<") ? $"{trigger.text}:" : $"{trigger.text},";
            return $"{EffectIcons.Tag(trigger.icon)}{(once.Length > 0 ? once + LowerFirst(lead) : lead)} {body}.";
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
                        : (EffectIcons.Hurt, Keywords.Link("When Hurt"));
                case EffectTrigger.StartOfTurn: return (EffectIcons.Dawn, Keywords.Link("Dawn"));
                case EffectTrigger.EndOfTurn: return (EffectIcons.Dusk, Keywords.Link("Dusk"));
                case EffectTrigger.ActionAttempted: return (EffectIcons.Attack, "Whenever this acts");
                default: return (null, null);
            }
        }

        private static string ActionIcon(EffectAction action) {
            switch (action.actionType) {
                case ActionType.Damage:
                    return action.targetModifier.HasFlag(TargetModifier.Chained) ? EffectIcons.Chain : EffectIcons.Damage;
                case ActionType.Heal: return EffectIcons.Heal;
                case ActionType.Draw: return EffectIcons.Draw;
                case ActionType.Summon: return EffectIcons.Summon;
                case ActionType.Armor: return EffectIcons.Armor;
                case ActionType.Stun: return EffectIcons.Paralyze;
                case ActionType.ModifyStat: return action.value >= 0 ? EffectIcons.Buff : EffectIcons.Debuff;
                case ActionType.ApplyStatus: return EffectIcons.ForStatus(action.statusEffectToApply);
                case ActionType.DelayDeployment: return EffectIcons.Dusk;
                case ActionType.Cleanse: return EffectIcons.Cleanse;
                case ActionType.PlaceSlotEffect: return SlotEffectIcon(action.slotEffect);
                case ActionType.CleanseSlot: return EffectIcons.Cleanse;
                case ActionType.CreateWatchdog: return EffectIcons.Dusk;
                case ActionType.Reveal: return EffectIcons.Reveal;
                default: return null;
            }
        }

        // One clause; target overrides who it lands on ("it": whoever damaged this creature)
        private static string DescribeAction(EffectAction action, bool isSpell, string targetOverride = null) {
            string target = targetOverride ?? DescribeTarget(action, isSpell);
            int value = action.value;

            switch (action.actionType) {
                case ActionType.Damage:
                    if (action.targetModifier.HasFlag(TargetModifier.Chained))
                        return $"deal {value} damage to its target and chain to 2 more, halving each jump";
                    return $"deal {value} damage to {target}";
                case ActionType.Heal:
                    return $"heal {target} for {value}";
                case ActionType.Draw:
                    return action.targetType == TargetType.Enemy ? $"the enemy draws {Cards(value)}" : $"draw {Cards(value)}";
                case ActionType.Summon:
                    return $"{Keywords.Link("Summon", "summon")} {(value == 1 ? "a creature" : $"{value} creatures")}";
                case ActionType.Armor:
                    return $"give {target} +{value} {Keywords.Link("Armor")}";
                case ActionType.Stun:
                    return $"{Keywords.Link("Exhausted", "exhaust")} {target}{(value > 1 ? $" for {Days(value)}" : "")}";
                case ActionType.ModifyStat:
                    return $"give {target} {DescribeStatChange(action)}";
                case ActionType.ApplyStatus:
                    return DescribeStatus(action, target);
                case ActionType.DelayDeployment:
                    return $"add {Days(value)} to {target}'s {Keywords.Link("Deployment Time")}";
                case ActionType.PlaceSlotEffect:
                    return $"place {DescribeSlotEffect(action)} on {SlotTarget(action, isSpell)}";
                case ActionType.CleanseSlot:
                    return $"remove the {Keywords.Link("Slot Effect")} from {SlotTarget(action, isSpell)}";
                case ActionType.CreateWatchdog:
                    return DescribeWatchdog(action, target);
                case ActionType.Reveal: // Instant: it resolves as soon as it's played
                    if (action.targetType is TargetType.Player or TargetType.Enemy) return $"{Keywords.Link("Reveal", "reveal")} {target}'s {Keywords.Link("Watchdog")} conditions";
                    return action.targetModifier.HasFlag(TargetModifier.Random)
                        ? $"{Keywords.Link("Reveal", "reveal")} the slot of {target}"
                        : $"{Keywords.Link("Reveal", "reveal")} {SlotTarget(action, isSpell)}";
                case ActionType.Cleanse: // A Cleanse removes the bad; a Purge removes everything
                    string removed = action.cleanse switch {
                        CleanseTarget.Statuses => "statuses",
                        CleanseTarget.StatModifications => "temporary stat changes",
                        _ => "statuses and temporary stat changes"
                    };
                    return action.purge ? $"{Keywords.Link("Purge", "purge")} {target}'s {removed}" : $"{Keywords.Link("Cleanse", "cleanse")} {target}'s harmful {removed}";
                default:
                    return null;
            }
        }

        private static string DescribeTarget(EffectAction action, bool isSpell) {
            if (action.targetModifier.HasFlag(TargetModifier.Random)) {
                switch (action.targetType) {
                    case TargetType.EnemyCreatures: return "a random enemy creature";
                    case TargetType.FriendlyCreatures: return "a random friendly creature";
                    case TargetType.AllCreatures: return "a random creature";
                }
            }

            string adjacent = action.targetModifier.HasFlag(TargetModifier.WithAdjacent) ? " and its neighbors" : "";
            // Spells resolve against whatever they are dropped on unless they hit a whole group
            bool hitsGroup = action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets);
            if (isSpell && !hitsGroup) {
                return (action.actionType is ActionType.Damage or ActionType.Heal ? "any target" : "target creature") + adjacent;
            }

            switch (action.targetType) {
                case TargetType.Self: return "this" + adjacent;
                case TargetType.Player: return "you";
                case TargetType.Enemy: return "the enemy player";
                case TargetType.FriendlyCreatures: return "each friendly creature";
                case TargetType.EnemyCreatures: return "each enemy creature";
                case TargetType.AllCreatures: return "each creature";
                default: return "target";
            }
        }

        // "+1 attack for this Day"; a Health decrease is Health Loss, so it's a link to that rule
        private static string DescribeStatChange(EffectAction action) {
            string sign = action.value >= 0 ? "+" : "";
            var stats = new List<string>();
            if (action.modifyAttack) stats.Add("attack");
            if (action.modifyHealth) stats.Add("health");
            if (action.modifySpeed) stats.Add("speed");
            string days = action.statusDuration switch {
                <= 0 => "",
                1 => " for this Day",
                _ => $" for {action.statusDuration} Days"
            };
            if (stats.Count == 0) return "no change";
            string change = $"{sign}{action.value} {JoinClauses(stats)}";
            if (action.value < 0 && action.modifyHealth) change = Keywords.Link("Health Loss", change);
            return change + days;
        }

        private static string DescribeStatus(EffectAction action, string target) {
            string days = Days(action.statusDuration);
            switch (action.statusEffectToApply) {
                case StatusEffectType.Exhausted: return $"{Keywords.Link("Exhausted", "exhaust")} {target}";
                case StatusEffectType.Burned: return $"{Keywords.Link("Burned", "burn")} {target} ({action.statusPotency} damage each {Keywords.Link("Dusk")}, {days})";
                case StatusEffectType.Doomed: return $"{Keywords.Link("Doomed", "doom")} {target} for {days}";
                case StatusEffectType.Blessed: return $"{Keywords.Link("Blessed", "bless")} {target}{(action.statusPotency > 1 ? $" {action.statusPotency} times" : "")}";
                case StatusEffectType.HeatSignature: return $"{target} gains {System.Math.Max(1, action.statusPotency)} {Keywords.Link("Heat")}";
                case StatusEffectType.Concealed: return $"{Keywords.Link("Concealed", "conceal")} {target} for {days}";
                case StatusEffectType.Revealed: return $"make {target} {Keywords.Link("Revealed")} for {days}";
                case StatusEffectType.Encrypted: return $"{Keywords.Link("Encrypted", "encrypt")} {target} for {days}";
                default: return null;
            }
        }

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
            string timing = spec.timing == WatchdogTiming.Briefing ? Keywords.Link("Briefing") : Keywords.Link("On Watch");
            string days = action.statusDuration > 0 ? $", {Days(action.statusDuration)}" : "";
            return $"{Keywords.Link("Watchdog")}: {pick}{whose}{what}{atMost}{by} -> {effect}, {charges} charge{(charges == 1 ? "" : "s")} ({timing}{days})";
        }

        // Slot Effects aim at the slot, whoever stands in it
        private static string SlotTarget(EffectAction action, bool isSpell) =>
            isSpell || action.targetType != TargetType.Self ? "target slot" : "this creature's slot";

        private static string DescribeSlotEffect(EffectAction action) {
            string days = action.statusDuration > 0 ? $" for {Days(action.statusDuration)}" : "";
            return action.slotEffect switch {
                SlotEffectType.Landmine => $"a hidden {Keywords.Link("Landmine")} ({action.value} damage)",
                SlotEffectType.BarbedSnare => $"a hidden {Keywords.Link("Barbed Snare")} ({action.value} damage)",
                SlotEffectType.TemporalShield => $"a {Keywords.Link("Temporal Shield")}{days}",
                SlotEffectType.ScorchedEarth => $"{Keywords.Link("Scorched Earth")}{days}",
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

        private static string Cards(int count) => count == 1 ? "a card" : $"{count} cards";
        private static string Days(int count) => count == 1 ? "1 Day" : $"{count} Days";

        // Upper-cases the first letter that is read, skipping rich-text tags and icons in front of it
        private static string CapitalizeRich(string text) {
            bool inTag = false;
            for (int i = 0; i < (text?.Length ?? 0); i++) {
                char c = text[i];
                if (c == '<') inTag = true;
                else if (c == '>') inTag = false;
                else if (!inTag && char.IsLetter(c)) return text.Substring(0, i) + char.ToUpper(c) + text.Substring(i + 1);
            }
            return text;
        }

        private static string LowerFirst(string text) => text.StartsWith("<") ? text : char.ToLower(text[0]) + text.Substring(1);
    }
}
