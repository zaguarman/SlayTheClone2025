namespace Zeitgeist {
    /// <summary>
    /// Builds the damage or heal action that matches a target's kind: a creature or a player.
    /// Returns null for targets that can't take damage or healing.
    /// </summary>
    public static class TargetedActions {
        public static IGameAction Damage(ITarget target, int amount, ICreature attacker = null, bool isCombatDamage = false) {
            switch (target) {
                case ICreature creature: return new DamageCreatureAction(creature, amount, attacker, isCombatDamage);
                case IPlayer player: return new DamagePlayerAction(player, amount, attacker, isCombatDamage);
                default: return null;
            }
        }

        /// <summary>A hit for the attacker's attack as it is when the hit lands, so buffs and debuffs that land first count.</summary>
        public static IGameAction AttackersHit(ITarget target, ICreature attacker) {
            switch (target) {
                case ICreature creature: return new DamageCreatureAction(creature, attacker, isCombatDamage: true);
                case IPlayer player: return new DamagePlayerAction(player, attacker, isCombatDamage: true);
                default: return null;
            }
        }

        public static IGameAction Heal(ITarget target, int amount) {
            switch (target) {
                case ICreature creature: return new HealCreatureAction(creature, amount);
                case IPlayer player: return new HealPlayerAction(player, amount);
                default: return null;
            }
        }
    }
}
