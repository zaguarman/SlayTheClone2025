using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Base class for UI components that can be initialized with dependencies.
/// </summary>
public abstract class UIComponent : MonoBehaviour {
    public bool IsInitialized { get; protected set; }
    // Store injected references instead of directly accessing singletons
    protected IGameMediator gameMediator { get; private set; }
    protected IGameReferences gameReferences { get; private set; }
    protected IGameManager gameManager { get; private set; }

    private bool hasBeenDestroyed = false;

    public UnityEvent onInitialized = new UnityEvent();

    public IPlayer Player { get; private set; }

    protected virtual void Awake() {
        // No base call needed anymore
    }

    /// <summary>
    /// Initialize the component with a player and dependencies.
    /// </summary>
    public virtual void Initialize(IPlayer player, IGameMediator mediator, IGameReferences references, IGameManager manager) {
        // Only initialize once
        if (IsInitialized) return;

        // Store references
        Player = player;
        gameMediator = mediator ?? throw new System.ArgumentNullException(nameof(mediator));
        gameReferences = references ?? throw new System.ArgumentNullException(nameof(references));
        gameManager = manager ?? throw new System.ArgumentNullException(nameof(manager));

        RegisterEvents();
        IsInitialized = true;

        // Fire the UnityEvent when initialization is complete
        onInitialized.Invoke();
    }

    /// <summary>
    /// Initialize the component with dependencies but no player.
    /// </summary>
    public virtual void Initialize(IGameMediator mediator, IGameReferences references, IGameManager manager) {
        Initialize(null, mediator, references, manager);
    }

    /// <summary>
    /// Initialize the component with dependencies but no GameManager.
    /// For components that truly don't need GameManager.
    /// </summary>
    public virtual void Initialize(IGameMediator mediator, IGameReferences references) {
        // Only initialize once
        if (IsInitialized) return;

        // Store references
        Player = null; // No player in this context
        gameMediator = mediator ?? throw new System.ArgumentNullException(nameof(mediator));
        gameReferences = references ?? throw new System.ArgumentNullException(nameof(references));
        gameManager = null; // Explicitly null

        RegisterEvents();
        IsInitialized = true;

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

    protected virtual void OnDestroy() {
        if (IsInitialized) {
            onInitialized.RemoveAllListeners();
            UnregisterEvents();
            CleanupComponent();
        }
        IsInitialized = false;
    }

    protected virtual void CleanupComponent() {
        // Override in derived classes to perform specific cleanup
    }

    protected abstract void RegisterEvents();
    protected abstract void UnregisterEvents();
    public abstract void UpdateUI(IPlayer player = null);
}