"""Hotel Together / Housekeeping prop pack. A mop + stack of three folded towels.
Individual Blender-friendly meshes with PBR materials, Z up, meters scale.
"""
import math, json, os
import trimesh
import numpy as np
from scipy.interpolate import CubicSpline
root='/mnt/data'; s=trimesh.Scene();obj=[]
C={'wood':'#9C724C','wood_edge':'#806042','silver':'#778C98','blue':'#4C8FAF','mop':'#ECEBE6','mopshade':'#D9D8D2','towel':'#F5F4EF','towelshadow':'#DBDFDC','band':'#A2C5CF'}
def add(n,m,k):
 col=C[k][1:];rgba=[int(col[i:i+2],16) for i in (0,2,4)]+[255]
 m.visual=trimesh.visual.TextureVisuals(material=trimesh.visual.material.PBRMaterial(name='Prop_'+k,baseColorFactor=rgba,roughnessFactor=.74,metallicFactor=.2 if k=='silver' else 0,doubleSided=True))
 s.add_geometry(m,node_name=n,geom_name=n);obj.append((n,m));return m
def ball(n,p,r,k):
 m=trimesh.creation.uv_sphere(radius=1,count=[36,24]);m.apply_scale(r);m.apply_translation(p);return add(n,m,k)
def cyl_between(n,a,b,r,k,sections=24):
 a=np.array(a);b=np.array(b);v=b-a;length=np.linalg.norm(v)
 m=trimesh.creation.cylinder(radius=r,height=length,sections=sections)
 rot=trimesh.geometry.align_vectors([0,0,1],v/length);m.apply_transform(rot);m.apply_translation((a+b)/2)
 return add(n,m,k)
def cord(n,pts,r,k):
 pts=np.array(pts,float);verts=[];faces=[];S=10
 for i,p in enumerate(pts):
  direction=pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)];direction/=max(1e-7,np.linalg.norm(direction))
  u=np.cross(direction,[0,0,1]);
  if np.linalg.norm(u)<.01:u=np.cross(direction,[0,1,0])
  u=u/np.linalg.norm(u);v=np.cross(direction,u)
  for j in range(S):
   theta=j*2*math.pi/S;verts.append(p+r*(math.cos(theta)*u+math.sin(theta)*v))
  if i:
   for j in range(S):
    a=(i-1)*S+j;b=(i-1)*S+(j+1)%S;c=i*S+j;d=i*S+(j+1)%S
    faces.extend([(a,b,d),(a,d,c)])
 m=trimesh.Trimesh(vertices=verts,faces=faces,process=False)
 return add(n,m,k)
# mop stands on floor upright with a slight lean: no pose/hand linking
cyl_between('Mop_Wooden_Handle',(-.30,0,.22),(-.02,0,1.55),.023,'wood',32)
cyl_between('Mop_Grip_End',(-.035,0,1.45),(-.02,0,1.55),.027,'wood_edge',30)
cyl_between('Mop_Metal_Ferrule',(-.30,0,.19),(-.267,0,.340),.042,'silver',32)
ball('Mop_Blue_Collar',(-.310,0,.183),(.107,.078,.091),'blue')
ball('Mop_Fabric_Head',(-.315,0,.115),(.175,.085,.047),'mopshade')
# individual rag loops droop from the rim. Each is a distinct mesh for editable prop.
for j in range(24):
 a=j*2*math.pi/24
 x=-.315+.125*math.cos(a);y=.071*math.sin(a)
 ex=-.315+(.20+.045*math.cos(j*.79))*math.cos(a)
 ey=(.095+.025*math.sin(j*.72))*math.sin(a)
 zlow=.043+.013*math.cos(j*.51)
 cord(f'Mop_String_{j:02d}',[(x,y,.16),(x+.5*(ex-x), y+.50*(ey-y), .105),(ex,ey,zlow),(ex+.012*math.sin(a),ey,zlow+.007)],.012 if j%3 else .015,'mop' if j%4 else 'mopshade')
# helper rounded folded towels by layered ellipsoid edges and main cube
for j,(x,y,z,w,d,h) in enumerate([(0.32,0,.072,.41,.30,.080),(.32,0,.154,.41,.30,.080),(.32,0,.236,.41,.30,.080)]):
 m=trimesh.creation.box(extents=[w-.018,d-.018,h*.72]);m.apply_translation([x,y,z]);add(f'Folded_Towel_{j}',m,'towel')
 for side in [-1,1]:
  ball(f'Towel_Soft_Edge_{j}_{side}',(x+side*(w*.45),y,z),(.023,d*.52,h*.43),'towel')
 ball(f'Towel_Top_Soft_Layer_{j}',(x,y,z+h*.29),(w*.49,d*.48,h*.15),'towel')
 ball(f'Towel_Rolled_Edge_{j}',(x,y-d*.45,z),(.19,.025,h*.32),'towelshadow')
 # blue wrap band across uppermost towel only
 if j==2:
  m=trimesh.creation.box(extents=[.08,d+.006,h*.08]);m.apply_translation([x,y,z+h*.39]);add('Towel_Stack_Blue_Band',m,'band')
path=root+'/HotelTogether_Housekeeping_Mop_Towels_V1.glb';s.export(path)
print(path,'meshes',len(obj),'triangles',sum(len(m.faces) for _,m in obj))
