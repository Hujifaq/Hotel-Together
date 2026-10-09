using UnityEngine;
namespace HotelTogether {
 public class HotelItem:MonoBehaviour {
  public string displayName,kind,sourceModel;public Vector3 dimensions;public float seatEyeHeight=1.28f;public DoorData door;public Transform[] leaves;public bool open,lampOn=true;public Light lamp;
  [HideInInspector]public Vector3 home;[HideInInspector]public Quaternion homeRotation;bool originalKinematic;float doorAmount;
  void Awake(){home=transform.position;homeRotation=transform.rotation;var rb=GetComponent<Rigidbody>();originalKinematic=rb==null||rb.isKinematic;}
  public void ResetItem(){open=false;doorAmount=0;PoseDoor(0);transform.SetPositionAndRotation(home,homeRotation);var rb=GetComponent<Rigidbody>();if(rb){rb.isKinematic=originalKinematic;if(!rb.isKinematic){rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;}rb.Sleep();}lampOn=true;if(lamp)lamp.enabled=true;}
  void Update(){if(kind!="door"||door==null)return;float next=Mathf.MoveTowards(doorAmount,open?1:0,Time.deltaTime*.85f);if(Mathf.Abs(next-doorAmount)<.0001f)return;if(PoseDoor(next,true)){doorAmount=next;PoseDoor(next);}}
  public bool PoseDoor(float a,bool check=false){if(door==null||door.leaves==null||leaves==null)return true;for(int i=0;i<leaves.Length;i++){var l=door.leaves[i];float angle=door.@base,x,z; if(door.slide){float local=l.closed+l.side*a*(door.w/2+.06f);x=door.x+Mathf.Cos(angle)*local;z=door.z-Mathf.Sin(angle)*local;}else{angle=door.@base-l.side*door.outside*a*Mathf.PI/2;float ax=Mathf.Cos(door.@base)*l.hinge,az=-Mathf.Sin(door.@base)*l.hinge;x=door.x+ax+Mathf.Cos(angle)*(-l.side*l.width/2);z=door.z+az-Mathf.Sin(angle)*(-l.side*l.width/2);}
   var pos=new Vector3(-x,door.height/2,z);var rot=Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0);var basePosition=new Vector3(-door.x,door.height/2,door.z);var baseRotation=Quaternion.Euler(0,-door.@base*Mathf.Rad2Deg,0);var moved=transform.rotation*Quaternion.Inverse(baseRotation);pos=transform.position+moved*(pos-basePosition);rot=moved*rot;if(check){foreach(var c in Physics.OverlapBox(pos,new Vector3(l.width/2-.01f,(door.height-.08f)/2,.025f),rot,~0,QueryTriggerInteraction.Ignore)){if(c.transform==leaves[i]||c.transform.IsChildOf(leaves[i]))continue;if(c.GetComponentInParent<HotelPlayer>()||c.attachedRigidbody&&!c.attachedRigidbody.isKinematic)return false;}}else leaves[i].SetPositionAndRotation(pos,rot);
  }return true;}
 }
 public class DoorLeaf:MonoBehaviour {public HotelItem owner;}
}
