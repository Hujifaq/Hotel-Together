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
        public bool Ready=>Batch&&Remaining<=0;
        public bool Use(HotelPlayer player)
        {
            if(Batch)
            {
                if(!Ready){player.Notify("Washing - "+Mathf.CeilToInt(Remaining)+" seconds remaining.");return false;}
                if(player.held||player.cart){player.Notify("Free your hands to collect clean linen.");return false;}
                var linen=Batch;Batch=null;linen.gameObject.SetActive(true);linen.type=GuestDeliveryType.CleanLaundry;linen.Assign(linen.Owner,linen.type);foreach(var renderer in linen.GetComponentsInChildren<Renderer>())if(renderer.name=="Linen cloth")renderer.sharedMaterial=jobs.cleanLinen;linen.transform.SetPositionAndRotation(output.position,output.rotation);return player.RequestPickup(linen.GetComponent<HotelItem>());
            }
            var dirty=player.held?player.held.GetComponent<HotelGuestDelivery>():null;
            if(!dirty||dirty.type!=GuestDeliveryType.DirtyLaundry){player.Notify("Bring a guest's dirty linen from their room.");return false;}
            player.Drop();dirty.transform.SetParent(transform,true);dirty.gameObject.SetActive(false);Batch=dirty;Remaining=washSeconds;player.Notify("Washing linen for Room "+dirty.Owner.RoomLabel+".");return true;
        }
        void Update()
        {
            if(!jobs||!jobs.hotel.Running)return;
            if(Batch&&Remaining>0){Remaining=Mathf.Max(0,Remaining-Time.deltaTime);if(drum)drum.Rotate(0,0,180*Time.deltaTime);if(Remaining<=0)jobs.hotel.player.Notify("Clean linen ready: Room "+Batch.Owner.RoomLabel);}
            if(status)status.text=!Batch?"WASHER / E LOAD":Ready?"CLEAN LINEN / E COLLECT":"ROOM "+Batch.Owner.RoomLabel+" / "+Mathf.CeilToInt(Remaining)+"s";
        }
        public void Cancel(HotelGuestBot guest){if(Batch&&Batch.Owner==guest){Destroy(Batch.gameObject);Batch=null;Remaining=0;}}
        public void ResetMachine(){if(Batch)Destroy(Batch.gameObject);Batch=null;Remaining=0;if(status)status.text="WASHER / E LOAD";}
    }
}
