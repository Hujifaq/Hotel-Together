using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using HotelTogether;

public static class HotelLobbyEntranceSetup {
 const string RootName="Lobby automatic sliding entrance";
 [MenuItem("Hotel Together/Environment/Repair lobby sliding entrance")]
 public static void Apply(){
  if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode before repairing the entrance.");
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  var architecture=GameObject.Find("Hotel architecture and furnishings");
  var collision=GameObject.Find("Architecture collision");
  if(!architecture||!collision)throw new System.InvalidOperationException("Hotel architecture roots are missing.");
  string[] sources={"Node-145","Node-146","Node-143","Node-144"};
  foreach(var name in sources)if(!architecture.transform.Find(name))throw new System.InvalidOperationException("Missing entrance visual: "+name);
  foreach(var index in new[]{77,78})if(!collision.transform.Find("Structure collision "+index+"/Collision 0"))throw new System.InvalidOperationException("Missing entrance collider "+index);
  var old=GameObject.Find(RootName);if(old)Undo.DestroyObjectImmediate(old);
  var root=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(root,"Repair lobby sliding entrance");root.transform.position=new Vector3(0,1.7f,7.18f);
  var item=root.AddComponent<HotelItem>();item.kind="door";item.displayName="Lobby Glass Entrance";item.sourceModel="Existing lobby glazing";item.dimensions=new Vector3(4,3.4f,.1f);
  item.door=new DoorData{x=0,z=7.18f,w=4,height=3.4f,slide=true,leaves=new[]{new LeafData{closed=1,side=1,width=1.96f},new LeafData{closed=-1,side=-1,width=1.96f}}};
  item.leaves=new Transform[2];
  for(int side=0;side<2;side++){
   var leaf=new GameObject(side==0?"Left sliding glass leaf":"Right sliding glass leaf");leaf.transform.SetParent(root.transform,false);leaf.transform.localPosition=new Vector3(side==0?-1:1,0,0);item.leaves[side]=leaf.transform;
   leaf.AddComponent<DoorLeaf>().owner=item;
   var box=leaf.AddComponent<BoxCollider>();box.size=new Vector3(1.96f,3.3f,.045f);
   for(int part=0;part<2;part++){
    var source=architecture.transform.Find(sources[side*2+part]).gameObject;
    var visual=Object.Instantiate(source,leaf.transform);visual.name=part==0?"Glass":"Metal edge";
    visual.transform.SetPositionAndRotation(source.transform.position+Vector3.forward*.17f,source.transform.rotation);visual.transform.localScale=source.transform.localScale;
    foreach(var c in visual.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
    foreach(var r in visual.GetComponentsInChildren<Renderer>())r.enabled=true;
    foreach(var r in source.GetComponentsInChildren<Renderer>()){Undo.RecordObject(r,"Replace static entrance glass");r.enabled=false;EditorUtility.SetDirty(r);}
   }
  }
  foreach(var index in new[]{77,78}){
   var c=collision.transform.Find("Structure collision "+index+"/Collision 0").GetComponent<Collider>();Undo.RecordObject(c,"Replace static entrance collision");c.enabled=false;EditorUtility.SetDirty(c);
  }
  var sensor=root.AddComponent<BoxCollider>();sensor.size=new Vector3(4,3.4f,.3f);sensor.isTrigger=true;
  root.AddComponent<HotelAutomaticEntrance>();
  var track=GameObject.CreatePrimitive(PrimitiveType.Cube);track.name="Sliding door overhead track";track.transform.SetParent(root.transform,false);track.transform.localPosition=new Vector3(0,1.75f,0);track.transform.localScale=new Vector3(4.35f,.12f,.26f);
  Object.DestroyImmediate(track.GetComponent<Collider>());track.GetComponent<Renderer>().sharedMaterial=architecture.transform.Find("Node-144").GetComponent<Renderer>().sharedMaterial;
  item.PoseDoor(0);EditorUtility.SetDirty(item);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  Debug.Log("Lobby entrance repaired: automatic sliding glass, moving colliders, both-side approach detection.");
 }
}
