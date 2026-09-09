using System.Collections;
using KLTN.Game.Networking;
using UnityEngine;

namespace KLTN.Game.Presentation
{
    public sealed class TurnBannerCoordinator : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] private MatchTransitionBanner transitionBanner;

        #endregion

        #region Runtime State

        private MatchClientProjection projection;
        private Coroutine playbackCoroutine;
        private bool hasSnapshot;
        private bool wasOwnTurn;
        private bool pendingYourTurn;
        private ulong lastQueuedRevision = ulong.MaxValue;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            projection = MatchProjectionRegistry.Current;
            projection.SnapshotChanged += HandleSnapshotChanged;

            if (projection.Current != null) { HandleSnapshotChanged(projection.Current); }
        }

        private void OnDisable()
        {
            if (projection != null) { projection.SnapshotChanged -= HandleSnapshotChanged; }

            if (playbackCoroutine != null)
            {
                StopCoroutine(playbackCoroutine);
                playbackCoroutine = null;
            }

            pendingYourTurn = false;
        }

        #endregion

        #region Snapshot Handling

        private void HandleSnapshotChanged(MatchSnapshotDto snapshot)
        {
            if (snapshot == null) { return; }

            bool isOwnTurn =
                snapshot.viewerCanAct &&
                snapshot.activeSeat == snapshot.viewerSeat;

            bool becameOwnTurn =
                isOwnTurn &&
                (!hasSnapshot || !wasOwnTurn);

            hasSnapshot = true;
            wasOwnTurn = isOwnTurn;

            if (!becameOwnTurn) { return; }
            if (snapshot.revision == lastQueuedRevision) { return; }

            lastQueuedRevision = snapshot.revision;
            pendingYourTurn = true;

            if (playbackCoroutine == null)
            {
                playbackCoroutine = StartCoroutine(PlayPendingBanners());
            }
        }

        private IEnumerator PlayPendingBanners()
        {
            while (pendingYourTurn)
            {
                pendingYourTurn = false;

                if (transitionBanner != null)
                {
                    yield return transitionBanner.PlayYourTurn();
                }
            }

            playbackCoroutine = null;
        }

        #endregion
    }
}