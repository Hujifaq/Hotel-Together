# Luggage delivery and guest room doors

Room service now uses the actual door. Delivery plaques and their colliders are hidden at startup; separate Door lock widgets are no longer created. Empty room doors keep normal open/close interaction.

After check-in, guests close their room door when they arrive. Passing guests do not open unrelated guest rooms. Guests also close the door when they return with luggage.

Take the labelled suitcase to its room, then left-click the door. This knocks with a hand gesture and three impact sounds, deposits a matching held suitcase or nearby trolley luggage outside, and asks the owner to answer. Alternatively, drop the bag near the doorway and left-click the door afterwards. A bag alone no longer calls the owner automatically.

The owner walks out, bends to collect the actual bag, carries it inside and sets it down. Only then is the delivery completed once. If there is no suitcase, the owner can still answer the knock, wait outside for ten seconds, then return and close the door. Clean laundry can be handed over through the door interaction when the owner is waiting there.

Guests who are busy, unsafe or away may not answer immediately. Requests wait for up to thirty seconds; a short cooldown prevents repeated knocking every frame. Existing barricades still block passage. Cancelling a luggage pickup leaves it on the trolley, and taking a waiting bag back cancels collection without awarding the delivery.
