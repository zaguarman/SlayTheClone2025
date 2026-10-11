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

        public static string GetTypeLine(CardData data) => data is SpellData ? "Spell" : "Creature";

        public static string GetRulesText(CardData data) => GetRulesText(data.effects, data is SpellData);

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
                sentences.Add(startsSentence ? body + "." : $"{EffectIcons.Tag(trigger.icon)}{trigger.text}, {body}.");
            }
            return string.Join("\n", sentences);
        }

        private static (string icon, string text) DescribeTrigger(CardEffect effect) {
            switch (effect.trigger) {
                case EffectTrigger.OnPlay: return (EffectIcons.Enter, "When this enters");
                case EffectTrigger.OnDeath: return (EffectIcons.Death, "When this dies");
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
            return stats.Count == 0 ? "no change" : $"{sign}{action.value} {JoinClauses(stats)}";
        }

        private static string DescribeStatus(EffectAction action, string target) {
            string turns = Turns(action.statusDuration);
            switch (action.statusEffectToApply) {
                case StatusEffectType.Paralyzed: return $"paralyze {target} for {turns}";
                case StatusEffectType.Burned: return $"burn {target} for {action.statusPotency} each turn for {turns}";
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
