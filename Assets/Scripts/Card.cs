using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CMCMProductions
{
    [CreateAssetMenu(fileName = "New Card", menuName = "Card")]
    public class Card : ScriptableObject
    {
        public string cardName;
        public List<CardType> cardType;
        public int health;
        public int damage;
        public int energy;

        public enum CardType
        {
            MaCo,
            MaDa,
            MaDoi,
            MaLon,
            MaMatMam,
            VongNhi
        }
    }
}
