using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;
using static DebugLogger;

public interface IInitializable {
    bool IsInitialized { get; }
    void Initialize();
}

public abstract class InitializableComponent : MonoBehaviour, IInitializable {
    public bool IsInitialized { get; protected set; }

    protected virtual void Awake() { }

    public virtual void Initialize() {
        IsInitialized = true;
    }

    protected virtual void OnDestroy() {
        IsInitialized = false;
    }
}

public class InitializationManager : MonoBehaviour {
    private static InitializationManager instance;
    public static InitializationManager Instance {
        get {
            if (instance == null) {
                var go = new GameObject("InitializationManager");
                instance = go.AddComponent<InitializationManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private Dictionary<IInitializable, bool> components = new Dictionary<IInitializable, bool>();
    private bool systemInitialized;
    public UnityEvent OnSystemInitialized { get; } = new UnityEvent();

    private void Awake() {
        if (instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterComponent(IInitializable component) {
        if (!components.ContainsKey(component)) {
            components[component] = false;
            Log($"Registered component: {component.GetType().Name}", LogTag.Initialization);
        }
    }

    public void InitializeComponents() {
        if (GameReferences.Instance == null) {
            LogError("GameReferences not found in scene! Please add it to the scene first.", LogTag.Initialization);
            return;
        }

        try {
            // Initialize in strict order
            InitializeComponent<GameReferences>();
            InitializeComponent<GameMediator>();
            InitializeComponent<GameManager>();
            InitializeComponent<GameUI>();

            CheckSystemInitialization();
        } catch (System.Exception e) {
            Debug.LogError($"Initialization failed: {e}");
        }
    }

    private void InitializeComponent<T>() where T : class {
        var component = components.Keys.FirstOrDefault(c => c is T);
        if (component != null && !components[component]) {
            component.Initialize();
            components[component] = true;
            Log($"Initialized component: {typeof(T).Name}", LogTag.Initialization);
        }
    }

    private void CheckSystemInitialization() {
        if (!systemInitialized && components.All(kvp => kvp.Value)) {
            systemInitialized = true;
            OnSystemInitialized.Invoke();
            Log("System initialization complete", LogTag.Initialization);
        }
    }

    public bool IsComponentInitialized<T>() where T : class {
        var component = components.Keys.FirstOrDefault(c => c is T);
        return component != null && components[component];
    }

    private void OnDestroy() {
        if (instance == this) {
            OnSystemInitialized.RemoveAllListeners();
            instance = null;
        }
    }
}