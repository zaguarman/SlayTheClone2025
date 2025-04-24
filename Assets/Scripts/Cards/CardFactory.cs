using static DebugLogger;
using System.Linq;
using System;
using UnityEngine.Events;
using UnityEngine;
using static Enums;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

public static class CardFactory {
    public static ICard CreateCard(CardData cardData) {
        if (cardData == null) return null;

        Log($"Creating card: {cardData.cardName} with {cardData.effects.Count} effects", LogTag.Cards | LogTag.Initialization);

        ICard card = null;
        switch (cardData) {
            case CreatureData creatureData:
                var creature = new Creature(
                    creatureData.cardName,
                    creatureData.attack,
                    creatureData.health,
                    creatureData.speed, // Pass speed
                    creatureData.cardId); // Pass cardId

                // Copy over the description
                creature.Description = creatureData.description;

                foreach (var effect in cardData.effects) {
                    var newEffect = new CardEffect {
                        effectType = effect.effectType,
                        trigger = effect.trigger,
                        actions = new List<EffectAction>() // Create a new list
                    };
                    foreach (var a in effect.actions) {
                        newEffect.actions.Add(new EffectAction { // Add actions one by one
                            actionType = a.actionType,
                            value = a.value,
                            targetType = a.targetType,
                            targetModifier = a.targetModifier,
                            modifyAttack = a.modifyAttack, modifyHealth = a.modifyHealth, modifySpeed = a.modifySpeed, // Copy modify flags
                            statusEffectToApply = a.statusEffectToApply, statusDuration = a.statusDuration, statusPotency = a.statusPotency // Copy status fields
                        });
                    }
                    creature.Effects.Add(newEffect);
                }
                card = creature;
                break;

            case SpellData spellData:
                var spell = new Spell(
                    spellData.cardName,
                    spellData.defaultTargetType,
                    spellData.cardId); // Pass cardId

                // Copy over the description
                spell.Description = spellData.description;

                // Configure spell actions based on the spell data's effects
                ConfigureSpellActionsFromEffects(spell, spellData);

                card = spell;
                break;
        }

        return card;
    }

    private static void ConfigureSpellActionsFromEffects(Spell spell, SpellData spellData) {
        // If there are explicit effects defined, use those
        if (spellData.effects != null && spellData.effects.Count > 0) {
            foreach (var effect in spellData.effects) {
                var newEffect = new CardEffect {
                    effectType = effect.effectType, trigger = effect.trigger, actions = new List<EffectAction>()
                };

                foreach (var action in effect.actions) {
                    var newAction = new EffectAction {
                        actionType = action.actionType,
                        value = action.value, targetType = action.targetType, targetModifier = action.targetModifier,
                        modifyAttack = action.modifyAttack,
                        modifyHealth = action.modifyHealth,
                        modifySpeed = action.modifySpeed,
                        statusEffectToApply = action.statusEffectToApply,
                        statusDuration = action.statusDuration,
                        statusPotency = action.statusPotency
                    };
                    newEffect.actions.Add(newAction);

                    // Also add the action to the spell's action list for direct execution
                    if (action.actionType == ActionType.ModifyStat) { // Changed from Buff
                        spell.AddAction(action.actionType, action.value, action.targetType, action.modifyAttack, action.modifyHealth, action.modifySpeed, action.targetModifier);
                    } else if (action.actionType == ActionType.ApplyStatus) {
                        spell.AddAction(action.actionType, action.targetType, action.statusEffectToApply, action.statusDuration, action.statusPotency, action.targetModifier);
                    } else { // Handle simple actions (Damage, Heal, Draw, etc.)
                         spell.AddAction(action.actionType, action.value, action.targetType, action.targetModifier);
                    }
                }

                spell.Effects.Add(newEffect);
            }
        }
        // Otherwise fall back to simple draw action
        else {
            var defaultEffect = new CardEffect {
                effectType = EffectType.Immediate,
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectAction> {
                    new EffectAction {
                        actionType = ActionType.Draw,
                        value = 1,
                        targetType = TargetType.Player
                    }
                }
            };
            spell.Effects.Add(defaultEffect);
        }
    }

