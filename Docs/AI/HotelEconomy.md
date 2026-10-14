# Hotel career, cash and daily management

Implemented recommendations 1 (daily goals), 2 (consequences), 3 (economy/upgrades/staff) and 5 (interaction polish). Runtime components attach to the existing hotel/player; the lobby scene does not need rebuilding.

## Playing

- Start with $250 and reputation 70/100. Daytime lasts at least 420 seconds; the existing 180-second night follows.
- The HUD shows check-ins, completed services and collected income. Initial goals are 3 check-ins, 5 services and $180 collected; targets increase gradually across completed days.
- Press E on the first guest at reception to request a room-payment offer. The guest holds the supplied cash bundle and keeps waiting; body/head LMB never requests or completes check-in. Click the visible cash with LMB to receive it. Room assignment, check-in/service counts and the room route commit only after the paid transfer settles.
- Money reaches the player's hand over 0.48 seconds before the wallet changes. Pausing freezes that transfer. Uncollected offers expire after 30 active seconds; offered money does not count as revenue.
- Reception patience defaults to 120 active seconds, increased by room upgrades. An overhead bar/countdown shows remaining time. Offered/collecting cash and head grips do not freeze it; pause, incapacity and genuine danger shelter do. Expiration cancels unpaid cash/reservations and removes the guest from reception, with departure after release if physically held. See [Reception.md](Reception.md).
- Press U for management. This pauses the world. Purchase room, equipment or security improvements, or hire caretakers, then return to work.
- At dawn the game pauses on a shift report. Check receipts, rewards, expenses, fines, investment costs and net income, then continue the next day.

## Prices and effects

| Upgrade | First price | Further levels | Effect per level |
| --- | ---: | --- | --- |
| Rooms | $180 | +$100, maximum 3 | Room rate +$25, guest patience +15%, capacity +1 within actual room count; room cushions |
| Equipment | $120 | +$100, maximum 3 | Work speed +25% for mopping, repairs and laundry |
| Security | $140 | +$100, maximum 3 | Defense structure strength +25%, night preparation grace +5 seconds |
| Caretakers | $180 | Second costs $280; maximum 2 | Actual NPC walks to reachable messes and cleans; $45 wages per caretaker per day |

Base room rate is $80. Caretakers use the existing guest character rig with a mop/apron and work animations. They select complete NavMesh routes on their current floor, clean only when physically close, avoid player-occupied messes and shelter at night. They are cleaning helpers rather than combat companions. Failed staff creation refunds the purchase.

## Consequences and accounts

Guest satisfaction affects tips and checkout reviews. Reputation affects arrival intervals. Grabbing or hitting a guest with a prop costs $3 and 2 reputation; throwing costs $8 and 4 reputation; a gunshot costs $20 and 5 reputation. Repeated grip/throw notifications have a six-second cooldown; gunshots use 0.25 seconds. Killing a guest adds $180 compensation and 20 reputation loss, once per victim. Other human deaths reduce reputation by 7. Guests withdraw money and refuse tips for 60 seconds after misconduct. Grabs/throws preserve unpaid reception state rather than evacuating the queue; shots/prop attacks still trigger danger reactions for victims and nearby witnesses.

Daily upkeep is $95, plus wages. Each completed check-in/service/income target rewards $25. Reputation at least 65 rewards $15. Surviving a day with check-ins and no human deaths rewards $30. The report includes collected room revenue, tips/work rewards, bonuses, upkeep, wages, misconduct compensation and purchased upgrades. Settlement is guarded against repeat charges/rewards.

Career money, reputation, completed days and upgrade levels save through PlayerPrefs at dawn. Restarting loads the following day from that checkpoint. Mid-shift positions, guests, unfinished incidents and transactions are not saved. R retains its existing world/career reset behavior when no firearm is equipped; with a firearm it reloads. Reset clears this career checkpoint.

## Presentation and implementation

The supplied `cash_lowpoly.glb` is imported under `Assets/HotelTogether/Art/Cash`; the selected bundle is normalized into `Resources/HotelTogetherCash/Cash payment.prefab`. Attribution is recorded beside the source. Customer hand poses retain seated/sleeping state; collection uses a bounded hand reach, delayed credit, and pickup/cash audio. Walking, landing, dropping and prop collisions have additional feedback. No package dependency was added.

`HotelEconomy` owns transaction identities, daily reports, reputation, upgrades and persistence. `HotelGuestPayment` owns offers/hand presentation, `HotelCash` owns transfer lifecycle, `HotelStaffBot` owns caretaker navigation/work, and `HotelEconomyUI` displays goals, management and reports. Receipt IDs remain unique across resets so late destruction of an old note cannot cancel a new payment.

## Validation

49 focused checks passed in the isolated Unity validation project with actual guest meshes, CharacterController, physics/raycasts and NavMesh movement. These cover payment selection/animation/pause/exactly-once credit, consequences, purchases, physical caretaker work, daily settlement and stale receipt cleanup. Existing guest physics (33), mouse controls (32), firearms (189), maintenance (43) and menu (20) checks also passed. Reports and native-editor previews are in workspace `output/economy-qa`.

Native HotelLobby compilation and preview were checked. Headless input checks use synthetic input/domain APIs; they do not establish full hardware mouse capture or performance on other machines. The report preview uses staged sample statistics; customer cash uses the actual supplied mesh and hotel character.
