using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace HotelTogether.Editor
{
    public static class HotelMaintenanceVisualValidation
    {
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var owner=new GameObject("Maintenance model preview").AddComponent<HotelMaintenanceSystem>();owner.Initialize(null,false);
            var root=new GameObject("Preview tool set").transform;
            HotelMaintenanceModels.Tool(owner,MaintenanceToolKind.Extinguisher,new Vector3(-.6f,.31f,0),root);
            var wrench=HotelMaintenanceModels.Tool(owner,MaintenanceToolKind.Wrench,new Vector3(.06f,.21f,0),root);wrench.transform.rotation=Quaternion.Euler(70,0,-10);
            var driver=HotelMaintenanceModels.Tool(owner,MaintenanceToolKind.Screwdriver,new Vector3(.56f,.22f,0),root);driver.transform.rotation=Quaternion.Euler(70,0,10);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.position=Vector3.down*.055f;ground.transform.localScale=new Vector3(4,.1f,3);ground.GetComponent<Renderer>().sharedMaterial=owner.CreateMaterial("Preview warm stone",new Color(.82f,.80f,.74f));
            var light=new GameObject("Preview light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(45,-30,0);light.shadows=LightShadows.Soft;
            RenderSettings.ambientLight=new Color(.48f,.48f,.48f);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.transform.position=new Vector3(1.4f,1.05f,-2.6f);camera.transform.LookAt(new Vector3(0,.27f,0));camera.fieldOfView=32;camera.backgroundColor=new Color(.82f,.8f,.75f);camera.clearFlags=CameraClearFlags.SolidColor;
            var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/MaintenanceTools.png",image.EncodeToPNG());RenderTexture.active=null;
            Debug.Log("Rendered native maintenance tool models to Logs/MaintenanceTools.png");if(Application.isBatchMode)EditorApplication.Exit(0);
        }
    }
}
