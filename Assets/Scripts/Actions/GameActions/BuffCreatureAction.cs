using static DebugLogger;
using static Enums; // Assuming your Enums class is accessible

public class BuffCreatureAction : IGameAction {
    private readonly ICreature targetCreature;
    private readonly int value; // Use a single value for simplicity, applied to attack/health based on flags
    private readonly bool buffAttack;
    private readonly bool buffHealth;
    private readonly ModifierCalculationType calculationType; // Add calculation type (e.g., Flat)
    private readonly int durationInTurns; // Duration in turns (0 = permanent)

    // Constructor for flat buffs with no duration (permanent)
    public BuffCreatureAction(ICreature target, int flatValue, bool buffAttack, bool buffHealth)
        : this(target, flatValue, buffAttack, buffHealth, 0) { }

    // Constructor for flat buffs with duration
    public BuffCreatureAction(ICreature target, int flatValue, bool buffAttack, bool buffHealth, int durationInTurns) {
        this.targetCreature = target;
        this.value = flatValue;
        this.buffAttack = buffAttack;
        this.buffHealth = buffHealth;
        this.calculationType = ModifierCalculationType.Flat; // Defaulting to Flat
        this.durationInTurns = durationInTurns;

        string durationText = durationInTurns > 0 ? $" for {durationInTurns} turns" : " (permanent)";
        Log($"Created BuffCreatureAction (Flat: {value}{durationText}) for {target?.Name} (TargetID: {target?.TargetId.ToUpper().Substring(0, 8)}), BuffAttack={buffAttack}, BuffHealth={buffHealth}",
            LogTag.Actions | LogTag.Creatures | LogTag.Effects);
    }

    // Constructor for backward compatibility
    public BuffCreatureAction(ICreature target, int attackBuff, int healthBuff, bool buffAttack, bool buffHealth)
        : this(target, buffAttack ? attackBuff : healthBuff, buffAttack, buffHealth, 0) { }

    // Constructor with duration for backward compatibility
    public BuffCreatureAction(ICreature target, int attackBuff, int healthBuff, bool buffAttack, bool buffHealth, int durationInTurns) {
        this.targetCreature = target;
        this.value = buffAttack ? attackBuff : healthBuff; // Use the appropriate value based on which buff is active
        this.buffAttack = buffAttack;
        this.buffHealth = buffHealth;
        this.calculationType = ModifierCalculationType.Flat; // Defaulting to Flat
        this.durationInTurns = durationInTurns;

        string buffDescription = "";
        if (buffAttack && buffHealth) {
            buffDescription = $"+{attackBuff}/+{healthBuff}";
        } else if (buffAttack) {
            buffDescription = $"+{attackBuff} attack";
        } else if (buffHealth) {
            buffDescription = $"+{healthBuff} health";
        }

        string durationText = durationInTurns > 0 ? $" for {durationInTurns} turns" : " (permanent)";
        Log($"Created BuffCreatureAction for {target?.Name} (TargetID: {target?.TargetId.ToUpper().Substring(0, 8)}) with {buffDescription}{durationText}",
            LogTag.Actions | LogTag.Creatures | LogTag.Effects);
    }

    // For backward compatibility
    public BuffCreatureAction(ICreature target, int attackBuff, int healthBuff)
        : this(target, attackBuff, healthBuff, true, true, 0) { }

    public void Execute() {
        if (targetCreature == null)
        {
            LogWarning($"BuffCreatureAction: Target creature is null. Cannot apply buff.", LogTag.Actions | LogTag.Effects);
            return;
        }

        var modifierManager = GameManager.Instance?.ModifierManager;
        var factory = modifierManager?._modifierFactory; // Access factory via manager (make it accessible if needed)

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
            string modDesc = $"{(calculationType == ModifierCalculationType.Flat ? "+" : "")}{value}{(calculationType == ModifierCalculationType.Percentage ? "%" : "")} Attack{durationText}";

            IModifier attackMod;
            if (durationInTurns > 0) {
                // Create a timed modifier
                attackMod = factory.CreateTimedStatModifier(modName, modDesc, ModifiableStat.Attack, calculationType, value, durationInTurns, currentTurn);
                Log($"BuffCreatureAction applying Timed Attack Modifier: {attackMod} to {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
            } else {
                // Create a permanent modifier
                attackMod = factory.CreateStatModifier(modName, modDesc, ModifiableStat.Attack, calculationType, value);
                Log($"BuffCreatureAction applying Permanent Attack Modifier: {attackMod} to {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
            }

            modifierManager.ApplyModifier(targetCreature, attackMod);
        }

        if (buffHealth && value != 0) {
            string durationText = durationInTurns > 0 ? $" for {durationInTurns} turns" : "";
            string modName = $"Health Buff ({calculationType} {value}{durationText})";
            string modDesc = $"{(calculationType == ModifierCalculationType.Flat ? "+" : "")}{value}{(calculationType == ModifierCalculationType.Percentage ? "%" : "")} Max Health{durationText}";

            IModifier healthMod;
            if (durationInTurns > 0) {
                // Create a timed modifier
                healthMod = factory.CreateTimedStatModifier(modName, modDesc, ModifiableStat.Health, calculationType, value, durationInTurns, currentTurn);
                Log($"BuffCreatureAction applying Timed Health Modifier: {healthMod} to {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
            } else {
                // Create a permanent modifier
                healthMod = factory.CreateStatModifier(modName, modDesc, ModifiableStat.Health, calculationType, value);
                Log($"BuffCreatureAction applying Permanent Health Modifier: {healthMod} to {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
            }

            modifierManager.ApplyModifier(targetCreature, healthMod);
        }
    }

    public override string ToString() {
        string buffDesc = "";
        if (buffAttack) buffDesc += $"Attack {(calculationType == ModifierCalculationType.Flat ? "+" : "")}{value}{(calculationType == ModifierCalculationType.Percentage ? "%" : "")} ";
        if (buffHealth) buffDesc += $"Health {(calculationType == ModifierCalculationType.Flat ? "+" : "")}{value}{(calculationType == ModifierCalculationType.Percentage ? "%" : "")}";

        string durationText = durationInTurns > 0 ? $" for {durationInTurns} turns" : " (permanent)";

        return $"BuffCreatureAction: Target={targetCreature?.Name} (ID: {targetCreature?.TargetId.ToUpper().Substring(0, 8)}), Buff={buffDesc.Trim()}{durationText}";
    }
}
