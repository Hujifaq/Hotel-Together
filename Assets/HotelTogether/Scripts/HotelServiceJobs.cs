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
        public string LuggageStatus(HotelGuestBot guest)=>work.TryGetValue(guest,out var entry)?entry.bagDone?"Luggage delivered":"Deliver luggage to Room "+guest.RoomLabel:"";
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
            var room=hotel.rooms[receiver.roomIndex];var defense=room.door.GetComponent<HotelDefenseOpening>();
            if(defense&&defense.BoardCount>0||!room.door.open&&!(defense&&defense.Broken)){player.Notify("Open the room door before delivering. Remove any barricade first.");return false;}
            bool done=false;var carried=player.held?player.held.GetComponent<HotelGuestDelivery>():null;
            if(carried){if(carried.Owner&&carried.Owner.RoomIndex==receiver.roomIndex&&carried.type!=GuestDeliveryType.DirtyLaundry){player.Drop();done=Complete(carried,receiver);}else{player.Notify("Wrong room or dirty linen. Check the guest tag.");return false;}}
            foreach(var trolley in trolleys)
            {
                if(Vector3.Distance(trolley.transform.position,receiver.transform.position)>3.4f)continue;
                var matching=new List<HotelGuestDelivery>();foreach(var bag in trolley.Loaded)if(bag&&bag.Owner&&bag.Owner.RoomIndex==receiver.roomIndex)matching.Add(bag);
                foreach(var bag in matching){trolley.Detach(bag);done|=Complete(bag,receiver);}
            }
            if(!done)player.Notify("Bring this room's tagged suitcase or clean linen here.");return done;
        }
        bool Complete(HotelGuestDelivery delivery,HotelRoomDelivery receiver)
        {
            var guest=delivery.Owner;if(!guest||!work.TryGetValue(guest,out var entry))return false;
            if(delivery.type==GuestDeliveryType.Luggage){if(entry.bagDone)return false;entry.bagDone=true;BagsDelivered++;guest.ReceiveLuggage();}
            else if(delivery.type==GuestDeliveryType.CleanLaundry){if(entry.linenDone)return false;entry.linenDone=true;LaundryReturned++;}
            else return false;
            Completed++;hotel.RecordService(guest,delivery.type==GuestDeliveryType.Luggage?15:12);
            delivery.transform.SetParent(transform,true);delivery.transform.SetPositionAndRotation(receiver.deposit.position+(delivery.type==GuestDeliveryType.Luggage?receiver.deposit.right*.4f-Vector3.up*.095f:Vector3.zero),receiver.deposit.rotation);
            delivery.GetComponent<Rigidbody>().isKinematic=true;foreach(var c in delivery.GetComponentsInChildren<Collider>())c.enabled=false;delivery.GetComponent<HotelItem>().enabled=false;
            hotel.player.Notify("Delivered "+delivery.Label+". Keep the trolley clear of the doorway.");return true;
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
            if(!hotel.Running||Keyboard.current==null||hotel.player.combat&&hotel.player.combat.IsDown)return;
            if(Time.time>=guideRefresh){guide=HotelServiceGuidance.Build(this,hotel.player);guideRefresh=Time.time+.15f;}
            if(Keyboard.current.gKey.wasPressedThisFrame){var trolley=hotel.player.cart?hotel.player.cart.GetComponent<HotelLuggageTrolley>():hotel.player.FocusedItem?hotel.player.FocusedItem.GetComponent<HotelLuggageTrolley>():null;if(trolley){if(hotel.player.held)trolley.Load(hotel.player);else trolley.Unload(hotel.player);}}
        }
    }
}
