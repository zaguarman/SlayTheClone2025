using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class DamagePlayerAction : IGameAction {
    #region Fields & Properties
    private IPlayer target;
    private int damage;
    public IPlayer GetTargetPlayer() => target;
    public int GetDamage() => damage;
    #endregion

    #region Constructor
    public DamagePlayerAction(IPlayer target, int damage) {
        this.target = target;
        this.damage = damage;
        Log($"Created DamagePlayerAction for {(target.IsPlayer1 ? "Player 1" : "Player 2")} (TargetID: {target.TargetId.ToUpper()}) with {damage} damage",
            LogTag.Actions | LogTag.Players);
    }
    #endregion

    #region Methods


    public override string ToString() {
        return $"DamagePlayerAction: Target={(target?.IsPlayer1 == true ? "Player 1" : "Player 2")} (TargetID: {target?.TargetId.ToUpper()}), Damage={damage}";
    }
    #endregion
}