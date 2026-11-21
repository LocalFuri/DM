#!/usr/bin/env python3
"""Offline indexed DOS ornament candidate renderer — research, no Unity edits.

Uses prior source-derived geometry CSV; original native indexed GRAPHICS.DAT via dmcsb;
6-bit FIRES F0129 sampling; D2/D3 palette remaps; mirroring and visible-window crop.
It makes hypotheses for perspectives not independently DOS-reference verified.

Usage:
  python DOS_Generic_6bit_Indexed_Renderer_20261121.py GRAPHICS_DOS.ZIP DOS_All_Ornament_Geometry_F0635_Corrected_20261121.csv
  python DOS_Generic_6bit_Indexed_Renderer_20261121.py GRAPHICS_DOS.ZIP DOS_All_Ornament_Geometry_F0635_Corrected_20261121.csv --all
"""
from __future__ import annotations
import argparse, csv, json, zipfile
from collections import Counter
from pathlib import Path
import numpy as np
from PIL import Image

D2=np.array([0,12,1,3,4,3,6,7,5,9,10,11,0,2,14,13],dtype=np.uint8)
D3=np.array([0,0,12,3,4,3,0,6,3,9,10,11,0,1,0,2],dtype=np.uint8)
IDENTITY=np.arange(16,dtype=np.uint8)
TRANSPARENT=10


def raw_graphics(path: Path)->bytes:
    if zipfile.is_zipfile(path):
        with zipfile.ZipFile(path) as arc:
            names=[n for n in arc.namelist() if n.upper().endswith('GRAPHICS.DAT')]
            if len(names)!=1: raise ValueError(f'Expected exactly one GRAPHICS.DAT, found {names}')
            return arc.read(names[0])
    if path.name.upper()!='GRAPHICS.DAT': raise ValueError('Need GRAPHICS.DAT or ZIP containing it')
    return path.read_bytes()


def shrink(src:np.ndarray, w:int, h:int)->np.ndarray:
    """FIRES 6-bit integer stepping: floor((src_dim*64)/dst_dim), half-step origin."""
    sh,sw=src.shape
    if min(sw,sh,w,h)<=0: raise ValueError(f'Illegal dimensions {(sw,sh,w,h)}')
    sx=(sw<<6)//w;sy=(sh<<6)//h
    if sx<=0 or sy<=0:raise ValueError('6-bit step underflow: unsupported enlargement')
    xx=((sx>>1)+np.arange(w,dtype=np.int64)*sx)>>6
    yy=((sy>>1)+np.arange(h,dtype=np.int64)*sy)>>6
    if xx.max()>=sw or yy.max()>=sh: raise ValueError('Source lookup outside native bitmap')
    return src[np.ix_(yy,xx)]


def indexed_png(path:Path, pixels:np.ndarray)->None:
    if pixels.ndim!=2 or pixels.dtype!=np.uint8 or np.any(pixels>15):raise ValueError('Not 4-bit indices')
    h,w=pixels.shape
    im=Image.frombytes('P',(w,h),pixels.tobytes())
    pal=[]
    for n in range(256):
        val=n*16 if n<16 else 0
        pal.extend([val,val,val])
    im.putpalette(pal)
    im.save(path,transparency=TRANSPARENT)
    # Verify decoded original indices, not their RGB rendering.
    with Image.open(path) as verify:
        if verify.tobytes()!=pixels.tobytes():raise RuntimeError(f'Indexed PNG round trip failed: {path}')


def render_row(geo, indexed_source):
    w=int(geo['width']);h=int(geo['height'])
    if indexed_source.ndim!=2:raise ValueError('Source is not rectangular')
    if (indexed_source.shape[1],indexed_source.shape[0])!=(int(geo['native_width']),int(geo['native_height'])):
        raise ValueError(f'Source dimensions mismatch for graphic {geo["native_graphic"]}')
    if np.any(indexed_source>15):raise ValueError('Palette index outside 0..15')
    scaled=shrink(indexed_source,w,h)
    group=geo['palette_group']
    palette= D2 if group=='D2' else D3 if group.startswith('D3') else IDENTITY if group=='native' else None
    if palette is None:raise ValueError(f'Unrecognized palette group {group}')
    mapped=palette[scaled]
    if int(geo['mirrored_horizontally']): mapped=mapped[:,::-1]
    cw=int(geo['clipped_width']);ch=int(geo['clipped_height'])
    if cw<=0 or ch<=0:
        if cw!=0 or ch!=0:raise ValueError('Inconsistent empty clip')
        return mapped,None
    ox=int(geo['clipped_x'])-int(geo['x']);oy=int(geo['clipped_y'])-int(geo['y'])
    if not (0<=ox and 0<=oy and ox+cw<=w and oy+ch<=h):
        raise ValueError(f'Clipping outside image for ornament {geo["ornament_type"]} view {geo["view_index"]}')
    visible=mapped[oy:oy+ch, ox:ox+cw].copy()
    return mapped,visible


