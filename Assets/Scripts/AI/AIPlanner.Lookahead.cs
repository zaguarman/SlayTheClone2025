using System.Collections.Generic;
using System.Linq;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// The AI's look ahead (a part of AIPlanner; the user, 2026-10-17: Hard and Very Hard "value each action with context and
    /// planning the future"). Hard sees the next Day as far as its seat can: which of their creatures will be able to act
    /// (the ones deploying now that are ready by then included), which empty slots of ours they can hit us through (lanes). It weighs its plays against that Day: a
    /// creature goes where it isn't killed while it deploys, a lane is closed
    /// when they could run us down, and a removal spell waits a Day for the bigger creature
    /// that is about to be ready when today's best is small. Very Hard also counts two of their attackers ganging up on one of ours.
    /// </summary>
    public partial class AIPlanner {
        #region Next Day
        // One of theirs that can act next Day: acting now, or one Day of deployment left (a known card says how long it needs)
        private bool ActsNextDay(SeenCreature theirs) {
            if (HealthLeft(theirs) <= 0 || Condemned(theirs)) return false;
            if (theirs.Operative) return !IsStunned(theirs);
            return Hard && theirs.Known && theirs.Creature.TurnsUntilOperative <= 1;
        }

        private IEnumerable<SeenCreature> TheirAttackersNextDay() => view.Theirs.Where(c => ActsNextDay(c) && AttackOf(c) > 0);

        // What they could deal next Day, if nothing stopped them (Hard: with the creatures ready by then)
        private int TheirDamageNextDay() => Hard ? TheirAttackersNextDay().Sum(AttackOf) : TheirPotentialDamage();

        // Their attackers that can hit this slot of ours next Day: all of them, any creature attacks any slot
        private IEnumerable<SeenCreature> TheirAttackersOn(int ourSlotIndex) => TheirAttackersNextDay();

        // An empty slot of ours one of them can hit the player through: a lane
        private bool IsLane(SeenSlot ours) => ours.IsFree && TheirAttackersOn(ours.Index).Any();

        // They could bring us to 0 next Day through the lanes and with what reaches us
        private bool TheyThreatenLethal() => TheirDamageNextDay() >= view.MyHealth && view.MySlots.Any(IsLane);
        #endregion

        #region Where a creature goes
        // Hard: what landing in this slot is worth beyond the slot's own effect (no slot gives a bonus or reach since 2026-10-17):
        // cover while it deploys from attackers that would kill it there, and a lane closed when they could run us down
        private float FormationValue(ICreature creature, SeenSlot slot) {
            float value = 0f;
            // Deploying under fire: a creature they can reach and kill before it acts is worth little there; one they can only scratch waits a little
            int deploying = creature.TurnsUntilOperative;
            if (deploying >= 1) {
                var guns = TheirAttackersOn(slot.Index).Select(AttackOf).OrderByDescending(d => d).ToList();
                int incoming = VeryHard ? guns.Take(2).Sum() : guns.FirstOrDefault();
                if (incoming >= creature.Health) value -= (creature.Attack + creature.Health) * 0.5f * Style.Protection;
                else if (incoming > 0) value -= 0.3f * deploying;
            }
            if (TheyThreatenLethal() && IsLane(slot)) value += 4f * Style.OwnHealth; // A body in the lane
            return value;
        }
        #endregion

        #region Holding a spell for tomorrow
        // Hard: a kill or a big hit is worth more on one of theirs that is ready next Day and bigger than anything today's aim
        // offers; the spell waits when the difference is clear, unless they threaten us now
        private bool BetterTomorrow(Spell spell, ITarget bestAim, float today) {
            if (!Hard || TheyThreatenLethal()) return false;
            var actions = spell.Effects.SelectMany(e => e.actions).ToList();
            var removal = actions.FirstOrDefault(a => a.actionType is ActionType.Destroy or ActionType.Damage && a.targetType is TargetType.EnemyCreatures or TargetType.AllCreatures
                && !a.targetModifier.HasFlag(TargetModifier.Random) && !a.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets));
            if (removal == null || Prophecy.DaysOf(spell) > 0 || Policy.Has(spell)) return false;
            float tomorrow = 0;
            foreach (var theirs in view.Theirs.Where(t => !t.Operative && t.Known && t.Creature.TurnsUntilOperative <= 1 && Hittable(t) && !Condemned(t))) {
                bool kills = removal.actionType == ActionType.Destroy ? Lands(removal, theirs) : removal.value + (IsExposed(theirs) ? 1 : 0) >= HealthLeft(theirs);
                if (!kills) continue;
                float worth = (theirs.Worth + HealthLeft(theirs) + AttackOf(theirs)) * Style.Removal * 0.8f; // Less: a Day may change it
                if (worth > tomorrow) tomorrow = worth;
            }
            return tomorrow > today + 1.5f;
        }
        #endregion
    }
}
