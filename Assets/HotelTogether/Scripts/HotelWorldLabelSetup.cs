using UnityEngine;
namespace HotelTogether
{
 [DefaultExecutionOrder(250)] public sealed class HotelWorldLabelSetup:MonoBehaviour
 {
  float next;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install(){var hotel=Object.FindAnyObjectByType<HotelGuestSimulation>();if(hotel&&!hotel.GetComponent<HotelWorldLabelSetup>())hotel.gameObject.AddComponent<HotelWorldLabelSetup>();}
  void Start(){Refresh();}
  void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+2;Refresh();}
  public void Refresh(){foreach(var label in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)){if(label.GetComponent<HotelWorldTextShadow>())continue;var world=label.GetComponent<HotelWorldText>();if(!world){label.gameObject.AddComponent<HotelWorldText>();continue;}
   // Re-run setup when a label's font was swapped at runtime (font upgrade happens in OnEnable).
   var lilita=Resources.Load<Font>("HotelTogetherUI/LilitaOne-Regular");bool latin=true;foreach(char c in label.text)if(c>127){latin=false;break;}if(!label.font||latin&&world.cartoonFont&&lilita&&label.font!=lilita){if(!label.font)label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");world.enabled=false;world.enabled=true;}}}
 }
}
