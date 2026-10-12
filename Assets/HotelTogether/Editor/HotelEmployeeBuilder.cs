using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HotelTogether.Editor
{
    public static class HotelEmployeeBuilder
    {
        const string Art = "Assets/HotelTogether/Art/Employees/";
        const string Prefabs = "Assets/HotelTogether/Prefabs/Employees/";
        static readonly List<Transform> Bones = new();
        static Transform root, hips, spine, chest, head;
        static Transform[] leftLeg, rightLeg, leftArm, rightArm;
        static readonly List<Transform> Fingers = new();
        static readonly Dictionary<string, Material> Materials = new();

        sealed class Geometry
        {
            public readonly List<Vector3> vertices = new();
            public readonly List<Vector3> normals = new();
            public readonly List<BoneWeight> weights = new();
            public readonly List<Material> materials = new();
            public readonly List<List<int>> triangles = new();
            public void Add(Vector3[] v, Vector3[] n, int[] t, BoneWeight[] w, Material material)
            {
                int index = materials.IndexOf(material);
                if (index < 0) { index = materials.Count; materials.Add(material); triangles.Add(new List<int>()); }
                int offset = vertices.Count;
                vertices.AddRange(v); normals.AddRange(n); weights.AddRange(w);
                foreach (int i in t) triangles[index].Add(i + offset);
            }
        }

        [MenuItem("Hotel Together/Characters/Build playable employees")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before rebuilding employees.");
            Directory.CreateDirectory(Art); Directory.CreateDirectory(Prefabs);
            AssetDatabase.Refresh();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string backup = Path.GetFullPath("../tmp/employees/HotelLobby-before-employees.unity");
            if (!File.Exists(backup)) File.Copy(scene.path, backup);
            var bellhop = BuildCharacter("Bellhop", false);
            var housekeeping = BuildCharacter("Housekeeping", true);
            var player = Object.FindFirstObjectByType<HotelPlayer>();
            if (!player) throw new InvalidOperationException("HotelPlayer was not found in the active scene.");
            Undo.RecordObject(player, "Install employees");
            var selection = player.GetComponent<HotelEmployeePlayer>();
            if (!selection) selection = Undo.AddComponent<HotelEmployeePlayer>(player.gameObject);
            selection.employees = new[]
            {
                new HotelEmployeePlayer.Employee { id="bellhop", title="BELLHOP", description="Guest services", prefab=bellhop,
                    portrait=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"BellhopPortrait.png"), accent=new Color(.66f,.15f,.22f) },
                new HotelEmployeePlayer.Employee { id="housekeeping", title="HOUSEKEEPING", description="Room care", prefab=housekeeping,
                    portrait=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"HousekeepingPortrait.png"), accent=new Color(.23f,.55f,.70f) }
            };
            player.employee = selection;
            player.itemHoldDistance = .45f;
            player.view.nearClipPlane = .035f;
            player.view.transform.localPosition=new Vector3(0,1.62f,.20f);
            EditorUtility.SetDirty(selection); EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            Save();
            Debug.Log("Installed two repaired, skinned playable employees. Choose your employee in the Esc menu.");
        }

        public static void Save()
        {
            foreach (var atmosphere in Object.FindObjectsByType<HotelCityAtmosphere>(FindObjectsSortMode.None)) atmosphere.SuspendForSceneSave();
            foreach (var quality in Object.FindObjectsByType<HotelGraphicsQuality>(FindObjectsSortMode.None)) quality.SuspendForSceneSave();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        static HotelEmployeeRig BuildCharacter(string name, bool house)
        {
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HotelTogether/Models/Employees/" + name + "Source.glb");
            if (!imported) throw new InvalidOperationException("Missing imported model " + name);
            Materials.Clear(); ReadMaterials(name);
            root = new GameObject(name + " playable employee").transform;
            Bones.Clear(); Fingers.Clear();
            hips = Bone("Hips", root, new Vector3(0,.79f,0));
            spine = Bone("Spine", hips, new Vector3(0,1.04f,0));
            chest = Bone("Chest", spine, new Vector3(0,1.24f,0));
            var neck = Bone("Neck", chest, new Vector3(0,1.34f,0));
            head = Bone("Head", neck, new Vector3(0,1.57f,0));
            leftLeg = Leg("Left", -1); rightLeg = Leg("Right", 1);
            leftArm = Arm("Left", -1); rightArm = Arm("Right", 1);
            var body = new Geometry(); var face = new Geometry();
            Vector3[] tunicVertices=null;int[] tunicTriangles=null;
            float sourceUnits = house ? .48f : 1;
            int originalTriangles = 0;
            foreach (var filter in imported.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                originalTriangles += mesh.triangles.Length / 3;
                string part = filter.name;
                bool isHead = IsHead(part);
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                if(normals.Length!=vertices.Length){normals=new Vector3[vertices.Length];for(int i=0;i<normals.Length;i++)normals[i]=Vector3.up;}
                var sourceMatrix = imported.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                {
                    // glTFast reflects X; the source prototype is still Z-up, -Y-front.
                    Vector3 p = sourceMatrix.MultiplyPoint3x4(vertices[i]) / sourceUnits;
                    bool handPart=part.StartsWith("Palm")||part.StartsWith("Thumb")||part.StartsWith("Finger");
                    float handHeight=Height(1.27f-(2.064f-1.445f)*.45f)+(p.z-1.27f)*.47f;
                    p = Repair(part, p, house);
                    float horizontal=isHead?.405f:.47f;
                    vertices[i] = new Vector3(p.x * horizontal, handPart?handHeight:Height(p.z), -p.y * horizontal);
                    Vector3 n = normals.Length == vertices.Length ? sourceMatrix.MultiplyVector(normals[i]) : Vector3.up;
                    float vertical=handPart?.47f:p.z<=1.06f?.78f/1.029f:p.z<=2.27f?.56f/1.21f:.52f/1.266f;
                    if(!handPart&&(part.Contains("Sleeve")||part.Contains("Wrist")))vertical*=1.45f;
                    normals[i] = new Vector3(n.x/horizontal, n.z/vertical, -n.y/horizontal).normalized;
                }
                if(part=="Hotel_Housekeeping_Blue_Tunic"){tunicVertices=(Vector3[])vertices.Clone();tunicTriangles=mesh.triangles;}
                if(house&&tunicVertices!=null&&(part.StartsWith("Apron_")||part=="Apron_Waistband")&&vertices.Average(v=>v.z)>.10f)
                {
                    // Project cloth onto the actual curved tunic rather than an approximate ellipsoid.
                    // Preserve tube thickness by moving each cross-section with its centerline.
                    float centerZ=vertices.Average(v=>v.z);
                    for(int i=0;i<vertices.Length;i++)
                    {
                        var p=vertices[i];float front=FrontSurface(p.x,p.y,tunicVertices,tunicTriangles);
                        if(float.IsFinite(front))
                        {
                            float detail=part.Contains("Stitch")?.004f:part.Contains("Waistband")?Mathf.Clamp(p.z-centerZ,-.004f,.004f):Mathf.Sin(p.x*48)*.002f;
                            p.z=front+.013f+detail;vertices[i]=p;normals[i]=Vector3.forward;
                        }
                    }
                }
                bool fine = part.Contains("Lid") || part.Contains("lid") || part.Contains("Seam") || part.Contains("Stitch") || part.Contains("Button") || part.Contains("Badge") || part.Contains("_H") || part.Contains("Strap") || part.Contains("Band") || part.Contains("Lip");
                bool handDetail=part.StartsWith("Palm")||part.StartsWith("Thumb")||part.StartsWith("Finger");
                float grid = handDetail?.003f:fine ? .0025f : isHead ? .012f : .015f;
                Cluster(vertices, normals, mesh.triangles, grid, out var cv, out var cn, out var ct);
                var weights = new BoneWeight[cv.Length];
                for (int i = 0; i < cv.Length; i++) weights[i] = Weight(part, cv[i], isHead);
                var sourceMaterial = filter.GetComponent<Renderer>().sharedMaterial;
                Material material = Materials[sourceMaterial.name];
                (isHead ? face : body).Add(cv, cn, ct, weights, material);
            }
            // Continuous hidden hip/ankle bridges close the prototype's separated clothing pieces.
            AddEllipsoid(body, "Hip bridge", new Vector3(0,.825f,-.015f), new Vector3(.213f,.06f,.15f), Materials[(house?"Housekeeping":"Bellhop")+"_pants"], hips);
            for (int i=0;i<2;i++)
            {
                float side=i==0?-1:1;
                AddEllipsoid(body,"Ankle bridge",new Vector3(side*.116f,.205f,.006f),new Vector3(.072f,.10f,.069f),Materials[(house?"Housekeeping":"Bellhop")+"_pants"],(i==0?leftLeg:rightLeg)[2]);
            }
            var rig = root.gameObject.AddComponent<HotelEmployeeRig>();
            rig.hips=hips;rig.chest=chest;rig.head=head;rig.leftLeg=leftLeg;rig.rightLeg=rightLeg;rig.leftArm=leftArm;rig.rightArm=rightArm;rig.fingers=Fingers.ToArray();
            rig.bodyRenderer=Renderer(body,name+"Body"); rig.headRenderer=Renderer(face,name+"Head");
            rig.skinColor=Materials[(house?"Housekeeping":"Bellhop")+"_skin"].color;
            rig.uniformColor=Materials[(house?"Housekeeping":"Bellhop")+(house?"_blue":"_red")].color;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=8;
            string path=Prefabs+name+".prefab";
            var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,path);
            int finalTriangles=(body.triangles.Sum(t=>t.Count)+face.triangles.Sum(t=>t.Count))/3;
            File.WriteAllText(Path.GetFullPath("../output/employee-"+name.ToLowerInvariant()+"-mesh-report.json"),
                Newtonsoft.Json.JsonConvert.SerializeObject(new {name, originalTriangles, finalTriangles, bones=Bones.Count, vertices=body.vertices.Count+face.vertices.Count, height=1.86f, bodyMaterials=body.materials.Count, headMaterials=face.materials.Count}));
            Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<HotelEmployeeRig>();
        }

        static bool IsHead(string name)
        {
            return name=="Neck" || name.Contains("Head") || name.Contains("Face") || name.Contains("Hair") || name.Contains("hair") || name.Contains("Fringe") || name.Contains("Bob_") || name.Contains("Bun_") || name.Contains("Ear_") || name.Contains("Eye") || name.Contains("Lid") || name.Contains("Lid_") || name.Contains("Iris") || name.Contains("Pupil") || name.Contains("Nose") || name.Contains("Mouth") || name.Contains("Lip") || name.Contains("Hat") || name.Contains("Pillbox");
        }

        static float Height(float y)
        {
            // Keep the original face and stout silhouette, with longer working legs and a smaller head.
            if (y<=1.06f) return (y-.031f)/1.029f*.78f;
            if (y<=2.27f) return .78f+(y-1.06f)/1.21f*.56f;
            return 1.34f+(y-2.27f)/1.266f*.52f;
        }

        static Vector3 Repair(string name, Vector3 p, bool house)
        {
            if(name.StartsWith("Trouser"))
                p.z+=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1.04f,p.z))*.12f;
            bool hand=name.StartsWith("Palm")||name.StartsWith("Thumb")||name.StartsWith("Finger");
            bool arm=name.Contains("Sleeve")||name.Contains("Wrist");
            if(arm&&p.z<2.064f)p.z=2.064f-(2.064f-p.z)*1.45f;
            if(hand)p.z-=(2.064f-1.445f)*.45f;
            return p;
        }

        static float FrontSurface(float x,float y,Vector3[] v,int[] triangles)
        {
            float front=float.NegativeInfinity;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=v[triangles[i]];var b=v[triangles[i+1]];var c=v[triangles[i+2]];
                if(x<Mathf.Min(a.x,Mathf.Min(b.x,c.x))||x>Mathf.Max(a.x,Mathf.Max(b.x,c.x))||y<Mathf.Min(a.y,Mathf.Min(b.y,c.y))||y>Mathf.Max(a.y,Mathf.Max(b.y,c.y)))continue;
                float determinant=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(determinant)<1e-8f)continue;
                float u=((b.y-c.y)*(x-c.x)+(c.x-b.x)*(y-c.y))/determinant;
                float w=((c.y-a.y)*(x-c.x)+(a.x-c.x)*(y-c.y))/determinant;float t=1-u-w;
                if(u<-.001f||w<-.001f||t<-.001f)continue;
                front=Mathf.Max(front,u*a.z+w*b.z+t*c.z);
            }
            return front;
        }

        static Transform Bone(string name, Transform parent, Vector3 position)
        {
            var bone=new GameObject(name).transform;bone.SetParent(parent,false);bone.position=position;Bones.Add(bone);return bone;
        }
        static Transform[] Leg(string name,float side)
        {
            var hip=Bone(name+" thigh",hips,new Vector3(side*.116f,.76f,-.015f));
            var knee=Bone(name+" shin",hip,new Vector3(side*.116f,.42f,0));
            var ankle=Bone(name+" foot",knee,new Vector3(side*.116f,.13f,.045f));
            return new[]{hip,knee,ankle};
        }
        static Transform[] Arm(string name,float side)
        {
            var shoulder=Bone(name+" upper arm",chest,new Vector3(side*.225f,1.26f,-.018f));
            var elbow=Bone(name+" forearm",shoulder,new Vector3(side*.292f,1.015f,.030f));
            var wrist=Bone(name+" wrist",elbow,new Vector3(side*.303f,.78f,.072f));
            Fingers.Add(Bone(name+" thumb",wrist,new Vector3(side*.254f,.725f,.105f)));
            for(int i=0;i<3;i++)Fingers.Add(Bone(name+" finger "+i,wrist,new Vector3(side*.309f+(i-1)*.031f,.70f,.09f)));
            return new[]{shoulder,elbow,wrist};
        }
        static BoneWeight One(Transform bone)=>new BoneWeight {boneIndex0=Bones.IndexOf(bone),weight0=1};
        static BoneWeight Blend(Transform a,Transform b,float weight)=>new BoneWeight {boneIndex0=Bones.IndexOf(a),boneIndex1=Bones.IndexOf(b),weight0=1-weight,weight1=weight};
        static BoneWeight Weight(string name,Vector3 p,bool isHead)
        {
            if(isHead)return One(head);
            bool arm=name.Contains("Sleeve")||name.Contains("Wrist")||name.StartsWith("Palm")||name.StartsWith("Thumb")||name.StartsWith("Finger")||name.Contains("Shoulder");
            var chain=p.x<0?leftArm:rightArm;
            if(name.StartsWith("Thumb"))return One(Fingers[(p.x<0?0:4)]);
            if(name.StartsWith("Finger")){int f=int.Parse(name.Substring(name.LastIndexOf('_')+1));return One(Fingers[(p.x<0?1:5)+f]);}
            if(arm)
            {
                if(name.Contains("Shoulder"))return Blend(chest,chain[0],.5f);
                if(p.y>1.055f)return Blend(chest,chain[0],Mathf.InverseLerp(1.30f,1.13f,p.y));
                if(p.y>.84f)return Blend(chain[0],chain[1],Mathf.InverseLerp(1.07f,.93f,p.y));
                return Blend(chain[1],chain[2],Mathf.InverseLerp(.87f,.72f,p.y));
            }
            if(name.Contains("Shoe")||name.Contains("Sole")||name.Contains("Leather_Toe")||name.Contains("Trainer"))return One((p.x<0?leftLeg:rightLeg)[2]);
            if(name.StartsWith("Trouser")||name.Contains("Pant_Hem"))
            {
                var leg=p.x<0?leftLeg:rightLeg;
                if(p.y>.66f)return Blend(leg[0],hips,Mathf.InverseLerp(.65f,.86f,p.y));
                if(p.y>.30f)return Blend(leg[0],leg[1],Mathf.InverseLerp(.50f,.34f,p.y));
                return Blend(leg[1],leg[2],Mathf.InverseLerp(.29f,.19f,p.y));
            }
            if(p.y>1.08f)return Blend(spine,chest,Mathf.InverseLerp(1.08f,1.24f,p.y));
            return Blend(hips,spine,Mathf.InverseLerp(.83f,1.08f,p.y));
        }

        static void Cluster(Vector3[] vertices,Vector3[] normals,int[] triangles,float grid,out Vector3[] v,out Vector3[] n,out int[] t)
        {
            var map=new Dictionary<Vector3Int,int>();var sums=new List<Vector3>();var ns=new List<Vector3>();var counts=new List<int>();var remap=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i];var key=new Vector3Int(Mathf.RoundToInt(p.x/grid),Mathf.RoundToInt(p.y/grid),Mathf.RoundToInt(p.z/grid));
                if(!map.TryGetValue(key,out int index)){index=sums.Count;map.Add(key,index);sums.Add(Vector3.zero);ns.Add(Vector3.zero);counts.Add(0);}
                remap[i]=index;sums[index]+=p;ns[index]+=normals[i];counts[index]++;
            }
            v=new Vector3[sums.Count];n=new Vector3[sums.Count];for(int i=0;i<v.Length;i++){v[i]=sums[i]/counts[i];n[i]=ns[i].normalized;}
            var output=new List<int>();var seen=new HashSet<(int,int,int)>();
            for(int i=0;i<triangles.Length;i+=3){int a=remap[triangles[i]],b=remap[triangles[i+1]],c=remap[triangles[i+2]];if(a==b||a==c||b==c)continue;if(Vector3.Cross(v[b]-v[a],v[c]-v[a]).sqrMagnitude<1e-14f)continue;if(!seen.Add((a,b,c)))continue;output.Add(a);output.Add(b);output.Add(c);}
            t=output.ToArray();
        }

        static void ReadMaterials(string name)
        {
            var bytes=File.ReadAllBytes("Assets/HotelTogether/Models/Employees/"+name+"Source.glb");
            var json=JObject.Parse(Encoding.UTF8.GetString(bytes,20,BitConverter.ToInt32(bytes,12)));
            foreach(var token in json["materials"])
            {
                string key=token["name"].Value<string>();var rgba=token["pbrMetallicRoughness"]["baseColorFactor"].Values<float>().ToArray();
                var material=AssetDatabase.LoadAssetAtPath<Material>(Art+key+".mat");
                if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=key};AssetDatabase.CreateAsset(material,Art+key+".mat");}
                material.color=new Color(rgba[0],rgba[1],rgba[2],1);
                material.SetFloat("_Smoothness",key.Contains("pupil")?.3f:.18f);material.SetFloat("_Metallic",key.Contains("gold")?.18f:0);
                material.SetFloat("_Cull",0);EditorUtility.SetDirty(material);Materials[key]=material;
            }
        }
        static SkinnedMeshRenderer Renderer(Geometry geometry,string name)
        {
            string path=Art+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool existing=mesh;
            if(existing)mesh.Clear();else mesh=new Mesh{name=name};
            mesh.indexFormat=IndexFormat.UInt32;mesh.SetVertices(geometry.vertices);mesh.SetNormals(geometry.normals);mesh.boneWeights=geometry.weights.ToArray();
            mesh.bindposes=Bones.Select(b=>b.worldToLocalMatrix*root.localToWorldMatrix).ToArray();mesh.subMeshCount=geometry.materials.Count;
            for(int i=0;i<geometry.materials.Count;i++)mesh.SetTriangles(geometry.triangles[i],i);
            mesh.RecalculateBounds();
            if(existing)EditorUtility.SetDirty(mesh);else AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject(name);go.transform.SetParent(root,false);var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.sharedMaterials=geometry.materials.ToArray();renderer.bones=Bones.ToArray();renderer.rootBone=hips;renderer.quality=SkinQuality.Bone2;renderer.updateWhenOffscreen=true;
            renderer.localBounds=new Bounds(new Vector3(0,.95f,.15f),new Vector3(2.2f,2.5f,2.4f));
            return renderer;
        }
        static void AddEllipsoid(Geometry g,string name,Vector3 center,Vector3 radii,Material material,Transform bone)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);var source=go.GetComponent<MeshFilter>().sharedMesh;
            var v=source.vertices;var n=source.normals;var w=new BoneWeight[v.Length];
            for(int i=0;i<v.Length;i++){v[i]=center+Vector3.Scale(v[i]*2,radii);w[i]=One(bone);}
            g.Add(v,n,source.triangles,w,material);Object.DestroyImmediate(go);
        }
    }
}
