using System.Threading.Tasks;
using UnityEngine;

public class ApplicationController : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] private ClientSingleton clientPrefab;
    [SerializeField] private HostSingleton hostPrefab;

    #endregion

    #region Unity Lifecycle

    private async void Start()
    {
        DontDestroyOnLoad(gameObject);
        await LauchInMode(SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null);
    }

    #endregion

    #region Startup Flow

    private async Task LauchInMode(bool isDedicatedServer)
    {
        if (isDedicatedServer) { return; }

        HostSingleton hostSingleton = Instantiate(hostPrefab);
        hostSingleton.CreateHost();
        ClientSingleton clientSingleton = Instantiate(clientPrefab);
        bool authenticated = await clientSingleton.CreateClient();

        if (authenticated)
        {
            Debug.Log("Authenticated");
            clientSingleton.GameManager.GoToMenu();
        }
    }

    #endregion
}
