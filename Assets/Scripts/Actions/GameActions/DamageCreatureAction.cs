using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DamageCreatureAction : IGameAction, ISourcedAction {
        /// <summary>The card whose effect deals it (a spell, a creature's effect, a status's or a trap's card), for the stats.</summary>
        public ICard Source { get; set; }
        private readonly ICreature target;
        private readonly int damage;
        private readonly ICreature attacker; // Creature dealing the damage, if any
        private readonly bool isCombatDamage; // True when dealt by a creature's attack (not by an effect)
        private readonly bool dealsAttackersAttack; // The amount is the attacker's attack when the hit lands

        public ICreature GetTarget() => target;
        public ICreature GetAttacker() => attacker;
        // A hit that deals the attacker's attack follows buffs and debuffs that land before it does
        public int GetDamage() => dealsAttackersAttack && attacker != null ? attacker.Attack + Abilities.AttackBonus(attacker) : damage;
        /// <summary>Whether it's the attacker's hit, dealing its Attack when it lands (Underground looks past the attacker's buffs).</summary>
        public bool DealsAttackersAttack => dealsAttackersAttack && attacker != null;
        public bool IsCombatDamage() => isCombatDamage;

        public DamageCreatureAction(ICreature target, int damage, ICreature attacker = null, bool isCombatDamage = false)
            : this(target, damage, attacker, isCombatDamage, dealsAttackersAttack: false) { }

        /// <summary>A hit for the attacker's attack, read when the hit lands rather than when it is queued.</summary>
        public DamageCreatureAction(ICreature target, ICreature attacker, bool isCombatDamage)
            : this(target, attacker?.Attack ?? 0, attacker, isCombatDamage, dealsAttackersAttack: attacker != null) { }

        private DamageCreatureAction(ICreature target, int damage, ICreature attacker, bool isCombatDamage, bool dealsAttackersAttack) {
            this.target = target;
            this.damage = damage;
            this.attacker = attacker;
            this.isCombatDamage = isCombatDamage;
            this.dealsAttackersAttack = dealsAttackersAttack;
            Log($"Created {this}", LogTag.Actions | LogTag.Creatures);
        }

        public override string ToString() {
            string amount = dealsAttackersAttack ? $"{attacker.Name}'s attack" : damage.ToString();
            return $"DamageCreatureAction: Target={target?.Name}({target?.TargetId.ShortId()}), Dmg={amount}, Attacker={attacker?.Name ?? "Source"}";
        }
    }
}
