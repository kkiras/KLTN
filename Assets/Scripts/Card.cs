using UnityEngine;

namespace CMCMProductions
{
    [CreateAssetMenu(fileName = "New Card", menuName = "Card")]
    public class Card : ScriptableObject
    {
        public string cardName;
        public int health;
        public int damage;
        public int energy;
         
    }
}
