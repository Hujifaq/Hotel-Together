using UnityEngine;
namespace HotelTogether
{
    public sealed class HotelServiceLift : MonoBehaviour
    {
        public HotelGuestSimulation hotel;
        public HotelServiceLift destination;
        public Vector3 arrivalPoint;
        public float arrivalHeading=135;
        public string floorName;
        float remaining;
        HotelPlayer rider;
        public bool Busy=>rider!=null;
        public bool Use(HotelPlayer player)
        {
            if(Busy||destination.Busy||Vector3.Distance(player.transform.position,transform.position)>3.2f)return false;
            if(player.IsReachingForItem||player.seated||player.combat&&player.combat.IsDown)return false;
            if(hotel.IsNight){player.Notify("Service lift locked during the zombie alarm.");return false;}
            rider=player;remaining=2.5f;player.transportLocked=true;player.Notify("Service lift to "+destination.floorName);return true;
        }
        void Update()
        {
            if(!rider||!hotel.Running)return;remaining-=Time.deltaTime;if(remaining>0)return;
            var player=rider;rider=null;var cc=player.GetComponent<CharacterController>();cc.enabled=false;
            var offset=destination.arrivalPoint-player.transform.position;player.transform.position=destination.arrivalPoint;
            player.Face(destination.arrivalHeading);
            if(player.held){var body=player.held.GetComponent<Rigidbody>();body.position+=offset;player.held.transform.position=body.position;}
            if(player.cart){var trolley=player.cart.GetComponent<HotelLuggageTrolley>();var body=player.cart.GetComponent<Rigidbody>();body.position=trolley?trolley.PositionFor(player.transform.position,player.transform.eulerAngles.y):body.position+offset;body.rotation=Quaternion.Euler(0,destination.arrivalHeading-90,0);player.cart.transform.SetPositionAndRotation(body.position,body.rotation);}
            cc.enabled=true;Physics.SyncTransforms();player.transportLocked=false;player.Notify("Arrived at "+destination.floorName+". E release trolley / deliver at room.");
        }
        void OnGUI(){if(!rider||!hotel.Running)return;var old=GUI.color;GUI.color=new Color(0,0,0,.96f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;GUI.Label(new Rect(Screen.width*.35f,Screen.height*.45f,500,100),"SERVICE LIFT\n"+floorName+" -> "+destination.floorName+"\n"+Mathf.CeilToInt(remaining)+"s");GUI.color=old;}
        public void ResetLift(){if(rider)rider.transportLocked=false;rider=null;remaining=0;}
        void OnDisable(){ResetLift();}
    }
}
