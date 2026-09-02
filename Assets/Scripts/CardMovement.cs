using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CardMovement : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    #region Serialized Fields

    [SerializeField] private float selectScale = 1.1f;
    [SerializeField] private Vector2 cardPlay;
    [SerializeField] private Vector3 playPosition;
    [SerializeField] private GameObject glowEffect;
    [SerializeField] private GameObject playArrow;
    [SerializeField] private float smoothSpeed = 20f;
    [SerializeField] private float hoverHoverOffsetY = 30f;

    #endregion

    #region Component References

    private RectTransform rectTransform;
    private Canvas canvas;

    #endregion

    #region Pointer Origin

    private Vector2 originalLocalPointerPosition;
    private Vector3 originalPanelLocalPosition;
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private Vector3 originalPosition;

    #endregion

    #region Animation State

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;
    private int currentState;
    private bool isReturningToHand;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        originalScale = rectTransform.localScale;
        originalPosition = rectTransform.localPosition;
        originalRotation = rectTransform.localRotation;
        targetPosition = originalPosition;
        targetRotation = originalRotation;
        targetScale = originalScale;
    }

    private void Update()
    {
        switch (currentState)
        {
            case 1:
                HandleHoverState();
                break;
            case 2:
                HandleDragState();
                if (Mouse.current != null && !Mouse.current.leftButton.isPressed) { TransitionToState0(); }
                break;
            case 3:
                HandlePlayState();
                if (Mouse.current != null && !Mouse.current.leftButton.isPressed) { TransitionToState0(); }
                break;
        }

        if (currentState != 0 || isReturningToHand)
        {
            rectTransform.localPosition = Vector3.Lerp(
                rectTransform.localPosition,
                targetPosition,
                Time.deltaTime * smoothSpeed);
            rectTransform.localRotation = Quaternion.Lerp(
                rectTransform.localRotation,
                targetRotation,
                Time.deltaTime * smoothSpeed);
            rectTransform.localScale = Vector3.Lerp(
                rectTransform.localScale,
                targetScale,
                Time.deltaTime * smoothSpeed);

            if (currentState == 0 && isReturningToHand)
            {
                if (Vector3.Distance(rectTransform.localPosition, targetPosition) < 0.5f)
                {
                    isReturningToHand = false;
                    rectTransform.localPosition = targetPosition;
                    rectTransform.localRotation = targetRotation;
                    rectTransform.localScale = targetScale;
                }
            }
        }
    }

    #endregion

    #region Pointer and Drag Events

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentState == 0)
        {
            if (!isReturningToHand) { originalScale = rectTransform.localScale; }

            currentState = 1;
            isReturningToHand = false;
            rectTransform.SetAsLastSibling();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentState == 1) { TransitionToState0(); }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (currentState == 1)
        {
            currentState = 2;
            rectTransform.SetAsLastSibling();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                eventData.position,
                eventData.pressEventCamera,
                out originalLocalPointerPosition);
            originalPanelLocalPosition = originalPosition;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (currentState == 2)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPointerPosition))
            {
                localPointerPosition /= canvas.scaleFactor;
                Vector3 offsetToOriginal = localPointerPosition - originalLocalPointerPosition;
                targetPosition = originalPanelLocalPosition + offsetToOriginal;

                if (targetPosition.y > cardPlay.y)
                {
                    currentState = 3;
                    playArrow.SetActive(true);
                }
            }
        }
    }

    #endregion

    #region State Transitions

    private void HandleHoverState()
    {
        glowEffect.SetActive(true);
        targetScale = originalScale * selectScale;
        targetPosition = originalPosition + new Vector3(0, hoverHoverOffsetY, 0);
        targetRotation = Quaternion.identity;
    }

    private void HandleDragState()
    {
        targetRotation = Quaternion.identity;
        targetScale = originalScale * selectScale;
    }

    private void HandlePlayState()
    {
        targetPosition = playPosition;
        targetRotation = Quaternion.identity;

        if (Mouse.current != null && Mouse.current.position.ReadValue().y < cardPlay.y)
        {
            currentState = 2;
            playArrow.SetActive(false);
        }
    }

    private void TransitionToState0()
    {
        currentState = 0;
        targetPosition = originalPosition;
        targetRotation = originalRotation;
        targetScale = originalScale;
        glowEffect.SetActive(false);
        playArrow.SetActive(false);
        isReturningToHand = true;
    }

    #endregion

    #region Layout Updates

    public void UpdateHomePosition(Vector3 newPos, Quaternion newRot)
    {
        originalPosition = newPos;
        originalRotation = newRot;

        if (currentState == 0 && !isReturningToHand)
        {
            rectTransform.localPosition = newPos;
            rectTransform.localRotation = newRot;
            targetPosition = newPos;
            targetRotation = newRot;
        }
    }

    #endregion
}
