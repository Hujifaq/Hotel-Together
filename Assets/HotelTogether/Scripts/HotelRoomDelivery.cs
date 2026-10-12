using UnityEngine;
namespace HotelTogether
{
    public sealed class HotelRoomDelivery : MonoBehaviour
    {
        public HotelServiceJobs jobs;
        public int roomIndex;
        public Transform deposit;
        public bool Deliver(HotelPlayer player)=>jobs.DeliverRoom(player,this);
    }
}
