using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
public static class PackageInstaller {
 static AddAndRemoveRequest request;static double deadline;
 public static void Install(){request=Client.AddAndRemove(new[]{"com.unity.cloud.gltfast"});deadline=EditorApplication.timeSinceStartup+600;EditorApplication.update+=Poll;}
 static void Poll(){if(!request.IsCompleted){if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("UPM timed out");EditorApplication.Exit(2);}return;}EditorApplication.update-=Poll;if(request.Status==StatusCode.Success){Debug.Log("GLTF_IMPORT_READY");EditorApplication.Exit(0);}else{Debug.LogError(request.Error.message);EditorApplication.Exit(1);}}
}
