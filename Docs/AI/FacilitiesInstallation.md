# Facilities installation — 2026-10-11

Installed 33 static supplied-model fittings in HotelLobby:
- 2 first-aid cabinets: staff-room entrance and upper stair A landing.
- 1 LOTO station: electrical room, beside the distribution panel and clear of the corner.
- 20 light switches: 16 inside guest-room entrances, plus staff room, kitchen, laundry and electrical room.
- 4 outlets: staff room, lobby reception, lobby lounge and laundry work area.
- 6 ceiling CCTV models: entrance, reception/lifts, service corridor and three upper common corridors/landings.

Original GLBs and generated Unity metadata are in Assets/HotelTogether/Models/Facilities. Six normalized reusable prefabs are in Prefabs/Facilities. The very small source first-aid cabinet is resized to 0.42 m high; camera height is 0.38 m; switch/outlet plates are 0.12 m. Clean plate variants are extracted separately from the source collection. Original meshes, textures and materials are retained.

Regenerate via Hotel Together > Facilities > Install supplied safety and electrical models, using HotelFacilitiesInstallation.cs. Requires HotelLobby in Edit Mode with the 16 existing guest doors. Reapplication replaces the dedicated root; placement uses mounting-surface rays and Undo. Failed installations revert scene changes. A before-change backup lives in workspace tmp/facilities/HotelLobby-before-installation.unity.

Validation: 33 fittings, zero missing scripts/meshes/materials and zero collider penetrations above 2 mm against existing scene colliders after moving guest switches away from bathroom jambs and LOTO away from the corner. Inspected Unity renders of first aid, CCTV, switch and LOTO. QA report and previews are in workspace output. These are installed decorative fittings; no switch light-control, CCTV feed or first-aid gameplay is added by this placement task.
