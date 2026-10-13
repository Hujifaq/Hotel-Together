"""Build six color atlases and a hair UV mask from the corrected customer GLB.

Run with Python 3, Pillow and NumPy. The output keeps the model's UV coordinates.
"""
from pathlib import Path
import json
import struct
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
GLB = ROOT / 'Assets/HotelTogether/Resources/HotelTogetherCharacters/Customer-Corrected-v2.glb'
OUT = ROOT / 'Assets/HotelTogether/Resources/HotelTogetherCharacters/CustomerLooks'
# Hair, shirt fabric, leaf print, shorts, and exposed skin in the packed atlas.
SOURCE = np.array([[151, 88, 35], [13, 42, 72], [239, 160, 8],
                   [186, 139, 83], [251, 177, 128]], dtype=np.float32)
LOOKS = [
    ('01-Tropical', [[151, 88, 35], [13, 42, 72], [239, 160, 8], [186, 139, 83], [251, 177, 128]]),
    ('02-Coastal', [[55, 42, 40], [27, 112, 115], [255, 211, 170], [188, 158, 108], [205, 139, 95]]),
    ('03-Garden', [[188, 100, 55], [83, 114, 83], [240, 220, 167], [80, 102, 128], [247, 195, 151]]),
    ('04-Sunset', [[89, 57, 39], [175, 70, 60], [250, 211, 141], [65, 79, 97], [184, 112, 78]]),
    ('05-Midnight', [[45, 38, 49], [79, 69, 125], [191, 214, 179], [136, 142, 113], [136, 80, 61]]),
    ('06-Skyline', [[204, 160, 91], [86, 147, 177], [20, 60, 86], [111, 125, 89], [231, 168, 119]]),
]
with GLB.open('rb') as f:
    magic, version, length = struct.unpack('<III', f.read(12))
    assert magic == 0x46546c67 and version == 2
    count, chunk_type = struct.unpack('<II', f.read(8))
    document = json.loads(f.read(count))
    binary_count, binary_type = struct.unpack('<II', f.read(8))
    view = document['bufferViews'][document['images'][0]['bufferView']]
    f.seek(f.tell() + view.get('byteOffset', 0))
    from io import BytesIO
    source = np.asarray(Image.open(BytesIO(f.read(view['byteLength']))).convert('RGB'), dtype=np.float32)

OUT.mkdir(parents=True, exist_ok=True)
# Classify each texel against the five deliberately distinct source palettes.
# A soft edge retains antialiasing and avoids replacing shoe and eye details.
distance = np.linalg.norm(source[:, :, None, :] - SOURCE[None, None, :, :], axis=3)
category = np.argmin(distance, axis=2)
nearest = np.min(distance, axis=2)
strength = np.clip((95 - nearest) / 48, 0, 1)
strength = strength * strength * (3 - 2 * strength)
for name, targets in LOOKS:
    target = np.array(targets, dtype=np.float32)
    adjusted = np.empty_like(source)
    for k in range(len(SOURCE)):
        # Scale the original light/dark detail about each palette anchor.
        light_ratio = np.mean(target[k]) / np.mean(SOURCE[k])
        adjusted[category == k] = target[k] + (source[category == k] - SOURCE[k]) * light_ratio
    output = np.clip(source * (1 - strength[:, :, None]) + adjusted * strength[:, :, None], 0, 255).astype(np.uint8)
    Image.fromarray(output, 'RGB').save(OUT / f'{name}.png', optimize=True)

# The mask follows the same atlas UVs; it is also used to shape only hair vertices.
hair = ((category == 0) & (nearest < 70)).astype(np.uint8) * 255
Image.fromarray(hair, 'L').save(OUT / 'Hair-UV-Mask.png', optimize=True)
print('Created six 1254x1254 UV-aligned customer atlases and hair mask in', OUT)
