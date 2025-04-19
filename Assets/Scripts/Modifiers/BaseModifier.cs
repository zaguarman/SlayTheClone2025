using System;
using static Enums;
using static DebugLogger;

// Optional base class for common modifier implementation
public abstract class BaseModifier : IModifier
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; protected set; }
    public string Description { get; protected set; }

    protected BaseModifier(string name, string description)
    {
        Name = name;
        Description = description;
        Log($"Modifier '{Name}' created (ID: {Id.ToString().ToUpper().Substring(0, 8)})", LogTag.Effects);
    }

    public abstract void Apply(object target, GameMediator mediator);
    public abstract void Remove(object target, GameMediator mediator);

    public virtual bool TryGetStatModification(ModifiableStat stat, out ModifierCalculationType calcType, out int value)
    {
        calcType = ModifierCalculationType.Flat; // Default
        value = 0;
        return false; // Default: no stat modification
    }

    public virtual bool HandlesEvent(EffectTrigger eventType) => false; // Default: no event handling

    public override string ToString() => $"{Name} (ID: {Id.ToString().ToUpper().Substring(0, 8)}) - {Description}";
}
