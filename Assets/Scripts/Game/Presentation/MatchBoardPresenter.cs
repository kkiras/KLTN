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

        #endregion

        #region Runtime State

        private readonly List<GameObject> spawnedViews = new List<GameObject>();
        private MatchClientProjection projection;
        private CardAssetCatalog catalog;

        private readonly List<GameObject> opponentHandBacks = new List<GameObject>();

        private readonly Dictionary<string, NetworkCardVisual> faceUpViewsById = new Dictionary<string, NetworkCardVisual>();

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

            RenderSelfHand(snapshot);
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
                opponentHandBacks.Add(view.gameObject);
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

        private NetworkCardVisual CreateFaceUpCard(CardViewDto dto, RectTransform parent, bool canDrag, CardVisualLocation location)
        {
            NetworkCardVisual view = Instantiate(cardPrefab, parent);
            CardVisualLayout layout = view.GetComponent<CardVisualLayout>();

            if (layout != null) { layout.Apply(location); }

            Card asset = catalog.Find(dto.definitionId);
            view.BindFaceUp(dto, asset);
            DraggableHandCard draggable = view.GetComponent<DraggableHandCard>();

            if (draggable != null) { draggable.Configure(canDrag, dragLayer); }

            spawnedViews.Add(view.gameObject);

            if (!string.IsNullOrWhiteSpace(view.InstanceId))
            {
                faceUpViewsById[view.InstanceId] = view;
            }

            if (location == CardVisualLocation.Board)
            {
                view.SetInteractableVisual(true);
            }

            return view;
        }

        #endregion

        #region Combat Presentation

        public void PrepareCombat(RoundResolutionDto resolution, int viewerSeat)
        {
            if (resolution?.steps == null) { return; }

            for (int i = 0; i < resolution.steps.Length; i++)
            {
                CombatStepDto step = resolution.steps[i];

                PrepareResolvedCard(step.hostCard, step.slotIndex, viewerSeat);

                PrepareResolvedCard(step.guestCard, step.slotIndex, viewerSeat);
            }
        }

        public bool TryGetFaceUpVisual(string instanceId, out NetworkCardVisual view)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                view = null;
                return false;
            }

            return faceUpViewsById.TryGetValue(instanceId, out view) &&
                view != null;
        }

        private void PrepareResolvedCard(
            ResolvedCardDto resolvedCard,
            int slotIndex,
            int viewerSeat)
        {
            CardViewDto card = resolvedCard?.cardBefore;

            if (card == null || string.IsNullOrWhiteSpace(card.instanceId)) { return; }

            bool isSelfCard = resolvedCard.seat == viewerSeat;
            RectTransform[] targetSlots = isSelfCard
                ? selfBoardSlots
                : opponentBoardSlots;

            if (targetSlots == null ||
                slotIndex < 0 ||
                slotIndex >= targetSlots.Length ||
                targetSlots[slotIndex] == null)
            {
                Debug.LogWarning(
                    $"Không thể chuẩn bị combat visual. " +
                    $"Seat={resolvedCard.seat}, Slot={slotIndex}."
                );

                return;
            }

            bool viewAlreadyExists = TryGetFaceUpVisual(card.instanceId, out NetworkCardVisual view);

            if (!viewAlreadyExists)
            {
                if (!isSelfCard) { RemoveOneOpponentHandBack(); }

                view = CreateFaceUpCard(card, targetSlots[slotIndex], false, CardVisualLocation.Board);
            }
            else
            {
                PlaceExistingCardOnBoard(view, card, targetSlots[slotIndex]);
            }

            view.SetPending(false);
            view.SetInteractableVisual(true);
        }

        private void PlaceExistingCardOnBoard(NetworkCardVisual view, CardViewDto card, RectTransform targetSlot)
        {
            view.transform.SetParent(targetSlot, false);

            CardVisualLayout layout = view.GetComponent<CardVisualLayout>();

            if (layout != null)
            {
                layout.Apply(CardVisualLocation.Board);
            }

            RectTransform cardRect = view.transform as RectTransform;

            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition = Vector2.zero;
                cardRect.localScale = Vector3.one;
                cardRect.localRotation = Quaternion.identity;
            }

            Card asset = catalog.Find(card.definitionId);
            view.BindFaceUp(card, asset);

            DraggableHandCard draggable = view.GetComponent<DraggableHandCard>();

            if (draggable != null)
            {
                draggable.Configure(false, dragLayer);
            }

            view.SetInteractableVisual(true);
        }

        private void RemoveOneOpponentHandBack()
        {
            for (int i = opponentHandBacks.Count - 1; i >= 0; i--)
            {
                GameObject cardBackObject = opponentHandBacks[i];
                opponentHandBacks.RemoveAt(i);

                if (cardBackObject == null) { continue; }

                spawnedViews.Remove(cardBackObject);
                cardBackObject.SetActive(false);
                Destroy(cardBackObject);
                return;
            }
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
            opponentHandBacks.Clear();
            faceUpViewsById.Clear();
        }

        #endregion
    }
}
