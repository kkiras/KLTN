using KLTN.Game.Networking;
using KLTN.Game.Presentation;
using UnityEngine;

public class GraveyardUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject graveyardPanel;
    [SerializeField] private RectTransform cardContainer;

    [Header("Card Presentation")]
    [SerializeField] private MatchBoardPresenter boardPresenter;

    private MatchClientProjection projection;

    private void OnEnable()
    {
        projection = MatchProjectionRegistry.Current;

        if (projection != null)
        {
            projection.SnapshotChanged += HandleSnapshotChanged;

            // Nếu snapshot đã tồn tại trước khi UI được enable,
            // cập nhật Graveyard ngay.
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

        if (graveyardCards == null || graveyardCards.Length == 0)
        {
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
                boardPresenter.CreateGraveyardCard(card, cardContainer);

            if (view == null)
            {
                Debug.LogWarning(
                    $"Could not create Graveyard card: {card.displayName}"
                );
            }
        }

        Debug.Log(
            $"Graveyard UI refreshed. Cards={graveyardCards.Length}"
        );
    }

    public void OpenGraveyard()
    {
        if (graveyardPanel != null)
        {
            graveyardPanel.SetActive(true);
        }

        // Refresh lại khi mở panel.
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
    }
}