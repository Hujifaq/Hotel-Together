using UnityEngine;
using UnityEngine.AI;
namespace HotelTogether
{
    public enum GuestActivity { Arriving, Queue, ToRoom, Unpacking, ToMeal, WaitingMeal, Eating, ToLounge, Relaxing, WaitingService, Sleeping, ToCheckout, Leaving, RouteBlocked, Fleeing, Hiding, Downed, ToWater, WaitingWater, FillingWater, DrinkingWater, ToLuggage, PickingLuggage, ReturningLuggage, StoringLuggage, ToDoor, AtDoor }
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
        public int PendingRoomIndex {get;private set;}=-1;
        public float ReceptionWaitSeconds {get;private set;}
        public bool ReceptionExpired {get;private set;}
        public bool AwaitingCheckIn=>!CheckedIn&&!ReceptionExpired;
        public float ReceptionLimit=>Mathf.Max(15,Hotel?Hotel.checkInPatience:120)*(Hotel&&Hotel.Economy&&Hotel.Economy.Initialized?Hotel.Economy.Patience:1);
        public float ReceptionRemaining=>Mathf.Max(0,ReceptionLimit-ReceptionWaitSeconds);
        int quotedRent;bool paymentAuthorized;
        public bool CheckedIn=>RoomIndex>=0;public bool IsSeated=>seated;public bool IsSleeping=>sleeping;
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
        public HotelGuestPhysics PrankPhysics { get; private set; }
        public HotelGuestPayment Payment {get;private set;}
        float prankPenalty;
        public HotelGuestElevatorPassenger liftPassenger {get;private set;}
        HotelGuestWaterStation waterStation;
        GameObject drinkingCup;
        int waterSlot=-1;
        float normalStoppingDistance;
        HotelGuestDelivery collectingBag;
        HotelRoomDelivery collectingRoom;
        float luggageStoppingDistance;
        float luggageGripHeight;
        float luggageBottomOffset;
        Vector3 luggageGripOffset;
        public bool CollectingLuggage=>collectingBag;
        public bool TryAnswerDoor(HotelRoomDelivery room)
        {
            if(!CheckedIn||checkoutQueued||CollectingLuggage||room.roomIndex!=RoomIndex||!room.CanCollect||PrankPhysics&&PrankPhysics.Suspended||survival&&survival.IsTakingCover||Payment&&(Payment.Pending||Payment.Transferring)||GetComponent<HotelHuman>()?.Targetable==false)return false;
            if(Activity!=GuestActivity.Unpacking&&Activity!=GuestActivity.Sleeping&&Activity!=GuestActivity.WaitingService)return false;
            CancelWater();ReleaseSeat();LeavePose();room.OpenForGuest();
            Move(room.Doorstep-room.Inward*.5f,GuestActivity.AtDoor,GuestActivity.ToDoor);return true;
        }
        public bool TryCollectLuggage(HotelGuestDelivery bag,HotelRoomDelivery room)
        {
            if(collectingBag||!CheckedIn||checkoutQueued||!bag||bag.Owner!=this||room.roomIndex!=RoomIndex||!room.CanCollect||PrankPhysics&&PrankPhysics.Suspended||survival&&survival.IsTakingCover||Payment&&(Payment.Pending||Payment.Transferring)||GetComponent<HotelHuman>()?.Targetable==false)return false;
            if(Activity!=GuestActivity.Unpacking&&Activity!=GuestActivity.Sleeping&&Activity!=GuestActivity.WaitingService&&Activity!=GuestActivity.AtDoor)return false;
            CancelWater();ReleaseSeat();LeavePose();Need=GuestNeed.Luggage;WaitSeconds=0;
            collectingBag=bag;collectingRoom=room;luggageStoppingDistance=Agent.stoppingDistance;Agent.stoppingDistance=.05f;
            room.OpenForGuest();var target=bag.transform.position-room.Inward*.5f;target.y=Hotel.rooms[RoomIndex].standPoint.y;
            Move(target,GuestActivity.PickingLuggage,GuestActivity.ToLuggage);return true;
        }
        public void AdvanceLuggage(float dt)
        {
            if(!collectingBag||!float.IsFinite(dt)||dt<=0||Hotel.player&&Hotel.player.GameplayPaused||PrankPhysics&&PrankPhysics.Suspended||survival&&survival.IsTakingCover)return;
            if(!collectingRoom.CanCollect||collectingBag.Trolley||Hotel.player.held==collectingBag.GetComponent<HotelItem>()||Vector3.Distance(collectingBag.transform.position,collectingRoom.Doorstep)>2&&!collectingBag.Attached){CancelLuggage();GoRoom();return;}
            if(Activity!=GuestActivity.StoringLuggage)collectingRoom.OpenForGuest();
            if(Activity==GuestActivity.PickingLuggage)
            {
                if(Vector3.Distance(transform.position,collectingBag.transform.position)>1.5f){CancelLuggage();GoRoom();return;}
                var direction=collectingBag.transform.position-transform.position;direction.y=0;if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),1-Mathf.Exp(-dt*8));
                if(activityTime<1.15f)return;
                if(!visual||!visual.rightForearm){CancelLuggage();GoRoom();return;}
                float top=collectingBag.transform.position.y,bottom=top;foreach(var c in collectingBag.GetComponentsInChildren<Collider>())if(c.enabled&&!c.isTrigger){top=Mathf.Max(top,c.bounds.max.y);bottom=Mathf.Min(bottom,c.bounds.min.y);}
                luggageGripHeight=top-collectingBag.transform.position.y;
                luggageBottomOffset=collectingBag.transform.position.y-bottom;luggageGripOffset=Vector3.up*luggageGripHeight;
                foreach(var t in collectingBag.GetComponentsInChildren<Transform>())if(t.name=="Guest luggage grip"){luggageGripOffset=Quaternion.Inverse(collectingBag.transform.rotation)*(t.position-collectingBag.transform.position);luggageGripHeight=luggageGripOffset.y;break;}
                collectingBag.AttachTo(visual.rightForearm,Vector3.zero,Quaternion.identity);
                Move(Hotel.rooms[RoomIndex].standPoint,GuestActivity.StoringLuggage,GuestActivity.ReturningLuggage);
            }
            else if(Activity==GuestActivity.StoringLuggage&&activityTime>=.85f)
            {
                var bag=collectingBag;bag.ReleaseAttachment();HotelServiceJobs.PlaceBag(bag,Hotel.rooms[RoomIndex].standPoint+Vector3.Cross(Vector3.up,collectingRoom.Inward)*.65f,Quaternion.LookRotation(collectingRoom.Inward));
                collectingBag=null;collectingRoom=null;Agent.stoppingDistance=luggageStoppingDistance;
                Need=GuestNeed.None;WaitSeconds=0;Activity=GuestActivity.Unpacking;activityTime=0;
                Hotel.serviceJobs.receivers[RoomIndex].CloseAfterEntry();Hotel.serviceJobs.FinishLuggage(this,bag);
            }
        }
        void CancelLuggage()
        {
            if(!collectingBag)return;collectingBag.ReleaseAttachment();collectingBag=null;collectingRoom=null;if(Agent)Agent.stoppingDistance=luggageStoppingDistance;
        }
        void PoseLuggage()
        {
            if(!collectingBag||!Hotel.Running)return;
            if(Activity==GuestActivity.PickingLuggage||Activity==GuestActivity.StoringLuggage)
            {
                float weight=Mathf.Sin(Mathf.Clamp01(activityTime/1.15f)*Mathf.PI);
                visual.hips.localPosition+=Vector3.down*(.23f*weight);visual.hips.localRotation=Quaternion.Euler(25*weight,0,0);visual.head.localRotation*=Quaternion.Euler(15*weight,0,0);
                visual.rightArm.localRotation=Quaternion.Euler(-28*weight,0,-10);visual.rightForearm.localRotation=Quaternion.Euler(-12,0,0);
            }
            else if(Activity==GuestActivity.ReturningLuggage){visual.rightArm.localRotation=Quaternion.Euler(8,0,-12);visual.rightForearm.localRotation=Quaternion.Euler(-8,0,0);}
            collectingBag.SyncAttachment();
            if(collectingBag.Attached)
            {
                // Rotate the existing arm bones only; translating the elbow stretches the skin.
                visual.rightArm.localRotation=Quaternion.Euler(-5,0,-22);
                visual.rightForearm.localRotation=Quaternion.Euler(-80,0,0);
                var grip=visual.rightForearm.TransformPoint(new Vector3(0,-.235f,0));
                var position=grip-transform.rotation*luggageGripOffset;
                position.y=Mathf.Max(position.y,transform.position.y+luggageBottomOffset+.05f);
                if(Activity==GuestActivity.StoringLuggage){var target=Hotel.rooms[RoomIndex].standPoint+Vector3.Cross(Vector3.up,collectingRoom.Inward)*.65f+Vector3.up*(luggageBottomOffset+.025f);position=Vector3.Lerp(position,target,Mathf.SmoothStep(0,1,activityTime/.85f));}
                collectingBag.SetPose(position,transform.rotation);
            }
        }
        public string RoomLabel=>CheckedIn?Hotel.rooms[RoomIndex].number.ToString():"--";
        public string NeedLabel=>Need switch {GuestNeed.CheckIn=>"CHECK IN",GuestNeed.Meal=>HotelGuestSupply.DishName(RequestedDish).ToUpperInvariant(),GuestNeed.Water=>"WATER",GuestNeed.Towel=>"CLEAN TOWEL",GuestNeed.CheckOut=>"CHECK OUT",GuestNeed.Help=>"ROUTE HELP",GuestNeed.Shelter=>"NEED SAFE SHELTER",GuestNeed.Rescue=>"RESCUE ME",GuestNeed.Luggage=>"BRING MY LUGGAGE",_=>""};
        public string ActionLabel=>Payment&&Payment.Transferring?"RECEIVING PAYMENT":Payment&&Payment.Pending?"LMB ON CASH: $"+Payment.Amount:Need==GuestNeed.Rescue?"RESCUE WITH MEDICINE":survival&&survival.IsTakingCover?"FOLLOW ME":AwaitingCheckIn?"E: ASK FOR CASH":Need switch {GuestNeed.CheckOut=>"CHECK OUT",GuestNeed.Help=>"RETRY ROUTE",GuestNeed.None=>"TALK",_=>"DELIVER "+NeedLabel};
        public void Initialize(HotelGuestSimulation hotel,string label)
        {
            Hotel=hotel;guestName=label;Agent=GetComponent<NavMeshAgent>();survival=GetComponent<HotelGuestSurvival>();liftPassenger=GetComponent<HotelGuestElevatorPassenger>();lastPosition=transform.position;
            PrankPhysics=GetComponent<HotelGuestPhysics>();if(!PrankPhysics)PrankPhysics=gameObject.AddComponent<HotelGuestPhysics>();
            Payment=GetComponent<HotelGuestPayment>();if(!Payment)Payment=gameObject.AddComponent<HotelGuestPayment>();Payment.Initialize(this);
            if(!GetComponent<HotelGuestPatienceBar>())gameObject.AddComponent<HotelGuestPatienceBar>();
            var health=GetComponent<HotelHuman>();if(!health)health=gameObject.AddComponent<HotelHuman>();
            var healthBar=GetComponent<HotelTargetHealthBar>();if(!healthBar)healthBar=gameObject.AddComponent<HotelTargetHealthBar>();healthBar.Initialize(health,null,hotel.player);
            var item=GetComponent<HotelItem>();item.kind="guest";item.displayName=label;
            Need=GuestNeed.CheckIn;Hotel.JoinQueue(this);Activity=GuestActivity.Arriving;RepositionQueue();
        }
        public void RepositionQueue()
        {
            if(!Hotel||!Agent||!Agent.enabled||!Agent.isOnNavMesh||ReceptionExpired||PrankPhysics&&PrankPhysics.Suspended)return;
            int slot=Hotel.QueuePosition(this);if(slot<0)return;
            Move(Hotel.QueuePoint(slot),GuestActivity.Queue,checkoutQueued?GuestActivity.ToCheckout:GuestActivity.Arriving);
        }
        public bool CheckIn(int room)
        {
            if(!paymentAuthorized||!AwaitingCheckIn||room!=PendingRoomIndex||!Hotel.RoomAvailable(room,this))return false;
            RoomIndex=room;RequestedDish=(MealDish)(room%2);CheckInDay=Hotel.DayNumber;Need=GuestNeed.None;Hotel.LeaveQueue(this);
            PendingRoomIndex=-1;quotedRent=0;
            if(Hotel.serviceJobs)Hotel.serviceJobs.CheckIn(this);Hotel.Economy?.CheckIn(this);Hotel.RecordService(this,12,false);GoRoom();Hotel.player?.Notify(guestName+" checked in - Room "+RoomLabel);return true;
        }
        public bool RequestCheckInPayment(HotelPlayer player)
        {
            if(!AwaitingCheckIn||!player||player.GameplayPaused||PrankPhysics&&PrankPhysics.Suspended||survival&&survival.IsTakingCover)return false;
            if(Hotel.QueuePosition(this)!=0){player.Notify("Please serve the first guest in the reception queue.");return false;}
            if(Activity!=GuestActivity.Queue){player.Notify("The guest must reach reception first.");return false;}
            if(Payment.Pending||Payment.Transferring){player.Notify(Payment.Pending?"Click the cash to finish check-in. E only requests the payment.":"Wait for the payment transfer to finish.");return false;}
            var economy=Hotel.Economy;if(!economy||!economy.Initialized){player.Notify("Reception payment service is not ready.");return false;}
            int room=Hotel.FindFreeRoom();if(room<0){player.Notify("All available rooms are occupied.");return false;}
            PendingRoomIndex=room;quotedRent=economy.RoomPrice;
            if(Payment.Offer(quotedRent,economy.QuoteTip(this,12)))return true;
            CancelCheckInPayment();return false;
        }
        public bool CanCompleteCheckIn(HotelCash note)=>note&&note.Owner==Payment&&AwaitingCheckIn&&Hotel.QueuePosition(this)==0&&PendingRoomIndex>=0&&note.Rent==quotedRent&&Hotel.RoomAvailable(PendingRoomIndex,this)&&!(PrankPhysics&&PrankPhysics.Suspended)&&!(survival&&survival.IsTakingCover)&&GetComponent<HotelHuman>()?.Targetable!=false;
        public void CompleteCheckInPayment(HotelCash note)
        {
            if(!note||!note.Paid||!note.Settled||!CanCompleteCheckIn(note))return;
            paymentAuthorized=true;CheckIn(PendingRoomIndex);paymentAuthorized=false;
        }
        public void CancelCheckInPayment(){PendingRoomIndex=-1;quotedRent=0;paymentAuthorized=false;}
        public void AdvanceReceptionWait(float dt)
        {
            if(!AwaitingCheckIn||!Hotel||!float.IsFinite(dt)||dt<=0||Hotel.player&&Hotel.player.GameplayPaused||survival&&survival.IsTakingCover||GetComponent<HotelHuman>()?.Targetable==false)return;
            ReceptionWaitSeconds+=dt;WaitSeconds=ReceptionWaitSeconds;
            Satisfaction=Mathf.Clamp01(1-ReceptionWaitSeconds/(ReceptionLimit*1.4f)-prankPenalty);
            if(ReceptionWaitSeconds<ReceptionLimit)return;
            ReceptionExpired=true;Payment?.Withdraw();CancelCheckInPayment();Need=GuestNeed.None;Activity=GuestActivity.Leaving;Hotel.LeaveQueue(this);Hotel.Economy?.Review(this,true);PrankPhysics?.Say("I have waited long enough. I'm leaving!");Hotel.player?.Notify(guestName+" left reception after waiting too long.");
            if(!(PrankPhysics&&PrankPhysics.Suspended))Move(Hotel.arrivalPoint,GuestActivity.Leaving,GuestActivity.Leaving);
        }
        void GoRoom()
        {
            CancelWater();
            ReleaseSeat();LeavePose();Need=GuestNeed.None;WaitSeconds=0;
            Move(Hotel.rooms[RoomIndex].standPoint,GuestActivity.Unpacking,GuestActivity.ToRoom);
        }
        public void OnPeriodChanged()
        {
            if(PrankPhysics&&PrankPhysics.Suspended)return;
            if(survival&&survival.IsTakingCover)return;
            if(CollectingLuggage||!CheckedIn||checkoutQueued||Activity==GuestActivity.Leaving)return;
            if(Hotel.IsNight){Payment?.Withdraw();GoRoom();}
            else if(Hotel.DayNumber>CheckInDay)
            {
                ReleaseSeat();LeavePose();Need=GuestNeed.CheckOut;WaitSeconds=0;checkoutQueued=true;
                Hotel.JoinQueue(this);RepositionQueue();
            }
        }
        void Request(GuestNeed need,GuestActivity activity){Need=need;Activity=activity;WaitSeconds=0;activityTime=0;Stop();}
        public bool StaffServe(Vector3 position,HotelGuestSupply supply=null){
            if(Vector3.Distance(position,transform.position)>2.5f||PrankPhysics&&PrankPhysics.Suspended||GetComponent<HotelHuman>()?.Targetable==false)return false;
            if(Payment.Pending)return Payment.CollectStaff(position);
            if(AwaitingCheckIn){if(Activity!=GuestActivity.Queue||Hotel.QueuePosition(this)!=0||Payment.Transferring)return false;int room=Hotel.FindFreeRoom();if(room<0)return false;PendingRoomIndex=room;quotedRent=Hotel.Economy.RoomPrice;return Payment.Offer(quotedRent,Hotel.Economy.QuoteTip(this,12));}
            if(Need==GuestNeed.CheckOut){if(Hotel.QueuePosition(this)!=0||Activity!=GuestActivity.Queue)return false;Hotel.Economy.Review(this);Hotel.RecordService(this,18);RoomIndex=-1;Hotel.LeaveQueue(this);Need=GuestNeed.None;Move(Hotel.arrivalPoint,GuestActivity.Leaving,GuestActivity.Leaving);return true;}if(Need==GuestNeed.Help){Need=blockedNeed;Move(destination,arrivalActivity,routeWalking);return true;}
            if(!supply||supply.type!=Need||Need==GuestNeed.Meal&&supply.dish!=RequestedDish)return false;
            bool collectingWater=waterStation;CancelWater();if(Need==GuestNeed.Meal&&Hotel.serviceJobs)Hotel.serviceJobs.RecordMeal();Hotel.RecordService(this,8);Satisfaction=Mathf.Clamp01(Satisfaction+.18f);Need=GuestNeed.None;WaitSeconds=0;activityTime=0;
            if(Activity==GuestActivity.WaitingMeal){Activity=GuestActivity.Eating;Hotel.ShowMeal(this,true);}else if(Hotel.IsNight)Sleep();else if(collectingWater)GoLounge();else Activity=GuestActivity.Relaxing;Destroy(supply.gameObject);return true;
        }
        public bool Serve(HotelPlayer player)
        {
            if(!player||player.GameplayPaused||Vector3.Distance(player.transform.position,transform.position)>3.2f||PrankPhysics&&PrankPhysics.Suspended)return false;
            var human=GetComponent<HotelHuman>();var combat=player.GetComponent<HotelCombat>();if(human&&human.Downed)return combat&&combat.Rescue(human);if(survival&&survival.IsTakingCover)return survival.Escort(player);
            if(AwaitingCheckIn)return RequestCheckInPayment(player);
            if(Payment&&Payment.Transferring)return false;if(Payment&&Payment.Pending)return Payment.Collect(player);
            if(Need==GuestNeed.None){player.Notify(guestName+": "+StatusLabel+". Thank you!");return false;}
            if(Need==GuestNeed.CheckIn)
            {
                if(Hotel.QueuePosition(this)!=0){player.Notify("Please serve the first guest in the reception queue.");return false;}
                if(Activity!=GuestActivity.Queue){player.Notify("The guest is approaching reception.");return false;}
                return RequestCheckInPayment(player);
            }
            if(Need==GuestNeed.CheckOut)
            {
                if(Hotel.QueuePosition(this)!=0||Activity!=GuestActivity.Queue){player.Notify("Please wait for the guest to reach the front of reception.");return false;}
                Hotel.Economy?.Review(this);Hotel.RecordService(this,18);RoomIndex=-1;Hotel.LeaveQueue(this);Need=GuestNeed.None;Move(Hotel.arrivalPoint,GuestActivity.Leaving,GuestActivity.Leaving);return true;
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
            player.PlayGesture(HotelPlayerGesture.Interact,transform.position);player.Notify("Delivered to "+guestName+". Thank you!");return true;
        }
        public string StatusLabel=>Activity switch {GuestActivity.Fleeing=>"Running to safety",GuestActivity.Hiding=>"Hiding safely",GuestActivity.Downed=>"Fallen - needs rescue",GuestActivity.Arriving=>"Arriving",GuestActivity.Queue=>"At reception",GuestActivity.ToRoom=>"Going to room "+RoomLabel,GuestActivity.Unpacking=>"Unpacking",GuestActivity.ToLuggage=>"Collecting suitcase at door",GuestActivity.PickingLuggage=>"Picking up suitcase",GuestActivity.ReturningLuggage=>"Taking suitcase inside",GuestActivity.StoringLuggage=>"Putting suitcase away",GuestActivity.ToMeal=>"Going to dining room",GuestActivity.WaitingMeal=>"Waiting for a meal",GuestActivity.Eating=>"Eating",GuestActivity.ToLounge=>"Going to the lounge",GuestActivity.Relaxing=>"Relaxing",GuestActivity.WaitingService=>"Waiting for service",GuestActivity.Sleeping=>"Sleeping",GuestActivity.ToWater=>"Going to refill water",GuestActivity.WaitingWater=>"Waiting at the water bar",GuestActivity.FillingWater=>"Filling a cup",GuestActivity.DrinkingWater=>"Drinking water",GuestActivity.ToCheckout=>"Going to check out",GuestActivity.Leaving=>"Leaving",_=>"Path blocked"};
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
            seated=sleeping=false;visualPositionTarget=Vector3.zero;visualRotationTarget=Quaternion.identity;
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
            visualPositionTarget=new Vector3(0,.96f,.75f);visualRotationTarget=Quaternion.Euler(-90,0,0);
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
            Stop();Activity=arrivalActivity;activityTime=0;if(CheckedIn&&Hotel.serviceJobs&&(Activity==GuestActivity.Unpacking||Activity==GuestActivity.StoringLuggage))Hotel.serviceJobs.receivers[RoomIndex].CloseAfterEntry();
            if(Activity==GuestActivity.Queue)transform.rotation=Quaternion.LookRotation(Vector3.left);
            if(Activity==GuestActivity.WaitingMeal){SitAt(Hotel.seats[SeatIndex]);Request(GuestNeed.Meal,GuestActivity.WaitingMeal);}
            if(Activity==GuestActivity.Relaxing){SitAt(Hotel.seats[SeatIndex]);}
            if(Activity==GuestActivity.WaitingWater&&waterStation)transform.rotation=Quaternion.LookRotation(-waterStation.transform.forward);
            if(Activity==GuestActivity.Leaving){Hotel.RemoveGuest(this);return;}
        }
        void Update()
        {
            if(!Hotel||!Hotel.Running)return;
            AdvanceReceptionWait(Time.deltaTime);
            if(PrankPhysics&&PrankPhysics.Suspended)return;
            if(Payment&&(Payment.Pending||Payment.Transferring)){Stop();return;}
            if(survival&&survival.IsTakingCover)return;
            if(waterStation)
            {
                if(!waterStation.WaterAvailable){CancelWater();Request(GuestNeed.Water,GuestActivity.WaitingService);return;}if(Hotel.IsNight){GoRoom();return;}
                int slot=waterStation.Position(this);
                if(WaitSeconds>55&&(Activity==GuestActivity.RouteBlocked||Activity==GuestActivity.ToWater)){CancelWater();Request(GuestNeed.Water,GuestActivity.WaitingService);return;}
                if(slot<0){CancelWater();Request(GuestNeed.Water,GuestActivity.WaitingService);return;}
                if(slot!=waterSlot){waterSlot=slot;Move(waterStation.QueuePoint(slot),GuestActivity.WaitingWater,GuestActivity.ToWater);}
                if(Activity==GuestActivity.WaitingWater&&slot==0){Activity=GuestActivity.FillingWater;activityTime=0;drinkingCup=Instantiate(waterStation.cupPrefab,transform);waterStation.Pour(this,true);}
                if(Activity==GuestActivity.FillingWater&&activityTime>=3){waterStation.Pour(this,false);Activity=GuestActivity.DrinkingWater;activityTime=0;}
                if(Activity==GuestActivity.DrinkingWater&&activityTime>=4){waterStation.Release(this,true);CancelWater();Need=GuestNeed.None;WaitSeconds=0;Satisfaction=Mathf.Clamp01(Satisfaction+.18f);GoLounge();return;}
            }
            float dt=Time.deltaTime;activityTime+=dt;AdvanceLuggage(dt);
            if(!AwaitingCheckIn&&Need!=GuestNeed.None&&Need!=GuestNeed.Help)
            {
                WaitSeconds+=dt;Satisfaction=Mathf.Clamp01(1-WaitSeconds/(180*(Hotel.Economy&&Hotel.Economy.Initialized?Hotel.Economy.Patience:1))-prankPenalty);
            }
            if(liftPassenger&&liftPassenger.Active)return;
            if(Agent.enabled&&Agent.isOnNavMesh&&!Agent.isStopped)
            {
                routeTime+=dt;
                if(Time.time>doorCheck){Hotel.OpenNearbyDoors(transform.position,this);doorCheck=Time.time+.3f;}
                if(!Agent.pathPending&&Agent.remainingDistance<=Agent.stoppingDistance+.08f)Arrive();
                else if(routeTime>100||!Agent.pathPending&&Agent.pathStatus!=NavMeshPathStatus.PathComplete)Blocked();
            }
            else if(Activity==GuestActivity.RouteBlocked&&activityTime>3){Need=blockedNeed;Move(destination,arrivalActivity,routeWalking);}
            else if(Activity==GuestActivity.AtDoor&&activityTime>10){GoRoom();}
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
            if(PrankPhysics&&PrankPhysics.Suspended){lastPosition=transform.position;return;}
            if(!Hotel||!visual)return;
            var displacement=transform.position-lastPosition;lastPosition=transform.position;
            float speed=displacement.sqrMagnitude>1?0:displacement.magnitude/Mathf.Max(.001f,Time.deltaTime);
            if(survival&&survival.IsTakingCover){survival.Pose(Hotel.Running?speed:0,Hotel.Running?Time.deltaTime:0);return;}
            if(Hotel.Running){float blend=1-Mathf.Exp(-Time.deltaTime*8);visual.transform.localPosition=Vector3.Lerp(visual.transform.localPosition,visualPositionTarget,blend);visual.transform.localRotation=Quaternion.Slerp(visual.transform.localRotation,visualRotationTarget,blend);}
            visual.Pose(Hotel.Running?speed:0,Hotel.Running?Time.deltaTime:0,seated,sleeping,Activity==GuestActivity.Eating,Satisfaction,!CheckedIn||checkoutQueued);
            visual.PoseActivity(Activity,Hotel.Running?Time.deltaTime:0);PoseLuggage();
            if(drinkingCup&&waterStation){var target=Activity==GuestActivity.FillingWater?waterStation.cupPosition.position:visual.head.position+transform.forward*.13f-transform.up*.11f;float sip=Activity==GuestActivity.DrinkingWater?-Mathf.Lerp(10,35,.5f+.5f*Mathf.Sin(visual.AnimationClock*2.4f)):0;drinkingCup.transform.SetPositionAndRotation(target,transform.rotation*Quaternion.Euler(sip,0,0));visual.HoldCup(target);}
            bool walking=Activity==GuestActivity.Arriving||Activity==GuestActivity.ToRoom||Activity==GuestActivity.ToMeal||Activity==GuestActivity.ToLounge||Activity==GuestActivity.ToCheckout||Activity==GuestActivity.Leaving||Activity==GuestActivity.ToWater||Activity==GuestActivity.ToLuggage||Activity==GuestActivity.ReturningLuggage||Activity==GuestActivity.ToDoor;
            if(liftPassenger&&liftPassenger.Active)walking=liftPassenger.Walking;
            if(Agent.enabled&&Agent.isOnNavMesh)Agent.isStopped=!Hotel.Running||!walking;
        }
        void OnDestroy(){CancelLuggage();CancelWater();if(Hotel)Hotel.Forget(this);}
        Vector3 visualPositionTarget;Quaternion visualRotationTarget=Quaternion.identity;
        public void ReactToPrank(float penalty){prankPenalty=Mathf.Clamp(prankPenalty+penalty,0,.7f);Satisfaction=Mathf.Clamp01(Satisfaction-penalty);}
        public void SuspendForPrank(){CancelLuggage();Payment?.Withdraw();CancelWater();liftPassenger?.CancelTravel();ReleaseSeat();seated=sleeping=false;visualPositionTarget=Vector3.zero;visualRotationTarget=Quaternion.identity;Stop();}
        public void ResumeAfterPrank()
        {
            lastPosition=transform.position;
            if(!Hotel)return;
            if(survival&&survival.IsTakingCover)return;
            if(Activity==GuestActivity.Leaving){Move(Hotel.arrivalPoint,GuestActivity.Leaving,GuestActivity.Leaving);return;}
            if(checkoutQueued){RepositionQueue();return;}
            if(!CheckedIn){Need=GuestNeed.CheckIn;Hotel.JoinQueue(this);RepositionQueue();return;}
            GoRoom();
        }
        public void PrepareForDanger(){CancelLuggage();Payment?.Withdraw();CancelWater();if(liftPassenger){if(liftPassenger.Aboard){SetSurvivalPresentation(GuestActivity.Fleeing,GuestNeed.Shelter);return;}liftPassenger.CancelTravel();}ReleaseSeat();LeavePose();Hotel.LeaveQueue(this);Stop();SetSurvivalPresentation(GuestActivity.Fleeing,GuestNeed.Shelter);}
        public void ResumeElevatorRoute(){Move(destination,arrivalActivity,routeWalking);}
        public void StopForDanger(){Stop();}
        public void ReceiveLuggage(){if(Activity==GuestActivity.Unpacking){Need=GuestNeed.None;WaitSeconds=0;if(!Hotel.IsNight)GoMeal();}}
        public void SetSurvivalPresentation(GuestActivity activity,GuestNeed need){Activity=activity;Need=need;WaitSeconds=0;}
        public void ResumeAfterDanger(){visual.transform.localRotation=Quaternion.identity;if(ReceptionExpired){Need=GuestNeed.None;Move(Hotel.arrivalPoint,GuestActivity.Leaving,GuestActivity.Leaving);return;}if(CheckedIn){if(Hotel.DayNumber>CheckInDay)OnPeriodChanged();else if(Hotel.IsNight){Payment?.Withdraw();GoRoom();}else GoMeal();}else{Need=GuestNeed.CheckIn;Hotel.JoinQueue(this);RepositionQueue();}}
    }
}
