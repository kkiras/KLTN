using UnityEngine;
using UnityEngine.EventSystems;

namespace KLTN.Game.Presentation
{
    public sealed class PendingCardReturnDropZone : MonoBehaviour, IDropHandler
    {
        #region Serialized Fields

        [SerializeField] private MatchDebugControls controls;

        #endregion

        #region Drop Handling

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null ||
                controls == null)
            {
                return;
            }

            DraggableHandCard card = eventData.pointerDrag.GetComponent<DraggableHandCard>();

            if (card == null || !card.IsPending) { return; }

            controls.TryReturnPendingCard(card);
        }

        #endregion
    }
}
