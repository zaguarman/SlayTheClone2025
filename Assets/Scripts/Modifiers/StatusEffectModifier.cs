using System;
using static DebugLogger;
using static Enums;
using UnityEngine.Events; // Required for UnityAction

// Represents status effects like Burned, Paralyzed, etc.
public class StatusEffectModifier : BaseModifier, ITimedModifier
{
    public StatusEffectType EffectType { get; }
    public int DurationInTurns { get; }
    public int TurnApplied { get; }
    public int Potency { get; } // e.g., Burn damage per turn

    // Store references to the listeners to allow unsubscribing
    private UnityAction<int> _turnEndListener;
    // Add other listener types if needed for other effects

    public StatusEffectModifier(string name, string description, StatusEffectType type, int duration, int potency, int currentTurn)
        : base(name, description)
    {
        EffectType = type;
        DurationInTurns = Math.Max(1, duration); // Ensure minimum 1 turn duration
        TurnApplied = currentTurn;
        Potency = potency;

        Log($"StatusEffectModifier '{Name}' created: Type={EffectType}, Duration={DurationInTurns}, Potency={Potency}, AppliedTurn={TurnApplied}", LogTag.Effects);
    }

    public bool HasExpired(int currentTurn)
    {
        // Same expiration logic as TimedStatModifier
        bool expired = currentTurn >= (TurnApplied + DurationInTurns);
        if (expired)
        {
            Log($"StatusEffect '{Name}' ({EffectType}) has expired on turn {currentTurn}.", LogTag.Effects | LogTag.Turns);
        }
        return expired;
    }

    public override void Apply(object target, IGameMediator mediator)
    {
        Log($"Applying StatusEffect '{Name}' ({EffectType}) to {target}", LogTag.Effects);
        // Subscribe to events based on the effect type
        switch (EffectType)
        {
            case StatusEffectType.Burned:
                // Subscribe to EndOfTurn event
                _turnEndListener = (turn) => HandleEndOfTurn(target, turn, mediator);
                mediator.AddTurnEndedListener(_turnEndListener);
                Log($"StatusEffect '{Name}' subscribed to TurnEnded.", LogTag.Effects);
                break;

            case StatusEffectType.Paralyzed:
                // Paralyzed doesn't need event subscription for its primary effect (preventing actions).
                // The check happens externally. We might subscribe to ActionAttempted later if needed.
                Log($"StatusEffect '{Name}' ({EffectType}) applied. Action prevention handled externally.", LogTag.Effects);
                break;
                // Add cases for other status effects
        }
    }

    public override void Remove(object target, IGameMediator mediator)
    {
        Log($"Removing StatusEffect '{Name}' ({EffectType}) from {target}", LogTag.Effects);
        // Unsubscribe from events
        switch (EffectType)
        {
            case StatusEffectType.Burned:
                if (_turnEndListener != null)
                {
                    mediator.RemoveTurnEndedListener(_turnEndListener);
                    Log($"StatusEffect '{Name}' unsubscribed from TurnEnded.", LogTag.Effects);
                    _turnEndListener = null; // Clear reference
                }
                break;

            case StatusEffectType.Paralyzed:
                // No event listeners to remove for the core effect.
                break;
                // Add cases for other status effects
        }
    }

    // --- Specific Effect Logic ---

    private void HandleEndOfTurn(object target, int turnNumber, IGameMediator mediator)
    {
        // Check if this modifier is still active for the target (sanity check)
        if (target is not Creature creatureTarget) return;

        // We don't need to check if the modifier is still active
        // The ModifierManager will handle removing expired modifiers
        // and unsubscribing their event handlers


        Log($"StatusEffect '{Name}' ({EffectType}) responding to EndOfTurn {turnNumber} for {creatureTarget.Name}", LogTag.Effects | LogTag.Turns);

        if (EffectType == StatusEffectType.Burned && Potency > 0)
        {
            Log($"Applying Burn damage ({Potency}) to {creatureTarget.Name} from StatusEffect '{Name}'", LogTag.Effects | LogTag.Creatures | LogTag.Actions);
            // Queue the damage action. Source can be the creature itself or null.
            var damageAction = new DamageCreatureAction(creatureTarget, Potency, creatureTarget);

            // Use the mediator to notify that an action should be queued
            // This is a workaround since we don't have direct access to ActionsQueue
            // The mediator can broadcast this, and ActionsQueue can listen for it
            mediator.NotifyActionsQueueChanged();

            // Note: This is not ideal - we should pass the action to be queued
            // A better approach would be to add a NotifyActionQueued method to IGameMediator
            // that takes an IGameAction parameter
        }
        // Add logic for other end-of-turn status effects here
    }

    // --- Query Methods for External Checks ---

    public bool PreventsActions()
    {
        return EffectType == StatusEffectType.Paralyzed || EffectType == StatusEffectType.Frozen;
    }

    // --- IModifier Implementation ---

    public override bool TryGetStatModification(ModifiableStat stat, out ModifierCalculationType calcType, out int value)
    {
        // Status effects typically don't directly modify stats in this way
        calcType = ModifierCalculationType.Flat;
        value = 0;
        return false;
    }

    public override bool HandlesEvent(EffectTrigger eventType)
    {
        // Declare which events this modifier *might* react to
        switch (EffectType)
        {
            case StatusEffectType.Burned:
                return eventType == EffectTrigger.EndOfTurn; // Listens to TurnEnded via GameMediator
            // Paralyzed/Frozen are checked externally, don't need to "handle" an event here for that.
            // Poisoned might listen to EndOfTurn or StartOfTurn
            default:
                return false;
        }
    }

     public override string ToString() =>
        $"{base.ToString()} - Type: {EffectType}, Duration: {DurationInTurns}, Potency: {Potency}";
}
