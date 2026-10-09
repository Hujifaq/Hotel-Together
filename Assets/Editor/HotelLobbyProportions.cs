using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HotelTogether;
public static class HotelLobbyProportions {
 const string Root="Assets/HotelTogether/";
 static MigrationData data;
 [MenuItem("HotelTogether/Apply lobby furniture proportions")]
 public static void Apply(){
 if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before changing furniture.");
 ApplyToScene();Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
 }
 public static void ApplyToScene(){
 data=JsonUtility.FromJson<MigrationData>(File.ReadAllText(Root+"Migration.json"));
 Resize("Prop_0021",new Vector3(1.22f,1.10f,1.20f),5.8f,1.3f);
 Resize("Prop_0025",new Vector3(1.30f,1.18f,1.30f),5.8f,3f);
 Resize("Prop_0029",new Vector3(1.20f,1.10f,1.20f),6.7f,4.7f);
 Resize("Prop_0030",new Vector3(1.20f,1.10f,1.20f),4.9f,4.7f);
 Resize("Prop_0037",Vector3.one*1.20f,7.65f,1.3f);
 Resize("Prop_0041",new Vector3(1.28f,1.12f,1.18f));Resize("Prop_0042",new Vector3(1.28f,1.12f,1.18f));
 Resize("Prop_0050",Vector3.one*1.25f);Resize("Prop_0051",Vector3.one*1.25f);
 Resize("Prop_0055",new Vector3(1.15f,1.10f,1.15f));
 OnSupport("Prop_0057","Prop_0041",Vector3.one*1.10f);
 OnSupport("Prop_0058","Prop_0042",Vector3.one*1.15f);
 OnSupport("Prop_0059","Prop_0037",Vector3.one*1.15f);
 OnSupport("Prop_0060","Prop_0025",Vector3.one*1.25f);
 // Reuse the existing meshes/materials for a second small waiting group.
 Clone("Lobby_WaitingChair_A","Prop_0029",new Vector3(3.1f,0,-3.2f),90);
 Clone("Lobby_WaitingChair_B","Prop_0030",new Vector3(5.9f,0,-3.2f),270);
 var table=Clone("Lobby_WaitingTable","Prop_0037",new Vector3(4.5f,0,-3.2f),0);
 var plant=Clone("Lobby_WaitingPlant","Prop_0060",new Vector3(4.5f,0,-3.2f),0);
 var pos=plant.transform.position;pos.y=table.transform.position.y+table.dimensions.y/2+plant.dimensions.y/2+.004f;plant.transform.position=pos;plant.home=pos;EditorUtility.SetDirty(plant);
 }
 static HotelItem Item(string id){var o=GameObject.Find(id);if(!o)throw new Exception("Missing lobby item "+id);return o.GetComponent<HotelItem>();}
 static PropData Original(string id)=>data.props.First(p=>p.id==id);
 static void Resize(string id,Vector3 factor,float? x=null,float? z=null){
 var o=Item(id);var original=Original(id);Undo.RecordObject(o.transform,"Lobby furniture proportions");Undo.RecordObject(o,"Lobby item dimensions");var size=Coordinates.Size(original.size);var oldPos=Coordinates.V(original.body.position);var foot=oldPos.y-size.y/2;var dimensions=Vector3.Scale(size,factor);o.transform.localScale=factor;o.transform.position=new Vector3(x??oldPos.x,foot+dimensions.y/2,z??oldPos.z);o.dimensions=dimensions;o.home=o.transform.position;o.homeRotation=o.transform.rotation;
 if(o.kind=="seat")o.seatEyeHeight=1.28f+(dimensions.y-size.y)*.5f;
 EditorUtility.SetDirty(o);EditorUtility.SetDirty(o.transform);
 }
 static void OnSupport(string id,string support,Vector3 factor){
 var s=Item(support);var baseSupport=Original(support);var baseItem=Original(id);var oldS=Coordinates.V(baseSupport.body.position);var oldI=Coordinates.V(baseItem.body.position);var size=Coordinates.Size(baseItem.size);var oldHeight=Coordinates.Size(baseSupport.size).y;
 Resize(id,factor);var o=Item(id);var local=Quaternion.Inverse(Coordinates.Q(baseSupport.body.rotation))*(oldI-oldS);local=Vector3.Scale(local,s.transform.localScale);var world=s.transform.position+s.transform.rotation*local;
 var gap=oldI.y-size.y/2-(oldS.y+oldHeight/2);world.y=s.transform.position.y+s.dimensions.y/2+o.dimensions.y/2+gap;o.transform.position=world;o.home=world;EditorUtility.SetDirty(o.transform);EditorUtility.SetDirty(o);
 }
 static HotelItem Clone(string name,string source,Vector3 location,float heading){
 var template=Item(source);var go=GameObject.Find(name);if(!go){go=UnityEngine.Object.Instantiate(template.gameObject,template.transform.parent);go.name=name;Undo.RegisterCreatedObjectUndo(go,"Lobby waiting group");}
 var o=go.GetComponent<HotelItem>();Undo.RecordObject(o.transform,"Position lobby waiting group");o.dimensions=template.dimensions;o.seatEyeHeight=template.seatEyeHeight;o.transform.localScale=template.transform.localScale;location.y=.025f+o.dimensions.y/2;o.transform.SetPositionAndRotation(location,Quaternion.Euler(0,heading,0));o.home=location;o.homeRotation=o.transform.rotation;EditorUtility.SetDirty(o);EditorUtility.SetDirty(o.transform);return o;
 }
}
