#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace HotelTogether.Editor
{
 [InitializeOnLoad]
 public static class HotelGraphicsSetup
 {
  const string Folder="Assets/HotelTogether/Resources/HotelTogetherGraphics/";
  static HotelGraphicsSetup()
  {
   EditorApplication.delayCall+=Install;
   EditorSceneManager.sceneOpened+=(s,m)=>EditorApplication.delayCall+=Install;
   EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Install;};
   EditorSceneManager.sceneSaving+=(s,p)=>{foreach(var q in Object.FindObjectsByType<HotelGraphicsQuality>())if(q.gameObject.scene==s)q.SuspendForSceneSave();};
   EditorSceneManager.sceneSaved+=s=>{foreach(var q in Object.FindObjectsByType<HotelGraphicsQuality>())if(q.gameObject.scene==s)q.Apply();};
  }
  [MenuItem("Hotel Together/Graphics/Apply Polished Lobby and Lighting")]
  public static void Install()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)return;
   var scene=SceneManager.GetActiveScene();if(scene.name!="HotelLobby")return;
   var shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"HotelPolishedFloor.shader");if(!shader)return;
   var tiles=AssetDatabase.LoadAssetAtPath<Material>(Folder+"PolishedLobbyTiles.mat");
   if(!tiles){tiles=new Material(shader){name="Polished warm porcelain tiles"};AssetDatabase.CreateAsset(tiles,Folder+"PolishedLobbyTiles.mat");}
   tiles.shader=shader;tiles.SetColor("_BaseColor",new Color(.83f,.76f,.62f,1));tiles.SetFloat("_TileSize",1.25f);tiles.SetFloat("_Smoothness",.82f);tiles.SetFloat("_ReflectionStrength",.48f);tiles.SetFloat("_ReflectionBlur",1.3f);EditorUtility.SetDirty(tiles);AssetDatabase.SaveAssetIfDirty(tiles);
   var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"HotelVisualQuality.asset");
   if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Folder+"HotelVisualQuality.asset");}
   ConfigureProfile(profile,true);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
   ConfigurePipeline();
   var quality=HotelGraphicsBootstrap.Ensure(scene);if(!quality)return;
   quality.polishedTiles=tiles;quality.qualityProfile=profile;quality.Apply();EditorUtility.SetDirty(quality);
   var camera=Camera.main;if(camera){camera.allowHDR=true;camera.allowMSAA=true;var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;data.antialiasingQuality=AntialiasingQuality.High;EditorUtility.SetDirty(data);}
   foreach(var probe in Object.FindObjectsByType<ReflectionProbe>())
   {
    if(probe.gameObject.scene!=scene)continue;
    probe.resolution=256;probe.boxProjection=true;probe.intensity=Mathf.Max(probe.intensity,.8f);probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;EditorUtility.SetDirty(probe);
   }
   EditorSceneManager.MarkSceneDirty(scene);SceneView.RepaintAll();
   Debug.Log("Hotel Together: polished lobby reflections and visual quality installed. Save HotelLobby.");
  }
  public static void ConfigureProfile(VolumeProfile profile,bool save)
  {
   var tone=Ensure<Tonemapping>(profile,save);tone.mode.Override(TonemappingMode.ACES);
   var bloom=Ensure<Bloom>(profile,save);bloom.intensity.Override(.16f);bloom.threshold.Override(1.15f);bloom.scatter.Override(.55f);bloom.highQualityFiltering.Override(true);
   var color=Ensure<ColorAdjustments>(profile,save);color.postExposure.Override(.25f);color.contrast.Override(4);color.saturation.Override(8);
   Ensure<WhiteBalance>(profile,save).temperature.Override(2);
   if(save)foreach(var component in profile.components){EditorUtility.SetDirty(component);AssetDatabase.SaveAssetIfDirty(component);}
  }
  static T Ensure<T>(VolumeProfile profile,bool save)where T:VolumeComponent
  {
   if(profile.TryGet<T>(out var value))return value;
   value=profile.Add<T>();if(save)AssetDatabase.AddObjectToAsset(value,profile);return value;
  }
  static void ConfigurePipeline()
  {
   var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");if(!pipeline)return;
   pipeline.supportsHDR=true;pipeline.msaaSampleCount=4;pipeline.renderScale=1;pipeline.mainLightShadowmapResolution=4096;pipeline.shadowDistance=65;pipeline.shadowCascadeCount=4;
   var settings=new SerializedObject(pipeline);foreach(var key in new[]{"m_SoftShadowsSupported","m_AdditionalLightShadowsSupported","m_ReflectionProbeBlending","m_ReflectionProbeBoxProjection"}){var property=settings.FindProperty(key);if(property!=null)property.boolValue=true;}var soft=settings.FindProperty("m_SoftShadowQuality");if(soft!=null)soft.intValue=3;settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);AssetDatabase.SaveAssetIfDirty(pipeline);
   var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");if(!renderer)return;
   foreach(var feature in renderer.rendererFeatures)
   {
    if(!feature || feature.GetType().Name!="ScreenSpaceAmbientOcclusion")continue;
    var serialized=new SerializedObject(feature);var downsample=serialized.FindProperty("m_Settings.Downsample");if(downsample!=null)downsample.boolValue=false;
    var blur=serialized.FindProperty("m_Settings.BlurQuality");if(blur!=null)blur.intValue=0;
    serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(feature);AssetDatabase.SaveAssetIfDirty(feature);
   }
  }
 }
}
#endif
