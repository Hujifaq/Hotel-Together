using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
namespace HotelTogether
{
    public sealed class HotelDefenseOpening : MonoBehaviour
    {
        public string label;
        public bool exterior=true,window;
        public float structureMax=120;
        public Vector3 outsidePoint,insidePoint;
        public HotelItem door;
        public Renderer[] structureVisuals=System.Array.Empty<Renderer>();
        public Collider[] structureColliders=System.Array.Empty<Collider>();
        public GameObject[] planks=System.Array.Empty<GameObject>();
        public BoxCollider boardCollider;
        public NavMeshObstacle obstacle;
        public NavMeshLink passage;
        public float StructureHealth { get; private set; }
        public bool Broken=>StructureHealth<=0;
        public int BoardCount {get{int n=0;foreach(float hp in boardHealth)if(hp>0)n++;return n;}}
        public float BoardHealth {get{float n=0;foreach(float hp in boardHealth)n+=hp;return n;}}
        public float BoardMaximum {get{float n=0;for(int i=0;i<4;i++)if(boardHealth[i]>0)n+=boardMaximum[i];return n;}}
        public bool Locked {get;private set;}
        public bool LockPending {get;private set;}
        public float LockHealth {get;private set;}=65;
        public bool CanOpen=>!Locked&&!LockPending&&BoardCount==0&&!Broken;
        public bool Blocks=>BoardCount>0||!Broken&&(window||door&&(Locked||LockPending||!door.open));
        public string Status=>BoardCount+"/4 PLANKS  "+Mathf.CeilToInt(BoardHealth)+" HP  /  "+(Broken?"BREACHED":Mathf.CeilToInt(StructureHealth)+" STRUCTURE")+(door?Locked?"  / LOCKED "+Mathf.CeilToInt(LockHealth):LockPending?"  / CLOSING":"  / UNLOCKED":"");
        readonly float[] boardHealth=new float[4],boardMaximum=new float[4];
        readonly Vector3[] plankScales=new Vector3[4];
        readonly Quaternion[] plankRotations=new Quaternion[4];
        HotelZombieDirector director;bool[] visualEnabled,colliderEnabled;
        bool initialized;float closingTimeout,shakeUntil;int installing=-1;float installTime;
        public void Initialize(HotelZombieDirector owner)
        {
            director=owner;if(initialized)return;initialized=true;StructureHealth=structureMax;
            visualEnabled=new bool[structureVisuals.Length];colliderEnabled=new bool[structureColliders.Length];
            for(int i=0;i<visualEnabled.Length;i++)visualEnabled[i]=structureVisuals[i]&&structureVisuals[i].enabled;
            for(int i=0;i<colliderEnabled.Length;i++)colliderEnabled[i]=structureColliders[i]&&structureColliders[i].enabled;
            for(int i=0;i<4;i++){boardMaximum[i]=45;if(i<planks.Length&&planks[i]){plankScales[i]=planks[i].transform.localScale;plankRotations[i]=planks[i].transform.localRotation;}}
            Refresh();
        }
        void Start(){Initialize(director?director:FindFirstObjectByType<HotelZombieDirector>());}
        public void ApplyUpgrade(float maximum){float ratio=structureMax>0?StructureHealth/structureMax:1;structureMax=Mathf.Max(1,maximum);StructureHealth=Mathf.Clamp01(ratio)*structureMax;}
        public Vector3 WorkPoint(HotelPlayer player)
        {
            var focused=player.FocusedItem;if(focused&&focused.GetComponent<HotelDefenseLatch>() is HotelDefenseLatch latch&&latch.opening==this){var sensor=focused.GetComponent<Collider>();return sensor?sensor.ClosestPoint(player.view.transform.position):focused.transform.position;}
            var collider=boardCollider?boardCollider:structureColliders.Length>0?structureColliders[0]:null;
            if(collider is BoxCollider box){var local=box.transform.InverseTransformPoint(player.view.transform.position)-box.center;var half=box.size*.5f;local=new Vector3(Mathf.Clamp(local.x,-half.x,half.x),Mathf.Clamp(local.y,-half.y,half.y),Mathf.Clamp(local.z,-half.z,half.z));return box.transform.TransformPoint(box.center+local);}
            return collider&&collider.enabled?collider.ClosestPoint(player.view.transform.position):transform.position;
        }
        public bool InReach(HotelPlayer player)
        {
            if(!player||!player.view||player.transportLocked||player.combat&&player.combat.IsDown||player.cart||player.seated)return false;
            var point=WorkPoint(player);var delta=point-player.view.transform.position;if(delta.magnitude>3.2f)return false;
            if(Physics.Raycast(player.view.transform.position,delta.normalized,out var hit,delta.magnitude,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))
            {
                if(hit.collider.transform.IsChildOf(transform)||hit.collider.GetComponentInParent<HotelDefenseOpening>()==this)return true;
                foreach(var collider in structureColliders)if(collider==hit.collider)return true;
                foreach(var renderer in structureVisuals)if(renderer&&hit.collider.transform.IsChildOf(renderer.transform))return true;
                return false;
            }
            return true;
        }
        public bool CanWork(DefenseWorkMode mode,HotelCombat combat,bool notify=false)
        {
            if(!combat||!InReach(combat.Player))return false;
            bool valid=mode switch {DefenseWorkMode.Build=>BoardCount<4,DefenseWorkMode.Remove=>BoardCount>0,DefenseWorkMode.Repair=>StructureHealth<structureMax||DamagedBoard()>=0||door&&LockHealth<65,DefenseWorkMode.Reinforce=>WeakBoard()>=0,_=>false};
            int cost=mode==DefenseWorkMode.Remove?0:mode==DefenseWorkMode.Repair&&StructureHealth<structureMax?2:1;
            if(!valid&&notify)combat.Player.Notify(mode==DefenseWorkMode.Build?"Four planks installed. Switch to REINFORCE or REPAIR.":"Nothing to "+mode.ToString().ToLower()+" here.");
            if(valid&&combat.Wood<cost){if(notify)combat.Player.Notify("Need "+cost+" wood. Collect more from the preparation rack.");return false;}return valid;
        }
        int DamagedBoard(){for(int i=0;i<4;i++)if(boardHealth[i]>0&&boardHealth[i]<boardMaximum[i]-.01f)return i;return -1;}
        int WeakBoard(){for(int i=0;i<4;i++)if(boardHealth[i]>0&&boardMaximum[i]<90)return i;return -1;}
        public bool AddBoard(HotelCombat combat)
        {
            if(!CanWork(DefenseWorkMode.Build,combat,true)||!combat.SpendWood(1))return false;
            for(int i=0;i<4;i++)if(boardHealth[i]<=0){boardHealth[i]=boardMaximum[i]=45;installing=i;installTime=0;break;}
            if(door)door.open=false;Refresh();combat.Player.PlayGesture(HotelPlayerGesture.Build,WorkPoint(combat.Player));return true;
        }
        public bool RemoveBoard(HotelCombat combat)
        {
            if(!CanWork(DefenseWorkMode.Remove,combat))return false;
            for(int i=3;i>=0;i--)if(boardHealth[i]>0){if(boardHealth[i]>=boardMaximum[i]-.01f)combat.ReturnWood(boardMaximum[i]>=90?2:1);else combat.Player.Notify("Damaged plank removed; its wood cannot be reused.");boardHealth[i]=0;Refresh();combat.Player.PlayGesture(HotelPlayerGesture.Place,WorkPoint(combat.Player));return true;}return false;
        }
        public bool StaffRepair(HotelCombat combat,Vector3 position){if(!combat||Vector3.Distance(position,insidePoint)>2.6f)return false;int damaged=DamagedBoard();if(StructureHealth<structureMax){if(!combat.SpendWood(2))return false;StructureHealth=structureMax;RestoreStructure();}else if(damaged>=0){if(!combat.SpendWood(1))return false;boardHealth[damaged]=boardMaximum[damaged];}else return false;Refresh();return true;}
        public bool Repair(HotelCombat combat)
        {
            if(!CanWork(DefenseWorkMode.Repair,combat,true))return false;
            if(StructureHealth<structureMax){if(!combat.SpendWood(2))return false;StructureHealth=structureMax;RestoreStructure();}
            else {if(!combat.SpendWood(1))return false;int damaged=DamagedBoard();if(damaged>=0)boardHealth[damaged]=boardMaximum[damaged];else LockHealth=65;}
            Refresh();combat.Player.PlayGesture(HotelPlayerGesture.Repair,WorkPoint(combat.Player));return true;
        }
        public bool Reinforce(HotelCombat combat)
        {
            if(!CanWork(DefenseWorkMode.Reinforce,combat,true)||!combat.SpendWood(1))return false;int i=WeakBoard();boardHealth[i]=boardMaximum[i]=90;Refresh();combat.Player.PlayGesture(HotelPlayerGesture.Build,WorkPoint(combat.Player));return true;
        }
        public bool ToggleLock(HotelPlayer player)
        {
            if(!door||!InReach(player)||Broken||BoardCount>0){if(player)player.Notify("Repair the door and remove its planks before using the lock.");return false;}
            if(Locked||LockPending){Locked=LockPending=false;player.PlayGesture(HotelPlayerGesture.Interact,WorkPoint(player));player.PlayFeedback(HotelFeedback.Pickup,.16f);Refresh();return true;}
            if(LockHealth<=0){player.Notify("The lock is broken. Use the hammer in REPAIR mode.");return false;}
            door.open=false;LockPending=true;closingTimeout=Time.time+5;player.PlayGesture(HotelPlayerGesture.Interact,WorkPoint(player));player.PlayFeedback(HotelFeedback.Pickup,.16f);Refresh();return true;
        }
        public void TakeAttack(float amount)
        {
            if(!director||!director.IsNight||!director.Running||!Blocks||!float.IsFinite(amount)||amount<=0)return;
            DamageLayers(amount);
        }
        void DamageLayers(float amount)
        {
            shakeUntil=Time.time+.24f;
            for(int i=3;i>=0&&amount>0;i--)if(boardHealth[i]>0){float hit=Mathf.Min(boardHealth[i],amount);boardHealth[i]-=hit;amount-=hit;}
            if(amount>0&&Locked){float hit=Mathf.Min(LockHealth,amount);LockHealth-=hit;amount-=hit;if(LockHealth<=0){Locked=false;director.Alert(label+": lock broken!");}}
            if(amount>0)StructureHealth=Mathf.Max(0,StructureHealth-amount);
            if(Broken){Locked=LockPending=false;foreach(var renderer in structureVisuals)if(renderer)renderer.enabled=false;foreach(var collider in structureColliders)if(collider)collider.enabled=false;if(door)door.open=true;}
            Refresh();if(Broken&&BoardCount==0)director.Alert(label+" breached! Protect the guests.");
        }
        void RestoreStructure(){for(int i=0;i<structureVisuals.Length;i++)if(structureVisuals[i])structureVisuals[i].enabled=visualEnabled[i];for(int i=0;i<structureColliders.Length;i++)if(structureColliders[i])structureColliders[i].enabled=colliderEnabled[i];if(door)door.open=false;}
        public void ResetOpening(){if(!initialized)Initialize(director);StructureHealth=structureMax;Locked=LockPending=false;LockHealth=65;System.Array.Clear(boardHealth,0,4);for(int i=0;i<4;i++)boardMaximum[i]=45;installing=-1;shakeUntil=0;RestoreStructure();if(door){door.open=false;door.PoseDoor(0);}Refresh();}
        void Refresh()
        {
            if((BoardCount>0||Locked||LockPending)&&door)door.open=false;
            for(int i=0;i<planks.Length&&i<4;i++)if(planks[i]){planks[i].SetActive(boardHealth[i]>0);var band=planks[i].transform.Find("Reinforcement strap");if(band)band.gameObject.SetActive(boardMaximum[i]>=90);}
            if(boardCollider)boardCollider.enabled=BoardCount>0;
            if(obstacle)obstacle.enabled=BoardCount>0||!Broken&&(window||Locked||LockPending||director&&director.IsNight&&door&&!door.open);
            if(passage)passage.enabled=!Blocks;
        }
        void Update()
        {
            if(!initialized)return;
            if(LockPending){if(door.DoorFraction<.02f&&!door.open){Locked=true;LockPending=false;}else if(Time.time>closingTimeout){LockPending=false;director?.Alert(label+": door blocked; clear it before locking.");}}
            Refresh();
            if(Time.deltaTime<=0)return;
            if(installing>=0){installTime+=Time.deltaTime;if(installTime>=.35f)installing=-1;}
            for(int i=0;i<planks.Length&&i<4;i++)if(planks[i]&&planks[i].activeSelf){float grow=i==installing?Mathf.SmoothStep(.15f,1,installTime/.35f):1;planks[i].transform.localScale=new Vector3(plankScales[i].x*grow,plankScales[i].y,plankScales[i].z);float ratio=boardHealth[i]/Mathf.Max(1,boardMaximum[i]);float shake=Time.time<shakeUntil?Mathf.Sin(Time.time*65)*2:0;planks[i].transform.localRotation=plankRotations[i]*Quaternion.Euler(0,0,(1-ratio)*7+shake);}
        }
    }
    public enum DefenseStockType { Wood, Medicine }
    public enum DefenseWorkMode { Build, Repair, Reinforce, Remove }
}
