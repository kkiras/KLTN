using UnityEngine;

public sealed class GoogleLoginUI : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] private AuthUIManager authUIManager;
    [SerializeField] private AuthFeedbackView feedbackView;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (GoogleAuthService.Instance == null) { return; }

        GoogleAuthService.Instance.OnGoogleLoginFailed += OnGoogleLoginFailed;
    }

    private void OnDisable()
    {
        if (GoogleAuthService.Instance == null) { return; }

        GoogleAuthService.Instance.OnGoogleLoginFailed -= OnGoogleLoginFailed;
    }

    #endregion

    #region UI Events

    public void OnGoogleLoginClicked()
    {
        if (!authUIManager.TryBeginOperation("Đang chờ đăng nhập Google...")) { return; }

        feedbackView?.Clear();
        GoogleAuthService.Instance.StartGoogleLogin();
    }

    #endregion

    #region Authentication Events

    private void OnGoogleLoginFailed()
    {
        authUIManager?.EndOperation();
        feedbackView?.ShowError("Đăng nhập Google thất bại hoặc đã bị hủy.");
    }

    #endregion

}
