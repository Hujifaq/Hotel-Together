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
    // Batch validation in a separate project: uses the actual customer prefab and engine physics.
    public static class HotelGuestPhysicsValidation
    {
        static readonly List<string> results=new();
        static HotelPlayer player;
        static HotelGuestBot guest;
        static HotelGuestPhysics physics;
        static double next, deadline;
        static int stage;
        static bool failed, oldOptions;
        static EnterPlayModeOptions oldMode;
        static Vector3 frozen;
        static NavMeshData nav;
        static int beforeHits;
        static int repeat;static float maximumAnchorError;
        static PlayerLoopSystem previousLoop;
        public static void Run()
        {
            results.Clear();stage=0;repeat=0;maximumAnchorError=0;failed=false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            oldOptions=EditorSettings.enterPlayModeOptionsEnabled;oldMode=EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.playModeStateChanged+=State;EditorApplication.isPlaying=true;
        }
        static void State(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                try
                {
                    var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.position=Vector3.down*.15f;ground.transform.localScale=new Vector3(40,.3f,40);
                    nav=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,transform=Matrix4x4.TRS(ground.transform.position,Quaternion.identity,Vector3.one),size=ground.transform.localScale,area=0}},new Bounds(Vector3.zero,new Vector3(40,8,40)),Vector3.zero,Quaternion.identity);
                    NavMesh.AddNavMeshData(nav);
                    var go=new GameObject("Physics validation player");go.SetActive(false);player=go.AddComponent<HotelPlayer>();
                    var camera=new GameObject("Camera",typeof(Camera));camera.transform.SetParent(go.transform);camera.transform.localPosition=new Vector3(0,1.4f,0);player.view=camera.GetComponent<Camera>();go.SetActive(true);
                    var hotel=new GameObject("Validation hotel").AddComponent<HotelGuestSimulation>();hotel.enabled=false;hotel.player=player;hotel.seats=Array.Empty<HotelGuestSimulation.GuestSeat>();hotel.rooms=Array.Empty<HotelGuestSimulation.GuestRoom>();hotel.receptionPoint=new Vector3(0,0,1.8f);
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HotelTogether/Prefabs/Guests/Tourist Milo.prefab");
                    var parent=new GameObject("Inactive prefab staging");parent.SetActive(false);
                    var template=UnityEngine.Object.Instantiate(prefab,parent.transform);template.GetComponent<NavMeshAgent>().enabled=false;template.GetComponent<NavMeshAgent>().agentTypeID=0;template.transform.position=new Vector3(0,0,1.8f);template.GetComponent<NavMeshAgent>().enabled=true;parent.SetActive(true);guest=template.GetComponent<HotelGuestBot>();
                    var agent=guest.GetComponent<NavMeshAgent>();agent.enabled=false;agent.agentTypeID=0;agent.enabled=true;agent.Warp(new Vector3(0,0,1.8f));
                    guest.Initialize(hotel,"Milo");physics=guest.PrankPhysics;
                    next=EditorApplication.timeSinceStartup+.5;EditorApplication.update+=Tick;
                }
                catch(Exception error){failed=true;results.Add("FAIL: "+error);Finish();}
            }
            else if(state==PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged-=State;EditorSettings.enterPlayModeOptionsEnabled=oldOptions;EditorSettings.enterPlayModeOptions=oldMode;
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/HotelGuestPhysicsValidation.txt",string.Join("\n",results));
                Debug.Log(string.Join("\n",results));if(Application.isBatchMode)EditorApplication.Exit(failed?1:0);
            }
        }
        static void Check(bool value,string text){if(!value)throw new Exception(text);results.Add("PASS: "+text);}
        static void Wait(float seconds){next=EditorApplication.timeSinceStartup+seconds;}
        static void Tick()
        {
            if(stage>=6&&physics&&physics.Suspended){foreach(var body in physics.GetComponentsInChildren<Rigidbody>()){var joint=body.GetComponent<CharacterJoint>();if(joint){float error=Vector3.Distance(body.position+body.rotation*joint.anchor,joint.connectedBody.position+joint.connectedBody.rotation*joint.connectedAnchor);maximumAnchorError=Mathf.Max(maximumAnchorError,error);if(error>.25f){failed=true;results.Add("FAIL: Repeated-grab joint separation "+error+" on "+body.name);Finish();return;}}}}
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                switch(stage)
                {
                    case 0:
                        // A headless Editor cannot retain cursor capture. Keep FixedUpdate/LateUpdate
                        // and real physics, while driving gameplay entry points directly.
                        previousLoop=PlayerLoop.GetCurrentPlayerLoop();var loop=previousLoop;RemoveUpdate(ref loop);PlayerLoop.SetPlayerLoop(loop);
                        player.ResumeGameplay();player.Face(0);player.view.transform.LookAt(physics.HeadPoint);
                        Check(player.GrabFocusedGuest(),"Looking at the head grabs the actual customer prefab");
                        Check(physics.Suspended&&!guest.Agent.enabled,"Ragdoll suspends guest navigation");
                        Check(physics.GetComponentsInChildren<Rigidbody>().Length==11,"Eleven articulated bodies are created");
                        stage++;Wait(.7f);break;
                    case 1:
                        foreach(var body in physics.GetComponentsInChildren<Rigidbody>())Check(float.IsFinite(body.position.x)&&body.linearVelocity.magnitude<30,"Ragdoll body remains finite and bounded: "+body.name);
                        Check(player.TryAnimationHandPose(1,out var hand,out var rotation,out var curl)&&Vector3.Distance(hand,physics.HeadPoint)<.3f,"Player gripping hand follows the head");
                        frozen=physics.HeadPoint;player.PauseGameplay();stage++;Wait(.4f);break;
                    case 2:
                        Check(Vector3.Distance(frozen,physics.HeadPoint)<.001f,"Pause freezes held ragdoll");player.ResumeGameplay();
                        typeof(HotelPlayer).GetMethod("ReleaseGuest",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,new object[]{true});
                        Check(!physics.IsHeld&&!player.GrabbedGuest,"Throw releases both ownership references");
                        Check(player.CurrentGesture==HotelPlayerGesture.Throw,"Throw starts player follow-through animation");
                        Check(Vector3.Dot(physics.GetComponentsInChildren<Rigidbody>()[0].linearVelocity,player.view.transform.forward)>5,"Throw transfers forward velocity to the body");
                        deadline=EditorApplication.timeSinceStartup+18;stage++;Wait(.5f);break;
                    case 3:
                        if(physics.Suspended){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Guest failed to recover after throw");Wait(.5f);break;}
                        Check(guest.Agent.enabled&&guest.Agent.isOnNavMesh,"Guest gets up and returns to NavMesh after landing");
                        Check(guest.Satisfaction<1,"Pranks reduce guest satisfaction");
                        Check(!string.IsNullOrEmpty(physics.Complaint),"Guest complains about being thrown");
                        beforeHits=physics.ImpactCount;
                        var prop=GameObject.CreatePrimitive(PrimitiveType.Cube);prop.transform.localScale=Vector3.one*.22f;
                        prop.transform.position=guest.transform.position+Vector3.up*1.1f+Vector3.back*1;
                        var projectileBody=prop.AddComponent<Rigidbody>();projectileBody.mass=4;projectileBody.useGravity=false;projectileBody.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;projectileBody.linearVelocity=Vector3.forward*8;
                        prop.AddComponent<HotelThrownProp>().Arm(player);stage++;Wait(.6f);break;
                    case 4:
                        Check(physics.ImpactCount>beforeHits,"A real thrown prop collision reaches the guest reaction");
                        Check(physics.Suspended,"Heavy prop impact knocks the guest into ragdoll");
                        physics.enabled=false;
                        Check(!physics.Suspended&&guest.Agent.enabled,"Disabling physics restores navigation and ownership");
                        Check(guest.GetComponent<CapsuleCollider>().enabled,"Recovery restores the guest collision capsule");
                        Check(!physics.Grab(player),"Disabled physics rejects new grabs");
                        physics.enabled=true;var controller=player.GetComponent<CharacterController>();controller.enabled=false;
                        player.transform.position=guest.transform.position+Vector3.back*1.8f;controller.enabled=true;
                        player.Face(0);player.view.transform.LookAt(physics.HeadPoint);Physics.SyncTransforms();
                        Check(player.GrabFocusedGuest(),"Recovered guest can be grabbed again");
                        Check(physics.GetComponentsInChildren<Rigidbody>().Length==11,"Repeated grabs reuse the eleven-body rig");
                        typeof(HotelPlayer).GetMethod("ReleaseGuest",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,new object[]{false});
                        Check(!physics.IsHeld&&player.CurrentGesture==HotelPlayerGesture.Place,"Gentle release starts the placement animation");
                        deadline=EditorApplication.timeSinceStartup+18;stage++;Wait(.5f);break;
                    case 5:
                        if(physics.Suspended){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Guest failed to recover after repeated grab");Wait(.5f);break;}
                        Check(guest.Agent.enabled&&guest.Agent.isOnNavMesh,"Repeated gentle release also restores navigation");
                        stage=6;Wait(.1f);break;
                    case 6:
                        var point=new Vector3(-8+repeat*4,0,0);guest.Agent.Warp(point);
                        var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=point+Vector3.back*1.8f;cc.enabled=true;
                        player.Face(0);player.view.transform.LookAt(physics.HeadPoint);Physics.SyncTransforms();
                        Check(player.GrabFocusedGuest(),"Grab reused ragdoll after moving navigation root / cycle "+repeat);
                        stage=7;Wait(.6f);break;
                    case 7:
                        Check(Vector3.Distance(guest.visual.head.position,guest.visual.hips.position)<1.25f,"Held skeleton retains body proportions / cycle "+repeat);
                        player.Face(repeat%2==0?65:-65);
                        stage=8;Wait(.4f);break;
                    case 8:
                        typeof(HotelPlayer).GetMethod("ReleaseGuest",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,new object[]{repeat%2==0});
                        deadline=EditorApplication.timeSinceStartup+20;stage=9;Wait(.2f);break;
                    case 9:
                        if(physics.Suspended){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Repeated landing did not recover");Wait(.2f);break;}
                        Check(guest.Agent.enabled&&guest.Agent.isOnNavMesh&&Mathf.Abs(guest.transform.position.y)<.15f,"Repeated release stands on current ground / cycle "+repeat);
                        Check(Vector3.Distance(guest.visual.head.position,guest.visual.hips.position)<1.25f,"Recovery preserves skeleton proportions / cycle "+repeat);
                        Check(Mathf.Abs(guest.visual.hips.localPosition.y-.76f)<.16f,"Recovery restores standing hip height / cycle "+repeat);
                        repeat++;if(repeat<5){stage=6;Wait(.1f);}else{Check(maximumAnchorError<.25f,"Five moved-root grab/turn/throw cycles keep anchors together; max "+maximumAnchorError);stage=10;Wait(.1f);}break;
                    case 10:
                        physics.Impact(Vector3.forward*4,4,guest.visual.hips.position,player);
                        foreach(var rb in physics.GetComponentsInChildren<Rigidbody>()){rb.position+=Vector3.up*4;rb.useGravity=false;rb.linearVelocity=Vector3.zero;}
                        typeof(HotelGuestPhysics).GetField("age",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(physics,20f);
                        stage=11;Wait(.6f);break;
                    case 11:
                        Check(physics.Suspended&&!guest.Agent.enabled&&physics.HeadPoint.y>2,"Expired recovery timer cannot teleport an airborne guest to navigation");
                        foreach(var rb in physics.GetComponentsInChildren<Rigidbody>())rb.useGravity=true;
                        deadline=EditorApplication.timeSinceStartup+20;stage=12;Wait(.2f);break;
                    case 12:
                        if(physics.Suspended){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Airborne guest did not recover after ground contact");Wait(.2f);break;}
                        Check(Mathf.Abs(guest.transform.position.y)<.15f,"Airborne guest gets up only after landing on the actual floor");
                        var platform=GameObject.CreatePrimitive(PrimitiveType.Cube);platform.transform.position=new Vector3(10,4.35f,10);platform.transform.localScale=new Vector3(7,.3f,7);
                        var upperNav=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,transform=Matrix4x4.TRS(platform.transform.position,Quaternion.identity,Vector3.one),size=platform.transform.localScale,area=0}},new Bounds(new Vector3(10,4.5f,10),new Vector3(8,4,8)),Vector3.zero,Quaternion.identity);NavMesh.AddNavMeshData(upperNav);
                        guest.Agent.Warp(new Vector3(10,4.5f,10));var upperController=player.GetComponent<CharacterController>();upperController.enabled=false;player.transform.position=new Vector3(10,4.5f,8.2f);upperController.enabled=true;
                        player.Face(0);player.view.transform.LookAt(physics.HeadPoint);Physics.SyncTransforms();Check(player.GrabFocusedGuest(),"Upper-floor guest can be grabbed with reused ragdoll");
                        stage=13;Wait(.5f);break;
                    case 13:
                        typeof(HotelPlayer).GetMethod("ReleaseGuest",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,new object[]{false});deadline=EditorApplication.timeSinceStartup+20;stage=14;Wait(.2f);break;
                    case 14:
                        if(physics.Suspended){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Upper-floor release did not recover");Wait(.2f);break;}
                        Check(guest.Agent.isOnNavMesh&&Mathf.Abs(guest.transform.position.y-4.5f)<.15f,"Upper-floor recovery remains on its physical floor without snapping downstairs");
                        Check(Vector3.Distance(guest.visual.head.position,guest.visual.hips.position)<1.25f,"Upper-floor get-up keeps normal skeleton proportions");Finish();break;
                }
            }
            catch(Exception error){failed=true;results.Add("FAIL: "+error);Finish();}
        }
        static void RemoveUpdate(ref PlayerLoopSystem loop)
        {
            if(loop.subSystemList==null)return;
            var children=new List<PlayerLoopSystem>();
            foreach(var child in loop.subSystemList){if(child.type==typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate))continue;var item=child;RemoveUpdate(ref item);children.Add(item);}
            loop.subSystemList=children.ToArray();
        }
        static void Finish(){if(previousLoop.subSystemList!=null)PlayerLoop.SetPlayerLoop(previousLoop);Time.timeScale=1;EditorApplication.update-=Tick;EditorApplication.isPlaying=false;}
    }
}
