using KLTN.Game.Networking;
using UnityEngine;

public class SurrenderUI : MonoBehaviour
{
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
        }
    }
}