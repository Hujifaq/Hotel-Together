# Compact stairways and corner entries

The saved hotel scene uses compact switchbacks in both 4 x 6 m stair cores. Imported stair geometry and the former parallel passage are removed from the core interiors. Door openings are relocated to the front corners at ground, upper floor and roof levels; the original ground doors are reused, with copies at the upper levels. Doors open outward and retain HotelItem interaction.

Each half flight rises 2.25 m on 14 steps (approximately 0.161 m rise, 0.243 m tread, 1.79 m flight width). Full-width entrance landings are 1.4 m deep to fit the existing door frames; rear half landings are 1 m deep. The two flights have a narrow solid guarded seam rather than an open well. These final depths account for the actual door frame, rather than using the smaller conceptual entrance landing.

All 28 handrail segments share `Stair timber handrail`, a 52 mm square section and a 1.04 m flight-top height. Matching returns connect the outside rails around each half landing. Glass passage guards are removed.

Regenerate using **Hotel Together > Environment > Rebuild stairways and passage guards**. `HotelStairBuilder.cs` builds flights, ramps, guards, doors and landings. `HotelStairSitePreparation.cs` subtracts only the core and entry-wall volumes from imported combined meshes, preserving neighboring rooms. Retained external collider fragments are under `Stair site retained structure`, so regeneration does not delete external wall collision. Updated mesh colliders explicitly recook after each mesh change.

Backup before this layout: workspace `tmp/stairs/HotelLobby-before-compact-stairs.unity`. Generated meshes/materials are in `Assets/HotelTogether/Art/Stairways`.

Validation in Play Mode used the actual HotelPlayer CharacterController. Both cores reached approximately 4.525 m and 9.025 m and returned to ground, grounded, with zero side contacts on the tested routes. All six new corner entries passed entry and exit with zero side contacts after correcting merged upper-wall collision. Reports: workspace `output/stairs-compact-validation.json`, `output/stairs-corner-entries-validation.json`. Actual scene screenshot: `output/hotel-stairs-compact-built.png`. No standalone player build was run.

Final checks also confirmed all 28 handrail segments share one material and identical 52 mm sections, no missing meshes/materials, no glass in the rebuilt root, and sideways seam crossings are blocked in both cores (output/stairs-compact-qa.json). The fresh-session entry test used normal door animation without forcing leaf poses.

## Entrance floors and enclosure repair

The old site cut removed a 0.5 m strip along each entry wall, while the replacement threshold covered only 0.4 m. This left visible ground gaps and overlapped the front landing. The repair replaces those plates with a single continuous entrance floor and fills the remaining cut strip with adjoining floor rectangles. Frame side openings and lintels now meet the actual reused door frame.

Opaque inset perimeter walls close the surviving wall-height and join gaps without coplanar surfaces. Both roof stair headhouses have a sealed roof and 3 m enclosure height. The repair is integrated into regeneration and is also available through **Hotel Together > Environment > Seal stair entrance floors and enclosure** (`HotelStairEnclosureRepair.cs`). Pre-repair backup: workspace `tmp/stairs/HotelLobby-before-enclosure-repair.unity`.

Validation: 1,800 floor-strip samples across both cores and all three levels found zero uncovered points and zero overlapping top faces among the replacement floor pieces. Wall/roof probe rays found zero leaks at the sampled perimeter points. Actual player entry/exit tests passed all six normally animated doors with zero side contacts; both full ascent/descent routes also passed with zero side contacts. Report: `output/stairs-enclosure-validation.json`. Same-angle visual evidence: `output/stair-gap-before.png`, `output/stair-gap-after.png`.

## Original floor overlap and floor signage

The earlier floor-strip check covered replacement pieces only. Imported ground-floor meshes Node-85 and Node-81 still had top faces coplanar with the new entrance landings. Site preparation now removes those original top faces in each entrance footprint, while preserving surrounding geometry.

Both cores now have large 01 / 02 / 03 floor numbers, STAIR A/B identification and floor names on inset wall panels. Ground uses teal, the guest floor terracotta, and the rooftop olive. The 28 matching timber handrails are unchanged. Sign panels have no colliders. Text uses a static TMP font asset generated from the existing LilitaOne font; the project's bundled TMP Essential Resources provide the required shaders/settings.

Apply through **Hotel Together > Environment > Polish stair entries and floor signage** (`HotelStairWayfinding.cs`). Decoration and overlap repair also run during stair regeneration/enclosure repair. Backup: workspace `tmp/stairs/HotelLobby-before-stair-wayfinding.unity`.

Validation checked 54 entrance points across all six entries against all enabled scene meshes, including imported meshes: every point had exactly one floor top face, with zero gaps or overlaps. Actual player tests passed all six normally animated doors and both complete ascent/descent routes, grounded with zero side contacts. Reports: `output/stairs-all-floor-surfaces.json`, `output/stairs-corner-entries-validation.json`, `output/stairs-compact-validation.json`. Actual scene image: `output/hotel-stairs-floor-colors.png`.
