import os
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import vtk
from vtk.util.numpy_support import numpy_to_vtk,numpy_to_vtkIdTypeArray,vtk_to_numpy
import HotelTogether_Employee03_Housekeeping_Build as G
O='/mnt/data'
def actor(mesh,k):
 v=np.ascontiguousarray(mesh.vertices.astype(np.float32));faces=np.ascontiguousarray(mesh.faces.astype(np.int64))
 points=vtk.vtkPoints();points.SetData(numpy_to_vtk(v,deep=True));cells=vtk.vtkCellArray();raw=np.column_stack((np.full(len(faces),3,dtype=np.int64),faces)).flatten()
 cells.SetCells(len(faces),numpy_to_vtkIdTypeArray(raw,deep=True));poly=vtk.vtkPolyData();poly.SetPoints(points);poly.SetPolys(cells)
 normals=vtk.vtkPolyDataNormals();normals.SetInputData(poly);normals.SetConsistency(True);normals.SetSplitting(False);normals.SetFeatureAngle(180);normals.Update()
 mapper=vtk.vtkPolyDataMapper();mapper.SetInputConnection(normals.GetOutputPort());a=vtk.vtkActor();a.SetMapper(mapper)
 h=G.C[k][1:];rgb=tuple(int(h[i:i+2],16)/255 for i in (0,2,4));pr=a.GetProperty();pr.SetColor(rgb);pr.SetAmbient(.40);pr.SetDiffuse(.66);pr.SetSpecular(.13);pr.SetSpecularPower(28);pr.BackfaceCullingOff();return a
r=vtk.vtkRenderer();r.SetBackground(.941,.927,.914);r.SetAmbient(.56,.56,.56)
for name,m,k in G.objects:r.AddActor(actor(m,k))
for pos,inten in [((-6,-9,8),.94),((6,3,7),.51),((0,-2,0),.23)]:
 l=vtk.vtkLight();l.SetPosition(*pos);l.SetFocalPoint(0,0,1.8);l.SetIntensity(inten);r.AddLight(l)
w=vtk.vtkRenderWindow();w.SetOffScreenRendering(1);w.SetMultiSamples(8);w.AddRenderer(r)
def shot(label,pos,look=(0,0,1.8),scale=2.10,wh=(760,910)):
 w.SetSize(*wh);cam=vtk.vtkCamera();cam.SetPosition(*pos);cam.SetFocalPoint(*look);cam.SetViewUp(0,0,1);cam.ParallelProjectionOn();cam.SetParallelScale(scale);r.SetActiveCamera(cam);r.ResetCameraClippingRange();w.Render()
 f=vtk.vtkWindowToImageFilter();f.SetInput(w);f.SetInputBufferTypeToRGB();f.ReadFrontBufferOff();f.Update();data=vtk_to_numpy(f.GetOutput().GetPointData().GetScalars())
 im=Image.fromarray(data.reshape(wh[1],wh[0],3)[::-1].copy(),'RGB');im.save(f'{O}/HotelTogether_Housekeeping_V1_{label}.png');return im
views=[]
for label,pos in [('Front',(0,-8,2.15)),('ThreeQuarter',(5,-8,2.22)),('Side',(9,0,2.1)),('Back',(0,9,2.1))]:
 print('render',label,flush=True);views.append((label,shot(label,pos)))
face=shot('Face',(0,-6.5,2.85),look=(0,-.2,2.84),scale=.84,wh=(760,640))
F='/usr/share/fonts/truetype/dejavu/DejaVuSans';regular=F+'.ttf';bold=F+'-Bold.ttf'
fonts=[ImageFont.truetype(bold,70),ImageFont.truetype(bold,34),ImageFont.truetype(regular,24),ImageFont.truetype(bold,29)]
board=Image.new('RGB',(1780,2050),'#F7F4F0');d=ImageDraw.Draw(board)
d.text((70,44),'HOTEL TOGETHER',font=fonts[0],fill='#26312E')
d.text((72,139),'EMPLOYEE 03  /  HOUSEKEEPING',font=fonts[1],fill='#397D9F')
d.text((73,186),'3D CHARACTER PROTOTYPE  |  BLUE TUNIC + WHITE APRON  |  GOOFY-CONFUSED FACE',font=fonts[2],fill='#66716E')
for (label,im),(x,y) in zip(views,[(65,280),(925,280),(65,1195),(925,1195)]):
 im=im.resize((780,934),Image.Resampling.LANCZOS)
 board.paste(im,(x,y));d.text((x+16,y+855),label.upper(),font=fonts[3],fill='#303B38')
board.save(f'{O}/HotelTogether_Housekeeping_V1_Turnaround.png')
print('Render completed')
