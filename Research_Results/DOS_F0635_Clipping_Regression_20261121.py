#!/usr/bin/env python3
"""Independent F0635 X720 normal 0..8 bitmap zone clipping regression.
Reads the previous geometry CSV; checks the actual 900 rectangles against source-derived
alignment and 224x136 bounds, then exercises clipping with synthetic bitmap rectangles.
No Unity or DOS game files are modified.
"""
import argparse,csv,json
from pathlib import Path

ALIGN={0:("mid","mid"),1:("start","start"),2:("end","start"),3:("end","end"),4:("start","end"),5:("mid","start"),6:("end","mid"),7:("mid","end"),8:("start","mid")}
def anchor(a,d):
    if a=='start':return 0
    if a=='mid':return -((d+1)>>1)
    if a=='end':return -(d-1)
    raise ValueError(a)

def source_compatible_clip(x,y,w,h,clip=(0,0,224,136)):
    """Replicate X720 F0635 final clipping formula for a normal unshifted bitmap.
    Rect expressed as left,top,width,height; source X/Y returned as left/top trim.
    """
    l,t,cw,ch=clip
    dx=l-x;dy=t-y
    if dx<=0:
        sx=0; outx=x; outw=min(w,cw+dx)
    else:
        sx=dx;outx=l;outw=min(w-dx,cw)
    if dy<=0:
        sy=0;outy=y;outh=min(h,ch+dy)
    else:
        sy=dy;outy=t;outh=min(h-dy,ch)
    if outw<=0 or outh<=0:
        return None
    return (outx,outy,outw,outh,sx,sy)

def intersection_clip(x,y,w,h,clip=(0,0,224,136)):
    l,t,cw,ch=clip
    a=max(l,x);b=max(t,y);r=min(l+cw,x+w);d=min(t+ch,y+h)
    if r<=a or d<=b:return None
    return (a,b,r-a,d-b,a-x,b-y)

def main():
    p=argparse.ArgumentParser()
    p.add_argument('geometry_csv',type=Path)
    p.add_argument('--json',type=Path,default=Path('DOS_F0635_Clipping_Regression_Results_20261121.json'))
    args=p.parse_args()
    rows=list(csv.DictReader(args.geometry_csv.open(newline='',encoding='utf-8')))
    if len(rows)!=900:raise AssertionError(f'Expected 900 rows; found {len(rows)}')
    n_clipped=0
    kinds={}
    for r in rows:
        x,y,w,h=map(lambda name:int(r[name]),('x','y','width','height'))
        assert w>0 and h>0
        a=source_compatible_clip(x,y,w,h)
        b=intersection_clip(x,y,w,h)
        assert a==b,(r,a,b)
        actual=tuple(int(r[k]) for k in ('clipped_x','clipped_y','clipped_width','clipped_height','source_x','source_y')) if r['clipped_x'] not in ('','None') else None
        assert a==actual,(r,a,actual)
        if a and (a[0]!=x or a[1]!=y or a[2]!=w or a[3]!=h):n_clipped+=1
    tests=[(-4,20,14,12), (218,20,14,12), (30,-6,14,12), (30,131,14,12),
           (-5,-7,14,12), (220,131,14,12), (-20,20,14,12), (224,20,14,12),
           (20,-30,14,12), (20,136,14,12), (0,0,224,136), (0,0,225,137)]
    results=[]
    for x,y,w,h in tests:
        a=source_compatible_clip(x,y,w,h);b=intersection_clip(x,y,w,h)
        assert a==b,(x,y,w,h,a,b)
        results.append(dict(input=[x,y,w,h],result=list(a) if a else None))
    # all 9 alignment modes, cross-check center/ends using width parity edge cases
    for mode,(ax,ay) in ALIGN.items():
        for w in (1,2,7,8,14):
            for h in (1,2,5,6,12):
                x=112+anchor(ax,w);y=50+anchor(ay,h)
                assert source_compatible_clip(x,y,w,h)==intersection_clip(x,y,w,h)
    output={'records_checked':len(rows),'nontrivial_viewport_clips':n_clipped,
            'artificial_clipping_cases':len(tests),'alignment_cases':9*5*5,
            'source_path':'COORD.C F0635_ X720_I34E_I34M_P31J final clip branch',
            'synthetic_results':results,
            'limit':['no layered V17 occlusion','no palette/bitmap pixel rendering','does not port F0635 special record 10..18 or shift flag']}
    args.json.write_text(json.dumps(output,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in output.items() if k!='synthetic_results'},indent=2))
if __name__=='__main__':main()
