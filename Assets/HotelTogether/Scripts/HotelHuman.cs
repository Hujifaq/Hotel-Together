using UnityEngine;
namespace HotelTogether
{
    public sealed class HotelHuman : MonoBehaviour
    {
        public float maxHealth=100;
        public float Health { get; private set; }=100;
        public bool Downed=>Health<=0&&!Dead;
        public bool Dead { get; private set; }
        public bool Targetable=>!Dead&&!Downed;
        public HotelPlayer Player { get; private set; }
        public HotelGuestBot Guest { get; private set; }
        public HotelZombieDirector Director { get; private set; }
        float bleedRemaining;
        void Awake(){Player=GetComponent<HotelPlayer>();Guest=GetComponent<HotelGuestBot>();Health=maxHealth;}
        void Start(){Director=FindFirstObjectByType<HotelZombieDirector>();if(Director)Director.Register(this);}
        public void Damage(float amount,Vector3 source)
        {
            if(!Targetable||!Director||!Director.Running)return;
            var combat=Player?Player.GetComponent<HotelCombat>():null;if(combat)amount=combat.BlockDamage(amount,source);
            Health=Mathf.Max(0,Health-amount);
            if(Player&&amount>0&&!Downed){Player.PlayGesture(HotelPlayerGesture.Hurt,source);HotelAudio.PlayAt("player_hurt",transform.position,.9f,Random.Range(.92f,1.08f),.25f,24);}
            if(Guest)Guest.GetComponent<HotelGuestSurvival>()?.OnThreat();
            if(Downed){bleedRemaining=45;if(Player)Player.Incapacitate();if(Guest)Guest.GetComponent<HotelGuestSurvival>()?.Incapacitate();Director.Alert((Player?"You":Guest.guestName)+" is down! Rescue with medicine.");}
        }
        public void Heal(float amount){if(Dead)return;bool wasDown=Downed;Health=Mathf.Min(maxHealth,Health+amount);if(wasDown&&Health>0){if(Player)Player.Recover();if(Guest)Guest.GetComponent<HotelGuestSurvival>()?.Revived();}}
        public bool TakeBullet(float amount,Vector3 source,Vector3 point)
        {
            if(Dead||!Guest||!float.IsFinite(amount)||amount<=0)return false;
            Guest.Hotel.Economy?.Misconduct(Guest,HotelMisconduct.Shot);Health=Mathf.Max(0,Health-amount);
            if(Health>0){Guest.PrankPhysics?.BulletReaction();Guest.GetComponent<HotelGuestSurvival>()?.OnThreat();return true;}
            Dead=true;bleedRemaining=0;Guest.Hotel.Economy?.Death(this,true);
            Guest.StopForDanger();Guest.PrankPhysics?.Die(point,(transform.position-source).normalized);
            if(Director){Director.Unregister(this);Director.Casualty(this);}
            if(Guest.Hotel)Guest.Hotel.Forget(Guest);
            Destroy(gameObject,20);return true;
        }
        public void ResetHealth(){Dead=false;Health=maxHealth;bleedRemaining=0;if(Player)Player.Recover();}
        void Update()
        {
            if(!Downed||!Director||!Director.Running)return;bleedRemaining-=Time.deltaTime;
            if(bleedRemaining>0)return;Dead=true;Director.Casualty(this);if(Guest)Guest.Hotel.RemoveGuest(Guest);
        }
        void OnDestroy(){if(Director)Director.Unregister(this);}
    }
}
