using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class MoveCreatureAction : IGameAction {
    #region Fields & Properties
    private readonly ICreature creature;
    private readonly ITarget fromSlot;
    private readonly ITarget toSlot;
    private readonly IPlayer player;
    public ICreature GetCreature() => creature;
    public ITarget GetFromSlot() => fromSlot;
    public ITarget GetToSlot() => toSlot;
    public IPlayer GetPlayer() => player;
    #endregion

    #region Constructor
    public MoveCreatureAction(ICreature creature, ITarget fromSlot, ITarget toSlot, IPlayer player) {
        this.creature = creature;
        this.fromSlot = fromSlot;
        this.toSlot = toSlot;
        this.player = player;
        Log($"Created MoveCreatureAction for {creature?.Name} (TargetID: {creature?.TargetId.ToUpper()}) from slot {fromSlot?.TargetId.ToUpper()} to {toSlot?.TargetId.ToUpper()}",
            LogTag.Actions | LogTag.Creatures);
    }
    #endregion

    #region Methods
    public void Execute() {
        // Logic moved to MoveCreatureActionExecutor
        Log($"MoveCreatureAction Execute() called for {creature?.Name}. Logic handled by Executor.", LogTag.Actions | LogTag.Creatures);
    }



    public override string ToString() {
        return $"MoveCreatureAction: Creature={creature?.Name} (TargetID: {creature?.TargetId.ToUpper()}), FromSlot={fromSlot?.TargetId.ToUpper()}, ToSlot={toSlot?.TargetId.ToUpper()}";
    }
    #endregion
}