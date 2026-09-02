using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(UnityTransport))]
public sealed class DedicatedServerBootstrap : MonoBehaviour
{
    #region Configuration

    [Header("Server")]
    [SerializeField] private ushort listenPort = 7777;
    [SerializeField] private int requiredPlayerCount = 2;
    [SerializeField] private float emptyServerTimeoutSeconds = 120f;

    [Header("Editor Testing")]
    [SerializeField] private bool allowEditorDedicatedServer;

    #endregion

    #region Dependencies

    private NetworkManager networkManager;
    private UnityTransport transport;

    #endregion

    #region Runtime State

    private readonly HashSet<ulong> connectedClients = new();
    private CancellationTokenSource lifetimeCancellation;
    private bool gameSceneLoadStarted;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
        transport = GetComponent<UnityTransport>();
    }

    private void Start()
    {
        if (!IsDedicatedRuntime())
        {
            enabled = false;
            return;
        }

        StartDedicatedServer();
    }

    private void OnDestroy()
    {
        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();

        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    #endregion

    #region Server Startup

    private bool IsDedicatedRuntime()
    {
#if UNITY_SERVER
        return true;
#else
        return allowEditorDedicatedServer ||
               SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
#endif
    }

    private void StartDedicatedServer()
    {
        Application.runInBackground = true;
        string matchId = Environment.GetEnvironmentVariable("MATCH_ID") ?? "local-match";
        Debug.Log($"Starting dedicated server. Match ID: {matchId}");
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        transport.SetConnectionData("0.0.0.0", listenPort, "0.0.0.0");

        if (!networkManager.StartServer())
        {
            Debug.LogError("Failed to start dedicated server.");
            Application.Quit(1);
            return;
        }

        Debug.Log($"Dedicated server listening on 0.0.0.0:{listenPort}");
        lifetimeCancellation = new CancellationTokenSource();
        _ = QuitIfUnusedAsync(lifetimeCancellation.Token);
    }

    #endregion

    #region Client Lifecycle

    private void OnClientConnected(ulong clientId)
    {
        if (gameSceneLoadStarted)
        {
            networkManager.DisconnectClient(clientId);
            return;
        }

        if (connectedClients.Count >= requiredPlayerCount)
        {
            networkManager.DisconnectClient(clientId);
            return;
        }

        connectedClients.Add(clientId);
        Debug.Log($"Client connected: {clientId}. Players: {connectedClients.Count}/{requiredPlayerCount}");

        if (connectedClients.Count == requiredPlayerCount) { LoadGameScene(); }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        connectedClients.Remove(clientId);
        Debug.Log($"Client disconnected: {clientId}. Remaining players: {connectedClients.Count}");

        if (gameSceneLoadStarted && connectedClients.Count == 0)
        {
            Debug.Log("All clients left. Stopping server.");
            Application.Quit();
        }
    }

    #endregion

    #region Scene Flow

    private void LoadGameScene()
    {
        if (gameSceneLoadStarted) { return; }

        gameSceneLoadStarted = true;
        lifetimeCancellation?.Cancel();
        SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(
            SceneNames.GameScene,
            LoadSceneMode.Single);

        if (status != SceneEventProgressStatus.Started)
        {
            gameSceneLoadStarted = false;
            Debug.LogError($"Could not start GameScene load: {status}");
        }
    }

    #endregion

    #region Server Lifetime

    private async Task QuitIfUnusedAsync(CancellationToken cancellationToken)
    {
        try
        {
            int delayMilliseconds = Mathf.CeilToInt(emptyServerTimeoutSeconds * 1000f);
            await Task.Delay(delayMilliseconds, cancellationToken);

            if (connectedClients.Count == 0)
            {
                Debug.Log("Server remained unused. Quitting.");
                Application.Quit();
            }
        }
        catch (OperationCanceledException)
        {
            // A complete match was formed.
        }
    }

    #endregion
}
