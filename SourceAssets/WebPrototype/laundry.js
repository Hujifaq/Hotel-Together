import * as T from 'three';
export function createLaundry({scene,world,objects,player,camera,toast,release,grab}){
 const machines=[],linens=[];let drying=null,folded=0,stored=0;
 const washSeconds=8,drySeconds=6;
 function tag(o){const cv=document.createElement('canvas');cv.width=768;cv.height=180;const tex=new T.CanvasTexture(cv),sprite=new T.Sprite(new T.SpriteMaterial({map:tex,transparent:true,depthWrite:false}));sprite.position.set(0,o.size.y/2+.27,0);sprite.scale.set(1.55,.365,1);o.root.add(sprite);o.laundryLabel={cv,tex,previous:''};}
 function label(o,text){if(o.laundryLabel.previous===text)return;const {cv,tex}=o.laundryLabel,c=cv.getContext('2d');c.fillStyle='#264b59';c.fillRect(0,0,cv.width,cv.height);c.fillStyle='#ffe3a6';c.textAlign='center';c.font='bold 43px sans-serif';const lines=text.split('\n');lines.forEach((v,i)=>c.fillText(v,384,lines.length===1?112:70+i*64));tex.needsUpdate=true;o.laundryLabel.previous=text;}
 function hud(){let el=document.getElementById('laundryStatus');if(!el){el=document.createElement('div');el.id='laundryStatus';el.className='task';document.getElementById('serviceStatus').after(el);}el.textContent='พับผ้า '+folded+'/3 · เก็บเข้าตู้ '+stored+'/3';}
 function tint(o,color){o.root.traverse(n=>{if(n.isMesh&&!n.userData.interactionProxy){if(!n.userData.laundryOwnMaterial){n.material=Array.isArray(n.material)?n.material.map(m=>m.clone()):n.material.clone();n.userData.laundryOwnMaterial=true;}for(const m of Array.isArray(n.material)?n.material:[n.material])m.color.set(color);}});}
 async function build(asset){
  for(const [x,name] of [[-27,'เครื่องซักผ้า A'],[-25,'เครื่องซักผ้า B']]){const o=await asset('LaundryWasher',name,'washer',x,-32.35,0,0,-.025);o.door=o.root.getObjectByName('LaundryDoorPivot');o.drum=o.root.getObjectByName('LaundryDrumPivot');o.doorOpen=false;o.phase='empty';o.detergent=false;o.elapsed=0;tag(o);machines.push(o);}
  drying=await asset('LaundryDryingRack','ราวตากผ้า','drying',-20.2,-32.15,0,0,1.2);drying.phase='empty';drying.elapsed=0;tag(drying);
  const added=await Promise.all([
   asset('LaundryCabinet','ตู้เก็บผ้าสะอาด','storage',-17.8,-28.6,Math.PI,0,-.025),asset('LaundryShelf','ตู้เก็บน้ำยา','static',-17.8,-31,Math.PI,0,-.025),
   asset('LaundryFoldTable','โต๊ะพับผ้า A','folding',-27.8,-26.5,-Math.PI/2,0,-.025),asset('LaundryFoldTable','โต๊ะพับผ้า B','folding',-25.6,-26.5,-Math.PI/2,0,-.025),
   asset('LaundrySofa','โซฟาห้องซักผ้า','seat',-18,-24.5,-Math.PI/2,0,-.025),asset('LaundryChair','เก้าอี้ห้องซักผ้า','seat',-21.3,-24.5,Math.PI/2,0,-.025),asset('LaundryCoffeeTable','โต๊ะพักห้องซักผ้า','table',-20,-24.5,0,0,-.025),
   asset('KitchenCounter','เคาน์เตอร์น้ำยาซักผ้า','static',-27.8,-23.2,0,0,-.025)
  ]);
  added.filter(o=>o.kind==='folding').forEach(o=>{o.tableClean=false;const stain=new T.Mesh(new T.CircleGeometry(.16,12),new T.MeshStandardMaterial({color:'#73583f',transparent:true,opacity:.7,depthWrite:false}));stain.rotation.x=-Math.PI/2;stain.position.set(.12,-o.size.y/2+.724,.05);o.root.add(stain);o.stain=stain;tag(o);label(o,o.name+'\nใช้สเปรย์เช็ดโต๊ะก่อนพับ');});const cabinet=added.find(o=>o.kind==='storage');tag(cabinet);label(cabinet,'ตู้เก็บผ้าสะอาด\nE · เก็บผ้าที่พับแล้ว');
  await Promise.all([
   asset('LaundryDetergent','น้ำยาซักผ้า','pickup',-27.8,-23.2,0,.9,.90),asset('LaundryCleaner','น้ำยาทำความสะอาด','pickup',-28.25,-23.2,0,.5,.90),asset('LaundrySpray','ขวดสเปรย์','pickup',-27.35,-23.2,0,.35,.90),asset('LaundryLamp','โคมไฟห้องซักผ้า','lamp',-20,-24.5,0,0,.50)
  ]);
  for(const [i,z] of [[1,-27.0],[2,-27.5],[3,-28.0]]){const o=await asset('LaundryLinen','ผ้าสกปรก '+i,'pickup',-28.35,z,0,.8,.05);o.linenStage='dirty';tint(o,'#9c8e78');linens.push(o);}
  hud();updateLabels();
 }
 function store(o){release(false);world.removeBody(o.body);o.root.visible=false;o.kind='static';o.laundryStored=true;}
 function take(o,station,stage){o.kind='pickup';o.linenStage=stage;o.laundryStored=false;o.root.visible=true;o.body.position.set(station.body.position.x,1.1,station.body.position.z+.75);o.body.quaternion.copy(o.rotation);o.body.velocity.setZero();o.body.angularVelocity.setZero();o.body.aabbNeedsUpdate=true;if(!world.bodies.includes(o.body))world.addBody(o.body);o.body.wakeUp();tint(o,stage==='wet'?'#91acbe':'#eee4d0');grab(o);}
 function interact(o,held){
  if(o.kind==='washer'){
   if(o.phase==='washing'){toast('กำลังซัก · รอให้เสร็จก่อนเปิดฝา');return true;}
   if(o.phase==='ready'){if(held){toast('วางของที่ถือก่อนรับผ้าเปียก');return true;}o.doorOpen=true;take(o.linen,o,'wet');o.linen=null;o.phase='empty';o.detergent=false;toast('ซักเสร็จแล้ว · นำผ้าเปียกไปที่ราวตาก');return true;}
   if(held?.linenStage==='dirty'){if(!o.doorOpen){toast('เปิดฝาเครื่องก่อนใส่ผ้า');return true;}if(o.linen){toast('เครื่องนี้มีผ้าอยู่แล้ว');return true;}store(held);o.linen=held;o.phase='loaded';toast('ใส่ผ้าแล้ว · หยิบน้ำยามาเติม');return true;}
   if(held?.file==='LaundryDetergent'){if(!o.linen){toast('ใส่ผ้าก่อนเติมน้ำยา');return true;}o.detergent=true;toast('เติมน้ำยาแล้ว · วางขวด แล้วกด E ที่เครื่องเพื่อเริ่มซัก');return true;}
   if(held){toast('เครื่องรับผ้าสกปรกและน้ำยาซักผ้า');return true;}
   if(o.linen&&o.detergent){o.doorOpen=false;o.phase='washing';o.elapsed=0;toast('ปิดฝาและเริ่มซักแล้ว');return true;}
   o.doorOpen=!o.doorOpen;toast(o.doorOpen?'เปิดฝาเครื่องแล้ว':'ปิดฝาเครื่องแล้ว');return true;
  }
  if(o.kind==='drying'){
   if(o.phase==='drying'){toast('กำลังตากผ้า · รอสักครู่');return true;}
   if(o.phase==='ready'){if(held){toast('วางของก่อนรับผ้าแห้ง');return true;}take(o.linen,o,'clean');o.linen=null;o.phase='empty';toast('ผ้าแห้งแล้ว · นำไปพับบนโต๊ะ');return true;}
   if(held?.linenStage==='wet'){store(held);o.linen=held;o.phase='drying';o.elapsed=0;held.root.visible=true;held.body.position.set(o.body.position.x,o.body.position.y-.16,o.body.position.z);toast('แขวนผ้าแล้ว · รอให้แห้ง');return true;}
   toast('นำผ้าเปียกจากเครื่องซักผ้ามาตาก');return true;
  }
  if(o.kind==='storage'){if(held?.linenStage==='folded'){store(held);held.linenStage='stored';stored++;hud();toast('เก็บผ้าสะอาดเข้าตู้แล้ว '+stored+'/3');}else toast('นำผ้าที่พับแล้วมาเก็บเข้าตู้');return true;}
  if(o.kind==='folding'){
   if(['LaundrySpray','LaundryCleaner'].includes(held?.file)){o.tableClean=true;o.stain.visible=false;label(o,o.name+'\nโต๊ะสะอาด · พร้อมพับผ้า');toast('เช็ดโต๊ะด้วย'+held.name+'แล้ว');return true;}
   if(held?.linenStage==='clean'&&!o.tableClean){toast('ใช้สเปรย์หรือน้ำยาเช็ดโต๊ะก่อนพับผ้า');return true;}
   if(held?.linenStage==='clean'){release(false);held.linenStage='folded';held.body.position.set(o.body.position.x,o.body.position.y-o.size.y/2+.720+held.size.y/2+.015,o.body.position.z);held.body.quaternion.copy(held.rotation);held.body.velocity.setZero();held.body.angularVelocity.setZero();held.body.aabbNeedsUpdate=true;held.body.wakeUp();folded++;hud();toast('พับผ้าสะอาดแล้ว '+folded+'/3');return true;}
   toast('นำผ้าที่ซักและตากแล้วมาพับ');return true;
  }
  return false;
 }
 function prompt(o,held){if(o.linenStage&&o.kind==='pickup')return 'E · หยิบ'+({dirty:'ผ้าสกปรก',wet:'ผ้าเปียก',clean:'ผ้าแห้งสะอาด',folded:'ผ้าที่พับแล้ว'}[o.linenStage]??'ผ้า');if(o.kind==='washer'){if(o.phase==='washing')return 'กำลังซัก '+Math.floor(o.elapsed/washSeconds*100)+'%';if(o.phase==='ready')return 'E · รับผ้าเปียก';if(held?.linenStage==='dirty')return 'E · ใส่ผ้า';if(held?.file==='LaundryDetergent')return 'E · เติมน้ำยา';return o.linen&&o.detergent?'E · ปิดฝาและเริ่มซัก':'E · '+(o.doorOpen?'ปิด':'เปิด')+'ฝาเครื่องซักผ้า';}if(o.kind==='drying')return o.phase==='ready'?'E · รับผ้าแห้ง':o.phase==='drying'?'กำลังตาก '+Math.floor(o.elapsed/drySeconds*100)+'%':'E · แขวนผ้าเปียก';if(o.kind==='storage')return 'E · เก็บผ้าที่พับแล้ว';if(o.kind==='folding')return ['LaundrySpray','LaundryCleaner'].includes(held?.file)?'E · เช็ดโต๊ะ':!o.tableClean?'โต๊ะมีคราบ · หยิบสเปรย์มาเช็ด':'E · พับผ้าสะอาด';return '';}
 function updateLabels(){for(const o of machines)label(o,o.name+'\n'+(o.phase==='washing'?'กำลังซัก '+Math.floor(o.elapsed/washSeconds*100)+'%':o.phase==='ready'?'ซักเสร็จ · E รับผ้า':o.linen?(o.detergent?'พร้อมซัก':'รอเติมน้ำยา'):(o.doorOpen?'ฝาเปิด · ใส่ผ้าได้':'E · เปิดฝา')));if(drying)label(drying,'ราวตากผ้า\n'+(drying.phase==='drying'?'กำลังตาก '+Math.floor(drying.elapsed/drySeconds*100)+'%':drying.phase==='ready'?'ผ้าแห้งแล้ว · E รับผ้า':'E · แขวนผ้าเปียก'));}
 function update(dt){for(const o of machines){o.door.rotation.y=T.MathUtils.damp(o.door.rotation.y,o.doorOpen?-1.45:0,8,dt);if(o.phase==='washing'){o.drum.rotation.z+=dt*5;o.elapsed=Math.min(washSeconds,o.elapsed+dt);if(o.elapsed===washSeconds){o.phase='ready';toast(o.name+'ซักเสร็จแล้ว');}}}if(drying?.phase==='drying'){drying.elapsed=Math.min(drySeconds,drying.elapsed+dt);if(drying.elapsed===drySeconds){drying.phase='ready';tint(drying.linen,'#eee4d0');toast('ผ้าแห้งแล้ว · รับไปพับได้');}}updateLabels();}
 function reset(){for(const o of machines){o.phase='empty';o.linen=null;o.detergent=false;o.elapsed=0;o.doorOpen=false;o.door.rotation.y=0;o.drum.rotation.z=0;}if(drying){drying.phase='empty';drying.elapsed=0;drying.linen=null;}for(const o of linens){if(!world.bodies.includes(o.body))world.addBody(o.body);o.kind='pickup';o.laundryStored=false;o.root.visible=true;o.linenStage='dirty';tint(o,'#9c8e78');}for(const o of objects.filter(o=>o.kind==='folding')){o.tableClean=false;o.stain.visible=true;label(o,o.name+'\nใช้สเปรย์เช็ดโต๊ะก่อนพับ');}folded=0;stored=0;hud();updateLabels();}
 return {build,interact,prompt,update,reset,status:()=>({folded,stored,machines:machines.map(o=>({name:o.name,phase:o.phase,doorOpen:o.doorOpen,doorAngle:o.door.rotation.y,detergent:o.detergent,linen:o.linen?.name??null,progress:o.elapsed/washSeconds})),drying:drying?{phase:drying.phase,linen:drying.linen?.name??null,progress:drying.elapsed/drySeconds}:null})};
}
