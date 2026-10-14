using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HotelTogether.Editor
{
    // Run in a disposable batch project with the same Scripts, employee prefabs/art and dependencies.
    public static class HotelAnimationValidation
    {
        static readonly List<string> results = new List<string>();
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly MethodInfo advance = typeof(HotelPlayer).GetMethod("AdvanceActionAnimation", Private);
        static readonly MethodInfo reset = typeof(HotelPlayer).GetMethod("ResetActionAnimation", Private);
        static string output;
        static Camera camera;
        static HotelPlayer player;
        static HotelEmployeeRig rig;
        static Mesh baked;

        static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); results.Add("PASS: " + label); }
        static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        static void Step(float dt)
        { advance.Invoke(player, new object[] { dt }); rig.Pose(player, dt); }
        static void CheckMesh(string label)
        {
            rig.bodyRenderer.BakeMesh(baked);
            foreach (var vertex in baked.vertices) if (!Finite(vertex)) throw new InvalidOperationException(label + " produced a non-finite vertex");
            Check(baked.bounds.size.magnitude < 4 && baked.vertexCount > 1000, label + " has finite, bounded skinning");
        }
        static void SetField(object owner, string name, object value)
        { owner.GetType().GetField(name, Private).SetValue(owner, value); }

        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project for this validation.");
            output = Environment.GetEnvironmentVariable("HOTEL_ANIMATION_QA_OUTPUT") ?? Path.GetFullPath("Logs/AnimationQA");
            Directory.CreateDirectory(output);
            int exit = 0;
            try
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.localScale = Vector3.one * 10;
                var light = new GameObject("Preview light").AddComponent<Light>(); light.type = LightType.Directional;
                light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(45,-35,0);
                RenderSettings.ambientLight = new Color(.65f,.68f,.72f);
                camera = new GameObject("Preview camera").AddComponent<Camera>(); camera.orthographic = true;
                camera.orthographicSize = 1.1f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.82f,.87f,.86f);
                baked = new Mesh();
                foreach (string employee in new[] { "Bellhop", "Housekeeping" }) ValidateEmployee(employee);
                ValidateGuestPause();
                UnityEngine.Object.DestroyImmediate(baked);
            }
            catch (Exception error) { exit = 1; results.Add("FAIL: " + error); }
            File.WriteAllLines(Path.Combine(output, "AnimationValidation.txt"), results);
            Debug.Log(string.Join("\n", results));
            EditorApplication.Exit(exit);
        }

        static void ValidateEmployee(string employee)
        {
            var go = new GameObject("Validation player"); go.SetActive(false);
            player = go.AddComponent<HotelPlayer>();
            var view = new GameObject("View").AddComponent<Camera>(); view.enabled = false;
            view.transform.SetParent(go.transform); view.transform.localPosition = Vector3.up * 1.6f; player.view = view;
            SetField(player,"cc",go.GetComponent<CharacterController>());
            var prefab = AssetDatabase.LoadAssetAtPath<HotelEmployeeRig>("Assets/HotelTogether/Prefabs/Employees/" + employee + ".prefab");
            Check(prefab != null, employee + " prefab resolves its rig script");
            rig = UnityEngine.Object.Instantiate(prefab, go.transform); rig.Initialize(false);
            // Preview uses built-in materials so shader import doesn't affect bone validation.
            foreach(var renderer in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    Color color = Color.gray;
                    var colors = new SerializedObject(materials[i]).FindProperty("m_SavedProperties.m_Colors");
                    if(colors!=null)for(int c=0;c<colors.arraySize;c++)
                    {
                        var entry=colors.GetArrayElementAtIndex(c);
                        if(entry.FindPropertyRelative("first").stringValue=="_BaseColor")color=entry.FindPropertyRelative("second").colorValue;
                    }
                    materials[i]=new Material(Shader.Find("Standard")){color=color};
                }
                renderer.sharedMaterials=materials;
            }
            // Inactive test objects let us sample presentation without Awake/Start changing game globals.
            rig.gameObject.SetActive(true);
            for(int i=0;i<30;i++)Step(1f/60);
            CheckMesh(employee + " idle"); Capture(employee + "-Idle");
            foreach(var action in (HotelPlayerGesture[])Enum.GetValues(typeof(HotelPlayerGesture)))
            {
                if(action==HotelPlayerGesture.None)continue;
                player.PlayGesture(action,view.transform.position+Vector3.forward*.6f);
                float duration=(float)typeof(HotelPlayer).GetField("gestureDuration",Private).GetValue(player);
                for(int i=0;i<Mathf.RoundToInt(duration*30);i++)Step(1f/60);
                bool hand=player.TryAnimationHandPose(1,out var target,out var rotation,out float curl);
                Check(hand && Finite(target) && float.IsFinite(rotation.x) && curl>=0 && curl<=1,employee+" "+action+" produces a valid hand pose");
                CheckMesh(employee+" "+action); Capture(employee+"-"+action);
                Step(duration+1); Check(player.CurrentGesture==HotelPlayerGesture.None,employee+" "+action+" returns to rest");
            }
            player.PlayGesture(HotelPlayerGesture.Strike,view.transform.position+Vector3.forward);
            Step(.2f); player.PlayGesture(HotelPlayerGesture.Hurt,view.transform.position);
            Check(player.CurrentGesture==HotelPlayerGesture.Hurt,employee+" hurt interrupts strike");
            float before=player.GestureProgress; var wrist=rig.rightArm[2].position;
            Step(0); Check(player.GestureProgress==before && rig.rightArm[2].position==wrist,employee+" zero-time pose freezes gesture and bones");
            reset.Invoke(player,null); rig.ResetPose(); Check(player.CurrentGesture==HotelPlayerGesture.None,employee+" reset clears transient animation");
            foreach(float speed in new[]{2.7f,4.4f})
            {
                go.transform.position=Vector3.zero; rig.ResetPose();
                for(int i=0;i<30;i++){go.transform.position+=Vector3.forward*(speed/60);Step(1f/60);}
                CheckMesh(employee+(speed>3?" run":" walk")); Capture(employee+(speed>3?"-Run":"-Walk"));
            }
            go.transform.position=Vector3.zero; rig.ResetPose();
            var seat=new GameObject("Seat").AddComponent<HotelItem>(); player.seated=seat;
            for(int i=0;i<30;i++)Step(1f/60);
            Check(player.CurrentMotion==HotelPlayerMotion.Seated,employee+" seated state is driven by gameplay"); CheckMesh(employee+" seated"); Capture(employee+"-Seated");
            player.seated=null; UnityEngine.Object.DestroyImmediate(seat.gameObject);
            rig.ResetPose();
            ValidateContinuous(employee);
            // Render actual sampled bone motion for a short visual review, rather than a concept image.
            string frames=Path.Combine(output,employee+"-StrikeFrames");Directory.CreateDirectory(frames);
            player.PlayGesture(HotelPlayerGesture.Strike,view.transform.position+Vector3.forward);
            for(int i=0;i<36;i++){Step(1f/30);Capture(employee+"-StrikeFrames/frame-"+i.ToString("000"));}
            UnityEngine.Object.DestroyImmediate(go);
        }

        static void Capture(string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            // Only the avatar renderer is shown in this isolated preview; the inactive parent is
            // kept inactive so HotelPlayer lifecycle never changes pause/audio preferences.
            var renderers=rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var proxies=new List<GameObject>();
            foreach(var skin in renderers)
            {
                var mesh=new Mesh();skin.BakeMesh(mesh);
                var proxy=new GameObject("Baked preview",typeof(MeshFilter),typeof(MeshRenderer));
                proxy.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);
                proxy.transform.localScale=skin.transform.lossyScale;
                proxy.GetComponent<MeshFilter>().sharedMesh=mesh;proxy.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;proxies.Add(proxy);
            }
            camera.transform.position=player.transform.position+new Vector3(2.2f,1.6f,3.2f);
            camera.transform.LookAt(player.transform.position+Vector3.up*.85f);
            var texture=new RenderTexture(320,380,24); var pixels=new Texture2D(320,380,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,320,380),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null;RenderTexture.active=previous;
                UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(pixels);
                foreach(var proxy in proxies){UnityEngine.Object.DestroyImmediate(proxy.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(proxy);}
            }
        }

        static void ValidateGuestPause()
        {
            var go=new GameObject("Guest pose validation");go.SetActive(false);var guest=go.AddComponent<HotelGuestVisual>();
            Transform Bone(string name){var bone=new GameObject(name).transform;bone.SetParent(go.transform);return bone;}
            guest.hips=Bone("Hips");guest.head=Bone("Head");guest.leftArm=Bone("Left arm");guest.rightArm=Bone("Right arm");guest.leftForearm=Bone("Left forearm");guest.rightForearm=Bone("Right forearm");
            guest.leftLeg=Bone("Left leg");guest.rightLeg=Bone("Right leg");guest.leftShin=Bone("Left shin");guest.rightShin=Bone("Right shin");guest.leftEye=Bone("Left eye");guest.rightEye=Bone("Right eye");guest.leftBrow=Bone("Left brow");guest.rightBrow=Bone("Right brow");guest.smile=Bone("Smile");
            guest.Pose(1.7f,.1f,false,false,false,1,false);var thigh=guest.leftLeg.localRotation;
            guest.Pose(0,0,true,true,true,.2f,false);
            Check(guest.leftLeg.localRotation==thigh,"Guest pause preserves bone pose");
            guest.Pose(0,.1f,true,false,true,1,false);
            Check(Quaternion.Angle(thigh,guest.leftLeg.localRotation)>1,"Guest seating blends after resume");
            UnityEngine.Object.DestroyImmediate(go);
        }

        static void ValidateContinuous(string employee)
        {
            var prop=new GameObject("Validation prop");prop.SetActive(false);
            var item=prop.AddComponent<HotelItem>();item.dimensions=new Vector3(.4f,.45f,.3f);prop.transform.position=new Vector3(0,1.15f,.6f);
            void Sample(HotelPlayerMotion expected,string label,int frames=18)
            {for(int i=0;i<frames;i++)Step(1f/60);Check(player.CurrentMotion==expected,employee+" "+label+" follows gameplay state");CheckMesh(employee+" "+label);Capture(employee+"-"+label);}
            player.held=item;Sample(HotelPlayerMotion.Carry,"Carry");player.held=null;
            item.dimensions=new Vector3(.8f,1.08f,1.3f);prop.transform.position=new Vector3(0,.54f,1);prop.transform.rotation=Quaternion.Euler(0,-90,0);
            player.cart=item;Sample(HotelPlayerMotion.PushCart,"PushCart");player.cart=null;
            item.dimensions=new Vector3(.4f,.45f,.3f);prop.transform.SetPositionAndRotation(new Vector3(0,1.15f,.6f),Quaternion.identity);
            SetField(player,"pendingPickup",item);rig.BeginReach(item.transform,item.transform.position);
            Sample(HotelPlayerMotion.Reach,"Reach");SetField(player,"pendingPickup",null);rig.EndReach();
            var wetGO=new GameObject("Wet spot");wetGO.SetActive(false);var wet=wetGO.AddComponent<HotelFloorMess>();
            SetField(wet,"<Mopping>k__BackingField",true);
            var systemGO=new GameObject("Cleaning");systemGO.SetActive(false);var system=systemGO.AddComponent<HotelHousekeepingSystem>();system.authored=new[]{wet};
            prop.AddComponent<HotelCleaningTool>();player.held=item;SetField(player,"housekeeping",system);
            Sample(HotelPlayerMotion.Mop,"Mop");player.held=null;SetField(player,"housekeeping",null);
            SetField(player,"blockWeight",1f);Sample(HotelPlayerMotion.Block,"Block",1);SetField(player,"blockWeight",0f);
            SetField(player,"vertical",-6f);Sample(HotelPlayerMotion.Falling,"Falling");SetField(player,"vertical",0f);
            var human=player.gameObject.AddComponent<HotelHuman>();var combat=player.gameObject.AddComponent<HotelCombat>();
            SetField(combat,"<Human>k__BackingField",human);player.combat=combat;SetField(human,"<Health>k__BackingField",0f);
            Sample(HotelPlayerMotion.Downed,"Downed");SetField(human,"<Health>k__BackingField",100f);player.combat=null;
            UnityEngine.Object.DestroyImmediate(prop);UnityEngine.Object.DestroyImmediate(wetGO);UnityEngine.Object.DestroyImmediate(systemGO);
            rig.ResetPose();reset.Invoke(player,null);
        }
    }
}
