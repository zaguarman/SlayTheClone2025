using static Enums;
using static DebugLogger;
using UnityEngine; // Needed for transform

public class SummonCreatureAction : IGameAction {
    private readonly ICreature creature;
    private readonly IPlayer owner;
    private readonly ITarget targetSlot; // Target is expected to be a BattlefieldSlot
    private readonly bool fromDeck;

    // Getters for executor
    public ICreature GetCreature() => creature;
    public IPlayer GetOwner() => owner;
    public ITarget GetTargetSlot() => targetSlot;
    public bool IsFromDeck() => fromDeck;

    public SummonCreatureAction(ICreature creature, IPlayer owner, ITarget target, bool fromDeck = false) {
        this.creature = creature;
        this.owner = owner;
        this.targetSlot = target; // Keep ITarget for flexibility, but expect BattlefieldSlot
        this.fromDeck = fromDeck;
        // Log($"Created SummonCreatureAction for {creature?.Name} targeting slot {target?.TargetId} with {creature?.Effects.Count ?? 0} effects (fromDeck: {fromDeck})",
        //     LogTag.Actions | LogTag.Creatures);
    }

    public void Execute() {
        // Logic moved to SummonCreatureActionExecutor
        Log($"SummonCreatureAction Execute() called for {creature?.Name}. Logic handled by Executor.", LogTag.Actions | LogTag.Creatures);
    }

    public override string ToString() {
         string slotIdStr = (targetSlot is BattlefieldSlot s) ? s.TargetId.ToUpper().Substring(0,8) : targetSlot?.TargetId ?? "UNKNOWN";
        return $"SummonCreatureAction: Cr={creature?.Name}({creature?.TargetId.ToUpper().Substring(0,8)}), Own={(owner?.IsPlayer1 == true ? "P1" : "P2")}, Slot={slotIdStr}, Deck={fromDeck}";
    }
}