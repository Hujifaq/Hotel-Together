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
        public int ActiveCount {get{int count=0;foreach(var z in zombies)if(z&&z.Alive)count++;return count;}}
        public int WaveForNight(int night)=>Mathf.Min(maxWave,firstWave+Mathf.Max(0,night-1)*waveGrowth);
        public void Register(HotelHuman human){if(!humans.Contains(human))humans.Add(human);if(IsNight&&wasNight&&human.Guest&&human.Targetable)human.GetComponent<HotelGuestSurvival>()?.OnThreat();}
        public void Unregister(HotelHuman human){humans.Remove(human);}
        public void Alert(string text){if(hotel&&hotel.player)hotel.player.Notify(text,true);}
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
    }
}
