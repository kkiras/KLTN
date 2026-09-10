using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MulliganCardToggle : MonoBehaviour, IPointerClickHandler
{
    public Image replaceOverlay; // Dành cho lớp phủ màu (nếu có)
    public ulong cardInstanceId;
    public bool isSelected = false;

    private Vector3 originalScale;

    private void Start()
    {
        // Lưu lại kích thước gốc của lá bài lúc mới sinh ra
        originalScale = transform.localScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Đảo ngược trạng thái chọn
        isSelected = !isSelected;

        // ƯU TIÊN 1: Nếu bạn có tạo lớp Overlay màu đen/đỏ trong Prefab thì bật/tắt nó
        if (replaceOverlay != null)
        {
            replaceOverlay.gameObject.SetActive(isSelected);
        }

        // ƯU TIÊN 2: Tự động tạo hiệu ứng "bị loại bỏ" cực ngầu bằng code
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (isSelected)
        {
            // Khi bị đánh dấu: Làm mờ lá bài và thu nhỏ lại 15%
            canvasGroup.alpha = 0.4f;
            transform.localScale = originalScale * 0.85f;
        }
        else
        {
            // Khi bỏ đánh dấu: Sáng lên và to lại như bình thường
            canvasGroup.alpha = 1f;
            transform.localScale = originalScale;
        }
    }
}