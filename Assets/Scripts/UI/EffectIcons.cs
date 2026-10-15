using static SlayTheClone.Enums;

namespace SlayTheClone {
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
        public const string Chain = "chain";
        public const string Doom = "doom";
        public const string Bless = "bless";
        public const string Cleanse = "cleanse";
        public const string Heat = "heat";
        public const string Conceal = "conceal";
        public const string Reveal = "reveal";
        public const string Encrypt = "encrypt";
        public const string Days = "days"; // A sand watch: how many Days something lasts

        // Stats
        public const string Speed = "speed";
        public const string Sword = "sword";
        public const string Heart = "heart";

        public static string Tag(string icon) => string.IsNullOrEmpty(icon) ? "" : $"<sprite name=\"{icon}\">";

        public static string ForStatus(StatusEffectType status) {
            switch (status) {
                case StatusEffectType.Exhausted: return Paralyze;
                case StatusEffectType.Burned: return Burn;
                case StatusEffectType.Doomed: return Doom;
                case StatusEffectType.Blessed: return Bless;
                case StatusEffectType.HeatSignature: return Heat;
                case StatusEffectType.Concealed: return Conceal;
                case StatusEffectType.Revealed: return Reveal;
                case StatusEffectType.Encrypted: return Encrypt;
                case StatusEffectType.Untouchable: return Armor;
                case StatusEffectType.Cooldown: return Days;
                case StatusEffectType.Brainwashed: return Hurt;
                case StatusEffectType.KillOrder: return Death;
                case StatusEffectType.BorrowedTime: return Days;
                case StatusEffectType.Licensed: return Attack;
                case StatusEffectType.Composed: return Bless;
                case StatusEffectType.Blackout: return Debuff;
                case StatusEffectType.Survivor: return Heart;
                default: return null;
            }
        }
    }
}
