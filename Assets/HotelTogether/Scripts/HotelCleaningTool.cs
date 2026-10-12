using UnityEngine;
namespace HotelTogether
{
    [RequireComponent(typeof(HotelItem))]
    public sealed class HotelCleaningTool : MonoBehaviour
    {
        public int capacity = 3;
        public int UsesLeft { get; private set; } = 3;
        public HotelCleaningCart DockedAt { get; internal set; }
        public void Rinse() { UsesLeft = capacity; }
        public void Consume() { UsesLeft = Mathf.Max(0, UsesLeft - 1); }
    }
}
