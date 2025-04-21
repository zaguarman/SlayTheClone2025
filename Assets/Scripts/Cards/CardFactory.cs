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

    public static async Task<CardController> CreateCardControllerAsync(ICard card, IPlayer owner, Transform parent, IGameMediator mediator, IGameReferences references, CancellationToken cancellationToken = default) {
        if (card == null || parent == null || mediator == null || references == null) return null;

        var cardPrefab = references.GetCardPrefab();
        if (cardPrefab == null) {
            LogError("Failed to get card prefab from game references", LogTag.Cards | LogTag.Initialization);
            return null;
        }

        // Add small delay to spread out card creation
        await Task.Delay(100, cancellationToken);

        var cardObj = GameObject.Instantiate(cardPrefab.gameObject, parent);
        var controller = cardObj.GetComponent<CardController>();
        if (controller != null) {
            var data = CreateCardData(card);

            // Only cast to ICreature if the card is actually a creature
            ICreature creature = card as ICreature;

            controller.Setup(data, owner, creature, mediator, references);
            Log($"Created card controller for {card.Name}", LogTag.Cards | LogTag.Initialization);
        }
        return controller;
    }

    public static CardController CreateCardController(ICard card, IPlayer owner, Transform parent, IGameMediator mediator, IGameReferences references) {
        if (card == null || parent == null || mediator == null || references == null) return null;

        var cardPrefab = references.GetCardPrefab();
        if (cardPrefab == null) {
            LogError("Failed to get card prefab from game references", LogTag.Cards | LogTag.Initialization);
            return null;
        }

        var cardObj = GameObject.Instantiate(cardPrefab.gameObject, parent);
        var controller = cardObj.GetComponent<CardController>();
        if (controller != null) {
            var data = CreateCardData(card);

            // Only cast to ICreature if the card is actually a creature
            ICreature creature = card as ICreature;

            controller.Setup(data, owner, creature, mediator, references);
            Log($"Created card controller for {card.Name}", LogTag.Cards | LogTag.Initialization);
        }
        return controller;
    }

    public static CardData CreateCardData(ICard card) {
        if (card == null) return null;

        // For creatures, copy ALL data including effects
        if (card is ICreature creature) {
            var creatureData = ScriptableObject.CreateInstance<CreatureData>();
            creatureData.cardId = card.CardId; // Copy the cardId
            creatureData.cardName = creature.Name;
            creatureData.description = creature.Description;
            creatureData.cardType = CardType.Creature; // Assuming creature type
            creatureData.attack = creature.BaseAttack; // Use BASE stats for data representation
            creatureData.health = creature.BaseHealth;
            creatureData.speed = creature.BaseSpeed;   // Use BASE speed
            creatureData.effects = creature.Effects.Select(e => new CardEffect {
                effectType = e.effectType,
                trigger = e.trigger,
                actions = e.actions.Select(a => new EffectAction {
                    actionType = a.actionType,
                    value = a.value, targetType = a.targetType, targetModifier = a.targetModifier,
                    modifyAttack = a.modifyAttack,
                    modifyHealth = a.modifyHealth,
                    modifySpeed = a.modifySpeed,
                    statusEffectToApply = a.statusEffectToApply, // Ensure these are copied
                    statusDuration = a.statusDuration,
                    statusPotency = a.statusPotency
                }).ToList() // Keep ToList() here as Select returns IEnumerable
            }).ToList();
            return creatureData;
        }

        // For spells, copy the data and effects
        if (card is Spell spell) {
            var spellData = ScriptableObject.CreateInstance<SpellData>();
            spellData.cardId = card.CardId;
            spellData.cardName = spell.Name;
            spellData.description = spell.Description;
            spellData.defaultTargetType = spell.DefaultTargetType;
            spellData.effects = spell.Effects.Select(e => new CardEffect {
                effectType = e.effectType,
                trigger = e.trigger,
                actions = e.actions.Select(a => new EffectAction {
                    actionType = a.actionType,
                    value = a.value, targetType = a.targetType, targetModifier = a.targetModifier,
                    modifyAttack = a.modifyAttack,
                    modifyHealth = a.modifyHealth,
                    modifySpeed = a.modifySpeed,
                    statusEffectToApply = a.statusEffectToApply, // Ensure these are copied
                    statusDuration = a.statusDuration,
                    statusPotency = a.statusPotency

                }).ToList()
            }).ToList();
            return spellData;
        }

        return null;
    }

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