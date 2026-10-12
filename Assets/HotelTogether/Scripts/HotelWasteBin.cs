using UnityEngine;
namespace HotelTogether
{
    [RequireComponent(typeof(HotelItem))]
    public sealed class HotelWasteBin : MonoBehaviour
    {
        public bool Use(HotelPlayer player)
        {
            if (Vector3.Distance(player.transform.position, transform.position) > 3.2f) return false;
            var cart = player.cart ? player.cart.GetComponent<HotelCleaningCart>() : null;
            if (cart)
            {
                int amount = cart.EmptyWaste(); player.Notify(amount > 0 ? "Cart waste emptied. Ready for more housekeeping." : "The cart waste bag is already empty."); return amount > 0;
            }
            var litter = player.held ? player.held.GetComponent<HotelFloorMess>() : null;
            if (!litter || litter.type != FloorMessType.Litter) { player.Notify("Pick up floor litter first, then E at this bin. A pushed housekeeping cart can also empty its waste here."); return false; }
            player.Drop(); return litter.Dispose();
        }
    }
}
