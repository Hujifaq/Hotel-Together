using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace HotelTogether
{
    [DefaultExecutionOrder(-50)]
    public sealed class HotelZombieDirector : MonoBehaviour
    {
        public HotelGuestSimulation hotel;
        public HotelCombat combat;
        public HotelZombieBot zombiePrefab;
        public HotelDefenseOpening[] openings;
        public Vector3[] spawnPoints;
        public int firstWave=3,waveGrowth=2,maxWave=40,maxActive=16;
        public float spawnInterval=3,nightGrace=12;
        public int woodStock=24,medicineStock=3;
        public int Rescued,Lost,Kills;
        public int NightNumber {get;private set;}
        public int WaveTotal {get;private set;}
        public int Spawned {get;private set;}
        public bool Running=>hotel&&hotel.Running;
        public bool IsNight=>hotel&&hotel.IsNight;
        public IReadOnlyList<HotelHuman> Humans=>humans;
        public IReadOnlyList<HotelZombieBot> Zombies=>zombies;
        readonly List<HotelHuman> humans=new();
        readonly List<HotelZombieBot> zombies=new();
        float activeNightTime,nextSpawn;
        bool wasNight;
        Texture2D panel;Font font;GUIStyle label,heading;
        public int ActiveCount {get{int count=0;foreach(var z in zombies)if(z&&z.Alive)count++;return count;}}
        public int WaveForNight(int night)=>Mathf.Min(maxWave,firstWave+Mathf.Max(0,night-1)*waveGrowth);
        public void Register(HotelHuman human){if(!humans.Contains(human))humans.Add(human);if(IsNight&&wasNight&&human.Guest&&human.Targetable)human.GetComponent<HotelGuestSurvival>()?.OnThreat();}
        public void Unregister(HotelHuman human){humans.Remove(human);}
        public void Alert(string text){if(hotel&&hotel.player)hotel.player.Notify(text);}
        public void Casualty(HotelHuman human){Lost++;Alert(human.Player?"You could not recover. R to start over.":human.Guest.guestName+" was lost.");}
        public void Kill(HotelZombieBot zombie){Kills++;zombies.Remove(zombie);}
        void Update()
        {
            if(!Running)return;
            if(IsNight&&!wasNight)BeginNight();if(!IsNight&&wasNight)EndNight();wasNight=IsNight;
            if(!IsNight)return;activeNightTime+=Time.deltaTime;
            if(activeNightTime>=nextSpawn&&Spawned<WaveTotal&&ActiveCount<maxActive)
            {if(SpawnZombie())Spawned++;nextSpawn=activeNightTime+spawnInterval;}
        }
        void BeginNight()
        {
            NightNumber=hotel.DayNumber;WaveTotal=WaveForNight(NightNumber);Spawned=0;activeNightTime=0;nextSpawn=nightGrace;
            foreach(var human in humans)if(human&&human.Guest&&human.Targetable)human.GetComponent<HotelGuestSurvival>()?.OnThreat();
            Alert("NIGHT "+NightNumber+": "+WaveTotal+" zombies. Protect the openings and guests!");
        }
        void EndNight()
        {
            ClearZombies();woodStock=Mathf.Min(36,woodStock+16);medicineStock=Mathf.Min(6,medicineStock+2);
            foreach(var human in humans.ToArray())if(human){if(human.Guest)human.GetComponent<HotelGuestSurvival>()?.EndDanger();else if(human.Targetable)human.Heal(20);}
            Alert("Dawn! Repair openings and remove entrance boards before check-in.");
        }
        public HotelZombieBot SpawnZombie()
        {
            if(!IsNight||!Running||!zombiePrefab||spawnPoints.Length==0)return null;
            int start=Spawned%spawnPoints.Length;var filter=new NavMeshQueryFilter{agentTypeID=zombiePrefab.GetComponent<NavMeshAgent>().agentTypeID,areaMask=NavMesh.AllAreas};
            for(int i=0;i<spawnPoints.Length;i++)
            {
                var point=spawnPoints[(start+i)%spawnPoints.Length];if(!NavMesh.SamplePosition(point,out var hit,2,filter))continue;
                var zombie=Instantiate(zombiePrefab,hit.position,Quaternion.Euler(0,180,0),transform);zombies.Add(zombie);zombie.Initialize(this,80+Mathf.Min(100,(hotel.DayNumber-1)*6));return zombie;
            }
            return null;
        }
        public HotelHuman NearestHuman(Vector3 position)
        {HotelHuman best=null;float nearest=float.PositiveInfinity;foreach(var human in humans)if(human&&human.Targetable){float score=(human.transform.position-position).sqrMagnitude;if(score<nearest){nearest=score;best=human;}}return best;}
        public HotelZombieBot NearestThreat(Vector3 position,float range)
        {HotelZombieBot best=null;float nearest=range*range;foreach(var zombie in zombies)if(zombie&&zombie.Alive){float d=(zombie.transform.position-position).sqrMagnitude;if(d<nearest){nearest=d;best=zombie;}}return best;}
        public HotelDefenseOpening ChooseOpening(NavMeshAgent agent,HotelHuman target,bool exterior,bool fromInside=false)
        {
            HotelDefenseOpening best=null;float score=float.PositiveInfinity;var path=new NavMeshPath();
            foreach(var opening in openings)
            {
                if(!opening||opening.exterior!=exterior||!exterior&&!opening.Blocks)continue;
                if(!exterior&&target&&(opening.insidePoint-target.transform.position).sqrMagnitude>45)continue;
                var approach=fromInside?opening.insidePoint:opening.outsidePoint;var exit=fromInside?opening.outsidePoint:opening.insidePoint;
                if(!agent.CalculatePath(approach,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                float candidate=Vector3.Distance(agent.transform.position,approach)+(target?Vector3.Distance(exit,target.transform.position)*.6f:0)+opening.BoardCount*.9f;
                if(candidate<score){score=candidate;best=opening;}
            }
            return best;
        }
        void ClearZombies(){foreach(var zombie in zombies)if(zombie)Destroy(zombie.gameObject);zombies.Clear();}
        public void ResetDefense()
        {
            ClearZombies();wasNight=false;NightNumber=WaveTotal=Spawned=Rescued=Lost=Kills=0;woodStock=24;medicineStock=3;activeNightTime=nextSpawn=0;
            foreach(var opening in openings)if(opening)opening.ResetOpening();if(combat)combat.ResetCombat();
        }
        void OnGUI()
        {
            if(!Running||!combat)return;
            if(!panel){font=Resources.Load<Font>("HotelTogetherUI/LilitaOne-Regular");panel=new Texture2D(1,1);panel.SetPixel(0,0,new Color(.13f,.035f,.035f,.90f));panel.Apply();label=new GUIStyle{font=font,fontSize=18,normal={textColor=new Color(1,.90f,.79f)}};heading=new GUIStyle(label){fontSize=24};}
            var matrix=GUI.matrix;var color=GUI.color;float scale=Mathf.Clamp(Mathf.Min(Screen.width/1200f,Screen.height/700f),.45f,2);GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*scale);float height=Screen.height/scale;
            GUI.DrawTexture(new Rect(18,120,300,113),panel);GUI.Label(new Rect(31,128,280,27),IsNight?"NIGHT "+NightNumber+"  /  DEFEND":"PREPARE YOUR DEFENSES",heading);
            GUI.Label(new Rect(31,158,280,24),IsNight?"Wave "+Spawned+"/"+WaveTotal+"   Alive "+ActiveCount:"Next wave: "+WaveForNight(hotel.DayNumber),label);
            GUI.Label(new Rect(31,184,280,24),"Saved "+Rescued+"   Lost "+Lost+"   Kills "+Kills,label);
            GUI.DrawTexture(new Rect(18,height-145,300,127),panel);GUI.Label(new Rect(31,height-137,275,27),"HEALTH "+Mathf.CeilToInt(combat.Human.Health)+"   ENERGY "+Mathf.CeilToInt(combat.Stamina),heading);
            GUI.Label(new Rect(31,height-104,275,24),"Wood "+combat.Wood+"   Medicine "+combat.Medicine,label);GUI.Label(new Rect(31,height-80,280,24),"F BAT   LMB HIT   RMB BLOCK",label);GUI.Label(new Rect(31,height-56,275,24),"H HEAL   E RESCUE   B BOARD",label);
            var opening=hotel.player.FocusedItem?hotel.player.FocusedItem.GetComponentInParent<HotelDefenseOpening>():null;
            if(opening){GUI.DrawTexture(new Rect(18,245,300,93),panel);GUI.Label(new Rect(31,253,275,24),opening.label,label);GUI.Label(new Rect(31,277,275,24),"Planks "+opening.BoardCount+"/4   HP "+Mathf.CeilToInt(opening.BoardHealth+opening.StructureHealth),label);GUI.Label(new Rect(31,302,275,24),"B ADD   N REMOVE   T REPAIR",label);}
            if(combat.IsDown){GUI.DrawTexture(new Rect(Screen.width/scale*.5f-205,height*.45f,410,86),panel);GUI.Label(new Rect(Screen.width/scale*.5f-185,height*.45f+13,390,29),"YOU ARE DOWN",heading);GUI.Label(new Rect(Screen.width/scale*.5f-185,height*.45f+47,390,25),"H use medicine / R start over",label);}
            GUI.color=color;GUI.matrix=matrix;
        }
        void OnDestroy(){if(panel)Destroy(panel);}
    }
}
