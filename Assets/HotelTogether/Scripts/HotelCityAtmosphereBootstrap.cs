using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotelTogether
{
    public static class HotelCityAtmosphereBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { SceneManager.sceneLoaded-=OnSceneLoaded;SceneManager.sceneLoaded+=OnSceneLoaded; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartEnvironment() { Ensure(SceneManager.GetActiveScene()); }
        static void OnSceneLoaded(Scene scene,LoadSceneMode mode) { Ensure(scene); }
        public static HotelCityAtmosphere Ensure(Scene scene)
        {
            if(!scene.IsValid() || !scene.isLoaded || scene.name!="HotelLobby") return null;
            foreach(var atmosphere in UnityEngine.Object.FindObjectsByType<HotelCityAtmosphere>(FindObjectsInactive.Include))
                if(atmosphere.gameObject.scene==scene) return atmosphere;
            if(!Resources.Load<Texture2D>("HotelTogetherEnvironment/CityPanorama") || !Resources.Load<Shader>("HotelTogetherEnvironment/HotelCityDome")) return null;
            var root=new GameObject("City atmosphere and white sunlight");
            SceneManager.MoveGameObjectToScene(root,scene);
            return root.AddComponent<HotelCityAtmosphere>();
        }
    }
}

