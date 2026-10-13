using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace HotelTogether
{
    [RequireComponent(typeof(HotelItem))]
    public sealed class HotelLuggageTrolley : MonoBehaviour
    {
        public Transform[] slots;
        public Transform leftHandle,rightHandle;
        public float pushDistance=1.38f,groundOffset=1.05f;
        public NavMeshObstacle obstacle;
        public void SetPushing(bool pushing){if(obstacle)obstacle.enabled=!pushing;}
        readonly List<HotelGuestDelivery> loaded=new();
        public IReadOnlyList<HotelGuestDelivery> Loaded=>loaded;
        public int Count { get {loaded.RemoveAll(b=>!b);return loaded.Count;} }
        public string RouteSummary
        {
            get { var result=new System.Text.StringBuilder();foreach(var bag in loaded)if(bag&&bag.Owner){if(result.Length>0)result.Append("\n");result.Append("ROOM ").Append(bag.Owner.RoomLabel).Append("  /  ").Append(bag.Owner.guestName);}return result.Length>0?result.ToString():"No luggage loaded"; }
        }
        public bool Load(HotelPlayer player)
        {
            var bag=player.held?player.held.GetComponent<HotelGuestDelivery>():null;
            if(!bag||bag.type!=GuestDeliveryType.Luggage){player.Notify("Load a guest suitcase onto this trolley.");return false;}
            if(Vector3.Distance(player.transform.position,transform.position)>3.2f||Count>=slots.Length){player.Notify("Trolley full - four suitcases maximum.");return false;}
            player.Drop();bag.Detach();loaded.Add(bag);bag.Trolley=this;
            var body=bag.GetComponent<Rigidbody>();body.linearVelocity=body.angularVelocity=Vector3.zero;body.isKinematic=true;
            foreach(var c in bag.GetComponentsInChildren<Collider>())c.enabled=false;
            Arrange();player.Notify("Loaded for Room "+bag.Owner.RoomLabel+". J shows the delivery route. Take the lift to floor 2.");return true;
        }
        void Arrange(){for(int i=0;i<loaded.Count;i++){if(!loaded[i])continue;loaded[i].transform.SetParent(slots[i],false);loaded[i].transform.localPosition=Vector3.zero;loaded[i].transform.localRotation=Quaternion.identity;}}
        public void Detach(HotelGuestDelivery bag)
        {
            if(!loaded.Remove(bag))return;bag.Trolley=null;bag.transform.SetParent(transform.parent,true);
            foreach(var c in bag.GetComponentsInChildren<Collider>())c.enabled=true;
            bag.GetComponent<Rigidbody>().isKinematic=false;Arrange();
        }
        public bool Unload(HotelPlayer player)
        {
            if(player.cart){player.Notify("E release the trolley before unloading.");return false;}
            if(player.held||Count==0)return false;
            var bag=loaded[loaded.Count-1];Detach(bag);bag.transform.position=player.view.transform.TransformPoint(new Vector3(.25f,-.6f,1.2f));return player.RequestPickup(bag.GetComponent<HotelItem>());
        }
        public void ResetCargo(){foreach(var bag in loaded.ToArray())if(bag)Detach(bag);loaded.Clear();}
        public Vector3 PositionFor(Vector3 player,float heading)=>player+Quaternion.Euler(0,heading,0)*Vector3.forward*pushDistance+Vector3.up*groundOffset;
    }
}
