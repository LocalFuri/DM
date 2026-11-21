#!/usr/bin/env python3
"""Read-only diagnostic. This is NOT an implementation of the original DOS shrink routine.
Compares the 2026 ScummVM fixed-point approximation with two earlier
sampling hypotheses against one color class of a genuine DOS screenshot.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image
ROOT = Path(__file__).resolve().parent
src = np.fromfile(ROOT / 'DOS_Manacles332_Indexed_20261121.bin', dtype=np.uint8)
assert src.size == 92*52 and src.max() < 16
src = src.reshape(52, 92)
reference = np.asarray(Image.open(ROOT / 'image(20261121-185132).png').convert('RGB'))
assert reference.shape[0]>=74 and reference.shape[1]>=142
brown = np.all(reference[40:74, 82:142] == [109,36,0], axis=2)
lut = np.array([0,12,1,3,4,3,6,7,5,9,10,11,0,2,14,13],dtype=np.uint8)

def score(xs,ys):
    assert xs.min()>=0 and xs.max()<92 and ys.min()>=0 and ys.max()<52
    predicted = lut[src[np.ix_(ys,xs)]]==3
    match=int(np.count_nonzero(predicted&brown))
    extra=int(np.count_nonzero(predicted&~brown))
    missing=int(np.count_nonzero(~predicted&brown))
    return dict(matching_brown=match, extra_brown=extra, missing_brown=missing,
                f1=round(2*match/(2*match+extra+missing),6))
xs=np.arange(60,dtype=np.int64);ys=np.arange(34,dtype=np.int64)
methods={
    'edge_exact_fraction':score(xs*92//60, ys*52//34),
    'center_exact_fraction':score((2*xs+1)*92//120,(2*ys+1)*52//68),
    'scummvm_2026_fixedpoint_32768':score(xs*((32768*92)//60)//32768,ys*((32768*52)//34)//32768)
}
assert methods['edge_exact_fraction']['matching_brown']==205
assert methods['center_exact_fraction']['matching_brown']==216
assert methods['scummvm_2026_fixedpoint_32768']['matching_brown']==203
output={
 'inputs':{'indexed_source':'DOS_Manacles332_Indexed_20261121.bin','source_size':[92,52],
           'reference':'image(20261121-185132).png','reference_crop':[82,40,60,34],
           'reference_brown_rgb':[109,36,0], 'reference_brown_count':int(brown.sum())},
 'palette_lut_f2':lut.tolist(), 'metrics':methods,
 'source':'https://raw.githubusercontent.com/scummvm/scummvm/master/engines/dm/gfx.cpp, blitToBitmapShrinkWithPalChange (accessed November 21 2026)',
 'scope':'Only brown-mask agreement for one ornament. No exact DOS F0129 implementation or full-pixel comparison.',
 'conclusion':'2026 ScummVM fixed-point sampling does not match the original DOS brown mask; cannot be accepted as original DOS behavior.'
}
(ROOT/'DOS_F0129_FixedPoint_Crosscheck_20261121.json').write_text(json.dumps(output,indent=2)+'\n')
print(json.dumps(methods,indent=2))
