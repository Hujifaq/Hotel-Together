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
        void Start(){ConfigureWetVisual();}
        void ConfigureWetVisual()
        {
            if(type!=FloorMessType.Wet||transform.position.y>=1)return;
            var hits=Physics.RaycastAll(transform.position+Vector3.up*.12f,Vector3.down,.35f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore);
            foreach(var hit in hits)
            {
                if(hit.normal.y<.9f||hit.collider.attachedRigidbody||hit.collider.GetComponentInParent<HotelItem>())continue;
                foreach(var r in visuals)if(r){var p=r.transform.position;p.y=hit.point.y+.003f;r.transform.position=p;}break;
            }
            UpdateVisual();
        }
        public string Requirement => type == FloorMessType.Wet ? "Mop required. Click the cart mop shaft or use G to take it; LMB at the cart rinses it." : "Empty hands: E pick up litter, then LMB at a sorting bin or housekeeping cart.";
        public string ActionLabel => type == FloorMessType.Litter ? "PICK UP LITTER" : "HOLD LMB  MOP FLOOR";
        public bool ContainsFloorPoint(Vector3 point)
        {
            if(Cleared||type!=FloorMessType.Wet||Mathf.Abs(point.y-transform.position.y)>.12f)return false;
            foreach(var c in colliders)if(c&&c.enabled){var b=c.bounds;b.Expand(new Vector3(.22f,.25f,.22f));if(b.Contains(point))return true;}return false;
        }
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
            return BeginCleaning(player);
        }
        public bool BeginCleaning(HotelPlayer player)
        {
            if(Cleared||type!=FloorMessType.Wet||!system||!player||player.GameplayPaused||!InReach(player))return false;
            var mop = player.held ? player.held.GetComponent<HotelCleaningTool>() : null;
            if (!mop || player.cart) { player.Notify(Requirement); return false; }
            if (mop.UsesLeft <= 0) { player.Notify("Mop is saturated. Hold it and LMB at a housekeeping cart to rinse."); return false; }
            cleaner = player; tool = mop; Mopping = true; return true;
        }
        void Update()
        {
            if (!system || !system.hotel.Running || !Mopping) return;
            AdvanceWork(Time.deltaTime,cleaner&&cleaner.InteractionHeld);
        }
        public void AdvanceWork(float dt,bool inputHeld)
        {
            if(!Mopping||!system||dt<=0||!float.IsFinite(dt)||Time.timeScale<=0)return;
            if (!cleaner || cleaner.combat && cleaner.combat.IsDown || !cleaner.held || cleaner.held.GetComponent<HotelCleaningTool>() != tool || cleaner.FocusedItem != GetComponent<HotelItem>() || !InReach(cleaner) || !inputHeld)
            { Mopping = false; cleaner = null; tool = null; return; }
            Progress = Mathf.Min(1, Progress + dt*(system.hotel.Economy&&system.hotel.Economy.Initialized?system.hotel.Economy.WorkSpeed:1) / cleanSeconds); UpdateVisual();
            if (Progress >= 1) { tool.Consume(); Clear(); cleaner.Notify("Floor dried. Mop capacity " + tool.UsesLeft + "/" + tool.capacity + "."); Mopping = false; cleaner = null; tool = null; }
        }
        public bool StaffWork(Vector3 position,float dt){if(Cleared||Mopping||!system||dt<=0||!float.IsFinite(dt)||Vector3.Distance(position,transform.position)>1.5f)return false;Progress=Mathf.Min(1,Progress+dt/cleanSeconds);UpdateVisual();if(Progress>=1)Clear();return true;}
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
            foreach (var r in visuals) if (r) { r.GetPropertyBlock(block); var color = transform.position.y<1?new Color(.24f,.38f,.40f,.22f):r.sharedMaterial.color; color.a *= 1 - Progress; block.SetColor("_BaseColor", color);block.SetColor("_Color",color); r.SetPropertyBlock(block); }
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
