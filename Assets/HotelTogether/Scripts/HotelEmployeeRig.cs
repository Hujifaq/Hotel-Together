using UnityEngine;
using UnityEngine.Rendering;

namespace HotelTogether
{
    /// <summary>Presentation rig for the supplied stylized employees. Movement remains owned by HotelPlayer.</summary>
    public sealed class HotelEmployeeRig : MonoBehaviour
    {
        public Transform hips, chest, head;
        public Transform[] leftLeg, rightLeg, leftArm, rightArm;
        public Transform[] fingers;
        public SkinnedMeshRenderer bodyRenderer, headRenderer;
        public Color skinColor, uniformColor;
        Transform[] bones;
        Vector3[] bindPositions;
        Quaternion[] bindRotations;
        float phase, motion, reachWeight;
        Vector3 lastPosition, reachPosition;
        Transform reachObject;
        float throwUntil;
        float seatedWeight, downWeight, runWeight, turnLean, fallWeight, workWeight,jumpWeight;
        bool groundFeet=true;
        float lastHeading;
        bool initialized;
        readonly RaycastHit[] groundHits = new RaycastHit[12];
        Mesh fullBodyMesh, interactionBodyMesh;

        // The original seamless first-person hands replace the supplied arms during interaction.
        // Keep mesh changes local to this instance; imported/generated assets stay intact.
        public void SetInteractionArmsHidden(bool hidden)
        {
            if (!fullBodyMesh) fullBodyMesh = bodyRenderer.sharedMesh;
            if (hidden && !interactionBodyMesh)
            {
                interactionBodyMesh = Instantiate(fullBodyMesh);
                interactionBodyMesh.name = fullBodyMesh.name + " - first person interaction";
                var armBones = new System.Collections.Generic.HashSet<Transform>(leftArm);
                armBones.UnionWith(rightArm);
                armBones.UnionWith(fingers);
                var weights = fullBodyMesh.boneWeights;
                var rendererBones = bodyRenderer.bones;
                var armVertex = new bool[weights.Length];
                float ArmWeight(int index, float weight) => index < rendererBones.Length && armBones.Contains(rendererBones[index]) ? weight : 0;
                for (int i = 0; i < weights.Length; i++)
                {
                    var w = weights[i];
                    armVertex[i] = ArmWeight(w.boneIndex0,w.weight0)+ArmWeight(w.boneIndex1,w.weight1)+ArmWeight(w.boneIndex2,w.weight2)+ArmWeight(w.boneIndex3,w.weight3) >= .5f;
                }
                for (int submesh = 0; submesh < fullBodyMesh.subMeshCount; submesh++)
                {
                    var source = fullBodyMesh.GetTriangles(submesh);
                    var kept = new System.Collections.Generic.List<int>(source.Length);
                    for (int i = 0; i < source.Length; i += 3)
                        if (!armVertex[source[i]] && !armVertex[source[i+1]] && !armVertex[source[i+2]])
                        { kept.Add(source[i]); kept.Add(source[i+1]); kept.Add(source[i+2]); }
                    interactionBodyMesh.SetTriangles(kept,submesh,false);
                }
            }
            bodyRenderer.sharedMesh = hidden ? interactionBodyMesh : fullBodyMesh;
        }

        void OnDestroy() { if (interactionBodyMesh) Destroy(interactionBodyMesh); }

