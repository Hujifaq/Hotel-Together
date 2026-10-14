using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HotelTogether
{
    // Independent physics bodies drive the existing skinned skeleton in world space.
    // Navigation never writes to the same transforms while the ragdoll is active.
    [DefaultExecutionOrder(60)]
    public sealed class HotelGuestPhysics : MonoBehaviour
    {
        public bool Suspended => active || recovering;
        public bool IsCorpse { get; private set; }
        public bool IsHeld => holder;
        public Vector3 HeadPoint => active && headBody ? headBody.position : guest.visual.head.position;
        public string Complaint { get; private set; }
        public float ComplaintUntil { get; private set; }
        public int ImpactCount { get; private set; }
        HotelGuestBot guest;
        HotelPlayer holder;
        GameObject rig;
        Rigidbody headBody, hipsBody;
        Collider[] originalColliders;
        bool[] colliderStates;
        bool active, recovering, agentWasEnabled;
        Vector3 safePoint;
        Quaternion safeRotation;
        float age, settled, recoveryTime, nextComplaint, reaction;
        readonly List<Part> parts = new();
        readonly List<Collider> ignoredPlayers = new();
        readonly RaycastHit[] gripHits = new RaycastHit[24];
        readonly List<SkinState> skins = new();
        sealed class SkinState
        {
            public SkinnedMeshRenderer renderer;
            public Transform[] bones;
            public Bounds standingBounds;
            public bool updateWhenOffscreen;
        }
        sealed class Part
        {
            public Transform bone;
            public Rigidbody body;
            public Collider collider;
            public Vector3 localPosition, recoveryPosition;
            public Quaternion localRotation, recoveryRotation, standingRotation;
        }
        void Awake() { guest = GetComponent<HotelGuestBot>(); }
        bool Available => isActiveAndEnabled && guest && guest.visual && guest.visual.head &&
            (!guest.GetComponent<HotelHuman>() || guest.GetComponent<HotelHuman>().Targetable) &&
            (!guest.liftPassenger || !guest.liftPassenger.Aboard);

        public bool Grab(HotelPlayer player)
        {
            if (!Available || recovering || holder || !player || player.held || player.cart || player.seated) return false;
            if (Vector3.Distance(player.view.transform.position, HeadPoint) > 2.6f) return false;
            Activate(); holder = player; age = settled = 0;guest.Hotel.Economy?.Misconduct(guest,HotelMisconduct.Grab);
            var controller=player.GetComponent<CharacterController>();
            if(!ignoredPlayers.Contains(controller))ignoredPlayers.Add(controller);
            foreach (var part in parts) Physics.IgnoreCollision(part.collider, controller, true);
            Complain("Hey! Let go of my head!", true);
            return true;
        }
        public void Release(bool throwIt)
        {
            if (!holder) return;
            var player = holder; holder = null; age = settled = 0;
            // Keep ignoring the thrower's capsule until recovery to prevent self-collisions.
            var velocity = throwIt ? player.view.transform.forward * 8.2f + Vector3.up * 2.1f : Vector3.zero;
            if(throwIt)guest.Hotel.Economy?.Misconduct(guest,HotelMisconduct.Throw);
            if (throwIt) foreach (var part in parts)
            {
                part.body.linearVelocity = Vector3.ClampMagnitude(part.body.linearVelocity * .35f + velocity, 13);
                part.body.AddTorque(player.view.transform.right * 2.5f, ForceMode.VelocityChange);
            }
            Complain(throwIt ? "I am a guest, not a suitcase!" : "You call this hospitality?!", true);
        }
        public void Impact(Vector3 velocity, float mass, Vector3 point, HotelPlayer source)
        {
            if (!Available || recovering || velocity.magnitude < 1.5f) return;
            ImpactCount++;if(source)guest.Hotel.Economy?.Misconduct(guest,HotelMisconduct.Prop);
            float impulse = Mathf.Clamp(mass * velocity.magnitude, 0, 65);
            guest.ReactToPrank(Mathf.Clamp(impulse * .0025f, .03f, .18f));
            reaction = 1;
            Complain(impulse > 9 ? "Ow! Stop throwing things at me!" : "Watch it! That hit me!", false);
            if (impulse < 9 && !active) return;
            Activate(); age = settled = 0;
            Part nearest = parts[0]; float best = float.PositiveInfinity;
            foreach (var part in parts)
            {
                float distance = (part.body.position - point).sqrMagnitude;
                if (distance < best) { best = distance; nearest = part; }
            }
            nearest.body.AddForceAtPosition(velocity.normalized * impulse, point, ForceMode.Impulse);
        }
        void Complain(string text, bool force)
        {
            if (!force && Time.time < nextComplaint) return;
            nextComplaint = Time.time + 1.5f; Complaint = text; ComplaintUntil = Time.time + 4;
            guest.ReactToPrank(.04f);
        }
        public void Say(string text){Complaint=text;ComplaintUntil=Time.time+4;}
        public void BulletReaction(){reaction=1;Complain("You shot me! Help!",true);}
        public void Die(Vector3 point,Vector3 direction)
        {
            if(IsCorpse||!guest||!guest.visual)return;
            if(recovering)Restore();
            Activate();holder=null;IsCorpse=true;ComplaintUntil=0;
            foreach(var part in parts){part.body.linearVelocity=Vector3.ClampMagnitude(part.body.linearVelocity+direction*2+Vector3.up*.35f,6);}
            hipsBody.AddForceAtPosition(direction*35,point,ForceMode.Impulse);
        }
        void BuildRig()
        {
            if (rig) return;
            rig = new GameObject("Guest runtime ragdoll"); rig.transform.SetParent(transform, false);
            var v = guest.visual;
            Add(v.hips, null, 12, new Vector3(0,.02f,0), .17f, .36f);
            hipsBody = parts[0].body;
            Add(v.head.parent, hipsBody, 18, new Vector3(0,.16f,0), .18f, .48f);
            var torso = parts[1].body;
            Add(v.head, torso, 5, Vector3.zero, .22f, .46f); headBody = parts[2].body;
            Add(v.leftArm, torso, 3, new Vector3(0,-.13f,0), .075f, .30f);
            Add(v.leftForearm, parts[3].body, 2, new Vector3(0,-.12f,0), .06f, .28f);
            Add(v.rightArm, torso, 3, new Vector3(0,-.13f,0), .075f, .30f);
            Add(v.rightForearm, parts[5].body, 2, new Vector3(0,-.12f,0), .06f, .28f);
            Add(v.leftLeg, hipsBody, 6, new Vector3(0,-.14f,0), .09f, .32f);
            Add(v.leftShin, parts[7].body, 4, new Vector3(0,-.14f,0), .075f, .32f);
            Add(v.rightLeg, hipsBody, 6, new Vector3(0,-.14f,0), .09f, .32f);
            Add(v.rightShin, parts[9].body, 4, new Vector3(0,-.14f,0), .075f, .32f);
            for (int i = 0; i < parts.Count; i++) for (int j = i + 1; j < parts.Count; j++)
                Physics.IgnoreCollision(parts[i].collider, parts[j].collider, true);
            rig.SetActive(false);
        }
        void Add(Transform bone, Rigidbody parent, float mass, Vector3 center, float radius, float height)
        {
            var obj = new GameObject(bone.name + " physics"); obj.transform.SetParent(rig.transform, false);
            obj.transform.SetPositionAndRotation(bone.position, bone.rotation);
            var body = obj.AddComponent<Rigidbody>(); body.mass = mass; body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearDamping = .15f; body.angularDamping = .8f; body.maxAngularVelocity = 12;
            body.solverIterations = 14; body.solverVelocityIterations = 6; body.maxDepenetrationVelocity = 3;
            var capsule = obj.AddComponent<CapsuleCollider>(); capsule.center = center; capsule.radius = radius; capsule.height = height;
            obj.AddComponent<HotelGuestPhysicsPart>().owner = this;
            if (parent)
            {
                var joint = obj.AddComponent<CharacterJoint>(); joint.connectedBody = parent;
                joint.axis = Vector3.right; joint.swingAxis = Vector3.forward;
                joint.lowTwistLimit = new SoftJointLimit { limit = -35 };
                joint.highTwistLimit = new SoftJointLimit { limit = 35 };
                joint.swing1Limit = new SoftJointLimit { limit = bone == guest.visual.head ? 35 : 65 };
                joint.swing2Limit = new SoftJointLimit { limit = 35 };
                joint.enableProjection = true; joint.projectionDistance = .08f; joint.projectionAngle = 15;
            }
            parts.Add(new Part { bone = bone, body = body, collider = capsule,localPosition=guest.visual.PrankRestPosition(bone),localRotation=bone.localRotation });
        }
        void Activate()
        {
            if (active) return;
            safePoint = transform.position; safeRotation = transform.rotation;
            var navFilter=new NavMeshQueryFilter{agentTypeID=guest.Agent.agentTypeID,areaMask=guest.Agent.areaMask};
            if(NavMesh.SamplePosition(safePoint,out var safeHit,2,navFilter))safePoint=safeHit.position;
            guest.SuspendForPrank();
            // Seated/sleeping guests also need navigation restored after landing.
            agentWasEnabled = guest.Agent;
            if (guest.Agent && guest.Agent.enabled) guest.Agent.enabled = false;
            BuildRig();
            var originals=new List<Collider>();foreach(var collider in GetComponentsInChildren<Collider>())if(!collider.transform.IsChildOf(rig.transform))originals.Add(collider);originalColliders=originals.ToArray();
            colliderStates = new bool[originalColliders.Length];
            for (int i=0;i<originalColliders.Length;i++) { colliderStates[i]=originalColliders[i].enabled; originalColliders[i].enabled=false; }
            rig.SetActive(true);
            // Teleport every proxy while kinematic before configuring any connected anchor.
            // Interpolated Transform poses can still belong to the previous ragdoll activation.
            foreach (var part in parts)
            {
                part.body.isKinematic=true;part.body.interpolation=RigidbodyInterpolation.None;
                part.body.transform.SetPositionAndRotation(part.bone.position,part.bone.rotation);
                part.body.position=part.bone.position;part.body.rotation=part.bone.rotation;
            }
            Physics.SyncTransforms();
            foreach (var part in parts)
            {
                var joint=part.body.GetComponent<CharacterJoint>();
                if(joint){joint.autoConfigureConnectedAnchor=false;joint.anchor=Vector3.zero;joint.connectedAnchor=Quaternion.Inverse(joint.connectedBody.rotation)*(part.body.position-joint.connectedBody.position);}
                part.collider.enabled = true; part.body.isKinematic = false;
                part.body.interpolation=RigidbodyInterpolation.Interpolate;
                part.body.linearVelocity = part.body.angularVelocity = Vector3.zero;
            }
            active = true; age = settled = 0;
            CaptureSkins();
            UpdateSkinBounds();
            if (guest.visual.luggage) guest.visual.luggage.SetActive(false);
        }
        void CaptureSkins()
        {
            skins.Clear();
            foreach(var skin in guest.visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Disabled legacy skins must stay disabled when corrected art is installed.
                if(!skin.enabled)continue;
                skins.Add(new SkinState { renderer=skin, bones=skin.bones,
                    standingBounds=skin.localBounds, updateWhenOffscreen=skin.updateWhenOffscreen });
                skin.updateWhenOffscreen=true;
            }
        }
        void UpdateSkinBounds()
        {
            foreach(var state in skins)
            {
                var skin=state.renderer;if(!skin)continue;
                // Navigation stays at the pickup point while physical bones travel in world space.
                // Bounds use the root bone's space, matching SkinnedMeshRenderer culling.
                var root=skin.rootBone?skin.rootBone:skin.transform;
                bool first=true;var bounds=new Bounds();
                foreach(var bone in state.bones)
                {
                    if(!bone)continue;
                    var point=root.InverseTransformPoint(bone.position);
                    if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
                if(first)continue;
                // Includes the oversized head, hands and feet beyond their joint pivots.
                var scale=root.lossyScale;
                bounds.Expand(new Vector3(1.5f/Mathf.Max(Mathf.Abs(scale.x),.001f),
                    1.5f/Mathf.Max(Mathf.Abs(scale.y),.001f),1.5f/Mathf.Max(Mathf.Abs(scale.z),.001f)));
                skin.localBounds=bounds;
            }
        }
        void FixedUpdate()
        {
            if (!active || Time.deltaTime <= 0) return;
            age += Time.fixedDeltaTime;
            if(IsCorpse)return;
            if (holder)
            {
                if (!holder.gameObject.activeInHierarchy || holder.transportLocked || holder.combat && holder.combat.IsDown) { Release(false); return; }
                var eye = holder.view.transform; var target = eye.TransformPoint(new Vector3(.12f,-.12f,1.15f));
                var direction = (target-eye.position).normalized;
                int count=Physics.SphereCastNonAlloc(eye.position,.23f,direction,gripHits,1.15f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore);
                float clearance=1.15f;
                for(int i=0;i<count;i++)
                {
                    var hit=gripHits[i];
                    if(hit.collider.GetComponent<HotelGuestPhysicsPart>()?.owner==this||hit.collider.transform.IsChildOf(holder.transform))continue;
                    clearance=Mathf.Min(clearance,Mathf.Max(.35f,hit.distance-.08f));
                }
                target=eye.position+direction*clearance;
                var error = target-headBody.position;
                if (error.sqrMagnitude > 12) { Release(false); return; }
                headBody.AddForce(Vector3.ClampMagnitude(error*230-headBody.linearVelocity*32-Physics.gravity,220),ForceMode.Acceleration);
                headBody.AddTorque(-headBody.angularVelocity*8,ForceMode.Acceleration);
                if (Time.time > nextComplaint + 2) Complain("Put me down! I want the manager!",false);
                return;
            }
            bool still = true;
            foreach(var part in parts) if(part.body.linearVelocity.sqrMagnitude> .4f || part.body.angularVelocity.sqrMagnitude>2) still=false;
            settled = still ? settled+Time.fixedDeltaTime : 0;
            if (age > 1.2f && settled > .65f || age > 12 || hipsBody.position.y < -8) BeginRecovery();
        }
        bool LandingPoint(out Vector3 point)
        {
            point=safePoint;var desired=hipsBody.position;
            if(desired.y < -8)return true; // Only an actual out-of-world fall uses the safety checkpoint.
            var filter=new NavMeshQueryFilter{agentTypeID=guest.Agent.agentTypeID,areaMask=guest.Agent.areaMask};
            for(int i=0;i<17;i++)
            {
                float radius=i==0?0:i<=8?.45f:.9f;float angle=i*Mathf.PI/4;
                var probe=desired+new Vector3(Mathf.Cos(angle)*radius,.35f,Mathf.Sin(angle)*radius);
                var hits=Physics.RaycastAll(probe,Vector3.down,1.6f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore);System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
                foreach(var hit in hits)
                {
                    if(hit.collider.transform.IsChildOf(transform)||hit.normal.y<.65f)continue;
                    if(!NavMesh.SamplePosition(hit.point,out var nav,.45f,filter)||Mathf.Abs(nav.position.y-hit.point.y)>.15f)break;
                    bool blocked=false;foreach(var overlap in Physics.OverlapCapsule(nav.position+Vector3.up*.3f,nav.position+Vector3.up*1.3f,.22f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))if(!overlap.transform.IsChildOf(transform))blocked=true;if(blocked)break;
                    point=nav.position;return true;
                }
            }
            return false;
        }
        void BeginRecovery()
        {
            if(!LandingPoint(out var point))return;
            foreach(var part in parts) { part.recoveryPosition=part.bone.position; part.recoveryRotation=part.bone.localRotation; part.body.isKinematic=true;part.body.interpolation=RigidbodyInterpolation.None; part.collider.enabled=false; }
            active=false; recovering=true; recoveryTime=0;
            transform.SetPositionAndRotation(point,safeRotation);
            guest.visual.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            foreach(var part in parts){part.bone.localPosition=part.localPosition;part.bone.localRotation=part.localRotation;}
            guest.visual.ResetStandingPose();
            foreach(var part in parts)part.standingRotation=part.bone.localRotation;
            // The hips blend in world space; limbs keep their canonical lengths throughout get-up.
            hipsBody.position=guest.visual.hips.position;
            foreach(var part in parts){part.bone.localPosition=part.localPosition;part.bone.localRotation=part.recoveryRotation;}
            guest.visual.hips.position=parts[0].recoveryPosition;
        }
        void LateUpdate()
        {
            if (Time.deltaTime<=0 || !guest || !guest.visual) return;
            if (active)
            {
                foreach(var part in parts) part.bone.SetPositionAndRotation(part.body.position,part.body.rotation);
            }
            else if (recovering)
            {
                recoveryTime += Time.deltaTime; float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(recoveryTime/.85f));
                foreach(var part in parts)
                {
                    part.bone.localPosition=part.localPosition;
                    part.bone.localRotation=Quaternion.Slerp(part.recoveryRotation,part.standingRotation,blend);
                    if(part==parts[0])part.bone.position=Vector3.Lerp(part.recoveryPosition,hipsBody.position,blend);
                }
                if(recoveryTime>=.85f) Restore();
            }
            else if (reaction>0)
            {
                reaction=Mathf.MoveTowards(reaction,0,Time.deltaTime*1.2f);
                guest.visual.PoseComplaint(reaction,Time.time<ComplaintUntil);
            }
            else if (Time.time<ComplaintUntil) guest.visual.PoseComplaint(.25f,true);
            if(Suspended)UpdateSkinBounds();
        }
        void Restore()
        {
            holder=null; active=recovering=false;
            foreach(var part in parts) { part.body.isKinematic=true; part.collider.enabled=false; part.bone.localPosition=part.localPosition; part.bone.localRotation=part.localRotation; }
            foreach(var controller in ignoredPlayers)if(controller)foreach(var part in parts)Physics.IgnoreCollision(part.collider,controller,false);
            ignoredPlayers.Clear();
            foreach(var state in skins)if(state.renderer)
            {
                state.renderer.updateWhenOffscreen=state.updateWhenOffscreen;
                state.renderer.localBounds=state.standingBounds;
            }
            skins.Clear();
            if(rig)rig.SetActive(false);
            if(originalColliders!=null)for(int i=0;i<originalColliders.Length;i++)if(originalColliders[i])originalColliders[i].enabled=colliderStates[i];
            if(!IsCorpse&&(!guest.GetComponent<HotelHuman>()||!guest.GetComponent<HotelHuman>().Dead)){
                if(guest.Agent && agentWasEnabled) { guest.Agent.enabled=true; if(guest.Agent.isOnNavMesh)guest.Agent.Warp(transform.position); }
                reaction=1;guest.ResumeAfterPrank();
            }
        }
        void OnDisable() { if(Suspended && guest && guest.Agent) Restore(); }
    }
}
