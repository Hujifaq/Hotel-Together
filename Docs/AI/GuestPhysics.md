# Customer physics and pranks

Implemented 2026-10-14. Guests receive `HotelGuestPhysics` automatically from `HotelGuestBot.Initialize`; no scene or prefab wiring is required.

## Controls

- Aim at a customer's head within 2.6 metres and hold **LMB** to grab it.
- Move/look around to drag the articulated body. Both hands follow the head.
- Release **LMB** to let go; **RMB** throws the guest with a forward/upward impulse and player follow-through.
- Hold **LMB** to pick up ordinary props, release to drop and **RMB** to throw. Tools/trays remain equipped after release. **Space** jumps. Legacy E/Q and contextual cart G shortcuts remain.

## Behavior

Eleven independent rigid bodies drive the existing skeleton: hips, torso, head, upper/lower arms and upper/lower legs. Character joints limit neck/limb rotations; self-collisions are suppressed, solver iterations and depenetration velocity are bounded, and the head grip uses a damped force in FixedUpdate. World collision checks shorten the grip target at obstacles. Bodies remain physical while falling, with continuous speculative collision detection.

AI navigation, seated/water activity and survival updates yield while a guest is in ragdoll or recovery. After settling, the skeleton blends upright over .85 seconds, the collision capsule is restored, and the agent returns to the landing floor's NavMesh. Recovery requires actual physical support, matching navigation height and clear standing space within about one metre of the body. An expired timer does not teleport an airborne or obstructed guest back to the pickup point. Only a fall below the world boundary uses the last safe checkpoint. Guests aboard moving elevators cannot be grabbed.

Ragdoll activation resets all proxy bodies while kinematic with interpolation disabled, synchronizes their poses, then computes connected anchors from Rigidbody positions/rotations in a second pass. This avoids reading interpolated transforms from an earlier grab. Canonical bone translations are cached once; repeated grabs cannot save deformed limb lengths. Get-up blends the hips in world space and limbs in local rotation, preserving bone lengths and clearing seated/IK presentation before navigation resumes.

Thrown props are armed for six game seconds. Guest trigger capsules and ragdoll collision bodies both receive hits. Light impacts cause a recoil/complaint gesture; stronger momentum triggers a ragdoll and applies the impact force near the hit. Complaints have cooldowns, display above the guest, animate head/arms/brows/mouth, and reduce satisfaction. Dialogue is text in English; recorded voice acting is not included.

Pause freezes physics, gestures and complaint lifetimes. Player disable/down/reset releases the guest; guest physics disable restores navigation/colliders. The implementation is local single-player and uses procedural animation rather than new mocap clips.

Skinned rendering follows physical bones throughout grabbing, falling, recovery and corpse motion. The navigation root remains at the pickup point during ragdoll, so its standing bounds cannot be used to cull a displaced body. Activation caches enabled skins' original bounds/offscreen settings, allows offscreen skin updates and recomputes padded bone bounds in root-bone space after each physical pose. Restore reinstates standing bounds and the original offscreen policy. Disabled legacy skins remain disabled.

## Validation

Runtime compilation uses the Unity Roslyn compiler and the project's references. `HotelGuestPhysicsValidation.Run` exercises the actual tourist prefab in a disposable Unity project with a baked floor, engine physics, first-person hands, pause, throws, projectile impacts and recovery. Full HotelLobby visual/clearance and performance checks remain separate from these isolated tests.

The stability update passes 62 isolated Play Mode checks, including five moved-root grab/turn/throw cycles, bone proportions and standing height, an expired recovery timer in midair, actual ground landing and an upper-floor drop without snapping downstairs. Maximum measured joint-anchor error across the repeated cycles was approximately .08m. Reports: workspace `output/ragdoll-stability-qa`. Native live guests were also checked and their already-corrupted runtime rigs repaired without resetting the wallet or day.

The batch project uses the tourist prefab's original skin; it does not import the corrected GLB resource, so its missing-resource diagnostic is expected in this test environment. An Editor Search indexing exception was also present. Neither is a reported gameplay test failure, but this run does not validate the corrected rendered skin, speech bubble appearance or live keyboard/cursor input. Headless input is driven through gameplay APIs while FixedUpdate, LateUpdate and engine physics run normally.

The disappearing-body fix was reproduced separately with the corrected GLB skin in the main Editor: bones were displaced eight metres while the navigation root stayed still. The camera saw the actual baked geometry but rejected the old renderer bounds, producing a blank render. After the fix, the same camera renders the complete customer. Twenty-eight native checks cover four camera/pose directions, unit-scale mesh vertex containment, bone containment at .7/1.4 scale, disabled legacy skins and restoration of the original rendering settings. The 62 physics regression checks also pass with the new code. Evidence and before/after renders: workspace `output/guest-visibility-qa`. Native visibility checks use a temporary isolated camera and direct pose/physics entry points rather than live mouse input; scene assets are not saved by the probe.
