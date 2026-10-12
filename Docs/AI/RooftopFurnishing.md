# Rooftop furnishing — 2026-10-11

Implemented the approved rooftop proposal in HotelLobby, following the supplied third-floor plan and preserving the existing 56 × 40 m roof, stair/lift cores, service structures, railing and non-walkable skylight above the lobby.

The new `Hotel furnished rooftop` root has eight groups: garden with raised beds, trees and benches; shaded pavilion with teal sofas and wood screens; activity terrace with table tennis; barbecue terrace with two grills, preparation counter and two picnic tables; dining terrace with six tables and umbrellas; quiet lounge; equipment inside the ventilation/water reserve rooms; and main circulation paving. The roof slab material now uses warm limestone. No new active Lights or emissive materials were added.

Furniture colliders are separate from combined shared render meshes. There are 61 renderers, 58 colliders and 23 E-interactive seats. Barbecues, table tennis and service equipment are decorative models. Existing HotelItem/HotelPlayer seating is reused. Fixed Stand() to retain the player's seated floor height rather than resetting to spawn.y, which previously returned an upstairs seated player to the ground floor.

Editor regeneration: Hotel Together > Environment > Build furnished rooftop. Source: Assets/HotelTogether/Editor/HotelRooftopBuilder.cs; generated assets: Assets/HotelTogether/Art/Rooftop. Reapplying replaces only the generated furnishing root, retaining asset GUIDs and original architecture. Undo is grouped. Backup: workspace tmp/rooftop/HotelLobby-before-furnishing.unity.

Validation:

- Scripts compiled successfully.
- One furnishing root; no missing material or mesh references.
- No penetration between new furniture/planter colliders above 2 cm; no new collider intersects the skylight footprint.
- Capsule clearance checks passed along the five main paths. Main paving ribbons are 3.8 m within the nominal 4 m route allocation.
- Actual player CharacterController traversed paths of 27, 26, 13, 14 and 52 m in Play Mode; zero side contacts and grounded at every endpoint.
- Rooftop seat Use/Use test: player y9.025 both seated and standing, controller re-enabled after standing.
- Existing ground-floor seat retained y0.025, upstairs seat retained y4.525 after standing.
- Visually inspected the actual Unity aerial render; removed narrow deck seams that produced distant moiré. Temporary review cameras and imported screenshots were removed.
- No full executable build was run. The path checks cover the rooftop; they do not claim an end-to-end walk from lobby through both staircases or a working passenger lift.

Evidence in workspace output: hotel-rooftop-before.png, hotel-rooftop-after-concept.png, hotel-rooftop-built.png, rooftop-validation.json and rooftop-play-validation.json.
