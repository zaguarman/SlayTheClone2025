using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// A creature uses its Active ability as its intent for the Day, instead of attacking or moving (GDD 3.6: one intent a Day).
    /// Aimed at a slot like an attack: it lands on whatever creature is there when it resolves, in the Ability and Attack layer.
    /// </summary>
    public class UseAbilityAction : IGameAction {
        public ICreature Creature { get; }
        public BoardSlot TargetSlot { get; }

        public UseAbilityAction(ICreature creature, BoardSlot targetSlot) {
            Creature = creature;
            TargetSlot = targetSlot;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"UseAbilityAction: {Creature?.Name} uses {Creature?.Ability} on {TargetSlot}";
    }

    public class UseAbilityActionExecutor : ActionExecutor<UseAbilityAction> {
        protected override void ExecuteAction(UseAbilityAction use, ActionExecutionContext context) {
            var user = use.Creature;
            var target = use.TargetSlot?.OccupyingCreature;
            if (user == null || !context.ModifierManager.CanAct(user)) return;
            if (!Abilities.CanUseActive(user, context.ModifierManager)) {
                Log($"{user.Name} can't use an ability now (it has {user.Ability}, switched {(Abilities.IsActive(user) ? "on" : "off")}, maybe resting or spent)", LogTag.Effects);
                return;
            }
            if (target == null || target == user || target.IsDead) {
                Log($"{user.Name}'s {user.Ability} fizzles: no creature in {use.TargetSlot}", LogTag.Effects);
                return;
            }

            switch (user.Ability) {
                case CreatureAbility.IdentityTheft:
                    // Swap abilities: the target ends up with Identity Theft and can use it in turn. It lasts until each card is reshuffled
                    Log($"{user.Name} steals {target.Name}'s {target.Ability}", LogTag.Effects);
                    (user.Ability, target.Ability) = (target.Ability, user.Ability);
                    context.GameMediator.NotifyCreatureStatsChanged(user);
                    context.GameMediator.NotifyCreatureStatsChanged(target);
                    break;
                case CreatureAbility.Bodyguard:
                    // Another friendly creature takes no damage this Day; then the Bodyguard rests a Day
                    if (target.Owner != user.Owner) {
                        Log($"{user.Name} only guards friendly creatures", LogTag.Effects);
                        return;
                    }
                    context.ActionsQueue.AddAction(new ApplyStatusEffectAction(target, StatusEffectType.Untouchable, 1, 0, user, user.Owner));
                    context.ActionsQueue.AddAction(new ApplyStatusEffectAction(user, StatusEffectType.Cooldown, 1, 0, user, user.Owner));
                    break;
                case CreatureAbility.Wannabe:
                    context.ActionsQueue.AddAction(new CopyStatsAction(user, target));
                    break;
                case CreatureAbility.CreditGrab:
                    user.Owner?.UsedOncePerCombat.Add(Abilities.OnceKey(user)); // Spent for the rest of the combat
                    context.ActionsQueue.AddAction(new StealStatChangesAction(user, target));
                    break;
                case CreatureAbility.ShellGame:
                    context.ActionsQueue.AddAction(new SwapPositionsAction(target));
                    break;
            }
        }
    }
}
