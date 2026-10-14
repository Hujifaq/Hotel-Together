# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project and environment

- Analyzed 2026-10-11, commit `11535abae38e88d726d2f56d14f549a165608067`.
- Root: `C:\Users\gram\Desktop\Game Design\Hotel-Together`.
- Unity 6000.6.5f1, revision 3ff58d469c8a (`ProjectSettings/ProjectVersion.txt`).
- URP 17.6.0; pipeline assets are assigned per quality level (`QualitySettings.asset`).
- Input System 1.20.0, exclusively enabled (`activeInputHandler: 1`); HotelPlayer reads Keyboard and Mouse directly.
- Other relevant packages: glTFast 6.20.0, AI Navigation 2.0.15, Pipeline 0.8.0-exp.1, Test Framework 1.8.0, uGUI 2.6.0.
- Desktop keyboard/mouse controls are confirmed; shipping platform is unspecified.

## Structure and architecture

- `Assets/HotelTogether/Scripts`: player, item interaction, graphics, city atmosphere and migration data.
- `Assets/HotelTogether/Editor`: automatic graphics and environment setup.
- `Assets/HotelTogether/Models`, `Art`, `Prefabs`, `Resources`: imported models and visual resources.
- `SourceAssets`: source material retained separately from imported Unity assets.
- No custom assembly definitions or first-party test scripts were found. Runtime and Editor code use Unity's default assembly boundaries.
- Confirmed MonoBehaviour architecture: HotelPlayer handles movement, pickup/throw, carts, seats, doors and UI; HotelItem handles interactive object state.
- Static graphics and city bootstrap classes subscribe to scene loading and create their systems for HotelLobby.
- Runtime namespace `HotelTogether`; Editor namespace `HotelTogether.Editor`. Existing scripts vary from compact formatting to expanded formatting, and commonly use public serialized fields.
- README reports unfinished online play, passenger elevator and some job systems. Multiplayer operation was not verified.

## Scenes and startup

- Enabled build scene: `Assets/HotelTogether/Scenes/HotelLobby.unity` (`EditorBuildSettings.asset`).
- SampleScene is also present but is not the configured startup scene.
- HotelLobby is stored using Git LFS. Clone completed and `git lfs fsck` passed.
- Controls from README and HotelPlayer: WASD move, Shift run, E interact, Q drop, Space throw, R reset, Esc menu.

## Validation and tooling

- Unity CLI and Unity 6000.6.5f1 are installed; project is registered in Unity Hub. Pipeline connection is verified ready. HotelLobby is loaded and active, with 16 scene roots.
- No additional MCP provider is needed for initial import. Do not install a second bridge without a specific reason.
- Initial package resolution, asset import and script compilation completed. Play Mode and gameplay have not been tested.
- No first-party EditMode/PlayMode tests or CI configuration were found in the tracked file inventory.
- Import log: `../output/hotel-together-import.log`. Existing compilation warnings include obsolete APIs and unused fields.
- Opening the scene revealed repeated GenerateMips errors in HotelGraphicsQuality. Changed autoGenerateMips to false because Reflect explicitly generates mipmaps after rendering.

## Constraints and evidence

- Preserve Assets with their .meta files, Packages, ProjectSettings and SourceAssets as described in README.
- Library, Temp, Logs and builds are generated and excluded from version control.
- Opening HotelLobby can run existing InitializeOnLoad Editor scripts which adjust graphics resources and mark the scene dirty. Inspect resulting changes before saving or committing.
- Files inspected: README.md, README-TH.md, .gitattributes, Packages/manifest.json, ProjectVersion.txt, EditorBuildSettings.asset, ProjectSettings.asset, GraphicsSettings.asset, QualitySettings.asset, HotelPlayer.cs, HotelItem.cs, MigrationData.cs, HotelGraphicsBootstrap.cs, HotelCityAtmosphereBootstrap.cs, HotelGraphicsSetup.cs.

<!-- unity-onboarding:generated:end -->

Bathroom scene integration now includes 16 upgraded guest bathrooms, supplied door leaf/frame assets and 34 E-controlled water fixtures. Focused Play Mode interaction and clearance checks are documented in BathroomUpgrade.md.

Playable employee selection now extends HotelPlayer with HotelEmployeePlayer and HotelEmployeeRig. The supplied Bellhop/Housekeeping prototypes have repaired skinned prefabs, first-person body/legs, ground-aware stepping and reach/grip animation. Selection is integrated into the existing Esc menu and remembers an employee ID. Implementation and Play Mode validation are documented in PlayableEmployees.md.

HotelGuestSimulation now coordinates original tourist customer models, staggered arrivals, reception check-in/checkout, unique room and seat reservations, meals, lounge drinks and nighttime towel/sleep routines. HotelPlayer E interactions deliver physical service supplies using the restored original hands. The cycle is 180 seconds daytime and 180 seconds nighttime, with a guest service HUD and runtime lighting transitions. Navigation is baked for a dedicated Hotel Guest agent type, with routes to all 16 rooms and 42 seats. Setup and validation are documented in GuestSimulation.md.

Night defense now extends that cycle with increasing zombie waves, player/customer health, bat combat/blocking, medicine rescue, customer refuge/escort decisions and destructible barricades. HotelZombieDirector owns the night quota and human/enemy registry; 30 defense openings use physical blockers, carving and passable-only links. Twelve ground-floor front glass panes have independent collision/breakage, and existing first-aid models now supply medicine. Dedicated weapon/stock scripts preserve prefab serialization; generated meshes update in place. Details and validation are in NightDefense.md.

