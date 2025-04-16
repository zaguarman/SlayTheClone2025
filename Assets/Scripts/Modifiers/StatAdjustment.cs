using Enums;

[System.Serializable]
public class StatAdjustment {
    public StatType statType;
    public StatAdjustmentType adjustmentType = StatAdjustmentType.Flat; // Default to Flat
    public float value;
}
