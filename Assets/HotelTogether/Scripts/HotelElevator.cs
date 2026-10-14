using System;
using System.Collections.Generic;
using UnityEngine;
namespace HotelTogether
{
 public enum ElevatorState { Open, Closing, Moving, Opening, Closed }
 public sealed class HotelElevator : MonoBehaviour
 {
  [Serializable] public sealed class Landing {public string label;public float height;public Transform[] doors;public TextMesh display;public Vector3 waitingPoint;}
  public HotelGuestSimulation hotel;
  public Transform cabin;
  public Transform[] cabinDoors;
  public Landing[] floors;
  public bool service;
  public AudioSource audioSource,motor;
  public TextMesh cabinDisplay;
  public float speed=1.8f,doorSeconds=1.1f,dwellSeconds=3;
  public ElevatorState State {get;private set;}
  public int CurrentFloor {get;private set;}
  public int TargetFloor {get;private set;}
  public float DoorAmount {get;private set;}=0;
  public int TripsCompleted {get;private set;}
  public int WaitingCount=>waiting.Count;
  public int PassengerCount=>passengers.Count;
  public bool MechanicalFault {get;private set;}
  public bool HasPower {get;private set;}=true;
  public bool Operational=>isActiveAndEnabled&&!MechanicalFault&&HasPower;
  public bool ReadyAt(int floor)=>Operational&&State==ElevatorState.Open&&CurrentFloor==floor&&DoorAmount>.99f;
  readonly List<HotelGuestElevatorPassenger> waiting=new(),passengers=new();
  readonly List<Rigidbody> cargo=new();
  readonly Dictionary<Rigidbody,(bool kinematic,RigidbodyInterpolation interpolation)> cargoModes=new();
  void ReleaseCargo(){foreach(var pair in cargoModes)if(pair.Key){pair.Key.isKinematic=pair.Value.kinematic;pair.Key.interpolation=pair.Value.interpolation;}cargoModes.Clear();cargo.Clear();}
  readonly Collider[] sensor=new Collider[48];
  bool[] requests;
  int[] hallDirections;
  float dwell,travelTime,travelDuration,startHeight;
  int direction=1;
  bool manualClose;
  HotelPlayer ridingPlayer;
  AudioClip chime,motorClip;
  void Awake(){requests=new bool[floors.Length];hallDirections=new int[floors.Length];CurrentFloor=TargetFloor=0;State=ElevatorState.Closed;SetHeight(floors[0].height);PoseDoors();BuildAudio();UpdateDisplays();}
  public int FloorFor(float y){int best=0;for(int i=1;i<floors.Length;i++)if(Mathf.Abs(y-floors[i].height)<Mathf.Abs(y-floors[best].height))best=i;return best;}
  public bool Contains(Vector3 foot){var p=cabin.InverseTransformPoint(foot);return Mathf.Abs(p.x)<1.53f&&p.z> -1.83f&&p.z<1.83f&&Mathf.Abs(p.y)<.7f;}
  public Vector3 ExitPoint(int floor)=>new Vector3(transform.position.x,floors[floor].height,-9.45f);
  public Vector3 QueuePoint(int floor,int slot)=>ExitPoint(floor)+new Vector3(slot%2==0?-.65f:.65f,0,slot/2*.85f);
  public Vector3 PassengerSlot(int slot)=>cabin.position+new Vector3(slot%2==0?-.72f:.72f,0,slot<2?-1.1f:-.12f);
  public int QueueIndex(HotelGuestElevatorPassenger passenger)=>waiting.IndexOf(passenger);
  public bool Enqueue(HotelGuestElevatorPassenger passenger,int source,int destination){if(!Operational||source==destination)return false;if(!waiting.Contains(passenger))waiting.Add(passenger);RequestFloor(source);return true;}
  public void Cancel(HotelGuestElevatorPassenger passenger){waiting.Remove(passenger);passengers.Remove(passenger);}
  public bool RequestFloor(int floor){if(!Operational||floor<0||floor>=floors.Length)return false;requests[floor]=true;if(floor==CurrentFloor&&(State==ElevatorState.Closing||State==ElevatorState.Closed)){State=ElevatorState.Opening;dwell=0;}return true;}
  public bool SelectFloor(HotelPlayer player,int floor){if(!Operational){player.Notify("Elevator out of service. Repair the service cabinet or use another lift / stairs.");return false;}if(floor<0||floor>=floors.Length)return false;if(!Contains(player.transform.position)){player.Notify("Enter the elevator before choosing a floor.");return false;}RequestFloor(floor);player.Notify("Elevator: "+floors[floor].label);return true;}
  public bool Call(HotelPlayer player,int floor,int travelDirection=0){if(!Operational){player.Notify("Elevator out of service. Maintenance cabinet nearby.");return false;}if(floor<0||floor>=floors.Length||Vector3.Distance(player.transform.position,ExitPoint(floor))>4)return false;hallDirections[floor]=travelDirection;if(CurrentFloor==floor&&travelDirection!=0&&State!=ElevatorState.Moving)direction=travelDirection;RequestFloor(floor);player.Notify("Elevator called. Wait for the doors to open.");return true;}
  public bool ControlDoors(HotelPlayer player,bool open){if(!Contains(player.transform.position))return false;if(State==ElevatorState.Moving){player.Notify("Doors are locked while the elevator is moving.");return false;}if(open){State=ElevatorState.Opening;dwell=0;manualClose=false;}else if(State==ElevatorState.Open)manualClose=true;return true;}
  public bool DoorBlocked()
  {
   var point=new Vector3(transform.position.x,cabin.position.y+1,-11);
   int count=Physics.OverlapBoxNonAlloc(point,new Vector3(.94f,1,.28f),sensor,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
   for(int i=0;i<count;i++){var c=sensor[i];if(c.transform.IsChildOf(transform))continue;if(c.GetComponentInParent<HotelPlayer>()||c.GetComponentInParent<HotelGuestBot>()||c.GetComponentInParent<HotelLuggageTrolley>()||c.attachedRigidbody)return true;}return false;
  }
  int NextFloor(){for(int step=1;step<floors.Length;step++){int f=CurrentFloor+direction*step;if(f>=0&&f<floors.Length&&requests[f])return f;}direction=-direction;for(int step=1;step<floors.Length;step++){int f=CurrentFloor+direction*step;if(f>=0&&f<floors.Length&&requests[f])return f;}return -1;}
  void Update()
  {
   if(!hotel||!hotel.Running){if(motor&&motor.isPlaying)motor.Pause();return;}
   AdvanceState(Time.deltaTime);
  }
  public void AdvanceState(float dt)
  {
   if(dt<=0||!hotel)return;
   if(!Operational){AdvanceOutOfService(dt);return;}
   waiting.RemoveAll(p=>!p);passengers.RemoveAll(p=>!p);
   // A guest may still be approaching when the cabin leaves this landing.
   // Keep the hall call alive until that guest has actually boarded.
   foreach(var p in waiting)requests[p.SourceFloor]=true;
   if(State==ElevatorState.Open)
   {
    requests[CurrentFloor]=false;dwell+=dt;
    bool boarding=false;foreach(var p in passengers)if(p.Boarding||p.Exiting)boarding=true;
    if(!boarding){foreach(var p in passengers)if(p.DestinationFloor==CurrentFloor){p.Exit();dwell=0;boarding=true;break;}}
    bool trolleyInside=false;if(hotel.serviceJobs)foreach(var trolley in hotel.serviceJobs.trolleys)if(trolley&&Contains(trolley.transform.position-Vector3.up*trolley.groundOffset))trolleyInside=true;
    int capacity=trolleyInside?2:hotel.player&&Contains(hotel.player.transform.position)?3:4;
    if(!boarding&&passengers.Count<capacity)
    {
     foreach(var p in waiting)if(p.SourceFloor==CurrentFloor&&p.ReadyToBoard){int slot=0;while(passengers.Exists(g=>g.Slot==slot))slot++;waiting.Remove(p);passengers.Add(p);p.Board(slot);RequestFloor(p.DestinationFloor);dwell=0;boarding=true;break;}
    }
    if(!boarding&&(manualClose||dwell>=dwellSeconds)&&!DoorBlocked()){State=ElevatorState.Closing;manualClose=false;}
   }
   else if(State==ElevatorState.Closing)
   {
    if(DoorBlocked()){State=ElevatorState.Opening;dwell=0;}
else{DoorAmount=Mathf.MoveTowards(DoorAmount,0,dt/doorSeconds);if(DoorAmount<=0){TargetFloor=NextFloor();if(TargetFloor<0){State=ElevatorState.Closed;dwell=0;return;}startHeight=cabin.position.y;travelTime=0;travelDuration=Mathf.Max(2,Mathf.Abs(floors[TargetFloor].height-startHeight)/speed+1);ridingPlayer=hotel.player&&Contains(hotel.player.transform.position)?hotel.player:null;CollectCargo();State=ElevatorState.Moving;if(motor)motor.Play();}}
   }
   else if(State==ElevatorState.Opening)
   {
    DoorAmount=Mathf.MoveTowards(DoorAmount,1,dt/doorSeconds);if(DoorAmount>=1){State=ElevatorState.Open;dwell=0;}
   }
   else if(State==ElevatorState.Closed){if(requests[CurrentFloor]&&waiting.Exists(p=>p.SourceFloor==CurrentFloor&&p.ReadyToBoard)){State=ElevatorState.Opening;dwell=0;}else if(NextFloor()>=0)State=ElevatorState.Closing;}
   PoseDoors();UpdateDisplays();
  }
  void LateUpdate()
  {
   if(State!=ElevatorState.Moving||!hotel||!hotel.Running)return;
   AdvanceTravel(Time.deltaTime);
  }
  public void AdvanceTravel(float dt)
  {
   if(State!=ElevatorState.Moving||dt<=0)return;
   if(motor&&!motor.isPlaying)motor.UnPause();travelTime+=dt;float t=Mathf.Clamp01(travelTime/travelDuration);float height=Mathf.Lerp(startHeight,floors[TargetFloor].height,t*t*(3-2*t));var delta=Vector3.up*(height-cabin.position.y);SetHeight(height);
   if(ridingPlayer)ridingPlayer.ApplyElevatorMotion(delta);foreach(var p in passengers)if(p)p.MoveWithCabin(delta);
   foreach(var body in cargo){if(!body)continue;if(ridingPlayer&&(ridingPlayer.cart&&body.gameObject==ridingPlayer.cart.gameObject||ridingPlayer.held&&body.gameObject==ridingPlayer.held.gameObject))continue;if(!cargoModes.ContainsKey(body)){cargoModes[body]=(body.isKinematic,body.interpolation);body.isKinematic=true;body.interpolation=RigidbodyInterpolation.None;}body.position+=delta;body.transform.position=body.position;}
   if(hotel.serviceJobs)foreach(var trolley in hotel.serviceJobs.trolleys)if(trolley)trolley.SyncCargo();
   if(t>=1){ReleaseCargo();CurrentFloor=TargetFloor;if(hallDirections[CurrentFloor]!=0){direction=hallDirections[CurrentFloor];hallDirections[CurrentFloor]=0;}TripsCompleted++;requests[CurrentFloor]=false;State=ElevatorState.Opening;ridingPlayer=null;if(motor)motor.Stop();if(audioSource&&chime)audioSource.PlayOneShot(chime,.4f);}
  }
  public void SetMechanicalFault(bool broken){if(MechanicalFault==broken)return;MechanicalFault=broken;if(broken)StopNewTrips();UpdateDisplays();}
  public void SetPowerAvailable(bool available){if(HasPower==available)return;HasPower=available;if(!available)StopNewTrips();UpdateDisplays();}
  void StopNewTrips()
  {
   var queue=waiting.ToArray();waiting.Clear();
   if(requests!=null)Array.Clear(requests,0,requests.Length);
   foreach(var passenger in queue)if(passenger){passenger.CancelTravel();passenger.GetComponent<HotelGuestBot>()?.ResumeElevatorRoute();}
   if(State!=ElevatorState.Moving){State=ElevatorState.Opening;dwell=0;if(motor)motor.Stop();}
  }
  void AdvanceOutOfService(float dt)
  {
   // Reserve/emergency drive finishes the current trip. Doors never open between landings.
   if(State==ElevatorState.Moving){UpdateDisplays();return;}
   if(State!=ElevatorState.Open){DoorAmount=Mathf.MoveTowards(DoorAmount,1,dt/doorSeconds);State=DoorAmount>=1?ElevatorState.Open:ElevatorState.Opening;}
   bool exiting=false;foreach(var passenger in passengers)if(passenger&&(passenger.Boarding||passenger.Exiting))exiting=true;
   if(!exiting)foreach(var passenger in passengers)if(passenger){passenger.Exit();break;}
   PoseDoors();UpdateDisplays();
  }
  void SetHeight(float height){var p=cabin.position;p.y=height;cabin.position=p;}
  void CollectCargo(){ReleaseCargo();var bodies=Physics.OverlapBox(cabin.position+Vector3.up*1.3f,new Vector3(1.53f,1.3f,1.83f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore);foreach(var c in bodies){var body=c.attachedRigidbody;if(!body||body.transform.IsChildOf(cabin)||cargo.Contains(body)||body.GetComponent<HotelGuestBot>()||body.GetComponent<HotelGuestDelivery>() is HotelGuestDelivery delivery&&delivery.Attached)continue;cargo.Add(body);if(!(ridingPlayer&&(body.gameObject==ridingPlayer.cart?.gameObject||body.gameObject==ridingPlayer.held?.gameObject))){cargoModes[body]=(body.isKinematic,body.interpolation);body.isKinematic=true;body.interpolation=RigidbodyInterpolation.None;}}if(ridingPlayer&&ridingPlayer.held){var body=ridingPlayer.held.GetComponent<Rigidbody>();if(body&&!cargo.Contains(body))cargo.Add(body);}}
  void PoseDoors(){for(int f=0;f<floors.Length;f++)Pose(floors[f].doors,f==CurrentFloor&&State!=ElevatorState.Moving?DoorAmount:0);Pose(cabinDoors,DoorAmount);}
  static void Pose(Transform[] doors,float amount){for(int i=0;i<doors.Length;i++){var p=doors[i].localPosition;p.x=(i==0?-1:1)*(.48f+amount*.99f);doors[i].localPosition=p;}}
  void UpdateDisplays(){int shown=State==ElevatorState.Moving?FloorFor(cabin.position.y):CurrentFloor;string number=shown==0?"LOBBY":(shown+1).ToString();string status=State==ElevatorState.Moving?(floors[TargetFloor].height>startHeight?"UP":"DOWN")+" / "+floors[TargetFloor].label:State==ElevatorState.Open?"WELCOME":State==ElevatorState.Opening?"ARRIVING":"READY";if(!Operational)status=State==ElevatorState.Moving?"EMERGENCY LANDING":MechanicalFault?"SERVICE REQUIRED":"BACKUP / POWER OFF";string text=number+"\n"+status;if(cabinDisplay&&cabinDisplay.text!=text)cabinDisplay.text=text;foreach(var floor in floors)if(floor.display&&floor.display.text!=text)floor.display.text=text;}
  public void ResetElevator(){foreach(var p in waiting.ToArray())if(p)p.CancelTravel();foreach(var p in passengers.ToArray())if(p)p.AbortAt(CurrentFloor);waiting.Clear();passengers.Clear();ReleaseCargo();if(requests!=null)Array.Clear(requests,0,requests.Length);if(hallDirections!=null)Array.Clear(hallDirections,0,hallDirections.Length);CurrentFloor=TargetFloor=0;SetHeight(floors[0].height);State=ElevatorState.Closed;DoorAmount=0;dwell=travelTime=TripsCompleted=0;manualClose=false;ridingPlayer=null;if(motor)motor.Stop();PoseDoors();UpdateDisplays();}
  void BuildAudio(){chime=AudioClip.Create("Elevator arrival chime",12000,1,24000,false);var samples=new float[12000];for(int i=0;i<samples.Length;i++){float t=i/24000f;samples[i]=(Mathf.Sin(t*2*Mathf.PI*880)+Mathf.Sin(t*2*Mathf.PI*1320))*.16f*Mathf.Exp(-t*7);}chime.SetData(samples,0);motorClip=AudioClip.Create("Elevator motor",24000,1,24000,false);samples=new float[24000];for(int i=0;i<samples.Length;i++)samples[i]=Mathf.Sin(i*2*Mathf.PI*90/24000f)*.07f;motorClip.SetData(samples,0);if(motor){motor.clip=motorClip;motor.loop=true;}}
  void OnDisable(){if(cabin&&floors!=null&&floors.Length>0)ResetElevator();}
  void OnDestroy(){if(chime)Destroy(chime);if(motorClip)Destroy(motorClip);}
 }
}
