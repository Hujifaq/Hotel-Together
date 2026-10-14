using UnityEngine;
namespace HotelTogether
{
 [DefaultExecutionOrder(250)] public sealed class HotelWorldLabelSetup:MonoBehaviour
 {
  float next;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install(){var hotel=Object.FindAnyObjectByType<HotelGuestSimulation>();if(hotel&&!hotel.GetComponent<HotelWorldLabelSetup>())hotel.gameObject.AddComponent<HotelWorldLabelSetup>();}
  void Start(){Refresh();}
  void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+2;Refresh();}
  public void Refresh(){foreach(var label in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)){var world=label.GetComponent<HotelWorldText>();bool latin=true;foreach(char c in label.text)if(c>127){latin=false;break;}if(!label.font||latin&&label.font.name!="LegacyRuntime"){label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");if(world){world.enabled=false;world.enabled=true;}}if(!world)label.gameObject.AddComponent<HotelWorldText>();}}
 }
}
