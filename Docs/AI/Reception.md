# Reception payment and patience

Implemented 2026-10-14.

## Controls and admission

E on the first customer who has reached reception requests a physical cash offer. It reserves an available room but does not set RoomIndex, count a check-in/service, remove the customer from the queue, or start the room route. Repeated E does not create another invoice or collect from the body.

LMB on the actual cash starts its .48-second hand-transfer animation. After the unique receipt is credited, the reservation commits to RoomIndex, the hotel records one check-in and one reception service, and the customer leaves the queue for the room. The initial quote includes the reception tip; completion does not create a second invoice. The quoted rent remains fixed while that invoice is outstanding.

Unpaid body LMB is consumed by the reception prompt instead of falling through to Serve. Head LMB remains a physics grip, release lets go and RMB throws. Cash focus has priority over head grabbing. Other registered-guest service interactions and firearm controls retain their existing behavior.

Gripping immediately withdraws offered/collecting cash and releases its pending room, independently of the misconduct fine cooldown. An interrupted transfer cannot credit rent or admit the guest. Released unpaid customers return to their reception requirement and preserve time waited. Grabs/throws no longer send the queue into danger shelter; genuine prop/gun threats retain safety behavior and withdraw cash before sheltering.

Room search includes pending reservations. Direct CheckIn calls cannot bypass the settled-payment authorization. Receipt ownership, room availability, quote, queue position, health and physics state are checked before settlement. Voided notes immediately disable collisions, detach and hide before deferred destruction, so a withdrawn note cannot steal a new interaction ray.

## Waiting

`HotelGuestSimulation.checkInPatience` defaults to 120 active seconds, multiplied by the existing room-upgrade patience bonus (15% per level). `ReceptionWaitSeconds` is separate from service/activity timers and does not reset on E, an offer, a head grip, recovery or a renewed invoice. It continues during offered/collecting cash and physical grips, before the normal AI/physics early returns. Menus, pause, incapacity and genuine danger shelter suspend it.

An overhead colored bar/countdown displays reception time and WAITING / ACCEPT CASH / PAYMENT status, respecting screen bounds, distance and walls. Incapacitated/dead customers do not display this bar. The interaction badge uses E for requesting admission payment.

At expiration, unpaid cash is voided, the room reservation and reception queue slot are released, one abandonment review is recorded, and the guest heads to the hotel exit. A physically held guest is marked Leaving immediately and continues the exit route after release/recovery; it cannot rejoin reception or be admitted with a late E. Paid customers stop consuming reception patience. Registered customers' meal/service timers retain their earlier behavior.

No scene rebuild, prefab save, new dependency or save-format change is required. The new bar attaches in guest initialization. Career checkpoints remain at dawn; mid-shift NPC positions/transactions are not persisted.

## Validation

Workspace evidence: `output/checkin-patience-qa`.

- 53 isolated Play Mode checks with actual guest/cash prefabs, raycasts, engine navigation, hands and the gameplay input router. Covers body clicks, E requests, unpaid authorization, reservations, repeat E, offered/held patience, grab/recovery, partial transfer, pause, paid admission, exactly-once accounting, mid-transfer cancellation, expiry while held/offering, queue promotion, reservation cleanup and departure routing.
- Updated economy regression: 49 PASS (cash, accounting, consequences, upgrades/staff, daily settlement, reset/stale receipts). Its fixture now uses paid admission instead of direct free CheckIn.
- Existing mouse regression: 32 PASS. Existing ragdoll stability/recovery regression: 62 PASS.
- Unity compilation succeeds. Batch fixtures use the tourist's original skin and supplied cash mesh; they drive callbacks/time through APIs rather than a complete hardware mouse playthrough. Native exploratory probes were not a completed validation run: overlapping fixture actors and later the stopped main Editor invalidated their setup. Do not count those probes as passed gameplay checks.

Temporary probe sources and logs are retained. Scene/prefab assets and career persistence format are unchanged. The pre-existing atmosphere scene-not-loaded reimport diagnostic and tool evaluation timeouts are separate from these gameplay results.
