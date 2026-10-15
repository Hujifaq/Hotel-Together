using UnityEngine;
namespace HotelTogether {
 public enum HotelFeedback {Pickup,Drop,Cash,Footstep,Landing}
 public partial class HotelPlayer {
  AudioSource feedback;AudioClip[] feedbackClips;float stepDistance;bool wasGrounded=true;
  void EnsureFeedback(){if(feedback)return;feedback=gameObject.AddComponent<AudioSource>();feedback.playOnAwake=false;feedback.ignoreListenerPause=true;feedback.spatialBlend=0;feedbackClips=new AudioClip[5];for(int k=0;k<5;k++){int count=k==2?10000:3600;var samples=new float[count];var rng=new System.Random(83+k);for(int i=0;i<count;i++){float t=i/24000f,n=(float)(rng.NextDouble()*2-1);float tone=k==2?Mathf.Sin(2*Mathf.PI*(t<.14f?880:1174)*t)*.32f:Mathf.Sin(t*2*Mathf.PI*(k==4?75:130))*.2f;samples[i]=(n*.28f+tone)*Mathf.Exp(-t*(k==2?8:28));}var clip=AudioClip.Create("Hotel "+(HotelFeedback)k,count,1,24000,false);clip.SetData(samples,0);feedbackClips[k]=clip;}}
  public void PlayFeedback(HotelFeedback cue,float volume=.4f){
   string cueName=cue switch{HotelFeedback.Pickup=>"pickup",HotelFeedback.Drop=>"drop",HotelFeedback.Cash=>"coin",HotelFeedback.Footstep=>HotelAudio.Pick(volume>.28f?"run":"walk",4),_=>"land"};
   if(HotelAudio.Has(cueName)){HotelAudio.Play(cueName,Mathf.Clamp01(volume*(cue==HotelFeedback.Footstep?(volume>.28f?1.7f:1.6f):1.7f)),cue==HotelFeedback.Footstep?Random.Range(.94f,1.06f):Random.Range(.97f,1.03f),cue==HotelFeedback.Footstep?.12f:.05f);return;}
   EnsureFeedback();feedback.pitch=cue==HotelFeedback.Footstep?Random.Range(.92f,1.06f):1;feedback.PlayOneShot(feedbackClips[(int)cue],volume);}
  public AudioClip ImpactClip{get{EnsureFeedback();return feedbackClips[1];}}
  void AdvanceFeedback(float dt){if(dt<=0||!cc||!cc.enabled||seated||transportLocked)return;bool ground=cc.isGrounded;if(ground&&!wasGrounded)PlayFeedback(HotelFeedback.Landing,.35f);wasGrounded=ground;if(!ground){stepDistance=0;return;}var velocity=cc.velocity;velocity.y=0;stepDistance+=velocity.magnitude*dt;if(stepDistance>=(velocity.magnitude>speed*1.1f?.95f:.8f)){stepDistance=0;PlayFeedback(HotelFeedback.Footstep,velocity.magnitude>speed*1.1f?.33f:.23f);}}
  void DisposeFeedback(){if(feedbackClips!=null)foreach(var clip in feedbackClips)HotelAudio.Release(clip);}
 }
 public sealed class HotelPropAudio:MonoBehaviour {
  HotelPlayer owner;AudioSource sound;float next;
  public void Initialize(HotelPlayer p){owner=p;if(!sound){sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=1;sound.minDistance=1;sound.maxDistance=18;}}
  void OnCollisionEnter(Collision c){if(!owner||Time.time<next||c.relativeVelocity.magnitude<1.2f)return;next=Time.time+.2f;if(HotelAudio.Has("impact_soft")){float speed=c.relativeVelocity.magnitude;HotelAudio.PlayAt(speed>5.5f?"impact_hard":"impact_soft",c.GetContact(0).point,Mathf.Clamp(speed*.12f,.2f,.9f),Random.Range(.88f,1.12f)*(GetComponent<HotelFirearm>()?.85f:1),.06f,26);return;}sound.pitch=GetComponent<HotelFirearm>()?.85f:1;sound.PlayOneShot(owner.ImpactClip,Mathf.Clamp(c.relativeVelocity.magnitude*.07f,.12f,.6f));}
 }
}
