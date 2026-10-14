# Building utilities

The HotelLobby scene contains `Hotel utility infrastructure`, built by `HotelUtilitiesBuilder.Build()` in Edit mode. The builder replaces only its own root and saves the scene. Imported utility GLBs are stored in `Assets/HotelTogether/Models/Utilities`; eight supplied model sets are used for the generator, fuse boxes, wiring, conduits, valves, and pump fittings. Unused supplied models remain available for later work.

The current layout includes 80 pipe/conduit segments, with brackets against actual walls or hangers reaching the physical ceiling. Branches cover ground-floor service rooms and public spaces, upper corridors, and all 16 guest rooms. Main equipment leaves central room aisles and door approaches clear. GLB normalization uses a separate axis-aligned parent so rotated imported meshes retain the requested world dimensions. Cabinet labels use the existing depth-tested world text shader and a seven-metre display range.

G05 has a generator on a plinth, main fuse cabinet with an animated safety cover and breaker, branch panels, wiring, and emergency lighting. G06 has a finned electric pump motor, inlet/discharge plumbing, two pressure vessels, the main valve, a moving pressure gauge, and emergency lighting. Motor fans, spatial hum, gauge, and status labels follow the actual maintenance state. Menu pause stops machinery motion and sound.

## Player actions

- **Power outage / G05:** equip the insulated screwdriver from the maintenance rack; hold LMB at the main fuse cabinet to replace the fuse; hold LMB again to test the breaker and restart power.
- **Water outage / G06:** hold LMB at the main valve to isolate the inlet; equip the wrench and hold LMB to repair the pump coupling; hold LMB again to restart and pressure-test the supply. If power is still out, the last step tells the player to restore G05 first.

J shows the active quest and location. Existing procedural hand/tool animations handle valve turning, wrench repair, screwdriver work and restart testing. Work stages persist after interruptions, require the correct tools and distance, and award the usual maintenance reward once.

`HotelUtilityPlant` registers the two authored sites after the shared maintenance setup and removes the old procedural power cabinet from the live registry. The scheduler rotates through five incident kinds, including WaterOutage. Water availability is `HasWater && HasPower`: an outage isolates every real tap and disables the automatic guest water station. Guests cancel an interrupted refill and request water through the existing service flow; carried bottled water remains available. A burst pipe cannot accidentally restore a tap while the building supply is unavailable. Restoring electricity alone cannot clear an unresolved pump fault.

## Validation

Reports are in the workspace's `output/utilities-qa` directory. The disposable Unity project covers 55 maintenance checks, including wrong tools, staged repairs, power/water dependencies, duplicate rewards, and reset. Native HotelLobby checks cover both corridor-to-site NavMesh routes, real interaction focus rays, actual tool pickup/work, first-person hand pose, 34 isolated/restored taps, and room emergency lighting. Additional real-frame checks cover moving/stopped fans, motor audio, opening the fuse cover, status text and gauge pressure loss. This is single-player maintenance logic using the existing procedural animation rig; it does not model fluid pressure or an electrical network numerically.
