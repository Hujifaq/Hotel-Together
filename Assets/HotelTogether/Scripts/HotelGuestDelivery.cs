using UnityEngine;
namespace HotelTogether
{
    public enum GuestDeliveryType { Luggage, DirtyLaundry, CleanLaundry }
    [RequireComponent(typeof(HotelItem),typeof(Rigidbody))]
    public sealed class HotelGuestDelivery : MonoBehaviour
    {
        public GuestDeliveryType type;
        public Renderer linenRenderer;
        public HotelGuestBot Owner { get; private set; }
        public HotelLuggageTrolley Trolley { get; internal set; }
        Transform anchor;
        Vector3 anchorOffset; Quaternion anchorRotation;
        Collider[] physical; bool[] collisionStates;
        RigidbodyInterpolation interpolation;
        public bool Attached => anchor;
        public void AttachTo(Transform target,Vector3 offset,Quaternion rotation)
        {
            Detach();ReleaseAttachment();var body=GetComponent<Rigidbody>();
            physical=GetComponentsInChildren<Collider>();collisionStates=new bool[physical.Length];
            for(int i=0;i<physical.Length;i++){collisionStates[i]=physical[i].enabled;physical[i].enabled=false;}
            interpolation=body.interpolation;if(!body.isKinematic)body.linearVelocity=body.angularVelocity=Vector3.zero;
            body.isKinematic=true;body.interpolation=RigidbodyInterpolation.None;
            anchor=target;anchorOffset=offset;anchorRotation=rotation;transform.SetParent(target,true);SyncAttachment();
        }
        public void SyncAttachment()
        {
            if(!anchor)return;SetPose(anchor.TransformPoint(anchorOffset),anchor.rotation*anchorRotation);
        }
        public void SetPose(Vector3 position,Quaternion rotation)
        {
            transform.SetPositionAndRotation(position,rotation);var body=GetComponent<Rigidbody>();body.position=position;body.rotation=rotation;
        }
        public void ReleaseAttachment()
        {
            if(!anchor)return;anchor=null;transform.SetParent(Owner&&Owner.Hotel.serviceJobs?Owner.Hotel.serviceJobs.transform:null,true);
            for(int i=0;i<physical.Length;i++)if(physical[i])physical[i].enabled=collisionStates[i];
            var body=GetComponent<Rigidbody>();body.interpolation=interpolation;body.isKinematic=false;body.useGravity=true;body.linearVelocity=body.angularVelocity=Vector3.zero;
        }
        void OnDestroy(){if(Trolley)Trolley.Detach(this);}
        public void Assign(HotelGuestBot owner,GuestDeliveryType purpose)
        {
            Owner=owner;type=purpose;
            GetComponent<HotelItem>().displayName=Label;
            foreach(var label in GetComponentsInChildren<TextMesh>())label.text=owner.guestName+"\nROOM "+owner.RoomLabel;
        }
        public string Label=>!Owner?"Guest supplies":Owner.guestName+" / Room "+Owner.RoomLabel+" / "+(type==GuestDeliveryType.Luggage?"Luggage":type==GuestDeliveryType.DirtyLaundry?"Dirty linen":"Clean linen");
        public void Detach(){if(Trolley)Trolley.Detach(this);}
    }
}
