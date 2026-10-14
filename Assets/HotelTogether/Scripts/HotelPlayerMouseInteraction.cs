using UnityEngine;
using UnityEngine.InputSystem;
namespace HotelTogether
{
    public partial class HotelPlayer
    {
        [Header("Mouse interactions and jump")]
        public bool keepWorkToolsEquipped=true;
        [Range(.3f,1.6f)]public float jumpHeight=.85f;
        bool pointerGrip,awaitPrimaryRelease,throwUntilPrimaryRelease,continuousToolAction,continuousFirearmAction;
        int primaryHandledFrame=-1,secondaryHandledFrame=-1;
        float jumpPresentation;
        public bool PointerGripActive=>pointerGrip;
        public bool PrimaryInputHandled=>primaryHandledFrame==Time.frameCount||pointerGrip||throwUntilPrimaryRelease||awaitPrimaryRelease;
        public bool SecondaryInputHandled=>secondaryHandledFrame==Time.frameCount||pointerGrip||throwUntilPrimaryRelease;
        public bool PrimaryHeld=>!awaitPrimaryRelease&&!throwUntilPrimaryRelease&&Mouse.current!=null&&Mouse.current.leftButton.isPressed;
        public bool InteractionHeld=>PrimaryHeld||Keyboard.current!=null&&Keyboard.current.eKey.isPressed;
        public bool WorkPrimaryHeld=>PrimaryHeld&&continuousToolAction&&!pointerGrip;
        public bool MaintenanceHeld=>WorkPrimaryHeld||Keyboard.current!=null&&Keyboard.current.eKey.isPressed;
        public bool IsAirborne=>cc&&cc.enabled&&!cc.isGrounded&&vertical>-20&&jumpPresentation>0;
        public float JumpPresentation=>jumpPresentation;

