using UnityEngine;
using UnityEngine.AI;
namespace HotelTogether
{
 [RequireComponent(typeof(HotelGuestBot))]
 public sealed class HotelGuestElevatorPassenger : MonoBehaviour
 {
  public HotelElevator Elevator {get;private set;}
  public int SourceFloor {get;private set;}
  public int DestinationFloor {get;private set;}
  public int Slot {get;private set;}
  public bool Active=>Elevator!=null;
  public bool Aboard {get;private set;}
  public bool Boarding {get;private set;}
  public bool Exiting {get;private set;}
  public bool Walking {get;private set;}
  public bool ReadyToBoard=>Active&&!Aboard&&guest.Agent.enabled&&guest.Agent.isOnNavMesh&&Vector3.Distance(transform.position,Elevator.QueuePoint(SourceFloor,Elevator.QueueIndex(this)))<.45f;
  HotelGuestBot guest;Vector3 walkTarget;int queueSlot=-1,walkStage;
  void Awake(){guest=GetComponent<HotelGuestBot>();}
  public bool TryBegin(Vector3 destination)
  {
   if(Aboard)return true;if(Active)CancelTravel();var lifts=guest.Hotel.elevators;if(lifts==null||lifts.Length==0)return false;
   HotelElevator best=null;float score=float.PositiveInfinity;
   foreach(var lift in lifts){if(!lift||!lift.isActiveAndEnabled)continue;int from=lift.FloorFor(transform.position.y),to=lift.FloorFor(destination.y);if(from==to)continue;var path=new NavMeshPath();var point=lift.QueuePoint(from,0);if(!guest.Agent.enabled||!guest.Agent.isOnNavMesh||!guest.Agent.CalculatePath(point,path)||path.status!=NavMeshPathStatus.PathComplete)continue;float candidate=lift.WaitingCount*4+Mathf.Abs(lift.cabin.position.y-transform.position.y)+(lift.service?6:0);if(candidate<score){score=candidate;best=lift;}}
   if(!best)return false;Elevator=best;SourceFloor=best.FloorFor(transform.position.y);DestinationFloor=best.FloorFor(destination.y);queueSlot=-1;Walking=true;return best.Enqueue(this,SourceFloor,DestinationFloor);
  }
  public void Board(int slot){Slot=slot;Aboard=Boarding=Walking=true;guest.Agent.isStopped=true;guest.Agent.enabled=false;walkStage=0;walkTarget=new Vector3(Elevator.transform.position.x,Elevator.cabin.position.y,-11.6f);}
  public void Exit(){Boarding=false;Exiting=Walking=true;walkStage=0;walkTarget=new Vector3(Elevator.transform.position.x,Elevator.cabin.position.y,-11.6f);}
  public void MoveWithCabin(Vector3 delta){if(Aboard)transform.position+=delta;}
  void Update()
  {
   if(!Active||!guest.Hotel||!guest.Hotel.Running)return;
   if(!Aboard){int slot=Elevator.QueueIndex(this);if(slot<0){CancelTravel();guest.ResumeElevatorRoute();return;}if(slot!=queueSlot){queueSlot=slot;var point=Elevator.QueuePoint(SourceFloor,slot);var path=new NavMeshPath();if(guest.Agent.CalculatePath(point,path)&&path.status==NavMeshPathStatus.PathComplete){guest.Agent.isStopped=false;guest.Agent.SetPath(path);}}Walking=!ReadyToBoard;if(ReadyToBoard){guest.Agent.isStopped=true;transform.rotation=Quaternion.Euler(0,180,0);}return;}
   if(!Boarding&&!Exiting){Walking=false;return;}var delta=walkTarget-transform.position;delta.y=0;if(delta.sqrMagnitude>.015f){transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime*8);transform.position=Vector3.MoveTowards(transform.position,walkTarget,1.5f*Time.deltaTime);return;}
   if(walkStage==0){walkStage=1;walkTarget=Exiting?Elevator.ExitPoint(Elevator.CurrentFloor):Elevator.PassengerSlot(Slot);return;}
   if(Exiting){var lift=Elevator;int floor=lift.CurrentFloor;lift.Cancel(this);Elevator=null;Aboard=Boarding=Exiting=Walking=false;RestoreAgent(lift.ExitPoint(floor));var survival=guest.GetComponent<HotelGuestSurvival>();if(survival&&survival.IsTakingCover)survival.OnThreat();else guest.ResumeElevatorRoute();}
   else{Boarding=Walking=false;transform.rotation=Quaternion.identity;}
  }
  void RestoreAgent(Vector3 point){transform.position=point;guest.Agent.enabled=true;guest.Agent.Warp(point);}
  public void CancelTravel(){if(Aboard)return;var lift=Elevator;Elevator=null;Walking=false;queueSlot=-1;if(lift)lift.Cancel(this);}
  public void AbortAt(int floor){var lift=Elevator;if(!lift)return;Elevator=null;Aboard=Boarding=Exiting=Walking=false;RestoreAgent(lift.ExitPoint(floor)+Vector3.right*(Slot%2==0?-.6f:.6f));lift.Cancel(this);}
  void OnDestroy(){if(Elevator)Elevator.Cancel(this);}
 }
}
