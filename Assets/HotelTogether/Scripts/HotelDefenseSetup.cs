using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
namespace HotelTogether
{
    public sealed class HotelDefenseSetup : MonoBehaviour
    {
        public int WindowCount {get;private set;}
        public int DoorCount {get;private set;}
        public HotelDefenseHammer Hammer {get;private set;}
        Material wood,steel,dark,red,green;
        bool installed;
        GameObject Shape(string name,Transform parent,Vector3 point,Vector3 size,Material material,bool physical=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=point;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;if(!physical){var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);}return go;
        }
        Material Material(string name,Color color){var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")??Shader.Find("Hidden/InternalErrorShader");var material=new Material(shader){name=name,color=color};material.SetFloat("_Smoothness",.22f);return material;}
        public void Install(HotelZombieDirector director)
        {
            if(installed||!director||!director.hotel||!director.hotel.player)return;installed=true;
            wood=Material("Barricade pine",new Color(.46f,.265f,.105f));steel=Material("Barricade steel",new Color(.3f,.34f,.36f));dark=Material("Hammer rubber",new Color(.06f,.07f,.07f));red=Material("Locked red",new Color(.85f,.12f,.06f));green=Material("Unlocked green",new Color(.12f,.65f,.3f));
            var openings=new List<HotelDefenseOpening>();if(director.openings!=null)foreach(var opening in director.openings)if(opening)openings.Add(opening);
            int nextWindow=0;foreach(var o in openings)if(o.window)nextWindow++;var covered=new HashSet<Renderer>();foreach(var opening in openings)foreach(var r in opening.structureVisuals)if(r)covered.Add(r);
            // Architectural glazing is identified by its shared material, not furniture glass.
            var panes=FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            System.Array.Sort(panes,(a,b)=>{int floor=a.bounds.center.y.CompareTo(b.bounds.center.y);if(floor!=0)return floor;int x=a.bounds.center.x.CompareTo(b.bounds.center.x);return x!=0?x:a.bounds.center.z.CompareTo(b.bounds.center.z);});
            foreach(var pane in panes)
            {
                if(!pane.enabled||covered.Contains(pane)||pane.GetComponentInParent<HotelItem>()?.kind=="door")continue;
                bool glass=false;foreach(var m in pane.sharedMaterials)if(m&&m.name=="CityWindowGlass")glass=true;if(!glass)continue;
                var bounds=pane.bounds;var size=bounds.size;Vector3 normal,right,up;
                if(size.y<.2f){normal=Vector3.down;right=Vector3.right;up=Vector3.forward;size=new Vector3(size.x,size.z,.12f);}
                else if(size.x<size.z){normal=bounds.center.x<0?Vector3.left:Vector3.right;right=Vector3.back;up=Vector3.up;size=new Vector3(size.z,size.y,.12f);}
                else{normal=bounds.center.z<0?Vector3.back:Vector3.forward;right=Vector3.right;up=Vector3.up;size=new Vector3(size.x,size.y,.12f);}
                var go=new GameObject("Defensible glazing - "+pane.name);go.SetActive(false);go.transform.SetParent(transform,false);go.transform.SetPositionAndRotation(bounds.center,Quaternion.LookRotation(normal,up));
                var opening=go.AddComponent<HotelDefenseOpening>();opening.label="Window "+(++nextWindow);opening.window=true;opening.structureMax=100;opening.structureVisuals=new Renderer[]{pane};
                var glassCollider=go.AddComponent<BoxCollider>();glassCollider.size=size;opening.structureColliders=new Collider[]{glassCollider};
                var sensor=go.AddComponent<BoxCollider>();sensor.size=new Vector3(size.x,size.y,.32f);sensor.isTrigger=true;
                var item=go.AddComponent<HotelItem>();item.kind="barricade";item.displayName=opening.label;item.dimensions=size;
                float floor=bounds.center.y<4.5f?.025f:bounds.center.y<9?4.525f:9.025f;
                opening.outsidePoint=bounds.center+normal*.85f;opening.insidePoint=bounds.center-normal*.85f;opening.outsidePoint.y=opening.insidePoint.y=floor;
                openings.Add(opening);go.SetActive(true);covered.Add(pane);
            }
            foreach(var item in FindObjectsByType<HotelItem>(FindObjectsSortMode.None))
            {
                if(item.kind!="door"||item.door==null||item.leaves==null||item.leaves.Length==0)continue;
                var opening=item.GetComponent<HotelDefenseOpening>();
                if(!opening)
                {
                    opening=item.gameObject.AddComponent<HotelDefenseOpening>();opening.label=HotelPlayer.DefenseName(item);opening.exterior=false;opening.door=item;opening.structureMax=160;
                    var visuals=new List<Renderer>();var colliders=new List<Collider>();foreach(var leaf in item.leaves)if(leaf){visuals.AddRange(leaf.GetComponentsInChildren<Renderer>());colliders.AddRange(leaf.GetComponentsInChildren<Collider>());}
                    opening.structureVisuals=visuals.ToArray();opening.structureColliders=colliders.ToArray();float floor=item.transform.position.y-item.dimensions.y*.5f;
                    opening.outsidePoint=item.transform.position+item.transform.forward*.8f;opening.insidePoint=item.transform.position-item.transform.forward*.8f;opening.outsidePoint.y=opening.insidePoint.y=floor;openings.Add(opening);
                }
            }
            foreach(var opening in openings)
            {
                Vector3 size=opening.door?opening.door.dimensions:opening.boardCollider?opening.boardCollider.size:opening.GetComponent<BoxCollider>()?.size??new Vector3(2,2,.12f);
                if(size.x<=0||size.y<=0)size=new Vector3(1.4f,2.45f,.15f);
                BuildPlanks(opening,size);
                if(!opening.boardCollider){var blocker=new GameObject("Plank physical blocker");blocker.transform.SetParent(opening.transform,false);opening.boardCollider=blocker.AddComponent<BoxCollider>();opening.boardCollider.size=new Vector3(size.x,size.y,.22f);opening.boardCollider.enabled=false;}
                if(!opening.obstacle){opening.obstacle=opening.gameObject.AddComponent<NavMeshObstacle>();opening.obstacle.shape=NavMeshObstacleShape.Box;opening.obstacle.size=new Vector3(size.x,size.y,.25f);opening.obstacle.carving=true;opening.obstacle.carveOnlyStationary=false;opening.obstacle.enabled=false;}
                if(!opening.passage&&Mathf.Abs(opening.transform.forward.y)<.1f)
                {
                    var filter=new NavMeshQueryFilter{agentTypeID=director.zombiePrefab?director.zombiePrefab.GetComponent<NavMeshAgent>().agentTypeID:0,areaMask=NavMesh.AllAreas};
                    if(NavMesh.SamplePosition(opening.outsidePoint,out var a,.7f,filter)&&NavMesh.SamplePosition(opening.insidePoint,out var b,.7f,filter)&&Mathf.Abs(a.position.y-opening.outsidePoint.y)<.3f&&Mathf.Abs(b.position.y-opening.insidePoint.y)<.3f&&Vector3.Dot(a.position-opening.transform.position,opening.transform.forward)>.2f&&Vector3.Dot(b.position-opening.transform.position,opening.transform.forward)<-.2f)
                    {opening.outsidePoint=a.position;opening.insidePoint=b.position;var link=opening.gameObject.AddComponent<NavMeshLink>();link.agentTypeID=filter.agentTypeID;link.startPoint=opening.transform.InverseTransformPoint(a.position);link.endPoint=opening.transform.InverseTransformPoint(b.position);link.width=Mathf.Max(.4f,size.x*.65f);link.bidirectional=true;opening.passage=link;}
                }
                opening.Initialize(director);
                if(opening.door){DoorCount++;}else WindowCount++;
            }
            director.openings=openings.ToArray();var player=director.hotel.player;if(!player.GetComponent<HotelDefenseWork>())player.gameObject.AddComponent<HotelDefenseWork>();
            CutDuplicateWindowWalls(openings);
            var stock=FindObjectsByType<HotelDefenseStock>(FindObjectsSortMode.None);Vector3 position=new Vector3(-7.1f,1,-8.35f);foreach(var rack in stock)if(rack.type==DefenseStockType.Wood){position=rack.transform.position+Vector3.up*.48f+Vector3.right*.65f;break;}
            var hammer=new GameObject("Barricade hammer - click to equip");hammer.transform.SetParent(transform,false);hammer.transform.position=position;
            var stand=new GameObject("Hammer stand beside plank rack");stand.transform.SetParent(transform,false);stand.transform.position=position-Vector3.up*.05f;
            float standHeight=Mathf.Max(.1f,stand.transform.position.y);Shape("Stand column",stand.transform,Vector3.down*(standHeight*.5f),new Vector3(.10f,standHeight,.10f),steel,true);Shape("Stand foot",stand.transform,Vector3.down*(standHeight-.02f),new Vector3(.30f,.04f,.30f),dark,true);
            Shape("Ash handle",hammer.transform,new Vector3(0,.16f,0),new Vector3(.045f,.38f,.045f),wood);
            Shape("Rubber grip",hammer.transform,new Vector3(0,.035f,0),new Vector3(.055f,.17f,.055f),dark);
            Shape("Steel hammer head",hammer.transform,new Vector3(0,.36f,0),new Vector3(.23f,.085f,.075f),steel);
            var body=hammer.AddComponent<Rigidbody>();body.isKinematic=true;body.mass=.7f;hammer.AddComponent<BoxCollider>().size=new Vector3(.25f,.48f,.12f);hammer.GetComponent<BoxCollider>().center=Vector3.up*.19f;
            var hammerItem=hammer.AddComponent<HotelItem>();hammerItem.kind="pickup";hammerItem.displayName="Barricade hammer";hammerItem.dimensions=new Vector3(.25f,.48f,.12f);Hammer=hammer.AddComponent<HotelDefenseHammer>();
        }
        void BuildPlanks(HotelDefenseOpening opening,Vector3 size)
        {
            if(opening.planks.Length==4&&opening.planks[0]){foreach(var plank in opening.planks)AddStrap(plank);return;}
            var group=new GameObject("Defensive planks");group.transform.SetParent(opening.transform,false);opening.planks=new GameObject[4];
            for(int i=0;i<4;i++){
                var plank=Shape("Plank "+(i+1),group.transform,new Vector3(0,-size.y*.5f+size.y*(i+1)/5,-.14f),new Vector3(size.x+.1f,.22f,.075f),wood);plank.transform.localRotation=Quaternion.Euler(0,0,i%2==0?4:-4);
                foreach(float side in new[]{-1f,1f})Shape("Nail head",plank.transform,new Vector3(side*.43f,0,-.53f),new Vector3(.013f,.14f,.10f),steel);AddStrap(plank);opening.planks[i]=plank;plank.SetActive(false);
            }
        }
        void AddStrap(GameObject plank){if(plank&& !plank.transform.Find("Reinforcement strap")){var strap=Shape("Reinforcement strap",plank.transform,new Vector3(0,0,-.59f),new Vector3(.9f,.22f,.12f),steel);strap.SetActive(false);}}
        void CutDuplicateWindowWalls(List<HotelDefenseOpening> openings)
        {
            var collisionRoot=GameObject.Find("Architecture collision");if(!collisionRoot)return;
            foreach(var collider in collisionRoot.GetComponentsInChildren<BoxCollider>())
            {
                if(!collider.enabled||collider.isTrigger)continue;var wall=collider.bounds;bool zPlane=wall.size.z<.4f&&wall.size.x>1;bool xPlane=wall.size.x<.4f&&wall.size.z>1;if(!zPlane&&!xPlane)continue;
                var slices=new List<Rect>{new Rect(zPlane?wall.min.x:wall.min.z,wall.min.y,zPlane?wall.size.x:wall.size.z,wall.size.y)};bool changed=false;
                foreach(var opening in openings)
                {
                    if(!opening.window||Mathf.Abs(opening.transform.forward.y)>.1f)continue;
                    foreach(var renderer in opening.structureVisuals)
                    {
                        if(!renderer)continue;var pane=renderer.bounds;
                        if(zPlane&&pane.size.z>.2f||xPlane&&pane.size.x>.2f||Mathf.Abs((zPlane?pane.center.z:pane.center.x)-(zPlane?wall.center.z:wall.center.x))>.18f)continue;
                        var hole=new Rect((zPlane?pane.min.x:pane.min.z)-.02f,pane.min.y-.02f,(zPlane?pane.size.x:pane.size.z)+.04f,pane.size.y+.04f);var remaining=new List<Rect>();
                        foreach(var slice in slices){float left=Mathf.Max(slice.xMin,hole.xMin),right=Mathf.Min(slice.xMax,hole.xMax),bottom=Mathf.Max(slice.yMin,hole.yMin),top=Mathf.Min(slice.yMax,hole.yMax);if(left>=right||bottom>=top){remaining.Add(slice);continue;}changed=true;AddSlice(remaining,new Rect(slice.xMin,slice.yMin,left-slice.xMin,slice.height));AddSlice(remaining,new Rect(right,slice.yMin,slice.xMax-right,slice.height));AddSlice(remaining,new Rect(left,slice.yMin,right-left,bottom-slice.yMin));AddSlice(remaining,new Rect(left,top,right-left,slice.yMax-top));}slices=remaining;
                    }
                }
                if(!changed)continue;collider.enabled=false;
                foreach(var slice in slices){var go=new GameObject("Window frame collision remainder");go.transform.SetParent(transform,false);go.transform.position=zPlane?new Vector3(slice.center.x,slice.center.y,wall.center.z):new Vector3(wall.center.x,slice.center.y,slice.center.x);go.AddComponent<BoxCollider>().size=zPlane?new Vector3(slice.width,slice.height,wall.size.z):new Vector3(wall.size.x,slice.height,slice.width);}
            }
        }
        static void AddSlice(List<Rect> slices,Rect slice){if(slice.width>.005f&&slice.height>.005f)slices.Add(slice);}
        void BuildLatches(HotelDefenseOpening opening,Vector3 size)
        {
            foreach(float side in new[]{-1f,1f})
            {
                var go=new GameObject("Door lock - click to toggle");go.transform.SetParent(opening.transform,false);go.transform.localPosition=new Vector3(size.x*.5f-.13f,Mathf.Clamp(1.25f-size.y*.5f,-.3f,.3f),side*(Mathf.Max(size.z,.1f)*.5f+.05f));go.transform.localRotation=Quaternion.Euler(0,side>0?180:0,0);
                // Choose the other jamb if an adjacent privacy door obstructs this face.
                var rayStart=go.transform.position-go.transform.forward*1.2f;
                if(Physics.Linecast(rayStart,go.transform.position,out var hit,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore)&&!hit.collider.transform.IsChildOf(opening.transform))go.transform.localPosition=new Vector3(-go.transform.localPosition.x,go.transform.localPosition.y,go.transform.localPosition.z);
                Shape("Lock plate",go.transform,Vector3.zero,new Vector3(.20f,.24f,.04f),steel);
                var bolt=Shape("Sliding bolt",go.transform,new Vector3(0,0,-.04f),new Vector3(.16f,.05f,.06f),dark).transform;
                var latch=go.AddComponent<HotelDefenseLatch>();latch.opening=opening;latch.bolt=bolt;latch.lockedIndicator=Shape("Locked",go.transform,new Vector3(-.06f,.075f,-.03f),new Vector3(.035f,.035f,.02f),red);latch.unlockedIndicator=Shape("Unlocked",go.transform,new Vector3(-.06f,.075f,-.03f),new Vector3(.035f,.035f,.02f),green);
                var collider=go.AddComponent<BoxCollider>();collider.size=new Vector3(.24f,.29f,.12f);collider.isTrigger=true;var item=go.AddComponent<HotelItem>();item.kind="defenseLock";item.displayName="Door lock";
            }
        }
        void OnDestroy(){foreach(var material in new[]{wood,steel,dark,red,green})if(material)Destroy(material);}
    }
}
