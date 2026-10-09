using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HotelTogether;
public static class HotelGlassTuning {
 const string MaterialPath="Assets/HotelTogether/Settings/SmokedWindowGlass.mat";
 [MenuItem("HotelTogether/Apply softly smoked window glass")]
 public static void Apply(){
 if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before saving glass changes.");
 var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
 if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,MaterialPath);}
 mat.name="Soft smoked reflective glass";mat.SetFloat("_WorkflowMode",0);mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",1);mat.SetFloat("_SrcBlend",(float)BlendMode.One);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);mat.SetFloat("_Cull",(float)CullMode.Off);mat.SetFloat("_AlphaClip",0);mat.SetFloat("_Smoothness",.68f);mat.SetFloat("_Metallic",0);mat.SetColor("_BaseColor",new Color(.57f,.66f,.68f,.38f));mat.SetColor("_SpecColor",new Color(.08f,.08f,.08f));mat.SetFloat("_EnvironmentReflections",1);mat.SetFloat("_SpecularHighlights",1);mat.SetOverrideTag("RenderType","Transparent");mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");mat.EnableKeyword("_SPECULAR_SETUP");mat.DisableKeyword("_ALPHATEST_ON");mat.DisableKeyword("_ALPHABLEND_ON");mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");mat.renderQueue=3000;mat.enableInstancing=true;EditorUtility.SetDirty(mat);
 int count=0;foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>()){var materials=r.sharedMaterials;bool changed=false;for(int i=0;i<materials.Length;i++){var m=materials[i];if(m==mat||IsWindow(m)){materials[i]=mat;changed=true;}}if(changed){Undo.RecordObject(r,"Window glass finish");r.sharedMaterials=materials;r.receiveShadows=true;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;r.shadowCastingMode=ShadowCastingMode.Off;EditorUtility.SetDirty(r);count++;}}
 // Capture neighbouring rooms once at startup; avoid a full-scene mirror every frame.
 AddProbe("Dining window reflection",new Vector3(18,2.5f,3),new Vector3(22,8,12));AddProbe("West wing window reflection",new Vector3(-19,2.5f,3),new Vector3(22,8,12));
 foreach(var p in UnityEngine.Object.FindObjectsByType<HotelItem>().Where(p=>p.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Contains(mat)))){var path="Assets/HotelTogether/Prefabs/"+p.name+".prefab";if(AssetDatabase.LoadAssetAtPath<GameObject>(path)){var root=PrefabUtility.LoadPrefabContents(path);foreach(var r in root.GetComponentsInChildren<Renderer>()){r.sharedMaterials=r.sharedMaterials.Select(m=>IsWindow(m)?mat:m).ToArray();}PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);}}
 HotelGlazingRepair.ApplyToScene();
 EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();Debug.Log("HOTEL_GLASS_UPDATED panes="+count);
 }
 static bool IsWindow(Material m)=>m&&m.HasProperty("baseColorFactor")&&Mathf.Abs(m.GetColor("baseColorFactor").a-.28f)<.001f&&m.shader.name.Contains("pbrMetallicRoughness");
 static void AddProbe(string name,Vector3 position,Vector3 size){var go=GameObject.Find(name);var p=go?go.GetComponent<ReflectionProbe>():new GameObject(name).AddComponent<ReflectionProbe>();p.transform.position=position;p.size=size;p.resolution=128;p.mode=ReflectionProbeMode.Realtime;p.refreshMode=ReflectionProbeRefreshMode.OnAwake;p.boxProjection=true;p.intensity=.7f;p.clearFlags=ReflectionProbeClearFlags.SolidColor;p.backgroundColor=new Color(.67f,.79f,.83f);p.nearClipPlane=.1f;p.farClipPlane=65;}
}
