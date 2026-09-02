using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AuthNotificationItem : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text messageLabel;

    #endregion

    #region Runtime State

    private Coroutine lifetimeCoroutine;
    private Action<AuthNotificationItem> hiddenCallback;

    #endregion

    #region Notification Lifecycle

    public void Show(
        string message,
        Color backgroundColor,
        Color textColor,
        float duration,
        Action<AuthNotificationItem> onHidden)
    {
        StopLifetimeCoroutine();
        hiddenCallback = onHidden;
        messageLabel.text = message;
        messageLabel.color = textColor;
        backgroundImage.color = backgroundColor;

        // New notifications render above older notifications.
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        lifetimeCoroutine = StartCoroutine(HideAfterDelay(duration));
    }

    public void HideImmediately()
    {
        StopLifetimeCoroutine();
        Hide();
    }

    private IEnumerator HideAfterDelay(float duration)
    {
        // Continues counting even if Time.timeScale is zero.
        yield return new WaitForSecondsRealtime(duration);
        lifetimeCoroutine = null;
        Hide();
    }

    private void Hide()
    {
        if (!gameObject.activeSelf) { return; }

        Action<AuthNotificationItem> callback = hiddenCallback;
        hiddenCallback = null;
        messageLabel.text = string.Empty;
        gameObject.SetActive(false);
        callback?.Invoke(this);
    }

    #endregion

    #region Helpers

    private void StopLifetimeCoroutine()
    {
        if (lifetimeCoroutine == null) { return; }

        StopCoroutine(lifetimeCoroutine);
        lifetimeCoroutine = null;
    }

    #endregion
}
