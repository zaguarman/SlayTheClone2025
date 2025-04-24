using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static DebugLogger;

/// <summary>
/// GameMediator serves as the central event hub for game events and notifications.
/// It implements IGameMediator to support dependency injection.
/// </summary>
public class GameMediator : MonoBehaviour, IGameMediator {
    // Implement IsInitialized property
    public bool IsInitialized { get; private set; }
    #region Game Events Class
    private class GameEvents {
        public readonly UnityEvent<IPlayer, int> PlayerDamaged = new UnityEvent<IPlayer, int>();
        public readonly UnityEvent<ICreature, int> CreatureDamaged = new UnityEvent<ICreature, int>();
        public readonly UnityEvent<ICreature> CreatureDied = new UnityEvent<ICreature>();
        public readonly UnityEvent<IPlayer> GameOver = new UnityEvent<IPlayer>();
        public readonly UnityEvent GameStateChanged = new UnityEvent();
        public readonly UnityEvent GameInitialized = new UnityEvent();
        public readonly UnityEvent<ICreature, IPlayer> CreatureSummoned = new UnityEvent<ICreature, IPlayer>();
        public readonly UnityEvent<ICreature, int> CreatureArmorChanged = new UnityEvent<ICreature, int>(); // NEW Event
        public readonly UnityEvent<ICreature> CreaturePreSummon = new UnityEvent<ICreature>();
        public readonly UnityEvent ActionsQueueChanged = new UnityEvent();
        public readonly UnityEvent<IPlayer> HandStateChanged = new UnityEvent<IPlayer>();
        public readonly UnityEvent<IPlayer> BattlefieldStateChanged = new UnityEvent<IPlayer>();
        public readonly UnityEvent<int> TurnEnded = new UnityEvent<int>();

        public void ClearAllListeners() {
            PlayerDamaged.RemoveAllListeners();
            CreatureDamaged.RemoveAllListeners();
            CreatureDied.RemoveAllListeners();
            GameOver.RemoveAllListeners();
            GameStateChanged.RemoveAllListeners();
            GameInitialized.RemoveAllListeners();
            CreatureSummoned.RemoveAllListeners();
            CreatureArmorChanged.RemoveAllListeners();
            CreaturePreSummon.RemoveAllListeners();
            ActionsQueueChanged.RemoveAllListeners();
            HandStateChanged.RemoveAllListeners();
            BattlefieldStateChanged.RemoveAllListeners();
            TurnEnded.RemoveAllListeners();
        }
    }
    #endregion

    #region Fields & Properties
    private readonly GameEvents events = new GameEvents();
    private readonly HashSet<IPlayer> registeredPlayers = new HashSet<IPlayer>();
    private bool gameInitialized = false;
    #endregion

    // Add Initialize method
    public void Initialize()
    {
        if (IsInitialized) return;
        IsInitialized = true;
        Log("GameMediator Initialized", LogTag.Initialization);
    }

    #region Methods
    public void AddGameStateChangedListener(UnityAction listener) {
        ValidateInitialization();
        events.GameStateChanged.AddListener(listener);
    }

    public void RemoveGameStateChangedListener(UnityAction listener) {
        events.GameStateChanged.RemoveListener(listener);
    }

    public void AddPlayerDamagedListener(UnityAction<IPlayer, int> listener) {
        ValidateInitialization();
        events.PlayerDamaged.AddListener(listener);
    }

    public void RemovePlayerDamagedListener(UnityAction<IPlayer, int> listener) {
        events.PlayerDamaged.RemoveListener(listener);
    }

    public void AddCreatureDamagedListener(UnityAction<ICreature, int> listener) {
        ValidateInitialization();
        events.CreatureDamaged.AddListener(listener);
    }

    public void RemoveCreatureDamagedListener(UnityAction<ICreature, int> listener) {
        events.CreatureDamaged.RemoveListener(listener);
    }

    public void AddCreatureDiedListener(UnityAction<ICreature> listener) {
        ValidateInitialization();
        events.CreatureDied.AddListener(listener);
    }

    public void RemoveCreatureDiedListener(UnityAction<ICreature> listener) {
        events.CreatureDied.RemoveListener(listener);
    }

    public void AddGameOverListener(UnityAction<IPlayer> listener) {
        ValidateInitialization();
        events.GameOver.AddListener(listener);
    }

    public void RemoveGameOverListener(UnityAction<IPlayer> listener) {
        events.GameOver.RemoveListener(listener);
    }

