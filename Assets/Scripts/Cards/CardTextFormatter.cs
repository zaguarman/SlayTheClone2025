using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Builds the printed text of a card from its data, in the style of Magic: The Gathering:
    /// a type line and rules text written as short templated sentences, each trigger and effect led by its icon
    /// ("[skull] When this dies, [lightning] paralyze each enemy creature for 1 turn.").
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
            string keywords = KeywordLine(creature != null ? creature.movement : MovementKeyword.None);
            string ability = creature != null ? AbilityLine(creature.ability) : "";
            string effects = GetRulesText(data.effects, data is SpellData);
            return string.Join("\n", new[] { keywords, ability, effects }.Where(line => !string.IsNullOrEmpty(line)));
        }

        /// <summary>A creature's ability, with its reminder text: "<b>Contrarian</b> (stat changes on it are reversed)."</summary>
        public static string AbilityLine(CreatureAbility ability) =>
            ability == CreatureAbility.None ? "" : $"<b>{Abilities.NameOf(ability)}</b> ({Abilities.ReminderOf(ability)}).";

        // Movement Keywords lead the text, like Magic's evergreen keywords ("Leapfrog.")
        private static string KeywordLine(MovementKeyword movement) {
            var words = new List<string>();
            if (movement.HasFlag(MovementKeyword.Leapfrog)) words.Add("<b>Leapfrog</b>");
            if (movement.HasFlag(MovementKeyword.Swap)) words.Add("<b>Swap</b>");
            return words.Count == 0 ? "" : string.Join(", ", words) + ".";
        }

        public static string GetRulesText(IEnumerable<CardEffect> effects, bool isSpell) {
            if (effects == null) return "";

            var sentences = new List<string>();
            foreach (var effect in effects) {
                var actions = effect.actions.Where(a => DescribeAction(a, isSpell) != null).ToList();
                if (actions.Count == 0) continue;

                var trigger = isSpell ? default : DescribeTrigger(effect);
                bool startsSentence = trigger.text == null;
                var clauses = actions.Select((a, i) => {
                    string text = DescribeAction(a, isSpell);
                    return EffectIcons.Tag(ActionIcon(a)) + (i == 0 && startsSentence ? Capitalize(text) : text);
                }).ToList();

                string body = JoinClauses(clauses);
                string once = effect.oncePerCombat ? "Once per combat, " : "";
                string triggerText = once.Length > 0 && trigger.text != null ? char.ToLower(trigger.text[0]) + trigger.text.Substring(1) : trigger.text;
                sentences.Add(startsSentence ? once + body + "." : $"{EffectIcons.Tag(trigger.icon)}{once}{triggerText}, {body}.");
            }
            return string.Join("\n", sentences);
        }

        private static (string icon, string text) DescribeTrigger(CardEffect effect) {
            switch (effect.trigger) {
                case EffectTrigger.OnPlay: return (EffectIcons.Enter, "When this enters");
                case EffectTrigger.OnDeath: return (EffectIcons.Death, "When this dies");
                case EffectTrigger.OnAboutToDie: return (EffectIcons.Death, "When this would die");
                case EffectTrigger.OnDamage:
                    // Chain/spread effects fire when this creature deals combat damage; the rest when it is damaged
                    return effect.actions.Any(a => a.targetModifier != TargetModifier.None)
                        ? (EffectIcons.Attack, "Whenever this attacks")
                        : (EffectIcons.Hurt, "Whenever this is damaged");
                case EffectTrigger.StartOfTurn: return (EffectIcons.Dawn, "At the start of each turn");
                case EffectTrigger.EndOfTurn: return (EffectIcons.Dusk, "At the end of each turn");
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
                default: return null;
            }
        }

        private static string DescribeAction(EffectAction action, bool isSpell) {
            string target = DescribeTarget(action, isSpell);
            int value = action.value;

            switch (action.actionType) {
                case ActionType.Damage:
                    if (action.targetModifier.HasFlag(TargetModifier.Chained))
                        return $"deal {value} damage to its target and chain to 2 more, halving each jump";
                    return $"deal {value} damage to {target}";
                case ActionType.Heal:
                    return $"heal {target} for {value}";
                case ActionType.Draw:
                    return action.targetType == TargetType.Enemy
                        ? $"the enemy draws {Cards(value)}"
                        : $"draw {Cards(value)}";
                case ActionType.Summon:
                    return $"summon {(value == 1 ? "a random creature" : $"{value} random creatures")} from your deck";
                case ActionType.Armor:
                    return $"give {target} +{value} armor";
                case ActionType.Stun:
                    return $"stun {target} for {Turns(value)}";
                case ActionType.ModifyStat:
                    return $"give {target} {DescribeStatChange(action)}";
                case ActionType.ApplyStatus:
                    return DescribeStatus(action, target);
                case ActionType.DelayDeployment:
                    return $"add {Turns(value).Replace("turn", "Day")} to {target}'s Deployment Time";
                case ActionType.PlaceSlotEffect:
                    return $"place {DescribeSlotEffect(action)} on {SlotTarget(action, isSpell)}";
                case ActionType.CleanseSlot:
                    return $"remove the Slot Effect from {SlotTarget(action, isSpell)}";
                case ActionType.CreateWatchdog:
                    return DescribeWatchdog(action, target);
                case ActionType.Cleanse: // A Cleanse removes the bad; a Purge removes everything
                    string removed = action.cleanse switch {
                        CleanseTarget.Statuses => "statuses",
                        CleanseTarget.StatModifications => "temporary stat changes",
                        _ => "statuses and temporary stat changes"
                    };
                    return action.purge ? $"purge {target}'s {removed}, good and bad" : $"cleanse {target}'s harmful {removed}";
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

            // Spells resolve against whatever they are dropped on unless they hit a whole group
            bool hitsGroup = action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets);
            if (isSpell && !hitsGroup) {
                return action.actionType is ActionType.Damage or ActionType.Heal ? "any target" : "target creature";
            }

            string adjacent = action.targetModifier.HasFlag(TargetModifier.WithAdjacent) ? " and its neighbors" : "";
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
            return stats.Count == 0 ? "no change" : $"{sign}{action.value} {JoinClauses(stats)}{days}";
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
                WatchdogEffect.Nullify => "Nullify", WatchdogEffect.Redirect => "Redirect", WatchdogEffect.Reveal => "Reveal",
                _ => spec.moveEarlier ? "move it first" : "move it last"
            };
            int charges = System.Math.Max(1, spec.charges);
            string pick = spec.timing == WatchdogTiming.Briefing && spec.pick != BriefingPick.First ? $"{spec.pick.ToString().ToLower()} " : "";
            string timing = spec.timing == WatchdogTiming.Briefing ? "Briefing" : "On Watch";
            string days = action.statusDuration > 0 ? $", {Turns(action.statusDuration).Replace("turn", "Day")}" : "";
            return $"<b>Watchdog</b>: {pick}{whose}{what}{atMost}{by} -> {effect}, {charges} charge{(charges == 1 ? "" : "s")} ({timing}{days})";
        }

        // Slot Effects aim at the slot, whoever stands in it
        private static string SlotTarget(EffectAction action, bool isSpell) =>
            isSpell || action.targetType != TargetType.Self ? "target slot" : "this creature's slot";

        private static string DescribeSlotEffect(EffectAction action) {
            string days = action.statusDuration > 0 ? $" for {Turns(action.statusDuration).Replace("turn", "Day")}" : "";
            return action.slotEffect switch {
                SlotEffectType.Landmine => $"a hidden Landmine ({action.value} damage to the next creature to enter)",
                SlotEffectType.BarbedSnare => $"a hidden Barbed Snare ({action.value} damage to the next creature to leave)",
                SlotEffectType.TemporalShield => $"a Temporal Shield (no combat damage to the creature there){days}",
                SlotEffectType.ScorchedEarth => $"Scorched Earth (the creature there can't trigger its effects){days}",
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

        private static string DescribeStatus(EffectAction action, string target) {
            string turns = Turns(action.statusDuration);
            switch (action.statusEffectToApply) {
                case StatusEffectType.Exhausted: return $"exhaust (stun) {target} for {turns}";
                case StatusEffectType.Burned: return $"burn {target} for {action.statusPotency} each turn for {turns}";
                case StatusEffectType.Doomed: return $"doom {target} for {turns}";
                case StatusEffectType.Blessed: return $"bless {target}{(action.statusPotency > 1 ? $" {action.statusPotency} times" : "")}";
                case StatusEffectType.HeatSignature: return $"{target} gains {System.Math.Max(1, action.statusPotency)} Heat";
                default: return null;
            }
        }

        private static string JoinClauses(IList<string> clauses) {
            if (clauses.Count == 1) return clauses[0];
            return string.Join(", ", clauses.Take(clauses.Count - 1)) + " and " + clauses[clauses.Count - 1];
        }

        private static string Cards(int count) => count == 1 ? "a card" : $"{count} cards";
        private static string Turns(int count) => count == 1 ? "1 turn" : $"{count} turns";
        private static string Capitalize(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);
    }
}
