using System.Collections.Generic;
using CMCMProductions;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    #region Configuration and Card Data

    public List<Card> allCards = new List<Card>();
    public int maxHandSize = 5;

    #endregion

    #region Runtime State

    private int currentIndex;
    private Transform handPosition;
    private HandManager myHandManager;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        Card[] cards = Resources.LoadAll<Card>("Cards");
        allCards.AddRange(cards);
        myHandManager = FindAnyObjectByType<HandManager>();
        GameObject handPosObj = GameObject.Find("HandPosition");
        if (handPosObj != null) { handPosition = handPosObj.transform; }

        for (int i = 0; i < 2; i++)
        {
            DrawCard();
        }
    }

    #endregion

    #region Drawing

    public void DrawCard()
    {
        if (allCards.Count == 0 || myHandManager == null) { return; }

        if (handPosition != null && handPosition.childCount >= maxHandSize)
        {
            Debug.Log($"Tay đã đầy! Đang có {handPosition.childCount}/{maxHandSize} lá. Không thể rút thêm.");
            return;
        }

        Card nextCard = allCards[currentIndex];
        myHandManager.AddCardToHand(nextCard);
        currentIndex = (currentIndex + 1) % allCards.Count;
    }

    #endregion
}
