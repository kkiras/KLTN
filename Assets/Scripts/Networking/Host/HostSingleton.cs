using UnityEngine;

public class HostSingleton : MonoBehaviour
{
    #region Singleton State

    private static HostSingleton instance;

    public static HostSingleton Instance
    {
        get
        {
            if (instance != null) { return instance; }

            instance = FindAnyObjectByType<HostSingleton>();

            if (instance == null)
            {
                Debug.LogError("No HostSingleton in the scene!");
                return null;
            }

            return instance;
        }
    }

    #endregion

    #region Properties

    public HostGameManager GameManager { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    #endregion

    #region Initialization

    public void CreateHost()
    {
        GameManager = new HostGameManager();
    }

    #endregion
}
