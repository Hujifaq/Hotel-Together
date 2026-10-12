using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Unity.AI.Navigation;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace HotelTogether.Editor
{
 public static class HotelLobbyCafeBuilder
 {
  const string Art="Assets/HotelTogether/Art/LobbyCafe/", Bags="Assets/HotelTogether/Prefabs/ServiceJobs/";
  static Material walnut,teal,brass,ivory,dark,glass,water,pastry;
  static Material Mat(string name,Color color,float smooth=.25f){var path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;}
  static GameObject Shape(string name,Transform root,Vector3 p,Vector3 size,Material mat,PrimitiveType type=PrimitiveType.Cube,bool solid=false){var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
  static Transform Point(string name,Transform root,Vector3 p){var t=new GameObject(name).transform;t.SetParent(root,false);t.localPosition=p;return t;}
  static TextMesh Label(string name,Transform root,Vector3 p,string text,float size,Color color){var t=Point(name,root,p);t.localRotation=Quaternion.Euler(0,180,0);var s=t.gameObject.AddComponent<TextMesh>();s.text=text;s.font=Resources.Load<Font>("HotelTogetherUI/LilitaOne-Regular");s.fontSize=80;s.characterSize=size;s.anchor=TextAnchor.MiddleCenter;s.color=color;t.GetComponent<MeshRenderer>().sharedMaterial=s.font.material;return s;}
  static void Strip(GameObject go){foreach(var c in go.GetComponentsInChildren<MonoBehaviour>(true).OrderBy(c=>c is HotelGuestDelivery?0:1))Object.DestroyImmediate(c);foreach(var c in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);foreach(var c in go.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(c);foreach(var c in go.GetComponentsInChildren<Component>(true))if(!(c is Transform)&&!(c is MeshFilter)&&!(c is Renderer)&&!(c is TextMesh))Object.DestroyImmediate(c);}
  static Bounds BoundsOf(GameObject go){var rs=go.GetComponentsInChildren<MeshRenderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
  static HotelGuestDelivery Bag(string source)
  {
   string path=Bags+"Guest "+source+".prefab";var saved=AssetDatabase.LoadAssetAtPath<HotelGuestDelivery>(path);
   var original=Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None).FirstOrDefault(i=>i.sourceModel==source&&i.transform.position.y<3);
   if(!original){if(saved)return saved;throw new InvalidOperationException("Missing source suitcase "+source);}
   var go=new GameObject("Tagged "+source);var geometry=Object.Instantiate(original.gameObject);Strip(geometry);geometry.name="Original hotel suitcase geometry";geometry.transform.SetParent(go.transform,false);geometry.transform.localPosition=Vector3.zero;geometry.transform.localRotation=Quaternion.identity;
   var b=BoundsOf(geometry);float scale=.80f/b.size.y;geometry.transform.localScale*=scale;b=BoundsOf(geometry);geometry.transform.localPosition-=b.center-new Vector3(0,.10f,0);b=BoundsOf(geometry);
   var handleRenderer=geometry.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>r.name.Contains("PullHandle"));Point("Guest luggage grip",go.transform,handleRenderer?handleRenderer.bounds.center:new Vector3(0,.50f,0));
   foreach(float side in new[]{-1f,1f}){Shape("Ownership tag plate",go.transform,new Vector3(0,.10f,side*(b.extents.z+.007f)),new Vector3(.20f,.12f,.01f),dark);var tag=Label("Guest room tag",go.transform,new Vector3(0,.10f,side*(b.extents.z+.013f)),"GUEST\nROOM --",.0035f,Color.white);if(side<0)tag.transform.localRotation=Quaternion.identity;}
   var c=go.AddComponent<BoxCollider>();c.center=new Vector3(0,.10f,0);c.size=new Vector3(b.size.x+.015f,.80f,b.size.z+.035f);var rb=go.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=true;rb.mass=3;
   var item=go.AddComponent<HotelItem>();item.kind="pickup";item.sourceModel=source;item.displayName="Tagged guest suitcase";item.dimensions=c.size;var d=go.AddComponent<HotelGuestDelivery>();d.type=GuestDeliveryType.Luggage;
   saved=PrefabUtility.SaveAsPrefabAsset(go,path).GetComponent<HotelGuestDelivery>();Object.DestroyImmediate(go);return saved;
  }
  static void GuestBag(HotelGuestBot prefab,HotelGuestDelivery bag)
  {
   string path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
   try{var v=root.GetComponentInChildren<HotelGuestVisual>();if(v.luggage)Object.DestroyImmediate(v.luggage);var model=Object.Instantiate(bag.gameObject,v.transform);Strip(model);model.name="Guest rolling "+bag.GetComponent<HotelItem>().sourceModel;model.transform.localPosition=new Vector3(.42f,.325f,-.16f);model.transform.localRotation=Quaternion.identity;v.luggage=model;v.luggageHandle=model.transform.Find("Guest luggage grip");v.arrivalLuggagePrefab=bag;PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  static GameObject Cup()
  {
   var go=new GameObject("Guest reusable drinking cup");Shape("Ivory cup",go.transform,new Vector3(0,.05f,0),new Vector3(.07f,.05f,.07f),ivory,PrimitiveType.Cylinder);Shape("Water surface",go.transform,new Vector3(0,.099f,0),new Vector3(.058f,.001f,.058f),water,PrimitiveType.Cylinder);var result=PrefabUtility.SaveAsPrefabAsset(go,"Assets/HotelTogether/Prefabs/ServiceJobs/Guest drinking cup.prefab");Object.DestroyImmediate(go);return result;
  }
  static void Plant(Transform root,HotelItem source,Vector3 p,float height)
  {
   var go=Object.Instantiate(source.gameObject);Strip(go);go.name="Lobby tropical planter";go.transform.SetParent(root,false);go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;var b=BoundsOf(go);go.transform.localScale*=height/b.size.y;b=BoundsOf(go);go.transform.localPosition+=p-new Vector3(b.center.x,b.min.y,b.center.z);
   foreach(var r in go.GetComponentsInChildren<MeshRenderer>())if(r.name.ToLower().Contains("pot"))r.sharedMaterial=brass;
  }
  [MenuItem("Hotel Together/Lobby/Build cafe, drinking water and guest suitcases")]
  public static void Build()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="HotelLobby")throw new InvalidOperationException("Open HotelLobby.");Directory.CreateDirectory(Art);Directory.CreateDirectory("../tmp/lobby-cafe");if(!File.Exists("../tmp/lobby-cafe/HotelLobby-before-cafe.unity"))File.Copy(scene.path,"../tmp/lobby-cafe/HotelLobby-before-cafe.unity");
   walnut=Mat("Cafe walnut",new Color(.28f,.135f,.065f));teal=Mat("Cafe deep teal",new Color(.025f,.20f,.21f));brass=Mat("Cafe brushed brass",new Color(.72f,.50f,.19f),.42f);ivory=Mat("Cafe porcelain",new Color(.95f,.89f,.74f),.5f);dark=Mat("Cafe charcoal",new Color(.025f,.036f,.039f));water=Mat("Drinking water",new Color(.17f,.64f,.77f),.75f);pastry=Mat("Golden pastry",new Color(.76f,.42f,.12f));glass=Mat("Pastry display glass",new Color(.52f,.80f,.86f,.16f),.65f);glass.SetFloat("_Surface",1);glass.SetFloat("_SrcBlend",5);glass.SetFloat("_DstBlend",10);glass.SetFloat("_ZWrite",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;
   var hotel=Object.FindFirstObjectByType<HotelGuestSimulation>();var blue=Bag("Blue-Suitcase");var cream=Bag("CreamSuitcase");for(int i=0;i<hotel.guestPrefabs.Length;i++)GuestBag(hotel.guestPrefabs[i],i%2==0?blue:cream);hotel.serviceJobs.luggagePrefab=blue;foreach(var trolley in hotel.serviceJobs.trolleys){for(int slot=0;slot<trolley.slots.Length;slot++){var p=trolley.slots[slot].localPosition;p.x=slot%2==0?-.30f:.30f;p.z=slot<2?-.215f:.215f;trolley.slots[slot].localPosition=p;}EditorUtility.SetDirty(trolley);}var staging=hotel.transform.Find("Player service job equipment/Luggage staging mat");if(staging){staging.localPosition=new Vector3(-3.13f,.03f,-5.04f);staging.localScale=new Vector3(2.4f,.01f,2);}
   foreach(var item in Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None).Where(i=>new[]{"Blue-Suitcase","CreamSuitcase","RedSuitcase","DuffelBag"}.Contains(i.sourceModel)&&!i.GetComponent<HotelGuestDelivery>()).ToArray())Object.DestroyImmediate(item.gameObject);
   var old=GameObject.Find("Lobby cafe and guest amenities");if(old)Object.DestroyImmediate(old);var root=new GameObject("Lobby cafe and guest amenities").transform;
   var cafe=Point("Coffee tea and hydration bar",root,new Vector3(12.46f,.025f,-8.70f));cafe.rotation=Quaternion.Euler(0,-90,0);
   Shape("Walnut feature backing",cafe,new Vector3(0,1.70f,-.30f),new Vector3(3.9f,3.35f,.06f),dark);
   for(int i=0;i<34;i++)Shape("Vertical walnut slat",cafe,new Vector3(-1.89f+i*.115f,1.70f,-.25f),new Vector3(.065f,3.35f,.05f),walnut);
   Shape("Shallow cafe cabinetry",cafe,new Vector3(0,.48f,.16f),new Vector3(3.85f,.94f,.74f),walnut,PrimitiveType.Cube,true);
   Shape("Ivory worktop",cafe,new Vector3(0,.98f,.17f),new Vector3(3.98f,.08f,.81f),ivory);
   Shape("Brass toe kick",cafe,new Vector3(0,.08f,.541f),new Vector3(3.81f,.10f,.012f),brass);
   for(int i=0;i<6;i++){float x=-1.58f+i*.635f;Shape("Teal cabinet door",cafe,new Vector3(x,.52f,.54f),new Vector3(.59f,.73f,.025f),teal);Shape("Brass cabinet handle",cafe,new Vector3(x+.19f,.72f,.563f),new Vector3(.015f,.13f,.015f),brass);}
   Shape("Cafe menu board",cafe,new Vector3(-.53f,2.38f,-.19f),new Vector3(2.5f,1.23f,.04f),brass);Shape("Cafe menu inset",cafe,new Vector3(-.53f,2.38f,-.16f),new Vector3(2.44f,1.17f,.02f),dark);
   Label("Coffee menu title",cafe,new Vector3(-.53f,2.75f,-.139f),"COFFEE & TEA",.033f,ivory.color);Label("Coffee menu",cafe,new Vector3(-.53f,2.30f,-.139f),"ESPRESSO   /   LATTE\nTEA   /   FRESH PASTRIES\nWELCOME TO YOUR STAY",.011f,ivory.color);
   Shape("Coffee machine",cafe,new Vector3(-1.28f,1.32f,.08f),new Vector3(.52f,.58f,.37f),dark);Shape("Coffee machine brushed face",cafe,new Vector3(-1.28f,1.38f,.276f),new Vector3(.47f,.35f,.03f),brass);Shape("Coffee drip tray",cafe,new Vector3(-1.28f,1.06f,.34f),new Vector3(.49f,.035f,.28f),dark);
   for(int i=0;i<2;i++){float x=-1.4f+i*.23f;Shape("Coffee nozzle",cafe,new Vector3(x,1.23f,.31f),new Vector3(.05f,.04f,.08f),dark);Shape("Coffee cup",cafe,new Vector3(x,1.12f,.35f),new Vector3(.10f,.045f,.10f),ivory,PrimitiveType.Cylinder);}
   Shape("Pastry display base",cafe,new Vector3(-.18f,1.06f,.17f),new Vector3(1.04f,.07f,.60f),walnut);Shape("Display glass front",cafe,new Vector3(-.18f,1.32f,.46f),new Vector3(1.04f,.5f,.015f),glass);Shape("Display glass top",cafe,new Vector3(-.18f,1.57f,.17f),new Vector3(1.04f,.015f,.60f),glass);
   foreach(float x in new[]{-.70f,.34f})Shape("Display glass side",cafe,new Vector3(x,1.32f,.17f),new Vector3(.015f,.5f,.60f),glass);
   for(int i=0;i<6;i++){float x=-.51f+i%3*.32f,z=.06f+i/3*.23f;Shape("Pastry plate",cafe,new Vector3(x,1.11f,z),new Vector3(.25f,.009f,.19f),ivory,PrimitiveType.Cylinder);Shape("Fresh baked bun",cafe,new Vector3(x,1.15f,z),new Vector3(.19f,.10f,.12f),pastry,PrimitiveType.Sphere);}
   Shape("Chilled water dispenser",cafe,new Vector3(1.31f,1.42f,.02f),new Vector3(.42f,.78f,.36f),teal);Shape("Water glass reservoir",cafe,new Vector3(1.31f,1.71f,.02f),new Vector3(.33f,.10f,.31f),glass,PrimitiveType.Cylinder);Shape("Water dispensing tap",cafe,new Vector3(1.31f,1.26f,.34f),new Vector3(.07f,.045f,.44f),brass);Shape("Water drip tray",cafe,new Vector3(1.31f,1.02f,.38f),new Vector3(.36f,.03f,.28f),dark);
   Label("Water dispenser label",cafe,new Vector3(1.31f,1.50f,.213f),"FRESH\nWATER",.007f,ivory.color);Label("Hydration wall sign",cafe,new Vector3(1.31f,2.35f,-.18f),"REFILL\n& REFRESH",.027f,ivory.color);
   foreach(float x in new[]{-1.35f,1.35f}){var lamp=Point("Warm cafe task light",cafe,new Vector3(x,2.75f,.8f)).gameObject.AddComponent<Light>();lamp.type=LightType.Point;lamp.color=new Color(1,.72f,.40f);lamp.intensity=2;lamp.range=3.5f;lamp.shadows=LightShadows.None;}
   var station=cafe.gameObject.AddComponent<HotelGuestWaterStation>();station.approach=Point("Guest water queue front",cafe,new Vector3(1.59f,0,.84f));station.nozzle=Point("Water nozzle",cafe,new Vector3(1.31f,1.245f,.55f));station.cupPosition=Point("Guest filling cup",cafe,new Vector3(1.31f,1.04f,.55f));station.cupPrefab=Cup();var stream=Point("Visible drinking water stream",cafe,Vector3.zero).gameObject.AddComponent<LineRenderer>();stream.sharedMaterial=water;stream.positionCount=2;stream.startWidth=.012f;stream.endWidth=.018f;stream.useWorldSpace=true;stream.enabled=false;station.stream=stream;hotel.waterStation=station;
   var existingPlanter=Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None).FirstOrDefault(i=>i.sourceModel=="PalmPlanter"&&i.transform.position.y<3&&Mathf.Abs(i.transform.position.x-9.65f)<.1f);if(existingPlanter){existingPlanter.transform.position=new Vector3(12.2f,existingPlanter.transform.position.y,-4.35f);EditorUtility.SetDirty(existingPlanter);}
   var plant=Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None).First(i=>i.sourceModel=="PalmPlanter");Plant(root,plant,new Vector3(11.8f,.025f,-10.72f),1.65f);Plant(root,plant,new Vector3(-8.2f,.025f,-5.8f),1.8f);Plant(root,plant,new Vector3(-8.25f,.025f,-.4f),1.65f);
   var map=Point("Reception hotel directory",root,new Vector3(-8.75f,1.9f,-1.55f));map.rotation=Quaternion.Euler(0,90,0);Shape("Directory frame",map,Vector3.zero,new Vector3(.85f,1.43f,.055f),brass);Shape("Directory panel",map,new Vector3(0,0,.04f),new Vector3(.79f,1.37f,.025f),teal);Label("Hotel directory",map,new Vector3(0,0,.058f),"HOTEL MAP\n\nG  LOBBY / DINING\n1  GUEST ROOMS\n2  ROOFTOP\n\nSERVICE LIFT\nSTAIRS A / B",.009f,ivory.color);
   var logo=Point("Hotel Together gallery branding",root,new Vector3(0,7.32f,-10.85f));Label("Hotel Together lettering",logo,Vector3.zero,"H O T E L   T O G E T H E R",.10f,brass.color);foreach(float side in new[]{-1f,1f})Shape("Brand brass rule",logo,new Vector3(side*6.7f,0,0),new Vector3(2.2f,.025f,.025f),brass);
   foreach(var item in Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None).Where(i=>i.transform.position.y<3&&new[]{"Mop","MopBucket","Vacuum"}.Contains(i.sourceModel))){float z=item.sourceModel=="Mop"?-29.5f:item.sourceModel=="MopBucket"?-30.2f:-31f;item.transform.position=new Vector3(24.2f,item.transform.position.y,z);EditorUtility.SetDirty(item);}
   var surface=hotel.GetComponent<NavMeshSurface>();surface.BuildNavMesh();string nav=Art+"Lobby cafe navigation.asset";if(AssetDatabase.LoadAssetAtPath<NavMeshData>(nav))AssetDatabase.DeleteAsset(nav);AssetDatabase.CreateAsset(surface.navMeshData,nav);EditorUtility.SetDirty(surface);EditorUtility.SetDirty(hotel);EditorUtility.SetDirty(hotel.serviceJobs);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);HotelEmployeeBuilder.Save();Debug.Log("LOBBY_CAFE_BUILT");
  }
 }
}




