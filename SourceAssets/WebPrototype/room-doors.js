import * as T from 'three';
import * as C from 'cannon-es';
export function createRoomDoors({scene,world,plan,objects,player,toast}){
 const doors=[];const wood=new T.MeshStandardMaterial({color:'#805b40',roughness:.65}),blue=new T.MeshStandardMaterial({color:'#476a70',roughness:.55}),metal=new T.MeshStandardMaterial({color:'#9ba6a5',roughness:.4,metalness:.45}),gold=new T.MeshStandardMaterial({color:'#ad8e51',roughness:.4,metalness:.6}),rim=new T.MeshStandardMaterial({color:'#e7dcc6',roughness:.7}),glass=new T.MeshPhysicalMaterial({color:'#9cb9be',transparent:true,opacity:.36,roughness:.23,metalness:.12,depthWrite:false});
 function box(parent,w,h,d,x,y,z,m){const n=new T.Mesh(new T.BoxGeometry(w,h,d),m);n.position.set(x,y,z);n.castShadow=true;n.receiveShadow=true;parent.add(n);return n;}
 function physical(w,h,d,x,y,z,rotation){const b=new C.Body({mass:0,shape:new C.Box(new C.Vec3(w/2,h/2,d/2)),collisionFilterGroup:1});b.position.set(x,y,z);b.quaternion.setFromAxisAngle(new C.Vec3(0,1,0),rotation);world.addBody(b);return b;}
 for(const [i,d] of plan.doors.entries()){
  const lift=['S','L'].includes(d.room),slide=lift||d.room==='G11',glazed=['G01','G04','G07','G10','G11'].includes(d.room),steel=lift||['G02','G03','G05','G06','A','B'].includes(d.room),double=lift||d.w>=1.6;
  const height=d.height??2.45,base=d.axis==='H'?0:Math.PI/2,outside=['S','E'].includes(d.side)?1:-1;
  const root=new T.Group();root.position.set(d.x,height/2,d.z);root.rotation.y=base;scene.add(root);
  const name='ประตู'+(plan.rooms.find(r=>r.id===d.room)?.label??d.room)+(d.room==='G03'?(d.side==='N'?' · ลานส่งของ':' · ทางเดิน'):'')+(d.room==='B'?(d.side==='N'?' · ทางเดิน':' · ห้องอาหาร'):'');
  const body=new C.Body({mass:0,collisionFilterGroup:0,collisionFilterMask:0});body.position.set(d.x,height/2,d.z);body.quaternion.setFromAxisAngle(new C.Vec3(0,1,0),base);world.addBody(body);
  const o={file:'RoomDoor',name,kind:'door',root,body,size:new T.Vector3(d.w,height,.18),home:body.position.clone(),rotation:body.quaternion.clone(),doorId:'door-'+i,room:d.room,side:d.side,open:false,amount:0,leaves:[],slide};root.userData.item=o;objects.push(o);doors.push(o);
  for(const s of [-1,1]){box(root,.065,height+.10,.18,s*(d.w/2-.025),.03,0,lift?metal:rim);const offset=new C.Vec3(s*(d.w/2-.025),0,0);body.quaternion.vmult(offset,offset);physical(d.axis==='H'?.065:.18,height+.10,d.axis==='H'?.18:.065,d.x+offset.x,height/2+.03,d.z+offset.z,0);}
  box(root,d.w+.04,.08,.18,0,height/2+.05,0,lift?metal:rim);
  const count=double?2:1,width=(d.w-.15)/count;
  for(let j=0;j<count;j++){
   const side=count===1?-1:j===0?-1:1,leaf=new T.Group();scene.add(leaf);leaf.userData.item=o;const material=steel?metal:glazed?blue:wood;
   if(glazed){box(leaf,width,1.0,.065,0,-height/2+.52,0,material);for(const s of [-1,1])box(leaf,.075,height-.04,.065,s*(width/2-.0375),0,0,material);box(leaf,width,.11,.065,0,height/2-.085,0,material);box(leaf,width,.065,.065,0,-height/2+1.05,0,material);box(leaf,width-.15,height-1.18,.016,0,.48,0,glass);}else{box(leaf,width,height-.04,.065,0,0,0,material);if(!steel)for(const y of [-.55,.4]){box(leaf,width-.18,.045,.025,0,y-.34,.047,gold);box(leaf,width-.18,.045,.025,0,y+.34,.047,gold);for(const s of [-1,1])box(leaf,.035,.68,.025,s*(width/2-.09),y,.047,gold);}if(['G05','G06'].includes(d.room))for(let k=0;k<5;k++)box(leaf,width-.24,.018,.015,0,-.78+k*.065,.044,blue);}
   if(lift)box(leaf,.014,height-.06,.006,-side*(width/2-.007),0,.036,blue);
   if(!lift)for(const s of [-1,1])box(leaf,.025,.27,.045,-side*(width/2-.10),-.18,s*.065,gold);
   const moving=new C.Body({type:C.Body.KINEMATIC,mass:0,shape:new C.Box(new C.Vec3(width/2,(height-.04)/2,.0325)),collisionFilterGroup:1,collisionFilterMask:1|2|4});world.addBody(moving);
   o.leaves.push({root:leaf,body:moving,side,width,closed:count===1?0:side*width/2,hinge:side*(d.w/2-.075),angle:0});
  }
  if(lift){for(const s of [-1,1]){const button=box(root,.16,.31,.07,s*(d.w/2+.20),.1,outside*.145,blue);const arrow=box(root,.045,.045,.012,s*(d.w/2+.20),.12,outside*.188,gold);button.userData.item=o;arrow.userData.item=o;}o.lift=true;}
  o.config={...d,height,base,outside};apply(o,0);
 }
 function pose(o,l,a){const {base}=o.config;let x,angle;if(o.slide){x=l.closed+l.side*a*(o.config.w/2+.06);angle=base;}else{angle=base-l.side*o.config.outside*a*Math.PI/2;const vector=new C.Vec3(-l.side*l.width/2,0,0),q=new C.Quaternion();q.setFromAxisAngle(new C.Vec3(0,1,0),angle);const shifted=q.vmult(vector),hinge=new C.Quaternion();hinge.setFromAxisAngle(new C.Vec3(0,1,0),base);const anchor=hinge.vmult(new C.Vec3(l.hinge,0,0));return {position:new C.Vec3(o.config.x+anchor.x+shifted.x,o.config.height/2,o.config.z+anchor.z+shifted.z),angle};}const dx=Math.cos(base)*x,dz=-Math.sin(base)*x;return {position:new C.Vec3(o.config.x+dx,o.config.height/2,o.config.z+dz),angle};}
 function apply(o,a){for(const l of o.leaves){const p=pose(o,l,a);l.body.position.copy(p.position);l.body.quaternion.setFromAxisAngle(new C.Vec3(0,1,0),p.angle);l.body.aabbNeedsUpdate=true;l.body.updateAABB();l.root.position.copy(l.body.position);l.root.quaternion.copy(l.body.quaternion);}world.broadphase.dirty=true;}
 function obstructed(o,a){const actors=[player,...objects.filter(p=>p.body.mass>0&&world.bodies.includes(p.body)).map(p=>p.body)];for(const l of o.leaves){const p=pose(o,l,a),q=new C.Quaternion();q.setFromAxisAngle(new C.Vec3(0,1,0),p.angle);const min=new C.Vec3(),max=new C.Vec3();l.body.shapes[0].calculateWorldAABB(p.position,q,min,max);for(const b of actors){if(b.aabbNeedsUpdate)b.updateAABB();const v=b.aabb;if(min.x< v.upperBound.x+.025&&max.x>v.lowerBound.x-.025&&min.y<v.upperBound.y&&max.y>v.lowerBound.y&&min.z<v.upperBound.z+.025&&max.z>v.lowerBound.z-.025)return true;}}return false;}
 function interact(o){if(o.kind!=='door')return false;o.open=!o.open;o.warned=false;toast((o.open?'เปิด':'ปิด')+o.name+(o.lift?' · ประตูลิฟต์ยังไม่เชื่อมการเดินทางขึ้นชั้น':''));return true;}
 function update(dt){for(const o of doors){const target=o.open?1:0;if(Math.abs(target-o.amount)<.001)continue;const a=T.MathUtils.clamp(o.amount+Math.sign(target-o.amount)*dt*.85,0,1);if(obstructed(o,a)){if(!o.warned){toast('มีคนหรือสิ่งของขวางประตู · ขยับออกก่อนครับ');o.warned=true;}continue;}o.amount=a;o.warned=false;apply(o,a);}}
 function reset(){for(const o of doors){o.open=false;o.amount=0;apply(o,0);}}
 return {interact,update,reset,status:()=>doors.map(o=>({id:o.doorId,name:o.name,room:o.room,side:o.side,width:o.config.w,height:o.config.height,open:o.open,amount:o.amount,sliding:o.slide,position:[o.config.x,o.config.height/2,o.config.z]}))};
}
