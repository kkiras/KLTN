using System.Collections.Generic;
using UnityEngine;

namespace CMCMProductions
{
    [CreateAssetMenu(fileName = "New Card", menuName = "Card")]
    public class Card : ScriptableObject
    {
        #region Card Data

        public string cardName;
        public Sprite artwork;
        public List<CardType> cardType;
        public int health;
        public int damage;
        public int energy;

        #endregion

        #region Types

        public enum CardType
        {
            MaCo,
            MaDa,
            MaDoi,
            MaLon,
            MaMatMam,
            VongNhi
        }

        #endregion
    }
}
