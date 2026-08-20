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
    
    void Start()
    {
        UpdateCardDisplay();    
    }

    public void UpdateCardDisplay()
    {
        healthText.text = cardData.health.ToString();
        damageText.text = cardData.damage.ToString();
    }
    
}
