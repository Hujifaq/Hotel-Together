using UnityEngine;
namespace HotelTogether
{
    public partial class HotelPlayer
    {
        public HotelGuestPhysics GrabbedGuest { get; private set; }
        GUIStyle guestSpeechStyle;
        public bool GrabFocusedGuest()
        {
            if(held||cart||seated||pendingPickup||transportLocked||GrabbedGuest)return false;
            if(!Physics.Raycast(view.transform.position,view.transform.forward,out var hit,2.6f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Collide))return false;
            if(hit.collider.GetComponentInParent<HotelCash>())return false;var bot=hit.collider.GetComponentInParent<HotelGuestBot>();
            var physics=bot?bot.GetComponent<HotelGuestPhysics>():hit.collider.GetComponent<HotelGuestPhysicsPart>()?.owner;
            if(!physics || Vector3.Distance(hit.point,physics.HeadPoint)>.55f)return false;
            if(!physics.Grab(this))return false;
            GrabbedGuest=physics;PlayGesture(HotelPlayerGesture.Interact,physics.HeadPoint);return true;
        }
        void ReleaseGuest(bool throwIt)
        {
            if(!GrabbedGuest)return;
            var guest=GrabbedGuest;GrabbedGuest=null;
            PlayGesture(throwIt?HotelPlayerGesture.Throw:HotelPlayerGesture.Place,guest.HeadPoint);
            guest.Release(throwIt);
        }
        void UpdateGuestGrip()
        {
            if(GrabbedGuest&&!GrabbedGuest.IsHeld)GrabbedGuest=null;
        }
        bool GuestHandPose(float side,out Vector3 target,out Quaternion rotation,out float curl)
        {
            target=Vector3.zero;rotation=Quaternion.identity;curl=.9f;
            if(!GrabbedGuest)return false;
            target=GrabbedGuest.HeadPoint+view.transform.right*side*.16f;
            rotation=view.transform.rotation*Quaternion.Euler(70,side*25,0);return true;
        }
        void DrawGuestSpeech(Rect safe)
        {
            if(!view||!hudHotel)return;
            if(guestSpeechStyle==null){guestSpeechStyle=new GUIStyle(uiSmall){wordWrap=true,fontSize=16};guestSpeechStyle.normal.textColor=new Color(1,.93f,.82f);}
            foreach(var guest in hudHotel.Guests)
            {
                if(!guest)continue;
                var physics=guest.GetComponent<HotelGuestPhysics>();
                if(!physics||Time.time>=physics.ComplaintUntil)continue;
                var point=physics.HeadPoint+Vector3.up*.4f;
                if(Vector3.Distance(view.transform.position,point)>12)continue;
                var screen=view.WorldToScreenPoint(point);if(screen.z<=0)continue;
                float scale=GUI.matrix.m00;
                var rect=new Rect(Mathf.Clamp(screen.x/scale-180,safe.x,safe.xMax-360),Mathf.Clamp((Screen.height-screen.y)/scale-64,safe.y,safe.yMax-64),360,64);
                Panel(rect,new Color(.12f,.07f,.035f,.94f),14);
                GUI.Label(new Rect(rect.x+12,rect.y+6,rect.width-24,52),guest.guestName+": "+physics.Complaint,guestSpeechStyle);
            }
        }
    }
}
