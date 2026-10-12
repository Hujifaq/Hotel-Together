# Hotel customer simulation V1

The two supplied JPGs are visual references. Only the tourist/customer portion is implemented: two original articulated 3D tourists with large eyes, chestnut hair, Hawaiian floral shirts, shorts, socks/sandals and rolling suitcases. Geometry and a seamless floral texture are generated locally by `HotelGuestBuilder`; no external generation credits or packages are used. The character is a stylized playable prototype rather than the illustrated reference's advertised retopology. It has a shared skinned body, animated eyelids/brows/smile, joint covers, procedural walking/seating/sleeping and a suitcase handle grip. Existing employee selection, first-person body and original interaction hands are retained.

## Gameplay

- Daytime lasts 180 seconds; nighttime lasts 180 seconds. Esc pauses the simulation together with guest movement; Play resumes it. R / Start Over resets guests, reservations, clock, supplies and service totals.
- The first arrival is after four active seconds, followed by one every 26 seconds during daytime. At most six guests are present. No new customers arrive at night. Existing customers can still be served.
- Customers walk through the automatic lobby entrance, join a spaced reception queue and wait for the player. E on the first customer assigns a unique room. Queue jumping is rejected.
- Guests go upstairs, unpack, return to a reserved dining seat, request a meal, eat, move to the lounge and request water. At night they return to their own room, request a clean towel and sleep on their bed. The following morning they return to reception to check out and then leave through the arrival court.
- Meal, water and towel pickups restock on the existing service counter near reception. Pick up with E, walk to the requesting guest and press E to deliver. Wrong supplies are rejected without consumption. Q drops and Space throws using the original physics and hands. Check-in/check-out require no item.
- The HUD shows period countdown, guest names, room numbers, satisfaction, current needs/wait times, completed services and tips. Requests also appear above nearby guests. Runtime UI uses the existing English LilitaOne font.
- Patience decreases while waiting. Unserved arrivals leave after 125 seconds. Unserved meals/drinks/towels have fallback activities after 65 seconds; satisfied service earns tips. Staff cannot take a guest's reserved seat, and guests avoid a chair occupied by the player.

## Navigation and configuration

`Hotel guests and service simulation` owns `HotelGuestSimulation` and one baked `NavMeshSurface`. Agent type **Hotel Guest** has radius 0.29 m, height 1.75 m, slope 52 degrees and climb 0.30 m. The surface uses physics colliders, a bounded hotel/arrival volume, and 0.075 m voxels. Moving doors have NavMeshModifier exclusions, with guests opening nearby doors before crossing. A non-carving capsule obstacle gives the player local avoidance. Guests use trigger capsules for targeting and NavMeshAgent movement; no dynamic Rigidbody competes with their navigation.

There are 16 configured rooms (201–216), 38 dining seats and four lobby seats. Bedroom targets are sampled at actual floor level and must have complete paths from reception. A blocked route stops and requests help; E retries it rather than teleporting through walls. Room and seat reservations belong to the simulation. Sleeping/seated agents are disabled, and return to their approach point when standing.

The daytime directional light, ambient lighting and city panorama exposure transition to nighttime over eight active seconds, and return at dawn. The panorama uses an instance property block; source environment materials are preserved. Lighting is restored on destruction.

With the night defense system installed, the zombie alarm interrupts normal nighttime towel/sleep routines. Customers seek shelter, flee breaches, and can be escorted or rescued by staff. Surviving customers resume their ordinary routines at dawn. See NightDefense.md for controls and validation.

Player service jobs increase the scene's customer limit to 12 and arrival interval to 14 seconds. Checked-in guests wait to unpack until their tagged luggage is delivered. Meal service now uses a reusable tray, and each room has a tagged laundry job. The paged job board replaces the unbounded customer HUD list. See PlayerServiceJobs.md for the full workflows and reset behavior.

Build/rebuild through **Hotel Together > Guests > Build customer simulation** with HotelLobby open and Play Mode stopped. Generated models, materials, floral texture and NavMesh are in `Assets/HotelTogether/Art/Guests`; tourist/supply prefabs are in `Assets/HotelTogether/Prefabs/Guests`. Rebuild after relevant layout/collision changes. Backup: workspace `tmp/guests/HotelLobby-before-guests.unity`. Two older static customer props are disabled rather than deleted.

## Validation and limits

All 16 bedroom destinations, all 42 seats and the arrival/reception route have complete paths in `output/guest-navigation-validation.json`. A Play Mode cycle exercised actual walking up/down the hotel stairs, queue order rejection, distinct room/seat allocation, physical pickup and meal delivery, lounge relaxation, nighttime room return/towel delivery/sleep, morning checkout, room release and departure. Runtime results are saved in `output/guest-runtime-validation.json`.

The final cycle also delivered lounge water and confirmed that a wrong item stays held. Additional checks validated menu clock pause and player seat reservation rejection. The final corrected nighttime check found the atmosphere's DontSave panorama through its hierarchy and verified sun intensity 0.18 and panorama exposure 0.07 with both periods at 180 seconds (`output/guest-night-validation.json`). Models have 25,032 body triangles each. Actual Unity renders: workspace `output/hotel-guests-models.png`, `output/hotel-guests-reception.png`. The service counter label faces the lobby, is mounted on a small plaque, and the meal is spaced away from the existing lamp.

This is a local single-player simulation. Guest progress is reset on starting a new session; it is not a persistent save or network-authoritative multiplayer implementation. Imported Humanoid clips, full facial blend shapes, payment economy and housekeeping room-cleanliness simulation are outside this version. The existing atmosphere scene-reload exception is unrelated. No standalone build has been run.
