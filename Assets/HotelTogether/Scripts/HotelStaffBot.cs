using System;using System.Linq;
using UnityEngine;
using UnityEngine.AI;
namespace HotelTogether {
 public enum StaffMotion {Idle,Walk,Pickup,CarryBag,CarryTray,Mop,Serve,ReceiveCash,Repair,Knock,Place,Rescue}
 public enum StaffDuty { All, Reception, Service, Housekeeping, Maintenance, Delivery }
 public sealed class HotelStaffBot:MonoBehaviour {
  public StaffDuty Duty;public bool OnDuty=true;public string StaffName;public int CompletedJobs{get;private set;}public string State{get;private set;}="Ready";
  HotelEconomy economy;NavMeshAgent agent;HotelEmployeeRig avatar;UnityEngine.Object job;HotelGuestSupply supply;HotelGuestDelivery cargo;HotelLaundryMachine washer;Transform carry;GameObject toolVisual;MaintenanceAction shownAction;float search,work,stuck;Vector3 last;bool working;bool cashSettled;HotelCash receivingCash;Vector3 cashOrigin;float cashStart;public StaffMotion Motion{get;private set;}public Vector3 CashHand=>avatar.rightArm[2].position;public void ReceiveCash(HotelCash note,Vector3 point){receivingCash=note;cashOrigin=point;cashStart=Time.time;}public void FinishCash(HotelCash note){if(receivingCash==note){receivingCash=null;cashSettled=note.Settled;}}
  public static HotelStaffBot Create(HotelEconomy owner,int index){
   var employee=owner.Hotel.player?.employee;if(!employee||employee.employees.Length==0||!owner.Hotel.guestPrefabs[0])return null;
   employee.Initialize(owner.Hotel.player);int type=owner.Hotel.guestPrefabs[0].GetComponent<NavMeshAgent>().agentTypeID;var filter=new NavMeshQueryFilter{agentTypeID=type,areaMask=NavMesh.AllAreas};
   if(!NavMesh.SamplePosition(owner.Hotel.receptionPoint+Vector3.right*(2+index*.7f),out var nav,4,filter))return null;
   var go=new GameObject("Hotel staff "+(index+1));go.transform.SetParent(owner.transform);go.transform.position=nav.position;
   var bot=go.AddComponent<HotelStaffBot>();bot.economy=owner;bot.StaffName="Employee "+(index+1);bot.agent=go.AddComponent<NavMeshAgent>();bot.agent.agentTypeID=type;bot.agent.speed=2.1f;bot.agent.radius=.3f;bot.agent.height=1.8f;bot.agent.stoppingDistance=.85f;
   bot.avatar=Instantiate(employee.employees[employee.SelectedIndex].prefab,go.transform);bot.avatar.Initialize(false);foreach(var child in bot.avatar.GetComponentsInChildren<Transform>())child.gameObject.layer=0;HotelWorldText.Create(go.transform,bot.StaffName,new Vector3(0,2.1f,0),.022f,new Color(.4f,.9f,.8f),true);
   bot.carry=new GameObject("Staff carry anchor").transform;bot.carry.SetParent(go.transform,false);bot.carry.localPosition=new Vector3(.25f,1,.55f);
   bot.last=go.transform.position;return bot;
  }
  bool Allows(StaffDuty duty)=>OnDuty&&(Duty==StaffDuty.All||Duty==duty);
  bool Claim(UnityEngine.Object candidate,StaffDuty duty,Vector3 point){if(!candidate||!Allows(duty)||!economy.ClaimStaffJob(candidate,this))return false;var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};var path=new NavMeshPath();if(!NavMesh.SamplePosition(point,out var nav,2,filter)||Mathf.Abs(nav.position.y-point.y)>1.8f||!agent.CalculatePath(nav.position,path)||path.status!=NavMeshPathStatus.PathComplete){economy.ReleaseStaffJob(candidate,this);return false;}job=candidate;agent.isStopped=false;agent.SetPath(path);work=stuck=0;return true;}
  void Search(){
   var h=economy.Hotel;
   foreach(var human in FindObjectsByType<HotelHuman>())if(human.Guest&&human.Downed&&!human.Dead&&h.player.combat.Medicine>0&&Claim(human,StaffDuty.Service,human.transform.position))return;
   foreach(var incident in h.Maintenance.incidents)if(incident&&incident.State==MaintenanceIncidentState.Active&&!(incident.kind==MaintenanceIncidentKind.WaterOutage&&incident.Action==MaintenanceAction.Breaker&&!h.Maintenance.HasPower)&&Claim(incident,StaffDuty.Maintenance,incident.Target+incident.transform.forward*.6f))return;
   foreach(var guest in h.Guests)if(guest&&(guest.AwaitingCheckIn||guest.Need==GuestNeed.CheckOut||guest.Payment.Pending||guest.Need==GuestNeed.Help)&&Claim(guest,StaffDuty.Reception,guest.transform.position))return;
   foreach(var guest in h.Guests)if(guest&&(guest.Need==GuestNeed.Meal||guest.Need==GuestNeed.Water||guest.Need==GuestNeed.Towel)){foreach(var station in h.stations){var item=station.prefab?station.prefab.GetComponent<HotelGuestSupply>():null;if(item&&item.type==guest.Need&&(item.type!=GuestNeed.Meal||item.dish==guest.RequestedDish)&&Claim(guest,StaffDuty.Service,station.point.position))return;}}

   foreach(var item in FindObjectsByType<HotelGuestDelivery>().OrderBy(i=>i.type==GuestDeliveryType.Luggage?0:i.type==GuestDeliveryType.CleanLaundry?1:2))if(item.Owner&&item.GetComponent<HotelItem>().enabled&&!item.Attached&&!item.Trolley&&h.player.held!=item.GetComponent<HotelItem>()&&!item.Owner.CollectingLuggage){
    if(item.type==GuestDeliveryType.Luggage&&!h.serviceJobs.StaffBagAvailable(item))continue;
    if(item.type==GuestDeliveryType.DirtyLaundry&&!Array.Exists(h.serviceJobs.washers,w=>!w.Batch))continue;
    if(Claim(item,StaffDuty.Delivery,item.transform.position))return;
   }
   foreach(var machine in h.serviceJobs.washers)if(machine.Ready&&Claim(machine,StaffDuty.Delivery,machine.transform.position))return;
   foreach(var opening in FindObjectsByType<HotelDefenseOpening>())if(opening.exterior&&(opening.StructureHealth<opening.structureMax||opening.BoardHealth<opening.BoardMaximum)&&h.player.combat.Wood>=2&&Claim(opening,StaffDuty.Maintenance,opening.insidePoint))return;
   foreach(var mess in FindObjectsByType<HotelFloorMess>())if(!mess.Cleared&&!mess.Mopping&&h.player.held!=mess.GetComponent<HotelItem>()&&Claim(mess,StaffDuty.Housekeeping,mess.transform.position))return;
   State="Waiting for available jobs";
  }
  void Release(){if(economy){economy.ReleaseStaffJob(job,this);economy.ReleaseStaffJob(washer,this);}if(job is HotelGuestBot customer&&customer.Payment.StaffRecipient==transform)customer.Payment.StaffRecipient=null;if(receivingCash){receivingCash.Void();receivingCash=null;}cashSettled=false;job=null;if(toolVisual)Destroy(toolVisual);toolVisual=null;shownAction=MaintenanceAction.None;work=stuck=0;working=false;if(supply){Destroy(supply.gameObject);supply=null;}if(cargo){cargo.ReleaseAttachment();cargo=null;}washer=null;}
  public void SetDuty(StaffDuty duty){Release();Duty=duty;State=OnDuty?"Ready":"Off duty";search=0;}
  public void SetOnDuty(bool value){Release();OnDuty=value;State=value?"Ready":"Off duty";search=0;}
  bool Travel(Vector3 point){var path=new NavMeshPath();if(!NavMesh.SamplePosition(point,out var nav,2,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask})||Mathf.Abs(nav.position.y-point.y)>1.8f||!agent.CalculatePath(nav.position,path)||path.status!=NavMeshPathStatus.PathComplete)return false;agent.isStopped=false;agent.SetPath(path);work=0;return true;}
  public void AdvanceAI(float dt){
   if(dt<=0||!float.IsFinite(dt)||!economy||!agent.isOnNavMesh)return;var h=economy.Hotel;if(!h.Running){agent.isStopped=true;return;}
   if(cashSettled){CompletedJobs++;Release();search=Time.time+1;return;}if(!OnDuty){Release();agent.isStopped=true;State="Off duty";return;}
   if(!job){Release();if(Time.time>=search){search=Time.time+2;Search();}return;}
   if(agent.pathStatus==NavMeshPathStatus.PathInvalid){Release();return;}
   if(agent.pathPending)return;if(agent.remainingDistance>1.15f){working=false;State="Walking: "+job.name;h.OpenNearbyDoors(transform.position);stuck+=agent.velocity.sqrMagnitude<.01f?dt:-stuck;if(stuck>18){Release();search=Time.time+4;}return;}
   agent.isStopped=true;working=true;work+=dt;State="Working: "+job.name;
   bool done=false;
   if(job is HotelMaintenanceIncident incident){done=!incident.Unresolved;if(!done&&!incident.StaffWork(transform.position,dt)){Release();return;}done=!incident.Unresolved;}
   else if(job is HotelHuman human){if(work>=4){done=h.player.combat.StaffRescue(human,transform.position);if(!done)Release();}}
   else if(job is HotelDefenseOpening opening){if(work>=5){done=opening.StaffRepair(h.player.combat,transform.position);if(!done)Release();}}
   else if(job is HotelFloorMess mess){mess.StaffWork(transform.position,dt*.7f);done=mess.Cleared;}
   else if(job is HotelGuestBot guest){
    guest.Payment.StaffRecipient=transform;if(guest.Payment.Transferring){if(receivingCash){State="Receiving cash from "+guest.guestName;return;}Release();return;}
    if(guest.AwaitingCheckIn||guest.Payment.Pending||guest.Need==GuestNeed.CheckOut||guest.Need==GuestNeed.Help){if(Vector3.Distance(transform.position,guest.transform.position)>2.4f){if(!Travel(guest.transform.position))Release();return;}if(work>=2){done=guest.Payment.Pending?guest.Payment.CollectStaff(this):guest.StaffServe(transform.position);if(receivingCash||guest.AwaitingCheckIn&&guest.Payment.Pending){work=0;done=false;}else if(!done){Release();return;}}}
    else if(!supply){if(work<.7f)return;foreach(var station in h.stations){var prefab=station.prefab?station.prefab.GetComponent<HotelGuestSupply>():null;if(prefab&&prefab.type==guest.Need&&(prefab.type!=GuestNeed.Meal||prefab.dish==guest.RequestedDish)&&Vector3.Distance(transform.position,station.point.position)<2.5f){supply=Instantiate(prefab,carry);supply.transform.localPosition=Vector3.zero;foreach(var c in supply.GetComponentsInChildren<Collider>())c.enabled=false;var rb=supply.GetComponent<Rigidbody>();if(rb)rb.isKinematic=true;if(!Travel(guest.transform.position))Release();return;}}Release();return;}
    else {if(Vector3.Distance(transform.position,guest.transform.position)>2.4f){if(!Travel(guest.transform.position))Release();return;}if(work>=2)done=guest.StaffServe(transform.position,supply);if(!done&&work>3)Release();}
   }
   else if(job is HotelLaundryMachine machine){cargo=machine.StaffCollect(transform.position);if(!cargo){Release();return;}economy.ReleaseStaffJob(job,this);job=cargo;if(!economy.ClaimStaffJob(job,this)){Release();return;}AttachCargo();Travel(h.serviceJobs.receivers[cargo.Owner.RoomIndex].Doorstep);}
   else if(job is HotelGuestDelivery item){
    if(!item.Owner){Release();return;}
    if(!cargo){if(work<1)return;cargo=item;AttachCargo();
     if(cargo.type==GuestDeliveryType.DirtyLaundry){foreach(var w in h.serviceJobs.washers)if(!w.Batch&&economy.ClaimStaffJob(w,this)){washer=w;break;}if(!washer||!Travel(washer.transform.position)){Release();return;}}
     else if(!Travel(h.serviceJobs.receivers[item.Owner.RoomIndex].Doorstep)){Release();return;}return;
    }
    if(work<2)return;
    if(cargo.type==GuestDeliveryType.DirtyLaundry){var soap=Array.Find(FindObjectsByType<HotelDetergentBottle>(),b=>b.Doses>0);done=washer&&washer.StaffLoad(cargo,soap,transform.position);if(washer)economy.ReleaseStaffJob(washer,this);if(done)cargo=null;else Release();}
    else {var receiver=h.serviceJobs.receivers[item.Owner.RoomIndex];receiver.StaffKnock(transform.position);if(cargo.type==GuestDeliveryType.CleanLaundry&&receiver.Occupant&&receiver.Occupant.Activity!=GuestActivity.AtDoor){if(work>35)Release();return;}done=h.serviceJobs.StaffDeliver(cargo,receiver,transform.position);if(done){receiver.StaffKnock(transform.position);cargo=null;}else Release();}
   }
   if(done&&job is HotelGuestBot paying&&paying.Payment.Pending){supply=null;work=0;done=false;}
   if(done){CompletedJobs++;Release();search=Time.time+1;}
  }
  void Update(){if(economy&&economy.Hotel.Running)AdvanceAI(Time.deltaTime);else if(agent&&agent.isOnNavMesh)agent.isStopped=true;}
  void ShowTool(){var incident=job as HotelMaintenanceIncident;if(job is HotelFloorMess&&!toolVisual){var house=economy.Hotel.GetComponent<HotelHousekeepingSystem>();if(house&&house.carts.Length>0&&house.carts[0].mop){toolVisual=Instantiate(house.carts[0].mop.gameObject,carry);toolVisual.transform.localPosition=Vector3.down*.3f;foreach(var c in toolVisual.GetComponentsInChildren<Collider>())c.enabled=false;var mopBody=toolVisual.GetComponent<Rigidbody>();if(mopBody)mopBody.isKinematic=true;toolVisual.GetComponent<HotelCleaningTool>().enabled=false;}}if(!incident)return;var action=incident.Action;if(action==shownAction)return;if(toolVisual)Destroy(toolVisual);shownAction=action;var kind=action==MaintenanceAction.Spray?MaintenanceToolKind.Extinguisher:action==MaintenanceAction.Screwdriver?MaintenanceToolKind.Screwdriver:MaintenanceToolKind.Wrench;var source=economy.Hotel.Maintenance.tools.Find(t=>t.kind==kind);if(!source)return;toolVisual=Instantiate(source.gameObject,carry);toolVisual.name="Staff equipment";toolVisual.transform.localPosition=Vector3.zero;toolVisual.transform.localRotation=Quaternion.identity;foreach(var c in toolVisual.GetComponentsInChildren<Collider>())c.enabled=false;var body=toolVisual.GetComponent<Rigidbody>();if(body)body.isKinematic=true;toolVisual.GetComponent<HotelItem>().enabled=false;var tool=toolVisual.GetComponent<HotelMaintenanceTool>();tool.Configure(economy.Hotel.Maintenance,kind);tool.enabled=false;}
    void AttachCargo(){var rs=cargo.GetComponentsInChildren<Renderer>().Where(r=>!r.GetComponent<TextMesh>()).ToArray();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);Vector3 grip=cargo.type==GuestDeliveryType.Luggage?new Vector3(bounds.center.x,bounds.max.y-.06f,bounds.center.z):bounds.center;var offset=cargo.transform.position-grip;cargo.AttachTo(carry,offset,Quaternion.identity);}
  void LateUpdate(){
   if(!avatar||!economy.Hotel.Running)return;float dt=Time.deltaTime;float speed=(transform.position-last).magnitude/Mathf.Max(dt,.001f);last=transform.position;
   if(working&&job is HotelGuestBot facing&&Vector3.Distance(transform.position,facing.transform.position)<2.5f){var direction=facing.transform.position-transform.position;direction.y=0;if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),dt*7);}Vector3 local=new Vector3(0,1.03f,.40f);Motion=working?StaffMotion.Repair:speed>.05f?StaffMotion.Walk:StaffMotion.Idle;
   if(cargo){Motion=cargo.type==GuestDeliveryType.Luggage?StaffMotion.CarryBag:StaffMotion.CarryTray;local=cargo.type==GuestDeliveryType.Luggage?new Vector3(.40f,.94f,.17f):local;}
   if(supply){Motion=working?StaffMotion.Serve:StaffMotion.CarryTray;if(working)local.z+=Mathf.Sin(Mathf.Clamp01(work/2)*Mathf.PI)*.13f;}
   if(working&&job is HotelGuestDelivery&&!cargo){Motion=StaffMotion.Pickup;local=new Vector3(.22f,.63f,.43f);}
   if(working&&job is HotelLaundryMachine){Motion=StaffMotion.Pickup;local=new Vector3(.20f,.75f,.45f);}
   if(cargo&&working&&work>.7f){Motion=StaffMotion.Place;local.y-=Mathf.Clamp01((work-.7f)/1.3f)*.14f;local.z+=.1f;}carry.SetPositionAndRotation(transform.TransformPoint(local),transform.rotation);
   Vector3 right=carry.position+transform.right*.17f,left=carry.position-transform.right*.17f;
   if(cargo&&cargo.type==GuestDeliveryType.Luggage)right=carry.position;if(cargo&&working&&work<.65f){Motion=StaffMotion.Knock;left=transform.TransformPoint(new Vector3(-.2f,1.28f,.43f+Mathf.Sin(work*28)*.025f));}
   if(working){ShowTool();if(job is HotelFloorMess){Motion=StaffMotion.Mop;if(toolVisual){toolVisual.transform.SetPositionAndRotation(transform.position+transform.forward*(.5f+Mathf.Sin(Time.time*4)*.16f),transform.rotation);var rs=toolVisual.GetComponentsInChildren<Renderer>();float bottom=float.PositiveInfinity;foreach(var r in rs)bottom=Mathf.Min(bottom,r.bounds.min.y);toolVisual.transform.position+=Vector3.up*(transform.position.y+.025f-bottom);right=toolVisual.transform.position+Vector3.up*.9f;left=right+Vector3.up*.18f-transform.forward*.12f;}}else if(job is HotelHuman){Motion=StaffMotion.Rescue;right=transform.TransformPoint(new Vector3(.2f,.64f,.43f));left=right-transform.right*.35f;}else if(job is HotelMaintenanceIncident site){right=transform.position+Vector3.ClampMagnitude(site.Target-(transform.position+Vector3.up*1.15f),.55f)+Vector3.up*1.15f;right+=transform.up*Mathf.Sin(Time.time*7)*.02f;}}
   if(receivingCash){Motion=StaffMotion.ReceiveCash;float t=Mathf.Clamp01((Time.time-cashStart)/.65f);right=Vector3.Lerp(cashOrigin,transform.TransformPoint(new Vector3(.25f,1.1f,.36f)),t);}
   else if(job is HotelGuestBot customer&&working&&!supply){Motion=customer.AwaitingCheckIn||customer.Payment.Pending?StaffMotion.Serve:StaffMotion.Pickup;right=Vector3.Lerp(transform.TransformPoint(new Vector3(.3f,.9f,.1f)),customer.transform.position+Vector3.up*1.05f,Mathf.Clamp01(work/2));}
   avatar.PoseStaffAction(Mathf.Min(speed,3),Motion,left,right,dt);
   if(toolVisual&&Motion!=StaffMotion.Mop)toolVisual.transform.SetPositionAndRotation(avatar.rightArm[2].position,transform.rotation*Quaternion.Euler(0,0,Mathf.Sin(Time.time*7)*20));
   if(supply){supply.transform.position=(avatar.rightArm[2].position+avatar.leftArm[2].position)*.5f;supply.transform.rotation=transform.rotation;}
   if(toolVisual&&job is HotelMaintenanceIncident fire&&fire.kind==MaintenanceIncidentKind.Fire){var extinguisher=toolVisual.GetComponent<HotelMaintenanceTool>();if(extinguisher)extinguisher.SetSpraying(working,(fire.Target-extinguisher.nozzle.position).normalized,dt);}if(cargo)cargo.SyncAttachment();
  }void OnDisable(){Release();}void OnDestroy(){Release();}
 }
}
