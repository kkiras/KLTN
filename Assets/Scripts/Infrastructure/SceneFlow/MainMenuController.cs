using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MainMenuController : MonoBehaviour
{
    #region UI Events

    public void OpenFindMatch()
    {
        if (!EnsureSignedIn()) { return; }

        SceneManager.LoadScene(SceneNames.FindMatch);
    }

    public void OpenHostClientMenu()
    {
        if (!EnsureSignedIn()) { return; }

        SceneManager.LoadScene(SceneNames.HostClientMenu);
    }

    public void Logout()
    {
        AuthManager.Instance?.SignOut();
    }

    #endregion

    #region Validation

    private static bool EnsureSignedIn()
    {
        if (AuthenticationService.Instance.IsSignedIn) { return true; }

        Debug.LogError("UGS authentication is required before entering multiplayer.");
        SceneManager.LoadScene(SceneNames.Login);
        return false;
    }

    #endregion
}
