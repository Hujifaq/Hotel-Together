using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class HotelGlazingRepair {
 [MenuItem("HotelTogether/Repair dining and lounge window coverage")]
 public static void Apply(){
 if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before saving window geometry.");
 int repaired=ApplyToScene();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());Debug.Log("HOTEL_WINDOW_COVERAGE_REPAIRED panes="+repaired);
 }
 public static int ApplyToScene(){
 int count=0;foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>()){
 var b=r.bounds;var c=b.center;
 // Front windows of both public rooms, plus the three dining/lobby divider panes.
 bool front=Mathf.Abs(c.z-7)<.025f&&Mathf.Abs(c.y-2.25f)<.025f&&Mathf.Abs(c.x)>9&&Mathf.Abs(c.x)<29&&b.size.x>2&&b.size.z<.01f;
 bool divider=Mathf.Abs(c.x+8.98f)<.025f&&Mathf.Abs(c.y-2.25f)<.025f&&c.z>.9f&&c.z<4.6f&&b.size.x<.01f&&b.size.z>.8f;
 if(!front&&!divider)continue;
 bool glass=false;foreach(var m in r.sharedMaterials)if(m&&(m.name=="Soft smoked reflective glass"||(m.HasProperty("baseColorFactor")&&Mathf.Abs(m.GetColor("baseColorFactor").a-.28f)<.001f)))glass=true;
 if(!glass)continue;
 // Front bays include 10 cm of frame overlap; divider mullions sit on the bay boundaries.
 float bayWidth=front?(c.x>0?20f/6-.10f:2.90f):(c.z>4?1f:2f);
 Undo.RecordObject(r.transform,"Fit glass to window frame");var scale=r.transform.localScale;scale.x*=bayWidth/(front?b.size.x:b.size.z);scale.y*=4.48f/b.size.y;r.transform.localScale=scale;EditorUtility.SetDirty(r.transform);count++;
 }
 return count;
 }
}
