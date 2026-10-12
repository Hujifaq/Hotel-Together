HOTEL TOGETHER — EMPLOYEE 02 / BELLHOP
โมเดล 3D จริงสำหรับ Blender — Sculpt-Style Prototype V1

คาแรกเตอร์นี้ยึดจาก Character Sheet เดิมของ Hotel Together:
- พนักงานยกกระเป๋าผู้หญิง ผมบ๊อบสั้นสีดำ
- เครื่องแบบสีแดงเข้ม กระดุมและขอบทอง หมวก Bellhop สีแดง
- รูปร่างท้วมตัน ตัวค่อนข้างเตี้ย คอสั้น ไหล่กลม
- ไม่มีคิ้ว ตาโปน หนังตาหนัก รูม่านตาเล็ก ปากอ้าเล็กน้อยให้ดูงง

ไฟล์ในชุด:
HotelTogether_Employee02_Bellhop_V1.glb — โมเดลตัวละคร 3D และ Materials
HotelTogether_Bellhop_LuggageCart_V1.glb — รถเข็นกระเป๋า/พร็อพแยก
HotelTogether_Bellhop_V1_Turnaround.png — ภาพ Render มุมหน้า 3/4 ข้าง หลัง
HotelTogether_Bellhop_V1_Face.png — ภาพ Render ใบหน้า
HotelTogether_Employee02_Bellhop_Build.py — Python source สร้าง Mesh (trimesh/scipy/skimage)
HotelTogether_Bellhop_LuggageCart_Build.py — Python source รถเข็น
HotelTogether_Employee02_Bellhop_V1_Metadata.json — ข้อมูลเชิงเทคนิค

เปิดใน Blender:
1) เปิด Blender
2) File > Import > glTF 2.0 (.glb/.gltf)
3) เลือก HotelTogether_Employee02_Bellhop_V1.glb
4) ตั้ง Viewport shading เป็น Material Preview หรือ Rendered เพื่อเห็นวัสดุ
5) หากต้องการรถเข็นให้นำเข้า HotelTogether_Bellhop_LuggageCart_V1.glb เพิ่ม

ข้อจำกัดสำคัญ:
- นี่เป็นขั้น Sculpt-Style Prototype ที่สร้างจาก Python-generated meshes ไม่ใช่งานปั้น Sculpt ระดับ Production
- ไม่ได้ Retopology, UV unwrap แบบ Production, ผูกกระดูก (Rig), ทำ Skin Weight หรือ Animation
- จำนวน Polygon สูง ไม่ควรใส่เข้าเกมโดยตรงโดยไม่ Optimize
- พื้นผิวและรูปทรงบางส่วนยังจำเป็นต้องเก็บแก้ด้วยมือใน Blender
- ไฟล์ GLB เป็นโมเดล 3D จริง มี geometry และ materials ไม่ใช่เพียงภาพอ้างอิง
- ภาพ Render สร้างจาก Mesh เรขาคณิต 3D ของไฟล์เดียวกัน

เกณฑ์ตรวจความใกล้เคียง Character Sheet ก่อนขึ้นเวอร์ชันต่อไป:
(1) สีหน้าให้ตลก / ง่วง / มึน โดยไม่ต้องยิ้ม
(2) ความท้วมตันของร่างกายและสัดส่วนในมุม 3/4
(3) รูปผมบ๊อบ + รูปทรงหมวก Bellhop
(4) ต้องแก้ทรงเสื้อ แขนและหนังตาให้เรียบเนียนก่อนผลิตเกม
