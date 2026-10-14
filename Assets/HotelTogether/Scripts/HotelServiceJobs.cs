using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace HotelTogether
{
    [DefaultExecutionOrder(60)]
    public sealed class HotelServiceJobs : MonoBehaviour
    {
        public HotelGuestSimulation hotel;
        public HotelHousekeepingSystem housekeeping;
        public HotelGuestDelivery luggagePrefab,linenPrefab;
        public HotelRoomDelivery[] receivers;
        public HotelLuggageTrolley[] trolleys;
        public HotelLaundryMachine[] washers;
        public HotelServiceTray[] trays;
        public Transform luggageDrop;
        public Material cleanLinen;
        public int Completed { get; private set; }
        public int BagsDelivered { get; private set; }
        public int LaundryReturned { get; private set; }
        public int MealsServed { get; private set; }
        sealed class Work {public HotelGuestDelivery bag,linen;public bool bagDone,linenDone;public int receptionSlot;}
        readonly Dictionary<HotelGuestBot,Work> work=new();
        HotelServiceGuide guide;float guideRefresh;
        public HotelServiceGuide Guide => guide;
        void Start()
        {
            for(int i=0;i<receivers.Length;i++)
            {
                var old=receivers[i];if(!old)continue;var door=hotel.rooms[i].door;
                var receiver=door.GetComponent<HotelRoomDelivery>();if(!receiver)receiver=door.gameObject.AddComponent<HotelRoomDelivery>();
                receiver.jobs=this;receiver.roomIndex=i;receiver.deposit=old.deposit;receivers[i]=receiver;
                if(old!=receiver)old.gameObject.SetActive(false);
            }
        }
        public int PendingCount
        {
            get
            {
                int count=housekeeping?housekeeping.Pending:0;
                foreach(var guest in hotel.Guests)
                {
                    if(!guest)continue;
                    if(guest.Need!=GuestNeed.None&&guest.Need!=GuestNeed.Luggage)count++;
                    if(work.TryGetValue(guest,out var entry)){if(!entry.bagDone)count++;if(!entry.linenDone)count++;}
                }
                return count;
            }
        }
        public string LuggageStatus(HotelGuestBot guest)=>work.TryGetValue(guest,out var entry)?entry.bagDone?"Luggage delivered":guest.CollectingLuggage?"Guest collecting luggage":entry.bag&&NearDoor(entry.bag,receivers[guest.RoomIndex])?"At door - waiting for guest":"Deliver luggage to Room "+guest.RoomLabel:"";
        public void CheckIn(HotelGuestBot guest)
        {
            if(work.ContainsKey(guest))return;int slot=0;var occupied=new HashSet<int>();foreach(var existing in work.Values)if(!existing.bagDone)occupied.Add(existing.receptionSlot);while(occupied.Contains(slot))slot++;
            var entry=new Work{receptionSlot=slot};work.Add(guest,entry);var p=luggageDrop.position+luggageDrop.right*(slot%4)*.58f+luggageDrop.forward*(slot/4)*.56f;
            var bagPrefab=guest.visual&&guest.visual.arrivalLuggagePrefab?guest.visual.arrivalLuggagePrefab:luggagePrefab;
            entry.bag=Instantiate(bagPrefab,p,luggageDrop.rotation,transform);entry.bag.Assign(guest,GuestDeliveryType.Luggage);
            var receiver=receivers[guest.RoomIndex];entry.linen=Instantiate(linenPrefab,receiver.deposit.position+Vector3.up*.05f,receiver.deposit.rotation,transform);entry.linen.Assign(guest,GuestDeliveryType.DirtyLaundry);
            hotel.player.Notify("Room "+guest.RoomLabel+": luggage delivery and laundry jobs added.");
        }
        public bool HasPendingBag(HotelGuestBot guest)=>work.TryGetValue(guest,out var entry)&&!entry.bagDone;
        public string LaundryStatus(HotelGuestBot guest)
        {
            if(!work.TryGetValue(guest,out var entry))return "";if(entry.linenDone)return "Linen returned";
            foreach(var washer in washers)if(washer.Batch&&washer.Batch.Owner==guest)return washer.Ready?"Collect clean linen at LAUNDRY":washer.NeedsDetergent?"Add detergent at LAUNDRY":"Washing "+Mathf.CeilToInt(washer.Remaining)+"s";
            return entry.linen&&entry.linen.type==GuestDeliveryType.CleanLaundry?"Return clean linen to room":"Collect room linen -> LAUNDRY";
        }
        public bool DeliverRoom(HotelPlayer player,HotelRoomDelivery receiver)
        {
            if(Vector3.Distance(player.transform.position,receiver.transform.position)>3.2f)return false;
            bool done=false;var carried=player.held?player.held.GetComponent<HotelGuestDelivery>():null;
            if(carried&&carried.type==GuestDeliveryType.CleanLaundry)
            {
                var room=hotel.rooms[receiver.roomIndex];var defense=room.door.GetComponent<HotelDefenseOpening>();
                if(!receiver.CanCollect||!room.door.open&&!(defense&&defense.Broken)){player.Notify("Open and clear the room door before returning clean linen.");return false;}
            }
            if(carried){if(carried.Owner&&carried.Owner.RoomIndex==receiver.roomIndex&&carried.type!=GuestDeliveryType.DirtyLaundry){player.Drop();done=Complete(carried,receiver);}else{player.Notify("Wrong room or dirty linen. Check the guest tag.");return false;}}
            foreach(var trolley in trolleys)
            {
                if(Vector3.Distance(trolley.transform.position,receiver.transform.position)>3.4f)continue;
                var matching=new List<HotelGuestDelivery>();foreach(var bag in trolley.Loaded)if(bag&&bag.Owner&&bag.Owner.RoomIndex==receiver.roomIndex)matching.Add(bag);
                foreach(var bag in matching){trolley.Detach(bag);done|=Complete(bag,receiver);}
            }
            if(!done)player.Notify("Bring this room's tagged suitcase or clean linen here.");return done;
        }
        public bool StaffDeliver(HotelGuestDelivery item,HotelRoomDelivery receiver,Vector3 position){if(!item||!receiver||!receiver.CanCollect||Vector3.Distance(position,receiver.Doorstep)>2.5f)return false;item.ReleaseAttachment();return Complete(item,receiver);}
        bool Complete(HotelGuestDelivery delivery,HotelRoomDelivery receiver)
        {
            var guest=delivery.Owner;if(!guest||!work.TryGetValue(guest,out var entry))return false;
            if(delivery.type==GuestDeliveryType.Luggage)
            {
                if(entry.bagDone||entry.bag!=delivery||guest.CollectingLuggage)return false;
                delivery.Detach();delivery.ReleaseAttachment();delivery.transform.SetParent(transform,true);
                PlaceBag(delivery,receiver.Doorstep,Quaternion.LookRotation(receiver.Inward));
                hotel.player.Notify("Suitcase placed outside Room "+guest.RoomLabel+". The guest will come out to collect it. Unlock / clear the door if needed.");return true;
            }
            else if(delivery.type==GuestDeliveryType.CleanLaundry){if(entry.linenDone)return false;entry.linenDone=true;LaundryReturned++;}
            else return false;
            Completed++;hotel.RecordService(guest,delivery.type==GuestDeliveryType.Luggage?15:12);
            delivery.transform.SetParent(transform,true);delivery.transform.SetPositionAndRotation(receiver.deposit.position+(delivery.type==GuestDeliveryType.Luggage?receiver.deposit.right*.4f-Vector3.up*.095f:Vector3.zero),receiver.deposit.rotation);
            delivery.GetComponent<Rigidbody>().isKinematic=true;foreach(var c in delivery.GetComponentsInChildren<Collider>())c.enabled=false;delivery.GetComponent<HotelItem>().enabled=false;
            hotel.player.Notify("Delivered "+delivery.Label+". Keep the trolley clear of the doorway.");return true;
        }
        public static void PlaceBag(HotelGuestDelivery bag,Vector3 floor,Quaternion rotation)
        {
            var body=bag.GetComponent<Rigidbody>();body.isKinematic=true;body.interpolation=RigidbodyInterpolation.None;
            bag.SetPose(floor,rotation);Physics.SyncTransforms();float bottom=float.PositiveInfinity;
            foreach(var c in bag.GetComponentsInChildren<Collider>())if(c.enabled&&!c.isTrigger)bottom=Mathf.Min(bottom,c.bounds.min.y);
            if(float.IsPositiveInfinity(bottom))foreach(var r in bag.GetComponentsInChildren<Renderer>())if(r.enabled&&!r.GetComponent<TextMesh>())bottom=Mathf.Min(bottom,r.bounds.min.y);
            if(float.IsPositiveInfinity(bottom))bottom=floor.y;
            bag.SetPose(bag.transform.position+Vector3.up*(floor.y+.025f-bottom),rotation);
            body.isKinematic=false;body.useGravity=true;body.linearVelocity=body.angularVelocity=Vector3.zero;
        }
        public bool StaffBagAvailable(HotelGuestDelivery bag)=>bag&&bag.Owner&&bag.Owner.RoomIndex>=0&&HasPendingBag(bag.Owner)&&!NearDoor(bag,receivers[bag.Owner.RoomIndex]);
        bool NearDoor(HotelGuestDelivery bag,HotelRoomDelivery receiver)
        {
            if(!bag||bag.Attached||bag.Trolley||hotel.player.held==bag.GetComponent<HotelItem>())return false;
            var delta=bag.transform.position-receiver.Doorstep;return Mathf.Abs(delta.y)<1.1f&&new Vector2(delta.x,delta.z).magnitude<1.15f&&bag.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude<.3f;
        }
        public void ScanLuggage()
        {
            if(hotel.player&&hotel.player.GameplayPaused)return;
            foreach(var pair in work)
            {
                var guest=pair.Key;var entry=pair.Value;if(!guest||!guest.CheckedIn)continue;
                var receiver=receivers[guest.RoomIndex];
                if(!receiver.KnockPending&&guest.Activity!=GuestActivity.AtDoor)continue;
                if(!entry.bagDone&&NearDoor(entry.bag,receiver)){if(guest.TryCollectLuggage(entry.bag,receiver))receiver.Answered();}
                else if(receiver.KnockPending&&guest.TryAnswerDoor(receiver))receiver.Answered();
            }
        }
        public bool FinishLuggage(HotelGuestBot guest,HotelGuestDelivery bag)
        {
            if(!guest||!bag||bag.Owner!=guest||!work.TryGetValue(guest,out var entry)||entry.bag!=bag||entry.bagDone)return false;
            entry.bagDone=true;BagsDelivered++;Completed++;hotel.RecordService(guest,15);
            hotel.player.Notify(guest.guestName+" collected the suitcase for Room "+guest.RoomLabel+".");return true;
        }
        public void RecordMeal(){Completed++;MealsServed++;}
        public void RecordCleaning(){Completed++;}
        public void Forget(HotelGuestBot guest)
        {
            if(!work.TryGetValue(guest,out var entry))return;work.Remove(guest);
            foreach(var washer in washers)washer.Cancel(guest);
            foreach(var item in new[]{entry.bag,entry.linen})if(item){item.Detach();if(hotel.player.held==item.GetComponent<HotelItem>())hotel.player.Drop();Destroy(item.gameObject);}
        }
        public void ResetJobs()
        {
            if(housekeeping)housekeeping.ResetCleaning();
            foreach(var trolley in trolleys)trolley.ResetCargo();foreach(var washer in washers)washer.ResetMachine();foreach(var tray in trays)tray.ResetTray();
            foreach(var entry in work.Values){if(entry.bag)Destroy(entry.bag.gameObject);if(entry.linen)Destroy(entry.linen.gameObject);}work.Clear();Completed=BagsDelivered=LaundryReturned=MealsServed=0;
        }
        void Update()
        {
            if(!hotel.Running||hotel.player.combat&&hotel.player.combat.IsDown)return;
            if(Time.time>=guideRefresh){ScanLuggage();guide=HotelServiceGuidance.Build(this,hotel.player);guideRefresh=Time.time+.15f;}
            if(Keyboard.current==null)return;
            if(Keyboard.current.gKey.wasPressedThisFrame){var trolley=hotel.player.cart?hotel.player.cart.GetComponent<HotelLuggageTrolley>():hotel.player.FocusedItem?hotel.player.FocusedItem.GetComponent<HotelLuggageTrolley>():null;if(trolley){if(hotel.player.held)trolley.Load(hotel.player);else trolley.Unload(hotel.player);}}
        }
    }
}
