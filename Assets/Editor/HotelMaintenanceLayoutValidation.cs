using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HotelTogether.Editor
{
    public static class HotelMaintenanceLayoutValidation
    {
        public static void Run()
        {
            var results=new List<string>();bool failed=false;
            try
            {
                // QA copy includes saved transforms, BoxColliders, prefab references and baked navigation.
                // Missing imported render meshes are outside this layout check's scope.
                EditorSceneManager.OpenScene("Assets/QA/MaintenanceLayout.unity",OpenSceneMode.Single);
                var data=AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/HotelTogether/Art/Housekeeping/Housekeeping navigation.asset");
                if(!data)throw new Exception("Saved hotel navigation missing");NavMesh.AddNavMeshData(data);Physics.SyncTransforms();
                var hotel=UnityEngine.Object.FindAnyObjectByType<HotelGuestSimulation>();if(!hotel)throw new Exception("Hotel controller missing");
                var system=hotel.GetComponent<HotelMaintenanceSystem>();if(!system)system=hotel.gameObject.AddComponent<HotelMaintenanceSystem>();system.Initialize(hotel);
                if(!system.rack)throw new Exception("No reachable rack placement");results.Add("PASS: Tools/refill rack placed on hotel navigation at "+system.rack.transform.position);
                foreach(var kind in (MaintenanceToolKind[])Enum.GetValues(typeof(MaintenanceToolKind)))
                {if(!system.tools.Exists(t=>t.kind==kind))throw new Exception("Missing tool "+kind);results.Add("PASS: Available tool "+kind);}
                foreach(var kind in (MaintenanceIncidentKind[])Enum.GetValues(typeof(MaintenanceIncidentKind)))
                {if(!system.incidents.Exists(i=>i.kind==kind))throw new Exception("No reachable site for "+kind);results.Add("PASS: Reachable site exists for "+kind);}
                var type=hotel.guestPrefabs[0].GetComponent<NavMeshAgent>().agentTypeID;var filter=new NavMeshQueryFilter{agentTypeID=type,areaMask=NavMesh.AllAreas};
                foreach(var incident in system.incidents)
                {
                    if(!NavMesh.SamplePosition(incident.transform.position,out var hit,.25f,filter))throw new Exception("Site outside hotel navigation: "+incident.location);
                    results.Add("PASS: "+incident.Title+" / "+incident.location+" / "+incident.transform.position);
                }
                results.Add("INFO: "+system.tools.Count+" tools / "+system.incidents.Count+" sites / no scene save performed");
            }
            catch(Exception error){failed=true;results.Add("FAIL: "+error);}
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/HotelMaintenanceLayoutValidation.txt",string.Join("\n",results));Debug.Log(string.Join("\n",results));
            if(Application.isBatchMode)EditorApplication.Exit(failed?1:0);
        }
    }
}
