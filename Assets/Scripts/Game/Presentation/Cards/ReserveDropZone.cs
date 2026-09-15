using UnityEngine;
using UnityEngine.EventSystems;

namespace KLTN.Game.Presentation
{
    public sealed class ReserveDropZone : MonoBehaviour, IDropHandler
    {
        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private MatchDebugControls controls;

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null || contentRoot == null || controls == null)
            {
                return;
            }

            DraggableHandCard card =
                eventData.pointerDrag.GetComponent<DraggableHandCard>();

            if (card == null)
            {
                return;
            }

            if (card.HomeLocation == CardVisualLocation.Hand)
            {
                controls.TryStageSummon(card, contentRoot);

                return;
            }

            if (card.HomeLocation == CardVisualLocation.Reserve)
            {
                controls.TryReturnBoardCardToReserve(card);
            }
        }
    }
}
