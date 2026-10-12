HOTEL TOGETHER — EMPLOYEE 03 / HOUSEKEEPING
3D SCULPT-STYLE PROTOTYPE V1 • Blender Import Pack

ไฟล์ในชุดนี้
- HotelTogether_Employee03_Housekeeping_V1.glb : ตัวละคร 3D พนักงานแม่บ้าน
- HotelTogether_Housekeeping_Mop_Towels_V1.glb : ไม้ถูพื้นและผ้าขนหนู (พร็อพแยก)
- HotelTogether_Housekeeping_V1_Turnaround.png : ภาพจากโมเดล 4 มุม
- HotelTogether_Housekeeping_V1_Face.png : ตัวอย่างระยะใกล้
- HotelTogether_Employee03_Housekeeping_Build.py : Python source ของตัวละคร (ต้องมี numpy scipy scikit-image trimesh)
- HotelTogether_Employee03_Mop_Towels_Build.py : Python source ของพร็อพ
- HotelTogether_Employee03_Housekeeping_Render.py : Render script (ต้องมี vtk, Pillow)
- HotelTogether_Employee03_Housekeeping_V1_Metadata.json : จำนวน Polygon และรายละเอียด

นำเข้า Blender
1. เปิด Blender 4.x หรือใหม่กว่า
2. File > Import > glTF 2.0 (.glb/.gltf)
3. เลือกไฟล์ GLB ของตัวละคร และเปิดใน Solid หรือ Material Preview
4. ถ้าต้องการพร็อพ ให้ Import GLB อีกไฟล์หนึ่ง
5. ใน Outliner มีชื่อ Mesh แยกให้ปรับได้

Design Intent
- ผู้หญิงผมสีน้ำตาลรวบมวยต่ำ
- เสื้อพนักงานแม่บ้านสีฟ้า ผ้ากันเปื้อนขาว กางเกงน้ำเงิน รองเท้าอ่อน
- ตัวท้วมตัน ไม่มีคิ้ว ตาโปน หนังตาหนัก รูม่านตาเล็ก หน้ามึน
- สไตล์ stylized co-op game สำหรับ Hotel Together

คำเตือนคุณภาพ / สถานะงาน
เป็นต้นแบบ 3D ที่สร้างขึ้นจากทรง parametrized, loft, SDF sculpt และชิ้นส่วนงานผ้า
ไม่ใช่งาน Sculpt มือหรือ Final Game Asset คุณภาพระดับ Character Sheet
โมเดลยังมีรอยต่อ/ชิ้นส่วนซ้อนอยู่บางจุด และไม่ผ่านขั้น Retopology, UV unwrap,
Texture bake, Rigging, Skinning, Animation, LOD optimization หรือ collision setup
จำนวน Tris สูงเกินสมควรสำหรับตัวละคร Co-op แบบ Production ต้องจัด Mesh ใหม่ก่อน
ยังไม่มีไฟล์ .blend แบบ Native แต่ Import จาก GLB ได้ และบันทึกเป็น .blend ใน Blender ได้

ขนาดตัวละครโดยประมาณ 1.67 เมตร, หน่วยนำเข้าเป็นเมตร, Z-up, หน้าหัน -Y
สร้างเพื่อใช้ทบทวนรูปร่างและวางแผนการปั้นต่อ ไม่ใช่ Final Delivery
