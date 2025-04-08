
public class Enums {
    public enum CardType { Creature, Spell, Enchantment }
    public enum EffectType { Immediate, Triggered, Continuous }
    public enum EffectTrigger { OnPlay, OnDeath, OnDamage, StartOfTurn, EndOfTurn }
    public enum ActionType { Damage, Heal, Draw, Summon, Armor, Stun, Buff }
    public enum TargetType { Player, Enemy, AllCreatures, FriendlyCreatures, EnemyCreatures, Self, RandomAlly }
}
