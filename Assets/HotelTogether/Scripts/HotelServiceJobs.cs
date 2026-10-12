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
        int page;
        HotelServiceGuide guide;float guideRefresh;GUIStyle guideText,guideTitle;
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
            foreach(var entry in work.Values){if(entry.bag)Destroy(entry.bag.gameObject);if(entry.linen)Destroy(entry.linen.gameObject);}work.Clear();Completed=BagsDelivered=LaundryReturned=MealsServed=page=0;
        }
        void Update()
        {
            if(!hotel.Running||Keyboard.current==null||hotel.player.combat&&hotel.player.combat.IsDown)return;
            if(Time.time>=guideRefresh){guide=HotelServiceGuidance.Build(this,hotel.player);guideRefresh=Time.time+.15f;}
            if(Keyboard.current.jKey.wasPressedThisFrame)page++;
            if(Keyboard.current.gKey.wasPressedThisFrame){var trolley=hotel.player.cart?hotel.player.cart.GetComponent<HotelLuggageTrolley>():hotel.player.FocusedItem?hotel.player.FocusedItem.GetComponent<HotelLuggageTrolley>():null;if(trolley){if(hotel.player.held)trolley.Load(hotel.player);else trolley.Unload(hotel.player);}}
        }
        public void DrawBoard(float x,Texture2D panel,GUIStyle title,GUIStyle small)
        {
            int count=hotel.Guests.Count,pages=Mathf.Max(1,(count+4)/5);page%=pages;int first=page*5,shown=Mathf.Min(5,count-first);var color=GUI.color;
            GUI.DrawTexture(new Rect(x,18,317,90+shown*84),panel);GUI.Label(new Rect(x+14,26,290,28),"HOTEL JOBS  "+Completed,title);GUI.Label(new Rect(x+14,54,290,24),"J next page "+(page+1)+"/"+pages+"   G load/unload",small);
            for(int i=0;i<shown;i++)
            {
                var guest=hotel.Guests[first+i];if(!guest)continue;float y=88+i*84;
                GUI.color=color;GUI.Label(new Rect(x+14,y,290,21),guest.guestName+" / ROOM "+guest.RoomLabel,small);
                GUI.color=new Color(1,.82f,.38f);GUI.Label(new Rect(x+14,y+21,290,21),guest.Need!=GuestNeed.None?guest.NeedLabel:guest.StatusLabel,small);
                if(work.TryGetValue(guest,out var entry)){GUI.color=new Color(.7f,.92f,.82f);GUI.Label(new Rect(x+14,y+42,290,21),entry.bagDone?"Luggage delivered":"Luggage -> room "+guest.RoomLabel,small);GUI.Label(new Rect(x+14,y+62,290,21),LaundryStatus(guest),small);}
            }
            GUI.color=color;
            DrawGuidance(x,panel,small);
        }
        void DrawGuidance(float boardX,Texture2D panel,GUIStyle small)
        {
            if(guideText==null)guideText=new GUIStyle();if(guideTitle==null)guideTitle=new GUIStyle();
            // Native IMGUI styles can lose their contents with domain/scene reload disabled.
            var guideFont=small.font?small.font:Resources.Load<Font>("HotelTogetherUI/LilitaOne-Regular");
            guideText.font=guideFont;guideText.fontSize=17;guideText.wordWrap=true;guideText.clipping=TextClipping.Clip;guideText.normal.textColor=new Color(.94f,.97f,.98f);
            guideTitle.font=guideFont;guideTitle.fontSize=21;guideTitle.normal.textColor=new Color(1,.86f,.46f);GUI.contentColor=Color.white;
            float width=boardX-350;if(width<300)return;float stepsHeight=guideText.CalcHeight(new GUIContent(guide.steps??""),width-24);float manifestHeight=string.IsNullOrEmpty(guide.manifest)?0:guideText.CalcHeight(new GUIContent(guide.manifest),width-24);float height=61+stepsHeight+(manifestHeight>0?manifestHeight+10:0)+16;var card=new Rect(335,112,width,Mathf.Max(155,height));GUI.DrawTexture(card,panel);GUI.Label(new Rect(card.x+12,card.y+8,width-24,27),guide.title,guideTitle);
            GUI.color=new Color(1,.83f,.39f);if(guide.target.HasValue)GUI.Label(new Rect(card.x+12,card.y+35,width-24,23),HotelServiceGuidance.Direction(hotel,guide.target.Value),guideText);GUI.color=Color.white;GUI.Label(new Rect(card.x+12,card.y+61,width-24,stepsHeight+2),guide.steps,guideText);
            if(!string.IsNullOrEmpty(guide.manifest)){GUI.color=new Color(.72f,.96f,.84f);GUI.Label(new Rect(card.x+12,card.y+71+stepsHeight,width-24,manifestHeight+2),guide.manifest,guideText);GUI.color=Color.white;}
            var m=housekeeping?housekeeping.ActiveMop:null;if(!m&&hotel.player.FocusedItem)m=hotel.player.FocusedItem.GetComponent<HotelFloorMess>();if(m&&m.type==FloorMessType.Wet&&m.Progress>0){GUI.color=new Color(.12f,.7f,.62f);GUI.DrawTexture(new Rect(card.x+12,card.yMax-8,(width-24)*m.Progress,4),Texture2D.whiteTexture);GUI.color=Color.white;}
            DrawRequests(panel,small);
        }
        void DrawRequests(Texture2D panel,GUIStyle small)
        {
            float scale=Mathf.Clamp(Mathf.Min(Screen.width/1200f,Screen.height/700f),.45f,2);foreach(var g in hotel.Guests){if(!g||g.Need==GuestNeed.None||Vector3.Distance(hotel.player.transform.position,g.transform.position)>12)continue;var body=g.transform.position+Vector3.up;var delta=body-hotel.player.view.transform.position;if(Physics.Raycast(hotel.player.view.transform.position,delta.normalized,out var hit,delta.magnitude+.1f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<HotelGuestBot>()!=g)continue;var point=hotel.player.view.WorldToScreenPoint(g.transform.position+Vector3.up*2.1f);if(point.z<=0||point.x<0||point.x>Screen.width)continue;var rect=new Rect(point.x/scale-130,(Screen.height-point.y)/scale-15,260,30);GUI.DrawTexture(rect,panel);GUI.Label(new Rect(rect.x+5,rect.y+4,250,25),"ROOM "+g.RoomLabel+" / "+g.NeedLabel,small);}
        }
    }
}
