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
        /// <summary>The creature whose effect it is, if any (where Return to Sender bounces it).</summary>
        public ICreature Source { get; }
        /// <summary>Whose effect it is; null: nobody's. Return to Sender only bounces enemy statuses.</summary>
        public IPlayer Controller { get; }

        public ApplyStatusEffectAction(ICreature target, StatusEffectType type, int duration, int potency, ICreature source = null, IPlayer controller = null) {
            Source = source;
            Controller = controller;
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