    public void AddGameInitializedListener(UnityAction listener) {
        ValidateInitialization();
        events.GameInitialized.AddListener(listener);
    }

    public void RemoveGameInitializedListener(UnityAction listener) {
        events.GameInitialized.RemoveListener(listener);
    }

    public void AddCreatureSummonedListener(UnityAction<ICreature, IPlayer> listener) {
        ValidateInitialization();
        events.CreatureSummoned.AddListener(listener);
    }

    public void RemoveCreatureSummonedListener(UnityAction<ICreature, IPlayer> listener) {
        events.CreatureSummoned.RemoveListener(listener);
    }

    // --- NEW Armor Change Listeners ---
    public void AddCreatureArmorChangedListener(UnityAction<ICreature, int> listener) {
        ValidateInitialization();
        events.CreatureArmorChanged.AddListener(listener);
    }

    public void RemoveCreatureArmorChangedListener(UnityAction<ICreature, int> listener) {
        events.CreatureArmorChanged.RemoveListener(listener);

    }

    public void AddCreaturePreSummonListener(UnityAction<ICreature> listener) {
        ValidateInitialization();
        events.CreaturePreSummon.AddListener(listener);
    }

    public void RemoveCreaturePreSummonListener(UnityAction<ICreature> listener) {
        events.CreaturePreSummon.RemoveListener(listener);
    }

    public void AddActionsQueueChangedListener(UnityAction listener) {
        ValidateInitialization();
        events.ActionsQueueChanged.AddListener(listener);
    }

    public void RemoveActionsQueueChangedListener(UnityAction listener) {
        events.ActionsQueueChanged.RemoveListener(listener);
    }

    public void AddTurnEndedListener(UnityAction<int> listener) {
        ValidateInitialization();
        events.TurnEnded.AddListener(listener);
    }

    public void RemoveTurnEndedListener(UnityAction<int> listener) {
        events.TurnEnded.RemoveListener(listener);
    }

    public void AddHandStateChangedListener(UnityAction<IPlayer> listener) {
        ValidateInitialization();
        events.HandStateChanged.AddListener(listener);
    }

    public void RemoveHandStateChangedListener(UnityAction<IPlayer> listener) {
        events.HandStateChanged.RemoveListener(listener);
    }

    public void AddBattlefieldStateChangedListener(UnityAction<IPlayer> listener) {
        ValidateInitialization();
        events.BattlefieldStateChanged.AddListener(listener);
    }

    public void RemoveBattlefieldStateChangedListener(UnityAction<IPlayer> listener) {
        events.BattlefieldStateChanged.RemoveListener(listener);
    }
    #endregion

    #region Player Registration
    public void RegisterPlayer(IPlayer player) {
        ValidateInitialization();
        if (player == null) throw new System.ArgumentNullException(nameof(player));

        if (registeredPlayers.Add(player)) {
            player.OnDamaged.AddListener((damage) => NotifyPlayerDamaged(player, damage));
            Log($"Player registered: {(player.IsPlayer1 ? "Player 1" : "Player 2")}", LogTag.Players);
        }
    }

    public void UnregisterPlayer(IPlayer player) {
        if (player == null) return;
        if (registeredPlayers.Remove(player)) {
            Log($"Player unregistered: {(player.IsPlayer1 ? "Player 1" : "Player 2")}", LogTag.Players);
        }
    }
    #endregion

    #region Notification Methods
    public void NotifyGameStateChanged() {
        ValidateInitialization();
        events.GameStateChanged.Invoke();
    }

    public void NotifyGameInitialized() {
        ValidateInitialization();
        if (!gameInitialized) {
            gameInitialized = true;
            Log("Game initialized", LogTag.Initialization);
            events.GameInitialized.Invoke();
        }
    }

    public void NotifyPlayerDamaged(IPlayer player, int damage) {
        ValidateInitialization();
        if (player == null) throw new System.ArgumentNullException(nameof(player));

        events.PlayerDamaged.Invoke(player, damage);
        Log($"{damage} damage to {(player.IsPlayer1 ? "Player 1" : "Player 2")}", LogTag.Players | LogTag.Combat);

        if (player.Health <= 0) {
            NotifyGameOver(player.Opponent);
        }
    }

    public void NotifyCreatureDamaged(ICreature creature, int damage) {
        ValidateInitialization();
        if (creature == null) throw new System.ArgumentNullException(nameof(creature));

        events.CreatureDamaged.Invoke(creature, damage);
        Log($"{damage} damage to {creature.Name}", LogTag.Creatures | LogTag.Combat);

        if (creature.Health <= 0) {
            NotifyCreatureDied(creature);
        }
    }

