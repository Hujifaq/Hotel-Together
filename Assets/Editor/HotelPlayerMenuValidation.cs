using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.LowLevel;

namespace HotelTogether.Editor
{
    /// <summary>Runs isolated Play Mode checks in a disposable scene, without saving hotel assets.</summary>
    public static class HotelPlayerMenuValidation
    {
        static readonly List<string> results = new List<string>();
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static HotelPlayer player;
        static Rigidbody body;
        static int stage;
        static double next;
        static float frozenTime, frozenY;
        static Scene testScene, originalScene;
        static bool failed;
        static bool previousEnterOptionsEnabled;
        static EnterPlayModeOptions previousEnterOptions;
        static PlayerLoopSystem previousLoop;
        static float frozenGesture;
        static Vector3 frozenHand;
        static Transform hands;
        static Rigidbody thrownBody;

        [MenuItem("Hotel Together/Validation/Player menu and pause")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running menu validation.");
            results.Clear(); failed = false; stage = 0;
            originalScene = SceneManager.GetActiveScene();
            testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            testScene.name = "Hotel Menu Validation";
            SceneManager.SetActiveScene(testScene);
            previousEnterOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Existing hotel objects are excluded in the isolated batch project. In the full
                // Editor, deactivate other scenes only for this Play Mode session.
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.name == testScene.name) continue;
                    foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
                }
                var go = new GameObject("Menu validation player"); go.SetActive(false);
                player = go.AddComponent<HotelPlayer>();
                var camera = new GameObject("Validation camera", typeof(Camera));
                camera.transform.SetParent(go.transform); camera.transform.localPosition = Vector3.up * 1.6f;
                player.view = camera.GetComponent<Camera>();
                go.SetActive(true);
                player.SendMessage("OnApplicationFocus", true);
                var falling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                falling.transform.position = new Vector3(10, 10, 10);
                body = falling.AddComponent<Rigidbody>();
                next = EditorApplication.timeSinceStartup + .5;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayMode;
                EditorSettings.enterPlayModeOptions = previousEnterOptions;
                EditorSettings.enterPlayModeOptionsEnabled = previousEnterOptionsEnabled;
                if (!Application.isBatchMode && testScene.IsValid()) EditorSceneManager.CloseScene(testScene, true);
                if (originalScene.IsValid()) SceneManager.SetActiveScene(originalScene);
                string report = string.Join("\n", results) + "\n";
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/HotelPlayerMenuValidation.txt", report);
                Debug.Log(report);
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            }
        }

        static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            results.Add("PASS: " + label);
        }
        static FieldInfo Field(string name) => typeof(HotelPlayer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                switch (stage++)
                {
                    case 0:
                        // Headless Editors cannot retain cursor capture. Drive the public pause API
                        // in isolation, leaving engine time and FixedUpdate/physics running normally.
                        previousLoop = PlayerLoop.GetCurrentPlayerLoop();
                        var apiLoop = previousLoop;
                        RemoveGameplayUpdate(ref apiLoop);
                        PlayerLoop.SetPlayerLoop(apiLoop);
                        Check(Time.timeScale == 0 && AudioListener.pause, "Startup menu pauses simulation and audio");
                        Check(!player.GameplayInputEnabled, "Menu rejects gameplay input");
                        frozenTime = Time.time; frozenY = body.position.y;
                        next = EditorApplication.timeSinceStartup + .6; break;
                    case 1:
                        Check(Mathf.Approximately(Time.time, frozenTime), "Game clock stays frozen during menu");
                        Check(Mathf.Approximately(body.position.y, frozenY), "Falling rigidbody stays frozen during menu");
                        player.ResumeGameplay();
                        Check(Time.timeScale == 1 && !AudioListener.pause, "Resume restores simulation and audio");
                        next = EditorApplication.timeSinceStartup + .6; break;
                    case 2:
                        Check(Time.time > frozenTime && body.position.y < frozenY, "Clock and physics advance after resume");
                        player.PauseGameplay();Field("resumeTimeScale").SetValue(player,0f);AudioListener.pause=true;player.ResumeGameplay();
                        Check(Time.timeScale==1&&!AudioListener.pause,"Play repairs a stale zero-speed menu checkpoint");
                        player.enabled=false;Time.timeScale=0;AudioListener.pause=true;player.enabled=true;
                        Check((float)Field("resumeTimeScale").GetValue(player)==1,"Re-enabled player does not inherit a frozen resume speed");
                        player.ResumeGameplay();Check(Time.timeScale==1&&!AudioListener.pause,"Play releases inherited global time and audio pause");
                        player.PauseGameplay();Field("resumeTimeScale").SetValue(player,float.NaN);player.ResumeGameplay();
                        Check(Time.timeScale==1,"Play rejects an invalid resume speed");
                        Time.timeScale = .5f; player.PauseGameplay(); player.PauseGameplay(); player.ResumeGameplay();
                        Check(Time.timeScale == .5f, "Repeated pause preserves prior simulation speed");
                        Time.timeScale = 1;
                        player.SendMessage("OnApplicationFocus", false);
                        Check(Time.timeScale == 0 && !player.GameplayInputEnabled, "Focus loss pauses and rejects input");
                        player.SendMessage("OnApplicationFocus", true);
                        Check(Time.timeScale == 0, "Focus return waits for explicit resume");
                        player.ResumeGameplay();
                        Field("vertical").SetValue(player, -35f);
                        player.view.transform.localRotation = Quaternion.Euler(40, 0, 5);
                        player.bellCount = 4; player.ResetWorld();
                        Check((float)Field("vertical").GetValue(player) == 0, "Restart clears falling velocity");
                        Check(Quaternion.Angle(player.view.transform.localRotation, Quaternion.identity) < .01f && player.bellCount == 0,
                            "Restart resets camera and bell count");
                        player.Notify("Validation notification"); player.PauseGameplay();
                        frozenTime = (float)Field("messageUntil").GetValue(player);
                        next = EditorApplication.timeSinceStartup + .6; break;
                    case 3:
                        player.ResumeGameplay();
                        Check((float)Field("messageUntil").GetValue(player) > frozenTime + .4f, "Notifications keep their remaining time after pause");
                        player.PlayStrikeGesture();
                        next = EditorApplication.timeSinceStartup + .12; break;
                    case 4:
                        hands = (Transform)Field("hands").GetValue(player);
                        Check(player.CurrentGesture == HotelPlayerGesture.Strike && hands && hands.gameObject.activeSelf,
                            "Unarmed strike animates seamless first-person hands in LateUpdate");
                        foreach(var mesh in (List<Mesh>)Field("handMeshes").GetValue(player))
                            foreach(var point in mesh.vertices)
                                if(!float.IsFinite(point.x)||!float.IsFinite(point.y)||!float.IsFinite(point.z))throw new InvalidOperationException("First-person hand skinning is non-finite");
                        Check(true,"Animated first-person hand vertices remain finite");
                        player.PauseGameplay(); frozenGesture = player.GestureProgress; frozenHand = hands.GetChild(0).position;
                        next = EditorApplication.timeSinceStartup + .3; break;
                    case 5:
                        Check(player.GestureProgress == frozenGesture && hands.GetChild(0).position == frozenHand,
                            "Pause freezes first-person gesture and hand transforms");
                        player.ResumeGameplay(); next = EditorApplication.timeSinceStartup + 1; break;
                    case 6:
                        Check(player.CurrentGesture == HotelPlayerGesture.None && !hands.gameObject.activeSelf,
                            "Strike finishes and hands blend out after resume");
                        var prop=GameObject.CreatePrimitive(PrimitiveType.Cube);prop.transform.localScale=Vector3.one*.2f;
                        prop.transform.position=player.view.transform.position+player.view.transform.forward*.6f;
                        var item=prop.AddComponent<HotelItem>();item.kind="pickup";item.dimensions=Vector3.one*.2f;var rigid=prop.AddComponent<Rigidbody>();thrownBody=rigid;
                        typeof(HotelPlayer).GetMethod("PickUp",Private).Invoke(player,new object[]{item});player.Drop(true);
                        Check(player.held==null && rigid.useGravity && prop.layer==0,
                            "Throw gesture preserves release layers and gravity");
                        // AddForce is integrated by the next physics step, not at the call site.
                        next = EditorApplication.timeSinceStartup + .12; break;
                    case 7:
                        Check(Vector3.Dot(thrownBody.linearVelocity,player.view.transform.forward)>3,"Throw preserves forward physics impulse");
                        Check(player.CurrentGesture==HotelPlayerGesture.Throw && hands.gameObject.activeSelf,"Throw release keeps its follow-through animation");
                        player.PauseGameplay(); player.enabled = false;
                        Check(Time.timeScale == 1 && !AudioListener.pause, "Disabling player releases global pause state");
                        Finish(); break;
                }
            }
            catch (Exception error) { failed = true; results.Add("FAIL: " + error); Finish(); }
        }
        static void Finish()
        {
            if (previousLoop.subSystemList != null) PlayerLoop.SetPlayerLoop(previousLoop);
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
        }
        static void RemoveGameplayUpdate(ref PlayerLoopSystem loop)
        {
            if (loop.subSystemList == null) return;
            var children = new List<PlayerLoopSystem>();
            foreach (var child in loop.subSystemList)
            {
                if (child.type == typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate)) continue;
                var item = child; RemoveGameplayUpdate(ref item); children.Add(item);
            }
            loop.subSystemList = children.ToArray();
        }
    }
}
