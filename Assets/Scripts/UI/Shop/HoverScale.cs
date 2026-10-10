using UnityEngine;
using UnityEngine.EventSystems;

namespace KLTN.UI.Shop
{
    /// <summary>Smoothly scales the element up while hovered (default 1.0 -> 1.2).</summary>
    public sealed class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float hoverScale = 1.2f;
        [SerializeField] private float speed = 14f;

        private Vector3 baseScale = Vector3.one;
        private float target = 1f;
        private float current = 1f;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            // The hover overlay hides this button; reset so it reappears at 1.0.
            target = current = 1f;
            transform.localScale = baseScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            target = hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            target = 1f;
        }

        private void Update()
        {
            if (Mathf.Approximately(current, target))
            {
                return;
            }

            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));

            if (Mathf.Abs(current - target) < 0.002f)
            {
                current = target;
            }

            transform.localScale = baseScale * current;
        }
    }
}