    public static async Task<CardController> CreateCardControllerAsync(ICard cardInstance, CardData baseCardData, IPlayer owner, Transform parent, IGameMediator mediator, IGameReferences references, CancellationToken cancellationToken = default) {
        if (baseCardData == null || parent == null || mediator == null || references == null) return null;

        var cardPrefab = references.GetCardPrefab();
        if (cardPrefab == null) {
            LogError("Failed to get card prefab from game references", LogTag.Cards | LogTag.Initialization);
            return null;
        }

        // Add small delay to spread out card creation
        await Task.Delay(50, cancellationToken); // Reduced delay

        var cardObj = GameObject.Instantiate(cardPrefab.gameObject, parent);
        var controller = cardObj.GetComponent<CardController>();
        if (controller != null) {
            // Pass the base CardData and the potentially live ICard instance
            controller.Setup(baseCardData, owner, cardInstance, mediator, references);
            Log($"Created card controller for {baseCardData.cardName}", LogTag.Cards | LogTag.Initialization);
        }
        return controller;
    }

    public static CardController CreateCardController(ICard cardInstance, CardData baseCardData, IPlayer owner, Transform parent, IGameMediator mediator, IGameReferences references) {
        if (baseCardData == null || parent == null || mediator == null || references == null) return null;

        var cardPrefab = references.GetCardPrefab();
        if (cardPrefab == null) {
            LogError("Failed to get card prefab from game references", LogTag.Cards | LogTag.Initialization);
            return null;
        }

        var cardObj = GameObject.Instantiate(cardPrefab.gameObject, parent);
        var controller = cardObj.GetComponent<CardController>();
        if (controller != null) {
            // Pass the base CardData and the potentially live ICard instance
            controller.Setup(baseCardData, owner, cardInstance, mediator, references);
            Log($"Created card controller for {baseCardData.cardName}", LogTag.Cards | LogTag.Initialization);
        }
        return controller;
    }

    // CreateCardData method has been removed to ensure clear separation between
    // WYSIWYG display (showing current state of cards in play/hand) and
    // reliable restoration (resetting cards to original state when discarded/returned to deck)

    // TODO
    public static void SetupCardEventHandlers(
        CardController controller,
        UnityAction<CardController> onBeginDrag = null,
        UnityAction<CardController> onEndDrag = null,
        UnityAction<CardController> onDrop = null,
        Action onPointerEnter = null,
        Action onPointerExit = null) {
        if (controller == null) return;

        CleanupCardEventHandlers(controller);

        if (onBeginDrag != null)
            controller.OnBeginDragEvent.AddListener(onBeginDrag);

        if (onEndDrag != null)
            controller.OnEndDragEvent.AddListener(onEndDrag);

        if (onDrop != null)
            controller.OnCardDropped.AddListener(onDrop);

        if (onPointerEnter != null)
            controller.OnPointerEnterHandler += onPointerEnter;

        if (onPointerExit != null)
            controller.OnPointerExitHandler += onPointerExit;

        Log($"Set up event handlers for card {controller.name}", LogTag.Cards | LogTag.UI);
    }

    public static void CleanupCardEventHandlers(CardController controller) {
        if (controller == null) return;

        controller.OnBeginDragEvent.RemoveAllListeners();
        controller.OnEndDragEvent.RemoveAllListeners();
        controller.OnCardDropped.RemoveAllListeners();
        controller.OnPointerEnterHandler = null;
        controller.OnPointerExitHandler = null;

        Log($"Cleaned up event handlers for card {controller.name}", LogTag.Cards | LogTag.UI);
    }

    // Helper to deep copy effects list
    private static List<CardEffect> CopyEffectsList(List<CardEffect> originalEffects)
    {
        if (originalEffects == null) return new List<CardEffect>();

        List<CardEffect> effectsCopy = new List<CardEffect>(originalEffects.Count);
        foreach (var effectData in originalEffects)
        {
            var newEffect = new CardEffect
            {
                effectType = effectData.effectType,
                trigger = effectData.trigger,
                actions = new List<EffectAction>()
            };
            if (effectData.actions != null)
            {
                foreach (var actionData in effectData.actions)
                {
                    newEffect.actions.Add(new EffectAction
                    {
                        actionType = actionData.actionType,
                        value = actionData.value,
                        targetType = actionData.targetType,
                        targetModifier = actionData.targetModifier,
                        modifyAttack = actionData.modifyAttack,
                        modifyHealth = actionData.modifyHealth,
                        modifySpeed = actionData.modifySpeed,
                        statusEffectToApply = actionData.statusEffectToApply,
                        statusDuration = actionData.statusDuration,
                        statusPotency = actionData.statusPotency
                    });
                }
            }
            effectsCopy.Add(newEffect);
        }
        return effectsCopy;
    }
}