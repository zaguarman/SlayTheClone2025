using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace SlayTheClone {
    /// <summary>
    /// Base class for UI components that can be initialized with dependencies.
    /// </summary>
    public abstract class UIComponent : MonoBehaviour {
        public bool IsInitialized { get; protected set; }
        protected IGameMediator GameMediator { get; private set; }
        protected IGameManager GameManager { get; private set; }

        [FormerlySerializedAs("onInitialized")]
        public UnityEvent OnInitialized = new UnityEvent();

        public IPlayer Player { get; private set; }

        protected virtual void Awake() {
            // No base call needed anymore
        }

        /// <summary>
        /// Initialize the component with a player and dependencies.
        /// </summary>
        public virtual void Initialize(IPlayer player, IGameMediator mediator, IGameManager manager) {
            if (IsInitialized) return;
            Bind(player, mediator, manager ?? throw new System.ArgumentNullException(nameof(manager)));
        }

        /// <summary>
        /// Initialize the component with dependencies but no player.
        /// </summary>
        public virtual void Initialize(IGameMediator mediator, IGameManager manager) {
            Initialize(null, mediator, manager);
        }

        // Stores the dependencies and starts listening; only the first call has an effect
        private void Bind(IPlayer player, IGameMediator mediator, IGameManager manager) {
            if (IsInitialized) return;

            Player = player;
            GameMediator = mediator ?? throw new System.ArgumentNullException(nameof(mediator));
            GameManager = manager;

            RegisterEvents();
            IsInitialized = true;
            OnInitialized.Invoke();
        }


        protected virtual void OnEnable() {
            if (IsInitialized) {
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
                OnInitialized.RemoveAllListeners();
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
}
