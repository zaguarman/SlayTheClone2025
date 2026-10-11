using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
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
            public readonly UnityEvent<ICreature, int> CreatureArmorChanged = new UnityEvent<ICreature, int>();
            public readonly UnityEvent<ICreature> CreatureStatsChanged = new UnityEvent<ICreature>(); // NEW Event
            public readonly UnityEvent<ICreature> CreaturePreSummon = new UnityEvent<ICreature>();
            public readonly UnityEvent ActionsQueueChanged = new UnityEvent();
            public readonly UnityEvent<IPlayer> HandStateChanged = new UnityEvent<IPlayer>();
            public readonly UnityEvent<IPlayer> BattlefieldStateChanged = new UnityEvent<IPlayer>();
            public readonly UnityEvent<int> TurnEnded = new UnityEvent<int>();
            public readonly UnityEvent<ICreature, int> CreatureHealed = new UnityEvent<ICreature, int>();
            public readonly UnityEvent<IPlayer, int> PlayerHealed = new UnityEvent<IPlayer, int>();
            public readonly UnityEvent<ICreature, Enums.StatusEffectType, bool> CreatureStatusChanged = new UnityEvent<ICreature, Enums.StatusEffectType, bool>();

            public void ClearAllListeners() {
                // Every event field, so a newly added event can't be forgotten here
                foreach (var field in typeof(GameEvents).GetFields()) {
                    (field.GetValue(this) as UnityEventBase)?.RemoveAllListeners();
                }
            }
        }
        #endregion

        #region Fields & Properties
        private readonly GameEvents events = new GameEvents();
        private readonly HashSet<IPlayer> registeredPlayers = new HashSet<IPlayer>();
        private bool gameInitialized = false;
        private bool gameOver = false;
        #endregion

        // Add Initialize method
        public void Initialize() {
            if (IsInitialized) return;
            IsInitialized = true;
            Log("GameMediator Initialized", LogTag.Initialization);
        }

        #region Listener Registration
        public void AddGameStateChangedListener(UnityAction listener) => Subscribe(events.GameStateChanged, listener);
        public void RemoveGameStateChangedListener(UnityAction listener) => events.GameStateChanged.RemoveListener(listener);

        public void AddPlayerDamagedListener(UnityAction<IPlayer, int> listener) => Subscribe(events.PlayerDamaged, listener);
        public void RemovePlayerDamagedListener(UnityAction<IPlayer, int> listener) => events.PlayerDamaged.RemoveListener(listener);

        public void AddCreatureDamagedListener(UnityAction<ICreature, int> listener) => Subscribe(events.CreatureDamaged, listener);
        public void RemoveCreatureDamagedListener(UnityAction<ICreature, int> listener) => events.CreatureDamaged.RemoveListener(listener);

        public void AddCreatureDiedListener(UnityAction<ICreature> listener) => Subscribe(events.CreatureDied, listener);
        public void RemoveCreatureDiedListener(UnityAction<ICreature> listener) => events.CreatureDied.RemoveListener(listener);

        public void AddGameOverListener(UnityAction<IPlayer> listener) => Subscribe(events.GameOver, listener);
        public void RemoveGameOverListener(UnityAction<IPlayer> listener) => events.GameOver.RemoveListener(listener);

        public void AddGameInitializedListener(UnityAction listener) => Subscribe(events.GameInitialized, listener);
        public void RemoveGameInitializedListener(UnityAction listener) => events.GameInitialized.RemoveListener(listener);

        public void AddCreatureSummonedListener(UnityAction<ICreature, IPlayer> listener) => Subscribe(events.CreatureSummoned, listener);
        public void RemoveCreatureSummonedListener(UnityAction<ICreature, IPlayer> listener) => events.CreatureSummoned.RemoveListener(listener);

        public void AddCreatureArmorChangedListener(UnityAction<ICreature, int> listener) => Subscribe(events.CreatureArmorChanged, listener);
        public void RemoveCreatureArmorChangedListener(UnityAction<ICreature, int> listener) => events.CreatureArmorChanged.RemoveListener(listener);

        public void AddCreatureStatsChangedListener(UnityAction<ICreature> listener) => Subscribe(events.CreatureStatsChanged, listener);
        public void RemoveCreatureStatsChangedListener(UnityAction<ICreature> listener) => events.CreatureStatsChanged.RemoveListener(listener);

        public void AddCreaturePreSummonListener(UnityAction<ICreature> listener) => Subscribe(events.CreaturePreSummon, listener);
        public void RemoveCreaturePreSummonListener(UnityAction<ICreature> listener) => events.CreaturePreSummon.RemoveListener(listener);

        public void AddActionsQueueChangedListener(UnityAction listener) => Subscribe(events.ActionsQueueChanged, listener);
        public void RemoveActionsQueueChangedListener(UnityAction listener) => events.ActionsQueueChanged.RemoveListener(listener);

        public void AddTurnEndedListener(UnityAction<int> listener) => Subscribe(events.TurnEnded, listener);
        public void RemoveTurnEndedListener(UnityAction<int> listener) => events.TurnEnded.RemoveListener(listener);

        public void AddHandStateChangedListener(UnityAction<IPlayer> listener) => Subscribe(events.HandStateChanged, listener);
        public void RemoveHandStateChangedListener(UnityAction<IPlayer> listener) => events.HandStateChanged.RemoveListener(listener);

        public void AddBattlefieldStateChangedListener(UnityAction<IPlayer> listener) => Subscribe(events.BattlefieldStateChanged, listener);
        public void RemoveBattlefieldStateChangedListener(UnityAction<IPlayer> listener) => events.BattlefieldStateChanged.RemoveListener(listener);

        public void AddCreatureHealedListener(UnityAction<ICreature, int> listener) => Subscribe(events.CreatureHealed, listener);
        public void RemoveCreatureHealedListener(UnityAction<ICreature, int> listener) => events.CreatureHealed.RemoveListener(listener);

        public void AddPlayerHealedListener(UnityAction<IPlayer, int> listener) => Subscribe(events.PlayerHealed, listener);
        public void RemovePlayerHealedListener(UnityAction<IPlayer, int> listener) => events.PlayerHealed.RemoveListener(listener);

        public void AddCreatureStatusChangedListener(UnityAction<ICreature, Enums.StatusEffectType, bool> listener) => Subscribe(events.CreatureStatusChanged, listener);
        public void RemoveCreatureStatusChangedListener(UnityAction<ICreature, Enums.StatusEffectType, bool> listener) => events.CreatureStatusChanged.RemoveListener(listener);

        private void Subscribe(UnityEvent gameEvent, UnityAction listener) {
            ValidateInitialization();
            gameEvent.AddListener(listener);
        }

        private void Subscribe<T0>(UnityEvent<T0> gameEvent, UnityAction<T0> listener) {
            ValidateInitialization();
            gameEvent.AddListener(listener);
        }

        private void Subscribe<T0, T1>(UnityEvent<T0, T1> gameEvent, UnityAction<T0, T1> listener) {
            ValidateInitialization();
            gameEvent.AddListener(listener);
        }

        private void Subscribe<T0, T1, T2>(UnityEvent<T0, T1, T2> gameEvent, UnityAction<T0, T1, T2> listener) {
            ValidateInitialization();
            gameEvent.AddListener(listener);
        }
        #endregion

        #region Player Registration
        public void RegisterPlayer(IPlayer player) {
            ValidateInitialization();
            if (player == null) throw new System.ArgumentNullException(nameof(player));

            if (registeredPlayers.Add(player)) {
                player.OnDamaged.AddListener((damage) => NotifyPlayerDamaged(player, damage));
                Log($"Player registered: {player.Name}", LogTag.Players);
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
            Log($"{damage} damage to {player.Name}", LogTag.Players | LogTag.Combat);

            if (player.Health <= 0) {
                NotifyGameOver(player.Opponent);
            }
        }

        public void NotifyCreatureDamaged(ICreature creature, int damage) {
            ValidateInitialization();
            if (creature == null) throw new System.ArgumentNullException(nameof(creature));

            events.CreatureDamaged.Invoke(creature, damage);
            Log($"{damage} damage to {creature.Name}", LogTag.Creatures | LogTag.Combat);
            // Death is announced separately by DamageCreatureActionExecutor via NotifyCreatureDied
        }

        public void NotifyCreatureDied(ICreature creature) {
            ValidateInitialization();
            if (creature == null) throw new System.ArgumentNullException(nameof(creature));

            events.CreatureDied.Invoke(creature);
            Log($"Creature died: {creature.Name}", LogTag.Creatures);
        }

        public void NotifyCreatureHealed(ICreature creature, int amount) {
            ValidateInitialization();
            if (creature == null) throw new System.ArgumentNullException(nameof(creature));

            events.CreatureHealed.Invoke(creature, amount);
            Log($"{creature.Name} healed for {amount}, health now: {creature.Health}", LogTag.Creatures | LogTag.Effects);
        }

        public void NotifyPlayerHealed(IPlayer player, int amount) {
            ValidateInitialization();
            if (player == null) throw new System.ArgumentNullException(nameof(player));

            events.PlayerHealed.Invoke(player, amount);
            Log($"{player.Name} healed for {amount}, health now: {player.Health}", LogTag.Players | LogTag.Effects);
        }

        // The game ends once; further damage to a player already at 0 must not announce it again
        public void NotifyGameOver(IPlayer winner) {
            ValidateInitialization();
            if (winner == null) throw new System.ArgumentNullException(nameof(winner));
            if (gameOver) return;
            gameOver = true;

            Log($"Game over: {winner.Name} wins", LogTag.Players);
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
            Log($"Creature summoned: {creature.Name} by {owner.Name}", LogTag.Creatures);
        }

        public void NotifyCreatureArmorChanged(ICreature creature, int newArmor) {
            ValidateInitialization();
            if (creature == null) throw new System.ArgumentNullException(nameof(creature));

            events.CreatureArmorChanged.Invoke(creature, newArmor);
        }

        public void NotifyCreatureStatsChanged(ICreature creature) {
            ValidateInitialization();
            if (creature == null) throw new System.ArgumentNullException(nameof(creature));

            events.CreatureStatsChanged.Invoke(creature);
            Log($"Stats changed for {creature.Name} (TargetID: {creature.TargetId.ToUpper()})", LogTag.Creatures | LogTag.Effects);
        }

        public void NotifyCreatureStatusChanged(ICreature creature, Enums.StatusEffectType status, bool applied) {
            ValidateInitialization();
            if (creature == null) throw new System.ArgumentNullException(nameof(creature));

            events.CreatureStatusChanged.Invoke(creature, status, applied);
            Log($"{creature.Name} {(applied ? "gained" : "lost")} status {status}", LogTag.Creatures | LogTag.Effects);
        }

        public void NotifyActionsQueueChanged() {
            ValidateInitialization();
            events.ActionsQueueChanged.Invoke();
        }

        public void NotifyHandStateChanged(IPlayer player) {
            ValidateInitialization();
            events.HandStateChanged.Invoke(player);
            Log($"Hand state changed for {player.Name}", LogTag.Players | LogTag.Cards);
        }

        public void NotifyBattlefieldStateChanged(IPlayer player) {
            ValidateInitialization();
            events.BattlefieldStateChanged.Invoke(player);
            Log($"Battlefield state changed for {player.Name}", LogTag.Players | LogTag.Cards);
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
}
