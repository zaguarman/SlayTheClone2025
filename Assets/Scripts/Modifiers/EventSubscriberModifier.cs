using System;
using UnityEngine.Events;
using static DebugLogger;
using static Enums; // Assuming your Enums class is accessible

// Represents a modifier that listens to game events and executes an action
// Note: This needs careful adaptation to your specific GameMediator events
public class EventSubscriberModifier : BaseModifier
{
    public EffectTrigger EventToListenTo { get; }
    private readonly object listenerAction; // Store the specific listener delegate

    // Example constructor for CreatureDamaged event
    public EventSubscriberModifier(string name, string description, EffectTrigger trigger, UnityAction<ICreature, int> action)
        : base(name, description)
    {
        if (trigger != EffectTrigger.OnDamage)
            throw new ArgumentException("This constructor is for OnDamage trigger only.", nameof(trigger));
        EventToListenTo = trigger;
        listenerAction = action ?? throw new ArgumentNullException(nameof(action));
         Log($"EventSubscriberModifier '{Name}' details: Listens for {trigger}", LogTag.Effects);
    }

     // Add other constructors for different event types (e.g., TurnEnded, OnPlay)
     // public EventSubscriberModifier(string name, string description, EffectTrigger trigger, UnityAction<int> turnEndAction) ...
     // public EventSubscriberModifier(string name, string description, EffectTrigger trigger, UnityAction<ICreature, IPlayer> summonAction) ...

    public override void Apply(object target, GameMediator mediator)
    {
        Log($"EventSubscriberModifier '{Name}' applying (subscribing) to {EventToListenTo} for target {target}", LogTag.Effects);
        SubscribeToEvent(mediator, true);
    }

    public override void Remove(object target, GameMediator mediator)
    {
        Log($"EventSubscriberModifier '{Name}' removing (unsubscribing) from {EventToListenTo} for target {target}", LogTag.Effects);
        SubscribeToEvent(mediator, false);
    }

    private void SubscribeToEvent(GameMediator mediator, bool subscribe)
    {
        try
        {
            // Map EffectTrigger to the correct GameMediator event
            switch (EventToListenTo)
            {
                case EffectTrigger.OnDamage:
                    var creatureDamageAction = listenerAction as UnityAction<ICreature, int>;
                    if (creatureDamageAction != null)
                    {
                        if (subscribe) mediator.AddCreatureDamagedListener(creatureDamageAction);
                        else mediator.RemoveCreatureDamagedListener(creatureDamageAction);
                    }
                    else LogError($"Action type mismatch for {EventToListenTo} event.", LogTag.Effects);
                    break;

                // Add cases for other EffectTriggers mapped to GameMediator events
                 case EffectTrigger.StartOfTurn:
                 case EffectTrigger.EndOfTurn:
                     // Example: Assuming TurnEnded covers both for now
                     var turnEndAction = listenerAction as UnityAction<int>;
                     if (turnEndAction != null) {
                         if (subscribe) mediator.AddTurnEndedListener(turnEndAction);
                         else mediator.RemoveTurnEndedListener(turnEndAction);
                     } else LogError($"Action type mismatch for {EventToListenTo} event.", LogTag.Effects);
                     break;

                 case EffectTrigger.OnPlay: // Assuming OnPlay corresponds to CreatureSummoned
                    var summonAction = listenerAction as UnityAction<ICreature, IPlayer>;
                    if (summonAction != null) {
                        if (subscribe) mediator.AddCreatureSummonedListener(summonAction);
                        else mediator.RemoveCreatureSummonedListener(summonAction);
                    } else LogError($"Action type mismatch for {EventToListenTo} event.", LogTag.Effects);
                     break;

                 case EffectTrigger.OnDeath:
                    var deathAction = listenerAction as UnityAction<ICreature>;
                     if (deathAction != null) {
                         if (subscribe) mediator.AddCreatureDiedListener(deathAction);
                         else mediator.RemoveCreatureDiedListener(deathAction);
                     } else LogError($"Action type mismatch for {EventToListenTo} event.", LogTag.Effects);
                     break;

                default:
                    LogWarning($"Event type {EventToListenTo} not mapped in EventSubscriberModifier.", LogTag.Effects);
                    break;
            }
        }
        catch (Exception ex)
        {
            LogError($"Error {(subscribe ? "subscribing" : "unsubscribing")} event {EventToListenTo}: {ex.Message}", LogTag.Effects);
        }
    }

    public override bool HandlesEvent(EffectTrigger eventType)
    {
        return eventType == EventToListenTo;
    }
}
