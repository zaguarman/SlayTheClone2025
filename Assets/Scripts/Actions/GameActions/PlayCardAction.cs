using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class PlayCardAction : IGameAction {
    private readonly ICard card;
    private readonly IPlayer owner;
    private readonly ITarget target;

    public PlayCardAction(ICard card, IPlayer owner, ITarget target) {
        this.card = card;
        this.owner = owner;
        this.target = target;
        Log($"Created PlayCardAction for {card?.Name} (TargetID: {card?.TargetId.ToUpper()}) targeting {target?.TargetId.ToUpper()}",
            LogTag.Actions | LogTag.Cards);
    }

    public void Execute() {
        if (card == null || owner == null) {
            LogError("Cannot execute PlayCardAction - card or owner is null", LogTag.Actions);
            return;
        }

        // Check if the card is in the player's hand
        if (!owner.Hand.Contains(card)) {
            LogError($"Cannot play card - {card.Name} is not in {owner.TargetId}'s hand", LogTag.Actions);
            return;
        }

        // Remove the card from hand
        owner.DiscardCard(card);

        // Play the card based on its type
        if (card is Spell spell) {
            GameManager.Instance.ActionsQueue.AddAction(new PlaySpellAction(spell, owner, target));
        } else if (card is ICreature creature) {
            GameManager.Instance.ActionsQueue.AddAction(new SummonCreatureAction(creature, owner, target));
        }

        Log($"Executed PlayCardAction for {card.Name} (TargetID: {card.TargetId.ToUpper()})", LogTag.Actions | LogTag.Cards);
    }

    public override string ToString() {
        return $"PlayCardAction: Card={card?.Name} (TargetID: {card?.TargetId.ToUpper()}), Owner={(owner?.IsPlayer1() == true ? "Player 1" : "Player 2")} (TargetID: {owner?.TargetId.ToUpper()}), Target={target?.TargetId.ToUpper()}";
    }
} 