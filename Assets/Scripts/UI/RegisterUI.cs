using System;
using TMPro;
using UnityEngine;

public sealed class RegisterUI : MonoBehaviour
{
    #region Serialized Fields

    [Header("Inputs")]
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_InputField confirmPasswordInput;

    [Header("Navigation")]
    [SerializeField] private AuthUIManager authUIManager;
    [SerializeField] private LoginUI loginUI;

    [Header("UI State")]
    [SerializeField] private AuthFeedbackView feedbackView;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        emailInput.contentType = TMP_InputField.ContentType.EmailAddress;
        passwordInput.contentType = TMP_InputField.ContentType.Password;
        confirmPasswordInput.contentType = TMP_InputField.ContentType.Password;
    }

    private void OnEnable()
    {
        feedbackView?.Clear();

        if (confirmPasswordInput != null) { confirmPasswordInput.onSubmit.AddListener(OnConfirmPasswordSubmitted); }
    }

    private void OnDisable()
    {
        if (confirmPasswordInput != null) { confirmPasswordInput.onSubmit.RemoveListener(OnConfirmPasswordSubmitted); }
    }

    #endregion

    #region UI Events

    public async void OnRegisterButtonClicked()
    {
        if (authUIManager == null ||
            authUIManager.IsBusy)
        {
            return;
        }

        string email = emailInput.text.Trim();
        string password = passwordInput.text;
        string confirmPassword = confirmPasswordInput.text;
        feedbackView?.Clear();

        if (!AuthInputValidator.TryValidateRegistration(email, password, confirmPassword, out string validationError))
        {
            feedbackView?.ShowError(validationError);
            return;
        }

        if (!authUIManager.TryBeginOperation("Đang tạo tài khoản...")) { return; }

        AuthResultData result;

        try
        {
            result = await AuthManager.Instance.ProcessRegister(email, password);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            result = AuthResultData.Failure("REGISTER_UI_EXCEPTION", "Đăng ký thất bại. Vui lòng thử lại.");
        }
        finally
        {
            if (authUIManager != null) { authUIManager.EndOperation(); }
        }

        if (this == null) { return; }

        if (!result.Success)
        {
            feedbackView?.ShowError(GetSafeErrorMessage(result));
            return;
        }

        passwordInput.text = string.Empty;
        confirmPasswordInput.text = string.Empty;

        // Activate Login first so its OnEnable has already cleared old feedback.
        authUIManager.ShowLogin();
        loginUI.PrepareAfterRegistration(email);
    }

    #endregion

    #region Input Events

    private void OnConfirmPasswordSubmitted(string _)
    {
        OnRegisterButtonClicked();
    }

    #endregion

    #region Error Presentation

    private static string GetSafeErrorMessage(AuthResultData result)
    {
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage)) { return result.ErrorMessage; }

        return "Đăng ký thất bại. Vui lòng thử lại.";
    }

    #endregion
}
