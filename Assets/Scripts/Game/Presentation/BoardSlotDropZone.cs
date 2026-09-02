using UnityEngine;
using UnityEngine.EventSystems;

namespace KLTN.Game.Presentation
{
    public sealed class BoardSlotDropZone : MonoBehaviour, IDropHandler
    {
        #region Serialized Fields

        [SerializeField] private int slotIndex;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private MatchDebugControls controls;

        #endregion

        #region Drop Handling

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) { return; }

            DraggableHandCard card = eventData.pointerDrag.GetComponent<DraggableHandCard>();

            if (card == null || controls == null) { return; }

            controls.TryStageCard(card, slotIndex, contentRoot);
        }

        #endregion
    }
}