        public void Initialize(bool firstPerson)
        {
            if (!initialized)
            {
                bones = bodyRenderer.bones;
                bindPositions = new Vector3[bones.Length];
                bindRotations = new Quaternion[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    bindPositions[i] = bones[i].localPosition;
                    bindRotations[i] = bones[i].localRotation;
                }
                lastPosition = transform.position;
                lastHeading = transform.eulerAngles.y;
                initialized = true;
            }
            // Hide only the local head. Its shadow still belongs to the complete character.
            headRenderer.shadowCastingMode = firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        public void BeginReach(Transform item, Vector3 point)
        {
            reachObject = item;
            reachPosition = item ? item.InverseTransformPoint(point) : point;
        }

        public void EndReach() { reachObject = null; }
        public void ThrowGesture() { throwUntil = Time.time + .28f; }

        public void ResetPose()
        {
            phase = motion = reachWeight = seatedWeight = downWeight = runWeight = turnLean = fallWeight = workWeight = jumpWeight = 0;groundFeet=true;
            reachObject = null; throwUntil = 0;
            lastPosition = transform.position; lastHeading = transform.eulerAngles.y;
            if (!initialized) return;
            for (int i = 0; i < bones.Length; i++)
            { bones[i].localPosition = bindPositions[i]; bones[i].localRotation = bindRotations[i]; }
        }

                public void PoseStaff(float speed,bool carrying,bool working,float dt){PoseStaffAction(speed,working?StaffMotion.Repair:carrying?StaffMotion.CarryTray:StaffMotion.Walk,transform.TransformPoint(new Vector3(-.2f,1.05f,.4f)),transform.TransformPoint(new Vector3(.2f,1.05f,.4f)),dt);}
        public void PoseStaffAction(float speed,StaffMotion action,Vector3 leftTarget,Vector3 rightTarget,float dt){
            if(!initialized)Initialize(false);if(dt<=0)return;
            for(int i=0;i<bones.Length;i++){bones[i].localPosition=bindPositions[i];bones[i].localRotation=bindRotations[i];}
            motion=Mathf.MoveTowards(motion,Mathf.Clamp01(speed/2.1f),dt*7);phase+=Mathf.Min(speed,3)*dt*8;groundFeet=true;runWeight=seatedWeight=downWeight=fallWeight=jumpWeight=0;
            bool bend=action==StaffMotion.Pickup||action==StaffMotion.Place||action==StaffMotion.Rescue||action==StaffMotion.Knock;
            hips.localPosition+=Vector3.up*(Mathf.Sin(phase*2)*.012f*motion-(bend?.08f:0));chest.localRotation*=Quaternion.Euler(bend?18:action==StaffMotion.Mop?8:0,Mathf.Sin(phase)*motion*2,0);
            PoseLeg(leftLeg,-1,phase,Vector3.forward);PoseLeg(rightLeg,1,phase+Mathf.PI,Vector3.forward);
            bool rightActive=action!=StaffMotion.Walk&&action!=StaffMotion.Idle;bool leftActive=action==StaffMotion.CarryTray||action==StaffMotion.Serve||action==StaffMotion.Mop||action==StaffMotion.Pickup||action==StaffMotion.Rescue||action==StaffMotion.Knock;
            Vector3 leftRest=transform.TransformPoint(new Vector3(-.31f,.78f,.08f+Mathf.Sin(phase+Mathf.PI)*.055f*motion));Vector3 rightRest=transform.TransformPoint(new Vector3(.31f,.78f,.08f+Mathf.Sin(phase)*.055f*motion));
            SolveLimb(leftArm,leftActive?leftTarget:leftRest,-transform.right*.8f-transform.forward*.3f,.25f,.235f,1.15f);
            SolveLimb(rightArm,rightActive?rightTarget:rightRest,transform.right*.8f-transform.forward*.3f,.25f,.235f,1.15f);
            leftArm[2].rotation=transform.rotation*Quaternion.Euler(leftActive?-65:0,-10,10);rightArm[2].rotation=transform.rotation*Quaternion.Euler(rightActive?-65:0,10,-10);
            foreach(var finger in fingers){bool left=finger.name.StartsWith("Left");finger.localRotation*=Quaternion.Euler((left?leftActive:rightActive)?-42:-5,0,0);}
        }
public void Pose(HotelPlayer player, float dt)
        {
            if (!initialized) Initialize(true);
            if (dt <= 0) return;
            float elapsed=Mathf.Clamp(dt,.0001f,.25f);
            dt = Mathf.Min(elapsed,.20f);
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = bindPositions[i];
                bones[i].localRotation = bindRotations[i];
            }
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            var local = transform.InverseTransformVector(delta / elapsed);
            local.y = 0;
            // Ignore reset/teleport displacement, which must not become a walking stride.
            float speed = delta.sqrMagnitude > 4 ? 0 : local.magnitude;
            bool postureChange = player.CurrentGesture == HotelPlayerGesture.Sit || player.CurrentGesture == HotelPlayerGesture.Stand || player.CurrentGesture == HotelPlayerGesture.Recover;
            if (postureChange && player.GestureProgress < .12f) { speed = 0; local = Vector3.zero; }
            runWeight = Mathf.MoveTowards(runWeight, speed > player.speed * 1.12f ? 1 : 0, dt * 5);
            seatedWeight = Mathf.MoveTowards(seatedWeight, player.seated ? 1 : 0, dt * 3.5f);
            downWeight = Mathf.MoveTowards(downWeight, player.combat && player.combat.IsDown ? 1 : 0, dt * 5);
            fallWeight = Mathf.MoveTowards(fallWeight, player.CurrentMotion == HotelPlayerMotion.Falling ? 1 : 0, dt * 6);
            workWeight = Mathf.MoveTowards(workWeight, player.IsMopping || player.IsMaintaining ? 1 : 0, dt * 6);
            float turn = Mathf.Clamp(Mathf.DeltaAngle(lastHeading, transform.eulerAngles.y) / elapsed, -150, 150) / 150;
            lastHeading = transform.eulerAngles.y;
            turnLean = Mathf.Lerp(turnLean, turn, 1 - Mathf.Exp(-dt * 10));
            motion = Mathf.MoveTowards(motion, player.seated ? 0 : Mathf.Clamp01(speed / 2.7f), dt * 7);
            phase += Mathf.Min(speed, 5) * elapsed * 8;
            float breath = Mathf.Sin(player.AnimationClock * 2.1f) * .004f * (1 - motion);
            float working = workWeight;
            jumpWeight=Mathf.MoveTowards(jumpWeight,player.IsAirborne?1:0,dt*8);groundFeet=!player.IsAirborne;
            float action = player.GestureWeight;
            float actionLean = player.CurrentGesture == HotelPlayerGesture.Hurt ? -9 : player.CurrentGesture == HotelPlayerGesture.Rescue ? 14 : player.CurrentGesture == HotelPlayerGesture.Place ? 7 : player.CurrentGesture == HotelPlayerGesture.Interact ? 4 : player.CurrentGesture == HotelPlayerGesture.Build || player.CurrentGesture == HotelPlayerGesture.Repair ? 6 : 0;
            hips.localPosition += Vector3.up * (breath + Mathf.Sin(phase * 2) * Mathf.Lerp(.008f,.018f,runWeight) * motion - .31f * seatedWeight - .38f * downWeight - working * .06f);
            chest.localRotation *= Quaternion.Euler(runWeight * 6 + working * 9 + downWeight * 24 + actionLean * action,
                Mathf.Sin(phase) * 2 * motion + (player.CurrentGesture == HotelPlayerGesture.Strike ? -18 * action : 0),
                Mathf.Sin(phase) * 1.2f * motion - turnLean * 3);
            if(head) head.localRotation *= Quaternion.Euler(-player.ViewPitch * .55f,turnLean * 7,0);
            // A slight backward lean clears the chunky jacket/apron from the downward view.
            float downward=Mathf.InverseLerp(35,80,player.ViewPitch);
            chest.localPosition+=Vector3.back*(downward*.07f);
            var direction = local.sqrMagnitude > .01f ? local.normalized : Vector3.forward;
            PoseLeg(leftLeg, -1, phase, direction);
            PoseLeg(rightLeg, 1, phase + Mathf.PI, direction);

            bool carrying = player.TryGetEmployeeGripPoints(out var left, out var right, out bool twoHands);
            reachWeight = Mathf.MoveTowards(reachWeight, reachObject || carrying ? 1 : 0, dt * 7);
            Vector3 reach = reachObject ? reachObject.TransformPoint(reachPosition) : right;
            PoseArm(player, leftArm, -1, phase + Mathf.PI, carrying && twoHands, left, false);
            PoseArm(player, rightArm, 1, phase, carrying, carrying ? right : reach, reachObject != null);
            for (int i = 0; i < fingers.Length; i++)
            {
                bool leftFinger = fingers[i].name.StartsWith("Left");
                float curl = carrying && (!leftFinger || twoHands) ? 45 : 5;
                if(!carrying && player.TryAnimationHandPose(leftFinger?-1:1,out _,out _,out float actionCurl))curl=actionCurl*55;
                fingers[i].localRotation *= Quaternion.Euler(-curl, 0, 0);
            }
        }

