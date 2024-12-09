using UnityEngine;

public interface IGameAction {
    void Execute();
}

public class SummonCreatureAction : IGameAction {
    private readonly ICard card;
    private readonly IPlayer owner;
    private readonly GameMediator gameMediator;

    public SummonCreatureAction(ICard card, IPlayer owner) {
        this.card = card;
        this.owner = owner;
        this.gameMediator = GameMediator.Instance;
    }

    public void Execute() {
        if (card is ICreature creature) {
            gameMediator.NotifyCreaturePreSummon(creature);
            owner.AddToBattlefield(creature);
            gameMediator.NotifyCreatureSummoned(creature, owner);
        }
    }
}

public class DamagePlayerAction : IGameAction {
    private IPlayer target;
    private int damage;

    public DamagePlayerAction(IPlayer target, int damage) {
        this.target = target;
        this.damage = damage;
        Debug.Log($"DamagePlayerAction created - Target: {target}, Damage: {damage}");
    }

    public void Execute() {
        target.TakeDamage(damage);
        Debug.Log($"{damage} damage dealt to {target}");
    }
}

public class DamageCreatureAction : IGameAction {
    private readonly ICreature target;
    private readonly int damage;
    private readonly ICreature attacker;

    public DamageCreatureAction(ICreature target, int damage, ICreature attacker = null) {
        this.target = target;
        this.damage = damage;
        this.attacker = attacker;
        Debug.Log($"[DamageCreatureAction] Created - Target: {target?.Name} (TargetId: {target?.TargetId}), " +
                 $"Damage: {damage}, Attacker: {attacker?.Name} (TargetId: {attacker?.TargetId})");
    }

    public void Execute() {
        Debug.Log($"[DamageCreatureAction] Executing - Target: {target?.Name} (TargetId: {target?.TargetId}), " +
                 $"Damage: {damage}, Attacker: {attacker?.Name} (TargetId: {attacker?.TargetId})");
        target?.TakeDamage(damage);
    }

    public ICreature GetTarget() {
        Debug.Log($"[DamageCreatureAction] Getting target: {target?.Name} (TargetId: {target?.TargetId})");
        return target;
    }

    public ICreature GetAttacker() {
        Debug.Log($"[DamageCreatureAction] Getting attacker: {attacker?.Name} (TargetId: {attacker?.TargetId})");
        return attacker;
    }

    public int GetDamage() => damage;
}