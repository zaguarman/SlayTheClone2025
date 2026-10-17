using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    public class PlaySpellAction : IGameAction {
        private readonly Spell spell;
        private readonly IPlayer owner;
        private readonly ITarget target;

        public Spell GetSpell() => spell;
        public IPlayer GetOwner() => owner;
        public ITarget GetTarget() => target;

        public PlaySpellAction(Spell spell, IPlayer owner, ITarget target = null) {
            this.spell = spell;
            this.owner = owner;
            this.target = target;
            Log($"Created {this}", LogTag.Actions | LogTag.Cards);
        }

        public override string ToString() {
            return $"PlaySpellAction: Spell={spell?.Name} (TargetID: {spell?.TargetId.ToUpper()}), Owner={owner?.Name} (TargetID: {owner?.TargetId.ToUpper()}), Target={target?.TargetId.ToUpper()}";
        }
    }
}
