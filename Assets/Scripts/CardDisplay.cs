using CMCMProductions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardDisplay : MonoBehaviour
{
    #region View References

    public Card cardData;
    public Image cardImage;
    public TMP_Text healthText;
    public TMP_Text damageText;
    public Image[] typeImages;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        UpdateCardDisplay();
    }

    #endregion

    #region Rendering

    public void UpdateCardDisplay()
    {
        healthText.text = cardData.health.ToString();
        damageText.text = cardData.damage.ToString();

        // Reset and then enable the icons represented by the card data.
        for (int i = 0; i < typeImages.Length; i++)
        {
            typeImages[i].gameObject.SetActive(false);
        }
        for (int i = 0; i < cardData.cardType.Count; i++)
        {
            Card.CardType type = cardData.cardType[i];
            int typeIndex = (int)type;
            if (typeIndex < typeImages.Length) { typeImages[typeIndex].gameObject.SetActive(true); }
        }
    }

    #endregion
}
