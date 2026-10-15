using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HotelTogether
{
    // An inward-visible, unlit city sphere. Generated geometry has no collisions or shadows.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class HotelCityAtmosphere : MonoBehaviour
    {
        public Texture2D cityPanorama;
        public Texture2D cityPanoramaNight;
        public Shader domeShader;
        public Light sunlight;
        public Material windowGlass;
        public bool landscapedGround = true;
        [Min(150)] public float radius = 350;
        [Range(0,360)] public float skylineRotation = 115;
        [Range(-.2f,.2f)] public float horizonOffset = -.085f;
        [Range(.1f,2)] public float backdropBrightness = .95f;
        [Range(0,4)] public float sunlightIntensity = 1.75f;
        public Vector3 sunlightAngles = new Vector3(48,215,0);
        public Vector3 cityCentre = new Vector3(1,0,-13);
        public bool replacePlaceholderBuildings = true;

        Mesh domeMesh;
        Material domeMaterial;
        GameObject dome, garden, placedTrees;
        readonly List<Mesh> gardenMeshes=new List<Mesh>();
        readonly List<Material> gardenMaterials=new List<Material>();
        bool captured, refreshRequested;
        float builtRadius, builtHorizon, nextCameraCheck;
        Light previousSun;
        Color previousSunColor, previousSky, previousEquator, previousGround;
        float previousIntensity, previousFogStart, previousFogEnd;
        bool previousTemperature;
        Quaternion previousRotation;
        LightShadows previousShadows;
        AmbientMode previousAmbient;
        readonly Dictionary<GameObject,bool> hiddenBuildings = new Dictionary<GameObject,bool>();
        readonly Dictionary<Renderer,Material[]> originalGlass = new Dictionary<Renderer,Material[]>();
        readonly Dictionary<Renderer,bool> hiddenTreeRenderers = new Dictionary<Renderer,bool>();
        readonly Dictionary<Camera,float> cameraDistances = new Dictionary<Camera,float>();

        // While a scene is still loading (e.g. the Editor restoring HotelLobby after Play mode) its roots are not
        // readable yet; defer the build to the first Update instead of throwing.
        void OnEnable() { if (gameObject.scene.isLoaded) Apply(); else refreshRequested=true; }
        void OnValidate() { refreshRequested=true; }
        void Update()
        {
            if(!dome || refreshRequested) { refreshRequested=false;Apply(); }
            if(Time.realtimeSinceStartup>=nextCameraCheck) {nextCameraCheck=Time.realtimeSinceStartup+.5f;ExtendCameraClips();}
        }

        public void Apply()
        {
            if (!cityPanorama) cityPanorama=Resources.Load<Texture2D>("HotelTogetherEnvironment/CityPanorama");
            if (!cityPanoramaNight) cityPanoramaNight=Resources.Load<Texture2D>("HotelTogetherEnvironment/CityPanoramaNight");
            if (!domeShader) domeShader=Resources.Load<Shader>("HotelTogetherEnvironment/HotelCityDome");
            if (!cityPanorama || !domeShader) return;
            if (!sunlight) sunlight=RenderSettings.sun;
            if (!sunlight) foreach (var light in FindObjectsByType<Light>())
                if(light.type==LightType.Directional && light.enabled && light.gameObject.scene==gameObject.scene) { sunlight=light;break; }
            if (!captured)
            {
                previousSun=RenderSettings.sun;
                previousAmbient=RenderSettings.ambientMode;
                previousSky=RenderSettings.ambientSkyColor;previousEquator=RenderSettings.ambientEquatorColor;previousGround=RenderSettings.ambientGroundColor;
                previousFogStart=RenderSettings.fogStartDistance;previousFogEnd=RenderSettings.fogEndDistance;
                if(sunlight) {previousSunColor=sunlight.color;previousIntensity=sunlight.intensity;previousTemperature=sunlight.useColorTemperature;previousRotation=sunlight.transform.rotation;previousShadows=sunlight.shadows;}
                captured=true;
            }
            if(!dome)
            {
                dome=new GameObject("City panorama skydome — 700m wide");
                dome.hideFlags=HideFlags.DontSave;
                dome.transform.SetParent(transform,false);
                dome.AddComponent<MeshFilter>().sharedMesh=domeMesh=BuildSphere();
                builtRadius=radius;builtHorizon=horizonOffset;
                domeMaterial=new Material(domeShader){name="Illustrated city sky",hideFlags=HideFlags.HideAndDontSave};
                domeMaterial.SetTexture("_MainTex",cityPanorama);
                domeMaterial.SetTexture("_NightTex",cityPanoramaNight ? cityPanoramaNight : cityPanorama);
                var renderer=dome.AddComponent<MeshRenderer>();renderer.sharedMaterial=domeMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            if(!Mathf.Approximately(builtRadius,radius) || !Mathf.Approximately(builtHorizon,horizonOffset))
            { Release(domeMesh);dome.GetComponent<MeshFilter>().sharedMesh=domeMesh=BuildSphere();builtRadius=radius;builtHorizon=horizonOffset; }
            dome.transform.position=cityCentre;dome.transform.rotation=Quaternion.Euler(0,skylineRotation,0);
            domeMaterial.SetTexture("_MainTex",cityPanorama);
            domeMaterial.SetTexture("_NightTex",cityPanoramaNight ? cityPanoramaNight : cityPanorama);
            domeMaterial.SetFloat("_Exposure",backdropBrightness);
            if(sunlight)
            {
                sunlight.color=Color.white;sunlight.useColorTemperature=false;sunlight.intensity=sunlightIntensity;
                sunlight.transform.rotation=Quaternion.Euler(sunlightAngles);sunlight.shadows=LightShadows.Soft;
                RenderSettings.sun=sunlight;
            }
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.59f,.69f,.81f);
            RenderSettings.ambientEquatorColor=new Color(.43f,.46f,.49f);
            RenderSettings.ambientGroundColor=new Color(.40f,.36f,.31f);
            RenderSettings.fogStartDistance=70;RenderSettings.fogEndDistance=280;
            if(replacePlaceholderBuildings)
                foreach(var renderer in FindObjectsByType<Renderer>())
                {
                    // Only the nine grey exterior mock-up towers, never hotel walls/furniture.
                    if(renderer.gameObject.scene!=gameObject.scene || !renderer.transform.parent || renderer.transform.parent.name!="Hotel architecture and furnishings") continue;
                    if(renderer.bounds.center.z<24 || renderer.bounds.size.x<3.9f || renderer.bounds.size.x>4.1f || renderer.bounds.size.z<3.9f || renderer.bounds.size.z>4.1f) continue;
                    if(!hiddenBuildings.ContainsKey(renderer.gameObject)) hiddenBuildings.Add(renderer.gameObject,renderer.gameObject.activeSelf);
                    renderer.gameObject.SetActive(false);
                }
            ClearExteriorGlazing();
            if(landscapedGround && !garden) BuildGarden();
            if(!placedTrees) BuildPlacedTrees();
            ExtendCameraClips();
        }

        void BuildPlacedTrees()
        {
            var prefab=Resources.Load<GameObject>("HotelTogetherEnvironment/CartoonTree");
            if(!prefab)return;
            placedTrees=new GameObject("Hotel cartoon tree placements");
            placedTrees.hideFlags=HideFlags.DontSave;
            placedTrees.transform.SetParent(transform,false);

            var parking=GameObject.Find("Hotel front parking and arrival court");
            if(parking && !HasTree(parking.transform,"Cartoon tree"))
            {
                HideTreePart(parking.transform.Find("Parking Parking tree bark"));
                HideTreePart(parking.transform.Find("Parking Parking sunlit foliage"));
                AddPlacedTree(prefab,"Cartoon parking tree",new Vector3(-11,0,20.25f),.93f,true);
                AddPlacedTree(prefab,"Cartoon parking tree",new Vector3(13,0,20.25f),.93f,true);
                AddPlacedTree(prefab,"Cartoon parking tree",new Vector3(-8.5f,0,31.75f),.93f,true);
                AddPlacedTree(prefab,"Cartoon parking tree",new Vector3(10.5f,0,31.75f),.93f,true);
                foreach(float x in new[]{-30.4f,32.4f})foreach(float z in new[]{24f,34f})
                    AddPlacedTree(prefab,"Cartoon parking tree",new Vector3(x,0,z),.93f,true);
                AddPlacedTree(prefab,"Cartoon parking tree",new Vector3(-4.8f,0,43),.744f,true);
                AddPlacedTree(prefab,"Cartoon parking tree",new Vector3(6.8f,0,43),.744f,true);
            }

            var rooftop=GameObject.Find("Hotel furnished rooftop");
            if(rooftop && !HasTree(rooftop.transform,"Cartoon planter tree"))
            {
                foreach(var renderer in rooftop.GetComponentsInChildren<MeshRenderer>(true))
                    if(renderer.name=="Rooftop light foliage")HideTreePart(renderer.transform);
                foreach(var position in new[]{
                    new Vector3(16.8f,9.615f,-29.6f),new Vector3(25.5f,9.615f,-29.6f),
                    new Vector3(2.6f,9.615f,-26),new Vector3(11.7f,9.615f,-26),
                    new Vector3(27.2f,9.615f,-19),new Vector3(15,9.615f,5.9f),
                    new Vector3(-25.8f,9.615f,5.4f),new Vector3(-25.8f,9.615f,-21),
                    new Vector3(-7.5f,9.615f,-14),new Vector3(-.5f,9.615f,-14)})
                    AddPlacedTree(prefab,"Cartoon planter tree",position,0,false);
            }
        }

        static bool HasTree(Transform root,string name)
        {
            foreach(Transform child in root)if(child.name==name)return true;
            foreach(var child in root.GetComponentsInChildren<Transform>(true))if(child.name==name)return true;
            return false;
        }

        void HideTreePart(Transform part)
        {
            if(!part)return;
            var renderer=part.GetComponent<MeshRenderer>();
            if(!renderer || hiddenTreeRenderers.ContainsKey(renderer))return;
            hiddenTreeRenderers.Add(renderer,renderer.enabled);
            renderer.enabled=false;
        }

        void AddPlacedTree(GameObject prefab,string name,Vector3 position,float uniformScale,bool trunkCollider)
        {
            var tree=Instantiate(prefab,position,Quaternion.Euler(0,Mathf.Repeat(position.x*67+position.z*31,360),0),placedTrees.transform);
            tree.name=name;
            tree.transform.localScale=uniformScale>0?Vector3.one*uniformScale:new Vector3(.35f,.45f,.35f);
            tree.hideFlags=HideFlags.DontSave;
            if(trunkCollider)
            {
                var collider=tree.AddComponent<CapsuleCollider>();
                collider.center=new Vector3(0,1.3f,0);collider.height=2.6f;collider.radius=.28f;
            }
        }

        void ClearExteriorGlazing()
        {
            if(!windowGlass) windowGlass=Resources.Load<Material>("HotelTogetherEnvironment/CityWindowGlass");
            if(!windowGlass) return;
            foreach(var renderer in FindObjectsByType<Renderer>())
            {
                if(renderer.gameObject.scene!=gameObject.scene || originalGlass.ContainsKey(renderer)) continue;
                bool facade=renderer.transform.parent && renderer.transform.parent.name=="Hotel architecture and furnishings" && Mathf.Abs(renderer.bounds.center.z-7)<.08f && renderer.bounds.size.z<.1f;
                bool window=renderer.name=="Daylight glazing" || renderer.name=="Skylight - not a walking floor";
                if(!facade && !window) continue;
                var materials=renderer.sharedMaterials;
                // Persistent material assets survive saving, domain reload and entering Play mode.
                var originals=(Material[])materials.Clone();bool changed=false;
                for(int i=0;i<materials.Length;i++)
                {
                    if(materials[i] && materials[i].renderQueue<3000) continue;
                    if(!originals[i]) originals[i]=windowGlass;
                    materials[i]=windowGlass;changed=true;
                }
                if(changed) {originalGlass.Add(renderer,originals);renderer.sharedMaterials=materials;}
            }
        }

        void BuildGarden()
        {
            garden=new GameObject("Landscaped hotel grounds");garden.hideFlags=HideFlags.DontSave;garden.transform.SetParent(transform,false);
            var treePrefab=Resources.Load<GameObject>("HotelTogetherEnvironment/CartoonTree");
            var sphere=GardenSphere();var temp=GameObject.CreatePrimitive(PrimitiveType.Cube);var cube=temp.GetComponent<MeshFilter>().sharedMesh;DestroyImmediate(temp);
            var pieces=new List<CombineInstance>[6];for(int i=0;i<pieces.Length;i++)pieces[i]=new List<CombineInstance>();
            bool parkingCourt=false;foreach(var sceneRoot in gameObject.scene.GetRootGameObjects()) if(sceneRoot.name=="Hotel front parking and arrival court") {parkingCourt=true;break;}
            var colors=new[]{new Color(.40f,.55f,.30f),new Color(.43f,.28f,.14f),new Color(.25f,.44f,.25f),new Color(.39f,.57f,.29f),new Color(.53f,.65f,.34f),new Color(.72f,.67f,.53f)};
            var shader=windowGlass?windowGlass.shader:Shader.Find("Universal Render Pipeline/Lit");
            AddGardenPiece(pieces[0],cube,new Vector3(1,-.25f,-13),new Vector3(660,.22f,660));
            if(!parkingCourt) AddGardenPiece(pieces[5],cube,new Vector3(1,-.10f,25),new Vector3(6,.08f,34));
            var random=new System.Random(1847);
            for(int i=0;i<68;i++)
            {
                float angle=i*Mathf.PI*2/68;
                var position=new Vector3(1+Mathf.Cos(angle)*(69+(float)random.NextDouble()*8),0,-13+Mathf.Sin(angle)*(70+(float)random.NextDouble()*8));
                if(parkingCourt && Mathf.Abs(position.x-1)<8 && position.z>30)continue;
                GardenTree(treePrefab,position,1+(float)random.NextDouble()*.45f,i);
            }
            for(int i=-6;i<=6;i++)
            {
                if(i==0 || (parkingCourt && Mathf.Abs(1+i*10)<35))continue;
                GardenTree(treePrefab,new Vector3(1+i*10,0,29+Mathf.Abs(i)*1.4f),.90f+(float)random.NextDouble()*.22f,i+12);
            }
            for(int i=-25;i<=25;i++)
            {
                if(Mathf.Abs(i)<2 || (parkingCourt && Mathf.Abs(1+i*2.8f)<35))continue;
                AddGardenPiece(pieces[2+(i+25)%3],sphere,new Vector3(1+i*2.8f,.60f,18+Mathf.Abs(i)*.10f),new Vector3(1.9f,1.2f,1.65f));
            }
            for(int i=0;i<pieces.Length;i++)
            {
                if(pieces[i].Count==0)continue;
                var mesh=new Mesh{name="Hotel park geometry",hideFlags=HideFlags.HideAndDontSave,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pieces[i].ToArray(),true,true);gardenMeshes.Add(mesh);
                var material=new Material(shader){name="Hotel garden colour",hideFlags=HideFlags.HideAndDontSave};material.SetColor("_BaseColor",colors[i]);material.SetFloat("_Smoothness",.12f);gardenMaterials.Add(material);
                var part=new GameObject(new[]{"Meadow lawn","Tree trunks","Deep green foliage","Soft green foliage","Sunlit foliage","Garden entrance path"}[i]);part.hideFlags=HideFlags.DontSave;part.transform.SetParent(garden.transform,false);
                part.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            }
            Release(sphere);
        }
        static void AddGardenPiece(List<CombineInstance> list,Mesh mesh,Vector3 position,Vector3 scale)
        {list.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(position,Quaternion.identity,scale)});}
        void GardenTree(GameObject prefab,Vector3 position,float size,int variation)
        {
            if(!prefab)return;
            var tree=Instantiate(prefab,position,Quaternion.Euler(0,Mathf.Repeat(variation*137.5f,360),0),garden.transform);
            tree.name="Cartoon garden tree";
            tree.transform.localScale=Vector3.one*size;
            tree.hideFlags=HideFlags.DontSave;
        }
        static Mesh GardenSphere()
        {
            const int columns=12,rows=8;var vertices=new Vector3[(columns+1)*(rows+1)];var triangles=new int[columns*rows*6];
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {float latitude=((float)y/rows-.5f)*Mathf.PI,longitude=(float)x/columns*Mathf.PI*2;vertices[y*(columns+1)+x]=new Vector3(Mathf.Sin(longitude)*Mathf.Cos(latitude),Mathf.Sin(latitude),Mathf.Cos(longitude)*Mathf.Cos(latitude))*.5f;}
            int t=0;for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {int i=y*(columns+1)+x;triangles[t++]=i;triangles[t++]=i+1;triangles[t++]=i+columns+1;triangles[t++]=i+1;triangles[t++]=i+columns+2;triangles[t++]=i+columns+1;}
            var mesh=new Mesh{name="Rounded low poly tree canopy",hideFlags=HideFlags.HideAndDontSave};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        Mesh BuildSphere()
        {
            const int columns=128,rows=64;
            var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];var triangles=new int[columns*rows*6];
            for(int y=0;y<=rows;y++)
            {
                float v=(float)y/rows, latitude=(v-.5f)*Mathf.PI;
                for(int x=0;x<=columns;x++)
                {
                    float u=(float)x/columns,longitude=u*Mathf.PI*2;int i=y*(columns+1)+x;
                    vertices[i]=new Vector3(Mathf.Sin(longitude)*Mathf.Cos(latitude),Mathf.Sin(latitude),Mathf.Cos(longitude)*Mathf.Cos(latitude))*radius;
                    uv[i]=new Vector2(u,v+horizonOffset);
                }
            }
            int t=0;for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {int i=y*(columns+1)+x;triangles[t++]=i;triangles[t++]=i+columns+1;triangles[t++]=i+1;triangles[t++]=i+1;triangles[t++]=i+columns+1;triangles[t++]=i+columns+2;}
            var mesh=new Mesh{name="City atmosphere sphere",hideFlags=HideFlags.HideAndDontSave};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
        void ExtendCameraClips()
        {
            if(!dome) return;
            foreach(var camera in Camera.allCameras)
            {
                if(camera.cameraType==CameraType.Reflection || camera.cameraType==CameraType.Preview) continue;
                if(!cameraDistances.ContainsKey(camera)) cameraDistances.Add(camera,camera.farClipPlane);
                camera.farClipPlane=Mathf.Max(camera.farClipPlane,radius*2.5f);
            }
        }
        void OnDisable() { RestoreEnvironment(); }
        public void SuspendForSceneSave() { RestoreEnvironment(); }
        void RestoreEnvironment()
        {
            if(captured)
            {
                RenderSettings.sun=previousSun;RenderSettings.ambientMode=previousAmbient;
                RenderSettings.ambientSkyColor=previousSky;RenderSettings.ambientEquatorColor=previousEquator;RenderSettings.ambientGroundColor=previousGround;
                RenderSettings.fogStartDistance=previousFogStart;RenderSettings.fogEndDistance=previousFogEnd;
                if(sunlight) {sunlight.color=previousSunColor;sunlight.intensity=previousIntensity;sunlight.useColorTemperature=previousTemperature;sunlight.transform.rotation=previousRotation;sunlight.shadows=previousShadows;}
            }
            foreach(var entry in hiddenBuildings) if(entry.Key) entry.Key.SetActive(entry.Value);
            foreach(var entry in cameraDistances) if(entry.Key) entry.Key.farClipPlane=entry.Value;
            foreach(var entry in originalGlass) if(entry.Key) entry.Key.sharedMaterials=entry.Value;
            foreach(var entry in hiddenTreeRenderers)if(entry.Key)entry.Key.enabled=entry.Value;
            hiddenTreeRenderers.Clear();
            originalGlass.Clear();
            Release(placedTrees);placedTrees=null;
            Release(garden);garden=null;
            foreach(var mesh in gardenMeshes) Release(mesh);gardenMeshes.Clear();
            foreach(var material in gardenMaterials) Release(material);gardenMaterials.Clear();
            hiddenBuildings.Clear();cameraDistances.Clear();captured=false;
            Release(dome);Release(domeMesh);Release(domeMaterial);dome=null;domeMesh=null;domeMaterial=null;
        }
        static void Release(UnityEngine.Object value) { if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value); }
    }
}
