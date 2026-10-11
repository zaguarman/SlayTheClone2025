using System;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    // Represents status effects like Burned, Paralyzed, etc.
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

        public bool HasExpired(int currentTurn) {
            bool expired = TimedModifierRules.HasExpired(this, currentTurn);
            if (expired) {
                Log($"StatusEffect '{Name}' ({EffectType}) has expired on turn {currentTurn}.", LogTag.Effects | LogTag.Turns);
            }
            return expired;
        }

        // Status effects hold no subscriptions: Burn damage is queued by ModifierManager.ProcessEndOfTurn
        // (which owns the actions queue) and action prevention is checked through PreventsActions().
        public override void Apply(object target, IGameMediator mediator) {
            Log($"Applying StatusEffect '{Name}' ({EffectType}) to {target}", LogTag.Effects);
        }

        public override void Remove(object target, IGameMediator mediator) {
            Log($"Removing StatusEffect '{Name}' ({EffectType}) from {target}", LogTag.Effects);
        }

        public bool PreventsActions() {
            return EffectType == StatusEffectType.Paralyzed || EffectType == StatusEffectType.Frozen;
        }

        // Paralyzed/Frozen are checked externally through PreventsActions
        public override bool HandlesEvent(EffectTrigger eventType) {
            return EffectType == StatusEffectType.Burned && eventType == EffectTrigger.EndOfTurn; // Ticked by ModifierManager.ProcessEndOfTurn
        }

        public override string ToString() =>
            $"{base.ToString()} - Type: {EffectType}, Duration: {DurationInTurns}, Potency: {Potency}";
    }
}
