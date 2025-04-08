using UnityEngine;

public abstract class Singleton<T> : InitializableComponent where T : InitializableComponent {
    protected static T instance;
    private static bool isQuitting = false;

    public static T Instance {
        get {
            if (isQuitting) {
                return null;
            }

            if (instance == null) {
                instance = FindObjectOfType<T>();
                if (instance == null) {
                    Debug.LogError($"{typeof(T).Name} not found in scene!");
                }
            }
            return instance;
        }
    }

    protected override void Awake() {
        base.Awake();
        isQuitting = false;

        if (instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }
        instance = this as T;
    }

    // Added override keyword to properly override base class method
    protected override void OnDestroy() {
        base.OnDestroy();
        if (instance == this) {
            instance = null;
        }
    }

    protected virtual void OnApplicationQuit() {
        isQuitting = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        isQuitting = false;
        instance = null;
    }
}