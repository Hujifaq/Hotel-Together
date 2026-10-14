# Firearms and lethal combat

Added 2026-10-14 from the user's Polygonal Modern Weapons Collection 1 GLB. All 28 complete assembled firearms/variants are separated without cutting triangles, normalized to game scale, and authored as standalone pickup prefabs. The source collection, extracted models, shared atlas, mesh assets and attribution are retained in `Art/Firearms`. Knives, grenades and loose attachment strips remain in the source collection; they are not firearm pickups.

## Playing

The armory is built automatically in ground-floor storage room G02 (world center x=12, z=-31.75; entrance near x=12, z=-21). All weapons are physically placed on six shelves, with a refill crate alongside them. Click LMB to pick up and keep equipped. LMB fires; hold for SMGs/rifles. R reloads an equipped firearm. Q drops and RMB throws. With no firearm equipped, R still resets the world; the pause menu also has the restart action. Click the ammo crate while equipped to replenish reserve ammunition.

Pistols, SMGs, rifles, sniper/marksman rifles, shotguns and the rocket launcher have distinct damage, rate, spread, magazine size, reserve size and reload time. Shotguns trace eight pellets; rockets travel at 24 m/s and explode with a bounded radius and solid-cover checks. Other shots are hitscan. Camera aim and muzzle obstruction both matter, including point-blank muzzle contact. Head hits do extra damage. Doors, switches, taps, bells, ammo crates and elevator buttons retain contextual LMB use while carrying a firearm; aiming at customers fires instead of serving them.

Weapon animation uses spring recoil, flash/light, shell ejection, tracers, impact bursts, synthesized gun/reload/dry-fire audio, support-hand grip and reload hand movement. Both the first-person hands and employee IK consume the gun's shared hand pose. Gameplay damage is independent of the animation. Reload commits only at completion, pauses with gameplay, and cancels on dropping without spending reserve rounds. Empty weapons auto-start a reload when reserves remain.

## Health and death

`HotelHuman` remains the authority for guest health; `HotelZombieBot` owns zombie health. Floating bars are projected above actual heads, hidden at full health, occluded by solid world geometry, and show a trailing damage chip. Shots produce a hit marker and a distinct kill marker.

Lethal firearm damage kills a customer immediately, unregisters them from survivor targets/service/room occupancy and enters a permanent eleven-body death ragdoll. Living customers react and seek cover through the existing survival behavior. Zombies use an eleven-body ragdoll on death. Dead NPCs stop navigation and cannot be revived by medicine, grabbed back to life or award repeated casualties/kills. Corpses clean up after 20 scaled seconds and world reset removes them immediately. The existing melee downed/rescue window remains separate.

## Integration and evidence

`HotelArmory` is attached by the existing hotel controller; no scene save, manual wiring or new package is required. `HotelFirearmBuilder` rebuilds resources from the retained source assets. Source credit and CC BY 4.0 attribution are in `Art/Firearms/ATTRIBUTION.md`.

The isolated Unity QA project tests every supplied prefab, actual LMB routing, ammunition/cadence, customer damage, rocket travel/explosion, reload pause/cancellation, empty/resupply behavior, solid-wall and point-blank shots, floating bar visibility, lethal state, one-time death rewards, no corpse recovery and bounded corpse physics. Layout checks use copied HotelLobby colliders and saved guest navigation, verifying all 28 pickup rays and the route into storage. A native live HotelLobby armory render verifies meshes/materials and placement. Existing guest physics, mouse controls, maintenance and pause regressions are checked separately.

Reports, test sources and the preview are in the workspace's `output/firearms-qa`. Headless pickup completion uses the existing pickup API because it cannot capture a native mouse. Hardware feel, long gameplay sessions and target-platform performance remain playtesting work. This feature is local single-player; it adds no multiplayer authority or persistent inventory.
