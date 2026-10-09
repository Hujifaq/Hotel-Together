import * as THREE from 'three';
import * as C from 'cannon-es';
// A physical grip anchor keeps the gold cart at the player's front. Collision checks
// reject blocked snap/sweep poses; the cart remains a dynamic body after release.
export function createLuggagePush({scene,world,player,camera,objects,hand,toast}){
 const anchor=new C.Body({type:C.Body.KINEMATIC,collisionFilterGroup:8,collisionFilterMask:0,allowSleep:false});world.addBody(anchor);
 const arms=new THREE.Group();arms.visible=false;scene.add(arms);let state=null,lastBlocked=null;
 const skin=new THREE.MeshStandardMaterial({color:'#d9aa7c',roughness:.78}),sleeve=new THREE.MeshStandardMaterial({color:'#214957',roughness:.9});
 const sides=[-1,1].map(side=>{
  const grip=new THREE.Group();
  const palm=new THREE.Mesh(new THREE.SphereGeometry(1,12,8),skin);palm.scale.set(.061,.044,.064);palm.position.set(.030,.012,0);grip.add(palm);
  for(const z of [-.034,0,.034]){const curve=new THREE.CatmullRomCurve3([new THREE.Vector3(.052,.028,z),new THREE.Vector3(-.022,.026,z),new THREE.Vector3(-.037,-.007,z),new THREE.Vector3(-.012,-.030,z)]);grip.add(new THREE.Mesh(new THREE.TubeGeometry(curve,8,.012,6,false),skin));}
  const thumb=new THREE.Mesh(new THREE.SphereGeometry(1,12,8),skin);thumb.scale.set(.035,.020,.025);thumb.position.set(.012,-.023,side*.056);grip.add(thumb);arms.add(grip);
  const upper=new THREE.Mesh(new THREE.CylinderGeometry(.062,.052,1,10),sleeve),lower=new THREE.Mesh(new THREE.CylinderGeometry(.040,.048,1,10),skin);arms.add(upper,lower);return {side,grip,upper,lower};
 });
 const Y=new THREE.Vector3(0,1,0),distance=1.12;
 function segment(mesh,a,b){const v=b.clone().sub(a);mesh.position.copy(a).add(b).multiplyScalar(.5);mesh.scale.y=v.length();mesh.quaternion.setFromUnitVectors(Y,v.normalize());}
 function pose(x,y,z,heading){return {p:new C.Vec3(x-Math.sin(heading)*distance,y,z-Math.cos(heading)*distance),q:new C.Quaternion().setFromAxisAngle(new C.Vec3(0,1,0),heading-Math.PI/2)};}
 function probeFor(o){const b=new C.Body({mass:0});o.body.shapes.forEach((s,i)=>{b.addShape(new C.Box(s.halfExtents.clone()),o.body.shapeOffsets[i].clone(),o.body.shapeOrientations[i].clone());});return b;}
 function clearPose(probe,p,q,excluded){probe.position.copy(p);probe.quaternion.copy(q);probe.updateAABB();const a=probe.aabb;
  for(const b of world.bodies){if(excluded.has(b)||!b.collisionFilterGroup||!(b.collisionFilterMask&2)||!b.shapes.length)continue;if(b.aabbNeedsUpdate)b.updateAABB();const c=b.aabb;
   if(Math.min(a.upperBound.x,c.upperBound.x)-Math.max(a.lowerBound.x,c.lowerBound.x)>.006&&Math.min(a.upperBound.y,c.upperBound.y)-Math.max(a.lowerBound.y,c.lowerBound.y)>.006&&Math.min(a.upperBound.z,c.upperBound.z)-Math.max(a.lowerBound.z,c.lowerBound.z)>.006){lastBlocked={name:objects.find(o=>o.body===b)?.name??'architecture',bounds:[c.lowerBound.toArray(),c.upperBound.toArray()]};return false;}
  }return true;
 }
 function sweep(probe,fromP,fromQ,toP,toQ,excluded){const angle=2*Math.acos(Math.min(1,Math.abs((fromQ.x*toQ.x+fromQ.y*toQ.y+fromQ.z*toQ.z+fromQ.w*toQ.w)))),n=Math.max(1,Math.ceil(fromP.distanceTo(toP)/.07),Math.ceil(angle/.10));for(let i=1;i<=n;i++){const t=i/n,p=new C.Vec3(),q=fromQ.slerp(toQ,t,new C.Quaternion());fromP.lerp(toP,t,p);if(!clearPose(probe,p,q,excluded))return false;}return true;}
 function begin(o,heading){const p=pose(player.position.x,o.body.position.y,player.position.z,heading),probe=probeFor(o),cargo=objects.filter(a=>a!==o&&a.body.mass>0&&a.body.position.y-a.size.y/2>o.body.position.y-o.size.y/2+.27&&a.body.position.y-a.size.y/2<o.body.position.y-o.size.y/2+.40&&Math.hypot(a.body.position.x-o.body.position.x,a.body.position.z-o.body.position.z)<.5),excluded=new Set([o.body,player,hand,anchor,...cargo.map(a=>a.body)]);
  let pathClear=sweep(probe,o.body.position,o.body.quaternion,p.p,o.body.quaternion,excluded);
  if(!pathClear)for(const side of [-1,1]){const offset=new C.Vec3(Math.cos(heading)*side*.32,0,-Math.sin(heading)*side*.32),a=o.body.position.vadd(offset),b=p.p.vadd(offset);if(sweep(probe,o.body.position,o.body.quaternion,a,o.body.quaternion,excluded)&&sweep(probe,a,o.body.quaternion,b,o.body.quaternion,excluded)&&sweep(probe,b,o.body.quaternion,p.p,o.body.quaternion,excluded)){pathClear=true;break;}}
  if(!pathClear||!sweep(probe,p.p,o.body.quaternion,p.p,p.q,excluded)){toast('พื้นที่ด้านหน้าไม่พอ · ขยับออกจากสิ่งกีดขวางแล้วลองอีกครั้ง');return false;}
  const inv=o.body.quaternion.inverse(),old=o.body.position.clone();for(const a of cargo){const local=inv.vmult(a.body.position.vsub(old));a.body.position.copy(p.p.vadd(p.q.vmult(local)));a.body.quaternion.copy(p.q.mult(inv.mult(a.body.quaternion)));a.body.velocity.setZero();a.body.angularVelocity.setZero();a.body.aabbNeedsUpdate=true;a.body.wakeUp();}
  o.body.position.copy(p.p);o.body.quaternion.copy(p.q);o.body.velocity.setZero();o.body.angularVelocity.setZero();o.body.aabbNeedsUpdate=true;o.body.wakeUp();anchor.position.copy(p.p);anchor.quaternion.copy(p.q);anchor.velocity.setZero();anchor.angularVelocity.setZero();const joint=new C.LockConstraint(anchor,o.body,{maxForce:2500,collideConnected:false});world.addConstraint(joint);state={o,heading,requestedHeading:heading,lastView:heading,joint,probe,excluded:new Set([o.body,player,hand,anchor,...cargo.map(a=>a.body)])};return true;
 }
 function end(){if(!state)return;world.removeConstraint(state.joint);state.o.body.velocity.scale(.35,state.o.body.velocity);state.o.body.angularVelocity.setZero();state.o.body.wakeUp();anchor.velocity.setZero();anchor.angularVelocity.setZero();state=null;arms.visible=false;}
 function turn(yaw,dt){if(!state)return yaw;state.requestedHeading+=Math.atan2(Math.sin(yaw-state.lastView),Math.cos(yaw-state.lastView));const delta=Math.atan2(Math.sin(state.requestedHeading-state.heading),Math.cos(state.requestedHeading-state.heading));state.lastView=state.heading+THREE.MathUtils.clamp(delta,-1.3*dt,1.3*dt);return state.lastView;}
 function step(dt,yaw){if(!state)return;const s=state,target=pose(player.position.x+player.velocity.x*dt,s.o.body.position.y,player.position.z+player.velocity.z*dt,yaw);
  if(!sweep(s.probe,s.o.body.position,s.o.body.quaternion,target.p,target.q,s.excluded)){player.velocity.x=0;player.velocity.z=0;anchor.position.copy(s.o.body.position);anchor.quaternion.copy(s.o.body.quaternion);anchor.velocity.setZero();anchor.angularVelocity.setZero();s.requestedHeading=s.heading;s.lastView=s.heading;return s.heading;}
  anchor.velocity.copy(target.p.vsub(anchor.position).scale(1/Math.max(dt,.001)));anchor.velocity.y=0;anchor.angularVelocity.set(0,Math.atan2(Math.sin(yaw-s.heading),Math.cos(yaw-s.heading))/Math.max(dt,.001),0);s.heading=yaw;s.o.body.wakeUp();return yaw;
 }
 function update(running){arms.visible=!!state&&running;if(!arms.visible)return;const {o,heading}=state;
  for(const a of sides){const local=new C.Vec3(.46,-o.size.y/2+1.08,-a.side*.16),p=o.body.position.vadd(o.body.quaternion.vmult(local));a.grip.position.set(p.x,p.y,p.z);a.grip.quaternion.copy(o.body.quaternion);
   const shoulder=new THREE.Vector3(player.position.x+a.side*Math.cos(heading)*.25,player.position.y+.47,player.position.z-a.side*Math.sin(heading)*.25),wrist=new THREE.Vector3(p.x,p.y,p.z),elbow=shoulder.clone().lerp(wrist,.5);elbow.y-=.12;segment(a.upper,shoulder,elbow);segment(a.lower,elbow,wrist);
  }
 }
 return {begin,end,turn,step,update,get active(){return !!state;},status:()=>({locked:!!state,lastBlocked,handsVisible:arms.visible,distance,grips:arms.visible?sides.map(a=>a.grip.position.toArray()):[]})};
}
