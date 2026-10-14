using UnityEngine;
namespace HotelTogether {
 [DefaultExecutionOrder(50)] public sealed class HotelGuestPayment:MonoBehaviour {
  public HotelGuestBot Guest{get;private set;}HotelCash cash;float blockedUntil;public Transform StaffRecipient;
  public bool Pending=>cash&&cash.Offered;public bool Transferring=>cash&&cash.Collecting;public bool TipsBlocked=>Time.time<blockedUntil;public int Amount=>cash?cash.Amount:0;
  public void Initialize(HotelGuestBot g){Guest=g;}
  public bool Offer(int rent,int tip){var economy=Guest&&Guest.Hotel?Guest.Hotel.Economy:null;if(!economy||!economy.Initialized||rent+tip<=0||Guest.GetComponent<HotelHuman>()?.Dead==true||Guest.ReceptionExpired||Guest.PrankPhysics&&Guest.PrankPhysics.Suspended)return false;if(Transferring)return false;if(Pending){cash.Add(rent,tip);return true;}var prefab=Resources.Load<HotelCash>("HotelTogetherCash/Cash payment");if(!prefab){Debug.LogError("Physical cash prefab missing. Build the supplied cash asset.");return false;}cash=Instantiate(prefab,Guest.transform);cash.Initialize(this,economy,rent,tip);Guest.StopForDanger();PoseOffering(.001f);Guest.Hotel.player?.Notify(Guest.guestName+" offers $"+cash.Amount+". Click the cash to accept.");return true;}
  public bool CollectStaff(HotelStaffBot staff)=>Pending&&cash.BeginStaffCollect(staff);
  public bool CollectStaff(Vector3 position)=>Pending&&cash.CollectStaff(position);
  public bool Collect(HotelPlayer p)=>Pending&&cash.TryCollect(p);
  public bool CanSettle(HotelCash note)=>cash==note&&Guest&&!Guest.ReceptionExpired&&Guest.GetComponent<HotelHuman>()?.Targetable!=false&&!(Guest.PrankPhysics&&Guest.PrankPhysics.Suspended)&&(note.Rent<=0||Guest.CanCompleteCheckIn(note));
  public void Accepted(HotelCash note){/* Retain ownership until transfer settles or is cancelled. */}
  public void Settled(HotelCash note){if(cash!=note)return;cash=null;if(note.Rent>0)Guest.CompleteCheckInPayment(note);}
  public void Cancelled(HotelCash note){if(cash!=note)return;cash=null;if(note.Rent>0)Guest?.CancelCheckInPayment();}
  public void Withdraw(){blockedUntil=Time.time+60;var note=cash;cash=null;if(note){if(note.Rent>0)Guest?.CancelCheckInPayment();note.Void();}}
  void LateUpdate(){if(Guest&&Guest.Hotel.Running)PoseOffering(Time.deltaTime);}
  public void PoseOffering(float dt){if(dt<=0||!Pending||!Guest||!Guest.visual)return;if(Guest.PrankPhysics&&Guest.PrankPhysics.Suspended){Withdraw();return;}var player=Guest.Hotel.player;var forward=StaffRecipient?StaffRecipient.position-Guest.transform.position:player?player.transform.position-Guest.transform.position:Guest.transform.forward;forward.y=0;if(forward.sqrMagnitude>.1f)Guest.transform.rotation=Quaternion.Slerp(Guest.transform.rotation,Quaternion.LookRotation(forward),dt*6);if(Guest.Agent&&Guest.Agent.enabled&&Guest.Agent.isOnNavMesh)Guest.Agent.isStopped=true;var v=Guest.visual;v.Pose(0,dt,Guest.IsSeated,Guest.IsSleeping,false,Guest.Satisfaction,false);var point=v.rightArm.position+Guest.transform.forward*.38f+Guest.transform.right*.08f+Vector3.down*.12f;v.HoldCup(point);cash.transform.SetPositionAndRotation(point+Guest.transform.forward*.19f+Guest.transform.right*.025f+Vector3.up*.035f,Guest.transform.rotation*Quaternion.AngleAxis(90,Vector3.forward)*Quaternion.Euler(65,0,0));}
  void OnDestroy(){if(cash)cash.Void();}
 }
}