        void HandleMouseInteraction(Mouse mouse)
        {
            if(!mouse.leftButton.isPressed){awaitPrimaryRelease=false;throwUntilPrimaryRelease=false;continuousToolAction=false;continuousFirearmAction=false;}
            if(mouse.leftButton.wasReleasedThisFrame)ReleasePrimaryInteraction();
            if(mouse.leftButton.wasPressedThisFrame&&!awaitPrimaryRelease&&!throwUntilPrimaryRelease)BeginPrimaryInteraction();
            if(mouse.rightButton.wasPressedThisFrame)SecondaryInteraction();
            if(pointerGrip&&!mouse.leftButton.isPressed)ReleasePrimaryInteraction();
            if(continuousFirearmAction&&PrimaryHeld&&held&&held.GetComponent<HotelFirearm>() is HotelFirearm gun&&gun.automatic)gun.TryFire(this);
            if(PrimaryHeld&&!pointerGrip&&held&&held.GetComponent<HotelCleaningTool>()&&focused&&focused.GetComponent<HotelFloorMess>() is HotelFloorMess mess&&mess.type==FloorMessType.Wet&&!mess.Cleared&&!mess.Mopping)mess.BeginCleaning(this);
            if(jumpPresentation>0){jumpPresentation=Mathf.Max(0,jumpPresentation-Time.deltaTime);if(cc.isGrounded&&vertical<=0)jumpPresentation=0;}
        }
        public bool BeginPrimaryInteraction()
        {
            if(transportLocked||combat&&combat.IsDown||pendingPickup||pointerGrip||awaitPrimaryRelease||throwUntilPrimaryRelease)return false;
            continuousToolAction=false;
            continuousFirearmAction=false;
            var latch=focused?focused.GetComponent<HotelDefenseLatch>():null;
            if(latch){primaryHandledFrame=Time.frameCount;return latch.opening.ToggleLock(this);}
            if(DefenseWork&&DefenseWork.Equipped){primaryHandledFrame=Time.frameCount;var site=focused?focused.GetComponentInParent<HotelDefenseOpening>():null;if(site){continuousToolAction=true;DefenseWork.Begin(site);return true;}if(!HasInteraction(focused))return true;}
            if(focused&&focused.GetComponent<HotelCash>() is HotelCash cash){primaryHandledFrame=Time.frameCount;return cash.TryCollect(this);}
            if(!held&&!cart&&!seated&&GrabFocusedGuest())
            {pointerGrip=true;primaryHandledFrame=Time.frameCount;return true;}
            // A missed head grab must never fall through to reception service.
            var receptionGuest=focused?focused.GetComponent<HotelGuestBot>():null;
            if(!cart&&!seated&&receptionGuest&&receptionGuest.AwaitingCheckIn&&(!held||!held.GetComponent<HotelFirearm>()))
            {primaryHandledFrame=Time.frameCount;Notify(receptionGuest.Payment.Pending?"Click the cash, not the guest, to complete check-in.":"E requests check-in payment. Hold LMB on the head to grab.");return true;}
            if(seated){Use();primaryHandledFrame=Time.frameCount;return true;}
            if(held)
            {
                var gun=held.GetComponent<HotelFirearm>();
                if(gun){primaryHandledFrame=Time.frameCount;if(FirearmUtility(focused))Use();else{continuousFirearmAction=true;gun.TryFire(this);}return true;}
                // A held tool has its own continuous action; a fresh click on a target uses it.
                if(held.GetComponent<HotelMaintenanceTool>()&&(!HasInteraction(focused)||focused.GetComponent<HotelMaintenanceIncident>()))
                {continuousToolAction=true;primaryHandledFrame=Time.frameCount;if(focused&&focused.GetComponent<HotelMaintenanceIncident>())UseMaintenance(focused);return true;}
                if(held.GetComponent<HotelWeapon>()&&!HasInteraction(focused))return false;
                if(held.GetComponent<HotelCleaningTool>()&&!HasInteraction(focused)){primaryHandledFrame=Time.frameCount;return true;}
                if(HasInteraction(focused)){continuousToolAction=focused.GetComponent<HotelMaintenanceIncident>();Use();primaryHandledFrame=Time.frameCount;return true;}
                // Clicking open space resumes a mouse grip on an object picked up with E.
                pointerGrip=!IsWorkEquipment(held);primaryHandledFrame=Time.frameCount;return true;
            }
            if(cart){var dockedMop=focused?focused.GetComponent<HotelCleaningTool>():null;if(dockedMop&&dockedMop.DockedAt){primaryHandledFrame=Time.frameCount;return dockedMop.DockedAt.TakeMop(this);}Use();primaryHandledFrame=Time.frameCount;return true;}
            if(!focused||!HasInteraction(focused))return false;
            bool pickup=focused.kind=="pickup"||focused.kind=="mop";
            if(pickup)
            {
                var mop=focused.GetComponent<HotelCleaningTool>();
                if(mop&&mop.DockedAt){if(!mop.DockedAt.ToggleMop(this))return false;}
                else if(!RequestPickup(focused))return false;
                pointerGrip=!IsWorkEquipment(focused);primaryHandledFrame=Time.frameCount;return true;
            }
            continuousToolAction=focused.GetComponent<HotelMaintenanceIncident>();Use();primaryHandledFrame=Time.frameCount;
            // Floor litter takes the same reach path; a cart uses a held push grip.
            if(pendingPickup)pointerGrip=!IsWorkEquipment(pendingPickup);
            else if(cart)pointerGrip=true;
            return true;
        }
        bool FirearmUtility(HotelItem item)=>item&&(item.kind=="door"||item.kind=="defenseLock"||item.kind=="bell"||item.kind=="lamp"||item.kind=="water"||item.GetComponent<HotelArmoryResupply>()||item.GetComponent<HotelCash>()||item.GetComponent<HotelElevatorButton>());
        bool IsWorkEquipment(HotelItem item)=>item&&(item.GetComponent<HotelFirearm>()||item.GetComponent<HotelDefenseHammer>()||keepWorkToolsEquipped&&(item.GetComponent<HotelMaintenanceTool>()||item.GetComponent<HotelCleaningTool>()||item.GetComponent<HotelServiceTray>()||item.GetComponent<HotelWeapon>()||item.GetComponent<HotelGuestSupply>()||item.GetComponent<HotelDetergentBottle>()));
        public void ReleasePrimaryInteraction(bool deliver=true)
        {
            if(!pointerGrip)return;
            pointerGrip=false;primaryHandledFrame=Time.frameCount;
            CancelPickupReach();
            if(GrabbedGuest)ReleaseGuest(false);
            else if(held)
            {
                var carried=held;
                if(deliver&&IsDropReceiver(focused))Use();
                if(held==carried)Drop();
            }
            else if(cart)EndCart();
        }
        public bool SecondaryInteraction()
        {
            if(transportLocked||combat&&combat.IsDown)return false;
            if(DefenseWork&&DefenseWork.Equipped&&focused&&focused.GetComponentInParent<HotelDefenseOpening>()){DefenseWork.CycleMode();secondaryHandledFrame=primaryHandledFrame=Time.frameCount;return true;}
            if(!GrabbedGuest&&!held){if(!pendingPickup)return false;CancelPickupReach();pointerGrip=false;secondaryHandledFrame=primaryHandledFrame=Time.frameCount;throwUntilPrimaryRelease=Mouse.current!=null&&Mouse.current.leftButton.isPressed;return true;}
            secondaryHandledFrame=primaryHandledFrame=Time.frameCount;
            pointerGrip=false;throwUntilPrimaryRelease=Mouse.current!=null&&Mouse.current.leftButton.isPressed;
            CancelPickupReach();
            if(GrabbedGuest)ReleaseGuest(true);else Drop(true);
            return true;
        }
        bool IsDropReceiver(HotelItem item)=>item&&(item.GetComponent<HotelRoomDelivery>()||item.GetComponent<HotelLaundryMachine>()||item.GetComponent<HotelLuggageTrolley>()||item.GetComponent<HotelWasteBin>()||item.GetComponent<HotelCleaningCart>());
        public void ParkCartForTool(){if(cart)EndCart();pointerGrip=false;continuousToolAction=false;}
        void CancelMouseGrip()
        {
            ReleasePrimaryInteraction(false);CancelPickupReach();
            continuousToolAction=continuousFirearmAction=false;
            awaitPrimaryRelease=Mouse.current!=null&&Mouse.current.leftButton.isPressed;
            throwUntilPrimaryRelease=false;
        }
        public bool TryJump()
        {
            if(!cc||!cc.enabled||!cc.isGrounded||vertical>0||seated||cart||transportLocked||combat&&combat.IsDown)return false;
            var center=transform.TransformPoint(cc.center)+transform.up*(cc.height*.5f-cc.radius);
            if(Physics.SphereCast(center,cc.radius*.92f,Vector3.up,out _,.18f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))return false;
            vertical=Mathf.Sqrt(2*Mathf.Abs(Physics.gravity.y)*jumpHeight);jumpPresentation=1.3f;
            PlayGesture(HotelPlayerGesture.Jump,transform.position);return true;
        }
        public void MoveCharacter(Vector3 horizontal,float dt,bool jumpPressed=false)
        {
            if(dt<=0||!cc||!cc.enabled||seated||transportLocked||combat&&combat.IsDown)return;
            if(cc.isGrounded&&vertical<0)vertical=-2;
            if(jumpPressed)TryJump();
            vertical+=Physics.gravity.y*dt;
            var collision=cc.Move(horizontal+Vector3.up*vertical*dt);
            if((collision&CollisionFlags.Above)!=0&&vertical>0)vertical=0;
        }
        public void RefreshInteractionFocus()
        {
            focused=null;if(!view)return;
            if(Physics.Raycast(view.transform.position,view.transform.forward,out var hit,3.1f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Collide))
            {focused=hit.collider.GetComponentInParent<HotelItem>();if(!focused)focused=hit.collider.GetComponentInParent<DoorLeaf>()?.owner;}
            if(held&&held.GetComponent<HotelCleaningTool>()&&housekeeping&&hit.collider&&hit.normal.y>.6f)
            {
                var wet=housekeeping.WetAt(hit.point);if(wet)focused=wet.GetComponent<HotelItem>();
            }
        }
    }
}
