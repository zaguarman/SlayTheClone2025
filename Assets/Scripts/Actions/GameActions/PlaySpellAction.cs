using static DebugLogger;

public class PlaySpellAction : IGameAction {
    private readonly Spell spell;
    private readonly IPlayer owner;
    private readonly ITarget target;

    // Getters for executor
    public Spell GetSpell() => spell;
    public IPlayer GetOwner() => owner;
    public ITarget GetTarget() => target;

    public PlaySpellAction(Spell spell, IPlayer owner, ITarget target = null) {
        this.spell = spell;
        this.owner = owner;
        this.target = target;
        Log($"Created PlaySpellAction for {spell.Name} (TargetID: {spell.TargetId.ToUpper()})", LogTag.Actions | LogTag.Cards);
    }

    public void Execute() {
        // Logic moved to PlaySpellActionExecutor
        Log($"PlaySpellAction Execute() called for {spell?.Name}. Logic handled by Executor.", LogTag.Actions | LogTag.Cards);
    }

    public override string ToString() {
        return $"PlaySpellAction: Spell={spell?.Name} (TargetID: {spell?.TargetId.ToUpper()}), Owner={(owner?.IsPlayer1 == true ? "Player 1" : "Player 2")} (TargetID: {owner?.TargetId.ToUpper()}), Target={target?.TargetId.ToUpper()}";
    }
}