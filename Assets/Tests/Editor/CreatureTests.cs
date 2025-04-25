using NUnit.Framework;
using static Enums; // Assuming Enums are accessible

public class CreatureTests {
    private Creature testCreature;

    [SetUp]
    public void Setup() {
        // Create a basic creature for each test
        testCreature = new Creature("Test Dummy", 5, 10, 2, System.Guid.NewGuid().ToString());
        // Initialize stats (normally done by ModifierManager)
        testCreature.UpdateEffectiveStats(testCreature.BaseAttack, testCreature.BaseHealth, testCreature.BaseSpeed);
    }

    [Test]
    public void Creature_TakeHealthDamage_ReducesHealthCorrectly() {
        // Arrange
        int initialHealth = testCreature.Health;
        int damageAmount = 3;
        int expectedHealth = initialHealth - damageAmount;

        // Act
        int actualDamageDealt = testCreature.TakeHealthDamage(damageAmount);

        // Assert
        Assert.AreEqual(damageAmount, actualDamageDealt);
        Assert.AreEqual(expectedHealth, testCreature.Health);
        Assert.IsFalse(testCreature.IsDead);
    }

    [Test]
    public void Creature_TakeHealthDamage_OnDeadCreature_DealsZeroDamage() {
        // Arrange
        testCreature.MarkAsDead(); // Mark as dead first
        int initialHealth = testCreature.Health; // Should be 0 if MarkAsDead worked correctly
        int damageAmount = 5;

        // Act
        int actualDamageDealt = testCreature.TakeHealthDamage(damageAmount);

        // Assert
        Assert.AreEqual(0, actualDamageDealt);
        Assert.AreEqual(initialHealth, testCreature.Health); // Health remains unchanged
        Assert.IsTrue(testCreature.IsDead);
    }

    [Test]
    public void Creature_Heal_RestoresHealthCorrectly() {
        // Arrange
        testCreature.TakeHealthDamage(5); // Damage it first
        int healthBeforeHeal = testCreature.Health;
        int healAmount = 3;
        int expectedHealth = healthBeforeHeal + healAmount;

        // Act
        int actualHeal = testCreature.Heal(healAmount);

        // Assert
        Assert.AreEqual(healAmount, actualHeal);
        Assert.AreEqual(expectedHealth, testCreature.Health);
    }

    [Test]
    public void Creature_Heal_ClampsAtMaxHealth() {
        // Arrange
        testCreature.TakeHealthDamage(2); // Damage slightly
        int healthBeforeHeal = testCreature.Health;
        int healAmount = 5; // More than needed
        int expectedHealth = testCreature.MaxHealth;

        // Act
        int actualHeal = testCreature.Heal(healAmount);

        // Assert
        Assert.AreEqual(testCreature.MaxHealth - healthBeforeHeal, actualHeal); // Only healed the missing amount
        Assert.AreEqual(expectedHealth, testCreature.Health);
    }

    [Test]
    public void Creature_Heal_OnDeadCreature_HealsZero() {
        // Arrange
        testCreature.TakeHealthDamage(testCreature.Health); // Kill it
        testCreature.MarkAsDead();
        int healthBeforeHeal = testCreature.Health; // Should be 0
        int healAmount = 5;

        // Act
        int actualHeal = testCreature.Heal(healAmount);

        // Assert
        Assert.AreEqual(0, actualHeal);
        Assert.AreEqual(healthBeforeHeal, testCreature.Health);
        Assert.IsTrue(testCreature.IsDead);
    }

    [Test]
    public void Creature_ModifyArmorPool_AddsAndRemovesArmor() {
        // Arrange
        Assert.AreEqual(0, testCreature.CurrentArmorPool);

        // Act: Add armor
        testCreature.ModifyArmorPool(5);
        // Assert: Armor added
        Assert.AreEqual(5, testCreature.CurrentArmorPool);

        // Act: Add more armor
        testCreature.ModifyArmorPool(3);
        // Assert: Armor increased
        Assert.AreEqual(8, testCreature.CurrentArmorPool);

        // Act: Remove some armor
        testCreature.ModifyArmorPool(-4);
        // Assert: Armor decreased
        Assert.AreEqual(4, testCreature.CurrentArmorPool);

        // Act: Remove more armor than available
        testCreature.ModifyArmorPool(-10);
        // Assert: Armor clamped at 0
        Assert.AreEqual(0, testCreature.CurrentArmorPool);
    }

    [Test]
    public void Creature_MarkAsDead_SetsIsDeadFlag() {
        // Arrange
        Assert.IsFalse(testCreature.IsDead);

        // Act
        testCreature.MarkAsDead();

        // Assert
        Assert.IsTrue(testCreature.IsDead);
    }

    [Test]
    public void Creature_UpdateEffectiveStats_UpdatesStatsAndClampsHealth() {
        // Arrange
        testCreature.TakeHealthDamage(3); // Current health is 7/10
        int newAttack = 8;
        int newMaxHealth = 15;
        int newSpeed = 1;
        int expectedHealth = 7 + (15 - 10); // Current + (NewMax - OldMax)

        // Act
        testCreature.UpdateEffectiveStats(newAttack, newMaxHealth, newSpeed);

        // Assert
        Assert.AreEqual(newAttack, testCreature.Attack);
        Assert.AreEqual(newMaxHealth, testCreature.MaxHealth);
        Assert.AreEqual(newSpeed, testCreature.Speed);
        Assert.AreEqual(expectedHealth, testCreature.Health); // Health increased proportionally

        // Arrange: Reduce max health below current
        testCreature.Heal(100); // Heal to full (15)
        int newMaxHealthLower = 12;
        expectedHealth = 12; // Health clamped to new max

        // Act
        testCreature.UpdateEffectiveStats(newAttack, newMaxHealthLower, newSpeed);

        // Assert
        Assert.AreEqual(newMaxHealthLower, testCreature.MaxHealth);
        Assert.AreEqual(expectedHealth, testCreature.Health); // Health clamped
    }
}
