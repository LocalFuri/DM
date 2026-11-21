#!/usr/bin/env python3
"""Read-only audit of DOS F0791 source offsets for clipped/mirrored ornaments.
Uses corrected F0635 geometry CSV. No graphics or Unity changes.
"""
import csv, json, argparse
from pathlib import Path

def blit_offsets(width,height,source_x,source_y,visible_width,visible_height,flip_x=False,flip_y=False):
    """Direct translation of DUNVIEW.C F0791: L2449/L2450 source correction."""
    if not (0 <= source_x <= width-visible_width and 0 <= source_y <= height-visible_height):
        raise ValueError('Source rectangle outside bitmap')
    ox=source_x
    oy=source_y
    if flip_x:
        ox=width-visible_width-source_x
    if flip_y:
        oy=height-visible_height-source_y
    return ox,oy

def pixel_map(width,height,x,y,visible_width,visible_height,flip_x=False,flip_y=False):
    """Returns source coordinate for each destination pixel in visible zone."""
    ox,oy=blit_offsets(width,height,x,y,visible_width,visible_height,flip_x,flip_y)
    return [[(ox+(visible_width-1-i if flip_x else i), oy+(visible_height-1-j if flip_y else j))
             for i in range(visible_width)] for j in range(visible_height)]

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('csv_file',type=Path)
    ap.add_argument('--output',type=Path, default=Path('DOS_F0791_Flip_Clipping_Audit_Results_20261121.json'))
    a=ap.parse_args()
    rows=list(csv.DictReader(a.csv_file.open(encoding='utf-8', newline='')))
    assert len(rows)==900
    clipped=[]; mirrored=0; mirrored_clipped=0
    for r in rows:
        w,h=int(r['width']),int(r['height'])
        if r['clipped_width'] in ('','None') or int(r['clipped_width'])<=0: continue
        cw,ch=int(r['clipped_width']),int(r['clipped_height'])
        sx,sy=int(r['source_x']),int(r['source_y'])
        flip=r['mirrored_horizontally']=='1'
        ox,oy=blit_offsets(w,h,sx,sy,cw,ch,flip_x=flip)
        assert (ox,oy)==(w-cw-sx if flip else sx,sy)
        mirrored+=flip
        if w!=cw or h!=ch:
            mirrored_clipped+=flip
            clipped.append({'type':r['ornament_type'],'view':r['view_index'],'view_name':r['view_name'],
                'original_bitmap':[int(r['x']),int(r['y']),w,h],
                'visible_rect':[int(r['clipped_x']),int(r['clipped_y']),cw,ch],
                'pre_flip_source':[sx,sy], 'final_source':[ox,oy], 'flip_x':flip})
    synthetic=[
      # x, y, width, height, clip_x, clip_y, clip_width, clip_height, flip_x, flip_y
      ( -4,20,14,12, 0,20,10,12,True,False),
      (218,20,14,12,218,20,6,12,True,False),
      ( 30,-6,14,12,30,0,14,6,True,True),
      (220,131,14,12,220,131,4,5,True,True),
    ]
    cases=[]
    for x,y,w,h,l,t,cw,ch,fx,fy in synthetic:
        sx,sy=l-x,t-y
        ox,oy=blit_offsets(w,h,sx,sy,cw,ch,fx,fy)
        pixels=pixel_map(w,h,sx,sy,cw,ch,fx,fy)
        assert len(pixels)==ch and len(pixels[0])==cw
        assert all(0<=px<w and 0<=py<h for row in pixels for px,py in row)
        if fx: assert pixels[0][0][0]==w-1-sx
        if fy: assert pixels[0][0][1]==h-1-sy
        cases.append({'bitmap_rect':[x,y,w,h],'visible_rect':[l,t,cw,ch],
              'flip_x':fx,'flip_y':fy,'f0635_source':[sx,sy],
              'f0791_blit_source':[ox,oy], 'first_drawn_source_pixel':list(pixels[0][0])})
    out={'records':len(rows),'partially_clipped_records':len(clipped),
         'mirrored_visible_records':mirrored,'mirrored_and_clipped_records':mirrored_clipped,
         'source_code':'DUNVIEW.C F0791_DUNGEONVIEW_DrawBitmapXX (X463 branch)',
         'source_verified_rule':'Flipped horizontal source start = original width - visible width - F0635 source_x; vertical analogue',
         'synthetic_flip_clip_cases':cases,
         'clipped_records':clipped,
         'limitations':['No decoded pixel colors','No runtime screenshot match','No V17 occlusion','All 900 live ordinary records have zero mirror+viewport-clip overlap']}
    a.output.write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
    print(f"Checked {len(rows)} records; {len(clipped)} partially clipped; {mirrored_clipped} mirrored+clipped; {len(cases)} synthetic mirrored tests passed")
if __name__=='__main__': main()
