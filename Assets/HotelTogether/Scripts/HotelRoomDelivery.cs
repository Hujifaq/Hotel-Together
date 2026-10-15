using UnityEngine;
namespace HotelTogether
{
    public sealed class HotelRoomDelivery : MonoBehaviour
    {
        public HotelServiceJobs jobs;
        public int roomIndex;
        public Transform deposit;
        public bool KnockPending {get;private set;}
        float knockRemaining,knockCooldown;
        AudioSource knockSound;
        System.Collections.IEnumerator KnockSound(HotelPlayer player)
        {
            if(!knockSound){knockSound=gameObject.AddComponent<AudioSource>();knockSound.playOnAwake=false;knockSound.spatialBlend=1;knockSound.maxDistance=12;}
            for(int i=0;i<3;i++){if(!player)yield break;player.PlayGesture(HotelPlayerGesture.Interact,transform.position);if(!HotelAudio.Has("knock"))knockSound.PlayOneShot(player.ImpactClip,.35f);else if(i==0)HotelAudio.PlayAt("knock",transform.position,.9f,1,0,16);yield return new WaitForSeconds(.15f);}
        }
        public HotelGuestBot Occupant
        {
            get {foreach(var guest in jobs.hotel.Guests)if(guest&&guest.RoomIndex==roomIndex)return guest;return null;}
        }
        public bool Knock(HotelPlayer player)
        {
            if(!player||player.GameplayPaused||Vector3.Distance(player.transform.position,transform.position)>3.2f)return false;
            if(Time.time<knockCooldown)return false;knockCooldown=Time.time+1.2f;
            if(!Occupant){player.Notify("This room is empty.");return false;}
            KnockPending=true;knockRemaining=30;
            StartCoroutine(KnockSound(player));
            if(player.held&&player.held.GetComponent<HotelGuestDelivery>() is HotelGuestDelivery carried&&carried.type==GuestDeliveryType.Luggage)jobs.DeliverRoom(player,this);
            foreach(var trolley in jobs.trolleys)if(trolley&&Vector3.Distance(trolley.transform.position,transform.position)<3.4f&&trolley.Count>0){jobs.DeliverRoom(player,this);break;}
            jobs.ScanLuggage();player.Notify("Knock knock - Room "+jobs.hotel.rooms[roomIndex].number+". Please wait for the guest.");return true;
        }
        public bool StaffKnock(Vector3 position){if(!Occupant||Vector3.Distance(position,Doorstep)>2.5f||!CanCollect)return false;KnockPending=true;knockRemaining=30;jobs.ScanLuggage();return true;}
        public void Answered(){KnockPending=false;knockRemaining=0;}
        public void CloseAfterEntry(){jobs.hotel.rooms[roomIndex].door.open=false;Answered();}
        void Update()
        {
            if(!jobs||!jobs.hotel.Running)return;
            if(KnockPending){knockRemaining-=Time.deltaTime;if(knockRemaining<=0){Answered();jobs.hotel.player.Notify("No answer yet. Try knocking again when the guest is in their room.");}}
            var door=jobs.hotel.rooms[roomIndex].door;var guest=Occupant;
            if(guest&&door.open&&guest.Activity!=GuestActivity.ToRoom&&guest.Activity!=GuestActivity.ToLuggage&&guest.Activity!=GuestActivity.ReturningLuggage&&guest.Activity!=GuestActivity.ToDoor&&Vector3.Distance(guest.transform.position,new Vector3(door.transform.position.x,guest.transform.position.y,door.transform.position.z))>2.6f)door.open=false;
        }
        public Vector3 Inward
        {
            get {var room=jobs.hotel.rooms[roomIndex];var v=room.standPoint-room.door.transform.position;return Mathf.Abs(v.x)>Mathf.Abs(v.z)?new Vector3(Mathf.Sign(v.x),0,0):new Vector3(0,0,Mathf.Sign(v.z));}
        }
        public Vector3 Doorstep
        {
            get {var room=jobs.hotel.rooms[roomIndex];var p=room.door.transform.position-Inward*.95f+Vector3.Cross(Vector3.up,Inward)*.65f;p.y=room.standPoint.y;return p;}
        }
        public bool CanCollect
        {
            get {var door=jobs.hotel.rooms[roomIndex].door;var defense=door.GetComponent<HotelDefenseOpening>();return !defense||defense.Broken&&defense.BoardCount==0||defense.CanOpen;}
        }
        public void OpenForGuest(){if(CanCollect)jobs.hotel.rooms[roomIndex].door.open=true;}
        public bool Deliver(HotelPlayer player)
        {
            if(Occupant&&Occupant.Activity==GuestActivity.AtDoor&&player.held&&player.held.GetComponent<HotelGuestDelivery>() is HotelGuestDelivery linen&&linen.type==GuestDeliveryType.CleanLaundry)return jobs.DeliverRoom(player,this);
            if(Occupant)return Knock(player);
            var door=jobs.hotel.rooms[roomIndex].door;if(!CanCollect)return false;door.open=!door.open;return true;
        }
    }
}
