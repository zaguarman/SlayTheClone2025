using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DamageCreatureAction : IGameAction {
        private readonly ICreature target;
        private readonly int damage;
        private readonly ICreature attacker; // Creature dealing the damage, if any
        private readonly bool isCombatDamage; // True when dealt by a creature's attack (not by an effect)

        public ICreature GetTarget() => target;
        public ICreature GetAttacker() => attacker;
        public int GetDamage() => damage;
        public bool IsCombatDamage() => isCombatDamage;

        public DamageCreatureAction(ICreature target, int damage, ICreature attacker = null, bool isCombatDamage = false) {
            this.target = target;
            this.damage = damage;
            this.attacker = attacker;
            this.isCombatDamage = isCombatDamage;
            Log($"Created {this}", LogTag.Actions | LogTag.Creatures);
        }

        public override string ToString() {
            return $"DamageCreatureAction: Target={target?.Name}({target?.TargetId.ShortId()}), Dmg={damage}, Attacker={attacker?.Name ?? "Source"}";
        }
    }
}
