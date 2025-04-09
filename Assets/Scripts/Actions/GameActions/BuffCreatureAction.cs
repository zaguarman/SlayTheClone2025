using static DebugLogger;
using static Enums;

public class BuffCreatureAction : IGameAction {
    private readonly ICreature target;
    private readonly int attackBuff;
    private readonly int healthBuff;
    private readonly bool buffAttack;
    private readonly bool buffHealth;

    public BuffCreatureAction(ICreature target, int attackBuff, int healthBuff) {
        this.target = target;
        this.attackBuff = attackBuff;
        this.healthBuff = healthBuff;
        this.buffAttack = true;
        this.buffHealth = true;
        Log($"Created BuffCreatureAction for {target.Name} (TargetID: {target.TargetId.ToUpper()}) with +{attackBuff}/+{healthBuff}",
            LogTag.Actions | LogTag.Creatures | LogTag.Effects);
    }

    public BuffCreatureAction(ICreature target, int attackBuff, int healthBuff, bool buffAttack, bool buffHealth) {
        this.target = target;
        this.attackBuff = attackBuff;
        this.healthBuff = healthBuff;
        this.buffAttack = buffAttack;
        this.buffHealth = buffHealth;

        string buffDescription = "";
        if (buffAttack && buffHealth) {
            buffDescription = $"+{attackBuff}/+{healthBuff}";
        } else if (buffAttack) {
            buffDescription = $"+{attackBuff} attack";
        } else if (buffHealth) {
            buffDescription = $"+{healthBuff} health";
        }

        Log($"Created BuffCreatureAction for {target.Name} (TargetID: {target.TargetId.ToUpper()}) with {buffDescription}",
            LogTag.Actions | LogTag.Creatures | LogTag.Effects);
    }

    public void Execute() {
        if (target == null) return;

        if (buffAttack && attackBuff != 0) {
            target.AddAttackModifier(new FlatModifier(attackBuff));
        }

        if (buffHealth && healthBuff != 0) {
            target.AddHealthModifier(new FlatModifier(healthBuff));
        }

        string buffDescription = "";
        if (buffAttack && buffHealth) {
            buffDescription = $"+{attackBuff}/+{healthBuff}";
        } else if (buffAttack) {
            buffDescription = $"+{attackBuff} attack";
        } else if (buffHealth) {
            buffDescription = $"+{healthBuff} health";
        }

        Log($"Applied buff of {buffDescription} to {target.Name} (TargetID: {target.TargetId.ToUpper()})",
            LogTag.Actions | LogTag.Creatures | LogTag.Effects);
    }

    public override string ToString() {
        string buffDescription = "";
        if (buffAttack && buffHealth) {
            buffDescription = $"+{attackBuff}/+{healthBuff}";
        } else if (buffAttack) {
            buffDescription = $"+{attackBuff} attack";
        } else if (buffHealth) {
            buffDescription = $"+{healthBuff} health";
        }

        return $"BuffCreatureAction: Target={target?.Name} (TargetID: {target?.TargetId.ToUpper()}), Buff={buffDescription}";
    }
}
