using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KLTN.Game.Presentation
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(NetworkCardVisual))]
    public sealed class DraggableHandCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region Component References

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private NetworkCardVisual visual;
        private CardVisualLayout visualLayout;
        private RectTransform dragLayer;

        #endregion

        #region Hand Origin

        private Transform homeParent;
        private int homeSiblingIndex;
        private Vector2 homeAnchorMin;
        private Vector2 homeAnchorMax;
        private Vector2 homePivot;
        private bool homeCaptured;

        #endregion

        #region Pending Placement

        private RectTransform pendingParent;
        private bool isPending;

        #endregion

        #region Drag State

        private bool canDrag;
        private bool dragging;
        private bool dropAccepted;

        #endregion

        #region Properties

        public NetworkCardVisual Visual => visual;
        public bool IsPending => isPending;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            visual = GetComponent<NetworkCardVisual>();
            visualLayout = GetComponent<CardVisualLayout>();
        }

        #endregion

        #region Configuration

        public void Configure(bool isInteractable, RectTransform targetDragLayer)
        {
            canDrag = isInteractable;
            dragLayer = targetDragLayer;

            if (!homeCaptured) { CaptureHome(); }

            visual.SetInteractableVisual(isInteractable);
        }

        private void CaptureHome()
        {
            homeParent = transform.parent;
            homeSiblingIndex = transform.GetSiblingIndex();
            homeAnchorMin = rectTransform.anchorMin;
            homeAnchorMax = rectTransform.anchorMax;
            homePivot = rectTransform.pivot;
            homeCaptured = homeParent != null;
        }

        #endregion

        #region Drag Events

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!canDrag || dragLayer == null) { return; }

            if (!homeCaptured) { CaptureHome(); }

            dragging = true;
            dropAccepted = false;
            transform.SetParent(dragLayer, false);
            transform.SetAsLastSibling();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            canvasGroup.blocksRaycasts = false;
            MoveToPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) { return; }

            MoveToPointer(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) { return; }

            canvasGroup.blocksRaycasts = true;
            dragging = false;

            if (dropAccepted) { return; }

            if (isPending) { RestorePendingPlacement(); }
            else
            {
                RestoreToHand();
            }
        }

        #endregion

        #region Pending Placement

        public void PlacePending(RectTransform slotContent)
        {
            if (!dragging || slotContent == null) { return; }

            dropAccepted = true;
            isPending = true;
            pendingParent = slotContent;

            // A pending card may be dragged again.
            canDrag = true;
            ApplyBoardPlacement(slotContent);
            visual.SetPending(true);
            visual.SetInteractableVisual(true);
        }

        private void RestorePendingPlacement()
        {
            if (pendingParent == null)
            {
                RestoreToHand();
                return;
            }

            ApplyBoardPlacement(pendingParent);
            canDrag = true;
            visual.SetPending(true);
            visual.SetInteractableVisual(true);
        }

        private void ApplyBoardPlacement(RectTransform slotContent)
        {
            transform.SetParent(slotContent, false);
            visualLayout?.Apply(CardVisualLocation.Board);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        public void RestoreToHand()
        {
            if (!homeCaptured || homeParent == null) { return; }

            // Prevent OnEndDrag from restoring the previous pending slot.
            dropAccepted = true;
            isPending = false;
            pendingParent = null;
            transform.SetParent(homeParent, false);
            transform.SetSiblingIndex(homeSiblingIndex);
            visualLayout?.Apply(CardVisualLocation.Hand);
            rectTransform.anchorMin = homeAnchorMin;
            rectTransform.anchorMax = homeAnchorMax;
            rectTransform.pivot = homePivot;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            canDrag = true;
            visual.SetPending(false);
            visual.SetInteractableVisual(true);

            if (homeParent is RectTransform handRoot) { LayoutRebuilder.ForceRebuildLayoutImmediate(handRoot); }
        }

        #endregion

        #region Helpers

        private void MoveToPointer(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragLayer,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPosition))
            {
                rectTransform.anchoredPosition = localPosition;
            }
        }

        #endregion
    }
}
