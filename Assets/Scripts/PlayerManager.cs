using UnityEngine;
using TMPro;

public class PlayerManager : MonoBehaviour
{
    public bool isLocalPlayer;

    public HandManager myHandManager;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private int maxHealth = 20;
    [SerializeField] private int maxMana = 10;

    private int currentHealth;
    private int currentMana;

    void Start()
    {
        currentHealth = maxHealth;
        currentMana = 1;

        UpdateUI();
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
        UpdateUI();
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
        UpdateUI();
    }

    public void StartTurn(int manaToGive)
    {
        currentMana += manaToGive;
        if (currentMana > maxMana)
        {
            currentMana = maxMana;
        }
        UpdateUI();
    }

    public bool TrySpendMana(int cost)
    {
        if (currentMana >= cost)
        {
            currentMana -= cost;
            UpdateUI();
            return true;
        }

        Debug.Log("Không đủ Mana để đánh lá bài này!");
        return false;
    }

    private void Die()
    {
        if (isLocalPlayer)
        {
            Debug.Log("DEFEAT! Bạn đã thua trận.");
        }
        else
        {
            Debug.Log("VICTORY! Đối thủ đã bị hạ gục.");
        }
    }

    private void UpdateUI()
    {
        if (healthText != null)
        {
            healthText.text = currentHealth.ToString();
        }

        if (manaText != null)
        {
            manaText.text = currentMana.ToString() + "/" + maxMana.ToString();
        }
    }
}