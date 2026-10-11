using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class SpellData : CardData {
        public TargetType defaultTargetType;

        public override void OnEnable() {
            base.OnEnable(); // Assigns the cardId
            cardType = CardType.Spell;
        }
    }
}
