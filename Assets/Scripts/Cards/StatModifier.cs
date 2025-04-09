using UnityEngine;

public interface StatModifier {
    int Apply(int baseValue);
    ModifierType Type { get; }
    int Value { get; }
}

public enum ModifierType {
    Flat,
    Percentage
}

public class FlatModifier : StatModifier {
    private int amount;
    
    public FlatModifier(int amount) {
        this.amount = amount;
    }
    
    public int Apply(int baseValue) {
        return baseValue + amount;
    }

    public ModifierType Type => ModifierType.Flat;
    public int Value => amount;
}

public class PercentageModifier : StatModifier {
    private float percent;
    
    public PercentageModifier(float percent) {
        this.percent = percent;
    }
    
    public int Apply(int baseValue) {
        return Mathf.RoundToInt(baseValue * (1 + percent));
    }

    public ModifierType Type => ModifierType.Percentage;
    public int Value => Mathf.RoundToInt(percent * 100);
}
