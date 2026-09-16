using UnityEngine;

namespace KLTN.Game.Presentation
{
    public class SimpleRotator : MonoBehaviour
    {
        [Header("Cài đặt")]
        [Tooltip("Tốc độ xoay (độ/giây). Dùng số âm để xoay cùng chiều kim đồng hồ.")]
        public float rotationSpeed = -250f; 

        private void Update()
        {
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }
    }
}