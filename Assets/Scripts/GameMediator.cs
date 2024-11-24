using System.Collections.Generic;
using UnityEngine;

public interface IGameMediator {
    GameEvents.GameStateChangedEvent OnGameStateChanged { get; }
    GameEvents.PlayerDamagedEvent OnPlayerDamaged { get; }
    GameEvents.CreatureDamagedEvent OnCreatureDamaged { get; }
    GameEvents.CreatureDiedEvent OnCreatureDied { get; }
    GameEvents.GameOverEvent OnGameOver { get; }

    void Initialize();
    void RegisterPlayer(IPlayer player);
    void UnregisterPlayer(IPlayer player);
    void RegisterUI(GameUI ui);
    void UnregisterUI(GameUI ui);
    void RegisterDamageResolver(DamageResolver resolver);
    void UnregisterDamageResolver(DamageResolver resolver);
    void NotifyGameStateChanged();
    void NotifyPlayerDamaged(IPlayer player, int damage);
    void NotifyCreatureDamaged(ICreature creature, int damage);
    void NotifyCreatureDied(ICreature creature);
    void NotifyGameOver(IPlayer winner);
    void Cleanup();
}

public class GameMediator : Singleton<GameMediator>, IGameMediator {
    private GameEvents gameEvents;
    private readonly List<IPlayer> players = new List<IPlayer>();
    private GameUI gameUI;
    private DamageResolver damageResolver;
    private bool isInitialized;

    protected override void Awake() {
        base.Awake();
        InitializeIfNeeded();
    }

    private void InitializeIfNeeded() {
        if (!isInitialized) {
            gameEvents = GameEvents.Instance;
            ClearRegistrations();
            isInitialized = true;
        }
    }

    public void Initialize() {
        InitializeIfNeeded();
    }

    private bool ValidateState(string operation) {
        InitializeIfNeeded(); 
        return true; 
    }

    public void RegisterPlayer(IPlayer player) {
        InitializeIfNeeded();
        if (player == null) return;

        if (!players.Contains(player)) {
            players.Add(player);
            player.OnDamaged.AddListener((damage) => NotifyPlayerDamaged(player, damage));
        }
    }

    public void UnregisterPlayer(IPlayer player) {
        if (player == null) return;
        players.Remove(player);
    }

    public void RegisterUI(GameUI ui) {
        if (!ValidateState("RegisterUI") || ui == null) return;
        gameUI = ui;
    }

    public void UnregisterUI(GameUI ui) {
        if (gameUI != ui) return;
        gameUI = null;
    }

    public void RegisterDamageResolver(DamageResolver resolver) {
        if (!ValidateState("RegisterDamageResolver") || resolver == null) return;
        damageResolver = resolver;
    }

    public void UnregisterDamageResolver(DamageResolver resolver) {
        if (damageResolver != resolver) return;
        damageResolver = null;
    }

    public GameEvents.GameStateChangedEvent OnGameStateChanged => gameEvents.OnGameStateChanged;
    public GameEvents.PlayerDamagedEvent OnPlayerDamaged => gameEvents.OnPlayerDamaged;
    public GameEvents.CreatureDamagedEvent OnCreatureDamaged => gameEvents.OnCreatureDamaged;
    public GameEvents.CreatureDiedEvent OnCreatureDied => gameEvents.OnCreatureDied;
    public GameEvents.GameOverEvent OnGameOver => gameEvents.OnGameOver;

    public void NotifyGameStateChanged() => gameEvents?.NotifyGameStateChanged();
    public void NotifyPlayerDamaged(IPlayer player, int damage) => gameEvents?.NotifyPlayerDamaged(player, damage);
    public void NotifyCreatureDamaged(ICreature creature, int damage) => gameEvents?.NotifyCreatureDamaged(creature, damage);
    public void NotifyCreatureDied(ICreature creature) => gameEvents?.NotifyCreatureDied(creature);
    public void NotifyGameOver(IPlayer winner) => gameEvents?.NotifyGameOver(winner);

    private void ClearRegistrations() {
        players.Clear();
        gameUI = null;
        damageResolver = null;
    }

    public void Cleanup() {
        ClearRegistrations();
        isInitialized = false;
    }

    protected override void OnDestroy() {
        if (instance == this) {
            Cleanup();
            instance = null;
        }
        base.OnDestroy();
    }
}