using System.Collections;
using System.Collections.Generic;
using CMCMProductions;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    public List<Card> allCards = new List<Card>();
    private int currentIndex = 0;
    public int maxHandSize = 5;

    public HandManager localHand;
    public HandManager enemyHand;

    void Start()
    {
        Card[] cards = Resources.LoadAll<Card>("Cards");
        allCards.AddRange(cards);

        for (int i = 0; i < 3; i++)
        {
            DrawCardLocal();
            DrawCardEnemy();
        }
    }

    private void DrawCardToTarget(HandManager targetHand)
    {
        if (allCards.Count == 0 || targetHand == null)
            return;

        if (targetHand.cardsInHand.Count >= maxHandSize)
        {
            Debug.Log($"Tay đã đầy! Đang có {targetHand.cardsInHand.Count}/{maxHandSize} lá. Không thể rút thêm.");
            return;
        }

        Card nextCard = allCards[currentIndex];
        targetHand.AddCardToHand(nextCard);

        currentIndex = (currentIndex + 1) % allCards.Count;
    }

    public void DrawCardLocal()
    {
        DrawCardToTarget(localHand);
    }

    public void DrawCardEnemy()
    {
        DrawCardToTarget(enemyHand);
    }
}