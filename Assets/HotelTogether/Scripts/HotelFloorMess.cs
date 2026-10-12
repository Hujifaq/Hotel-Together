using UnityEngine;
using UnityEngine.InputSystem;
namespace HotelTogether
{
    public enum FloorMessType { Wet, Litter }
    [RequireComponent(typeof(HotelItem))]
    public sealed class HotelFloorMess : MonoBehaviour
    {
        public HotelHousekeepingSystem system;
        public FloorMessType type;
        public bool generated;
        public Renderer[] visuals;
        public float cleanSeconds = 3;
        public float Progress { get; private set; }
        public bool Cleared { get; private set; }
        public bool Mopping { get; private set; }
        HotelPlayer cleaner;
        HotelCleaningTool tool;
        MaterialPropertyBlock block;
        Collider[] colliders;
        void Awake() { colliders = GetComponentsInChildren<Collider>(); block = new MaterialPropertyBlock(); }
        public string Requirement => type == FloorMessType.Wet ? "Mop required. G at a housekeeping cart to take one; E at the cart rinses it." : "Empty hands: E pick up litter, then E at a sorting bin or housekeeping cart.";
        public string ActionLabel => type == FloorMessType.Litter ? "PICK UP LITTER" : "HOLD E  MOP FLOOR";
        bool InReach(HotelPlayer player) => Mathf.Abs(player.transform.position.y-transform.position.y) < .65f && Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(transform.position.x,transform.position.z)) < 2.25f;
        public bool Use(HotelPlayer player)
        {
            if (Cleared || !system || !system.hotel.Running || !InReach(player)) return false;
            if(player.cart){player.EndCart();player.Notify("Cart parked. G at the cart takes its mop; empty hands can E pick up litter.");return false;}
            if (type == FloorMessType.Litter)
            {
                if (player.held || player.cart) { player.Notify(Requirement); return false; }
                return player.RequestPickup(GetComponent<HotelItem>());
            }
            var mop = player.held ? player.held.GetComponent<HotelCleaningTool>() : null;
            if (!mop || player.cart) { player.Notify(Requirement); return false; }
            if (mop.UsesLeft <= 0) { player.Notify("Mop is saturated. Hold it and E at a housekeeping cart to rinse."); return false; }
            cleaner = player; tool = mop; Mopping = true; return true;
        }
        void Update()
        {
            if (!system || !system.hotel.Running || !Mopping) return;
            if (!cleaner || cleaner.combat && cleaner.combat.IsDown || !cleaner.held || cleaner.held.GetComponent<HotelCleaningTool>() != tool || cleaner.FocusedItem != GetComponent<HotelItem>() || !InReach(cleaner) || Keyboard.current == null || !Keyboard.current.eKey.isPressed)
            { Mopping = false; cleaner = null; tool = null; return; }
            Progress = Mathf.Min(1, Progress + Time.deltaTime / cleanSeconds); UpdateVisual();
            if (Progress >= 1) { tool.Consume(); Clear(); cleaner.Notify("Floor dried. Mop capacity " + tool.UsesLeft + "/" + tool.capacity + "."); Mopping = false; cleaner = null; tool = null; }
        }
        public bool Dispose()
        {
            if (Cleared || type != FloorMessType.Litter) return false;
            Clear(); system.hotel.player.Notify("Litter disposed. Housekeeping +$5."); return true;
        }
        void Clear()
        {
            if (Cleared) return; Cleared = true; Progress = 1;
            foreach (var r in visuals) if (r) r.enabled = false;
            foreach (var c in colliders) if (c) c.enabled = false;
            var body = GetComponent<Rigidbody>(); if (body) body.isKinematic = true;
            GetComponent<HotelItem>().enabled = false; system.CompletedMess(this);
        }
        void UpdateVisual()
        {
            if (type != FloorMessType.Wet) return;
            foreach (var r in visuals) if (r) { r.GetPropertyBlock(block); var color = r.sharedMaterial.color; color.a *= 1 - Progress; block.SetColor("_BaseColor", color); r.SetPropertyBlock(block); }
        }
        public void ResetMess()
        {
            Progress = 0; Cleared = Mopping = false; cleaner = null; tool = null;
            foreach (var r in visuals) if (r) r.enabled = true;
            if (colliders == null) colliders = GetComponentsInChildren<Collider>();
            foreach (var c in colliders) if (c) c.enabled = true;
            GetComponent<HotelItem>().enabled = true;
            if (block == null) block = new MaterialPropertyBlock(); UpdateVisual();
        }
    }
}
