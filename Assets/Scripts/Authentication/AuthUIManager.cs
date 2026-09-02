using TMPro;
using UnityEngine;

public sealed class AuthUIManager : MonoBehaviour
{
    #region Serialized Fields

    [Header("Forms")]
    [SerializeField] private GameObject loginMethod;
    [SerializeField] private GameObject loginForm;
    [SerializeField] private GameObject registerForm;

    [Header("Interaction")]
    [SerializeField] private CanvasGroup formsCanvasGroup;
    [SerializeField] private GameObject loadingOverlay;
    [SerializeField] private TMP_Text loadingLabel;

    #endregion

    #region State

    public bool IsBusy { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        IsBusy = false;

        if (formsCanvasGroup != null)
        {
            formsCanvasGroup.alpha = 1f;
            formsCanvasGroup.interactable = true;
            formsCanvasGroup.blocksRaycasts = true;
        }

        SetLoadingVisible(false);
    }

    #endregion

    #region Operation State

    public bool TryBeginOperation(string message)
    {
        if (IsBusy) { return false; }

        IsBusy = true;

        if (formsCanvasGroup != null)
        {
            formsCanvasGroup.interactable = false;
            formsCanvasGroup.blocksRaycasts = false;
        }

        if (loadingLabel != null) { loadingLabel.text = message; }

        SetLoadingVisible(true);
        return true;
    }

    public void EndOperation()
    {
        IsBusy = false;

        if (formsCanvasGroup != null)
        {
            formsCanvasGroup.interactable = true;
            formsCanvasGroup.blocksRaycasts = true;
        }

        SetLoadingVisible(false);
    }

    private void SetLoadingVisible(bool visible)
    {
        if (loadingOverlay != null) { loadingOverlay.SetActive(visible); }
    }

    #endregion

    #region Panel Navigation

    public void ShowLoginMethods()
    {
        if (IsBusy) { return; }

        SetPanelState(showLoginMethods: true, showLogin: false, showRegister: false);
    }

    public void ShowLogin()
    {
        if (IsBusy) { return; }

        SetPanelState(showLoginMethods: false, showLogin: true, showRegister: false);
    }

    public void ShowRegister()
    {
        if (IsBusy) { return; }

        SetPanelState(showLoginMethods: false, showLogin: false, showRegister: true);
    }

    private void SetPanelState(bool showLoginMethods, bool showLogin, bool showRegister)
    {
        loginMethod.SetActive(showLoginMethods);
        loginForm.SetActive(showLogin);
        registerForm.SetActive(showRegister);
    }

    #endregion
}
