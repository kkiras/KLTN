using System.Collections.Generic;
using UnityEngine;

public sealed class AuthFeedbackView : MonoBehaviour
{
    #region Serialized Fields

    [Header("Notification")]
    [SerializeField] private RectTransform notificationContainer;
    [SerializeField] private AuthNotificationItem notificationPrefab;
    [SerializeField] private float displayDuration = 2f;

    [Header("Background Colors")]
    [SerializeField] private Color infoColor = new(0.15f, 0.35f, 0.65f, 0.9f);
    [SerializeField] private Color successColor = new(0.15f, 0.55f, 0.25f, 0.9f);
    [SerializeField] private Color errorColor = new(0.7f, 0.15f, 0.15f, 0.9f);

    [Header("Text")]
    [SerializeField] private Color textColor = Color.white;

    #endregion

    #region Runtime State

    private readonly List<AuthNotificationItem> activeItems = new();
    private readonly Queue<AuthNotificationItem> inactiveItems = new();

    #endregion

    #region Feedback

    public void ShowInfo(string message)
    {
        Show(message, infoColor);
    }

    public void ShowSuccess(string message)
    {
        Show(message, successColor);
    }

    public void ShowError(string message)
    {
        Show(message, errorColor);
    }

    public void Clear()
    {
        // HideImmediately modifies activeItems through OnItemHidden.
        while (activeItems.Count > 0)
        {
            AuthNotificationItem item = activeItems[activeItems.Count - 1];
            item.HideImmediately();
        }
    }

    private void Show(string message, Color backgroundColor)
    {
        if (string.IsNullOrWhiteSpace(message)) { return; }

        AuthNotificationItem item = GetOrCreateItem();
        activeItems.Add(item);
        item.Show(message, backgroundColor, textColor, displayDuration, OnItemHidden);
    }

    #endregion

    #region Pooling

    private AuthNotificationItem GetOrCreateItem()
    {
        AuthNotificationItem item;

        if (inactiveItems.Count > 0) { item = inactiveItems.Dequeue(); }
        else
        {
            item = Instantiate(notificationPrefab, notificationContainer);
        }

        item.transform.SetParent(notificationContainer, false);
        return item;
    }

    private void OnItemHidden(AuthNotificationItem item)
    {
        activeItems.Remove(item);

        // The item is inactive and ready for reuse.
        inactiveItems.Enqueue(item);
    }

    #endregion
}
