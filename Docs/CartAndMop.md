# Cart contacts and floor cleaning

Both luggage and housekeeping carts now push loose physics props rather than treating them as walls. Contact wakes small authored floor litter into dynamic physics. Impulses act at contact points and are bounded by mass and movement speed. Walls, furniture, people and heavy objects remain blocking; a prop trapped against a wall also blocks the cart. Diagonal movement can slide along a wall, and an obstructed turn can keep the previous heading rather than stopping all movement.

Take the mop from a cleaning cart, aim at a wet patch and hold left click. Holding the button can start cleaning even if the initial press was missed, and can resume when the aim returns to the patch. Floor-hit selection also recognises the wet patch within a small margin of its thin collider. Releasing stops work but keeps partial progress. Three patches saturate the mop; click the cart bucket while holding it to rinse.

The old concentric blue Lobby spill was decorative imported geometry without a cleaning task. Its five renderers are now hidden through scene overrides and an explicit housekeeping reference. The actual wet-floor task occupies that location, uses the same irregular mesh as the upper floor, is positioned close to the Lobby floor and has a subtler transparent colour. It disappears after cleaning. The original GLB is unchanged.
