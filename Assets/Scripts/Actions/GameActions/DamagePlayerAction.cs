using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DamagePlayerAction : IGameAction, ISourcedAction {
        /// <summary>The card whose effect deals it, for the stats.</summary>
        public ICard Source { get; set; }
        private readonly IPlayer target;
        private readonly int damage;
        private readonly ICreature attacker; // Creature dealing the damage, if any
        private readonly bool isCombatDamage; // True when dealt by a creature's attack (not by an effect)
        private readonly bool dealsAttackersAttack; // The amount is the attacker's attack when the hit lands

        public IPlayer GetTargetPlayer() => target;
        // A hit that deals the attacker's attack follows buffs and debuffs that land before it does
        public int GetDamage() => dealsAttackersAttack && attacker != null ? attacker.Attack + Abilities.AttackBonus(attacker) : damage;
        public ICreature GetAttacker() => attacker;
        public bool IsCombatDamage() => isCombatDamage;

        public DamagePlayerAction(IPlayer target, int damage, ICreature attacker = null, bool isCombatDamage = false)
            : this(target, damage, attacker, isCombatDamage, dealsAttackersAttack: false) { }

        /// <summary>A hit for the attacker's attack, read when the hit lands rather than when it is queued.</summary>
        public DamagePlayerAction(IPlayer target, ICreature attacker, bool isCombatDamage)
            : this(target, attacker?.Attack ?? 0, attacker, isCombatDamage, dealsAttackersAttack: attacker != null) { }

        private DamagePlayerAction(IPlayer target, int damage, ICreature attacker, bool isCombatDamage, bool dealsAttackersAttack) {
            this.target = target;
            this.damage = damage;
            this.attacker = attacker;
            this.isCombatDamage = isCombatDamage;
            this.dealsAttackersAttack = dealsAttackersAttack;
            Log($"Created {this}", LogTag.Actions | LogTag.Players);
        }

        public override string ToString() {
            string amount = dealsAttackersAttack ? $"{attacker.Name}'s attack" : damage.ToString();
            return $"DamagePlayerAction: Target={target?.Name} (TargetID: {target?.TargetId.ToUpper()}), Damage={amount}";
        }
    }
}
