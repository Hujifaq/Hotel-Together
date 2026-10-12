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
        public Renderer[] structureVisuals;
        public Collider[] structureColliders;
        public GameObject[] planks;
        public BoxCollider boardCollider;
        public NavMeshObstacle obstacle;
        public NavMeshLink passage;
        public float StructureHealth { get; private set; }
        public bool Broken=>StructureHealth<=0;
        public int BoardCount { get { int count=0;foreach(float hp in boardHealth)if(hp>0)count++;return count;} }
        public float BoardHealth {get{float sum=0;foreach(float hp in boardHealth)sum+=hp;return sum;}}
        public bool Blocks=>BoardCount>0||!Broken&&(window||door&&!door.open);
        readonly float[] boardHealth=new float[4];
        HotelZombieDirector director;bool[] visualEnabled,colliderEnabled;
        void Awake()
        {
            director=FindFirstObjectByType<HotelZombieDirector>();StructureHealth=structureMax;
            visualEnabled=new bool[structureVisuals.Length];colliderEnabled=new bool[structureColliders.Length];
            for(int i=0;i<visualEnabled.Length;i++)visualEnabled[i]=structureVisuals[i]&&structureVisuals[i].enabled;
            for(int i=0;i<colliderEnabled.Length;i++)colliderEnabled[i]=structureColliders[i]&&structureColliders[i].enabled;
            Refresh();
        }
        public bool AddBoard(HotelCombat combat)
        {
            if(BoardCount>=4){combat.Player.Notify("This opening already has four planks.");return false;}
            if(!combat.SpendWood(1))return false;
            for(int i=0;i<4;i++)if(boardHealth[i]<=0){boardHealth[i]=45;break;}
            if(door){door.open=false;door.PoseDoor(0);}Refresh();return true;
        }
        public bool RemoveBoard(HotelCombat combat)
        {
            for(int i=3;i>=0;i--)if(boardHealth[i]>0){bool reusable=boardHealth[i]>=44.99f;boardHealth[i]=0;if(reusable)combat.ReturnWood(1);else combat.Player.Notify("Damaged plank removed. Replace it with fresh wood.");Refresh();return true;}return false;
        }
        public bool Repair(HotelCombat combat)
        {
            if(StructureHealth>=structureMax)return false;if(!combat.SpendWood(2))return false;
            StructureHealth=structureMax;RestoreStructure();Refresh();return true;
        }
        public void TakeAttack(float amount)
        {
            if(!director||!director.IsNight||!director.Running||!Blocks)return;
            for(int i=3;i>=0&&amount>0;i--)if(boardHealth[i]>0){float hit=Mathf.Min(boardHealth[i],amount);boardHealth[i]-=hit;amount-=hit;}
            if(amount>0)StructureHealth=Mathf.Max(0,StructureHealth-amount);
            if(Broken){foreach(var renderer in structureVisuals)if(renderer)renderer.enabled=false;foreach(var collider in structureColliders)if(collider)collider.enabled=false;if(door)door.open=true;}
            Refresh();if(Broken&&BoardCount==0)director.Alert(label+" breached! Protect the guests.");
        }
        void RestoreStructure(){for(int i=0;i<structureVisuals.Length;i++)if(structureVisuals[i])structureVisuals[i].enabled=visualEnabled[i];for(int i=0;i<structureColliders.Length;i++)if(structureColliders[i])structureColliders[i].enabled=colliderEnabled[i];}
        public void ResetOpening(){StructureHealth=structureMax;System.Array.Clear(boardHealth,0,4);RestoreStructure();if(door){door.open=false;door.PoseDoor(0);}Refresh();}
        void Refresh()
        {
            if(BoardCount>0&&door)door.open=false;
            for(int i=0;i<planks.Length;i++)if(planks[i])planks[i].SetActive(i<4&&boardHealth[i]>0);
            if(boardCollider)boardCollider.enabled=BoardCount>0;
            if(obstacle)obstacle.enabled=BoardCount>0||!Broken&&(window||director&&director.IsNight&&door&&!door.open);
            if(passage)passage.enabled=!Blocks;
        }
        void Update(){Refresh();}
    }
    public enum DefenseStockType { Wood, Medicine }
}