def main():
    parser=argparse.ArgumentParser(description=__doc__,formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('graphics',type=Path)
    parser.add_argument('geometry',type=Path)
    parser.add_argument('--all',action='store_true',help='Render all 60 types instead of only Hook (4) and Manacles (36)')
    parser.add_argument('--output',type=Path,default=Path('DOS_6bit_Indexed_Output_20261121'))
    args=parser.parse_args()
    try:from dmcsb import open_graphics
    except ImportError as ex:raise SystemExit('Install dmcsb[pil] on this PC first; no fallback decoder') from ex
    graph=open_graphics(data=raw_graphics(args.graphics))
    with args.geometry.open(encoding='utf-8-sig',newline='') as f: rows=list(csv.DictReader(f))
    chosen=[r for r in rows if args.all or int(r['ornament_type']) in (4,36)]
    if not chosen:raise RuntimeError('No ornament types found')
    if len(chosen)!=(900 if args.all else 30):
        raise RuntimeError(f'Incomplete geometry set: {len(chosen)} rows')
    args.output.mkdir(parents=True,exist_ok=True)
    cache={};out=[]
    for row in chosen:
        idx=int(row['native_graphic'])
        if idx not in cache:
            original=graph.image_indexed(idx)
            if original is None or len(original)!=3:raise RuntimeError(f'Could not extract native graphic {idx}')
            nw,nh,p=original
            arr=np.frombuffer(bytes(p),dtype=np.uint8)
            if arr.size!=nw*nh:raise RuntimeError(f'Incomplete native pixels for graphic {idx}')
            cache[idx]=arr.reshape((nh,nw))
        full,visible=render_row(row,cache[idx])
        prefix=f"T{int(row['ornament_type']):02d}_V{int(row['view_index']):02d}_G{idx:03d}"
        full.tofile(args.output/f'{prefix}_full.bin')
        indexed_png(args.output/f'{prefix}_full.png',full)
        if visible is not None:
            visible.tofile(args.output/f'{prefix}_visible.bin')
            indexed_png(args.output/f'{prefix}_visible.png',visible)
        out.append(dict(type=int(row['ornament_type']),view=int(row['view_index']),graphic=idx,
                        origin=[int(row['x']),int(row['y'])],size=list(reversed(full.shape)),
                        visible_origin=None if visible is None else [int(row['clipped_x']),int(row['clipped_y'])],
                        visible_size=[0,0] if visible is None else list(reversed(visible.shape)),
                        transparent=int(np.sum(full==TRANSPARENT)),
                        source=list(reversed(cache[idx].shape)),
                        sampled_index_histogram={str(i):int(n) for i,n in Counter(full.flat).items()}))
    (args.output/'manifest.json').write_text(json.dumps({'status':'UNVERIFIED except Manacles F2 regression',
        'sampling':'FIRES 6-bit integer step, half-step start',
        'palette':'D2/D3 LUT mapped after sampling',
        'caveats':['No independent DOS screenshot match for F3 or S2/S3',
                   'No wall occlusion compositing',
                   'Mirror-before/after-clipping implementation requires independent side-view reference',
                   'Palette PNG is diagnostic grayscale; .bin retains correct DOS palette indices'],
        'rows':out},indent=2)+'\n',encoding='utf-8')
    print(f'SUCCESS: wrote {len(out)} views to {args.output}')
    print('IMPORTANT: F3/S2/S3 are DOS-algorithm predictions, not independently pixel-verified.')

if __name__=='__main__':main()
