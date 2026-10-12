"""Hotel Together Employee 02 (Bellhop) — Blender-importable GLB art prototype.
Generated real 3D triangle meshes with separate PBR materials and named objects.
Requires numpy scipy scikit-image trimesh. Not retopologized or rigged.
Coordinate axes: +Z up, -Y character front. Unit scale: meters.
"""
import os, math, json, random
import numpy as np
import trimesh
from scipy.interpolate import CubicSpline
from skimage.measure import marching_cubes
O='/mnt/data';R=random.Random(126);scene=trimesh.Scene();objects=[]
C={'skin':'#DBA27F','skinlight':'#E5B090','skinshade':'#BF846C','sclera':'#F6F4EC','pupil':'#211B1B','eyeshadow':'#956955','mouth':'#3A242A','lip':'#AD6E65',
   'hair':'#282425','hair2':'#363036','hairlight':'#463C42','hairdark':'#201E22','red':'#A82737','redlight':'#BA3442','redshade':'#881D30',
   'gold':'#E6AE49','goldshade':'#B78132','goldbright':'#F9CB6B','shirt':'#E5DED2','pants':'#2D303B','seam':'#202532','shoe':'#403334','sole':'#241E20','shoehighlight':'#544144','metal':'#D9A543','leather':'#805036','luggageblue':'#2A6E91','luggagesand':'#D2A16A','luggagered':'#A94845','wheel':'#272728'}
rough={k:.81 for k in C};rough.update({'sclera':.30,'pupil':.32,'metal':.36,'gold':.42,'goldbright':.42,'shoe':.55,'hair':.83,'skin':.70})
def add(n,m,k):
 m.metadata['name']=n;h=C[k].strip('#');rgb=[int(h[i:i+2],16) for i in [0,2,4]]+[255]
 m.visual=trimesh.visual.TextureVisuals(material=trimesh.visual.material.PBRMaterial(name='Bellhop_'+k,baseColorFactor=rgb,roughnessFactor=rough[k],metallicFactor=.6 if k=='metal' else .0,doubleSided=True))
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

# SHOES / SHORT STOUT TROUSERS
for sign,L in [(-1,'L'),(1,'R')]:
 x=sign*.248
 ball('Shoe_'+L,(x,-.112,.175),(.206,.285,.128),'shoe',52)
 ball('Leather_Toe_'+L,(x,-.242,.175),(.204,.166,.103),'shoehighlight',44)
 ball('Rubber_Sole_'+L,(x,-.128,.090),(.213,.306,.059),'sole',48)
 ball('Shoe_Ankle_'+L,(x,.002,.226),(.152,.150,.124),'shoe',48)
 loft('Trouser_'+L,[(x,.023,.34,.163,.155),(x,.035,.48,.173,.164),(x,.049,.65,.187,.181),(x,.045,.86,.201,.205),(x,.040,1.03,.230,.211)],'pants')
 sweep('Pant_Hem_'+L,[(x-.150,-.053,.354),(x,-.142,.354),(x+.150,-.053,.354)],[.004,.006,.004],'seam',40,10)
# UNDER SHIRT inside jacket neck
ball('Neck',(0,.046,2.27),(.167,.167,.138),'skin',40)
# torso with belly / broad hips / sagging relaxed round shoulders
loft('Bellhop_Doublebreasted_Jacket',[(0,.035,1.065,.400,.340),(0,-.025,1.18,.502,.409),(0,-.065,1.41,.540,.446),(0,-.023,1.68,.531,.416),(0,.010,1.90,.510,.350),(0,.050,2.14,.433,.298),(0,.060,2.265,.320,.233)],'red',72,72)
# Jacket front panels slightly curved for 3d stitching
for sign,L in [(-1,'Left'),(1,'Right')]:
 sweep('Jacket_Lower_Hem_'+L,[(0,-.412,1.085),(sign*.205,-.405,1.081),(sign*.390,-.340,1.092),(sign*.502,-.076,1.107)],[.005,.008,.008,.005],'gold',64,10)
 sweep('Jacket_Pocket_Trim_'+L,[(sign*.22,-.420,1.49),(sign*.35,-.363,1.50),(sign*.437,-.253,1.507)],[.004,.006,.004],'goldshade',36,10)
 # buttons double breasted curved around torso, small golden hemispheres
 for j,z in enumerate([1.48,1.70,1.91]):
  xx=sign*.155;yy=[-.49,-.449,-.380][j]
  ball('Brass_Button_'+L+str(j),(xx,yy,z),(.028,.023,.028),'goldbright',28)
 # collar band lower
 face('Bellhop_Collar_'+L,[(sign*.04,-.293,2.247),(sign*.264,-.295,2.247),(sign*.315,-.314,2.084),(sign*.07,-.359,2.085)],'redshade')
 sweep('Collar_Gold_'+L,[(sign*.052,-.307,2.244),(sign*.265,-.305,2.241),(sign*.320,-.319,2.079)],[.005,.008,.005],'gold',38,10)
