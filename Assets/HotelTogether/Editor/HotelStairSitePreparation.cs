using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

// Subtract only the stair volumes from imported, sometimes combined, meshes.
// Adjacent rooms and the rest of each combined mesh are retained.
public static class HotelStairSitePreparation
{
    struct Vertex { public Vector3 p,n; public Vector2 uv; public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t),uv=Vector2.Lerp(a.uv,b.uv,t)}; }
    static List<Vertex> Clip(List<Vertex> polygon,int axis,float edge,bool greater)
    {
        var result=new List<Vertex>();if(polygon.Count==0)return result;
        var a=polygon[polygon.Count-1];bool ia=greater?a.p[axis]>=edge:a.p[axis]<=edge;
        foreach(var b in polygon){bool ib=greater?b.p[axis]>=edge:b.p[axis]<=edge;if(ia!=ib)result.Add(Vertex.Lerp(a,b,(edge-a.p[axis])/(b.p[axis]-a.p[axis])));if(ib)result.Add(b);a=b;ia=ib;}
        return result;
    }
    static IEnumerable<List<Vertex>> Outside(List<Vertex> polygon,Bounds cut)
    {
        var inside=polygon;
        for(int axis=0;axis<3;axis++){
            var piece=Clip(inside,axis,cut.min[axis],false);if(piece.Count>=3)yield return piece;
            inside=Clip(inside,axis,cut.min[axis],true);
            piece=Clip(inside,axis,cut.max[axis],true);if(piece.Count>=3)yield return piece;
            inside=Clip(inside,axis,cut.max[axis],false);
            if(inside.Count<3)yield break;
        }
    }
    static void CutMesh(MeshFilter filter,Bounds cut,string assetPrefix="Assets/HotelTogether/Art/Stairways/Site ")
    {
        var mesh=filter.sharedMesh;if(!mesh)return;var renderer=filter.GetComponent<Renderer>();if(!renderer||!renderer.bounds.Intersects(cut))return;
        var source=mesh.vertices;var normal=mesh.normals;var uv=mesh.uv;var positions=new List<Vector3>();var normals=new List<Vector3>();var tex=new List<Vector2>();var submeshes=new List<int[]>();bool changed=false;
        for(int sub=0;sub<mesh.subMeshCount;sub++){
            var indices=new List<int>();var triangles=mesh.GetTriangles(sub);
            for(int i=0;i<triangles.Length;i+=3){var polygon=new List<Vertex>();for(int j=0;j<3;j++){int v=triangles[i+j];polygon.Add(new Vertex{p=filter.transform.TransformPoint(source[v]),n=normal.Length==source.Length?normal[v]:Vector3.up,uv=uv.Length==source.Length?uv[v]:Vector2.zero});}
                var bounds=new Bounds(polygon[0].p,Vector3.zero);foreach(var v in polygon)bounds.Encapsulate(v.p);
                var pieces=bounds.Intersects(cut)?Outside(polygon,cut).ToList():new List<List<Vertex>>{polygon};
                if(bounds.Intersects(cut))changed=true;
                foreach(var piece in pieces){int start=positions.Count;foreach(var v in piece){positions.Add(filter.transform.InverseTransformPoint(v.p));normals.Add(v.n);tex.Add(v.uv);}for(int j=1;j<piece.Count-1;j++){indices.Add(start);indices.Add(start+j);indices.Add(start+j+1);}}
            }submeshes.Add(indices.ToArray());
        }
        if(!changed)return;
        var path=assetPrefix+filter.gameObject.GetEntityId().ToString().Replace(":","-")+".asset";
        var output=new Mesh{name="Stair site retained mesh",indexFormat=IndexFormat.UInt32};output.SetVertices(positions);output.SetNormals(normals);output.SetUVs(0,tex);output.subMeshCount=submeshes.Count;for(int i=0;i<submeshes.Count;i++)output.SetTriangles(submeshes[i],i);output.RecalculateBounds();var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(output,existing);Object.DestroyImmediate(output);output=existing;EditorUtility.SetDirty(existing);}else AssetDatabase.CreateAsset(output,path);Undo.RecordObject(filter,"Cut stair site geometry");filter.sharedMesh=output;EditorUtility.SetDirty(filter);
        var collider=filter.GetComponent<MeshCollider>();if(collider){Undo.RecordObject(collider,"Cut stair site collider");collider.sharedMesh=null;collider.sharedMesh=output;}
    }
    static IEnumerable<Bounds> Outside(Bounds source,Bounds cut)
    {
        var min=source.min;var max=source.max;
        for(int axis=0;axis<3;axis++){
            if(min[axis]<cut.min[axis]){var end=max;end[axis]=Mathf.Min(max[axis],cut.min[axis]);yield return new Bounds((min+end)/2,end-min);min[axis]=end[axis];}
            if(max[axis]>cut.max[axis]){var start=min;start[axis]=Mathf.Max(min[axis],cut.max[axis]);yield return new Bounds((start+max)/2,max-start);max[axis]=start[axis];}
        }
    }
    public static void RemoveEntranceFloorOverlap(Transform replacement,float wall,float front)
    {
        var cut=new Bounds(new Vector3(wall+1.825f,0,front),new Vector3(4.15f,.024f,1.4f));
        foreach(var f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)){
            if(f.transform.IsChildOf(replacement)||f.GetComponentInParent<HotelTogether.HotelItem>()||f.GetComponentInParent<HotelTogether.HotelPlayer>())continue;
            CutMesh(f,cut);
        }
    }
    public static void CutElevatorVolume(Transform replacement,Bounds cut)
    {
        var retained=GameObject.Find("Elevator retained structure");if(!retained)retained=new GameObject("Elevator retained structure");
        foreach(var f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)){
            if(f.transform.IsChildOf(replacement)||f.GetComponentInParent<HotelTogether.HotelItem>()||f.GetComponentInParent<HotelTogether.HotelPlayer>())continue;
            CutMesh(f,cut,"Assets/HotelTogether/Art/Elevators/Shaft ");
        }
        foreach(var c in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)){
            if(!c.enabled||c.isTrigger||c.transform.IsChildOf(replacement)||c.GetComponentInParent<HotelTogether.HotelItem>()||!cut.Intersects(c.bounds))continue;
            var overlap=Vector3.Min(c.bounds.max,cut.max)-Vector3.Max(c.bounds.min,cut.min);if(overlap.x<.001f||overlap.y<.001f||overlap.z<.001f)continue;
            var source=c.bounds;c.enabled=false;EditorUtility.SetDirty(c);
            foreach(var b in Outside(source,cut)){if(b.size.x<.001f||b.size.y<.001f||b.size.z<.001f)continue;var g=new GameObject("Retained structure outside elevator shaft");g.transform.SetParent(retained.transform,false);g.transform.position=b.center;g.AddComponent<BoxCollider>().size=b.size;}
        }
    }
    public static void Prepare(Transform replacement,float wall,float z)
    {
        var retained=GameObject.Find("Stair site retained structure");if(!retained){retained=new GameObject("Stair site retained structure");Undo.RegisterCreatedObjectUndo(retained,"Retain external stair site structure");}var cuts=new[]{new Bounds(new Vector3(wall+2,6,z),new Vector3(3.76f,11.96f,5.76f)),new Bounds(new Vector3(wall,6,z),new Vector3(.5f,12.1f,5.98f))};
        foreach(var cut in cuts){
            foreach(var f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)){
                if(f.transform.IsChildOf(replacement)||f.GetComponentInParent<HotelTogether.HotelItem>())continue;
                // Imported stairs, trim and architecture, plus procedural partitions.
                if(f.GetComponentInParent<HotelTogether.HotelPlayer>())continue;
                CutMesh(f,cut);
            }
            foreach(var c in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)){
                if(c.isTrigger||c.transform.IsChildOf(replacement)||c.GetComponentInParent<HotelTogether.HotelItem>()||!cut.Intersects(c.bounds))continue;
                if(!c.transform.parent||(!c.transform.parent.name.StartsWith("Structure collision")&&!c.name.StartsWith("Partition")))continue;
                var bounds=c.bounds;Undo.RecordObject(c,"Remove old stair site collision");c.enabled=false;EditorUtility.SetDirty(c);
                foreach(var b in Outside(bounds,cut)){if(b.size.x<.001f||b.size.y<.001f||b.size.z<.001f)continue;var g=new GameObject("Retained outside stair site collision");g.transform.SetParent(retained.transform,false);g.transform.position=b.center;var box=g.AddComponent<BoxCollider>();box.size=b.size;}
            }
        }
    }
}
