using UnityEngine;
namespace HotelTogether
{
    public enum GuestDeliveryType { Luggage, DirtyLaundry, CleanLaundry }
    [RequireComponent(typeof(HotelItem),typeof(Rigidbody))]
    public sealed class HotelGuestDelivery : MonoBehaviour
    {
        public GuestDeliveryType type;
        public Renderer linenRenderer;
        public HotelGuestBot Owner { get; private set; }
        public HotelLuggageTrolley Trolley { get; internal set; }
        public void Assign(HotelGuestBot owner,GuestDeliveryType purpose)
        {
            Owner=owner;type=purpose;
            GetComponent<HotelItem>().displayName=Label;
            foreach(var label in GetComponentsInChildren<TextMesh>())label.text=owner.guestName+"\nROOM "+owner.RoomLabel;
        }
        public string Label=>!Owner?"Guest supplies":Owner.guestName+" / Room "+Owner.RoomLabel+" / "+(type==GuestDeliveryType.Luggage?"Luggage":type==GuestDeliveryType.DirtyLaundry?"Dirty linen":"Clean linen");
        public void Detach(){if(Trolley)Trolley.Detach(this);}
    }
}
