using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class ModifyActionExecutor : ActionExecutor<ModifyAction> {
        private const int SpiteAttack = 2;

        protected override void ExecuteAction(ModifyAction modifyAction, ActionExecutionContext context) {
            var targetCreature = modifyAction.GetTarget() as ICreature;
            if (targetCreature == null) {
                LogWarning("ModifyActionExecutor: Target creature is null.", LogTag.Actions | LogTag.Effects);
                return;
            }

            int value = modifyAction.GetValue();
            int duration = modifyAction.GetDuration();

            // Health Loss (-X Health, GDD glossary) is permanent unless the effect says otherwise: a Persistent Stat
            // Modification of base Health, which stays on the card. Maximum and current Health both drop by X.
            // A timed -X Health is a regular stat change instead, and gives the Health back when it ends.
            // Health Loss isn't a stat change for abilities: Unbothered, No U and the rest don't stop it
            bool isHealthLoss = modifyAction.ShouldModifyHealth() && value < 0 && duration <= 0
                && modifyAction.GetCalculationType() == ModifierCalculationType.Flat;
            if (isHealthLoss) {
                context.ModifierManager.ChangeBaseStats(targetCreature, 0, value, 0);
                context.ModifierManager.Attach(targetCreature, modifyAction.Origin, null, modifyAction.Controller);
            }

            bool changesStats = modifyAction.ShouldModifyAttack() || modifyAction.ShouldModifySpeed() || (modifyAction.ShouldModifyHealth() && !isHealthLoss);
            if (changesStats) {
                // The 'Abilities Compendium' Resolution Order: bypass, block, reflect, transform, then apply and react
                var resolved = Abilities.Resolve(new Abilities.StatChange {
                    Target = targetCreature, Value = value, CalcType = modifyAction.GetCalculationType(),
                    Source = modifyAction.Source, Controller = modifyAction.Controller
                });
                if (resolved is Abilities.StatChange change) {
                    var made = StatModifiers.Apply(context.ModifierManager, context.ModifierFactory, change.Target, change.Value,
                        modifyAction.ShouldModifyAttack(), modifyAction.ShouldModifyHealth() && !isHealthLoss, modifyAction.ShouldModifySpeed(),
                        change.CalcType, duration, context.TurnClock.TurnNumber);
                    foreach (var modifier in made) context.ModifierManager.Attach(change.Target, modifyAction.Origin, modifier, modifyAction.Controller);
                    ReactWithSpite(change, context);
                    if (change.Target != targetCreature) CheckLethal(change.Target, context); // Bounced by No U
                }
            }

            // Losing Health this way isn't damage (no Armor, no damage modifiers, no OnDamaged), but at 0 the creature
            // still goes through the death sequence (GDD 3.3)
            CheckLethal(targetCreature, context);
            Log($"Executed ModifyAction via Executor for {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
        }

        // Spite: an effect that lowered its stats, friendly or enemy, gives it +2 Attack, until it leaves play
        private static void ReactWithSpite(Abilities.StatChange change, ActionExecutionContext context) {
            if (!change.Lowers || !Abilities.Defends(change.Target, CreatureAbility.Spite, change.Source)) return;
            Log($"{change.Target.Name}'s Spite: +{SpiteAttack} Attack", LogTag.Effects);
            StatModifiers.Apply(context.ModifierManager, context.ModifierFactory, change.Target, SpiteAttack,
                true, false, false, ModifierCalculationType.Flat, 0, context.TurnClock.TurnNumber);
        }

        private static void CheckLethal(ICreature creature, ActionExecutionContext context) {
            if (creature.Health <= 0 && !creature.IsDead) {
                context.ActionsQueue.AddAction(new CheckLethalAction(creature));
            }
        }
    }
}
