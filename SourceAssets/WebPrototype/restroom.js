import * as T from 'three';
import * as C from 'cannon-es';
import {createRoomDoors} from './room-doors.js';
export function createRestroom({scene,world,objects,player,toast,grab,collider}){
 const ceramic=new T.MeshStandardMaterial({color:'#f4ecdc',roughness:.24}),sage=new T.MeshStandardMaterial({color:'#64847b',roughness:.58}),brass=new T.MeshStandardMaterial({color:'#b99b60',metalness:.65,roughness:.32}),chrome=new T.MeshStandardMaterial({color:'#b5c5c4',metalness:.7,roughness:.19}),wood=new T.MeshStandardMaterial({color:'#ae8b62',roughness:.65});
 const fixtures=[],sheets=[],waterMat=new T.MeshStandardMaterial({color:'#87cad3',transparent:true,opacity:.65,roughness:.14});let stallDoors,flushes=0,washes=0,papers=0,meterOn=true,meterLight;
 function box(parent,w,h,d,x,y,z,m){const n=new T.Mesh(new T.BoxGeometry(w,h,d),m);n.position.set(x,y,z);n.castShadow=true;n.receiveShadow=true;parent.add(n);return n;}
 function fixed(w,h,d,x,y,z,m){box(scene,w,h,d,x,y,z,m);collider(w,h,d,x,y,z);}
 function cylinder(parent,r,h,x,y,z,m){const n=new T.Mesh(new T.CylinderGeometry(r,r,h,24),m);n.position.set(x,y,z);parent.add(n);return n;}
 function item(name,kind,root,size,position,mass=0){scene.add(root);root.position.set(...position);const body=new C.Body({mass,shape:new C.Box(new C.Vec3(size[0]/2,size[1]/2,size[2]/2)),collisionFilterGroup:mass?2:1,collisionFilterMask:1|2|4,linearDamping:.35});body.position.set(...position);world.addBody(body);const o={name,kind,file:'BathroomBuilt',root,body,size:new T.Vector3(...size),home:body.position.clone(),rotation:body.quaternion.clone()};root.userData.item=o;objects.push(o);return o;}
 function label(text,x,y,z,rot=0,w=.65){const cv=document.createElement('canvas');cv.width=512;cv.height=180;const ctx=cv.getContext('2d');ctx.fillStyle='#335b57';ctx.fillRect(0,0,512,180);ctx.fillStyle='#f3e6bd';ctx.textAlign='center';ctx.font='bold 60px sans-serif';ctx.fillText(text,256,112);const tex=new T.CanvasTexture(cv);tex.colorSpace=T.SRGBColorSpace;const m=new T.Mesh(new T.PlaneGeometry(w,w*180/512),new T.MeshBasicMaterial({map:tex}));m.position.set(x,y,z);m.rotation.y=rot;scene.add(m);}
 async function build(asset){
  // Two private stalls, with a 1.9 m shared aisle on their east side.
  for(const z of [-14.8,-12.7])fixed(1.75,2.16,.065,-16.03,1.16,z,sage);
  for(const [z,d] of [[-16.6375,.525],[-14.8,1.05],[-12.9625,.525]])fixed(.065,2.16,d,-15.1,1.16,z,sage);
  for(const z of [-15.85,-13.75])box(scene,.065,.10,1.4,-15.1,2.29,z,brass);
  stallDoors=createRoomDoors({scene,world,objects,player,toast,plan:{rooms:[{id:'WC1',label:'คูหาสุขา 1'},{id:'WC2',label:'คูหาสุขา 2'}],doors:[{room:'WC1',axis:'V',side:'E',x:-15.1,z:-15.85,w:1.05,height:2.1},{room:'WC2',axis:'V',side:'E',x:-15.1,z:-13.75,w:1.05,height:2.1}]}});
  for(const [i,z] of [-15.85,-13.75].entries()){
   const o=await asset('BathroomToilet','ชักโครก '+(i+1),'toilet',-16.45,z,Math.PI,0,-.025);o.timer=0;fixtures.push(o);
   const ripple=new T.Mesh(new T.RingGeometry(.055,.073,24),waterMat);ripple.rotation.x=-Math.PI/2;ripple.position.set(-16.22,.465,z);ripple.visible=false;scene.add(ripple);o.ripple=ripple;
   const p=await asset('BathroomPaper','ที่แขวนกระดาษ '+(i+1),'paper',-16.80,z+.45,Math.PI/2,0,.70);fixtures.push(p);
   label('WC '+(i+1),-15.055,2.02,z,Math.PI/2,.45);
  }
  // Surface panels are offset from the existing walls, avoiding coplanar faces.
  box(scene,.025,2.15,5.7,-16.88,1.075,-14,ceramic);box(scene,3.7,2.15,.025,-15,1.075,-16.88,ceramic);box(scene,3.7,2.15,.025,-15,1.075,-11.12,ceramic);
  const cv=document.createElement('canvas');cv.width=cv.height=128;const cx=cv.getContext('2d');cx.fillStyle='#e4e1d2';cx.fillRect(0,0,128,128);cx.fillStyle='#879c8e';cx.fillRect(2,2,60,60);cx.fillRect(66,66,60,60);const tex=new T.CanvasTexture(cv);tex.colorSpace=T.SRGBColorSpace;tex.wrapS=tex.wrapT=T.RepeatWrapping;tex.repeat.set(4,6);const floor=new T.Mesh(new T.PlaneGeometry(3.8,5.8),new T.MeshStandardMaterial({map:tex,roughness:.42}));floor.rotation.x=-Math.PI/2;floor.position.set(-15,.012,-14);floor.receiveShadow=true;scene.add(floor);
  const root=new T.Group();box(root,1.75,.70,.58,0,-.12,0,wood);box(root,1.9,.075,.72,0,.265,0,ceramic);for(const x of [-.44,.44]){box(root,.81,.58,.025,x,-.10,-.306,sage);box(root,.19,.025,.035,x,.10,-.33,brass);const bowl=cylinder(root,.22,.045,x,.33,-.05,ceramic);const inner=cylinder(root,.175,.008,x,.354,-.05,waterMat);const lip=new T.Mesh(new T.TorusGeometry(.195,.026,10,32),ceramic);lip.rotation.x=Math.PI/2;lip.position.set(x,.378,-.05);root.add(lip);cylinder(root,.024,.30,x,.47,.18,chrome);box(root,.048,.045,.23,x,.61,.085,chrome);}
  const sink=item('อ่างล้างมือ','sink',root,[1.9,1.24,.72],[-14.35,.535,-11.48]);sink.timer=0;sink.streams=[];for(const x of [-.44,.44]){const n=cylinder(root,.012,.22,x,.46,-.025,waterMat);n.visible=false;sink.streams.push(n);}fixtures.push(sink);
  box(scene,1.9,1.0,.035,-14.35,1.93,-11.13,brass);box(scene,1.78,.89,.018,-14.35,1.93,-11.155,new T.MeshPhysicalMaterial({color:'#a7bfbb',metalness:.75,roughness:.16}));
  label('WASH HANDS',-14.35,2.64,-11.14,Math.PI,1.5);
  const soap=new T.Group();box(soap,.095,.14,.08,0,0,0,new T.MeshStandardMaterial({color:'#e3b36d'}));box(soap,.085,.02,.035,.012,.09,0,chrome);item('สบู่ล้างมือ','pickup',soap,[.11,.2,.09],[-13.67,.94,-11.53],.25);
  const bin=new T.Group();cylinder(bin,.17,.43,0,0,0,sage);cylinder(bin,.185,.045,0,.22,0,chrome);item('ถังขยะสุขา','static',bin,[.38,.49,.38],[-16.5,.25,-11.53]);
  await asset('BathroomBucket','ถังน้ำทำความสะอาดสุขา','pickup',-16.45,-12.13,0,1.5,-.025);await asset('BathroomTool','ที่โกยผงสุขา','pickup',-16.6,-12.5,0,.6,-.025);
  for(let i=0;i<4;i++){const g=new T.Group();box(g,.14,.012,.20,0,0,0,ceramic);const p=item('กระดาษสุขา '+(i+1),'pickup',g,[.14,.012,.20],[0,-4-i,0],.05);p.root.visible=false;world.removeBody(p.body);sheets.push(p);}
  const light=new T.PointLight('#ffe7ba',3.2,9,2);light.position.set(-14.1,3.7,-14);scene.add(light);box(scene,3.5,.02,5.5,-15,4.29,-14,new T.MeshStandardMaterial({color:'#e1d8ba'}));
  // Use the supplied classic furniture as a compact reading/tea corner in the lounge.
  await asset('UtilityTeaTable','โต๊ะน้ำชาเลานจ์','table',-27.4,3,0,0,-.025);
  await asset('UtilityChair','เก้าอี้มุมน้ำชา','seat',-27.4,.95,0,0,-.025);
  await asset('UtilityTeapot','กาน้ำชา','pickup',-27.55,3,0,.6,.65);
  await asset('UtilityTeaCup','ถ้วยน้ำชา 1','pickup',-27.12,2.85,0,.15,.65);await asset('UtilityTeaCup','ถ้วยน้ำชา 2','pickup',-27.15,3.22,0,.15,.65);
  await asset('UtilityPlant','ไม้กระถางมุมน้ำชา','plant',-27.5,5.6,0,0,-.025);
  await asset('UtilityWindow','หน้าต่างเลานจ์','static',-28.91,3,0,0,.875);
  const glass=new T.Mesh(new T.PlaneGeometry(1.7,2.22),new T.MeshPhysicalMaterial({color:'#a2bec5',transparent:true,opacity:.28,roughness:.28,metalness:.15,side:T.DoubleSide,depthWrite:false}));glass.rotation.y=Math.PI/2;glass.position.set(-28.98,2.25,3);glass.userData.sightGlass=true;scene.add(glass);
  const meter=await asset('UtilityMeter','แผงไฟ G05','meter',19.5,-32.73,0,0,.75);fixtures.push(meter);meterLight=new T.PointLight('#ffe7b7',2.8,12,2);meterLight.position.set(19.5,3.8,-27);scene.add(meterLight);label('G05 · LOCAL POWER',19.5,3.3,-32.7,0,2.6);
 }
 function interact(o,held){if(!['toilet','paper','sink','meter'].includes(o.kind))return false;if(held){toast('วางของในมือก่อนใช้งาน');return true;}if(o.kind==='toilet'){if(o.timer>0){toast('กำลังกดน้ำ');return true;}o.timer=2;flushes++;toast('กดน้ำชักโครกแล้ว');}if(o.kind==='sink'){o.timer=3;washes++;toast('เปิดน้ำล้างมือ · น้ำจะหยุดเอง');}if(o.kind==='paper'){const p=sheets.find(p=>!p.root.visible);if(!p){toast('กระดาษชุดทดลองหมด · กด R เติมใหม่');return true;}p.root.visible=true;p.body.position.set(player.position.x,.95,player.position.z);p.body.velocity.setZero();world.addBody(p.body);p.body.wakeUp();papers++;grab(p);}if(o.kind==='meter'){meterOn=!meterOn;meterLight.intensity=meterOn?2.8:0;toast((meterOn?'เปิด':'ปิด')+'ไฟเสริมใน G05');}return true;}
 function prompt(o){return {toilet:'E · กดน้ำชักโครก',paper:'E · ดึงกระดาษมาใช้',sink:'E · เปิดน้ำล้างมือ',meter:'E · เปิด/ปิดไฟเสริม G05'}[o.kind]??'';}
 function update(dt){stallDoors?.update(dt);for(const o of fixtures){if(o.timer!==undefined){o.timer=Math.max(0,o.timer-dt);if(o.ripple){o.ripple.visible=o.timer>0;o.ripple.scale.setScalar(.6+(2-o.timer)%1);o.ripple.rotation.z+=dt*5;}o.streams?.forEach(n=>{n.visible=o.timer>0;});}}}
 function reset(){stallDoors?.reset();flushes=washes=papers=0;meterOn=true;if(meterLight)meterLight.intensity=2.8;for(const o of fixtures){o.timer=0;if(o.ripple)o.ripple.visible=false;o.streams?.forEach(n=>n.visible=false);}for(const p of sheets){p.root.visible=false;if(world.bodies.includes(p.body))world.removeBody(p.body);}}
 return {build,interact,prompt,update,reset,doors:()=>stallDoors?.status()??[],status:()=>({flushes,washes,papers,meterOn,active:fixtures.filter(o=>o.timer>0).map(o=>o.name)})};
}
