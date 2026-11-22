"""Read-only DOS ornament geometry/sampling diagnostic.

Usage:
 python DOS_Ornament_Geometry_Regression_20261122.py --graphics GRAPHICS_DOS.ZIP \
   --source Hook_S1_14x19.png --s2-dos S2_DOS.png --s3-dos S3_DOS_CROPPED.png

Requires Pillow. S2 screenshot must be 224x136. S3 screenshot must be the
148x136 crop of the original viewport starting at viewport X=76.
All image coordinates are viewport-local, top-down; crop offsets restored.
"""
import argparse, hashlib, struct, zipfile
from pathlib import Path
from PIL import Image

BROWN_SOURCE = {(146, 73, 0), (109, 36, 0)}
BROWN_DOS = (109, 36, 0)

def graphics_bytes(path):
    path=Path(path)
    if zipfile.is_zipfile(path):
        with zipfile.ZipFile(path) as z:
            matches=[n for n in z.namelist() if n.upper().endswith('GRAPHICS.DAT')]
            if len(matches)!=1: raise ValueError('Expected exactly one GRAPHICS.DAT member')
            return z.read(matches[0])
    return path.read_bytes()

def resource696(data):
    # Original resource begins with 0xFC0D, followed by 23 ranges.
    for pos in range(len(data)-96):
        if data[pos:pos+4]!=b'\x0d\xfc\x17\x00': continue
        nr=struct.unpack_from('<H',data,pos+2)[0]
        ranges=[struct.unpack_from('<HH',data,pos+4+4*i) for i in range(nr)]
        if any(b<a for a,b in ranges):continue
        if sum(b-a+1 for a,b in ranges)!=1133:continue
        offset=pos+4+4*nr; rec={}
        for a,b in ranges:
            for index in range(a,b+1):
                rec[index]=struct.unpack_from('<hhhh',data,offset)
                offset+=8
        if offset>len(data):continue
        return pos,rec
    raise ValueError('Resource 696 not found')

def test_view(label, records, source, screenshot, di, scale, crop_x):
    zone=1004+di
    kind,parent,cx,cy=records[zone]
    if kind!=0 or parent!=4:raise ValueError(f'Unexpected layout record: {zone}: {records[zone]}')
    sw,sh=source.size
    # M078_SCALED_DIMENSION from DEFS.H
    w=(sw*scale+(scale>>1))>>5
    h=(sh*scale+(scale>>1))>>5
    x0=cx-((w+1)>>1); y0=cy-((h+1)>>1)
    stepx=(sw*64)//w; stepy=(sh*64)//h
    sx=[min(sw-1,(x*stepx+(stepx>>1))>>6) for x in range(w)]
    sy=[min(sh-1,(y*stepy+(stepy>>1))>>6) for y in range(h)]
    src=source.load(); ref=Image.open(screenshot).convert('RGB').load()
    expected={(x,y) for y in range(h) for x in range(w)
             if src[sx[x],sy[y]][3]>0 and src[sx[x],sy[y]][:3] in BROWN_SOURCE}
    actual={(x,y) for y in range(h) for x in range(w)
            if ref[x0+x-crop_x,y0+y]==BROWN_DOS}
    differences=expected.symmetric_difference(actual)
    print(f'{label}: DI={di}, zone={zone}, layout center=({cx},{cy}), scale={scale}/32')
    print(f'  source={sw}x{sh}, scaled={w}x{h}, rect=({x0},{y0},{w},{h})')
    print(f'  source X={sx}; source Y={sy}')
    print(f'  brown expected={len(expected)}, DOS={len(actual)}, differing positions={len(differences)}')
    for y in range(h):
        a=''.join('#' if (x,y) in expected else '.' for x in range(w))
        b=''.join('#' if (x,y) in actual else '.' for x in range(w))
        print(f'    {a}  {b}')
    if differences: raise AssertionError(f'{label}: brown pixel mismatch at {sorted(differences)}')

if __name__=='__main__':
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--graphics',required=True)
    ap.add_argument('--source',required=True)
    ap.add_argument('--s2-dos',required=True)
    ap.add_argument('--s3-dos',required=True)
    a=ap.parse_args()
    data=graphics_bytes(a.graphics)
    pos,records=resource696(data)
    source=Image.open(a.source).convert('RGBA')
    assert source.size==(14,19),f'Expected native Hook side source 14x19, got {source.size}'
    assert Image.open(a.s2_dos).size==(224,136),'S2 needs original 224x136 viewport screenshot'
    assert Image.open(a.s3_dos).size==(148,136),'S3 needs 148x136 crop beginning viewport X=76'
    print('GRAPHICS.DAT SHA256:',hashlib.sha256(data).hexdigest())
    print(f'Resource 696 at {pos}, {len(records)} layout records')
    print('Brown only. Other palette entries and transparency are not validated.')
    test_view('Hook S2 Left',records,source,a.s2_dos,di=7,scale=21,crop_x=0)
    test_view('Hook S3 Left',records,source,a.s3_dos,di=2,scale=14,crop_x=76)
    print('PASS: 2 views; 28 / 28 DOS brown pixels matched, zero mismatching rectangle cells.')
