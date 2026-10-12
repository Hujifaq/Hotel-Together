# Lobby sliding entrance repair — 2026-10-11

The entrance glass was static geometry with separate blocking colliders and no door interaction. Before the repair, a 1.7 m CharacterController stopped at z6.60 when trying to leave the lobby.

The existing glass and metal edges are reused in two sliding leaves under `Lobby automatic sliding entrance`. Each leaf has its own moving collider and uses the existing HotelItem door animation and collision checks. Only the two original panel colliders (Structure collision 77/78) are disabled; adjacent facade collision 75/76 remains enabled. The panes sit slightly ahead of the fixed glazing and frame so they can slide sideways without clipping it.

HotelAutomaticEntrance opens on approach from either side, holds open while the player occupies the entrance, and closes after 1.8 seconds away. E requests opening through the existing player interaction. Other doors retain their normal toggle action.

Reapply in Edit Mode with Hotel Together > Environment > Repair lobby sliding entrance. The tool replaces its generated root and keeps original visual sources for rebuilding. Backup: workspace tmp/lobby-door/HotelLobby-before-sliding-entry.unity.

Validated in Play Mode with the actual HotelPlayer CharacterController:

- Automatic opening from inside: both leaves reach x ±3.06.
- Walked from lobby z4.4 to parking approach z52 with zero side contacts.
- Door closed when the player was away; returning from z52 to z10 triggered opening from outside.
- Walked back through the entrance to lobby z4 with zero side contacts.
- Door remained open with the player at z7.18 in the doorway.
- The player's Use method successfully requested opening; fixed facade colliders stayed enabled.
- Scripts compiled successfully; the current Console reported zero errors during the focused Play Mode checks. No executable build was run.

Evidence: workspace output/lobby-door-before.json, lobby-door-outbound.json, lobby-door-inbound.json and lobby-door-validation.json.
