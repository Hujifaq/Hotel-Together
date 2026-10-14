using UnityEngine;
namespace HotelTogether
{
    public enum MaintenanceIncidentKind { Fire, PipeBurst, ElevatorFault, PowerOutage, WaterOutage }
    public enum MaintenanceIncidentState { Ready, Warning, Active, Resolved }
    [RequireComponent(typeof(HotelItem))]
    public sealed class HotelMaintenanceIncident : MonoBehaviour
    {
        public HotelMaintenanceSystem system;
        public MaintenanceIncidentKind kind;
        public string location;
        public HotelElevator elevator;
        public HotelWaterFixture fixture;
        public Vector3 workPoint;
        public Transform valve,cover,breaker;
        public ParticleSystem effect,smoke;
        public Light effectLight;
        public Renderer indicator;
        public AudioSource ambientSource;
        public Collider selectionCollider;
        MaterialPropertyBlock indicatorBlock;
        public MaintenanceIncidentState State { get; private set; }
        public bool Unresolved=>State==MaintenanceIncidentState.Warning||State==MaintenanceIncidentState.Active;
        public bool CanBegin=>!Unresolved&&Time.time>=cooledUntil;
        public int Step { get; private set; }
        public float Progress { get; private set; }
        public float Severity { get; private set; }
        public float WarningLeft { get; private set; }
        float activeTime,puddleTime,cooledUntil;
        public bool WaterIsolated=>kind==MaintenanceIncidentKind.PipeBurst&&Step>=1;
        public Vector3 Target=>transform.TransformPoint(workPoint);
        public string Title=>kind switch{MaintenanceIncidentKind.Fire=>"FIRE",MaintenanceIncidentKind.PipeBurst=>"BURST PIPE",MaintenanceIncidentKind.ElevatorFault=>"ELEVATOR FAULT",MaintenanceIncidentKind.WaterOutage=>"WATER OUTAGE",_=>"POWER OUTAGE"};
        public MaintenanceAction Action=>kind switch
        {
            MaintenanceIncidentKind.Fire=>MaintenanceAction.Spray,
            MaintenanceIncidentKind.PipeBurst=>Step==0?MaintenanceAction.Valve:Step==1?MaintenanceAction.Wrench:MaintenanceAction.Valve,
            MaintenanceIncidentKind.WaterOutage=>Step==0?MaintenanceAction.Valve:Step==1?MaintenanceAction.Wrench:MaintenanceAction.Breaker,
            MaintenanceIncidentKind.ElevatorFault=>Step==0?MaintenanceAction.Wrench:MaintenanceAction.Breaker,
            _=>Step==0?MaintenanceAction.Screwdriver:MaintenanceAction.Breaker
        };
        public string ActionLabel=>State==MaintenanceIncidentState.Warning?"INSPECT WARNING":State!=MaintenanceIncidentState.Active?"SYSTEM NORMAL":Action switch
        {
            MaintenanceAction.Spray=>"HOLD LMB  EXTINGUISH BASE",
            MaintenanceAction.Valve=>Step==0?"HOLD LMB  CLOSE WATER VALVE":"HOLD LMB  RESTORE WATER",
            MaintenanceAction.Wrench=>"HOLD LMB  REPAIR WITH WRENCH",
            MaintenanceAction.Screwdriver=>"HOLD LMB  REPLACE FAILED FUSE",
            _=>"HOLD LMB  TEST AND RESTART"
        };
        public string Instructions=>kind switch
        {
            MaintenanceIncidentKind.Fire=>"Take a fire extinguisher. Aim at the base and hold LMB within 4.5m. LMB at the maintenance rack refills it.",
            MaintenanceIncidentKind.PipeBurst=>"Hold LMB to close the valve. Hold the wrench and LMB to seal the pipe. Then hold LMB to reopen and test the water.",
            MaintenanceIncidentKind.ElevatorFault=>"Use the wrench at the service cabinet, then hold LMB to run the restart test. Passengers exit at a landing; new trips use another lift or stairs.",
            MaintenanceIncidentKind.WaterOutage=>"Close the inlet valve, repair the pump coupling with a wrench, then hold LMB to restart and pressure-test the water supply.",
            _=>"Power is isolated automatically. Use the insulated screwdriver to replace the fuse, then hold LMB to restore power. Emergency lights remain on."
        };
        public void Begin(float warningSeconds=8)
        {
            if(Unresolved||Time.time<cooledUntil)return;
            State=MaintenanceIncidentState.Warning;WarningLeft=Mathf.Max(0,warningSeconds);Step=0;Progress=0;Severity=.2f;activeTime=puddleTime=0;
            if(selectionCollider)selectionCollider.enabled=true;
            if(ambientSource&&system.FaultTone)ambientSource.PlayOneShot(system.FaultTone,.5f);
            if(WarningLeft==0)Activate();
        }
        void Activate()
        {
            State=MaintenanceIncidentState.Active;
            if(elevator)elevator.SetMechanicalFault(true);
            if(fixture)fixture.SetMaintenanceIsolation(true);
            if(kind==MaintenanceIncidentKind.PowerOutage)system.SetPower(false);if(kind==MaintenanceIncidentKind.WaterOutage)system.SetWater(false);
            SetEffects(true);
            system.Announce(Title+" / "+location+". "+Instructions);
        }
        public void Advance(float dt)
        {
            if(State==MaintenanceIncidentState.Warning)
            {
                WarningLeft=Mathf.Max(0,WarningLeft-dt);if(WarningLeft==0)Activate();
            }
            else if(State==MaintenanceIncidentState.Active)
            {
                activeTime+=dt;Severity=Mathf.Clamp01(.2f+activeTime/180);
                if(kind==MaintenanceIncidentKind.PipeBurst&&!WaterIsolated&&system.housekeeping)
                {
                    puddleTime+=dt;
                    if(puddleTime>=28){puddleTime=0;var point=transform.position+transform.forward*.65f;point.y=system.FloorHeight(point.y);system.housekeeping.SpawnMess(FloorMessType.Wet,point);}
                }
            }
            if(valve)valve.localRotation=Quaternion.Euler(0,0,Step==0?Progress*90:Step==1?90:Step==2?(1-Progress)*90:0);
            if(cover)cover.localRotation=Quaternion.Slerp(cover.localRotation,Quaternion.Euler(0,Mathf.SmoothStep(0,-65,State==MaintenanceIncidentState.Active?Mathf.Clamp01((Step+Progress)*2):0),0),1-Mathf.Exp(-dt*8));
            if(breaker)breaker.localRotation=Quaternion.Euler(State==MaintenanceIncidentState.Active?-30+Progress*20:20,0,0);
            if(indicator){indicatorBlock??=new MaterialPropertyBlock();indicator.GetPropertyBlock(indicatorBlock);indicatorBlock.SetColor("_BaseColor",Unresolved?new Color(1,.20f,.04f):new Color(.08f,.8f,.34f));indicator.SetPropertyBlock(indicatorBlock);}
            if(kind==MaintenanceIncidentKind.Fire&&effect&&State==MaintenanceIncidentState.Active){var emission=effect.emission;emission.rateOverTimeMultiplier=Mathf.Lerp(48,5,Progress);}
            if(effectLight&&Unresolved)effectLight.intensity=(kind==MaintenanceIncidentKind.Fire?1.8f:.3f)*(1+Mathf.Sin(Time.time*16)*.15f);
        }
        public bool CanWork(HotelPlayer player,out string reason)
        {
            reason=null;if(State!=MaintenanceIncidentState.Active){reason=State==MaintenanceIncidentState.Warning?"Fault warning: wait for diagnosis.":"This system is working normally.";return false;}
            if(!player||player.cart||player.seated||player.GrabbedGuest||player.combat&&player.combat.IsDown){reason="Park the cart and stand up before maintenance.";return false;}
            if(kind==MaintenanceIncidentKind.WaterOutage&&Action==MaintenanceAction.Breaker&&!system.HasPower){reason="Restore power in G05 before restarting the water pump.";return false;}
            var tool=player.held?player.held.GetComponent<HotelMaintenanceTool>():null;
            MaintenanceToolKind? required=Action==MaintenanceAction.Spray?MaintenanceToolKind.Extinguisher:Action==MaintenanceAction.Wrench?MaintenanceToolKind.Wrench:Action==MaintenanceAction.Screwdriver?MaintenanceToolKind.Screwdriver:null;
            if(required.HasValue&&(!tool||tool.kind!=required.Value)){reason="Required: "+required+". Tools are at the maintenance rack near the staff room.";return false;}
            if(required==MaintenanceToolKind.Extinguisher&&tool.Charge<=0){reason="Extinguisher empty. LMB at the maintenance rack to refill.";return false;}
            return true;
        }
        public bool Work(HotelPlayer player,float dt,bool spraying=false)
        {
            if(dt<=0||!CanWork(player,out _)||Action==MaintenanceAction.Spray&&!spraying)return false;
            float range=Action==MaintenanceAction.Spray?4.5f:2.6f;
            if(Vector3.Distance(player.view.transform.position,Target)>range)return false;
            return AdvanceRepair(dt);
        }
        public bool StaffWork(Vector3 position,float dt){if(dt<=0||!float.IsFinite(dt)||State!=MaintenanceIncidentState.Active||Vector3.Distance(position+Vector3.up,Target)>2.6f||kind==MaintenanceIncidentKind.WaterOutage&&Action==MaintenanceAction.Breaker&&!system.HasPower)return false;return AdvanceRepair(dt*.7f);}
        bool AdvanceRepair(float dt){
            float seconds=Action==MaintenanceAction.Spray?5.5f:Action==MaintenanceAction.Valve?1.6f:Action==MaintenanceAction.Breaker?2.5f:5.0f;
            Progress=Mathf.Min(1,Progress+dt/seconds);
            if(Progress<1)return true;
            if(kind==MaintenanceIncidentKind.Fire)Resolve();
            else
            {
                Step++;Progress=0;
                if(kind==MaintenanceIncidentKind.PipeBurst&&Step==1)SetEffects(false);
                int total=kind==MaintenanceIncidentKind.PipeBurst||kind==MaintenanceIncidentKind.WaterOutage?3:2;
                if(Step>=total)Resolve();else system.Announce(location+": step completed. "+ActionLabel);
            }
            return true;
        }
        void Resolve()
        {
            State=MaintenanceIncidentState.Resolved;Progress=1;SetEffects(false);RestoreService();cooledUntil=Time.time+120;
            if(selectionCollider)selectionCollider.enabled=false;
            system.Completed(this);
        }
        void RestoreService(){if(elevator)elevator.SetMechanicalFault(false);if(fixture)fixture.SetMaintenanceIsolation(!system.WaterAvailable);if(kind==MaintenanceIncidentKind.PowerOutage)system.SetPower(true);if(kind==MaintenanceIncidentKind.WaterOutage)system.SetWater(true);}
        void SetEffects(bool on)
        {
            if(ambientSource){if(on&&!ambientSource.isPlaying)ambientSource.Play();else if(!on)ambientSource.Stop();}
            foreach(var particles in new[]{effect,smoke})if(particles){if(on)particles.Play();else particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
            if(effectLight)effectLight.enabled=on;
        }
        public void ResetIncident(){SetEffects(false);RestoreService();if(selectionCollider)selectionCollider.enabled=false;State=MaintenanceIncidentState.Ready;Step=0;Progress=0;Severity=0;cooledUntil=0;WarningLeft=0;activeTime=puddleTime=0;}
        void OnDisable(){if(Unresolved)ResetIncident();}
    }
}
