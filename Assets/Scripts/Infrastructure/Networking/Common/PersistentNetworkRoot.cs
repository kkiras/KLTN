using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
public sealed class PersistentNetworkRoot : MonoBehaviour
{
    #region Singleton State

    private static PersistentNetworkRoot instance;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; }
    }

    #endregion
}
