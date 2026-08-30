using System.Collections;
using System.Collections.Generic;
using CMCMProductions;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    public List<Card> allCards = new List<Card>();
    private int currentIndex = 0;
    public int maxHandSize = 5;
    private Transform handPosition;
    private HandManager myHandManager;

    void Start()
    {
        Card[] cards = Resources.LoadAll<Card>("Cards");
        allCards.AddRange(cards);

        myHandManager = FindObjectOfType<HandManager>();

        GameObject handPosObj = GameObject.Find("HandPosition");
        if (handPosObj != null)
        {
            handPosition = handPosObj.transform;
        }

        for (int i = 0; i < 2; i++)
        {
            DrawCard();
        }
    }

    public void DrawCard()
    {
        if (allCards.Count == 0 || myHandManager == null)
            return;
        if (handPosition != null && handPosition.childCount >= maxHandSize)
        {
            Debug.Log($"Tay đã đầy! Đang có {handPosition.childCount}/{maxHandSize} lá. Không thể rút thêm.");
            return;
        }

        Card nextCard = allCards[currentIndex];
        myHandManager.AddCardToHand(nextCard);

        currentIndex = (currentIndex + 1) % allCards.Count;
    }
}