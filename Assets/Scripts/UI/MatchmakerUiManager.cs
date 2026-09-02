using UnityEngine;

public class MatchmakerUiManager : MonoBehaviour
{
    #region UI Events

    public void OnLogoutButtonClicked()
    {
        AuthManager.Instance.SignOut();
    }

    #endregion
}
