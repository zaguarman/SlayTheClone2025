using static Zeitgeist.Enums;

namespace Zeitgeist {
    public class SpellData : CardData {
        public TargetType defaultTargetType;

        public override void OnEnable() {
            base.OnEnable(); // Assigns the cardId
            cardType = CardType.Spell;
        }
    }
}
