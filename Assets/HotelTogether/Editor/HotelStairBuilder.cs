using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using HotelTogether;

public static class HotelStairBuilder
{
    public const string RootName="Hotel rebuilt stairways";
    const string Art="Assets/HotelTogether/Art/Stairways/";
    static Transform parent;
    static Material stone,tread,metal,wood,cream,teal;
    static readonly List<Vector3> vertices=new();static readonly List<int> indices=new();
    static Material Mat(string name,Color color)
    {var path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.2f);m.SetFloat("_Metallic",0);m.SetFloat("_SpecularHighlights",0);m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");EditorUtility.SetDirty(m);return m;}
    static GameObject Box(string name,Vector3 center,Vector3 size,Material mat,bool collide=true)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.position=center;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;if(!collide)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
    static void Bar(string name,Vector3 from,Vector3 to,float thickness,Material material,bool collide=false)
    {var d=to-from;var g=Box(name,(from+to)/2,new Vector3(thickness,thickness,d.magnitude),material,collide);g.transform.rotation=Quaternion.LookRotation(d);}
    static void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward)
    {int start=vertices.Count;vertices.AddRange(new[]{a,b,c,d});if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0)indices.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});else indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});}
    static Mesh Save(string name,Mesh mesh)
    {string path=Art+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){old.Clear();old.vertices=mesh.vertices;old.normals=mesh.normals;old.triangles=mesh.triangles;old.RecalculateBounds();old.UploadMeshData(false);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(mesh);return old;}AssetDatabase.CreateAsset(mesh,path);return mesh;}
    static Mesh Finish(string name)
    {var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();vertices.Clear();indices.Clear();return Save(name,mesh);}
    static void Flight(string name,float x,float startZ,float endZ,float startY)
    {
        const int count=14;const float height=2.25f,width=1.79f;float direction=Mathf.Sign(endZ-startZ),run=Mathf.Abs(endZ-startZ),rise=height/count;
        float left=x-width/2,right=x+width/2;
        for(int i=0;i<count;i++){
            float z0=startZ+direction*run*i/count,z1=startZ+direction*run*(i+1)/count,y=startY+rise*(i+1),previous=startY+rise*i;
            Face(new Vector3(left,y,z0),new Vector3(right,y,z0),new Vector3(right,y,z1),new Vector3(left,y,z1),Vector3.up);
            Face(new Vector3(left,previous,z0),new Vector3(right,previous,z0),new Vector3(right,y,z0),new Vector3(left,y,z0),Vector3.back*direction);
            Face(new Vector3(left,y,z0),new Vector3(left,y,z1),new Vector3(left,previous+rise-.14f,z1),new Vector3(left,previous-.14f,z0),Vector3.left);
            Face(new Vector3(right,y,z0),new Vector3(right,y,z1),new Vector3(right,previous+rise-.14f,z1),new Vector3(right,previous-.14f,z0),Vector3.right);
            // Quiet, broad tread edge rather than a high-contrast stripe.
            Box(name+" tread edge "+i,new Vector3(x,y+.004f,z0+direction*.035f),new Vector3(width-.035f,.008f,.045f),tread,false);
        }
        Face(new Vector3(left,startY-.14f,startZ),new Vector3(right,startY-.14f,startZ),new Vector3(right,startY+height-.14f,endZ),new Vector3(left,startY+height-.14f,endZ),Vector3.down);
        Face(new Vector3(left,startY+height-.14f,endZ),new Vector3(right,startY+height-.14f,endZ),new Vector3(right,startY+height,endZ),new Vector3(left,startY+height,endZ),Vector3.forward*direction);
        Face(new Vector3(left,startY-.14f,startZ),new Vector3(right,startY-.14f,startZ),new Vector3(right,startY,startZ),new Vector3(left,startY,startZ),Vector3.back*direction);
        var mesh=Finish(name+" closed risers");var visual=new GameObject(name+" closed stone flight");visual.transform.SetParent(parent,false);visual.AddComponent<MeshFilter>().sharedMesh=mesh;visual.AddComponent<MeshRenderer>().sharedMaterial=stone;
        Face(new Vector3(left,startY,startZ),new Vector3(right,startY,startZ),new Vector3(right,startY+height,endZ),new Vector3(left,startY+height,endZ),Vector3.up);
        var ramp=new GameObject(name+" smooth walking surface");ramp.transform.SetParent(parent,false);ramp.AddComponent<MeshCollider>().sharedMesh=Finish(name+" ramp collision");
        bool lower=name.Contains("lower");float guardX=lower?right+.025f:left-.025f;
        float startTop=startY+1.04f,endTop=startY+height+1.04f;
        float x0=guardX-.025f,x1=guardX+.025f;
        var a=new Vector3(x0,startY,startZ);var b=new Vector3(x0,startY+height,endZ);var c=new Vector3(x0,endTop,endZ);var d=new Vector3(x0,startTop,startZ);
        Face(a,b,c,d,Vector3.left);Face(a+Vector3.right*.05f,d+Vector3.right*.05f,c+Vector3.right*.05f,b+Vector3.right*.05f,Vector3.right);
        Face(d,c,c+Vector3.right*.05f,d+Vector3.right*.05f,Vector3.up);
        Face(a,d,d+Vector3.right*.05f,a+Vector3.right*.05f,Vector3.back*direction);
        Face(b,b+Vector3.right*.05f,c+Vector3.right*.05f,c,Vector3.forward*direction);
        var guard=new GameObject(name+" solid inner guard");guard.transform.SetParent(parent,false);var guardMesh=Finish(name+" solid inner guard");guard.AddComponent<MeshFilter>().sharedMesh=guardMesh;guard.AddComponent<MeshRenderer>().sharedMaterial=cream;guard.AddComponent<MeshCollider>().sharedMesh=guardMesh;
        // Identical 52 mm timber handrails, both inner guards and outside walls.
        foreach(float railX in new[]{guardX,lower?left+.06f:right-.06f})
            Bar(name+" uniform timber handrail",new Vector3(railX,startTop,startZ),new Vector3(railX,endTop,endZ),.052f,wood);
    }
    static void WallPiece(string name,float x,float z0,float z1,float y0,float y1)
    {
        if(z1-z0<.01f||y1-y0<.01f)return;
        Box(name,new Vector3(x,(y0+y1)/2,(z0+z1)/2),new Vector3(.20f,y1-y0,z1-z0),cream);
        float baseY=Mathf.Floor(y0/4.5f)*4.5f;
        float lo=Mathf.Max(y0,baseY),hi=Mathf.Min(y1,baseY+1.08f);
        if(hi>lo)Box(name+" teal panel",new Vector3(x+.115f,(lo+hi)/2,(z0+z1)/2),new Vector3(.025f,hi-lo,z1-z0),teal,false);
    }
    static void Stair(string letter,float leftWall,float centerZ,float direction)
    {
        var g=new GameObject("Stair "+letter+" compact switchback");g.transform.SetParent(parent,false);var previous=parent;parent=g.transform;
        float lowerX=leftWall+1.055f,upperX=leftWall+2.945f;
        float front=centerZ-direction*2.2f,rear=centerZ+direction*2.4f,start=centerZ-direction*1.5f,end=centerZ+direction*1.9f;
        float doorZ=front;
        var original=GameObject.Find(letter=="A"?"Prop_0009":"Prop_0014").GetComponent<HotelItem>();
        Undo.RecordObject(original.transform,"Move stair corner entry");Undo.RecordObject(original,"Update relocated door home");
        original.transform.position=new Vector3(leftWall,original.transform.position.y,doorZ);original.open=false;original.home=original.transform.position;original.homeRotation=original.transform.rotation;original.door.outside=-1;original.PoseDoor(0);EditorUtility.SetDirty(original);
        // Original detached frame collision bodies do not follow the interactive door.
        foreach(var collider in UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)){
            if(!collider.transform.parent||!collider.transform.parent.name.StartsWith("Structure collision"))continue;
            if(Mathf.Abs(collider.bounds.center.x-leftWall)<.2f&&Mathf.Abs(collider.bounds.center.z-centerZ)<.8f&&collider.bounds.max.y<2.6f){Undo.RecordObject(collider,"Remove old frame collision");collider.enabled=false;}
        }
        for(int floor=0;floor<3;floor++){
            float y=floor*4.5f;
            Box(letter+" level "+floor+" full front landing",new Vector3(leftWall+2,y-.08f,front),new Vector3(3.8f,.16f,1.4f),stone);
            Box(letter+" level "+floor+" doorway threshold",new Vector3(leftWall,y-.08f,front),new Vector3(.4f,.16f,1.4f),stone);
            float min=centerZ-3,max=centerZ+3,doorMin=doorZ-.665f,doorMax=doorZ+.665f;
            WallPiece(letter+" entry wall rear "+floor,leftWall,min,doorMin,y,y+4.5f);
            WallPiece(letter+" entry wall front "+floor,leftWall,doorMax,max,y,y+4.5f);
            WallPiece(letter+" entry lintel "+floor,leftWall,doorMin,doorMax,y+2.50f,y+4.5f);
            if(floor>0){var clone=UnityEngine.Object.Instantiate(original.gameObject,parent);clone.name="Stair "+letter+" corner door level "+floor;clone.transform.position+=Vector3.up*y;var item=clone.GetComponent<HotelItem>();item.door=JsonUtility.FromJson<DoorData>(JsonUtility.ToJson(original.door));item.home=clone.transform.position;item.homeRotation=clone.transform.rotation;item.open=false;item.PoseDoor(0);}
            if(floor==2)continue;
            Flight(letter+" lower "+floor,lowerX,start,end,y);
            Box(letter+" half landing "+floor,new Vector3(leftWall+2,y+2.25f-.08f,rear),new Vector3(3.8f,.16f,1),stone);
            Flight(letter+" upper "+floor,upperX,end,start,y+2.25f);
            float railY=y+3.29f,backRailZ=centerZ+direction*2.77f;
            Bar(letter+" uniform half landing handrail "+floor,new Vector3(leftWall+.22f,railY,end),new Vector3(leftWall+.22f,railY,backRailZ),.052f,wood);
            Bar(letter+" uniform back wall handrail "+floor,new Vector3(leftWall+.22f,railY,backRailZ),new Vector3(leftWall+3.78f,railY,backRailZ),.052f,wood);
            Bar(letter+" uniform half landing return "+floor,new Vector3(leftWall+3.78f,railY,backRailZ),new Vector3(leftWall+3.78f,railY,end),.052f,wood);
        }
        parent=previous;
    }
    [MenuItem("Hotel Together/Environment/Rebuild stairways and passage guards")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var originals=new[]{GameObject.Find("Upper floors and rooftop")?.transform.Find("Stair A - ground to roof"),GameObject.Find("Upper floors and rooftop")?.transform.Find("Stair B - ground to roof")};
        if(originals.Any(t=>!t))throw new InvalidOperationException("Both original stair roots are required.");
        Directory.CreateDirectory(Art);string backup=Path.GetFullPath("../tmp/stairs/HotelLobby-before-stair-rebuild.unity");if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Rebuild hotel stairs");
        string compactBackup=Path.GetFullPath("../tmp/stairs/HotelLobby-before-compact-stairs.unity");if(!File.Exists(compactBackup))EditorSceneManager.SaveScene(scene,compactBackup,true);var old=GameObject.Find(RootName);if(old)Undo.DestroyObjectImmediate(old);foreach(var t in originals){Undo.RecordObject(t.gameObject,"Replace original stairs");t.gameObject.SetActive(false);EditorUtility.SetDirty(t.gameObject);}
        var root=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(root,"Rebuild stairs");parent=root.transform;vertices.Clear();indices.Clear();
        stone=Mat("Stair warm stone",new Color(.58f,.55f,.47f));tread=Mat("Stair tread nosing",new Color(.44f,.42f,.36f));metal=Mat("Stair graphite rails",new Color(.09f,.12f,.13f));wood=Mat("Stair timber handrail",new Color(.35f,.23f,.13f));
        cream=Mat("Stair ivory guard",new Color(.75f,.72f,.64f));teal=Mat("Stair teal wall panel",new Color(.035f,.19f,.21f));HotelStairSitePreparation.Prepare(parent,13,-8);HotelStairSitePreparation.Prepare(parent,-13,-14);Stair("A",13,-8,-1);Stair("B",-13,-14,1);HotelStairEnclosureRepair.Repair(parent);
        foreach(var a in UnityEngine.Object.FindObjectsByType<HotelCityAtmosphere>(FindObjectsSortMode.None))a.SuspendForSceneSave();foreach(var q in UnityEngine.Object.FindObjectsByType<HotelGraphicsQuality>(FindObjectsSortMode.None))q.SuspendForSceneSave();
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);
        Debug.Log("STAIRS_COMPLETE: compact switchbacks, corner entries and uniform timber handrails.");
    }
}
