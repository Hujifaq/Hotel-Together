using System.Collections.Generic;
using UnityEngine;
namespace HotelTogether {
 [DefaultExecutionOrder(60)] public sealed class HotelZombieDeathPhysics:MonoBehaviour {
  readonly List<Transform> bones=new();readonly List<Rigidbody> bodies=new();readonly List<Collider> colliders=new();
  public bool Active=>bodies.Count==11;
  public void Initialize(HotelGuestVisual v,Vector3 impulse){if(bodies.Count>0||!v)return;var parts=new[]{v.hips,v.head.parent,v.head,v.leftArm,v.leftForearm,v.rightArm,v.rightForearm,v.leftLeg,v.leftShin,v.rightLeg,v.rightShin};var parent=new[]{-1,0,1,1,3,1,5,0,7,0,9};var masses=new[]{12f,18,5,3,2,3,2,6,4,6,4};
   var root=new GameObject("Zombie death ragdoll");root.transform.SetParent(transform,false);if(v.luggage)v.luggage.SetActive(false);
   for(int i=0;i<parts.Length;i++){var go=new GameObject(parts[i].name+" death physics");go.transform.SetParent(root.transform,false);go.transform.SetPositionAndRotation(parts[i].position,parts[i].rotation);var rb=go.AddComponent<Rigidbody>();rb.mass=masses[i];rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;rb.linearDamping=.25f;rb.angularDamping=.9f;rb.maxDepenetrationVelocity=3;rb.maxAngularVelocity=10;rb.solverIterations=14;rb.solverVelocityIterations=6;var c=go.AddComponent<CapsuleCollider>();c.radius=i==0?.17f:i==1?.18f:i==2?.22f:.075f;c.height=i==1?.48f:i==2?.46f:.32f;c.center=i>2?Vector3.down*.12f:i==1?Vector3.up*.16f:Vector3.zero;
    if(parent[i]>=0){var joint=go.AddComponent<CharacterJoint>();joint.connectedBody=bodies[parent[i]];joint.axis=Vector3.right;joint.swingAxis=Vector3.forward;joint.lowTwistLimit=new SoftJointLimit{limit=-35};joint.highTwistLimit=new SoftJointLimit{limit=35};joint.swing1Limit=new SoftJointLimit{limit=55};joint.swing2Limit=new SoftJointLimit{limit=35};joint.enableProjection=true;joint.projectionDistance=.08f;joint.projectionAngle=15;}
    bones.Add(parts[i]);bodies.Add(rb);colliders.Add(c);rb.linearVelocity=impulse.normalized*2.4f+Vector3.up*.3f;
   }
   for(int i=0;i<colliders.Count;i++)for(int k=i+1;k<colliders.Count;k++)Physics.IgnoreCollision(colliders[i],colliders[k],true);
  }
  void LateUpdate(){if(Time.deltaTime<=0)return;for(int i=0;i<bones.Count;i++)if(bones[i]&&bodies[i])bones[i].SetPositionAndRotation(bodies[i].position,bodies[i].rotation);}
 }
}
