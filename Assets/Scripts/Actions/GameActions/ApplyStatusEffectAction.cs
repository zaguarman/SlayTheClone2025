using static DebugLogger;
using static Enums;

public class ApplyStatusEffectAction : IGameAction {
    private readonly ICreature targetCreature;
    private readonly StatusEffectType statusType;
    private readonly int duration;
    private readonly int potency;

    // Getters for executor
    public ICreature GetTargetCreature() => targetCreature;
    public StatusEffectType GetStatusType() => statusType;
    public int GetDuration() => duration;
    public int GetPotency() => potency;

    // Constructor remains the same
    public ApplyStatusEffectAction(ICreature target, StatusEffectType type, int duration, int potency) {
        this.targetCreature = target;
        this.statusType = type;
        // Ensure duration has a minimum if needed (e.g., 1 turn)
        this.duration = System.Math.Max(1, duration);
        this.potency = potency;
        Log($"Created ApplyStatusEffectAction: Target={target?.Name}, Status={type}, Duration={this.duration}, Potency={this.potency}", LogTag.Actions | LogTag.Effects);
    }



    public override string ToString() {
        return $"ApplyStatusEffectAction: Target={targetCreature?.Name}, Status={statusType}, Duration={duration}, Potency={potency}";
    }
}
