using System.Collections.Generic;
using static Enums;

public class SpellData : CardData {
    public int spellPower;
    public TargetType defaultTargetType;

    public void OnEnable() {
        cardType = CardType.Spell;
    }
}