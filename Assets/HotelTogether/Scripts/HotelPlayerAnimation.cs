using UnityEngine;

namespace HotelTogether
{
    public enum HotelPlayerMotion { Idle, Walk, Run, Reach, Carry, PushCart, Seated, Mop, Block, Falling, Downed }
    public enum HotelPlayerGesture { None, Interact, Place, Throw, Strike, Build, Repair, Heal, Rescue, Hurt, Sit, Stand, Recover, Refill, Jump, Fire, Reload, CollectCash }

    // A presentation layer only: interactions, damage, cargo and movement keep their existing owners.
    public partial class HotelPlayer
    {
        HotelPlayerGesture gesture;
        float gestureTime, gestureDuration, animationClock, blockWeight;
        Vector3 gestureTarget;
        public HotelPlayerMotion CurrentMotion { get; private set; }
        public HotelPlayerGesture CurrentGesture => gesture;
        public float GestureProgress => gestureDuration > 0 ? Mathf.Clamp01(gestureTime / gestureDuration) : 0;
        public float GestureWeight => gesture == HotelPlayerGesture.None ? 0 : Mathf.Sin(GestureProgress * Mathf.PI);
        public float BlockWeight => blockWeight;
        public bool IsMopping => housekeeping && housekeeping.ActiveMop && held && held.GetComponent<HotelCleaningTool>();
        public float AnimationClock => animationClock;

        public void PlayGesture(HotelPlayerGesture action, Vector3 target)
        {
            if (action == HotelPlayerGesture.None) { gesture = action; return; }
            gesture = action; gestureTime = 0; gestureTarget = target;
            gestureDuration = action switch
            {
                HotelPlayerGesture.Throw => .52f,
                HotelPlayerGesture.Strike => .55f,
                HotelPlayerGesture.Build or HotelPlayerGesture.Repair => .85f,
                HotelPlayerGesture.Heal or HotelPlayerGesture.Rescue => .9f,
                HotelPlayerGesture.Refill => .85f,
                HotelPlayerGesture.Jump => .55f,
                HotelPlayerGesture.Fire => .16f,
                HotelPlayerGesture.Reload => 2.1f,
                HotelPlayerGesture.CollectCash => .48f,
                HotelPlayerGesture.Sit or HotelPlayerGesture.Stand or HotelPlayerGesture.Recover => .55f,
                HotelPlayerGesture.Hurt => .32f,
                HotelPlayerGesture.Place => .38f,
                _ => .42f
            };
        }

        void AnimateUse(HotelItem item)
        {
            if (!item || item.kind == "pickup" || item.kind == "mop" || item.kind == "seat" || item.kind == "cart") return;
            var mess = item.GetComponent<HotelFloorMess>();
            if (mess && mess.type == FloorMessType.Wet) return;
            PlayGesture(HotelPlayerGesture.Interact, item.transform.position);
        }

        void AdvanceActionAnimation(float dt)
        {
            if (dt <= 0) return;
            animationClock += dt;
            if (gesture != HotelPlayerGesture.None)
            {
                gestureTime += dt;
                if (gestureTime >= gestureDuration) gesture = HotelPlayerGesture.None;
            }
            blockWeight = Mathf.MoveTowards(blockWeight, combat && combat.Blocking ? 1 : 0, dt * 8);
            var velocity = cc && cc.enabled ? cc.velocity : Vector3.zero;
            float moving = new Vector2(velocity.x, velocity.z).magnitude;
            CurrentMotion = combat && combat.IsDown ? HotelPlayerMotion.Downed
                : seated ? HotelPlayerMotion.Seated
                : IsMopping ? HotelPlayerMotion.Mop
                : blockWeight > .1f ? HotelPlayerMotion.Block
                : cart ? HotelPlayerMotion.PushCart
                : pendingPickup ? HotelPlayerMotion.Reach
                : held || GrabbedGuest ? HotelPlayerMotion.Carry
                : cc && cc.enabled && !cc.isGrounded && vertical < -3 ? HotelPlayerMotion.Falling
                : moving > speed * 1.12f ? HotelPlayerMotion.Run
                : moving > .12f ? HotelPlayerMotion.Walk : HotelPlayerMotion.Idle;
        }

        void ResetActionAnimation()
        {
            gesture = HotelPlayerGesture.None; gestureTime = gestureDuration = blockWeight = 0;
            animationClock = 0; CurrentMotion = HotelPlayerMotion.Idle;
            if (employee && employee.Avatar) employee.Avatar.ResetPose();
        }

