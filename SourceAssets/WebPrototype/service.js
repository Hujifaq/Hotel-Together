import * as THREE from 'three';
import * as C from 'cannon-es';
export function createServingSystem({scene,world,objects,toast,release,detachCargo}){
 const customers=[];const skin=new THREE.MeshStandardMaterial({color:'#d6a574',roughness:.8}),hair=new THREE.MeshStandardMaterial({color:'#634630',roughness:.9}),white=new THREE.MeshStandardMaterial({color:'#fff4df',roughness:.7}),dark=new THREE.MeshStandardMaterial({color:'#243540',roughness:.8});
 function status(){document.getElementById('serviceStatus').textContent=`เสิร์ฟอาหาร ${customers.filter(c=>c.served).length}/${customers.length}`;}
 function label(c){const cv=document.createElement('canvas');cv.width=768;cv.height=160;const ctx=cv.getContext('2d');ctx.fillStyle='#25495a';ctx.fillRect(0,0,768,160);ctx.fillStyle='#ffe4ac';ctx.font='bold 46px sans-serif';ctx.textAlign='center';ctx.fillText(c.served?'ขอบคุณครับ!':`${c.name} · ${c.order==='meal'?'ขออาหารจาน':'ขอซุป'}`,384,99);if(c.tag)c.tag.material.map.dispose();else{c.tag=new THREE.Sprite(new THREE.SpriteMaterial({transparent:true,depthWrite:false}));c.tag.position.y=1.94;c.tag.scale.set(1.8,.375,1);c.root.add(c.tag);}c.tag.material.map=new THREE.CanvasTexture(cv);c.tag.material.needsUpdate=true;}
 function add({name,order,chair,table}){
  const root=new THREE.Group();const shirt=new THREE.MeshStandardMaterial({color:order==='meal'?'#be865a':'#697eae',roughness:.8});
  function sphere(x,y,z,sx,sy,sz,m){const mesh=new THREE.Mesh(new THREE.SphereGeometry(1,16,10),m);mesh.position.set(x,y,z);mesh.scale.set(sx,sy,sz);mesh.castShadow=true;root.add(mesh);return mesh;}
  sphere(0,.92,0,.245,.29,.18,shirt);sphere(0,1.34,0,.235,.255,.225,skin);
  const cap=new THREE.Mesh(new THREE.SphereGeometry(.24,16,8,0,Math.PI*2,0,Math.PI*.48),hair);cap.position.set(0,1.39,0);root.add(cap);
  for(const x of [-.092,.092]){sphere(x,1.355,.20,.066,.077,.04,white);sphere(x,1.355,.235,.024,.029,.01,dark);}sphere(0,1.29,.224,.043,.045,.042,skin);
  for(const side of [-1,1]){sphere(side*.265,.84,.04,.062,.22,.07,shirt);sphere(side*.26,.68,.075,.060,.070,.065,skin);sphere(side*.10,.51,.13,.09,.085,.22,dark);sphere(side*.10,.29,.29,.064,.23,.066,dark);sphere(side*.10,.065,.34,.078,.055,.13,dark);}
  const body=new C.Body({mass:0,collisionFilterGroup:0,collisionFilterMask:0});body.position.set(chair.body.position.x,0,chair.body.position.z);body.quaternion.copy(chair.body.quaternion);world.addBody(body);root.position.copy(body.position);root.quaternion.copy(body.quaternion);scene.add(root);
  const c={name,kind:'customer',order,served:false,root,body,size:new THREE.Vector3(.58,1.6,.62),home:body.position.clone(),rotation:body.quaternion.clone(),table};root.userData.item=c;const proxy=new THREE.Mesh(new THREE.BoxGeometry(.58,1.55,.6),new THREE.MeshBasicMaterial({transparent:true,opacity:0,depthWrite:false,colorWrite:false}));proxy.position.y=.8;root.add(proxy);chair.customer=c;customers.push(c);objects.push(c);label(c);status();return c;
 }
 function serve(c,held){
  if(c.served){toast('โต๊ะนี้ได้รับอาหารแล้วครับ');return false;}
  let dish=held;
  if(held.isTray) dish=objects.find(o=>o.mealType===c.order&&Math.hypot(o.body.position.x-held.body.position.x,o.body.position.z-held.body.position.z)<.46&&o.body.position.y>held.body.position.y&&o.body.position.y-held.body.position.y<.35);
  if(!dish||dish.mealType!==c.order){toast(c.order==='meal'?'โต๊ะนี้ขออาหารจานครับ':'โต๊ะนี้ขอซุปครับ');return false;}
  detachCargo(dish);if(dish===held)release(false);
  const front=c.body.quaternion.vmult(new C.Vec3(0,0,1));dish.body.position.set(c.body.position.x+front.x*.50,c.table.body.position.y+c.table.size.y/2+dish.size.y/2+.01,c.body.position.z+front.z*.50);dish.body.quaternion.set(0,0,0,1);dish.body.velocity.setZero();dish.body.angularVelocity.setZero();dish.body.angularFactor.set(1,1,1);dish.body.aabbNeedsUpdate=true;dish.body.wakeUp();c.served=true;label(c);status();toast(`เสิร์ฟให้${c.name}แล้ว · ขอบคุณครับ!`);return true;
 }
 function reset(){for(const c of customers){c.served=false;label(c);}status();}
 return {add,serve,reset,get count(){return customers.filter(c=>c.served).length;},get customers(){return customers;}};
}