# white collar tip only, high neck like hotel uniform
face('Shirt_Inset',[( -.120,-.255,2.257),(.120,-.255,2.257),(.083,-.347,2.11),(0,-.356,2.068),(-.083,-.347,2.11)],'shirt')
# gold hotel H badge using strokes, front chest
for p in [[(.318,-.338,1.975),(.318,-.350,2.037)],[(.382,-.298,1.975),(.382,-.311,2.037)],[(.318,-.354,2.005),(.382,-.309,2.005)]]:
 sweep('Hotel_H',p,[.006,.006],'goldbright',20,8)
sweep('H_Crown',[(.315,-.343,2.055),(.333,-.344,2.075),(.350,-.328,2.060),(.370,-.320,2.076),(.386,-.298,2.054)],[.003,.004,.003,.004,.003],'goldbright',42,9)
# ARMS relaxed slightly away from hips
for sign,L in [(-1,'L'),(1,'R')]:
 sh=np.array([sign*.475,.05,2.038]);el=np.array([sign*.623,-.051,1.73]);wr=np.array([sign*.635,-.141,1.412]);
 sweep('Sleeve_'+L,[sh,sh+[sign*.042,-.030,-.121],el,wr],[.213,.216,.174,.144],'red',72,28)
 ball('Cloth_Shoulder_'+L,sh,(.187,.204,.175),'red',44)
 sweep('Golden_Wrist_Band_'+L,[(sign*.487,-.179,1.45),(sign*.569,-.260,1.430),(sign*.653,-.287,1.410),(sign*.744,-.182,1.403)],[.006,.008,.008,.006],'gold',52,10)
 hand=wr+np.array([sign*.017,-.036,-.148]);ball('Palm_'+L,hand,(.130,.110,.154),'skin',48)
 ball('Thumb_'+L,hand+np.array([-sign*.116,-.048,.008]),(.069,.075,.098),'skin',34)
 for f in range(3):
  xx=hand[0]+(f-1)*.066
  ball(f'Finger_{L}_{f}',(xx,hand[1]-.025,hand[2]-.108),(.041,.050,.077),'skin',26)
# HAIR BACK SCULPT, bob neat but volume (under head; render should cover back, not face)
organic('Hair_Bob_Back','hair',[(-.70,.70),(-.20,.68),(2.39,3.39)],.016,[
 ((0,.212,2.96),(.574,.443,.468)),((0,.218,2.690),(.580,.390,.292)),
 ((-.30,.166,2.708),(.311,.401,.287)),((.30,.166,2.708),(.311,.401,.287)),
],.13)
# face head in front of hair, fleshy eye sockets, substantial adult cheeks and heavier lower jaw
organic('Female_Face_Sculpt','skin',[(-.68,.68),(-.82,.55),(2.17,3.43)],.013,[
 ((0,-.045,2.823),(.530,.430,.470)),
 ((0,-.100,2.551),(.526,.424,.365)),
 ((0,-.068,2.390),(.415,.347,.240)),
 ((-.260,-.368,2.761),(.272,.205,.258)),
 ((.260,-.368,2.761),(.272,.205,.258)),
 ((0,-.045,3.088),(.476,.394,.237)),
],.17)
# compact styled hair fringe, flowing organic sculpt plus a few broad directional waves
organic('Swooping_Fringe','hair',[(-.68,.68),(-.68,.34),(3.08,3.43)],.015,[
 ((-.15,-.136,3.245),(.375,.285,.150)),((.16,-.070,3.235),(.386,.310,.147)),
 ((-.405,-.095,3.146),(.198,.220,.183))
],.11)
# locks sculpt hugging head cheeks from the sides; highlights are tiny not tubes
for sign,L in [(-1,'L'),(1,'R')]:
 organic('Bob_Side_'+L,'hair',[(-.77,.77),(-.57,.59),(2.41,3.18)],.022,[
 ((sign*.485,.025,2.83),(.170,.351,.314)),((sign*.471,.065,2.65),(.175,.326,.241)),
 ((sign*.490,.105,2.538),(.135,.265,.130))
 ],.12)
 for j in range(3):
  px=sign*(.492+j*.027);p_y=-.230+j*.12;z=2.72-j*.078
  sweep('Bob_Lock_'+L+str(j),[(px-sign*.036,p_y+.09,z+.22),(px+sign*.020,p_y+.06,z+.04),(px+sign*.012,p_y-.03,z-.17)],[.008,.048,.004],['hair','hair2','hair'][j],46,16,flatten=.70)
 # ears partially tucked beneath bob
 ball('Ear_'+L,(sign*.551,-.050,2.635),(.101,.106,.144),'skin',40)
# forehead one broad S-curve sweep instead of bead curls
sweep('Fringe_Wave_Main',[(-.445,-.362,3.206),(-.256,-.440,3.247),(-.070,-.473,3.244),(.105,-.402,3.262),(.305,-.321,3.255)],[.013,.094,.105,.087,.014],'hair2',72,22,flatten=.72)
for j in range(3):
 px=-.37+j*.27
 sweep('Fringe_Strand_'+str(j),[(px-.075,-.432,3.266),(px+.030,-.484,3.242),(px+.093,-.449,3.181)],[.003,.020,.003],'hairlight',40,9,flatten=.75)
