using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
namespace HotelTogether.Editor
{
 public static class HotelElevatorBuilder
 {
  const string Art="Assets/HotelTogether/Art/Elevators/";
  static Material ivory,teal,brass,steel,dark,wood,glass;
  static Material Mat(string name,Color color,float metal=0){var path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",.4f);EditorUtility.SetDirty(m);return m;}
  static Transform Point(string name,Transform root,Vector3 p){var t=new GameObject(name).transform;t.SetParent(root,false);t.localPosition=p;return t;}
  static GameObject Box(string name,Transform root,Vector3 p,Vector3 size,Material m,bool collision=true){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<MeshRenderer>().sharedMaterial=m;if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
  static TextMesh Label(Transform root,Vector3 p,string text,float size=.008f){var t=Point("Elevator label",root,p);t.localRotation=Quaternion.Euler(0,180,0);var l=t.gameObject.AddComponent<TextMesh>();l.font=Resources.Load<Font>("HotelTogetherUI/LilitaOne-Regular");l.fontSize=60;l.characterSize=size;l.text=text;l.anchor=TextAnchor.MiddleCenter;l.color=ivory.color;t.GetComponent<MeshRenderer>().sharedMaterial=l.font.material;return l;}
  static HotelElevatorButton Button(HotelElevator lift,Transform root,Vector3 p,int floor,bool inside,string label)
  {
   var go=Point("Elevator "+label,root,p).gameObject;Box("Elevator button face",go.transform,Vector3.zero,new Vector3(.32f,.25f,.06f),teal,false);go.AddComponent<BoxCollider>().size=new Vector3(.32f,.25f,.06f);var item=go.AddComponent<HotelItem>();item.kind="elevator";item.displayName=label;var button=go.AddComponent<HotelElevatorButton>();button.elevator=lift;button.floor=floor;button.inside=inside;Label(go.transform,new Vector3(0,0,.042f),label,.007f);return button;
  }
  // Unit-scale door roots slide the whole leaf, including its real viewing opening.
  static Transform WindowDoor(string name,Transform parent,Vector3 position,float depth)
  {
   var leaf=Point(name,parent,position);leaf.gameObject.AddComponent<BoxCollider>().size=new Vector3(.96f,2.7f,depth);
   const float width=.28f,height=.68f,y=.36f;
   float sideWidth=(.96f-width)/2;
   foreach(float side in new[]{-1f,1f})Box("Door satin side",leaf,new Vector3(side*(width+sideWidth)/2,0,0),new Vector3(sideWidth,2.7f,depth),steel,false);
   float low=y-height/2,high=y+height/2;
   Box("Door lower panel",leaf,new Vector3(0,(-1.35f+low)/2,0),new Vector3(width,low+1.35f,depth),steel,false);
   Box("Door upper panel",leaf,new Vector3(0,(1.35f+high)/2,0),new Vector3(width,1.35f-high,depth),steel,false);
   Box("Door viewing glass",leaf,new Vector3(0,y,0),new Vector3(width,height,.008f),glass,false);
   foreach(float side in new[]{-1f,1f}){
    Box("Window brass upright",leaf,new Vector3(side*(width/2+.008f),y,0),new Vector3(.016f,height+.032f,depth+.008f),brass,false);
    Box("Window brass edge",leaf,new Vector3(0,y+side*(height/2+.008f),0),new Vector3(width+.032f,.016f,depth+.008f),brass,false);
   }
   return leaf;
  }
  static void FloorButton(HotelElevator lift,Transform panel,float y,int floor,string label)
  {
   var button=Button(lift,panel,new Vector3(0,y,.036f),floor,true,label);
   Object.DestroyImmediate(button.transform.Find("Elevator button face").gameObject);
   var text=button.GetComponentInChildren<TextMesh>();text.characterSize=label=="Lobby"?.009f:.014f;text.transform.localPosition=new Vector3(.068f,0,.039f);
   button.GetComponent<BoxCollider>().size=new Vector3(.38f,.22f,.06f);
   Box("Recessed floor key",button.transform,Vector3.zero,new Vector3(.38f,.22f,.024f),steel,false);
   var ring=GameObject.CreatePrimitive(PrimitiveType.Cylinder);ring.name="Brass floor key ring";ring.transform.SetParent(button.transform,false);ring.transform.localPosition=new Vector3(-.12f,0,.021f);ring.transform.localRotation=Quaternion.Euler(90,0,0);ring.transform.localScale=new Vector3(.08f,.007f,.08f);ring.GetComponent<MeshRenderer>().sharedMaterial=brass;Object.DestroyImmediate(ring.GetComponent<Collider>());
   var inset=Object.Instantiate(ring,button.transform);inset.name="Teal floor key";inset.transform.localPosition=new Vector3(-.12f,0,.030f);inset.transform.localScale=new Vector3(.061f,.005f,.061f);inset.GetComponent<MeshRenderer>().sharedMaterial=teal;
  }
  [MenuItem("Hotel Together/Elevators/Build operational cabin elevators")]
  public static void Build()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="HotelLobby")throw new InvalidOperationException("Open HotelLobby.");Directory.CreateDirectory(Art);
   var old=GameObject.Find("Operational hotel elevators");if(old)Object.DestroyImmediate(old);var root=new GameObject("Operational hotel elevators").transform;var h=Object.FindFirstObjectByType<HotelGuestSimulation>();
   foreach(var item in Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None).Where(i=>i.GetComponent<HotelServiceLift>()||i.kind=="door"&&Mathf.Abs(i.transform.position.z+11)<.1f&&(Mathf.Abs(i.transform.position.x-3)<.1f||Mathf.Abs(i.transform.position.x-7)<.1f)).ToArray()){if(item.leaves!=null)foreach(var leaf in item.leaves)if(leaf)Object.DestroyImmediate(leaf.gameObject);Object.DestroyImmediate(item.gameObject);}
   foreach(float x in new[]{3f,7f}){
    HotelStairSitePreparation.CutElevatorVolume(root,new Bounds(new Vector3(x,6.05f,-14),new Vector3(3.5f,11.9f,6.12f)));
    foreach(float floor in new[]{.025f,4.525f,9.025f})HotelStairSitePreparation.CutElevatorVolume(root,new Bounds(new Vector3(x,floor+1.36f,-11),new Vector3(1.94f,2.72f,.44f)));
   }
   ivory=Mat("Elevator ivory",new Color(.91f,.86f,.72f));teal=Mat("Elevator teal",new Color(.035f,.19f,.21f));brass=Mat("Elevator brass",new Color(.65f,.47f,.21f),.65f);steel=Mat("Elevator satin steel",new Color(.53f,.57f,.59f),.62f);dark=Mat("Elevator charcoal",new Color(.025f,.03f,.035f));wood=Mat("Elevator warm walnut",new Color(.23f,.11f,.055f));
   glass=Mat("Elevator viewing glass",new Color(.55f,.77f,.80f,.13f));glass.SetFloat("_Surface",1);glass.SetFloat("_SrcBlend",5);glass.SetFloat("_DstBlend",10);glass.SetFloat("_ZWrite",0);glass.SetFloat("_Cull",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;glass.SetFloat("_Smoothness",.65f);EditorUtility.SetDirty(glass);
   h.elevators=new HotelElevator[2];
   for(int index=0;index<2;index++)
   {
    float x=index==0?3:7;var liftRoot=Point(index==0?"Guest elevator":"Service elevator",root,new Vector3(x,0,0));var lift=liftRoot.gameObject.AddComponent<HotelElevator>();lift.hotel=h;lift.service=index==1;lift.floors=new HotelElevator.Landing[3];h.elevators[index]=lift;
    lift.cabin=Point("Moving elevator cabin",liftRoot,new Vector3(0,.025f,-13));var body=lift.cabin.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;var ignore=lift.cabin.gameObject.AddComponent<NavMeshModifier>();ignore.ignoreFromBuild=true;
    Box("Cabin floor",lift.cabin,new Vector3(0,-.07f,0),new Vector3(3.4f,.14f,4),ivory);Box("Cabin ceiling",lift.cabin,new Vector3(0,2.74f,0),new Vector3(3.4f,.12f,4),ivory);Box("Cabin rear wall",lift.cabin,new Vector3(0,1.35f,-1.94f),new Vector3(3.4f,2.7f,.12f),wood);
    foreach(float side in new[]{-1f,1f}){Box("Cabin side wall",lift.cabin,new Vector3(side*1.64f,1.35f,0),new Vector3(.12f,2.7f,4),ivory);Box("Cabin teal wainscot",lift.cabin,new Vector3(side*1.57f,.55f,0),new Vector3(.02f,1.05f,3.8f),teal,false);Box("Cabin handrail",lift.cabin,new Vector3(side*1.50f,1.1f,-.10f),new Vector3(.035f,.04f,3.45f),brass,false);Box("Cabin front return",lift.cabin,new Vector3(side*1.3f,1.35f,1.95f),new Vector3(.67f,2.7f,.10f),wood);}
    Box("Cabin back handrail",lift.cabin,new Vector3(0,1.1f,-1.85f),new Vector3(3.0f,.04f,.035f),brass,false);
    lift.cabinDoors=new Transform[2];for(int door=0;door<2;door++)lift.cabinDoors[door]=WindowDoor("Cabin sliding door "+door,lift.cabin,new Vector3(door==0?-.48f:.48f,1.35f,1.98f),.06f);
    var ceilingLight=Point("Cabin ceiling light",lift.cabin,new Vector3(0,2.55f,0));var light=ceilingLight.gameObject.AddComponent<Light>();light.type=LightType.Spot;ceilingLight.localRotation=Quaternion.Euler(90,0,0);light.spotAngle=140;light.innerSpotAngle=100;light.range=4;light.intensity=1.6f;light.color=new Color(1,.84f,.65f);light.shadows=LightShadows.None;Box("Ceiling luminous panel",lift.cabin,new Vector3(0,2.66f,0),new Vector3(2,.035f,1.8f),ivory,false);
    var control=Point("Cabin control panel",lift.cabin,new Vector3(1.38f,1.42f,1.83f));control.localRotation=Quaternion.Euler(0,180,0);
    Box("Cabin control brass border",control,new Vector3(0,0,-.025f),new Vector3(.48f,1.10f,.025f),brass,false);Box("Cabin control backing",control,Vector3.zero,new Vector3(.44f,1.06f,.04f),dark,false);
    Label(control,new Vector3(0,.43f,.032f),"FLOORS",.007f);for(int f=0;f<3;f++)FloorButton(lift,control,.24f-f*.27f,f,new[]{"Lobby","2","3"}[f]);
    var screen=Point("Interior floor indicator",lift.cabin,new Vector3(0,2.43f,1.87f));screen.localRotation=Quaternion.Euler(0,180,0);
    Box("Indicator brass surround",screen,Vector3.zero,new Vector3(1.25f,.42f,.035f),brass,false);Box("Indicator dark screen",screen,new Vector3(0,0,.024f),new Vector3(1.19f,.36f,.02f),dark,false);lift.cabinDisplay=Label(screen,new Vector3(0,0,.045f),"LOBBY\nREADY",.013f);
    lift.audioSource=lift.cabin.gameObject.AddComponent<AudioSource>();lift.audioSource.playOnAwake=false;lift.audioSource.spatialBlend=1;lift.audioSource.maxDistance=14;lift.motor=Point("Elevator motor audio",lift.cabin,new Vector3(0,2.5f,0)).gameObject.AddComponent<AudioSource>();lift.motor.playOnAwake=false;lift.motor.spatialBlend=1;lift.motor.volume=.2f;lift.motor.maxDistance=8;
    for(int f=0;f<3;f++)
    {
     float y=.025f+4.5f*f;var landing=Point("Landing "+f,liftRoot,new Vector3(0,y,-10.96f));var data=new HotelElevator.Landing{label=new[]{"Lobby","2","3"}[f],height=y,waitingPoint=new Vector3(x,y,-9.45f),doors=new Transform[2]};lift.floors[f]=data;
     for(int d=0;d<2;d++)data.doors[d]=WindowDoor("Landing safety door "+d,landing,new Vector3(d==0?-.48f:.48f,1.35f,0),.07f);
     foreach(float side in new[]{-1f,1f})Box("Landing brass jamb",landing,new Vector3(side*1.02f,1.36f,0),new Vector3(.08f,2.74f,.12f),brass);Box("Landing brass lintel",landing,new Vector3(0,2.75f,0),new Vector3(2.12f,.07f,.12f),brass);Box("Landing threshold",landing,new Vector3(0,-.016f,.085f),new Vector3(1.94f,.035f,.18f),steel);
     Box("Landing display backing",landing,new Vector3(0,3.04f,.03f),new Vector3(1.12f,.37f,.05f),dark,false);data.display=Label(landing,new Vector3(0,3.04f,.065f),"LOBBY\nREADY",.009f);if(f<2)Button(lift,landing,new Vector3(1.21f,1.36f,.05f),f,false,"UP").callDirection=1;if(f>0)Button(lift,landing,new Vector3(1.21f,1.04f,.05f),f,false,"DOWN").callDirection=-1;
    }
    // Idle cabins stay shut until a call; arriving cabins open automatically.
    EditorUtility.SetDirty(lift);
   }
   foreach(var prefab in h.guestPrefabs){string path=AssetDatabase.GetAssetPath(prefab);var instance=PrefabUtility.LoadPrefabContents(path);try{if(!instance.GetComponent<HotelGuestElevatorPassenger>())instance.AddComponent<HotelGuestElevatorPassenger>();PrefabUtility.SaveAsPrefabAsset(instance,path);}finally{PrefabUtility.UnloadPrefabContents(instance);}}
   var worldShader=AssetDatabase.LoadAssetAtPath<Shader>(Art+"HotelWorldText.shader");foreach(var label in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)){var depth=label.GetComponent<HotelWorldText>();if(!depth)depth=label.gameObject.AddComponent<HotelWorldText>();depth.shader=worldShader;EditorUtility.SetDirty(depth);}
   foreach(var bagPath in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/HotelTogether/Prefabs/ServiceJobs","Assets/HotelTogether/Prefabs/Guests"}).Select(AssetDatabase.GUIDToAssetPath)){var instance=PrefabUtility.LoadPrefabContents(bagPath);try{bool changed=false;foreach(var label in instance.GetComponentsInChildren<TextMesh>(true)){var depth=label.GetComponent<HotelWorldText>();if(!depth)depth=label.gameObject.AddComponent<HotelWorldText>();depth.shader=worldShader;changed=true;}if(changed)PrefabUtility.SaveAsPrefabAsset(instance,bagPath);}finally{PrefabUtility.UnloadPrefabContents(instance);}}
   var surface=h.GetComponent<NavMeshSurface>();surface.BuildNavMesh();var navPath=Art+"Hotel elevators navigation.asset";if(AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath))AssetDatabase.DeleteAsset(navPath);AssetDatabase.CreateAsset(surface.navMeshData,navPath);EditorUtility.SetDirty(surface);EditorUtility.SetDirty(h);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);HotelEmployeeBuilder.Save();Debug.Log("OPERATIONAL_ELEVATORS_BUILT");
  }
 }
}
