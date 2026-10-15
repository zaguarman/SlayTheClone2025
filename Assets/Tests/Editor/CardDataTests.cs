using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Every card asset in the project describes effects the rules can carry out: an ability aimed at something it can't
    /// affect (a paralyze aimed at a player, say) silently does nothing in play. And every keyword it prints is explained.
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

            // Every keyword printed on a card, or mentioned in another keyword's explanation, has an explanation
            // ...and the text says each thing once: the same effect on groups that make up a bigger one reads as that group
            var deadLinks = new List<string>();
            var printed = new Dictionary<string, string>();
            foreach (var asset in assets) {
                var data = asset.ToCardData();
                try {
                    printed[data.cardName] = Keywords.Plain(CardTextFormatter.GetRulesText(data));
                    deadLinks.AddRange(Keywords.LinksIn(CardTextFormatter.GetRulesText(data)).Where(k => !Keywords.Exists(k)).Select(k => $"{data.cardName}: {k}"));
                }
                finally {
                    Object.DestroyImmediate(data);
                }
            }
            foreach (var entry in Keywords.All) {
                var mentioned = System.Text.RegularExpressions.Regex.Matches(entry.Text, @"\[([^\]|]+)").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value);
                deadLinks.AddRange(mentioned.Where(k => !Keywords.Exists(k)).Select(k => $"{entry.Key}: {k}"));
            }
            CollectionAssert.IsEmpty(deadLinks, "Keywords without an explanation");

            // ...and the font draws every character of it, and every faction's coin: its fallback is baked (static), so it
            // doesn't rewrite itself at each run. A new sign goes into "LiberationSans SDF - Fallback" (TryAddCharacters)
            var shown = string.Concat(printed.Keys.Concat(printed.Values).Concat(Keywords.All.Select(k => k.Title + Keywords.Plain(k.Text)))
                .Concat(System.Enum.GetValues(typeof(Faction)).Cast<Faction>().Select(UITheme.FactionGlyph)));
            TMPro.TMP_Settings.defaultFontAsset.HasCharacters(shown, out uint[] undrawable, searchFallbacks: true);
            CollectionAssert.IsEmpty((undrawable ?? new uint[0]).Distinct().Where(c => !char.IsControl((char)c)).Select(c => (char)c), "Characters the font can't draw");
            Assert.AreEqual("Exhaust all creatures", printed["Strike"], "All enemy and all friendly creatures: all creatures");
            Assert.AreEqual("Target +3 Attack and -2 Health", printed["Dead Cat Bounce"], "One target, named once");
            StringAssert.EndsWith("and Heat Signature on all creatures", printed["Climate Change"], "Statuses on the same creatures share them, and a 1 goes without saying");
            StringAssert.StartsWith("Swap\n", printed["Quantum Physicist"], "No full stop: each line is one rule");
            StringAssert.EndsWith("Retaliate: Exhaust", printed["Quantum Physicist"], "Retaliate, then what it does to the attacker");
            Assert.AreEqual("Arrival: exhaust itself", printed["Hikikomori"], "Aimed at itself, it says so");
            StringAssert.Contains("Counterattack", printed["Keyboard Warrior"], "Hitting back for its Attack");

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
            // Counterattack and Retaliate: an enemy-targeted OnDamage effect lands on the creature whose attack hit this one
            bool retaliates = trigger == EffectTrigger.OnDamage && action.targetType == TargetType.Enemy;
            switch (action.actionType) {
                case ActionType.Armor:
                case ActionType.ModifyStat:
                case ActionType.Stun:
                case ActionType.ApplyStatus:
                case ActionType.Cleanse:
                case ActionType.CopyStats:
                case ActionType.StealStatChanges:
                case ActionType.SwapPositions:
                case ActionType.GrantAbility:
                case ActionType.Destroy:
                case ActionType.TakeControl or ActionType.Attack or ActionType.Funnel or ActionType.PassHarmfulStatuses
                    or ActionType.StealGoodStatuses or ActionType.ReturnToHand or ActionType.FakeDeath or ActionType.Vanish or ActionType.Revive
                    or ActionType.Resurrect:
                    if (!CreatureTargets.Contains(action.targetType) && !retaliates) return $"{action.actionType} only affects creatures but targets {action.targetType}";
                    break;
                case ActionType.Draw:
                case ActionType.Summon:
                case ActionType.SummonDecoys:
                case ActionType.WipeWatchdogs:
                case ActionType.CutDraws:
                case ActionType.SendFor:
                case ActionType.Grace:
                    if (!PlayerTargets.Contains(action.targetType)) return $"{action.actionType} only affects players but targets {action.targetType}";
                    break;
            }

            if (action.actionType == ActionType.ApplyStatus) {
                if (action.statusEffectToApply == StatusEffectType.None) return "applies no status";
                bool persistent = action.statusEffectToApply == StatusEffectType.Blessed; // Counts stacks, not Days
                if (!persistent && action.statusDuration <= 0) return $"applies {action.statusEffectToApply} for {action.statusDuration} turns";
            }
            else if (action.actionType == ActionType.SetTrend) {
                if (action.trend == Trend.None) return "sets no Trend";
            }
            else if (action.actionType == ActionType.Earthquake) {
                if (action.value <= 0) return "an Earthquake that deals no damage";
            }
            else if (action.actionType == ActionType.GrantAbility) {
                if (action.ability == CreatureAbility.None) return "grants no ability";
            }
            else if (action.actionType is ActionType.CopyStats or ActionType.StealStatChanges or ActionType.SwapPositions or ActionType.Destroy
                     or ActionType.TakeControl or ActionType.Attack or ActionType.Funnel or ActionType.PassHarmfulStatuses
                     or ActionType.StealGoodStatuses or ActionType.ReturnToHand or ActionType.FakeDeath or ActionType.Vanish or ActionType.Revive) {
                return null; // Says what it does instead of a value
            }
            else if (action.actionType is ActionType.Cleanse or ActionType.CleanseSlot or ActionType.CreateWatchdog or ActionType.Reveal or ActionType.WipeWatchdogs
                     or ActionType.SendFor) {
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
                // Attack add-ons without a value use the attacker's attack; Counterattack, the creature's own
                bool usesAttack = action.actionType == ActionType.Damage && (action.targetModifier != TargetModifier.None || retaliates);
                if (!usesAttack) return $"{action.actionType} has no value ({action.value})";
            }
            return null;
        }
    }
}
