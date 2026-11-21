import zipfile,json, numpy as np, csv, io
from collections import Counter
from pathlib import Path
p=Path('/mnt/data/DOS_6bit_Indexed_Output_20261121.zip')
z=zipfile.ZipFile(p)
base='DOS_6bit_Indexed_Output_20261121/'
m=json.loads(z.read(base+'manifest.json'))
geo={ (int(r['ornament_type']),int(r['view_index'])):r for r in csv.DictReader(open('/mnt/data/DOS_All_Ornament_Geometry_F0635_Corrected_20261121.csv',newline='',encoding='utf8')) }
D2=np.array([0,12,1,3,4,3,6,7,5,9,10,11,0,2,14,13],dtype=np.uint8)
D3=np.array([0,0,12,3,4,3,0,6,3,9,10,11,0,1,0,2],dtype=np.uint8)
N=np.arange(16,dtype=np.uint8)
sources={}
for t,g,s in [(4,267,(14,19)),(4,268,(28,28)),(36,331,(32,42)),(36,332,(92,52))]:
 v=12 if g in (267,331) else 14
 fname=f'T{t:02}_V{v:02}_G{g:03}_full.bin'
 ar=np.frombuffer(z.read(base+fname),dtype=np.uint8).reshape((s[1],s[0]))
 sources[g]=ar
 print('source',g,'size',ar.shape,'palette',sorted(Counter(ar.flat).items()))

rows=[]
for r in m['rows']:
 t,v,g=r['type'],r['view'],r['graphic']
 row=geo[(t,v)];src=sources[g];sh,sw=src.shape; w,h=r['size']
 sx=(sw<<6)//w;sy=(sh<<6)//h
 x6=((sx>>1)+np.arange(w)*sx)>>6
 y6=((sy>>1)+np.arange(h)*sy)>>6
 sx16=((sw<<10)//w)<<6;sy16=((sh<<10)//h)<<6
 x16=((sx16>>1)+np.arange(w)*sx16)>>16
 y16=((sy16>>1)+np.arange(h)*sy16)>>16
 if max(x16)>=sw or max(y16)>=sh: raise Exception('bad 16')
 map_=D2 if row['palette_group']=='D2' else D3 if row['palette_group'].startswith('D3') else N
 a=map_[src[np.ix_(y6,x6)]]
 b=map_[src[np.ix_(y16,x16)]]
 if int(row['mirrored_horizontally']): a=a[:,::-1];b=b[:,::-1]
 actual=np.frombuffer(z.read(base+f'T{t:02}_V{v:02}_G{g:03}_full.bin'),dtype=np.uint8).reshape((h,w))
 assert np.array_equal(a,actual),(t,v,'batch mismatch')
 diff=a!=b
 # visible only crop
 ox=r['visible_origin'][0]-r['origin'][0];oy=r['visible_origin'][1]-r['origin'][1]
 vw,vh=r['visible_size']
 vd=diff[oy:oy+vh,ox:ox+vw] if vw and vh else np.zeros((0,0),dtype=bool)
 dxy=np.argwhere(diff)
 rows.append(dict(type=t,view=v,graphic=g,position=r['origin'],dimensions=r['size'],source=r['source'],mirror=bool(int(row['mirrored_horizontally'])),clipped=[vw,vh],x_cols_different=np.where(x6!=x16)[0].tolist(),y_rows_different=np.where(y6!=y16)[0].tolist(),pixel_differences=int(diff.sum()),visible_pixel_differences=int(vd.sum()),differences_by_dos_index={str(int(n)):int(np.count_nonzero(a[diff]==n)) for n in np.unique(a[diff])}, first_differences=[dict(x=int(i+ r['origin'][0]),y=int(j+ r['origin'][1]),dos_index=int(a[j,i]),unity16_index=int(b[j,i])) for j,i in dxy[:15]]))
for r in rows:print(f"type {r['type']:02} v{r['view']:02} 6bit-vs-16bit diff={r['pixel_differences']:3} visible={r['visible_pixel_differences']:3} x={r['x_cols_different']} y={r['y_rows_different']}")
output=Path('/mnt/data/DOS_6bit_vs_Unity16bit_All30_20261121.json')
output.write_text(json.dumps({'description':'Source-derived sample-coordinate comparison only; not a comparison with actual Unity code output, and not independent original DOS F3/S2/S3 validation','original_F1_graphic_332_matches_user_bin':bool(np.array_equal(sources[332].reshape(-1),np.fromfile('/mnt/data/DOS_Manacles332_Indexed_20261121.bin',dtype=np.uint8))),'entries':rows},indent=2)+'\n')
print('saved',output)
