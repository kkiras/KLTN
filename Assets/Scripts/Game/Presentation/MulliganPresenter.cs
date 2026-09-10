using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KLTN.Game.Networking;
using KLTN.Game.Content;
using KLTN.Game.Presentation;
using CMCMProductions;

public class MulliganPresenter : MonoBehaviour
{
    [Header("UI References")]
    public Transform cardsContainer;
    public GameObject networkCardPrefab;

    [Header("Action Button Override")]
    public Button actionButton;
    public TextMeshProUGUI actionButtonText;

    private List<ulong> selectedToReplace = new List<ulong>();
    private bool hasConfirmed = false;
    private CardAssetCatalog catalog;

    private void Awake()
    {
        catalog = new CardAssetCatalog();
    }

    public void UpdateMulliganState(MatchSnapshotDto snapshot)
    {
        if (snapshot.roundNumber == 0 && !hasConfirmed)
        {
            gameObject.SetActive(true);
            actionButtonText.text = "XÁC NHẬN";

            actionButton.onClick.RemoveListener(OnConfirmClicked);
            actionButton.onClick.AddListener(OnConfirmClicked);
            actionButton.interactable = true;

            if (cardsContainer.childCount == 0)
            {
                foreach (var cardDto in snapshot.self.hand)
                {
                    GameObject cardObj = Instantiate(networkCardPrefab, cardsContainer);

                    var visual = cardObj.GetComponent<NetworkCardVisual>();
                    if (visual != null)
                    {
                        var layout = cardObj.GetComponent<CardVisualLayout>();
                        if (layout != null) layout.Apply(CardVisualLocation.Hand);

                        Card asset = catalog.Find(cardDto.definitionId);

                        if (asset != null)
                        {
                            visual.BindFaceUp(cardDto, asset);
                        }
                        else
                        {
                            Debug.LogWarning($"[Mulligan] Không tìm thấy dữ liệu cho lá bài: {cardDto.definitionId}");
                        }
                    }

                    var dragScript = cardObj.GetComponent<DraggableHandCard>();
                    if (dragScript != null)
                    {
                        Destroy(dragScript);
                    }

                    var toggle = cardObj.AddComponent<MulliganCardToggle>();
                    if (ulong.TryParse(cardDto.instanceId, out ulong id))
                    {
                        toggle.cardInstanceId = id;
                    }

                    Transform overlayTran = cardObj.transform.Find("ReplaceOverlay") ?? cardObj.transform.Find("VisualRoot/ReplaceOverlay");
                    if (overlayTran != null) toggle.replaceOverlay = overlayTran.GetComponent<Image>();
                }
            }
        }
        else if (snapshot.roundNumber > 0)
        {
            actionButton.onClick.RemoveListener(OnConfirmClicked);
            actionButton.interactable = true;
            gameObject.SetActive(false);
        }
    }

    private void OnConfirmClicked()
    {
        if (hasConfirmed) return;

        selectedToReplace.Clear();
        foreach (Transform child in cardsContainer)
        {
            var toggle = child.GetComponent<MulliganCardToggle>();
            if (toggle != null && toggle.isSelected)
            {
                selectedToReplace.Add(toggle.cardInstanceId);
            }
        }

        Debug.Log("Số lượng bài muốn đổi: " + selectedToReplace.Count);

        var networkBridge = FindAnyObjectByType<NetworkMatchBridge>();
        if (networkBridge != null)
        {
            networkBridge.RequestMulligan(selectedToReplace.ToArray());
        }
        else
        {
            Debug.LogError("Không tìm thấy NetworkMatchBridge để gửi lệnh!");
        }

        hasConfirmed = true;
        actionButtonText.text = "ĐANG CHỜ...";
        actionButton.interactable = false;
    }
}