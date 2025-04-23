using static DebugLogger;
using static Enums;

public class ApplyStatusEffectAction : IGameAction {
    private readonly ICreature targetCreature;
    private readonly StatusEffectType statusType;
    private readonly int duration;
    private readonly int potency;

    // Constructor remains the same
    public ApplyStatusEffectAction(ICreature target, StatusEffectType type, int duration, int potency) {
        this.targetCreature = target;
        this.statusType = type;
        // Ensure duration has a minimum if needed (e.g., 1 turn)
        this.duration = System.Math.Max(1, duration);
        this.potency = potency;
        Log($"Created ApplyStatusEffectAction: Target={target?.Name}, Status={type}, Duration={this.duration}, Potency={this.potency}", LogTag.Actions | LogTag.Effects);
    }

    public void Execute() {
        // --- Centralized Service Lookup & Null Checks ---
        var gameManager = GameManager.Instance; // Accept limited singleton access here
        if (gameManager == null) {
            LogError("ApplyStatusEffectAction: GameManager instance is null. Cannot execute.", LogTag.Actions | LogTag.Effects | LogTag.Initialization);
            return;
        }

        var modifierManager = gameManager.ModifierManager;
        var factory = modifierManager?.ModifierFactory; // Get factory via manager
        var turnManager = gameManager.TurnManager;

        if (modifierManager == null || factory == null || turnManager == null) {
            LogError($"ApplyStatusEffectAction: ModifierManager ({modifierManager?.GetType().Name}), Factory ({factory?.GetType().Name}), or TurnManager ({turnManager?.GetType().Name}) is null. Cannot apply {statusType} to {targetCreature?.Name}.", LogTag.Actions | LogTag.Effects | LogTag.Initialization);
            return;
        }

        if (targetCreature == null || statusType == StatusEffectType.None) {
            LogWarning("ApplyStatusEffectAction: Target creature is null or status type is None.", LogTag.Actions | LogTag.Effects);
            return;
        }
        // --- End Service Lookup ---

        // --- Core Logic: Create and Apply Modifier ---
        string effectName = $"{statusType} Effect";
        string effectDescription = $"Applies {statusType} for {duration} turns";
        if (potency > 0 && (statusType == StatusEffectType.Burned || statusType == StatusEffectType.Poisoned)) {
            effectDescription += $" (Potency: {potency})";
        }

        // Create the StatusEffectModifier using the factory
        IModifier statusModifier = factory.CreateStatusEffectModifier(
            effectName,
            effectDescription,
            statusType,
            duration,
            potency,
            turnManager.TurnNumber // Pass the current turn number
        );

        // Apply the modifier via the ModifierManager
        modifierManager.ApplyModifier(targetCreature, statusModifier);
        Log($"ApplyStatusEffectAction executed: Applied {statusType} to {targetCreature.Name} via ModifierManager", LogTag.Actions | LogTag.Effects);
        // --- End Core Logic ---
    }

    public override string ToString() {
        return $"ApplyStatusEffectAction: Target={targetCreature?.Name}, Status={statusType}, Duration={duration}, Potency={potency}";
    }
}
