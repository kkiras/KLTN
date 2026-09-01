using UnityEditorInternal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CardMovement : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform rectTransform;
    private Canvas canvas;
    private Vector2 originalLocalPointerPosition;
    private Vector3 originalPanelLocalPosition;
    private Vector3 originalScale;
    private int currentState = 0;
    private Quaternion originalRotation;
    private Vector3 originalPosition;
    private Transform originalParent;

    [SerializeField] private float selectScale = 1.1f;
    [SerializeField] private Vector2 cardPlay;
    [SerializeField] private Vector3 playPosition;
    [SerializeField] private GameObject glowEffect;
    [SerializeField] private GameObject playArrow;
    [SerializeField] private float smoothSpeed = 20f;
    [SerializeField] private float hoverHoverOffsetY = 30f;
    [SerializeField] private float maxDragY = 0f;

    public bool isEnemy = false;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;

    private bool isReturningToHand = false;
    public HandManager myHandManager;
    public BoardManager playerBoard;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        originalScale = rectTransform.localScale;
        originalPosition = rectTransform.localPosition;
        originalRotation = rectTransform.localRotation;

        originalParent = transform.parent;

        targetPosition = originalPosition;
        targetRotation = originalRotation;
        targetScale = originalScale;
    }

    void Update()
    {
        switch (currentState)
        {
            case 1:
                HandleHoverState();
                break;
            case 2:
                HandleDragState();
                if (Mouse.current != null && !Mouse.current.leftButton.isPressed)
                {
                    TransitionToState0();
                }
                break;
            case 3:
                HandlePlayState();
                if (Mouse.current != null && !Mouse.current.leftButton.isPressed)
                {
                    PlayCardDown();
                }
                break;
        }

        if (currentState != 0 || isReturningToHand)
        {
            rectTransform.localPosition = Vector3.Lerp(rectTransform.localPosition, targetPosition, Time.deltaTime * smoothSpeed);
            rectTransform.localRotation = Quaternion.Lerp(rectTransform.localRotation, targetRotation, Time.deltaTime * smoothSpeed);
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.deltaTime * smoothSpeed);

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

    private void TransitionToState0()
    {
        currentState = 0;
        transform.SetParent(originalParent, true);
        targetPosition = originalPosition;
        targetRotation = originalRotation;
        targetScale = originalScale;

        glowEffect.SetActive(false);
        playArrow.SetActive(false);

        isReturningToHand = true;

        if (myHandManager != null) myHandManager.isCardBeingDragged = false;
        if (playerBoard != null) playerBoard.HideAllSlots();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isEnemy) return;
        if (currentState == 0)
        {
            if (!isReturningToHand)
            {
                originalScale = rectTransform.localScale;
            }

            currentState = 1;
            isReturningToHand = false;

            rectTransform.SetAsLastSibling();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isEnemy) return;
        if (currentState == 1)
        {
            TransitionToState0();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isEnemy) return;
        if (currentState == 1)
        {
            currentState = 2;
            transform.SetParent(canvas.transform, true);
            rectTransform.SetAsLastSibling();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.GetComponent<RectTransform>(), eventData.position, eventData.pressEventCamera, out originalLocalPointerPosition);
            originalPanelLocalPosition = rectTransform.localPosition;

            targetPosition = originalPanelLocalPosition;

            if (myHandManager != null)
            {
                myHandManager.isCardBeingDragged = true;
            }

            EnsurePlayerBoard();

            if (playerBoard != null)
            {
                playerBoard.ShowEmptySlots();
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isEnemy) return;
        if (currentState == 2 || currentState == 3)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.GetComponent<RectTransform>(), eventData.position, eventData.pressEventCamera, out Vector2 localPointerPosition))
            {
                localPointerPosition /= canvas.scaleFactor;
                Vector3 offsetToOriginal = localPointerPosition - originalLocalPointerPosition;
                Vector3 newTargetPos = originalPanelLocalPosition + offsetToOriginal;

                if (newTargetPos.y > maxDragY)
                {
                    newTargetPos.y = maxDragY;
                }

                targetPosition = newTargetPos;

                if (newTargetPos.y > cardPlay.y)
                {
                    currentState = 3;
                    playArrow.SetActive(true);
                }
                else
                {
                    currentState = 2;
                    playArrow.SetActive(false);
                }
            }
        }
    }

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
        targetRotation = Quaternion.identity;
        targetScale = originalScale * selectScale;
    }

    private void PlayCardDown()
    {
        EnsurePlayerBoard();

        if (playerBoard != null)
        {
            int targetSlotIndex = playerBoard.GetClosestEmptySlot(rectTransform.position);
            if (targetSlotIndex != -1)
            {
                if (myHandManager != null)
                {
                    myHandManager.RemoveCardFromHand(this.gameObject);
                    myHandManager.isCardBeingDragged = false;
                }

                playerBoard.AddCardToSlot(this.gameObject, targetSlotIndex);
                glowEffect.SetActive(false);
                playArrow.SetActive(false);

                playerBoard.HideAllSlots();
            }
            else
            {
                Debug.Log("Không có ô trống nào trên bàn!");
                TransitionToState0();
            }
        }
        else
        {
            TransitionToState0();
        }
    }

    private void EnsurePlayerBoard()
    {
        if (playerBoard == null)
        {
            BoardManager[] allBoards = Object.FindObjectsByType<BoardManager>(FindObjectsSortMode.None);
            foreach (BoardManager board in allBoards)
            {
                if (!board.isEnemyBoard)
                {
                    playerBoard = board;
                    break;
                }
            }
        }
    }

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
}