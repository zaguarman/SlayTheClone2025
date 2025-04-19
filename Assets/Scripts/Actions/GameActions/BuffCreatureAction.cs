using static DebugLogger;
using static Enums;
using System; // Added for Math.Max

public class BuffCreatureAction : IGameAction {
    private readonly ICreature targetCreature;
    private readonly int value;
    private readonly bool buffAttack;
    private readonly bool buffHealth;
    private readonly ModifierCalculationType calculationType;
    private readonly int durationInTurns; // 0 = permanent

    // --- Single Primary Constructor ---
    public BuffCreatureAction(
        ICreature target,
        int value,
        bool buffAttack,
        bool buffHealth,
        int durationInTurns = 0, // Default to permanent
        ModifierCalculationType calculationType = ModifierCalculationType.Flat) // Default to Flat
    {
        if (target == null)
        {
            LogWarning($"BuffCreatureAction: Target creature is null. Action cannot be created.", LogTag.Actions | LogTag.Effects);
            // Optional: throw an exception or handle gracefully
            // For now, we'll allow creation but Execute will fail safely.
        }

        this.targetCreature = target;
        // Ensure value isn't negative if we only intend to buff
        this.value = (buffAttack || buffHealth) ? Math.Max(0, value) : value; // Allow negative for debuffs if needed later
        this.buffAttack = buffAttack;
        this.buffHealth = buffHealth;
        this.calculationType = calculationType;
        this.durationInTurns = Math.Max(0, durationInTurns); // Ensure non-negative duration

        string buffDesc = DescribeBuff();
        string durationText = this.durationInTurns > 0 ? $" for {this.durationInTurns} turns" : " (permanent)";
        string targetName = target?.Name ?? "NULL TARGET";
        string targetId = target?.TargetId?.ToUpper().Substring(0, 8) ?? "UNKNOWN";

        Log($"Created BuffCreatureAction ({calculationType}) for {targetName} (TargetID: {targetId}) with {buffDesc}{durationText}",
            LogTag.Actions | LogTag.Creatures | LogTag.Effects);
    }

    private string DescribeBuff()
    {
        string desc = "";
        string sign = (calculationType == ModifierCalculationType.Flat && value >= 0) ? "+" : "";
        string suffix = (calculationType == ModifierCalculationType.Percentage) ? "%" : "";

        if (buffAttack && buffHealth) desc = $"{sign}{value}{suffix} Attack & Health";
        else if (buffAttack) desc = $"{sign}{value}{suffix} Attack";
        else if (buffHealth) desc = $"{sign}{value}{suffix} Health";
        else desc = "No Stat Buff"; // Should not happen if constructor logic is sound
        return desc;
    }

    public void Execute() {
        if (targetCreature == null)
        {
            LogWarning($"BuffCreatureAction: Target creature is null. Cannot apply buff.", LogTag.Actions | LogTag.Effects);
            return;
        }

        // --- This action now directly applies the modifier ---
        // No longer queues another action.

        var modifierManager = GameManager.Instance?.ModifierManager;
        var factory = modifierManager?._modifierFactory;

        if (modifierManager == null || factory == null) {
             LogError($"BuffCreatureAction: ModifierManager or Factory is null. Cannot apply buff to {targetCreature.Name}.", LogTag.Actions | LogTag.Effects);
            return;
        }

        // Get current turn number for timed modifiers
        int currentTurn = GameManager.Instance?.TurnManager?.TurnNumber ?? 0;

        // Create and apply modifiers via the ModifierManager
        if (buffAttack && value != 0) {
            string durationText = durationInTurns > 0 ? $" for {durationInTurns} turns" : "";
            string modName = $"Attack Buff ({calculationType} {value}{durationText})";
            string modDesc = $"{(calculationType == ModifierCalculationType.Flat && value >= 0 ? "+" : "")}{value}{(calculationType == ModifierCalculationType.Percentage ? "%" : "")} Attack{durationText}";

            IModifier attackMod;
            if (durationInTurns > 0) {
                attackMod = factory.CreateTimedStatModifier(modName, modDesc, ModifiableStat.Attack, calculationType, value, durationInTurns, currentTurn);
                Log($"BuffCreatureAction applying Timed Attack Modifier: {attackMod} to {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
            } else {
                attackMod = factory.CreateStatModifier(modName, modDesc, ModifiableStat.Attack, calculationType, value);
                Log($"BuffCreatureAction applying Permanent Attack Modifier: {attackMod} to {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
            }
            modifierManager.ApplyModifier(targetCreature, attackMod);
        }

        if (buffHealth && value != 0) {
             // NOTE: Health buffs apply to MAX health. Current health is clamped.
            string durationText = durationInTurns > 0 ? $" for {durationInTurns} turns" : "";
            string modName = $"Health Buff ({calculationType} {value}{durationText})";
            string modDesc = $"{(calculationType == ModifierCalculationType.Flat && value >= 0 ? "+" : "")}{value}{(calculationType == ModifierCalculationType.Percentage ? "%" : "")} Max Health{durationText}";

            IModifier healthMod;
            if (durationInTurns > 0) {
                healthMod = factory.CreateTimedStatModifier(modName, modDesc, ModifiableStat.Health, calculationType, value, durationInTurns, currentTurn);
                 Log($"BuffCreatureAction applying Timed Health Modifier: {healthMod} to {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
            } else {
                healthMod = factory.CreateStatModifier(modName, modDesc, ModifiableStat.Health, calculationType, value);
                Log($"BuffCreatureAction applying Permanent Health Modifier: {healthMod} to {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
            }
            modifierManager.ApplyModifier(targetCreature, healthMod);
             // RecalculateStats called within ApplyModifier will handle clamping current health
        }
    }

    public override string ToString() {
        string buffDesc = DescribeBuff();
        string durationText = durationInTurns > 0 ? $" for {durationInTurns} turns" : " (permanent)";
        string targetName = targetCreature?.Name ?? "NULL";
        string targetId = targetCreature?.TargetId?.ToUpper().Substring(0, 8) ?? "UNKNOWN";

        return $"BuffCreatureAction: Target={targetName}({targetId}), Buff={buffDesc}{durationText}, Calc={calculationType}";
    }
}
