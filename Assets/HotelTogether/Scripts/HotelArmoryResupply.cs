using UnityEngine;
namespace HotelTogether {
 public sealed class HotelArmoryResupply:MonoBehaviour {
  public void Use(HotelPlayer p){var gun=p.held?p.held.GetComponent<HotelFirearm>():null;if(!gun){p.Notify("Equip a firearm, then click this crate to refill reserve ammunition.");return;}gun.Resupply();p.PlayGesture(HotelPlayerGesture.Refill,transform.position);p.Notify("Reserve ammunition refilled. R to reload.");}
 }
}
