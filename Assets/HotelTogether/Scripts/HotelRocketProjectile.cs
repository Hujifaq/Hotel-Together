using System.Collections.Generic;
using UnityEngine;
namespace HotelTogether {
 public sealed class HotelRocketProjectile:MonoBehaviour {
  HotelFirearm gun;HotelPlayer player;Vector3 direction;float age;bool exploded;Material material;
  public bool Exploded=>exploded;
  public static HotelRocketProjectile Launch(HotelFirearm source,HotelPlayer owner,Vector3 position,Vector3 forward){var go=GameObject.CreatePrimitive(PrimitiveType.Capsule);go.name="Live rocket";Destroy(go.GetComponent<Collider>());go.transform.SetPositionAndRotation(position,Quaternion.LookRotation(forward)*Quaternion.Euler(90,0,0));go.transform.localScale=new Vector3(.06f,.15f,.06f);var rocket=go.AddComponent<HotelRocketProjectile>();rocket.gun=source;rocket.player=owner;rocket.direction=forward.normalized;rocket.material=new Material(Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color"));rocket.material.color=new Color(.7f,.4f,.12f);go.GetComponent<Renderer>().sharedMaterial=rocket.material;return rocket;}
  public void Advance(float dt){if(exploded||dt<=0)return;float distance=24*dt;RaycastHit hit;bool contact=gun?gun.FindHit(transform.position,direction,distance,out hit):Physics.Raycast(transform.position,direction,out hit,distance,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore);if(contact){transform.position=hit.point;Explode();return;}transform.position+=direction*distance;age+=dt;if(age>4)Explode();}
  void Update(){Advance(Time.deltaTime);}
  public void Explode(){if(exploded)return;exploded=true;var center=transform.position;var affected=new HashSet<Object>();foreach(var c in Physics.OverlapSphere(center,2.6f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Collide)){var target=HotelFirearm.Target(c);if(!target||affected.Contains(target))continue;var point=c.ClosestPoint(center);var path=point-center;if(path.magnitude>.07f&&Physics.Raycast(center+path.normalized*.04f,path.normalized,out var obstruction,path.magnitude-.05f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore)&&HotelFirearm.Target(obstruction.collider)!=target)continue;affected.Add(target);float d=Vector3.Distance(center,c.bounds.center);float amount=(gun?gun.damage:120)*Mathf.Lerp(1,.3f,Mathf.Clamp01(d/2.6f));bool killed;if(HotelFirearm.ApplyHit(c,amount,player?player.transform.position:center,point,path.normalized,out killed)&&gun)gun.NotifyRocketHit(killed);}
   if(gun)gun.Impact(center,Vector3.up,false);GetComponent<Renderer>().enabled=false;transform.localScale=Vector3.one;var ps=gameObject.AddComponent<ParticleSystem>();var main=ps.main;main.loop=false;main.startLifetime=.5f;main.startSpeed=4;main.startSize=.14f;main.maxParticles=80;main.startColor=new Color(1,.5f,.1f);var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.2f;ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;ps.Emit(50);Destroy(gameObject,1.1f);
  }
  void OnDestroy(){if(material)Destroy(material);}
 }
}
