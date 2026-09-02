using System.Threading.Tasks;
using UnityEngine;

public class ClientSingleton : MonoBehaviour
{
    #region Singleton State

    private static ClientSingleton instance;

    public static ClientSingleton Instance
    {
        get
        {
            if (instance != null) { return instance; }

            instance = FindAnyObjectByType<ClientSingleton>();

            if (instance == null)
            {
                Debug.LogError("No ClientSingleton in the scene!");
                return null;
            }

            return instance;
        }
    }

    #endregion

    #region Properties

    public ClientGameManager GameManager { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    #endregion

    #region Initialization

    public async Task<bool> CreateClient()
    {
        GameManager = new ClientGameManager();
        return await GameManager.InitAsync();
    }

    #endregion
}
