using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    public class ModifyArmorAction : IGameAction {
        private readonly ICreature targetCreature;
        private readonly int amount; // Positive to add armor, negative to remove

        public ICreature GetTargetCreature() => targetCreature;
        public int GetAmount() => amount;

        public ModifyArmorAction(ICreature target, int amount) {
            if (target == null) {
                LogWarning("ModifyArmorAction: Target creature is null. Action cannot be created.", LogTag.Actions | LogTag.Effects);
            }
            targetCreature = target;
            this.amount = amount;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() {
            return $"ModifyArmorAction: Target={targetCreature?.Name}, Amount={amount}";
        }
    }
}
