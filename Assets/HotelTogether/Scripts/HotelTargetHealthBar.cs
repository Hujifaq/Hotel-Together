using UnityEngine;
namespace HotelTogether {
 public sealed class HotelTargetHealthBar:MonoBehaviour {
  HotelHuman human;HotelZombieBot zombie;HotelPlayer player;Camera view;float trailing=1,deathAt=-1;GUIStyle label;
  public bool Visible=>MaxHealth>0&&Health<MaxHealth&&(Health>0||deathAt>=0&&Time.time-deathAt<2);
  public float Health=>human?human.Health:zombie?zombie.Health:0;
  public float MaxHealth=>human?human.maxHealth:zombie?zombie.MaxHealth:0;
  public void Initialize(HotelHuman h,HotelZombieBot z,HotelPlayer p){human=h;zombie=z;player=p;view=p?p.view:null;trailing=1;deathAt=-1;}
  void Update(){if(MaxHealth<=0)return;float ratio=Health/MaxHealth;trailing=Mathf.MoveTowards(trailing,ratio,Time.deltaTime*.6f);if(Health<=0&&deathAt<0)deathAt=Time.time;if(Health>0)deathAt=-1;}
  Vector3 Head=>human&&human.Guest&&human.Guest.PrankPhysics?human.Guest.PrankPhysics.HeadPoint:zombie&&zombie.visual?zombie.visual.head.position:transform.position+Vector3.up*1.75f;
  void OnGUI(){if(!Visible||!view||player&&player.GameplayPaused)return;var head=Head;var delta=head-view.transform.position;float distance=delta.magnitude;if(distance>42)return;var s=view.WorldToScreenPoint(head+Vector3.up*.35f);if(s.z<=0||s.x<0||s.x>Screen.width||s.y<0||s.y>Screen.height)return;
   if(Physics.Raycast(view.transform.position,delta.normalized,out var hit,Mathf.Max(0,distance-.12f),~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(transform)&&!(human&&human.Guest&&hit.collider.GetComponent<HotelGuestPhysicsPart>()?.owner==human.Guest.PrankPhysics))return;
   float width=Mathf.Clamp(110-distance*1.2f,66,110),alpha=Health>0?1:Mathf.Clamp01(2-(Time.time-deathAt));var old=GUI.color;var rect=new Rect(s.x-width*.5f,Screen.height-s.y,width,8);GUI.color=new Color(.035f,.04f,.045f,.9f*alpha);GUI.DrawTexture(new Rect(rect.x-2,rect.y-2,width+4,12),Texture2D.whiteTexture);GUI.color=new Color(1,.72f,.25f,alpha);GUI.DrawTexture(new Rect(rect.x,rect.y,width*trailing,8),Texture2D.whiteTexture);GUI.color=zombie?new Color(.9f,.2f,.17f,alpha):new Color(.23f,.83f,.64f,alpha);GUI.DrawTexture(new Rect(rect.x,rect.y,width*Mathf.Clamp01(Health/MaxHealth),8),Texture2D.whiteTexture);
   if(label==null)label=new GUIStyle(GUI.skin.label){alignment=TextAnchor.LowerCenter,fontSize=12,fontStyle=FontStyle.Bold};GUI.color=new Color(1,1,1,alpha);GUI.Label(new Rect(rect.x-40,rect.y-24,width+80,22),(human&&human.Guest?human.Guest.guestName:"ZOMBIE")+"  "+Mathf.CeilToInt(Health),label);GUI.color=old;
  }
 }
}
