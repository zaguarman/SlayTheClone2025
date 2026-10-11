using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class Spell : Card {
        public TargetType DefaultTargetType { get; }

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