        void PoseLeg(Transform[] chain, float side, float cycle, Vector3 direction)
        {
            float swing = Mathf.Sin(cycle);
            float lift = Mathf.Max(0, Mathf.Cos(cycle)) * Mathf.Lerp(.055f,.085f,runWeight) * motion;
            Vector3 foot = new Vector3(side * .116f, .13f + lift, .12f);
            foot += direction * (swing * Mathf.Lerp(.13f,.20f,runWeight) * motion);
            foot = Vector3.Lerp(foot, new Vector3(side * .14f, .13f, .38f), seatedWeight);
            foot = Vector3.Lerp(foot, new Vector3(side * .17f, .13f, .29f), downWeight);
            foot += Vector3.up * (fallWeight * .08f+jumpWeight*.16f);
            Vector3 target = transform.TransformPoint(foot);
            int count = groundFeet?Physics.RaycastNonAlloc(target + Vector3.up * .35f, Vector3.down,
                groundHits, .9f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore):0;
            float nearest = float.PositiveInfinity;
            Vector3 normal = Vector3.up;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.normal.y < .55f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                target.y = hit.point.y + .13f + lift;
                normal = hit.normal;
            }
            SolveLimb(chain, target, transform.forward, .34f, .29f, 1.1f);
            chain[2].rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.FromToRotation(Vector3.up, normal) * transform.rotation, 15);
        }

        void PoseArm(HotelPlayer player, Transform[] chain, float side, float cycle, bool carrying, Vector3 grip, bool reaching)
        {
            Vector3 rest = transform.TransformPoint(new Vector3(side * .31f, .78f, .08f + Mathf.Sin(cycle) * Mathf.Lerp(.055f,.10f,runWeight) * motion));
            rest = Vector3.Lerp(rest, transform.TransformPoint(new Vector3(side * .16f,.50f,.35f)), seatedWeight);
            rest += transform.right * (side * fallWeight * .10f) + transform.up * (fallWeight * .12f);
            float weight = carrying || reaching ? reachWeight : 0;
            Vector3 target = Vector3.Lerp(rest, grip, Mathf.SmoothStep(0, 1, weight));
            bool animated = player.TryAnimationHandPose(side, out var actionTarget, out var actionRotation, out _);
            animated &= !reaching&&(!carrying||player.held&&player.held.GetComponent<HotelFirearm>());
            if (animated) target = actionTarget;
            target = Vector3.Lerp(target, transform.TransformPoint(new Vector3(side * .23f,.28f,.34f)), downWeight);
            if (throwUntil > Time.time)
                target += transform.forward * (Mathf.Sin((throwUntil - Time.time) / .28f * Mathf.PI) * .10f);
            Vector3 pole = transform.right * side * .8f - transform.forward * .35f;
            SolveLimb(chain, target, pole, .25f, .235f, 1.8f);
            chain[2].rotation = animated ? actionRotation : transform.rotation * Quaternion.Euler(-70 * weight, side * 14 * weight, -side * 12 * weight);
        }

        void SolveLimb(Transform[] chain, Vector3 target, Vector3 pole, float upperLength, float lowerLength, float maxStretch)
        {
            Vector3 start = chain[0].position;
            Vector3 direction = target - start;
            float distance = direction.magnitude;
            if (distance < .001f) return;
            direction /= distance;
            float stretch = Mathf.Clamp(distance / ((upperLength + lowerLength) * .98f), 1, maxStretch);
            upperLength *= stretch; lowerLength *= stretch;
            distance = Mathf.Clamp(distance, Mathf.Abs(upperLength - lowerLength) + .001f, upperLength + lowerLength - .001f);
            Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
            if (bend.sqrMagnitude < .01f) bend = Vector3.ProjectOnPlane(transform.right, direction).normalized;
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2 * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
            Vector3 elbow = start + direction * along + bend * height;
            Vector3 end = start + direction * distance;
            Vector3 bindUpper = bindPositions[System.Array.IndexOf(bones, chain[1])];
            Vector3 bindLower = bindPositions[System.Array.IndexOf(bones, chain[2])];
            chain[0].rotation = Quaternion.FromToRotation(transform.TransformDirection(bindUpper), elbow - start) * transform.rotation;
            chain[1].position = elbow;
            chain[1].rotation = Quaternion.FromToRotation(transform.TransformDirection(bindLower), end - elbow) * transform.rotation;
            chain[2].position = end;
        }
    }
}
