#!/usr/bin/env python3
"""Research-only PC DOS ornament pixel candidates (NOT a verified F0129 implementation).

Usage (working directory contains the ZIP and corrected geometry CSV):
 python DOS_Ornament_Pixel_Sampling_Candidates_20261121.py GRAPHICS_DOS.ZIP DOS_All_Ornament_Geometry_F0635_Corrected_20261121.csv

Requires locally installed dmcsb and Pillow (no installation by this script).
Writes a labeled comparison PNG and a JSON manifest in the current directory.
No game or Unity files are modified.

Hypotheses:
 - edge: source pixel = floor(destination_index * source_size / dest_size)
 - center: source pixel = floor((destination_index + .5) * source_size / dest_size)
Neither has been proven to be F0129's pixel selection. There may also be palette
changes absent from this experiment. Only native pixels and source-derived
geometry are used; no Hook/Manacles positioning exceptions.
"""
from __future__ import annotations
import argparse,csv,json,math,zipfile
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

SLOTS = [(10,'F2 front'),(5,'F3 front'),(7,'S2 left'),(8,'S2 right'),(2,'S3 left'),(3,'S3 right')]
ORNAMENTS = {4:'Hook',36:'Manacles'}

def read_graphics(path):
    if zipfile.is_zipfile(path):
        with zipfile.ZipFile(path) as z:
            names=[n for n in z.namelist() if n.upper().endswith('GRAPHICS.DAT')]
            if len(names)!=1: raise ValueError('Expected exactly one GRAPHICS.DAT')
            return z.read(names[0])
    return path.read_bytes()

def sample_index(dst,src_size,dst_size,method):
    if method=='edge': return min(src_size-1,(dst*src_size)//dst_size)
    return min(src_size-1,((2*dst+1)*src_size)//(2*dst_size))

def scaled_rgba(src, width, height, method):
    src=src.convert('RGBA'); sw,sh=src.size
    if width < 1 or height < 1: raise ValueError('Zero-size destination')
    out=Image.new('RGBA',(width,height)); s=src.load(); d=out.load()
    for y in range(height):
        yy=sample_index(y,sh,height,method)
        for x in range(width):
            xx=sample_index(x,sw,width,method)
            d[x,y]=s[xx,yy]
    return out

def render_one(src, rec, method):
    w,h=int(rec['width']),int(rec['height'])
    picture=scaled_rgba(src,w,h,method)
    if int(rec['mirrored_horizontally']): picture=picture.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    cx,cy=rec['clipped_x'],rec['clipped_y']
    if cx in ('',None) or cy in ('',None): return Image.new('RGBA',(224,136)),picture
    cx,cy=int(cx),int(cy)
    sx,sy=int(rec['source_x']),int(rec['source_y'])
    cw,ch=int(rec['clipped_width']),int(rec['clipped_height'])
    if sx<0 or sy<0 or sx+cw>w or sy+ch>h or cx<0 or cy<0 or cx+cw>224 or cy+ch>136:
        raise ValueError('Geometry clipping invalid; refuse silent fallback')
    visible=picture.crop((sx,sy,sx+cw,sy+ch))
    canvas=Image.new('RGBA',(224,136),(30,30,30,255))
    canvas.alpha_composite(visible,(cx,cy))
    return canvas,picture

def main():
    p=argparse.ArgumentParser(description=__doc__,formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument('graphics',type=Path)
    p.add_argument('geometry_csv',type=Path)
    p.add_argument('--prefix',default='DOS_Ornament_Pixel_Candidates_20261121')
    a=p.parse_args()
    # Dependency imported here to permit --help when not installed
    from dmcsb import open_graphics
    data=read_graphics(a.graphics)
    gfx=open_graphics(data=data)
    with a.geometry_csv.open(newline='',encoding='utf-8-sig') as f: rows=list(csv.DictReader(f))
    keyed={(int(r['ornament_type']),int(r['view_index'])):r for r in rows}
    expected={(t,view) for t in ORNAMENTS for view,_ in SLOTS}
    if not expected.issubset(keyed):
        raise ValueError('Corrected geometry CSV lacks required Hook/Manacles F2/F3/S2/S3 rows')
    panel_w,panel_h=224*2+16,136+34
    sheet=Image.new('RGB',(panel_w*len(SLOTS),panel_h*len(ORNAMENTS)),(24,24,24))
    dr=ImageDraw.Draw(sheet)
    manifest=[]
    for ri,(orn_type,name) in enumerate(ORNAMENTS.items()):
        for ci,(slot,label) in enumerate(SLOTS):
            r=keyed[(orn_type,slot)]
            graphic=int(r['native_graphic'])
            native=gfx.to_pil(graphic)
            if native is None:raise ValueError(f'Decoder returned no image for graphic {graphic}')
            if tuple(native.size)!=(int(r['native_width']),int(r['native_height'])):
                raise ValueError(f'Graphic {graphic}: decoded dimensions {native.size}, metadata {r["native_width"]}x{r["native_height"]}')
            for pi,method in enumerate(('edge','center')):
                panel,picture=render_one(native,r,method)
                xx=ci*panel_w+pi*(224+16)
                yy=ri*panel_h+30
                sheet.paste(panel.convert('RGB'),(xx,yy))
                dr.text((xx+3,yy-18),f'{name} {label} / {method}',fill=(245,245,245))
            manifest.append({'ornament':name,'type':orn_type,'slot':label,'view_index':slot,
                'native_graphic':graphic,'native_dimensions':list(native.size),
                'scaled_dimensions':[int(r['width']),int(r['height'])],
                'rectangle':[int(r['x']),int(r['y']),int(r['width']),int(r['height'])],
                'mirror':bool(int(r['mirrored_horizontally'])),
                'clip':[int(r[k]) if r[k] not in ('',None) else None for k in
                        ('clipped_x','clipped_y','clipped_width','clipped_height','source_x','source_y')]})
    output=Path(a.prefix+'_Comparison.png')
    sheet.save(output)
    manifest_file=Path(a.prefix+'_Manifest.json')
    manifest_file.write_text(json.dumps({'status':'experimental: sampling and palette are not DOS-verified',
                        'sampling':['edge','center'],'cases':manifest},indent=2)+'\n',encoding='utf-8')
    print('Wrote:',output,output.stat().st_size,'bytes')
    print('Wrote:',manifest_file,manifest_file.stat().st_size,'bytes')
    print('Upload the comparison PNG for pixel comparison. Nothing in Unity has been changed.')

if __name__=='__main__':main()
