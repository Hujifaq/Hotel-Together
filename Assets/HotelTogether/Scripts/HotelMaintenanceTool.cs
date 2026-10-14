using UnityEngine;
namespace HotelTogether
{
    public enum MaintenanceToolKind { Extinguisher, Wrench, Screwdriver }
    public enum MaintenanceAction { None, Spray, Valve, Wrench, Screwdriver, Breaker, Refill }
    [RequireComponent(typeof(HotelItem))]
    public sealed class HotelMaintenanceTool : MonoBehaviour
    {
        public MaintenanceToolKind kind;
        public float capacity=16;
        public Transform nozzle;
        public Transform squeezeLever;
        public float Charge { get; private set; }=16;
        public bool Spraying { get; private set; }
        ParticleSystem spray;
        AudioSource audioSource;
        HotelMaintenanceSystem system;
        public void Configure(HotelMaintenanceSystem owner,MaintenanceToolKind type)
        {
            system=owner;kind=type;Charge=capacity;
            var item=GetComponent<HotelItem>();item.kind="pickup";
            item.displayName=type==MaintenanceToolKind.Extinguisher?"Fire extinguisher":type==MaintenanceToolKind.Wrench?"Maintenance wrench":"Insulated screwdriver";
            var body=GetComponent<Rigidbody>();if(!body){body=gameObject.AddComponent<Rigidbody>();body.isKinematic=true;}
            body.mass=type==MaintenanceToolKind.Extinguisher?3.5f:.6f;
            if(type==MaintenanceToolKind.Extinguisher&&!spray)
            {
                if(!nozzle){nozzle=new GameObject("Extinguisher nozzle").transform;nozzle.SetParent(transform,false);nozzle.localPosition=new Vector3(.08f,.15f,.16f);}
                spray=HotelMaintenanceModels.Particles(nozzle,owner.FoamMaterial,new Color(.92f,.98f,1,.6f),70,.065f,.45f,8,7);
                var shape=spray.shape;shape.angle=11;shape.radius=.015f;
                audioSource=gameObject.AddComponent<AudioSource>();audioSource.clip=owner.Hiss;audioSource.loop=true;audioSource.spatialBlend=1;audioSource.minDistance=1;audioSource.maxDistance=12;audioSource.volume=.35f;
            }
        }
        public bool SetSpraying(bool value,Vector3 direction,float dt)
        {
            value=value&&kind==MaintenanceToolKind.Extinguisher&&Charge>0;
            if(value)
            {
                Charge=Mathf.Max(0,Charge-dt);nozzle.rotation=Quaternion.LookRotation(direction);
                if(!Spraying){spray.Play();audioSource.Play();}
            }
            else if(Spraying){spray.Stop(true,ParticleSystemStopBehavior.StopEmitting);audioSource.Stop();}
            if(squeezeLever)squeezeLever.localRotation=Quaternion.Slerp(squeezeLever.localRotation,Quaternion.Euler(0,0,value?0:-12),1-Mathf.Exp(-Mathf.Max(dt,.016f)*12));
            Spraying=value;return value;
        }
        public void Refill(){Charge=capacity;}
        public void ResetTool(){SetSpraying(false,Vector3.forward,0);Refill();}
        void OnDisable(){if(spray)SetSpraying(false,Vector3.forward,0);}
    }
}
