using System.Collections.Generic;
using UnityEngine;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public static class CardFactory {
        public static ICard CreateCard(CardData cardData) {
            if (cardData == null) return null;

            Log($"Creating card: {cardData.cardName} with {cardData.effects.Count} effects", LogTag.Cards | LogTag.Initialization);

            Card card = null;
            switch (cardData) {
                case CreatureData creatureData:
                    var creature = new Creature(
                        creatureData.cardName,
                        creatureData.attack,
                        creatureData.health,
                        creatureData.speed,
                        creatureData.cardId);
                    foreach (var effect in cardData.effects) {
                        creature.Effects.Add(effect.Clone());
                    }
                    card = creature;
                    break;

                case SpellData spellData:
                    var spell = new Spell(
                        spellData.cardName,
                        spellData.defaultTargetType,
                        spellData.cardId);
                    ConfigureSpellActionsFromEffects(spell, spellData);
                    card = spell;
                    break;
            }

            if (card != null) {
                card.Description = cardData.description;
                card.SourceData = cardData;
            }
            return card;
        }

        private static void ConfigureSpellActionsFromEffects(Spell spell, SpellData spellData) {
            // Without explicit effects a spell just draws a card
            if (spellData.effects == null || spellData.effects.Count == 0) {
                spell.Effects.Add(new CardEffect {
                    effectType = EffectType.Immediate,
                    trigger = EffectTrigger.OnPlay,
                    actions = new List<EffectAction> {
                        new EffectAction {
                            actionType = ActionType.Draw,
                            value = 1,
                            targetType = TargetType.Player
                        }
                    }
                });
                return;
            }

            foreach (var effect in spellData.effects) {
                // Also add each action to the spell's action list for direct execution
                foreach (var action in effect.actions) {
                    if (action.actionType == ActionType.ModifyStat) {
                        spell.AddAction(action.actionType, action.value, action.targetType, action.modifyAttack, action.modifyHealth, action.modifySpeed, action.targetModifier);
                    }
                    else if (action.actionType == ActionType.ApplyStatus) {
                        spell.AddAction(action.actionType, action.targetType, action.statusEffectToApply, action.statusDuration, action.statusPotency, action.targetModifier);
                    }
                    else {
                        spell.AddAction(action.actionType, action.value, action.targetType, action.targetModifier);
                    }
                }
                spell.Effects.Add(effect.Clone());
            }
        }

        /// <summary>The definition a card instance was created from, looked up in its owner's deck if the card doesn't carry it.</summary>
        public static CardData FindCardData(ICard card, IPlayer owner) {
            if (card == null) return null;
            return card.SourceData ?? owner?.Deck?.FindOriginalCardDataById(card.CardId);
        }

        /// <summary>
        /// Builds throwaway data from a live card's printed values, for cards that have no definition (e.g. tokens).
        /// Effects can't be recovered this way.
        /// </summary>
        public static CardData CreateFallbackData(ICard card) {
            CardData data;
            switch (card) {
                case ICreature creature:
                    var creatureData = ScriptableObject.CreateInstance<CreatureData>();
                    creatureData.attack = creature.BaseAttack;
                    creatureData.health = creature.BaseHealth;
                    creatureData.speed = creature.BaseSpeed;
                    data = creatureData;
                    break;
                case Spell spell:
                    var spellData = ScriptableObject.CreateInstance<SpellData>();
                    spellData.defaultTargetType = spell.DefaultTargetType;
                    data = spellData;
                    break;
                default:
                    return null;
            }

            data.cardId = card.CardId;
            data.cardName = card.Name;
            data.description = card.Description;
            return data;
        }

        public static CardController CreateCardController(
            ICard cardInstance,
            CardData baseCardData,
            IPlayer owner,
            Transform parent,
            IGameMediator mediator,
            IGameReferences references,
            IGameManager manager) {
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
                controller.Setup(baseCardData, owner, cardInstance, mediator, references, manager);
                Log($"Created card controller for {baseCardData.cardName}", LogTag.Cards | LogTag.Initialization);
            }
            return controller;
        }
    }
}
