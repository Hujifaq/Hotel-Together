# Front parking and arrival court — 2026-10-11

Implemented the approved parking concept in HotelLobby while preserving the hotel building and existing 18 m entrance apron.

- 20 spaces: 18 standard bays (2.8 × 5.3 m), 2 blue accessible bays (3.4 × 5.3 m) with 1.5 m striped transfer aisles.
- 6.5 m central approach, 6.2 m parking maneuvering aisle, clear central arrival/drop-off area, zebra crossing and 2.6 m frontage pedestrian walk.
- Limestone curbs and wheel stops, lawn islands, trees and shrub borders, low bollard lights, directional arrows and approach centre line.
- Five static stylized sedan models built from shared generated mesh assets, painted bodies, glazing, tyres, alloy wheels and lamps. Vehicle driving is outside this scene-art task.
- Existing runtime garden now omits trees/shrubs and its straight entrance path where the parking court occupies the forecourt; the surrounding landscape remains. It also clears the extended centre approach through the outer tree line.
- Disabled the old invisible one-metre collision barrier at the entrance-apron edge (Structure collision 121 / Collision 0), which blocked access from the new forecourt.

## Regeneration and assets

Editor menu: Hotel Together > Environment > Build front parking and arrival court. Tool: Assets/HotelTogether/Editor/HotelParkingBuilder.cs. The single root is Hotel front parking and arrival court; regeneration replaces that root. Combined static meshes and shared materials are saved in Assets/HotelTogether/Art/Parking with stable asset identities. Road, curbs and trunks use mesh collision; cars have box collision. Scene changes are grouped for Undo.

Backup: workspace tmp/parking/HotelLobby-before-parking.unity. The builder rebuilds the garden and saves HotelLobby. Use the menu in Edit Mode after normal script compilation, as it dispatches the current Editor assembly.

## Evidence

- Regenerated the court and confirmed one root, five cars, no missing scripts, meshes or materials.
- Physics checks: all sampled approach/apron positions from z12 to z80 have ground support; player capsule and vehicle-sized approach box checks are clear; both transfer aisles are clear; zero parked-car penetration above 2 mm after adjusting clearance to wheel stops.
- Play Mode: the existing CharacterController moved 38 m from the approach to the entrance apron, with zero side contacts and remained grounded after opening the old perimeter barrier.
- No compilation failure; zero runtime Console errors during the focused Play Mode check. Existing warning remains. No full executable build was run.
- Actual Unity preview: workspace output/hotel-parking-built.png. Earlier concept and original render: output/hotel-parking-concept.png and output/hotel-parking-before.png.
- Reports: output/parking-validation.json and output/parking-play-validation.json.
- Follow-up lobby entrance repair: tested the actual player from inside the lobby at z4.4 through the automatic glass entrance to z52 and back to z4, with zero side contacts. See LobbySlidingEntrance.md and output/lobby-door-*.json.

## Parked car glare adjustment

The parked cars have no Light components. A close view showed bright specular patches on paint and glass with smoothness 0.6/0.8. Reduced paint/glass/alloy smoothness, disabled their direct specular highlights and environment reflection flashes, and separated all ten headlamp lenses from the bollard material. Parked headlamps now use a non-emissive muted lens material. Other lighting and post-processing settings are unchanged. The parking builder applies these settings on regeneration; the Reduce parked car glare menu updates the existing court without rebuilding geometry.

Compared matching 1400 × 900 captures in Play Mode: the white highlight on the blue car and bright glass reflection are removed. Evidence: workspace output/car-glare-before.png, car-glare-after.png and car-glare-validation.json. Compilation succeeded. Temporary review camera and imported screenshots were removed.

Follow-up: the user still reported a whole white windshield after the initial material adjustment. Inspected all five cabin renderers: they shared the corrected Lit material, but their mesh had eight zero-length normals and inconsistent normals caused by opposite triangles sharing vertices. Rebuilt separate front/back vertices (48 vertices, zero invalid normals), explicitly assigned outward normals, and changed the parked car glass to opaque dark blue URP Unlit so direct lighting and sky reflections cannot bleach the pane. This is a static tinted representation without live glass reflections. Both the rebuild and Repair parked car windshields menu preserve the correction. Play Mode captures at the reported position and directly in front of the terracotta car show solid dark panes without the previous triangular shading. The exact white-pane symptom was not reproduced in the initial capture; the repair removes both the malformed mesh and the glass lighting path. Evidence: output/windshield-before.png, windshield-after.png, windshield-front.png and windshield-validation.json.
