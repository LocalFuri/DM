"""Read-only Manacles S3 Left regression against original DOS and Unity screenshots.

python DOS_Manacles_S3_Geometry_Regression_20261122.py \
  --graphics GRAPHICS_DOS.zip --source Manacles_S1.png \
  --dos DOS_320x200.png --unity Unity_224x136.png

The DOS screenshot has the 224x136 viewport at (0,33). The Unity screenshot
is viewport-only. No files in the Unity project are modified.
"""
import argparse
import struct
import zipfile
from pathlib import Path
from PIL import Image

BROWN = {(109, 36, 0), (145, 72, 0), (146, 73, 0)}

def graphics_bytes(path):
    path = Path(path)
    if zipfile.is_zipfile(path):
        with zipfile.ZipFile(path) as archive:
            matches = [name for name in archive.namelist() if name.upper().endswith('GRAPHICS.DAT')]
            if len(matches) != 1:
                raise ValueError('Expected precisely one GRAPHICS.DAT member')
            return archive.read(matches[0])
    return path.read_bytes()

def layout_records(data):
    for pos in range(len(data) - 96):
        if data[pos:pos+4] != b'\x0d\xfc\x17\x00':
            continue
        count = struct.unpack_from('<H', data, pos+2)[0]
        ranges = [struct.unpack_from('<HH', data, pos+4+i*4) for i in range(count)]
        if any(b<a for a,b in ranges) or sum(b-a+1 for a,b in ranges) != 1133:
            continue
        cur = pos + 4 + count*4
        if cur + 1133*8 > len(data):
            continue
        records = {}
        for a,b in ranges:
            for index in range(a,b+1):
                records[index] = struct.unpack_from('<hhhh', data, cur)
                cur += 8
        return pos,records
    raise ValueError('Resource 696 not found')

def brown_mask_image(image, rect, offset_y=0):
    x0,y0,w,h=rect
    pixels=image.convert('RGBA')
    return {(x,y) for y in range(h) for x in range(w)
            if pixels.getpixel((x+x0,y+y0+offset_y))[:3] in BROWN}

def main(args):
    src=Image.open(args.source).convert('RGBA')
    dos=Image.open(args.dos).convert('RGBA')
    unity=Image.open(args.unity).convert('RGBA')
    if src.size!=(32,42) or dos.size!=(320,200) or unity.size!=(224,136):
        raise ValueError(f'Unexpected image sizes: src={src.size}, DOS={dos.size}, Unity={unity.size}')
    pos,records=layout_records(graphics_bytes(args.graphics))
    di=2; zone=1004+1*15+di;scale=14
    kind,parent,cx,cy=records[zone]
    if (kind,parent)!=(0,4):
        raise ValueError(f'Unexpected zone: {zone}={records[zone]}')
    sw,sh=src.size
    w=(sw*scale+(scale>>1))>>5
    h=(sh*scale+(scale>>1))>>5
    x0=cx-((w+1)>>1);y0=cy-((h+1)>>1)
    rect=(x0,y0,w,h)
    stepx=(sw*64)//w;stepy=(sh*64)//h
    xx=[min(sw-1,(i*stepx+(stepx>>1))>>6) for i in range(w)]
    yy=[min(sh-1,(i*stepy+(stepy>>1))>>6) for i in range(h)]
    predicted={(x,y) for y in range(h) for x in range(w)
               if src.getpixel((xx[x],yy[y]))[3]>0 and src.getpixel((xx[x],yy[y]))[:3] in BROWN}
    observed=brown_mask_image(dos,rect,33)
    unity_mask=brown_mask_image(unity,rect)
    print(f'Resource 696 offset={pos}; zone={zone}, DI={di}, center=({cx},{cy})')
    print(f'Native bitmap={sw}x{sh}; scale={scale}/32; full bitmap rect={rect}')
    print(f'X samples: {xx}; Y samples: {yy}')
    print('Row masks: CALCULATED | PC DOS | UNITY')
    for y in range(h):
        masks=[''.join('#' if (x,y) in v else '.' for x in range(w)) for v in (predicted,observed,unity_mask)]
        print(' | '.join(masks))
    print(f'Brown pixels calculated={len(predicted)}, DOS={len(observed)}, Unity={len(unity_mask)}')
    print(f'Calculated vs DOS differences: {len(predicted ^ observed)}')
    print(f'Calculated vs Unity differences: {len(predicted ^ unity_mask)}')
    assert not (predicted ^ observed), 'Prediction does not match DOS brown-mask reference'
    print('PASS: Manacles S3 Left predicted brown mask matches DOS, cell for cell')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--graphics',required=True)
    p.add_argument('--source',required=True)
    p.add_argument('--dos',required=True)
    p.add_argument('--unity',required=True)
    main(p.parse_args())
