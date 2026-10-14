using System.Collections.Generic;
using UnityEngine;
namespace HotelTogether
{
    public sealed class HotelGuestWaterStation : MonoBehaviour
    {
        public Transform approach, nozzle, cupPosition;
        public GameObject cupPrefab;
        public LineRenderer stream;
        public bool WaterAvailable {get;private set;}=true;
        public void SetSupply(bool available){WaterAvailable=available;if(!available&&stream)stream.enabled=false;}
        public int DrinksServed { get; private set; }
        readonly List<HotelGuestBot> queue=new();
        public int Join(HotelGuestBot guest)
        {
            if(!WaterAvailable)return -1;queue.RemoveAll(g=>!g);
            if(!queue.Contains(guest)){if(queue.Count>=4)return -1;queue.Add(guest);}
            return queue.IndexOf(guest);
        }
        public int Position(HotelGuestBot guest)=>queue.IndexOf(guest);
        public Vector3 QueuePoint(int index)=>approach.position+Vector3.left*Mathf.Max(0,index)*.85f;
        public void Release(HotelGuestBot guest,bool drank=false)
        {
            if(queue.Count>0&&queue[0]==guest&&stream)stream.enabled=false;
            if(queue.Remove(guest)&&drank)DrinksServed++;
        }
        public void Pour(HotelGuestBot guest,bool enabled)
        {
            if(!stream||Position(guest)!=0)return;if(!WaterAvailable)enabled=false;
            stream.enabled=enabled;
            if(enabled){stream.SetPosition(0,nozzle.position);stream.SetPosition(1,cupPosition.position+Vector3.up*.08f);}
        }
        public void ResetStation(){queue.Clear();DrinksServed=0;if(stream)stream.enabled=false;}
        void OnDisable(){queue.Clear();if(stream)stream.enabled=false;}
    }
}
