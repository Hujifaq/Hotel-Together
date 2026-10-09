import * as T from 'three';
const specs={
 BathroomToilet:{source:'Toilet',whole:true,height:1.15},
 BathroomPaper:{source:'Paper',whole:true,height:.23},
 BathroomBucket:{source:'Janitor',names:['SM_CleaningTools_Dmo_MI_Cleaning_Kit_B005_0'],height:.36},
 BathroomTool:{source:'Janitor',names:['SM_CleaningTools_Gmo_MI_Cleaning_Kit_B001_0'],height:1.1},
 UtilityMeter:{source:'Electric',whole:true,width:2.6},
 UtilityWindow:{source:'Window',whole:true,height:2.6},
 UtilityChair:{source:'Furniture',names:['Object_4'],straight:true,height:1.05},
 UtilityTeaTable:{source:'Furniture',names:['Object_18'],height:.65},
 UtilityTeapot:{source:'Furniture',names:['Object_20'],height:.23},
 UtilityTeaCup:{source:'Furniture',names:['Object_8'],height:.085},
 UtilityPlant:{source:'Furniture',names:['Object_24','Object_25'],height:1.2},
 LaundryWasher:{source:'Washing',names:['01_Washing-machine','01_Door','01_Drum'],height:1.0},
 LaundryDryingRack:{source:'Washing',names:['01_Clothes-dryer'],width:1.8},
 LaundryLinen:{source:'Washing',names:['02_Cloth'],width:.46},
 LaundryCabinet:{source:'TinyLiving',names:['TinyLiving_StubbyCubbyDresser_38'],height:1.6},
 LaundryShelf:{source:'TinyLiving',names:['TinyLiving_PowerTowerDresser_24'],height:1.65},
 LaundryFoldTable:{source:'TinyLiving',names:['TinyLiving_TinyDeskForTinyLiving_37'],height:.9},
 LaundrySofa:{source:'TinyLiving',names:['TinyLiving_LovelyLoveseat_12'],height:.92},
 LaundryChair:{source:'TinyLiving',names:['TinyLiving_LovelyArmchair_33'],height:1.0},
 LaundryCoffeeTable:{source:'TinyLiving',names:['TinyLiving_SirCumferenceCoffeeTable_11'],height:.50},
 LaundryLamp:{source:'TinyLiving',names:['TinyLiving_TheModernDeskLamp_23'],height:.40},
 LaundryDetergent:{source:'Cleaning',part:1,height:.40},LaundryCleaner:{source:'Cleaning',part:2,height:.28},LaundrySpray:{source:'Cleaning',part:0,height:.32}
};
const cache=new Map();
// Weld dense, untextured window surfaces into small spatial cells. Preserve materials and outlines.
function windowLOD(g){const pos=g.attributes.position;if(pos.count<2000)return g;g.computeBoundingBox();const extent=g.boundingBox.getSize(new T.Vector3()),cell=Math.max(extent.x,extent.y,extent.z)/128,min=g.boundingBox.min,lookup=new Map(),ids=new Uint32Array(pos.count),first=[],sum=[],counts=[];
 for(let i=0;i<pos.count;i++){const key=Math.round((pos.getX(i)-min.x)/cell)+','+Math.round((pos.getY(i)-min.y)/cell)+','+Math.round((pos.getZ(i)-min.z)/cell);let j=lookup.get(key);if(j===undefined){j=first.length;lookup.set(key,j);first.push(i);sum.push([0,0,0]);counts.push(0);}ids[i]=j;sum[j][0]+=pos.getX(i);sum[j][1]+=pos.getY(i);sum[j][2]+=pos.getZ(i);counts[j]++;}
 const index=g.index,indices=[];for(let i=0;i<(index?.count??pos.count);i+=3){const a=ids[index?index.getX(i):i],b=ids[index?index.getX(i+1):i+1],c=ids[index?index.getX(i+2):i+2];if(a!==b&&b!==c&&a!==c)indices.push(a,b,c);}const out=new T.BufferGeometry();for(const [name,a] of Object.entries(g.attributes)){const data=new a.array.constructor(first.length*a.itemSize);first.forEach((v,i)=>{for(let k=0;k<a.itemSize;k++)data[i*a.itemSize+k]=name==='position'?sum[i][k]/counts[i]:a.array[v*a.itemSize+k];});out.setAttribute(name,new T.BufferAttribute(data,a.itemSize,a.normalized));}out.setIndex(indices);out.computeVertexNormals();out.computeBoundingBox();out.computeBoundingSphere();return out;
}
function subset(g,part){const triangles=[],pos=g.attributes.position,ind=g.index;const limits=[-.0411698,.0250902];for(let i=0;i<ind.count;i+=3){const ids=[ind.getX(i),ind.getX(i+1),ind.getX(i+2)],z=ids.reduce((a,v)=>a+pos.getZ(v),0)/3,k=z<limits[0]?0:z<limits[1]?1:2;if(k===part)triangles.push(...ids);}
 const unique=[...new Set(triangles)],map=new Map(unique.map((v,i)=>[v,i])),out=new T.BufferGeometry();for(const [key,a] of Object.entries(g.attributes)){const data=new a.array.constructor(unique.length*a.itemSize);unique.forEach((v,i)=>{for(let j=0;j<a.itemSize;j++)data[i*a.itemSize+j]=a.array[v*a.itemSize+j];});out.setAttribute(key,new T.BufferAttribute(data,a.itemSize,a.normalized));}out.setIndex(triangles.map(v=>map.get(v)));out.computeBoundingBox();out.computeBoundingSphere();return out;
}
export async function loadImported(file,loader){const spec=specs[file];if(!spec)return null;if(!cache.has(spec.source))cache.set(spec.source,loader.loadAsync('assets/Imported-'+spec.source+'.glb'));const src=(await cache.get(spec.source)).scene;src.updateMatrixWorld(true);const group=new T.Group();
 if(spec.names){for(const name of spec.names){const node=src.getObjectByName(name);if(!node)throw Error('Missing supplied model component '+name);const clone=node.clone(true);node.matrixWorld.decompose(clone.position,clone.quaternion,clone.scale);if(spec.straight)clone.rotation.y=0;group.add(clone);}}
 else if(spec.whole){group.add(src.clone(true));}
 else{let mesh;src.traverse(n=>{if(n.isMesh&&!mesh)mesh=n;});const node=new T.Mesh(subset(mesh.geometry,spec.part),mesh.material);mesh.matrixWorld.decompose(node.position,node.quaternion,node.scale);group.add(node);}
 if(file==='UtilityWindow')group.traverse(n=>{if(n.isMesh)n.geometry=windowLOD(n.geometry);});
 group.updateMatrixWorld(true);
 if(file==='LaundryWasher'){for(const [name,pivotName] of [['01_Door','LaundryDoorPivot'],['01_Drum','LaundryDrumPivot']]){const node=group.getObjectByName(name),b=new T.Box3().setFromObject(node),c=b.getCenter(new T.Vector3()),pivot=new T.Group();pivot.name=pivotName;pivot.position.copy(c);if(name==='01_Door')pivot.position.x=b.min.x+.01;group.add(pivot);group.updateMatrixWorld(true);pivot.attach(node);}}
 const b=new T.Box3().setFromObject(group),size=b.getSize(new T.Vector3()),scale=spec.height?spec.height/size.y:spec.width/size.x;group.scale.multiplyScalar(scale);group.updateMatrixWorld(true);return {scene:group};
}
