using System.Collections.Generic;
using CMCMProductions;
using UnityEngine;
using UnityEngine.InputSystem;

public class HandManager : MonoBehaviour
{
    #region References and Card Views

    public DeckManager deckManager;
    public GameObject cardPrefab;
    public Transform handTransform;
    public float fanSpread = -7.5f;
    public float cardSpacing = 150f;
    public float verticalSpacing = 100f;
    public List<GameObject> cardsInHand = new List<GameObject>();

    #endregion

    #region Layout Configuration

    public float loweredYPosition = -600f;
    public float raisedYPosition = -380f;
    public float handLerpSpeed = 15f;

    #endregion

    #region Animation State

    private float currentTargetY;
    private float currentFanSpread;
    private float targetFanSpread;
    private float currentVerticalSpacing;
    private float targetVerticalSpacing;
    private Canvas mainCanvas;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        currentTargetY = loweredYPosition;
        mainCanvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        HandleHandHoverEffect();
    }

    #endregion

    #region Hand Contents

    public void AddCardToHand(Card cardData)
    {
        GameObject newCard = Instantiate(cardPrefab, handTransform.position, Quaternion.identity, handTransform);
        cardsInHand.Add(newCard);
        newCard.GetComponent<CardDisplay>().cardData = cardData;
        UpdateHandVisuals();
    }

    #endregion

    #region Hover Animation

    private void HandleHandHoverEffect()
    {
        if (Mouse.current == null) return;

        if (!Mouse.current.leftButton.isPressed)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            bool isHoveringAnyCard = false;
            Camera uiCamera = mainCanvas != null && mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : mainCanvas != null ? mainCanvas.worldCamera : null;
            float liftAmount = Mathf.Abs(raisedYPosition - loweredYPosition);

            foreach (GameObject card in cardsInHand)
            {
                if (card != null && card.activeInHierarchy)
                {
                    RectTransform cardRect = card.GetComponent<RectTransform>();

                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        cardRect,
                        mousePos,
                        uiCamera,
                        out Vector2 localPoint))
                    {
                        Rect expandedRect = cardRect.rect;
                        expandedRect.yMin -= liftAmount;

                        if (expandedRect.Contains(localPoint))
                        {
                            isHoveringAnyCard = true;
                            break;
                        }
                    }
                }
            }

            if (isHoveringAnyCard)
            {
                currentTargetY = raisedYPosition;
                targetFanSpread = fanSpread;
                targetVerticalSpacing = verticalSpacing;
            }
            else
            {
                currentTargetY = loweredYPosition;
                targetFanSpread = 0f;
                targetVerticalSpacing = 0f;
            }
        }

        Vector3 targetPos = new Vector3(handTransform.localPosition.x, currentTargetY, handTransform.localPosition.z);
        handTransform.localPosition = Vector3.Lerp(
            handTransform.localPosition,
            targetPos,
            Time.deltaTime * handLerpSpeed);
        bool isSpreadChanging = Mathf.Abs(currentFanSpread - targetFanSpread) > 0.01f;
        bool isVerticalChanging = Mathf.Abs(currentVerticalSpacing - targetVerticalSpacing) > 0.01f;

        if (isSpreadChanging || isVerticalChanging)
        {
            currentFanSpread = Mathf.Lerp(currentFanSpread, targetFanSpread, Time.deltaTime * handLerpSpeed);
            currentVerticalSpacing = Mathf.Lerp(
                currentVerticalSpacing,
                targetVerticalSpacing,
                Time.deltaTime * handLerpSpeed);
            UpdateHandVisuals();
        }
        else
        {
            if (currentFanSpread != targetFanSpread || currentVerticalSpacing != targetVerticalSpacing)
            {
                currentFanSpread = targetFanSpread;
                currentVerticalSpacing = targetVerticalSpacing;
                UpdateHandVisuals();
            }
        }
    }

    #endregion

    #region Layout

    public void UpdateHandVisuals()
    {
        int cardCount = cardsInHand.Count;
        if (cardCount == 0) return;

        if (cardCount == 1)
        {
            ApplyTransformToCard(cardsInHand[0], Vector3.zero, Quaternion.identity);
            return;
        }

        for (int i = 0; i < cardCount; i++)
        {
            float rotationAngle = (currentFanSpread * (i - (cardCount - 1) / 2f));
            float horizontalOffset = (cardSpacing * (i - (cardCount - 1) / 2f));
            float normallizedPosition = (2f * i / (cardCount - 1) - 1f);
            float verticalOffset = currentVerticalSpacing * (1 - normallizedPosition * normallizedPosition);
            Vector3 newPos = new Vector3(horizontalOffset, verticalOffset, 0f);
            Quaternion newRot = Quaternion.Euler(0f, 0f, rotationAngle);
            ApplyTransformToCard(cardsInHand[i], newPos, newRot);
        }
    }

    private void ApplyTransformToCard(GameObject card, Vector3 pos, Quaternion rot)
    {
        CardMovement cm = card.GetComponent<CardMovement>();
        if (cm != null) { cm.UpdateHomePosition(pos, rot); }
        else
        {
            card.transform.localPosition = pos;
            card.transform.localRotation = rot;
        }
    }

    #endregion
}
