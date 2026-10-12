using UnityEngine;
namespace HotelTogether
{
    [RequireComponent(typeof(HotelItem))]
    public sealed class HotelDetergentBottle : MonoBehaviour
    {
        public int capacity = 5;
        public int Doses { get; private set; } = 5;
        void Awake() { ResetBottle(); }
        public bool UseDose() { if (Doses <= 0) return false; Doses--; UpdateLabel(); return true; }
        public void ResetBottle() { Doses = capacity; UpdateLabel(); }
        void UpdateLabel() { GetComponent<HotelItem>().displayName = "Laundry detergent / " + Doses + " doses"; }
    }
}
