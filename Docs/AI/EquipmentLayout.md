# Equipment layout and world labels

The maintenance rack, lift service cabinets, electrical cabinet and pipe repair sites now use physical wall surfaces with a reachable front work area. Mounting checks the wall plane across both edges, navigation, cabinet clearance and the player's selection line. Coplanar wall segments are accepted; moving cabins, doors and furniture are excluded. When no usable wall exists, equipment has a floor support and faces its work area. Emergency lamps attach to a wall or ceiling, or receive a physical stand.

The maintenance rack faces into the corridor, with tools parented to its orientation and matching reset/home rotations. Lift cabinets sit beside the lift doors rather than on freestanding posts in the corridor. The three original wall extinguishers are brought to a 1cm gap from the wall. Pipe clamps/supports make their placement explicit; mounting behind a washbasin is rejected.

All runtime TextMesh labels use the existing URP world-text shader with depth testing and back-face culling. English labels use LegacyRuntime; non-Latin fonts are preserved. Shader changes keep labels from showing through walls or displaying mirrored text from the back. Labels have viewing distances of 8m for maintenance, 9m for armory and 12m by default. Health bars and screen UI remain separate.

Armory shelf names and ammunition instructions have physical identification plates. Header, shelf, cabinet and extinguisher text sizes fit their backing surfaces. Shared font materials track dynamic atlas rebuilds and are released when no labels use them.

Validation: native HotelLobby inspection passed 131 layout/label checks after a fresh Play Mode start, including all five repair-site approach/selection tests, both wall-mounted lift cabinets and all three rack tool pickup/reset checks. The actual GPU render showed 2333 visible text pixels, zero behind an opaque blocker and zero from the back. Maintenance regression passed 43 checks; armory placement/pickup/navigation passed 30 checks. Native previews and reports are in workspace `output/layout-polish-qa`.

The fixes are in the runtime builders; no scene rebuild or scene save is needed. The editor already has unrelated city-atmosphere scene-load errors and deprecated-API warnings, which are outside this placement change.
