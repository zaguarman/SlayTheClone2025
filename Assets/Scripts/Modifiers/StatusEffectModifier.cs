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
        public const int MaxBlessedStacks = 3;
        public const int MaxHeat = 99; // Only the display limit for now
        /// <summary>Intensity, for statuses that count it instead of Days (Blessed charges, Heat). Time doesn't reduce it.</summary>
        public int Stacks { get; private set; }
        /// <summary>Lasts until removed or used up, whatever its Days (Blessed, Heat Signature).</summary>
        public bool IsPersistent => EffectType is StatusEffectType.Blessed or StatusEffectType.HeatSignature or StatusEffectType.Licensed or StatusEffectType.Survivor;
        /// <summary>Whether it harms its creature: what a Cleanse removes (a Purge removes the rest too). Blessed is the good one.</summary>
        public bool IsNegative => EffectType is not (StatusEffectType.Blessed or StatusEffectType.Concealed or StatusEffectType.Encrypted or StatusEffectType.Untouchable
            or StatusEffectType.BorrowedTime or StatusEffectType.Licensed or StatusEffectType.Composed or StatusEffectType.Survivor);
        /// <summary>Its Days count the Day it lands, like a Day-long buff (Untouchable 1 is this Day only), instead of skipping it.</summary>
        public bool CountsTheDayItLands => EffectType is StatusEffectType.Untouchable or StatusEffectType.Composed;

        /// <summary>Doomed can't be cleansed: a cleanse attempt passes it on instead (Heresy). Nor can Brainwashed: only taking
        /// the creature off the board in time saves it.</summary>
        public bool IsCleansable => EffectType is not (StatusEffectType.Doomed or StatusEffectType.Brainwashed);

        public StatusEffectModifier(string name, string description, StatusEffectType type, int duration, int potency, int currentTurn)
            : base(name, description) {
            EffectType = type;
            DurationInTurns = Math.Max(1, duration);
            TurnApplied = currentTurn;
            Potency = potency;
            if (IsPersistent) SetStacks(potency);

            Log($"StatusEffectModifier '{Name}' created: Type={EffectType}, Duration={DurationInTurns}, Potency={Potency}, AppliedTurn={TurnApplied}", LogTag.Effects);
        }

        /// <summary>The Dusks it has left, today's included (what its icon shows); 0 for a persistent one.</summary>
        public int DusksLeft(int today) => IsPersistent ? 0 : Math.Max(1, TurnApplied + DurationInTurns - (CountsTheDayItLands ? 1 : 0) - today + 1);

        /// <summary>Whether it ticks at the Dusk ending this turn: never on the Day it was applied.</summary>
        public bool ActsAtDuskOf(int endedTurn) => endedTurn > TurnApplied;

        /// <summary>Checked with the turn about to start: its last Dusk was the one ending Day TurnApplied + Duration.</summary>
        public bool HasExpired(int currentTurn) {
            if (IsPersistent) return false;
            bool expired = currentTurn > TurnApplied + DurationInTurns - (CountsTheDayItLands ? 1 : 0);
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
        public bool DisablesAbilities() => EffectType is StatusEffectType.Exhausted or StatusEffectType.Blackout; // Blackout: GoldenEye's pulse, an NDA
        public bool Exposes() => EffectType == StatusEffectType.Exhausted;

        /// <summary>Sets the status's intensity, between 1 and its maximum. Shown as "Blessed 2" or "Heat 3".</summary>
        public void SetStacks(int stacks) {
            if (EffectType == StatusEffectType.HeatSignature) {
                Stacks = Math.Max(1, Math.Min(MaxHeat, stacks));
                Description = $"Heat Signature {Stacks}: random effects and redirected attacks go to the hottest creature";
                return;
            }
            if (EffectType == StatusEffectType.Survivor) {
                Stacks = 1;
                Description = "Survivor: from full Health, a killing hit leaves it at 1";
                return;
            }
            if (EffectType == StatusEffectType.Licensed) {
                Stacks = 1;
                Description = "Licensed: its next attack on a creature destroys it";
                return;
            }
            Stacks = Math.Max(1, Math.Min(MaxBlessedStacks, stacks));
            Description = $"Blessed {Stacks}: the next status that would land is negated";
        }

        /// <summary>A Dawn effect acts from the Dawn of Day X+2 when applied on Day X (Status Timing).</summary>
        public bool ActsAtDawnOf(int startingTurn) => startingTurn >= TurnApplied + 2;

        public override string ToString() =>
            $"{base.ToString()} - Type: {EffectType}, Duration: {DurationInTurns}, Potency: {Potency}";
    }
}
