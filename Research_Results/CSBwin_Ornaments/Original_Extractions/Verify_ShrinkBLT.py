from zipfile import ZipFile
import numpy as np
z=None
p=''
from pathlib import Path
folder=Path(__file__).resolve().parent
class LocalExtract:
 def read(self, name): return (folder/name).read_bytes()
z=LocalExtract()
def decode(filename,w,h):
 b=z.read(p+filename); stride=((w+15)//16)*8
 assert len(b)==stride*h,(filename,len(b),stride*h)
 a=np.zeros((h,stride//8*16),dtype=np.uint8)
 for y in range(h):
  for blk in range(stride//8):
   off=y*stride+blk*8
   planes=[int.from_bytes(b[off+i*2:off+i*2+2],'big') for i in range(4)]
   for x in range(16):
    a[y,blk*16+x]=sum(((v>>(15-x))&1)<<i for i,v in enumerate(planes))
 return a
src=decode('CSB_Ornament_G9_P12.planar',96,56)
maps={'F2':[0,120,10,30,40,30,60,70,50,90,100,110,0,20,140,130], 'F3':[0,0,120,30,40,30,0,60,30,90,100,110,0,10,0,20]}
for name,w,h,storage in [('F2',64,37,64),('F3',47,27,48)]:
 target=decode('CSB_Ornament_G9_P08.planar' if name=='F2' else 'CSB_Ornament_G9_P03.planar',storage,h)
 sx=(96*1024//w)<<6; sy=(56*1024//h)<<6
 xs=[(sx//2+32767+i*sx)>>16 for i in range(w)]
 ys=[(sy//2+32767+i*sy)>>16 for i in range(h)]
 mapped=np.array(maps[name],dtype=np.uint8)//10
 pred=mapped[src[np.ix_(ys,xs)]]
 m=(pred!=target[:,:w]); print(name,'step',sx,sy,'x init',xs[:12],'y init',ys[:12], 'mismatch',m.sum(),'total',w*h,'rate',100*(1-m.mean()))
 print('mismatch coords:', list(zip(*np.where(m)))[:24])
 print('target extra pixel',np.unique(target[:,w:],return_counts=True))

def encode(arr):
 height,width=arr.shape
 assert width%16==0
 data=bytearray()
 for y in range(height):
  for k in range(width//16):
   line=arr[y,k*16:(k+1)*16]
   for plane in range(4):
    word=sum(((int(v)>>plane)&1)<<(15-i) for i,v in enumerate(line))
    data+=word.to_bytes(2,'big')
 return bytes(data)
for name,w,h,storage in [('F2',64,37,64),('F3',47,27,48)]:
 sx=(96*1024//w)<<6;sy=(56*1024//h)<<6
 xs=[(sx//2+32767+i*sx)>>16 for i in range(w)]
 ys=[(sy//2+32767+i*sy)>>16 for i in range(h)]
 mapped=np.array(maps[name],dtype=np.uint8)//10
 pred=mapped[src[np.ix_(ys,xs)]]
 padded=np.zeros((h,storage),dtype=np.uint8);padded[:,:w]=pred
 raw=encode(padded)
 namefile='CSB_Ornament_G9_P08.planar' if name=='F2' else 'CSB_Ornament_G9_P03.planar'
 orig=z.read(p+namefile)
 print('BYTE MATCH',name,len(raw), len(orig),raw==orig, 'mismatches',sum(a!=b for a,b in zip(raw,orig)))
