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
