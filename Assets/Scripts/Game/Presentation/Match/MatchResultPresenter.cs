using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using KLTN.Game.Domain;   
using KLTN.Game.Networking;

public class MatchResultPresenter : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TextMeshProUGUI resultTitleText;

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