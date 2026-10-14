using UnityEngine;
namespace HotelTogether
{
    [DefaultExecutionOrder(120),RequireComponent(typeof(HotelItem))]
    public sealed class HotelCleaningTool : MonoBehaviour
    {
        public int capacity = 3;
        public int UsesLeft { get; private set; } = 3;
        public HotelCleaningCart DockedAt { get; internal set; }
        public HotelCleaningCart PickupOrigin {get;internal set;}
        CapsuleCollider dockGrip;
        Rigidbody body;
        public void ConfigureDock(bool docked)
        {
            if(!body)body=GetComponent<Rigidbody>();
            if(!dockGrip){dockGrip=gameObject.AddComponent<CapsuleCollider>();dockGrip.direction=1;dockGrip.radius=.115f;dockGrip.height=.75f;dockGrip.center=new Vector3(0,.50f,0);}
            foreach(var collider in GetComponentsInChildren<Collider>()){collider.enabled=!docked&&collider!=dockGrip;if(collider!=dockGrip)collider.isTrigger=false;}
            dockGrip.isTrigger=true;dockGrip.enabled=docked;
            if(docked&&body){if(!body.isKinematic){body.linearVelocity=body.angularVelocity=Vector3.zero;}body.isKinematic=true;body.interpolation=RigidbodyInterpolation.None;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;}
        }
        void LateUpdate(){if(!DockedAt||!DockedAt.mopDock)return;if(!body)body=GetComponent<Rigidbody>();var dock=DockedAt.mopDock;transform.SetPositionAndRotation(dock.position,dock.rotation);if(body){body.position=dock.position;body.rotation=dock.rotation;}}
        public void Rinse() { UsesLeft = capacity; }
        public void Consume() { UsesLeft = Mathf.Max(0, UsesLeft - 1); }
    }
}
