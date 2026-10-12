using UnityEngine;
namespace HotelTogether
{
    public sealed class HotelLaundryMachine : MonoBehaviour
    {
        public HotelServiceJobs jobs;
        public Transform output,drum;
        public TextMesh status;
        public float washSeconds=20;
        public HotelGuestDelivery Batch { get; private set; }
        public float Remaining { get; private set; }
        public int DetergentCharges { get; private set; }
        public bool WashingStarted { get; private set; }
        public bool NeedsDetergent => Batch && !WashingStarted;
        public bool Ready => Batch && WashingStarted && Remaining<=0;
        public string ActionLabel => NeedsDetergent?"ADD LAUNDRY DETERGENT":Ready?"COLLECT CLEAN LINEN":Batch?"WASHING / PLEASE WAIT":"LOAD DIRTY LINEN";
        bool AddDetergent(HotelPlayer player)
        {
            var bottle=player.held?player.held.GetComponent<HotelDetergentBottle>():null;
            if(!bottle){player.Notify("Needs laundry detergent. Get the bottle from the LAUNDRY shelves, then E at this washer.");return false;}
            if(DetergentCharges>=3){player.Notify("Washer already has three detergent doses.");return false;}
            if(!bottle.UseDose()){player.Notify("This detergent bottle is empty. Get another bottle from the laundry shelf.");return false;}
            DetergentCharges++;if(NeedsDetergent)StartWash();else player.Notify("Detergent added. Bring tagged dirty linen from a guest room.");return true;
        }
        void StartWash(){if(!Batch||DetergentCharges<=0)return;DetergentCharges--;WashingStarted=true;Remaining=washSeconds;jobs.hotel.player.Notify("Washing Room "+Batch.Owner.RoomLabel+" linen. Collect here after "+washSeconds+"s, then return it to that room.");}
        public bool Use(HotelPlayer player)
        {
            if(Vector3.Distance(player.transform.position,transform.position)>3.2f)return false;
            if(Batch)
            {
                if(!Batch.Owner){ResetMachine();return false;}
                if(NeedsDetergent)return AddDetergent(player);
                if(!Ready){player.Notify("Room "+Batch.Owner.RoomLabel+" washing: "+Mathf.CeilToInt(Remaining)+"s left. Return here to collect.");return false;}
                if(player.held||player.cart){player.Notify("Free your hands to collect Room "+Batch.Owner.RoomLabel+" clean linen.");return false;}
                var linen=Batch;linen.gameObject.SetActive(true);linen.Assign(linen.Owner,GuestDeliveryType.CleanLaundry);
                foreach(var renderer in linen.GetComponentsInChildren<Renderer>())if(renderer.name=="Linen cloth")renderer.sharedMaterial=jobs.cleanLinen;
                linen.transform.SetPositionAndRotation(output.position,output.rotation);
                if(!player.RequestPickup(linen.GetComponent<HotelItem>()))return false;
                Batch=null;WashingStarted=false;Remaining=0;return true;
            }
            if(player.held&&player.held.GetComponent<HotelDetergentBottle>())return AddDetergent(player);
            var dirty=player.held?player.held.GetComponent<HotelGuestDelivery>():null;
            if(!dirty||dirty.type!=GuestDeliveryType.DirtyLaundry||!dirty.Owner){player.Notify("Bring tagged DIRTY LINEN from a guest room shelf. This washer is in the Lobby laundry room.");return false;}
            player.Drop();dirty.transform.SetParent(transform,true);dirty.gameObject.SetActive(false);Batch=dirty;WashingStarted=false;Remaining=0;
            if(DetergentCharges>0)StartWash();else player.Notify("Room "+dirty.Owner.RoomLabel+" linen loaded. Add laundry detergent from the shelf to start washing.");return true;
        }
        void Update()
        {
            if(!jobs||!jobs.hotel.Running)return;
            if(Batch&&!Batch.Owner){ResetMachine();return;}
            if(Batch&&WashingStarted&&Remaining>0){Remaining=Mathf.Max(0,Remaining-Time.deltaTime);if(drum)drum.Rotate(0,0,180*Time.deltaTime);if(Remaining<=0)jobs.hotel.player.Notify("Clean linen ready: Room "+Batch.Owner.RoomLabel+". Collect at the Lobby laundry room.");}
            if(status)status.text=!Batch?"WASHER\nE LOAD LINEN":NeedsDetergent?"ROOM "+Batch.Owner.RoomLabel+"\nADD DETERGENT":Ready?"ROOM "+Batch.Owner.RoomLabel+"\nE COLLECT":"ROOM "+Batch.Owner.RoomLabel+" / "+Mathf.CeilToInt(Remaining)+"s";
        }
        public void Cancel(HotelGuestBot guest){if(Batch&&Batch.Owner==guest)ResetMachine();}
        public void ResetMachine(){if(Batch)Destroy(Batch.gameObject);Batch=null;Remaining=0;WashingStarted=false;DetergentCharges=0;if(status)status.text="WASHER / E LOAD";}
    }
}
