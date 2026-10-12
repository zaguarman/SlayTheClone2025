using System;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    // Represents status effects like Burned, Exhausted, etc.
    // Status Timing (GDD 5.2): every status skips the Day it's applied. Applied on Day X for N Days, it first acts at the
    // Dusk of Day X+1 and ends at the Dusk of Day X+N, so there's always a full Day to see it and react.
    public class StatusEffectModifier : BaseModifier, ITimedModifier {
        public StatusEffectType EffectType { get; }
        public int DurationInTurns { get; }
        public int TurnApplied { get; }
        public int Potency { get; } // e.g., Burn damage per turn

        public StatusEffectModifier(string name, string description, StatusEffectType type, int duration, int potency, int currentTurn)
            : base(name, description) {
            EffectType = type;
            DurationInTurns = Math.Max(1, duration);
            TurnApplied = currentTurn;
            Potency = potency;

            Log($"StatusEffectModifier '{Name}' created: Type={EffectType}, Duration={DurationInTurns}, Potency={Potency}, AppliedTurn={TurnApplied}", LogTag.Effects);
        }

        /// <summary>Whether it ticks at the Dusk ending this turn: never on the Day it was applied.</summary>
        public bool ActsAtDuskOf(int endedTurn) => endedTurn > TurnApplied;

        /// <summary>Checked with the turn about to start: its last Dusk was the one ending Day TurnApplied + Duration.</summary>
        public bool HasExpired(int currentTurn) {
            bool expired = currentTurn > TurnApplied + DurationInTurns;
            if (expired) {
                Log($"StatusEffect '{Name}' ({EffectType}) has expired on turn {currentTurn}.", LogTag.Effects | LogTag.Turns);
            }
            return expired;
        }

        // Status effects hold no subscriptions: Burn damage is queued by ModifierManager.ProcessEndOfTurn
        // (which owns the actions queue) and action prevention is checked through PreventsActions().
        public override void Apply(ICreature target, IGameMediator mediator) {
            Log($"Applying StatusEffect '{Name}' ({EffectType}) to {target}", LogTag.Effects);
        }

        public override void Remove(ICreature target, IGameMediator mediator) {
            Log($"Removing StatusEffect '{Name}' ({EffectType}) from {target}", LogTag.Effects);
        }

        // Exhausted is a complete shutdown: Cannot Act, Abilities Disabled, and the Exposed that comes with it
        public bool PreventsActions() => EffectType == StatusEffectType.Exhausted;
        public bool DisablesAbilities() => EffectType == StatusEffectType.Exhausted;
        public bool Exposes() => EffectType == StatusEffectType.Exhausted;

        /// <summary>A Dawn effect acts from the Dawn of Day X+2 when applied on Day X (Status Timing).</summary>
        public bool ActsAtDawnOf(int startingTurn) => startingTurn >= TurnApplied + 2;

        public override string ToString() =>
            $"{base.ToString()} - Type: {EffectType}, Duration: {DurationInTurns}, Potency: {Potency}";
    }
}
