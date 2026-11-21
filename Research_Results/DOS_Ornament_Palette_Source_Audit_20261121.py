#!/usr/bin/env python3
"""Read-only verification of DOS wall ornament perspective palette tables.
Usage: python DOS_Ornament_Palette_Source_Audit_20261121.py DUNVIEW.C
Does not infer palette indices from RGB values or modify project data.
"""
import re, json, sys
from pathlib import Path
if len(sys.argv)!=2:
    raise SystemExit('Usage: python DOS_Ornament_Palette_Source_Audit_20261121.py DUNVIEW.C')
source=Path(sys.argv[1]).read_text(encoding='utf-8-sig',errors='replace')
keys={'D2':'G0199_auc_Graphic558_PaletteChanges_DoorButtonAndWallOrnament_D2',
      'D3':'G0198_auc_Graphic558_PaletteChanges_DoorButtonAndWallOrnament_D3'}
result={}
for branch in ('X417_P20JA_P20JB','X379_I34E_I34M_P31J'):
    marker=f'#ifdef {branch}'
    at=source.find(marker)
    if at<0: raise ValueError('Missing branch '+branch)
    segment=source[at:at+1300]
    result[branch]={}
    for distance,name in keys.items():
        m=re.search(re.escape(name)+r'\[16\]\s*=\s*\{([^}]+)\}',segment)
        if not m:raise ValueError(f'{branch} missing {distance} table')
        vals=[int(x) for x in re.findall(r'\d+',m.group(1))]
        if len(vals)!=16:raise ValueError('Wrong LUT length')
        result[branch][distance]=vals
    expected_transparent = 100 if branch == 'X417_P20JA_P20JB' else 10
    if result[branch]['D2'][10]!=expected_transparent or result[branch]['D3'][10]!=expected_transparent:
        raise ValueError('Transparent index 10 was not preserved')
if 'F0129_VIDEO_BlitShrinkWithPaletteChanges(L0125_puc_Bitmap_Native' not in source:
    raise ValueError('Source lacks expected F0129 call')
for name in keys.values():
    if source.count(name)<2:raise ValueError('Expected table to be used in draw path: '+name)
print(json.dumps({'source':str(Path(sys.argv[1]).name), 'tables':result,
                  'transparent_index_remains_10':True,
                  'status':'source tables verified; original F0129 sampling/decoded pixel indices not verified'},indent=2))
