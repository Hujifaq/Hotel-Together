using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace HotelTogether
{
    [DefaultExecutionOrder(100)]
    public sealed class HotelMaintenanceSystem : MonoBehaviour
    {
        public HotelGuestSimulation hotel;
        public HotelHousekeepingSystem housekeeping;
        public float firstFaultSeconds=55,intervalSeconds=115;
        public int maximumConcurrent=2;
        public bool automaticIncidents=true;
        public readonly List<HotelMaintenanceIncident> incidents=new();
        public readonly List<HotelMaintenanceTool> tools=new();
        public HotelMaintenanceRack rack;
        public HotelMaintenanceIncident Working { get; private set; }
        public MaintenanceAction CurrentAction { get; private set; }
        public bool HasPower { get; private set; }=true;
        public bool HasWater {get;private set;}=true;public bool WaterAvailable=>HasWater&&HasPower;
        public void SetWater(bool available){HasWater=available;ApplyWater();}
        void ApplyWater(){foreach(var fixture in FindObjectsByType<HotelWaterFixture>(FindObjectsSortMode.None)){bool isolated=!WaterAvailable;foreach(var incident in incidents)if(incident&&incident.fixture==fixture&&incident.Unresolved)isolated=true;fixture.SetMaintenanceIsolation(isolated);}if(hotel&&hotel.waterStation)hotel.waterStation.SetSupply(WaterAvailable);}

        public int CompletedCount { get; private set; }
        public bool Initialized { get; private set; }
        public Material FoamMaterial { get; private set; }
        public AudioClip Hiss { get; private set; }
        public AudioClip FaultTone { get; private set; }
        public int ActiveCount { get { int n=0;foreach(var incident in incidents)if(incident&&incident.Unresolved)n++;return n; } }
        readonly List<LightState> lightStates=new();
        readonly List<Light> emergencyLights=new();
        readonly List<Material> ownedMaterials=new();
        readonly List<Texture2D> ownedTextures=new();
        float nextFault,concernAt;
        int nextKind;
        HotelMaintenanceTool activeSprayer;
        sealed class LightState { public Light light;public bool enabled; }
        void Start(){if(!Initialized&&gameObject.scene.name=="HotelLobby")Initialize(GetComponent<HotelGuestSimulation>());}
        public void Initialize(HotelGuestSimulation simulation,bool createModels=true)
        {
            if(Initialized)return;Initialized=true;hotel=simulation;housekeeping=simulation?simulation.GetComponent<HotelHousekeepingSystem>():null;
            FoamMaterial=CreateMaterial("Maintenance foam",new Color(.9f,.97f,1,.7f),true);
            Hiss=AudioClip.Create("Maintenance air hiss",24000,1,24000,false);
            var samples=new float[24000];var random=new System.Random(46);float last=0;
            for(int i=0;i<samples.Length;i++){float noise=(float)random.NextDouble()*2-1;last=Mathf.Lerp(last,noise,.6f);samples[i]=last*.22f;}Hiss.SetData(samples,0);
            FaultTone=AudioClip.Create("Maintenance warning tone",24000,1,24000,false);
            for(int i=0;i<samples.Length;i++){float t=i/24000f;samples[i]=Mathf.Sin(t*Mathf.PI*2*880)*.12f*(Mathf.Repeat(t,.5f)<.15f?1:0);}FaultTone.SetData(samples,0);
            nextFault=firstFaultSeconds;
            if(createModels&&hotel)HotelMaintenanceModels.Build(this);
        }
        public Material CreateMaterial(string name,Color color,bool particles=false)
        {
            var shader=Shader.Find(particles?"Universal Render Pipeline/Particles/Unlit":"Universal Render Pipeline/Lit");if(!shader)shader=Shader.Find("Standard");
            var material=new Material(shader){name=name,color=color};ownedMaterials.Add(material);
            if(particles)
            {
                var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);texture.name="Soft particle falloff";
                var pixels=new Color[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++){float radius=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-radius),1.5f));}texture.SetPixels(pixels);texture.Apply();ownedTextures.Add(texture);material.SetTexture("_BaseMap",texture);material.mainTexture=texture;
                material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",10);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;
            }
            return material;
        }
        public void RegisterEmergency(Light light){if(!light)return;if(!emergencyLights.Contains(light))emergencyLights.Add(light);light.enabled=!HasPower;}
        public float FloorHeight(float y)
        {
            if(hotel&&hotel.elevators!=null&&hotel.elevators.Length>0&&hotel.elevators[0]){var lift=hotel.elevators[0];return lift.floors[lift.FloorFor(y)].height;}
            return Mathf.Round(y/4.5f)*4.5f+.025f;
        }
        public void Announce(string text){if(hotel&&hotel.player)hotel.player.Notify(text,true);}
        public bool Trigger(MaintenanceIncidentKind kind)
        {
            if(ActiveCount>=maximumConcurrent)return false;
            if(kind==MaintenanceIncidentKind.ElevatorFault||kind==MaintenanceIncidentKind.PowerOutage)
                foreach(var incident in incidents)if(incident.Unresolved&&(incident.kind==MaintenanceIncidentKind.ElevatorFault||incident.kind==MaintenanceIncidentKind.PowerOutage))return false;
            foreach(var incident in incidents)if(incident.kind==kind&&incident.CanBegin){incident.Begin();Announce("MAINTENANCE WARNING / "+incident.location+" / "+incident.Title+" in 8 seconds. Check shift notes with J.");return true;}
            return false;
        }
        public void Advance(float dt)
        {
            if(dt<=0)return;
            foreach(var incident in incidents)if(incident)incident.Advance(dt);
            if(!hotel)return;
            if(automaticIncidents)
            {
                nextFault-=dt;
                if(nextFault<=0)
                {
                    bool started=false;
                    for(int attempt=0;attempt<5&&!started;attempt++){started=Trigger((MaintenanceIncidentKind)(nextKind%5));nextKind++;}
                    nextFault=started?Mathf.Max(65,intervalSeconds-CompletedCount*3):15;
                }
            }
            concernAt-=dt;
            if(concernAt<=0)
            {
                concernAt=8;
                foreach(var incident in incidents)if(incident.State==MaintenanceIncidentState.Active)
                    foreach(var guest in hotel.Guests)if(guest&&(incident.kind==MaintenanceIncidentKind.PowerOutage||Vector3.Distance(guest.transform.position,incident.transform.position)<6))guest.ReactToPrank(.008f*incident.Severity);
            }
        }
        void Update()
        {
            if(!Initialized||!hotel)return;
            if(!hotel.Running){StopWork();return;}
            Advance(Time.deltaTime);
            var player=hotel.player;
            if(!player||player.transportLocked||player.combat&&player.combat.IsDown){StopWork();return;}
            var keyboard=Keyboard.current;var mouse=Mouse.current;
            var tool=player.held?player.held.GetComponent<HotelMaintenanceTool>():null;
            bool spray=tool&&tool.kind==MaintenanceToolKind.Extinguisher&&player.WorkPrimaryHeld&&tool.Charge>0&&(!player.FocusedItem||!player.FocusedItem.GetComponent<HotelMaintenanceIncident>()||player.FocusedItem.GetComponent<HotelMaintenanceIncident>().kind==MaintenanceIncidentKind.Fire);
            if(activeSprayer&&activeSprayer!=tool)activeSprayer.SetSpraying(false,Vector3.forward,0);
            activeSprayer=tool;
            if(tool)tool.SetSpraying(spray,player.view.transform.forward,Time.deltaTime);
            if(spray)
            {
                var target=FindSprayTarget(player);
                if(target&&TryWork(target,player,Time.deltaTime,true)){}
                else{Working=null;CurrentAction=MaintenanceAction.Spray;}
            }
            else if(player.MaintenanceHeld&&player.FocusedItem)
            {
                var target=player.FocusedItem.GetComponent<HotelMaintenanceIncident>();
                if(target&&target.Action!=MaintenanceAction.Spray&&TryWork(target,player,Time.deltaTime)){}
                else{Working=null;CurrentAction=MaintenanceAction.None;}
            }
            else{Working=null;CurrentAction=MaintenanceAction.None;}
        }
        public bool TryWork(HotelMaintenanceIncident incident,HotelPlayer player,float dt,bool spraying=false)
        {
            var action=incident.Action;
            if(!incident.Work(player,dt*(hotel&&hotel.Economy&&hotel.Economy.Initialized?hotel.Economy.WorkSpeed:1),spraying)){Working=null;CurrentAction=MaintenanceAction.None;return false;}
            Working=incident.State==MaintenanceIncidentState.Active?incident:null;CurrentAction=Working?action:MaintenanceAction.None;return true;
        }
        public HotelMaintenanceIncident FindSprayTarget(HotelPlayer player)
        {
            HotelMaintenanceIncident best=null;float nearest=4.6f;var eye=player.view.transform;
            foreach(var incident in incidents)
            {
                if(incident.kind!=MaintenanceIncidentKind.Fire||incident.State!=MaintenanceIncidentState.Active)continue;
                var delta=incident.Target-eye.position;float distance=delta.magnitude;if(distance>4.5f||distance>=nearest||Vector3.Dot(delta.normalized,eye.forward)<.96f)continue;
                if(Physics.Raycast(eye.position,delta.normalized,out var hit,distance,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore)&&!hit.collider.transform.IsChildOf(incident.transform))continue;
                best=incident;nearest=distance;
            }
            return best;
        }
        public bool Inspect(HotelMaintenanceIncident incident,HotelPlayer player)
        {
            if(!incident.CanWork(player,out var reason)){player.Notify(reason);return false;}
            if(incident.Action==MaintenanceAction.Spray)player.Notify("Aim at the base and hold LMB. Sweep the nozzle across the fire.");
            return true;
        }
        public void Completed(HotelMaintenanceIncident incident)
        {
            CompletedCount++;Working=null;CurrentAction=MaintenanceAction.None;
            int tip=Mathf.RoundToInt(Mathf.Lerp(25,12,incident.Severity));hotel?.RecordHousekeeping(tip,true);
            Announce(incident.location+": "+incident.Title+" resolved. Maintenance +$"+tip+".");
        }
        public void SetPower(bool value)
        {
            if(value==HasPower)return;HasPower=value;
            if(!value)
            {
                lightStates.Clear();
                foreach(var light in FindObjectsByType<Light>(FindObjectsInactive.Include))
                {
                    if(light.gameObject.scene!=gameObject.scene||light.type==LightType.Directional||emergencyLights.Contains(light)||light.GetComponentInParent<HotelMaintenanceIncident>())continue;
                    lightStates.Add(new LightState{light=light,enabled=light.enabled});light.enabled=false;
                }
            }
            else{foreach(var state in lightStates)if(state.light)state.light.enabled=state.enabled;lightStates.Clear();}
            foreach(var light in emergencyLights)if(light)light.enabled=!value;ApplyWater();
            if(hotel&&hotel.elevators!=null)foreach(var lift in hotel.elevators)if(lift)lift.SetPowerAvailable(value);
        }
        void LateUpdate(){if(!HasPower)foreach(var state in lightStates)if(state.light)state.light.enabled=false;}
        public HotelServiceGuide Guide
        {
            get
            {
                HotelMaintenanceIncident target=Working;
                var p=hotel?hotel.player:null;
                if(!target&&p&&p.FocusedItem)target=p.FocusedItem.GetComponent<HotelMaintenanceIncident>();
                if(!target)foreach(var incident in incidents)if(incident&&incident.Unresolved&&(!target||p&&(incident.transform.position-p.transform.position).sqrMagnitude<(target.transform.position-p.transform.position).sqrMagnitude))target=incident;
                if(target&&target.Unresolved)return new HotelServiceGuide(target.Title+" / "+target.location,target.Instructions,target.Target);
                return default;
            }
        }
        public void StopWork(){Working=null;CurrentAction=MaintenanceAction.None;if(activeSprayer)activeSprayer.SetSpraying(false,Vector3.forward,0);activeSprayer=null;}
        public void ResetMaintenance(){StopWork();foreach(var incident in incidents)if(incident)incident.ResetIncident();SetPower(true);SetWater(true);foreach(var tool in tools)if(tool)tool.ResetTool();CompletedCount=nextKind=0;nextFault=firstFaultSeconds;concernAt=0;}
        void OnDisable(){if(Initialized)ResetMaintenance();}
        static void ReleaseOwned(UnityEngine.Object asset){if(!asset)return;if(Application.isPlaying)Destroy(asset);else DestroyImmediate(asset);}
        void OnDestroy(){ReleaseOwned(Hiss);ReleaseOwned(FaultTone);foreach(var material in ownedMaterials)ReleaseOwned(material);foreach(var texture in ownedTextures)ReleaseOwned(texture);}
    }
}
