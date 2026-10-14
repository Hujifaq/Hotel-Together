# Mouse controls

- LMB on offered customer cash accepts room payment/tips with a hand-transfer animation. U opens the paused management store; Enter continues from the dawn report. See [HotelEconomy.md](HotelEconomy.md).
- Check-in requires E on the first guest at reception to request cash, then LMB on that cash to collect it. Body LMB cannot offer money or admit an unpaid guest; holding LMB on the head remains a physics grip. Grabbing/withdrawing/partial transfer never bypasses registration. Reception patience continues through offers and grips. See [Reception.md](Reception.md).

- Hold LMB on a guest's head to grip; release LMB to let go; RMB throws.
- Hold LMB on ordinary props to reach/pick up; release cancels unfinished reach or drops the prop. RMB throws or cancels unfinished reach.
- Hold LMB to push a cart, release to stop. Releasing a carried suitcase/laundry item over its receiver attempts loading/delivery before dropping.
- Click tools, mop, weapons, supplies and service trays once to equip. Release keeps them equipped. Click LMB to operate/serve; hold LMB for continuous spray, mop and repairs. Q drops and RMB throws equipment. `keepWorkToolsEquipped` controls this policy.
- LMB opens doors, operates lights/taps, rings bells, uses elevator buttons, sits/stands and serves customers, including while tools are equipped.
- Space jumps with a gravity arc, ceiling checks and procedural hand/leg motion. No jump while seated, pushing, transported or incapacitated.
- E/Q and contextual cart G shortcuts remain available. Ctrl blocks with equipment; bare hands can also block with RMB.
- Pause, focus loss, disable, reset and incapacity cancel mouse-owned reaches/grips. A mouse button held during menu resume must be released before starting a new action.

`HotelPlayerMouseInteraction` routes input to existing gameplay APIs. No new packages or serialized scene edits are required. Reach/throw/repair/mop animations use the existing procedural rig.

QA uses an isolated Unity project with actual raycasts, CharacterController and rigidbody simulation. Headless reach completion uses the existing pickup API because native cursor capture is unavailable; this does not validate end-to-end physical mouse capture. Reports are in `output/mouse-controls-qa`.

Firearms remain equipped. LMB fires (hold for automatic weapons), R reloads while equipped, Q drops, RMB throws; click the storage-room ammo crate to refill reserves. R resets only when a firearm is not equipped. See FirearmCombat.md.

The barricade hammer also stays equipped. Aim at architectural glass or a door and hold LMB for timed build/repair/reinforce/removal. RMB while focused on an opening cycles tasks; elsewhere it throws the hammer. Click a door's bolt plate with LMB to lock/unlock, including while holding tools. Q puts the hammer down. See HotelDefense.md.
