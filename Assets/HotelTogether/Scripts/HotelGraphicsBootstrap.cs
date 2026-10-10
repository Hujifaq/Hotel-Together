using UnityEngine;
using UnityEngine.SceneManagement;
namespace HotelTogether
{
 public static class HotelGraphicsBootstrap
 {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void Register(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Start(){Ensure(SceneManager.GetActiveScene());}
  static void Loaded(Scene scene,LoadSceneMode mode){Ensure(scene);}
  public static HotelGraphicsQuality Ensure(Scene scene)
  {
   if(!scene.IsValid() || !scene.isLoaded || scene.name!="HotelLobby")return null;
   foreach(var item in Object.FindObjectsByType<HotelGraphicsQuality>(FindObjectsInactive.Include))if(item.gameObject.scene==scene)return item;
   if(!Resources.Load<Material>("HotelTogetherGraphics/PolishedLobbyTiles"))return null;
   var root=new GameObject("Hotel polished floor and visual quality");SceneManager.MoveGameObjectToScene(root,scene);return root.AddComponent<HotelGraphicsQuality>();
  }
 }
}
