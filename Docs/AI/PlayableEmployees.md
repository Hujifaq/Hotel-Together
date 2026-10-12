# Playable employees V1

The supplied Bellhop and Housekeeping GLBs are retained under `SourceAssets/Employees` and imported under `Assets/HotelTogether/Models/Employees`. Source Python and README files were inspected as asset documentation, not executed. Both sources were unrigged, with inconsistent scale and separated clothing pieces.

## Character preparation

`HotelEmployeeBuilder` creates 1.86 m stylized employee prefabs with longer working legs, consistent facing/ground origin, continuous hip/ankle bridges, repaired apron contact against the actual tunic surface, URP materials, corrected normals and vertex clustering. Close-up palms/fingers retain finer geometry. There are 25 weighted bones and separate body/head skinned renderers. Mesh assets are cleared and repopulated through Unity mesh setters when rebuilt, preserving GUIDs while avoiding stale skinning buffers.

The resulting meshes have 121,701 triangles (Bellhop, source 414,199) and 109,949 triangles (Housekeeping, source 348,984). This is a playable cleanup of the supplied stylized prototypes, not a hand-retopologized, UV-baked production character. Movement uses the procedural rig rather than a Humanoid Animator or imported animation clips. Only one selected avatar is active.

Rebuild through **Hotel Together > Characters > Build playable employees**. Generated meshes/materials/portraits are under `Assets/HotelTogether/Art/Employees`; prefabs under `Assets/HotelTogether/Prefabs/Employees`. The scene/player setup is automatic. Backup scene and original player script: workspace `tmp/employees/HotelLobby-before-employees.unity`, `tmp/employees/HotelPlayer-before-employees.cs`.

## Selection and first person

The existing Esc menu offers Bellhop and Housekeeping portrait cards. Click a card or use Left/Right arrows, then Play. The employee ID is remembered through a dedicated PlayerPrefs key; missing/unknown IDs fall back to Bellhop. Changing employee while reaching, holding, pushing or seated is blocked. Start Over preserves the selected employee.

`HotelEmployeePlayer` owns selection/avatar lifetime; `HotelPlayer` retains movement, physics and interaction authority. The original CharacterController and environment collision remain in use. The local head renders shadows only so it cannot cover the view. The camera is offset 0.20 m forward within the controller, near clip 0.035 m, and downward pitch extends to 88 degrees. A local body offset/posture adjustment clears the bulky uniform from the downward legs view. Standing camera configuration is restored after seating/reset.

`HotelEmployeeRig` provides distance-driven stepping, ground-normal foot IK, arm IK and finger curl. Foot contact uses nearby non-trigger surface hits, including the stair ramps. Pose timing tolerates slower frames and ignores teleport displacement. It adds no avatar colliders and no movement root motion.

E on a pickup begins a 0.24 second reach, then the existing physics carry system takes the object if it remains valid/in range. Q cancels a pending reach or drops a held item; Space throws. Larger items use both hands; carts use their existing handle targets. At the user's request, interactions now use the original `HotelBulkyHand.bytes` seamless cartoon hand/arm meshes and finger morphs, with skin color following the selected employee. The employee body/legs stay visible. While these hands are visible, a cached instance-only body mesh excludes arm-weighted triangles to avoid duplicate arms; the complete body mesh returns after the hand fade. Source meshes and prefabs are unchanged. The cached interaction mesh is destroyed with its avatar. Carry distance remains 0.45 m. Menu fonts are initialized by the UI rather than overwritten by player startup; only runtime-owned fallback fonts are destroyed.

## Validation

Play Mode checks used the actual player and Input System keyboard events. Both employees moved, animated their feet, reached/picked up, rejected switching during reach/hold, restored item layers/gravity on throw, canceled a pending pickup, and entered/exited seats. Baked body vertices remained finite; local heads remained shadows only and avatar collider counts were zero. Standing mesh floor minima were approximately 0.0001 m / 0.0017 m, and stabilized wrists met the computed grip targets.

Both employees completed ground-to-upper-to-roof and return routes in stair cores A and B, reaching 4.525/9.025 m, with zero side contacts and grounded completion. Reports: workspace `output/employee-runtime-validation.json`, `output/employee-stair-validation.json`; additional two-hand/cart/menu/persistence checks in `output/employee-additional-validation.json`.

Additional checks passed: a wide parcel used both hands with stabilized grip targets, cart pushing/release did not create legacy-hand errors, Right Arrow changed the menu selection, and a newly initialized selection component restored the saved employee. The test restored the user's prior preference. A final fresh Play Mode pass confirmed the bundled LilitaOne font, exactly one avatar and no duplicate legacy hands. Actual scene images: workspace `output/hotel-employee-selection.png`, `output/hotel-employee-legs.png`, `output/employee-bellhop-holding.png`, `output/employee-housekeeping-holding.png`.

No additional UPM packages or Higgsfield credits were used. No standalone build or multiplayer validation was run. The project's existing atmosphere scene-reload exception and synchronous Pipeline timeouts are separate from this feature.

Original-hand restoration was checked in Play Mode on both employees: a small parcel displayed one seamless hand, a wide parcel displayed two, and a cart displayed two. Hand vertices remained finite, the instance body mesh switched during interaction, and dropping restored the complete mesh and hid the interaction hands. Report: workspace `output/legacy-hands-validation.json`. A delayed background-frame sample missed the short initial reach on Bellhop; Housekeeping's reach was captured. These checks replace the earlier assumption that no legacy hands are created.
