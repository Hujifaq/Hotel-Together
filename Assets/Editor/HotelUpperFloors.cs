using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HotelTogether;

// Coordinates come from the approved 56 x 40 plan. Unity mirrors the imported web model's X.
public static class HotelUpperFloors {
 const string Root="Assets/HotelTogether/";
 [Serializable] public class Door {public string side;public float p,width;}
 [Serializable] public class Room {public string id,label,cat;public float x,y,w,h;public Door[] doors;}
 [Serializable] public class Plan {public Room[] upper,roof;}
 class Edge {public bool horizontal;public float c;public List<Vector2> ranges=new();public List<Vector2> cuts=new();}
 static Transform parent;static Material cream,teal,stone,carpet,gold,glass,glow,labelMaterial;static int serial;
 public static Vector3 P(float x,float y,float z)=>new Vector3(29-x,y,z-33);
 static Material Mat(string name,Color color,float smooth=.25f){Directory.CreateDirectory(Root+"Art/Upper");var path=Root+"Art/Upper/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;}
 [MenuItem("HotelTogether/Apply empty upper floors and complete stairs")]
 public static void Apply(){
  if(EditorApplication.isPlaying)throw new Exception("Stop Play mode before editing the building.");
  Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Build empty upper floors");
  cream=Mat("Warm plaster",new Color(.85f,.80f,.69f));teal=Mat("Teal structure",new Color(.16f,.34f,.36f));stone=Mat("Roof stone",new Color(.56f,.58f,.55f));carpet=Mat("Upper floor sand",new Color(.64f,.56f,.43f));gold=Mat("Rail bronze",new Color(.56f,.40f,.18f),.5f);gold.SetFloat("_Metallic",.55f);
  glass=Mat("Skylight glass",new Color(.46f,.63f,.66f,.42f),.7f);glass.SetFloat("_Surface",1);glass.SetFloat("_SrcBlend",5);glass.SetFloat("_DstBlend",10);glass.SetFloat("_ZWrite",0);glass.SetFloat("_Cull",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;
  var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");labelMaterial=Mat("World labels",new Color(.08f,.2f,.21f));labelMaterial.shader=Shader.Find("Universal Render Pipeline/Unlit");labelMaterial.SetTexture("_BaseMap",font.material.mainTexture);labelMaterial.SetColor("_BaseColor",new Color(.08f,.2f,.21f));labelMaterial.SetFloat("_AlphaClip",1);labelMaterial.SetFloat("_Cutoff",.1f);labelMaterial.EnableKeyword("_ALPHATEST_ON");labelMaterial.SetFloat("_Cull",2);labelMaterial.renderQueue=2450;
  glow=Mat("Warm ceiling strip",new Color(1,.85f,.58f));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1,.7f,.34f)*2);
  var old=GameObject.Find("Upper floors and rooftop");if(old)Undo.DestroyObjectImmediate(old);
  var root=new GameObject("Upper floors and rooftop");Undo.RegisterCreatedObjectUndo(root,"Upper architecture");parent=root.transform;serial=0;
  ReplaceLegacyUpper();
  foreach(string id in new[]{"Prop_0009","Prop_0014"}){var g=GameObject.Find(id);if(g){var door=g.GetComponent<HotelItem>();Undo.RecordObject(door,"Stair door opens into corridor");door.door.outside=-1;EditorUtility.SetDirty(door);}}
  var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Root+"UpperPlan.json"));
  var upper=new GameObject("Level 2 - empty rooms 201-216");upper.transform.SetParent(parent);parent=upper.transform;
  Floor(4.5f,false);Walls(plan.upper,4.5f,4.5f);RoomSigns(plan.upper,4.5f);
  // A continuous guard at each accessible edge of the double-height lobby.
  Rail(20,26,20,40,4.5f);Rail(20,26,38,26,4.5f);Rail(38,26,38,40,4.5f);
  Box("Lobby front upper header",29,8.55f,40,18,.9f,.2f,cream);
  foreach(var r in plan.upper.Where(r=>r.cat!="stair"&&r.cat!="lift")){
   Box(r.id+" ceiling strip",r.x+r.w/2,8.72f,r.y+r.h/2,Mathf.Min(2.8f,r.w*.5f),.06f,.16f,glow,false);
  }
  foreach(var v in new[]{new Vector3(14,7.5f,10),new Vector3(18,7.5f,24),new Vector3(44,7.5f,24),new Vector3(33,7.5f,10)})LightAt(v.x,v.y,v.z);
  parent=root.transform;var roof=new GameObject("Level 3 - empty rooftop zones");roof.transform.SetParent(parent);parent=roof.transform;
  Floor(9,true);Walls(plan.roof.Where(r=>r.cat=="service"||r.cat=="stair"||r.cat=="lift").ToArray(),9,3);RoomSigns(plan.roof,9);
  foreach(var r in plan.roof.Where(r=>r.cat=="service"||r.cat=="stair"||r.cat=="lift"))Box(r.id+" roof cap",r.x+r.w/2,12.1f,r.y+r.h/2,r.w,.2f,r.h,cream);
  var shelter=plan.roof.First(r=>r.id=="R02");Box("Empty roof shelter canopy",22,12.2f,4,12,.2f,8,cream);
  foreach(float x in new[]{16.25f,27.75f})foreach(float z in new[]{.25f,7.75f})Box("Shelter column",x,10.6f,z,.22f,3.2f,.22f,teal);
  Box("Skylight - not a walking floor",29,9.16f,33,17.8f,.12f,13.8f,glass,true);
  for(float x=20;x<=38;x+=3)Box("Skylight mullion",x,9.24f,33,.07f,.09f,14,teal,false);
  for(float z=26;z<=40;z+=3.5f)Box("Skylight crossbeam",29,9.24f,z,18,.09f,.07f,teal,false);
  Rail(20,26,38,26,9);Rail(20,26,20,40,9);Rail(38,26,38,40,9);
  Rail(0,0,56,0,9);Rail(0,0,0,40,9);Rail(56,0,56,40,9);Rail(0,40,56,40,9);
  parent=root.transform;Stairs("A",12,22,false);Stairs("B",38,16,true);
  // Far clipping must include the 56-m-wide hotel when viewing from the roof.
  var camera=GameObject.Find("Player camera").GetComponent<Camera>();Undo.RecordObject(camera,"Upper floor camera range");camera.farClipPlane=Mathf.Max(camera.farClipPlane,120);
  Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  Undo.CollapseUndoOperations(Undo.GetCurrentGroup());Debug.Log("HOTEL_UPPER_BUILT rooms=16 stairs=A+B floors=0/4.5/9 empty=true");
 }
 static void ReplaceLegacyUpper(){
  var imported=GameObject.Find("Hotel architecture and furnishings");
  foreach(var r in imported.GetComponentsInChildren<Renderer>(true)){
   if(r.GetComponentInParent<HotelItem>()||!r.name.StartsWith("Node-"))continue;
   if(!int.TryParse(r.name.Substring(5),out int n))continue;
   // Old slabs, partial gallery, flights, rails and roof are replaced, not overlaid.
   if((n>=87&&n<=93)||(n>=184&&n<=326)||n==170||n==171){Undo.RecordObject(r,"Replace old upper surface");r.enabled=false;}
  }
  var physics=GameObject.Find("Architecture collision");foreach(Transform t in physics.transform){if(int.TryParse(t.name.Replace("Structure collision ",""),out int n)&&n>=89&&n<=116)foreach(var c in t.GetComponentsInChildren<Collider>()){Undo.RecordObject(c,"Replace old upper collision");c.enabled=false;}}
 }
 static bool Hole(float x,float z)=> (x>=20&&x<38&&z>=26&&z<40)||(x>=12&&x<16&&z>=22&&z<28)||(x>=38&&x<42&&z>=16&&z<22);
 static void Floor(float y,bool roof){
  // Non-overlapping row strips: one slab serves as ceiling below and walking floor above.
  for(int z=0;z<40;z++){int begin=-1;for(int x=0;x<=56;x++){bool fill=x<56&&!Hole(x+.5f,z+.5f);if(fill&&begin<0)begin=x;if(!fill&&begin>=0){Box((roof?"Roof slab ":"Level 2 slab ")+z+"/"+begin,(begin+x)*.5f,y-.09f,z+.5f,x-begin,.18f,1,roof?stone:carpet);begin=-1;}}}
 }
 static List<Vector2> Merge(List<Vector2> a){a=a.OrderBy(v=>v.x).ToList();var b=new List<Vector2>();foreach(var v in a){if(b.Count>0&&v.x<=b[b.Count-1].y+.001f){var q=b[b.Count-1];q.y=Mathf.Max(q.y,v.y);b[b.Count-1]=q;}else b.Add(v);}return b;}
 static void Walls(Room[] rooms,float floor,float height){
  var edges=new Dictionary<string,Edge>();
  Action<bool,float,float,float> add=(h,c,a,b)=>{string key=(h?"H":"V")+c; if(!edges.TryGetValue(key,out var e)){e=new Edge{horizontal=h,c=c};edges[key]=e;}e.ranges.Add(new Vector2(a,b));};
  foreach(var r in rooms){add(true,r.y,r.x,r.x+r.w);add(true,r.y+r.h,r.x,r.x+r.w);add(false,r.x,r.y,r.y+r.h);add(false,r.x+r.w,r.y,r.y+r.h);}
  if(floor<8){add(true,0,0,56);add(true,40,0,20);add(true,40,38,56);add(false,0,0,40);add(false,56,0,40);}
  foreach(var r in rooms)foreach(var d in r.doors??Array.Empty<Door>()){
   bool h=d.side=="N"||d.side=="S";float c=d.side=="N"?r.y:d.side=="S"?r.y+r.h:d.side=="W"?r.x:r.x+r.w;
   edges[(h?"H":"V")+c].cuts.Add(new Vector2(d.p-d.width/2,d.p+d.width/2));
  }
  foreach(var e in edges.Values)foreach(var range in Merge(e.ranges)){
   float at=range.x;foreach(var cut in Merge(e.cuts)){if(cut.y<=range.x||cut.x>=range.y)continue;float lo=Mathf.Max(range.x,cut.x),hi=Mathf.Min(range.y,cut.y);if(lo>at)WallPiece(e,at,lo,floor,height);float lintel=height-2.5f; if(lintel>0)WallPiece(e,lo,hi,floor+2.5f,lintel);at=Mathf.Max(at,hi);}if(at<range.y)WallPiece(e,at,range.y,floor,height);
  }
 }
 static void WallPiece(Edge e,float a,float b,float floor,float h){if(b-a<.01f)return;Box("Partition "+(e.horizontal?"H":"V")+e.c+"/"+a,e.horizontal?(a+b)/2:e.c,floor+h/2,e.horizontal?e.c:(a+b)/2,e.horizontal?b-a:.18f,h,e.horizontal?.18f:b-a,cream);}
 static GameObject Box(string name,float x,float y,float z,float w,float h,float d,Material m,bool collision=true){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent);g.transform.position=P(x,y,z);g.transform.localScale=new Vector3(w,h,d);g.GetComponent<Renderer>().sharedMaterial=m;if(!collision)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.BatchingStatic);return g;}
 static void Rail(float x,float z,float xx,float zz,float floor){bool alongX=Mathf.Abs(xx-x)>.01f;float len=alongX?Mathf.Abs(xx-x):Mathf.Abs(zz-z);Box("Guardrail",(x+xx)/2,floor+.57f,(z+zz)/2,alongX?len:.075f,1.14f,alongX?.075f:len,glass);Box("Guardrail bronze cap",(x+xx)/2,floor+1.17f,(z+zz)/2,alongX?len:.09f,.07f,alongX?.09f:len,gold,false);int n=Mathf.CeilToInt(len/2);for(int i=0;i<=n;i++)Box("Guardrail post",Mathf.Lerp(x,xx,(float)i/n),floor+.58f,Mathf.Lerp(z,zz,(float)i/n),.06f,1.16f,.06f,teal,false);}
 static void RoomSigns(Room[] rooms,float floor){foreach(var r in rooms){
  if(r.cat=="outside"||r.cat=="shelter"){Label(r.id+" / "+r.label,r.x+r.w/2,floor+.12f,r.y+r.h/2,0,true);continue;}
  foreach(var d in r.doors??Array.Empty<Door>()){float x=d.side=="N"||d.side=="S"?d.p:d.side=="W"?r.x:r.x+r.w;float z=d.side=="W"||d.side=="E"?d.p:d.side=="N"?r.y:r.y+r.h;var sign=Label(r.id+" / "+r.label,x,floor+2.88f,z,d.side=="N"?0:d.side=="E"?90:d.side=="W"?-90:180,false);sign.transform.position-=sign.transform.forward*.11f;}
 }}
 static GameObject Label(string s,float x,float y,float z,float yaw,bool ground){var g=new GameObject(s+" label");g.transform.SetParent(parent);g.transform.position=P(x,y,z);g.transform.rotation=ground?Quaternion.Euler(90,0,0):Quaternion.Euler(0,yaw,0);var t=g.AddComponent<TextMesh>();t.text=s;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=48;t.characterSize=.075f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=ground?new Color(.15f,.28f,.29f):new Color(.08f,.2f,.21f);g.GetComponent<Renderer>().sharedMaterial=labelMaterial;return g;}
 static void LightAt(float x,float y,float z){var l=new GameObject("Upper corridor warm fill").AddComponent<Light>();l.transform.SetParent(parent);l.transform.position=P(x,y,z);l.type=LightType.Point;l.color=new Color(1,.81f,.55f);l.range=13;l.intensity=5;l.shadows=LightShadows.None;}
 static void Stairs(string id,float x,float z,bool reverse){
  var saved=parent;var root=new GameObject("Stair "+id+" - ground to roof");root.transform.SetParent(parent);parent=root.transform;
  // 4 x 6 m core: two 1.4 m flights, a side access passage and 1 m end landings.
  float front=reverse?z+1.2f:z+4.8f,back=reverse?z+4.8f:z+1.2f,frontCentre=reverse?z+.65f:z+5.35f,backCentre=reverse?z+5.35f:z+.65f;
  for(int level=0;level<3;level++){
   float baseY=level*4.5f;
   if(level>0)Box(id+" level "+level+" front landing",x+1.63f,baseY-.09f,frontCentre,3.06f,.18f,1.1f,stone);
   if(level>0)Box(id+" level "+level+" access passage",x+3.53f,baseY-.09f,z+3,.74f,.18f,5.8f,stone);
   // Side passage guard keeps players out of the flight opening without blocking the doors.
   Box(id+" passage guard "+level,x+3.12f,baseY+.57f,z+3,.06f,1.14f,3.6f,glass);
   if(level==2)continue;
   Flight(id+" lower "+level,x+2.35f,front,back,baseY,baseY+2.25f);
   Box(id+" half landing "+level,x+1.63f,baseY+2.16f,backCentre,3.06f,.18f,1.1f,stone);
   Flight(id+" upper "+level,x+.85f,back,front,baseY+2.25f,baseY+4.5f);
  }
  parent=saved;
 }
 static void Flight(string name,float x,float from,float to,float low,float high){
  float run=Mathf.Abs(to-from),rise=high-low;int n=15;
  for(int i=0;i<n;i++){float t=(i+.5f)/n;Box(name+" tread "+i,x,low+rise*(i+1)/n-.065f,Mathf.Lerp(from,to,t),1.4f,.13f,run/n,teal,false);}
  // Actual walking collider is a smooth wedge. Top follows the risers with no stepped jitter.
  var points=new[]{P(x-.7f,low-.18f,from),P(x+.7f,low-.18f,from),P(x-.7f,high-.18f,to),P(x+.7f,high-.18f,to),P(x-.7f,low,from),P(x+.7f,low,from),P(x-.7f,high,to),P(x+.7f,high,to)};
  int[] tris={4,6,5,5,6,7,0,1,2,1,3,2,0,4,1,1,4,5,2,3,6,3,7,6,0,2,4,2,6,4,1,5,3,3,5,7};
  if(to>from)for(int i=0;i<tris.Length;i+=3){int temp=tris[i+1];tris[i+1]=tris[i+2];tris[i+2]=temp;}
  var mesh=new Mesh{name=name+" walk wedge"};mesh.vertices=points;mesh.triangles=tris;mesh.RecalculateNormals();mesh.RecalculateBounds();string path=Root+"Art/Upper/"+name.Replace(" ","_")+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
  var ramp=new GameObject(name+" smooth collision");ramp.transform.SetParent(parent);ramp.AddComponent<MeshCollider>().sharedMesh=mesh;
  foreach(float dx in new[]{-.73f,.73f}){var rail=Box(name+" handrail",x+dx,(low+high)/2+1.05f,(from+to)/2,.065f,.065f,Mathf.Sqrt(run*run+rise*rise),gold,true);for(int i=0;i<=4;i++)Box(name+" rail post",x+dx,Mathf.Lerp(low,high,i/4f)+.52f,Mathf.Lerp(from,to,i/4f),.055f,1.04f,.055f,gold,false);rail.transform.rotation=Quaternion.Euler(from>to?Mathf.Atan2(rise,run)*Mathf.Rad2Deg:-Mathf.Atan2(rise,run)*Mathf.Rad2Deg,0,0);}
 }
}
