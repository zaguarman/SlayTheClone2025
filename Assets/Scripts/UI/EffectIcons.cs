using static Enums;

/// <summary>
/// Names of the icons in the effect icon sprite asset (Assets/Art/Icons/EffectIcons.asset, TMP's default sprite asset),
/// so any TextMeshPro text can show them inline. Built by Cards > Build Effect Icons.
/// </summary>
public static class EffectIcons {
    // Triggers
    public const string Enter = "enter";
    public const string Death = "death";
    public const string Attack = "attack";
    public const string Hurt = "hurt";
    public const string Dawn = "dawn";
    public const string Dusk = "dusk";

    // Effects
    public const string Damage = "damage";
    public const string Heal = "heal";
    public const string Draw = "draw";
    public const string Summon = "summon";
    public const string Armor = "armor";
    public const string Paralyze = "paralyze";
    public const string Buff = "buff";
    public const string Debuff = "debuff";
    public const string Burn = "burn";
    public const string Poison = "poison";
    public const string Freeze = "freeze";
    public const string Chain = "chain";

    // Stats
    public const string Speed = "speed";
    public const string Sword = "sword";
    public const string Heart = "heart";

    public static string Tag(string icon) => string.IsNullOrEmpty(icon) ? "" : $"<sprite name=\"{icon}\">";

    public static string ForStatus(StatusEffectType status) {
        switch (status) {
            case StatusEffectType.Paralyzed: return Paralyze;
            case StatusEffectType.Frozen: return Freeze;
            case StatusEffectType.Burned: return Burn;
            case StatusEffectType.Poisoned: return Poison;
            default: return null;
        }
    }
}
