using System;
using static DebugLogger;
using static Enums;

// Represents a permanent Armor modifier that doesn't expire
public class PermanentArmorModifier : ArmorModifier
{
    public PermanentArmorModifier(string name, string description, int value)
        : base(name, description, value)
    {
        Log($"PermanentArmorModifier '{Name}' created: Value={Value}", LogTag.Effects);
    }

    public override string ToString() => $"{base.ToString()} - Permanent";
}
