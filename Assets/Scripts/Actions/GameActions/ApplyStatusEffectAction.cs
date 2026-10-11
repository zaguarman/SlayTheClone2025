using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class ApplyStatusEffectAction : IGameAction {
        private readonly ICreature targetCreature;
        private readonly StatusEffectType statusType;
        private readonly int duration;
        private readonly int potency;

        public ICreature GetTargetCreature() => targetCreature;
        public StatusEffectType GetStatusType() => statusType;
        public int GetDuration() => duration;
        public int GetPotency() => potency;

        public ApplyStatusEffectAction(ICreature target, StatusEffectType type, int duration, int potency) {
            targetCreature = target;
            statusType = type;
            this.duration = System.Math.Max(1, duration); // A status lasts at least one turn
            this.potency = potency;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() {
            return $"ApplyStatusEffectAction: Target={targetCreature?.Name}, Status={statusType}, Duration={duration}, Potency={potency}";
        }
    }
}
