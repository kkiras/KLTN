using KLTN.Game.Networking;
using UnityEngine;

public class SurrenderUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject surrenderConfirmPanel;
    [SerializeField] private GameObject settingsPanel;

    // Bấm SURRENDER trong Settings
    public void OpenConfirmation()
    {
        // Đóng Settings trước
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        // Mở bảng xác nhận
        if (surrenderConfirmPanel != null)
        {
            surrenderConfirmPanel.SetActive(true);
        }
    }

    // Bấm CANCEL
    public void CloseConfirmation()
    {
        if (surrenderConfirmPanel != null)
        {
            surrenderConfirmPanel.SetActive(false);
        }

        // Quay trở lại Settings
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    // Bấm YES
    public void Surrender()
    {
        NetworkMatchBridge bridge =
            FindFirstObjectByType<NetworkMatchBridge>();

        if (bridge == null)
        {
            Debug.LogWarning("Không tìm thấy NetworkMatchBridge.");
            return;
        }

        bool sent = bridge.RequestSurrender();

        if (!sent)
        {
            Debug.LogWarning("Không thể gửi yêu cầu Surrender.");
            return;
        }

        // Đầu hàng thành công: đóng cả hai panel
        if (surrenderConfirmPanel != null)
        {
            surrenderConfirmPanel.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }
}