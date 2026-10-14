using UnityEngine;
using UnityEngine.InputSystem;
namespace HotelTogether
{
    [DefaultExecutionOrder(80)]
    public sealed class HotelCombat : MonoBehaviour
    {
        public HotelItem batPrefab;
        public HotelPlayer Player { get; private set; }
        public HotelHuman Human { get; private set; }
        public HotelZombieDirector Director { get; private set; }
        public int Wood { get; private set; }=8;
        public int Medicine { get; private set; }=1;
        public float Stamina { get; private set; }=100;
        public bool IsDown=>Human&&(Human.Downed||Human.Dead);
        public Quaternion SwingOffset=>Player&&Player.held&&Player.held.GetComponent<HotelWeapon>()?Quaternion.Euler(-Mathf.Sin(Mathf.Clamp01((attackUntil-Time.time)/.55f)*Mathf.PI)*55,0,0):Quaternion.identity;
        public bool Blocking=>Player&&Player.GameplayInputEnabled&&!IsDown&&Mouse.current!=null&&(Mouse.current.rightButton.isPressed&&!Player.held&&!Player.SecondaryInputHandled||Keyboard.current!=null&&Keyboard.current.leftCtrlKey.isPressed)&&!Player.GrabbedGuest&&Stamina>=10;
        readonly Collider[] hits=new Collider[48];
        HotelItem bat;float attackUntil,healUntil;
        void Awake(){Player=GetComponent<HotelPlayer>();Human=GetComponent<HotelHuman>();}
        void Start(){Director=FindFirstObjectByType<HotelZombieDirector>();}
        public bool SpendWood(int count){if(Wood<count){Player.Notify("Not enough wood. Collect planks from the preparation rack.");return false;}Wood-=count;return true;}
        public void ReturnWood(int count){Wood=Mathf.Min(30,Wood+count);}
        public void CollectWood(){int count=Mathf.Min(30-Wood,Director.woodStock);Wood+=count;Director.woodStock-=count;Player.Notify("Planks "+Wood+" / 30");}
        public void CollectMedicine(){int count=Mathf.Min(4-Medicine,Director.medicineStock);Medicine+=count;Director.medicineStock-=count;Player.Notify("Medicine "+Medicine+" / 4");}
        public bool EquipBat()
        {
            if(IsDown||Player.cart||Player.seated||Player.IsReachingForItem)return false;
            if(Player.held){Player.Notify("Q to put down your item before equipping the bat.");return false;}
            if(bat)Destroy(bat.gameObject);bat=Instantiate(batPrefab,Player.view.transform.TransformPoint(new Vector3(.1f,-.25f,.65f)),Quaternion.identity,Director.transform);return Player.RequestPickup(bat);
        }
        public bool Strike()
        {
            if(Player.PrimaryInputHandled||Player.GrabbedGuest)return false;
            if(Player.held&&Player.held.GetComponent<HotelFirearm>())return false;
            if(Player.held&&Player.held.GetComponent<HotelDefenseHammer>())return false;
            if(Player.held&&Player.held.GetComponent<HotelMaintenanceTool>())return false;if(!Director||!Director.Running||IsDown||Player.transportLocked||Time.time<attackUntil||Stamina<14||Player.IsReachingForItem)return false;
            Stamina-=14;attackUntil=Time.time+.55f;Player.PlayStrikeGesture();var origin=Player.view.transform.position;var forward=Player.view.transform.forward;int count=Physics.OverlapSphereNonAlloc(origin+forward,1.45f,hits,~((1<<8)|(1<<9)),QueryTriggerInteraction.Collide);
            HotelZombieBot best=null;float nearest=2.4f;
            for(int i=0;i<count;i++)
            {
                var zombie=hits[i].GetComponentInParent<HotelZombieBot>();if(!zombie||!zombie.Alive)continue;var point=zombie.transform.position+Vector3.up*.9f;var delta=point-origin;float distance=delta.magnitude;if(distance>nearest||Vector3.Dot(delta.normalized,forward)<.30f)continue;
                if(Physics.Raycast(origin,delta.normalized,out var hit,distance,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<HotelZombieBot>()!=zombie)continue;
                best=zombie;nearest=distance;
            }
            if(best){float damage=Player.held&&Player.held.GetComponent<HotelWeapon>()?Player.held.GetComponent<HotelWeapon>().damage:9;best.Damage(damage,forward);Player.Notify("Hit! Zombie health "+Mathf.CeilToInt(best.Health));}
            return true;
        }
        public float BlockDamage(float damage,Vector3 source)
        {if(Blocking&&Vector3.Dot(Player.view.transform.forward,(source-transform.position).normalized)>.25f){Stamina-=10;return damage*.3f;}return damage;}
        public bool StaffRescue(HotelHuman target,Vector3 position){if(!target||!target.Downed||target.Dead||Medicine<=0||Vector3.Distance(position,target.transform.position)>2.5f)return false;Medicine--;target.Heal(45);if(Director)Director.Rescued++;return true;}
        public bool Rescue(HotelHuman target)
        {
            if(!target||!target.Downed||target.Dead||IsDown||Medicine<=0||Time.time<healUntil||Vector3.Distance(transform.position,target.transform.position)>3.2f){Player.Notify("Rescue needs medicine and a nearby fallen guest.");return false;}
            if(Physics.Linecast(Player.view.transform.position,target.transform.position+Vector3.up*.7f,out var hit,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<HotelHuman>()!=target)return false;
            Medicine--;healUntil=Time.time+1;target.Heal(45);Player.PlayGesture(HotelPlayerGesture.Rescue,target.transform.position);Director.Rescued++;Player.Notify("Guest rescued! E to escort them to safety.");return true;
        }
        public void ResetCombat(){Wood=8;Medicine=1;Stamina=100;attackUntil=healUntil=0;if(bat)Destroy(bat.gameObject);bat=null;Human.ResetHealth();Player.DefenseWork?.ResetWork();}
        void Update()
        {
            if(!Director||!Director.Running||!Player.GameplayInputEnabled||Player.transportLocked||Keyboard.current==null)return;var k=Keyboard.current;
            if(IsDown){if(k.rKey.wasPressedThisFrame){Player.ResetWorld();return;}if(k.hKey.wasPressedThisFrame&&Medicine>0&&!Human.Dead){Medicine--;Human.Heal(40);Player.PlayGesture(HotelPlayerGesture.Heal,transform.position);}return;}
            Stamina=Mathf.Min(100,Stamina+Time.deltaTime*(Blocking?5:22));
            if(k.fKey.wasPressedThisFrame)EquipBat();if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame)Strike();
            if(k.hKey.wasPressedThisFrame&&Medicine>0&&Human.Health<Human.maxHealth&&Time.time>=healUntil){Medicine--;Human.Heal(35);Player.PlayGesture(HotelPlayerGesture.Heal,transform.position);healUntil=Time.time+1;}
            var opening=Player.FocusedItem?Player.FocusedItem.GetComponentInParent<HotelDefenseOpening>():null;
            if(opening&&Player.DefenseWork){if(k.bKey.wasPressedThisFrame)Player.DefenseWork.SelectMode(DefenseWorkMode.Build);if(k.nKey.wasPressedThisFrame)Player.DefenseWork.SelectMode(DefenseWorkMode.Remove);if(k.tKey.wasPressedThisFrame)Player.DefenseWork.SelectMode(DefenseWorkMode.Repair);if(k.lKey.wasPressedThisFrame)opening.ToggleLock(Player);}
        }
    }
}
