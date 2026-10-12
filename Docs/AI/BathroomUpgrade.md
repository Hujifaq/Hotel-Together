# Bathroom upgrade — 2026-10-11

- Upgraded all 16 furnished guest en-suites (201–216), retaining their existing position and rotation.
- Replaced guest vanities with the supplied Wellworth pedestal washbasin. Replaced both public restroom basins with the same model.
- Added a 1.00 × 1.08 m walk-in shower area in each guest bathroom, using the supplied rainfall shower, sage feature tile, linear drain, glass side screen, and a clear entry.
- Used individual pieces from bathroom_stuff for soap, shampoo, conditioner and toothbrush cups; added towel storage and paper holders.
- Enlarged existing guest toilet geometry from 0.72 to 1.00 scale (39% over the original, another 18% over the first upgrade) and public toilets by 10%, keeping their base on the floor and preserving HotelItem components.
- Raised bathroom partitions to 4.30 m above the floor, meeting the room ceiling, and closed the area above the doorway. Replaced the previous walnut doors with the supplied door.glb, separating moving leaves from stationary frames for 16 working bathroom doors. Press E using the existing HotelPlayer interaction.

- Added E-controlled running water to 18 washbasins and 16 showers, with falling droplets, impact splashes, expanding ripples and spatial looping audio. R reset and disabling a fixture stop its water. Effects initialize once when first used and are reused on later toggles.

## Files and regeneration

- Editor tool: `Assets/HotelTogether/Editor/HotelBathroomUpgrade.cs`.
- Menu: **Hotel Together > Rooms > Upgrade all bathrooms**. Requires HotelLobby in Edit Mode and the existing 16 furnished rooms. Reapplying replaces generated bathroom groups rather than stacking them; public toilet resizing uses a marker to prevent repeated enlargement.
- Models: `Assets/HotelTogether/Models/Bathroom/`.
- Generated reusable prefabs: `Assets/HotelTogether/Prefabs/Bathrooms/`.
- Materials: `Assets/HotelTogether/Art/Bathrooms/`.
- Original supplied shower retained in `SourceAssets/Bathroom/bathroom_shower-original.glb`. The Unity copy omits an unused UV2 normal map unsupported by URP; the adapted shower uses a chrome material.
- Before-change scene copy: workspace `tmp/bathrooms/backup/HotelLobby-before-bathrooms.unity` (outside repository).

## Validation

- Unity 6000.6.5f1 imported the adapted shower with zero importer reports; scripts compile successfully.
- All 16 rooms contain one new basin, one supplied shower and one working privacy door.
- Checked missing scripts, meshes and materials in generated groups: zero.
- Capsule checks with player-sized clearance at entry and shower access: zero blockers in all rooms.
- Verified every door opens outward and returns to its exact closed pose.
- Representative Room 201 door also animated correctly in Play Mode. No Console errors during this test. This does not claim a complete gameplay test of the hotel.
- Inspected a Unity-rendered room preview. Exported preview and JSON QA report are in workspace `output/`, outside the repository.
- Existing unrelated Unity compilation warnings and automatic graphics setup remain separate from this change.

## Latest door and water validation

- Supplied door source: `Models/Bathroom/door.glb`. `DoorLeafPrepared.glb` and `DoorFramePrepared.glb` exclude a stray camera mesh, straighten the posed leaf, and normalize its proportions for the bathroom opening.
- Runtime component: `Scripts/HotelWaterFixture.cs`, integrated with HotelItem reset and HotelPlayer interaction/HUD.
- Water audio: `Audio/TapRunningWater.wav` and `Audio/ShowerRunningWater.wav`, generated loopable noise/droplet textures; materials and radial droplet texture in Art/Bathrooms.
- Checked all 16 enlarged guest toilet colliders against generated solid bathroom colliders using ComputePenetration: zero overlaps above 2 mm. Entry and shower capsule clearance checks passed all rooms; opening doors leaves frames stationary.
- Play Mode validation through HotelPlayer.Use passed all 34 fixtures: starts water and spatial audio, emits particles, clears particles and stops audio when switched off, reuses effect children, and stops on HotelItem.ResetItem.
- Representative approach rays selected the correct fixture in all 34 cases using the player's existing layer mask and trigger settings.
- After a visual test revealed radial spray, changed particle emission to a narrow downward cone; inspected the corrected Unity render and supplied door render.
- Final Unity console ground truth: no compilation failure and zero console errors. A pre-existing warning remains. A full hotel build was not run.
- Latest workspace evidence: output/bathroom-validation-v2.json, output/water-runtime-validation.json, output/water-raycast-validation.json, output/hotel-bathroom-water.png, output/hotel-bathroom-door.png.
- Additional backup: workspace tmp/bathrooms/backup/HotelLobby-before-water-and-newdoors.unity.
