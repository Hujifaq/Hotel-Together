using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEditor;
using Object=UnityEngine.Object;
namespace HotelTogether.Editor
{
    public static class HotelServiceJobsBuilder
    {
        const string Art="Assets/HotelTogether/Art/ServiceJobs/",Prefabs="Assets/HotelTogether/Prefabs/ServiceJobs/";
        static Material wood,dark,metal,coral,dirty,clean,teal;
        static Material Mat(string name,Color color){var path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",.24f);EditorUtility.SetDirty(m);return m;}
        static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material mat,bool collider=false)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
        static Transform Point(string name,Transform parent,Vector3 p){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
        static TextMesh Sign(string name,Transform parent,Vector3 p,Vector3 facing,string text,float size=.012f)
        {var go=new GameObject(name,typeof(TextMesh));go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localRotation=Quaternion.LookRotation(-facing,Vector3.up);var label=go.GetComponent<TextMesh>();label.text=text;label.font=Resources.Load<Font>("HotelTogetherUI/LilitaOne-Regular");label.fontSize=50;label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;label.color=new Color(.96f,.92f,.8f);go.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;return label;}
        static Mesh SuitcaseMesh()
        {
            // The original luggage mesh also stores unused body vertices. Trim those
            // so pickup bounds, hand placement and camera collision describe only the bag.
            var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/HotelTogether/Art/Guests/Tourist Milo suitcase.asset");var indices=Enumerable.Range(0,source.subMeshCount).SelectMany(i=>source.GetTriangles(i)).Distinct().OrderBy(i=>i).ToArray();var map=indices.Select((old,index)=>new{old,index}).ToDictionary(v=>v.old,v=>v.index);
            var path=Art+"Tagged suitcase mesh.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!mesh){mesh=new Mesh{name="Tagged guest suitcase"};AssetDatabase.CreateAsset(mesh,path);}mesh.Clear();var v=source.vertices;var n=source.normals;var uv=source.uv;mesh.vertices=indices.Select(i=>v[i]).ToArray();mesh.normals=indices.Select(i=>n[i]).ToArray();mesh.uv=indices.Select(i=>uv[i]).ToArray();mesh.subMeshCount=source.subMeshCount;for(int i=0;i<source.subMeshCount;i++)mesh.SetTriangles(source.GetTriangles(i).Select(t=>map[t]).ToArray(),i);mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
        }
        static HotelGuestDelivery DeliveryPrefab(bool linen)
        {
            var go=new GameObject(linen?"Tagged guest linen":"Tagged guest suitcase");
            Renderer cloth=null;
            if(linen){for(int i=0;i<3;i++)cloth=Box("Linen cloth",go.transform,new Vector3(0,-.055f+i*.055f,0),new Vector3(.38f,.05f,.29f),dirty).GetComponent<Renderer>();}
            else
            {
                // Reuse the existing guest suitcase geometry and palette.
                var shape=new GameObject("Guest coral suitcase",typeof(MeshFilter),typeof(MeshRenderer));shape.transform.SetParent(go.transform,false);shape.GetComponent<MeshFilter>().sharedMesh=SuitcaseMesh();
                var guest=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HotelTogether/Prefabs/Guests/Tourist Milo.prefab");shape.GetComponent<MeshRenderer>().sharedMaterials=guest.GetComponentInChildren<HotelGuestVisual>().luggage.GetComponent<MeshRenderer>().sharedMaterials;
                Box("Guest tag plate",go.transform,new Vector3(0,.09f,.12f),new Vector3(.23f,.12f,.012f),dark);
            }
            Sign("Guest ownership tag",go.transform,new Vector3(0,linen?.11f:.09f,linen?.154f:.131f),Vector3.forward,"GUEST\nROOM --",.0035f);
            if(!linen){Box("Reverse guest tag plate",go.transform,new Vector3(0,.09f,-.12f),new Vector3(.23f,.12f,.012f),dark);Sign("Reverse ownership tag",go.transform,new Vector3(0,.09f,-.131f),Vector3.back,"GUEST\nROOM --",.0035f);}
            var collider=go.AddComponent<BoxCollider>();collider.size=linen?new Vector3(.40f,.18f,.31f):new Vector3(.32f,.78f,.25f);collider.center=linen?Vector3.zero:new Vector3(0,.10f,0);
            var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=true;body.mass=linen?.45f:3;
            var item=go.AddComponent<HotelItem>();item.kind="pickup";item.dimensions=collider.size;item.displayName=linen?"Tagged dirty linen":"Tagged guest suitcase";
            var delivery=go.AddComponent<HotelGuestDelivery>();delivery.type=linen?GuestDeliveryType.DirtyLaundry:GuestDeliveryType.Luggage;delivery.linenRenderer=cloth;
            var path=Prefabs+(linen?"Tagged linen.prefab":"Tagged suitcase.prefab");var prefab=PrefabUtility.SaveAsPrefabAsset(go,path).GetComponent<HotelGuestDelivery>();Object.DestroyImmediate(go);return prefab;
        }
        static HotelServiceTray TrayPrefab()
        {
            var go=new GameObject("Three place service tray");Box("Warm wood tray",go.transform,Vector3.zero,new Vector3(.90f,.035f,.39f),wood);
            foreach(float side in new[]{-1f,1f}){Box("Raised tray edge",go.transform,new Vector3(side*.445f,.027f,0),new Vector3(.018f,.06f,.39f),dark);Box("Tray handle",go.transform,new Vector3(side*.495f,.016f,0),new Vector3(.08f,.028f,.14f),metal);Box("Tray edge",go.transform,new Vector3(0,.027f,side*.188f),new Vector3(.90f,.06f,.015f),dark);}
            var c=go.AddComponent<BoxCollider>();c.size=new Vector3(1.07f,.06f,.39f);var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=true;body.mass=.7f;var item=go.AddComponent<HotelItem>();item.kind="pickup";item.displayName="Service tray / 0 of 3";item.dimensions=c.size;
            var tray=go.AddComponent<HotelServiceTray>();tray.slots=Enumerable.Range(0,3).Select(i=>Point("Meal slot "+i,go.transform,new Vector3((i-1)*.29f,.043f,0))).ToArray();
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Prefabs+"Service tray.prefab").GetComponent<HotelServiceTray>();Object.DestroyImmediate(go);return prefab;
        }
        [MenuItem("Hotel Together/Service/Build player jobs and luggage trolleys")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="HotelLobby")throw new InvalidOperationException("Open HotelLobby.");
            Directory.CreateDirectory(Art);Directory.CreateDirectory(Prefabs);Directory.CreateDirectory("../tmp/service-jobs");if(!File.Exists("../tmp/service-jobs/HotelLobby-before-jobs.unity"))File.Copy(scene.path,"../tmp/service-jobs/HotelLobby-before-jobs.unity");
            wood=Mat("Service tray warm walnut",new Color(.43f,.22f,.10f));dark=Mat("Service charcoal trim",new Color(.055f,.09f,.10f));metal=Mat("Service brass",new Color(.68f,.48f,.19f));coral=Mat("Service coral",new Color(.76f,.23f,.12f));dirty=Mat("Used guest linen",new Color(.49f,.45f,.36f));clean=Mat("Clean guest linen",new Color(.96f,.95f,.88f));teal=Mat("Service teal",new Color(.12f,.33f,.34f));
            var hotel=Object.FindFirstObjectByType<HotelGuestSimulation>();var jobs=hotel.GetComponent<HotelServiceJobs>();if(!jobs)jobs=hotel.gameObject.AddComponent<HotelServiceJobs>();jobs.hotel=hotel;hotel.serviceJobs=jobs;hotel.maxGuests=12;hotel.arrivalInterval=14;
            var old=hotel.transform.Find("Player service job equipment");if(old)Object.DestroyImmediate(old.gameObject);var root=new GameObject("Player service job equipment").transform;root.SetParent(hotel.transform,false);jobs.cleanLinen=clean;jobs.luggagePrefab=DeliveryPrefab(false);jobs.linenPrefab=DeliveryPrefab(true);
            var luggage=Point("Reception tagged luggage area",root,new Vector3(-4,.335f,-5.6f));jobs.luggageDrop=luggage;
            Box("Luggage sign post",root,new Vector3(-4.4f,.55f,-5.64f),new Vector3(.025f,1.1f,.025f),metal);Box("Luggage sorting plaque",root,new Vector3(-4.4f,1.1f,-5.64f),new Vector3(.9f,.30f,.025f),dark);Box("Luggage staging mat",root,new Vector3(-3.35f,.03f,-5.04f),new Vector3(1.9f,.01f,2),teal);Sign("Luggage sorting sign",root,new Vector3(-4.4f,1.1f,-5.6f),Vector3.forward,"TAGGED LUGGAGE\nLOAD TROLLEY / G",.009f);
            var trolleys=new List<HotelLuggageTrolley>();
            foreach(var item in Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None).Where(i=>i.sourceModel=="Gold-Trolley"||i.name.StartsWith("Gold-Trolley")))
            {
                var trolley=item.GetComponent<HotelLuggageTrolley>();if(!trolley){trolley=item.gameObject.AddComponent<HotelLuggageTrolley>();var geometry=new GameObject("Enlarged luggage trolley model").transform;geometry.SetParent(item.transform,false);foreach(var child in item.transform.Cast<Transform>().Where(t=>t!=geometry).ToArray())child.SetParent(geometry,true);geometry.localScale=new Vector3(1.2f,1.05f,1.2f);item.dimensions=Vector3.Scale(item.dimensions,new Vector3(1.2f,1.05f,1.2f));item.transform.position+=Vector3.up*(item.dimensions.y/2-.93f);}
                if(item.transform.position.y<3)item.transform.position=new Vector3(-2.6f,item.dimensions.y/2+.025f,-1.6f);
                var modifier=item.GetComponent<NavMeshModifier>();if(!modifier)modifier=item.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
                trolley.obstacle=item.GetComponent<NavMeshObstacle>();if(!trolley.obstacle)trolley.obstacle=item.gameObject.AddComponent<NavMeshObstacle>();trolley.obstacle.shape=NavMeshObstacleShape.Box;trolley.obstacle.size=item.dimensions;trolley.obstacle.carving=true;trolley.obstacle.carveOnlyStationary=true;trolley.obstacle.enabled=true;
                var points=item.transform.Find("Service cargo slots");if(points)Object.DestroyImmediate(points.gameObject);points=new GameObject("Service cargo slots").transform;points.SetParent(item.transform,false);
                float platform=item.GetComponentsInChildren<BoxCollider>().First(c=>c.name=="Collision 0").bounds.max.y-item.transform.position.y;
                trolley.slots=Enumerable.Range(0,4).Select(i=>Point("Suitcase slot "+(i+1),points,new Vector3(i%2==0?-.27f:.27f,platform+.29f,i<2?-.18f:.18f))).ToArray();
                trolley.leftHandle=Point("Left push grip",points,new Vector3(-.55f,-item.dimensions.y/2+1.18f,.20f));trolley.rightHandle=Point("Right push grip",points,new Vector3(-.55f,-item.dimensions.y/2+1.18f,-.20f));trolley.groundOffset=item.dimensions.y/2;trolley.pushDistance=1.4f;item.displayName="Luggage trolley / 4 tagged suitcases";var body=item.GetComponent<Rigidbody>();if(body){body.mass=18;body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;}
                trolleys.Add(trolley);EditorUtility.SetDirty(item);EditorUtility.SetDirty(trolley);
            }
            jobs.trolleys=trolleys.ToArray();jobs.receivers=new HotelRoomDelivery[hotel.rooms.Length];
            for(int i=0;i<hotel.rooms.Length;i++)
            {
                var room=hotel.rooms[i];var direction=room.standPoint-room.door.transform.position;direction.y=0;direction.Normalize();
                var position=room.door.transform.position-direction*.16f+room.door.transform.right*.94f;position.y=room.standPoint.y+1.15f;
                var go=new GameObject("Room "+room.number+" delivery point");go.transform.SetParent(root,false);go.transform.position=position;Box("Delivery plaque",go.transform,Vector3.zero,new Vector3(.36f,.22f,.035f),teal);var plaqueCollider=go.AddComponent<BoxCollider>();plaqueCollider.size=new Vector3(.36f,.22f,.05f);plaqueCollider.isTrigger=true;go.transform.rotation=Quaternion.LookRotation(-direction,Vector3.up);var item=go.AddComponent<HotelItem>();item.kind="roomDelivery";item.displayName="Room "+room.number+" luggage / clean linen";
                var receiver=go.AddComponent<HotelRoomDelivery>();receiver.jobs=jobs;receiver.roomIndex=i;receiver.deposit=Point("Room delivery deposit",root,room.door.transform.position+direction*1.1f);receiver.deposit.position=new Vector3(receiver.deposit.position.x,room.standPoint.y+.43f,receiver.deposit.position.z);
                Box("Room linen shelf",root,receiver.deposit.position-Vector3.up*.13f,new Vector3(.43f,.09f,.35f),wood);Sign("Delivery room label",go.transform,new Vector3(0,0,.023f),Vector3.forward,"ROOM "+room.number+"\nE DELIVER",.004f);jobs.receivers[i]=receiver;
            }
            var machines=new List<HotelLaundryMachine>();
            foreach(var item in Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None).Where(i=>i.sourceModel=="LaundryWasher"))
            {
                var machine=item.GetComponent<HotelLaundryMachine>();if(!machine)machine=item.gameObject.AddComponent<HotelLaundryMachine>();machine.jobs=jobs;item.kind="laundry";item.displayName="Guest laundry washer";
                machine.output=Point("Clean linen collection",root,item.transform.position+Vector3.forward*.8f+Vector3.up*.32f);
                machine.status=Sign("Laundry washer status",root,item.transform.position+new Vector3(0,.72f,.51f),Vector3.forward,"WASHER / E LOAD",.006f);machines.Add(machine);EditorUtility.SetDirty(item);EditorUtility.SetDirty(machine);
            }
            if(machines.Count==0)throw new InvalidOperationException("Existing laundry washer not found.");jobs.washers=machines.ToArray();
            var trayPrefab=TrayPrefab();jobs.trays=new HotelServiceTray[3];for(int i=0;i<3;i++){var tray=(HotelServiceTray)PrefabUtility.InstantiatePrefab(trayPrefab,root);tray.transform.SetPositionAndRotation(new Vector3(-6.78f,1.27f,4.0f+i*.5f),Quaternion.Euler(0,90,0));jobs.trays[i]=tray;}
            Sign("Tray preparation instructions",root,new Vector3(-6.42f,1.8f,4.5f),Vector3.right,"TRAY SERVICE\nE LOAD FOOD / E SERVE",.007f);
            var lifts=new HotelServiceLift[2];for(int i=0;i<2;i++)
            {
                float floor=i==0?.025f:4.525f;var point=new Vector3(8.3f,floor+1.25f,-10.85f);var go=new GameObject("Service lift "+(i==0?"ground":"guest floor"));go.transform.SetParent(root,false);go.transform.position=point;Box("Service lift panel",go.transform,Vector3.zero,new Vector3(.45f,.65f,.10f),dark);go.AddComponent<BoxCollider>().size=new Vector3(.45f,.65f,.10f);var item=go.AddComponent<HotelItem>();item.kind="serviceLift";item.displayName="Service lift to "+(i==0?"guest rooms":"ground floor");var lift=go.AddComponent<HotelServiceLift>();lift.hotel=hotel;lift.floorName=i==0?"GROUND":"GUEST ROOMS";lift.arrivalPoint=new Vector3(9.7f,floor,-10.25f);lifts[i]=lift;Sign("Lift service panel",go.transform,new Vector3(0,0,.061f),Vector3.forward,"SERVICE LIFT\nE + TROLLEY\n"+(i==0?"UP TO ROOMS":"DOWN TO LOBBY"),.005f);
            }
            lifts[0].destination=lifts[1];lifts[1].destination=lifts[0];
            var surface=hotel.GetComponent<NavMeshSurface>();surface.BuildNavMesh();string navPath=Art+"Hotel service navigation.asset";if(AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath))AssetDatabase.DeleteAsset(navPath);AssetDatabase.CreateAsset(surface.navMeshData,navPath);
            EditorUtility.SetDirty(surface);EditorUtility.SetDirty(hotel);EditorUtility.SetDirty(jobs);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);HotelEmployeeBuilder.Save();Debug.Log("SERVICE_JOBS_BUILT rooms="+jobs.receivers.Length+" trolleys="+trolleys.Count+" washers="+machines.Count+" guests="+hotel.maxGuests);
        }
    }
}
