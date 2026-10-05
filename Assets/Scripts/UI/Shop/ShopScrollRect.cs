using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KLTN.UI.Shop
{
    /// <summary>
    /// ScrollRect for the shop list: mouse wheel / scrollbar only (no press-and-drag),
    /// clamped at both ends and without inertia, so there is no elastic bounce or jitter
    /// when the list is already at the top or bottom.
    /// </summary>
    public sealed class ShopScrollRect : ScrollRect
    {
        protected override void Awake()
        {
            base.Awake();
            movementType = MovementType.Clamped;
            inertia = false;
            horizontal = false;
        }

        public override void OnInitializePotentialDrag(PointerEventData eventData) { }

        public override void OnBeginDrag(PointerEventData eventData) { }

        public override void OnDrag(PointerEventData eventData) { }

        public override void OnEndDrag(PointerEventData eventData) { }
    }
}
