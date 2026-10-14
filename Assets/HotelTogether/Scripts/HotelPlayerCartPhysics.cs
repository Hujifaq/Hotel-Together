using System.Collections.Generic;
using UnityEngine;
namespace HotelTogether {
 public partial class HotelPlayer {
  [Header("Cart contact physics")]
  public float cartPushMassLimit=40;
  readonly Collider[] cartContacts=new Collider[64];
  readonly HashSet<Rigidbody> pushedBodies=new();
  bool CanPushCartProp(Rigidbody body)
  {
   if(!body||body.gameObject==cart?.gameObject||body.mass>cartPushMassLimit||body.GetComponentInParent<HotelHuman>()||body.GetComponentInParent<HotelGuestBot>())return false;
   var item=body.GetComponent<HotelItem>();if(!item||item.kind=="cart"||item.kind=="door"||item.kind=="seat")return false;
   var bag=body.GetComponent<HotelGuestDelivery>();if(bag&&bag.Attached)return false;
   var mop=body.GetComponent<HotelCleaningTool>();if(mop&&mop.DockedAt)return false;
   return !body.isKinematic||body.GetComponent<HotelFloorMess>() is HotelFloorMess mess&&mess.type==FloorMessType.Litter&&!mess.Cleared;
  }
  bool PropHasPushRoom(Rigidbody body,Vector3 delta)
  {
   delta.y=0;if(delta.sqrMagnitude<.00001f)return true;
   foreach(var collider in body.GetComponentsInChildren<Collider>())
   {
    if(!collider.enabled||collider.isTrigger||collider.attachedRigidbody!=body)continue;
    var bounds=collider.bounds;var half=Vector3.Max(bounds.extents-Vector3.one*.018f,Vector3.one*.005f);
    int count=Physics.OverlapBoxNonAlloc(bounds.center+delta,half,cartContacts,Quaternion.identity,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore);
    for(int i=0;i<count;i++){var other=cartContacts[i];if(other.attachedRigidbody==body||other.transform.IsChildOf(cart.transform)||other.transform.IsChildOf(transform))continue;if(other.bounds.max.y<=bounds.min.y+.035f)continue;if(!CanPushCartProp(other.attachedRigidbody))return false;}
   }
   return true;
  }
  void PushCartProps(Vector3 desired,Quaternion rotation)
  {
   var delta=desired-cart.transform.position;delta.y=0;pushedBodies.Clear();
   foreach(var box in cart.GetComponentsInChildren<BoxCollider>())
   {
    if(!box.enabled||box.isTrigger||box.GetComponentInParent<HotelItem>()!=cart)continue;
    var local=cart.transform.InverseTransformPoint(box.transform.TransformPoint(box.center));var half=Vector3.Max(Vector3.Scale(box.size,box.transform.lossyScale)*.5f-Vector3.one*.01f,Vector3.one*.005f);var relative=Quaternion.Inverse(cart.transform.rotation)*box.transform.rotation;
    var contacts=Physics.OverlapBox(desired+rotation*local,half,rotation*relative,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore);
    foreach(var contact in contacts)
    {
     var body=contact.attachedRigidbody;if(!CanPushCartProp(body)||!pushedBodies.Add(body))continue;
     if(body.isKinematic){body.isKinematic=false;body.useGravity=true;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.interpolation=RigidbodyInterpolation.Interpolate;}
     body.WakeUp();var targetVelocity=Vector3.ClampMagnitude(delta/Mathf.Max(.001f,Time.fixedDeltaTime),speed);var current=new Vector3(body.linearVelocity.x,0,body.linearVelocity.z);
     var impulse=Vector3.ClampMagnitude((targetVelocity-current)*body.mass*.35f,body.mass*1.25f);
     if(delta.sqrMagnitude>.00001f)body.AddForceAtPosition(impulse,contact.ClosestPoint(desired),ForceMode.Impulse);
    }
   }
  }
  void ResolveCartMovement(ref Vector3 move,ref float heading)
  {
   if(ClearCart(transform.position+move,heading))return;
   if(ClearCart(transform.position+move,yaw)){heading=yaw;return;}
   heading=yaw;var x=new Vector3(move.x,0,0);var z=new Vector3(0,0,move.z);
   if(x.sqrMagnitude>0&&ClearCart(transform.position+x,heading)){move=x;return;}
   if(z.sqrMagnitude>0&&ClearCart(transform.position+z,heading)){move=z;return;}
   float low=0,high=1;for(int i=0;i<6;i++){float mid=(low+high)*.5f;if(ClearCart(transform.position+move*mid,heading))low=mid;else high=mid;}move*=low;
  }
 }
}
