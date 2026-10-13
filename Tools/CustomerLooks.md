# Customer appearance variants

The six PNG files in `Assets/HotelTogether/Resources/HotelTogetherCharacters/CustomerLooks/` use the exact UV layout of `Customer-Corrected-v2.glb`. Apply any one of them to the model's `Customer_ShirtHairClothes` material. The separate `Skin` and `Eyebrows` materials must use the matching colors below.

| Atlas | Hair | Skin | Shirt and shorts | Hair shape in Unity |
| --- | --- | --- | --- | --- |
| 01-Tropical | chestnut | peach | navy and tan | original layered hair |
| 02-Coastal | dark brown | medium tan | teal and sand | short crop |
| 03-Garden | copper | light warm | sage and denim | side sweep |
| 04-Sunset | brown | warm tan | coral and slate | raised quiff |
| 05-Midnight | black plum | deep brown | violet and olive | longer sides |
| 06-Skyline | golden blond | golden tan | sky blue and olive | swept back bun |

The Unity game assigns looks 1–6 to successive guests automatically. Hair silhouette changes are Unity blend shapes; the Skyline bun follows the head bone. The mask PNG is used to target only hair vertices when building those shapes. Run `Tools/GenerateCustomerLooks.py` with Python, Pillow, and NumPy to recreate the six atlases from the corrected GLB.
