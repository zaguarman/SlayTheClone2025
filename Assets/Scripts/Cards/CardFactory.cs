using System.Collections.Generic;
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
                        creatureData.cardId,
                        creatureData.turnsUntilOperative);
                    card = creature;
                    break;

                case SpellData spellData:
                    var spell = new Spell(
                        spellData.cardName,
                        spellData.defaultTargetType,
                        spellData.cardId);
                    card = spell;
                    break;
            }

            if (card != null) {
                // Each instance owns its effects, so changing them never touches the definition
                foreach (var effect in cardData.effects) {
                    card.Effects.Add(effect.Clone());
                }
                if (card is Spell && card.Effects.Count == 0) {
                    card.Effects.Add(DrawOneCard()); // Without explicit effects a spell just draws a card
                }
                card.Description = cardData.description;
                card.SourceData = cardData;
            }
            return card;
        }

        private static CardEffect DrawOneCard() {
            return new CardEffect {
                effectType = EffectType.Immediate,
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectAction> {
                    new EffectAction { actionType = ActionType.Draw, value = 1, targetType = TargetType.Player }
                }
            };
        }
    }
}
