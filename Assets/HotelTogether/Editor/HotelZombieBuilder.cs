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
    public static class HotelZombieBuilder
    {
        const string Art="Assets/HotelTogether/Art/Defense/",Prefabs="Assets/HotelTogether/Prefabs/Defense/";
        static Material wood,metal,dark;
        static Transform root;
        static Material Mat(string name,Color color)
        {var path=Art+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}mat.color=color;mat.SetFloat("_Smoothness",.16f);EditorUtility.SetDirty(mat);return mat;}
        static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material material,bool collider=false)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
        static HotelDefenseOpening Opening(GameObject owner,string label,bool exterior,bool window,Vector3 outside,Vector3 inside,Vector3 size,Renderer[] visuals,Collider[] colliders,HotelItem door=null)
        {
            var opening=owner.GetComponent<HotelDefenseOpening>();if(!opening)opening=owner.AddComponent<HotelDefenseOpening>();opening.label=label;opening.exterior=exterior;opening.window=window;opening.outsidePoint=outside;opening.insidePoint=inside;opening.door=door;opening.structureMax=window?100:160;opening.structureVisuals=visuals;opening.structureColliders=colliders;
            var old=owner.transform.Find("Defensive planks");if(old)Object.DestroyImmediate(old.gameObject);var group=new GameObject("Defensive planks").transform;group.SetParent(owner.transform,false);opening.planks=new GameObject[4];
            for(int i=0;i<4;i++)
            {
                var plank=Box("Plank "+(i+1),group,new Vector3(0,-size.y*.5f+.55f+i*.49f,-.12f),new Vector3(size.x+.16f,.23f,.075f),wood);plank.transform.localRotation=Quaternion.Euler(0,0,i%2==0?5:-5);
                foreach(float side in new[]{-1f,1f})Box("Galvanized nail",plank.transform,new Vector3(side*.42f,0,-.58f),new Vector3(.008f,.085f,.18f),metal);
                opening.planks[i]=plank;plank.SetActive(false);
            }
            var barrier=owner.transform.Find("Plank physical blocker");if(barrier)Object.DestroyImmediate(barrier.gameObject);var blocker=new GameObject("Plank physical blocker");blocker.transform.SetParent(owner.transform,false);opening.boardCollider=blocker.AddComponent<BoxCollider>();opening.boardCollider.size=new Vector3(size.x,size.y,.18f);opening.boardCollider.enabled=false;
            opening.obstacle=owner.GetComponent<NavMeshObstacle>();if(!opening.obstacle)opening.obstacle=owner.AddComponent<NavMeshObstacle>();opening.obstacle.shape=NavMeshObstacleShape.Box;float navHeight=window?3.75f:size.y;opening.obstacle.center=new Vector3(0,(navHeight-size.y)*.5f,0);opening.obstacle.size=new Vector3(size.x,navHeight,.20f);opening.obstacle.carving=true;opening.obstacle.carveOnlyStationary=false;opening.obstacle.enabled=false;
            var modifier=owner.GetComponent<NavMeshModifier>();if(!modifier)modifier=owner.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
            EditorUtility.SetDirty(opening);return opening;
        }
        static HotelItem Bat()
        {
            var go=new GameObject("Hotel defense baseball bat");var shaft=GameObject.CreatePrimitive(PrimitiveType.Capsule);shaft.transform.SetParent(go.transform,false);shaft.transform.localScale=new Vector3(.085f,.40f,.085f);shaft.GetComponent<Renderer>().sharedMaterial=wood;Object.DestroyImmediate(shaft.GetComponent<Collider>());
            var grip=GameObject.CreatePrimitive(PrimitiveType.Cylinder);grip.transform.SetParent(go.transform,false);grip.transform.localPosition=new Vector3(0,-.25f,0);grip.transform.localScale=new Vector3(.051f,.105f,.051f);grip.GetComponent<Renderer>().sharedMaterial=dark;Object.DestroyImmediate(grip.GetComponent<Collider>());
            var collider=go.AddComponent<CapsuleCollider>();collider.radius=.045f;collider.height=.83f;var rb=go.AddComponent<Rigidbody>();rb.isKinematic=true;rb.mass=.9f;
            var item=go.AddComponent<HotelItem>();item.kind="pickup";item.displayName="Defense bat";item.dimensions=new Vector3(.09f,.83f,.09f);go.AddComponent<HotelWeapon>();var prefab=PrefabUtility.SaveAsPrefabAsset(go,Prefabs+"Defense bat.prefab").GetComponent<HotelItem>();Object.DestroyImmediate(go);return prefab;
        }
        [MenuItem("Hotel Together/Defense/Build nighttime zombies and barricades")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="HotelLobby")throw new InvalidOperationException("Open HotelLobby first.");Directory.CreateDirectory(Art);Directory.CreateDirectory(Prefabs);Directory.CreateDirectory("../tmp/zombies");
            if(!File.Exists("../tmp/zombies/HotelLobby-before-defense.unity"))File.Copy(scene.path,"../tmp/zombies/HotelLobby-before-defense.unity");
            var hotel=Object.FindFirstObjectByType<HotelGuestSimulation>();if(!hotel)throw new InvalidOperationException("Install the customer simulation first.");
            var previous=Object.FindFirstObjectByType<HotelZombieDirector>();if(previous)Object.DestroyImmediate(previous.gameObject);var go=new GameObject("Hotel night defense and zombie waves");root=go.transform;var director=go.AddComponent<HotelZombieDirector>();director.hotel=hotel;
            wood=Mat("Defense rough pine",new Color(.48f,.28f,.115f));metal=Mat("Defense galvanized nails",new Color(.36f,.39f,.40f));dark=Mat("Defense rubber grip",new Color(.035f,.035f,.025f));
            var zombieSource=HotelGuestBuilder.BuildZombieModel();var model=(GameObject)PrefabUtility.InstantiatePrefab(zombieSource);
            // Existing variants can retain null mesh overrides from an earlier mesh rebuild.
            model.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/HotelTogether/Art/Guests/Pink robe zombie body.asset");
            model.GetComponentInChildren<MeshFilter>(true).sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/HotelTogether/Art/Guests/Pink robe zombie suitcase.asset");
            Object.DestroyImmediate(model.GetComponent<HotelGuestBot>());model.GetComponent<HotelItem>().kind="zombie";model.GetComponent<HotelItem>().displayName="Pink robe zombie";
            var agent=model.GetComponent<NavMeshAgent>();agent.speed=1.1f;agent.stoppingDistance=.10f;agent.avoidancePriority=65;model.GetComponent<CapsuleCollider>().isTrigger=false;var body=model.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
            var bot=model.AddComponent<HotelZombieBot>();bot.visual=model.GetComponentInChildren<HotelGuestVisual>();bot.visual.transform.localPosition=Vector3.down*.025f;if(bot.visual.luggage)bot.visual.luggage.SetActive(false);director.zombiePrefab=PrefabUtility.SaveAsPrefabAsset(model,Prefabs+"Pink robe zombie.prefab").GetComponent<HotelZombieBot>();Object.DestroyImmediate(model);
            foreach(var guestPrefab in hotel.guestPrefabs)
            {
                string path=AssetDatabase.GetAssetPath(guestPrefab);var contents=PrefabUtility.LoadPrefabContents(path);if(!contents.GetComponent<HotelHuman>())contents.AddComponent<HotelHuman>();if(!contents.GetComponent<HotelGuestSurvival>())contents.AddComponent<HotelGuestSurvival>();PrefabUtility.SaveAsPrefabAsset(contents,path);PrefabUtility.UnloadPrefabContents(contents);
            }
            var player=hotel.player;if(!player.GetComponent<HotelHuman>())player.gameObject.AddComponent<HotelHuman>();var combat=player.GetComponent<HotelCombat>();if(!combat)combat=player.gameObject.AddComponent<HotelCombat>();combat.batPrefab=Bat();player.combat=combat;director.combat=combat;
            var openings=new List<HotelDefenseOpening>();var entrance=GameObject.Find("Lobby automatic sliding entrance");var entranceItem=entrance.GetComponent<HotelItem>();openings.Add(Opening(entrance,"Lobby glass entrance",true,false,new Vector3(0,.025f,8.0f),new Vector3(0,.025f,6.25f),new Vector3(4,3.4f,.2f),entranceItem.leaves.SelectMany(l=>l.GetComponentsInChildren<Renderer>()).ToArray(),entranceItem.leaves.SelectMany(l=>l.GetComponentsInChildren<Collider>()).ToArray(),entranceItem));
            var architecture=GameObject.Find("Hotel architecture and furnishings");var collision=GameObject.Find("Architecture collision");
            foreach(int id in new[]{79,88}){var wall=collision.transform.Find("Structure collision "+id+"/Collision 0");if(wall){wall.GetComponent<Collider>().enabled=false;EditorUtility.SetDirty(wall.GetComponent<Collider>());}}
            var panes=architecture.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.sharedMaterials.Any(m=>m&&m.name=="CityWindowGlass")&&Mathf.Abs(r.bounds.center.z-7)<.15f&&r.bounds.center.y<3&&r.bounds.size.x>2).OrderBy(r=>r.bounds.center.x).ToArray();
            foreach(var pane in panes)
            {
                var owner=new GameObject("Defensible front window "+pane.name);owner.transform.SetParent(root,false);owner.transform.position=pane.bounds.center;float width=pane.bounds.size.x+.09f;var box=owner.AddComponent<BoxCollider>();box.size=new Vector3(width,4.5f,.12f);var trigger=owner.AddComponent<BoxCollider>();trigger.size=new Vector3(width,4.5f,.30f);trigger.isTrigger=true;var item=owner.AddComponent<HotelItem>();item.kind="barricade";item.displayName="Front window "+pane.name;item.dimensions=box.size;
                float x=pane.bounds.center.x;openings.Add(Opening(owner,"Front window "+Mathf.RoundToInt(x),true,true,new Vector3(x,.025f,7.85f),new Vector3(x,.025f,5.8f),box.size,new[]{pane},new Collider[]{box}));
            }
            foreach(var room in hotel.rooms)
            {
                var door=room.door;Vector3 normal=room.bed.position-door.transform.position;normal.y=0;normal.Normalize();Vector3 center=door.transform.position;center.y=4.525f;
                openings.Add(Opening(door.gameObject,"Room "+room.number+" shelter door",false,false,center-normal*.80f,center+normal*.80f,door.dimensions,door.leaves.SelectMany(l=>l.GetComponentsInChildren<Renderer>()).ToArray(),door.leaves.SelectMany(l=>l.GetComponentsInChildren<Collider>()).ToArray(),door));
            }
            var rear=Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None).FirstOrDefault(i=>i.name=="Prop_0003");if(rear)openings.Add(Opening(rear.gameObject,"Rear delivery entrance",true,false,new Vector3(3,.025f,-33.8f),new Vector3(3,.025f,-31.8f),rear.dimensions,rear.leaves.SelectMany(l=>l.GetComponentsInChildren<Renderer>()).ToArray(),rear.leaves.SelectMany(l=>l.GetComponentsInChildren<Collider>()).ToArray(),rear));
            director.openings=openings.ToArray();director.spawnPoints=new[]{new Vector3(0,.025f,18),new Vector3(-12,.025f,12),new Vector3(14,.025f,12),new Vector3(-24,.025f,12),new Vector3(25,.025f,12)};
            var rack=new GameObject("Defense preparation plank rack");rack.transform.SetParent(root,false);rack.transform.position=new Vector3(-7.65f,.38f,-8.75f);Box("Sturdy wood storage crate",rack.transform,Vector3.zero,new Vector3(1.05f,.75f,.45f),wood,true);for(int i=0;i<7;i++){var plank=Box("Spare barricade plank",rack.transform,new Vector3(-.38f+i*.125f,.52f,0),new Vector3(.105f,.72f,.065f),wood);plank.transform.localRotation=Quaternion.Euler(0,0,i%2==0?7:-6);}
            var stock=rack.AddComponent<HotelDefenseStock>();stock.type=DefenseStockType.Wood;var stockItem=rack.AddComponent<HotelItem>();stockItem.kind="defenseStock";stockItem.displayName="Barricade plank rack";
            foreach(var kit in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("First aid - ")))
            {GameObjectUtility.RemoveMonoBehavioursWithMissingScript(kit.gameObject);var medical=kit.GetComponent<HotelDefenseStock>();if(!medical)medical=kit.gameObject.AddComponent<HotelDefenseStock>();medical.type=DefenseStockType.Medicine;var item=kit.GetComponent<HotelItem>();if(!item)item=kit.gameObject.AddComponent<HotelItem>();item.kind="defenseStock";item.displayName="First aid cabinet";if(!kit.GetComponent<Collider>()){var sensor=kit.gameObject.AddComponent<BoxCollider>();sensor.size=new Vector3(.5f,.5f,.22f);sensor.isTrigger=true;}}
            var surface=hotel.GetComponent<NavMeshSurface>();surface.BuildNavMesh();string navPath=Art+"Hotel defense navigation.asset";if(AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath))AssetDatabase.DeleteAsset(navPath);AssetDatabase.CreateAsset(surface.navMeshData,navPath);
            var filter=new NavMeshQueryFilter{agentTypeID=surface.agentTypeID,areaMask=NavMesh.AllAreas};foreach(var opening in openings)
            {
                var start=opening.outsidePoint;var end=opening.insidePoint;
                if(NavMesh.SamplePosition(start,out var a,1.2f,filter)&&Mathf.Abs(a.position.y-start.y)<.4f)start=a.position;
                if(NavMesh.SamplePosition(end,out var b,1.2f,filter)&&Mathf.Abs(b.position.y-end.y)<.4f)end=b.position;
                opening.outsidePoint=start;opening.insidePoint=end;var link=opening.GetComponent<NavMeshLink>();if(!link)link=opening.gameObject.AddComponent<NavMeshLink>();link.agentTypeID=surface.agentTypeID;link.startPoint=opening.transform.InverseTransformPoint(start);link.endPoint=opening.transform.InverseTransformPoint(end);link.width=opening.obstacle.size.x*.65f;link.bidirectional=true;link.enabled=false;opening.passage=link;EditorUtility.SetDirty(opening);
            }
            EditorUtility.SetDirty(director);EditorUtility.SetDirty(hotel);EditorUtility.SetDirty(player);EditorUtility.SetDirty(combat);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);HotelEmployeeBuilder.Save();Debug.Log("DEFENSE_BUILT openings="+openings.Count+" windows="+panes.Length+" zombie nighttime waves=3,5,7...");
        }
    }
}
