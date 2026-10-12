using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class CleanseActionExecutor : ActionExecutor<CleanseAction> {
        protected override void ExecuteAction(CleanseAction cleanse, ActionExecutionContext context) {
            var creature = cleanse.GetTargetCreature();
            if (creature == null || creature.IsDead) return;

            var manager = context.ModifierManager;
            if (cleanse.GetWhat() != CleanseTarget.StatModifications) {
                foreach (var status in manager.GetActiveModifiersFor(creature).OfType<StatusEffectModifier>().ToList()) {
                    if (!cleanse.IsPurge && !status.IsNegative) continue; // A Cleanse leaves the good ones (Blessed)
                    if (status.IsCleansable) manager.RemoveModifier(creature, status); // Blessed loses all its stacks
                    else if (status.EffectType == StatusEffectType.Doomed) PassOnDoomed(creature, status, context);
                }
            }

            if (cleanse.GetWhat() != CleanseTarget.Statuses) {
                // Temporary Stat Modifications only: base-stat (Persistent) changes stay on the card
                foreach (var change in manager.GetActiveModifiersFor(creature).OfType<StatModifier>().ToList()) {
                    if (!cleanse.IsPurge && !Lowers(change)) continue; // A Cleanse only removes decreases
                    manager.RemoveModifier(creature, change);
                }
                // Losing a Health buff takes its Health back, which can kill (GDD 3.3)
                if (creature.Health <= 0) context.ActionsQueue.AddAction(new CheckLethalAction(creature));
            }
            Log($"Executed {cleanse}", LogTag.Actions | LogTag.Effects);
        }

        private static bool Lowers(StatModifier change) =>
            new[] { ModifiableStat.Attack, ModifiableStat.Health, ModifiableStat.Speed }
                .Any(stat => change.TryGetStatModification(stat, out _, out int value) && value < 0);

        // Heresy ('Status Effects Compendium' - Doomed): the sentence moves, with its remaining Days, to the creature with the
        // lowest current Health on this creature's side (GDD 5.2 Passing a Status). This one is freed and keeps the Attack it
        // gained. With nobody to pass it to, the sentence is carried out now. If the vessel is Doomed already, Heresy fizzles:
        // nothing happens and this creature stays Doomed.
        private static void PassOnDoomed(ICreature creature, StatusEffectModifier doomed, ActionExecutionContext context) {
            var vessel = creature.Owner?.Battlefield
                .Select(slot => slot.OccupyingCreature)
                .Where(other => other != null && other != creature && !other.IsDead)
                .OrderBy(other => other.Health)
                .ThenBy(_ => GameRandom.Next(int.MaxValue))
                .FirstOrDefault();

            if (vessel != null && context.ModifierManager.HasStatusEffect(vessel, StatusEffectType.Doomed)) {
                Log($"Heresy fizzles: {vessel.Name} is Doomed already, so {creature.Name} stays Doomed", LogTag.Effects);
                return;
            }

            context.ModifierManager.RemoveModifier(creature, doomed);
            if (vessel == null) {
                Log($"Heresy: {creature.Name} has nobody to pass Doomed to, so the sentence is carried out", LogTag.Effects);
                context.ActionsQueue.AddAction(new DoomedSentenceAction(creature));
                return;
            }

            Log($"Heresy: {creature.Name} passes Doomed on to {vessel.Name}", LogTag.Effects);
            // Same countdown: it keeps the Day it started and its Days
            StatusRules.Land(context, vessel, StatusEffectType.Doomed, doomed.DurationInTurns, doomed.Potency, doomed.TurnApplied);
        }
    }
}
