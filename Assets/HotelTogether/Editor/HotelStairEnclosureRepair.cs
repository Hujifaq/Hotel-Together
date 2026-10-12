using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class HotelStairEnclosureRepair
{
    static GameObject Box(Transform parent,string name,Vector3 center,Vector3 size,Material material,bool collision=true)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.position=center;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    public static void Repair(Transform stairs)
    {
        const string art="Assets/HotelTogether/Art/Stairways/";
        var stone=AssetDatabase.LoadAssetAtPath<Material>(art+"Stair warm stone.mat");
        var cream=AssetDatabase.LoadAssetAtPath<Material>(art+"Stair ivory guard.mat");
        var teal=AssetDatabase.LoadAssetAtPath<Material>(art+"Stair teal wall panel.mat");
        foreach(var config in new[]{new Vector3(13,-8,-1),new Vector3(-13,-14,1)}){
            string letter=config.x>0?"A":"B";var parent=stairs.Find("Stair "+letter+" compact switchback");if(!parent)throw new System.InvalidOperationException("Compact stairs must exist before sealing.");
            var previous=parent.Find("Sealed floor seams and enclosure");if(previous)Object.DestroyImmediate(previous.gameObject);
            var root=new GameObject("Sealed floor seams and enclosure");root.transform.SetParent(parent,false);
            float wall=config.x,z=config.y,front=z-config.z*2.2f;
            HotelStairSitePreparation.RemoveEntranceFloorOverlap(stairs,wall,front);
            for(int floor=0;floor<3;floor++){
                float y=floor*4.5f;
                // Replace both overlapping plates with a single full-width entrance floor.
                foreach(var name in new[]{letter+" level "+floor+" full front landing",letter+" level "+floor+" doorway threshold"}){var old=parent.Find(name);if(old)Object.DestroyImmediate(old.gameObject);}
                Box(root.transform,letter+" continuous entrance floor "+floor,new Vector3(wall+1.825f,y-.08f,front),new Vector3(4.15f,.16f,1.4f),stone);
                // Fill exactly the strip removed by the site cut, excluding the entrance rectangle.
                float a=z-2.99f,b=front-.7f,c=front+.7f,d=z+2.99f;
                if(b>a)Box(root.transform,letter+" corridor floor seam rear "+floor,new Vector3(wall,y-.08f,(a+b)/2),new Vector3(.5f,.16f,b-a),stone);
                if(d>c)Box(root.transform,letter+" corridor floor seam front "+floor,new Vector3(wall,y-.08f,(c+d)/2),new Vector3(.5f,.16f,d-c),stone);
                // Join the walls to the real door frame; the previous opening left small slits.
                foreach(var side in new[]{"rear","front"}){
                    var t=parent.Find(letter+" entry wall "+side+" "+floor);if(!t)continue;
                    var bounds=t.GetComponent<Renderer>().bounds;float min=side=="front"?front+.665f:bounds.min.z,max=side=="rear"?front-.665f:bounds.max.z;
                    var scale=t.localScale;scale.z=max-min;t.localScale=scale;var pos=t.position;pos.z=(max+min)/2;t.position=pos;
                }
                var lintel=parent.Find(letter+" entry lintel "+floor);if(lintel){var bounds=lintel.GetComponent<Renderer>().bounds;float top=floor==2?12:y+4.5f;lintel.localScale=new Vector3(.2f,top-y-2.5f,1.33f);lintel.position=new Vector3(wall,(top+y+2.5f)/2,front);}
                // Roof stair headhouse is 3 m high, matching the existing roof partitions.
                if(floor==2)foreach(var t in parent.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith(letter+" entry ")&&t.name.EndsWith(" 2")).ToArray()){
                    if(!t.GetComponent<Renderer>())continue;var bounds=t.GetComponent<Renderer>().bounds;if(bounds.max.y>12.001f){float min=bounds.min.y;var size=t.localScale;size.y=12-min;t.localScale=size;var pos=t.position;pos.y=(12+min)/2;t.position=pos;}
                }
            }
            // Slightly inset opaque faces avoid coplanar overlap with surviving imported walls.
            Box(root.transform,letter+" continuous outer side wall",new Vector3(wall+3.93f,6,z),new Vector3(.14f,12,6.04f),cream);
            foreach(float sign in new[]{-1f,1f})Box(root.transform,letter+" continuous end wall "+sign,new Vector3(wall+2,6,z+sign*2.93f),new Vector3(4.24f,12,.14f),cream);
            for(int floor=0;floor<3;floor++){
                float y=floor*4.5f+.54f;
                Box(root.transform,letter+" side wall teal finish "+floor,new Vector3(wall+3.845f,y,z),new Vector3(.03f,1.08f,5.72f),teal,false);
                foreach(float sign in new[]{-1f,1f})Box(root.transform,letter+" end wall teal finish "+floor+" "+sign,new Vector3(wall+2,y,z+sign*2.845f),new Vector3(3.69f,1.08f,.03f),teal,false);
            }
            Box(root.transform,letter+" sealed stair headhouse roof",new Vector3(wall+2,11.91f,z),new Vector3(4.24f,.18f,6.24f),cream);
        }
        HotelStairWayfinding.Decorate(stairs);Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(stairs.gameObject.scene);
    }
    [MenuItem("Hotel Together/Environment/Seal stair entrance floors and enclosure")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit Play Mode first.");
        var root=GameObject.Find(HotelStairBuilder.RootName);if(!root)throw new System.InvalidOperationException("Rebuilt stair root missing.");
        var scene=root.scene;string path=Path.GetFullPath("../tmp/stairs/HotelLobby-before-enclosure-repair.unity");if(!File.Exists(path))EditorSceneManager.SaveScene(scene,path,true);
        Undo.RegisterFullObjectHierarchyUndo(root,"Seal stair enclosure");Repair(root.transform);
        foreach(var a in Object.FindObjectsByType<HotelTogether.HotelCityAtmosphere>(FindObjectsSortMode.None))a.SuspendForSceneSave();foreach(var q in Object.FindObjectsByType<HotelTogether.HotelGraphicsQuality>(FindObjectsSortMode.None))q.SuspendForSceneSave();
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
    }
}
