using UnityEngine;
using UnityEngine.UI;

namespace KLTN.Game.Presentation
{
    public class KeywordGlossaryModal : MonoBehaviour
    {
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (backdropButton != null) backdropButton.onClick.AddListener(Hide);
            Hide();
        }

        private void Update()
        {
            if (modalRoot != null && modalRoot.activeSelf && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
            }
        }

        public void Show()
        {
            if (modalRoot != null) modalRoot.SetActive(true);
            else gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (modalRoot != null) modalRoot.SetActive(false);
            else gameObject.SetActive(false);
        }
    }
}