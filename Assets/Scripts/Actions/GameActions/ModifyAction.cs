using System;
using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class ModifyAction : IGameAction {
        private readonly ICreature targetCreature;
        private readonly int value;
        private readonly bool modifyAttack;
        private readonly bool modifyHealth;
        private readonly bool modifySpeed;
        private readonly ModifierCalculationType calculationType;
        private readonly int durationInTurns; // 0 = permanent

        public ITarget GetTarget() => targetCreature;
        public int GetValue() => value;
        public bool ShouldModifyAttack() => modifyAttack;
        public bool ShouldModifyHealth() => modifyHealth;
        public bool ShouldModifySpeed() => modifySpeed;
        public int GetDuration() => durationInTurns;
        public ModifierCalculationType GetCalculationType() => calculationType;
        /// <summary>The creature whose effect it is, if any (where No U bounces it, and whether Zero-Day bypasses abilities).</summary>
        public ICreature Source { get; }
        /// <summary>Whose effect it is: an enemy's effect is the one Unbothered, No U and Spite react to. Null: nobody's.</summary>
        public IPlayer Controller { get; }

        public ModifyAction(
            ICreature target,
            int value,
            bool modifyAttack,
            bool modifyHealth,
            bool modifySpeed,
            int durationInTurns = 0,
            ModifierCalculationType calculationType = ModifierCalculationType.Flat,
            ICreature source = null,
            IPlayer controller = null) {
            if (target == null) {
                // The action is still created; its executor skips it
                LogWarning("ModifyAction: Target creature is null. Action cannot be created.", LogTag.Actions | LogTag.Effects);
            }

            targetCreature = target;
            this.value = value; // Negative values are debuffs
            this.modifyAttack = modifyAttack;
            this.modifyHealth = modifyHealth;
            this.modifySpeed = modifySpeed;
            this.calculationType = calculationType;
            this.durationInTurns = Math.Max(0, durationInTurns);
            Source = source;
            Controller = controller;

            Log($"Created {this}", LogTag.Actions | LogTag.Creatures | LogTag.Effects);
        }

        private string DescribeModification() {
            string sign = (calculationType == ModifierCalculationType.Flat && value >= 0) ? "+" : "";
            string suffix = (calculationType == ModifierCalculationType.Percentage) ? "%" : "";

            var stats = new List<string>();
            if (modifyAttack) stats.Add("Attack");
            if (modifyHealth) stats.Add("Health");
            if (modifySpeed) stats.Add("Speed");

            if (stats.Count == 0) return "No Stat Buff";
            // Join multiple stats with commas and "&" for the last one
            string statsText = stats.Count == 1
                ? stats[0]
                : $"{string.Join(", ", stats.Take(stats.Count - 1))} & {stats[stats.Count - 1]}";
            return $"{sign}{value}{suffix} {statsText}";
        }

        public override string ToString() {
            string durationText = durationInTurns > 0 ? $" for {durationInTurns} turns" : " (permanent)";
            string targetName = targetCreature?.Name ?? "NULL";
            string targetId = targetCreature?.TargetId.ShortId() ?? "UNKNOWN";
            return $"ModifyAction: Target={targetName}({targetId}), Buff={DescribeModification()}{durationText}, Calc={calculationType}";
        }
    }
}
