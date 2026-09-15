using System.Collections.Generic;
using KLTN.Game.Content;
using KLTN.Game.Domain;
using UnityEngine;

namespace CMCMProductions
{
    [CreateAssetMenu(fileName = "New Card", menuName = "Card")]
    public class Card : ScriptableObject
    {
        #region Card Data

        public string cardName;

        [TextArea(2, 5)]
        public string rulesText;

        public List<CardType> cardType;

        public int health;
        public int damage;
        public int energy;

        #endregion

        #region Runtime Rules

        public UnitKeyword keywords = UnitKeyword.None;

        public List<CardAbilityData> abilities = new List<CardAbilityData>();

        public CardPassiveData passiveRules = new CardPassiveData();

        [Min(0)]
        [Tooltip("0 means no card-specific deck limit.")]
        public int maximumCopiesPerDeck;

        #endregion

        #region Types

        public enum CardType
        {
            MaCo,
            MaDa,
            MaDoi,
            MaLon,
            MaMatMam,
            VongNhi,
        }

        #endregion
    }
}
