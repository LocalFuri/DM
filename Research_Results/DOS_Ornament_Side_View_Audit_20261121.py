#!/usr/bin/env python3
"""Read-only audit of original ReDMCSB view definitions, layout data and generated ornament CSV.
Run in C:\\Unity\\DM\\Research_Results with the previous geometry calculator present.
python DOS_Ornament_Side_View_Audit_20261121.py GRAPHICS_DOS.ZIP DOS_All_Ornament_Geometry_20261121.csv
"""
import csv, sys, re
from pathlib import Path
from collections import Counter
from DOS_Generic_Ornament_Geometry_20261121 import read_graphics,read_layout696

expected={0:'D3 far-left right',1:'D3 far-right left',2:'D3 left right',3:'D3 right left',7:'D2 left right',8:'D2 right left',12:'D1 left right',13:'D1 right left'}
if len(sys.argv)!=3: raise SystemExit('Usage: python DOS_Ornament_Side_View_Audit_20261121.py GRAPHICS_DOS.ZIP DOS_All_Ornament_Geometry_20261121.csv')
_, rec=read_layout696(read_graphics(Path(sys.argv[1])))
with open(sys.argv[2],newline='',encoding='utf-8') as f: rows=list(csv.DictReader(f))
assert len(rows)==900 and len({(r['ornament_type'],r['view_index']) for r in rows})==900
for r in rows:
  t, v, s=map(int,[r['ornament_type'],r['view_index'],r['coordinate_set']])
  assert int(r['zone_index'])==1004+15*s+v
  z=rec[int(r['zone_index'])]
  assert z.parent==4 and rec[4].kind==1 and rec[4].parent==3 and rec[3].kind==9 and (rec[3].data1,rec[3].data2)==(224,136)
  assert z.kind in range(9)
for t in range(60):
  for v in [0,1,2,3,7,8,12,13]:
    r=next(r for r in rows if int(r['ornament_type'])==t and int(r['view_index'])==v)
    assert r['view_name']==expected[v]
    assert int(r['native_graphic'])==259+2*t
    assert int(r['mirrored_horizontally'])==int(v in [1,3,8,13])
    sx,sy= ((30,14) if v in [0,1] else (14,14) if v in [2,3] else (21,21) if v in [7,8] else (32,32))
    assert (int(r['scale_x']),int(r['scale_y']))==(sx,sy)
output=[]
for r in rows:
  if int(r['view_index']) in expected:
    output.append({k:r[k] for k in ['ornament_type','coordinate_set','view_index','view_name','zone_index','native_graphic','native_width','native_height','scale_x','scale_y','mirrored_horizontally','x','y','width','height','clipped_x','clipped_y','clipped_width','clipped_height','source_x','source_y']})
name='DOS_Ornament_Side_View_480_20261121.csv'
with open(Path(__file__).with_name(name),'w',newline='',encoding='utf-8') as f:
 w=csv.DictWriter(f,fieldnames=list(output[0]));w.writeheader();w.writerows(output)
print('Validated 900 layout-parent chains, 480 side-view records, 8 side view indices, native graphic selection, mirror and scale definitions.')
print('Side views per type: S3 variants 0-3 (4); S2 7-8 (2); S1 12-13 (2).')
print('Viewport clipping window: 224x136. Side records with nontrivial clipping:',sum(int(r['clipped_width'])!=int(r['width']) or int(r['clipped_height'])!=int(r['height']) for r in output))
print('Saved:',name)
