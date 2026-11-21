"""Render targeted native graphics from Dungeon Master PC GRAPHICS.DAT.

Requirements on the computer running this script:
    python -m pip install 'dmcsb==0.2.1' Pillow
Usage:
    python DOS_Ornament_Pixel_Identity_Check_20261121.py GRAPHICS_DOS.ZIP
    python DOS_Ornament_Pixel_Identity_Check_20261121.py GRAPHICS.DAT
Writes a contact sheet and JSON metadata beside the input file. Does not alter source data.
Check visual graphic identity; dimensions alone cannot prove ornament names.
"""
from pathlib import Path
import json, struct, sys, zipfile

INDICES = (267,268,283,284,303,304,331,332)

def load_bytes(p):
    if zipfile.is_zipfile(p):
        with zipfile.ZipFile(p) as z:
            found=[n for n in z.namelist() if n.upper().endswith('GRAPHICS.DAT')]
            if len(found)!=1: raise ValueError('Archive must contain exactly one GRAPHICS.DAT')
            return z.read(found[0])
    return p.read_bytes()

def metadata(data):
    if len(data)<8: raise ValueError('Graphics file too short')
    version,n=struct.unpack_from('<HH',data)
    if version != 0x8001 or n <= max(INDICES):
        raise ValueError(f'Expected PC little-endian version 0x8001 and >= {max(INDICES)+1} graphics; got {version:#06x}, {n}')
    result={}
    for i in INDICES:
        w,h=struct.unpack_from('<HH',data,4+4*n+4*i)
        result[i]={'width':w,'height':h,'pair_type':(i-259)//2,
                   'view':'front' if i%2==0 else 'side'}
    return result

def main():
    if len(sys.argv)!=2: raise SystemExit('Usage: python DOS_Ornament_Pixel_Identity_Check_20261121.py GRAPHICS_DOS.ZIP')
    source=Path(sys.argv[1]).resolve()
    data=load_bytes(source)
    info=metadata(data)
    try:
        from dmcsb import open_graphics
        from PIL import Image, ImageOps, ImageDraw
    except ImportError as exc:
        raise SystemExit("Dependency missing: run python -m pip install 'dmcsb==0.2.1' Pillow") from exc
    graphics=open_graphics(data=data)
    tile_w,tile_h=270,240
    canvas=Image.new('RGB',(4*tile_w,2*tile_h),'#222222')
    draw=ImageDraw.Draw(canvas)
    for j,i in enumerate(INDICES):
        image=graphics.to_pil(i)
        if image is None: raise RuntimeError(f'No decoded bitmap for index {i}')
        native=info[i]
        if tuple(image.size)!=(native['width'],native['height']):
            raise RuntimeError(f'Graphic {i}: decoded pixels {image.size} != DAT header {native}')
        # show transparent pixels over a checker-free neutral background, preserving visible palette
        image=image.convert('RGBA')
        image.thumbnail((220,185),Image.Resampling.NEAREST)
        scale=max(1,min(4,220//image.width,185//image.height))
        image=image.resize((image.width*scale,image.height*scale),Image.Resampling.NEAREST)
        bg=Image.new('RGBA',image.size,(45,45,45,255))
        bg.alpha_composite(image)
        x=(j%4)*tile_w; y=(j//4)*tile_h
        canvas.paste(bg.convert('RGB'),(x+(tile_w-bg.width)//2,y+32+(185-bg.height)//2))
        draw.text((x+12,y+8),f'Graphic {i} / type {native["pair_type"]} / {native["width"]}x{native["height"]}',fill='white')
    sheet=source.parent/'DOS_Ornament_Candidate_Contact_Sheet_20261121.png'
    catalog=source.parent/'DOS_Ornament_Candidate_Metadata_20261121.json'
    canvas.save(sheet)
    catalog.write_text(json.dumps(info,indent=2)+'\n',encoding='utf-8')
    print('Contact sheet:',sheet)
    print('Metadata:',catalog)
    print('Compare the actual pixel shapes with the Hook and Manacles reference artwork.')
    print('Do NOT identify ornaments by dimensions alone.')

if __name__=='__main__': main()
