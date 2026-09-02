using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class LoadingSpinner : MonoBehaviour
{
    #region Configuration

    [SerializeField] private float rotationSpeed = 240f;
    [SerializeField] private bool clockwise = true;

    #endregion

    #region Dependencies

    private RectTransform rectTransform;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        rectTransform.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        float direction = clockwise ? -1f : 1f;
        float rotationAmount = direction * rotationSpeed * Time.unscaledDeltaTime;
        rectTransform.Rotate(0f, 0f, rotationAmount);
    }

    #endregion
}
