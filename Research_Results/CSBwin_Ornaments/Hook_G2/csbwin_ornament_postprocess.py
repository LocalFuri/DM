#!/usr/bin/env python3
"""Reconstruct CSBwin ornament draw masks from raw pre-blit planar exports.

Usage: python csbwin_ornament_postprocess.py extraction.zip --ordinal 2 \
           --graphic-ids 129,130 --name Hook --out Hook_G2_Output

Python >=3.9, Pillow required. The byte-aligned planar pixels are decoded
using 4 interleaved big-endian 16-bit bitplanes per 16 horizontal pixels.
"""
import argparse
import csv
import io
import json
import re
import zipfile
from pathlib import Path
from PIL import Image, ImageDraw

SLOTS = [
    'S3_Left','S3_Right','F3_LeftSlice','F3_Center','F3_RightSlice',
    'S2_Left','S2_Right','F2_LeftSlice','F2_Center','F2_RightSlice',
    'S1_Left','S1_Right','F1_Center',
]


def parse_kv(s):
    return dict(line.split('=', 1) for line in s.splitlines() if '=' in line)


def extract_records(z, ordinal):
    pat = re.compile(rf'(^|/)CSB_Ornament_G{ordinal}_P(\d\d)\.txt$')
    records=[]
    for n in z.namelist():
        m=pat.search(n)
        if not m: continue
        prefix=n[:-4]
        d=parse_kv(z.read(n).decode('utf-8-sig'))
        palname=prefix+'.palette.txt'; rawname=prefix+'.planar'
        if not (palname in z.namelist() and rawname in z.namelist()):
            raise ValueError('Missing planar/palette companion for '+n)
        records.append((int(m.group(2)), d, z.read(rawname),z.read(palname).decode('utf-8-sig'),n))
    return sorted(records)


def parse_palette(text):
    cmap={}
    for m in re.finditer(r'^index\s+(\d+):.*RGB8=(\d+),(\d+),(\d+)', text,re.M):
        idx,r,g,b=map(int,m.groups())
        cmap[idx]=(r,g,b)
    if set(cmap)!=set(range(16)):
        raise ValueError('Palette does not contain exactly indices 0..15')
    return cmap


