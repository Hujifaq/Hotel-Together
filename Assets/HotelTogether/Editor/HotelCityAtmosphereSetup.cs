#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotelTogether.Editor
{
    [InitializeOnLoad]
    public static class HotelCityAtmosphereSetup
    {
        static HotelCityAtmosphereSetup()
        {
            EditorApplication.delayCall+=Install;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Install;};
            EditorSceneManager.sceneOpened+=OnSceneOpened;
            EditorSceneManager.sceneSaving+=BeforeSceneSave;
            EditorSceneManager.sceneSaved+=AfterSceneSave;
        }
        static void OnSceneOpened(Scene scene,OpenSceneMode mode) { EditorApplication.delayCall+=Install; }
        [MenuItem("Hotel Together/Environment/Add City Sky and White Sunlight")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            var scene=SceneManager.GetActiveScene();
            if(scene.name!="HotelLobby") return;
            var glass=EnsureGlassMaterial();
            bool existed=false;
            foreach(var item in UnityEngine.Object.FindObjectsByType<HotelCityAtmosphere>(FindObjectsInactive.Include))
                if(item.gameObject.scene==scene) {existed=true;break;}
            var atmosphere=HotelCityAtmosphereBootstrap.Ensure(scene);
            if(!atmosphere) return;
            atmosphere.windowGlass=glass;atmosphere.Apply();
            if(existed) {EditorUtility.SetDirty(atmosphere);EditorSceneManager.MarkSceneDirty(scene);SceneView.RepaintAll();return;}
            Undo.RegisterCreatedObjectUndo(atmosphere.gameObject,"Add city atmosphere");
            EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
            Debug.Log("Hotel Together: city skydome and white sunlight are ready. Save HotelLobby to keep the environment object in the scene.");
        }
        static Material EnsureGlassMaterial()
        {
            const string path="Assets/HotelTogether/Resources/HotelTogetherEnvironment/CityWindowGlass.mat";
            var glass=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!glass)
            {
                glass=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Hotel clear daylight glass"};
                AssetDatabase.CreateAsset(glass,path);
            }
            glass.SetColor("_BaseColor",new Color(.73f,.87f,.90f,.12f));
            glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite",0);glass.SetFloat("_Cull",0);glass.SetFloat("_Smoothness",.80f);glass.SetFloat("_Metallic",0);glass.SetFloat("_BlendModePreserveSpecular",0);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");glass.DisableKeyword("_ALPHAMODULATE_ON");glass.renderQueue=3000;
            EditorUtility.SetDirty(glass);AssetDatabase.SaveAssetIfDirty(glass);
            return glass;
        }
        static void BeforeSceneSave(Scene scene,string path)
        {
            // Serialize original window materials and lighting; generated resources are rebuilt on load.
            foreach(var atmosphere in UnityEngine.Object.FindObjectsByType<HotelCityAtmosphere>())
                if(atmosphere.gameObject.scene==scene) atmosphere.SuspendForSceneSave();
        }
        static void AfterSceneSave(Scene scene)
        {
            foreach(var atmosphere in UnityEngine.Object.FindObjectsByType<HotelCityAtmosphere>())
                if(atmosphere.gameObject.scene==scene) atmosphere.Apply();
            SceneView.RepaintAll();
        }
    }
    public sealed class HotelCityPanoramaImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(assetPath!="Assets/HotelTogether/Resources/HotelTogetherEnvironment/CityPanorama.png") return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.wrapModeU=TextureWrapMode.Repeat;importer.wrapModeV=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Trilinear;importer.mipmapEnabled=true;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.isReadable=false;
            importer.alphaSource=TextureImporterAlphaSource.None;
        }
    }
}
#endif

