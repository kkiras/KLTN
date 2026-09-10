using System;
using System.Collections.Generic;
using CMCMProductions;
using KLTN.Game.Presentation;
using UnityEngine;

namespace KLTN.Game.Content
{
    [Serializable]
    public sealed class CardPresentationEntry
    {
        #region Serialized Fields

        [SerializeField] private Card cardAsset;
        [SerializeField] private CardArtworkView artworkPrefab;

        #endregion

        #region Properties

        public string CardId => cardAsset != null ? cardAsset.name : string.Empty;
        public CardArtworkView ArtworkPrefab => artworkPrefab;

        #endregion
    }

    [CreateAssetMenu(
        fileName = "CardPresentationCatalog",
        menuName = "KLTN/Card Presentation Catalog"
    )]
    public sealed class CardPresentationCatalog : ScriptableObject
    {
        #region Serialized Fields

        [SerializeField] private List<CardPresentationEntry> entries =
            new List<CardPresentationEntry>();

        #endregion

        #region Runtime State

        private readonly Dictionary<string, CardArtworkView> artworkByCardId =
            new Dictionary<string, CardArtworkView>(StringComparer.Ordinal);

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            RebuildIndex();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildIndex();
        }
#endif

        #endregion

        #region Queries

        public CardArtworkView Find(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId)) { return null; }
            if (artworkByCardId.Count == 0 && entries.Count > 0) { RebuildIndex(); }

            artworkByCardId.TryGetValue(cardId, out CardArtworkView artworkPrefab);
            return artworkPrefab;
        }

        #endregion

        #region Indexing

        private void RebuildIndex()
        {
            artworkByCardId.Clear();

            foreach (CardPresentationEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.CardId)) { continue; }

                if (artworkByCardId.ContainsKey(entry.CardId))
                {
                    Debug.LogWarning(
                        $"Duplicate card presentation ID: {entry.CardId}",
                        this
                    );

                    continue;
                }

                artworkByCardId.Add(entry.CardId, entry.ArtworkPrefab);
            }
        }

        #endregion
    }
}