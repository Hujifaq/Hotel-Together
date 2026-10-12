using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using HotelTogether;

public static class HotelFacilitiesInstallation
{
    const string Base = "Assets/HotelTogether/";
    const string Prefabs = Base+"Prefabs/Facilities/";
    static Transform root;
    static Bounds BoundsOf(GameObject g){var rs=g.GetComponentsInChildren<Renderer>();if(rs.Length==0)throw new InvalidOperationException("No mesh: "+g.name);var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
    static GameObject Prepare(string file,string node,string name,float height,bool ceiling=false)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Base+"Models/Facilities/"+file+".glb");if(!asset)throw new InvalidOperationException("Model missing: "+file);
        var full=UnityEngine.Object.Instantiate(asset);var visual=full;
        if(node!=null){var t=full.GetComponentsInChildren<Transform>().First(x=>x.name==node);visual=UnityEngine.Object.Instantiate(t.gameObject);visual.transform.SetPositionAndRotation(t.position,t.rotation);visual.transform.localScale=t.lossyScale;UnityEngine.Object.DestroyImmediate(full);}
        var g=new GameObject(name);visual.name="Supplied model";visual.transform.SetParent(g.transform,true);var b=BoundsOf(g);if(b.size.y<.000001f)throw new InvalidOperationException("Invalid bounds: "+name);
        float s=height/b.size.y;visual.transform.position-=new Vector3(b.center.x,ceiling?b.max.y:b.center.y,ceiling?b.center.z:b.min.z);visual.transform.localPosition*=s;visual.transform.localScale*=s;
        b=BoundsOf(g);var col=g.AddComponent<BoxCollider>();col.center=b.center;col.size=b.size;
        var prefab=PrefabUtility.SaveAsPrefabAsset(g,Prefabs+name+".prefab");UnityEngine.Object.DestroyImmediate(g);return prefab;
    }
    static RaycastHit Surface(Vector3 origin,Vector3 direction,float reach)
    {
        var hits=Physics.RaycastAll(origin,direction,reach,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
        foreach(var h in hits){if(h.collider.transform.IsChildOf(root)||h.collider.GetComponentInParent<HotelItem>()||h.collider.GetComponentInParent<HotelPlayer>())continue;if(Vector3.Dot(h.normal,-direction)>.95f)return h;}
        throw new InvalidOperationException("No mounting surface at "+origin+" toward "+direction);
    }
    static void Mount(GameObject prefab,string name,Vector3 origin,Vector3 direction,float reach=1.5f)
    {
        var hit=Surface(origin,direction,reach);var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);g.name=name;g.transform.SetParent(root,false);g.transform.SetPositionAndRotation(hit.point+hit.normal*.008f,Quaternion.LookRotation(hit.normal,Vector3.up));Undo.RegisterCreatedObjectUndo(g,"Install "+name);
    }
    static void CameraMount(GameObject prefab,string name,Vector3 origin,float yaw)
    {
        var hit=Surface(origin,Vector3.up,2);var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);g.name=name;g.transform.SetParent(root,false);g.transform.SetPositionAndRotation(hit.point-Vector3.up*.008f,Quaternion.Euler(0,yaw,0));Undo.RegisterCreatedObjectUndo(g,"Install "+name);
    }
    [MenuItem("Hotel Together/Facilities/Install supplied safety and electrical models")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before installing facilities.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="HotelLobby")throw new InvalidOperationException("Open HotelLobby first.");
        var doors=UnityEngine.Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None).Where(i=>i.name.StartsWith("Guest room")&&i.kind=="door").OrderBy(i=>i.name).ToArray();if(doors.Length!=16)throw new InvalidOperationException("Expected 16 guest doors.");
        Directory.CreateDirectory(Prefabs);var backup=Path.GetFullPath("../tmp/facilities/HotelLobby-before-installation.unity");if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
        var firstAid=Prepare("wall_mount_first_aid_kit",null,"Wall mounted first aid cabinet",.42f);
        var loto=Prepare("loto_box",null,"Maintenance lockout station",.34f);
        var single=Prepare("light_switch_outlet_-_7mb_-_2part","Light_Switch_Toggle_1_1","Clean single light switch",.12f);
        var dual=Prepare("light_switch_outlet_-_7mb_-_2part","Light_Switch_Toggle_2_1","Clean double light switch",.12f);
        var outlet=Prepare("light_switch_outlet_-_7mb_-_2part","Outlet_1","Clean duplex outlet",.12f);
        var camera=Prepare("cctv-camera",null,"Ceiling mounted security camera",.38f,true);
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Install hotel safety and electrical fittings");
        try
        {
            var old=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Hotel safety and electrical fittings");if(old)Undo.DestroyObjectImmediate(old);
            var group=new GameObject("Hotel safety and electrical fittings");root=group.transform;Undo.RegisterCreatedObjectUndo(group,"Facilities group");Physics.SyncTransforms();
            Mount(firstAid,"First aid - staff room entrance",new Vector3(-5.5f,1.5f,-10.2f),Vector3.back);
            Mount(firstAid,"First aid - upper stair A landing",new Vector3(15,6f,-24),Vector3.back,2);
            Mount(loto,"LOTO - electrical room beside distribution panel",new Vector3(-21.5f,1.5f,-31.8f),Vector3.back,2);
            foreach(var door in doors)Mount(single,"Light switch - "+door.name.Substring(11,3)+" entry",door.transform.TransformPoint(new Vector3(-1.25f,.12f,-.7f)),door.transform.forward);
            Mount(dual,"Light switch - staff room",new Vector3(-5.1f,1.35f,-12),Vector3.forward);
            Mount(single,"Light switch - kitchen",new Vector3(-11.1f,1.35f,-22),Vector3.forward);
            Mount(single,"Light switch - laundry",new Vector3(24.1f,1.35f,-22),Vector3.forward);
            Mount(single,"Light switch - electrical room",new Vector3(-18.3f,1.35f,-22),Vector3.forward);
            Mount(outlet,"Outlet - staff room",new Vector3(-6, .35f,-12),Vector3.forward);
            Mount(outlet,"Outlet - lobby reception",new Vector3(-8.2f,.35f,-9.8f),Vector3.back,2);
            Mount(outlet,"Outlet - lobby lounge",new Vector3(20,.35f,-.2f),Vector3.back,2);
            Mount(outlet,"Outlet - laundry work area",new Vector3(26,.95f,-31.8f),Vector3.back,2);
            CameraMount(camera,"CCTV - lobby entrance",new Vector3(10,3,5.5f),180);
            CameraMount(camera,"CCTV - reception and lifts",new Vector3(1,3,-9.5f),0);
            CameraMount(camera,"CCTV - service corridor",new Vector3(8,3,-19),270);
            CameraMount(camera,"CCTV - upper stair A landing",new Vector3(12,7.5f,-23),0);
            CameraMount(camera,"CCTV - upper west corridor",new Vector3(-14.8f,7.5f,-16),0);
            CameraMount(camera,"CCTV - upper north corridor",new Vector3(16,7.5f,-3),90);
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);Debug.Log("FACILITIES_INSTALLED count="+root.childCount);
        }
        catch{Undo.RevertAllDownToGroup(undo);throw;}
    }
}
