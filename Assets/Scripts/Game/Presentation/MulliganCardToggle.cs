using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MulliganCardToggle : MonoBehaviour, IPointerClickHandler
{
    public Image replaceOverlay;
    public ulong cardInstanceId;
    public bool isSelected = false;

    private Vector3 originalScale;

    private void Start()
    {

        originalScale = transform.localScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {

        isSelected = !isSelected;


        if (replaceOverlay != null)
        {
            replaceOverlay.gameObject.SetActive(isSelected);
        }


        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (isSelected)
        {

            canvasGroup.alpha = 0.4f;
            transform.localScale = originalScale * 0.85f;
        }
        else
        {

            canvasGroup.alpha = 1f;
            transform.localScale = originalScale;
        }
    }
}