Player service jobs now extend customer check-in with owned luggage delivery and laundry return. Maximum guests is 12 with 14-second arrivals. Enlarged original gold trolleys have four persistent cargo slots; reusable three-place trays serve meals; existing washers have timed owner-tagged cycles; 16 room receivers verify delivery identity. Service lift panels transfer staff and pushed cargo between ground and guest floors. HotelServiceJobs owns assignments/reset and presents a paged job board; HotelPlayer still owns carrying, pushing, doors and original hands. Details are in PlayerServiceJobs.md.
Lobby cafe and guest hydration extend the existing lounge routine with a reserved water queue, physical cup filling and drinking, pause/night/danger cancellation, and original imported rolling suitcase geometry shared between arriving visuals and tagged delivery bags. Decorative floor luggage was removed and four trolley cargo slots now accommodate the thicker cases. See LobbyCafeAndWater.md for setup and validation.
Operational elevators now replace the authored service teleport panels with two physical cabins serving all three floors. HotelElevator owns landing/cabin door interlocks, calls, destinations and continuous cargo transport; HotelGuestElevatorPassenger integrates sequential queuing/boarding/exiting into guest and survival routes. Guest/service prefabs, source shaft openings and dedicated navigation are authored by HotelElevatorBuilder. Details and Play Mode evidence are in OperationalElevators.md.

HotelLobby now has metre-scaled saved PBR wall finishes: warm limewashed plaster, painted accents, walnut surfaces and bathroom stone walls. The Editor-only HotelWallFinishBuilder updates selected render materials and UV meshes while preserving source geometry, collisions and navigation. See WallFinishes.md for authoring and validation.

Playable housekeeping now integrates original mops, housekeeping carts, sorting bins and detergent with timed mopping, real litter disposal, finite mop/waste capacities and source-generated messes. Service guidance shows trolley room manifests, two specific meal requests and the complete tagged linen / detergent / washer / return cycle. See PlayableHousekeeping.md for controls and validation.

Player experience update (2026-10-13, based on `4728177`): HotelPlayer now has a partial menu/settings implementation in HotelPlayerMenu.cs. It owns global single-player pause, focus-loss pause, persistent mouse/audio/camera options and GameplayInputEnabled, consumed by guest simulation and combat. HotelPlayerMenuValidation provides isolated Play Mode checks; the earlier statement about no first-party checks describes the initial import. See PlayerExperience.md for validation scope and limitations. Pipeline was not reachable from the existing Editor during this update; compilation used Unity's Roslyn compiler and the project's compiler references.

Animation update (2026-10-13): HotelPlayerAnimation.cs owns cosmetic continuous motion and transient gestures, shared by the seamless hands and HotelEmployeeRig IK. Existing game components trigger work, combat, healing and damage poses; gameplay remains authoritative. Employee sit/run/work/down transitions and NPC activity poses use scaled animation time. HotelGuestVisual/GuestBot/GuestSurvival blend seated/sleep/cover poses, and HotelZombieBot blends recoil/death. No Animator controller, scene wiring or additional package is required. Isolated rig/mesh checks and Play Mode hand/physics checks are documented in PlayerAnimations.md; full HotelLobby and performance validation remain outstanding.

Customer physics update (2026-10-14): HotelGuestPhysics is attached at guest initialization and drives the existing skeleton with an eleven-body runtime ragdoll. HotelPlayerPranks adds head grabbing, releasing, throwing, shared hand targets and overhead complaint text; HotelThrownProp handles trigger/collision impacts. Guest AI yields during physics/recovery. See GuestPhysics.md for behavior and validation scope.

Maintenance update (2026-10-14): HotelGuestSimulation now owns an automatically initialized HotelMaintenanceSystem. Four staged incident types use physical tools, warning/cooldown/concurrency rules, finite extinguisher charge/refill, plumbing isolation, exact room-light state restoration and continuous emergency elevator landing. Procedural tools/stations/VFX, shared hand/employee work poses and J/HUD guidance integrate with existing carrying, housekeeping and service systems. See MaintenanceIncidents.md for validation and scope.

Mouse interaction update (2026-10-14): HotelPlayerMouseInteraction owns LMB press/hold/release priority, sticky work equipment, RMB throwing/canceling, and CharacterController jumping on Space. Pause/reset cancels pointer ownership. Existing service/maintenance APIs and procedural animation remain shared; no scene wiring or package changes. See MouseControls.md.

Firearm combat update (2026-10-14): HotelArmory initializes 28 supplied-model firearm pickups and ammo resupply inside G02 storage without scene edits. HotelFirearm owns magazines, reload transactions, firing traces/rockets, hit feedback and shared first-person/employee hand animation. Guest health remains in HotelHuman with immediate firearm death; HotelGuestPhysics gains permanent corpses. Zombies use HotelZombieDeathPhysics. HotelTargetHealthBar projects only damaged NPC health. See FirearmCombat.md for controls, attribution and validation scope.
## Current hotel economy

Equipment presentation update: maintenance equipment mounts to clear, navigable wall surfaces, original wall extinguishers fit against walls, emergency lights have supports, and world TextMesh labels use depth-tested one-sided rendering with consistent English fonts. Armory labels have physical backing plates. See [EquipmentLayout.md](EquipmentLayout.md).

Daily goals, physical customer cash payments, reputation/consequences, paid upgrades, caretaker NPCs, dawn accounting and career checkpoints are implemented. U opens management; LMB accepts cash. Runtime setup preserves the existing scene. See [HotelEconomy.md](HotelEconomy.md) for rules, pricing, persistence boundaries and validation.