    public void NotifyCreatureDied(ICreature creature) {
        ValidateInitialization();
        if (creature == null) throw new System.ArgumentNullException(nameof(creature));

        events.CreatureDied.Invoke(creature);
        Log($"Creature died: {creature.Name}", LogTag.Creatures);
        NotifyGameStateChanged();
    }

    public void NotifyCreatureHealed(ICreature creature, int amount) {
        ValidateInitialization();
        if (creature == null) throw new System.ArgumentNullException(nameof(creature));

        // If we had a CreatureHealed event, we would invoke it here
        // events.CreatureHealed.Invoke(creature, amount);
        Log($"{creature.Name} healed for {amount}, health now: {creature.Health}", LogTag.Creatures | LogTag.Effects);
        NotifyGameStateChanged();
    }

    public void NotifyPlayerHealed(IPlayer player, int amount) {
        ValidateInitialization();
        if (player == null) throw new System.ArgumentNullException(nameof(player));

        // If we had a PlayerHealed event, we would invoke it here
        // events.PlayerHealed.Invoke(player, amount);
        Log($"{(player.IsPlayer1 ? "Player 1" : "Player 2")} healed for {amount}, health now: {player.Health}", LogTag.Players | LogTag.Effects);
        NotifyGameStateChanged();
    }

    public void NotifyGameOver(IPlayer winner) {
        ValidateInitialization();
        if (winner == null) throw new System.ArgumentNullException(nameof(winner));

        Log($"Game over: {(winner.IsPlayer1 ? "Player 1" : "Player 2")} wins", LogTag.Players);
        events.GameOver.Invoke(winner);
    }

    public void NotifyCreaturePreSummon(ICreature creature) {
        ValidateInitialization();
        if (creature == null) throw new System.ArgumentNullException(nameof(creature));

        events.CreaturePreSummon.Invoke(creature);
        Log($"Creature pre-summon: {creature.Name}", LogTag.Creatures);
    }

    public void NotifyCreatureSummoned(ICreature creature, IPlayer owner) {
        ValidateInitialization();
        if (creature == null) throw new System.ArgumentNullException(nameof(creature));
        if (owner == null) throw new System.ArgumentNullException(nameof(owner));

        events.CreatureSummoned.Invoke(creature, owner);
        Log($"Creature summoned: {creature.Name} by {(owner.IsPlayer1 ? "Player 1" : "Player 2")}", LogTag.Creatures);
        NotifyGameStateChanged();
    }
    // --- NEW Armor Change Notifier ---
    public void NotifyCreatureArmorChanged(ICreature creature, int newArmor) {
        ValidateInitialization();
        if (creature == null) throw new System.ArgumentNullException(nameof(creature));

        events.CreatureArmorChanged.Invoke(creature, newArmor);
        // Optional Log: Can be noisy if armor changes frequently
        // Log($"Creature armor changed: {creature.Name} now has {newArmor} armor", LogTag.Creatures | LogTag.Effects);
        NotifyGameStateChanged(); // Ensure UI updates
    }

    public void NotifyActionsQueueChanged() {
        ValidateInitialization();
        events.ActionsQueueChanged.Invoke();
    }

    public void NotifyHandStateChanged(IPlayer player) {
        ValidateInitialization();
        events.HandStateChanged.Invoke(player);
        Log($"Hand state changed for {(player.IsPlayer1 ? "Player 1" : "Player 2")}", LogTag.Players | LogTag.Cards);
    }

    public void NotifyBattlefieldStateChanged(IPlayer player) {
        ValidateInitialization();
        events.BattlefieldStateChanged.Invoke(player);
        Log($"Battlefield state changed for {(player.IsPlayer1 ? "Player 1" : "Player 2")}", LogTag.Players | LogTag.Cards);
    }

    public void NotifyTurnEnded(int turnNumber) {
        ValidateInitialization();
        events.TurnEnded.Invoke(turnNumber);
        Log($"Turn {turnNumber} ended", LogTag.Turns);
    }
    #endregion

    #region Initialization Validation
    private void ValidateInitialization() {
        if (!IsInitialized) {
            throw new System.InvalidOperationException("GameMediator is not initialized");
        }
    }
    #endregion

    #region Unity Lifecycle
    protected void OnDestroy() {
        events.ClearAllListeners();
        registeredPlayers.Clear();
        IsInitialized = false; // Mark as not initialized on destroy
    }
    #endregion
}