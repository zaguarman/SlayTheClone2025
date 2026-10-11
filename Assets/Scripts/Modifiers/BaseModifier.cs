using System;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    // Common base for modifiers: identity, name and the default "affects nothing" answers
    public abstract class BaseModifier : IModifier {
        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; protected set; }
        public string Description { get; protected set; }

        protected BaseModifier(string name, string description) {
            Name = name;
            Description = description;
            Log($"Modifier '{Name}' created (ID: {Id.ToString().ShortId()})", LogTag.Effects);
        }

        public abstract void Apply(object target, IGameMediator mediator);
        public abstract void Remove(object target, IGameMediator mediator);

        public virtual bool TryGetStatModification(ModifiableStat stat, out ModifierCalculationType calcType, out int value) {
            calcType = ModifierCalculationType.Flat;
            value = 0;
            return false;
        }

        public virtual bool HandlesEvent(EffectTrigger eventType) => false;

        public override string ToString() => $"{Name} (ID: {Id.ToString().ShortId()}) - {Description}";
    }
}
