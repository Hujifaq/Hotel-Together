# เมนูพักเกมและการตั้งค่า

ปรับจาก commit `4728177` วันที่ 13 ตุลาคม 2026 สำหรับ Unity 6000.6.5f1

## สิ่งที่เปลี่ยน

- เมนูเริ่มเกมและ Esc หยุดเวลาเกม ฟิสิกส์ และเสียงผ่าน `Time.timeScale` / `AudioListener.pause` ระบบแขกและการต่อสู้ตรวจสถานะรับ input ของผู้เล่นร่วมกัน
- กลับเข้าเล่นด้วยปุ่ม Resume, Enter หรือ Esc หลังเริ่มเล่นครั้งแรก เวลาเกมและเสียงกลับสู่ค่าก่อนพัก การพักซ้ำไม่เขียนทับค่าที่ต้องคืน
- สลับหน้าต่างหรือเสีย focus แล้วพักเกม กลับมาที่หน้าต่างยังต้องสั่งเล่นต่อเอง
- Settings & Controls ปรับความไวเมาส์ 0.25–2.5 เท่าจากค่าเดิม เสียง 0–100% การโยกกล้อง และกลับแกน Y ค่าถูกเก็บใน PlayerPrefs ด้วย prefix `HotelTogether.Settings.` ไม่เปลี่ยน prefab
- O เปิด/ปิดหน้าตั้งค่า Esc กลับเมนู ลูกศรเลือกพนักงานเฉพาะหน้าเลือกพนักงาน
- การพักเก็บเวลาที่เหลือของข้อความแจ้งเตือน ตัวผู้เล่นและมือไม่เคลื่อนไหวขณะเมนูเปิด
- เริ่มใหม่ล้างความเร็วตก การโยกและการหมุนกล้อง เป้าหมายใช้งาน และจำนวนครั้งกดกริ่ง
- เมนูใช้สี/องค์ประกอบเดิมและเพิ่มพื้นที่สำหรับปุ่มตั้งค่า การย่อ UI รองรับหน้าต่างเล็กกว่าค่าเดิม

## ไฟล์และการเปิดใช้งาน

`HotelPlayer` เป็น partial class ส่วนใหม่ `HotelPlayerMenu.cs` จัดการเมนูและค่าตั้งค่า ข้อมูล serialized เดิมและ GUID ของ HotelPlayer ไม่เปลี่ยน ไม่ต้องเพิ่ม Component หรือตั้งค่า Inspector และไม่เพิ่มแพ็กเกจ

`HotelCombat` และ `HotelGuestSimulation` ใช้ `GameplayInputEnabled` เป็นเงื่อนไขทำงาน ส่วน `HotelEmployeePlayer` ไม่รับลูกศรเลือกตัวละครขณะเปิดหน้าตั้งค่า

## การตรวจสอบ

คอมไพล์ Runtime และ Editor ด้วย Roslyn ของ Unity 6000.6.5f1 และ references จาก compiler response files ของโปรเจกต์ ไม่มี compile errors ฝั่ง Editor มีคำเตือน API เก่าในไฟล์เดิม

ชุดตรวจ `Assets/Editor/HotelPlayerMenuValidation.cs` ทดสอบผ่าน public API ใน Play Mode: การพักเวลา/ฟิสิกส์ การคืนค่าเมื่อเล่นต่อ focus loss การเริ่มใหม่ และการคืน global state เมื่อปิด Component ผลแต่ละครั้งบันทึกที่ `Logs/HotelPlayerMenuValidation.txt` ทดสอบในฉากแยก ไม่บันทึกฉากโรงแรม ชุดทดสอบหยุด MonoBehaviour Update ชั่วคราวเพื่อแยกการทดสอบ API ออกจากการจับ cursor ใน headless Editor โดย engine time และ physics ยังทำงาน และคืน PlayerLoop เดิมเมื่อจบ

ผลรอบนี้: ผ่าน 13 รายการในโปรเจกต์ทดสอบแยกด้วย runtime DLL ที่คอมไพล์จากโค้ดปัจจุบันและไลบรารีของโปรเจกต์เดิม ได้แก่ startup pause, input guard, clock freeze, rigidbody freeze, resume state, clock/physics resume, repeated pause, focus loss, explicit focus resume, reset velocity, reset camera/bell, notification lifetime และ disable cleanup

การตรวจในโปรเจกต์ทดสอบแยกไม่ได้แทนการเล่น HotelLobby จริงหรือการตรวจภาพเมนู การเลื่อน sliders การจำค่าหลังเปิดเกมใหม่ เสียงจริง และสัดส่วน UI ควรตรวจใน Game View ของโปรเจกต์หลักก่อน build

## ข้อจำกัด

ระบบพักนี้ใช้ global time scale สำหรับเกมผู้เล่นคนเดียว ต้องเปลี่ยนแนวทางเมื่อเพิ่ม multiplayer ค่าตั้งค่าเป็นค่าของเครื่อง ไม่ใช่ข้อมูลเซฟความคืบหน้าเกม ชุดตรวจนี้ไม่ยืนยัน FPS หรือประสิทธิภาพของฉากโรงแรม
