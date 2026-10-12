using UnityEngine;
using UnityEngine.AI;
namespace HotelTogether
{
    [RequireComponent(typeof(HotelItem))]
    public sealed class HotelCleaningCart : MonoBehaviour
    {
        public HotelHousekeepingSystem system;
        public HotelCleaningTool mop;
        public Transform mopDock;
        public GameObject wasteVisual;
        public NavMeshObstacle obstacle;
        public int wasteCapacity = 6;
        public int Waste { get; private set; }
        public void SetPushing(bool pushing) { if (obstacle) obstacle.enabled = !pushing; }
        public bool Use(HotelPlayer player)
        {
            if (Vector3.Distance(player.transform.position, transform.position) > 3.2f) return false;
            var litter = player.held ? player.held.GetComponent<HotelFloorMess>() : null;
            if (litter && litter.type == FloorMessType.Litter)
            {
                if (Waste >= wasteCapacity) { player.Notify("Cart waste bag full. Push it to a sorting bin and E to empty."); return false; }
                player.Drop();
                if (!litter.Dispose()) return false;
                Waste++; RefreshWaste(); player.Notify("Litter bagged. Cart waste " + Waste + "/" + wasteCapacity + "."); return true;
            }
            var tool = player.held ? player.held.GetComponent<HotelCleaningTool>() : null;
            if (tool) { tool.Rinse(); player.Notify("Mop rinsed in the cart bucket. Ready for " + tool.UsesLeft + " wet spots."); return true; }
            if (player.held) { player.Notify("Housekeeping cart: bring a mop to rinse, or litter to dispose. G takes the mop."); return false; }
            if (player.cart) { player.EndCart(); return true; }
            player.BeginCart(GetComponent<HotelItem>()); return player.cart == GetComponent<HotelItem>();
        }
        public bool ToggleMop(HotelPlayer player)
        {
            if (Vector3.Distance(player.transform.position, transform.position) > 3.2f) return false;
            var heldTool = player.held ? player.held.GetComponent<HotelCleaningTool>() : null;
            if (heldTool)
            {
                if (mop && mop != heldTool && mop.DockedAt == this) { player.Notify("This cart already has a mop."); return false; }
                player.Drop(); mop = heldTool; Dock(); player.Notify("Mop parked. E to push the housekeeping cart."); return true;
            }
            if (player.held || player.IsReachingForItem) { player.Notify("Free your hands before taking the mop."); return false; }
            if (!mop || mop.DockedAt != this) { player.Notify("The mop is out. Bring it back with G."); return false; }
            if (player.cart) player.EndCart();
            mop.DockedAt = null; mop.transform.SetParent(system.transform, true);
            foreach (var c in mop.GetComponentsInChildren<Collider>()) c.enabled = true;
            if (player.RequestPickup(mop.GetComponent<HotelItem>())) return true;
            Dock(); return false;
        }
        public void Dock()
        {
            if (!mop || !mopDock) return;
            mop.transform.SetParent(mopDock, false); mop.transform.localPosition = Vector3.zero; mop.transform.localRotation = Quaternion.identity;
            mop.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var c in mop.GetComponentsInChildren<Collider>()) c.enabled = false;
            mop.DockedAt = this;
        }
        public int EmptyWaste() { int amount = Waste; Waste = 0; RefreshWaste(); return amount; }
        void RefreshWaste() { if (wasteVisual) wasteVisual.SetActive(Waste > 0); }
        public void ResetCart() { Waste = 0; RefreshWaste(); if (mop) { mop.Rinse(); Dock(); } SetPushing(false); }
    }
}
