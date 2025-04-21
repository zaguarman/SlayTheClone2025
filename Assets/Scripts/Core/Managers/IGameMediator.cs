using UnityEngine.Events;

/// <summary>
/// Interface for the GameMediator, providing event subscription and notification methods
/// for game state changes and events.
/// </summary>
public interface IGameMediator
{
    // Property to check if the mediator is initialized
    bool IsInitialized { get; }

    #region Event Subscription Methods
    void AddGameStateChangedListener(UnityAction listener);
    void RemoveGameStateChangedListener(UnityAction listener);
    
    void AddPlayerDamagedListener(UnityAction<IPlayer, int> listener);
    void RemovePlayerDamagedListener(UnityAction<IPlayer, int> listener);
    
    void AddCreatureDamagedListener(UnityAction<ICreature, int> listener);
    void RemoveCreatureDamagedListener(UnityAction<ICreature, int> listener);
    
    void AddCreatureDiedListener(UnityAction<ICreature> listener);
    void RemoveCreatureDiedListener(UnityAction<ICreature> listener);
    
    void AddGameOverListener(UnityAction<IPlayer> listener);
    void RemoveGameOverListener(UnityAction<IPlayer> listener);
    
    void AddGameInitializedListener(UnityAction listener);
    void RemoveGameInitializedListener(UnityAction listener);
    
    void AddCreatureSummonedListener(UnityAction<ICreature, IPlayer> listener);
    void RemoveCreatureSummonedListener(UnityAction<ICreature, IPlayer> listener);
    
    void AddCreatureArmorChangedListener(UnityAction<ICreature, int> listener);
    void RemoveCreatureArmorChangedListener(UnityAction<ICreature, int> listener);
    
    void AddCreaturePreSummonListener(UnityAction<ICreature> listener);
    void RemoveCreaturePreSummonListener(UnityAction<ICreature> listener);
    
    void AddActionsQueueChangedListener(UnityAction listener);
    void RemoveActionsQueueChangedListener(UnityAction listener);
    
    void AddHandStateChangedListener(UnityAction<IPlayer> listener);
    void RemoveHandStateChangedListener(UnityAction<IPlayer> listener);
    
    void AddBattlefieldStateChangedListener(UnityAction<IPlayer> listener);
    void RemoveBattlefieldStateChangedListener(UnityAction<IPlayer> listener);
    
    void AddTurnEndedListener(UnityAction<int> listener);
    void RemoveTurnEndedListener(UnityAction<int> listener);
    #endregion

    #region Player Registration
    void RegisterPlayer(IPlayer player);
    void UnregisterPlayer(IPlayer player);
    #endregion

    #region Notification Methods
    void NotifyGameStateChanged();
    void NotifyGameInitialized();
    void NotifyPlayerDamaged(IPlayer player, int damage);
    void NotifyCreatureDamaged(ICreature creature, int damage);
    void NotifyCreatureDied(ICreature creature);
    void NotifyCreatureHealed(ICreature creature, int amount);
    void NotifyPlayerHealed(IPlayer player, int amount);
    void NotifyGameOver(IPlayer winner);
    void NotifyCreaturePreSummon(ICreature creature);
    void NotifyCreatureSummoned(ICreature creature, IPlayer owner);
    void NotifyCreatureArmorChanged(ICreature creature, int newArmor);
    void NotifyActionsQueueChanged();
    void NotifyHandStateChanged(IPlayer player);
    void NotifyBattlefieldStateChanged(IPlayer player);
    void NotifyTurnEnded(int turnNumber);
    #endregion
}
