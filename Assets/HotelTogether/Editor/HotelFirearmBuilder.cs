using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace HotelTogether.Editor {
 public static class HotelFirearmBuilder {
  [Serializable] class Catalog {public Entry[] weapons;}[Serializable] class Entry {public string name,asset,kind;public float[] size;public int triangles,clipped;}
  const string Art="Assets/HotelTogether/Art/Firearms/",Output="Assets/HotelTogether/Resources/HotelTogetherFirearms/";
  [MenuItem("Hotel Together/Combat/Build supplied firearms")]
  public static void Build(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before authoring weapons.");Directory.CreateDirectory(Output);AssetDatabase.Refresh();var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Art+"Catalog.json"));Material shared=null;
   foreach(var entry in catalog.weapons){if(entry.clipped!=0)throw new InvalidDataException("Incomplete weapon mesh: "+entry.name);var source=AssetDatabase.LoadAssetAtPath<GameObject>(Art+entry.asset+".glb");if(!source)throw new FileNotFoundException("Weapon model not imported",entry.asset);var go=new GameObject(entry.asset);var visual=UnityEngine.Object.Instantiate(source,go.transform);visual.name="Weapon model";visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one;
    foreach(var r in visual.GetComponentsInChildren<Renderer>()){if(!shared){var materialPath=Art+"Shared weapon atlas.mat";shared=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(!shared){shared=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(shared,materialPath);}shared.shader=Shader.Find("Universal Render Pipeline/Lit");shared.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Weapon atlas.png");shared.color=Color.white;shared.SetFloat("_Metallic",.4f);shared.SetFloat("_Smoothness",.4f);EditorUtility.SetDirty(shared);}r.sharedMaterial=shared;}
    foreach(var filter in visual.GetComponentsInChildren<MeshFilter>()){var path=Art+entry.asset+" mesh.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!mesh){mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name=entry.name;AssetDatabase.CreateAsset(mesh,path);}filter.sharedMesh=mesh;}
    var size=new Vector3(entry.size[0],entry.size[1],entry.size[2]);var item=go.AddComponent<HotelItem>();item.kind="pickup";item.displayName=entry.name;item.sourceModel="Aligned Games / "+entry.asset;item.dimensions=size;var collider=go.AddComponent<BoxCollider>();collider.size=new Vector3(Mathf.Max(.07f,size.x),size.y,size.z);var body=go.AddComponent<Rigidbody>();body.mass=entry.kind=="Pistol"?.9f:entry.kind=="Launcher"?4.5f:2.7f;body.isKinematic=true;body.useGravity=true;var gun=go.AddComponent<HotelFirearm>();gun.model=visual.transform;gun.suppressed=entry.name.StartsWith("Suppressed",StringComparison.Ordinal);gun.Configure((HotelFirearmKind)Enum.Parse(typeof(HotelFirearmKind),entry.kind),size);PrefabUtility.SaveAsPrefabAsset(go,Output+entry.asset+".prefab");UnityEngine.Object.DestroyImmediate(go);
   }AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("Created "+catalog.weapons.Length+" complete supplied firearm prefabs. No scene changes.");}
 }
}
