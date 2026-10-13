using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using HotelTogether;

public static class HotelRooftopBuilder
{
    public const string RootName="Hotel furnished rooftop";
    const string Art="Assets/HotelTogether/Art/Rooftop/";
    static Transform root,zone;
    static Mesh cube,sphere,cylinder;
    static GameObject treePrefab;
    static Material stone,wood,woodLight,teal,cream,metal,soil,green,lightGreen,flower,white,net;
    static readonly Dictionary<Material,List<CombineInstance>> batches=new();
    static readonly List<Mesh> temporaryMeshes=new();
    static Vector3 V(float x,float y,float z)=>new Vector3(x,9+y,z);
    static Material Mat(string name,Color color,float smooth=.18f)
    {
        string path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",0);
        m.SetFloat("_SpecularHighlights",0);m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.SetColor("_EmissionColor",Color.black);m.DisableKeyword("_EMISSION");EditorUtility.SetDirty(m);return m;
    }
    static Mesh Primitive(PrimitiveType type){var g=GameObject.CreatePrimitive(type);var m=g.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(g);return m;}
    static void Part(Material material,Mesh mesh,Vector3 position,Vector3 size,Quaternion rotation)
    {
        if(!batches.TryGetValue(material,out var list)){list=new List<CombineInstance>();batches.Add(material,list);}
        list.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(position,rotation,size)});
    }
    static void Box(Material m,Vector3 p,Vector3 size,float yaw=0)=>Part(m,cube,p,size,Quaternion.Euler(0,yaw,0));
    static void Ball(Material m,Vector3 p,Vector3 size)=>Part(m,sphere,p,size,Quaternion.identity);
    static void Pole(Material m,Vector3 p,float radius,float height)=>Part(m,cylinder,p,new Vector3(radius,height/2,radius),Quaternion.identity);
    static void Obstacle(string name,Vector3 center,Vector3 size,float yaw=0)
    {
        var g=new GameObject(name);g.transform.SetParent(zone,false);g.transform.SetPositionAndRotation(center,Quaternion.Euler(0,yaw,0));g.AddComponent<BoxCollider>().size=size;
    }
    static Mesh SaveMesh(string name,Mesh mesh)
    {
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(Art+name+".asset");
        if(existing){existing.Clear();existing.indexFormat=mesh.indexFormat;existing.vertices=mesh.vertices;existing.normals=mesh.normals;existing.uv=mesh.uv;existing.triangles=mesh.triangles;existing.RecalculateBounds();existing.UploadMeshData(false);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;}
        AssetDatabase.CreateAsset(mesh,Art+name+".asset");return mesh;
    }
    static void Begin(string name)
    {
        if(zone)Flush();var g=new GameObject(name);g.transform.SetParent(root,false);zone=g.transform;
    }
    static void Flush()
    {
        foreach(var batch in batches){var mesh=new Mesh{name=zone.name+" - "+batch.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(batch.Value.ToArray(),true,true);mesh.RecalculateBounds();mesh=SaveMesh(mesh.name,mesh);var g=new GameObject(batch.Key.name);g.transform.SetParent(zone,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=batch.Key;}
        batches.Clear();
        foreach(var mesh in temporaryMeshes)UnityEngine.Object.DestroyImmediate(mesh);temporaryMeshes.Clear();
    }
    static void Deck(float x,float z,float w,float d)
    {
        Box(woodLight,V(x,.015f,z),new Vector3(w,.03f,d));
    }
    static void Bed(float x,float z,float width,float depth,bool tree=false)
    {
        Box(stone,V(x,.30f,z),new Vector3(width,.60f,depth));Box(soil,V(x,.606f,z),new Vector3(width-.18f,.018f,depth-.18f));
        for(float dx=-width/2+.4f;dx<width/2-.2f;dx+=.65f)for(float dz=-depth/2+.35f;dz<depth/2-.15f;dz+=.65f){Ball(green,V(x+dx,.9f,z+dz),new Vector3(.64f,.60f,.64f));if(((int)((dx+width)*3))%3==0)Ball(flower,V(x+dx,.99f,z+dz+.08f),new Vector3(.18f,.20f,.18f));}
        if(tree)
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(treePrefab);
            instance.name="Cartoon planter tree";
            instance.transform.SetParent(zone,false);
            instance.transform.localPosition=V(x,.615f,z);
            instance.transform.localRotation=Quaternion.Euler(0,Mathf.Repeat(x*41+z*57,360),0);
            instance.transform.localScale=new Vector3(.35f,.45f,.35f);
        }
        Obstacle("Raised planter",V(x,.30f,z),new Vector3(width,.6f,depth));
    }
    static void Bench(float x,float z,float yaw=0,bool upholstered=false,float width=2.2f)
    {
        var q=Quaternion.Euler(0,yaw,0);var c=V(x,0,z);Vector3 P(float px,float py,float pz)=>c+q*new Vector3(px,py,pz);
        Box(wood,P(0,.40f,0),new Vector3(width,.12f,.72f),yaw);Box(upholstered?teal:woodLight,P(0,.52f,0),new Vector3(width-.12f,.16f,.70f),yaw);
        Box(upholstered?teal:wood,P(0,.84f,.31f),new Vector3(width,.60f,.14f),yaw);
        foreach(float sx in new[]{-width/2+.16f,width/2-.16f}){Box(metal,P(sx,.21f,0),new Vector3(.10f,.42f,.56f),yaw);if(upholstered)Box(wood,P(sx,.70f,0),new Vector3(.14f,.17f,.74f),yaw);}
        var item=new GameObject(upholstered?"Rooftop lounge sofa":"Rooftop garden bench");item.transform.SetParent(zone,false);item.transform.SetPositionAndRotation(P(0,.52f,0),q);
        var col=item.AddComponent<BoxCollider>();col.center=new Vector3(0,.12f,0);col.size=new Vector3(width,.95f,.82f);
        var seat=item.AddComponent<HotelItem>();seat.kind="seat";seat.displayName=item.name;seat.dimensions=new Vector3(width,1.04f,.82f);seat.seatEyeHeight=1.30f;
    }
    static void Coffee(float x,float z)
    {Box(woodLight,V(x,.42f,z),new Vector3(1.4f,.10f,.8f));Box(metal,V(x,.20f,z),new Vector3(.65f,.40f,.45f));Obstacle("Low coffee table",V(x,.25f,z),new Vector3(1.4f,.5f,.8f));}
    static void Umbrella(float x,float z)
    {
        Pole(metal,V(x,1.45f,z),.07f,2.90f);Box(stone,V(x,.05f,z),new Vector3(.65f,.10f,.65f));
        const int n=12;var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;int o=vertices.Count;vertices.Add(V(x,3.05f,z));vertices.Add(V(x+Mathf.Cos(a)*1.9f,2.65f,z+Mathf.Sin(a)*1.9f));vertices.Add(V(x+Mathf.Cos(b)*1.9f,2.65f,z+Mathf.Sin(b)*1.9f));triangles.AddRange(new[]{o,o+2,o+1,o,o+1,o+2});}
        var mesh=new Mesh{name="Umbrella canopy"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
        // Explicit per-face normals avoid cancelling normals on double-sided canopy triangles.
        mesh.normals=vertices.Select((p,i)=>Vector3.up).ToArray();
        temporaryMeshes.Add(mesh);
        Part(cream,mesh,Vector3.zero,Vector3.one,Quaternion.identity);
    }
    static void Dining(float x,float z,bool parasol)
    {
        Box(woodLight,V(x,.79f,z),new Vector3(1.55f,.10f,1.55f));foreach(float sx in new[]{-.57f,.57f})foreach(float sz in new[]{-.57f,.57f})Box(metal,V(x+sx,.39f,z+sz),new Vector3(.08f,.78f,.08f));
        Obstacle("Outdoor dining table",V(x,.43f,z),new Vector3(1.55f,.86f,1.55f));
        Bench(x,z+1.25f,0,true,1.25f);Bench(x,z-1.25f,180,true,1.25f);if(parasol)Umbrella(x,z);
    }
    static void Picnic(float x,float z)
    {
        Box(woodLight,V(x,.80f,z),new Vector3(3,.10f,1.10f));foreach(float sx in new[]{-1.1f,1.1f})Box(metal,V(x+sx,.40f,z),new Vector3(.12f,.8f,1));
        Obstacle("Picnic table",V(x,.45f,z),new Vector3(3,.9f,1.1f));Bench(x,z-1.15f,180,false,3);Bench(x,z+1.15f,0,false,3);
    }
    static void Grill(float x,float z)
    {
        Box(metal,V(x,.50f,z),new Vector3(1.4f,1,.65f));Box(woodLight,V(x,.78f,z+.48f),new Vector3(1.5f,.08f,.25f));
        Part(metal,cylinder,V(x,1.02f,z),new Vector3(.62f,.66f,.62f),Quaternion.Euler(0,0,90));Box(white,V(x,1.03f,z+.35f),new Vector3(.55f,.045f,.06f));
        foreach(float sx in new[]{-.4f,-.15f,.15f,.4f})Ball(white,V(x+sx,.76f,z+.345f),new Vector3(.07f,.07f,.025f));
        Obstacle("Barbecue grill",V(x,.70f,z),new Vector3(1.5f,1.4f,1));
    }
    static void Lamp(float x,float z)
    {Pole(metal,V(x,.4f,z),.10f,.80f);Pole(cream,V(x,.79f,z),.11f,.10f);}
    [MenuItem("Hotel Together/Environment/Build furnished rooftop")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        treePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(HotelTreeModelBuilder.PrefabPath);if(!treePrefab)throw new InvalidOperationException("Prepare the cartoon tree model first.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var original=GameObject.Find("Level 3 - empty rooftop zones");
        if(scene.name!="HotelLobby"||!original)throw new InvalidOperationException("Open the HotelLobby rooftop scene.");
        Directory.CreateDirectory(Art);string backup=Path.GetFullPath("../tmp/rooftop/HotelLobby-before-furnishing.unity");if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Furnish hotel rooftop");
        var old=GameObject.Find(RootName);if(old)Undo.DestroyObjectImmediate(old);var g=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(g,"Furnish rooftop");root=g.transform;zone=null;batches.Clear();
        cube=Primitive(PrimitiveType.Cube);sphere=Primitive(PrimitiveType.Sphere);cylinder=Primitive(PrimitiveType.Cylinder);
        stone=Mat("Rooftop warm limestone",new Color(.70f,.66f,.55f));wood=Mat("Rooftop warm timber",new Color(.36f,.20f,.10f));woodLight=Mat("Rooftop honey timber",new Color(.59f,.39f,.20f));teal=Mat("Rooftop teal upholstery",new Color(.075f,.31f,.34f));cream=Mat("Rooftop linen shade",new Color(.77f,.73f,.61f));metal=Mat("Rooftop graphite metal",new Color(.055f,.075f,.08f));soil=Mat("Rooftop planter soil",new Color(.16f,.12f,.075f));green=Mat("Rooftop deep foliage",new Color(.19f,.38f,.16f));lightGreen=Mat("Rooftop light foliage",new Color(.39f,.52f,.22f));flower=Mat("Rooftop lavender flowers",new Color(.48f,.32f,.55f));white=Mat("Rooftop muted white",new Color(.77f,.79f,.74f));net=Mat("Rooftop table tennis",new Color(.07f,.32f,.38f));
        Begin("R01 rooftop garden");Deck(21,-28,14,8);Bed(21,-31.4f,12,1.5f);Bed(27.2f,-27.2f,1.5f,6.0f);Bed(15,-28,1.3f,5);foreach(float x in new[]{16.8f,25.5f})Bed(x,-29.6f,1.4f,1.4f,true);Bench(20,-26.2f);Bench(24,-26.2f);Lamp(14.3f,-24.8f);Lamp(27.8f,-24.8f);
        Begin("R02 shaded pavilion");Deck(7,-29,10.5f,6.5f);Bench(4.5f,-30.2f,0,true,3);Bench(9.5f,-30.2f,0,true,3);Coffee(4.5f,-28.7f);Coffee(9.5f,-28.7f);
        for(float x=2.1f;x<=12;x+=.34f)Box(wood,V(x,1.45f,-32.6f),new Vector3(.12f,2.9f,.12f));Bed(2.6f,-26,1.2f,1.2f,true);Bed(11.7f,-26,1.2f,1.2f,true);
        Begin("R05 outdoor activity terrace");Deck(23,-14.2f,9,10);
        Box(net,V(23,.79f,-15),new Vector3(1.525f,.06f,2.74f));Box(white,V(23,.826f,-15),new Vector3(.018f,.008f,2.72f));foreach(float x in new[]{22.25f,23.75f})Box(white,V(x,.826f,-15),new Vector3(.018f,.008f,2.72f));foreach(float z in new[]{-16.36f,-13.64f})Box(white,V(23,.826f,z),new Vector3(1.51f,.008f,.018f));Box(metal,V(23,.9f,-15),new Vector3(1.65f,.15f,.02f));foreach(float x in new[]{22.5f,23.5f})foreach(float z in new[]{-16f,-14f})Box(metal,V(x,.39f,z),new Vector3(.075f,.78f,.075f));Obstacle("Table tennis table",V(23,.45f,-15),new Vector3(1.65f,.9f,2.74f));Bench(27,-12.5f,90,true);Bed(27.2f,-19,1.2f,1.2f,true);Bed(27.2f,-8,1.3f,2.2f);Lamp(18.2f,-19.7f);
        Begin("R08 barbecue and picnic terrace");Deck(21,1.3f,13,9.5f);Grill(16.7f,-1.9f);Grill(19,-1.9f);Box(stone,V(22,.47f,-2),new Vector3(3.2f,.94f,.8f));Box(white,V(22,.97f,-2),new Vector3(3.35f,.06f,.95f));Obstacle("Barbecue preparation counter",V(22,.5f,-2),new Vector3(3.35f,1,.95f));Picnic(18,2);Picnic(24,2);Bed(27.4f,1.8f,1.2f,5.8f);Bed(15,5.9f,1.2f,1.2f,true);Lamp(14.2f,-3.8f);Lamp(27.8f,5.5f);
        Begin("R09 outdoor dining terrace");Deck(-22,-8,9,25);
        foreach(float x in new[]{-24f,-19f})foreach(float z in new[]{-17f,-9f,-1f})Dining(x,z,true);
        Bed(-25.8f,5.4f,1.2f,1.2f,true);Bed(-19,5.4f,4.5f,1.2f);Bed(-25.8f,-21,1.2f,1.2f,true);foreach(float z in new[]{-20f,-12f,-4f,4f})Lamp(-26.4f,z);
        Begin("R06 quiet rooftop lounge");Deck(-4,-16,7.8f,6);Bench(-6,-18.1f,0,true,2.4f);Bench(-2,-18.1f,0,true,2.4f);Coffee(-6,-16.6f);Coffee(-2,-16.6f);Bed(-7.5f,-14,1,1,true);Bed(-.5f,-14,1,1,true);
        Begin("R03 R04 service room details");
        foreach(float x in new[]{-3f,-11f})for(float y=.8f;y<2.3f;y+=.16f)Box(metal,V(x,y,-24.86f),new Vector3(2.1f,.06f,.025f));
        // Equipment stays inside its existing service rooms, clear of their doors.
        Box(metal,V(-5,.65f,-30),new Vector3(3.4f,1.3f,2));Obstacle("Enclosed ventilation unit",V(-5,.65f,-30),new Vector3(3.4f,1.3f,2));
        foreach(float x in new[]{-18f,-24f}){Pole(cream,V(x,1.25f,-30),2.1f,2.5f);Obstacle("Enclosed reserve water tank",V(x,1.25f,-30),new Vector3(2.1f,2.5f,2.1f));}
        Begin("Rooftop circulation paving");
        Box(stone,V(1,.006f,-23),new Vector3(55,.012f,3.8f));Box(stone,V(11,.006f,-10),new Vector3(3.8f,.012f,26));Box(stone,V(-15,.006f,-16),new Vector3(3.8f,.012f,14));Box(stone,V(-2,.006f,-9),new Vector3(29.8f,.012f,3.8f));Box(stone,V(-11,.006f,-1),new Vector3(3.8f,.012f,12));
        Flush();
        foreach(var r in original.GetComponentsInChildren<Renderer>())if(r.name.StartsWith("Roof slab")){Undo.RecordObject(r,"Warm rooftop paving");r.sharedMaterial=stone;EditorUtility.SetDirty(r);}
        foreach(var t in original.GetComponentsInChildren<Transform>())if(t.name.StartsWith("R0")&&t.name.Contains("label")){foreach(var r in t.GetComponents<Renderer>()){Undo.RecordObject(r,"Hide rooftop construction labels");r.enabled=false;EditorUtility.SetDirty(r);}}
        foreach(var a in UnityEngine.Object.FindObjectsByType<HotelCityAtmosphere>(FindObjectsSortMode.None))a.SuspendForSceneSave();foreach(var q in UnityEngine.Object.FindObjectsByType<HotelGraphicsQuality>(FindObjectsSortMode.None))q.SuspendForSceneSave();
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);
        Debug.Log("ROOFTOP_COMPLETE garden, pavilion, activity, BBQ, dining, lounge and service equipment; skylight and main access preserved.");
    }
}
