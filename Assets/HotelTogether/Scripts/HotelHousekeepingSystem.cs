using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
namespace HotelTogether
{
    public sealed class HotelHousekeepingSystem : MonoBehaviour
    {
        public HotelGuestSimulation hotel;
        public HotelCleaningCart[] carts;
        public HotelWasteBin[] bins;
        public HotelFloorMess[] authored;
        public HotelFloorMess wetPrefab, litterPrefab;
        public Renderer[] legacyLobbyWater = System.Array.Empty<Renderer>();
        public HotelWaterFixture[] waterSources;
        public HotelDetergentBottle[] detergent;
        public int Completed { get; private set; }
        readonly List<HotelFloorMess> spawned = new();
        float[] waterTimers;
        public HotelFloorMess ActiveMop { get { foreach(var m in authored)if(m&&m.Mopping)return m;foreach(var m in spawned)if(m&&m.Mopping)return m;return null; } }
        public int Pending { get { int count=0;foreach(var m in authored)if(m&&!m.Cleared)count++;foreach(var m in spawned)if(m&&!m.Cleared)count++;return count; } }
        void Awake() { waterTimers = new float[waterSources.Length]; foreach(var cart in carts)if(cart)cart.ResetCart();foreach(var r in legacyLobbyWater)if(r)r.enabled=false; }
        public void CompletedMess(HotelFloorMess mess) { Completed++; hotel.RecordHousekeeping(5);if(hotel.serviceJobs)hotel.serviceJobs.RecordCleaning(); }
        public HotelCleaningCart NearestCart(Vector3 position) { HotelCleaningCart best=null;foreach(var c in carts)if(c&&(!best||Vector3.Distance(position,c.transform.position)<Vector3.Distance(position,best.transform.position)))best=c;return best; }
        public HotelFloorMess NearestMess(Vector3 position) { HotelFloorMess best=null;foreach(var m in authored)if(m&&!m.Cleared&&(!best||Vector3.Distance(position,m.transform.position)<Vector3.Distance(position,best.transform.position)))best=m;foreach(var m in spawned)if(m&&!m.Cleared&&(!best||Vector3.Distance(position,m.transform.position)<Vector3.Distance(position,best.transform.position)))best=m;return best; }
        public HotelFloorMess WetAt(Vector3 point)
        {
            foreach(var m in authored)if(m&&m.ContainsFloorPoint(point))return m;
            foreach(var m in spawned)if(m&&m.ContainsFloorPoint(point))return m;return null;
        }
        public HotelFloorMess SpawnMess(FloorMessType type, Vector3 point)
        {
            spawned.RemoveAll(m=>!m); int live=0;foreach(var m in spawned)if(!m.Cleared)live++; if(live>=12)return null;
            float floor = hotel.elevators.Length>0 ? hotel.elevators[0].floors[hotel.elevators[0].FloorFor(point.y)].height : point.y;
            point.y=floor;
            if(!TryFloorPoint(hotel,point,out var safe))return null;
            point=safe+Vector3.up*.008f;
            foreach(var m in authored)if(m&&!m.Cleared&&m.type==type&&Vector3.Distance(m.transform.position,point)<.9f)return m;
            foreach(var m in spawned)if(m&&!m.Cleared&&m.type==type&&Vector3.Distance(m.transform.position,point)<.9f)return m;
            // Completed dynamic messes have no further gameplay state; reclaim them at the next spawn.
            for(int i=spawned.Count-1;i>=0;i--)if(spawned[i].Cleared){Destroy(spawned[i].gameObject);spawned.RemoveAt(i);}
            var prefab=type==FloorMessType.Wet?wetPrefab:litterPrefab;if(!prefab)return null;
            var result=Instantiate(prefab,point,Quaternion.identity,transform);result.system=this;result.generated=true;spawned.Add(result);return result;
        }
        public static bool TryFloorPoint(HotelGuestSimulation hotel,Vector3 requested,out Vector3 point)
        {
            var filter=new NavMeshQueryFilter{agentTypeID=hotel.guestPrefabs[0].GetComponent<NavMeshAgent>().agentTypeID,areaMask=-1};
            for(int attempt=0;attempt<17;attempt++){
                float angle=(attempt-1)*Mathf.PI/4,radius=attempt==0?0:attempt<=8?.8f:1.3f;
                var candidate=requested+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                if(!NavMesh.SamplePosition(candidate,out var hit,.35f,filter)||Mathf.Abs(hit.position.y-requested.y)>.2f)continue;
                if(Physics.CheckCapsule(hit.position+Vector3.up*.35f,hit.position+Vector3.up*1.4f,.27f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))continue;
                point=hit.position;return true;
            }
            point=requested;return false;
        }
        void Update()
        {
            if(!hotel.Running||hotel.player.combat&&hotel.player.combat.IsDown)return;
            var p=hotel.player;
            if(Keyboard.current!=null&&Keyboard.current.gKey.wasPressedThisFrame){var item=p.cart?p.cart:p.FocusedItem;var cart=item?item.GetComponent<HotelCleaningCart>():null;if(!cart&&item)cart=item.GetComponent<HotelCleaningTool>()?.DockedAt;if(cart)cart.ToggleMop(p);}
            for(int i=0;i<waterSources.Length;i++)
            {
                var source=waterSources[i];if(!source||!source.IsRunning){waterTimers[i]=0;continue;}
                waterTimers[i]+=Time.deltaTime;
                if(waterTimers[i]>=(source.shower?10:25)){waterTimers[i]=0;var point=source.transform.TransformPoint(source.impact)+source.transform.forward*.5f;point.y=source.transform.position.y;SpawnMess(FloorMessType.Wet,point);}
            }
        }
        public void ResetCleaning()
        {
            foreach(var m in spawned)if(m)Destroy(m.gameObject);spawned.Clear();
            foreach(var m in authored)if(m)m.ResetMess();foreach(var cart in carts)if(cart)cart.ResetCart();foreach(var bottle in detergent)if(bottle)bottle.ResetBottle();
            if(waterTimers!=null)System.Array.Clear(waterTimers,0,waterTimers.Length);Completed=0;
        }
    }
}