def decode(raw, width_bytes, height):
    if width_bytes<=0 or width_bytes%8 or height<=0:
        raise ValueError('Bad planar stride/height')
    if len(raw)!=width_bytes*height:
        raise ValueError(f'Planar byte count mismatch {len(raw)} != {width_bytes*height}')
    width=width_bytes//8*16
    indexes=Image.new('P',(width,height))
    pixels=indexes.load()
    for y in range(height):
        for x in range(width):
            bit=15-(x%16)
            offset=y*width_bytes+(x//16)*8
            value=0
            for k in range(4):
                plane=int.from_bytes(raw[offset+2*k:offset+2*k+2],'big')
                value|=((plane>>bit)&1)<<k
            pixels[x,y]=value
    return indexes


def rgb_ornament(idx_im,pal):
    out=Image.new('RGBA',idx_im.size,(0,0,0,0))
    src=idx_im.load(); dest=out.load()
    for y in range(idx_im.height):
        for x in range(idx_im.width):
            i=src[x,y]
            dest[x,y]=(*pal[i],0 if i==10 else 255)
    return out


def process(input_zip, ordinal, allowed_ids, name, output):
    output.mkdir(parents=True,exist_ok=True)
    (output/'viewport').mkdir(exist_ok=True)
    (output/'tight').mkdir(exist_ok=True)
    metadata=[]; excluded=[]; warnings=[]
    with zipfile.ZipFile(input_zip) as z:
        records=extract_records(z,ordinal)
        if not records: raise ValueError(f'No G{ordinal} records in archive')
        for position,d,raw,paltext,source_name in records:
            graphic_id=int(d['graphic_id'])
            if graphic_id not in allowed_ids:
                excluded.append(dict(position=position,graphic_id=graphic_id,source=source_name,reason='Graphic ID outside selected ornament identity'))
                continue
            if position>=len(SLOTS): raise ValueError(f'Unknown relative position {position}')
            stride=int(d['source_stride_bytes']); height=int(d['source_height'])
            index_img=decode(raw,stride,height)
            x1,y1,x2,y2=(int(d['destination_'+k]) for k in ('x1','y1','x2','y2'))
            width=x2-x1+1
            rect_height=y2-y1+1
            sx=int(d['source_offset_x']); sy=int(d['source_offset_y'])
            if sx<0 or sy<0 or sx+width>index_img.width or sy+rect_height>index_img.height:
                warnings.append({'position':position,'reason':'Source rectangle exceeds planar bounds; skipped', 'src_x':sx,'src_y':sy,'width':width,'height':rect_height,'bitmap_width':index_img.width,'bitmap_height':index_img.height})
                continue
            if (width,height)==(0,0): continue
            visible_indices=index_img.crop((sx,sy,sx+width,sy+rect_height))
            viewport=rgb_ornament(visible_indices,parse_palette(paltext))
            bbox=viewport.getbbox()
            tight=viewport.crop(bbox) if bbox is not None else Image.new('RGBA',(1,1))
            base=f'{name}_G{ordinal}_P{position:02d}_{SLOTS[position]}'
            vp_file='viewport/'+base+'_Viewport.png'
            tight_file='tight/'+base+'_Tight.png'
            viewport.save(output/vp_file)
            tight.save(output/tight_file)
            left,top,right,bottom=bbox if bbox is not None else (0,0,0,0)
            item={
                'graphic_ordinal':ordinal,'graphic_id':graphic_id,'relative_position':position,'slot_name':SLOTS[position],
                'source_path':source_name, 'viewport_file':vp_file,'tight_file':tight_file,
                'destination_x1':x1,'destination_y1':y1,'destination_x2':x2,'destination_y2':y2,
                'source_offset_x':sx,'source_offset_y':sy,'source_stride_bytes':stride,'source_height':height,
                'viewport_width':width,'viewport_height':rect_height,
                'tight_crop_left':left,'tight_crop_top':top,'tight_crop_right_exclusive':right,'tight_crop_bottom_exclusive':bottom,
                'tight_width':right-left,'tight_height':bottom-top,
                'tight_screen_x1':x1+left,'tight_screen_y1':y1+top,
                'transparency_index':10,'opaque_black_palette_index':0,
                'has_visible_pixels':bbox is not None,
                'method':'pre-blit pixel selection; palette index 10 transparent; indices 0..9 and 11..15 opaque',
            }
            metadata.append(item)
    metadata=sorted(metadata,key=lambda r:r['relative_position'])
    if metadata:
        fields=list(metadata[0].keys())
        with (output/'metadata.csv').open('w',newline='',encoding='utf-8') as f:
            writer=csv.DictWriter(f,fieldnames=fields);writer.writeheader();writer.writerows(metadata)
    (output/'metadata.json').write_text(json.dumps({'name':name,'ordinal':ordinal,'accepted_graphic_ids':sorted(allowed_ids),
        'records':metadata,'excluded':excluded,'warnings':warnings,
        'limits':['These are isolated pre-blit ornament pixels, not a screenshot of the complete stone wall.',
                  'Metadata captures one runtime palette and one representative cache state per export.',
                  'For shared F2/F3 derived graphics, visual equivalence needs verification against actual game rendering.']},indent=2),encoding='utf-8')
    # Preview using nearest-neighbor for display only
    sheet=Image.new('RGB',(1056,((len(metadata)+3)//4)*180),(235,235,235))
    draw=ImageDraw.Draw(sheet)
    for i,r in enumerate(metadata):
        im=Image.open(output/r['viewport_file']).convert('RGBA')
        x=(i%4)*264+9;y=(i//4)*180+7
        draw.text((x,y),f"P{r['relative_position']:02d} {r['slot_name']} {im.width}x{im.height}",fill=(20,20,20))
        zoom=max(1,min(6,240//im.width,140//im.height))
        large=im.resize((im.width*zoom,im.height*zoom),Image.Resampling.NEAREST)
        sheet.paste(large,(x,y+26),large)
    sheet.save(output/'preview.png')
    return metadata,excluded,warnings


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('archive',type=Path)
    p.add_argument('--ordinal',type=int,required=True)
    p.add_argument('--graphic-ids',required=True,help='Comma separated side/front source graphic IDs')
    p.add_argument('--name',required=True)
    p.add_argument('--out',type=Path,required=True)
    a=p.parse_args()
    ids={int(i.strip()) for i in a.graphic_ids.split(',')}
    processed,excluded,warnings=process(a.archive,a.ordinal,ids,a.name,a.out)
    print(f'{len(processed)} reconstructed, {len(excluded)} excluded, {len(warnings)} warnings')
    for item in warnings: print('WARNING',item)

if __name__=='__main__': main()
