using UnityEngine;
namespace HotelTogether
{
    public sealed class HotelThrownProp : MonoBehaviour
    {
        HotelPlayer thrower;
        Rigidbody body;
        float armedUntil;
        Vector3 velocity;
        public void Arm(HotelPlayer player) { thrower=player;body=GetComponent<Rigidbody>();armedUntil=Time.time+6;velocity=body.linearVelocity; }
        void FixedUpdate() { if(body && Time.time<armedUntil)velocity=body.linearVelocity; }
        void OnCollisionEnter(Collision collision)
        {
            if(Time.time>armedUntil || !body)return;
            var impactVelocity=velocity.sqrMagnitude>collision.relativeVelocity.sqrMagnitude?velocity:collision.relativeVelocity;
            Hit(collision.collider,impactVelocity,collision.GetContact(0).point,false);
        }
        void OnTriggerEnter(Collider other)
        {
            if(Time.time>armedUntil || !body)return;
            Hit(other,velocity,other.ClosestPoint(body.position),true);
        }
        void Hit(Collider other,Vector3 incoming,Vector3 point,bool trigger)
        {
            var guest=other.GetComponentInParent<HotelGuestBot>();
            var physics=guest?guest.GetComponent<HotelGuestPhysics>():other.GetComponent<HotelGuestPhysicsPart>()?.owner;
            if(!physics)return;
            physics.Impact(incoming,body.mass,point,thrower);
            if(trigger)body.linearVelocity=-incoming*.18f;
            armedUntil=0;
        }
    }
}
