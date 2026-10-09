using System;
using UnityEngine;
namespace HotelTogether {
 [Serializable] public class MigrationData {public PropData[] props;public BodyData[] colliders;}
 [Serializable] public class PropData {public string id,name,file,kind;public float[] size;public BodyData body;public DoorData door;}
 [Serializable] public class BodyData {public float[] position,rotation;public float mass;public ShapeData[] shapes;}
 [Serializable] public class ShapeData {public int type;public float[] size,offset,rotation;public float radius;}
 [Serializable] public class DoorData {public float x,z,w,height, @base,outside;public bool slide;public LeafData[] leaves;}
 [Serializable] public class LeafData {public string id;public float side,width,hinge,closed;public BodyData body;}
 public static class Coordinates {public static Vector3 V(float[] a)=>a!=null&&a.Length>=3?new Vector3(-a[0],a[1],a[2]):Vector3.zero;public static Vector3 Size(float[] a)=>new Vector3(a[0],a[1],a[2]);public static Quaternion Q(float[] a)=>a!=null&&a.Length==4?new Quaternion(a[0],-a[1],-a[2],a[3]):Quaternion.identity;}
}
