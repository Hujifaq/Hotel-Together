using UnityEngine;
namespace HotelTogether
{
    public readonly struct HotelServiceGuide
    {
        public readonly string title, steps, manifest;
        public readonly Vector3? target;
        public HotelServiceGuide(string title,string steps,Vector3? target=null,string manifest=null){this.title=title;this.steps=steps;this.target=target;this.manifest=manifest;}
    }
    // Derived from the actual carried cargo, customer order and machine stage.
    public static class HotelServiceGuidance
    {
        static HotelServiceGuide Linen(HotelServiceJobs jobs,HotelGuestDelivery delivery)
        {
            if(!delivery.Owner)return new HotelServiceGuide("GUEST ITEM","This guest has left. The hotel will clear this item.");
            string room=delivery.Owner.RoomLabel;
            if(delivery.type==GuestDeliveryType.DirtyLaundry)
            {
                var washer=jobs.washers[0];foreach(var w in jobs.washers)if(w&&!w.Batch){washer=w;break;}
                return new HotelServiceGuide("DIRTY LINEN / ROOM "+room,"1. Take this bundle to LAUNDRY, Lobby back right.\n2. E load washer, then add a laundry detergent dose.\n3. Wait, collect clean linen and return to Room "+room+".",washer.transform.position);
            }
            return new HotelServiceGuide((delivery.type==GuestDeliveryType.Luggage?"LUGGAGE":"CLEAN LINEN")+" / ROOM "+room,"Take the lift to 2. Open the room door.\nE at the room delivery plaque to hand over this tagged item.\nThe guest name and room must match.",jobs.receivers[delivery.Owner.RoomIndex].transform.position);
        }
        public static HotelServiceGuide Build(HotelServiceJobs jobs,HotelPlayer p)
        {
            var focus=p.FocusedItem;var held=p.held;var cleaning=jobs.housekeeping;
            var mess=focus?focus.GetComponent<HotelFloorMess>():null;
            if(mess&&!mess.Cleared)return new HotelServiceGuide(mess.type==FloorMessType.Wet?(held&&held.GetComponent<HotelCleaningTool>()?"DRYING FLOOR / "+Mathf.RoundToInt(mess.Progress*100)+"%":"WET FLOOR / MOP REQUIRED"):"FLOOR LITTER",mess.type==FloorMessType.Wet?"G at a housekeeping cart takes its mop. Hold E on this wet spot to dry it.\nA saturated mop needs rinsing: hold it, E at the cart bucket.":"Free your hands and E to pick up the litter.\nCarry it to a sorting bin or housekeeping cart; E disposes it.\nDropping it on the floor does not complete the job.",mess.transform.position);
            var cleaningCart=focus?focus.GetComponent<HotelCleaningCart>():null;
            if(!cleaningCart&&p.cart)cleaningCart=p.cart.GetComponent<HotelCleaningCart>();
            if(cleaningCart)return new HotelServiceGuide("HOUSEKEEPING CART / WASTE "+cleaningCart.Waste+"/"+cleaningCart.wasteCapacity,"E push / park. G take or return the original mop.\nHold a mop and E here to rinse. Hold litter and E to bag it.\nPush a full cart to a sorting bin and E to empty.");
            var bin=focus?focus.GetComponent<HotelWasteBin>():null;
            if(bin)return new HotelServiceGuide("SORTING BIN","Hold floor litter and E to dispose.\nOr push a housekeeping cart here and E to empty its waste.",bin.transform.position);
            var litter=held?held.GetComponent<HotelFloorMess>():null;
            if(litter&&cleaning){var cart=cleaning.NearestCart(p.transform.position);Vector3 destination=cart?cart.transform.position:p.transform.position;foreach(var b in cleaning.bins)if(b&&Vector3.Distance(p.transform.position,b.transform.position)<Vector3.Distance(p.transform.position,destination))destination=b.transform.position;return new HotelServiceGuide("LITTER / TAKE TO A BIN","E at a sorting bin or housekeeping cart to dispose.\nQ puts it down and leaves the cleaning job unfinished.",destination);}
            var washer=focus?focus.GetComponent<HotelLaundryMachine>():null;
            if(washer)
            {
                if(washer.NeedsDetergent){HotelDetergentBottle bottle=null;if(cleaning)foreach(var b in cleaning.detergent)if(b&&b.Doses>0&&(!bottle||Vector3.Distance(p.transform.position,b.transform.position)<Vector3.Distance(p.transform.position,bottle.transform.position)))bottle=b;return new HotelServiceGuide("ROOM "+washer.Batch.Owner.RoomLabel+" / ADD DETERGENT","The tagged linen is already loaded.\nPick up laundry detergent from the laundry shelf, then E here.\nThe machine will not start without a dose.",bottle?bottle.transform.position:washer.transform.position);}
                if(washer.Ready)return new HotelServiceGuide("CLEAN LINEN / ROOM "+washer.Batch.Owner.RoomLabel,"Free your hands and E to collect.\nTake the lift to 2 and return it at that room's delivery plaque.",washer.transform.position);
                if(washer.Batch)return new HotelServiceGuide("ROOM "+washer.Batch.Owner.RoomLabel+" / WASHING",Mathf.CeilToInt(washer.Remaining)+"s remaining. Come back here to collect the clean bundle.\nReturn it to its original room; other rooms reject it.",washer.transform.position);
                return new HotelServiceGuide("LAUNDRY / LOAD TAGGED LINEN","Collect dirty linen from a guest room shelf. E here to load.\nAdd laundry detergent from the laundry shelf.\nWait 20s, collect and return to the same room.",washer.transform.position);
            }
            var delivery=held?held.GetComponent<HotelGuestDelivery>():null;
            if(delivery)return Linen(jobs,delivery);
            var trolley=p.cart?p.cart.GetComponent<HotelLuggageTrolley>():focus?focus.GetComponent<HotelLuggageTrolley>():null;
            if(!trolley&&!held)foreach(var nearby in jobs.trolleys)if(nearby&&nearby.Count>0&&Vector3.Distance(p.transform.position,nearby.transform.position)<4){trolley=nearby;break;}
            if(trolley)
            {
                if(trolley.Count>0){HotelGuestDelivery next=null;foreach(var bag in trolley.Loaded)if(bag&&bag.Owner&&(!next||Vector3.Distance(p.transform.position,jobs.receivers[bag.Owner.RoomIndex].transform.position)<Vector3.Distance(p.transform.position,jobs.receivers[next.Owner.RoomIndex].transform.position)))next=bag;return new HotelServiceGuide("LUGGAGE DELIVERY ROUTE / "+trolley.Count+" BAGS","Take the lift to 2. Open each room's door.\nE at its delivery plaque transfers only matching luggage.\nG unloads a bag after you park the trolley.",next?jobs.receivers[next.Owner.RoomIndex].transform.position:(Vector3?)null,trolley.RouteSummary);}
                return new HotelServiceGuide("LUGGAGE TROLLEY / EMPTY","Pick up a guest's tagged suitcase at reception.\nE at this trolley loads it; E with empty hands pushes.\nThe delivery room list appears here as soon as bags are loaded.",jobs.luggageDrop.position);
            }
            var mop=held?held.GetComponent<HotelCleaningTool>():null;
            if(mop&&cleaning){var cart=cleaning.NearestCart(p.transform.position);var next=cleaning.NearestMess(p.transform.position);return new HotelServiceGuide("MOP / CAPACITY "+mop.UsesLeft+"/"+mop.capacity,mop.UsesLeft>0?"Aim at a wet spot and HOLD E until the floor is dry.\nE at a housekeeping cart rinses the mop; G parks it.\nCompleted housekeeping earns $5.":"Mop saturated. Hold it and E at the housekeeping cart to rinse.\nThen return to the wet floor.",mop.UsesLeft<=0&&cart?cart.transform.position:next?next.transform.position:(Vector3?)null);}
            var bottleHeld=held?held.GetComponent<HotelDetergentBottle>():null;
            if(bottleHeld)return new HotelServiceGuide("LAUNDRY DETERGENT / "+bottleHeld.Doses+" DOSES","E at a washer adds one dose. The bottle stays in your hand.\nA loaded washer starts after detergent is added.\nQ puts the bottle down; empty bottles need replacing.",jobs.washers[0].transform.position);
            var guest=focus?focus.GetComponent<HotelGuestBot>():null;
            if(guest&&guest.Need==GuestNeed.CheckIn)return new HotelServiceGuide("RECEPTION / CHECK IN","E serves the first guest in the reception queue.\nCheck-in assigns a room and creates labeled luggage and laundry jobs.",jobs.hotel.receptionPoint);
            if(guest&&guest.Need==GuestNeed.CheckOut)return new HotelServiceGuide("RECEPTION / CHECK OUT","E at the first guest in the reception queue completes checkout.\nFinish their delivery jobs before they leave.",jobs.hotel.receptionPoint);
            if(guest&&guest.Need==GuestNeed.Luggage)return new HotelServiceGuide("ROOM "+guest.RoomLabel+" / LUGGAGE","Collect this guest's tagged suitcase at reception, or load it on a trolley.\nTake the lift to 2 and E at that room's delivery plaque.",jobs.luggageDrop.position);
            if(guest&&(guest.Need==GuestNeed.Rescue||guest.Need==GuestNeed.Shelter))return new HotelServiceGuide("GUEST SAFETY / "+guest.NeedLabel,"Rescue a fallen guest with medicine; E asks a mobile guest to follow.\nUse the first-aid cabinets for medicine and protect the hotel entrances.",guest.transform.position);
            if(!guest)foreach(var g in jobs.hotel.Guests)if(g&&g.Need==GuestNeed.Meal&&(!guest||g.WaitSeconds>guest.WaitSeconds))guest=g;
            var tray=held?held.GetComponent<HotelServiceTray>():null;
            if(guest&&guest.Need==GuestNeed.Meal)
            {
                Vector3 station=Vector3.zero;foreach(var s in jobs.hotel.stations)if(s.prefab&&s.prefab.GetComponent<HotelGuestSupply>().type==GuestNeed.Meal&&s.prefab.GetComponent<HotelGuestSupply>().dish==guest.RequestedDish)station=s.point.position;
                bool matching=false;if(tray)foreach(var food in tray.Contents)if(food&&food.type==GuestNeed.Meal&&food.dish==guest.RequestedDish)matching=true;
                return new HotelServiceGuide(guest.guestName+" / ROOM "+guest.RoomLabel+" / "+HotelGuestSupply.DishName(guest.RequestedDish).ToUpperInvariant(),"1. Pick up a tray at the Lobby GUEST SERVICE counter.\n2. E load "+HotelGuestSupply.DishName(guest.RequestedDish)+" from its labeled plate station.\n3. E at this guest in the dining area. Wrong dishes are rejected.",matching?guest.transform.position:tray?station:jobs.trays[0].transform.position,tray?TrayManifest(tray):null);
            }
            if(tray)return new HotelServiceGuide("SERVICE TRAY / "+tray.Count+" PORTIONS","Load meals or water at the Lobby GUEST SERVICE counter.\nThe job board shows each guest's exact order.\nE at the matching guest serves one portion and keeps this tray.",jobs.hotel.stations[0].point.position,TrayManifest(tray));
            if(guest&&guest.Need==GuestNeed.Water||guest&&guest.Need==GuestNeed.Towel){var need=guest.Need;foreach(var s in jobs.hotel.stations)if(s.prefab.GetComponent<HotelGuestSupply>().type==need)return new HotelServiceGuide(guest.guestName+" / "+guest.NeedLabel,"Collect "+guest.NeedLabel+" from the Lobby GUEST SERVICE counter.\nCarry it or load water onto a tray, then E at this guest.",s.point.position);}
            if(cleaning&&cleaning.Pending>0){var next=cleaning.NearestMess(p.transform.position);return new HotelServiceGuide("HOUSEKEEPING / "+cleaning.Pending+" SPOTS",next?next.Requirement:"Check the floor for litter and water.",next?next.transform.position:(Vector3?)null);}
            return new HotelServiceGuide("HOTEL SERVICE","J opens your jobs. LEFT / RIGHT browses guests.\nTagged luggage goes to its room; dirty linen goes to LAUNDRY.\nFood trays and labeled meals are at the Lobby GUEST SERVICE counter.");
        }
        static string TrayManifest(HotelServiceTray tray){var text=new System.Text.StringBuilder();foreach(var supply in tray.Contents)if(supply){if(text.Length>0)text.Append(" / ");text.Append(supply.Label);}return text.Length>0?"ON TRAY: "+text:"Tray empty";}
        public static string Direction(HotelGuestSimulation hotel,Vector3 point)
        {
            var delta=point-hotel.player.transform.position;int floor=hotel.elevators.Length>0?hotel.elevators[0].FloorFor(point.y):0;string level=floor==0?"LOBBY":(floor+1).ToString();
            if(Mathf.Abs(delta.y)>2)return "DESTINATION: "+level+" / take the elevator";
            float angle=Vector3.SignedAngle(hotel.player.transform.forward,new Vector3(delta.x,0,delta.z),Vector3.up);string direction=Mathf.Abs(angle)<30?"ahead":Mathf.Abs(angle)>145?"behind":angle>0?"right":"left";
            return "DESTINATION: "+level+" / "+Mathf.CeilToInt(new Vector2(delta.x,delta.z).magnitude)+"m "+direction;
        }
    }
}
