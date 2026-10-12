"""Hotel Together - Employee 03: Housekeeping, Blender-importable 3D art prototype.
Female with brown bun, blue housekeeper tunic, white apron, heavy-lidded eyes,
no eyebrows, stout body. +Z up; front -Y. Generated sculpt-like meshes;
NOT rigged/retopologized/game-ready. Requires trimesh/numpy/scipy/scikit-image.
"""
import os, math, json, random
import numpy as np
import trimesh
from scipy.interpolate import CubicSpline
from skimage.measure import marching_cubes
O='/mnt/data';R=random.Random(3303);scene=trimesh.Scene();objects=[]
C={'skin':'#CF966F','skinlight':'#D8A583','skinshade':'#AD755D',
'sclera':'#F4F3EA','pupil':'#1C1B1A','eyeshadow':'#B88469','mouth':'#4A292D','lip':'#AF7668',
'hair':'#66452F','hair2':'#795239','hairlight':'#8A6040','hairdark':'#513523',
'blue':'#579BC0','bluelight':'#78B7D3','blueshade':'#397D9F','bluedark':'#2F6787',
'apron':'#F2F0E9','apronshadow':'#D9DCE0','apronstitch':'#CDD5D6','pants':'#294355',
'seam':'#203744','shoe':'#E8E6DF','sole':'#C7CBC8','shoeseam':'#B3B8B5',
'badge':'#E9F0EA','badgeblue':'#2E6D89','wood':'#9D6C46','woodshade':'#775038','cloth':'#F1F1EE',
'mopmetal':'#6F8797','mopblue':'#4B92BC','towelblue':'#ABC5D2','towelside':'#D1E0E3'}
rough={k:.82 for k in C};rough.update({'sclera':.29,'pupil':.29,'apron':.86,'shoe':.57,'hair':.80,'skin':.72,'mopmetal':.5})
def add(n,m,k):
 m.metadata['name']=n;h=C[k].strip('#');rgb=[int(h[i:i+2],16) for i in [0,2,4]]+[255]
 m.visual=trimesh.visual.TextureVisuals(material=trimesh.visual.material.PBRMaterial(name='Housekeeping_'+k,baseColorFactor=rgb,roughnessFactor=rough[k],metallicFactor=.6 if k=='metal' else .0,doubleSided=True))
 scene.add_geometry(m,geom_name=n,node_name=n);objects.append((n,m,k));return m

