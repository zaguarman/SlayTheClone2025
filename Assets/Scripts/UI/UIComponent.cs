using UnityEngine;
using UnityEngine.Events;
using System; // Needed for Obsolete attribute

/// <summary>
/// Base class for UI components that can be initialized with dependencies.
/// </summary>
public abstract class UIComponent : InitializableComponent {
    // Store injected references instead of directly accessing singletons
    protected IGameMediator gameMediator { get; private set; }
    protected IGameReferences gameReferences { get; private set; }
    protected GameManager gameManager { get; private set; } // Keep for now, will be replaced with interface later

    private bool hasBeenDestroyed = false;

    public UnityEvent onInitialized = new UnityEvent();

    public IPlayer Player { get; private set; }

    protected override void Awake() {
        base.Awake();
    }

    /// <summary>
    /// Initialize the component with a player and dependencies.
    /// </summary>
    public virtual void Initialize(IPlayer player, IGameMediator mediator, IGameReferences references) {
        // Only initialize once
        if (IsInitialized) return;

        // Store references
        Player = player;
        gameMediator = mediator ?? throw new System.ArgumentNullException(nameof(mediator));
        gameReferences = references ?? throw new System.ArgumentNullException(nameof(references));
        gameManager = GameManager.Instance; // Still using singleton for now

        RegisterEvents();
        base.Initialize();

        // Fire the UnityEvent when initialization is complete
        onInitialized.Invoke();
    }

    /// <summary>
    /// Initialize the component with dependencies but no player.
    /// </summary>
    public virtual void Initialize(IGameMediator mediator, IGameReferences references) {
        Initialize(null, mediator, references);
    }

    /// <summary>
    /// Legacy initialization method for backward compatibility.
    /// Will be removed once all code is migrated to DI.
    /// </summary>
    [Obsolete("Use Initialize with explicit dependencies (IGameMediator, IGameReferences) instead", true)] // true = error
    public virtual void Initialize(IPlayer player = null) {
        if (Player == null) {
            Player = player;
        }

        if (IsInitialized) return;

        // Ensure dependencies are initialized and get them from singletons
        if (!InitializationManager.Instance.IsComponentInitialized<GameMediator>()) {
            Debug.LogWarning($"{GetType().Name}: GameMediator not initialized yet");
            return;
        }

        // Get dependencies from singletons and direct find
        gameMediator = GameMediator.Instance;
        gameReferences = UnityEngine.Object.FindObjectOfType<GameReferences>(); // Direct find since GameReferences is no longer a singleton
        gameManager = GameManager.Instance;

        RegisterEvents();
        base.Initialize();

        // Fire the UnityEvent when initialization is complete
        onInitialized.Invoke();
    }

    protected virtual void OnEnable() {
        if (IsInitialized && !hasBeenDestroyed) {
            RegisterEvents();
        }
    }

    protected virtual void OnDisable() {
        if (IsInitialized) {
            UnregisterEvents();
        }
    }

    protected override void OnDestroy() {
        if (IsInitialized) {
            onInitialized.RemoveAllListeners();
            UnregisterEvents();
            CleanupComponent();
        }
        base.OnDestroy();
    }

    protected virtual void CleanupComponent() {
        // Override in derived classes to perform specific cleanup
    }

    protected abstract void RegisterEvents();
    protected abstract void UnregisterEvents();
    public abstract void UpdateUI(IPlayer player = null);
}