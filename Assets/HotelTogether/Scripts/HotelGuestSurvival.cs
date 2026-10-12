using UnityEngine;
using UnityEngine.AI;
namespace HotelTogether
{
    [DefaultExecutionOrder(20)]
    public sealed class HotelGuestSurvival : MonoBehaviour
    {
        public bool IsTakingCover {get;private set;}
        HotelGuestBot guest;HotelHuman human;HotelZombieDirector director;
        HotelPlayer escort;HotelItem shelterDoor;
        Vector3 refuge;float think,safeSince;
        bool arrived;
        void Awake(){guest=GetComponent<HotelGuestBot>();human=GetComponent<HotelHuman>();}
        void Start(){director=FindFirstObjectByType<HotelZombieDirector>();}
        public void OnThreat()
        {
            if(!director)director=FindFirstObjectByType<HotelZombieDirector>();if(!director||!human.Targetable)return;
            if(!IsTakingCover){guest.PrepareForDanger();IsTakingCover=true;}escort=null;arrived=false;think=0;ChooseRefuge();
        }
        void ChooseRefuge()
        {
            shelterDoor=null;
            if(guest.CheckedIn)
            {
                var room=guest.Hotel.rooms[guest.RoomIndex];var defense=room.door.GetComponent<HotelDefenseOpening>();
                if((!defense||!defense.Broken&&defense.BoardCount==0)&&!director.NearestThreat(room.standPoint,4)){shelterDoor=room.door;refuge=room.standPoint;shelterDoor.open=true;return;}
            }
            ChooseAlternateRefuge();
        }
        void ChooseAlternateRefuge()
        {
            var options=new[]{new Vector3(0,4.525f,-18),new Vector3(0,9.025f,-20.8f),new Vector3(14,4.525f,-20.5f)};
            refuge=options[0];var filter=new NavMeshQueryFilter{agentTypeID=guest.Agent.agentTypeID,areaMask=guest.Agent.areaMask};var path=new NavMeshPath();
            foreach(var point in options){if(director.NearestThreat(point,8)||!NavMesh.SamplePosition(point,out var hit,1.2f,filter)||Mathf.Abs(hit.position.y-point.y)>.3f)continue;if(guest.Agent.CalculatePath(hit.position,path)&&path.status==NavMeshPathStatus.PathComplete){refuge=hit.position;break;}}
        }
        public bool Escort(HotelPlayer player)
        {
            if(!human.Targetable)return false;
            if(director.NearestThreat(player.transform.position,3)){player.Notify("Clear nearby zombies before escorting this guest.");return false;}
            if(!IsTakingCover){guest.PrepareForDanger();IsTakingCover=true;}
            escort=player;arrived=false;think=0;player.Notify(guest.guestName+" is following you to safety.");return true;
        }
        public void Incapacitate(){if(!IsTakingCover)guest.PrepareForDanger();IsTakingCover=true;escort=null;guest.StopForDanger();guest.SetSurvivalPresentation(GuestActivity.Downed,GuestNeed.Rescue);}
        public void Revived(){OnThreat();}
        public void EndDanger(){if(!IsTakingCover||!human.Targetable)return;IsTakingCover=false;escort=null;guest.ResumeAfterDanger();}
        void Update()
        {
            if(!director||!director.Running)return;
            if(!human.Targetable){guest.StopForDanger();return;}
            if(IsTakingCover&&!director.IsNight&&!director.NearestThreat(transform.position,9)){EndDanger();return;}
            if(!IsTakingCover){if(director.NearestThreat(transform.position,9))OnThreat();else return;}
            var agent=guest.Agent;if(!agent||!agent.enabled||!agent.isOnNavMesh)return;
            if(Time.time<think)return;think=Time.time+.65f;
            if(transform.position.y<2&&transform.position.z>7.3f)
            {
                HotelDefenseOpening entrance=null;float best=float.PositiveInfinity;var approach=new NavMeshPath();
                foreach(var opening in director.openings)
                {if(!opening.exterior||opening.window||opening.BoardCount>0||director.NearestThreat(opening.outsidePoint,6))continue;if(!agent.CalculatePath(opening.outsidePoint,approach)||approach.status!=NavMeshPathStatus.PathComplete)continue;float distance=Vector3.Distance(transform.position,opening.outsidePoint);if(distance<best){best=distance;entrance=opening;}}
                if(entrance)
                {
                    if(best<2.3f){var automatic=entrance.door.GetComponent<HotelAutomaticEntrance>();if(automatic)automatic.RequestOpen(true);else entrance.door.open=true;}
                    else{agent.isStopped=false;agent.SetDestination(entrance.outsidePoint);guest.SetSurvivalPresentation(GuestActivity.Fleeing,GuestNeed.Shelter);return;}
                }
            }
            var threat=director.NearestThreat(transform.position,6);
            if(arrived&&threat)
            {
                var cover=shelterDoor?shelterDoor.GetComponent<HotelDefenseOpening>():null;
                if(!cover||!cover.Blocks){if(shelterDoor)shelterDoor.open=true;arrived=false;shelterDoor=null;ChooseAlternateRefuge();}
            }
            Vector3 target=escort?escort.transform.position-escort.transform.forward*1.4f:refuge;
            if(shelterDoor&&!arrived){var opening=shelterDoor.GetComponent<HotelDefenseOpening>();if(!opening||opening.BoardCount==0)shelterDoor.open=true;}
            guest.Hotel.OpenNearbyDoors(transform.position);
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};var path=new NavMeshPath();
            if(!NavMesh.SamplePosition(target,out var hit,1.3f,filter)||!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)
            {guest.SetSurvivalPresentation(GuestActivity.Fleeing,GuestNeed.Shelter);agent.isStopped=true;if(!escort&&shelterDoor&&Time.time-safeSince>5){shelterDoor.open=true;shelterDoor=null;ChooseAlternateRefuge();}return;}
            if(Vector3.Distance(transform.position,hit.position)<(escort?2:.55f))
            {agent.isStopped=true;arrived=true;safeSince=Time.time;if(shelterDoor&&!escort)shelterDoor.open=false;guest.SetSurvivalPresentation(GuestActivity.Hiding,GuestNeed.None);}
            else{arrived=false;agent.isStopped=false;agent.SetPath(path);guest.SetSurvivalPresentation(GuestActivity.Fleeing,GuestNeed.Shelter);}
        }
        public void Pose(float speed,float dt)
        {
            if(!human.Targetable){guest.visual.Pose(0,dt,true,true,false,.2f,false);guest.visual.transform.localRotation=Quaternion.Euler(0,0,25);return;}
            guest.visual.transform.localRotation=Quaternion.identity;guest.visual.Pose(speed,dt,false,false,false,.30f,false);
            if(guest.Agent&&guest.Agent.enabled&&guest.Agent.isOnNavMesh&&!director.Running)guest.Agent.isStopped=true;
        }
    }
}
