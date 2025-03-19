using static Enums;

public class SpellData : CardData {
    public TargetType defaultTargetType;

    public void OnEnable() {
        cardType = CardType.Spell;
    }
}