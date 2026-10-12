using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
namespace HotelTogether.Editor
{
    public static class HotelGuestBuilder
    {
        const string Art="Assets/HotelTogether/Art/Guests/";
        const string Prefabs="Assets/HotelTogether/Prefabs/Guests/";
        static readonly List<Transform> bones=new();
        static readonly List<Vector3> vertices=new(),normals=new();
        static readonly List<Vector2> uv=new();
        static readonly List<BoneWeight> weights=new();
        static readonly List<Material> materials=new();
        static readonly List<List<int>> triangles=new();
        static Mesh sphere,cube;
        static Transform root;
        static int agentType;
        static Material skin,white,dark,hair,shirt,shorts,leather,red;
        static Material Mat(string name,Color color)
        {
            var path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.18f);m.SetFloat("_Metallic",0);EditorUtility.SetDirty(m);return m;
        }
        static Transform Bone(string name,Transform parent,Vector3 local)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=local;bones.Add(t);return t;}
        static Mesh Sphere()
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();const int rows=10,cols=16;
            for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++){float a=(float)y/rows*Mathf.PI,b=(float)x/cols*Mathf.PI*2;var p=new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b));v.Add(p*.5f);n.Add(p);u.Add(new Vector2((float)x/cols,(float)y/rows));}
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++){int a=y*(cols+1)+x,b=a+cols+1;t.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            var mesh=new Mesh{name="Guest smooth ellipsoid"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,u);mesh.SetTriangles(t,0);mesh.RecalculateBounds();return mesh;
        }
        static void Part(Transform bone,Material material,Vector3 local,Vector3 size,Quaternion rotation,bool box=false)
        {
            var mesh=box?cube:sphere;int offset=vertices.Count,index=materials.IndexOf(material);if(index<0){index=materials.Count;materials.Add(material);triangles.Add(new List<int>());}
            var matrix=root.worldToLocalMatrix*bone.localToWorldMatrix*Matrix4x4.TRS(local,rotation,size);var normal=matrix.inverse.transpose;
            var source=mesh.vertices;var sourceNormals=mesh.normals;var sourceUv=mesh.uv;
            for(int i=0;i<source.Length;i++){vertices.Add(matrix.MultiplyPoint3x4(source[i]));normals.Add(normal.MultiplyVector(sourceNormals[i]).normalized);uv.Add(sourceUv.Length>i?sourceUv[i]:Vector2.zero);weights.Add(new BoneWeight{boneIndex0=bones.IndexOf(bone),weight0=1});}
            foreach(int i in mesh.triangles)triangles[index].Add(offset+i);
        }
        static void Ball(Transform bone,Material mat,Vector3 p,Vector3 s)=>Part(bone,mat,p,s,Quaternion.identity);
        static void Box(Transform bone,Material mat,Vector3 p,Vector3 s)=>Part(bone,mat,p,s,Quaternion.identity,true);
        static Mesh SaveMesh(Mesh mesh,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!existing){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            existing.Clear();existing.indexFormat=mesh.indexFormat;existing.vertices=mesh.vertices;existing.normals=mesh.normals;existing.uv=mesh.uv;existing.boneWeights=mesh.boneWeights;existing.bindposes=mesh.bindposes;existing.subMeshCount=mesh.subMeshCount;for(int i=0;i<mesh.subMeshCount;i++)existing.SetTriangles(mesh.GetTriangles(i),i,false);existing.bounds=mesh.bounds;existing.UploadMeshData(false);EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);return existing;
        }
        static Texture2D FloralTexture()
        {
            const int size=512;var texture=new Texture2D(size,size,TextureFormat.RGBA32,true);var pixels=new Color[size*size];var rng=new System.Random(917);
            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(.035f,.32f,.42f);
            void Disc(float cx,float cy,float rx,float ry,Color color){int loX=Mathf.FloorToInt(cx-rx),hiX=Mathf.CeilToInt(cx+rx),loY=Mathf.FloorToInt(cy-ry),hiY=Mathf.CeilToInt(cy+ry);for(int y=loY;y<=hiY;y++)for(int x=loX;x<=hiX;x++){float d=Mathf.Pow((x-cx)/rx,2)+Mathf.Pow((y-cy)/ry,2);if(d<=1)pixels[((y%size+size)%size)*size+(x%size+size)%size]=color;}}
            for(int flower=0;flower<68;flower++)
            {
                float x=rng.Next(size),y=rng.Next(size),r=rng.Next(11,22);var color=flower%3==0?new Color(1,.74f,.12f):flower%3==1?new Color(.98f,.25f,.19f):new Color(1,.53f,.16f);
                Disc(x+r,y+r,r*.85f,r*.36f,new Color(.21f,.58f,.21f));
                for(int petal=0;petal<5;petal++){float a=petal*Mathf.PI*2/5;Disc(x+Mathf.Cos(a)*r*.55f,y+Mathf.Sin(a)*r*.55f,r*.49f,r*.49f,color);}Disc(x,y,r*.20f,r*.20f,new Color(1,.92f,.44f));
            }
            texture.SetPixels(pixels);texture.Apply();string path=Art+"Guest Hawaiian flowers.png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static HotelGuestBot BuildGuest(string name,Color skinTone,Color shortsColor,bool zombie=false)
        {
            bones.Clear();vertices.Clear();normals.Clear();uv.Clear();weights.Clear();materials.Clear();triangles.Clear();
            var go=new GameObject(name);root=new GameObject("Guest articulated model").transform;root.SetParent(go.transform,false);
            skin=Mat(name+" skin",skinTone);shorts=Mat(name+" shorts",shortsColor);
            var visual=root.gameObject.AddComponent<HotelGuestVisual>();var hips=Bone("Hips",root,new Vector3(0,.75f,0));visual.hips=hips;
            var torso=Bone("Torso",hips,new Vector3(0,.20f,0));var head=Bone("Head",torso,new Vector3(0,.47f,0));visual.head=head;
            Ball(torso,shirt,new Vector3(0,.01f,0),new Vector3(.49f,.49f,.31f));
            Ball(hips,shorts,new Vector3(0,zombie?-.11f:-.02f,0),zombie?new Vector3(.51f,.40f,.33f):new Vector3(.40f,.23f,.29f));Ball(hips,zombie?shirt:leather,new Vector3(0,.02f,.085f),new Vector3(.38f,.03f,.13f));Ball(torso,skin,new Vector3(0,.255f,0),new Vector3(.15f,.17f,.16f));
            for(int side=-1;side<=1;side+=2)
            {
                var leg=Bone(side<0?"Left thigh":"Right thigh",hips,new Vector3(side*.115f,-.07f,0));var shin=Bone(side<0?"Left shin":"Right shin",leg,new Vector3(0,-.29f,0));
                if(side<0){visual.leftLeg=leg;visual.leftShin=shin;}else{visual.rightLeg=leg;visual.rightShin=shin;}
                if(!zombie){Ball(leg,shorts,new Vector3(0,-.08f,0),new Vector3(.235f,.28f,.275f));Box(leg,shorts,new Vector3(side*.113f,-.055f,.03f),new Vector3(.018f,.125f,.13f));}else Ball(leg,skin,new Vector3(0,-.075f,0),new Vector3(.15f,.26f,.16f));
                Ball(leg,skin,new Vector3(0,-.20f,.003f),new Vector3(.14f,.18f,.15f));Ball(shin,skin,new Vector3(0,.006f,0),new Vector3(.145f,.14f,.15f));Ball(shin,skin,new Vector3(0,-.11f,0),new Vector3(.13f,.25f,.14f));
                Ball(shin,zombie?skin:white,new Vector3(0,-.25f,.01f),new Vector3(.15f,.14f,.16f));Ball(shin,skin,new Vector3(0,-.315f,.067f),new Vector3(.18f,.095f,.29f));
                if(!zombie){Ball(shin,leather,new Vector3(0,-.343f,.055f),new Vector3(.195f,.058f,.31f));Box(shin,leather,new Vector3(0,-.292f,.125f),new Vector3(.18f,.028f,.064f));Box(shin,leather,new Vector3(0,-.277f,.01f),new Vector3(.16f,.03f,.062f));}else for(int toe=0;toe<4;toe++)Ball(shin,skin,new Vector3((toe-1.5f)*.039f,-.333f,.19f),new Vector3(.043f,.063f,.095f));
                var arm=Bone(side<0?"Left shoulder":"Right shoulder",torso,new Vector3(side*.235f,.105f,0));var forearm=Bone(side<0?"Left elbow":"Right elbow",arm,new Vector3(side*.05f,-.23f,0));
                if(side<0){visual.leftArm=arm;visual.leftForearm=forearm;}else{visual.rightArm=arm;visual.rightForearm=forearm;}
                Ball(arm,shirt,new Vector3(side*.018f,-.07f,0),new Vector3(.195f,.22f,.23f));Ball(arm,skin,new Vector3(side*.035f,-.17f,0),new Vector3(.142f,.20f,.15f));
                Ball(forearm,skin,Vector3.zero,new Vector3(.14f,.13f,.15f));Ball(forearm,skin,new Vector3(0,-.105f,.015f),new Vector3(.13f,.22f,.14f));Ball(forearm,skin,new Vector3(0,-.231f,.015f),new Vector3(.115f,.13f,.075f));
                for(int finger=0;finger<4;finger++)Ball(forearm,skin,new Vector3((finger-1.5f)*.025f,-.296f,.016f),new Vector3(.028f,.094f,.031f));Ball(forearm,skin,new Vector3(-side*.071f,-.236f,.04f),new Vector3(.038f,.083f,.047f));
                Ball(head,skin,new Vector3(side*.266f,-.025f,-.01f),new Vector3(.095f,.16f,.08f));Ball(head,skin,new Vector3(side*.274f,-.019f,.025f),new Vector3(.052f,.095f,.033f));
                var eye=Bone(side<0?"Left eyelid":"Right eyelid",head,new Vector3(side*.118f,.054f,.217f));Ball(eye,white,Vector3.zero,new Vector3(.185f,.21f,.135f));Ball(eye,dark,new Vector3(-side*.011f,-.014f,.071f),new Vector3(.068f,.085f,.038f));Ball(eye,white,new Vector3(-side*.020f,.007f,.090f),new Vector3(.019f,.021f,.012f));
                if(side<0)visual.leftEye=eye;else visual.rightEye=eye;
                var brow=Bone(side<0?"Left brow":"Right brow",head,new Vector3(side*.118f,.177f,.21f));Ball(brow,hair,Vector3.zero,new Vector3(.139f,.031f,.037f));if(side<0)visual.leftBrow=brow;else visual.rightBrow=brow;
                Part(torso,shirt,new Vector3(side*.060f,zombie?.11f:.176f,.135f),zombie?new Vector3(.09f,.29f,.05f):new Vector3(.090f,.10f,.035f),Quaternion.Euler(0,0,side*(zombie?18:28)));
            }
            Ball(head,skin,Vector3.zero,new Vector3(.535f,.59f,.47f));Ball(head,skin,new Vector3(0,-.034f,.254f),new Vector3(.085f,.098f,.105f));
            Ball(head,hair,new Vector3(0,.238f,-.012f),new Vector3(.53f,.18f,.46f));Ball(head,hair,new Vector3(0,.08f,-.183f),new Vector3(.49f,.43f,.17f));
            for(int i=0;i<7;i++)Part(head,hair,new Vector3(-.20f+i*.064f,.25f+Mathf.Sin(i*.52f)*.028f,.10f),new Vector3(.16f,.12f,.24f),Quaternion.Euler(-12,0,-24));
            var smile=Bone("Smile",head,new Vector3(0,-.136f,.205f));visual.smile=smile;
            if(zombie)
            {
                Ball(smile,dark,new Vector3(0,-.025f,0),new Vector3(.19f,.14f,.064f));for(int i=0;i<4;i++)Box(smile,white,new Vector3(-.065f+i*.04f,.005f,.034f),new Vector3(.026f,i==1?.015f:.035f,.020f));
                var marks=Mat("Zombie mottled skin",new Color(.20f,.31f,.12f));foreach(var p in new[]{new Vector3(-.17f,.15f,.19f),new Vector3(.19f,-.09f,.18f),new Vector3(-.10f,-.13f,.20f)})Ball(head,marks,p,new Vector3(.047f,.038f,.018f));
                for(int i=0;i<7;i++)Part(hips,shirt,new Vector3(-.22f+i*.073f,-.24f-(i%3)*.015f,.09f),new Vector3(.073f,.16f,.07f),Quaternion.Euler(0,0,i%2==0?12:-9),true);
                Part(hips,shirt,new Vector3(.075f,.014f,.171f),new Vector3(.16f,.045f,.025f),Quaternion.Euler(0,0,-18));Part(hips,shirt,new Vector3(.13f,-.045f,.169f),new Vector3(.065f,.15f,.027f),Quaternion.Euler(0,0,24));
            }
            else {for(int i=0;i<11;i++){float x=(i-5)*.018f;Ball(smile,dark,new Vector3(x,Mathf.Pow(x/.1f,2)*.032f,0),new Vector3(.025f,.015f,.018f));}for(int i=0;i<4;i++)Ball(torso,white,new Vector3(0,.11f-i*.067f,.162f),new Vector3(.015f,.015f,.014f));}
            var bag=Bone("Rolling suitcase",root,new Vector3(.40f,.36f,-.13f));visual.luggage=bag.gameObject;Box(bag,red,Vector3.zero,new Vector3(.30f,.49f,.22f));for(int rib=0;rib<5;rib++)Box(bag,leather,new Vector3(-.10f+rib*.05f,0,.112f),new Vector3(.008f,.36f,.008f));
            for(int side=-1;side<=1;side+=2){Ball(bag,dark,new Vector3(side*.11f,-.27f,0),new Vector3(.063f,.063f,.06f));Box(bag,dark,new Vector3(side*.065f,.365f,0),new Vector3(.016f,.26f,.019f));}Box(bag,dark,new Vector3(0,.49f,0),new Vector3(.15f,.026f,.028f));
            // Luggage has its own renderer so it can disappear after check-in without affecting the skinned body.
            int bagIndex=bones.IndexOf(bag);var mesh=new Mesh{name=name+" body",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.boneWeights=weights.ToArray();mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*root.localToWorldMatrix).ToArray();mesh.subMeshCount=materials.Count;
            for(int sub=0;sub<materials.Count;sub++){var keep=new List<int>();foreach(int t in triangles[sub])if(weights[t].boneIndex0!=bagIndex)keep.Add(t);mesh.SetTriangles(keep,sub);}mesh.RecalculateBounds();
            string meshPath=Art+name+" body.asset";mesh=SaveMesh(mesh,meshPath);
            var renderer=root.gameObject.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.sharedMaterials=materials.ToArray();renderer.bones=bones.ToArray();renderer.rootBone=hips;renderer.updateWhenOffscreen=true;renderer.localBounds=new Bounds(new Vector3(0,.8f,0),new Vector3(1.4f,2.2f,1.5f));
            var luggageMesh=new Mesh{name=name+" luggage"};var li=new List<int>();foreach(var list in triangles)foreach(int t in list)if(weights[t].boneIndex0==bagIndex)li.Add(t);var bagVertices=vertices.Select(p=>bag.InverseTransformPoint(root.TransformPoint(p))).ToArray();luggageMesh.indexFormat=IndexFormat.UInt32;luggageMesh.vertices=bagVertices;luggageMesh.normals=normals.Select(n=>bag.InverseTransformDirection(root.TransformDirection(n))).ToArray();luggageMesh.uv=uv.ToArray();luggageMesh.subMeshCount=2;
            for(int sub=0;sub<2;sub++){var list=triangles[materials.IndexOf(sub==0?red:dark)].Where(t=>weights[t].boneIndex0==bagIndex).ToArray();luggageMesh.SetTriangles(list,sub);}luggageMesh.RecalculateBounds();string bagPath=Art+name+" suitcase.asset";luggageMesh=SaveMesh(luggageMesh,bagPath);bag.gameObject.AddComponent<MeshFilter>().sharedMesh=luggageMesh;bag.gameObject.AddComponent<MeshRenderer>().sharedMaterials=new[]{red,dark};
            var agent=go.AddComponent<NavMeshAgent>();agent.agentTypeID=agentType;agent.radius=.29f;agent.height=1.75f;agent.speed=1.8f;agent.angularSpeed=240;agent.acceleration=5;agent.stoppingDistance=.12f;agent.avoidancePriority=35;agent.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            var collider=go.AddComponent<CapsuleCollider>();collider.center=new Vector3(0,.85f,0);collider.radius=.31f;collider.height=1.7f;collider.isTrigger=true;
            var item=go.AddComponent<HotelItem>();item.kind="guest";item.displayName="Hotel Guest";item.dimensions=new Vector3(.6f,1.75f,.5f);var bot=go.AddComponent<HotelGuestBot>();bot.visual=visual;
            if(!zombie){go.AddComponent<HotelHuman>();go.AddComponent<HotelGuestSurvival>();}
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Prefabs+name+".prefab").GetComponent<HotelGuestBot>();Object.DestroyImmediate(go);return prefab;
        }
        static HotelItem BuildSupply(GuestNeed type)
        {
            var go=new GameObject(type+" service supply");var root=go.transform;void Shape(Material mat,Vector3 p,Vector3 scale,bool rounded=false){var g=GameObject.CreatePrimitive(rounded?PrimitiveType.Sphere:PrimitiveType.Cube);g.transform.SetParent(root,false);g.transform.localPosition=p;g.transform.localScale=scale;Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mat;}
            Vector3 size;
            if(type==GuestNeed.Meal){size=new Vector3(.36f,.14f,.28f);Shape(leather,Vector3.zero,new Vector3(.36f,.025f,.28f));Shape(white,new Vector3(0,.018f,0),new Vector3(.28f,.023f,.24f),true);Shape(Mat("Guest rice",new Color(.95f,.88f,.62f)),new Vector3(-.06f,.06f,0),new Vector3(.12f,.07f,.15f),true);Shape(Mat("Guest meal vegetables",new Color(.22f,.55f,.19f)),new Vector3(.08f,.065f,.04f),new Vector3(.09f,.08f,.10f),true);Shape(red,new Vector3(.05f,.045f,-.06f),new Vector3(.11f,.04f,.07f),true);}
            else if(type==GuestNeed.Water){size=new Vector3(.085f,.23f,.085f);Shape(Mat("Guest water bottle",new Color(.25f,.70f,.91f)),new Vector3(0,.06f,0),new Vector3(.085f,.19f,.085f),true);Shape(white,new Vector3(0,.156f,0),new Vector3(.042f,.024f,.042f));Shape(white,new Vector3(0,.04f,0),new Vector3(.087f,.05f,.086f));}
            else{size=new Vector3(.28f,.095f,.21f);for(int i=0;i<3;i++)Shape(white,new Vector3(0,i*.029f,0),new Vector3(.28f,.027f,.21f));}
            var collider=go.AddComponent<BoxCollider>();collider.size=size;collider.center=type==GuestNeed.Water?new Vector3(0,.065f,0):new Vector3(0,.045f,0);var rb=go.AddComponent<Rigidbody>();rb.isKinematic=true;var item=go.AddComponent<HotelItem>();item.kind="pickup";item.displayName=type==GuestNeed.Towel?"Clean guest towel":type==GuestNeed.Meal?"Guest meal":"Guest water";item.dimensions=size;go.AddComponent<HotelGuestSupply>().type=type;
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Prefabs+"Supply "+type+".prefab").GetComponent<HotelItem>();Object.DestroyImmediate(go);return prefab;
        }
        public static GameObject BuildZombieModel()
        {
            Directory.CreateDirectory(Art);Directory.CreateDirectory(Prefabs);agentType=Object.FindFirstObjectByType<HotelGuestSimulation>().guestPrefabs[0].GetComponent<NavMeshAgent>().agentTypeID;
            sphere=Sphere();var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(primitive);
            white=Mat("Zombie yellowed eyes and teeth",new Color(.88f,.90f,.65f));dark=Mat("Zombie dark mouth",new Color(.085f,.045f,.035f));hair=Mat("Zombie matted hair",new Color(.20f,.12f,.055f));shirt=Mat("Zombie torn pink bathrobe",new Color(.69f,.28f,.37f));leather=Mat("Zombie old trim",new Color(.25f,.16f,.09f));red=Mat("Zombie unused suitcase",new Color(.3f,.2f,.1f));
            var prefab=BuildGuest("Pink robe zombie",new Color(.37f,.56f,.28f),new Color(.69f,.28f,.37f),true);Object.DestroyImmediate(sphere);return prefab.gameObject;
        }
        [MenuItem("Hotel Together/Guests/Build customer simulation")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Art);Directory.CreateDirectory(Prefabs);Directory.CreateDirectory("../tmp/guests");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="HotelLobby")throw new InvalidOperationException("Open HotelLobby first.");
            if(!File.Exists("../tmp/guests/HotelLobby-before-guests.unity"))File.Copy(scene.path,"../tmp/guests/HotelLobby-before-guests.unity");
            var settings=Enumerable.Range(0,NavMesh.GetSettingsCount()).Select(NavMesh.GetSettingsByIndex).FirstOrDefault(s=>NavMesh.GetSettingsNameFromID(s.agentTypeID)=="Hotel Guest");
            if(settings.agentTypeID==0||NavMesh.GetSettingsNameFromID(settings.agentTypeID)!="Hotel Guest")settings=NavMesh.CreateSettings();
            agentType=settings.agentTypeID;
            var navigationSettings=new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings"));var entries=navigationSettings.FindProperty("m_Settings");var names=navigationSettings.FindProperty("m_SettingNames");
            for(int i=0;i<entries.arraySize;i++){var entry=entries.GetArrayElementAtIndex(i);if(entry.FindPropertyRelative("agentTypeID").intValue!=agentType)continue;entry.FindPropertyRelative("agentRadius").floatValue=.29f;entry.FindPropertyRelative("agentHeight").floatValue=1.75f;entry.FindPropertyRelative("agentSlope").floatValue=52;entry.FindPropertyRelative("agentClimb").floatValue=.30f;names.GetArrayElementAtIndex(i).stringValue="Hotel Guest";}
            navigationSettings.ApplyModifiedPropertiesWithoutUndo();
            var old=Object.FindFirstObjectByType<HotelGuestSimulation>();if(old)Object.DestroyImmediate(old.gameObject);
            var system=new GameObject("Hotel guests and service simulation");var simulation=system.AddComponent<HotelGuestSimulation>();
            var surface=system.AddComponent<NavMeshSurface>();surface.agentTypeID=agentType;surface.collectObjects=CollectObjects.Volume;surface.center=new Vector3(0,4,-9);surface.size=new Vector3(62,11,58);surface.layerMask=~((1<<8)|(1<<9));surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.075f;
            foreach(var item in Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None))
            {if(item.kind=="customer"){item.gameObject.SetActive(false);continue;}if(item.kind=="door"){var modifier=item.GetComponent<NavMeshModifier>();if(!modifier)modifier=item.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;}}
            surface.BuildNavMesh();if(!surface.navMeshData)throw new InvalidOperationException("Guest NavMesh baking failed.");
            string navPath=Art+"Hotel guest navigation.asset";if(AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath))AssetDatabase.DeleteAsset(navPath);AssetDatabase.CreateAsset(surface.navMeshData,navPath);
            sphere=Sphere();var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(primitive);
            white=Mat("Guest ivory",new Color(.97f,.95f,.88f));dark=Mat("Guest pupil and trim",new Color(.045f,.03f,.024f));hair=Mat("Guest chestnut hair",new Color(.24f,.095f,.04f));shirt=Mat("Guest Hawaiian shirt",Color.white);shirt.SetTexture("_BaseMap",FloralTexture());leather=Mat("Guest brown leather",new Color(.22f,.105f,.055f));red=Mat("Guest coral suitcase",new Color(.78f,.16f,.095f));
            simulation.guestPrefabs=new[]{BuildGuest("Tourist Milo",new Color(.91f,.62f,.40f),new Color(.48f,.33f,.17f)),BuildGuest("Tourist Noah",new Color(.66f,.38f,.22f),new Color(.21f,.31f,.40f))};Object.DestroyImmediate(sphere);
            var all=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);var rooms=new List<HotelGuestSimulation.GuestRoom>();
            foreach(var door in Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None).Where(i=>i.name.StartsWith("Guest room ")).OrderBy(i=>i.name))
            {
                int number=int.Parse(door.name.Substring(11,3));var room=all.FirstOrDefault(t=>t.name.StartsWith("Room "+number+" - "));if(!room)continue;var bed=room.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Upholstered guest bed");if(!bed)continue;
                var filter=new NavMeshQueryFilter{agentTypeID=agentType,areaMask=NavMesh.AllAreas};Vector3 stand=Vector3.zero;bool reachable=false;
                foreach(var candidate in new[]{bed.position+bed.right*1.3f,bed.position-bed.right*1.3f,bed.position+bed.forward*1.8f,bed.position-bed.forward*1.8f})
                {if(!NavMesh.SamplePosition(candidate,out var hit,1.1f,filter)||Mathf.Abs(hit.position.y-bed.position.y)>.25f)continue;var route=new NavMeshPath();if(!NavMesh.CalculatePath(simulation.receptionPoint,hit.position,filter,route)||route.status!=NavMeshPathStatus.PathComplete)continue;stand=hit.position;reachable=true;break;}
                if(!reachable)continue;rooms.Add(new HotelGuestSimulation.GuestRoom{number=number,door=door,bed=bed,standPoint=stand});
            }
            simulation.rooms=rooms.ToArray();if(rooms.Count==0)throw new InvalidOperationException("No navigable guest rooms found.");
            var seats=new List<HotelGuestSimulation.GuestSeat>();var items=Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None);
            foreach(var seat in items.Where(i=>i.kind=="seat"&&i.transform.position.y<2&&(i.transform.position.x<-14||i.name.StartsWith("Lobby_WaitingChair")||i.name=="Prop_0029"||i.name=="Prop_0030")).OrderBy(i=>i.name))
            {
                Vector3 floor=seat.transform.position;floor.y=.025f;Vector3 approach=floor-seat.transform.forward*(seat.dimensions.z*.5f+.42f);var filter=new NavMeshQueryFilter{agentTypeID=agentType,areaMask=NavMesh.AllAreas};if(!NavMesh.SamplePosition(approach,out var hit,.7f,filter))continue;
                var table=items.Where(i=>i.kind=="table"&&i.transform.position.y<2).OrderBy(i=>Vector3.Distance(i.transform.position,seat.transform.position)).FirstOrDefault();
                seats.Add(new HotelGuestSimulation.GuestSeat{dining=seat.transform.position.x<-14,position=floor,approach=hit.position,rotation=seat.transform.rotation,table=table?table.transform:null,chair=seat});
            }
            simulation.seats=seats.ToArray();simulation.player=Object.FindFirstObjectByType<HotelPlayer>();simulation.sun=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);
            var obstacle=simulation.player.GetComponent<NavMeshObstacle>();if(!obstacle)obstacle=simulation.player.gameObject.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Capsule;obstacle.center=new Vector3(0,.85f,0);obstacle.radius=.34f;obstacle.height=1.7f;obstacle.carving=false;
            simulation.stations=new HotelGuestSimulation.SupplyStation[3];var needs=new[]{GuestNeed.Meal,GuestNeed.Water,GuestNeed.Towel};
            for(int i=0;i<3;i++){var marker=new GameObject(needs[i]+" service station").transform;marker.SetParent(system.transform,false);marker.position=new Vector3(-6.93f,1.28f,i==0?1.90f:1.6f+i*.75f);simulation.stations[i]=new HotelGuestSimulation.SupplyStation{prefab=BuildSupply(needs[i]),point=marker};}
            var sign=new GameObject("Guest supply station sign",typeof(TextMesh));sign.transform.SetParent(system.transform,false);sign.transform.position=new Vector3(-6.40f,1.81f,2.5f);sign.transform.rotation=Quaternion.Euler(0,-90,0);var text=sign.GetComponent<TextMesh>();text.text="GUEST SERVICE\nMEAL  /  WATER  /  TOWEL";text.font=Resources.Load<Font>("HotelTogetherUI/LilitaOne-Regular");text.fontSize=52;text.characterSize=.016f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.97f,.95f,.88f);sign.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
            void SignPart(string name,Vector3 p,Vector3 size,Material material){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(system.transform,false);g.transform.position=p;g.transform.localScale=size;Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=material;}
            SignPart("Guest service plaque",new Vector3(-6.43f,1.81f,2.5f),new Vector3(.025f,.42f,2.0f),dark);foreach(float z in new[]{1.65f,3.35f})SignPart("Guest service plaque support",new Vector3(-6.43f,1.45f,z),new Vector3(.025f,.34f,.025f),leather);
            EditorUtility.SetDirty(simulation);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);HotelEmployeeBuilder.Save();Debug.Log("GUESTS_BUILT rooms="+rooms.Count+" seats="+seats.Count+" navAgent="+agentType);
        }
    }
}
