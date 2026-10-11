using System;
using UnityEngine.Events;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    // A modifier that listens to a GameMediator event while it is applied
    public class EventSubscriberModifier : BaseModifier {
        public EffectTrigger EventToListenTo { get; }
        private readonly object listenerAction; // The listener delegate; its type depends on the event

        public EventSubscriberModifier(string name, string description, EffectTrigger trigger, UnityAction<ICreature, int> action)
            : base(name, description) {
            if (trigger != EffectTrigger.OnDamage)
                throw new ArgumentException("This constructor is for OnDamage trigger only.", nameof(trigger));
            EventToListenTo = trigger;
            listenerAction = action ?? throw new ArgumentNullException(nameof(action));
            Log($"EventSubscriberModifier '{Name}' details: Listens for {trigger}", LogTag.Effects);
        }

        public override void Apply(object target, IGameMediator mediator) {
            Log($"EventSubscriberModifier '{Name}' applying (subscribing) to {EventToListenTo} for target {target}", LogTag.Effects);
            SubscribeToEvent(mediator, true);
        }

        public override void Remove(object target, IGameMediator mediator) {
            Log($"EventSubscriberModifier '{Name}' removing (unsubscribing) from {EventToListenTo} for target {target}", LogTag.Effects);
            SubscribeToEvent(mediator, false);
        }

        private void SubscribeToEvent(IGameMediator mediator, bool subscribe) {
            try {
                switch (EventToListenTo) {
                    case EffectTrigger.OnDamage:
                        Toggle<UnityAction<ICreature, int>>(subscribe, mediator.AddCreatureDamagedListener, mediator.RemoveCreatureDamagedListener);
                        break;
                    case EffectTrigger.StartOfTurn:
                    case EffectTrigger.EndOfTurn: // TurnEnded covers both for now
                        Toggle<UnityAction<int>>(subscribe, mediator.AddTurnEndedListener, mediator.RemoveTurnEndedListener);
                        break;
                    case EffectTrigger.OnPlay: // A creature entering play
                        Toggle<UnityAction<ICreature, IPlayer>>(subscribe, mediator.AddCreatureSummonedListener, mediator.RemoveCreatureSummonedListener);
                        break;
                    case EffectTrigger.OnDeath:
                        Toggle<UnityAction<ICreature>>(subscribe, mediator.AddCreatureDiedListener, mediator.RemoveCreatureDiedListener);
                        break;
                    default:
                        LogWarning($"Event type {EventToListenTo} not mapped in EventSubscriberModifier.", LogTag.Effects);
                        break;
                }
            }
            catch (Exception ex) {
                LogError($"Error {(subscribe ? "subscribing" : "unsubscribing")} event {EventToListenTo}: {ex.Message}", LogTag.Effects);
            }
        }

        private void Toggle<TListener>(bool subscribe, Action<TListener> add, Action<TListener> remove) where TListener : class {
            if (!(listenerAction is TListener listener)) {
                LogError($"Action type mismatch for {EventToListenTo} event.", LogTag.Effects);
                return;
            }

            if (subscribe) add(listener);
            else remove(listener);
        }

        public override bool HandlesEvent(EffectTrigger eventType) {
            return eventType == EventToListenTo;
        }
    }
}
