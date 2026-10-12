using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using HotelTogether;

public static class HotelParkingBuilder
{
    const string Art="Assets/HotelTogether/Art/Parking/";
    public const string RootName="Hotel front parking and arrival court";
    static Transform root;
    static Mesh cube,sphere,cylinder;
    static readonly Dictionary<Material,List<CombineInstance>> batches=new();
    static Material asphalt,stone,white,blue,grass,leaf,leafLight,bark,dark,metal,glass,red,glow,headlamp;
    static Material Mat(string name,Color color,float smooth=.2f,float metallic=0)
    {var path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metallic);EditorUtility.SetDirty(m);return m;}
    static Mesh Primitive(PrimitiveType type){var g=GameObject.CreatePrimitive(type);var mesh=g.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(g);return mesh;}
    static void ReduceCarGlare(Material material,float smoothness,float metallic)
    {
        material.SetFloat("_Smoothness",smoothness);material.SetFloat("_Metallic",metallic);
        material.SetFloat("_SpecularHighlights",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        material.SetFloat("_EnvironmentReflections",0);material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        material.SetColor("_EmissionColor",Color.black);material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;EditorUtility.SetDirty(material);
    }
    static void TuneCarMaterials(Transform parking)
    {
        headlamp=Mat("Parked car unlit headlamp lenses",new Color(.48f,.52f,.50f),.18f);
        ReduceCarGlare(headlamp,.18f,0);
        foreach(var renderer in parking.GetComponentsInChildren<Renderer>())
        {
            if(!renderer.transform.parent || !renderer.transform.parent.name.StartsWith("Parked sedan - "))continue;
            if(renderer.name=="Headlamp"){Undo.RecordObject(renderer,"Reduce parked car glare");renderer.sharedMaterial=headlamp;EditorUtility.SetDirty(renderer);continue;}
            foreach(var material in renderer.sharedMaterials)
                if(material.name.EndsWith("parked car"))ReduceCarGlare(material,.28f,0);
                else if(material.name=="Car dark blue glass")ConfigureCarGlass(material);
                else if(material.name=="Wheel alloy")ReduceCarGlare(material,.3f,.35f);
                else if(material.name=="Rear lights")ReduceCarGlare(material,.2f,0);
        }
    }
    static void ConfigureCarGlass(Material material)
    {
        // Dark tint stays stable at grazing angles and cannot produce a sun highlight.
        material.shader=Shader.Find("Universal Render Pipeline/Unlit");material.shaderKeywords=Array.Empty<string>();
        material.SetColor("_BaseColor",new Color(.075f,.12f,.15f,1));material.SetTexture("_BaseMap",null);
        material.SetFloat("_Surface",0);material.SetFloat("_Blend",0);material.SetFloat("_AlphaClip",0);
        material.SetFloat("_SrcBlend",1);material.SetFloat("_DstBlend",0);material.SetFloat("_ZWrite",1);material.SetFloat("_Cull",2);
        material.SetOverrideTag("RenderType","Opaque");material.renderQueue=2000;
        material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;EditorUtility.SetDirty(material);
    }
    static void RepairWindowMesh(Mesh mesh)
    {
        var source=mesh.vertices;var vertices=new List<Vector3>();var normals=new List<Vector3>();var indices=new List<int>();
        for(int offset=0;offset<source.Length;offset+=4)
        {
            var normal=Vector3.Cross(source[offset+1]-source[offset],source[offset+2]-source[offset]).normalized;
            var center=(source[offset]+source[offset+1]+source[offset+2]+source[offset+3])*.25f;
            var outward=new Vector3(center.x,0,center.z+.15f);
            bool reverse=Vector3.Dot(normal,outward)<0;if(reverse)normal=-normal;
            int start=vertices.Count;
            for(int side=0;side<2;side++)for(int corner=0;corner<4;corner++){vertices.Add(source[offset+corner]);normals.Add(side==0?normal:-normal);}
            int[] front=reverse?new[]{0,2,1,0,3,2}:new[]{0,1,2,0,2,3};
            foreach(int index in front)indices.Add(start+index);
            for(int triangle=0;triangle<front.Length;triangle+=3){indices.Add(start+4+front[triangle]);indices.Add(start+4+front[triangle+2]);indices.Add(start+4+front[triangle+1]);}
        }
        mesh.Clear();mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();mesh.UploadMeshData(false);EditorUtility.SetDirty(mesh);
    }
    [MenuItem("Hotel Together/Environment/Repair parked car windshields")]
    public static void RepairParkedCarWindshields()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var parking=GameObject.Find(RootName);if(!parking)throw new InvalidOperationException("Parking court is missing.");
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"Sedan cabin windows.asset");
        // Existing assets have either 24 shared vertices or 48 separate face vertices.
        if(mesh.vertexCount==24)RepairWindowMesh(mesh);
        TuneCarMaterials(parking.transform);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(parking.scene);
    }
    [MenuItem("Hotel Together/Environment/Reduce parked car glare")]
    public static void ReduceParkedCarGlare()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var parking=GameObject.Find(RootName);if(!parking)throw new InvalidOperationException("Parking court is missing.");
        TuneCarMaterials(parking.transform);AssetDatabase.SaveAssets();
        var scene=parking.scene;EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static void Piece(Material mat,Mesh mesh,Vector3 position,Vector3 size,Quaternion rotation)
    {if(!batches.TryGetValue(mat,out var list)){list=new List<CombineInstance>();batches.Add(mat,list);}list.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(position,rotation,size)});}
    static void Box(Material m,Vector3 p,Vector3 size,float yaw=0){Piece(m,cube,p,size,Quaternion.Euler(0,yaw,0));}
    static void Line(Vector3 a,Vector3 b,float width=.10f){var delta=b-a;Box(white,(a+b)*.5f,new Vector3(width,.008f,delta.magnitude),Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg);}
    static void Shrub(float x,float z,float s=.8f){Piece(leaf,sphere,new Vector3(x,.37f,z),new Vector3(s,.65f,s),Quaternion.identity);}
    static void Tree(float x,float z,float size=1)
    {Piece(bark,cylinder,new Vector3(x,1.55f*size,z),new Vector3(.28f,1.55f,.28f)*size,Quaternion.identity);Piece(leaf,sphere,new Vector3(x,3.7f*size,z),new Vector3(2.7f,2.8f,2.7f)*size,Quaternion.identity);Piece(leafLight,sphere,new Vector3(x-.7f*size,3.2f*size,z+.2f),new Vector3(2,2.2f,2)*size,Quaternion.identity);}
    static void Island(float x,float z,float width,float depth,bool tree=true)
    {Box(stone,new Vector3(x,.04f,z),new Vector3(width,.16f,depth));Box(grass,new Vector3(x,.125f,z),new Vector3(width-.30f,.025f,depth-.30f));for(float px=x-width*.5f+.6f;px<x+width*.5f-.4f;px+=1.2f){Shrub(px,z-depth*.5f+.6f);Shrub(px,z+depth*.5f-.6f);}if(tree)Tree(x,z,.8f);}
    static void Bollard(float x,float z)
    {Piece(dark,cylinder,new Vector3(x,.40f,z),new Vector3(.13f,.40f,.13f),Quaternion.identity);Piece(glow,cylinder,new Vector3(x,.73f,z),new Vector3(.145f,.07f,.145f),Quaternion.identity);}
    static void Bay(float x,float z,bool north)
    {Line(new Vector3(x-1.4f,.016f,z-2.65f),new Vector3(x-1.4f,.016f,z+2.65f));Line(new Vector3(x+1.4f,.016f,z-2.65f),new Vector3(x+1.4f,.016f,z+2.65f));Box(stone,new Vector3(x,.045f,z+(north?-2.05f:2.05f)),new Vector3(1.6f,.09f,.16f));}
    static void Accessible(float x,float aisleX)
    {Box(blue,new Vector3(x,.009f,20.25f),new Vector3(3.4f,.008f,5.3f));foreach(float dx in new[]{-1.7f,1.7f})Line(new Vector3(x+dx,.02f,17.6f),new Vector3(x+dx,.02f,22.9f));Line(new Vector3(x-1.7f,.02f,22.9f),new Vector3(x+1.7f,.02f,22.9f));
        float left=aisleX-.75f,right=aisleX+.75f;for(float z=16.2f;z<23;z+=.52f){float a=Mathf.Max(17.6f,z),b=Mathf.Min(22.9f,z+1.5f);if(b>a)Line(new Vector3(left+(a-z),.024f,a),new Vector3(left+(b-z),.024f,b),.09f);}Line(new Vector3(left,.024f,17.6f),new Vector3(left,.024f,22.9f));Line(new Vector3(right,.024f,17.6f),new Vector3(right,.024f,22.9f));
        Vector3 c=new Vector3(x-.12f,.024f,20.5f);for(int i=0;i<25;i++){float a=(i*12+45)*Mathf.Deg2Rad,b=((i+1)*12+45)*Mathf.Deg2Rad;Line(c+new Vector3(Mathf.Cos(a)*.48f,0,Mathf.Sin(a)*.48f),c+new Vector3(Mathf.Cos(b)*.48f,0,Mathf.Sin(b)*.48f),.09f);}Line(c+new Vector3(-.2f,0,-.9f),c+new Vector3(-.2f,0,.08f));Line(c+new Vector3(-.2f,0,.08f),c+new Vector3(.45f,0,.08f));Line(c+new Vector3(.45f,0,.08f),c+new Vector3(.62f,0,.65f));Line(c+new Vector3(.62f,0,.65f),c+new Vector3(.94f,0,.65f));Piece(white,cylinder,c+new Vector3(-.2f,0,-1.1f),new Vector3(.28f,.004f,.28f),Quaternion.identity);}
    static void Arrow(float x,float z,float yaw)
    {var q=Quaternion.Euler(0,yaw,0);var c=new Vector3(x,.018f,z);Line(c+q*new Vector3(0,0,-1),c+q*new Vector3(0,0,1),.15f);Line(c+q*new Vector3(-.5f,0,.45f),c+q*new Vector3(0,0,1),.18f);Line(c+q*new Vector3(.5f,0,.45f),c+q*new Vector3(0,0,1),.18f);}
    static Mesh SaveMesh(string name,Mesh mesh)
    {string path=Art+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){old.Clear();old.indexFormat=mesh.indexFormat;old.vertices=mesh.vertices;old.normals=mesh.normals;old.uv=mesh.uv;old.triangles=mesh.triangles;old.bounds=mesh.bounds;old.UploadMeshData(false);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(mesh,path);return mesh;}
    static void Flush()
    {foreach(var pair in batches){var mesh=new Mesh{name="Parking "+pair.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();mesh=SaveMesh(mesh.name,mesh);var g=new GameObject(mesh.name);g.transform.SetParent(root,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;if(pair.Key==asphalt||pair.Key==stone||pair.Key==bark)g.AddComponent<MeshCollider>().sharedMesh=mesh;}batches.Clear();}
    static Mesh BodyMesh()
    {float[] zs={-2.1f,-1.65f,-.8f,.65f,1.65f,2.1f};float[] widths={.74f,.9f,.9f,.9f,.86f,.73f};float[] tops={.73f,.93f,.98f,.96f,.84f,.72f};var vertices=new List<Vector3>();var tris=new List<int>();for(int i=0;i<zs.Length;i++){vertices.Add(new Vector3(-widths[i],.42f,zs[i]));vertices.Add(new Vector3(widths[i],.42f,zs[i]));vertices.Add(new Vector3(widths[i],tops[i],zs[i]));vertices.Add(new Vector3(-widths[i],tops[i],zs[i]));}for(int i=0;i<zs.Length-1;i++)for(int side=0;side<4;side++){int a=i*4+side,b=i*4+(side+1)%4,c=b+4,d=a+4;tris.AddRange(new[]{a,b,c,a,c,d});}tris.AddRange(new[]{3,2,1,3,1,0,20,21,22,20,22,23});var m=new Mesh{name="Low poly parked sedan body"};m.SetVertices(vertices);m.SetTriangles(tris,0);m.RecalculateNormals();m.RecalculateBounds();return m;}
    static GameObject CarPart(Transform parent,string name,Mesh mesh,Material mat,Vector3 p,Vector3 scale,Quaternion rotation)
    {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localRotation=rotation;g.transform.localScale=scale;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;return g;}
    static Mesh QuadMesh(string name,Vector3[] vertices)
    {var m=new Mesh{name=name};m.vertices=vertices;m.triangles=new[]{0,1,2,0,2,3,2,1,0,3,2,0};m.RecalculateNormals();m.RecalculateBounds();return SaveMesh(name,m);}
    static void Car(float x,float z,float yaw,Material paint,Mesh body,Mesh windows)
    {var g=new GameObject("Parked sedan - "+paint.name);g.transform.SetParent(root,false);g.transform.SetPositionAndRotation(new Vector3(x,0,z),Quaternion.Euler(0,yaw,0));CarPart(g.transform,"Body",body,paint,Vector3.zero,Vector3.one,Quaternion.identity);CarPart(g.transform,"Cabin glazing",windows,glass,Vector3.zero,Vector3.one,Quaternion.identity);CarPart(g.transform,"Painted roof",cube,paint,new Vector3(0,1.47f,-.15f),new Vector3(1.38f,.06f,1.13f),Quaternion.identity);
        foreach(float sx in new[]{-.92f,.92f})foreach(float sz in new[]{-1.28f,1.28f}){CarPart(g.transform,"Tyre",cylinder,dark,new Vector3(sx,.33f,sz),new Vector3(.64f,.115f,.64f),Quaternion.Euler(0,0,90));CarPart(g.transform,"Alloy wheel",cylinder,metal,new Vector3(sx+Mathf.Sign(sx)*.12f,.33f,sz),new Vector3(.36f,.012f,.36f),Quaternion.Euler(0,0,90));}
        foreach(float sx in new[]{-.48f,.48f}){CarPart(g.transform,"Headlamp",cube,glow,new Vector3(sx,.69f,2.102f),new Vector3(.4f,.14f,.025f),Quaternion.identity);CarPart(g.transform,"Rear lamp",cube,red,new Vector3(sx,.70f,-2.102f),new Vector3(.36f,.14f,.025f),Quaternion.identity);}CarPart(g.transform,"Grille",cube,dark,new Vector3(0,.54f,2.11f),new Vector3(.72f,.14f,.035f),Quaternion.identity);foreach(float sx in new[]{-.84f,.84f})CarPart(g.transform,"Side mirror",cube,paint,new Vector3(sx,1.03f,.62f),new Vector3(.23f,.13f,.25f),Quaternion.identity);var col=g.AddComponent<BoxCollider>();col.center=new Vector3(0,.75f,0);col.size=new Vector3(2.13f,1.5f,4.25f);}
    [MenuItem("Hotel Together/Environment/Build front parking and arrival court")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="HotelLobby")throw new InvalidOperationException("Open HotelLobby first.");Directory.CreateDirectory(Art);string backup=Path.GetFullPath("../tmp/parking/HotelLobby-before-parking.unity");if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Build hotel front parking");var old=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==RootName);if(old)Undo.DestroyObjectImmediate(old);var group=new GameObject(RootName);root=group.transform;Undo.RegisterCreatedObjectUndo(group,"Parking court");
        cube=Primitive(PrimitiveType.Cube);sphere=Primitive(PrimitiveType.Sphere);cylinder=Primitive(PrimitiveType.Cylinder);batches.Clear();
        asphalt=Mat("Warm charcoal asphalt",new Color(.20f,.22f,.23f),.13f);stone=Mat("Light limestone curbs",new Color(.72f,.68f,.57f));white=Mat("Parking white paint",new Color(.94f,.94f,.87f));blue=Mat("Accessible blue paint",new Color(.10f,.32f,.52f));grass=Mat("Island lawn",new Color(.40f,.55f,.30f));leaf=Mat("Parking deep foliage",new Color(.25f,.44f,.25f));leafLight=Mat("Parking sunlit foliage",new Color(.53f,.65f,.34f));bark=Mat("Parking tree bark",new Color(.43f,.28f,.14f));dark=Mat("Tyres and bollards",new Color(.055f,.065f,.07f));metal=Mat("Wheel alloy",new Color(.53f,.56f,.59f),.65f,.75f);glass=Mat("Car dark blue glass",new Color(.10f,.20f,.26f),.8f,.2f);red=Mat("Rear lights",new Color(.70f,.08f,.045f),.6f);glow=Mat("Warm light lenses",new Color(.97f,.89f,.63f),.6f);glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(.5f,.4f,.20f));
        Box(asphalt,new Vector3(1,-.09f,25.5f),new Vector3(60,.18f,21));Box(asphalt,new Vector3(1,-.09f,59),new Vector3(6.5f,.18f,46));
        Box(stone,new Vector3(1,-.065f,16.3f),new Vector3(60,.16f,2.6f));
        // A flush crossing maintains the clear existing 18 m entrance apron.
        for(float x=-2.1f;x<4.2f;x+=.6f)Box(white,new Vector3(x,.021f,18.7f),new Vector3(.30f,.008f,2.1f));
        for(int i=0;i<5;i++){Bay(-26.4f+i*2.8f,20.25f,true);Bay(17.2f+i*2.8f,20.25f,true);}
        for(int i=0;i<4;i++){Bay(-25f+i*2.8f,31.75f,false);Bay(18.6f+i*2.8f,31.75f,false);}
        Accessible(-6.5f,-4.05f);Accessible(8.5f,6.05f);
        Island(-11,20.25f,4.6f,5.3f);Island(13,20.25f,4.6f,5.3f);Island(-8.5f,31.75f,11.4f,5.3f);Island(10.5f,31.75f,11.4f,5.3f);
        foreach(float x in new[]{-29f,31f}){Box(stone,new Vector3(x,.025f,25.5f),new Vector3(.22f,.13f,21));for(float z=18;z<36;z+=2)Shrub(x+Mathf.Sign(x)*.7f,z);Tree(x+Mathf.Sign(x)*1.4f,24);Tree(x+Mathf.Sign(x)*1.4f,34);}
        foreach(float x in new[]{-16f,18f}){Box(stone,new Vector3(x,.025f,35.6f),new Vector3(25.5f,.13f,.22f));for(float px=x-12;px<x+12;px+=1.3f)Shrub(px,36.3f);}
        foreach(float x in new[]{-3.3f,5.3f}){Box(stone,new Vector3(x,.025f,58.8f),new Vector3(.2f,.13f,46.3f));Tree(x+Mathf.Sign(x-1)*1.5f,43,.8f);}
        for(float x=-27;x<=29;x+=4){if(Mathf.Abs(x)<4)continue;Bollard(x,15.5f);}Arrow(-17,26,90);Arrow(19,26,270);Arrow(-.65f,43,180);Arrow(2.65f,43,0);for(float z=38;z<82;z+=3)Box(white,new Vector3(1,.018f,z),new Vector3(.10f,.008f,1.3f));
        Flush();var body=SaveMesh("Parked sedan body",BodyMesh());
        // Shared cabin mesh includes front, rear and two side windows with central pillars.
        var verts=new List<Vector3>();var tris=new List<int>();void Q(Vector3 a,Vector3 b,Vector3 c,Vector3 d){int i=verts.Count;verts.AddRange(new[]{a,b,c,d});tris.AddRange(new[]{i,i+1,i+2,i,i+2,i+3,i+2,i+1,i,i+3,i+2,i});}
        Q(new Vector3(-.68f,1.44f,.42f),new Vector3(.68f,1.44f,.42f),new Vector3(.78f,.98f,1.1f),new Vector3(-.78f,.98f,1.1f));Q(new Vector3(-.68f,1.44f,-.72f),new Vector3(.68f,1.44f,-.72f),new Vector3(.78f,.98f,-1.18f),new Vector3(-.78f,.98f,-1.18f));foreach(float side in new[]{-1f,1f}){Q(new Vector3(side*.68f,1.44f,-.68f),new Vector3(side*.68f,1.44f,-.20f),new Vector3(side*.78f,.99f,-.20f),new Vector3(side*.78f,.99f,-1.12f));Q(new Vector3(side*.68f,1.44f,-.12f),new Vector3(side*.68f,1.44f,.40f),new Vector3(side*.78f,.99f,1.05f),new Vector3(side*.78f,.99f,-.12f));}
        var windowMesh=new Mesh{name="Sedan cabin windows"};windowMesh.SetVertices(verts);RepairWindowMesh(windowMesh);windowMesh=SaveMesh("Sedan cabin windows",windowMesh);
        Car(-25,31.4f,180,Mat("Pearl parked car",new Color(.85f,.85f,.80f),.6f),body,windowMesh);Car(-22.2f,31.4f,180,Mat("Blue parked car",new Color(.10f,.33f,.54f),.6f),body,windowMesh);Car(-26.4f,20.6f,0,Mat("Graphite parked car",new Color(.25f,.29f,.3f),.6f),body,windowMesh);Car(20,20.6f,0,Mat("Terracotta parked car",new Color(.62f,.23f,.12f),.6f),body,windowMesh);Car(24.2f,31.4f,180,Mat("Silver parked car",new Color(.62f,.67f,.68f),.6f),body,windowMesh);
        // Remove the old invisible one-metre perimeter barrier at the front edge of the entrance apron.
        foreach(var barrier in UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(barrier.transform.parent && barrier.transform.parent.parent && barrier.transform.parent.parent.name=="Architecture collision" && Mathf.Abs(barrier.bounds.center.z-15)<.12f && barrier.bounds.size.x>17 && barrier.bounds.size.x<19 && barrier.bounds.size.y>.9f && barrier.bounds.size.y<1.1f) {Undo.RecordObject(barrier,"Open entrance apron to parking");barrier.enabled=false;}
        TuneCarMaterials(root);
        foreach(var atmosphere in UnityEngine.Object.FindObjectsByType<HotelCityAtmosphere>()){atmosphere.SuspendForSceneSave();atmosphere.Apply();}Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);Debug.Log("PARKING_COMPLETE bays=20 accessible=2 parkedCars=5");
    }
}
