using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class ServerManager : MonoBehaviour
{
    #region Constants

    private const string SCENE_NAME = "Board";

    #endregion

    #region Runtime State

    private int playerCount;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        bool isDedicatedServer = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
        if (isDedicatedServer)
        {
            Debug.Log("===> ServerManager: Detected Dedicated Server (NullGfxDevice).");
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null) { transport.SetConnectionData("0.0.0.0", 7777, "0.0.0.0"); }

            NetworkManager.Singleton.OnClientConnectedCallback += OnPlayerJoined;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnPlayerLeft;
            bool serverStarted = NetworkManager.Singleton.StartServer();
            Debug.Log($"===> ServerManager: StartServer() result = {serverStarted} (Port 7777)");
            _ = AutoCloseIfEmpty();
        }
    }

    #endregion

    #region Connection Events

    private void OnPlayerJoined(ulong clientId)
    {
        playerCount++;
        Debug.Log($"===> ServerManager: Player connected! ClientId: {clientId}. Total: {playerCount}/2");
        if (playerCount == 2)
        {
            Debug.Log("===> ServerManager: Đủ 2 người chơi! Đang chuyển tất cả sang Scene GameBoard...");
            NetworkManager.Singleton.SceneManager.LoadScene(SCENE_NAME, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private void OnPlayerLeft(ulong clientId)
    {
        playerCount--;
        Debug.Log($"===> ServerManager: Player disconnected. ClientId: {clientId}. Remaining: {playerCount}");
        if (playerCount <= 0)
        {
            Debug.Log("===> ServerManager: Không còn người chơi nào, tắt Server.");
            Application.Quit();
        }
    }

    #endregion

    #region Server Lifetime

    private async Task AutoCloseIfEmpty()
    {
        await Task.Delay(120000);
        if (playerCount == 0)
        {
            Debug.Log("===> ServerManager: Tắt Server sau 2 phút.");
            Application.Quit();
        }
    }

    #endregion
}
