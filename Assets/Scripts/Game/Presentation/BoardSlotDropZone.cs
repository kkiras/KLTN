using UnityEngine;
using UnityEngine.EventSystems;

namespace KLTN.Game.Presentation
{
    public sealed class BoardSlotDropZone :
        MonoBehaviour,
        IDropHandler
    {
        [SerializeField]
        private int slotIndex;

        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private MatchDebugControls controls;

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null ||
                contentRoot == null ||
                controls == null)
            {
                return;
            }

            DraggableHandCard card =
                eventData.pointerDrag
                    .GetComponent<DraggableHandCard>();

            if (card == null)
            {
                return;
            }

            controls.TryStageBoardCard(
                card,
                slotIndex,
                contentRoot);
        }
    }
}