def ball(n,p,r,k,num=50):
 m=trimesh.creation.uv_sphere(radius=1,count=[num,max(24,num//2)]);m.apply_scale(r);m.apply_translation(p);return add(n,m,k)
def smoothmin(a,b,k):
 h=np.maximum(k-np.abs(a-b),0)/k;return np.minimum(a,b)-h*h*k*.25
def sdf(X,Y,Z,c,r):return (np.sqrt(((X-c[0])/r[0])**2+((Y-c[1])/r[1])**2+((Z-c[2])/r[2])**2)-1)*min(r)
def organic(n,k,bounds,res,ellipses,smooth=.09):
 a=[np.arange(u,v+res*.50,res) for u,v in bounds];X,Y,Z=np.meshgrid(*a,indexing='ij');D=None
 for p,r in ellipses:
  E=sdf(X,Y,Z,p,r);D=E if D is None else smoothmin(D,E,smooth)
 v,f,*_=marching_cubes(D,level=0,spacing=(res,res,res));v+=np.array([a[0] for a in bounds]);m=trimesh.Trimesh(vertices=v,faces=f,process=False)
 if m.volume<0:m.invert()
 return add(n,m,k)
def loft(n,section,k,steps=8,sides=60):
 s=np.asarray(section,float);ii=np.linspace(0,len(s)-1,(len(s)-1)*steps+1);ss=np.stack([CubicSpline(np.arange(len(s)),s[:,j])(ii) for j in range(5)],axis=1);v=[];f=[]
 for x,y,z,rx,ry in ss:
  for j in range(sides):
   a=2*np.pi*j/sides;v.append((x+rx*np.cos(a),y+ry*np.sin(a),z))
 for i in range(len(ss)-1):
  for j in range(sides):a=i*sides+j;b=i*sides+(j+1)%sides;c=a+sides;d=b+sides;f.extend(((a,b,d),(a,d,c)))
 v.extend((ss[0,:3],ss[-1,:3]));end=len(v)-1
 for j in range(sides):f.extend(((end-1,(j+1)%sides,j),(end,(len(ss)-1)*sides+j,(len(ss)-1)*sides+(j+1)%sides)))
 m=trimesh.Trimesh(vertices=v,faces=f,process=False)
 if m.volume<0:m.invert()
 return add(n,m,k)
def frame(t):
 t=t/max(1e-8,np.linalg.norm(t));a=np.array([0.,1.,0.]) if abs(t[1])<.85 else np.array([1.,0.,0.]);u=np.cross(t,a);u=u/max(1e-8,np.linalg.norm(u));return u,np.cross(t,u)
def sweep(n,way,radii,k,div=55,sect=12,flatten=1.0):
 w=np.asarray(way,float);dist=np.r_[0,np.cumsum(np.linalg.norm(np.diff(w,axis=0),axis=1))];dist/=max(dist[-1],1e-7)
 t=np.linspace(0,1,div);p=np.stack([CubicSpline(dist,w[:,j],bc_type='natural')(t) for j in range(3)],axis=1);r=CubicSpline(np.linspace(0,1,len(radii)),radii)(t);v=[];f=[];previous=None
 for i,pt in enumerate(p):
  tg=p[min(i+1,div-1)]-p[max(i-1,0)];tg=tg/max(np.linalg.norm(tg),1e-8);u,vv=frame(tg)
  if previous is not None:
   u=previous-np.dot(previous,tg)*tg;u=u/max(np.linalg.norm(u),1e-8);vv=np.cross(tg,u)
  previous=u
  for j in range(sect):
   a=2*np.pi*j/sect;v.append(pt+max(.0008,r[i])*(np.cos(a)*u+flatten*np.sin(a)*vv))
  if i:
   for j in range(sect):
    a=(i-1)*sect+j;b=(i-1)*sect+(j+1)%sect;c=i*sect+j;d=i*sect+(j+1)%sect;f.extend(((a,b,d),(a,d,c)))
 v.extend((p[0],p[-1]));n0=len(v)-2
 for j in range(sect):f.extend(((n0,(j+1)%sect,j),(n0+1,(div-1)*sect+j,(div-1)*sect+(j+1)%sect)))
 m=trimesh.Trimesh(vertices=v,faces=f,process=False)
 if m.volume<0:m.invert()
 return add(n,m,k)
def face(n,v,k):
 v=np.asarray(v,float);cent=v.mean(axis=0);f=[[j,(j+1)%len(v),len(v)] for j in range(len(v))];return add(n,trimesh.Trimesh(vertices=np.r_[v,cent[None,:]],faces=f,process=False),k)

def tube_ring(n,center,radius,r,k,N=46):
 xyz=[(center[0]+radius[0]*np.cos(i*2*np.pi/N),center[1]+radius[1]*np.sin(i*2*np.pi/N),center[2]) for i in range(N+1)]
 return sweep(n,xyz,[r]*(N+1),k,div=130,sect=12)

# ---------- HOUSEKEEPER: short heavy adult body, uniform designed for a game ---------
# Shoes chunky but not chibi, practical slip-on white trainers
for sign,L in [(-1,'L'),(1,'R')]:
 x=sign*.244
 ball('Work_Shoe_'+L,(x,-.136,.183),(.209,.292,.141),'shoe',56)
 ball('Shoe_Front_'+L,(x,-.248,.176),(.207,.170,.112),'shoe',48)
 ball('Rubber_Sole_'+L,(x,-.143,.090),(.218,.303,.057),'sole',52)
 ball('Shoe_Back_'+L,(x,.027,.229),(.166,.153,.123),'shoe',40)
 for i in range(2):
  z=.246+.039*i
  sweep('Trainer_Stitch_'+L+str(i),[(x-.100,-.188,z),(x,-.233,z+.006),(x+.100,-.188,z)],[.002,.004,.002],'shoeseam',30,10)
 loft('Trousers_'+L,[(x,.010,.331,.172,.152),(x,.019,.44,.185,.167),(x,.03,.63,.202,.182),(x,.035,.84,.224,.200),(x,.05,1.05,.248,.213)],'pants',8,66)
 sweep('Pant_Hem_'+L,[(x-.155,-.065,.359),(x,-.136,.354),(x+.155,-.065,.359)],[.003,.006,.003],'seam',45,10)

# Plump blue tunic with deliberately rounded shoulders and a belly silhouette
loft('Hotel_Housekeeping_Blue_Tunic',[(0,.045,1.042,.407,.351),(0,-.028,1.125,.490,.430),(0,-.070,1.33,.554,.465),(0,-.053,1.54,.554,.444),(0,.003,1.77,.536,.393),(0,.047,2.025,.476,.325),(0,.065,2.228,.333,.243)],'blue',9,78)
# small, short neck buried in rounded shoulder
ball('Neck',(0,.042,2.300),(.185,.183,.167),'skin',50)

# Simple white collar over blue housekeeping shirt
face('Left_Collar', [(-.127,-.284,2.245),(-.008,-.305,2.219),(-.057,-.349,2.157),(-.171,-.309,2.191)], 'apron')
face('Right_Collar',[(.127,-.284,2.245),(.008,-.305,2.219),(.057,-.349,2.157),(.171,-.309,2.191)],'apron')
sweep('Front_Shirt_Seam',[(0,-.325,2.16),(0,-.388,1.928),(0,-.446,1.790)],[.004,.005,.002],'blueshade',52,10)
for z,y in [(2.04,-.337),(1.93,-.389)]:ball('Button_'+str(z),(.00,y,z),(.022,.014,.022),'apron',26)
# Name badge on chest, white rectangle with raised trim and two text-stripes
badgecorn=[(.235,-.319,2.035),(.382,-.267,2.035),(.382,-.272,2.115),(.235,-.316,2.115)]
face('Employee_Badge',badgecorn,'badge')
sweep('Badge_Label_Top',[(.261,-.324,2.083),(.349,-.288,2.083)],[.003,.003],'badgeblue',22,8)
sweep('Badge_Label_Bottom',[(.261,-.325,2.063),(.330,-.300,2.063)],[.0025,.0025],'badgeblue',22,8)

# WHITE APRON: three-dimensional folded fabric following protruding tunic, not flat sticker.
# front curved cloth mesh with edge thickness, knots and seams
# apron skirt front frontmost (-Y). Five vertical panels w/ slight ripple.
def apron_cloth(name,zlist,widths,frontys,shaping=.010):
 U=36;V=24; verts=[]; faces=[]
 for row in range(V+1):
  t=row/V; z=zlist[0]*(1-t)+zlist[-1]*t
  w=widths[0]*(1-t)+widths[-1]*t
  y=frontys[0]*(1-t)+frontys[-1]*t
  for col in range(U+1):
   a=(col/U)*2-1
   xx=a*w
   yy=y+.165*a*a+shaping*math.cos(a*3.5*math.pi)*math.sin(math.pi*t)
   verts.append((xx,yy,z+.018*math.cos(a*4)*math.sin(math.pi*t)))
 for row in range(V):
  for col in range(U):
   a=row*(U+1)+col;b=a+1;c=a+U+1;d=c+1
   faces.extend([(a,b,d),(a,d,c)])
 add(name,trimesh.Trimesh(vertices=verts,faces=faces,process=False),'apron')
 # edge stitched lines following panel outlines
 for side in [-1,1]:
  points=[]
  for row in range(13):
   t=row/12;a=side
   z=zlist[0]*(1-t)+zlist[-1]*t
   w=widths[0]*(1-t)+widths[-1]*t
   y=frontys[0]*(1-t)+frontys[-1]*t+.165
   points.append([a*w,y-.010,z])
  sweep('Apron_Stitch_'+name+str(side),points,[.004]*len(points),'apronshadow',63,9)
 return
# main skirt, top near waist has belly curve, bottom reaches upper pants
apron_cloth('Apron_Draped_Skirt',[1.060,1.681],[.372,.361],[-.578,-.584],.016)
# bib upper portion tapered from a narrow chest to waist
apron_cloth('Apron_Chest_Bib',[1.665,2.119],[.264,.182],[-.596,-.368],.008)
# apron waistband crossing belly
sweep('Apron_Waistband',[(-.448,-.428,1.692),(-.249,-.534,1.696),(0,-.572,1.699),(.249,-.534,1.696),(.448,-.428,1.692)],[.015,.026,.027,.026,.015],'apron',80,14,flatten=.42)
# upper apron shoulder straps lie on blue uniform, taper to bib
for sign,L in [(-1,'L'),(1,'R')]:
 sweep('Apron_Strap_'+L,[(sign*.196,-.343,2.117),(sign*.288,-.305,2.194),(sign*.317,-.221,2.275)],[.016,.021,.015],'apron',48,16,flatten=.32)
# A true back bow with hanging strips
ball('Waist_Bow_Knot',(0,.382,1.677),(.067,.055,.053),'apron',32)
for sign,L in [(-1,'L'),(1,'R')]:
 ball('Apron_Bow_Loop_'+L,(sign*.121,.401,1.681),(.108,.041,.052),'apron',42)
 sweep('Bow_Ribbon_'+L,[(sign*.060,.401,1.675),(sign*.141,.415,1.60),(sign*.164,.390,1.47)],[.039,.035,.023],'apron',48,14,flatten=.39)

# Strong, plump sleeves and warm hands that could grab objects in coop gameplay
for sign,L in [(-1,'L'),(1,'R')]:
 sh=np.array([sign*.457,.037,2.064]); elbow=np.array([sign*.621,-.061,1.756]); cuff=np.array([sign*.642,-.145,1.448])
 sweep('Housekeeping_Sleeve_'+L,[sh,sh+np.array([sign*.073,-.025,-.120]),elbow,cuff],[.214,.212,.167,.150],'blue',78,32)
 ball('Soft_Shoulder_'+L,sh,(.184,.191,.179),'blue',48)
 # sleeve cuff soft sky-blue trim
 sweep('Sleeve_Rim_'+L,[(sign*.496,-.197,1.472),(sign*.575,-.247,1.465),(sign*.665,-.285,1.449),(sign*.747,-.175,1.443)],[.007,.010,.010,.006],'bluelight',55,11)
 hand=cuff+np.array([sign*.015,-.033,-.144]);ball('Palm_'+L,hand,(.130,.111,.151),'skin',46)
 ball('Thumb_'+L,hand+np.array([-sign*.127,-.066,.019]),(.067,.069,.103),'skin',30)
 for finger in range(3):
  xx=hand[0]+(finger-1)*.064
  ball('Finger_'+L+'_'+str(finger),(xx,hand[1]-.028,hand[2]-.119),(.038,.051,.074),'skin',26)

# Sculpted head: adult round cheeks + droopy face, NOT chibi marble head
organic('Head_Main','skin',[(-.70,.70),(-.79,.56),(2.20,3.45)],.014, [
 ((0,-.039,2.822),(.545,.441,.495)),
 ((0,-.095,2.584),(.548,.423,.356)),
 ((0,-.061,2.423),(.427,.346,.228)),
 ((-.249,-.360,2.796),(.279,.181,.273)),
 ((.249,-.360,2.796),(.279,.181,.273)),
 ((0,-.013,3.093),(.465,.383,.251)),
 ],.14)
for sign,L in [(-1,'L'),(1,'R')]:
 ball('Ear_'+L,(sign*.538,.025,2.656),(.117,.097,.158),'skin',40)
 ball('Ear_Inner_'+L,(sign*.623,-.046,2.651),(.037,.030,.092),'skinshade',32)

# Eyes: two forward bulging objects WITH overhanging thick upper lids and softly baggy underside
# neutral face should be 'Why am I on shift?' comedic and perplexed without painted eyebrows.
for sign,L in [(-1,'L'),(1,'R')]:
 x=sign*.262;cy=-.540;cz=2.812
 ball('Large_White_Eye_'+L,(x,cy,cz),(.251,.257,.241),'sclera',76)
 nu=60;nv=22;v=[];f=[]
 for i in range(nu+1):
  u=-.988+1.976*i/nu;mx=math.sqrt(max(.002,1-u*u));lower=min(mx*.91,.28+(sign*.065)*u)
  for j in range(nv+1):
   vv=lower+(mx-lower)*j/nv;q=max(.000001,1-u*u-vv*vv)
   v.append([x+.257*u,cy-.257*math.sqrt(q)-.015,cz+.240*vv])
   if i and j:
    a=(i-1)*(nv+1)+j-1;b=i*(nv+1)+j-1;f.extend([(a,b,b+1),(a,b+1,a+1)])
 add('Heavy_Upper_Eyelid_'+L,trimesh.Trimesh(vertices=v,faces=f,process=False),'skin')
 rim=[]
 for i in range(51):
  u=-.97+1.94*i/50;vv=.28+(sign*.065)*u;q=max(.001,1-u*u-vv*vv)
  rim.append((x+.258*u,cy-.258*math.sqrt(q)-.018,cz+.240*vv))
 # eyelid shell geometry itself defines rim; no bright separate outline
 # under-eye puff integrated in facial flesh (not black lined spectacles)
 sweep('Eye_Bag_'+L,[(x-.205,-.692,2.672),(x-.150,-.735,2.622),(x,-.749,2.607),(x+.15,-.730,2.629),(x+.200,-.692,2.685)],[.002,.006,.010,.006,.002],'skin',69,13)
 # tiny dot pupils aim sideways, visibly asymmetric
 px=x+(.054 if sign<0 else .088);pz=cz-.067+(sign*.018)
 ux=(px-x)/.251;uz=(pz-cz)/.241
 py=cy-.257*math.sqrt(max(.009,1-ux*ux-uz*uz))-.014
 ball('Small_Pupil_'+L,(px,py,pz),(.070,.020,.071),'pupil',40)
 ball('Eye_Glint_'+L,(px-.018,py-.018,pz+.020),(.013,.006,.013),'sclera',20)

# Smaller recessed nose & confused slumped mouth, no perpetually happy smiles
ball('Soft_Nose_Bridge',(0,-.478,2.555),(.070,.080,.093),'skin',40)
ball('Nose_Tip',(0,-.533,2.505),(.075,.055,.045),'skin',36)
ball('Confused_Small_Open_Mouth',(.010,-.488,2.393),(.080,.025,.046),'mouth',40)
sweep('Mouth_Lower_Lip',[(-.064,-.465,2.355),(.013,-.489,2.348),(.086,-.458,2.354)],[.003,.010,.003],'lip',40,10)

# HAIR: short hair tucked into a tidy low bun. Sculpt top as connected hair volume,
# larger flowing sculpt strands rather than repeating spheres/tubes.
organic('Hair_Swept_Cap','hair',[(-.67,.67),(-.58,.54),(3.020,3.615)],.014,[
 ((0,.092,3.260),(.516,.386,.239)),
 ((-.250,-.100,3.231),(.321,.245,.172)),
 ((.245,.011,3.256),(.305,.278,.161)),
 ((0,.215,3.281),(.425,.323,.163)),
 ],.12)
# hair side covers upper back ears, partially exposed side and fringe
for sign,L in [(-1,'L'),(1,'R')]:
 organic('Side_Hair_'+L,'hair',[(-.68,.68),(-.42,.46),(2.660,3.300)],.025,[
 ((sign*.451,.128,3.040),(.169,.287,.245)),
 ((sign*.461,.108,2.884),(.112,.242,.146))
 ],.08)
 # Sweeping directional clumps hugging scalp, few for organized hairstyle
 for j in range(2):
  tx=sign*(.192+.112*j)
  sweep('Hair_Highlight_'+L+str(j),[(sign*.045,-.326,3.303),(tx,-.337,3.334),(sign*(.402+.022*j),-.272,3.190)],[.003,.025,.003], 'hair2',49,14,flatten=.48)
# twist fringe sweeping elegantly away from forehead
sweep('Front_Fringed_Wave',[(-.456,-.284,3.190),(-.260,-.378,3.222),(-.080,-.411,3.253),(.110,-.379,3.290),(.340,-.264,3.252)],[.004,.065,.081,.074,.004],'hair2',72,20,flatten=.64)
# Heavy bun on rear crown/back: overlapping sculpt organically, not long ponytail
organic('Braided_Low_Bun','hair',[(-.53,.53),(.31,.93),(2.69,3.42)],.014,[
 ((0,.566,2.987),(.362,.326,.298)),
 ((-.17,.616,2.999),(.217,.245,.219)),
 ((.18,.617,2.998),(.219,.246,.217)),
 ((0,.696,2.941),(.273,.196,.226))
 ],.11)
# Structured winding hair seams on bun, fine and intentional, no ring of identical beads
for j in range(3):
 ang=(j/3)*2*math.pi
 way=[]
 for t in np.linspace(-.9,.9,9):
  a=ang+t*.8
  way.append((.299*math.sin(a),.842-.050*math.cos(ang),2.989+.255*t+.028*math.sin(a*1.7)))
 sweep('Bun_Lock_Direction_'+str(j),way,[.003]+[.015]*7+[.003],'hair2' if j%2==0 else 'hairdark',45,11,flatten=.54)
# hair bun center spiral, swirled into bun
way=[(.095*r*math.cos(t),.832,.095*r*math.sin(t)+2.993) for r,t in [(r,3*math.pi*r) for r in np.linspace(1,.05,13)]]
sweep('Bun_Swirling_Center',way,[.002]+[.012]*11+[.001],'hairlight',72,9,flatten=.52)

# Main export & QA report
path=os.path.join(O,'HotelTogether_Employee03_Housekeeping_V1.glb')
# Blender scale: 0.48 real-world meters per authoring unit; keep render scene unchanged.
export_scene=trimesh.Scene()
for object_name,object_mesh,material_key in objects:
 mesh_copy=object_mesh.copy();mesh_copy.apply_scale(.48)
 export_scene.add_geometry(mesh_copy,geom_name=object_name,node_name=object_name)
export_scene.export(path)
metadata={'name':'Hotel Together Employee 03 - Housekeeping','format':'glTF 2.0 GLB','objects':len(objects),'triangles':sum(len(m.faces) for _,m,_ in objects),'rigged':False,'retopologized':False,'game_ready':False,'approx_height_m':round(float(scene.bounds[1,2]-scene.bounds[0,2])*.48,3),'design':'female; brown low bun; blue short sleeves and white apron; chubby adult; no eyebrows; heavy eyelids over protruding eyes; goofy confused face', 'coordinate_system':'Z-up, -Y-front', 'workflow':'3D blockout / sculpt style prototype, Blender mesh cleanup and retopology needed'}
with open(os.path.join(O,'HotelTogether_Employee03_Housekeeping_V1_Metadata.json'),'w',encoding='utf-8') as fp:json.dump(metadata,fp,indent=2,ensure_ascii=False)
print('BUILT',metadata,'file',os.path.getsize(path),flush=True)
