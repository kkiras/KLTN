using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;

public class UGSInitializer : MonoBehaviour
{
    #region Unity Lifecycle

    private async void Awake()
    {
        await Initialize();
    }

    #endregion

    #region Initialization

    public static async Task Initialize()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized) { return; }

        try
        {
            await UnityServices.InitializeAsync();
            Debug.Log("Unity Gaming Services initialized.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Unity Gaming Services initialization failed: {e}");
        }
    }

    #endregion
}
