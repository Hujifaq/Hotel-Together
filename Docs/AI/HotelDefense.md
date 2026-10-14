# Hotel defenses

Implemented 2026-10-14. `HotelZombieDirector.Start` installs `HotelDefenseSetup` at runtime. No scene rebuild, NavMesh asset overwrite, or prefab wiring is required.

## Player controls

- Click the barricade hammer beside the plank rack in the staff preparation room to equip it. It stays equipped after releasing the mouse.
- Aim at any architectural window, roof glazing, or ordinary hotel door. Hold **LMB** to complete the selected task. Release or look away to cancel without spending wood.
- **RMB while aiming at an opening** cycles BUILD → REPAIR → REINFORCE → REMOVE. Elsewhere RMB throws the hammer. **Q** puts it down. B/N/T select the legacy build/remove/repair tasks; hold LMB to perform them.
- Click the small bolt plate on either side of a door to lock/unlock. **L** while looking at the door is an additional shortcut. Locking first closes the door through the existing animation, then engages the bolt; an obstructed door cancels after five seconds. Locks cannot engage through planks or on a broken door/lock.
- At dawn, remove entrance planks and unlock guest routes so check-in and room access can resume.

## Gameplay rules

Each opening holds four planks. BUILD costs one wood and 1.6 seconds for a 45 HP plank. REINFORCE costs one more wood and 1.9 seconds to upgrade one installed plank to 90 HP, with a visible steel strap. REPAIR takes 2.2 seconds: two wood rebuilds the window/door structure; one wood restores one damaged plank or the 65 HP lock. REMOVE takes .9 seconds: intact planks refund their original material cost (one wood, or two if reinforced); damaged planks cannot be recycled into fresh supplies. Inventory is deducted only on successful completion, with range, focus, tools, resource limits and valid task checked again at completion. Materials remain limited by the existing rack stock/dawn replenishment system.

Nighttime zombie attacks damage outer planks, then a locked door's bolt, then the original structure. Excess damage carries through layers. A breach disables the structure's visible geometry/collision and opens its navigation link; repair restores the structure and closes the breach. Planks grow into place, damaged boards tilt/shake under attack, reinforcement adds metal, and the bolt slides with red/green indicators. The player's hands and held hammer use procedural repeated work gestures and contact sounds. No new mocap/voice assets are used.

The focused prompt displays task progress, plank HP, structure HP and lock state. A defense HUD appears while holding the hammer or at night, showing wood, boarded/locked/damaged counts, breaches and the most damaged repair priority. Existing firefighting/maintenance HUD remains on the other side.

## Scene integration

HotelLobby currently has 35 standalone glazing openings (including roof glazing), 55 door openings and 110 lock plates. Architectural glass uses the shared `CityWindowGlass` material. Glass in sliding entry leaves is protected by the whole door's defense. Furniture/display glass, vehicle windows and elevator safety assemblies retain their existing mechanics.

The installer keeps existing defense references, adds missing windows/doors, puts planks on the hotel-facing side, and builds a supported hammer stand beside the rack. Installation is idempotent. Door locks are authoritative over both manual/automatic opening and guest attempts, and navigation obstacles carve blocked passages. Guest refuge selection excludes doors the player has locked or boarded.

Some original architecture colliders were solid slabs behind the narrow lobby glass. The installer subtracts the architectural window rectangles from those runtime colliders and retains solid frame/jamb/lintel remainders. Dedicated glass and plank colliders fill the resulting holes until broken. Runtime NavMesh links are only created when navigation samples lie on opposite sides at the correct height; upstairs windows with no exterior walking surface do not provide a ground-level zombie shortcut. Roof glazing is protected but does not become a zombie entry link or walking floor.

Reset clears defense work, planks, damage, lock state and combat inventory. State lasts through the current session/day-night cycle; this change does not add persistent save-file storage. Scene/prefab assets are untouched by installation.

## Validation

Evidence is in workspace `output/defense-expansion-qa`:

- 42 isolated Play Mode checks: tool pickup/ownership, work progress/cancellation/pause, resource consumption/refunds, reinforcement, layered damage, breaches/repair, locks, AI-open rejection, remote actions, capacity limits, reset and hand poses. Headless tests drive gameplay entry points; layered damage uses the internal damage method because a headless Editor cannot retain a real captured cursor.
- 20 native main-Editor checks with a real captured cursor: real hammer/reach entry point, actual camera focus, construction, authored physical planks, daytime damage rejection, a spawned zombie prefab running its actual AI attack, entrance lock/unlock, automatic-open rejection and lock-first nighttime damage. Pickup completion/work time is advanced through entry points; this is not a synthetic full mouse-playthrough claim.
- All 35 glazing targets pass actual ray focus/reach checks. All 110 latch faces pass focus/reach. All 17 navigable glass breach links have clear physical paths after removing glass collision.
- Existing mouse interaction regression: 32 PASS. Existing firearms regression: 189 PASS. Unity compilation succeeds without new errors. The pre-existing atmosphere scene-not-loaded diagnostic during reimport is unrelated.
- Native rendered barricade/strap and defense HUD captures were inspected. Temporary test objects, resources, position and inventory were restored; no scene was saved.
