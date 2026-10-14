using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
namespace HotelTogether.Editor
{
    public static class HotelGameplayJobsBuilder
    {
        const string Art="Assets/HotelTogether/Art/Housekeeping/", Prefabs="Assets/HotelTogether/Prefabs/Housekeeping/";
        static Material water,paper,teal,dark;
        static Material Mat(string name,Color color,float smooth=.2f){string path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;}
        static GameObject Shape(string name,Transform parent,Vector3 p,Vector3 size,Material m,PrimitiveType type=PrimitiveType.Cube){var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
        static TextMesh Sign(string name,Transform parent,Vector3 p,string text,float yaw=180,float size=.01f){var t=new GameObject(name,typeof(TextMesh)).transform;t.SetParent(parent,false);t.localPosition=p;t.localRotation=Quaternion.Euler(0,yaw,0);var l=t.GetComponent<TextMesh>();l.font=Resources.Load<Font>("HotelTogetherUI/LilitaOne-Regular");l.text=text;l.fontSize=60;l.characterSize=size;l.anchor=TextAnchor.MiddleCenter;l.color=new Color(.95f,.9f,.75f);t.GetComponent<Renderer>().sharedMaterial=l.font.material;var depth=t.gameObject.AddComponent<HotelWorldText>();depth.shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/HotelTogether/Art/Elevators/HotelWorldText.shader");return l;}
        static Mesh PuddleMesh()
        {
            string path=Art+"Irregular floor water.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh)return mesh;
            mesh=new Mesh{name="Irregular thin floor puddle"};var v=new Vector3[34];var n=new Vector3[34];var uv=new Vector2[34];var triangles=new int[96];n[0]=Vector3.up;uv[0]=Vector2.one*.5f;
            for(int i=0;i<=32;i++){float a=i*Mathf.PI*2/32,r=.52f+.07f*Mathf.Sin(a*3)+.045f*Mathf.Cos(a*5);v[i+1]=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r*.8f);n[i+1]=Vector3.up;uv[i+1]=new Vector2(v[i+1].x+.5f,v[i+1].z+.5f);if(i<32){triangles[i*3]=0;triangles[i*3+1]=i+2;triangles[i*3+2]=i+1;}}
            mesh.vertices=v;mesh.normals=n;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static HotelFloorMess MessPrefab(bool wet)
        {
            var go=new GameObject(wet?"Moppable wet floor":"Discarded floor cup");
            if(wet){var visual=new GameObject("Reflective floor water",typeof(MeshFilter),typeof(MeshRenderer));visual.transform.SetParent(go.transform,false);visual.GetComponent<MeshFilter>().sharedMesh=PuddleMesh();visual.GetComponent<MeshRenderer>().sharedMaterial=water;visual.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
            else{var cup=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HotelTogether/Prefabs/ServiceJobs/Guest drinking cup.prefab");if(cup){var model=Object.Instantiate(cup,go.transform);model.name="Discarded existing guest cup";model.transform.localPosition=new Vector3(0,.07f,0);model.transform.localRotation=Quaternion.Euler(0,25,65);foreach(var c in model.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);foreach(var b in model.GetComponentsInChildren<Rigidbody>())Object.DestroyImmediate(b);foreach(var component in model.GetComponentsInChildren<MonoBehaviour>())Object.DestroyImmediate(component);}else Shape("Paper litter",go.transform,new Vector3(0,.025f,0),new Vector3(.18f,.05f,.12f),paper);}
            var collider=go.AddComponent<BoxCollider>();collider.size=wet?new Vector3(1.2f,.03f,1):new Vector3(.25f,.16f,.25f);collider.center=wet?Vector3.zero:new Vector3(0,.07f,0);collider.isTrigger=wet;
            if(!wet){var body=go.AddComponent<Rigidbody>();body.mass=.1f;body.isKinematic=true;body.useGravity=true;}
            var item=go.AddComponent<HotelItem>();item.kind=wet?"floorMess":"pickup";item.displayName=wet?"Wet floor / mop required":"Floor litter / take to a bin";item.dimensions=collider.size;
            var mess=go.AddComponent<HotelFloorMess>();mess.type=wet?FloorMessType.Wet:FloorMessType.Litter;mess.visuals=go.GetComponentsInChildren<Renderer>();var modifier=go.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
            string path=Prefabs+(wet?"Wet floor.prefab":"Floor litter.prefab");var prefab=PrefabUtility.SaveAsPrefabAsset(go,path).GetComponent<HotelFloorMess>();Object.DestroyImmediate(go);return prefab;
        }
        static HotelItem FoodPrefab(HotelItem source,MealDish dish)
        {
            var instance=Object.Instantiate(source.gameObject);instance.name=HotelGuestSupply.DishName(dish);var item=instance.GetComponent<HotelItem>();item.displayName=instance.name;instance.GetComponent<HotelGuestSupply>().dish=dish;
            var protein=Mat(dish==MealDish.ChickenRice?"Roasted chicken portion":"Vegetable meal carrot",dish==MealDish.ChickenRice?new Color(.64f,.35f,.14f):new Color(.88f,.46f,.13f));
            foreach(var r in instance.GetComponentsInChildren<MeshRenderer>())if(r.sharedMaterial&&new[]{"Guest coral suitcase","Roasted chicken portion","Vegetable meal carrot"}.Contains(r.sharedMaterial.name))r.sharedMaterial=protein;
            string path="Assets/HotelTogether/Prefabs/ServiceJobs/"+instance.name+".prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(instance,path).GetComponent<HotelItem>();Object.DestroyImmediate(instance);return prefab;
        }
        [MenuItem("Hotel Together/Service/Build playable housekeeping and service guidance")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="HotelLobby")throw new InvalidOperationException("Open HotelLobby.");
            Directory.CreateDirectory(Art);Directory.CreateDirectory(Prefabs);AssetDatabase.Refresh();var h=Object.FindFirstObjectByType<HotelGuestSimulation>();var jobs=h.serviceJobs;
            var old=GameObject.Find("Housekeeping gameplay");if(old)Object.DestroyImmediate(old);var root=new GameObject("Housekeeping gameplay").transform;
            var cleaning=h.GetComponent<HotelHousekeepingSystem>();if(!cleaning)cleaning=h.gameObject.AddComponent<HotelHousekeepingSystem>();cleaning.hotel=h;jobs.housekeeping=cleaning;
            water=Mat("Thin reflective floor water",new Color(.19f,.43f,.5f,.34f),.94f);water.SetFloat("_Surface",1);water.SetFloat("_SrcBlend",5);water.SetFloat("_DstBlend",10);water.SetFloat("_ZWrite",0);water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");water.renderQueue=3000;
            paper=Mat("Housekeeping paper",new Color(.94f,.9f,.76f));teal=Mat("Housekeeping teal",new Color(.04f,.23f,.24f));dark=Mat("Housekeeping waste bag",new Color(.035f,.04f,.045f),.05f);
            cleaning.wetPrefab=MessPrefab(true);cleaning.litterPrefab=MessPrefab(false);
            var items=Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None);
            var sourceMop=items.Where(i=>i.kind=="mop").OrderBy(i=>i.name=="Prop_0046"?0:1).ThenBy(i=>i.transform.position.y).First();if(!sourceMop.GetComponent<HotelCleaningTool>())sourceMop.gameObject.AddComponent<HotelCleaningTool>();
            if(!sourceMop.GetComponent<Rigidbody>()){var body=sourceMop.gameObject.AddComponent<Rigidbody>();body.useGravity=true;body.isKinematic=true;}
            var mopPrefab=PrefabUtility.SaveAsPrefabAsset(sourceMop.gameObject,Prefabs+"Original housekeeping mop.prefab");
            var carts=new List<HotelCleaningCart>();int number=0;
            foreach(var item in items.Where(i=>i.sourceModel=="HousekeepingCart"))
            {
                if(item.transform.position.y<2)item.transform.position=new Vector3(6.5f,.63f,-6.0f);
                var prior=item.GetComponent<HotelCleaningCart>();if(prior&&prior.mop){if(prior.mop.gameObject==sourceMop.gameObject)sourceMop.transform.SetParent(root,true);else Object.DestroyImmediate(prior.mop.gameObject);}foreach(var child in item.transform.Cast<Transform>().Where(t=>new[]{"Functional mop dock","Cart waste bag","Housekeeping cart instructions"}.Contains(t.name)).ToArray())Object.DestroyImmediate(child.gameObject);
                var cart=item.GetComponent<HotelCleaningCart>();if(!cart)cart=item.gameObject.AddComponent<HotelCleaningCart>();cart.system=cleaning;item.displayName="Housekeeping cart / mop, bucket and waste";
                var dock=new GameObject("Functional mop dock").transform;dock.SetParent(item.transform,false);dock.localPosition=new Vector3(.4f,.12f,.14f);cart.mopDock=dock;
                var mop=number++==0?sourceMop:Object.Instantiate(mopPrefab).GetComponent<HotelItem>();mop.displayName="Housekeeping mop";cart.mop=mop.GetComponent<HotelCleaningTool>();cart.Dock();
                cart.wasteVisual=Shape("Cart waste bag",item.transform,new Vector3(-.35f,-.05f,.13f),new Vector3(.25f,.30f,.25f),dark,PrimitiveType.Sphere);cart.wasteVisual.SetActive(false);
                var modifier=item.GetComponent<NavMeshModifier>();if(!modifier)modifier=item.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
                var obstacle=item.GetComponent<NavMeshObstacle>();if(!obstacle)obstacle=item.gameObject.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=item.dimensions;obstacle.carving=true;cart.obstacle=obstacle;
                Sign("Housekeeping cart instructions",item.transform,new Vector3(0,.30f,.28f),"HOUSEKEEPING\nG MOP / E PUSH",180,.004f);EditorUtility.SetDirty(item);EditorUtility.SetDirty(cart);carts.Add(cart);
            }
            cleaning.carts=carts.ToArray();var bins=new List<HotelWasteBin>();foreach(var item in items.Where(i=>i.sourceModel=="SortingBin")){
                if(item.transform.position.x<15)item.transform.position=new Vector3(6.5f,.47f,-8.0f);
                var previousTarget=item.transform.Find("Bin drop interaction");if(previousTarget)Object.DestroyImmediate(previousTarget.gameObject);
                var target=new GameObject("Bin drop interaction").transform;target.SetParent(item.transform,false);target.localPosition=new Vector3(0,item.dimensions.y*.5f+.13f,0);var collider=target.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(item.dimensions.x,.34f,item.dimensions.z);collider.isTrigger=true;
                var bin=item.GetComponent<HotelWasteBin>();if(!bin)bin=item.gameObject.AddComponent<HotelWasteBin>();item.displayName="Sorting bin / dispose litter";bins.Add(bin);EditorUtility.SetDirty(item);}cleaning.bins=bins.ToArray();
            cleaning.detergent=items.Where(i=>i.sourceModel=="LaundryDetergent").Select(i=>{var b=i.GetComponent<HotelDetergentBottle>();if(!b)b=i.gameObject.AddComponent<HotelDetergentBottle>();i.displayName="Laundry detergent / 5 doses";EditorUtility.SetDirty(i);return b;}).ToArray();cleaning.waterSources=Object.FindObjectsByType<HotelWaterFixture>(FindObjectsSortMode.None);
            var seeds=new[]{new Vector3(1f,.025f,1.5f),new Vector3(1.6f,.025f,-1.5f),new Vector3(24,.025f,-29.3f),new Vector3(-16,.025f,-5.6f),new Vector3(14.4f,4.525f,-22.8f),new Vector3(18,4.525f,-22.8f),new Vector3(12,9.025f,-30),new Vector3(-7,9.025f,-29)};
            var authored=new List<HotelFloorMess>();var filter=new NavMeshQueryFilter{agentTypeID=h.guestPrefabs[0].GetComponent<NavMeshAgent>().agentTypeID,areaMask=-1};
            for(int i=0;i<seeds.Length;i++){if(!HotelHousekeepingSystem.TryFloorPoint(h,seeds[i],out var safe))continue;var m=Object.Instantiate(i%2==0?cleaning.wetPrefab:cleaning.litterPrefab,safe+Vector3.up*.008f,Quaternion.identity,root);m.name=(m.type==FloorMessType.Wet?"Wet floor task ":"Floor litter task ")+i;m.system=cleaning;authored.Add(m);}cleaning.authored=authored.ToArray();
            var meal=h.stations.First(s=>s.prefab.GetComponent<HotelGuestSupply>().type==GuestNeed.Meal).prefab;var previous=h.stations.Where(s=>s.prefab.GetComponent<HotelGuestSupply>().type!=GuestNeed.Meal).ToList();
            foreach(var dish in new[]{MealDish.ChickenRice,MealDish.VegetableRice}){var marker=new GameObject(HotelGuestSupply.DishName(dish)+" station").transform;marker.SetParent(root,false);marker.position=new Vector3(-6.93f,1.28f,dish==MealDish.ChickenRice?1.90f:1.30f);previous.Insert((int)dish,new HotelGuestSimulation.SupplyStation{prefab=FoodPrefab(meal,dish),point=marker});Sign("Meal station label",root,new Vector3(-6.43f,1.53f,marker.position.z),HotelGuestSupply.DishName(dish).ToUpperInvariant(),-90,.0045f);}
            h.stations=previous.ToArray();
            Sign("Laundry workflow sign",root,new Vector3(26,1.8f,-31.7f),"LAUNDRY\nLOAD LINEN / ADD DETERGENT\nWAIT / COLLECT / RETURN TO ROOM",180,.007f);
            foreach(var washer in jobs.washers){washer.washSeconds=20;EditorUtility.SetDirty(washer);}
            var surface=h.GetComponent<NavMeshSurface>();surface.BuildNavMesh();string navPath=Art+"Housekeeping navigation.asset";if(AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath))AssetDatabase.DeleteAsset(navPath);AssetDatabase.CreateAsset(surface.navMeshData,navPath);EditorUtility.SetDirty(surface);EditorUtility.SetDirty(h);EditorUtility.SetDirty(jobs);EditorUtility.SetDirty(cleaning);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);HotelEmployeeBuilder.Save();Debug.Log("PLAYABLE_HOUSEKEEPING_BUILT carts="+carts.Count+" messes="+authored.Count+" bins="+bins.Count);
        }
    }
}
