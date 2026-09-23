using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class MatchResultPresenter : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TextMeshProUGUI resultTitleText;

    public void ShowResult(bool isVictory)
    {
       
        resultPanel.SetActive(true);

        if (isVictory)
        {
            resultTitleText.text = "CHIẾN THẮNG";
            ColorUtility.TryParseHtmlString("#FFD700", out Color goldColor);
            resultTitleText.color = goldColor;
        }
        else
        {
            resultTitleText.text = "THẤT BẠI";
            ColorUtility.TryParseHtmlString("#CC0000", out Color bloodRed);
            resultTitleText.color = bloodRed;
        }
    }

    public void OnReturnButtonClicked()
    {
        Debug.Log("Ngắt kết nối mạng và quay về Main Menu...");
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        SceneManager.LoadScene("MainMenu");
    }
}