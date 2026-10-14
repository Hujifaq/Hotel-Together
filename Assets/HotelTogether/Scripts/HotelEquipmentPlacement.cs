using UnityEngine;
using UnityEngine.AI;
namespace HotelTogether
{
 public static class HotelEquipmentPlacement
 {
  public static void AttachExisting(HotelItem item)
  {
   var bounds=new Bounds(item.transform.position,Vector3.zero);foreach(var renderer in item.GetComponentsInChildren<MeshRenderer>())bounds.Encapsulate(renderer.bounds);
   float best=.8f;RaycastHit wall=default;
   for(int d=0;d<4;d++)foreach(var hit in Physics.RaycastAll(bounds.center,Quaternion.Euler(0,d*90,0)*Vector3.forward,.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
   {var size=hit.collider.bounds.size;if(hit.distance<best&&Mathf.Abs(hit.normal.y)<.1f&&size.y>1.8f&&Mathf.Min(size.x,size.z)<.65f&&!hit.collider.GetComponentInParent<HotelItem>()&&!hit.collider.attachedRigidbody){best=hit.distance;wall=hit;}}
   if(!wall.collider)return;
   var axis=new Vector3(Mathf.Abs(wall.normal.x),0,Mathf.Abs(wall.normal.z));float gap=Vector3.Dot(bounds.center-wall.point,wall.normal)-Vector3.Dot(bounds.extents,axis);
   if(gap>.015f&&gap<.4f){item.transform.position-=wall.normal*(gap-.01f);item.home=item.transform.position;item.homeRotation=item.transform.rotation;}
  }
  // Local +Z is the back of a cabinet. Local -Z faces its accessible work area.
  public static bool Wall(HotelMaintenanceSystem system,Vector3 preferred,float width,float backDepth,out Vector3 point,out Quaternion rotation)
  {
   point=preferred;rotation=Quaternion.identity;float best=3.1f;
   int type=system.hotel.guestPrefabs[0].GetComponent<NavMeshAgent>().agentTypeID;
   var filter=new NavMeshQueryFilter{agentTypeID=type,areaMask=NavMesh.AllAreas};
   for(int direction=0;direction<4;direction++)
   {
    var ray=Quaternion.Euler(0,direction*90,0)*Vector3.forward;
    foreach(var hit in Physics.RaycastAll(preferred+Vector3.up,ray,3,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
    {
     var c=hit.collider;var size=c.bounds.size;
     if(hit.distance>=best||Mathf.Abs(hit.normal.y)>.1f||size.y<1.8f||Mathf.Min(size.x,size.z)>.65f||Mathf.Max(size.x,size.z)<width+.08f||c.GetComponentInParent<HotelItem>()||c.GetComponentInParent<HotelElevator>()||c.attachedRigidbody)continue;
     var candidate=hit.point+hit.normal*(backDepth+.006f);candidate.y=preferred.y;
     var approach=candidate+hit.normal*.8f;
     if(!NavMesh.SamplePosition(approach,out var nav,.35f,filter)||Mathf.Abs(nav.position.y-preferred.y)>.2f)continue;
     var facing=Quaternion.LookRotation(-hit.normal,Vector3.up);
     if(Physics.CheckBox(candidate+hit.normal*.24f+Vector3.up*.75f,new Vector3(width*.5f,.68f,.20f),facing,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))continue;
     if(Physics.Linecast(approach+Vector3.up*.9f,candidate+hit.normal*.19f+Vector3.up*.8f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))continue;
     // Both rear corners must touch a continuous plane, including coplanar wall segments.
     var tangent=Vector3.Cross(Vector3.up,hit.normal);bool supported=true;
     foreach(float side in new[]{-1f,1f})if(!Physics.Raycast(hit.point+hit.normal*.12f+tangent*width*.5f*side,-hit.normal,out var edge,.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||Vector3.Dot(edge.normal,hit.normal)<.99f||Mathf.Abs(Vector3.Dot(edge.point-hit.point,hit.normal))>.02f||edge.collider.attachedRigidbody||edge.collider.GetComponentInParent<HotelItem>())supported=false;
     if(!supported)continue;
     best=hit.distance;point=candidate;rotation=facing;
    }
   }
   return best<3.1f;
  }
  public static Quaternion FaceOpenArea(HotelMaintenanceSystem system,Vector3 point)
  {
   var target=system.hotel.player?system.hotel.player.transform.position-point:Vector3.back;target.y=0;
   return target.sqrMagnitude>.01f?Quaternion.LookRotation(-target):Quaternion.identity;
  }
 }
}
