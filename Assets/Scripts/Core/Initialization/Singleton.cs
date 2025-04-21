using UnityEngine;

public abstract class Singleton<T> : InitializableComponent where T : InitializableComponent
{
    protected static T instance;
    private static bool isQuitting = false;
    private static readonly object lockObject = new object(); // For thread safety if needed, though less critical here

    public static T Instance
    {
        get
        {
            if (isQuitting)
            {
                // Debug.LogWarning($"[Singleton] Instance '{typeof(T)}' already destroyed on application quit. Won't create again.");
                return null;
            }

            lock (lockObject) // Lock for thread safety during instance check/creation
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<T>();

                    if (instance == null)
                    {
                        // Only log error if not quitting - prevents errors during editor shutdown/play mode exit
                        if (!isQuitting)
                        {
                            Debug.LogError($"{typeof(T).Name} not found in scene!");
                        }
                    }
                    // Removed automatic DontDestroyOnLoad from here - let derived classes handle it if needed in Awake
                }
                return instance;
            }
        }
    }

    protected override void Awake()
    {
        base.Awake(); // Call InitializableComponent's Awake if it exists
        isQuitting = false;

        if (instance == null)
        {
            instance = this as T;
            // --- Let derived classes decide if they need DontDestroyOnLoad ---
            // Example: Add this line in GameManager.Awake, GameMediator.Awake etc. if they MUST persist normally
            // DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            // If an instance already exists and it's not me, destroy myself.
            // This handles scene reloads where a persistent instance already exists.
             Debug.LogWarning($"Duplicate instance of {typeof(T).Name} found. Destroying the new one ({this.gameObject.name}). Keeping existing ({instance.gameObject.name}).");
            Destroy(gameObject);
        }
        // If instance == this, do nothing (we are the singleton instance)
    }

    protected override void OnDestroy()
    {
        base.OnDestroy(); // Call InitializableComponent's OnDestroy
        if (instance == this)
        {
            // Clear the static instance only if this object *is* the instance
            // This prevents accidental clearing if a duplicate is destroyed in Awake
            instance = null;
        }
    }

    protected virtual void OnApplicationQuit()
    {
        isQuitting = true;
        // instance = null; // Optionally clear instance on quit too
    }

    // --- Method specifically for Tests ---
    /// <summary>
    /// Resets the static instance variable. Use ONLY in test teardown.
    /// </summary>
    public static void ResetInstanceForTests()
    {
        isQuitting = false; // Reset quitting flag too
        instance = null;
        Debug.Log($"[Test] Static instance for {typeof(T).Name} reset.");
    }

    // This helps reset statics when scripts recompile or play mode is entered/exited
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsForDomainReload()
    {
        ResetInstanceForTests(); // Use the same logic
    }
}