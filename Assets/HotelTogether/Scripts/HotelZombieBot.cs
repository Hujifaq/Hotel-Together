using UnityEngine;
using UnityEngine.AI;
namespace HotelTogether
{
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class HotelZombieBot : MonoBehaviour
    {
        public HotelGuestVisual visual;
        public float Health {get;private set;}
        public float MaxHealth {get;private set;}
        public bool Alive=>Health>0;
        public HotelHuman Target {get;private set;}
        public string State {get;private set;}="Approaching";
        public NavMeshAgent Agent {get;private set;}
        HotelZombieDirector director;HotelDefenseOpening entry,assault;
        bool inside,assaultFromInside;float think,attackTime,stunned,animationTime,deathStarted;Vector3 lastPosition;
        Vector3 visualHome;Quaternion visualRotation;
        public void Initialize(HotelZombieDirector owner,float health)
        {director=owner;Health=MaxHealth=health;Agent=GetComponent<NavMeshAgent>();Agent.speed=1.1f+Mathf.Min(.45f,(owner.hotel.DayNumber-1)*.045f);lastPosition=transform.position;if(visual){visualHome=visual.transform.localPosition;visualRotation=visual.transform.localRotation;}var bar=GetComponent<HotelTargetHealthBar>();if(!bar)bar=gameObject.AddComponent<HotelTargetHealthBar>();bar.Initialize(null,this,owner.hotel.player);}
        public void Damage(float amount,Vector3 direction)
        {
            if(!Alive||!director||!float.IsFinite(amount)||amount<=0)return;Health=Mathf.Max(0,Health-amount);stunned=Time.time+.30f;HotelAudio.PlayAt(Health>0?"zombie_hit":"zombie_die",transform.position,.95f,Random.Range(.9f,1.1f),.05f,40);
            if(Agent.isOnNavMesh){Agent.isStopped=true;var filter=new NavMeshQueryFilter{agentTypeID=Agent.agentTypeID,areaMask=Agent.areaMask};if(NavMesh.SamplePosition(transform.position+direction.normalized*.18f,out var hit,.4f,filter))Agent.Warp(hit.position);}
            if(!Alive){deathStarted=Time.time;Agent.enabled=false;foreach(var collider in GetComponents<Collider>())collider.enabled=false;State="Defeated";if(visual)gameObject.AddComponent<HotelZombieDeathPhysics>().Initialize(visual,direction);director.Kill(this);Destroy(gameObject,20);}
        }
        float groanTime=-1;
        void Update()
        {
            if(groanTime<0)groanTime=Time.time+Random.Range(1f,8f);
            if(!director||!Alive||!Agent.enabled||!Agent.isOnNavMesh)return;
            if(!director.Running||!director.IsNight||Time.time<stunned){Agent.isStopped=true;return;}
            if(Time.time>=groanTime){groanTime=Time.time+Random.Range(5f,12f);HotelAudio.PlayAt(HotelAudio.Pick("zombie_groan",3),transform.position,.7f,Random.Range(.85f,1.12f),.4f,34);}
            Agent.isStopped=false;if(Time.time>=think){think=Time.time+.45f;Think();}
            if(TryAttackHuman())return;
            if(assault)
            {
                if(!assault.Blocks){assault=null;think=0;return;}
                if(Vector3.Distance(transform.position,assaultFromInside?assault.insidePoint:assault.outsidePoint)<.45f)
                {Agent.isStopped=true;State="Breaking in";Face(assault.transform.position);if(Time.time>=attackTime){attackTime=Time.time+1.1f;HotelAudio.PlayAt("zombie_attack",transform.position,.85f,Random.Range(.9f,1.1f),.1f,30);assault.TakeAttack(16+Mathf.Min(12,director.hotel.DayNumber-1));}}
                return;
            }
        }
        bool TryAttackHuman()
        {
            if(Target&&Target.Targetable)
            {
                var delta=Target.transform.position-transform.position;
                if(new Vector2(delta.x,delta.z).magnitude<1.4f&&Mathf.Abs(delta.y)<1.8f)
                {
                    Face(Target.transform.position);
                    var origin=transform.position+Vector3.up*1.1f;var end=Target.transform.position+Vector3.up*1.1f;
                    bool clear=!Physics.Linecast(origin,end,out var hit,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<HotelHuman>()==Target;
                    if(clear){Agent.isStopped=true;State="Attacking";if(Time.time>=attackTime){attackTime=Time.time+1.2f;HotelAudio.PlayAt("zombie_attack",transform.position,.85f,Random.Range(.9f,1.1f),.1f,30);Target.Damage(12+Mathf.Min(12,director.hotel.DayNumber-1),transform.position);}return true;}
                }
            }
            return false;
        }
        void Think()
        {
            Target=director.NearestHuman(transform.position);if(!Target){Agent.isStopped=true;State="Searching";return;}
            bool targetOutside=Target.transform.position.y<2&&(Target.transform.position.z>7.2f||Target.transform.position.z< -33.2f||Target.transform.position.x< -27.2f||Target.transform.position.x>29.2f);
            if(targetOutside)
            {
                var query=new NavMeshQueryFilter{agentTypeID=Agent.agentTypeID,areaMask=Agent.areaMask};var direct=new NavMeshPath();
                if(NavMesh.SamplePosition(Target.transform.position,out var surface,1.5f,query)&&Agent.CalculatePath(surface.position,direct)&&direct.status==NavMeshPathStatus.PathComplete){assault=null;Agent.SetPath(direct);State="Hunting humans outside";return;}
            }
            if(!inside)
            {
                if(!entry)entry=director.ChooseOpening(Agent,Target,true);
                if(!entry){State="Searching for an entrance";return;}
                if(Vector3.Dot(transform.position-entry.transform.position,(entry.outsidePoint-entry.insidePoint).normalized)<-.5f)inside=true;
                else {assaultFromInside=false;assault=entry.Blocks?entry:null;Agent.SetDestination(assault?entry.outsidePoint:entry.insidePoint);State=assault?"Approaching opening":"Entering breach";return;}
            }
            Vector3 goal=Target.transform.position;
            if(Target.Guest&&Target.Guest.CheckedIn&&Target.Guest.Activity==GuestActivity.Sleeping)goal=Target.Guest.Hotel.rooms[Target.Guest.RoomIndex].standPoint;
            var filter=new NavMeshQueryFilter{agentTypeID=Agent.agentTypeID,areaMask=Agent.areaMask};var path=new NavMeshPath();
            if(NavMesh.SamplePosition(goal,out var hit,1.5f,filter)&&Agent.CalculatePath(hit.position,path)&&path.status==NavMeshPathStatus.PathComplete){assault=null;Agent.SetPath(path);State="Hunting humans";}
            else{assaultFromInside=targetOutside;assault=director.ChooseOpening(Agent,Target,targetOutside,targetOutside);if(assault){Agent.SetDestination(assaultFromInside?assault.insidePoint:assault.outsidePoint);State="Approaching shelter door";}else State="Searching";}
        }
        void Face(Vector3 point){var direction=point-transform.position;direction.y=0;if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(direction);}
        void LateUpdate()
        {
            if(!director||!visual)return;float dt=director.Running?Time.deltaTime:0;if(dt<=0)return;
            if(!Alive)return;
            animationTime+=dt;var delta=transform.position-lastPosition;lastPosition=transform.position;float speed=delta.sqrMagnitude>1?0:delta.magnitude/Mathf.Max(.001f,Time.deltaTime);
            visual.Pose(director.Running?speed:0,dt,false,false,false,.4f,false);float reach=State=="Breaking in"||State=="Attacking"?Mathf.Sin(animationTime*6)*18:Mathf.Sin(animationTime*2)*5;
            visual.leftArm.localRotation=Quaternion.Euler(-68+reach,0,15);visual.rightArm.localRotation=Quaternion.Euler(-73-reach,0,-15);visual.leftForearm.localRotation=Quaternion.Euler(-18,0,0);visual.rightForearm.localRotation=Quaternion.Euler(-22,0,0);visual.head.localRotation=Quaternion.Euler(8,0,Mathf.Sin(animationTime)*6);
            if(Time.time<stunned){float recoil=Mathf.Sin((stunned-Time.time)/.30f*Mathf.PI);visual.head.localRotation*=Quaternion.Euler(-12*recoil,0,0);visual.hips.localPosition+=Vector3.back*(recoil*.055f);}
        }
        void OnCollisionEnter(Collision collision)
        {var rb=collision.rigidbody;if(!rb||rb.gameObject.layer==9||!rb.GetComponent<HotelWeapon>()||collision.relativeVelocity.magnitude<3)return;Damage(Mathf.Min(35,collision.relativeVelocity.magnitude*4),(transform.position-collision.transform.position).normalized);}
    }
}
