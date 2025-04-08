using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class PlaySpellAction : IGameAction {
    private readonly Spell spell;
    private readonly IPlayer owner;
    private readonly ITarget target;

    public PlaySpellAction(Spell spell, IPlayer owner, ITarget target = null) {
        this.spell = spell;
        this.owner = owner;
        this.target = target;
        Log($"Created PlaySpellAction for {spell.Name} (TargetID: {spell.TargetId.ToUpper()})", LogTag.Actions | LogTag.Cards);
    }

    public void Execute() {
        if (spell == null || owner == null) {
            LogError("Cannot execute PlaySpellAction - spell or owner is null", LogTag.Actions);
            return;
        }

        // Discard the spell from hand if it hasn't been already
        if (owner.Hand.Contains(spell)) {
            owner.DiscardCard(spell);
        }

        // Process spell effects
        spell.Play(owner, GameManager.Instance.ActionsQueue, target);

        Log($"Executed PlaySpellAction for {spell.Name} (TargetID: {spell.TargetId.ToUpper()})", LogTag.Actions | LogTag.Cards);
    }

    public override string ToString() {
        return $"PlaySpellAction: Spell={spell?.Name} (TargetID: {spell?.TargetId.ToUpper()}), Owner={(owner?.IsPlayer1() == true ? "Player 1" : "Player 2")} (TargetID: {owner?.TargetId.ToUpper()}), Target={target?.TargetId.ToUpper()}";
    }
} 