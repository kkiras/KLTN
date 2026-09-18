using KLTN.Game.Domain;
using KLTN.Game.Networking;
using TMPro;
using UnityEngine;

public class MatchResultUI : MonoBehaviour
{
    [SerializeField] private GameObject matchResultPanel;
    [SerializeField] private TMP_Text resultText;

    private MatchClientProjection projection;

    private void Start()
    {
        if (matchResultPanel != null)
        {
            matchResultPanel.SetActive(false);
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
        if (snapshot == null)
        {
            return;
        }

        MatchOutcome outcome = (MatchOutcome)snapshot.outcome;

        if (outcome == MatchOutcome.Running)
        {
            if (matchResultPanel != null)
            {
                matchResultPanel.SetActive(false);
            }

            return;
        }

        string result;

        if (outcome == MatchOutcome.Draw)
        {
            result = "DRAW";
        }
        else
        {
            SeatId viewerSeat = (SeatId)snapshot.viewerSeat;

            bool viewerWon =
                (outcome == MatchOutcome.HostWon && viewerSeat == SeatId.Host)
                || (outcome == MatchOutcome.GuestWon && viewerSeat == SeatId.Guest);

            result = viewerWon ? "VICTORY" : "DEFEAT";
        }

        if (resultText != null)
        {
            resultText.text = result;
        }

        if (matchResultPanel != null)
        {
            matchResultPanel.SetActive(true);
        }
    }
}