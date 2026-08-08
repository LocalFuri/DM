# -*- coding: utf-8 -*-
"""F1 side strips vs F1L/F1R inspection — temp outputs only."""
from __future__ import annotations
import json
import os
from pathlib import Path
from collections import defaultdict
from PIL import Image
import numpy as np

OUT = Path(r"C:\Unity\DM\LockedBackup\_f1_side_strips_vs_f1lr")
OUT.mkdir(parents=True, exist_ok=True)

SRC160_PATH = Path(r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images\Wall 1 Original 160 x 111 px-f2c325c9-9464-4e0f-a1fc-0228c11b735a.png")
REF224_PATH = Path(r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images\Map 1,2 West 224 x 111 px Orig-415aed03-f7d5-4413-a4b0-e0bad5e2c3a2.png")
F1L_PATH = Path(r"C:\Unity\DM\Assets\Art\Walls\D_TILETYPE_WALL_F1L.png")
F1R_PATH = Path(r"C:\Unity\DM\Assets\Art\Walls\D_TILETYPE_WALL_F1R.png")
MASK_PATH = Path(r"C:\Unity\DM\Assets\Art\Walls\D_MASK_WALL_F1L.png")

BANDS = [(0, 27), (28, 54), (55, 82), (83, 110)]
W_STRIP = 32
H = 111
TOTAL = W_STRIP * H  # 3552


def load_rgba(p: Path) -> np.ndarray:
    im = Image.open(p).convert("RGBA")
    arr = np.array(im)
    print(f"  {p.name}: {arr.shape[1]}x{arr.shape[0]}")
    return arr


def luminance(rgb: np.ndarray) -> np.ndarray:
    # rgb HxWx3 or HxWx4
    r = rgb[..., 0].astype(np.float64)
    g = rgb[..., 1].astype(np.float64)
    b = rgb[..., 2].astype(np.float64)
    return 0.299 * r + 0.587 * g + 0.114 * b


def to_rank_map(rgba: np.ndarray) -> np.ndarray:
    """Map each pixel to palette lightness rank 0..n-1 by sorted unique gray values."""
    lum = luminance(rgba)
    # quantize to int for uniqueness of gray values as they appear
    gray = np.round(lum).astype(np.int32)
    uniq = np.unique(gray)
    # sort ascending = darker first -> rank 0 = darkest
    order = np.argsort(uniq)
    rank_of = {int(uniq[i]): int(np.where(order == i)[0][0]) for i in range(len(uniq))}
    # Actually: sorted unique ascending, rank by position in sorted list
    sorted_u = sorted(int(u) for u in uniq)
    rank_of = {v: i for i, v in enumerate(sorted_u)}
    ranks = np.vectorize(lambda g: rank_of[int(g)])(gray).astype(np.int16)
    return ranks, sorted_u


def hflip(a: np.ndarray) -> np.ndarray:
    return a[:, ::-1].copy()


def crop_x(a: np.ndarray, ox: int, w: int = W_STRIP) -> np.ndarray:
    return a[:, ox : ox + w].copy()


def match_stats(a: np.ndarray, b: np.ndarray, valid: np.ndarray | None = None):
    """Compare rank maps. Returns match%, mismatch_count, total_compared."""
    assert a.shape == b.shape
    if valid is None:
        valid = np.ones(a.shape, dtype=bool)
    n = int(valid.sum())
    if n == 0:
        return 0.0, 0, 0
    mism = int(((a != b) & valid).sum())
    match = n - mism
    pct = 100.0 * match / n
    return pct, mism, n


def save_png(arr_rgba, path):
    Image.fromarray(arr_rgba).save(path)


print("=== Loading ===")
src = load_rgba(SRC160_PATH)
ref = load_rgba(REF224_PATH)
f1l = load_rgba(F1L_PATH)
f1r = load_rgba(F1R_PATH)
mask = load_rgba(MASK_PATH)

assert src.shape[:2] == (111, 160)
assert ref.shape[:2] == (111, 224)
assert f1l.shape[:2] == (111, 60)
assert f1r.shape[:2] == (111, 60)
assert mask.shape[:2] == (111, 60)

left_ref = ref[:, 0:32]
right_ref = ref[:, 192:224]

# Save strips for inspection
save_png(left_ref, OUT / "left_strip_ref_32x111.png")
save_png(right_ref, OUT / "right_strip_ref_32x111.png")

# Rank maps (per-image global)
print("=== Rank maps ===")
src_rank, src_grays = to_rank_map(src)
ref_rank, ref_grays = to_rank_map(ref)
f1l_rank, f1l_grays = to_rank_map(f1l)
f1r_rank, f1r_grays = to_rank_map(f1r)
mask_lum = luminance(mask)
mask_alpha = mask[..., 3]

print(f"  SRC unique grays ({len(src_grays)}): {src_grays}")
print(f"  REF unique grays ({len(ref_grays)}): {ref_grays}")
print(f"  F1L unique grays ({len(f1l_grays)}): {f1l_grays}")
print(f"  F1R unique grays ({len(f1r_grays)}): {f1r_grays}")
print(f"  Mask lum unique sample: min={mask_lum.min():.1f} max={mask_lum.max():.1f} alpha unique={np.unique(mask_alpha)[:10]}")

left_rank = ref_rank[:, 0:32]
right_rank = ref_rank[:, 192:224]

# Flipped SRC160
src_flip = hflip(src)
src_flip_rank = hflip(src_rank)  # same as recompute on flip for ranks if global same set
# Actually flipping preserves unique set so ranks identical mapping
assert np.array_equal(to_rank_map(src_flip)[0], src_flip_rank)

# Center known: REF[32:192] == hflip(SRC160) — quick verify
center_ref = ref_rank[:, 32:192]
pct_c, mism_c, n_c = match_stats(center_ref, src_flip_rank)
print(f"  Sanity center REF vs hflip(SRC): {pct_c:.4f}% match, mism={mism_c}/{n_c}")


def mask_opaque(thresh_mode: str) -> np.ndarray:
    """60x111 bool. thresh_mode: 'gt0' or 'gt127' on luminance; also consider alpha."""
    if thresh_mode == "gt0":
        return mask_lum > 0
    elif thresh_mode == "gt127":
        return mask_lum > 127
    elif thresh_mode == "a_gt0":
        return mask_alpha > 0
    elif thresh_mode == "a_gt127":
        return mask_alpha > 127
    else:
        raise ValueError(thresh_mode)


# Precompute mask modes
MASK_MODES = ["gt0", "gt127", "a_gt0", "a_gt127"]
mask_bools = {m: mask_opaque(m) for m in MASK_MODES}
for m, mb in mask_bools.items():
    print(f"  Mask {m}: opaque={mb.sum()}/{mb.size} ({100*mb.mean():.1f}%)")


def search_full_strip(target_rank: np.ndarray, side_name: str):
    """Search all crops / mirrors / SRC spans / masked combos."""
    results = []

    assets = {
        "F1L": f1l_rank,
        "F1R": f1r_rank,
    }

    # 1-3) contiguous 32-wide crops + mirrored
    for aname, arank in assets.items():
        aw = arank.shape[1]
        for ox in range(0, aw - W_STRIP + 1):  # 0..28
            crop = crop_x(arank, ox)
            for mir in (False, True):
                cand = hflip(crop) if mir else crop
                pct, mism, n = match_stats(target_rank, cand)
                results.append({
                    "kind": "crop",
                    "asset": aname,
                    "ox": ox,
                    "x_range": [ox, ox + 31],
                    "mirrored": mir,
                    "mask": None,
                    "mask_mode": None,
                    "fill": None,
                    "match_pct": pct,
                    "mismatch": mism,
                    "compared": n,
                })

    # Flipped SRC160 contiguous 32-col spans
    sw = src_flip_rank.shape[1]
    for ox in range(0, sw - W_STRIP + 1):
        crop = crop_x(src_flip_rank, ox)
        for mir in (False, True):
            cand = hflip(crop) if mir else crop
            pct, mism, n = match_stats(target_rank, cand)
            results.append({
                "kind": "src_flip_span",
                "asset": "SRC160_hflip",
                "ox": ox,
                "x_range": [ox, ox + 31],
                "mirrored": mir,
                "mask": None,
                "mask_mode": None,
                "fill": None,
                "match_pct": pct,
                "mismatch": mism,
                "compared": n,
            })

    # Also non-flipped SRC spans
    for ox in range(0, src_rank.shape[1] - W_STRIP + 1):
        crop = crop_x(src_rank, ox)
        for mir in (False, True):
            cand = hflip(crop) if mir else crop
            pct, mism, n = match_stats(target_rank, cand)
            results.append({
                "kind": "src_span",
                "asset": "SRC160",
                "ox": ox,
                "x_range": [ox, ox + 31],
                "mirrored": mir,
                "mask": None,
                "mask_mode": None,
                "fill": None,
                "match_pct": pct,
                "mismatch": mism,
                "compared": n,
            })

    # 4) Masked combos
    # For each asset crop (and mirrored crop), apply mask crop at same ox
    # Fill modes: ignore transparent; fill from flipped SRC; don't-care (same as ignore for scoring)
    # Also try mask horizontally flipped

    fill_src_options = []
    # flipped SRC columns that might fill — try each 32-span as fill source
    for fox in range(0, sw - W_STRIP + 1):
        fill_src_options.append(("src_flip", fox, crop_x(src_flip_rank, fox)))
    for fox in range(0, src_rank.shape[1] - W_STRIP + 1):
        fill_src_options.append(("src", fox, crop_x(src_rank, fox)))

    # Limit fill search: also try fill from same crop's "background" — but user said:
    # (a) ignore dest pixels (b) fill from flipped SRC160 columns (c) don't-care among opaque
    # We'll do (a)/(c) as ignore, and (b) with best SRC flip span (search)

    for aname, arank in assets.items():
        aw = arank.shape[1]
        for ox in range(0, aw - W_STRIP + 1):
            crop = crop_x(arank, ox)
            mask_crop = crop_x(mask_bools["gt0"].astype(np.uint8), ox).astype(bool)  # placeholder shape
            for mask_mode in MASK_MODES:
                mfull = mask_bools[mask_mode]
                for mask_flip in (False, True):
                    m60 = hflip(mfull) if mask_flip else mfull
                    m32 = crop_x(m60.astype(np.uint8), ox).astype(bool)
                    for mir in (False, True):
                        cand_base = hflip(crop) if mir else crop
                        # If we mirror the crop, should mask also be mirrored with the crop?
                        # User: "mirrored crops" + "also try mask flipped horizontally"
                        # When mir=True we mirror after extract — apply mask to dest coords:
                        # Option A: mirror mask with crop (m32_eff = hflip(m32) if mir)
                        # Option B: independent mask_flip already covers
                        # Try both: mask_with_crop_mirror True/False
                        for mask_follow_mir in (False, True):
                            m_eff = hflip(m32) if (mir and mask_follow_mir) else m32

                            # (a)/(c) ignore transparent
                            pct, mism, n = match_stats(target_rank, cand_base, valid=m_eff)
                            results.append({
                                "kind": "masked_ignore",
                                "asset": aname,
                                "ox": ox,
                                "x_range": [ox, ox + 31],
                                "mirrored": mir,
                                "mask": True,
                                "mask_mode": mask_mode,
                                "mask_flip": mask_flip,
                                "mask_follow_mir": mask_follow_mir,
                                "fill": "ignore",
                                "match_pct": pct,
                                "mismatch": mism,
                                "compared": n,
                                "opaque_px": int(m_eff.sum()),
                            })

                            # (b) fill transparent from flipped SRC — search best fill span
                            best_fill = None
                            for ftag, fox, fspan in fill_src_options:
                                # optionally mirror fill
                                for fmir in (False, True):
                                    fill = hflip(fspan) if fmir else fspan
                                    cand = np.where(m_eff, cand_base, fill)
                                    pct2, mism2, n2 = match_stats(target_rank, cand)
                                    if best_fill is None or mism2 < best_fill["mismatch"]:
                                        best_fill = {
                                            "kind": "masked_fill",
                                            "asset": aname,
                                            "ox": ox,
                                            "x_range": [ox, ox + 31],
                                            "mirrored": mir,
                                            "mask": True,
                                            "mask_mode": mask_mode,
                                            "mask_flip": mask_flip,
                                            "mask_follow_mir": mask_follow_mir,
                                            "fill": f"{ftag}_ox{fox}_mir{fmir}",
                                            "fill_ox": fox,
                                            "fill_mir": fmir,
                                            "fill_asset": ftag,
                                            "match_pct": pct2,
                                            "mismatch": mism2,
                                            "compared": n2,
                                            "opaque_px": int(m_eff.sum()),
                                        }
                            if best_fill:
                                results.append(best_fill)

    return results


def top_n(results, n=15, key="mismatch"):
    # prefer lower mismatch; for ties higher match_pct / higher compared
    return sorted(results, key=lambda r: (r["mismatch"], -r["match_pct"], -r.get("compared", 0)))[:n]


def best_exact(results):
    exact = [r for r in results if r["mismatch"] == 0 and r["compared"] == TOTAL]
    return exact


print("\n=== Searching LEFT ===")
left_res = search_full_strip(left_rank, "LEFT")
print(f"  candidates: {len(left_res)}")

print("\n=== Searching RIGHT ===")
right_res = search_full_strip(right_rank, "RIGHT")
print(f"  candidates: {len(right_res)}")


def summarize_side(name, target, results):
    print(f"\n######## {name} SIDE ########")
    # Best among full compared (no ignore)
    full = [r for r in results if r.get("compared", 0) == TOTAL or r.get("fill") not in ("ignore",) or r.get("kind") in ("crop", "src_flip_span", "src_span", "masked_fill")]
    # Separate categories
    cats = {
        "crop_F1": [r for r in results if r["kind"] == "crop"],
        "src_flip": [r for r in results if r["kind"] == "src_flip_span"],
        "src": [r for r in results if r["kind"] == "src_span"],
        "masked_ignore": [r for r in results if r["kind"] == "masked_ignore"],
        "masked_fill": [r for r in results if r["kind"] == "masked_fill"],
    }

    report = {"side": name, "categories": {}}
    for cat, lst in cats.items():
        tops = top_n(lst, 8)
        report["categories"][cat] = tops
        print(f"\n--- Best {cat} ---")
        for i, r in enumerate(tops[:5]):
            print(
                f"  #{i+1} mism={r['mismatch']}/{r['compared']} ({r['match_pct']:.4f}%) "
                f"asset={r['asset']} x={r['x_range']} mir={r['mirrored']} "
                f"mask={r.get('mask')} mode={r.get('mask_mode')} mflip={r.get('mask_flip')} "
                f"follow={r.get('mask_follow_mir')} fill={r.get('fill')}"
            )

    # Overall best full 3552 compare
    full_cmp = [r for r in results if r["compared"] == TOTAL]
    best_full = top_n(full_cmp, 5)
    report["best_full"] = best_full
    print(f"\n--- Best FULL-strip (compared=3552) ---")
    for i, r in enumerate(best_full):
        print(
            f"  #{i+1} mism={r['mismatch']}/3552 ({r['match_pct']:.4f}%) "
            f"kind={r['kind']} asset={r['asset']} x={r['x_range']} mir={r['mirrored']} "
            f"mask={r.get('mask')} mode={r.get('mask_mode')} fill={r.get('fill')}"
        )

    exact = [r for r in full_cmp if r["mismatch"] == 0]
    report["exact_full"] = exact[:10]
    print(f"  Exact full matches: {len(exact)}")

    # Best ignore (opaque only) — report coverage
    ign = cats["masked_ignore"]
    # Prefer high coverage + zero or low mism
    ign_sorted = sorted(ign, key=lambda r: (r["mismatch"], -r["compared"]))
    report["best_ignore"] = ign_sorted[:8]
    print(f"\n--- Best masked_ignore (opaque only) ---")
    for i, r in enumerate(ign_sorted[:5]):
        print(
            f"  #{i+1} mism={r['mismatch']}/{r['compared']} opaque={r.get('opaque_px')} ({r['match_pct']:.4f}%) "
            f"asset={r['asset']} x={r['x_range']} mir={r['mirrored']} mode={r.get('mask_mode')} "
            f"mflip={r.get('mask_flip')} follow={r.get('mask_follow_mir')}"
        )

    return report


left_report = summarize_side("LEFT", left_rank, left_res)
right_report = summarize_side("RIGHT", right_rank, right_res)

# 5) Multi-span 2-piece splits
print("\n=== Multi-span 2-piece splits ===")


def search_2piece(target_rank, assets_dict):
    """Split strip at split_x in 1..31: left piece from one crop, right from another.
    Band-unaware full height. Also try same ox for both with different assets.
    Search: for each split_col s in 1..31, for each asset/ox/mir for left piece and right piece.
    This is large — prune: only use F1L/F1R crops (29 ox * 2 mir * 2 assets = 116) per piece
    31 * 116^2 ~ 400k — OK.
    """
    pieces = []
    for aname, arank in assets_dict.items():
        aw = arank.shape[1]
        for ox in range(0, aw - W_STRIP + 1):
            crop = crop_x(arank, ox)
            for mir in (False, True):
                cand = hflip(crop) if mir else crop
                pieces.append((aname, ox, mir, cand))

    # Also SRC flip pieces
    for ox in range(0, src_flip_rank.shape[1] - W_STRIP + 1):
        crop = crop_x(src_flip_rank, ox)
        for mir in (False, True):
            pieces.append(("SRC160_hflip", ox, mir, hflip(crop) if mir else crop))

    best = None
    # Too many if include SRC — 29*2*2 + 129*2 = 116+258=374; 31*374^2 ~ 4.3M — still OK in numpy loops carefully
    # Optimize: for each split, precompute mismatch per column for each piece vs target
    # For each piece, col_mism[x] = sum over y of (piece[:,x] != target[:,x])
    piece_col_cost = []
    for meta in pieces:
        aname, ox, mir, cand = meta
        neq = (cand != target_rank)  # HxW
        col_cost = neq.sum(axis=0)  # W
        piece_col_cost.append((aname, ox, mir, col_cost))

    best = {"mismatch": TOTAL + 1}
    # For each split s (left uses cols 0..s-1, right uses s..31)
    n_pieces = len(piece_col_cost)
    # prefix sums
    prefixes = []
    suffixes = []
    for aname, ox, mir, col_cost in piece_col_cost:
        pref = np.cumsum(col_cost)
        # cost of cols [0, s) = pref[s-1]
        # cost of cols [s, 32) = total - (pref[s-1] if s>0 else 0)
        prefixes.append(pref)
        suffixes.append(col_cost.sum() - pref)  # suffixes[i][s] = cost of cols[s+1:]? 
        # pref[k] = cost cols 0..k inclusive
        # left cost for split s: pref[s-1] if s>=1
        # right cost: sum(col_cost[s:]) = total - (pref[s-1] if s>=1 else 0)

    totals = [int(c[3].sum()) for c in piece_col_cost]

    for s in range(1, W_STRIP):
        left_costs = []
        right_costs = []
        for i, (aname, ox, mir, col_cost) in enumerate(piece_col_cost):
            pref = prefixes[i]
            lc = int(pref[s - 1])
            rc = totals[i] - lc
            left_costs.append(lc)
            right_costs.append(rc)
        # best left piece + best right piece independently
        iL = int(np.argmin(left_costs))
        iR = int(np.argmin(right_costs))
        mism = left_costs[iL] + right_costs[iR]
        if mism < best["mismatch"]:
            aL, oxL, mirL, _ = piece_col_cost[iL][:4] if False else piece_col_cost[iL]
            # unpack properly
            anL, oxL, mirL, _cc = piece_col_cost[iL]
            anR, oxR, mirR, _cc = piece_col_cost[iR]
            best = {
                "split": s,
                "mismatch": mism,
                "match_pct": 100.0 * (TOTAL - mism) / TOTAL,
                "left_piece": {"asset": anL, "ox": oxL, "mirrored": mirL, "cols": [0, s - 1], "cost": left_costs[iL]},
                "right_piece": {"asset": anR, "ox": oxR, "mirrored": mirR, "cols": [s, 31], "cost": right_costs[iR]},
            }
    return best, len(pieces)


assets_fr = {"F1L": f1l_rank, "F1R": f1r_rank}
left_2p, nL = search_2piece(left_rank, assets_fr)
right_2p, nR = search_2piece(right_rank, assets_fr)
print(f"  LEFT best 2-piece: {json.dumps(left_2p, indent=2)}")
print(f"  RIGHT best 2-piece: {json.dumps(right_2p, indent=2)}")

# 3-piece? optional quick — split into 3 contiguous — heavier. Do band-aware instead.

# 6) Per brick bands
print("\n=== Per-band independent best ===")


def search_band(target_full, y0, y1):
    th = y1 - y0 + 1
    target = target_full[y0 : y1 + 1, :]
    tpix = W_STRIP * th
    results = []
    for aname, arank in (("F1L", f1l_rank), ("F1R", f1r_rank), ("SRC160_hflip", src_flip_rank), ("SRC160", src_rank)):
        aw = arank.shape[1]
        for ox in range(0, aw - W_STRIP + 1):
            crop = arank[y0 : y1 + 1, ox : ox + W_STRIP]
            for mir in (False, True):
                cand = crop[:, ::-1] if mir else crop
                pct, mism, n = match_stats(target, cand)
                results.append({
                    "asset": aname, "ox": ox, "x_range": [ox, ox + 31], "mirrored": mir,
                    "match_pct": pct, "mismatch": mism, "compared": n, "band": [y0, y1],
                })
    # masked ignore for band
    for aname, arank in (("F1L", f1l_rank), ("F1R", f1r_rank)):
        aw = arank.shape[1]
        for ox in range(0, aw - W_STRIP + 1):
            for mask_mode in MASK_MODES:
                mfull = mask_bools[mask_mode]
                for mask_flip in (False, True):
                    m60 = mfull[:, ::-1] if mask_flip else mfull
                    m32 = m60[y0 : y1 + 1, ox : ox + W_STRIP]
                    crop = arank[y0 : y1 + 1, ox : ox + W_STRIP]
                    for mir in (False, True):
                        cand = crop[:, ::-1] if mir else crop
                        for mask_follow_mir in (False, True):
                            m_eff = m32[:, ::-1] if (mir and mask_follow_mir) else m32
                            pct, mism, n = match_stats(target, cand, valid=m_eff)
                            results.append({
                                "asset": aname, "ox": ox, "x_range": [ox, ox + 31], "mirrored": mir,
                                "mask": True, "mask_mode": mask_mode, "mask_flip": mask_flip,
                                "mask_follow_mir": mask_follow_mir, "fill": "ignore",
                                "match_pct": pct, "mismatch": mism, "compared": n, "band": [y0, y1],
                            })
                            # fill from src flip best for this band only — try all src spans
                            best_m = None
                            for fox in range(0, src_flip_rank.shape[1] - W_STRIP + 1):
                                for fmir in (False, True):
                                    fill = src_flip_rank[y0 : y1 + 1, fox : fox + W_STRIP]
                                    if fmir:
                                        fill = fill[:, ::-1]
                                    cand2 = np.where(m_eff, cand, fill)
                                    pct2, mism2, n2 = match_stats(target, cand2)
                                    if best_m is None or mism2 < best_m["mismatch"]:
                                        best_m = {
                                            "asset": aname, "ox": ox, "x_range": [ox, ox + 31], "mirrored": mir,
                                            "mask": True, "mask_mode": mask_mode, "mask_flip": mask_flip,
                                            "mask_follow_mir": mask_follow_mir,
                                            "fill": f"src_flip_ox{fox}_mir{fmir}",
                                            "match_pct": pct2, "mismatch": mism2, "compared": n2, "band": [y0, y1],
                                        }
                            if best_m:
                                results.append(best_m)
    return top_n(results, 5), tpix


band_reports = {"LEFT": {}, "RIGHT": {}}
for side, tr in (("LEFT", left_rank), ("RIGHT", right_rank)):
    print(f"\n--- {side} bands ---")
    for y0, y1 in BANDS:
        tops, tpix = search_band(tr, y0, y1)
        band_reports[side][f"{y0}-{y1}"] = tops
        b = tops[0]
        print(
            f"  y={y0}-{y1} BEST mism={b['mismatch']}/{b['compared']} ({b['match_pct']:.4f}%) "
            f"asset={b['asset']} x={b['x_range']} mir={b['mirrored']} "
            f"mask={b.get('mask')} mode={b.get('mask_mode')} fill={b.get('fill')}"
        )
        # also best unmasked crop-only
        unmasked = [r for r in tops if not r.get("mask")]
        # need separate: get best unmasked from full search
        # re-quick
        best_u = None
        for aname, arank in (("F1L", f1l_rank), ("F1R", f1r_rank), ("SRC160_hflip", src_flip_rank), ("SRC160", src_rank)):
            aw = arank.shape[1]
            for ox in range(0, aw - W_STRIP + 1):
                crop = arank[y0 : y1 + 1, ox : ox + W_STRIP]
                for mir in (False, True):
                    cand = crop[:, ::-1] if mir else crop
                    pct, mism, n = match_stats(tr[y0 : y1 + 1], cand)
                    if best_u is None or mism < best_u["mismatch"]:
                        best_u = {"asset": aname, "ox": ox, "mirrored": mir, "mismatch": mism, "compared": n, "match_pct": pct, "x_range": [ox, ox+31]}
        print(f"       best_unmasked: mism={best_u['mismatch']}/{best_u['compared']} asset={best_u['asset']} x={best_u['x_range']} mir={best_u['mirrored']}")

# LEFT vs RIGHT symmetry
print("\n=== LEFT vs RIGHT symmetry ===")
# Is RIGHT hflip of LEFT? (same ref rank space — same image so ranks align)
right_from_left = hflip(left_rank)
pct_sym, mism_sym, n_sym = match_stats(right_rank, right_from_left)
print(f"  RIGHT vs hflip(LEFT): {pct_sym:.4f}% match, mism={mism_sym}/{n_sym}")

# Per-band symmetry
for y0, y1 in BANDS:
    pct_b, mism_b, n_b = match_stats(right_rank[y0:y1+1], hflip(left_rank[y0:y1+1]))
    print(f"  band y={y0}-{y1}: RIGHT vs hflip(LEFT) mism={mism_b}/{n_b} ({pct_b:.4f}%)")

# Check if F1R is hflip of F1L
pct_fr, mism_fr, n_fr = match_stats(f1r_rank, hflip(f1l_rank))
print(f"  F1R vs hflip(F1L): {pct_fr:.4f}% match, mism={mism_fr}/{n_fr}")

# Mask: is it left-sided?
print(f"\n=== Mask profile ===")
for mode in MASK_MODES:
    mb = mask_bools[mode]
    col_occ = mb.sum(axis=0)
    print(f"  {mode} opaque per col (0..59): {col_occ.tolist()}")

# Row-uniform check: does one mapping work all 111 rows?
# For best full candidates, report per-row mismatch
print("\n=== Per-row consistency for top recipes ===")


def per_row_mismatches(target, cand):
    return (target != cand).sum(axis=1).tolist()


def describe_recipe(r):
    return (
        f"{r.get('kind','?')} {r['asset']} x={r.get('x_range')} mir={r.get('mirrored')} "
        f"mask={r.get('mask')} mode={r.get('mask_mode')} mflip={r.get('mask_flip')} "
        f"follow={r.get('mask_follow_mir')} fill={r.get('fill')}"
    )


def materialize_candidate(r, side_target):
    """Rebuild candidate rank map for a result dict when possible."""
    asset_map = {"F1L": f1l_rank, "F1R": f1r_rank, "SRC160_hflip": src_flip_rank, "SRC160": src_rank}
    if r["kind"] in ("crop", "src_flip_span", "src_span"):
        arank = asset_map[r["asset"]]
        ox = r["ox"]
        crop = crop_x(arank, ox)
        cand = hflip(crop) if r["mirrored"] else crop
        return cand, None
    if r["kind"] == "masked_ignore":
        arank = asset_map[r["asset"]]
        ox = r["ox"]
        crop = crop_x(arank, ox)
        cand = hflip(crop) if r["mirrored"] else crop
        mfull = mask_bools[r["mask_mode"]]
        m60 = hflip(mfull) if r.get("mask_flip") else mfull
        m32 = crop_x(m60.astype(np.uint8), ox).astype(bool)
        if r.get("mirrored") and r.get("mask_follow_mir"):
            m32 = hflip(m32)
        return cand, m32
    if r["kind"] == "masked_fill":
        arank = asset_map[r["asset"]]
        ox = r["ox"]
        crop = crop_x(arank, ox)
        cand_base = hflip(crop) if r["mirrored"] else crop
        mfull = mask_bools[r["mask_mode"]]
        m60 = hflip(mfull) if r.get("mask_flip") else mfull
        m32 = crop_x(m60.astype(np.uint8), ox).astype(bool)
        if r.get("mirrored") and r.get("mask_follow_mir"):
            m32 = hflip(m32)
        fill_tag = r.get("fill_asset", "src_flip")
        fox = r["fill_ox"]
        fsrc = src_flip_rank if fill_tag == "src_flip" else src_rank
        fill = crop_x(fsrc, fox)
        if r.get("fill_mir"):
            fill = hflip(fill)
        cand = np.where(m32, cand_base, fill)
        return cand, None
    return None, None


for side, results, target in (("LEFT", left_res, left_rank), ("RIGHT", right_res, right_rank)):
    full_cmp = [r for r in results if r["compared"] == TOTAL]
    best = top_n(full_cmp, 3)
    print(f"\n{side}:")
    for r in best:
        cand, valid = materialize_candidate(r, target)
        if cand is None:
            print(f"  (skip materialize) {describe_recipe(r)}")
            continue
        if valid is not None:
            row_m = ((target != cand) & valid).sum(axis=1)
            rows_bad = int((row_m > 0).sum())
            print(f"  {describe_recipe(r)}")
            print(f"    mism={r['mismatch']}/{r['compared']}; rows_with_any_mism={rows_bad}/111")
        else:
            row_m = (target != cand).sum(axis=1)
            rows_bad = int((row_m > 0).sum())
            print(f"  {describe_recipe(r)}")
            print(f"    mism={r['mismatch']}/3552; rows_with_any_mism={rows_bad}/111; per_row={row_m.tolist()}")

# Combined band recipe score: if we take best unmasked per band independently, total mism
print("\n=== Combined independent band recipes (unmasked crops only) ===")
for side, tr in (("LEFT", left_rank), ("RIGHT", right_rank)):
    total_m = 0
    recipes = []
    for y0, y1 in BANDS:
        best_u = None
        for aname, arank in (("F1L", f1l_rank), ("F1R", f1r_rank), ("SRC160_hflip", src_flip_rank), ("SRC160", src_rank)):
            aw = arank.shape[1]
            for ox in range(0, aw - W_STRIP + 1):
                crop = arank[y0 : y1 + 1, ox : ox + W_STRIP]
                for mir in (False, True):
                    cand = crop[:, ::-1] if mir else crop
                    mism = int((tr[y0 : y1 + 1] != cand).sum())
                    if best_u is None or mism < best_u["mismatch"]:
                        best_u = {"band": [y0, y1], "asset": aname, "ox": ox, "x_range": [ox, ox+31], "mirrored": mir, "mismatch": mism, "compared": W_STRIP * (y1-y0+1)}
        recipes.append(best_u)
        total_m += best_u["mismatch"]
    print(f"  {side} combined band mism={total_m}/3552 ({100*(3552-total_m)/3552:.4f}%)")
    for b in recipes:
        print(f"    y={b['band'][0]}-{b['band'][1]}: {b['asset']} x={b['x_range']} mir={b['mirrored']} mism={b['mismatch']}/{b['compared']}")

# Multi-span per band (2-piece within each band)
print("\n=== Per-band 2-piece splits (F1L/F1R/SRC flip) ===")
for side, tr in (("LEFT", left_rank), ("RIGHT", right_rank)):
    total_m = 0
    print(f"  {side}:")
    for y0, y1 in BANDS:
        # build pieces for this band only
        pieces = []
        for aname, arank in (("F1L", f1l_rank), ("F1R", f1r_rank), ("SRC160_hflip", src_flip_rank), ("SRC160", src_rank)):
            aw = arank.shape[1]
            for ox in range(0, aw - W_STRIP + 1):
                crop = arank[y0 : y1 + 1, ox : ox + W_STRIP]
                for mir in (False, True):
                    cand = crop[:, ::-1] if mir else crop
                    col_cost = (cand != tr[y0 : y1 + 1]).sum(axis=0)
                    pieces.append((aname, ox, mir, col_cost))
        best = {"mismatch": 10**9}
        totals = [int(c[3].sum()) for c in pieces]
        prefixes = [np.cumsum(c[3]) for c in pieces]
        for s in range(1, W_STRIP):
            left_costs = [int(prefixes[i][s - 1]) for i in range(len(pieces))]
            right_costs = [totals[i] - left_costs[i] for i in range(len(pieces))]
            iL = int(np.argmin(left_costs))
            iR = int(np.argmin(right_costs))
            mism = left_costs[iL] + right_costs[iR]
            if mism < best["mismatch"]:
                anL, oxL, mirL, _ = pieces[iL]
                anR, oxR, mirR, _ = pieces[iR]
                best = {
                    "split": s, "mismatch": mism,
                    "L": {"asset": anL, "ox": oxL, "mir": mirL, "cost": left_costs[iL]},
                    "R": {"asset": anR, "ox": oxR, "mir": mirR, "cost": right_costs[iR]},
                }
        th = y1 - y0 + 1
        tpix = W_STRIP * th
        total_m += best["mismatch"]
        print(f"    y={y0}-{y1}: mism={best['mismatch']}/{tpix} split={best['split']} "
              f"L={best['L']} R={best['R']}")
    print(f"  {side} total band-2piece mism={total_m}/3552")

# Save JSON report (compact tops)
def scrub(obj):
    if isinstance(obj, dict):
        return {k: scrub(v) for k, v in obj.items()}
    if isinstance(obj, list):
        return [scrub(x) for x in obj]
    if isinstance(obj, (np.integer, np.floating)):
        return obj.item()
    if isinstance(obj, np.ndarray):
        return obj.tolist()
    return obj

report = {
    "dims": {"strip": [32, 111], "total_px": TOTAL},
    "palette_grays": {"SRC": src_grays, "REF": ref_grays, "F1L": f1l_grays, "F1R": f1r_grays},
    "center_sanity_mism": mism_c,
    "symmetry": {"right_vs_hflip_left_mism": mism_sym, "f1r_vs_hflip_f1l_mism": mism_fr},
    "left_best_full": left_report["best_full"][:5],
    "right_best_full": right_report["best_full"][:5],
    "left_2piece": left_2p,
    "right_2piece": right_2p,
    "bands": band_reports,
    "mask_opaque_counts": {m: int(mask_bools[m].sum()) for m in MASK_MODES},
}
with open(OUT / "report.json", "w", encoding="utf-8") as f:
    json.dump(scrub(report), f, indent=2)

# Write FINDINGS.txt summary with clearest numbers — recompute top lines
lines = []
lines.append("F1 SIDE STRIPS vs F1L/F1R — FINDINGS")
lines.append(f"TOTAL pixels per strip: {TOTAL}")
lines.append(f"Palette ranks — SRC grays {src_grays} ({len(src_grays)}), REF {ref_grays}, F1L {f1l_grays}, F1R {f1r_grays}")
lines.append(f"Center sanity REF[32:192] vs hflip(SRC): mism={mism_c}/{160*111}")
lines.append(f"RIGHT vs hflip(LEFT) palette ranks: mism={mism_sym}/{TOTAL} ({pct_sym:.4f}%)")
lines.append(f"F1R vs hflip(F1L): mism={mism_fr}/{60*111}")
lines.append("")

for side, report_s, results in (("LEFT", left_report, left_res), ("RIGHT", right_report, right_res)):
    lines.append(f"==== {side} ====")
    bf = report_s["best_full"][0]
    lines.append(
        f"BEST FULL: mism={bf['mismatch']}/3552 ({bf['match_pct']:.4f}%) "
        f"kind={bf['kind']} asset={bf['asset']} x={bf['x_range']} mir={bf['mirrored']} "
        f"mask={bf.get('mask')} mode={bf.get('mask_mode')} mflip={bf.get('mask_flip')} "
        f"follow={bf.get('mask_follow_mir')} fill={bf.get('fill')}"
    )
    # best crop only
    bc = top_n([r for r in results if r["kind"] == "crop"], 1)[0]
    lines.append(
        f"BEST CROP: mism={bc['mismatch']}/3552 asset={bc['asset']} x={bc['x_range']} mir={bc['mirrored']}"
    )
    bs = top_n([r for r in results if r["kind"] == "src_flip_span"], 1)[0]
    lines.append(
        f"BEST SRC_hflip span: mism={bs['mismatch']}/3552 x={bs['x_range']} mir={bs['mirrored']}"
    )
    bi = report_s["best_ignore"][0]
    lines.append(
        f"BEST MASKED IGNORE: mism={bi['mismatch']}/{bi['compared']} opaque={bi.get('opaque_px')} "
        f"asset={bi['asset']} x={bi['x_range']} mir={bi['mirrored']} mode={bi['mask_mode']} "
        f"mflip={bi.get('mask_flip')} follow={bi.get('mask_follow_mir')}"
    )
    bm = top_n([r for r in results if r["kind"] == "masked_fill"], 1)[0]
    lines.append(
        f"BEST MASKED FILL: mism={bm['mismatch']}/3552 asset={bm['asset']} x={bm['x_range']} mir={bm['mirrored']} "
        f"mode={bm.get('mask_mode')} mflip={bm.get('mask_flip')} follow={bm.get('mask_follow_mir')} fill={bm.get('fill')}"
    )
    lines.append("")

lines.append(f"LEFT 2-piece: {json.dumps(left_2p)}")
lines.append(f"RIGHT 2-piece: {json.dumps(right_2p)}")

(OUT / "FINDINGS.txt").write_text("\n".join(lines), encoding="utf-8")
print("\nWrote", OUT / "FINDINGS.txt")
print("Wrote", OUT / "report.json")
print("DONE")
