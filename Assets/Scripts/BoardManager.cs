using System.Collections.Generic;
using CMCMProductions;
using Mono.Cecil;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public bool isEnemyBoard;
    public int maxCardsOnBoard = 6;
    public float cardSpacing = 210f;
    public float lerpSpeed = 15f;

    public Vector3 cardScaleOnBoard = new Vector3(1f, 1f, 1f);

    public GameObject slotPrefab;
    public GameObject[] cardsInSlots;
    public Vector3[] slotPositions;
    private GameObject[] visualSlots;

    void Awake()
    {
        cardsInSlots = new GameObject[maxCardsOnBoard];
        slotPositions = new Vector3[maxCardsOnBoard];
        visualSlots = new GameObject[maxCardsOnBoard];

        float startX = -(maxCardsOnBoard - 1) * cardSpacing / 2f;

        for (int i = 0; i < maxCardsOnBoard; i++)
        {
            slotPositions[i] = new Vector3(startX + (i * cardSpacing), 0, 0);

            if (slotPrefab != null)
            {
                GameObject visualSlot = Instantiate(slotPrefab, transform);
                visualSlot.transform.localPosition = slotPositions[i];
                visualSlot.transform.localScale = Vector3.one;
                visualSlot.transform.SetAsFirstSibling();

                visualSlot.SetActive(false);
                visualSlots[i] = visualSlot;
            }
        }
    }

    void Update()
    {
        UpdateBoardVisuals();
    }

    private void UpdateBoardVisuals()
    {
        for (int i = 0; i < maxCardsOnBoard; i++)
        {
            if (cardsInSlots[i] != null)
            {
                RectTransform cardRect = cardsInSlots[i].GetComponent<RectTransform>();

                Vector3 targetScale = Vector3.one;

                if (visualSlots != null && visualSlots[i] != null)
                {
                    RectTransform slotRect = visualSlots[i].GetComponent<RectTransform>();

                    float scaleX = slotRect.rect.width / cardRect.rect.width;
                    float scaleY = slotRect.rect.height / cardRect.rect.height;

                    targetScale = new Vector3(scaleX, scaleY, 1f);
                }

                if (Vector3.Distance(cardRect.localPosition, slotPositions[i]) > 0.01f)
                {
                    cardRect.localPosition = Vector3.Lerp(cardRect.localPosition, slotPositions[i], Time.deltaTime * lerpSpeed);
                    cardRect.localRotation = Quaternion.Lerp(cardRect.localRotation, Quaternion.identity, Time.deltaTime * lerpSpeed);

                    cardRect.localScale = Vector3.Lerp(cardRect.localScale, targetScale, Time.deltaTime * lerpSpeed);
                }
                else
                {
                    cardRect.localPosition = slotPositions[i];
                    cardRect.localRotation = Quaternion.identity;

                    cardRect.localScale = targetScale;
                }
            }
        }
    }

    public void AddCardToSlot(GameObject card, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxCardsOnBoard || cardsInSlots[slotIndex] != null)
        {
            Debug.Log("Slot không hợp lệ hoặc đã có bài!");
            return;
        }

        cardsInSlots[slotIndex] = card;
        card.transform.SetParent(this.transform, true);

        CardMovement cm = card.GetComponent<CardMovement>();
        if (cm != null) Destroy(cm);
    }

    public void AddCardToBoard(GameObject card)
    {
        for (int i = 0; i < maxCardsOnBoard; i++)
        {
            if (cardsInSlots[i] == null)
            {
                AddCardToSlot(card, i);
                return;
            }
        }
        Debug.Log("Bàn đã đầy!");
    }

    public int GetClosestEmptySlot(Vector3 cardWorldPosition)
    {
        int bestSlot = -1;
        float minDistance = float.MaxValue;

        Vector3 localPos = transform.InverseTransformPoint(cardWorldPosition);

        for (int i = 0; i < maxCardsOnBoard; i++)
        {
            if (cardsInSlots[i] == null)
            {
                float distance = Vector3.Distance(localPos, slotPositions[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestSlot = i;
                }
            }
        }

        return bestSlot;
    }

    public void ShowEmptySlots()
    {
        if (visualSlots == null) return;

        for (int i = 0; i < maxCardsOnBoard; i++)
        {
            if (visualSlots[i] != null && cardsInSlots[i] == null)
            {
                visualSlots[i].SetActive(true);
            }
        }
    }

    public void HideAllSlots()
    {
        if (visualSlots == null) return;

        for (int i = 0; i < maxCardsOnBoard; i++)
        {
            if (visualSlots[i] != null)
            {
                visualSlots[i].SetActive(false);
            }
        }
    }
}