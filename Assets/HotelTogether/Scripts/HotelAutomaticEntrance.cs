using UnityEngine;

namespace HotelTogether {
 [RequireComponent(typeof(HotelItem))]
 public sealed class HotelAutomaticEntrance : MonoBehaviour {
  public float approachDistance=3.2f, approachHalfWidth=2.5f, closeDelay=1.8f;
  HotelItem item;
  HotelPlayer player;
  float keepOpenUntil, nextPlayerSearch;
  HotelDefenseOpening defense;HotelZombieDirector danger;
  void Awake(){item=GetComponent<HotelItem>();defense=GetComponent<HotelDefenseOpening>();danger=FindFirstObjectByType<HotelZombieDirector>();}
  public void RequestOpen(bool manual=false){if(!defense)defense=GetComponent<HotelDefenseOpening>();if(defense&&!defense.CanOpen)return;if(!manual&&danger&&danger.IsNight)return;item.open=true;keepOpenUntil=Time.time+closeDelay;}
  void Update(){
   if(defense&&defense.Broken){item.open=true;return;}if(defense&&(defense.BoardCount>0||defense.Locked||defense.LockPending)){item.open=false;return;}if(danger&&danger.IsNight){if(Time.time>keepOpenUntil)item.open=false;return;}
   if(!player&&Time.time>=nextPlayerSearch){player=FindFirstObjectByType<HotelPlayer>();nextPlayerSearch=Time.time+1;}
   if(player){
    var p=transform.InverseTransformPoint(player.transform.position);
    if(Mathf.Abs(p.x)<approachHalfWidth&&Mathf.Abs(p.z)<approachDistance&&p.y>-2.1f&&p.y<1.5f)RequestOpen();
   }
   if(Time.time>keepOpenUntil)item.open=false;
  }
 }
}
