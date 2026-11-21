"""Tests for the experimental candidate renderer; no claims about DOS fidelity."""
import importlib.util
from PIL import Image
from pathlib import Path
spec=importlib.util.spec_from_file_location('render',Path(__file__).with_name('DOS_Ornament_Pixel_Sampling_Candidates_20261121.py'))
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
src=Image.new('RGBA',(5,3))
for y in range(3):
 for x in range(5):src.putpixel((x,y),(x*20,y*30,0,255))
for method in ('edge','center'):
 dst=m.scaled_rgba(src,3,2,method)
 assert dst.size==(3,2)
 for y in range(2):
  for x in range(3):
   assert dst.getpixel((x,y))==src.getpixel((m.sample_index(x,5,3,method),m.sample_index(y,3,2,method)))
 record={'width':'3','height':'2','clipped_x':'223','clipped_y':'134',
         'clipped_width':'1','clipped_height':'2','source_x':'1','source_y':'0','mirrored_horizontally':'1'}
 panel,_=m.render_one(src,record,method)
 assert panel.size==(224,136)
 assert panel.getpixel((222,134))==(30,30,30,255)
 assert panel.getpixel((223,134))==dst.transpose(Image.Transpose.FLIP_LEFT_RIGHT).getpixel((1,0))
print('PASS: 2 sampling candidates; scaled pixel indices; mirrored clipping; 224x136 output')
