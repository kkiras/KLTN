using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Legacy manual Relay API. Kept commented during the Sessions migration.
// using Unity.Services.Relay;
// using Unity.Services.Relay.Models;
// using Unity.Netcode.Transports.UTP;
// using Unity.Networking.Transport.Relay;

public class HostGameManager
{
    #region Constants

    private const string GAME_SCENE_NAME = "Board";

    #endregion

    #region Runtime State

    // Legacy manual Relay state. UgsSessionService now owns the active session.
    // private Allocation allocation;
    private string joinCode;

    // Legacy Relay capacity. SessionOptions.MaxPlayers includes the host.
    // private const int MaxConnections = 20;

    #endregion

    #region Properties

    public string JoinCode => joinCode;

    #endregion

    #region Hosting

    public async Task StartHostAsync()
    {
        /*
         * Legacy manual Relay flow. Kept for migration reference only.
         *
        try
        {
            allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return;
        }

        try
        {
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log(joinCode);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData relayServerData = allocation.ToRelayServerData("dtls");
        transport.SetRelayServerData(relayServerData);
        NetworkManager.Singleton.StartHost();

         */

        try
        {
            joinCode = await UgsSessionService.Instance.HostAsync();
            Debug.Log($"Session created. Join code: {joinCode}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(GAME_SCENE_NAME, LoadSceneMode.Single);
    }

    #endregion
}
