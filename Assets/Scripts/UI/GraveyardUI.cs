using KLTN.Game.Networking;
using KLTN.Game.Presentation;
using UnityEngine;
using UnityEngine.UI;

public class GraveyardUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject graveyardPanel;
    [SerializeField] private RectTransform cardContainer;
    [SerializeField] private ScrollRect graveyardScrollRect;

    [Header("Card Presentation")]
    [SerializeField] private MatchBoardPresenter boardPresenter;

    [Header("Layout")]
    [SerializeField] private int centeredCardLimit = 3;

    private MatchClientProjection projection;
    private HorizontalLayoutGroup layoutGroup;
    private ContentSizeFitter contentSizeFitter;

    private void Awake()
    {
        if (cardContainer != null)
        {
            layoutGroup =
                cardContainer.GetComponent<HorizontalLayoutGroup>();

            contentSizeFitter =
                cardContainer.GetComponent<ContentSizeFitter>();
        }
    }

    private void OnEnable()
    {
        projection = MatchProjectionRegistry.Current;

        if (projection != null)
        {
            projection.SnapshotChanged += HandleSnapshotChanged;

            if (projection.Current != null)
            {
                HandleSnapshotChanged(projection.Current);
            }
        }
    }

    private void OnDisable()
    {
        if (projection != null)
        {
            projection.SnapshotChanged -= HandleSnapshotChanged;
        }
    }

    private void HandleSnapshotChanged(MatchSnapshotDto snapshot)
    {
        if (snapshot == null || snapshot.self == null)
        {
            return;
        }

        RefreshGraveyard(snapshot.self.graveyard);
    }

    private void RefreshGraveyard(CardViewDto[] graveyardCards)
    {
        if (cardContainer == null || boardPresenter == null)
        {
            return;
        }

        // Xóa các card Graveyard cũ khỏi UI.
        for (int i = cardContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(cardContainer.GetChild(i).gameObject);
        }

        int cardCount = graveyardCards?.Length ?? 0;

        // Chuyển layout dựa trên số lượng card.
        UpdateLayout(cardCount);

        if (cardCount == 0)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardContainer);
            return;
        }

        // Tạo visual cho từng card trong Graveyard.
        foreach (CardViewDto card in graveyardCards)
        {
            if (card == null)
            {
                continue;
            }

            NetworkCardVisual view =
                boardPresenter.CreateGraveyardCard(
                    card,
                    cardContainer
                );

            if (view == null)
            {
                Debug.LogWarning(
                    $"Could not create Graveyard card: {card.displayName}"
                );
            }
        }

        // Bắt Unity cập nhật layout ngay.
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardContainer);

        // Khi có nhiều card, luôn bắt đầu từ bên trái.
        if (graveyardScrollRect != null && cardCount > centeredCardLimit)
        {
            Canvas.ForceUpdateCanvases();
            graveyardScrollRect.horizontalNormalizedPosition = 0f;
        }

        Debug.Log(
            $"Graveyard UI refreshed. Cards={cardCount}"
        );
    }

    private void UpdateLayout(int cardCount)
    {
        bool shouldCenter = cardCount <= centeredCardLimit;

        // =========================
        // 1 - 3 CARD: CĂN GIỮA
        // =========================
        if (shouldCenter)
        {
            // Không để ContentSizeFitter tự thu nhỏ container.
            if (contentSizeFitter != null)
            {
                contentSizeFitter.horizontalFit =
                    ContentSizeFitter.FitMode.Unconstrained;
            }

            // Container rộng bằng Viewport.
            if (graveyardScrollRect != null &&
                graveyardScrollRect.viewport != null)
            {
                float viewportWidth =
                    graveyardScrollRect.viewport.rect.width;

                cardContainer.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    viewportWidth
                );
            }

            // Card nằm giữa.
            if (layoutGroup != null)
            {
                layoutGroup.childAlignment =
                    TextAnchor.MiddleCenter;
            }

            // Ít card thì không cần kéo ngang.
            if (graveyardScrollRect != null)
            {
                graveyardScrollRect.horizontal = false;
                graveyardScrollRect.velocity = Vector2.zero;
            }
        }

        // =========================
        // 4+ CARD: SCROLL NGANG
        // =========================
        else
        {
            // Container tự dài ra theo số card.
            if (contentSizeFitter != null)
            {
                contentSizeFitter.horizontalFit =
                    ContentSizeFitter.FitMode.PreferredSize;
            }

            // Card bắt đầu từ bên trái.
            if (layoutGroup != null)
            {
                layoutGroup.childAlignment =
                    TextAnchor.MiddleLeft;
            }

            // Cho phép kéo / vuốt ngang.
            if (graveyardScrollRect != null)
            {
                graveyardScrollRect.horizontal = true;
                graveyardScrollRect.velocity = Vector2.zero;
                graveyardScrollRect.horizontalNormalizedPosition = 0f;
            }
        }
    }

    public void OpenGraveyard()
    {
        if (graveyardPanel != null)
        {
            graveyardPanel.SetActive(true);
        }

        // Refresh lại khi mở Graveyard.
        if (projection != null && projection.Current != null)
        {
            HandleSnapshotChanged(projection.Current);
        }
    }

    public void CloseGraveyard()
    {
        if (graveyardPanel != null)
        {
            graveyardPanel.SetActive(false);
        }

        // Dừng quán tính khi đóng Graveyard.
        if (graveyardScrollRect != null)
        {
            graveyardScrollRect.velocity = Vector2.zero;
        }
    }
}