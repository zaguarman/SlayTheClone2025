using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Every card asset in the project describes effects the rules can carry out: an ability aimed at something it can't
    /// affect (a paralyze aimed at a player, say) silently does nothing in play.
    /// </summary>
    public class CardDataTests {
        private const string CardsFolder = "Assets/Scriptables/Cards";

        [Test]
        public void EveryCardAsset_HasAUniqueIdAndAName_EffectsThatCanWork_AndEveryAbilityIsOnACard() {
            var assets = LoadCardAssets();
            Assert.IsNotEmpty(assets, $"No card assets found in {CardsFolder}");

            foreach (var asset in assets) {
                Assert.IsFalse(string.IsNullOrEmpty(asset.cardName), $"{asset.name} has no card name");
                Assert.IsFalse(string.IsNullOrEmpty(asset.cardId), $"{asset.cardName} has no id");
            }
            var sharedIds = assets.GroupBy(a => a.cardId).Where(g => g.Count() > 1).Select(g => string.Join(", ", g.Select(a => a.cardName)));
            CollectionAssert.IsEmpty(sharedIds, "Cards sharing an id");

            var problems = new List<string>();
            foreach (var asset in assets) {
                var data = asset.ToCardData();
                try {
                    foreach (var (effect, action) in data.effects.SelectMany(e => e.actions.Select(a => (e, a)))) {
                        string problem = FindProblem(action, effect.trigger);
                        if (problem != null) problems.Add($"{data.cardName}: {problem}");
                    }
                }
                finally {
                    Object.DestroyImmediate(data);
                }
            }

            Assert.IsEmpty(problems, "Card effects that can't work:\n" + string.Join("\n", problems));

            var carried = assets.OfType<CreatureCardScriptableObject>().Select(c => c.ability).ToHashSet();
            var missing = System.Enum.GetValues(typeof(CreatureAbility)).Cast<CreatureAbility>()
                .Where(a => a != CreatureAbility.None && !carried.Contains(a));
            CollectionAssert.IsEmpty(missing, "Abilities no card has ('Abilities Compendium')");
        }

        private static List<CardDataScriptableObject> LoadCardAssets() {
            return AssetDatabase.FindAssets("t:ScriptableObject", new[] { CardsFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<CardDataScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .ToList();
        }

        private static readonly TargetType[] CreatureTargets = { TargetType.Self, TargetType.AllCreatures, TargetType.FriendlyCreatures, TargetType.EnemyCreatures };
        private static readonly TargetType[] PlayerTargets = { TargetType.Player, TargetType.Enemy };

        // Null when the action can work
        private static string FindProblem(EffectAction action, EffectTrigger trigger) {
            // Retaliation: an enemy-targeted OnDamage effect lands on whoever damaged the creature
            bool retaliates = trigger == EffectTrigger.OnDamage && action.targetType == TargetType.Enemy;
            switch (action.actionType) {
                case ActionType.Armor:
                case ActionType.ModifyStat:
                case ActionType.Stun:
                case ActionType.ApplyStatus:
                case ActionType.Cleanse:
                    if (!CreatureTargets.Contains(action.targetType) && !retaliates) return $"{action.actionType} only affects creatures but targets {action.targetType}";
                    break;
                case ActionType.Draw:
                case ActionType.Summon:
                    if (!PlayerTargets.Contains(action.targetType)) return $"{action.actionType} only affects players but targets {action.targetType}";
                    break;
            }

            if (action.actionType == ActionType.ApplyStatus) {
                if (action.statusEffectToApply == StatusEffectType.None) return "applies no status";
                bool persistent = action.statusEffectToApply == StatusEffectType.Blessed; // Counts stacks, not Days
                if (!persistent && action.statusDuration <= 0) return $"applies {action.statusEffectToApply} for {action.statusDuration} turns";
            }
            else if (action.actionType is ActionType.Cleanse or ActionType.CleanseSlot or ActionType.CreateWatchdog or ActionType.Reveal) {
                return null; // Says what it removes instead of a value
            }
            else if (action.actionType == ActionType.PlaceSlotEffect) {
                if (action.slotEffect == SlotEffectType.None) return "places no Slot Effect";
                bool deals = action.slotEffect is SlotEffectType.Landmine or SlotEffectType.BarbedSnare;
                if (deals && action.value <= 0) return $"{action.slotEffect} deals no damage";
            }
            else if (action.actionType == ActionType.ModifyStat) {
                if (action.value == 0) return "changes stats by 0"; // Negative values are debuffs
            }
            else if (action.value <= 0) {
                // Attack add-ons without a value use the attacker's attack
                bool usesAttack = action.actionType == ActionType.Damage && action.targetModifier != TargetModifier.None;
                if (!usesAttack) return $"{action.actionType} has no value ({action.value})";
            }
            return null;
        }
    }
}
