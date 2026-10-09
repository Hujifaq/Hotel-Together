import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import * as C from 'cannon-es';
import { buildLobby } from './lobby.js';
import { createServingSystem } from './service.js';
import { createLuggagePush } from './luggage-push.js';
import { loadImported } from './imported-assets.js';
import { createLaundry } from './laundry.js';
import { createRoomDoors } from './room-doors.js';
import { createWallProps } from './wall-props.js';
import { createRestroom } from './restroom.js';
const $=id=>document.getElementById(id), canvas=$('game');
const scene=new THREE.Scene(); scene.background=new THREE.Color('#abcad4');scene.fog=new THREE.Fog('#c3d6d9',42,85);
const camera=new THREE.PerspectiveCamera(67,innerWidth/innerHeight,.10,120);camera.rotation.order='YXZ';
const low=new URLSearchParams(location.search).has("low");const renderer=new THREE.WebGLRenderer({canvas,antialias:true});renderer.setSize(innerWidth,innerHeight);renderer.setPixelRatio(low?1:Math.min(devicePixelRatio,1.6));renderer.shadowMap.enabled=!low;renderer.shadowMap.type=THREE.PCFSoftShadowMap;renderer.outputColorSpace=THREE.SRGBColorSpace;renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.05;
scene.add(new THREE.HemisphereLight('#fff0d9','#ac9477',1.65));const sun=new THREE.DirectionalLight('#ffe4bf',2.7);sun.position.set(-6,13,15);sun.castShadow=true;sun.shadow.mapSize.set(1024,1024);Object.assign(sun.shadow.camera,{left:-34,right:20,top:25,bottom:-25,near:1,far:60});sun.shadow.normalBias=.045;sun.shadow.bias=-.0003;scene.add(sun);
const world=new C.World({gravity:new C.Vec3(0,-9.81,0)});world.allowSleep=true;world.solver.iterations=16;world.broadphase=new C.SAPBroadphase(world);world.defaultContactMaterial.friction=.45;world.defaultContactMaterial.restitution=.08;
const propMaterial=new C.Material({friction:.55,restitution:.08});const objects=[], solids=[], puddles=[], keys=new Set(), ray=new THREE.Raycaster(), pointer=new THREE.Vector2(0,0);
let hasPlayed=false,running=false, ready=false, drag=false, cleaning=false, held=null, grip=null, pushing=null, sitting=null, focused=null, yaw=0,pitch=-.05, toastTime=0, cleanCount=0;
const done={pickup:false,cart:false,sit:false,mop:false};
const tasks={pickup:'หยิบและวางสิ่งของ',cart:'เข็นรถไปอีกตำแหน่ง',sit:'ลองนั่งเก้าอี้',mop:'ถูแอ่งน้ำทั้ง 3 จุด'};
function updateTasks(){ $('tasks').innerHTML=Object.entries(tasks).map(([k,v])=>`<div class="task ${done[k]?'done':''}">${done[k]?'✓':'○'} ${v}</div>`).join('');$('progress').style.width=Object.values(done).filter(Boolean).length/4*100+'%';}
updateTasks();
function toast(s){$('toast').textContent=s;$('toast').style.display='block';toastTime=3;}
function mat(color,roughness=.65,metalness=0){return new THREE.MeshStandardMaterial({color,roughness,metalness});}
function meshBox(w,h,d,x,y,z,material){const m=new THREE.Mesh(new THREE.BoxGeometry(w,h,d),material);m.position.set(x,y,z);m.castShadow=true;m.receiveShadow=true;scene.add(m);return m;}
function collider(w,h,d,x,y,z){const body=new C.Body({mass:0,material:propMaterial,shape:new C.Box(new C.Vec3(w/2,h/2,d/2)),collisionFilterGroup:1});body.position.set(x,y,z);body.aabbNeedsUpdate=true;body.updateAABB();world.addBody(body);solids.push(body);return body;}
function structure(w,h,d,x,y,z,m){meshBox(w,h,d,x,y,z,m);return collider(w,h,d,x,y,z);}
function sign(text,x,y,z,w=2.2){const c=document.createElement('canvas');c.width=768;c.height=256;const ctx=c.getContext('2d');ctx.fillStyle='#25495a';ctx.fillRect(0,0,768,256);ctx.strokeStyle='#dabc78';ctx.lineWidth=10;ctx.strokeRect(12,12,744,232);ctx.fillStyle='#f1d39a';ctx.font='bold 58px sans-serif';ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText(text,384,128);const t=new THREE.CanvasTexture(c);t.colorSpace=THREE.SRGBColorSpace;const m=new THREE.Mesh(new THREE.PlaneGeometry(w,w/3),new THREE.MeshBasicMaterial({map:t}));m.position.set(x,y,z);scene.add(m);return m;}
const architecture=buildLobby({scene,world,meshBox,structure,collider,mat,sign,low});const carpet=architecture.carpet;
const player=new C.Body({mass:75,material:new C.Material({friction:0,restitution:0}),allowSleep:false,linearDamping:.0,fixedRotation:true,collisionFilterGroup:4,collisionFilterMask:1|2});player.addShape(new C.Sphere(.30),new C.Vec3(0,-.50,0));player.addShape(new C.Sphere(.30),new C.Vec3(0,.40,0));player.addShape(new C.Box(new C.Vec3(.28,.45,.28)));player.position.set(0,.82,5.8);player.updateMassProperties();world.addBody(player);
const hand=new C.Body({type:C.Body.KINEMATIC,allowSleep:false,collisionFilterGroup:8,collisionFilterMask:0});world.addBody(hand);
const loader=new GLTFLoader();const templates=new Map();
async function asset(file,name,kind,x,z,rot=0,mass=0,floorY=0){if(!templates.has(file))templates.set(file,/^(Laundry|Bathroom|Utility)/.test(file)?loadImported(file,loader):loader.loadAsync(`assets/HotelTogether-${file}.glb`));const gltf=await templates.get(file);const visual=gltf.scene.clone(true);visual.updateMatrixWorld(true);const bounds=new THREE.Box3().setFromObject(visual),size=bounds.getSize(new THREE.Vector3()),center=bounds.getCenter(new THREE.Vector3());const root=new THREE.Group();visual.position.sub(center);root.add(visual);root.position.set(x,floorY+size.y/2+.025,z);root.rotation.y=rot;visual.traverse(n=>{if(n.isMesh){n.castShadow=true;n.receiveShadow=true;}});scene.add(root);
const body=new C.Body({mass,material:propMaterial,position:new C.Vec3(x,floorY+size.y/2+.025,z),linearDamping:.35,angularDamping:.65,collisionFilterGroup:mass?2:1,collisionFilterMask:1|2|4});body.quaternion.setFromAxisAngle(new C.Vec3(0,1,0),rot);
const o={file,name,kind,root,body,size,home:body.position.clone(),rotation:body.quaternion.clone(),used:false};root.userData.item=o;if(!["static","table"].includes(kind)){const proxy=new THREE.Mesh(new THREE.BoxGeometry(size.x,kind==='folding'?.043:size.y,size.z),new THREE.MeshBasicMaterial({transparent:true,opacity:0,depthWrite:false,colorWrite:false}));proxy.userData.interactionProxy=true;if(kind==='folding')proxy.position.y=-size.y/2+.6985;root.add(proxy);}
if(file==='LaundryFoldTable'){body.addShape(new C.Box(new C.Vec3(size.x/2,.0215,size.z/2)),new C.Vec3(0,-size.y/2+.6985,0));body.addShape(new C.Box(new C.Vec3(.055,.09,size.z/2)),new C.Vec3(-size.x/2+.055,-size.y/2+.81,0));for(const sx of [-1,1])for(const sz of [-1,1])body.addShape(new C.Box(new C.Vec3(.045,.338,.045)),new C.Vec3(sx*(size.x/2-.08),-size.y/2+.338,sz*(size.z/2-.08)));}else if(file==='KitchenCounter'||file==='KitchenStove'){body.addShape(new C.Box(new C.Vec3(size.x*.46,size.y/2,size.z*.46)));}else if(file==='StockPot'){body.addShape(new C.Box(new C.Vec3(.145,.015,.145)),new C.Vec3(0,-size.y/2+.015,0));body.addShape(new C.Box(new C.Vec3(.16,.13,.16)),new C.Vec3(0,-size.y/2+.16,0));}else if(file==='StorageRack'){for(const y of [.22,.76,1.30,1.84])body.addShape(new C.Box(new C.Vec3(.70,.02,.31)),new C.Vec3(0,-size.y/2+y,0));for(const x of [-.67,.67])for(const z of [-.275,.275])body.addShape(new C.Box(new C.Vec3(.0215,size.y/2,.0215)),new C.Vec3(x,0,z));}else if(file==='MealPlate'){o.mealType='meal';body.addShape(new C.Box(new C.Vec3(.145,.011,.145)),new C.Vec3(0,-size.y/2+.011,0));}else if(file==='SoupBowl'){o.mealType='soup';body.addShape(new C.Box(new C.Vec3(.11,.05,.11)),new C.Vec3(0,-size.y/2+.05,0));}else if(file==='ServingTray'){o.isTray=true;body.addShape(new C.Box(new C.Vec3(.33,.012,.19)),new C.Vec3(0,-size.y/2+.012,0));for(const x of [-.317,.317])body.addShape(new C.Box(new C.Vec3(.011,.022,.175)),new C.Vec3(x,-size.y/2+.035,0));for(const z of [-.174,.174])body.addShape(new C.Box(new C.Vec3(.324,.022,.010)),new C.Vec3(0,-size.y/2+.035,z));}else if(file==='DiningChair'){o.diningSeat=true;body.addShape(new C.Box(new C.Vec3(.245,.035,.235)),new C.Vec3(0,-size.y/2+.475,0));body.addShape(new C.Box(new C.Vec3(.2325,.145,.04)),new C.Vec3(0,-size.y/2+.728,-.215));for(const sx of [-1,1])for(const sz of [-1,1])body.addShape(new C.Box(new C.Vec3(.0175,.216,.0175)),new C.Vec3(sx*.19,-size.y/2+.216,sz*.175));}else if(file==='DiningTableRound'){body.addShape(new C.Cylinder(.80,.80,.05,24),new C.Vec3(0,-size.y/2+.735,0));body.addShape(new C.Cylinder(.35,.35,.075,16),new C.Vec3(0,-size.y/2+.0375,0));body.addShape(new C.Box(new C.Vec3(.075,.31,.075)),new C.Vec3(0,-size.y/2+.38,0));}else if(file.startsWith('DiningTable')){body.addShape(new C.Box(new C.Vec3(size.x/2,.025,size.z/2)),new C.Vec3(0,-size.y/2+.735,0));for(const sx of [-1,1])for(const sz of [-1,1])body.addShape(new C.Box(new C.Vec3(file==='DiningTableLong'?.0325:.0275,.35,file==='DiningTableLong'?.0325:.0275)),new C.Vec3(sx*(size.x/2-(file==='DiningTableLong'?.09:.075)),-size.y/2+.35,sz*(size.z/2-(file==='DiningTableLong'?.09:.075))));}else if(kind==='plant'){body.addShape(new C.Box(new C.Vec3(.38,.285,.38)),new C.Vec3(0,-size.y/2+.285,0));}else if(kind==='cart'){body.addShape(new C.Box(new C.Vec3(size.x/2,file==='Gold-Trolley'?.05:file==='DishCart'?.0175:.09,size.z/2)),new C.Vec3(0,-size.y/2+(file==='Gold-Trolley'?.258:file==='DishCart'?.20:.16),0));for(const h of (file==='Gold-Trolley'?[]:file==='DishCart'?[.72]:[.40,.78]))if(h<size.y)body.addShape(new C.Box(new C.Vec3(size.x*.37,.035,size.z*.40)),new C.Vec3(0,-size.y/2+h,0));for(const sx of [-1,1])for(const sz of [-1,1])body.addShape(new C.Box(new C.Vec3(.035,size.y*.40,.035)),new C.Vec3(sx*size.x*.37,0,sz*size.z*.4));for(const sx of [-1,1])for(const sz of [-1,1])body.addShape(new C.Box(new C.Vec3(.07,.07,.07)),new C.Vec3(sx*size.x*.37,-size.y/2+.075,sz*size.z*.4));body.angularFactor.set(0,1,0);body.linearDamping=.65;
}else if(kind==='seat'){body.addShape(new C.Box(new C.Vec3(size.x/2,.09,size.z/2)),new C.Vec3(0,-size.y/2+.47,0));body.addShape(new C.Box(new C.Vec3(size.x/2,.20,.055)),new C.Vec3(0,size.y/2-.20,-size.z/2+.055));for(const sx of [-1,1])body.addShape(new C.Box(new C.Vec3(.055,.27,size.z/2)),new C.Vec3(sx*(size.x/2-.06),-.08,0));
}else if(kind==='table'){body.addShape(new C.Box(new C.Vec3(size.x/2,.07,size.z/2)),new C.Vec3(0,size.y/2-.07,0));for(const sx of [-1,1])for(const sz of [-1,1])body.addShape(new C.Box(new C.Vec3(.05,size.y/2,.05)),new C.Vec3(sx*(size.x/2-.08),0,sz*(size.z/2-.08)));
}else{body.addShape(new C.Box(new C.Vec3(Math.max(.035,size.x*.46),size.y*.49,Math.max(.035,size.z*.46))));}
body.aabbNeedsUpdate=true;body.updateAABB();world.addBody(body);
if(kind==='lamp'){o.glowMaterials=[];visual.traverse(n=>{if(n.isMesh&&/Lamp_(Bulb|Shade)/.test(n.name)){n.material=n.material.clone();n.material.emissive.set('#ffd392');n.material.emissiveIntensity=.18;o.glowMaterials.push(n.material);}});o.lampLight=new THREE.PointLight('#ffe1ad',2.4,3,2);o.lampLight.position.set(0,.02,0);root.add(o.lampLight);setLamp(o,true);}
if(kind==='bell'){o.rings=0;visual.traverse(n=>{if(n.name==='Bell_Button'){o.bellButton=n;o.buttonRest=n.position.y;}});}
objects.push(o);return o;}
const serving=createServingSystem({scene,world,objects,toast,release,detachCargo});
const luggagePush=createLuggagePush({scene,world,player,camera,objects,hand,toast});
const laundry=createLaundry({scene,world,objects,player,camera,toast,release,grab});
const roomDoors=createRoomDoors({scene,world,plan:architecture.floorPlan.ground,objects,player,toast});
const wallProps=createWallProps({scene,world,objects});
const restroom=createRestroom({scene,world,objects,player,toast,grab,collider});
let mop;
async function load(){const definitions=[["LobbyBench3","โซฟาล็อบบี้","seat",-5.5,1.3,0,0],["LobbyChair","เก้าอี้ล็อบบี้","seat",-6.3,4.7,3.141592653589793,0],["LobbyChair","เก้าอี้ล็อบบี้","seat",-4.8,4.7,3.141592653589793,0],["CoffeeTable","โต๊ะกลางล็อบบี้","table",-5.5,3,0,0],["SideTable","โต๊ะข้าง","table",-7.2,1.3,0,0],["ReceptionCounter","เคาน์เตอร์เช็กอิน","static",7.1,-3.6,1.5707963267948966,0],["ReceptionCounter","เคาน์เตอร์บริการ","static",7.1,2.5,1.5707963267948966,0],["HousekeepingCart","รถเข็นทำความสะอาด","cart",-6.5,-4.3,0,24],["Gold-Trolley","รถเข็นกระเป๋า","cart",2.6,3.8,0,24],["Blue-Suitcase","กระเป๋า","pickup",3.4,4.1,0.3,4],["Mop","ไม้ม็อบ","mop",-7.7,-3.5,0,1.5],["MopBucket","ถังน้ำ","pickup",-7.7,-2.4,0,4],["WetFloorSign","ป้ายพื้นเปียก","pickup",-0.8,0.5,-0.3,2],["Vacuum","เครื่องดูดฝุ่น","pickup",-8,-5.8,0,6],["LobbyBench3","โซฟาเลานจ์","seat",-24,0.5,0,0],["CoffeeTable","โต๊ะกลางเลานจ์","table",-24,2.3,0,0],["LobbyChair","เก้าอี้เลานจ์","seat",-24.8,4.2,3.141592653589793,0],["LobbyChair","เก้าอี้เลานจ์","seat",-23.2,4.2,3.141592653589793,0],["SideTable","โต๊ะข้างเลานจ์","table",-22.5,0.5,0,0],["LobbyBench3","โซฟาเลานจ์","seat",-18,0.5,0,0],["CoffeeTable","โต๊ะกลางเลานจ์","table",-18,2.3,0,0],["LobbyChair","เก้าอี้เลานจ์","seat",-18.8,4.2,3.141592653589793,0],["LobbyChair","เก้าอี้เลานจ์","seat",-17.2,4.2,3.141592653589793,0],["SideTable","โต๊ะข้างเลานจ์","table",-16.5,0.5,0,0],["LobbyBench3","โซฟาเลานจ์","seat",-12,0.5,0,0],["CoffeeTable","โต๊ะกลางเลานจ์","table",-12,2.3,0,0],["LobbyChair","เก้าอี้เลานจ์","seat",-12.8,4.2,3.141592653589793,0],["LobbyChair","เก้าอี้เลานจ์","seat",-11.2,4.2,3.141592653589793,0],["SideTable","โต๊ะข้างเลานจ์","table",-10.5,0.5,0,0]];
let count=0;await Promise.all(definitions.map(async d=>{const o=await asset(...d);if(o.kind==='mop')mop=o;$('loading').textContent=`โหลดโมเดล ${++count}/${definitions.length}`;}));const decor=[];
for(const [x,z] of [[-7.6,5.9],[7.7,5.9],[-9.65,-6.4],[-26,5.6],[-15,5.7]])decor.push(asset('PalmPlanter','ต้นปาล์ม','plant',x,z,0,0,-.025));
for(const [x,z] of [[-8,-.4],[-27,-3.7]])decor.push(asset('SortingBin','ถังแยกขยะ','static',x,z,0,0,-.025));
function onTable(file,label,supportName,dx=0,dz=0){const t=objects.find(o=>o.name===supportName);return asset(file,label,file==='TableLamp'?'lamp':file==='CheckInBell'?'bell':'static',t.body.position.x+dx,t.body.position.z+dz,0,0,t.body.position.y+t.size.y/2-.025+.004);}
decor.push(onTable('CheckInBell','กระดิ่งเช็กอิน','เคาน์เตอร์เช็กอิน',-.25,.85));
decor.push(onTable('TableLamp','โคมไฟเคาน์เตอร์','เคาน์เตอร์บริการ',.10,-.75));
decor.push(onTable('TableLamp','โคมไฟโต๊ะข้าง','โต๊ะข้าง'));
decor.push(onTable('TablePlant','ต้นไม้บนโต๊ะ','โต๊ะกลางล็อบบี้'));
for(const t of objects.filter(o=>o.name==='โต๊ะกลางเลานจ์'))decor.push(asset('TablePlant','ต้นไม้เลานจ์','static',t.body.position.x,t.body.position.z,0,0,t.body.position.y+t.size.y/2-.025+.004));
await Promise.all(decor);
await Promise.all([
asset('CreamSuitcase','กระเป๋าสีครีม','pickup',1.1,4.7,0,5),
asset('RedSuitcase','กระเป๋าใบเล็กสีแดง','pickup',2.6,3.8,0,3,.31),
asset('DuffelBag','กระเป๋าสะพาย','pickup',-2.2,5.1,.2,2),
asset('HotelParcel','กล่องพัสดุ','pickup',5.5,1.4,-.1,2.5)
 ]);
const dining=[];
for(const [x,z,id] of [[16,1.5,'A1'],[23,1.5,'B1'],[16,-7,'A2'],[23,-7,'B2']]){
 dining.push(['DiningTable4','โต๊ะ '+id,'table',x,z,0]);
 for(const [dx,dz,rot] of [[-.36,-.78,0],[.36,-.78,0],[-.36,.78,Math.PI],[.36,.78,Math.PI]])dining.push(['DiningChair','เก้าอี้อาหาร','seat',x+dx,z+dz,rot]);
}
for(const [x,z,id] of [[16,5,'A3'],[23,5,'B3'],[16,-13.5,'A4'],[23,-13.5,'B4']]){dining.push(['DiningTable2','โต๊ะ '+id,'table',x,z,0]);for(const side of [-1,1])dining.push(['DiningChair','เก้าอี้อาหาร','seat',x+side*.81,z,side<0?Math.PI/2:-Math.PI/2]);}
for(const z of [-3,-10])dining.push(['DiningSideboard','ตู้เก็บจาน','static',26.5,z,-Math.PI/2]);
dining.push(['DiningTableLong','โต๊ะยาว L1','table',16,-3,0]);
for(const dx of [-.85,0,.85])for(const side of [-1,1])dining.push(['DiningChair','เก้าอี้โต๊ะยาว','seat',16+dx,-3+side*.94,side<0?0:Math.PI]);
for(const [x,z,id] of [[23,-3,'R1'],[16,-10.5,'R2']]){
 dining.push(['DiningTableRound','โต๊ะกลม '+id,'table',x,z,0]);
 for(const [dx,dz,r] of [[0,-1.12,0],[1.12,0,-Math.PI/2],[0,1.12,Math.PI],[-1.12,0,Math.PI/2]])dining.push(['DiningChair','เก้าอี้โต๊ะกลม '+id,'seat',x+dx,z+dz,r]);
}
await Promise.all(dining.map(d=>asset(...d,0,-.025)));
await Promise.all([
asset('PrepTable','โต๊ะเตรียมอาหาร','table',8,-25,0,0,-.025),asset('PrepTable','โต๊ะเตรียมซุป','table',12,-25,0,0,-.025),
asset('KitchenStove','เตาครัว','static',4,-32.45,0,0,-.025),asset('KitchenSink','อ่างล้างจาน','static',7,-32.45,0,0,-.025),asset('KitchenFridge','ตู้เย็น','static',14.5,-32.4,0,0,-.025),
asset('ServingTray','ถาดเสิร์ฟ','pickup',8,-25,0,.8,.9),asset('MealPlate','จานอาหาร','pickup',7.87,-25,0,.35,.93),asset('SoupBowl','ถ้วยซุป','pickup',8.18,-25,0,.3,.93),
asset('ServingTray','ถาดสำรอง','pickup',12,-25,0,.8,.9),asset('MealPlate','จานอาหารสำรอง','pickup',11.87,-25,0,.35,.93),asset('SoupBowl','ถ้วยซุปสำรอง','pickup',12.18,-25,0,.3,.93),
asset('PalmPlanter','ต้นปาล์มห้องอาหาร','plant',25,5.5,0,0,-.025),asset('PalmPlanter','ต้นปาล์มห้องอาหาร','plant',26,-14.5,0,0,-.025),asset('PalmPlanter','ต้นปาล์มห้องอาหาร','plant',10.3,-9,0,0,-.025)
]);
// Cooking wall and cold-storage zone. Keep the central service route clear.
await Promise.all([
 ...[2.65,5.35,8.6,10.2].map(x=>asset('KitchenCounter','เคาน์เตอร์ครัว','static',x,-32.45,0,0,-.025)),
 asset('RangeHood','เครื่องดูดควัน','static',4,-32.45,0,0,2.025),
 asset('StorageRack','ชั้นวัตถุดิบ A','static',16.55,-29,-Math.PI/2,0,-.025),asset('StorageRack','ชั้นวัตถุดิบ B','static',16.55,-26.5,-Math.PI/2,0,-.025),
 asset('StockPot','หม้อครัว','pickup',3.80,-32.28,0,2.5,.928),asset('FryingPan','กระทะ','pickup',5.35,-32.30,0,1.4,.9),
 asset('VegCrate','ลังผัก A','pickup',16.55,-29.35,-Math.PI/2,4,.215),asset('VegCrate','ลังผัก B','pickup',16.55,-28.65,-Math.PI/2,4,.215),
 asset('RiceSack','กระสอบข้าว A','pickup',16.55,-29.35,-Math.PI/2,8,.755),asset('RiceSack','กระสอบข้าว B','pickup',16.55,-28.65,-Math.PI/2,8,.755),
 asset('FoodBox','กล่องอาหาร A','pickup',16.55,-29.35,-Math.PI/2,3,1.295),asset('FoodBox','กล่องอาหาร B','pickup',16.55,-28.65,-Math.PI/2,3,1.295),
 asset('FoodBox','กล่องอาหาร C','pickup',16.55,-26.85,-Math.PI/2,3,.215),asset('FoodBox','กล่องอาหาร D','pickup',16.55,-26.15,-Math.PI/2,3,.215),
 asset('VegCrate','ลังผักสำรอง','pickup',13.2,-32.4,0,4,-.025),asset('RiceSack','กระสอบข้าวสำรอง','pickup',12.25,-32.4,0,8,-.025),
 asset('StockPot','หม้อสำรอง','pickup',8.55,-32.45,0,2.5,.90)
]);
// Dining service set: repeat templates, preserve place settings used by the serving targets.
await Promise.all([
 asset('BuffetCounter','เคาน์เตอร์บุฟเฟต์','static',26.4,-6,-Math.PI/2,0,-.025),
 ...[-5,-6,-7].map((z,i)=>asset('ChafingDish','ถาดบุฟเฟต์ '+(i+1),'static',26.4,z,-Math.PI/2,0,.930)),
 asset('DrinkDispenser','เครื่องจ่ายน้ำ','static',26.5,-10,-Math.PI/2,0,.930),
 asset('DishCart','รถเข็นเก็บจาน','cart',24,-11,-Math.PI/2,18,-.025),
 asset('PrepTable','โต๊ะหั่นเตรียมวัตถุดิบ','table',4,-25,0,0,-.025),
 asset('PrepBoardSet','เขียงเตรียมอาหาร','pickup',3.8,-25,0,1,.90),
 asset('MixingBowl','ชามผสม','pickup',4.3,-25,0,.65,.90),
 asset('OilBottle','ขวดน้ำมัน','pickup',4.48,-25.20,0,.45,.90),
 asset('DishRack','ตะแกรงพักจาน','static',10.2,-32.45,0,0,.900),
 asset('PlateStack','จานสะอาดซ้อน','pickup',2.65,-32.45,0,1.6,.90)
]);
const settingJobs=[];
for(const t of objects.filter(o=>/^โต๊ะ (A|B)|^โต๊ะยาว |^โต๊ะกลม /.test(o.name))){
 const x=t.body.position.x,z=t.body.position.z,top=t.body.position.y+t.size.y/2-.025;
 settingJobs.push(asset('TableMenu','เมนู '+t.name,'static',x+.18,z,0,0,top),asset('NapkinHolder','กระดาษเช็ดปาก '+t.name,'static',x-.17,z,0,0,top));
 if(t.size.x>1.0)settingJobs.push(asset('SeasoningSet','เครื่องปรุง '+t.name,'pickup',x+(t.name.startsWith('โต๊ะยาว')?.60:0),z+(t.name.startsWith('โต๊ะยาว')?0:.26),0,.35,top));
 const places=t.name.startsWith('โต๊ะยาว')?[[-.85,-.29],[0,-.29],[.85,-.29],[-.85,.29],[0,.29],[.85,.29]]:t.name.startsWith('โต๊ะกลม')?[[0,-.48],[.48,0],[0,.48],[-.48,0]]:t.size.x>1.0?[[-.36,-.26],[.36,.26]]:[[-.22,0],[.22,0]];
 places.forEach(([dx,dz],i)=>{
  // Customer A1 is served at dx -.36/dz -.28: glass offset avoids that plate landing.
  const round=t.name.startsWith('โต๊ะกลม');const cupDx=dx+(round?dz/ .48*.18:(dx<0?-.12:.12)),cupDz=dz+(round?-dx/.48*.18:(['โต๊ะ A1','โต๊ะ B1'].includes(t.name)&&i===0?.26:0));
  settingJobs.push(asset('WaterCup','แก้วน้ำ '+t.name+' '+(i+1),'pickup',x+cupDx,z+cupDz,0,.2,top));
  if(t.name!=='โต๊ะ A1'&&t.name!=='โต๊ะ B1'&&t.size.x>1.0)settingJobs.push(asset('CutlerySet','ช้อนส้อม '+t.name+' '+(i+1),'pickup',x+dx,z+dz,0,.18,top));
 });
}
await Promise.all(settingJobs);
for(const [name,order,x,tableName] of [['แขก A1','meal',15.64,'โต๊ะ A1'],['แขก B1','soup',22.64,'โต๊ะ B1']])serving.add({name,order,chair:objects.find(o=>o.diningSeat&&Math.abs(o.body.position.x-x)<.02&&Math.abs(o.body.position.z-.72)<.02),table:objects.find(o=>o.name===tableName)});
await laundry.build(asset);
await restroom.build(asset);
ready=true;$('start').disabled=false;$('start').textContent='เข้าเล่น';$('loading').textContent='พร้อมแล้ว · ลองเล่นด้วยคีย์บอร์ดและเมาส์';}
for(const [x,z,r] of [[-1,1.5,.66],[1,-2,.72],[-3,-4,.62]]){const mesh=new THREE.Mesh(new THREE.CircleGeometry(r,40),new THREE.MeshStandardMaterial({color:'#4fbed4',transparent:true,opacity:.64,roughness:.12,metalness:.15,depthWrite:false}));mesh.rotation.x=-Math.PI/2;mesh.position.set(x,.018,z);scene.add(mesh);const p={mesh,x,z,r,progress:0};puddles.push(p);for(let i=0;i<4;i++){const ripple=new THREE.Mesh(new THREE.RingGeometry(r*(.22+i*.17),r*(.23+i*.17),40),new THREE.MeshBasicMaterial({color:'#b7f3fa',transparent:true,opacity:.3,depthWrite:false,side:THREE.DoubleSide}));ripple.rotation.x=-Math.PI/2;ripple.position.set(x,.021,z);scene.add(ripple);p.ripples??=[];p.ripples.push(ripple);}}
function look(){camera.rotation.set(pitch,yaw,0,'YXZ');}
function direction(){return camera.getWorldDirection(new THREE.Vector3());}
function detachCargo(o){if(o.cargoLink){world.removeConstraint(o.cargoLink);o.cargoLink=null;o.carrier=null;o.body.wakeUp();}}
function release(throwIt=false){if(!held)return;world.removeConstraint(grip);const o=held;for(const dish of objects.filter(d=>d.carrier===o))detachCargo(dish);o.body.angularFactor.set(1,1,1);o.body.collisionFilterMask=1|2|4;o.body.wakeUp();if(throwIt){const d=direction();o.body.velocity.set(d.x*6,Math.max(1,d.y*6),d.z*6);}else{o.body.velocity.scale(.3,o.body.velocity);}done.pickup=true;held=null;grip=null;updateTasks();toast(throwIt?'โยนแล้ว':'วางแล้ว');}
function grab(o){wallProps.detach(o);held=o;if(o.mealType||o.isTray){o.body.quaternion.set(0,0,0,1);o.body.angularVelocity.setZero();o.body.angularFactor.set(0,0,0);}if(o.kind==="mop"){o.body.quaternion.set(0,0,0,1);o.body.position.y=Math.max(o.body.position.y,o.size.y/2+.02);o.body.angularVelocity.setZero();}o.body.wakeUp();o.body.collisionFilterMask=1|2;const d=direction();hand.position.copy(o.body.position);hand.velocity.setZero();hand.quaternion.copy(o.body.quaternion);grip=new C.LockConstraint(hand,o.body,{maxForce:170});world.addConstraint(grip);if(o.isTray){for(const dish of objects.filter(d=>d.mealType&&Math.hypot(d.body.position.x-o.body.position.x,d.body.position.z-o.body.position.z)<.33&&d.body.position.y>o.body.position.y&&d.body.position.y-o.body.position.y<.20)){dish.body.wakeUp();dish.carrier=o;dish.cargoLink=new C.LockConstraint(o.body,dish.body,{maxForce:60,collideConnected:false});world.addConstraint(dish.cargoLink);}}toast(o.kind==='mop'?'ถือไม้ม็อบแล้ว · เล็งแอ่งน้ำแล้วกดเมาส์ซ้ายค้าง':'Q วาง · Space โยน');}
function stand(){if(!sitting)return;const s=sitting;const local=new C.Vec3(0,0,(s.diningSeat?-1:1)*(s.size.z/2+.65)),offset=s.body.quaternion.vmult(local);player.position.set(s.body.position.x+offset.x,.82,s.body.position.z+offset.z);player.velocity.setZero();player.collisionFilterMask=1|2;sitting=null;toast('ลุกจากเก้าอี้แล้ว');}
let bellAudio=null;
function setLamp(o,on){o.lampOn=on;o.lampLight.intensity=on?2.4:0;o.glowMaterials.forEach(m=>m.emissiveIntensity=on?.18:0);}
function ringBell(o){o.rings++;o.bellPulse=.18;toast('กริ๊ง · เรียกพนักงานเช็กอิน');try{bellAudio??=new (window.AudioContext||window.webkitAudioContext)();bellAudio.resume().then(()=>{const now=bellAudio.currentTime;for(const [hz,volume] of [[1568,.14],[2352,.05],[3920,.02]]){const osc=bellAudio.createOscillator(),gain=bellAudio.createGain();osc.type='sine';osc.frequency.setValueAtTime(hz,now);gain.gain.setValueAtTime(.001,now);gain.gain.linearRampToValueAtTime(volume,now+.008);gain.gain.exponentialRampToValueAtTime(.001,now+.75);osc.connect(gain);gain.connect(bellAudio.destination);osc.start(now);osc.stop(now+.8);}}).catch(()=>toast('เบราว์เซอร์ยังไม่อนุญาตเสียง'));}catch{toast('เบราว์เซอร์นี้ไม่รองรับเสียงกระดิ่ง');}}
function interact(){if(!running)return;if(sitting){stand();return;}if(focused&&roomDoors.interact(focused))return;if(focused&&laundry.interact(focused,held))return;if(focused&&restroom.interact(focused,held))return;if(held&&focused?.kind==='customer'){serving.serve(focused,held);return;}if(held){release();return;}if(pushing){luggagePush.end();pushing=null;toast('ปล่อยรถเข็นแล้ว');return;}if(!focused)return;const o=focused;
if(o.kind==='customer'){toast(o.served?'ขอบคุณครับ!':o.order==='meal'?'ขออาหารจานครับ':'ขอซุปครับ');}
else if(o.kind==='lamp'){setLamp(o,!o.lampOn);toast(o.lampOn?'เปิดโคมไฟแล้ว':'ปิดโคมไฟแล้ว');}
else if(o.kind==='bell')ringBell(o);
else if(o.kind==='seat'){if(o.customer){toast('ที่นั่งนี้มีแขกอยู่ครับ');return;}if(o.diningSeat){const front=o.body.quaternion.vmult(new C.Vec3(0,0,1));yaw=Math.atan2(-front.x,-front.z);pitch=-.22;}sitting=o;player.velocity.setZero();player.collisionFilterMask=0;done.sit=true;updateTasks();toast('นั่งแล้ว · E ลุก');}
else if(o.kind==='cart'){if(o.file==='Gold-Trolley'){if(!luggagePush.begin(o,yaw))return;pitch=Math.min(pitch,-.24);}pushing=o;o.body.wakeUp();o.pushStart=o.body.position.clone();toast('จับรถเข็นแล้ว · เดินเพื่อเข็น · E ปล่อย');}
else if(['pickup','mop'].includes(o.kind))grab(o);}
function reset(){restroom.reset();wallProps.reset();roomDoors.reset();laundry.reset();serving.reset();release();luggagePush.end();pushing=null;if(sitting)stand();player.position.set(0,.82,5.8);player.velocity.setZero();yaw=0;pitch=-.05;for(const o of objects){if(o.kind==='lamp')setLamp(o,true);if(o.kind==='bell'){o.rings=0;o.bellPulse=0;}o.body.position.copy(o.home);o.body.quaternion.copy(o.rotation);o.body.velocity.setZero();o.body.angularVelocity.setZero();o.body.wakeUp();}for(const p of puddles){p.progress=0;p.mesh.visible=true;p.mesh.scale.setScalar(1);p.ripples.forEach(m=>{m.visible=true;m.scale.setScalar(1);});}cleanCount=0;Object.keys(done).forEach(k=>done[k]=false);updateTasks();toast('เริ่มห้องทดลองใหม่แล้ว');}
function pause(){running=false;keys.clear();cleaning=false;drag=false;$('menu').style.display='flex';$('start').textContent='กลับเข้าเล่น';if(document.pointerLockElement)document.exitPointerLock();}
$('start').onclick=()=>{if(!ready)return;hasPlayed=true;running=true;$('menu').style.display='none';canvas.focus();try{const request=canvas.requestPointerLock();request?.catch(()=>toast('ลากเมาส์เพื่อมอง · หรือใช้ปุ่มลูกศร'));}catch{toast('ลากเมาส์เพื่อมอง');}};
canvas.tabIndex=0;
addEventListener('keydown',e=>{if(!running)return;if(['Space','ArrowUp','ArrowDown','ArrowLeft','ArrowRight'].includes(e.code))e.preventDefault();keys.add(e.code);if(e.repeat)return;if(e.code==='KeyE')interact();if(e.code==='KeyQ')release();if(e.code==='Space'&&held)release(true);if(e.code==='KeyR')reset();if(e.code==='Escape')pause();});
addEventListener('keyup',e=>keys.delete(e.code));addEventListener('blur',()=>{keys.clear();cleaning=false;});
canvas.addEventListener('mousedown',e=>{if(!running)return;drag=true;if(e.button===0)cleaning=true;});addEventListener('mouseup',()=>{drag=false;cleaning=false;});
addEventListener('mousemove',e=>{if(!running||(!document.pointerLockElement&&!drag))return;yaw-=e.movementX*.0024;pitch=THREE.MathUtils.clamp(pitch-e.movementY*.0024,-1.35,1.25);});
addEventListener('pointerlockchange',()=>{if(!document.pointerLockElement&&running)toast('เมาส์ปลดล็อก · ลากเมาส์เพื่อมอง หรือ Esc เปิดเมนู');});
addEventListener('resize',()=>{camera.aspect=innerWidth/innerHeight;camera.updateProjectionMatrix();renderer.setSize(innerWidth,innerHeight);});
function sync(){for(const o of objects){if(o.bellButton)o.bellButton.position.y=o.buttonRest-(o.bellPulse>0?.006:0);o.root.position.copy(o.body.position);o.root.quaternion.copy(o.body.quaternion);}}
function focus(){focused=null;let cartFallback=null;ray.setFromCamera(pointer,camera);ray.far=3.1;const hits=ray.intersectObjects(scene.children,true);for(const h of hits){let visible=true;for(let ancestor=h.object;ancestor;ancestor=ancestor.parent)if(!ancestor.visible){visible=false;break;}if(!visible)continue;if(h.object.userData.sightGlass||h.object.userData.reflectionFloor||h.object===carpet||puddles.some(p=>p.mesh===h.object||p.ripples.includes(h.object)))continue;let n=h.object;while(n&&!n.userData.item)n=n.parent;if(n&&(n.userData.item===held||(held&&n.userData.item.carrier===held)))continue;if(n&&h.object.userData.interactionProxy&&n.userData.item.kind==='cart'){cartFallback??=n.userData.item;continue;}if(n&&n.userData.item!==held)focused=n.userData.item;break;}focused??=cartFallback;
let text='';if(sitting)text='E · ลุกจากเก้าอี้';else if(pushing)text=`กำลังเข็น${pushing.name} · E ปล่อย`;else if(held)text=held.kind==='mop'?'เล็งแอ่งน้ำ + เมาส์ซ้ายค้าง · Q วาง':'E / Q วาง · Space โยน';else if(focused){if(focused.kind==='lamp')text=`E · ${focused.lampOn?'ปิด':'เปิด'}${focused.name}`;if(focused.kind==='bell')text='E · กดกระดิ่งเช็กอิน';if(focused.kind==='seat')text=`E · นั่ง${focused.name}`;if(focused.kind==='cart')text=`E · เข็น${focused.name}`;if(['mop','pickup'].includes(focused.kind))text=`E · หยิบ${focused.name}`;}
if(focused?.kind==='door')text='E · '+(focused.open?'ปิด':'เปิด')+focused.name;
if(focused&&laundry.prompt(focused,held))text=laundry.prompt(focused,held);
if(focused&&restroom.prompt(focused))text=restroom.prompt(focused);
if(focused?.kind==='customer')text=focused.served?'ได้รับอาหารแล้ว · ขอบคุณครับ':held?'E · เสิร์ฟอาหาร':`แขกต้องการ${focused.order==='meal'?'อาหารจาน':'ซุป'} · หยิบจากครัวมาเสิร์ฟ`;$('prompt').textContent=text;$('prompt').style.display=text?'block':'none';$('crosshair').style.color=focused?'#f5d39b':'#ffffffaa';}
function step(dt){for(const o of objects)if(o.bellPulse>0)o.bellPulse=Math.max(0,o.bellPulse-dt);yaw+=((keys.has('ArrowLeft')?1:0)-(keys.has('ArrowRight')?1:0))*dt*1.7;pitch=THREE.MathUtils.clamp(pitch+((keys.has('ArrowUp')?1:0)-(keys.has('ArrowDown')?1:0))*dt,-1.35,1.25);if(luggagePush.active)yaw=luggagePush.turn(yaw,dt);look();
if(sitting){const s=sitting;camera.position.set(s.body.position.x,1.28,s.body.position.z);player.position.set(s.body.position.x,.82,s.body.position.z);player.velocity.setZero();}
else{const f=(keys.has('KeyW')?1:0)-(keys.has('KeyS')?1:0),r=(keys.has('KeyD')?1:0)-(keys.has('KeyA')?1:0),speed=pushing?1.8:keys.has('ShiftLeft')?4:2.8;const v=new THREE.Vector3(r,0,-f);if(v.lengthSq())v.normalize().applyAxisAngle(new THREE.Vector3(0,1,0),yaw).multiplyScalar(speed);const a=1-Math.exp(-16*dt);player.velocity.x+=(v.x-player.velocity.x)*a;player.velocity.z+=(v.z-player.velocity.z)*a;camera.position.set(player.position.x,player.position.y+.80,player.position.z);}
if(held){const d=direction();const target=new C.Vec3(camera.position.x+d.x*1.35,camera.position.y+d.y*1.35-.3,camera.position.z+d.z*1.35);const delta=target.vsub(hand.position);const distance=delta.length();if(distance>5){release();}else{delta.scale(1/Math.max(dt,.001),hand.velocity);if(hand.velocity.length()>8)hand.velocity.scale(8/hand.velocity.length(),hand.velocity);hand.angularVelocity.setZero();held.body.wakeUp();}}
if(pushing&&luggagePush.active){const result=luggagePush.step(dt,yaw);if(result!==undefined){yaw=result;look();}if(!done.cart&&pushing.body.position.distanceTo(pushing.pushStart)>1){done.cart=true;updateTasks();}}
else if(pushing){const o=pushing;const d=new THREE.Vector3(-Math.sin(yaw),0,-Math.cos(yaw));const target=new C.Vec3(player.position.x+d.x*1.25,o.body.position.y,player.position.z+d.z*1.25);const error=target.vsub(o.body.position);if(error.length()>3.4){pushing=null;toast('ปล่อยรถเข็นเพราะอยู่ไกลเกินไป');}else{const force=error.scale(150);force.x-=o.body.velocity.x*55;force.z-=o.body.velocity.z*55;force.y=0;const l=force.length();if(l>250)force.scale(250/l,force);o.body.applyForce(force);o.body.wakeUp();const euler=new C.Vec3();o.body.quaternion.toEuler(euler,'YZX');const angle=Math.atan2(Math.sin(yaw-euler.y),Math.cos(yaw-euler.y));o.body.torque.y+=angle*35-o.body.angularVelocity.y*12;if(!done.cart&&o.body.position.distanceTo(o.pushStart)>1){done.cart=true;updateTasks();}}}
restroom.update(dt);wallProps.update();roomDoors.update(dt);laundry.update(dt);architecture.update(player,objects,dt);world.step(1/60,dt,5);sync();
if(!sitting)camera.position.set(player.position.x,player.position.y+.80,player.position.z);
if(held?.kind==='mop'&&cleaning){const d=direction();if(d.y<-.1){const t=-camera.position.y/d.y;const x=camera.position.x+d.x*t,z=camera.position.z+d.z*t;const p=puddles.find(p=>p.progress<1&&Math.hypot(p.x-x,p.z-z)<p.r&&Math.hypot(p.x-player.position.x,p.z-player.position.z)<2.5);if(p){p.progress=Math.min(1,p.progress+dt*.40);const scale=Math.max(.05,Math.sqrt(1-p.progress));p.mesh.scale.setScalar(scale);p.ripples.forEach(m=>m.scale.setScalar(scale));$('prompt').textContent=`กำลังถูพื้น ${Math.round(p.progress*100)}%`;if(p.progress===1){p.mesh.visible=false;p.ripples.forEach(m=>m.visible=false);cleanCount++;toast(`พื้นแห้งแล้ว ${cleanCount}/3 จุด`);if(cleanCount===3){done.mop=true;updateTasks();}}}}}
if(player.position.y<-3)reset();for(const o of objects)if(o.body.position.y<-3){o.body.position.copy(o.home);o.body.velocity.setZero();}
}
let frames=0;let previous=performance.now();function frame(now){frames++;const dt=Math.min(.05,(now-previous)/1000);previous=now;if(ready){if(running){focus();step(dt);}else{if(!hasPlayed){camera.position.set(0,1.65,5.8);look();}sync();}}if(toastTime>0){toastTime-=dt;if(toastTime<=0)$('toast').style.display='none';}luggagePush.update(running);renderer.render(scene,camera);requestAnimationFrame(frame);}requestAnimationFrame(frame);
load().catch(error=>{$('loading').textContent='โหลดไม่สำเร็จ: '+error.message;console.error(error);});
// Read-only status for verification; no gameplay shortcuts.
window.hotelStatus=()=>({doors:[...roomDoors.status(),...restroom.doors()],restroom:restroom.status(),ready,running,luggagePush:luggagePush.status(),laundry:laundry.status(),architecture:architecture.floorPlan,serveCount:serving.count,frames,keys:[...keys],held:held?.name??null,pushing:pushing?.name??null,sitting:sitting?.name??null,focused:focused?.name??null,cleanCount,audioState:bellAudio?.state??null,done:{...done},view:[yaw,pitch],player:[player.position.x,player.position.y,player.position.z],objects:objects.map(o=>({name:o.name,kind:o.kind,linenStage:o.linenStage,mealType:o.mealType,isTray:o.isTray,served:o.served,order:o.order,lampOn:o.lampOn,lightIntensity:o.lampLight?.intensity,rings:o.rings,quaternion:[o.body.quaternion.x,o.body.quaternion.y,o.body.quaternion.z,o.body.quaternion.w],position:[o.body.position.x,o.body.position.y,o.body.position.z]}))});
