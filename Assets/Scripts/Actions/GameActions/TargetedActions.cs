namespace SlayTheClone {
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

        public static IGameAction Heal(ITarget target, int amount) {
            switch (target) {
                case ICreature creature: return new HealCreatureAction(creature, amount);
                case IPlayer player: return new HealPlayerAction(player, amount);
                default: return null;
            }
        }
    }
}
