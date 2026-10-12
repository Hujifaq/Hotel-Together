"""Separate optional luggage-cart GLB companion prop for Employee 02 Bellhop."""
import os,math
import numpy as np
from skimage.measure import marching_cubes
import trimesh
import HotelTogether_Employee02_Bellhop_Build as G
G.scene=trimesh.Scene();G.objects=[]
# Z floor .05, gold cart approx 2.5m high relative to 3.5m Bellhop

def rod(n,points,rad=.027,col='metal'):
 return G.sweep(n,points,[rad]*len(points),col,div=max(42,len(points)*10),sect=12)
def rounded_box(n,cen,ext,r,k,voxel=.018):
 lo=np.array(cen)-np.array(ext)/2-r-.06;hi=np.array(cen)+np.array(ext)/2+r+.06
 axes=[np.arange(lo[j],hi[j],voxel) for j in range(3)];X,Y,Z=np.meshgrid(*axes,indexing='ij');q=np.stack([abs(X-cen[0])-(ext[0]/2-r),abs(Y-cen[1])-(ext[1]/2-r),abs(Z-cen[2])-(ext[2]/2-r)],axis=-1)
 outside=np.linalg.norm(np.maximum(q,0),axis=-1);inside=np.minimum(np.maximum.reduce([q[...,i] for i in range(3)]),0);S=outside+inside-r
 v,f,*_=marching_cubes(S,level=0,spacing=(voxel,)*3);v+=lo;m=trimesh.Trimesh(vertices=v,faces=f,process=False)
 if m.volume<0:m.invert()
 return G.add(n,m,k)
# wheeled base, cart platform & golden guard, cart parked empty or suitcases next to each other
rounded_box('Cart_Base',(0,0,.42),(1.45,.87,.17),.10,'leather',.016)
rounded_box('Cart_Base_Edge',(0,0,.48),(1.50,.90,.055),.03,'metal',.016)
for s in [-1,1]:
 for t in [-1,1]:
  x=s*.57;y=t*.32
  G.ball('Wheel_%s_%s'%(s,t),(x,y,.137),(.112,.108,.112),'wheel',32)
  rod('Wheel_Axle_%s_%s'%(s,t),[(x,y-.13,.137),(x,y+.13,.137)],.022,'metal')
# cart arc with crown cap, two arch support rods
for y in [-.34,.34]:
 pts=[]
 for i in range(41):
  t=np.pi*i/40
  pts.append((.62*math.cos(t),y,1.94+.62*math.sin(t)))
 rod('Golden_Arch_'+str(y),pts,.032,'metal')
 for x in [-.62,.62]:rod('Vertical_Leg_'+str(x)+'_'+str(y),[(x,y,.49),(x,y,1.94)],.026,'metal')
rod('Arch_Top_Connector',[(0,-.34,2.56),(0,.34,2.56)],.030,'metal')
G.ball('Gold_Finial',(0,0,2.65),(.085,.083,.085),'goldbright',36)
# colorful luggage bodies, strap panels, telescopic handles
cases=[('Blue',(-.37,0,1.00),(.57,.48,.99),'luggageblue'),('Brown',(.33,.04,.91),(.59,.54,.82),'leather'),('Mini',(0,-.17,1.59),(.47,.39,.25),'luggagered')]
for nm,center,ext,col in cases:
 rounded_box('Suitcase_'+nm,center,ext,.07,col,.018)
 # contrasting center zipper stripe
 G.sweep('Suitcase_Zipper_'+nm,[(center[0]-ext[0]/2+.035,center[1]-ext[1]/2-.015,center[2]),(center[0],center[1]-ext[1]/2-.021,center[2]),(center[0]+ext[0]/2-.035,center[1]-ext[1]/2-.015,center[2])],[.004,.007,.004],'goldshade',38,8)
 if nm!='Mini':
  x=center[0];z=center[2]+ext[2]/2
  rod('Suitcase_Handle_'+nm,[(x-.115,center[1],z+.025),(x-.115,center[1],z+.135),(x+.115,center[1],z+.135),(x+.115,center[1],z+.025)],.024,'wheel')
# wheel-stop bars
for x in [-.66,.66]:rod('Bumper_'+str(x),[(x,-.39,.45),(x,.39,.45)],.021,'metal')
f='/mnt/data/HotelTogether_Bellhop_LuggageCart_V1.glb';G.scene.export(f)
print('Exported',f,'size',os.stat(f).st_size,'meshes',len(G.objects))
