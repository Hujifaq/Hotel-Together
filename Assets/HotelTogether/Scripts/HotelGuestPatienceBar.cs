using UnityEngine;
namespace HotelTogether
{
    // Reception time belongs to the guest, independently of money and head-grip state.
    public sealed class HotelGuestPatienceBar : MonoBehaviour
    {
        HotelGuestBot guest;HotelHuman human;GUIStyle label;
        void Awake(){guest=GetComponent<HotelGuestBot>();human=GetComponent<HotelHuman>();}
        void OnGUI()
        {
            if(!guest||!guest.Hotel||!guest.AwaitingCheckIn||guest.Need==GuestNeed.Shelter||guest.Need==GuestNeed.Rescue)return;
            if(!human)human=GetComponent<HotelHuman>();if(human&&!human.Targetable)return;
            var player=guest.Hotel.player;if(!player||!player.view||player.GameplayPaused)return;
            var head=guest.PrankPhysics?guest.PrankPhysics.HeadPoint:guest.visual.head.position;
            var delta=head-player.view.transform.position;float distance=delta.magnitude;if(distance>10)return;
            var screen=player.view.WorldToScreenPoint(head+Vector3.up*.65f);if(screen.z<=0||screen.x<0||screen.x>Screen.width||screen.y<0||screen.y>Screen.height)return;
            if(Physics.Raycast(player.view.transform.position,delta.normalized,out var hit,Mathf.Max(0,distance-.15f),~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(guest.transform)&&hit.collider.GetComponent<HotelGuestPhysicsPart>()?.owner!=guest.PrankPhysics)return;
            float ratio=Mathf.Clamp01(guest.ReceptionRemaining/guest.ReceptionLimit);int seconds=Mathf.CeilToInt(guest.ReceptionRemaining);float width=126;var rect=new Rect(screen.x-width*.5f,Screen.height-screen.y,width,6);var old=GUI.color;
            GUI.color=new Color(.06f,.05f,.04f,.9f);GUI.DrawTexture(new Rect(rect.x-2,rect.y-2,width+4,10),Texture2D.whiteTexture);
            GUI.color=ratio>.45f?new Color(.28f,.8f,.5f):ratio>.2f?new Color(1,.7f,.2f):new Color(.95f,.23f,.17f);GUI.DrawTexture(new Rect(rect.x,rect.y,width*ratio,6),Texture2D.whiteTexture);
            if(label==null)label=new GUIStyle(GUI.skin.label){fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.LowerCenter};GUI.color=Color.white;
            string action=guest.Payment&&guest.Payment.Transferring?"PAYMENT":guest.Payment&&guest.Payment.Pending?"ACCEPT CASH":"WAITING";
            GUI.Label(new Rect(rect.x-25,rect.y-23,width+50,22),action+"  "+(seconds/60)+":"+(seconds%60).ToString("00"),label);GUI.color=old;
        }
    }
}
