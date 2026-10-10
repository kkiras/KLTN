using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KLTN.UI.Shop
{
    /// <summary>Modal "Bạn có đồng ý mua thẻ X không?" with Accept / Cancel.</summary>
    public sealed class ShopConfirmDialog : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button cancelButton;

        private Action pendingAccept;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            acceptButton?.onClick.AddListener(Accept);
            cancelButton?.onClick.AddListener(Hide);
            Hide();
        }

        public void Show(string message, Action onAccept)
        {
            pendingAccept = onAccept;

            if (messageText != null)
            {
                messageText.text = message;
            }

            root?.SetActive(true);
        }

        public void Hide()
        {
            pendingAccept = null;
            root?.SetActive(false);
        }

        private void Accept()
        {
            Action action = pendingAccept;
            Hide();
            action?.Invoke();
        }

#if UNITY_EDITOR
        public void EditorWire(GameObject dialogRoot, TextMeshProUGUI message, Button accept, Button cancel)
        {
            root = dialogRoot;
            messageText = message;
            acceptButton = accept;
            cancelButton = cancel;
        }
#endif
    }
}
