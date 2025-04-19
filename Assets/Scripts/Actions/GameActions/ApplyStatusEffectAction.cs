using static DebugLogger;
using static Enums;

public class ApplyStatusEffectAction : IGameAction
{
    private readonly ICreature targetCreature;
    private readonly StatusEffectType statusType;
    private readonly int duration;
    private readonly int potency;

    // Updated Constructor
    public ApplyStatusEffectAction(ICreature target, StatusEffectType type, int duration, int potency)
    {
        this.targetCreature = target;
        this.statusType = type;
        // Ensure duration/potency have reasonable defaults if needed
        this.duration = System.Math.Max(1, duration); // Min 1 turn?
        this.potency = potency;
        Log($"Created ApplyStatusEffectAction: Target={target?.Name}, Status={type}, Duration={this.duration}, Potency={this.potency}", LogTag.Actions | LogTag.Effects);
    }

    public void Execute()
    {
        // --- This action now directly applies the modifier ---
        if (targetCreature == null || statusType == StatusEffectType.None)
        {
            LogWarning("ApplyStatusEffectAction: Target creature is null or status type is None.", LogTag.Actions | LogTag.Effects);
            return;
        }

        var modifierManager = GameManager.Instance?.ModifierManager;
        var factory = modifierManager?._modifierFactory;
        var turnManager = GameManager.Instance?.TurnManager;

        if (modifierManager == null || factory == null || turnManager == null)
        {
            LogError($"ApplyStatusEffectAction: ModifierManager, Factory, or TurnManager is null. Cannot apply {statusType} to {targetCreature.Name}.", LogTag.Actions | LogTag.Effects);
            return;
        }

        // Define name and description based on status type
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
    }

     public override string ToString() {
        return $"ApplyStatusEffectAction: Target={targetCreature?.Name}, Status={statusType}, Duration={duration}, Potency={potency}";
    }
}
