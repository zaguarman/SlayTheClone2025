using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    public class BattlefieldCombatAction : IGameAction {
        private readonly ICreature attacker;
        private readonly BoardSlot targetSlot;

        public ICreature GetAttacker() => attacker;
        /// <summary>
        /// Where it hits: the slot it was aimed at, or for a Homing attacker the slot its mark stands in now (the slot it
        /// was last in, once it's gone).
        /// </summary>
        public BoardSlot GetTargetSlot() => Mark != null ? Mark.Slot ?? Mark.LastSlot ?? targetSlot : targetSlot;
        /// <summary>The slot the attack was aimed at, whoever stands there now.</summary>
        public BoardSlot AimedSlot => targetSlot;
        /// <summary>A Homing attacker's mark: the enemy creature in the slot it aimed at, followed wherever it goes.</summary>
        public ICreature Mark { get; }
        /// <summary>Doomed's final attack: it resolves even if the attacker Cannot Act.</summary>
        public bool IsLastRites { get; }

        public BattlefieldCombatAction(ICreature attacker, BoardSlot targetSlot, bool isLastRites = false) {
            this.attacker = attacker;
            this.targetSlot = targetSlot;
            IsLastRites = isLastRites;
            var there = targetSlot?.OccupyingCreature;
            if (EffectResolver.IsHoming(attacker) && there != null && there.Owner != attacker?.Owner) Mark = there;
            Log($"Created {this}", LogTag.Actions | LogTag.Combat);
        }

        public override string ToString() {
            return $"BattlefieldCombatAction: Attacker={attacker?.Name ?? "Unknown"} (AttackerID: {attacker?.TargetId.ToUpper() ?? "UNKNOWN"}), " +
                   $"TargetSlot=(SlotID: {targetSlot?.TargetId.ToUpper() ?? "UNKNOWN"}) (ActionID: {GetHashCode()})";
        }
    }
}
