import * as THREE from 'three';
import * as C from 'cannon-es';
import { buildGroundFloor } from './ground.js';
import { decorateInterior } from './interior.js';
import { Reflector } from 'three/addons/objects/Reflector.js';
export function buildLobby({scene,world,meshBox,structure,collider,mat,sign,low}){
const cream=mat('#efdabd',.68),navy=mat('#2f5462',.65),brass=mat('#b99650',.29,.65),walnut=mat('#855c3e',.48),edge=mat('#ded5c4');
// A shared, softly blurred light probe adds restrained glass sheen without extra scene renders.
const glassProbe=new THREE.CubeTexture(Array.from({length:6},(_,i)=>{const c=document.createElement('canvas');c.width=c.height=128;const ctx=c.getContext('2d');const g=ctx.createLinearGradient(0,0,0,128);g.addColorStop(0,'#d6dfe0');g.addColorStop(.48,'#9aaeb4');g.addColorStop(1,'#77786e');ctx.fillStyle=g;ctx.fillRect(0,0,128,128);ctx.filter='blur(7px)';ctx.fillStyle='rgba(255,245,217,0.75)';if(i!==3){ctx.fillRect(12,20,104,5);ctx.fillRect(24,8,5,96);ctx.fillRect(93,8,5,96);}return c;}));glassProbe.colorSpace=THREE.SRGBColorSpace;glassProbe.needsUpdate=true;
const glass=new THREE.MeshPhysicalMaterial({color:'#bdcbd0',transparent:true,opacity:.28,roughness:.23,metalness:0,envMap:glassProbe,envMapIntensity:.7,clearcoat:.2,clearcoatRoughness:.25,depthWrite:false,side:THREE.DoubleSide});
const height=8.1,upper=4.5,archObjects=[];
function box(w,h,d,x,y,z,m,physical=false){const mesh=meshBox(w,h,d,x,y,z,m);archObjects.push(mesh);if(physical)collider(w,h,d,x,y,z);return mesh;}
function pane(w,h,x,y,z,rotation=0){const m=new THREE.Mesh(new THREE.PlaneGeometry(w,h),glass);m.position.set(x,y,z);m.rotation.y=rotation;m.userData.sightGlass=true;scene.add(m);return m;}
// A stripe on the wall itself avoids stacking a second mesh on the same face.
const wallMaterial=cream.clone();wallMaterial.onBeforeCompile=shader=>{shader.uniforms.wallTrimColor={value:brass.color};shader.vertexShader='varying float worldWallY;\n'+shader.vertexShader.replace('#include <worldpos_vertex>','#include <worldpos_vertex>\nworldWallY=(modelMatrix*vec4(transformed,1.0)).y;');shader.fragmentShader='varying float worldWallY;uniform vec3 wallTrimColor;\n'+shader.fragmentShader.replace('#include <color_fragment>','#include <color_fragment>\nif(worldWallY>=0.0&&worldWallY<0.11)diffuseColor.rgb=wallTrimColor;');};
function wall(w,h,d,x,y,z){return box(w,h,d,x,y,z,wallMaterial,true);}
// The plan uses x=20..38, y=26..40 for the lobby. World origin is its center (29,33).
const tile=document.createElement('canvas');tile.width=tile.height=512;const ctx=tile.getContext('2d');ctx.fillStyle='#e9dfca';ctx.fillRect(0,0,512,512);for(let j=0;j<2;j++)for(let i=0;i<2;i++){ctx.fillStyle=(i+j)%2?'#ece4d5':'#f5edde';ctx.fillRect(i*256+1,j*256+1,254,254);for(let k=0;k<14;k++){const a=(k*71+i*23+j*41)%256,b=(k*107+i*39)%256;ctx.strokeStyle='rgba(162,144,115,.09)';ctx.lineWidth=.5;ctx.beginPath();ctx.moveTo(i*256+a,j*256+b);ctx.bezierCurveTo(i*256+a+20,j*256+b-8,i*256+a+34,j*256+b+18,i*256+a+64,j*256+b+10);ctx.stroke();}}ctx.strokeStyle='#c3b8a3';ctx.lineWidth=2;[0,256,512].forEach(v=>{ctx.beginPath();ctx.moveTo(v,0);ctx.lineTo(v,512);ctx.stroke();ctx.beginPath();ctx.moveTo(0,v);ctx.lineTo(512,v);ctx.stroke();});
const texture=new THREE.CanvasTexture(tile);texture.colorSpace=THREE.SRGBColorSpace;texture.wrapS=texture.wrapT=THREE.RepeatWrapping;texture.repeat.set(9,7);texture.anisotropy=8;
const tileMat=new THREE.MeshStandardMaterial({map:texture,roughness:.21,metalness:.12});
const ground=buildGroundFloor({scene,structure,box,wall,pane,sign,cream,navy,brass,tileMat,mat});
structure(18,.2,8,0,-.1,11,mat('#d5d0c3',.7));
// Polished lobby floor: restrained planar reflection in normal quality, simple material in low mode.
if(!low){const shader={...Reflector.ReflectorShader,uniforms:THREE.UniformsUtils.clone(Reflector.ReflectorShader.uniforms)};shader.uniforms.tileMap={value:texture};shader.vertexShader=shader.vertexShader.replace('varying vec4 vUv;','varying vec4 vUv; varying vec2 floorUv;').replace('vUv = textureMatrix','floorUv = uv * vec2(9.0,7.0); vUv = textureMatrix');shader.fragmentShader=shader.fragmentShader.replace('varying vec4 vUv;','varying vec4 vUv; varying vec2 floorUv; uniform sampler2D tileMap;').replace('gl_FragColor = vec4( blendOverlay( base.rgb, color ), 1.0 );','vec3 stone = texture2D(tileMap, floorUv).rgb; stone = pow(stone, vec3(2.2)); gl_FragColor = vec4(mix(stone * 1.15, base.rgb, 0.24), 1.0);');const floor=new Reflector(new THREE.PlaneGeometry(18,14),{shader,textureWidth:768,textureHeight:512,clipBias:.003,multisample:0,color:0xded2b7});floor.rotation.x=-Math.PI/2;floor.position.y=.008;floor.userData.reflectionFloor=true;scene.add(floor);}
// Lobby front facade, full-height glazing and clear four-meter sliding entrance.
for(const side of [-1,1]){for(let i=0;i<3;i++){const x=side*(2+(i+.5)*7/3);pane(7/3-.10,7.8,x,4.05,7);box(.12,8.1,.16,side*(2+i*7/3),4.05,7,edge);}collider(7,8.1,.10,side*5.5,4.05,7);}
for(const y of [0,3.5,6,8.1])box(18,.13,.2,0,y,7,edge);
pane(4,4.5,0,5.85,7);box(.16,3.6,.20,-2.1,1.8,7,walnut);box(.16,3.6,.20,2.1,1.8,7,walnut);box(4.4,.16,.22,0,3.5,7,walnut);
const doors=[];for(const side of [-1,1]){const panel=pane(1.95,3.3,side,1.7,7.01);const trim=box(.045,3.3,.045,side*1.97,1.7,7.02,brass);const body=collider(1.95,3.3,.09,side,1.7,7);doors.push({side,panel,trim,body});}let open=0;
// Lounge windows, rear wall and hall portals. The broad lounge opening has columns instead of a wall.
for(let i=0;i<6;i++){const x=-29+(i+.5)*20/6;pane(3.22,3.8,x,2.25,7);box(.12,4.5,.18,-29+i*20/6,2.25,7,edge);}collider(20,4.5,.12,-19,2.25,7);
// Right-side restaurant glazing, keeping privacy behind reception and one broad restaurant portal.
wall(.22,4.5,5,9,2.25,-4.5);wall(.22,4.5,.2,9,2.25,-1.9);wall(.22,4.5,.2,9,2.25,-.1);box(.22,2.05,1.6,9,3.475,-1,cream,true);ground.audit.doors.push({room:'G10',side:'W',axis:'V',c:38,p:32,w:1.6,height:2.45,x:9,z:-1});wall(.22,4.5,2,9,2.25,6);for(const z of [1,3,4.5]){const w=z===4.5?.9:1.9;pane(w,3.4,8.98,2.25,z,Math.PI/2);collider(.10,4.5,w,9,2.25,z);box(.18,4.5,.1,9,2.25,z-w/2,edge);}box(.3,.30,14,9,4.5,0,cream);box(.22,3.6,14,9,6.3,0,cream);
for(let i=0;i<6;i++){const x=10.5+i*3;pane(2.85,3.55,x,2.25,7);box(.12,4.5,.16,9+i*3,2.25,7,edge);}collider(18,4.5,.12,18,2.25,7);

// Balcony and aligned west stair A. Structural surfaces and rails have collision.
structure(4,.22,18,-11,upper-.11,-2,mat('#e5dac6'));structure(18,.22,4,0,upper-.11,-9,mat('#e5dac6'));box(16,.18,8,-21,upper-.09,3,cream).castShadow=false;
function rail(x,z,length,alongZ=false){const m=pane(length,1.05,x,upper+.62,z,alongZ?Math.PI/2:0);m.material=glass;collider(alongZ?.1:length,1.2,alongZ?length:.1,x,upper+.6,z);box(alongZ?.09:length,.07,alongZ?length:.09,x,upper+1.16,z,brass);for(let i=-length/2;i<=length/2;i+=2){box(.075,1.17,.075,x+(alongZ?0:i),upper+.58,z+(alongZ?i:0),edge);}}
rail(-9,-2,18,true);rail(0,-7,18);rail(-11,7,4);wall(.18,3.6,5.5,-13,6.3,-8.25);wall(.18,3.6,10.5,-13,6.3,1.75);box(.18,1,2,-13,7.6,-4.5,cream);wall(18,3.6,.18,0,6.3,-11);wall(.18,3.6,4,9,6.3,-9);
// Smooth physical ramps under stair treads avoid snagging the cart/player on tiny risers.
function flight(x,startZ,endZ,startY,endY){const run=Math.abs(endZ-startZ),rise=endY-startY,angle=Math.atan2(rise,run),length=Math.hypot(run,rise);const body=new C.Body({mass:0,shape:new C.Box(new C.Vec3(.85,.06,length/2)),material:new C.Material({friction:0}),collisionFilterGroup:1});body.position.set(x,(startY+endY)/2-.06,(startZ+endZ)/2);body.quaternion.setFromAxisAngle(new C.Vec3(1,0,0),startZ>endZ?angle:-angle);body.aabbNeedsUpdate=true;body.updateAABB();world.addBody(body);for(let i=0;i<15;i++){const t=(i+.5)/15,z=startZ+(endZ-startZ)*t,y=startY+rise*(i+1)/15;box(1.7,.13,run/15,x,y-.065,z,edge);}for(const dx of [-.89,.89]){const handrail=box(.065,.065,length,x+dx,(startY+endY)/2+1.05,(startZ+endZ)/2,brass);handrail.rotation.x=startZ>endZ?angle:-angle;}return body;}
flight(-15.9,-6,-9.7,0,2.25);flight(-14.1,-9.7,-6,2.25,4.5);structure(4,.2,1.3,-15,2.15,-10.35,edge);structure(2,.22,2.1,-14,4.39,-4.95,edge);
collider(.13,1.15,3.2,-15,1.65,-7.9);box(.06,3.6,.06,-15,1.8,-7.9,brass);
// Stair B: plan x38..42/y16..22 => world x9..13/z-17..-11.
flight(9.9,-16,-12.3,0,2.25);flight(11.7,-12.3,-16,2.25,4.5);
structure(4,.2,1.3,11,2.15,-11.65,edge);structure(4,.22,3,11,4.39,-17.5,edge);
collider(.13,1.15,3.2,10.8,1.65,-14.1);box(.06,3.6,.06,10.8,1.8,-14.1,brass);
wall(.2,3.6,6,9,6.3,-14);wall(.2,3.6,6,13,6.3,-14);wall(4,3.6,.2,11,6.3,-11);
rail(11,-19,4);rail(9,-18,2,true);rail(13,-18,2,true);
box(4,.18,6,11,8.19,-14,cream).castShadow=false;
sign('STAIR B / LEVEL 2',11,6.4,-11.14,2.4).rotation.y=Math.PI;
wall(.2,3.6,6,-17,6.3,-8);wall(4,3.6,.2,-15,6.3,-11);rail(-12,-3.9,6);
// Upper roof and gallery beams.
box(18,.18,14,0,height+.09,0,cream).castShadow=false;box(18,.18,4,0,height+.09,-9,cream).castShadow=false;box(4,.18,18,-11,height+.09,-2,cream).castShadow=false;for(const z of [-5,1,5])box(18,.2,.22,0,height-.15,z,edge);
for(const [x,z] of [[-8.7,6.7],[8.7,6.7],[-8.7,-6.7],[8.7,-6.7]]){box(.35,height,.35,x,height/2,z,cream,true);box(.42,.25,.42,x,.13,z,walnut);}
// Suspended linear lights with warm emission, and a small number of actual lights.
const luminous=new THREE.MeshBasicMaterial({color:'#fff3d6'});for(const [x,z,len] of [[-20,3,6],[-13,3,4]]){const y=x<-9?4.1:5.8;box(len,.06,.09,x,y,z,luminous);for(const dx of [-len*.35,len*.35])box(.018,Math.max(.2,(x<-9?4.5:8.1)-y),.018,x+dx,((x<-9?4.5:8.1)+y)/2,z,edge);}
for(const [x,z] of [[-4,2],[5,-2],[-20,3]]){const light=new THREE.PointLight('#ffd19a',28,18,2);light.position.set(x,3.9,z);scene.add(light);}
// Reusable Blender palm planters are loaded with the other lobby assets.

const carpet=box(2.3,.012,5.8,4.2,.014,-.3,mat('#355764',.92));
sign('CHECK-IN  /  RECEPTION',0,2.7,0,3.8);const receptionSign=scene.children.at(-1);receptionSign.position.set(8.84,2.7,-3.8);receptionSign.rotation.y=-Math.PI/2;
sign('LOUNGE',-22,2.8,-.86,3);
// Crest banner on the balcony, borrowing the navy and gold palette from the reference.
const banner=document.createElement('canvas');banner.width=256;banner.height=384;const bc=banner.getContext('2d');bc.fillStyle='#244b5d';bc.fillRect(0,0,256,384);bc.strokeStyle='#d3b16b';bc.lineWidth=10;bc.strokeRect(12,12,232,360);bc.fillStyle='#e4c47e';bc.textAlign='center';bc.font='38px Georgia';bc.fillText('♛',128,74);bc.font='120px Georgia';bc.fillText('H',128,208);bc.font='25px sans-serif';bc.fillText('★★★★★',128,263);bc.font='18px sans-serif';bc.fillText('HOTEL TOGETHER',128,314);const bt=new THREE.CanvasTexture(banner);bt.colorSpace=THREE.SRGBColorSpace;const flag=new THREE.Mesh(new THREE.PlaneGeometry(1.2,1.8),new THREE.MeshStandardMaterial({map:bt,side:THREE.DoubleSide,roughness:.8}));flag.position.set(-8.85,4.5,-.8);flag.rotation.y=Math.PI/2;scene.add(flag);
// Outside skyline is visual backdrop. The playable boundary is the forecourt.
scene.background=new THREE.Color('#b7d9eb');for(let i=0;i<9;i++){const x=-30+i*7;const h=5+(i*7)%9;box(4,h,4,x,h/2,26+(i%3)*3,mat(i%2?'#a8bdca':'#bdc8d0',.95)).castShadow=false;}collider(18,1,.2,0,.5,15);collider(.2,1,8,-9,.5,11);collider(.2,1,8,9,.5,11);
decorateInterior({scene,audit:ground.audit,low});
// Read-only audit of broad horizontal slabs; adjacent edges do not count as overlap.
const slabs=scene.children.filter(n=>n.isMesh&&n.geometry?.type==='BoxGeometry'&&n.geometry.parameters.height<=.3&&!n.rotation.x&&!n.rotation.y&&!n.rotation.z).map(n=>{const p=n.geometry.parameters;return {x:n.position.x-p.width/2,z:n.position.z-p.depth/2,w:p.width,d:p.depth,top:n.position.y+p.height/2,bottom:n.position.y-p.height/2};});
const slabOverlaps=[];for(let i=0;i<slabs.length;i++)for(let j=i+1;j<slabs.length;j++){const a=slabs[i],b=slabs[j],area=Math.max(0,Math.min(a.x+a.w,b.x+b.w)-Math.max(a.x,b.x))*Math.max(0,Math.min(a.z+a.d,b.z+b.d)-Math.max(a.z,b.z));if(area>.05&&(Math.abs(a.top-b.top)<1e-6||Math.abs(a.bottom-b.bottom)<1e-6))slabOverlaps.push({a,b,area});}
ground.audit.slabOverlaps=slabOverlaps;
return {carpet,update(player,objects,dt){const nearby=Math.hypot(player.position.x,player.position.z-7)<3.4||objects.some(o=>o.body.mass>0&&Math.hypot(o.body.position.x,o.body.position.z-7)<2.8);open=THREE.MathUtils.damp(open,nearby?1:0,7,dt);for(const d of doors){const x=d.side*(1+open*2.15);d.panel.position.x=x;d.trim.position.x=x+d.side*.97;d.body.position.x=x;d.body.aabbNeedsUpdate=true;}world.broadphase.dirty=true;},floorPlan:{lobby:{width:18,depth:14,height:8.1},lounge:{width:20,depth:8},upperLevel:4.5,dining:{width:18,depth:24,area:408,notch:[4,6]},kitchen:{width:16,depth:12},serviceCorridorWidth:4,ground:ground.audit,stairs:{A:{x:-15,z:-8},B:{x:11,z:-14}}}};
}
