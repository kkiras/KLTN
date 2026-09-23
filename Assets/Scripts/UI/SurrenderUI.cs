using KLTN.Game.Networking;
using UnityEngine;

public class SurrenderUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject surrenderConfirmPanel;
    [SerializeField] private GameObject settingsPanel;

    public void OpenConfirmation()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (surrenderConfirmPanel != null) surrenderConfirmPanel.SetActive(true);
    }

    public void CloseConfirmation()
    {
        if (surrenderConfirmPanel != null) surrenderConfirmPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void Surrender()
    {
        NetworkMatchBridge bridge = FindFirstObjectByType<NetworkMatchBridge>();
        if (bridge != null)
        {
            bridge.RequestSurrender();
        }
        
        if (surrenderConfirmPanel != null) surrenderConfirmPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }
}