using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.LowLevel;

namespace HotelTogether.Editor
{
    // Run in the disposable QA project. Public work APIs share the production input path.
    public static class HotelMaintenanceValidation
    {
        static readonly List<string> results=new();
        static HotelPlayer player;
        static HotelGuestSimulation hotel;
        static HotelMaintenanceSystem system;
        static HotelMaintenanceTool extinguisher,wrench,driver;
        static HotelMaintenanceIncident fire,pipe,power,liftFault;
        static HotelElevator lift;
        static int stage;
        static double next;
        static bool failed,oldOptions;
        static EnterPlayModeOptions oldMode;
        static PlayerLoopSystem oldLoop;
        static Light normal,originallyOff,emergency;
        static Vector3 frozen;
        public static void Run()
        {
            results.Clear();failed=false;stage=0;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            oldOptions=EditorSettings.enterPlayModeOptionsEnabled;oldMode=EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.playModeStateChanged+=State;EditorApplication.isPlaying=true;
        }
        static void Check(bool value,string text){if(!value)throw new Exception(text);results.Add("PASS: "+text);}
        static void State(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                try
                {
                    var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.position=Vector3.down*.15f;ground.transform.localScale=new Vector3(120,.3f,120);
                    var nav=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,transform=Matrix4x4.TRS(ground.transform.position,Quaternion.identity,Vector3.one),size=ground.transform.localScale,area=0}},new Bounds(Vector3.zero,new Vector3(120,10,120)),Vector3.zero,Quaternion.identity);NavMesh.AddNavMeshData(nav);
                    var go=new GameObject("Maintenance QA player");go.SetActive(false);go.layer=8;player=go.AddComponent<HotelPlayer>();var cam=new GameObject("QA camera",typeof(Camera));cam.transform.SetParent(go.transform);cam.transform.localPosition=Vector3.up*1.6f;player.view=cam.GetComponent<Camera>();go.SetActive(true);
                    hotel=new GameObject("Maintenance QA hotel").AddComponent<HotelGuestSimulation>();hotel.enabled=false;hotel.player=player;hotel.rooms=Array.Empty<HotelGuestSimulation.GuestRoom>();hotel.seats=Array.Empty<HotelGuestSimulation.GuestSeat>();hotel.stations=Array.Empty<HotelGuestSimulation.SupplyStation>();hotel.elevators=Array.Empty<HotelElevator>();
                    var dummy=new GameObject("Inactive guest settings");dummy.SetActive(false);var guest=dummy.AddComponent<HotelGuestBot>();dummy.GetComponent<NavMeshAgent>().agentTypeID=0;hotel.guestPrefabs=new[]{guest};
                    system=hotel.Maintenance;system.Initialize(hotel,false);system.automaticIncidents=false;
                    HotelMaintenanceModels.Build(system);
                    extinguisher=system.tools.Find(t=>t.kind==MaintenanceToolKind.Extinguisher);wrench=system.tools.Find(t=>t.kind==MaintenanceToolKind.Wrench);driver=system.tools.Find(t=>t.kind==MaintenanceToolKind.Screwdriver);
                    fire=Incident(MaintenanceIncidentKind.Fire,new Vector3(0,0,2));pipe=Incident(MaintenanceIncidentKind.PipeBurst,new Vector3(0,0,2));power=Incident(MaintenanceIncidentKind.PowerOutage,new Vector3(0,0,2));liftFault=Incident(MaintenanceIncidentKind.ElevatorFault,new Vector3(0,0,2));
                    var water=new GameObject("Fixture");water.AddComponent<HotelItem>();pipe.fixture=water.AddComponent<HotelWaterFixture>();
                    var liftGO=new GameObject("Test lift");liftGO.SetActive(false);liftGO.transform.position=new Vector3(8,0,-12.8f);lift=liftGO.AddComponent<HotelElevator>();lift.hotel=hotel;lift.cabin=new GameObject("Cabin").transform;lift.cabin.SetParent(liftGO.transform,false);lift.cabinDoors=Array.Empty<Transform>();lift.floors=new[]{new HotelElevator.Landing{label="Lobby",height=0,doors=Array.Empty<Transform>()},new HotelElevator.Landing{label="2",height=4.5f,doors=Array.Empty<Transform>()},new HotelElevator.Landing{label="3",height=9,doors=Array.Empty<Transform>()}};liftGO.SetActive(true);hotel.elevators=new[]{lift};liftFault.elevator=lift;
                    normal=new GameObject("Normal interior light").AddComponent<Light>();originallyOff=new GameObject("Originally off interior light").AddComponent<Light>();originallyOff.enabled=false;emergency=new GameObject("Emergency light").AddComponent<Light>();system.RegisterEmergency(emergency);
                    next=EditorApplication.timeSinceStartup+.5;EditorApplication.update+=Tick;
                }
                catch(Exception error){failed=true;results.Add("FAIL: "+error);Finish();}
            }
            else if(state==PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged-=State;EditorSettings.enterPlayModeOptionsEnabled=oldOptions;EditorSettings.enterPlayModeOptions=oldMode;
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/HotelMaintenanceValidation.txt",string.Join("\n",results));Debug.Log(string.Join("\n",results));if(Application.isBatchMode)EditorApplication.Exit(failed?1:0);
            }
        }
        static HotelMaintenanceIncident Incident(MaintenanceIncidentKind kind,Vector3 point)
        {
            var obj=new GameObject("QA "+kind);obj.transform.position=point;obj.AddComponent<HotelItem>().kind="maintenanceIncident";var incident=obj.AddComponent<HotelMaintenanceIncident>();incident.kind=kind;incident.location="Test "+kind;incident.workPoint=Vector3.up*.7f;incident.system=system;system.incidents.Add(incident);return incident;
        }
        static void Equip(HotelMaintenanceTool tool){var previous=player.held;player.Drop();if(previous){var body=previous.GetComponent<Rigidbody>();body.position=new Vector3(30,1,30);previous.transform.position=body.position;}typeof(HotelPlayer).GetMethod("PickUp",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,new object[]{tool.GetComponent<HotelItem>()});}
        static void Move(Vector3 point){var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=point;cc.enabled=true;player.Face(0);}
        static void Wait(float seconds){next=EditorApplication.timeSinceStartup+seconds;}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            // Editor import/indexing can delay the first engine startup frame past the wall timer.
            // Do not remove ScriptRunBehaviourUpdate before the player's Start has made its grip anchor.
            if(stage==0&&typeof(HotelPlayer).GetField("anchor",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player)==null){Wait(.1f);return;}
            try
            {
                switch(stage++)
                {
                    case 0:
                        oldLoop=PlayerLoop.GetCurrentPlayerLoop();var loop=oldLoop;RemoveUpdate(ref loop);PlayerLoop.SetPlayerLoop(loop);player.ResumeGameplay();Move(Vector3.zero);
                        Check(system.rack&&extinguisher&&wrench&&driver,"Equipment rack creates all three usable tool models");
                        Check(system.incidents.Count>=8,"Procedural fixture placement creates all incident categories");
                        fire.Begin();Check(fire.State==MaintenanceIncidentState.Warning,"Incident starts with a warning");
                        Check(!system.TryWork(fire,player,1,true),"Warning cannot be repaired prematurely");fire.Advance(8);
                        Equip(wrench);Check(!system.TryWork(fire,player,1,true)&&fire.Progress==0,"Wrong tool cannot extinguish a fire");
                        Equip(extinguisher);Check(!system.TryWork(fire,player,0,true)&&fire.Progress==0,"Zero elapsed time cannot advance a repair");
                        player.view.transform.LookAt(fire.Target);
                        if(system.FindSprayTarget(player)!=fire){var delta=fire.Target-player.view.transform.position;if(Physics.Raycast(player.view.transform.position,delta.normalized,out var obstruction,delta.magnitude,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))Debug.Log("SPRAY BLOCKED BY "+obstruction.collider.name+" layer "+obstruction.collider.gameObject.layer+" at "+obstruction.point);Debug.Log("SPRAY TEST eye="+player.view.transform.position+" target="+fire.Target+" state="+fire.State);}
                        Check(system.FindSprayTarget(player)==fire,"Aim cone acquires the fire base");
                        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,.9f,1);wall.transform.localScale=new Vector3(1,2,.15f);Physics.SyncTransforms();Check(system.FindSprayTarget(player)==null,"A wall blocks extinguishing through geometry");UnityEngine.Object.Destroy(wall);
                        extinguisher.SetSpraying(true,Vector3.forward,.5f);system.TryWork(fire,player,1,true);Check(fire.Progress>0&&extinguisher.Charge<extinguisher.capacity,"Spray consumes charge and reduces the fire");
                        Check(player.TryAnimationHandPose(1,out var hand,out var rotation,out var curl)&&float.IsFinite(hand.x)&&curl>.4f,"Spray action supplies a finite gripping hand animation");
                        frozen=extinguisher.nozzle.position;player.PauseGameplay();Wait(.4f);break;
                    case 1:
                        Check(Vector3.Distance(frozen,extinguisher.nozzle.position)<.001f,"Pause freezes held tool and nozzle pose");player.ResumeGameplay();
                        system.TryWork(fire,player,6,true);Check(fire.State==MaintenanceIncidentState.Resolved&&system.CompletedCount==1,"Extinguishing completes exactly one maintenance job");
                        Check(!system.TryWork(fire,player,1,true)&&system.CompletedCount==1,"Resolved incidents cannot pay duplicate rewards");
                        extinguisher.SetSpraying(true,Vector3.forward,20);Check(!extinguisher.SetSpraying(true,Vector3.forward,.1f)&&extinguisher.Charge==0,"Empty extinguisher stops spraying");system.rack.Use(player);Check(extinguisher.Charge==extinguisher.capacity&&player.CurrentGesture==HotelPlayerGesture.Refill,"Rack refills the cylinder with a refill gesture");
                        pipe.Begin(0);Check(pipe.fixture.MaintenanceIsolated,"Burst pipe isolates the real water fixture");pipe.fixture.SetRunning(true);Check(!pipe.fixture.IsRunning,"Isolated plumbing rejects tap flow");
                        player.Drop();system.TryWork(pipe,player,2);Check(pipe.Step==1&&pipe.WaterIsolated,"Valve closure persists as a completed stage");
                        Check(!system.TryWork(pipe,player,1)&&pipe.Step==1,"Pipe seal requires the wrench after valve closure");
                        Equip(wrench);system.TryWork(pipe,player,2);float progress=pipe.Progress;system.StopWork();Check(pipe.Progress==progress,"Interrupting work preserves partial repair progress");
                        system.TryWork(pipe,player,4);Check(pipe.Step==2,"Wrench seals the isolated pipe");system.TryWork(pipe,player,2);Check(pipe.State==MaintenanceIncidentState.Resolved&&!pipe.fixture.MaintenanceIsolated,"Reopening and testing restores water service");
                        power.Begin(0);Check(!system.HasPower&&!normal.enabled&&!originallyOff.enabled&&emergency.enabled,"Blackout disables room lights and enables emergency lighting");Check(!lift.Operational,"Blackout prevents new elevator calls");
                        Check(!system.TryWork(power,player,1),"Power cabinet rejects an uninsulated wrench");Equip(driver);system.TryWork(power,player,1);Check(system.CurrentAction==MaintenanceAction.Screwdriver,"Fuse replacement selects screwdriver animation");system.TryWork(power,player,5);Check(power.Step==1&&!system.HasPower,"Replacing a fuse does not energize the circuit before testing");system.TryWork(power,player,3);Check(system.HasPower&&normal.enabled&&!originallyOff.enabled&&!emergency.enabled,"Breaker test restores each light's previous state");Check(lift.Operational,"Power restoration re-enables elevator requests");
                        player.Drop();lift.ResetElevator();Move(lift.cabin.position);lift.RequestFloor(1);lift.AdvanceState(.1f);lift.AdvanceState(1.2f);Check(lift.State==ElevatorState.Moving,"A normal call starts a real cabin trip");lift.AdvanceTravel(.5f);float y=lift.cabin.position.y;
                        liftFault.Begin(0);Check(!lift.Operational&&!lift.RequestFloor(2)&&lift.State==ElevatorState.Moving,"Mid-trip fault rejects new trips and retains continuous travel");lift.AdvanceState(.5f);Check(lift.DoorAmount==0,"Fault does not open doors between landings");lift.AdvanceTravel(0);Check(lift.cabin.position.y==y,"Zero delta freezes emergency travel");for(int i=0;i<30;i++)lift.AdvanceTravel(.2f);lift.AdvanceState(2);Check(lift.CurrentFloor==1&&lift.DoorAmount==1,"Emergency drive lands the cabin and opens doors safely");Check(Mathf.Abs(player.transform.position.y-4.5f)<.1f,"Riding player is carried continuously to the emergency landing");
                        Move(Vector3.zero);Equip(wrench);system.TryWork(liftFault,player,6);Check(!lift.Operational&&liftFault.Step==1,"Mechanical repair still requires the restart test");system.TryWork(liftFault,player,3);Check(lift.Operational&&liftFault.State==MaintenanceIncidentState.Resolved,"Restart test restores elevator service");
                        system.ResetMaintenance();Check(system.ActiveCount==0&&system.CompletedCount==0&&system.HasPower,"Reset clears faults, counters and outage effects");Check(system.Trigger(MaintenanceIncidentKind.PowerOutage),"Scheduler can create a critical fault");Check(!system.Trigger(MaintenanceIncidentKind.ElevatorFault),"Scheduler prevents overlapping lift and power failures");Check(system.Trigger(MaintenanceIncidentKind.Fire)&&!system.Trigger(MaintenanceIncidentKind.PipeBurst),"Concurrent incidents are capped at two");
                        system.ResetMaintenance();Check(system.Trigger(MaintenanceIncidentKind.PipeBurst),"Reset makes repaired incident sites reusable");system.enabled=false;Check(system.ActiveCount==0&&system.HasPower,"Disabling the system restores affected services");
                        Finish();break;
                }
            }
            catch(Exception error){failed=true;results.Add("FAIL: "+error);Finish();}
        }
        static void RemoveUpdate(ref PlayerLoopSystem loop)
        {
            if(loop.subSystemList==null)return;var children=new List<PlayerLoopSystem>();foreach(var child in loop.subSystemList){if(child.type==typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate))continue;var item=child;RemoveUpdate(ref item);children.Add(item);}loop.subSystemList=children.ToArray();
        }
        static void Finish(){if(oldLoop.subSystemList!=null)PlayerLoop.SetPlayerLoop(oldLoop);Time.timeScale=1;EditorApplication.update-=Tick;EditorApplication.isPlaying=false;}
    }
}
