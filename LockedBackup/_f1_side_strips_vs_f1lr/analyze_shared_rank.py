# -*- coding: utf-8 -*-
"""Follow-up: shared 5-class palette ranks + column bounds + F1-only 2piece + recipe symmetry."""
from pathlib import Path
import json
import numpy as np
from PIL import Image

OUT = Path(r"C:\Unity\DM\LockedBackup\_f1_side_strips_vs_f1lr")
SRC160_PATH = Path(r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images\Wall 1 Original 160 x 111 px-f2c325c9-9464-4e0f-a1fc-0228c11b735a.png")
REF224_PATH = Path(r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images\Map 1,2 West 224 x 111 px Orig-415aed03-f7d5-4413-a4b0-e0bad5e2c3a2.png")
F1L_PATH = Path(r"C:\Unity\DM\Assets\Art\Walls\D_TILETYPE_WALL_F1L.png")
F1R_PATH = Path(r"C:\Unity\DM\Assets\Art\Walls\D_TILETYPE_WALL_F1R.png")
MASK_PATH = Path(r"C:\Unity\DM\Assets\Art\Walls\D_MASK_WALL_F1L.png")

BANDS = [(0, 27), (28, 54), (55, 82), (83, 110)]
W, H, TOTAL = 32, 111, 32 * 111

def load(p):
    return np.array(Image.open(p).convert("RGBA"))

def lum(a):
    return np.round(0.299*a[...,0] + 0.587*a[...,1] + 0.114*a[...,2]).astype(np.int32)

src, ref, f1l, f1r, mask = map(load, [SRC160_PATH, REF224_PATH, F1L_PATH, F1R_PATH, MASK_PATH])
src_g, ref_g, f1l_g, f1r_g = map(lum, [src, ref, f1l, f1r])
mask_g = lum(mask)

# Shared palette = REF unique grays (5)
PAL = np.array(sorted(np.unique(ref_g).tolist()), dtype=np.int32)
print("Shared PAL (REF):", PAL.tolist())
print("SRC unique:", sorted(np.unique(src_g).tolist()))
print("F1L unique:", sorted(np.unique(f1l_g).tolist()))
print("F1R unique:", sorted(np.unique(f1r_g).tolist()))

# Count F1 extra gray 163
for name, g in (("F1L", f1l_g), ("F1R", f1r_g)):
    extra = (g == 163).sum()
    print(f"  {name} pixels gray=163: {extra}/{g.size}")

def to_shared_rank(g):
    # nearest PAL value -> rank index
    # |g - pal| argmin
    diff = np.abs(g[..., None].astype(np.int32) - PAL[None, None, :])
    idx = diff.argmin(axis=-1).astype(np.int16)
    return idx

src_r = to_shared_rank(src_g)
ref_r = to_shared_rank(ref_g)
f1l_r = to_shared_rank(f1l_g)
f1r_r = to_shared_rank(f1r_g)
src_fr = src_r[:, ::-1].copy()

left = ref_r[:, 0:32]
right = ref_r[:, 192:224]

# Sanity: independent REF rank vs shared should be identical for REF
ref_ind = {v:i for i,v in enumerate(sorted(np.unique(ref_g).tolist()))}
ref_ind_map = np.vectorize(lambda x: ref_ind[int(x)])(ref_g).astype(np.int16)
print("REF independent vs shared identical:", np.array_equal(ref_ind_map, ref_r))
print("Center vs hflip SRC shared:", int((ref_r[:,32:192] != src_fr).sum()), "/17760")

def match(a,b,valid=None):
    if valid is None:
        valid = np.ones(a.shape, bool)
    n = int(valid.sum())
    mism = int(((a!=b)&valid).sum())
    return 100.0*(n-mism)/n if n else 0.0, mism, n

def search_crops(target, assets):
    best = []
    for aname, ar in assets.items():
        aw = ar.shape[1]
        for ox in range(0, aw-W+1):
            crop = ar[:, ox:ox+W]
            for mir in (False, True):
                cand = crop[:, ::-1] if mir else crop
                pct, mism, n = match(target, cand)
                best.append(dict(asset=aname, ox=ox, x_range=[ox,ox+31], mir=mir, mism=mism, pct=pct, n=n))
    best.sort(key=lambda r: r["mism"])
    return best

assets = {"F1L": f1l_r, "F1R": f1r_r, "SRC": src_r, "SRC_hflip": src_fr}

print("\n=== SHARED-RANK full strip ===")
for side, tgt in (("LEFT", left), ("RIGHT", right)):
    tops = search_crops(tgt, assets)
    print(f"\n{side} top 8:")
    for i,r in enumerate(tops[:8]):
        print(f"  #{i+1} mism={r['mism']}/3552 ({r['pct']:.4f}%) {r['asset']} x={r['x_range']} mir={r['mir']}")

# Masked with shared ranks
mask_op = mask_g > 0  # == gt127
print("\n=== SHARED-RANK masked ===")
for side, tgt in (("LEFT", left), ("RIGHT", right)):
    best_ign = None
    best_fill = None
    for aname, ar in (("F1L", f1l_r), ("F1R", f1r_r)):
        for ox in range(0, 60-W+1):
            for mask_flip in (False, True):
                m60 = mask_op[:, ::-1] if mask_flip else mask_op
                m32 = m60[:, ox:ox+W]
                crop = ar[:, ox:ox+W]
                for mir in (False, True):
                    cand = crop[:, ::-1] if mir else crop
                    for follow in (False, True):
                        me = m32[:, ::-1] if (mir and follow) else m32
                        pct, mism, n = match(tgt, cand, me)
                        rec = dict(asset=aname, ox=ox, mir=mir, mask_flip=mask_flip, follow=follow, mism=mism, n=n, pct=pct)
                        if best_ign is None or (mism, -n) < (best_ign["mism"], -best_ign["n"]):
                            best_ign = rec
                        # fill from SRC / SRC_hflip
                        for ftag, fr in (("SRC", src_r), ("SRC_hflip", src_fr)):
                            for fox in range(0, fr.shape[1]-W+1):
                                for fmir in (False, True):
                                    fill = fr[:, fox:fox+W]
                                    if fmir: fill = fill[:, ::-1]
                                    cand2 = np.where(me, cand, fill)
                                    pct2, mism2, n2 = match(tgt, cand2)
                                    rec2 = dict(asset=aname, ox=ox, mir=mir, mask_flip=mask_flip, follow=follow,
                                                fill=f"{ftag}@{fox}mir{fmir}", mism=mism2, n=n2, pct=pct2)
                                    if best_fill is None or mism2 < best_fill["mism"]:
                                        best_fill = rec2
    print(f"{side} best ignore: {best_ign}")
    print(f"{side} best fill:   {best_fill}")

# F1-only 2-piece (no SRC)
print("\n=== F1-only 2-piece (shared rank) ===")
def two_piece(target, asset_dict):
    pieces = []
    for aname, ar in asset_dict.items():
        for ox in range(0, ar.shape[1]-W+1):
            crop = ar[:, ox:ox+W]
            for mir in (False, True):
                cand = crop[:, ::-1] if mir else crop
                col = (cand != target).sum(axis=0)
                pieces.append((aname, ox, mir, col, int(col.sum())))
    prefs = [np.cumsum(p[3]) for p in pieces]
    best = {"mism": 10**9}
    for s in range(1, W):
        Lcosts = [int(prefs[i][s-1]) for i in range(len(pieces))]
        Rcosts = [pieces[i][4]-Lcosts[i] for i in range(len(pieces))]
        iL, iR = int(np.argmin(Lcosts)), int(np.argmin(Rcosts))
        mism = Lcosts[iL]+Rcosts[iR]
        if mism < best["mism"]:
            best = {"split":s, "mism":mism, "L":{"asset":pieces[iL][0],"ox":pieces[iL][1],"mir":pieces[iL][2],"cost":Lcosts[iL]},
                    "R":{"asset":pieces[iR][0],"ox":pieces[iR][1],"mir":pieces[iR][2],"cost":Rcosts[iR]}}
    return best

f1_only = {"F1L": f1l_r, "F1R": f1r_r}
print("LEFT F1-only 2p:", two_piece(left, f1_only))
print("RIGHT F1-only 2p:", two_piece(right, f1_only))
print("LEFT all 2p:", two_piece(left, assets))
print("RIGHT all 2p:", two_piece(right, assets))

# Per-column best from F1L/F1R (any single column from asset, with optional mirror of whole? per-col from assets)
print("\n=== Per-column lower bound from F1 assets ===")
for side, tgt in (("LEFT", left), ("RIGHT", right)):
    # For each dest col, find best matching source col from F1L or F1R (any x)
    col_best = []
    for dx in range(W):
        tcol = tgt[:, dx]
        best_m = H+1
        best_src = None
        for aname, ar in (("F1L", f1l_r), ("F1R", f1r_r)):
            for sx in range(ar.shape[1]):
                mism = int((tcol != ar[:, sx]).sum())
                if mism < best_m:
                    best_m = mism
                    best_src = (aname, sx, False)
                # mirrored asset column = original at (w-1-sx) of mirrored image = column from hflip
                # equivalent: compare to ar[:, sx] already; hflip asset cols are just reindexed
        col_best.append((best_m, best_src))
    total = sum(c[0] for c in col_best)
    print(f"{side} per-col independent F1 lower bound mism={total}/3552; per-col mism={ [c[0] for c in col_best] }")
    print(f"  sources: { [c[1] for c in col_best] }")

# Per-column from SRC
for side, tgt in (("LEFT", left), ("RIGHT", right)):
    col_best = []
    for dx in range(W):
        tcol = tgt[:, dx]
        best_m, best_src = H+1, None
        for aname, ar in (("SRC", src_r), ("SRC_hflip", src_fr)):
            for sx in range(ar.shape[1]):
                mism = int((tcol != ar[:, sx]).sum())
                if mism < best_m:
                    best_m = mism
                    best_src = (aname, sx)
        col_best.append((best_m, best_src))
    total = sum(c[0] for c in col_best)
    print(f"{side} per-col SRC lower bound mism={total}/3552; zero-cols={sum(1 for c in col_best if c[0]==0)}")
    print(f"  per-col mism={ [c[0] for c in col_best] }")
    print(f"  sources={ [c[1] for c in col_best] }")

# Band best F1-only shared
print("\n=== Per-band best F1-only (shared) ===")
for side, tgt in (("LEFT", left), ("RIGHT", right)):
    tot = 0
    print(side)
    for y0,y1 in BANDS:
        best = None
        for aname, ar in (("F1L", f1l_r), ("F1R", f1r_r)):
            for ox in range(0, 60-W+1):
                crop = ar[y0:y1+1, ox:ox+W]
                for mir in (False, True):
                    cand = crop[:, ::-1] if mir else crop
                    mism = int((tgt[y0:y1+1] != cand).sum())
                    n = W*(y1-y0+1)
                    if best is None or mism < best["mism"]:
                        best = dict(asset=aname, ox=ox, mir=mir, mism=mism, n=n)
        tot += best["mism"]
        print(f"  y={y0}-{y1}: {best}")
    print(f"  combined F1-only bands mism={tot}/3552")

# Recipe symmetry: take LEFT best SRC recipe and see if RIGHT best is mirror construction
print("\n=== Construction symmetry check ===")
# LEFT best: SRC[2:33] mir=False  OR SRC_hflip[126:157] mir=True
# If RIGHT were mirror of LEFT construction: RIGHT should equal hflip(LEFT_recipe_pixels)
# We already know RIGHT vs hflip(LEFT) mism=560
# Check if RIGHT best SRC[80:111] is geometric mirror of LEFT's SRC[2:33]:
# Mirror of SRC cols 2..33 would be SRC cols (159-33)..(159-2) = 126..157, then placed mirrored into RIGHT
# That would mean RIGHT ~= hflip(SRC[2:33]) = SRC_hflip[126:157] with mir=False? 
# hflip(SRC[2:33]) = SRC[33:1:-1] = SRC[33..2] = SRC_flip[(159-33):(159-2)+1] wait
left_recipe = src_r[:, 2:34]  # 32 cols
right_as_mirror_of_left_recipe = left_recipe[:, ::-1]
pct, mism, n = match(right, right_as_mirror_of_left_recipe)
print(f"RIGHT vs hflip(LEFT's best SRC[2:34] recipe): mism={mism}/3552 ({pct:.4f}%)")

right_recipe = src_r[:, 80:112]
left_as_mirror_of_right_recipe = right_recipe[:, ::-1]
pct, mism, n = match(left, left_as_mirror_of_right_recipe)
print(f"LEFT vs hflip(RIGHT's best SRC[80:112] recipe): mism={mism}/3552 ({pct:.4f}%)")

# Direct: is RIGHT best equal to mirror of LEFT best as images?
pct, mism, n = match(right, left[:, ::-1])
print(f"RIGHT vs hflip(LEFT strip) shared-rank: mism={mism}/3552 ({pct:.4f}%)")

# Compare F1L best crop recipe vs F1R
# LEFT best F1 was F1R[0:32] mir=False (independent ranks) — recheck shared
lf1 = search_crops(left, {"F1L": f1l_r, "F1R": f1r_r})[0]
rf1 = search_crops(right, {"F1L": f1l_r, "F1R": f1r_r})[0]
print(f"LEFT best F1 shared: {lf1}")
print(f"RIGHT best F1 shared: {rf1}")
# If recipes are mirrors: LEFT uses F1R[0:32] and RIGHT uses F1L[28:59] or F1R[0:32] mirrored etc.
# Check whether RIGHT best cand == hflip(LEFT best cand)
def cand(r, assets):
    ar = assets[r["asset"]]
    crop = ar[:, r["ox"]:r["ox"]+W]
    return crop[:, ::-1] if r["mir"] else crop
lc, rc = cand(lf1, {"F1L":f1l_r,"F1R":f1r_r}), cand(rf1, {"F1L":f1l_r,"F1R":f1r_r})
pct, mism, n = match(rc, lc[:, ::-1])
print(f"RIGHT_best_F1_cand vs hflip(LEFT_best_F1_cand): mism={mism}/3552 ({pct:.4f}%)")

# How many F1 pixels map to each shared rank after nearest
for name, r in (("F1L", f1l_r), ("F1R", f1r_r), ("REF_left", left), ("SRC", src_r)):
    print(f"  rank hist {name}:", {i:int((r==i).sum()) for i in range(5)})

# Exact column identity: any dest col exact match to any F1 col?
print("\n=== Exact column matches ===")
for side, tgt in (("LEFT", left), ("RIGHT", right)):
    exact = 0
    for dx in range(W):
        tcol = tgt[:, dx]
        hit = False
        for ar in (f1l_r, f1r_r, src_r, src_fr):
            for sx in range(ar.shape[1]):
                if np.array_equal(tcol, ar[:, sx]):
                    hit = True
                    break
            if hit: break
        if hit: exact += 1
    print(f"{side}: {exact}/32 dest cols have some exact source col in F1L/F1R/SRC/SRCflip")

# RGB exact (not rank) for completeness on best SRC recipes
print("\n=== RGB exact on best recipes ===")
left_rgb = ref[:, 0:32]
right_rgb = ref[:, 192:224]
# SRC[2:34]
cand = src[:, 2:34]
mism_rgb = int(np.any(left_rgb[...,:3] != cand[...,:3], axis=-1).sum())
print(f"LEFT RGB vs SRC[2:34]: mism={mism_rgb}/3552")
cand = src[:, 80:112]
mism_rgb = int(np.any(right_rgb[...,:3] != cand[...,:3], axis=-1).sum())
print(f"RIGHT RGB vs SRC[80:112]: mism={mism_rgb}/3552")

# Nearest gray remap note: SRC gray 72 vs REF 73 — ranks still align via nearest
src_pal = sorted(np.unique(src_g).tolist())
print("SRC pal", src_pal, "REF pal", PAL.tolist())
print("pairwise |SRC-REF|:", [abs(a-b) for a,b in zip(src_pal, PAL.tolist())])

# Write supplemental findings
supp = []
supp.append("SHARED 5-CLASS (nearest to REF palette) FOLLOW-UP")
supp.append(f"PAL={PAL.tolist()}")
supp.append(f"F1L/F1R extra gray 163 px: F1L={(f1l_g==163).sum()} F1R={(f1r_g==163).sum()}")
topsL = search_crops(left, assets)
topsR = search_crops(right, assets)
supp.append(f"LEFT best shared: {topsL[0]}")
supp.append(f"RIGHT best shared: {topsR[0]}")
supp.append(f"LEFT best F1: {lf1}")
supp.append(f"RIGHT best F1: {rf1}")
supp.append(f"LEFT F1-only 2p: {two_piece(left, f1_only)}")
supp.append(f"RIGHT F1-only 2p: {two_piece(right, f1_only)}")
(OUT/"FINDINGS_shared_rank.txt").write_text("\n".join(supp)+"\n", encoding="utf-8")
print("Wrote supplemental")
