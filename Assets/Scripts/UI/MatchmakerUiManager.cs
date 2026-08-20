using UnityEngine;

public class MatchmakerUiManager : MonoBehaviour
{
    public void OnLogoutButtonClicked()
    {
        AuthManager.Instance.SignOut();
    }
}
