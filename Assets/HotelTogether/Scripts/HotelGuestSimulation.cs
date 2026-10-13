using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace HotelTogether
{
    public sealed class HotelGuestSimulation : MonoBehaviour
    {
        [Serializable] public sealed class GuestRoom { public int number;public HotelItem door;public Transform bed;public Vector3 standPoint; }
        [Serializable] public sealed class GuestSeat { public bool dining;public Vector3 position,approach;public Quaternion rotation;public Transform table;public HotelItem chair; }
        [Serializable] public sealed class SupplyStation { public HotelItem prefab;public Transform point;[NonSerialized]public HotelItem current; }
        public HotelGuestBot[] guestPrefabs;
        public GuestRoom[] rooms;
        public GuestSeat[] seats;
        public SupplyStation[] stations;
        public Vector3 arrivalPoint=new Vector3(0,.025f,13);
        public Vector3 receptionPoint=new Vector3(-5.65f,.025f,-3.6f);
        public int maxGuests=6;
        public float daySeconds=180,nightSeconds=180,arrivalInterval=26;
        public Light sun;
        public HotelPlayer player;
        public HotelServiceJobs serviceJobs;
        public HotelGuestWaterStation waterStation;
        public HotelElevator[] elevators;
        public IReadOnlyList<HotelGuestBot> Guests=>guests;
        public bool Running=>enabled&&player&&Cursor.lockState==CursorLockMode.Locked;
        public bool IsNight=>elapsed>=daySeconds;
        public int DayNumber { get; private set; }=1;
        public float PeriodRemaining=>Mathf.Max(0,(IsNight?daySeconds+nightSeconds:daySeconds)-elapsed);
        public int Tips { get; private set; }
        public int Services { get; private set; }
        public float elapsed;
        readonly List<HotelGuestBot> guests=new();
        readonly List<HotelGuestBot> queue=new();
        readonly List<HotelGuestSupply> supplies=new();
        readonly Dictionary<int,HotelGuestBot> occupiedSeats=new();
        readonly Dictionary<HotelGuestBot,GameObject> meals=new();
        HotelItem[] doors;
        float nextArrival=4,supplyTick,sunIntensity,ambientIntensity;
        Quaternion sunRotation;
        Color sunColor,ambientColor;
        int guestSerial;
        Renderer cityBackground;
        float cityExposure=1,backgroundSearch;
        MaterialPropertyBlock originalBackground,nightBackground;
        static readonly string[] Names={"Milo","Noah","Leo","Theo","Finn","Alex","Jamie","Robin","Sam","Riley","Luca","Avery"};
        void Awake()
        {
            if(!player)player=FindFirstObjectByType<HotelPlayer>();
            if(!serviceJobs)serviceJobs=GetComponent<HotelServiceJobs>();
            doors=FindObjectsByType<HotelItem>(FindObjectsSortMode.None);
            if(sun){sunIntensity=sun.intensity;sunRotation=sun.transform.rotation;sunColor=sun.color;}
            ambientIntensity=RenderSettings.ambientIntensity;ambientColor=RenderSettings.ambientLight;
        }
        void Update()
        {
            if(!Running)return;
            bool wasNight=IsNight;elapsed+=Time.deltaTime;
            if(elapsed>=daySeconds+nightSeconds){elapsed-=daySeconds+nightSeconds;DayNumber++;}
            if(wasNight!=IsNight){foreach(var guest in guests.ToArray())if(guest)guest.OnPeriodChanged();nextArrival=elapsed+7;}
            if(!IsNight&&elapsed>=nextArrival)
            {nextArrival=elapsed+arrivalInterval;if(guests.Count<maxGuests&&FindFreeRoom()>=0)SpawnGuest();}
            if(Time.time>=supplyTick){Restock();supplyTick=Time.time+2;}
            UpdateLighting();
        }
        public HotelGuestBot SpawnGuest()
        {
            if(guestPrefabs==null||guestPrefabs.Length==0||guests.Count>=maxGuests)return null;
            var prefab=guestPrefabs[guestSerial%guestPrefabs.Length];var filter=new NavMeshQueryFilter{agentTypeID=prefab.GetComponent<NavMeshAgent>().agentTypeID,areaMask=NavMesh.AllAreas};
            if(!NavMesh.SamplePosition(arrivalPoint,out var hit,2,filter)){Debug.LogWarning("No guest NavMesh at arrival point.",this);return null;}
            var guest=Instantiate(prefab,hit.position,Quaternion.Euler(0,180,0),transform);
            HotelCorrectedCharacter.ApplyCustomerLook(guest.GetComponentInChildren<HotelGuestVisual>(true), guestSerial);
            guests.Add(guest);guest.Initialize(this,Names[guestSerial%Names.Length]+" "+(guestSerial/Names.Length+1));guestSerial++;return guest;
        }
        public int QueuePosition(HotelGuestBot guest)=>queue.IndexOf(guest);
        public Vector3 QueuePoint(int slot)=>receptionPoint+Vector3.right*slot*.9f;
        public void JoinQueue(HotelGuestBot guest){if(!queue.Contains(guest))queue.Add(guest);}
        public void LeaveQueue(HotelGuestBot guest){queue.Remove(guest);foreach(var g in queue)if(g)g.RepositionQueue();}
        public int FindFreeRoom()
        {
            for(int i=0;i<rooms.Length;i++){bool occupied=false;foreach(var g in guests)if(g&&g.RoomIndex==i){occupied=true;break;}if(!occupied)return i;}return -1;
        }
        public int AcquireSeat(HotelGuestBot guest,bool dining)
        {
            for(int i=0;i<seats.Length;i++)if(seats[i].dining==dining&&!occupiedSeats.ContainsKey(i)&&(!player||player.seated!=seats[i].chair)){occupiedSeats[i]=guest;return i;}return -1;
        }
        public bool IsSeatOccupied(HotelItem chair){foreach(var entry in occupiedSeats)if(seats[entry.Key].chair==chair)return true;return false;}
        public void ReleaseSeat(int index,HotelGuestBot guest){if(occupiedSeats.TryGetValue(index,out var owner)&&owner==guest)occupiedSeats.Remove(index);}
        public void RemoveGuest(HotelGuestBot guest){Forget(guest);Destroy(guest.gameObject);}
        public void Forget(HotelGuestBot guest)
        {
            if(serviceJobs)serviceJobs.Forget(guest);
            guests.Remove(guest);LeaveQueue(guest);if(guest.SeatIndex>=0)ReleaseSeat(guest.SeatIndex,guest);ShowMeal(guest,false);
        }
        public void OpenNearbyDoors(Vector3 position)
        {
            foreach(var door in doors)
            {
                if(!door||door.kind!="door"||Mathf.Abs(door.transform.position.y-position.y)>2)continue;
                var delta=door.transform.position-position;delta.y=0;if(delta.sqrMagnitude>9)continue;
                var defense=door.GetComponent<HotelDefenseOpening>();if(defense&&(defense.BoardCount>0||IsNight))continue;
                var entrance=door.GetComponent<HotelAutomaticEntrance>();if(entrance)entrance.RequestOpen();else door.open=true;
            }
        }
        public void RecordHousekeeping(int tip){Services++;Tips+=tip;}
        public void RecordService(HotelGuestBot guest,int baseTip){Services++;Tips+=Mathf.RoundToInt(baseTip*Mathf.Lerp(.3f,1,guest.Satisfaction));}
        void Restock()
        {
            supplies.RemoveAll(s=>!s);
            foreach(var station in stations)
            {
                if(!station.point||!station.prefab||supplies.Count>=36)continue;
                if(station.current&&station.current.enabled&&player.held!=station.current&&Vector3.Distance(station.current.transform.position,station.point.position)<.45f)continue;
                var item=Instantiate(station.prefab,station.point.position,station.point.rotation,transform);station.current=item;supplies.Add(item.GetComponent<HotelGuestSupply>());
            }
        }
        public void ShowMeal(HotelGuestBot guest,bool show)
        {
            if(meals.TryGetValue(guest,out var old)){if(old){if(!show&&serviceJobs&&serviceJobs.housekeeping)serviceJobs.housekeeping.SpawnMess(FloorMessType.Litter,guest.transform.position+guest.transform.right*.65f);Destroy(old);}meals.Remove(guest);}
            if(!show||guest.SeatIndex<0)return;
            var seat=seats[guest.SeatIndex];var mealStation=Array.Find(stations,s=>s.prefab&&s.prefab.GetComponent<HotelGuestSupply>().type==GuestNeed.Meal&&s.prefab.GetComponent<HotelGuestSupply>().dish==guest.RequestedDish);
            if(mealStation==null)return;
            Vector3 position=seat.table?seat.table.position+Vector3.up*.44f:guest.transform.position+guest.transform.forward*.4f+Vector3.up*.82f;
            var model=Instantiate(mealStation.prefab.gameObject,position,Quaternion.identity,transform);foreach(var c in model.GetComponentsInChildren<Collider>())Destroy(c);Destroy(model.GetComponent<Rigidbody>());Destroy(model.GetComponent<HotelGuestSupply>());Destroy(model.GetComponent<HotelItem>());meals[guest]=model;
        }
        public void ResetSimulation()
        {
            if(waterStation)waterStation.ResetStation();
            if(serviceJobs)serviceJobs.ResetJobs();
            foreach(var guest in guests.ToArray())if(guest)Destroy(guest.gameObject);guests.Clear();queue.Clear();occupiedSeats.Clear();
            foreach(var supply in supplies)if(supply)Destroy(supply.gameObject);supplies.Clear();foreach(var meal in meals.Values)if(meal)Destroy(meal);meals.Clear();
            foreach(var s in stations)s.current=null;elapsed=0;DayNumber=1;Tips=Services=guestSerial=0;nextArrival=4;Restock();UpdateLighting();
        }
        void UpdateLighting()
        {
            // Dusk starts before the zombie wave; dawn reverses when the next day begins.
            float transition=DayNumber>1&&elapsed<18
                ? 1-Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/18))
                : Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-(daySeconds-25))/35));
            if(sun){sun.intensity=Mathf.Lerp(sunIntensity,.18f,transition);sun.color=Color.Lerp(sunColor,new Color(.40f,.52f,.82f),transition);sun.transform.rotation=Quaternion.Slerp(sunRotation,Quaternion.Euler(35,-40,0),transition);}
            RenderSettings.ambientIntensity=Mathf.Lerp(ambientIntensity,.35f,transition);RenderSettings.ambientLight=Color.Lerp(ambientColor,new Color(.12f,.17f,.28f),transition);
            if(!cityBackground&&Time.time>=backgroundSearch)
            {
                backgroundSearch=Time.time+2;
                // The panorama is a DontSave child and is intentionally omitted by scene-wide FindObjects calls.
                foreach(var atmosphere in FindObjectsByType<HotelCityAtmosphere>(FindObjectsSortMode.None))
                foreach(var renderer in atmosphere.GetComponentsInChildren<MeshRenderer>(true))
                    if(renderer.sharedMaterial&&renderer.sharedMaterial.shader.name=="HotelTogether/City Skydome")
                    {cityBackground=renderer;cityExposure=renderer.sharedMaterial.GetFloat("_Exposure");originalBackground=new MaterialPropertyBlock();renderer.GetPropertyBlock(originalBackground);nightBackground=new MaterialPropertyBlock();renderer.GetPropertyBlock(nightBackground);break;}
            }
            if(cityBackground)
            {
                nightBackground.SetFloat("_NightBlend",transition);
                nightBackground.SetFloat("_Exposure",Mathf.Lerp(cityExposure,cityExposure*.85f,transition));
                cityBackground.SetPropertyBlock(nightBackground);
            }
        }
        void OnDestroy()
        {
            if(sun){sun.intensity=sunIntensity;sun.color=sunColor;sun.transform.rotation=sunRotation;}RenderSettings.ambientIntensity=ambientIntensity;RenderSettings.ambientLight=ambientColor;
            if(cityBackground)cityBackground.SetPropertyBlock(originalBackground);
        }
    }
}
