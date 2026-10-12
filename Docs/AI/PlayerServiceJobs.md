# Player service jobs

The existing customer simulation now creates tagged luggage and laundry jobs on check-in. HotelServiceJobs owns assignments and completion; HotelGuestBot retains customer routines and the original service/tip counters. Maximum concurrent customers is 12, up from six, with staggered arrivals every 14 seconds. Day and night remain 180 seconds each. Sixteen room delivery receivers preserve existing room assignment.

## Luggage workflow

Checked-in customers leave a physically selectable, labeled suitcase in the reception luggage area. Each suitcase retains its guest and room identity. Customers wait to unpack until their luggage arrives, then proceed to dining. The existing gold trolleys on the ground and guest floors are enlarged 20% in horizontal dimensions and 5% in height (about 1.32 × 1.96 × 0.90 m). Each has four distinct cargo positions and explicit handle targets for the restored original hands.

E while holding a suitcase and looking at a trolley loads it. E with empty hands starts pushing. G at a parked trolley unloads its last suitcase into the player's hands. Loaded cargo remains attached and stable when the trolley is released. A full trolley rejects extra suitcases without dropping the held bag. Cargo is kept separate from the original loose-object cart behavior.

Aim at a door and E to open it while pushing; E elsewhere releases the trolley. Open a room door, aim at its delivery plaque and E to deliver matching cargo. Wrong-room tags and dirty laundry are rejected. A nearby parked trolley is also checked, so staff can release it and still hand over its cargo. Only matching suitcases leave the trolley; delivery awards tips and completes the luggage job.

Service lift panels beside the existing central lift core connect the ground and guest floors. E transports the player, held items and a pushed trolley with its cargo after a 2.5-second transition. This is a functional floor transfer with an overlay, not a moving passenger cabin. The lift locks during a zombie alarm. Character movement is suspended during transport, and reset clears its lock. Trolley follow height now tracks the player's floor instead of its original home elevation.

## Tray and laundry workflow

Three reusable trays are placed beside the existing guest food/drink station. E picks up a tray; E on a food/drink supply loads it. Each tray has three places. E at a customer serves one matching portion while keeping the tray and remaining portions held. With this feature installed, meal service requires a tray; water can also be carried on it. Wrong supplies and full trays are rejected. The existing carry physics and original hand animations are retained.

Each customer's dirty linen is tagged for their room, at the room's linen shelf. Take it to one of the two existing laundry washer models on the ground floor. E loads a 20-second wash cycle. Menu pause stops the timer. E collects the clean bundle after the cycle, then E at the correct room delivery plaque returns it and awards the job reward. Washers preserve owner identity throughout the cycle.

The right-hand HUD is now a compact paged job board, showing customer requests, room numbers, luggage progress and laundry stage. J cycles pages; no more than five customers are drawn per page. Nighttime refuge/rescue status still comes from the original customer survival system. Work remains assigned during alarms; customer departure/death removes their belongings and pending washer batch. R / Start Over clears jobs, trays, washers, trolley cargo and lift transport.

## Assets and validation

Runtime scripts are under Assets/HotelTogether/Scripts; HotelServiceJobsBuilder is under Editor. Use Hotel Together > Service > Build player jobs and luggage trolleys with HotelLobby open and Play Mode stopped. The builder preserves imported trolley and washer models, creates dedicated prefabs under Prefabs/ServiceJobs and materials under Art/ServiceJobs, and saves the configured scene. A pre-change scene backup is in workspace tmp/service-jobs/HotelLobby-before-jobs.unity.

Play Mode report: workspace output/service-jobs-runtime-validation.json. It exercised five guest assignments, owned luggage tags, four-bag loading and capacity rejection, distinct stable positions, wrong-room rejection, real keyboard-driven cart movement, retained cargo after release, service lift transport of player/cart/four bags, door opening while pushing, selective room delivery and rewards, washer pause/completion/owner retention, clean linen return, three-meal loading/capacity checks, customer eating while staff keeps the tray, alarm integration, 12-customer capacity and complete reset. Compiler and missing-script checks passed. This remains a local gameplay implementation; networking and campaign persistence are unchanged.

Additional actual keyboard-driven pushing from the upper lift landing to Room 201 passed (output/service-trolley-route-validation.json), using the wide south corridor at approximately x14.9, z-22.8. The enlarged trolley needs space to turn; following a human NavMesh path that hugs the storage corner was correctly blocked. The lift now lands at x9.7, z-10.25 facing 135 degrees, with physical clearance for the trolley, rather than facing the atrium railing.

Suitcase geometry reuses the existing tourist suitcase, with unused body vertices removed into a separate saved mesh. This keeps bounds, carry positioning and hands aligned to the bag while preserving the original guest asset. Ownership labels appear on both faces. Tray carry stays level with a lower, forward pose, and its interaction card moves left to keep food visible. Context interactions display E; Q is shown only for putting down an item. Actual Unity images are in workspace output/hotel-service-luggage.png and output/hotel-service-tray.png.

The ground trolley is parked near reception's tagged luggage at x-2.6, z-1.6, clear of the original decorative suitcases. Parked trolleys carve navigation; carving is disabled while staff push them. They are excluded from the baked geometry, which is saved in Art/ServiceJobs/Hotel service navigation.asset so old parking positions do not leave permanent route holes. All 16 room routes remain complete (output/service-jobs-navigation-validation.json). Staff should move a trolley clear of a delivered room's doorway. Customers retry temporarily blocked service routes every three active seconds and resume their original destination when passage clears.

The final service cycle also passed unloading/reloading the fourth suitcase and pausing the lift transition from the menu. The existing full nighttime combat/defense/customer rescue scenario passed with the service feature and new navigation installed (output/zombie-with-service-validation.json), including three-to-five wave growth, physical breach, bat damage, blocking, rescue, escort and dawn cleanup. No new errors appeared in the final Play Mode runs.
