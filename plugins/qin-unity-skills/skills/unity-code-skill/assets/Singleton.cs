using UnityEngine;

public abstract class Singleton : MonoBehaviour
{
}

public abstract class Singleton<T> : Singleton where T : Singleton<T>
{
    public static T Instance { get; set; }

    public virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = (T)this;
    }

    public virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
