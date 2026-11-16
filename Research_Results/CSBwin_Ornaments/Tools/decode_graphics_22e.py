import zipfile,struct
import sys
with zipfile.ZipFile(sys.argv[1]) as z:
 b=z.read('graphics.dat')
n=int.from_bytes(b[:2],'big')
cs=[int.from_bytes(b[2+2*i:4+2*i],'big') for i in range(n)]
ds=[int.from_bytes(b[2+2*n+2*i:4+2*n+2*i],'big') for i in range(n)]
gid=0x22e
start=2+4*n+sum(cs[:gid]); dat=b[start:start+cs[gid]]
assert ds[gid] == 0x1270
class ReadCodes:
 def __init__(self,data):self.data=data;self.pos=0;self.chunk=b'';self.bit=0;self.bits=0;self.width=0
 def get(self,width,force=False):
  if force or width!=self.width or self.bit>=self.bits:
   self.chunk=self.data[self.pos:self.pos+width];self.pos+=len(self.chunk);self.bit=0;self.bits=len(self.chunk)*8-width+1;self.width=width
  if self.bit>=self.bits: return None
  v=int.from_bytes(self.chunk,'little')>>self.bit & ((1<<width)-1);self.bit+=width;return v

def unpack(code_bytes):
 out=bytearray();previous=0;rep=False
 for c in code_bytes:
  if rep:
   if c==0: out.append(0x90)
   else: out.extend([previous]*(c-1))
   rep=False
  elif c==0x90: rep=True
  else:out.append(c);previous=c
 return out

def lzw():
 r=ReadCodes(dat);width=9;mx=(1<<width)-1;nextc=257;force=False
 table={i:bytes([i]) for i in range(256)}
 prev=r.get(width)

 if prev is None:return
 out=bytearray();repeat=False;repchar=0
 def emit(s):
  nonlocal repeat,repchar
  for c in s:
   if repeat:
    if c==0:out.append(0x90)
    else:out.extend(bytes([repchar])*(c-1))
    repeat=False
   elif c==0x90:repeat=True
   else:out.append(c);repchar=c
 emit(bytes([prev & 255]))
 log=[]
 for j in range(100000):
  if nextc>mx:
   width+=1
   mx=4096 if width==12 else (1<<width)-1
  c=r.get(width,force=force);force=False
  if c is None:break
  if c==256:
   table={i:bytes([i]) for i in range(256)}
   nextc=256; width=9;mx=511;force=True
   c=r.get(width,force=True)
   if c is None:break
  old=c
  if c>=nextc:
   s=table[prev]+table[prev][:1]
  else:s=table[c]
  emit(s)
  if nextc<4096:
   table[nextc]=table[prev]+s[:1]
   nextc+=1
  prev=old
  if len(out)>100000:break
 assert len(out) == ds[gid], (len(out),ds[gid])
 assert r.pos == len(dat), (r.pos,len(dat))
 return out
out=lzw()
if out:
 from pathlib import Path
 Path(sys.argv[2] if len(sys.argv)>2 else 'Graphic_0x22e_Raw.bin').write_bytes(out)
 print(f'Decoded graphic 0x22e: {len(out)} bytes')
