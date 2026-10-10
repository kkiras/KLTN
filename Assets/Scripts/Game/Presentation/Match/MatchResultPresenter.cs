using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using KLTN.Game.Domain;   
using KLTN.Game.Networking;
using KLTN.Game.Domain.Economy;
using KLTN.Infrastructure.Economy;

public class MatchResultPresenter : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TextMeshProUGUI resultTitleText;

    [Tooltip("Optional. Shows the coins/silver earned, e.g. \"+75 xu · +1 bạc\".")]
    [SerializeField] private TextMeshProUGUI rewardText;

    private string rewardRequestedForMatchId;

    private MatchClientProjection projection;

    private void Start()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
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
        if (snapshot == null) return;

        MatchOutcome outcome = (MatchOutcome)snapshot.outcome;

        if (outcome == MatchOutcome.Running) return;

        SeatId viewerSeat = (SeatId)snapshot.viewerSeat;
        bool viewerWon = (outcome == MatchOutcome.HostWon && viewerSeat == SeatId.Host) || (outcome == MatchOutcome.GuestWon && viewerSeat == SeatId.Guest);

        ShowResult(viewerWon);
        ClaimReward(snapshot, outcome, viewerSeat);
    }

    private async void ClaimReward(MatchSnapshotDto snapshot, MatchOutcome outcome, SeatId viewerSeat)
    {
        if (string.IsNullOrEmpty(snapshot.matchId) || snapshot.matchId == rewardRequestedForMatchId)
        {
            return;
        }

        rewardRequestedForMatchId = snapshot.matchId;

        SeatId? surrenderedSeat = snapshot.surrenderedSeat >= 0 ? (SeatId?)(SeatId)snapshot.surrenderedSeat : null;
        MatchReward reward = MatchRewardPolicy.Compute(outcome, viewerSeat, surrenderedSeat, snapshot.roundNumber);

        SetRewardText(FormatReward(reward) + "  (đang lưu...)");

        PlayerProfileService service = PlayerProfileService.Instance;

        if (service == null)
        {
            SetRewardText(FormatReward(reward));
            Debug.LogWarning("[MatchResult] PlayerProfileService is not available; reward not saved.");
            return;
        }

        EconomyResult result = await service.ClaimMatchRewardAsync(snapshot.matchId, reward);

        if (this == null)
        {
            return;
        }

        SetRewardText(result.Success
            ? FormatReward(reward)
            : FormatReward(reward) + "  (lưu thất bại: " + result.Message + ")");
    }

    private static string FormatReward(MatchReward reward)
    {
        if (reward.Kind == MatchResultKind.EarlySurrender)
        {
            return "Đầu hàng trước vòng 3: không nhận thưởng";
        }

        return reward.Silver > 0
            ? $"+{reward.Coins} xu · +{reward.Silver} bạc"
            : $"+{reward.Coins} xu";
    }

    private void SetRewardText(string text)
    {
        if (rewardText != null)
        {
            rewardText.text = text;
        }
    }

    public void ShowResult(bool isVictory)
    {
        if (resultPanel != null) resultPanel.SetActive(true);

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