using System.Collections.Generic;
using CMCMProductions;
using UnityEngine;

namespace KLTN.Game.Content
{
    public sealed class CardAssetCatalog
    {
        #region Fields

        private readonly Dictionary<string, Card> cardsById = new Dictionary<string, Card>();

        #endregion

        #region Construction

        public CardAssetCatalog()
        {
            Card[] assets = Resources.LoadAll<Card>("Cards");

            foreach (Card asset in assets)
            {
                if (asset == null ||
                    cardsById.ContainsKey(asset.name))
                {
                    continue;
                }

                cardsById.Add(asset.name, asset);
            }
        }

        #endregion

        #region Queries

        public Card Find(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId)) { return null; }

            cardsById.TryGetValue(definitionId, out Card asset);
            return asset;
        }

        #endregion
    }
}
