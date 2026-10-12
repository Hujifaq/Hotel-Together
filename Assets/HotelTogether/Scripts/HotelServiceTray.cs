using System.Collections.Generic;
using UnityEngine;
namespace HotelTogether
{
    public sealed class HotelServiceTray : MonoBehaviour
    {
        public Transform[] slots;
        readonly List<HotelGuestSupply> loaded=new();
        public int Count { get {loaded.RemoveAll(s=>!s);return loaded.Count;} }
        void UpdateName(){GetComponent<HotelItem>().displayName="Service tray / "+Count+" of "+slots.Length;}
        public bool Load(HotelGuestSupply supply,HotelPlayer player)
        {
            if(!supply||supply.type!=GuestNeed.Meal&&supply.type!=GuestNeed.Water||Count>=slots.Length){player.Notify("Tray holds three meals or drinks.");return false;}
            if(player.held!=GetComponent<HotelItem>()){player.Notify("Pick up the service tray first.");return false;}
            var body=supply.GetComponent<Rigidbody>();if(body){body.isKinematic=true;body.detectCollisions=false;}
            foreach(var c in supply.GetComponentsInChildren<Collider>())c.enabled=false;
            var item=supply.GetComponent<HotelItem>();item.enabled=false;
            supply.transform.SetParent(slots[Count],false);supply.transform.localPosition=Vector3.zero;supply.transform.localRotation=Quaternion.identity;loaded.Add(supply);
            UpdateName();player.Notify("Tray "+Count+" / 3. E at a guest to serve.");return true;
        }
        public bool Serve(GuestNeed need)
        {
            loaded.RemoveAll(s=>!s);var supply=loaded.Find(s=>s.type==need);if(!supply)return false;
            loaded.Remove(supply);Destroy(supply.gameObject);for(int i=0;i<loaded.Count;i++){loaded[i].transform.SetParent(slots[i],false);loaded[i].transform.localPosition=Vector3.zero;loaded[i].transform.localRotation=Quaternion.identity;}UpdateName();return true;
        }
        public void ResetTray(){foreach(var supply in loaded)if(supply)Destroy(supply.gameObject);loaded.Clear();UpdateName();}
    }
}
