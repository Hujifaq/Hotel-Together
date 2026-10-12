using UnityEngine;
using UnityEngine.AI;
namespace HotelTogether
{
    public enum GuestActivity { Arriving, Queue, ToRoom, Unpacking, ToMeal, WaitingMeal, Eating, ToLounge, Relaxing, WaitingService, Sleeping, ToCheckout, Leaving, RouteBlocked, Fleeing, Hiding, Downed, ToWater, WaitingWater, FillingWater, DrinkingWater }
    [RequireComponent(typeof(NavMeshAgent),typeof(HotelItem))]
    public sealed class HotelGuestBot : MonoBehaviour
    {
        public HotelGuestVisual visual;
        public string guestName;
        public GuestActivity Activity { get; private set; }
        public GuestNeed Need { get; private set; }
        public MealDish RequestedDish { get; private set; }
        public float Satisfaction { get; private set; }=1;
        public float WaitSeconds { get; private set; }
        public int RoomIndex { get; private set; }=-1;
        public int CheckInDay { get; private set; }
        public bool CheckedIn=>RoomIndex>=0;
        public NavMeshAgent Agent { get; private set; }
        public HotelGuestSimulation Hotel { get; private set; }
        public int SeatIndex { get; private set; }=-1;
        float activityTime, routeTime, doorCheck;
        GuestActivity arrivalActivity;
        GuestActivity routeWalking;
        GuestNeed blockedNeed;
        Vector3 destination, lastPosition;
        bool seated, sleeping, checkoutQueued;
        HotelGuestSurvival survival;
        public HotelGuestElevatorPassenger liftPassenger {get;private set;}
        HotelGuestWaterStation waterStation;
        GameObject drinkingCup;
        int waterSlot=-1;
        float normalStoppingDistance;
        public string RoomLabel=>CheckedIn?Hotel.rooms[RoomIndex].number.ToString():"--";
        public string NeedLabel=>Need switch {GuestNeed.CheckIn=>"CHECK IN",GuestNeed.Meal=>HotelGuestSupply.DishName(RequestedDish).ToUpperInvariant(),GuestNeed.Water=>"WATER",GuestNeed.Towel=>"CLEAN TOWEL",GuestNeed.CheckOut=>"CHECK OUT",GuestNeed.Help=>"ROUTE HELP",GuestNeed.Shelter=>"NEED SAFE SHELTER",GuestNeed.Rescue=>"RESCUE ME",GuestNeed.Luggage=>"BRING MY LUGGAGE",_=>""};
        public string ActionLabel=>Need==GuestNeed.Rescue?"RESCUE WITH MEDICINE":survival&&survival.IsTakingCover?"FOLLOW ME":Need switch {GuestNeed.CheckIn=>"CHECK IN",GuestNeed.CheckOut=>"CHECK OUT",GuestNeed.Help=>"RETRY ROUTE",GuestNeed.None=>"TALK",_=>"DELIVER "+NeedLabel};
        public void Initialize(HotelGuestSimulation hotel,string label)
        {
            Hotel=hotel;guestName=label;Agent=GetComponent<NavMeshAgent>();survival=GetComponent<HotelGuestSurvival>();liftPassenger=GetComponent<HotelGuestElevatorPassenger>();lastPosition=transform.position;
            var item=GetComponent<HotelItem>();item.kind="guest";item.displayName=label;
            Need=GuestNeed.CheckIn;Hotel.JoinQueue(this);Activity=GuestActivity.Arriving;RepositionQueue();
        }
        public void RepositionQueue()
        {
            if(!Hotel||!Agent||!Agent.isOnNavMesh)return;
            int slot=Hotel.QueuePosition(this);if(slot<0)return;
            Move(Hotel.QueuePoint(slot),GuestActivity.Queue,checkoutQueued?GuestActivity.ToCheckout:GuestActivity.Arriving);
        }
        public bool CheckIn(int room)
        {
            if(Need!=GuestNeed.CheckIn||Hotel.QueuePosition(this)!=0)return false;
            RoomIndex=room;RequestedDish=(MealDish)(room%2);CheckInDay=Hotel.DayNumber;Need=GuestNeed.None;Hotel.LeaveQueue(this);
            if(Hotel.serviceJobs)Hotel.serviceJobs.CheckIn(this);GoRoom();return true;
        }
        void GoRoom()
        {
            CancelWater();
            ReleaseSeat();LeavePose();Need=GuestNeed.None;WaitSeconds=0;
            Move(Hotel.rooms[RoomIndex].standPoint,GuestActivity.Unpacking,GuestActivity.ToRoom);
        }
        public void OnPeriodChanged()
        {
            if(survival&&survival.IsTakingCover)return;
            if(!CheckedIn||checkoutQueued||Activity==GuestActivity.Leaving)return;
            if(Hotel.IsNight)GoRoom();
            else if(Hotel.DayNumber>CheckInDay)
            {
                ReleaseSeat();LeavePose();Need=GuestNeed.CheckOut;WaitSeconds=0;checkoutQueued=true;
                Hotel.JoinQueue(this);RepositionQueue();
            }
        }
        void Request(GuestNeed need,GuestActivity activity){Need=need;Activity=activity;WaitSeconds=0;activityTime=0;Stop();}
        public bool Serve(HotelPlayer player)
        {
            if(Vector3.Distance(player.transform.position,transform.position)>3.2f)return false;
            var human=GetComponent<HotelHuman>();var combat=player.GetComponent<HotelCombat>();if(human&&human.Downed)return combat&&combat.Rescue(human);if(survival&&survival.IsTakingCover)return survival.Escort(player);
            if(Need==GuestNeed.None){player.Notify(guestName+": "+StatusLabel+". Thank you!");return false;}
            if(Need==GuestNeed.CheckIn)
            {
                if(Hotel.QueuePosition(this)!=0){player.Notify("Please serve the first guest in the reception queue.");return false;}
                if(Activity!=GuestActivity.Queue){player.Notify("The guest is approaching reception.");return false;}
                int room=Hotel.FindFreeRoom();if(room<0){player.Notify("All available rooms are occupied.");return false;}
                CheckIn(room);Hotel.RecordService(this,12);player.Notify(guestName+" checked in - Room "+RoomLabel);return true;
            }
            if(Need==GuestNeed.CheckOut)
            {
                if(Hotel.QueuePosition(this)!=0||Activity!=GuestActivity.Queue){player.Notify("Please wait for the guest to reach the front of reception.");return false;}
                Hotel.RecordService(this,18);RoomIndex=-1;Hotel.LeaveQueue(this);Need=GuestNeed.None;Move(Hotel.arrivalPoint,GuestActivity.Leaving,GuestActivity.Leaving);return true;
            }
            if(Need==GuestNeed.Help){Need=blockedNeed;Move(destination,arrivalActivity,routeWalking);return true;}
            if(Need==GuestNeed.Luggage){player.Notify("Bring the tagged suitcase to the delivery point outside Room "+RoomLabel+".");return false;}
            var supply=player.held?player.held.GetComponent<HotelGuestSupply>():null;
            var tray=player.held?player.held.GetComponent<HotelServiceTray>():null;
            if(Need==GuestNeed.Meal&&Hotel.serviceJobs&&!tray){player.Notify("Serve meals on a service tray. Pick up a tray at the Lobby GUEST SERVICE counter, then E on the requested plate.");return false;}
            if(tray){if(!tray.Serve(Need,RequestedDish)){player.Notify("No "+NeedLabel+" on this tray. Get the matching plate at the Lobby GUEST SERVICE counter.");return false;}}
            else{if(!supply||supply.type!=Need){player.Notify(guestName+" needs "+NeedLabel+". Collect it at the Lobby GUEST SERVICE counter.");return false;}var delivered=supply.gameObject;player.Drop();Destroy(delivered);}
            bool collectingWater=waterStation;CancelWater();if(Need==GuestNeed.Meal&&Hotel.serviceJobs)Hotel.serviceJobs.RecordMeal();Hotel.RecordService(this,8);Satisfaction=Mathf.Clamp01(Satisfaction+.18f);Need=GuestNeed.None;WaitSeconds=0;activityTime=0;
            if(Activity==GuestActivity.WaitingMeal){Activity=GuestActivity.Eating;Hotel.ShowMeal(this,true);}
            else if(Hotel.IsNight)Sleep();else if(collectingWater)GoLounge();else Activity=GuestActivity.Relaxing;
            player.Notify("Delivered to "+guestName+". Thank you!");return true;
        }
        public string StatusLabel=>Activity switch {GuestActivity.Fleeing=>"Running to safety",GuestActivity.Hiding=>"Hiding safely",GuestActivity.Downed=>"Fallen - needs rescue",GuestActivity.Arriving=>"Arriving",GuestActivity.Queue=>"At reception",GuestActivity.ToRoom=>"Going to room "+RoomLabel,GuestActivity.Unpacking=>"Unpacking",GuestActivity.ToMeal=>"Going to dining room",GuestActivity.WaitingMeal=>"Waiting for a meal",GuestActivity.Eating=>"Eating",GuestActivity.ToLounge=>"Going to the lounge",GuestActivity.Relaxing=>"Relaxing",GuestActivity.WaitingService=>"Waiting for service",GuestActivity.Sleeping=>"Sleeping",GuestActivity.ToWater=>"Going to refill water",GuestActivity.WaitingWater=>"Waiting at the water bar",GuestActivity.FillingWater=>"Filling a cup",GuestActivity.DrinkingWater=>"Drinking water",GuestActivity.ToCheckout=>"Going to check out",GuestActivity.Leaving=>"Leaving",_=>"Path blocked"};
        bool Move(Vector3 point,GuestActivity next,GuestActivity walking)
        {
            destination=point;arrivalActivity=next;routeWalking=walking;Activity=walking;activityTime=routeTime=0;
            if(liftPassenger){if(liftPassenger.Aboard)return true;if(liftPassenger.Active)liftPassenger.CancelTravel();if(Mathf.Abs(point.y-transform.position.y)>3&&liftPassenger.TryBegin(point))return true;}
            if(!Agent||!Agent.enabled||!Agent.isOnNavMesh)return Blocked();
            var filter=new NavMeshQueryFilter{agentTypeID=Agent.agentTypeID,areaMask=Agent.areaMask};
            if(!NavMesh.SamplePosition(point,out var hit,1.3f,filter))return Blocked();
            var path=new NavMeshPath();if(!Agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)return Blocked();
            Agent.isStopped=false;Agent.SetPath(path);return true;
        }
        bool Blocked(){Activity=GuestActivity.RouteBlocked;if(Need!=GuestNeed.Help)blockedNeed=Need;Need=GuestNeed.Help;Stop();return false;}
        void Stop(){if(Agent&&Agent.enabled&&Agent.isOnNavMesh){Agent.isStopped=true;Agent.ResetPath();}}
        void ReleaseSeat(){if(SeatIndex>=0){Hotel.ReleaseSeat(SeatIndex,this);SeatIndex=-1;}Hotel.ShowMeal(this,false);}
        void LeavePose()
        {
            seated=sleeping=false;visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
            if(liftPassenger&&liftPassenger.Aboard)return;
            if(!Agent.enabled){Agent.enabled=true;Agent.Warp(destination);}
        }
        void SitAt(HotelGuestSimulation.GuestSeat seat)
        {
            Stop();Agent.enabled=false;seated=true;transform.SetPositionAndRotation(seat.position,seat.rotation);
        }
        void Sleep()
        {
            Stop();Agent.enabled=false;seated=false;sleeping=true;Activity=GuestActivity.Sleeping;Need=GuestNeed.None;
            var bed=Hotel.rooms[RoomIndex].bed;transform.SetPositionAndRotation(bed.position,bed.rotation);
            visual.transform.localPosition=new Vector3(0,.96f,.75f);visual.transform.localRotation=Quaternion.Euler(-90,0,0);
        }
        void GoMeal()
        {
            ReleaseSeat();LeavePose();SeatIndex=Hotel.AcquireSeat(this,true);
            if(SeatIndex<0){activityTime=-5;return;}
            Move(Hotel.seats[SeatIndex].approach,GuestActivity.WaitingMeal,GuestActivity.ToMeal);
        }
        void GoLounge()
        {
            ReleaseSeat();LeavePose();SeatIndex=Hotel.AcquireSeat(this,false);
            if(SeatIndex<0){GoRoom();return;}
            Move(Hotel.seats[SeatIndex].approach,GuestActivity.Relaxing,GuestActivity.ToLounge);
        }
        public bool GoDrinkWater()
        {
            if(!CheckedIn||Hotel.IsNight||survival&&survival.IsTakingCover)return false;
            CancelWater();waterStation=Hotel.waterStation;
            if(!waterStation||!waterStation.isActiveAndEnabled){Request(GuestNeed.Water,GuestActivity.WaitingService);return false;}
            waterSlot=waterStation.Join(this);if(waterSlot<0){waterStation=null;Request(GuestNeed.Water,GuestActivity.WaitingService);return false;}
            ReleaseSeat();LeavePose();normalStoppingDistance=Agent.stoppingDistance;Agent.stoppingDistance=.04f;Need=GuestNeed.Water;WaitSeconds=0;
            return Move(waterStation.QueuePoint(waterSlot),GuestActivity.WaitingWater,GuestActivity.ToWater);
        }
        void CancelWater()
        {
            if(waterStation){waterStation.Release(this);if(Agent)Agent.stoppingDistance=normalStoppingDistance;}waterStation=null;waterSlot=-1;
            if(drinkingCup)Destroy(drinkingCup);drinkingCup=null;
        }
        void Arrive()
        {
            Stop();Activity=arrivalActivity;activityTime=0;
            if(Activity==GuestActivity.Queue)transform.rotation=Quaternion.LookRotation(Vector3.left);
            if(Activity==GuestActivity.WaitingMeal){SitAt(Hotel.seats[SeatIndex]);Request(GuestNeed.Meal,GuestActivity.WaitingMeal);}
            if(Activity==GuestActivity.Relaxing){SitAt(Hotel.seats[SeatIndex]);}
            if(Activity==GuestActivity.WaitingWater&&waterStation)transform.rotation=Quaternion.LookRotation(-waterStation.transform.forward);
            if(Activity==GuestActivity.Leaving){Hotel.RemoveGuest(this);return;}
        }
        void Update()
        {
            if(!Hotel||!Hotel.Running)return;
            if(survival&&survival.IsTakingCover)return;
            if(waterStation)
            {
                if(Hotel.IsNight){GoRoom();return;}
                int slot=waterStation.Position(this);
                if(WaitSeconds>55&&(Activity==GuestActivity.RouteBlocked||Activity==GuestActivity.ToWater)){CancelWater();Request(GuestNeed.Water,GuestActivity.WaitingService);return;}
                if(slot<0){CancelWater();Request(GuestNeed.Water,GuestActivity.WaitingService);return;}
                if(slot!=waterSlot){waterSlot=slot;Move(waterStation.QueuePoint(slot),GuestActivity.WaitingWater,GuestActivity.ToWater);}
                if(Activity==GuestActivity.WaitingWater&&slot==0){Activity=GuestActivity.FillingWater;activityTime=0;drinkingCup=Instantiate(waterStation.cupPrefab,transform);waterStation.Pour(this,true);}
                if(Activity==GuestActivity.FillingWater&&activityTime>=3){waterStation.Pour(this,false);Activity=GuestActivity.DrinkingWater;activityTime=0;}
                if(Activity==GuestActivity.DrinkingWater&&activityTime>=4){waterStation.Release(this,true);CancelWater();Need=GuestNeed.None;WaitSeconds=0;Satisfaction=Mathf.Clamp01(Satisfaction+.18f);GoLounge();return;}
            }
            float dt=Time.deltaTime;activityTime+=dt;
            if(Need!=GuestNeed.None&&Need!=GuestNeed.Help)
            {
                WaitSeconds+=dt;Satisfaction=Mathf.Clamp01(1-WaitSeconds/180);
                if(WaitSeconds>125&&!CheckedIn){Need=GuestNeed.None;Hotel.LeaveQueue(this);Move(Hotel.arrivalPoint,GuestActivity.Leaving,GuestActivity.Leaving);}
            }
            if(liftPassenger&&liftPassenger.Active)return;
            if(Agent.enabled&&Agent.isOnNavMesh&&!Agent.isStopped)
            {
                routeTime+=dt;
                if(Time.time>doorCheck){Hotel.OpenNearbyDoors(transform.position);doorCheck=Time.time+.3f;}
                if(!Agent.pathPending&&Agent.remainingDistance<=Agent.stoppingDistance+.08f)Arrive();
                else if(routeTime>100||!Agent.pathPending&&Agent.pathStatus!=NavMeshPathStatus.PathComplete)Blocked();
            }
            else if(Activity==GuestActivity.RouteBlocked&&activityTime>3){Need=blockedNeed;Move(destination,arrivalActivity,routeWalking);}
            else if(Activity==GuestActivity.Unpacking&&activityTime>7)
            {if(Hotel.IsNight)Request(GuestNeed.Towel,GuestActivity.WaitingService);else if(Hotel.serviceJobs&&Hotel.serviceJobs.HasPendingBag(this)){if(Need!=GuestNeed.Luggage){Need=GuestNeed.Luggage;WaitSeconds=0;}}else GoMeal();}
            else if(Activity==GuestActivity.Eating&&activityTime>14){GoLounge();}
            else if(Activity==GuestActivity.Relaxing&&activityTime>22&&Need==GuestNeed.None){GoDrinkWater();}
            else if(Activity==GuestActivity.WaitingService&&WaitSeconds>65)
            {Satisfaction=.55f;Need=GuestNeed.None;if(Hotel.IsNight)Sleep();else GoRoom();}
            else if(Activity==GuestActivity.WaitingMeal&&WaitSeconds>65){Satisfaction=.5f;Need=GuestNeed.None;GoLounge();}
        }
        void LateUpdate()
        {
            if(!Hotel||!visual)return;
            var displacement=transform.position-lastPosition;lastPosition=transform.position;
            float speed=displacement.sqrMagnitude>1?0:displacement.magnitude/Mathf.Max(.001f,Time.deltaTime);
            if(survival&&survival.IsTakingCover){survival.Pose(Hotel.Running?speed:0,Hotel.Running?Time.deltaTime:0);return;}
            visual.Pose(Hotel.Running?speed:0,Hotel.Running?Time.deltaTime:0,seated,sleeping,Activity==GuestActivity.Eating,Satisfaction,!CheckedIn||checkoutQueued);
            if(drinkingCup&&waterStation){var target=Activity==GuestActivity.FillingWater?waterStation.cupPosition.position:visual.head.position+transform.forward*.13f-transform.up*.11f;drinkingCup.transform.SetPositionAndRotation(target,transform.rotation*Quaternion.Euler(Activity==GuestActivity.DrinkingWater?-25:0,0,0));visual.HoldCup(target);}
            bool walking=Activity==GuestActivity.Arriving||Activity==GuestActivity.ToRoom||Activity==GuestActivity.ToMeal||Activity==GuestActivity.ToLounge||Activity==GuestActivity.ToCheckout||Activity==GuestActivity.Leaving||Activity==GuestActivity.ToWater;
            if(liftPassenger&&liftPassenger.Active)walking=liftPassenger.Walking;
            if(Agent.enabled&&Agent.isOnNavMesh)Agent.isStopped=!Hotel.Running||!walking;
        }
        void OnDestroy(){CancelWater();if(Hotel)Hotel.Forget(this);}
        public void PrepareForDanger(){CancelWater();if(liftPassenger){if(liftPassenger.Aboard){SetSurvivalPresentation(GuestActivity.Fleeing,GuestNeed.Shelter);return;}liftPassenger.CancelTravel();}ReleaseSeat();LeavePose();Hotel.LeaveQueue(this);Stop();SetSurvivalPresentation(GuestActivity.Fleeing,GuestNeed.Shelter);}
        public void ResumeElevatorRoute(){Move(destination,arrivalActivity,routeWalking);}
        public void StopForDanger(){Stop();}
        public void ReceiveLuggage(){if(Activity==GuestActivity.Unpacking){Need=GuestNeed.None;WaitSeconds=0;if(!Hotel.IsNight)GoMeal();}}
        public void SetSurvivalPresentation(GuestActivity activity,GuestNeed need){Activity=activity;Need=need;WaitSeconds=0;}
        public void ResumeAfterDanger(){visual.transform.localRotation=Quaternion.identity;if(CheckedIn){if(Hotel.DayNumber>CheckInDay)OnPeriodChanged();else if(Hotel.IsNight)GoRoom();else GoMeal();}else{Need=GuestNeed.CheckIn;Hotel.JoinQueue(this);RepositionQueue();}}
    }
}
