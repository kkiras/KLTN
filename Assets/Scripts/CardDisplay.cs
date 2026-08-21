using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CMCMProductions;


public class CardDisplay : MonoBehaviour
{
    public Card cardData;
    public Image cardImage;
    public TMP_Text healthText;
    public TMP_Text damageText;
    public Image[] typeImages;
    void Start()
    {
        UpdateCardDisplay();
    }

    public void UpdateCardDisplay()
    {
        healthText.text = cardData.health.ToString();
        damageText.text = cardData.damage.ToString();

        // //Update type images
        for (int i = 0; i < typeImages.Length; i++)
        {
            typeImages[i].gameObject.SetActive(false);
        }
        for (int i = 0; i < cardData.cardType.Count; i++)
        {
            Card.CardType type = cardData.cardType[i];
            int typeIndex = (int)type;
            if (typeIndex < typeImages.Length)
            {
                typeImages[typeIndex].gameObject.SetActive(true);
            }
        }
    }

}
