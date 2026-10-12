using UnityEngine;
namespace HotelTogether
{
    public enum GuestNeed { None, CheckIn, Meal, Water, Towel, CheckOut, Help, Shelter, Rescue, Luggage }
    public sealed class HotelGuestSupply : MonoBehaviour
    {
        public GuestNeed type;
    }
}
