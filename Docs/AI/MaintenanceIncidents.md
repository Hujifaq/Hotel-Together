# Hotel maintenance incidents

Added 2026-10-14. `HotelGuestSimulation` owns `HotelMaintenanceSystem`; it builds its equipment automatically when HotelLobby starts. No scene/prefab wiring or new package is required.

## Play

The first fault is scheduled after 55 seconds of active gameplay. Each fault gives an eight-second warning. Later faults rotate through fire, burst pipes, elevator faults and blackouts, with an interval that decreases from 115 seconds toward a 65-second minimum as repairs are completed. There are at most two unresolved incidents; power and elevator faults cannot overlap.

The maintenance rack/refill point is near the staff-room entrance. LMB picks up and equips tools, Q drops them and RMB throws them through the existing physics system. Three previously decorative wall extinguishers are also usable. The rack adds a modeled extinguisher, adjustable wrench and insulated screwdriver.

| Incident | Player action | Service consequence |
|---|---|---|
| Fire | Hold an extinguisher, aim at the base within 4.5 m and hold LMB for 5.5 seconds. LMB at the rack refills it. | Flames, smoke, flickering light, spatial sound and nearby customer dissatisfaction. |
| Burst pipe | Hold LMB to close the valve; hold the wrench and LMB to seal the pipe; hold LMB to reopen and test. | The associated tap is isolated. The leak can create wet-floor cleanup work until shut off. |
| Elevator fault | Hold the wrench and LMB at the lift service cabinet; hold LMB to run the restart test. | New calls are refused. An ongoing trip reaches its destination continuously, then opens at a landing and lets passengers exit. Guests use another lift or stairs. |
| Power outage | Hold the insulated screwdriver and LMB to replace the failed fuse; hold LMB to test the breaker. | Interior lights go off; four emergency route lights come on. Lifts finish an existing trip on the simulated emergency drive and reject new trips. |

J shows the active fault, tool requirements, repair sequence and direction. The main HUD shows the fault/warning and stage progress. Each completed stage persists when the player stops working. Wrong tools, an empty extinguisher, excessive distance, a cart, sitting or incapacity reject work. Spray target selection checks its aim cone and solid line of sight; it cannot extinguish through a wall. Repaired sites have a two-minute cooldown.

## Integration

- `HotelMaintenanceIncident` owns warning/active/resolved states and repair stages; gameplay progress is separate from effects.
- `HotelMaintenanceSystem` owns scheduling, work selection, charge use, exact interior-light state restoration and reward counts. Rewards vary from $25 toward $12 as severity rises; customer satisfaction decreases if faults remain unresolved.
- `HotelMaintenanceModels` creates metre-scaled tool meshes, a refill rack, two fire sites, two water lines, one cabinet per lift and an electrical cabinet. Placement samples the existing guest NavMesh and checks solid clearance. New solid racks/cabinets/pipe fixtures carve navigation around themselves.
- `HotelMaintenanceTool` drives charge, nozzle aim, squeeze lever, foam particles and spatial spray audio.
- `HotelPlayerMaintenance` extends existing hands, employee presentation, tool physics, contextual E actions and HUD. Valve turning, wrench tightening, screwdriver twisting, breaker tests, nozzle sweeps and refilling use procedural pose animation. Employee torso work poses and the existing first-person fingers follow the action.
- `HotelElevator` now exposes operational/power/fault state. The normal input loop and validation share `AdvanceState` / `AdvanceTravel`; zero delta does not advance either. Emergency movement preserves the continuous transport of player, passengers and cargo.
- `HotelWaterFixture` rejects flow while maintenance-isolated. Pipe-created puddles use the existing housekeeping/mop system.

Esc freezes game time, physics, particles and work. Stopping work stops the spray sound. R and disabling maintenance restore power, water, elevator service and tool charge. Repeated initialization is guarded; particles and runtime materials/audio/textures have bounded ownership and cleanup.

## Evidence and scope

Isolated engine checks cover 43 maintenance behaviors, including wrong tools, obstruction, charge/refill, pause, staged repair, reward duplication, exact light restoration, safe emergency landing with the player aboard, restart testing, concurrency, reset and disable cleanup. Player pause/throw and customer-ragdoll regression checks run separately.

The layout check loads a copy of HotelLobby's saved transforms, BoxColliders, prefab references and baked guest navigation. The initial check placed six tools and seven incident sites, including the rack near (-2.60, .03, -10.30). This is a placement/navigation check; omitted imported render meshes are not covered by it. Native Unity tool renders verify the three new models separately using built-in materials.

Reports and preview live in the workspace's `output/maintenance-qa`. Full live HotelLobby visual, keyboard/mouse, crowded NPC route and performance validation remain outstanding. The current fire is a local incident rather than an architectural fire/spread simulation; there is no structural destruction, thermal injury or multiplayer synchronization. The blackout preserves natural daylight and emergency/incident lights. Repair animation uses the existing procedural rig rather than added mocap clips.
