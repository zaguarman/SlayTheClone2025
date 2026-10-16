using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class Spell : Card {
        public TargetType DefaultTargetType { get; }
        /// <summary>Ripening: the Dawns it has ripened in the hand (its numbers are this much higher); 0 for a fresh copy.</summary>
        public int Ripeness { get; set; }

        public Spell(string name, TargetType defaultTargetType, string cardId = null) : base(name, cardId) {
            DefaultTargetType = defaultTargetType;
        }

        // What the spell does is its Effects, interpreted like any other card's (see EffectResolver)
        public override void Play(IPlayer owner, IActionsQueue context, ITarget target = null) {
            Log($"Playing spell {Name} (TargetID: {TargetId.ShortId()}) with {Effects.Count} effects", LogTag.Cards | LogTag.Actions);
            EffectResolver.QueueSpellEffects(this, owner, target, context);
        }
    }
}
