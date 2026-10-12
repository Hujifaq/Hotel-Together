# Night defense V1

The customer simulation's existing 180-second daytime / 180-second nighttime cycle drives the defense system. Zombies spawn only at night; remaining active enemies are removed at dawn. Night quotas grow 3, 5, 7, 9… with a safety cap of 40 total and 16 active enemies. Spawns are staggered from five arrival-court positions after a 12-second warning. The shared Hotel Guest NavMesh agent type is reused; no packages or generation credits were added.

## Characters and behavior

The supplied reference's zombie has been modeled as a green stylized character in a torn pink bathrobe with yellowed eyes, uneven teeth, skin marks, bare toes and reaching hands. It uses the existing articulated sculpt pipeline with separate zombie materials and a procedural shuffle/attack pose. This is a local prototype, not an imported Humanoid animation/retopology asset.

HotelZombieDirector registers the player and spawned customers as HotelHuman targets. Enemies choose nearby living humans, approach an accessible entrance, attack its boards/structure, cross a breach and chase humans through the hotel. Closed shelter doors can also be attacked. AI decisions run on a cadence and reject unreachable paths. Damage needs physical line of sight; trigger sensors do not block it. Defeated zombies stop attacking, fall visually and despawn.

At the night warning customers stop service routines, release seats/queue slots and head for their room or an upstairs refuge. They close their shelter door and remain behind intact protection when enemies approach. If protection fails they seek another refuge. E asks a healthy customer to follow staff after nearby enemies are cleared. Downed guests request rescue, stop moving and have a 45-second recovery window. E plus one medicine revives a nearby, visible guest. At dawn surviving customers resume normal routines/check-out. The same player health system supports blocking, self-healing, downed state and restart.

Late-registering customers also receive the night alarm. Stranded outdoor guests approach an unboarded doorway and open it only when no enemy is near the approach. Refuge samples must match floor height and have complete paths. Enemies pursue reachable outdoor humans directly and may attack an exit from inside when a blocked exit separates them from their target.

## Controls and supplies

- F: equip the bat through the existing pickup system and original seamless first-person hands. Q: drop; Space: throw.
- Left mouse: strike (bat 28 damage; bare hands 9), with range, line-of-sight, cooldown and stamina checks. Right mouse: frontal block, using stamina and reducing damage.
- H: spend medicine to heal/recover. E on a fallen customer: rescue. E on a healthy sheltered customer: escort.
- Aim at a protected door/window: B adds a plank, N removes one, T repairs the structure using two wood. E on a window also builds; doors retain normal E interaction when not boarded. Nighttime lobby automatic opening is disabled; E can deliberately open an unboarded entrance.
- E on the preparation rack near the staff corridor collects wood; E on the previously installed first-aid cabinets collects medicine. Inventory caps are 30 wood / four medicine. Initial personal supplies are eight wood / one medicine; rack stock 24 wood / three medicine. Dawn replenishes stocks within caps.

HUD displays health/stamina, wood/medicine, current wave, spawned/alive enemies, rescued/lost guests, kills, aimed opening durability and controls. Existing employee choice and guest service UI are retained.

## Physical defenses and persistence

There are 30 openings: lobby glass entrance, 12 individual ground-floor front panes, rear delivery entrance and 16 guest-room shelter doors. Four planks fit each opening, each with 45 health. Wood absorbs damage before glass/door structure. Broken glazing hides its own renderer and disables its own collider. A passage link is active only when an opening is passable; physical blockers and NavMesh carving stop entry while protected. Per-pane colliders replace the two broad front-wall collider strips, preserving adjacent panes. Existing side facade walls remain solid.

Only undamaged planks can be reclaimed as reusable wood. Damaged planks are discarded on removal. Generated model meshes are updated in place to retain GUIDs and avoid stale GPU batch references during rebuilds.

Original moving door/glass assets and materials are retained. Runtime breakage and boards reset with R / Start Over and are not authored into the saved scene. A dedicated baked navigation asset is stored under `Art/Defense`. Preparation crates, bat, nails and planks are generated locally. Weapon and stock components have dedicated MonoBehaviour files so Unity serializes them correctly.

Build/rebuild: **Hotel Together > Defense > Build nighttime zombies and barricades**, with HotelLobby open and Play Mode stopped. Backup: workspace `tmp/zombies/HotelLobby-before-defense.unity`. Runtime scripts are in `Assets/HotelTogether/Scripts`; builder in `Editor/HotelZombieBuilder.cs`; equipment/enemy prefabs in `Prefabs/Defense`. Model meshes/materials reuse the guest sculpt asset pipeline under `Art/Guests` with zombie-specific names.

Validation reports and actual Unity images are in workspace `output`. No standalone build, networking, persistent campaign save, gun system or full ragdoll simulation is included. Existing atmosphere scene-reload errors are separate from this feature.

Core Play Mode validation (`output/zombie-runtime-validation.json`) passed daytime spawn rejection; quotas 3/5/7; unique customer check-in; plank damage absorption; glass/physical collider removal and repaired restoration; viable breach passage; actual zombie entry and player damage; original-hand bat pickup and 28-point strikes; enemy defeat; customer shelter/closed door; guest targeting, downed state, medicine rescue and escort movement; mouse blocking; dawn cleanup/resumed customer routines; and automatic spawning of five enemies on night two. Prefab reload inspection found a valid weapon component, three stock components, 30 passage links and zero missing scripts.

The direct customer scenario (`output/zombie-customer-attack-validation.json`) passed safe entry/shelter, an enemy physically crossing broken front glazing, customer targeting, shelter-door assault, protection while intact, and actual customer damage plus attempted escape after the door failed. No new runtime feature errors were reported during the final runs.

Outdoor player pursuit and actual damage also passed (`output/zombie-outdoor-validation.json`).

An isolated input test (`output/zombie-block-validation.json`) confirmed right-button blocking reduces 20 damage to six. Synthetic mouse events must be processed and asserted immediately: waiting a rendered frame in an unfocused Editor may clear the simulated button.

Final visual inspection caught null mesh overrides retained by the zombie prefab variant from an earlier rebuild. The variant now explicitly references its saved body and suitcase assets, and the builder restores those references on subsequent rebuilds. Actual Unity renders: workspace `output/hotel-zombie-model.png` and `output/hotel-zombie-defense.png`.
