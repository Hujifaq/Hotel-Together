using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace HotelTogether
{
 [ExecuteAlways,DisallowMultipleComponent]
 public sealed class HotelGraphicsQuality : MonoBehaviour
 {
  public Material polishedTiles;
  public VolumeProfile qualityProfile;
  [Range(.25f,1)] public float reflectionScale=.65f;
  [Range(256,2048)] public int maximumReflectionSize=1024;
  public float reflectionPlaneHeight=.002f;
  [Tooltip("Floor reflections only need nearby furniture and people; distant city geometry is skipped.")] public float reflectionFarClip=45;
  [Min(1)] public int reflectionInterval=1;
  public bool reflectionsEnabled=true;
  Camera reflectionCamera;
  int lastReflectionFrame=-100;
  RenderTexture reflection;
  GameObject volumeObject;
  bool rendering;
  readonly Dictionary<Renderer,Material[]> originals=new Dictionary<Renderer,Material[]>();
  readonly List<Renderer> floorRenderers=new List<Renderer>();
  // The local player's own body sits right under the camera; mirrored and blurred it smears across the floor.
  readonly List<Renderer> hiddenFromReflection=new List<Renderer>();
  HotelPlayer localPlayer;
  static readonly int ReflectionId=Shader.PropertyToID("_HotelFloorReflection"), MatrixId=Shader.PropertyToID("_HotelReflectionVP"),ReadyId=Shader.PropertyToID("_HotelReflectionReady");
  void OnEnable(){Apply();RenderPipelineManager.beginCameraRendering+=Reflect;}
  void Update(){if(!polishedTiles || !volumeObject)Apply();}
  public void Apply()
  {
   if(!polishedTiles)polishedTiles=Resources.Load<Material>("HotelTogetherGraphics/PolishedLobbyTiles");
   if(!qualityProfile)qualityProfile=Resources.Load<VolumeProfile>("HotelTogetherGraphics/HotelVisualQuality");
   if(polishedTiles)
   foreach(var renderer in FindObjectsByType<Renderer>())
   {
    if(renderer.gameObject.scene!=gameObject.scene || originals.ContainsKey(renderer))continue;
    bool target=renderer.name=="Node-84"||renderer.name=="Node-85"||renderer.name=="Node-86"||renderer.name=="Node-83";
    if(!target || !renderer.transform.parent || renderer.transform.parent.name!="Hotel architecture and furnishings")continue;
    originals[renderer]=renderer.sharedMaterials;renderer.sharedMaterial=polishedTiles;floorRenderers.Add(renderer);
   }
   if(qualityProfile && !volumeObject)
   {
    volumeObject=new GameObject("Hotel quality colour and light"){hideFlags=HideFlags.DontSave};volumeObject.transform.SetParent(transform,false);
    var volume=volumeObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=20;volume.sharedProfile=qualityProfile;
   }
  }
  public void ApplyTier(HotelPerformanceDirector.TierSettings tier)
  {
   reflectionsEnabled=tier.floorReflections;reflectionScale=tier.reflectionScale;maximumReflectionSize=tier.reflectionMaxSize;reflectionInterval=Mathf.Max(1,tier.reflectionInterval);
   if(!reflectionsEnabled){Shader.SetGlobalFloat(ReadyId,0);Release(reflection);reflection=null;}
  }
  void Reflect(ScriptableRenderContext context,Camera source)
  {
   if(!reflectionsEnabled || source.cameraType!=CameraType.Game && source.cameraType!=CameraType.SceneView)return;
   // Reuse the previous reflection on cheaper tiers; the floor reflection is blurred anyway.
   if(reflection && Application.isPlaying && Time.frameCount-lastReflectionFrame<reflectionInterval)return;
   if(rendering || !polishedTiles || source==reflectionCamera || source.cameraType==CameraType.Reflection || source.cameraType==CameraType.Preview || source.transform.position.y<.1f || source.transform.position.y>4.5f)return;
   if(!reflectionCamera)
   {
    var go=new GameObject("Hotel floor reflection camera"){hideFlags=HideFlags.HideAndDontSave};reflectionCamera=go.AddComponent<Camera>();reflectionCamera.enabled=false;
   }
   int width=Mathf.Clamp(Mathf.RoundToInt(source.pixelWidth*reflectionScale),256,maximumReflectionSize);
   int height=Mathf.Max(128,Mathf.RoundToInt(width/(source.aspect>0?source.aspect:1.6f)));
   if(!reflection || reflection.width!=width || reflection.height!=height)
   {
    Release(reflection);reflection=new RenderTexture(width,height,24,RenderTextureFormat.DefaultHDR){name="Lobby floor reflection",hideFlags=HideFlags.HideAndDontSave,useMipMap=true,autoGenerateMips=false,filterMode=FilterMode.Trilinear};reflection.Create();
   }
   reflectionCamera.CopyFrom(source);reflectionCamera.ResetProjectionMatrix();reflectionCamera.farClipPlane=Mathf.Min(source.farClipPlane,reflectionFarClip);reflectionCamera.enabled=false;reflectionCamera.cameraType=CameraType.Reflection;reflectionCamera.targetTexture=reflection;reflectionCamera.allowMSAA=false;reflectionCamera.allowHDR=true;reflectionCamera.useOcclusionCulling=false;
   // Shadows are re-rendered per camera; the blurred, low-resolution reflection does not need them.
   var data=reflectionCamera.GetUniversalAdditionalCameraData();data.renderType=CameraRenderType.Base;data.renderPostProcessing=false;data.renderShadows=false;data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;data.antialiasing=AntialiasingMode.None;
   var matrix=Matrix4x4.identity;matrix.m11=-1;matrix.m13=2*reflectionPlaneHeight;
   var reflectedPosition=matrix.MultiplyPoint(source.transform.position);
   reflectionCamera.transform.position=reflectedPosition;
   var euler=source.transform.eulerAngles;reflectionCamera.transform.rotation=Quaternion.Euler(-euler.x,euler.y,-euler.z);
   reflectionCamera.worldToCameraMatrix=source.worldToCameraMatrix*matrix;
   var planePoint=new Vector3(0,reflectionPlaneHeight+.015f,0);var cameraPoint=reflectionCamera.worldToCameraMatrix.MultiplyPoint(planePoint);var cameraNormal=reflectionCamera.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
   var clipPlane=new Vector4(cameraNormal.x,cameraNormal.y,cameraNormal.z,-Vector3.Dot(cameraPoint,cameraNormal));
   reflectionCamera.projectionMatrix=reflectionCamera.CalculateObliqueMatrix(clipPlane);
   reflectionCamera.cullingMask=source.cullingMask & ~((1<<8)|(1<<9));
   bool oldCulling=GL.invertCulling;rendering=true;
   if(!localPlayer)localPlayer=FindAnyObjectByType<HotelPlayer>();
   hiddenFromReflection.Clear();
   if(localPlayer)localPlayer.GetComponentsInChildren(false,hiddenFromReflection);
   for(int i=hiddenFromReflection.Count-1;i>=0;i--){if(hiddenFromReflection[i].forceRenderingOff)hiddenFromReflection.RemoveAt(i);else hiddenFromReflection[i].forceRenderingOff=true;}
   try
   {
    GL.invertCulling=!oldCulling;
    #pragma warning disable 0618
    UniversalRenderPipeline.RenderSingleCamera(context,reflectionCamera);
    #pragma warning restore 0618
    reflection.GenerateMips();lastReflectionFrame=Time.frameCount;
    Shader.SetGlobalTexture(ReflectionId,reflection);Shader.SetGlobalMatrix(MatrixId,GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix,true)*reflectionCamera.worldToCameraMatrix);Shader.SetGlobalFloat(ReadyId,1);
   }
   finally{GL.invertCulling=oldCulling;rendering=false;foreach(var r in hiddenFromReflection)if(r)r.forceRenderingOff=false;hiddenFromReflection.Clear();}
  }
  public void SuspendForSceneSave()
  {
   foreach(var item in originals)if(item.Key)item.Key.sharedMaterials=item.Value;
   originals.Clear();floorRenderers.Clear();
  }
  void OnDisable()
  {
   RenderPipelineManager.beginCameraRendering-=Reflect;Shader.SetGlobalFloat(ReadyId,0);
   SuspendForSceneSave();Release(volumeObject);volumeObject=null;Release(reflectionCamera?reflectionCamera.gameObject:null);reflectionCamera=null;Release(reflection);reflection=null;
  }
  static void Release(UnityEngine.Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
 }
}
