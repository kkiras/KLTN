using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

// Legacy manual Relay/NGO setup. Kept commented during the Sessions migration.
// using Unity.Netcode;
// using Unity.Netcode.Transports.UTP;
// using Unity.Networking.Transport.Relay;
// using Unity.Services.Relay;
// using Unity.Services.Relay.Models;

public class ClientGameManager
{
    #region Constants

    private const string MENU_SCENE_NAME = "TestMenu";

    #endregion

    #region Runtime State

    // Legacy manual Relay state. UgsSessionService now owns the active session.
    // private JoinAllocation allocation;

    #endregion

    #region Initialization

    public async Task<bool> InitAsync()
    {
        // InitializationOptions options = new InitializationOptions();
        // options.SetProfile(System.Guid.NewGuid().ToString().Substring(0, 8));

        // await UnityServices.InitializeAsync(options);
        await UnityServices.InitializeAsync();
        AuthState authState = await AuthenticationWrapper.DoAuth();

        if (authState == AuthState.Authenticated) { return true; }

        return false;
    }

    #endregion

    #region Scene Navigation

    public void GoToMenu()
    {
        SceneManager.LoadScene(MENU_SCENE_NAME);
    }

    #endregion

    #region Session Join

    public async Task StartClientAsync(string joinCode)
    {
        /*
         * Legacy manual Relay flow. Kept for migration reference only.
         *
        try
        {
            allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        }
        catch (Exception e)
        {
            Debug.Log(e);
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData relayServerData = allocation.ToRelayServerData("dtls");
        transport.SetRelayServerData(relayServerData);
        NetworkManager.Singleton.StartClient();

         */

        try
        {
            await UgsSessionService.Instance.JoinByCodeAsync(joinCode);
            Debug.Log("Joined session successfully.");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    #endregion
}
