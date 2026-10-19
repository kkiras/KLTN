using UnityEngine;

namespace KLTN.Game.Presentation
{
    public class CardLoreProfile : MonoBehaviour
    {
        [Header("Thông tin Modal Chi Tiết")]
        public string role = "Đấu sĩ";
        
        [TextArea(3, 6)]
        public string skillDescription = "Mô tả kỹ năng...";
        
        [TextArea(4, 8)]
        public string folklore = "Giai thoại dân gian...";
    }
}