# EYES protruding yet shaped into heavy eyelids; glance off-center funny, no eyebrows
for sign,L in [(-1,'L'),(1,'R')]:
 x=sign*.267;cy=-.555;cz=2.812
 ball('White_Eye_'+L,(x,cy,cz),(.253,.266,.235),'sclera',72)
 # heavy organic droop folds; upper actual shell drapes over white upper orbit
 nu=48;nv=18;verts=[];faces=[]
 for i in range(nu+1):
  u=-.997+1.994*i/nu;maxz=math.sqrt(max(.002,1-u*u));low=.23+(.09 if sign==1 else -.05)*u
  low=min(low,maxz*.91)
  for j in range(nv+1):
   v=low+(maxz-low)*j/nv;q=max(.00004,1-u*u-v*v)
   verts.append([x+.262*u,cy-.267*math.sqrt(q)-.012,cz+.236*v])
   if i and j:
    a=(i-1)*(nv+1)+j-1;b=i*(nv+1)+j-1;faces.extend(((a,b,b+1),(a,b+1,a+1)))
 add('Droopy_Upper_Lid_'+L,trimesh.Trimesh(vertices=verts,faces=faces,process=False),'skin')
 rim=[]
 for i in range(51):
  u=-.97+1.94*i/50;v=.23+(.09 if sign==1 else -.05)*u;q=max(.002,1-u*u-v*v)
  rim.append((x+.268*u,cy-.266*math.sqrt(q)-.018,cz+.236*v))
 sweep('UpperLid_Seam_'+L,rim,[.012]*len(rim),'skinlight',82,11)
 # Puffy eye bags without thick spectacles outlines
 sweep('Under_Eye_Bag_'+L,[(x-.201,-.701,2.689),(x-.132,-.758,2.645),(x,-.773,2.629),(x+.135,-.755,2.648),(x+.196,-.700,2.704)],[.004,.010,.012,.010,.004],'skin',64,12)
 # eye pupil protruding forward; look slightly asymmetrically to the side
 px=x+(.070 if sign<0 else .037);pz=cz-.057
 normx=(px-x)/.253;normz=(pz-cz)/.235
 py=cy-.266*math.sqrt(max(.02,1-normx**2-normz**2))-.01
 ball('Iris_'+L,(px,py,pz),(.070,.019,.074),'pupil',42)
 ball('Pupil_Gloss_'+L,(px-.014,py-.018,pz+.019),(.012,.007,.012),'sclera',19)
# nose extremely restrained and attached, narrow elongated nostril-less shape
ball('Nose_Bridge',(0,-.480,2.574),(.068,.088,.110),'skin',38)
ball('Nose_Tip',(0,-.544,2.513),(.083,.069,.053),'skin',36)
# small worried o-mouth, asymmetric oval tucked into lower jaw
ball('Small_Confused_Mouth',(.028,-.475,2.372),(.069,.033,.041),'mouth',40)
sweep('Lower_Lip',[(-.049,-.447,2.331),(.026,-.473,2.321),(.092,-.438,2.330)],[.002,.009,.002],'lip',40,10)
# FLAT RED BELLHOP CAP / crown panel, gold braid, rounded crown with short tilt
loft('Pillbox_Hat',[(0,.045,3.312,.358,.314),(0,.045,3.36,.366,.317),(0,.055,3.413,.362,.310),(0,.068,3.49,.340,.302),(0,.065,3.519,.300,.273)],'redshade',8,68)
loft('Pillbox_Hat_Top',[(0,.066,3.511,.327,.285),(0,.066,3.526,.304,.269),(0,.067,3.536,.235,.212)],'red',8,64)
# visible seam band at bottom oval (use ring z3.39), elevated gold band and red hem
for z,col,r in [(3.373,'gold',.016),(3.401,'goldbright',.010)]:tube_ring('Hat_Band_'+str(z),(0,.043,z),(.367,.323),r,col)
# center hat medallion oval
ball('Hat_Emblem',(0,-.285,3.450),(.053,.019,.044),'goldbright',30)
# zip scene: main character only; props generated in second scene below separately
out=os.path.join(O,'HotelTogether_Employee02_Bellhop_V1.glb');scene.export(out)
meta={'name':'Hotel Together Employee 02 - Bellhop','format':'glTF 2.0 GLB','meshes':len(objects),'triangles':sum(len(m.faces) for _,m,_ in objects),'rigged':False,'game_ready':False,'source_format':'procedural parametric sculpt prototype','approx_height_m':round(float(scene.bounds[1,2]-scene.bounds[0,2]),3),'notes':'Design reference: short black bob, red bellhop hat + double-breasted jacket, tired bulging eyes, heavier lower eyelids, no eyebrows, chubby torso. Intended for Blender sculpt cleanup / retopology.'}
with open(os.path.join(O,'HotelTogether_Employee02_Bellhop_V1_Metadata.json'),'w') as f:json.dump(meta,f,indent=2,ensure_ascii=False)
print('Created',out,meta,flush=True)
