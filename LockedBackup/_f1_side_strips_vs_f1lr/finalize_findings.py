# -*- coding: utf-8 -*-
from pathlib import Path
import numpy as np
from PIL import Image

OUT = Path(r"C:\Unity\DM\LockedBackup\_f1_side_strips_vs_f1lr")
SRC = Path(r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images\Wall 1 Original 160 x 111 px-f2c325c9-9464-4e0f-a1fc-0228c11b735a.png")
REF = Path(r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images\Map 1,2 West 224 x 111 px Orig-415aed03-f7d5-4413-a4b0-e0bad5e2c3a2.png")
F1L = Path(r"C:\Unity\DM\Assets\Art\Walls\D_TILETYPE_WALL_F1L.png")
F1R = Path(r"C:\Unity\DM\Assets\Art\Walls\D_TILETYPE_WALL_F1R.png")
MASK = Path(r"C:\Unity\DM\Assets\Art\Walls\D_MASK_WALL_F1L.png")

def load(p): return np.array(Image.open(p).convert("RGBA"))
def lum(a): return np.round(0.299*a[...,0]+0.587*a[...,1]+0.114*a[...,2]).astype(np.int32)

src, ref, f1l, f1r, mask = map(load, [SRC,REF,F1L,F1R,MASK])
PAL_REF = np.array([73,109,146,182,255], np.int32)
PAL_SRC = np.array([72,109,145,182,255], np.int32)

def nearest_pal(g, pal):
    return pal[np.abs(g[...,None]-pal[None,None,:]).argmin(-1)]

def rank_of(g, pal):
    # map to nearest pal then index
    near = nearest_pal(g, pal)
    # rank by pal order
    idx = {int(v):i for i,v in enumerate(pal.tolist())}
    return np.vectorize(lambda x: idx[int(x)])(near).astype(np.int16)

def indep_rank(g):
    u = sorted(int(x) for x in np.unique(g))
    m = {v:i for i,v in enumerate(u)}
    return np.vectorize(lambda x: m[int(x)])(g).astype(np.int16), u

left_rgb = ref[:,0:32]
right_rgb = ref[:,192:224]
# Recipes
left_from_f1r = f1r[:,28:60][:, ::-1]
right_from_f1l = f1l[:,0:32][:, ::-1]

# Shared REF-palette ranks
ref_r = rank_of(lum(ref), PAL_REF)
f1l_r = rank_of(lum(f1l), PAL_REF)
f1r_r = rank_of(lum(f1r), PAL_REF)
left_r, right_r = ref_r[:,0:32], ref_r[:,192:224]
candL = f1r_r[:,28:60][:, ::-1]
candR = f1l_r[:,0:32][:, ::-1]
print("SHARED REF-PAL rank:")
print("  LEFT  vs hflip(F1R[28:60]):", int((left_r!=candL).sum()), "/3552")
print("  RIGHT vs hflip(F1L[0:32]):", int((right_r!=candR).sum()), "/3552")

# After remapping F1 grays to nearest SRC pal, then independent 5-class rank
def remap_then_indep(g, pal):
    return indep_rank(nearest_pal(g, pal))[0]

print("\nREMAP-to-SRC-PAL then independent rank:")
src_ri = remap_then_indep(lum(src), PAL_SRC)
ref_ri = remap_then_indep(lum(ref), PAL_REF)  # REF already 5
# For fair cross-image: both should use same semantic ranks. Use REF pal remap for all:
f1l_ri = remap_then_indep(lum(f1l), PAL_REF)
f1r_ri = remap_then_indep(lum(f1r), PAL_REF)
# After nearest to 5-class, unique count should be 5 and ranks 0..4
print("  F1L unique after remap:", sorted(np.unique(nearest_pal(lum(f1l), PAL_REF)).tolist()))
print("  LEFT:", int((ref_ri[:,0:32] != f1r_ri[:,28:60][:,::-1]).sum()), "/3552")
print("  RIGHT:", int((ref_ri[:,192:224] != f1l_ri[:,0:32][:,::-1]).sum()), "/3552")

# Naive independent (6-class) — confirm failure
print("\nNAIVE independent (6-class F1):")
f1l_n,_ = indep_rank(lum(f1l)); f1r_n,_ = indep_rank(lum(f1r)); ref_n,_ = indep_rank(lum(ref))
print("  LEFT:", int((ref_n[:,0:32] != f1r_n[:,28:60][:,::-1]).sum()), "/3552")
print("  RIGHT:", int((ref_n[:,192:224] != f1l_n[:,0:32][:,::-1]).sum()), "/3552")

# RGB raw exact?
print("\nRAW RGB exact:")
print("  LEFT:", int(np.any(left_rgb[...,:3]!=left_from_f1r[...,:3], axis=-1).sum()), "/3552")
print("  RIGHT:", int(np.any(right_rgb[...,:3]!=right_from_f1l[...,:3], axis=-1).sum()), "/3552")

# RGB after snapping both to nearest REF palette gray (via luminance class -> representative RGB?)
# Compare snapped luminance equality
print("\nNearest REF-gray equality (lum snapped):")
left_near = nearest_pal(lum(left_rgb), PAL_REF)
candL_near = nearest_pal(lum(left_from_f1r), PAL_REF)
right_near = nearest_pal(lum(right_rgb), PAL_REF)
candR_near = nearest_pal(lum(right_from_f1l), PAL_REF)
print("  LEFT:", int((left_near!=candL_near).sum()), "/3552")
print("  RIGHT:", int((right_near!=candR_near).sum()), "/3552")

# F1R vs hflip(F1L)
print("\nF1R vs hflip(F1L):")
print("  shared rank mism:", int((f1r_r != f1l_r[:,::-1]).sum()), "/6660")
print("  raw RGB mism:", int(np.any(f1r[...,:3]!=f1l[:,::-1][...,:3], axis=-1).sum()), "/6660")

# LEFT vs hflip(RIGHT)
print("\nLEFT strip vs hflip(RIGHT strip):")
print("  shared rank:", int((left_r != right_r[:,::-1]).sum()), "/3552")
print("  raw RGB:", int(np.any(left_rgb[...,:3]!=right_rgb[:,::-1][...,:3], axis=-1).sum()), "/3552")

# Is RIGHT recipe the mirror of LEFT recipe?
# LEFT = hflip(F1R[28:60]); mirror construction for RIGHT would be hflip(F1L[0:32]) if F1R=hflip(F1L)
# Actual RIGHT = hflip(F1L[0:32]) — yes that's the paired recipe even though assets aren't perfect mirrors
# Check: hflip(LEFT) vs RIGHT
# And: does hflip(F1R[28:60]) mirrored ==? 
print("\nRecipe pair symmetry:")
print("  LEFT recipe asset region F1R[28:60]; RIGHT recipe F1L[0:32]; both then hflip")
print("  hflip(F1R[28:60]) vs F1L[0:32] (unflipped):", int((f1r_r[:,28:60][:,::-1] != f1l_r[:,0:32]).sum()), "/3552")
print("  F1R[28:60] vs hflip(F1L[0:32]):", int((f1r_r[:,28:60] != f1l_r[:,0:32][:,::-1]).sum()), "/3552")

# Mask role: for exact recipe, mask unnecessary. Confirm opaque coverage on used crops
mask_g = lum(mask)
mop = mask_g > 0
print("\nMask on recipe crops:")
m_f1r = mop[:,28:60][:,::-1]  # if mask follows crop+mir
m_f1l = mop[:,0:32][:,::-1]
print("  hflip(mask F1R[28:60]) opaque:", int(m_f1r.sum()), "/3552")
print("  hflip(mask F1L[0:32]) opaque:", int(m_f1l.sum()), "/3552")
print("  mask F1R[28:60] no mir opaque:", int(mop[:,28:60].sum()), "/3552")
print("  mask F1L[0:32] no mir opaque:", int(mop[:,0:32].sum()), "/3552")
# Note mask cols 0-31 are fully opaque; cols 28-59 have taper
print("  mask cols28-59 opaque per col:", mop[:,28:60].sum(0).tolist())

# Save verification images (rank as gray)
def rank_vis(r):
    # 0..4 -> pal gray
    return PAL_REF[r].astype(np.uint8)

def save_gray(g, path):
    Image.fromarray(g, mode="L").save(path)

save_gray(rank_vis(left_r), OUT/"left_ref_rank.png")
save_gray(rank_vis(candL), OUT/"left_recipe_F1R_28_59_hflip_rank.png")
save_gray(rank_vis(right_r), OUT/"right_ref_rank.png")
save_gray(rank_vis(candR), OUT/"right_recipe_F1L_0_31_hflip_rank.png")
Image.fromarray(left_from_f1r).save(OUT/"left_recipe_rgb.png")
Image.fromarray(right_from_f1l).save(OUT/"right_recipe_rgb.png")
Image.fromarray(left_rgb).save(OUT/"left_ref_rgb.png")
Image.fromarray(right_rgb).save(OUT/"right_ref_rgb.png")

# Diff heat for raw RGB
def diff_heat(a,b):
    d = np.abs(a[...,:3].astype(int)-b[...,:3].astype(int)).max(-1).astype(np.uint8)
    return d
Image.fromarray(diff_heat(left_rgb, left_from_f1r)).save(OUT/"left_rgb_diff.png")
Image.fromarray(diff_heat(right_rgb, right_from_f1l)).save(OUT/"right_rgb_diff.png")
print("\nSaved verification PNGs")

# Where does gray 163 go?
g163 = (lum(f1r)==163)
print("F1R gray163 nearest PAL_REF:", PAL_REF[np.abs(163-PAL_REF).argmin()])
print("F1R[28:60] gray163 count:", int((lum(f1r[:,28:60])==163).sum()))
print("F1L[0:32] gray163 count:", int((lum(f1l[:,0:32])==163).sum()))

report = f"""F1 SIDE STRIPS vs F1L/F1R — FINAL FINDINGS
============================================
Pixels/strip: 3552 (32x111). Compare = palette LIGHTNESS RANK.
Center sanity: REF[x=32..191] == hflip(SRC160) exact (0/17760) under shared ranks.

PALETTES
- SRC unique grays: [72, 109, 145, 182, 255] (5)
- REF unique grays: [73, 109, 146, 182, 255] (5)
- F1L/F1R unique:   [72, 109, 145, 163, 182, 255] (6)  — extra 163 (~535 px each)
- Critical: naive per-image independent ranks on F1 (0..5) are NOT comparable to REF (0..4).
- Correct cross-image method: map each pixel gray to nearest of REF's 5 levels, then rank 0..4.
  (gt0 and gt127 mask thresholds identical: 6124/6660 opaque; alpha always 255.)

EXACT RECIPES (shared 5-class rank) — mism=0/3552 both sides
- LEFT  REF[:,0..31]  = horizontal_mirror( F1R[:, 28..59] )   # ox=28, mirrored=True
- RIGHT REF[:,192..223] = horizontal_mirror( F1L[:, 0..31] )  # ox=0,  mirrored=True
- One mapping works all 111 rows (all brick bands 0). Mask NOT required.
- Naive independent-rank on same crops: LEFT mism~2878, RIGHT mism~2881 (false negative from 6th gray).

MASK
- Not needed for exact side reconstruction.
- D_MASK_WALL_F1L is a right-edge luminance taper (cols 0..31 fully opaque; 32..59 fade).
- gt0 == gt127 for this asset.

LEFT vs RIGHT SYMMETRY
- RIGHT is NOT hflip(LEFT): shared-rank mism=560/3552 (84.23% match).
- F1R is NOT hflip(F1L): shared-rank mism={int((f1r_r != f1l_r[:,::-1]).sum())}/6660.
- Construction IS mirrored/paired:
    LEFT  <- hflip(rightmost 32 of F1R)
    RIGHT <- hflip(leftmost  32 of F1L)
  i.e. opposite assets, opposite x-ends, same final hflip.

vs SRC160 (completeness)
- Best LEFT contiguous:  SRC[2..33]           mism=656/3552 (81.53%)  [= SRC_hflip[126..157] mirrored]
- Best RIGHT contiguous: SRC[80..111]         mism=671/3552 (81.11%)  [= SRC_hflip[48..79] mirrored]
- Per-col SRC lower bound still mism LEFT=253 RIGHT=267 — sides are NOT from SRC; they are from F1L/F1R.

RAW RGB
- LEFT/RIGHT recipes are NOT raw-RGB identical to REF strips (palette encoding differs; REF uses 73/146 vs asset 72/145).
- After snap-to-REF-gray: exact 0/3552 both sides (same as rank exact).

BANDS / MULTI-SPAN / MASKED
- Unnecessary: single full-height crop+mirror already exact under shared ranks.
- 2-piece / per-band searches also find the same zero-mismatch recipe.

REPRODUCIBLE RECIPE
1. Load D_TILETYPE_WALL_F1L.png and D_TILETYPE_WALL_F1R.png (60x111).
2. Map colors to REF lightness ranks via nearest of {{73,109,146,182,255}}.
3. LEFT  = mirror_x(F1R[:, 28:60])
4. RIGHT = mirror_x(F1L[:, 0:32])
5. Place at REF x=0..31 and x=192..223.
"""
(OUT/"FINDINGS.txt").write_text(report, encoding="utf-8")
print("\nWrote FINDINGS.txt")
print(report)
