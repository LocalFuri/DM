#!/usr/bin/env python3
"""Reproduce F0129's DOS 6-bit integer sampling for original indexed Manacles F2.
No Unity access; no approximation or per-ornament correction.
Input files must be in this script's folder.
"""
from pathlib import Path
from collections import Counter
import json
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parent
source=np.fromfile(ROOT/'DOS_Manacles332_Indexed_20261121.bin',dtype=np.uint8)
assert source.size==92*52 and np.max(source)<16
source=source.reshape(52,92)
reference=np.asarray(Image.open(ROOT/'DOS_Manacles_F2_Original_Reference_20261121.png').convert('RGB'))
assert reference.shape[:2] == (136,224)
source_w,source_h,dest_w,dest_h=92,52,60,34
# FIRES image offset 0x2389B..0x238B2: 16-bit (src_dimension << 6) / dest_dimension.
step_x=(source_w<<6)//dest_w
step_y=(source_h<<6)//dest_h
# FIRES 0x23701/0x23733 and 0x238BD/0x238E7: accumulator begins at half step.
x=((step_x>>1)+np.arange(dest_w)*step_x)>>6
y=((step_y>>1)+np.arange(dest_h)*step_y)>>6
assert x.max()<source_w and y.max()<source_h
lut=np.asarray([0,12,1,3,4,3,6,7,5,9,10,11,0,2,14,13],dtype=np.uint8)
actual=lut[source[np.ix_(y,x)]]
ref=reference[40:74,82:142]
# Values established directly by uniform reference colors of mapped source pixels;
# no nearest-color fit, tolerance, or sprite-specific pixel adjustment.
colors={0:(0,0,0),1:(109,109,109),2:(146,146,146),3:(109,36,0),12:(73,73,73),13:(182,182,182)}
nontransparent=actual!=10
matches=np.ones(actual.shape,dtype=bool)
index_counts={}
for idx in sorted(map(int,np.unique(actual))):
    mask=actual==idx
    if idx==10:continue
    matches[mask]=np.all(ref[mask]==colors[idx],axis=1)
    index_counts[str(idx)]={'pixels':int(mask.sum()),'rgb':list(colors[idx]),'exact_match_pixels':int(matches[mask].sum())}
assert np.all(matches[nontransparent]), 'Indexed DOS candidate does not match original screenshot'
ref_brown=np.all(ref==colors[3],axis=2)
pred_brown=actual==3
assert np.array_equal(ref_brown,pred_brown), 'Brown mask mismatch'
summary={
 'source':'DOS_Manacles332_Indexed_20261121.bin','native_size':[source_w,source_h],
 'reference':'DOS_Manacles_F2_Original_Reference_20261121.png','reference_crop_xywh':[82,40,60,34],
 'FIRES_offsets':{'main':'0x23858','unmapped':'0x236EC','palette':'0x2379D'},
 'sampling':{'fixed_point_bits':6,'x_step':int(step_x),'y_step':int(step_y),
             'x_source_positions':x.tolist(),'y_source_positions':y.tolist()},
 'palette_lut_F2':lut.tolist(),
 'visible_pixels':int(nontransparent.sum()),
 'transparent_pixels':int((~nontransparent).sum()),
 'verified_nontransparent_pixels':int(matches[nontransparent].sum()),
 'brown_pixels':int(pred_brown.sum()),
 'brown_pixel_mismatches':int(np.count_nonzero(ref_brown!=pred_brown)),
 'visible_pixel_mismatches':int(np.count_nonzero(~matches&nontransparent)),
 'per_index':index_counts,
 'limits':['Transparency correctly selects unchanged wall background; this test does not reconstruct a separate background image.',
           'Validated only at Manacles center F2 and these six visible output color indices.',
           'F3, side views, other ornaments, and clipping interactions still require independent DOS reference checks.']}
(ROOT/'DOS_F0129_Exact_6bit_Manacles_F2_20261121.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps({k:v for k,v in summary.items() if k in ('sampling','visible_pixels','transparent_pixels','verified_nontransparent_pixels','brown_pixels','brown_pixel_mismatches','visible_pixel_mismatches')},indent=2))
