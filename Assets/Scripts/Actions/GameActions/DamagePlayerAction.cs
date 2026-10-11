using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DamagePlayerAction : IGameAction {
        private readonly IPlayer target;
        private readonly int damage;
        private readonly ICreature attacker; // Creature dealing the damage, if any
        private readonly bool isCombatDamage; // True when dealt by a creature's attack (not by an effect)

        public IPlayer GetTargetPlayer() => target;
        public int GetDamage() => damage;
        public ICreature GetAttacker() => attacker;
        public bool IsCombatDamage() => isCombatDamage;

        public DamagePlayerAction(IPlayer target, int damage, ICreature attacker = null, bool isCombatDamage = false) {
            this.target = target;
            this.damage = damage;
            this.attacker = attacker;
            this.isCombatDamage = isCombatDamage;
            Log($"Created {this}", LogTag.Actions | LogTag.Players);
        }

        public override string ToString() {
            return $"DamagePlayerAction: Target={target?.Name} (TargetID: {target?.TargetId.ToUpper()}), Damage={damage}";
        }
    }
}
