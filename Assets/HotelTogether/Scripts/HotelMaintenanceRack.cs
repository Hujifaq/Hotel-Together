using UnityEngine;
namespace HotelTogether
{
    [RequireComponent(typeof(HotelItem))]
    public sealed class HotelMaintenanceRack : MonoBehaviour
    {
        public HotelMaintenanceSystem system;
        public void Use(HotelPlayer player)
        {
            var tool=player.held?player.held.GetComponent<HotelMaintenanceTool>():null;
            if(tool&&tool.kind==MaintenanceToolKind.Extinguisher){tool.Refill();player.PlayGesture(HotelPlayerGesture.Refill,transform.position);player.Notify("Extinguisher refilled. Aim at the fire base and hold LMB.");}
            else player.Notify("MAINTENANCE: pick up an extinguisher, wrench or insulated screwdriver. J shows active faults and repair steps.");
        }
    }
}
