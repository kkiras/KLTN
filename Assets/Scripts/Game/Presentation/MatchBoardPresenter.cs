using System.Collections.Generic;
using CMCMProductions;
using KLTN.Game.Content;
using KLTN.Game.Networking;
using UnityEngine;

namespace KLTN.Game.Presentation
{
    public sealed class MatchBoardPresenter : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] private NetworkCardVisual cardPrefab;

        [SerializeField] private Sprite cardBack;

        [Header("Hands")]
        [SerializeField] private RectTransform selfHandRoot;

        [SerializeField] private RectTransform opponentHandRoot;

        [Header("Board slots: element 0 = slot 1")]
        [SerializeField] private RectTransform[] selfBoardSlots = new RectTransform[3];

        [SerializeField] private RectTransform[] opponentBoardSlots = new RectTransform[3];

        [Header("Drag")]
        [SerializeField] private RectTransform dragLayer;
        public MulliganPresenter mulliganPresenter;

        #endregion

        #region Runtime State

        private readonly List<GameObject> spawnedViews = new List<GameObject>();
        private MatchClientProjection projection;
        private CardAssetCatalog catalog;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            catalog = new CardAssetCatalog();
        }

        private void OnEnable()
        {
            projection = MatchProjectionRegistry.Current;
            projection.SnapshotChanged += Render;

            if (projection.Current != null) { Render(projection.Current); }
        }

        private void OnDisable()
        {
            if (projection != null) { projection.SnapshotChanged -= Render; }

            ClearViews();
        }

        #endregion

        #region Snapshot Rendering

        private void Render(MatchSnapshotDto snapshot)
        {
            ClearViews();

            if (snapshot == null ||
                snapshot.self == null ||
                snapshot.opponent == null ||
                cardPrefab == null)
            {
                return;
            }

            if (mulliganPresenter != null)
            {
                mulliganPresenter.UpdateMulliganState(snapshot);
            }

            if (snapshot.roundNumber > 0)
            {
                RenderSelfHand(snapshot);
            }

            RenderOpponentHand(snapshot);
            RenderBoard(snapshot.self.board, selfBoardSlots);
            RenderBoard(snapshot.opponent.board, opponentBoardSlots);
        }

        private void RenderSelfHand(MatchSnapshotDto snapshot)
        {
            if (selfHandRoot == null ||
                snapshot.self.hand == null)
            {
                return;
            }

            foreach (CardViewDto dto in snapshot.self.hand)
            {
                bool canDrag = snapshot.viewerCanAct && dto.energy <= snapshot.self.mana;
                CreateFaceUpCard(dto, selfHandRoot, canDrag, CardVisualLocation.Hand);
            }
        }

        private void RenderOpponentHand(MatchSnapshotDto snapshot)
        {
            if (opponentHandRoot == null) { return; }

            for (int i = 0;
                 i < snapshot.opponent.handCount;
                 i++)
            {
                NetworkCardVisual view = Instantiate(cardPrefab, opponentHandRoot);
                CardVisualLayout layout = view.GetComponent<CardVisualLayout>();

                if (layout != null) { layout.Apply(CardVisualLocation.Hand); }

                view.BindBack(cardBack);
                DraggableHandCard draggable = view.GetComponent<DraggableHandCard>();

                if (draggable != null) { draggable.Configure(false, dragLayer); }

                spawnedViews.Add(view.gameObject);
            }
        }

        private void RenderBoard(CardViewDto[] cards, RectTransform[] slots)
        {
            if (cards == null ||
                slots == null ||
                slots.Length < 3)
            {
                return;
            }

            foreach (CardViewDto dto in cards)
            {
                if (dto.boardSlotIndex < 0 ||
                    dto.boardSlotIndex >= slots.Length ||
                    slots[dto.boardSlotIndex] == null)
                {
                    continue;
                }

                CreateFaceUpCard(dto, slots[dto.boardSlotIndex], false, CardVisualLocation.Board);
            }
        }

        private void CreateFaceUpCard(CardViewDto dto, RectTransform parent, bool canDrag, CardVisualLocation location)
        {
            NetworkCardVisual view = Instantiate(cardPrefab, parent);
            CardVisualLayout layout = view.GetComponent<CardVisualLayout>();

            if (layout != null) { layout.Apply(location); }

            Card asset = catalog.Find(dto.definitionId);
            view.BindFaceUp(dto, asset);
            DraggableHandCard draggable = view.GetComponent<DraggableHandCard>();

            if (draggable != null) { draggable.Configure(canDrag, dragLayer); }

            spawnedViews.Add(view.gameObject);
        }

        #endregion

        #region View Cleanup

        private void ClearViews()
        {
            foreach (GameObject spawnedView in spawnedViews)
            {
                if (spawnedView != null) { Destroy(spawnedView); }
            }

            spawnedViews.Clear();
        }

        #endregion
    }
}
