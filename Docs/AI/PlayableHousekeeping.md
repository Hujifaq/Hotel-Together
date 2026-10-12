# Playable housekeeping and guided service work

HotelLobby extends the existing HotelPlayer / HotelServiceJobs / HotelGuestSimulation workflow. The original housekeeping carts, mop model, sorting bins, detergent bottles, luggage and washers are used.

## Housekeeping

E on a wet spot with no mop explains the requirement. G while looking at a housekeeping cart takes its original mop; G with a held mop returns it. Hold E while aiming at the wet spot and standing within 2.25m on the same floor. Cleaning takes 3 active seconds. Releasing E or walking/looking away stops work and retains partial progress; menu pause preserves it. The original hands follow the physical mop while its head sweeps the floor. One completed wet spot consumes one of three mop uses. Hold a saturated mop and E at a cart to rinse it in the bucket.

Litter is a physical discarded cup. Empty hands and E pick it up. E at a housekeeping cart or sorting bin disposes it. Picking up or dropping it does not complete the job. Carts hold six disposed litter items; push to a sorting bin and E to empty the waste bag. A drop interaction above the bin stays visible over the pushed cart. Parked carts carve navigation; pushing disables carving. Completed cleaning pays $5 once and increments both hotel services and job completion.

Initial messes are authored on all three floors. Running showers generate nearby wet spots after 10s; sinks left running generate overflow after 25s. Guests leave actual litter when a served meal is cleared. Dynamic messes are capped at 12. Placement checks both NavMesh and physical capsule clearance, avoiding furniture that NavMesh alone does not exclude. Reset clears generated tasks, restores authored tasks, docks/rinses mops, empties cart waste and restores detergent bottles.

## Service guidance

A contextual center HUD card gives the required tool/item, next steps, destination floor, direction and distance. Loaded luggage trolleys show guest names and room numbers, including parked loaded trolleys near the player. Delivery to a room removes only its matching bags and updates the remaining route. The physical hotel map uses Lobby, 2 and 3, matching the elevator labels.

Customers order chicken rice or vegetable rice. Both have separate labeled supply points at the existing Lobby GUEST SERVICE counter beside reception. Pick up an existing tray, E on the requested plate, and E on the customer in dining. Only the matching dish is served, wrong portions remain on the tray, and staff keep the tray. Requests are visible on the job board and nearby customers. Taken tray portions immediately become eligible for restocking even if still near the counter.

Laundry: collect tagged dirty linen from the room shelf, take it to the Lobby back-right LAUNDRY room, E to load a washer, then bring an original laundry detergent bottle and E to add one dose. The machine waits until detergent is added, preserves the guest identity, runs for 20 active seconds, pauses from the menu, and yields clean linen. Collect with empty hands and return via the correct room delivery plaque on floor 2. Dirty bundles and other guests' clean bundles are rejected. Bottles have five doses; washers can hold three preloaded doses. Checkout/death cancels that guest's washer batch.

## Authoring and validation

Hotel Together > Service > Build playable housekeeping and service guidance. The Editor-only builder reuses the source mop and cart models, creates small cleaning/food prefabs and updates a saved dedicated navigation asset. It preserves elevator and wall-finish geometry. No packages or project settings are changed. Rollback scene/source is ../tmp/gameplay-jobs.

Validation artifacts: housekeeping-gameplay-validation.json, guided-service-workflows-validation.json and housekeeping-integration-validation.json in workspace output; final reports are also copied to Docs/AI/Validation. Runtime tests use real E/G key events for mop, litter and cart interactions and exercise wrong requirements, interrupted/paused cleaning, three-use saturation, rinsing, disposal, waste emptying, rewards/reset, multiple room manifests/selective delivery, wrong/correct menu choices, and the full detergent/washer/return cycle.