        // Shared by the seamless first-person hands and the employee arm IK.
        public bool TryAnimationHandPose(float side, out Vector3 target, out Quaternion rotation, out float curl)
        {
            target = Vector3.zero; rotation = Quaternion.identity; curl = .2f;
            if (!view || CurrentMotion == HotelPlayerMotion.Downed) return false;
            if(gesture==HotelPlayerGesture.CollectCash&&side<0){var reach=view.transform.position+Vector3.ClampMagnitude(gestureTarget-view.transform.position,.8f);target=Vector3.Lerp(view.transform.TransformPoint(new Vector3(-.24f,-.42f,.18f)),reach,GestureWeight);rotation=view.transform.rotation*Quaternion.Euler(60,-12,0);curl=.75f;return true;}if(GuestHandPose(side,out target,out rotation,out curl))return true;
            if(DefenseHandPose(side,out target,out rotation,out curl))return true;
            if(MaintenanceHandPose(side,out target,out rotation,out curl))return true;
            if(held&&held.GetComponent<HotelFirearm>() is HotelFirearm firearm)return firearm.HandPose(side,out target,out rotation,out curl);
            Vector3 rest = new Vector3(side * .30f, -.52f, .16f), pose = rest;
            float p = GestureProgress, weight = GestureWeight;
            bool left = side < 0;
            if (blockWeight > .01f && !held && !cart)
            {
                pose = Vector3.Lerp(rest, new Vector3(side * .21f, -.17f, .47f), blockWeight);
                curl = .92f;
            }
            else if (gesture != HotelPlayerGesture.None)
            {
                switch (gesture)
                {
                    case HotelPlayerGesture.Strike:
                        if (left) pose = Vector3.Lerp(rest, new Vector3(-.24f, -.30f, .29f), weight);
                        else
                        {
                            var windup = new Vector3(.32f, -.18f, .10f);
                            var hit = new Vector3(.03f, -.23f, .75f);
                            var swing = p < .28f ? Vector3.Lerp(rest, windup, Mathf.SmoothStep(0, 1, p / .28f))
                                : p < .58f ? Vector3.Lerp(windup, hit, Mathf.SmoothStep(0, 1, (p - .28f) / .30f))
                                : Vector3.Lerp(hit, rest, Mathf.SmoothStep(0, 1, (p - .58f) / .42f));
                            pose = swing;
                        }
                        curl = 1; break;
                    case HotelPlayerGesture.Throw:
                        pose = Vector3.Lerp(rest, new Vector3(side * .20f, -.24f, .66f), weight);
                        pose.z -= Mathf.Sin(Mathf.Clamp01(p / .35f) * Mathf.PI) * .22f;
                        curl = Mathf.Lerp(.95f, .08f, Mathf.SmoothStep(0, 1, p / .6f)); break;
                    case HotelPlayerGesture.Place:
                        pose = Vector3.Lerp(rest, new Vector3(side * .22f, -.68f, .45f), weight);
                        curl = .12f; break;
                    case HotelPlayerGesture.Heal:
                        pose = Vector3.Lerp(rest, new Vector3(side * .13f, -.48f, .35f), weight);
                        curl = .55f; break;
                    case HotelPlayerGesture.Refill:
                        pose = Vector3.Lerp(rest, new Vector3(side * .12f, -.22f, .65f), weight);
                        curl = .55f; break;
                    case HotelPlayerGesture.Rescue:
                        pose = Vector3.Lerp(rest, new Vector3(side * .18f, -.67f, .57f), weight);
                        curl = .35f; break;
                    case HotelPlayerGesture.Hurt:
                        pose = Vector3.Lerp(rest, new Vector3(side * .26f, -.29f, .25f), weight);
                        curl = .65f; break;
                    case HotelPlayerGesture.Jump:
                        pose = Vector3.Lerp(rest,new Vector3(side*.25f,-.34f,.28f),weight);curl=.4f;break;
                    case HotelPlayerGesture.Sit:
                    case HotelPlayerGesture.Stand:
                    case HotelPlayerGesture.Recover:
                        pose = Vector3.Lerp(rest, new Vector3(side * .28f, -.67f, .24f), weight); break;
                    default:
                        if (left && gesture == HotelPlayerGesture.Interact) return false;
                        var point = view.transform.InverseTransformPoint(gestureTarget);
                        point = Vector3.ClampMagnitude(point, .78f);
                        point.x = left ? -.22f : Mathf.Clamp(point.x, -.12f, .38f);
                        point.y = Mathf.Clamp(point.y, -.65f, -.13f);
                        point.z = Mathf.Clamp(point.z, .32f, .72f);
                        if (gesture == HotelPlayerGesture.Build || gesture == HotelPlayerGesture.Repair)
                            point.z += Mathf.Sin(p * Mathf.PI * 6) * .055f;
                        pose = Vector3.Lerp(rest, point, weight); curl = .35f; break;
                }
            }
            else return false;
            target = view.transform.TransformPoint(pose);
            rotation = view.transform.rotation * Quaternion.Euler(55 + weight * 20, side * 15, -side * blockWeight * 12);
            return true;
        }
    }
}
