using UnityEngine;

public abstract class InitializableComponent : MonoBehaviour
{
    public bool IsInitialized { get; protected set; }

    protected virtual void Awake() { }

    public virtual void Initialize()
    {
        IsInitialized = true;
    }

    protected virtual void OnDestroy()
    {
        IsInitialized = false;
    }
}
