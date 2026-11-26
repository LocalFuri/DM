"""Read-only source extraction. Writes a separate JSON report; no palette remapping."""
import argparse, collections, hashlib, json, zipfile
from pathlib import Path
from datetime import datetime

def main():
    p=argparse.ArgumentParser(); p.add_argument('source',type=Path); p.add_argument('--output',type=Path,required=True); a=p.parse_args()
    if zipfile.is_zipfile(a.source):
        with zipfile.ZipFile(a.source) as z:
            names=[n for n in z.namelist() if Path(n).name.lower() in ('graphics_dos.dat','graphics.dat')]
            if len(names)!=1: raise ValueError(f'Expected exactly one DOS archive member: {names}')
            data=z.read(names[0])
    else: data=a.source.read_bytes()
    if data[:2] != bytes.fromhex('01 80') or int.from_bytes(data[2:4], 'little') != 713:
        raise ValueError(f'Unexpected DOS archive header: {data[:4].hex(" " )}; expected 01 80 c9 02')
    from dmcsb import open_graphics
    g=open_graphics(data=data)
    report={'timestamp':datetime.now().astimezone().isoformat(),'source':str(a.source),'sha256':hashlib.sha256(data).hexdigest(),'notes':'Original indices returned by decoder; no transparency or RGB conversion performed by this script. Decoder-internal transparency handling and DOS runtime semantics not verified.','graphics':{}}
    for i in range(433,439):
        w,h,ind=g.image_indexed(i); pixels=bytes(ind)
        if len(pixels)!=w*h or any(v>15 for v in pixels): raise ValueError(f'Invalid index data for {i}')
        rows=[list(pixels[y*w:(y+1)*w]) for y in range(h)]
        report['graphics'][str(i)]={'width':w,'height':h,'histogram':dict(sorted(collections.Counter(pixels).items())),'rows':rows}
        print(f'{i}: {w}x{h}: {report["graphics"][str(i)]["histogram"]}')
        if i==437:
            for row in rows: print(' '.join(f'{v:X}' for v in row))
    a.output.parent.mkdir(parents=True,exist_ok=True)
    with a.output.open('x',encoding='utf-8') as f: json.dump(report,f,indent=2)
    print('Saved:',a.output)
if __name__=='__main__': main()
