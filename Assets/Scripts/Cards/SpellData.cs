using static Enums;

public class SpellData : CardData {
    public TargetType defaultTargetType;

    public override void OnEnable() {
        cardType = CardType.Spell;
    }
}